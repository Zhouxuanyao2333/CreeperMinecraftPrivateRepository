using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Project.Launch.Tools.JavaConfig;
using Project.Launch.Tools.LogsConfig;

namespace Project.Launch.Download
{
    /// <summary>下载进度（UI 消费）。</summary>
    public class DownloadProgress
    {
        public string State { get; set; } = "";
        public double Percentage { get; set; }
        public long SpeedBytesPerSec { get; set; }
        public int TotalFiles { get; set; }
        public int CompletedFiles { get; set; }
        public int FailedFiles { get; set; }
    }

    /// <summary>安装结果 —— 不再「无条件报告 100% 完成」。</summary>
    public sealed class InstallResult
    {
        public int TotalFiles { get; set; }
        public int CompletedFiles { get; set; }
        public int FailedFiles { get; set; }
        public int SkippedFiles { get; set; }
        public long TotalBytes { get; set; }
        public long DownloadedBytes { get; set; }

        public bool Success => TotalFiles > 0 && CompletedFiles == TotalFiles && FailedFiles == 0;

        public string Summary =>
            $"共 {TotalFiles} 个文件：成功 {CompletedFiles}，跳过（已存在）{SkippedFiles}，失败 {FailedFiles}";
    }

    /// <summary>
    /// 安装调度 —— 对齐 PCL 的核心行为：
    ///   1) 每个文件按「源健康度」顺序尝试，坏源冷却后不再被首选
    ///   2) 失速 / 404 / 403 / 429 / DNS 失败 → 立刻换源，不在坏源上反复重试
    ///   3) 全部源都失败 → 再整体降级重试一轮（PCL 的 Retried 语义）
    ///   4) 真实统计成功/失败，失败聚合写日志
    /// </summary>
    public static class GameInstaller
    {
        /// <summary>单个源上对「可恢复错误」的最大尝试次数。</summary>
        private const int PerSourceAttempts = 2;

        /// <summary>失败明细日志最多打印多少条。</summary>
        private const int MaxFailureLogLines = 50;

        private sealed class FileTask
        {
            public required string OfficialUrl { get; init; }
            public required DownloadSourceKind Kind { get; init; }
            public required string LocalPath { get; init; }
            public required string DisplayPath { get; init; }
            public string? Sha1 { get; init; }
            public long Size { get; init; }
        }

        // ============================================================
        // 入口
        // ============================================================

        public static async Task<InstallResult> InstallVanillaAsync(
            VersionEntry version,
            string targetPath,
            int maxConcurrency,
            IProgress<DownloadProgress> progress,
            CancellationToken ct)
        {
            SourcePolicy.Reset();

            if (maxConcurrency < 1) maxConcurrency = 1;
            if (maxConcurrency > 128) maxConcurrency = 128;

            string targetVersionsDir = Path.Combine(targetPath, "versions");
            string targetLibrariesDir = Path.Combine(targetPath, "libraries");
            string targetAssetsDir = Path.Combine(targetPath, "assets");
            string targetVersionDir = Path.Combine(targetVersionsDir, version.VersionId);
            Directory.CreateDirectory(targetVersionDir);

            var result = new InstallResult();
            long startBytes = Interlocked.Read(ref DownloadEngine.GlobalBytesDownloaded);

            // ---------- 1) 版本 json ----------
            progress.Report(new DownloadProgress { State = "正在获取版本配置...", Percentage = 2 });

            string jsonPath = Path.Combine(targetVersionDir, version.VersionId + ".json");
            var jsonDownload = await DownloadOneAsync(
                new FileTask
                {
                    OfficialUrl = version.Url,
                    Kind = DownloadSourceKind.Meta,
                    LocalPath = jsonPath,
                    DisplayPath = version.VersionId + ".json"
                },
                gate: null, ct).ConfigureAwait(false);

            if (!jsonDownload.ok)
                throw new InvalidOperationException($"下载版本 JSON 失败：{jsonDownload.error}");

            var vj = MinecraftVersionJson.Load(jsonPath);
            if (vj == null) throw new InvalidOperationException("版本 JSON 解析失败");

            // ---------- 2) 收集文件清单 ----------
            progress.Report(new DownloadProgress { State = "正在解析文件清单...", Percentage = 5 });

            var tasks = new List<FileTask>();

            // client.jar（走 piston-data，官方 CDN 实测 6.9MB/s）
            if (vj.Downloads?.Client != null && !string.IsNullOrEmpty(vj.Downloads.Client.Url))
            {
                tasks.Add(new FileTask
                {
                    OfficialUrl = vj.Downloads.Client.Url,
                    Kind = DownloadSourceKind.Meta,
                    LocalPath = Path.Combine(targetVersionDir, version.VersionId + ".jar"),
                    DisplayPath = version.VersionId + ".jar",
                    Sha1 = vj.Downloads.Client.Sha1,
                    Size = vj.Downloads.Client.Size
                });
            }

            // libraries
            foreach (var lib in vj.Libraries)
            {
                if (!IsLibraryAllowed(lib)) continue;
                var artifact = lib.Downloads?.Artifact;
                if (artifact == null || string.IsNullOrEmpty(artifact.Path) || string.IsNullOrEmpty(artifact.Url))
                    continue;

                tasks.Add(new FileTask
                {
                    OfficialUrl = artifact.Url,
                    Kind = DownloadSourceKind.Maven,
                    LocalPath = Path.Combine(targetLibrariesDir, artifact.Path.Replace('/', Path.DirectorySeparatorChar)),
                    DisplayPath = artifact.Path,
                    Sha1 = artifact.Sha1,
                    Size = artifact.Size
                });
            }

            // ---------- 3) asset index（必须先下，才能展开 objects） ----------
            if (vj.AssetIndex != null && !string.IsNullOrEmpty(vj.AssetIndex.Url))
            {
                string indexPath = Path.Combine(targetAssetsDir, "indexes", vj.AssetIndex.Id + ".json");

                var indexDownload = await DownloadOneAsync(
                    new FileTask
                    {
                        OfficialUrl = vj.AssetIndex.Url,
                        Kind = DownloadSourceKind.Meta,
                        LocalPath = indexPath,
                        DisplayPath = "assets/indexes/" + vj.AssetIndex.Id + ".json",
                        Sha1 = vj.AssetIndex.Sha1
                    },
                    gate: null, ct).ConfigureAwait(false);

                if (indexDownload.ok && File.Exists(indexPath))
                {
                    foreach (var asset in await ParseAssetObjectsAsync(indexPath, targetAssetsDir).ConfigureAwait(false))
                        tasks.Add(asset);
                }
                else
                {
                    LogHelper.Write(LauncherPaths.AppLog,
                        $"asset index 下载失败: {indexDownload.error}",
                        $"asset index download failed: {indexDownload.error}");
                }
            }

            result.TotalFiles = tasks.Count;
            result.TotalBytes = tasks.Sum(t => t.Size);

            LogHelper.Write(LauncherPaths.AppLog,
                $"开始下载 {version.VersionId}：{tasks.Count} 个文件，约 {result.TotalBytes / 1024 / 1024} MB，" +
                $"并发 {maxConcurrency}，源偏好 {SourcePolicy.Preference}",
                $"Start install {version.VersionId}: {tasks.Count} files, {result.TotalBytes / 1024 / 1024} MB, " +
                $"concurrency {maxConcurrency}, preference {SourcePolicy.Preference}");

            // ---------- 4) 并行下载 ----------
            using var gate = new SemaphoreSlim(maxConcurrency, maxConcurrency);

            int completed = 0, failed = 0, skipped = 0;
            long skippedBytes = 0;
            var failures = new ConcurrentQueue<string>();
            var failureKinds = new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            int totalFiles = tasks.Count;
            long totalBytes = result.TotalBytes;

            // 进度/测速线程
            using var progressCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var progressTask = Task.Run(async () =>
            {
                long lastBytes = 0;
                while (!progressCts.IsCancellationRequested)
                {
                    try { await Task.Delay(500, progressCts.Token).ConfigureAwait(false); }
                    catch (OperationCanceledException) { break; }

                    long current = Interlocked.Read(ref DownloadEngine.GlobalBytesDownloaded) - startBytes;
                    if (current < 0) current = 0;
                    long speed = (current - lastBytes) * 2;
                    lastBytes = current;

                    int done = Volatile.Read(ref completed);
                    int bad = Volatile.Read(ref failed);
                    long doneBytes = current + Interlocked.Read(ref skippedBytes);

                    double percent = totalBytes > 0
                        ? 10 + Math.Min(1.0, (double)doneBytes / totalBytes) * 89
                        : 10 + (double)done / Math.Max(1, totalFiles) * 89;

                    var snapshot = new DownloadProgress
                    {
                        State = bad > 0 ? $"正在下载（失败 {bad} 个）..." : "正在下载游戏文件...",
                        Percentage = Math.Min(99, percent),
                        SpeedBytesPerSec = speed,
                        TotalFiles = totalFiles,
                        CompletedFiles = done,
                        FailedFiles = bad
                    };

                    try { progress.Report(snapshot); } catch { }
                }
            }, progressCts.Token);

            // 逐文件任务
            var fileTasks = tasks.Select(task => Task.Run(async () =>
            {
                ct.ThrowIfCancellationRequested();

                // 已存在且校验通过 → 跳过（PCL 的本地复用）
                if (File.Exists(task.LocalPath))
                {
                    bool ok = string.IsNullOrEmpty(task.Sha1)
                              || await DownloadEngine.VerifySha1Async(task.LocalPath, task.Sha1, ct).ConfigureAwait(false);
                    if (ok)
                    {
                        Interlocked.Add(ref skippedBytes, task.Size);
                        Interlocked.Increment(ref skipped);
                        Interlocked.Increment(ref completed);
                        return;
                    }

                    try { File.Delete(task.LocalPath); } catch { }
                }

                var (ok2, error) = await DownloadOneAsync(task, gate, ct).ConfigureAwait(false);
                if (ok2)
                {
                    Interlocked.Increment(ref completed);
                }
                else
                {
                    Interlocked.Increment(ref failed);
                    string kindKey = ClassifyFailureText(error);
                    failureKinds.AddOrUpdate(kindKey, 1, (_, v) => v + 1);
                    if (failures.Count < MaxFailureLogLines)
                        failures.Enqueue($"{task.DisplayPath} <- {error}");
                }
            }, ct)).ToArray();

            await Task.WhenAll(fileTasks).ConfigureAwait(false);
            progressCts.Cancel();
            try { await progressTask.ConfigureAwait(false); } catch { }

            // ---------- 5) 汇总 ----------
            result.CompletedFiles = Volatile.Read(ref completed);
            result.FailedFiles = Volatile.Read(ref failed);
            result.SkippedFiles = Volatile.Read(ref skipped);
            result.DownloadedBytes = Math.Max(0,
                Interlocked.Read(ref DownloadEngine.GlobalBytesDownloaded) - startBytes + Volatile.Read(ref skippedBytes));

            // 失败日志
            if (result.FailedFiles > 0)
            {
                LogHelper.Write(LauncherPaths.AppLog,
                    $"下载结束但仍有 {result.FailedFiles} 个文件失败。失败类型分布：" +
                    string.Join("，", failureKinds.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key}×{kv.Value}")),
                    $"Install finished with {result.FailedFiles} failed files. Breakdown: " +
                    string.Join(", ", failureKinds.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key}x{kv.Value}")));

                int i = 0;
                foreach (var line in failures)
                {
                    LogHelper.Write(LauncherPaths.AppLog, $"失败文件: {line}", $"Failed file: {line}");
                    if (++i >= MaxFailureLogLines) break;
                }

                LogHelper.Write(LauncherPaths.AppLog,
                    $"源健康状态: {SourcePolicy.DescribeHealth()}",
                    $"Source health: {SourcePolicy.DescribeHealth()}");
            }

            progress.Report(new DownloadProgress
            {
                State = result.FailedFiles == 0 ? "下载完成" : $"部分文件失败（{result.FailedFiles} 个）",
                Percentage = 100,
                TotalFiles = totalFiles,
                CompletedFiles = result.CompletedFiles,
                FailedFiles = result.FailedFiles
            });

            LogHelper.Write(LauncherPaths.AppLog,
                $"下载结束：{result.Summary}",
                $"Install done: {result.Summary}");

            return result;
        }

        // ============================================================
        // 单文件：源降级 + 换源
        // ============================================================

        private static async Task<(bool ok, string error)> DownloadOneAsync(
            FileTask task,
            SemaphoreSlim? gate,
            CancellationToken ct)
        {
            string lastError = "";

            // 两轮：第二轮是 PCL 的「全源降级重试一次」
            for (int round = 0; round < 2; round++)
            {
                var candidates = SourcePolicy.GetCandidates(task.OfficialUrl, task.Kind);
                bool anyUsable = candidates.Any(s => !SourcePolicy.IsCoolingDown(s));

                foreach (var source in candidates)
                {
                    // 冷却中的源跳过（除非所有源都在冷却，此时被迫都试一遍）
                    if (anyUsable && SourcePolicy.IsCoolingDown(source)) continue;

                    for (int attempt = 0; attempt < PerSourceAttempts; attempt++)
                    {
                        ct.ThrowIfCancellationRequested();

                        DownloadAttemptResult attemptResult;

                        if (gate != null) await gate.WaitAsync(ct).ConfigureAwait(false);
                        try
                        {
                            using (await SourcePolicy.AcquireAsync(source, ct).ConfigureAwait(false))
                            {
                                attemptResult = await DownloadEngine
                                    .DownloadAsync(source.Url, task.LocalPath, task.Sha1, ct)
                                    .ConfigureAwait(false);
                            }
                        }
                        finally
                        {
                            gate?.Release();
                        }

                        if (attemptResult.IsSuccess)
                        {
                            SourcePolicy.ReportSuccess(source);
                            return (true, "");
                        }

                        SourcePolicy.ReportFailure(source, attemptResult.Kind, attemptResult.RetryAfter);
                        lastError = $"{source.Id}: {attemptResult.Kind} {attemptResult.Message}";

                        // 磁盘错误：换源也没用，直接失败
                        if (attemptResult.Kind == DownloadFailureKind.Fatal)
                            return (false, lastError);

                        // 可恢复的传输错误：同源再试一次
                        if (attemptResult.Kind == DownloadFailureKind.Transient)
                            continue;

                        // 失速 / 404 / 403 / 429 / DNS / 校验失败：立刻换源，不在坏源上耗时间
                        break;
                    }
                }
            }

            return (false, string.IsNullOrEmpty(lastError) ? "所有下载源均失败" : lastError);
        }

        // ============================================================
        // assets 解析
        // ============================================================

        private static async Task<List<FileTask>> ParseAssetObjectsAsync(string indexPath, string assetsDir)
        {
            var list = new List<FileTask>();
            string objectsDir = Path.Combine(assetsDir, "objects");

            string json = await File.ReadAllTextAsync(indexPath).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("objects", out var objects)) return list;

            foreach (var prop in objects.EnumerateObject())
            {
                if (!prop.Value.TryGetProperty("hash", out var hashEl)) continue;
                string? hash = hashEl.GetString();
                if (string.IsNullOrEmpty(hash) || hash.Length < 2) continue;

                long size = 0;
                if (prop.Value.TryGetProperty("size", out var sizeEl) && sizeEl.TryGetInt64(out long s))
                    size = s;

                string prefix = hash.Substring(0, 2);

                list.Add(new FileTask
                {
                    // 统一以官方 URL 记录，镜像由 SourcePolicy 映射
                    OfficialUrl = $"https://resources.download.minecraft.net/{prefix}/{hash}",
                    Kind = DownloadSourceKind.Assets,
                    LocalPath = Path.Combine(objectsDir, prefix, hash),
                    DisplayPath = $"assets/objects/{prefix}/{hash}",
                    Sha1 = hash,
                    Size = size
                });
            }

            return list;
        }

        // ============================================================
        // 工具
        // ============================================================

        private static bool IsLibraryAllowed(LibraryEntry lib)
        {
            if (lib.Rules == null || lib.Rules.Count == 0) return true;

            bool allowed = false;
            foreach (var rule in lib.Rules)
            {
                bool matches = true;
                if (rule.Os != null && !string.IsNullOrEmpty(rule.Os.Name))
                {
                    if (!rule.Os.Name.Equals("windows", StringComparison.OrdinalIgnoreCase))
                        matches = false;
                }

                if (matches)
                {
                    if (rule.Action == "allow") allowed = true;
                    else if (rule.Action == "disallow") return false;
                }
            }

            return allowed;
        }

        private static string ClassifyFailureText(string error)
        {
            if (string.IsNullOrEmpty(error)) return "未知";

            int idx = error.IndexOf(':');
            string prefix = idx > 0 ? error.Substring(0, idx).Trim() : "?";
            string rest = idx > 0 ? error.Substring(idx + 1).Trim() : error;

            int space = rest.IndexOf(' ');
            string kind = space > 0 ? rest.Substring(0, space) : rest;

            return $"{prefix}/{kind}";
        }
    }
}
