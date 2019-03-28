using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Timers;
using static FolderCleanerService.GlobalEnum;

namespace FolderCleanerService
{
    public class FolderCleaner
    {
        Timer _timer;
        static bool _checkFoldersEventExecuting;
        readonly SearchOption _so = SearchOption.TopDirectoryOnly;

        List<string> CleanupFolders { get; }
        int DeleteFilesOlderThanDays { get; }
        List<string> FileSearchPatterns { get; }
        TimeSpan CheckFoldersOnceADayAtSpecificTime { get; }
        bool CheckFoldersAtServiceStart { get; }
        bool RecursiveSearch { get; }
        bool DeleteEmptyFolders { get; }
        bool LoggingEnabled { get; }

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

        private void InitTimer()
        {
            var nowTimespan = DateTime.Now.TimeOfDay;
            var result = (nowTimespan >= CheckFoldersOnceADayAtSpecificTime)
                         ? nowTimespan - CheckFoldersOnceADayAtSpecificTime + TimeSpan.FromHours(24)
                         : CheckFoldersOnceADayAtSpecificTime - nowTimespan;

            _timer = new Timer(result.TotalMilliseconds) { AutoReset = true };
            _timer.Elapsed += CheckFoldersEvent;
            ConsoleHandler.Print("Timer created/initialized");
        }

        private void StopTimer()
        {
            _timer.Stop();
            ConsoleHandler.Print("Timer stopped");
        }

        private void StartTimer()
        {
            _timer.Start();
            ConsoleHandler.Print("Timer started");
        }

        private void CheckFoldersEvent(object sender, ElapsedEventArgs e)
        {
            ConsoleHandler.Print($"{nameof(CheckFoldersEvent)} fired on {DateTime.Now}");

            if (_timer != null) { StopTimer(); }

            if (_checkFoldersEventExecuting) { return; }
            else { _checkFoldersEventExecuting = true; }

            int filesDeleted = 0;
            long spaceFreedUpMb = 0;
            int emptyDirsDeleted = 0;

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
                                spaceFreedUpMb += size;
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

            if (filesDeleted > 0)
            {
                ConsoleHandler.Print($"Total files deleted: {filesDeleted}", MessageType.Info);
                ConsoleHandler.Print($"Total space freed: {(double)spaceFreedUpMb / 1048576:0.00} MB", MessageType.Info);
            }

            if (emptyDirsDeleted > 0)
            {
                ConsoleHandler.Print($"Total empty dirs deleted: {emptyDirsDeleted}", MessageType.Info);
            }

            ConsoleHandler.Print($"{nameof(CheckFoldersEvent)} finished on {DateTime.Now}");

            InitTimer();
            StartTimer();
            _checkFoldersEventExecuting = false;
        }

        public void Start()
        {
            ConsoleHandler.Print(Program.ProgramHeader);
            ConsoleHandler.Print(Program.ProgramBuild);
            ConsoleHandler.Print(Program.ProgramLastCommit);
            ConsoleHandler.Print(Program.ProgramAuthor);

            ConsoleHandler.Print($"{nameof(CleanupFolders)}:");

            foreach (var folder in CleanupFolders)
            {
                ConsoleHandler.Print($"'{folder}'");
            }

            ConsoleHandler.Print($"{nameof(FileSearchPatterns)}:");

            foreach (var pattern in FileSearchPatterns)
            {
                ConsoleHandler.Print($"'{pattern}'");
            }

            ConsoleHandler.Print($"{nameof(DeleteFilesOlderThanDays)}: {DeleteFilesOlderThanDays}");
            ConsoleHandler.Print($"{nameof(CheckFoldersOnceADayAtSpecificTime)}: {CheckFoldersOnceADayAtSpecificTime}");
            ConsoleHandler.Print($"{nameof(CheckFoldersAtServiceStart)}: {CheckFoldersAtServiceStart}");
            ConsoleHandler.Print($"{nameof(RecursiveSearch)}: {RecursiveSearch}");
            ConsoleHandler.Print($"{nameof(DeleteEmptyFolders)}: {DeleteEmptyFolders}");
            ConsoleHandler.Print($"{nameof(LoggingEnabled)}: {LoggingEnabled}");

            if (CheckFoldersAtServiceStart)
            {
                CheckFoldersEvent(null, null);
            }
            else
            {
                InitTimer();
                StartTimer();
            }
        }

        public void Stop()
        {
            StopTimer();

            if (Logging.Instance != null) { Logging.Instance.FlushLogBuffer(); }
        }

        public void Shutdown()
        {
            Stop();
        }
    }
}
