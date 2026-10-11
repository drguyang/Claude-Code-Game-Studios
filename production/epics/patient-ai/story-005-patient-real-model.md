# Story 005: P1 病人真模型提前 —— 灰盒胶囊 → 可识别人形(面色五色阶梯)

> **Epic**: 病人 AI 与行为
> **Status**: Ready ⬜(依赖素材本体产出 —— 外部美术 / 用户自产)
> **Layer**: Feature
> **Type**: Visual / Feel
> **Estimate**: 3.0 人日(承 `production/asset-batches/README.md` G-3 patient-ai 2 项合计)
> **Manifest Version**: 2026-10-12
> **Last Updated**: 2026-10-12

## Context

**Spec**: `design/assets/specs/patient.md`(T0–T3 四档 · 面色五色阶梯 · 7 套动画 · rig)
**裁定**:sprint-07 §6 美术提前提案 —— **2026-10-12 用户取甲案「P1+P2 全提前」**(AskUserQuestion);
本 story = P1 半边。**提前至 M4 之前**(42 项 VS Critical 中此 2 项先行),不进 sprint-07 关键路径。
**批次承接**:G-3 `patient-ai` 2 项(病人 + HUD 脉冲 + VFX 中的「病人」项 · 3.0 人日)—— 逐 epic 拆 story 约定(batch-02 行)。

**ADR Governing Implementation**: ADR-016 §三(13 病人 AI 只读消费者,读 `VitalsDto` / `IPresentPatients`,不引用 sim 状态)· ADR-009 §五(病人位置 = 表现态)
**ADR Decision Summary**: 病人视觉实体 = **纯表现态**(sprint-05 T2.0 `PatientVisualSpawner` 已立载体);
本 story 只替换**表现形态**(胶囊 → 人形 + 面色映射),**零 sim / 零流 / 零判定变更**。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: MEDIUM(Humanoid rig + 动画导入 + 面色材质变体;ADR-012 不涉 —— 表现层 float 自由)
**Engine Notes**: 表现层浮点不受确定性约束(ADR-005 只约束 sim 域);面色映射 = 材质变体切换(art-bible §8.6.1 面色五色阶梯),**禁入任何判定路径**(13 决策不吃表现)。

**Control Manifest Rules (this layer)**:
- Required: 病人视觉只随在场集与 `VitalsDto` 表现,零写三流(ADR-009 / ADR-016)
- Forbidden: 把面色 / 姿态读进 13 决策器(13 只吃 `position`×`trend`,signs 只喂材质 —— patient-ai EPIC Overview 机械护栏)
- Guardrail: 灰盒胶囊保留为回退形态(素材缺失 = 胶囊 + 既有测试不红,承 Gray-Box Policy)

---

## 现状实测(2026-10-12 · HEAD `912535f`)

- `PatientVisualSpawner.OnTickEdge` 已随在场集生成**灰盒胶囊**视觉实体(sprint-05 T2.0 全绿)。
- `design/assets/specs/patient.md` 已给出**完整资产规格**:`patient_T0…T3_body.png`(面色四档)×
  `patient_face_shared.png` + `patient_skin_normals.png` + `patient_rig.fbx`(Humanoid,7 套动画:
  idle / walk / run / sit / lie_down / groan / seizure)+ LOD0 ≈1500 tris / LOD1 ≈600 tris。
- 消费侧就绪:8 查体对像(值区词条已真装载,sprint-07 S1.2)· 37 立案(在场视图)· 13 表现驱动(滞回带)。
- **缺口 = 素材本体**(0 真资产)与**表现形态替换**(胶囊 → 人形 + severity 面色映射)。

## Acceptance Criteria

*From `design/assets/specs/patient.md` + sprint-07 §6 P1 裁定:*

- [ ] 病人视觉实体从灰盒胶囊替换为**可识别人形**(rig + idle/行走/躺卧动画面接入;
  素材缺失时**回退胶囊不红** —— 灰盒豁免判据,承 M4 出口 §六第 4 条)
- [ ] **面色五色阶梯映射**:T0–T3 四档 severity 由材质变体切换呈现(art-bible §8.6.1),
  **禁数字角标 / 禁血条**(拟物 UI 铁律,承支柱四)
- [ ] 表现态边界:面色 / 姿态只随 `VitalsDto` 表现,零三流写入(反射断言,承 ADR-016 机械护栏);
  13 决策器输入类型白名单**不增面色来源**
- [ ] 消费系统对像:8 查体(近病人可查体,对像 = 人形)+ 37 立案(在场集不变)+ 13 表现(五档姿态)——
  端到端一张图:人形病人随体征变化面色 / 姿态

## Implementation Notes

1. 素材落地路径 = `assets/art/`(承 art-bible §资产路径);导入格式 / 元数据读出归 G-1 内部收口先例。
2. 面色映射 = **材质变体表**(severity → material instance),禁运行期 Shader 参数热改(表现层性能面,承 ADR-013 §6.6 假设 6 未 spike)。
3. 动画接入走 Animator 控制器 + 7 段 clip;`seizure`(痉挛)触发 = TR-patient-021 效果表现(写路径归 10,story-004 已闭)。
4. **验收证据 = 截图 + 用户/美术签署**(Visual-Feel 类,ADVISORY 级);自动化只保「回退胶囊不红」与「映射表存在」。

## Out of Scope

- 13 / 8 / 37 的机制实现(本 story 只替换表现形态)
- HUD 脉冲 / VFX(patient-ai G-3 另一项,VFX#5 病人状态变化,未提前)
- 数值轮(severity 面色阈值归 9,数值用户自己调)

---

## QA Test Cases

| # | Given | When | Then |
|---|-------|------|------|
| TC-1 | 素材齐备 | 运行 | 人形可见,非胶囊;idle/躺卧动画随行为档切换 |
| TC-2 | 素材缺失 | 运行 | 回退胶囊,既有测试全绿(不借绿判据反向成立) |
| TC-3 | severity T0→T3 | 推进体征 | 面色五色阶梯切换;姿态档位同步(坐→躺卧→昏迷) |
| TC-4 | 查体对像 | 近病人按 E | 值区词条出现(对像仍可查体,形态替换无回归) |

**Edge cases**: severity 跨档临界(面色变、决策不变 —— 13 决策不吃表现);素材部分缺失(单档缺失 = 该档回退灰盒,不整组回退)。