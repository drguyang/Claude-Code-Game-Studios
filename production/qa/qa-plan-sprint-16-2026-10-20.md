# QA Plan: Sprint 16 — 口径复位与欠账(非 feature · 零 story)

**日期**: 2026-10-20
**生成**: `/sprint-plan`(用户裁定「写轻量 QA plan」,选项原话「零 story 的 sprint 也有测试面」)
**范围**: 5 个工作项,**零 story** —— `production/sprints/sprint-16.md`
**引擎**: Unity 6.3 LTS(6000.3.24f1)
**Sprint 文件**: `production/sprints/sprint-16.md`
**基线三元组**: 截至 `2026-10-20` · 批次 `sprint-16 规划轮(Phase 1 重扫)` · commit `be60b2c`

> ### ⚠️ 本件为何存在(与常规 QA plan 的差别)
>
> 常规 QA plan 的输入是 **story 的 AC**;本 sprint **零 story** ⇒ 没有 AC 可映射。
> 但**测试面并不因此为空** —— 本 sprint 的 5 件全部产出**判据**,而**判据本身也可证伪**:
> 一个「机械门」若改了生产码仍不红,它就不是门,是装饰。
> ⇒ **本件的作用 = 把 5 件的验据封死到「可复算」,防止「文档写完 = 做完」的假绿。**

---

## Test Summary

| 工作项 | 类型 | 自动化证据要求 | 人工验收 |
|--------|------|----------------|----------|
| **S16-1** `CAPACITY-MODEL` 三元 | Config/Data(纸面) | **无自动化** —— 判据 = 「`/sprint-status` 引用三元」,靠**引用实测**(grep)。✅ **已收口 2026-10-20**:`grep -n "记录三元\|追加率\|复杂度分布" .claude/skills/sprint-status/SKILL.md` = **8 命中**(§4b);⚠️ **判据首版被自己证伪** —— 原拟「`grep 件/日` = 0 命中」实测 **2 命中**,但两条均为**禁令句**(禁令里必然出现被禁单位名)⇒ 已收窄为「无『件/日 作对照单位』的命中」。见 `sprint-16.md` S16-1 收口注 | 无 |
| **S16-2** 三件合并 + M3 Exit 两条合取 | Config/Data(纸面) | **无自动化** —— 判据 = `ls` 实测「静态件只剩一份」+ 四处分数 `grep` 同值。✅ **已收口 2026-10-20**,四条判据逐条实测:① `ls production/milestones/p0-*.md` = **1 处**(两件已 `git rm`;`ls production/p0-*.md` 报「无此文件」);② 两条合取落在 **`README:370-385` 块**(① = `:372` · ② = `:373`,**行号随加注下移**,已按实测登记,非按规划时的 `:371`);③ 四分数 `226 / 182 / 44 / 18-31` 在路线图 §1.1·§1.2 + `sprint-16.md` + `sprint-status.yaml` + `README` **同值**;④ README **四处加注**(`:49` 表 · `:370` 块 · `:393` 复核块 · `:570` 附表),**原文一律保留不删** | 无 |
| **S16-3** 债册两列 + 门载体 | Config/Data(纸面)+ **求值脚本** | **可自动化**:M3 第二条合取的**求值命令**须可跑并给出当前值(如实报 **≠ 0**)。✅ **已收口 2026-10-20** —— 命令实跑输出:`条目 91 \| 状态 {open:70, partial:5, closed:16} \| P0 面 {Y:87, N:4}`,`M3 Exit 第二条合取 ⇒ 假 —— 72 条(未闭),分布在 12/31 个 epic`(**如实报 ≠ 0,未为好看而判非 P0**);⚠️ 初版命令漏过滤已闭 15 条 ⇒ 曾报 87,已修;⚠️ `N:2, 待裁:2` → `N:4` 系同日待裁四条裁定为 `N`(出口量 72 不变) | 无 |
| **S16-4** 「测试/工具不得写仓库」机械门 | **Logic / 门** | **必须自动化 + 必须证伪** —— 见下 ✅ **2026-10-20 已结**:17 条测试 + F1/F2/F3 全 `exactRed` | 无 |
| **S16-5** EPIC.md 4 户分母刷账 | Config/Data(纸面) | **无自动化** —— 判据 = 四份 EPIC.md 的分数分母 == 实际 story 文件数(脚本可核) | 无 |

> ⚠️ **四件是纸面件** —— 它们的证据形态**只能是「可复算命令 + 预期输出」**,不是测试。
> 本件因此把判据写成一个统一的**证据形态要求**:每件收口时须贴出命令与输出,
> 且输出须能被第三方重跑复现。**没有命令的件不计 done**(承 sprint-16.md DoD)。

---

## Automated Tests Required

### S16-4 — 「测试/工具不得写仓库」机械门(本 sprint 唯一的真测试件)

**载体路径**: 待定 —— 按既有门惯例落 `unity/Assets/Editor.Tools.Gates/`(Editor 装配)
**账本路径**: `tests/unit/`(若走 .NET 层)或 `unity/Assets/Tests/EditMode/`(若走 UTF)—— 收口时**写明实际落地那棵树**

**要测什么**:
- **正例面**: `grep` 生产测试树里的 `File.WriteAllText` / `File.AppendAllText` /
  `Directory.CreateDirectory` / `StreamWriter` / `File.Delete` 命中 ⇒ 门工作
- **白名单面**: 已明知合法的例外(如 S14-6 的烘焙写面、`tools/` 下的编辑期工具)**必须显式列白名单**,
  且白名单项**每条附理由**(禁「白名单 = 全部命中项」这种自我豁免)
- **负例面(判别力)**: 停用门 ⇒ 命中项**不再报** —— 证明门真的在拦

**边界/证伪用例(本件的核心判据)**:

| # | 变异 | 期望 |
|---|------|------|
| F1 | 在生产测试树**注入**一处 `File.WriteAllText` | 门 **红**,且报文点名文件与行号 |
| F2 | 把白名单**清空** | 既有白名单项转红(证明白名单真被读) |
| F3 | 停用门的调用点 | F1 的注入**不再被拦**(证明判别力来自生产门,非夹具自证) |

> ⚠️ **判别力定义(承 TD-epic-02 的最贵教训)**:**「改坏生产码 ⇒ 断言转红」**,
> **不是**「删断言 ⇒ 变绿」(后者 trivially true)。本条门的证伪**必须**走 F1/F3 形态。

> ### ✅ 结清(2026-10-20 实现轮 —— 与上文的差异逐条登记)
>
> | 上文写法 | 实际落地 | 差异性质 |
> |---|---|---|
> | 载体路径「待定」 | **门** = `unity/Assets/Editor.Tools.Gates/FsWriteBoundaryGate.cs`(Editor 装配,读 `.cs` 文本);**测试** = `unity/Assets/Tests/EditMode/DataPipeline/fs_write_boundary_gate_test.cs`(UTF,17 条) | 二者**都**成立,不是二选一 |
> | 「grep + 白名单」 | **两条规则**:R1 字面量目标 ⇒ 红;R2 写盘 **且** 代码锚点 ⇒ 须具名声明 | 名单式判据实测会逼出 6 条无差别声明,**且漏掉两个真违规**(它们的写目标是 foreach 变量);锚点判据零误报 |
> | 白名单含「S14-6 烘焙写面 / `tools/` 编辑期工具」 | **工具侧整块不在扫描面**(用户裁定「仅测试树」) | 否则只能整目录白名单 = 自我豁免 |
> | F2 = 把白名单**清空** | F2 = **停用声明查找**(`Declaration decl = null`) | 等价且更强:清空表 = 6 个文件全红;停用查找 = 同一效果,但证明的是「查找被调用」 |
> | F3 = 停用门的**调用点** ⇒ F1 注入不再被拦 | F3 = 停用**锚点识别** ⇒ 合成夹具转红 | ⚠️ **未在叠变异态实测** —— 驱动不允许叠变异;已在 spec `_note` 逐字登记 |
> | 证实测「报文点名文件与行号」 | `test_reportedLine_matchesSourceLine` 钉住 | — |
>
> **结果**:三条变异 **全 `exactRed`**,退出码 0;全量回归 `4071 / 4035 / 0 / 36`
> (对照 `4054 / 4018 / 0 / 36`,Δ = +17,消失 0)。原件
> `production/qa/evidence/impl-s16-4-fs-write-boundary-2026-10-20.md`。

**预估**: ~6–10 条测试函数(正例 / 白名单 / 判别力三面)+ 3 条证伪变异。

### S16-3 — M3 第二条合取的求值命令(自动化,但非「测试」)

**✅ 已收口(2026-10-20)** —— 命令已落 `docs/tech-debt-register.md` **§★ 总索引 §五**,
夹在 `<!-- TD-INDEX:BEGIN -->` / `<!-- TD-INDEX:END -->` 标记之间(表与命令同一处,防漂移)。
**实跑输出(逐字)**:

```text
条目 91 | 状态 {'open': 70, 'partial': 5, 'closed': 16} | P0 面 {'Y': 87, 'N': 4}
未闭 P0 面 = 72 （已闭但曾标 P0 面的 15 条不计）
M3 Exit 第二条合取（31 个 epic,每个名下「未闭 TD 的 P0 面 = 0」）⇒ 假 —— 72 条,分布在 12/31 个 epic
    audio-system: 30 · medical-consequences: 20 · clinic-machine: 16 · skeuomorphic-ui: 5 ·
    telemetry-analytics: 5 · disease-simulation: 2 · player-controller: 2 · persistence-service: 2 ·
    prescription-medication: 1 · patient-ai: 1 · enemy-ai: 1 · random-events: 1 · item-database: 1
```

> ⚠️ **该输出不是绿** —— 它把 M3 的出口量**如实显示为 72 条**。禁为好看而判非 P0。

> ### 🔴 **2026-10-21 订正(C-2 · QA 签核轮实测发现):上行分布是错的口径混写**
>
> **病症**:上行标称「**未闭** P0 面 = 72」,但紧随的逐 epic 分布**求和 = 87**
> (`30+20+16+5+5+2+2+2+1+1+1+1+1 = 87`),且多列了一个 `prescription-medication: 1`。
> 87 = **曾经/现在属于 P0 面的静态条目数**;72 = **仍需偿还的**(87 − 已闭 15)。
> ⇒ **同一代码块里混了两个口径**,读者无法判断哪半边可信。
>
> **逐条实证(不是推断)**:
> - `prescription-medication` 只有 **1** 行,即 `TD-s13-04`,其状态 = **`closed`**
>   ⇒ 它**只在 87 口径里**,不在 72 口径里。它出现在「未闭分布」中就是混写的铁证。
> - 实测的**正确未闭分布**(2026-10-21 重跑 §五 求值命令):
>   `medical-consequences 20 · audio-system 20 · clinic-machine 16 · telemetry-analytics 5 ·
>   skeuomorphic-ui 2 · disease-simulation 2 · persistence-service 2 · patient-ai 1 ·
>   player-controller 1 · enemy-ai 1 · random-events 1 · item-database 1` = **72 / 12 epic**。
>   与 `docs/tech-debt-register.md` §五 的「当前预期输出」**逐字一致**。
>
> **结论:错的只有本 QA plan 这一处,债册 §五 与 `smoke-2026-10-21.md` §4.2 都是对的。**
> 两点都对(总数 72 · epic 数 12)⇒ **不是借绿,是抄写时把静态分布贴进了动态口径**。
> **原文保留不删**(它记录的是当时写下的样子);**判据以本订正块为准**。
> ⚠️ **表内有一列不可机械复算**:`状态`(91 行逐条读原文判定 —— 实测 5 处 `✅` 在
> **非结案语境**,机械判会把勘验结论读成结案)。该列如需第三方复核,**复核对象是原条原文**,
> 不是本表;两者不符时以原文为准(§★ 总索引 §三 已明写)。
> ⚠️ **口径订正一处**:本行的「**不可机械求值**」是**收口前**的状态,原文保留不删 ——
> 收口后该条合取**可求值了,只是值为「假」**(72 ≠ 0)。「可求值」与「已满足」是两件事。

---

## Manual QA Checklist

**无。** 本 sprint 零 story、零 UI、零 Visual/Feel 面 ⇒ **无人工验收项**。

> ⚠️ 唯一沾人的是 S16-3 的「**P0 面待裁项**」—— 那些**不是本 sprint 的验收项**,
> 而是要**交用户裁定**的清单。它们的处置是「登记 + 交裁」,**不是**「本 sprint 做完」。
> ✅ **2026-10-20 已裁完毕**:4 条全判 `N`,**无遗留人工项** —— 本 sprint 至此**仍为零人工验收**。

---

## Smoke Test Scope

本 sprint 无播放面,smoke 面收敛为**回归与门**:

1. **全量 EditMode 回归** —— 基线 `3940 / 3904 / 0 / 36`(`TD-player-01` 修复后首次真正零红);
   S16-4 落门后须**零回归**(门是新增,不应改变既有计数;若改变,须说明新增测数并核对等式 `Δtotal = Δpassed`)
   → ✅ **已跑(2026-10-20 23:45 CST · sprint-16/S16-4/全量 · commit `f6cdaaf`)**:
   `4071 / 4035 / 0 / 36`(件 `unity/Logs/s16-4-full-editmode.xml`),**failed 0**。
   ⚠️ **基线订正(原文保留)**:本行记的 `3940 / 3904 / 0 / 36` 是**本 QA plan 规划时刻**的快照;
   同日下午 clinic-machine story-001 修复轮先期落了两批全量 —— `4041`(18:44,`clinic-001-editmode-final.xml`)
   → **`4054`(19:10,`clinic-001-fix-full.xml`)**。⇒ **真正的对照基线 = `4054 / 4018 / 0 / 36`**,
   **Δ = +17 / +17 / 0 / 0**。**差集实测**:新增 17 条全为 `FsWriteBoundaryGateTest.*`,**消失 0 条**。
   若仍拿 `3940` 当对照,`Δ = +131` 会把 114 条差额误读成本门新增(或被掩盖的回归)
   —— 即 `feedback_borrowed_green_regression_counts` 的第一形态。
   证据原件:`production/qa/evidence/impl-s16-4-fs-write-boundary-2026-10-20.md`
2. **S16-4 的证伪轮** —— F1/F2/F3 三条,逐条「变异 → 跑 → 还原」,**同一命令内**完成,
   还原后做 **SHA-256 逐字节校验**
   → ✅ **已跑(2026-10-20)**:三条**全部 `exactRed`**(转红集合恰等于预期集合),
   驱动退出码 **0**;SHA-256 校验通过、受保护路径指纹未变、`grep -c FALSIFY-F` = 0 / 0。
   规格 `tools/falsify/specs/s16-4-fs-write-boundary.json`。
   ⚠️ **口径差异如实登记**:本行对 F3 的措辞是「停用门的调用点 ⇒ F1 的注入不再被拦」;
   驱动**不允许叠变异**,故 F3 实测的是**判别力源头**(停用锚点识别 ⇒ 合成夹具转红)。
   它与「注入不再被拦」是同一件事的合成形态,但**未在注入态下实测后者** ——
   已在 spec 的 `_note` 里逐字写明,不借绿。
3. **S16-2 的合并实测** —— ✅ **已跑(2026-10-20)**:`ls production/milestones/p0-*.md` ⇒ **只 1 处**
   (`p0-remaining-roadmap-2026-10-20.md`);`ls production/p0-*.md` ⇒ **No such file**(backlog 已 `git rm`)
4. **四分数同值核** —— ✅ **已跑(2026-10-20)**:`226 / 182 / 44` 与 `18/31` 在**五处**同值
   (路线图 §1.1 · §1.2 · `sprints/sprint-16.md` · `sprint-status.yaml` `baseline` · `milestones/README.md` 加注块)
5. **S16-3 的求值命令实跑** —— ✅ **已跑(2026-10-20)**:命令取自债册 §★ 总索引 §五,
   夹在 `TD-INDEX` 标记间;**输出 = `条目 91 | 状态 {open:70, partial:5, closed:16} | P0 面 {Y:87, N:4}`**
   + `未闭 P0 面 = 72` + `M3 Exit 第二条合取 ⇒ 假 —— 72 条 / 12 个 epic`
6. **S16-5 的 EPIC 口径折账** —— ✅ **已跑(2026-10-21)**:两条判据
   ```
   ① 分母 = 实际 story 文件数:emergency 9→8/9 · patient-ai 5→4/5 · skeuo 25→24/25 · world 6→5/6  全 ✅
   ② EPIC.md 直接读出 = 18/31 ;story 逐条归并 = 18/31 ;非 Complete 13 户名单逐一相同
      改前 EPIC.md 读出 = 22/31  ← 差 4 户 = 本次四户
   ```
   ⚠️ **基线须能解释**:首版脚本对「无分数的 Status 行」处理有误,误得 18(与正确值偶合);
   修正后基线 **22** → 刷账后 **18** 才是可解释的链。见 `sprint-16.md` S16-5 收口注。
   (⚠️ 初版命令漏过滤已闭的 15 条,曾报 87 —— 已修,见债册 §五 留痕;
   ⚠️ `N:2, 待裁:2` → `N:4` 系同日待裁四条裁定,出口量 72 不变)。**该输出与债册所记的「当前预期输出」逐字一致**。

> ⚠️ **Unity batch 独占工程**:跑全量前先 `pgrep` + 查 `unity/Temp/UnityLockfile`;
> **不得 kill 用户并行起的 batch**;跑完提醒用户可重开编辑器。

---

## Playtest Requirements

**无。** 本 sprint 不含任何可玩面。

---

## 未及核查项(禁借绿 —— 规划时即声明)

1. ~~**S16-1 的「引用实测」形态未定**~~ ✅ **已结(2026-10-20 收口)** —— 形态定为
   **`grep` 命中 SKILL §4b 的三元词表**,并实测通过(8 命中);取数命令与预期输出见
   `sprint-16.md` S16-1 收口注。⚠️ **教训留档**:该判据**首版由我自己写错**
   (「`grep 件/日` = 0 命中」—— 而禁令句里必然出现被禁的单位名,实测 2 命中)
   ⇒ **判据设计时须先问「满足它的输出长什么样」**,否则会因**自己的禁令**判自己失败。
2. ~~**S16-4 的载体路径未定** —— Editor 装配 vs UTF,取决于门读的是 `.cs` 文件文本还是编译后程序集,勘察归实现轮。~~
   ✅ **已结(2026-10-20 实现轮)** —— 形态定为**读 `.cs` 文件文本**(不走程序集),
   门本体住 `unity/Assets/Editor.Tools.Gates/FsWriteBoundaryGate.cs`(Editor 装配,
   经 `AssemblyGates.RunAll` 接线),**测试住 UTF EditMode 装配**
   (`unity/Assets/Tests/EditMode/DataPipeline/fs_write_boundary_gate_test.cs`,17 条)。
   ⇒ 两者都成立,不是二选一:门在 Editor 侧、判别力由 EditMode 测试钉住。
3. **S16-5 的「分母」口径** —— `world-ecozones` 是「分子多 1」而非「分母少 N」,刷账形态与另三户**不同类**,须单独处置。
4. **IL2CPP 三格(ADR-012 矩阵)** —— **NOT RUN**(本机仅 Mono)。本 sprint 全部纸面 + 一处 Editor 门,**不新增**跨平台面。<br>⚠️ **2026-10-21 订正:本句「本机仅 Mono」为假** —— ADR-012 §Validation 2026-10-17 已逐格实测:Linux-x64-Mono ✅ 09-24(3/3)· **Linux-x64-IL2CPP ✅ 09-24**(StandaloneLinux64 player 3/3,与独立 Python 金标准 + Mono 三方 19/19 逐位相同)· Linux-ARM64-IL2CPP ❌ **目标不存在**(pinned 工具链 46 个 BuildTarget 无任何 arm 目标;用户裁定长期未达)· CI 常驻 ⚠️ job 已建但从未跑(缺 `UNITY_CLIENT_ID`/`UNITY_CLIENT_SECRET`)。⇒ **矩阵级,非本件个体判据**。原文保留不删。

---

## Definition of Done — This Sprint

- [ ] Must 5 件完成,**每件贴出可复算命令 + 输出**
- [x] S16-4 过证伪轮(F1 / F2 / F3 全转红),工作树零变异残留(SHA-256 校验)
  ⇒ ✅ 2026-10-20 三条全 `exactRed`,退出码 0,残留 0 / 0
- [x] 全量 EditMode 零回归(对照 `3940 / 3904 / 0 / 36`,带三元组)
  ⇒ ✅ 2026-10-20 23:45 CST · sprint-16/S16-4/全量 · commit `f6cdaaf` = `4071 / 4035 / 0 / 36`;
  ⚠️ **对照值订正为 `4054 / 4018 / 0 / 36`**(`3940` 是规划时刻快照,已过期 —— 见 §Smoke 第 1 条订正),
  Δ = **+17 / +17 / 0 / 0**,差集新增 17 条全为 `FsWriteBoundaryGateTest.*`、消失 0
- [x] 四处分数同值;**零 story 转 `Complete`**
  ⇒ ✅ 2026-10-21 复核:四处分数仍同值(`226 / 182 / 44 / 18-31`),S16-5 只改 **EPIC.md 的
  epic 级状态词与分母**,**未转任何 story**(`git status` 对 `story-*.md` 为空);
  epic 侧两口径**同值 18/31**(改前 `EPIC.md` 读 22,差 4 户已刷)
- [ ] smoke check 记录(`/smoke-check sprint`)
- [ ] QA sign-off 报告:APPROVED 或 APPROVED WITH CONDITIONS(`/team-qa sprint`)
- [ ] 无 S1 / S2 bug

---

> **不经用户指令不 commit。**
