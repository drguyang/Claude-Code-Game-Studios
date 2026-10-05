# AI 文生图 Prompt 清单 — 系统 42 拟物 UI 资产

> **用途**：批量生成拟物 UI 所需的贴图/材质资产
> **生成工具**：任意 AI 文生图（Midjourney / Stable Diffusion / DALL-E / SenseNova 等）
> **输出规格**：PNG，512×512 或 1024×1024，透明背景（如支持）

---

## 一、纸纹贴图（4 张）

### 1. 新宣纸
```
A seamless texture of fresh Chinese rice paper (宣纸), white with subtle fiber texture, slight translucency, traditional calligraphy paper, flat lay view, even lighting, no shadows, high detail, 4K
```

### 2. 旧宣纸
```
A seamless texture of aged Chinese rice paper, yellowish-brown tone, visible fiber texture, slight foxing spots and discoloration, antique calligraphy paper, flat lay view, even lighting, high detail, 4K
```

### 3. 焦边纸
```
A seamless texture of burnt-edge Chinese paper, dark brown to black gradient edges, charred paper texture, antique scroll effect, flat lay view, even lighting, high detail, 4K
```

### 4. 粗麻纸
```
A seamless texture of rough hemp paper, beige tone, coarse fiber texture, visible straw particles, traditional Chinese bookbinding paper, flat lay view, even lighting, high detail, 4K
```

---

## 二、墨迹贴图（4 张）

### 5. 湿墨晕染
```
A single Chinese ink wash stroke, wet ink bleeding effect, black ink on white paper, soft edges, water diffusion, traditional calligraphy, isolated on transparent background, high detail, 4K
```

### 6. 干墨飞白
```
A single Chinese ink brush stroke, dry brush effect, black ink with white streaks, flying white (飞白) texture, traditional calligraphy, isolated on transparent background, high detail, 4K
```

### 7. 淡墨渲染
```
A soft Chinese ink wash gradient, light gray to white, subtle ink diffusion, watercolor effect, traditional painting style, isolated on transparent background, high detail, 4K
```

### 8. 浓墨点
```
A single dense Chinese ink dot, solid black, slight edge bleeding, traditional calligraphy seal effect, isolated on transparent background, high detail, 4K
```

---

## 三、印章贴图（2 张）

### 9. 印泥
```
A square Chinese seal stamp impression, red ink paste (印泥), traditional seal script characters, slightly uneven ink distribution, isolated on white background, high detail, 4K
```

### 10. 印面
```
A square Chinese seal stone surface, carved seal script characters, red ink residue in grooves, traditional chop stamp, top-down view, even lighting, high detail, 4K
```

---

## 四、卷轴配件（3 张）

### 11. 木轴
```
A Chinese scroll wooden rod, dark brown wood, smooth polished surface, traditional scroll handle, horizontal orientation, isolated on transparent background, high detail, 4K
```

### 12. 绳结
```
A Chinese knot cord, red silk rope, traditional decorative knot, scroll binding cord, isolated on transparent background, high detail, 4K
```

### 13. 轴头
```
A Chinese scroll end cap, brass or jade material, ornate carved decoration, traditional scroll fitting, isolated on transparent background, high detail, 4K
```

---

## 五、图标雪碧图（1 张）

### 14. UI 图标集
```
A sprite sheet of 16 traditional Chinese medicine UI icons, 4x4 grid, each icon 128x128 pixels, ink brush style, black on white, icons include: pulse diagnosis, herbal medicine, acupuncture needle, scroll book, seal stamp, mortar and pestle, teapot, yin-yang symbol, dragon, phoenix, turtle, crane, mountain, water, fire, wood, clean vector style, high detail, 4K
```

---

## 六、九宫格边框（2 张）

### 15. 纸面九宫格
```
A 9-slice border frame for Chinese paper UI, thin black ink lines, traditional book page border, corner ornaments, seamless edges, isolated on transparent background, high detail, 4K
```

### 16. 卷轴九宫格
```
A 9-slice border frame for Chinese scroll UI, dark brown wood texture edges, brass corner fittings, traditional scroll border, seamless edges, isolated on transparent background, high detail, 4K
```

---

## 七、生成后检查清单

- [ ] 16 张贴图全部生成
- [ ] 分辨率 ≥ 512×512
- [ ] 背景透明或纯白
- [ ] 无水印、无文字标注
- [ ] 风格统一（水墨/拟物）
- [ ] 文件命名与清单一致

---

## 八、存放路径

```
unity/Assets/Gameplay.UI/Skeuomorphic/Textures/
├── paper_xuan-final.png
├── paper_aged-final.png
├── paper_burnt_edge-final.png
├── paper_hemp-final.png
├── ink_wet-final.png
├── ink_dry-final.png
├── ink_light-final.png
├── ink_dot-final.png
├── seal_red-final.png
├── seal_surface-final.png
├── scroll_rod-final.png
├── scroll_knot-final.png
├── scroll_cap-final.png
├── ui_icons_sprite-final.png
├── border_paper-final.png
├── border_scroll-final.png
├── casebook_paper_base-final.png   ← 2026-10-05 第 17 张(脉案五层拆分第一层,另一套 spec)
└── …
```

> **⚠️ 第 17 张(2026-10-05)**:`casebook_paper_base-final.png` **不是**上面那 16 张的同批产物 ——
> 它属 `design/assets/specs/casebook-paper.md`(脉案纸页五层拆分里的第一层),生成留痕见下 §九之二。
> 树里列它是为了**让目录与库位一一对应**(否则下一个人会以为它是孤儿)。
> **脉案还剩 4 个 P0 层未出**:`casebook_paper_ruling` / `casebook_paper_stitch` /
> `casebook_ink_font` / `casebook_stamp`。

> **`-final` 后缀的由来（2026-09-30）**：`/image-gen` 的迭代产物带 `-v1`/`-v2`… 后缀留在
> `assets/design-references/`（gitignored，不入库）。入库的是每张的定稿版，改名为
> `<资产名>-final.png`——**后缀本身是"这是定稿、不要再迭代"的标记**，去掉会让
> 后续的 `-v7` 与定稿在目录里无法区分。
>
> ⚠️ **2026-10-05 订正:`-final` 不等于"不可改"** —— `border_paper-final.png` 已于
> 2026-10-05 重出(第 4 版,见下 §九 表格第 15 行与 slice 表订正说明)。
> **该后缀的真实语义是"当前入库位是这一份",不是"这份永远不再变"** —— 规格裁变
> (此处 = 用户 2026-10-05 裁甲-B:纸面暖褐 vs 原白框版纯白)时,入库位随之替换,
> 迭代版仍留 `assets/design-references/` 不入库。**改名规则不变**:替换后仍是 `-final`。

---

## 九、生成记录(2026-09-30 定稿 · **2026-10-05 border_paper 第 4 版订正**)

| 项 | 值 |
|---|---|
| **model** | `sensenova-u1.5-fast`（SenseNova U1.5 Fast） |
| **endpoint** | `POST {base_url}/images/generations`（t2i 与 i2i 同端点，i2i 以 base64 data URI 传源图） |
| **size** | 1024×1024（`ui_icons_sprite` 2048×2048，4×4 网格每格 512） |
| **seed** | 未提供 —— SenseNova 该端点不回传 seed，**不可复现**（见下） |
| **human_edits** | `none` —— 全部经 API 生成与 i2i 编辑，无人工描图 / 临摹 / 矢量化（art-bible §7.2） |
| **迭代轮次** | 见下表 |

| # | 资产 | 轮次 | 定稿版 | 关键迭代要点 |
|---|---|---|---|---|
| 1 | `paper_xuan` | 4 | v3 | v1–v2 无纤维感；v3 换 "Extreme macro close-up" 模板后 std 3.0→7.3、接缝比 1.14。v4 改坏（暗褐 `#7A624A`）弃用 |
| 2 | `paper_aged` | 6 | v6 | v2–v3 在"黄"与"白"间来回；v4 去黄成功但过白；v5 过黄；v6 拉回 `#D9D6CC`，接缝比 1.34 |
| 3 | `paper_burnt_edge` | 1 | v1 | 一次命中，接缝比 0.04（全批最低） |
| 4 | `paper_hemp` | 4 | v4 | v2 有 TB=70.98 硬接缝；v3 修接缝但磨平了麻纹（std 4.8）；v4 补回纤维 std 18.0、接缝 0.52 |
| 5 | `ink_wet` | 2 | v2 | v1 笔画太细；v2 要求"墨团占 80% 画面"→ 主体 `#190A07`（目标 `#1A1714`） |
| 6 | `ink_dry` | 1 | v1 | 一次命中 |
| 7 | `ink_light` | 2 | v2 | v2 暗区 `#4E4842`（目标 `#4A4640`），保留 p5=112/p95=210 的软渐变 |
| 8 | `ink_dot` | 2 | v2 | v2 主体 `#030201`，LR 3.73 / TB 4.97 = 孤立墨点无杂边 |
| 9 | `seal_red` | 5 | v5 | **触线资产**。轨迹：v1 太亮（sat 96.9）→ v2 过暗 → v3 亮对纯度未降（94.5）→ v4 降饱和成功但连亮度一起拖暗（81.4）→ v5 亮度正确、纯度 84.5（目标 ~68）。用户目视验收后定稿 |
| 10 | `seal_surface` | 2 | v1 | v2 偏橙（`#E26B2F`）明显更差，退回 v1 `#851C10` |
| 11 | `scroll_rod` | 1 | v1 | 深色抛光木，横贯画面 |
| 12 | `scroll_knot` | 5 | v5 | 轨迹同 seal_red：v1 暗 → v3 降饱和但拖暗 → v4 提亮过头 → v5 `#B9403F` sat 70.9（目标 sat 68）命中 |
| 13 | `scroll_cap` | 1 | v1 | 做旧黄铜 `#67532F` |
| 14 | `ui_icons_sprite` | 1 | v1 | 4×4 网格分隔线精确落 512/1024/1536，16 格覆盖率 4.7%–35.4% |
| 15 | `border_paper` | 4 | **v4** | v1 边线内缩 109px → v2 贴边但暗带后有中性白 rim(R-B +0.3、亮度 0→248 硬跳≈127、近白 88.4%) → v3 修好白 rim + 改暖褐纸(R-B +136)但墨线外缩 11px → v4 墨线落 22–57px、四条边不对称(top 收 57 / left 收 51) |
| 16 | `border_scroll` | 2 | **v2** | v1 木带内缩 69px 且带宽 102px(几乎占满半图) → v2 贴边(最外 68px),暗带落 0–67px,中央 ½ 区纯白 |

### 九之二 · `casebook_paper_base`(2026-10-05 重出轮 —— **已入库,不在上表 16 张内**)

> **为什么单列**:上表 16 张是 2026-09-30 那批**拟物 UI 边框 / 纸纹 / 墨迹 / 卷轴配件 / 印章**,
> 走 `ai-generation-prompts.md §一`~`§六` 的 prompt。`casebook_paper_base` 属
> `design/assets/specs/casebook-paper.md`(脉案纸页五层拆分里的第一层),**另一套 spec**,
> 其生成留痕主表在该 spec 的 §8.10.2。**本节只登记 2026-10-05 那一次重出**,避免两处主表重复。

| 字段 | 值 |
|---|---|
| **定稿版** | **v9E-b**(2026-10-05 · 用户目视验收后定稿) |
| **入库位** | `unity/Assets/Gameplay.UI/Skeuomorphic/Textures/Casebook/casebook_paper_base-final.png`(2048² · 7.3 MB)。⚠️ **2026-10-05 已移入 `Textures/Casebook/` 子目录** —— 它**不进 story-019 的五族贴图扫描面**(门与夹具扫 `Textures/` 顶层 `TopDirectoryOnly`),因为它是脉案专用纸,走 `casebook-paper.md` 那条独立链,不是五族元件库元件。原入库位在 `Textures/` 根目录时会被五族夹具误扫(实测致 3 条夹具红,已修) |
| **新建方式** | **t2i + PIL 频域重建,无新 API 调用** —— 全部由当时库里的 v9D 数值重建(`d = v9D − #F6DEBC` → 纤维 `hi` / 云斑 `lo` 拆开独立缩放 → `tanh` 软滚降消高光过曝) |
| **轮次** | **9 版**(v7 → v9B → v9C → v9D → v9E-a…e)。v7 纤维够但冷白无纸感;v9D 找回暖调却把霉斑/水渍的低频起伏抹平;v9E-b 两者兼顾。**v1–v8 与 v9B/C/D/E-a/c/d/e 已按用户裁定删除,唯留 v9E-b** |
| **实测** | 纤维 std **7.59** · 云斑 std **1.104** · 裁剪 **0.000%** · 冷色 **0.00%** · 接缝 TB/LR **0.94 / 0.77** · 淡墨对比 **p50 7.11:1 / 最亮像素 9.02:1**(两种读法均过 7:1 门) |
| **两项口径裁定(用户 2026-10-05)** | ① 7:1 门**只管正文 / 墨迹淡 `#4A4640`**,浓墨与界行红单列目视;② 对比按 **p50 中位**量,不按 p5 最差 5% 霉斑 |
| **分辨率裁定** | **2048² 原样入库,不降到 1024²** —— 降采样会把纤维 std 削到 4.73(−38%)。⚠️ 偏离 `art-bible §8.2` 的「UI-纸 1K」**提案档**,偏离理由与 story-019 的连带义务已登记在 `casebook-paper.md` 的 Resolution tier 行 |
| **未兑现(禁借绿)** | 无 slice 值(尚未量;**须另立承接方** —— 非 story-019,见入库位行)· 无 USS 绑定(全库零 `casebook_paper_base` 引用)· 未进任何 `.spriteatlas` |

### 九宫格 slice 值(2026-09-30 初版 · **2026-10-05 甲-B 口径订正**)

> **⚠️ 本节 2026-09-30 的原表已整行作废** —— 那张表按「纸面 = 纯白 + 暗带贴画布边」量出
> `SkeuoPaper.uss` = **42px**。**用户 2026-10-05 裁甲-B:border_paper 的纸面是暖褐 aging paper,
> 不是纯白** ⇒ 白框版 v2 删除,42px 的依据不复存在。
>
> 新图 v4 实测:**三道墨线落 22–57px**(top 边最完整;left 收在 51,right/bottom 收在 50),
> 暖纸占 y0–21(R-B +110.3 环带,中性近白 0.00%),中央 ½ 区亮度 133.8 / R-B +140.3 / std 3.5。
>
> **slice 取 64px**(2026-10-05 用户裁定)—— 判据:
> ① 须 ≥ 最宽边(57)才不被切;② 切在纸上不切在墨上(S=58/60 接缝跳变 57.17 / 9.88,是切在墨线上的 mean,非缺陷);
> ③ S=64 接缝跳变 mean **4.00** ≈ 普通邻域梯度 4.61(等于无缝);
> ④ 占边 6.2%,远离卷轴 13% 的笨重告警;⑤ 四角 64² std 74.4–75.7(角饰完整);
> ⑥ `< halfSize` 不触发(64 ≪ 512)。
>
> ⚠️ **v4 墨线未贴画布边** —— 我按「贴边」连出三轮(v3 外缩 11 / v4 外缩 22)均失败,
> 判定「精确像素位置」超出该模型能力(见 image-gen Gotcha 2),故改走**用 slice 值吸收**:
> slice 64 把三道墨线完整包进角块,拉伸从墨线外侧的暖纸开始。

| USS 文件 | 原 slice(已作废) | 现 slice | 依据(2026-10-05 · 1024×1024 v4 实测) |
|---|---|---|---|
| `SkeuoPaper.uss:25-28` | ~~42px~~(白框版暗带 0–41px) | **64px** | 三道墨线 22–57px(占边 6.2%)· 接缝跳变 mean 4.00 · 四角 std 74.4–75.7 |
| `SkeuoScroll.uss:17-20` | 16px | 68px | 暗带 0–67px(木带 + 黄铜压条) |

`NineSliceBoundsValidator` 的 `< halfSize` 断言在 1024² 上不触发(64/68 ≪ 512)。

> ⚠️ **卷轴 68px 偏大**:木带 24px + 压条合计占边 13%。若后续小尺寸元素
> (如 `save-slot-ui` 的册面行)复用该边框,九宫格角块会显得笨重,届时应单独出一张
> 细带版而非改这张 —— 改 slice 会连带改拉伸行为。
>
> ⚠️ **border_paper 与 border_scroll 的 slice 值已不同族** —— 纸 64 / 卷轴 68 各有实测依据,
> 不得再按「两边框对齐」的思路统一。

### spec 一致性(2026-10-05 复核 —— 甲-B 与 art-bible 的口径对齐)

用户裁甲-B 后,`border_paper` 的纸面从「纯白」改为「暖褐 aging paper」。**这不是口味变更,
是与 art-bible 既有条款对齐**:`§7.2` 纸面元件行的「禁用」列**明写「纯色底(纸纹当纹理)」**,
而 `SkeuoPaper.uss` 的 `--skeuo-paper-bg` 是变量不是白,`casebook-paper.md:44` 的
`casebook_paper_base` 语义是「宣纸纤维 / 泛黄 / 霉斑 / 水渍 / 卷边磨损」。
⇒ **原 v2 白框版(近白 88.4% / R-B +0.3)本身就是违反 §7.2 的纯色底,不是合规版。**

| 条款 | 要求 | v4 实测 | 判定 |
|---|---|---|---|
| `art-bible §8.2` UI-纸 **1K** 档 | 单张 ≤ 1K,合图走 atlas | 1024×1024 / 1417720 B | ✅ |
| `art-bible §7.2` 纸面材质语言 | 旧纸米黄底 + 纤维 + 自然老化 | R-B **+140.3** · 中央 std 3.5(纤维) | ✅ |
| `art-bible §7.2` 禁**纯色底** | 禁把纸纹当纹理 | 中央区亮度 133.8(非 250+ 白) | ✅ 该条正是原 v2 被判不合格的理由 |
| `art-bible §7.2` 禁镜面高光 | 无 specular | 无(缩略图目视) | ✅ |
| `art-bible §7.2` AI 材质层 | AI 生成 + 零人工描图 | `human_edits: none` | ✅ |
| `-unity-slice-*` | 九宫格切片 | slice 64px,见上表 | ✅ |

⚠️ **仍不入 atlas**:v4 尚未进任何 `.spriteatlas`(全库零 `.spriteatlas`),
§8.2 末的「UI-纸 1K 档须合入 atlas、atlas ≤2K 短边」**未兑现** —— 归 `story-019` 019-b
(`PAGES_MAX` 未冻结,结构性 NOT-RUN,禁借绿)。

> **验收方式**：颜色 / 饱和度 / 平铺接缝 / 构图结构由量化脚本判（分位灰度、HSV 饱和度、
> 边缘邻域 std 归一化的接缝比、主体区取色、格线落点检查）；**内容语义与风格由用户目视定稿**。
> `art-bible §7.2` 禁人工描图 —— 全部 16 张零人工编辑。

