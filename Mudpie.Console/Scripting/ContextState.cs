namespace Mudpie.Console.Scripting
{
    internal enum ContextState
    {
        /// <summary>
        /// Unknown or default state
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// A program was loaded into the context, but it has not yet been started
        /// </summary>
        Loaded = 1,

        /// <summary>
        /// A program is running in the context
        /// </summary>
        Running = 2,

        /// <summary>
        /// A program was running in the context, but it is presently paused
        /// </summary>
        Paused = 3,

        /// <summary>
        /// Terminated by the triggering object
        /// </summary>
        Aborted = 4,

        /// <summary>
        /// Ended due to an error condition
        /// </summary>
        Errored = 5,

        /// <summary>
        /// Terminated by an administrative user
        /// </summary>
        Killed = 6,

        /// <summary>
        /// Completed normally
        /// </summary>
        Completed = 7
    }
}
