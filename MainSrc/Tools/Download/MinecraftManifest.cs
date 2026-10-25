using System;
using System.Collections.Generic;

namespace Project.Launch.Tools.Download
{
    public class VersionManifest
    {
        public LatestVersions Latest { get; set; } = new();
        public List<VersionEntry> Versions { get; set; } = new();
    }

    public class LatestVersions
    {
        public string Release { get; set; } = "";
        public string Snapshot { get; set; } = "";
    }

    public class VersionEntry
    {
        public string Id { get; set; } = "";
        public string Type { get; set; } = "";
        public string Url { get; set; } = "";
        public DateTime ReleaseTime { get; set; }

        // 供 UI 显示的格式化时间
        public string ReleaseTimeStr => ReleaseTime.ToString("yyyy-MM-dd");
    }
}