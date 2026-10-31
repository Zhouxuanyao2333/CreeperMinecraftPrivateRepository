using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Project.Launch.Download
{
    /// <summary>
    /// 下载资源类别 —— 决定官方/镜像的 URL 映射方式与源健康度分桶。
    /// </summary>
    public enum DownloadSourceKind
    {
        /// <summary>assets/objects（resources.download.minecraft.net）</summary>
        Assets,
        /// <summary>libraries（libraries.minecraft.net）</summary>
        Maven,
        /// <summary>元数据：版本清单 / 版本 json / asset index / client.jar（piston-meta、piston-data）</summary>
        Meta
    }

    /// <summary>
    /// 源偏好，对齐 PCL 的 ToolDownloadSource：自动 / 镜像优先 / 官方优先。
    /// </summary>
    public enum SourcePreference
    {
        /// <summary>官方优先；官方失效（连续失败进入冷却）后自动切镜像。</summary>
        Auto = 0,
        MirrorFirst = 1,
        OfficialFirst = 2
    }

    /// <summary>
    /// 一个具体的下载源（同一个文件最多两个：官方 / 镜像）。
    /// </summary>
    public sealed class DownloadSource
    {
        public required string Id { get; init; }
        public required string Url { get; init; }
        public required bool IsMirror { get; init; }
        public required DownloadSourceKind Kind { get; init; }

        /// <summary>健康度分桶键：镜像/官方 × 资源类别（maven 的镜像和 assets 的镜像质量完全不同，必须分开算）。</summary>
        public string HealthKey => Id + ":" + Kind;

        public override string ToString() => Id;
    }

    /// <summary>
    /// 下载源策略 —— PCL 的 "源失效降级 + 冷却" 的最小可用实现。
    ///
    /// 背景（实测结论）：BMCLAPI 的 assets 现在会 302 到一个「每请求随机分配」的节点池，
    /// 池中约半数节点 NXDOMAIN / TLS RST / 25s 不回字节，单连接有效速率 0.6~63 KB/s；
    /// 而官方源同批文件 320~5957 KB/s 且从不截断（差 30~50 倍）。
    /// 所以默认 Auto = 官方优先，同时用健康度把任何「正在坏掉的源」快速冷却掉。
    /// </summary>
    public static class SourcePolicy
    {
        // ============================================================
        // 可调参数
        // ============================================================

        public static SourcePreference Preference { get; set; } = SourcePreference.Auto;

        /// <summary>镜像同时并发上限。PCL 对 bmclapi 的做法是「禁止追加连接 + 每次启动间隔 100ms」，
        /// 这里折算成主机级并发上限 8，避免把唯一国内镜像打成 403/429。</summary>
        public const int MirrorConcurrency = 8;

        /// <summary>官方源并发上限（实测 c=50 仍稳定 1160 KB/s，不需要压太低）。</summary>
        public const int OfficialConcurrency = 32;

        /// <summary>镜像连续失败多少次进入冷却。</summary>
        private const int MirrorFailThreshold = 2;
        /// <summary>官方连续失败多少次进入冷却（官方通常可靠，门槛放宽一点）。</summary>
        private const int OfficialFailThreshold = 3;

        private static readonly TimeSpan MirrorCooldown = TimeSpan.FromSeconds(60);
        private static readonly TimeSpan OfficialCooldown = TimeSpan.FromSeconds(20);
        private static readonly TimeSpan MaxCooldown = TimeSpan.FromMinutes(5);

        // ============================================================
        // 内部状态
        // ============================================================

        private sealed class Health
        {
            public int FailCount;
            public long CooldownUntilMs; // Environment.TickCount64
        }

        private static readonly ConcurrentDictionary<string, Health> _health =
            new(StringComparer.OrdinalIgnoreCase);

        private static readonly ConcurrentDictionary<string, SemaphoreSlim> _gates =
            new(StringComparer.OrdinalIgnoreCase);

        // ============================================================
        // URL 映射
        // ============================================================

        /// <summary>官方 URL → 镜像 URL（BMCLAPI）。没有对应镜像时原样返回。</summary>
        public static string ToMirror(string officialUrl)
        {
            if (string.IsNullOrEmpty(officialUrl)) return officialUrl;

            return officialUrl
                .Replace("https://resources.download.minecraft.net", "https://bmclapi2.bangbang93.com/assets", StringComparison.OrdinalIgnoreCase)
                .Replace("https://libraries.minecraft.net", "https://bmclapi2.bangbang93.com/maven", StringComparison.OrdinalIgnoreCase)
                .Replace("https://piston-meta.mojang.com", "https://bmclapi2.bangbang93.com", StringComparison.OrdinalIgnoreCase)
                .Replace("https://piston-data.mojang.com", "https://bmclapi2.bangbang93.com", StringComparison.OrdinalIgnoreCase)
                .Replace("https://launchermeta.mojang.com", "https://bmclapi2.bangbang93.com", StringComparison.OrdinalIgnoreCase)
                .Replace("https://launcher.mojang.com", "https://bmclapi2.bangbang93.com", StringComparison.OrdinalIgnoreCase);
        }

        public static string ToOfficial(string url)
        {
            if (string.IsNullOrEmpty(url)) return url;

            return url
                .Replace("https://bmclapi2.bangbang93.com/assets", "https://resources.download.minecraft.net", StringComparison.OrdinalIgnoreCase)
                .Replace("https://bmclapi2.bangbang93.com/maven", "https://libraries.minecraft.net", StringComparison.OrdinalIgnoreCase)
                .Replace("https://bmclapi2.bangbang93.com", "https://piston-data.mojang.com", StringComparison.OrdinalIgnoreCase);
        }

        // ============================================================
        // 候选源排序
        // ============================================================

        /// <summary>
        /// 给出该文件的候选源顺序。冷却中的源排到最后，但仍保留 ——
        /// 避免「两个源都在冷却」时出现无源可用（PCL 的 SourcesOnce 全量重试语义）。
        /// </summary>
        public static IReadOnlyList<DownloadSource> GetCandidates(string officialUrl, DownloadSourceKind kind)
        {
            var official = new DownloadSource
            {
                Id = "official",
                Url = officialUrl,
                IsMirror = false,
                Kind = kind
            };

            string mirrorUrl = ToMirror(officialUrl);
            var list = new List<DownloadSource>(2);

            bool hasMirror = !string.Equals(mirrorUrl, officialUrl, StringComparison.OrdinalIgnoreCase);
            DownloadSource? mirror = null;
            if (hasMirror)
            {
                mirror = new DownloadSource
                {
                    Id = "mirror",
                    Url = mirrorUrl,
                    IsMirror = true,
                    Kind = kind
                };
            }

            bool mirrorFirst;
            switch (Preference)
            {
                case SourcePreference.MirrorFirst:
                    mirrorFirst = hasMirror;
                    break;
                case SourcePreference.OfficialFirst:
                    mirrorFirst = false;
                    break;
                default: // Auto：官方优先，官方冷却中才让镜像先上
                    mirrorFirst = hasMirror && IsCoolingDown(official);
                    break;
            }

            if (mirrorFirst && mirror != null)
            {
                list.Add(mirror);
                list.Add(official);
            }
            else
            {
                list.Add(official);
                if (mirror != null) list.Add(mirror);
            }

            // 冷却中的源沉底（Stable 排序保持其余顺序）
            return list
                .OrderBy(s => IsCoolingDown(s) ? 1 : 0)
                .ToList();
        }

        /// <summary>是否存在镜像。</summary>
        public static bool HasMirror(string officialUrl) =>
            !string.Equals(ToMirror(officialUrl), officialUrl, StringComparison.OrdinalIgnoreCase);

        // ============================================================
        // 健康度
        // ============================================================

        public static bool IsCoolingDown(DownloadSource source)
        {
            if (!_health.TryGetValue(source.HealthKey, out var h)) return false;
            long until = Volatile.Read(ref h.CooldownUntilMs);
            return until != 0 && Environment.TickCount64 < until;
        }

        /// <summary>收到字节即清零失败计数（对齐 PCL：区分「慢但能下」与「连不上」）。</summary>
        public static void ReportSuccess(DownloadSource source)
        {
            if (_health.TryGetValue(source.HealthKey, out var h))
            {
                Interlocked.Exchange(ref h.FailCount, 0);
                Volatile.Write(ref h.CooldownUntilMs, 0);
            }
        }

        public static void ReportFailure(DownloadSource source, DownloadFailureKind kind, TimeSpan? retryAfter)
        {
            var h = _health.GetOrAdd(source.HealthKey, _ => new Health());
            int fails = Interlocked.Increment(ref h.FailCount);

            // 429：按服务端 Retry-After 冷却，绝不原地重试（PCL 明确不把 bmclapi 的 403/429 当源坏，
            // 但我们的问题是「重试风暴」把 60s 的 per-URL 配额打满，所以这里必须冷却）。
            if (kind == DownloadFailureKind.RateLimited)
            {
                TimeSpan cd = retryAfter ?? TimeSpan.FromSeconds(60);
                if (cd < TimeSpan.FromSeconds(10)) cd = TimeSpan.FromSeconds(10);
                if (cd > MaxCooldown) cd = MaxCooldown;
                Cooldown(source, cd);
                return;
            }

            // 单文件级别的错误不惩罚整个源
            if (kind == DownloadFailureKind.NotFound || kind == DownloadFailureKind.Forbidden)
                return;

            // 磁盘错误直接判死，与源无关
            if (kind == DownloadFailureKind.Fatal)
                return;

            int threshold = source.IsMirror ? MirrorFailThreshold : OfficialFailThreshold;
            if (fails >= threshold)
            {
                // 连续失败越多，冷却越久（最多 5 分钟）
                int multiplier = Math.Min(4, fails - threshold + 1);
                TimeSpan baseCd = source.IsMirror ? MirrorCooldown : OfficialCooldown;
                Cooldown(source, TimeSpan.FromMilliseconds(baseCd.TotalMilliseconds * multiplier));
            }
        }

        private static void Cooldown(DownloadSource source, TimeSpan duration)
        {
            var h = _health.GetOrAdd(source.HealthKey, _ => new Health());
            long until = Environment.TickCount64 + (long)duration.TotalMilliseconds;
            Volatile.Write(ref h.CooldownUntilMs, until);
        }

        // ============================================================
        // 主机级并发门（PCL 的全局连接数上限 / CE 的 per-host 配额）
        // ============================================================

        public static async Task<IDisposable> AcquireAsync(DownloadSource source, CancellationToken ct)
        {
            var sem = _gates.GetOrAdd(
                source.Id,
                id => new SemaphoreSlim(id == "mirror" ? MirrorConcurrency : OfficialConcurrency,
                                        id == "mirror" ? MirrorConcurrency : OfficialConcurrency));

            await sem.WaitAsync(ct).ConfigureAwait(false);
            return new GateReleaser(sem);
        }

        private sealed class GateReleaser : IDisposable
        {
            private SemaphoreSlim? _sem;

            public GateReleaser(SemaphoreSlim sem) => _sem = sem;

            public void Dispose()
            {
                SemaphoreSlim? sem = Interlocked.Exchange(ref _sem, null);
                sem?.Release();
            }
        }

        // ============================================================
        // 生命周期
        // ============================================================

        /// <summary>每次开始安装时清空健康度（冷却与失败计数），但保留并发门。</summary>
        public static void Reset()
        {
            _health.Clear();
        }

        /// <summary>诊断用：当前各源的健康状态。</summary>
        public static string DescribeHealth()
        {
            if (_health.IsEmpty) return "全部源健康";

            return string.Join("; ", _health
                .Select(kv =>
                {
                    long until = Volatile.Read(ref kv.Value.CooldownUntilMs);
                    string cd = until != 0 && Environment.TickCount64 < until
                        ? $"冷却剩余 {(until - Environment.TickCount64) / 1000}s"
                        : "正常";
                    return $"{kv.Key}: 连续失败 {Volatile.Read(ref kv.Value.FailCount)}，{cd}";
                })
                .OrderBy(s => s));
        }
    }
}
