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
using System.Collections.Generic;
using Project.Launch.Download;

using Project.Launch;
using Project.Launch.Auth;
using Project.Launch.Tools.AnimSettings;
using Project.Launch.Tools.JavaConfig;
using Project.Launch.Tools.LogsConfig;
using Project.Launch.Tools.Player3DConfig;
using Project.Launch.Tools.SoundControl;
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

        // 下载页：基岩版 / Java版 选择器
        private Button? _bedrockEditionBtn, _javaEditionBtn;
        private StackPanel? _bedrockPanel;
        private Grid? _javaPanel;
        private LiquidGlassSurface? _editionSlider;
        private TranslateTransform? _editionSliderTransform;
        private ScaleTransform? _editionSliderScale;
        private int _selectedEdition = 0;
        private DispatcherTimer? _editionAnimTimer;
        private double _editionAnimStartX, _editionAnimTargetX;
        private DateTime _editionAnimStartTime;

        // Java 版版本列表
        private List<VersionEntry>? _allVersions;
        private bool _isVersionsLoading = false;
        private VersionEntry? _selectedVersion;

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
            InitializeEditionSelector();
            InitializeVersionList();

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
                Log("初始化完成（未登录，显示渐变标题栏）", "Initialization complete (not logged in, gradient title bar)");

                Opened += async (s, e) =>
                {
                    try
                    {
                        await ShowLoginDialog();
                        await SetTransparentTitleBarAsync();
                        UpdateLoginStatus();
                        await RefreshAvatarAsync();
                        Log("登录窗口已关闭，切换为透明标题栏", "Login window closed, switched to transparent title bar");
                    }
                    catch (Exception ex)
                    {
                        Log($"弹登录窗异常: {ex.Message}", $"ShowLoginDialog exception: {ex.Message}");
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
                        Log("初始化完成（已登录，显示透明标题栏）", "Initialization complete (logged in, transparent title bar)");
                    }
                    catch (Exception ex)
                    {
                        Log($"初始化异常: {ex.Message}", $"Init exception: {ex.Message}");
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

            _tabAnimTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            _tabAnimTimer.Tick += (s, e) =>
            {
                var elapsed = DateTime.Now - _tabAnimStartTime;
                double progress = elapsed.TotalMilliseconds / AnimationConfig.TabMoveDuration.TotalMilliseconds;
                if (progress >= 1.0)
                {
                    progress = 1.0;
                    _tabAnimTimer.Stop();
                }

                double eased = progress < 0.5 ? 4 * progress * progress * progress : 1 - Math.Pow(-2 * progress + 2, 3) / 2;
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

        private void InitializeEditionSelector()
        {
            _bedrockEditionBtn = this.FindControl<Button>("BedrockEditionButton");
            _javaEditionBtn = this.FindControl<Button>("JavaEditionButton");
            _bedrockPanel = this.FindControl<StackPanel>("BedrockPanel");
            _javaPanel = this.FindControl<Grid>("JavaPanel");

            _editionSlider = this.FindControl<LiquidGlassSurface>("EditionSlider");
            if (_editionSlider != null)
            {
                var group = _editionSlider.RenderTransform as TransformGroup;
                if (group != null)
                {
                    _editionSliderTransform = group.Children.OfType<TranslateTransform>().FirstOrDefault();
                    _editionSliderScale = group.Children.OfType<ScaleTransform>().FirstOrDefault();
                }

                if (_editionSliderTransform != null)
                {
                    _editionSliderTransform.X = 0;
                    _editionSliderTransform.Y = 0;
                }
                if (_editionSliderScale != null)
                {
                    _editionSliderScale.ScaleX = 1.0;
                    _editionSliderScale.ScaleY = 1.0;
                }
            }

            if (_bedrockEditionBtn != null) _bedrockEditionBtn.Click += (s, e) => SelectEdition(0);
            if (_javaEditionBtn != null) _javaEditionBtn.Click += (s, e) => SelectEdition(1);

            UpdateEditionVisuals(0);
        }

        private void SelectEdition(int index)
        {
            if (index == _selectedEdition) return;
            _selectedEdition = index;
            AnimateEditionSliderTo(index);
            UpdateEditionVisuals(index);
        }

        private void UpdateEditionVisuals(int index)
        {
            var buttons = new[] { _bedrockEditionBtn, _javaEditionBtn };
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] != null)
                {
                    buttons[i]!.Foreground = (i == index)
                        ? new SolidColorBrush(Colors.Black)
                        : new SolidColorBrush(Color.Parse("#666666"));
                    buttons[i]!.IsHitTestVisible = (i != index);
                }
            }

            if (_bedrockPanel != null) _bedrockPanel.IsVisible = (index == 0);
            if (_javaPanel != null) _javaPanel.IsVisible = (index == 1);
        }

        private void AnimateEditionSliderTo(int index)
        {
            if (_editionSliderTransform == null || _editionSlider == null) return;

            double targetX = index * 120;
            double startX = _editionSliderTransform.X;

            _editionAnimTimer?.Stop();

            _editionAnimStartX = startX;
            _editionAnimTargetX = targetX;
            _editionAnimStartTime = DateTime.Now;

            TimeSpan duration = AnimationConfig.SliderMoveDuration;
            double peakScale = AnimationConfig.SliderPeakScale;
            double riseEnd = AnimationConfig.SliderScaleRiseEnd;
            double fallStart = AnimationConfig.SliderScaleFallStart;
            double blurPeak = AnimationConfig.SliderBlurPeak;
            double halfW = AnimationConfig.SliderWidth / 2.0;
            double halfH = AnimationConfig.SliderHeight / 2.0;

            _editionAnimTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            _editionAnimTimer.Tick += (s, e) =>
            {
                var elapsed = DateTime.Now - _editionAnimStartTime;
                double progress = elapsed.TotalMilliseconds / duration.TotalMilliseconds;
                if (progress >= 1.0)
                {
                    progress = 1.0;
                    _editionAnimTimer.Stop();
                }

                double eased = progress < 0.5 ? 4 * progress * progress * progress : 1 - Math.Pow(-2 * progress + 2, 3) / 2;
                double baseX = _editionAnimStartX + (_editionAnimTargetX - _editionAnimStartX) * eased;

                double scaleCurve;
                if (progress <= riseEnd)
                {
                    double t = progress / riseEnd;
                    scaleCurve = t * t * (3 - 2 * t);
                }
                else if (progress <= fallStart)
                {
                    scaleCurve = 1.0;
                }
                else
                {
                    double t = (progress - fallStart) / (1.0 - fallStart);
                    scaleCurve = 1.0 - t * t * (3 - 2 * t);
                }
                double currentScale = 1.0 + (peakScale - 1.0) * scaleCurve;

                double blurCurve = 1.0 - Math.Abs(progress * 2.0 - 1.0);
                _editionSlider.BlurRadius = blurPeak * blurCurve;

                double xComp = -(currentScale - 1.0) * halfW;
                double yComp = -(currentScale - 1.0) * halfH;

                _editionSliderTransform.X = baseX + xComp;
                _editionSliderTransform.Y = yComp;

                if (_editionSliderScale != null)
                {
                    _editionSliderScale.ScaleX = currentScale;
                    _editionSliderScale.ScaleY = currentScale;
                }

                if (progress >= 1.0)
                {
                    _editionSliderTransform.X = _editionAnimTargetX;
                    _editionSliderTransform.Y = 0;
                    if (_editionSliderScale != null)
                    {
                        _editionSliderScale.ScaleX = 1.0;
                        _editionSliderScale.ScaleY = 1.0;
                    }
                    _editionSlider.BlurRadius = 0;
                }
            };
            _editionAnimTimer.Start();
        }

        private void InitializeVersionList()
        {
            _ = LoadVersionManifestAsync();
        }

        private async Task LoadVersionManifestAsync()
        {
            if (_isVersionsLoading) return;
            _isVersionsLoading = true;

            var loadingPanel = this.FindControl<StackPanel>("VersionLoadingPanel");
            var errorPanel = this.FindControl<StackPanel>("VersionErrorPanel");
            var listBox = this.FindControl<ListBox>("VersionListBox");

            if (loadingPanel != null) loadingPanel.IsVisible = true;
            if (errorPanel != null) errorPanel.IsVisible = false;
            if (listBox != null) listBox.IsVisible = false;

            var manifest = await VersionService.GetVersionManifestAsync();

            if (manifest != null && manifest.Versions.Count > 0)
            {
                _allVersions = manifest.Versions.OrderByDescending(v => v.ReleaseTime).ToList();

                foreach (var version in _allVersions)
                {
                    version.IconPath = GetVersionIcon(version);
                    try
                    {
                        using var stream = AssetLoader.Open(new Uri(version.IconPath));
                        version.IconBitmap = new Bitmap(stream);
                    }
                    catch (Exception ex)
                    {
                        Log($"加载图标失败: {version.IconPath} - {ex.Message}", $"Failed to load icon: {version.IconPath} - {ex.Message}");
                    }
                }

                ApplyVersionFilter();

                if (loadingPanel != null) loadingPanel.IsVisible = false;
                if (listBox != null) listBox.IsVisible = true;
            }
            else
            {
                if (loadingPanel != null) loadingPanel.IsVisible = false;
                if (errorPanel != null) errorPanel.IsVisible = true;
            }

            _isVersionsLoading = false;
        }

        private string GetVersionIcon(VersionEntry version)
        {
            const string iconBase = "avares://Project.Launch/MainSrc/Views/imgs/Main/images/Blocks/";

            var aprilFoolsIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "15w14a", "1.rv-pre1", "3d shareware v1.34", "20w14infinite",
                "22w13oneblockatatime", "23w13a_or_b", "24w14potato", "25w14craftmine"
            };

            if (aprilFoolsIds.Contains(version.VersionId) || version.VersionId.Contains("potato"))
            {
                return iconBase + "chest.png";
            }

            if (version.Type.Equals("release", StringComparison.OrdinalIgnoreCase))
            {
                return iconBase + "grass2.png";
            }

            if (version.Type.Equals("snapshot", StringComparison.OrdinalIgnoreCase))
            {
                return iconBase + "GrassPath.png";
            }

            return iconBase + "furnace.png";
        }

        private void ApplyVersionFilter()
        {
            if (_allVersions == null) return;

            var listBox = this.FindControl<ListBox>("VersionListBox");
            var filterCombo = this.FindControl<ComboBox>("VersionTypeFilter");
            var searchBox = this.FindControl<TextBox>("VersionSearchBox");

            if (listBox == null || filterCombo == null || searchBox == null) return;

            string filter = (filterCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "all";
            string search = searchBox.Text?.Trim().ToLowerInvariant() ?? "";

            var filtered = _allVersions.Where(v =>
            {
                if (filter != "all" && !v.Type.Equals(filter, StringComparison.OrdinalIgnoreCase))
                    return false;
                if (!string.IsNullOrEmpty(search) && !v.VersionId.ToLowerInvariant().Contains(search))
                    return false;
                return true;
            }).ToList();

            listBox.ItemsSource = filtered;
        }

        private void OnVersionFilterChanged(object? sender, SelectionChangedEventArgs e) => ApplyVersionFilter();
        private void OnVersionSearchChanged(object? sender, TextChangedEventArgs e) => ApplyVersionFilter();
        private void OnRetryVersionsClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => _ = LoadVersionManifestAsync();

        private void OnVersionSelected(object? sender, SelectionChangedEventArgs e)
        {
            var listBox = this.FindControl<ListBox>("VersionListBox");
            _selectedVersion = listBox?.SelectedItem as VersionEntry;

            if (_selectedVersion != null)
            {
                Log($"用户选中了版本: {_selectedVersion.VersionId}", $"User selected version: {_selectedVersion.VersionId}");
            }
        }

        private async void OnVersionListDoubleTapped(object? sender, TappedEventArgs e)
        {
            if (_selectedVersion == null) return;

            // 1. 选择安装目录（选择器本身可能抛异常，必须兜住，否则 async void 会把整个进程带崩）
            string? targetPath = await PickFolderSafeAsync(
                $"选择 {_selectedVersion.VersionId} 的安装目录（即 .minecraft 文件夹）");

            if (string.IsNullOrEmpty(targetPath)) return;

            // 2. 准备后台下载 CTS
            var cts = new System.Threading.CancellationTokenSource();

            // 3. 弹窗接管 UI（逻辑类似 LoginWindow 模态窗）
            UpdateTitleBarGradient(_defaultStartColor, _defaultEndColor);
            var contentArea = this.FindControl<Grid>("ContentArea");
            if (contentArea != null) contentArea.IsVisible = false;

            bool success = false;
            InstallResult? installResult = null;
            try
            {
                var downloadWindow = new DownloadWindow(_selectedVersion, targetPath, cts);
                success = await downloadWindow.ShowDialog<bool>(this);
                installResult = downloadWindow.LastResult;
            }
            catch (Exception ex)
            {
                Log($"下载窗异常: {ex.Message}", $"Download window error: {ex.Message}");
            }
            finally
            {
                if (contentArea != null) contentArea.IsVisible = true;
                await SetTransparentTitleBarAsync();
            }

            // 4. 根据结果处理
            if (success)
            {
                await ShowToastAsync($"安装 {_selectedVersion.VersionId} 成功！", "DownloadDone");
                var result = MinecraftDirectoryAnalyzer.Analyze(targetPath);
                if (result.IsValid)
                {
                    var instance = new InstanceInfo
                    {
                        Name = _selectedVersion.VersionId,
                        RootPath = targetPath,
                        Layout = result.Layout,
                        VersionId = _selectedVersion.VersionId,
                        ImportedAt = DateTime.UtcNow,
                        LastUsed = DateTime.UtcNow
                    };
                    InstanceService.SetSelectedInstance(instance);
                    _currentInstance = instance;
                    UpdateLoginStatus();
                }
            }
            else
            {
                if (!cts.IsCancellationRequested)
                {
                    string msg = installResult != null && installResult.FailedFiles > 0
                        ? $"安装未完成：失败 {installResult.FailedFiles} / {installResult.TotalFiles} 个文件"
                        : "下载失败，请查看日志";
                    await ShowToastAsync(msg, "Errors");
                }
            }
        }

        private async void OnMainButtonClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (_currentInstance == null) await ImportInstanceAsync();
            else await StartGameAsync();
        }

        // ============================================================
        // ★ 正版链路改造点：启动游戏
        //
        // 改造前：直接用 App.PlayerName / App.OfflineUuid 按离线身份启动
        // 改造后：
        //   1. 从 AccountService 读取选中账户（不再信任 App 静态字段）
        //   2. 微软账户：检查 MC access_token 是否临近过期，过期则用 refresh_token 静默刷新
        //   3. 用 LaunchAuthInfo 承载真实身份，交给 GameLauncher
        // ============================================================
        private async Task StartGameAsync()
        {
            if (_currentInstance == null) return;

            try
            {
                string versionsDir = Path.Combine(_currentInstance.RootPath, "versions");
                string jsonPath = Path.Combine(versionsDir, _currentInstance.VersionId, _currentInstance.VersionId + ".json");
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
                    Log($"  Java: {j.Path} (major={j.MajorVersion})", $"  Java: {j.Path} (major={j.MajorVersion})");

                var matched = JavaDetector.Match(javas, requiredJava);
                if (matched == null)
                {
                    Log($"未找到合适的 Java（需要 {requiredJava}）", $"No suitable Java (need {requiredJava})");
                    await ShowToastAsync($"Java 环境异常：需要 Java {requiredJava}", "Messages");
                    return;
                }

                Log($"匹配到 Java: {matched.Path}", $"Matched Java: {matched.Path}");

                _currentInstance.JavaPath = matched.Path;
                InstanceService.SetSelectedInstance(_currentInstance);

                // ★ 读取选中账户（真身来自 accounts.json，不依赖 App 静态字段）
                var selected = AccountService.GetSelectedAccountWithId();
                if (selected == null)
                {
                    Log("未选择账户，拒绝启动", "No selected account, abort launch");
                    await ShowToastAsync("请先登录账户", "Messages");
                    return;
                }

                var (accountId, account) = selected.Value;

                // ★ 微软账户：临近过期 / token 缺失 → 静默刷新
                if (account.Type == "microsoft")
                {
                    // 提前 5 分钟视为过期，避免游戏启动过程中 token 恰好失效
                    bool needRefresh =
                        account.TokenExpireAt <= DateTime.UtcNow.AddMinutes(5)
                        || string.IsNullOrEmpty(account.MinecraftAccessToken);

                    if (needRefresh)
                    {
                        Log(
                            $"正版凭证需要刷新（过期时间 {account.TokenExpireAt:yyyy-MM-dd HH:mm:ss}，当前 {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}）",
                            $"Microsoft token requires refresh (expires {account.TokenExpireAt:yyyy-MM-dd HH:mm:ss}, now {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss})");

                        var refreshed = await RefreshMicrosoftAccountAsync(account);
                        if (refreshed == null)
                        {
                            Log("正版凭证刷新失败，拒绝启动", "Microsoft token refresh failed, abort launch");
                            await ShowToastAsync("正版凭证已失效，请重新登录", "Errors");
                            return;
                        }

                        // 持久化 + 更新静态字段（头像等依赖 App.PlayerName 的地方保持同步）
                        AccountService.UpdateMicrosoftTokens(refreshed);
                        App.PlayerName = refreshed.Username;
                        App.OfflineUuid = refreshed.Uuid;

                        account = refreshed;
                        accountId = $"microsoft_{refreshed.Uuid}";
                        Log(
                            $"正版凭证刷新成功，新过期时间 {account.TokenExpireAt:yyyy-MM-dd HH:mm:ss}",
                            $"Microsoft token refreshed, new expiry {account.TokenExpireAt:yyyy-MM-dd HH:mm:ss}");
                    }
                }

                // ★ 构造 LaunchAuthInfo
                LaunchAuthInfo auth = account.Type == "microsoft"
                    ? LaunchAuthInfo.ForMicrosoft(account.Username, account.Uuid, account.MinecraftAccessToken)
                    : LaunchAuthInfo.ForOffline(account.Username, account.Uuid);

                Log(
                    $"以 {auth.UserType} 身份启动：{auth.PlayerName} ({auth.Uuid})",
                    $"Launching as {auth.UserType}: {auth.PlayerName} ({auth.Uuid})");

                await ShowToastAsync("正在启动游戏...", "Messages");

                bool ok = await GameLauncher.LaunchAsync(_currentInstance, vj, matched, auth);

                if (ok) await ShowToastAsync("游戏已启动", "Messages");
                else await ShowToastAsync("启动失败，查看日志", "Messages");
            }
            catch (Exception ex)
            {
                Log($"启动游戏异常: {ex.Message}", $"Start game exception: {ex.Message}");
                await ShowToastAsync("启动失败", "Messages");
            }
        }

        /// <summary>
        /// 用 refresh_token 静默刷新微软账户的 Minecraft access_token。
        /// 成功返回新 AccountInfo；失败返回 null（调用方应提示重新登录）。
        /// </summary>
        private async Task<AccountInfo?> RefreshMicrosoftAccountAsync(AccountInfo account)
        {
            if (string.IsNullOrEmpty(account.MicrosoftRefreshToken))
            {
                Log("refresh_token 为空，无法静默刷新", "refresh_token is empty, cannot silent refresh");
                return null;
            }

            try
            {
                var flow = new MicrosoftAuthFlow();
                var refreshed = await flow.RefreshAsync(account.MicrosoftRefreshToken);
                return refreshed;
            }
            catch (Exception ex)
            {
                Log($"静默刷新异常: {ex.Message}", $"Silent refresh exception: {ex.Message}");
                return null;
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
                string? path = await PickFolderSafeAsync("选择 Minecraft 目录（.minecraft 根目录或隔离实例目录）");
                if (string.IsNullOrEmpty(path))
                {
                    Log("用户取消导入", "User cancelled import");
                    return;
                }

                var result = MinecraftDirectoryAnalyzer.Analyze(path);
                if (!result.IsValid)
                {
                    Log("目录无效（未找到 versions/ 或 .minecraft/versions/）", "Invalid directory (no versions/ or .minecraft/versions/)");
                    await ShowToastAsync("未找到有效的 Minecraft 目录", "Messages");
                    return;
                }

                Log($"分析结果: Layout={result.Layout}, Versions={string.Join(",", result.Versions)}", $"Analysis: Layout={result.Layout}, Versions={string.Join(",", result.Versions)}");

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

        private async void OnSwitchAccountClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            await ShowAccountManagerAsync();
        }

        private async Task ShowAccountManagerAsync()
        {
            UpdateTitleBarGradient(_defaultStartColor, _defaultEndColor);
            var contentArea = this.FindControl<Grid>("ContentArea");
            if (contentArea != null) contentArea.IsVisible = false;

            await Task.Delay(150);

            try
            {
                var win = new AccountManagerWindow();
                await win.ShowDialog(this);
                if (win.ShouldOpenLogin) await RunLoginWindowAsync();

                UpdateLoginStatus();
                await RefreshAvatarAsync();
            }
            finally
            {
                if (contentArea != null) contentArea.IsVisible = true;
                await SetTransparentTitleBarAsync();
            }
        }

        private async Task RunLoginWindowAsync()
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

            if (!App.IsLoggedIn) _ = ShowCancelToastAsync();
        }

        private async Task ShowLoginDialog()
        {
            var contentArea = this.FindControl<Grid>("ContentArea");
            if (contentArea != null) contentArea.IsVisible = false;

            await Task.Delay(150);

            try
            {
                await RunLoginWindowAsync();
            }
            finally
            {
                if (contentArea != null) contentArea.IsVisible = true;
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
                    var uri = new Uri("avares://Project.Launch/MainSrc/Views/imgs/Main/images/Buttons/black-.png");
                    using var stream = AssetLoader.Open(uri);
                    minImg.Source = new Bitmap(stream);
                }
                if (closeImg != null)
                {
                    var uri = new Uri("avares://Project.Launch/MainSrc/Views/imgs/Main/images/Buttons/blackx.png");
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
                    using var stream = AssetLoader.Open(new Uri($"avares://Project.Launch/MainSrc/Views/imgs/Main/images/Buttons/{mainIcon}"));
                    launchImg.Source = new Bitmap(stream);
                }
                if (launchTxt != null) launchTxt.Text = hasInstance ? "开始游戏" : "导入实例";

                if (expandImg != null)
                {
                    using var stream = AssetLoader.Open(new Uri($"avares://Project.Launch/MainSrc/Views/imgs/Main/images/Buttons/{subIcon}"));
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
            if (!App.IsLoggedIn) { Log("未登录，跳过皮肤加载", "Not logged in, skip skin loading"); return; }

            var account = AccountService.GetSelectedAccount();
            Bitmap? skinBitmap = null;

            if (account != null && account.Type == "microsoft" && !string.IsNullOrEmpty(account.SkinUrl))
            {
                try
                {
                    Log($"正在下载正版皮肤: {account.SkinUrl}", $"Downloading official skin: {account.SkinUrl}");
                    byte[] bytes = await _httpClient.GetByteArrayAsync(account.SkinUrl);
                    using var ms = new MemoryStream(bytes);
                    skinBitmap = new Bitmap(ms);
                    Log("正版皮肤下载成功", "Official skin downloaded successfully");
                }
                catch (Exception ex)
                {
                    Log($"正版皮肤下载失败，将回退到默认皮肤: {ex.Message}", $"Skin download failed, falling back to default: {ex.Message}");
                }
            }

            if (skinBitmap == null)
            {
                string skinAssetPath = DefaultSkinSelector.GetSkinAssetPath(account?.Uuid ?? App.OfflineUuid);
                Log($"使用默认皮肤: {skinAssetPath}", $"Using default skin: {skinAssetPath}");
                try
                {
                    using var stream = AssetLoader.Open(new Uri(skinAssetPath));
                    skinBitmap = new Bitmap(stream);
                }
                catch (Exception ex)
                {
                    Log($"加载默认皮肤失败: {ex.Message}", $"Failed to load default skin: {ex.Message}");
                }
            }

            if (skinBitmap == null) return;

            try
            {
                _fullSkinBitmap?.Dispose();
                _fullSkinBitmap = skinBitmap;
                var skinViewer = this.FindControl<Pivot3DControl>("SkinViewer");
                if (skinViewer != null) skinViewer.Skin = _fullSkinBitmap;
                Log("3D 皮肤视图已更新", "3D skin view updated");
            }
            catch (Exception ex)
            {
                Log($"更新 3D 皮肤视图失败: {ex.Message}", $"Failed to update 3D skin view: {ex.Message}");
            }

            var faceImg = this.FindControl<Image>("HomeFaceImage");
            var hatImg = this.FindControl<Image>("HomeHatImage");
            if (faceImg != null && hatImg != null)
            {
                try
                {
                    if (_fullSkinBitmap != null)
                    {
                        var (face, hat) = CropSkinFromBitmap(_fullSkinBitmap);
                        if (face != null && hat != null)
                        {
                            faceImg.Source = face;
                            hatImg.Source = hat;
                            RenderOptions.SetBitmapInterpolationMode(faceImg, BitmapInterpolationMode.None);
                            RenderOptions.SetBitmapInterpolationMode(hatImg, BitmapInterpolationMode.None);
                            Log("2D 头像已更新", "2D avatar updated");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log($"更新 2D 头像失败: {ex.Message}", $"Failed to update 2D avatar: {ex.Message}");
                }
            }
        }

        private void Log(string zh, string en) => LogHelper.Write(LauncherPaths.MainWindowLog, zh, en);

        // ============================================================
        // 目录选择（不能崩）
        // ============================================================

        /// <summary>上一次成功选中的目录，手动输入时作为默认值。</summary>
        private string? _lastPickPath;

        /// <summary>
        /// 安全地选目录：
        ///   1) Avalonia 的 Win32 选择器在「选中项已失效 / 非本地路径」时会直接抛
        ///      ArgumentException("Directory must exist")，必须兜住；
        ///   2) 即便选择器没抛，也要自己校验路径真实存在；
        ///   3) 选择器不可用时退化成手动输入路径，保证用户仍能继续。
        /// </summary>
        private async Task<string?> PickFolderSafeAsync(string title)
        {
            try
            {
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel == null) return null;

                var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    Title = title,
                    AllowMultiple = false
                });

                if (folders == null || folders.Count < 1)
                {
                    Log("用户取消目录选择", "User cancelled folder picking");
                    return null;
                }

                string? path = null;
                try
                {
                    path = folders[0].TryGetLocalPath();
                }
                catch (Exception ex)
                {
                    Log($"取本地路径失败: {ex.Message}", $"TryGetLocalPath failed: {ex.Message}");
                }

                path = path?.Trim().TrimEnd('\\', '/');

                if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
                {
                    Log($"所选目录不可用: {path}", $"Picked folder unavailable: {path}");
                    await ShowToastAsync("所选目录不可用，请重新选择", "Errors");
                    return await PromptForLocalPathAsync(title);
                }

                Log($"用户选择目录: {path}", $"User selected directory: {path}");
                _lastPickPath = path;
                return path;
            }
            catch (Exception ex)
            {
                // 这里曾经直接把整个启动器带崩（async void + Avalonia 选择器内部异常）
                Log($"文件夹选择器异常: {ex.Message}", $"Folder picker failed: {ex.Message}");
                LogHelper.Write(LauncherPaths.AppLog,
                    $"文件夹选择器异常: {ex}",
                    $"Folder picker exception: {ex}");

                await ShowToastAsync("无法打开文件夹选择器，改为手动输入", "Errors");
                return await PromptForLocalPathAsync(title);
            }
        }

        /// <summary>选择器不可用时的兜底：手动输入目录。</summary>
        private async Task<string?> PromptForLocalPathAsync(string title)
        {
            var window = new Window
            {
                Title = title,
                Width = 580,
                Height = 220,
                CanResize = false,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = Brushes.White
            };

            var inputBox = new TextBox
            {
                Text = _lastPickPath ?? string.Empty,
                PlaceholderText = @"例如 E:\Minecraft",
                FontSize = 15
            };

            string? result = null;

            var okButton = new Button
            {
                Content = "确定",
                Width = 110,
                Height = 36,
                Classes = { "Miret" }
            };
            var cancelButton = new Button
            {
                Content = "取消",
                Width = 110,
                Height = 36,
                Classes = { "Miret" }
            };

            okButton.Click += (_, _) => { result = inputBox.Text; window.Close(); };
            cancelButton.Click += (_, _) => { result = null; window.Close(); };

            var buttonRow = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                Spacing = 12
            };
            buttonRow.Children.Add(cancelButton);
            buttonRow.Children.Add(okButton);

            var panel = new StackPanel { Margin = new Thickness(20), Spacing = 14 };
            panel.Children.Add(new TextBlock
            {
                Text = "请输入 Minecraft 安装目录（.minecraft 文件夹）：",
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.Parse("#666666"))
            });
            panel.Children.Add(inputBox);
            panel.Children.Add(buttonRow);

            window.Content = panel;
            await window.ShowDialog(this);

            string? path = result?.Trim().Trim('"').TrimEnd('\\', '/');
            if (string.IsNullOrWhiteSpace(path)) return null;

            if (!Directory.Exists(path))
            {
                await ShowToastAsync("目录不存在，请重新选择", "Errors");
                return null;
            }

            Log($"用户手动输入目录: {path}", $"User entered directory: {path}");
            _lastPickPath = path;
            return path;
        }

        private async Task ShowToastAsync(string message, string sound = "Messages")
        {
            SoundService.Play(sound);
            var toast = this.FindControl<Border>("CancelToast");
            var toastText = this.FindControl<TextBlock>("ToastText");
            if (toast == null) return;

            if (toastText != null) toastText.Text = message;

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

        private (Bitmap? face, Bitmap? hat) CropSkinFromBitmap(Bitmap bitmap)
        {
            try
            {
                int width = bitmap.PixelSize.Width;
                int height = bitmap.PixelSize.Height;

                int scale = width / 64;
                if (scale < 1) return (null, null);

                int faceX = 8 * scale;
                int faceY = 8 * scale;
                int faceSize = 8 * scale;
                int hatX = 40 * scale;
                int hatY = 8 * scale;

                var faceCropped = new CroppedBitmap(bitmap, new PixelRect(faceX, faceY, faceSize, faceSize));
                var hatCropped = new CroppedBitmap(bitmap, new PixelRect(hatX, hatY, faceSize, faceSize));

                var faceBmp = new RenderTargetBitmap(new PixelSize(faceSize, faceSize), new Vector(96, 96));
                using (var ctx = faceBmp.CreateDrawingContext()) ctx.DrawImage(faceCropped, new Rect(0, 0, faceSize, faceSize));

                var hatBmp = new RenderTargetBitmap(new PixelSize(faceSize, faceSize), new Vector(96, 96));
                using (var ctx = hatBmp.CreateDrawingContext()) ctx.DrawImage(hatCropped, new Rect(0, 0, faceSize, faceSize));

                return (faceBmp, hatBmp);
            }
            catch { return (null, null); }
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
            if (titleText != null) titleText.Foreground = useDark ? new SolidColorBrush(Colors.Black) : new SolidColorBrush(Colors.White);

            var minImg = this.FindControl<Image>("MinimizeImage");
            if (minImg != null)
            {
                var path = useDark ? "avares://Project.Launch/MainSrc/Views/imgs/Main/images/Buttons/black-.png" : "avares://Project.Launch/MainSrc/Views/imgs/Main/images/Buttons/white-.png";
                try { var uri = new Uri(path); using var stream = AssetLoader.Open(uri); minImg.Source = new Bitmap(stream); } catch { }
            }

            var closeImg = this.FindControl<Image>("CloseImage");
            if (closeImg != null)
            {
                var path = useDark ? "avares://Project.Launch/MainSrc/Views/imgs/Main/images/Buttons/blackx.png" : "avares://Project.Launch/MainSrc/Views/imgs/Main/images/Buttons/whitex.png";
                try { var uri = new Uri(path); using var stream = AssetLoader.Open(uri); closeImg.Source = new Bitmap(stream); } catch { }
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
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) BeginMoveDrag(e);
        }

        public void SetTitleBarGradient(Color start, Color end)
        {
            UpdateTitleBarGradient(start, end);
        }
    }
}