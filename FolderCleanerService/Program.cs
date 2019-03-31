using System;
using System.Reflection;
using Topshelf;

namespace FolderCleanerService
{
    class Program
    {
        #region Program configuration

        const string VERSION_INFO = "";

        #if DEBUG
        const string BUILD_CONFIGURATION = " [Debug]";
        #else
        const string BUILD_CONFIGURATION = "";
        #endif

        #endregion

        #region Build/program info

        private static Version Ver { get; } = new AssemblyName(Assembly.GetExecutingAssembly().FullName).Version;
        public static string ProgramVersion { get; } = $"{Ver.Major}.{Ver.Minor}.{Ver.Build}";
        public static string ProgramBaseDirectory { get; } = AppDomain.CurrentDomain.BaseDirectory;
        public static string ProgramPath { get; } = Assembly.GetEntryAssembly().Location;
        public static string ProgramSHA256 { get; } = Utils.CalculateFileSHA256(ProgramPath);
        public static string ProgramName { get; } = Assembly.GetExecutingAssembly().GetName().Name;
        public static string ProgramHeader { get; } = $"{ProgramName} v{ProgramVersion}{VERSION_INFO}{BUILD_CONFIGURATION}";
        public static string ProgramBuild { get; } = $"Build: {Properties.Resources.BuildTime} (UTC) CLR: {Assembly.GetExecutingAssembly().ImageRuntimeVersion} SHA256: {ProgramSHA256}";
        public static string ProgramLastCommit { get; } = $"Commit: {Properties.Resources.LastCommit}";
        public static string ProgramAuthor { get; } = "Author: Daniil Shipilin";

        #endregion

        static void Main(string[] args)
        {
            var host = HostFactory.New(x =>
            {
                x.Service<FolderCleaner>(sc =>
                {
                    sc.ConstructUsing(() => new FolderCleaner());

                    // the start and stop methods for the service
                    sc.WhenStarted((s, hostControl) => s.Start(hostControl));
                    sc.WhenStopped((s, hostControl) => s.Stop(hostControl));

                    // optional pause/continue methods if used
                    //sc.WhenPaused(s => s.Pause());
                    //sc.WhenContinued(s => s.Continue());

                    // optional, when shutdown is supported
                    sc.WhenShutdown((s, hostControl) => s.Shutdown(hostControl));
                });

                x.RunAsLocalSystem();

                x.SetServiceName("FolderCleaner_Service");
                x.SetDisplayName("Folder Cleaner Service");
                x.SetDescription("Folder cleaner service application.");
                x.SetHelpTextPrefix(ProgramBuild);

                //x.EnablePauseAndContinue();
                x.EnableShutdown();

                x.StartAutomatically();
                //x.StartAutomaticallyDelayed();
                //x.StartManually();
                //x.Disabled();
            });

            var serviceExitCode = host.Run();

            Environment.ExitCode = (int)serviceExitCode;
        }
    }
}
