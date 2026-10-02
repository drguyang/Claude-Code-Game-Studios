# Story 005: SNR 分析域与噪声下界

> **Epic**: 音频系统
> **Status**: Complete
> **Layer**: Foundation(系统分类;实现落表现层 L5)
> **Type**: Logic
> **Estimate**: 3h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-09-27

## Context

**GDD**: `design/gdd/audio-system.md`(F-44.2 · 规则六)
**Requirement**: TR-audio-005(接触噪声/信噪比是暴露参数;`READ_FLOOR_MIN > 0` 音频侧对应实现)

**ADR Governing Implementation**: ADR-018: 音频架构(主:§四 需求③ —— `Stethoscope` 总线暴露接触噪声底与信噪比)
**ADR Decision Summary**: 背景噪声永不为零(禁静音路径);数值**用户自己调**,44 只交付形状与断言。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: MEDIUM(`OQ-44-5` AudioMixer 6.3 悬置)
**Engine Notes**: 本 story 的核心是**纯函数 + 构建期数据断言**,引擎面窄;暴露参数枚举走 Force Text YAML 扫描。

**Control Manifest Rules (this layer)**:
- Required: `ComputeSnrDb` 纯函数按 F-44.2(dB 线性化相减,钳位 ±24 dB)· 测试自带常量夹具(不取待调值)· 暴露参数 ∈ 注册表
- Forbidden: dB/dB 直除(首轮量纲事故,已废)·「dB ≠ 0」当噪声非零判据(语义反)· 断言取「待调」TierMap 值
- Guardrail: 分析域与渲染域分离 —— 构建期断言配置,运行期 gain_scale 不回写分析值

---

## Acceptance Criteria

*From GDD `design/gdd/audio-system.md`, scoped to this story:*

- [x] **AC-44-05**:构建期校验 —— ① 三输入直取 `tier_map` 列(`signal_db`/`noise_floor_db`/`contact_noise_floor_db`),`ComputeSnrDb` 按 F-44.2 计算,**测试自带常量夹具**(边界 −24/+24 恰含、±200 溢出钳位),`∀T: ComputeSnrDb(行) ∈ [−24,+24] dB`,超界 = 配置拒绝;② 三列 ∈ `Stethoscope` mixer 组暴露参数闭集(YAML 枚举断言)。
  **② 的载体已于 2026-09-27 独立成 Story 014**(readiness 轮用户裁定)—— 现状 `.mixer` 25 个 effect 全 `Attenuation`、**零 filter effect**,而 `noise_floor_db`/`contact_noise_floor_db` **无任何组参数承载**(`bus_volume_stethoscope` 已暴露,是 7 名之一)⇒ ② 的「闭集」当前最多含 1 员。**本 story 的 ② 改为前置引用**:断言「载体已就绪」(Story 014 交付后自动满足),不重复实现。
- [x] **AC-44-06**:`noise_floor_db`/`contact_noise_db` 均**有限且 ≥ `NOISE_FLOOR_DB_MIN`**;`∀T: ComputeSnrDb ≥ −24 dB`(「不毁掉信号」的量化形态);与 8 侧 `READ_FLOOR_MIN > 0` 方向对齐断言(或注明归 8 轮)。

---

## Implementation Notes

*Derived from ADR-018 §四 + GDD F-44.2(2026-09-18 量纲修复 + 2026-09-25 双域合成规则)*:

- `ComputeSnrDb(signal_db, noise_db, contact_db)`:线性化 `10·log10(10^(n/10)+10^(c/10))` 相减,分母真数恒正(边界已验,首轮除零形态不复现);钳位在**派生值**上。
- **分析/渲染双域**(GDD F-44.2 末注):本 story 只实现**分析域**(构建期门);F-44.6 `gain_scale` 的渲染乘子归 Story 006,两域不得互写。
- 夹具 = EditMode 测试内**自带常量**(±24 恰含边界、±200 溢出),不依赖 `tier_map` 待调值 —— 承 qa 具体化判据。
- `NOISE_FLOOR_DB_MIN` = 登记常量(进入 `tier_map` 校验谓词;值域归用户)。

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 006: F-44.6 `gain_scale`(渲染域乘子)与 Intensity 分桶
- Story 004: 档位听感可辨([L])—— 本 story 只要数据/计算面

## QA Test Cases

*Written at story creation(lean mode — QL-STORY-READY skipped;specs self-authored from AC text). The developer implements against these — do not invent new test cases during implementation.*

- **AC-44-05**: 纯函数夹具 + 配置门。Given: `ComputeSnrDb` + 常量夹具集。
  - When: 跑夹具。
  - Then: (0,0,0)⇒0dB;边界(使输出恰 ±24)不被错钳;±200 输入 ⇒ 钳到 ±24;非法 NaN 输入 ⇒ 拒;`tier_map` 含一行使 SNR=+30 ⇒ **配置拒绝**(门红);暴露参数扫描三列 ∈ Stethoscope 闭集。
  - Edge cases: 两噪声同为最小负 dB ⇒ 分母最小但 >0(无除零);`signal_db` 缺列 ⇒ schema 门(002)红,不重复拦。
  - **② 的 mixer 暴露断言**:前置引用 Story 014(2026-09-27 立)—— 载体未就绪时本条**不判红**,只记「载体未就绪」;Story 014 交付后自动满足。
- **AC-44-06**: 下界断言。Given: `tier_map` 全行。When: 读两噪声列。Then: 均有限 ≥ `NOISE_FLOOR_DB_MIN`;`ComputeSnr ≥ −24`;负向:写 `-200` ⇒ 红;与 8 的 `READ_FLOOR_MIN` 方向对齐(双正断言或显式转 8 轮注记)。

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- 真身 `unity/Assets/Tests/EditMode/Audio/snr_analysis_test.cs` — must exist and pass
  (登记路径原写仓库根 `tests/...`,Unity 只编译 `unity/Assets/` 树 —— 承 Story 002/003/004 先例,真身落 `unity/Assets/`)
- 账本互链 `tests/unit/audio_system/README.md`(AC→测映射)

**Status**: [x] Created — 真身 `unity/Assets/Tests/EditMode/Audio/snr_analysis_test.cs`(**21 测全过**)+ 账本互链 `tests/unit/audio_system/README.md` §Story 005
**Evidence**: 过滤 21/21 · 全量 EditMode 905/905 Passed(2026-09-27)

---

## Dependencies

- Depends on: Story 001 · 002(`tier_map` 载体与六列集)
- Unlocks: Story 013(听测前的数据面就绪)
- **Blocked by: Story 014**(2026-09-27 立)—— AC-44-05 ② 的 mixer 暴露载体(给组加 filter effect + 暴露参数);未交付前 ② 只作前置引用,不判红

## Completion Notes
**Completed**: 2026-09-27
**Criteria**: 2/2 passing(AC-44-05 · AC-44-06;无 deferred,0 UNTESTED)
**Deviations**(均 ADVISORY):
1. **AC-44-05 ② 的载体独立成 Story 014**(2026-09-27 readiness 轮用户裁定)—— 现状 `.mixer` 25 个 effect 全 `Attenuation`、**零 filter effect**,而 `noise_floor_db`/`contact_noise_floor_db` **无任何组参数承载**(`bus_volume_stethoscope` 已暴露,是 7 名之一)⇒ ② 的「闭集」当前最多含 1 员。**本 story 的 ② 改为前置引用**:断言「载体已就绪」(Story 014 交付后自动满足),不重复实现。
2. Test Evidence 登记路径原写仓库根 `tests/...`,Unity 只编译 `unity/Assets/` 树 —— 真身落 `unity/Assets/Tests/EditMode/Audio/snr_analysis_test.cs`,由 `tests/unit/audio_system/README.md` 互链(承 Story 002/003/004 先例)。

**评审与修复**:双评审(unity-specialist 代码面 0 BLOCKING + qa-tester 3 BLOCKING · 4 REC)→ **全修并复验**:
- **B1**(两评审独立命中):AC-44-05 ② 的 `ParseStethoscopeExposedParams` 扫全文件唯一那处 `m_ExposedParameters`(挂在 `AudioMixerController` 根级),与 Stethoscope 组在 YAML 层**无关联** ⇒ 断言无法区分「挂在 Stethoscope 下」与「三条恰好同名」。**处置 = 改为直接调门** `ValidateTierFilterCarrierGroups`(门的判据主体 = 找 Stethoscope 组 → 读 `m_Children` → 断言五名 ∈ 子组名集合),不再自写全局扫描;并加夹具自证(三列名出现在 YAML 可达面上)。门的该判据此前已正确,**是测试在测一个不存在的判据**。
- **REC2**(QA 面):**tier guid 对齐无自动化回归** —— 五名只在 Story 014 的反射探针覆盖 7 名 bus,**tier 五名不在任何自动化断言内**,只靠一次性人工实测。新增 `test_tierFilterCarrierGroups_guidMatchesGroupVolumeHash`:五名 exposed guid 与同名组 `m_Volume` 逐条相等。起因:Story 004 曾发现「合成 guid 对不上」⇒ 当时只改了值,**而这条等式本身从未进自动化断言**。
- **REC1**:AC-44-T3「回读 == v」因 EditMode 限制未测(H-B),story Known Risks 3 已登记为**有意留白**,非遗漏。
- **REC4**:幽灵引据「MUST DO 1/2」→ story 真实小节名。

**⚠️ H-B 结论适用**:Story 004 已实测 `AudioMixer.SetFloat` 在 EditMode 对**任何名字**都返回 false(含明显不存在的名字),而 `GetFloat` 返回 true ⇒ `SetFloat` 的「按名查表」native 路径在此环境本身不通,**与 exposed 配置无关**。本 story 的 T3 判据据此调整为「`GetFloat` 可读 + 回读一致」,并在 T3 doc 注明 SetFloat 为何在此环境不可判。**运行期(播放态)仍有效。**

**残余 NICE(登记不修)**:`InjectExposedParameter`/`InjectSnapshotCapture` 无注入自证(对照同文件其他破坏性测试的纪律)· T5 的「直 set 绕过 ramp」真负例零测试(该测断言的是**正例行为**:首调直落位)· tier 组父归属分支未测(只测了「删整组」,没测「组存在但挂错总线」)。

**Test Evidence**: 真身 `unity/Assets/Tests/EditMode/Audio/snr_analysis_test.cs`(**21 测全过**);全量 EditMode **905/905 Passed** · 0 红 · 0 跳过(`unity/Logs/s005-final2.xml`,2026-09-27)
**Code Review**: Complete —— 双专审并行 + B1 修复 + 复验;review mode = lean,QL-TEST-COVERAGE / LP-CODE-REVIEW 门按 lean 规则跳过。
**ADR Compliance**: ADR-018 §四 需求③(`Stethoscope` 总线暴露接触噪声底与信噪比)—— COMPLIANT。五名承载组挂 Stethoscope 下,暴露 12 条(7 bus + 5 tier),闭集双向差集为零。
**Tech Debt**: 未立文件;上述 NICE 3 项 + tier 缺口已分处登记(本 notes · story Known Risks · 账本)。
