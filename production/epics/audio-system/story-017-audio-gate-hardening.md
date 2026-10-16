# Story 017: 音频门加固(组判据收紧 · 跨平台大小写 · ValidateEntries 拆分)

> **Epic**: 音频系统
> **Status**: Ready
> **Layer**: Foundation(加固既有门/工具,**不新增功能**)
> **Type**: Integration
> **Estimate**: 待估(小 —— 三处均为既有方法内的局部改动)
> **Manifest Version**: 2026-10-15
> **Last Updated**: 2026-10-17

## Context

**GDD**: `design/gdd/audio-system.md`
**Requirement**: TR-audio-010 的**门侧加固半边**(素材本体半边归 story-015 / 016)

**ADR Governing Implementation**: ADR-018 §五 · ADR-014 §三/§五 · ADR-022 §一
**ADR Decision Summary**: 素材与 `data-core` 组区分;阶段 2 谓词聚合 `throw`;Tooling 不进构建

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW(纯 C# 谓词与 BCL 文件枚举,**不触 post-cutoff API**)
**Engine Notes**: 本 story **不引入新 API 面** —— 只在既有 `AudioAssetGroupGates` / `AudioAssetExistence` 内做局部加固。

### ⚠️ 本 story 的存在理由(2026-10-17 `/code-review` 产出)

story-015 的收口轮代码评审(`production/qa/evidence/review-s15-code-2026-10-17.md`)
判 **APPROVED WITH SUGGESTIONS**,产出 7 条 WARNING。其中两条落在 **story-010 的既有门代码**上
(非 story-015 引入),用户 2026-10-17 裁定**新立加固件**承接:

| 评审条目 | 判定 | 出处 |
|---|---|---|
| **W-2** | `Directory.GetFiles(dir, "*.wav")` **大小写敏感** —— Linux(CI 平台,ADR-012)下 `.WAV` 漏扫 ⇒ 盘上有却判「不存在」,**跨平台行为分叉** | `AudioAssetGroupGates.cs:197` |
| **W-3** | `ValidateEntries` **45 行 / 圈复杂度 15** —— 双超标(标准 = ≤40 行 / <10) | `AudioAssetGroupGates.cs:76-120` |

> **同源但不在本件面内**(用户 2026-10-17 裁定归 **`TD-audio-01`**):W-4(组判据弱于 AC 语义)·
> W-5(`EnsureAudioGroup` 不完备)· W-7(`CreateGroup` 全新 clone 路径未验)。
> **W-1 / W-6**(`music_*` 的 `loadType` 判据张力)**不改** —— GDD §OQ-44-2 附已覆盖。
> ⚠️ **W-4 与 W-3 同处 `ValidateEntries`**:本件只动**结构与行数**(W-3),
> **不动组归属判据的语义**(W-4)—— 后者归 `TD-audio-01`,避免同一方法被两条工作流同时改。

---

## Acceptance Criteria

- [ ] **AC-44-D5b(新)**: `AudioAssetGroupGates.AudioAssetsOnDisk()` 的扩展名过滤**跨平台一致** ——
      Linux 与 Windows 下对同一目录返回**同一集合**;`.WAV` / `.Wav` / `.wav` 三形态**均被识别**
      (判别力:停用修复 ⇒ 大小写混写夹具转红)
- [ ] **AC-44-D5c(新)**: `AudioAssetGroupGates.ValidateEntries` 满足项目编码标准 ——
      **≤ 40 行**(不含数据声明)· **圈复杂度 < 10**;拆分后**全部既有 D5 夹具逐条仍绿**
      (判别力:拆分若改变行为,`test_d5_pure_*` 四条须有一条转红)
- [ ] **AC-44-D5d(新)**: 加固**不改变门的判定结果** —— 对同一输入,加固前后
      `ValidateEntries` 的**返回错误集逐字相同**(纯重构,非语义变更)

> **证据原件**:`production/qa/evidence/impl-s17-gate-hardening-<日期>.md`(实现轮落盘)

---

## Implementation Notes

*Derived from `review-s15-code-2026-10-17.md` §三 3.1 W-2 / W-3:*

1. **W-2 修法**:把 `Directory.GetFiles(dir, "*.wav")` 的 pattern 过滤改为
   **先取全量再按扩展名判**(`Path.GetExtension(p)` + `StringComparison.OrdinalIgnoreCase`),
   或显式枚举三形态。⚠️ **不得**只改成 `"*.WAV"` —— 那只是把分叉挪到另一边。
2. **W-3 修法**:`ValidateEntries` 拆为两个私有方法(建议按 (a) 期望集归属 / (b) `data-core` 纯洁性
   两半拆 —— 正好对应方法内已有的两段注释),主方法退化为编排。⚠️ **拆分不得改变判定语义**
   (AC-44-D5d 钉死)。
3. **夹具**:本件须**新增**大小写混写夹具(`.WAV` 副本 ⇒ 应被识别)+ 重构等价性夹具
   (同一输入 ⇒ 错误集逐字相同)。既有 14 条 D5/D6 测**不得删改**。
4. **证伪轮(BLOCKING)**:两条新 AC 各须一条反证 —— 停用 W-2 修复 ⇒ 大小写夹具红;
   停用 W-3 拆分(还原为长方法)⇒ **不红**(行数/CC 无夹具钉),故 AC-44-D5c 的
   **判别力载体须显式设计**(建议:断言私有方法存在 + 主方法行数,或引入行数守卫测试)。
   ⚠️ 若 AC-44-D5c 无法造出有判别力的夹具,**须如实登记为「标准类无界检查项」**,
   不得标「已证」(承 `review-workflow.md` Phase 8b 双列纪律)。
5. **`ValidateEntries` 住 `Editor.Tools.Gates`** ⇒ 改它须复核 `Editor.Tools.Gates.asmdef`
   的 `includePlatforms: ["Editor"]` 不变(ADR-022 §一)。

---

## Out of Scope

*Handled by neighbouring stories / 债册 — do not implement here:*

- **W-4 / W-5 / W-7**:组判据语义收紧 · `EnsureAudioGroup` 自举 · `CreateGroup` 全新 clone 路径
  —— 归 **`TD-audio-01`**(用户 2026-10-17 裁定)
- **W-1 / W-6**:`music_*` 的 `loadType` 判据张力 —— **不改**(GDD §OQ-44-2 附已覆盖)
- **story-015 / 016 的素材面**:本件只动门代码,**不碰任何 `.wav` / `.meta` / 事件表**
- **组定义入库(笔 1 收窄 `.gitignore`)**:已于 2026-10-17 随本裁定落地,不属本 story

---

## QA Test Cases

*Written by qa-lead at story creation. The developer implements against these — do not invent new test cases during implementation.*

**AC-44-D5b**:
- Given: `unity/Assets/Audio/` 内存在 `.wav` 与 `.WAV` 两种大小写副本
- When: `AudioAssetsOnDisk()`
- Then: 两者**均被返回**
- Edge cases: 同名不同大小写共存;无扩展名文件;`.wav` 大小写混写(`.Wav`)

**AC-44-D5c**:
- Given: `AudioAssetGroupGates.ValidateEntries` 源码
- When: 行数 / 圈复杂度实测
- Then: ≤ 40 行 ∧ CC < 10
- Edge cases: 拆分后主方法只剩编排;私有方法各自 ≤ 40 行

**AC-44-D5d**:
- Given: 一组固定输入(混合:纯音频在 `data-core` / 纯音频未登记 / 干净集 / null 集)
- When: 加固前后各跑一次 `ValidateEntries`
- Then: 返回的错误集**逐字相同**(顺序 + 内容)
- Edge cases: null 输入;空集;同时命中 (a) 与 (b) 两半

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- `production/qa/evidence/impl-s17-gate-hardening-<日期>.md`(含**加固前后错误集对照**)
- 测试扩展:`unity/Assets/Tests/EditMode/Audio/pipeline_gate_test.cs`(既有 52 测 **不得删改**,
  本件**追加**)
  · 账本并列:`tests/integration/audio_system/README.md`

**Status**: NOT-RUN(本件为 2026-10-17 新立,**尚未实现** —— 禁借绿)
**Test File**: `unity/Assets/Tests/EditMode/Audio/pipeline_gate_test.cs`(追加,当前 52 测)

> ⚠️ **AC-44-D5c 是「标准类无界检查项」的高风险面** —— 行数 / CC 无天然夹具。
> 实现轮须显式设计判别力载体,否则按 Phase 8b 纪律标 `Landed=✅ / Proven=✗`。

---

## Dependencies

- Depends on: **Story 010**(烘焙管线与门机制 —— 本件改的就是它产出的门)
- Depends on: **story-015**(评审产出的 W-2/W-3 即本件输入)
- BLOCKED-BY: **无** —— 本件**不依赖素材产出批次**,可在素材接入前独立完成
- Unlocks: 无(story-010 的代码质量面收敛)

---

## Completion Notes

*尚未实现 —— 本节留待实现轮填写。*
