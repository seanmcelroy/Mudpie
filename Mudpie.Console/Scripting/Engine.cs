// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Engine.cs" company="Sean McElroy">
//   Released under the terms of the MIT License
// </copyright>
// <summary>
//   The scripting engine is the master factory of execution contexts for asynchronously running programs in the MUD
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Mudpie.Console.Scripting
{
    using System.Diagnostics;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    using JetBrains.Annotations;

    using Mudpie.Scripting.Common;
    using Server.Data;

    using StackExchange.Redis.Extensions.Core;

    /// <summary>
    /// The scripting engine is the master factory of execution contexts for asynchronously running programs in the MUD
    /// </summary>
    internal class Engine
    {
        /// <summary>
        /// The underlying data store client
        /// </summary>
        [NotNull]
        private readonly ICacheClient redis;

        /// <summary>
        /// Initializes a new instance of the <see cref="Engine"/> class.
        /// </summary>
        /// <param name="redis">The client to access the data store</param>
        public Engine([NotNull] ICacheClient redis)
        {
            this.redis = redis;
        }
        
        /// <summary>
        /// Gets the client proxy to the data store instance used by the engine
        /// </summary>
        internal ICacheClient Redis => this.redis;

        /// <summary>
        /// Executes the script of a program
        /// </summary>
        /// <typeparam name="T">The return type of the program</typeparam>
        /// <param name="programRef">The <see cref="DbRef"/> of the program to execute</param>
        /// <param name="connection">The connection on which the request to run the command came, if it was triggered by an interactive session</param>
        /// <param name="thisObject">The object on which the verb for the command was found</param>
        /// <param name="caller">The object on which the verb that called the currently-running verb</param>
        /// <param name="verb">The verb; a string, the name by which the currently-running verb was identified.</param>
        /// <param name="argString">A string, everything after the first word of the command</param>
        /// <param name="args">A list of strings, the words in <see cref="argString"/></param>
        /// <param name="directObjectString">A string, the direct object string found during parsing</param>
        /// <param name="directObject">An object, the direct object value found during matching</param>
        /// <param name="prepositionString">A string, the prepositional phrase string found during parsing</param>
        /// <param name="indirectObjectString">A string, the indirect object string found during parsing</param>
        /// <param name="indirectObject">An object, the indirect object value found during matching</param>
        /// <param name="cancellationToken">A cancellation token used to abort the method</param>
        /// <returns>A context object that contains information about the execution of the script, including error conditions or return value</returns>
        [NotNull, ItemNotNull]
        public async Task<Context<T>> RunProgramAsync<T>(
            DbRef programRef,
            [CanBeNull] Network.Connection connection,
            [NotNull] ObjectBase thisObject,
            [NotNull] ObjectBase caller,
            [CanBeNull] string verb,
            [CanBeNull] string argString,
            [CanBeNull] string[] args,
            [CanBeNull] string directObjectString,
            [CanBeNull] ObjectBase directObject,
            [CanBeNull] string prepositionString,
            [CanBeNull] string indirectObjectString,
            [CanBeNull] ObjectBase indirectObject,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (programRef.Equals(DbRef.Nothing))
            {
                return Context<T>.Error(null, ContextErrorNumber.ProgramNotSpecified, "No program was supplied");
            }

            var program = await Program.GetAsync(this.redis, programRef, cancellationToken);
            if (program == null)
            {
                return Context<T>.Error(
                    null,
                    ContextErrorNumber.ProgramNotFound,
                    $"Unable to locate program {programRef}");
            }

            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (connection?.Identity == null && !program.UnauthenticatedExecution)
            {
                return Context<T>.Error(program, ContextErrorNumber.AuthenticationRequired, "No trigger was supplied");
            }

            var context = new Context<T>(program);
            using (var outputStream = new MemoryStream(2048))
            using (var outputStreamReader = new StreamReader(outputStream))
            using (var outputStreamWriter = new StreamWriter(outputStream))
            using (var outputCancellationTokenSource = new CancellationTokenSource())
            {
                Debug.Assert(outputStream != null, "outputStream != null");
                Debug.Assert(outputStreamWriter != null, "outputStreamWriter != null");

                var scriptGlobals = new ContextGlobals(thisObject, caller, outputStreamWriter, new Libraries.DatabaseLibrary(caller, this.redis))
                {
                    ArgString = argString,
                    Args = args,
                    DirectObject = directObject,
                    DirectObjectString = directObjectString,
                    IndirectObject = indirectObject,
                    IndirectObjectString = indirectObjectString,
                    Player = connection?.Identity,
                    PlayerLocation = connection?.Identity == null ? null : (await CacheManager.LookupOrRetrieveAsync<ObjectBase>(connection.Identity.Location, this.Redis, async (d, token) => await Room.GetAsync(this.Redis, d, token), cancellationToken))?.DataObject,
                    PrepositionString = prepositionString,
                    Verb = verb
                };

                var outputLastPositionRead = 0L;

                // OUTPUT
                var appendOutputTask = new Task(
                    async () =>
                        {
                            while (context.State == ContextState.Loaded || context.State == ContextState.Running)
                            {
                                if (context.State == ContextState.Running)
                                {
                                    // Input
                                    await scriptGlobals.PlayerInputWriterInternal.FlushAsync();

                                    // Output
                                    await scriptGlobals.PlayerOutput.FlushAsync();
                                    outputStream.Position = Interlocked.Read(ref outputLastPositionRead);
                                    var outputString = await outputStreamReader.ReadToEndAsync();
                                    if (!string.IsNullOrEmpty(outputString))
                                    {
                                        context.AppendFeedback(outputString);
                                    }

                                    Interlocked.Exchange(ref outputLastPositionRead, outputStream.Position);

                                    // Send output to trigger, if capable of receiving.
                                    while (connection != null && context.Output.Count > 0)
                                    {
                                        var nextOutput = context.Output.Dequeue();
                                        if (nextOutput != null)
                                        {
                                            await connection.SendAsync(nextOutput, cancellationToken);
                                        }
                                    }
                                }

                                Thread.Sleep(100);
                            }
                        }, 
                outputCancellationTokenSource.Token);
                appendOutputTask.Start();

                // INPUT
                if (program.Interactive)
                {
                    connection?.RedirectInputToProgram(
                        async input =>
                            {
                                await scriptGlobals.PlayerInputWriterInternal.WriteAsync(input);
                                await scriptGlobals.PlayerInputWriterInternal.FlushAsync();
                            });
                }

                try
                {
                    await context.RunAsync(scriptGlobals, cancellationToken);
                }
                finally
                {
                    connection?.ResetInputRedirection();
                }

                outputCancellationTokenSource.Cancel();

                // Do one last time to get any last feedback

                // Input
                await scriptGlobals.PlayerInputWriterInternal.FlushAsync();

                // Output
                await scriptGlobals.PlayerOutput.FlushAsync();
                outputStream.Position = Interlocked.Read(ref outputLastPositionRead);
                var feedbackString2 = await outputStreamReader.ReadToEndAsync();
                Interlocked.Exchange(ref outputLastPositionRead, outputStream.Position);
                if (!string.IsNullOrEmpty(feedbackString2))
                {
                    context.AppendFeedback(feedbackString2);
                }

                // Send output to trigger, if capable of receiving.
                while (connection != null && context.Output.Count > 0)
                {
                    await connection.SendAsync(context.Output.Dequeue(), cancellationToken);
                }
            }

            return context;
        }

        /// <summary>
        /// Checks to see whether a <see cref="Mudpie.Server.Data.Program"/> with the given <paramref name="programName"/> exists in the data store
        /// </summary>
        /// <param name="programName">The name of the <see cref="Mudpie.Server.Data.Program"/> to search for in the data store</param>
        /// <returns><see cref="System.Boolean.True"/> if the program with the specified <paramref name="programName"/> was found in the data store; otherwise, <see cref="System.Boolean.False"/></returns>
        [NotNull]
        public async Task<bool> ProgramExistsAsync([NotNull] string programName)
        {
            var normalizedProgramName = programName.ToLowerInvariant();
            return await this.redis.ExistsAsync($"mudpie::program:{normalizedProgramName}");
        }

        [NotNull]
        public async Task SaveProgramAsync([NotNull] Program program)
        {
            var normalizedProgramName = program.Name.ToLowerInvariant();

            // ReSharper disable once PossibleNullReferenceException
            if (await this.redis.ExistsAsync($"mudpie::program:{normalizedProgramName}"))
            {
                // ReSharper disable once PossibleNullReferenceException
                await this.redis.ReplaceAsync($"mudpie::program:{normalizedProgramName}", program);
            }
            else
            {
                // ReSharper disable once PossibleNullReferenceException
                await this.redis.AddAsync($"mudpie::program:{normalizedProgramName}", program);
            }

            // ReSharper disable once PossibleNullReferenceException
            await this.redis.SetAddAsync("mudpie::programs", normalizedProgramName);
        }
    }
}