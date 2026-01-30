using System.Text;

using LibReFrontier.Abstractions;

namespace ReFrontier.Tests.Mocks
{
    /// <summary>
    /// Test implementation of ILogger that captures output for assertions.
    /// </summary>
    public class TestLogger : ILogger
    {
        private readonly StringBuilder _output = new();
        private readonly List<string> _lines = new();
        private readonly List<string> _messages = new();

        /// <summary>
        /// Get all output as a single string.
        /// </summary>
        public string Output => _output.ToString();

        /// <summary>
        /// Get all lines written (via WriteLine).
        /// </summary>
        public IReadOnlyList<string> Lines => _lines;

        /// <summary>
        /// Get all messages (both Write and WriteLine).
        /// </summary>
        public IReadOnlyList<string> Messages => _messages;

        /// <inheritdoc />
        public void WriteLine(string message)
        {
            _output.AppendLine(message);
            _lines.Add(message);
            _messages.Add(message);
        }

        /// <inheritdoc />
        public void Write(string message)
        {
            _output.Append(message);
            _messages.Add(message);
        }

        /// <inheritdoc />
        public void WriteSeparator()
        {
            WriteLine("==============================");
        }

        /// <inheritdoc />
        public void PrintWithSeparator(string message, bool printBefore)
        {
            if (printBefore)
            {
                WriteLine("");
                WriteSeparator();
                WriteLine(message);
            }
            else
            {
                WriteLine(message);
                WriteSeparator();
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
            WriteLine($"{prefix}{message}");
        }

        /// <inheritdoc />
        public void Debug(string message) => WriteLine($"[DEBUG] {message}");

        /// <inheritdoc />
        public void Information(string message) => WriteLine(message);

        /// <inheritdoc />
        public void Warning(string message) => WriteLine($"[WARN] {message}");

        /// <inheritdoc />
        public void Error(string message) => WriteLine($"[ERROR] {message}");

        /// <inheritdoc />
        public void Error(Exception exception, string message)
        {
            WriteLine($"[ERROR] {message}");
            WriteLine($"Exception: {exception.Message}");
        }

        /// <summary>
        /// Check if the output contains a specific message.
        /// </summary>
        /// <param name="text">Text to search for.</param>
        /// <returns>true if found.</returns>
        public bool ContainsMessage(string text)
        {
            return _output.ToString().Contains(text);
        }

        /// <summary>
        /// Check if any line contains the specified text.
        /// </summary>
        /// <param name="text">Text to search for.</param>
        /// <returns>true if found in any line.</returns>
        public bool AnyLineContains(string text)
        {
            foreach (var line in _lines)
            {
                if (line.Contains(text))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Clear all captured output.
        /// </summary>
        public void Clear()
        {
            _output.Clear();
            _lines.Clear();
            _messages.Clear();
        }
    }
}
