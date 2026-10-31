using Avalonia.Controls;
using Avalonia.Interactivity;
using Project.Launch.Tools;
using Project.Launch.Download;
using System;
using System.Threading;
using System.Threading.Tasks;

using Project.Launch.Tools.JavaConfig;
using Project.Launch.Tools.LogsConfig;

namespace Project.Launch.Views
{
    public partial class DownloadWindow : Window
    {
        private readonly VersionEntry _version;
        private readonly string _targetPath;
        private readonly CancellationTokenSource _cts;
        private bool _hasStarted = false;

        /// <summary>本次安装结果（失败详情、文件计数），供调用方决定提示文案。</summary>
        public InstallResult? LastResult { get; private set; }

        public DownloadWindow(VersionEntry version, string targetPath, CancellationTokenSource cts)
        {
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            InitializeComponent();

            _version = version;
            _targetPath = targetPath;
            _cts = cts;

            TitleText.Text = $"正在下载 Minecraft {version.VersionId}";
        }

        private void OnStartClicked(object? sender, RoutedEventArgs e)
        {
            if (_hasStarted) return;
            _hasStarted = true;

            SetupPanel.IsVisible = false;
            ProgressPanel.IsVisible = true;

            int maxThreads = (int)(ThreadCountBox.Value ?? 20);
            if (maxThreads < 1) maxThreads = 1;
            if (maxThreads > 128) maxThreads = 128;

            _ = StartDownloadAsync(maxThreads);
        }

        private async Task StartDownloadAsync(int maxThreads)
        {
            var progress = new Progress<DownloadProgress>(UpdateUi);

            try
            {
                LastResult = await GameInstaller.InstallVanillaAsync(
                    _version, _targetPath, maxThreads, progress, _cts.Token);

                if (LastResult.Success)
                {
                    Close(true);
                }
                else
                {
                    // 不再把「缺文件的安装」当成功
                    StatusText.Text = LastResult.Summary;
                    LogHelper.Write(LauncherPaths.AppLog,
                        $"安装未完成：{LastResult.Summary}",
                        $"Install incomplete: {LastResult.Summary}");
                    Close(false);
                }
            }
            catch (OperationCanceledException)
            {
                LastResult = null;
                Close(false);
            }
            catch (Exception ex)
            {
                LogHelper.Write(LauncherPaths.AppLog,
                    $"下载异常: {ex.Message}",
                    $"Download error: {ex.Message}");
                Close(false);
            }
        }

        private void UpdateUi(DownloadProgress p)
        {
            StatusText.Text = p.State;
            MainProgressBar.Value = p.Percentage;

            SpeedText.Text = $"速度: {FormatBytes(p.SpeedBytesPerSec)}/s";

            if (p.TotalFiles > 0)
                FilesText.Text = p.FailedFiles > 0
                    ? $"文件: {p.CompletedFiles} / {p.TotalFiles}（失败 {p.FailedFiles}）"
                    : $"文件: {p.CompletedFiles} / {p.TotalFiles}";
            else
                FilesText.Text = "文件: -- / --";
        }

        private string FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            return $"{bytes / 1024.0 / 1024.0:F1} MB";
        }

        private void OnCancelClicked(object? sender, RoutedEventArgs e)
        {
            CancelButton.IsEnabled = false;
            CancelButton.Content = "正在取消...";
            _cts.Cancel();
        }
    }
}
