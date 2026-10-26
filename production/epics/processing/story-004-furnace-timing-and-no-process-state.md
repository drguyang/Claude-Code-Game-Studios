# Story 004: 执行时序:单炉并发模型、无进程态、重放等值

> **Epic**: 炮制
> **Status**: Complete
> **Layer**: Core
> **Type**: Logic
> **Estimate**: 6h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-10-26

## Context

**GDD**: `design/gdd/processing.md`(规则八 时序与并发 · OQ-18-3[甲] · OQ-18-5 已结清 · 规则七 · AC-18-12/13/17/23/15)
**Requirement**: TR-processing-012(完成时点 = Tick(start)+duration_ticks,tick 计零墙钟零浮点)· TR-processing-015(并发模型:每玩家单炉、不可取消、拒绝零事件、跨玩家并发允许,⚠️ partial —— OQ-18-8 器具互斥归 24)· TR-processing-016(发起即落流、无进程态,7a 只存事件流)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-005(主): 主机唯一 Step/Append + 事件流唯一真源 ⇒ 「进行中」= 派生量,非状态 · ADR-010(次): 存档 = 头部+三流+快照,18 无段 · ADR-011 Amendment B(次): 点火意图归主机终裁
**ADR Decision Summary**: `CompleteTick = start_tick + duration_ticks`(F-18.3,纯 tick 域,`ITickProvider` 驱动,20 Hz 已标定 ⇒ 1 tick = 50 ms 量纲);单炉不可取消(OQ-18-3[甲]):第二点火意图 ⇒ 拒绝 + 零事件;同 tick 两玩家各一点火 ⇒ 两条 `Craft` 均落流(器具互斥归 24 = OQ-18-8,18 不判);点火时点产出装不下 ⇒ 20 整体拒绝 ⇒ **零 `Craft` 落流、库存逐位不变、炉不点燃**(R-18-D 前半,AC-18-12);无进程态三重门(AC-18-17):18 程序集零 `[Serializable]` + 7a 段清单无 18 记录 + 重放重建 `CompleteTick` 与直算逐位相同。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: 全部判据为门 A 纯 C# + 程序集属性扫描,零引擎 API;AC-18-15 跨平台三出参逐位属 ADR-012 矩阵(EXTERNAL·BLOCKED-BY,矩阵不存在 + F7 未跑 ⇒ 本故事只交 Mono 侧重放等值)。

**Control Manifest Rules (this layer)**:
- Required: 炉状态 = `Craft` 流前缀的派生(每玩家「最后一炉」由流重放算出,禁独立持久字段跨存档存活 —— 内存缓存允许但键须纯 (流前缀, tick));`duration_ticks` 为烘焙 int;`ITickProvider` 步相位(承 tick 裁定 ③:每 tick 恰一次 Step,不由渲染帧驱动)
- Forbidden: `DateTime` / `Stopwatch` / `Time.*`(墙钟与渲染帧符号)入结算/完成路径(IL 零符号引用);取消语义(P0 不可取消);18 自建「进行中进度」持久态;为性能把炉表写进快照
- Guardrail: 器具共享语义(OQ-18-8)未裁 ⇒ 18 只判「玩家自身单炉」,不判器具占用(该半边外抛 24,禁私裁);AC-18-12 的拒绝发生在时点(裁决前),结构上不存在「中途回滚」路径

---

## Acceptance Criteria

*From GDD `design/gdd/processing.md`, scoped to this story:*

- [x] **AC-18-13**: `CompleteTick` 路径 IL 层扫描 ⇒ 零浮点、零墙钟 API(`DateTime`/`Stopwatch`/`Time.*` = 零符号引用)
  - **载体**: `test_ac1813_sourceSurface_hasNoFloatingPointTypes` · `test_ac1813_sourceSurface_hasNoWallClockIdentifiers` · `test_ac1813_ilSurface_hasNoFloatOpcodes` · `test_ac1813_ilSurface_hasNoWallClockSymbolReferences` · **正控** `test_ac1813_scannerPositiveControl_detectsInjectedWallClock`
  - ⚠️ **IL 面只证 Mono 后端**(见下方边界 ③)
- [x] **AC-18-12**: 点火时点产出不装下 ⇒ 主机终裁**零 `Craft` 事件落流**、库存逐位不变、炉不点燃(R-18-D 前半:拒绝在时点)
  - **载体**: `test_ac1812_outputExceedingCapacity_isRejectedAtIgnitionTick` · `test_ac1812_rejection_appendsZeroCraftAndLeavesInventoryBitIdentical` · `test_ac1812_exactBoundary_isAdmitted` · `test_ac1812_multiLineAccumulation_rejectsTheLineThatOverflows` · `test_ac1812_multiLineAccumulation_usesFullRowWeightNotUnitWeight` · `test_ac1812_currentLoadIsCounted_soExistingWeightCanBlockIgnition` · `test_ac1812_emptyOutputs_isTriviallyAdmitted` · **负控** `test_ac1812_rejectionAssertion_hasDiscriminatingPower`
  - ⚠️ **`20.Apply` 不存在**(归 S20-14)⇒ 本件交付的是**准入判据**,接线是夹具内 seam(见下方边界 ①)
  - ⚠️ **「库存逐位不变」的判别力来自负控,不是正例**(见下方边界 ②)
- [x] **AC-18-23**: 某玩家已有一炉在烧 ⇒ 第二点火意图被拒 + 零事件(单炉,OQ-18-3[甲]);同 tick 两玩家各一点火 ⇒ 两条 `Craft` 均落流(跨玩家并发允许;器具互斥归 24,18 不判)
  - **载体**: `test_ac1823_secondIgnitionWhileBurning_isRejectedWithZeroEvents` · `test_ac1823_completionTickItself_isIdle` · `test_ac1823_crossPlayerConcurrency_isAllowed` · `test_ac1823_sameTickTwoPlayers_bothBusy` · `test_ac1823_noCancelApiExists` · `test_ac1823_outOfOrderStream_givesSameAnswer`
- [x] **AC-18-17**: 一炉进行中 ⇒ ① 18 程序集零 `[Serializable]` 类型;② 7a 存档段清单不含任何 18 记录;③ 重放重建的 `CompleteTick` 与直算逐位相同(OQ-18-5;措辞按 GDD 订正 = 程序集属性 + 段清单两处机器可断言面)
  - **载体**: `test_ac1817a_processingNamespace_hasNoSerializableTypes` + **负控** `test_ac1817a_serializableScan_hasDiscriminatingPower` · `test_ac1817b_saveComposerReferencesNoProcessingTypes` · `test_ac1817c_replayRebuiltCompleteTick_matchesDirectComputation` · `test_ac1817c_independentRebuilds_areBitIdentical`
  - ⚠️ **② 是降级判据**(7a 无「段清单」常量;见下方边界 ④)
- [ ] **AC-18-15** [I]: 同一 `(WorldSeed, recipe, 输入实例集, 两 EnvMod 分量)` 三格 CI 重放 ⇒ 三出参逐位相同。**⚠️ EXTERNAL · BLOCKED-BY-ADR-012**(矩阵不存在 + F7 spike 未跑 ⇒ **不勾、不写桩**;夹具限定每 tick 单 `Craft`,D-21-28 已裁[甲]但 Seq 发号跨平台逐位同属实测面)
  - 🔁 **2026-10-27 GDD 措辞订正(本行不复述,以 GDD 为准)**:上面引的 `GIVEN` 元组是**成文时快照**,已过时。GDD `processing.md` AC-18-15 现作 `(recipe, L, 输入实例集, EquipMod, 两 EnvMod 分量)` —— 删 `WorldSeed`(18/21a 侧零消费,冗余)+ 补 `L` / `EquipMod`(F-18.1 契约主变量,原漏列 ⇒ 两平台各取不同值而「逐位相同」仍成立)。**承「已 Complete 卡面的判据只加注不追改」** ⇒ 本行原文保留;订正详注见 `design/gdd/processing.md` AC-18-15 下方。⚠️ 本条仍是 **EXTERNAL 不勾**,订正**不改变**其未验收状态。
  - ⚠️ **本件交付的 Mono 侧重放等值 = AC-18-17 ③,不是 AC-18-15 的证据**。两者同源但判据面不同:17③ 是「同平台两次重放同值」,15 是「三个平台逐位同值」。<br>⇒ **禁止**把 `test_ac1817c_*` 读成 AC-18-15 已覆盖。

---

## Implementation Notes

*Derived from ADR-005 §Decision(主)/ ADR-010 §一:*

1. 炉状态查询 `IsBusy(actor, tick)` / `CompleteTickOf(actor, tick)` = `Craft` 前缀纯函数(取该 actor 最新一条 `Craft`:`busy ⟺ tick < start+duration`);单实例内存缓存合法但实现「从空重建等值」测试(同 foraging Story 004 前缀纪律,共用扫描基建)。
2. 完成判定 = tick 比较(Story 005 的调度消费本条谓词);整型加法无溢出路径(duration 上限烘焙期校验,装载纪律同 Story 002 的硬失败族)。
3. 点火事务(承 Story 003 序):准入 → **本故事单炉判定** → F1 → 20.Apply(容量先验)→ Append。AC-18-12 用 spy-sink 断「Apply 拒绝分支上零 Append」;「炉不点燃」= 单炉谓词在拒绝后仍 false(时点拒绝无副作用)。
4. 墙钟扫描:IL/符号引用扫描 `System.DateTime`/`Diagnostics.Stopwatch`/`UnityEngine.Time` 在 18 结算路径零命中(扫描器与 AC-18-02 同一驱动,新增标识符表)。
5. 7a 段清单断言:读 ADR-010 §三 义务汇总表生成物(若 7a 尚无机器可读清单,以「存档格式常量表不含 Processing 段」静态断言替代,接缝回写义务登记 `O-18-R8` 类注记,不阻塞①③两面)。
6. 重放等值(Mono 侧半):同事件流两次重建 `CompleteTick`/`IsBusy` 序列逐位同;跨平台半边留 `EXTERNAL(ADR-012)` 标注。
7. 取消 = 无路径:18 不定义取消 API(禁「预留但不断言」的灰色接口 —— YAGNI + AC-18-23 判据即拒绝路径覆盖)。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 003:点火落流与铸造(本故事只做「能不能点/点完没有」的派生谓词)
- Story 005:完成时刻的调度执行、起货呈现 cue、溢出落地(本故事只交 `CompleteTick` 谓词)
- 24 医馆即机器:器具占用/OQ-18-8(18 不判)
- ADR-012 落地轮:三格矩阵、F7 spike、Seq 发号跨平台逐位(AC-18-15 的 EXTERNAL 半边)
- 7a(ADR-010):存档 codec 本体(本故事只断「无 18 段」的形状事实)

---

## QA Test Cases

*Written at story creation (lean mode — QL-STORY-READY skipped; specs self-authored from AC text).*

- **AC-18-13**: 墙钟/浮点零符号。
  - Given: 18 程序集 IL。
  - When: 扫描三类符号 + 浮点指令。
  - Then: 结算路径零命中;负样例注入 `Stopwatch` 必红。
  - Edge cases: 测试代码与编辑器工具路径不在扫描集(按程序集归属)。
- **AC-18-12**: 容量不足时点拒绝。
  - Given: F1 产出总量 > 20 剩余容量(合成临界)。
  - When: 点火意图。
  - Then: spy-sink 零 `Craft`;`InventoryOf` 前后逐位相同;再查 `IsBusy == false`。
  - Edge cases: 临界恰等(装得下 ⇒ 成功);F1 已算但 Apply 拒 ⇒ F1 调用发生但零事件(允许:纯函数无副作用)。
- **AC-18-23**: 单炉与并发。
  - Given: actor A 一炉在烧(tick ∈ [start, complete));A 第二意图同 tick 到达;另 actor B 同 tick 一意图。
  - When: 主机裁决。
  - Then: A 第二意图拒绝零事件;B 的 `Craft` 正常落流(两条同 tick,Seq 递增)。
  - Edge cases: A 完成 tick 当拍新点火(`tick < complete` 为 false ⇒ 空闲,边界取等可点);A 取消不存在(无 API)。
- **AC-18-17**: 三重无进程态门。
  - Given: 一炉进行中;18 程序集反射 + 7a 段清单常量。
  - When: 断言。
  - Then: ① `[Serializable]` 类型计数 = 0;② 段清单零 18 记录;③ 从事件流重放,`CompleteTick` 与直算逐位等(100 点采样)。
  - Edge cases: 跨「存档往返」后仍由流重建(与 persistence 测试交叉的最小样本;codec 真身归 7a 故事)。
- **AC-18-15**(EXTERNAL): Mono 侧等值先测。
  - Given: 同参数二次求值(干净进程)。
  - When: 比对三出参。
  - Then: 逐位同(本仓可证半边);`Ignore("EXTERNAL BLOCKED-BY-ADR-012")` 的跨平台项留桩。
  - Edge cases: 夹具每 tick 单 Craft 约束显式写入夹具生成器。

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `unity/Assets/Tests/EditMode/Processing/furnace_timing_no_process_state_test.cs` — ✅ **已建,31 条全绿**
- Integration 交叉(AC-18-12 与 20):`unity/Assets/Tests/PlayMode/craft_atomicity_test.cs`(**未建** —— `20.Apply` 归 S20-14,本件无真装配可交叉;见边界 ①)

**Status**: [x] Created and passing

### 实测数

| 项 | 值 | 可跑复算命令 |
|---|---|---|
| 本夹具 | **31 / 31 passed / 0 failed / 0 skipped** | `python3 -c "import xml.etree.ElementTree as ET;r=next(ET.parse('unity/Logs/s20-9-fix.xml').iter('test-run'));print(r.get('total'),r.get('passed'),r.get('failed'),r.get('skipped'))"` |
| 全量 EditMode(评审修复后) | **5385 / 5350 / 0 / 35** | `python3 -c "import xml.etree.ElementTree as ET;r=next(ET.parse('unity/Logs/s20-9-review-full.xml').iter('test-run'));print(r.get('total'),r.get('passed'),r.get('failed'),r.get('skipped'))"` |
| 基线(截至 2026-10-26 21:38 · S20-12 收口) | **5354 / 5319 / 0 / 35** | `python3 -c "import xml.etree.ElementTree as ET;r=next(ET.parse('unity/Logs/s20-12-full.xml').iter('test-run'));print(r.get('total'),r.get('passed'),r.get('failed'),r.get('skipped'))"` |
| **增量** | **+31** | `5385 − 5354 = 31` == 本件新增用例数 **31** ⇒ **零借绿、零回归** |

> ⚠️ 基线三元组:**截至 2026-10-26 21:38 · 批次 S20-12 收口 · commit `ad1b033`**(该文件即 S20-12 轮产出的全量 XML)。本件在其之后,增量全归本件。
>
> ⚠️ **本轮两次全量跑,第一次白跑**:`-quit` 会让 Unity 在测试启动前退出(23:30:42→23:31:06 共 24 秒,
> 零 XML)。**`-batchmode … -runTests` 不得带 `-quit`** —— 由 `-runTests` 自己收尾。
> 对照组证据:`unity/Logs/s20-9-review-full.log` 的 `COMMAND LINE ARGUMENTS` 段有 `-quit`,
> `s20-9-full.log`(22:40 成功那次)没有。

### 夹具 → AC 映射(31 条)

| AC | 条数 | 夹具 |
|---|---:|---|
| AC-18-13 | 4 + 2 正控 | `sourceSurface_hasNoFloatingPointTypes` · `sourceSurface_hasNoWallClockIdentifiers` · `ilSurface_hasNoFloatOpcodes` · `ilSurface_hasNoWallClockSymbolReferences` · **`scannerPositiveControl_detectsInjectedWallClock`** · **`ilFloatScan_positiveControl_detectsInjectedFloat`**(评审 W6 新增) |
| AC-18-12 | 7 + 1 负控 + 2 守卫 | `outputExceedingCapacity_isRejectedAtIgnitionTick` · `rejection_appendsZeroCraftAndLeavesInventoryBitIdentical` · **`rejectionAssertion_hasDiscriminatingPower`** · `exactBoundary_isAdmitted` · `multiLineAccumulation_rejectsTheLineThatOverflows` · `multiLineAccumulation_usesFullRowWeightNotUnitWeight` · `currentLoadIsCounted_soExistingWeightCanBlockIgnition` · `emptyOutputs_isTriviallyAdmitted` · **`nullOutputs_failsLoud`**(B1)· **`negativeLoad_failsLoudEvenWithEmptyOutputs`**(B1 连带) |
| AC-18-23 | 7 | `secondIgnitionWhileBurning_isRejectedWithZeroEvents` · `completionTickItself_isIdle` · `crossPlayerConcurrency_isAllowed` · `sameTickTwoPlayers_bothBusy` · `noCancelApiExists` · `outOfOrderStream_givesSameAnswer` · **`futureEnvelopeEvent_doesNotLeakBackIntoPastPrefix`**(W1) |
| AC-18-17 | 4 + 1 负控 | `processingNamespace_hasNoSerializableTypes` + **`serializableScan_hasDiscriminatingPower`** · `saveComposerReferencesNoProcessingTypes` · `replayRebuiltCompleteTick_matchesDirectComputation` · `independentRebuilds_areBitIdentical` |
| 域守卫(横切) | 3 | `furnaceNonPositiveDuration_failsLoud` · `furnaceStreamWithNonCraftEvents_isIgnored` · `decoderNull_failsLoud` |
| **合计** | **31** | |

> ⚠️ **四条负控/正控不是 AC 的证据,是「AC 证据有判别力」的证据**。没有它们,AC-18-13 的「零命中」
> 与 AC-18-12 的「零 Append」都可能是空集冒充绿。
>
> 📌 **`ilFloatScan_positiveControl` 的由来(评审 W6)**:前一版浮点 IL 扫描**无正控**,
> 且 `Math.Sqrt` / `Math.Pow` 的 IL 是 `call double System.Math::*` —— **不产生** float opcode,
> 也不在墙钟符号表内 ⇒ 判据有真实盲区。补的负控注入 `acc = acc * 1.5 + Math.Sqrt(acc)` 与
> `float[] buf`,要求扫描器必须扫出。**无正控的「零命中」不构成证据** —— 空集与真空集不可区分。

### Falsifiability Gate(S12-4)

**命中形态**:②共存互斥(取等边界 `busy ⟺ tick < CompleteTick`)· ③定义域收窄(duration ≥ 1 / 只认 Craft / 只认本 actor / 全序键取最大 / 空解码器)· ⑤阈值边界(逐行累计 vs 单件重量 / 拒绝行点名)。

**反证记录(14 条变异,2026-10-26 23:22–23:30 第 4 轮全部恰好预期)**:

| # | 停用什么守卫 ⇒ 哪条转红 | 红数 |
|---|---|---:|
| M1 | `IsBusy` 的 `<` 改 `<=`(取等边界)⇒ `completionTickItself_isIdle` · `sameTickTwoPlayers_bothBusy` | 2 |
| M2 | 停用 `DurationTicks < 1` 域收窄 ⇒ `furnaceNonPositiveDuration_failsLoud` | 1 |
| M3 | 停用 `Kind != Craft` 过滤 ⇒ `furnaceStreamWithNonCraftEvents_isIgnored` | 1 |
| M4 | 停用 `ActorId != actorId` 过滤 ⇒ `crossPlayerConcurrency_isAllowed` · `sameTickTwoPlayers_bothBusy` | 2 |
| M5 | 全序键改取「流里第一条」⇒ `replayRebuiltCompleteTick_matchesDirectComputation` · `outOfOrderStream_givesSameAnswer` | 2 |
| M6 | 空解码器兜底成「谁都不忙」⇒ `decoderNull_failsLoud` | 1 |
| M7 | `BusyActorCount` 空解码器兜底成 0 ⇒ `decoderNull_failsLoud` | 1 |
| M8 | 累加用单件重量而非整行重量 ⇒ `multiLineAccumulation_usesFullRowWeightNotUnitWeight` | 1 |
| M9 | 跳过 `CanCarry` 容量裁决 ⇒ 5 条 AC-18-12 拒绝类夹具 | 5 |
| M10 | 不点名被拒行 ⇒ 3 条断言 `RejectedLineIndex` 的夹具 | 3 |
| M11 | F-18.3 加号改减号 ⇒ **8** 条时间相关夹具 | 8 |
| M12 | `BusyActorCount` 不委托 `IsBusy` ⇒ `sameTickTwoPlayers_bothBusy` | 1 |
| **M13** | **前缀退回 `p.StartTick`**(W1 回归钉)⇒ `futureEnvelopeEvent_doesNotLeakBackIntoPastPrefix` | 1 |
| **M14** | **`outputs == null` 兜底放行**(B1 回归钉)⇒ `nullOutputs_failsLoud` | 1 |

**Gate 结果**:**PASS** —— **14/14** 恰如预期,`exactRed` 全绿;`──── 还原:SHA-256 逐字节校验通过 ────`;
受保护路径 `AddressableAssetsData` / `Audio` / `DataCooked` 指纹未变(`✅ 指纹未变`);
还原后复查:`grep -rn "FALSIFY-" unity/Assets/Sim/Processing/` → **0 命中**。

> ⚠️ **M13 是本节最重要的一条**:它在**合法流上完全不可见**(规则八:点火同一 tick 发 `Craft` ⇒
> `e.Tick == p.StartTick`)。它之所以存在,是因为上一轮把它误判成「等价、不可证伪」——
> 而那道守卫的意义恰恰在**非法流**。见下方「不可证伪」节与自曝错误表第 7 条。

**原件**:`tools/falsify/specs/s20-9-furnace-timing-no-process-state.json`;复跑命令见其 `_note`(须显式带三个 `--guard`,驱动器**不读** spec 里的 `guards` 键)。

#### 六轮演进(如实记录,禁只留最后一轮)

| 轮 | 结果 | 成因 |
|---|---|---|
| 首轮(编译) | 编译失败 | 夹具 `switch` 模式匹配不可达:`MemberReference` 派生自 `TypeReference` |
| 首轮(跑测) | 26/27 | 负控给了**独立 blob 池**,`PayloadRef` 指错池 ⇒ 解码炸在 codec 里 |
| 第 1 轮证伪 | 10/12 恰预期 | M10 多红 1 条 · M11 多红 3 条 —— **我漏列 `expectRed`** |
| 第 2 轮证伪 | 9/12 恰预期 | M1 多红 1 条 · M5 多红 1 条(同一漏法**第二次犯**);**M6 变异体编译失败**(`out occupancy` 未赋值 ⇒ CS0177,无结果 XML) |
| 第 3 轮证伪 | **12/12 恰预期** | 期望补齐 + 变异体补 `{ occupancy = default; return false; }` |
| **第 4 轮证伪(评审修复后)** | **14/14 恰预期** | 新增 M13 / M14;其余 12 条在**新实现**上重跑仍全中 ⇒ 判据未因修复而松动 |

> **两次自查结论(已写入 spec 的 `_why`,供下轮复用)**:
> ① **变异体必须能编译** —— 落在带 `out` / `ref` 参数或 `readonly` 字段的行上时,替换串要自己交代赋值。M6 就是没做,退出码 6 且无 XML,当时若只看「没通过」会误判成「逃逸」。
> ② **`expectRed` 要按「谁断言了被变异属性」全量枚举,不是按「我打算打哪条」** —— 四次多红全是同一漏法。上游 `grep` 夹具里所有调用点,能省掉三轮。

#### 「不可证伪」的守卫(⚠️ 此节曾登记错两条,2026-10-26 评审轮订正)

> ⚠️ **上表(旧版)有三行,其中两行是错的,已就地作废。** 原文保留在下方订正表里作闭环记录
> (承「原文不删」纪律)。
>
> **旧登记的错误**(两条同因):我把「**在合法流上等价**」写成了「**等价**」,于是判成「不可证伪」。
> 但那两个守卫的存在意义**恰恰是防御非法输入** —— 合法流上等价,不等于它们没用。
> 详见 `production/qa/evidence/review-s20-9-2026-10-26.md` §一 W1 / B1 + §四 的 M13 / M14。

| 守卫 | 旧登记(❌ 作废) | 实况 | 现处置 |
|---|---|---|---|
| `FurnaceState.cs` 前缀过滤 | 「`p.StartTick > tick` 换 `e.Tick > tick` 是**数值等价**,一切合法流相同」 | **不等价**。非法流里 `StartTick ≤ tick < e.Tick` 的 `Craft` 会被计入「截至 tick 的前缀」⇒ **一个在该 tick 还不存在的炉子回溯污染了过去时点**。旧代码**本来就是** `p.StartTick`,即 bug 属实(W1) | **改生产件**为 `e.Tick > tick`;立 **M13** 钉住(断言 `futureEnvelopeEvent_doesNotLeakBackIntoPastPrefix` 必红) |
| `IgnitionAdmission.cs` 的 `outputs == null` 短路 | 「等价变换、不可证伪」 | **不等价**。它把「调用方漏传」当成「零产出 ⇒ 恒装得下」,给 AC-18-12(BLOCKING)唯一的时点拒绝门开一条 **fail-open** 捷径(B1) | **删短路**,改抛 `ArgumentNullException`;立 **M14** 钉住 |
| `IgnitionAdmission.cs` 的 `outputs.Count == 0` 分支 | 空数组落进 `for` 零次,仍返回 `Admitted = true`,与短路返回逐位相同 | **这条是对的** —— 它是 `for` 循环的自然结果,没有 `if` 可停 ⇒ 无变异可立 | 保留。语义由「空产出恒可装」夹具覆盖;`currentLoad < 0` 的域校验已**移到循环之前**(否则空列表会绕过它) |
| `FurnaceState.cs` 的 `if (events == null) return false;` | 去掉后 `events.Count` 抛 `NullReferenceException`,与返回 false 不是同一行为;契约未规定 null 流语义 | 仍然成立 | 保留为「未判据化」 |

> 📌 **可复用教训**(已抄进 spec 的 `_note`):**判「等价变换」前,必须明确写出「对哪些输入等价」。**
> 两条错误登记的等价性都只在「合法流 / 合法配方」上成立,而守卫的存在意义正是防御非法输入 ——
> 只在合法输入上验等价,等于**用量子化后的样本证明一个全称命题**。

#### 判别面受限、已如实降级的夹具

- `test_ac1812_rejection_appendsZeroCraftAndLeavesInventoryBitIdentical` 的**正例半边恒真**:生产侧今天没有任何改动库存的路径 ⇒ 「库存逐位不变」在正例中不可能变。判别力**全部来自负控** `rejectionAssertion_hasDiscriminatingPower`(它跑同一套断言、接在绕过容量直接改库存的接线后面,必须红)。**只跑正例 = 假绿**,已在夹具内注释写明。

---

## Dependencies

- Depends on: Story 003(`Craft` 落流形状 —— 前缀谓词的输入),Story 001(准入),inventory-items Story 001/002(`InventoryOf` fold + 20.Apply 容量先验)
- Unlocks: Story 005(完成调度谓词),重放集成回归(全 epic 等值基线)

---

## Completion Notes

**Completed**: 2026-10-26
**Criteria**: **4/5 passing**(AC-18-13 · AC-18-12 · AC-18-23 · AC-18-17 已勾;**AC-18-15 不勾** —— EXTERNAL · BLOCKED-BY-ADR-012)
**Deviations**: 见下方「**六条**必须与 4/5 同读的边界」+「卡面自述订正」
**Test Evidence**: Logic —— `unity/Assets/Tests/EditMode/Processing/furnace_timing_no_process_state_test.cs`(**31 条全绿**)
**Code Review**: **Complete(2026-10-26 · CHANGES REQUIRED → 已全修)** —— 原件
`production/qa/evidence/review-s20-9-2026-10-26.md`。**1 BLOCKING(B1)+ 8 WARNING + 4 MINOR**;
其中 **B1(fail-open 绕过 AC-18-12)与 W1(前缀用了载荷 tick,回溯污染过去时点)已改生产件并各立一条变异(M14 / M13)**;
W2/W3/W4/W5/W6/M1/M3/M4 已落;**W7/W8 与 M2 明确未修**,如实登记在原件 §三 遗留(L1–L8)。

> 🔁 **2026-10-27 追记(用户裁定「L7 先修,其余立债」)**:
> - **L7(W8)已修** —— `test_ac1817b` 的 `filesScanned > 0` 换成**按名点名**(`scanned` 列表 +
>   `requiredAnchors` 必须含 `Assets/Gameplay.Boot/SaveComposer.cs`)。反证 **M-W8**(扫描根改
>   `Gameplay.Boot_NONEXISTENT`)⇒ **恰 1 条红**;修好前同一变异保持绿 = 正是 W8 说的盲区。
>   还原 SHA-256 `8b4a1dfb…b07417` 逐字节一致;修复后 `31/31/0/0`(`unity/Logs/w8-fix.xml`)。
> - **其余 7 条(L1–L6 / L8)已立债** —— `docs/tech-debt-register.md` **三十九订**,
>   `TD-processing-01…06`(L3 与 L6 合并),`P0 面` 全 `N`。**立债 ≠ 已修,六条仍 open。**
> - ⇒ 下方边界表 **③ 的「扫描面还可能半空」已失效**(L7 已修),但 **③ 的「降级判据」主句仍成立**
>   (AC-18-17② 仍是「零类型引用」而非「段清单」)。**其余五条边界一字未变。**

---

### ⚠️ 六条必须与「4/5」同读的边界

**这条分数的读法**:4 条已勾的 AC 各有一个**受限的判据面**。把任一条独立引用
(「AC-18-12 已验」「AC-18-17 已验」)都会**高报**。逐条登记:

| # | 边界 | 影响哪条 AC | 真实强度 |
|---|---|---|---|
| ① | **`20.Apply` 不存在**(归 S20-14)⇒ 本件交付的是**准入谓词 `IgnitionAdmission.CanAdmit`**,点火五步接线只存在于夹具内的 `IIgnitionApplyPort` seam(**非生产代码**) | AC-18-12 | 谓词与次序已验;**「点火事务端到端」未验**(没有被测的真装配) |
| ② | **「库存逐位不变」的正例恒真** —— 生产侧今天没有任何改动库存的路径,正例里它不可能变。判别力**全部来自负控** `rejectionAssertion_hasDiscriminatingPower` | AC-18-12 | 断言的**判别力已证**(负控必红),但**被断言的性质本身尚未被真实场景考验** |
| ③ | **AC-18-17 ② 是降级判据** —— 7a 无机器可读的「存档段清单」常量,改判为「存档组版器 / codec 零引用 18 的类型」。~~且唯一断言是 `filesScanned > 0`,**不证 `SaveComposer.cs` 在扫描集内**(评审 W8)~~ ⇒ **2026-10-27 L7 已修**:改为按名点名,`SaveComposer.cs` 不在扫描集即红 | AC-18-17 | 证的是**类型面无交叉引用**,不是 AC 原文的「段清单不含 18 记录」。~~扫描面还可能半空~~ ⇒ **扫描面已钉**(L7 修复 + M-W8 反证);**降级判据本身仍在**(另立 `TD-processing-02`) |
| ④ | **AC-18-13 的 IL 面只证 Mono 后端** —— `AssemblyGates.ScriptAssemblyPath` 取的是 `Library/ScriptAssemblies/*.dll`(Mono 产物);IL2CPP 生成物不同 | AC-18-13 | 「零浮点 / 零墙钟」在 **Mono 下成立**;IL2CPP 侧须 ADR-012 三格矩阵 —— 而这正是 AC-18-15 被 BLOCKED 的同一原因 |
| ⑤ | **AC-18-17 边界的「跨存档往返」用例缺位** —— `independentRebuilds_areBitIdentical` 做的是**换池重编码**,不等于落盘→读回;真往返需要 7a 的 `SaveComposer` / `SaveReader` 真装配(评审 §三 L8) | AC-18-17 | 「同进程内重放等值」已证;**「跨存档往返等值」未验** |
| ⑥ | **AC-18-23 的 `Seq` 定序零行为断言** —— 夹具内 `TryIgnite` 硬编码 `seq: 0`,全件仅 1 处读 `.Seq`(`:927` 重编码透传),**零断言**(评审 W7 / §三 L6) | AC-18-23 | 「同 tick 两玩家各自在烧」已验;**「同 tick 由 `Seq` 定序」未验** —— 该半条的断言对象尚未定义(L3:属 ADR-008 裁决面) |

> **④ 与 AC-18-15 是同一个洞的两种可见形态**:本件能证的等值/纯净度止于「同一后端、同一进程」。
> 凡「跨平台逐位」字样,**一律**不属本件交付面。
>
> **① 与 ⑤ 同因**:两条都缺 7a / 20 的真装配 —— 本件是**纯谓词层**,凡需要「真装配」才能问的问题都不在交付面内。

### 卡面自述订正 —— 「共用扫描基建」并不存在

Implementation Note 1 原文:

> 「…单实例内存缓存合法但实现「从空重建等值」测试(**同 foraging Story 004 前缀纪律,共用扫描基建**)。」

**实测两条都不成立**(复算命令见下):

1. **foraging story-004 尚未实现** —— `production/epics/foraging/story-004-node-remaining-prefix.md:99` 是
   `**Status**: [ ] Not yet created`。它自己都没有可被「同」的东西。
2. **全仓无「共用前缀扫描基建」** —— `unity/Assets/Sim/Foraging/` 只有四个文件
   (`ForagingConstants` / `GatherQualityRoll` / `HarvestOrchestrator` / `QualityDistributionShape`),
   其中**零个**是前缀扫描器;`InventoryFold.OrderByFullOrderKey` 是
   `private static`(`InventoryFold.cs:482`),外部够不着。

```bash
# 复算 1
grep -n '^\*\*Status\*\*' production/epics/foraging/story-004-node-remaining-prefix.md
#  → 99:**Status**: [ ] Not yet created

# 复算 2
ls unity/Assets/Sim/Foraging/ | grep -v '\.meta$'
#  → ForagingConstants.cs / GatherQualityRoll.cs / HarvestOrchestrator.cs / QualityDistributionShape.cs

ls unity/Assets/Sim/Foraging/*.cs | xargs grep -ln 'class .*Scan\|PrefixScan'
#  → (零输出)

grep -n 'OrderByFullOrderKey' unity/Assets/Sim/ItemDatabase/InventoryFold.cs
#  → 312: 调用点 · 482: private static List<SimEvent> OrderByFullOrderKey(...)
```

**处置**:本件**没有**去建这个「共用基建」—— 建了就是**为一句不成立的自述新造一件无消费者的抽象**。
`FurnaceState` 走的是自己的前缀过滤(`TryGetLatest` 的 `for` 循环),31 条夹具钉住它。
**这是本项目第 5 次「卡面自述顶替实作」**(前四次见 `project_cross_epic_fake_green_audit.md` 一族)。

> **归因**:卡面写于 2026-10-02,当时 foraging Story 004 或许被当作会先落地。
> 但登记口径不该写未来时 —— 它读起来像既成事实。**教训**:凡「同 X 的纪律 / 共用 Y 的基建」
> 这类**比较级自述**,须在实现轮当场复核 X / Y 是否真的存在;不存在就**就地订正卡面**,
> 不要顺着它去造 Y。

### 交付面

| 文件 | 类型 | 内容 |
|---|---|---|
| `unity/Assets/Sim/Processing/FurnaceState.cs` | 新建 ~230 行 | `FurnaceOccupancy`(只读结构)+ `FurnaceState`(四个 `static` 纯函数:`TryGetLatest` / `IsBusy` / `TryCompleteTickOf` / `BusyActorCount`)。**零字段、零缓存、零单例** —— AC-18-17 由类型面守,不由纪律守 |
| `unity/Assets/Sim/Processing/IgnitionAdmission.cs` | 新建 ~125 行 | `OutputLoadLine` / `IgnitionAdmissionResult` / `IgnitionAdmission.CanAdmit`。**零 `IEventSink`、零 `IPayloadEncoder`** ⇒ 结构上写不了事件 |
| `unity/Assets/Sim/Processing.meta` + 两个 `.cs.meta` | 生成 | 目录与文件元数据 |
| `unity/Assets/Tests/EditMode/Processing/furnace_timing_no_process_state_test.cs` | 新建 ~950 行 | **31** 条夹具(初版 27,评审轮 +4) |
| `tools/falsify/specs/s20-9-furnace-timing-no-process-state.json` | 新建 | **14** 条变异的证伪规格(初版 12,评审轮 +M13/M14) |

**未落地(明确不在本件交付面)**:
- `AC-18-15` 的跨平台三格 —— EXTERNAL,BLOCKED-BY-ADR-012(矩阵不存在 + F7 spike 未跑)。**未写桩**。
- `20.Apply` —— 归 S20-14。本件的 `IgnitionAdmission` **不调用**它,也不预演它。
- 取消语义 —— 刻意**零 API**(GDD 规则八「不允许取消」;AC-18-23 的判据即「无路径」)。

### 对下游的契约承诺

下一件可以直接依赖的**已验**形状:

- **`FurnaceState.IsBusy(events, decoder, actorId, tick)`** —— `busy ⟺ tick < start + duration`,**完成当拍不忙**(取等可再点火)。
- **`FurnaceState.TryCompleteTickOf(events, decoder, actorId, tick, out long completeTick)`** —— 给 S20-13 的调度用;返回的是「最后一次**点火**的完成时点」,**不**表示仍在烧。
- **`IgnitionAdmission.CanAdmit(currentLoad, outputs, capacity)`** —— 逐行累计,拒绝时点名 `RejectedLineIndex`;空产出恒准入。
- **三个入口全部 `decoder == null` 抛 `ArgumentNullException`**(fail-closed,不静默返回「谁都不忙」)。

⚠️ **下件若要在本件之上做 `20.Apply` 接线(S20-14),须自己重跑证伪轮** ——
本件证的是「谓词与次序」,不是「Apply 实现正确」。本件的 seam(`IIgnitionApplyPort`)只活在测试件里。

### 数字与复算

```bash
# 本夹具(截至 2026-10-26 23:21 · 批次 S20-9 评审修复后)
python3 -c "import xml.etree.ElementTree as ET;r=next(ET.parse('unity/Logs/s20-9-fix.xml').iter('test-run'));print(r.get('total'),r.get('passed'),r.get('failed'),r.get('skipped'))"
#  → 31 31 0 0

# 全量 EditMode(截至 2026-10-26 23:33 · 批次 S20-9 评审修复后)
python3 -c "import xml.etree.ElementTree as ET;r=next(ET.parse('unity/Logs/s20-9-review-full.xml').iter('test-run'));print(r.get('total'),r.get('passed'),r.get('failed'),r.get('skipped'))"
#  → 5385 5350 0 35

# 基线(截至 2026-10-26 21:38 · 批次 S20-12 收口 · commit ad1b033)
python3 -c "import xml.etree.ElementTree as ET;r=next(ET.parse('unity/Logs/s20-12-full.xml').iter('test-run'));print(r.get('total'),r.get('passed'),r.get('failed'),r.get('skipped'))"
#  → 5354 5319 0 35
```

**增量 `5385 − 5354 = 31` == 本件新增用例数 31 ⇒ 全量无回归。**
`skipped 35` 两侧相同 ⇒ 本件未引入新的跳过项。

> 📌 **`unity/Logs/s20-9-d.xml`(27/27)与 `s20-9-full.xml`(5381/5346)是修复轮之前的存档**,
> 保留不删作演进记录;上表「实测数」引的是**修复后**的 `s20-9-fix.xml` / `s20-9-review-full.xml`。

### 过程中自曝的错(如实留档,禁只留结论)

| # | 错 | 后果 | 订正 |
|---|---|---|---|
| 1 | 负控 `AlwaysAcceptingBrokenPort` 自带**私有 blob 池** | `PayloadRef` 指进另一池 ⇒ 解码炸在 codec 里,红的是夹具不是被证断言 | 改为构造注入共享 `PayloadEncoder`,并在夹具注释写明缘由 |
| 2 | 三处守卫**无判别夹具**(`duration < 1` / `Kind != Craft` / `decoder == null`) | 会是空覆盖 | 补三条域守卫夹具 → 23 → 26 条 |
| 3 | 多行夹具三行全 `qty = 1` | 对「用单件重量而非整行重量」**零判别力** | 单立 `qty = 2` 夹具 → 26 → 27 条 |
| 4 | `expectRed` 四次漏列(M10 / M11 / M1 / M5) | 证伪轮三轮才达标 | 全部改成「按谁断言了被变异属性全量 `grep` 枚举」 |
| 5 | M6 变异体**编译不过**(`out occupancy` 未赋值 ⇒ CS0177) | 退出码 6、无 XML;若只看「没通过」会误判成「逃逸」 | 变异体补 `{ occupancy = default; return false; }`;**变异体必须先能编译** |
| 6 | 全量回归首轮用了 `-projectPath "$PWD"`,而该 shell 的 cwd 是**仓库根** | Unity 在仓库根造出 `Library/`(308M)、`Logs/`、`Temp/` | 改用**绝对路径** `.../Claude-Code-Game-Studios/unity` 重跑成功。⚠️ 仓库根那三个目录**仍在**(已被 `.gitignore:61/66/62` 覆盖,零跟踪文件污染),删除请求被拒 ⇒ **留给用户决定** |
| **7** | **把两个「只在合法流上等价」的守卫判成「等价变换、不可证伪」**(前缀 `StartTick` vs `e.Tick`;`outputs == null` 短路) | 这两个判定**掩盖了两个真缺陷**:① 前缀用载荷 tick ⇒ 非法流下**过去时点被回溯污染**(W1);② `outputs == null` 放行 ⇒ **AC-18-12 唯一的时点拒绝门被绕过**(B1,BLOCKING)。它们被写进「不可证伪」表,等于**用一句自述关掉了两个该立的变异** | 改生产件 + 立 **M13 / M14** 钉住;story 卡该节已整节重写并保留原文作闭环 |
| **8** | 评审前**未跑过 `/code-review`**(卡面 `Code Review: Pending`) | 上述 B1 是 BLOCKING 级,却一路绿到「Complete」—— 因为**没有任何独立读者**看过这两个文件。**测试全绿 ≠ 无 BLOCKING 缺陷**:27 条夹具全打在「合法输入的正确性」上,没有一条问「非法输入会不会 fail-open」 | 补跑评审(`review-s20-9-2026-10-26.md`);**流程教训:Logic 级 story 不得以「测试全绿」替代评审轮** |

> 第 6 条是本件**唯一一次**触碰仓库根。已核 `git check-ignore -v` 三个目录全部命中,
> `git status` 无新增跟踪改动。
>
> **第 7 条与第 8 条同源**:第 7 条之所以能存活,正因为第 8 条(没人独立评审)。
> 两者合起来是本件最值得带走的一条 —— **「我自己写的测试」无法证伪「我自己写的守卫」**。

> 第 6 条是本件**唯一一次**触碰仓库根。已核 `git check-ignore -v` 三个目录全部命中,
> `git status` 无新增跟踪改动。
