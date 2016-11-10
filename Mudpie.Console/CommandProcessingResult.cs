﻿namespace Mudpie.Console
{
    using System;
    using System.Threading.Tasks;

    using JetBrains.Annotations;

    internal sealed class CommandProcessingResult
    {
        public bool IsHandled { get; set; }
        public bool IsQuitting { get; set; }

        /// <summary>
        /// Gets or sets a value that, if not null, indicates the request was the 
        /// start of a message that should be read until its end,
        /// at which time this function should be invoked on the result.
        /// </summary>
        [CanBeNull]
        public Func<string, CommandProcessingResult, Task<CommandProcessingResult>> MessageHandler { get; set; }

        [CanBeNull]
        public string Message { get; set; }

        public CommandProcessingResult(bool isHandled)
            : this(isHandled, false)
        {
        }
        public CommandProcessingResult(bool isHandled, bool isQuitting)
        {
            this.IsHandled = isHandled;
            this.IsQuitting = isQuitting;
        }
    }
}
