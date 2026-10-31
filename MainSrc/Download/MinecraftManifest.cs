using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Avalonia.Media.Imaging;

namespace Project.Launch.Download
{
    public class VersionManifest
    {
        [JsonPropertyName("latest")]
        public LatestVersions Latest { get; set; } = new();

        [JsonPropertyName("versions")]
        public List<VersionEntry> Versions { get; set; } = new();
    }

    public class LatestVersions
    {
        [JsonPropertyName("release")]
        public string Release { get; set; } = "";

        [JsonPropertyName("snapshot")]
        public string Snapshot { get; set; } = "";
    }

    public class VersionEntry
    {
        // ★ 改名避开基础类型的隐藏冲突
        [JsonPropertyName("id")]
        public string VersionId { get; set; } = "";

        [JsonPropertyName("type")]
        public string Type { get; set; } = "";

        [JsonPropertyName("url")]
        public string Url { get; set; } = "";

        [JsonPropertyName("releaseTime")]
        public DateTime ReleaseTime { get; set; }

        // 供 UI 显示的格式化时间
        public string ReleaseTimeStr => ReleaseTime.ToString("yyyy-MM-dd");

        // ★ UI 显示的图标路径
        public string IconPath { get; set; } = "";

        // ★ UI 直接绑定的图标对象
        public Bitmap? IconBitmap { get; set; }
    }
}