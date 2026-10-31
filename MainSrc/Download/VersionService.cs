using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Project.Launch.Tools.JavaConfig;
using Project.Launch.Tools.LogsConfig;

namespace Project.Launch.Download
{
    /// <summary>
    /// 版本清单获取 —— 走 SourcePolicy 的源顺序与健康度：
    /// 某个源超时/DNS 失败后会被冷却，下一次直接换另一个源，而不是每次都先等 15s。
    /// </summary>
    public static class VersionService
    {
        private const string OfficialManifestUrl =
            "https://piston-meta.mojang.com/mc/game/version_manifest_v2.json";

        /// <summary>单个源的单次尝试超时（PCL：官方优先时官方只给 5s，避免长等）。</summary>
        private static readonly TimeSpan AttemptTimeout = TimeSpan.FromSeconds(10);

        /// <summary>请求一次；若是系统代理端点不可达，则自动改直连再试一次。</summary>
        private static async Task<string> FetchAsync(string url, CancellationToken ct)
        {
            try
            {
                return await FetchOnceAsync(url, ct).ConfigureAwait(false);
            }
            catch (HttpRequestException ex) when (ProxyEnvironment.LooksLikeDeadProxy(ex.Message))
            {
                ProxyEnvironment.DisableAfterFailure(
                    $"版本清单请求命中系统代理端点（{ex.Message}），改用直连重试");
                return await FetchOnceAsync(url, ct).ConfigureAwait(false);
            }
        }

        private static async Task<string> FetchOnceAsync(string url, CancellationToken ct)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(AttemptTimeout);
            return await DownloadEngine.SharedClient.GetStringAsync(url, cts.Token).ConfigureAwait(false);
        }

        public static async Task<VersionManifest?> GetVersionManifestAsync(CancellationToken ct = default)
        {
            var candidates = SourcePolicy.GetCandidates(OfficialManifestUrl, DownloadSourceKind.Meta);

            foreach (var source in candidates)
            {
                if (SourcePolicy.IsCoolingDown(source))
                {
                    LogHelper.Write(LauncherPaths.AppLog,
                        $"跳过冷却中的源：{source.Id}（{source.Url}）",
                        $"Skip cooling source: {source.Id} ({source.Url})");
                    continue;
                }

                try
                {
                    string json = await FetchAsync(source.Url, ct).ConfigureAwait(false);

                    var manifest = JsonSerializer.Deserialize<VersionManifest>(
                        json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (manifest != null && manifest.Versions.Count > 0)
                    {
                        SourcePolicy.ReportSuccess(source);
                        LogHelper.Write(LauncherPaths.AppLog,
                            $"成功获取版本清单（{source.Id}：{source.Url}），共 {manifest.Versions.Count} 个版本",
                            $"Fetched manifest from {source.Id} ({source.Url}), count: {manifest.Versions.Count}");
                        return manifest;
                    }

                    SourcePolicy.ReportFailure(source, DownloadFailureKind.Transient, null);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    SourcePolicy.ReportFailure(source, DownloadFailureKind.Stall, null);
                    LogHelper.Write(LauncherPaths.AppLog,
                        $"获取版本清单超时（{source.Id}：{source.Url}，{AttemptTimeout.TotalSeconds:0}s）",
                        $"Manifest timeout from {source.Id} ({source.Url}), {AttemptTimeout.TotalSeconds:0}s");
                }
                catch (HttpRequestException ex)
                {
                    var kind = ex.HttpRequestError == HttpRequestError.NameResolutionError
                        ? DownloadFailureKind.Dns
                        : DownloadFailureKind.Transient;

                    SourcePolicy.ReportFailure(source, kind, null);
                    LogHelper.Write(LauncherPaths.AppLog,
                        $"获取版本清单失败（{source.Id}：{source.Url}）: {ex.Message}",
                        $"Failed to fetch manifest from {source.Id} ({source.Url}): {ex.Message}");
                }
                catch (Exception ex)
                {
                    SourcePolicy.ReportFailure(source, DownloadFailureKind.Transient, null);
                    LogHelper.Write(LauncherPaths.AppLog,
                        $"获取版本清单异常（{source.Id}）: {ex.Message}",
                        $"Manifest exception from {source.Id}: {ex.Message}");
                }
            }

            LogHelper.Write(LauncherPaths.AppLog,
                $"所有源均无法获取版本清单。源状态：{SourcePolicy.DescribeHealth()}",
                $"All sources failed. Health: {SourcePolicy.DescribeHealth()}");

            return null;
        }
    }
}
