using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using Topshelf;
using static FolderCleanerService.GlobalEnum;

namespace FolderCleanerService.Helpers
{
    public class FolderCleaner : ServiceControl
    {
        // reference to the service host
        HostControl _hostControl;

        Timer _timer;
        readonly SearchOption _so = SearchOption.TopDirectoryOnly;
        static bool _checkFoldersEventIsExecuting;

        public readonly List<string> CleanupFolders;
        public int DeleteFilesOlderThanDays { get; }
        public readonly List<string> FileSearchPatterns;
        public TimeSpan CheckFoldersOnceADayAtSpecificTime { get; }
        public bool CheckFoldersAtServiceStart { get; }
        public bool RecursiveSearch { get; }
        public bool DeleteEmptyFolders { get; }
        public bool LoggingEnabled { get; }

        static readonly List<string> _appConfig = new List<string>()
        {
            nameof(CleanupFolders),
                   nameof(DeleteFilesOlderThanDays),
                   nameof(FileSearchPatterns),
                   nameof(CheckFoldersOnceADayAtSpecificTime),
                   nameof(CheckFoldersAtServiceStart),
                   nameof(RecursiveSearch),
                   nameof(DeleteEmptyFolders),
                   nameof(LoggingEnabled)
        };

        public FolderCleaner()
        {
            foreach (var key in _appConfig)
            {
                string val = ConfigurationManager.AppSettings[key];

                if (string.IsNullOrEmpty(val)) { throw new Exception($"'{key}' key doesn't have value"); }

                if (key.Equals(nameof(CleanupFolders)))
                {
                    CleanupFolders = new List<string>();
                    var dirs = val.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

                    foreach (var dir in dirs)
                    {
                        if (Directory.Exists(dir)) { CleanupFolders.Add(dir); } // add only existing dirs
                    }

                    if (CleanupFolders.Count < 1) { throw new Exception($"'{nameof(CleanupFolders)}' list is empty"); }
                }
                else if (key.Equals(nameof(DeleteFilesOlderThanDays)))
                {
                    if (int.TryParse(val, out int tmp))
                    {
                        if (tmp > 0) { DeleteFilesOlderThanDays = tmp; }
                        else
                        {
                            throw new Exception($"'{nameof(DeleteFilesOlderThanDays)}' value should be positive integer");
                        }
                    }
                    else
                    {
                        throw new Exception($"'{nameof(DeleteFilesOlderThanDays)}' couldn't parse the value");
                    }
                }
                else if (key.Equals(nameof(FileSearchPatterns)))
                {
                    FileSearchPatterns = val.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries).ToList();

                    if (FileSearchPatterns.Count < 1) { throw new Exception($"'{nameof(FileSearchPatterns)}' list is empty"); }
                }
                else if (key.Equals(nameof(CheckFoldersOnceADayAtSpecificTime)))
                {
                    if (TimeSpan.TryParse(val, out TimeSpan tmp))
                    {
                        CheckFoldersOnceADayAtSpecificTime = tmp;
                    }
                    else
                    {
                        throw new Exception($"'{nameof(CheckFoldersOnceADayAtSpecificTime)}' couldn't parse the value");
                    }

                    if (CheckFoldersOnceADayAtSpecificTime == null)
                    {
                        throw new Exception($"'{nameof(CheckFoldersOnceADayAtSpecificTime)}' value is null");
                    }
                }
                else if (key.Equals(nameof(CheckFoldersAtServiceStart)))
                {
                    if (bool.TryParse(val, out bool tmp))
                    {
                        CheckFoldersAtServiceStart = tmp;
                    }
                    else
                    {
                        throw new Exception($"'{nameof(CheckFoldersAtServiceStart)}' couldn't parse the value");
                    }
                }
                else if (key.Equals(nameof(RecursiveSearch)))
                {
                    if (bool.TryParse(val, out bool tmp))
                    {
                        RecursiveSearch = tmp;
                    }
                    else
                    {
                        throw new Exception($"'{nameof(RecursiveSearch)}' couldn't parse the value");
                    }

                    if (RecursiveSearch) { _so = SearchOption.AllDirectories; }
                }
                else if (key.Equals(nameof(DeleteEmptyFolders)))
                {
                    if (bool.TryParse(val, out bool tmp))
                    {
                        DeleteEmptyFolders = tmp;
                    }
                    else
                    {
                        throw new Exception($"'{nameof(DeleteEmptyFolders)}' couldn't parse the value");
                    }
                }
                else if (key.Equals(nameof(LoggingEnabled)))
                {
                    if (bool.TryParse(val, out bool tmp))
                    {
                        LoggingEnabled = tmp;
                    }
                    else
                    {
                        throw new Exception($"'{nameof(LoggingEnabled)}' couldn't parse the value");
                    }

                    if (LoggingEnabled) { Logging.Instance = new Logging(); }
                }
                else
                {
                    throw new Exception($"'{key}' key not found in '{AppDomain.CurrentDomain.SetupInformation.ConfigurationFile}'");
                }
            }
        }

        private void InitStartTimer()
        {
            _timer = new Timer(100) { AutoReset = true };
            _timer.Elapsed += (sender, e) =>
            {
                if (!_checkFoldersEventIsExecuting)
                {
                    if ((int)DateTime.Now.TimeOfDay.TotalSeconds == (int)CheckFoldersOnceADayAtSpecificTime.TotalSeconds)
                    {
                        var task = CheckFoldersEvent();
                        Task.WaitAll(task);
                    }
                }
            };
            _timer.Start();
            ConsoleHandler.Print("Timer initialized and started");
        }

        private void StopTimer()
        {
            _timer.Stop();
            ConsoleHandler.Print("Timer stopped");
        }

        private async Task CheckFoldersEvent()
        {
            _checkFoldersEventIsExecuting = true;
            ConsoleHandler.Print($"{nameof(CheckFoldersEvent)} fired on {DateTime.Now}");

            int filesDeleted = 0;
            long spaceFreedUpBytes = 0;
            int emptyDirsDeleted = 0;

            var sw = Stopwatch.StartNew();

            foreach (var dir in CleanupFolders)
            {
                if (!Directory.Exists(dir)) { ConsoleHandler.Print($"'{dir}' doesn't exist"); continue; }

                ConsoleHandler.Print($"Performing cleanup in '{dir}'");

                foreach (var pattern in FileSearchPatterns)
                {
                    foreach (var file in Directory.GetFiles(dir, pattern, _so))
                    {
                        try
                        {
                            var fi = new FileInfo(file);

                            if ((DateTime.Now - fi.LastWriteTime).TotalDays >= DeleteFilesOlderThanDays)
                            {
                                long size = fi.Length;
                                fi.IsReadOnly = false;
                                fi.Delete();
                                filesDeleted++;
                                spaceFreedUpBytes += size;
                            }
                        }
                        catch (Exception ex) { ConsoleHandler.Print(ex.Message, MessageType.Exception); }
                    }
                }

                if (DeleteEmptyFolders)
                {
                    foreach (var currentDir in Directory.GetDirectories(dir, "*", _so).OrderByDescending(q => q))
                    {
                        var di = new DirectoryInfo(currentDir);

                        if (di.GetDirectories().Length == 0 && di.GetFiles().Length == 0)
                        {
                            try
                            {
                                di.Attributes = FileAttributes.Normal;
                                di.Delete();
                                emptyDirsDeleted++;
                            }
                            catch (Exception ex) { ConsoleHandler.Print(ex.Message, MessageType.Exception); }
                        }
                    }
                }
            }

            sw.Stop();
            ConsoleHandler.Print($"Time elapsed: {sw.ElapsedMilliseconds} ms", MessageType.Info);

            if (filesDeleted > 0)
            {
                ConsoleHandler.Print($"Total files deleted: {filesDeleted}", MessageType.Info);
                ConsoleHandler.Print($"Total space freed: {(double)spaceFreedUpBytes / 1048576:0.00} MB", MessageType.Info);
            }

            if (emptyDirsDeleted > 0)
            {
                ConsoleHandler.Print($"Total empty dirs deleted: {emptyDirsDeleted}", MessageType.Info);
            }

            ConsoleHandler.Print($"{nameof(CheckFoldersEvent)} finished on {DateTime.Now}");

            // 1 sec. delay, to make sure, that this method wont be called again in the same time frame (second), since task should be executed once at specific time
            await Task.Delay(1000);
            _checkFoldersEventIsExecuting = false;
        }

        /// <summary>
        /// Starts the service.
        /// </summary>
        public bool Start(HostControl hostControl)
        {
            _hostControl = hostControl;

            ConsoleHandler.PrintProgramHeader();
            ConsoleHandler.PrintConfiguration(this);

            if (CheckFoldersAtServiceStart) { Task.Run(() => CheckFoldersEvent()); }

            InitStartTimer();

            return (true);
        }

        /// <summary>
        /// Stops the service.
        /// </summary>
        public bool Stop(HostControl hostControl)
        {
            StopTimer();

            if (Logging.Instance != null) { Logging.Instance.FlushLogBuffer(); }

            return (true);
        }

        /// <summary>
        /// Service shutdown.
        /// </summary>
        public bool Shutdown(HostControl hostControl)
        {
            return (Stop(hostControl));
        }

        /// <summary>
        /// This method stops the service using HostControl reference.
        /// </summary>
        private void StopProgrammatically()
        {
            _hostControl.Stop();
        }
    }
}
