using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using LiquidGlassAvaloniaUI;
using Project.Launch.Tools;
using System;
using System.IO;
using System.Linq;
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
        private Bitmap? _fullSkinBitmap;

        private Button? _homeTab, _launchTab, _downloadTab, _settingsTab, _toolsTab;
        private LiquidGlassSurface? _tabSlider;
        private TranslateTransform? _tabSliderTransform;
        private ScaleTransform? _tabSliderScale;
        private int _selectedTab = 2;
        private DispatcherTimer? _tabAnimTimer;
        private double _tabAnimStartX, _tabAnimTargetX;
        private DateTime _tabAnimStartTime;

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

            var launchGameButton = this.FindControl<Button>("LaunchGameButton");
            if (launchGameButton != null)
                launchGameButton.Click += OnMainButtonClicked;

            var expandButton = this.FindControl<Button>("ExpandButton");
            if (expandButton != null)
                expandButton.Click += OnExpandClicked;

            var reLoginButton = this.FindControl<Button>("ReLoginButton");
            if (reLoginButton != null)
                reLoginButton.Click += OnReLoginClicked;

            InitializeTabSelector();

            Log("按钮事件绑定完成", "Button events bound");

            _currentInstance = InstanceService.GetSelectedInstance();
            if (_currentInstance != null)
            {
                Log($"已加载实例: {_currentInstance.VersionId} @ {_currentInstance.RootPath}",
                    $"Loaded instance: {_currentInstance.VersionId} @ {_currentInstance.RootPath}");
            }

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

        private void InitializeTabSelector()
        {
            _homeTab = this.FindControl<Button>("HomeTabButton");
            _launchTab = this.FindControl<Button>("LaunchTabButton");
            _downloadTab = this.FindControl<Button>("DownloadTabButton");
            _settingsTab = this.FindControl<Button>("SettingsTabButton");
            _toolsTab = this.FindControl<Button>("ToolsTabButton");

            _tabSlider = this.FindControl<LiquidGlassSurface>("TabSlider");
            if (_tabSlider != null)
            {
                var group = _tabSlider.RenderTransform as TransformGroup;
                if (group != null)
                {
                    _tabSliderTransform = group.Children.OfType<TranslateTransform>().FirstOrDefault();
                    _tabSliderScale = group.Children.OfType<ScaleTransform>().FirstOrDefault();
                }

                if (_tabSliderTransform != null)
                {
                    _tabSliderTransform.X = _selectedTab * AnimationConfig.TabStep;
                    _tabSliderTransform.Y = 0;
                }
                if (_tabSliderScale != null)
                {
                    _tabSliderScale.ScaleX = 1.0;
                    _tabSliderScale.ScaleY = 1.0;
                }
            }

            if (_downloadTab != null) _downloadTab.Click += (s, e) => SelectTab(0);
            if (_launchTab != null) _launchTab.Click += (s, e) => SelectTab(1);
            if (_homeTab != null) _homeTab.Click += (s, e) => SelectTab(2);
            if (_settingsTab != null) _settingsTab.Click += (s, e) => SelectTab(3);
            if (_toolsTab != null) _toolsTab.Click += (s, e) => SelectTab(4);

            UpdateTabVisuals(_selectedTab);
        }

        private void SelectTab(int index)
        {
            if (index == _selectedTab) return;
            int oldIndex = _selectedTab;
            _selectedTab = index;

            AnimateTabSliderTo(index);
            UpdateTabVisuals(index);
            _ = SwitchPanelAsync(oldIndex, index);
        }

        private void UpdateTabVisuals(int index)
        {
            var tabs = new[] { _downloadTab, _launchTab, _homeTab, _settingsTab, _toolsTab };
            for (int i = 0; i < tabs.Length; i++)
            {
                if (tabs[i] != null)
                {
                    tabs[i]!.Foreground = (i == index)
                        ? new SolidColorBrush(Colors.Black)
                        : new SolidColorBrush(Color.Parse("#666666"));
                    tabs[i]!.IsHitTestVisible = (i != index);
                }
            }
        }

        private async Task SwitchPanelAsync(int oldIndex, int newIndex)
        {
            var panels = new Grid?[]
            {
                this.FindControl<Grid>("DownloadPanel"),
                this.FindControl<Grid>("LaunchPanel"),
                this.FindControl<Grid>("HomePanel"),
                this.FindControl<Grid>("SettingsPanel"),
                this.FindControl<Grid>("ToolsPanel")
            };

            var oldPanel = (oldIndex >= 0 && oldIndex < panels.Length) ? panels[oldIndex] : null;
            if (oldPanel != null && oldPanel.IsVisible)
            {
                oldPanel.Opacity = 0;
                await Task.Delay(AnimationConfig.TabPanelFadeDuration);
                oldPanel.IsVisible = false;
            }

            var newPanel = (newIndex >= 0 && newIndex < panels.Length) ? panels[newIndex] : null;
            if (newPanel != null)
            {
                newPanel.Opacity = 0;
                newPanel.IsVisible = true;
                await Task.Delay(20);
                newPanel.Opacity = 1;
            }
        }

        private void AnimateTabSliderTo(int index)
        {
            if (_tabSliderTransform == null || _tabSlider == null) return;

            double targetX = index * AnimationConfig.TabStep;
            double startX = _tabSliderTransform.X;

            _tabAnimTimer?.Stop();

            _tabAnimStartX = startX;
            _tabAnimTargetX = targetX;
            _tabAnimStartTime = DateTime.Now;

            double halfW = AnimationConfig.TabWidth / 2.0;
            double halfH = AnimationConfig.TabHeight / 2.0;

            _tabAnimTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16)
            };
            _tabAnimTimer.Tick += (s, e) =>
            {
                var elapsed = DateTime.Now - _tabAnimStartTime;
                double progress = elapsed.TotalMilliseconds / AnimationConfig.TabMoveDuration.TotalMilliseconds;
                if (progress >= 1.0)
                {
                    progress = 1.0;
                    _tabAnimTimer.Stop();
                }

                double eased = progress < 0.5
                    ? 4 * progress * progress * progress
                    : 1 - Math.Pow(-2 * progress + 2, 3) / 2;
                double baseX = _tabAnimStartX + (_tabAnimTargetX - _tabAnimStartX) * eased;

                double scaleCurve;
                if (progress <= AnimationConfig.TabScaleRiseEnd)
                {
                    double t = progress / AnimationConfig.TabScaleRiseEnd;
                    scaleCurve = t * t * (3 - 2 * t);
                }
                else if (progress <= AnimationConfig.TabScaleFallStart)
                {
                    scaleCurve = 1.0;
                }
                else
                {
                    double t = (progress - AnimationConfig.TabScaleFallStart) / (1.0 - AnimationConfig.TabScaleFallStart);
                    scaleCurve = 1.0 - t * t * (3 - 2 * t);
                }
                double currentScale = 1.0 + (AnimationConfig.TabPeakScale - 1.0) * scaleCurve;

                double blurCurve = 1.0 - Math.Abs(progress * 2.0 - 1.0);
                _tabSlider.BlurRadius = AnimationConfig.TabBlurPeak * blurCurve;

                double xComp = -(currentScale - 1.0) * halfW;
                double yComp = -(currentScale - 1.0) * halfH;

                _tabSliderTransform.X = baseX + xComp;
                _tabSliderTransform.Y = yComp;

                if (_tabSliderScale != null)
                {
                    _tabSliderScale.ScaleX = currentScale;
                    _tabSliderScale.ScaleY = currentScale;
                }

                if (progress >= 1.0)
                {
                    _tabSliderTransform.X = _tabAnimTargetX;
                    _tabSliderTransform.Y = 0;
                    if (_tabSliderScale != null)
                    {
                        _tabSliderScale.ScaleX = 1.0;
                        _tabSliderScale.ScaleY = 1.0;
                    }
                    _tabSlider.BlurRadius = 0;
                }
            };
            _tabAnimTimer.Start();
        }

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

        private async void OnExpandClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            await ImportInstanceAsync();
        }

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

        private async void OnReLoginClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            try
            {
                UpdateTitleBarGradient(_defaultStartColor, _defaultEndColor);

                await ShowLoginDialog();
                UpdateLoginStatus();
                await RefreshAvatarAsync();

                await SetTransparentTitleBarAsync();
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

        private void UpdateLoginStatus()
        {
            var homeNotLoggedIn = this.FindControl<StackPanel>("HomeNotLoggedIn");
            var homeLoggedIn = this.FindControl<StackPanel>("HomeLoggedIn");
            var homePlayerName = this.FindControl<TextBlock>("HomePlayerName");
            var launchBtn = this.FindControl<Button>("LaunchGameButton");
            var launchImg = this.FindControl<Image>("LaunchGameImage");
            var launchTxt = this.FindControl<TextBlock>("LaunchGameText");
            var expandBtn = this.FindControl<Button>("ExpandButton");
            var expandImg = this.FindControl<Image>("ExpandImage");
            var reLoginBtn = this.FindControl<Button>("ReLoginButton");
            var reLoginContainer = this.FindControl<Grid>("ReLoginContainer");

            var launchNotLoggedIn = this.FindControl<StackPanel>("LaunchNotLoggedIn");
            var skinViewer = this.FindControl<Pivot3DControl>("SkinViewer");
            var launchButtonsPanel = this.FindControl<StackPanel>("LaunchButtonsPanel");

            bool loggedIn = App.IsLoggedIn && !string.IsNullOrEmpty(App.PlayerName);
            bool hasInstance = _currentInstance != null;

            if (homeNotLoggedIn != null) homeNotLoggedIn.IsVisible = !loggedIn;
            if (homeLoggedIn != null) homeLoggedIn.IsVisible = loggedIn;
            if (homePlayerName != null) homePlayerName.Text = App.PlayerName ?? "Steve";

            if (launchNotLoggedIn != null) launchNotLoggedIn.IsVisible = !loggedIn;
            if (skinViewer != null) skinViewer.IsVisible = loggedIn;
            if (launchButtonsPanel != null) launchButtonsPanel.IsVisible = loggedIn;

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

            bool showMainBtn = loggedIn;
            bool showReLogin = !loggedIn;

            if (launchBtn != null) launchBtn.IsVisible = showMainBtn;
            if (expandBtn != null) expandBtn.IsVisible = showMainBtn;
            if (reLoginBtn != null) reLoginBtn.IsVisible = showReLogin;
            if (reLoginContainer != null) reLoginContainer.IsVisible = showReLogin;
        }

        private async Task RefreshAvatarAsync()
        {
            if (!App.IsLoggedIn) return;

            // 3D 皮肤
            try
            {
                var uri = new Uri("avares://Project.Launch/src/Views/imgs/skin/Steve.png");
                using var stream = AssetLoader.Open(uri);

                _fullSkinBitmap?.Dispose();
                _fullSkinBitmap = new Bitmap(stream);

                var skinViewer = this.FindControl<Pivot3DControl>("SkinViewer");
                if (skinViewer != null)
                {
                    skinViewer.Skin = _fullSkinBitmap;
                }

                Log("已加载 3D 皮肤", "3D skin loaded");
            }
            catch (Exception ex)
            {
                Log($"加载 3D 皮肤失败: {ex.Message}", $"Failed to load 3D skin: {ex.Message}");
            }

            // 主页 2D 头像
            var faceImg = this.FindControl<Image>("HomeFaceImage");
            var hatImg = this.FindControl<Image>("HomeHatImage");
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
            var contentArea = this.FindControl<Grid>("ContentArea");

            if (contentArea != null)
            {
                contentArea.IsVisible = false;
            }

            await Task.Delay(150);

            try
            {
                var loginWindow = new LoginWindow();
                await loginWindow.ShowDialog(this);

                UpdateLoginStatus();

                if (App.IsLoggedIn && loginWindow.FaceBitmap != null && loginWindow.HatBitmap != null)
                {
                    var faceImg = this.FindControl<Image>("HomeFaceImage");
                    var hatImg = this.FindControl<Image>("HomeHatImage");
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
                if (contentArea != null)
                {
                    contentArea.IsVisible = true;
                }
            }
        }

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