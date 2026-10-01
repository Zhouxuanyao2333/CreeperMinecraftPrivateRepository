using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Project.Launch.Tools;
using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Project.Launch.Views
{
    public partial class MainWindow : Window
    {
        private readonly Color _defaultStartColor = Color.Parse("#CCFF6B9D");
        private readonly Color _defaultEndColor = Color.Parse("#CCB366FF");
        private readonly HttpClient _httpClient = new HttpClient();

        private InstanceInfo? _currentInstance;

        public MainWindow()
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            LogHelper.Reset(LauncherPaths.MainWindowLog,
                "Kilim 启动器日志", "Kilim Launcher Log");

            Log("程序启动", "Program started");

            InitializeComponent();

            Log("InitializeComponent 完成", "InitializeComponent completed");

            var closeButton = this.FindControl<Button>("CloseButton");
            if (closeButton != null)
                closeButton.Click += (s, e) => this.Close();

            var minimizeButton = this.FindControl<Button>("MinimizeButton");
            if (minimizeButton != null)
                minimizeButton.Click += (s, e) => this.WindowState = WindowState.Minimized;

            // 主按钮
            var launchGameButton = this.FindControl<Button>("LaunchGameButton");
            if (launchGameButton != null)
                launchGameButton.Click += OnMainButtonClicked;

            // 换实例按钮（^）
            var expandButton = this.FindControl<Button>("ExpandButton");
            if (expandButton != null)
                expandButton.Click += OnExpandClicked;

            // 重新登录按钮
            var reLoginButton = this.FindControl<Button>("ReLoginButton");
            if (reLoginButton != null)
                reLoginButton.Click += OnReLoginClicked;

            Log("按钮事件绑定完成", "Button events bound");

            // 载入记忆的实例
            _currentInstance = InstanceService.GetSelectedInstance();
            if (_currentInstance != null)
            {
                Log($"已加载实例: {_currentInstance.VersionId} @ {_currentInstance.RootPath}",
                    $"Loaded instance: {_currentInstance.VersionId} @ {_currentInstance.RootPath}");
            }

            // ★ 根据登录状态决定标题栏样式
            if (!App.LoginCheck)
            {
                UpdateTitleBarGradient(_defaultStartColor, _defaultEndColor);
                Log("初始化完成（未登录，显示渐变标题栏）",
                    "Initialization complete (not logged in, gradient title bar)");

                Opened += async (s, e) =>
                {
                    try
                    {
                        await ShowLoginDialog();

                        await SetTransparentTitleBarAsync();
                        UpdateLoginStatus();

                        Log("登录窗口已关闭，切换为透明标题栏",
                            "Login window closed, switched to transparent title bar");
                    }
                    catch (Exception ex)
                    {
                        Log($"弹登录窗异常: {ex.Message}",
                            $"ShowLoginDialog exception: {ex.Message}");
                    }
                };
            }
            else
            {
                Opened += async (s, e) =>
                {
                    try
                    {
                        await SetTransparentTitleBarAsync(animate: false);
                        UpdateLoginStatus();
                        await RefreshAvatarAsync();

                        Log("初始化完成（已登录，显示透明标题栏）",
                            "Initialization complete (logged in, transparent title bar)");
                    }
                    catch (Exception ex)
                    {
                        Log($"初始化异常: {ex.Message}",
                            $"Init exception: {ex.Message}");
                    }
                };
            }
        }

        // ★ 主按钮点击：无实例 → 导入；有实例 → 启动游戏
        private async void OnMainButtonClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (_currentInstance == null)
            {
                await ImportInstanceAsync();
            }
            else
            {
                await StartGameAsync();
            }
        }

        // ★ 启动游戏
        private async Task StartGameAsync()
        {
            if (_currentInstance == null) return;

            try
            {
                string versionsDir = Path.Combine(_currentInstance.RootPath, "versions");
                string jsonPath = Path.Combine(versionsDir, _currentInstance.VersionId,
                    _currentInstance.VersionId + ".json");
                if (!File.Exists(jsonPath))
                {
                    Log($"版本 json 不存在: {jsonPath}", $"Version json not found: {jsonPath}");
                    await ShowToastAsync("版本文件缺失", "Messages");
                    return;
                }

                var vj = MinecraftVersionJson.Load(jsonPath);
                if (vj == null)
                {
                    await ShowToastAsync("版本解析失败", "Messages");
                    return;
                }

                int requiredJava = vj.JavaVersion?.MajorVersion ?? 8;
                Log($"版本要求 Java {requiredJava}", $"Version requires Java {requiredJava}");

                var javas = await JavaDetector.DetectAllAsync();
                Log($"检测到 {javas.Count} 个 Java", $"Detected {javas.Count} Java(s)");
                foreach (var j in javas)
                    Log($"  Java: {j.Path} (major={j.MajorVersion})",
                        $"  Java: {j.Path} (major={j.MajorVersion})");

                var matched = JavaDetector.Match(javas, requiredJava);
                if (matched == null)
                {
                    Log($"未找到合适的 Java（需要 {requiredJava}）",
                        $"No suitable Java (need {requiredJava})");
                    await ShowToastAsync($"Java 环境异常：需要 Java {requiredJava}", "Messages");
                    return;
                }

                Log($"匹配到 Java: {matched.Path}", $"Matched Java: {matched.Path}");

                _currentInstance.JavaPath = matched.Path;
                InstanceService.SetSelectedInstance(_currentInstance);

                string playerName = App.PlayerName ?? "Steve";
                string playerUuid = App.OfflineUuid
                    ?? OfflineUuidGenerator.GenerateUuidString(playerName).Replace("-", "");

                await ShowToastAsync("正在启动游戏...", "Messages");

                bool ok = await GameLauncher.LaunchAsync(
                    _currentInstance, vj, matched, playerName, playerUuid);

                if (ok)
                    await ShowToastAsync("游戏已启动", "Messages");
                else
                    await ShowToastAsync("启动失败，查看日志", "Messages");
            }
            catch (Exception ex)
            {
                Log($"启动游戏异常: {ex.Message}", $"Start game exception: {ex.Message}");
                await ShowToastAsync("启动失败", "Messages");
            }
        }

        // ★ 点"^"按钮 → 更换实例目录（复用导入逻辑）
        private async void OnExpandClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            await ImportInstanceAsync();
        }

        // ★ 导入 / 更换实例流程
        private async Task ImportInstanceAsync()
        {
            try
            {
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel == null) return;

                var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    Title = "选择 Minecraft 目录（.minecraft 根目录或隔离实例目录）",
                    AllowMultiple = false
                });

                if (folders.Count < 1)
                {
                    Log("用户取消导入", "User cancelled import");
                    return;
                }

                var folder = folders[0];
                string path = folder.Path.LocalPath;
                Log($"用户选择目录: {path}", $"User selected directory: {path}");

                var result = MinecraftDirectoryAnalyzer.Analyze(path);
                if (!result.IsValid)
                {
                    Log("目录无效（未找到 versions/ 或 .minecraft/versions/）",
                        "Invalid directory (no versions/ or .minecraft/versions/)");
                    await ShowToastAsync("未找到有效的 Minecraft 目录", "Messages");
                    return;
                }

                Log($"分析结果: Layout={result.Layout}, Versions={string.Join(",", result.Versions)}",
                    $"Analysis: Layout={result.Layout}, Versions={string.Join(",", result.Versions)}");

                string versionId = result.Versions[result.Versions.Count - 1];

                var instance = new InstanceInfo
                {
                    Name = versionId,
                    RootPath = result.RootPath,
                    Layout = result.Layout,
                    VersionId = versionId,
                    ImportedAt = DateTime.UtcNow,
                    LastUsed = DateTime.UtcNow
                };

                InstanceService.SetSelectedInstance(instance);
                _currentInstance = instance;

                Log($"实例导入成功: {versionId}", $"Instance imported: {versionId}");

                UpdateLoginStatus();
                await ShowToastAsync($"已导入 {versionId}", "Messages");
            }
            catch (Exception ex)
            {
                Log($"导入实例异常: {ex.Message}", $"Import instance exception: {ex.Message}");
                await ShowToastAsync("导入失败", "Messages");
            }
        }

        // ★ 点"登录"按钮 → 重新弹 LoginWindow
        private async void OnReLoginClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            try
            {
                await ShowLoginDialog();
                UpdateLoginStatus();
                await RefreshAvatarAsync();
            }
            catch (Exception ex)
            {
                Log($"重新登录异常: {ex.Message}", $"ReLogin exception: {ex.Message}");
            }
        }

        private async Task SetTransparentTitleBarAsync(bool animate = true)
        {
            var titleBar = this.FindControl<Border>("TitleBarBorder");
            var titleText = this.FindControl<TextBlock>("TitleText");
            var minImg = this.FindControl<Image>("MinimizeImage");
            var closeImg = this.FindControl<Image>("CloseImage");

            if (!animate)
            {
                if (titleBar != null) titleBar.Background = Brushes.Transparent;
                if (titleText != null) titleText.Foreground = new SolidColorBrush(Colors.Black);
                SetBlackIcons(minImg, closeImg);
                return;
            }

            if (minImg != null) minImg.Opacity = 0;
            if (closeImg != null) closeImg.Opacity = 0;

            if (titleBar != null) titleBar.Background = Brushes.Transparent;
            if (titleText != null) titleText.Foreground = new SolidColorBrush(Colors.Black);

            await Task.Delay(200);

            SetBlackIcons(minImg, closeImg);

            if (minImg != null) minImg.Opacity = 1;
            if (closeImg != null) closeImg.Opacity = 1;
        }

        private void SetBlackIcons(Image? minImg, Image? closeImg)
        {
            try
            {
                if (minImg != null)
                {
                    var uri = new Uri("avares://Project.Launch/src/Views/imgs/black-.png");
                    using var stream = AssetLoader.Open(uri);
                    minImg.Source = new Bitmap(stream);
                }
                if (closeImg != null)
                {
                    var uri = new Uri("avares://Project.Launch/src/Views/imgs/blackx.png");
                    using var stream = AssetLoader.Open(uri);
                    closeImg.Source = new Bitmap(stream);
                }
            }
            catch { }
        }

        // 根据登录态 + 实例状态 更新按钮显示
        private void UpdateLoginStatus()
        {
            var statusText = this.FindControl<TextBlock>("SkinStatusText");
            var launchBtn = this.FindControl<Button>("LaunchGameButton");
            var launchImg = this.FindControl<Image>("LaunchGameImage");
            var launchTxt = this.FindControl<TextBlock>("LaunchGameText");
            var expandBtn = this.FindControl<Button>("ExpandButton");
            var expandImg = this.FindControl<Image>("ExpandImage");
            var reLoginBtn = this.FindControl<Button>("ReLoginButton");
            var reLoginContainer = this.FindControl<Grid>("ReLoginContainer");

            bool loggedIn = App.IsLoggedIn && !string.IsNullOrEmpty(App.PlayerName);
            bool hasInstance = _currentInstance != null;

            if (statusText != null)
                statusText.Text = loggedIn ? App.PlayerName : "未登录";

            // 按钮图像 / 文字
            try
            {
                string mainIcon = hasInstance ? "button-Start1.png" : "button-Start3.png";
                string subIcon = hasInstance ? "button-Start2.png" : "button-Start4.png";

                if (launchImg != null)
                {
                    using var stream = AssetLoader.Open(new Uri(
                        $"avares://Project.Launch/src/Views/imgs/{mainIcon}"));
                    launchImg.Source = new Bitmap(stream);
                }
                if (launchTxt != null)
                    launchTxt.Text = hasInstance ? "开始游戏" : "导入实例";

                if (expandImg != null)
                {
                    using var stream = AssetLoader.Open(new Uri(
                        $"avares://Project.Launch/src/Views/imgs/{subIcon}"));
                    expandImg.Source = new Bitmap(stream);
                }
            }
            catch (Exception ex)
            {
                Log($"更新按钮图像失败: {ex.Message}", $"Update button icon failed: {ex.Message}");
            }

            // ★ 显示逻辑（登录优先）：
            //   未登录            → 登录按钮
            //   已登录 + 无实例   → 导入实例 + ^
            //   已登录 + 有实例   → 开始游戏 + ^
            bool showMainBtn = loggedIn;
            bool showReLogin = !loggedIn;

            if (launchBtn != null) launchBtn.IsVisible = showMainBtn;
            if (expandBtn != null) expandBtn.IsVisible = showMainBtn;
            if (reLoginBtn != null) reLoginBtn.IsVisible = showReLogin;
            if (reLoginContainer != null) reLoginContainer.IsVisible = showReLogin;
        }

        // 确保已登录时头像一定显示（用默认 Steve 兜底）
        private async Task RefreshAvatarAsync()
        {
            if (!App.IsLoggedIn) return;

            var faceImg = this.FindControl<Image>("FaceImage");
            var hatImg = this.FindControl<Image>("HatImage");
            if (faceImg == null || hatImg == null) return;

            if (faceImg.Source != null && hatImg.Source != null) return;

            try
            {
                var uri = new Uri("avares://Project.Launch/src/Views/imgs/skin/Steve.png");
                using var stream = AssetLoader.Open(uri);
                var (face, hat) = await CropSkinAsync(stream);

                if (face != null && hat != null)
                {
                    faceImg.Source = face;
                    hatImg.Source = hat;
                    RenderOptions.SetBitmapInterpolationMode(faceImg, BitmapInterpolationMode.None);
                    RenderOptions.SetBitmapInterpolationMode(hatImg, BitmapInterpolationMode.None);

                    Log("已加载默认 Steve 头像", "Default Steve avatar loaded");
                }
            }
            catch (Exception ex)
            {
                Log($"加载头像失败: {ex.Message}", $"Failed to load avatar: {ex.Message}");
            }
        }

        private void Log(string zh, string en)
        {
            LogHelper.Write(LauncherPaths.MainWindowLog, zh, en);
        }

        private async Task ShowLoginDialog()
        {
            var overlay = this.FindControl<Border>("Overlay");
            if (overlay != null) overlay.IsVisible = true;

            try
            {
                var loginWindow = new LoginWindow();
                await loginWindow.ShowDialog(this);

                UpdateLoginStatus();

                if (App.IsLoggedIn && loginWindow.FaceBitmap != null && loginWindow.HatBitmap != null)
                {
                    var faceImg = this.FindControl<Image>("FaceImage");
                    var hatImg = this.FindControl<Image>("HatImage");
                    if (faceImg != null)
                    {
                        faceImg.Source = loginWindow.FaceBitmap;
                        RenderOptions.SetBitmapInterpolationMode(faceImg, BitmapInterpolationMode.None);
                    }
                    if (hatImg != null)
                    {
                        hatImg.Source = loginWindow.HatBitmap;
                        RenderOptions.SetBitmapInterpolationMode(hatImg, BitmapInterpolationMode.None);
                    }
                }

                if (!App.IsLoggedIn)
                {
                    _ = ShowCancelToastAsync();
                }
            }
            finally
            {
                if (overlay != null) overlay.IsVisible = false;
            }
        }

        // 通用 toast
        private async Task ShowToastAsync(string message, string sound = "Messages")
        {
            SoundService.Play(sound);

            var toast = this.FindControl<Border>("CancelToast");
            var toastText = this.FindControl<TextBlock>("ToastText");
            if (toast == null) return;

            if (toastText != null)
                toastText.Text = message;

            toast.IsVisible = true;
            toast.Opacity = 0;

            await Task.Delay(20);
            toast.Opacity = 1;

            await Task.Delay(2500);

            toast.Opacity = 0;
            await Task.Delay(320);
            toast.IsVisible = false;
        }

        private Task ShowCancelToastAsync() => ShowToastAsync("用户取消登录", "Messages");

        private async Task<(Bitmap? face, Bitmap? hat)> CropSkinAsync(Stream stream)
        {
            try
            {
                byte[] imageData;
                using (var ms = new MemoryStream())
                {
                    await stream.CopyToAsync(ms);
                    imageData = ms.ToArray();
                }

                return await Task.Run<(Bitmap? face, Bitmap? hat)>(() =>
                {
                    try
                    {
                        using var bitmap = new Bitmap(new MemoryStream(imageData));
                        int width = bitmap.PixelSize.Width;
                        int height = bitmap.PixelSize.Height;

                        bool isValidSize = (width == 64 && height == 32) ||
                                           (width == 64 && height == 64) ||
                                           (width == 128 && height == 128);
                        if (!isValidSize)
                            return (null, null);

                        int faceX, faceY, faceSize, hatX, hatY;

                        if (width >= 128 && height >= 128)
                        {
                            faceX = 16; faceY = 16; faceSize = 16;
                            hatX = 80; hatY = 16;
                        }
                        else
                        {
                            faceX = 8; faceY = 8; faceSize = 8;
                            hatX = 40; hatY = 8;
                        }

                        var faceCropped = new CroppedBitmap(bitmap, new PixelRect(faceX, faceY, faceSize, faceSize));
                        var hatCropped = new CroppedBitmap(bitmap, new PixelRect(hatX, hatY, faceSize, faceSize));

                        var faceBmp = new RenderTargetBitmap(new PixelSize(faceSize, faceSize), new Vector(96, 96));
                        using (var ctx = faceBmp.CreateDrawingContext())
                            ctx.DrawImage(faceCropped, new Rect(0, 0, faceSize, faceSize));

                        var hatBmp = new RenderTargetBitmap(new PixelSize(faceSize, faceSize), new Vector(96, 96));
                        using (var ctx = hatBmp.CreateDrawingContext())
                            ctx.DrawImage(hatCropped, new Rect(0, 0, faceSize, faceSize));

                        return (faceBmp, hatBmp);
                    }
                    catch
                    {
                        return (null, null);
                    }
                });
            }
            catch
            {
                return (null, null);
            }
        }

        private void UpdateTitleBarGradient(Color startColor, Color endColor)
        {
            var titleBar = this.FindControl<Border>("TitleBarBorder");
            if (titleBar == null) return;

            var gradient = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                GradientStops = new GradientStops
                {
                    new GradientStop(startColor, 0.0),
                    new GradientStop(endColor, 1.0)
                }
            };

            titleBar.Background = gradient;
            UpdateTitleBarForeground(startColor, endColor);
        }

        private void UpdateTitleBarForeground(Color startColor, Color endColor)
        {
            double avgLuminance = (GetLuminance(startColor) + GetLuminance(endColor)) / 2.0;
            bool useDark = avgLuminance > 0.5;

            var titleText = this.FindControl<TextBlock>("TitleText");
            if (titleText != null)
                titleText.Foreground = useDark ? new SolidColorBrush(Colors.Black) : new SolidColorBrush(Colors.White);

            var minImg = this.FindControl<Image>("MinimizeImage");
            if (minImg != null)
            {
                var path = useDark ? "avares://Project.Launch/src/Views/imgs/black-.png"
                                   : "avares://Project.Launch/src/Views/imgs/white-.png";
                try
                {
                    var uri = new Uri(path);
                    using var stream = AssetLoader.Open(uri);
                    minImg.Source = new Bitmap(stream);
                }
                catch { }
            }

            var closeImg = this.FindControl<Image>("CloseImage");
            if (closeImg != null)
            {
                var path = useDark ? "avares://Project.Launch/src/Views/imgs/blackx.png"
                                   : "avares://Project.Launch/src/Views/imgs/whitex.png";
                try
                {
                    var uri = new Uri(path);
                    using var stream = AssetLoader.Open(uri);
                    closeImg.Source = new Bitmap(stream);
                }
                catch { }
            }
        }

        private static double GetLuminance(Color color)
        {
            double r = color.R / 255.0;
            double g = color.G / 255.0;
            double b = color.B / 255.0;

            r = r <= 0.03928 ? r / 12.92 : Math.Pow((r + 0.055) / 1.055, 2.4);
            g = g <= 0.03928 ? g / 12.92 : Math.Pow((g + 0.055) / 1.055, 2.4);
            b = b <= 0.03928 ? b / 12.92 : Math.Pow((b + 0.055) / 1.055, 2.4);

            return 0.2126 * r + 0.7152 * g + 0.0722 * b;
        }

        private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                BeginMoveDrag(e);
        }

        public void SetTitleBarGradient(Color start, Color end)
        {
            UpdateTitleBarGradient(start, end);
        }
    }
}