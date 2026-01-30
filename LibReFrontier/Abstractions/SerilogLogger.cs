using System;
using Serilog;
using Serilog.Events;

namespace LibReFrontier.Abstractions
{
    /// <summary>
    /// Serilog-based implementation of ILogger with structured logging support.
    /// </summary>
    public class SerilogLogger : ILogger
    {
        private readonly Serilog.ILogger _logger;
        private const string Separator = "==============================";

        public SerilogLogger(Serilog.ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Create a default console logger with optional file logging.
        /// </summary>
        /// <param name="enableFileLogging">Enable file logging to logs/refrontier.log.</param>
        /// <param name="minLogLevel">Minimum log level to write.</param>
        /// <returns>Configured SerilogLogger instance.</returns>
        public static SerilogLogger CreateDefault(bool enableFileLogging = false, LogEventLevel minLogLevel = LogEventLevel.Information)
        {
            var config = new LoggerConfiguration()
                .MinimumLevel.Is(minLogLevel)
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}",
                    restrictedToMinimumLevel: LogEventLevel.Information
                );

            if (enableFileLogging)
            {
                config = config.WriteTo.File(
                    "logs/refrontier.log",
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
                );
            }

            return new SerilogLogger(config.CreateLogger());
        }

        /// <inheritdoc />
        public void WriteLine(string message)
        {
            _logger.Information(message);
        }

        /// <inheritdoc />
        public void Write(string message)
        {
            // For inline messages without newline, still log at Information level
            _logger.Information(message);
        }

        /// <inheritdoc />
        public void WriteSeparator()
        {
            _logger.Information(Separator);
        }

        /// <inheritdoc />
        public void PrintWithSeparator(string message, bool printBefore)
        {
            if (printBefore)
            {
                _logger.Information("");
                _logger.Information(Separator);
                _logger.Information(message);
            }
            else
            {
                _logger.Information(message);
                _logger.Information(Separator);
            }
        }

        /// <inheritdoc />
        public void Log(LogLevel level, string message)
        {
            var serilogLevel = MapLogLevel(level);
            _logger.Write(serilogLevel, message);
        }

        /// <inheritdoc />
        public void Debug(string message)
        {
            _logger.Debug(message);
        }

        /// <inheritdoc />
        public void Information(string message)
        {
            _logger.Information(message);
        }

        /// <inheritdoc />
        public void Warning(string message)
        {
            _logger.Warning(message);
        }

        /// <inheritdoc />
        public void Error(string message)
        {
            _logger.Error(message);
        }

        /// <inheritdoc />
        public void Error(Exception exception, string message)
        {
            _logger.Error(exception, message);
        }

        private static LogEventLevel MapLogLevel(LogLevel level)
        {
            return level switch
            {
                LogLevel.Trace => LogEventLevel.Verbose,
                LogLevel.Debug => LogEventLevel.Debug,
                LogLevel.Information => LogEventLevel.Information,
                LogLevel.Warning => LogEventLevel.Warning,
                LogLevel.Error => LogEventLevel.Error,
                LogLevel.Fatal => LogEventLevel.Fatal,
                _ => LogEventLevel.Information
            };
        }
    }
}
