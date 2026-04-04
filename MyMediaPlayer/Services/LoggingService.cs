using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MyMediaPlayer.Models;

namespace MyMediaPlayer.Services
{
    public class LoggingService
    {
        private static LoggingService? _instance;
        public static LoggingService Instance => _instance ??= new LoggingService();

        private readonly string _logFolder;
        private readonly string _logFile;
        private readonly object _lock = new();
        private readonly int _maxLogSize = 50 * 1024 * 1024;

        public List<LogEntry> LogEntries { get; private set; } = new();

        public event Action<LogEntry>? OnLogAdded;

        private LoggingService()
        {
            _logFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            Directory.CreateDirectory(_logFolder);
            _logFile = Path.Combine(_logFolder, "app.log");
            LoadLogEntries();
        }

        private void LoadLogEntries()
        {
            try
            {
                if (File.Exists(_logFile))
                {
                    var lines = File.ReadAllLines(_logFile);
                    foreach (var line in lines)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        var entry = ParseLogLine(line);
                        if (entry != null) LogEntries.Add(entry);
                    }
                }
            }
            catch { }
        }

        private LogEntry? ParseLogLine(string line)
        {
            try
            {
                var parts = line.Split(new[] { " - " }, 3, StringSplitOptions.None);
                if (parts.Length >= 3)
                {
                    var timestamp = DateTime.Parse(parts[0]);
                    var level = Enum.Parse<LogLevel>(parts[1]);
                    return new LogEntry
                    {
                        Timestamp = timestamp,
                        Level = level,
                        Message = parts[2]
                    };
                }
            }
            catch { }
            return null;
        }

        public void LogInfo(string message)
        {
            AddLogEntry(LogLevel.Info, message);
        }

        public void LogWarning(string message)
        {
            AddLogEntry(LogLevel.Warning, message);
        }

        public void LogError(string message, Exception? ex = null)
        {
            var fullMessage = message;
            if (ex != null)
            {
                fullMessage = $"{message}\nException: {ex.GetType().Name}\nMessage: {ex.Message}\nStackTrace: {ex.StackTrace}";
                if (ex.InnerException != null)
                {
                    fullMessage += $"\nInnerException: {ex.InnerException.Message}\nInnerStackTrace: {ex.InnerException.StackTrace}";
                }
            }
            AddLogEntry(LogLevel.Error, fullMessage);
        }

        private void AddLogEntry(LogLevel level, string message)
        {
            var entry = new LogEntry
            {
                Timestamp = DateTime.Now,
                Level = level,
                Message = message
            };

            lock (_lock)
            {
                LogEntries.Add(entry);
                WriteToFile(entry);

                if (LogEntries.Count > 1000)
                {
                    LogEntries.RemoveRange(0, 500);
                }
            }

            OnLogAdded?.Invoke(entry);
        }

        private void WriteToFile(LogEntry entry)
        {
            try
            {
                if (File.Exists(_logFile) && new FileInfo(_logFile).Length > _maxLogSize)
                {
                    RotateLog();
                }

                var line = $"{entry.Timestamp:yyyy-MM-dd HH:mm:ss} - {entry.Level} - {entry.Message}";
                File.AppendAllText(_logFile, line + Environment.NewLine);
            }
            catch { }
        }

        private void RotateLog()
        {
            try
            {
                var backupFile = Path.Combine(_logFolder, $"app_{DateTime.Now:yyyyMMdd_HHmmss}.log");
                File.Move(_logFile, backupFile);

                var oldLogs = Directory.GetFiles(_logFolder, "app_*.log");
                if (oldLogs.Length > 10)
                {
                    Array.Sort(oldLogs);
                    for (int i = 0; i < oldLogs.Length - 10; i++)
                    {
                        File.Delete(oldLogs[i]);
                    }
                }
            }
            catch { }
        }

        public void ClearLogs()
        {
            lock (_lock)
            {
                LogEntries.Clear();
            }
        }
    }
}
