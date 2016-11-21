﻿// --------------------------------------------------------------------------------------------------------------------
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

    using Microsoft.CodeAnalysis.Scripting;

    using Mudpie.Server.Data;

    /// <summary>
    /// An execution context instance of a <see cref="Microsoft.CodeAnalysis.Scripting.Script"/> running in an <see cref="Engine"/>
    /// </summary>
    /// <typeparam name="T">The return type of the script</typeparam>
    internal class StatementContext<T> : Context<T>
    {
        /// <summary>
        /// Gets or sets the statement to evaluate
        /// </summary>
        [CanBeNull]
        private readonly string statement;

        /// <summary>
        /// Initializes a new instance of the <see cref="StatementContext{T}"/> class.
        /// </summary>
        /// <param name="statement">The statement to evaluate</param>
        public StatementContext([NotNull] string statement)
        {
            if (string.IsNullOrWhiteSpace(statement)) throw new ArgumentNullException(nameof(statement));

            this.statement = statement;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="StatementContext{T}"/> class.
        /// </summary>
        /// <param name="statement">The statement that was evaluated</param>
        /// <param name="errorNumber">The general category of error that occurred</param>
        /// <param name="errorMessage">The specific error message that was recorded</param>
        /// <exception cref="ArgumentNullException">Thrown when a null error message is supplied to this constructor</exception>
        private StatementContext([CanBeNull] string statement, ContextErrorNumber errorNumber, [NotNull] string errorMessage)
            : base(errorNumber, errorMessage)
        {
            if (string.IsNullOrWhiteSpace(errorMessage)) throw new ArgumentNullException(nameof(errorMessage));

            this.statement = statement;
        }

        /// <summary>
        /// Gets the name of the program
        /// </summary>
        [CanBeNull]
        public string ProgramName => this.program?.Name;

        /// <summary>
        /// Crafts an execution context object that represents a failed execution due to an error condition
        /// </summary>
        /// <param name="statement">The statement that was evaluated</param>
        /// <param name="errorNumber">The general category of error that occurred</param>
        /// <param name="errorMessage">The specific error message that was recorded</param>
        /// <returns>
        /// The <see cref="Scripting.Context{T}"/> object representing the error conditions
        /// </returns>
        [NotNull, Pure]
        public static StatementContext<T> Error(
            [CanBeNull] string statement,
            ContextErrorNumber errorNumber,
            [NotNull] string errorMessage)
        {
            return new StatementContext<T>(statement, errorNumber, errorMessage);
        }

        [NotNull]
        public async Task RunAsync([CanBeNull] object globals, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(this.statement))
            {
                throw new InvalidOperationException("string.IsNullOrWhiteSpace(this.statement)");
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