using System;
using System.IO;

namespace Project.Launch.Tools
{
    /// <summary>
    /// 启动器所有关键路径集中管理
    /// Launcher path definitions - centralized
    /// </summary>
    public static class LauncherPaths
    {
        /// <summary>
        /// 启动器根目录：exe 同级 KilimLauncher/
        /// Launcher root: KilimLauncher/ next to exe
        /// </summary>
        public static readonly string Root = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "KilimLauncher"
        );

        // ================== 配置文件 ==================
        /// <summary>账户文件 / Accounts file</summary>
        public static readonly string AccountsFile = Path.Combine(Root, "accounts.json");

        // ================== 日志文件 ==================
        /// <summary>主窗口日志 / Main window log</summary>
        public static readonly string MainWindowLog = Path.Combine(Root, "MainWindowLog.txt");

        /// <summary>启动日志 / Startup log</summary>
        public static readonly string StartupLog = Path.Combine(Root, "startup.log");

        /// <summary>崩溃日志 / Crash log</summary>
        public static readonly string CrashLog = Path.Combine(Root, "crash.log");

        /// <summary>App 启动日志 / App startup log</summary>
        public static readonly string AppLog = Path.Combine(Root, "app.log");

        static LauncherPaths()
        {
            try
            {
                if (!Directory.Exists(Root))
                    Directory.CreateDirectory(Root);
            }
            catch { }
        }
    }
}