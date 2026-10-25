using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Project.Launch.Tools.Auth
{
    /// <summary>
    /// Xbox Live (XBL) + XSTS 认证
    /// </summary>
    public class XboxAuthService
    {
        private static readonly HttpClient _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        private const string XblUrl = "https://user.auth.xboxlive.com/user/authenticate";
        private const string XstsUrl = "https://xsts.auth.xboxlive.com/xsts/authorize";

        // ============================================================
        // 阶段 4：MS access_token → XBL token
        // ============================================================
        public async Task<XboxAuthResponse?> AuthenticateWithXboxLiveAsync(string msAccessToken)
        {
            try
            {
                var req = new XboxAuthRequest
                {
                    Properties = new XboxAuthProperties
                    {
                        AuthMethod = "RPS",
                        SiteName = "user.auth.xboxlive.com",
                        // ★ 官方要求加前缀 "d="
                        RpsTicket = "d=" + msAccessToken
                    },
                    RelyingParty = "http://auth.xboxlive.com",
                    TokenType = "JWT"
                };

                var json = JsonSerializer.Serialize(req);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var resp = await _http.PostAsync(XblUrl, content);
                var respJson = await resp.Content.ReadAsStringAsync();

                if (!resp.IsSuccessStatusCode)
                {
                    LogHelper.Write(LauncherPaths.AppLog,
                        $"XBL 认证失败: {resp.StatusCode} {respJson}",
                        $"XBL auth failed: {resp.StatusCode} {respJson}");
                    return null;
                }

                var result = JsonSerializer.Deserialize<XboxAuthResponse>(respJson);
                LogHelper.Write(LauncherPaths.AppLog,
                    "XBL 认证成功",
                    "XBL auth success");
                return result;
            }
            catch (Exception ex)
            {
                LogHelper.Write(LauncherPaths.AppLog,
                    $"XBL 认证异常: {ex.Message}",
                    $"XBL auth exception: {ex.Message}");
                return null;
            }
        }

        // ============================================================
        // 阶段 5：XBL token → XSTS token
        // ============================================================
        public async Task<XboxAuthResponse?> AuthenticateWithXstsAsync(string xblToken)
        {
            try
            {
                var req = new XstsAuthRequest
                {
                    Properties = new XstsAuthProperties
                    {
                        SandboxId = "RETAIL",
                        UserTokens = new System.Collections.Generic.List<string> { xblToken }
                    },
                    RelyingParty = "rp://api.minecraftservices.com/",
                    TokenType = "JWT"
                };

                var json = JsonSerializer.Serialize(req);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var resp = await _http.PostAsync(XstsUrl, content);
                var respJson = await resp.Content.ReadAsStringAsync();

                if (!resp.IsSuccessStatusCode)
                {
                    // XSTS 有专属错误码
                    XboxAuthResponse? errResp = null;
                    try
                    {
                        errResp = JsonSerializer.Deserialize<XboxAuthResponse>(respJson);
                    }
                    catch { }

                    string errMsg = errResp?.XErr switch
                    {
                        2148916233 => "此微软账号没有 Xbox 账号，请先去 xbox.com 创建",
                        2148916235 => "此账号所在地区不支持 Xbox Live",
                        2148916236 => "此账号需要成人验证",
                        2148916237 => "此账号需要成人验证",
                        2148916238 => "此账号未成年，需要加入家庭组",
                        _ => $"XSTS 认证失败: {resp.StatusCode}"
                    };

                    LogHelper.Write(LauncherPaths.AppLog,
                        $"XSTS 认证失败 [{errResp?.XErr}]: {errMsg}",
                        $"XSTS auth failed [{errResp?.XErr}]: {errMsg}");

                    // 把用户友好的错误存起来，供上层显示
                    throw new Exception(errMsg);
                }

                var result = JsonSerializer.Deserialize<XboxAuthResponse>(respJson);
                LogHelper.Write(LauncherPaths.AppLog,
                    "XSTS 认证成功",
                    "XSTS auth success");
                return result;
            }
            catch (Exception ex)
            {
                LogHelper.Write(LauncherPaths.AppLog,
                    $"XSTS 认证异常: {ex.Message}",
                    $"XSTS auth exception: {ex.Message}");
                throw;
            }
        }
    }
}