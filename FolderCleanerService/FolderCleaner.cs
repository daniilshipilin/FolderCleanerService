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

        readonly List<string> _dirs;
        readonly int _deleteFilesOlderThanDays;
        readonly List<string> _fileSearchPatterns;
        readonly TimeSpan _checkFoldersAtSpecificTime;
        readonly bool _checkFoldersAtServiceStart;

        public FolderCleaner()
        {
            var appSettings = ConfigurationManager.AppSettings;
            string cleanupFolders = appSettings["CleanupFolders"];
            string deleteFilesOlderThanDays = appSettings["DeleteFilesOlderThanDays"];
            string fileSearchPatterns = appSettings["FileSearchPatterns"];
            string checkFoldersAtSpecificTime = appSettings["CheckFoldersAtSpecificTime"];
            string checkFoldersAtServiceStart = appSettings["CheckFoldersAtServiceStart"];

            if (!string.IsNullOrEmpty(cleanupFolders))
            {
                _dirs = new List<string>();
                var dirs = cleanupFolders.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var dir in dirs)
                {
                    // add only existing dirs
                    if (Directory.Exists(dir)) { _dirs.Add(dir); }
                }
            }

            if (_dirs.Count < 1) { throw new Exception("'CleanupFolders' list is empty"); }

            if (!string.IsNullOrEmpty(deleteFilesOlderThanDays))
            {
                if (int.TryParse(deleteFilesOlderThanDays, out int tmp))
                {
                    if (tmp > 0) { _deleteFilesOlderThanDays = tmp; }
                    else
                    {
                        throw new Exception("'DeleteFilesOlderThanDays' value should be positive integer");
                    }
                }
                else
                {
                    throw new Exception("'DeleteFilesOlderThanDays' couldn't parse the value");
                }
            }

            if (_deleteFilesOlderThanDays < 1)
            {
                throw new Exception($"'DeleteFilesOlderThanDays' value should be positive integer");
            }

            if (!string.IsNullOrEmpty(fileSearchPatterns))
            {
                _fileSearchPatterns = fileSearchPatterns.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            }

            if (_fileSearchPatterns.Count < 1) { throw new Exception("'FileSearchPatterns' list is empty"); }

            if (!string.IsNullOrEmpty(checkFoldersAtSpecificTime))
            {
                if (TimeSpan.TryParse(checkFoldersAtSpecificTime, out TimeSpan tmp))
                {
                    _checkFoldersAtSpecificTime = tmp;
                }
                else
                {
                    throw new Exception("'CheckFoldersAtSpecificTime' couldn't parse the value");
                }
            }

            if (_checkFoldersAtSpecificTime == null)
            {
                throw new Exception("'CheckFoldersAtSpecificTime' value is null");
            }

            if (!string.IsNullOrEmpty(checkFoldersAtServiceStart))
            {
                if (bool.TryParse(checkFoldersAtServiceStart, out bool tmp))
                {
                    _checkFoldersAtServiceStart = tmp;
                }
                else
                {
                    throw new Exception("'CheckFoldersAtServiceStart' couldn't parse the value");
                }
            }
        }

        private void InitTimer()
        {
            var nowTimespan = DateTime.Now.TimeOfDay;
            var result = (nowTimespan >= _checkFoldersAtSpecificTime)
                         ? nowTimespan - _checkFoldersAtSpecificTime + TimeSpan.FromHours(24)
                         : _checkFoldersAtSpecificTime - nowTimespan;

            _timer = new Timer(result.TotalMilliseconds) { AutoReset = true };
            _timer.Elapsed += CheckFoldersEvent;
            _timer.Start();
            //Console.WriteLine("Timer started");
        }

        private void StopTimer()
        {
            _timer.Stop();
            //Console.WriteLine("Timer stopped");
        }

        private void CheckFoldersEvent(object sender, ElapsedEventArgs e)
        {
            if (_checkFoldersEventExecuting) { return; }
            else { _checkFoldersEventExecuting = true; }

            int filesDeleted = 0;
            long spaceFreedUpMb = 0;
            Console.WriteLine($"{nameof(CheckFoldersEvent)} fired on {DateTime.Now}");

            foreach (var dir in _dirs)
            {
                if (!Directory.Exists(dir)) { Console.WriteLine($"'{dir}' doesn't exist"); continue; }

                Console.WriteLine($"Performing cleanup in '{dir}'");

                foreach (var pattern in _fileSearchPatterns)
                {
                    foreach (var file in Directory.GetFiles(dir, pattern, SearchOption.AllDirectories))
                    {
                        try
                        {
                            var fi = new FileInfo(file);

                            if ((DateTime.Now - fi.LastWriteTime).TotalDays >= _deleteFilesOlderThanDays)
                            {
                                File.Delete(file);
                                filesDeleted++;
                                spaceFreedUpMb += fi.Length;
                                //Console.WriteLine($"'{file}' deleted");
                            }
                        }
                        catch (Exception ex) { Console.WriteLine(ex.Message); }
                    }
                }
            }

            if (filesDeleted > 0)
            {
                Console.WriteLine($"Total files deleted: {filesDeleted}");
                Console.WriteLine($"Total space freed: {(double)spaceFreedUpMb / 1048576:0.00} MB");
            }

            Console.WriteLine($"{nameof(CheckFoldersEvent)} finished on {DateTime.Now}");

            if (_timer != null) { StopTimer(); }

            InitTimer();
            _checkFoldersEventExecuting = false;
        }

        public void Start()
        {
            Console.WriteLine(Program.ProgramHeader);
            Console.WriteLine(Program.ProgramBuild);
            Console.WriteLine(Program.ProgramLastCommit);
            Console.WriteLine(Program.ProgramAuthor);

            Console.WriteLine("Dirs:");

            foreach (var folder in _dirs)
            {
                Console.WriteLine($"'{folder}'");
            }

            Console.WriteLine("FileSearchPatterns:");

            foreach (var pattern in _fileSearchPatterns)
            {
                Console.WriteLine($"'{pattern}'");
            }

            Console.WriteLine($"DeleteFilesOlderThanDays: {_deleteFilesOlderThanDays}");
            Console.WriteLine($"CheckFoldersAtSpecificTime: {_checkFoldersAtSpecificTime}");
            Console.WriteLine($"CheckFoldersAtServiceStart: {_checkFoldersAtServiceStart}");

            if (_checkFoldersAtServiceStart)
            {
                CheckFoldersEvent(null, null);
            }
            else
            {
                InitTimer();
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
