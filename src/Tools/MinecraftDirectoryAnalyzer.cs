using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Project.Launch.Tools
{
    public class AnalysisResult
    {
        /// <summary>"standard" / "isolated"</summary>
        public string Layout { get; set; } = string.Empty;

        /// <summary>用户选的目录（A 的根 / B 的实例根）</summary>
        public string RootPath { get; set; } = string.Empty;

        /// <summary>versions 目录</summary>
        public string VersionsDir { get; set; } = string.Empty;

        /// <summary>libraries 目录</summary>
        public string LibrariesDir { get; set; } = string.Empty;

        /// <summary>assets 目录</summary>
        public string AssetsDir { get; set; } = string.Empty;

        /// <summary>游戏目录（A = RootPath，B = RootPath/.minecraft）</summary>
        public string GameDirectory { get; set; } = string.Empty;

        /// <summary>扫描到的版本 id 列表</summary>
        public List<string> Versions { get; set; } = new();

        public bool IsValid => !string.IsNullOrEmpty(Layout) && Versions.Count > 0;
    }

    public static class MinecraftDirectoryAnalyzer
    {
        public static AnalysisResult Analyze(string path)
        {
            var result = new AnalysisResult { RootPath = path };

            if (!Directory.Exists(path))
                return result;

            // 判断 A / B
            string versionsInRoot = Path.Combine(path, "versions");
            string minecraftSub = Path.Combine(path, ".minecraft");
            string versionsInSub = Path.Combine(minecraftSub, "versions");

            if (Directory.Exists(versionsInRoot))
            {
                // 形态 A：标准 .minecraft 根目录
                result.Layout = "standard";
                result.VersionsDir = versionsInRoot;
                result.LibrariesDir = Path.Combine(path, "libraries");
                result.AssetsDir = Path.Combine(path, "assets");
                result.GameDirectory = path;
            }
            else if (Directory.Exists(versionsInSub))
            {
                // 形态 B：隔离实例
                result.Layout = "isolated";
                result.VersionsDir = versionsInSub;
                result.LibrariesDir = Path.Combine(path, "libraries");
                result.AssetsDir = Path.Combine(path, "assets");
                result.GameDirectory = minecraftSub;
            }
            else
            {
                // 都不像
                return result;
            }

            // 扫版本
            try
            {
                foreach (var dir in Directory.EnumerateDirectories(result.VersionsDir))
                {
                    string versionId = Path.GetFileName(dir);
                    string jsonPath = Path.Combine(dir, versionId + ".json");
                    if (File.Exists(jsonPath))
                        result.Versions.Add(versionId);
                }

                // 按版本号排序（简单的自然排序，新版本靠后）
                result.Versions = result.Versions
                    .OrderBy(v => v, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            catch (Exception ex)
            {
                LogHelper.Write(LauncherPaths.AppLog,
                    $"扫描版本失败: {ex.Message}",
                    $"Scan versions failed: {ex.Message}");
            }

            return result;
        }
    }
}