# 019 完全实现所需美术资产清单(实测盘点 · 2026-10-05)

> **性质**:**需求清单**,供美术侧排产。**不是裁定**,不是新清单真源 ——
> 资产需求的**唯一登记处**仍是 `design/assets/entity-inventory.md`(承 `art-bible §8.11.6`)。
> 本件只回答一个问题:**「019 要完全落地,还差哪些美术产出?」**
>
> **编制依据(逐条实测,可复算)**:
> - `production/epics/skeuomorphic-ui/story-019-texture-binding.md` §状态拆分 / AC
> - `production/milestones/README.md` §三 Exit Criteria 第 5 条 + 附「M2 灰盒登记表」
> - `design/art/art-bible.md` §8.11(灰盒政策)· §4.5(纸纹非纯色底)
> - `unity/Assets/Gameplay.UI/Skeuomorphic/Textures/`(16 张逐张验尺寸/字节)
> - `production/qa/evidence/art-asset-ruling-recommendation-2026-10-03.md`(分歧 1 的两种 (b))

---

## 〇、一句话

**019-d 的要害不是「再出 16 张图」—— 16 张已在库。缺的是三类「非图」产出:
① 九宫格切图边界元数据(冻结件)· ② 逐变体映射语义裁定 · ③ 铜族焦点 2px 实物。**
外加一项**工程侧**(非美术)的导入格式订正作前置。

---

## 一、已在库(无需新出图)—— 16 张 `*-final.png`,25 MB 总计

| 族 | 件 | 尺寸 | 字节 | 有对应注册类? |
|---|---|---|---|---|
| **纸族** | `paper_xuan-final.png` | 1024² | 1450 KB | ✅ `paper` |
| | `paper_aged-final.png` | 1024² | 1195 KB | ✅ `paper-aged` 变体 |
| | `paper_hemp-final.png` | 1024² | 1383 KB | ⚠️ **无对应** |
| | `paper_burnt_edge-final.png` | 1024² | 1657 KB | ⚠️ **无对应** |
| | `border_paper-final.png` | 1024² | 739 KB | ✅ `paper`(九宫格边框) |
| **墨族** | `ink_wet-final.png` | 1024² | 1240 KB | ✅ `ink` |
| | `ink_dry-final.png` | 1024² | 1646 KB | ✅ `ink`(乾态) |
| | `ink_light-final.png` | 1024² | 1540 KB | ⚠️ **无对应** |
| | `ink_dot-final.png` | 1024² | 976 KB | ✅ `ink`(点记号) |
| **卷轴族** | `scroll_cap-final.png` | 1024² | 2124 KB | ⚠️ **无对应** |
| | `scroll_rod-final.png` | 1024² | 1121 KB | ✅ `scroll` |
| | `scroll_knot-final.png` | 1024² | 1277 KB | ⚠️ **无对应** |
| | `border_scroll-final.png` | 1024² | 993 KB | ✅ `scroll`(九宫格边框) |
| **印章族** | `seal_red-final.png` | 1024² | 1311 KB | ✅ `seal` |
| | `seal_surface-final.png` | 1024² | 1511 KB | ✅ `seal` |
| **图标族** | `ui_icons_sprite-final.png` | **2048²** | 4546 KB | ⚠️ **无注册类** |

> **实测结论(重要)**:16 张图中 **7 张无对应注册类**。
> ⇒ 这不是「缺图」,是**「有图无处挂」** —— 属下面 §二 的映射语义问题。

---

## 二、019-d 缺的三类产出

### ① 九宫格切图边界元数据(**冻结件**)—— 🔴 最承重

**AC-42-C8 原文**:「`-unity-slice-*` 值与该元件图集内的**实际切图边界**一致(值来自切图冻结件的元数据,**不得手填**)」。

**实测缺口**:`design/assets/specs/` 下 19 份 spec **零九宫格边界元数据**;
`SkeuoPaper.uss` 现用的 `64px` 是**人工实测填**的 —— 正是本 AC 点名的**教科书反例**。

**须美术交付**(按族):

| 族 | 须冻结的九宫格件 | 现状态 |
|---|---|---|
| 纸族 | `border_paper` 的四边 slice | ⚠️ 手填 64px(未冻结) |
| 卷轴族 | `border_scroll` 的四边 slice | ⚠️ 手填 68px(未冻结) |
| 印章族 | `scroll` / `seal` 边界 | ⚠️ 手填 8px(未冻结) |
| 墨族 | 记号不涉九宫格(点/划) | — |

**元数据形态建议**:每件一份 `{left, right, top, bottom}` 的**冻结记录**(可入 `design/assets/specs/<name>.md`
的切片表)—— 关键在**可追溯到冻结轮次**,而非格式。

> ⚠️ **为什么这是最承重的**:`milestones/README.md` 已裁「**切图早错 = 全局返工**,牵连全部 USS + atlas」。
> 冻结件一旦错,所有 `-unity-slice-*` 与 atlas 布局须重做。

### ② 逐变体映射语义裁定 —— 🟡 属裁定,非出图

**AC-42-C7** 要求「每个**已注册元件类**含 `background-image` 指向真实贴图」。但实测:

- 注册表仅 **4 类**:`paper` / `scroll` / `ink` / `seal`(+ 3 个变体)
- 16 张图有 **7 张无对应类**(见 §一 表)

**须裁的两问**:

1. **一张图对哪个变体?** —— 例:`paper` 用 `paper_xuan` 还是 `paper_hemp`?`paper-aged` 用 `paper_aged` 还是 `paper_burnt_edge`?这是**语义映射**,工程无法自裁(自裁 = 第二真源)。
2. **无对应类的 7 张图怎么办?** —— 三条路:
   - (甲) 新增注册类(`MaxRegisteredComponents = 16`,现用 4 ⇒ **有余量**)
   - (乙) 作为现有类的**变体**挂载(`MaxVariantsPerComponent = 4`,余量需核)
   - (丙) 明确**排除**(如 `ui_icons_sprite` 图标族可能根本不走元件库)

> ⚠️ **「铜」族另计**:全库 `grep brass|copper|bronze` = **0** —— 铜族在 16 张里**零实物**。
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

## 三、工程侧前置(非美术,但阻塞 019-d)

### 导入格式订正 —— 16 张 `.meta` 全不合九宫格要求

**实测**(16 个 `.meta` 逐个核):

```
spriteMode: 0                        ← 单图,九宫格须 1(Multiple)
spriteBorder: {x:0, y:0, z:0, w:0}   ← 零边界
textureType: 0                       ← Default,须 8(Sprite)
alphaIsTransparency: 0               ← 须 1
```

**依据**:`story-019:404` 明写这是 **AC-42-C8 的「物理前提」**(`spriteMode:0` 下九宫格**不可能工作**)。

> ⚠️ **归属**:此项**可内部做**(不需美术),但须**与冻结件同时落地** ——
> 因为 `spriteBorder` 的值**正是**冻结件的九宫格边界。分开做 = 做完即错。

---

## 四、依赖图(谁卡谁)

```
        切图冻结件(美术 ①)─────────────┐
                                        ▼
导入格式订正(工程)──┐        019-d 接图(C7接图半 + C8)
                    └────────►            ▲
逐变体映射裁定(裁定 ②)──────────────────┘
                                          │
铜族 2px 实物(美术 ③)────────────────────┘
                                          ▼
                        M2 Exit Criteria 第 5 条(形态件 ①②)
                                          │
                        atlas 打包 + 页数实测 ──► 019-b(C9,待 PAGES_MAX spike)
```

---

## 五、汇总:须美术产出的**仅 2 件**,其余是元数据与裁定

| # | 产出 | 类型 | 卡它的是什么 | 归属 |
|---|---|---|---|---|
| **1** | **五族九宫格切图边界元数据**(冻结件) | 元数据 | 需美术裁定真实切图边界 | 019-d(BLOCKING) |
| **2** | **逐变体映射语义**(7 张无类图 + 变体对应) | 裁定 | 需语义决策,工程不可自裁 | 019-d(BLOCKING) |
| **3** | **铜族焦点黄铜 2px 最小切片** | **新出图** | 零实物 | M2 硬前置(E 裁) |
| 4 | 导入格式订正(16 `.meta`) | 工程 | 依赖 #1 | 019-d |

> **⇒ 结论**:019 完全实现的**美术侧瓶颈只有 3 件** ——
> **一件新出图(铜 2px)+ 两份元数据/语义交付**。
> 16 张主贴图**早已入库**,不构成缺口。
>
> ⚠️ **不在本清单内**(避免误读):42 项 VS Critical 的其余 39 项(角色/环境/道具/物品/HUD 等)
> 已由 `milestones/README.md` 「M2 灰盒登记表」显式登记为**灰盒** —— 那是**已裁定**的状态,非缺口。

---

## 六、⚠️ 附加实测发现:C7 的「已注册元件类」范围**比注册表更宽**

**019-c 期间实测(2026-10-05)** —— 元件库 9 个 USS 内共 **24 个类选择器**,
但 `SkeuoComponentRegistry` 只登记 **4 类**(`paper` 变体 `-aged` / `ink` 变体 `-faded` /
`seal` 变体 `-small` / `scroll` 无变体)。其余类分属**别的登记表/文件**:

| 类族 | USS 类(实测) | 登记处 | 与 16 张图的关系 |
|---|---|---|---|
| **元件类**(注册表) | `.paper` `.paper-aged`(2+1=4) `.scroll` `.scroll-rod` `.ink` `.ink-faded` `.seal` `.seal-small`(=`Register` 所出) | `SkeuoComponentRegistry`(**4 类**) | ✅ C7 的直接面 |
| **记号族** | `.mark-check` `.mark-dot` `.mark-fold-single` `.mark-fold-double` `.mark-fold-locked` `.mark-strike` `.mark-seal`(**7 选择器 / 6 登记形状**) | **`MarkRegistry`**(6 形状,**独立登记表**) | ⚠️ **C7 是否覆盖?未裁** |
| **黄铜族** | `.brass` `.brass-scale` `.brass-aged` | **无登记表**(硬编码色值) | 🔴 **零实物**(见 §二 ③) |
| **器具族** | `.implement` `.implement-wood` `.implement-leather` | **无登记表** | ⚠️ 无对应图 |
| **焦点族** | `.focus-visible` `.focus-visible-disabled` | `FocusVisibleStyle` | 🔴 铜 2px 的落点 |
| 排版辅助 | `.ink-title` `.ink-body` | (样式细分,非元件) | — |

> **实测口径**:`grep -hoE '^\s*\.[a-z][a-z0-9-]*' *.uss | sort -u` 去重后 **24** 个;
> `SkeuoComponentRegistry.cs:84-92` 实测 `Register` 4 次(paper 2 槽 / scroll 1 / ink 2 / seal 2);
> `MarkRegistry.cs:27-28` 实测 6 个 mark 形状(`mark-fold-locked` 在 USS 有选择器但**不在登记表**)。

**⇒ 这直接放大 §二 ② 的裁定面**:**C7 的「已注册元件类」到底指哪一批?** 三种读法:
- **(窄)** 只 `SkeuoComponentRegistry` 的 4 类 —— 019-c 现采此读法
- **(中)** 加 `MarkRegistry` 6 记号 —— 但记号是**形状**(点/划),本就不是贴图元件
- **(宽)** 全部 24 类 —— 会把 `.brass` / `.implement`(零实物、无登记表)全拉进来

> ⚠️ **我(主会话)不自裁此范围** —— 它直接决定 019-d 的接图量(4 类 vs 24 类)。
> 已登记在 `story-019` §状态拆分「图标族无注册类」一并待裁。
