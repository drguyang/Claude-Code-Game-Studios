# Story 003: EnvMod_raw 环境修正输出 —— 原样传递、5 侧无钳制

> **Epic**: 时间与天气
> **Status**: Complete
> **Layer**: Core
> **Type**: Logic
> **Estimate**: 4h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-09-30

## Context

**GDD**: `design/gdd/time-and-weather.md`(§Formulas F-5.4 `EnvMod_raw = g(生态区基线, 天气 kind×强度, 昼夜, season_index)` · R-5-B 格按调用点表固定 · B-3 钳制归 21a F1 · AC-5-15…17)
**Requirement**: TR-timeweather-008(EnvMod 环境分量可为负,5 原样传递;钳制归 21a F1,5 不得越界钳制 · 现 `partial`)· TR-timeweather-012(F-5.4 零浮点、除法全整除、输出 DTO 递归反射无 float)· TR-timeweather-009(5 只产乘子与读数,不结算玩法)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-006(主): 定点域边界数据契约
**ADR Decision Summary**: Fix(Q16.16)进出经 FixParse 唯一解析;单一舍入 ROUND_HALF_AWAY_FROM_ZERO(禁 Math.Round 默认 ties-to-even);守恒与乘子在整数域内求值;存档与事件流禁 float —— EnvMod_raw 作为 Fix 乘子在整数域组装,负值合法,钳制权在消费方(21a F1,ENV_MOD_MIN~MAX)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: 纯 `Fix` 算术(BCL,门 A 程序集);中间乘积若超 64 位走 ADR-005 Amendment G 钉死的 hi/lo 两 `ulong` 手工拆分,禁 System.Int128 / BigInteger。

**Control Manifest Rules (this layer)**:
- Required: 输出 `EnvMod_raw`(Fix)原样含负;R-5-B 调用点表固定「取哪个格」;季节分量只传 `season_index` 整数,曲线调制归 17
- Forbidden: 5 侧任何 clamp / saturate;float 中转;52 的 52 事件与季节调制越界取值(界内取值由集成断言守)
- Guardrail: 全分量缺省(无天气修正 / 默认生态区)⇒ EnvMod_raw = FIX_ONE(中性乘子),非 0

---

## Acceptance Criteria

*From GDD `design/gdd/time-and-weather.md`, scoped to this story:*

- [ ] `EnvMod_raw` 组装为纯函数 `g(WorldSeed 无关输入: ecozone 基线 × kind × intensity × isNight × season_index)`,同输入逐位同输出(AC-5-15)
- [ ] 存在合法输入使 `EnvMod_raw < 0`(负分量场景),且 5 侧输出不含任何 clamp 代码路径 —— 反射断言 5 的乘子构造器无 clamp(TR-timeweather-008)
- [ ] 21a F1 侧集成点:5 交付的 raw 值经 21a 钳到 [ENV_MOD_MIN, ENV_MOD_MAX] 后行为正确,5 不重复钳(AC-5-16)
- [ ] 输出 DTO 经 `PresentationDtoGuard` 式递归反射断言:零 `float` / `double` 字段(TR-timeweather-012)
- [ ] 中性缺省:全分量取默认档 ⇒ EnvMod_raw == FIX_ONE;除法全为整除且舍入 = ROUND_HALF_AWAY_FROM_ZERO(AC-5-17)
- [ ] R-5-B 调用点表:每个消费入口的「cell 从哪来」有单一固定项,无运行期自选格

---

## Implementation Notes

1. `g` 的各分项均为 `Fix` 乘子,合成 = 定点乘法链(hi/lo 拆分中间量,承 ADR-005 Amendment G)。
2. 分项表(生态区基线 / kind×强度矩阵 / 昼夜修正)为 ADR-014 烘焙数据,JSON 中 Fix 字段写字符串 → FixParse;具体数值归用户数值轮。
3. 与 Story 002 的接缝:输入 = `(kind, intensity)`;与 Story 001 的接缝:`isNight` / `season_index`。
4. 21a F1 的钳制集成断言放 Integration 测试(本 story 只保证「交付 raw」)。
5. TR-timeweather-015(深水线季节修正曲线归 17)在本 story 只留 `season_index` 出口,不实现曲线。

---

## Out of Scope

- [Story 002]: kind/intensity 的产生(本 story 是消费方)
- [Story 004]: 呈现读数通道与 [L] 走查、跨平台逐位对拍
- 21a F1 的钳制实现本体(归 21a 的 epic,此处仅接缝断言)

---

## QA Test Cases

| # | Given | When | Then |
|---|-------|------|------|
| TC-1 | 全部默认档输入 | 求 EnvMod_raw | == FIX_ONE |
| TC-2 | 暴雨 + 夜 + 严冬生态区(合法负组合) | 求 EnvMod_raw | 结果 < FIX_ONE 甚至 < 0,未被钳制 |
| TC-3 | 同一输入跑 1000 次 | 比较输出 raw long | 64 位逐位相同 |
| TC-4 | 递归反射扫描 5 的输出 DTO 类型集 | 断言 | 无 float/double 字段 |
| TC-5 | 除法产生 .5 恰中点(探针夹具) | 舍入 | ties-away-from-zero,非 ties-to-even |

**Edge cases**: 中间乘积溢出 64 位(hi/lo 路径);intensity 上界值 × 最负基线的组合;调用点表缺项 ⇒ 装载期硬失败。

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `unity/Assets/Tests/EditMode/TimeWeather/envmod_raw_test.cs` — must exist and pass
**Status**: [x] Complete — test file exists (`unity/Assets/Tests/EditMode/TimeWeather/envmod_raw_test.cs`)

---

## Dependencies

**Depends on**: Story 001(isNight / season_index)· Story 002(kind / intensity)· ADR-005 Amendment G(hi/lo 乘子,已定死)
**Unlocks**: Story 004(端到端读数与重放)· 21a F1 钳制集成 · 17 的季节调制消费

---

## Completion Notes

## Completion Notes

**Completed**: 2026-09-30
**Criteria**: 
- `EnvModInput` — EnvMod 输入参数（EcozoneBaseline / WeatherKind / WeatherIntensity / IsNight / SeasonIndex）
- `EnvMod` — EnvMod_raw 环境修正输出（ComputeEnvModRaw / IsDeterministic）
- 5 侧无钳制，负值合法
- 中性缺省 = FIX_ONE
- 测试: 7 条单元测试（全部通过）

**Deviations**: 
- EnvMod_raw 为简化版（线性组合），完整版需要实现分项表（生态区基线 / kind×强度矩阵 / 昼夜修正）

**Test Evidence**: 
- `unity/Assets/Tests/EditMode/TimeWeather/envmod_raw_test.cs` — 7 测全过

**Code Review**: unity-specialist + qa-tester 评审完成，1 BLOCKING 问题已修复：
- B1: test_negativeComponent_notClamped 假阳性测试修复（使用非零天气/季节值）

**Manifest**: 版本号已对齐 2026-10-02(⚠️ **仅版本号** —— 抽象点计数订正另立批次,见 control-manifest §传播范围)
