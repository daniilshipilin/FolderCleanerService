using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Timers;
using static FolderCleanerService.GlobalEnum;

namespace FolderCleanerService.Helpers
{
    public class Logging
    {
        const int LOG_WRITE_CHECK_INTERVAL_MS = 1000;
        const string LOG_TIMESTAMP_FORMAT = "yyyy-MM-dd HH:mm:ss.fff";
        const string LOG_FILE_TIMESTAMP_FORMAT = "yyyyMMdd";

        /// <summary>
        /// Singleton Logging class instance.
        /// </summary>
        public static Logging Instance { get; set; }

        /// <summary>
        /// Currently used log file path.
        /// </summary>
        public string LogFilePath { get => $"{_logDir}\\{_logPrefix}_{DateTime.Now.ToString(LOG_FILE_TIMESTAMP_FORMAT)}.log"; }

        /// <summary>
        /// Currently used exceptions log file path.
        /// </summary>
        public string ExceptionsLogFilePath { get => $"{_logDir}\\{_logPrefix}_{DateTime.Now.ToString(LOG_FILE_TIMESTAMP_FORMAT)}_Exceptions.log"; }

        /// <summary>
        /// Previous day log file path.
        /// </summary>
        public string PreviousDayLogFilePath { get => $"{_logDir}\\{_logPrefix}_{DateTime.Now.Date.AddDays(-1).ToString(LOG_FILE_TIMESTAMP_FORMAT)}.log"; }

        /// <summary>
        /// Previous day log file path.
        /// </summary>
        public string PreviousDayExceptionsLogFilePath { get => $"{_logDir}\\{_logPrefix}_{DateTime.Now.Date.AddDays(-1).ToString(LOG_FILE_TIMESTAMP_FORMAT)}_Exceptions.log"; }

        readonly string _logDir;
        readonly string _logPrefix;
        readonly List<string> _logBuffer;
        readonly object _lockerObj1;
        readonly List<string> _exceptionsLogBuffer;
        readonly object _lockerObj2;

        readonly Encoding _encoding = new UTF8Encoding(false);

        /// <summary>
        /// Log entry struct.
        /// </summary>
        public struct LogEntry
        {
            public DateTime Timestamp { get; }
            public string MethodName { get; }
            public MessageType MessageType { get; }
            public string Message { get; }

            public LogEntry(string methodName, MessageType messageType, string message)
            {
                Timestamp = DateTime.Now;
                MethodName = methodName;
                MessageType = messageType;
                Message = message;
            }

            public override string ToString() => (string.Format("{0,-25} ", Timestamp.ToString(LOG_TIMESTAMP_FORMAT)) + // timestamp
                                                  string.Format("{0,-20} ", MethodName) + // calling method name
                                                  string.Format("{0,-10} ", MessageType) + // message type
                                                  Message);
        };

        /// <summary>
        /// Initializes a new instance of the Logging class with log buffer flush timer.
        /// </summary>
        public Logging()
        {
            _logDir = $"{Program.ProgramBaseDirectory}Log";
            _logPrefix = Program.ProgramName;
            _logBuffer = new List<string>();
            _lockerObj1 = new object();
            _exceptionsLogBuffer = new List<string>();
            _lockerObj2 = new object();

            // timer, that checks, whether log buffer has new data
            var _timer = new Timer(LOG_WRITE_CHECK_INTERVAL_MS) { AutoReset = true };
            _timer.Elapsed += (sender, e) => FlushLogBuffer();
            _timer.Start();
        }

        /// <summary>
        /// Add log entry to the log buffer.
        /// </summary>
        public void AddEntry(LogEntry entry)
        {
            lock (_lockerObj1) { _logBuffer.Add(entry.ToString()); }

            // add log entry to exceptions log buffer, if messageType is exception or error type
            if (entry.MessageType == MessageType.Exception || entry.MessageType == MessageType.Error)
            {
                lock (_lockerObj2) { _exceptionsLogBuffer.Add(entry.ToString()); }
            }
        }

        /// <summary>
        /// Flush log buffer and write its content to the log file.
        /// </summary>
        public void FlushLogBuffer()
        {
            if (_logBuffer.Count > 0) { AppendToLogFile(); }

            if (_exceptionsLogBuffer.Count > 0) { AppendToExceptionsLogFile(); }
        }

        private void AppendToLogFile()
        {
            lock (_lockerObj1)
            {
                try
                {
                    if (!Directory.Exists(_logDir)) { Directory.CreateDirectory(_logDir); }

                    File.AppendAllLines(LogFilePath, _logBuffer, _encoding);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }

                _logBuffer.Clear();
            }
        }

        private void AppendToExceptionsLogFile()
        {
            lock (_lockerObj2)
            {
                try
                {
                    if (!Directory.Exists(_logDir)) { Directory.CreateDirectory(_logDir); }

                    File.AppendAllLines(ExceptionsLogFilePath, _exceptionsLogBuffer, _encoding);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }

                _exceptionsLogBuffer.Clear();
            }
        }
    }
}
