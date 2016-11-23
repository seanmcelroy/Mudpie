// --------------------------------------------------------------------------------------------------------------------
// <copyright file="LogUtility.cs" company="Sean McElroy">
//   Released under the terms of the MIT License
// </copyright>
// <summary>
//   A utility class that provides extension methods to log4net that allow for Trace and Verbose logging levels.
// </summary>
// --------------------------------------------------------------------------------------------------------------------
namespace Mudpie.Console
{
    using System;
    using System.Reflection;

    using JetBrains.Annotations;

    using log4net;

    /// <summary>
    /// A utility class that provides extension methods to log4net that allow for Trace and Verbose logging levels.
    /// </summary>
    public static class LogUtility
    {
        /// <summary>
        /// Provides a simple 'Trace' level logging of a message
        /// </summary>
        /// <param name="log">The logger to use to log the message</param>
        /// <param name="message">The message to log</param>
        /// <exception cref="TargetException">Thrown when the current method cannot be determined through reflection</exception>
        [PublicAPI]
        public static void Trace([NotNull] this ILog log, [NotNull] string message)
        {
            if (log == null)
            {
                throw new ArgumentNullException(nameof(log));
            }

            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            log.Logger?.Log(
                MethodBase.GetCurrentMethod()?.DeclaringType,
                log4net.Core.Level.Trace,
                message,
                null);
        }

        /// <summary>
        /// Provides a simple 'Trace' level logging of a message using a format string and arguments
        /// </summary>
        /// <param name="log">The logger to use to log the message</param>
        /// <param name="format">The format string for the message to log</param>
        /// <param name="args">The format arguments used to formulate the message to log</param>
        /// <exception cref="TargetException">Thrown when the current method cannot be determined through reflection</exception>
        /// <exception cref="ArgumentNullException">Thrown when the format string is null</exception>
        /// <exception cref="FormatException">Thrown when the format string and associated arguments cannot be used to create a formatted message</exception>
        [PublicAPI]
        public static void TraceFormat([NotNull] this ILog log, [NotNull] string format, [NotNull] params object[] args)
        {
            if (log == null)
            {
                throw new ArgumentNullException(nameof(log));
            }

            if (format == null)
            {
                throw new ArgumentNullException(nameof(format));
            }

            if (args == null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            log.Trace(string.Format(format, args));
        }

        /// <summary>
        /// Provides a simple 'Verbose' level logging of a message
        /// </summary>
        /// <param name="log">The logger to use to log the message</param>
        /// <param name="message">The message to log</param>
        /// <exception cref="TargetException">Thrown when the current method cannot be determined through reflection</exception>
        [PublicAPI]
        public static void Verbose([NotNull] this ILog log, [NotNull] string message)
        {
            if (log == null)
            {
                throw new ArgumentNullException(nameof(log));
            }

            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            log.Logger?.Log(
                MethodBase.GetCurrentMethod()?.DeclaringType,
                log4net.Core.Level.Verbose,
                message,
                null);
        }

        /// <summary>
        /// Provides a simple 'Verbose' level logging of a message using a format string and arguments
        /// </summary>
        /// <param name="log">The logger to use to log the message</param>
        /// <param name="format">The format string for the message to log</param>
        /// <param name="args">The format arguments used to formulate the message to log</param>
        /// <exception cref="TargetException">Thrown when the current method cannot be determined through reflection</exception>
        /// <exception cref="ArgumentNullException">Thrown when the format string is null</exception>
        /// <exception cref="FormatException">Thrown when the format string and associated arguments cannot be used to create a formatted message</exception>
        [PublicAPI]
        public static void VerboseFormat([NotNull] this ILog log, [NotNull] string format, [NotNull] params object[] args)
        {
            if (log == null)
            {
                throw new ArgumentNullException(nameof(log));
            }

            if (format == null)
            {
                throw new ArgumentNullException(nameof(format));
            }

            if (args == null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            log.Verbose(string.Format(format, args));
        }

        /// <summary>
        /// Provides a simple 'Info' level logging of a message using a format string and arguments
        /// </summary>
        /// <param name="log">The logger to use to log the message</param>
        /// <param name="format">The format string for the message to log</param>
        /// <param name="args">The format arguments used to formulate the message to log</param>
        /// <exception cref="TargetException">Thrown when the current method cannot be determined through reflection</exception>
        /// <exception cref="ArgumentNullException">Thrown when the format string is null</exception>
        /// <exception cref="FormatException">Thrown when the format string and associated arguments cannot be used to create a formatted message</exception>
        [PublicAPI]
        public static void InfoFormat([NotNull] this ILog log, [NotNull] string format, [NotNull] params object[] args)
        {
            if (log == null)
            {
                throw new ArgumentNullException(nameof(log));
            }

            if (format == null)
            {
                throw new ArgumentNullException(nameof(format));
            }

            if (args == null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            log.Info(string.Format(format, args));
        }
    }
}
