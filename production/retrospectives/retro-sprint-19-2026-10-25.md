# Retrospective: Sprint 19 —— AC 门清偿(G-4 · G-5 · G-6 · G-7)

Period: 2026-10-24 -- 2026-10-25
Generated: 2026-10-25(收口轮,与收口判定段同批落盘)

---

## Metrics

| Metric | Planned | Actual | Delta |
|--------|---------|--------|-------|
| 计划内 story | 5(L×1 + M×3 + S×1) | 5 全 done | 0 |
| 计划内完成率 | — | **100%** | sprint-18 的 66.7% → 回满 |
| 追加工作 | 预留额度 1 件 | **远超预留**(见下) | ⚠️ |
| Commits | — | 28(`git log --oneline --since=2026-10-24`,本轮实跑) | — |
| S1/S2 bug | 0 | 0(`production/qa/bugs/` 空) | 0 |
| QA 签核发现 | — | 6 条条件(C-1…C-6;4 处置 / 2 如实登记) | — |
| code-review 发现 | — | 10 条(W1–W10);3 必修已修,W6–W10 登记未修 | — |
| 新立 P0 面债 | — | +5(`TD-persist-04/05/06/07` + `TD-randomevents-02`) | +5 |
| 结清 P0 面债 | — | −12(`TD-persist-01/03/04/05/06/07` · `TD-enemy-01/02` · `TD-player-02/03` · `TD-randomevents-01/02` · `TD-epic-01-f`) | 净 −7 |

**追加工作的实际构成**(预留 1 件额度被显著突破,逐项具名):
PRE-S19-A/C/D 三处入场阻塞裁定轮(当日掀出 `TD-persist-04/05/06/07` 四债)·
`TD-persist-05` 动作①②③④ 四个兑现轮 · `TD-persist-06` 兑现 · `TD-persist-04` 动作① + 义务②
两轮 · 52 GDD 轮 + `TD-randomevents-02` 实现 + 52 生产接线 · QA 收口轮 · 评审修复轮(W1/W4/W5)·
TD-enemy-02 + TD-player-03 双实现收敛轮(C-6 裁定驱动)。

## Velocity Trend

| Sprint | 计划件 | 计划内完成 | 追加 | 计划内完成率 |
|--------|--------|-----------|------|-------------|
| 17 | 6 | 6 | 0 | 100% |
| 18 | 3 | 2 | 3 | 66.7% |
| **19**(current) | **5** | **5** | **(见上,远超 1 件预留)** | **100%** |

**Trend**: **计划内完成率回满** —— sprint-18 的结转(S18-1)按四条入口判据全额兑现进 S19-1,
无新结转。但**追加率指标本 sprint 失真**:预留按 `[0%, 57%]` 区间给了 1 件额度,实际追加轮次
以「轮」计不以「件」计 —— 追加的工作大多**结成了新债条目**(可复算:+5 条)而非新 story。

## What Went Well

- **四门(G-4…G-7)全部清偿** —— 本 sprint 的存在理由兑现:M3 Exit 技术侧无阻塞项
  (§Required ADRs #1–#5 全 Accepted 的下一层)。路线图 §6.5 语义成立。
- **证伪轮纪律全程稳定**:S19-2 F1–F3(3 轮)· S19-3 M1'–M8(8 轮,**含 M1 假绿夹具被自己
  的证伪轮抓出重构** —— S12-4 门的又一次实战生效)· S19-4 M1–M9(9 轮)· 评审修复 4 轮 ·
  收敛轮 2 轮;全部同命令改→跑→还原 + SHA-256 逐字节,`git checkout` 禁令零违反。
- **retro-18 五条行动项 5/5 兑现**:① 数字复算命令(已立 BLOCKING 规,本 sprint DoD :403 实跑)
  · ② Branch 列(落 DoD :451)· ③ CAPACITY-MODEL 补 16/17/18(已补)· ④ S18-1 结转判据
  入计划正文(已入且兑现)· ⑤ 「有门」自述带 file:line(已立规)。**前 sprint 行动项零悬空**。
- **假绿病灶家族继续清除**:收敛轮把「AC 语义层双实现」同族两件(player / enemy)一次清完,
  五个夹具归一,且 AC-1-05 断言在迁移中**升级为更强判据**(provider=999 / 边沿=42)。
- **「如实登记」文化经受住了考验**:QA 两条条件按「如实登记未满足」处置而非粉饰;W6–W10
  五条未修项逐条具名;收敛轮漏扫(7 处消费面)在证据件里原文留痕。

## What Went Poorly

- **入场裁定轮掀出 4+1 条新 P0 债** —— sprint 计划的依赖勘察在「每件实现面」粒度仍不足
  (承 retro-18 Process Improvement 1:重扫粒度到件集了,实现面没到位)。好在 Phase 1 重扫
  **当日**抓出,未演变成中途爆炸 —— 但这靠人盯,不靠流程。
- **算术性低估第三次**:S19-1 构造形参「3 → 5」漏算 `Checkpoint` 数据源(实为 6,当日订正)。
  与 sprint-18 的「低估 4 倍」同根:**解耦面的枚举是估算,不是勘察**。
- **收口动作滞后(「做完 ≠ 记完」第三连)**:判据面 10-25 上午就齐,头部收口日 / §九 / retro
  拖到收敛轮之后才补(sprint-17 / 18 均登记过同一条)。每次都靠用户显式指令触发。
- **QA 签核自陈「未独立复跑全量测试」**(未及核查项 #1):采信 XML 实体而非重跑 ——
  预算约束下的如实登记,但这意味着签核对「XML 造假」不可见,该残余不应被遗忘。

## Blockers Encountered

| Blocker | Duration | Resolution | Prevention |
|---------|----------|------------|------------|
| PRE-S19-A/C/D 三处入场阻塞(叙事上下文查询无归属 / Load 字节半前置 / 瞬时源) | 当日(10-24) | 三裁定全结(甲案:注入委托 + fail-closed / 字节半并入 TD-persist-03 / W5 后改丙案推迟 10) | 依赖勘察进实现面(见行动项 2) |
| W5 语义发现(累积 vs 瞬时混淆,AC-7a-19(c) fail-open) | 半日 | 用户裁定丙:回退 null,瞬时源归 10 | 教训已落会话记忆:「字面兑现裁定 ≠ 满足 AC」 |
| C-6 双实现悬空自述 | 半日 | 用户裁定「都入债册」→ 当日收敛轮还清 | 已有机械门族(NoHealthbarFeedbackTest 同型),本案属 GDD 面无法机械化,靠签核 C 条件兜住 |
| Unity batch 独占(多轮测试排队) | 零散 | 排队轮转,`pgrep` + lockfile 前检纪律生效 | 保持现状 |

## Estimation Accuracy

| Task | Estimated | Actual | Variance | Likely Cause |
|------|-----------|--------|----------|--------------|
| S19-1 | L(含 TD-persist-01 剩余两项并入) | 远超 —— 掀出 4 条新 P0 债 + 四个兑现轮 | 大幅低估 | 入场阻塞是**新依赖**,债册描述时不存在 |
| S19-2 | M | done + 引出 C-3(TD-player-03) | 件内准,残留面漏 | 双实现属跨件病灶,单件视角不可见 |
| S19-3 | M | done + 引出 TD-enemy-02 | 同上 | 同上 |
| S19-4 | M | done(21 新 + 7 改,与实收对账一致) | ≈准 | — |
| S19-5 | S | done | 准 | — |

**Overall**: 计划内 5 件中 2 件估算准、2 件「件内准但掀出新债」、1 件大幅低估。
规律与前两 sprint 一致:**误差不在做,在勘察** —— 每次动手都发现依赖面比债册描述的大。

## Carryover Analysis

| Task | Original Sprint | Times Carried | Reason | Action |
|------|----------------|---------------|--------|--------|
| (无 story 结转) | — | — | S18-1 判据已全额兑现进 S19-1 | — |
| W6–W10(五条 code-review 未修) | sprint-19 | 0 | 各自具名登记,非 BUG 级 | **sprint-20 规划时显式盘点**(承接 or 立债),防「登记即蒸发」 |
| QA 签核 2 条如实登记条件 | sprint-19 | 0 | 同上 | 同上,与 W6–W10 一并盘点 |

## Technical Debt Status

- 当前 TODO 计数: **14**(`grep -rn "TODO" unity/Assets --include=*.cs | wc -l` 本轮实跑 = 14;
  FIXME 0 · HACK 0)—— **与 retro-14 / retro-18 的 14 持平**(口径警示同 retro-18:14 条里
  约含 4 条护栏文本,非真债)。
- 债册(三十订实跑):条目 **109** · `closed 34 / open 68 / partial 6` · **未闭 P0 面 69**,
  分布 **9/31 epic**(player-controller 与 enemy-ai 双双清零)。
- **趋势:净改善** —— 未闭 P0 面 76 → 69(−7),且净减是在**掀出 +5 新债**之后达成的;
  三大块(medical-consequences 20 / audio-system 20 / clinic-machine 17)占未闭 P0 面 **77%**,
  是 M3 Exit 的主战场。

## Previous Action Items Follow-Up(retro-18 五项)

| # | Action Item(retro-18) | Status | Notes |
|---|---|--------|-------|
| 1 | 数字带可跑复算命令 | **Done** | 2026-10-22 立 BLOCKING 规;本 sprint DoD :403 / 证据件多处实跑 |
| 2 | 分支覆盖列 | **Done** | DoD :451 双列 + Branch 列勾选 |
| 3 | CAPACITY-MODEL 补 16/17/18 | **Done**(2026-10-22)| **sprint-19 行待本轮补**(见行动项 1) |
| 4 | S18-1 结转判据写进计划正文 | **Done** | §S19-1 四条入口判据全兑现 |
| 5 | 「有门」自述给 file:line + 可跑命令 | **Done** | 2026-10-22 立规,本 sprint 执行 |

## Action Items for Next Iteration

| # | Action | Owner | Priority | Deadline |
|---|--------|-------|----------|----------|
| 1 | **CAPACITY-MODEL.md 补 sprint-19 行**(本轮收口即做,别再让表变化石) | Claude | High | 本轮收口批 |
| 2 | **Phase 1 重扫增加「实现面对账」步**:对每件,把债册「解除动作」逐句翻成可 grep 判据后**当场跑**,并把「将掀出几条新债」的预估写进计划(估算是勘察,不是算术) | Claude | **High** | sprint-20 规划 |
| 3 | **收口四件套同日化**:最后一个实现轮 commit 当天完成 头部收口日 + §九 + retro + CAPACITY-MODEL 行 —— 已三连滞后,写进 sprint 模板 DoD 尾部 | Claude | Med | sprint-20 收口 |
| 4 | **W6–W10 + QA 2 条登记条件显式盘点**:sprint-20 规划时逐条决定「承接 / 立债 / 显式豁免」,不得默认滚存 | Claude | **High** | sprint-20 规划 |
| 5 | **medical-consequences(20 条)整块处置策略**:它是最大未闭块且全部挂在数值轮 / 复评批前置上 —— sprint-20 规划时决定「等数值轮整块解锁」还是「挑前置无关的子集先清」 | Claude + 用户 | Med | sprint-20 规划 |

## Process Improvements

1. **勘察替代估算**(行动项 2 的机制化):sprint-plan Phase 1 重扫已存在,本 sprint 证明它
   在件集粒度有效(当日抓出三处入场阻塞),但实现面仍靠临场。补一步「解除动作逐句 → grep
   判据 → 当场跑」,预期把「掀出新债」从临场惊喜变为计划期已知项。
2. **收口四件套模板化**(行动项 3):把「头部收口日 / §九 / retro / CAPACITY-MODEL 行」写进
   sprint-plan 的 DoD 模板尾部,让收口由 checklist 驱动而非记忆驱动。

## Summary

sprint-19 是**高质量的一轮**:计划内 5/5、四门全清、完成率回满、前 sprint 行动项零悬空、
证伪轮零违纪;P0 面债净 −7 且两条 epic 名下清零。最大的系统性问题依然只有一个:**勘察粒度
落后于实现粒度** —— 每件都比债册描述的大,增量全靠临场裁定与当日重扫兜住。下一 sprint 最值
得做的改变:把「解除动作逐句翻成可 grep 判据并当场跑」变成规划期的硬步骤,让「将掀出几条新债」
在计划期可见。
