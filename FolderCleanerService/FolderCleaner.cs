using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Timers;

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

        static readonly List<string> _appConfig = new List<string>()
        {
            nameof(CleanupFolders),
                   nameof(DeleteFilesOlderThanDays),
                   nameof(FileSearchPatterns),
                   nameof(CheckFoldersOnceADayAtSpecificTime),
                   nameof(CheckFoldersAtServiceStart),
                   nameof(RecursiveSearch),
                   nameof(DeleteEmptyFolders)
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
            Console.WriteLine("Timer created/initialized");
        }

        private void StopTimer()
        {
            _timer.Stop();
            Console.WriteLine("Timer stopped");
        }

        private void StartTimer()
        {
            _timer.Start();
            Console.WriteLine("Timer started");
        }

        private void CheckFoldersEvent(object sender, ElapsedEventArgs e)
        {
            Console.WriteLine($"{nameof(CheckFoldersEvent)} fired on {DateTime.Now}");

            if (_timer != null) { StopTimer(); }

            if (_checkFoldersEventExecuting) { return; }
            else { _checkFoldersEventExecuting = true; }

            int filesDeleted = 0;
            long spaceFreedUpMb = 0;
            int emptyDirsDeleted = 0;

            foreach (var dir in CleanupFolders)
            {
                if (!Directory.Exists(dir)) { Console.WriteLine($"'{dir}' doesn't exist"); continue; }

                Console.WriteLine($"Performing cleanup in '{dir}'");

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
                        catch (Exception ex) { Console.WriteLine(ex.Message); }
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
                            catch (Exception ex) { Console.WriteLine(ex.Message); }
                        }
                    }
                }
            }

            if (filesDeleted > 0)
            {
                Console.WriteLine($"Total files deleted: {filesDeleted}");
                Console.WriteLine($"Total space freed: {(double)spaceFreedUpMb / 1048576:0.00} MB");
            }

            if (emptyDirsDeleted > 0)
            {
                Console.WriteLine($"Total empty dirs deleted: {emptyDirsDeleted}");
            }

            Console.WriteLine($"{nameof(CheckFoldersEvent)} finished on {DateTime.Now}");

            InitTimer();
            StartTimer();
            _checkFoldersEventExecuting = false;
        }

        public void Start()
        {
            Console.WriteLine(Program.ProgramHeader);
            Console.WriteLine(Program.ProgramBuild);
            Console.WriteLine(Program.ProgramLastCommit);
            Console.WriteLine(Program.ProgramAuthor);

            Console.WriteLine($"{nameof(CleanupFolders)}:");

            foreach (var folder in CleanupFolders)
            {
                Console.WriteLine($"'{folder}'");
            }

            Console.WriteLine($"{nameof(FileSearchPatterns)}:");

            foreach (var pattern in FileSearchPatterns)
            {
                Console.WriteLine($"'{pattern}'");
            }

            Console.WriteLine($"{nameof(DeleteFilesOlderThanDays)}: {DeleteFilesOlderThanDays}");
            Console.WriteLine($"{nameof(CheckFoldersOnceADayAtSpecificTime)}: {CheckFoldersOnceADayAtSpecificTime}");
            Console.WriteLine($"{nameof(CheckFoldersAtServiceStart)}: {CheckFoldersAtServiceStart}");
            Console.WriteLine($"{nameof(RecursiveSearch)}: {RecursiveSearch}");
            Console.WriteLine($"{nameof(DeleteEmptyFolders)}: {DeleteEmptyFolders}");

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
        }

        public void Shutdown()
        {
            StopTimer();
        }
    }
}
