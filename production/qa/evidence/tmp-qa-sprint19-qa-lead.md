# QA 签核草稿 — sprint-19(2026-10-25)

> 本文件 = 签核报告的分析段。逐项 Append,不事后补写。

| ID | 判定 | 证据 | 出处 |
|---|---|---|---|
| ENV-1 | PASS | `production/qa/bugs/` 仅含 `README.md`,零 `BUG-*.md` ⇒ 本轮无已立案 bug | `ls -la production/qa/bugs/` 实跑:总计 12,唯一文件 `README.md` |
| SMOKE-1 | PASS(带注) | 两套件三元组齐备:EditMode `4867/4832/0/35` · PlayMode `202/202/0/0`,均带「截至时刻 2026-10-25 08:27 CST · 批次 qa-full · commit 4d468a5」 | `production/qa/smoke-2026-10-25-sprint19.md:12-15` |
| SMOKE-2 | PASS | commit 差(`6198671`/`8ba3fb4` → `4d468a5`)被正确归因为「两纯文档 commit 间隔」;且报告自陈「被验的树 = 4d468a5 本身」,`git status` 仅文档改动 | 同上 `:17-20` / `:45-47` |
| SMOKE-3 | PASS(独立复跑) | 我按报告 §可跑复算命令 ③ 自跑:EditMode 基线 4867 · 本次 4867,新增 0 / 消失 0 / 翻转 0 / 非绿 0;PlayMode 基线 202 · 本次 202,四项全 0。**与报告自述逐字相同** | 自跑输出见报告 §全名级差集 |
| SMOKE-4 | PASS | XML 实体在盘:`qa-full-editmode.xml` 3642197 B @10-25 08:27 · `qa-full-playmode.xml` 213908 B @10-25 08:27 · 基线 `s194-full.xml` @10-25 07:20 · `tdp03-full-playmode.xml` @10-24 23:12 —— 四条 `start-time` 独立存在,非照抄 | `ls -la /home/gu/.claude/jobs/181888b1/tmp/` |
| SMOKE-5 | PASS | 计数独立复跑:`total="4867" passed="4832" failed="0" inconclusive="0" skipped="35"` / `total="202" passed="202" failed="0" inconclusive="0" skipped="0"` | `grep -o 'total=…' <xml> \| head -1` |
| STAT-1 | PASS | 五件状态全 `done` | `production/sprint-status.yaml` 正则实跑:S19-1…S19-5 → done |
| CARRIER-1 | PASS | 五条测试文件实体在盘,时间戳与各实现轮吻合(`save_slot_contract_test.cs` 10-24 22:59 · `player_motor_test.cs` 10-24 23:48 · `logi_pose_test.cs` 10-25 01:36 · `deferred_threat_slot_test.cs` 10-25 07:15) | `ls -la unity/Assets/Tests/EditMode/{PersistenceService,PlayerController,EnemyAI,RandomEvents}/` |
| CARRIER-2 | PASS | 条数逐类复跑与 smoke §Test Coverage 表**逐项相同**:`SaveSlotContractTest:36` `SaveComposerWiringTest:15` `FoldRowCodecTest:8` `EventStreamRestoreTest:9` `SaveReplayRoundtripTest:1` `PlayerMotorTest:9` `LogiPoseTest:15` `DeferredThreatSlotTest:21` `EventBudgetDeferTest:7` | 自跑 `collections.Counter` 扫 `qa-full-editmode.xml` |
| TYPE-1 | PASS | 分类与 QA plan §Test Summary 一致:S19-1 Integration / S19-2 Logic / S19-3 Logic / S19-4 Integration / S19-5 Config-Data | `qa-plan-sprint-19-2026-10-24.md:59-65` vs 各证据件 |
| FALS-1 | PASS | S19-1 **3 窗 6 变异**(M1…M6)全 exact,还原判据 `restore_all()` 逐文件 SHA-256 比对通过 + `grep -c "FALSIFY-M"` 四文件全 0;并**自报违规一处**(第一窗内误读 `SaveService.cs:262-266`,已说明读到的是基线态) | `impl-s19-1-slot-contract-2026-10-24.md:70-131` |
| FALS-2 | PASS(带注) | S19-2 **F1–F3 三轮全红**,还原判据 SHA-256(基准 `7cc21b79…`);**首版 F1 零红 = 假绿夹具,自报并修正后转红** —— 属「证伪轮抓到假绿」的正向案例 | `impl-s19-2-player-motor-2026-10-24.md:41-51` |
| FALS-3 | PASS | S19-3 **M1–M8 八轮**;M1 零红被如实判为「不可达代码 = 无效变异」,M1' 修正后 1 红;判别力自证 8/8(不含 M1)全数转红 | `impl-s19-3-logi-pose-2026-10-25.md:53-71` |
| FALS-4 | PASS | S19-4 **M1–M9 九轮全红**,三文件 SHA-256 基准齐(`5a58c9e8…`/`f4764122…`/`257d2adb…`),备份 `fz-s194/` **在盘** | `impl-s19-4-deferred-threat-2026-10-25.md:63-80`;`ls -d fz-s194` 命中 |
| FALS-5 | **PASS WITH NOTES** | S19-3 证据件称备份 `fz-s193/`,**该目录在盘不存在**(`ls -d fz-s193*` 无命中,仓内只有 `fz-s194`)。⇒ 其「SHA-256 逐字节一致」**今天不可复验**;基准哈希值已记录、还原在同命令内完成,故不判 FAIL,但按「缺一即 NOT-RUN」的严格读法,该轮的**可复验性**降级 | `impl-s19-3-logi-pose-2026-10-25.md:55` vs `ls -d fz-s193*` |
| GATE-1 | PASS | 判据③ 两豁免确已撤除,且撤除有红证:M8 回插 `("ThreatDeferred", …)` ⇒ `gate_threatKinds_notInExemptTable` 1 红 | `unity/Assets/Editor.Tools.Gates/WriterExistenceGate.cs:130-132`;`impl-s19-4…:77` |
| GATE-2 | PASS(带注) | 门子集 `373/373/0/0` @ `s194-gates.xml`;「撤表 + 写者同批落地后门仍绿」为正向前提,**反向(删写者 ⇒ 门红)由 M8 覆盖** | `impl-s19-4…:55-56` |
| NOTRUN-1 | PASS(如实登记) | S19-2 的 PlayMode 面在证据件内**主动标 NOT-RUN**,明写「不借上轮 202/202 充数」;已由本轮 smoke 全量 PlayMode 覆盖 | `impl-s19-2…:38` |
| NOTRUN-2 | **CONFIRMED NOT-RUN** | `AC-7a-20` 全树零载体。我自跑两棵树 + epics:`grep -rl 'AC-7a-20' unity/Assets/Tests/ tests/ production/epics/` ⇒ **命中数 0**(命令与输出见报告)。⇒ 该 AC 是 `NOT-RUN`,**不得计入已满足** | 自跑;`TD-persist-04` 索引行 `open` / `Y` @ `docs/tech-debt-register.md:354` |
| RESID-1 | **RISK(需条件)** | `AC-7a-01`/`AC-7a-02` 载体**今天存在**(`save_replay_roundtrip_test.cs` / `save_service_test.cs` 各引编号),但 `story-001` 仍 `Status: Complete` 且 **7 条 AC 全未勾(0 `[x]` / 7 `[ ]`)**。证据件自陈「不得读成 AC-7a-01 已满足」(快照段 / 跨版本 / 掉落清单仍未转绿) | `production/epics/persistence-service/story-001-storage-abstraction.md:4` / `:33-39`;`impl-td-persist-03-replay-and-body-2026-10-24.md:76` |
| RESID-2 | **RISK(需条件)** | `TD-persist-04` open ⇒ `CompositionRoot.cs:453` 传 `narrativeContext: null`,fail-closed ⇒ **生产路径存档 / 读档不可用**。这是刻意取舍(不可利用 > 不可用),已登记,非缺陷 | `unity/Assets/Gameplay.Boot/CompositionRoot.cs:394` / `:453`;`SaveService.cs:300` |
| DEBT-1 | PASS(独立复算) | 债册 §五 脚本自跑:`条目 104 | 状态 {'open':70,'partial':6,'closed':27,'done':1} | P0 面 {'Y':93,'N':10,'~~Y~~':1}` ⇒ **未闭 P0 面 = 71**;M3 Exit 第二条合取 ⇒ **假 —— 71 条,分布在 11/31 个 epic**。与 QA plan 推算 `76 → 71` 恰合,**且与「71」实跑一致(非照抄)** | `docs/tech-debt-register.md` §五 脚本原样执行 |
| CITE-1 | **DEFECT(S4 · 文档)** | smoke 报告 `:92` 与 QA plan `:526` 引用证据件 `impl-td-persist-03-2026-10-25.md`,**该文件不存在**;实际文件 = `impl-td-persist-03-replay-and-body-2026-10-24.md`(日期也不同) | `ls production/qa/evidence/ \| grep persist` |
| CITE-2 | **DEFECT(S4 · 文档)** | QA plan §实收对账 写 S19-1「36 + **13**(`save_composer_wiring_test.cs`)」,实测该类 = **15**。差值 +2 可由 `TD-persist-03` 追加两测解释,但 plan 未就地加注 | `qa-plan…:526` vs 自跑 `SaveComposerWiringTest: 15` |
| SCOPE-1 | PASS | 未越权:全域否定(`AC-7a-20` 零载体)已附全树搜索命令 + 命中数 0 | 承 `review-scope-discipline.md` |
