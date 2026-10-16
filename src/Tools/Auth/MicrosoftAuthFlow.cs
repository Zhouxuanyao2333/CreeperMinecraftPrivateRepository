using System;
using System.Threading;
using System.Threading.Tasks;

namespace Project.Launch.Tools.Auth
{
    /// <summary>
    /// 微软正版登录全流程协调器
    /// 阶段 1-3：微软 OAuth 设备代码流
    /// 阶段 4-5：Xbox Live + XSTS
    /// 阶段 6：Minecraft 认证 + profile
    /// </summary>
    public class MicrosoftAuthFlow
    {
        /// <summary>登录过程中触发，参数是给用户看的提示</summary>
        public event Action<string>? Progress;

        /// <summary>拿到设备代码后触发（user_code + verification_uri）</summary>
        public event Action<string, string>? DeviceCodeReady;

        /// <summary>
        /// 完整登录流程
        /// 成功返回 AccountInfo，失败返回 null 并把错误抛给 Progress
        /// </summary>
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
                // 阶段 4：XBL 认证
                // ============================================================
                Progress?.Invoke("正在验证 Xbox Live...");

                var xboxSvc = new XboxAuthService();
                var xblResp = await xboxSvc.AuthenticateWithXboxLiveAsync(msToken.AccessToken);
                if (xblResp == null)
                {
                    Progress?.Invoke("❌ Xbox Live 认证失败");
                    return null;
                }

                // ============================================================
                // 阶段 5：XSTS 认证
                // ============================================================
                Progress?.Invoke("正在验证 XSTS...");

                XboxAuthResponse? xstsResp;
                try
                {
                    xstsResp = await xboxSvc.AuthenticateWithXstsAsync(xblResp.Token);
                }
                catch (Exception ex)
                {
                    // XboxAuthService 会把用户友好错误通过 Exception 抛出
                    Progress?.Invoke($"❌ {ex.Message}");
                    return null;
                }

                if (xstsResp == null || xstsResp.DisplayClaims == null
                    || xstsResp.DisplayClaims.Xui.Count == 0)
                {
                    Progress?.Invoke("❌ XSTS 认证失败");
                    return null;
                }

                string uhs = xstsResp.DisplayClaims.Xui[0].Uhs;

                // ============================================================
                // 阶段 6：Minecraft 认证 + profile
                // ============================================================
                Progress?.Invoke("正在获取 Minecraft 档案...");

                var mcSvc = new MinecraftAuthService();
                var mcToken = await mcSvc.AuthenticateWithMinecraftAsync(xstsResp.Token, uhs);
                if (mcToken == null)
                {
                    Progress?.Invoke("❌ Minecraft 认证失败");
                    return null;
                }

                MinecraftProfile? profile;
                try
                {
                    profile = await mcSvc.GetProfileAsync(mcToken.AccessToken);
                }
                catch (Exception ex)
                {
                    Progress?.Invoke($"❌ {ex.Message}");
                    return null;
                }

                if (profile == null)
                {
                    Progress?.Invoke("❌ 无法获取 Minecraft 档案");
                    return null;
                }

                // ============================================================
                // 阶段 7：构造 AccountInfo
                // ============================================================
                var account = new AccountInfo
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

                Progress?.Invoke($"✅ 登录成功：{profile.Name}");
                LogHelper.Write(LauncherPaths.AppLog,
                    $"正版登录成功: {profile.Name} ({profile.Id})",
                    $"Microsoft login success: {profile.Name} ({profile.Id})");

                return account;
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
    }
}