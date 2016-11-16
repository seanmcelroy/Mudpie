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
    using Mudpie.Server.Data;

    using StackExchange.Redis.Extensions.Core;

    using Program = Mudpie.Console.Program;

    /// <summary>
    /// The scripting engine is the master factory of execution contexts for asynchronously running programs in the MUD
    /// </summary>
    internal class Engine
    {
        /// <summary>
        /// The underlying Redis data store client
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
            if (programRef.Equals(DbRef.NOTHING))
            {
                return Context<T>.Error(null, ContextErrorNumber.ProgramNotSpecified, "No program was supplied");
            }

            var program = await Server.Data.Program.GetAsync(this.redis, programRef);
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
                Debug.Assert(outputStreamWriter != null, "outputStreamWriter != null");

                var scriptGlobals = new ContextGlobals(thisObject, caller, outputStreamWriter, new Libraries.DatabaseLibrary(caller.DbRef, this.redis))
                {
                    ArgString = argString,
                    Args = args,
                    DirectObject = directObject,
                    DirectObjectString = directObjectString,
                    IndirectObject = indirectObject,
                    IndirectObjectString = indirectObjectString,
                    Player = connection?.Identity,
                    PlayerLocation = connection?.Identity == null ? null : (await CacheManager.LookupOrRetrieveAsync(connection.Identity.Location, this.Redis, async d => await Room.GetAsync(this.Redis, d)))?.DataObject,
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
                            outputStream.Position = outputLastPositionRead;
                            var outputString = await outputStreamReader.ReadToEndAsync();
                            if (!string.IsNullOrEmpty(outputString))
                                context.AppendFeedback(outputString);
                            outputLastPositionRead = outputStream.Position;

                            // Send output to trigger, if capable of receiving.
                            while (connection != null && context.Output.Count > 0)
                            {
                                var nextOutput = context.Output.Dequeue();
                                if (nextOutput != null)
                                    await connection.SendAsync(nextOutput);
                            }
                        }

                        Thread.Sleep(100);
                    }
                }, 
                outputCancellationTokenSource.Token);
                appendOutputTask.Start();

                // INPUT
                if (program.Interactive)
                    connection?.RedirectInputToProgram(async input =>
                        {
                            await scriptGlobals.PlayerInputWriterInternal.WriteAsync(input);
                            await scriptGlobals.PlayerInputWriterInternal.FlushAsync();
                        });

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
                outputStream.Position = outputLastPositionRead;
                var feedbackString2 = await outputStreamReader.ReadToEndAsync();
                if (!string.IsNullOrEmpty(feedbackString2))
                    context.AppendFeedback(feedbackString2);
                outputLastPositionRead = outputStream.Position;

                // Send output to trigger, if capable of receiving.
                while (connection != null && context.Output.Count > 0)
                    await connection.SendAsync(context.Output.Dequeue());
            }

            return context;
        }

        /// <summary>
        /// Checks to see whether a <see cref="Data.Program"/> with the given <paramref name="programName"/> exists in the data store
        /// </summary>
        /// <param name="programName">The name of the <see cref="Data.Program"/> to search for in the data store</param>
        /// <returns><see cref="System.Boolean.True"/> if the program with the specified <paramref name="programName"/> was found in the data store; otherwise, <see cref="System.Boolean.False"/></returns>
        [NotNull]
        public async Task<bool> ProgramExistsAsync([NotNull] string programName)
        {
            var normalizedProgramName = programName.ToLowerInvariant();
            return await this.redis.ExistsAsync($"mudpie::program:{normalizedProgramName}");
        }

        [NotNull]
        public async Task SaveProgramAsync([NotNull] Server.Data.Program program)
        {
            var normalizedProgramName = program.Name.ToLowerInvariant();

            // ReSharper disable once PossibleNullReferenceException
            if (await this.redis.ExistsAsync($"mudpie::program:{normalizedProgramName}"))
                // ReSharper disable once PossibleNullReferenceException
                await this.redis.ReplaceAsync($"mudpie::program:{normalizedProgramName}", program);
            else
                // ReSharper disable once PossibleNullReferenceException
                await this.redis.AddAsync($"mudpie::program:{normalizedProgramName}", program);

            // ReSharper disable once PossibleNullReferenceException
            await this.redis.SetAddAsync("mudpie::programs", normalizedProgramName);
        }
    }
}