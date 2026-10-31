using System;
using System.Threading;
using System.Threading.Tasks;

using Project.Launch.Tools.JavaConfig;
using Project.Launch.Tools.LogsConfig;
namespace Project.Launch.Auth
{
    /// <summary>
    /// 微软正版登录 / 静默刷新 全流程协调器。
    ///
    /// 登录：设备代码流 → XBL → XSTS → MC → profile
    /// 刷新：refresh_token → 新的 MS token → XBL → XSTS → MC → profile
    ///
    /// 两者共用后半段（XBL→XSTS→MC→profile），由 AuthenticateWithXboxChainAsync 统一实现。
    /// </summary>
    public class MicrosoftAuthFlow
    {
        /// <summary>登录过程中触发，参数是给用户看的提示</summary>
        public event Action<string>? Progress;

        /// <summary>拿到设备代码后触发（user_code + verification_uri）</summary>
        public event Action<string, string>? DeviceCodeReady;

        /// <summary>Xbox 链的结果（profile + MC token）</summary>
        private sealed class ChainResult
        {
            public required MinecraftAuthResponse McToken { get; init; }
            public required MinecraftProfile Profile { get; init; }
        }

        // ============================================================
        // 登录（设备代码流）
        // ============================================================
        public async Task<AccountInfo?> LoginAsync(CancellationToken ct = default)
        {
            try
            {
                // ============================================================
                // 阶段 1：申请设备代码
                // ============================================================
                Progress?.Invoke("正在申请设备代码...");

                var devSvc = new DeviceCodeAuthService();
                var deviceCode = await devSvc.RequestDeviceCodeAsync();
                if (deviceCode == null)
                {
                    Progress?.Invoke("❌ 无法连接微软服务器");
                    return null;
                }

                // 通知 UI 显示代码
                DeviceCodeReady?.Invoke(deviceCode.UserCode, deviceCode.VerificationUri);

                // ============================================================
                // 阶段 3：轮询等待授权
                // ============================================================
                Progress?.Invoke($"等待授权中（{deviceCode.ExpiresIn / 60} 分钟）...");

                var msToken = await devSvc.PollForTokenAsync(
                    deviceCode.DeviceCode,
                    deviceCode.Interval,
                    deviceCode.ExpiresIn,
                    ct);

                if (msToken == null)
                {
                    if (ct.IsCancellationRequested)
                        Progress?.Invoke("❌ 已取消");
                    else
                        Progress?.Invoke("❌ 授权超时或已拒绝");
                    return null;
                }

                // ============================================================
                // 阶段 4-7：XBL → XSTS → MC → profile
                // ============================================================
                var chain = await AuthenticateWithXboxChainAsync(msToken, Progress, ct);
                if (chain == null)
                    return null;

                // ============================================================
                // 构造 AccountInfo
                // ============================================================
                var account = BuildAccountInfo(msToken, chain);

                Progress?.Invoke($"✅ 登录成功：{account.Username}");
                LogHelper.Write(LauncherPaths.AppLog,
                    $"正版登录成功: {account.Username} ({account.Uuid})",
                    $"Microsoft login success: {account.Username} ({account.Uuid})");

                return account;
            }
            catch (OperationCanceledException)
            {
                Progress?.Invoke("❌ 已取消");
                return null;
            }
            catch (Exception ex)
            {
                LogHelper.Write(LauncherPaths.AppLog,
                    $"正版登录流程异常: {ex.Message}",
                    $"Microsoft auth flow exception: {ex.Message}");
                Progress?.Invoke($"❌ 登录异常: {ex.Message}");
                return null;
            }
        }

        // ============================================================
        // 静默刷新（refresh_token → 全链重走）
        //
        // 调用时机：启动游戏前检测到 MinecraftAccessToken 已过期或缺失。
        // 成功返回新的 AccountInfo（已含新 refresh_token / access_token / 过期时间 / 皮肤 URL），
        // 失败返回 null，调用方应提示用户重新登录。
        // ============================================================
        public async Task<AccountInfo?> RefreshAsync(
            string refreshToken,
            CancellationToken ct = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(refreshToken))
                {
                    LogHelper.Write(LauncherPaths.AppLog,
                        "静默刷新失败：refresh_token 为空",
                        "Silent refresh failed: refresh_token is empty");
                    return null;
                }

                Progress?.Invoke("正在刷新正版凭证...");

                var devSvc = new DeviceCodeAuthService();
                var msToken = await devSvc.RefreshMicrosoftTokenAsync(refreshToken, ct);
                if (msToken == null)
                {
                    Progress?.Invoke("❌ 正版凭证已失效");
                    return null;
                }

                var chain = await AuthenticateWithXboxChainAsync(msToken, Progress, ct);
                if (chain == null)
                {
                    Progress?.Invoke("❌ 正版凭证刷新失败");
                    return null;
                }

                var account = BuildAccountInfo(msToken, chain);

                Progress?.Invoke($"✅ 正版凭证已刷新：{account.Username}");
                LogHelper.Write(LauncherPaths.AppLog,
                    $"正版凭证刷新成功: {account.Username} ({account.Uuid})，新过期时间 {account.TokenExpireAt:yyyy-MM-dd HH:mm:ss}",
                    $"Microsoft token refreshed: {account.Username} ({account.Uuid}), expires {account.TokenExpireAt:yyyy-MM-dd HH:mm:ss}");

                return account;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (Exception ex)
            {
                LogHelper.Write(LauncherPaths.AppLog,
                    $"静默刷新异常: {ex.Message}",
                    $"Silent refresh exception: {ex.Message}");
                return null;
            }
        }

        // ============================================================
        // 公共链：MS access_token → XBL → XSTS → MC → profile
        // ============================================================
        private async Task<ChainResult?> AuthenticateWithXboxChainAsync(
            MicrosoftTokenResponse msToken,
            Action<string>? progress,
            CancellationToken ct)
        {
            // ============================================================
            // 阶段 4：XBL 认证
            // ============================================================
            progress?.Invoke("正在验证 Xbox Live...");

            var xboxSvc = new XboxAuthService();
            var xblResp = await xboxSvc.AuthenticateWithXboxLiveAsync(msToken.AccessToken);
            if (xblResp == null)
            {
                progress?.Invoke("❌ Xbox Live 认证失败");
                return null;
            }

            // ============================================================
            // 阶段 5：XSTS 认证
            // ============================================================
            progress?.Invoke("正在验证 XSTS...");

            XboxAuthResponse? xstsResp;
            try
            {
                xstsResp = await xboxSvc.AuthenticateWithXstsAsync(xblResp.Token);
            }
            catch (Exception ex)
            {
                // XboxAuthService 会把用户友好错误通过 Exception 抛出
                progress?.Invoke($"❌ {ex.Message}");
                return null;
            }

            if (xstsResp == null || xstsResp.DisplayClaims == null
                || xstsResp.DisplayClaims.Xui.Count == 0)
            {
                progress?.Invoke("❌ XSTS 认证失败");
                return null;
            }

            string uhs = xstsResp.DisplayClaims.Xui[0].Uhs;

            // ============================================================
            // 阶段 6：Minecraft 认证 + profile
            // ============================================================
            progress?.Invoke("正在获取 Minecraft 档案...");

            var mcSvc = new MinecraftAuthService();
            var mcToken = await mcSvc.AuthenticateWithMinecraftAsync(xstsResp.Token, uhs);
            if (mcToken == null)
            {
                progress?.Invoke("❌ Minecraft 认证失败");
                return null;
            }

            MinecraftProfile? profile;
            try
            {
                profile = await mcSvc.GetProfileAsync(mcToken.AccessToken);
            }
            catch (Exception ex)
            {
                progress?.Invoke($"❌ {ex.Message}");
                return null;
            }

            if (profile == null)
            {
                progress?.Invoke("❌ 无法获取 Minecraft 档案");
                return null;
            }

            return new ChainResult { McToken = mcToken, Profile = profile };
        }

        // ============================================================
        // 把 MS token + 链结果拼成 AccountInfo
        // ============================================================
        private static AccountInfo BuildAccountInfo(MicrosoftTokenResponse msToken, ChainResult chain)
        {
            var profile = chain.Profile;
            var mcToken = chain.McToken;

            return new AccountInfo
            {
                Type = "microsoft",
                Username = profile.Name,
                Uuid = profile.Id,   // 无连字符的 32 位 hex
                LastUsed = DateTime.UtcNow,

                MicrosoftRefreshToken = msToken.RefreshToken,
                MinecraftAccessToken = mcToken.AccessToken,
                TokenExpireAt = DateTime.UtcNow.AddSeconds(mcToken.ExpiresIn),
                SkinUrl = profile.Skins.Count > 0 ? profile.Skins[0].Url : ""
            };
        }
    }
}