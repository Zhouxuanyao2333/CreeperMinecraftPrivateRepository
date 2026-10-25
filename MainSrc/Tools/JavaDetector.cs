using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Project.Launch.Tools
{
    public class JavaInfo
    {
        public string Path { get; set; } = string.Empty;
        public int MajorVersion { get; set; }
        public string RawVersion { get; set; } = string.Empty;
    }

    public static class JavaDetector
    {
        public static async Task<List<JavaInfo>> DetectAllAsync()
        {
            var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1. JAVA_HOME
            try
            {
                var javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
                if (!string.IsNullOrEmpty(javaHome))
                {
                    var exe = Path.Combine(javaHome, "bin", "java.exe");
                    if (File.Exists(exe)) candidates.Add(exe);
                }
            }
            catch { }

            // 2. PATH
            try
            {
                var pathVar = Environment.GetEnvironmentVariable("PATH") ?? "";
                foreach (var dir in pathVar.Split(Path.PathSeparator))
                {
                    try
                    {
                        var exe = Path.Combine(dir, "java.exe");
                        if (File.Exists(exe)) candidates.Add(exe);
                    }
                    catch { }
                }
            }
            catch { }

            // 3. 常见 JDK 安装目录
            var commonRoots = new[]
            {
                @"C:\Program Files\Java",
                @"C:\Program Files (x86)\Java",
                @"C:\Program Files\Eclipse Adoptium",
                @"C:\Program Files\Microsoft\jdk",
                @"C:\Program Files\Zulu",
                @"C:\Program Files\BellSoft",
                @"C:\Program Files\Amazon Corretto",
            };
            foreach (var root in commonRoots)
            {
                try
                {
                    if (!Directory.Exists(root)) continue;
                    foreach (var sub in Directory.EnumerateDirectories(root))
                    {
                        var exe = Path.Combine(sub, "bin", "java.exe");
                        if (File.Exists(exe)) candidates.Add(exe);
                    }
                }
                catch { }
            }

            // 4. 已保存的 Java 路径
            try
            {
                var inst = InstanceService.GetSelectedInstance();
                if (inst != null && !string.IsNullOrEmpty(inst.JavaPath) && File.Exists(inst.JavaPath))
                    candidates.Add(inst.JavaPath);
            }
            catch { }

            // 5. 逐个执行 java -version
            var result = new List<JavaInfo>();
            foreach (var exe in candidates)
            {
                var info = await GetJavaVersionAsync(exe);
                if (info != null && info.MajorVersion > 0)
                    result.Add(info);
            }

            return result;
        }

        private static async Task<JavaInfo?> GetJavaVersionAsync(string exePath)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = "-version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var proc = Process.Start(psi);
                if (proc == null) return null;

                string stdout = await proc.StandardOutput.ReadToEndAsync();
                string stderr = await proc.StandardError.ReadToEndAsync();
                proc.WaitForExit(5000);

                // java -version 输出在 stderr（JDK 8）或 stdout（JDK 9+）
                string output = stdout + "\n" + stderr;
                int major = ParseMajorVersion(output);
                if (major <= 0) return null;

                string firstLine = output
                    .Split('\n')
                    .Select(l => l.Trim())
                    .FirstOrDefault(l => l.Length > 0) ?? "";

                return new JavaInfo
                {
                    Path = exePath,
                    MajorVersion = major,
                    RawVersion = firstLine
                };
            }
            catch
            {
                return null;
            }
        }

        private static int ParseMajorVersion(string output)
        {
            // 匹配 version "1.8.0_381" 或 "17.0.9" 或 "21.0.1"
            var m = Regex.Match(output, @"version\s+""([^""]+)""");
            if (!m.Success) return -1;

            string ver = m.Groups[1].Value;

            // 1.8.xxx → 8
            if (ver.StartsWith("1."))
            {
                var parts = ver.Split('.');
                if (parts.Length >= 2 && int.TryParse(parts[1], out int v8))
                    return v8;
            }

            // 17.0.9 → 17
            var firstDot = ver.IndexOf('.');
            string majorStr = firstDot > 0 ? ver.Substring(0, firstDot) : ver;
            if (int.TryParse(majorStr, out int major))
                return major;

            return -1;
        }

        public static JavaInfo? Match(IEnumerable<JavaInfo> available, int requiredMajor)
        {
            var list = available.ToList();
            if (list.Count == 0) return null;

            // 1. 精确匹配
            var exact = list.FirstOrDefault(j => j.MajorVersion == requiredMajor);
            if (exact != null) return exact;

            // 2. 最小且 >= requiredMajor
            var higher = list.Where(j => j.MajorVersion > requiredMajor)
                             .OrderBy(j => j.MajorVersion)
                             .FirstOrDefault();
            return higher;
        }
    }
}