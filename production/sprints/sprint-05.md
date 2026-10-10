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
| T1.1 | `PatientSpawner` 运行期调用方 | 组合根(CompositionRoot)+ tick 驱动 | ✅ **Complete 2026-10-10**(B 案修订) —— PlayMode:启动即 spawn ≥1 病人 + 接线期上限哨 3。证据:`unity/Logs/s5_t1_1_bfix.xml` |
| T1.2 | `CaseOpenWriter.TryOpen` 运行期入口 | 9 诊断链触发点 | ✅ **Complete 2026-10-10** —— PlayMode 验证测通过:启动后病史流含 `CaseOpened` 事件 + 在场病人数 ≥1。证据:`unity/Logs/s5_t1_2_case_open.xml` |
| T1.3 | 急救链接线 | `HostEmergencyProcessor` ← 输入聚合器 | ✅ **Complete 2026-10-10** —— PlayMode 验证测通过:启动后病史流含 `EmergencyAttempt` + `EmergencyTreatmentApplied` 事件。证据:`unity/Logs/s5_t1_3_emergency_verify.xml` |
| T1.4 | 边界语义(出界) | 6 世界扩展或 World.unity 出界重置 | ✅ **Complete 2026-10-10** —— PlayMode 验证测通过:玩家出界后 y 回到 ≥0(坠落重置生效)。证据:`unity/Logs/s5_t1_4_fall_reset.xml` |
| T1.5 | 相机跟随 | ADR-020 第三人称越肩 | 运行时 `ICameraRig` 驱动主相机;玩家位移时相机跟随;PlayMode 断言相机位置 ≠ 初始 |

### T2 形态件施加:拟物 UI 呈现(Phase B · 约 1.5–2 天)

| # | 任务 | 落点 | 判据 |
|---|---|---|---|
| T2.0 | 病人最小可见实体 | `PatientVisualSpawner`(Gameplay.Boot)+ BootRoot 清理 | ✅ **Complete 2026-10-10** —— PlayMode:Hierarchy 病人视觉实体数 = 在场病人数(1:1)。证据:`unity/Logs/s5_t2_0_patient_visual2.xml` |
| T2.1 | `SkeuoRuntimeDriver` DTO 绑定 TODO | 42 UI 表现 | ✅ **Complete 2026-10-10** —— 两处 TODO 之一落地:新建 `ICasebookDtoSource`(住 Gameplay.UI)+ 生产实装 `CasebookVitalsSource`(住 Gameplay.Boot,接到 `IVitalsQuery` 唯一浮点出口)+ `CasebookScreen.ApplyData` 值区施加 + `BootRoot` 每 tick 边沿刷新。**禁假数据**:`RevealedRows` 恒空集(F2 signs tripwire:词表内容与阈值归 8,9 侧当前无阈值可判活跃词条)⇒ 走「未查 = 空行」(`:329`)。证据:`unity/Logs/s5_t2_driver8.xml`(15/15) |
| T2.2 | `SkeuoRuntimeDriver` Screen 实例化 TODO | 同上 | ✅ **Complete 2026-10-10** —— 七具名 Screen 查表工厂(`(VisualElement, SkeuoElementLibrary)` 同形构造,**零反射**);未知名 fail-loud 不 fallback;UIDocument 运行期自建(ADR-023 §① 零预摆)。**SkeuoRuntimeDriver 两处 TODO 全清零**(T2.2 判据「零残留 TODO」)。证据同上 |
| T2.3 | `SignChannelBinder` 挂真词条 | story-024 施加点 | ✅ **Complete 2026-10-10(载体半)** —— `SignChannelBinder.Bind` 在运行期有**生产调用方**(此前零施加点:全库唯一引用 = 测试):`CasebookScreen.ApplyData` 每 tick 经它分流 + F-8.2 档位取词,落到五通道 + 问诊栏行。分发纯函数本身 AC-024-1/2/3 早已全绿(story-024),本轮补的是**运行期挂行**。真表端到端(34 词条)已由 story-024 AC-024-3 断言;本轮新增:阳性/阴性形态类分化 + 读不出(`Word=null`)降阴性 + 同通道多词并列保序 |
| T2.4 | story-023 候选列表载体 | 10 急救轮 | ✅ **Complete 2026-10-10** —— 新建 `EmergencyCandidateList`(Gameplay.UI):`.skip-entry` 跳过入口**恒在列表末项**(规则六之甲)+ 零候选也实例化 + AB-3 宽半由 `.candidate-entry` 承接(story-023-3 只锁高半,宽半已登记归 10 轮载体 —— 本轮建的载体即锁定方)+ 零数字(G2)+ 可见性门零时序(Idle 可达归 10 状态机)。USS `SkeuoPaper.uss` + `.candidate-entry` 块。证据:`unity/Logs/s5_t2_t24.xml`(23/23) |
| T2.5 | story-021/022 行级施加 | 脉案行 + 墨态 | ✅ **Complete 2026-10-10** —— ① 线格 `.ruled` 挂行运行期生效(承 T2.0:`CasebookScreen.AddChannel` 已挂,本轮 `BuildUI` 一字未改 ⇒ 既有六条 + 交叉断言零回归);② 墨龄状态类运行期可见:`ApplyData` 按 `InkAgeTicks` 挂 `.ink-wet` / `.ink-dry`(规则十三 派生态,42 侧零计时器 —— 门面 `ICasebookDtoSource.InkAgeTicks` 收**已减好的差值**)。⚠️ 加载序冲突已登记(story-021 边界⑥:与 `.channel-reading*` 同写 `color`,单元素双类胜负由未钉死 stylesheet 加载序决定 —— 今日零焦点施加者不可观测,非缺陷,裁决项不擅自裁) |

### T3 验证与收口

| # | 任务 | 判据 |
|---|---|---|
| T3.1 | 全量门复跑 | ✅ **Complete 2026-10-10**(Phase A) —— PlayMode **104/104 全绿**(+5 新增接线测)· EditMode **3146 passed / 0 failed / 1 inconclusive / 46 skipped**。证据:`unity/Logs/s5_t3_playmode_full2.xml` · `unity/Logs/s5_t3_editmode_full.xml`。修复:组合根 `ResolveRegistry` caller 表优先。◐ **Phase B 后复跑(2026-10-10 晚)**:PlayMode **128/128 全绿**(基线 105 + 15 driver + 8 候选列表)· EditMode **3147 passed / 0 failed / 1 inconclusive / 46 skipped**(基线 3146 + 1 新 composition_root 测)证据:`unity/Logs/s5_t2_playmode2.xml` · `unity/Logs/s5_t2_editmode7.xml` |
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
