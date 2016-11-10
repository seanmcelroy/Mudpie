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
        public async Task<Context<T>> RunProgramAsync<T>([NotNull] string programName, [CanBeNull] ObjectBase trigger, CancellationToken cancellationToken = default(CancellationToken))
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
            var feedbackStream = new MemoryStream(2048);
            var scriptGlobals = new ContextGlobals
                                    {
                                        EngineGlobals = this.engineGlobals,
                                        TriggerId = trigger?.Id,
                                        TriggerName = trigger?.Name,
                                        TriggerType = trigger is Player ? "PLAYER" : "?",
                                        Feedback = new StreamWriter(feedbackStream)
                                    };

            await context.RunAsync(scriptGlobals, cancellationToken);

            await scriptGlobals.Feedback.FlushAsync();
            feedbackStream.Position = 0;
            using (var sr = new StreamReader(feedbackStream))
            {
                var feedbackString = await sr.ReadToEndAsync();
                if (!string.IsNullOrEmpty(feedbackString))
                    context.AppendFeedback(feedbackString);
            }

            return context;
        }

        /// <summary>
        /// Loads a <see cref="Data.Program"/> from the Redis store
        /// </summary>
        /// <param name="programName">The name of the <see cref="Data.Program"/> to load</param>
        /// <returns>The <see cref="Data.Program"/> if found; otherwise, null</returns>
        [NotNull, Pure, ItemCanBeNull]
        public async Task<Data.Program> LoadProgramAsync([NotNull] string programName)
        {
            var normalizedProgramName = programName.ToLowerInvariant();

            if (await redis.ExistsAsync($"mudpie::program:{normalizedProgramName}"))
                return await redis.GetAsync<Data.Program>($"mudpie::program:{normalizedProgramName}");

            return null;
        }

        [NotNull]
        public async Task SaveProgramAsync([NotNull] Data.Program program)
        {
            var normalizedProgramName = program.Name.ToLowerInvariant();

            if (await redis.ExistsAsync($"mudpie::program:{normalizedProgramName}"))
                await redis.ReplaceAsync($"mudpie::program:{normalizedProgramName}", program);
            else
                await redis.AddAsync($"mudpie::program:{normalizedProgramName}", program);

            await redis.SetAddAsync("mudpie::programs", normalizedProgramName);
        }
    }
}
