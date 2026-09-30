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
└── border_scroll-final.png
```

> **`-final` 后缀的由来（2026-09-30）**：`/image-gen` 的迭代产物带 `-v1`/`-v2`… 后缀留在
> `assets/design-references/`（gitignored，不入库）。入库的是每张的定稿版，改名为
> `<资产名>-final.png`——**后缀本身是"这是定稿、不要再迭代"的标记**，去掉会让
> 后续的 `-v7` 与定稿在目录里无法区分。

---

## 九、生成记录（2026-09-30 定稿）

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
| 15 | `border_paper` | 2 | **v2** | v1 边线内缩 109px,九宫格切不到 → v2 要求边线贴画布边(最外 42px),暗带落 0–41px,中央 ½ 区纯白 |
| 16 | `border_scroll` | 2 | **v2** | v1 木带内缩 69px 且带宽 102px(几乎占满半图) → v2 贴边(最外 68px),暗带落 0–67px,中央 ½ 区纯白 |

### 九宫格 slice 值(2026-09-30 · 随 v2 重出同步修正)

两张边框重出后,边线贴到画布边,USS 里原有的 slice 值(纸面 12px / 卷轴 16px)全部作废 ——
那是从**纯白边缘**量出来的,切不到任何墨线。已按实测暗带落点改为:

| USS 文件 | 原 slice | 现 slice | 依据(1024×1024 实测) |
|---|---|---|---|
| `SkeuoPaper.uss:21-24` | 12px | **42px** | 暗带 0–41px(三道:0–14 / 22–29 / 39–41) |
| `SkeuoScroll.uss:17-20` | 16px | **68px** | 暗带 0–67px(木带 + 黄铜压条) |

四角 42px / 68px 见方内暗像素占比 81% / 98%,角部装饰可用,非空角。
`NineSliceBoundsValidator` 的 `< halfSize` 断言在 1024² 上不触发(42/68 ≪ 512)。

> ⚠️ **卷轴 68px 偏大**:木带 24px + 压条合计占边 13%。若后续小尺寸元素
> (如 `save-slot-ui` 的册面行)复用该边框,九宫格角块会显得笨重,届时应单独出一张
> 细带版而非改这张 —— 改 slice 会连带改拉伸行为。

> **验收方式**：颜色 / 饱和度 / 平铺接缝 / 构图结构由量化脚本判（分位灰度、HSV 饱和度、
> 边缘邻域 std 归一化的接缝比、主体区取色、格线落点检查）；**内容语义与风格由用户目视定稿**。
> `art-bible §7.2` 禁人工描图 —— 全部 16 张零人工编辑。

