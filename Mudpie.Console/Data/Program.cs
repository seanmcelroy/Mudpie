namespace Mudpie.Console.Data
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using JetBrains.Annotations;

    /// <summary>
    /// A program is a series of lines of code that 
    /// </summary>
    public class Program : ObjectBase
    {
        [NotNull]
        public List<string> ScriptSourceCodeLines { get; set; }

        public Program(string programName, [NotNull] string scriptSourceCode) : base(programName)
        {
            if (scriptSourceCode == null)
                throw new ArgumentNullException(nameof(scriptSourceCode));

            this.ScriptSourceCodeLines = scriptSourceCode.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries).ToList();
        }

        public Program(string programName, [NotNull] string[] scriptSourceCodeLines) : base(programName)
        {
            if (scriptSourceCodeLines == null)
                throw new ArgumentNullException(nameof(scriptSourceCodeLines));
            if (scriptSourceCodeLines.Length == 0)
                throw new ArgumentException("Empty array", nameof(scriptSourceCodeLines));

            this.ScriptSourceCodeLines = scriptSourceCodeLines.ToList();
        }

        protected Program()
            : base()
        {
        }

        protected Program(string programName) : base(programName)
        {
        }
    }
}