// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Program.cs" company="Sean McElroy">
//   Released under the terms of the MIT License
// </copyright>
// <summary>
//   A program is a series of lines of code that can be executed within the MUD process
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Mudpie.Console.Data
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;

    using JetBrains.Annotations;
    using Microsoft.CodeAnalysis.CSharp.Scripting;
    using Microsoft.CodeAnalysis.Scripting;

    /// <summary>
    /// A program is a series of lines of code that can be executed within the MUD process
    /// </summary>
    public class Program : ObjectBase
    {
        /// <summary>
        /// Once the script is compiled, the finished state is stored here for future executions
        /// </summary>
        [CanBeNull]
        private Script _compiledScript;

        /// <summary>
        /// Initializes a new instance of the <see cref="Program"/> class.
        /// </summary>
        /// <param name="programName">The name of the program</param>
        /// <param name="scriptSourceCode">The C# lines of script source code</param>
        /// <param name="unauthenticated">Whether or not the program may be run from an authenticated <see cref="Network.Connection"/></param>
        /// <exception cref="ArgumentNullException">Thrown of the <paramref name="scriptSourceCode"/> is specified as null</exception>
        public Program([NotNull] string programName, [NotNull] string scriptSourceCode, bool unauthenticated = false) : base(programName)
        {
            if (scriptSourceCode == null)
                throw new ArgumentNullException(nameof(scriptSourceCode));

            this.ScriptSourceCodeLines = scriptSourceCode.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries).ToList();
            this.UnauthenticatedExecution = unauthenticated;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Program"/> class.
        /// </summary>
        /// <param name="programName">The name of the program</param>
        /// <param name="scriptSourceCodeLines">The C# lines of script source code</param>
        /// <param name="unauthenticated">Whether or not the program may be run from an authenticated <see cref="Network.Connection"/></param>
        /// <exception cref="ArgumentNullException">Thrown of the <paramref name="scriptSourceCodeLines"/> is specified as null</exception>
        public Program([NotNull] string programName, [NotNull] string[] scriptSourceCodeLines, bool unauthenticated = false) : base(programName)
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
            this.ScriptSourceCodeLines = new List<string>(0);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Program"/> class.
        /// </summary>
        /// <param name="programName">The name of the program</param>
        protected Program([NotNull] string programName) : base(programName)
        {
            this.ScriptSourceCodeLines = new List<string>(0);
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
        public List<string> ScriptSourceCodeLines { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether a user who has not yet authenticated can run the program
        /// </summary>
        public bool UnauthenticatedExecution { get; set; }

        /// <summary>
        /// Compiles the program into a Roslyn Scripting API object that can be executed
        /// </summary>
        /// <typeparam name="T">The return type of the script</typeparam>
        /// <returns>
        /// The <see cref="Script"/> object that can be executed
        /// </returns>
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
                    .Create<T>(";", globalsType: typeof(Scripting.ContextGlobals));
                Debug.Assert(roslynScript != null, "The script object must not be null after constructing it from default banner lines");

                foreach (var line in this.ScriptSourceCodeLines)
                {
                    Debug.Assert(roslynScript != null, "The script object must not be null after appending lines of source script");
                    roslynScript = roslynScript.ContinueWith<T>(line);
                }

                Debug.Assert(roslynScript != null, "The script object must not be null after appending lines of source script");
                roslynScript.WithOptions(scriptOptions).Compile();
                this._compiledScript = roslynScript;
            }

            return (Script<T>)this._compiledScript;
        }
    }
}