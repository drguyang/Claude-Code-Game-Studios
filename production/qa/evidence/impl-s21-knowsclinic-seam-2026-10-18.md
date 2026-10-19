# story-021 · `knowsClinic` 注入缝 —— 实现证据原件

> **对象**:`production/epics/audio-system/story-021-knowsclinic-injection-seam.md`
> **AC**:`AC-021-1` … `AC-021-4`(前三条 [A],第四条账面)
> **日期**:2026-10-18
> **来源判定**:`docs/tech-debt-register.md` **`TD-audio-033`** —— `PatientCueDriver` 逐字
> `_behavior.Step(id, knowsClinic: false)`,装配层**发明了 13 的行为输入** ⇒ 全部病人永不进 `Seeking`。
> **本件性质**:实现轮证据(交付面 → AC 载体 → 证伪轮 → 回归对账 → 如实登记缺口)。

---

## §一 交付面

| # | 交付 | 文件 | 关法 |
|---|---|---|---|
| **AC-021-1** | 注入缝**必填**(null ⇒ 具名 ANE,不提供静默默认) | `Gameplay.Boot/PatientCueDriver.cs` | 构造参数 `Func<PatientId,bool> knowsClinic = null`,体内 `?? throw new ArgumentNullException(nameof(knowsClinic), …)`;**所选口径 = 显式 fail-loud**(story 边界裁定 1 的二选一) |
| **AC-021-1** | 装配层从**真值源**喂,不写死常量 | `Gameplay.Boot/CompositionRoot.cs` | `knowsClinic: clinicDirectory.KnowsClinic` —— 真值链 = 6 的 `EcozoneRegistry` → `RegistryClinicKnowledgeSource` → 13 的 `ClinicKnowledge` → `ClinicKnowledgeDirectory` |
| **AC-021-1** | 真值派发面(F-13.2 的三段) | `Gameplay.Boot/ClinicKnowledgeDirectory.cs`(新) | 取锚点格 → 首见求 `HomeRegion` 并缓存 → `ClinicKnownByRegion`;**不自造规则**(三项事实全来自注入源) |
| **AC-021-1** | `IClinicKnowledgeSource` 的接线期实装 | `Gameplay.Presentation/PatientAI/ClinicKnowledge.cs` | 新增 `RegistryClinicKnowledgeSource`:`EcozoneOf` 直转 6 的注册表;POI / 建造两缝 **null ⇒ 该析取项恒假**(如实表达,非发明) |
| **AC-021-2/3** | 判定宾语 = 真 `PatientBehaviorDirector` + 真 `BehaviorMap` | `PatientCueDriver.KnowsClinicOf(PatientId)`(新可观测面) | 只**转发**注入的委托,不参与决策 —— 使「装配层喂了什么」可判定,而不必等体征落进寻诊带 |
| **AC-021-1** | 组合根支持注入真值链 | `CompositionRoot.Assemble(...)` | 新增可选形参 `ClinicKnowledge clinicKnowledge = null`;null ⇒ 自建**空**生态区表(= 装载面未接的今日形态) |
| **AC-021-2/3** | 装配出的行为导演可观测 | `CompositionRootServices.BehaviorDirector` | 暴露 `PatientCueDriver` 消费的**同一个**实例(仅为可判定性;⚠️ **不**是行为态合并,`TD-audio-033` ② 仍 open) |
| 测试 | 新夹具 8 测 | `unity/Assets/Tests/EditMode/Audio/knowsclinic_seam_test.cs`(新) | 断言宾语 = 真组合根装配件 + 真 `ClinicKnowledge` + 真 `EcozoneRegistry` |

### 为什么 `knowsClinic` 选「必填 + 抛」而不是「默认 false」

story 边界裁定 1 要求二选一并**在实现轮登记所选**。选 **fail-loud** 的理由:
「静默默认 `false`」正是本条债项的**形态本身** —— 允许省略参数等于允许装配层再次静默发明输入,
只是把字面量换成了缺省值。代价是 62 处既有 `new PatientCueDriver(...)` 调用点必须显式给值,
这个代价**是收益**:每一处都必须在注释里写明「这里的 `KnowsClinic` 是什么」。

### 为什么另立 `ClinicKnowledgeDirectory` 而不是在组合根里写个 lambda

F-13.2 有**两条**语义(「`HomeRegion` 只在初始化时求一次」与「`ClinicKnownByRegion` 是析取」)。
写进 lambda 就无法被单独断言(只能整条装配跑)。独立成类后:
① 缓存语义有独立夹具(`test_ac021_3_directory_cachesHomeRegionOncePerPatient`,断言 `EcozoneOf` 调用数 = 2 而非 3);
② 读档轮有 `ResetForLoad` 的落点;
③ `TD-audio-033` ② 收敛时,视觉侧可直接复用**同一个** directory,不必抄第二份逻辑。

---

## §二 AC ↔ 测试载体映射

| AC | 载体(测试名) | 判据 |
|---|---|---|
| **AC-021-1** | `KnowsClinicSeamTest.test_ac021_1_driver_ctor_requiresKnowsClinic_neverSilentlyDefaults` | 缺缝 ⇒ 具名 `ArgumentNullException`,`ParamName == "knowsClinic"` |
| | `test_ac021_1_compositionRoot_feedsSeamFromTruthSource_notHardcodedFalse` | 宾语 = **真 `CompositionRoot.Assemble`** 装配出的驱动:① 注入「区 0 有医馆 POI」⇒ `KnowsClinicOf` 必须 true;② 不注入(空表)⇒ 必须 false。写死 false ⇒ ① 红;写死 true ⇒ ② 红 |
| | `test_ac021_1_productionTree_hasNoHardcodedKnowsClinicFalse` | 扫 `Gameplay.Boot` + `Gameplay.Presentation` 两棵**生产**树(剥行注释后),`knowsClinic: false` 字面量必须为零;并断言扫描件数 > 50(空集不得冒充绿) |
| **AC-021-2** | `test_ac021_2_knowsClinicTrue_patientEntersSeeking` | 真 `PatientBehaviorDirector` + 真 `BehaviorMap`,体征 0.6 ∈ [SEEK_MIN, COLLAPSE_MIN),`knowsClinic = true` ⇒ `Seeking` |
| **AC-021-3** | `test_ac021_3_knowsClinicFalse_patientDoesNotEnterSeeking` | 同体征同格,`knowsClinic = false` ⇒ `Idle`(防「一律 true」的假修) |
| | `test_ac021_3_emptyRegistry_makesTruthSourceHonestlyFalse` | 空生态区注册表 ⇒ `KnowsClinic` 为假,且组合根装配仍成立 |
| | `test_ac021_3_noAnchorCell_isFalse_notThrow` | 不在场(无锚点格)⇒ false 且**不抛**,不缓存 `HomeRegion` |
| | `test_ac021_3_directory_cachesHomeRegionOncePerPatient` | 首见求一次 + 缓存命中 + `ResetForLoad` 后重求 = `EcozoneOf` 恰 2 次 |
| **AC-021-4** | `docs/tech-debt-register.md` `TD-audio-033` 结案注 · 本文件 §五 | ① 结案(注入缝已落 + 证伪齐);② **明确留 open**(两份行为态分叉) |

---

## §三 证伪轮(逐条「停用什么 ⇒ 哪条红」)

> 纪律:每例**改 → 跑 → 还原在同一命令内**(`/tmp/fz.py`),还原后 **SHA-256 逐字节校验**
> (`restored sha ok`)。过滤器固定 `DaYiJingCheng.Tests.Unit.Audio.KnowsClinicSeamTest`。

| # | 停用的守卫 | 期望变红 | 实测 |
|---|---|---|---|
| **F1** | 组合根注入改回写死 `_ => false` | AC-021-1 主测(① 半边) | **红 ×1** ✅ |
| **F2** | `BehaviorMap` 的 `Seeking` 分支短路(返回 `Idle`) | AC-021-2 + AC-021-1 主测 | **红 ×2** ✅ |
| **F3** | 组合根注入改为写死 `_ => true` | AC-021-1 主测(② 半边) | **红 ×1** ✅ |
| **F4** | 构造参数改回「可省略 + 静默默认 false」 | AC-021-1 契约测 | **红 ×1** ✅ |
| **F5** | 只塞**文本违例**、不改行为(常量里写 `"knowsClinic: false"`) | AC-021-1 源码门 | **红 ×1** ✅ |
| **F6** | `ClinicKnowledgeDirectory` 去掉 `HomeRegion` 缓存(每拍重求) | AC-021-3 缓存测 | **红 ×1** ✅ |

**⚠️ 两处如实登记的失败尝试(不算证伪成功)**:

1. **首轮 F1 / F3 双双不红**(`exit 0`)。根因 = 我最初那条 AC-021-1 主测**根本没有驱动
   组合根装配出的驱动** —— 它自己另建了一个 `PatientCueDriver`,于是「组合根注入什么」对它
   没有观测差。这正是 story 自己点名的失败模式(「判据须是**生产件可测**的」)。
   **修法 = 加可观测面而不是加断言**:`PatientCueDriver.KnowsClinicOf` + `CompositionRootServices.BehaviorDirector`,
   并把主测的宾语改成 `CompositionRoot.Assemble(...)` 的产物。改后 F1 / F3 均转红。
2. **F5 首版不红** —— 第一版 F5 把 `knowsClinic: clinicDirectory.KnowsClinic` 换成
   `_ => false,   // knowsClinic: false`(行注释)。源码门**剥行注释**后看不到违例,而行为半边
   又被 F1 覆盖 ⇒ 这一例与 F1 **没有观测差**。改为「只加一条含违例的常量、行为不变」后才红
   —— 那才是源码门**独有的**判别面。

---

## §四 回归

| 项 | 值 | 文件 |
|---|---|---|
| 基线(S20 终跑,story-020) | `3757 / 3712 / 0 / 45` | `unity/Logs/s20-full-editmode.xml` |
| **本件全量(首轮,评审前)** | `3765 / 3719 / 1 / 45`,exit 8 | 被评审轮覆盖 |
| **本件全量(终轮,评审修复后)** | **`3765 / 3720 / 0 / 45`**,exit 0 | `unity/Logs/s21-full-editmode.xml` |
| 等式判据 | `total 3765 − 3757 = +8`;新增 `[Test]` 逐名数 = **8** | `knowsclinic_seam_test.cs` |
| `passed` 对账(首轮) | `3719 − 3712 = +7`,**+1 红** ⇒ `7 + 1 = 8` 严丝合缝 | — |
| `passed` 对账(终轮) | `3720 − 3712 = +8` = 新增 8 条,零红 | — |
| `MovementFeedTest` 单跑 | `5 / 5 / 0` 绿 | 见下 |

### 首轮唯一红 = 已知间歇红(**非本件引入**;终轮未复现)

`DaYiJingCheng.Tests.Boot.MovementFeedTest.test_move_afterFreeFall_horizontalDisplacementBoundedByConfig`
—— 失败信息 `Expected: 0.000932… +/- 0.000193… But was: 0.0`,与 `TD-player-01` 记录的
「`expectedDx < CharacterController.minMoveDistance (0.001)` ⇒ 位移被引擎吞」**同型**。
单跑该 fixture **5/5 绿** ⇒ **间歇红坐实**(承 `impl-s18-*` §六 ① 与 `impl-s25-*` §回归的同一判定)。
⚠️ 本件**不**把它记成「已修」,也**不**用它抵账 —— 归系统 1 的实现轮(`TD-player-01`)。

---

## §五 如实登记:未证 / 未跑 / 留 open

| # | 项 | 状态 | 说明 |
|---|---|---|---|
| 1 | **`TD-audio-033` ②**(装配层与视觉侧各持一份行为输入 ⇒ 同一病人两份行为态可分叉) | **仍 open** | story 边界裁定 2 明写「本件只登记,不硬改」。本件只把 `PatientCueDriver` 消费的那份**接到真值源**;`CompositionRootServices.BehaviorDirector` 的暴露**不是**合并,只是可观测 |
| 2 | **生产 `KnowsClinic` 今日恒假** | **如实登记** | 生态区装载面(`world_ecozones.cooked`)未接 ⇒ 组合根自建空 `EcozoneRegistry`;POI 定义 / `StructurePlaced` 两个查询缝亦未接 ⇒ F-13.2 两个析取项皆假。**行为与 story-021 之前等价**,差别只在**形状**(真值源到位后只换三个注入缝,调用点不改)。`test_ac021_3_emptyRegistry_makesTruthSourceHonestlyFalse` 钉住这一实况 —— **装载面接上后该条期望须随之改**,不得当回归修掉。<br>⚠️ **禁止误读**:**不得**把本件读成「`Seeking` 现在可达了」。本件只让 `knowsClinic` 的**传值路径**从写死常量变成注入真值源;真值源今天全假 ⇒ 生产行为的可观测结果**与旧硬编码 `false` 逐位相同**。「`Seeking` 在生产可达」的充要条件是三个装载面接通后另跑一次,**不在本件的证据面内** |
| 3 | `spawn_anchor(p)` 的病人侧登记(`O-13-5`) | **未解决** | 本件用**在场登记簿的逻辑格**作锚点(`presence.TryGetCell`),因为病人侧 `spawn_anchor` 尚无登记面。这不是 `spawn_anchor` 的替代品 —— 只是「没有锚点时最保守的读法」;真锚点到位后 `ClinicKnowledgeDirectory` 的定位缝换掉即可 |
| 4 | AC-13-D2/D3 的 `[L]` 走查 | **NOT-RUN** | story `## Out of Scope` 明写归 13 epic 的走查面 |
| 5 | IL2CPP 三格(ADR-012 矩阵) | **NOT-RUN** | 本机仅 Mono |
| 6 | 44 epic 收口 | **不得收口** | story DoD 明写;本件只结 `TD-audio-033` ① |

---

## §六 顺手修正(同批,非新裁决)

- `CompositionRoot.Assemble` 的两个重载各加一个**可选**形参 `ClinicKnowledge clinicKnowledge = null`
  —— 默认 null 时逐字回到原行为(自建空表),既有调用点零改动。
- `CompositionRoot` 内把 `new PatientBehaviorDirector(...)` 提成局部变量 `behaviorDirector`,
  供驱动与服务袋共用**同一实例**(避免服务袋暴露一个与驱动内部不同的导演)。

---

## §七 评审轮(2026-10-18)

原件 = `production/qa/evidence/review-s21-knowsclinic-seam-2026-10-18.md`。
两路并行评审(Unity/C# + QA 可测性):**BLOCKING 0 · WARNING 8 · INFO 4**。
本轮修 2 条(W-1 空表测宾语改组合根产物 / W-2 源码门扩到全部生产树),两条各有新反证(F7/F8);
W-3(`StripComments`)**已落未证**(无夹具),另行登记。
⚠️ 另修出的事实:`TD-audio-033` ② 是**前瞻性**债项 —— 生产侧 `new PatientBehaviorDirector(` 唯一在组合根,
`PatientSpatialDirector` 生产零构造点。
