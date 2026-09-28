# Story 003: 呈现投影 —— ViewState 优先级、IPresentPatients 视图与 cue / Material 通道

> **Epic**: 病人 AI 与行为
> **Status**: Ready
> **Layer**: Core
> **Type**: Integration
> **Estimate**: 6h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-28

## Context

**GDD**: `design/gdd/patient-ai.md`(§Formulas F-13.5 `CueInterval = CUE_INTERVAL[SymptomTier]`(分段常函数,ClampByte[0,255],相位 = `hash32(p.Id) mod`,零 PRNG)· F-13.6 `ViewState` 优先级 `InTreatment > Collapsed > AwaitingCare > Present` · F-13.8 `signs[] → MaterialTable` · §Rules 十 `IPresentPatients` 载荷 = `PatientId + WorldPos 格 + 粗状态枚举,无 disease_id` · C/D 组 AC-13-C1…C5 / D1…D6)
**Requirement**: TR-patient-011(表现映射归 13;8 拥有体征语义、9 拥有真值,三者不互窜)· TR-patient-012(`IPresentPatients` 只读视图;13 不引用 37,单向无环)· TR-patient-018(咳嗽/呻吟 cue 契约 → 44,`AudioCueDto` 整数语义,不带「状态变化」语义)· TR-patient-019(昏迷/死亡靠呼吸层与姿态区分,禁音效硬报)· TR-patient-020(13 的呈现 DTO 受 `PresentationDtoGuard` 递归扫描)· TR-patient-023(13 无玩家可见 UI;调试视图仅 Development Build 且不显示病种 · 现 `gap`)· TR-patient-024(听障/视障可及性 · 现 `gap`,AC-13-F1…F3 在本 story 落断言)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-013(主): 拟物 UI 框架(呈现只渲染不持状态);ADR-018: 音频架构(无提示音铁律白名单)
**ADR Decision Summary**: ADR-016 §六 结清 13↔37:`IPresentPatients` 只读在场视图,37 读 13、13 不引用 37(单向无环);ADR-013 §9 C3:呈现层只渲染永不持有游戏状态,`disease_id` 不进呈现层(`PresentationDtoGuard` 递归反射);ADR-018:cue 走 `AudioCueDto` + 烘焙事件表,禁 sting/jingle/素材切换报状态(AC-44-09 同白名单)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: cue 经 44 的既有 `AudioCueDto` 通道(长期稳定 API);相位哈希 `hash32` 为 BCL 纯函数;`MaterialTable`/`CUE_INTERVAL` 走 ADR-014 烘焙。

**Control Manifest Rules (this layer)**:
- Required: `CueInterval` 按 **SymptomTier 档**不按连续 position(分段常函数);同档内扫任意 position 输出恰一个间隔值(AC-13-D6 反幻想护栏);`Intensity ∈ [0,255]` 产物侧夹取(AC-13-D4);去同步相位 = `hash32(PatientId)` 纯函数
- Forbidden: 任何 PRNG(`UnityEngine.Random`/`System.Random`/噪声源)出现在 cue 路径(AC-13-D5);cue 携带状态变化语义 / 死亡一次性播报(AC-13-D2/D3);`signs[]` / `position` / `trend` / `disease_id` 进 `IPresentPatients` 载荷(AC-13-C1)
- Guardrail: 13 零玩家可见 UI(调试视图 = Development Build 条件编译剥离 + 不显示病种,AC-13-A4/TR-023);`Down/Terminal` 的表现 = 呼吸层停止 + 最静止档,无音效硬报

---

## Acceptance Criteria

*From GDD `design/gdd/patient-ai.md`, scoped to this story:*

- [ ] `IPresentPatients` 载荷类型递归反射断言:恰 = {`PatientId`, `WorldPos`, 粗状态枚举};无 `disease_id` / `position` / `trend` / `signs[]`(AC-13-C1,与 AC-37-15 同门)
- [ ] `ViewState`(F-13.6)与 §States 二枚举表**逐字一致**:真值表叉乘 `(BehaviorState, SessionState, SeekingPhase)` 全部合法组合,优先级 `InTreatment > Collapsed > AwaitingCare > Present` 锁死(AC-13-C5)
- [ ] 场外病人不出现在视图;13 不生成/不删除病人(AC-13-C3/C4 视图侧)
- [ ] 13 不引用 37 程序集(单向无环,静态引用断言,AC-13-C2)
- [ ] `CueInterval` 分段常函数性质测试 + `Intensity` 对越界输入(position ∈ [−10,10] 夹具)恒 ∈ [0,255] 无回绕(AC-13-D4/D6)
- [ ] cue 路径零 PRNG 调用点(AC-13-D5);同 id 病人相位稳定、异 id 去同步(哈希分布抽查)
- [ ] 死亡表现 = 呼吸层消失,无一次性播报音效;昏迷/死亡区分仅靠呼吸层+姿态(AC-13-D3,承 ADR-018)
- [ ] `MaterialTable`(F-13.8)存在、覆盖症状三档 × `signs[]` 词条两栏、落 `assets/data/ai_patient.json` 且带 44 签署记录(AC-13-D1 `[A]`)
- [ ] **[L]** 无障碍三判据:AC-13-F1 三类 cue 均有非色相可辨的可见对应物且默认可见;AC-13-F2 会移动病人在 `PERCEPT_R` 内至少呼吸层可闻;AC-13-F3 非目标条款存在且被 `design/accessibility-requirements.md` 引用(文档判据,引用已兑现 ≠ 本条记绿)
- [ ] 调试视图:玩家构建中代码路径不存在(构建产物扫描,AC-13-A4 `[A]`)

---

## Implementation Notes

1. 投影链 = 单向:`(BehaviorState, SessionState, LogiPose, VitalsDto.signs[]) → {ViewState, PatientSignalDto, AudioCueDto 触发}`,任何环节零回写 sim/流(承 Story 001 的零写门)。
2. `hash32(p.Id) mod CUE_INTERVAL[tier]` 的相位只在 cue 发射器内用,不进决策(决策无相位)。
3. 与 44 的接缝:13 只发触发,音图(咳嗽/呻吟/呼吸层)与混音归 44;`MaterialTable` 的 44 签署是流程门(证据 = 签署记录文件)。
4. 视图为 pull 语义:37 在自身 tick 里读快照;13 不 push、不缓存 37 侧任何东西。
5. [L] 无障碍项需 44 侧素材与 42 冗余通道就位 —— 未就位时 SIGN-OFF 记 BLOCKED-BY 外部项,禁借绿。

---

## Out of Scope

- [Story 001]: Map/滞回机制(本 story 是其下游投影)
- [Story 002]: LogiPose 的产生(本 story 只读格)
- [Story 004]: 联机(客户端表现态位置经 ADR-001 第二 QoS 的接缝)与写路径归 10 的验收
- 44 的音频素材/混音本体、42 的读数条呈现(归各自 epic)

---

## QA Test Cases

| # | Given | When | Then |
|---|-------|------|------|
| TC-1 | 全组合夹具 (Behavior×Session×Phase) | 求 ViewState | 与表逐字一致;优先级冲突时高优先胜出 |
| TC-2 | 同档内 position 扫描 0.60→0.79(假设档界) | 读 CueInterval | 恰一个值(分段常函数) |
| TC-3 | position = −3 / 9 越界夹具 | Emit | Intensity ∈ [0,255],无静默回绕 |
| TC-4 | 两个异 id 病人同档 | 观察相位序列 | 去同步(hash32 相位);同 id 重放相位稳定 |
| TC-5 | 病人死亡(Terminal) | 听/看 cue 流 | 呼吸层消失;无死亡 sting;姿态为最静止档 |
| TC-6 | 视图载荷反射 | 断言 | 类型集恰三项;`disease_id` 不存在 |
| TC-7 | 关闭调试符号的玩家构建 | 扫描 | 调试视图代码路径不存在 |

**Edge cases**: 跳级 ViewState(Se→Collapsed 无中间帧)的表现可解释性;`signs[]` 空集 ⇒ 中性材质无附加词条;同 tick 多人入场的发射预算(不饿死)。

---

## Test Evidence

**Story Type**: Integration
**Required evidence**: `unity/Assets/Tests/EditMode/PatientAI/presentation_projection_test.cs`(投影/视图/cue)+ `production/qa/evidence/patient-ai/story-003-accessibility-signoff.md`([L] 项) — must exist and pass
**Status**: [ ] Not yet created

---

## Dependencies

**Depends on**: Story 001(SessionState/BehaviorState)· Story 002(LogiPose/LOD)· 44 的 `AudioCueDto` 通道(ADR-014/018 管线已立)· `PresentationDtoGuard`(ADR-013 已立)
**Unlocks**: 37 的立案消费(`IPresentPatients`)· 42 的读数条接线 · Story 004(联机投影一致性)

---

## Completion Notes
