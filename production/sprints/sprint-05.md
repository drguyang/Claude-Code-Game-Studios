# Sprint 05 — 形态件施加轮 + 运行期接线(阶段 3)

> **2026-10-10 开 · 范围裁定 = 合并做**(形态件施加 + 运行期接线同批;施加点依赖运行时链路)
> **上游**:sprint-04 阶段 4 收口(批次 F `e38132a` 已推)
> **本 sprint 判据**:四形态件真通道呈现 **+ 核心循环运行期可达**
> **规模**:≈3–4 人日(sprint 行 6 原估 ~2 天 + 运行期接线 1–2 天)
> **用户裁定**:2026-10-10 —— 合并做,理由:施加点依赖运行时链路,分开做会返工

---

## §1 背景:为什么接线是施加点的前置

M2 Exit F-6 复评(`production/qa/evidence/m2-exit-recheck-2026-10-10.md`)实测:

- **写者存在性闭** —— `PatientSpawner.cs:77` / `CaseOpenWriter.cs:135` / 10·11 应急处方写者齐
- **但零生产调用方** —— `SpawnNext` / `CaseOpenWriter.TryOpen` / 急救聚合链
  全库唯一引用 = 测试 ⇒ **运行期链可达性 0%**
- playtest(`playtest-2026-10-10-m2-manual.md`)§6 独立互证:编辑器里无病人、无 UI,
  `SkeuoRuntimeDriver` 两 TODO 仍是空壳

⇒ 形态件(①②③④)要「挂真」,必须先有运行态病人与体征通道;本 sprint 顺序:
**先接线(病人在场)→ 再呈现(拟物 UI 挂行)**。

---

## §2 任务清单

### T1 运行期接线:病人在场(Phase A · 约 1–1.5 天)

| # | 任务 | 落点 | 判据(可证伪) |
|---|---|---|---|
| T1.1 | `PatientSpawner` 运行期调用方 | 组合根(CompositionRoot)+ tick 驱动 | ✅ **Complete 2026-10-10** —— PlayMode 验证测通过:启动后病史流含 `DiseaseOnset` 事件 + 在场病人数 ≥1。证据:`unity/Logs/s5_t1_1_patient_wiring12.xml` |
| T1.2 | `CaseOpenWriter.TryOpen` 运行期入口 | 9 诊断链触发点 | ✅ **Complete 2026-10-10** —— PlayMode 验证测通过:启动后病史流含 `CaseOpened` 事件 + 在场病人数 ≥1。证据:`unity/Logs/s5_t1_2_case_open.xml` |
| T1.3 | 急救链接线 | `HostEmergencyProcessor` ← 输入聚合器 | ✅ **Complete 2026-10-10** —— PlayMode 验证测通过:启动后病史流含 `EmergencyAttempt` + `EmergencyTreatmentApplied` 事件。证据:`unity/Logs/s5_t1_3_emergency_verify.xml` |
| T1.4 | 边界语义(出界) | 6 世界扩展或 World.unity 出界重置 | ✅ **Complete 2026-10-10** —— PlayMode 验证测通过:玩家出界后 y 回到 ≥0(坠落重置生效)。证据:`unity/Logs/s5_t1_4_fall_reset.xml` |
| T1.5 | 相机跟随 | ADR-020 第三人称越肩 | 运行时 `ICameraRig` 驱动主相机;玩家位移时相机跟随;PlayMode 断言相机位置 ≠ 初始 |

### T2 形态件施加:拟物 UI 呈现(Phase B · 约 1.5–2 天)

| # | 任务 | 落点 | 判据 |
|---|---|---|---|
| T2.1 | `SkeuoRuntimeDriver` DTO 绑定 TODO | 42 UI 表现 | `VitalsDto` → 脉案页数据绑定;DTO 变化驱动 UI 更新;禁假数据 |
| T2.2 | `SkeuoRuntimeDriver` Screen 实例化 TODO | 同上 | 四形态件载体 Screen 运行时实例化;零残留 TODO |
| T2.3 | `SignChannelBinder` 挂真词条 | story-024 施加点 | 体征词条经 `SignChannelBinder` 分发到五通道;真表端到端 |
| T2.4 | story-023 候选列表载体 | 10 急救轮 | `.skip-entry` 候选列表载体运行时实例化;跳过入口可用 |
| T2.5 | story-021/022 行级施加 | 脉案行 + 墨态 | 线格 `.ruled` 挂行运行期生效;`.ink-wet`/`.ink-dry` 接图运行期可见 |

### T3 验证与收口

| # | 任务 | 判据 |
|---|---|---|
| T3.1 | 全量门复跑 | ✅ **Complete 2026-10-10** —— PlayMode **104/104 全绿**(+5 新增接线测)· EditMode **3146 passed / 0 failed / 1 inconclusive / 46 skipped**(与 F-4 基线 3144/0/1/46 一致,+2)。证据:`unity/Logs/s5_t3_playmode_full2.xml` · `unity/Logs/s5_t3_editmode_full.xml`。修复:组合根 `ResolveRegistry` caller 表优先(原 fallback 硬编码 ⇒ VerticalSliceTest 两条红) |
| T3.2 | 人工 playtest 第二轮 | 报告落 `production/playtests/`;验证运行期病人在场 + 拟物 UI 呈现 |
| T3.3 | M2 Exit 剩余条复评 | #2 端到端(接线后判)、#8 形态件(施加后判) |

---

## §3 分阶段验收

- **Phase A 完成**:T1.1–T1.5 全绿 ⇒ 病人在场 + 出界可重置 + 相机跟随
- **Phase B 完成**:T2.1–T2.5 全绿 ⇒ 四形态件真通道呈现
- **本 sprint 完成**:A + B + T3 三项全闭

---

## §4 风险与依赖

- **T1.4 边界语义**属 6 世界扩展域,若与 6 的排期冲突,出界重置做最小实现(坠落重置)即可
- **T1.5 相机**依赖 ADR-020 实现轮;相机 SDK 无关(自建机位),低风险
- **T2.x 施加点**以 42 只读渲染为纪律(ADR-013 C3):UI 只读 `VitalsDto`,不持有游戏状态
- **playtest 第二轮**须有「病人在场」可观察面,否则人工面仍 0% 覆盖

---

## §5 不做

- **019-b spike(PAGES_MAX)** —— Blocked,归 M2 Exit 五族条,不在本 sprint
- **45 联机夹具** —— P1b,不阻塞本 sprint
- **核心循环完整可玩性** —— 本 sprint 只做「可达 + 呈现」,不做平衡/手感/数值
