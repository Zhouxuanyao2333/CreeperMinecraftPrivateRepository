using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Project.Launch.Tools
{
    /// <summary>
    /// Minecraft 版本 JSON 的数据模型
    /// Data model for Minecraft version JSON
    /// </summary>
    public class MinecraftVersionJson
    {
        public string Id { get; set; } = string.Empty;
        public string MainClass { get; set; } = string.Empty;
        public JavaVersionInfo? JavaVersion { get; set; }
        public AssetIndexInfo? AssetIndex { get; set; }
        public string Assets { get; set; } = string.Empty;
        public List<LibraryEntry> Libraries { get; set; } = new();
        public ArgumentsSection? Arguments { get; set; }
        public DownloadsSection? Downloads { get; set; }

        /// <summary>旧版（1.12-）的参数字符串</summary>
        public string? MinecraftArguments { get; set; }

        public static MinecraftVersionJson? Load(string jsonPath)
        {
            try
            {
                string json = File.ReadAllText(jsonPath);
                var opts = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                return JsonSerializer.Deserialize<MinecraftVersionJson>(json, opts);
            }
            catch (Exception ex)
            {
                LogHelper.Write(LauncherPaths.AppLog,
                    $"解析版本 json 失败: {ex.Message}",
                    $"Failed to parse version json: {ex.Message}");
                return null;
            }
        }
    }

    public class JavaVersionInfo
    {
        public string Component { get; set; } = string.Empty;
        public int MajorVersion { get; set; } = 8;
    }

    public class AssetIndexInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Sha1 { get; set; } = string.Empty;
        public long Size { get; set; }
        public long TotalSize { get; set; }
        public string Url { get; set; } = string.Empty;
    }

    public class DownloadsSection
    {
        public LibraryArtifact? Client { get; set; }
    }

    public class LibraryEntry
    {
        public string Name { get; set; } = string.Empty;
        public LibraryDownloads? Downloads { get; set; }
        public Dictionary<string, string>? Natives { get; set; }
        public List<LibraryRule>? Rules { get; set; }
    }

    public class LibraryDownloads
    {
        public LibraryArtifact? Artifact { get; set; }
        public Dictionary<string, LibraryArtifact>? Classifiers { get; set; }
    }

    public class LibraryArtifact
    {
        public string Path { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string Sha1 { get; set; } = string.Empty;
        public long Size { get; set; }
    }

    public class LibraryRule
    {
        public string Action { get; set; } = string.Empty;
        public RuleOs? Os { get; set; }
        public Dictionary<string, bool>? Features { get; set; }
    }

    public class RuleOs
    {
        public string? Name { get; set; }
        public string? Version { get; set; }
        public string? Arch { get; set; }
    }

    public class ArgumentsSection
    {
        /// <summary>元素可能是 string，也可能是带 rules 的 object</summary>
        public List<JsonElement> Game { get; set; } = new();
        public List<JsonElement> Jvm { get; set; } = new();
    }
}