// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ProgramContext.cs" company="Sean McElroy">
//   Released under the terms of the MIT License
// </copyright>
// <summary>
//   An execution context instance of a <see cref="Script" /> running in an <see cref="Engine" />
// </summary>
// --------------------------------------------------------------------------------------------------------------------
namespace Mudpie.Console.Scripting
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using JetBrains.Annotations;

    using Microsoft.CodeAnalysis.Scripting;

    using Server.Data;

    /// <summary>
    /// An execution context instance of a <see cref="Microsoft.CodeAnalysis.Scripting.Script"/> running in an <see cref="Engine"/>
    /// </summary>
    /// <typeparam name="T">The return type of the script</typeparam>
    internal class ProgramContext<T> : Context<T>
    {
        /// <summary>
        /// Gets or sets the script to execute
        /// </summary>
        [CanBeNull]
        private readonly Program program;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProgramContext{T}"/> class.
        /// </summary>
        /// <param name="program">The script to execute</param>
        public ProgramContext([NotNull] Program program)
        {
            if (program == null) throw new ArgumentNullException(nameof(program));

            this.program = program;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ProgramContext{T}"/> class.
        /// </summary>
        /// <param name="program">The program that was executed</param>
        /// <param name="errorNumber">The general category of error that occurred</param>
        /// <param name="errorMessage">The specific error message that was recorded</param>
        /// <exception cref="ArgumentNullException">Thrown when a null error message is supplied to this constructor</exception>
        private ProgramContext([CanBeNull] Program program, ContextErrorNumber errorNumber, [NotNull] string errorMessage)
            : base(errorNumber, errorMessage)
        {
            if (string.IsNullOrWhiteSpace(errorMessage)) throw new ArgumentNullException(nameof(errorMessage));

            this.program = program;
        }

        /// <summary>
        /// Gets the name of the program
        /// </summary>
        [CanBeNull]
        public string ProgramName => this.program?.Name;

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
        public static ProgramContext<T> Error(
            [CanBeNull] Program program,
            ContextErrorNumber errorNumber,
            [NotNull] string errorMessage)
        {
            return new ProgramContext<T>(program, errorNumber, errorMessage);
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
            catch (CompilationErrorException ex)
            {
                this.State = ContextState.Errored;
                this.ErrorMessage = ex.Message;
            }
            catch (Exception ex)
            {
                this.State = ContextState.Errored;
                this.ErrorMessage = ex.ToString();
            }
        }
    }
}