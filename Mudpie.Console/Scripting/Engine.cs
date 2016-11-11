namespace Mudpie.Console.Scripting
{
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    using JetBrains.Annotations;

    using Mudpie.Console.Data;

    using StackExchange.Redis.Extensions.Core;

    internal class Engine
    {
        [NotNull]
        private ICacheClient redis;

        [CanBeNull]
        private object engineGlobals;

        public Engine([NotNull] ICacheClient redis, [CanBeNull] object engineGlobals = null)
        {
            this.redis = redis;
        }

        [NotNull, ItemNotNull]
        public async Task<Context<T>> RunProgramAsync<T>([NotNull] string programName, [CanBeNull] ObjectBase trigger, [CanBeNull] Network.Connection connection, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrWhiteSpace(programName))
                return Context<T>.Error(null, ContextErrorNumber.ProgramNotSpecified, "No program name was supplied");

            var program = await this.LoadProgramAsync(programName);
            if (program == null)
                return Context<T>.Error(program, ContextErrorNumber.ProgramNotFound, $"Unable to locate program with name {programName}");

            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (trigger == null && !program.UnauthenticatedExecution)
                return Context<T>.Error(null, ContextErrorNumber.AuthenticationRequired, "No trigger was supplied");

            var context = new Context<T>(program);
            using (var outputStream = new MemoryStream(2048))
            using (var outputStreamReader = new StreamReader(outputStream))
            using (var outputStreamWriter = new StreamWriter(outputStream))
            using (var outputCancellationTokenSource = new CancellationTokenSource())
            {
                var scriptGlobals = new ContextGlobals
                {
                    EngineGlobals = this.engineGlobals,
                    TriggerId = trigger?.Id,
                    TriggerName = trigger?.Name,
                    TriggerType = trigger is Player ? "PLAYER" : "?",
                    __INTERNAL__ScriptOutput = outputStreamWriter
                };

                var outputLastPositionRead = 0L;

                // OUTPUT
                var appendOutputTask = new Task(async () =>
                {
                    while (context.State == ContextState.Loaded || context.State == ContextState.Running)
                    {
                        if (context.State == ContextState.Running)
                        {
                            await scriptGlobals.__INTERNAL__ScriptOutput.FlushAsync();
                            outputStream.Position = outputLastPositionRead;
                            var outputString = await outputStreamReader.ReadToEndAsync();
                            if (!string.IsNullOrEmpty(outputString))
                                context.AppendFeedback(outputString);
                            outputLastPositionRead = outputStream.Position;

                            // Send output to trigger, if capable of receiving.
                            while (connection != null && context.Output.Count > 0)
                                await connection.SendAsync(context.Output.Dequeue());
                        }
                        Thread.Sleep(100);
                    }
                }, outputCancellationTokenSource.Token);
                appendOutputTask.Start();

                // INPUT
                if (program.Interactive && connection != null)
                {
                    connection.RedirectInputToProgram(async input =>
                    {
                        await scriptGlobals.__INTERNAL__ScriptInputWriter.WriteAsync(input);
                        await scriptGlobals.__INTERNAL__ScriptInputWriter.FlushAsync();
                    });
                }

                try
                {
                    await context.RunAsync(scriptGlobals, cancellationToken);
                }
                finally
                {
                    connection.ResetInputRedirection();
                }

                outputCancellationTokenSource.Cancel();
                // Do one last time to get any last feedback

                await scriptGlobals.__INTERNAL__ScriptOutput.FlushAsync();
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
        /// Loads a <see cref="Data.Program"/> from the Redis store
        /// </summary>
        /// <param name="programName">The name of the <see cref="Data.Program"/> to load</param>
        /// <returns>The <see cref="Data.Program"/> if found; otherwise, null</returns>
        [NotNull, Pure, ItemCanBeNull]
        public async Task<Program> LoadProgramAsync([NotNull] string programName)
        {
            var normalizedProgramName = programName.ToLowerInvariant();

            if (await this.redis.ExistsAsync($"mudpie::program:{normalizedProgramName}"))
                return await this.redis.GetAsync<Program>($"mudpie::program:{normalizedProgramName}");

            return null;
        }

        [NotNull]
        public async Task SaveProgramAsync([NotNull] Program program)
        {
            var normalizedProgramName = program.Name.ToLowerInvariant();

            if (await this.redis.ExistsAsync($"mudpie::program:{normalizedProgramName}"))
                await this.redis.ReplaceAsync($"mudpie::program:{normalizedProgramName}", program);
            else
                await this.redis.AddAsync($"mudpie::program:{normalizedProgramName}", program);

            await this.redis.SetAddAsync("mudpie::programs", normalizedProgramName);
        }
    }
}