using Microsoft.CodeAnalysis.Scripting;

namespace Mudpie.Console.Data
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using JetBrains.Annotations;
    using Microsoft.CodeAnalysis.CSharp.Scripting;

    /// <summary>
    /// A program is a series of lines of code that 
    /// </summary>
    public class Program : ObjectBase
    {
        [NotNull]
        public List<string> ScriptSourceCodeLines { get; set; }
        
        /// <summary>
        /// Once the script is compiled, the finished state is stored here for future executions
        /// </summary>
        [CanBeNull]
        private Script _compiledScript = null;

        public Program(string programName, [NotNull] string scriptSourceCode, bool unauthenticated = false) : base(programName)
        {
            if (scriptSourceCode == null)
                throw new ArgumentNullException(nameof(scriptSourceCode));

            this.ScriptSourceCodeLines = scriptSourceCode.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries).ToList();
            this.UnauthenticatedExecution = unauthenticated;
        }

        public Program(string programName, [NotNull] string[] scriptSourceCodeLines, bool unauthenticated = false) : base(programName)
        {
            if (scriptSourceCodeLines == null)
                throw new ArgumentNullException(nameof(scriptSourceCodeLines));
            if (scriptSourceCodeLines.Length == 0)
                throw new ArgumentException("Empty array", nameof(scriptSourceCodeLines));

            this.ScriptSourceCodeLines = scriptSourceCodeLines.ToList();
            this.UnauthenticatedExecution = unauthenticated;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Program"/> class.
        /// </summary>
        protected Program()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Program"/> class.
        /// </summary>
        /// <param name="programName">The name of the program</param>
        protected Program([NotNull] string programName) : base(programName)
        {
        }

        /// <summary>
        /// Gets or sets a value indicating whether the program allows for capture of user input via 'read' commands in the script.
        /// If this flag is not set on a program, it may produce output, but not accept any input.
        /// </summary>
        public bool Interactive { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether a user who has not yet authenticated can run the program
        /// </summary>
        public bool UnauthenticatedExecution { get; set; }

        public Script<T> Compile<T>()
        {
            if (this._compiledScript == null)
            {
                // Add references
                var scriptOptions = ScriptOptions.Default;
                var mscorlib = typeof(object).Assembly;
                var systemCore = typeof(Enumerable).Assembly;
                scriptOptions = scriptOptions.AddReferences(mscorlib, systemCore);

                var roslynScript = CSharpScript
                    .Create<T>("System.Console.SetOut(__INTERNAL__ScriptOutput);System.Console.SetIn(__INTERNAL__ScriptInput);", globalsType: typeof(Scripting.ContextGlobals));

                foreach (var line in this.ScriptSourceCodeLines)
                    roslynScript = roslynScript.ContinueWith<T>(line);
                roslynScript.WithOptions(scriptOptions).Compile();
                this._compiledScript = roslynScript;
            }

            return (Script<T>)this._compiledScript;
        }
    }
}