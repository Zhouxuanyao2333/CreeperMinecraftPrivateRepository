using System.Collections.Generic;

namespace Project.Launch.Tools
{
    /// <summary>
    /// 音效配置——逻辑名直接映射到文件名
    /// Sound configuration - logical name maps directly to filename
    /// </summary>
    public static class SoundConfig
    {
        /// <summary>
        /// 音频资源根路径（末尾要带斜杠）
        /// Asset root path (trailing slash required)
        /// </summary>
        public const string AssetRoot = "avares://Project.Launch/src/Sound/";

        /// <summary>
        /// 全局音量倍率（0.0 ~ 1.0）
        /// Global volume multiplier (0.0 ~ 1.0)
        /// </summary>
        public static double MasterVolume { get; set; } = 0.7;

        /// <summary>
        /// 总开关：false 时所有 Play 直接返回
        /// Master switch: when false, Play returns immediately
        /// </summary>
        public static bool Enabled { get; set; } = true;

        /// <summary>
        /// 单个音效的音量覆盖（乘在 MasterVolume 之上）
        /// Per-sound volume override (multiplied on top of MasterVolume)
        /// 未列出的音效默认 1.0
        /// </summary>
        public static readonly Dictionary<string, double> VolumeOverrides = new()
        {
            { "Open",         0.7 },
            { "DownloadDone", 0.9 },
            { "Messages",     0.8 },
            { "Messages2",    0.8 },
            { "Errors",       1.0 },
            { "BigError",     1.0 },
        };

        /// <summary>
        /// 取某个音效的最终音量（已乘 MasterVolume）
        /// Get final volume for a sound (MasterVolume already applied)
        /// </summary>
        public static double GetVolume(string name)
        {
            double perSound = VolumeOverrides.TryGetValue(name, out var v) ? v : 1.0;
            return MasterVolume * perSound;
        }
    }
}