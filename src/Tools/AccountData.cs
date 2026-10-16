using System;
using System.Collections.Generic;

namespace Project.Launch.Tools
{
    public class AccountInfo
    {
        public string Type { get; set; } = "offline";
        public string Username { get; set; } = string.Empty;
        public string Uuid { get; set; } = string.Empty;
        public DateTime LastUsed { get; set; } = DateTime.UtcNow;

        // ============================================================
        // 正版账户专属字段（离线账户留空）
        // ============================================================

        /// <summary>微软 refresh_token（长期，用于静默刷新）</summary>
        public string MicrosoftRefreshToken { get; set; } = string.Empty;

        /// <summary>Minecraft access_token（短期，24 小时）</summary>
        public string MinecraftAccessToken { get; set; } = string.Empty;

        /// <summary>token 过期时间（UTC）</summary>
        public DateTime TokenExpireAt { get; set; } = DateTime.MinValue;

        /// <summary>皮肤 URL（正版专属，留空则用 UUID 推算 Steve/Alex）</summary>
        public string SkinUrl { get; set; } = string.Empty;
    }

    public class AccountsRoot
    {
        public Dictionary<string, AccountInfo> Accounts { get; set; } = new();
        public string SelectedAccount { get; set; } = string.Empty;
        public bool LoginCheck { get; set; } = false;
    }
}