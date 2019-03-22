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
        readonly SearchOption _so = SearchOption.AllDirectories;

        readonly List<string> _folders;
        readonly int _deleteFilesOlderThanDays;
        readonly List<string> _fileSearchPatterns;
        readonly int _checkFoldersIntervalMs;
        readonly bool _checkFoldersAtServiceStart;

        public FolderCleaner()
        {
            var appSettings = ConfigurationManager.AppSettings;
            string cleanupFolders = appSettings["CleanupFolders"];
            string deleteFilesOlderThanDays = appSettings["DeleteFilesOlderThanDays"];
            string fileSearchPatterns = appSettings["FileSearchPatterns"];
            string checkFoldersIntervalMs = appSettings["CheckFoldersIntervalMs"];
            string checkFoldersAtServiceStart = appSettings["CheckFoldersAtServiceStart"];

            if (!string.IsNullOrEmpty(cleanupFolders))
            {
                _folders = cleanupFolders.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            }

            if (!string.IsNullOrEmpty(deleteFilesOlderThanDays))
            {
                if (int.TryParse(deleteFilesOlderThanDays, out int tmp))
                {
                    _deleteFilesOlderThanDays = tmp;
                }
            }

            if (!string.IsNullOrEmpty(fileSearchPatterns))
            {
                _fileSearchPatterns = fileSearchPatterns.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            }

            if (!string.IsNullOrEmpty(checkFoldersIntervalMs))
            {
                if (int.TryParse(checkFoldersIntervalMs, out int tmp))
                {
                    _checkFoldersIntervalMs = tmp;
                }
            }

            if (!string.IsNullOrEmpty(checkFoldersAtServiceStart))
            {
                if (bool.TryParse(checkFoldersAtServiceStart, out bool tmp))
                {
                    _checkFoldersAtServiceStart = tmp;
                }
            }
        }

        private void InitTimer()
        {
            _timer = new Timer(_checkFoldersIntervalMs) { AutoReset = true };
            _timer.Elapsed += CheckFoldersEvent;
            _timer.Start();
            Console.WriteLine("Timer started");
        }

        private void StopTimer()
        {
            _timer.Stop();
            Console.WriteLine("Timer stopped");
        }

        private void CheckFoldersEvent(object sender, ElapsedEventArgs e)
        {
            int filesDeleted = 0;
            Console.WriteLine($"{nameof(CheckFoldersEvent)} fired at {DateTime.Now}");

            foreach (var folder in _folders)
            {
                if (!Directory.Exists(folder)) { Console.WriteLine($"'{folder}' doesn't exist"); continue; }

                foreach (var pattern in _fileSearchPatterns)
                {
                    var files = Directory.GetFiles(folder, pattern, _so).ToList();

                    if (files.Count > 0)
                    {
                        Console.WriteLine($"{files.Count} files detected with pattern '{pattern}' in folder '{folder}'");

                        foreach (var file in files)
                        {
                            try
                            {
                                if ((DateTime.Now - File.GetLastWriteTime(file)).TotalDays >= _deleteFilesOlderThanDays)
                                {
                                    File.Delete(file);
                                    filesDeleted++;
                                    Console.WriteLine($"'{file}' deleted");
                                }
                            }
                            catch (Exception ex) { Console.WriteLine(ex.Message); }
                        }
                    }
                }
            }

            if (filesDeleted > 0) { Console.WriteLine($"Files deleted: {filesDeleted}"); }
        }

        public void Start()
        {
            Console.WriteLine(Program.ProgramHeader);
            Console.WriteLine(Program.ProgramBuild);
            Console.WriteLine(Program.ProgramLastCommit);
            Console.WriteLine(Program.ProgramAuthor);

            Console.WriteLine("Folders to clean:");

            foreach (var folder in _folders)
            {
                Console.WriteLine($"'{folder}'");
            }

            Console.WriteLine("FileSearchPatterns:");

            foreach (var pattern in _fileSearchPatterns)
            {
                Console.WriteLine($"'{pattern}'");
            }

            Console.WriteLine($"DeleteFilesOlderThanDays: {_deleteFilesOlderThanDays}");
            Console.WriteLine($"CheckFoldersIntervalMs: {_checkFoldersIntervalMs}");

            if (_checkFoldersAtServiceStart)
            {
                Console.WriteLine("Check folders at service start");
                CheckFoldersEvent(null, null);
            }

            InitTimer();
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
