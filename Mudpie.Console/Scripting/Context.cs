namespace Mudpie.Console.Scripting
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    using JetBrains.Annotations;

    using Microsoft.CodeAnalysis.Scripting;

    /// <summary>
    /// An execution context instance of a <see cref="Script"/> running in an <see cref="Engine"/>
    /// </summary>
    internal class Context<T>
    {
        /// <summary>
        /// Gets or sets the script to execute
        /// </summary>
        [CanBeNull]
        private readonly Data.Program _program;

        /// <summary>
        /// Gets or sets the current state of the execution context
        /// </summary>
        public ContextState State { get; private set; }

        [CanBeNull]
        public T ReturnValue { get; private set; }

        [CanBeNull]
        public ContextErrorNumber? ErrorNumber { get; private set; }

        [CanBeNull]
        public string ErrorMessage { get; private set; }

        /// <summary>
        /// Gets the name of the program
        /// </summary>
        public string ProgramName => this._program?.Name;

        /// <summary>
        /// Gets or sets the feedback provided by the output of the executing program
        /// </summary>
        public Queue<string> Feedback { get; private set; } = new Queue<string>();

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
            this.State = ContextState.Loaded;
        }

        private Context([CanBeNull] Data.Program program, ContextErrorNumber errorNumber, [NotNull] string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(errorMessage))
                throw new ArgumentNullException(nameof(errorMessage));

            this._program = program;
            this.ErrorNumber = errorNumber;
            this.ErrorMessage = errorMessage;
            this.State = ContextState.Errored;
        }

        [NotNull, Pure]
        public static Context<T> Error([CanBeNull] Data.Program program, ContextErrorNumber errorNumber, [NotNull] string errorMessage)
        {
            return new Context<T>(program, errorNumber, errorMessage);
        }
            
        internal void AppendFeedback(string feedback)
        {
            if (this.Feedback.Count == 0 || this.Feedback.Peek().EndsWith("\r\n"))
                this.Feedback.Enqueue(feedback);
            else if (feedback.EndsWith("\r\n"))
                this.Feedback.Enqueue(feedback);
            else if (feedback.IndexOf("\r\n", StringComparison.Ordinal) > -1)
            {
                var lines = feedback.Split(new[] { "\r\n" }, StringSplitOptions.None);
                for (int i = 0; i < lines.Length; i++)
                    if (i < lines.Length - 1)
                        this.Feedback.Enqueue(lines[i] + "\r\n");
                    else
                        this.Feedback.Enqueue(lines[i]);
            }
            else
                this.Feedback.Enqueue(feedback);
        }

        [NotNull]
        public async Task RunAsync([CanBeNull] object globals, CancellationToken cancellationToken = default(CancellationToken))
        {
            this.State = ContextState.Running;
            try {
                var state = await this._program.Compile<T>().RunAsync(globals, cancellationToken);
                this.ReturnValue = state.ReturnValue;
                this.State = ContextState.Completed;
            }
            catch (Exception ex)
            {
                this.State = ContextState.Errored;
                this.ErrorMessage = ex.ToString();
            }
        }
    }
}
