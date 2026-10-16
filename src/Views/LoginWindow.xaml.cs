using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using LiquidGlassAvaloniaUI;
using Project.Launch.Tools;
using Project.Launch.Tools.Auth;

namespace Project.Launch.Views
{
    public partial class LoginWindow : Window
    {
        private readonly HttpClient _httpClient = new HttpClient();

        public string PlayerName { get; private set; } = "Steve";
        public Bitmap? FaceBitmap { get; private set; }
        public Bitmap? HatBitmap { get; private set; }
        public string? OfflineUuid { get; private set; }

        // 顶部三按钮
        private Button? _offlineBtn;
        private Button? _onlineBtn;
        private Button? _thirdPartyBtn;
        private LiquidGlassSurface? _slider;
        private TranslateTransform? _sliderTransform;
        private ScaleTransform? _sliderScale;
        private int _selectedIndex = 1;

        // 标题 / 面板
        private Border? _titleContainer;
        private TranslateTransform? _titleTransform;
        private Border? _inputPanel;

        // 离线面板控件
        private TextBox? _playerNameBox;
        private Button? _loginButton;
        private TextBlock? _loginStatus;

        // 三个面板
        private StackPanel? _offlinePanel;
        private Grid? _microsoftPanel;
        private StackPanel? _thirdPartyPanel;

        // 正版：三种状态
        private StackPanel? _msIdlePanel;
        private StackPanel? _msWaitingPanel;
        private StackPanel? _msProgressPanel;
        private TextBlock? _msVerificationUri;
        private TextBlock? _msUserCode;
        private TextBlock? _msStatus;
        private TextBlock? _msProgressText;

        // 正版登录流程状态
        private MicrosoftAuthFlow? _msFlow;
        private CancellationTokenSource? _msCts;
        private string _msCurrentUserCode = "";
        private string _msCurrentVerificationUri = "";

        // 滑块动画
        private DispatcherTimer? _animationTimer;
        private double _animationStartX;
        private double _animationTargetX;
        private DateTime _animationStartTime;

        private const double OFFSET_OFFLINE = 0;
        private const double OFFSET_ONLINE = -10;
        private const double OFFSET_THIRD = -60;

        public LoginWindow()
        {
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            InitializeComponent();

            InitializeLoginModeSelector();
            InitializeInputControls();

            // 离线：回车登录
            if (_playerNameBox != null)
                _playerNameBox.KeyDown += (s, e) => { if (e.Key == Key.Enter) OnLoginClicked(s, e); };

            var gitHubButton = this.FindControl<Button>("GitHubButton");
            if (gitHubButton != null)
                gitHubButton.Click += OnGitHubClicked;

            _titleContainer = this.FindControl<Border>("TitleContainer");
            if (_titleContainer != null)
            {
                _titleTransform = _titleContainer.RenderTransform as TranslateTransform;
                if (_titleTransform == null)
                {
                    _titleTransform = new TranslateTransform();
                    _titleContainer.RenderTransform = _titleTransform;
                }
                _titleTransform.Y = OFFSET_ONLINE;
            }

            _inputPanel = this.FindControl<Border>("InputPanel");

            UpdateModeUI(1);
            SetDefaultSteveSkin();

            Opened += (s, e) => SoundService.Play("Open");
        }

        private void InitializeInputControls()
        {
            _playerNameBox = this.FindControl<TextBox>("PlayerNameBox");
            _loginButton = this.FindControl<Button>("LoginButton");
            _loginStatus = this.FindControl<TextBlock>("LoginStatus");

            _offlinePanel = this.FindControl<StackPanel>("OfflinePanel");
            _microsoftPanel = this.FindControl<Grid>("MicrosoftPanel");
            _thirdPartyPanel = this.FindControl<StackPanel>("ThirdPartyPanel");

            _msIdlePanel = this.FindControl<StackPanel>("MsIdlePanel");
            _msWaitingPanel = this.FindControl<StackPanel>("MsWaitingPanel");
            _msProgressPanel = this.FindControl<StackPanel>("MsProgressPanel");
            _msVerificationUri = this.FindControl<TextBlock>("MsVerificationUri");
            _msUserCode = this.FindControl<TextBlock>("MsUserCode");
            _msStatus = this.FindControl<TextBlock>("MsStatus");
            _msProgressText = this.FindControl<TextBlock>("MsProgressText");
        }

        private void InitializeLoginModeSelector()
        {
            _offlineBtn = this.FindControl<Button>("OfflineLoginButton");
            _onlineBtn = this.FindControl<Button>("OnlineLoginButton");
            _thirdPartyBtn = this.FindControl<Button>("ThirdPartyLoginButton");

            _slider = this.FindControl<LiquidGlassSurface>("LoginModeSlider");
            if (_slider != null)
            {
                var group = _slider.RenderTransform as TransformGroup;
                if (group != null)
                {
                    _sliderTransform = group.Children.OfType<TranslateTransform>().FirstOrDefault();
                    _sliderScale = group.Children.OfType<ScaleTransform>().FirstOrDefault();
                }

                if (_sliderTransform != null)
                {
                    _sliderTransform.X = 120;
                    _sliderTransform.Y = 0;
                }

                if (_sliderScale != null)
                {
                    _sliderScale.ScaleX = 1;
                    _sliderScale.ScaleY = 1;
                }
            }

            if (_offlineBtn != null) _offlineBtn.Click += (s, e) => SelectMode(0);
            if (_onlineBtn != null) _onlineBtn.Click += (s, e) => SelectMode(1);
            if (_thirdPartyBtn != null) _thirdPartyBtn.Click += (s, e) => SelectMode(2);

            UpdateButtonColors(1);
        }

        private void SelectMode(int index)
        {
            if (index == _selectedIndex) return;
            _selectedIndex = index;
            AnimateSliderTo(index);
            UpdateModeUI(index);
        }

        private void UpdateModeUI(int index)
        {
            if (_inputPanel != null)
                _inputPanel.Opacity = 0;

            Dispatcher.UIThread.InvokeAsync(async () =>
            {
                await Task.Delay(AnimationConfig.InputPanelFadeDuration);

                if (_offlinePanel != null) _offlinePanel.IsVisible = (index == 0);
                if (_microsoftPanel != null) _microsoftPanel.IsVisible = (index == 1);
                if (_thirdPartyPanel != null) _thirdPartyPanel.IsVisible = (index == 2);

                if (_inputPanel != null)
                    _inputPanel.Opacity = 1;
            });
        }

        // ============================================================
        // 滑块动画
        // ============================================================
        private void AnimateSliderTo(int index)
        {
            if (_sliderTransform == null || _slider == null) return;

            double targetX = index * 120;
            double startX = _sliderTransform.X;

            _animationTimer?.Stop();

            _animationStartX = startX;
            _animationTargetX = targetX;
            _animationStartTime = DateTime.Now;

            TimeSpan duration = AnimationConfig.SliderMoveDuration;
            double peakScale = AnimationConfig.SliderPeakScale;
            double riseEnd = AnimationConfig.SliderScaleRiseEnd;
            double fallStart = AnimationConfig.SliderScaleFallStart;
            double blurPeak = AnimationConfig.SliderBlurPeak;
            double halfW = AnimationConfig.SliderWidth / 2.0;
            double halfH = AnimationConfig.SliderHeight / 2.0;

            _animationTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16)
            };
            _animationTimer.Tick += (s, e) =>
            {
                var elapsed = DateTime.Now - _animationStartTime;
                double progress = elapsed.TotalMilliseconds / duration.TotalMilliseconds;
                if (progress >= 1.0)
                {
                    progress = 1.0;
                    _animationTimer.Stop();
                }

                double eased = progress < 0.5
                    ? 4 * progress * progress * progress
                    : 1 - Math.Pow(-2 * progress + 2, 3) / 2;

                double baseX = _animationStartX + (_animationTargetX - _animationStartX) * eased;

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

                // ★ 还原：模糊驱动（三角曲线，与 MainWindow 的滑块动画保持一致）
                double blurCurve = 1.0 - Math.Abs(progress * 2.0 - 1.0);
                _slider.BlurRadius = blurPeak * blurCurve;

                double xCompensation = -(currentScale - 1.0) * halfW;
                double yCompensation = -(currentScale - 1.0) * halfH;

                _sliderTransform.X = baseX + xCompensation;
                _sliderTransform.Y = yCompensation;

                if (_sliderScale != null)
                {
                    _sliderScale.ScaleX = currentScale;
                    _sliderScale.ScaleY = currentScale;
                }

                if (progress >= 1.0)
                {
                    _sliderTransform.X = _animationTargetX;
                    _sliderTransform.Y = 0;
                    if (_sliderScale != null)
                    {
                        _sliderScale.ScaleX = 1.0;
                        _sliderScale.ScaleY = 1.0;
                    }
                    _slider.BlurRadius = 0;
                }
            };

            _animationTimer.Start();

            UpdateButtonColors(index);
        }

        private void UpdateButtonColors(int selectedIndex)
        {
            var buttons = new[] { _offlineBtn, _onlineBtn, _thirdPartyBtn };
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] != null)
                {
                    buttons[i]!.Foreground = (i == selectedIndex)
                        ? new SolidColorBrush(Colors.Black)
                        : new SolidColorBrush(Color.Parse("#666666"));
                    buttons[i]!.IsHitTestVisible = (i != selectedIndex);
                }
            }
        }

        // ============================================================
        // 离线登录
        // ============================================================
        public void OnLoginClicked(object sender, RoutedEventArgs e)
        {
            if (_playerNameBox == null || string.IsNullOrWhiteSpace(_playerNameBox.Text))
            {
                if (_loginStatus != null) _loginStatus.Text = "❌ 请输入用户名";
                return;
            }

            string username = _playerNameBox.Text.Trim();
            try
            {
                string uuidStr = OfflineUuidGenerator.GenerateUuidString(username).Replace("-", "");

                App.PlayerName = username;
                App.OfflineUuid = uuidStr;
                App.IsLoggedIn = true;
                App.LoginCheck = true;

                var account = new AccountInfo
                {
                    Type = "offline",
                    Username = username,
                    Uuid = uuidStr,
                    LastUsed = DateTime.UtcNow
                };
                string accountId = $"offline_{username}";
                AccountService.SetSelectedAccount(accountId, account);
                AccountService.SetLoginCheck(true);

                Close(true);
            }
            catch (Exception ex)
            {
                if (_loginStatus != null)
                    _loginStatus.Text = $"❌ 保存账户失败: {ex.Message}";
            }
        }

        // ============================================================
        // 正版登录：点击"开始登录"
        // ============================================================
        private async void OnMsLoginClicked(object? sender, RoutedEventArgs e)
        {
            if (_msIdlePanel != null) _msIdlePanel.IsVisible = false;
            if (_msWaitingPanel != null) _msWaitingPanel.IsVisible = true;
            if (_msProgressPanel != null) _msProgressPanel.IsVisible = false;

            _msCts = new CancellationTokenSource();
            _msFlow = new MicrosoftAuthFlow();

            _msFlow.DeviceCodeReady += (userCode, verificationUri) =>
            {
                _msCurrentUserCode = userCode;
                _msCurrentVerificationUri = verificationUri;

                Dispatcher.UIThread.Post(() =>
                {
                    if (_msUserCode != null) _msUserCode.Text = userCode;
                    if (_msVerificationUri != null) _msVerificationUri.Text = verificationUri;

                    try
                    {
                        string url = verificationUri + "?otc=" + Uri.EscapeDataString(userCode);
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = url,
                            UseShellExecute = true
                        });
                    }
                    catch { }
                });
            };

            _msFlow.Progress += msg =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (_msStatus != null) _msStatus.Text = msg;
                });
            };

            var account = await _msFlow.LoginAsync(_msCts.Token);

            if (account == null)
            {
                if (_msWaitingPanel != null) _msWaitingPanel.IsVisible = false;
                if (_msIdlePanel != null) _msIdlePanel.IsVisible = true;
                return;
            }

            try
            {
                string accountId = $"microsoft_{account.Uuid}";

                App.PlayerName = account.Username;
                App.OfflineUuid = account.Uuid;
                App.IsLoggedIn = true;
                App.LoginCheck = true;

                AccountService.SetSelectedAccount(accountId, account);
                AccountService.SetLoginCheck(true);

                LogHelper.Write(LauncherPaths.AppLog,
                    $"正版账户已保存: {account.Username}",
                    $"Microsoft account saved: {account.Username}");

                Close(true);
            }
            catch (Exception ex)
            {
                LogHelper.Write(LauncherPaths.AppLog,
                    $"保存正版账户失败: {ex.Message}",
                    $"Save Microsoft account failed: {ex.Message}");
            }
        }

        // ============================================================
        // 正版登录：复制代码（★ 已改成扩展方法 SetTextAsync）
        // ============================================================
        private async void OnMsCopyCodeClicked(object? sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_msCurrentUserCode)) return;
            try
            {
                var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
                if (clipboard != null)
                {
                    await clipboard.SetTextAsync(_msCurrentUserCode);
                }

                if (_msStatus != null)
                    _msStatus.Text = "已复制代码";
            }
            catch { }
        }

        // ============================================================
        // 正版登录：手动打开浏览器
        // ============================================================
        private void OnMsOpenBrowserClicked(object? sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_msCurrentVerificationUri)) return;
            try
            {
                string url = _msCurrentVerificationUri;
                if (!string.IsNullOrEmpty(_msCurrentUserCode))
                    url += "?otc=" + Uri.EscapeDataString(_msCurrentUserCode);

                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch { }
        }

        // ============================================================
        // 正版登录：取消
        // ============================================================
        private void OnMsCancelClicked(object? sender, RoutedEventArgs e)
        {
            _msCts?.Cancel();

            if (_msWaitingPanel != null) _msWaitingPanel.IsVisible = false;
            if (_msProgressPanel != null) _msProgressPanel.IsVisible = false;
            if (_msIdlePanel != null) _msIdlePanel.IsVisible = true;
        }

        // ============================================================
        // GitHub
        // ============================================================
        private async void OnGitHubClicked(object? sender, EventArgs e)
        {
            try
            {
                var uri = new Uri("https://github.com/Zhouxuanyao2333/KilimMinecraftLauncher");
                var launcher = TopLevel.GetTopLevel(this)?.Launcher;
                if (launcher != null)
                    await launcher.LaunchUriAsync(uri);
                else
                {
                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = uri.ToString(),
                        UseShellExecute = true
                    };
                    Process.Start(psi);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"打开链接失败: {ex.Message}");
                if (_loginStatus != null)
                    _loginStatus.Text = "❌ 打开链接失败，请手动访问仓库";
            }
        }

        // ============================================================
        // 默认 Steve 皮肤（离线登录用）
        // ============================================================
        private void SetDefaultSteveSkin()
        {
            try
            {
                var stevePath = "avares://Project.Launch/src/Views/imgs/skin/Steve.png";
                var uri = new Uri(stevePath);
                using var stream = AssetLoader.Open(uri);

                var result = CropSkinAsync(stream).GetAwaiter().GetResult();

                if (result.face != null && result.hat != null)
                {
                    FaceBitmap = result.face;
                    HatBitmap = result.hat;
                    PlayerName = "Steve";
                }
            }
            catch (Exception ex)
            {
                LogHelper.Write(LauncherPaths.AppLog,
                    $"加载默认皮肤失败: {ex.Message}",
                    $"Failed to load default skin: {ex.Message}");
            }
        }

        private async Task<(Bitmap? face, Bitmap? hat)> CropSkinAsync(Stream stream)
        {
            try
            {
                byte[] imageData;
                using (var ms = new MemoryStream())
                {
                    await stream.CopyToAsync(ms).ConfigureAwait(false);
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
                }).ConfigureAwait(false);
            }
            catch
            {
                return (null, null);
            }
        }

        private void Close(bool isLogin) => Close();
    }
}