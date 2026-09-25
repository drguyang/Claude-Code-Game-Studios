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
可能指一个东西也可能指三个。本 ADR 裁决:**具名六装配清单**(`Sim` / `Sim.Contracts` / `Sim.Codec` /
`Gameplay.Presentation` / `Gameplay.UI` / `Editor.*` 工具族)、**「门面」二义就地拆除**(QQ-03:
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
| **Depends On** | **ADR-005**(Accepted —— 六个 P0 抽象点与边界程序集内容)· **ADR-006**(Accepted —— `Fix` 序列化禁令 D-21-18 / 唯一舍入)· **ADR-017**(Accepted —— `Sim.asmdef` 与门 A 白名单断言,本件把断言的**作用域事实**外推到全清单)· **ADR-010**(Accepted —— codec 义务清单)· **ADR-020**(Accepted —— `IPlayerMotor`/`ICameraRig` 契约)· **ADR-022**(Accepted —— Tooling 层「不进构建」先例)· **ADR-024**(Accepted 同批 —— `StreamRouting.g.cs` 的归属程序集在本件清单内落位) |
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

### ① 具名装配清单(六 + 工具族;**2026-09-20 裁定:清单与命名全照准,不改名**)

| asmdef | 层 | 引用集 | `noEngineReferences` | 成员(§2.0/§2.2 的收口) |
|---|---|---|---|---|
| **`Sim`** | L2 | **期望引用集 = BCL + `Sim.Contracts`(仅此一件)** | **true**(ADR-017 原文) | 19 个 sim 模块(§2.1 表)+ `StreamRouting.g.cs`(ADR-024)+ **25 与 9 同程序集**(2026-09-20 已裁,§2.1 行内)|
| **`Sim.Contracts`** | L3 | **期望引用集 = BCL** | **true** | `WorldPos` · 六抽象点 · `SimEvent`/`PatientId`/`StreamId`/`EventKind` · `VitalsDto` · `Fix` + `FixParse` · `IDataProvider` · `AudioCueDto`+`IAudioCueSink`(44 名义并入)· `IPositionalChannel` · `ClinicEnvDto`+`IClinicEnvQuery`(24 → 42 只读源,2026-09-22 OQ-CP-4 裁定)` |
| **`Sim.Codec`** | L3 | BCL only | true | 7a 三流 codec + 存档头 + `Fix` 自定义编码器(**`internal` + `InternalsVisibleTo("Sim.Contracts.Tests")`** —— D-21-18 守卫的可执行形态)|
| **`Gameplay.Presentation`** | L5(+L4) | UnityEngine · URP · `Sim.Contracts` | false | 1 · 2 · 42-UGUI 侧 · 44 · `ISceneRouter`/`IWorldSpawner`(ADR-023)· L4 边界层模块(4 / 8 / 13 / 51)|
| **`Gameplay.Input`** | L4 | `Unity.InputSystem` only | false | 3 输入与设备(`InputService`,动作资产唯一持有者;**不引 `Sim.Contracts`** —— AC-3-A6 输入程序集不引任何声明 `IEventSink`/`SimEvent` 的程序集,2026-09-25 story-001 复核 B1 拆装)|
| **`Gameplay.UI`** | L5 | + UI Toolkit | false | 42 UI Toolkit 栈 · 39 · 7b · 48(与 Presentation 分装配 = 焦点单栈门的**编译期**表达,ADR-013)|
| **`Editor.Tools`** 族 | L6 | UnityEditor 自由 | n/a | `tools/level/`(ADR-022)+ `tools/kindgen/`(ADR-024)—— **全部不进构建**,asmdef 限 `includePlatforms: ["Editor"]` |

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
- **Negative**:六个 asmdef + 测试族 = 装配管理成本(单人项目下由 §④ 封闭性断言兜底,防漂移);
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
