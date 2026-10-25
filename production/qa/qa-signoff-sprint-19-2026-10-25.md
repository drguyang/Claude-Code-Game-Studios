# QA Sign-off Report — Sprint 19

**Date**: 2026-10-25
**Sprint**: 19 — AC 门清偿(G-4 · G-5 · G-6 · G-7 + `TD-persist-01` 剩余两项 + 记账订正)
**Engine**: Unity 6.3 LTS(6000.3.24f1)
**QA Plan**: `production/qa/qa-plan-sprint-19-2026-10-24.md`
**Smoke**: `production/qa/smoke-2026-10-25-sprint19.md`
**Sprint file**: `production/sprints/sprint-19.md`
**分析草稿(逐项判定留痕)**: `production/qa/evidence/tmp-qa-sprint19-qa-lead.md`
**被验的树**: `4d468a5`(生产码零未提交改动;工作树仅两份文档 + `fz-s194/`)

---

## Verdict: ⚠️ **APPROVED WITH CONDITIONS**

无 S1 / S2 bug 未闭(`production/qa/bugs/` 空);五件全部 `done` 且各有实测载体、全绿、零回归。
**但存在 5 条须处置的条件**(2 条治理级 Done-gate 违反 + 2 条证据引用缺陷 + 1 条可复验性降级),
故不判 `APPROVED`。

---

## Test Coverage Summary

| 件 | 类型(QA plan 分类) | 自动化载体(实测条数) | 结果 | 证伪轮 |
|---|---|---|---|---|
| **S19-1** G-4 + `TD-persist-01` 剩两项 | Integration | `EditMode/PersistenceService/save_slot_contract_test.cs`(**36**)· `save_composer_wiring_test.cs`(**15**)· `fold_row_codec_test.cs`(**8**)· `eventstream_restore_test.cs`(**9**)· `save_replay_roundtrip_test.cs`(**1**) | ✅ **PASS** | ✅ 3 窗 / 6 变异 M1–M6,全 exact;SHA-256 还原逐文件通过 |
| **S19-2** G-5(`IPlayerMotor`) | Logic | `EditMode/PlayerController/player_motor_test.cs`(**9**) | ✅ **PASS** | ✅ F1–F3 三轮全红;F1 首版零红自报并修正 |
| **S19-3** G-6(`LogiPose`/`ITameable`) | Logic | `EditMode/EnemyAI/logi_pose_test.cs`(**15**) | ✅ **PASS**(带注) | ✅ M1–M8(8/8 判别力);⚠️ 备份 `fz-s193/` 不在盘 |
| **S19-4** G-7(`DeferredThreatSlot`) | Integration | `EditMode/RandomEvents/deferred_threat_slot_test.cs`(**21**)· `event_budget_defer_test.cs`(7,全改写) | ✅ **PASS** | ✅ M1–M9 九轮全红;三文件 SHA-256 基准齐 |
| **S19-5** 记账订正 | Config/Data | —(纸面:grep 双命中 + 实跑输出) | ✅ **PASS** | 不适用(纸面件) |

**汇总**:5 PASS · 0 FAIL · 0 MISSING · 0 MANUAL(本 sprint 零人工验收项,非漏测)。

### 条数复算命令与实跑输出(承 `coding-standards.md` §数字须带可跑复算命令)

```bash
python3 - <<'PY'
import xml.etree.ElementTree as ET, collections
r = ET.parse('/home/gu/.claude/jobs/181888b1/tmp/qa-full-editmode.xml').getroot()
c = collections.Counter()
for t in r.iter('test-case'):
    n = t.get('fullname','')
    for cls in ('SaveSlotContractTest','SaveComposerWiringTest','FoldRowCodecTest',
                'EventStreamRestoreTest','SaveReplayRoundtripTest','PlayerMotorTest',
                'LogiPoseTest','DeferredThreatSlotTest','EventBudgetDeferTest'):
        if f'.{cls}.' in n: c[cls] += 1
for k, v in sorted(c.items()): print(f'  {k}: {v}')
PY
```

实跑输出(2026-10-25,QA 签核轮):

```
  DeferredThreatSlotTest: 21
  EventBudgetDeferTest: 7
  EventStreamRestoreTest: 9
  FoldRowCodecTest: 8
  LogiPoseTest: 15
  PlayerMotorTest: 9
  SaveComposerWiringTest: 15
  SaveReplayRoundtripTest: 1
  SaveSlotContractTest: 36
```

⇒ 与 smoke 报告 §Test Coverage 表**逐项相同**;唯一差异是 QA plan §实收对账 把
`save_composer_wiring_test.cs` 记为 13(见条件 C-4)。

---

## 全名级差集结论

**两套件均为 新增 0 / 消失 0 / 翻转 0 / 非绿 0 ⇒ 零回归**,且是**逐测试全名**一致,非总数巧合。

我**独立复跑**了 smoke 报告 §③ 的命令(不采信自述),实跑输出:

```
EditMode: 基线 4867 · 本次 4867 | 新增 0 · 消失 0 · 翻转 0 · 非绿 0
PlayMode: 基线 202 · 本次 202 | 新增 0 · 消失 0 · 翻转 0 · 非绿 0
```

**三元组核验**(逐项对照,非照抄):

| 套件 | 本次结果 | 截至时刻 | 批次 | commit |
|---|---|---|---|---|
| EditMode | `4867/4832/0/35` | `2026-10-25 08:27 CST` | `qa-full`(全量) | `4d468a5` |
| PlayMode | `202/202/0/0` | `2026-10-25 08:27 CST` | `qa-full`(全量) | `4d468a5` |

**commit 差是否被正确归因**:基线 commit 为 `6198671`(EditMode)/ `8ba3fb4`(PlayMode),本次为 `4d468a5`。
两者之间仅 `2847a4a` + `4d468a5` 两个**纯文档** commit(S19-5 记账订正),零代码改动
⇒ 数字逐项相同**归因正确,非回归、非照抄**。旁证:四条 XML 的 `start-time` 各自独立存在
(EditMode 基线 07:20 / 本次 08:27;PlayMode 基线 10-24 23:12 / 本次 08:27),且本次树经
`git status` 确认生产码零未提交改动 —— **三元组可如实读作「`4d468a5` 这棵树跑出这个数」**。

---

## 逐件分类核账

### S19-1(G-4 + `TD-persist-01` 剩两项 · Integration)

- **类型一致性**:✅ 与 QA plan 一致(plan 明写存档往返取 Integration,落地树仍 `EditMode/` 真装配件)。
- **证据在盘且全绿**:✅ 5 个载体文件,合计 69 条(36+15+8+9+1),全 Passed。
- **证伪轮 ≥3 轮 + 三要素**:✅ 3 窗 / 6 变异;每轮记录「变异 → 红集 → 还原 `sha256sum -c`」;
  还原用 `restore_all()` 逐文件 SHA-256(未用 `git checkout`/`stash`),另核 `grep -c "FALSIFY-M"` 四文件全 0。
  **并主动自报违规一处**(第一窗内误读 `SaveService.cs:262-266`)—— 属诚实登记。
- **`NOT-RUN` 是否被当成已满足**:✅ 未。判据⑤ 字节半原标 `NOT-RUN`,本轮由 `TD-persist-03` 兑现
  (`FoldRowCodec` + `Compose` 折叠段 + `ParseBody` 复合读),证据件 §五 逐条给出落点。
  ⚠️ 但 `AC-7a-01`/`AC-7a-02` 的**全量语义**仍未转绿,证据件已明写「不得读成已满足」。
- **判**:PASS。

### S19-2(G-5 `IPlayerMotor` · Logic)

- **类型一致性**:✅ Logic。
- **证据在盘且全绿**:✅ `player_motor_test.cs` 9 条,全 Passed。
- **证伪轮**:✅ F1–F3 三轮全红,还原判据 SHA-256(基准 `7cc21b79…`)+ 锚串唯一性断言。
  **F1 首版零红 = 假绿夹具,自报并重构前提后转红** —— 这是证伪门按设计工作的正向证据。
- **`NOT-RUN` 处置**:✅ PlayMode 面在证据件内主动标 `NOT-RUN` 且明写「不借上轮 `202/202` 充数」;
  本轮 smoke 已全量覆盖 PlayMode。
- **残余(已登记,非缺口)**:C-1 客户端上行链整体 `NOT-RUN`(归 P1b / 45)· C-2 零消费者(只落形状)·
  C-3 死字段已删,内联算子与 `CellTransitionDetector` 的重复**尚未入债册**(证据件 §六 自陈「未立,待用户裁」)。
- **判**:PASS。

### S19-3(G-6 `LogiPose` / `ITameable` · Logic)

- **类型一致性**:✅ Logic。
- **证据在盘且全绿**:✅ `logi_pose_test.cs` 15 条,全 Passed。
- **证伪轮**:✅ M1–M8;M1 零红被**如实判为无效变异**(不可达代码),M1' 修正后转红 —— 未把无效轮算进判别力。
- **`NOT-RUN` 处置**:✅ 消费者仍为零、`StepAccumulator` 双实现并存,均如实登记,未冒称已接线。
- **⚠️ 条件 C-5**:证据件称备份 `fz-s193/`,**该目录在盘不存在**。基准哈希值已记录、还原在同命令内完成,
  故不判 FAIL;但「SHA-256 逐字节还原」这一条**今天不可复验**。
- **判**:PASS WITH NOTES。

### S19-4(G-7 `DeferredThreatSlot` · Integration)

- **类型一致性**:✅ Integration。
- **证据在盘且全绿**:✅ 新 `deferred_threat_slot_test.cs` 21 条 + 既有 `event_budget_defer_test.cs` 7 条全改写,全 Passed。
- **证伪轮**:✅ M1–M9 九轮全红;三文件 SHA-256 基准齐(`5a58c9e8…`/`f4764122…`/`257d2adb…`),备份 `fz-s194/` **在盘可验**。
- **门有红证**:✅ 判据③ 两豁免确已撤除(`WriterExistenceGate.cs:130-132`),且 M8 回插豁免 ⇒
  `gate_threatKinds_notInExemptTable` 1 红 —— **「有门」带了 `file:line` + 能证明它会红的命令**,符合纪律。
- **规划 vs 实收偏差**:判据①(删 `AddToDefer`)与 F-1 修法①「加真上界」相抵,实现取判据①优先;
  上界移至写者域校验 + 折叠侧抛。证据件 §二 如实登记。**`DEFER_MAX` 仍无 AC 编号**(GDD 判据缺口),
  未冒充 AC 已验证。
- **判**:PASS。

### S19-5(记账订正 · Config/Data)

- **判据形态**:✅ 纸面件,判据 = 每条 grep 双命中 + 实跑输出;符合 Config/Data 的 ADVISORY 面。
- **独立复算**:✅ 我自跑了债册 §五 脚本(原文原样),实跑输出:

```
条目 104 | 状态 {'open': 70, 'partial': 6, 'closed': 27, '**done**(2026-10-21)': 1} | P0 面 {'Y': 93, 'N': 10, '~~**Y**~~': 1}
未闭 P0 面 = 71
M3 Exit 第二条合取（31 个 epic,每个名下「未闭 TD 的 P0 面 = 0」）⇒ 假 —— 71 条,分布在 11/31 个 epic
    medical-consequences: 20 · audio-system: 20 · clinic-machine: 17 · telemetry-analytics: 5
    skeuomorphic-ui: 2 · disease-simulation: 2 · patient-ai: 1 · player-controller: 1
    persistence-service: 1 · item-database: 1 · modular-building: 1
```

⇒ 与 QA plan 推算 `76 → 71` **恰合**,且 `71` 是**本次实跑值**,不是照抄规划算术。
路线图 M3 Exit 第二条合取「由 `NOT-YET-EVALUABLE` 转可机械求值」✅ 成立(且如实报「假」,未美化)。
- **判**:PASS。

> 🔴 **2026-10-25 收口轮就地订正 —— 上面那个 `71` 已失效,原文不删**:
> 用户裁定 **C-6「都入债册」** 后新立 `TD-enemy-02`(`enemy-ai` · `open` · P0 面 `Y`)⇒ 当场复跑 §五脚本:
> ```
> 条目 105 | 未闭 P0 面 = 72
> M3 Exit 第二条合取 ⇒ 假 —— 72 条,分布在 12/31 个 epic
> ```
> ⇒ **本 sprint 净变化 = `76 → 72`(降 4)**,不是 `76 → 71`。
> **差的 1 不是漏算,是 C-6 裁定新立的债**(四门清偿 −4,新立 +1)。
> ⚠️ 本条是「同一 sprint 内后续裁定改变分母」的实例 —— `71` 在 S19-5 记账当刻是真值,
> 但**不能当现值**。承 `coding-standards.md` §数字须带可跑复算命令。
> 复算命令 = 本报告 §溯源 ⑥(原样跑)。

---

## Bugs Found

**无已立案 bug。** `production/qa/bugs/` 仅含 `README.md`,零 `BUG-*.md`。

核账命令:

```bash
ls -la production/qa/bugs/ && ls production/qa/bugs/BUG-*.md 2>&1 | tail -1
```

实跑输出:

```
总计 12
drwxrwxr-x  2 gu gu 4096 10月  2 10:40 .
drwxrwxr-x  5 gu gu 4096 10月 25 08:28 ..
-rw-rw-r--  1 gu gu 1498 10月  2 10:40 README.md
ls: 无法访问 'production/qa/bugs/BUG-*.md': 没有那个文件或目录
```

**但本轮的缺陷没有走 bug 通道**:S19-2 的「内联算子与 `CellTransitionDetector` 重复」、
S19-3 的「`StepAccumulator` 与 `EnemyLocomotion.Advance` 双实现」均自陈**未入债册**。
这两条是技术债而非 bug,不阻塞签核,但**无载体 = 会随轮次蒸发**(见条件 C-6)。

---

## Conditions

### C-1(治理级 · 须处置)`story-001` 标 `Complete` 而 AC 0/7 全未勾

- **事实**:`production/epics/persistence-service/story-001-storage-abstraction.md:4` = `**Status**: Complete`;
  其 7 条 AC(`:33-39`)**全部 `- [ ]` 未勾**(实测 `grep -c "^- \[x\]"` = **0** / `grep -c "^- \[ \]"` = **7**)。
- **性质**:`story-001` 的 `Type: Logic` ⇒ 按 `coding-standards.md` §Test Evidence by Story Type,
  Logic 故事的自动化测试证据是 **BLOCKING** 门。今天其 AC 的语义层只部分落地
  (载体存在但证据件自陈「快照段 / 跨版本 / 掉落清单仍未转绿」)。
- **这是「第五个假绿形态」**:用「文件已落盘」顶替了「AC 已判定」。
- **为何本 sprint 不代修**:用户 2026-10-24 已裁「本 sprint 不代修,须另立评审项」。
- **签核要求**:收口前须**二选一**并留痕 ——(a) 把 `story-001` 状态退回 `In Progress` 并重开 AC 勾选;
  (b) 另立具名评审项并落 `production/qa/evidence/review-*`。**不得维持现状**。

### C-2(治理级 · 须跟踪)`AC-7a-20` 全树零载体 · `TD-persist-04` open

- **事实(全树搜索,非抽样)**:`grep -rl 'AC-7a-20' unity/Assets/Tests/ tests/ production/epics/` ⇒ **命中数 0**
  (`grep` 退出码 1)。`TD-persist-04` 索引行 = `open` / P0 面 `Y`(`docs/tech-debt-register.md:354`)。
- **后果**:`CompositionRoot.cs:453` 传 `narrativeContext: null` ⇒ fail-closed ⇒
  **生产路径的存档 / 读档不可用**。这是刻意取舍(不可利用 > 不可用),**不是缺陷**。
- **签核要求**:`AC-7a-20` 在 `TD-persist-04` 结案前**任何件都不得声称已满足**;
  `save-slot-ui` 解封前须先闭此债。已在本报告登记为 `NOT-RUN`。

### C-3(S4 · 文档 · 须订正)证据件引用指向不存在的文件

- **事实**:`production/qa/smoke-2026-10-25-sprint19.md:92` 与 `qa-plan-sprint-19-2026-10-24.md:526`
  均引用 `production/qa/evidence/impl-td-persist-03-2026-10-25.md`,**该文件不存在**;
  实际文件 = `impl-td-persist-03-replay-and-body-2026-10-24.md`(文件名与日期均不同)。
- **依据**:`coding-standards.md` §Review Evidence Standards —— 原件须可定位;悬空引用使
  「字节半已兑现」这一判据**在追溯面上断链**。
- **签核要求**:两处就地加注订正(原文不删),指向真实路径。

### C-4(S4 · 文档 · 须订正)QA plan 实收数 13 与实测 15 不符

- **事实**:`qa-plan…:526` 记 S19-1「36 + **13**(`save_composer_wiring_test.cs`)」,实测该类 = **15**。
- **可解释但不完整**:+2 由 `TD-persist-03` 追加(证据件 §二 记「+2」),差值有源;但 plan 未就地加注。
- **签核要求**:§实收对账 该格加注「13 → 15(+2,`TD-persist-03` 追加)」。

### C-5(S4 · 证据可复验性)`fz-s193/` 备份目录不在盘

- **事实**:`impl-s19-3-logi-pose-2026-10-25.md:55` 称「备份 `fz-s193/`」;`ls -d fz-s193*` **无命中**,
  仓内只有 `fz-s194/`。
- **影响**:S19-3 的「SHA-256 逐字节还原」今天**不可复验**。基准哈希值已记录、还原在同命令内完成,
  故不判 FAIL;但按「缺一即按 `NOT-RUN` 登记」的严格读法,该轮的**还原可复验性**降级为 `NOT-RUN`。
- **签核要求**:后续证伪轮的备份目录须**保留至收口**(或改为把备份哈希与还原脚本一并入库),
  不得随会话清理。

### C-6(ADVISORY)两条「自陈未入债册」的重复实现

- S19-2:`PlayerController` 内联跨格算子 vs `CellTransitionDetector`(`impl-s19-2…:63` 自陈「未立,待用户裁」)。
- S19-3:`EnemyPathing.StepAccumulator` vs `EnemyLocomotion.Advance`(`impl-s19-3…:76` 同)。
- **要求**:用户裁定「入债册 / 明确豁免」其一并留痕;悬空自述会随轮次蒸发。

---

## 未及核查项(禁借绿 —— 如实列出)

| # | 项 | 为什么没核 | 影响 |
|---|---|---|---|
| 1 | **未独立复跑 Unity 全量测试** | 预算 15 次工具调用,重跑全量需 ~2 分钟引擎独占 + 多次调用;改为**独立复算 XML**(计数 + 全名级差集 + 条数),两套件结论与 smoke 自述逐项相同 | 采信的是**XML 实体内容**,不是 agent 自述。若 XML 本身造假,本报告不能发现 |
| 2 | **证伪轮的「变异 → 红集」未逐轮重演** | 每轮需改生产码 + 跑测试 + SHA-256 还原,四件合计 26 轮,远超预算 | 采信证据件的**表格记录**。已核:各件的轮数、基准哈希值、还原方式、`git checkout` 禁令遵守情况均齐;S19-3 的备份缺失已在 C-5 登记 |
| 3 | **`AC-7a-01`/`AC-7a-02` 语义层的逐条覆盖度** | 需逐条比对 GDD `persistence-service.md:597-605` 与测试断言体 | 只核到「载体存在且证据件自陈部分未绿」;完整覆盖度判定归 C-1 的评审项 |
| 4 | **IL2CPP 跨平台逐位一致性** | 本机仅 x64;ARM64 目标不存在;CI 缺凭证(`TD-ci-01` EXTERNAL) | 矩阵级,**不得声称跨平台逐位一致**(承 QA plan §未及核查项 6) |
| 5 | **人工可玩性 smoke** | QA plan §Smoke Test Scope 第 3 条明写「不要求」——本 sprint 零玩家可见变化 | 非失败、非漏测。`save-slot-ui` 仍 `BLOCKED-BY-7a`,无下游 story 被打开 |
| 6 | **`/code-review` 双列结账表** | 属 DoD 另一项,不在 QA 签核面 | `sprint-19.md:422` 仍未勾,须另行跑 |
| 7 | **账本树 `tests/integration/persistence-service/`** | 该目录不存在(已知差异,承 2026-10-15 注:两棵树都可能承载活测试,编译树是主体) | 不构成证据缺口;本件按实际落地树核 |

---

## 溯源与复算命令

| 项 | 出处 |
|---|---|
| 件状态真源 | `production/sprint-status.yaml`(`S19-1…S19-5 → done`,正则实跑) |
| 测试结果 | `/home/gu/.claude/jobs/181888b1/tmp/qa-full-{editmode,playmode}.xml` |
| 基线 XML | `s194-full.xml`(EditMode)· `tdp03-full-playmode.xml`(PlayMode) |
| 债册复算 | `docs/tech-debt-register.md` §五 脚本(原样执行) |
| 证据标准 | `.claude/docs/coding-standards.md` §Review Evidence Standards |

```bash
# ① 五件状态
python3 -c "
import re
t=open('production/sprint-status.yaml',encoding='utf-8').read()
for m in re.finditer(r'- id: \"(S19-[0-9])\"\n(?:.*\n){0,10}?\s+status: \"([^\"]+)\"', t):
    print(m.group(1), '->', m.group(2))"
# 实跑:S19-1..S19-5 -> done

# ② 两套件计数
grep -o 'total="[0-9]*" passed="[0-9]*" failed="[0-9]*" inconclusive="[0-9]*" skipped="[0-9]*"' \
  /home/gu/.claude/jobs/181888b1/tmp/qa-full-editmode.xml | head -1
grep -o 'total="[0-9]*" passed="[0-9]*" failed="[0-9]*" inconclusive="[0-9]*" skipped="[0-9]*"' \
  /home/gu/.claude/jobs/181888b1/tmp/qa-full-playmode.xml | head -1
# 实跑:4867/4832/0/0/35 与 202/202/0/0/0

# ③ bug 目录
ls production/qa/bugs/BUG-*.md 2>&1 | tail -1
# 实跑:No such file —— 零已立案 bug

# ④ AC-7a-20 全树(全域否定须附命令,承 review-scope-discipline.md)
grep -rl 'AC-7a-20' unity/Assets/Tests/ tests/ production/epics/ | wc -l
# 实跑:0

# ⑤ story-001 AC 勾选
grep -c '^- \[x\]' production/epics/persistence-service/story-001-storage-abstraction.md   # 实跑:0
grep -c '^- \[ \]' production/epics/persistence-service/story-001-storage-abstraction.md   # 实跑:7

# ⑥ 债册复算
python3 - <<'PYEVAL'
import re, collections
txt = open('docs/tech-debt-register.md', encoding='utf-8').read()
blk = re.search(r'<!-- TD-INDEX:BEGIN -->(.*?)<!-- TD-INDEX:END -->', txt, re.S).group(1)
rows = [[c.strip() for c in l.strip().strip('|').split('|')]
        for l in blk.split('\n') if l.startswith('| `TD-')]
live = [r for r in rows if r[2] != 'closed' and r[3].strip('*') == 'Y']
ep = collections.Counter(r[1].strip('`') for r in live)
print('条目', len(rows), '| 未闭 P0 面 =', len(live))
print('M3 Exit 第二条合取 ⇒', '真' if not live else f'假 —— {len(live)} 条,分布在 {len(ep)}/31 个 epic')
PYEVAL
# 实跑:条目 104 | 未闭 P0 面 = 71 ;M3 Exit 第二条合取 ⇒ 假 —— 71 条,分布在 11/31 个 epic
# 🔴 2026-10-25 收口轮就地订正(原文不删):C-6 裁定「都入债册」新立 TD-enemy-02 后**当场复跑**,
# 现值为:
#   条目 105 | 未闭 P0 面 = 72 ;M3 Exit 第二条合取 ⇒ 假 —— 72 条,分布在 12/31 个 epic
# ⇒ 上面那行 71 读作「@ 本报告 §⑥ 执行时刻(早于 C-6 裁定)」,不是现值。
# 复算命令 = 本块脚本本体(自包含,当场可跑)。

# ⑦ 证据件引用核对(C-3)
ls production/qa/evidence/ | grep persist
# 实跑:impl-s19-1-slot-contract-2026-10-24.md
#       impl-td-persist-03-replay-and-body-2026-10-24.md   ← 真实路径
#       (无 impl-td-persist-03-2026-10-25.md)

# ⑧ 备份目录(C-5)
ls -d fz-s19* 2>&1
# 实跑:fz-s194  ← 只有 s194,fz-s193 不在盘
```

---

## 原文不删 · 就地加注

本报告为首版,无前序版本。判定基于 2026-10-25 的实测;**未跑项已在 §未及核查项 逐条列出**,
不得读作「已全部核查」。`APPROVED WITH CONDITIONS` 表示**五件交付本身合格**,
不表示 sprint-19 已可收口 —— 收口仍待 C-1…C-6 处置 + `/code-review` 双列结账表。

---

## 八、C-1…C-6 处置结果(2026-10-25 收口轮)

| # | 条件 | 处置 | 状态 | 可复算判据 |
|---|---|---|---|---|
| **C-1** | `story-001` 标 Complete 而 AC 0/7 全未勾 | **用户裁定「退回 In Progress 并重开勾选」** —— 已就地加注(原文 `Status: Complete` 不删) | ✅ **已处置** | `grep -c '^- \[x\]' production/epics/persistence-service/story-001-storage-abstraction.md` ⇒ **3**;`grep -c '^- \[ \]'` ⇒ **4**;`grep -n 'Status' …` ⇒ `:4 **Status**: In Progress` |
| **C-2** | `AC-7a-20` 全树零载体 · `TD-persist-04` open | **用户裁定「本 sprint 补实现」→ 后经 W5 语义核查,裁定「丙」:回退 fail-closed,瞬时源归 10** | ⚠️ **仍未满足,但已定性** | `grep -rl 'AC-7a-20' unity/Assets/Tests/ tests/ production/epics/ \| wc -l` ⇒ **0**(不变);`CompositionRoot.cs:507` `narrativeContext = null`。**`AC-7a-20` 仍不得声称已满足** —— 详见评审报告 §七 W5 裁定轨迹 |
| **C-3** | 证据件引用指向不存在的文件 | 两处**就地加注订正**(原文不删),指向真实路径 | ✅ **已处置** | `grep -n "impl-td-persist-03-2026-10-25" production/qa/qa-plan-sprint-19-2026-10-24.md` ⇒ `:527` 订正行(指明真名 `impl-td-persist-03-replay-and-body-2026-10-24.md`) |
| **C-4** | QA plan 实收数 13 与实测 15 不符 | §实收对账该格**就地加注**「13 → 15(+2,`TD-persist-03` 追加)」 | ✅ **已处置** | 同上 `:527`;<br>`grep -c '^\s*\[Test\]' unity/Assets/Tests/EditMode/PersistenceService/save_composer_wiring_test.cs` ⇒ **15** |
| **C-5** | `fz-s193/` 备份目录不在盘 | **未补** —— 备份已随会话清理,无法追溯重建 | ⚠️ **如实登记为不可复验** | `ls -d fz-s19*` ⇒ 仅 `fz-s194`。S19-3 的「SHA-256 逐字节还原」**今天不可复验**,判 `NOT-RUN`。已写入 §未及核查项;后续轮次须保留备份至收口 |
| **C-6** | 两条「自陈未入债册」的重复实现 | **用户裁定「都入债册」** —— 已入 | ✅ **已处置** | `grep -n "StepAccumulator" docs/tech-debt-register.md` ⇒ `:1948`(残余)+ `:1955` `### TD-enemy-02`(具名债);`CellTransitionDetector` 见 `:454` |

**汇总**:6 条中 **4 条已处置**(C-1 / C-3 / C-4 / C-6)· **2 条如实登记为未满足**(C-2 已定性归 10、C-5 不可复验)。
⇒ **C-2 的未满足是裁定结果,不是遗漏** —— 用户 2026-10-25 裁定「丙」明确选择「不可用 > 不安全」;
C-5 是历史证据的可复验性缺陷,不影响本轮任何判据的成立,但**须在后续轮次避免重犯**。

### 签核结论是否随之变更?

**否,维持 `APPROVED WITH CONDITIONS`。** 理由:
- C-1 / C-3 / C-4 / C-6 的处置**不改变五件交付本身的判定**(它们本就是治理/文档层条件);
- C-2 在裁定丙后**仍是 open**(`TD-persist-04`),但该条件原文即写「这是刻意取舍,不是缺陷」
  ⇒ 维持「有条件的通过」是正确口径;
- **本轮新增一条须知**:评审轮发现的 W1 / W4 / W5 三条必修**已全部结清**(双列皆 ✅,
  见评审报告 §七)。W5 的处理方式(回退而非接线)是**安全属性的胜利**,不是交付缺陷。

⇒ 若要转 `APPROVED`,须先闭 C-2(`TD-persist-04` ⇒ 依赖 10 的实现轮),**不在本 sprint 能力面内**。
