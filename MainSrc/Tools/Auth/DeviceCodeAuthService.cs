using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Project.Launch.Tools.Auth
{
    /// <summary>
    /// 微软 OAuth 2.0 设备代码流
    /// </summary>
    public class DeviceCodeAuthService
    {
        public const string ClientId = "7dddbcf7-5d7d-46cf-96fc-292d6f392cd0";

        // ★ 修复：个人账户必须用 consumers 端点
        public const string Tenant = "consumers";

        // ★ 修复：XboxLive.offline_access 而非 offline_access
        public const string Scope = "XboxLive.signin XboxLive.offline_access";

        private static readonly HttpClient _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        private const string DeviceCodeUrl =
            "https://login.microsoftonline.com/" + Tenant + "/oauth2/v2.0/devicecode";
        private const string TokenUrl =
            "https://login.microsoftonline.com/" + Tenant + "/oauth2/v2.0/token";

        // ============================================================
        // 阶段 1：申请设备代码
        // ============================================================
        public async Task<DeviceCodeResponse?> RequestDeviceCodeAsync()
        {
            try
            {
                var content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("client_id", ClientId),
                    new KeyValuePair<string, string>("scope", Scope)
                });

                var resp = await _http.PostAsync(DeviceCodeUrl, content);
                var json = await resp.Content.ReadAsStringAsync();

                if (!resp.IsSuccessStatusCode)
                {
                    LogHelper.Write(LauncherPaths.AppLog,
                        $"设备代码申请失败: {resp.StatusCode} {json}",
                        $"Device code request failed: {resp.StatusCode} {json}");
                    return null;
                }

                var result = JsonSerializer.Deserialize<DeviceCodeResponse>(json);
                LogHelper.Write(LauncherPaths.AppLog,
                    $"设备代码已申请: {result?.UserCode}",
                    $"Device code obtained: {result?.UserCode}");
                return result;
            }
            catch (Exception ex)
            {
                LogHelper.Write(LauncherPaths.AppLog,
                    $"设备代码申请异常: {ex.Message}",
                    $"Device code request exception: {ex.Message}");
                return null;
            }
        }

        // ============================================================
        // 阶段 3：轮询等待用户授权
        // ============================================================
        public async Task<MicrosoftTokenResponse?> PollForTokenAsync(
            string deviceCode,
            int intervalSeconds,
            int expiresInSeconds,
            CancellationToken ct = default)
        {
            var startTime = DateTime.UtcNow;
            var timeout = TimeSpan.FromSeconds(expiresInSeconds);
            int interval = Math.Max(3, intervalSeconds);

            while (true)
            {
                if (ct.IsCancellationRequested)
                {
                    LogHelper.Write(LauncherPaths.AppLog,
                        "用户取消轮询",
                        "User cancelled polling");
                    return null;
                }

                if (DateTime.UtcNow - startTime > timeout)
                {
                    LogHelper.Write(LauncherPaths.AppLog,
                        "设备代码已超时",
                        "Device code expired");
                    return null;
                }

                // 先等，再轮询
                try
                {
                    await Task.Delay(interval * 1000, ct);
                }
                catch (OperationCanceledException)
                {
                    return null;
                }

                try
                {
                    var content = new FormUrlEncodedContent(new[]
                    {
                        new KeyValuePair<string, string>(
                            "grant_type",
                            "urn:ietf:params:oauth:grant-type:device_code"),
                        new KeyValuePair<string, string>("client_id", ClientId),
                        new KeyValuePair<string, string>("device_code", deviceCode)
                    });

                    var resp = await _http.PostAsync(TokenUrl, content, ct);
                    var json = await resp.Content.ReadAsStringAsync(ct);

                    if (resp.IsSuccessStatusCode)
                    {
                        var token = JsonSerializer.Deserialize<MicrosoftTokenResponse>(json);
                        LogHelper.Write(LauncherPaths.AppLog,
                            "微软账号授权成功，已获取 access_token",
                            "Microsoft auth success, access_token obtained");
                        return token;
                    }

                    // 错误处理
                    MicrosoftErrorResponse? err = null;
                    try
                    {
                        err = JsonSerializer.Deserialize<MicrosoftErrorResponse>(json);
                    }
                    catch { }

                    switch (err?.Error)
                    {
                        case "authorization_pending":
                            continue;

                        case "slow_down":
                            interval += 5;
                            continue;

                        case "access_denied":
                            LogHelper.Write(LauncherPaths.AppLog,
                                "用户拒绝了授权",
                                "User denied authorization");
                            return null;

                        case "expired_token":
                            LogHelper.Write(LauncherPaths.AppLog,
                                "设备代码已过期",
                                "Device code expired");
                            return null;

                        default:
                            LogHelper.Write(LauncherPaths.AppLog,
                                $"OAuth 错误: {err?.Error} - {err?.ErrorDescription}",
                                $"OAuth error: {err?.Error} - {err?.ErrorDescription}");
                            return null;
                    }
                }
                catch (OperationCanceledException)
                {
                    return null;
                }
                catch (Exception ex)
                {
                    LogHelper.Write(LauncherPaths.AppLog,
                        $"轮询异常: {ex.Message}",
                        $"Poll exception: {ex.Message}");
                    continue;
                }
            }
        }
    }
}