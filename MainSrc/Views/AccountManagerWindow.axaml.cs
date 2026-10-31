using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Project.Launch.Tools;

using Project.Launch;
using Project.Launch.Auth;
using Project.Launch.Tools.JavaConfig;
using Project.Launch.Tools.LogsConfig;
namespace Project.Launch.Views
{
    public partial class AccountManagerWindow : Window
    {
        /// <summary>用户是否点了"添加账户"（外部据此决定是否打开 LoginWindow）</summary>
        public bool ShouldOpenLogin { get; private set; } = false;

        public AccountManagerWindow()
        {
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            InitializeComponent();

            RebuildAccountList();
        }

        // ============================================================
        // 重建账户列表
        // ============================================================
        private void RebuildAccountList()
        {
            var listPanel = this.FindControl<StackPanel>("AccountListPanel");
            var emptyAddBtn = this.FindControl<Button>("EmptyAddButton");
            var listContainer = this.FindControl<Grid>("ListContainer");

            if (listPanel == null || emptyAddBtn == null || listContainer == null) return;

            listPanel.Children.Clear();

            var root = AccountService.LoadAccounts();
            var accounts = root.Accounts
                .OrderByDescending(a => a.Value.LastUsed)
                .ToList();

            if (accounts.Count == 0)
            {
                emptyAddBtn.IsVisible = true;
                listContainer.IsVisible = false;
                return;
            }

            emptyAddBtn.IsVisible = false;
            listContainer.IsVisible = true;

            foreach (var kv in accounts)
            {
                bool isSelected = kv.Key == root.SelectedAccount;
                var item = CreateAccountItem(kv.Key, kv.Value, isSelected);
                listPanel.Children.Add(item);
            }
        }

        // ============================================================
        // 创建单个账户项（方形头像，无边框）
        // ============================================================
        private Control CreateAccountItem(string id, AccountInfo info, bool isSelected)
        {
            var button = new Button
            {
                Background = isSelected
                    ? new SolidColorBrush(Color.Parse("#E0D8F0"))
                    : new SolidColorBrush(Color.Parse("#F0F0F0"))
            };
            button.Classes.Add("AccountItem");

            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto,Auto"),
                VerticalAlignment = VerticalAlignment.Center
            };

            // ★ 方形头像（40×40，无边框，用裁切好的 face）
            var avatar = new Image
            {
                Width = 40,
                Height = 40,
                Stretch = Stretch.Uniform,
                VerticalAlignment = VerticalAlignment.Center
            };

            // ★ 用附加属性设置 Nearest 采样
            RenderOptions.SetBitmapInterpolationMode(avatar, BitmapInterpolationMode.None);

            try
            {
                string skinAsset = DefaultSkinSelector.GetSkinAssetPath(info.Uuid);
                var face = SkinCropper.CropFaceFromAsset(skinAsset);
                if (face != null)
                    avatar.Source = face;
            }
            catch { }

            Grid.SetColumn(avatar, 0);
            grid.Children.Add(avatar);

            // 用户名
            var nameText = new TextBlock
            {
                Text = info.Username,
                FontSize = 15,
                FontWeight = FontWeight.SemiBold,
                Foreground = new SolidColorBrush(Color.Parse("#333")),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 0, 0)
            };
            Grid.SetColumn(nameText, 1);
            grid.Children.Add(nameText);

            // 类型标签
            var typeText = new TextBlock
            {
                Text = info.Type == "microsoft" ? "正版" : "离线",
                FontSize = 11,
                Foreground = new SolidColorBrush(
                    info.Type == "microsoft" ? Color.Parse("#4C8F4C") : Color.Parse("#888888")),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 10, 0)
            };
            Grid.SetColumn(typeText, 2);
            grid.Children.Add(typeText);

            // UUID 简写
            string uuidShort = string.IsNullOrEmpty(info.Uuid)
                ? "-------"
                : info.Uuid.Substring(0, Math.Min(8, info.Uuid.Length));
            var uuidText = new TextBlock
            {
                Text = uuidShort,
                FontSize = 11,
                FontFamily = new FontFamily("Consolas,monospace"),
                Foreground = new SolidColorBrush(Color.Parse("#AAA")),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0)
            };
            Grid.SetColumn(uuidText, 3);
            grid.Children.Add(uuidText);

            // 删除按钮
            var deleteBtn = new Button { Content = "✕" };
            deleteBtn.Classes.Add("DeleteAccount");
            deleteBtn.Click += (s, e) =>
            {
                e.Handled = true;
                _ = OnDeleteAccountAsync(id, info.Username);
            };
            Grid.SetColumn(deleteBtn, 4);
            grid.Children.Add(deleteBtn);

            button.Content = grid;
            button.Click += (s, e) => OnSelectAccount(id, info);

            return button;
        }

        // ============================================================
        // 点击账户 → 切换
        // ============================================================
        private void OnSelectAccount(string id, AccountInfo info)
        {
            try
            {
                App.PlayerName = info.Username;
                App.OfflineUuid = info.Uuid;
                App.IsLoggedIn = true;
                App.LoginCheck = true;

                var root = AccountService.LoadAccounts();
                root.SelectedAccount = id;
                AccountService.SaveAccounts(root);

                Close();
            }
            catch (Exception ex)
            {
                LogHelper.Write(LauncherPaths.AppLog,
                    $"切换账户失败: {ex.Message}",
                    $"Switch account failed: {ex.Message}");
            }
        }

        // ============================================================
        // 删除账户（含确认框）
        // ============================================================
        private async Task OnDeleteAccountAsync(string id, string username)
        {
            bool confirmed = await ShowConfirmDialogAsync(
                $"确认删除账户「{username}」？",
                "删除",
                "取消");
            if (!confirmed) return;

            try
            {
                AccountService.RemoveAccount(id);

                var root = AccountService.LoadAccounts();
                if (string.IsNullOrEmpty(root.SelectedAccount))
                {
                    App.IsLoggedIn = false;
                    App.PlayerName = null;
                    App.OfflineUuid = null;
                }

                RebuildAccountList();
            }
            catch (Exception ex)
            {
                LogHelper.Write(LauncherPaths.AppLog,
                    $"删除账户失败: {ex.Message}",
                    $"Delete account failed: {ex.Message}");
            }
        }

        // ============================================================
        // 确认对话框
        // ============================================================
        private async Task<bool> ShowConfirmDialogAsync(string message, string yesText, string noText)
        {
            var dialog = new Window
            {
                Title = "确认",
                Width = 360,
                Height = 180,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false,
                Background = new SolidColorBrush(Colors.White),
                WindowDecorations = WindowDecorations.None,
                TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent },
                ShowInTaskbar = false
            };

            var border = new Border
            {
                CornerRadius = new CornerRadius(12),
                Background = new SolidColorBrush(Colors.White),
                BorderBrush = new SolidColorBrush(Color.Parse("#20000000")),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(24)
            };

            var panel = new StackPanel { Spacing = 20 };

            panel.Children.Add(new TextBlock
            {
                Text = message,
                FontSize = 14,
                Foreground = new SolidColorBrush(Color.Parse("#333")),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center
            });

            var btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Spacing = 12
            };

            var yesBtn = new Button
            {
                Content = yesText,
                Padding = new Thickness(24, 8),
                Background = new SolidColorBrush(Color.Parse("#CC5555")),
                Foreground = new SolidColorBrush(Colors.White),
                FontWeight = FontWeight.SemiBold,
                CornerRadius = new CornerRadius(8),
                Cursor = new Cursor(StandardCursorType.Hand)
            };
            var noBtn = new Button
            {
                Content = noText,
                Padding = new Thickness(24, 8),
                Background = new SolidColorBrush(Color.Parse("#EEEEEE")),
                Foreground = new SolidColorBrush(Color.Parse("#333")),
                CornerRadius = new CornerRadius(8),
                Cursor = new Cursor(StandardCursorType.Hand)
            };

            yesBtn.Click += (s, e) => dialog.Close(true);
            noBtn.Click += (s, e) => dialog.Close(false);

            btnPanel.Children.Add(yesBtn);
            btnPanel.Children.Add(noBtn);
            panel.Children.Add(btnPanel);

            border.Child = panel;
            dialog.Content = border;

            return await dialog.ShowDialog<bool>(this);
        }

        // ============================================================
        // 添加账户
        // ============================================================
        private void OnAddAccountClicked(object? sender, RoutedEventArgs e)
        {
            ShouldOpenLogin = true;
            Close();
        }

        private void OnCloseClicked(object? sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}