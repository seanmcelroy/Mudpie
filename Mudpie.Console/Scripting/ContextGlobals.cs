// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ContextGlobals.cs" company="Sean McElroy">
//   
// </copyright>
// <summary>
//   Global context variables passed into a program execution context
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Mudpie.Console.Scripting
{
    using System.IO;

    using Mudpie.Console.Data;

    /// <summary>
    /// Global context variables passed into a program execution context
    /// </summary>
    public class ContextGlobals
    {
        /// <summary>
        /// Gets or sets global variables provided by the running engine, global to the whole MUD instance
        /// </summary>
        public object EngineGlobals { get; internal set; }

        /// <summary>
        /// Gets or sets the unique identifier of the <see cref="ObjectBase"/> that triggered the program execution
        /// </summary>
        public string TriggerId { get; internal set; }

        /// <summary>
        /// Gets or sets the type of the <see cref="ObjectBase"/> that triggered the program execution
        /// </summary>
        /// <example>
        /// Sample values are PLAYER and EXIT
        /// </example>
        public string TriggerType { get; internal set; }

        /// <summary>
        /// Gets or sets the name of the <see cref="ObjectBase"/> that triggered the program execution
        /// </summary>
        public string TriggerName { get; internal set; }

        /// <summary>
        /// Gets or sets a text writer that can be used to receive information from the triggering object.
        /// </summary>
        private MemoryStream __INTERNAL__ScriptInputStream { get; set; } = new MemoryStream(2048);

        public TextReader __INTERNAL__ScriptInput { get; private set; }

        public TextWriter __INTERNAL__ScriptInputWriter { get; private set; }

        /// <summary>
        /// Gets or sets a text writer that can be used to send information back to the triggering object.
        /// </summary>
        public TextWriter __INTERNAL__ScriptOutput { get; internal set; }

        public ContextGlobals()
        {
            this.__INTERNAL__ScriptInput = new PlayerInputTextReader(__INTERNAL__ScriptInputStream);
            this.__INTERNAL__ScriptInputWriter = new StreamWriter(__INTERNAL__ScriptInputStream);
        }
    }
}
