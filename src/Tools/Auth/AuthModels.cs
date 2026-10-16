using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Project.Launch.Tools.Auth
{
    // ============================================================
    // 微软 OAuth 设备代码流
    // ============================================================

    public class DeviceCodeResponse
    {
        [JsonPropertyName("device_code")]
        public string DeviceCode { get; set; } = "";

        [JsonPropertyName("user_code")]
        public string UserCode { get; set; } = "";

        [JsonPropertyName("verification_uri")]
        public string VerificationUri { get; set; } = "";

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonPropertyName("interval")]
        public int Interval { get; set; } = 5;

        [JsonPropertyName("message")]
        public string Message { get; set; } = "";
    }

    public class MicrosoftTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = "";

        [JsonPropertyName("refresh_token")]
        public string RefreshToken { get; set; } = "";

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonPropertyName("token_type")]
        public string TokenType { get; set; } = "Bearer";
    }

    public class MicrosoftErrorResponse
    {
        [JsonPropertyName("error")]
        public string Error { get; set; } = "";

        [JsonPropertyName("error_description")]
        public string ErrorDescription { get; set; } = "";
    }

    // ============================================================
    // Xbox Live 认证
    // ============================================================

    public class XboxAuthRequest
    {
        [JsonPropertyName("Properties")]
        public XboxAuthProperties Properties { get; set; } = new();

        [JsonPropertyName("RelyingParty")]
        public string RelyingParty { get; set; } = "http://auth.xboxlive.com";

        [JsonPropertyName("TokenType")]
        public string TokenType { get; set; } = "JWT";
    }

    public class XboxAuthProperties
    {
        [JsonPropertyName("AuthMethod")]
        public string AuthMethod { get; set; } = "RPS";

        [JsonPropertyName("SiteName")]
        public string SiteName { get; set; } = "user.auth.xboxlive.com";

        [JsonPropertyName("RpsTicket")]
        public string RpsTicket { get; set; } = "";
    }

    public class XboxAuthResponse
    {
        [JsonPropertyName("Token")]
        public string Token { get; set; } = "";

        [JsonPropertyName("DisplayClaims")]
        public XboxDisplayClaims? DisplayClaims { get; set; }

        [JsonPropertyName("XErr")]
        public long? XErr { get; set; }

        [JsonPropertyName("Message")]
        public string? Message { get; set; }
    }

    public class XboxDisplayClaims
    {
        [JsonPropertyName("xui")]
        public List<XboxXui> Xui { get; set; } = new();
    }

    public class XboxXui
    {
        [JsonPropertyName("uhs")]
        public string Uhs { get; set; } = "";
    }

    // ============================================================
    // XSTS 认证
    // ============================================================

    public class XstsAuthRequest
    {
        [JsonPropertyName("Properties")]
        public XstsAuthProperties Properties { get; set; } = new();

        [JsonPropertyName("RelyingParty")]
        public string RelyingParty { get; set; } = "rp://api.minecraftservices.com/";

        [JsonPropertyName("TokenType")]
        public string TokenType { get; set; } = "JWT";
    }

    public class XstsAuthProperties
    {
        [JsonPropertyName("SandboxId")]
        public string SandboxId { get; set; } = "RETAIL";

        [JsonPropertyName("UserTokens")]
        public List<string> UserTokens { get; set; } = new();
    }

    // ============================================================
    // Minecraft 认证
    // ============================================================

    public class MinecraftAuthRequest
    {
        [JsonPropertyName("identityToken")]
        public string IdentityToken { get; set; } = "";
    }

    public class MinecraftAuthResponse
    {
        [JsonPropertyName("username")]
        public string Username { get; set; } = "";

        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = "";

        [JsonPropertyName("token_type")]
        public string TokenType { get; set; } = "Bearer";

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }

    public class MinecraftProfile
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("skins")]
        public List<MinecraftSkin> Skins { get; set; } = new();
    }

    public class MinecraftSkin
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("state")]
        public string State { get; set; } = "";

        [JsonPropertyName("url")]
        public string Url { get; set; } = "";

        [JsonPropertyName("variant")]
        public string Variant { get; set; } = "";
    }
}