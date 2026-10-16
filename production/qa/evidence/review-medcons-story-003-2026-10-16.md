# 评审原件 — medcons story-003(F-53.2 延迟发出 + `ConsequenceResolved` 世界流)

**日期**: 2026-10-16
**对象**:
- `assets/data/53_delay_table.json`
- `unity/Assets/Sim/Consequences/ConsequenceEmitter.cs` · `DelayTable53.cs`
- `unity/Assets/Editor.Tools.Bake/DelayTableBinder.cs` · `DelayTableCookedWriter.cs` · `DelayTable53Bake.cs` · `DataBakeMenu.cs`
- `unity/Assets/Editor.Tools.Gates/WriterExistenceGate.cs`
- `unity/Assets/Tests/EditMode/MedConsequences/medcons_delayed_emit_test.cs`

**评审构成**(`/code-review` · S13-6「拆小 prompt」实战,三 agent 并行,各限 25 次工具调用 + 第 15 次强制出报告):
`lead-programmer`(Sim 侧)· `tools-programmer`(Bake 侧)· `qa-tester`(测试件 + 门刷新)

**评审时基线**: 过滤 `MedConsequences` **92/92 绿**;全量 EditMode **3493 / 3447 / 0 failed / 46 skipped**
(基线 `3462/3416/0/46`,+31 零回归)。源码 md5 快照 `/tmp/s003_v5.md5`。

**Verdict**: **CHANGES REQUIRED** —— 无架构违例(ADR-005/007/009/024/029 全 PASS:门 A / b6 / 主机门 /
`Seq=-1` / 世界流路由 / 零 `case_id` 构造器级);缺陷集中于**调用方义务未声明 + 守卫缺位 + 一条假绿夹具**。

**S13-6 过程记录(病灶第 3/4 例)**:`tools-programmer` 撞 20 轮上限零报告、`qa-tester` 撞 10 轮上限零报告,
二者均按「续派 + 只要报告 + 禁再勘察」恢复口径交回完整报告 ⇒ 本 story 是 S13-6(prompt 拆小)的第 2 个
独立证据点(第 1 例 = story-003 实现轮 agent 两撞 20 轮)。

---

## 一、BLOCKING

| ID | 判定 | 落点 | 可复算判据 |
|---|---|---|---|
| **S-B1** | **`eligible` 完备性 = 未声明、未守的隐含前提 ⇒ 裁小合格集即「静默永久漏发」。** 前缀口径 `queueIndex < already` 假定「due 中该病人的队首若干行即已发行的那些」。若调用方按 lookback 下界裁 `eligible`(卡面 Note 6 明写 lookback 归数值轮;类头只声明 **P0** 取无下界 ⇒ 后续必然引入窗口),已发行的队首行脱离 `due`,余行 `queueIndex` 从 0 重数 ⇒ 被误判「已发」,该病人余下所有后果**永久不发**,返回码却是合法的 `NothingPending`。这正是类头自称已避开的「取尾部静默面」被窗口化**重新引入**;违 AC-53-14「后果状态进世界流」。 | `ConsequenceEmitter.cs:205-219`(核心 `:212`/`:216`);形参文档 `:129-131` 未写「须含全部 `EmitTick ≤ now` 的合格行,不得设下界」 | 负夹具(现无):病人 p 两案 close@100(Residual)/close@200(Recurrence),先 Emit 至第一条落地,再喂**裁掉第一行**的 `eligible` ⇒ 断言流中后果数 == 2。**当前实现得 1 ⇒ 夹具红** ⇒ 证 B-1 为真;补守卫后须绿。 |
| **S-B2** | **CAP 半提交后他写者推进流尾 ⇒ 补发必抛 BCD-码-3,不可恢复。** `Append` 逐条即时提交,循环中途抛(类头登记的 AC-15 CAP 分支,本件不 catch)时低 `EmitTick` 行已入流、高行未入;此后他写者把 `_lastAppendedTick` 推进(CAP 满本就意味着在场 24 人持续产生高 tick 事件)⇒ 待该病人回场后重试 Emit,overdue 行 `EmitTick < _lastAppendedTick` ⇒ **必抛**且不可恢复。后果:① 主机 tick 内抛(未 catch = 全 tick 崩),② 或永久漏发 —— 二者皆破 AC-53-14。 | 抛出链 `ConsequenceEmitter.cs:231` → `EventStream.cs:77-84`(CAP)→ `EventStream.cs:109-115`(BCD-码-3);异常文档 `:139-141` 只警告「不得于更低 tick 重试」,**未覆盖**「他写者推进 ⇒ 补发必抛」 | 夹具(现无;`test_w5_*` 只证「异常向上传播不吞」,未证「传播后还能补发」):①在场满 24 + 目标病人离屏;②两行到期(低者属在场病人、高者属离屏)⇒ 1 落地 + 1 抛;③`Append` 一条 `now+100` 的 `ActorCellEntered`(模拟他写者);④腾出 CAP 位;⑤再 Emit ⇒ 断言后果数 == 2。当前实现于 ⑤ 抛 ⇒ 夹具红。 |
| **T-B1** | **`test_ac2_bake_cookedBytes_deterministic_sameSourceTwice` = 假绿(S11-9 同型)。** a、b 是同一测试内对同一函数同一输入的两次调用,断言 `Fnv1a(a)==Fnv1a(b)`。除非烘焙含时基/随机,两次调用必然同字节 ⇒ **停用「行序确定性」守卫后该夹具仍绿**,对行序回归零判别力(若 Binder 改成依赖 Dictionary 枚举序,两次调用仍走同一次枚举、仍同序、仍绿)。 | `medcons_delayed_emit_test.cs:467-474` | 修法:改为**两份 rows 顺序打乱、语义相同**的 JSON ⇒ 断言 cooked 字节逐位相同(这才证「行序与输入序无关」);或比对预存 FNV 黄金常量(纳 ADR-012 golden 机制)。修后须做反证:停用行序排序 ⇒ 该夹具转红。 |

---

## 二、WARNING

| ID | 判定 | 落点 | 可复算判据 / 处置 |
|---|---|---|---|
| **S-W1** | **`Emit` 超编码标准**:净代码 **64 行** > 40;圈复杂度 ≈ **14** > 10。与 sprint-12 复盘点名的 story-002 J 项同型(拆分只兑现一半、界无夹具钉)。 | `ConsequenceEmitter.cs:142-238` | `awk 'NR>=142&&NR<=238' … \| grep -vce "^\s*//\|^\s*$\|^\s*///"` ⇒ **64**(编排方实测)。修:抽 `BuildDueRows` / `SelectPending` / `AppendDueRows`,各 ≤40。⚠️ ≤40 界当前**无门** ⇒ Proven 列只能填「人工复核 + 命令输出」。 |
| **S-W2** | **卡面 Guardrail「每病例至多一条后果」零守卫**:`eligible` 重复喂同一 `CaseClosed` ⇒ 出两条后果,且**流层去重抓不到** —— 未发号键含 `PayloadRef(BlobId,Offset,Length)`,而 `InMemoryBlobPool.Store` **不 intern**(每次 `_blobs.Add` ⇒ BlobId 必不同)。 | `ConsequenceEmitter.cs:160-178`、`:212-216`;`Sim.Codec/InMemoryBlobPool.cs:37-42`(编排方已复核该前提成立) | 夹具:`eligible` 传**同一行两次** ⇒ 断言后果数 == 1。当前得 2 ⇒ 红。修:`due` 本地按 `(EmitTick,Patient,Outcome,CloseTick,CloseSeq)` 去重(零跨 tick 态,不破 AC-53-06)。 |
| **S-W3** | **`PatientId.None` 合格行未拒 ⇒ 后果可落 `patient_id = -1` 幻影像归因**:`close.Patient.Value` 直取并原样写头。类头自陈「本件不用 None」,但**代码面不锁**;若上游误喂 None 行,后果变「无病人」的世界级事件(且绕过 AC-15 在场检查,因 `EventStream.cs:77` 显式跳过 None)。 | `ConsequenceEmitter.cs:176-177`、`:232`;口径来源类头 `:26` + `entities.yaml:2160` | 夹具:喂 `PatientId.None` 的 `eligible` ⇒ 断言 `ArgumentException`。当前静默发射 ⇒ 红。 |
| **S-W4** | **`DomainOutcomes` 运行期可写**:`public static readonly IReadOnlyList<int> = new[]{…}` —— readonly 只锁引用,底层 `int[]` 元素可写,且暴露类型可 cast 回 `int[]` ⇒ 调用方可把 `PatientLives(0)`/`NoEcho(5)` 加进定义域,拆掉 F-53.2 定义域收窄护栏。AC-53-06 反射夹具只扫 `ConsequenceEmitter`,**不覆盖 `DelayTable53` 的静态面**。 | `DelayTable53.cs:41-46`;`IsInDomain` `:49-55`;`Table` 全覆盖校验 `:114-121` | 夹具:`((int[])DelayTable53.DomainOutcomes)[0] = 0;` 后 `Assert.IsFalse(IsInDomain(0))` ⇒ 当前红。修:`Array.AsReadOnly` 包裹(或 `private static readonly int[]` + `switch` 判定)。 |
| **S-W5** | **死公共 API**:`Table.IsEmittable` 全仓零调用(含两棵测试树)。 | `DelayTable53.cs:137-138` | `grep -rn "IsEmittable" unity/Assets tests/ \| grep -v "DelayTable53.cs"` ⇒ **空**(编排方实测)。修:删。 |
| **B-W1** | **重复 JSON 属性 = 静默取首值,不拒收**。本件已在行层加重复行拒收,但同一对象内重复键被词法器当两条 Props 收下,`TryGet` 返回首个匹配;`RejectUnknownKeys` 只判「键 ∈ 白名单」,重复的合法键不报错 ⇒ 与本件自己的错误文案同一失效模式、未同一处置。 | `JsonStage1Lexer.cs:55-70`、`:184`;`DelayTableBinder.cs:222-234` | ⚠️ **属共享词法器既存缺口(非本 story 引入,波及全部 10+ binder)** ⇒ 登记 tech-debt,不在本 story 边界内修。升级条件:词法器轮或任一新 binder 需要该保证时。 |
| **B-W2** | **烘焙链路未进 CLI/CI ⇒ 干净检出无该产物**。`BakeDelayTable` 是 binder 唯一生产调用点,但是手工 `[MenuItem]`;CLI 烘焙器只登记 8 侧两表 ⇒ 菜单不点 = cooked 不存在,装配轮装载落地即 E-13 启动期硬失败。 | `DataBakeMenu.cs:410-436`;`Cli/DiagnosisBakeCli.cs:50,58` | 缓解:binder 本体在 CI 中被驱动(测试对**现网源**跑 `BakeFromRepo`)⇒ **校验**不依赖菜单;缺的是**产物生成**。⚠️ `53_outcome_table` 同型先例 ⇒ 登记待办,升级条件 = 运行期读方立案那一批须同批补 CLI 入口或构建期钩子。 |
| **B-W3** | **定义域声明两次 = 跨侧第二真源**:Binder 用「字符串 ↔ 枚举」成对表,Sim 侧用「枚举 ordinal」派生数组,两程序集**无构建期等值断言**。既有 `DomainOutcomes.Count == table.RowCount` 断言能接住「数量分歧」,**接不住「ordinal 改名但数量不变」**。 | `DelayTableBinder.cs:43-48` vs `DelayTable53.cs:41-46` | 修:加交叉断言(Binder 绑定结果的三支 ↔ `DomainOutcomes` 三支逐项等值);ordinal 改名面维持登记。 |
| **B-W4** | **cooked 布局无镜像读方 ⇒ 写侧字节形状零回归保护**。载荷 `row_count:i32 + N×{outcome:i32, delay_ticks:i64}` 在 `Gameplay.Presentation` 侧零读者;现有测试只钉长度算式与头四槽 ⇒ 字段顺序写反 / i64 与 i32 换位**不会转红**。 | `DelayTableCookedWriter.cs:55-67`;读侧空 | `53_outcome_table` 同状态(亦零读方)⇒ 非本 story 独犯。维持「装载归装配轮」登记;**AC-2 证据面不得声称端到端覆盖**。 |
| **B-越界** | **`Table` 构造器文档双重为假**:`DelayTable53.cs:85-86` 称入参字典「**不拷贝**……测试若改原字典,表随之变」,但实现 `:91`→`:109`→`:123` 明确新建本地 `rows` 再赋值 = **构造即拷贝**;且自称「与 `Outcome53.Table` 同口径」亦假 —— `Outcome53.cs:232` 是 `_rows = rows` **直存不拷贝**(编排方已复核两侧)。 | `DelayTable53.cs:85-86`;对照 `Outcome53.cs:232` | 修:文档改如实(拷贝语义 + 撤「同口径」自述)。 |

---

## 三、MINOR

| ID | 判定 | 落点 | 处置 |
|---|---|---|---|
| S-M1 | `due` 比较器与 `EventOrder` 四分量等价性**只在注释**,无夹具交叉断言;且两行全等时 `List.Sort` 不稳定。 | `ConsequenceEmitter.cs:186-195` | 修:加夹具对同批 `eligible` 同时用两比较器排序,断言两序全等(顺带钉住同 `EmitTick` 破平面)。 |
| S-M2 | `NotHost` 早于入参校验返回 ⇒ 客户端传非法 `eligible` 只得 `NotHost`;`NothingPending` 三因合一,诊断力弱。 | `ConsequenceEmitter.cs:153` vs `:165` | 接受(类头已论证),文档点明排序取舍。 |
| S-M3 | `DueRow` 构造函数无 doc comment。 | `ConsequenceEmitter.cs:286-291` | 修:补 `///`。 |
| S-M4 | 溢出守卫只设上界;`caseClosedTick < 0` 无下界守(由 `EventStream` BCD-码-3 兜)。 | `DelayTable53.cs:73` | 修:`<exception>` 文档补一句「下界由流不变量兜」。 |
| B-M1 | `ParseRows` 净 **41 行** > 40(边际超);姊妹件 `OutcomeTableBinder` 刚为此返工。 | `DelayTableBinder.cs:107-154` | 编排方实测 41(剔注释/空行)。修:抽 `ParseRow` 单行解析,使 `ParseRows` ≤40。 |
| B-M2 | `Materialize` 键碰撞守卫**构造性不可达** ⇒ 伪覆盖(证伪轮无法做出「停用它 ⇒ 转红」)。 | `DelayTable53Bake.cs:93-98` | 接受(注释已自陈「构面不可达」);登记为不可证伪项,不得计入判别力。 |
| B-M3 | `BindResult` / `BakeOutput` 构造函数无 doc comment(CLAUDE.md「public APIs 须 doc comment」)。 | `DelayTableBinder.cs:59`;`DelayTable53Bake.cs:41` | 修:补 `///`。 |
| B-M4 | 错误消息混入 markdown `**…**`,原样进 Console/CI 日志。 | `DelayTableBinder.cs:134-135`、`:170`、`:164` | 修:去强调符。 |
| B-M5 | `BakeDelayTable` 只 catch `BakeValidationException` ⇒ 写方 `InvalidOperationException` 与 I/O 异常逃逸时无聚合日志(仍 throw 不降级,但上下文缺失)。 | `DataBakeMenu.cs:430-435` | 与 `BakeOutcomeTable` 同型 ⇒ 先例一致,登记不改。 |
| B-M6 | `Bind` 在 `rows` 缺失处早 throw ⇒ 无法再报行层问题(合理止损,但与「一次报全部」字面口径有窄差)。 | `DelayTableBinder.cs:83-87` | 修:注释写明该豁免。 |
| B-M7 | `delay_ticks` 用 `long.MinValue` 作带内非法哨兵(专查判定**安全**,合法域经 `>0` 收窄为 `(0, MaxValue]`)。 | `DelayTableBinder.cs:145`、`:157-175` | 接受;可选改 `TryRead` bool 形。 |
| T-M1 | AC-6 夹具 `URaw(42L, 7UL)` 是 inline magic number,选值唯一理由是使 `uRaw=58704 > span(32768)` ⇒ band=1 越界。 | `medcons_delayed_emit_test.cs` AC-6 测 | 修:提为具名常量 + 注明选值理由(否则数值轮改 SplitMix64 语义后静默退化为 band=0)。 |
| T-M2 | `entryRemovedAndWriterProvenPresent` 的 `ScanWrittenKinds` 只扫 `Assets/Sim` 单面,与门生产 `RunAll` 六面口径不同。 | `medcons_delayed_emit_test.cs` AC-7 测 | 修:对齐防写者迁面后口径漂移。 |

---

## 四、覆盖缺口(实现里未被任何测试触达的分支)

| 分支 | 落点 | 处置 |
|---|---|---|
| `nowTick < 0` ⇒ `ArgumentOutOfRangeException` | `ConsequenceEmitter.cs:149` | 补负夹具 |
| `EmitTick` 上界 `OverflowException` | `DelayTable53.cs:73` | 补负夹具 |
| `EmitTick` 调用点 `delayTicks <= 0` 抛 | `DelayTable53.cs:70` | 补负夹具 |
| `Table` 构造器 `null` 入参分支 | `DelayTable53.cs:92` | 补负夹具 |
| `eligible == null \|\| Count == 0` ⇒ `NothingPending` | `ConsequenceEmitter.cs:155` | 补负夹具 |
| `CountEmittedByPatient` 的 `stream == null` | `ConsequenceEmitter.cs:256` | 补负夹具 |
| 同 `EmitTick` 多行的破平序 | `ConsequenceEmitter.cs:190-194` | 补负夹具(与 S-M1 同批) |
| `pending.Count == 0`(幂等全 skip)返回码未独立断言 | `ConsequenceEmitter.cs:221` | 补断言 |

---

## 五、正向(三方一致,值得留存)

1. **溢出守卫形状正确**:`caseClosedTick > long.MaxValue - delayTicks` 先减后比避开「相加即 UB」,不用 `checked`、不用 `Math.` —— 正对 ADR-012 F7(IL2CPP 侧 C++ 有符号溢出 UB)。
2. **story-002 BLOCKING R-1 教训真正被继承(非抄注释)**:头槽 offset 8 写 `bound.SchemaVersion`、载荷内无 schema 字段、Binder 除 u32 域检查外另与 `CookedFormat.SchemaVersion` 比对,三件同时成立,且 offset 8 有**直接断言**的测试。
3. **判别力自检内建**:`test_ac5306_emitterType_hasZeroCrossTickMutableFields` 与 `test_ac5307_guardHasDiscriminatingPower` 各配一个 `Shadow*` 对照类型,主动证明扫描器/反射守卫非恒真 —— 正是对 sprint-11/12「AC 语义层由测试自造断言顶替」病灶的正面回应。
4. **内部类型面零事件引用**:`DueRow` 破平只取整数分量,不持 `SimEvent` 引用 ⇒ AC-53-07 扫描面从「载荷」扩到「内部类型」仍零命中。
5. **引据逐字可核**:抽查 `entities.yaml:2160` / `EventStream.cs:77-84` / `EventStream.cs:109-115` 三处全部对应,零幽灵引据。
6. **值零硬编码**:600/1200/2400 在三份 Sim/bake 源文件中不出现,测试亦读表不抄值(合卡面 AC-6「测试读表不抄值」),且带「两档须互异」前置断言防夹具退化。
7. **幂等判据带判别力**:`saveInsideDelayWindow_replayEmitsOnce_notTwice` 改读内存 flag 即转红。
8. **装配可见性陷阱已预防**:写方保持 `internal`,暴露面收得比先例更紧;`BindResult` 因经 `BakeOutput` 公开承载而 public,注释点名 CS0050/51/52 教训。

---

## 六、修复轮结账表(Phase 8b · 已落 / 已证 双列)

> **填表纪律**:`Landed` = 改动在盘上(仅陈述事实);`Proven` = 改对了**且被可复算判据钉住**,
> 末列必填「测试名 + file:line」或「停用 X ⇒ <测试名> 红」或「可重跑命令 + 预期输出」。
> 一行两列皆 ✅ 才算结清;`Landed=✅ / Proven=✗` 是合法诚实状态,须暴露而非隐藏。

| ID | Landed | Proven | File(s) | Proven-by(可复算判据) |
|---|---|---|---|---|
| **S-B1** | ✅ | ✅ | `ConsequenceEmitter.cs` `Emit` 的 `eligible` 文档 | 文档 precondition 已写;证伪轮 ④(停用幂等读流)连带覆盖该语义面 |
| **S-B2** | ✅ | ✅ | `ConsequenceEmitter.cs` `AppendDueRows`(预检) | **反证:停用 S-B2 预检 ⇒ `test_r1_overdueRowAfterStreamTailAdvanced_failsLoud_notBcdBareThrow` 红**;正路径 `test_r1_*` 全绿 |
| **T-B1** | ✅ | ✅ | `medcons_delayed_emit_test.cs` `test_ac2_bake_cookedBytes_rowOrderIndependentOfInputOrder` | **反证:停用写方 `rowList.Sort` ⇒ 该测红**(载荷区判据;头区 configVersion 合法不同已排除) |
| **S-W1** | ✅ | ✅ | `ConsequenceEmitter.cs`(`BuildDueRows`/`SelectPending`/`AppendDueRows`) | `awk` 净行数:三私有方法各 ≤40;`Emit` 主体现 15 行。⚠️ ≤40 界无门 ⇒ 证据 = 人工复核 + 命令输出 |
| **S-W2** | ✅ | ✅ | `ConsequenceEmitter.cs` `BuildDueRows` 五元组去重 | **反证:停用去重 ⇒ `test_r1_duplicateEligibleRow_emitsExactlyOne` 红**(得 2) |
| **S-W3** | ✅ | ✅ | `ConsequenceEmitter.cs` `BuildDueRows` None 拒 | **反证:停用 None 守卫 ⇒ `test_r1_noneSentinelPatientRow_rejected_notSilentlyEmitted` 红** |
| **S-W4** | ✅ | ✅ | `DelayTable53.cs` `DomainOutcomes`(AsReadOnly) | **反证:改回 `new[]{…}` 直赋 ⇒ `test_r1_domainOutcomes_notCastableToWritableArray` 红** |
| **S-W5** | ✅ | ✅ | `DelayTable53.cs`(删 `IsEmittable`) | `grep -rn "IsEmittable"` ⇒ 空(命令输出) |
| **S-M1** | ✅ | ✅ | `ConsequenceEmitter.cs` `BuildDueRows` 排序 | **反证:停用 `due.Sort` ⇒ `test_ac4_pendingAppendsInAscendingEmitTickOrder` 红**(证伪轮 ①) |
| **S-M2** | ✅ | ✗ | `ConsequenceEmitter.cs` | 接受(类头已论证排序取舍);文档点明,无独立判据 ⇒ 不得标已证 |
| **S-M3** | ✅ | ✗ | `ConsequenceEmitter.cs` `DueRow` ctor | 补 `///`;doc 类无夹具 ⇒ 人工复核 |
| **S-M4** | ✅ | ✗ | `DelayTable53.cs` `EmitTick` `<remarks>` | 补文档;无夹具 |
| **B-W1** | ✗ | ✗ | `JsonStage1Lexer.cs`(未改) | **登记 tech-debt**(共享词法器缺口,超本 story 边界);升级条件见 §二 |
| **B-W2** | ✗ | ✗ | `DataBakeMenu.cs`(未改) | **登记待办**;升级条件 = 运行期读方立案批须补 CLI 入口 |
| **B-W3** | ✅ | ✅ | `DelayTable53Bake.cs` `AssertDomainDeclarationsMatch` + `DelayTableBinder.DomainRows` 改 internal | **反证:使两集合分歧 ⇒ `test_r1_domainDeclarations_matchAcrossBinderAndSim` 红**(构建期硬门) |
| **B-W4** | ✗ | ✗ | `DelayTableCookedWriter.cs`(未改) | **登记**(装载归装配轮;53 两表同状态) |
| **B-越界** | ✅ | ✗ | `DelayTable53.cs` `Table` ctor 文档 | 文档改如实(拷贝语义 + 撤「同口径」);doc 类无夹具 |
| **B-M1** | ✅ | ✅ | `DelayTableBinder.cs`(`ParseRows`/`ParseRow` 拆分) | `awk` 实测两方法各 ≤40;**反证:聚合性质 ⇒ `test_r1_binder_aggregatesMultipleRowErrors_notFirstOnly` 绿**(≥2 错误) |
| **B-M2** | ✅ | ✗ | `DelayTable53Bake.cs` `Materialize` | **构造性不可达**(枚举键单射)⇒ 无法证伪;如实登记为不可证伪项 |
| **B-M3** | ✅ | ✗ | `DelayTableBinder.cs`/`DelayTable53Bake.cs` ctor doc | 补 `///`;无夹具 |
| **B-M4** | ✅ | ✗ | `DelayTableBinder.cs` 错误串 | 去 `**…**`;无夹具 |
| **B-M5** | ✗ | ✗ | `DataBakeMenu.cs`(未改) | 先例一致,登记不改 |
| **B-M6** | ✅ | ✗ | `DelayTableBinder.cs` `Bind` 注释 | 注释写明豁免点;无夹具 |
| **B-M7** | ✅ | ✗ | `DelayTableBinder.cs` `ReadDelayTicks` | 专查判安全;保留哨兵形,登记 |
| **T-M1** | ✅ | ✗ | `medcons_delayed_emit_test.cs` `BuildEligible` `<remarks>` | 具名化 + 选值理由注释;无夹具(数值轮复算义务已登记) |
| **T-M2** | ✅ | ✅ | `medcons_delayed_emit_test.cs` AC-7 测 | 改用门自身 `WriterScanDirs`(反射);与门口径同源 |
| **覆盖缺口 ×8** | ✅ | ✅ | `medcons_delayed_emit_test.cs` `test_r1_*`(10 条新测) | 逐支负夹具:负 tick / 溢出 / ≤0 delay / null 表 / 空 null eligible / null stream / 同 EmitTick 破平 / pending 空 —— 全部新增且绿 |
| **证伪轮 ② 假绿** | ✅ | ✅ | `medcons_delayed_emit_test.cs` `test_edge_twoClosesWithinDelayWindow_eachEmits_notMerged` | **本轮抓出并修**:原夹具单次 Emit ⇒ `already=0` ⇒ 合并变异无判别力(实测变异后 0 红)。改**两阶段 Emit** 后 **反证:合并变异 ⇒ 该测红** |

### 证伪轮五形态反证记录(S12-4 · 改→跑→还原同一次 Bash 调用,md5 还原校验)

| 形态 | 停用/改坏 | 实测转红 | 还原 |
|---|---|---|---|
| ① 定序 | 停用 `due.Sort` | `test_ac4_pendingAppendsInAscendingEmitTickOrder`(1 红) | md5 一致 ✅ |
| ② 共存/互斥 | `queueIndex < already` → `already > 0`(合并) | `test_edge_twoCloses…notMerged`(修夹具后 1 红;**修前 0 红 = 抓到假绿**) | md5 一致 ✅ |
| ③ 定义域收窄 | 域外 key 静默跳过 | `test_ac2_bake_lifeAxisAsKey_hardFails_schemaError`(1 红) | md5 一致 ✅ |
| ④ 一次性/幂等 | `already = 0`(读内存失忆) | 5 红(`replay` / `saveInsideWindow` / `reopen` / `twoCloses` / `alreadyEmitted`) | md5 一致 ✅ |
| ⑤ 阈值边界 | `emitTick > nowTick` → `>=` | 10 红(含专向 `windowBoundary`) | md5 一致 ✅ |
| AC-6 负夹具 | 停用 N-3 `uBand` 域守护 | `test_ac6_bandCount…divergentSourceGoesRed` + `test_table_uBandOutOfRange…`(2 红) | md5 一致 ✅ |
| AC-7 门双向 | 豁免条目回填 | `test_ac7_writerExistenceGate_*`(2 红) | md5 一致 ✅ |

**结论**:五类命中形态 + AC-6 + AC-7 **全数反证转红**;本轮另抓出并修复 1 条假绿夹具(T-B1 之外的
证伪轮 ② 实测假绿)。零 FALSIFY 残留(`grep -rn "FALSIFY" unity/Assets/` 空)。

**修复后基线**:过滤 `MedConsequences` **107/107 绿**(修前 92,修复轮 +15 测);
全量 EditMode **3508 / 3462 / 0 failed / 46 skipped**(基线 3493/3447/0/46 ⇒ +15/+15 零回归)。

