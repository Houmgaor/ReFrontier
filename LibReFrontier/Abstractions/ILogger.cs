namespace LibReFrontier.Abstractions
{
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
    }
}
