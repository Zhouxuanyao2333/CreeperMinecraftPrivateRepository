# Kilim Minecraft Launcher — 完整交接文档（自包含）

> **本文件是自包含的**：包含项目框架说明、用途定位、已知缺陷、下一步需求调研，以及**当前全部源码**。
> 接手方只需看这一个文件即可理解并继续开发。
>
> | 项 | 值 |
> |---|---|
> | 项目 | Kilim Minecraft Launcher（内部代号 `Project.Launch`） |
> | 仓库 | https://github.com/Zhouxuanyao2333/KilimMinecraftAllSource （**私有**） |
> | 分支 / HEAD | `master` / `2c01d3e` |
> | 文档生成 | 2026-10-16 |
> | 技术栈 | C# / .NET 10 + Avalonia 12.1.2 + SkiaSharp 3.119.4 + NAudio 2.2.1 |
> | 平台 | Windows x64 |
> | 文档规模 | 源码部分由脚本从仓库真实文件生成，**不存在手抄误差** |

---

# 目录

- [第零部分：这份文档怎么用（先读这个）](#第零部分这份文档怎么用先读这个)
- [第一部分：这是什么项目](#第一部分这是什么项目)
- [第二部分：环境、构建与运行](#第二部分环境构建与运行)
- [第三部分：已实现功能详解](#第三部分已实现功能详解)
- [第四部分：已知缺陷与待办](#第四部分已知缺陷与待办)
- [第五部分：下一步需求 —— 下载功能](#第五部分下一步需求--下载功能)
- [第六部分：完整源码](#第六部分完整源码)
- [第七部分：协作约定](#第七部分协作约定)

---

# 第零部分：这份文档怎么用（先读这个）

## 0.1 文档一定会过期 —— 这是设计前提

**本项目的功能仍在快速迭代，代码每天都在变。这份文档是一张"快照"，不是"事实来源"。**

因此请牢记下面这条规则，它能帮你省掉大量返工：

> ### ⚠️ 需要改 / 查任何具体实现时，先向用户索要该文件的最新版本
>
> - 本文档第六部分的源码对应 `commit 2c01d3e` 的工作区状态
> - 如果用户在此之后又改过代码，**第六部分对应的小节就已经失效**
> - **不要根据本文档里的代码去推断"当前实现是怎样的"**，也不要基于它做重构
> - 正确做法：告诉用户"我需要 `src/Views/MainWindow.cs` 的最新内容"，由用户提供

同理：

- 本文档的**框架说明、用途定位、架构分层、环境陷阱、协作约定**（第一、二、七部分）**相对稳定**，可以放心参考
- 本文档的**已知缺陷、待办清单**（第四部分）会随着修复而失效
- **下载功能调研**（第五部分）是产品层面的调研结论，有效期较长，但其中的待决策项需要用户拍板

## 0.2 源码内嵌策略（第六部分）

| 类别 | 是否嵌入全文 |
|---|---|
| 根目录：`Program.cs`、`App.axaml(.cs)`、`.csproj`、`app.manifest`、`.gitignore`、`README.md` | ✅ 全文 |
| `src\Views\`：四个窗口的 XAML 与代码 + 两套样式表 | ✅ 全文 |
| `src\Tools\`：全部 21 个内核文件 | ✅ 全文 |
| `src\Tools\Auth\`：微软登录 5 个文件 | ✅ 全文 |
| `src\ViewModel\` | ✅ 全文 |
| `src\LiquidGlassAvaloniaUI\`：玻璃库 14 个 .cs + 6 个 .sksl + csproj | ✅ 全文 |
| 图片 / 音效 / 图标（50 个，约 2 MB） | ❌ 仅列清单（6.7 节） |

## 0.3 推荐阅读顺序

**如果你是完全第一次接触这个项目：**

1. [1.1 一句话定位](#11-一句话定位) → [1.5 架构分层](#15-架构分层与调用关系) —— 先建立全局印象
2. [2.2 构建与运行](#22-构建与运行命令) + [2.3 环境陷阱](#23--环境陷阱务必先读) —— **动手前必读，能省大量时间**
3. [6.3 `GameLauncher.cs`](#6-3-srctools--内核工具层) —— 启动器的核心
4. [6.2 `MainWindow.axaml` + `.cs`](#6-2-srcviews--界面层) —— 界面结构与交互范式
5. [3.8 液态玻璃子系统](#38-液态玻璃子系统--含本次根因修复) —— **踩坑最多、最反直觉的一块**

**如果你要接着做某个功能：**

- 做下载功能 → 直接读 [第五部分](#第五部分下一步需求--下载功能)，那份调研已经很扎实
- 改 UI → 先读 [3.6 UI 交互](#36-ui-交互) 的"标题栏两态"与"滑块动画范式"，再读对应窗口源码
- 碰液态玻璃 → **务必先读 [3.8](#38-液态玻璃子系统--含本次根因修复)**，否则会重复走一遍我们走过的所有弯路

---

# 第一部分：这是什么项目

## 1.1 一句话定位

**Kilim Minecraft Launcher** 是一个用 **C# / .NET 10 + Avalonia 12 从零手写**的 Minecraft 启动器，主打两个卖点：

1. **不用 WebView**（原生渲染，无内嵌浏览器开销）
2. **自研液态玻璃 UI**（SKSL 运行时着色器做折射 / 色散 / 模糊）

## 1.2 用处与产品定位（重要，决定了"什么该做什么不该做"）

### 当前形态：**导入式启动器**

它**依赖用户机器上已经装好的 Minecraft**：扫描 `.minecraft` 目录 → 识别版本 → 启动。**不从网络下载任何游戏文件。**

### 目标形态：Java 版 + 基岩版双线

用户的产品规划是：

- **正版（Java）与基岩版两条线并行，共用一套下载引擎**
- 基岩版**为规避版权只支持"安装包下载"**（不重分发游戏本体），数据源走第三方站 MCAPPX
- 完整约束见 [第五部分 5.1](#51-用户的产品定位与硬约束重要)

### ⚠️ 最关键的现状判断

> **这个项目"外壳完成度约 90%，内核完成度约 40%"。**
>
> UI / 动画 / 液态玻璃 / 3D 皮肤这些"看得见的部分"打磨得很细（连 120% DPI 补偿、WCAG 亮度选色、梯形缩放脉冲都考虑到了），
> 但**功能内核缺了最关键的一块：下载与安装**——那是它从"Demo"变成"启动器"的最后一道门槛。
>
> 另外，从"下载"到"用正版身份启动"这条主干**两头都断着**：入口没有下载器，出口没有接通正版凭据
> （见 [4.1 P0 级缺陷](#41--p0-级功能失效缺陷)）。

## 1.3 技术栈

| 项 | 内容 |
|---|---|
| 框架 | .NET 10（`net10.0`），`win-x64`，`WinExe` |
| UI | Avalonia 12.1.2 + Fluent 主题 + Inter 字体 |
| 渲染 | SkiaSharp 3.119.4（3D 皮肤投影 + 液态玻璃着色器） |
| 音频 | NAudio 2.2.1 |
| 发布 | 单文件自包含（`PublishSingleFile` + `EnableCompressionInSingleFile`），**未裁剪** |
| 玻璃库 | `src/LiquidGlassAvaloniaUI`（**独立 csproj**，主项目排除其源码后以 `ProjectReference` 引用） |
| 测试 | **无测试工程**（全项目只有两个 csproj） |
| MVVM | **没有**（全部 `FindControl` + 命令式赋值，状态放在 `App` 的静态字段里） |

## 1.4 项目结构总览

```
Project.Launch\
├── Program.cs                    程序入口（含崩溃日志兜底）
├── App.axaml / App.axaml.cs      应用级资源、启动时恢复登录态
├── Project.Launch.csproj         主项目文件（资源注册规则见 2.3 陷阱）
├── app.manifest                  Windows 清单（透明窗口相关，勿删）
├── README.md                     一句话 + 署名
│
├── src\
│   ├── Codes\                    【空目录】原本规划为"内核级主程序代码"，目前只有 read.me
│   ├── Tools\                    ★ 内核工具层（启动链路 / 检测 / 持久化 / 3D / 音效）
│   │   ├── Auth\                 微软正版登录全链路（MSA→XBL→XSTS→MC）
│   │   ├── GameLauncher.cs       ★ 启动器的核心：拼命令行 + 起进程
│   │   ├── JavaDetector.cs       Java 检测与版本匹配
│   │   ├── MinecraftDirectoryAnalyzer.cs  目录布局识别（standard / isolated）
│   │   ├── InstanceService.cs / InstanceData.cs   实例持久化
│   │   ├── MinecraftVersionJson.cs  版本 JSON 数据模型
│   │   ├── AccountService.cs / AccountData.cs     账户持久化
│   │   ├── OfflineUuidGenerator.cs  离线 UUID（兼容 HMCL / Java）
│   │   ├── MinecraftSkinModel.cs / Pivot3DControl.cs / Pivot3DRenderer.cs  3D 皮肤投影
│   │   ├── DefaultSkinSelector.cs / SkinCropper.cs  默认皮肤判定 + 头像裁切
│   │   ├── LauncherPaths.cs      所有路径的唯一来源
│   │   ├── LogHelper.cs          双语日志
│   │   ├── SoundService.cs / SoundConfig.cs  音效
│   │   └── AnimationConfig.cs / InteractionConfig.cs  动画与交互参数（设置页预留出口）
│   │
│   ├── Views\                    ★ 界面层
│   │   ├── MainWindow.axaml/.cs      主窗（5 个 Tab + 自绘标题栏 + 底部导航滑块）
│   │   ├── LoginWindow.axaml/.cs     登录窗（离线 / 正版 / 第三方 三模式）
│   │   ├── AccountManagerWindow.axaml/.cs  账户管理窗
│   │   ├── Styles.axaml              主题色 + Miret 系列按钮样式
│   │   ├── Glass.axaml               玻璃参数表（**注意：当前实际未被引用**，见 3.8.5）
│   │   ├── imgs\                     界面图片 + 皮肤贴图
│   │   └── read.me
│   │
│   ├── ViewModel\                【预留】协调接口层，目前只有一个无人引用的演示文件
│   │
│   └── LiquidGlassAvaloniaUI\    ★ 液态玻璃库（vendored 第三方，MIT）
│       ├── LiquidGlassSurface.cs         公开控件（40 个 StyledProperty）
│       ├── LiquidGlassBackdropProvider.cs ★ 背景抓取引擎（**根因修复在此**）
│       ├── LiquidGlassDrawOperation.cs   着色器管线
│       ├── Assets\Shaders\*.sksl         6 个 SKSL 运行时着色器
│       └── ...
│
├── Sound\                        6 个音效 wav（在 src\Sound）
└── 【运行时生成】KilimLauncher\   数据目录，在 exe 同级（见 2.4）
```

## 1.5 架构分层与调用关系

```
                              Program.cs
                                  │  构建 Avalonia App，崩溃写 crash.log
                                  ▼
                            App.axaml.cs
                                  │  启动时读 accounts.json 恢复登录态
                                  │  跳登录 → MainWindow；否则 MainWindow + LoginWindow
                                  ▼
        ┌─────────────────── MainWindow（主窗） ───────────────────┐
        │  5 个 Tab：下载(0) / 启动(1) / 主页(2) / 设置(3) / 工具(4)  │
        │  默认停在 index=2（主页）                                 │
        └───────┬─────────────────────────────────┬───────────────┘
                │                                 │
      ┌─────────▼──────────┐          ┌───────────▼─────────────┐
      │  导入实例链路       │          │  启动游戏链路            │
      │  （首次点击"导入"） │          │  （已有实例时点击"启动"）│
      └─────────┬──────────┘          └───────────┬─────────────┘
                │                                 │
   MinecraftDirectoryAnalyzer.Analyze(path)        │
     · 形态 A standard：versions/ 在根下           │
     · 形态 B isolated：.minecraft/versions/ 下    │
                │                                 │
   InstanceService.SetSelectedInstance             │
     → KilimLauncher\instances.json                │
                │                                 │
                └────────────► MinecraftVersionJson.Load(json)
                                        │
                               JavaDetector.DetectAllAsync()
                               JavaDetector.Match(需 major 版本)
                                        │
                               OfflineUuidGenerator（离线 UUID）
                                        │
                               GameLauncher.LaunchAsync(...)  ★核心
                                 · 拼 classpath（client.jar + libraries）
                                 · 解压 natives
                                 · 替换占位符（auth_* / assets_* / ...）
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

**几个必须知道的架构事实：**

1. **数据目录不在 `%APPDATA%`**，而是在 **exe 同级的 `KilimLauncher\`**（见 [2.4](#24-运行时数据目录)）。这意味着**装到 `Program Files` 会因无写权限而静默失效**。
2. **没有 ViewModel、没有绑定**：所有 UI 状态由 `App` 的静态字段（`PlayerName` / `OfflineUuid` / `IsLoggedIn` / `LoginCheck`）+ 各窗口私有字段维护。
3. **玻璃库是 vendored 的**：它的源码在仓库里，但主项目**不编译它**（`<Compile Remove>`），而是引用它独立编译的 DLL。改它需要单独编译。
4. **`src\Codes\` 是空目录**：规划为"内核级主程序代码"，实际代码都在 `src\Tools\`。
5. **`Glass.axaml` 那套"玻璃参数表"当前是空转的**——真正的玻璃参数写在各窗口 XAML 里内联。详见 [3.8.5](#385-已知的空转与死代码)。

---

# 第二部分：环境、构建与运行

## 2.1 环境要求

| 项 | 要求 |
|---|---|
| OS | Windows 10/11 x64 |
| SDK | **.NET 10 SDK**（实测 `10.0.401`；机器上也装了 `9.0.317`，但主项目要 net10.0） |
| 显示缩放 | 实测开发机为 **125%**（`RenderScaling=1.25`）。代码里多处涉及 DPI，改动前请确认当前缩放 |
| GPU | 需要支持 Skia 的 GPU 后端；无 GPU 时玻璃会退化 |

## 2.2 构建与运行命令

```bash
# 构建（Debug）
dotnet build Project.Launch.csproj -c Debug

# 构建（强制完整重编，排查"改了没生效"时用这个）
dotnet build Project.Launch.csproj -c Debug --no-incremental

# 单独编译玻璃库（改过 LiquidGlassAvaloniaUI 之后）
dotnet build src\LiquidGlassAvaloniaUI\LiquidGlassAvaloniaUI.csproj -c Debug

# 运行
bin\Debug\net10.0\win-x64\Project.Launch.exe

# 发布单文件
dotnet publish Project.Launch.csproj -c Release
```

**可执行文件位置：** `bin\Debug\net10.0\win-x64\Project.Launch.exe`

## 2.3 ⚠️ 环境陷阱（务必先读）

> 以下第 1~4 条来自前人的交接记录，**本次开发全部复现验证过，依然成立**；第 5~7 条是本次新增。

1. **编译前必须关掉正在运行的启动器**
   否则 `dotnet build` 在最后复制 `Project.Launch.exe` 时报 `MSB3027 / MSB3021 文件被锁定`。
   **这不是代码错误，只是文件占用。**

2. **绝不要在仓库根目录创建临时 csproj**
   主项目使用默认 `**/*.cs` 通配。临时项目 `obj/` 下生成的 `AssemblyInfo.cs` 会被吸进主项目编译，
   导致 `CS0579 程序集特性重复`（8 个错误）。调试脚本请放在仓库外（本项目的排查脚本都放在 `%TEMP%`）。

3. **`Border` 是 `Decorator`，只能有一个 Child**
   多个子元素必须套一层 `Grid`，否则报
   `AVLN3000: Unable to find a setter that allows multiple assignments to the property Child`。

4. **新增窗口需要在 csproj 的 `<AvaloniaXaml Update="...">` 里登记**，否则不参与 XAML 编译。

5. **⚠️ 增量构建会静默"假装成功"（本次踩到的真实坑）**
   如果你只改了**玻璃库**的 `.cs`，然后直接 `dotnet build Project.Launch.csproj`，
   MSBuild 有时**不会重新编译玻璃库**，只复制旧 DLL —— 构建报告"成功"，但你的改动**根本没进产物**。
   **验证方法**（务必养成习惯）：
   ```powershell
   # 比较时间戳，或直接在 DLL 元数据里搜你的新方法名
   (Get-Item 'bin\Debug\net10.0\win-x64\LiquidGlassAvaloniaUI.dll').LastWriteTime
   $t = [System.Text.Encoding]::UTF8.GetString([IO.File]::ReadAllBytes((Resolve-Path 'bin\Debug\net10.0\win-x64\LiquidGlassAvaloniaUI.dll')))
   $t.Contains('你的新方法名')      # 注意：.NET 元数据字符串堆是 UTF-8，不是 UTF-16
   ```
   排查不确定时直接用 `--no-incremental`。

6. **PowerShell 脚本执行的坑（如果你也用脚本排查）**
   - 本机执行策略**禁止直接运行 `.ps1` 文件**，需要 `Invoke-Expression ([IO.File]::ReadAllText($p,[Text.Encoding]::UTF8))`
   - `Get-Content` 默认**不是 UTF-8**，读含中文的脚本会出现乱码 → 必须显式 `-Encoding UTF8`，或直接用 `[IO.File]::ReadAllText(..., UTF8)`
   - 中文引号 `"` `"` 出现在 PowerShell 双引号字符串里会导致**语法错误** → 用单引号字符串

7. **数据目录写在 exe 同级**（见 2.4）：`Program Files` 下会静默失效，所有保存操作都 `try/catch` 吞掉异常。

## 2.4 运行时数据目录

所有运行时数据在 **`<exe 所在目录>\KilimLauncher\`**：

| 文件 / 目录 | 内容 | 由谁写 |
|---|---|---|
| `accounts.json` | 账户字典 + `SelectedAccount` + `LoginCheck`（**token 明文存储**） | `AccountService` |
| `instances.json` | 已导入实例列表 + `SelectedInstanceId` | `InstanceService` |
| `app.log` | 追加式日志，**永不轮转** | `LogHelper` |
| `MainWindowLog.txt` | 每次启动主窗时截断重写 | `LogHelper.Reset` |
| `startup.log` | 程序入口日志（追加） | `Program.Main` |
| `crash.log` | 崩溃时的完整 `ex.ToString()` | `Program.Main` catch |
| `instances\<id>\natives\` | 解压出的 natives（**从不清理**） | `GameLauncher` |
| `instances\<id>\game.log` | 游戏 stdout+stderr（每次启动截断） | `GameLauncher` |
| `current_skin.png` | 当前皮肤缓存（历史遗留） | — |

**只读的、属于用户自己的：** `<RootPath>\versions\<id>\<id>.{json,jar}`、`<RootPath>\libraries\`、`<RootPath>\assets\`。


---

# 第三部分：已实现功能详解

## 3.1 账户系统 ✅ 可用

### 离线登录

`LoginWindow` → `OfflineUuidGenerator`：

- 输入用户名 → `MD5("OfflinePlayer:" + username)`
- 设 `hash[6] = (hash[6] & 0x0F) | 0x30`（version 3）、`hash[8] = (hash[8] & 0x3F) | 0x80`（IETF variant）
- **手动按大端序拼 hex 字符串，不经过 .NET `Guid` 类**（走 `Guid` 会字节序错位，导致与 Java 服务器不兼容；`Guid Generate()` 已标 `[Obsolete]`）
- 账户 ID 格式 `offline_{username}`，`Uuid` 存**去掉横线**的 32 位 hex
- 唯一校验是 `IsNullOrWhiteSpace`（无长度 / 字符集 / 冲突校验）

### 微软正版登录（7 阶段全链路已实现，但**目前走不通**）

| 阶段 | 文件 | 端点 / 要点 |
|---|---|---|
| 1 申请设备代码 | `DeviceCodeAuthService` | `https://login.microsoftonline.com/consumers/oauth2/v2.0/devicecode`；scope `XboxLive.signin XboxLive.offline_access` |
| 3 轮询授权 | `DeviceCodeAuthService.PollForTokenAsync` | `.../token`，`grant_type=urn:ietf:params:oauth:grant-type:device_code`；sleep-then-poll（符合 RFC 8628）；处理 `authorization_pending` / `slow_down`(+5s) / `access_denied` / `expired_token` |
| 4 XBL | `XboxAuthService` | `https://user.auth.xboxlive.com/user/authenticate`；**`RpsTicket = "d=" + token`，前缀必须有** |
| 5 XSTS | `XboxAuthService` | `https://xsts.auth.xboxlive.com/xsts/authorize`；`SandboxId=RETAIL`，`RelyingParty=rp://api.minecraftservices.com/`；**含 XErr 错误码中文映射**（2148916233/235/236/237/238） |
| 6a MC 认证 | `MinecraftAuthService` | `https://api.minecraftservices.com/authentication/login_with_xbox`；`identityToken = "XBL3.0 x={uhs};{xsts}"` |
| 6b 拉 profile | `MinecraftAuthService` | `GET /minecraft/profile`；**404 → 抛"此微软账号没有购买 Minecraft"** |
| 7 装配账户 | `MicrosoftAuthFlow` | 统一编排，通过 `Progress` / `DeviceCodeReady` 事件回调 UI |

> ### ⚠️ 当前阻塞（两条）
>
> 1. **`DeviceCodeAuthService.ClientId = "7dddbcf7-5d7d-46cf-96fc-292d6f392cd0"` 是占位测试 ID，被 Mojang 拒绝。**
>    用户已申请自有 AppID，**等审批下来后替换该常量**即可打通。
> 2. **`MicrosoftRefreshToken` 存了但没有任何地方用它换新 token**（全仓无 `grant_type=refresh_token`），
>    `TokenExpireAt` 也从不读取 → **24 小时后必须重新走完整设备代码登录**。
>
> 另外：登录成功后拿到的 `MinecraftAccessToken` 等凭据，在启动游戏时**完全没被使用**（见 [4.1](#41--p0-级功能失效缺陷)）。

### 账户持久化（`AccountService` + `accounts.json`）

- **原子写**：写 `accounts.json.tmp` → `File.Move(overwrite: true)`
- 所有 read-modify-write 收口在 `ModifyAccounts` 的单个 `lock` 内
- `LoginCheck` 控制启动时是否跳过登录窗
- **token 明文存储**（无 DPAPI 加密）
- ⚠️ **`accounts.json` 损坏会被静默清空**：`ModifyAccounts` 捕获解析异常后写回一个空 root，**所有账户被抹掉**（只留一行日志）
- ⚠️ `AccountManagerWindow.OnSelectAccount` 用的是**未加锁的** `LoadAccounts(); ...; SaveAccounts()` 组合，与 `ModifyAccounts` 可能交错

### 账户管理窗（`AccountManagerWindow`）

- 列表按 `LastUsed` 倒序、点击切换、删除（带自绘确认弹窗）
- 列表项**纯代码构造 UI**（无 DataTemplate），含 40×40 头像 / 用户名 / 类型标签（正版绿·离线灰）/ UUID 前 8 位 / 删除按钮
- `ShouldOpenLogin` 属性通知主窗是否要拉起登录窗
- 删除当前选中账户会**登出**（清 `App.IsLoggedIn` / `PlayerName` / `OfflineUuid`）

## 3.2 游戏启动 ✅ 可用（导入式）

`GameLauncher.LaunchAsync` 是**真正能跑的启动器实现**：

- 兼容**新版** `arguments.jvm` / `arguments.game` 数组 **和** **旧版** `minecraftArguments` 字符串
- **全量占位符替换**：`${classpath}` `${auth_player_name}` `${auth_uuid}` `${assets_index_name}` `${game_directory}` `${natives_directory}` `${classpath_separator}` `${library_directory}` 等
- **rules 规则判定**：库与参数都做 OS 过滤（**仅判定 Windows**，`arch` / `features` 被忽略）
- **natives 自动解压**：跳过 `META-INF/`，处理 `${arch}` 的 64/32 位，**扁平化**到同一目录（子目录丢失、同名覆盖）
- 两种目录布局分流：`standard`（`.minecraft` 根）/ `isolated`（`.minecraft` 子目录）
- 进程输出重定向到 `instances/{id}/game.log`
- 用 `psi.ArgumentList` 传参（**引号处理正确**，含空格的路径没问题）

### ⚠️ 已知缺陷（正版相关 + 其他）

| 缺陷 | 位置 | 后果 |
|---|---|---|
| `${auth_access_token}` 硬编码 `"0"`、`${user_type}` 硬编码 `"legacy"`、`${auth_session}` 硬编码 `"0"` | `ReplacePlaceholders` | **正版账号实际以离线身份进服，进不了正版服务器** |
| 无 `-Xmx/-Xms` 内存设置 | 同上 | 只能用版本 JSON 自带的内存参数，**无每实例内存设置** |
| 分辨率写死 925×530 | 同上 | 无法自定义窗口 |
| 缺库**静默跳过**，无完整性校验 | classpath 拼接 | 缺文件不报错，启动失败原因不明 |
| 起进程后立刻 `return await Task.FromResult(true)` | 末尾 | **不保留 `Process` 句柄** → 监听不到游戏退出、无退出码、无崩溃检测、无停止按钮 |
| `${version_type}`、`${quickPlay*}` **未在替换表内** | 同上 | 这些占位符会**字面量进入命令行**（1.20+ 的 quick-play 参数） |
| `MainClass` 无空值校验 | 同上 | JSON 异常时启动参数错误 |
| natives 目录从不清理 | — | 磁盘占用持续增长 |

## 3.3 Java 检测 ✅ 可用

`JavaDetector.DetectAllAsync` 从 **4 个来源**收集候选（去重靠 `HashSet<string>(OrdinalIgnoreCase)`，**未做路径规范化**）：

1. `JAVA_HOME\bin\java.exe`
2. `PATH` 逐段找 `java.exe`
3. 7 个常见 JDK 厂商目录（Java / Adoptium / Microsoft / Zulu / BellSoft / Corretto），各扫一层
4. `InstanceService.GetSelectedInstance()?.JavaPath`

执行 `java -version` 解析主版本（兼容 `1.8.0_x` → 8 与 `17.0.9` → 17 两种格式，stdout/stderr 都读）。

`Match()` 策略：**优先精确匹配**版本 JSON 的 `javaVersion.majorVersion`，否则取**最小且 > 要求**的。

> ⚠️ 缺陷：
> - `proc.WaitForExit(5000)` 是**死代码**（两个流此时已 EOF），`java.exe` 挂起会让检测**无限卡住**
> - `Match()` **无上界**：需要 Java 8 时若只有 Java 21，会选 Java 21
> - 顺序执行，每个候选都要起一次进程，慢

## 3.4 实例管理 ✅ 可用

`MinecraftDirectoryAnalyzer.Analyze(path)` 自动识别两种布局：

- **形态 A（standard）**：`<path>\versions` 存在 → 用户选的是 `.minecraft` 根目录
- **形态 B（isolated）**：`<path>\.minecraft\versions` 存在 → 隔离实例
- 两者都不像 → `Layout` 为空，`IsValid = false`

扫出所有"**目录名 == 同名 `.json`**"的版本（**不检查 jar 是否存在**），排序后持久化到 `instances.json`。

> ### 🔴 两个已确认的缺陷（见 [4.1](#41--p0-级功能失效缺陷)）
> 1. **`isolated` 布局的实例永远无法启动**（版本路径硬编码）
> 2. **版本排序用 `StringComparer.OrdinalIgnoreCase`**，与注释"新版本靠后"行为相反：
>    `"1.20.1"` 排在 `"1.9"` **前面**（因为 `'2' < '9'`），而 UI 取 `Versions[^1]` 作为默认版本
>    → **实测会挑中更旧的 `1.9`**

## 3.5 UI 交互 ✅ 完成度较高

### 主窗（`MainWindow`）

- 960×560，`WindowDecorations="None"`，`CanResize="False"`（**固定尺寸**）
- `TransparencyLevelHint="AcrylicBlur"` + `Background="Transparent"`，根 `Grid` 半透明灰 `#D9E0E0E0`
- Win11 圆角（`Win32Properties.WindowCornerPreference="Round"`）
- 自绘标题栏 35px，可拖动（XAML 绑 `TitleBar_PointerPressed` → `BeginMoveDrag`）

### 五个 Tab

| index | 底部按钮 | 内容面板 | 状态 |
|---|---|---|---|
| 0 | `DownloadTabButton` | `DownloadPanel` | **纯 UI 骨架，无下载逻辑** |
| 1 | `LaunchTabButton` | `LaunchPanel` | 3D 皮肤 + 启动按钮 |
| 2 | `HomeTabButton` | `HomePanel` | 登录态 + 头像 + 切换账户（**默认停在这里**） |
| 3 | `SettingsTabButton` | `SettingsPanel` | 纯占位"暂时无设置功能" |
| 4 | `ToolsTabButton` | `ToolsPanel` | 纯占位"暂时无工具功能" |

底栏 `BottomNavBar` 520×52，`Grid ColumnDefinitions="*,*,*,*,*"` 每列 104 DIP（= `AnimationConfig.TabStep`），玻璃滑块 `TabSlider` 96×44。

### 标题栏两态机制（改 UI 时必读）

- **态 A 渐变**：`UpdateTitleBarGradient(_defaultStartColor, _defaultEndColor)`
  用于：主窗启动未登录时、弹出模态窗（登录窗 / 账户管理窗）时
  默认色 `#CCFF6B9D` → `#CCB366FF`
- **态 B 透明**：`SetTransparentTitleBarAsync()`
  登录完成后恢复。实现是"图标淡出 → 200ms → 换黑图 → 淡入"，背景/字色由 XAML 的 `BrushTransition` 0.4s 补间
  ⚠️ 两个时长不一致（0.2 vs 0.4），图标与背景并非同步收尾
- 亮度判定 `GetLuminance` 是标准 WCAG 相对亮度，`useDark = avgLuminance > 0.5` 决定黑/白文字与图标

### 滑块动画范式（可复用到其他选择器）

`LoginWindow.AnimateSliderTo` / `MainWindow.AnimateTabSliderTo` / `AnimateEditionSliderTo`
三者用**同一套手写 16ms `DispatcherTimer` 缓动**：

```
位移：cubic ease-in-out
缩放：梯形曲线 —— 0 → riseEnd(0.25) smoothstep 升到峰值 → 平台 → fallStart(0.75) → 1 smoothstep 降回 1.0
     并做缩放中心补偿 xComp = -(scale-1)*halfW
模糊：三角曲线 blurCurve = 1 - |2p - 1|，写入控件 BlurRadius
参数全部来自 AnimationConfig，改那里即可统一调整手感
```

### 其他 UI 事实

- **Toast**：右下角 `CancelToast`，`ShowToastAsync` 2.5s 自动隐藏，**无重入保护**（连续调用会互相打断）
- **音效**：`SoundService`（NAudio，后台线程播放）。全工程只有 **2 处** `Play` 调用（登录窗 `"Open"`、主窗 Toast `"Messages"`）；
  `Errors` / `BigError` / `Messages2` / **`DownloadDone`** 配置了却**从未被调用**（`DownloadDone` 显然是给下载功能预留的）
- **头像**：主页 `HomeFaceImage`/`HomeHatImage`，按 UUID 推算 Steve/Alex 后裁切脸+帽子两层。
  ⚠️ **正版账号也不拉取真实皮肤**（`AccountInfo.SkinUrl` 只写不读），始终显示内置 `Steve.png`/`Alex.png`
- ⚠️ **`SwitchPanelAsync` / `ShowToastAsync` 都无并发保护**，快速连点会竞态
- ⚠️ **登录窗与账户管理窗都设了 `WindowDecorations=None`，但只有主窗绑了拖动逻辑** → **这两个子窗口无法用鼠标拖动**
- ⚠️ 头像裁切逻辑在 `MainWindow` 与 `LoginWindow` 里**各写了一份**（`CropSkinAsync`），未复用 `SkinCropper`

## 3.6 3D 皮肤投影 ✅ 可用

- `MinecraftSkinModel`：手工定义头/身/手臂/腿 + 各 overlay 的顶点与 UV；支持 **Classic（4 宽臂）/ Slim（3 宽臂）**
  UV 表已核对官方 1.8+ 布局，**是正确的**（含 Slim 手臂背面偏移 51/43 这类刁钻处）
- `Pivot3DControl`：`ICustomDrawOperation` + SkiaSharp `DrawVertices`
  - 伪 3D 投影 + **画家算法排序**（无 z-buffer）+ 2D 投影背面剔除
  - 手写 yaw/pitch 旋转，`cameraDist=60`，`scale = viewH*0.78/32`
  - 光照：`0.6 + 0.4 * max(0, n·l)`，光源**跟随相机**（法线已修正为朝外，**不要把这里改回去**）
- **皮肤类型自动检测**：按 64 宽比例缩放后，看 `(54*ratio, 20*ratio)` 起 `12*ratio` 行像素是否全透明 → 判 Slim/Classic
- 自动旋转（33ms timer）+ 鼠标拖拽旋转（俯仰限 ±30°，灵敏度 `InteractionConfig.PivotSensitivity`）
- 默认皮肤判定 `DefaultSkinSelector`：按官方逻辑取 UUID 第 8/16/24/32 位 hex 异或判奇偶
- 头像裁切 `SkinCropper`：支持 64×32 / 64×64 / 128×128

> ⚠️ 已知缺陷：
> - **64×32 旧版皮肤 3D 渲染错误**（overlay 面采样越界），而 `src\Views\imgs\skin\` 里**自带 2 张 64×32 皮肤**
> - **256×256 不一致**：`SkinCropper` 拒绝（头像 null），`Pivot3DControl` 接受（`ratio=4`）；仓库里自带 1 张
> - `SKShader.CreateImage` **每面每帧都新建**（12 盒 × 6 面 × 30fps ≈ **2160 次/秒**），且 `Equals => false` 主动放弃渲染缓存
> - `BitmapToSkImage` 假定 `Bgra8888`/`Premul`，**未检查 `Bitmap.Format`**
> - `SkinCropper` 全部异常用裸 `catch` 吞掉，**无任何日志**

## 3.7 液态玻璃子系统 — 含本次根因修复

> **这一节是最重要的**。这个子系统曾经有一个极其反直觉、排查了很长时间的 bug，
> 在你动玻璃相关代码之前**务必读完**，否则会重复走一遍所有弯路。

### 3.7.1 它是什么

`src\LiquidGlassAvaloniaUI\` 是一个 **vendored 的第三方库**（上游 MIT 项目 `KaranocaVe/LiquidGlassAvaloniaUI`）。

它**不是**普通的 Avalonia 效果库，而是一个**手写的背景采样渲染器**：

```
抓取窗口背景 → 存成 SKImage 快照 → 每个 LiquidGlassSurface 用 SKSL 着色器采样它
```

公开 API：

| API | 说明 |
|---|---|
| `LiquidGlassSurface`（`: ContentControl`） | 40 个 StyledProperty 的玻璃控件 |
| `LiquidGlassInteractiveSurface` | 带按压缩放形变 + 径向高光 |
| `LiquidGlassBackdrop.IsExcludedFromCapture` | 附加属性：把某个 Visual 排除出快照 |
| `LiquidGlassDiagnostics.Snapshot` / `.Reset()` | **25 个运行时计数器**（详见 3.7.6） |
| `UseLiquidGlassPerformanceDefaults()` | AppBuilder 扩展：设 `UseRegionDirtyRectClipping` / `MaxDirtyRects` / Skia GPU 预算 |

宿主实际使用的参数（三个滑块几乎相同）：

| 参数 | `LoginModeSlider` | `TabSlider` | `EditionSlider` |
|---|---|---|---|
| 尺寸 / 圆角 | 114×36 / 6 | 96×44 / **22** | 114×36 / 6 |
| `RefractionHeight` | 4 | 4 | 4 |
| `RefractionAmount` | 40 | 40 | 40 |
| `BlurRadius`（静止） | 0 | 0 | 0 |
| `Vibrancy` | 0.86 | 0.86 | 0.86 |
| `ChromaticAberration` | **True** | True | True |
| `HighlightEnabled` | True | **False** | True |

> 库的默认值是 `RefractionHeight=12`、`BlurRadius=2.0`、`Vibrancy=1.5`、`ChromaticAberration=false`。
> **宿主把前三项都调弱了**（尤其 `Vibrancy` 从"加饱和"变成了"去饱和"）。

### 3.7.2 🎯 根因：背景快照是一张全透明的空图（已于 `2c01d3e` 修复）

#### 症状（用户原话）

1. **登录滑块**：只有最左边（离线登录）才有色散；动画时也只有它才有"毛玻璃层次感"；
   "像是把 LoginWindow 砍成四份，最左边的那四分之一才有动画"；
   "玻璃一半过了那个四分之一的线，左边还是模糊色散，**中间直接隔开**，右边就啥都没有了"
2. **底部导航滑块**：**只有"从任意位置移动到下载"才有动画**；启动→工具、工具→设置、下载→启动都没有
3. **下载页的基岩版/Java版选择器反而完全正常**
4. 所有涉及液态玻璃的地方都有问题；**怎么调参都没用**（这是 HANDOVER 上一版明确记录的）

#### 真因

库里的 `LiquidGlassVisualRenderer` 是一个**手写的视觉树遍历器**，负责把窗口背景画进 `RenderTargetBitmap`。
实测它**在真实窗口下一个像素都没画进去**：

```
快照位图 336×193 = 64,848 像素
alpha = 0 的像素：64,848          ← 全部
任一通道非零的像素：0
存出来的 PNG 只有 347 字节（纯空图）
```

**玻璃着色器采样的就是这张空图**，于是：采样透明 → 位移透明 → 输出透明 → **玻璃实际什么都没画**。

#### 一个机制解释全部症状

| 症状 | 机制 |
|---|---|
| 看不到折射/模糊，**却能"看到玻璃下的文字"** | 玻璃画的是全透明，你看到的是**下层真实 UI**，不是玻璃采样的结果 |
| **调参完全无效** | 参数作用在透明像素上，数学上恒为透明。这就是上一版文档记录"折射带 4→10 调参完全无效"的真正原因 |
| **"只有最靠左的位置有效果"** | 手写遍历的输出**整体偏移了 `+origin`**，只有落在位图内的那部分存活，存活比例 = `(W−origin.x)(H−origin.y)/(W·H)`。**控件越靠左，origin 越小，存活越多** |
| **"中间一条硬竖缝、右边什么都没有"** | **那条线就是存活区域的边界**——线左边还有内容可采样（有折射/色散），右边全是透明 |
| **"只有移动到下载才有动画"** | 下载是导航栏**最左**一格（index 0）→ origin 最小 → 存活最多 |
| **"基岩版/Java版选择器反而正常"** | 它在下载页**左上角**，位置最靠左 → 同样 origin 最小 |

> **另有旁证**：无头复刻实验测得同一机制的存活比例为 2.9%（那组几何的 origin 较小），
> 与公式 `(W−ox)(H−oy)/(W·H)` 精确吻合；本次真机样本 origin=(432,507)、位图 336×193，
> 公式给出**负数** → 存活 0% → 与实测 0.00% 完全一致。

#### 修复

**改动一处**：`LiquidGlassBackdropProvider.Capture` 中，弃用手写遍历，改用 **Avalonia 自带的 `RenderTargetBitmap.Render(Visual)`**：

```csharp
// 位图取【整窗客户区】尺寸，原点固定 (0,0)，因此不需要任何手写平移
PixelSize fullPx = new(
    Math.Max(1, (int)Math.Ceiling(topLevel.ClientSize.Width  * scaling)),
    Math.Max(1, (int)Math.Ceiling(topLevel.ClientSize.Height * scaling)));

// ...（尺寸变化时重建 ScratchBitmap）

state.ScratchBitmap.Render(topLevel);   // ← 用 Avalonia 真正的渲染器
pixelSize = fullPx;
PixelPoint originInPixels = new(0, 0);
```

**为什么不需要手动排除玻璃控件**：抓取期间 `s_captureDepth > 0` → `IsCapturing == true` →
`LiquidGlassSurface.Render` 与两个 overlay 都会**提前 return**，所以玻璃天然不出现在快照里，也不会递归。

**验证数据：**

| 指标 | 修复前 | 修复后 |
|---|---|---|
| 快照不透明占比 | **0.00%**（64848/64848 像素 alpha=0） | **100.00%** |
| 快照 PNG | 347 B（空图） | 24,550 B（完整画面） |
| CPU（15s 窗口） | 约 0.64 核 | **约 0.48 核**（更快，`Render()` 比手写遍历高效） |
| 工作集 | ~200 MB | ~209 MB |
| 崩溃 | — | 无 |

### 3.7.3 快照机制原理（修好之后仍然需要理解）

- **触发**：`EnsureSnapshot` 在每次 `Render` 时被调用；场景失效（`SceneInvalidated`）也会触发
- **节流**：抓取间隔下限 33ms（约 30fps）；`state.CaptureQueued` 做请求合并
- **裁剪区**：所有订阅者 `TransformedBounds` 的并集 ∩ 客户区，**按设备像素对齐**（`Floor`/`Ceiling`）
- **膨胀量** `GetBackdropInflate`：`max(32, |RefractionAmount|*(CA?2:1) + BlurRadius*3 + 6 + ...)` DIP
  → 本组合 = `max(32, 80+0+6)` = **86 DIP**
- **去重**：配置相同（缩放/尺寸/原点）且 **8×8 采样哈希**相同时跳过发布
- **滤镜缓存**：`FilteredKey` 含 crop 四元组，LRU 上限 8
- **发布顺序**：先 `Volatile.Write` 新快照，**再** `RequestDispose` 旧的（避免渲染线程看到已释放的快照而回退成白色填充）
- **订阅者失效**：发布后对所有订阅者 `InvalidateVisual()`

### 3.7.4 本次同时做的库健壮性补强

除根因修复外，另加了两处（**它们不是根因**，但补上了一个真实缺口）：

1. **`LiquidGlassSurface` 订阅渲染变换变化**
   `RenderTransform`（递归到 `TransformGroup` 的子变换）的 `Changed` 事件 → 通知 `EnsureSnapshot`。
   原因：`TranslateTransform.X` 这类改动由**合成器**处理，`Visual.RenderTransformChanged` 只更新合成视觉的变换、
   **不重新录制绘制树**，因此**不触发 `SceneInvalidated`** —— 玻璃库唯一的"按位置重抓"入口就此失效。
   （已用独立探针验证过：`TranslateTransform.Changed` / `ScaleTransform.Changed` 确实会触发，`TransformGroup` 会冒泡。）
2. **`EnsureSnapshot` 增加"控件跑出上次抓取覆盖区"的触发条件**
   原来这个检测（`needsClipGrowthCapture`）**只存在于 `SceneInvalidated` 处理器内部**，事件不来就是死代码。

### 3.7.5 已知的空转与死代码

> ⚠️ 改玻璃前请先确认这些，避免浪费时间：

| 项 | 状态 |
|---|---|
| **`src\Views\Glass.axaml`** 的全部资源 + `Button.Glass` 样式 | **零使用**。它是一张"玻璃参数表"，但真正的玻璃参数写在各窗口 XAML 里内联 |
| `Styles.axaml` 的 `Button.MiretAcrylic` / `Button.LarMiret` | **零使用**（只有 `Button.Miret` 在用） |
| **`LiquidGlassVisualRenderer.cs`（153 行）** | **根因修复后已成为死代码**（不再被调用），且它本身有缺陷（见下） |
| `RenderVisualWithClip` / `GetExcludedRoots` | 同上，已无调用者 |
| `UseLiquidGlassPerformanceDefaults()` | **宿主从未调用** —— 脏矩形裁剪与 Skia GPU 预算仍是 Avalonia 默认值 |
| `LiquidGlassDiagnostics` 的 25 个计数器 | **从未被任何 App 代码读取**（只有库内部在写）。排查时接上它极为有用，见 3.7.6 |
| `TintColor` / `SurfaceColor` | 在 `GammaPower == 1.0`（默认）时**被静默忽略**——`DrawSurfaceOverlay` 只在 gamma 分支内可达 |
| `DepthEffect` / `ProgressiveBlur*` / `AdaptiveLuminance*` / `InnerShadow*` / `BackdropZoom` / `BackdropOffset` | 全部是 `false` / 默认值，**当前均不参与渲染** |
| 哈希去重 | 只采样 **8×8 = 64 个像素**，细进度条 / 光标这类小范围变化会被误判为"没变" |

> 关于 `LiquidGlassVisualRenderer` 的缺陷（**即使它现在是死代码，也请知道这件事**）：
> 它的递归里，遇到 `ClipToBounds == true` 的视觉时会执行 `totalTransform = Matrix.Identity; clipRect = rect;`
> ——**就地改写剔除基准**，会让同一父级中排在它之后的兄弟子树用错误基准做 `Intersects` 剔除。
> 官方 `ImmediateRenderer` 只重设 clip、**不重设 transform**。这正是上面那个"内容整体偏移 `+origin`"的来源。

### 3.7.6 出问题时怎么排查（强烈建议）

这次能挖出根因，靠的是**把库自己抓到的快照落盘 + 读它的计数器**。以后再遇到玻璃问题，
**先做这两件事，不要先调参**：

**① 接上计数器**（`LiquidGlassDiagnostics.Snapshot` 是公开 API）：

```
发布 CapturesPublished / 请求 CaptureQueueRequests / 节流 SkippedByCadence
脏矩形抑制 SkippedByDirtyRect / 哈希跳过 SkippedByHash / 空裁剪 SkippedEmptyClip
重入 SkippedReentrant / 无订阅者 SkippedNoSubscribers
场景失效 RendererInvalidations / 脏矩形可用性 DirtyRectAvailable/Unavailable
订阅者失效 SubscriberInvalidations / 滤镜命中 FilterCacheHits/Misses
```

判读要点：

- `CapturesPublished` 持续增长 = 抓取健康
- **`脏矩形抑制` 与 `哈希跳过` 长期为 0** = 那两条"优化"从未生效（本次实测如此）
- `空裁剪 SkippedEmptyClip` 不为 0 = 有帧没抓成，快照会停在旧位置

**② 把快照落盘**（本次用的方法，几行代码即可）：

```csharp
// 在 LiquidGlassBackdropProvider.Capture 里 Render 之后
state.ScratchBitmap.Save(某个路径 + ".png");
// 然后统计：不透明像素占比、内容包围盒
```

- **不透明占比 ≈ 0%** → 快照是空图（本次的根因）
- 占比正常但**内容整体偏移** → 遍历器的变换问题
- 占比与内容都正常 → 问题在采样矩阵 / 着色器 / 滤镜裁剪

**③ 参数解释不了的事，别用参数去解释**

`RefractionHeight=4` 意味着折射**只发生在离边缘 4 DIP 的窄带内**，滑块内部 100% 原样透传；
而 `BlurRadius=0` 表示静止态**没有模糊**。所以在平坦的 `#D8D8D8` 轨道上，
**即使一切正常，静止态也几乎看不见玻璃效果**——这是参数决定的，不是 bug。
（想要更明显的观感，把 `RefractionHeight` 调到 `10~12`、`BlurRadius` 静止值调到 `1~2`。）

---

# 第四部分：已知缺陷与待办

## 4.1 🔴 P0 级（功能失效）

| # | 问题 | 位置 | 说明 |
|---|---|---|---|
| 1 | **`isolated` 布局的实例永远无法启动** | `MinecraftDirectoryAnalyzer` vs `MainWindow.StartGameAsync` / `GameLauncher` | 分析器把形态 B 的版本目录算成 `<path>\.minecraft\versions`，但**消费方全部硬编码** `Path.Combine(RootPath, "versions")` —— 而这个路径**不存在正是分析器判定形态 B 的原因**。结果：弹"版本文件缺失"，**`GameLauncher` 根本不会被调用**。<br>**铁证**：`AnalysisResult` 算出的 `VersionsDir` / `LibrariesDir` / `AssetsDir` / `GameDirectory` **四个字段全项目零消费**。<br>讽刺的是 `GameLauncher` 专门为 isolated 处理了 `gameDirectory`，说明作者想到了 isolated，但**版本路径这条线没接上** |
| 2 | **正版账号以离线身份启动** | `GameLauncher.ReplacePlaceholders` | `${auth_access_token}`→`"0"`、`${user_type}`→`"legacy"`、`${auth_session}`→`"0"` 硬编码；`MainWindow` 只传 `playerName`/`playerUuid`。<br>`MicrosoftAuthFlow` 认真写进 `accounts.json` 的 `MicrosoftRefreshToken` / `MinecraftAccessToken` / `TokenExpireAt` / `SkinUrl` **四个字段无人读取** → **登录成功只等于拿到一个用户名 + UUID** |
| 3 | **正版登录走不通** | `DeviceCodeAuthService.ClientId` | 占位测试 ID `7dddbcf7-…` 被 Mojang 拒绝。等用户自有 AppID 审批下来后替换即可 |

## 4.2 🟠 P1 级（明显错误）

| # | 问题 | 位置 |
|---|---|---|
| 4 | **版本排序 Ordinal → 默认选中旧版本**（实测 `1.9` 胜过 `1.20.1`） | `MinecraftDirectoryAnalyzer` 排序 + `MainWindow` 取 `Versions[^1]` |
| 5 | **无 `inheritsFrom` 建模** → Forge / NeoForge / Fabric / OptiFine **全部不支持** | `MinecraftVersionJson.cs` |
| 6 | **无刷新令牌流程** → 24 小时后必须重新设备码登录 | `src\Tools\Auth\` |
| 7 | **登录窗 / 账户管理窗无法用鼠标拖动**（`WindowDecorations=None` 但只有主窗绑了 `BeginMoveDrag`） | 两个子窗口的 axaml |
| 8 | **64×32 旧版皮肤 3D 渲染错误**，而仓库自带 2 张 | `Pivot3DControl.DetectSkinType` |
| 9 | **`accounts.json` 损坏会被静默清空**（解析失败后写回空 root → 所有账户被抹掉） | `AccountService.ModifyAccounts` |
| 10 | **无 `-Xmx/-Xms` 内存设置**，无每实例 JVM 参数 | `GameLauncher` / `InstanceData` |
| 11 | **起进程后立刻返回，不保留句柄** → 监听不到游戏退出、无退出码 | `GameLauncher` 末尾 |

## 4.3 🟡 P2 级（健壮性 / 安全）

| # | 问题 |
|---|---|
| 12 | **token 明文存储**（`accounts.json`，无 DPAPI） |
| 13 | **数据写在 exe 同级** → 装到 `Program Files` 后所有保存**静默失效** |
| 14 | **`app.log` 无轮转**，永久增长；natives 目录从不清理 |
| 15 | **`SwitchPanelAsync` / `ShowToastAsync` 无并发保护**，连点会竞态 |
| 16 | **`SkinCropper` 裸 `catch` 无日志**，且 `RenderTargetBitmap` 在 `Task.Run` 线程池线程上创建（非支持的 Avalonia 用法） |
| 17 | **`JavaDetector` 的 `WaitForExit(5000)` 是死代码**；`Match()` 无上界 |
| 18 | **`rules[].features` 与 `RuleOs.Arch/Version` 被忽略** → feature-gated 参数（quick-play）被无条件加入 |
| 19 | **`Pivot3DControl` 每面每帧新建 `SKShader`**（约 2160 次/秒）；`Equals => false` 放弃缓存 |
| 20 | **`BitmapToSkImage` 未检查 `Bitmap.Format`**，假定 `Bgra8888`/`Premul` |
| 21 | 手动遍历器（现为死代码）的 `ClipToBounds` 剔除缺陷（见 [3.7.5](#375-已知的空转与死代码)） |
| 22 | **无测试工程**；全仓**无 TODO / FIXME / NotImplementedException 标记** → 所有缺口都是"静默遗漏"，不是"标注的桩" |

## 4.4 ⚪ 死代码 / 遗留（清理项）

| 项 | 位置 |
|---|---|
| `LiquidGlassVisualRenderer.cs`（153 行）、`RenderVisualWithClip`、`GetExcludedRoots`、`IsExcludedFromCapture` | 根因修复后**已无调用者** |
| `Glass.axaml` 全部资源 + `Button.Glass`；`Styles.axaml` 的 `MiretAcrylic` / `LarMiret` | 零使用 |
| `MsProgressPanel` / `MsProgressText` | 声明并 `FindControl` 了，**从不设为可见**（进度写进 `MsStatus`） |
| `LoginWindow` 的 `OFFSET_OFFLINE` / `OFFSET_THIRD` 常量；`Close(bool isLogin)` 的参数 | 未使用 / 无效 |
| `MainWindow.SetTitleBarGradient`（public）；`src\ViewModel\TestWindowButton.cs` | 无调用者 |
| `MainWindow._httpClient`、`LoginWindow._httpClient` | 声明后从未使用 |
| `InteractionConfig` / `SoundConfig` / `AnimationConfig` | 可变静态字段，**无持久化、无 UI**（设置页是它们的预留出口） |
| `publish\` 目录下的旧日志 | 曾被误改，已还原 |
| `LoginWindow.axaml` 底部红字 **"请更换仓库不要露全部代码"** 与硬编码 `rc-5`；主窗标题硬编码 `KilimMinecraftLauncherDemo` | **交付前必须清掉的开发期痕迹** |
| `Pivot3DControl` 文件在 `src\Tools\`，**namespace 却是 `Project.Launch.Views`** | 命名不一致 |

## 4.5 已排除的排查方向（**勿重复踩坑**）

以下是本次排查中**用硬证据证伪**的假设。它们看起来都很有道理，但**都不是原因**：

| 假设 | 证伪方式 |
|---|---|
| 快照刷新时机 / 抓取没触发 | 真实 App 内计数器显示：**抓取持续发生**（发布 242→534，约 20~50 次/秒），`脏矩形抑制=0`、`哈希跳过=0`、`空裁剪=0` |
| 控件位置测量错误（`GetTransformedBounds` 不含 RenderTransform） | 诊断打印的全局 AABB 随动画**平滑正确变化**（如 TabSlider 432→312→224），`TransformToAABB` 正确 |
| 变换事件不被感知 | 独立探针证明 `TranslateTransform.Changed` / `ScaleTransform.Changed` 会触发，`TransformGroup` 会冒泡 |
| DPI 问题（120% / 125% 缩放） | 用户实测 100% / 150% 均无效；且映射公式中 `scaling` 在分子分母**完全对消**，三档缩放逐像素精确 |
| 滤镜结果图比所需采样范围小 → Clamp 硬缝 | 数值推导：余量 88 DIP ≥ 需求最坏 80 DIP；且 `crop` 恒等于整张快照 |
| 采样矩阵 / `WithPixelOrigin` 的 localMatrix 约定写错 | 用真实 SkiaSharp 双向反证：**现在的写法是对的**，改成"看起来对"的写法反而会坏 |
| 宿主玻璃参数配置导致 | `LoginModeSlider`（坏）与 `EditionSlider`（好）的**参数逐项完全相同** |
| 着色器本身能产生竖直分界 | 主透镜**左右严格对称**；唯一的不对称来自坐标域错误，表现是"错位重影"而非"平坦" |

> **教训**：上面每一条看起来都很有道理，我们花了大量时间在这几条上。
> 真正的突破点是**把快照本身落盘看了一眼**——发现它是全透明的。
> **下次遇到渲染问题，先拿到中间产物看一眼，再谈推理。**

---

# 第五部分：下一步需求 —— 下载功能

> 这一部分来自前人的产品调研，**结论仍然有效**。这是本项目从"Demo"走向"可用启动器"的关键一步。

## 5.1 用户的产品定位与硬约束（重要）

- 启动器要引入**基岩版版本管理**，但**为规避版权只支持"安装包下载"**（不重分发游戏本体）
- 正版（Java）与基岩版两条线并行，**共用一套下载引擎**
- 基岩版数据源走第三方站 **MCAPPX**，站长的硬性约束：
  1. **下载不走他们的服务器**，全部走微软官方 CDN
  2. **其下载服务一律禁止调用**（会触发机器人挑战返回 403）
     → 启动器**绝不能热链 mcappx 的任何下载链接**
  3. **唯一要求：公开项目必须标注来源 `mcappx.com`**（建议做进"关于"页 + 下载页脚注）
  4. 数据源对部分区域屏蔽，可向他们申请访问权限
     - ⚠️ **实测该数据接口在用户本机请求超时**（`https://data.mcappx.com/v2/bedrock.json` 30s 无响应，文档站可正常访问）—— **印证区域屏蔽**

## 5.2 Java 版下载源（已调研确认）

**官方**
- 清单：`https://piston-meta.mojang.com/mc/game/version_manifest_v2.json`
- 版本 JSON：`https://piston-meta.mojang.com/v1/packages/{hash}/{id}.json`
- client.jar：版本 JSON 内 `downloads.client.url`（`piston-data.mojang.com`）
- 库：`https://libraries.minecraft.net/{maven路径}`（group 的 `.` 换成 `/`）
- 资源：`https://resources.download.minecraft.net/{hash前2位}/{hash}`

**镜像**
- **BMCLAPI**（`bmclapi2.bangbang93.com`）：国内事实标准，覆盖全部端点，
  字符串替换域名即可，文档 `bmclapidoc.bangbang93.com`。**推荐作为默认源 + 官方回退**
- **MCBBS 镜像已废弃**（站点关闭），不要使用

**关键字段**：`downloads.client.url/sha1`、`assetIndex.url/sha1`、`libraries[]`（需按 OS/arch 过滤）

## 5.3 基岩版下载源（MCAPPX 数据源）

```
https://data.mcappx.com/v2/bedrock.json     （v1 已停止更新）
文档：https://www.mcappx.com/dev-docs/data-source/
```

**v2 数据结构**
```
From_mcappx.com
 └─ "1.21.100.24"                        ← 版本号
     ├─ Type        Release / Beta / Preview
     ├─ BuildType   UWP / GDK            ← ★ 决定走哪条下载链
     ├─ ID          安装包ID
     ├─ Date        发布时间
     └─ Variations[]
         ├─ Arch          x64 / x86 / arm
         ├─ ArchivalStatus 3=有官方记录 2=无记录 1=未发布 0=已丢失
         ├─ OSbuild       最低 Windows 构建号（如 19041）
         ├─ MetaData[]    ★ UWP 存 UpdateID(GUID)，GDK 存 msixvc 直链
         └─ MD5           校验值（注意：只有 MD5，没有 SHA）
```

**路线 A：GDK（msixvc 直链，最省事，建议先行）**
- `MetaData` 直接就是完整直链，例如：
  `http://assets1.xboxlive.cn/.../Microsoft.MinecraftWindowsBeta_1.21.12021.0_x64__8wekyb3d8bbwe.msixvc`
- **可切换 CDN 域名提速**（文档给出的国内快域名）：
  `assets1.xboxlive.cn` / `d1.xboxlive.cn` / `assets2.xboxlive.cn` / `d2.xboxlive.cn`（**快**）
  `*.xboxlive.com`（中国大陆**慢**）
- ⚠️ **msixvc 是原始 Xbox 分发包，不是 zip，无法解压**，只能整体用 `Add-AppxPackage` 安装

**路线 B：UWP（UpdateID → FE3 解析，麻烦且成功率低）**
- 只有 UpdateID (GUID)，需调微软 SOAP API：
  `https://fe3cr.delivery.mp.microsoft.com/ClientWebService/client.asmx/secured`
  用 `GetExtendedUpdateInfo2`，请求 `FileUrl` + `FileDecryption` 两种 infoType
- ⚠️ **文档明确警告：大量 UpdateID 已失效**（所有 Beta、早期 Preview、少数正式版）
  → **必须有兜底**（退回打开 Microsoft Store 页面）

**安装注意事项**
- GDK 依赖 **Xbox Gaming Services** 服务在运行（缺失则装完也跑不起来）
- UWP 需先装"框架包"，否则 `Add-AppxPackage` 会失败
- 装旧版会被系统拒绝，需 `-ForceUpdateFromAnyVersion`（甚至先卸载）
- **多版本共存涉及改包身份 → 签名失效、需开发者模式**，MCAPPX 有专门的"GDK 功能修补应用"处理
  → **建议 v1 只做单版本模式**

## 5.4 建议架构（三段式，Java / 基岩共用）

```
① 数据层  拉清单 + 本地缓存（按 CreationTime 判断是否刷新）
          区域屏蔽/超时 → 用缓存 + 明确提示，不能崩
② 解析层  Java: 版本 JSON（含 inheritsFrom 继承链）
          基岩: GDK 直链 / UWP → FE3 SOAP（失败兜底 Store）
③ 下载层  DownloadEngine（★ 共用）
          HttpClient + 并发 + Range 断点续传 + 校验 + 进度/速度/取消/重试
④ 安装层  基岩: Add-AppxPackage（提权/依赖/错误码）
```

**下载引擎要点**
- `SocketsHttpHandler`：`MaxConnectionsPerServer` 拉高（资源小文件可 32–64 并发）、
  `AutomaticDecompression = GZip | Deflate`
- 断点续传：`.part` 临时文件 + HTTP `Range`；大文件可分段并行
- 校验：Java 用 **SHA1**，基岩用 **MD5**
- 多镜像回退：每资源配镜像列表（BMCLAPI → 官方，或反序），失败自动切换
- 进度：`CancellationTokenSource` + `IProgress<double>`，直接接现有 UI

### 🎯 与现有架构的衔接（好消息）

> 下载器把版本装进一个 **standard 布局目录**（`versions/` + `libraries/` + `assets/`）后，
> `MinecraftDirectoryAnalyzer.Analyze` **直接就能扫出来**，`GameLauncher` **完全不用改**。
>
> → **"下载安装新版本" = 新建目录 → 下载 → 调 `SetSelectedInstance` 注册**，
>   **与现有"导入"走同一条下游链路**。
>
> → 基岩版不能用 `InstanceInfo`（没有 `.minecraft/versions/json/Java`），需新建 `BedrockInstance` 实体，
>   目录建议在 `LauncherPaths` 下加 `bedrock/`。
>
> ⚠️ **但请注意**：如果先不修 [4.1 的 P0-1](#41--p0-级功能失效缺陷)（isolated 版本路径），
> 下载器装出来的 standard 布局目录是能用的（standard 那条路径是自洽的），所以**下载功能可以先做**。

## 5.5 待产品决策（用户尚未拍板）

1. **基岩版先做 GDK 还是 UWP？**（建议 GDK 先行，UWP 后置 + Store 兜底）
2. **只下载，还是下载+安装一条龙？**（后者要处理提权、依赖、错误码）
3. **单版本还是多版本共存？**（建议 v1 单版本）
4. **区域屏蔽只做缓存容错，还是去找站长申请访问权限？**
5. **镜像策略**：BMCLAPI 优先 + 官方回退，还是启动时测速自动选最快？
6. **Mod 下载**（Modrinth / CurseForge）是否进近期规划？

## 5.6 建议路线图

1. **P1 版本清单**：拉 manifest、解析、版本列表 UI（Release/Snapshot 分类 + 搜索）——零风险，先把下载 Tab 激活
2. **P2 原版安装**：版本 JSON（含 `inheritsFrom`）→ client.jar → libraries → assets，
   SHA1 校验 + 并发 + 断点续传 + BMCLAPI 优先
3. **P3 安装进度 UI**：速度 / 进度条 / 取消 / 重试 / 镜像设置
4. **P4 Java 运行时自动下载**（1.17+ 必备）
5. **P5 加载器**（Fabric / Forge / NeoForge）
6. **P6 Mod 下载**（Modrinth API v2 无需 key；CurseForge 需申请 key）

## 5.7 可参考的实现

- **MinecraftLaunch**（Blessed-Flight）：C# 启动器库，与本项目技术栈一致，**强烈建议翻源码参考**镜像处理与下载实现
- **CmlLib.Core**：老牌 C# 启动器库
- **HMCL**：镜像列表与测速切换的标杆
- `Downloader`（Bezzad）NuGet：现成分段下载组件

## 5.8 下载页 UI 骨架现状（已完成，逻辑未写）

`MainWindow.axaml` 的 `DownloadPanel` 已完成骨架：

- **左上角**：基岩版 / Java版 切换器（240×44 灰轨道 + `LiquidGlassSurface` 滑块 `EditionSlider`）
- **下方安全区**：白色圆角卡片（`CornerRadius=12` + `DropShadowEffect`），
  内含 `BedrockPanel` / `JavaPanel` 两个占位面板（目前只显示"版本列表（待接入）"）
- 切换逻辑在 `MainWindow.cs` 的 `InitializeEditionSelector` / `SelectEdition` / `AnimateEditionSliderTo`
  （复用 `AnimationConfig.Slider*` 参数，曲线与 LoginWindow 完全一致）

**页面里完全没有的**：版本列表控件（无 ListBox / ItemsControl / ScrollViewer）、版本数据源、
"下载/安装"按钮、进度条、任何网络请求（`MainWindow._httpClient` 声明后未使用）。


# 第六部分：完整源码

> **本部分由脚本从仓库真实文件直接生成**，内容与 `commit 2c01d3e` 的工作区逐字一致，不存在手抄误差。
>
> 若你发现某一节与仓库里的文件不一致，说明**文档已过期**——请直接向用户索要该文件的最新版本，
> 不要试图根据这里的代码去推断当前实现。详见 [第零部分 0.1](#01-文档一定会过期--这是设计前提)。
>
> 二进制资源（图片 / 音效 / 图标 / 着色器外的资源）**不嵌入**，清单见 6.7。

## 6.1 根目录

工程入口、应用级 XAML、项目文件与清单。

### `Program.cs`

<!-- 49 行 / 1.4 KB -->

```csharp
using System;
using System.IO;
using System.Threading;
using Avalonia;
using Project.Launch.Tools;

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

<!-- 13 行 / 0.6 KB -->

```xml
<Application xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="Project.Launch.App"
             RequestedThemeVariant="Default">

    <Application.Styles>
        <FluentTheme />
        <!-- 主题色 + Miret 样式 -->
        <StyleInclude Source="avares://Project.Launch/src/Views/Styles.axaml" />
        <!-- ★ 液态玻璃参数表 + Glass 按钮样式 -->
        <StyleInclude Source="avares://Project.Launch/src/Views/Glass.axaml" />
    </Application.Styles>
</Application>
```

### `App.axaml.cs`

<!-- 63 行 / 2.1 KB -->

```csharp
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Project.Launch.Views;
using System;
using Project.Launch.Tools;

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

<!-- 73 行 / 3.1 KB -->

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ApplicationManifest>app.manifest</ApplicationManifest>
    <ApplicationIcon>src\Views\imgs\MainLogo.ico</ApplicationIcon>

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

  <ItemGroup>
    <Compile Remove="src\LiquidGlassAvaloniaUI\**" />
    <AvaloniaXaml Remove="src\LiquidGlassAvaloniaUI\**" />
    <AvaloniaResource Remove="src\LiquidGlassAvaloniaUI\**" />
    <EmbeddedResource Remove="src\LiquidGlassAvaloniaUI\**" />
    <None Remove="src\LiquidGlassAvaloniaUI\**" />
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

    <ProjectReference Include="src\LiquidGlassAvaloniaUI\LiquidGlassAvaloniaUI.csproj" />
  </ItemGroup>

  <ItemGroup>
    <!-- ★ 用 Update（不是 Include），避免和默认 glob 重复 -->
    <AvaloniaXaml Update="App.axaml" />
    <AvaloniaXaml Update="src\Views\LoginWindow.axaml" />
    <AvaloniaXaml Update="src\Views\MainWindow.axaml" />
    <AvaloniaXaml Update="src\Views\AccountManagerWindow.axaml" />

    <!-- 样式：作为 AvaloniaResource 读，避免被当独立 XAML 编译 -->
    <AvaloniaResource Include="src\Views\Styles.axaml" />
    <AvaloniaResource Include="src\Views\Glass.axaml" />

    <!-- 图片和音效 -->
    <AvaloniaResource Include="src\Views\imgs\**" Exclude="src\Views\imgs\MainLogo.ico" />
    <AvaloniaResource Include="src\Sound\**" />

    <!-- 任务栏图标 -->
    <Content Include="src\Views\imgs\MainLogo.ico">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </Content>

    <Compile Update="src\Views\MainWindow.cs">
      <DependentUpon>MainWindow.axaml</DependentUpon>
    </Compile>
    <Compile Update="src\ViewModel\TestWindowButton.cs" />
    <Compile Update="App.axaml.cs" />
  </ItemGroup>
</Project>
```

### `app.manifest`

<!-- 19 行 / 0.9 KB -->

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

<!-- 4 行 / 0 KB -->

```text
bin/
obj/
.vscode/
```

### `README.md`

<!-- 57 行 / 4.2 KB -->

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

## 6.2 src\Views —— 界面层

四个窗口（主窗 / 登录 / 账户管理）+ 两套样式表。`MainWindow` 与 `LoginWindow` 是理解本项目 UI 的核心。

### `src\Views\MainWindow.axaml`

<!-- 751 行 / 38.8 KB -->

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:avalonia="clr-namespace:Avalonia.Controls;assembly=Avalonia.Controls"
        xmlns:lg="using:LiquidGlassAvaloniaUI"
        xmlns:views="clr-namespace:Project.Launch.Views"
        x:Class="Project.Launch.Views.MainWindow"
        Title="KilimMinecraftLauncherDemo"
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
                           Text="KilimMinecraftLauncherDemo"
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
                           Source="avares://Project.Launch/src/Views/imgs/white-.png"
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
                           Source="avares://Project.Launch/src/Views/imgs/whitex.png"
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
                                    <Image Source="avares://Project.Launch/src/Views/imgs/ReLogin.png"
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

                    <views:Pivot3DControl x:Name="SkinViewer"
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
                                       Source="avares://Project.Launch/src/Views/imgs/button-Start3.png"
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
                                       Source="avares://Project.Launch/src/Views/imgs/button-Start4.png"
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

                            <!-- Java版面板 -->
                            <StackPanel x:Name="JavaPanel"
                                        HorizontalAlignment="Center"
                                        VerticalAlignment="Center"
                                        Spacing="10"
                                        IsVisible="False">
                                <TextBlock Text="Java版"
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
                            <Image Source="avares://Project.Launch/src/Views/imgs/Download.png"
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
                            <Image Source="avares://Project.Launch/src/Views/imgs/Launch.png"
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
                            <Image Source="avares://Project.Launch/src/Views/imgs/Home.png"
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
                            <Image Source="avares://Project.Launch/src/Views/imgs/Setting.png"
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
                            <Image Source="avares://Project.Launch/src/Views/imgs/Tool.png"
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
                <Image Source="avares://Project.Launch/src/Views/imgs/warning.png"
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

### `src\Views\MainWindow.cs`

<!-- 1070 行 / 41.6 KB -->

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
        private StackPanel? _bedrockPanel, _javaPanel;
        private LiquidGlassSurface? _editionSlider;
        private TranslateTransform? _editionSliderTransform;
        private ScaleTransform? _editionSliderScale;
        private int _selectedEdition = 0;
        private DispatcherTimer? _editionAnimTimer;
        private double _editionAnimStartX, _editionAnimTargetX;
        private DateTime _editionAnimStartTime;

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
                Log("初始化完成（未登录，显示渐变标题栏）",
                    "Initialization complete (not logged in, gradient title bar)");

                Opened += async (s, e) =>
                {
                    try
                    {
                        await ShowLoginDialog();
                        await SetTransparentTitleBarAsync();
                        UpdateLoginStatus();
                        await RefreshAvatarAsync();

                        Log("登录窗口已关闭，切换为透明标题栏",
                            "Login window closed, switched to transparent title bar");
                    }
                    catch (Exception ex)
                    {
                        Log($"弹登录窗异常: {ex.Message}",
                            $"ShowLoginDialog exception: {ex.Message}");
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

                        Log("初始化完成（已登录，显示透明标题栏）",
                            "Initialization complete (logged in, transparent title bar)");
                    }
                    catch (Exception ex)
                    {
                        Log($"初始化异常: {ex.Message}",
                            $"Init exception: {ex.Message}");
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

            _tabAnimTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16)
            };
            _tabAnimTimer.Tick += (s, e) =>
            {
                var elapsed = DateTime.Now - _tabAnimStartTime;
                double progress = elapsed.TotalMilliseconds / AnimationConfig.TabMoveDuration.TotalMilliseconds;
                if (progress >= 1.0)
                {
                    progress = 1.0;
                    _tabAnimTimer.Stop();
                }

                double eased = progress < 0.5
                    ? 4 * progress * progress * progress
                    : 1 - Math.Pow(-2 * progress + 2, 3) / 2;
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

        // ============================================================
        // 下载页：基岩版 / Java版 选择器
        // ============================================================
        private void InitializeEditionSelector()
        {
            _bedrockEditionBtn = this.FindControl<Button>("BedrockEditionButton");
            _javaEditionBtn = this.FindControl<Button>("JavaEditionButton");
            _bedrockPanel = this.FindControl<StackPanel>("BedrockPanel");
            _javaPanel = this.FindControl<StackPanel>("JavaPanel");

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

            _editionAnimTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16)
            };
            _editionAnimTimer.Tick += (s, e) =>
            {
                var elapsed = DateTime.Now - _editionAnimStartTime;
                double progress = elapsed.TotalMilliseconds / duration.TotalMilliseconds;
                if (progress >= 1.0)
                {
                    progress = 1.0;
                    _editionAnimTimer.Stop();
                }

                double eased = progress < 0.5
                    ? 4 * progress * progress * progress
                    : 1 - Math.Pow(-2 * progress + 2, 3) / 2;

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

        private async void OnMainButtonClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (_currentInstance == null)
            {
                await ImportInstanceAsync();
            }
            else
            {
                await StartGameAsync();
            }
        }

        private async Task StartGameAsync()
        {
            if (_currentInstance == null) return;

            try
            {
                string versionsDir = Path.Combine(_currentInstance.RootPath, "versions");
                string jsonPath = Path.Combine(versionsDir, _currentInstance.VersionId,
                    _currentInstance.VersionId + ".json");
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
                    Log($"  Java: {j.Path} (major={j.MajorVersion})",
                        $"  Java: {j.Path} (major={j.MajorVersion})");

                var matched = JavaDetector.Match(javas, requiredJava);
                if (matched == null)
                {
                    Log($"未找到合适的 Java（需要 {requiredJava}）",
                        $"No suitable Java (need {requiredJava})");
                    await ShowToastAsync($"Java 环境异常：需要 Java {requiredJava}", "Messages");
                    return;
                }

                Log($"匹配到 Java: {matched.Path}", $"Matched Java: {matched.Path}");

                _currentInstance.JavaPath = matched.Path;
                InstanceService.SetSelectedInstance(_currentInstance);

                string playerName = App.PlayerName ?? "Steve";
                string playerUuid = App.OfflineUuid
                    ?? OfflineUuidGenerator.GenerateUuidString(playerName).Replace("-", "");

                await ShowToastAsync("正在启动游戏...", "Messages");

                bool ok = await GameLauncher.LaunchAsync(
                    _currentInstance, vj, matched, playerName, playerUuid);

                if (ok)
                    await ShowToastAsync("游戏已启动", "Messages");
                else
                    await ShowToastAsync("启动失败，查看日志", "Messages");
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
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel == null) return;

                var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    Title = "选择 Minecraft 目录（.minecraft 根目录或隔离实例目录）",
                    AllowMultiple = false
                });

                if (folders.Count < 1)
                {
                    Log("用户取消导入", "User cancelled import");
                    return;
                }

                var folder = folders[0];
                string path = folder.Path.LocalPath;
                Log($"用户选择目录: {path}", $"User selected directory: {path}");

                var result = MinecraftDirectoryAnalyzer.Analyze(path);
                if (!result.IsValid)
                {
                    Log("目录无效（未找到 versions/ 或 .minecraft/versions/）",
                        "Invalid directory (no versions/ or .minecraft/versions/)");
                    await ShowToastAsync("未找到有效的 Minecraft 目录", "Messages");
                    return;
                }

                Log($"分析结果: Layout={result.Layout}, Versions={string.Join(",", result.Versions)}",
                    $"Analysis: Layout={result.Layout}, Versions={string.Join(",", result.Versions)}");

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

        // ★ 主页"切换账户"按钮
        private async void OnSwitchAccountClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            await ShowAccountManagerAsync();
        }

        // ★ 账户管理窗口
        private async Task ShowAccountManagerAsync()
        {
            // 与登录窗行为一致：主窗切渐变标题栏（浅色文字/图标）+ 隐藏内容区
            UpdateTitleBarGradient(_defaultStartColor, _defaultEndColor);

            var contentArea = this.FindControl<Grid>("ContentArea");
            if (contentArea != null) contentArea.IsVisible = false;

            await Task.Delay(150);

            try
            {
                var win = new AccountManagerWindow();
                await win.ShowDialog(this);

                if (win.ShouldOpenLogin)
                {
                    await RunLoginWindowAsync();
                }

                UpdateLoginStatus();
                await RefreshAvatarAsync();
            }
            finally
            {
                if (contentArea != null) contentArea.IsVisible = true;
                // 关闭后恢复透明标题栏（深色文字/图标）
                await SetTransparentTitleBarAsync();
            }
        }

        // ★ 跑 LoginWindow 并处理结果
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

            if (!App.IsLoggedIn)
            {
                _ = ShowCancelToastAsync();
            }
        }

        // ★ 登录窗
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
                    var uri = new Uri("avares://Project.Launch/src/Views/imgs/black-.png");
                    using var stream = AssetLoader.Open(uri);
                    minImg.Source = new Bitmap(stream);
                }
                if (closeImg != null)
                {
                    var uri = new Uri("avares://Project.Launch/src/Views/imgs/blackx.png");
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
                    using var stream = AssetLoader.Open(new Uri(
                        $"avares://Project.Launch/src/Views/imgs/{mainIcon}"));
                    launchImg.Source = new Bitmap(stream);
                }
                if (launchTxt != null)
                    launchTxt.Text = hasInstance ? "开始游戏" : "导入实例";

                if (expandImg != null)
                {
                    using var stream = AssetLoader.Open(new Uri(
                        $"avares://Project.Launch/src/Views/imgs/{subIcon}"));
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
            if (!App.IsLoggedIn)
            {
                Log("未登录，跳过皮肤加载", "Not logged in, skip skin loading");
                return;
            }

            // ★ 按 UUID 决定 Steve / Alex
            string skinAssetPath = DefaultSkinSelector.GetSkinAssetPath(App.OfflineUuid);
            Log($"选中默认皮肤: {skinAssetPath}",
                $"Selected default skin: {skinAssetPath}");

            // 3D 皮肤
            try
            {
                var uri = new Uri(skinAssetPath);
                using var stream = AssetLoader.Open(uri);

                _fullSkinBitmap?.Dispose();
                _fullSkinBitmap = new Bitmap(stream);

                var skinViewer = this.FindControl<Pivot3DControl>("SkinViewer");
                if (skinViewer != null)
                {
                    skinViewer.Skin = _fullSkinBitmap;
                }

                Log("已加载 3D 皮肤", "3D skin loaded");
            }
            catch (Exception ex)
            {
                Log($"加载 3D 皮肤失败: {ex.Message}", $"Failed to load 3D skin: {ex.Message}");
            }

            // 主页 2D 头像
            var faceImg = this.FindControl<Image>("HomeFaceImage");
            var hatImg = this.FindControl<Image>("HomeHatImage");
            if (faceImg == null || hatImg == null) return;

            try
            {
                var uri = new Uri(skinAssetPath);
                using var stream = AssetLoader.Open(uri);
                var (face, hat) = await CropSkinAsync(stream);

                if (face != null && hat != null)
                {
                    faceImg.Source = face;
                    hatImg.Source = hat;
                    RenderOptions.SetBitmapInterpolationMode(faceImg, BitmapInterpolationMode.None);
                    RenderOptions.SetBitmapInterpolationMode(hatImg, BitmapInterpolationMode.None);

                    Log("已加载 2D 头像", "2D avatar loaded");
                }
            }
            catch (Exception ex)
            {
                Log($"加载头像失败: {ex.Message}", $"Failed to load avatar: {ex.Message}");
            }
        }

        private void Log(string zh, string en)
        {
            LogHelper.Write(LauncherPaths.MainWindowLog, zh, en);
        }

        private async Task ShowToastAsync(string message, string sound = "Messages")
        {
            SoundService.Play(sound);

            var toast = this.FindControl<Border>("CancelToast");
            var toastText = this.FindControl<TextBlock>("ToastText");
            if (toast == null) return;

            if (toastText != null)
                toastText.Text = message;

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

        private async Task<(Bitmap? face, Bitmap? hat)> CropSkinAsync(Stream stream)
        {
            try
            {
                byte[] imageData;
                using (var ms = new MemoryStream())
                {
                    await stream.CopyToAsync(ms);
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
                });
            }
            catch
            {
                return (null, null);
            }
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
            if (titleText != null)
                titleText.Foreground = useDark ? new SolidColorBrush(Colors.Black) : new SolidColorBrush(Colors.White);

            var minImg = this.FindControl<Image>("MinimizeImage");
            if (minImg != null)
            {
                var path = useDark ? "avares://Project.Launch/src/Views/imgs/black-.png"
                                   : "avares://Project.Launch/src/Views/imgs/white-.png";
                try
                {
                    var uri = new Uri(path);
                    using var stream = AssetLoader.Open(uri);
                    minImg.Source = new Bitmap(stream);
                }
                catch { }
            }

            var closeImg = this.FindControl<Image>("CloseImage");
            if (closeImg != null)
            {
                var path = useDark ? "avares://Project.Launch/src/Views/imgs/blackx.png"
                                   : "avares://Project.Launch/src/Views/imgs/whitex.png";
                try
                {
                    var uri = new Uri(path);
                    using var stream = AssetLoader.Open(uri);
                    closeImg.Source = new Bitmap(stream);
                }
                catch { }
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
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                BeginMoveDrag(e);
        }

        public void SetTitleBarGradient(Color start, Color end)
        {
            UpdateTitleBarGradient(start, end);
        }
    }
}
```

### `src\Views\LoginWindow.axaml`

<!-- 285 行 / 17 KB -->

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:media="clr-namespace:Avalonia.Media;assembly=Avalonia.Base"
        xmlns:avalonia="using:Avalonia.Controls"
        xmlns:lg="using:LiquidGlassAvaloniaUI"
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
                            <TextBlock Text="登录 Kilim Minecraft Launcher"
                                       FontSize="20"
                                       FontWeight="Bold"
                                       Foreground="#333"
                                       HorizontalAlignment="Center" />
                            <Grid Margin="0,10,0,0" Width="360" Height="44">
                                <Border Background="#D8D8D8" CornerRadius="8" />
                                <Grid ColumnDefinitions="*, *, *">
                                    <Button x:Name="OfflineLoginButton"
                                            Grid.Column="0"
                                            Background="Transparent"
                                            BorderThickness="0"
                                            Foreground="#666666"
                                            FontWeight="SemiBold"
                                            Content="离线登录"
                                            Cursor="Hand"
                                            HorizontalAlignment="Stretch"
                                            HorizontalContentAlignment="Center"
                                            Padding="0" />
                                    <Button x:Name="OnlineLoginButton"
                                            Grid.Column="1"
                                            Background="Transparent"
                                            BorderThickness="0"
                                            Foreground="#666666"
                                            FontWeight="SemiBold"
                                            Content="正版登录"
                                            Cursor="Hand"
                                            HorizontalAlignment="Stretch"
                                            HorizontalContentAlignment="Center"
                                            Padding="0" />
                                    <Button x:Name="ThirdPartyLoginButton"
                                            Grid.Column="2"
                                            Background="Transparent"
                                            BorderThickness="0"
                                            Foreground="#666666"
                                            FontWeight="SemiBold"
                                            Content="第三方登录"
                                            Cursor="Hand"
                                            HorizontalAlignment="Stretch"
                                            HorizontalContentAlignment="Center"
                                            Padding="0" />
                                </Grid>

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
                                                       HighlightEnabled="True"
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
                    <TextBlock DockPanel.Dock="Left" Text="rc-5" FontSize="11" Foreground="#999" VerticalAlignment="Center" />
                    <StackPanel DockPanel.Dock="Right" Orientation="Horizontal" VerticalAlignment="Center" Spacing="8">
                        <Button x:Name="GitHubButton" Background="Transparent" BorderThickness="0" Cursor="Hand" Padding="0" Width="44" Height="44" ToolTip.Tip="点击即访问仓库">
                            <Image Source="avares://Project.Launch/src/Views/imgs/github.png" Width="36" Height="36" Stretch="Uniform" />
                        </Button>
                        <TextBlock Text="请更换仓库不要露全部代码" FontSize="9" Foreground="#CC5555" VerticalAlignment="Center" Opacity="0.7" />
                    </StackPanel>
                </DockPanel>
            </Border>
        </Grid>
    </Border>
</Window>
```

### `src\Views\LoginWindow.xaml.cs`

<!-- 605 行 / 22.8 KB -->

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
using Project.Launch.Tools.Auth;

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

        private void UpdateButtonColors(int selectedIndex)
        {
            var buttons = new[] { _offlineBtn, _onlineBtn, _thirdPartyBtn };
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] != null)
                {
                    buttons[i]!.Foreground = (i == selectedIndex)
                        ? new SolidColorBrush(Colors.Black)
                        : new SolidColorBrush(Color.Parse("#666666"));
                    buttons[i]!.IsHitTestVisible = (i != selectedIndex);
                }
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
        // 正版登录：复制代码（★ 已改成扩展方法 SetTextAsync）
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
                var stevePath = "avares://Project.Launch/src/Views/imgs/skin/Steve.png";
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

### `src\Views\AccountManagerWindow.axaml`

<!-- 193 行 / 8.6 KB -->

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

### `src\Views\AccountManagerWindow.axaml.cs`

<!-- 319 行 / 11.5 KB -->

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

### `src\Views\Styles.axaml`

<!-- 192 行 / 8.8 KB -->

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

### `src\Views\Glass.axaml`

<!-- 147 行 / 7.5 KB -->

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

### `src\Views\read.me`

<!-- 4 行 / 0.1 KB -->

```text
--By Erics   九条开源
Gui层  图标源文件引用  美化

GUI Layer | Icon Asset References | Theme/Skin Scripts Directory
```

## 6.3 src\Tools —— 内核工具层

启动链路、Java 检测、实例管理、账户持久化、3D 皮肤投影、音效与配置。**这是启动器的"引擎"**。

### `src\Tools\GameLauncher.cs`

<!-- 339 行 / 13.9 KB -->

```csharp
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
```

### `src\Tools\JavaDetector.cs`

<!-- 183 行 / 6.1 KB -->

```csharp
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
```

### `src\Tools\MinecraftDirectoryAnalyzer.cs`

<!-- 98 行 / 3.5 KB -->

```csharp
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
```

### `src\Tools\MinecraftVersionJson.cs`

<!-- 109 行 / 3.5 KB -->

```csharp
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
```

### `src\Tools\InstanceData.cs`

<!-- 24 行 / 0.9 KB -->

```csharp
using System;
using System.Collections.Generic;

namespace Project.Launch.Tools
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

### `src\Tools\InstanceService.cs`

<!-- 84 行 / 2.9 KB -->

```csharp
using System;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Project.Launch.Tools
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

### `src\Tools\AccountData.cs`

<!-- 36 行 / 1.4 KB -->

```csharp
using System;
using System.Collections.Generic;

namespace Project.Launch.Tools
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

### `src\Tools\AccountService.cs`

<!-- 131 行 / 4.4 KB -->

```csharp
using System;
using System.IO;
using System.Text.Json;

namespace Project.Launch.Tools
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

### `src\Tools\OfflineUuidGenerator.cs`

<!-- 50 行 / 1.9 KB -->

```csharp
using System;
using System.Security.Cryptography;
using System.Text;

namespace Project.Launch.Tools
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

### `src\Tools\DefaultSkinSelector.cs`

<!-- 71 行 / 2.6 KB -->

```csharp
using System;

namespace Project.Launch.Tools
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
            "avares://Project.Launch/src/Views/imgs/skin/Steve.png";

        public const string AlexAssetPath =
            "avares://Project.Launch/src/Views/imgs/skin/Alex.png";

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

### `src\Tools\SkinCropper.cs`

<!-- 97 行 / 3.2 KB -->

```csharp
using System;
using System.IO;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Project.Launch.Tools
{
    /// <summary>
    /// 皮肤裁切工具：从皮肤 PNG 裁出方形头像
    /// </summary>
    public static class SkinCropper
    {
        /// <summary>
        /// 同步裁切：返回 (face 头像, hat 帽子层)
        /// 支持 64×64 / 128×128 皮肤，64×32 旧版不支持（返回 null）
        /// </summary>
        public static (Bitmap? face, Bitmap? hat) Crop(Stream stream)
        {
            try
            {
                using var src = new Bitmap(stream);
                int width = src.PixelSize.Width;
                int height = src.PixelSize.Height;

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

### `src\Tools\MinecraftSkinModel.cs`

<!-- 284 行 / 10.6 KB -->

```csharp
using System.Collections.Generic;

namespace Project.Launch.Tools
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

### `src\Tools\Pivot3DControl.cs`

<!-- 337 行 / 12.1 KB -->

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

namespace Project.Launch.Views
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

### `src\Tools\Pivot3DRenderer.cs`

<!-- 136 行 / 5.2 KB -->

```csharp
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Project.Launch.Tools
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

### `src\Tools\LauncherPaths.cs`

<!-- 48 行 / 1.6 KB -->

```csharp
using System;
using System.IO;

namespace Project.Launch.Tools
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

### `src\Tools\LogHelper.cs`

<!-- 51 行 / 1.6 KB -->

```csharp
using System;
using System.IO;

namespace Project.Launch.Tools
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

### `src\Tools\SoundConfig.cs`

<!-- 54 行 / 1.9 KB -->

```csharp
using System.Collections.Generic;

namespace Project.Launch.Tools
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
        public const string AssetRoot = "avares://Project.Launch/src/Sound/";

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

### `src\Tools\SoundService.cs`

<!-- 88 行 / 2.9 KB -->

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Platform;
using NAudio.Wave;

namespace Project.Launch.Tools
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

### `src\Tools\AnimationConfig.cs`

<!-- 79 行 / 3.4 KB -->

```csharp
using System;

namespace Project.Launch.Tools
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

### `src\Tools\InteractionConfig.cs`

<!-- 28 行 / 1 KB -->

```csharp
namespace Project.Launch.Tools
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

### `src\Tools\read.me`

<!-- 5 行 / 0.2 KB -->

```text
--By Erics  九条开源
内核级别 应用文件夹操作、内部操作 校验工具 下载 api握手

Kernel-level：Application Folder Operations, Internal
Operations, Verification Tools, Download API Handshake
```

## 6.4 src\Tools\Auth —— 微软正版登录

MSA → Xbox Live → XSTS → Minecraft 全链路。注意：`DeviceCodeAuthService.ClientId` 目前是**占位 ID**，正版登录走不通。

### `src\Tools\Auth\AuthModels.cs`

<!-- 186 行 / 5.4 KB -->

```csharp
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Project.Launch.Tools.Auth
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

### `src\Tools\Auth\MicrosoftAuthFlow.cs`

<!-- 167 行 / 6.7 KB -->

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Project.Launch.Tools.Auth
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

### `src\Tools\Auth\DeviceCodeAuthService.cs`

<!-- 186 行 / 7.1 KB -->

```csharp
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Project.Launch.Tools.Auth
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

### `src\Tools\Auth\XboxAuthService.cs`

<!-- 138 行 / 5.4 KB -->

```csharp
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Project.Launch.Tools.Auth
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

### `src\Tools\Auth\MinecraftAuthService.cs`

<!-- 116 行 / 4.6 KB -->

```csharp
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Project.Launch.Tools.Auth
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

## 6.5 src\ViewModel

预留的协调接口层，目前只有一个演示用文件，**未被任何代码引用**。

### `src\ViewModel\TestWindowButton.cs`

<!-- 20 行 / 0.5 KB -->

```csharp
// TestWindowViewModel.cs
using Avalonia.Controls;
using System;
namespace Project.Launch.ViewModel
{
    public class TestWindowViewModel
    {
        public void ShowHelloWorld()
        {
            var messageBox = new Window
            {
                Title = "Window",
                Content = "Hello World!",
                Width = 200,
                Height = 100
            };
            messageBox.Show();
        }
    }
}
```

### `src\ViewModel\read.me`

<!-- 6 行 / 0.3 KB -->

```text
--By Erics  九条开源
内核级别协调接口提前引入，后期进行插入更新、完成其他操作的时候能够更快插入

Kernel-level：Introduce the coordination interface early,
so that later insertions and updates can be
plugged in more quickly when performing other operations.
```

## 6.6 src\LiquidGlassAvaloniaUI —— 液态玻璃库（vendored 第三方）

**重要说明**：这是一个**内置（vendored）的第三方库**，上游 MIT 项目为 `KaranocaVe/LiquidGlassAvaloniaUI`。
主项目通过 `<Compile Remove="src\LiquidGlassAvaloniaUI\**" />` 把它的源码排除在自己的编译之外，再以 `ProjectReference` 引用它独立编译出的 DLL。

**本次根因修复改动了其中两个文件**：
- `LiquidGlassBackdropProvider.cs` —— 抓取实现改为 Avalonia 自带的 `RenderTargetBitmap.Render(Visual)`（**根因修复**）+ 移动触发条件
- `LiquidGlassSurface.cs` —— 订阅渲染变换变化

其余文件基本是上游原样。**改动前请先读 [3.8 液态玻璃子系统](#38-液态玻璃子系统--含本次根因修复)。**

### `src\LiquidGlassAvaloniaUI\LiquidGlassAvaloniaUI.csproj`

<!-- 40 行 / 1.9 KB -->

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

### `src\LiquidGlassAvaloniaUI\LiquidGlassBackdropProvider.cs`

<!-- 848 行 / 34 KB -->

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

### `src\LiquidGlassAvaloniaUI\LiquidGlassSurface.cs`

<!-- 969 行 / 38.5 KB -->

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

### `src\LiquidGlassAvaloniaUI\LiquidGlassBackdropSnapshot.cs`

<!-- 231 行 / 7.6 KB -->

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

### `src\LiquidGlassAvaloniaUI\LiquidGlassDrawOperation.cs`

<!-- 864 行 / 35 KB -->

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

### `src\LiquidGlassAvaloniaUI\LiquidGlassDrawParameters.cs`

<!-- 65 行 / 2.2 KB -->

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

### `src\LiquidGlassAvaloniaUI\LiquidGlassVisualRenderer.cs`

<!-- 154 行 / 5.4 KB -->

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

### `src\LiquidGlassAvaloniaUI\LiquidGlassBackdrop.cs`

<!-- 35 行 / 0.9 KB -->

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

### `src\LiquidGlassAvaloniaUI\LiquidGlassInteractiveSurface.cs`

<!-- 388 行 / 12.7 KB -->

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

### `src\LiquidGlassAvaloniaUI\LiquidGlassOverlays.cs`

<!-- 54 行 / 1.9 KB -->

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

### `src\LiquidGlassAvaloniaUI\LiquidGlassShadowDrawOperations.cs`

<!-- 227 行 / 7.2 KB -->

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

### `src\LiquidGlassAvaloniaUI\LiquidGlassAppBuilderExtensions.cs`

<!-- 26 行 / 0.7 KB -->

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

### `src\LiquidGlassAvaloniaUI\LiquidGlassDiagnostics.cs`

<!-- 210 行 / 11 KB -->

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

### `src\LiquidGlassAvaloniaUI\LiquidGlassPathUtils.cs`

<!-- 43 行 / 1.4 KB -->

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

### `src\LiquidGlassAvaloniaUI\Assets\Shaders\LiquidGlassShader.sksl`

<!-- 117 行 / 3.9 KB -->

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

### `src\LiquidGlassAvaloniaUI\Assets\Shaders\LiquidGlassHighlight.sksl`

<!-- 54 行 / 1.9 KB -->

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

### `src\LiquidGlassAvaloniaUI\Assets\Shaders\LiquidGlassInteractiveHighlight.sksl`

<!-- 17 行 / 0.5 KB -->

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

### `src\LiquidGlassAvaloniaUI\Assets\Shaders\LiquidGlassGamma.sksl`

<!-- 15 行 / 0.3 KB -->

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

### `src\LiquidGlassAvaloniaUI\Assets\Shaders\LiquidGlassProgressiveMask.sksl`

<!-- 37 行 / 1.2 KB -->

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

### `src\LiquidGlassAvaloniaUI\Assets\Shaders\LiquidGlassBackdropTransform.sksl`

<!-- 19 行 / 0.6 KB -->

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

## 6.7 二进制资源清单（不嵌入内容）

这些文件以 `AvaloniaResource` 形式打进程序集，**内容不在此文档内**。若需要改动它们（换图/换音效），请向用户索要源文件。

| 路径 | 用途 |
|---|---|
| `src\Sound\BigError.wav` | 音效 |
| `src\Sound\DownloadDone.wav` | 音效 |
| `src\Sound\Errors.wav` | 音效 |
| `src\Sound\Messages.wav` | 音效 |
| `src\Sound\Messages2.wav` | 音效 |
| `src\Sound\Open.wav` | 音效 |
| `src\Views\imgs\a7d21-in3zc.png` | 界面图片 |
| `src\Views\imgs\black-.png` | 界面图片 |
| `src\Views\imgs\blackx.png` | 界面图片 |
| `src\Views\imgs\button-Start1.png` | 界面图片 |
| `src\Views\imgs\button-Start2.png` | 界面图片 |
| `src\Views\imgs\button-Start3.png` | 界面图片 |
| `src\Views\imgs\button-Start4.png` | 界面图片 |
| `src\Views\imgs\cyan_glazed_terracotta.png` | 界面图片 |
| `src\Views\imgs\Download.png` | 界面图片 |
| `src\Views\imgs\github.png` | 界面图片 |
| `src\Views\imgs\hav-Background\button-choose1.png` | 备用背景图组 |
| `src\Views\imgs\hav-Background\button-Start1.png` | 备用背景图组 |
| `src\Views\imgs\hav-Background\button-Start2.png` | 备用背景图组 |
| `src\Views\imgs\hav-Background\button-Start3.png` | 备用背景图组 |
| `src\Views\imgs\hav-Background\button-Start4.png` | 备用背景图组 |
| `src\Views\imgs\hav-Background\github1.jpg` | 备用背景图组 |
| `src\Views\imgs\Home.png` | 界面图片 |
| `src\Views\imgs\Launch.png` | 界面图片 |
| `src\Views\imgs\MainLogo.ico` | 程序图标（同时作为任务栏图标，以 Content 复制） |
| `src\Views\imgs\ReLogin.png` | 界面图片 |
| `src\Views\imgs\Setting.png` | 界面图片 |
| `src\Views\imgs\show.png` | 界面图片 |
| `src\Views\imgs\show-64x.png` | 界面图片 |
| `src\Views\imgs\skin\_-酔った东京空_3.png` | 皮肤贴图（3D 投影 / 头像裁切用） |
| `src\Views\imgs\skin\【搬运】◇苦力怕娘（作者：钻茜）.png` | 皮肤贴图（3D 投影 / 头像裁切用） |
| `src\Views\imgs\skin\Alex.png` | 皮肤贴图（3D 投影 / 头像裁切用） |
| `src\Views\imgs\skin\Steve.png` | 皮肤贴图（3D 投影 / 头像裁切用） |
| `src\Views\imgs\skin\艾利克斯-Alex 夏日款.png` | 皮肤贴图（3D 投影 / 头像裁切用） |
| `src\Views\imgs\skin\菜鸟.png` | 皮肤贴图（3D 投影 / 头像裁切用） |
| `src\Views\imgs\skin\电脑人.png` | 皮肤贴图（3D 投影 / 头像裁切用） |
| `src\Views\imgs\skin\粉嫩女装史蒂夫.png` | 皮肤贴图（3D 投影 / 头像裁切用） |
| `src\Views\imgs\skin\胡桃.png` | 皮肤贴图（3D 投影 / 头像裁切用） |
| `src\Views\imgs\skin\猎奇史蒂夫.png` | 皮肤贴图（3D 投影 / 头像裁切用） |
| `src\Views\imgs\skin\猫娘史蒂夫（白丝.png` | 皮肤贴图（3D 投影 / 头像裁切用） |
| `src\Views\imgs\skin\美西螈.png` | 皮肤贴图（3D 投影 / 头像裁切用） |
| `src\Views\imgs\skin\史蒂夫，但是byd脸.png` | 皮肤贴图（3D 投影 / 头像裁切用） |
| `src\Views\imgs\skin\史蒂夫【卡兰兹大眼】.png` | 皮肤贴图（3D 投影 / 头像裁切用） |
| `src\Views\imgs\skin\幼猫.png` | 皮肤贴图（3D 投影 / 头像裁切用） |
| `src\Views\imgs\Tool.png` | 界面图片 |
| `src\Views\imgs\warning.png` | 界面图片 |
| `src\Views\imgs\white-.png` | 界面图片 |
| `src\Views\imgs\whitex.png` | 界面图片 |
| `src\Views\imgs\wildflowers.png` | 界面图片 |
| `src\Views\imgs\微信图片_20260803115740_100_82.jpg` | 界面图片 |

> 合计 50 个文件，约 2075 KB。


---

# 第七部分：协作约定

> 以下是本项目原作者与 AI / 协作者之间形成的约定，**接手时请一并遵守**。

## 7.1 分工模式

**用户负责架构与进度规划，只把一个小进度点交给协作者实现。** 因此：

1. **只改被要求的那一个功能**，不做顺手重构、不加未要求的功能
2. 有想法**先说出来问**，不擅自拍板
3. 改动前**先读相关文件**，改完汇报**改了哪些文件、什么位置、为什么**
4. 遇到架构冲突或多种做法，**先问再写**
5. 用户说"不写代码"时**绝对不碰代码**
6. **源码就在仓库里**，任何文件都可用 read 工具直接读，不要向用户索要代码
   （⚠️ **例外**：如果你手上的是本文档而非仓库，那么本文档的源码部分可能已过期 —— 见 [0.1](#01-文档一定会过期--这是设计前提)）

## 7.2 本项目的两个"反直觉"教训（值得记住）

### 教训一：渲染类问题，先看中间产物，再谈推理

这个液态玻璃 bug 花了极长时间，因为前期的排查全都在**推理与调参**：
DPI、采样矩阵、脏矩形抑制、变换事件、滤镜裁剪……**每一条看起来都很有道理，全部被证伪**。

真正的突破点只有一步：**把库抓到的背景快照存成 PNG 看了一眼** —— 发现它是全透明的。

> **所以：遇到"渲染结果不对"，第一件事应该是把链路中间的产物 dump 出来看，而不是读代码猜。**

### 教训二：改完必须验证"产物里真的有我的改动"

本次踩到一个很隐蔽的坑：只改了玻璃库的 `.cs`，然后 `dotnet build` 主项目，
**MSBuild 没有重新编译玻璃库**，构建却报告"成功"。如果不去查 DLL 时间戳 / 在元数据里搜方法名，
你会以为改动生效了，实际上跑的还是旧代码。

> **所以：构建成功 ≠ 改动生效。** 特别是改 vendored 子项目时，务必验证产物（见 [2.3 陷阱 5](#23--环境陷阱务必先读)）。

### 教训三：用户的现象描述是最有价值的线索

这次能定位，关键信息是用户说的"**只有移动到下载才有动画**"和"**中间一条硬竖缝**"。
前者把范围从"玻璃不好看"缩小到"**位置相关**"，后者直接指向"**某条边界在钳制采样**"。

> **所以：追问具体现象（哪个位置、哪个方向、边界在哪）比反复读代码更有价值。**

## 7.3 排查脚本的存放约定

- **绝不在仓库根目录建临时 csproj / 脚本**（见 [2.3 陷阱 2](#23--环境陷阱务必先读)）
- 排查用的临时工程、脚本、实验产物一律放 `%TEMP%` 下
- **不要把实验产物（PNG / 日志 / 临时 md）留在仓库里** —— 本次排查中曾有协作者越界改动
  `src\Views\LoginWindow.axaml`（把 `RefractionHeight` 改成 `18` 并留下 4 个多余引号，**XAML 已损坏**），
  以及把分析记录写进仓库根目录。**发现这类情况应立即还原并告知用户。**

## 7.4 交付前检查清单

| 检查项 | 说明 |
|---|---|
| 编译 | `dotnet build Project.Launch.csproj -c Debug --no-incremental`，确认 0 错误 |
| 产物 | 确认 DLL / exe 时间戳已更新，关键改动确实在产物里 |
| 运行 | 至少跑一次，确认无 `crash.log`、无异常 CPU 占用 |
| 痕迹 | 清掉调试代码、临时日志、开发期提示文字（如"请更换仓库不要露全部代码"、硬编码 `rc-5`） |
| 仓库 | `git status` 确认没有把实验产物 / 运行日志 / `bin`、`obj` 带进提交 |
| 汇报 | 明确列出：改了哪些文件、哪一行、为什么 |

## 7.5 用户环境备忘

| 项 | 值 |
|---|---|
| 显示缩放 | 125%（`RenderScaling = 1.25`）—— 涉及像素计算时注意 |
| 数据目录 | `<exe 目录>\KilimLauncher\`，**不在 `%AppData%`** |
| 已有账户 | `offline_ZCLEZM`、`offline_321`（均离线账户） |
| 3D 皮肤 | 检测结果为 Slim |
| 仓库 | 私有，`master` 分支，作者 `Erics <Zhouxuanyao2333@outlook.com>` |

---

# 附：如何更新本文档

本文档的**第六部分（完整源码）是脚本从仓库真实文件生成的**，不要手工编辑那一部分。

若需要重新生成：

1. 向用户索要 `gen_code.ps1`（本次生成脚本，位于 `%TEMP%\kilim_doc\`，未入库）
2. 或在仓库根目录执行等价逻辑：按 6.1~6.6 的文件清单依次读取文件，
   包进带语言标记的代码块，拼成一个 markdown
3. **第一~五、七部分（叙述部分）如需更新，请针对性地重写对应小节**，
   并在文档开头的表格里更新 `分支 / HEAD` 与 `文档生成` 日期

> ⚠️ **再次强调**：只要用户又改了代码，本文档第六部分对应的小节立即失效。
> **需要查/改具体实现时，直接向用户索要该文件的最新版本，不要依赖本文档里的代码。**

---

*文档结束。如需任何一节的展开或有疑问，请向用户确认，不要基于本文档的推测直接动手。*

