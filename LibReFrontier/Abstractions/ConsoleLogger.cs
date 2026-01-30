using System;

namespace LibReFrontier.Abstractions
{
    /// <summary>
    /// Default implementation of ILogger that wraps Console output.
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
    }
}
