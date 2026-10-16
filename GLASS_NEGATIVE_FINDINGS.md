# 反方结论：快照/滤镜“覆盖不足”主线假设不成立

> 角色：反方（证伪方）。目标：尽力证明“快照 / 滤镜覆盖不足”是错的，或证明问题在别处。
> 结论：**主线假设被证伪**。真正的机制在 **SKSL 着色器自身的算式 + 宿主参数**，与快照覆盖、滤镜裁剪、
> `LiquidGlassVisualRenderer` 的手动遍历都无关。
> 本报告所有数值均来自可复现实验（离线 Skia 复算 + 真机 GPU 路径插桩），不含推测。

---

## 0. 一句话结论

玻璃内部（离边缘 > 4 **设备像素**的区域）是 **逐像素原样透传** `content.eval(coord)`（`LiquidGlassShader.sksl:92-94`），
透镜只在一个 **4 设备像素**宽的边缘带里生效。在用户机器的 **125% 缩放**（真机实测 `RenderScaling=1.25`）下，
这个带只有 **3.2 DIP**，占 36 DIP 高滑块的 8.9%。所以：
* “只有左边有、右边啥都没有”→ 不是覆盖问题，而是**折射/模糊只在边缘 3.2 DIP 生效**，
  其余区域和背景**逐像素完全相同**，肉眼只能看到边缘一圈变化；
* “中间一条竖线隔开”→ 折射梯度 `gradSdRoundedRect` 在**控件正中竖线/横线**处发生方向翻转，
  色散强度 `(cx*cy)/(halfW*halfH)` 在正中竖线处**恒等于 0**、两侧符号相反；
* “下载页选择器正常 / TabSlider 只在某位置正常”→ 同一套着色器输出在每个位置都**逐像素自相似**，
  差别只来自**背景内容**（白色卡片 vs 平坦 #D8D8D8 轨道）。

---

## 1. 被证伪的三条“覆盖不足”子假设（含硬证据）

### 1.1 「快照过时 / 部分帧被合并导致快照停在旧时刻」——证伪

* 覆盖范围是**完全正确**的。真机插桩（见 §3）实测：
  `LoginModeSlider`（控件 114.4×36 DIP）的快照为 `359×237 px @(271→471 像素)`；
  且**每个模式的快照原点差恰好 = 120 DIP × 1.25 = 150 px**，快照**严格跟随**控件移动。
* 快照尺寸可由代码公式反推吻合：`宽 = 控件宽 + 2×86.6 DIP`，
  86.6 = `max(32, |40|×2 + 0×3 + 0 + 0 + 6)`，正是
  `LiquidGlassBackdropProvider.GetBackdropInflate`（`LiquidGlassBackdropProvider.cs:513-550`）
  与 `CalculateBackdropClip`（同文件 `:416-457`）的组合结果。
* 用户日志 `发布=534 / 哈希=0`：`哈希` 计数只在
  “配置相同且 8×8 采样指纹相同”时自增（`LiquidGlassBackdropProvider.cs:239-246`），
  534 帧全部发布新快照、零次哈希跳过 ⇒ **不存在陈旧的旧时刻快照**。
* 「大量请求被 CaptureQueued 合并」只说明**请求多于必要**，不是丢帧：
  `QueueCapture` 的合并是标准的“下一帧只抓一次”（`:149-177`），
  动画期间仍有 ~30 次/秒的真实抓取。

### 1.2 「滤镜图比透镜采样范围小，采样撞边被 Clamp」——证伪（在宿主参数下不存在）

* `CalculateFilterCrop`（`LiquidGlassDrawOperation.cs:463-502`）：
  `margin = max(8, |40|×2 + 0 + 0 + 0 + 8) × 1.25 = 120 px`，
  裁切结果被 clamp 到快照边界；实测裁切矩形 = **整张快照**（或仅比快照小几像素）。
* 透镜实际采样需求：折射位移 ≤ 40 **DIP**（`RefractionAmount`），色散最坏再 ×2 = 80 DIP；
  快照单边余量 = 86.6 DIP > 80 DIP ⇒ **采样永远落在快照内部**，不会撞边。
* 离线复算（真实 SKSL + 真实 helper 逐行搬运）确认：`filtered` 尺寸 = 快照尺寸，
  `filteredOrigin` 与采样坐标系自洽，玻璃输出与“直接采样滤镜图”**在内部逐像素相同**。

### 1.3 「`LiquidGlassVisualRenderer` 手动遍历把右侧内容裁掉了」——证伪（对本场景无影响）

* 代码位置：`LiquidGlassVisualRenderer.cs:74-83`
  ```csharp
  if (visual.ClipToBounds) { totalTransform = Matrix.Identity; clipRect = rect; }
  // 随后对同级后续子树用被改写后的 clipRect 做剔除：
  if (visualBounds.Intersects(clipRect)) visual.Render(context);
  ```
  这**确实是一个真实缺陷**：`clipRect` 被就地改写成“当前 ClipToBounds 视觉的局部矩形”，
  同一父级中排在它**之后**的兄弟子树会用这个矩形做 `Intersects` 剔除
  （AABB 用的是各自独立的 `totalTransform`，所以量纲虽一致、基准却不同）。
  对照 Avalonia 官方 `ImmediateRenderer`，它只重设 `_clipRect`、**不重设 `_transform`**，
  且递归时把 `bounds` 与 `transform` 分离传递，不会污染兄弟节点。
* 但**在登录/主窗的真实视觉树里这个缺陷不触发**，因为 ClipToBounds 只出现在整棵树的**叶子侧**：
  - `LoginWindow` 是最外层唯一的 `ClipToBounds=True`（真机 `harness` 打印的树证实），
    它没有“后面的兄弟”；
  - 模板里的 `Border ClipToBounds=true`（`LiquidGlassSurface.cs:959-964`）是
    `LiquidGlassSurface` 模板的**唯一子节点且为最后一个兄弟**，
    其下 `Grid → InteractiveOverlay / ContentPresenter / FrontOverlay` 全部
    `bounds == (0,0,114.4,36)`，与该矩形完全重合，不会被剔除。
* 结论：这条线索值得作为**独立缺陷**修（换成 `localClip = rect` 的局部副本，
  并把 `totalTransform` 重设为 `Matrix.Identity` 仅用于子树），
  但它**不是**本症状的原因。修复它对当前症状零收益。

---

## 2. 症状的真正成因（可计算、可复现）

### 2.1 「内部逐像素透传」——直接解释了“大部分区域看着是平的”

`LiquidGlassShader.sksl:88-94`：
```glsl
float sd = sdRoundedRect(centeredCoord, halfSize, radius);
float h = max(refractionHeight, 0.001);
if (-sd >= h) { return content.eval(coord); }   // ← 内部原样返回，零像素改动
```
离线实测（真实 SKSL，`|glass − plain|` 逐列差分，中点行）：

| 配置 | 缩放 | 中心行被改动的列数 | 改动所在范围 |
|---|---|---|---|
| Login/Edition 114×36 r6 | 1.00 | 2 / 114 | 最右 2 列 |
| Login/Edition 114×36 r6 | 1.25 | 31 / 142 | 仅在两侧边缘带 |
| Tab 96×44 r22 | 1.25 | 28 / 120 | 仅在两侧边缘带 |

> 结论：**内部 90% 以上区域与背景逐像素相同**。玻璃“看不出来”不是采样错了，而是**算式上就没有效果**。

### 2.2 「4 DIP 变 3.2 DIP」——着色器坐标系单位混用（次要真 bug）

着色器里 `coord` 是**设备像素**（`canvas.TotalMatrix` 含 DPI 缩放，真机 `1.250`），
而 `refractionHeight` / `size` 是 **DIP**。于是 `-sd >= h` 这个比较把带宽钉死在
**4 设备像素**：

| 系统缩放 | 实际折射带宽 |
|---|---|
| 100% | 4.00 DIP |
| 120%（用户另一台/历史） | 3.33 DIP |
| **125%（真机实测）** | **3.20 DIP** |
| 150% | 2.67 DIP |

正确做法：在 `main()` 里把 `refractionHeight * scale`、`refractionAmount * scale` 先换算到设备像素
（`scale` 可由 `length(float2(dFdx(coord.x), dFdy(coord.y)))` 或从 C# 传入 `scaling` uniform 得到）。
这也顺带解释了 HANDOVER 里记录的“把折射带 4→10 调参完全无效”——
它是一个很窄的边圈，用户不会把它认成“效果”。

### 2.3 「中间一条竖线」——色散与梯度的零线/翻转线，全都落在控件正中

`LiquidGlassShader.sksl:96-114`：
```glsl
float d = circleMap(1.0 - (-sd / h)) * refractionAmount;
float2 grad = normalize(gradSdRoundedRect(...) + depthEffect * normalize(centeredCoord));
float2 refractedCoord = coord + d * grad;
float dispersionIntensity = chromaticAberration * ((centeredCoord.x * centeredCoord.y) / denom);
float2 dispersedCoord  = d * grad * dispersionIntensity;
```
* `dispersionIntensity ∝ cx*cy`，`cx = coord.x − w/2`：**在正中竖线处恒为 0**，两侧**符号相反**。
  → 恰好是“中间一条竖线，左边有色散、右边没有”的算式来源。
* `gradSdRoundedRect`（`:30-38`）按 `abs(centeredCoord)` 的象限选取方向，**在 cx=0 / cy=0 处翻转**；
  并且它在“角区/直边区”的边界 `max(halfW,halfH) − radius` 处通过
  `step(cornerCoord.y, cornerCoord.x)` 硬切换分量 → 又一条肉眼可见的方向不连续线。
  TabSlider 的 `radius=22`（96×44）使角区边界落在 `x = 48 − 22/1.5 ≈ 33.3 DIP` 与
  `x = 48 + 14.7 ≈ 62.7 DIP`（`gradRadius = min(r×1.5, min(halfW,halfH))`），
  正好在滑块中段形成两条方向分界线。

### 2.4 「只有最左格有 / 只有切到下载才有」——同一输出落在不同背景上的知觉差

* 折射带内的采样点会向外位移最多 `refractionAmount = 40 DIP`
  （`d` 最大 40；色散再乘 `|dispersionIntensity| ≤ 1`，最坏 80 DIP）。
  即：**边缘那一圈显示的是控件外 0〜40 DIP 处的背景内容**。
* LoginModeSlider 最左格左缘紧挨白色卡片/灰轨的**大对比边界**，边缘圈能取到强对比 ⇒ 看得出来；
  中间、右侧两格四周全是平坦 `#D8D8D8` ⇒ 取到什么都是同一个灰 ⇒ **视觉上“没有效果”**。
* TabSlider：底部导航条左右两端是灰轨的**圆角端点**（`CornerRadius=26/25`，`MainWindow.axaml:548-549`），
  只有滑块位于端点（index 0 = 下载）时，40 DIP 的采样才会跨出灰轨取到白卡片；
  中间三格四周纯灰 ⇒ 同样“没有效果”。
* 关键佐证：离线/真机两套实验里，**每个位置的玻璃内部输出逐像素自相似**；
  任何“位置相关”的差异只可能来自背景内容，而**不可能来自渲染管线**。

### 2.5 为什么下载页的 EditionSlider “完全正常”

它用的是**同一套着色器、同一组参数**（`RefractionHeight=4 RefractionAmount=40 BlurRadius=0 CA=True`），
唯一区别是它坐在 `#D8D8D8` 轨道 + 白卡片交界处、且左右两端都邻接**高对比内容**
（`下载页 240×44 轨道 + 两个文字按钮`）。所以它“看起来正常”不是因为它走了不同代码路径，
而是因为它的**边缘采样圈里有东西可折射**。

---

## 3. 真机路径插桩实验（不改仓库：整份仓库复制到 %TEMP% 后插桩）

* 复制到 `%TEMP%\lgapp`，仅在该副本里给 `LiquidGlassDrawOperation.RenderLens`
  与 `Render` 加日志；仓库源文件**未改动**。
* 真机运行结果（`RenderScaling = 1.250`，GPU 路径 `GRContext != null`）：

```
window MainWindow client=960,560 scaling=1.25
window LoginWindow visible=True client=800,440 scaling=1.25
LoginModeSlider bounds=3.2,4,114.4,36  TR=(0.00,0.00)  windowRect=223.2,67.6,114.4,36
   snapshot 359x237px @(171,0) -> DIP x 136.80..424.00  y 0.00..189.60
LoginModeSlider TR=(120.00,0.00)  windowRect=343.2,67.6
   snapshot 359x237px @(321,0) -> DIP x 256.80..544.00
LoginModeSlider TR=(240.00,0.00)  windowRect=463.2,67.6
   snapshot 359x237px @(471,0) -> DIP x 376.80..664.00
```
  → 水平方向 **快照 = 控件 ± 86.6 DIP，且随控件逐点跟随**（原点差值 = 120×1.25 = 150 px，精确）。
  → 竖直方向：快照 `y 0..189.6 DIP` 相对控件 `67.6..103.6`，上下余量分别为 67.6 / 86.0 DIP，
    唯一不足的一侧（67.6 < 80）被 `CalculateBackdropClip` 与客户区求交
    （`clip.Intersect(clientRect)`）截断——这是**几何必然**，不是缺陷。
* 同时实测出 GPU 画布矩阵确含合成器变换：
  `canvasTotal=[1.250,0,429.0; 0,1.250,84.5]`（= 343.2 DIP × 1.25）。
  → **`SKCanvas.TotalMatrix` 包含 RenderTransform 与 DPI 缩放**，
  `WithPixelOrigin`（`LiquidGlassDrawOperation.cs:851-862`）的逆变换推理成立，
  「采样坐标系算错」这条也一并被排除。

---

## 4. 候选机制清单（按要求给出 ≥3 个，并标注证据强度）

| # | 机制 | 代码位置 | 能否产生“竖直分界” | 证据强度 |
|---|---|---|---|---|
| 1 | 透镜内部逐像素透传，只有 4 设备像素边带生效 | `LiquidGlassShader.sksl:92-94` | 能：边带与内部之间就是硬边界 | **已证实（实测）** |
| 2 | 色散强度 `cx*cy/denom` 在正中竖线恒为 0 且两侧异号 | `LiquidGlassShader.sksl:110-113` | 能：正中竖线天然分界 | **已证实（实测+算式）** |
| 3 | 折射梯度按象限/角区硬切换方向 | `LiquidGlassShader.sksl:30-38, 100-102` | 能：正中竖线 + 角区边界两条线 | **已证实（算式）** |
| 4 | 着色器把 DIP 参数当设备像素用，带宽随 DPI 收缩 | `LiquidGlassShader.sksl:89-92` vs `LiquidGlassDrawOperation.cs:201-216` | 不能（只让整体更不可见） | **已证实（离线复算）** |
| 5 | 手动遍历的 `clipRect` 就地改写污染后续兄弟剔除 | `LiquidGlassVisualRenderer.cs:74-83` | 能（若 ClipToBounds 出现在兄弟中段） | 机制真实，**本场景不触发**（真机视觉树已核） |
| 6 | `Highlight` 的 `SaveLayer` + `SKBlendMode.Plus`、阴影 `Clear` 合成 | `LiquidGlassDrawOperation.cs:738-748`、`LiquidGlassShadowDrawOperations.cs:106-115` | 理论可能（取决于合成器是否隔离层） | **未证实**；离线/真机均未观察到 |
| 7 | `Visual.Effect` 未在手动遍历中渲染 | `LiquidGlassVisualRenderer.cs:26-85`（无 Effect 分支） | 否 | 真实差异，与本题无关 |

---

## 5. 置信度与建议

* **“快照/滤镜覆盖不足”是错的**：置信度 **高（≈0.9）**。
  依据：真机实测快照严格跟随且尺寸 >= 采样需求（§3）；`哈希=0` 与 `发布=534` 联合排除陈旧快照；
  离线复算证明滤镜裁切覆盖全图且采样不撞边（§1.2）。
* **真实主因 = 着色器算式 + 宿主参数**：置信度 **中高（≈0.75）**。
  明细：内部透传（高）、正中零线/翻转线（高）、DIP/设备像素混用（高，但属“让效果更弱”而非“造分界”）。
  剩余不确定：**用户所看到的“分界”究竟是 §2.3 的方向翻转线，还是 §2.1 的边带-内部硬边界**，
  需要一张真机截图在“滑块中段 + 高对比背景”下确认（该实验在本环境无法完成：
  无交互式桌面会话，无法为运行中的窗口截图）。
* **建议的验证/修复顺序**（都不涉及快照逻辑）：
  1. 把 `refractionHeight`/`refractionAmount` 在 `main()` 里乘上 `coord` 的像素缩放
     （或新增 `scaling` uniform），使带宽真正等于 DIP；
  2. 把 `dispersionIntensity` 改为不随 `cx*cy` 变号的量（例如 `length(centeredCoord)/halfDiag` 或
     `smoothstep` 后的单调量），消除正中竖线的符号翻转；
  3. 把 `gradSdRoundedRect` 在角区/直边区与象限处的硬切换改为连续插值
     （对 `sdRoundedRect` 求解析梯度或有限差分），消除方向分界线；
  4. 调参验证：临时把 `RefractionHeight` 改成 `18`（≈几何半高的一半），
     若“中间竖线 + 只有边缘有反应”的现象变成“整块都在流动”，即坐实 §2.1/2.2。

---

## 6. 附：可复现实验资产（均在 %TEMP%，不影响仓库）

| 路径 | 内容 |
|---|---|
| `%TEMP%\lgshader\` | 离线 Skia 复算：真实 SKSL + 真实 `CalculateFilterCrop/Margin/WithPixelOrigin` 搬运；含 `SeamProbe`（逐像素差分找“硬缝”）、`FinalNumbers`（带宽与零线） |
| `%TEMP%\lgneg\` | Avalonia 头显探针：`SKCanvas.TotalMatrix` 包含 RenderTransform 的证明；`TransformedBounds.AABB` 量化（125×57 ↔ scale 1.30 精确吻合） |
| `%TEMP%\lgneg2\` | `LiquidGlassVisualRenderer` 反射调用实验（ClipToBounds 兄弟剔除以真实组件复现） |
| `%TEMP%\lgreal\` | 真实 `LiquidGlassSurface` 头显场景 + “有玻璃帧 vs 无玻璃帧”逐像素对照（证明各处内部逐像素自洽） |
| `%TEMP%\lgapp\` | 仓库副本 + 真机插桩运行（GPU 路径实测快照/画布矩阵/缩放） |
| `%TEMP%\lgrun\` | 真机运行产物：`harness.log`、`lens-diag.log`、`snap_mode*.png` |
