namespace Mudpie.Console.Scripting
{
    using System.Threading;
    using System.Threading.Tasks;

    using JetBrains.Annotations;

    using StackExchange.Redis.Extensions.Core;

    public class Engine
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
        public async Task<Context<T>> RunProgramAsync<T>([NotNull] string programName, CancellationToken cancellationToken = default(CancellationToken))
        {
            var program = await this.LoadProgramAsync(programName);
            if (program == null)
                return Context<T>.Error(program, $"Unable to locate program with name {programName}");

            var context = new Context<T>(program);
            var scriptGlobals = new ContextGlobals();

            await context.RunAsync(scriptGlobals, cancellationToken);
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
