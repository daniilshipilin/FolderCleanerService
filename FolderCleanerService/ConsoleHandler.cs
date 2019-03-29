using System;
using System.Diagnostics;
using static FolderCleanerService.GlobalEnum;

namespace FolderCleanerService
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
    }
}
