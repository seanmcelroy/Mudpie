namespace Mudpie.Console.Scripting
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using JetBrains.Annotations;

    using Microsoft.CodeAnalysis.CSharp.Scripting;
    using Microsoft.CodeAnalysis.Scripting;

    /// <summary>
    /// An execution context instance of a <see cref="Script"/> running in an <see cref="Engine"/>
    /// </summary>
    public class Context<T>
    {
        /// <summary>
        /// Gets or sets the script to execute
        /// </summary>
        [CanBeNull]
        private readonly Data.Program _program;

        [CanBeNull]
        public T ReturnValue { get; private set; }

        [CanBeNull]
        public string ErrorMessage { get; private set; }

        /// <summary>
        /// Once the script is compiled, the finished state is stored here for future executions
        /// </summary>
        [CanBeNull]
        private Script<T> _compiledScript = null;

        /// <summary>
        /// Initializes a new instance of the <see cref="Context{T}"/> class.
        /// </summary>
        /// <param name="script">
        /// The script to execute
        /// </param>
        public Context([NotNull] Data.Program program)
        {
            if (program == null)
                throw new ArgumentNullException(nameof(program));

            this._program = program;
        }

        private Context([CanBeNull] Data.Program program, [NotNull] string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(errorMessage))
                throw new ArgumentNullException(nameof(errorMessage));

            this._program = program;
            this.ErrorMessage = errorMessage;
        }

        public static Context<T> Error([CanBeNull] Data.Program program, [NotNull] string errorMessage)
        {
            return new Context<T>(program, errorMessage);
        }

        [NotNull]
        public async Task RunAsync([CanBeNull] object globals, CancellationToken cancellationToken = default(CancellationToken))
        {
            // Add references
            var scriptOptions = ScriptOptions.Default;
            var mscorlib = typeof(object).Assembly;
            var systemCore = typeof(Enumerable).Assembly;
            scriptOptions = scriptOptions.AddReferences(mscorlib, systemCore);

            if (this._compiledScript == null)
            {
                var roslynScript = CSharpScript.Create<T>(this._program.ScriptSourceCodeLines.Aggregate((c, n) => c + n), globalsType: typeof(ContextGlobals));
                roslynScript.Compile();
                this._compiledScript = roslynScript;
            }

            var state = await this._compiledScript.WithOptions(scriptOptions).RunAsync(globals, cancellationToken);

            this.ReturnValue = state.ReturnValue;
        }
    }
}
