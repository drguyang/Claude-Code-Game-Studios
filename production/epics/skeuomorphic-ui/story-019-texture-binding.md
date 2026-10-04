# Story 019: 贴图接入(16 张 `*-final.png` → USS 元件族 · 九宫格 slice 对齐真实切图 · 图集页数实测)

> **Epic**: 拟物 UI 框架
> **Status**: **Blocked ⛔**(2026-10-04 J 拆分 —— 原 `Ready`,见下 §状态拆分)
> **Layer**: Foundation
> **Type**: UI
> **Estimate**: 待估(依赖五族切图冻结)
> **Manifest Version**: 2026-10-03
> **Last Updated**: 2026-10-04

### ⚠️ 状态拆分(2026-10-04 用户裁定 J)

**原状态 `Ready` 与文件自身登记矛盾** —— 本 story §Dependencies 挂着两件 **BLOCKED-BY**
(ADR-013 §6.6 假设 6 spike 未跑 · `PAGES_MAX` 未冻结),却标 `Ready`。技术美术实测指出:
其 5 条 AC 中 **AC-42-C9 现在根本无法判**(它要求「页数 ≤ `PAGES_MAX`」,而 `PAGES_MAX` 未冻结)。

**裁定:拆为两半** ——

| 半 | AC | 可做性 | 状态 |
|---|---|---|---|
| **019-a · 接图** | AC-42-C7 / C8 / C10 / C11 | ✅ **现在可做**(只依赖五族切图冻结件,不依赖 spike) | 待冻结点亮 |
| **019-b · 图集预算** | AC-42-C9(页数 ≤ `PAGES_MAX`) | ⛔ **结构性 NOT-RUN** —— `PAGES_MAX` 冻结依赖 ADR-013 §6.6 假设 6 spike | **Blocked**(禁借绿) |

> ⚠️ **019-b 不是「没跑」,是「不可判」** —— 它与「没跑」在登记上须区分(承 `coding-standards.md`
> §测试证据「NOT-RUN vs 未做」纪律)。
>
> ⚠️ **019 与 `interaction-system` 的排序(2026-10-04 I 裁)**:**先 `interaction`,`019` 后置**
> —— `019` 不做 ⇒ M2 少一条 Exit Criteria;`interaction` 不做 ⇒ **整条判断链不通**,M2 判据直接不成立。
> **这是判据依赖,不是优先级偏好。**

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
- [ ] **AC-42-C8(新)**: 九宫格 `-unity-slice-left/right/top/bottom` 值与该元件图集内的**实际切图边界**一致
      (值来自切图冻结件的元数据,不得手填)
- [ ] **AC-42-C9(新)** ⛔ **归 019-b · Blocked**: 实测**图集页数 ≤ `PAGES_MAX`**;超限 => 构建期冲突并给出溢出告警
      (兑现 `TR-skeuoui-011` 第 ② 半;`PAGES_MAX` 值待与图集布局同批冻结)
      ⇒ ⚠️ **2026-10-04 J:本条现不可判** —— `PAGES_MAX` 未冻结,填占位值 = 第二真源。
      **须待 ADR-013 §6.6 假设 6 spike 落定后方可勾。禁借绿。**
- [ ] **AC-42-C10(新)**: 贴图**只经元件库接入** —— 屏幕级 UXML/USS(脉案 / 存档位 / 库存 / 设置 / 教学 / 调试视图)
      内**不得**出现指向 `Textures/` 的 `url()`;违者构建期报冲突(与「元件库唯一出口」同源)
- [ ] **AC-42-C11(新)**: 贴图缺失 / GUID 悬空 ⇒ **构建期硬失败**(非运行期 fallback 到纯色 ——
      静默降级会让「皮未贴」再次不可见,正是本 story 要消灭的失效模式)

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

**Status**: [ ] NOT-RUN
**Test File**: TBD(待实现)

> ⚠️ **本 story 的 AC-42-C7/C8 是「断言可判 + 目视可判」双面** —— 断言证明「有引用」,
> **证明不了「贴对了」**(引用错图仍过断言)。故**截图签核为 BLOCKING 级**,按
> `.claude/docs/coding-standards.md` §Review Evidence Standards 落 `production/qa/evidence/`。

---

## Dependencies

- Depends on: **Story 001**(元件库 + 注册表)· **三族切图与 atlas 布局冻结**(美术裁定 · 非 story)
- BLOCKED-BY: `PAGES_MAX` 值冻结(AC-42-C9 的可判前提)· ADR-013 §6.6 假设 6 spike(UI Toolkit 图集行为)
- Unlocks: **M2 形态件 ① ② 的交付**(脉案线格/明度轴 · 墨乾湿两态)

---

## Completion Notes

(未开工 —— 本件为 2026-10-03 补立,填「贴图绑定」这个原本**没有任何 story 覆盖**的面。)
