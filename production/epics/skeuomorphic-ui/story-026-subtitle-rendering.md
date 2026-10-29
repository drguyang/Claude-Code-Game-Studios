# Story 026: 语声字幕呈现(42 元件 · AC-44-15 [L] 半的落点)

> **Epic**: 拟物 UI 框架
> **Status**: Ready
> **Layer**: Presentation
> **Type**: UI
> **Estimate**: 1-2 days
> **Manifest Version**: 2026-10-21
> **Last Updated**: 2026-10-29

## Context

**GDD**: `design/gdd/skeuomorphic-ui.md` §Edge Cases 四 **E-12b**(`:997-1000`)
· `design/accessibility-requirements.md:94-95` / `:143`

**Requirement**: `AC-44-15` 的 **`[L]` 半边** —— 「病人语声与口述**有字幕**(WCAG 1.2.2)」,
呈现归 42。**本 story 是该半边的 42 侧落点**,此前零承载。

**ADR Governing Implementation**: ADR-013(拟物 UI 框架,primary)· ADR-018 §一(44 只入不出,secondary)

**ADR Decision Summary**:
- 42 **只渲染、永不持有游戏状态**(ADR-013 §9 C3);
- 44 **不新增出向口**(ADR-018 §一 + `AC-44-15` 原文「维持只入不出」)⇒
  **字幕文本由上游发射点解析,不经 44 转发**;
- `E-12b` 逐字:**「42 不持有语声状态、不判何时显示」** —— 语义 / 时机归 44,42 只渲染文本。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: MEDIUM

**Engine Notes**: 本 story 只做 UI Toolkit 文本呈现(`Label` + USS),**不触**焦点桥 / 自定义材质 /
图集 —— ADR-013 的 HIGH 风险面**不在此**。`skeuomorphic-ui.md` 已登记「文本呈现走元件层文本组件
(F2 文本贴合 + USS 主题变量)」。

**Control Manifest Rules (this layer)**:
- Required: 字幕 = 文本呈现,走元件层文本组件;`textScale` 主题变量层(不写死字号)
- Forbidden: 42 持有语声状态 / 判定何时显示;`disease_id` 进呈现层;内联变体
- Guardrail: 字幕开关**默认开**(`accessibility-requirements.md:94`)

---

## 为什么新立本 story(2026-10-29)

`OQ-42-8` 已于 2026-10-06 裁定:42 侧**四钩子 = 四处接口**,**具体要求归 49(P2)**。
本 story 是用户 2026-10-29 **明示裁定「把缺口 C 提前到现在做」**的产物 ——
即:在 49 未成文之前,先把 42 侧的**实际呈现元件**做出来,不等 P2。

⚠️ **这改变了 `OQ-42-8` 的分工边界**(原「42 只留接口,实现归 49」)。
**改动是明示的,不是悄悄扩大 42 的范围** —— 见下方 §与 OQ-42-8 的关系。

---

## 与 `OQ-42-8` 的关系(必须写清,防两处口径分叉)

| 项 | 原裁(2026-10-06) | 本 story(2026-10-29 用户裁定) |
|---|---|---|
| 42 侧交付 | **四处接口**(命名 / 文本缩放 / 焦点样式 / 动效缩放) | 四接口 **+ 字幕呈现元件** |
| 字幕的**呈现规格**(位置 / 时长 / 换行 / 字号) | 归 49(P2) | **本 story 给出 P0 最小规格**(见 §Acceptance Criteria ③) |
| 字幕的**语义 / 时机**(说什么、何时说) | 归 44 | **仍归 44**(不变) |

> ⚠️ **本 story 不解除 49 的 P2 义务** —— 49 仍可细化 / 改写呈现规格。
> 本 story 交付的是「**P0 可用的最小呈现**」,不是「最终形态」。

---

## Acceptance Criteria

- [ ] **AC-1 元件存在且可渲染**:42 元件库新增一个字幕元件(`SubtitleLine`),
      能显示一行文本;走 `Label` + USS 主题变量(**字号不写死**)。

- [ ] **AC-2 说话人识别**:`accessibility-requirements.md:95` 要求「名字前置」。
      元件接受 `(speakerName, text)` 并在文本前呈现说话人;**颜色区分仅作辅助**
      (须同时有非颜色差异 —— 承色-only 铁律)。

- [ ] **AC-3 P0 最小呈现规格**(本 story 自定,49 可细化):
      - **位置**:屏幕底部安全区内,水平居中
      - **行数**:至多 2 行,超出以省略号截断
      - **显示时长**:由调用方给(42 **不判**时长 —— 见 AC-5)
      - **字号**:跟随 `textScale` 主题变量

- [ ] **AC-4 默认开**:字幕开关的出厂默认 = **开**(`accessibility-requirements.md:94` 逐字)。
      该开关**不新增注册表行** —— 它是 42 侧的**呈现偏好**,与 44 的 `settings_visible`
      8 行**无交集**(44 的注册表是**音频参数**,字幕是**文本呈现**)。

- [ ] **AC-5 42 不持有状态、不判时机**:元件只有 `Show(speaker, text)` / `Hide()`
      两个入口;**没有**「多久后自动隐藏」的逻辑(那是时机,归 44)。
      判据 = **反射断言**:元件类型不含 `Timer` / `duration` / `autoHide` 类成员。

- [ ] **AC-6 `disease_id` 不进呈现层**:字幕文本来自事件表 `subtitle_text`,
      不含任何病种标识。判据 = `PresentationDtoGuard` 递归扫描覆盖(承 `AC-37-15`)。

---

## 与缺口 B 的关系(本 story **不做** 上游接线)

`AC-44-15` 的 `[A]` 半边(「文本真的送到 42」)是**上游发射点**的义务,
**不在本 story 面内**。本 story 只交付**接收端元件 + 一个接收口**:

```csharp
// 42 侧唯一接收口(上游发射点调它;42 不反问「该不该显示」)
public interface ISubtitleSink { void Show(in SubtitleLineData line); void Hide(); }
```

⚠️ **上游谁调它、何时调** —— 归 44 / 48 的接线轮(缺口 B)。本 story 交付「有地方可送」。

---

## Test Evidence

**Story Type**: UI → 需要 `production/qa/evidence/` 的走查记录 + 元件级 EditMode 夹具。

| AC | 载体 |
|---|---|
| AC-1 / AC-3 / AC-4 | EditMode:元件构造 + USS 变量解析 + 默认开 |
| AC-2 | EditMode:说话人前置 + 非颜色差异存在 |
| AC-5 | **反射断言**:元件类型无 `Timer`/`duration`/`autoHide` 成员 |
| AC-6 | `PresentationDtoGuard` 递归扫描 |
| 整体呈现 | 人工走查截图 → `production/qa/evidence/story-026-subtitle-walkthrough-<date>.md` |

---

## Out of Scope

- 上游发射点的接线(缺口 B,归 44 / 48)
- 字幕的**最终**呈现规格(49 的 P2 可细化)
- 字幕的本地化 / 翻译管线(归 49 / 本地化轮)
- 语音通道(ADR-018 §五 明写不做)

---

## Dependencies

- **上游**:无(本 story 只交付接收端)
- **下游**:缺口 B 的上游接线轮(它调 `ISubtitleSink`)
- **相关**:`AC-44-15`(`[A]` 半在 44 侧已落,`[L]` 半由本 story + 49 承接)
