using System;

namespace LibReFrontier.Abstractions
{
    /// <summary>
    /// Simple implementation of ILogger that wraps Console output (backward compatible).
    /// For structured logging, use SerilogLogger instead.
    /// </summary>
    public class ConsoleLogger : ILogger
    {
        private const string Separator = "==============================";

        /// <inheritdoc />
        public void WriteLine(string message) => Console.WriteLine(message);

        /// <inheritdoc />
        public void Write(string message) => Console.Write(message);

        /// <inheritdoc />
        public void WriteSeparator() => Console.WriteLine(Separator);

        /// <inheritdoc />
        public void PrintWithSeparator(string message, bool printBefore)
        {
            if (printBefore)
            {
                Console.WriteLine("\n" + Separator);
                Console.WriteLine(message);
            }
            else
            {
                Console.WriteLine(message);
                Console.WriteLine(Separator);
            }
        }

        /// <inheritdoc />
        public void Log(LogLevel level, string message)
        {
            var prefix = level switch
            {
                LogLevel.Trace => "[TRACE] ",
                LogLevel.Debug => "[DEBUG] ",
                LogLevel.Information => "[INFO] ",
                LogLevel.Warning => "[WARN] ",
                LogLevel.Error => "[ERROR] ",
                LogLevel.Fatal => "[FATAL] ",
                _ => ""
            };
            Console.WriteLine($"{prefix}{message}");
        }

        /// <inheritdoc />
        public void Debug(string message) => Console.WriteLine($"[DEBUG] {message}");

        /// <inheritdoc />
        public void Information(string message) => Console.WriteLine(message);

        /// <inheritdoc />
        public void Warning(string message) => Console.WriteLine($"[WARN] {message}");

        /// <inheritdoc />
        public void Error(string message) => Console.Error.WriteLine($"[ERROR] {message}");

        /// <inheritdoc />
        public void Error(Exception exception, string message)
        {
            ArgumentNullException.ThrowIfNull(exception);

            Console.Error.WriteLine($"[ERROR] {message}");
            Console.Error.WriteLine($"Exception: {exception.Message}");
            if (exception.StackTrace != null)
            {
                Console.Error.WriteLine($"Stack trace: {exception.StackTrace}");
            }
        }
    }
}
