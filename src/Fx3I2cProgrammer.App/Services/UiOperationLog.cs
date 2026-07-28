using System;
using System.Collections.ObjectModel;
using System.Windows.Threading;
using Fx3I2cProgrammer.Core.Logging;

namespace Fx3I2cProgrammer.App.Services
{
    /// <summary>A single operation-log line for display.</summary>
    public sealed class LogEntry
    {
        public LogEntry(LogLevel level, string message)
        {
            Timestamp = DateTime.Now;
            Level = level;
            Message = message ?? string.Empty;
        }

        public DateTime Timestamp { get; }

        public LogLevel Level { get; }

        public string Message { get; }

        public string Display => string.Format(
            System.Globalization.CultureInfo.CurrentCulture,
            "{0:HH:mm:ss}  {1,-7}  {2}",
            Timestamp,
            Level.ToString().ToUpperInvariant(),
            Message);
    }

    /// <summary>
    /// <see cref="IOperationLog"/> that appends to an observable collection on the UI thread, so it
    /// can safely be written to from background operation threads.
    /// </summary>
    public sealed class UiOperationLog : IOperationLog
    {
        private readonly Dispatcher _dispatcher;

        public UiOperationLog(Dispatcher dispatcher)
        {
            _dispatcher = dispatcher ?? Dispatcher.CurrentDispatcher;
        }

        public ObservableCollection<LogEntry> Entries { get; } = new ObservableCollection<LogEntry>();

        public void Log(LogLevel level, string message)
        {
            var entry = new LogEntry(level, message);
            if (_dispatcher.CheckAccess())
            {
                Entries.Add(entry);
            }
            else
            {
                _dispatcher.BeginInvoke(new Action(() => Entries.Add(entry)));
            }
        }

        public void Clear()
        {
            if (_dispatcher.CheckAccess())
            {
                Entries.Clear();
            }
            else
            {
                _dispatcher.BeginInvoke(new Action(() => Entries.Clear()));
            }
        }
    }
}
