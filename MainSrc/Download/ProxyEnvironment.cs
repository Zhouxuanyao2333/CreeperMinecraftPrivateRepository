using System;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;

using Project.Launch.Tools.JavaConfig;
using Project.Launch.Tools.LogsConfig;

namespace Project.Launch.Download
{
    /// <summary>
    /// 系统代理探测与自动降级。
    ///
    /// 背景：Windows 的 WinINET 代理（如 Clash / 各种加速器）会把
    /// `ProxyEnable=1, ProxyServer=127.0.0.1:7892` 写进注册表，而 .NET 的
    /// HttpClient **默认继承系统代理**。一旦代理客户端被关掉，所有请求都会
    /// 被送到这个已经没人监听的本地端口，得到「目标计算机积极拒绝」——
    /// 表现就是「获取版本列表失败 / 下载全部失败」，看起来像网络没了，
    /// 其实直连完全正常（curl 不走系统代理，所以 curl 是通的）。
    ///
    /// 这里做两件事：
    ///   1) 启动时探测：系统代理指向本机且端口没在监听 → 直接不用代理；
    ///   2) 运行时兜底：请求因代理端点不可达而失败 → 立即禁用代理并重试直连。
    /// </summary>
    public static class ProxyEnvironment
    {
        private static int _initialized;
        private static int _enabled = 1;   // 0 = 关闭（直连）, 1 = 使用系统代理
        private static int _logOnce;
        private static string _endpoint = "";
        private static bool _isLoopbackProxy;

        /// <summary>当前是否使用系统代理。false 时所有请求走直连。</summary>
        public static bool IsEnabled => Volatile.Read(ref _enabled) == 1;

        /// <summary>系统代理端点（如 127.0.0.1:7892），未配置时为空。</summary>
        public static string Endpoint => _endpoint;

        /// <summary>
        /// 启动时探测一次。幂等，可安全多处调用。
        /// </summary>
        public static void EnsureInitialized()
        {
            if (Interlocked.Exchange(ref _initialized, 1) == 1) return;

            try
            {
                IWebProxy systemProxy = HttpClient.DefaultProxy;

                // 用一个确定会走网络的地址去问「该用哪个代理」
                Uri probeDestination = new Uri("https://piston-meta.mojang.com/");
                Uri? proxyUri = systemProxy.GetProxy(probeDestination);

                if (proxyUri == null || proxyUri == probeDestination)
                {
                    SetEnabled(false, "系统未配置 HTTP 代理，使用直连");
                    return;
                }

                _endpoint = proxyUri.IsDefaultPort
                    ? proxyUri.Host
                    : $"{proxyUri.Host}:{proxyUri.Port}";

                // 只对本机代理做存活探测；远程代理探测代价高且可能被策略拦截
                bool isLoopback = proxyUri.IsLoopback
                                  || proxyUri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
                                  || proxyUri.Host == "::1";
                _isLoopbackProxy = isLoopback;

                if (isLoopback && !IsTcpPortAlive(proxyUri.Host, proxyUri.Port, 400))
                {
                    SetEnabled(false,
                        $"系统代理 {_endpoint} 未在监听（代理客户端可能没启动），已自动改为直连");
                    return;
                }

                // 再确认目标主机没有被加入代理例外名单
                if (systemProxy.IsBypassed(probeDestination))
                {
                    SetEnabled(false, $"目标主机在系统代理例外名单中，使用直连");
                    return;
                }

                SetEnabled(true, $"使用系统代理 {_endpoint}");
            }
            catch (Exception ex)
            {
                SetEnabled(false, $"读取系统代理设置失败（{ex.Message}），改用直连");
            }
        }

        /// <summary>
        /// 运行时兜底：某个请求的失败信息表明「是代理端点连不上」，就永久禁用代理。
        /// 只在确实像代理故障时触发，避免把普通网络抖动误判成代理问题。
        /// </summary>
        public static bool LooksLikeDeadProxy(string? message)
        {
            if (!IsEnabled || string.IsNullOrEmpty(message)) return false;

            if (!string.IsNullOrEmpty(_endpoint)
                && message.Contains(_endpoint, StringComparison.OrdinalIgnoreCase))
                return true;

            if (message.Contains("积极拒绝", StringComparison.Ordinal)
                || message.Contains("refused", StringComparison.OrdinalIgnoreCase))
                return true;

            // 本机代理「能建 TCP 却不转发」：表现为建连/首字节超时。
            // 只有代理指向本机时才这么判定，远程代理超时可能只是网络抖动。
            if (_isLoopbackProxy
                && (message.Contains("建连/首字节超时", StringComparison.Ordinal)
                    || message.Contains("代理", StringComparison.Ordinal)))
                return true;

            return false;
        }

        /// <summary>禁用代理（直连）。幂等。</summary>
        public static void DisableAfterFailure(string reason)
        {
            if (!IsEnabled) return;
            SetEnabled(false, reason);
        }

        private static void SetEnabled(bool enabled, string reason)
        {
            Volatile.Write(ref _enabled, enabled ? 1 : 0);

            // 只在第一次决定时写一条日志，避免刷屏
            if (Interlocked.Exchange(ref _logOnce, 1) == 0)
            {
                LogHelper.Write(LauncherPaths.AppLog,
                    $"[网络] {reason}",
                    $"[Network] {reason}");
            }
            else if (!enabled)
            {
                LogHelper.Write(LauncherPaths.AppLog,
                    $"[网络] 已切换为直连：{reason}",
                    $"[Network] Switched to direct connection: {reason}");
            }
        }

        private static bool IsTcpPortAlive(string host, int port, int timeoutMs)
        {
            try
            {
                using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                var connectTask = socket.ConnectAsync(host, port);
                if (!connectTask.Wait(timeoutMs)) return false;
                return socket.Connected;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// 自适应 IWebProxy：每个请求实时询问 ProxyEnvironment。
    /// 这样「禁用代理」对后续请求立即生效，不需要重建 HttpClient。
    /// </summary>
    internal sealed class AdaptiveProxy : IWebProxy
    {
        private readonly IWebProxy _inner = HttpClient.DefaultProxy;

        public ICredentials? Credentials
        {
            get => _inner.Credentials;
            set => _inner.Credentials = value;
        }

        public Uri? GetProxy(Uri destination)
        {
            if (!ProxyEnvironment.IsEnabled) return null; // null = 直连

            try
            {
                Uri? proxy = _inner.GetProxy(destination);
                // 返回与目标相同的 URI 表示「该主机不走代理」
                return proxy == destination ? null : proxy;
            }
            catch
            {
                return null;
            }
        }

        public bool IsBypassed(Uri host)
        {
            if (!ProxyEnvironment.IsEnabled) return true;

            try
            {
                return _inner.IsBypassed(host);
            }
            catch
            {
                return true;
            }
        }
    }
}
