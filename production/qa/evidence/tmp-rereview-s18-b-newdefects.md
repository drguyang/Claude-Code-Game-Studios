# sprint-18 修复后重评 — Agent B(新缺陷 + 硬约束)草稿

HEAD = `d91edeb`(修复轮**已提交**;工作树仅 `production/session-state/active.md` / `sprint-status.yaml` 改动)。
面内 7 件 + 旁件 2 件:**只读**评审,未改任何源码/测试,未跑 Unity batch。

## 一、新缺陷表(按严重度)

| ID | 判定 | 落点 | 证据 |
|---|---|---|---|
| N-1 | WARNING(上轮已报、本轮仍在 ⇒ 新记) | `unity/Assets/Sim/FoldedPredicate.cs:14-15` 头注「¬豁免(p) = 病人的**任一**病种不属于豁免类、且不带 relapse.* 块」与代码**方向相反** | 实现 `Exempt()` :110-135 = **任一**病种属于豁免类/带 relapse 即 `true`;故 ¬豁免 应为「**全部**病种不属于豁免类且均不带 relapse」。同文件 :101 方法注方向正确,仅头注错 |
| N-2 | WARNING | `unity/Assets/Sim.Contracts/IFoldedPredicate.cs:36` `<returns>四条件全部成立 ⇒ true</returns>` 与契约实际承载面不符 | 同文件 :4 写三条件;ADR-010 §二(`adr-010:159-160`)定义 `Folded(p)` 合并**三**条件;`FoldedPredicate.cs:18-19` 与 `HistoryFolder.cs:11-13` 均自陈条件 ④(SkillGrown)落在折叠**动作**、不在本布尔 ⇒ 读契约者会误以为谓词已含 ④ |
| N-3 | INFO(核验结论:无缺陷) | `HistoryFolder.cs` 拆四方法**行为不变** | `git show d91edeb -- HistoryFolder.cs`:纯方法抽取;`Fold` 只串 `Collect` / `BuildRows` / `Erase`;排序、抹除谓词、异常消息逐字未动 |
| N-4 | INFO | `IFoldedPredicate`(第九抽象点)未进 b5 门黑名单 ⇒ 44 可合法引用折叠谓词 | `AssemblyGates.cs:408-423` 数组仍 10 项(八抽象点 + SimEvent + VitalsDto);commit `d91edeb` message 已自陈「8 条 Suggestions(含此条)未修,已登记去向」 |
| N-5 | INFO(证据口径) | R-4 还原方式只声明 SHA-256 一致,**未明写以 `cp` 还原** | `review-sprint18-implementation-2026-10-22.md:117` 只写「还原后 SHA-256 逐字节一致(`8aca6a6d…5d92`)」;原件与 commit message 未见 `git checkout` / `git stash` 字样 |

**BLOCKING 新缺陷:0 条。**

## 二、硬约束逐条实测

| # | 约束 | 判定 | 证据 |
|---|---|---|---|
| 1 | 门 A | PASS | `Sim.asmdef`: `"references": ["Sim.Contracts"]` + `"noEngineReferences": true`;`Sim.Contracts.asmdef`: `"references": []` + `noEngineReferences: true` |
| 2 | 兄弟程序集不可见 / 唯一解码路径 | PASS | `Sim/` 内 `Sim.Codec` 文本命中 9 处**全为注释**;`new PayloadRef(` 命中 15 处**全为注释/文档串**(禁止性说明);`TryGetPayload` **0 命中**;`Sim.asmdef` 无 `Sim.Codec` 引用 |
| 3 | ADR-010 折叠行形状 | PASS | `adr-010:160` = `(onset, 病种_id, patient_id, patient_seed, outcome, t_end)`;`FoldRow.cs:33-48` 声明序 = `Onset/DiseaseId/PatientId/PatientSeed/Outcome/TEnd`;构造参数与 `HistoryFolder.cs:148-155` 传参同序 |
| 4 | 代码规模 | PASS | 逐方法近似统计见 §三:面内**无** >40 行方法,**最大近似 CC = 7(<10)** |
| 5 | 证伪轮形态 | PASS(附 N-5) | `unity/Logs/mut-r4-editmode-20261022.xml`: `total=21 passed=20 failed=1`,唯一失败 = `test_lethalFor_unspecifiedFailsLoud_evenOnNonLethalEntry`(R-4 专钉),**零附带红**;该 XML 为 `testFilter=EntityDeathGatesTest` **过滤跑**(顶层 `testcasecount=4419` 但 `total=21`),**未被当作全量基线**;全量回归另记(4419/4383/0/…/36,Δtotal=Δpassed=1)。**无借绿迹象** |
| 6 | 测试写盘边界 | PASS | 面内测试件对 `GetTempPath` / `persistentDataPath` / `File.Write*` / `Directory.Create*` / `FileStream` / `Path.Combine` **零命中**(grep 空输出) |
| 7 | 文档-代码方向一致性 | FAIL(2 处) | `EntityDeathGates` 头注 `:5-9` 与 `SelfLimited` 主注 `:133-154` 均已是订正后的 `[self_limit ∨ IsCombatant] ∧ ¬战伤破例`,与实现 `:156-174` 一致 ⇒ 该两处 PASS;**不合格** = N-1(`FoldedPredicate.cs:14-15` 方向反)、N-2(`IFoldedPredicate.cs:36` 条件数不符) |

## 三、代码规模(近似,脚本口径)

> 口径:按 `{`/`}` 深度定位方法体,统计**非空且非 `///` 注释**行;近似 CC = 1 + `if/for/foreach/while/case/catch/&&/||/??/?` 计数。
> ⚠️ 多行签名会被正则错标方法名(下表 `static` 一行实为 `Collect`);`FoldRow.Equals` 一行可能吞并相邻成员。数值仅作阈值筛查用。

| 文件 | 方法 | 非空非注释行 | 近似 CC | 超阈 |
|---|---|---|---|---|
| HistoryFolder.cs | `static`(实为 `Collect`,签名跨行) | 29 | 7 | — |
| FoldedPredicate.cs | `Exempt` | 22 | 7 | — |
| HistoryFolder.cs | `BuildRows` | 22 | 4 | — |
| SaveService.cs | `AddChecksum` | 20 | 3 | — |
| FoldRow.cs | `Equals` | 19 | 7 | — |
| SaveService.cs | `SaveBytes` | 19 | 4 | — |
| FoldedPredicate.cs | `DiseasesOf` | 16 | 6 | — |
| HistoryFolder.cs | `Fold` | 16 | 6 | — |
| SaveService.cs | `LoadBytes` | 15 | 5 | — |
| EntityDeathGates.cs | `SelfLimited` | 14 | 7 | — |
| SaveService.cs | `ValidateChecksum` | 14 | 3 | — |
| EntityDeathGates.cs | `IsCombatant` | 12 | 5 | — |
| HistoryFolder.cs | `Erase` | 11 | 4 | — |
| FoldedPredicate.cs | `Terminal` | 10 | 5 | — |
| FoldedPredicate.cs | `FoldedPredicate` | 9 | 5 | — |
| FoldedPredicate.cs | `Folded` | 9 | 5 | — |
| EntityDeathGates.cs | `IsBattleInjury` | 6 | 4 | — |
| FoldedPredicate.cs | `HasOpenCase` | 4 | 2 | — |
| EntityDeathGates.cs | `LethalFor` | 3 | 3 | — |
| EntityDeathGates.cs | `LethalityOf` | 1 | 1 | — |

(测试文件 `entity_death_gates_test.cs` 全部方法 1–13 行 / CC 1,未列全。)

## 四、未及核查项

- `unity/Assets/Sim.Codec/SaveService.cs` 仅核到 `IFoldedPredicate` 注入面(`:30/:47/:59`)与规模,未逐行核折叠行写盘路径(本轮无 diff,不属「修复轮新引入」)。
- R-4 变异操作的实际 shell 命令序列(是否真用 `cp`)无日志落盘,**无法从产物反证**;仅能确认产物与声明数字自洽、且无 `git checkout` 痕迹。
- 未跑任何 Unity 测试(工程独占 + 纪律禁跑),测试结论全部引用既有 XML/日志。
- 硬预算已超(约 14 次工具调用),以上未及项未补查。
