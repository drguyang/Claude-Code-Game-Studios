# 只读代码评审草稿 —— sprint-18(Unity 主程 / 引擎侧 + C# 侧)

评审对象:S18-1 判死谓词 · S18-2 折叠谓词与折叠行 · SaveService 构造注入
评审方式:只读;未跑 Unity batch / 未跑 UTF / 未改任何源码
仓库根:`/home/gu/文档/nm/nm2/Claude-Code-Game-Studios`

| ID | 判定 | 落点 file:line | 证据 |
|---|---|---|---|
| G-01 | PASS | `unity/Assets/Sim/Sim.asmdef:4,13` | `references: ["Sim.Contracts"]`、`noEngineReferences: true`;新代码 import 仅 `System`/`System.Collections.Generic`/`System.IO`/`Sim.Contracts` |
| G-02 | PASS | `unity/Assets/Sim.Contracts/Sim.Contracts.asmdef:4,13` | `references: []`、`noEngineReferences: true`;新增 `ActorClass`/`IFoldedPredicate` 零引擎依赖 |
| G-03 | PASS(兄弟装配) | `unity/Assets/Sim.Codec/SaveService.cs:21,47` | SaveService 只见 `Sim.Contracts` 的 `IFoldedPredicate` 抽象点,未见 `Sim` 实现;无越界引用 |
| G-04 | PASS(附保留) | `git show --stat ad7316c` | `Sim.Contracts` **只新增** `IFoldedPredicate.cs`(+39),无删改;未对全装配做成员面 diff(见未及核查项) |
| G-05 | PASS(未违约)· 既存缺口 | `unity/Assets/Sim.Codec/ISaveService.cs:20-27` · `SaveService.cs:47` | 接口当前**根本没有 `Checkpoint`**(只有 `SaveBytes`/`LoadBytes`)⇒ ADR-010 `:331` 反射断言今日**空转**;本轮以构造注入回避加形参,**未破坏**该契约 |
| G-06 | PASS | `Sim/FoldedPredicate.cs:167-168` · `Sim/HistoryFolder.cs:96-97,104-106` | 读载荷唯一路径 = `_decoder.Decode<T>(EventKind, e.Payload)`;四个评审文件内 `TryGetPayload` / `new PayloadRef` 零命中 |
| G-07 | PASS | `Sim/HistoryFolder.cs:139-148` | 条件 ④ 落**动作侧**:抹除写为 `belongsToFolded && e.Kind != EventKind.SkillGrown`;全仓生产侧无第二处按病人删除病史行的消费者(`grep .Folded(` 仅 `HistoryFolder.cs:82`) |
| G-08 | PASS | `Sim/HistoryFolder.cs:113,132` | `t_end` = 终态事件**头部** `e.Tick`,非载荷字段(ADR-032 ④a) |
| G-09 | PASS | `Sim/HistoryFolder.cs:87,108-113,119` | `outcomes` 以 `DiseaseId` 为键 ⇒ 终态逐病种(ADR-032 ④b) |
| G-10 | PASS | `Sim/HistoryFolder.cs:108-111` | 同病人同病种第二条终态 ⇒ `InvalidDataException`(ADR-032 ⑤「重复即流损坏」) |
| G-11 | PASS | `Sim/FoldedPredicate.cs:87-98` | 条件 ① = 遍历找 `PatientOutcomeChanged` 存在性查询,不重放 F4(ADR-032 ⑥) |
| G-12 | PASS(行为) | `Sim/EntityDeathGates.cs:143-161` | 与**订正后**公式 `[self_limit ∨ IsCombatant] ∧ ¬(Player ∧ injury ∧ lethal ∧ 白名单)` 等价:combatant 先 `true`;仅 `Player` 走白名单破例;余回 `Curve.SelfLimit` |
| G-13 | **DEFECT(中)· 文档级** | `Sim/EntityDeathGates.cs:5` 与 `:124` | 两处仍引**作废旧式** `self_limit(d) ∧ ¬IsCombatant(entity)`,与该文件 `:129`「combatant ⇒ 恒 true」及订正公式**自相矛盾**;GDD `:1203` 已明写「实现时不得照旧式写」⇒ 未来实现者照头注写即复发 bug |
| G-14 | **FAIL(中)** | `Sim/HistoryFolder.cs:69-151` | `Fold` 圈复杂度约 **14**(4 null 检查 + 1 id 检查 + 1 谓词分支 + 两处循环内 4 分支 + `TryGetValue` + sort 三元) > 10 |
| G-15 | **FAIL(中)** | `Sim/HistoryFolder.cs:69-151` | `Fold` 单方法 **83 行** > 40;建议拆 `CollectOnsets` / `CollectOutcomes` / `EraseAndRetain` |
| G-16 | PASS | 四个实现文件 public 成员 | 所有 public 类型/方法均有 `<summary>` + 参数/异常说明;`FoldRow` 六字段亦有 doc |
| G-17 | PASS(附注) | `Sim/EntityDeathGates.cs:52-59` | 唯一「数据」是 5 键战伤白名单 —— 文件自陈为**常量闭集**(PRE-S18-A),非玩法阈值;零 `TICK_SECONDS`/窗口类硬编码旋钮 |
| G-18 | WARN(中 · 性能) | `Sim/FoldedPredicate.cs:89,112,143,157` · `Sim/HistoryFolder.cs:82,89-115,140-148` | 单次 `Folded(p)` ≈ O(H+C);`HistoryFolder.Fold(p)` 再调一次 `Folded` 并**全量重扫两次 + 新建整条 Retained 列表** ⇒ ≈ 5~6 次 H 遍历 + O(H) 分配。对 P 个病人逐一折叠 = **O(P·(H+C)),P∝H 时退化为 O(H²)**;每 5 分钟 checkpoint 一次(ADR-010 §六) |
| G-19 | **DEFECT(低-中)** | `Sim/EntityDeathGates.cs:120` | `LethalityOf(entry) && !IsCombatant(actorClass)` 短路:非致死条目下 `IsCombatant` **不被调用** ⇒ `ActorClass.Unspecified` 静默返回 `false`,与 `:85-90` 声明的「fail-loud,不猜」不符;建议先校验 `actorClass` |
| G-20 | 低 · 文档 | `Sim/FoldedPredicate.cs:14-15` | 注释「**任一**病种不属于豁免类」与代码 `:123-133`(命中**任一豁免**即返回 `true`)方向相反;结论口径(保守不折)正确,仅措辞误 |
| G-21 | 低 · 一致性 | `Sim/EntityDeathGates.cs:160` vs `FoldedPredicate.cs:123-125` | `Curve == null` 时前者按「不自限(false)」、后者按「不豁免(可折)」;两处均自陈 legacy 占位,真数据一律带 `Curve`(`RegistrySchema.cs:172-177`) |
| G-22 | **已登记(高)** | `Sim/HistoryFolder.cs:19-22`(`OQ-7a-10`) | 终态存在性查询是**病人级**、折叠行按**病种**产 ⇒ 病人有 A 病终态、B 病仍在演进时,B 的全部病史行**被抹除且无折叠行**;文件自陈按权威字面实现并登记 OQ-7a-10,**不作为新发现** |
| G-23 | PASS | `Sim/HistoryFolder.cs:82-83` | 谓词为假时返回**入参同一列表**(零拷贝、零变更);`IsIdentity = Rows.Count == 0` 在谓词真时必为假(真⇒至少一条 outcome⇒至少一行) |
| G-24 | 低 · 类型安全 | `Sim/FoldRow.cs:45,50` | `Outcome` 为 `int`,注释却 `<see cref="PatientOutcome.Recovered"/>`;`PatientOutcome` 枚举已存在于 `Sim.Contracts/Payloads/HistoryPayloads.cs:55`,零值 0 在 `FoldRow` 构造处**无校验**(ADR-032 ② 的零值哨兵只在解码期) |
| G-25 | 低 · 不对称 | `Sim/HistoryFolder.cs:99-100` vs `:108-111` | 同病种重复 `onset` 静默取首条,同病种重复 `outcome` 抛 —— 同属流损坏却一静一响;建议至少加注释说明去重键由 EventStream 守(现 `:98` 已有半句) |
| G-26 | 低 · 契约文档 | `Sim.Contracts/IFoldedPredicate.cs:28,36` | `<returns>` 写「**四条件**全部成立 ⇒ true」,但 ④ 明确不在本接口(`:18-19`)⇒ 消费方若直接按 `Folded(p)` 删病人全部行,会**抹掉 `SkillGrown`**;接口自身未留 ④ 警告 |
| G-27 | 低 · 装配 | `Sim.Codec/SaveService.cs:47,59` | `folded` 可空且**无 fail-fast**;`FoldedPredicate` 属性可返回 `null`,消费方自行判断。当前无消费者,风险暂为零,但「未注入 ≠ 可折叠」这条纪律**只写在注释里**,无编译期/运行期护栏 |
| G-28 | NOT RUN | `unity/Assets/Tests/EditMode/PersistenceService/folded_predicate_test.cs`(503 行) | 本轮**未执行**测试,不对绿/红作任何判断 |

## 未及核查项(诚实清单 · 禁借绿)

1. 未运行任何编译 / UTF / 批处理构建 ⇒ 上表无一条来自执行结果,全部为读码判定。
2. 未对 `Sim.Contracts` 做**全装配成员面 diff**(仅据本轮 diff 看到纯新增)。
3. 未阅读 ADR-032 全文,只按 `grep` 命中行(④/⑤/⑥ 与附录 A)核对,未验证条款间有无本轮未覆盖的附加义务。
4. 未读 `Sim.Codec.asmdef`,「Sim.Codec 引用 Sim.Contracts」为**既有代码反推**,非直接观测。
5. 未核实 `EventKind.PatientOutcomeChanged` / `DiseaseOnset` 在 `entities.yaml` 的 `stream:` 归属(路由是否真落病史流)。
6. 未读 ADR-010 `:331` 原文行,「反射断言」依 `SaveService.cs:39-42` 转述。
7. 未测性能量级(上表 G-18 为静态复杂度分析,非实测 Profiler 数据)。
