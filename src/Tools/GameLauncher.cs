using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Project.Launch.Tools
{
    public static class GameLauncher
    {
        public static async Task<bool> LaunchAsync(
            InstanceInfo instance,
            MinecraftVersionJson versionJson,
            JavaInfo java,
            string playerName,
            string playerUuid)
        {
            try
            {
                // 1. 计算路径
                string versionsDir = Path.Combine(instance.RootPath, "versions");
                string librariesDir = Path.Combine(instance.RootPath, "libraries");
                string assetsDir = Path.Combine(instance.RootPath, "assets");
                string gameDirectory = instance.Layout == "isolated"
                    ? Path.Combine(instance.RootPath, ".minecraft")
                    : instance.RootPath;

                string clientJar = Path.Combine(versionsDir, instance.VersionId, instance.VersionId + ".jar");
                if (!File.Exists(clientJar))
                {
                    LogHelper.Write(LauncherPaths.AppLog,
                        $"client.jar 不存在: {clientJar}",
                        $"client.jar not found: {clientJar}");
                    return false;
                }

                // 2. natives 目录
                string nativesDir = Path.Combine(LauncherPaths.Root, "instances", instance.Id, "natives");
                Directory.CreateDirectory(nativesDir);

                // 3. 拼 classpath + 解压 natives
                var classpathParts = new List<string> { clientJar };
                foreach (var lib in versionJson.Libraries)
                {
                    if (!IsLibraryAllowed(lib)) continue;

                    // 普通 jar
                    if (lib.Downloads?.Artifact != null && !string.IsNullOrEmpty(lib.Downloads.Artifact.Path))
                    {
                        string libPath = Path.Combine(librariesDir,
                            lib.Downloads.Artifact.Path.Replace('/', Path.DirectorySeparatorChar));
                        if (File.Exists(libPath)) classpathParts.Add(libPath);
                    }

                    // natives
                    if (lib.Natives != null && lib.Downloads?.Classifiers != null)
                    {
                        string nativeKey = GetNativeKey(lib.Natives);
                        if (!string.IsNullOrEmpty(nativeKey) &&
                            lib.Downloads.Classifiers.TryGetValue(nativeKey, out var nativeArtifact) &&
                            !string.IsNullOrEmpty(nativeArtifact.Path))
                        {
                            string nativeJar = Path.Combine(librariesDir,
                                nativeArtifact.Path.Replace('/', Path.DirectorySeparatorChar));
                            if (File.Exists(nativeJar))
                                ExtractNatives(nativeJar, nativesDir);
                        }
                    }
                }

                string classpath = string.Join(Path.PathSeparator, classpathParts);

                // 4. 拼参数
                var args = new List<string>();

                args.Add($"-Djava.library.path={nativesDir}");
                args.Add("-cp");
                args.Add(classpath);

                // JVM 参数（新版本 json）
                if (versionJson.Arguments?.Jvm != null && versionJson.Arguments.Jvm.Count > 0)
                {
                    foreach (var el in versionJson.Arguments.Jvm)
                    {
                        foreach (var s in ExtractArguments(el, instance, nativesDir, assetsDir,
                                     gameDirectory, classpath, playerName, playerUuid, versionJson))
                            args.Add(s);
                    }
                }
                else
                {
                    // 老版本没有 arguments.jvm，给默认
                    args.Add("-Djava.library.path=" + nativesDir);
                }

                // 主类
                args.Add(versionJson.MainClass);

                // 游戏参数
                if (versionJson.Arguments?.Game != null && versionJson.Arguments.Game.Count > 0)
                {
                    foreach (var el in versionJson.Arguments.Game)
                    {
                        foreach (var s in ExtractArguments(el, instance, nativesDir, assetsDir,
                                     gameDirectory, classpath, playerName, playerUuid, versionJson))
                            args.Add(s);
                    }
                }
                else if (!string.IsNullOrEmpty(versionJson.MinecraftArguments))
                {
                    // 旧版字符串参数
                    string replaced = ReplacePlaceholders(versionJson.MinecraftArguments,
                        instance, nativesDir, assetsDir, gameDirectory, classpath,
                        playerName, playerUuid, versionJson);
                    args.AddRange(replaced.Split(' ', StringSplitOptions.RemoveEmptyEntries));
                }

                // 5. 日志文件
                string logDir = Path.Combine(LauncherPaths.Root, "instances", instance.Id);
                Directory.CreateDirectory(logDir);
                string logPath = Path.Combine(logDir, "game.log");
                try { File.WriteAllText(logPath, $"=== Game log - {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===\n"); }
                catch { }

                // 6. 起进程
                var psi = new ProcessStartInfo
                {
                    FileName = java.Path,
                    WorkingDirectory = gameDirectory,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };
                foreach (var a in args) psi.ArgumentList.Add(a);

                var proc = new Process { StartInfo = psi };

                proc.OutputDataReceived += (s, e) =>
                {
                    if (e.Data != null) AppendLog(logPath, e.Data);
                };
                proc.ErrorDataReceived += (s, e) =>
                {
                    if (e.Data != null) AppendLog(logPath, e.Data);
                };

                proc.Start();
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();

                LogHelper.Write(LauncherPaths.AppLog,
                    $"游戏已启动 (PID={proc.Id})",
                    $"Game launched (PID={proc.Id})");

                return await Task.FromResult(true);
            }
            catch (Exception ex)
            {
                LogHelper.Write(LauncherPaths.AppLog,
                    $"启动游戏失败: {ex.Message}",
                    $"Launch failed: {ex.Message}");
                return false;
            }
        }

        private static bool IsLibraryAllowed(LibraryEntry lib)
        {
            if (lib.Rules == null || lib.Rules.Count == 0) return true;

            bool allowed = false;
            foreach (var rule in lib.Rules)
            {
                bool matches = MatchesRule(rule);
                if (!matches) continue;

                if (rule.Action == "allow") allowed = true;
                else if (rule.Action == "disallow") return false;
            }
            return allowed;
        }

        private static bool MatchesRule(LibraryRule rule)
        {
            if (rule.Os != null && !string.IsNullOrEmpty(rule.Os.Name))
            {
                if (!rule.Os.Name.Equals("windows", StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            return true;
        }

        private static string GetNativeKey(Dictionary<string, string> natives)
        {
            if (natives.TryGetValue("windows", out var key))
                return key.Replace("${arch}", Environment.Is64BitOperatingSystem ? "64" : "32");
            return string.Empty;
        }

        private static void ExtractNatives(string nativeJar, string targetDir)
        {
            try
            {
                using var zip = ZipFile.OpenRead(nativeJar);
                foreach (var entry in zip.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name)) continue;
                    if (entry.FullName.StartsWith("META-INF/")) continue;

                    string outPath = Path.Combine(targetDir, entry.Name);
                    entry.ExtractToFile(outPath, overwrite: true);
                }
            }
            catch (Exception ex)
            {
                LogHelper.Write(LauncherPaths.AppLog,
                    $"解压 natives 失败 ({Path.GetFileName(nativeJar)}): {ex.Message}",
                    $"Extract natives failed ({Path.GetFileName(nativeJar)}): {ex.Message}");
            }
        }

        private static IEnumerable<string> ExtractArguments(JsonElement el,
            InstanceInfo instance, string nativesDir, string assetsDir, string gameDirectory,
            string classpath, string playerName, string playerUuid, MinecraftVersionJson vj)
        {
            var result = new List<string>();

            if (el.ValueKind == JsonValueKind.String)
            {
                string raw = el.GetString() ?? "";
                result.Add(ReplacePlaceholders(raw, instance, nativesDir, assetsDir,
                    gameDirectory, classpath, playerName, playerUuid, vj));
                return result;
            }

            if (el.ValueKind == JsonValueKind.Object)
            {
                if (el.TryGetProperty("rules", out var rules) && !ArgumentAllowed(rules))
                    return result;

                if (el.TryGetProperty("value", out var value))
                {
                    if (value.ValueKind == JsonValueKind.String)
                    {
                        result.Add(ReplacePlaceholders(value.GetString() ?? "",
                            instance, nativesDir, assetsDir, gameDirectory, classpath,
                            playerName, playerUuid, vj));
                    }
                    else if (value.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in value.EnumerateArray())
                        {
                            if (item.ValueKind == JsonValueKind.String)
                            {
                                result.Add(ReplacePlaceholders(item.GetString() ?? "",
                                    instance, nativesDir, assetsDir, gameDirectory, classpath,
                                    playerName, playerUuid, vj));
                            }
                        }
                    }
                }
            }

            return result;
        }

        private static bool ArgumentAllowed(JsonElement rules)
        {
            if (rules.ValueKind != JsonValueKind.Array) return true;

            bool allowed = true;
            foreach (var rule in rules.EnumerateArray())
            {
                string action = rule.TryGetProperty("action", out var a) ? a.GetString() ?? "" : "";
                bool matches = true;

                if (rule.TryGetProperty("os", out var os))
                {
                    if (os.TryGetProperty("name", out var name))
                    {
                        var n = name.GetString();
                        if (!string.IsNullOrEmpty(n) &&
                            !n.Equals("windows", StringComparison.OrdinalIgnoreCase))
                            matches = false;
                    }
                }

                if (matches)
                {
                    if (action == "allow") allowed = true;
                    else if (action == "disallow") return false;
                }
            }
            return allowed;
        }

        private static string ReplacePlaceholders(string template,
            InstanceInfo instance, string nativesDir, string assetsDir, string gameDirectory,
            string classpath, string playerName, string playerUuid, MinecraftVersionJson vj)
        {
            return template
                .Replace("${natives_directory}", nativesDir)
                .Replace("${launcher_name}", "Kilim")
                .Replace("${launcher_version}", "0.2.1")
                .Replace("${classpath}", classpath)
                .Replace("${classpath_separator}", Path.PathSeparator.ToString())
                .Replace("${library_directory}", Path.Combine(instance.RootPath, "libraries"))
                .Replace("${auth_player_name}", playerName)
                .Replace("${auth_uuid}", playerUuid)
                .Replace("${auth_access_token}", "0")
                .Replace("${auth_session}", "0")
                .Replace("${user_type}", "legacy")
                .Replace("${version_name}", instance.VersionId)
                .Replace("${game_directory}", gameDirectory)
                .Replace("${assets_root}", assetsDir)
                .Replace("${assets_index_name}", vj.AssetIndex?.Id ?? vj.Assets ?? "")
                .Replace("${user_properties}", "{}")
                .Replace("${resolution_width}", "925")
                .Replace("${resolution_height}", "530")
                .Replace("${clientid}", "")
                .Replace("${auth_xuid}", "0");
        }

        private static void AppendLog(string path, string line)
        {
            try
            {
                File.AppendAllText(path, line + "\n");
            }
            catch { }
        }
    }
}