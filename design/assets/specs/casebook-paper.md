# Asset Spec: 脉案纸页（线装书）

> **Tier**: Vertical Slice Critical
> **Category**: Prop / UI Surface
> **Source**: hud.md §1, casebook-39.md, case-system.md
> **Art Bible Ref**: §7.2 纸面元件库（宣纸纤维 / 墨迹 / 印章）; §1 P3「纸面正文对比承诺 ≥7:1」
> **ADR Ref**: ADR-013 §三（UI Toolkit 主栈）; ADR-013 §十（ModalId.Casebook 闭集第一员）

---

## Visual Description

**脉案**是诊断态的核心纸面承载。形态为**线装书展开**——左页「四诊摘要」，右页「辨证记录」。

- **纸张**: 宣纸质感，微黄底色，纤维纹理可见。纸面有轻微泛黄（非新纸），边角有磨损痕迹。
- **墨迹**: AI 生成含完整版式（楷书四诊 / 行书辨证 / 界行 / 印章 / 落款）的材质层 → DA 按材质语义裁切为独立纹理层；动态文字内容由 UXML `<Label>` 叠层承载，USS 控制字体 / 字号 / 行距。字号差异走 USS 变量，不产出字号系列材质。
- **版式**: 线装书页，竖排从右至左。页边有红色竖线（界行）。书脊处可见线装绳结。
- **状态痕迹**: 空白行 = 未填写（非空格占位，是「空行即答案」的纪律）；已填写行有墨迹；错误/改写处有轻微涂改痕迹（非删除线）。
- **翻页动画**: 书页展开 0.3s ease-out，收起 0.2s ease-in。

> **版式归属**：本 spec 只描述**材质与状态**。版式（通道行数 / 行序 / 行高 / 分区）
> 的唯一权威是 `design/ux/casebook-39.md` §5 —— 两者形状一致但**权责分离**，
> 版式改动不触发本 spec 的贴图重出。

---

## Technical Specs

| Property | Value | Notes |
|----------|-------|-------|
| **Resolution** | 2048×2048 | ⚠️ **「page spread / 两页并排 / 左四诊 + 右辨证」是 2026-09-29 四层拆分前的旧口径,已作废** —— 本表现在只是**四层里的第一层**:一张**无缝、可平铺、可 9-slice 拉伸的素纸背板**。**不含界行、不含绳结、不含印章**,也**不承载任何版式**(版式权威 = `casebook-39.md` §5) |
| **Resolution tier** | ⚠️ **偏离 `art-bible §8.2` 的「UI-纸 1K」提案档**(2026-10-05 用户裁定 = 2048² 原样入库,**不**降采样到 1024²) | 降采样会把纤维 std **7.59 → 4.73(−38%)**,等于削掉本轮的修正成果。§8.2 该档自陈「提案值 / 不定绝对像素上限」且 `Memory Ceiling` 仍待定 ⇒ **不构成硬冲突**。⚠️ 连带义务:story-019 建 atlas 时须复核(「atlas ≤2K 短边」对着一张 2048² 素纸,是**切片进 atlas** 还是**作独立大图**须裁) |
| **Format** | PNG (sRGB) | 纸面底 + 界行 + 绳结 + 墨迹分层 |
| **Material slots** | 4 (paper_base 9-slice + ruling repeat + stitch + ink_overlay) | 界行与绳结各自独立，版式改动不需重出纸底（见「为什么要拆成四层」） |
| **Poly count** | N/A (UI element) | UI Toolkit UXML + USS |
| **LOD** | 不适用 | UI 始终全分辨率 |
| **Platform variants** | 无 | 全平台同一套纸面 |

---

## Asset Breakdown

| Asset | Type | Description | Slicing | Priority |
|-------|------|-------------|---------|----------|
| `casebook_paper_base.png` | Texture | **纯纸面**：宣纸纤维 / 泛黄 / 霉斑 / 水渍 / 卷边磨损。**不含界行、不含绳结、不含印章** | 9-slice | P0 ✅ **已入库 2026-10-05**(v9E-b · `casebook_paper_base-final.png` 2048²) |
| `casebook_paper_ruling.png` | Texture | 红色界行（竖线）**单行可 repeat 条**，不含任何行标签文字 | 横向 repeat | P0 |
| `casebook_paper_stitch.png` | Texture | 中缝线装绳结（一条窄图，含线环 + 结） | 9-slice | P0 |
| `casebook_ink_font.png` | Texture Atlas | 墨迹材质层（楷书四诊 / 行书辨证 / 界行 / 印章 / 落款，AI 生成含完整版式的材质参考图 → DA 裁切为独立纹理层；动态文字内容由 UXML `<Label>` 叠层承载） | — | P0 |
| `casebook_stamp.png` | Texture | 印章样式（已识模式标记 / 鉴别诊断标记） | — | P0 |
| `casebook_wear.png` | Texture | 边角磨损 / 折痕细节层 | 9-slice | P1a |
| `casebook_cover.png` | Texture | 线装书封面（深褐漆面，书名烫金） | — | P1a |

### 为什么要拆成四层

> **2026-09-29 裁定**：材质与版式**解耦**。

原先 `casebook_paper_base.png` 把「宣纸底 + 界行 + 线装绳结」焊在一张图里，导致底图的竖线必须与
UXML 的五行通道行**逐像素对齐**——改一次行高或行序，整张图作废。这是把**代码资产**（版式）
与**美术资产**（材质）绑在一起的典型失败面。

拆开后：

| 改什么 | 动哪里 | 贴图重出？ |
|--------|--------|-----------|
| 通道行数 / 行序 / 行高 | UXML + USS 变量 | **否**（界行 repeat 次数自动跟随） |
| 页面比例 | USS 尺寸 | **否**（底图 9-slice 拉伸） |
| 印章位置 | USS anchor | **否** |
| 焦点高亮粗细 | USS 变量 | **否** |
| 纸的旧度 / 质感 | `casebook_paper_base.png` | **是**（仅此一种） |

**版式权威 = `casebook-39.md` §5**（五行通道区 + 右页判断区 + 网格要点「行高恒定」）。
**贴图只提供材质**，AI 生成含完整版式的参考图 → DA 按材质语义裁切 → UXML `<Label>` 叠字。
版式改动不触发重出图（文字内容由 UXML 承载，材质层只提供墨迹质感 / 印章 / 界行等静态视觉）。

**行高恒定的实现约束**：界行是**均分 repeat**的，不是「按内容自适应」。
未查的空行与已查的行视觉等重（`casebook-39.md` §5.4 网格要点）——空行若缩小/置底
= 变相进度指示，直接违反支柱「绝不①」。

---

## Interaction States

| State | Visual |
|-------|--------|
| **Closed** | 书合拢，仅书脊可见 |
| **Opening** | 书页展开 0.3s ease-out，墨迹渐显 |
| **Open — Default** | 左页四诊摘要（空白行 = 未查），右页辨证记录（空白） |
| **Open — Filling** | 四诊读数落笔，墨迹逐通道填入对应行 |
| **Open — Pattern Found** | 已识模式以印章盖入（红色印泥，非数字） |
| **Open — Confidence Low** | 病名栏用「?」或留空（非文字说明，承 V-8.3） |
| **Closing** | 合书 0.2s ease-in |

---

## Color Palette (from Art Bible)

| Element | Color | Role |
|---------|-------|------|
| 纸面底 | `#F5F0E8` (warm rice paper) | 主底 |
| 墨迹浓 | `#1A1714` (near-black ink) | 已填写内容 |
| 墨迹淡 | `#4A4640` (faded ink) | 辅助文字 / 界行 |
| 界行红 | `#8B3A3A` (seal red) | 竖线界行 |
| 印章红 | `#C13A3A` (vermilion) | 模式标记 |
| 纸面泛黄 | `#E8DFC8` (aged paper) | 边缘 / 旧纸感 |

---

## Accessibility

- 纸面正文对比度 ≥ 7:1（AB-4 已裁定）
  > ⚠️ **2026-10-05 用户裁定:该门只对「正文 / 墨迹淡 `#4A4640`」生效,且按 p50 中位量**
  > (原条款字面是「最亮像素」,重心在「别让纸比字更亮」;**不是**把 07 整张纸当判据 ——
  > 07 是**纹理**,不是被读的文本面)。**浓墨 `#1A1714` 与界行红 `#8B3A3A` 不在门内**,
  > 单列目视判断。入库定稿 v9E-b 实测:**淡墨 p50 = 7.11:1 / 最亮像素 = 9.02:1**
  > —— **两种读法都过门**(不是靠改判才过的)。
  > **界行红 5.77:1(p50)= 结构性事实,登记不隐藏**:任何暖色纸底上 `#8B3A3A` 都过不了 7:1;
  > 若将来它也要过门,该改的是**界行红自己的色值或加描边**,**不是**这张纸。
- 墨迹浓 ≠ 仅颜色区分——有笔触粗细 / 位置 / 形态差异
- 焦点高亮 = 黄铜边框 2px（承 art-bible §3.3 / §7.4 + ADR-013）

---

## AI Generation Prompt (for reference)

> ⚠️ **产出定位 = 材质板，不是版式图**（2026-09-29 裁定）。
> 版式权威是 `casebook-39.md` §5，本节 prompt 只负责**纸的质感 / 旧度 / 光影 / 调性**。
> 生成图中的行数、行高、印章位置**一律不作为规格**；版式一改不需要重出图。

```
A traditional Chinese medical casebook (脉案), open spread view.
Style: Late Qing / early Republican era (清末民初), circa 1910s.
Material focus: Rice paper (宣纸) with warm yellowish tone, visible fiber texture,
slight aging discoloration, foxing spots, a water stain, darkened curled corners,
a gently cockled surface.
Binding: thread-bound spine (线装) at the centre gutter — loose folded paper sewn
with off-white cotton thread, visible thread loops and knots. No hardcover.
Lighting: Warm ambient, no harsh shadows, flat lay view.
Mood: Scholarly, meticulous, historical authenticity.
Constraints: NO HP bars, NO numbers, NO modern UI elements. Pure paper and ink aesthetic.
Resolution: 2048x2048, high detail for close-up reading.
```

**该 prompt 刻意不写的内容**（避免模型回填不可控版式，见 image-gen skill 的「空行即答案」条目）：
行标签 / 通道名 / 病名文字 / 手写正文。**产出定位 = 材质板，不是版式图** —— AI 的文字密度和版式细节**不作为规格依赖**，只要求材质语义清晰可裁切。

### Generation Record(§8.10.2 · 独创性留痕)

> **本条记录的是「纸面底材质」这一项**,对应 `casebook_paper_base.png`。
> 同批生成的另外 15 张贴图(界行 / 绳结 / 墨迹 / 印章 / 卷轴配件 / 图标集 / 九宫格边框)
> 的生成留痕见 `design/assets/ai-generation-prompts.md` §九。

| 字段 | 值 |
|---|---|
| `prompt` | 见上方代码块(prompt 本体)。**实际出图用的是改写版**——原 prompt 生成的是完整脉案版式图,而本 spec §「为什么要拆成四层」已裁定材质与版式解耦,故改用单材质 macro prompt(见 `ai-generation-prompts.md` §一)。原 prompt 保留在此作为**风格基调**的权威表述。 |
| `model` | `sensenova-u1.5-fast`(SenseNova U1.5 Fast)。端点 `POST {base}/images/generations`,t2i 与 i2i 同端点,i2i 以 base64 data URI 传源图。 |
| `iterations` | **4 轮**(2026-09-30)。① 原 prompt 版式图 → 作废(材质与版式解耦裁定后不再需要版式参考)。② 换 "Extreme macro close-up" 单材质模板,std 3.0→7.3。③ 微调至 `#EDE6D9`(目标 `#F5F0E8`)、接缝比 1.14 —— 当时定稿。④ 追加"更暖更深"指令 → 暗褐 `#7A624A`,回归。 |
| `iterations`(⑤ 2026-10-05 重出轮) | **9 版**(v7 → v9B → v9C → v9D → v9E-a…e,**定稿 v9E-b**)。根因:③ 的"定稿"落在**冷白调**上(纸面偏灰白、无纸感),而④ 的"更暖"又**只留纤维、把霉斑/水渍的低频起伏整个抹平**(云斑 std 1.155 → v9D 的 1.104 之前几近于无)。轮内:a) 频域拆解(`d = v9D − #F6DEBC` → 纤维 `hi` + 云斑 `lo`,再把两者**独立**缩放)把纤维 std 拉回 **7.59**、云斑 std 保住 **1.104**;b) 高光通道过 `tanh` 软滚降(W=250/A=6)消掉高光过曝(clip **0.000%**);c) 用户裁**两项口径**后重测 —— 7:1 门只管正文、按 p50 量 —— **淡墨 p50 7.11:1 / 最亮 9.02:1**。⚠️ **本轮无新 API 调用**:全部由 v9D 经 PIL 数值重建产生(不可复现性**不因此恶化**,该图本就无 seed)。 |
| `seed` | **无** —— SenseNova `images/generations` 端点不回传 seed。后果:该贴图**不可复现**,重出必得不同结果。若需可复现,须换回传 seed 的模型并在资产上另记 seed 值。 |
| `human_edits` | `none` —— 全部经 API 生成与 i2i 编辑,无人工描图 / 临摹 / 矢量化(art-bible §7.2)。 |

> **§8.10.2 落地**:本表是发行时 Steam AI 申报清单的数据源。
> **产出入库状态(2026-10-05 改判 · 取代 2026-09-30 口径)**:`casebook_paper_base`
> 的定稿位图 = **v9E-b**,已入库至
> `unity/Assets/Gameplay.UI/Skeuomorphic/Textures/Casebook/casebook_paper_base-final.png`(2048² · 7.3 MB)。
> ⚠️ **2026-10-05 移入 `Textures/Casebook/` 子目录** —— 它**不属于 story-019 的五族元件库**,
> 走本 spec 这条独立链;门与夹具只扫 `Textures/` 顶层,故移入子目录即脱离其扫描面。
> (原置于根目录时被五族夹具误扫,实测致 3 条夹具红。)
> **此时它只是素材图,尚不是运行期资产** —— 它尚无 slice 值、无 USS 绑定、未进任何
> `.spriteatlas`。⚠️ **它的切图边界同样禁手填**(承 `AC-42-C8` 同款纪律:值须来自冻结件元数据),
> 但**不由 story-019 承接** —— story-019 的面是五族元件库(16 张),
> 本件是脉案专用纸,其 slice 冻结须**另立承接方**(待裁)。
>
> 其余 15 张贴图仍走 2026-09-30 原口径(定稿版入库 + 迭代版 gitignored)。
> **迭代版(`-v1`~`-v6` 及其后的 `-v7`/`-v9*` 系列)依然 gitignored** 留在
> `assets/design-references/`,理由原样成立(无法重建、每轮规格不同、入库制造假版本权威)。
> ⚠️ 2026-10-05 已按用户裁定**删除 casebook 侧 v1–v8 与 v9B/C/D/E-a/c/d/e**,
> **唯留定稿 v9E-b**(目录里已无其他 `casebook_*` 版本 = 库位文件的出处,不是 orphan)。
> 规格真源 = 本 spec;生成留痕 = 本表 + `ai-generation-prompts.md` §九。
>
> ⚠️ **`seed` 缺失是申报风险项**:Steam 要求披露 AI 生成内容,本表已满足;
> 但"可复现性"在多数平台的 AI 内容条款下并非强制项。若后续出现需要 seed 的场景
> (如法务举证 / 争议溯源),须换模型重出并补记。

---

## Open Questions

- [x] ~~字体是否引入商用字库（楷体 / 行书）还是手写扫描？？~~ → **2026-09-29 裁定：P0 = 手写扫描素材,非商用字体**。
- [x] ~~印章图案是否随病种变化（每种疾病有独特印章）？？~~ → **2026-09-29 裁定：印章固定不随病种**。
- [x] ~~方笺页（续页）是否复用同一纸面底 + 不同墨迹层？？~~ → **2026-09-29 裁定：续页复用同一纸底**。
---