# Story 007: 落笔写流与 7a checkpoint 锚点

> **Epic**: 脉案
> **Status**: **Ready ⬜**(2026-11-02 由 `story-006` 拆分而来;内容原样转移,**未开工**)
> **Layer**: Feature
> **Type**: Integration
> **Estimate**: 3h(原整卡 4h,相机半已留在 story-006)
> **Manifest Version**: 2026-11-02
> **Last Updated**: 2026-11-02

> **本卡的由来**:`story-006` 原捆了「相机意图 + 落笔写流 + checkpoint 锚点」三件事。
> 相机半与后两件**无共享判据**,且后两件今日仍是空的(`SaveAnchor` 全仓零命中)
> ⇒ 捆着会让 006 永远无法 `Complete`。2026-11-02 拆分,本卡承接后两件。
>
> ⚠️ **不是新需求** —— 以下 AC 与测试规格逐条来自原 `story-006`,`git log --follow
> production/epics/casebook/story-006-camera-mode-intent.md` 可取证。原编号保留在标题文本里。

---

## Context

**GDD**: `design/gdd/casebook.md`(规则六 落笔与存档 · 边界表「联机落笔 = 判定输入」)
**Requirement**: **`TR-casebook-004`**(联机落笔 `Seq` 由主机发号 —— **gap**,
随 ADR-001 窄修订 + 45 GDD 轮,P1b 前;本卡运行面 **BLOCKED-BY-45**,禁借绿)

**ADR Governing Implementation**:
- **ADR-010 §六**(主):7a 的**三个 checkpoint 锚点** —— 落笔 = 锚点 ①
- **ADR-011 Amendment B**(§一之三 裁决一):判定输入类走**可靠通道**(联机形态)
- **ADR-008**:病例流 `JudgmentRecorded` / `JudgmentRevised` 的 `Seq` 由主机 `Append` 发号

**现状(2026-11-02 勘察)**:

| 事 | 现状 |
|---|---|
| 落笔写流(`TryRecord` / `TryRevise`) | **已实现** —— `CasebookCommandRouter.SubmitDiseaseName` / `SubmitConfidence` 直接调 `JudgmentWriter` |
| `SaveAnchor` 枚举 | **不存在** —— `grep -rn "enum SaveAnchor" unity/Assets` 零命中 |
| `CasebookWritten` 锚点 | **零命中** —— 全仓无任何地方触发它 |
| 7a 锚点接收面 | 未勘察到(实现轮须先确认 7a 侧有没有接收入口) |

⇒ 本卡**真正待做的**是 checkpoint 锚点这一条,以及把已实现的写流纳入判据面。

---

## Acceptance Criteria

- [ ] **AC-7-01**:落笔提交 = 一条 `JudgmentRecorded`(病例流,`author_player_id` 必填)经
      `IEventSink.Append`,单机 P0 路径本地直写;39 **不自造 `Seq`**(发号 = 主机 Append 时)
- [ ] **AC-7-02**:写成功后**同帧内**触发 7a 锚点 ① checkpoint(锚点枚举与触发接口本卡新立)
- [ ] **AC-7-03**:联机落笔形态:意图上行接口**形状就位**(客户端聚合 → 主机 `Judge` / `Append` / 发 `Seq`),
      运行面 `BLOCKED-BY-45`(45 无 GDD),**禁借绿**
- [ ] **AC-7-04**:同内容重交 = 两条合法事件(39 去重**不是**义务 —— 幂等归判定链语义)
- [ ] **AC-7-05**:写失败(磁盘满)⇒ UI 报错**不吞笔**

---

## Implementation Notes

1. **锚点接口形状待定**:是先立 `enum SaveAnchor { CasebookWritten, … }` 还是直接给
   7a 一个具名方法,须先看 7a 侧现状(`persistence-service.md` / `SaveService`)。
   ⚠️ **不得**在本卡顺手改 7a 的写盘线程 / flush 时机 —— 39 只报锚。
2. **写流已实现** ⇒ 本卡的 AC-7-01 / 04 / 05 属于「补判据」而非「补实现」;
   实现轮须先核实既有 `casebook_*` 夹具是否已覆盖,避免重复造。
3. **联机形状**(不实现):接口进只契约件,实现体标 `BLOCKED-BY-45`。

---

## Out of Scope

- **相机档位意图** —— 留在 `story-006`
- **落笔区 UI 与焦点路径** —— `story-005`
- **`J(c,p)` / 痕投影折叠** —— `story-003`
- **出诊启动锚点 ③ / 病例结案锚点 ② 的触发方** —— 归 1 / 37 的 epic;39 只认领锚点 ①

---

## Test Evidence

**Story Type**: Integration
**Required evidence**: `unity/Assets/Tests/PlayMode/Casebook/casebook_camera_pen_hook_test.cs`
—— ⚠️ **文件名沿用原卡的写法**,但本卡已不含相机半;实现轮须改名为
`casebook_writing_anchor_test.cs` 并在改名前登记。**未创建**。

---

## Dependencies

- Depends on:`story-001`(分册段)· `story-003`(投影刷新)· `story-005`(提交 UI)·
  7a 锚点接口(**本卡新立**,当前不存在)
- Blocked by:**45**(联机形态,`AC-7-03`)

---

## Completion Notes

*(empty — fill at story completion via `/story-done`)*
