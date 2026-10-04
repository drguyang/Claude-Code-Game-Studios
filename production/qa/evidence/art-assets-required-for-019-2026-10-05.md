# 019 完全实现所需美术资产清单(实测盘点 · 2026-10-05 · **019 拆解收口版**)

> **性质**:**需求清单**,供美术侧排产。**不是裁定**,不是新清单真源 ——
> 资产需求的**唯一登记处**仍是 `design/assets/entity-inventory.md`(承 `art-bible §8.11.6`)。
> 本件只回答一个问题:**「019 要完全落地,还差哪些美术产出?」**
>
> **本轮订正(2026-10-05 · 019-e 收口后)**:019 已拆为 **c / e / d / f / b** 五档,
> 其中 **c 与 e 已 Complete** ⇒ 本清单据**当前实测**重写:
> - §三(导入格式订正)**已由 019-e 完成** ⇒ 从「缺项」移入「已闭」
> - §六(C7 范围)**已由用户裁定 = 窄读法 4 类** ⇒ 从「待裁」移入「已裁」
> - 类选择器复算 **25 个**(非原记 24)
>
> **编制依据(逐条实测,可复算)**:
> - `production/epics/skeuomorphic-ui/story-019-texture-binding.md` §状态拆分 / AC
> - `production/milestones/README.md` §三 Exit Criteria 第 5 条 + 附「M2 灰盒登记表」
> - `design/art/art-bible.md` §8.11(灰盒政策)· §4.5(纸纹非纯色底)
> - `unity/Assets/Gameplay.UI/Skeuomorphic/Textures/`(16 张逐张验尺寸/字节)
> - `unity/Assets/Gameplay.UI/Skeuomorphic/*.uss`(25 类选择器 + 3 族 slice 现值)
> - `production/qa/evidence/art-asset-ruling-recommendation-2026-10-03.md`(分歧 1 的两种 (b))

---

## 〇、一句话

**019-d 的要害不是「再出 16 张图」—— 16 张已在库。缺的是三类「非图」产出**:
**① 九宫格切图边界元数据(冻结件)· ② 逐变体映射语义裁定 · ③ 铜族焦点 2px 实物(唯一须新出图)。**

> ✅ **原第四项「工程侧导入格式订正」已由 019-e 完成(2026-10-05)** —— 16 张 `.meta` 已转 Sprite、
> 引擎回读 `mode=Single / spriteCount=1`;`spriteBorder` 留零哨兵待 ①。
> ✅ **原「C7 范围待裁」已由用户裁定 2026-10-05 = 窄读法(4 类)** —— 见 §六。

---

## 一、已在库(无需新出图)—— 16 张 `*-final.png`,约 25 MB

| 族 | 件 | 尺寸 | 字节 | 有对应注册类? |
|---|---|---|---|---|
| **纸族** | `paper_xuan-final.png` | 1024² | 1450 KB | ✅ `paper` |
| | `paper_aged-final.png` | 1024² | 1195 KB | ✅ `paper` 变体 `-aged` |
| | `paper_hemp-final.png` | 1024² | 1383 KB | ⚠️ **无对应** |
| | `paper_burnt_edge-final.png` | 1024² | 1657 KB | ⚠️ **无对应** |
| | `border_paper-final.png` | 1024² | 739 KB | ✅ `paper`(九宫格边框) |
| **墨族** | `ink_wet-final.png` | 1024² | 1240 KB | ✅ `ink` |
| | `ink_dry-final.png` | 1024² | 1646 KB | ✅ `ink`(乾态) |
| | `ink_light-final.png` | 1024² | 1540 KB | ⚠️ **无对应** |
| | `ink_dot-final.png` | 1024² | 976 KB | ✅ `ink`(点记号) |
| **卷轴族** | `scroll_cap-final.png` | 1024² | 2124 KB | ⚠️ **无对应** |
| | `scroll_rod-final.png` | 1024² | 1121 KB | ✅ `scroll-rod` |
| | `scroll_knot-final.png` | 1024² | 1277 KB | ⚠️ **无对应** |
| | `border_scroll-final.png` | 1024² | 993 KB | ✅ `scroll`(九宫格边框) |
| **印章族** | `seal_red-final.png` | 1024² | 1311 KB | ✅ `seal` |
| | `seal_surface-final.png` | 1024² | 1511 KB | ✅ `seal` |
| **图标族** | `ui_icons_sprite-final.png` | **2048²** | 4546 KB | ⚠️ **无注册类** |

> **实测结论(重要)**:16 张图中 **7 张无对应注册类**
> (`paper_hemp` · `paper_burnt_edge` · `ink_light` · `scroll_cap` · `scroll_knot` · `ui_icons_sprite` …)。
> ⇒ 这不是「缺图」,是**「有图无处挂」** —— 属 §二 ② 的映射语义问题。
>
> ⚠️ **实测补充**:16 张中**仅 `border_paper` 在 USS 内被提及**(且只在注释里)——
> 其余 15 张 `grep` 全库 USS **零引用**。这正是 story-019「骨架已建,皮未贴」的实测形态。

---

## 二、019-d 缺的三类产出

### ① 九宫格切图边界元数据(**冻结件**)—— 🔴 最承重

**AC-42-C8 原文**:「`-unity-slice-*` 值与该元件图集内的**实际切图边界**一致(值来自切图冻结件的元数据,**不得手填**)」。

**实测缺口**:`design/assets/specs/` 下 19 份 spec **零九宫格边界元数据**;
现 3 族 slice 值**均为人工实测填** —— 正是本 AC 点名的**教科书反例**:

| 族 | USS 文件 | 现值 | 现状态 |
|---|---|---|---|
| 纸族 | `SkeuoPaper.uss` | `64px`(四边) | ⚠️ 手填(注释自陈「实测落 22–57px,取 64」) |
| 卷轴族 | `SkeuoScroll.uss` | `68px`(四边) | ⚠️ 手填(未冻结) |
| 印章族 | `SkeuoSeal.uss` | `8px`(四边) | ⚠️ 手填(未冻结) |
| 墨族 | 记号不涉九宫格(点/划) | — | — |

> ⚠️ **`spriteBorder` 现为全零哨兵**(019-e 刻意留)——
> 引擎回读 `spriteBorderActual=(0,0,0,0)`。**冻结件落盘后一次填入**,禁 019-e / 019-d 自填(第二真源)。

**须美术交付**:每件一份 `{left, right, top, bottom}` 的**冻结记录**(可入
`design/assets/specs/<name>.md` 的切片表)—— 关键在**可追溯到冻结轮次**,而非格式。

> ⚠️ **为什么这是最承重的**:`milestones/README.md` 已裁「**切图早错 = 全局返工**,牵连全部 USS + atlas」。
> 冻结件一旦错,所有 `-unity-slice-*` 与 atlas 布局须重做。

### ② 逐变体映射语义裁定 —— 🟡 属裁定,非出图

**AC-42-C7(窄读法)** 要求「**4 个已注册元件类**含 `background-image` 指向真实贴图」。但实测:

- 注册表 **4 类**(`SkeuoComponentRegistry.cs:84-92`):`paper`(槽 2,变体 `-aged`)·
  `scroll`(槽 1)· `ink`(槽 2,变体 `-faded`)· `seal`(槽 2,变体 `-small`)
- 16 张图有 **7 张无对应类**(见 §一 表)

**须裁的两问**:

1. **一张图对哪个类/变体?** —— 例:`paper` 用 `paper_xuan` 还是 `paper_hemp`?
   `paper-aged` 变体用 `paper_aged` 还是 `paper_burnt_edge`?
   **这是语义映射,工程无法自裁(自裁 = 第二真源)。**
2. **无对应类的 7 张图怎么办?** —— 三条路:
   - (甲) 新增注册类(`MaxRegisteredComponents` 现有余量,须核)
   - (乙) 作为现有类的**变体**挂载(`MaxVariantsPerComponent = 4`;`paper` 已用 2/2 槽,
     `ink` 已用 2/2,**余量须核**)
   - (丙) 明确**排除**(如 `ui_icons_sprite` 图标族可能根本不走元件库)

> ⚠️ **「铜」族另计**:16 张里**零铜族实物**(`grep -iE 'brass|copper|bronze'` = 0)。
> 但 `SkeuoBrass.uss` 存在(`.brass` / `.brass-scale` / `.brass-aged`,硬编码 `#b87333` 等色值)。
> 其真资产归下面 ③。

### ③ 铜族焦点黄铜 2px 最小切片 —— 🔴 M2 硬前置(**零实物,须新出图**)

**依据**:`milestones/README.md:125`(2026-10-04 E 裁)——
「焦点高亮的**黄铜 2px** 须真做(可读性);形态件 ① 的**明度轴判据要跑在『贴图 × 主题色的最亮像素』上**,
`art-bible §4.5` 已警告纸纹是**纹理非纯色底** ⇒ **无铜族贴图 ⇒ ① 的验收无载体**」。

**须交付**:一件「**焦点黄铜 2px**」最小切片(**只此一件**,E 裁明确「不拉整个铜族进来」)。

| 项 | 说明 |
|---|---|
| 用途 | 焦点态高亮边框(可读性硬需求) |
| 尺寸 | 2px 视觉厚度(切图本身可更大,须标 slice) |
| 承载元件 | `SkeuoFocusVisible.uss` 的 `.focus-visible`(现有;无双栈例外) |
| 现状 | **零实物** —— `SkeuoBrass.uss` 只有纯色值,无贴图 |

---

## 三、工程侧前置 —— ✅ **已由 019-e 完成(2026-10-05)**

~~导入格式订正 —— 16 张 `.meta` 全不合九宫格要求~~

**已交付**:
```
spriteMode: 1                        ← = SpriteImportMode.Single(枚举 None=0/Single=1/Multiple=2/Polygon=3)
textureType: 8                       ← Sprite
alphaIsTransparency: 1               ← 开
spriteBorder: {x:0,y:0,z:0,w:0}      ← 零哨兵(刻意留,待 §二 ① 冻结件)
```
- 引擎回读实测:16 张全 `mode=Single` · `spriteCount=1` · `spriteBorderActual=(0,0,0,0)`
- 承接两道门:`ValidateSlicedTextureImportFormat` · `ValidateSpriteBorderLeftAsSentinel`
- 原件:`production/qa/evidence/review-skeuomorphic-ui-story-019e-2026-10-05.md`

> ⚠️ **口径订正(2026-10-05)**:本条曾写「`spriteMode:0` 下九宫格不可能工作 ⇒ 须转 1(Multiple)」。
> 实测 `spriteMode:1` **= Single**(非 Multiple)—— 「Multiple」是**误标**,已全库订正。
> Single 正是**独立九宫格图**的正确模式;Multiple 是**图集/切割**模式。

---

## 四、依赖图(谁卡谁)

```
        切图冻结件(美术 ①)─────────────┐
                                        ▼
逐变体映射裁定(裁定 ②)────────────► 019-d 接图(C7 接图半 + C8)
                                        ▲
铜族 2px 实物(美术 ③)──────────────────┘
                                        │
    [已闭] 019-e 导入格式订正 ──────────┘
                                        ▼
                        M2 Exit Criteria 第 5 条(形态件 ①②)
                                        │
                        atlas 打包 + 页数实测 ──► 019-b(C9,待 PAGES_MAX spike)
```

---

## 五、汇总:须美术产出的**仅 2 件**,其余是元数据与裁定

| # | 产出 | 类型 | 卡它的是什么 | 归属 | 状态 |
|---|---|---|---|---|---|
| **1** | **五族九宫格切图边界元数据**(冻结件) | 元数据 | 需美术裁定真实切图边界 | **019-f** | ⬜ 待美术 |
| **2** | **逐变体映射语义**(7 张无类图 + 变体对应) | 裁定 | 需语义决策,工程不可自裁 | **019-d** | ⬜ 待裁定 |
| **3** | **铜族焦点黄铜 2px 最小切片** | **新出图** | 零实物 | M2 硬前置(E 裁) | ⬜ 待出图 |
| ~~4~~ | ~~导入格式订正(16 `.meta`)~~ | 工程 | — | 019-e | ✅ **已闭 2026-10-05** |

> **⇒ 结论**:019 完全实现的**美术侧瓶颈只有 3 件** ——
> **一件新出图(铜 2px)+ 两份元数据/语义交付**。
> 16 张主贴图**早已入库**,不构成缺口。
>
> ⚠️ **不在本清单内**(避免误读):42 项 VS Critical 的其余 39 项(角色/环境/道具/物品/HUD 等)
> 已由 `milestones/README.md` 「M2 灰盒登记表」显式登记为**灰盒** —— 那是**已裁定**的状态,非缺口。

---

## 六、✅ C7「已注册元件类」范围 —— **已裁 = 窄读法(4 类)** · 2026-10-05 用户裁定

**原问题(019-c 期间实测)**:元件库 9 个 USS 内共 **25 个类选择器**,
而 `SkeuoComponentRegistry` 只登记 **4 类** ⇒ C7 的「已注册元件类」指哪一批?

### 裁定:**窄读法 = `SkeuoComponentRegistry` 登记的 4 类(+ 3 变体)。宽读法否决。**

**依据(三条)**:
1. **AC 原文自限** —— C7 写的是「`SkeuoComponentRegistry` 登记的全部种类与变体」,
   **AC 自己就点了注册表**,不是「全部 USS 类」。
2. **记号族本就不是贴图元件** —— `.mark-*`(7 选择器 / 6 形状)是**几何形状**(点/划),
   由 `MarkRegistry` 派生绘制,**物理上无对应贴图** ⇒ 拉进来只造「有类无图」假缺口。
3. **黄铜/器具/焦点族零实物且无登记表** —— `.brass*` / `.implement*` / `.focus-visible*`
   **无登记表** ⇒ 「已注册」判据无法适用。

> **⇒ 019-d 接图量 = 4 类**(不是 25)。

### 25 个类选择器逐族实测(复算命令见下)

| 类族 | USS 类(实测) | 登记处 | 与 16 张图的关系 |
|---|---|---|---|
| **元件类**(注册表) | `.paper` `.paper-aged` `.scroll` `.seal` `.seal-small` `.ink` `.ink-faded` `.scroll-rod` | `SkeuoComponentRegistry`(**4 类 + 3 变体**) | ✅ **C7 的直接面(窄读法)** |
| **记号族** | `.mark-check` `.mark-dot` `.mark-fold-single` `.mark-fold-double` `.mark-fold-locked` `.mark-strike` `.mark-seal`(**7**) | `MarkRegistry`(6 形状,独立表) | ➖ **不在 C7 面**(形状非贴图) |
| **黄铜族** | `.brass` `.brass-scale` `.brass-aged`(**3**) | **无登记表** | 🔴 **零实物**(见 §二 ③) |
| **器具族** | `.implement` `.implement-wood` `.implement-leather`(**3**) | **无登记表** | ➖ 无对应图 |
| **焦点族** | `.focus-visible` `.focus-visible-disabled`(**2**) | `FocusVisibleStyle` | 🔴 铜 2px 的落点 |
| **排版辅助** | `.ink-title` `.ink-body`(**2**) | (样式细分,非元件) | ➖ 非元件 |

**复算命令**:
```bash
cd unity/Assets/Gameplay.UI/Skeuomorphic
grep -hoE '^\s*\.[a-z][a-z0-9-]*' *.uss | tr -d ' ' | sort -u | wc -l   # → 25
grep -n "Register(" SkeuoComponentRegistry.cs                            # → 4 行(:84/87/89/92)
```

> **已同步**:story-019 §状态拆分 + §AC-42-C7 · `MarkRegistry` 归属 · 本件。
