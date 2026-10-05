# Story 019: 贴图接入(16 张 `*-final.png` → USS 元件族 · 九宫格 slice 对齐真实切图 · 图集页数实测)

> **Epic**: 拟物 UI 框架
> **Status**: **部分完成** —— **019-c `Complete ✅`(2026-10-05)** · **019-d `Complete ✅`(2026-10-05)** · **019-e `Complete ✅`(2026-10-05)** · 019-f/b 见下 §状态拆分
> **Layer**: Foundation
> **Type**: UI
> **Estimate**: 待估(依赖五族切图冻结)
> **Manifest Version**: 2026-10-03
> **Last Updated**: 2026-10-05

### ⚠️ 状态拆分(2026-10-04 J 裁 → 2026-10-05 三档再裁 → 2026-10-05 用户裁定拆 d/e/f)

**原状态 `Ready` 与文件自身登记矛盾** —— 本 story §Dependencies 挂着两件 **BLOCKED-BY**
(ADR-013 §6.6 假设 6 spike 未跑 · `PAGES_MAX` 未冻结),却标 `Ready`。技术美术实测指出:
其 5 条 AC 中 **AC-42-C9 现在根本无法判**(它要求「页数 ≤ `PAGES_MAX`」,而 `PAGES_MAX` 未冻结)。

**2026-10-04 J 初裁:拆 a/b 两半** —— a(接图,C7/C8/C10/C11)· b(图集预算,C9,结构性 NOT-RUN)。

**2026-10-05 逐 AC 实测再裁 —— J 的 a/b 二分不够细,按「卡什么」重划**:

实测发现 019-a 的 4 条 AC 卡点**各不相同**,其中两条**零外部依赖**,另有一条**只卡数据不卡裁定**:

| 分档 | AC / 内容 | 卡什么(实测) | 状态 |
|---|---|---|---|
| **019-c · 护栏** | **C10** + **C11** + C7(骨架半) | **零外部依赖** —— 屏幕层禁引 / 悬空即红,均为**纯 lint 断言** | ✅ **Complete 2026-10-05** |
| **019-e · 导入格式订正** | 16 张 `.meta`:`spriteMode 0→1` · `textureType 0→8` · `alphaIsTransparency 0→1` · **`spriteBorder` 值(留哨兵待美术)** | **零裁定依赖** —— 格式是**机械的**;唯 `spriteBorder` 的**数值**待 019-f 冻结件 | ✅ **Complete 2026-10-05** |
| **019-d · 接图** | C7(接图半)+ **贴图容器类**加 `background-image` + `-unity-slice-*` | ✅ **裁定已闭 + 工程已落(2026-10-05)** —— 注册表增列 `IsTextureContainer` · 四基类接图(落地 5 选择器)· `.ink` 补背景 · 焦点改铜侧 · C4 硬编码 44→0 | ✅ **Complete 2026-10-05** |
| **019-f · C8 冻结件** | 九宫格 slice 值**来自切图冻结件的元数据**(不得手填) | **美术** —— 需美术实测真实切图边界,产出冻结记录 | 待美术点亮 |
| **019-b · 图集预算** | C9 | **spike** —— `PAGES_MAX` 未冻结(非美术) | **Blocked** |

> **019-c 的实测依据**:① 7 个屏幕 UXML 全部只引类名,`url(` 命中 **0** ⇒ C10 可立即锁死;
> ② 16 张图 GUID 均在,可解析 ⇒ C11 可立即实现;③ 注册表实测仅 **4 类**(纸/卷轴/墨/印章 —— 截至 019-c),
> 图标族(`ui_icons_sprite`)**无注册类**。
>
> ⇒ **019-c 不受冻结件阻塞,不该陪 C8 一起等** —— 它现在就把「皮未贴」这个失效模式
> 变成**构建失败**(C11 悬空即红 + C10 屏幕层不许绕开元件库),正是本 story 的存在理由。

### ⚠️ 拆 d/e/f 的理由(2026-10-05 用户裁定)

**原 019-d 把「格式订正」与「美术裁定」混成一件,导致最轻的一块被最重的一块拖着。** 实测三者的卡点完全不同:

| 块 | 卡的是什么 | 谁能解开 |
|---|---|---|
| **019-e · 格式** | 无 —— `.meta` 的四项是**机械订正**,值域确定 | **工程**(立即) |
| **019-d · 接图** | 贴图容器类 → 哪张图的**语义映射** | ✅ **已裁 2026-10-05**(见 §C7 收窄) |
| **019-f · C8** | 真实切图**边界的数值** | 美术(实测) |

> ⚠️ **019-e 与 019-f 的耦合点(承 story:126「做完即错」纪律)**:`spriteBorder` 的**值**正是
> 019-f 的冻结件边界 —— 故 019-e **不能自填** `spriteBorder`。裁:019-e 先把**格式**改对,
> `spriteBorder` 留**零哨兵**(与现状同值,不引入第二真源),由 019-f 落冻结件后**一次填入**。
> ⇒ 分开做**不**等于「做完即错」;**自填才算**。

### ⚠️ C7「已注册元件类」范围 = **「贴图容器类」** —— 用户裁定 2026-10-05(**取代原窄读法**)

> 🔴 **本节 2026-10-05 第二轮裁定后重写** —— 原裁「窄读法 = 注册表 4 类」**已被取代**。
> **新判据**:`SkeuoComponentRegistry` **增列 `IsTextureContainer: bool`**,
> C7 判据 = **「`IsTextureContainer == true` 的类须含 `background-image`」**。
> 依据:实测发现原「4 类全须有 `background-image`」**本身不自洽** ——
> `.ink` 整类**无** `background-color`(纯字色 + `text-shadow`,承 `art-bible §7.2`
> 「字号差异走 USS 变量」)· `.seal-small` 仅 `font-size` · `.scroll-rod` 注释自陈「纯装饰」。
> ⇒ 把「已注册」与「贴图容器」**解耦**。

**问题**(2026-10-05 首轮):元件库 USS 实有 **24 个类选择器**,而 `SkeuoComponentRegistry` 只登记 4 类
(纸/卷轴/墨/印章)。C7 说「每个**已注册元件类**」——「已注册」指哪个面?

**首轮裁定(已被取代,留档)**:~~窄读法 = 注册表 4 类(+3 变体);宽读法(24 类)否决~~。

**现行裁定(2026-10-05 第二轮):判据 = `IsTextureContainer`。** 见本节节首。
逐类判定(实测 + 用户裁定):

| 类 | `IsTextureContainer` | 依据 |
|---|---|---|
| `paper` | **true** | `.paper` 有 `background-color` |
| `paper-aged` | **true** | 覆盖 `--skeuo-paper-bg-aged` |
| `scroll` | **true** | `.scroll` 有 `background-color` |
| `ink` | **true** ⚠️ **改判** | 原判 false(纯字色);因 `ink_light` 归此族 ⇒ 改判 · **`.ink` 须补 `background-image`** |
| `ink-faded` | **false** | 仅 `color:`,且无对应图(裁:仅基类改判,变体保持) |
| `seal` | **true** | `.seal` 有 `background-color`;`seal_red` / `seal_surface` 有处挂 |
| `seal-small` | **false** | 仅 `font-size` |

> ⇒ **贴图容器(注册项)= 4 项**(`paper` / `scroll` / `ink` / `seal`)= **019-d 的 C7 判据面**
> (原「3 类」被 `ink` 改判加入取代)。
> ⚠️ **2026-10-05 019-d 实施勘误**:原记「5 项」把 `.paper-aged` 误作独立项 —— 实测
> `SkeuoElement` 枚举**只 4 值**,`.paper-aged` 是 `paper` 的**变体类**(`VariantSuffix = "-aged"`),
> 非注册项。⇒ **接图落地 = 5 处选择器**,但 **C7 判据面 = 4 注册项**(门只查注册项)。
> ⚠️ **24 个选择器其余部分**(记号 7 归 `MarkRegistry` 派生 · 黄铜 3 归铜族登记表 ·
> 器具 3 无对应图 · 焦点 2 归 `FocusVisibleStyle` · 排版 2 非元件)**仍不在 C7 面内**。

### ⚠️ 6 张无类图 —— 逐个裁定(2026-10-05 用户裁定 · 混合路径)

| 图 | 裁定 | 连带 |
|---|---|---|
| `paper_hemp` | **新增注册类 `paper-hemp`** | 元件 4→5 |
| `paper_burnt_edge` | **新增注册类(状态类贴图)** | 元件 5→6 |
| `ink_light` | **归 `ink` 族** | 🔴 `.ink` 须补 `background-image` |
| `scroll_cap` | **归铜族** | 🔴 铜族须立登记表(见下) |
| `scroll_knot` | **与 `casebook_paper_stitch` 合并** | 🔴 跨链(语义归 `casebook-paper.md`) |
| `ui_icons_sprite` | **交图标系统**(不走元件库) | 🔴 承接方须登记 |

### ⚠️ 铜族 = **立登记表**(成员 ≥2) —— 2026-10-05 用户裁定

**原口径**:E 裁(2026-10-04)「不拉整个铜族进来」,焦点 2px **只此一件**。
**现行**:用户裁 **铜族立登记表**,成员 = `scroll_cap` + **焦点黄铜 2px** ≥ **2 件**。
⇒ **E 裁的「只此一件」范围已扩**。须定义:切图冻结 · USS 落点 · atlas 归属。

### ⚠️ 焦点载体 = **铜侧 2px**(2026-10-05 用户裁定 · 取代墨侧)

**原状**:`art-bible §7.4` 原文 = 墨色加深 + 纸面压痕边对;`SkeuoFocusVisible.uss`
**实测亦为墨侧实现**。
**现行**:用户裁 **焦点载体 = 黄铜 2px**;`art-bible §7.4` + `:331` **已落 Amendment 注记**(保留原文 + 记改判)。
⇒ 🔴 **`SkeuoFocusVisible.uss` 须由墨侧改为铜侧**(019-d 义务)。

> ⚠️ **019 与 `interaction-system` 的排序(2026-10-04 I 裁)**:**先 `interaction`,`019` 后置**
> —— `019` 不做 ⇒ M2 少一条 Exit Criteria;`interaction` 不做 ⇒ **整条判断链不通**,M2 判据直接不成立。
> **这是判据依赖,不是优先级偏好。** ⇒ interaction 已 7/7 全闭,该约束已解除。

## Context

**GDD**: `design/gdd/skeuomorphic-ui.md`
**Requirement**: `TR-skeuoui-011`(**图集护栏 —— 第 ② 半:图集页数预算 `Pages_frame` / `PAGES_MAX` 与溢出告警阈值**;
第 ① 半「注册表配额」已由 story 001 兑现在 `SkeuoComponentRegistry.cs`,见 EPIC.md §TR-skeuoui-011 混计登记)

**ADR Governing Implementation**: ADR-013: 拟物 UI 框架
**ADR Decision Summary**: 拟物视觉 = 自建 USS 元件库(纸纹 / 墨迹 / 卷轴九宫格 `-unity-slice-*` + 主题变量) + UXML 组合

**Engine**: Unity 6.3 LTS | **Risk**: HIGH
**Engine Notes**: UI Toolkit 图集 / 九宫格 slicing / 自定义材质均 post-cutoff(ADR-013 Knowledge Risk HIGH,§6.6 假设 6 spike 未跑)。本 story **踩在该 spike 面上** —— 它是 TR-skeuoui-004 降级路径之外,另一处直接依赖 UI Toolkit 图集行为的实现面。

**Control Manifest Rules (this layer)**:
- Required: 元件库唯一出口;九宫格 `-unity-slice-*`;主题变量层
- Forbidden: 内联变体(AC-42-C4);硬编码字号/文本(AC-42-C5);超过图集配额
- Guardrail: fallback 字体位必须存在;贴图必须经元件库接入(不得在屏幕 UXML 内直引)

### ⚠️ 本 story 的存在理由(实测,2026-10-03)

story 001 的 6 条 AC **全为 USS 结构断言**,**无一条要求「把贴图绑到元素上」**。实测后果:

| 实测项 | 结果 |
|---|---|
| `unity/Assets/Gameplay.UI/Skeuomorphic/Textures/` 的 16 张 `*-final.png` | **已入库**(真图 0.9–4.6 MB) |
| 这 16 个 GUID **在全库(非 `.meta`)引用数** | **0**(逐个查证无一例外) |
| 全部 `.uss` / `.uxml` 内 `url(` / `background-image` / `resource(` 命中数 | **0** |
| 实际走的链路 | **按 USS 类名**:`Register(SkeuoElement.Paper, "paper", …)` → `element.AddToClassList("paper")` |

⇒ **骨架与护栏已建,皮未贴。** 本 story 贴皮。**它不是可选美化** —— `milestones/README.md` §三
Exit Criteria 第 5 条的形态件 **① 脉案线格/空行/焦点明度轴压在九宫格切图上、② 墨乾湿两态压在墨迹 brush 上**,
**不接图则 ①② 无法交付**。

**前置**:切图族的九宫格切图与 atlas 布局**先冻结**(美术总监裁定
`art-asset-ruling-recommendation-2026-10-03.md` §一:切图早错 = 全局返工,牵连全部 USS + atlas)。
本 story **不产出新图**,只把已冻结的切图接上。

> ✅ **口径订正(2026-10-04 用户裁定 D)**:**「三族(纸 / 墨 / 铜)」为措辞误**,
> 实测 **五族**(16 张逐张复验)—— 与本文件 §Implementation Notes 第 1 条**自陈的五族分法一致**
> (该条原已正确列出纸/墨/卷轴/印章/图标)。原文两处口径不一致,现统一为**五族**。
> ⚠️ 「铜」族**零实物**(截至 2026-10-04),其最小切片已另由 **2026-10-04 E 裁**升为 M2 硬前置
> (`milestones/README.md` §三 Exit Criteria 第 5 条括注)。
> ✅ **2026-10-05 更新**:用户裁定 **铜族立登记表**(成员 ≥2 = `scroll_cap` + 焦点 2px),
> **焦点载体 = 铜侧 2px**(取代墨侧)—— 见 §状态拆分 铜族 / 焦点两小节。

---

## Acceptance Criteria

*承 EPIC.md §范围边界声明;GDD `design/gdd/skeuomorphic-ui.md` AC-42-C3 的图集半边 + 第 ② 半 TR。*

- [x] **AC-42-C7(新)** ✅ **019-d Complete 2026-10-05**: 每个**贴图容器类**(`SkeuoComponentRegistry` 中 `IsTextureContainer == true` 者)对应的 USS 规则
      含 `background-image: url(...)`,指向**真实贴图资产**;纯色填充模拟贴图 ⇒ 构建期/lint 报冲突
      ⇒ **骨架半 ✅(019-c,2026-10-05)**:`ValidateRegisteredClassesHaveSelectorBlock` + 4 类注册表实测(当时值);
      **接图半 ✅(019-d,2026-10-05)**:四基类接图落地 ⇒ 接图半已入 `ValidateAll` 聚合
      (`ValidateTextureContainerHasTexture`,原恒红故此前不入聚合)。
      ✅ **「已注册元件类」范围 = `IsTextureContainer == true`** —— **用户裁定 2026-10-05**
      (**取代**首轮窄读法 4 类;依据见 §状态拆分):
      注册表增列 `IsTextureContainer` ⇒ **贴图容器(注册项)= 4 项**(
      `paper` / `scroll` / `ink`(改判加入)/ `seal`)。
      **`ink-faded` / `seal-small` = false**(仅基类改判,变体保持)。
      ⚠️ **`.paper-aged` 非注册项**(`paper` 的变体类)⇒ 不计入本判据面;
      **接图落地 = 5 处选择器**(4 基类 + `.paper-aged`),但**门只查 4 注册项**。
      ⇒ **019-d 接图量 = 5 处选择器 / C7 判据面 = 4 项**(不是 24,也不是原 3)。
      (值来自切图冻结件的元数据,不得手填)
      ⇒ ⚠️ **实测 2026-10-05**:`design/assets/specs/` **零九宫格边界元数据**;`SkeuoPaper.uss` 的 64px 为**人工实测填**
      = 本 AC 的**教科书反例**。且 16 张 `.meta` 全 `spriteMode:0`(九宫格在此模式下**不可能工作**)
      ⇒ 须先作 **019-e 格式订正** + **019-f 冻结件落盘**。**禁借绿。**
- [ ] **AC-42-C9(新)** ⛔ **归 019-b · Blocked**: 实测**图集页数 ≤ `PAGES_MAX`**;超限 => 构建期冲突并给出溢出告警
      (兑现 `TR-skeuoui-011` 第 ② 半;`PAGES_MAX` 值待与图集布局同批冻结)
      ⇒ ⚠️ **2026-10-04 J:本条现不可判** —— `PAGES_MAX` 未冻结,填占位值 = 第二真源。
      **须待 ADR-013 §6.6 假设 6 spike 落定后方可勾。禁借绿。**
- [x] **AC-42-E1(新)** ✅ **019-e Complete 2026-10-05**: 16 张元件贴图的 `.meta` 导入格式须满足九宫格要求 ——
      `spriteMode: 1`(**= `SpriteImportMode.Single`** —— 枚举 `None=0/Single=1/Multiple=2/Polygon=3`,
      实测 Unity 回读 `spriteImportMode == Single`;「Multiple」是**误标**)· `textureType: 8`(Sprite)· `alphaIsTransparency: 1`;
      `spriteBorder` **不得自填**(值 = 019-f 冻结件;先留哨兵,由 019-f 一次填入)
      ⇒ **依据**:story:404 明写这是 AC-42-C8 的「物理前提」(`spriteMode:0` 下九宫格**不可能工作**)。
      **可判**:构建期/EditMode 断言逐张核三项格式;`spriteBorder` 只核「不早于 019-f 被写死」。
- [x] **AC-42-C10(新)** ✅ **019-c Complete 2026-10-05**: 贴图**只经元件库接入** —— 屏幕级 UXML/USS(脉案 / 存档位 / 库存 / 设置 / 教学 / 调试视图)
      内**不得**出现指向 `Textures/` 的 `url()`;违者构建期报冲突(与「元件库唯一出口」同源)
      ⇒ 实现:`TextureBindingGates.ValidateScreenLevelNoTextureUrl`(门聚合);实测 7 屏幕 `url(` 命中 **0**。
      **MUT-C10**(屏幕 UXML 注入 `url("Textures/…")`)⇒ **2 红**(原夹具 + 真扫描面锚)。
      ⚠️ **扫描面口径(2026-10-05 评审登记)**:实现扫 `Screens/` **全目录**(实 7 uxml)`url()` **任一皆拦**(比 AC 措辞更严);
      AC 点名的 6 屏中「调试视图」**当前无 uxml 文件**,另多出医馆面板 / 教学纸近景。全目录扫描**不会漏新增屏**,故采此口径。
- [x] **AC-42-C11(新)** ✅ **019-c Complete 2026-10-05**: 贴图缺失 / GUID 悬空 ⇒ **构建期硬失败**(非运行期 fallback 到纯色 ——
      静默降级会让「皮未贴」再次不可见,正是本 story 要消灭的失效模式)
      ⇒ 实现:`TextureBindingGates.ValidateTextureReferencesResolve` + `AssetResolves`(可注入 GUID 解析)。
      **MUT-C11**(`SkeuoPaper.uss` 注入悬空 `Textures/__dangling_mut__-final.png`)⇒ **2 红**。
      ⚠️ **残余(登记 019-d 义务)**:现只扫元件库 `*.uss` —— `resource(...)` / `.uxml` 媒介未覆盖(当前全库 0 命中,未触发)。

---

## Implementation Notes

*Derived from ADR-013 Implementation Guidelines:*

1. **接入面**: 16 张 `*-final.png` 按族归位 —— 纸族(paper_xuan / paper_aged / paper_hemp / paper_burnt_edge /
   border_paper)· 墨族(ink_wet / ink_dry / ink_light / ink_dot)· 卷轴族(scroll_cap / scroll_rod / scroll_knot /
   border_scroll)· 印章族(seal_red / seal_surface)· 图标族(ui_icons_sprite)
2. **USS 绑定**: 在元件库 USS(非屏幕 USS)为每个已注册类加 `background-image: url("…")`
   + `-unity-slice-*`;`SkeuoElementLibrary.cs` 的类名链路保持不变(**元件库唯一出口**)
3. **图集**: 按族建 sprite atlas;atlas 页数须实测;**不引入 `.uss` 内的逐图引用散落**
4. **切图元数据**: 九宫格边界**从冻结件读出**,不手填(手填 = 第二真源)
5. **构建期断言**: 扩展 story 001 已有的构建期扫描工具 —— 增 C7/C8/C10/C11 四条断言
   (C3 的注册表配额断言保持不动)
6. ⚠️ **UI Toolkit 图集行为 post-cutoff** —— 实现前须确认 ADR-013 §6.6 假设 6 的 spike 面,
   或按实测另开登记(承 ADR-023 ⑧ 的「零 custom Renderer Feature + 触发条款」精神)

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 001: 元件库结构 / 主题变量 / 注册表配额(第 ① 半)
- Story 011–018: 各屏幕的布局与焦点(本 story 只接图,不改布局)
- 新美术产出: 切图与 atlas 布局的**冻结**归美术;本 story 只消费冻结件

---

## QA Test Cases

*Written by qa-lead at story creation. The developer implements against these — do not invent new test cases during implementation.*

**AC-42-C7**:
- Given: `SkeuoComponentRegistry` 全部登记项(`IsTextureContainer` 标志)+ 元件库 USS
- When: 构建期断言 + lint 扫描
- Then: 每个 **`IsTextureContainer == true`** 的类都有 `background-image: url(...)` 指向真实资产
- Edge cases: 纯色模拟贴图(非法);**容器类**有类无图(非法);非容器类有图(**合法** —— 如
  `ink-faded` / `seal-small` 明确豁免);有图未注册(非法)

**AC-42-C8**:
- Given: 切图冻结件元数据 + 元件库 USS 的 `-unity-slice-*`
- When: 逐元件比对
- Then: slice 值与真实切图边界一致
- Edge cases: slice 值手填但碰巧正确(须证明来自元数据,非重言);零 slice(不应存在)

**AC-42-C9**:
- Given: 族级 sprite atlas
- When: 构建期页数统计
- Then: 页数 ≤ `PAGES_MAX`,超限报冲突并告警
- Edge cases: 恰在阈值;单族跨页;`PAGES_MAX` 未冻结(须先冻结,不得填占位值)

**AC-42-C10**:
- Given: 屏幕级 UXML/USS(story 011–018 的产物)
- When: lint 扫描 `url()`
- Then: 零命中 `Textures/`
- Edge cases: 元件库本身(合法);屏幕内直引(`非法`)

**AC-42-C11**:
- Given: 构建期资产解析
- When: 贴图缺失 / GUID 悬空
- Then: **硬失败**(`throw`,非 `Debug.Assert`,非运行期 fallback)
- Edge cases: 资产存在但导入失败;GUID 指向已删除资产

---

## Test Evidence

**Story Type**: UI
**Required evidence**: `production/qa/evidence/texture-binding-evidence.md`(含**截图** —— 贴图接入与否**只有肉眼可判**,
故本 story 的 AC 走「构建断言 + 目视截图」双轨,不能只靠断言)+ 构建期断言测试

**Status**: **019-c ✅ 17/17 绿(2026-10-05)** · 019-d / 019-b NOT-RUN
**Test File**: `unity/Assets/Tests/EditMode/SkeuomorphicUI/texture_binding_gate_test.cs`(17 条)
**Implementation**: `unity/Assets/Editor.Tools.Gates/TextureBindingGates.cs`(纯逻辑,零引擎依赖)·
  `unity/Assets/Editor.Tools.Gates/SkeuomorphicUiGates.cs`(门聚合,`#if UNITY_EDITOR`)
**评审原件**: `production/qa/evidence/review-skeuomorphic-ui-story-019c-2026-10-05.md`(单轮双代理)

**验证命令**:
```
unity test unity --mode EditMode --filter "DaYiJingCheng.Tests.Unit.SkeuomorphicUI" \
  --output "$PWD/unity/Logs/skeuo-019c.xml"
```
- 019-c 过滤跑:`unity/Logs/skeuo-019c.xml` = **212 / 199 passed / 0 failed / 13 skipped**(含外部 13 Skipped,均为既有 playtest 占位)
- 全量 EditMode:`unity/Logs/full-editmode-019c.xml` = **2434 / 2390 passed / 0 failed / 43 skipped / 1 inconclusive**
  (基线 2429/2385 ⇒ **+5,零回归**;唯一 inconclusive = 既有 `SettingsExposureTest.test_monoOption_existsWithValidDefault`)

**变异证明(修复轮后重跑 —— 旧 MUT 日志已作废)**:

| MUT | 变异 | 结果 | 日志 |
|---|---|---|---|
| **MUT-C10** | 屏幕 UXML(`Casebook39.uxml`)注入 `background-image: url("Textures/paper_xuan-final.png")` | **2 红**:C10 原夹具 + `test_current_repo_root_scan_finds_real_files_not_empty`(证明扫到**真** `Screens/`;报文路径 `Casebook39.uxml:4`,非 `<repo>/Assets/...`) | `unity/Logs/mut-c10.xml` |
| **MUT-C11** | 元件库(`SkeuoPaper.uss`)注入悬空 `url("Textures/__dangling_mut__-final.png")` | **2 红**:C11 原夹具 + 同上真扫描面夹具 | `unity/Logs/mut-c11.xml` |

> ⚠️ **修复轮要旨(2026-10-05 · 单轮评审发现)**:评审实测 `Directory.GetCurrentDirectory()` 在
> Unity CLI EditMode 下 = **`<repo>/unity`(工程根)**,而原门以 `Path.Combine(cwd,"Assets",…)` 拼路径
> ⇒ 拼不中 ⇒ 扫描**静默空跑** ⇒ `Assert.IsEmpty` 假绿(**变异测试证伪不了** —— 注入物与扫描面同落泄漏目录)。
> 修复:① `DefaultRepoRoot` 上溯寻含 `Assets/` 的那层;② 三扫描函数加**反空跑守卫**(扫描面缺失/为空 ⇒ 硬报错);
> ③ `UrlTargetsTextures` 由死代码降为**诊断分级**,C10 保留 `AnyUrlRegex` 过拦。详见评审原件。

> ⚠️ **本 story 的 AC-42-C7/C8 是「断言可判 + 目视可判」双面** —— 断言证明「有引用」,
> **证明不了「贴对了」**(引用错图仍过断言)。故**截图签核为 BLOCKING 级**,按
> `.claude/docs/coding-standards.md` §Review Evidence Standards 落 `production/qa/evidence/`。
> ⇒ 该截图义务**归 019-d**(019-c 无贴图可看)。

---

## Dependencies

- Depends on: **Story 001**(元件库 + 注册表)· **五族切图与 atlas 布局冻结**(美术裁定 · 非 story)
- BLOCKED-BY(仅 019-d / 019-b): 切图冻结件 + 逐变体映射语义(019-d)· `PAGES_MAX` 值冻结(019-b) ·
  ADR-013 §6.6 假设 6 spike
- **019-c 无 BLOCKED-BY** —— 它是本 story 中唯一零外部依赖的分档
- Unlocks: **M2 形态件 ① ② 的交付**(脉案线格/明度轴 · 墨乾湿两态)—— 须 019-d 接图后方成立

---

## Completion Notes

**019-c(护栏)· Complete ✅ 2026-10-05** —— 本 story 唯一零外部依赖的分档,按「严格执行」五步协议交付:
创建 + unity cli 测试 → 双代理评审 → 修复 → 复跑绿 → 收口提交推送。

- 交付:AC-42-C10(屏幕层禁直引贴图)· AC-42-C11(悬空即硬失败)· AC-42-C7 骨架半(已注册类须有选择器块)
- 落点:`TextureBindingGates.cs`(纯逻辑,零引擎依赖,照 `PresentationDtoGuard` 先例)·
  `SkeuomorphicUiGates.cs` 门聚合 · **17 条** EditMode 夹具(原 12 + 修复轮新增 5 条反空跑/上溯)
- **修复轮(单轮评审触发)**:评审实测 cwd = `<repo>/unity` ⇒ 原门静默空跑假绿。
  修:`DefaultRepoRoot` 上溯寻 `Assets/` · 三扫描函数加反空跑守卫 · `UrlTargetsTextures` 由死代码降为诊断分级。
  重跑变异(C10/C11 各 2 红,含真扫描面锚)。原件 `review-skeuomorphic-ui-story-019c-2026-10-05.md`。
- 实测发现:注册表**仅 4 类**;图标族(`ui_icons_sprite`)无注册类;16 张 `.meta` 全 `spriteMode:0`
- 实测发现(修复轮):元件库 USS 实有 **24 个类选择器**(注册表只覆盖 4)⇒ C7 的「已注册类」范围待裁(见 §状态拆分 + 美术资产清单 §六)

**019-e(导入格式订正)· Complete ✅ 2026-10-05** —— 零裁定依赖块,同五步协议。

- 交付:**16 个 `.meta`** 三项格式订正(`spriteMode 0→1` · `textureType 0→8` · `alphaIsTransparency 0→1`)+
  两道门:`ValidateSlicedTextureImportFormat`(逐张核三项)· `ValidateSpriteBorderLeftAsSentinel`(**耦合守卫**:
  `spriteBorder` 须仍为零哨兵 —— 其值归 019-f 冻结件,019-e **不得自填**,违者即「第二真源」)
- **⚠️ Unity 重导入副产物(实测,非手改)**:三项订正触发 Unity 重导入 ⇒ 引擎**自动**补齐
  `spriteSheet.sprites`(1 条 `rect` = 全图)· `nPOTScale: 1→0` · sprite `internalID`/`spriteID` ——
  **这才是九宫格切片真正的物理前提**(仅改三项而 spriteSheet 空 ⇒ 切片不生效)。16 张逐张已验一致性。
- 绿:过滤 217/204 passed/0 failed/13 skipped · 全量 EditMode **2439/2395/0/43/1**(基线 2434 ⇒ +5 零回归)
- 变异:**MUT-E1a**(单张回落 `spriteMode:0`)⇒ 1 红(`unity/Logs/mut-e1a.xml`)· **MUT-E1b**
  (单张注入 `spriteBorder:{64,64,64,64}`)⇒ 1 红(`unity/Logs/mut-e1b.xml`)
- **单轮评审(QA 侧 BLOCKING 被实测推翻 —— 详见评审原件)**:
  QA 判「`spriteMode:1` = Multiple ⇒ 与单图九宫格矛盾」。**独立取证推翻之**:
  运行期反射 `SpriteImportMode` = **`None=0/Single=1/Multiple=2/Polygon=3`** ⇒ `spriteMode:1` **= Single**;
  引擎回读 16 张全 `mode=Single /* spriteCount=1 */`。**交付态正确**。
  ⚠️ 但暴露**真缺陷(文档级)**:原文括注 `(Multiple)` 是**误标**(自 commit `6049204` 引入,从未核过),
  **已修** story-019 + `art-assets-required-for-019`(改为「= `SpriteImportMode.Single`」+ 枚举真值)。
  取证日志 `unity/Logs/probe-enum.xml` / `probe-sprite-mode.xml`(探针已删,日志留档)。
- 原件:`production/qa/evidence/review-skeuomorphic-ui-story-019e-2026-10-05.md`
- 未闭(禁借绿):`spriteBorder` 的**值**仍待 019-f;`-unity-slice-*` 的**运行期实测**归 019-d(截图签核)

**019-d(接图)· Complete ✅ 2026-10-05** —— 同五步协议。

- 交付:C7 接图半(**落地 5 处选择器** = 四基类 + `.paper-aged` 变体;判据面 = **4 注册项**)·
  `SkeuoComponentRegistry` 增列 `IsTextureContainer`(默认 false,须显式传 true)⇒ C7 判据收窄为「贴图容器类」·
  C7 接图半入 `ValidateAll()` 聚合 · **C4 硬编码全清 44 → 0**(主题变量 layer 5 → 9 员)
- `brass-bg` 取 art-bible §4.1 权威值 `#B8863B`,订正原内联 `#b87333`(绿通道差 19,非本项目裁定值);
  `.brass-scale` 底色与 border **解耦**(评审 M4,独立命名 `--skeuo-brass-scale-color`)
- 焦点载体墨 → 铜(承 `art-bible §7.4` Amendment + GDD 规则十注记;三处实现侧注释同步)
- **夹具补齐(实测缺口)**:新增 `validate_all_aggregate_test.cs`(**7 条**)—— `ValidateAll()` 此前
  **全 `Tests/` 零调用**,C1/C2/C4/C5/C6 在 CI 长期无覆盖;`texture_binding_gate_test.cs` 两处 `null` 桩
  改真 `AssetDatabase.GUIDToAssetPath`(019-c 遗留,接图后误判悬空)
- 绿:过滤 **224/211 passed/0 failed/13 skipped** · 全量 EditMode **2446/2402/0/43/1**(基线 2445 ⇒ +1 零回归)
- 门探针:`ValidateAll()` = **0 条 VERDICT=GREEN**
- 变异:删 `.ink` 接图 ⇒ C7 精确报 `.ink`;还原 `#b87333` ⇒ C4 精确报 line 4;均还原后全绿
- ⚠️ **残余 obligation(禁借绿)**:`-unity-slice-*` 的**运行期实测 + 截图签核**仍归本 story 的
  目视半 —— 现仅断言「有引用」,证明不了「贴对了」;切片值待 019-f 冻结件
- ⚠️ **未闭色值(待裁)**:`--skeuo-brass-aged`(`#A0653A`,注释自称「铜锈」)与 art-bible §4.1/§8.6.3
  的铜锈 `#4F7A6B`(青绿)**语义冲突**;`#8C5A2B` 全文无出处 —— 经 `git show HEAD` 确证均为**既有值**,
  本轮保值抽变量未纠正,已就地加警示注释

**019-f(冻结件)/ 019-b(图集预算)· NOT-RUN** —— 分别受限切图实测 / spike,见 §状态拆分。

**本件为 2026-10-03 补立**,填「贴图绑定」这个原本**没有任何 story 覆盖**的面。
