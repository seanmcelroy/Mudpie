namespace Mudpie.Console.Scripting
{
    using System.IO;

    using Data;

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
        /// Gets or sets a text writer that can be used to send information back to the triggering object.
        /// </summary>
        public TextWriter Feedback { get; internal set; }
    }
}
