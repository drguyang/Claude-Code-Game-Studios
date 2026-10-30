# ADR-025: 契约程序集清单与命名(边界程序集 / 门面程序集 / 独立契约程序集收口)

## Status

Accepted

> **2026-09-20 起草;同日用户裁定转 Accepted**(逐份裁定轮 #3):
> **① 具名装配清单照准(拟定名采用,不改名)· ② QQ-03 取甲案(`Fix` 保持 public +
> `ToFloat()` 调用点白名单断言;乙案 `internal`+`InternalsVisibleTo` 否决)·
> ③ QQ-01 取 ①′(传送契约拆两半,整数半进 `Sim.Contracts`;流事件通道备选方案未取)·
> ④⑤⑥ 随全件照准。**
> ~~起草时注:①③ 须用户照准~~(已履行)。本件**无引擎实测前置**(纯引用集裁决,
> asmdef 机制自 2019 稳定)—— 转 Accepted 不欠 spike;V-0 复用 ADR-017 §二 已登记义务的结果。
> **残留义务归回写轮(不撤销 Accepted)**:V-6 的 ADR-017 `"references": []` 订正挂讫 ·
> V-5 两称谓作废四处加注 · Migration 步骤 2–3。
> **✅ 2026-09-23 回写轮:三项全部结** —— V-6(ADR-017 §二 `"references": []` 歧义订正注
> 已挂讫,见 §Validation 该行)、V-5(两称谓命中处已全数加注,`tests/README.md`
> 第 5 处由 2026-09-21 review S-4 处理)、Migration 步骤 2–3(asmdef 装配 / 种子测试
> 落点)已由 U0 批实物落地。本行就本件回结。
> **TD 条件 C2 就此两侧全结(#1 = ADR-023 附条件 · #2 = 本件无条件)。**

## Date

2026-09-20

## Last Verified

2026-09-20(§2.0 七行骨架逐行对源:`adr-017:251` `Sim.asmdef` 原文 · `adr-005:228` `ToFloat` 注释 ·
`audio-system.md:242` · `persistence-service.md:577` · `player-controller-and-movement.md:124` ·
`death-and-respawn.md:402` 跨门点 grep 复算;`adr-023` / `adr-024` 已同日落盘,引用其接口形状)

## Decision Makers

dr_guyang(用户 · **2026-09-20 全件照准,转 Accepted**)· technical-director(起草)·
门 A 两侧全体(本件是它们的装配图)· 7a 持久化(codec 程序集)· 44(`IAudioCueSink` 契约程序集名义方)·
29 死亡与复活(**全案唯一跨门调用点的出处** `death-and-respawn.md:402`)· unity-specialist(lean 复核先例:ADR-023)

## Summary

§2.0 证明:**全案唯一有名字的 asmdef 是 `Sim`**(ADR-017 §二 只硬化了门 A 内侧,因为它要挡 DOTS),
门外的一切靠散文 —— 「边界程序集」「门面程序集」「独立契约程序集」是**三个从未对齐的名字**,
可能指一个东西也可能指三个。本 ADR 裁决:**具名七装配清单**(`Sim` / `Sim.Contracts` / `Sim.Codec` /
`Gameplay.Presentation` / `Gameplay.Input` / `Gameplay.UI` / `Editor.*` 工具族;**2026-09-25 story-001
复核 B1 拆装增 `Gameplay.Input`,六 → 七**)、**「门面」二义就地拆除**(QQ-03:
采白名单断言案(甲)落定)、
**`IPlayerMotor`/`ICameraRig` 落位**(QQ-01:三选一,已收窄为「是否给门 A 加一支整数传送命令抽象」)、
**`Fix` 禁入 `Unity` 序列化器**的守卫(承 ADR-006 D-21-18)由 asmdef 划分物化为可编译事实。

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Knowledge Risk** | **LOW** —— asmdef 机制(`noEngineReferences` / `autoReferenced` / `defineConstraints`)自 2019 稳定;本件不触任何 post-cutoff API。⚠️ 与 ADR-005/017 同款:**风险在行为逐位面,不在此件的裁决面** |
| **References Consulted** | `VERSION.md`(无本域条目 —— 恰是 LOW 的证据)· `adr-017 §二`(Sim.asmdef 先例文本) |
| **Post-Cutoff APIs Used** | 无 |
| **Verification Required** | **V-0(单测位)**:asmdef 的 `noEngineReferences` 在 6.3 仍使 `UnityEngine.*` 引用成为构建失败 —— 该断言 ADR-017 §二 已登记为义务,本件复用其结果,不另开 spike |
| **Deprecated API Check** | 通过 |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | **ADR-005**(Accepted —— 八个 P0 抽象点与边界程序集内容;计数经 ADR-029 追加第七个、ADR-031 追加第八个)· **ADR-006**(Accepted —— `Fix` 序列化禁令 D-21-18 / 唯一舍入)· **ADR-017**(Accepted —— `Sim.asmdef` 与门 A 白名单断言,本件把断言的**作用域事实**外推到全清单)· **ADR-010**(Accepted —— codec 义务清单)· **ADR-020**(Accepted —— `IPlayerMotor`/`ICameraRig` 契约)· **ADR-022**(Accepted —— Tooling 层「不进构建」先例)· **ADR-024**(Accepted 同批 —— `StreamRouting.g.cs` 的归属程序集在本件清单内落位) |
| **Blocks** | 一切 C# 实现故事(**C2 的另一半**);`tests/` 种子测试的编译(其 asmdef 落点 = 本件);ADR-023 的 `ISceneRouter`/`IWorldSpawner` 归属 |
| **Supersedes** | **散文三名** —— 「门面程序集」(ADR-005:228)与「独立契约程序集」(44 GDD:242)作为**称谓作废**,其内容并入具名清单项 |
| **Related** | QQ-01 / QQ-03(本件主裁)· QQ-02(**本件不裁** —— 逐字回填 ADR-005 的独立义务,见 §Decision ⑥)|

## Context

### Problem Statement

故事拆分与 asmdef 落地都要求「每个契约住哪个程序集」有唯一答案。当前答案不存在:
`architecture.md` §2.0 七行里 **6 行标 ⚠️ 名未定**;其中三行(L3 的「边界程序集 /
门面程序集 / 独立契约程序集」)互相不知道是不是同一个东西。
**后果不是文档不齐,是约束落不了地**:「`Fix` 不可经 Unity 内置序列化器承载」(ADR-006 D-21-18)
需要一个**可引用 `internal` 编码器 + `InternalsVisibleTo` 边界**的程序集对像才能写守卫测试;
「仅门面程序集可调用 `ToFloat()`」(ADR-005:228)在「门面 = Sim 自身」读法下**恒假**
(public 类型无外部约束)。`tests/unit/sim/sim_fixedpoint_test.cs` 已因此**不被编译**。

### Current State(§2.0 七行骨架的实测注)

- `Sim` —— 唯一具名;`adr-017:251` 的 asmdef `"references": []` ⇒ **`Sim` 必须能看见
  `SimEvent`/抽象点**,故契约程序集**必然在 `Sim` 的引用集内** —— 这是 QQ-03 两种读法的来源。
- 唯一跨门调用点(2026-09-20 grep 全库复算,QQ-01 已收窄):
  **`death-and-respawn.md:402`**「触发 1 的传送 住边界程序集(`IPlayerMotor`)… 跨门契约类型」——
  29 的结算半在门 A 内,它需要**发出一次传送**。除此处外,全库无第二例
  (原「与 ADR-017 §二 白名单冲突」的说法不成立 —— 该白名单作用域只有 `Sim`,两接口住 L4)。
- `Presentation` / `GameTool` 从未具名;各 GDD 只写「呈现侧」。

### Constraints

- 门 A 语义不变(ADR-017 §一:sim 侧结构性排除,引用集恰 = BCL ∪ 契约程序集)。
- ADR-006 D-21-18:`Fix` 的自定义编码器守卫必须是**编译期/测试期可执行**的,不是 grep。
- ADR-023 已裁 `StreamRouting.g.cs` 住门 A 程序集内(纯 BCL)⇒ 其枚举本体住契约程序集。
- ADR-024 已裁「真源 = registry,生成器住编辑期」⇒ `tools/kindgen/` 落 Tooling 族,不进构建。
- 测试可编译性:`tests/EditMode`(门 A 纯逻辑)+ `tests/PlayMode`(需 player)。

### Requirements

- TD 条件 **C2** 的另一半(#1 = ADR-023 已起草)。
- QQ-01(`IPlayerMotor` 的 `Vector3` 张力)· QQ-03(`ToFloat` 归属)· `TR-skill-002`
  (定点纪律的程序集落点,与 Required ADR **#4** 共享;本件只给容器,不裁 #4 的内容)。
- `OQ-A2`(无障碍设置「不进流」断言的守卫落点 —— `accessibility-requirements.md` 预挂本件)。

## Decision

### ① 具名装配清单(**八装配 = 七具名 + `Editor.Tools` 工具族**;2026-09-20 裁定:清单与命名全照准,不改名;**2026-09-25 story-001 复核 B1 增 `Gameplay.Input`**,六 → 七;**2026-10-09 M2 接线轮阶段 1 · 组合根案 A 增 `Gameplay.Boot`**,七 → 八)

| asmdef | 层 | 引用集 | `noEngineReferences` | 成员(§2.0/§2.2 的收口) |
|---|---|---|---|---|
| **`Sim`** | L2 | **期望引用集 = BCL + `Sim.Contracts`(仅此一件)** | **true**(ADR-017 原文) | 19 个 sim 模块(§2.1 表)+ `StreamRouting.g.cs`(ADR-024)+ **25 与 9 同程序集**(2026-09-20 已裁,§2.1 行内)|
| **`Sim.Contracts`** | L3 | **期望引用集 = BCL** | **true** | `WorldPos` · **九抽象点**(含 ADR-029 的 `IPayloadEncoder` · ADR-031 的 `IPayloadDecoder` · **S18-2 的 `IFoldedPredicate`**)· `SimEvent`/`PatientId`/`StreamId`/`EventKind` · `VitalsDto` · `Fix` + `FixParse` · `IDataProvider` · `AudioCueDto`+`IAudioCueSink`(44 名义并入)· `IPositionalChannel` · `IPatientVisualAnchorSource`(病人视觉锚点,2026-10-30 增 · `TD-prescription-06` 兑换轮) · `ClinicEnvDto`(24 → 42 只读源,2026-09-22 OQ-CP-4 裁定)  🔁 **2026-10-30 窄修订(`TD-clinic-20` 结案 · 用户裁定丙案):`IClinicEnvQuery` 已删** —— 它**零实装、零调用点**(`grep -rn ': IClinicEnvQuery' unity/Assets/ --include=*.cs` ⇒ 0),且签名 `GetEnv(int roomId)` **装不下**真实的房间标识:后者是 `Sim.Clinic.ClinicRoomId`,**64 位**(`High`/`Low` 两个 `int`,由布局规范排序格集哈希派生)⇒ 任何 `int` 映射必然碰撞。而 `ClinicRoomId` 住 `Sim`,本程序集引用集**恰 = BCL** ⇒ 契约面**物理上引不到**它,改签名 = 击穿本条已裁的程序集边。**真实通道 = `ClinicEnvironmentMemo.GetDto()`**(42 侧唯一读面,缓存 `ClinicEnvDto`,由 `ClinicEnvProjection.ToDto` 单点投影,`AC-24-09` 的同步可读由它兑现)。⚠️ 若将来要恢复接口形态,须先裁 `ClinicRoomId` 的程序集归属(甲:搬进 `Sim.Contracts` / 乙:契约改 `GetEnv(High, Low)`),不得静默加回 —— 守卫见 `Tests/EditMode/ClinicMachine/clinic_env_delivery_test.cs` 的「不得复活」断言` 🔁 **2026-10-21 待增员两件(S18 实施期义务,原文不删)**:① **`ActorClass` 枚举**—— S18-1 `IsCombatant(entity)` 的形参类型;现状全库**无此类型**,`ActorClass`(`CombatActionSchema.cs:25`)/`EntityKind`(`EnemyBehaviorProgram.cs:26`)两者均为 `string` 且只校验非空,`PlayerId` 类型亦不存在(`PayloadCommon.cs:13-14` 自陈)⇒ 用户 2026-10-21 裁定取 **A 案:新建枚举**。🔁 **2026-10-22 订正(实施期实测)**:该案原文写 `{Player=0, HumanEnemy=1, Beast=2}`,**实际落地 = `{Unspecified=0, Player=1, HumanEnemy=2, Beast=3}`** —— 原文把 `Player` 定为 0,而 `Player` 的 combatant = **false** ⇒ 漏填会**静默放行致死**(最危险方向)。落地时改为 0 留 `Unspecified` 哨兵,谓词对它 **fail-loud**。原文不删,留作闭环记录。② **`IFoldedPredicate`**(`bool Folded(int patientId)`)—— S18-2;✅ **2026-10-22 已落**:`Sim.Contracts/IFoldedPredicate.cs`(纯接口,签名逐字 = `bool Folded(int patientId)`)+ `Sim/FoldedPredicate.cs`(实现)+ `Sim/HistoryFolder.cs` + `Sim/FoldRow.cs`(折叠动作与折叠行,**不进本表** —— 它们是 `Sim` 内部类型,不改 `Sim.Contracts` 的成员面);`Sim.Codec/SaveService.cs` 加**可选**构造函数形参,`ISaveService` 方法集未动。用户 2026-10-21 裁定取 **甲案:抽象点住 `Sim.Contracts`、实现 `FoldedPredicate` 住 `Sim`**(与 ADR-029 `IPayloadEncoder`/`PayloadEncoder` 同款分居),因三条件数据(`RegistrySchema` 的 `Plateau`/`SelfLimit`/`Relapse`/`Lethality` + `CaseStreamQuery.HasOpenCase`)全部住 `Sim`,而 `Sim` 与 `Sim.Codec` 是**兄弟装配、互不可见**(ADR-025 §① 本行 + `Sim.Codec` 行)⇒ `SaveService` 须以**构造函数注入**接收它,`Checkpoint(SaveSlot)` 签名**不动**(ADR-010 `:331` 反射断言要求公开方法参数集恰等于 `{SaveSlot}`)。⚠️ **本表无任何机械门校验**(`AssemblyGates.cs:417-425` 的 `AudioForbiddenContractTypes` 只校验**引用集**,不校验成员表)⇒ 上述两件落地时**必须**同批回写本单元格,否则本表即失准。 ✅ **2026-10-21 ① 已落**:`Sim.Contracts/ActorClass.cs` 落盘(⚠️ **零值 = `Unspecified = 0`,非 `Player = 0`** —— ADR-025 该注原文写的 `{Player=0, …}` 与实施相反,见下方订正注)。 🔁 **2026-10-30 增员一件(`TD-prescription-06` 兑换轮,原文不删)**:**`IPatientVisualAnchorSource`**(`bool TryGetAnchorCell(int patientId, out WorldPos cell)`)—— 病人视觉「该站在哪一格」的接缝。**住本程序集的理由**:签名只用 BCL 类型(`int` / `WorldPos`),而**真实实现方**(6 世界生态区 + 13 病人 AI 空间侧)住 sim 侧 / 边界层 —— 它们够得着 `Sim.Contracts` 但**够不着** `Gameplay.Presentation`;若契约住表现层,真位置实现就得由表现层反向依赖 sim 空间数据 = 破 ADR-016 §一。⚠️ 与其**分居**的视觉接缝 `IPatientVisualFactory` **刻意住表现层**(`Gameplay.Presentation/Patients/`)—— 它的签名必须出现 `GameObject`,物理上进不了本程序集(引用集恰 = BCL)。两处分居承 ADR-029 / ADR-031 的同款模式。⚠️ 返回**整数格**不返回 `Vector3`(承 ADR-015 §三:空间真值一律整数格,格 → 连续坐标换算归表现层)。
🔁 **2026-10-22 再增员两件(ADR-032 批,原文不删)**:③ **`PatientOutcome` 枚举**(`{Unspecified=0, Recovered=1, Died=2}`)—— 病程终态,0 为漏填哨兵;④ **`PatientOutcomeChangedPayload`**(`{PatientId, DiseaseId, Outcome, Seq}`,全整数域)—— `EventKind.PatientOutcomeChanged` 的载荷。✅ **2026-10-22 已落**(QA 条件 C-8 复核:`grep` 三项成员 `ActorClass` / `IFoldedPredicate` / `PatientOutcome` 在本行各命中 ≥2 次;本条此前只写「同批落 `Sim.Contracts`」而未带「已落」字样,措辞已订正,落地事实不变)。二者同批落 `Sim.Contracts`(枚举与载荷均在 `Payloads/HistoryPayloads.cs` / 同文件),`Sim` 与 `Sim.Codec` 均按既有引用集够得着,**零新增程序集边**。⚠️ 同款义务:本表无机械门校验 ⇒ 已在此就地回写。
| **`Sim.Codec`** | L3 | **BCL + `Sim.Contracts`** | **true** | 7a 三流 codec + 存档头 + `Fix` 自定义编码器(**`internal` + `InternalsVisibleTo("Sim.Contracts.Tests")`** —— D-21-18 守卫的可执行形态)· **2026-09-30 修订**:引用集由「BCL only」改为「BCL + `Sim.Contracts`」—— `Sim.Codec` 需要引用 `SimEvent`/`Fix` 等类型,原定义导致编译失败;`noEngineReferences` 保持 `true`(`Sim.Contracts` 不是引擎程序集)|
| **`Telemetry`** | L4(边界层) | `Sim.Contracts`(读事件流的类型) | **true** | **51 遥测(2026-10-19 TD-epic-01 还债件)** —— ADR-019 §三「51 住边界层、不进 sim」的**编译期**表达:`noEngineReferences: true`(与 `Sim.Contracts` 同款)⇒ 结构上不可能碰 `UnityEngine` / 网络 / 文件写。成员:`JudgmentMetrics`(F1 指标纯函数重算面,AC-51-B1…B4)+ `JudgmentRecord`(输入形状)。⚠️ **只读消费者**(ADR-019 §三 逐字:51 不是第四个 `IEventSink` 写入者)⇒ 本装配**零写面**。⚠️ 建立缘由 = 普查(`review-cross-epic-fake-green-2026-10-18.md`)实测 51 在 `unity/Assets/` 下**零生产文件**而 10 story 全 `Complete` ⇒ AC 语义层由**测试自持谓词面**顶替(`f1_judgment_accuracy_test.cs` 自陈「测试自持谓词面,零生产依赖」)。谓词面已上移为本类,测试改打生产类。 ⚠️ **P0 剩余切面未落(如实)**:指标重算器的**事件流→`JudgmentRecord` 还原器** / 调试视图 / 本地导出 —— 三条归后续 story,本件只还「谓词面无生产主语」这一条。 |
| **`Gameplay.Presentation`** | L5(+L4) | UnityEngine · URP · `Sim.Contracts` | false | 1 · 2 · 42-UGUI 侧 · 44 · `ISceneRouter`/`IWorldSpawner`(ADR-023)· L4 边界层模块(4 / 8 / 13 / 51)| **⚠️ 2026-10-26 注记(S20-2 评审 W-1,原文不删)**:上列引用集**不含 `Sim`**,但树中存在两条**已登记在案的 WARN 基线债**:`Gameplay.Presentation.asmdef:5-6` 实含 `Sim.Contracts` + `Sim`,由 `Editor.Tools.Gates/AssemblyGates.cs:473` 的 `AudioRefBaseline = {"Sim"}` 按 WARN 放行(`:933` 自陈「摘除归装配轮,另账」)。⚠️ **2026-10-26 订正**:原注记写 `:470`,实测该行落在 `AudioRefBaseline` 的 doc 注释块内(常量本体在 `:473`)—— 与 S20-2 评审 D2「引用须指现行实体」同型,就地订正行号,原文不删。**既有模式,非本卡首创**:`Gameplay.Presentation/ClinicRoomMatchingService.cs:44-59` 早已把 `Sim.Clinic` 的 `ClinicRoomExtractionResult` / `ClinicRoomMatchDiagnostics` 直接作为表现层 struct 字段暴露;S20-2 的 `ClinicEnvironmentMemo` 沿用同一模式,缓存 `Sim.Clinic.ClinicEnvEvaluation`。**~~为何不用 `ClinicEnvDto`~~ → 🔁 2026-10-30 该段已作废(原文留档)**:原写「DTO 投影需要两处**全库未裁**的映射 ⇒ 发明映射 = 造第二真源,故未擅自发明」——**两处映射已于 2026-10-26 全部裁掉**(①`RoomNameId` ↔ `ClinicRoomId` 的 int 房号:该前提本身被证伪,见上格 `IClinicEnvQuery` 删除注;②`ContextIds` ↔ `string`:S20-2 两张词表解决)。⇒ `ClinicEnvironmentMemo` **现已缓存 `ClinicEnvDto`** 并提供 `GetDto()`,由 `ClinicEnvProjection.ToDto` 单点投影,类头注自陈「兑现 ADR-025 §① 契约通道,W-2 的真修」。`Get()`(返 `ClinicEnvEvaluation`)保留为**诊断 / 对拍**面,不是交付面。**本条注记至此结案**(`TD-clinic-20`)。 |
| **`Gameplay.Input`** | L4 | `Unity.InputSystem` only | false | 3 输入与设备(`InputService`,动作资产唯一持有者;**不引 `Sim.Contracts`** —— AC-3-A6 输入程序集不引任何声明 `IEventSink`/`SimEvent` 的程序集,2026-09-25 story-001 复核 B1 拆装)|
| **`Gameplay.UI`** | L5 | + UI Toolkit | false | 42 UI Toolkit 栈 · 39 · 7b · 48(与 Presentation 分装配 = 焦点单栈门的**编译期**表达,ADR-013)|
| **`Gameplay.Boot`** | L5(组合根) | `Sim` · `Sim.Codec` · `Sim.Contracts` · **`Gameplay.Input`** · `Gameplay.Presentation` · `Unity.InputSystem` · `Unity.Addressables` · `Unity.ResourceManager` · UnityEngine | false | **2026-10-09 增补(M2 接线轮阶段 1 · 组合根案 A)** —— 依据 = b5 门把 `Sim.Codec` **装配级标红**,而 `PayloadEncoder` 实现体住 `Sim.Codec`、`Gameplay.Presentation` 不可引用它 ⇒ 编码器构造点须独立装配。承载 `BootRoot`(Boot.unity 启动序宿主)· `CompositionRoot` / `CompositionRootServices`(生产装配)· `SimTickDriver`(`ITickProvider` 实装) · `MovementFeed` / `MovementInputReader`(阶段 1 尾:输入→采样→tick 边沿帧泵;`Unity.InputSystem` 为阶段 1 尾增补,直读 `Keyboard.current`/`Gamepad.leftStick` 兜底 —— action 装载器缺失登记为未来故事)。**2026-10-14 story-009 窄修订:增 `Gameplay.Input`**(引用集 8 → 9)—— 依据 = `EmergencyChannelDriver` 须消费 3 的 `EmergencyDirectReadChannel` / `DirectChannelState` / `AggregatedEmergency` 交出物(story-009 R-9.2 驱动权:Boot 装配层是「3 的通道态 → 10 的判域」的**唯一**映射点,AC-3-A6 禁 Gameplay.Input 反向引 Boot ⇒ 只能 Boot→Input 加边;同 2026-10-09 增员先例)。**无生产装配引用 `Gameplay.Boot`**(组合根为装配叶;仅 `Sim.Contracts.Tests` 即 EditMode 测试装配引用;Boot.unity 仅以脚本 guid 挂 `BootRoot`)|
| **`Editor.Tools`** 族 | L6 | UnityEditor 自由 | n/a | `tools/level/`(ADR-022)+ `tools/kindgen/`(ADR-024)+ **`Editor.Tools.Gates`(U0-b 四门 b2–b5)· `Editor.Tools.Spike`(U1 spike 批)· `Editor.Tools.Bake`(Story 008 数据管线)**(2026-09-26 audio Story 001 代码审查 Required-5 回写 —— 三支此前仅在 `AssemblyGates` 清单内、本表零登记,与 §④「新 asmdef 须追加进本表」冲突,今补平;**全部不进构建**,asmdef 限 `includePlatforms: ["Editor"]`)。**2026-10-16 S14-6 窄修订:`Editor.Tools.Gates` 引用集 +`Unity.Addressables` / `Unity.Addressables.Editor`** —— 依据 = AC-44-D5 逐字要求「组区分判据读 `AddressableAssetSettings`」,而该 API 住 `Unity.Addressables` ⇒ 执行体 `AudioAssetGroupGates` 物理上够不着,不加边则 AC 只能由测试自造替身顶替(S14-6 的病根)。**不进构建** ⇒ 门 A / b2 / b5 三条约束均不受影响(与 `Editor.Tools.Bake` 引 `Unity.Addressables` 同款先例)。**2026-10-17 story-017 窄修订:`Editor.Tools.Spike` 引用集 +`Editor.Tools.Bake`** —— 依据 = `TD-audio-01f` 根因①(「Play Mode Script 漂移」):`U1SpikeSetup.ConfigurePlayMode` 把机器切到 Existing Build,`Teardown` 须复原,而「哪个 builder 是 Use Asset Database」的判据**已有实现与夹具**(`DataBakeMenu.FindAssetDatabaseBuilderIndex`,反证 F3)⇒ 由 `Spike` 委派而非另抄一份(「单一出处」)。装配引用为 `Spike → Bake` **单向**;两支同为 `includePlatforms: ["Editor"]`、**均不进构建** ⇒ 门 A / b2 / b5 不受影响 |
| **`SkeuomorphicUI.Tests`** | L6(测试) | `Gameplay.Presentation` · `Gameplay.UI` · `Unity.InputSystem` | n/a | Story 003 焦点导航边界单元测试(`focus_boundary_test.cs`, 8 测;**不进构建**,asmdef 限 `includePlatforms: ["Editor"]`) |
| **`Casebook.Tests`** | L6(测试) | `Sim.Contracts` · `Gameplay.UI` | n/a | **2026-10-21 增员(S17-4 / casebook-002)** —— 病例列表全序纯函数测试(`casebook_sort_test.cs`,16 测)。依据 = 被测生产件 `CaseListOrdering` 住 `Gameplay.UI`,而共享的 `Sim.Contracts.Tests`(`Tests/EditMode/EditMode.asmdef`)**不引用 `Gameplay.UI`** ⇒ 测试目录无法复用该装配,只能自带 asmdef(`Tests/EditMode/Casebook/Casebook.Tests.asmdef`,继承 `SkeuomorphicUI.Tests` 先例)。**不进构建**,asmdef 限 `includePlatforms: ["Editor"]`。⚠️ 首跑漏登记被 b3 门即时捕获(「未登记 asmdef「Casebook.Tests」」),**该门正是本 ADR §④ 的执行体,工作正常**;本行即同批回写。🔁 **2026-10-21 收窄**:初版引用集列 `Sim` 与 `Gameplay.Presentation`,实测**零使用**(asmdef 引用不传递,测试只命名 `PatientId` 等 `Sim.Contracts` 类型)⇒ 已按最小引用集收敛为 \`Sim.Contracts\` · \`Gameplay.UI\`,并补 \`autoReferenced: false\`。 |
| **`Editor.Tools.Font`** | L6(Tooling 族) | `Unity.TextMeshPro`(ugui 2.0.0 传递依赖,precompiled DLL 按名引用) | n/a | 脉案 SDF FontAsset 生成器(`SdfFontAssetTool.cs`,sprint-08 S3「字糊」根治件)—— 菜单 `大医精诚/Font/生成脉案 SDF FontAsset`,把 `Fonts/NotoSansCJKsc-Bold.otf`(UI Toolkit 动态字体路径,糊的根因)转 TMP SDF FontAsset;**不进构建**,asmdef 限 `includePlatforms: ["Editor"]`;一次性权威动作,**不进 CI**(重跑产字节级不确定资产)。2026-10-13 补平:本行首跑时漏登记,`AssemblyGates` b3 门即时捕获(「未登记 asmdef「Editor.Tools.Font」」—— 该门正是本 ADR §④ 的执行体,工作正常) |
| **`Editor.Tools.Capture`** | L6(Tooling 族) | `Unity.InputSystem`(F12 轮询读键;本项目 `activeInputHandler: 1` = 纯新 Input System ⇒ 旧 `Input.GetKeyDown` 会抛异常) | n/a | 截图工具(`ScreenshotTool.cs`,2026-10-14 立)—— 菜单 `大医精诚/Capture/截图 {1x,2x,4x}`(快捷键 `_F12` / `%F12` / `%#F12`)+ Play 中 F12 轮询兜底,落 `Logs/shots/<时间戳>_<倍率>.png`(`Logs/` 已 gitignore ⇒ 截图永不入库)。**零第三方**(项目零第三方取向,不引截图插件)。⚠️ **只在 Play 模式有效**:项目两次独立实测 batch 无渲染帧(`ScreenCapture` 抛异常 / PNG 零字节,`Screen=640×480`)—— 见 `font_rendering_path_spike_test.cs` SPIKE-D + `atlas-budget-freeze-2026-10-14.md` §四。**不进构建**,asmdef 限 `includePlatforms: ["Editor"]` |

- **「门面程序集」「独立契约程序集」两个称谓自本件起作废**,由表内具名项承接;
  引用它们的原文(ADR-005:228 / 44 GDD:242)在回写轮加注「现名 = …」(**不追改原文**,防重写历史)。✅ **2026-09-23 回写轮已落**(见 §Validation V-5)。
- **`Sim` 的引用集白名单升格**:ADR-017 §二 的断言(构建失败级)作用域从「`Sim` 引用 ⊆ BCL」
  改为「`Sim` 引用集 **期望 = {BCL, Sim.Contracts}**」—— ⚠️ 措辞订正(2026-09-21 · 承
  `architecture-review-2026-09-20.md` RC-5):「**恰 =**」若作**清单事实**读,不可能成立 ——
  **没有任何 asmdef 字段能产出「BCL-only」的引用集**(`"references": []` ≠「什么都不引」)⇒
  两处表值意即**被断言的性质**,由裁定 ④ 的**构建期断言**强制(构建期读 `GetReferencedAssemblies()`
  比对,不合 = 构建失败)。白名单仍不含任何引擎程序集(§一 结论不动)。

### ② QQ-03 门面二义拆除(**2026-09-20 裁定 = 甲**)

- **甲(推荐)**:`Fix` 为 `Sim.Contracts` 内 **public struct**,但 `ToFloat()` 标
  **`public` 仅限契约可见、消费约束改走「白名单断言」**:构建期扫描断言「`ToFloat()` 的调用点
  所在 asmdef ∈ {`Sim.Codec`, `Gameplay.*`},`Sim` 内调用 = 构建失败」。
  **理由**:乙案(门面 = `Sim` 自身 public 面)使「仅门面可调用」字面恒假;甲案用**同一把
  ADR-017 白名单锤**执行同一纪律,不引入 `InternalsVisibleTo` 的可见性迷宫
  (`Fix` 必须 public —— 它在 `Sim` 与 `Sim.Codec` 两边出现,`internal` 反而切不开)。
- **乙(备选)**:`ToFloat()` 设 `internal` + `InternalsVisibleTo` 精确点名消费装配。
- 两案**都满足** ADR-006「单一浮点出口」语义;差别在执法机制(断言 vs 编译器)。
  **2026-09-20 用户裁定 = 甲**;乙案留档(若实现期白名单扫描器成本失控,重提乙须另开修订)。

### ③ QQ-01 `IPlayerMotor` / `ICameraRig` 落位(三选一 · **2026-09-20 裁定 = ①′**)

事实(2026-09-20 收窄):唯一跨门需求 = **29 结算 → 触发 1 传送**(一次**命令**,不需要连续量)。
- **①′(已裁)**:契约拆两半 —— **整数命令半进 `Sim.Contracts`**
  (`ITeleportCommand{ actor_id, cell(WorldPos) }` 纯整数;`Sim` 可安全引用)·
  **`Vector3` 连续半(`Position`/`Tick(dt)`)留 `Gameplay.Presentation`**,不进任何跨门契约。
  `architecture.yaml` 的两条所有权条目改指拆分后名字。② 破「仅 BCL」不取;③ 自建 `Float3`
  造出第二浮点值类型(与 `Fix` 域混淆 + ADR-006 浮点禁令的读者误导面)不取。
- 本条同时兑现 ④ 的「门 A 加抽象须追加进本法」义务(ADR-005 Amendment F 流程)。

### ④ 清单封闭性 = 本法(ADR-005 Amendment F 的程序集版)

新 asmdef 的**增加**即「新增跨门类型」同款事件:必须**追加进本表**(修订本 ADR 或另开 ADR),
构建脚本断言「工程内 asmdef 集合 = 本表 ∪ {测试装配族}」—— 多一个未登记装配 = 构建失败。
(与 ADR-024 A1 断言同构:**登记面与构建面一致性**,一个方向都不许漏。)

### ⑤ 测试装配落点(解种子测试不编译)

> **⚠️ 2026-09-23 回写轮(路径订正 · 承 U0 裁定)**:下列 `tests/EditMode` / `tests/PlayMode`
> 为**仓库根路径**的散文写法;实际落点由 **U0 工程根裁定**(乙案 `unity/` 子目录)钉为
> **`unity/Assets/Tests/{EditMode,PlayMode}/`** —— Unity 只编译 `Assets/` 内的测试装配,
> 仓库根 `tests/` 保留为非 Unity 产物(种子测试迁移已由 U0 b1a/b5 实物落地)。
> **本件原文不追改**(防重写历史);判据以本注为准。

`tests/EditMode` → asmdef `Sim.Contracts.Tests`(引 `Sim`/`Sim.Contracts`/`Sim.Codec`,UTF EditMode);
`tests/PlayMode` → `Gameplay.Tests`(引 Presentation/UI)。现有种子测试 `sim_fixedpoint_test.cs`
**归 `Sim.Contracts.Tests`** —— 本件 Accepted 即其编译前提(承 `requirements-traceability.md` §何时成真 RTM 第 3 条)。

### ⑥ QQ-02 不入本件(如实登记)

`IIdAuthority.Next()` 单/双方法口径 = **逐字回填 ADR-005** 的修正案义务,非程序集划分问题;
留在 `architecture.md` QQ 台账,防止「清单 ADR 顺手裁流内契约」的越权模式(ADR-007 教训)。

### Key Interfaces(本件新增/拆分的契约)

```csharp
// Sim.Contracts —— 纯 BCL;门 A 可引用
public interface ITeleportCommandSink {      // ③ 的整数半;写者 = 29 结算,执行者 = 1 的表现层适配器
    void RequestTeleport(int actorId, WorldPos cell);   // 落不落流 = 29 的 GDD 裁(本件只给通道形状)
}
// Gameplay.Presentation —— Vector3 半留在 L5,不再是「边界程序集成员」
// (IPlayerMotor.Position / Tick(float dt) 与 ICameraRig.Camera 在此程序集内声明)
```

## Alternatives

### Alt B —— 单一大契约装配吸收 codec(三名合一 → 一名)
- **Pros**:装配数最少。
- **Rejection Reason**:codec 的磁盘/迁移代码与热路径契约同装配 ⇒ `Sim` 引用集被迫拖进
  文件 I/O 面;且 `Fix` 编码器守卫(D-21-18)失去「引用 codec 与否」的天然测试边界。
- **What Would Change Our Mind**:若实现期 asmdef 数量造成循环引用实测困难。

### Alt C —— 不新增 `Gameplay.UI` 分装配(全呈现一锅)
- **Rejection Reason**:焦点单栈门(ADR-013)从「运行时状态断言」升为「编译期引用隔离」
  的机会白白丢掉;分装成本 = 一次 asmdef 文件。

## Consequences

- **Positive**:C2 两侧(#1 #2)均有裁决文本;种子测试有编译落点;「门面」二义、三名并列
  一次性拆除;门 A 白名单升为**恰等于**两件套(`BCL ∪ Sim.Contracts`),比原文更严且仍不含引擎。
- **Negative**:七个 asmdef + 测试族 = 装配管理成本(单人项目下由 §④ 封闭性断言兜底,防漂移);
  称谓作废的涟漪触及 4 份原文(回写轮逐处加注,记入 `requirements-traceability.md` 回表面)。
- **Neutral**:`TR-skill-002` 的容器有了,**内容仍归 #4**(不借本件记绿)。

## Risks

| Risk | Probability | Impact | Mitigation |
|---|---|---|---|
| `Sim` 引用集从「[]」变为「[Sim.Contracts]」被误读为门 A 松动 | 中 | 中 | §① 注记原文:`adr-017:251` 的 `"references": []` 本就**不可能**成立(Sim 必须见 SimEvent)—— 该空集是**示例简写**还是**断言文本**,Accepted 时须向 ADR-017 挂一记订正(登记,不偷改)|
| 甲案白名单断言的扫描成本 | 低 | 低 | 与 ADR-017 §二 共用同一把 Roslyn-free 扫描器(实现期工具位)|
| asmdef 循环(UI ↔ Presentation 互引)| 低 | 高 | §④ 封闭性断言 + CI 编译即测 |

## Performance Implications

(无 —— 装配划分不改运行时路径;`StreamRouting.g.cs` 的 switch 成本见 ADR-024。)

## Migration Plan

1. ~~用户照准 ①②③~~(2026-09-20 已裁)→ 建 6 个 asmdef 文件(实现故事,非本件义务)
2. 种子测试挂 `Sim.Contracts.Tests`,CI(ADR-012 矩阵)首跑编译
3. `architecture.md` §2.0 七行全部改指具名装配;`architecture.yaml` 两所有权条目改指 ③ 拆分名
4. 称谓作废四处的回写加注

**Rollback**:纯文档 + 未创建的工具位;git revert。

## Validation Criteria

- [ ] **V-1** 工程内 asmdef 集合 = §① 表 ∪ 测试族(§④ 断言可执行;承 ADR-017 构建失败级)
- [ ] **V-2** `Sim` 引用集实测恰 = {BCL, `Sim.Contracts`};含任何 `UnityEngine.*` = 构建失败
- [ ] **V-3** 种子测试在 `Sim.Contracts.Tests` 下**编译并首跑**(ADR-012 矩阵内)—— 跑通前
      `requirements-traceability.md` 第 3 前提不划掉(禁借绿)
- [ ] **V-4** `ToFloat()` 调用点扫描(甲案)在 `Sim` 内出现调用 = 构建失败样例测试一条
- [x] **V-5** 全库 grep「门面程序集 / 独立契约程序集」命中处均带「现名」注(回写完成判据) —— **✅ 2026-09-23 回写轮结**:四处原文(ADR-005:228 / 44 GDD:242 / `audio-system.md:971` / `tests/README.md:38-41`)命中处均已带「现名 = …」注(`tests/README.md` 第 5 处由 2026-09-21 review S-4 处理);⚠️ 判据量词 = 「命中**处**」,落实时**不得降格为按文件计数**(`audio-system.md` 同文件 `:242` 有注而 `:971` 曾漏网 = 文件级 grep 谎报完成,已修)
- [x] **V-6** ADR-017 `"references": []` 歧义的订正记录挂讫(§Risks 第 1 行) —— **✅ 2026-09-23 回写轮结**:ADR-017 §二 `"references": []` 已挂订正注(该空集为**示例简写**非**断言文本** —— `Sim` 必须见 `SimEvent`),见 §Risks 第 1 行

## GDD Requirements Addressed

| TR / 义务 | 来源 | 本 ADR 如何覆盖 |
|---|---|---|
| `TR-randomevents-010` 的 asmdef 半 | `random-events.md`(Foundation,gap)| §①④(校验体系半 = ADR-024 ⑤)—— **✅ 2026-09-23 回写轮:`adr:` 已挂(`ADR-024 + ADR-025`,`gap → covered`)**;⚠️ 执行体落地归实现轮(禁借绿)|
| `TR-skill-002`(定点纪律容器)| `skill-progression.md` | 容器(`Sim` + `Sim.Contracts` 内 `Fix` 域);内容归 Required ADR #4 |
| `TR-skill-008` 相邻(持久化落点)| 同上 | `Sim.Codec` 是落位;逐字段仍归 7a 实现轮 |
| QQ-01 / QQ-03 | `architecture.md` §Open Questions | §② §③ 直裁 |
| `OQ-A2` 守卫落点 | `accessibility-requirements.md` | §①:`Gameplay.Presentation` 内本地态 + 不引 codec ⇒ 写流物理不可达(断言可加在 ④ 扫描器)|
| TD 条件 C2 的另一半 | 架构 v1.0 | Accepted 即与 ADR-023 同批结案 |

## Related

`docs/architecture/architecture.md`(§2.0 / §2.2 / §4.5 / QQ-01/02/03)· `adr-005` · `adr-006` ·
`adr-017`(白名单先例)· `adr-010` · `adr-020` · `adr-022`(不进构建先例)· `adr-023` · `adr-024` ·
`tests/unit/sim/sim_fixedpoint_test.cs`(等待本件 Accepted 的编译前提)
