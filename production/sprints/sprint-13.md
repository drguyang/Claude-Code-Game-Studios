# Sprint 13 — 53 结算链贯通 + 收口纪律固化

**周期**:不设结束日期(承 sprint-12 口径)—— **做完即收,收口时补记结束日**
**规划依据**:`production/retrospectives/retro-sprint-12-2026-10-16.md`(行动项 5 条 + 产能结论)
**前置勘察**:Phase 1-5 已重扫 `medical-consequences` epic 现状 ——
story-002 **已 `Complete`**(2026-10-16)· story-001 **仍 `In Review`**(全仓唯一未收口的 In Review 件)·
story-003/004 `Ready` 且依赖面已满足。

## Sprint Goal

把 53 医疗后果链从「判定表就绪」推到「**回响真正写进世界流**」(story-003),
并清偿它唯一的收口前置(story-001 `In Review` + `EPIC.md` 口径 stale);
同时把 sprint-12 复盘识别出的 **「✅ 早于事实」** 病理固化为机制。

> **本 sprint 不做**:53 数值轮(U 档扩档 / 试药史阈值 / Outcome 行值)——
> 数值归用户,已在 story-002 登记为留槽。S13-2 接线时**不得**把 `bandCount` 硬编码,
> 否则数值轮一扩档即踩 N-3 的别名失效模式。

## Capacity

- **不用 `estimate_days`**(sprint-12 起废止,依 `production/CAPACITY-MODEL.md`)·
  复杂度 = S/M/L
- **件/日基线**:sprint-11 = 8 · sprint-12 = **10**(实测)⇒ 本 sprint 按 **8–10 件/日**监督
- **并行纪律**:同时在跑 ≤ 3 件;超限即拆分或顺延讨论
- **本 sprint 预算**:Must 3 件(2×S + 1×M)+ Should 3 件(3×S)+ Nice 2 件(1×user + 1×S)
  —— **无 L 件**(承 sprint-12 高出基数的成因之一,但不作目标)

## Tasks

### Must Have (Critical Path)

| ID | Task | Owner | Est | Dependencies | Acceptance Criteria |
|----|------|-------|-----|--------------|-------------------|
| **S13-1** | **story-001 收口 + `EPIC.md` 口径 reconcile**(复盘行动项 5) | Claude | **S** | 无 | ① `story-001` 经 `/story-done` 翻 `Complete`(现 `In Review`;Test Evidence 已 passing 而 Completion Notes 空 —— **全仓唯一未收口的 In Review 件**)② `medical-consequences/EPIC.md` stories 表四行按**真值**刷新(001 → Complete · 002 → Complete · 003/004 → Ready)③ epic `Status:` 行同步。**判据 = 四行状态与四个 story 文件 `Status:` 行逐字一致(可证伪:改任一行即红)** |
| **S13-2** | **story-003 实现:F-53.2 延迟发出 + `ConsequenceResolved` 世界流写入** | Claude | **M** | S13-1 · story-002 ✅ Complete · registry `ConsequenceResolved` 已直登 | ① `Outcome → DelayTable → EmitTick` 延迟调度落地 ② 世界流 `ConsequenceResolved` 写入者就位(主机唯一 `Append`,承 ADR-005)③ **N-3 的 `uBand` 真实喂入路径** —— story-003 是第一个真实喂 `uBand` 的调用点(ADR-031 重评轮 **P1** 判据),须带域守护回归 ④ 黄金夹具 + 证伪轮(定序 / 阈值边界 / 一次性三类形态必命中)⑤ 装配轮装载面登记(`IDataProvider` 读 cooked) |
| **S13-3** | **结账表 ✅ 纪律固化**(复盘行动项 1 · **High**) | Claude | **S** | 无 | `/code-review` SKILL 增两处:① 结账表**分「已落 / 已证」两列** —— 「代码改了」与「改对了且被夹具钉住」不可混填 ② 「已证」列**必须附可复算判据**(测试名 + 行号 + 证伪记录),只写「我拆了方法」即不通过。**判据 = 对 story-002 重跑一遍新模板:J 项进「已落」列而**不**进「已证」列(它首轮只拆一半)** |

### Should Have

| ID | Task | Owner | Est | Dependencies | Acceptance Criteria |
|----|------|-------|-----|--------------|-------------------|
| **S13-4** | **asmdef 引用面判读固化**(复盘行动项 2 · **High**) | Claude | **S** | 无 | 一处可复用判定:`asmdef` 的 references 须**解析 GUID → 装配名**再判存在性;名称列表不构成完整事实。判据 = 注入一条「按名补已由 GUID 覆盖的引用」⇒ 判定器报 duplicate(可证伪) |
| **S13-5** | **新测试自检前置固化为脚本**(复盘行动项 4) | Claude | **S** | 无 | 把返工期散落建成的 python 预检(括号配平 / JSON 合法性 / 断言自相矛盾)收成**一个可复用脚本** + 「何时必跑」说明。判据 = 对 `medcons_outcome_judge_test.cs` 跑出与返工期一致的结论 |
| **S13-6** | **重评专家 prompt 拆小**(复盘行动项 3) | Claude | **S** | S13-3 | `/code-review` 的重评 prompt 拆为「原判据核账」与「新缺陷 + 硬约束」**两个 agent**,或预声明「先出报告再补查」。判据 = 下一次重评两位专家均在轮次上限内交付报告 |

### Nice to Have

| ID | Task | Owner | Est | Dependencies | Acceptance Criteria |
|----|------|-------|-----|--------------|-------------------|
| **S13-7** | **S12-10 结转:11 人工走查 AC-11-12/13/21 + 三签** | **user + 三签**(主创 / 医学从业 / 音频 lead) | — | S12-2 ✅(前置已满足) | 真人走查 + 三签落盘。**agent 不可代劳**,不占 sprint 治理容量 |
| **S13-8** | 9 条 Minor 入 `tech-debt-register` + 重评件 §五 收口 | Claude | **S** | 无 | `docs/tech-debt-register.md` 建册(若不存在)并登记 9 条;每条带出处(重评件 §五 条目号) |

## Carryover from Previous Sprint

| Task | Reason | New Estimate |
|------|--------|--------------|
| S12-10 人工走查 AC-11-12/13/21 + 三签 | **agent 不可代劳** —— 需真人 + 三签(项目外部,不可催) | 转入 S13-7;前置 S12-2 已满足 ⇒ **现在就能启动** |

## Risks

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|------------|
| **story-003 是 53 链第一个「真喂 `uBand`」的调用点** —— ADR-031 重评轮 P1 判据;若装配轮用与表不同的 `bandCount` 源,越界档位会**静默别名到相邻组合** | 中 | **高** | S13-2 AC ③ 把域守护回归列为必交;N-3 守护已在 Sim 侧落地(F16 证伪),本条防的是**装配层双源** |
| story-003 依赖 story-001 收口,而 story-001 已 `In Review` 多日 | 低 | 中 | S13-1 先行且判据可证伪(状态逐字一致);它是全仓唯一未收口的 In Review 件 |
| 「建闸 / 固化」类三件(S13-3/4/5)又变没人引的规则 | 中 | 中 | 每件判据都是**可证伪动作**(改一行即红 / 跑脚本得一致结论),不是「写进文档」;S13-3 尤须以 story-002 重跑自证 |
| 53 数值轮(U 档扩档 / 试药史阈值)一直未启动 ⇒ U 判别力持续空转 | 中 | 中 | 已在 story-002 登记「U 判别力载体 = `band_count > 1`,归数值轮」;本 sprint 不启动数值轮,但 S13-2 接线时**不得**把 `bandCount` 硬编码 |

## Dependencies on External Factors

- **S13-7 三签**(主创 / 医学从业 / 音频 lead)—— 项目外部,不可催。
- CI 真 runner 环境(R-2:`UNITY_LICENSE` 未配)—— 影响 ADR-012 矩阵的 IL2CPP 格;
  **本 sprint 的 IL2CPP 三格仍登记 NOT-RUN**(禁借绿)。

## Definition of Done for this Sprint

- [x] All Must Have tasks completed(S13-1 + S13-2 + S13-3)✅ 2026-10-16
- [x] 各任务按衍生验收标准通过,**每条附可证伪判据** ✅
      (S12-4 闸门:story-003 五形态全红;S13-4 停用判据 ⇒ 2 红;S13-5 三类 ⇒ 3 红)
- [x] **QA plan 存在**(`production/qa/qa-plan-sprint-13-2026-10-16.md`)✅
- [x] story-003 的 Logic 测试件全 Passed + 证伪轮全红 ✅(107/107 绿;五形态全红)
- [x] IL2CPP 三格 **NOT-RUN 如实登记**(禁借绿,挂 ADR-012 矩阵)✅
- [x] Design documents updated for any deviations ✅(tech-debt-register 建册 + 回指)
- [x] Code reviewed(`/code-review` story-003,原件落 evidence)✅
- [x] **`end` 日期在收口时补记** ✅ —— 见下

> **✅ 收口记录(2026-10-16)**
> - **Must 3 件 + Should 3 件 + Nice 1 件(S13-8)= 7 件 done**;S13-7(需真人三签)仍 backlog。
> - **全量 EditMode 3515 / 3469 passed / 0 failed / 46 skipped**(基线 3508/3462 ⇒ **+7/+7 零回归**)。
> - **`start` 空白(未记)→ 不追补**:计划写「不设结束日期,做完即收,收口时补记结束日」;
>   实际执行 = 2026-10-16 单日。⚠️ **矛盾留档不改写**(承 sprint-12 教训)。
> - **`end` = 2026-10-16**。
> - 证据原件:`production/qa/evidence/sprint-13-governance-s13-4-5-6-8-2026-10-16.md`
>   + `review-medcons-story-003-2026-10-16.md` + `review-medcons-story-002-rework-2026-10-16.md`。

---

## 规划期勘察记录(Phase 1-5 · HARD prerequisite)

> 依 `/sprint-plan` Phase 1-5:每一条拟议任务的**前提**都对着**当下**文件核过,不照快照写。

| 拟议前提 | 当下真值 | 处置 |
|---|---|---|
| 「实现 story-003」 | story-003 确为 `Ready`,依赖 story-002 ✅ + story-001 ⚠️ + registry 已直登 | 保留,但**前置 S13-1** 先行收口 story-001 |
| 「story-002 待实现」 | **已 `Complete`(2026-10-16)** | 不作为任务;其返工/重评经验转为本 sprint 的纪律固化件 |
| 「story-001 已收」 | **仍 `In Review`**,Completion Notes 空,Test Evidence 已 passing | **立 S13-1** —— 全仓唯一未收口的 In Review 件 |
| 「EPIC 口径已刷新」 | `EPIC.md` stories 表四行全 `Ready`,epic `Status: Ready` | 并入 S13-1 一并 reconcile |
| 「M3 = 22/31」 | 实测 Complete epic = **22 / 31**,与 README 一致 ✅ | 无需修订;medical-consequences 是 9 个 Ready epic 之一 |

**全仓未收口面清点**(Phase 1-5 实测):51 个非 Complete story 中,**仅 1 个 `In Review`**
(`medical-consequences/story-001`),其余 50 个均为 `Ready`(真·未开工,非治理债)。
⇒ 本 sprint 的治理债面 = **story-001 收口 + EPIC 口径**,无其他隐性欠账。
