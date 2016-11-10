namespace Mudpie.Console.Scripting
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using JetBrains.Annotations;
    using System.Collections.Generic;

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
        public string ProgramName => _program == null ? null : _program.Name;

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

        public static Context<T> Error([CanBeNull] Data.Program program, ContextErrorNumber errorNumber, [NotNull] string errorMessage)
        {
            return new Context<T>(program, errorNumber, errorMessage);
        }

        internal void AppendFeedback(string feedback)
        {
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
