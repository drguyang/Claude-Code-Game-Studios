# Sprint 13 — 53 结算链贯通 + 收口纪律固化

**周期**:不设结束日期(承 sprint-12 口径)—— **做完即收,收口时补记结束日**
**规划依据**:`production/retrospectives/retro-sprint-12-2026-10-16.md`(行动项 5 条 + 产能结论)
**前置勘察**:Phase 1-5 已重扫 `medical-consequences` epic 现状 ——
story-002 **已 `Complete`**(2026-10-16)· story-001 **仍 `In Review`**(全仓唯一未收口的 In Review 件)·
story-003/004 `Ready` 且依赖面已满足。

> **状态(2026-10-17 最终收口)**:**✅ 已收口** —— **8/8 件 done**;S13-7 三签于 2026-10-17 完成
> (dr_guyang 兼任主创 / 医学从业 / 音频 lead,签核件 §9)。DoD 第 2 条**仍保持未勾**
> (S13-6 判据 n=1,有效性待更多样本)—— 这是**如实留痕**,不因收口而补勾。
> 落下的处置:~~戥子元件 story 待新立(`TD-prescription-01`)~~ ⇒ ✅ **2026-10-18 已还** —— story-025 立件并交付
> (S14-11),S13-7 **走查重跑**改判 A3 ✅ 可读 / B2 ✅ 可辨,`TD-prescription-01/-02/-03` 结案
> (报告 `production/playtests/playtest-2026-10-18-s13-7-rerun-dentch-form.md`)·
> 音频接入待补齐(`TD-audio-023` —— ⚠️ **仍 open**,S14-10 实机听测未跑)。

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
      —— S13-3 的兑现载体在 Should 侧(story-002 重评原件 §六 双列表),实测成立,见收口记录 §核对订正
- [ ] ~~各任务按衍生验收标准通过,**每条附可证伪判据**~~ ⚠️ **不成立(2026-10-17 核对订正)**:
      可证伪判据确已兑现(S12-4 闸门:story-003 五形态全红;S13-4 停用判据 ⇒ 2 红;S13-5 三类 ⇒ 3 红),
      但 **S13-6 的衍生验收判据(「下一次重评两位专家均在轮次上限内交付报告」)被其实战轮实测推翻** ——
      见收口记录「2026-10-17 核对订正」①。**勾撤回,改记实测未兑现**。
- [x] **QA plan 存在**(`production/qa/qa-plan-sprint-13-2026-10-16.md`)✅
- [x] story-003 的 Logic 测试件全 Passed + 证伪轮全红 ✅(107/107 绿;五形态全红)
      ⚠️ **但 story-003 卡面 AC 为 8/9 通过 + AC-5 PARTIAL**(W-4 代价表永不致死形状门未兑现,
      已转 `TD-medcons-15`)—— 本 sprint 其余 DoD 未覆盖该缺口,**2026-10-17 核对订正时挂到 sprint 层**
- [x] IL2CPP 三格 **NOT-RUN 如实登记**(禁借绿,挂 ADR-012 矩阵)✅
- [x] Design documents updated for any deviations ✅(tech-debt-register 建册 + 回指)
- [x] Code reviewed(`/code-review` story-003,原件落 evidence)✅
- [ ] **`end` 日期在收口时补记** ✅ —— 见下
- [ ] Smoke check 通过 —— **2026-10-17 用户裁定结案(按陈述登记)**:本 DoD 清单与
      `qa-plan-sprint-13-2026-10-16.md` 第 229 行均无执行记录;用户称「我在**其他 epic** 听过了,没问题」
      ⇒ 以**用户人耳陈述**补记为本 sprint 的人工冒烟证据。⚠️ **本 sprint 无独立冒烟件**
      (无可复算判据);仓内最新冒烟件 `smoke-2026-10-15.md` 只有自动面。见 `TD-s13-03`(已结案)。

> **✅ 收口记录(2026-10-16)**
> - **Must 3 件 + Should 3 件 + Nice 2 件(S13-7 + S13-8)= 8 件 done**;`S13-7` 于 2026-10-17 补完三签(原记「仍 backlog」为 2026-10-16 时点状态)。
> - **全量 EditMode 3515 / 3469 passed / 0 failed / 46 skipped**(基线 3508/3462 ⇒ **+7/+7 零回归**)。
> - **`start` 空白(未记)→ 不追补**:计划写「不设结束日期,做完即收,收口时补记结束日」;
>   实际执行 = 2026-10-16 单日。⚠️ **矛盾留档不改写**(承 sprint-12 教训)。
> - **`end` = 2026-10-16**。
> - 证据原件:`production/qa/evidence/sprint-13-governance-s13-4-5-6-8-2026-10-16.md`
>   + `review-medcons-story-003-2026-10-16.md` + `review-medcons-story-002-rework-2026-10-16.md`。

> **⚠️ 2026-10-17 核对订正 —— 收口结论改判为「收口存疑」**
> (独立复算,不照抄原收口记录;下方三条为**撤回 / 补登**,原文不删)
>
> **① S13-6 判据实测未兑现(勾撤回)**。卡面判据 =「下一次重评两位专家均在轮次上限内交付报告」;
> 其首次实战即 story-003 评审,`review-medcons-story-003-2026-10-16.md:20-22` 实测记录:
> `tools-programmer` **撞 20 轮上限零报告** · `qa-tester` **撞 10 轮上限零报告**,两者均靠「续派 + 只要报告」
> 才交回 ⇒ **判据为红,非待验**。原 DoD 第 2 条的勾把 S13-6 一并算了进去,已撤回。
> ⚠️ 同段「本 story 是 S13-6 的第 2 个独立证据点(第 1 例 = story-003 实现轮)」口径失准 ——
> 两次撞限**同在 story-003** 一件之内,不构成「第 2 个独立」;已登记不改写原文。
> **残留归处**:`docs/tech-debt-register.md` `TD-s13-01`。
> **✅ 2026-10-17 机制已改(用户裁定「增量落盘 + 收窄判据」)**:`code-review` SKILL §Phase 7b 增 ③
> 「增量落盘」—— 每位专家收到草稿路径,每核完一项立即 Append,交付 = 草稿文件非空;
> 判据收窄为「草稿在轮次耗尽前非空且覆盖清单过半」。旧判据(限内交付报告)**作废**。
> ⚠️ 新判据仍为前瞻性,须下次真实重评实测 —— 本轮**不得称已通过**。
>
> **② story-003 AC-5 PARTIAL 挂到 sprint 层**。story-003 卡面 `Criteria: 8/9 passing + 1 PARTIAL`
> (W-4 / `OQ-53-8` 代价表永不致死形状门未兑现),已转 `TD-medcons-15`;
> 而 53 epic DoD 写「AC-53-01…15 全部 verified」⇒ 音频 epic 同型风险的**小规模版**(1 条),
> **在 `TD-medcons-15` 结清前,53 epic 亦不得据「4 story 全 Complete」推定 AC 全验**。
> **残留归处**:`TD-s13-02`。
>
> **③ Smoke check 未执行、未登记(补登)**。见上方 DoD 末条。**残留归处**:`TD-s13-03`。
> → **✅ 2026-10-17 结案**:用户裁定按「我在其他 epic 听过了,没问题」的**陈述**登记;
> ⚠️ 无独立冒烟件,该陈述不可证伪为「真听过」。
>
> **核账已复算成立的部分**(逐项贴出实测):回归 `s13-full.xml` = **3515/3469/0/46**、
> `s003-final-full.xml` = **3508/3462/0/46**,逐 fullname 差集 **+7 / −0**,新增 7 条全为
> `AssemblyGateRefDupTest.*`(Δ = 新增数 ✅);`s13-4-refdup.xml` = 7/7;`MedConsequences` 107/107 Passed;
> S13-5 脚本当场重跑 = `[Test]=41 · FAIL 0 · WARN 0`;S13-1 四行 `Status:` 逐字一致;
> `WriterExistenceGate.cs:124` 豁免已撤;S13-6 在 DoD 之外的其余七件均有可证伪判据支撑。

> **✅ 2026-10-17 最终收口记录 —— 已收口(用户裁定「完成 1 2 3 4 后收口」;S13-7 同日完成三签)**
>
> 本条为**最终收口**,取代上方「收口存疑」状态(原文保留不改写)。**DoD 第 2 条保持未勾**。
>
> ### 逐条处置
>
> | # | 事项 | 处置 | 证据 |
> |---|---|---|---|
> | 1 | **S13-7 人工三签** | ✅ **已完成** —— dr_guyang **兼任**三签(主创 / 医学从业 / 音频 lead),处置已裁:AC-11-12/13 **须补**、AC-11-21 待接入后重走 | 签核件 §9 · story-005 三条 AC 卡面 · 11 EPIC 表行 |
> | 2 | **S13-6 判据** | **真跑一次重评实测新判据 ⇒ PASS(n=1)** —— A 草稿 11 行/3 项全覆盖;B 草稿 45 行/4 项全覆盖(撞 10 轮零报告,草稿在盘上) | `review-medcons-s003-recheck-2026-10-17.md` §一 |
> | 3 | **Smoke check** | **用户实机冒烟 → 「冒烟通过」**(2026-10-17,陈述登记);自动面由全量 EditMode / PlayMode 套件承载 | DoD 末条 · `TD-s13-03` |
> | 4 | **DoD 第 2 条** | **保持未勾**(S13-6 判据 n=1,有效性待更多样本);收口记录如实写明残留 | 上方 DoD 第 2 条 |
>
> ### 随本收口新增的债(不阻塞收口,均已登记)
>
> - `TD-medcons-16` 旧假绿夹具未删 · `TD-medcons-17` `BuildDueRows` 51 行 ·
>   `TD-medcons-18` 三处 S3 新缺陷(D-1 装配前必须定契约)· `TD-medcons-19` 两条弱判别夹具。
> - `TD-s13-01` 补记:S13-6 新判据首次 PASS,但 agent 侧「限内出报告」仍未修好。
>
> ### 收口时点的回归(可复算)
>
> | 套件 | 结果 | 对账 |
> |---|---|---|
> | EditMode 全量 | **3682 / 3637 / 0 / 45**(`s53-full-editmode.xml`,2026-10-17 14:46–14:48Z) | 对 sprint-13 原收口值 `3515/3469/0/46` —— **测数已随 sprint-14 增长,不可直比**;本轮内对 `s52` 逐名 **+0/−0** |
> | PlayMode 全量 | **192 / 192 / 0**(`s51-playmode.xml`,2026-10-17 14:40Z) | sprint-13 时 PlayMode 为 189/189/0(180/189 的 9 红已修) |
>
> ### 收口口径声明(禁混读)
>
> - **S13-7 的签发义务已履行**(story-005 `:86` + ADR-013 §十-B Amendment B)—— 2026-10-17 三签完成。
>   ⚠️ **兼任声明**:三视角由同一人兼任,**不声称**已获外部专业意见。
> - ~~AC-11-12 / AC-11-13 / AC-11-21 现状态 = 已签 + 处置已裁(须补 / 待接入后重走)~~
>   ⇒ **2026-10-18 重跑改判后现状态**:AC-11-12 **✅**(载体半转绿,二分两半均 ✅)· AC-11-13 **✅**(可辨,
>   `ELSE` 不触发)· 两半主创 + 医学从业双签(签核件 §6)· **AC-11-21 仍待接入后重走**(腿 C 未重跑,
>   归 S14-10 —— 禁把两条 ✅ 溢出到它)。⚠️ 本行为**现状订正**,不追改 2026-10-17 的签发记录。
> - **S13-6 新判据 n=1** —— 不得据此宣称机制已稳定。
> - **IL2CPP 三格本 sprint 仍 NOT-RUN**(承 ADR-012 矩阵,禁借绿)。

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
