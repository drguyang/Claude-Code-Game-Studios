# Story 008: P2 施治动作真资产提前 —— 止血/通气的真接触音 + 手势/节律 VFX

> **Epic**: 急救动作模块
> **Status**: Ready ⬜(依赖素材本体产出 —— 外部美术 / 用户自产)
> **Layer**: Feature
> **Type**: Visual / Feel
> **Estimate**: 待估(素材本体依赖;spec 面已定)
> **Manifest Version**: 2026-10-12
> **Last Updated**: 2026-10-12

## Context

**裁定**:sprint-07 §6 美术提前提案 —— **2026-10-12 用户取甲案「P1+P2 全提前」**(AskUserQuestion);
本 story = P2 半边。**提前至 M4 之前**,不进 sprint-07 关键路径。
**批次承接**:G-3 `emergency-procedures` 7 项中的**手势 VFX 子集**(VFX#6 急救止血包扎 / #7 CPR 节律视觉反馈)+
**batch-03 音频素材**(SFX#4 止血包扎接触音 / SFX#5 CPR 节律音 —— 素材在 batch-03,流派归本 epic)。

**GDD / Spec 来源**:`design/gdd/emergency-procedures.md`(两动作具名:`OQ-10-4` 已裁 = 止血包扎两拍序列 + 节奏型通气动作 · `:900` VS Critical 清单 · AC-44-08 医学准确性)· `design/gdd/audio-system.md`(接触音 / 节律音 · F-44.7 通带+噪声底)· `design/assets/entity-inventory.md`(SFX#4/5 · VFX#6/7)

**ADR Governing Implementation**: ADR-018(44 只渲染零状态 · 音频事件表烘焙 · 无提示音铁律 AC-44-09)· ADR-011(急救直读通道 · 预表现)
**ADR Decision Summary**: 施治动作手感 = **音频 + 视觉反馈**(接触音 + 手势 VFX + 节律视觉)——
`EmergencyAttempt` / `EmergencyTreatmentApplied` 流契约已闭(story-007);本 story 只**喂真素材**到
既有 cue/VFX 通道,零流 / 零判定变更。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: MEDIUM(素材导入 + VFX 表现;AC-44-08 医学准确性为特色劳动)
**Engine Notes**: VFX 走既有事件通道(`AudioCueDto` 整数语义 + 视觉事件表);素材缺失 = 编辑期门拒绝(story-015/016 已立)。

**Control Manifest Rules (this layer)**:
- Required: 施治反馈经 44 通道(音频)+ 表现层 VFX,零三流写入(ADR-018 §一)
- Forbidden: 无提示音铁律(AC-44-09)—— 节律反馈是**动作同步**,不得报「状态成功/失败」(sting/jingle 禁)
- Guardrail: 素材缺失 = 既有测试不红(灰盒豁免);门只检「表里引的文件存在」—— 真素材落地前 story-015/016 为 Not-Run 面

---

## 现状实测(2026-10-12 · HEAD `912535f`)

- 10 的两动作具名已冻结(`OQ-10-4` = 止血包扎两拍序列 + 节奏型通气;`EmergencyAction` 枚举住 10 侧,
  `OQ-10-6` 已裁,P0 = `HemostasisBandage=0` / `RhythmVentilation=1`)。
- **施治手感判定全绿**(story-001…007 全闭;`L_input` < 35 ms 实测,OQ-10-12)—— **但玩家零手感素材**:
  接触音 / 节律音 / 手势 VFX 的 `assets/audio/` 与 VFX 事件通道**均无真素材**。
- `assets/audio/` **空目录**(story-015 实测);`audio_events.json` 已引 5 个 `.wav` 但逐个不存在。
- 两动作的施治视觉载体(手势 / 节律)**零素材** —— 玩家「按 F」只有判定,无动作感。

## Acceptance Criteria

*From `emergency-procedures.md` + `entity-inventory.md` + sprint-07 §6 P2 裁定:*

- [ ] **止血包扎接触音**(SFX#4):绷带接触身体的接触音真素材入 `assets/audio/`,
  经 `audio_events.json` 表 → story-015/016 存在性门**真素材检验通过**(删一 wav ⇒ 构建失败负测成立)
- [ ] **CPR 节律音**(SFX#5):按压节律反馈音 —— **节律 = 动作同步**(与 `JITTER` / 550 ms 周期对齐),
  **禁报状态**(AC-44-09 无提示音铁律:成功/失败不得由 sting/jingle 表达)
- [ ] **急救止血包扎 VFX**(VFX#6):止血处理视觉反馈(手势 + 接触表现)—— 玩家「按 F 施治」有真动作感
- [ ] **CPR 节律视觉反馈**(VFX#7):按压节律视觉同步 —— 非数字 / 非血条(拟物铁律)
- [ ] **医学准确性**(AC-44-08 半边):接触音 / 节律音**医学受众不出戏**(细湿啰音非连续水声同型纪律)
- [ ] 流契约零变更:`EmergencyAttempt` / `EmergencyTreatmentApplied` 载荷**不因素材引入而改**
  (素材 = 纯表现,story-007 九字段已闭)

## Implementation Notes

1. 素材落地路径:`assets/audio/`(`.wav`,batch-03 承接)+ 施治 VFX 走表现层事件通道(ADR-018 §五)。
2. 节律视觉同步 = 以 tick 边沿驱动(CPR 周期 ≈ 550 ms / `TICK_PERIOD 50 ms` = 11× 抖动门余量,OQ-10-12 实测)。
3. 接触音真实性优先(AC-44-08):录制 / 合成须过医学准确性走查(44 侧实现轮)。
4. **验收证据 = 截图(施治瞬间)+ 音频走查签署**(Visual-Feel / 音频,ADVISORY 级);自动化只保「门拒绝存在」与「流契约零变更」。

## Out of Scope

- 10 的判定 / 手感机制(本 story 只喂真素材)
- 手术灯 / 针具 / 绷带包(Props#6 / Items#5/#6,未提前,维持 M4)
- 数值轮(节律参数 / 音量 / 增益,数值用户自己调)

---

## QA Test Cases

| # | Given | When | Then |
|---|-------|------|------|
| TC-1 | 素材齐备 | 按 F 施治(止血) | 接触音播放 + 手势 VFX 可见;流契约零新事件 |
| TC-2 | 素材齐备 | 按 F 施治(通气/CPR) | 节律音与按压边沿同步;节律视觉同频 |
| TC-3 | 删任一 wav | 编辑期门 | story-015/016 存在性门拒绝(负测成立) |
| TC-4 | 素材缺失 | 运行 | 判定照常(灰盒豁免),既有测试全绿 |

**Edge cases**: 施治中途取消(`Method=Skip` 路径无提示音 —— 跳过不播节律);两动作同频按压(节律音不叠加报状态)。