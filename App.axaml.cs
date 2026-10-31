using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Project.Launch.Views;
using System;
using Project.Launch.Tools;

using Project.Launch.Auth;
using Project.Launch.Tools.JavaConfig;
using Project.Launch.Tools.LogsConfig;
namespace Project.Launch
{
    public partial class App : Application
    {
        public static bool LoginCheck { get; set; }
        public static bool IsLoggedIn { get; set; }
        public static string? PlayerName { get; set; }
        public static string? OfflineUuid { get; set; }

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);

            InstallCrashGuards();
        }

        /// <summary>
        /// 全局异常兜底。
        /// 之前 MainWindow 的 async void 事件处理器里一旦抛异常（例如文件夹选择器内部报错），
        /// 异常会回到 Dispatcher 并终止整个进程 —— 用户看到的是「窗口无响应然后崩溃」。
        /// 这里把 UI 线程上的未处理异常记录后吞掉，保证单个交互出错不会带走整个启动器。
        /// </summary>
        private static void InstallCrashGuards()
        {
            Avalonia.Threading.Dispatcher.UIThread.UnhandledException += (_, e) =>
            {
                try
                {
                    LogHelper.Write(LauncherPaths.AppLog,
                        $"UI 线程未处理异常（已拦截，程序继续运行）: {e.Exception}",
                        $"Unhandled UI exception (suppressed): {e.Exception}");

                    System.IO.File.AppendAllText(LauncherPaths.CrashLog,
                        $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\n[UI-Thread Suppressed]\n{e.Exception}\n\n");
                }
                catch { }

                // 标记为已处理：避免单个事件处理器异常把整个应用带崩
                e.Handled = true;
            };

            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                try
                {
                    System.IO.File.AppendAllText(LauncherPaths.CrashLog,
                        $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\n[AppDomain]\n{e.ExceptionObject}\n\n");
                }
                catch { }
            };

            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (_, e) =>
            {
                try
                {
                    LogHelper.Write(LauncherPaths.AppLog,
                        $"未观察的任务异常: {e.Exception?.GetBaseException().Message}",
                        $"Unobserved task exception: {e.Exception?.GetBaseException().Message}");
                }
                catch { }

                e.SetObserved();
            };
        }

        public override void OnFrameworkInitializationCompleted()
        {
            LogHelper.Write(LauncherPaths.AppLog,
                "应用初始化开始", "Application initialization started");

            var account = AccountService.GetSelectedAccount();
            if (account != null)
            {
                PlayerName = account.Username;
                OfflineUuid = account.Uuid;
                IsLoggedIn = true;
                LoginCheck = AccountService.GetLoginCheck();

                LogHelper.Write(LauncherPaths.AppLog,
                    $"已加载账户: {PlayerName}，跳过登录: {LoginCheck}",
                    $"Account loaded: {PlayerName}, LoginCheck: {LoginCheck}");
            }
            else
            {
                PlayerName = null;
                OfflineUuid = null;
                IsLoggedIn = false;
                LoginCheck = false;

                AccountService.SetLoginCheck(false);

                LogHelper.Write(LauncherPaths.AppLog,
                    "未找到账户，LoginCheck 设为 false",
                    "No account found, LoginCheck set to false");
            }

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new MainWindow();
                base.OnFrameworkInitializationCompleted();
            }

            LogHelper.Write(LauncherPaths.AppLog,
                "应用初始化完成", "Application initialization complete");
        }
    }
}