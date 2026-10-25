using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Project.Launch.Tools.Auth
{
    /// <summary>
    /// Minecraft 认证 + 拉取 profile
    /// </summary>
    public class MinecraftAuthService
    {
        private static readonly HttpClient _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        private const string McAuthUrl =
            "https://api.minecraftservices.com/authentication/login_with_xbox";
        private const string McProfileUrl =
            "https://api.minecraftservices.com/minecraft/profile";

        // ============================================================
        // 阶段 6a：XSTS token → Minecraft access_token
        // ============================================================
        public async Task<MinecraftAuthResponse?> AuthenticateWithMinecraftAsync(
            string xstsToken, string uhs)
        {
            try
            {
                // ★ 官方格式：XBL3.0 x=<uhs>;<xsts_token>
                string identityToken = $"XBL3.0 x={uhs};{xstsToken}";

                var req = new MinecraftAuthRequest
                {
                    IdentityToken = identityToken
                };

                var json = JsonSerializer.Serialize(req);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var resp = await _http.PostAsync(McAuthUrl, content);
                var respJson = await resp.Content.ReadAsStringAsync();

                if (!resp.IsSuccessStatusCode)
                {
                    LogHelper.Write(LauncherPaths.AppLog,
                        $"Minecraft 认证失败: {resp.StatusCode} {respJson}",
                        $"Minecraft auth failed: {resp.StatusCode} {respJson}");
                    return null;
                }

                var result = JsonSerializer.Deserialize<MinecraftAuthResponse>(respJson);
                LogHelper.Write(LauncherPaths.AppLog,
                    "Minecraft 认证成功",
                    "Minecraft auth success");
                return result;
            }
            catch (Exception ex)
            {
                LogHelper.Write(LauncherPaths.AppLog,
                    $"Minecraft 认证异常: {ex.Message}",
                    $"Minecraft auth exception: {ex.Message}");
                return null;
            }
        }

        // ============================================================
        // 阶段 6b：拉取 profile（UUID + 用户名 + 皮肤 URL）
        // ============================================================
        public async Task<MinecraftProfile?> GetProfileAsync(string mcAccessToken)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, McProfileUrl);
                request.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", mcAccessToken);

                var resp = await _http.SendAsync(request);
                var respJson = await resp.Content.ReadAsStringAsync();

                if (!resp.IsSuccessStatusCode)
                {
                    if ((int)resp.StatusCode == 404)
                    {
                        // 没买 Minecraft
                        LogHelper.Write(LauncherPaths.AppLog,
                            "此账号未拥有 Minecraft",
                            "This account does not own Minecraft");
                        throw new Exception("此微软账号没有购买 Minecraft");
                    }

                    LogHelper.Write(LauncherPaths.AppLog,
                        $"获取 profile 失败: {resp.StatusCode} {respJson}",
                        $"Get profile failed: {resp.StatusCode} {respJson}");
                    return null;
                }

                var profile = JsonSerializer.Deserialize<MinecraftProfile>(respJson);
                LogHelper.Write(LauncherPaths.AppLog,
                    $"Profile 获取成功: {profile?.Name} ({profile?.Id})",
                    $"Profile obtained: {profile?.Name} ({profile?.Id})");
                return profile;
            }
            catch (Exception ex)
            {
                LogHelper.Write(LauncherPaths.AppLog,
                    $"获取 profile 异常: {ex.Message}",
                    $"Get profile exception: {ex.Message}");
                throw;
            }
        }
    }
}