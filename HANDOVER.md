# Kilim Minecraft Launcher — 完整交接文档（自包含）

> **本文件是自包含的**：包含项目框架、用途定位、技术栈、架构分层、**2026-10-31 下载引擎重写全过程（含困难与解决方式）**、下载引擎核心用法、已知缺陷、下一步待办，以及**当前全部源码**。
> 接手方只看这一个文件即可理解项目并继续开发。

| 项 | 值 |
|---|---|
| 项目 | Kilim Minecraft Launcher（内部代号 `Project.Launch`） |
| 仓库 | https://github.com/Zhouxuanyao2333/KilimMinecraftPrivateRepository （**私有**） |
| 文档生成 | 2026-10-31 |
| 技术栈 | C# / .NET 10 + Avalonia 12.1.2 + SkiaSharp 3.119.4 + NAudio 2.2.1 |
| 平台 | Windows x64 |
| 源码目录 | **`MainSrc\`** |
| 源码规模 | 71 个文件 / 约 13,800 行（含空行）/ 约 300 KB（不含图片音效） |
| 版本号 | `SetConstants.Version = "rc-6"` |

---

# 目录

- [第零部分：这份文档怎么用](#第零部分这份文档怎么用)
- [第一部分：这是什么项目](#第一部分这是什么项目)
- [第二部分：环境、构建与运行](#第二部分环境构建与运行)
- [第三部分：2026-10-31 改造记录（困难与解决方式）](#第三部分2026-10-31-改造记录困难与解决方式)
- [第四部分：下载引擎——设计原理与核心用法](#第四部分下载引擎设计原理与核心用法)
- [第五部分：其余模块现状](#第五部分其余模块现状)
- [第六部分：完整源码](#第六部分完整源码)
- [第七部分：协作约定](#第七部分协作约定)

---

# 第零部分：这份文档怎么用

## 0.1 文档一定会过期 —— 这是设计前提

**这个项目仍在快速迭代。本文件是一张"快照"，不是"事实来源"。**

- 第六部分对应 **2026-10-31 的源码状态**；
- 用户在此之后改过的文件，**第六部分对应小节就已经失数**；
- **不要根据本文档的代码去推断"当前实现是怎样的"**，也不要基于它做重构；
- 正确做法：直接读磁盘上的最新文件。

相对稳定、可放心参考的部分：**第一部分的框架与用途、第二部分的构建与环境陷阱、第三部分的改造记录与根因分析、第四部分的下载引擎设计原理、第七部分的协作约定。**

会随开发失效的部分：第五部分的功能细节、第六部分的完整源码。

## 0.2 源码内嵌策略（第六部分）

| 类别 | 是否嵌入全文 |
|---|---|
| 根目录：`Program.cs`、`App.axaml(.cs)`、`.csproj`、`app.manifest`、`.gitignore`、`README.md` | ✅ 全文 |
| `MainSrc\Download\`：下载引擎 6 个文件 | ✅ 全文 |
| `MainSrc\Auth\`：微软登录 9 个文件 | ✅ 全文 |
| `MainSrc\Views\`：4 个窗口 + 2 套样式表（Styles/Glass）+ read.md | ✅ 全文 |
| `MainSrc\Tools\JavaConfig\`：启动内核 9 个文件 | ✅ 全文 |
| `MainSrc\Tools\Player3DConfig\`：3D 皮肤 3 个文件 | ✅ 全文 |
| `MainSrc\Tools\{AnimSettings,AppConstants,LogsConfig,SoundControl}\`：5 个文件 | ✅ 全文 |
| `MainSrc\Tools\LiquidGlassAvaloniaUI\`：玻璃库 13 个 `.cs` + 6 个 `.sksl` + csproj | ✅ 全文 |
| 图片 / 音效 / 图标（约 140 个，约 3.6 MB） | ❌ 仅列清单（见 6.10） |
| `HANDOVER.md`（本文件）、`GLASS_NEGATIVE_FINDINGS.md` | ❌ 不嵌入（它们本身就是文档） |
| `Design\`（PSD/MP3/参考图） | ❌ 不嵌入 |

## 0.3 推荐阅读顺序

**第一次接触本项目：**

1. [1.1 一句话定位](#11-一句话定位) → [1.5 架构分层](#15-架构分层与调用关系) —— 建立全局印象
2. [2.3 环境陷阱](#23--环境陷阱务必先读) —— **动手前必读**（代理、输出目录、文件占用）
3. [第三部分 改造记录](#第三部分2026-10-31-改造记录困难与解决方式) —— 理解"为什么代码长这样"
4. [第四部分 下载引擎](#第四部分下载引擎设计原理与核心用法) —— 本项目最复杂、最值得读的子系统
5. [6.3 `GameLauncher.cs`](#65-mainsrctoolsjavaconfig--启动内核) —— 启动器的核心
6. [6.4 `MainWindow`](#64-mainsrcviews--界面层) —— 界面结构与交互范式

**要动下载功能** → 直接读 [第四部分](#第四部分下载引擎设计原理与核心用法) + [6.2 `MainSrc\Download`](#62-mainsrcdownload--下载引擎)。

---

# 第一部分：这是什么项目

## 1.1 一句话定位

**Kilim Minecraft Launcher** 是一个用 **C# / .NET 10 + Avalonia 12 从零手写**的 Minecraft 启动器，主打：

1. **不用 WebView**（原生渲染，无内嵌浏览器开销）；
2. **自研液态玻璃 UI**（SKSL 运行时着色器做折射 / 色散 / 模糊）；
3. **原生下载引擎**（自研，对标 PCL 的失败兜底与失速治理）。

## 1.2 现状判断

| 模块 | 状态 |
|---|---|
| UI / 动画 / 液态玻璃 / 3D 皮肤 | ✅ 打磨得很细（外观完成度约 90%） |
| 账户系统（离线 + 微软正版全链路） | ✅ 代码完整，正版登录可用 |
| 游戏启动（导入式） | ✅ 可用（classpath、natives、新旧两代参数都处理了） |
| **下载 / 安装** | ✅ **2026-10-31 重写完成**，实测平均 3.6 MB/s、0 失败 |
| 设置页 / 工具页 | ⚪ 仍是占位文字 |
| 正版 token 传入游戏进程 | ❌ **未打通**（见 5.6，目前正版账户等于按离线启动） |

> **一句话**：UI 和下载都已经是"能打"的水平，**"下载 → 用正版身份启动"这条主干的出口还差最后一跳**。

## 1.3 技术栈

| 项 | 内容 |
|---|---|
| 框架 | .NET 10（`net10.0`），`win-x64`，`WinExe` |
| UI | Avalonia 12.1.2 + Fluent 主题 + Inter 字体 |
| 渲染 | SkiaSharp 3.119.4（3D 皮肤投影 + 液态玻璃着色器） |
| 音频 | NAudio 2.2.1 |
| 发布 | 单文件自包含（`PublishSingleFile` + 压缩），**未裁剪** |
| 玻璃库 | `MainSrc\Tools\LiquidGlassAvaloniaUI`，**独立 csproj**，主项目排除其源码后用 `ProjectReference` 引用 |
| 测试 | **无测试工程、无 CI**（全项目只有两个 csproj） |
| MVVM | **没有**（全部 `FindControl` + 命令式赋值，状态放在 `App` 的静态字段里） |

## 1.4 项目结构总览

```
Project.Launch\                        ← 仓库根
├── Program.cs                        程序入口（崩溃日志兜底 + 启动代理探测）
├── App.axaml / App.axaml.cs          应用级资源、启动时恢复登录态、★全局异常兜底
├── Project.Launch.csproj             主项目文件（★路径集中，改目录必须同步）
├── app.manifest                      Windows 清单（透明窗口相关，勿删）
├── .gitignore                        构建/发布产物忽略规则
├── README.md                         项目说明 + 隐私政策
├── HANDOVER.md                       ★ 本文件
├── GLASS_NEGATIVE_FINDINGS.md        液态玻璃的一次反向排查记录（历史资料）
├── Design\                           美术素材（PSD / MP3 / 参考图，不入编译）
│
├── MainSrc\                          ★ 源码根（2026-10 由 src\ 改名而来）
│   ├── Auth\                         微软正版登录全链路（MSA→XBL→XSTS→MC）
│   ├── Download\                     ★★ 下载引擎（本次改造重点）
│   │   ├── SourcePolicy.cs           源排序 / 健康度冷卻 / 主机级并发门
│   │   ├── DownloadEngine.cs         下载原语：看门狗 / 失速判据 / 校验 / 内存直写
│   │   ├── GameInstaller.cs          安装调度：清单展开 / 换源 / 统计 / 日志
│   │   ├── VersionService.cs         版本清单（元数据）
│   │   ├── MinecraftManifest.cs      清单数据模型
│   │   └── ProxyEnvironment.cs       ★ 系统代理探测与直连降级
│   ├── Tools\
│   │   ├── JavaConfig\               启动内核：GameLauncher / JavaDetector / 实例 / 版本 JSON
│   │   ├── Player3DConfig\           3D 皮肤（SkiaSharp 顶点绘制）
│   │   ├── LiquidGlassAvaloniaUI\    ★ 液态玻璃库（vendored 第三方，MIT）
│   │   ├── AnimSettings\             动画参数
│   │   ├── AppConstants\             版本号 / 应用名唯一来源
│   │   ├── LogsConfig\               双语日志
│   │   └── SoundControl\             音效
│   ├── Views\                        界面层（MainWindow / LoginWindow / AccountManagerWindow / DownloadWindow + 样式）
│   ├── imgs\                         ★ 图片资源（按用途分目录）
│   ├── Sound\                        6 个音效 wav
│   ├── Codes\                        【空】规划为"内核级主程序代码"，实际代码都在 Tools\ 与 Download\
│   └── BuiltDebugRun\                ★ Debug 输出目录（**已 gitignore，绝不入库**）
│
├── obj\                              ★ 已 gitignore
└── publish\                          ★ 已 gitignore
```

> **运行时数据目录不在这里**：它在 **exe 同级的 `KilimLauncher\`**，即 `MainSrc\BuiltDebugRun\KilimLauncher\`。见 [2.4](#24-运行时数据目录)。

## 1.5 架构分层与调用关系

```
                              Program.cs
                                  │  ① 写 startup.log
                                  │  ② ProxyEnvironment.EnsureInitialized()  ← 启动即探测系统代理
                                  │  ③ 构建 Avalonia App，崩溃写 crash.log
                                  ▼
                            App.axaml.cs
                                  │  ① AvaloniaXamlLoader.Load
                                  │  ② ★ InstallCrashGuards()：UI 线程未处理异常拦截
                                  │  ③ 启动时读 accounts.json 恢复登录态
                                  ▼
        ┌─────────────────── MainWindow（主窗） ───────────────────┐
        │  5 Tab：下载(0) / 启动(1) / 主页(2) / 设置(3) / 工具(4)    │
        │  默认停在 index=2（主页）                                 │
        └───────┬─────────────────────────────────┬───────────────┘
                │                                 │
      ┌─────────▼──────────┐          ┌───────────▼─────────────┐
      │  下载链路 ★本次重写 │          │  启动游戏链路            │
      └─────────┬──────────┘          └───────────┬─────────────┘
                │                                 │
   VersionService.GetVersionManifestAsync          │
     └ SourcePolicy 决定源顺序 + 健康度              │
                │                                 │
   DownloadWindow（线程数 + 进度 + 取消）           │
                │                                 │
   GameInstaller.InstallVanillaAsync  ★核心调度     │
     · 下版本 json → 解析 libraries / client.jar    │
     · 下 asset index → 展开数千个 object           │
     · 逐文件：SourcePolicy 排序候选源               │
                └→ DownloadEngine.DownloadAsync     │
                     · 建连看门狗 8s                │
                     · 逐读看门狗 15s               │
                     · 失速判据 10s < 24KB/s        │
                     · 小文件内存直写 + 边下边算 SHA1│
                     · 失败分类 → 换源/重试/冷却     │
                                            MinecraftVersionJson.Load(json)
                                                     │
                                            JavaDetector.DetectAllAsync() / Match()
                                                     │
                                            OfflineUuidGenerator（离线 UUID）
                                                     │
                                            GameLauncher.LaunchAsync(...)  ★核心
                                              · 拼 classpath（client.jar + libraries）
                                              · 解压 natives
                                              · 替换占位符（auth_* / assets_* / ...）
                                              · --language xx_xx（按系统语言）
                                              · Process.Start(java, args)
                                              · stdout/stderr → instances\{id}\game.log
                                                     │
                                               java.exe 启动 Minecraft

  账户链路（与上并行）：
    LoginWindow ─┬─ 离线：OfflineUuidGenerator → AccountService
                 └─ 正版：MicrosoftAuthFlow（MSA→XBL→XSTS→MC）→ AccountService
    AccountService → KilimLauncher\accounts.json
    AccountManagerWindow → 列表 / 切换 / 删除

  渲染子系统（横切）：
    LiquidGlassSurface.Render()
      → LiquidGlassBackdropProvider.EnsureSnapshot()   抓窗口背景快照
      → LiquidGlassDrawOperation                        SKSL 折射/色散/模糊
    3D 皮肤：Pivot3DControl（ICustomDrawOperation + SkiaSharp DrawVertices）
```

**必须知道的架构事实：**

1. **数据目录不在 `%APPDATA%`**，而在 **exe 同级的 `KilimLauncher\`**。装到 `Program Files` 会因无写权限而**静默失效**。
2. **没有 ViewModel、没有绑定**：UI 状态由 `App` 的静态字段 + 各窗口私有字段维护。
3. **玻璃库是 vendored 的**：源码在仓库里，但主项目**不编译它**（`<Compile Remove>`），而是引用它独立编译的 DLL。改它需要单独编译。
4. **`.NET` 会继承 Windows 系统代理** —— 这是本项目最容易"明明网络没问题却全部失败"的原因，已在 `ProxyEnvironment` 中处理。
5. **所有图片/音效/样式都是 `avares://` 运行时加载**，路径写错**不会编译报错**，只会运行时静默失败。

---

# 第二部分：环境、构建与运行

## 2.1 环境要求

| 项 | 要求 |
|---|---|
| .NET SDK | **10.0.x**（本项目用 10.0.401 验证） |
| OS | Windows x64（含 Win10/11） |
| PowerShell | **注意：`pwsh -Command` 在本机实际是 PowerShell 5.1** —— 没有 `ForEach-Object -Parallel`，不支持 `if` 作表达式 |
| 磁盘 | 下载一个完整版本约需 650 MB（1.20.1）/ 约 530 MB（26.3） |

## 2.2 构建与运行命令

```powershell
# 构建（Debug）—— 输出到 MainSrc\BuiltDebugRun\
dotnet build Project.Launch.csproj -c Debug

# 强制完整重编（怀疑"改了没生效"时用）
dotnet clean Project.Launch.csproj ; dotnet build Project.Launch.csproj -c Debug

# 单独编译玻璃库（改过 LiquidGlassAvaloniaUI 之后）
dotnet build MainSrc\Tools\LiquidGlassAvaloniaUI\LiquidGlassAvaloniaUI.csproj

# 运行
.\MainSrc\BuiltDebugRun\Project.Launch.exe

# 发布单文件
dotnet publish Project.Launch.csproj -c Release

# ★ 输出目录被占用时的规避（启动器正在运行时）
dotnet build Project.Launch.csproj -c Debug -p:OutputPath="$env:TEMP\buildcheck\"
```

## 2.3 ⚠️ 环境陷阱（务必先读）

### 🔴 陷阱 1：系统代理开着但代理客户端没运行 → 所有网络请求失败

Windows 的 WinINET 代理配置在注册表
`HKCU\Software\Microsoft\Windows\CurrentVersion\Internet Settings`
（`ProxyEnable` / `ProxyServer`）。**.NET 的 `HttpClient` 默认继承它**；而 `curl.exe` **不读**注册表。

所以会出现这种极具误导性的现象：

- 启动器：`获取版本清单失败：由于目标计算机积极拒绝，无法连接。(127.0.0.1:7892)`
- 同机 `curl`：官方源 200，一切正常

**已在代码中处理**：`ProxyEnvironment` 启动时探测本机代理端口是否在监听，不通则自动改直连；运行期若请求命中代理端点也会自动降级。见 [4.7](#47-代理自适应proxyenvironment)。

> 排查手法：`Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Internet Settings'` 看 `ProxyEnable`；
> `Get-NetTCPConnection -LocalPort <端口> -State Listen` 看代理是否真的在跑。

### 🟠 陷阱 2：`BuiltDebugRun\` 在 `MainSrc\` 内部

主项目用默认 `**/*.cs` 通配，会把输出目录里生成的 `.cs` 吸进编译 → `CS0579 程序集特性重复`（8 个错误）。
csproj 里已有 5 组 `<... Remove="MainSrc\BuiltDebugRun\**" />`。**改 OutputPath 时必须同步改这 5 行**。

### 🟠 陷阱 3：`.gitignore` 与 `<OutputPath>` 联动

OutputPath 改了但 gitignore 没跟 → 构建产物入库。历史上曾把 `publish\` 132 MB 二进制提交进去。

### 🟡 陷阱 4：`avares://` 路径与磁盘路径强耦合

路径写错**不会编译报错**，只在运行时 `AssetLoader.Open` 抛异常；而多数加载点又被 `try/catch` 包住，
表现为"图标就是不见了"，日志里最多一行。改目录时务必全局搜 `avares://`。

### 🟡 陷阱 5：输出目录被运行中的 exe 锁住

`MSB3027/MSB3021`（无法复制 apphost.exe，文件被占用）= 启动器还开着。
关掉进程，或用 `-p:OutputPath=` 指到别的目录做编译验证。

## 2.4 运行时数据目录

**位置**：`LauncherPaths.Root` = `exe 同级\KilimLauncher\`

| 文件 | 用途 |
|---|---|
| `accounts.json` | 账户（含正版 refresh/access token） |
| `instances.json` | 已导入实例列表 + 当前选中实例 |
| `instances\{实例Id}\natives\` | 解压出来的 natives |
| `instances\{实例Id}\game.log` | 游戏进程 stdout/stderr |
| `startup.log` | 程序入口日志 |
| `app.log` | 应用级日志（**下载引擎的全部诊断信息都在这里**） |
| `MainWindowLog.txt` | 主窗交互日志（**每次启动会重置**） |
| `crash.log` | 崩溃日志（含被拦截的 UI 异常） |

⚠️ 所有写入都被 `try/catch` 包住 → 装到无写权限的目录（如 `Program Files`）会**静默失效**。

## 2.5 Git 仓库与忽略规则

`.gitignore` 忽略：`bin/`、`obj/`、`BuiltDebugRun/`、`publish/`、`.vscode/`、`.vs/`、`*.log`、`KilimLauncher/`、`*.tmp`。


---

# 第三部分：2026-10-31 改造记录（困难与解决方式）

> 这一部分记录本次三个真实故障的**现象、取证方法、根因、修复、验证**。
> 目的是让后来者不再重复踩坑，也保留"如何定位"的方法论。

## 3.1 改造概览

| # | 故障现象 | 根因 | 修复落点 | 状态 |
|---|---|---|---|---|
| 1 | 下载开始很快，随后掉到数十 KB/s 且不恢复；装完"成功"但文件缺一半 | 镜像源坏 + 慢不触发回退 + 无 body 超时 + 信号量被僵尸连接占死 | `SourcePolicy.cs`（新）、`DownloadEngine.cs`、`GameInstaller.cs` 重写 | ✅ 已验证 |
| 2 | 版本列表一直"获取失败"，但同机 curl 正常 | 系统代理开关开着、代理客户端没运行；.NET 继承 WinINET → 全打向死端口 | `ProxyEnvironment.cs`（新）+ 共享 HttpClient | ✅ 已验证 |
| 3 | 选好下载目录后无响应并崩溃 | Avalonia 文件夹选择器抛 `Directory must exist`，砸在 `async void` 上终止进程 | `App.axaml.cs` 全局守卫 + `MainWindow.PickFolderSafeAsync` | ✅ 已验证 |

## 3.2 困难一：下载速度从 MB/s 掉到数十 KB/s（本项目的头号问题）

### 3.2.1 现象

- 点下载后，**头几十秒速度很高**（多线程拉满）；
- 随后**掉到数十 KB/s** 并**长时间不恢复**；
- 进度条几乎不动，但**界面报"安装成功"**；
- `app.log` 里**看不到任何下载失败记录**。

### 3.2.2 取证方法（这是本次最有价值的部分）

因为"看起来像网络问题"，必须先分清**是网络差，还是代码路径选错**。用了四路并行取证：

1. **现场对账**：直接检查真实下载产物目录（`E:\MinecraftDownloadTest\26.3`），按 `asset index` 与版本 JSON 逐项核对"应下 / 实有"。
2. **传输层 A/B 实测**：同一批真实资源，用 `curl` 分别打官方源与 BMCLAPI，测聚合吞吐、成功数、状态码、302 链、节点 host 分布。
3. **代码审计**：逐行审查调度、信号量、超时、重试、错误分类，答案必须能解释"先快后慢"。
4. **原理对照**：按 PCL 公开的设计思路（失速掐断 / 源降级 / 连接纪律 / 小文件特化）核对"它为什么不会掉"。

### 3.2.3 关键实测数据

**现场对账（12 分钟那一次的真实结果）**

| 项 | 结果 |
|---|---|
| `client.jar`（41 MB） | ✅ 完整，约 780 KB/s 完成 |
| assets objects | **2190 / 5147** 完成，缺 2957 个 / 363 MB |
| libraries | **8 / 114** 完成，缺 106 个 |
| `.tmp` 残留 | 无（说明不是"半路崩"，而是**大量请求静默失败**） |

**传输层 A/B（24 个真实 asset，并发 20）**

| 源 | 完成 | 实收 | 墙钟 | 聚合吞吐 |
|---|---|---|---|---|
| 官方 `resources.download.minecraft.net` | **24/24** | 1.24 MB | 2.12 s | **571 KB/s** |
| BMCLAPI `bmclapi2.bangbang93.com/assets` | **12/24** | 355 KB | 30.5 s | **11 KB/s** |

**单连接采样**

- 镜像：`0.6 ~ 63 KB/s`，多例 20~25 s 零字节；有的 200 响应把 1.33 MB **截断成 190 KB**；
- 官方：`320 ~ 5957 KB/s`，**从不截断**。

**BMCLAPI 的结构性变化（这是根因的核心）**

它不再直连返回文件，而是 **302 重定向到签名节点**，且节点是**每请求随机分配**的：

- 30 个请求落到 **16~18 个不同 host**；同一个 hash 连测 3 次得到 3 个不同节点；
- 节点池里**9 个 host 结构性不可用**：`bmclapi.hurtmc.com`（DNS NXDOMAIN）、`bmclapi2/3.hurtmc.com`（间歇 NXDOMAIN）、`bmclapi3.sakura.sld.tw`（TLS 被 RST）、`1/14/15/16.749333.xyz`（0 成功）、`bmclapi21.sakura.sld.tw`（4/4 挂死 15s）；
- 响应头 `X-RateLimit-Limit-short: 10`，且 **429 是按 URL 计**（10 次/60 s，`Retry-After=60`）；
- 实测：并发 20 + 命中 429 后重试 → 24 个文件跑 **154.7 s、只完成 20 个、2 KB/s**。

**并发梯度（证明"不是并发不够"）**

| BMCLAPI 并发 | 聚合吞吐 | 成功率 |
|---|---|---|
| 1 | 1.5 KB/s | 14/24 |
| 8 | 8.5 KB/s | 16/24 |
| 20 | 9.9 KB/s | 24/48 |
| 50 | **33.7 KB/s** | 23/48 |
| 官方 50 | **1160 KB/s** | **48/48** |

→ **差距约 34 倍，且不是靠调并发能补上的。**

### 3.2.4 根因链（为什么"先快后慢且回不来"）

**"先快"的真相**：任务清单第一个是 `client.jar`，它的 302 落到官方 CDN `piston-data.mojang.com`，实测 **6.9 MB/s**。所以开头那几十秒的高速度，来自**一个大文件走官方 CDN**，不是小文件并行快。

**"后慢"的成因**：下载主体是 **数千个 asset 小文件**（占文件数 98%、字节 97%，平均约 94 KB，中位数仅 19.7 KB）。每个文件都先撞 BMCLAPI。五个致命点叠加：

| # | 代码位置（改造前） | 问题 | 后果 |
|---|---|---|---|
| 1 | `GameInstaller.ResolveMirrors` | **镜像永远排第一**（`yield mirror; yield official;`） | 每个文件都先去撞坏源 |
| 2 | `GameInstaller.DownloadWithFallbackAsync` | 回退判据**只看 HTTP 错误码**，`404/403` 才换源 | **"慢但返回 200"被当成成功**，永不回退 |
| 3 | `DownloadEngine.DownloadOnceAsync` | 15 s 超时**只覆盖"等响应头"**；body 读用外层 `ct`；`HttpClient.Timeout = Infinite` | 涓流/挂死连接**永不报错**，无限期占住槽位 |
| 4 | `GameInstaller.InstallVanillaAsync` | `SemaphoreSlim` 包住**整个** `DownloadOnceAsync`（含 15 s TTFB 等待 + 302 第二跳 + 全部 body + SHA1 回读） | 槽位被僵尸连接**逐个吃掉**，有效并发单调递减 |
| 5 | `GameInstaller.DownloadWithFallbackAsync` | `429` 映射成 `RateLimited`，但只对 `NotFound/Forbidden` 提前退出 | 429 落进**同 URL 3 次重试 + 退避**，把 60 s 配额打满 |

**量化对上了你的观感**：
> 15 s TTFB × 3 次重试 ≈ 46 s/文件；20 并发 ÷ 平均 94 KB ≈ **41 KB/s** —— 正是"数十 KB/s"。

**为什么"回不来"**：没有失速判据，也没有源健康记忆。每卡死一个连接就永久少一个槽位，
20 → 18 → … → 2，吞吐单调下坡，且**没有任何机制能自愈**。

### 3.2.5 为什么 PCL 不会掉（五条原理）

把 PCL 的做法提炼成**不依赖其源码**的原理，这也是本次改造的设计依据：

| # | 原理 | 具体准则 | 改造前本项目 |
|---|---|---|---|
| 1 | **失速即掐断** | 不能"等到报错才换源"。判据是**速率**：约 5 秒内收不到 1 KB 就掐断；同时每一次读取都单独计时（逐读看门狗），而不是只给整个请求一个总超时 | ❌ 只有"首字节超时"，body 读零超时 |
| 2 | **坏源要降级、不能被丢弃或反复首选** | 源出问题后要**降级为单线程 / 进入冷却**，而不是简单剔除；同时**全源兜底重试只做一次**，避免无限重试 | ❌ 无记忆，每个文件都重新首选坏源 |
| 3 | **连接要有纪律** | 全局连接数设上限；小文件**每文件只允许 1 条连接**；对 bmclapi 这类有频控的源要**禁止追加连接 + 启动间隔 ≥100ms** | ❌ 20~128 并发无差别打 |
| 4 | **小文件要特化** | 小文件（<1MB~4MB）**不分片**、**全程内存缓冲、一次落盘**；避免"流式小写 + 每次 flush"在小文件上被 IO/杀软放大 | ❌ 流式写 + 下完再整文件回读校验 |
| 5 | **并发要闭环** | 并发数不该是"用户填死的控制变量"，而应是"上限"：维护一个**只升不降的速度下限**（近期均速 × 0.85），速度够就停止加连接，不够才加 | ❌ 固定并发，无反馈 |

> 结论：**PCL 的高速不是"线程多"，而是"坏源活不过 5 秒"**。
> 这也解释了为什么单纯把并发从 20 调到 50 只能从 9.9 KB/s 抬到 33.7 KB/s —— 治标不治本。

### 3.2.6 解决方案

按"收益 / 成本"排序落地：

**P0-1 失速判据（这是"回不来"的唯一解）** → `DownloadEngine`
- 建连/首字节 8 s 超时；
- **逐读 15 s 看门狗**（每次 `ReadAsync` 单独计时，PCL 的动态超时基线）；
- **失速判据**：10 s 窗口内平均速率 < **24 KB/s** 即判失速、立刻换源；
- 收到字节即清零失败计数（区分"慢但能下"与"连不上"）。

**P0-2 治源：镜像不再无条件第一** → 新增 `SourcePolicy`
- 候选源按偏好 + 健康度排序；默认 `Auto` = **官方优先**（实测差 30~50 倍）；
- 连续失败进入**冷却**（镜像 2 次 → 60 s；官方 3 次 → 20 s，失败越多冷却越久，上限 5 min）；
- 冷却中的源排到最后但**不丢弃**（避免"两个源都冷却"时无源可用）；
- 主机级并发门：镜像 **8**、官方 **32**（对应 PCL 对 bmclapi 的限频纪律）。

**P0-3 错误语义修正** → `GameInstaller` + `DownloadEngine`
- 失速 / 404 / 403 / 429 / DNS → **立刻换源**，不在坏源上耗时间；
- `429` 读 `Retry-After`，做**源级冷却**，禁止原地立即重试；
- 只有 `Transient`（5xx/连接中断）在同源重试一次；
- **全源都失败 → 再整体降级重试一轮**（PCL 的 `Retried` 语义）；
- 磁盘错误（空间不足 / 无权限）→ `Fatal`，换源无用，直接失败。

**P0-4 小文件特化** → `DownloadEngine`
- < 4 MB 走内存缓冲，**一次落盘**（PCL `<1MB` / PCL-CE `<4MB` 同思路）；
- **边下边算 SHA1**（`IncrementalHash`），省掉"下完再整文件回读"；
- 失败尝试的字节**回滚**，测速不再虚高。

**P1 真实完成率与诊断** → `GameInstaller` / `DownloadWindow`
- 返回 `InstallResult`（总数/成功/跳过/失败），**不再无条件报 100%**；
- 失败按 (源, 失败类型) 聚合写 `app.log`，最多打印 50 条明细；
- 结束写一行源健康状态。

### 3.2.7 验证结果

**A/B 基准（同一台机器、同一批真实资源）**

| 指标 | 改造前 | 改造后 |
|---|---|---|
| 平均吞吐 | 11 KB/s（BMCLAPI 实测）/ 数十 KB/s | **3,630 KB/s** |
| 峰值吞吐 | ~34 KB/s（BMCLAPI 天花板） | **12,395 KB/s** |
| 67 秒内完成文件 | 数十个 | **1337 个，0 失败** |
| 结束报告 | 无条件"安装成功 100%" | 真实成功/跳过/失败计数 |

**坏源逃生验证**（把偏好强制成 `MirrorFirst`，模拟改造前必踩的路径）：
速度一度掉到 0~247 KB/s，但**健康度冷却触发后自动切到官方源，速度回到 1~2 MB/s，全程 0 失败** —— 不再"掉下去回不来"。

## 3.3 困难二：版本列表一直"获取失败"（系统代理死端口）

### 3.3.1 现象

界面显示「获取版本列表失败，请检查网络连接」；`app.log`：

```
获取版本清单失败（official：https://piston-meta.mojang.com/...）: 由于目标计算机积极拒绝，无法连接。 (127.0.0.1:7892)
获取版本清单失败（mirror：https://bmclapi2.bangbang93.com/...）: 由于目标计算机积极拒绝，无法连接。 (127.0.0.1:7892)
所有源均无法获取版本清单。
```

### 3.3.2 取证

| 检查 | 结果 |
|---|---|
| 注册表 `ProxyEnable` | **1**，`ProxyServer = 127.0.0.1:7892` |
| `Get-NetTCPConnection -LocalPort 7892 -State Listen` | **无监听**（代理客户端已退出） |
| `curl` 直连官方源 | **200，1.04 s**（正常） |
| `curl` 直连 BMCLAPI | **302，0.25 s**（正常） |

→ 网络本身没问题；**是 .NET 把请求发给了已死的本地代理端口**。
`curl` 不读 WinINET 注册表，所以"curl 好、启动器坏"。

### 3.3.3 解决方案：`ProxyEnvironment`（新增）

三个层次：

1. **启动探测**：读系统代理；若指向本机（loopback）且端口**无监听** → 直接改用直连并写日志；
2. **每请求实时生效**：`AdaptiveProxy : IWebProxy` 每次询问 `ProxyEnvironment.IsEnabled`，禁用代理对后续请求立即生效（无需重建 `HttpClient`）；
3. **运行时兜底**：请求失败信息里命中"代理端点 / 连接被拒 / 本机代理建连超时" → 禁用代理并**立刻直连重试一次**。

同时把 `DownloadEngine.SharedClient` 作为全项目唯一 `HttpClient`（连接池与代理策略统一），`VersionService` 改用它。

### 3.3.4 验证（复现坏状态）

临时把 `ProxyEnable` 改回 1（端口仍无人监听），跑新代码：

```
[网络] 系统代理 127.0.0.1:7892 未在监听（代理客户端可能没启动），已自动改为直连
[成功获取版本清单（official），共 921 个版本]
```

验证后已把注册表还原。

## 3.4 困难三：选好下载目录后窗口无响应并崩溃

### 3.4.1 现象

双击版本 → 选择目录 → **没有开始下载，整个窗口无响应然后进程退出**。

### 3.4.2 取证（`crash.log` 给得答案）

```
System.ArgumentException: Directory must exist (Parameter 'fileSystemInfo')
   at Avalonia.Platform.Storage.FileIO.BclStorageItem..ctor(FileSystemInfo)
   at Avalonia.Platform.Storage.FileIO.BclStorageFolder..ctor(DirectoryInfo)
   at Avalonia.Win32.Win32StorageProvider.OpenFolderPickerAsync(...)
   at Project.Launch.Views.MainWindow.OnVersionListDoubleTapped(...)
```

时间线对得上：07:44:50 选中版本 → **07:45:20 用户删掉了旧的 `26.3` 目录**（目标文件夹被清空）→ 07:46:00 崩溃。

即：**文件夹对话框返回的路径在 Avalonia 构造 `BclStorageFolder` 时已不存在**，
Avalonia 直接抛异常；而 `OnVersionListDoubleTapped` 是 **`async void`**，未捕获异常回到 Dispatcher → 进程终止。

### 3.4.3 解决方案（两层）

**① 单个 UI 事件出错不再拖垮整个程序**（`App.axaml.cs` → `InstallCrashGuards()`）
- `Dispatcher.UIThread.UnhandledException`：记录到 `app.log` + `crash.log`，并 `e.Handled = true`；
- `AppDomain.CurrentDomain.UnhandledException`：记 `crash.log`；
- `TaskScheduler.UnobservedTaskException`：记日志 + `SetObserved()`。

**② 选目录全程防御**（`MainWindow.PickFolderSafeAsync`）
- `OpenFolderPickerAsync` 整个包进 `try/catch`（**异常正是在这里抛出**）；
- 用 `TryGetLocalPath()` 取路径，而不是 `.Path.LocalPath`；
- 去首尾空格、去掉结尾 `\` / `/`，再 `Directory.Exists` 复核；
- 路径无效 → 提示并**退化成手动输入路径**（`PromptForLocalPathAsync`，代码构建的简易窗口）；
- 两个调用点都替换：版本下载、导入实例。

## 3.5 本次改造的文件清单

| 文件 | 类型 | 说明 |
|---|---|---|
| `MainSrc\Download\SourcePolicy.cs` | **新增** | 源排序 / 健康度冷却 / 主机级并发门 |
| `MainSrc\Download\ProxyEnvironment.cs` | **新增** | 系统代理探测 + 直连降级 + `AdaptiveProxy` |
| `MainSrc\Download\DownloadEngine.cs` | **重写** | 看门狗 / 失速判据 / 错误分类 / 内存直写 / 边下边算 SHA1 |
| `MainSrc\Download\GameInstaller.cs` | **重写** | 换源调度 / 全源重试 / 真实统计 / 失败聚合日志 |
| `MainSrc\Download\VersionService.cs` | 重写 | 元数据走源策略 + 代理降级 |
| `MainSrc\Views\DownloadWindow.axaml.cs` | 修改 | 真实结果 / 失败计数 / 不再把缺文件当成功 |
| `MainSrc\Views\MainWindow.cs` | 修改 | 安全选目录 + 手动输入兜底 + 失败提示带计数 |
| `App.axaml.cs` | 修改 | 全局异常兜底 |
| `Program.cs` | 修改 | 启动即探测代理 |
| `Project.Launch.csproj` | 修改 | 增加"非项目源码目录"排除规则（若目录已删除，这 6 行可一并移除） |

## 3.6 遗留待办（按收益排序）

1. **正版 token 传入游戏进程**（`GameLauncher` 的 `${auth_access_token}` 现在写死 `"0"`）——「下载 → 正版启动」主干唯一断点。
2. **大文件 Range 分片**：`client.jar` 仍单连接（官方 CDN 实测 6.9 MB/s，暂不是瓶颈，但换到慢源就撑不住）。
3. **自适应并发闭环**（速度下限只升不降，见 3.2.5 原理 5）：目前是固定并发 + 主机级上限。
4. **`.tmp` 跨进程续传**：中断后大文件从 0 开始。
5. **natives / classifiers 从未下载**：`GameLauncher` 只解压"磁盘上已存在的" natives jar，`InstallVanillaAsync` 也只收集 `downloads.artifact`。旧版本（含 `natives` 字段）会因此启动失败。
6. **设置页 / 工具页**仍是占位。
7. **无测试、无 CI**：下载引擎至少应补一个"用本地 HTTP 桩做失速/换源"的集成测试。

---


# 第四部分：下载引擎——设计原理与核心用法

> 这是本项目最复杂、也最值得读的子系统。本篇讲**怎么用**和**为什么这么设计**；
> 完整实现见 [6.2 `MainSrc\Download`](#62-mainsrcdownload--下载引擎)。

## 4.1 分层与职责

```
┌──────────────────────────────────────────────────────────────┐
│ VersionService            元数据（版本清单）                    │
│   · 走 SourcePolicy 的源顺序 + 健康度                          │
│   · 单次 10s 超时；代理故障自动直连重试                         │
├──────────────────────────────────────────────────────────────┤
│ GameInstaller             安装调度（"装一个版本"的业务）          │
│   · 展开清单（client.jar / libraries / asset index / objects）  │
│   · 逐文件：源排序 → 换源 → 全源重试一轮 → 统计 → 聚合日志        │
│   · 全局并发门 + 进度/测速 + InstallResult                      │
├──────────────────────────────────────────────────────────────┤
│ SourcePolicy              源策略（"该用哪个源、还能不能用"）       │
│   · 候选源排序（偏好 + 冷却）                                   │
│   · 健康度：连续失败计数 / 冷却时间                              │
│   · 主机级并发门（镜像 8 / 官方 32）                             │
│   · 官方 ↔ 镜像 URL 映射                                       │
├──────────────────────────────────────────────────────────────┤
│ DownloadEngine            下载原语（"怎么把一个文件下对"）         │
│   · 建连看门狗 / 逐读看门狗 / 失速判据                          │
│   · 错误分类（404/403/429/5xx/DNS/失速/校验/磁盘）              │
│   · 小文件内存直写 + 边下边算 SHA1 + 原子落盘                    │
│   · 共享 HttpClient（含自适应代理）                             │
├──────────────────────────────────────────────────────────────┤
│ ProxyEnvironment          网络环境（"能不能直连"）               │
│   · 系统代理探测 / 直连降级 / AdaptiveProxy                     │
└──────────────────────────────────────────────────────────────┘
```

**职责边界（重要）**：
- `DownloadEngine` **只管一个 URL 的一次下载**，不关心"源好不好"、"要不要重试"；
- `SourcePolicy` **只管源的选择与健康度**，不碰网络；
- `GameInstaller` 负责把两者拼成"多源失败兜底"的业务流程。

## 4.2 装一个版本的完整时序

```
GameInstaller.InstallVanillaAsync(version, targetPath, maxConcurrency, progress, ct)
│
├─ 1. SourcePolicy.Reset()                    清空上次运行的冷却/失败计数
├─ 2. 下载 版本 json（Meta）                   失败 → 抛异常（无法继续）
├─ 3. 解析 → 收集 FileTask：
│       client.jar（Meta）
│       libraries（Maven，按 rules 过滤 OS）
├─ 4. 下载 asset index（Meta）→ 展开 objects（Assets，数千个）
│       失败 → 只记日志、跳过 assets（不整体失败）
├─ 5. 统计 totalFiles / totalBytes
├─ 6. 启动进度线程（每 500ms 采样 GlobalBytesDownloaded 算速度）
├─ 7. 为每个文件建 Task，用全局 SemaphoreSlim(maxConcurrency) 控并发
│       每文件：
│         a) 已存在且 SHA1 通过 → 跳过（计入 skipped）
│         b) candidates = SourcePolicy.GetCandidates(officialUrl, kind)
│         c) 逐源尝试（冷却中的源跳过；若全冷却则被迫全试）
│              · 成功 → ReportSuccess → 计数完成
│              · 失败 → ReportFailure → 按错误类型决定"同源重试一次"或"立刻换源"
│         d) 两轮（第二轮 = PCL 的"全源降级重试一次"）
│         e) 仍失败 → 计入 failed + 聚合失败类型
├─ 8. Task.WhenAll → 停进度线程
├─ 9. 写聚合日志（失败分布 + 源健康状态）
└─ 10. 返回 InstallResult { TotalFiles, CompletedFiles, FailedFiles, SkippedFiles, Success }
```

## 4.3 错误分类与处置矩阵

`DownloadFailureKind` 是整条链路的"共同语言"：

| 类型 | 触发条件 | `GameInstaller` 的处置 | `SourcePolicy` 的影响 |
|---|---|---|---|
| `NotFound` | 404 / 410 | **立刻换源**，不在本 URL 重试 | 不惩罚源（是单文件问题） |
| `Forbidden` | 401 / 403 / 451 | **立刻换源** | 不惩罚源 |
| `RateLimited` | 429 | 读 `Retry-After` → **换源** | **该源冷却** `max(10s, Retry-After)`，上限 5 min |
| `Transient` | 5xx / 连接中断 / 长度不符 | **同源重试 1 次**，再换源 | 连续失败累计，达阈值进冷却 |
| `Corrupt` | SHA1 不匹配 | 换源重下 | 连续失败累计 |
| `Stall` | 建连超时 / 逐读无数据 / 低于速率下限 | **立刻换源** | 连续失败累计 |
| `Dns` | NXDOMAIN（`HttpRequestError.NameResolutionError`） | **立刻换源**（不重试） | 连续失败累计 |
| `Fatal` | 磁盘空间不足 / 无权限 | **直接失败**（换源无用） | 不影响源 |

> 设计要点：**"能换源就换源，不能换源才重试"**。这是与改造前最大的语义差别 ——
> 改造前所有非 404/403 的错误都会在同一个坏 URL 上重试 3 次。

## 4.4 关键参数表（要调手感只看这张表）

| 参数 | 值 | 位置 | 说明 |
|---|---|---|---|
| `ConnectTimeoutMs` | 8000 | `DownloadEngine` | 建连 + 等响应头（含 302 第二跳） |
| `ReadIdleTimeoutMs` | 15000 | `DownloadEngine` | **逐读**无数据超时（每次 `ReadAsync` 单独计时） |
| `MinSpeedBytesPerSec` | 24576（24 KB/s） | `DownloadEngine` | 失速速率下限 |
| `SpeedWindowMs` | 10000 | `DownloadEngine` | 失速判定的观测窗口 |
| `SmallFileThreshold` | 4 MB | `DownloadEngine` | 小于此走内存缓冲、一次落盘 |
| `BufferSize` | 81920 | `DownloadEngine` | 读缓冲 |
| `MaxAttemptBytes` | 4 GB | `DownloadEngine` | 单文件兜底上限 |
| `MirrorConcurrency` | 8 | `SourcePolicy` | 镜像主机级并发上限（PCL 对 bmclapi 的纪律） |
| `OfficialConcurrency` | 32 | `SourcePolicy` | 官方源并发上限 |
| `MirrorFailThreshold` | 2 | `SourcePolicy` | 镜像连续失败几次进冷却 |
| `OfficialFailThreshold` | 3 | `SourcePolicy` | 官方连续失败几次进冷却 |
| `MirrorCooldown` | 60 s | `SourcePolicy` | 镜像冷却基数（失败越多越久） |
| `OfficialCooldown` | 20 s | `SourcePolicy` | 官方冷却基数 |
| `MaxCooldown` | 5 min | `SourcePolicy` | 冷却上限 |
| `PerSourceAttempts` | 2 | `GameInstaller` | 单个源上对 `Transient` 的最大尝试次数 |
| `MaxFailureLogLines` | 50 | `GameInstaller` | 失败明细日志上限 |
| `AttemptTimeout` | 10 s | `VersionService` | 版本清单单源单次超时 |

## 4.5 公开 API 与调用示例

### 4.5.1 装一个完整版本（UI 用法）

```csharp
using Project.Launch.Download;

var progress = new Progress<DownloadProgress>(p =>
{
    statusText.Text = p.State;              // "正在下载游戏文件..." / "部分文件失败（N 个）"
    bar.Value       = p.Percentage;         // 0~100
    speedText.Text  = $"{p.SpeedBytesPerSec / 1024} KB/s";
    filesText.Text  = $"{p.CompletedFiles}/{p.TotalFiles}（失败 {p.FailedFiles}）";
});

using var cts = new CancellationTokenSource();

InstallResult result = await GameInstaller.InstallVanillaAsync(
    version:        versionEntry,   // 来自 VersionManifest.Versions
    targetPath:     @"E:\Minecraft",
    maxConcurrency: 32,
    progress:       progress,
    ct:             cts.Token);

if (result.Success) { /* 只有真正全部成功才当成功 */ }
else
{
    // 例：共 3663 个文件：成功 3650，跳过（已存在）13，失败 13
    Log($"安装未完成：{result.Summary}");
}
```

**返回的 `InstallResult`**：

| 属性 | 含义 |
|---|---|
| `TotalFiles` / `TotalBytes` | 本次清单的规模 |
| `CompletedFiles` | 成功（含跳过） |
| `SkippedFiles` | 已存在且 SHA1 通过而跳过 |
| `FailedFiles` | 失败 |
| `DownloadedBytes` | 实际下载字节（含跳过文件的大小） |
| `Success` | `TotalFiles > 0 && CompletedFiles == TotalFiles && FailedFiles == 0` |
| `Summary` | 一行中文摘要，可直接写日志/提示 |

### 4.5.2 只下单个文件（带 SHA1 校验）

```csharp
DownloadAttemptResult r = await DownloadEngine.DownloadAsync(
    url:          "https://resources.download.minecraft.net/00/0056c1a9...",
    localPath:    @"E:\Minecraft\assets\objects\00\0056c1a9...",
    expectedSha1: "0056c1a9...",     // 可空：为空则只校验完整长度
    ct:           ct);

if (r.IsSuccess)
    Console.WriteLine($"OK，{r.BytesReceived} 字节");
else
    Console.WriteLine($"{r.Kind}: {r.Message}");   // 用 Kind 决定后续动作
```

> ⚠️ **单文件下载不做镜像回退**。它只打你给的这一个 URL。
> 要"多源兜底"请用 `GameInstaller`，或自己按 `SourcePolicy.GetCandidates` 迭代。

### 4.5.3 校验本地已有文件（断点续传的基础）

```csharp
bool ok = await DownloadEngine.VerifySha1Async(localPath, expectedSha1, ct);
if (!ok) File.Delete(localPath);   // 坏文件要删掉，否则会一直被当成"已存在"
```

### 4.5.4 手动控制源顺序

```csharp
// 默认 Auto：官方优先；官方连续失败后自动切镜像
SourcePolicy.Preference = SourcePreference.Auto;

// 强制镜像优先（国内无代理用户可能更快，但实测当前 BMCLAPI 的 assets 节点池很差）
SourcePolicy.Preference = SourcePreference.MirrorFirst;

// 强制官方优先
SourcePolicy.Preference = SourcePreference.OfficialFirst;

// 诊断：看当前各源健康状态
Log(SourcePolicy.DescribeHealth());   // "mirror:Assets: 连续失败 2，冷却剩余 58s; ..."
```

### 4.5.5 拉版本清单

```csharp
VersionManifest? manifest = await VersionService.GetVersionManifestAsync(ct);
// manifest.Latest.Release / manifest.Versions[]
// VersionEntry { VersionId, Type, Url, ReleaseTime, IconPath, IconBitmap }
```

## 4.6 源策略与健康度（`SourcePolicy`）

### 4.6.1 候选源怎么排

1. 以**官方 URL** 为准（所有 `FileTask` 都记官方 URL），用 `ToMirror()` 派生镜像 URL；
2. 按 `Preference` 决定谁先；
3. **冷却中的源沉底**（但仍保留在列表里）；
4. 若两个源都在冷却 → 全都保留（避免无源可用）。

**URL 映射规则**：

| 官方 | 镜像（BMCLAPI） |
|---|---|
| `resources.download.minecraft.net/...` | `bmclapi2.bangbang93.com/assets/...` |
| `libraries.minecraft.net/...` | `bmclapi2.bangbang93.com/maven/...` |
| `piston-meta.mojang.com/...` | `bmclapi2.bangbang93.com/...` |
| `piston-data.mojang.com/...` | `bmclapi2.bangbang93.com/...` |
| `launchermeta.mojang.com/...`、`launcher.mojang.com/...` | 同上 |

### 4.6.2 健康度是怎么算的

- **分桶键** = `Id + ":" + Kind`，即 `mirror:Assets`、`mirror:Maven`、`official:Meta` 各自独立。
  （因为 **maven 的镜像和 assets 的镜像质量完全不同**：实测 maven 走 `mirrors.cernet.edu.cn` 是好的，assets 的 sakura 节点池很差。）
- 成功一次 → `FailCount` 清零、冷却清除；
- 失败 → `FailCount++`；达阈值 → 冷却（失败越多，冷却越久，上限 5 min）；
- `NotFound` / `Forbidden` / `Fatal` **不惩罚源**（是单文件或磁盘问题）。

### 4.6.3 主机级并发门

`AcquireAsync(source, ct)` 返回一个 `IDisposable`，**必须 `using`**：

```csharp
using (await SourcePolicy.AcquireAsync(source, ct))
{
    result = await DownloadEngine.DownloadAsync(source.Url, localPath, sha1, ct);
}
```

镜像 8 / 官方 32 的上限用于**避免把唯一国内镜像打成 403/429**（PCL 的对应做法是
"禁止对 bmclapi 追加第 2 条连接 + 每次启动 Sleep(100)"）。

## 4.7 代理自适应（`ProxyEnvironment`）

**为什么需要**：Windows 的 WinINET 代理是"全局开关"，用户关掉代理客户端后开关**常常还留着**。
.NET 默认继承它 → 所有请求被送去一个没人监听的端口 → 表现成"网络全挂了"。

**三层保护**：

| 层 | 时机 | 行为 |
|---|---|---|
| 启动探测 | `Program.Main` / `DownloadEngine` 静态构造 | 代理指向本机且端口无监听 → 直接禁用代理 |
| 每请求 | `AdaptiveProxy.GetProxy` | 实时读取 `ProxyEnvironment.IsEnabled`，禁用后立即走直连 |
| 运行时兜底 | 请求失败时 | 失败信息命中"代理端点 / 连接被拒 / 本机代理建连超时" → 禁用代理 + **直连重试一次** |

**日志关键字**：`[网络] ...`（在 `app.log` 里）

## 4.8 扩展指南

### 加一个镜像源

目前 `ToMirror()` 是"官方 → BMCLAPI"的单映射。要支持多镜像：

1. 把 `ToMirror` 改成返回 `IEnumerable<string>`（多个镜像 URL）；
2. `GetCandidates` 里为每个镜像构造 `DownloadSource`（`Id` 用 `mirror:名称`，健康度自动分桶）；
3. 其余逻辑无需改动（换源、冷却、并发门都是按 `DownloadSource` 工作的）。

### 加一种错误分类

1. `DownloadFailureKind` 加枚举值；
2. `DownloadEngine` 里识别并返回它；
3. `SourcePolicy.ReportFailure` 里决定是否惩罚源；
4. `GameInstaller.DownloadOneAsync` 里决定"重试 / 换源 / 直接失败"。

### 给大文件加 Range 分片（待办）

落点在 `DownloadEngine.DownloadCoreAsync`：在拿到 `Content-Length` 后判断
`contentLength > SmallFileThreshold 且支持 Range` → 走分片分支：

- 预分配文件（`FileStream.SetLength`）；
- 每片独立 `HttpRequestMessage` + `Range: bytes=a-b`；
- 用 `RandomAccess.WriteAsync(handle, buffer, offset, ct)` 并发写同一个文件；
- 严格校验 `206` + `Content-Range` 三元组；
- 每片独立看门狗（复用现有 `ReadWithIdleTimeoutAsync`）；
- 单片失败只重切该片，不要整体重来。

### 调手感

只改 [4.4 参数表](#44-关键参数表要调手感只看这张表) 里的常量即可，不要散落在业务代码里。

## 4.9 不变式与常见误用

**必须遵守的不变式：**

1. `DownloadEngine.GlobalBytesDownloaded` = **已提交 + 在途**；失败尝试会**回滚**。
   它只用于测速/进度，业务判断请用 `InstallResult`。
2. **全局门 → 主机门** 的获取顺序固定（先 `SemaphoreSlim` 后 `SourcePolicy.AcquireAsync`），
   两者都必须释放（`using` / `finally`）。
3. `DownloadEngine.DownloadAsync` **内部已含建连/逐读/失速三重超时**，
   **外层不要再包 `CancellationTokenSource.CancelAfter` 做整体超时**（会误杀大文件）。
4. 小文件（<4 MB）走内存直写；大文件走 `localPath + ".tmp"` 再 `File.Move`。
   **失败路径会删除 `.tmp`**，所以**不要**在外部假设 `.tmp` 一定存在。
5. 已有文件必须 **SHA1 校验通过** 才算"已存在"；坏文件要先删再下。

**常见误用：**

| 误用 | 后果 |
|---|---|
| 给 `HttpClient` 设 `Timeout = 15s` | 大文件被腰斩（本项目已改为 `InfiniteTimeSpan` + 逐读看门狗） |
| 在 `GameInstaller` 外自己重试循环 | 会和源健康度打架，冷却失效 |
| `NotFound` 也重试 3 次 | 白等（实测最坏 90 s/文件） |
| 用 `File.Exists` 判断"已下载" | 坏文件永远修不好 |
| 直接 `new HttpClient()` | 连接池失效、不走代理策略；请用 `DownloadEngine.SharedClient` |

## 4.10 排查手册（看哪条日志）

`MainSrc\BuiltDebugRun\KilimLauncher\app.log`：

| 日志关键字 | 含义 |
|---|---|
| `[网络] ...` | 代理决策（用/不用代理、何时切的直连） |
| `成功获取版本清单（official/mirror...）` | 元数据走的是哪个源 |
| `开始下载 <版本>：N 个文件，约 M MB，并发 X，源偏好 Y` | 安装开始 |
| `下载结束：共 N 个文件：成功 A，跳过（已存在）B，失败 C` | 安装结果（**这是唯一可信的完成度**） |
| `下载结束但仍有 N 个文件失败。失败类型分布：mirror/Stall×12，official/Transient×3` | 失败归因（按 源/错误类型 聚合） |
| `失败文件: assets/objects/xx/yyy <- mirror: Stall ...` | 单文件明细（最多 50 条） |
| `源健康状态: ...` | 结束时各源的失败计数与冷却 |
| `UI 线程未处理异常（已拦截，程序继续运行）: ...` | 界面事件里的异常（已被全局守卫兜住） |

**复现 A/B 的最小实验**（不改代码即可判断"是不是源的问题"）：

```powershell
# 官方源：24 个真实 asset，并发 20
curl.exe --parallel --parallel-max 20 -sL -o NUL -w "%{http_code} %{size_download}\n" <url1> <url2> ...

# 同一个文件对比两个源
curl.exe -sL -o NUL --max-time 30 -w "official: %{http_code} ttfb=%{time_starttransfer} size=%{size_download}\n" `
  "https://resources.download.minecraft.net/<p>/<hash>"
curl.exe -sL -o NUL --max-time 30 -w "mirror  : %{http_code} ttfb=%{time_starttransfer} size=%{size_download}\n" `
  "https://bmclapi2.bangbang93.com/assets/<p>/<hash>"
```

判据：**`size` 小于预期 = 被截断；`ttfb` > 10 s = 该源当天不可用**。

---

# 第五部分：其余模块现状

## 5.1 账户系统 ✅

- **离线登录**：用户名 → `OfflineUuidGenerator`（`MD5("OfflinePlayer:"+name)`，置 version 3、IETF variant，手拼大端 hex，兼容 HMCL / Java 服务端）；
- **微软正版**：`MicrosoftAuthFlow` 七阶段
  `设备代码 → 轮询 → XBL → XSTS → MC token → profile → AccountInfo`；
  设备代码流用 `consumers` 端点 + `XboxLive.signin XboxLive.offline_access`；
  XSTS 有 `XErr` 人类可读错误映射（无 Xbox 账号 / 地区不支持 / 未成年需家庭组…）；
- **持久化**：`AccountService` → `accounts.json`，**原子写**（写 `.tmp` 再 `File.Move` 覆盖）；
- **第三方登录**：UI 有入口，后端未实现（占位）。

## 5.2 游戏启动 ✅（导入式）

`GameLauncher.LaunchAsync` 已实现：
拼 classpath（`client.jar` + `libraries`，按 `rules` 过滤 OS）、解压 natives、
**新旧两代参数格式**（`arguments.game/jvm` 与 `minecraftArguments`）、
`features` 规则（要求 feature=true 的一律拒绝）、进程保活列表防 GC、
3 秒闪退检测并回读 `game.log` 尾部写入 `app.log`。

**占位符现状**（关键）：

| 占位符 | 值 |
|---|---|
| `${auth_player_name}` | 账户用户名 |
| `${auth_uuid}` | 离线/正版 UUID |
| **`${auth_access_token}`** | **固定 `"0"`** ← 正版未打通 |
| `${auth_session}` | 固定 `"0"` |
| `${user_type}` | 固定 `"legacy"` |
| `${version_name}` / `${game_directory}` / `${assets_root}` | 正确 |
| `${resolution_width/height}` | 固定 925×530 |

## 5.3 Java 检测 ✅

`JAVA_HOME` / `PATH` / 6 个常见 JDK 目录 / 已保存路径 → 四路扫描，
`java -version` 解析兼容 `1.8.0_x` 与 `21.0.1`，
匹配策略：**精确版本 → 最小且 ≥ 要求的版本**。

> 已知小缺陷：`WaitForExit(5000)` 在 `ReadToEndAsync` 之后是死代码；`java.exe` 挂起会卡住。

## 5.4 实例管理 ✅

- `MinecraftDirectoryAnalyzer` 识别两种布局：
  `standard`（`versions/` 在 `.minecraft` 根下）/ `isolated`（`versions/` 在 `.minecraft/` 下）；
- `InstanceService` → `instances.json`（原子写），记录 `RootPath / Layout / VersionId / JavaPath`。

## 5.5 UI 交互 ✅

- 主窗 5 Tab：下载 / 启动 / 主页 / 设置 / 工具，默认停在**主页（index=2）**；
- 自绘标题栏两态（渐变 ↔ 透明），图标黑/白自动切换按梯度亮度计算；
- **滑块动画范式**：EaseInOut + 梯形缩放 + 三角形模糊，参数全在 `AnimationConfig`；
- 底部导航 / 版本选择器 / 登录三模式都用同一套滑块动画。

## 5.6 ⚠️ 已知缺陷（除 3.7 待办外）

| 级别 | 问题 |
|---|---|
| 🔴 | 正版 token 未传给游戏进程（`${auth_access_token}` 写死 `"0"`） |
| 🟠 | `accounts.json` 解析失败会静默返回空并**随后覆盖写回** → 抹掉全部账户 |
| 🟠 | `natives/classifiers` 从未下载（`GameLauncher` 只解压已存在的 natives jar） |
| 🟠 | `SkinCropper` / `CropSkinFromBitmap` 用裸 `catch` 吞异常，无日志 |
| 🟡 | `Glass.axaml` 是空转的：真实玻璃参数内联在各窗口 XAML |
| 🟡 | `avares://` 路径写错不编译报错，多处加载点 `try/catch` 吞掉 |
| 🟡 | 数据目录在 exe 同级，装到 `Program Files` 后所有保存静默失效 |
| ⚪ | `MainSrc\Codes\` 空目录；`read.md` / `read.me` 混用 |

## 5.7 液态玻璃子系统

vendored 第三方库（作者 KaranocaVe，MIT），在 `MainSrc\Tools\LiquidGlassAvaloniaUI\`，
**独立 csproj、独立编译**，主项目 `<Compile Remove>` 排除源码后 `ProjectReference` 引用。

机制：`LiquidGlassSurface.Render()`
→ `LiquidGlassBackdropProvider.EnsureSnapshot()` 抓窗口背景快照
→ `LiquidGlassDrawOperation` 用 SKSL 着色器做折射/色散/模糊/高光。

**历史坑**：曾出现"背景快照是一张全透明空图"导致玻璃完全不可见，排查记录见 `GLASS_NEGATIVE_FINDINGS.md`。

## 5.8 3D 皮肤

`Pivot3DControl`（`ICustomDrawOperation` + SkiaSharp `DrawVertices`）+
`MinecraftSkinModel`（盒子模型 / UV 映射）+ `Pivot3DRenderer`。
支持鼠标拖拽旋转，灵敏度与方向反转在 `InteractionConfig`。

# 第六部分：完整源码

> **生成方式**：本节由脚本从磁盘直接读取源码生成（见 7.5），未手工搬运。
> 如果它与磁盘上的文件不一致，**以磁盘为准**，并重新生成本节。

## 6.0 规模统计

| 分组 | 文件数 | 行数 |
|---|---|---|
| 6.1 根目录 | 7 | 398 |
| 6.2 MainSrc\Download —— 下载引擎 | 6 | 1665 |
| 6.3 MainSrc\Auth —— 微软正版登录 | 9 | 1093 |
| 6.4 MainSrc\Views —— 界面层 | 11 | 4070 |
| 6.5 MainSrc\Tools\JavaConfig —— 启动内核 | 9 | 1098 |
| 6.6 MainSrc\Tools\Player3DConfig —— 3D 皮肤 | 3 | 760 |
| 6.7 MainSrc\Tools 其余（AnimSettings / AppConstants / LogsConfig / SoundControl） | 5 | 316 |
| 6.8 MainSrc\Tools\LiquidGlassAvaloniaUI —— 液态玻璃库（vendored, MIT） | 20 | 4413 |
| 6.9 MainSrc\Codes | 1 | 4 |
| **合计** | **71** | **约 13817** |

## 6.1 根目录

### `Program.cs`

```csharp
using System;
using System.IO;
using System.Threading;
using Avalonia;
using Project.Launch.Tools;

using Project.Launch.Tools.JavaConfig;
using Project.Launch.Tools.LogsConfig;
using Project.Launch.Download;
namespace Project.Launch
{
    class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            try
            {
                LogHelper.Write(LauncherPaths.StartupLog,
                    "程序入口执行", "Program entry executed");

                // 启动即探测系统代理：代理客户端没运行（但注册表仍开着代理）时自动改直连，
                // 否则所有请求都会被送去一个没人监听的本地端口，表现为「获取版本列表失败」
                ProxyEnvironment.EnsureInitialized();

                BuildAvaloniaApp()
                    .StartWithClassicDesktopLifetime(args);
            }
            catch (Exception ex)
            {
                try
                {
                    File.WriteAllText(LauncherPaths.CrashLog,
                        $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\n\n{ex}");
                }
                catch { }

                Console.WriteLine("=== CRASH ===");
                Console.WriteLine(ex.ToString());
                Console.WriteLine("=============");
                Console.WriteLine("崩溃日志已写入 / Crash log written: " + LauncherPaths.CrashLog);
                Console.ReadKey();
                Environment.Exit(1);
            }
        }

        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
#if DEBUG
                .WithDeveloperTools()
#endif
                .WithInterFont()
                .LogToTrace();
    }
}
```

### `App.axaml`

```xml
<Application xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="Project.Launch.App"
             RequestedThemeVariant="Default">

    <Application.Styles>
        <FluentTheme />
        <!-- 主题色 + Miret 样式 -->
        <StyleInclude Source="avares://Project.Launch/MainSrc/Views/Styles.axaml" />
        <!-- ★ 液态玻璃参数表 + Glass 按钮样式 -->
        <StyleInclude Source="avares://Project.Launch/MainSrc/Views/Glass.axaml" />
    </Application.Styles>
</Application>
```

### `App.axaml.cs`

```csharp
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Project.Launch.Views;
using System;
using Project.Launch.Tools;

using Project.Launch.Auth;
using Project.Launch.Tools.JavaConfig;
using Project.Launch.Tools.LogsConfig;
namespace Project.Launch
{
    public partial class App : Application
    {
        public static bool LoginCheck { get; set; }
        public static bool IsLoggedIn { get; set; }
        public static string? PlayerName { get; set; }
        public static string? OfflineUuid { get; set; }

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);

            InstallCrashGuards();
        }

        /// <summary>
        /// 全局异常兜底。
        /// 之前 MainWindow 的 async void 事件处理器里一旦抛异常（例如文件夹选择器内部报错），
        /// 异常会回到 Dispatcher 并终止整个进程 —— 用户看到的是「窗口无响应然后崩溃」。
        /// 这里把 UI 线程上的未处理异常记录后吞掉，保证单个交互出错不会带走整个启动器。
        /// </summary>
        private static void InstallCrashGuards()
        {
            Avalonia.Threading.Dispatcher.UIThread.UnhandledException += (_, e) =>
            {
                try
                {
                    LogHelper.Write(LauncherPaths.AppLog,
                        $"UI 线程未处理异常（已拦截，程序继续运行）: {e.Exception}",
                        $"Unhandled UI exception (suppressed): {e.Exception}");

                    System.IO.File.AppendAllText(LauncherPaths.CrashLog,
                        $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\n[UI-Thread Suppressed]\n{e.Exception}\n\n");
                }
                catch { }

                // 标记为已处理：避免单个事件处理器异常把整个应用带崩
                e.Handled = true;
            };

            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                try
                {
                    System.IO.File.AppendAllText(LauncherPaths.CrashLog,
                        $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\n[AppDomain]\n{e.ExceptionObject}\n\n");
                }
                catch { }
            };

            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (_, e) =>
            {
                try
                {
                    LogHelper.Write(LauncherPaths.AppLog,
                        $"未观察的任务异常: {e.Exception?.GetBaseException().Message}",
                        $"Unobserved task exception: {e.Exception?.GetBaseException().Message}");
                }
                catch { }

                e.SetObserved();
            };
        }

        public override void OnFrameworkInitializationCompleted()
        {
            LogHelper.Write(LauncherPaths.AppLog,
                "应用初始化开始", "Application initialization started");

            var account = AccountService.GetSelectedAccount();
            if (account != null)
            {
                PlayerName = account.Username;
                OfflineUuid = account.Uuid;
                IsLoggedIn = true;
                LoginCheck = AccountService.GetLoginCheck();

                LogHelper.Write(LauncherPaths.AppLog,
                    $"已加载账户: {PlayerName}，跳过登录: {LoginCheck}",
                    $"Account loaded: {PlayerName}, LoginCheck: {LoginCheck}");
            }
            else
            {
                PlayerName = null;
                OfflineUuid = null;
                IsLoggedIn = false;
                LoginCheck = false;

                AccountService.SetLoginCheck(false);

                LogHelper.Write(LauncherPaths.AppLog,
                    "未找到账户，LoginCheck 设为 false",
                    "No account found, LoginCheck set to false");
            }

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new MainWindow();
                base.OnFrameworkInitializationCompleted();
            }

            LogHelper.Write(LauncherPaths.AppLog,
                "应用初始化完成", "Application initialization complete");
        }
    }
}
```

### `Project.Launch.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ApplicationManifest>app.manifest</ApplicationManifest>
    <ApplicationIcon>MainSrc\Views\imgs\Main\images\Launcher\MainLogo.ico</ApplicationIcon>

    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>

    <RuntimeIdentifiers>win-x64</RuntimeIdentifiers>
    <PublishSingleFile>true</PublishSingleFile>
    <SelfContained>true</SelfContained>
    <EnableCompressionInSingleFile>true</EnableCompressionInSingleFile>
    <DebugType>none</DebugType>
    <PublishReadyToRun>false</PublishReadyToRun>
    <PublishTrimmed>false</PublishTrimmed>
    <IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
    <SatelliteResourceLanguages>zh-Hans;en</SatelliteResourceLanguages>
  </PropertyGroup>

  <!-- ★ 新增：指定 Debug 构建输出到 MainSrc\BuiltDebugRun\ -->
  <PropertyGroup Condition="'$(Configuration)' == 'Debug'">
    <OutputPath>MainSrc\BuiltDebugRun\</OutputPath>
    <!-- 去除中间目录，让 exe 和 dll 直接铺在 BuiltDebugRun 根目录 -->
    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
    <AppendRuntimeIdentifierToOutputPath>false</AppendRuntimeIdentifierToOutputPath>
  </PropertyGroup>

  <ItemGroup>
    <!-- ★ 排除「非项目源码目录」：若在项目内放置第三方/参考资料源码，
         默认通配（**/*.cs、**/*.resx）会把它们吸进本项目编译与资源处理，
         导致构建失败或类型冲突。该目录删除后，下面这 6 行可一并移除。 -->
    <Compile Remove="pclsource\**" />
    <AvaloniaXaml Remove="pclsource\**" />
    <AvaloniaResource Remove="pclsource\**" />
    <EmbeddedResource Remove="pclsource\**" />
    <None Remove="pclsource\**" />
    <Content Remove="pclsource\**" />

    <!-- ★ 玻璃库已搬到 MainSrc\Tools\LiquidGlassAvaloniaUI\（2026-10 架构调整）
         必须排除它的源码与 obj，否则会被主项目的 **/*.cs 通配吸进来，
         与主项目自己的 obj/AssemblyInfo.cs 撞成 CS0579（程序集特性重复） -->
    <Compile Remove="MainSrc\Tools\LiquidGlassAvaloniaUI\**" />
    <AvaloniaXaml Remove="MainSrc\Tools\LiquidGlassAvaloniaUI\**" />
    <AvaloniaResource Remove="MainSrc\Tools\LiquidGlassAvaloniaUI\**" />
    <EmbeddedResource Remove="MainSrc\Tools\LiquidGlassAvaloniaUI\**" />
    <None Remove="MainSrc\Tools\LiquidGlassAvaloniaUI\**" />

    <!-- ★ 新增：排除输出目录，防止生成的 .cs 被重复编译导致 CS0579 错误 -->
    <Compile Remove="MainSrc\BuiltDebugRun\**" />
    <AvaloniaXaml Remove="MainSrc\BuiltDebugRun\**" />
    <AvaloniaResource Remove="MainSrc\BuiltDebugRun\**" />
    <EmbeddedResource Remove="MainSrc\BuiltDebugRun\**" />
    <None Remove="MainSrc\BuiltDebugRun\**" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Avalonia" Version="12.1.2" />
    <PackageReference Include="Avalonia.Desktop" Version="12.1.2" />
    <PackageReference Include="Avalonia.Themes.Fluent" Version="12.1.2" />
    <PackageReference Include="Avalonia.Fonts.Inter" Version="12.1.2" />
    <PackageReference Include="AvaloniaUI.DiagnosticsSupport" Version="2.2.3">
      <IncludeAssets Condition="'$(Configuration)' != 'Debug'">None</IncludeAssets>
      <PrivateAssets Condition="'$(Configuration)' != 'Debug'">All</PrivateAssets>
    </PackageReference>

    <PackageReference Include="NAudio" Version="2.2.1" />

    <PackageReference Include="SkiaSharp" Version="3.119.4" />

    <ProjectReference Include="MainSrc\Tools\LiquidGlassAvaloniaUI\LiquidGlassAvaloniaUI.csproj" />
  </ItemGroup>

  <ItemGroup>
    <!-- ★ 用 Update（不是 Include），避免和默认 glob 重复 -->
    <AvaloniaXaml Update="App.axaml" />
    <AvaloniaXaml Update="MainSrc\Views\LoginWindow.axaml" />
    <AvaloniaXaml Update="MainSrc\Views\MainWindow.axaml" />
    <AvaloniaXaml Update="MainSrc\Views\AccountManagerWindow.axaml" />
    <AvaloniaXaml Update="MainSrc\Views\DownloadWindow.axaml" />

    <!-- 样式：作为 AvaloniaResource 读，避免被当独立 XAML 编译 -->
    <AvaloniaResource Include="MainSrc\Views\Styles.axaml" />
    <AvaloniaResource Include="MainSrc\Views\Glass.axaml" />

    <!-- 图片和音效 -->
    <AvaloniaResource Include="MainSrc\Views\imgs\**" Exclude="MainSrc\Views\imgs\Main\images\Launcher\MainLogo.ico" />
    <AvaloniaResource Include="MainSrc\Sound\**" />

    <!-- 任务栏图标 -->
    <Content Include="MainSrc\Views\imgs\Main\images\Launcher\MainLogo.ico">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </Content>

    <Compile Update="MainSrc\Views\MainWindow.cs">
      <DependentUpon>MainWindow.axaml</DependentUpon>
    </Compile>
    <Compile Update="App.axaml.cs" />
  </ItemGroup>
</Project>
```

### `app.manifest`

```xml
<?xml version="1.0" encoding="utf-8"?>
<assembly manifestVersion="1.0" xmlns="urn:schemas-microsoft-com:asm.v1">
  <!-- This manifest is used on Windows only.
       Don't remove it as it might cause problems with window transparency and embedded controls.
       For more details visit https://learn.microsoft.com/en-us/windows/win32/sbscs/application-manifests -->
  <assemblyIdentity version="1.0.0.0" name="Project.Launch.Desktop"/>

  <compatibility xmlns="urn:schemas-microsoft-com:compatibility.v1">
    <application>
      <!-- A list of the Windows versions that this application has been tested on
           and is designed to work with. Uncomment the appropriate elements
           and Windows will automatically select the most compatible environment. -->

      <!-- Windows 10 -->
      <supportedOS Id="{8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a}" />
    </application>
  </compatibility>
</assembly>
```

### `.gitignore`

```text
# ============================================================
# Kilim Minecraft Launcher — 忽略规则
# 目标：构建产物、发布产物、IDE 配置、运行日志一律不入库
# ============================================================

# ---------- 构建输出（任意层级的 bin / obj 都忽略）----------
bin/
obj/

# ---------- Debug 构建输出目录 ----------
# csproj 里 Debug 的 OutputPath 指向 MainSrc\BuiltDebugRun\
BuiltDebugRun/

# ---------- 发布产物 ----------
# 含 exe / dll / pdb，历史上一度达到 132 MB，绝对不入库
publish/

# ---------- IDE / 编辑器 ----------
.vscode/
.vs/
.idea/
*.user
*.suo

# ---------- 运行日志 ----------
# 运行时数据目录（exe 同级的 KilimLauncher\）与各类日志文件
KilimLauncher/
*.log

# ---------- .NET 临时文件 ----------
*.tmp
*.cache
project.assets.json
project.nuget.cache
```

### `README.md`

```markdown
# Kilim Minecraft Launcher
--By Erics  九条开源私人仓库
A next-gen cross-platform Minecraft launcher written in C# and Avalonia. Clean-slate architecture, native performance, zero WebView overhead.

# 隐私政策
Kilim Minecraft Launcher（以下简称"本启动器"）尊重并保护用户隐私。
我们收集的信息
本启动器在运行过程中可能接触以下信息：
微软账户认证信息：通过微软官方 OAuth 2.0 设备代码流（Device Code Flow）获取的访问令牌（Access Token）与刷新令牌（Refresh Token），仅用于验证您的正版游戏所有权。
本地配置数据：游戏安装路径、Java 路径、内存分配设置、版本列表等，仅存储在您的计算机本地。
日志文件：运行日志可能包含系统版本、错误堆栈等信息，用于排查问题。
信息存储与传输
所有认证令牌与配置文件仅保存在您的本地设备，我们不会将您的账户信息上传至任何第三方服务器。
本启动器仅与以下官方服务器通信：
微软认证服务（login.microsoftonline.com）
Xbox Live / XSTS 服务（user.auth.xboxlive.com, xsts.auth.xboxlive.com）
Mojang 官方 API（api.minecraftservices.com）
Minecraft 官方资源服务器（piston-meta.mojang.com, resources.download.minecraft.net）
我们不做的事
我们不会收集、存储或分享您的微软账户密码。
我们不会将您的任何个人数据用于广告、分析或商业用途。
我们不会将数据出售给第三方。
第三方服务
本启动器使用微软与 Mojang 的官方 API 进行身份验证与游戏资源下载。这些服务的隐私实践受其各自政策约束：
微软隐私声明
Mojang 隐私政策
免责声明
本启动器为非官方开源项目，与 Mojang Studios、Microsoft 或其关联公司无任何隶属关系。本启动器不对因使用本软件导致的任何账户问题、数据丢失或系统损坏承担责任。使用本启动器即表示您同意自行承担风险。
政策更新
我们可能随时更新本政策，更新后将在本页面公布。继续使用本启动器即视为接受更新后的政策。
# Privacy Policy
Kilim Minecraft Launcher (the "Launcher") respects and protects user privacy.
Information We Collect
The Launcher may access the following information during operation:
Microsoft Account Authentication Data: Access tokens and refresh tokens obtained via Microsoft's official OAuth 2.0 Device Code Flow, used solely to verify your ownership of a legitimate Minecraft license.
Local Configuration Data: Game installation paths, Java paths, memory allocation settings, and version lists, stored exclusively on your local machine.
Log Files: Runtime logs may include system version and error stack traces for troubleshooting purposes.
Data Storage & Transmission
All authentication tokens and configuration files are stored locally on your device. We do not upload your account information to any third-party servers.
The Launcher only communicates with the following official servers:
Microsoft Authentication Service (login.microsoftonline.com)
Xbox Live / XSTS Services (user.auth.xboxlive.com, xsts.auth.xboxlive.com)
Mojang Official API (api.minecraftservices.com)
Minecraft Official Resource Servers (piston-meta.mojang.com, resources.download.minecraft.net)
What We Do NOT Do
We do not collect, store, or share your Microsoft account password.
We do not use your personal data for advertising, analytics, or commercial purposes.
We do not sell data to third parties.
Third-Party Services
This Launcher utilizes official APIs from Microsoft and Mojang for authentication and game asset downloads. Their privacy practices are governed by their respective policies:
Microsoft Privacy Statement
Mojang Privacy Policy
Disclaimer
This Launcher is an unofficial open-source project and is not affiliated with, endorsed by, or connected to Mojang Studios, Microsoft, or their affiliates. The Launcher assumes no liability for any account issues, data loss, or system damage arising from the use of this software. Use at your own risk.
Policy Updates
We may update this policy at any time. Continued use of the Launcher constitutes acceptance of the updated policy.
```


## 6.2 MainSrc\Download —— 下载引擎

### `MainSrc\Download\DownloadEngine.cs`

```csharp
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
```

### `MainSrc\Download\GameInstaller.cs`

```csharp
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
```

### `MainSrc\Download\MinecraftManifest.cs`

```csharp
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Avalonia.Media.Imaging;

namespace Project.Launch.Download
{
    public class VersionManifest
    {
        [JsonPropertyName("latest")]
        public LatestVersions Latest { get; set; } = new();

        [JsonPropertyName("versions")]
        public List<VersionEntry> Versions { get; set; } = new();
    }

    public class LatestVersions
    {
        [JsonPropertyName("release")]
        public string Release { get; set; } = "";

        [JsonPropertyName("snapshot")]
        public string Snapshot { get; set; } = "";
    }

    public class VersionEntry
    {
        // ★ 改名避开基础类型的隐藏冲突
        [JsonPropertyName("id")]
        public string VersionId { get; set; } = "";

        [JsonPropertyName("type")]
        public string Type { get; set; } = "";

        [JsonPropertyName("url")]
        public string Url { get; set; } = "";

        [JsonPropertyName("releaseTime")]
        public DateTime ReleaseTime { get; set; }

        // 供 UI 显示的格式化时间
        public string ReleaseTimeStr => ReleaseTime.ToString("yyyy-MM-dd");

        // ★ UI 显示的图标路径
        public string IconPath { get; set; } = "";

        // ★ UI 直接绑定的图标对象
        public Bitmap? IconBitmap { get; set; }
    }
}
```

### `MainSrc\Download\ProxyEnvironment.cs`

```csharp
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
```

### `MainSrc\Download\SourcePolicy.cs`

```csharp
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
```

### `MainSrc\Download\VersionService.cs`

```csharp
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
```


## 6.3 MainSrc\Auth —— 微软正版登录

### `MainSrc\Auth\AccountData.cs`

```csharp
using System;
using System.Collections.Generic;

namespace Project.Launch.Auth
{
    public class AccountInfo
    {
        public string Type { get; set; } = "offline";
        public string Username { get; set; } = string.Empty;
        public string Uuid { get; set; } = string.Empty;
        public DateTime LastUsed { get; set; } = DateTime.UtcNow;

        // ============================================================
        // 正版账户专属字段（离线账户留空）
        // ============================================================

        /// <summary>微软 refresh_token（长期，用于静默刷新）</summary>
        public string MicrosoftRefreshToken { get; set; } = string.Empty;

        /// <summary>Minecraft access_token（短期，24 小时）</summary>
        public string MinecraftAccessToken { get; set; } = string.Empty;

        /// <summary>token 过期时间（UTC）</summary>
        public DateTime TokenExpireAt { get; set; } = DateTime.MinValue;

        /// <summary>皮肤 URL（正版专属，留空则用 UUID 推算 Steve/Alex）</summary>
        public string SkinUrl { get; set; } = string.Empty;
    }

    public class AccountsRoot
    {
        public Dictionary<string, AccountInfo> Accounts { get; set; } = new();
        public string SelectedAccount { get; set; } = string.Empty;
        public bool LoginCheck { get; set; } = false;
    }
}
```

### `MainSrc\Auth\AccountService.cs`

```csharp
using System;
using System.IO;
using System.Text.Json;

using Project.Launch.Tools.JavaConfig;
using Project.Launch.Tools.LogsConfig;
namespace Project.Launch.Auth
{
    public static class AccountService
    {
        // ★ P1-5 修复：路径统一走 LauncherPaths，不再自己拼一份
        private static readonly object _lock = new object();

        public static AccountsRoot LoadAccounts()
        {
            lock (_lock)
            {
                if (!File.Exists(LauncherPaths.AccountsFile))
                    return new AccountsRoot();

                try
                {
                    string json = File.ReadAllText(LauncherPaths.AccountsFile);
                    return JsonSerializer.Deserialize<AccountsRoot>(json) ?? new AccountsRoot();
                }
                catch (Exception ex)
                {
                    LogHelper.Write(LauncherPaths.AppLog,
                        $"读取账户失败: {ex.Message}",
                        $"Failed to load accounts: {ex.Message}");
                    return new AccountsRoot();
                }
            }
        }

        // ★ P0-5 修复：写 tmp + Move 覆盖 + try-catch
        public static void SaveAccounts(AccountsRoot root)
        {
            lock (_lock)
            {
                SaveAccountsInternal(root);
            }
        }

        // ★ P1-1 修复：read-modify-write 收口在单个 lock 内
        private static void ModifyAccounts(Action<AccountsRoot> mutate)
        {
            lock (_lock)
            {
                AccountsRoot root;
                if (!File.Exists(LauncherPaths.AccountsFile))
                {
                    root = new AccountsRoot();
                }
                else
                {
                    try
                    {
                        string json = File.ReadAllText(LauncherPaths.AccountsFile);
                        root = JsonSerializer.Deserialize<AccountsRoot>(json) ?? new AccountsRoot();
                    }
                    catch (Exception ex)
                    {
                        LogHelper.Write(LauncherPaths.AppLog,
                            $"读取账户失败（将重置）: {ex.Message}",
                            $"Failed to load accounts (will reset): {ex.Message}");
                        root = new AccountsRoot();
                    }
                }

                mutate(root);
                SaveAccountsInternal(root);
            }
        }

        private static void SaveAccountsInternal(AccountsRoot root)
        {
            try
            {
                string json = JsonSerializer.Serialize(root,
                    new JsonSerializerOptions { WriteIndented = true });

                // 原子写：先写临时文件，再 Move 覆盖
                string tmpPath = LauncherPaths.AccountsFile + ".tmp";
                File.WriteAllText(tmpPath, json);
                File.Move(tmpPath, LauncherPaths.AccountsFile, overwrite: true);
            }
            catch (Exception ex)
            {
                LogHelper.Write(LauncherPaths.AppLog,
                    $"保存账户失败: {ex.Message}",
                    $"Failed to save accounts: {ex.Message}");
            }
        }

        public static AccountInfo? GetSelectedAccount()
        {
            var root = LoadAccounts();
            if (string.IsNullOrEmpty(root.SelectedAccount) || !root.Accounts.ContainsKey(root.SelectedAccount))
                return null;
            return root.Accounts[root.SelectedAccount];
        }

        public static void SetSelectedAccount(string accountId, AccountInfo account)
        {
            ModifyAccounts(root =>
            {
                root.Accounts[accountId] = account;
                root.SelectedAccount = accountId;
            });
        }

        public static void RemoveAccount(string accountId)
        {
            ModifyAccounts(root =>
            {
                if (root.Accounts.Remove(accountId) && root.SelectedAccount == accountId)
                    root.SelectedAccount = string.Empty;
            });
        }

        public static bool GetLoginCheck()
        {
            var root = LoadAccounts();
            return root.LoginCheck;
        }

        public static void SetLoginCheck(bool value)
        {
            ModifyAccounts(root => root.LoginCheck = value);
        }
    }
}
```

### `MainSrc\Auth\AuthModels.cs`

```csharp
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Project.Launch.Auth
{
    // ============================================================
    // 微软 OAuth 设备代码流
    // ============================================================

    public class DeviceCodeResponse
    {
        [JsonPropertyName("device_code")]
        public string DeviceCode { get; set; } = "";

        [JsonPropertyName("user_code")]
        public string UserCode { get; set; } = "";

        [JsonPropertyName("verification_uri")]
        public string VerificationUri { get; set; } = "";

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonPropertyName("interval")]
        public int Interval { get; set; } = 5;

        [JsonPropertyName("message")]
        public string Message { get; set; } = "";
    }

    public class MicrosoftTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = "";

        [JsonPropertyName("refresh_token")]
        public string RefreshToken { get; set; } = "";

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonPropertyName("token_type")]
        public string TokenType { get; set; } = "Bearer";
    }

    public class MicrosoftErrorResponse
    {
        [JsonPropertyName("error")]
        public string Error { get; set; } = "";

        [JsonPropertyName("error_description")]
        public string ErrorDescription { get; set; } = "";
    }

    // ============================================================
    // Xbox Live 认证
    // ============================================================

    public class XboxAuthRequest
    {
        [JsonPropertyName("Properties")]
        public XboxAuthProperties Properties { get; set; } = new();

        [JsonPropertyName("RelyingParty")]
        public string RelyingParty { get; set; } = "http://auth.xboxlive.com";

        [JsonPropertyName("TokenType")]
        public string TokenType { get; set; } = "JWT";
    }

    public class XboxAuthProperties
    {
        [JsonPropertyName("AuthMethod")]
        public string AuthMethod { get; set; } = "RPS";

        [JsonPropertyName("SiteName")]
        public string SiteName { get; set; } = "user.auth.xboxlive.com";

        [JsonPropertyName("RpsTicket")]
        public string RpsTicket { get; set; } = "";
    }

    public class XboxAuthResponse
    {
        [JsonPropertyName("Token")]
        public string Token { get; set; } = "";

        [JsonPropertyName("DisplayClaims")]
        public XboxDisplayClaims? DisplayClaims { get; set; }

        [JsonPropertyName("XErr")]
        public long? XErr { get; set; }

        [JsonPropertyName("Message")]
        public string? Message { get; set; }
    }

    public class XboxDisplayClaims
    {
        [JsonPropertyName("xui")]
        public List<XboxXui> Xui { get; set; } = new();
    }

    public class XboxXui
    {
        [JsonPropertyName("uhs")]
        public string Uhs { get; set; } = "";
    }

    // ============================================================
    // XSTS 认证
    // ============================================================

    public class XstsAuthRequest
    {
        [JsonPropertyName("Properties")]
        public XstsAuthProperties Properties { get; set; } = new();

        [JsonPropertyName("RelyingParty")]
        public string RelyingParty { get; set; } = "rp://api.minecraftservices.com/";

        [JsonPropertyName("TokenType")]
        public string TokenType { get; set; } = "JWT";
    }

    public class XstsAuthProperties
    {
        [JsonPropertyName("SandboxId")]
        public string SandboxId { get; set; } = "RETAIL";

        [JsonPropertyName("UserTokens")]
        public List<string> UserTokens { get; set; } = new();
    }

    // ============================================================
    // Minecraft 认证
    // ============================================================

    public class MinecraftAuthRequest
    {
        [JsonPropertyName("identityToken")]
        public string IdentityToken { get; set; } = "";
    }

    public class MinecraftAuthResponse
    {
        [JsonPropertyName("username")]
        public string Username { get; set; } = "";

        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = "";

        [JsonPropertyName("token_type")]
        public string TokenType { get; set; } = "Bearer";

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }

    public class MinecraftProfile
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("skins")]
        public List<MinecraftSkin> Skins { get; set; } = new();
    }

    public class MinecraftSkin
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("state")]
        public string State { get; set; } = "";

        [JsonPropertyName("url")]
        public string Url { get; set; } = "";

        [JsonPropertyName("variant")]
        public string Variant { get; set; } = "";
    }
}
```

### `MainSrc\Auth\DefaultSkinSelector.cs`

```csharp
using System;

using Project.Launch.Tools.JavaConfig;
using Project.Launch.Tools.LogsConfig;
namespace Project.Launch.Auth
{
    /// <summary>
    /// 根据 UUID 决定默认皮肤（Steve / Alex）
    ///
    /// 算法来源：Minecraft Java 版官方逻辑
    ///   取 UUID 第 8、16、24、32 位（0-based 索引 7、15、23、31）的 hex 值
    ///   异或后取末位：奇数 → Alex，偶数 → Steve
    /// </summary>
    public static class DefaultSkinSelector
    {
        public const string SteveAssetPath =
            "avares://Project.Launch/MainSrc/Views/imgs/skin/Steve.png";

        public const string AlexAssetPath =
            "avares://Project.Launch/MainSrc/Views/imgs/skin/Alex.png";

        /// <summary>
        /// 根据 UUID 判断是否应使用 Alex 模型
        /// </summary>
        public static bool ShouldUseAlex(string? uuid)
        {
            // ★ UUID 缺失是错误状态，不静默兜底，写日志暴露
            if (string.IsNullOrEmpty(uuid))
            {
                LogHelper.Write(LauncherPaths.AppLog,
                    "[错误] UUID 为空，无法判定 Steve/Alex（异常状态）",
                    "[ERROR] UUID is null/empty, cannot determine Steve/Alex");
                return false;
            }

            string hex = uuid.Replace("-", "");

            if (hex.Length < 32)
            {
                LogHelper.Write(LauncherPaths.AppLog,
                    $"[错误] UUID 长度不足（{uuid}），无法判定 Steve/Alex（异常状态）",
                    $"[ERROR] UUID too short ({uuid}), cannot determine Steve/Alex");
                return false;
            }

            try
            {
                int a = Convert.ToInt32(hex[7].ToString(), 16);
                int b = Convert.ToInt32(hex[15].ToString(), 16);
                int c = Convert.ToInt32(hex[23].ToString(), 16);
                int d = Convert.ToInt32(hex[31].ToString(), 16);

                int xor = a ^ b ^ c ^ d;
                return (xor & 1) == 1;
            }
            catch (Exception ex)
            {
                LogHelper.Write(LauncherPaths.AppLog,
                    $"[错误] UUID 解析失败（{uuid}）: {ex.Message}",
                    $"[ERROR] UUID parse failed ({uuid}): {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 根据 UUID 返回对应的皮肤资源路径
        /// </summary>
        public static string GetSkinAssetPath(string? uuid)
        {
            return ShouldUseAlex(uuid) ? AlexAssetPath : SteveAssetPath;
        }
    }
}
```

### `MainSrc\Auth\DeviceCodeAuthService.cs`

```csharp
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Project.Launch.Tools.JavaConfig;
using Project.Launch.Tools.LogsConfig;
namespace Project.Launch.Auth
{
    /// <summary>
    /// 微软 OAuth 2.0 设备代码流
    /// </summary>
    public class DeviceCodeAuthService
    {
        public const string ClientId = "7dddbcf7-5d7d-46cf-96fc-292d6f392cd0";

        // ★ 修复：个人账户必须用 consumers 端点
        public const string Tenant = "consumers";

        // ★ 修复：XboxLive.offline_access 而非 offline_access
        public const string Scope = "XboxLive.signin XboxLive.offline_access";

        private static readonly HttpClient _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        private const string DeviceCodeUrl =
            "https://login.microsoftonline.com/" + Tenant + "/oauth2/v2.0/devicecode";
        private const string TokenUrl =
            "https://login.microsoftonline.com/" + Tenant + "/oauth2/v2.0/token";

        // ============================================================
        // 阶段 1：申请设备代码
        // ============================================================
        public async Task<DeviceCodeResponse?> RequestDeviceCodeAsync()
        {
            try
            {
                var content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("client_id", ClientId),
                    new KeyValuePair<string, string>("scope", Scope)
                });

                var resp = await _http.PostAsync(DeviceCodeUrl, content);
                var json = await resp.Content.ReadAsStringAsync();

                if (!resp.IsSuccessStatusCode)
                {
                    LogHelper.Write(LauncherPaths.AppLog,
                        $"设备代码申请失败: {resp.StatusCode} {json}",
                        $"Device code request failed: {resp.StatusCode} {json}");
                    return null;
                }

                var result = JsonSerializer.Deserialize<DeviceCodeResponse>(json);
                LogHelper.Write(LauncherPaths.AppLog,
                    $"设备代码已申请: {result?.UserCode}",
                    $"Device code obtained: {result?.UserCode}");
                return result;
            }
            catch (Exception ex)
            {
                LogHelper.Write(LauncherPaths.AppLog,
                    $"设备代码申请异常: {ex.Message}",
                    $"Device code request exception: {ex.Message}");
                return null;
            }
        }

        // ============================================================
        // 阶段 3：轮询等待用户授权
        // ============================================================
        public async Task<MicrosoftTokenResponse?> PollForTokenAsync(
            string deviceCode,
            int intervalSeconds,
            int expiresInSeconds,
            CancellationToken ct = default)
        {
            var startTime = DateTime.UtcNow;
            var timeout = TimeSpan.FromSeconds(expiresInSeconds);
            int interval = Math.Max(3, intervalSeconds);

            while (true)
            {
                if (ct.IsCancellationRequested)
                {
                    LogHelper.Write(LauncherPaths.AppLog,
                        "用户取消轮询",
                        "User cancelled polling");
                    return null;
                }

                if (DateTime.UtcNow - startTime > timeout)
                {
                    LogHelper.Write(LauncherPaths.AppLog,
                        "设备代码已超时",
                        "Device code expired");
                    return null;
                }

                // 先等，再轮询
                try
                {
                    await Task.Delay(interval * 1000, ct);
                }
                catch (OperationCanceledException)
                {
                    return null;
                }

                try
                {
                    var content = new FormUrlEncodedContent(new[]
                    {
                        new KeyValuePair<string, string>(
                            "grant_type",
                            "urn:ietf:params:oauth:grant-type:device_code"),
                        new KeyValuePair<string, string>("client_id", ClientId),
                        new KeyValuePair<string, string>("device_code", deviceCode)
                    });

                    var resp = await _http.PostAsync(TokenUrl, content, ct);
                    var json = await resp.Content.ReadAsStringAsync(ct);

                    if (resp.IsSuccessStatusCode)
                    {
                        var token = JsonSerializer.Deserialize<MicrosoftTokenResponse>(json);
                        LogHelper.Write(LauncherPaths.AppLog,
                            "微软账号授权成功，已获取 access_token",
                            "Microsoft auth success, access_token obtained");
                        return token;
                    }

                    // 错误处理
                    MicrosoftErrorResponse? err = null;
                    try
                    {
                        err = JsonSerializer.Deserialize<MicrosoftErrorResponse>(json);
                    }
                    catch { }

                    switch (err?.Error)
                    {
                        case "authorization_pending":
                            continue;

                        case "slow_down":
                            interval += 5;
                            continue;

                        case "access_denied":
                            LogHelper.Write(LauncherPaths.AppLog,
                                "用户拒绝了授权",
                                "User denied authorization");
                            return null;

                        case "expired_token":
                            LogHelper.Write(LauncherPaths.AppLog,
                                "设备代码已过期",
                                "Device code expired");
                            return null;

                        default:
                            LogHelper.Write(LauncherPaths.AppLog,
                                $"OAuth 错误: {err?.Error} - {err?.ErrorDescription}",
                                $"OAuth error: {err?.Error} - {err?.ErrorDescription}");
                            return null;
                    }
                }
                catch (OperationCanceledException)
                {
                    return null;
                }
                catch (Exception ex)
                {
                    LogHelper.Write(LauncherPaths.AppLog,
                        $"轮询异常: {ex.Message}",
                        $"Poll exception: {ex.Message}");
                    continue;
                }
            }
        }
    }
}
```

### `MainSrc\Auth\MicrosoftAuthFlow.cs`

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;

using Project.Launch.Tools.JavaConfig;
using Project.Launch.Tools.LogsConfig;
namespace Project.Launch.Auth
{
    /// <summary>
    /// 微软正版登录全流程协调器
    /// 阶段 1-3：微软 OAuth 设备代码流
    /// 阶段 4-5：Xbox Live + XSTS
    /// 阶段 6：Minecraft 认证 + profile
    /// </summary>
    public class MicrosoftAuthFlow
    {
        /// <summary>登录过程中触发，参数是给用户看的提示</summary>
        public event Action<string>? Progress;

        /// <summary>拿到设备代码后触发（user_code + verification_uri）</summary>
        public event Action<string, string>? DeviceCodeReady;

        /// <summary>
        /// 完整登录流程
        /// 成功返回 AccountInfo，失败返回 null 并把错误抛给 Progress
        /// </summary>
        public async Task<AccountInfo?> LoginAsync(CancellationToken ct = default)
        {
            try
            {
                // ============================================================
                // 阶段 1：申请设备代码
                // ============================================================
                Progress?.Invoke("正在申请设备代码...");

                var devSvc = new DeviceCodeAuthService();
                var deviceCode = await devSvc.RequestDeviceCodeAsync();
                if (deviceCode == null)
                {
                    Progress?.Invoke("❌ 无法连接微软服务器");
                    return null;
                }

                // 通知 UI 显示代码
                DeviceCodeReady?.Invoke(deviceCode.UserCode, deviceCode.VerificationUri);

                // ============================================================
                // 阶段 3：轮询等待授权
                // ============================================================
                Progress?.Invoke($"等待授权中（{deviceCode.ExpiresIn / 60} 分钟）...");

                var msToken = await devSvc.PollForTokenAsync(
                    deviceCode.DeviceCode,
                    deviceCode.Interval,
                    deviceCode.ExpiresIn,
                    ct);

                if (msToken == null)
                {
                    if (ct.IsCancellationRequested)
                        Progress?.Invoke("❌ 已取消");
                    else
                        Progress?.Invoke("❌ 授权超时或已拒绝");
                    return null;
                }

                // ============================================================
                // 阶段 4：XBL 认证
                // ============================================================
                Progress?.Invoke("正在验证 Xbox Live...");

                var xboxSvc = new XboxAuthService();
                var xblResp = await xboxSvc.AuthenticateWithXboxLiveAsync(msToken.AccessToken);
                if (xblResp == null)
                {
                    Progress?.Invoke("❌ Xbox Live 认证失败");
                    return null;
                }

                // ============================================================
                // 阶段 5：XSTS 认证
                // ============================================================
                Progress?.Invoke("正在验证 XSTS...");

                XboxAuthResponse? xstsResp;
                try
                {
                    xstsResp = await xboxSvc.AuthenticateWithXstsAsync(xblResp.Token);
                }
                catch (Exception ex)
                {
                    // XboxAuthService 会把用户友好错误通过 Exception 抛出
                    Progress?.Invoke($"❌ {ex.Message}");
                    return null;
                }

                if (xstsResp == null || xstsResp.DisplayClaims == null
                    || xstsResp.DisplayClaims.Xui.Count == 0)
                {
                    Progress?.Invoke("❌ XSTS 认证失败");
                    return null;
                }

                string uhs = xstsResp.DisplayClaims.Xui[0].Uhs;

                // ============================================================
                // 阶段 6：Minecraft 认证 + profile
                // ============================================================
                Progress?.Invoke("正在获取 Minecraft 档案...");

                var mcSvc = new MinecraftAuthService();
                var mcToken = await mcSvc.AuthenticateWithMinecraftAsync(xstsResp.Token, uhs);
                if (mcToken == null)
                {
                    Progress?.Invoke("❌ Minecraft 认证失败");
                    return null;
                }

                MinecraftProfile? profile;
                try
                {
                    profile = await mcSvc.GetProfileAsync(mcToken.AccessToken);
                }
                catch (Exception ex)
                {
                    Progress?.Invoke($"❌ {ex.Message}");
                    return null;
                }

                if (profile == null)
                {
                    Progress?.Invoke("❌ 无法获取 Minecraft 档案");
                    return null;
                }

                // ============================================================
                // 阶段 7：构造 AccountInfo
                // ============================================================
                var account = new AccountInfo
                {
                    Type = "microsoft",
                    Username = profile.Name,
                    Uuid = profile.Id,   // 无连字符的 32 位 hex
                    LastUsed = DateTime.UtcNow,

                    MicrosoftRefreshToken = msToken.RefreshToken,
                    MinecraftAccessToken = mcToken.AccessToken,
                    TokenExpireAt = DateTime.UtcNow.AddSeconds(mcToken.ExpiresIn),
                    SkinUrl = profile.Skins.Count > 0 ? profile.Skins[0].Url : ""
                };

                Progress?.Invoke($"✅ 登录成功：{profile.Name}");
                LogHelper.Write(LauncherPaths.AppLog,
                    $"正版登录成功: {profile.Name} ({profile.Id})",
                    $"Microsoft login success: {profile.Name} ({profile.Id})");

                return account;
            }
            catch (Exception ex)
            {
                LogHelper.Write(LauncherPaths.AppLog,
                    $"正版登录流程异常: {ex.Message}",
                    $"Microsoft auth flow exception: {ex.Message}");
                Progress?.Invoke($"❌ 登录异常: {ex.Message}");
                return null;
            }
        }
    }
}
```

### `MainSrc\Auth\MinecraftAuthService.cs`

```csharp
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using Project.Launch.Tools.JavaConfig;
using Project.Launch.Tools.LogsConfig;
namespace Project.Launch.Auth
{
    /// <summary>
    /// Minecraft 认证 + 拉取 profile
    /// </summary>
    public class MinecraftAuthService
    {
        private static readonly HttpClient _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        private const string McAuthUrl =
            "https://api.minecraftservices.com/authentication/login_with_xbox";
        private const string McProfileUrl =
            "https://api.minecraftservices.com/minecraft/profile";

        // ============================================================
        // 阶段 6a：XSTS token → Minecraft access_token
        // ============================================================
        public async Task<MinecraftAuthResponse?> AuthenticateWithMinecraftAsync(
            string xstsToken, string uhs)
        {
            try
            {
                // ★ 官方格式：XBL3.0 x=<uhs>;<xsts_token>
                string identityToken = $"XBL3.0 x={uhs};{xstsToken}";

                var req = new MinecraftAuthRequest
                {
                    IdentityToken = identityToken
                };

                var json = JsonSerializer.Serialize(req);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var resp = await _http.PostAsync(McAuthUrl, content);
                var respJson = await resp.Content.ReadAsStringAsync();

                if (!resp.IsSuccessStatusCode)
                {
                    LogHelper.Write(LauncherPaths.AppLog,
                        $"Minecraft 认证失败: {resp.StatusCode} {respJson}",
                        $"Minecraft auth failed: {resp.StatusCode} {respJson}");
                    return null;
                }

                var result = JsonSerializer.Deserialize<MinecraftAuthResponse>(respJson);
                LogHelper.Write(LauncherPaths.AppLog,
                    "Minecraft 认证成功",
                    "Minecraft auth success");
                return result;
            }
            catch (Exception ex)
            {
                LogHelper.Write(LauncherPaths.AppLog,
                    $"Minecraft 认证异常: {ex.Message}",
                    $"Minecraft auth exception: {ex.Message}");
                return null;
            }
        }

        // ============================================================
        // 阶段 6b：拉取 profile（UUID + 用户名 + 皮肤 URL）
        // ============================================================
        public async Task<MinecraftProfile?> GetProfileAsync(string mcAccessToken)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, McProfileUrl);
                request.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", mcAccessToken);

                var resp = await _http.SendAsync(request);
                var respJson = await resp.Content.ReadAsStringAsync();

                if (!resp.IsSuccessStatusCode)
                {
                    if ((int)resp.StatusCode == 404)
                    {
                        // 没买 Minecraft
                        LogHelper.Write(LauncherPaths.AppLog,
                            "此账号未拥有 Minecraft",
                            "This account does not own Minecraft");
                        throw new Exception("此微软账号没有购买 Minecraft");
                    }

                    LogHelper.Write(LauncherPaths.AppLog,
                        $"获取 profile 失败: {resp.StatusCode} {respJson}",
                        $"Get profile failed: {resp.StatusCode} {respJson}");
                    return null;
                }

                var profile = JsonSerializer.Deserialize<MinecraftProfile>(respJson);
                LogHelper.Write(LauncherPaths.AppLog,
                    $"Profile 获取成功: {profile?.Name} ({profile?.Id})",
                    $"Profile obtained: {profile?.Name} ({profile?.Id})");
                return profile;
            }
            catch (Exception ex)
            {
                LogHelper.Write(LauncherPaths.AppLog,
                    $"获取 profile 异常: {ex.Message}",
                    $"Get profile exception: {ex.Message}");
                throw;
            }
        }
    }
}
```

### `MainSrc\Auth\OfflineUuidGenerator.cs`

```csharp
using System;
using System.Security.Cryptography;
using System.Text;

namespace Project.Launch.Auth
{
    public static class OfflineUuidGenerator
    {
        /// <summary>
        /// 生成标准离线 UUID（兼容 HMCL / Java 实现）
        /// 算法：MD5("OfflinePlayer:" + username) → 设置 version/variant → 按大端序输出 hex
        /// </summary>
        public static string GenerateUuidString(string username)
        {
            // 1. 构造输入字符串
            string input = $"OfflinePlayer:{username}";

            // 2. 计算 MD5 哈希
            byte[] hash;
            using (MD5 md5 = MD5.Create())
            {
                hash = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
            }

            // 3. 设置 UUID 版本（version 3）
            hash[6] = (byte)((hash[6] & 0x0F) | 0x30);
            // 4. 设置变体（IETF variant）
            hash[8] = (byte)((hash[8] & 0x3F) | 0x80);

            // 5. ★ 手动拼接 hex 字符串（按大端序，不用 Guid 类）
            // 格式：xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
            return string.Format(
                "{0:x2}{1:x2}{2:x2}{3:x2}-{4:x2}{5:x2}-{6:x2}{7:x2}-{8:x2}{9:x2}-{10:x2}{11:x2}{12:x2}{13:x2}{14:x2}{15:x2}",
                hash[0], hash[1], hash[2], hash[3],
                hash[4], hash[5],
                hash[6], hash[7],
                hash[8], hash[9],
                hash[10], hash[11], hash[12], hash[13], hash[14], hash[15]
            );
        }

        // 如果你还需要返回 Guid 类型（用于现有代码），保留此方法并标记为 Obsolete
        [Obsolete("此方法返回的 Guid 可能与 Java 服务器不兼容，请使用 GenerateUuidString 替代")]
        public static Guid Generate(string username)
        {
            string uuidString = GenerateUuidString(username);
            return Guid.ParseExact(uuidString, "D");
        }
    }
}
```

### `MainSrc\Auth\XboxAuthService.cs`

```csharp
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using Project.Launch.Tools.JavaConfig;
using Project.Launch.Tools.LogsConfig;
namespace Project.Launch.Auth
{
    /// <summary>
    /// Xbox Live (XBL) + XSTS 认证
    /// </summary>
    public class XboxAuthService
    {
        private static readonly HttpClient _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        private const string XblUrl = "https://user.auth.xboxlive.com/user/authenticate";
        private const string XstsUrl = "https://xsts.auth.xboxlive.com/xsts/authorize";

        // ============================================================
        // 阶段 4：MS access_token → XBL token
        // ============================================================
        public async Task<XboxAuthResponse?> AuthenticateWithXboxLiveAsync(string msAccessToken)
        {
            try
            {
                var req = new XboxAuthRequest
                {
                    Properties = new XboxAuthProperties
                    {
                        AuthMethod = "RPS",
                        SiteName = "user.auth.xboxlive.com",
                        // ★ 官方要求加前缀 "d="
                        RpsTicket = "d=" + msAccessToken
                    },
                    RelyingParty = "http://auth.xboxlive.com",
                    TokenType = "JWT"
                };

                var json = JsonSerializer.Serialize(req);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var resp = await _http.PostAsync(XblUrl, content);
                var respJson = await resp.Content.ReadAsStringAsync();

                if (!resp.IsSuccessStatusCode)
                {
                    LogHelper.Write(LauncherPaths.AppLog,
                        $"XBL 认证失败: {resp.StatusCode} {respJson}",
                        $"XBL auth failed: {resp.StatusCode} {respJson}");
                    return null;
                }

                var result = JsonSerializer.Deserialize<XboxAuthResponse>(respJson);
                LogHelper.Write(LauncherPaths.AppLog,
                    "XBL 认证成功",
                    "XBL auth success");
                return result;
            }
            catch (Exception ex)
            {
                LogHelper.Write(LauncherPaths.AppLog,
                    $"XBL 认证异常: {ex.Message}",
                    $"XBL auth exception: {ex.Message}");
                return null;
            }
        }

        // ============================================================
        // 阶段 5：XBL token → XSTS token
        // ============================================================
        public async Task<XboxAuthResponse?> AuthenticateWithXstsAsync(string xblToken)
        {
            try
            {
                var req = new XstsAuthRequest
                {
                    Properties = new XstsAuthProperties
                    {
                        SandboxId = "RETAIL",
                        UserTokens = new System.Collections.Generic.List<string> { xblToken }
                    },
                    RelyingParty = "rp://api.minecraftservices.com/",
                    TokenType = "JWT"
                };

                var json = JsonSerializer.Serialize(req);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var resp = await _http.PostAsync(XstsUrl, content);
                var respJson = await resp.Content.ReadAsStringAsync();

                if (!resp.IsSuccessStatusCode)
                {
                    // XSTS 有专属错误码
                    XboxAuthResponse? errResp = null;
                    try
                    {
                        errResp = JsonSerializer.Deserialize<XboxAuthResponse>(respJson);
                    }
                    catch { }

                    string errMsg = errResp?.XErr switch
                    {
                        2148916233 => "此微软账号没有 Xbox 账号，请先去 xbox.com 创建",
                        2148916235 => "此账号所在地区不支持 Xbox Live",
                        2148916236 => "此账号需要成人验证",
                        2148916237 => "此账号需要成人验证",
                        2148916238 => "此账号未成年，需要加入家庭组",
                        _ => $"XSTS 认证失败: {resp.StatusCode}"
                    };

                    LogHelper.Write(LauncherPaths.AppLog,
                        $"XSTS 认证失败 [{errResp?.XErr}]: {errMsg}",
                        $"XSTS auth failed [{errResp?.XErr}]: {errMsg}");

                    // 把用户友好的错误存起来，供上层显示
                    throw new Exception(errMsg);
                }

                var result = JsonSerializer.Deserialize<XboxAuthResponse>(respJson);
                LogHelper.Write(LauncherPaths.AppLog,
                    "XSTS 认证成功",
                    "XSTS auth success");
                return result;
            }
            catch (Exception ex)
            {
                LogHelper.Write(LauncherPaths.AppLog,
                    $"XSTS 认证异常: {ex.Message}",
                    $"XSTS auth exception: {ex.Message}");
                throw;
            }
        }
    }
}
```


## 6.4 MainSrc\Views —— 界面层

### `MainSrc\Views\AccountManagerWindow.axaml`

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:avalonia="using:Avalonia.Controls"
        x:Class="Project.Launch.Views.AccountManagerWindow"
        Title="账户管理"
        Width="800" Height="440"
        WindowDecorations="None"
        ExtendClientAreaToDecorationsHint="True"
        ExtendClientAreaTitleBarHeightHint="-1"
        CanResize="False"
        Background="Transparent"
        TransparencyLevelHint="AcrylicBlur"
        avalonia:Win32Properties.WindowCornerPreference="Round">

    <Window.Styles>
        <!-- 添加账户按钮（主题色） -->
        <Style Selector="Button.AddAccount">
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate>
                        <Border Background="{TemplateBinding Background}"
                                CornerRadius="10"
                                Padding="{TemplateBinding Padding}">
                            <ContentPresenter Content="{TemplateBinding Content}"
                                              HorizontalAlignment="Center"
                                              VerticalAlignment="Center" />
                        </Border>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
            <Setter Property="Background" Value="#DD8A68CE" />
            <Setter Property="Foreground" Value="White" />
            <Setter Property="FontWeight" Value="SemiBold" />
            <Setter Property="FontSize" Value="16" />
            <Setter Property="Padding" Value="28,14" />
            <Setter Property="Cursor" Value="Hand" />
            <Setter Property="HorizontalAlignment" Value="Center" />
            <Setter Property="Transitions">
                <Transitions>
                    <BrushTransition Property="Background" Duration="0:0:0.15" />
                </Transitions>
            </Setter>
        </Style>
        <Style Selector="Button.AddAccount:pointerover">
            <Setter Property="Background" Value="#EE9A78DE" />
        </Style>

        <!-- 账户项按钮 -->
        <Style Selector="Button.AccountItem">
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate>
                        <Border Background="{TemplateBinding Background}"
                                CornerRadius="8"
                                Padding="{TemplateBinding Padding}">
                            <ContentPresenter Content="{TemplateBinding Content}"
                                              HorizontalAlignment="Stretch"
                                              VerticalAlignment="Center" />
                        </Border>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
            <Setter Property="Cursor" Value="Hand" />
            <Setter Property="HorizontalAlignment" Value="Stretch" />
            <Setter Property="HorizontalContentAlignment" Value="Stretch" />
            <Setter Property="Padding" Value="12,10" />
            <Setter Property="Transitions">
                <Transitions>
                    <BrushTransition Property="Background" Duration="0:0:0.15" />
                </Transitions>
            </Setter>
        </Style>
        <Style Selector="Button.AccountItem:pointerover">
            <Setter Property="Background" Value="#E8E0F0" />
        </Style>

        <!-- 删除按钮 -->
        <Style Selector="Button.DeleteAccount">
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate>
                        <Border Background="{TemplateBinding Background}"
                                CornerRadius="6"
                                Padding="{TemplateBinding Padding}">
                            <ContentPresenter Content="{TemplateBinding Content}"
                                              HorizontalAlignment="Center"
                                              VerticalAlignment="Center" />
                        </Border>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
            <Setter Property="Background" Value="Transparent" />
            <Setter Property="Foreground" Value="#CC5555" />
            <Setter Property="FontWeight" Value="Bold" />
            <Setter Property="FontSize" Value="16" />
            <Setter Property="Padding" Value="10,4" />
            <Setter Property="Cursor" Value="Hand" />
            <Setter Property="VerticalAlignment" Value="Center" />
        </Style>
        <Style Selector="Button.DeleteAccount:pointerover">
            <Setter Property="Background" Value="#22CC5555" />
        </Style>

        <!-- 关闭按钮 -->
        <Style Selector="Button.CloseBtn">
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate>
                        <Border Background="{TemplateBinding Background}"
                                CornerRadius="6"
                                Padding="{TemplateBinding Padding}">
                            <ContentPresenter Content="{TemplateBinding Content}"
                                              HorizontalAlignment="Center"
                                              VerticalAlignment="Center" />
                        </Border>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
            <Setter Property="Background" Value="Transparent" />
            <Setter Property="Foreground" Value="#666" />
            <Setter Property="FontSize" Value="12" />
            <Setter Property="Padding" Value="14,6" />
            <Setter Property="Cursor" Value="Hand" />
        </Style>
        <Style Selector="Button.CloseBtn:pointerover">
            <Setter Property="Background" Value="#E8E8E8" />
        </Style>
    </Window.Styles>

    <Border CornerRadius="12" Background="White">
        <Grid>
            <Grid.RowDefinitions>
                <RowDefinition Height="*" />
                <RowDefinition Height="Auto" />
            </Grid.RowDefinitions>

            <!-- 内容区 -->
            <Grid Grid.Row="0" Margin="30,30,30,15">

                <!-- 空状态：居中大按钮 -->
                <Button x:Name="EmptyAddButton"
                        Classes="AddAccount"
                        Content="＋ 添加账户"
                        HorizontalAlignment="Center"
                        VerticalAlignment="Center"
                        IsVisible="False"
                        Click="OnAddAccountClicked" />

                <!-- 非空：列表 + 底部添加按钮 -->
                <Grid x:Name="ListContainer" IsVisible="False">
                    <Grid.RowDefinitions>
                        <RowDefinition Height="*" />
                        <RowDefinition Height="Auto" />
                    </Grid.RowDefinitions>

                    <ScrollViewer Grid.Row="0"
                                  VerticalScrollBarVisibility="Auto"
                                  HorizontalScrollBarVisibility="Disabled">
                        <StackPanel x:Name="AccountListPanel" Spacing="8" />
                    </ScrollViewer>

                    <Button x:Name="BottomAddButton"
                            Grid.Row="1"
                            Classes="AddAccount"
                            Content="＋ 添加账户"
                            HorizontalAlignment="Stretch"
                            Margin="0,15,0,0"
                            Click="OnAddAccountClicked" />
                </Grid>
            </Grid>

            <!-- 底部栏 -->
            <Border Grid.Row="1"
                    Height="44"
                    Background="#F5F5F5"
                    CornerRadius="0,0,12,12">
                <DockPanel Margin="15,0,15,0">
                    <TextBlock DockPanel.Dock="Left"
                               Text="账户管理"
                               FontSize="11"
                               Foreground="#999"
                               VerticalAlignment="Center" />
                    <Button DockPanel.Dock="Right"
                            x:Name="CloseButton"
                            Classes="CloseBtn"
                            Content="关闭"
                            VerticalAlignment="Center"
                            Click="OnCloseClicked" />
                </DockPanel>
            </Border>
        </Grid>
    </Border>
</Window>
```

### `MainSrc\Views\AccountManagerWindow.axaml.cs`

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Project.Launch.Tools;

using Project.Launch;
using Project.Launch.Auth;
using Project.Launch.Tools.JavaConfig;
using Project.Launch.Tools.LogsConfig;
namespace Project.Launch.Views
{
    public partial class AccountManagerWindow : Window
    {
        /// <summary>用户是否点了"添加账户"（外部据此决定是否打开 LoginWindow）</summary>
        public bool ShouldOpenLogin { get; private set; } = false;

        public AccountManagerWindow()
        {
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            InitializeComponent();

            RebuildAccountList();
        }

        // ============================================================
        // 重建账户列表
        // ============================================================
        private void RebuildAccountList()
        {
            var listPanel = this.FindControl<StackPanel>("AccountListPanel");
            var emptyAddBtn = this.FindControl<Button>("EmptyAddButton");
            var listContainer = this.FindControl<Grid>("ListContainer");

            if (listPanel == null || emptyAddBtn == null || listContainer == null) return;

            listPanel.Children.Clear();

            var root = AccountService.LoadAccounts();
            var accounts = root.Accounts
                .OrderByDescending(a => a.Value.LastUsed)
                .ToList();

            if (accounts.Count == 0)
            {
                emptyAddBtn.IsVisible = true;
                listContainer.IsVisible = false;
                return;
            }

            emptyAddBtn.IsVisible = false;
            listContainer.IsVisible = true;

            foreach (var kv in accounts)
            {
                bool isSelected = kv.Key == root.SelectedAccount;
                var item = CreateAccountItem(kv.Key, kv.Value, isSelected);
                listPanel.Children.Add(item);
            }
        }

        // ============================================================
        // 创建单个账户项（方形头像，无边框）
        // ============================================================
        private Control CreateAccountItem(string id, AccountInfo info, bool isSelected)
        {
            var button = new Button
            {
                Background = isSelected
                    ? new SolidColorBrush(Color.Parse("#E0D8F0"))
                    : new SolidColorBrush(Color.Parse("#F0F0F0"))
            };
            button.Classes.Add("AccountItem");

            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto,Auto"),
                VerticalAlignment = VerticalAlignment.Center
            };

            // ★ 方形头像（40×40，无边框，用裁切好的 face）
            var avatar = new Image
            {
                Width = 40,
                Height = 40,
                Stretch = Stretch.Uniform,
                VerticalAlignment = VerticalAlignment.Center
            };

            // ★ 用附加属性设置 Nearest 采样
            RenderOptions.SetBitmapInterpolationMode(avatar, BitmapInterpolationMode.None);

            try
            {
                string skinAsset = DefaultSkinSelector.GetSkinAssetPath(info.Uuid);
                var face = SkinCropper.CropFaceFromAsset(skinAsset);
                if (face != null)
                    avatar.Source = face;
            }
            catch { }

            Grid.SetColumn(avatar, 0);
            grid.Children.Add(avatar);

            // 用户名
            var nameText = new TextBlock
            {
                Text = info.Username,
                FontSize = 15,
                FontWeight = FontWeight.SemiBold,
                Foreground = new SolidColorBrush(Color.Parse("#333")),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 0, 0)
            };
            Grid.SetColumn(nameText, 1);
            grid.Children.Add(nameText);

            // 类型标签
            var typeText = new TextBlock
            {
                Text = info.Type == "microsoft" ? "正版" : "离线",
                FontSize = 11,
                Foreground = new SolidColorBrush(
                    info.Type == "microsoft" ? Color.Parse("#4C8F4C") : Color.Parse("#888888")),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 10, 0)
            };
            Grid.SetColumn(typeText, 2);
            grid.Children.Add(typeText);

            // UUID 简写
            string uuidShort = string.IsNullOrEmpty(info.Uuid)
                ? "-------"
                : info.Uuid.Substring(0, Math.Min(8, info.Uuid.Length));
            var uuidText = new TextBlock
            {
                Text = uuidShort,
                FontSize = 11,
                FontFamily = new FontFamily("Consolas,monospace"),
                Foreground = new SolidColorBrush(Color.Parse("#AAA")),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0)
            };
            Grid.SetColumn(uuidText, 3);
            grid.Children.Add(uuidText);

            // 删除按钮
            var deleteBtn = new Button { Content = "✕" };
            deleteBtn.Classes.Add("DeleteAccount");
            deleteBtn.Click += (s, e) =>
            {
                e.Handled = true;
                _ = OnDeleteAccountAsync(id, info.Username);
            };
            Grid.SetColumn(deleteBtn, 4);
            grid.Children.Add(deleteBtn);

            button.Content = grid;
            button.Click += (s, e) => OnSelectAccount(id, info);

            return button;
        }

        // ============================================================
        // 点击账户 → 切换
        // ============================================================
        private void OnSelectAccount(string id, AccountInfo info)
        {
            try
            {
                App.PlayerName = info.Username;
                App.OfflineUuid = info.Uuid;
                App.IsLoggedIn = true;
                App.LoginCheck = true;

                var root = AccountService.LoadAccounts();
                root.SelectedAccount = id;
                AccountService.SaveAccounts(root);

                Close();
            }
            catch (Exception ex)
            {
                LogHelper.Write(LauncherPaths.AppLog,
                    $"切换账户失败: {ex.Message}",
                    $"Switch account failed: {ex.Message}");
            }
        }

        // ============================================================
        // 删除账户（含确认框）
        // ============================================================
        private async Task OnDeleteAccountAsync(string id, string username)
        {
            bool confirmed = await ShowConfirmDialogAsync(
                $"确认删除账户「{username}」？",
                "删除",
                "取消");
            if (!confirmed) return;

            try
            {
                AccountService.RemoveAccount(id);

                var root = AccountService.LoadAccounts();
                if (string.IsNullOrEmpty(root.SelectedAccount))
                {
                    App.IsLoggedIn = false;
                    App.PlayerName = null;
                    App.OfflineUuid = null;
                }

                RebuildAccountList();
            }
            catch (Exception ex)
            {
                LogHelper.Write(LauncherPaths.AppLog,
                    $"删除账户失败: {ex.Message}",
                    $"Delete account failed: {ex.Message}");
            }
        }

        // ============================================================
        // 确认对话框
        // ============================================================
        private async Task<bool> ShowConfirmDialogAsync(string message, string yesText, string noText)
        {
            var dialog = new Window
            {
                Title = "确认",
                Width = 360,
                Height = 180,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false,
                Background = new SolidColorBrush(Colors.White),
                WindowDecorations = WindowDecorations.None,
                TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent },
                ShowInTaskbar = false
            };

            var border = new Border
            {
                CornerRadius = new CornerRadius(12),
                Background = new SolidColorBrush(Colors.White),
                BorderBrush = new SolidColorBrush(Color.Parse("#20000000")),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(24)
            };

            var panel = new StackPanel { Spacing = 20 };

            panel.Children.Add(new TextBlock
            {
                Text = message,
                FontSize = 14,
                Foreground = new SolidColorBrush(Color.Parse("#333")),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center
            });

            var btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Spacing = 12
            };

            var yesBtn = new Button
            {
                Content = yesText,
                Padding = new Thickness(24, 8),
                Background = new SolidColorBrush(Color.Parse("#CC5555")),
                Foreground = new SolidColorBrush(Colors.White),
                FontWeight = FontWeight.SemiBold,
                CornerRadius = new CornerRadius(8),
                Cursor = new Cursor(StandardCursorType.Hand)
            };
            var noBtn = new Button
            {
                Content = noText,
                Padding = new Thickness(24, 8),
                Background = new SolidColorBrush(Color.Parse("#EEEEEE")),
                Foreground = new SolidColorBrush(Color.Parse("#333")),
                CornerRadius = new CornerRadius(8),
                Cursor = new Cursor(StandardCursorType.Hand)
            };

            yesBtn.Click += (s, e) => dialog.Close(true);
            noBtn.Click += (s, e) => dialog.Close(false);

            btnPanel.Children.Add(yesBtn);
            btnPanel.Children.Add(noBtn);
            panel.Children.Add(btnPanel);

            border.Child = panel;
            dialog.Content = border;

            return await dialog.ShowDialog<bool>(this);
        }

        // ============================================================
        // 添加账户
        // ============================================================
        private void OnAddAccountClicked(object? sender, RoutedEventArgs e)
        {
            ShouldOpenLogin = true;
            Close();
        }

        private void OnCloseClicked(object? sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
```

### `MainSrc\Views\DownloadWindow.axaml`

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:avalonia="using:Avalonia.Controls"
        x:Class="Project.Launch.Views.DownloadWindow"
        Title="下载中"
        Width="800" Height="440"
        WindowDecorations="None"
        ExtendClientAreaToDecorationsHint="True"
        ExtendClientAreaTitleBarHeightHint="-1"
        CanResize="False"
        Background="Transparent"
        TransparencyLevelHint="AcrylicBlur"
        avalonia:Win32Properties.WindowCornerPreference="Round">

    <Border CornerRadius="12" Background="White">
        <Grid>
            <Grid.RowDefinitions>
                <RowDefinition Height="*" />
                <RowDefinition Height="Auto" />
            </Grid.RowDefinitions>

            <Grid Grid.Row="0" Margin="30,30,30,15">
                
                <!-- ★ 配置面板（初始显示） -->
                <StackPanel x:Name="SetupPanel" HorizontalAlignment="Center" VerticalAlignment="Center" Spacing="20">
                    <TextBlock Text="下载选项配置" FontSize="20" FontWeight="Bold" Foreground="#333" HorizontalAlignment="Center" />
                    <TextBlock Text="请输入最大并发下载线程数 (推荐 20-50):" FontSize="14" Foreground="#666" HorizontalAlignment="Center" />
                    <NumericUpDown x:Name="ThreadCountBox" Value="20" Minimum="1" Maximum="128" Increment="5" 
                                   Width="200" HorizontalAlignment="Center" />
                    <Button x:Name="StartButton" Content="开始下载" Classes="Miret" Width="140" Height="40" 
                            HorizontalAlignment="Center" Margin="0,10,0,0" Click="OnStartClicked" />
                </StackPanel>

                <!-- ★ 进度面板（点击开始后显示） -->
                <StackPanel x:Name="ProgressPanel" IsVisible="False" HorizontalAlignment="Center" VerticalAlignment="Center" Spacing="20">
                    <TextBlock x:Name="TitleText" Text="正在下载 Minecraft" FontSize="20" FontWeight="Bold" Foreground="#333" HorizontalAlignment="Center" />
                    <TextBlock x:Name="StatusText" Text="正在获取版本信息..." FontSize="14" Foreground="#666" HorizontalAlignment="Center" />
                    <ProgressBar x:Name="MainProgressBar" Width="500" Height="12" Minimum="0" Maximum="100" Value="0" CornerRadius="6" Foreground="#D969CE" Background="#E0E0E0" />
                    <Grid Width="500" ColumnDefinitions="*,*">
                        <TextBlock x:Name="SpeedText" Text="速度: 0 KB/s" FontSize="13" Foreground="#888" HorizontalAlignment="Left" />
                        <TextBlock x:Name="FilesText" Grid.Column="1" Text="文件: 0 / 0" FontSize="13" Foreground="#888" HorizontalAlignment="Right" />
                    </Grid>
                </StackPanel>
            </Grid>

            <!-- 底部状态栏 -->
            <Border Grid.Row="1" Height="44" Background="#F5F5F5" CornerRadius="0,0,12,12">
                <DockPanel Margin="15,0,15,0">
                    <TextBlock DockPanel.Dock="Left" Text="Kilim 下载引擎" FontSize="11" Foreground="#999" VerticalAlignment="Center" />
                    <Button x:Name="CancelButton" DockPanel.Dock="Right" Content="取消下载" Background="#FF5555" Foreground="White" 
                            Padding="14,6" CornerRadius="6" Cursor="Hand" VerticalAlignment="Center" Click="OnCancelClicked" />
                </DockPanel>
            </Border>
        </Grid>
    </Border>
</Window>
```

### `MainSrc\Views\DownloadWindow.axaml.cs`

```csharp
using Avalonia.Controls;
using Avalonia.Interactivity;
using Project.Launch.Tools;
using Project.Launch.Download;
using System;
using System.Threading;
using System.Threading.Tasks;

using Project.Launch.Tools.JavaConfig;
using Project.Launch.Tools.LogsConfig;

namespace Project.Launch.Views
{
    public partial class DownloadWindow : Window
    {
        private readonly VersionEntry _version;
        private readonly string _targetPath;
        private readonly CancellationTokenSource _cts;
        private bool _hasStarted = false;

        /// <summary>本次安装结果（失败详情、文件计数），供调用方决定提示文案。</summary>
        public InstallResult? LastResult { get; private set; }

        public DownloadWindow(VersionEntry version, string targetPath, CancellationTokenSource cts)
        {
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            InitializeComponent();

            _version = version;
            _targetPath = targetPath;
            _cts = cts;

            TitleText.Text = $"正在下载 Minecraft {version.VersionId}";
        }

        private void OnStartClicked(object? sender, RoutedEventArgs e)
        {
            if (_hasStarted) return;
            _hasStarted = true;

            SetupPanel.IsVisible = false;
            ProgressPanel.IsVisible = true;

            int maxThreads = (int)(ThreadCountBox.Value ?? 20);
            if (maxThreads < 1) maxThreads = 1;
            if (maxThreads > 128) maxThreads = 128;

            _ = StartDownloadAsync(maxThreads);
        }

        private async Task StartDownloadAsync(int maxThreads)
        {
            var progress = new Progress<DownloadProgress>(UpdateUi);

            try
            {
                LastResult = await GameInstaller.InstallVanillaAsync(
                    _version, _targetPath, maxThreads, progress, _cts.Token);

                if (LastResult.Success)
                {
                    Close(true);
                }
                else
                {
                    // 不再把「缺文件的安装」当成功
                    StatusText.Text = LastResult.Summary;
                    LogHelper.Write(LauncherPaths.AppLog,
                        $"安装未完成：{LastResult.Summary}",
                        $"Install incomplete: {LastResult.Summary}");
                    Close(false);
                }
            }
            catch (OperationCanceledException)
            {
                LastResult = null;
                Close(false);
            }
            catch (Exception ex)
            {
                LogHelper.Write(LauncherPaths.AppLog,
                    $"下载异常: {ex.Message}",
                    $"Download error: {ex.Message}");
                Close(false);
            }
        }

        private void UpdateUi(DownloadProgress p)
        {
            StatusText.Text = p.State;
            MainProgressBar.Value = p.Percentage;

            SpeedText.Text = $"速度: {FormatBytes(p.SpeedBytesPerSec)}/s";

            if (p.TotalFiles > 0)
                FilesText.Text = p.FailedFiles > 0
                    ? $"文件: {p.CompletedFiles} / {p.TotalFiles}（失败 {p.FailedFiles}）"
                    : $"文件: {p.CompletedFiles} / {p.TotalFiles}";
            else
                FilesText.Text = "文件: -- / --";
        }

        private string FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            return $"{bytes / 1024.0 / 1024.0:F1} MB";
        }

        private void OnCancelClicked(object? sender, RoutedEventArgs e)
        {
            CancelButton.IsEnabled = false;
            CancelButton.Content = "正在取消...";
            _cts.Cancel();
        }
    }
}
```

### `MainSrc\Views\Glass.axaml`

```xml
<Styles xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:media="using:Avalonia.Media">

    <Styles.Resources>
        <!-- ============================================================ -->
        <!-- 液态玻璃参数总表 - 调参只改这里                              -->
        <!-- ============================================================ -->

        <!-- ★ 透明度控制（0~1）— 大幅降低，让窗口的亚克力透出来 -->
        <x:Double x:Key="GlassTintOpacity">0.12</x:Double>
        <x:Double x:Key="GlassTintOpacityHover">0.25</x:Double>
        <x:Double x:Key="GlassTintOpacityPressed">0.35</x:Double>

        <!-- ★ 边框 — 加强，让玻璃边缘有"实感" -->
        <Thickness x:Key="GlassBorderThickness">1.2</Thickness>
        <x:Double x:Key="GlassBorderOpacity">0.55</x:Double>
        <x:Double x:Key="GlassBorderOpacityHover">0.75</x:Double>

        <!-- ★ 圆角 -->
        <CornerRadius x:Key="GlassCornerRadius">14</CornerRadius>

        <!-- ★ 阴影 — 更柔更淡，避免显脏 -->
        <x:Double x:Key="GlassShadowBlur">14</x:Double>
        <x:Double x:Key="GlassShadowBlurHover">20</x:Double>
        <x:Double x:Key="GlassShadowOffsetY">6</x:Double>
        <x:Double x:Key="GlassShadowOffsetYHover">8</x:Double>
        <Color x:Key="GlassShadowColor">#33000000</Color>
        <Color x:Key="GlassShadowColorHover">#55000000</Color>

        <!-- ★ 高光 — 加强，模拟玻璃反射 -->
        <Color x:Key="GlassHighlightColor">#B3FFFFFF</Color>
        <x:Double x:Key="GlassHighlightEndOffset">0.7</x:Double>

        <!-- ============================================================ -->
        <!-- 派生资源                                                     -->
        <!-- ============================================================ -->

        <SolidColorBrush x:Key="GlassBrush"
                         Color="{StaticResource MiretPrimaryColor}"
                         Opacity="{StaticResource GlassTintOpacity}" />
        <SolidColorBrush x:Key="GlassBrushHover"
                         Color="{StaticResource MiretHoverColor}"
                         Opacity="{StaticResource GlassTintOpacityHover}" />
        <SolidColorBrush x:Key="GlassBrushPressed"
                         Color="{StaticResource MiretPressedColor}"
                         Opacity="{StaticResource GlassTintOpacityPressed}" />

        <SolidColorBrush x:Key="GlassBorderBrush"
                         Color="White"
                         Opacity="{StaticResource GlassBorderOpacity}" />
        <SolidColorBrush x:Key="GlassBorderBrushHover"
                         Color="White"
                         Opacity="{StaticResource GlassBorderOpacityHover}" />

        <media:DropShadowEffect x:Key="GlassShadow"
                                BlurRadius="{StaticResource GlassShadowBlur}"
                                OffsetX="0"
                                OffsetY="{StaticResource GlassShadowOffsetY}"
                                Color="{StaticResource GlassShadowColor}" />
        <media:DropShadowEffect x:Key="GlassShadowHover"
                                BlurRadius="{StaticResource GlassShadowBlurHover}"
                                OffsetX="0"
                                OffsetY="{StaticResource GlassShadowOffsetYHover}"
                                Color="{StaticResource GlassShadowColorHover}" />

        <LinearGradientBrush x:Key="GlassHighlight"
                             StartPoint="0,0"
                             EndPoint="1,1">
            <GradientStop Color="{StaticResource GlassHighlightColor}"
                          Offset="0.0" />
            <GradientStop Color="#00FFFFFF"
                          Offset="{StaticResource GlassHighlightEndOffset}" />
        </LinearGradientBrush>

        <!-- ★ 文字投影 — 让字从半透明背景中"浮"出来 -->
        <media:DropShadowEffect x:Key="GlassTextShadow"
                                BlurRadius="4"
                                OffsetX="0"
                                OffsetY="1"
                                Color="#55000000" />
    </Styles.Resources>

    <!-- ============================================================ -->
    <!-- 液态玻璃按钮样式                                             -->
    <!-- ============================================================ -->
    <Style Selector="Button.Glass">
        <Setter Property="ClipToBounds" Value="True" />
        <Setter Property="Background" Value="{StaticResource GlassBrush}" />
        <Setter Property="Foreground" Value="White" />
        <Setter Property="BorderBrush" Value="{StaticResource GlassBorderBrush}" />
        <Setter Property="BorderThickness" Value="{StaticResource GlassBorderThickness}" />
        <Setter Property="CornerRadius" Value="{StaticResource GlassCornerRadius}" />
        <Setter Property="Effect" Value="{StaticResource GlassShadow}" />
        <Setter Property="Cursor" Value="Hand" />
        <Setter Property="Padding" Value="20,8" />
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate>
                    <Grid>
                        <!-- 玻璃主层 -->
                        <Border Background="{TemplateBinding Background}"
                                BorderBrush="{TemplateBinding BorderBrush}"
                                BorderThickness="{TemplateBinding BorderThickness}"
                                CornerRadius="{TemplateBinding CornerRadius}" />
                        <!-- 左上角高光层（不阻挡鼠标） -->
                        <Border Background="{StaticResource GlassHighlight}"
                                CornerRadius="{TemplateBinding CornerRadius}"
                                IsHitTestVisible="False" />
                        <!-- 内容 -->
                        <ContentPresenter Content="{TemplateBinding Content}"
                                          HorizontalAlignment="Center"
                                          VerticalAlignment="Center" />
                    </Grid>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
        <Setter Property="Transitions">
            <Setter.Value>
                <Transitions>
                    <BrushTransition Property="Background" Duration="0:0:0.2" />
                    <BrushTransition Property="BorderBrush" Duration="0:0:0.2" />
                </Transitions>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- 悬停 -->
    <Style Selector="Button.Glass:pointerover">
        <Setter Property="Background" Value="{StaticResource GlassBrushHover}" />
        <Setter Property="BorderBrush" Value="{StaticResource GlassBorderBrushHover}" />
        <Setter Property="Effect" Value="{StaticResource GlassShadowHover}" />
    </Style>

    <!-- 按下 -->
    <Style Selector="Button.Glass:pressed">
        <Setter Property="Background" Value="{StaticResource GlassBrushPressed}" />
        <Setter Property="Effect" Value="{StaticResource GlassShadow}" />
    </Style>

    <!-- ★ 玻璃按钮内文字：加投影，字清晰 -->
    <Style Selector="Button.Glass TextBlock">
        <Setter Property="Foreground" Value="White" />
        <Setter Property="FontWeight" Value="Bold" />
        <Setter Property="Effect" Value="{StaticResource GlassTextShadow}" />
    </Style>
</Styles>
```

### `MainSrc\Views\LoginWindow.axaml`

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:media="clr-namespace:Avalonia.Media;assembly=Avalonia.Base"
        xmlns:avalonia="using:Avalonia.Controls"
        xmlns:lg="using:LiquidGlassAvaloniaUI"
        xmlns:sc="using:Project.Launch.Tools.AppConstants"
        x:Class="Project.Launch.Views.LoginWindow"
        Title="登录"
        Width="800" Height="440"
        WindowDecorations="None"
        ExtendClientAreaToDecorationsHint="True"
        ExtendClientAreaTitleBarHeightHint="-1"
        CanResize="False"
        Background="Transparent"
        TransparencyLevelHint="AcrylicBlur"
        avalonia:Win32Properties.WindowCornerPreference="Round">

    <Window.Styles>
        <!-- ============================================================ -->
        <!-- 模式选择按钮（离线 / 正版 / 第三方）                          -->
        <!--   · 未选中无鼠标 → 灰字                                      -->
        <!--   · 鼠标悬停     → 白字（无背景、无阴影）                    -->
        <!--   · 液态玻璃选中 → 加粗黑字                                  -->
        <!-- ============================================================ -->
        <Style Selector="Button.ModeBtn">
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate>
                        <ContentPresenter Content="{TemplateBinding Content}"
                                          HorizontalAlignment="Center"
                                          VerticalAlignment="Center" />
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
            <Setter Property="Background" Value="Transparent" />
            <Setter Property="BorderThickness" Value="0" />
            <Setter Property="Foreground" Value="#555555" />
            <Setter Property="FontWeight" Value="SemiBold" />
            <Setter Property="Cursor" Value="Hand" />
            <Setter Property="HorizontalAlignment" Value="Stretch" />
            <Setter Property="HorizontalContentAlignment" Value="Center" />
            <Setter Property="VerticalContentAlignment" Value="Center" />
            <Setter Property="Padding" Value="0" />
            <Setter Property="Transitions">
                <Transitions>
                    <BrushTransition Property="Foreground" Duration="0:0:0.15" />
                </Transitions>
            </Setter>
        </Style>

        <!-- 鼠标悬停：只改字色为白，不加任何背景/阴影 -->
        <Style Selector="Button.ModeBtn:pointerover">
            <Setter Property="Foreground" Value="White" />
        </Style>

        <!-- 液态玻璃选中的那一项：加粗黑字，无背景 -->
        <Style Selector="Button.ModeBtn.Selected">
            <Setter Property="Foreground" Value="Black" />
            <Setter Property="FontWeight" Value="Bold" />
        </Style>
    </Window.Styles>

    <Border CornerRadius="12" Background="White">
        <Grid>
            <Grid.RowDefinitions>
                <RowDefinition Height="*" />
                <RowDefinition Height="Auto" />
            </Grid.RowDefinitions>

            <Grid Grid.Row="0" Margin="30,30,30,15">
                <StackPanel HorizontalAlignment="Center"
                            VerticalAlignment="Center"
                            Spacing="15">

                    <Border x:Name="TitleContainer"
                            HorizontalAlignment="Center">
                        <Border.RenderTransform>
                            <TranslateTransform Y="-10" />
                        </Border.RenderTransform>

                        <StackPanel HorizontalAlignment="Center">
                            <TextBlock Text="{x:Static sc:SetConstants.LoginHeader}"
                                       FontSize="20"
                                       FontWeight="Bold"
                                       Foreground="#333"
                                       HorizontalAlignment="Center" />
                            <Grid Margin="0,10,0,0" Width="360" Height="44">
                                <Border Background="#D8D8D8" CornerRadius="8" />

                                <!-- ★ 液态玻璃滑块：位于按钮下层；
                                     HighlightEnabled=False 去掉玻璃自身白色高光 -->
                                <lg:LiquidGlassSurface x:Name="LoginModeSlider"
                                                       Width="114"
                                                       Height="36"
                                                       CornerRadius="6"
                                                       VerticalAlignment="Center"
                                                       HorizontalAlignment="Left"
                                                       Margin="3,0,3,0"
                                                       RenderTransformOrigin="0.5,0.5"
                                                       RefractionHeight="4"
                                                       RefractionAmount="40"
                                                       BlurRadius="0"
                                                       Vibrancy="0.86"
                                                       Brightness="0"
                                                       Contrast="1.0"
                                                       ExposureEv="0"
                                                       GammaPower="1.0"
                                                       BackdropOpacity="1.0"
                                                       ChromaticAberration="True"
                                                       DepthEffect="False"
                                                       HighlightEnabled="False"
                                                       ShadowEnabled="True"
                                                       ShadowRadius="24"
                                                       ShadowOffset="0,4"
                                                       ShadowColor="#1A000000">
                                    <lg:LiquidGlassSurface.RenderTransform>
                                        <TransformGroup>
                                            <ScaleTransform ScaleX="1" ScaleY="1" />
                                            <TranslateTransform X="120" Y="0" />
                                        </TransformGroup>
                                    </lg:LiquidGlassSurface.RenderTransform>
                                </lg:LiquidGlassSurface>

                                <!-- 按钮层：z-order 在玻璃之上；视觉由 Window.Styles 提供 -->
                                <Grid ColumnDefinitions="*, *, *">
                                    <Button x:Name="OfflineLoginButton"
                                            Grid.Column="0"
                                            Classes="ModeBtn"
                                            Content="离线登录" />
                                    <Button x:Name="OnlineLoginButton"
                                            Grid.Column="1"
                                            Classes="ModeBtn"
                                            Content="正版登录" />
                                    <Button x:Name="ThirdPartyLoginButton"
                                            Grid.Column="2"
                                            Classes="ModeBtn"
                                            Content="第三方登录" />
                                </Grid>
                            </Grid>
                        </StackPanel>
                    </Border>

                    <Border x:Name="InputPanel"
                            HorizontalAlignment="Center"
                            Height="240"
                            VerticalAlignment="Top"
                            Opacity="1">
                        <Border.Transitions>
                            <Transitions>
                                <DoubleTransition Property="Opacity"
                                                  Duration="0:0:0.25">
                                    <DoubleTransition.Easing>
                                        <CubicEaseInOut />
                                    </DoubleTransition.Easing>
                                </DoubleTransition>
                            </Transitions>
                        </Border.Transitions>

                        <Grid>
                            <!-- ============ 离线登录面板 ============ -->
                            <StackPanel x:Name="OfflinePanel"
                                        HorizontalAlignment="Center"
                                        VerticalAlignment="Top"
                                        Spacing="8"
                                        IsVisible="False">
                                <TextBox x:Name="PlayerNameBox"
                                         Width="200"
                                         Height="32"
                                         PlaceholderText="输入用户名"
                                         VerticalContentAlignment="Center" />
                                <Button x:Name="LoginButton"
                                        Classes="Miret"
                                        Content="确认登录"
                                        Width="130"
                                        Height="34"
                                        HorizontalAlignment="Center"
                                        Margin="0,5,0,0"
                                        Click="OnLoginClicked" />
                                <TextBlock x:Name="LoginStatus"
                                           FontSize="12"
                                           Foreground="#888"
                                           HorizontalAlignment="Center"
                                           Text="输入用户名，点击确认登录" />
                            </StackPanel>

                            <!-- ============ 正版登录面板 ============ -->
                            <Grid x:Name="MicrosoftPanel"
                                  HorizontalAlignment="Center"
                                  VerticalAlignment="Top"
                                  IsVisible="False">
                                <!-- 状态 1：初始 -->
                                <StackPanel x:Name="MsIdlePanel"
                                            Spacing="12"
                                            HorizontalAlignment="Center"
                                            VerticalAlignment="Center">
                                    <TextBlock Text="使用 Microsoft 账号登录"
                                               FontSize="14"
                                               Foreground="#555"
                                               HorizontalAlignment="Center" />
                                    <TextBlock Text="登录后可使用正版皮肤、联机"
                                               FontSize="12"
                                               Foreground="#888"
                                               HorizontalAlignment="Center" />
                                    <Button x:Name="MsLoginButton"
                                            Classes="Miret"
                                            Content="开始登录"
                                            Width="140"
                                            Height="36"
                                            HorizontalAlignment="Center"
                                            Margin="0,10,0,0"
                                            Click="OnMsLoginClicked" />
                                </StackPanel>

                                <!-- 状态 2：等待用户授权 -->
                                <StackPanel x:Name="MsWaitingPanel"
                                            Spacing="10"
                                            HorizontalAlignment="Center"
                                            VerticalAlignment="Center"
                                            IsVisible="False">
                                    <TextBlock Text="在浏览器打开以下网址："
                                               FontSize="13"
                                               Foreground="#555"
                                               HorizontalAlignment="Center" />
                                    <TextBlock x:Name="MsVerificationUri"
                                               FontSize="12"
                                               Foreground="#4C8F4C"
                                               HorizontalAlignment="Center" />
                                    <TextBlock Text="输入代码："
                                               FontSize="13"
                                               Foreground="#555"
                                               HorizontalAlignment="Center"
                                               Margin="0,8,0,0" />
                                    <TextBlock x:Name="MsUserCode"
                                               FontSize="22"
                                               FontWeight="Bold"
                                               FontFamily="Consolas,monospace"
                                               Foreground="#333"
                                               HorizontalAlignment="Center" />
                                    <StackPanel Orientation="Horizontal"
                                                HorizontalAlignment="Center"
                                                Spacing="10"
                                                Margin="0,6,0,0">
                                        <Button x:Name="MsCopyCodeButton"
                                                Classes="Miret"
                                                Content="复制代码"
                                                Padding="14,6"
                                                Click="OnMsCopyCodeClicked" />
                                        <Button x:Name="MsOpenBrowserButton"
                                                Classes="Miret"
                                                Content="打开浏览器"
                                                Padding="14,6"
                                                Click="OnMsOpenBrowserClicked" />
                                    </StackPanel>
                                    <TextBlock x:Name="MsStatus"
                                               FontSize="12"
                                               Foreground="#888"
                                               HorizontalAlignment="Center"
                                               Margin="0,8,0,0"
                                               Text="等待授权中..." />
                                    <Button x:Name="MsCancelButton"
                                            Classes="Miret"
                                            Content="取消"
                                            Padding="14,6"
                                            HorizontalAlignment="Center"
                                            Margin="0,4,0,0"
                                            Click="OnMsCancelClicked" />
                                </StackPanel>

                                <!-- 状态 3：认证链推进 -->
                                <StackPanel x:Name="MsProgressPanel"
                                            Spacing="10"
                                            HorizontalAlignment="Center"
                                            VerticalAlignment="Center"
                                            IsVisible="False">
                                    <TextBlock x:Name="MsProgressText"
                                               FontSize="14"
                                               Foreground="#333"
                                               HorizontalAlignment="Center" />
                                </StackPanel>
                            </Grid>

                            <!-- ============ 第三方登录面板 ============ -->
                            <StackPanel x:Name="ThirdPartyPanel"
                                        HorizontalAlignment="Center"
                                        VerticalAlignment="Top"
                                        Spacing="8"
                                        IsVisible="False">
                                <TextBlock Text="第三方登录暂未支持"
                                           FontSize="14"
                                           Foreground="#888"
                                           HorizontalAlignment="Center" />
                            </StackPanel>
                        </Grid>
                    </Border>
                </StackPanel>
            </Grid>

            <Border Grid.Row="1"
                    Height="44"
                    Background="#F5F5F5"
                    CornerRadius="0,0,12,12">
                <DockPanel Margin="15,0,15,0">
                    <TextBlock DockPanel.Dock="Left" Text="{x:Static sc:SetConstants.Version}" FontSize="11" Foreground="#999" VerticalAlignment="Center" />
                    <StackPanel DockPanel.Dock="Right" Orientation="Horizontal" VerticalAlignment="Center" Spacing="8">
                        <Button x:Name="GitHubButton" Background="Transparent" BorderThickness="0" Cursor="Hand" Padding="0" Width="44" Height="44" ToolTip.Tip="点击即访问仓库">
                            <Image Source="avares://Project.Launch/MainSrc/Views/imgs/Main/images/Support/github.png" Width="36" Height="36" Stretch="Uniform" />
                        </Button>
                        <TextBlock Text="请更换仓库不要露全部代码" FontSize="9" Foreground="#CC5555" VerticalAlignment="Center" Opacity="0.7" />
                    </StackPanel>
                </DockPanel>
            </Border>
        </Grid>
    </Border>
</Window>
```

### `MainSrc\Views\LoginWindow.xaml.cs`

```csharp
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using LiquidGlassAvaloniaUI;
using Project.Launch.Tools;
using Project.Launch.Auth;

using Project.Launch;
using Project.Launch.Tools.AnimSettings;
using Project.Launch.Tools.JavaConfig;
using Project.Launch.Tools.LogsConfig;
using Project.Launch.Tools.SoundControl;
namespace Project.Launch.Views
{
    public partial class LoginWindow : Window
    {
        private readonly HttpClient _httpClient = new HttpClient();

        public string PlayerName { get; private set; } = "Steve";
        public Bitmap? FaceBitmap { get; private set; }
        public Bitmap? HatBitmap { get; private set; }
        public string? OfflineUuid { get; private set; }

        // 顶部三按钮
        private Button? _offlineBtn;
        private Button? _onlineBtn;
        private Button? _thirdPartyBtn;
        private LiquidGlassSurface? _slider;
        private TranslateTransform? _sliderTransform;
        private ScaleTransform? _sliderScale;
        private int _selectedIndex = 1;

        // 标题 / 面板
        private Border? _titleContainer;
        private TranslateTransform? _titleTransform;
        private Border? _inputPanel;

        // 离线面板控件
        private TextBox? _playerNameBox;
        private Button? _loginButton;
        private TextBlock? _loginStatus;

        // 三个面板
        private StackPanel? _offlinePanel;
        private Grid? _microsoftPanel;
        private StackPanel? _thirdPartyPanel;

        // 正版：三种状态
        private StackPanel? _msIdlePanel;
        private StackPanel? _msWaitingPanel;
        private StackPanel? _msProgressPanel;
        private TextBlock? _msVerificationUri;
        private TextBlock? _msUserCode;
        private TextBlock? _msStatus;
        private TextBlock? _msProgressText;

        // 正版登录流程状态
        private MicrosoftAuthFlow? _msFlow;
        private CancellationTokenSource? _msCts;
        private string _msCurrentUserCode = "";
        private string _msCurrentVerificationUri = "";

        // 滑块动画
        private DispatcherTimer? _animationTimer;
        private double _animationStartX;
        private double _animationTargetX;
        private DateTime _animationStartTime;

        private const double OFFSET_OFFLINE = 0;
        private const double OFFSET_ONLINE = -10;
        private const double OFFSET_THIRD = -60;

        public LoginWindow()
        {
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            InitializeComponent();

            InitializeLoginModeSelector();
            InitializeInputControls();

            // 离线：回车登录
            if (_playerNameBox != null)
                _playerNameBox.KeyDown += (s, e) => { if (e.Key == Key.Enter) OnLoginClicked(s, e); };

            var gitHubButton = this.FindControl<Button>("GitHubButton");
            if (gitHubButton != null)
                gitHubButton.Click += OnGitHubClicked;

            _titleContainer = this.FindControl<Border>("TitleContainer");
            if (_titleContainer != null)
            {
                _titleTransform = _titleContainer.RenderTransform as TranslateTransform;
                if (_titleTransform == null)
                {
                    _titleTransform = new TranslateTransform();
                    _titleContainer.RenderTransform = _titleTransform;
                }
                _titleTransform.Y = OFFSET_ONLINE;
            }

            _inputPanel = this.FindControl<Border>("InputPanel");

            UpdateModeUI(1);
            SetDefaultSteveSkin();

            Opened += (s, e) => SoundService.Play("Open");
        }

        private void InitializeInputControls()
        {
            _playerNameBox = this.FindControl<TextBox>("PlayerNameBox");
            _loginButton = this.FindControl<Button>("LoginButton");
            _loginStatus = this.FindControl<TextBlock>("LoginStatus");

            _offlinePanel = this.FindControl<StackPanel>("OfflinePanel");
            _microsoftPanel = this.FindControl<Grid>("MicrosoftPanel");
            _thirdPartyPanel = this.FindControl<StackPanel>("ThirdPartyPanel");

            _msIdlePanel = this.FindControl<StackPanel>("MsIdlePanel");
            _msWaitingPanel = this.FindControl<StackPanel>("MsWaitingPanel");
            _msProgressPanel = this.FindControl<StackPanel>("MsProgressPanel");
            _msVerificationUri = this.FindControl<TextBlock>("MsVerificationUri");
            _msUserCode = this.FindControl<TextBlock>("MsUserCode");
            _msStatus = this.FindControl<TextBlock>("MsStatus");
            _msProgressText = this.FindControl<TextBlock>("MsProgressText");
        }

        private void InitializeLoginModeSelector()
        {
            _offlineBtn = this.FindControl<Button>("OfflineLoginButton");
            _onlineBtn = this.FindControl<Button>("OnlineLoginButton");
            _thirdPartyBtn = this.FindControl<Button>("ThirdPartyLoginButton");

            _slider = this.FindControl<LiquidGlassSurface>("LoginModeSlider");
            if (_slider != null)
            {
                var group = _slider.RenderTransform as TransformGroup;
                if (group != null)
                {
                    _sliderTransform = group.Children.OfType<TranslateTransform>().FirstOrDefault();
                    _sliderScale = group.Children.OfType<ScaleTransform>().FirstOrDefault();
                }

                if (_sliderTransform != null)
                {
                    _sliderTransform.X = 120;
                    _sliderTransform.Y = 0;
                }

                if (_sliderScale != null)
                {
                    _sliderScale.ScaleX = 1;
                    _sliderScale.ScaleY = 1;
                }
            }

            if (_offlineBtn != null) _offlineBtn.Click += (s, e) => SelectMode(0);
            if (_onlineBtn != null) _onlineBtn.Click += (s, e) => SelectMode(1);
            if (_thirdPartyBtn != null) _thirdPartyBtn.Click += (s, e) => SelectMode(2);

            UpdateButtonColors(1);
        }

        private void SelectMode(int index)
        {
            if (index == _selectedIndex) return;
            _selectedIndex = index;
            AnimateSliderTo(index);
            UpdateModeUI(index);
        }

        private void UpdateModeUI(int index)
        {
            if (_inputPanel != null)
                _inputPanel.Opacity = 0;

            Dispatcher.UIThread.InvokeAsync(async () =>
            {
                await Task.Delay(AnimationConfig.InputPanelFadeDuration);

                if (_offlinePanel != null) _offlinePanel.IsVisible = (index == 0);
                if (_microsoftPanel != null) _microsoftPanel.IsVisible = (index == 1);
                if (_thirdPartyPanel != null) _thirdPartyPanel.IsVisible = (index == 2);

                if (_inputPanel != null)
                    _inputPanel.Opacity = 1;
            });
        }

        // ============================================================
        // 滑块动画
        // ============================================================
        private void AnimateSliderTo(int index)
        {
            if (_sliderTransform == null || _slider == null) return;

            double targetX = index * 120;
            double startX = _sliderTransform.X;

            _animationTimer?.Stop();

            _animationStartX = startX;
            _animationTargetX = targetX;
            _animationStartTime = DateTime.Now;

            TimeSpan duration = AnimationConfig.SliderMoveDuration;
            double peakScale = AnimationConfig.SliderPeakScale;
            double riseEnd = AnimationConfig.SliderScaleRiseEnd;
            double fallStart = AnimationConfig.SliderScaleFallStart;
            double blurPeak = AnimationConfig.SliderBlurPeak;
            double halfW = AnimationConfig.SliderWidth / 2.0;
            double halfH = AnimationConfig.SliderHeight / 2.0;

            _animationTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16)
            };
            _animationTimer.Tick += (s, e) =>
            {
                var elapsed = DateTime.Now - _animationStartTime;
                double progress = elapsed.TotalMilliseconds / duration.TotalMilliseconds;
                if (progress >= 1.0)
                {
                    progress = 1.0;
                    _animationTimer.Stop();
                }

                double eased = progress < 0.5
                    ? 4 * progress * progress * progress
                    : 1 - Math.Pow(-2 * progress + 2, 3) / 2;

                double baseX = _animationStartX + (_animationTargetX - _animationStartX) * eased;

                double scaleCurve;
                if (progress <= riseEnd)
                {
                    double t = progress / riseEnd;
                    scaleCurve = t * t * (3 - 2 * t);
                }
                else if (progress <= fallStart)
                {
                    scaleCurve = 1.0;
                }
                else
                {
                    double t = (progress - fallStart) / (1.0 - fallStart);
                    scaleCurve = 1.0 - t * t * (3 - 2 * t);
                }

                double currentScale = 1.0 + (peakScale - 1.0) * scaleCurve;

                // ★ 还原：模糊驱动（三角曲线，与 MainWindow 的滑块动画保持一致）
                double blurCurve = 1.0 - Math.Abs(progress * 2.0 - 1.0);
                _slider.BlurRadius = blurPeak * blurCurve;

                double xCompensation = -(currentScale - 1.0) * halfW;
                double yCompensation = -(currentScale - 1.0) * halfH;

                _sliderTransform.X = baseX + xCompensation;
                _sliderTransform.Y = yCompensation;

                if (_sliderScale != null)
                {
                    _sliderScale.ScaleX = currentScale;
                    _sliderScale.ScaleY = currentScale;
                }

                if (progress >= 1.0)
                {
                    _sliderTransform.X = _animationTargetX;
                    _sliderTransform.Y = 0;
                    if (_sliderScale != null)
                    {
                        _sliderScale.ScaleX = 1.0;
                        _sliderScale.ScaleY = 1.0;
                    }
                    _slider.BlurRadius = 0;
                }
            };

            _animationTimer.Start();

            UpdateButtonColors(index);
        }

        /// <summary>
        /// 通过样式类切换选中态。
        /// 视觉（灰色 / 悬停白字深底药丸 / 选中加粗黑字）由 LoginWindow.axaml 的
        /// Button.ModeBtn 系列样式定义，这里只负责切换 "Selected" 类与命中测试开关。
        /// </summary>
        private void UpdateButtonColors(int selectedIndex)
        {
            var buttons = new[] { _offlineBtn, _onlineBtn, _thirdPartyBtn };
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] == null) continue;

                if (i == selectedIndex)
                {
                    if (!buttons[i]!.Classes.Contains("Selected"))
                        buttons[i]!.Classes.Add("Selected");
                }
                else
                {
                    buttons[i]!.Classes.Remove("Selected");
                }

                buttons[i]!.IsHitTestVisible = (i != selectedIndex);
            }
        }

        // ============================================================
        // 离线登录
        // ============================================================
        public void OnLoginClicked(object sender, RoutedEventArgs e)
        {
            if (_playerNameBox == null || string.IsNullOrWhiteSpace(_playerNameBox.Text))
            {
                if (_loginStatus != null) _loginStatus.Text = "❌ 请输入用户名";
                return;
            }

            string username = _playerNameBox.Text.Trim();
            try
            {
                string uuidStr = OfflineUuidGenerator.GenerateUuidString(username).Replace("-", "");

                App.PlayerName = username;
                App.OfflineUuid = uuidStr;
                App.IsLoggedIn = true;
                App.LoginCheck = true;

                var account = new AccountInfo
                {
                    Type = "offline",
                    Username = username,
                    Uuid = uuidStr,
                    LastUsed = DateTime.UtcNow
                };
                string accountId = $"offline_{username}";
                AccountService.SetSelectedAccount(accountId, account);
                AccountService.SetLoginCheck(true);

                Close(true);
            }
            catch (Exception ex)
            {
                if (_loginStatus != null)
                    _loginStatus.Text = $"❌ 保存账户失败: {ex.Message}";
            }
        }

        // ============================================================
        // 正版登录：点击"开始登录"
        // ============================================================
        private async void OnMsLoginClicked(object? sender, RoutedEventArgs e)
        {
            if (_msIdlePanel != null) _msIdlePanel.IsVisible = false;
            if (_msWaitingPanel != null) _msWaitingPanel.IsVisible = true;
            if (_msProgressPanel != null) _msProgressPanel.IsVisible = false;

            _msCts = new CancellationTokenSource();
            _msFlow = new MicrosoftAuthFlow();

            _msFlow.DeviceCodeReady += (userCode, verificationUri) =>
            {
                _msCurrentUserCode = userCode;
                _msCurrentVerificationUri = verificationUri;

                Dispatcher.UIThread.Post(() =>
                {
                    if (_msUserCode != null) _msUserCode.Text = userCode;
                    if (_msVerificationUri != null) _msVerificationUri.Text = verificationUri;

                    try
                    {
                        string url = verificationUri + "?otc=" + Uri.EscapeDataString(userCode);
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = url,
                            UseShellExecute = true
                        });
                    }
                    catch { }
                });
            };

            _msFlow.Progress += msg =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (_msStatus != null) _msStatus.Text = msg;
                });
            };

            var account = await _msFlow.LoginAsync(_msCts.Token);

            if (account == null)
            {
                if (_msWaitingPanel != null) _msWaitingPanel.IsVisible = false;
                if (_msIdlePanel != null) _msIdlePanel.IsVisible = true;
                return;
            }

            try
            {
                string accountId = $"microsoft_{account.Uuid}";

                App.PlayerName = account.Username;
                App.OfflineUuid = account.Uuid;
                App.IsLoggedIn = true;
                App.LoginCheck = true;

                AccountService.SetSelectedAccount(accountId, account);
                AccountService.SetLoginCheck(true);

                LogHelper.Write(LauncherPaths.AppLog,
                    $"正版账户已保存: {account.Username}",
                    $"Microsoft account saved: {account.Username}");

                Close(true);
            }
            catch (Exception ex)
            {
                LogHelper.Write(LauncherPaths.AppLog,
                    $"保存正版账户失败: {ex.Message}",
                    $"Save Microsoft account failed: {ex.Message}");
            }
        }

        // ============================================================
        // 正版登录：复制代码
        // ============================================================
        private async void OnMsCopyCodeClicked(object? sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_msCurrentUserCode)) return;
            try
            {
                var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
                if (clipboard != null)
                {
                    await clipboard.SetTextAsync(_msCurrentUserCode);
                }

                if (_msStatus != null)
                    _msStatus.Text = "已复制代码";
            }
            catch { }
        }

        // ============================================================
        // 正版登录：手动打开浏览器
        // ============================================================
        private void OnMsOpenBrowserClicked(object? sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_msCurrentVerificationUri)) return;
            try
            {
                string url = _msCurrentVerificationUri;
                if (!string.IsNullOrEmpty(_msCurrentUserCode))
                    url += "?otc=" + Uri.EscapeDataString(_msCurrentUserCode);

                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch { }
        }

        // ============================================================
        // 正版登录：取消
        // ============================================================
        private void OnMsCancelClicked(object? sender, RoutedEventArgs e)
        {
            _msCts?.Cancel();

            if (_msWaitingPanel != null) _msWaitingPanel.IsVisible = false;
            if (_msProgressPanel != null) _msProgressPanel.IsVisible = false;
            if (_msIdlePanel != null) _msIdlePanel.IsVisible = true;
        }

        // ============================================================
        // GitHub
        // ============================================================
        private async void OnGitHubClicked(object? sender, EventArgs e)
        {
            try
            {
                var uri = new Uri("https://github.com/Zhouxuanyao2333/KilimMinecraftLauncher");
                var launcher = TopLevel.GetTopLevel(this)?.Launcher;
                if (launcher != null)
                    await launcher.LaunchUriAsync(uri);
                else
                {
                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = uri.ToString(),
                        UseShellExecute = true
                    };
                    Process.Start(psi);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"打开链接失败: {ex.Message}");
                if (_loginStatus != null)
                    _loginStatus.Text = "❌ 打开链接失败，请手动访问仓库";
            }
        }

        // ============================================================
        // 默认 Steve 皮肤（离线登录用）
        // ============================================================
        private void SetDefaultSteveSkin()
        {
            try
            {
                var stevePath = "avares://Project.Launch/MainSrc/Views/imgs/skin/Steve.png";
                var uri = new Uri(stevePath);
                using var stream = AssetLoader.Open(uri);

                var result = CropSkinAsync(stream).GetAwaiter().GetResult();

                if (result.face != null && result.hat != null)
                {
                    FaceBitmap = result.face;
                    HatBitmap = result.hat;
                    PlayerName = "Steve";
                }
            }
            catch (Exception ex)
            {
                LogHelper.Write(LauncherPaths.AppLog,
                    $"加载默认皮肤失败: {ex.Message}",
                    $"Failed to load default skin: {ex.Message}");
            }
        }

        private async Task<(Bitmap? face, Bitmap? hat)> CropSkinAsync(Stream stream)
        {
            try
            {
                byte[] imageData;
                using (var ms = new MemoryStream())
                {
                    await stream.CopyToAsync(ms).ConfigureAwait(false);
                    imageData = ms.ToArray();
                }

                return await Task.Run<(Bitmap? face, Bitmap? hat)>(() =>
                {
                    try
                    {
                        using var bitmap = new Bitmap(new MemoryStream(imageData));
                        int width = bitmap.PixelSize.Width;
                        int height = bitmap.PixelSize.Height;

                        bool isValidSize = (width == 64 && height == 32) ||
                                           (width == 64 && height == 64) ||
                                           (width == 128 && height == 128);
                        if (!isValidSize)
                            return (null, null);

                        int faceX, faceY, faceSize, hatX, hatY;

                        if (width >= 128 && height >= 128)
                        {
                            faceX = 16; faceY = 16; faceSize = 16;
                            hatX = 80; hatY = 16;
                        }
                        else
                        {
                            faceX = 8; faceY = 8; faceSize = 8;
                            hatX = 40; hatY = 8;
                        }

                        var faceCropped = new CroppedBitmap(bitmap, new PixelRect(faceX, faceY, faceSize, faceSize));
                        var hatCropped = new CroppedBitmap(bitmap, new PixelRect(hatX, hatY, faceSize, faceSize));

                        var faceBmp = new RenderTargetBitmap(new PixelSize(faceSize, faceSize), new Vector(96, 96));
                        using (var ctx = faceBmp.CreateDrawingContext())
                            ctx.DrawImage(faceCropped, new Rect(0, 0, faceSize, faceSize));

                        var hatBmp = new RenderTargetBitmap(new PixelSize(faceSize, faceSize), new Vector(96, 96));
                        using (var ctx = hatBmp.CreateDrawingContext())
                            ctx.DrawImage(hatCropped, new Rect(0, 0, faceSize, faceSize));

                        return (faceBmp, hatBmp);
                    }
                    catch
                    {
                        return (null, null);
                    }
                }).ConfigureAwait(false);
            }
            catch
            {
                return (null, null);
            }
        }

        private void Close(bool isLogin) => Close();
    }
}
```

### `MainSrc\Views\MainWindow.axaml`

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:avalonia="clr-namespace:Avalonia.Controls;assembly=Avalonia.Controls"
        xmlns:lg="using:LiquidGlassAvaloniaUI"
        xmlns:p3d="clr-namespace:Project.Launch.Tools.Player3DConfig"
        xmlns:dl="clr-namespace:Project.Launch.Download"
        xmlns:sc="using:Project.Launch.Tools.AppConstants"
        x:Class="Project.Launch.Views.MainWindow"
        Title="{x:Static sc:SetConstants.AppName}"
        Width="960" Height="560"
        WindowDecorations="None"
        ExtendClientAreaToDecorationsHint="True"
        ExtendClientAreaTitleBarHeightHint="-1"
        CanResize="False"
        Background="Transparent"
        TransparencyLevelHint="AcrylicBlur"
        avalonia:Win32Properties.WindowCornerPreference="Round">

    <Window.Styles>
        <Style Selector="Button.TabBtn">
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate>
                        <ContentPresenter Content="{TemplateBinding Content}"
                                          HorizontalAlignment="Stretch"
                                          VerticalAlignment="Stretch"
                                          HorizontalContentAlignment="Stretch"
                                          VerticalContentAlignment="Stretch" />
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>
    </Window.Styles>

    <Grid Background="#D9E0E0E0">

        <Grid.RowDefinitions>
            <RowDefinition Height="35" />
            <RowDefinition Height="*" />
        </Grid.RowDefinitions>

        <!-- ★ 标题栏 -->
        <Border Grid.Row="0"
                x:Name="TitleBarBorder"
                PointerPressed="TitleBar_PointerPressed"
                CornerRadius="12,12,0,0">
            <Border.Background>
                <LinearGradientBrush StartPoint="0%,0%" EndPoint="100%,0%">
                    <GradientStop Color="#CCFF6B9D" Offset="0.0"/>
                    <GradientStop Color="#CCB366FF" Offset="1.0"/>
                </LinearGradientBrush>
            </Border.Background>
            <Border.Transitions>
                <Transitions>
                    <BrushTransition Property="Background" Duration="0:0:0.4" />
                </Transitions>
            </Border.Transitions>

            <Grid ColumnDefinitions="*, Auto, Auto">
                <TextBlock Grid.Column="0"
                           x:Name="TitleText"
                           Text="{x:Static sc:SetConstants.AppName}"
                           FontSize="16"
                           Foreground="White"
                           VerticalAlignment="Center"
                           Margin="10,0,0,0">
                    <TextBlock.Transitions>
                        <Transitions>
                            <BrushTransition Property="Foreground" Duration="0:0:0.4" />
                        </Transitions>
                    </TextBlock.Transitions>
                </TextBlock>

                <Button Grid.Column="1"
                        x:Name="MinimizeButton"
                        Background="Transparent"
                        BorderThickness="0"
                        Width="45" Height="35"
                        Cursor="Hand">
                    <Image x:Name="MinimizeImage"
                           Source="avares://Project.Launch/MainSrc/Views/imgs/Main/images/Buttons/white-.png"
                           Width="20" Height="26" Stretch="Fill"
                           VerticalAlignment="Center" HorizontalAlignment="Center"
                           Opacity="1">
                        <Image.Transitions>
                            <Transitions>
                                <DoubleTransition Property="Opacity" Duration="0:0:0.2" />
                            </Transitions>
                        </Image.Transitions>
                    </Image>
                </Button>

                <Button Grid.Column="2"
                        x:Name="CloseButton"
                        Background="Transparent"
                        BorderThickness="0"
                        Width="45" Height="35"
                        Cursor="Hand"
                        Margin="0,0,10,0">
                    <Image x:Name="CloseImage"
                           Source="avares://Project.Launch/MainSrc/Views/imgs/Main/images/Buttons/whitex.png"
                           Width="20" Height="20" Stretch="Uniform"
                           VerticalAlignment="Center" HorizontalAlignment="Center"
                           Opacity="1">
                        <Image.Transitions>
                            <Transitions>
                                <DoubleTransition Property="Opacity" Duration="0:0:0.2" />
                            </Transitions>
                        </Image.Transitions>
                    </Image>
                </Button>
            </Grid>
        </Border>

        <!-- ★ 主内容区域 -->
        <Grid x:Name="ContentArea" Grid.Row="1" Margin="20">
            <Grid.RowDefinitions>
                <RowDefinition Height="*" />
                <RowDefinition Height="Auto" />
            </Grid.RowDefinitions>

            <!-- ★ 内容区 -->
            <Grid Grid.Row="0">

                <!-- ★ 主页 -->
                <Grid x:Name="HomePanel" IsVisible="True" Opacity="1">
                    <Grid.Transitions>
                        <Transitions>
                            <DoubleTransition Property="Opacity" Duration="0:0:0.2" />
                        </Transitions>
                    </Grid.Transitions>

                    <!-- 未登录提示 + ReLogin 按钮 -->
                    <StackPanel x:Name="HomeNotLoggedIn"
                                HorizontalAlignment="Center"
                                VerticalAlignment="Center"
                                Spacing="24"
                                IsVisible="False">
                        <TextBlock Text="您似乎未登录任何账号绑定至启动器或实例"
                                   FontSize="18"
                                   Foreground="#555"
                                   HorizontalAlignment="Center" />

                        <Grid x:Name="ReLoginContainer"
                              Width="292" Height="85"
                              HorizontalAlignment="Center">
                            <Button x:Name="ReLoginButton"
                                    Width="275"
                                    Height="80"
                                    Padding="0"
                                    Background="Transparent"
                                    BorderThickness="0"
                                    Cursor="Hand"
                                    HorizontalAlignment="Center"
                                    VerticalAlignment="Center">
                                <Button.Styles>
                                    <Style Selector="Button">
                                        <Setter Property="Template">
                                            <Setter.Value>
                                                <ControlTemplate>
                                                    <ContentPresenter Content="{TemplateBinding Content}"
                                                                      HorizontalAlignment="Stretch"
                                                                      VerticalAlignment="Stretch"
                                                                      HorizontalContentAlignment="Stretch"
                                                                      VerticalContentAlignment="Stretch" />
                                                </ControlTemplate>
                                            </Setter.Value>
                                        </Setter>
                                    </Style>
                                </Button.Styles>
                                <Grid>
                                    <Image Source="avares://Project.Launch/MainSrc/Views/imgs/Main/images/Buttons/ReLogin.png"
                                           Stretch="Fill"
                                           HorizontalAlignment="Stretch"
                                           VerticalAlignment="Stretch" />
                                    <TextBlock Text="登录"
                                               FontSize="24"
                                               FontWeight="Bold"
                                               Foreground="White"
                                               HorizontalAlignment="Center"
                                               VerticalAlignment="Center" />
                                </Grid>
                            </Button>
                        </Grid>
                    </StackPanel>

                    <!-- 已登录：头像 + ID + 切换账户 -->
                    <StackPanel x:Name="HomeLoggedIn"
                                HorizontalAlignment="Center"
                                VerticalAlignment="Center"
                                Spacing="12"
                                IsVisible="False">
                        <Grid Width="160" Height="160"
                              HorizontalAlignment="Center">
                            <Image x:Name="HomeFaceImage"
                                   Stretch="Uniform"
                                   Width="120" Height="120"
                                   HorizontalAlignment="Center"
                                   VerticalAlignment="Center"
                                   RenderOptions.BitmapInterpolationMode="None" />
                            <Image x:Name="HomeHatImage"
                                   Stretch="Uniform"
                                   Width="120" Height="120"
                                   HorizontalAlignment="Center"
                                   VerticalAlignment="Center"
                                   Margin="-3,-3,-3,-3"
                                   RenderOptions.BitmapInterpolationMode="None" />
                            <Border Width="120" Height="120"
                                    BorderBrush="#DDD"
                                    BorderThickness="2"
                                    CornerRadius="4"
                                    IsHitTestVisible="False"
                                    HorizontalAlignment="Center"
                                    VerticalAlignment="Center" />
                        </Grid>
                        <TextBlock x:Name="HomePlayerName"
                                   Text="Steve"
                                   FontSize="22"
                                   FontWeight="Bold"
                                   Foreground="#333"
                                   HorizontalAlignment="Center" />

                        <Button x:Name="SwitchAccountButton"
                                Classes="Miret"
                                Content="切换账户"
                                Padding="16,7"
                                FontSize="13"
                                HorizontalAlignment="Center"
                                Margin="0,6,0,0"
                                Click="OnSwitchAccountClicked" />
                    </StackPanel>
                </Grid>

                <!-- ★ 启动页 -->
                <Grid x:Name="LaunchPanel" IsVisible="False" Opacity="1">
                    <Grid.Transitions>
                        <Transitions>
                            <DoubleTransition Property="Opacity" Duration="0:0:0.2" />
                        </Transitions>
                    </Grid.Transitions>

                    <Grid.RowDefinitions>
                        <RowDefinition Height="*" />
                        <RowDefinition Height="Auto" />
                    </Grid.RowDefinitions>

                    <p3d:Pivot3DControl x:Name="SkinViewer"
                                          Grid.Row="0"
                                          HorizontalAlignment="Stretch"
                                          VerticalAlignment="Stretch"
                                          Margin="0,10,0,10"
                                          IsVisible="False" />

                    <StackPanel x:Name="LaunchNotLoggedIn"
                                Grid.Row="0"
                                HorizontalAlignment="Center"
                                VerticalAlignment="Center"
                                Spacing="10"
                                IsVisible="False">
                        <TextBlock Text="请登录后查看"
                                   FontSize="20"
                                   FontWeight="SemiBold"
                                   Foreground="#555"
                                   HorizontalAlignment="Center" />
                        <TextBlock Text="请先在主页完成登录"
                                   FontSize="13"
                                   Foreground="#888"
                                   HorizontalAlignment="Center" />
                    </StackPanel>

                    <StackPanel x:Name="LaunchButtonsPanel"
                                Grid.Row="1"
                                Orientation="Horizontal"
                                HorizontalAlignment="Center"
                                Spacing="10"
                                Margin="0,8,0,8"
                                IsVisible="False">

                        <Button x:Name="LaunchGameButton"
                                Width="220"
                                Height="80"
                                Padding="0"
                                Background="Transparent"
                                BorderThickness="0"
                                Cursor="Hand"
                                IsVisible="False">
                            <Button.Styles>
                                <Style Selector="Button">
                                    <Setter Property="Template">
                                        <Setter.Value>
                                            <ControlTemplate>
                                                <ContentPresenter Content="{TemplateBinding Content}"
                                                                  HorizontalAlignment="Stretch"
                                                                  VerticalAlignment="Stretch"
                                                                  HorizontalContentAlignment="Stretch"
                                                                  VerticalContentAlignment="Stretch" />
                                            </ControlTemplate>
                                        </Setter.Value>
                                    </Setter>
                                </Style>
                            </Button.Styles>
                            <Grid>
                                <Image x:Name="LaunchGameImage"
                                       Source="avares://Project.Launch/MainSrc/Views/imgs/Main/images/Buttons/button-Start3.png"
                                       Stretch="Fill" />
                                <TextBlock x:Name="LaunchGameText"
                                           Text="导入实例"
                                           FontSize="24"
                                           FontWeight="Bold"
                                           Foreground="White"
                                           HorizontalAlignment="Center"
                                           VerticalAlignment="Center" />
                            </Grid>
                        </Button>

                        <Button x:Name="ExpandButton"
                                Width="45"
                                Height="80"
                                Padding="0"
                                Background="Transparent"
                                BorderThickness="0"
                                Cursor="Hand"
                                IsVisible="False">
                            <Button.Styles>
                                <Style Selector="Button">
                                    <Setter Property="Template">
                                        <Setter.Value>
                                            <ControlTemplate>
                                                <ContentPresenter Content="{TemplateBinding Content}"
                                                                  HorizontalAlignment="Stretch"
                                                                  VerticalAlignment="Stretch"
                                                                  HorizontalContentAlignment="Stretch"
                                                                  VerticalContentAlignment="Stretch" />
                                            </ControlTemplate>
                                        </Setter.Value>
                                    </Setter>
                                </Style>
                            </Button.Styles>
                            <Grid>
                                <Image x:Name="ExpandImage"
                                       Source="avares://Project.Launch/MainSrc/Views/imgs/Main/images/Buttons/button-Start4.png"
                                       Stretch="Fill" />
                                <TextBlock Text="^"
                                           FontSize="30"
                                           FontWeight="Bold"
                                           Foreground="White"
                                           HorizontalAlignment="Center"
                                           VerticalAlignment="Center" />
                            </Grid>
                        </Button>
                    </StackPanel>
                </Grid>

                <!-- ★ 下载 -->
                <Grid x:Name="DownloadPanel" IsVisible="False" Opacity="1">
                    <Grid.Transitions>
                        <Transitions>
                            <DoubleTransition Property="Opacity" Duration="0:0:0.2" />
                        </Transitions>
                    </Grid.Transitions>

                    <Grid.RowDefinitions>
                        <RowDefinition Height="Auto" />
                        <RowDefinition Height="*" />
                    </Grid.RowDefinitions>

                    <!-- 版本类型选择（安全区左上角） -->
                    <Grid Grid.Row="0" HorizontalAlignment="Left" Margin="0,0,0,12">
                        <Border Background="#D8D8D8" CornerRadius="8" Width="240" Height="44" />
                        <Grid ColumnDefinitions="*,*" Width="240" Height="44">
                            <Button x:Name="BedrockEditionButton"
                                    Grid.Column="0"
                                    Background="Transparent"
                                    BorderThickness="0"
                                    Foreground="#666666"
                                    FontWeight="SemiBold"
                                    FontSize="14"
                                    Content="基岩版"
                                    Cursor="Hand"
                                    HorizontalAlignment="Stretch"
                                    HorizontalContentAlignment="Center"
                                    Padding="0" />
                            <Button x:Name="JavaEditionButton"
                                    Grid.Column="1"
                                    Background="Transparent"
                                    BorderThickness="0"
                                    Foreground="#666666"
                                    FontWeight="SemiBold"
                                    FontSize="14"
                                    Content="Java版"
                                    Cursor="Hand"
                                    HorizontalAlignment="Stretch"
                                    HorizontalContentAlignment="Center"
                                    Padding="0" />
                        </Grid>

                        <lg:LiquidGlassSurface x:Name="EditionSlider"
                                               Width="114"
                                               Height="36"
                                               CornerRadius="6"
                                               VerticalAlignment="Center"
                                               HorizontalAlignment="Left"
                                               Margin="3,0,3,0"
                                               RenderTransformOrigin="0.5,0.5"
                                               RefractionHeight="4"
                                               RefractionAmount="40"
                                               BlurRadius="0"
                                               Vibrancy="0.86"
                                               Brightness="0"
                                               Contrast="1.0"
                                               ExposureEv="0"
                                               GammaPower="1.0"
                                               BackdropOpacity="1.0"
                                               ChromaticAberration="True"
                                               DepthEffect="False"
                                               HighlightEnabled="True"
                                               ShadowEnabled="True"
                                               ShadowRadius="24"
                                               ShadowOffset="0,4"
                                               ShadowColor="#1A000000">
                            <lg:LiquidGlassSurface.RenderTransform>
                                <TransformGroup>
                                    <ScaleTransform ScaleX="1" ScaleY="1" />
                                    <TranslateTransform X="0" Y="0" />
                                </TransformGroup>
                            </lg:LiquidGlassSurface.RenderTransform>
                        </lg:LiquidGlassSurface>
                    </Grid>

                    <!-- 安全区（负责显示内容） -->
                    <Border Grid.Row="1"
                            Background="White"
                            CornerRadius="12">
                        <Border.Effect>
                            <DropShadowEffect BlurRadius="24"
                                              OffsetX="0"
                                              OffsetY="8"
                                              Color="#1A000000" />
                        </Border.Effect>

                        <Grid>
                            <!-- 基岩版面板 -->
                            <StackPanel x:Name="BedrockPanel"
                                        HorizontalAlignment="Center"
                                        VerticalAlignment="Center"
                                        Spacing="10">
                                <TextBlock Text="基岩版"
                                           FontSize="22"
                                           FontWeight="SemiBold"
                                           Foreground="#555"
                                           HorizontalAlignment="Center" />
                                <TextBlock Text="版本列表（待接入）"
                                           FontSize="14"
                                           Foreground="#888"
                                           HorizontalAlignment="Center" />
                                <TextBlock Text="自觉遵守 Demo 测试保密"
                                           FontSize="12"
                                           Foreground="#AAA"
                                           HorizontalAlignment="Center"
                                           Margin="0,8,0,0" />
                            </StackPanel>

                            <!-- ★ Java版面板（版本列表） -->
                            <Grid x:Name="JavaPanel" IsVisible="False" RowDefinitions="Auto, *" Margin="20">
                                <!-- 顶部工具栏：类型筛选 + 搜索 -->
                                <Grid Grid.Row="0" ColumnDefinitions="Auto, *">
                                    <ComboBox x:Name="VersionTypeFilter" SelectedIndex="0" Width="140" 
                                              SelectionChanged="OnVersionFilterChanged">
                                        <ComboBoxItem Content="全部版本" Tag="all" />
                                        <ComboBoxItem Content="正式版 (Release)" Tag="release" />
                                        <ComboBoxItem Content="快照版 (Snapshot)" Tag="snapshot" />
                                    </ComboBox>
                                    
                                    <TextBox x:Name="VersionSearchBox" Grid.Column="1" PlaceholderText="搜索版本号..." 
                                             Width="220" HorizontalAlignment="Right" TextChanged="OnVersionSearchChanged" />
                                </Grid>
                                
                                <!-- 列表区 -->
                                <Border Grid.Row="1" Margin="0,15,0,0" BorderBrush="#E0E0E0" BorderThickness="1" CornerRadius="8">
                                    <Grid>
                                        <!-- 版本列表 -->
                                        <ListBox x:Name="VersionListBox" Background="Transparent" BorderThickness="0" 
                                                 DoubleTapped="OnVersionListDoubleTapped" SelectionChanged="OnVersionSelected">
                                            <ListBox.ItemTemplate>
                                                <DataTemplate x:DataType="dl:VersionEntry">
                                                    <DockPanel Margin="0,5">
                                                        <!-- 右侧：日期 -->
                                                        <TextBlock DockPanel.Dock="Right" Text="{Binding ReleaseTimeStr}" Foreground="#AAA" 
                                                                   VerticalAlignment="Center" Margin="0,0,15,0" />
                                                        <!-- 右侧：类型 -->
                                                        <TextBlock DockPanel.Dock="Right" Text="{Binding Type}" Foreground="#888" 
                                                                   VerticalAlignment="Center" Width="80" />
                                                        <!-- 左侧：图标 -->
                                                        <Image DockPanel.Dock="Left" Source="{Binding IconBitmap}" Width="24" Height="24" 
                                                               VerticalAlignment="Center" />
                                                        <!-- ★ 最关键：最后一个元素不要写 Dock，让它填满中间，且显式赋予深色字体 -->
                                                        <TextBlock Text="{Binding VersionId}" Foreground="#333" FontWeight="SemiBold" 
                                                                   VerticalAlignment="Center" Margin="10,0,0,0" FontSize="15" />
                                                    </DockPanel>
                                                </DataTemplate>
                                            </ListBox.ItemTemplate>
                                        </ListBox>
                                        
                                        <!-- 加载中提示 -->
                                        <StackPanel x:Name="VersionLoadingPanel" HorizontalAlignment="Center" VerticalAlignment="Center" Spacing="10">
                                            <TextBlock Text="正在连接镜像 API 获取版本列表..." Foreground="#666" FontSize="14"/>
                                        </StackPanel>
                                        
                                        <!-- 加载失败提示 -->
                                        <StackPanel x:Name="VersionErrorPanel" IsVisible="False" HorizontalAlignment="Center" VerticalAlignment="Center" Spacing="15">
                                            <TextBlock Text="获取版本列表失败，请检查网络连接" Foreground="#CC5555" FontSize="14"/>
                                            <Button Content="重试" Click="OnRetryVersionsClicked" Classes="Miret" HorizontalAlignment="Center" />
                                        </StackPanel>
                                    </Grid>
                                </Border>
                            </Grid>
                        </Grid>
                    </Border>
                </Grid>

                <!-- ★ 设置（占位） -->
                <Grid x:Name="SettingsPanel" IsVisible="False" Opacity="1">
                    <Grid.Transitions>
                        <Transitions>
                            <DoubleTransition Property="Opacity" Duration="0:0:0.2" />
                        </Transitions>
                    </Grid.Transitions>
                    <StackPanel HorizontalAlignment="Center"
                                VerticalAlignment="Center"
                                Spacing="10">
                        <TextBlock Text="暂时无设置功能"
                                   FontSize="22"
                                   FontWeight="SemiBold"
                                   Foreground="#555"
                                   HorizontalAlignment="Center" />
                        <TextBlock Text="等待后续开发"
                                   FontSize="14"
                                   Foreground="#888"
                                   HorizontalAlignment="Center" />
                        <TextBlock Text="自觉遵守 Demo 测试保密"
                                   FontSize="12"
                                   Foreground="#AAA"
                                   HorizontalAlignment="Center"
                                   Margin="0,8,0,0" />
                    </StackPanel>
                </Grid>

                <!-- ★ 工具（占位） -->
                <Grid x:Name="ToolsPanel" IsVisible="False" Opacity="1">
                    <Grid.Transitions>
                        <Transitions>
                            <DoubleTransition Property="Opacity" Duration="0:0:0.2" />
                        </Transitions>
                    </Grid.Transitions>
                    <StackPanel HorizontalAlignment="Center"
                                VerticalAlignment="Center"
                                Spacing="10">
                        <TextBlock Text="暂时无工具功能"
                                   FontSize="22"
                                   FontWeight="SemiBold"
                                   Foreground="#555"
                                   HorizontalAlignment="Center" />
                        <TextBlock Text="等待后续开发"
                                   FontSize="14"
                                   Foreground="#888"
                                   HorizontalAlignment="Center" />
                        <TextBlock Text="自觉遵守 Demo 测试保密"
                                   FontSize="12"
                                   Foreground="#AAA"
                                   HorizontalAlignment="Center"
                                   Margin="0,8,0,0" />
                    </StackPanel>
                </Grid>
            </Grid>

            <!-- ★ 底部导航条 -->
            <Grid x:Name="BottomNavBar"
                  Grid.Row="1"
                  Width="520" Height="52"
                  HorizontalAlignment="Center"
                  Margin="0,16,0,0">

                <Border Background="#D8D8D8" CornerRadius="26" />
                <Border Background="#C8C8C8" CornerRadius="25" Margin="1" />

                <Grid ColumnDefinitions="*,*,*,*,*">
                    <Button x:Name="DownloadTabButton"
                            Grid.Column="0"
                            Classes="TabBtn"
                            Background="Transparent"
                            BorderThickness="0"
                            Cursor="Hand"
                            HorizontalAlignment="Stretch"
                            HorizontalContentAlignment="Center"
                            VerticalContentAlignment="Center"
                            Padding="0">
                        <StackPanel Orientation="Vertical"
                                    Spacing="2"
                                    Background="Transparent"
                                    HorizontalAlignment="Center"
                                    VerticalAlignment="Center">
                            <Image Source="avares://Project.Launch/MainSrc/Views/imgs/Main/images/Buttons/Download.png"
                                   Width="22" Height="22" Stretch="Uniform" />
                            <TextBlock Text="下载"
                                       FontSize="10"
                                       Foreground="#444"
                                       HorizontalAlignment="Center" />
                        </StackPanel>
                    </Button>
                    <Button x:Name="LaunchTabButton"
                            Grid.Column="1"
                            Classes="TabBtn"
                            Background="Transparent"
                            BorderThickness="0"
                            Cursor="Hand"
                            HorizontalAlignment="Stretch"
                            HorizontalContentAlignment="Center"
                            VerticalContentAlignment="Center"
                            Padding="0">
                        <StackPanel Orientation="Vertical"
                                    Spacing="2"
                                    Background="Transparent"
                                    HorizontalAlignment="Center"
                                    VerticalAlignment="Center">
                            <Image Source="avares://Project.Launch/MainSrc/Views/imgs/Main/images/Buttons/Launch.png"
                                   Width="22" Height="22" Stretch="Uniform" />
                            <TextBlock Text="启动"
                                       FontSize="10"
                                       Foreground="#444"
                                       HorizontalAlignment="Center" />
                        </StackPanel>
                    </Button>
                    <Button x:Name="HomeTabButton"
                            Grid.Column="2"
                            Classes="TabBtn"
                            Background="Transparent"
                            BorderThickness="0"
                            Cursor="Hand"
                            HorizontalAlignment="Stretch"
                            HorizontalContentAlignment="Center"
                            VerticalContentAlignment="Center"
                            Padding="0">
                        <StackPanel Orientation="Vertical"
                                    Spacing="2"
                                    Background="Transparent"
                                    HorizontalAlignment="Center"
                                    VerticalAlignment="Center">
                            <Image Source="avares://Project.Launch/MainSrc/Views/imgs/Main/images/Buttons/Home.png"
                                   Width="22" Height="22" Stretch="Uniform" />
                            <TextBlock Text="主页"
                                       FontSize="10"
                                       Foreground="#444"
                                       HorizontalAlignment="Center" />
                        </StackPanel>
                    </Button>
                    <Button x:Name="SettingsTabButton"
                            Grid.Column="3"
                            Classes="TabBtn"
                            Background="Transparent"
                            BorderThickness="0"
                            Cursor="Hand"
                            HorizontalAlignment="Stretch"
                            HorizontalContentAlignment="Center"
                            VerticalContentAlignment="Center"
                            Padding="0">
                        <StackPanel Orientation="Vertical"
                                    Spacing="2"
                                    Background="Transparent"
                                    HorizontalAlignment="Center"
                                    VerticalAlignment="Center">
                            <Image Source="avares://Project.Launch/MainSrc/Views/imgs/Main/images/Buttons/Setting.png"
                                   Width="22" Height="22" Stretch="Uniform" />
                            <TextBlock Text="设置"
                                       FontSize="10"
                                       Foreground="#444"
                                       HorizontalAlignment="Center" />
                        </StackPanel>
                    </Button>
                    <Button x:Name="ToolsTabButton"
                            Grid.Column="4"
                            Classes="TabBtn"
                            Background="Transparent"
                            BorderThickness="0"
                            Cursor="Hand"
                            HorizontalAlignment="Stretch"
                            HorizontalContentAlignment="Center"
                            VerticalContentAlignment="Center"
                            Padding="0">
                        <StackPanel Orientation="Vertical"
                                    Spacing="2"
                                    Background="Transparent"
                                    HorizontalAlignment="Center"
                                    VerticalAlignment="Center">
                            <Image Source="avares://Project.Launch/MainSrc/Views/imgs/Main/images/Buttons/Tool.png"
                                   Width="22" Height="22" Stretch="Uniform" />
                            <TextBlock Text="工具"
                                       FontSize="10"
                                       Foreground="#444"
                                       HorizontalAlignment="Center" />
                        </StackPanel>
                    </Button>
                </Grid>

                <lg:LiquidGlassSurface x:Name="TabSlider"
                                       Width="96"
                                       Height="44"
                                       CornerRadius="22"
                                       VerticalAlignment="Center"
                                       HorizontalAlignment="Left"
                                       Margin="4,0,4,0"
                                       RenderTransformOrigin="0.5,0.5"
                                       RefractionHeight="4"
                                       RefractionAmount="40"
                                       BlurRadius="0"
                                       Vibrancy="0.86"
                                       Brightness="0"
                                       Contrast="1.0"
                                       ExposureEv="0"
                                       GammaPower="1.0"
                                       BackdropOpacity="1.0"
                                       ChromaticAberration="True"
                                       DepthEffect="False"
                                       HighlightEnabled="False"
                                       HighlightOpacity="0"
                                       ShadowEnabled="True"
                                       ShadowRadius="20"
                                       ShadowOffset="0,3"
                                       ShadowColor="#1A000000">
                    <lg:LiquidGlassSurface.RenderTransform>
                        <TransformGroup>
                            <ScaleTransform ScaleX="1" ScaleY="1" />
                            <TranslateTransform X="208" Y="0" />
                        </TransformGroup>
                    </lg:LiquidGlassSurface.RenderTransform>
                </lg:LiquidGlassSurface>
            </Grid>
        </Grid>

        <!-- Toast -->
        <Border x:Name="CancelToast"
                Grid.Row="1"
                IsVisible="False"
                Opacity="0"
                Background="#F2FFFFFF"
                CornerRadius="14"
                BorderBrush="#33FFFFFF"
                BorderThickness="1"
                Padding="20,16"
                MinWidth="360"
                HorizontalAlignment="Right"
                VerticalAlignment="Bottom"
                Margin="0,0,24,24">
            <Border.Effect>
                <DropShadowEffect BlurRadius="24"
                                  OffsetX="0"
                                  OffsetY="8"
                                  Color="#33000000" />
            </Border.Effect>
            <Border.Transitions>
                <Transitions>
                    <DoubleTransition Property="Opacity" Duration="0:0:0.3">
                        <DoubleTransition.Easing>
                            <CubicEaseInOut />
                        </DoubleTransition.Easing>
                    </DoubleTransition>
                </Transitions>
            </Border.Transitions>

            <StackPanel Orientation="Horizontal"
                        VerticalAlignment="Center"
                        Spacing="14">
                <Image Source="avares://Project.Launch/MainSrc/Views/imgs/Main/images/Buttons/warning.png"
                       Width="28"
                       Height="28"
                       Stretch="Uniform"
                       VerticalAlignment="Center" />
                <TextBlock x:Name="ToastText"
                           Text="用户取消登录"
                           FontSize="16"
                           FontWeight="SemiBold"
                           Foreground="#333"
                           VerticalAlignment="Center" />
            </StackPanel>
        </Border>
    </Grid>
</Window>
```

### `MainSrc\Views\MainWindow.cs`

```csharp
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using LiquidGlassAvaloniaUI;
using Project.Launch.Tools;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using Project.Launch.Download;

using Project.Launch;
using Project.Launch.Auth;
using Project.Launch.Tools.AnimSettings;
using Project.Launch.Tools.JavaConfig;
using Project.Launch.Tools.LogsConfig;
using Project.Launch.Tools.Player3DConfig;
using Project.Launch.Tools.SoundControl;
namespace Project.Launch.Views
{
    public partial class MainWindow : Window
    {
        private readonly Color _defaultStartColor = Color.Parse("#CCFF6B9D");
        private readonly Color _defaultEndColor = Color.Parse("#CCB366FF");
        private readonly HttpClient _httpClient = new HttpClient();

        private InstanceInfo? _currentInstance;
        private Bitmap? _fullSkinBitmap;

        private Button? _homeTab, _launchTab, _downloadTab, _settingsTab, _toolsTab;
        private LiquidGlassSurface? _tabSlider;
        private TranslateTransform? _tabSliderTransform;
        private ScaleTransform? _tabSliderScale;
        private int _selectedTab = 2;
        private DispatcherTimer? _tabAnimTimer;
        private double _tabAnimStartX, _tabAnimTargetX;
        private DateTime _tabAnimStartTime;

        // 下载页：基岩版 / Java版 选择器
        private Button? _bedrockEditionBtn, _javaEditionBtn;
        private StackPanel? _bedrockPanel;
        private Grid? _javaPanel;
        private LiquidGlassSurface? _editionSlider;
        private TranslateTransform? _editionSliderTransform;
        private ScaleTransform? _editionSliderScale;
        private int _selectedEdition = 0;
        private DispatcherTimer? _editionAnimTimer;
        private double _editionAnimStartX, _editionAnimTargetX;
        private DateTime _editionAnimStartTime;

        // Java 版版本列表
        private List<VersionEntry>? _allVersions;
        private bool _isVersionsLoading = false;
        private VersionEntry? _selectedVersion;

        public MainWindow()
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            LogHelper.Reset(LauncherPaths.MainWindowLog,
                "Kilim 启动器日志", "Kilim Launcher Log");

            Log("程序启动", "Program started");

            InitializeComponent();

            Log("InitializeComponent 完成", "InitializeComponent completed");

            var closeButton = this.FindControl<Button>("CloseButton");
            if (closeButton != null)
                closeButton.Click += (s, e) => this.Close();

            var minimizeButton = this.FindControl<Button>("MinimizeButton");
            if (minimizeButton != null)
                minimizeButton.Click += (s, e) => this.WindowState = WindowState.Minimized;

            var launchGameButton = this.FindControl<Button>("LaunchGameButton");
            if (launchGameButton != null)
                launchGameButton.Click += OnMainButtonClicked;

            var expandButton = this.FindControl<Button>("ExpandButton");
            if (expandButton != null)
                expandButton.Click += OnExpandClicked;

            var reLoginButton = this.FindControl<Button>("ReLoginButton");
            if (reLoginButton != null)
                reLoginButton.Click += OnReLoginClicked;

            InitializeTabSelector();
            InitializeEditionSelector();
            InitializeVersionList();

            Log("按钮事件绑定完成", "Button events bound");

            _currentInstance = InstanceService.GetSelectedInstance();
            if (_currentInstance != null)
            {
                Log($"已加载实例: {_currentInstance.VersionId} @ {_currentInstance.RootPath}",
                    $"Loaded instance: {_currentInstance.VersionId} @ {_currentInstance.RootPath}");
            }

            if (!App.LoginCheck)
            {
                UpdateTitleBarGradient(_defaultStartColor, _defaultEndColor);
                Log("初始化完成（未登录，显示渐变标题栏）", "Initialization complete (not logged in, gradient title bar)");

                Opened += async (s, e) =>
                {
                    try
                    {
                        await ShowLoginDialog();
                        await SetTransparentTitleBarAsync();
                        UpdateLoginStatus();
                        await RefreshAvatarAsync();
                        Log("登录窗口已关闭，切换为透明标题栏", "Login window closed, switched to transparent title bar");
                    }
                    catch (Exception ex)
                    {
                        Log($"弹登录窗异常: {ex.Message}", $"ShowLoginDialog exception: {ex.Message}");
                    }
                };
            }
            else
            {
                Opened += async (s, e) =>
                {
                    try
                    {
                        await SetTransparentTitleBarAsync(animate: false);
                        UpdateLoginStatus();
                        await RefreshAvatarAsync();
                        Log("初始化完成（已登录，显示透明标题栏）", "Initialization complete (logged in, transparent title bar)");
                    }
                    catch (Exception ex)
                    {
                        Log($"初始化异常: {ex.Message}", $"Init exception: {ex.Message}");
                    }
                };
            }
        }

        private void InitializeTabSelector()
        {
            _homeTab = this.FindControl<Button>("HomeTabButton");
            _launchTab = this.FindControl<Button>("LaunchTabButton");
            _downloadTab = this.FindControl<Button>("DownloadTabButton");
            _settingsTab = this.FindControl<Button>("SettingsTabButton");
            _toolsTab = this.FindControl<Button>("ToolsTabButton");

            _tabSlider = this.FindControl<LiquidGlassSurface>("TabSlider");
            if (_tabSlider != null)
            {
                var group = _tabSlider.RenderTransform as TransformGroup;
                if (group != null)
                {
                    _tabSliderTransform = group.Children.OfType<TranslateTransform>().FirstOrDefault();
                    _tabSliderScale = group.Children.OfType<ScaleTransform>().FirstOrDefault();
                }

                if (_tabSliderTransform != null)
                {
                    _tabSliderTransform.X = _selectedTab * AnimationConfig.TabStep;
                    _tabSliderTransform.Y = 0;
                }
                if (_tabSliderScale != null)
                {
                    _tabSliderScale.ScaleX = 1.0;
                    _tabSliderScale.ScaleY = 1.0;
                }
            }

            if (_downloadTab != null) _downloadTab.Click += (s, e) => SelectTab(0);
            if (_launchTab != null) _launchTab.Click += (s, e) => SelectTab(1);
            if (_homeTab != null) _homeTab.Click += (s, e) => SelectTab(2);
            if (_settingsTab != null) _settingsTab.Click += (s, e) => SelectTab(3);
            if (_toolsTab != null) _toolsTab.Click += (s, e) => SelectTab(4);

            UpdateTabVisuals(_selectedTab);
        }

        private void SelectTab(int index)
        {
            if (index == _selectedTab) return;
            int oldIndex = _selectedTab;
            _selectedTab = index;

            AnimateTabSliderTo(index);
            UpdateTabVisuals(index);
            _ = SwitchPanelAsync(oldIndex, index);
        }

        private void UpdateTabVisuals(int index)
        {
            var tabs = new[] { _downloadTab, _launchTab, _homeTab, _settingsTab, _toolsTab };
            for (int i = 0; i < tabs.Length; i++)
            {
                if (tabs[i] != null)
                {
                    tabs[i]!.Foreground = (i == index)
                        ? new SolidColorBrush(Colors.Black)
                        : new SolidColorBrush(Color.Parse("#666666"));
                    tabs[i]!.IsHitTestVisible = (i != index);
                }
            }
        }

        private async Task SwitchPanelAsync(int oldIndex, int newIndex)
        {
            var panels = new Grid?[]
            {
                this.FindControl<Grid>("DownloadPanel"),
                this.FindControl<Grid>("LaunchPanel"),
                this.FindControl<Grid>("HomePanel"),
                this.FindControl<Grid>("SettingsPanel"),
                this.FindControl<Grid>("ToolsPanel")
            };

            var oldPanel = (oldIndex >= 0 && oldIndex < panels.Length) ? panels[oldIndex] : null;
            if (oldPanel != null && oldPanel.IsVisible)
            {
                oldPanel.Opacity = 0;
                await Task.Delay(AnimationConfig.TabPanelFadeDuration);
                oldPanel.IsVisible = false;
            }

            var newPanel = (newIndex >= 0 && newIndex < panels.Length) ? panels[newIndex] : null;
            if (newPanel != null)
            {
                newPanel.Opacity = 0;
                newPanel.IsVisible = true;
                await Task.Delay(20);
                newPanel.Opacity = 1;
            }
        }

        private void AnimateTabSliderTo(int index)
        {
            if (_tabSliderTransform == null || _tabSlider == null) return;

            double targetX = index * AnimationConfig.TabStep;
            double startX = _tabSliderTransform.X;

            _tabAnimTimer?.Stop();

            _tabAnimStartX = startX;
            _tabAnimTargetX = targetX;
            _tabAnimStartTime = DateTime.Now;

            double halfW = AnimationConfig.TabWidth / 2.0;
            double halfH = AnimationConfig.TabHeight / 2.0;

            _tabAnimTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            _tabAnimTimer.Tick += (s, e) =>
            {
                var elapsed = DateTime.Now - _tabAnimStartTime;
                double progress = elapsed.TotalMilliseconds / AnimationConfig.TabMoveDuration.TotalMilliseconds;
                if (progress >= 1.0)
                {
                    progress = 1.0;
                    _tabAnimTimer.Stop();
                }

                double eased = progress < 0.5 ? 4 * progress * progress * progress : 1 - Math.Pow(-2 * progress + 2, 3) / 2;
                double baseX = _tabAnimStartX + (_tabAnimTargetX - _tabAnimStartX) * eased;

                double scaleCurve;
                if (progress <= AnimationConfig.TabScaleRiseEnd)
                {
                    double t = progress / AnimationConfig.TabScaleRiseEnd;
                    scaleCurve = t * t * (3 - 2 * t);
                }
                else if (progress <= AnimationConfig.TabScaleFallStart)
                {
                    scaleCurve = 1.0;
                }
                else
                {
                    double t = (progress - AnimationConfig.TabScaleFallStart) / (1.0 - AnimationConfig.TabScaleFallStart);
                    scaleCurve = 1.0 - t * t * (3 - 2 * t);
                }
                double currentScale = 1.0 + (AnimationConfig.TabPeakScale - 1.0) * scaleCurve;

                double blurCurve = 1.0 - Math.Abs(progress * 2.0 - 1.0);
                _tabSlider.BlurRadius = AnimationConfig.TabBlurPeak * blurCurve;

                double xComp = -(currentScale - 1.0) * halfW;
                double yComp = -(currentScale - 1.0) * halfH;

                _tabSliderTransform.X = baseX + xComp;
                _tabSliderTransform.Y = yComp;

                if (_tabSliderScale != null)
                {
                    _tabSliderScale.ScaleX = currentScale;
                    _tabSliderScale.ScaleY = currentScale;
                }

                if (progress >= 1.0)
                {
                    _tabSliderTransform.X = _tabAnimTargetX;
                    _tabSliderTransform.Y = 0;
                    if (_tabSliderScale != null)
                    {
                        _tabSliderScale.ScaleX = 1.0;
                        _tabSliderScale.ScaleY = 1.0;
                    }
                    _tabSlider.BlurRadius = 0;
                }
            };
            _tabAnimTimer.Start();
        }

        private void InitializeEditionSelector()
        {
            _bedrockEditionBtn = this.FindControl<Button>("BedrockEditionButton");
            _javaEditionBtn = this.FindControl<Button>("JavaEditionButton");
            _bedrockPanel = this.FindControl<StackPanel>("BedrockPanel");
            _javaPanel = this.FindControl<Grid>("JavaPanel");

            _editionSlider = this.FindControl<LiquidGlassSurface>("EditionSlider");
            if (_editionSlider != null)
            {
                var group = _editionSlider.RenderTransform as TransformGroup;
                if (group != null)
                {
                    _editionSliderTransform = group.Children.OfType<TranslateTransform>().FirstOrDefault();
                    _editionSliderScale = group.Children.OfType<ScaleTransform>().FirstOrDefault();
                }

                if (_editionSliderTransform != null)
                {
                    _editionSliderTransform.X = 0;
                    _editionSliderTransform.Y = 0;
                }
                if (_editionSliderScale != null)
                {
                    _editionSliderScale.ScaleX = 1.0;
                    _editionSliderScale.ScaleY = 1.0;
                }
            }

            if (_bedrockEditionBtn != null) _bedrockEditionBtn.Click += (s, e) => SelectEdition(0);
            if (_javaEditionBtn != null) _javaEditionBtn.Click += (s, e) => SelectEdition(1);

            UpdateEditionVisuals(0);
        }

        private void SelectEdition(int index)
        {
            if (index == _selectedEdition) return;
            _selectedEdition = index;
            AnimateEditionSliderTo(index);
            UpdateEditionVisuals(index);
        }

        private void UpdateEditionVisuals(int index)
        {
            var buttons = new[] { _bedrockEditionBtn, _javaEditionBtn };
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] != null)
                {
                    buttons[i]!.Foreground = (i == index)
                        ? new SolidColorBrush(Colors.Black)
                        : new SolidColorBrush(Color.Parse("#666666"));
                    buttons[i]!.IsHitTestVisible = (i != index);
                }
            }

            if (_bedrockPanel != null) _bedrockPanel.IsVisible = (index == 0);
            if (_javaPanel != null) _javaPanel.IsVisible = (index == 1);
        }

        private void AnimateEditionSliderTo(int index)
        {
            if (_editionSliderTransform == null || _editionSlider == null) return;

            double targetX = index * 120;
            double startX = _editionSliderTransform.X;

            _editionAnimTimer?.Stop();

            _editionAnimStartX = startX;
            _editionAnimTargetX = targetX;
            _editionAnimStartTime = DateTime.Now;

            TimeSpan duration = AnimationConfig.SliderMoveDuration;
            double peakScale = AnimationConfig.SliderPeakScale;
            double riseEnd = AnimationConfig.SliderScaleRiseEnd;
            double fallStart = AnimationConfig.SliderScaleFallStart;
            double blurPeak = AnimationConfig.SliderBlurPeak;
            double halfW = AnimationConfig.SliderWidth / 2.0;
            double halfH = AnimationConfig.SliderHeight / 2.0;

            _editionAnimTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            _editionAnimTimer.Tick += (s, e) =>
            {
                var elapsed = DateTime.Now - _editionAnimStartTime;
                double progress = elapsed.TotalMilliseconds / duration.TotalMilliseconds;
                if (progress >= 1.0)
                {
                    progress = 1.0;
                    _editionAnimTimer.Stop();
                }

                double eased = progress < 0.5 ? 4 * progress * progress * progress : 1 - Math.Pow(-2 * progress + 2, 3) / 2;
                double baseX = _editionAnimStartX + (_editionAnimTargetX - _editionAnimStartX) * eased;

                double scaleCurve;
                if (progress <= riseEnd)
                {
                    double t = progress / riseEnd;
                    scaleCurve = t * t * (3 - 2 * t);
                }
                else if (progress <= fallStart)
                {
                    scaleCurve = 1.0;
                }
                else
                {
                    double t = (progress - fallStart) / (1.0 - fallStart);
                    scaleCurve = 1.0 - t * t * (3 - 2 * t);
                }
                double currentScale = 1.0 + (peakScale - 1.0) * scaleCurve;

                double blurCurve = 1.0 - Math.Abs(progress * 2.0 - 1.0);
                _editionSlider.BlurRadius = blurPeak * blurCurve;

                double xComp = -(currentScale - 1.0) * halfW;
                double yComp = -(currentScale - 1.0) * halfH;

                _editionSliderTransform.X = baseX + xComp;
                _editionSliderTransform.Y = yComp;

                if (_editionSliderScale != null)
                {
                    _editionSliderScale.ScaleX = currentScale;
                    _editionSliderScale.ScaleY = currentScale;
                }

                if (progress >= 1.0)
                {
                    _editionSliderTransform.X = _editionAnimTargetX;
                    _editionSliderTransform.Y = 0;
                    if (_editionSliderScale != null)
                    {
                        _editionSliderScale.ScaleX = 1.0;
                        _editionSliderScale.ScaleY = 1.0;
                    }
                    _editionSlider.BlurRadius = 0;
                }
            };
            _editionAnimTimer.Start();
        }

        private void InitializeVersionList()
        {
            _ = LoadVersionManifestAsync();
        }

        private async Task LoadVersionManifestAsync()
        {
            if (_isVersionsLoading) return;
            _isVersionsLoading = true;
            
            var loadingPanel = this.FindControl<StackPanel>("VersionLoadingPanel");
            var errorPanel = this.FindControl<StackPanel>("VersionErrorPanel");
            var listBox = this.FindControl<ListBox>("VersionListBox");
            
            if (loadingPanel != null) loadingPanel.IsVisible = true;
            if (errorPanel != null) errorPanel.IsVisible = false;
            if (listBox != null) listBox.IsVisible = false;

            var manifest = await VersionService.GetVersionManifestAsync();
            
            if (manifest != null && manifest.Versions.Count > 0)
            {
                _allVersions = manifest.Versions.OrderByDescending(v => v.ReleaseTime).ToList();
                
                foreach (var version in _allVersions)
                {
                    version.IconPath = GetVersionIcon(version);
                    try
                    {
                        using var stream = AssetLoader.Open(new Uri(version.IconPath));
                        version.IconBitmap = new Bitmap(stream);
                    }
                    catch (Exception ex)
                    {
                        Log($"加载图标失败: {version.IconPath} - {ex.Message}", $"Failed to load icon: {version.IconPath} - {ex.Message}");
                    }
                }

                ApplyVersionFilter();
                
                if (loadingPanel != null) loadingPanel.IsVisible = false;
                if (listBox != null) listBox.IsVisible = true;
            }
            else
            {
                if (loadingPanel != null) loadingPanel.IsVisible = false;
                if (errorPanel != null) errorPanel.IsVisible = true;
            }
            
            _isVersionsLoading = false;
        }

        private string GetVersionIcon(VersionEntry version)
        {
            const string iconBase = "avares://Project.Launch/MainSrc/Views/imgs/Main/images/Blocks/";
            
            var aprilFoolsIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "15w14a", "1.rv-pre1", "3d shareware v1.34", "20w14infinite",
                "22w13oneblockatatime", "23w13a_or_b", "24w14potato", "25w14craftmine"
            };

            if (aprilFoolsIds.Contains(version.VersionId) || version.VersionId.Contains("potato"))
            {
                return iconBase + "chest.png";
            }

            if (version.Type.Equals("release", StringComparison.OrdinalIgnoreCase))
            {
                return iconBase + "grass2.png";
            }

            if (version.Type.Equals("snapshot", StringComparison.OrdinalIgnoreCase))
            {
                return iconBase + "GrassPath.png";
            }
            
            return iconBase + "furnace.png";
        }

        private void ApplyVersionFilter()
        {
            if (_allVersions == null) return;
            
            var listBox = this.FindControl<ListBox>("VersionListBox");
            var filterCombo = this.FindControl<ComboBox>("VersionTypeFilter");
            var searchBox = this.FindControl<TextBox>("VersionSearchBox");
            
            if (listBox == null || filterCombo == null || searchBox == null) return;
            
            string filter = (filterCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "all";
            string search = searchBox.Text?.Trim().ToLowerInvariant() ?? "";
            
            var filtered = _allVersions.Where(v => 
            {
                if (filter != "all" && !v.Type.Equals(filter, StringComparison.OrdinalIgnoreCase)) 
                    return false;
                if (!string.IsNullOrEmpty(search) && !v.VersionId.ToLowerInvariant().Contains(search)) 
                    return false;
                return true;
            }).ToList();
            
            listBox.ItemsSource = filtered;
        }

        private void OnVersionFilterChanged(object? sender, SelectionChangedEventArgs e) => ApplyVersionFilter();
        private void OnVersionSearchChanged(object? sender, TextChangedEventArgs e) => ApplyVersionFilter();
        private void OnRetryVersionsClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => _ = LoadVersionManifestAsync();

        private void OnVersionSelected(object? sender, SelectionChangedEventArgs e)
        {
            var listBox = this.FindControl<ListBox>("VersionListBox");
            _selectedVersion = listBox?.SelectedItem as VersionEntry;
            
            if (_selectedVersion != null)
            {
                Log($"用户选中了版本: {_selectedVersion.VersionId}", $"User selected version: {_selectedVersion.VersionId}");
            }
        }

        private async void OnVersionListDoubleTapped(object? sender, TappedEventArgs e)
        {
            if (_selectedVersion == null) return;

            // 1. 选择安装目录（选择器本身可能抛异常，必须兜住，否则 async void 会把整个进程带崩）
            string? targetPath = await PickFolderSafeAsync(
                $"选择 {_selectedVersion.VersionId} 的安装目录（即 .minecraft 文件夹）");

            if (string.IsNullOrEmpty(targetPath)) return;

            // 2. 准备后台下载 CTS
            var cts = new System.Threading.CancellationTokenSource();

            // 3. 弹窗接管 UI（逻辑类似 LoginWindow 模态窗）
            UpdateTitleBarGradient(_defaultStartColor, _defaultEndColor);
            var contentArea = this.FindControl<Grid>("ContentArea");
            if (contentArea != null) contentArea.IsVisible = false;

            bool success = false;
            InstallResult? installResult = null;
            try
            {
                var downloadWindow = new DownloadWindow(_selectedVersion, targetPath, cts);
                success = await downloadWindow.ShowDialog<bool>(this);
                installResult = downloadWindow.LastResult;
            }
            catch (Exception ex)
            {
                Log($"下载窗异常: {ex.Message}", $"Download window error: {ex.Message}");
            }
            finally
            {
                if (contentArea != null) contentArea.IsVisible = true;
                await SetTransparentTitleBarAsync();
            }

            // 4. 根据结果处理
            if (success)
            {
                await ShowToastAsync($"安装 {_selectedVersion.VersionId} 成功！", "DownloadDone");
                var result = MinecraftDirectoryAnalyzer.Analyze(targetPath);
                if (result.IsValid)
                {
                    var instance = new InstanceInfo
                    {
                        Name = _selectedVersion.VersionId,
                        RootPath = targetPath,
                        Layout = result.Layout,
                        VersionId = _selectedVersion.VersionId,
                        ImportedAt = DateTime.UtcNow,
                        LastUsed = DateTime.UtcNow
                    };
                    InstanceService.SetSelectedInstance(instance);
                    _currentInstance = instance;
                    UpdateLoginStatus();
                }
            }
            else
            {
                if (!cts.IsCancellationRequested)
                {
                    string msg = installResult != null && installResult.FailedFiles > 0
                        ? $"安装未完成：失败 {installResult.FailedFiles} / {installResult.TotalFiles} 个文件"
                        : "下载失败，请查看日志";
                    await ShowToastAsync(msg, "Errors");
                }
            }
        }

        private async void OnMainButtonClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (_currentInstance == null) await ImportInstanceAsync();
            else await StartGameAsync();
        }

        private async Task StartGameAsync()
        {
            if (_currentInstance == null) return;

            try
            {
                string versionsDir = Path.Combine(_currentInstance.RootPath, "versions");
                string jsonPath = Path.Combine(versionsDir, _currentInstance.VersionId, _currentInstance.VersionId + ".json");
                if (!File.Exists(jsonPath))
                {
                    Log($"版本 json 不存在: {jsonPath}", $"Version json not found: {jsonPath}");
                    await ShowToastAsync("版本文件缺失", "Messages");
                    return;
                }

                var vj = MinecraftVersionJson.Load(jsonPath);
                if (vj == null)
                {
                    await ShowToastAsync("版本解析失败", "Messages");
                    return;
                }

                int requiredJava = vj.JavaVersion?.MajorVersion ?? 8;
                Log($"版本要求 Java {requiredJava}", $"Version requires Java {requiredJava}");

                var javas = await JavaDetector.DetectAllAsync();
                Log($"检测到 {javas.Count} 个 Java", $"Detected {javas.Count} Java(s)");
                foreach (var j in javas)
                    Log($"  Java: {j.Path} (major={j.MajorVersion})", $"  Java: {j.Path} (major={j.MajorVersion})");

                var matched = JavaDetector.Match(javas, requiredJava);
                if (matched == null)
                {
                    Log($"未找到合适的 Java（需要 {requiredJava}）", $"No suitable Java (need {requiredJava})");
                    await ShowToastAsync($"Java 环境异常：需要 Java {requiredJava}", "Messages");
                    return;
                }

                Log($"匹配到 Java: {matched.Path}", $"Matched Java: {matched.Path}");

                _currentInstance.JavaPath = matched.Path;
                InstanceService.SetSelectedInstance(_currentInstance);

                string playerName = App.PlayerName ?? "Steve";
                string playerUuid = App.OfflineUuid ?? OfflineUuidGenerator.GenerateUuidString(playerName).Replace("-", "");

                await ShowToastAsync("正在启动游戏...", "Messages");

                bool ok = await GameLauncher.LaunchAsync(_currentInstance, vj, matched, playerName, playerUuid);

                if (ok) await ShowToastAsync("游戏已启动", "Messages");
                else await ShowToastAsync("启动失败，查看日志", "Messages");
            }
            catch (Exception ex)
            {
                Log($"启动游戏异常: {ex.Message}", $"Start game exception: {ex.Message}");
                await ShowToastAsync("启动失败", "Messages");
            }
        }

        private async void OnExpandClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            await ImportInstanceAsync();
        }

        private async Task ImportInstanceAsync()
        {
            try
            {
                string? path = await PickFolderSafeAsync("选择 Minecraft 目录（.minecraft 根目录或隔离实例目录）");
                if (string.IsNullOrEmpty(path))
                {
                    Log("用户取消导入", "User cancelled import");
                    return;
                }

                var result = MinecraftDirectoryAnalyzer.Analyze(path);
                if (!result.IsValid)
                {
                    Log("目录无效（未找到 versions/ 或 .minecraft/versions/）", "Invalid directory (no versions/ or .minecraft/versions/)");
                    await ShowToastAsync("未找到有效的 Minecraft 目录", "Messages");
                    return;
                }

                Log($"分析结果: Layout={result.Layout}, Versions={string.Join(",", result.Versions)}", $"Analysis: Layout={result.Layout}, Versions={string.Join(",", result.Versions)}");

                string versionId = result.Versions[result.Versions.Count - 1];

                var instance = new InstanceInfo
                {
                    Name = versionId,
                    RootPath = result.RootPath,
                    Layout = result.Layout,
                    VersionId = versionId,
                    ImportedAt = DateTime.UtcNow,
                    LastUsed = DateTime.UtcNow
                };

                InstanceService.SetSelectedInstance(instance);
                _currentInstance = instance;

                Log($"实例导入成功: {versionId}", $"Instance imported: {versionId}");

                UpdateLoginStatus();
                await ShowToastAsync($"已导入 {versionId}", "Messages");
            }
            catch (Exception ex)
            {
                Log($"导入实例异常: {ex.Message}", $"Import instance exception: {ex.Message}");
                await ShowToastAsync("导入失败", "Messages");
            }
        }

        private async void OnReLoginClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            try
            {
                UpdateTitleBarGradient(_defaultStartColor, _defaultEndColor);
                await ShowLoginDialog();
                UpdateLoginStatus();
                await RefreshAvatarAsync();
                await SetTransparentTitleBarAsync();
            }
            catch (Exception ex)
            {
                Log($"重新登录异常: {ex.Message}", $"ReLogin exception: {ex.Message}");
            }
        }

        private async void OnSwitchAccountClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            await ShowAccountManagerAsync();
        }

        private async Task ShowAccountManagerAsync()
        {
            UpdateTitleBarGradient(_defaultStartColor, _defaultEndColor);
            var contentArea = this.FindControl<Grid>("ContentArea");
            if (contentArea != null) contentArea.IsVisible = false;

            await Task.Delay(150);

            try
            {
                var win = new AccountManagerWindow();
                await win.ShowDialog(this);
                if (win.ShouldOpenLogin) await RunLoginWindowAsync();

                UpdateLoginStatus();
                await RefreshAvatarAsync();
            }
            finally
            {
                if (contentArea != null) contentArea.IsVisible = true;
                await SetTransparentTitleBarAsync();
            }
        }

        private async Task RunLoginWindowAsync()
        {
            var loginWindow = new LoginWindow();
            await loginWindow.ShowDialog(this);

            UpdateLoginStatus();

            if (App.IsLoggedIn && loginWindow.FaceBitmap != null && loginWindow.HatBitmap != null)
            {
                var faceImg = this.FindControl<Image>("HomeFaceImage");
                var hatImg = this.FindControl<Image>("HomeHatImage");
                if (faceImg != null)
                {
                    faceImg.Source = loginWindow.FaceBitmap;
                    RenderOptions.SetBitmapInterpolationMode(faceImg, BitmapInterpolationMode.None);
                }
                if (hatImg != null)
                {
                    hatImg.Source = loginWindow.HatBitmap;
                    RenderOptions.SetBitmapInterpolationMode(hatImg, BitmapInterpolationMode.None);
                }
            }

            if (!App.IsLoggedIn) _ = ShowCancelToastAsync();
        }

        private async Task ShowLoginDialog()
        {
            var contentArea = this.FindControl<Grid>("ContentArea");
            if (contentArea != null) contentArea.IsVisible = false;

            await Task.Delay(150);

            try
            {
                await RunLoginWindowAsync();
            }
            finally
            {
                if (contentArea != null) contentArea.IsVisible = true;
            }
        }

        private async Task SetTransparentTitleBarAsync(bool animate = true)
        {
            var titleBar = this.FindControl<Border>("TitleBarBorder");
            var titleText = this.FindControl<TextBlock>("TitleText");
            var minImg = this.FindControl<Image>("MinimizeImage");
            var closeImg = this.FindControl<Image>("CloseImage");

            if (!animate)
            {
                if (titleBar != null) titleBar.Background = Brushes.Transparent;
                if (titleText != null) titleText.Foreground = new SolidColorBrush(Colors.Black);
                SetBlackIcons(minImg, closeImg);
                return;
            }

            if (minImg != null) minImg.Opacity = 0;
            if (closeImg != null) closeImg.Opacity = 0;

            if (titleBar != null) titleBar.Background = Brushes.Transparent;
            if (titleText != null) titleText.Foreground = new SolidColorBrush(Colors.Black);

            await Task.Delay(200);

            SetBlackIcons(minImg, closeImg);

            if (minImg != null) minImg.Opacity = 1;
            if (closeImg != null) closeImg.Opacity = 1;
        }

        private void SetBlackIcons(Image? minImg, Image? closeImg)
        {
            try
            {
                if (minImg != null)
                {
                    var uri = new Uri("avares://Project.Launch/MainSrc/Views/imgs/Main/images/Buttons/black-.png");
                    using var stream = AssetLoader.Open(uri);
                    minImg.Source = new Bitmap(stream);
                }
                if (closeImg != null)
                {
                    var uri = new Uri("avares://Project.Launch/MainSrc/Views/imgs/Main/images/Buttons/blackx.png");
                    using var stream = AssetLoader.Open(uri);
                    closeImg.Source = new Bitmap(stream);
                }
            }
            catch { }
        }

        private void UpdateLoginStatus()
        {
            var homeNotLoggedIn = this.FindControl<StackPanel>("HomeNotLoggedIn");
            var homeLoggedIn = this.FindControl<StackPanel>("HomeLoggedIn");
            var homePlayerName = this.FindControl<TextBlock>("HomePlayerName");
            var launchBtn = this.FindControl<Button>("LaunchGameButton");
            var launchImg = this.FindControl<Image>("LaunchGameImage");
            var launchTxt = this.FindControl<TextBlock>("LaunchGameText");
            var expandBtn = this.FindControl<Button>("ExpandButton");
            var expandImg = this.FindControl<Image>("ExpandImage");
            var reLoginBtn = this.FindControl<Button>("ReLoginButton");
            var reLoginContainer = this.FindControl<Grid>("ReLoginContainer");

            var launchNotLoggedIn = this.FindControl<StackPanel>("LaunchNotLoggedIn");
            var skinViewer = this.FindControl<Pivot3DControl>("SkinViewer");
            var launchButtonsPanel = this.FindControl<StackPanel>("LaunchButtonsPanel");

            bool loggedIn = App.IsLoggedIn && !string.IsNullOrEmpty(App.PlayerName);
            bool hasInstance = _currentInstance != null;

            if (homeNotLoggedIn != null) homeNotLoggedIn.IsVisible = !loggedIn;
            if (homeLoggedIn != null) homeLoggedIn.IsVisible = loggedIn;
            if (homePlayerName != null) homePlayerName.Text = App.PlayerName ?? "Steve";

            if (launchNotLoggedIn != null) launchNotLoggedIn.IsVisible = !loggedIn;
            if (skinViewer != null) skinViewer.IsVisible = loggedIn;
            if (launchButtonsPanel != null) launchButtonsPanel.IsVisible = loggedIn;

            try
            {
                string mainIcon = hasInstance ? "button-Start1.png" : "button-Start3.png";
                string subIcon = hasInstance ? "button-Start2.png" : "button-Start4.png";

                if (launchImg != null)
                {
                    using var stream = AssetLoader.Open(new Uri($"avares://Project.Launch/MainSrc/Views/imgs/Main/images/Buttons/{mainIcon}"));
                    launchImg.Source = new Bitmap(stream);
                }
                if (launchTxt != null) launchTxt.Text = hasInstance ? "开始游戏" : "导入实例";

                if (expandImg != null)
                {
                    using var stream = AssetLoader.Open(new Uri($"avares://Project.Launch/MainSrc/Views/imgs/Main/images/Buttons/{subIcon}"));
                    expandImg.Source = new Bitmap(stream);
                }
            }
            catch (Exception ex)
            {
                Log($"更新按钮图像失败: {ex.Message}", $"Update button icon failed: {ex.Message}");
            }

            bool showMainBtn = loggedIn;
            bool showReLogin = !loggedIn;

            if (launchBtn != null) launchBtn.IsVisible = showMainBtn;
            if (expandBtn != null) expandBtn.IsVisible = showMainBtn;
            if (reLoginBtn != null) reLoginBtn.IsVisible = showReLogin;
            if (reLoginContainer != null) reLoginContainer.IsVisible = showReLogin;
        }

        private async Task RefreshAvatarAsync()
        {
            if (!App.IsLoggedIn) { Log("未登录，跳过皮肤加载", "Not logged in, skip skin loading"); return; }

            var account = AccountService.GetSelectedAccount();
            Bitmap? skinBitmap = null;

            if (account != null && account.Type == "microsoft" && !string.IsNullOrEmpty(account.SkinUrl))
            {
                try
                {
                    Log($"正在下载正版皮肤: {account.SkinUrl}", $"Downloading official skin: {account.SkinUrl}");
                    byte[] bytes = await _httpClient.GetByteArrayAsync(account.SkinUrl);
                    using var ms = new MemoryStream(bytes);
                    skinBitmap = new Bitmap(ms);
                    Log("正版皮肤下载成功", "Official skin downloaded successfully");
                }
                catch (Exception ex)
                {
                    Log($"正版皮肤下载失败，将回退到默认皮肤: {ex.Message}", $"Skin download failed, falling back to default: {ex.Message}");
                }
            }

            if (skinBitmap == null)
            {
                string skinAssetPath = DefaultSkinSelector.GetSkinAssetPath(account?.Uuid ?? App.OfflineUuid);
                Log($"使用默认皮肤: {skinAssetPath}", $"Using default skin: {skinAssetPath}");
                try
                {
                    using var stream = AssetLoader.Open(new Uri(skinAssetPath));
                    skinBitmap = new Bitmap(stream);
                }
                catch (Exception ex)
                {
                    Log($"加载默认皮肤失败: {ex.Message}", $"Failed to load default skin: {ex.Message}");
                }
            }

            if (skinBitmap == null) return;

            try
            {
                _fullSkinBitmap?.Dispose();
                _fullSkinBitmap = skinBitmap;
                var skinViewer = this.FindControl<Pivot3DControl>("SkinViewer");
                if (skinViewer != null) skinViewer.Skin = _fullSkinBitmap;
                Log("3D 皮肤视图已更新", "3D skin view updated");
            }
            catch (Exception ex)
            {
                Log($"更新 3D 皮肤视图失败: {ex.Message}", $"Failed to update 3D skin view: {ex.Message}");
            }

            var faceImg = this.FindControl<Image>("HomeFaceImage");
            var hatImg = this.FindControl<Image>("HomeHatImage");
            if (faceImg != null && hatImg != null)
            {
                try
                {
                    if (_fullSkinBitmap != null)
                    {
                        var (face, hat) = CropSkinFromBitmap(_fullSkinBitmap);
                        if (face != null && hat != null)
                        {
                            faceImg.Source = face;
                            hatImg.Source = hat;
                            RenderOptions.SetBitmapInterpolationMode(faceImg, BitmapInterpolationMode.None);
                            RenderOptions.SetBitmapInterpolationMode(hatImg, BitmapInterpolationMode.None);
                            Log("2D 头像已更新", "2D avatar updated");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log($"更新 2D 头像失败: {ex.Message}", $"Failed to update 2D avatar: {ex.Message}");
                }
            }
        }

        private void Log(string zh, string en) => LogHelper.Write(LauncherPaths.MainWindowLog, zh, en);

        // ============================================================
        // 目录选择（不能崩）
        // ============================================================

        /// <summary>上一次成功选中的目录，手动输入时作为默认值。</summary>
        private string? _lastPickPath;

        /// <summary>
        /// 安全地选目录：
        ///   1) Avalonia 的 Win32 选择器在「选中项已失效 / 非本地路径」时会直接抛
        ///      ArgumentException("Directory must exist")，必须兜住；
        ///   2) 即便选择器没抛，也要自己校验路径真实存在；
        ///   3) 选择器不可用时退化成手动输入路径，保证用户仍能继续。
        /// </summary>
        private async Task<string?> PickFolderSafeAsync(string title)
        {
            try
            {
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel == null) return null;

                var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    Title = title,
                    AllowMultiple = false
                });

                if (folders == null || folders.Count < 1)
                {
                    Log("用户取消目录选择", "User cancelled folder picking");
                    return null;
                }

                string? path = null;
                try
                {
                    path = folders[0].TryGetLocalPath();
                }
                catch (Exception ex)
                {
                    Log($"取本地路径失败: {ex.Message}", $"TryGetLocalPath failed: {ex.Message}");
                }

                path = path?.Trim().TrimEnd('\\', '/');

                if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
                {
                    Log($"所选目录不可用: {path}", $"Picked folder unavailable: {path}");
                    await ShowToastAsync("所选目录不可用，请重新选择", "Errors");
                    return await PromptForLocalPathAsync(title);
                }

                Log($"用户选择目录: {path}", $"User selected directory: {path}");
                _lastPickPath = path;
                return path;
            }
            catch (Exception ex)
            {
                // 这里曾经直接把整个启动器带崩（async void + Avalonia 选择器内部异常）
                Log($"文件夹选择器异常: {ex.Message}", $"Folder picker failed: {ex.Message}");
                LogHelper.Write(LauncherPaths.AppLog,
                    $"文件夹选择器异常: {ex}",
                    $"Folder picker exception: {ex}");

                await ShowToastAsync("无法打开文件夹选择器，改为手动输入", "Errors");
                return await PromptForLocalPathAsync(title);
            }
        }

        /// <summary>选择器不可用时的兜底：手动输入目录。</summary>
        private async Task<string?> PromptForLocalPathAsync(string title)
        {
            var window = new Window
            {
                Title = title,
                Width = 580,
                Height = 220,
                CanResize = false,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = Brushes.White
            };

            var inputBox = new TextBox
            {
                Text = _lastPickPath ?? string.Empty,
                PlaceholderText = @"例如 E:\Minecraft",
                FontSize = 15
            };

            string? result = null;

            var okButton = new Button
            {
                Content = "确定",
                Width = 110,
                Height = 36,
                Classes = { "Miret" }
            };
            var cancelButton = new Button
            {
                Content = "取消",
                Width = 110,
                Height = 36,
                Classes = { "Miret" }
            };

            okButton.Click += (_, _) => { result = inputBox.Text; window.Close(); };
            cancelButton.Click += (_, _) => { result = null; window.Close(); };

            var buttonRow = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                Spacing = 12
            };
            buttonRow.Children.Add(cancelButton);
            buttonRow.Children.Add(okButton);

            var panel = new StackPanel { Margin = new Thickness(20), Spacing = 14 };
            panel.Children.Add(new TextBlock
            {
                Text = "请输入 Minecraft 安装目录（.minecraft 文件夹）：",
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.Parse("#666666"))
            });
            panel.Children.Add(inputBox);
            panel.Children.Add(buttonRow);

            window.Content = panel;
            await window.ShowDialog(this);

            string? path = result?.Trim().Trim('"').TrimEnd('\\', '/');
            if (string.IsNullOrWhiteSpace(path)) return null;

            if (!Directory.Exists(path))
            {
                await ShowToastAsync("目录不存在，请重新选择", "Errors");
                return null;
            }

            Log($"用户手动输入目录: {path}", $"User entered directory: {path}");
            _lastPickPath = path;
            return path;
        }

        private async Task ShowToastAsync(string message, string sound = "Messages")
        {
            SoundService.Play(sound);
            var toast = this.FindControl<Border>("CancelToast");
            var toastText = this.FindControl<TextBlock>("ToastText");
            if (toast == null) return;

            if (toastText != null) toastText.Text = message;

            toast.IsVisible = true;
            toast.Opacity = 0;

            await Task.Delay(20);
            toast.Opacity = 1;
            await Task.Delay(2500);

            toast.Opacity = 0;
            await Task.Delay(320);
            toast.IsVisible = false;
        }

        private Task ShowCancelToastAsync() => ShowToastAsync("用户取消登录", "Messages");

        private (Bitmap? face, Bitmap? hat) CropSkinFromBitmap(Bitmap bitmap)
        {
            try
            {
                int width = bitmap.PixelSize.Width;
                int height = bitmap.PixelSize.Height;
                
                int scale = width / 64;
                if (scale < 1) return (null, null);
                
                int faceX = 8 * scale;
                int faceY = 8 * scale;
                int faceSize = 8 * scale;
                int hatX = 40 * scale;
                int hatY = 8 * scale;

                var faceCropped = new CroppedBitmap(bitmap, new PixelRect(faceX, faceY, faceSize, faceSize));
                var hatCropped = new CroppedBitmap(bitmap, new PixelRect(hatX, hatY, faceSize, faceSize));

                var faceBmp = new RenderTargetBitmap(new PixelSize(faceSize, faceSize), new Vector(96, 96));
                using (var ctx = faceBmp.CreateDrawingContext()) ctx.DrawImage(faceCropped, new Rect(0, 0, faceSize, faceSize));

                var hatBmp = new RenderTargetBitmap(new PixelSize(faceSize, faceSize), new Vector(96, 96));
                using (var ctx = hatBmp.CreateDrawingContext()) ctx.DrawImage(hatCropped, new Rect(0, 0, faceSize, faceSize));

                return (faceBmp, hatBmp);
            }
            catch { return (null, null); }
        }

        private void UpdateTitleBarGradient(Color startColor, Color endColor)
        {
            var titleBar = this.FindControl<Border>("TitleBarBorder");
            if (titleBar == null) return;

            var gradient = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                GradientStops = new GradientStops
                {
                    new GradientStop(startColor, 0.0),
                    new GradientStop(endColor, 1.0)
                }
            };

            titleBar.Background = gradient;
            UpdateTitleBarForeground(startColor, endColor);
        }

        private void UpdateTitleBarForeground(Color startColor, Color endColor)
        {
            double avgLuminance = (GetLuminance(startColor) + GetLuminance(endColor)) / 2.0;
            bool useDark = avgLuminance > 0.5;

            var titleText = this.FindControl<TextBlock>("TitleText");
            if (titleText != null) titleText.Foreground = useDark ? new SolidColorBrush(Colors.Black) : new SolidColorBrush(Colors.White);

            var minImg = this.FindControl<Image>("MinimizeImage");
            if (minImg != null)
            {
                var path = useDark ? "avares://Project.Launch/MainSrc/Views/imgs/Main/images/Buttons/black-.png" : "avares://Project.Launch/MainSrc/Views/imgs/Main/images/Buttons/white-.png";
                try { var uri = new Uri(path); using var stream = AssetLoader.Open(uri); minImg.Source = new Bitmap(stream); } catch { }
            }

            var closeImg = this.FindControl<Image>("CloseImage");
            if (closeImg != null)
            {
                var path = useDark ? "avares://Project.Launch/MainSrc/Views/imgs/Main/images/Buttons/blackx.png" : "avares://Project.Launch/MainSrc/Views/imgs/Main/images/Buttons/whitex.png";
                try { var uri = new Uri(path); using var stream = AssetLoader.Open(uri); closeImg.Source = new Bitmap(stream); } catch { }
            }
        }

        private static double GetLuminance(Color color)
        {
            double r = color.R / 255.0;
            double g = color.G / 255.0;
            double b = color.B / 255.0;

            r = r <= 0.03928 ? r / 12.92 : Math.Pow((r + 0.055) / 1.055, 2.4);
            g = g <= 0.03928 ? g / 12.92 : Math.Pow((g + 0.055) / 1.055, 2.4);
            b = b <= 0.03928 ? b / 12.92 : Math.Pow((b + 0.055) / 1.055, 2.4);

            return 0.2126 * r + 0.7152 * g + 0.0722 * b;
        }

        private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) BeginMoveDrag(e);
        }

        public void SetTitleBarGradient(Color start, Color end)
        {
            UpdateTitleBarGradient(start, end);
        }
    }
}
```

### `MainSrc\Views\read.md`

```markdown
--By Erics   九条开源
Gui层  图标源文件引用  美化

GUI Layer | Icon Asset References | Theme/Skin Scripts Directory
```

### `MainSrc\Views\Styles.axaml`

```xml
<Styles xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:media="using:Avalonia.Media">
    
    <Styles.Resources>
        <!-- 主题色 -->
        <Color x:Key="MiretPrimaryColor">#D969CE</Color>
        <Color x:Key="MiretHoverColor">#E88ADB</Color>
        <Color x:Key="MiretPressedColor">#C24AB5</Color>
        
        <!-- Miret 半透明刷子 -->
        <SolidColorBrush x:Key="MiretAcrylicBrush" 
                         Color="{StaticResource MiretPrimaryColor}" 
                         Opacity="0.55" />
        <SolidColorBrush x:Key="MiretAcrylicHoverBrush" 
                         Color="{StaticResource MiretHoverColor}" 
                         Opacity="0.7" />
        <SolidColorBrush x:Key="MiretAcrylicPressedBrush" 
                         Color="{StaticResource MiretPressedColor}" 
                         Opacity="0.85" />

        <!-- Lar-Miret 亚克力刷（更浅、更透） -->
        <SolidColorBrush x:Key="LarMiretBrush" 
                         Color="{StaticResource MiretPrimaryColor}" 
                         Opacity="0.35" />
        <SolidColorBrush x:Key="LarMiretHoverBrush" 
                         Color="{StaticResource MiretHoverColor}" 
                         Opacity="0.5" />
        <SolidColorBrush x:Key="LarMiretPressedBrush" 
                         Color="{StaticResource MiretPressedColor}" 
                         Opacity="0.6" />

        <!-- Lar-Miret 阴影 -->
        <media:DropShadowEffect x:Key="LarMiretShadow"
                                BlurRadius="12"
                                OffsetX="0"
                                OffsetY="6"
                                Color="#44000000" />
        <media:DropShadowEffect x:Key="LarMiretHoverShadow"
                                BlurRadius="16"
                                OffsetX="0"
                                OffsetY="8"
                                Color="#66000000" />

        <!-- 左上角高光渐变 -->
        <LinearGradientBrush x:Key="LarMiretHighlight"
                             StartPoint="0,0"
                             EndPoint="1,1">
            <GradientStop Color="#80FFFFFF" Offset="0.0" />
            <GradientStop Color="#00FFFFFF" Offset="0.6" />
        </LinearGradientBrush>
    </Styles.Resources>

    <!-- ================================================== -->
    <!-- Miret 标准按钮 -->
    <!-- ================================================== -->
    <Style Selector="Button.Miret">
        <Setter Property="ClipToBounds" Value="False" />
        <Setter Property="Background" Value="{StaticResource MiretPrimaryColor}" />
        <Setter Property="Foreground" Value="White" />
        <Setter Property="BorderThickness" Value="0" />
        <Setter Property="Cursor" Value="Hand" />
        <Setter Property="FontWeight" Value="SemiBold" />
        <Setter Property="Padding" Value="14,6" />
        <Setter Property="MinHeight" Value="32" />
        <Setter Property="CornerRadius" Value="8" />
        <Setter Property="Transitions">
            <Setter.Value>
                <Transitions>
                    <BrushTransition Property="Background" Duration="0:0:0.15" />
                </Transitions>
            </Setter.Value>
        </Setter>
    </Style>

    <Style Selector="Button.Miret:pointerover /template/ ContentPresenter">
        <Setter Property="Background" Value="{StaticResource MiretHoverColor}" />
        <Setter Property="TextBlock.Foreground" Value="White" />
    </Style>

    <Style Selector="Button.Miret:pressed /template/ ContentPresenter">
        <Setter Property="Background" Value="{StaticResource MiretPressedColor}" />
    </Style>

    <!-- ================================================== -->
    <!-- Miret 亚克力按钮 -->
    <!-- ================================================== -->
    <Style Selector="Button.MiretAcrylic">
        <Setter Property="ClipToBounds" Value="False" />
        <Setter Property="Background" Value="{StaticResource MiretAcrylicBrush}" />
        <Setter Property="Foreground" Value="White" />
        <Setter Property="BorderBrush" Value="#55FFFFFF" />
        <Setter Property="BorderThickness" Value="1.5" />
        <Setter Property="Cursor" Value="Hand" />
        <Setter Property="FontWeight" Value="SemiBold" />
        <Setter Property="Padding" Value="14,6" />
        <Setter Property="MinHeight" Value="32" />
        <Setter Property="CornerRadius" Value="8" />
        <Setter Property="Effect">
            <Setter.Value>
                <media:DropShadowEffect BlurRadius="8" OffsetX="3" OffsetY="4" Color="#44000000" />
            </Setter.Value>
        </Setter>
        <Setter Property="Transitions">
            <Setter.Value>
                <Transitions>
                    <BrushTransition Property="Background" Duration="0:0:0.2" />
                </Transitions>
            </Setter.Value>
        </Setter>
    </Style>

    <Style Selector="Button.MiretAcrylic:pointerover /template/ ContentPresenter">
        <Setter Property="Background" Value="{StaticResource MiretAcrylicHoverBrush}" />
        <Setter Property="TextBlock.Foreground" Value="White" />
        <Setter Property="Effect">
            <Setter.Value>
                <media:DropShadowEffect BlurRadius="12" OffsetX="4" OffsetY="6" Color="#66000000" />
            </Setter.Value>
        </Setter>
    </Style>

    <Style Selector="Button.MiretAcrylic:pressed /template/ ContentPresenter">
        <Setter Property="Background" Value="{StaticResource MiretAcrylicPressedBrush}" />
        <Setter Property="Effect">
            <Setter.Value>
                <media:DropShadowEffect BlurRadius="4" OffsetX="1" OffsetY="2" Color="#44000000" />
            </Setter.Value>
        </Setter>
    </Style>

    <!-- ================================================== -->
    <!-- Lar-Miret 大按钮 -->
    <!-- ================================================== -->
    <Style Selector="Button.LarMiret">
        <Setter Property="ClipToBounds" Value="True" />
        <Setter Property="Background" Value="{StaticResource LarMiretBrush}" />
        <Setter Property="Foreground" Value="White" />
        <Setter Property="BorderBrush" Value="#44FFFFFF" />
        <Setter Property="BorderThickness" Value="1.5" />
        <Setter Property="Cursor" Value="Hand" />
        <Setter Property="FontWeight" Value="Bold" />
        <Setter Property="FontSize" Value="22" />
        <Setter Property="Padding" Value="20,8" />
        <Setter Property="MinHeight" Value="72" />
        <Setter Property="CornerRadius" Value="14" />
        <Setter Property="Effect" Value="{StaticResource LarMiretShadow}" />
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate>
                    <Grid>
                        <Border Background="{TemplateBinding Background}"
                                BorderBrush="{TemplateBinding BorderBrush}"
                                BorderThickness="{TemplateBinding BorderThickness}"
                                CornerRadius="{TemplateBinding CornerRadius}" />
                        <Border Background="{StaticResource LarMiretHighlight}"
                                CornerRadius="{TemplateBinding CornerRadius}" />
                        <ContentPresenter Content="{TemplateBinding Content}"
                                          HorizontalAlignment="Center"
                                          VerticalAlignment="Center" />
                    </Grid>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
        <Setter Property="Transitions">
            <Setter.Value>
                <Transitions>
                    <!-- ★ 只保留 Background 的过渡，Effect 不能使用 BrushTransition -->
                    <BrushTransition Property="Background" Duration="0:0:0.2" />
                </Transitions>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- Lar-Miret 悬停 -->
    <Style Selector="Button.LarMiret:pointerover">
        <Setter Property="Background" Value="{StaticResource LarMiretHoverBrush}" />
        <Setter Property="Effect" Value="{StaticResource LarMiretHoverShadow}" />
    </Style>

    <!-- Lar-Miret 按下 -->
    <Style Selector="Button.LarMiret:pressed">
        <Setter Property="Background" Value="{StaticResource LarMiretPressedBrush}" />
        <Setter Property="Effect" Value="{StaticResource LarMiretShadow}" />
    </Style>

    <!-- Lar-Miret 内文字样式 -->
    <Style Selector="Button.LarMiret TextBlock">
        <Setter Property="FontWeight" Value="Bold" />
        <Setter Property="Foreground" Value="White" />
    </Style>
</Styles>
```


## 6.5 MainSrc\Tools\JavaConfig —— 启动内核

### `MainSrc\Tools\JavaConfig\GameLauncher.cs`

```csharp
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Project.Launch.Tools.AppConstants;
using Project.Launch.Tools.LogsConfig;
namespace Project.Launch.Tools.JavaConfig
{
    public static class GameLauncher
    {
        // 保活正在运行的进程，防止 Process 对象被 GC 回收导致输出流中断
        private static readonly List<Process> _runningProcesses = new();

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

                // ★ 追加语言参数：确保与系统语言一致（覆盖 options.txt 中的默认英文）
                args.Add("--language");
                args.Add(GetMinecraftLanguage());

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

                // 将进程加入保活列表
                lock (_runningProcesses)
                {
                    _runningProcesses.Add(proc);
                }

                LogHelper.Write(LauncherPaths.AppLog,
                    $"游戏已启动 (PID={proc.Id})，日志文件: {logPath}",
                    $"Game launched (PID={proc.Id}), log file: {logPath}");

                // 等待 3 秒检测是否闪退
                using var cts = new CancellationTokenSource(3000);
                try
                {
                    // 如果 3 秒内进程退出了，说明启动失败闪退了
                    await proc.WaitForExitAsync(cts.Token);
                    
                    // 确保输出流被完全读取
                    proc.WaitForExit(); 

                    // 闪退时，读取最后几十行日志直接写入 app.log，方便用户排查
                    string crashLog = "";
                    try
                    {
                        if (File.Exists(logPath))
                        {
                            var lines = File.ReadAllLines(logPath);
                            crashLog = string.Join("\n", lines.TakeLast(50));
                        }
                    }
                    catch { }

                    LogHelper.Write(LauncherPaths.AppLog,
                        $"游戏进程异常退出，退出码: {proc.ExitCode}\n最后输出:\n{crashLog}",
                        $"Game process exited with code: {proc.ExitCode}\nLast Output:\n{crashLog}");

                    lock (_runningProcesses)
                    {
                        _runningProcesses.Remove(proc);
                    }

                    return false;
                }
                catch (OperationCanceledException)
                {
                    // 3秒后依然在运行，说明游戏成功启动了
                    return true;
                }
            }
            catch (Exception ex)
            {
                LogHelper.Write(LauncherPaths.AppLog,
                    $"启动游戏失败: {ex.Message}",
                    $"Launch failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 获取当前系统语言对应的 Minecraft 语言代码
        /// </summary>
        private static string GetMinecraftLanguage()
        {
            try
            {
                // 获取系统 UI 语言，例如 "zh-CN", "en-US"
                string culture = System.Globalization.CultureInfo.CurrentUICulture.Name;
                string lang = culture.Replace("-", "_").ToLowerInvariant();

                // 处理简繁体映射
                if (lang.StartsWith("zh_hans") || lang == "zh_cn" || lang == "zh_sg")
                    return "zh_cn";
                if (lang.StartsWith("zh_hant") || lang == "zh_tw" || lang == "zh_hk")
                    return "zh_tw";
                
                return lang;
            }
            catch
            {
                return "en_us";
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

            // 默认必须是 false，否则非 Windows 的规则（如 macOS）会被错误放行
            bool allowed = false; 
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

                // ★ 修复：处理 features 规则。目前启动器不支持任何特殊 feature（如 quick play），
                // 因此只要 JSON 要求某个 feature 为 true，我们就视为不匹配，拒绝添加该参数。
                if (rule.TryGetProperty("features", out var features) && 
                    features.ValueKind == JsonValueKind.Object)
                {
                    foreach (var feature in features.EnumerateObject())
                    {
                        if (feature.Value.ValueKind == JsonValueKind.True)
                        {
                            matches = false;
                            break;
                        }
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
                .Replace("${launcher_name}", SetConstants.LauncherName)
                .Replace("${launcher_version}", SetConstants.Version)
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
```

### `MainSrc\Tools\JavaConfig\InstanceData.cs`

```csharp
using System;
using System.Collections.Generic;

namespace Project.Launch.Tools.JavaConfig
{
    public class InstanceInfo
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = string.Empty;
        public string RootPath { get; set; } = string.Empty;
        /// <summary>"standard"（.minecraft 根目录） 或 "isolated"（隔离实例）</summary>
        public string Layout { get; set; } = "standard";
        public string VersionId { get; set; } = string.Empty;
        public string JavaPath { get; set; } = string.Empty;
        public DateTime ImportedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastUsed { get; set; } = DateTime.UtcNow;
    }

    public class InstancesRoot
    {
        public List<InstanceInfo> Instances { get; set; } = new();
        public string SelectedInstanceId { get; set; } = string.Empty;
    }
}
```

### `MainSrc\Tools\JavaConfig\InstanceService.cs`

```csharp
using System;
using System.IO;
using System.Linq;
using System.Text.Json;

using Project.Launch.Tools.LogsConfig;
namespace Project.Launch.Tools.JavaConfig
{
    public static class InstanceService
    {
        private static readonly object _lock = new object();

        private static string FilePath => Path.Combine(LauncherPaths.Root, "instances.json");

        public static InstancesRoot LoadInstances()
        {
            lock (_lock)
            {
                if (!File.Exists(FilePath))
                    return new InstancesRoot();

                try
                {
                    string json = File.ReadAllText(FilePath);
                    return JsonSerializer.Deserialize<InstancesRoot>(json) ?? new InstancesRoot();
                }
                catch (Exception ex)
                {
                    LogHelper.Write(LauncherPaths.AppLog,
                        $"读取实例失败: {ex.Message}",
                        $"Failed to load instances: {ex.Message}");
                    return new InstancesRoot();
                }
            }
        }

        public static void SaveInstances(InstancesRoot root)
        {
            lock (_lock)
            {
                try
                {
                    string json = JsonSerializer.Serialize(root,
                        new JsonSerializerOptions { WriteIndented = true });
                    string tmp = FilePath + ".tmp";
                    File.WriteAllText(tmp, json);
                    File.Move(tmp, FilePath, overwrite: true);
                }
                catch (Exception ex)
                {
                    LogHelper.Write(LauncherPaths.AppLog,
                        $"保存实例失败: {ex.Message}",
                        $"Failed to save instances: {ex.Message}");
                }
            }
        }

        public static InstanceInfo? GetSelectedInstance()
        {
            var root = LoadInstances();
            if (string.IsNullOrEmpty(root.SelectedInstanceId))
                return null;
            return root.Instances.FirstOrDefault(i => i.Id == root.SelectedInstanceId);
        }

        public static void SetSelectedInstance(InstanceInfo instance)
        {
            var root = LoadInstances();
            var existing = root.Instances.FirstOrDefault(i => i.Id == instance.Id);
            if (existing == null)
                root.Instances.Add(instance);
            else
            {
                existing.Name = instance.Name;
                existing.RootPath = instance.RootPath;
                existing.Layout = instance.Layout;
                existing.VersionId = instance.VersionId;
                existing.JavaPath = instance.JavaPath;
                existing.LastUsed = DateTime.UtcNow;
            }
            root.SelectedInstanceId = instance.Id;
            SaveInstances(root);
        }
    }
}
```

### `MainSrc\Tools\JavaConfig\InteractionConfig.cs`

```csharp
namespace Project.Launch.Tools.JavaConfig
{
    /// <summary>
    /// 交互配置——鼠标拖拽、手势等
    /// 后期"设置"页面可在这里读写
    /// </summary>
    public static class InteractionConfig
    {
        /// <summary>
        /// 3D 皮肤拖拽：水平方向反转
        /// false = 鼠标向右拖 → 人物向右转（当前默认）
        /// true  = 鼠标向右拖 → 人物向左转
        /// </summary>
        public static bool PivotInvertX { get; set; } = false;

        /// <summary>
        /// 3D 皮肤拖拽：垂直方向反转
        /// false = 鼠标向上拖 → 人物向上看（当前默认）
        /// true  = 鼠标向上拖 → 人物向下看
        /// </summary>
        public static bool PivotInvertY { get; set; } = false;

        /// <summary>
        /// 3D 皮肤拖拽灵敏度（0.5 = 鼠标 1 像素转 0.5°）
        /// </summary>
        public static float PivotSensitivity { get; set; } = 0.5f;
    }
}
```

### `MainSrc\Tools\JavaConfig\JavaDetector.cs`

```csharp
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Project.Launch.Tools.JavaConfig
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
```

### `MainSrc\Tools\JavaConfig\LauncherPaths.cs`

```csharp
using System;
using System.IO;

namespace Project.Launch.Tools.JavaConfig
{
    /// <summary>
    /// 启动器所有关键路径集中管理
    /// Launcher path definitions - centralized
    /// </summary>
    public static class LauncherPaths
    {
        /// <summary>
        /// 启动器根目录：exe 同级 KilimLauncher/
        /// Launcher root: KilimLauncher/ next to exe
        /// </summary>
        public static readonly string Root = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "KilimLauncher"
        );

        // ================== 配置文件 ==================
        /// <summary>账户文件 / Accounts file</summary>
        public static readonly string AccountsFile = Path.Combine(Root, "accounts.json");

        // ================== 日志文件 ==================
        /// <summary>主窗口日志 / Main window log</summary>
        public static readonly string MainWindowLog = Path.Combine(Root, "MainWindowLog.txt");

        /// <summary>启动日志 / Startup log</summary>
        public static readonly string StartupLog = Path.Combine(Root, "startup.log");

        /// <summary>崩溃日志 / Crash log</summary>
        public static readonly string CrashLog = Path.Combine(Root, "crash.log");

        /// <summary>App 启动日志 / App startup log</summary>
        public static readonly string AppLog = Path.Combine(Root, "app.log");

        static LauncherPaths()
        {
            try
            {
                if (!Directory.Exists(Root))
                    Directory.CreateDirectory(Root);
            }
            catch { }
        }
    }
}
```

### `MainSrc\Tools\JavaConfig\MinecraftDirectoryAnalyzer.cs`

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Project.Launch.Tools.LogsConfig;
namespace Project.Launch.Tools.JavaConfig
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
```

### `MainSrc\Tools\JavaConfig\MinecraftVersionJson.cs`

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

using Project.Launch.Tools.LogsConfig;
namespace Project.Launch.Tools.JavaConfig
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
```

### `MainSrc\Tools\JavaConfig\SkinCropper.cs`

```csharp
using System;
using System.IO;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Project.Launch.Tools.JavaConfig
{
    /// <summary>
    /// 皮肤裁切工具：从皮肤 PNG 裁出方形头像
    /// </summary>
    public static class SkinCropper
    {
        /// <summary>
        /// 同步裁切：返回 (face 头像, hat 帽子层)
        /// 支持 64×64 / 128×128 / 256×256 等任意倍率皮肤
        /// </summary>
        public static (Bitmap? face, Bitmap? hat) Crop(Stream stream)
        {
            try
            {
                using var src = new Bitmap(stream);
                int width = src.PixelSize.Width;
                int height = src.PixelSize.Height;

                // 动态计算比例
                int scale = width / 64;
                if (scale < 1) return (null, null);

                int faceX = 8 * scale;
                int faceY = 8 * scale;
                int faceSize = 8 * scale;
                int hatX = 40 * scale;
                int hatY = 8 * scale;

                var faceCropped = new CroppedBitmap(src, new PixelRect(faceX, faceY, faceSize, faceSize));
                var hatCropped = new CroppedBitmap(src, new PixelRect(hatX, hatY, faceSize, faceSize));

                var faceBmp = new RenderTargetBitmap(new PixelSize(faceSize, faceSize), new Vector(96, 96));
                using (var ctx = faceBmp.CreateDrawingContext())
                    ctx.DrawImage(faceCropped, new Rect(0, 0, faceSize, faceSize));

                var hatBmp = new RenderTargetBitmap(new PixelSize(faceSize, faceSize), new Vector(96, 96));
                using (var ctx = hatBmp.CreateDrawingContext())
                    ctx.DrawImage(hatCropped, new Rect(0, 0, faceSize, faceSize));

                return (faceBmp, hatBmp);
            }
            catch
            {
                return (null, null);
            }
        }

        /// <summary>
        /// 从资源 URI 同步裁切 face 头像
        /// </summary>
        public static Bitmap? CropFaceFromAsset(string assetUri)
        {
            try
            {
                using var stream = AssetLoader.Open(new Uri(assetUri));
                var (face, _) = Crop(stream);
                return face;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 从资源 URI 同步裁切 face + hat
        /// </summary>
        public static (Bitmap? face, Bitmap? hat) CropFromAsset(string assetUri)
        {
            try
            {
                using var stream = AssetLoader.Open(new Uri(assetUri));
                return Crop(stream);
            }
            catch
            {
                return (null, null);
            }
        }
    }
}
```


## 6.6 MainSrc\Tools\Player3DConfig —— 3D 皮肤

### `MainSrc\Tools\Player3DConfig\MinecraftSkinModel.cs`

```csharp
using System.Collections.Generic;

namespace Project.Launch.Tools.Player3DConfig
{
    public struct Vec3
    {
        public float X, Y, Z;
        public Vec3(float x, float y, float z) { X = x; Y = y; Z = z; }
    }

    public struct Vec2
    {
        public float U, V;
        public Vec2(float u, float v) { U = u; V = v; }
    }

    /// <summary>模型类型：Classic（4 宽手臂） / Slim（3 宽手臂）</summary>
    public enum SkinModelType
    {
        Classic,
        Slim
    }

    public class SkinFace
    {
        public Vec3 V0, V1, V2, V3;
        public Vec2 T0, T1, T2, T3;
        public string Part = "";
        public bool Overlay;

        public SkinFace(Vec3 v0, Vec3 v1, Vec3 v2, Vec3 v3,
                        Vec2 t0, Vec2 t1, Vec2 t2, Vec2 t3,
                        string part, bool overlay)
        {
            V0 = v0; V1 = v1; V2 = v2; V3 = v3;
            T0 = t0; T1 = t1; T2 = t2; T3 = t3;
            Part = part;
            Overlay = overlay;
        }
    }

    public static class MinecraftSkinModel
    {
        private const float TEX_W = 64f;
        private const float TEX_H = 64f;

        public static List<SkinFace> Build(SkinModelType type = SkinModelType.Classic)
        {
            var faces = new List<SkinFace>();

            // ============ 头部 8×8×8 ============
            AddBox(faces, "head", false,
                x0: -4f, y0: 24f, z0: -4f, x1: 4f, y1: 32f, z1: 4f,
                front:  (8, 8, 8, 8),
                back:   (24, 8, 8, 8),
                right:  (0, 8, 8, 8),
                left:   (16, 8, 8, 8),
                top:    (8, 0, 8, 8),
                bottom: (16, 0, 8, 8));

            AddBox(faces, "hat", true,
                x0: -4.25f, y0: 23.75f, z0: -4.25f, x1: 4.25f, y1: 32.25f, z1: 4.25f,
                front:  (40, 8, 8, 8),
                back:   (56, 8, 8, 8),
                right:  (32, 8, 8, 8),
                left:   (48, 8, 8, 8),
                top:    (40, 0, 8, 8),
                bottom: (48, 0, 8, 8));

            // ============ 身体 8×12×4 ============
            AddBox(faces, "body", false,
                x0: -4f, y0: 12f, z0: -2f, x1: 4f, y1: 24f, z1: 2f,
                front:  (20, 20, 8, 12),
                back:   (32, 20, 8, 12),
                right:  (16, 20, 4, 12),
                left:   (28, 20, 4, 12),
                top:    (20, 16, 8, 4),
                bottom: (28, 16, 8, 4));

            AddBox(faces, "bodyOverlay", true,
                x0: -4.25f, y0: 11.75f, z0: -2.25f, x1: 4.25f, y1: 24.25f, z1: 2.25f,
                front:  (20, 36, 8, 12),
                back:   (32, 36, 8, 12),
                right:  (16, 36, 4, 12),
                left:   (28, 36, 4, 12),
                top:    (20, 32, 8, 4),
                bottom: (28, 32, 8, 4));

            // ============ 手臂（Classic / Slim 分支）============
            if (type == SkinModelType.Slim)
            {
                // 右臂 3×12×4，中心 (-5.5, 18, 0)，X 范围 [-7, -4]
                AddBox(faces, "rightArm", false,
                    x0: -7f, y0: 12f, z0: -2f, x1: -4f, y1: 24f, z1: 2f,
                    front:  (44, 20, 3, 12),
                    back:   (51, 20, 3, 12),
                    right:  (40, 20, 4, 12),
                    left:   (47, 20, 4, 12),
                    top:    (44, 16, 3, 4),
                    bottom: (47, 16, 3, 4));

                AddBox(faces, "rightArmOverlay", true,
                    x0: -7.25f, y0: 11.75f, z0: -2.25f, x1: -3.75f, y1: 24.25f, z1: 2.25f,
                    front:  (44, 36, 3, 12),
                    back:   (51, 36, 3, 12),
                    right:  (40, 36, 4, 12),
                    left:   (47, 36, 4, 12),
                    top:    (44, 32, 3, 4),
                    bottom: (47, 32, 3, 4));

                // 左臂 3×12×4，中心 (5.5, 18, 0)，X 范围 [4, 7]
                AddBox(faces, "leftArm", false,
                    x0: 4f, y0: 12f, z0: -2f, x1: 7f, y1: 24f, z1: 2f,
                    front:  (36, 52, 3, 12),
                    back:   (43, 52, 3, 12),
                    right:  (32, 52, 4, 12),
                    left:   (39, 52, 4, 12),
                    top:    (36, 48, 3, 4),
                    bottom: (39, 48, 3, 4));

                AddBox(faces, "leftArmOverlay", true,
                    x0: 3.75f, y0: 11.75f, z0: -2.25f, x1: 7.25f, y1: 24.25f, z1: 2.25f,
                    front:  (52, 52, 3, 12),
                    back:   (59, 52, 3, 12),
                    right:  (48, 52, 4, 12),
                    left:   (55, 52, 4, 12),
                    top:    (52, 48, 3, 4),
                    bottom: (55, 48, 3, 4));
            }
            else
            {
                // Classic 4×12×4
                AddBox(faces, "rightArm", false,
                    x0: -8f, y0: 12f, z0: -2f, x1: -4f, y1: 24f, z1: 2f,
                    front:  (44, 20, 4, 12),
                    back:   (52, 20, 4, 12),
                    right:  (40, 20, 4, 12),
                    left:   (48, 20, 4, 12),
                    top:    (44, 16, 4, 4),
                    bottom: (48, 16, 4, 4));

                AddBox(faces, "rightArmOverlay", true,
                    x0: -8.25f, y0: 11.75f, z0: -2.25f, x1: -3.75f, y1: 24.25f, z1: 2.25f,
                    front:  (44, 36, 4, 12),
                    back:   (52, 36, 4, 12),
                    right:  (40, 36, 4, 12),
                    left:   (48, 36, 4, 12),
                    top:    (44, 32, 4, 4),
                    bottom: (48, 32, 4, 4));

                AddBox(faces, "leftArm", false,
                    x0: 4f, y0: 12f, z0: -2f, x1: 8f, y1: 24f, z1: 2f,
                    front:  (36, 52, 4, 12),
                    back:   (44, 52, 4, 12),
                    right:  (32, 52, 4, 12),
                    left:   (40, 52, 4, 12),
                    top:    (36, 48, 4, 4),
                    bottom: (40, 48, 4, 4));

                AddBox(faces, "leftArmOverlay", true,
                    x0: 3.75f, y0: 11.75f, z0: -2.25f, x1: 8.25f, y1: 24.25f, z1: 2.25f,
                    front:  (52, 52, 4, 12),
                    back:   (60, 52, 4, 12),
                    right:  (48, 52, 4, 12),
                    left:   (56, 52, 4, 12),
                    top:    (52, 48, 4, 4),
                    bottom: (56, 48, 4, 4));
            }

            // ============ 腿（Classic / Slim 一致）============
            AddBox(faces, "rightLeg", false,
                x0: -4f, y0: 0f, z0: -2f, x1: 0f, y1: 12f, z1: 2f,
                front:  (4, 20, 4, 12),
                back:   (12, 20, 4, 12),
                right:  (0, 20, 4, 12),
                left:   (8, 20, 4, 12),
                top:    (4, 16, 4, 4),
                bottom: (8, 16, 4, 4));

            AddBox(faces, "rightLegOverlay", true,
                x0: -4.25f, y0: -0.25f, z0: -2.25f, x1: 0.25f, y1: 12.25f, z1: 2.25f,
                front:  (4, 36, 4, 12),
                back:   (12, 36, 4, 12),
                right:  (0, 36, 4, 12),
                left:   (8, 36, 4, 12),
                top:    (4, 32, 4, 4),
                bottom: (8, 32, 4, 4));

            AddBox(faces, "leftLeg", false,
                x0: 0f, y0: 0f, z0: -2f, x1: 4f, y1: 12f, z1: 2f,
                front:  (20, 52, 4, 12),
                back:   (28, 52, 4, 12),
                right:  (16, 52, 4, 12),
                left:   (24, 52, 4, 12),
                top:    (20, 48, 4, 4),
                bottom: (24, 48, 4, 4));

            AddBox(faces, "leftLegOverlay", true,
                x0: -0.25f, y0: -0.25f, z0: -2.25f, x1: 4.25f, y1: 12.25f, z1: 2.25f,
                front:  (4, 52, 4, 12),
                back:   (12, 52, 4, 12),
                right:  (0, 52, 4, 12),
                left:   (8, 52, 4, 12),
                top:    (4, 48, 4, 4),
                bottom: (8, 48, 4, 4));

            return faces;
        }

        private static void AddBox(
            List<SkinFace> faces, string part, bool overlay,
            float x0, float y0, float z0, float x1, float y1, float z1,
            (float u, float v, float w, float h) front,
            (float u, float v, float w, float h) back,
            (float u, float v, float w, float h) right,
            (float u, float v, float w, float h) left,
            (float u, float v, float w, float h) top,
            (float u, float v, float w, float h) bottom)
        {
            AddFace(faces, part, overlay,
                new Vec3(x0, y1, z1),
                new Vec3(x1, y1, z1),
                new Vec3(x1, y0, z1),
                new Vec3(x0, y0, z1),
                front);

            AddFace(faces, part, overlay,
                new Vec3(x1, y1, z0),
                new Vec3(x0, y1, z0),
                new Vec3(x0, y0, z0),
                new Vec3(x1, y0, z0),
                back);

            AddFace(faces, part, overlay,
                new Vec3(x0, y1, z0),
                new Vec3(x0, y1, z1),
                new Vec3(x0, y0, z1),
                new Vec3(x0, y0, z0),
                right);

            AddFace(faces, part, overlay,
                new Vec3(x1, y1, z1),
                new Vec3(x1, y1, z0),
                new Vec3(x1, y0, z0),
                new Vec3(x1, y0, z1),
                left);

            AddFace(faces, part, overlay,
                new Vec3(x0, y1, z0),
                new Vec3(x1, y1, z0),
                new Vec3(x1, y1, z1),
                new Vec3(x0, y1, z1),
                top);

            AddFace(faces, part, overlay,
                new Vec3(x1, y0, z0),
                new Vec3(x0, y0, z0),
                new Vec3(x0, y0, z1),
                new Vec3(x1, y0, z1),
                bottom);
        }

        private static void AddFace(
            List<SkinFace> faces, string part, bool overlay,
            Vec3 v0, Vec3 v1, Vec3 v2, Vec3 v3,
            (float u, float v, float w, float h) uv)
        {
            const float half = 0f;

            float uLeft = (uv.u + half) / TEX_W;
            float vTop = (uv.v + half) / TEX_H;
            float uRight = (uv.u + uv.w - half) / TEX_W;
            float vBottom = (uv.v + uv.h - half) / TEX_H;

            faces.Add(new SkinFace(
                v0, v1, v2, v3,
                new Vec2(uLeft, vTop),
                new Vec2(uRight, vTop),
                new Vec2(uRight, vBottom),
                new Vec2(uLeft, vBottom),
                part, overlay));
        }
    }
}
```

### `MainSrc\Tools\Player3DConfig\Pivot3DControl.cs`

```csharp
using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using Project.Launch.Tools;
using SkiaSharp;


using Project.Launch.Tools.JavaConfig;
using Project.Launch.Tools.LogsConfig;
namespace Project.Launch.Tools.Player3DConfig
{
    public class Pivot3DControl : Control
    {
        public static readonly StyledProperty<Bitmap?> SkinProperty =
            AvaloniaProperty.Register<Pivot3DControl, Bitmap?>(nameof(Skin));

        public Bitmap? Skin
        {
            get => GetValue(SkinProperty);
            set => SetValue(SkinProperty, value);
        }

        private List<SkinFace> _faces;
        private SkinModelType _currentModelType = SkinModelType.Classic;
        private SKImage? _skinImage;
        private float _yaw = 0f;
        private float _pitch = 0f;

        private bool _isDragging;
        private Point _dragStart;
        private float _dragStartYaw;
        private float _dragStartPitch;
        private DateTime _lastInteraction = DateTime.MinValue;

        private DispatcherTimer? _autoRotateTimer;

        public Pivot3DControl()
        {
            _faces = MinecraftSkinModel.Build(SkinModelType.Classic);
            Focusable = true;
            ClipToBounds = true;

            PointerPressed += OnPointerPressed;
            PointerMoved += OnPointerMoved;
            PointerReleased += OnPointerReleased;
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            _lastInteraction = DateTime.Now;
            _autoRotateTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
            _autoRotateTimer.Tick += OnAutoRotateTick;
            _autoRotateTimer.Start();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
            if (_autoRotateTimer != null)
            {
                _autoRotateTimer.Stop();
                _autoRotateTimer.Tick -= OnAutoRotateTick;
                _autoRotateTimer = null;
            }
            _skinImage?.Dispose();
            _skinImage = null;
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == SkinProperty)
            {
                _skinImage?.Dispose();
                _skinImage = BitmapToSkImage(Skin);

                if (Skin != null)
                {
                    var detected = DetectSkinType(Skin);
                    if (detected != _currentModelType)
                    {
                        _currentModelType = detected;
                        _faces = MinecraftSkinModel.Build(detected);

                        LogHelper.Write(LauncherPaths.AppLog,
                            $"皮肤检测: {detected}",
                            $"Skin detected: {detected}");
                    }
                }

                InvalidateVisual();
            }
        }

        // ============================================================
        // 检测皮肤类型（Classic / Slim）
        //
        // 方案：
        //   1. 看皮肤宽度决定是普通（64）还是 HD（128）
        //   2. 按比例缩放检测坐标
        //   3. 检测该列是否有颜色：
        //        - 有颜色 → Steve (Classic)
        //        - 全透明 → Alex (Slim)
        //
        // 基准：0-based (54, 20)，长度 12 行（对应 64×64）
        // ============================================================
        private static SkinModelType DetectSkinType(Bitmap bitmap)
        {
            try
            {
                int w = bitmap.PixelSize.Width;
                int h = bitmap.PixelSize.Height;

                // 老版 64×32 皮肤一律按 Classic
                if (w < 64 || h < 32) return SkinModelType.Classic;

                // 计算比例：64 → 1，128 → 2，256 → 4
                int ratio = w / 64;

                // 缩放检测坐标和长度
                int checkX = 54 * ratio;
                int checkY = 20 * ratio;
                int checkH = 12 * ratio;

                // 边界保护
                if (checkX >= w || checkY + checkH > h)
                    return SkinModelType.Classic;

                var rect = new PixelRect(checkX, checkY, 1, checkH);
                byte[] pixels = new byte[checkH * 4];
                unsafe
                {
                    fixed (byte* p = pixels)
                    {
                        bitmap.CopyPixels(rect, (IntPtr)p, pixels.Length, 4);
                    }
                }

                // 有任一不透明像素 → Classic
                for (int i = 0; i < checkH; i++)
                {
                    if (pixels[i * 4 + 3] != 0)
                    {
                        LogHelper.Write(LauncherPaths.AppLog,
                            $"检测: 尺寸={w}x{h}, ratio={ratio}, 位置=({checkX},{checkY}), 结果=Classic",
                            $"Detect: size={w}x{h}, ratio={ratio}, pos=({checkX},{checkY}), result=Classic");
                        return SkinModelType.Classic;
                    }
                }

                LogHelper.Write(LauncherPaths.AppLog,
                    $"检测: 尺寸={w}x{h}, ratio={ratio}, 位置=({checkX},{checkY}), 结果=Slim",
                    $"Detect: size={w}x{h}, ratio={ratio}, pos=({checkX},{checkY}), result=Slim");
                return SkinModelType.Slim;
            }
            catch (Exception ex)
            {
                LogHelper.Write(LauncherPaths.AppLog,
                    $"检测异常: {ex.Message}",
                    $"Detect exception: {ex.Message}");
                return SkinModelType.Classic;
            }
        }

        private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
            _isDragging = true;
            _dragStart = e.GetPosition(this);
            _dragStartYaw = _yaw;
            _dragStartPitch = _pitch;
            _lastInteraction = DateTime.Now;
            e.Pointer.Capture(this);
        }

        private void OnPointerMoved(object? sender, PointerEventArgs e)
        {
            if (!_isDragging) return;
            var cur = e.GetPosition(this);
            float dx = (float)(cur.X - _dragStart.X);
            float dy = (float)(cur.Y - _dragStart.Y);

            if (InteractionConfig.PivotInvertX) dx = -dx;
            if (InteractionConfig.PivotInvertY) dy = -dy;

            float sensitivity = InteractionConfig.PivotSensitivity;
            _yaw = _dragStartYaw + dx * sensitivity;
            _pitch = Math.Clamp(_dragStartPitch + dy * sensitivity, -30f, 30f);
            _lastInteraction = DateTime.Now;
            InvalidateVisual();
        }

        private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            _isDragging = false;
            _lastInteraction = DateTime.Now;
            e.Pointer.Capture(null);
        }

        private void OnAutoRotateTick(object? sender, EventArgs e)
        {
            if (_isDragging) return;
            if ((DateTime.Now - _lastInteraction).TotalSeconds < 2.0) return;
            _yaw += 0.4f;
            InvalidateVisual();
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);
            var b = Bounds;
            if (b.Width <= 0 || b.Height <= 0) return;

            context.FillRectangle(Brushes.Transparent, new Rect(b.Size));

            var projected = Pivot3DRenderer.Render(
                _faces, _yaw, _pitch,
                (float)b.Width, (float)b.Height,
                0.78f);

            context.Custom(new Pivot3DDrawOperation(
                new Rect(b.Size), projected, _skinImage));
        }

        private static SKImage? BitmapToSkImage(Bitmap? bmp)
        {
            if (bmp == null) return null;
            try
            {
                int w = bmp.PixelSize.Width;
                int h = bmp.PixelSize.Height;
                if (w <= 0 || h <= 0) return null;

                var info = new SKImageInfo(w, h, SKColorType.Bgra8888, SKAlphaType.Premul);
                byte[] pixels = new byte[w * h * 4];
                unsafe
                {
                    fixed (byte* p = pixels)
                    {
                        bmp.CopyPixels(new PixelRect(0, 0, w, h), (IntPtr)p, pixels.Length, w * 4);
                    }
                }
                return SKImage.FromPixelCopy(info, pixels, w * 4);
            }
            catch { return null; }
        }

        private class Pivot3DDrawOperation : ICustomDrawOperation
        {
            private readonly List<ProjectedFace> _faces;
            private readonly SKImage? _skin;

            public Rect Bounds { get; }

            public Pivot3DDrawOperation(Rect bounds, List<ProjectedFace> faces, SKImage? skin)
            {
                Bounds = bounds;
                _faces = faces;
                _skin = skin;
            }

            public void Dispose() { }
            public bool HitTest(Point p) => false;
            public bool Equals(ICustomDrawOperation? other) => false;

            public void Render(ImmediateDrawingContext context)
            {
                var leaseFeature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
                if (leaseFeature == null) return;

                using var lease = leaseFeature.Lease();
                var canvas = lease.SkCanvas;

                if (_skin == null || _faces.Count == 0) return;

                float texW = _skin.Width;
                float texH = _skin.Height;

                foreach (var f in _faces)
                {
                    var positions = new SKPoint[]
                    {
                        new SKPoint(f.X0, f.Y0),
                        new SKPoint(f.X1, f.Y1),
                        new SKPoint(f.X2, f.Y2),
                        new SKPoint(f.X0, f.Y0),
                        new SKPoint(f.X2, f.Y2),
                        new SKPoint(f.X3, f.Y3),
                    };

                    var texs = new SKPoint[]
                    {
                        new SKPoint(f.T0.U * texW, f.T0.V * texH),
                        new SKPoint(f.T1.U * texW, f.T1.V * texH),
                        new SKPoint(f.T2.U * texW, f.T2.V * texH),
                        new SKPoint(f.T0.U * texW, f.T0.V * texH),
                        new SKPoint(f.T2.U * texW, f.T2.V * texH),
                        new SKPoint(f.T3.U * texW, f.T3.V * texH),
                    };

                    byte s = (byte)Math.Clamp(f.Shade * 255f, 0f, 255f);
                    var colors = new SKColor[]
                    {
                        new SKColor(s, s, s, 255),
                        new SKColor(s, s, s, 255),
                        new SKColor(s, s, s, 255),
                        new SKColor(s, s, s, 255),
                        new SKColor(s, s, s, 255),
                        new SKColor(s, s, s, 255),
                    };

                    using var shader = SKShader.CreateImage(
                        _skin,
                        SKShaderTileMode.Clamp,
                        SKShaderTileMode.Clamp);

                    using var paint = new SKPaint
                    {
                        IsAntialias = false,
#pragma warning disable CS0618
                        FilterQuality = SKFilterQuality.None,
#pragma warning restore CS0618
                        Shader = shader
                    };

                    canvas.DrawVertices(SKVertexMode.Triangles, positions, texs, colors, paint);
                }
            }
        }
    }
}
```

### `MainSrc\Tools\Player3DConfig\Pivot3DRenderer.cs`

```csharp
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Project.Launch.Tools.Player3DConfig
{
    public class ProjectedFace
    {
        public float X0, Y0, X1, Y1, X2, Y2, X3, Y3;
        public Vec2 T0, T1, T2, T3;
        public float Depth;
        public float Shade;
        public bool Overlay;
    }

    public static class Pivot3DRenderer
    {
        public static List<ProjectedFace> Render(
            List<SkinFace> faces,
            float yawDeg, float pitchDeg,
            float viewW, float viewH,
            float scaleFactor = 0.75f)
        {
            var result = new List<ProjectedFace>(faces.Count);
            if (faces.Count == 0 || viewW <= 0 || viewH <= 0) return result;

            var center = new Vector3(0f, 16f, 0f);
            const float cameraDist = 60f;
            float scale = viewH * scaleFactor / 32f;
            float cx = viewW / 2f;
            float cy = viewH / 2f;

            float yawRad = yawDeg * MathF.PI / 180f;
            float pitchRad = pitchDeg * MathF.PI / 180f;
            float cosY = MathF.Cos(yawRad), sinY = MathF.Sin(yawRad);
            float cosP = MathF.Cos(pitchRad), sinP = MathF.Sin(pitchRad);

            foreach (var f in faces)
            {
                var p0 = Transform(f.V0, center, cosY, sinY, cosP, sinP, cameraDist, scale, cx, cy);
                var p1 = Transform(f.V1, center, cosY, sinY, cosP, sinP, cameraDist, scale, cx, cy);
                var p2 = Transform(f.V2, center, cosY, sinY, cosP, sinP, cameraDist, scale, cx, cy);
                var p3 = Transform(f.V3, center, cosY, sinY, cosP, sinP, cameraDist, scale, cx, cy);

                float avgZ = (p0.Z + p1.Z + p2.Z + p3.Z) * 0.25f;

                // 背面剔除（逆时针为正面）
                float cross = (p1.X - p0.X) * (p2.Y - p0.Y) - (p1.Y - p0.Y) * (p2.X - p0.X);
                if (cross <= 0f) continue;

                float shade = EstimateShade(f, cosY, sinY, cosP, sinP);

                result.Add(new ProjectedFace
                {
                    X0 = p0.X, Y0 = p0.Y,
                    X1 = p1.X, Y1 = p1.Y,
                    X2 = p2.X, Y2 = p2.Y,
                    X3 = p3.X, Y3 = p3.Y,
                    T0 = f.T0, T1 = f.T1, T2 = f.T2, T3 = f.T3,
                    Depth = avgZ,
                    Shade = shade,
                    Overlay = f.Overlay
                });
            }

            // 画家算法：远的先画，近的覆盖远的
            result.Sort((a, b) => b.Depth.CompareTo(a.Depth));
            return result;
        }

        private static Vector3 Transform(
            Vec3 v, Vector3 center,
            float cosY, float sinY, float cosP, float sinP,
            float cameraDist, float scale, float cx, float cy)
        {
            float x = v.X - center.X;
            float y = v.Y - center.Y;
            float z = v.Z - center.Z;

            // Yaw（绕 Y 轴）
            float x1 = x * cosY + z * sinY;
            float z1 = -x * sinY + z * cosY;

            // Pitch（绕 X 轴）
            float y1 = y * cosP - z1 * sinP;
            float z2 = y * sinP + z1 * cosP;

            // ★ 关键修正：相机在 +Z 方向看向 -Z，+Z 面（正脸）离相机更近
            float depth = cameraDist - z2;
            if (depth < 1f) depth = 1f;
            float persp = cameraDist / depth;

            float sx = cx + x1 * scale * persp;
            float sy = cy - y1 * scale * persp;

            return new Vector3(sx, sy, depth);
        }

        private static float EstimateShade(
            SkinFace f, float cosY, float sinY, float cosP, float sinP)
        {
            // 面法线（叉乘两条边）
            float ax = f.V1.X - f.V0.X, ay = f.V1.Y - f.V0.Y, az = f.V1.Z - f.V0.Z;
            float bx = f.V2.X - f.V0.X, by = f.V2.Y - f.V0.Y, bz = f.V2.Z - f.V0.Z;
            float nx = ay * bz - az * by;
            float ny = az * bx - ax * bz;
            float nz = ax * by - ay * bx;

            float len = MathF.Sqrt(nx * nx + ny * ny + nz * nz);
            if (len < 0.0001f) return 1.0f;

            // ★ 修复：模型面片缠绕方向给出的法线朝内，取反得到真正朝外的几何法线
            nx = -nx / len;
            ny = -ny / len;
            nz = -nz / len;

            // 光照方向（世界空间 / 相机空间）：从相机方向略偏左上
            float lx = 0.3f, ly = 0.6f, lz = 0.7f;
            float llen = MathF.Sqrt(lx * lx + ly * ly + lz * lz);
            lx /= llen; ly /= llen; lz /= llen;

            // ★ 修复：把光照方向从相机空间变换回模型空间（逆 Pitch 再逆 Yaw），
            //   让"光源跟着相机走"——人物转到哪个角度，朝向相机的一面始终被照亮，
            //   不再出现转到某个角度就死黑一片的情况
            float ly1 = ly * cosP + lz * sinP;
            float lz1 = -ly * sinP + lz * cosP;
            float lxm = lx * cosY - lz1 * sinY;
            float lzm = lx * sinY + lz1 * cosY;

            float dot = nx * lxm + ny * ly1 + nz * lzm;

            // 环境光 0.6 + 漫反射
            return 0.6f + 0.4f * MathF.Max(0f, dot);
        }
    }
}
```


## 6.7 MainSrc\Tools 其余（AnimSettings / AppConstants / LogsConfig / SoundControl）

### `MainSrc\Tools\AnimSettings\AnimationConfig.cs`

```csharp
using System;

namespace Project.Launch.Tools.AnimSettings
{
    /// <summary>
    /// 全局动画参数配置
    /// 后期"设置"页面只需修改这里的属性值即可统一调整动画手感
    /// </summary>
    public static class AnimationConfig
    {
        // ============================================================
        // LoginWindow 登录滑块动画
        // ============================================================

        /// <summary>登录滑块移动总时长</summary>
        public static TimeSpan SliderMoveDuration { get; set; } = TimeSpan.FromMilliseconds(700);

        /// <summary>登录滑块放大峰值倍数</summary>
        public static double SliderPeakScale { get; set; } = 1.2;

        /// <summary>梯形曲线上升段结束位置</summary>
        public static double SliderScaleRiseEnd { get; set; } = 0.25;

        /// <summary>梯形曲线下降段开始位置</summary>
        public static double SliderScaleFallStart { get; set; } = 0.75;

        /// <summary>登录滑块最大模糊值</summary>
        public static double SliderBlurPeak { get; set; } = 1.4;

        /// <summary>登录滑块宽度</summary>
        public static double SliderWidth { get; set; } = 114;

        /// <summary>登录滑块高度</summary>
        public static double SliderHeight { get; set; } = 36;

        // ============================================================
        // LoginWindow 输入面板淡出淡入
        // ============================================================

        /// <summary>输入面板淡出时长</summary>
        public static TimeSpan InputPanelFadeDuration { get; set; } = TimeSpan.FromMilliseconds(250);

        // ============================================================
        // MainWindow 底部导航滑块动画
        // 曲线形状与 LoginWindow 完全一致（EaseInOut + 梯形缩放 + 三角形模糊）
        // ============================================================

        /// <summary>导航滑块移动总时长</summary>
        public static TimeSpan TabMoveDuration { get; set; } = TimeSpan.FromMilliseconds(700);

        /// <summary>导航滑块放大峰值倍数</summary>
        public static double TabPeakScale { get; set; } = 1.30;

        /// <summary>导航滑块梯形曲线上升段结束位置</summary>
        public static double TabScaleRiseEnd { get; set; } = 0.25;

        /// <summary>导航滑块梯形曲线下降段开始位置</summary>
        public static double TabScaleFallStart { get; set; } = 0.75;

        /// <summary>导航滑块最大模糊值</summary>
        public static double TabBlurPeak { get; set; } = 0.8;

        /// <summary>导航滑块宽度（用于缩放中心补偿）</summary>
        public static double TabWidth { get; set; } = 96;

        /// <summary>导航滑块高度（用于缩放中心补偿）</summary>
        public static double TabHeight { get; set; } = 44;

        /// <summary>导航步长（每格宽度）</summary>
        public static double TabStep { get; set; } = 104;

        // ============================================================
        // MainWindow 页面切换淡入淡出
        // ============================================================

        /// <summary>页面切换淡出/淡入时长</summary>
        public static TimeSpan TabPanelFadeDuration { get; set; } = TimeSpan.FromMilliseconds(200);
    }
}
```

### `MainSrc\Tools\AppConstants\SetConstants.cs`

```csharp
namespace Project.Launch.Tools.AppConstants
{
    /// <summary>
    /// ★ 启动器全局常量 —— 版本号 / 应用名的**唯一来源**。
    ///
    /// 想改版本号或应用标题，**只改这个文件**，其余位置全部自动跟随：
    ///   · <see cref="Version"/>            → 登录窗左下角显示的版本号
    ///                                      + 传给 Minecraft 的 ${launcher_version}
    ///   · <see cref="AppName"/>             → 主窗标题（任务栏） + 自绘标题栏文字
    ///   · <see cref="DisplayName"/>         → 需要好看排版的展示名（带空格）
    ///   · <see cref="LoginHeader"/>         → 登录窗顶部大字（"登录 " + DisplayName）
    ///   · <see cref="LauncherName"/>        → 传给 Minecraft 的 ${launcher_name}（游戏日志里可见）
    ///
    /// 注意：这些是编译期常量（const），改完必须重新编译才生效。
    /// </summary>
    public static class SetConstants
    {
        // ============================================================
        //  ★★★ 只需修改下面这几行 ★★★
        // ============================================================

        /// <summary>版本号 —— 登录窗左下角显示；同时作为 Minecraft 的 ${launcher_version}</summary>
        public const string Version = "rc-8";

        /// <summary>应用名（不含空格）—— 主窗标题与自绘标题栏文字</summary>
        public const string AppName = "KML!Launcherz";

        // ============================================================
        //  以下由上面派生，通常不需要改
        // ============================================================

        /// <summary>展示名（含空格，排版更好看）</summary>
        public const string DisplayName = "Kilim Minecraft Launcher";

        /// <summary>登录窗顶部大字</summary>
        public const string LoginHeader = "登录 " + DisplayName;

        /// <summary>传给 Minecraft 的 ${launcher_name}（显示在游戏日志/崩溃报告中）</summary>
        public const string LauncherName = "Kilim";
    }
}
```

### `MainSrc\Tools\LogsConfig\LogHelper.cs`

```csharp
using System;
using System.IO;

namespace Project.Launch.Tools.LogsConfig
{
    /// <summary>
    /// 统一日志写入工具（中英双语）
    /// Unified log writer (Chinese + English)
    /// </summary>
    public static class LogHelper
    {
        private static readonly object _lock = new object();

        /// <summary>
        /// 写入一条日志
        /// Write a log entry
        /// </summary>
        /// <param name="filePath">日志文件路径 / Log file path</param>
        /// <param name="zh">中文描述 / Chinese description</param>
        /// <param name="en">英文描述 / English description</param>
        public static void Write(string filePath, string zh, string en)
        {
            try
            {
                lock (_lock)
                {
                    string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - [{zh}] {en}\n";
                    File.AppendAllText(filePath, line);
                }
            }
            catch { }
        }

        /// <summary>
        /// 清空并写入首行标题
        /// Clear and write a header line
        /// </summary>
        public static void Reset(string filePath, string zhHeader, string enHeader)
        {
            try
            {
                lock (_lock)
                {
                    string header = $"=== {zhHeader} / {enHeader} - {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===\n";
                    File.WriteAllText(filePath, header);
                }
            }
            catch { }
        }
    }
}
```

### `MainSrc\Tools\SoundControl\SoundConfig.cs`

```csharp
using System.Collections.Generic;

namespace Project.Launch.Tools.SoundControl
{
    /// <summary>
    /// 音效配置——逻辑名直接映射到文件名
    /// Sound configuration - logical name maps directly to filename
    /// </summary>
    public static class SoundConfig
    {
        /// <summary>
        /// 音频资源根路径（末尾要带斜杠）
        /// Asset root path (trailing slash required)
        /// </summary>
        public const string AssetRoot = "avares://Project.Launch/MainSrc/Sound/";

        /// <summary>
        /// 全局音量倍率（0.0 ~ 1.0）
        /// Global volume multiplier (0.0 ~ 1.0)
        /// </summary>
        public static double MasterVolume { get; set; } = 0.7;

        /// <summary>
        /// 总开关：false 时所有 Play 直接返回
        /// Master switch: when false, Play returns immediately
        /// </summary>
        public static bool Enabled { get; set; } = true;

        /// <summary>
        /// 单个音效的音量覆盖（乘在 MasterVolume 之上）
        /// Per-sound volume override (multiplied on top of MasterVolume)
        /// 未列出的音效默认 1.0
        /// </summary>
        public static readonly Dictionary<string, double> VolumeOverrides = new()
        {
            { "Open",         0.7 },
            { "DownloadDone", 0.9 },
            { "Messages",     0.8 },
            { "Messages2",    0.8 },
            { "Errors",       1.0 },
            { "BigError",     1.0 },
        };

        /// <summary>
        /// 取某个音效的最终音量（已乘 MasterVolume）
        /// Get final volume for a sound (MasterVolume already applied)
        /// </summary>
        public static double GetVolume(string name)
        {
            double perSound = VolumeOverrides.TryGetValue(name, out var v) ? v : 1.0;
            return MasterVolume * perSound;
        }
    }
}
```

### `MainSrc\Tools\SoundControl\SoundService.cs`

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Platform;
using NAudio.Wave;

using Project.Launch.Tools.JavaConfig;
using Project.Launch.Tools.LogsConfig;
namespace Project.Launch.Tools.SoundControl
{
    /// <summary>
    /// 音效播放服务——从内嵌资源读取 wav
    /// Sound playback service - reads wav from embedded resources
    /// </summary>
    public static class SoundService
    {
        private static readonly List<WaveOutEvent> _active = new();
        private static readonly object _lock = new();

        /// <summary>
        /// 播放音效（逻辑名 == 文件名，不含 .wav）
        /// Play a sound (logical name == filename, no .wav extension)
        /// 注意：内部走后台线程，不会阻塞 UI
        /// </summary>
        public static void Play(string name)
        {
            if (!SoundConfig.Enabled) return;
            if (string.IsNullOrWhiteSpace(name)) return;

            // ★ 音频设备初始化是同步 IO（100~300ms），放后台线程避免卡 UI
            Task.Run(() => PlayInternal(name));
        }

        private static void PlayInternal(string name)
        {
            try
            {
                string uriStr = SoundConfig.AssetRoot + name + ".wav";
                using var assetStream = AssetLoader.Open(new Uri(uriStr));

                // NAudio 的 WaveFileReader 需要可 Seek 的流，先拷进内存
                var memory = new MemoryStream();
                assetStream.CopyTo(memory);
                memory.Position = 0;

                var reader = new WaveFileReader(memory);
                var output = new WaveOutEvent
                {
                    Volume = (float)Math.Clamp(SoundConfig.GetVolume(name), 0.0, 1.0)
                };
                output.Init(reader);

                output.PlaybackStopped += (s, e) =>
                {
                    lock (_lock) _active.Remove(output);
                    try { output.Dispose(); } catch { }
                    try { reader.Dispose(); } catch { }
                    try { memory.Dispose(); } catch { }
                };

                lock (_lock) _active.Add(output);

                output.Play();
            }
            catch (Exception ex)
            {
                LogHelper.Write(LauncherPaths.AppLog,
                    $"播放音效失败 ({name}): {ex.Message}",
                    $"Failed to play sound ({name}): {ex.Message}");
            }
        }

        /// <summary>
        /// 停止全部正在播放的音效
        /// Stop all currently playing sounds
        /// </summary>
        public static void StopAll()
        {
            lock (_lock)
            {
                foreach (var o in _active)
                {
                    try { o.Stop(); } catch { }
                }
                _active.Clear();
            }
        }
    }
}
```


## 6.8 MainSrc\Tools\LiquidGlassAvaloniaUI —— 液态玻璃库（vendored, MIT）

### `MainSrc\Tools\LiquidGlassAvaloniaUI\LiquidGlassAppBuilderExtensions.cs`

```csharp
using Avalonia;
using Avalonia.Rendering.Composition;

namespace LiquidGlassAvaloniaUI
{
    public static class LiquidGlassAppBuilderExtensions
    {
        public static AppBuilder UseLiquidGlassPerformanceDefaults(
            this AppBuilder builder,
            long skiaMaxGpuResourceSizeBytes = 256L * 1024L * 1024L,
            int maxDirtyRects = 8)
        {
            return builder
                .With(new CompositionOptions
                {
                    UseRegionDirtyRectClipping = true,
                    MaxDirtyRects = maxDirtyRects
                })
                .With(new SkiaOptions
                {
                    MaxGpuResourceSizeBytes = skiaMaxGpuResourceSizeBytes
                });
        }
    }
}
```

### `MainSrc\Tools\LiquidGlassAvaloniaUI\LiquidGlassAvaloniaUI.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">

    <PropertyGroup>
      <TargetFramework>net8.0</TargetFramework>
      <LangVersion>9</LangVersion>
      <Nullable>enable</Nullable>
      <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
      <PackageId>LiquidGlassAvaloniaUI</PackageId>
      <Authors>KaranocaVe</Authors>
      <Description>AvaloniaUI liquid-glass backdrop and lens effects powered by Skia.</Description>
      <PackageLicenseExpression>MIT</PackageLicenseExpression>
      <PackageReadmeFile>README.md</PackageReadmeFile>
      <RepositoryUrl>https://github.com/KaranocaVe/LiquidGlassAvaloniaUI</RepositoryUrl>
      <RepositoryType>git</RepositoryType>
    </PropertyGroup>

    <ItemGroup>
      <PackageReference Include="Avalonia" Version="12.0.1" />
      <PackageReference Include="Avalonia.Skia" Version="12.0.1" />
      <PackageReference Include="SkiaSharp" Version="3.119.3-preview.1.1" />
    </ItemGroup>

    <ItemGroup>
      <None Include="..\README.md" Pack="true" PackagePath="\" />
      <None Remove="Assets\Shaders\LiquidGlassShader.sksl" />
      <AvaloniaResource Include="Assets\Shaders\LiquidGlassShader.sksl" />
      <None Remove="Assets\Shaders\LiquidGlassHighlight.sksl" />
      <AvaloniaResource Include="Assets\Shaders\LiquidGlassHighlight.sksl" />
      <None Remove="Assets\Shaders\LiquidGlassInteractiveHighlight.sksl" />
      <AvaloniaResource Include="Assets\Shaders\LiquidGlassInteractiveHighlight.sksl" />
      <None Remove="Assets\Shaders\LiquidGlassGamma.sksl" />
      <AvaloniaResource Include="Assets\Shaders\LiquidGlassGamma.sksl" />
      <None Remove="Assets\Shaders\LiquidGlassProgressiveMask.sksl" />
      <AvaloniaResource Include="Assets\Shaders\LiquidGlassProgressiveMask.sksl" />
      <None Remove="Assets\Shaders\LiquidGlassBackdropTransform.sksl" />
      <AvaloniaResource Include="Assets\Shaders\LiquidGlassBackdropTransform.sksl" />
    </ItemGroup>

</Project>
```

### `MainSrc\Tools\LiquidGlassAvaloniaUI\LiquidGlassBackdrop.cs`

```csharp
using System;
using Avalonia;
using Avalonia.VisualTree;

namespace LiquidGlassAvaloniaUI
{
    public sealed class LiquidGlassBackdrop
    {
        private LiquidGlassBackdrop()
        {
        }

        public static readonly AttachedProperty<bool> IsExcludedFromCaptureProperty =
            AvaloniaProperty.RegisterAttached<LiquidGlassBackdrop, Visual, bool>(
                "IsExcludedFromCapture",
                false);

        public static bool GetIsExcludedFromCapture(Visual visual)
        {
            if (visual is null)
                throw new ArgumentNullException(nameof(visual));

            return visual.GetValue(IsExcludedFromCaptureProperty);
        }

        public static void SetIsExcludedFromCapture(Visual visual, bool value)
        {
            if (visual is null)
                throw new ArgumentNullException(nameof(visual));

            visual.SetValue(IsExcludedFromCaptureProperty, value);
        }
    }
}
```

### `MainSrc\Tools\LiquidGlassAvaloniaUI\LiquidGlassBackdropProvider.cs`

```csharp
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Rendering;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SkiaSharp;

namespace LiquidGlassAvaloniaUI
{
    internal static class LiquidGlassBackdropProvider
    {
        // Target capture rate while visuals are being rendered. (Avoids constant polling when idle.)
        private static readonly long s_minCaptureIntervalTicks = TimeSpan.FromMilliseconds(33).Ticks;

        private sealed class BackdropState
        {
            public bool CaptureQueued;
            public LiquidGlassBackdropSnapshot? Snapshot;
            public ulong SnapshotHash;
            public PixelSize SnapshotPixelSize;
            public PixelPoint SnapshotOriginInPixels;
            public double SnapshotScaling;
            public long LastCaptureTicksUtc;
            public bool HasLastClipRect;
            public Rect LastClipRect;
            public bool ForcePublishNextCapture;
            public bool HasSubscriberOnlyDirtyRect;
            public Rect SubscriberOnlyDirtyRect;
            public long SubscriberOnlyDirtyTicksUtc;
            public List<WeakReference<Control>> Subscribers { get; } = new();

            public RenderTargetBitmap? ScratchBitmap;

            public object? Renderer;
            public EventInfo? SceneInvalidatedEvent;
            public Delegate? SceneInvalidatedDelegate;
        }

        private static readonly ConditionalWeakTable<TopLevel, BackdropState> s_states = new();
        private static int s_captureDepth;
        private static PropertyInfo? s_topLevelRendererProperty;
        private static PropertyInfo? s_dirtyRectProperty;

        public static bool IsCapturing
        {
            get => s_captureDepth > 0;
        }

        public static LiquidGlassBackdropSnapshot? TryGetSnapshot(Control control)
        {
            TopLevel? topLevel = TopLevel.GetTopLevel(control);
            if (topLevel is null)
                return null;

            return s_states.TryGetValue(topLevel, out BackdropState? state)
                ? System.Threading.Volatile.Read(ref state.Snapshot)
                : null;
        }

        public static void EnsureSnapshot(Control control)
        {
            TopLevel? topLevel = TopLevel.GetTopLevel(control);
            if (topLevel is null)
                return;

            BackdropState? state = s_states.GetOrCreateValue(topLevel);
            TrackSubscriber(state, control);
            CleanupSubscribers(state);
            EnsureRendererSubscription(topLevel, state);

            double scaling = topLevel.RenderScaling;

            bool shouldCapture =
                state.Snapshot is null
                || !state.SnapshotScaling.Equals(scaling);

            // ★ 修复：渲染变换（TranslateTransform / ScaleTransform）由合成器直接处理，
            // Avalonia 的 Visual.RenderTransformChanged 只更新合成视觉的变换，不会重新录制
            // 绘制树，因此不会触发 SceneInvalidated。若只依赖该事件，快照会一直停在旧位置：
            // 只有快照覆盖范围内的那部分玻璃还有折射/模糊，越界处被 Clamp 钳制，表现为硬缝。
            // 这里把"当前所需覆盖区是否仍在上次抓取范围内"提升为一等触发条件。
            if (!shouldCapture
                && state.HasLastClipRect
                && !IsBackdropWithinLastClip(control, topLevel, state))
            {
                shouldCapture = true;
            }

            if (shouldCapture)
                QueueCapture(topLevel, state);
        }

        /// <summary>
        /// 控件当前的背景采样区（含折射/模糊膨胀量、并与客户区求交后）是否仍被上次抓取的裁剪区包含。
        /// 无法测量时返回 true（即不触发重抓），避免隐藏或尚未完成布局的控件造成每帧重抓。
        /// </summary>
        private static bool IsBackdropWithinLastClip(Control control, TopLevel topLevel, BackdropState state)
        {
            if (!TryCalculateControlBackdropBounds(control, topLevel, out Rect bounds))
                return true;

            Rect clipped = bounds.Intersect(new Rect(topLevel.ClientSize));
            if (clipped.Width <= 0 || clipped.Height <= 0)
                return true;

            return RectContains(state.LastClipRect, clipped);
        }

        public static void NotifySubscriberOnlyInvalidation(Control control)
        {
            TopLevel? topLevel = TopLevel.GetTopLevel(control);
            if (topLevel is null)
                return;

            if (!s_states.TryGetValue(topLevel, out BackdropState? state))
                return;

            if (!TryCalculateControlVisualBounds(control, topLevel, out Rect bounds))
                return;

            bounds = bounds.Inflate(4.0);
            long nowTicks = DateTime.UtcNow.Ticks;
            if (state.HasSubscriberOnlyDirtyRect && nowTicks - state.SubscriberOnlyDirtyTicksUtc < TimeSpan.FromMilliseconds(100).Ticks)
                state.SubscriberOnlyDirtyRect = state.SubscriberOnlyDirtyRect.Union(bounds);
            else
                state.SubscriberOnlyDirtyRect = bounds;

            state.HasSubscriberOnlyDirtyRect = true;
            state.SubscriberOnlyDirtyTicksUtc = nowTicks;
        }

        private static void CleanupSubscribers(BackdropState state)
        {
            for (int i = state.Subscribers.Count - 1; i >= 0; i--)
            {
                if (!state.Subscribers[i].TryGetTarget(out _))
                    state.Subscribers.RemoveAt(i);
            }
        }

        private static void QueueCapture(TopLevel topLevel, BackdropState state)
        {
            LiquidGlassDiagnostics.RecordCaptureQueueRequest();

            if (state.CaptureQueued)
            {
                LiquidGlassDiagnostics.RecordCaptureQueueCoalesced();
                return;
            }

            state.CaptureQueued = true;

            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Post(() => ScheduleCaptureOnNextFrame(topLevel, state), DispatcherPriority.Background);
                return;
            }

            ScheduleCaptureOnNextFrame(topLevel, state);
        }

        private static void ScheduleCaptureOnNextFrame(TopLevel topLevel, BackdropState state)
        {
            topLevel.RequestAnimationFrame(_ =>
            {
                state.CaptureQueued = false;
                Capture(topLevel, state);
            });
        }

        private static void Capture(TopLevel topLevel, BackdropState state)
        {
            if (IsCapturing)
            {
                LiquidGlassDiagnostics.RecordCaptureSkippedReentrant();
                return;
            }

            LiquidGlassDiagnostics.RecordCaptureStarted();

            CleanupSubscribers(state);
            if (state.Subscribers.Count == 0)
            {
                LiquidGlassDiagnostics.RecordCaptureSkippedNoSubscribers();
                DetachRendererSubscription(state);
                DisposeAllSnapshots(state);
                state.ScratchBitmap?.Dispose();
                state.ScratchBitmap = null;
                return;
            }

            double scaling = topLevel.RenderScaling;
            BackdropClip clip = CalculateBackdropClip(topLevel, state, scaling);
            if (clip.DipRect.Width <= 0 || clip.DipRect.Height <= 0)
            {
                LiquidGlassDiagnostics.RecordCaptureSkippedEmptyClip();
                return;
            }

            PixelSize pixelSize = clip.PixelRect.Size;
            state.LastClipRect = clip.DipRect;
            state.HasLastClipRect = true;

            s_captureDepth++;
            try
            {
                Vector dpi = new(96 * scaling, 96 * scaling);

                // ★★★ 根因修复（验证中）★★★
                // 原实现用手写遍历 (LiquidGlassVisualRenderer) 把视觉树画进一个"裁剪区尺寸"的
                // RenderTargetBitmap，实测该路径在真实窗口下产出的是【全透明空图】(336x193 全部 alpha=0)，
                // 于是玻璃采样到空背景 —— 表现为"看不到折射/模糊、却能直接看穿到下面的 UI"。
                // 这里改用 Avalonia 自带渲染器 RenderTargetBitmap.Render(Visual)：
                //   · 位图取【整窗客户区】尺寸，原点固定 (0,0)，因此不需要任何手写平移；
                //   · 玻璃控件自身在 Render() 里因 IsCapturing 为真而不绘制，天然被排除，不会递归。
                PixelSize fullPx = new(
                    Math.Max(1, (int)Math.Ceiling(topLevel.ClientSize.Width * scaling)),
                    Math.Max(1, (int)Math.Ceiling(topLevel.ClientSize.Height * scaling)));

                if (state.ScratchBitmap is null
                    || state.ScratchBitmap.PixelSize != fullPx
                    || !state.SnapshotScaling.Equals(scaling))
                {
                    state.ScratchBitmap?.Dispose();
                    state.ScratchBitmap = new RenderTargetBitmap(fullPx, dpi);
                    LiquidGlassDiagnostics.RecordScratchBitmapRecreated();
                }

                long nowTicks = DateTime.UtcNow.Ticks;
                state.ScratchBitmap.Render(topLevel);

                pixelSize = fullPx;
                PixelPoint originInPixels = new(0, 0);

                LiquidGlassBackdropSnapshot? currentSnapshot = System.Threading.Volatile.Read(ref state.Snapshot);
                bool isSameConfig =
                    currentSnapshot is not null
                    && state.SnapshotScaling.Equals(scaling)
                    && state.SnapshotPixelSize == pixelSize
                    && state.SnapshotOriginInPixels == originInPixels;

                bool forcePublish = state.ForcePublishNextCapture;
                state.ForcePublishNextCapture = false;

                (SKImage? snapshotImage, ulong hash) = CreateSkImageWithHash(state.ScratchBitmap, isSameConfig && !forcePublish, state.SnapshotHash);

                if (!forcePublish && isSameConfig && state.SnapshotHash == hash)
                {
                    state.LastCaptureTicksUtc = nowTicks;
                    LiquidGlassDiagnostics.RecordCaptureSkippedByHash();
                    return;
                }

                if (snapshotImage is null)
                    return;

                LiquidGlassBackdropSnapshot snapshot = new(snapshotImage, originInPixels, pixelSize, scaling);
                state.SnapshotHash = hash;
                state.SnapshotPixelSize = pixelSize;
                state.SnapshotOriginInPixels = originInPixels;
                state.SnapshotScaling = scaling;
                state.LastCaptureTicksUtc = nowTicks;

                // Publish the new snapshot before disposing the old one. Disposing first can leave a short
                // window where the render thread observes a disposed snapshot and falls back to a white fill.
                System.Threading.Volatile.Write(ref state.Snapshot, snapshot);
                currentSnapshot?.RequestDispose();
                LiquidGlassDiagnostics.RecordCapturePublished();
            }
            finally
            {
                s_captureDepth--;
            }

            InvalidateSubscribers(state);
        }

        private static void EnsureRendererSubscription(TopLevel topLevel, BackdropState state)
        {
            if (state.SceneInvalidatedDelegate is not null)
                return;

            object? renderer = GetRenderer(topLevel);
            if (renderer is null)
                return;

            EventInfo? evt = renderer.GetType().GetEvent("SceneInvalidated", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (evt is null)
                return;

            EventHandler<SceneInvalidatedEventArgs> handler = (_, args) =>
            {
                LiquidGlassDiagnostics.RecordRendererInvalidation();

                if (IsCapturing)
                    return;

                if (!topLevel.IsVisible)
                    return;

                bool hasDirtyRect = TryGetDirtyRect(args, out Rect dirtyRect);
                if (hasDirtyRect)
                    LiquidGlassDiagnostics.RecordDirtyRectAvailable();
                else
                    LiquidGlassDiagnostics.RecordDirtyRectUnavailable();

                Dispatcher.UIThread.Post(() =>
                {
                    if (IsCapturing)
                        return;

                    if (!topLevel.IsVisible)
                        return;

                    if (!s_states.TryGetValue(topLevel, out BackdropState? current) || !ReferenceEquals(current, state))
                        return;

                    CleanupSubscribers(state);
                    if (state.Subscribers.Count == 0)
                    {
                        DetachRendererSubscription(state);
                        DisposeAllSnapshots(state);
                        state.ScratchBitmap?.Dispose();
                        state.ScratchBitmap = null;
                        return;
                    }

                    // If the required clip (inflated by blur/refraction) grows beyond the last capture,
                    // don't wait for the cadence timer — otherwise interactive slider drags can render
                    // with an undersized snapshot and appear to "shift" as the blur kernel clamps.
                    double scaling = topLevel.RenderScaling;
                    BackdropClip desiredClip = CalculateBackdropClip(topLevel, state, scaling);
                    bool needsClipGrowthCapture =
                        desiredClip.DipRect.Width > 0
                        && desiredClip.DipRect.Height > 0
                        && (!state.HasLastClipRect || !RectContains(state.LastClipRect, desiredClip.DipRect));

                    long nowTicks = DateTime.UtcNow.Ticks;
                    if (!needsClipGrowthCapture && nowTicks - state.LastCaptureTicksUtc < s_minCaptureIntervalTicks)
                    {
                        LiquidGlassDiagnostics.RecordCaptureSkippedByCadence();
                        return;
                    }

                    bool forcePublishNextCapture = false;
                    if (!needsClipGrowthCapture && hasDirtyRect && state.HasLastClipRect)
                    {
                        if (!dirtyRect.Intersects(state.LastClipRect))
                        {
                            LiquidGlassDiagnostics.RecordCaptureSkippedByDirtyRect();
                            return;
                        }

                        if (IsKnownSubscriberOnlyDirtyRect(state, dirtyRect, nowTicks))
                        {
                            LiquidGlassDiagnostics.RecordCaptureSkippedByDirtyRect();
                            return;
                        }

                        forcePublishNextCapture = true;
                    }

                    if (forcePublishNextCapture)
                        state.ForcePublishNextCapture = true;

                    QueueCapture(topLevel, state);
                }, DispatcherPriority.Background);
            };

            state.Renderer = renderer;
            state.SceneInvalidatedEvent = evt;
            state.SceneInvalidatedDelegate = handler;
            evt.AddEventHandler(renderer, handler);
        }

        private static void DetachRendererSubscription(BackdropState state)
        {
            if (state.Renderer is null || state.SceneInvalidatedEvent is null || state.SceneInvalidatedDelegate is null)
            {
                state.Renderer = null;
                state.SceneInvalidatedEvent = null;
                state.SceneInvalidatedDelegate = null;
                return;
            }

            state.SceneInvalidatedEvent.RemoveEventHandler(state.Renderer, state.SceneInvalidatedDelegate);
            state.Renderer = null;
            state.SceneInvalidatedEvent = null;
            state.SceneInvalidatedDelegate = null;
        }

        private static bool RectContains(Rect outer, Rect inner)
        {
            return inner.X >= outer.X
                   && inner.Y >= outer.Y
                   && inner.Right <= outer.Right
                   && inner.Bottom <= outer.Bottom;
        }

        private static bool IsKnownSubscriberOnlyDirtyRect(BackdropState state, Rect dirtyRect, long nowTicks)
        {
            if (!state.HasSubscriberOnlyDirtyRect)
                return false;

            if (nowTicks - state.SubscriberOnlyDirtyTicksUtc > TimeSpan.FromMilliseconds(100).Ticks)
            {
                state.HasSubscriberOnlyDirtyRect = false;
                state.SubscriberOnlyDirtyRect = default;
                state.SubscriberOnlyDirtyTicksUtc = 0;
                return false;
            }

            if (!RectContains(state.SubscriberOnlyDirtyRect, dirtyRect))
                return false;

            state.HasSubscriberOnlyDirtyRect = false;
            state.SubscriberOnlyDirtyRect = default;
            state.SubscriberOnlyDirtyTicksUtc = 0;
            return true;
        }

        private static BackdropClip CalculateBackdropClip(TopLevel topLevel, BackdropState state, double scaling)
        {
            Rect? union = null;
            TopLevel root = topLevel;
            Rect clientRect = new(topLevel.ClientSize);

            for (int i = state.Subscribers.Count - 1; i >= 0; i--)
            {
                if (!state.Subscribers[i].TryGetTarget(out Control? control))
                {
                    state.Subscribers.RemoveAt(i);
                    continue;
                }

                if (!TryCalculateControlBackdropBounds(control, root, out Rect globalBounds))
                    continue;

                union = union is null ? globalBounds : union.Value.Union(globalBounds);
            }

            if (union is null)
                return default;

            Rect clip = union.Value.Intersect(clientRect);
            if (clip.Width <= 0 || clip.Height <= 0)
                return default;

            // Snap to pixel grid to keep shader sampling stable and avoid shimmering on fractional DPI.
            int pixelLeft = (int)Math.Floor(clip.X * scaling);
            int pixelTop = (int)Math.Floor(clip.Y * scaling);
            int pixelRight = (int)Math.Ceiling(clip.Right * scaling);
            int pixelBottom = (int)Math.Ceiling(clip.Bottom * scaling);
            PixelRect pixelRect = new(new PixelPoint(pixelLeft, pixelTop), new PixelPoint(pixelRight, pixelBottom));

            Rect dipRect = new(
                pixelRect.X / scaling,
                pixelRect.Y / scaling,
                pixelRect.Width / scaling,
                pixelRect.Height / scaling);

            return new BackdropClip(dipRect, pixelRect);
        }

        private static bool TryCalculateControlBackdropBounds(Control control, TopLevel root, out Rect bounds)
        {
            bounds = default;

            if (!TryCalculateControlVisualBounds(control, root, out Rect globalBounds))
                return false;

            // Inflate by an approximate sampling margin (blur + refraction). We scale the inflation
            // by the visual's render transform to keep it conservative for interactive transforms.
            double localInflate = GetBackdropInflate(control);
            TransformedBounds? transformed = control.GetTransformedBounds();
            if (transformed is null)
                return false;

            double scaleX = Math.Sqrt(transformed.Value.Transform.M11 * transformed.Value.Transform.M11 + transformed.Value.Transform.M21 * transformed.Value.Transform.M21);
            double scaleY = Math.Sqrt(transformed.Value.Transform.M12 * transformed.Value.Transform.M12 + transformed.Value.Transform.M22 * transformed.Value.Transform.M22);
            double inflateX = localInflate * Math.Max(1.0, scaleX);
            double inflateY = localInflate * Math.Max(1.0, scaleY);

            bounds = globalBounds.Inflate(new Thickness(inflateX, inflateY));
            return true;
        }

        private static bool TryCalculateControlVisualBounds(Control control, TopLevel root, out Rect bounds)
        {
            bounds = default;

            if (!control.IsVisible || control.Bounds.Width <= 0 || control.Bounds.Height <= 0)
                return false;

            if (!ReferenceEquals(TopLevel.GetTopLevel(control), root))
                return false;

            TransformedBounds? transformed = control.GetTransformedBounds();
            if (transformed is null)
                return false;

            bounds = transformed.Value.Bounds.TransformToAABB(transformed.Value.Transform);
            return true;
        }

        private readonly struct BackdropClip
        {
            public BackdropClip(Rect dipRect, PixelRect pixelRect)
            {
                DipRect = dipRect;
                PixelRect = pixelRect;
            }

            public Rect DipRect { get; }

            public PixelRect PixelRect { get; }
        }

        private static double GetBackdropInflate(Control control)
        {
            // Conservative but small default: enough for 2 DIP blur + 24 DIP refraction.
            const double minInflate = 32.0;

            switch (control)
            {
                case LiquidGlassSurface surface:
                    // Blur is applied as a Gaussian (sigma ~= BlurRadius), so sampling reaches ~3*sigma.
                    double zoomValue = surface.BackdropZoom;
                    if (zoomValue <= 0.0005 || double.IsNaN(zoomValue) || double.IsInfinity(zoomValue))
                        zoomValue = 1.0;

                    double zoom = Clamp(zoomValue, 0.1, 10.0);

                    // BackdropTransform can shift sampling outside the visible bounds (offset / zoom),
                    // and zoom < 1 expands the sampled region further beyond the control bounds.
                    Vector offset = surface.BackdropOffset;
                    double offsetMargin = Math.Max(Math.Abs(offset.X), Math.Abs(offset.Y)) / zoom;

                    double zoomOutMargin = 0.0;
                    if (zoom < 1.0)
                    {
                        double halfMaxSize = Math.Max(surface.Bounds.Width, surface.Bounds.Height) * 0.5;
                        zoomOutMargin = (1.0 / zoom - 1.0) * halfMaxSize;
                    }

                    // Chromatic aberration can sample up to ~2x refractionAmount in the worst case,
                    // so capture a wider border to avoid clamping artifacts.
                    double refractionMargin = Math.Abs(surface.RefractionAmount) * (surface.ChromaticAberration ? 2.0 : 1.0);

                    return Math.Max(
                        minInflate,
                        refractionMargin + surface.BlurRadius * 3.0 + 6.0 + offsetMargin + zoomOutMargin);
                default:
                    return minInflate;
            }
        }

        private static double Clamp(double value, double min, double max)
        {
            if (value < min)
                return min;
            if (value > max)
                return max;
            return value;
        }

        private static HashSet<Visual> GetExcludedRoots(TopLevel topLevel, BackdropState state)
        {
            HashSet<Visual> excluded = new();

            for (int i = state.Subscribers.Count - 1; i >= 0; i--)
            {
                if (!state.Subscribers[i].TryGetTarget(out Control? control))
                    continue;

                if (!control.IsVisible)
                    continue;

                if (!ReferenceEquals(TopLevel.GetTopLevel(control), topLevel))
                    continue;

                excluded.Add(control);
            }

            return excluded;
        }

        private static bool TryGetDirtyRect(object args, out Rect dirtyRect)
        {
            dirtyRect = default;
            if (args is null)
                return false;

            if (s_dirtyRectProperty is null || s_dirtyRectProperty.DeclaringType != args.GetType())
            {
                s_dirtyRectProperty = args.GetType().GetProperty(
                    "DirtyRect",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            }

            if (s_dirtyRectProperty?.PropertyType != typeof(Rect))
                return false;

            if (s_dirtyRectProperty.GetValue(args) is not Rect value)
                return false;

            dirtyRect = value;
            return true;
        }

        private static object? GetRenderer(TopLevel topLevel)
        {
            if (s_topLevelRendererProperty is null)
            {
                s_topLevelRendererProperty = typeof(TopLevel).GetProperty(
                    "Renderer",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            }

            return s_topLevelRendererProperty?.GetValue(topLevel);
        }

        private static void RenderVisualWithClip(RenderTargetBitmap target, Visual visual, Rect clipRect, ISet<Visual>? excludedRoots)
        {
            using DrawingContext ctx = target.CreateDrawingContext();
            LiquidGlassVisualRenderer.Render(ctx, visual, clipRect, excludedRoots);
        }

        private static void TrackSubscriber(BackdropState state, Control control)
        {
            for (int i = state.Subscribers.Count - 1; i >= 0; i--)
            {
                if (!state.Subscribers[i].TryGetTarget(out Control? existing))
                {
                    state.Subscribers.RemoveAt(i);
                    continue;
                }

                if (ReferenceEquals(existing, control))
                    return;
            }

            state.Subscribers.Add(new WeakReference<Control>(control));
        }

        private static void DisposeAllSnapshots(BackdropState state)
        {
            System.Threading.Volatile.Read(ref state.Snapshot)?.RequestDispose();

            state.SnapshotHash = 0;
            state.SnapshotPixelSize = default;
            state.SnapshotOriginInPixels = default;
            state.SnapshotScaling = 0;
            System.Threading.Volatile.Write(ref state.Snapshot, null);
            state.HasLastClipRect = false;
            state.LastClipRect = default;
            state.ForcePublishNextCapture = false;
            state.HasSubscriberOnlyDirtyRect = false;
            state.SubscriberOnlyDirtyRect = default;
            state.SubscriberOnlyDirtyTicksUtc = 0;
        }

        private static void InvalidateSubscribers(BackdropState state)
        {
            for (int i = state.Subscribers.Count - 1; i >= 0; i--)
            {
                if (!state.Subscribers[i].TryGetTarget(out Control? control))
                {
                    state.Subscribers.RemoveAt(i);
                    continue;
                }

                control.InvalidateVisual();
                LiquidGlassDiagnostics.RecordSubscriberInvalidation();
            }
        }

        private static (SKImage? image, ulong hash) CreateSkImageWithHash(Bitmap bitmap, bool isSameConfig, ulong previousHash)
        {
            PixelFormat format = bitmap.Format ?? throw new NotSupportedException("Bitmap pixel format is not readable.");
            AlphaFormat alpha = bitmap.AlphaFormat ?? throw new NotSupportedException("Bitmap alpha format is not readable.");

            SKColorType colorType = format == PixelFormat.Bgra8888 ? SKColorType.Bgra8888 :
                format == PixelFormat.Rgba8888 ? SKColorType.Rgba8888 :
                throw new NotSupportedException($"Unsupported pixel format: {format}");

            SKAlphaType alphaType = alpha == AlphaFormat.Premul ? SKAlphaType.Premul :
                alpha == AlphaFormat.Unpremul ? SKAlphaType.Unpremul :
                SKAlphaType.Unpremul;

            SKImageInfo info = new(bitmap.PixelSize.Width, bitmap.PixelSize.Height, colorType, alphaType);

            if (isSameConfig)
            {
                // The fingerprint intentionally samples an 8x8 grid. For unchanged static scenes this avoids
                // copying the full rendered backdrop just to discover that the current snapshot is still valid.
                ulong sampledHash = ComputeBackdropHash(bitmap, info.BytesPerPixel, info.Width, info.Height);
                if (sampledHash == previousHash)
                    return (null, sampledHash);

                return (CreateSkImageFromBitmap(bitmap, info), sampledHash);
            }

            return CreateSkImageWithHashFromFullCopy(bitmap, info);
        }

        private static (SKImage? image, ulong hash) CreateSkImageWithHashFromFullCopy(Bitmap bitmap, SKImageInfo info)
        {
            int rowBytes = info.Width * info.BytesPerPixel;
            int length = rowBytes * info.Height;
            byte[]? bytes = ArrayPool<byte>.Shared.Rent(length);

            unsafe
            {
                fixed (byte* ptr = bytes)
                {
                    bitmap.CopyPixels(new PixelRect(bitmap.PixelSize), (IntPtr)ptr, length, rowBytes);
                    LiquidGlassDiagnostics.RecordFullBitmapCopy(length);
                }
            }

            try
            {
                ulong hash = ComputeBackdropHash(bytes, rowBytes, info.Width, info.Height);
                return (SKImage.FromPixelCopy(info, bytes, rowBytes), hash);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(bytes);
            }
        }

        private static SKImage? CreateSkImageFromBitmap(Bitmap bitmap, SKImageInfo info)
        {
            int rowBytes = info.Width * info.BytesPerPixel;
            int length = rowBytes * info.Height;
            byte[]? bytes = ArrayPool<byte>.Shared.Rent(length);

            unsafe
            {
                fixed (byte* ptr = bytes)
                {
                    bitmap.CopyPixels(new PixelRect(bitmap.PixelSize), (IntPtr)ptr, length, rowBytes);
                    LiquidGlassDiagnostics.RecordFullBitmapCopy(length);
                }
            }

            try
            {
                return SKImage.FromPixelCopy(info, bytes, rowBytes);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(bytes);
            }
        }

        private static ulong ComputeBackdropHash(Bitmap bitmap, int bytesPerPixel, int width, int height)
        {
            const ulong fnvOffset = 14695981039346656037UL;
            const ulong fnvPrime = 1099511628211UL;

            ulong hash = fnvOffset;
            const int samplesX = 8;
            const int samplesY = 8;
            int maxX = Math.Max(1, width) - 1;
            int maxY = Math.Max(1, height) - 1;
            int rowBytes = width * bytesPerPixel;

            byte[]? row = ArrayPool<byte>.Shared.Rent(rowBytes);
            try
            {
                unsafe
                {
                    fixed (byte* ptr = row)
                    {
                        for (int sy = 0; sy < samplesY; sy++)
                        {
                            int y = samplesY == 1 ? 0 : (int)((long)sy * maxY / (samplesY - 1));
                            bitmap.CopyPixels(new PixelRect(0, y, width, 1), (IntPtr)ptr, rowBytes, rowBytes);
                            LiquidGlassDiagnostics.RecordSampledHash(rowBytes);

                            for (int sx = 0; sx < samplesX; sx++)
                            {
                                int x = samplesX == 1 ? 0 : (int)((long)sx * maxX / (samplesX - 1));
                                int offset = x * bytesPerPixel;

                                for (int i = 0; i < bytesPerPixel; i++)
                                    hash = (hash ^ row[offset + i]) * fnvPrime;
                            }
                        }
                    }
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(row);
            }

            hash = (hash ^ (ulong)width) * fnvPrime;
            hash = (hash ^ (ulong)height) * fnvPrime;
            return hash;
        }

        private static ulong ComputeBackdropHash(byte[] bytes, int rowBytes, int width, int height)
        {
            // Fast, stable "good enough" fingerprint (8x8 samples) to avoid redundant invalidations.
            const ulong fnvOffset = 14695981039346656037UL;
            const ulong fnvPrime = 1099511628211UL;

            ulong hash = fnvOffset;
            const int samplesX = 8;
            const int samplesY = 8;
            int maxX = Math.Max(1, width) - 1;
            int maxY = Math.Max(1, height) - 1;

            for (int sy = 0; sy < samplesY; sy++)
            {
                int y = samplesY == 1 ? 0 : (int)((long)sy * maxY / (samplesY - 1));
                int row = y * rowBytes;

                for (int sx = 0; sx < samplesX; sx++)
                {
                    int x = samplesX == 1 ? 0 : (int)((long)sx * maxX / (samplesX - 1));
                    int offset = row + x * 4;

                    for (int i = 0; i < 4; i++)
                    {
                        hash = (hash ^ bytes[offset + i]) * fnvPrime;
                    }
                }
            }

            hash = (hash ^ (ulong)width) * fnvPrime;
            hash = (hash ^ (ulong)height) * fnvPrime;
            return hash;
        }
    }
}
```

### `MainSrc\Tools\LiquidGlassAvaloniaUI\LiquidGlassBackdropSnapshot.cs`

```csharp
using System;
using System.Collections.Generic;
using Avalonia;
using SkiaSharp;

namespace LiquidGlassAvaloniaUI
{
    internal sealed class LiquidGlassBackdropSnapshot : IDisposable
    {
        internal readonly struct FilteredKey : IEquatable<FilteredKey>
        {
            public FilteredKey(
                int brightnessQ,
                int contrastQ,
                int saturationQ,
                int exposureEvQ,
                int opacityQ,
                int blurSigmaPxQ,
                int cropX,
                int cropY,
                int cropWidth,
                int cropHeight,
                int renderContextId)
            {
                BrightnessQ = brightnessQ;
                ContrastQ = contrastQ;
                SaturationQ = saturationQ;
                ExposureEvQ = exposureEvQ;
                OpacityQ = opacityQ;
                BlurSigmaPxQ = blurSigmaPxQ;
                CropX = cropX;
                CropY = cropY;
                CropWidth = cropWidth;
                CropHeight = cropHeight;
                RenderContextId = renderContextId;
            }

            public int BrightnessQ { get; }
            public int ContrastQ { get; }
            public int SaturationQ { get; }
            public int ExposureEvQ { get; }
            public int OpacityQ { get; }
            public int BlurSigmaPxQ { get; }
            public int CropX { get; }
            public int CropY { get; }
            public int CropWidth { get; }
            public int CropHeight { get; }
            public int RenderContextId { get; }

            public bool Equals(FilteredKey other)
            {
                return BrightnessQ == other.BrightnessQ
                       && ContrastQ == other.ContrastQ
                       && SaturationQ == other.SaturationQ
                       && ExposureEvQ == other.ExposureEvQ
                       && OpacityQ == other.OpacityQ
                       && BlurSigmaPxQ == other.BlurSigmaPxQ
                       && CropX == other.CropX
                       && CropY == other.CropY
                       && CropWidth == other.CropWidth
                       && CropHeight == other.CropHeight
                       && RenderContextId == other.RenderContextId;
            }

            public override bool Equals(object? obj)
            {
                return obj is FilteredKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hashCode = BrightnessQ;
                    hashCode = hashCode * 397 ^ ContrastQ;
                    hashCode = hashCode * 397 ^ SaturationQ;
                    hashCode = hashCode * 397 ^ ExposureEvQ;
                    hashCode = hashCode * 397 ^ OpacityQ;
                    hashCode = hashCode * 397 ^ BlurSigmaPxQ;
                    hashCode = hashCode * 397 ^ CropX;
                    hashCode = hashCode * 397 ^ CropY;
                    hashCode = hashCode * 397 ^ CropWidth;
                    hashCode = hashCode * 397 ^ CropHeight;
                    hashCode = hashCode * 397 ^ RenderContextId;
                    return hashCode;
                }
            }
        }

        internal readonly struct FilteredResult
        {
            public FilteredResult(SKImage image, PixelPoint originInPixels)
            {
                Image = image ?? throw new ArgumentNullException(nameof(image));
                OriginInPixels = originInPixels;
            }

            public SKImage Image { get; }

            public PixelPoint OriginInPixels { get; }
        }

        private int _leases;
        private int _disposeRequested;
        private int _disposed;
        private readonly object _filteredLock = new();
        private Dictionary<FilteredKey, FilteredResult>? _filtered;
        private Queue<FilteredKey>? _filteredOrder;
        private const int MaxFilteredEntries = 8;

        public LiquidGlassBackdropSnapshot(SKImage image, PixelPoint originInPixels, PixelSize pixelSize, double scaling)
        {
            Image = image ?? throw new ArgumentNullException(nameof(image));
            OriginInPixels = originInPixels;
            PixelSize = pixelSize;
            Scaling = scaling;
        }

        public SKImage Image { get; }

        /// <summary>
        /// Snapshot origin in device pixels (TopLevel coordinate space).
        /// </summary>
        public PixelPoint OriginInPixels { get; }

        public PixelSize PixelSize { get; }

        public double Scaling { get; }

        public bool TryGetFiltered(FilteredKey key, out FilteredResult result)
        {
            lock (_filteredLock)
            {
                if (_filtered is null)
                {
                    result = default;
                    return false;
                }

                return _filtered.TryGetValue(key, out result);
            }
        }

        public void StoreFiltered(FilteredKey key, FilteredResult result)
        {
            lock (_filteredLock)
            {
                _filtered ??= new Dictionary<FilteredKey, FilteredResult>();
                _filteredOrder ??= new Queue<FilteredKey>();

                if (_filtered.TryGetValue(key, out FilteredResult existing))
                {
                    existing.Image.Dispose();
                    _filtered[key] = result;
                    return;
                }

                _filtered[key] = result;
                _filteredOrder.Enqueue(key);

                while (_filtered.Count > MaxFilteredEntries && _filteredOrder.Count > 0)
                {
                    FilteredKey evictKey = _filteredOrder.Dequeue();
                    if (_filtered.TryGetValue(evictKey, out FilteredResult evictValue) && !evictKey.Equals(key))
                    {
                        _filtered.Remove(evictKey);
                        evictValue.Image.Dispose();
                    }
                }
            }
        }

        public bool TryAddLease()
        {
            while (true)
            {
                if (System.Threading.Volatile.Read(ref _disposed) != 0)
                    return false;

                int current = System.Threading.Volatile.Read(ref _leases);
                if (System.Threading.Interlocked.CompareExchange(ref _leases, current + 1, current) == current)
                {
                    if (System.Threading.Volatile.Read(ref _disposed) != 0)
                    {
                        ReleaseLease();
                        return false;
                    }

                    return true;
                }
            }
        }

        public void ReleaseLease()
        {
            int remaining = System.Threading.Interlocked.Decrement(ref _leases);
            if (remaining < 0)
                return;

            if (remaining == 0 && System.Threading.Volatile.Read(ref _disposeRequested) != 0)
                Dispose();
        }

        public void RequestDispose()
        {
            System.Threading.Volatile.Write(ref _disposeRequested, 1);
            if (System.Threading.Volatile.Read(ref _leases) == 0)
                Dispose();
        }

        public void Dispose()
        {
            if (System.Threading.Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            lock (_filteredLock)
            {
                if (_filtered is not null)
                {
                    foreach (KeyValuePair<FilteredKey, FilteredResult> pair in _filtered)
                        pair.Value.Image.Dispose();
                    _filtered = null;
                    _filteredOrder = null;
                }
            }

            Image.Dispose();
        }
    }
}
```

### `MainSrc\Tools\LiquidGlassAvaloniaUI\LiquidGlassDiagnostics.cs`

```csharp
using System.Threading;

namespace LiquidGlassAvaloniaUI
{
    public readonly struct LiquidGlassDiagnosticsSnapshot
    {
        internal LiquidGlassDiagnosticsSnapshot(
            long captureQueueRequests,
            long captureQueueCoalesced,
            long capturesStarted,
            long capturesSkippedReentrant,
            long capturesSkippedNoSubscribers,
            long capturesSkippedEmptyClip,
            long capturesSkippedByCadence,
            long capturesSkippedByDirtyRect,
            long capturesSkippedByHash,
            long capturesPublished,
            long scratchBitmapRecreated,
            long rendererInvalidations,
            long dirtyRectAvailable,
            long dirtyRectUnavailable,
            long subscriberInvalidations,
            long sampledHashChecks,
            long sampledHashBytes,
            long fullBitmapCopies,
            long fullBitmapCopyBytes,
            long filterIdentityHits,
            long filterCacheHits,
            long filterCacheMisses,
            long filterGpuSurfaces,
            long filterCpuSurfaces,
            long filterSurfaceFailures)
        {
            CaptureQueueRequests = captureQueueRequests;
            CaptureQueueCoalesced = captureQueueCoalesced;
            CapturesStarted = capturesStarted;
            CapturesSkippedReentrant = capturesSkippedReentrant;
            CapturesSkippedNoSubscribers = capturesSkippedNoSubscribers;
            CapturesSkippedEmptyClip = capturesSkippedEmptyClip;
            CapturesSkippedByCadence = capturesSkippedByCadence;
            CapturesSkippedByDirtyRect = capturesSkippedByDirtyRect;
            CapturesSkippedByHash = capturesSkippedByHash;
            CapturesPublished = capturesPublished;
            ScratchBitmapRecreated = scratchBitmapRecreated;
            RendererInvalidations = rendererInvalidations;
            DirtyRectAvailable = dirtyRectAvailable;
            DirtyRectUnavailable = dirtyRectUnavailable;
            SubscriberInvalidations = subscriberInvalidations;
            SampledHashChecks = sampledHashChecks;
            SampledHashBytes = sampledHashBytes;
            FullBitmapCopies = fullBitmapCopies;
            FullBitmapCopyBytes = fullBitmapCopyBytes;
            FilterIdentityHits = filterIdentityHits;
            FilterCacheHits = filterCacheHits;
            FilterCacheMisses = filterCacheMisses;
            FilterGpuSurfaces = filterGpuSurfaces;
            FilterCpuSurfaces = filterCpuSurfaces;
            FilterSurfaceFailures = filterSurfaceFailures;
        }

        public long CaptureQueueRequests { get; }
        public long CaptureQueueCoalesced { get; }
        public long CapturesStarted { get; }
        public long CapturesSkippedReentrant { get; }
        public long CapturesSkippedNoSubscribers { get; }
        public long CapturesSkippedEmptyClip { get; }
        public long CapturesSkippedByCadence { get; }
        public long CapturesSkippedByDirtyRect { get; }
        public long CapturesSkippedByHash { get; }
        public long CapturesPublished { get; }
        public long ScratchBitmapRecreated { get; }
        public long RendererInvalidations { get; }
        public long DirtyRectAvailable { get; }
        public long DirtyRectUnavailable { get; }
        public long SubscriberInvalidations { get; }
        public long SampledHashChecks { get; }
        public long SampledHashBytes { get; }
        public long FullBitmapCopies { get; }
        public long FullBitmapCopyBytes { get; }
        public long FilterIdentityHits { get; }
        public long FilterCacheHits { get; }
        public long FilterCacheMisses { get; }
        public long FilterGpuSurfaces { get; }
        public long FilterCpuSurfaces { get; }
        public long FilterSurfaceFailures { get; }
    }

    public static class LiquidGlassDiagnostics
    {
        private static long s_captureQueueRequests;
        private static long s_captureQueueCoalesced;
        private static long s_capturesStarted;
        private static long s_capturesSkippedReentrant;
        private static long s_capturesSkippedNoSubscribers;
        private static long s_capturesSkippedEmptyClip;
        private static long s_capturesSkippedByCadence;
        private static long s_capturesSkippedByDirtyRect;
        private static long s_capturesSkippedByHash;
        private static long s_capturesPublished;
        private static long s_scratchBitmapRecreated;
        private static long s_rendererInvalidations;
        private static long s_dirtyRectAvailable;
        private static long s_dirtyRectUnavailable;
        private static long s_subscriberInvalidations;
        private static long s_sampledHashChecks;
        private static long s_sampledHashBytes;
        private static long s_fullBitmapCopies;
        private static long s_fullBitmapCopyBytes;
        private static long s_filterIdentityHits;
        private static long s_filterCacheHits;
        private static long s_filterCacheMisses;
        private static long s_filterGpuSurfaces;
        private static long s_filterCpuSurfaces;
        private static long s_filterSurfaceFailures;

        public static LiquidGlassDiagnosticsSnapshot Snapshot
        {
            get => new(
                Volatile.Read(ref s_captureQueueRequests),
                Volatile.Read(ref s_captureQueueCoalesced),
                Volatile.Read(ref s_capturesStarted),
                Volatile.Read(ref s_capturesSkippedReentrant),
                Volatile.Read(ref s_capturesSkippedNoSubscribers),
                Volatile.Read(ref s_capturesSkippedEmptyClip),
                Volatile.Read(ref s_capturesSkippedByCadence),
                Volatile.Read(ref s_capturesSkippedByDirtyRect),
                Volatile.Read(ref s_capturesSkippedByHash),
                Volatile.Read(ref s_capturesPublished),
                Volatile.Read(ref s_scratchBitmapRecreated),
                Volatile.Read(ref s_rendererInvalidations),
                Volatile.Read(ref s_dirtyRectAvailable),
                Volatile.Read(ref s_dirtyRectUnavailable),
                Volatile.Read(ref s_subscriberInvalidations),
                Volatile.Read(ref s_sampledHashChecks),
                Volatile.Read(ref s_sampledHashBytes),
                Volatile.Read(ref s_fullBitmapCopies),
                Volatile.Read(ref s_fullBitmapCopyBytes),
                Volatile.Read(ref s_filterIdentityHits),
                Volatile.Read(ref s_filterCacheHits),
                Volatile.Read(ref s_filterCacheMisses),
                Volatile.Read(ref s_filterGpuSurfaces),
                Volatile.Read(ref s_filterCpuSurfaces),
                Volatile.Read(ref s_filterSurfaceFailures));
        }

        public static void Reset()
        {
            Interlocked.Exchange(ref s_captureQueueRequests, 0);
            Interlocked.Exchange(ref s_captureQueueCoalesced, 0);
            Interlocked.Exchange(ref s_capturesStarted, 0);
            Interlocked.Exchange(ref s_capturesSkippedReentrant, 0);
            Interlocked.Exchange(ref s_capturesSkippedNoSubscribers, 0);
            Interlocked.Exchange(ref s_capturesSkippedEmptyClip, 0);
            Interlocked.Exchange(ref s_capturesSkippedByCadence, 0);
            Interlocked.Exchange(ref s_capturesSkippedByDirtyRect, 0);
            Interlocked.Exchange(ref s_capturesSkippedByHash, 0);
            Interlocked.Exchange(ref s_capturesPublished, 0);
            Interlocked.Exchange(ref s_scratchBitmapRecreated, 0);
            Interlocked.Exchange(ref s_rendererInvalidations, 0);
            Interlocked.Exchange(ref s_dirtyRectAvailable, 0);
            Interlocked.Exchange(ref s_dirtyRectUnavailable, 0);
            Interlocked.Exchange(ref s_subscriberInvalidations, 0);
            Interlocked.Exchange(ref s_sampledHashChecks, 0);
            Interlocked.Exchange(ref s_sampledHashBytes, 0);
            Interlocked.Exchange(ref s_fullBitmapCopies, 0);
            Interlocked.Exchange(ref s_fullBitmapCopyBytes, 0);
            Interlocked.Exchange(ref s_filterIdentityHits, 0);
            Interlocked.Exchange(ref s_filterCacheHits, 0);
            Interlocked.Exchange(ref s_filterCacheMisses, 0);
            Interlocked.Exchange(ref s_filterGpuSurfaces, 0);
            Interlocked.Exchange(ref s_filterCpuSurfaces, 0);
            Interlocked.Exchange(ref s_filterSurfaceFailures, 0);
        }

        internal static void RecordCaptureQueueRequest() => Interlocked.Increment(ref s_captureQueueRequests);
        internal static void RecordCaptureQueueCoalesced() => Interlocked.Increment(ref s_captureQueueCoalesced);
        internal static void RecordCaptureStarted() => Interlocked.Increment(ref s_capturesStarted);
        internal static void RecordCaptureSkippedReentrant() => Interlocked.Increment(ref s_capturesSkippedReentrant);
        internal static void RecordCaptureSkippedNoSubscribers() => Interlocked.Increment(ref s_capturesSkippedNoSubscribers);
        internal static void RecordCaptureSkippedEmptyClip() => Interlocked.Increment(ref s_capturesSkippedEmptyClip);
        internal static void RecordCaptureSkippedByCadence() => Interlocked.Increment(ref s_capturesSkippedByCadence);
        internal static void RecordCaptureSkippedByDirtyRect() => Interlocked.Increment(ref s_capturesSkippedByDirtyRect);
        internal static void RecordCaptureSkippedByHash() => Interlocked.Increment(ref s_capturesSkippedByHash);
        internal static void RecordCapturePublished() => Interlocked.Increment(ref s_capturesPublished);
        internal static void RecordScratchBitmapRecreated() => Interlocked.Increment(ref s_scratchBitmapRecreated);
        internal static void RecordRendererInvalidation() => Interlocked.Increment(ref s_rendererInvalidations);
        internal static void RecordDirtyRectAvailable() => Interlocked.Increment(ref s_dirtyRectAvailable);
        internal static void RecordDirtyRectUnavailable() => Interlocked.Increment(ref s_dirtyRectUnavailable);
        internal static void RecordSubscriberInvalidation() => Interlocked.Increment(ref s_subscriberInvalidations);
        internal static void RecordSampledHash(long bytesCopied)
        {
            Interlocked.Increment(ref s_sampledHashChecks);
            Interlocked.Add(ref s_sampledHashBytes, bytesCopied);
        }

        internal static void RecordFullBitmapCopy(long bytesCopied)
        {
            Interlocked.Increment(ref s_fullBitmapCopies);
            Interlocked.Add(ref s_fullBitmapCopyBytes, bytesCopied);
        }

        internal static void RecordFilterIdentityHit() => Interlocked.Increment(ref s_filterIdentityHits);
        internal static void RecordFilterCacheHit() => Interlocked.Increment(ref s_filterCacheHits);
        internal static void RecordFilterCacheMiss() => Interlocked.Increment(ref s_filterCacheMisses);
        internal static void RecordFilterGpuSurface() => Interlocked.Increment(ref s_filterGpuSurfaces);
        internal static void RecordFilterCpuSurface() => Interlocked.Increment(ref s_filterCpuSurfaces);
        internal static void RecordFilterSurfaceFailure() => Interlocked.Increment(ref s_filterSurfaceFailures);
    }
}
```

### `MainSrc\Tools\LiquidGlassAvaloniaUI\LiquidGlassDrawOperation.cs`

```csharp
using System;
using System.IO;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using SkiaSharp;

namespace LiquidGlassAvaloniaUI
{
    internal class LiquidGlassDrawOperation : ICustomDrawOperation
    {
        private static SKRuntimeEffect? s_lensEffect;
        private static SKRuntimeEffect? s_highlightEffect;
        private static SKRuntimeEffect? s_gammaEffect;
        private static SKRuntimeEffect? s_interactiveHighlightEffect;
        private static SKRuntimeEffect? s_progressiveMaskEffect;
        private static SKRuntimeEffect? s_backdropTransformEffect;
        private static bool s_loaded;

        private readonly Rect _bounds;
        private readonly LiquidGlassDrawParameters _parameters;
        private readonly LiquidGlassBackdropSnapshot? _backdropSnapshot;
        private readonly LiquidGlassDrawPass _pass;

        public LiquidGlassDrawOperation(
            Rect bounds,
            LiquidGlassDrawParameters parameters,
            LiquidGlassBackdropSnapshot? snapshot,
            LiquidGlassDrawPass pass)
        {
            _bounds = bounds;
            _parameters = parameters;
            _backdropSnapshot = snapshot is not null && snapshot.TryAddLease() ? snapshot : null;
            _pass = pass;
        }

        public void Dispose()
        {
            _backdropSnapshot?.ReleaseLease();
        }

        public bool HitTest(Point p)
        {
            return false;
        }

        public Rect Bounds
        {
            get => _bounds;
        }

        public bool Equals(ICustomDrawOperation? other)
        {
            return false;
        }

        public void Render(ImmediateDrawingContext context)
        {
            ISkiaSharpApiLeaseFeature? leaseFeature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
            if (leaseFeature is null)
                return;

            LoadShaders();

            using ISkiaSharpApiLease lease = leaseFeature.Lease();
            SKCanvas canvas = lease.SkCanvas;

            switch (_pass)
            {
                case LiquidGlassDrawPass.Lens:
                    RenderLens(canvas, lease.GrContext);
                    break;
                case LiquidGlassDrawPass.InteractiveHighlight:
                    RenderInteractiveHighlight(canvas);
                    break;
                case LiquidGlassDrawPass.Highlight:
                    RenderHighlight(canvas);
                    break;
            }
        }

        private static void LoadShaders()
        {
            if (s_loaded)
                return;
            s_loaded = true;

            s_lensEffect = LoadRuntimeEffect("avares://LiquidGlassAvaloniaUI/Assets/Shaders/LiquidGlassShader.sksl");
            s_highlightEffect = LoadRuntimeEffect("avares://LiquidGlassAvaloniaUI/Assets/Shaders/LiquidGlassHighlight.sksl");
            s_gammaEffect = LoadRuntimeEffect("avares://LiquidGlassAvaloniaUI/Assets/Shaders/LiquidGlassGamma.sksl");
            s_interactiveHighlightEffect = LoadRuntimeEffect("avares://LiquidGlassAvaloniaUI/Assets/Shaders/LiquidGlassInteractiveHighlight.sksl");
            s_progressiveMaskEffect = LoadRuntimeEffect("avares://LiquidGlassAvaloniaUI/Assets/Shaders/LiquidGlassProgressiveMask.sksl");
            s_backdropTransformEffect = LoadRuntimeEffect("avares://LiquidGlassAvaloniaUI/Assets/Shaders/LiquidGlassBackdropTransform.sksl");
        }

        private static SKRuntimeEffect? LoadRuntimeEffect(string assetUriString)
        {
            try
            {
                Uri assetUri = new(assetUriString);
                using Stream stream = AssetLoader.Open(assetUri);
                using StreamReader reader = new(stream);
                string shaderCode = reader.ReadToEnd();

                SKRuntimeEffect? effect = SKRuntimeEffect.CreateShader(shaderCode, out string? errorText);
                if (effect == null)
                    Console.WriteLine($"[LiquidGlass] Failed to create SKRuntimeEffect ({assetUriString}): {errorText}");

                return effect;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LiquidGlass] Exception while loading shader ({assetUriString}): {ex.Message}");
                return null;
            }
        }

        private void RenderLens(SKCanvas canvas, GRContext? grContext)
        {
            if (s_lensEffect is null)
            {
                DrawErrorHint(canvas);
                return;
            }

            if (_backdropSnapshot is null)
            {
                DrawBackdropNotReady(canvas);
                return;
            }

            SKMatrix currentTransform = canvas.TotalMatrix;
            if (!currentTransform.TryInvert(out SKMatrix currentInvertedTransform))
                return;

            SKSize size = new((float)_bounds.Width, (float)_bounds.Height);
            if (size.Width <= 0 || size.Height <= 0)
                return;

            float maxRadius = Math.Min(size.Width, size.Height) * 0.5f;
            float[] cornerRadii = GetCornerRadii(_parameters.CornerRadius, maxRadius);

            // Pipeline order:
            //   color controls (brightness/contrast/vibrancy) -> blur -> lens -> (optional gamma)
            //
            // In Avalonia we approximate the RenderEffect chain by applying an SKImageFilter pipeline to the
            // captured backdrop snapshot, then sampling the filtered image in the lens runtime shader.
            LiquidGlassBackdropSnapshot.FilteredResult filtered = GetOrCreateFilteredBackdrop(
                _backdropSnapshot,
                _parameters,
                grContext,
                currentTransform,
                size);

            using SKShader? backdropShader = SKShader.CreateImage(
                filtered.Image,
                SKShaderTileMode.Clamp,
                SKShaderTileMode.Clamp,
                WithPixelOrigin(currentInvertedTransform, filtered.OriginInPixels));

            SKShader? lensInput = (SKShader)backdropShader;

            SKShader? backdropTransformShader = null;
            try
            {
                double zoomValue = _parameters.BackdropZoom;
                if (zoomValue <= 0.0005 || double.IsNaN(zoomValue) || double.IsInfinity(zoomValue))
                    zoomValue = 1.0;

                float zoom = (float)Clamp(zoomValue, 0.1, 10.0);
                Vector offset = _parameters.BackdropOffset;
                bool needsTransform =
                    Math.Abs(zoom - 1.0f) > 0.0005f
                    || Math.Abs(offset.X) > 0.0005
                    || Math.Abs(offset.Y) > 0.0005;

                if (needsTransform && s_backdropTransformEffect is not null)
                {
                    using SKRuntimeEffectUniforms uniforms = new(s_backdropTransformEffect);
                    uniforms["size"] = new[]
                    {
                        size.Width, size.Height
                    };
                    uniforms["zoom"] = zoom;
                    uniforms["offset"] = new[]
                    {
                        (float)offset.X, (float)offset.Y
                    };

                    using SKRuntimeEffectChildren children = new(s_backdropTransformEffect);
                    children["content"] = lensInput;

                    backdropTransformShader = s_backdropTransformEffect.ToShader(uniforms, children);
                    if (backdropTransformShader is not null)
                        lensInput = backdropTransformShader;
                }

                float refractionHeight = (float)Clamp(_parameters.RefractionHeight, 0.0, Math.Min(size.Width, size.Height) * 0.5);
                float refractionAmount = (float)_parameters.RefractionAmount;
                bool applyLens = refractionHeight > 0.001f && Math.Abs(refractionAmount) > 0.001f;

                SKShader? lensShader = null;
                if (applyLens)
                {
                    using SKRuntimeEffectUniforms lensUniforms = new(s_lensEffect);
                    lensUniforms["size"] = new[]
                    {
                        size.Width, size.Height
                    };
                    lensUniforms["cornerRadii"] = cornerRadii;
                    lensUniforms["refractionHeight"] = refractionHeight;
                    // The lens shader expects a negative refraction amount.
                    lensUniforms["refractionAmount"] = -refractionAmount;
                    lensUniforms["depthEffect"] = _parameters.DepthEffect ? 1.0f : 0.0f;
                    lensUniforms["chromaticAberration"] = _parameters.ChromaticAberration ? 1.0f : 0.0f;

                    using SKRuntimeEffectChildren lensChildren = new(s_lensEffect);
                    lensChildren["content"] = lensInput;

                    lensShader = s_lensEffect.ToShader(lensUniforms, lensChildren);
                }

                SKShader? baseShader = lensShader ?? lensInput;

                SKShader? progressiveMaskShader = null;
                try
                {
                    if (_parameters.ProgressiveBlurEnabled
                        && s_progressiveMaskEffect is not null)
                    {
                        using SKRuntimeEffectUniforms uniforms = new(s_progressiveMaskEffect);
                        uniforms["size"] = new[]
                        {
                            size.Width, size.Height
                        };
                        uniforms["start"] = (float)Clamp(_parameters.ProgressiveBlurStart, 0.0, 1.0);
                        uniforms["end"] = (float)Clamp(_parameters.ProgressiveBlurEnd, 0.0, 1.0);

                        Color tint = _parameters.ProgressiveTintColor;
                        uniforms["tint"] = new[]
                        {
                            tint.R / 255f, tint.G / 255f, tint.B / 255f, tint.A / 255f
                        };

                        float tintIntensity = tint.A > 0
                            ? (float)Clamp(_parameters.ProgressiveTintIntensity, 0.0, 1.0)
                            : 0.0f;
                        uniforms["tintIntensity"] = tintIntensity;

                        using SKRuntimeEffectChildren children = new(s_progressiveMaskEffect);
                        children["content"] = baseShader;

                        progressiveMaskShader = s_progressiveMaskEffect.ToShader(uniforms, children);
                        if (progressiveMaskShader is not null)
                            baseShader = progressiveMaskShader;
                    }

                    SKShader? gammaShader = null;
                    try
                    {
                        float gammaPower = (float)Clamp(_parameters.GammaPower, 0.0, 10.0);
                        if (s_gammaEffect is not null && Math.Abs(gammaPower - 1.0f) > 0.0005f)
                        {
                            using SKRuntimeEffectUniforms uniforms = new(s_gammaEffect);
                            uniforms["power"] = gammaPower;
                            using SKRuntimeEffectChildren children = new(s_gammaEffect);
                            children["content"] = baseShader;
                            gammaShader = s_gammaEffect.ToShader(uniforms, children);
                        }

                        using SKPaint paint = new()
                        {
                            Shader = gammaShader ?? baseShader,
                            IsAntialias = true
                        };

                        SKRect rect = SKRect.Create(0, 0, size.Width, size.Height);
                        using SKPath clipPath = CreateRoundRectPath(rect, cornerRadii);

                        canvas.Save();
                        canvas.ClipPath(clipPath, SKClipOperation.Intersect, true);
                        canvas.DrawRect(rect, paint);

                        DrawSurfaceOverlay(canvas, rect);

                        canvas.Restore();
                    }
                    finally
                    {
                        gammaShader?.Dispose();
                    }
                }
                finally
                {
                    progressiveMaskShader?.Dispose();
                    lensShader?.Dispose();
                }
            }
            finally
            {
                backdropTransformShader?.Dispose();
            }

        }

        private static LiquidGlassBackdropSnapshot.FilteredResult GetOrCreateFilteredBackdrop(
            LiquidGlassBackdropSnapshot snapshot,
            LiquidGlassDrawParameters parameters,
            GRContext? grContext,
            SKMatrix currentTransform,
            SKSize localSize)
        {
            float brightness = (float)Clamp(parameters.Brightness, -1.0, 1.0);
            float contrast = (float)Clamp(parameters.Contrast, 0.0, 4.0);
            float saturation = (float)Clamp(parameters.Vibrancy, 0.0, 4.0);
            float exposureEv = (float)Clamp(parameters.ExposureEv, -8.0, 8.0);
            float opacity = (float)Clamp(parameters.BackdropOpacity, 0.0, 1.0);

            float blurSigmaPx = (float)(Clamp(parameters.BlurRadius, 0.0, 256.0) * snapshot.Scaling);

            bool isIdentity =
                Math.Abs(brightness) < 0.0005f
                && Math.Abs(contrast - 1.0f) < 0.0005f
                && Math.Abs(saturation - 1.0f) < 0.0005f
                && Math.Abs(exposureEv) < 0.0005f
                && Math.Abs(opacity - 1.0f) < 0.0005f
                && blurSigmaPx <= 0.0005f;

            if (isIdentity)
            {
                LiquidGlassDiagnostics.RecordFilterIdentityHit();
                return new LiquidGlassBackdropSnapshot.FilteredResult(snapshot.Image, snapshot.OriginInPixels);
            }

            SKRectI cropRect = CalculateFilterCrop(snapshot, parameters, currentTransform, localSize);

            // Quantize to keep the cache size stable during slider drags.
            const float q = 1000.0f;
            LiquidGlassBackdropSnapshot.FilteredKey key = new(
                (int)Math.Round(brightness * q),
                (int)Math.Round(contrast * q),
                (int)Math.Round(saturation * q),
                (int)Math.Round(exposureEv * q),
                (int)Math.Round(opacity * q),
                (int)Math.Round(blurSigmaPx * q),
                cropRect.Left,
                cropRect.Top,
                cropRect.Width,
                cropRect.Height,
                GetRenderContextId(grContext));

            if (snapshot.TryGetFiltered(key, out LiquidGlassBackdropSnapshot.FilteredResult cached))
            {
                LiquidGlassDiagnostics.RecordFilterCacheHit();
                return cached;
            }

            LiquidGlassDiagnostics.RecordFilterCacheMiss();

            LiquidGlassBackdropSnapshot.FilteredResult filtered = CreateFilteredBackdrop(snapshot, cropRect, brightness, contrast, saturation, exposureEv, opacity, blurSigmaPx, grContext)
                                                                  ?? new LiquidGlassBackdropSnapshot.FilteredResult(snapshot.Image, snapshot.OriginInPixels);

            // Never cache the unfiltered snapshot image (it is owned/disposed separately).
            if (!ReferenceEquals(filtered.Image, snapshot.Image))
                snapshot.StoreFiltered(key, filtered);

            return filtered;
        }

        private static LiquidGlassBackdropSnapshot.FilteredResult? CreateFilteredBackdrop(
            LiquidGlassBackdropSnapshot snapshot,
            SKRectI cropRect,
            float brightness,
            float contrast,
            float saturation,
            float exposureEv,
            float opacity,
            float blurSigmaPx,
            GRContext? grContext)
        {
            SKImage source = snapshot.Image;
            bool isFullCrop =
                cropRect.Left == 0
                && cropRect.Top == 0
                && cropRect.Width == source.Width
                && cropRect.Height == source.Height;

            using SKImage? croppedSource = isFullCrop ? null : source.Subset(cropRect);
            SKImage filterSource = croppedSource ?? source;
            PixelPoint filteredOrigin = isFullCrop
                ? snapshot.OriginInPixels
                : new PixelPoint(snapshot.OriginInPixels.X + cropRect.Left, snapshot.OriginInPixels.Y + cropRect.Top);

            SKImageFilter? filter = null;

            bool needsColorControls =
                Math.Abs(brightness) > 0.0005f
                || Math.Abs(contrast - 1.0f) > 0.0005f
                || Math.Abs(saturation - 1.0f) > 0.0005f;

            if (needsColorControls)
            {
                using SKColorFilter? colorFilter = SKColorFilter.CreateColorMatrix(CreateColorControlsColorMatrix(brightness, contrast, saturation));
                filter = SKImageFilter.CreateColorFilter(colorFilter, filter);
            }

            if (Math.Abs(exposureEv) > 0.0005f)
            {
                using SKColorFilter? colorFilter = SKColorFilter.CreateColorMatrix(CreateExposureColorMatrix(exposureEv));
                filter = SKImageFilter.CreateColorFilter(colorFilter, filter);
            }

            if (Math.Abs(opacity - 1.0f) > 0.0005f)
            {
                using SKColorFilter? colorFilter = SKColorFilter.CreateColorMatrix(CreateOpacityColorMatrix(opacity));
                filter = SKImageFilter.CreateColorFilter(colorFilter, filter);
            }

            if (blurSigmaPx > 0.0005f)
            {
                // Use TileMode.Clamp: Skia's default tile mode can introduce alpha falloff near image edges (kDecal),
                // which shows up as darkened borders when the snapshot is clipped by the window bounds.
                SKRect filterCropRect = new(0, 0, filterSource.Width, filterSource.Height);
                filter = SKImageFilter.CreateBlur(blurSigmaPx, blurSigmaPx, SKShaderTileMode.Clamp, filter, filterCropRect);
            }

            if (filter is null)
                return null;

            using (filter)
            {
                // ApplyImageFilter() can return a varying offset/subset for blur radii, which can cause the
                // sampled backdrop to appear to "drift" while dragging the blur slider. Instead, render the
                // filtered snapshot into an explicit same-size surface at (0,0) so the origin remains stable.
                SKImageInfo info = new(filterSource.Width, filterSource.Height, filterSource.ColorType, filterSource.AlphaType, filterSource.ColorSpace);
                using SKSurface? surface = CreateFilterSurface(info, grContext);
                if (surface is null)
                {
                    LiquidGlassDiagnostics.RecordFilterSurfaceFailure();
                    return null;
                }

                SKCanvas? canvas = surface.Canvas;
                canvas.Clear(SKColors.Transparent);

                using SKPaint paint = new()
                {
                    ImageFilter = filter,
                    BlendMode = SKBlendMode.Src
                };

                canvas.DrawImage(filterSource, 0, 0, paint);
                canvas.Flush();

                SKImage? filteredImage = surface.Snapshot();
                return new LiquidGlassBackdropSnapshot.FilteredResult(filteredImage, filteredOrigin);
            }
        }

        private static SKRectI CalculateFilterCrop(
            LiquidGlassBackdropSnapshot snapshot,
            LiquidGlassDrawParameters parameters,
            SKMatrix currentTransform,
            SKSize localSize)
        {
            SKImage source = snapshot.Image;
            SKRectI full = new(0, 0, source.Width, source.Height);

            if (localSize.Width <= 0 || localSize.Height <= 0)
                return full;

            if (Math.Abs(currentTransform.Persp0) > 0.000001f
                || Math.Abs(currentTransform.Persp1) > 0.000001f
                || Math.Abs(currentTransform.Persp2 - 1.0f) > 0.000001f)
            {
                return full;
            }

            SKRect deviceBounds = MapRect(currentTransform, SKRect.Create(0, 0, localSize.Width, localSize.Height));
            if (deviceBounds.Width <= 0 || deviceBounds.Height <= 0)
                return full;

            double margin = CalculateFilterMarginPx(parameters, localSize, snapshot.Scaling);

            int left = (int)Math.Floor(deviceBounds.Left - snapshot.OriginInPixels.X - margin);
            int top = (int)Math.Floor(deviceBounds.Top - snapshot.OriginInPixels.Y - margin);
            int right = (int)Math.Ceiling(deviceBounds.Right - snapshot.OriginInPixels.X + margin);
            int bottom = (int)Math.Ceiling(deviceBounds.Bottom - snapshot.OriginInPixels.Y + margin);

            left = Math.Clamp(left, 0, source.Width);
            top = Math.Clamp(top, 0, source.Height);
            right = Math.Clamp(right, left, source.Width);
            bottom = Math.Clamp(bottom, top, source.Height);

            if (right <= left || bottom <= top)
                return full;

            return new SKRectI(left, top, right, bottom);
        }

        private static double CalculateFilterMarginPx(LiquidGlassDrawParameters parameters, SKSize localSize, double scaling)
        {
            double zoomValue = parameters.BackdropZoom;
            if (zoomValue <= 0.0005 || double.IsNaN(zoomValue) || double.IsInfinity(zoomValue))
                zoomValue = 1.0;

            double zoom = Clamp(zoomValue, 0.1, 10.0);
            Vector offset = parameters.BackdropOffset;
            double offsetMargin = Math.Max(Math.Abs(offset.X), Math.Abs(offset.Y)) / zoom;

            double zoomOutMargin = 0.0;
            if (zoom < 1.0)
            {
                double halfMaxSize = Math.Max(localSize.Width, localSize.Height) * 0.5;
                zoomOutMargin = (1.0 / zoom - 1.0) * halfMaxSize;
            }

            double refractionMargin = Math.Abs(parameters.RefractionAmount) * (parameters.ChromaticAberration ? 2.0 : 1.0);
            double blurMargin = parameters.BlurRadius * 3.0;

            return Math.Max(8.0, refractionMargin + blurMargin + offsetMargin + zoomOutMargin + 8.0) * scaling;
        }

        private static SKRect MapRect(SKMatrix matrix, SKRect rect)
        {
            float x0 = matrix.ScaleX * rect.Left + matrix.SkewX * rect.Top + matrix.TransX;
            float y0 = matrix.SkewY * rect.Left + matrix.ScaleY * rect.Top + matrix.TransY;
            float x1 = matrix.ScaleX * rect.Right + matrix.SkewX * rect.Top + matrix.TransX;
            float y1 = matrix.SkewY * rect.Right + matrix.ScaleY * rect.Top + matrix.TransY;
            float x2 = matrix.ScaleX * rect.Right + matrix.SkewX * rect.Bottom + matrix.TransX;
            float y2 = matrix.SkewY * rect.Right + matrix.ScaleY * rect.Bottom + matrix.TransY;
            float x3 = matrix.ScaleX * rect.Left + matrix.SkewX * rect.Bottom + matrix.TransX;
            float y3 = matrix.SkewY * rect.Left + matrix.ScaleY * rect.Bottom + matrix.TransY;

            float left = Math.Min(Math.Min(x0, x1), Math.Min(x2, x3));
            float top = Math.Min(Math.Min(y0, y1), Math.Min(y2, y3));
            float right = Math.Max(Math.Max(x0, x1), Math.Max(x2, x3));
            float bottom = Math.Max(Math.Max(y0, y1), Math.Max(y2, y3));

            return new SKRect(left, top, right, bottom);
        }

        private static SKSurface? CreateFilterSurface(SKImageInfo info, GRContext? grContext)
        {
            if (grContext is not null)
            {
                SKSurface? gpuSurface = SKSurface.Create(grContext, false, info);
                if (gpuSurface is not null)
                {
                    LiquidGlassDiagnostics.RecordFilterGpuSurface();
                    return gpuSurface;
                }
            }

            SKSurface? cpuSurface = SKSurface.Create(info);
            if (cpuSurface is not null)
                LiquidGlassDiagnostics.RecordFilterCpuSurface();

            return cpuSurface;
        }

        private static int GetRenderContextId(GRContext? grContext)
        {
            return grContext is null ? 0 : RuntimeHelpers.GetHashCode(grContext);
        }

        private static float[] CreateColorControlsColorMatrix(float brightness, float contrast, float saturation)
        {
            // Color-controls matrix (brightness/contrast/saturation).
            // Note: Skia's color matrix operates on normalized (0..1) colors.
            float invSat = 1f - saturation;
            float r = 0.213f * invSat;
            float g = 0.715f * invSat;
            float b = 0.072f * invSat;

            float c = contrast;
            // Translation terms must also be normalized.
            float t = 0.5f - c * 0.5f + brightness;
            float s = saturation;

            float cr = c * r;
            float cg = c * g;
            float cb = c * b;
            float cs = c * s;

            return new[]
            {
                cr + cs, cg, cb, 0f, t, cr, cg + cs, cb, 0f, t, cr, cg, cb + cs, 0f, t, 0f, 0f, 0f, 1f, 0f
            };
        }

        private static float[] CreateExposureColorMatrix(float ev)
        {
            float scale = (float)Math.Pow(2.0, ev / 2.2);
            return new[]
            {
                scale, 0f, 0f, 0f, 0f, 0f, scale, 0f, 0f, 0f, 0f, 0f, scale, 0f, 0f, 0f, 0f, 0f, 1f, 0f
            };
        }

        private static float[] CreateOpacityColorMatrix(float alpha)
        {
            return new[]
            {
                1f, 0f, 0f, 0f, 0f, 0f, 1f, 0f, 0f, 0f, 0f, 0f, 1f, 0f, 0f, 0f, 0f, 0f, alpha, 0f
            };
        }

        private void RenderInteractiveHighlight(SKCanvas canvas)
        {
            if (s_interactiveHighlightEffect is null)
                return;

            float progress = (float)Clamp(_parameters.InteractiveProgress, 0.0, 1.0);
            if (progress <= 0.001f)
                return;

            SKSize size = new((float)_bounds.Width, (float)_bounds.Height);
            if (size.Width <= 0 || size.Height <= 0)
                return;

            float maxRadius = Math.Min(size.Width, size.Height) * 0.5f;
            float[] cornerRadii = GetCornerRadii(_parameters.CornerRadius, maxRadius);

            SKRect rect = SKRect.Create(0, 0, size.Width, size.Height);
            using SKPath clipPath = CreateRoundRectPath(rect, cornerRadii);

            canvas.Save();
            canvas.ClipPath(clipPath, SKClipOperation.Intersect, true);

            using (SKPaint basePaint = new()
                {
                    Color = new SKColor(255, 255, 255, (byte)Clamp(0.08f * progress * 255f, 0f, 255f)),
                    BlendMode = SKBlendMode.Plus,
                    IsAntialias = true
                })
            {
                canvas.DrawRect(rect, basePaint);
            }

            using SKRuntimeEffectUniforms uniforms = new(s_interactiveHighlightEffect);
            uniforms["size"] = new[]
            {
                size.Width, size.Height
            };
            uniforms["color"] = new[]
            {
                1.0f, 1.0f, 1.0f, (float)Clamp(0.15 * progress, 0.0, 1.0)
            };
            uniforms["radius"] = Math.Min(size.Width, size.Height) * 1.5f;
            uniforms["position"] = new[]
            {
                (float)Clamp(_parameters.InteractivePosition.X, 0.0, size.Width), (float)Clamp(_parameters.InteractivePosition.Y, 0.0, size.Height)
            };

            using SKRuntimeEffectChildren children = new(s_interactiveHighlightEffect);
            using SKShader? shader = s_interactiveHighlightEffect.ToShader(uniforms, children);

            if (shader is not null)
            {
                using SKPaint paint = new()
                {
                    Shader = shader,
                    BlendMode = SKBlendMode.Plus,
                    IsAntialias = true
                };
                canvas.DrawRect(rect, paint);
            }

            canvas.Restore();
        }

        private void RenderHighlight(SKCanvas canvas)
        {
            if (s_highlightEffect is null)
                return;

            if (_parameters.HighlightOpacity <= 0.001 || _parameters.HighlightWidth <= 0.001)
                return;

            SKSize size = new((float)_bounds.Width, (float)_bounds.Height);
            if (size.Width <= 0 || size.Height <= 0)
                return;

            float maxRadius = Math.Min(size.Width, size.Height) * 0.5f;
            float[] cornerRadii = GetCornerRadii(_parameters.CornerRadius, maxRadius);

            using SKRuntimeEffectUniforms uniforms = new(s_highlightEffect);
            uniforms["size"] = new[]
            {
                size.Width, size.Height
            };
            uniforms["cornerRadii"] = cornerRadii;

            float alpha = (float)Clamp(_parameters.HighlightOpacity, 0.0, 1.0);
            uniforms["color"] = new[]
            {
                1.0f, 1.0f, 1.0f, alpha
            };

            float angleRad = (float)(_parameters.HighlightAngleDegrees * (Math.PI / 180.0));
            uniforms["angle"] = angleRad;
            uniforms["falloff"] = (float)Clamp(_parameters.HighlightFalloff, 0.0, 8.0);

            using SKRuntimeEffectChildren children = new(s_highlightEffect);
            using SKShader? shader = s_highlightEffect.ToShader(uniforms, children);
            if (shader is null)
                return;

            float blurRadius = (float)Clamp(_parameters.HighlightBlurRadius, 0.0, 20.0);
            using SKMaskFilter? maskFilter = blurRadius > 0.001f
                ? SKMaskFilter.CreateBlur(SKBlurStyle.Normal, blurRadius)
                : null;

            float strokeWidth = (float)(Math.Ceiling(Clamp(_parameters.HighlightWidth, 0.0, 100.0)) * 2.0);

            using SKPaint paint = new()
            {
                Shader = shader,
                IsAntialias = true,
                BlendMode = SKBlendMode.Plus,
                Style = SKPaintStyle.Stroke,
                StrokeJoin = SKStrokeJoin.Round,
                StrokeCap = SKStrokeCap.Round,
                StrokeWidth = Math.Max(0.5f, strokeWidth),
                MaskFilter = maskFilter
            };

            SKRect rect = SKRect.Create(0, 0, size.Width, size.Height);
            using SKPath path = CreateRoundRectPath(rect, cornerRadii);

            // Pad the highlight layer to avoid edge artifacts when transformed and/or rasterized into an intermediate surface.
            const float safePad = 1.0f;

            canvas.Save();
            canvas.Translate(-safePad, -safePad);
            SKRect layerBounds = SKRect.Create(0, 0, size.Width + safePad * 2.0f, size.Height + safePad * 2.0f);
            canvas.SaveLayer(layerBounds, null);

            canvas.Translate(safePad, safePad);
            canvas.ClipPath(path, SKClipOperation.Intersect, true);
            canvas.DrawPath(path, paint);

            canvas.Restore();
            canvas.Restore();
        }

        private void DrawSurfaceOverlay(SKCanvas canvas, SKRect rect)
        {
            // Optional tint/surface overlays. If TintColor is specified, it draws it twice: Hue blend + alpha fill.
            if (_parameters.TintColor.A > 0)
            {
                Color tint = _parameters.TintColor;
                using SKPaint huePaint = new()
                {
                    Color = new SKColor(tint.R, tint.G, tint.B, 255),
                    IsAntialias = true,
                    BlendMode = SKBlendMode.Hue
                };
                canvas.DrawRect(rect, huePaint);

                using SKPaint fillPaint = new()
                {
                    Color = new SKColor(tint.R, tint.G, tint.B, (byte)Clamp(tint.A * 0.75, 0.0, 255.0)),
                    IsAntialias = true,
                    BlendMode = SKBlendMode.SrcOver
                };
                canvas.DrawRect(rect, fillPaint);
            }

            if (_parameters.SurfaceColor.A > 0)
            {
                Color surface = _parameters.SurfaceColor;
                using SKPaint paint = new()
                {
                    Color = new SKColor(surface.R, surface.G, surface.B, surface.A),
                    IsAntialias = true,
                    BlendMode = SKBlendMode.SrcOver
                };
                canvas.DrawRect(rect, paint);
            }
        }

        private static float[] GetCornerRadii(CornerRadius cornerRadius, float maxRadius)
        {
            float tl = (float)Clamp(cornerRadius.TopLeft, 0.0, maxRadius);
            float tr = (float)Clamp(cornerRadius.TopRight, 0.0, maxRadius);
            float br = (float)Clamp(cornerRadius.BottomRight, 0.0, maxRadius);
            float bl = (float)Clamp(cornerRadius.BottomLeft, 0.0, maxRadius);
            return new[]
            {
                tl, tr, br, bl
            };
        }

        private static SKPath CreateRoundRectPath(SKRect rect, float[] cornerRadii)
        {
            using SKRoundRect rr = new();
            rr.SetRectRadii(rect, new[]
            {
                new SKPoint(cornerRadii[0], cornerRadii[0]), new SKPoint(cornerRadii[1], cornerRadii[1]), new SKPoint(cornerRadii[2], cornerRadii[2]), new SKPoint(cornerRadii[3], cornerRadii[3])
            });

            SKPath path = new();
            path.AddRoundRect(rr, SKPathDirection.Clockwise);
            return path;
        }

        private void DrawErrorHint(SKCanvas canvas)
        {
            using SKPaint errorPaint = new()
            {
                Color = new SKColor(255, 0, 0, 120),
                Style = SKPaintStyle.Fill
            };

            canvas.DrawRect(SKRect.Create(0, 0, (float)_bounds.Width, (float)_bounds.Height), errorPaint);
        }

        private void DrawBackdropNotReady(SKCanvas canvas)
        {
            SKSize size = new((float)_bounds.Width, (float)_bounds.Height);
            SKRect rect = SKRect.Create(0, 0, size.Width, size.Height);

            float maxRadius = Math.Min(size.Width, size.Height) * 0.5f;
            float[] cornerRadii = GetCornerRadii(_parameters.CornerRadius, maxRadius);
            using SKPath path = CreateRoundRectPath(rect, cornerRadii);

            using SKPaint paint = new()
            {
                Color = new SKColor(255, 255, 255, 32),
                IsAntialias = true
            };

            canvas.Save();
            canvas.ClipPath(path, SKClipOperation.Intersect, true);
            canvas.DrawRect(rect, paint);
            canvas.Restore();
        }

        private static double Clamp(double value, double min, double max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private static SKMatrix WithPixelOrigin(SKMatrix invertedTransform, PixelPoint originInPixels)
        {
            // localMatrix = invertedTransform * Translate(+originInPixels)
            //
            // We first cancel the current canvas transform (so shader coordinates become device pixels),
            // then shift into the clipped snapshot's coordinate system.
            float ox = (float)originInPixels.X;
            float oy = (float)originInPixels.Y;
            invertedTransform.TransX = invertedTransform.TransX + invertedTransform.ScaleX * ox + invertedTransform.SkewX * oy;
            invertedTransform.TransY = invertedTransform.TransY + invertedTransform.SkewY * ox + invertedTransform.ScaleY * oy;
            return invertedTransform;
        }
    }
}
```

### `MainSrc\Tools\LiquidGlassAvaloniaUI\LiquidGlassDrawParameters.cs`

```csharp
using Avalonia;
using Avalonia.Media;

namespace LiquidGlassAvaloniaUI
{
    internal enum LiquidGlassDrawPass
    {
        Lens,
        InteractiveHighlight,
        Highlight
    }

    internal struct LiquidGlassDrawParameters
    {
        public CornerRadius CornerRadius { get; set; }

        public double BackdropZoom { get; set; }
        public Vector BackdropOffset { get; set; }

        public double RefractionHeight { get; set; }
        public double RefractionAmount { get; set; }
        public bool DepthEffect { get; set; }
        public bool ChromaticAberration { get; set; }

        public double BlurRadius { get; set; }
        public double Vibrancy { get; set; }
        public double Brightness { get; set; }
        public double Contrast { get; set; }
        public double ExposureEv { get; set; }
        public double GammaPower { get; set; }
        public double BackdropOpacity { get; set; }

        public Color TintColor { get; set; }
        public Color SurfaceColor { get; set; }

        public bool ProgressiveBlurEnabled { get; set; }
        public double ProgressiveBlurStart { get; set; }
        public double ProgressiveBlurEnd { get; set; }
        public Color ProgressiveTintColor { get; set; }
        public double ProgressiveTintIntensity { get; set; }

        public bool HighlightEnabled { get; set; }
        public double HighlightWidth { get; set; }
        public double HighlightBlurRadius { get; set; }
        public double HighlightOpacity { get; set; }
        public double HighlightAngleDegrees { get; set; }
        public double HighlightFalloff { get; set; }

        public double InteractiveProgress { get; set; }
        public Point InteractivePosition { get; set; }

        public bool ShadowEnabled { get; set; }
        public double ShadowRadius { get; set; }
        public Vector ShadowOffset { get; set; }
        public Color ShadowColor { get; set; }
        public double ShadowOpacity { get; set; }

        public bool InnerShadowEnabled { get; set; }
        public double InnerShadowRadius { get; set; }
        public Vector InnerShadowOffset { get; set; }
        public Color InnerShadowColor { get; set; }
        public double InnerShadowOpacity { get; set; }
    }
}
```

### `MainSrc\Tools\LiquidGlassAvaloniaUI\LiquidGlassInteractiveSurface.cs`

```csharp
using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;

namespace LiquidGlassAvaloniaUI
{
    /// <summary>
    /// An interactive variant of <see cref="LiquidGlassSurface"/> that adds press/drag deformation and
    /// an interactive highlight overlay.
    /// </summary>
    public class LiquidGlassInteractiveSurface : LiquidGlassSurface
    {
        public static readonly StyledProperty<bool> IsInteractiveProperty =
            AvaloniaProperty.Register<LiquidGlassInteractiveSurface, bool>(nameof(IsInteractive), true);

        public static readonly StyledProperty<bool> InteractiveHighlightEnabledProperty =
            AvaloniaProperty.Register<LiquidGlassInteractiveSurface, bool>(nameof(InteractiveHighlightEnabled), true);

        public static readonly StyledProperty<double> InteractiveMaxScaleDipProperty =
            AvaloniaProperty.Register<LiquidGlassInteractiveSurface, double>(nameof(InteractiveMaxScaleDip), 4.0);

        static LiquidGlassInteractiveSurface()
        {
            AffectsRender<LiquidGlassInteractiveSurface>(
                IsInteractiveProperty,
                InteractiveHighlightEnabledProperty,
                InteractiveMaxScaleDipProperty);
        }

        private const double SpringDampingRatio = 0.5;
        private const double SpringStiffness = 300.0;
        private const double SpringProgressThreshold = 0.001;
        private const double InitialDerivative = 0.05;

        private readonly DispatcherTimer _animationTimer;
        private DateTime _lastAnimationTickUtc;

        private int? _activePointerId;
        private TopLevel? _hookedTopLevel;

        private Point _startPosition;
        private SpringDouble _pressProgress = new(SpringStiffness, SpringDampingRatio, SpringProgressThreshold);
        private SpringPoint _position = new(SpringStiffness, SpringDampingRatio, 0.5);

        public LiquidGlassInteractiveSurface()
        {
            RenderTransformOrigin = RelativePoint.Center;

            _animationTimer = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(16)
            };

            _animationTimer.Tick += (_, _) =>
            {
                DateTime now = DateTime.UtcNow;
                double dt = (now - _lastAnimationTickUtc).TotalSeconds;
                _lastAnimationTickUtc = now;

                // Clamp dt to avoid huge steps on debugger stops / tab switches.
                dt = Math.Max(0.0, Math.Min(0.05, dt));

                bool any = false;
                any |= _pressProgress.Step(dt);
                any |= _activePointerId is null && _position.Step(dt);

                ApplyDeformation();
                InvalidateSubscriberVisual();
                InteractiveOverlay?.InvalidateVisual();

                if (!any)
                    _animationTimer.Stop();
            };

            AddHandler(PointerPressedEvent, OnSelfPointerPressed, RoutingStrategies.Tunnel, true);
        }

        public bool IsInteractive
        {
            get => GetValue(IsInteractiveProperty);
            set => SetValue(IsInteractiveProperty, value);
        }

        public bool InteractiveHighlightEnabled
        {
            get => GetValue(InteractiveHighlightEnabledProperty);
            set => SetValue(InteractiveHighlightEnabledProperty, value);
        }

        public double InteractiveMaxScaleDip
        {
            get => GetValue(InteractiveMaxScaleDipProperty);
            set => SetValue(InteractiveMaxScaleDipProperty, value);
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
            UnhookTopLevel();
            _activePointerId = null;
            _animationTimer.Stop();
        }

        private void OnSelfPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (!IsInteractive)
                return;

            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                return;

            if (_activePointerId is not null)
                return;

            _activePointerId = e.Pointer.Id;
            _startPosition = e.GetPosition(this);
            _position.SnapTo(_startPosition);

            _pressProgress.Target = 1.0;
            _position.Target = _startPosition;

            HookTopLevel();
            ApplyDeformation();
            InvalidateSubscriberVisual();
            InteractiveOverlay?.InvalidateVisual();
            StartAnimation();
        }

        private void OnTopLevelPointerMoved(object? sender, PointerEventArgs e)
        {
            if (_activePointerId is null || e.Pointer.Id != _activePointerId.Value)
                return;

            Point p = e.GetPosition(this);
            _position.SnapTo(p);
            ApplyDeformation();
            InvalidateSubscriberVisual();
            InteractiveOverlay?.InvalidateVisual();
            StartAnimation();
        }

        private void OnTopLevelPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (_activePointerId is null || e.Pointer.Id != _activePointerId.Value)
                return;

            EndInteraction();
        }

        private void OnTopLevelPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
        {
            // Treat capture loss as cancellation.
            if (_activePointerId is null)
                return;

            EndInteraction();
        }

        private void EndInteraction()
        {
            _activePointerId = null;
            UnhookTopLevel();

            _pressProgress.Target = 0.0;
            _position.Target = _startPosition;
            ApplyDeformation();
            InvalidateSubscriberVisual();
            InteractiveOverlay?.InvalidateVisual();
            StartAnimation();
        }

        private void HookTopLevel()
        {
            TopLevel? topLevel = TopLevel.GetTopLevel(this);
            if (topLevel is null || ReferenceEquals(topLevel, _hookedTopLevel))
                return;

            UnhookTopLevel();

            _hookedTopLevel = topLevel;
            topLevel.AddHandler(PointerMovedEvent, OnTopLevelPointerMoved, RoutingStrategies.Tunnel, true);
            topLevel.AddHandler(PointerReleasedEvent, OnTopLevelPointerReleased, RoutingStrategies.Tunnel, true);
            topLevel.AddHandler(PointerCaptureLostEvent, OnTopLevelPointerCaptureLost, RoutingStrategies.Tunnel, true);
        }

        private void UnhookTopLevel()
        {
            if (_hookedTopLevel is null)
                return;

            _hookedTopLevel.RemoveHandler(PointerMovedEvent, OnTopLevelPointerMoved);
            _hookedTopLevel.RemoveHandler(PointerReleasedEvent, OnTopLevelPointerReleased);
            _hookedTopLevel.RemoveHandler(PointerCaptureLostEvent, OnTopLevelPointerCaptureLost);
            _hookedTopLevel = null;
        }

        private void StartAnimation()
        {
            if (_animationTimer.IsEnabled)
                return;

            _lastAnimationTickUtc = DateTime.UtcNow;
            _animationTimer.Start();
        }

        private void InvalidateSubscriberVisual()
        {
            LiquidGlassBackdropProvider.NotifySubscriberOnlyInvalidation(this);
            InvalidateVisual();
        }

        private void ApplyDeformation()
        {
            if (!IsInteractive || Bounds.Width <= 0 || Bounds.Height <= 0)
            {
                RenderTransform = null;
                return;
            }

            double width = Bounds.Width;
            double height = Bounds.Height;

            double progress = Clamp(_pressProgress.Value, 0.0, 1.0);
            double maxScale = Math.Max(0.0, InteractiveMaxScaleDip);

            // Matches LiquidButton.kt layerBlock math (dp converted to px there; DIPs here).
            double scale = Lerp(1.0, 1.0 + maxScale / height, progress);

            Point offset = _position.Value - _startPosition;

            double minDim = Math.Min(width, height);
            double maxDim = Math.Max(width, height);

            double tx = minDim * Math.Tanh(InitialDerivative * offset.X / minDim);
            double ty = minDim * Math.Tanh(InitialDerivative * offset.Y / minDim);

            double maxDragScale = maxScale / height;
            double angle = Math.Atan2(offset.Y, offset.X);

            double aspectX = Math.Min(width / height, 1.0);
            double aspectY = Math.Min(height / width, 1.0);

            double sx =
                scale +
                maxDragScale * Math.Abs(Math.Cos(angle) * offset.X / maxDim) * aspectX;

            double sy =
                scale +
                maxDragScale * Math.Abs(Math.Sin(angle) * offset.Y / maxDim) * aspectY;

            if (Math.Abs(tx) < 0.01 && Math.Abs(ty) < 0.01 && Math.Abs(sx - 1.0) < 0.0005 && Math.Abs(sy - 1.0) < 0.0005)
                RenderTransform = null;
            else
                RenderTransform = new MatrixTransform(Matrix.CreateScale(sx, sy) * Matrix.CreateTranslation(tx, ty));
        }

        internal double GetInteractiveHighlightProgress()
        {
            if (!IsInteractive || !InteractiveHighlightEnabled)
                return 0.0;

            return Clamp(_pressProgress.Value, 0.0, 1.0);
        }

        internal Point GetInteractiveHighlightPosition()
        {
            return _position.Value;
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == IsInteractiveProperty
                || change.Property == InteractiveHighlightEnabledProperty
                || change.Property == InteractiveMaxScaleDipProperty)
            {
                LiquidGlassBackdropProvider.NotifySubscriberOnlyInvalidation(this);
                InteractiveOverlay?.InvalidateVisual();
            }
        }

        private static double Lerp(double a, double b, double t)
        {
            return a + (b - a) * t;
        }

        private static double Clamp(double value, double min, double max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private sealed class SpringDouble
        {
            private readonly double _stiffness;
            private readonly double _dampingRatio;
            private readonly double _threshold;

            public SpringDouble(double stiffness, double dampingRatio, double threshold)
            {
                _stiffness = stiffness;
                _dampingRatio = dampingRatio;
                _threshold = threshold;
            }

            public double Value { get; private set; }
            public double Velocity { get; private set; }
            public double Target { get; set; }

            public void SnapTo(double value)
            {
                Value = value;
                Velocity = 0.0;
            }

            public bool Step(double dt)
            {
                double x = Value;
                double v = Velocity;
                double target = Target;

                double k = _stiffness;
                double c = 2.0 * _dampingRatio * Math.Sqrt(k);

                double a = -k * (x - target) - c * v;
                v += a * dt;
                x += v * dt;

                Value = x;
                Velocity = v;

                bool done = Math.Abs(x - target) <= _threshold && Math.Abs(v) <= _threshold;
                if (done)
                {
                    Value = target;
                    Velocity = 0.0;
                }

                return !done;
            }
        }

        private sealed class SpringPoint
        {
            private readonly SpringDouble _x;
            private readonly SpringDouble _y;

            public SpringPoint(double stiffness, double dampingRatio, double positionThreshold)
            {
                _x = new SpringDouble(stiffness, dampingRatio, positionThreshold);
                _y = new SpringDouble(stiffness, dampingRatio, positionThreshold);
            }

            public Point Value
            {
                get => new(_x.Value, _y.Value);
            }

            public Point Target
            {
                get => new(_x.Target, _y.Target);
                set
                {
                    _x.Target = value.X;
                    _y.Target = value.Y;
                }
            }

            public void SnapTo(Point value)
            {
                _x.SnapTo(value.X);
                _y.SnapTo(value.Y);
            }

            public bool Step(double dt)
            {
                return _x.Step(dt) | _y.Step(dt);
            }
        }
    }
}
```

### `MainSrc\Tools\LiquidGlassAvaloniaUI\LiquidGlassOverlays.cs`

```csharp
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace LiquidGlassAvaloniaUI
{
    internal sealed class LiquidGlassInteractiveOverlay : Control
    {
        public override void Render(DrawingContext context)
        {
            if (LiquidGlassBackdropProvider.IsCapturing)
                return;

            if (TemplatedParent is not LiquidGlassInteractiveSurface surface)
                return;

            if (!surface.InteractiveHighlightEnabled)
                return;

            double progress = surface.GetInteractiveHighlightProgress();
            if (progress <= 0.001)
                return;

            Rect bounds = new(0, 0, Bounds.Width, Bounds.Height);
            LiquidGlassDrawParameters parameters = surface.CreateDrawParameters();
            parameters.InteractiveProgress = progress;
            parameters.InteractivePosition = surface.GetInteractiveHighlightPosition();

            context.Custom(new LiquidGlassDrawOperation(bounds, parameters, null, LiquidGlassDrawPass.InteractiveHighlight));
        }
    }

    internal sealed class LiquidGlassFrontOverlay : Control
    {
        public override void Render(DrawingContext context)
        {
            if (LiquidGlassBackdropProvider.IsCapturing)
                return;

            if (TemplatedParent is not LiquidGlassSurface surface)
                return;

            Rect bounds = new(0, 0, Bounds.Width, Bounds.Height);
            LiquidGlassDrawParameters parameters = surface.CreateDrawParameters();

            if (surface.HighlightEnabled)
                context.Custom(new LiquidGlassDrawOperation(bounds, parameters, null, LiquidGlassDrawPass.Highlight));

            if (surface.InnerShadowEnabled && parameters.InnerShadowOpacity > 0.001 && parameters.InnerShadowColor.A > 0)
                context.Custom(new LiquidGlassInnerShadowDrawOperation(bounds, parameters));
        }
    }
}
```

### `MainSrc\Tools\LiquidGlassAvaloniaUI\LiquidGlassPathUtils.cs`

```csharp
using System;
using Avalonia;
using Avalonia.Media;
using SkiaSharp;

namespace LiquidGlassAvaloniaUI
{
    internal static class LiquidGlassPathUtils
    {
        public static float[] GetCornerRadii(CornerRadius cornerRadius, float maxRadius)
        {
            float tl = (float)Clamp(cornerRadius.TopLeft, 0.0, maxRadius);
            float tr = (float)Clamp(cornerRadius.TopRight, 0.0, maxRadius);
            float br = (float)Clamp(cornerRadius.BottomRight, 0.0, maxRadius);
            float bl = (float)Clamp(cornerRadius.BottomLeft, 0.0, maxRadius);
            return new[]
            {
                tl, tr, br, bl
            };
        }

        public static SKPath CreateRoundRectPath(SKRect rect, float[] cornerRadii)
        {
            using SKRoundRect rr = new();
            rr.SetRectRadii(rect, new[]
            {
                new SKPoint(cornerRadii[0], cornerRadii[0]), new SKPoint(cornerRadii[1], cornerRadii[1]), new SKPoint(cornerRadii[2], cornerRadii[2]), new SKPoint(cornerRadii[3], cornerRadii[3])
            });

            SKPath path = new();
            path.AddRoundRect(rr, SKPathDirection.Clockwise);
            return path;
        }

        private static double Clamp(double value, double min, double max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}
```

### `MainSrc\Tools\LiquidGlassAvaloniaUI\LiquidGlassShadowDrawOperations.cs`

```csharp
using System;
using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using SkiaSharp;

namespace LiquidGlassAvaloniaUI
{
    internal sealed class LiquidGlassShadowDrawOperation : ICustomDrawOperation
    {
        private readonly Rect _controlBounds;
        private readonly Rect _operationBounds;
        private readonly LiquidGlassDrawParameters _parameters;

        public LiquidGlassShadowDrawOperation(Rect controlBounds, LiquidGlassDrawParameters parameters)
        {
            _controlBounds = controlBounds;
            _parameters = parameters;

            double radius = Math.Max(0.0, parameters.ShadowRadius);
            Vector offset = parameters.ShadowOffset;
            double pad = radius * 2.0;

            double leftPad = pad + Math.Max(0.0, -offset.X);
            double topPad = pad + Math.Max(0.0, -offset.Y);
            double rightPad = pad + Math.Max(0.0, offset.X);
            double bottomPad = pad + Math.Max(0.0, offset.Y);

            _operationBounds = new Rect(
                -leftPad,
                -topPad,
                controlBounds.Width + leftPad + rightPad,
                controlBounds.Height + topPad + bottomPad);
        }

        public void Dispose()
        {
        }

        public bool HitTest(Point p)
        {
            return false;
        }

        public Rect Bounds
        {
            get => _operationBounds;
        }

        public bool Equals(ICustomDrawOperation? other)
        {
            return false;
        }

        public void Render(ImmediateDrawingContext context)
        {
            ISkiaSharpApiLeaseFeature? leaseFeature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
            if (leaseFeature is null)
                return;

            using ISkiaSharpApiLease lease = leaseFeature.Lease();
            SKCanvas canvas = lease.SkCanvas;

            SKSize size = new((float)_controlBounds.Width, (float)_controlBounds.Height);
            if (size.Width <= 0 || size.Height <= 0)
                return;

            float radius = (float)Clamp(_parameters.ShadowRadius, 0.0, 512.0);
            if (radius <= 0.001f)
                return;

            float opacity = (float)Clamp(_parameters.ShadowOpacity, 0.0, 1.0);
            Color color = _parameters.ShadowColor;
            byte alpha = (byte)Clamp(color.A * opacity, 0.0, 255.0);
            if (alpha <= 0)
                return;

            float offsetX = (float)_parameters.ShadowOffset.X;
            float offsetY = (float)_parameters.ShadowOffset.Y;

            float maxRadius = Math.Min(size.Width, size.Height) * 0.5f;
            float[] cornerRadii = LiquidGlassPathUtils.GetCornerRadii(_parameters.CornerRadius, maxRadius);
            SKRect rect = SKRect.Create(0, 0, size.Width, size.Height);

            using SKPath path = LiquidGlassPathUtils.CreateRoundRectPath(rect, cornerRadii);
            using SKMaskFilter? blur = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, radius);

            using SKPaint shadowPaint = new()
            {
                Color = new SKColor(color.R, color.G, color.B, alpha),
                MaskFilter = blur,
                IsAntialias = true
            };

            using SKPaint clearPaint = new()
            {
                BlendMode = SKBlendMode.Clear,
                IsAntialias = true
            };

            float pad = radius * 2.0f;
            SKRect layerBounds = SKRect.Create(-pad, -pad, size.Width + pad * 2.0f, size.Height + pad * 2.0f);

            canvas.SaveLayer(layerBounds, null);

            canvas.Save();
            canvas.Translate(offsetX, offsetY);
            canvas.DrawPath(path, shadowPaint);
            canvas.Restore();

            canvas.DrawPath(path, clearPaint);

            canvas.Restore();
        }

        private static double Clamp(double value, double min, double max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }

    internal sealed class LiquidGlassInnerShadowDrawOperation : ICustomDrawOperation
    {
        private readonly Rect _bounds;
        private readonly LiquidGlassDrawParameters _parameters;

        public LiquidGlassInnerShadowDrawOperation(Rect bounds, LiquidGlassDrawParameters parameters)
        {
            _bounds = bounds;
            _parameters = parameters;
        }

        public void Dispose()
        {
        }

        public bool HitTest(Point p)
        {
            return false;
        }

        public Rect Bounds
        {
            get => _bounds;
        }

        public bool Equals(ICustomDrawOperation? other)
        {
            return false;
        }

        public void Render(ImmediateDrawingContext context)
        {
            ISkiaSharpApiLeaseFeature? leaseFeature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
            if (leaseFeature is null)
                return;

            using ISkiaSharpApiLease lease = leaseFeature.Lease();
            SKCanvas canvas = lease.SkCanvas;

            SKSize size = new((float)_bounds.Width, (float)_bounds.Height);
            if (size.Width <= 0 || size.Height <= 0)
                return;

            float radius = (float)Clamp(_parameters.InnerShadowRadius, 0.0, 512.0);
            if (radius <= 0.001f)
                return;

            float opacity = (float)Clamp(_parameters.InnerShadowOpacity, 0.0, 1.0);
            Color color = _parameters.InnerShadowColor;
            byte alpha = (byte)Clamp(color.A * opacity, 0.0, 255.0);
            if (alpha <= 0)
                return;

            float offsetX = (float)_parameters.InnerShadowOffset.X;
            float offsetY = (float)_parameters.InnerShadowOffset.Y;

            float maxRadius = Math.Min(size.Width, size.Height) * 0.5f;
            float[] cornerRadii = LiquidGlassPathUtils.GetCornerRadii(_parameters.CornerRadius, maxRadius);
            SKRect rect = SKRect.Create(0, 0, size.Width, size.Height);

            using SKPath path = LiquidGlassPathUtils.CreateRoundRectPath(rect, cornerRadii);

            using SKImageFilter blur = SKImageFilter.CreateBlur(radius, radius, SKShaderTileMode.Decal, null, rect);
            using SKPaint layerPaint = new()
            {
                ImageFilter = blur
            };

            using SKPaint fillPaint = new()
            {
                Color = new SKColor(color.R, color.G, color.B, alpha),
                IsAntialias = true
            };

            using SKPaint clearPaint = new()
            {
                BlendMode = SKBlendMode.Clear,
                IsAntialias = true
            };

            canvas.Save();
            canvas.ClipPath(path, SKClipOperation.Intersect, true);

            canvas.SaveLayer(rect, layerPaint);
            canvas.DrawPath(path, fillPaint);
            canvas.Translate(offsetX, offsetY);
            canvas.DrawPath(path, clearPaint);
            canvas.Translate(-offsetX, -offsetY);
            canvas.Restore();

            canvas.Restore();
        }

        private static double Clamp(double value, double min, double max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}
```

### `MainSrc\Tools\LiquidGlassAvaloniaUI\LiquidGlassSurface.cs`

```csharp
using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SkiaSharp;

namespace LiquidGlassAvaloniaUI
{
    /// <summary>
    /// A composited “liquid glass” surface:
    /// vibrancy + blur + lens refraction + (optional) highlights + shadows.
    /// </summary>
    public class LiquidGlassSurface : ContentControl
    {
        public static readonly StyledProperty<double> BackdropZoomProperty =
            AvaloniaProperty.Register<LiquidGlassSurface, double>(nameof(BackdropZoom), 1.0);

        public static readonly StyledProperty<Vector> BackdropOffsetProperty =
            AvaloniaProperty.Register<LiquidGlassSurface, Vector>(nameof(BackdropOffset), new Vector(0.0, 0.0));

        public static readonly StyledProperty<double> RefractionHeightProperty =
            AvaloniaProperty.Register<LiquidGlassSurface, double>(nameof(RefractionHeight), 12.0);

        public static readonly StyledProperty<double> RefractionAmountProperty =
            AvaloniaProperty.Register<LiquidGlassSurface, double>(nameof(RefractionAmount), 24.0);

        public static readonly StyledProperty<bool> DepthEffectProperty =
            AvaloniaProperty.Register<LiquidGlassSurface, bool>(nameof(DepthEffect), false);

        public static readonly StyledProperty<bool> ChromaticAberrationProperty =
            AvaloniaProperty.Register<LiquidGlassSurface, bool>(nameof(ChromaticAberration), false);

        public static readonly StyledProperty<double> BlurRadiusProperty =
            AvaloniaProperty.Register<LiquidGlassSurface, double>(nameof(BlurRadius), 2.0);

        public static readonly StyledProperty<double> VibrancyProperty =
            AvaloniaProperty.Register<LiquidGlassSurface, double>(nameof(Vibrancy), 1.5);

        public static readonly StyledProperty<double> BrightnessProperty =
            AvaloniaProperty.Register<LiquidGlassSurface, double>(nameof(Brightness), 0.0);

        public static readonly StyledProperty<double> ContrastProperty =
            AvaloniaProperty.Register<LiquidGlassSurface, double>(nameof(Contrast), 1.0);

        public static readonly StyledProperty<double> ExposureEvProperty =
            AvaloniaProperty.Register<LiquidGlassSurface, double>(nameof(ExposureEv), 0.0);

        public static readonly StyledProperty<double> GammaPowerProperty =
            AvaloniaProperty.Register<LiquidGlassSurface, double>(nameof(GammaPower), 1.0);

        public static readonly StyledProperty<double> BackdropOpacityProperty =
            AvaloniaProperty.Register<LiquidGlassSurface, double>(nameof(BackdropOpacity), 1.0);

        public static readonly StyledProperty<Color> TintColorProperty =
            AvaloniaProperty.Register<LiquidGlassSurface, Color>(nameof(TintColor), Colors.Transparent);

        public static readonly StyledProperty<Color> SurfaceColorProperty =
            AvaloniaProperty.Register<LiquidGlassSurface, Color>(nameof(SurfaceColor), Colors.Transparent);

        public static readonly StyledProperty<bool> ProgressiveBlurEnabledProperty =
            AvaloniaProperty.Register<LiquidGlassSurface, bool>(nameof(ProgressiveBlurEnabled), false);

        /// <summary>
        /// Progressive blur start position (0..1, relative to control height).
        /// Above this fraction the blur is fully visible.
        /// </summary>
        public static readonly StyledProperty<double> ProgressiveBlurStartProperty =
            AvaloniaProperty.Register<LiquidGlassSurface, double>(nameof(ProgressiveBlurStart), 0.5);

        /// <summary>
        /// Progressive blur end position (0..1, relative to control height).
        /// Below this fraction the blur fades out to fully transparent.
        /// </summary>
        public static readonly StyledProperty<double> ProgressiveBlurEndProperty =
            AvaloniaProperty.Register<LiquidGlassSurface, double>(nameof(ProgressiveBlurEnd), 1.0);

        public static readonly StyledProperty<Color> ProgressiveTintColorProperty =
            AvaloniaProperty.Register<LiquidGlassSurface, Color>(nameof(ProgressiveTintColor), Colors.Transparent);

        public static readonly StyledProperty<double> ProgressiveTintIntensityProperty =
            AvaloniaProperty.Register<LiquidGlassSurface, double>(nameof(ProgressiveTintIntensity), 0.8);

        public static readonly StyledProperty<bool> AdaptiveLuminanceEnabledProperty =
            AvaloniaProperty.Register<LiquidGlassSurface, bool>(nameof(AdaptiveLuminanceEnabled), false);

        public static readonly StyledProperty<double> AdaptiveLuminanceUpdateIntervalMsProperty =
            AvaloniaProperty.Register<LiquidGlassSurface, double>(nameof(AdaptiveLuminanceUpdateIntervalMs), 250.0);

        public static readonly StyledProperty<double> AdaptiveLuminanceSmoothingProperty =
            AvaloniaProperty.Register<LiquidGlassSurface, double>(nameof(AdaptiveLuminanceSmoothing), 0.2);

        public static readonly DirectProperty<LiquidGlassSurface, double> AdaptiveLuminanceProperty =
            AvaloniaProperty.RegisterDirect<LiquidGlassSurface, double>(
                nameof(AdaptiveLuminance),
                o => o.AdaptiveLuminance);

        public static readonly StyledProperty<bool> HighlightEnabledProperty =
            AvaloniaProperty.Register<LiquidGlassSurface, bool>(nameof(HighlightEnabled), true);

        public static readonly StyledProperty<double> HighlightWidthProperty =
            AvaloniaProperty.Register<LiquidGlassSurface, double>(nameof(HighlightWidth), 0.5);

        public static readonly StyledProperty<double> HighlightBlurRadiusProperty =
            AvaloniaProperty.Register<LiquidGlassSurface, double>(nameof(HighlightBlurRadius), 0.25);

        public static readonly StyledProperty<double> HighlightOpacityProperty =
            AvaloniaProperty.Register<LiquidGlassSurface, double>(nameof(HighlightOpacity), 0.5);

        public static readonly StyledProperty<double> HighlightAngleProperty =
            AvaloniaProperty.Register<LiquidGlassSurface, double>(nameof(HighlightAngle), 45.0);

        public static readonly StyledProperty<double> HighlightFalloffProperty =
            AvaloniaProperty.Register<LiquidGlassSurface, double>(nameof(HighlightFalloff), 1.0);

        public static readonly StyledProperty<bool> ShadowEnabledProperty =
            AvaloniaProperty.Register<LiquidGlassSurface, bool>(nameof(ShadowEnabled), true);

        public static readonly StyledProperty<double> ShadowRadiusProperty =
            AvaloniaProperty.Register<LiquidGlassSurface, double>(nameof(ShadowRadius), 24.0);

        public static readonly StyledProperty<Vector> ShadowOffsetProperty =
            AvaloniaProperty.Register<LiquidGlassSurface, Vector>(nameof(ShadowOffset), new Vector(0.0, 4.0));

        public static readonly StyledProperty<Color> ShadowColorProperty =
            AvaloniaProperty.Register<LiquidGlassSurface, Color>(nameof(ShadowColor), Color.FromArgb(26, 0, 0, 0));

        public static readonly StyledProperty<double> ShadowOpacityProperty =
            AvaloniaProperty.Register<LiquidGlassSurface, double>(nameof(ShadowOpacity), 1.0);

        public static readonly StyledProperty<bool> InnerShadowEnabledProperty =
            AvaloniaProperty.Register<LiquidGlassSurface, bool>(nameof(InnerShadowEnabled), false);

        public static readonly StyledProperty<double> InnerShadowRadiusProperty =
            AvaloniaProperty.Register<LiquidGlassSurface, double>(nameof(InnerShadowRadius), 24.0);

        public static readonly StyledProperty<Vector> InnerShadowOffsetProperty =
            AvaloniaProperty.Register<LiquidGlassSurface, Vector>(nameof(InnerShadowOffset), new Vector(0.0, 24.0));

        public static readonly StyledProperty<Color> InnerShadowColorProperty =
            AvaloniaProperty.Register<LiquidGlassSurface, Color>(nameof(InnerShadowColor), Color.FromArgb(38, 0, 0, 0));

        public static readonly StyledProperty<double> InnerShadowOpacityProperty =
            AvaloniaProperty.Register<LiquidGlassSurface, double>(nameof(InnerShadowOpacity), 1.0);

        static LiquidGlassSurface()
        {
            // TemplatedControl defaults ClipToBounds=true, which would clip the shadow pass to the control bounds.
            // We clip the content via the template's Border and clip shader passes explicitly.
            ClipToBoundsProperty.OverrideDefaultValue<LiquidGlassSurface>(false);

            AffectsRender<LiquidGlassSurface>(
                CornerRadiusProperty,
                BackdropZoomProperty,
                BackdropOffsetProperty,
                RefractionHeightProperty,
                RefractionAmountProperty,
                DepthEffectProperty,
                ChromaticAberrationProperty,
                BlurRadiusProperty,
                VibrancyProperty,
                BrightnessProperty,
                ContrastProperty,
                ExposureEvProperty,
                GammaPowerProperty,
                BackdropOpacityProperty,
                TintColorProperty,
                SurfaceColorProperty,
                ProgressiveBlurEnabledProperty,
                ProgressiveBlurStartProperty,
                ProgressiveBlurEndProperty,
                ProgressiveTintColorProperty,
                ProgressiveTintIntensityProperty,
                AdaptiveLuminanceEnabledProperty,
                AdaptiveLuminanceUpdateIntervalMsProperty,
                AdaptiveLuminanceSmoothingProperty,
                ShadowEnabledProperty,
                ShadowRadiusProperty,
                ShadowOffsetProperty,
                ShadowColorProperty,
                ShadowOpacityProperty);

            TemplateProperty.OverrideDefaultValue<LiquidGlassSurface>(CreateDefaultTemplate());
        }

        internal LiquidGlassInteractiveOverlay? InteractiveOverlay { get; private set; }
        internal LiquidGlassFrontOverlay? FrontOverlay { get; private set; }

        private DispatcherTimer? _adaptiveLuminanceTimer;
        private EventHandler? _adaptiveLuminanceTickHandler;
        private double _adaptiveLuminance;

        // ★ 修复：已订阅 Changed 的变换列表（RenderTransform 本身，以及 TransformGroup 下的子变换）
        private readonly List<Transform> _observedTransforms = new();
        private Transform? _observedRootTransform;

        /// <summary>
        /// Last sampled backdrop luminance (0..1) when <see cref="AdaptiveLuminanceEnabled"/> is true.
        /// </summary>
        public double AdaptiveLuminance
        {
            get => _adaptiveLuminance;
            private set => SetAndRaise(AdaptiveLuminanceProperty, ref _adaptiveLuminance, value);
        }

        public Control? Child
        {
            get => Content as Control;
            set => Content = value;
        }

        /// <summary>
        /// Magnifier-style zoom applied to backdrop sampling (1 = no zoom).
        /// </summary>
        public double BackdropZoom
        {
            get => GetValue(BackdropZoomProperty);
            set => SetValue(BackdropZoomProperty, value);
        }

        /// <summary>
        /// Additional translation applied to the sampled backdrop (in DIPs).
        /// </summary>
        public Vector BackdropOffset
        {
            get => GetValue(BackdropOffsetProperty);
            set => SetValue(BackdropOffsetProperty, value);
        }

        public double RefractionHeight
        {
            get => GetValue(RefractionHeightProperty);
            set => SetValue(RefractionHeightProperty, value);
        }

        public double RefractionAmount
        {
            get => GetValue(RefractionAmountProperty);
            set => SetValue(RefractionAmountProperty, value);
        }

        public bool DepthEffect
        {
            get => GetValue(DepthEffectProperty);
            set => SetValue(DepthEffectProperty, value);
        }

        public bool ChromaticAberration
        {
            get => GetValue(ChromaticAberrationProperty);
            set => SetValue(ChromaticAberrationProperty, value);
        }

        public double BlurRadius
        {
            get => GetValue(BlurRadiusProperty);
            set => SetValue(BlurRadiusProperty, value);
        }

        public double Vibrancy
        {
            get => GetValue(VibrancyProperty);
            set => SetValue(VibrancyProperty, value);
        }

        public double Saturation
        {
            get => Vibrancy;
            set => Vibrancy = value;
        }

        public double Brightness
        {
            get => GetValue(BrightnessProperty);
            set => SetValue(BrightnessProperty, value);
        }

        public double Contrast
        {
            get => GetValue(ContrastProperty);
            set => SetValue(ContrastProperty, value);
        }

        public double ExposureEv
        {
            get => GetValue(ExposureEvProperty);
            set => SetValue(ExposureEvProperty, value);
        }

        public double GammaPower
        {
            get => GetValue(GammaPowerProperty);
            set => SetValue(GammaPowerProperty, value);
        }

        public double BackdropOpacity
        {
            get => GetValue(BackdropOpacityProperty);
            set => SetValue(BackdropOpacityProperty, value);
        }

        public Color TintColor
        {
            get => GetValue(TintColorProperty);
            set => SetValue(TintColorProperty, value);
        }

        public Color SurfaceColor
        {
            get => GetValue(SurfaceColorProperty);
            set => SetValue(SurfaceColorProperty, value);
        }

        /// <summary>
        /// Enables the progressive blur mask stage.
        /// </summary>
        public bool ProgressiveBlurEnabled
        {
            get => GetValue(ProgressiveBlurEnabledProperty);
            set => SetValue(ProgressiveBlurEnabledProperty, value);
        }

        public double ProgressiveBlurStart
        {
            get => GetValue(ProgressiveBlurStartProperty);
            set => SetValue(ProgressiveBlurStartProperty, value);
        }

        public double ProgressiveBlurEnd
        {
            get => GetValue(ProgressiveBlurEndProperty);
            set => SetValue(ProgressiveBlurEndProperty, value);
        }

        public Color ProgressiveTintColor
        {
            get => GetValue(ProgressiveTintColorProperty);
            set => SetValue(ProgressiveTintColorProperty, value);
        }

        public double ProgressiveTintIntensity
        {
            get => GetValue(ProgressiveTintIntensityProperty);
            set => SetValue(ProgressiveTintIntensityProperty, value);
        }

        /// <summary>
        /// Enables adaptive luminance mode which samples the backdrop behind this surface and adjusts
        /// rendering parameters to improve legibility.
        /// </summary>
        public bool AdaptiveLuminanceEnabled
        {
            get => GetValue(AdaptiveLuminanceEnabledProperty);
            set => SetValue(AdaptiveLuminanceEnabledProperty, value);
        }

        public double AdaptiveLuminanceUpdateIntervalMs
        {
            get => GetValue(AdaptiveLuminanceUpdateIntervalMsProperty);
            set => SetValue(AdaptiveLuminanceUpdateIntervalMsProperty, value);
        }

        public double AdaptiveLuminanceSmoothing
        {
            get => GetValue(AdaptiveLuminanceSmoothingProperty);
            set => SetValue(AdaptiveLuminanceSmoothingProperty, value);
        }

        public bool HighlightEnabled
        {
            get => GetValue(HighlightEnabledProperty);
            set => SetValue(HighlightEnabledProperty, value);
        }

        public double HighlightWidth
        {
            get => GetValue(HighlightWidthProperty);
            set => SetValue(HighlightWidthProperty, value);
        }

        public double HighlightBlurRadius
        {
            get => GetValue(HighlightBlurRadiusProperty);
            set => SetValue(HighlightBlurRadiusProperty, value);
        }

        public double HighlightOpacity
        {
            get => GetValue(HighlightOpacityProperty);
            set => SetValue(HighlightOpacityProperty, value);
        }

        public double HighlightAngle
        {
            get => GetValue(HighlightAngleProperty);
            set => SetValue(HighlightAngleProperty, value);
        }

        public double HighlightFalloff
        {
            get => GetValue(HighlightFalloffProperty);
            set => SetValue(HighlightFalloffProperty, value);
        }

        public bool ShadowEnabled
        {
            get => GetValue(ShadowEnabledProperty);
            set => SetValue(ShadowEnabledProperty, value);
        }

        public double ShadowRadius
        {
            get => GetValue(ShadowRadiusProperty);
            set => SetValue(ShadowRadiusProperty, value);
        }

        public Vector ShadowOffset
        {
            get => GetValue(ShadowOffsetProperty);
            set => SetValue(ShadowOffsetProperty, value);
        }

        public Color ShadowColor
        {
            get => GetValue(ShadowColorProperty);
            set => SetValue(ShadowColorProperty, value);
        }

        public double ShadowOpacity
        {
            get => GetValue(ShadowOpacityProperty);
            set => SetValue(ShadowOpacityProperty, value);
        }

        public bool InnerShadowEnabled
        {
            get => GetValue(InnerShadowEnabledProperty);
            set => SetValue(InnerShadowEnabledProperty, value);
        }

        public double InnerShadowRadius
        {
            get => GetValue(InnerShadowRadiusProperty);
            set => SetValue(InnerShadowRadiusProperty, value);
        }

        public Vector InnerShadowOffset
        {
            get => GetValue(InnerShadowOffsetProperty);
            set => SetValue(InnerShadowOffsetProperty, value);
        }

        public Color InnerShadowColor
        {
            get => GetValue(InnerShadowColorProperty);
            set => SetValue(InnerShadowColorProperty, value);
        }

        public double InnerShadowOpacity
        {
            get => GetValue(InnerShadowOpacityProperty);
            set => SetValue(InnerShadowOpacityProperty, value);
        }

        public override void Render(DrawingContext context)
        {
            if (LiquidGlassBackdropProvider.IsCapturing)
                return;

            if (Bounds.Width <= 0 || Bounds.Height <= 0)
                return;

            LiquidGlassDrawParameters parameters = CreateDrawParameters();
            Rect controlBounds = new(0, 0, Bounds.Width, Bounds.Height);

            if (ShadowEnabled && ShadowOpacity > 0.001 && ShadowColor.A > 0 && ShadowRadius > 0.001)
                context.Custom(new LiquidGlassShadowDrawOperation(controlBounds, parameters));

            LiquidGlassBackdropProvider.EnsureSnapshot(this);
            LiquidGlassBackdropSnapshot? snapshot = LiquidGlassBackdropProvider.TryGetSnapshot(this);

            context.Custom(new LiquidGlassDrawOperation(controlBounds, parameters, snapshot, LiquidGlassDrawPass.Lens));
        }

        internal LiquidGlassDrawParameters CreateDrawParameters()
        {
            LiquidGlassDrawParameters parameters = new()
            {
                CornerRadius = CornerRadius,
                BackdropZoom = BackdropZoom,
                BackdropOffset = BackdropOffset,
                RefractionHeight = RefractionHeight,
                RefractionAmount = RefractionAmount,
                DepthEffect = DepthEffect,
                ChromaticAberration = ChromaticAberration,
                BlurRadius = BlurRadius,
                Vibrancy = Vibrancy,
                Brightness = Brightness,
                Contrast = Contrast,
                ExposureEv = ExposureEv,
                GammaPower = GammaPower,
                BackdropOpacity = BackdropOpacity,
                TintColor = TintColor,
                SurfaceColor = SurfaceColor,
                ProgressiveBlurEnabled = ProgressiveBlurEnabled,
                ProgressiveBlurStart = ProgressiveBlurStart,
                ProgressiveBlurEnd = ProgressiveBlurEnd,
                ProgressiveTintColor = ProgressiveTintColor,
                ProgressiveTintIntensity = ProgressiveTintIntensity,
                HighlightEnabled = HighlightEnabled,
                HighlightWidth = HighlightWidth,
                HighlightBlurRadius = HighlightBlurRadius,
                HighlightOpacity = HighlightOpacity,
                HighlightAngleDegrees = HighlightAngle,
                HighlightFalloff = HighlightFalloff,
                ShadowEnabled = ShadowEnabled,
                ShadowRadius = ShadowRadius,
                ShadowOffset = ShadowOffset,
                ShadowColor = ShadowColor,
                ShadowOpacity = ShadowOpacity,
                InnerShadowEnabled = InnerShadowEnabled,
                InnerShadowRadius = InnerShadowRadius,
                InnerShadowOffset = InnerShadowOffset,
                InnerShadowColor = InnerShadowColor,
                InnerShadowOpacity = InnerShadowOpacity
            };

            if (AdaptiveLuminanceEnabled)
            {
                // Tone-mapping heuristic based on sampled backdrop luminance.
                double luminance = Clamp(AdaptiveLuminance, 0.0, 1.0);
                double l = luminance * 2.0 - 1.0;
                l = Math.Sign(l) * l * l;

                parameters.Vibrancy = 1.5;

                parameters.Brightness =
                    l > 0.0
                        ? Lerp(0.1, 0.5, l)
                        : Lerp(0.1, -0.2, -l);

                parameters.Contrast =
                    l > 0.0
                        ? Lerp(1.0, 0.0, l)
                        : 1.0;

                parameters.BlurRadius =
                    l > 0.0
                        ? Lerp(8.0, 16.0, l)
                        : Lerp(8.0, 2.0, -l);
            }

            return parameters;
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            InteractiveOverlay = e.NameScope.Find<LiquidGlassInteractiveOverlay>("PART_InteractiveOverlay");
            FrontOverlay = e.NameScope.Find<LiquidGlassFrontOverlay>("PART_FrontOverlay");
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            // ★ 修复：RenderTransform 被整体替换时重新订阅，并立即按新位置校验快照覆盖范围
            if (change.Property == RenderTransformProperty)
            {
                ObserveRenderTransform();
                LiquidGlassBackdropProvider.EnsureSnapshot(this);
            }

            if (IsSubscriberOnlyRenderProperty(change.Property))
                LiquidGlassBackdropProvider.NotifySubscriberOnlyInvalidation(this);

            if (change.Property == CornerRadiusProperty)
            {
                InteractiveOverlay?.InvalidateVisual();
                FrontOverlay?.InvalidateVisual();
                return;
            }

            if (change.Property == HighlightEnabledProperty
                || change.Property == HighlightWidthProperty
                || change.Property == HighlightBlurRadiusProperty
                || change.Property == HighlightOpacityProperty
                || change.Property == HighlightAngleProperty
                || change.Property == HighlightFalloffProperty
                || change.Property == InnerShadowEnabledProperty
                || change.Property == InnerShadowRadiusProperty
                || change.Property == InnerShadowOffsetProperty
                || change.Property == InnerShadowColorProperty
                || change.Property == InnerShadowOpacityProperty)
            {
                FrontOverlay?.InvalidateVisual();
                return;
            }

            if (change.Property == AdaptiveLuminanceEnabledProperty
                || change.Property == AdaptiveLuminanceUpdateIntervalMsProperty)
            {
                UpdateAdaptiveLuminanceTimer();
            }
        }

        private static bool IsSubscriberOnlyRenderProperty(AvaloniaProperty property)
        {
            return property == CornerRadiusProperty
                   || property == BackdropZoomProperty
                   || property == BackdropOffsetProperty
                   || property == RefractionHeightProperty
                   || property == RefractionAmountProperty
                   || property == DepthEffectProperty
                   || property == ChromaticAberrationProperty
                   || property == BlurRadiusProperty
                   || property == VibrancyProperty
                   || property == BrightnessProperty
                   || property == ContrastProperty
                   || property == ExposureEvProperty
                   || property == GammaPowerProperty
                   || property == BackdropOpacityProperty
                   || property == TintColorProperty
                   || property == SurfaceColorProperty
                   || property == ProgressiveBlurEnabledProperty
                   || property == ProgressiveBlurStartProperty
                   || property == ProgressiveBlurEndProperty
                   || property == ProgressiveTintColorProperty
                   || property == ProgressiveTintIntensityProperty
                   || property == AdaptiveLuminanceEnabledProperty
                   || property == AdaptiveLuminanceUpdateIntervalMsProperty
                   || property == AdaptiveLuminanceSmoothingProperty
                   || property == HighlightEnabledProperty
                   || property == HighlightWidthProperty
                   || property == HighlightBlurRadiusProperty
                   || property == HighlightOpacityProperty
                   || property == HighlightAngleProperty
                   || property == HighlightFalloffProperty
                   || property == ShadowEnabledProperty
                   || property == ShadowRadiusProperty
                   || property == ShadowOffsetProperty
                   || property == ShadowColorProperty
                   || property == ShadowOpacityProperty
                   || property == InnerShadowEnabledProperty
                   || property == InnerShadowRadiusProperty
                   || property == InnerShadowOffsetProperty
                   || property == InnerShadowColorProperty
                   || property == InnerShadowOpacityProperty;
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            UpdateAdaptiveLuminanceTimer();
            ObserveRenderTransform();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            ReleaseRenderTransform();
            StopAdaptiveLuminanceTimer();
            base.OnDetachedFromVisualTree(e);
        }

        // ============================================================
        // ★ 修复：渲染变换感知
        //
        // 宿主通过定时器逐帧修改 TranslateTransform.X / ScaleTransform 来移动玻璃控件。
        // 这类改动由合成器直接处理（Avalonia 的 Visual.RenderTransformChanged 只更新合成
        // 视觉的变换），不会重新录制绘制树，因此不会触发 SceneInvalidated，背景提供者也就
        // 无从得知控件已经移动 —— 快照会停在旧位置，导致只有快照覆盖范围内还有折射/模糊。
        //
        // 这里直接订阅变换自身的 Changed 事件，把"我移动了"主动告知提供者，使其按新位置重抓。
        // TransformGroup 是否会把子变换的 Changed 冒泡上来依实现而定，因此这里递归订阅，
        // 两种实现下都成立（重复通知无副作用：抓取请求会被 CaptureQueued 合并）。
        // ============================================================
        private void ObserveRenderTransform()
        {
            // Visual.RenderTransform 的静态类型是 ITransform；只有可变变换（Transform 及其派生，
            // 如 TransformGroup / TranslateTransform / ScaleTransform）才带 Changed 事件。
            // 若宿主设的是不可变变换，则无法观测，此时只能依赖常规失效路径。
            Transform? transform = RenderTransform as Transform;
            if (ReferenceEquals(_observedRootTransform, transform))
                return;

            ReleaseRenderTransform();
            _observedRootTransform = transform;
            SubscribeTransform(transform);
        }

        private void SubscribeTransform(Transform? transform)
        {
            if (transform is null)
                return;

            transform.Changed += OnObservedTransformChanged;
            _observedTransforms.Add(transform);

            if (transform is TransformGroup group)
            {
                foreach (Transform child in group.Children)
                    SubscribeTransform(child);
            }
        }

        private void ReleaseRenderTransform()
        {
            for (int i = 0; i < _observedTransforms.Count; i++)
                _observedTransforms[i].Changed -= OnObservedTransformChanged;

            _observedTransforms.Clear();
            _observedRootTransform = null;
        }

        private void OnObservedTransformChanged(object? sender, EventArgs e)
        {
            LiquidGlassBackdropProvider.EnsureSnapshot(this);
        }

        private void UpdateAdaptiveLuminanceTimer()
        {
            if (!this.IsAttachedToVisualTree())
            {
                StopAdaptiveLuminanceTimer();
                return;
            }

            if (AdaptiveLuminanceEnabled)
                StartAdaptiveLuminanceTimer();
            else
                StopAdaptiveLuminanceTimer();
        }

        private void StartAdaptiveLuminanceTimer()
        {
            if (_adaptiveLuminanceTimer is not null)
            {
                TimeSpan desired = TimeSpan.FromMilliseconds(Math.Max(16.0, AdaptiveLuminanceUpdateIntervalMs));
                if (_adaptiveLuminanceTimer.Interval != desired)
                    _adaptiveLuminanceTimer.Interval = desired;
                if (!_adaptiveLuminanceTimer.IsEnabled)
                    _adaptiveLuminanceTimer.Start();
                return;
            }

            _adaptiveLuminanceTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(Math.Max(16.0, AdaptiveLuminanceUpdateIntervalMs))
            };

            _adaptiveLuminanceTickHandler ??= (_, _) => TickAdaptiveLuminance();
            _adaptiveLuminanceTimer.Tick += _adaptiveLuminanceTickHandler;
            _adaptiveLuminanceTimer.Start();
        }

        private void StopAdaptiveLuminanceTimer()
        {
            if (_adaptiveLuminanceTimer is null)
                return;

            _adaptiveLuminanceTimer.Stop();
            if (_adaptiveLuminanceTickHandler is not null)
                _adaptiveLuminanceTimer.Tick -= _adaptiveLuminanceTickHandler;
            _adaptiveLuminanceTimer = null;
        }

        private void TickAdaptiveLuminance()
        {
            if (!AdaptiveLuminanceEnabled)
                return;

            if (LiquidGlassBackdropProvider.IsCapturing)
                return;

            LiquidGlassBackdropProvider.EnsureSnapshot(this);

            if (!TrySampleBackdropLuminance(out double sampled))
                return;

            double smoothing = Clamp(AdaptiveLuminanceSmoothing, 0.0, 1.0);
            double next = AdaptiveLuminance + (sampled - AdaptiveLuminance) * smoothing;
            if (Math.Abs(next - AdaptiveLuminance) > 0.0005)
            {
                AdaptiveLuminance = next;
                LiquidGlassBackdropProvider.NotifySubscriberOnlyInvalidation(this);
                InvalidateVisual();
            }
        }

        private bool TrySampleBackdropLuminance(out double luminance)
        {
            luminance = 0.0;

            TopLevel? topLevel = TopLevel.GetTopLevel(this);
            if (topLevel is null)
                return false;

            LiquidGlassBackdropSnapshot? snapshot = LiquidGlassBackdropProvider.TryGetSnapshot(this);
            if (snapshot is null || !snapshot.TryAddLease())
                return false;

            try
            {
                if (!TryGetBoundsInTopLevelPixels(topLevel, out PixelRect boundsPx))
                    return false;

                PixelRect rel = new(
                    new PixelPoint(boundsPx.X - snapshot.OriginInPixels.X, boundsPx.Y - snapshot.OriginInPixels.Y),
                    boundsPx.Size);

                int imgW = snapshot.Image.Width;
                int imgH = snapshot.Image.Height;

                int x0 = Clamp((int)rel.X, 0, imgW - 1);
                int y0 = Clamp((int)rel.Y, 0, imgH - 1);
                int x1 = Clamp((int)rel.Right, 0, imgW);
                int y1 = Clamp((int)rel.Bottom, 0, imgH);
                int w = x1 - x0;
                int h = y1 - y0;
                if (w <= 1 || h <= 1)
                    return false;

                using SKPixmap? pixmap = snapshot.Image.PeekPixels();
                using SKBitmap? fallbackBitmap = pixmap is null
                    ? new SKBitmap(new SKImageInfo(imgW, imgH, SKColorType.Bgra8888, SKAlphaType.Premul))
                    : null;

                if (pixmap is null
                    && !snapshot.Image.ReadPixels(fallbackBitmap!.Info, fallbackBitmap.GetPixels(), fallbackBitmap.RowBytes, 0, 0))
                {
                    return false;
                }

                const int samplesX = 5;
                const int samplesY = 5;
                double sum = 0.0;
                int count = 0;

                for (int sy = 0; sy < samplesY; sy++)
                {
                    int yy = y0 + (int)Math.Round((double)sy * (h - 1) / (samplesY - 1));
                    for (int sx = 0; sx < samplesX; sx++)
                    {
                        int xx = x0 + (int)Math.Round((double)sx * (w - 1) / (samplesX - 1));
                        SKColor c = pixmap is not null
                            ? pixmap.GetPixelColor(xx, yy)
                            : fallbackBitmap!.GetPixel(xx, yy);
                        double r = c.Red / 255.0;
                        double g = c.Green / 255.0;
                        double b = c.Blue / 255.0;
                        sum += 0.2126 * r + 0.7152 * g + 0.0722 * b;
                        count++;
                    }
                }

                if (count == 0)
                    return false;

                luminance = sum / count;
                return true;
            }
            finally
            {
                snapshot.ReleaseLease();
            }
        }

        private bool TryGetBoundsInTopLevelPixels(TopLevel topLevel, out PixelRect pixelRect)
        {
            pixelRect = default;

            Matrix? transform = this.TransformToVisual(topLevel);
            if (transform is null)
                return false;

            Rect bounds = new(0, 0, Bounds.Width, Bounds.Height);
            Point p0 = bounds.TopLeft.Transform(transform.Value);
            Point p1 = bounds.TopRight.Transform(transform.Value);
            Point p2 = bounds.BottomRight.Transform(transform.Value);
            Point p3 = bounds.BottomLeft.Transform(transform.Value);

            double left = Math.Min(Math.Min(p0.X, p1.X), Math.Min(p2.X, p3.X));
            double right = Math.Max(Math.Max(p0.X, p1.X), Math.Max(p2.X, p3.X));
            double top = Math.Min(Math.Min(p0.Y, p1.Y), Math.Min(p2.Y, p3.Y));
            double bottom = Math.Max(Math.Max(p0.Y, p1.Y), Math.Max(p2.Y, p3.Y));

            Rect aabb = new(left, top, Math.Max(0.0, right - left), Math.Max(0.0, bottom - top));
            if (aabb.Width <= 0 || aabb.Height <= 0)
                return false;

            double scaling = topLevel.RenderScaling;
            int pxLeft = (int)Math.Floor(aabb.X * scaling);
            int pxTop = (int)Math.Floor(aabb.Y * scaling);
            int pxRight = (int)Math.Ceiling(aabb.Right * scaling);
            int pxBottom = (int)Math.Ceiling(aabb.Bottom * scaling);

            if (pxRight <= pxLeft || pxBottom <= pxTop)
                return false;

            pixelRect = new PixelRect(new PixelPoint(pxLeft, pxTop), new PixelPoint(pxRight, pxBottom));
            return true;
        }

        private static double Lerp(double a, double b, double t)
        {
            return a + (b - a) * Clamp(t, 0.0, 1.0);
        }

        private static double Clamp(double value, double min, double max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private static FuncControlTemplate CreateDefaultTemplate()
        {
            return new FuncControlTemplate<LiquidGlassSurface>((_, ns) =>
            {
                LiquidGlassInteractiveOverlay interactiveOverlay = new LiquidGlassInteractiveOverlay
                {
                    Name = "PART_InteractiveOverlay",
                    IsHitTestVisible = false
                }.RegisterInNameScope(ns);

                ContentPresenter presenter = new ContentPresenter
                {
                    Name = "PART_ContentPresenter",
                    [~ContentPresenter.ContentProperty] = new TemplateBinding(ContentProperty),
                    [~ContentPresenter.ContentTemplateProperty] = new TemplateBinding(ContentTemplateProperty),
                    [~ContentPresenter.VerticalContentAlignmentProperty] = new TemplateBinding(VerticalContentAlignmentProperty),
                    [~ContentPresenter.HorizontalContentAlignmentProperty] = new TemplateBinding(HorizontalContentAlignmentProperty)
                }.RegisterInNameScope(ns);

                LiquidGlassFrontOverlay frontOverlay = new LiquidGlassFrontOverlay
                {
                    Name = "PART_FrontOverlay",
                    IsHitTestVisible = false
                }.RegisterInNameScope(ns);

                Grid grid = new();
                grid.Children.Add(interactiveOverlay);
                grid.Children.Add(presenter);
                grid.Children.Add(frontOverlay);

                return new Border
                {
                    ClipToBounds = true,
                    [~Border.CornerRadiusProperty] = new TemplateBinding(CornerRadiusProperty),
                    Child = grid
                };
            });
        }
    }
}
```

### `MainSrc\Tools\LiquidGlassAvaloniaUI\LiquidGlassVisualRenderer.cs`

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace LiquidGlassAvaloniaUI
{
    internal static class LiquidGlassVisualRenderer
    {
        public static void Render(DrawingContext context, Visual visual, Rect clipRect, ISet<Visual>? excludedRoots)
        {
            if (clipRect.Width <= 0 || clipRect.Height <= 0)
                return;

            Rect targetClip = new(clipRect.Size);
            using (context.PushTransform(Matrix.CreateTranslation(-clipRect.Position.X, -clipRect.Position.Y)))
            using (context.PushClip(targetClip))
            {
                Render(context, visual, new Rect(visual.Bounds.Size), Matrix.Identity, clipRect, excludedRoots);
            }
        }

        private static void Render(
            DrawingContext context,
            Visual visual,
            Rect bounds,
            Matrix parentTransform,
            Rect clipRect,
            ISet<Visual>? excludedRoots)
        {
            if (excludedRoots is not null && excludedRoots.Contains(visual))
                return;

            if (LiquidGlassBackdrop.GetIsExcludedFromCapture(visual))
                return;

            if (!visual.IsVisible || visual.Opacity <= 0)
                return;

            Rect rect = new(bounds.Size);
            Matrix transform;

            if (visual.RenderTransform?.Value is { } rt)
            {
                Point origin = visual.RenderTransformOrigin.ToPixels(visual.Bounds.Size);
                Matrix offset = Matrix.CreateTranslation(origin);
                transform = -offset * rt * offset * Matrix.CreateTranslation(bounds.Position);
            }
            else
            {
                transform = Matrix.CreateTranslation(bounds.Position);
            }

            using (context.PushTransform(transform))
            using (visual.HasMirrorTransform
                ? context.PushTransform(new Matrix(-1.0, 0.0, 0.0, 1.0, visual.Bounds.Width, 0))
                : default(DrawingContext.PushedState?))
            using (context.PushOpacity(visual.Opacity))
            using (PushClipToBounds(context, visual, rect))
            using (visual.Clip is { } clip ? context.PushGeometryClip(clip) : default(DrawingContext.PushedState?))
            using (visual.OpacityMask is { } opacityMask ? context.PushOpacityMask(opacityMask, rect) : default(DrawingContext.PushedState?))
            {
                Matrix totalTransform = transform * parentTransform;
                Rect visualBounds = rect.TransformToAABB(totalTransform);

                if (visualBounds.Intersects(clipRect))
                    visual.Render(context);

                IReadOnlyList<Visual> children = GetOrderedChildren(visual);

                if (visual.ClipToBounds)
                {
                    totalTransform = Matrix.Identity;
                    clipRect = rect;
                }

                foreach (Visual? child in children)
                {
                    Render(context, child, child.Bounds, totalTransform, clipRect, excludedRoots);
                }
            }
        }

        private static DrawingContext.PushedState? PushClipToBounds(DrawingContext context, Visual visual, Rect rect)
        {
            if (!visual.ClipToBounds)
                return default;

            if (TryGetClipToBoundsRadius(visual, out CornerRadius radius))
                return context.PushClip(new RoundedRect(rect, radius));

            return context.PushClip(rect);
        }

        private static bool TryGetClipToBoundsRadius(Visual visual, out CornerRadius radius)
        {
            radius = default;

            Type type = visual.GetType();
            PropertyInfo? prop = type.GetProperty("ClipToBoundsRadius", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);

            if (prop is null)
            {
                prop = type
                    .GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                    .FirstOrDefault(p => p.Name.EndsWith(".ClipToBoundsRadius", StringComparison.Ordinal));
            }

            if (prop?.PropertyType != typeof(CornerRadius))
                return false;

            if (prop.GetValue(visual) is not CornerRadius value)
                return false;

            if (value == default)
                return false;

            radius = value;
            return true;
        }

        private static IReadOnlyList<Visual> GetOrderedChildren(Visual visual)
        {
            IEnumerable<Visual> children = visual.GetVisualChildren();

            List<Visual>? list = null;
            int? firstZIndex = null;
            bool hasNonUniformZIndex = false;

            foreach (Visual? child in children)
            {
                list ??= new List<Visual>();
                list.Add(child);

                if (firstZIndex is null)
                    firstZIndex = child.ZIndex;
                else if (child.ZIndex != firstZIndex.Value)
                    hasNonUniformZIndex = true;
            }

            if (list is null || list.Count == 0)
                return Array.Empty<Visual>();

            if (!hasNonUniformZIndex)
                return list;

            return list.OrderBy(x => x.ZIndex).ToArray();
        }
    }
}
```

### `MainSrc\Tools\LiquidGlassAvaloniaUI\Assets\Shaders\LiquidGlassBackdropTransform.sksl`

```glsl
// Backdrop transform stage (zoom + offset)
// Used for magnifier-style sampling.

uniform shader content;

uniform float2 size;
uniform float zoom;      // 0+ (1 = identity)
uniform float2 offset;   // DIPs, applied in the same coordinate space as `coord`

half4 main(float2 coord) {
    float z = max(zoom, 1e-3);
    float2 center = size * 0.5;

    // Inverse transform: sample from a smaller region to magnify (z > 1),
    // and subtract offset so positive offset moves the drawn backdrop right/down.
    float2 sampleCoord = center + (coord - center - offset) / z;
    return content.eval(sampleCoord);
}
```

### `MainSrc\Tools\LiquidGlassAvaloniaUI\Assets\Shaders\LiquidGlassGamma.sksl`

```glsl
// Liquid Glass Gamma Adjustment Shader
// Adapted for SKRuntimeEffect.

uniform shader content;
uniform float power;

half4 main(float2 coord) {
    half4 color = content.eval(coord);
    float p = max(power, 0.000001);
    color.r = pow(color.r, p);
    color.g = pow(color.g, p);
    color.b = pow(color.b, p);
    return color;
}
```

### `MainSrc\Tools\LiquidGlassAvaloniaUI\Assets\Shaders\LiquidGlassHighlight.sksl`

```glsl
// Liquid Glass Highlight Shader
// Adapted for SKRuntimeEffect.

uniform float2 size;
uniform float4 cornerRadii; // [topLeft, topRight, bottomRight, bottomLeft]
uniform float4 color;       // RGBA (0..1)
uniform float angle;        // radians
uniform float falloff;      // >= 0

float radiusAt(float2 centeredCoord, float4 radii) {
    if (centeredCoord.x >= 0.0) {
        if (centeredCoord.y < 0.0) return radii.y;
        else return radii.z;
    } else {
        if (centeredCoord.y < 0.0) return radii.x;
        else return radii.w;
    }
}

float sdRoundedRect(float2 coord, float2 halfSize, float radius) {
    float2 cornerCoord = abs(coord) - (halfSize - float2(radius));
    float outside = length(max(cornerCoord, 0.0)) - radius;
    float inside = min(max(cornerCoord.x, cornerCoord.y), 0.0);
    return outside + inside;
}

float2 gradSdRoundedRect(float2 coord, float2 halfSize, float radius) {
    float2 cornerCoord = abs(coord) - (halfSize - float2(radius));
    if (cornerCoord.x >= 0.0 || cornerCoord.y >= 0.0) {
        return sign(coord) * normalize(max(cornerCoord, 0.0));
    } else {
        float gradX = step(cornerCoord.y, cornerCoord.x);
        return sign(coord) * float2(gradX, 1.0 - gradX);
    }
}

half4 main(float2 coord) {
    float2 halfSize = size * 0.5;
    float2 centeredCoord = coord - halfSize;
    float radius = radiusAt(centeredCoord, cornerRadii);

    // Compute the "surface normal" using SDF gradient.
    float gradRadius = min(radius * 1.5, min(halfSize.x, halfSize.y));
    float2 grad = gradSdRoundedRect(centeredCoord, halfSize, gradRadius);
    float2 normal = float2(cos(angle), sin(angle));

    float d = dot(grad, normal);
    float intensity = pow(abs(d), max(falloff, 0.0));
    // Skia expects premultiplied output colors.
    half a = half(color.a * intensity);
    half3 rgb = half3(color.rgb) * a;
    return half4(rgb, a);
}
```

### `MainSrc\Tools\LiquidGlassAvaloniaUI\Assets\Shaders\LiquidGlassInteractiveHighlight.sksl`

```glsl
// Liquid Glass Interactive Highlight Shader
// Radial highlight used for interactive press feedback.

uniform float2 size;
uniform float4 color;   // RGBA (0..1)
uniform float radius;
uniform float2 position;

half4 main(float2 coord) {
    float dist = distance(coord, position);
    float intensity = smoothstep(radius, radius * 0.5, dist);
    // Skia expects premultiplied output colors.
    half a = half(color.a * intensity);
    half3 rgb = half3(color.rgb) * a;
    return half4(rgb, a);
}
```

### `MainSrc\Tools\LiquidGlassAvaloniaUI\Assets\Shaders\LiquidGlassProgressiveMask.sksl`

```glsl
// Progressive alpha-mask + tint stage
// Adapted for SKRuntimeEffect and premultiplied output.

uniform shader content;

uniform float2 size;
uniform float start;          // 0..1 (fraction of height where blur starts fading)
uniform float end;            // 0..1 (fraction of height where blur fades to transparent)
uniform float4 tint;          // RGBA (0..1, unpremultiplied)
uniform float tintIntensity;  // 0..1

float smoothstepSafe(float edge0, float edge1, float x) {
    float denom = edge1 - edge0;
    if (abs(denom) < 1e-6) {
        return step(edge0, x);
    }
    float t = clamp((x - edge0) / denom, 0.0, 1.0);
    return t * t * (3.0 - 2.0 * t);
}

half4 main(float2 coord) {
    float edge0 = size.y * end;
    float edge1 = size.y * start;

    // Keep the top region fully visible and fade towards the bottom.
    float alpha = smoothstepSafe(edge0, edge1, coord.y);

    half4 base = content.eval(coord) * half(alpha);

    half tintA = half(tint.a) * half(alpha);
    half3 tintRgb = half3(tint.rgb) * tintA;
    half4 tintPremul = half4(tintRgb, tintA);

    half t = half(clamp(tintIntensity, 0.0, 1.0));
    return base * (half(1.0) - t) + tintPremul * t;
}
```

### `MainSrc\Tools\LiquidGlassAvaloniaUI\Assets\Shaders\LiquidGlassShader.sksl`

```glsl
// Liquid Glass SKSL Shader (Lens refraction + dispersion)
// Adapted to Avalonia + SkiaSharp (SKRuntimeEffect).

uniform shader content;

uniform float2 size;                 // Control size (DIP in Avalonia coordinates)
uniform float4 cornerRadii;          // [topLeft, topRight, bottomRight, bottomLeft]
uniform float refractionHeight;      // Edge band thickness (DIP)
uniform float refractionAmount;      // Refraction strength (DIP)
uniform float depthEffect;           // 0..1
uniform float chromaticAberration;   // 0..1

float radiusAt(float2 centeredCoord, float4 radii) {
    if (centeredCoord.x >= 0.0) {
        if (centeredCoord.y < 0.0) return radii.y;
        else return radii.z;
    } else {
        if (centeredCoord.y < 0.0) return radii.x;
        else return radii.w;
    }
}

float sdRoundedRect(float2 coord, float2 halfSize, float radius) {
    float2 cornerCoord = abs(coord) - (halfSize - float2(radius));
    float outside = length(max(cornerCoord, 0.0)) - radius;
    float inside = min(max(cornerCoord.x, cornerCoord.y), 0.0);
    return outside + inside;
}

float2 gradSdRoundedRect(float2 coord, float2 halfSize, float radius) {
    float2 cornerCoord = abs(coord) - (halfSize - float2(radius));
    if (cornerCoord.x >= 0.0 || cornerCoord.y >= 0.0) {
        return sign(coord) * normalize(max(cornerCoord, 0.0));
    } else {
        float gradX = step(cornerCoord.y, cornerCoord.x);
        return sign(coord) * float2(gradX, 1.0 - gradX);
    }
}

float circleMap(float x) {
    x = clamp(x, 0.0, 1.0);
    return 1.0 - sqrt(1.0 - x * x);
}

half4 sampleDispersed(float2 refractedCoord, float2 dispersedCoord) {
    half4 color = half4(0.0);

    half4 red = content.eval(refractedCoord + dispersedCoord);
    color.r += red.r / 3.5;
    color.a += red.a / 7.0;

    half4 orange = content.eval(refractedCoord + dispersedCoord * (2.0 / 3.0));
    color.r += orange.r / 3.5;
    color.g += orange.g / 7.0;
    color.a += orange.a / 7.0;

    half4 yellow = content.eval(refractedCoord + dispersedCoord * (1.0 / 3.0));
    color.r += yellow.r / 3.5;
    color.g += yellow.g / 3.5;
    color.a += yellow.a / 7.0;

    half4 green = content.eval(refractedCoord);
    color.g += green.g / 3.5;
    color.a += green.a / 7.0;

    half4 cyan = content.eval(refractedCoord - dispersedCoord * (1.0 / 3.0));
    color.g += cyan.g / 3.5;
    color.b += cyan.b / 3.0;
    color.a += cyan.a / 7.0;

    half4 blue = content.eval(refractedCoord - dispersedCoord * (2.0 / 3.0));
    color.b += blue.b / 3.0;
    color.a += blue.a / 7.0;

    half4 purple = content.eval(refractedCoord - dispersedCoord);
    color.r += purple.r / 7.0;
    color.b += purple.b / 3.0;
    color.a += purple.a / 7.0;

    return color;
}

half4 main(float2 coord) {
    float2 halfSize = size * 0.5;
    float2 centeredCoord = coord - halfSize;
    float radius = radiusAt(centeredCoord, cornerRadii);

    float sd = sdRoundedRect(centeredCoord, halfSize, radius);
    float h = max(refractionHeight, 0.001);

    // Flat center region: no refraction.
    if (-sd >= h) {
        return content.eval(coord);
    }

    // Only care about inside. Outside will be clipped in C#.
    sd = min(sd, 0.0);

    float d = circleMap(1.0 - (-sd / h)) * refractionAmount;
    float gradRadius = min(radius * 1.5, min(halfSize.x, halfSize.y));
    float2 grad = gradSdRoundedRect(centeredCoord, halfSize, gradRadius);
    grad = normalize(grad + depthEffect * normalize(centeredCoord));

    float2 refractedCoord = coord + d * grad;

    half4 result;
    if (chromaticAberration <= 0.001) {
        result = content.eval(refractedCoord);
    } else {
        float denom = max(halfSize.x * halfSize.y, 0.001);
        float dispersionIntensity = chromaticAberration * ((centeredCoord.x * centeredCoord.y) / denom);
        float2 dispersedCoord = d * grad * dispersionIntensity;
        result = sampleDispersed(refractedCoord, dispersedCoord);
    }
    return result;
}
```


## 6.9 MainSrc\Codes

### `MainSrc\Codes\read.md`

```markdown
--By Erics  九条开源
内核级别 主程序代码

Kernel level：Main codes
```


## 6.10 二进制资源清单（不嵌入内容）

| 目录 | 文件数 | 体积 | 样本 |
|---|---|---|---|
| `MainSrc\Views\imgs\Main\images\Buttons` | 15 | 21.3 KB | black-.png、blackx.png、button-Start1.png、button-Start2.png、button-Start3.png、button-Start4.png |
| `MainSrc\Views\imgs\Main\images\Launcher` | 7 | 716.3 KB | a7d21-in3zc.png、cyan_glazed_terracotta.png、Erics.jpg、MainLogo.ico、show-64x.png、show.png |
| `MainSrc\Views\imgs\Main\images\Blocks` | 16 | 186.4 KB | Anvil.png、april_fools.png、chest.png、CobbleStone.png、command.png、CommandBlock.png |
| `MainSrc\Views\imgs\Main\images\Loaders` | 15 | 171.6 KB | 8mi-tech.png、chicken.png、cleanroom.png、Egg.png、Fabric.png、fabric2.png |
| `MainSrc\Views\imgs\Main\images\Support` | 7 | 107.2 KB | discord.png、github.png、ic_curseforge.png、ic_modrinth.png、ic_mouse_pointer.png、kookapp.png |
| `MainSrc\Views\imgs\Main\images\BackGrounds` | 3 | 925.5 KB | banb_img.png、bg.jpg、ic_setting_sign_in_background.webp |
| `MainSrc\Views\imgs\Main\images\svgs` | 15 | 242.9 KB | clucker.svg、creeper.svg、enderpearl.svg、flame.svg、ftb-glow.svg、ftb-logo.svg |
| `MainSrc\Views\imgs\Main\images\xmls` | 43 | 43.6 KB | background_card.xml、background_control_editor.xml、background_icon_edit.xml、background_line.xml、background_line_selected.xml、background_line_unselected.xml |
| `MainSrc\Views\imgs\skin` | 15 | 32.5 KB | Alex.png、Steve.png、_-酔った东京空_3.png、【搬运】◇苦力怕娘（作者：钻茜）.png、史蒂夫【卡兰兹大眼】.png、史蒂夫，但是byd脸.png |
| `MainSrc\Sound` | 6 | 1201.3 KB | BigError.wav、DownloadDone.wav、Errors.wav、Messages.wav、Messages2.wav、Open.wav |

> 全部通过 `<AvaloniaResource Include="MainSrc\Views\imgs\**" />` / `MainSrc\Sound\**` 打包进程序集，
> 运行时以 `avares://Project.Launch/MainSrc/...` 读取。


# 第七部分：协作约定

## 7.1 分工模式

- 用户是产品与需求的唯一来源；AI 负责实现、取证与文档。
- **大规模排查优先并行分工**：本次"下载掉速"就是按 4 个方向（代码审计 / 网络实测 / PCL 机制 / 302 机制验证）并行取证，
  最后由 Lead 交叉验证收敛。每个方向都要求给出**可复现的数字**，不接受"可能是…"。
- 改代码前先确认改哪个文件、影响哪些调用点；跨文件改动一次性做完再编译。

## 7.2 反直觉教训（本次新增）

1. **"网络慢"和"代码路径选错"必须先用数据分开。**
   本次真相是：不是带宽问题，是**每个文件都先去撞一个坏镜像**。
   方法：同一批文件 A/B 双源实测 + 真实下载目录对账。
2. **`curl` 通 ≠ 启动器通。**
   `curl` 不读 WinINET 系统代理，.NET 读。排查"启动器网络全挂"时两者表现会完全相反。
3. **`async void` 是崩溃放大器。**
   Avalonia 事件处理器里任何未捕获异常都会直接终止进程。必须加全局 `Dispatcher.UIThread.UnhandledException` 守卫。
4. **"慢但返回 200"是最阴的失败。**
   只按 HTTP 错误码判断成功/失败，会让坏源永远"看起来正常"。
   必须有**速率判据**（失速）而不只是**状态码判据**。
5. **代理端口"能建 TCP 但不转发"** 也要能降级，不能只判"拒绝连接"。
6. **测速数字不能直接信。** 失败重试的字节会被重复计入，导致"速度虚高、进度不动"。
   本次已改为失败回滚。

## 7.3 排查脚本的存放约定

- 临时脚本/产物一律写 `%TEMP%\mcdiag\<方向>\`，**不要污染仓库**。
- 排查脚本要带注释说明：目的、判据、如何复现。
- 需要长期保留的结论写进本文档（第三部分），脚本可丢弃。

## 7.4 交付前检查清单

- [ ] `dotnet build Project.Launch.csproj -c Debug` **0 错误**
- [ ] 新增/修改的 `avares://` 路径在磁盘上真实存在
- [ ] 下载相关改动：至少跑一次真实版本安装，确认 `app.log` 里 `下载结束：…失败 0`
- [ ] 涉及 UI 事件：确认异常不会终止进程（`app.log` 查"UI 线程未处理异常"）
- [ ] 改了 `OutputPath` / 目录结构 → 同步检查 `.gitignore` 与 csproj 的 6 条排除
- [ ] 改完 `HANDOVER.md` 时，**若源码已变，第六部分必须重新生成**（见 7.5）

## 7.5 如何重新生成本文档第六部分

第六部分是**脚本生成**的，避免手工搬运出错。思路：

1. 盘点文件：`Get-ChildItem -Recurse -Include *.cs,*.axaml,*.csproj,*.sksl,*.manifest,*.md`
   排除 `obj\`、`bin\`、`BuiltDebugRun\`、`publish\`、`Design\`、`.git\`；
2. 按目录分组，为每个文件输出 `### `路径`` + 对应语言围栏的完整内容；
3. 语言映射：`.cs→csharp`、`.axaml/.csproj/.manifest→xml`、`.sksl→glsl`、`.md→markdown`、其他 `text`；
4. 若文件内容本身含三反引号，围栏升级为四反引号；
5. 拼接顺序：第零~五部分（人工叙述）→ 第六部分（生成）→ 第七部分（人工叙述）。

## 7.6 用户环境备忘

| 项 | 值 |
|---|---|
| 工作目录 | `C:\NoiRaceing\Project.Launch\Project.Launch` |
| 打包环境 | .NET SDK 10.0.401；`pwsh -Command` 实际是 PowerShell 5.1 |
| 代理 | Windows 系统代理指向 `127.0.0.1:7892`（客户端可能不常开）→ 已由 `ProxyEnvironment` 处理 |
| 测试下载目录 | `E:\MinecraftDownloadTest`（用户手工测试用） |
| 已知偏好 | 中文交流；喜欢"先说结论 + 给证据/数字"；重效率，能并行就并行 |

---

# 文档结束

> 若你读到这里，说明你已经掌握了本项目的全貌。
> 记住三件事：
> 1. **下载慢先查源，不要先调并发**；
> 2. **网络全挂先查系统代理**；
> 3. **UI 事件抛异常要先兜住**，否则整个程序一起陪葬。

