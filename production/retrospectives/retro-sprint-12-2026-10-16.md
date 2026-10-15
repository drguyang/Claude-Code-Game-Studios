# Retrospective: Sprint 12 — 治理债结清

**Period**: 2026-10-15 — 2026-10-16 · **Generated**: 2026-10-16
**Sprint 文件**: `production/sprints/sprint-12.md` · **状态真源**: `production/sprint-status.yaml`
**周期口径**: 计划写「2026-10-16 起,不设结束日期」;git 实证 **10 件提交全部落在 2026-10-15**
(计划声明日晚于实际首执行日 1 天 —— 矛盾已如实留档于 sprint-12 §收口记,**未改写 `start` 原文**)。
**`end` 日期**:2026-10-16(依「做完即收,收口时补记结束日」裁定)。

---

## Metrics

| Metric | Planned | Actual | Delta |
|---|---|---|---|
| Tasks | 11 | 10 | **-1**(S12-10 结转) |
| Completion Rate | — | **91%** | — |
| 估点(Effort Days) | **不适用** | — | 新口径明令废止 `estimate_days` |
| 评审发现(首轮 + 重评) | — | **26 条**(3 BLOCKING + 23 WARNING/MINOR/INFO) | 全部已修 |
| **假绿 / 判别力不足被抓** | — | **3 处** | 全在证伪轮暴露 |
| 返工自犯错误 | — | 2 处(X-1 GUID · X-2 括号) | 均被编译红当场捕获 |
| Unplanned Tasks Added | — | 1(S12-11,承接 S12-9 退回) | — |
| Commits | — | 3(sprint-12 归属)+ 14 未提交收口件 | — |

> ** commits 口径**:sprint-12 归属提交 = `9960e41`(治理批:计划 + QA 计划 + 复盘 + S12-2/S12-6/S12-8 原件)·
> `b9f400a`(S12-9 story-002 Judge)· `46873d1`(S12-11 ADR-031)。其余 10-15 早段提交属 sprint-11 收尾。
> 本 sprint 的返工 / 重评 / 收口件(14 个文件)截至本件生成时**未提交** —— 依「No commits without user instruction」。

---

## Velocity 趋势

| Sprint | 计划 | 完成 | 率 |
|---|---|---|---|
| 09 | 3 | 3 | 100% |
| 10 | 1 | 1 | 100% |
| 11 | 17(始 6) | 16 | 94% |
| **12(本轮)** | **11** | **10** | **91%** |

**趋势**:量级回落(17 → 11),完成率稳定高位。

**产能(依 `production/CAPACITY-MODEL.md` 三旋钮)**:
- **复杂度 C**:S×4(S12-0/1/6/8)/ M×5(S12-2/3/4/11/12)/ 未估×2(S12-9 重开件 · S12-10 人工件)—— **无 L 件**
- **并发上限 P**:峰值 2 件同时在跑(S12-9 + S12-11 同族依赖可并行勘察)⇒ **未超 3 上限,无流程旗标**
- **判别力门槛 D**:**10 件 done 全部有可证伪判据,0 件未证伪**
  - 测试型「回退即转红」6 件:S12-2 · S12-4 · S12-6 · **S12-9(18/18 RED)** · S12-11(3/3 全红)· S12-12
  - 断言型(可逐条核、无测试面)3 件:S12-0(五处补注)· S12-1(判据 = 本次 `/sprint-status` 引用它)· S12-3(三次违规回填)

**件/日 = 10 件 / 1 执行日**,高于 sprint-11 基线(16 件 / 2 执行日 = 8 件/日)**25%**。
差异来源 = ① 件均复杂度低于 sprint-11(无 L 件)② agent 并行实到 2 路
③ 治理债类判别力判据较轻(断言型占 3/10)。

---

## 做得好

- **S12-4 闸门首次实战即抓出 3 处判别力不足** —— 其中两条(F2 / F15b)是返工轮**自己刚写**的探针。
  这正是该闸门存在的理由,且它抓的是自己。
- **重评独立抓到 N-3**:`Table.TryLookup` 不校验 `uBand` 域 ⇒ 越界档位使 key 越出本 (p,c,d) 段、
  **落入下一个组合的段内** ⇒ 静默返回**另一组合**的 Outcome。这是返工**新引入**的暴露面
  (返工前硬编码 0/1 反而有界),而首轮评审已批准 B 的修法 —— **只有独立重评能抓到**。
- **QA 抓到 F1 子断言② 不可判别**:现网 `schema_version == u_band_count == 1` ⇒
  旧布局(载荷首字段仍是 schema_version)下同样绿 ⇒「第二真源已消」这半句原本**无任何夹具钉住**。
- **产能模型判据真兑现**:`/sprint-status 12` 输出含「### 产能(无结束日期 —— 依
  `production/CAPACITY-MODEL.md`)」段,三旋钮逐项给出 —— **不是一份没人引的文档**。
- **sprint-11 五项行动项 5/5 闭环**(含「补 Driver `passed` 汇总 bug」这条中优先级项)。

---

## 做得不好

- **J 项首轮只兑现一半就被标 ✅** —— `Bind` 110 行拆成 `ParseRows`(69 行)+
  `ValidateCoverage`(31 行),**单体仍超 40 行标准**。结账表的 ✅ **早于事实**。
- **返工迭代 8 轮里 4 轮花在修自己写的测试上**:
  ① 前置断言被子串 `u_band_count` 骗过(裸 `Contains("u_band")` 恒真)
  ② 目录路径少跳一级(`ConsequencesDirRelative` 相对工程根,非 `Application.dataPath`)
  ③ 删行留双逗号致 JSON 词法失败
  ④ 断言自相矛盾(消息写「档 1 或 0」却 `AreEqual(0, …)`,而真值是 1)
- **X-1 侦察错误**:判 H 项只读 asmdef **名称列表**、漏看 GUID 形引用 ⇒
  按名补一条 `"Editor.Tools.Gates"` 触发 `Assembly has duplicate references` **编译红**。
- **两位评审专家都撞轮次上限未出报告** —— 需 SendMessage 续跑才交付;
  重评 prompt 要求「14 项逐条核 + 硬约束逐条实测」,单 agent 10 / 20 轮不够。

---

## Blockers Encountered

| Blocker | Duration | Resolution | Prevention |
|---|---|---|---|
| asmdef duplicate references 编译红 | 1 轮 | 回退按名补的引用(GUID `09b3130c…` 早已覆盖) | **asmdef 引用面须解析 GUID 到装配名再判**;名称列表不是完整事实 |
| CS1026 括号少一个 | 1 轮 | 补配对 `)` | 括号改配平加**代码级预检**(返工期已建 python 预检) |
| 专家 10 / 20 轮上限未出报告 | 2 次 | SendMessage 续跑交付 | 重评 prompt 拆小 / 预声明「先出报告再补查」 |
| F1 子断言② 不可判别 | 1 轮 | 改**推导载荷长度**断言(旧布局会多 4 字节) | 断言语须问「**旧实现下会怎样**」,不能只问「新实现下通过吗」 |
| 驱动器双重路径前缀 | 1 轮 | 改传相对路径 | 脚本内 `"$ROOT/$file"` 与传入绝对路径混用 ⇒ 统一相对路径约定 |

---

## Estimation Accuracy

**不适用** —— sprint-12 起明令废止 `estimate_days`(`CAPACITY-MODEL.md` §一:
旧「solo 人日」口径与实测吞吐脱节 ≈2.6×,超容规则因此从未生效)。
新口径 = 复杂度(S/M/L)+ 依赖数 + 判别力门槛,**不编估点方差**。

---

## Carryover Analysis

| Task | Original Sprint | Times Carried | Reason | Action |
|---|---|---|---|---|
| S12-10 人工走查 AC-11-12/13/21 + 三签 | 12 | 1 | **agent 不可代劳**,需真人 + 主创 / 医学从业 / 音频 lead | **sprint-13 首选** |

---

## Technical Debt Status

- TODO **15** / FIXME **0** / HACK **0** —— 与 sprint-11 **完全相同** ⇒ **Stable**(零增长)
- ⚠️ 口径仅扫 `unity/Assets/`(承 sprint-11 同口径;`tests/` 与 `tools/` 未扫,非全仓口径)
- **新增登记面**:重评件 `review-medcons-story-002-rework-2026-10-16.md` §五 9 条 Minor
  (golden `C01_degraded_uraw` 孤儿行 · 5 组合仅测试内镜像兜底 · AC-53-03 `FixParse` 半句无载体 ·
  门构建期注册层检不出「注册被删」 · `Materialize` 碰撞分支无直驱 · `Judge.Evaluate(null)` 无测试 ·
  共享静态池无 teardown · EditMode 真实文件 I/O · N-7 verbatim/插值字符串不在源级判据面内)
  —— **尚未入 `docs/tech-debt-register.md`**,建议行动项 1 同批处理。

---

## Previous Action Items Follow-Up(sprint-11 五项)

| # | Action Item | Status | Notes |
|---|---|---|---|
| 1 | 重建产能模型 | **Done** | `production/CAPACITY-MODEL.md` 落盘;**判据(收尾时被 `/sprint-status` 引用)已兑现** |
| 2 | 处方前置检查 | **Done** | `docs/architecture/control-manifest.md:536` §Cross-Cutting Constraints 之七 |
| 3 | 假绿夹具必过证伪轮 | **Done** | `.claude/skills/story-done/SKILL.md:145` §Falsifiability Gate(含 S11-9 假绿案例具名 + BLOCKING 措辞) |
| 4 | 补 Driver `passed` 汇总 bug | **Done** | S12-2:真值**完全不经字符串**(`OkCount`/`FailCount` 累计)+ 空腿显式补红;原 `checks.All(c => c.StartsWith("✅"))` 在 Mono 下误判 `🔴` |
| 5 | case-system EPIC 状态刷新 | **Done** | `production/epics/case-system/EPIC.md` 6/6 Complete |

---

## Action Items for Next Iteration

| # | Action | Owner | Priority | Deadline |
|---|---|---|---|---|
| 1 | **结账表的 ✅ 不得早于事实** —— 「已结清」须附**可复算判据**(测试名 + 行号 + 证伪记录),不能只附「我拆了方法」;并区分「已落」与「已证」两列 | Claude | **High** | sprint-13 每个 `/code-review` 收尾 |
| 2 | **asmdef 引用面判读须解析 GUID 到装配名** —— 名称列表不是完整事实 | Claude | **High** | sprint-13 起手 |
| 3 | **重评专家 prompt 拆小 / 预声明「先出报告再补查」** —— 本轮两位专家均撞轮次上限未交付 | Claude | Med | sprint-13 首次 `/code-review` |
| 4 | **新测试自检前置固化为可复用脚本** —— 括号配平 + JSON 合法性 + 断言自相矛盾(返工期已散落建成 python 预检) | Claude | Med | sprint-13 |
| 5 | **reconcile `medical-consequences/EPIC.md`** —— stories 表四行全 `Ready`,而 002 本次已 Complete、001 亦已收;同批把重评件 §五 9 条 Minor 入 `docs/tech-debt-register.md` | Claude | Med | sprint-13 起手 |

---

## Process Improvements

1. **证伪轮的「停用 X」必须真的跑一遍才能记 PASS** —— 本轮 F2 / F15b 两条照抄描述即得假绿
   (F2:键互异后任何正确排序都定序;F15b:两组夹具在两种序下结论相同)。
   判据模板应加一栏:**「旧实现下该夹具会怎样」**。
2. **返工结账表分「已落」与「已证」两列** —— 「代码改了」与「改对了且被夹具钉住」是两件事,
   本轮 J 项与我自己两处过早标记都源于把两者混为一谈。

---

## Summary

10/11 件、91% 完成率、sprint-11 五项行动项 5/5 闭环、S12-4 闸门首次实战即抓出 3 处判别力不足
(含自己刚写的两条探针),终轮 `3462 / 3416 / 0 / 46` 零回归。

**最重要的一件事:结账表的 ✅ 必须晚于可复算的判据。**
本轮 J 项(首轮只拆一半就标已结清)、以及我自己在评审过程里两处过早标记,是同一个病 ——
把「我做了动作」当成「我做对了并被证明」。行动项 1 就是治它。
