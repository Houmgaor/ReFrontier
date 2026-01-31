using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;

namespace LibReFrontier.Abstractions
{
    /// <summary>
    /// Thread-safe buffered logger that batches output to reduce lock contention.
    /// Messages are queued and flushed periodically or when the buffer reaches capacity.
    /// </summary>
    public class BufferedLogger : ILogger, IDisposable
    {
        private const string Separator = "==============================";
        private const int DefaultFlushIntervalMs = 50;
        private const int DefaultBufferCapacity = 100;

        private readonly ConcurrentQueue<string> _buffer = new();
        private readonly Timer _flushTimer;
        private readonly TextWriter _output;
        private readonly TextWriter _errorOutput;
        private readonly int _bufferCapacity;
        private volatile bool _disposed;
        private int _messageCount;

        /// <summary>
        /// Create a new BufferedLogger with default settings.
        /// </summary>
        public BufferedLogger()
            : this(Console.Out, Console.Error, DefaultFlushIntervalMs, DefaultBufferCapacity)
        {
        }

        /// <summary>
        /// Create a new BufferedLogger with custom output writers.
        /// </summary>
        /// <param name="output">Standard output writer.</param>
        /// <param name="errorOutput">Error output writer.</param>
        /// <param name="flushIntervalMs">Flush interval in milliseconds.</param>
        /// <param name="bufferCapacity">Maximum messages before forced flush.</param>
        public BufferedLogger(TextWriter output, TextWriter? errorOutput = null, int flushIntervalMs = DefaultFlushIntervalMs, int bufferCapacity = DefaultBufferCapacity)
        {
            _output = output ?? throw new ArgumentNullException(nameof(output));
            _errorOutput = errorOutput ?? output;
            _bufferCapacity = bufferCapacity;
            _flushTimer = new Timer(_ => Flush(), null, flushIntervalMs, flushIntervalMs);
        }

        /// <inheritdoc />
        public void WriteLine(string message)
        {
            EnqueueMessage(message);
        }

        /// <inheritdoc />
        public void Write(string message)
        {
            // For Write without newline, we still buffer but mark it specially
            EnqueueMessage(message, addNewline: false);
        }

        /// <inheritdoc />
        public void WriteSeparator()
        {
            EnqueueMessage(Separator);
        }

        /// <inheritdoc />
        public void PrintWithSeparator(string message, bool printBefore)
        {
            if (printBefore)
            {
                EnqueueMessage("\n" + Separator);
                EnqueueMessage(message);
            }
            else
            {
                EnqueueMessage(message);
                EnqueueMessage(Separator);
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
            EnqueueMessage($"{prefix}{message}");
        }

        /// <inheritdoc />
        public void Debug(string message) => EnqueueMessage($"[DEBUG] {message}");

        /// <inheritdoc />
        public void Information(string message) => EnqueueMessage(message);

        /// <inheritdoc />
        public void Warning(string message) => EnqueueMessage($"[WARN] {message}");

        /// <inheritdoc />
        public void Error(string message)
        {
            // Errors go directly to error output, not buffered
            _errorOutput.WriteLine($"[ERROR] {message}");
        }

        /// <inheritdoc />
        public void Error(Exception exception, string message)
        {
            ArgumentNullException.ThrowIfNull(exception);

            // Errors go directly to error output, not buffered
            _errorOutput.WriteLine($"[ERROR] {message}");
            _errorOutput.WriteLine($"Exception: {exception.Message}");
            if (exception.StackTrace != null)
            {
                _errorOutput.WriteLine($"Stack trace: {exception.StackTrace}");
            }
        }

        private void EnqueueMessage(string message, bool addNewline = true)
        {
            if (_disposed) return;

            var formattedMessage = addNewline ? message + Environment.NewLine : message;
            _buffer.Enqueue(formattedMessage);

            // Force flush if buffer is full
            if (Interlocked.Increment(ref _messageCount) >= _bufferCapacity)
            {
                Flush();
            }
        }

        /// <summary>
        /// Flush all buffered messages to output.
        /// </summary>
        public void Flush()
        {
            if (_disposed && _buffer.IsEmpty) return;

            var sb = new StringBuilder();
            while (_buffer.TryDequeue(out var message))
            {
                sb.Append(message);
                Interlocked.Decrement(ref _messageCount);
            }

            if (sb.Length > 0)
            {
                _output.Write(sb.ToString());
            }
        }

        /// <summary>
        /// Dispose the logger, flushing any remaining messages.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _flushTimer.Dispose();
            Flush(); // Final flush

            GC.SuppressFinalize(this);
        }
    }
}
