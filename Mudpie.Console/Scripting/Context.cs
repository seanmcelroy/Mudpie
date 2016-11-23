// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Context.cs" company="Sean McElroy">
//   Released under the terms of the MIT License
// </copyright>
// <summary>
//   An execution context instance of a <see cref="Script" /> running in an <see cref="Engine" />
// </summary>
// --------------------------------------------------------------------------------------------------------------------
namespace Mudpie.Console.Scripting
{
    using System;
    using System.Collections.Generic;

    using JetBrains.Annotations;

    /// <summary>
    /// An execution context instance of a <see cref="Microsoft.CodeAnalysis.Scripting.Script"/> running in an <see cref="Engine"/>
    /// </summary>
    /// <typeparam name="T">The return type of the script</typeparam>
    internal abstract class Context<T>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Context{T}"/> class.
        /// </summary>
        protected Context()
        {
            this.State = ContextState.Loaded;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Context{T}"/> class.
        /// </summary>
        /// <param name="errorNumber">The general category of error that occurred</param>
        /// <param name="errorMessage">The specific error message that was recorded</param>
        /// <exception cref="ArgumentNullException">Thrown when a null error message is supplied to this constructor</exception>
        protected Context(ContextErrorNumber errorNumber, [NotNull] string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(errorMessage)) throw new ArgumentNullException(nameof(errorMessage));

            this.ErrorNumber = errorNumber;
            this.ErrorMessage = errorMessage;
            this.State = ContextState.Errored;
        }

        /// <summary>
        /// Gets or sets the current state of the execution context
        /// </summary>
        public ContextState State { get; protected set; }

        /// <summary>
        /// Gets or sets the return value from the completed execution context
        /// </summary>
        [CanBeNull]
        public T ReturnValue { get; protected set; }

        /// <summary>
        /// Gets the general category of the error, if one was observed
        /// </summary>
        [CanBeNull]
        public ContextErrorNumber? ErrorNumber { get; private set; }

        /// <summary>
        /// Gets or sets the specific message for the error, if one was observed
        /// </summary>
        [CanBeNull]
        public string ErrorMessage { get; protected set; }

        /// <summary>
        /// Gets the feedback provided by the output of the executing program
        /// </summary>
        [NotNull]
        public Queue<string> Output { get; } = new Queue<string>();

        protected internal void AppendFeedback([NotNull] string feedback)
        {
            if (this.Output.Count == 0 || this.Output.Peek().EndsWith("\r\n")) this.Output.Enqueue(feedback);
            else if (feedback.EndsWith("\r\n")) this.Output.Enqueue(feedback);
            else if (feedback.IndexOf("\r\n", StringComparison.Ordinal) > -1)
            {
                var lines = feedback.Split(new[] { "\r\n" }, StringSplitOptions.None);
                for (int i = 0; i < lines.Length; i++)
                    if (i < lines.Length - 1) this.Output.Enqueue(lines[i] + "\r\n");
                    else this.Output.Enqueue(lines[i]);
            }
            else this.Output.Enqueue(feedback);
        }
    }
}