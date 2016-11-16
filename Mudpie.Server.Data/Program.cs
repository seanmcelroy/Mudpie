// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Program.cs" company="Sean McElroy">
//   Released under the terms of the MIT License
// </copyright>
// <summary>
//   A program is a series of lines of code that can be executed within the MUD process
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Mudpie.Server.Data
{
    using System;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading.Tasks;

    using JetBrains.Annotations;

    using log4net;

    using Microsoft.CodeAnalysis.CSharp.Scripting;
    using Microsoft.CodeAnalysis.Scripting;

    using Scripting.Common;

    using StackExchange.Redis.Extensions.Core;

    /// <summary>
    /// A program is a series of lines of code that can be executed within the MUD process
    /// </summary>
    public class Program : ObjectBase
    {
        /// <summary>
        /// The logging utility instance to use to log events from this class
        /// </summary>
        [NotNull]
        private static readonly ILog Logger = LogManager.GetLogger(typeof(Program));

        /// <summary>
        /// Once the script is compiled, the finished state is stored here for future executions
        /// </summary>
        [CanBeNull]
        private Script compiledScript;

        /// <summary>
        /// Initializes a new instance of the <see cref="Program"/> class.
        /// </summary>
        /// <param name="programName">The name of the program</param>
        /// <param name="owner">The reference of the owner of the object</param>
        /// <param name="scriptSource">The C# lines of script source code</param>
        /// <param name="unauthenticated">Whether or not the program may be run from an authenticated connection</param>
        /// <exception cref="ArgumentNullException">Thrown of the <paramref name="scriptSource"/> is specified as null</exception>
        public Program([NotNull] string programName, DbRef owner, [NotNull] string scriptSource, bool unauthenticated = false)
            : base(programName, owner)
        {
            if (programName == null)
                throw new ArgumentNullException(nameof(programName));
            if (scriptSource == null)
                throw new ArgumentNullException(nameof(scriptSource));

            this.ScriptSource = scriptSource;
            this.UnauthenticatedExecution = unauthenticated;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Program"/> class.
        /// </summary>
        [Obsolete("Only made public for a generic type parameter requirement", false)]
        public Program()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Program"/> class.
        /// </summary>
        /// <param name="programName">The name of the program</param>
        /// <param name="owner">The reference of the owner of the object</param>
        protected Program([NotNull] string programName, DbRef owner)
            : base(programName, owner)
        {
        }

        /// <summary>
        /// Gets or sets a value indicating whether the program allows for capture of user input via 'read' commands in the script.
        /// If this flag is not set on a program, it may produce output, but not accept any input.
        /// </summary>
        public bool Interactive { get; set; }

        /// <summary>
        /// Gets or sets the lines of the C# source code for this program
        /// </summary>
        [NotNull]
        public string ScriptSource { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether a user who has not yet authenticated can run the program
        /// </summary>
        public bool UnauthenticatedExecution { get; set; }

        /// <summary>
        /// Loads a <see cref="Data.Program"/> from the cache or the data store
        /// </summary>
        /// <param name="redis">The client proxy to the underlying data store</param>
        /// <param name="programRef">The <see cref="DbRef"/> of the <see cref="Data.Program"/> to load</param>
        /// <returns>The <see cref="Data.Program"/> if found; otherwise, null</returns>
        [NotNull, Pure, ItemCanBeNull]
        public static new async Task<Program> GetAsync([NotNull] ICacheClient redis, DbRef programRef) => (Program)(await CacheManager.LookupOrRetrieveAsync(programRef, redis, async d => await redis.GetAsync<Program>($"mudpie::program:{d}"))).DataObject;

        /// <summary>
        /// Compiles the program into a Roslyn Scripting API object that can be executed
        /// </summary>
        /// <typeparam name="T">The return type of the script</typeparam>
        /// <returns>
        /// The <see cref="Script"/> object that can be executed
        /// </returns>
        [NotNull]
        public Script<T> Compile<T>()
        {
            if (this.compiledScript == null)
            {
                Logger.InfoFormat("Compiling program {0}... ", this.Name);
                var sw = new Stopwatch();
                sw.Start();

                // Add references
                var scriptOptions = ScriptOptions.Default;
                var mscorlib = typeof(object).Assembly;
                var systemCore = typeof(Enumerable).Assembly;
                var scriptingCommon = typeof(DbRef).Assembly;
                scriptOptions = scriptOptions.AddReferences(mscorlib, systemCore, scriptingCommon);

                var roslynScript = CSharpScript.Create<T>(this.ScriptSource, globalsType: typeof(ContextGlobals));
                Debug.Assert(roslynScript != null, "The script object must not be null after constructing it from default banner lines");
                
                roslynScript.WithOptions(scriptOptions).Compile();
                this.compiledScript = roslynScript;

                sw.Stop();
                Logger.InfoFormat("Compiled program {0} in {1:N2} seconds", this.Name, sw.Elapsed.TotalSeconds);
            }

            return (Script<T>)this.compiledScript;
        }

        /// <inheritdoc />
        public override async Task SaveAsync(ICacheClient redis)
        {
            if (redis == null)
            {
                throw new ArgumentNullException(nameof(redis));
            }

            await
                Task.WhenAll(
                    redis.SetAddAsync<string>("mudpie::programs", this.DbRef),
                    redis.AddAsync($"mudpie::program:{this.DbRef}", this),
                    CacheManager.UpdateAsync(this.DbRef, redis, this));
        }
    }
}