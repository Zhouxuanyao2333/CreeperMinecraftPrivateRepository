using System;

namespace Project.Launch.Tools
{
    /// <summary>
    /// 根据 UUID 决定默认皮肤（Steve / Alex）
    ///
    /// 算法来源：Minecraft Java 版官方逻辑
    ///   取 UUID 第 8、16、24、32 位（0-based 索引 7、15、23、31）的 hex 值
    ///   异或后取末位：奇数 → Alex，偶数 → Steve
    /// </summary>
    public static class DefaultSkinSelector
    {
        public const string SteveAssetPath =
            "avares://Project.Launch/src/Views/imgs/skin/Steve.png";

        public const string AlexAssetPath =
            "avares://Project.Launch/src/Views/imgs/skin/Alex.png";

        /// <summary>
        /// 根据 UUID 判断是否应使用 Alex 模型
        /// </summary>
        public static bool ShouldUseAlex(string? uuid)
        {
            // ★ UUID 缺失是错误状态，不静默兜底，写日志暴露
            if (string.IsNullOrEmpty(uuid))
            {
                LogHelper.Write(LauncherPaths.AppLog,
                    "[错误] UUID 为空，无法判定 Steve/Alex（异常状态）",
                    "[ERROR] UUID is null/empty, cannot determine Steve/Alex");
                return false;
            }

            string hex = uuid.Replace("-", "");

            if (hex.Length < 32)
            {
                LogHelper.Write(LauncherPaths.AppLog,
                    $"[错误] UUID 长度不足（{uuid}），无法判定 Steve/Alex（异常状态）",
                    $"[ERROR] UUID too short ({uuid}), cannot determine Steve/Alex");
                return false;
            }

            try
            {
                int a = Convert.ToInt32(hex[7].ToString(), 16);
                int b = Convert.ToInt32(hex[15].ToString(), 16);
                int c = Convert.ToInt32(hex[23].ToString(), 16);
                int d = Convert.ToInt32(hex[31].ToString(), 16);

                int xor = a ^ b ^ c ^ d;
                return (xor & 1) == 1;
            }
            catch (Exception ex)
            {
                LogHelper.Write(LauncherPaths.AppLog,
                    $"[错误] UUID 解析失败（{uuid}）: {ex.Message}",
                    $"[ERROR] UUID parse failed ({uuid}): {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 根据 UUID 返回对应的皮肤资源路径
        /// </summary>
        public static string GetSkinAssetPath(string? uuid)
        {
            return ShouldUseAlex(uuid) ? AlexAssetPath : SteveAssetPath;
        }
    }
}