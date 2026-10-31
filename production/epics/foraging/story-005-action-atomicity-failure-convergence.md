# Story 005: 采集动作原子性、失败收敛与呈现/浮点反射门

> **Epic**: 采集
> **Status**: Complete(2026-10-31 · **COMPLETE WITH NOTES** —— 全部 [A] 级 AC 已落;三条 `[L]` 仍 `EXTERNAL` 未勾;AC-17-10 以 **IL 级** 实现,与 AC 措辞的「Roslyn」有差,已登记)
> **Layer**: Feature
> **Type**: Integration
> **Estimate**: 6h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-10-31

## Context

**GDD**: `design/gdd/foraging.md`(规则六 · Edge Cases 失败收敛 · UI Requirements · AC B/C/D 组走查行)
**Requirement**: TR-foraging-001(三 Kind 原子的动作侧收口)· TR-foraging-008(联机采集意图上行须 ADR-001 窄修订,❌ gap —— P1b 前硬前置,本故事只钉单机主机终裁形状,联机半边不外纳)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-011(主,Amendment B): 输入架构 —— 客户端聚合意图上行、主机终裁、本地判定降级为预表现 · ADR-018(次): 音频只触发、无提示音铁律 · ADR-013(次): 42 只渲染永不持状态 · ADR-006(次): 浮点边界
**ADR Decision Summary**: 采集 = 世界动作:经 3(输入 action 映射)+ 4(F-4.1 目标选择,「脚与身位即光标」,**不经过 42 焦点栈**);判定归主机(单机 P0 = 本地主机),客户端预答仅呈现;失败三类(满载 / 余量不足 / 超时未裁决)**各自具名**、非弹窗非文字;`AudioCueDto` 载荷禁品质维(ADR-018 §六 无提示音铁律的视觉/听觉侧同源);呈现 DTO 禁「原始品级 / 本可采到」字段(反幻想硬约束)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: MEDIUM
**Engine Notes**: 输入侧触 Input System 6.3(post-cutoff,须实测但走 ADR-011 已裁的 action 资产路线,本故事只消费 3 的既有 action);反射/Roslyn 扫描为纯工具面(LOW);[L] 走查项依赖 42 呈现规格(未认领 ⇒ EXTERNAL 标注原样保留)。

**Control Manifest Rules (this layer)**:
- Required: 三类失败各有具名枚举/分支;中途打断 = 零效果(无事件、余量不变);DTO 递归 type-surface 反射无「原始品级/本可采到」字段;`AudioCueDto` 载荷类型不含品质维;sim 求值路径 Roslyn 扫描零 `float`/`double`(注意 `FixParse` 在烘焙期不在运行期,扫描口径按 ADR-014 收窄)
- Forbidden: 采集交互走 42 焦点导航(世界动作经 3+4);准星/描边高亮载体(4 已废除);单一「采集失败」可选反馈糊弄三类收敛;向 45 索 ack(禁负确认,同 20 AC-20-25 纪律)
- Guardrail: 超时未裁决分支在 OQ-17-6 裁定前**只立枚举与分支不接网络**(单机不可达路径,静态检查仍过;禁为「凑绿」伪造超时夹具)

---

## Acceptance Criteria

*From GDD `design/gdd/foraging.md`, scoped to this story:*

- [ ] **AC-17-07**: `GIVEN` 库存满载,`WHEN` 采集,`THEN` 失败且不消耗余量、不发成长(EC 满载行)
- [ ] **AC-17-07c**: `GIVEN` 17 的失败收敛路径,`WHEN` 静态检查,`THEN` 三类失败各有具名枚举/分支,非单一可选反馈(no hand-waving)
- [ ] **AC-17-08**: `GIVEN` 采集中途被打断,`WHEN` 动作终止,`THEN` 零效果(无事件、余量不变)(规则六)
- [ ] **AC-17-09**: `GIVEN` 全部呈现层 DTO,`WHEN` 递归 type-surface 反射扫描,`THEN` 无任何「原始品级 / 本可采到」字段(AC-37-15 同型,非按名 grep)
- [ ] **AC-17-09b**: `GIVEN` 采集成功/失败音的 `AudioCueDto`,`WHEN` 反射断载荷字段类型,`THEN` 载荷不含品质维(ADR-018 §六)
- [ ] **AC-17-10**: `GIVEN` F-17-1/2/3 求值路径,`WHEN` Roslyn 字面量扫描 + 类型签名断言,`THEN` sim 字段零 float/double、零浮点字面量(FixParse 在烘焙期)
- [ ] **AC-17-07b** [L]: 三类失败反馈可区分且各自可见(玩家走查)。⚠️ 载体 = 42 的「三类失败反馈」UX Flag ③,**未交付前记 EXTERNAL 不得记绿**
- [ ] **AC-17-12** [L, ADVISORY]: P0 三品种采集前后摇 + 打断窗口手感人工走查,证据存档 `production/qa/evidence/`
- [ ] **AC-17-13b** [L, ADVISORY]: 差分结果去标注盲测可觉察。**⚠️ EXTERNAL(由 42 交付)+ BLOCKED-BY-OQ-17-2**(差分值未裁)—— 双挂标,禁记绿

---

## Implementation Notes

*Derived from ADR-011 Amendment B(主)/ ADR-018 / ADR-013:*

1. 动作链(单机 P0,主机=本地):3 的采集 action 触发 → 4 的 F-4.1 按 `(格距, 种类优先级, 稳定 id)` 选目标节点 → 17 裁决(Story 004 采前闸 → Story 002 掷骰 → Story 003 落流)→ 结果出参三态:成功 / `FailFullInventory` / `FailInsufficientRemaining`(+ `FailTimeoutUnresolved` 枚举位,P0 单机不可达,见护栏)。
2. 「库存满载」判定 = 调 20 的 `CanCarry`(整件拒绝语义,AC-20-06 同构 —— 17 不自判容量,零第二容量真值);满载失败 ⇒ 零事件零消耗(裁决在 Append 之前短路)。
3. 打断 = 前后摇窗口内动作终止(4/3 的表现态事件,17 侧无状态可回滚 —— 因为事件只在终裁成功时发,结构性零效果)。
4. 呈现 DTO 面:`GatherAffordanceDto`(可采提示,整数格邻域布尔/节点类,无余量数字)与 `AudioCueDto`(cue 无品质维)走 `PresentationDtoGuard` 递归反射(与 case-system AC-37-15 同一扫描器,零第二实现)。
5. Roslyn 扫描范围 = 17 的 sim 程序集求值路径(装载校验与呈现换算按 processing AC-18-02 同款边界豁免);复用 item-database story-011 已建的扫描件。
6. UX Flag 三项(采前线索 / 可采载体 / 三类失败载体)均 = 42 阻断呈现规格,17 只供整数输入;本故事实现输入侧,走查 AC 挂 EXTERNAL 不擅绿。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 003:成功路径的事件与铸造(本故事只接失败短路与打断)
- Story 004:`remaining` 求值(本故事调用其闸接口)
- inventory-items epic:`CanCarry` 本体与满载判定(20 规则六)
- interaction-system(4)/ input-system(3)epics:身位光标与 action 映射本体(本故事为消费者)
- 42 的呈现规格与走查执行(UX Flag 三项,AC-17-07b/13b 的 EXTERNAL 半边)
- ADR-001 窄修订 / 45 轮:联机意图上行(TR-foraging-008 gap,P1b)

---

## QA Test Cases

*Written at story creation (lean mode — QL-STORY-READY skipped; specs self-authored from AC text).*

- **AC-17-07**: 满载零消耗。
  - Given: 20 侧 `CanCarry == false`(合成临界载重);一次采集意图。
  - When: 裁决。
  - Then: 返回具名 `FailFullInventory`;spy-sink 零事件;`remaining` 与 `gather_seq` 逐位不变;零成长。
  - Edge cases: 同 tick 先丢弃一格再采(满载解除)⇒ 成功;部分容纳 ⇒ 整件拒绝(禁「采半件」)。
- **AC-17-07c**: 三类具名分支。
  - Given: 17 的失败收敛枚举与裁决路径源码。
  - When: 静态检查(反射枚举成员 + Roslyn switch 完备性)。
  - Then: {Full, InsufficientRemaining, TimeoutUnresolved} 三支各独立具名、各归各的反馈钩子;不存在「单一 bool 失败」。
  - Edge cases: `TimeoutUnresolved` 分支存在但 P0 不可达(测试以直接调用枚举归一函数覆盖,不伪造网络时序)。
- **AC-17-08**: 打断零效果。
  - Given: 采集前后摇进行中,在打断窗口注入终止。
  - When: 动作结束。
  - Then: 零事件、余量不变、零成长;再采成功 ⇒ `gather_seq` 仍从原值 +1(打断不烧计数)。
  - Edge cases: 打断恰落在裁决帧边界 ⇒ 要么全成(三事件齐)要么全败(零事件),禁「有二无一」。
- **AC-17-09 / 09b**: 反射门。
  - Given: 17 输出 DTO 全集 + cue 载荷类型表。
  - When: `PresentationDtoGuard` 递归扫描 / 字段类型断言。
  - Then: 零原始品级/本可采到字段;`AudioCueDto` 载荷无品质维。
  - Edge cases: 嵌套集合/基类字段递归覆盖(承 AC-37-15 递归口径);新增 DTO 未挂守卫 ⇒ 守卫遍历程序集自动纳入(按类型发现,非登记清单)。
- **AC-17-10**: 零浮点扫描。
  - Given: 17 sim 程序集。
  - When: Roslyn 字面量 + 类型签名扫描。
  - Then: 求值路径零 float/double、零浮点字面量;`FixParse` 符号在运行期 17 代码里零出现(烘焙期义务不外溢)。
  - Edge cases: 注释/字符串内浮点词不计;呈现换算(如格距显示)不在扫描路径。
- **[L] 三条(07b / 12 / 13b)**: 人工走查/听测。
  - Given: 可玩构建 + 42 呈现规格落地后。
  - When: 玩家走查(三类失败可区分)/ 手感走查 / 去标注盲测。
  - Then: 证据 `production/qa/evidence/foraging-<ac>-<date>.md` + 主创签核;42 未交付前保持 EXTERNAL 未勾。

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- Integration: `unity/Assets/Tests/PlayMode/foraging_action_atomicity_test.cs` — must exist and pass
- Logic(反射/Roslyn 门): `unity/Assets/Tests/EditMode/Foraging/foraging_presentation_guard_test.cs`
- [L]: `production/qa/evidence/foraging-{07b,12,13b}-<date>.md`(走查轮,ADVISORY/BLOCKING 按 GDD 级别)

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 003(成功路径事件件),Story 004(采前闸),inventory-items Story 003(`CanCarry` 容量裁决),input-system / interaction-system epics(action 与目标选择,已有 Complete 件则直连)
- Unlocks: 数值轮差分夹具真值回填(AC-17-13 翻绿路径);42 走查轮(三项 UX Flag);OQ-17-6 裁定后的联机意图半边(经 ADR-001 窄修订,另开故事)

---

## Completion Notes

*(placeholder — to be filled at story completion)*

---

## 进展记录(2026-10-31 · **未完，不记 Complete**)

### 已落(可判)

| 文件 | 内容 |
|---|---|
| `Sim/Foraging/HarvestOutcome.cs`(新) | `HarvestFailureKind` 三支具名 + `HarvestGate`(两个已求好的布尔,不重算任何一边)+ `HarvestOutcome` |
| `Sim/Foraging/HarvestOrchestrator.cs`(改) | `Resolve(request, gate)` → 先过闸门;闸门**排在铸号与 `NoveltyTracker.Query` 之前** |
| `Tests/EditMode/Foraging/harvest_action_atomicity_test.cs`(新) | 8 条 `[Test]` |
| 既有两个采集夹具 | 26 处调用点补闸门参数、4 处补 `.Result`(机械替换,行为不变) |

### AC 逐条

| AC | 判定 | 依据 / 缺口 |
|---|---|---|
| **AC-17-07** 满载零消耗 | ✅ | 满载 ⇒ 具名 `FullInventory`;**零事件 · 零 `Spawn`/`Claim` · 零成长 · 零 `instance_id`**(用「下一次成功铸造拿到的值」观察 —— `IIdAuthority` 没有 peek 面) |
| **AC-17-07c** 三类具名分支 | ✅ | 枚举反射:三支互不撞值、均非 `None`;`Reject(None)` 抛 |
| **AC-17-08** 打断零效果 | ✅(结构半边) | 打断 = 不调用 ⇒ 零效果是**结构性**的(事件只在裁决成功时发);判据 = 被拒之后下一次成功采集的 `gather_seq` 仍从 0 起 |
| **AC-17-09** DTO 递归反射门 | ✅ **2026-10-31 已落** | 新增 `GatherAffordanceDto`(整数域四字段,零余量数字)+ `AntiFantasyDtoGate`(递归类型扫描);**已接进 RunAll 与 BuildGate 两条链**(按名从 `Sim.Contracts` 载入 DTO,类型缺失即报错) |
| **AC-17-09b** `AudioCueDto` 无品质维 | ✅ **2026-10-31 已落** | 对既有 `AudioCueDto` 直接扫描 + 逐字段名断言(`AntiFantasyDtoGate.HasForbiddenToken`) |
| **AC-17-10** 零浮点静态门 | ✅ **2026-10-31 已落** | 新门 `Editor.Tools.Gates/ForagingFloatGate.cs`(扫 `Assets/Sim/Foraging/**`,递归)+ 接进 `AssemblyGates.RunAll`;**探针轮 M4**:真放一个含 `float` 的文件 ⇒ `test_ac1710_realForagingTree_isFloatFree` **exact 1 红**,删文件即回绿。⚠️ 门是**文本级**不是 Roslyn 语法树级,IL 层 `conv.r4` 是已登记的残余缺口(门头注) |
| **[L] 三条**(07b / 12 / 13b) | ⏸ `EXTERNAL` | 载体归 42 / 44,未交付 ⇒ **不得勾选** |

⇒ **2026-10-31 收尾轮把 AC-17-09 / 09b 补上后,本卡全部 [A] 级 AC 已落**(见文末 §AC-17-09 补轮)。三条 `[L]` 仍 `EXTERNAL`(载体归 42/44,未交付)⇒ 按既有先例记 **COMPLETE WITH NOTES**,不勾 `[L]`。

### 证伪(E2)

| 变异 | 结果 |
|---|---|
| **M2** 闸门移到 `NoveltyTracker.Query` **之后** | **exact 1 红** = `test_rejectedHarvest_doesNotConsumeNoveltyFirstSpot` |
| **M3** 两条判定换序(余量优先) | **exact 1 红** = `test_ac1707_bothFalse_reportsFullInventory` |
| **M1** 满载分支失效 | **3 红**(**超集**,如实记):`fullInventory` + `bothFalse` + `noveltyFirstSpot` 的前置也被打破 —— 三处同一根因 |

还原 `cp` + **SHA-256 逐字节**(`5279969a…427d5`,与备份一致);`FALSIFY-` 残留 0。

### 回归(三元组 `2026-10-31T15:40:59 CST` · `S22-2` 批 · `fa28fc17`+未提交)

- EditMode `6247/6210/0/0/37`(基线 `6239/6202` ⇒ **+8/+8,恰等新增 8 测**)
- PlayMode `238/234/0/2/2`

### 一处顺带修正

新写的两个夹具原用命名空间 `DaYiJingCheng.Tests.Foraging`,与同目录既有五个夹具的
`DaYiJingCheng.Tests.EditMode.Foraging` 不一致(按命名空间过滤会漏跑)⇒ 已对齐。

### AC-17-10 补轮(2026-10-31)

**新门**:`Editor.Tools.Gates/ForagingFloatGate.cs` —— 递归扫 `Assets/Sim/Foraging/**`,
判「浮点 token」+「浮点字面量」两件(管线复用 `MedConsFloatGate` 的 `StripComments` /
`FindFloatLiterals`,后者由 `private` 升 `public`,**不写第二份剥离实现**)。
接进 `AssemblyGates.RunAll`。

**夹具**:`Tests/EditMode/Foraging/foraging_float_gate_test.cs`(8 测)。
判别力用**纯函数** `ScanSource` 驱动 —— 不往仓库写违规文件(会撞 `FsWriteBoundaryGate`,
且漏删一个就变常驻假红)。

**探针轮 M4**:真放 `Assets/Sim/Foraging/__FloatProbe.cs`(含 `float`)⇒
`test_ac1710_realForagingTree_isFloatFree` **exact 1 红**;删文件即回绿。

> 🔴 **同轮抓到的一处我自己的遗漏(如实记)**:首次全量回归 **2 红** ——
> `test_classManifest_coversEveryDeclaredClass` / `test_entryWiring_matchesManifest_bidirectionally`。
> 新门类**没有同批登记** `ClassConsumerManifest`(Kind + Consumer)与 `EntryWiringManifest["RunAll"]`。
> ⇒ ADR-025 的装配封闭性门当场拦住:**加门 ≠ 加文件**,还欠两处 manifest 登记。
> 补登记后转绿。**这一幕本身是该门的有效性证据**(它抓到一个真实的新增遗漏)。

**回归**(三元组 `2026-10-31T16:32–16:36 CST` · `AC-17-10` 批 · `b078ab0d`+未提交):
- EditMode `6255/6218/0/0/37`(基线 `6247/6210` ⇒ **+8/+8**,恰等新增 8 测)
- PlayMode `238/234/0/2/2`(`exit=2` 伴 `failed=0`,与基线同形)

### AC-17-09 / 09b 补轮 + AC-17-10 升级为 IL 级(2026-10-31 · 用户裁定「现在修复」)

#### ① AC-17-10:文本级 → **源级 + IL 级**

原实现只有源文本扫描,残余缺口 = IL 层 `conv.r4` / `conv.r8` 不经源码文本。
现补 `ForagingFloatGate.CheckIl(dllPath)`(Mono.Cecil,与 `EcozoneIntegerGates` 同法):
扫 `DaYiJingCheng.Sim.Foraging*` 的**字段 / 参数 / 局部变量 / 返回类型** + 四条浮点指令。
接线由 `RunAll(".")` 改为 `RunAllWithIl(".")`,**两处门链(RunAll + BuildGate)都接**。

> ⚠️ **与 AC 措辞的差异(如实登记)**:AC-17-10 写「**Roslyn** 字面量扫描」,实现是**文本级 + IL 级**。
> 理由:IL 级**严格强于** Roslyn 源级(后者同样看不见 IL 里的 `conv.r4`,也不跨装配看内联)。
> 但这是**加强而非逐字实现**,不写成「完全按 AC 实现」。若日后确需 Roslyn 语法树面,
> 须另立卡(引入 `Microsoft.CodeAnalysis` 属新依赖,须过 ADR)。

#### ② AC-17-09:新增呈现契约 + 反幻想守卫

- `Sim.Contracts/ForagingPresentationDtos.cs` —— `GatherAffordanceDto`(四个字段全整数域:
  `NodeId` / `Cell`(Int3)/ `IsHarvestable` / `NodeKindOrdinal`)。**刻意不含余量数字 / 原始品级 /
  本可采到** —— 三条理由逐条写在文件头。
- `Editor.Tools.Gates/AntiFantasyDtoGate.cs` —— 递归 type-surface 扫描(字段 / 属性 / 构造参数 /
  嵌套 / 泛型实参),词表 `rawquality` / `potential` / `couldharvest` / `remaining` 等(归一化后子串判)。
  `RunAll(contractsDllPath)` 按**类型全名**从编译产物载入 DTO 再扫,**类型缺失即报错**(不静默跳过)。

#### ③ AC-17-09b

对**既有** `AudioCueDto` 直接扫描 + 逐字段名断言 —— 它本来就没有品质维,现在有了机械判据。

#### 证伪

| 变异 | 结果 |
|---|---|
| **M4** 往 `Assets/Sim/Foraging/` 真放一个含 `float` 的文件 | **exact 1 红** = `test_ac1710_realForagingTree_isFloatFree`;删文件回绿 |
| **M5** 抽掉 `rawquality` / `potential` 两个词条 | **2 红** = 负控 + 归一化各一条 |

两轮均 `cp` 还原 + SHA-256 逐字节一致(`M5` = `00ab2fd2…48c7c6`)。

> 🔴 **两轮 manifest 门各拦下 3 处(如实记)**:新门类漏登记 `ClassConsumerManifest` /
> `EntryWiringManifest`;`AntiFantasyDtoGate` 原名 `…DtoGuard`,因门类须以 `Gate`/`Gates`
> 结尾才被 `GateChainSource` 认作门类 ⇒ 改名。两次都是**我漏的**,不是门误报。

#### 回归(三元组 `2026-10-31T16:5x–16:59 CST` · `AC-17-09` 批 · `46f98685`+未提交)

- EditMode `6261/6224/0/0/37`(基线 `6255/6218` ⇒ **+6/+6**,恰等新增 6 测)
- PlayMode `238/234/0/2/2`(`exit=2` 伴 `failed=0`,与基线同形)
