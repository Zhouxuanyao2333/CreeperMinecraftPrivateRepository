using System;

namespace Project.Launch.Tools.JavaConfig
{
    /// <summary>
    /// 游戏启动所需的身份认证信息。
    ///
    /// 放在 JavaConfig 命名空间而不是 Auth，是为了避免命名空间循环引用：
    /// Auth.AccountService 已依赖 Tools.JavaConfig.LauncherPaths，
    /// 若 GameLauncher 反过来依赖 Auth.AccountInfo，会造成循环。
    /// 这里用一个纯粹的、无外部依赖的数据载体把两者解耦。
    /// </summary>
    public sealed class LaunchAuthInfo
    {
        /// <summary>游戏内显示的用户名（离线 / 微软均适用）。</summary>
        public required string PlayerName { get; init; }

        /// <summary>无连字符的 32 位 UUID。离线 UUID 与微软 UUID 统一用这一格式。</summary>
        public required string Uuid { get; init; }

        /// <summary>
        /// Minecraft access token。
        /// 微软正版填真实 token；离线账户填 "0"（与改造前行为一致，离线服务器不校验）。
        /// </summary>
        public string AccessToken { get; init; } = "0";

        /// <summary>
        /// 传给 ${user_type} 的值。
        ///   "msa"    → 微软正版（1.16+ 的 JSON 会用到）
        ///   "legacy" → 离线 / 老 Mojang 账户
        /// </summary>
        public string UserType { get; init; } = "legacy";

        /// <summary>
        /// Xbox xuid。当前链路未从 XSTS 的 xid 字段回读，未知时留空，
        /// 占位符会回退为 "0"（与改造前一致）。
        /// </summary>
        public string Xuid { get; init; } = "";

        /// <summary>是否为微软正版账户。</summary>
        public bool IsMicrosoft =>
            string.Equals(UserType, "msa", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// 构造离线账户的启动信息：user_type=legacy，access_token="0"，xuid 空。
        /// </summary>
        public static LaunchAuthInfo ForOffline(string playerName, string uuid) => new()
        {
            PlayerName = playerName,
            Uuid = uuid,
            AccessToken = "0",
            UserType = "legacy",
            Xuid = ""
        };

        /// <summary>
        /// 构造微软正版账户的启动信息。
        /// </summary>
        public static LaunchAuthInfo ForMicrosoft(string playerName, string uuid, string accessToken, string xuid = "") => new()
        {
            PlayerName = playerName,
            Uuid = uuid,
            AccessToken = string.IsNullOrEmpty(accessToken) ? "0" : accessToken,
            UserType = "msa",
            Xuid = xuid ?? ""
        };
    }
}