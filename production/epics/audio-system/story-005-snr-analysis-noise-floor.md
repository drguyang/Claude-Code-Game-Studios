# Story 005: SNR 分析域与噪声下界

> **Epic**: 音频系统
> **Status**: Ready
> **Layer**: Foundation(系统分类;实现落表现层 L5)
> **Type**: Logic
> **Estimate**: 3h
> **Manifest Version**: 2026-09-21
> **Last Updated**: (set by /dev-story when implementation begins)

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

- [ ] **AC-44-05**:构建期校验 —— ① 三输入直取 `tier_map` 列(`signal_db`/`noise_floor_db`/`contact_noise_floor_db`),`ComputeSnrDb` 按 F-44.2 计算,**测试自带常量夹具**(边界 −24/+24 恰含、±200 溢出钳位),`∀T: ComputeSnrDb(行) ∈ [−24,+24] dB`,超界 = 配置拒绝;② 三列 ∈ `Stethoscope` mixer 组暴露参数闭集(YAML 枚举断言)。
- [ ] **AC-44-06**:`noise_floor_db`/`contact_noise_db` 均**有限且 ≥ `NOISE_FLOOR_DB_MIN`**;`∀T: ComputeSnrDb ≥ −24 dB`(「不毁掉信号」的量化形态);与 8 侧 `READ_FLOOR_MIN > 0` 方向对齐断言(或注明归 8 轮)。

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
- **AC-44-06**: 下界断言。Given: `tier_map` 全行。When: 读两噪声列。Then: 均有限 ≥ `NOISE_FLOOR_DB_MIN`;`ComputeSnr ≥ −24`;负向:写 `-200` ⇒ 红;与 8 的 `READ_FLOOR_MIN` 方向对齐(双正断言或显式转 8 轮注记)。

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- `tests/unit/audio_system/snr_analysis_test.cs` — must exist and pass

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001 · 002(`tier_map` 载体与六列集)
- Unlocks: Story 013(听测前的数据面就绪)
