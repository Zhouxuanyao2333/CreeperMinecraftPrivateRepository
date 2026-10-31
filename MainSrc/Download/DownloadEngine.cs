using System;
using System.Buffers;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Project.Launch.Download
{
    /// <summary>
    /// 下载失败类型 —— 决定「换源 / 重试 / 冷却 / 直接失败」。
    /// 分类依据：HTTP 语义 + 网络错误类型 + 磁盘错误，避免"重试不该重试的错误"。
    /// </summary>
    public enum DownloadFailureKind
    {
        None = 0,
        /// <summary>404 / 410：本文件在该源不存在，立刻换源，不在本 URL 重试。</summary>
        NotFound,
        /// <summary>401 / 403 / 451：立刻换源。</summary>
        Forbidden,
        /// <summary>429：按 Retry-After 冷却该源，禁止原地重试。</summary>
        RateLimited,
        /// <summary>5xx / 连接中断 / 读写异常：同源退避重试一次后换源。</summary>
        Transient,
        /// <summary>SHA1 校验失败：换源重下。</summary>
        Corrupt,
        /// <summary>建连超时 / 逐读无数据 / 速度低于下限：失速，立刻换源。</summary>
        Stall,
        /// <summary>DNS 解析失败（NXDOMAIN）：立刻换源。</summary>
        Dns,
        /// <summary>磁盘写入失败 / 空间不足：换源也没用，直接失败。</summary>
        Fatal
    }

    /// <summary>单次下载尝试的结果。</summary>
    public sealed class DownloadAttemptResult
    {
        public bool IsSuccess { get; private init; }
        public DownloadFailureKind Kind { get; private init; }
        public TimeSpan? RetryAfter { get; private init; }
        public string Message { get; private init; } = "";
        public long BytesReceived { get; private init; }

        public static DownloadAttemptResult Ok(long bytes) => new()
        {
            IsSuccess = true,
            Kind = DownloadFailureKind.None,
            BytesReceived = bytes
        };

        public static DownloadAttemptResult Fail(
            DownloadFailureKind kind, string message, TimeSpan? retryAfter = null, long bytes = 0) => new()
        {
            IsSuccess = false,
            Kind = kind,
            Message = message,
            RetryAfter = retryAfter,
            BytesReceived = bytes
        };

        public override string ToString() =>
            IsSuccess ? $"OK ({BytesReceived:N0} B)" : $"{Kind}: {Message}";
    }

    /// <summary>
    /// 下载原语。核心是 PCL 的三件套：
    ///   1) 建连/首字节超时（8s）
    ///   2) 逐读看门狗（每次 ReadAsync 单独 15s 超时）
    ///   3) 失速判据（10s 窗口内低于 24KB/s 即掐断，比常见的 1KB/s 判据更严格）
    /// 另外：小文件内存直写（PCL IsNoSplit &lt;1MB / CE &lt;4MB），边下边算 SHA1（省掉整文件回读）。
    /// </summary>
    public static class DownloadEngine
    {
        /// <summary>累计已成功接收的字节（失败尝试会回滚，避免「重试重复计入」导致测速虚高）。</summary>
        public static long GlobalBytesDownloaded;

        /// <summary>小于该阈值走内存缓冲，一次落盘（PCL-CE SmallFileThreshold = 4MB）。</summary>
        private const int SmallFileThreshold = 4 * 1024 * 1024;

        private const int BufferSize = 81920;

        /// <summary>建连 + 等响应头超时。</summary>
        private const int ConnectTimeoutMs = 8000;

        /// <summary>逐读无数据超时（PCL 的动态超时基线 = max(平均建连, 15s)）。</summary>
        private const int ReadIdleTimeoutMs = 15000;

        /// <summary>速度下限：低于它就判定失速换源（实测镜像涓流 0.6~63 KB/s）。</summary>
        private const long MinSpeedBytesPerSec = 24 * 1024;

        /// <summary>测速判失速的窗口。</summary>
        private const int SpeedWindowMs = 10000;

        private const long MaxAttemptBytes = 4L * 1024 * 1024 * 1024; // 4GB 兜底

        private static readonly HttpClient _http = CreateClient();

        /// <summary>全项目共用的 HttpClient（元数据请求也复用它：共享连接池与代理策略）。</summary>
        public static HttpClient SharedClient => _http;

        private static HttpClient CreateClient()
        {
            // 启动时先探测系统代理是否可用（代理客户端没开时直接走直连）
            ProxyEnvironment.EnsureInitialized();

            var handler = new SocketsHttpHandler
            {
                AutomaticDecompression = DecompressionMethods.All,
                MaxConnectionsPerServer = 256,
                PooledConnectionLifetime = TimeSpan.FromMinutes(15),
                ConnectTimeout = TimeSpan.FromSeconds(8),
                EnableMultipleHttp2Connections = true,
                // 每个请求实时询问 ProxyEnvironment：代理挂掉可立即切直连
                UseProxy = true,
                Proxy = new AdaptiveProxy(),
                // 保持默认 HTTP/1.1：实测强制 h2 对「每文件换 host」的 BMCLAPI 零收益
            };

            return new HttpClient(handler)
            {
                // 总超时交给逐读看门狗，不用全局 Timeout（PCL 也是按读超时，不用整体超时）
                Timeout = Timeout.InfiniteTimeSpan
            };
        }

        // ============================================================
        // 公开入口
        // ============================================================

        public static async Task<DownloadAttemptResult> DownloadAsync(
            string url,
            string localPath,
            string? expectedSha1,
            CancellationToken ct)
        {
            DownloadAttemptResult result = await AttemptAsync(url, localPath, expectedSha1, ct)
                .ConfigureAwait(false);

            // 系统代理端点连不上（例如代理客户端被关掉但注册表还开着）→ 立刻切直连重试一次
            if (!result.IsSuccess && ProxyEnvironment.LooksLikeDeadProxy(result.Message))
            {
                ProxyEnvironment.DisableAfterFailure(
                    $"请求命中系统代理端点（{result.Message}），已改用直连重试");

                result = await AttemptAsync(url, localPath, expectedSha1, ct).ConfigureAwait(false);
            }

            return result;
        }

        /// <summary>一次完整尝试。失败时回滚本次已计入的字节，避免测速虚高。</summary>
        private static async Task<DownloadAttemptResult> AttemptAsync(
            string url,
            string localPath,
            string? expectedSha1,
            CancellationToken ct)
        {
            DownloadAttemptResult result = await DownloadCoreAsync(url, localPath, expectedSha1, ct)
                .ConfigureAwait(false);

            if (!result.IsSuccess && result.BytesReceived > 0)
                Interlocked.Add(ref GlobalBytesDownloaded, -result.BytesReceived);

            return result;
        }

        /// <summary>校验本地已有文件（PCL 的「已有本地文件复用」）。</summary>
        public static async Task<bool> VerifySha1Async(string filePath, string expectedSha1, CancellationToken ct = default)
        {
            try
            {
                if (!File.Exists(filePath)) return false;
                using var sha1 = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
                await using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read,
                    81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
                byte[] buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
                try
                {
                    int read;
                    while ((read = await fs.ReadAsync(buffer.AsMemory(0, buffer.Length), ct).ConfigureAwait(false)) > 0)
                        sha1.AppendData(buffer, 0, read);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }

                string actual = Convert.ToHexString(sha1.GetHashAndReset()).ToLowerInvariant();
                return actual.Equals(expectedSha1, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        // ============================================================
        // 核心实现
        // ============================================================

        private static async Task<DownloadAttemptResult> DownloadCoreAsync(
            string url,
            string localPath,
            string? expectedSha1,
            CancellationToken ct)
        {
            long received = 0;
            string? tempPath = null;
            byte[]? buffer = null;
            Stream? dest = null;
            bool committed = false;

            try
            {
                string? dir = Path.GetDirectoryName(localPath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                // ---------- 1) 请求（建连/首字节看门狗） ----------
                HttpResponseMessage response;
                using (var connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
                {
                    connectCts.CancelAfter(ConnectTimeoutMs);
                    try
                    {
                        response = await _http
                            .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, connectCts.Token)
                            .ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                    {
                        return DownloadAttemptResult.Fail(DownloadFailureKind.Stall,
                            $"建连/首字节超时（{ConnectTimeoutMs}ms）");
                    }
                    catch (HttpRequestException ex)
                    {
                        return ClassifyHttpException(ex);
                    }
                }

                using (response)
                {
                    // ---------- 2) 状态码分类 ----------
                    var status = (int)response.StatusCode;

                    if (status == 404 || status == 410)
                        return DownloadAttemptResult.Fail(DownloadFailureKind.NotFound, $"HTTP {status}");

                    if (status == 401 || status == 403 || status == 451)
                        return DownloadAttemptResult.Fail(DownloadFailureKind.Forbidden, $"HTTP {status}");

                    if (status == 429)
                        return DownloadAttemptResult.Fail(DownloadFailureKind.RateLimited,
                            "HTTP 429（限流）", ParseRetryAfter(response));

                    if (status >= 500)
                        return DownloadAttemptResult.Fail(DownloadFailureKind.Transient, $"HTTP {status}");

                    if (!response.IsSuccessStatusCode)
                        return DownloadAttemptResult.Fail(DownloadFailureKind.Transient, $"HTTP {status}");

                    // ---------- 3) 选择缓冲：小文件进内存，大文件进 .tmp ----------
                    long? contentLength = response.Content.Headers.ContentLength;
                    bool useMemory = contentLength is null || contentLength.Value <= SmallFileThreshold;

                    if (useMemory)
                    {
                        dest = new MemoryStream(contentLength is > 0 and <= int.MaxValue
                            ? (int)contentLength.Value
                            : 0);
                    }
                    else
                    {
                        tempPath = localPath + ".tmp";
                        dest = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None,
                            BufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);
                    }

                    // ---------- 4) 读循环（逐读看门狗 + 失速判据 + 边下边算 SHA1） ----------
                    using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);

                    Stream stream;
                    try
                    {
                        stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                    }
                    catch (HttpRequestException ex)
                    {
                        return ClassifyHttpException(ex, received);
                    }

                    buffer = ArrayPool<byte>.Shared.Rent(BufferSize);

                    long windowStart = Environment.TickCount64;
                    long windowBytes = 0;

                    while (true)
                    {
                        int read;
                        try
                        {
                            read = await ReadWithIdleTimeoutAsync(stream, buffer, ct).ConfigureAwait(false);
                        }
                        catch (ReadIdleTimeoutException)
                        {
                            return DownloadAttemptResult.Fail(DownloadFailureKind.Stall,
                                $"逐读无数据超过 {ReadIdleTimeoutMs}ms", null, received);
                        }
                        catch (OperationCanceledException) when (ct.IsCancellationRequested)
                        {
                            throw;
                        }
                        catch (Exception ex) when (ex is IOException or HttpRequestException or ObjectDisposedException)
                        {
                            return DownloadAttemptResult.Fail(DownloadFailureKind.Transient,
                                $"传输中断: {ex.Message}", null, received);
                        }

                        if (read <= 0) break;

                        await dest.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                        hasher.AppendData(buffer, 0, read);

                        received += read;
                        windowBytes += read;
                        Interlocked.Add(ref GlobalBytesDownloaded, read);

                        if (received > MaxAttemptBytes)
                            return DownloadAttemptResult.Fail(DownloadFailureKind.Fatal,
                                "单文件超过 4GB 上限", null, received);

                        // 失速判据：窗口内平均速率低于下限就掐断，立刻换源
                        long elapsed = Environment.TickCount64 - windowStart;
                        if (elapsed >= SpeedWindowMs)
                        {
                            long rate = windowBytes * 1000 / Math.Max(elapsed, 1);
                            if (rate < MinSpeedBytesPerSec)
                            {
                                return DownloadAttemptResult.Fail(DownloadFailureKind.Stall,
                                    $"速度过慢断开：{rate / 1024} KB/s（下限 {MinSpeedBytesPerSec / 1024} KB/s）",
                                    null, received);
                            }

                            windowStart = Environment.TickCount64;
                            windowBytes = 0;
                        }
                    }

                    // 大小一致性（有 Content-Length 时）
                    if (contentLength is > 0 && received != contentLength.Value)
                    {
                        return DownloadAttemptResult.Fail(DownloadFailureKind.Transient,
                            $"下载不完整：收到 {received:N0} / 期望 {contentLength.Value:N0} B", null, received);
                    }

                    // ---------- 5) SHA1 校验（不需要回读文件） ----------
                    if (!string.IsNullOrEmpty(expectedSha1))
                    {
                        string actual = Convert.ToHexString(hasher.GetHashAndReset()).ToLowerInvariant();
                        if (!actual.Equals(expectedSha1, StringComparison.OrdinalIgnoreCase))
                        {
                            return DownloadAttemptResult.Fail(DownloadFailureKind.Corrupt,
                                $"SHA1 不匹配：期望 {expectedSha1}，实际 {actual}", null, received);
                        }
                    }

                    // ---------- 6) 落盘 ----------
                    if (dest is MemoryStream ms)
                    {
                        await dest.FlushAsync(ct).ConfigureAwait(false);
                        // 小文件：一次写盘，避免流式小写触发多次杀软扫描
                        await File.WriteAllBytesAsync(localPath, ms.ToArray(), ct).ConfigureAwait(false);
                    }
                    else
                    {
                        await dest.FlushAsync(ct).ConfigureAwait(false);
                        await dest.DisposeAsync().ConfigureAwait(false);
                        dest = null;
                        File.Move(tempPath!, localPath, overwrite: true);
                        tempPath = null;
                    }

                    committed = true;
                    return DownloadAttemptResult.Ok(received);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (HttpRequestException ex)
            {
                return ClassifyHttpException(ex, received);
            }
            catch (IOException ex)
            {
                // 区分「网络流中断」与「磁盘写失败」
                return DownloadAttemptResult.Fail(
                    IsDiskError(ex) ? DownloadFailureKind.Fatal : DownloadFailureKind.Transient,
                    ex.Message, null, received);
            }
            catch (UnauthorizedAccessException ex)
            {
                return DownloadAttemptResult.Fail(DownloadFailureKind.Fatal, ex.Message, null, received);
            }
            catch (Exception ex)
            {
                return DownloadAttemptResult.Fail(DownloadFailureKind.Transient,
                    $"{ex.GetType().Name}: {ex.Message}", null, received);
            }
            finally
            {
                if (buffer != null) ArrayPool<byte>.Shared.Return(buffer);

                if (dest != null)
                {
                    try { await dest.DisposeAsync().ConfigureAwait(false); } catch { }
                }

                if (!committed && tempPath != null)
                {
                    try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
                }
            }
        }

        // ============================================================
        // 逐读看门狗（每次读取单独计时，与整体请求超时解耦）
        // ============================================================

        private sealed class ReadIdleTimeoutException : Exception
        {
            public ReadIdleTimeoutException() : base("read idle timeout") { }
        }

        private static async Task<int> ReadWithIdleTimeoutAsync(Stream stream, byte[] buffer, CancellationToken ct)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(ReadIdleTimeoutMs);

            try
            {
                return await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                // 是我们自己的看门狗触发，不是用户取消
                throw new ReadIdleTimeoutException();
            }
        }

        // ============================================================
        // 错误分类
        // ============================================================

        private static DownloadAttemptResult ClassifyHttpException(HttpRequestException ex, long received = 0)
        {
            // .NET 8+ 的结构化错误类型：DNS 失败要「立刻换源」，不能当 Transient 重试
            switch (ex.HttpRequestError)
            {
                case HttpRequestError.NameResolutionError:
                    return DownloadAttemptResult.Fail(DownloadFailureKind.Dns,
                        $"DNS 解析失败: {ex.Message}", null, received);
                case HttpRequestError.SecureConnectionError:
                case HttpRequestError.ConnectionError:
                    return DownloadAttemptResult.Fail(DownloadFailureKind.Transient,
                        $"连接失败: {ex.Message}", null, received);
            }

            // 兼容没有 HttpRequestError 的路径
            for (Exception? inner = ex.InnerException; inner != null; inner = inner.InnerException)
            {
                if (inner is SocketException se && se.SocketErrorCode == SocketError.HostNotFound)
                    return DownloadAttemptResult.Fail(DownloadFailureKind.Dns,
                        $"DNS 解析失败: {ex.Message}", null, received);
            }

            return DownloadAttemptResult.Fail(DownloadFailureKind.Transient, ex.Message, null, received);
        }

        private static bool IsDiskError(IOException ex)
        {
            // 磁盘满 / 路径非法 / 文件被占用 视为不可恢复（换源无用）
            string msg = ex.Message ?? "";
            return ex is DirectoryNotFoundException
                   || msg.Contains("space", StringComparison.OrdinalIgnoreCase)
                   || msg.Contains("空间", StringComparison.OrdinalIgnoreCase)
                   || msg.Contains("denied", StringComparison.OrdinalIgnoreCase)
                   || msg.Contains("另一个程序", StringComparison.OrdinalIgnoreCase);
        }

        private static TimeSpan? ParseRetryAfter(HttpResponseMessage response)
        {
            var retryAfter = response.Headers.RetryAfter;
            if (retryAfter is null) return null;

            if (retryAfter.Delta is TimeSpan delta) return delta;

            if (retryAfter.Date is DateTimeOffset date)
            {
                TimeSpan wait = date - DateTimeOffset.UtcNow;
                return wait > TimeSpan.Zero ? wait : TimeSpan.Zero;
            }

            return null;
        }
    }
}
