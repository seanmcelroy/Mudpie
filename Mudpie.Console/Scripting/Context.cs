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
    using System.Threading;
    using System.Threading.Tasks;

    using JetBrains.Annotations;

    using Mudpie.Server.Data;

    /// <summary>
    /// An execution context instance of a <see cref="Microsoft.CodeAnalysis.Scripting.Script"/> running in an <see cref="Engine"/>
    /// </summary>
    /// <typeparam name="T">The return type of the script</typeparam>
    internal class Context<T>
    {
        /// <summary>
        /// Gets or sets the script to execute
        /// </summary>
        [CanBeNull]
        private readonly Program program;

        /// <summary>
        /// Initializes a new instance of the <see cref="Context{T}"/> class.
        /// </summary>
        /// <param name="program">The script to execute</param>
        public Context([NotNull] Program program)
        {
            if (program == null) throw new ArgumentNullException(nameof(program));

            this.program = program;
            this.State = ContextState.Loaded;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Context{T}"/> class.
        /// </summary>
        /// <param name="program">The program that was executed</param>
        /// <param name="errorNumber">The general category of error that occurred</param>
        /// <param name="errorMessage">The specific error message that was recorded</param>
        /// <exception cref="ArgumentNullException">Thrown when a null error message is supplied to this constructor</exception>
        private Context([CanBeNull] Program program, ContextErrorNumber errorNumber, [NotNull] string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(errorMessage)) throw new ArgumentNullException(nameof(errorMessage));

            this.program = program;
            this.ErrorNumber = errorNumber;
            this.ErrorMessage = errorMessage;
            this.State = ContextState.Errored;
        }

        /// <summary>
        /// Gets the current state of the execution context
        /// </summary>
        public ContextState State { get; private set; }

        /// <summary>
        /// Gets the return value from the completed execution context
        /// </summary>
        [CanBeNull]
        public T ReturnValue { get; private set; }

        /// <summary>
        /// Gets the general category of the error, if one was observed
        /// </summary>
        [CanBeNull]
        public ContextErrorNumber? ErrorNumber { get; private set; }

        [CanBeNull]
        public string ErrorMessage { get; private set; }

        /// <summary>
        /// Gets the name of the program
        /// </summary>
        public string ProgramName => this.program?.Name;

        /// <summary>
        /// Gets the feedback provided by the output of the executing program
        /// </summary>
        public Queue<string> Output { get; } = new Queue<string>();

        /// <summary>
        /// Crafts an execution context object that represents a failed execution due to an error condition
        /// </summary>
        /// <param name="program">The program that was executed</param>
        /// <param name="errorNumber">The general category of error that occurred</param>
        /// <param name="errorMessage">The specific error message that was recorded</param>
        /// <returns>
        /// The <see cref="Scripting.Context{T}"/> object representing the error conditions
        /// </returns>
        [NotNull, Pure]
        public static Context<T> Error(
            [CanBeNull] Program program,
            ContextErrorNumber errorNumber,
            [NotNull] string errorMessage)
        {
            return new Context<T>(program, errorNumber, errorMessage);
        }

        [NotNull]
        public async Task RunAsync([CanBeNull] object globals, CancellationToken cancellationToken)
        {
            if (this.program == null)
            {
                throw new InvalidOperationException("this.program == null");
            }

            this.State = ContextState.Running;
            try
            {
                var state = await this.program.Compile().RunAsync(globals, cancellationToken);
                if (state.ReturnValue != null)
                {
                    this.ReturnValue = (T)state.ReturnValue;
                }

                this.State = ContextState.Completed;
            }
            catch (Exception ex)
            {
                this.State = ContextState.Errored;
                this.ErrorMessage = ex.ToString();
            }
        }

        internal void AppendFeedback([NotNull] string feedback)
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