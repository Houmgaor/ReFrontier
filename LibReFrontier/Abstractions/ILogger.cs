using System;

namespace LibReFrontier.Abstractions
{
    /// <summary>
    /// Log level enumeration.
    /// </summary>
    public enum LogLevel
    {
        Trace,
        Debug,
        Information,
        Warning,
        Error,
        Fatal
    }

    /// <summary>
    /// Abstracts logging/console output for testability.
    /// </summary>
    public interface ILogger
    {
        /// <summary>
        /// Write a line to output.
        /// </summary>
        /// <param name="message">Message to write.</param>
        void WriteLine(string message);

        /// <summary>
        /// Write to output without a newline.
        /// </summary>
        /// <param name="message">Message to write.</param>
        void Write(string message);

        /// <summary>
        /// Write a separator line.
        /// </summary>
        void WriteSeparator();

        /// <summary>
        /// Print with a separator, either before or after the message.
        /// </summary>
        /// <param name="message">Message to print.</param>
        /// <param name="printBefore">If true, print separator before message; otherwise after.</param>
        void PrintWithSeparator(string message, bool printBefore);

        /// <summary>
        /// Log a message at the specified level.
        /// </summary>
        /// <param name="level">Log level.</param>
        /// <param name="message">Message to log.</param>
        void Log(LogLevel level, string message);

        /// <summary>
        /// Log a debug message.
        /// </summary>
        /// <param name="message">Message to log.</param>
        void Debug(string message);

        /// <summary>
        /// Log an informational message.
        /// </summary>
        /// <param name="message">Message to log.</param>
        void Information(string message);

        /// <summary>
        /// Log a warning message.
        /// </summary>
        /// <param name="message">Message to log.</param>
        void Warning(string message);

        /// <summary>
        /// Log an error message.
        /// </summary>
        /// <param name="message">Message to log.</param>
        void Error(string message);

        /// <summary>
        /// Log an error with exception details.
        /// </summary>
        /// <param name="exception">Exception to log.</param>
        /// <param name="message">Additional message.</param>
        void Error(Exception exception, string message);
    }
}
