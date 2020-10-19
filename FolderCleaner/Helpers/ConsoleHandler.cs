using System;
using System.Diagnostics;
using System.Text;
using static FolderCleanerService.GlobalEnum;

namespace FolderCleanerService.Helpers
{
    public static class ConsoleHandler
    {
        public static void Print(string message, MessageType messageType = MessageType.Verbose)
        {
            string callingMethodName = new StackTrace().GetFrame(1).GetMethod().Name;
            Console.WriteLine(message);

            if (Logging.Instance != null)
            {
                Logging.Instance.AddEntry(new Logging.LogEntry(callingMethodName, messageType, message));
            }
        }

        public static void PrintProgramHeader()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Program Header:");
            sb.AppendLine(Program.ProgramHeader);
            sb.AppendLine(Program.ProgramAuthor);

            Print(sb.ToString());
        }

        public static void PrintConfiguration(FolderCleaner instance)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Program Config:");
            sb.AppendLine($"{nameof(instance.CleanupFolders)}:");

            foreach (var folder in instance.CleanupFolders)
            {
                sb.AppendLine($"'{folder}'");
            }

            sb.AppendLine($"{nameof(instance.FileSearchPatterns)}:");

            foreach (var pattern in instance.FileSearchPatterns)
            {
                sb.AppendLine($"'{pattern}'");
            }

            foreach (var prop in instance.GetType().GetProperties())
            {
                sb.AppendLine($"{prop.Name}: {prop.GetValue(instance)}");
            }

            Print(sb.ToString());
        }
    }
}
