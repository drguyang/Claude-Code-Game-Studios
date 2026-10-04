# Story 019: 贴图接入(16 张 `*-final.png` → USS 元件族 · 九宫格 slice 对齐真实切图 · 图集页数实测)

> **Epic**: 拟物 UI 框架
> **Status**: **部分完成** —— **019-c `Complete ✅`(2026-10-05)** · 019-a/d/b 见下 §状态拆分
> **Layer**: Foundation
> **Type**: UI
> **Estimate**: 待估(依赖五族切图冻结)
> **Manifest Version**: 2026-10-03
> **Last Updated**: 2026-10-05

### ⚠️ 状态拆分(2026-10-04 J 裁 + 2026-10-05 三档再裁)

**原状态 `Ready` 与文件自身登记矛盾** —— 本 story §Dependencies 挂着两件 **BLOCKED-BY**
(ADR-013 §6.6 假设 6 spike 未跑 · `PAGES_MAX` 未冻结),却标 `Ready`。技术美术实测指出:
其 5 条 AC 中 **AC-42-C9 现在根本无法判**(它要求「页数 ≤ `PAGES_MAX`」,而 `PAGES_MAX` 未冻结)。

**2026-10-04 J 初裁:拆 a/b 两半** ——

| 半 | AC | 可做性 | 状态 |
|---|---|---|---|
| **019-a · 接图** | AC-42-C7 / C8 / C10 / C11 | ✅ 现在可做(只依赖五族切图冻结件,不依赖 spike) | 待冻结点亮 |
| **019-b · 图集预算** | AC-42-C9(页数 ≤ `PAGES_MAX`) | ⛔ 结构性 NOT-RUN | **Blocked**(禁借绿) |

> ⚠️ **019-b 不是「没跑」,是「不可判」** —— 它与「没跑」在登记上须区分(承 `coding-standards.md`
> §测试证据「NOT-RUN vs 未做」纪律)。

**2026-10-05 逐 AC 实测再裁 —— J 的 a/b 二分不够细,按「卡什么」重划三档**:

实测发现 019-a 的 4 条 AC 卡点**各不相同**,其中两条**零外部依赖**:

| 分档 | AC | 卡什么(2026-10-05 实测) | 状态 |
|---|---|---|---|
| **019-c · 护栏** | **C10** + **C11** + C7(骨架半) | **零外部依赖** —— 屏幕层禁引 / 悬空即红,均为**纯 lint 断言** | ✅ **Complete 2026-10-05** |
| **019-d · 接图** | C7(接图半) + **C8** + 导入格式 | **美术** —— 需五族切图冻结件 + 逐变体映射语义;16 张 `.meta` 全 `spriteMode:0` | 待冻结点亮 |
| **019-b · 图集预算** | C9 | **spike** —— `PAGES_MAX` 未冻结(非美术) | **Blocked** |

> **019-c 的实测依据**:① 7 个屏幕 UXML 全部只引类名,`url(` 命中 **0** ⇒ C10 可立即锁死;
> ② 16 张图 GUID 均在,可解析 ⇒ C11 可立即实现;③ 注册表实测仅 **4 类**(纸/卷轴/墨/印章),
> 图标族(`ui_icons_sprite`)**无注册类**。
>
> ⇒ **019-c 不受冻结件阻塞,不该陪 C8 一起等** —— 它现在就把「皮未贴」这个失效模式
> 变成**构建失败**(C11 悬空即红 + C10 屏幕层不许绕开元件库),正是本 story 的存在理由。

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
> ⚠️ 「铜」族**零实物**,其最小切片已另由 **2026-10-04 E 裁**升为 M2 硬前置
> (`milestones/README.md` §三 Exit Criteria 第 5 条括注)。

---

## Acceptance Criteria

*承 EPIC.md §范围边界声明;GDD `design/gdd/skeuomorphic-ui.md` AC-42-C3 的图集半边 + 第 ② 半 TR。*

- [ ] **AC-42-C7(新)**: 每个**已注册元件类**(`SkeuoComponentRegistry` 登记的全部种类与变体)对应的 USS 规则
      含 `background-image: url(...)`,指向**真实贴图资产**;纯色填充模拟贴图 ⇒ 构建期/lint 报冲突
      ⇒ **骨架半 ✅(019-c,2026-10-05)**:`ValidateRegisteredClassesHaveSelectorBlock` + 4 类注册表实测;
      **接图半 ⬜ 归 019-d**(须逐变体映射语义 + 冻结件)。⚠️ 接图半现**必红**(零引用),
      故 `ValidateAll` **不聚合**该半(恒红门 = 噪声),由 `TextureBindingGates.ValidateRegisteredClassesHaveTexture` 承接。
      ⚠️ **「已注册元件类」范围待裁(2026-10-05 评审放大)**:元件库 USS 实有 **24 个类选择器**,
      而注册表仅 4 类(记号族 6 / 黄铜族 3 / 器具族 3 / 焦点族 2 等住 `MarkRegistry` 或**零登记表**)。
      **窄读法(4 类)= 019-c 现采**;**宽读法(24 类)** 会把 `.brass`/`.implement`(零实物)拉入。**决定 019-d 接图量,主会话不自裁。**
- [ ] **AC-42-C8(新)** ⛔ **归 019-d · 受限美术**: 九宫格 `-unity-slice-left/right/top/bottom` 值与该元件图集内的**实际切图边界**一致
      (值来自切图冻结件的元数据,不得手填)
      ⇒ ⚠️ **实测 2026-10-05**:`design/assets/specs/` **零九宫格边界元数据**;`SkeuoPaper.uss` 的 64px 为**人工实测填**
      = 本 AC 的**教科书反例**。且 16 张 `.meta` 全 `spriteMode:0`(九宫格在此模式下**不可能工作**)
      ⇒ 须先作导入格式订正 + 冻结件落盘。**禁借绿。**
- [ ] **AC-42-C9(新)** ⛔ **归 019-b · Blocked**: 实测**图集页数 ≤ `PAGES_MAX`**;超限 => 构建期冲突并给出溢出告警
      (兑现 `TR-skeuoui-011` 第 ② 半;`PAGES_MAX` 值待与图集布局同批冻结)
      ⇒ ⚠️ **2026-10-04 J:本条现不可判** —— `PAGES_MAX` 未冻结,填占位值 = 第二真源。
      **须待 ADR-013 §6.6 假设 6 spike 落定后方可勾。禁借绿。**
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
- Given: `SkeuoComponentRegistry` 全部登记项 + 元件库 USS
- When: 构建期断言 + lint 扫描
- Then: 每个登记类都有 `background-image: url(...)` 指向真实资产
- Edge cases: 纯色模拟贴图(非法);有类无图(非法);有图未注册(非法)

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

**019-d(接图)/ 019-b(图集预算)· NOT-RUN** —— 分别受限美术冻结件与 spike,见 §状态拆分。

**本件为 2026-10-03 补立**,填「贴图绑定」这个原本**没有任何 story 覆盖**的面。
