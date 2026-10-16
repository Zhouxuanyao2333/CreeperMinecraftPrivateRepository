# Kilim Minecraft Launcher — 完整交接文档（自包含）

> **本文件是自包含的**：包含全部源码，下一个 Agent 只需看这一个文件即可理解并继续开发，**无需再向用户索要任何代码**。
>
> - 项目：Kilim Minecraft Launcher（C# / .NET 10 + Avalonia 12，无 WebView，自绘液态玻璃 UI）
> - 分支：`master`，HEAD `e0a782e`（rc-6人物投影完善）
> - 生成：2026-08
> - **⚠️ 工作区有大量未提交改动**（账户系统、3D 皮肤、下载页 UI 骨架均在 working tree）

---

# 目录

- [第一部分：项目现状与交接要点](#第一部分项目现状与交接要点)
- [第二部分：已实现功能详解](#第二部分已实现功能详解)
- [第三部分：下一步需求 — 下载功能](#第三部分下一步需求--下载功能)
- [第四部分：完整源码](#第四部分完整源码)
- [第五部分：协作约定](#第五部分协作约定)

---

# 第一部分：项目现状与交接要点

## 1.1 一句话现状

**前端完成度高、启动链路可用，但"下载功能"完全没做**（只有 UI 占位）。
当前是一个**导入式**启动器：依赖用户机器上已装好的 Minecraft 目录，扫描后启动，不从网络下载游戏文件。

## 1.2 技术栈

| 项目 | 内容 |
|---|---|
| 框架 | .NET 10（`net10.0`），win-x64 |
| UI | Avalonia 12.1.2 + Fluent 主题 + Inter 字体 |
| 渲染 | SkiaSharp 3.119.4（3D 皮肤投影、玻璃着色器） |
| 音频 | NAudio 2.2.1 |
| 发布 | 单文件自包含（`PublishSingleFile` + 压缩），未裁剪 |
| 子系统 | `src/LiquidGlassAvaloniaUI`（独立 csproj，主项目引用 DLL 但**源码编译排除**） |

**构建命令**
```bash
dotnet build Project.Launch.csproj -c Debug
```

## 1.3 ⚠️ 环境陷阱（务必先读，能省大量时间）

1. **编译前必须关掉正在运行的启动器**
   否则 `dotnet build` 最后复制 `Project.Launch.exe` 报 `MSB3027/MSB3021 文件被锁定`。
   **这不是代码错误**，只是文件占用。

2. **绝不要在仓库根目录创建临时 csproj**
   主项目使用默认 `**/*.cs` 通配。临时项目 `obj/` 下生成的 `AssemblyInfo.cs` 会被吸进主项目编译，
   导致 `CS0579 程序集特性重复`（8 个错误）。调试脚本请放在仓库外，或在 csproj 里显式 `<Compile Remove="..."/>`。

3. **`Border` 是 `Decorator`，只能有一个 Child**
   多个子元素必须套一层 `Grid`，否则报
   `AVLN3000: Unable to find a setter that allows multiple assignments to the property Child`。

4. **新增窗口需要在 csproj 的 `<AvaloniaXaml Update="...">` 里登记**，否则不参与 XAML 编译。

5. **数据目录不在 AppData**，而是 exe 同级的 `KilimLauncher/`。

6. **用户系统为 120% DPI 缩放**（`AppliedDPI=120`），涉及像素坐标计算时要注意。

## 1.4 已确认的代码事实（前人排查结论，勿重复踩坑）

- 3D 光照**已修复过一次**：原本面片法线朝内导致"光打在背面、正脸全黑"，
  现已在 `Pivot3DRenderer.EstimateShade` 中做法线取反 + 光照方向逆变换回模型空间。
  **改动 3D 渲染时不要把这里改回去。**
- Avalonia 12.1.2 的 `VisualExtensions.GetTransformedBounds`（内部本地函数 `g__Visit|9_0`）
  **包含 RenderTransform**（已反汇编验证）。
- `SKShader.CreateImage` 的 `localMatrix` 约定与 `LiquidGlassDrawOperation.WithPixelOrigin`
  的写法一致（已用真实 SkiaSharp 管线做离屏实验验证，100%/120% DPI 下均精确命中预期像素）。

---

# 第二部分：已实现功能详解

## 2.1 账户系统 ✅ 可用

**离线登录**（`LoginWindow` → `OfflineUuidGenerator`）
- 输入用户名 → `MD5("OfflinePlayer:" + username)`，设 version 3 + IETF variant，
  **手动按大端序拼 hex**（不经过 `Guid` 类，避免字节序错误）
- 账户 ID 格式 `offline_{username}`

**微软正版登录**（`src/Tools/Auth/`）— 7 阶段全链路已实现

| 阶段 | 文件 | 说明 |
|---|---|---|
| 1 申请设备代码 | `DeviceCodeAuthService` | `consumers` 租户；scope `XboxLive.signin XboxLive.offline_access` |
| 3 轮询授权 | `DeviceCodeAuthService.PollForTokenAsync` | 支持 `CancellationToken`；处理 `authorization_pending` / `slow_down` / `access_denied` / `expired_token` |
| 4 XBL | `XboxAuthService` | `RpsTicket = "d=" + token`（**前缀必须有**） |
| 5 XSTS | `XboxAuthService` | **含 XErr 错误码中文映射**（2148916233/235/236/237/238） |
| 6a MC 认证 | `MinecraftAuthService` | `XBL3.0 x={uhs};{xsts}` |
| 6b 拉 profile | `MinecraftAuthService` | 404 → 抛"此微软账号没有购买 Minecraft" |
| 7 装配账户 | `MicrosoftAuthFlow` | 统一编排，通过 `Progress` / `DeviceCodeReady` 事件回调 UI |

> **⚠️ 当前阻塞**：`DeviceCodeAuthService.ClientId = "7dddbcf7-5d7d-46cf-96fc-292d6f392cd0"` 是
> **占位测试用 ID，被 Mojang 拒绝**。用户已申请自有 AppID，**等审批下来后替换该常量**即可打通。
>
> **⚠️ 未实现**：`AccountInfo.MicrosoftRefreshToken` 存了但**没有任何地方用它换新 token**，
> 24 小时后需重新走设备代码登录。

**账户持久化**（`AccountService` + `accounts.json`）
- 原子写：写 `.tmp` → `File.Move(overwrite: true)`
- 所有 read-modify-write 收口在 `ModifyAccounts` 的单个 `lock` 内
- `LoginCheck` 控制启动时是否跳过登录窗

**账户管理窗**（`AccountManagerWindow`）
- 列表按 `LastUsed` 倒序、点击切换、删除（带确认弹窗）
- `ShouldOpenLogin` 属性通知主窗外层是否要拉起登录窗

## 2.2 游戏启动 ✅ 可用（导入式）

`GameLauncher.LaunchAsync` 是**真正能跑的启动器实现**：

- 兼容**新版** `arguments.jvm` / `arguments.game` 数组 与 **旧版** `minecraftArguments` 字符串
- **全量占位符替换**：`${classpath}` `${auth_player_name}` `${auth_uuid}` `${assets_index_name}`
  `${game_directory}` `${natives_directory}` `${classpath_separator}` `${library_directory}` 等
- **rules 规则判定**：库与参数都做 OS 过滤（仅 Windows 判定）
- **natives 自动解压**：跳过 `META-INF/`，处理 `${arch}` 的 64/32 位
- 两种目录布局分流：`standard`（`.minecraft` 根）/ `isolated`（`.minecraft` 子目录）
- 进程输出重定向到 `instances/{id}/game.log`

> **⚠️ 已知缺陷（正版相关）**：`ReplacePlaceholders` 里
> `${auth_access_token}` 硬编码 `"0"`、`${user_type}` 硬编码 `"legacy"`。
> → **正版账号实际以离线身份进服，进不了正版服务器**。AppID 到位后必须一并修掉。
>
> **⚠️ 其他缺陷**：无 `-Xmx/-Xms` 内存设置；分辨率在占位符里写死 925×530；
> 无启动前完整性校验（缺库只会静默跳过）；起进程后立刻 `return true`，监听不到游戏退出；
> 无 Forge/Fabric 等加载器支持。

## 2.3 Java 检测 ✅ 可用

`JavaDetector.DetectAllAsync` 从 5 个来源收集候选：
1. `JAVA_HOME`
2. `PATH` 逐段
3. 7 个常见 JDK 厂商目录（Java / Adoptium / Microsoft / Zulu / BellSoft / Corretto）
4. 实例已保存路径

执行 `java -version` 解析主版本（兼容 `1.8.0_x` 与 `17.0.9` 两种格式）。
`Match()` 策略：优先精确匹配版本 JSON 的 `javaVersion.majorVersion`，否则取**最小且 ≥ 要求**的。

## 2.4 实例管理 ✅ 可用

`MinecraftDirectoryAnalyzer.Analyze(path)` 自动识别两种布局：
- **形态 A**：选了 `.minecraft` 根目录（`versions/` 在根下）→ `Layout="standard"`
- **形态 B**：隔离实例（`.minecraft/versions/`）→ `Layout="isolated"`

扫出所有"目录名 == 同名 .json"的版本，持久化到 `instances.json`。

## 2.5 UI 交互 ✅ 完成度较高

- **主窗**：5 个 Tab（下载/启动/主页/设置/工具），底部玻璃滑块动画
  （700ms EaseInOut + 梯形缩放脉冲 + 模糊）+ 面板淡入淡出
- **自绘标题栏**：`WindowDecorations="None"` + 35px 自绘 `TitleBarBorder`，两态切换
  （**渐变浅色** ↔ **透明深色**），图标白/黑切换 + 淡出淡入
- **皮肤头像**：按 UUID 推算 Steve/Alex（`DefaultSkinSelector`，取第 8/16/24/32 位 hex 异或判奇偶），
  裁出头像脸+帽子两层（`SkinCropper`）
- **音效**：`SoundService`（NAudio，后台线程播放避免卡 UI）
- **Toast**：主窗右下角提示

**标题栏两态机制**（重要，改 UI 时必读）：
- **态 A 渐变**：`UpdateTitleBarGradient(_defaultStartColor, _defaultEndColor)`，
  主窗启动未登录时、以及弹出模态窗（登录窗/账户管理窗）时使用
- **态 B 透明**：`SetTransparentTitleBarAsync()`，登录完成后恢复；
  实现是"图标淡出 → 200ms → 换黑图 → 淡入"，背景/字色由 XAML 的 `BrushTransition` 0.4s 补间
- 注意两个时长不一致（0.2 vs 0.4），图标与背景并非同步收尾

## 2.6 3D 皮肤投影 ✅ 可用（启动页）

- `MinecraftSkinModel`：手工定义头/身/手臂/腿 + 各 overlay 的顶点与 UV；
  支持 **Classic（4 宽臂）/ Slim（3 宽臂）**
- `Pivot3DControl`：`ICustomDrawOperation` + SkiaSharp `DrawVertices`，
  伪 3D 投影 + 画家算法排序 + 背面剔除
- **皮肤类型自动检测**：按 64 宽比例缩放检测坐标，看 (54,20) 起 12 行像素是否全透明 → 判 Slim/Classic；
  支持 HD 皮肤（128/256）
- 自动旋转（33ms timer，交互后停 2 秒）+ 鼠标拖拽旋转（俯仰限 ±30°）
- **光照**：光源跟随相机（法线已修正为朝外）

## 2.7 液态玻璃 UI（自研子系统）

`src/LiquidGlassAvaloniaUI/` 是独立库（在主项目中**源码被排除**，只引用编译好的 DLL）：

- 从 `TopLevel` **抓取背景快照**（`RenderTargetBitmap` + 排除订阅者自身），发布给所有 `LiquidGlassSurface`
- SKSL 运行时着色器：透镜折射（`LiquidGlassShader.sksl`）、色散、高光描边、内阴影、渐进模糊、伽马
- 快照缓存：8×8 采样哈希去重、按脏矩形/cadence 节流、滤镜结果 LRU（8 条）
- 可用参数：`RefractionHeight` `RefractionAmount` `ChromaticAberration` `DepthEffect`
  `BlurRadius` `Vibrancy` `Brightness` `Contrast` `Shadow*` `Highlight*` 等

### ⚠️ 已知未解之谜：登录滑块色散/模糊只在最左格生效

`LoginWindow` 的登录模式滑块（`LoginModeSlider`）**只有最左格（离线登录）有折射/色散效果，
中间和右侧两格表现为一块平的浅色玻璃**。用户反馈"好几次用各种 AI 怎么改都是这样"。

**已做过的排查（都是硬证据，勿重复）**：
1. 反汇编 Avalonia 12.1.2 的 `VisualExtensions.GetTransformedBounds` → **包含 RenderTransform**
   → "捕捉区不跟着滑块走"的假设**被证伪**
2. 离屏实验用真实 SkiaSharp 管线复现 `WithPixelOrigin` 矩阵
   → 在 **100% 与 120% DPI、三个格子位置**全部精确命中预期像素 → 采样公式**正确**
3. 用户系统为 **120% 缩放**（已核实注册表 `AppliedDPI=120`）
4. 调参实验（折射带 4→10、强度 40→20、加静止模糊、开 DepthEffect）**完全无效**
5. 用户已确认：**能看到玻璃下的文字**，只是没折射/模糊

**当前最可疑方向（未验证）**：**快照刷新时机**。
库在静止态仅靠合成器脏矩形事件触发重捕，叠加 33ms 节流 + 哈希跳过 + "订阅者自身脏矩形"过滤。
滑块靠 `TranslateTransform` 移动，若动画期间部分帧捕捉被合并，不同格子快照会停在**不同的旧时刻**，
采样钳制到快照边缘（白色卡片）→ 白块。
**这能解释为什么调参无效**（参数不影响快照是否刷新）。
→ 若日后要治：在动画结束帧调用一次**强制重捕**（绕过哈希跳过），而不是继续调参数。

**用户当前决定**：已要求把 `LoginModeSlider` 的**色散和模糊全部砍掉，只保留位移+缩放动画**
（已在源码中实施：`ChromaticAberration="False"`，并删除了代码里的模糊驱动逻辑）。

---

# 第三部分：下一步需求 — 下载功能

## 3.1 用户的产品定位与硬约束（重要）

- 启动器要引入**基岩版版本管理**，但**为规避版权只支持"安装包下载"**（不重分发游戏本体）
- 正版（Java）与基岩版两条线并行，**共用一套下载引擎**
- 基岩版数据源走第三方站 **MCAPPX**，站长的硬性约束：
  1. **下载不走他们的服务器**，全部走微软官方 CDN
  2. **其下载服务一律禁止调用**（会触发机器人挑战返回 403）
     → 启动器**绝不能热链 mcappx 的任何下载链接**
  3. **唯一要求：公开项目必须标注来源 `mcappx.com`**（建议做进"关于"页 + 下载页脚注）
  4. 数据源对部分区域屏蔽，可向他们申请访问权限
     - ⚠️ **实测该数据接口在用户本机请求超时**
       （`https://data.mcappx.com/v2/bedrock.json` 30s 无响应，文档站可正常访问）—— **印证区域屏蔽**

## 3.2 Java 版下载源（已调研确认）

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

## 3.3 基岩版下载源（MCAPPX 数据源）

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

## 3.4 建议架构（三段式，Java/基岩共用）

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

**与现有架构的衔接（好消息）**
> 下载器把版本装进一个 **standard 布局目录**（`versions/` + `libraries/` + `assets/`）后，
> `MinecraftDirectoryAnalyzer.Analyze` **直接就能扫出来**，`GameLauncher` **完全不用改**。
> → "下载安装新版本" = 新建目录 → 下载 → 调 `SetSelectedInstance` 注册，
>   **与现有"导入"走同一条下游链路**。
>
> → 基岩版不能用 `InstanceInfo`（没有 .minecraft/versions/json/Java），需新建 `BedrockInstance` 实体，
>   目录建议在 `LauncherPaths` 下加 `bedrock/`。

## 3.5 待产品决策（用户尚未拍板）

1. **基岩版先做 GDK 还是 UWP？**（建议 GDK 先行，UWP 后置 + Store 兜底）
2. **只下载，还是下载+安装一条龙？**（后者要处理提权、依赖、错误码）
3. **单版本还是多版本共存？**（建议 v1 单版本）
4. **区域屏蔽只做缓存容错，还是去找站长申请访问权限？**
5. **镜像策略**：BMCLAPI 优先 + 官方回退，还是启动时测速自动选最快？
6. **Mod 下载**（Modrinth / CurseForge）是否进近期规划？

## 3.6 建议路线图

1. **P1 版本清单**：拉 manifest、解析、版本列表 UI（Release/Snapshot 分类 + 搜索）——零风险，先把下载 Tab 激活
2. **P2 原版安装**：版本 JSON（含 `inheritsFrom`）→ client.jar → libraries → assets，
   SHA1 校验 + 并发 + 断点续传 + BMCLAPI 优先
3. **P3 安装进度 UI**：速度/进度条/取消/重试/镜像设置
4. **P4 Java 运行时自动下载**（1.17+ 必备）
5. **P5 加载器**（Fabric / Forge / NeoForge）
6. **P6 Mod 下载**（Modrinth API v2 无需 key；CurseForge 需申请 key）

## 3.7 可参考的实现

- **MinecraftLaunch**（Blessed-Flight）：C# 启动器库，与项目技术栈一致，
  **强烈建议翻源码参考**镜像处理与下载实现
- **CmlLib.Core**：老牌 C# 启动器库
- **HMCL**：镜像列表与测速切换的标杆
- `Downloader`（Bezzad）NuGet：现成分段下载组件

## 3.8 下载页 UI 骨架（已完成，逻辑未写）

`MainWindow.axaml` 的 `DownloadPanel` 已完成骨架：
- **左上角**：基岩版 / Java版 切换器（240×44 灰轨道 + `LiquidGlassSurface` 滑块 `EditionSlider`）
- **下方安全区**：白色圆角卡片（`CornerRadius=12` + `DropShadowEffect`），
  内含 `BedrockPanel` / `JavaPanel` 两个占位面板（目前只显示"版本列表（待接入）"）
- 切换逻辑在 `MainWindow.cs` 的 `InitializeEditionSelector` / `SelectEdition` / `AnimateEditionSliderTo`
  （复用 `AnimationConfig.Slider*` 参数，曲线与 LoginWindow 完全一致）

---

# 第四部分：完整源码

> 以下为全部源码（29 个文件），按目录组织。**这是文档生成时的最新状态。**

## 4.1 根目录

### `Project.Launch.csproj`

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

### `Program.cs`

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

## 4.2 src\Views

### `src\Views\Styles.axaml`

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

### `src\Views\AccountManagerWindow.axaml`

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

### `src\Views\LoginWindow.axaml`

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
                                                       ChromaticAberration="False"
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

### `src\Views\MainWindow.axaml`

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

### `src\Tools\AccountData.cs`

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

### `src\Tools\AnimationConfig.cs`

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

### `src\Tools\DefaultSkinSelector.cs`

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

### `src\Tools\GameLauncher.cs`

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

### `src\Tools\InstanceData.cs`

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

### `src\Tools\InteractionConfig.cs`

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

### `src\Tools\JavaDetector.cs`

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

### `src\Tools\LauncherPaths.cs`

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

### `src\Tools\MinecraftDirectoryAnalyzer.cs`

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

### `src\Tools\MinecraftSkinModel.cs`

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

### `src\Tools\MinecraftVersionJson.cs`

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

### `src\Tools\OfflineUuidGenerator.cs`

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

### `src\Tools\Pivot3DControl.cs`

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

### `src\Tools\SkinCropper.cs`

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

### `src\Tools\SoundConfig.cs`

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

### `src\Tools\Auth\AuthModels.cs`

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

### `src\Tools\Auth\DeviceCodeAuthService.cs`

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

### `src\Tools\Auth\MicrosoftAuthFlow.cs`

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

### `src\Tools\Auth\MinecraftAuthService.cs`

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

### `src\Tools\Auth\XboxAuthService.cs`

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



---

# 第五部分：协作约定

用户负责**架构与进度规划**，只把**一个小进度点**交给 Agent 实现。因此：

1. **只改被要求的那一个功能**，不做顺手重构、不加未要求的功能
2. 有想法**先说出来问**，不擅自拍板
3. 改动前**先读相关文件**，改完汇报**改了哪些文件、什么位置、为什么**
4. 遇到架构冲突或多种做法，**先问再写**
5. 用户说"不写代码"时**绝对不碰代码**
6. **源码就在仓库里**，任何文件都可用 read 工具直接读，不要向用户索要代码

## 快速上手建议（推荐阅读顺序）

1. `Project.Launch.csproj` — 搞清资源注册规则（改 XAML 必读）
2. `App.axaml.cs` → `LauncherPaths.cs` — 全局状态与路径
3. `src/Views/MainWindow.axaml` + `MainWindow.cs` — 主窗结构、Tab 体系、标题栏两态
4. `src/Views/LoginWindow.xaml.cs` — 登录流程与滑块动画范式（**动画代码可复用到其他选择器**）
5. `src/Tools/GameLauncher.cs` — 启动链路全貌
6. `src/Tools/Auth/MicrosoftAuthFlow.cs` — 正版登录编排

