# 乙档剩余项 —— 阻塞登记(零代码改动)

**日期**: 2026-10-20
**用户裁定**(AskUserQuestion):「**三项全登记为阻塞,不动代码**」
**前置**:`370345c`(6 份读者面)· `001de75`(乙档第一份 `disease_action_axis`)· `7d563a2`(甲档剩余项 `MAX_DOSE_DETENTS`)

---

## §0 本轮范围:乙档「剩下的」是哪几项

甲档已清零(`7d563a2`)。乙档原有 5 项,`001de75` 接走 1 项(`disease_action_axis` 的 A 侧)。
**剩下 4 项**:

| # | 字段 | 所属产物 | 本轮判 |
|---|---|---|---|
| 1 | `r_interact` | `interaction_kinds` | **阻塞**(§1) |
| 2 | `routes_to` | `interaction_kinds` | **阻塞**(§1) |
| 3 | `stable_id_source` | `interaction_kinds` | **阻塞**(§1) |
| 4 | `single_dose_max` | `prescription_actions` | **阻塞**(§2) |

前 3 项的阻塞**根因相同**(同一个缺席的装配链),第 4 项的根因**完全不同**(缺的是数值裁定,不是接线面)。

---

## §1 `r_interact` / `routes_to` / `stable_id_source` —— 载体齐备,但装配链整条不存在

### 1.1 载体本身是好的(这一点必须说清,否则会被误读成「没实现」)

三个字段**各有带真实语义的载体类**,且都含构建期判据:

| 字段 | 载体 | 携带的判据 |
|---|---|---|
| `r_interact` | `InteractionRadius` | 4-DC-1 下界(恒不可交互的半径 ⇒ 装载期硬失败) |
| `routes_to` | `KindRouteTable` | 4-DC-5(`RoutesTo` ∈ 已登记系统集)+ 双向闭合 |
| `stable_id_source` | `CandidateSetLoader` | 4-DC-4 ②(`SlotLinearKey` 行须 W/H/D 在场) |

⇒ **不是「缺实现」**。缺的是把这些载体**接上产物**的那条链。

### 1.2 阻塞证据(全部可复算)

**A. 4 侧类的生产构造点**(命令:`grep -rn "new <类>(" unity/Assets --include=*.cs | grep -v "/Tests/"`)

| 类 | 生产构造点 |
|---|---|
| `InteractionSelector` | **0** |
| `ModalGate` | **0** |
| `NeighbourhoodReporter` | **0** |
| `CandidateSetLoader` | **0** |
| `KindRouteTable` | **0** |
| `InteractionRadius` | **1** —— 但**唯一那一处在 `InteractionSelector` 自己的便捷重载里**,而选择器本身零构造 ⇒ **实际不可达** |

**B. 4 的出境面与候选源,生产实现数**(命令:`grep -rn ": *[A-Za-z, ]*\\b<接口>\\b" unity/Assets --include=*.cs | grep -v "/Tests/" | grep -v "interface "`)

| 接口 | 生产实现 | 生产构造 |
|---|---|---|
| `IDiscoveryReporter` | **0** | 0 |
| `IDropSource` | **0** | 0 |
| `IWorldBakedSource` | **0** | 0 |
| `IPatientSource` | **0** | 0 |
| `CandidateSources` | **0** | 0 |

`IDiscoveryReporter` 是 **4 的唯一出境通道**(`InteractionSelector:48` 自陈「本类刻意只有它,没有 `IEventSink`」)——
它零实现 ⇒ 即便硬造一个选择器,`Select()` 里 `_discoveryReporter.Request(...)` 那一步**无处可去**。

**C. 4 的运行期装配在 Boot 侧整体缺席**

```
$ grep -rn "InteractionSelector\|KindRouteTable\|CandidateSetLoader" unity/Assets/Gameplay.Boot --include=*.cs
unity/Assets/Gameplay.Boot/CompositionRoot.cs:345:  // ⚠️ **`KindPriorityTable` 的最终运行期消费者 = 4 的选择器,而 `InteractionSelector`
  ⇒ 唯一命中是**一条注释**,零装配代码。
```

**D. 上一轮(`370345c`)已如实登记过同一条边界** —— 账本更新④ §「一处必须看清的边界」:

> 「`InteractionSelector` 在生产侧**仍零构造点**……⇒ 本轮接的是**契约层读点**,
> **不是**「运行期已有实例在查」。选择器的生产装配归 4 的装配轮。」

**本轮复测:该边界**未变**。** ⇒ `r_interact` / `routes_to` / `stable_id_source` 三项**仍是同一状态**。

### 1.3 为什么「硬接」不成立(不是懒,是接不出有意义的东西)

若给这三个载体各塞一份从产物读来的值:

- `new InteractionRadius(ds.RInteract)` ⇒ 造出一个**没人持有**的半径对象(选择器不构造)。
- `new KindRouteTable(路由字典)` ⇒ 造出一张**没人查**的路由表(4 不在跑)。
- `CandidateSetLoader(...)` ⇒ 三个源接口**零实现** ⇒ 构造不出来;即便伪造三个空实现,
  `Load()` 产出的是候选集,而**消费候选集的选择器不存在**。

⇒ 三者的共同形态 = 「**已装载但无人用**」,正是 `370345c` / `001de75` 反复登记过的借绿形态
(账本更新④:「往缝里塞 provider 而没人读 = 借绿家族」)。**本轮不重犯。**

### 1.4 解除条件(可判,不是愿望)

三项解锁的**唯一前置 = 4 的运行期装配轮**,其判据可枚举:

1. `IDiscoveryReporter` 有一个生产实现(6 的 `PoiDiscoveryGate`?—— 归 6 / 4 的装配轮裁);
2. `CandidateSources` 的三个源接口各有一个生产实现;
3. `InteractionSelector` 在 Boot 侧被**生产构造**(那时 `InteractionRadius` 的便捷重载才可达);
4. 以上齐备后,`r_interact` / `routes_to` / `stable_id_source` 才有**真的读点**可接。

⚠️ **本轮不擅自选 (1)/(2) 的实现归属** —— 那是 4 与 6 的边界裁决,不在「乙档接线」面内。

---

## §2 `single_dose_max` —— 载体在跑,但界的一个因子是「待定」

### 2.1 载体本身**已经是接好的**(与 §1 三项不同,这一点须分清)

| 环节 | 状态 |
|---|---|
| 派生 | ✅ `PrescriptionDerivedBaker.DeriveSingleDoseMaxRaw` —— 烘焙期从 `item_database_items.json` × `DOSE_BASE` 派生,**零手填**(AC-11-09) |
| 落盘 | ✅ cooked 载荷 `[8..15]` = i64 raw Q16.16(本轮 §7.1 解码已验 `= 32768`) |
| 解码 | ✅ `PrescriptionActionsCookedCodec:36` 读入 `SingleDoseMaxRaw` |
| 消费面 API | ✅ `ProgressionEvaluator.Evaluate(..., Fix? doseClampBound = null)` —— 饱和截断语义**已实现且有测** |

⇒ **`single_dose_max` 自己的数据流是通的**。断点在下游那一步。

### 2.2 断点:`DiseaseVitalsService:260` 不传 `doseClampBound`

```csharp
_results[course.Patient.Value] = ProgressionEvaluator.Evaluate(
    entry, course.OnsetTick, tick, course.Doses, _worldSeed, course.Patient);
//                                                          ↑ 第七参缺省 ⇒ doseClampBound = null ⇒ 不 clamp
```

### 2.3 为什么**不能**就这么把 `single_dose_max` 传进去 —— 关键约束

clamp 界 = **`MAX_ACTIVE_DOSE × single_dose_max`**,是**两个因子**。

| 因子 | 值 | 出处 |
|---|---|---|
| `single_dose_max` | ✅ 产物里有(`32768`) | 烘焙派生 |
| **`MAX_ACTIVE_DOSE`** | ❌ **`待定`** | `entities.yaml:1755` 逐字:`value: "待定 —— 见 disease-simulation.md Tuning Knobs"` |

GDD `disease-simulation.md:1576` 同款:`MAX_ACTIVE_DOSE` 默认值 = **`*待定*`**,「值归**数值轮**」。

> ⚠️ **一处须分清**:`entities.yaml:1772` 的 `single_dose_max` **也**写着 `value: "待定"` ——
> 但那是**登记表旋钮行**的口径,与**产物里的派生值**是两回事:产物 `32768` 是
> `PrescriptionDerivedBaker` 按 AC-11-09 从 `drug_profile` 现算出来的,已落盘可读(§2.1 已验)。
> 「登记表写待定」= 该旋钮的**人定值**未裁,而它是**派生量、不由人定**(GDD `prescription-and-medication.md:795` 逐字「⚠️ **不由人定** —— 手填必漂移」)。
> ⇒ **不能拿 `entities.yaml:1772` 当「`single_dose_max` 也没值」的证据** —— 它有值,值是派生的。

⇒ **只传 `single_dose_max` 当界 = 隐含把 `MAX_ACTIVE_DOSE` 当成 1**。
这个假设**没有任何出处** —— 而它一旦写进代码,就变成一个**不可见的数值裁定**
(还是由一个非数值轮的轮次做的)。这正是 `ProgressionEvaluator:121-123` 自陈要避的事:

> 「两个旋钮的**取值均待定**……**本批不自造数值,登记为缺口**」

### 2.4 一个容易踩的误判:接上 `single_dose_max` 会**改变行为**

有人可能认为「反正界很大,传进去也不会变」。**这是错的,且可复算**:

- 当前 `doseClampBound = null` ⇒ **完全不 clamp**;
- 若传 `32768`(单剂 raw)⇒ Σ 一旦超 `32768` 就被**截断**。
- 而 Σ = `Σ drug_potency × Decay` —— 多剂叠加可超单剂值。GDD 特意把界定为
  `MAX_ACTIVE_DOSE × single_dose_max`(而不是 `single_dose_max`),就是因为**单剂界会误截多次给药**。

⇒ 传 `single_dose_max` 不是「保守的中间态」,是**一个语义错误的界**。
**不做比做对** —— 保持 `null` 并登记,是当前唯一正确的处置。

### 2.5 解除条件

| 前置 | 归属 | 状态 |
|---|---|---|
| `MAX_ACTIVE_DOSE` 定值(`> 0` 整数) | **数值轮(用户)** | `待定` |
| 装载面:9 侧把两个因子相乘并传入 | 9 的装载轮 | 未做 |

⚠️ **两项齐备才能接**。只做后一项 ⇒ 自造数值。

---

## §3 本轮做了什么

| # | 内容 | 性质 |
|---|---|---|
| — | **零代码改动** | 用户裁定 |
| R-1 | 三项阻塞的**可复算证据**(§1.2 A–D) | 登记 |
| R-2 | `single_dose_max` 断点的**两因子分析**(§2.3) | 登记 |
| R-3 | 「传 `single_dose_max` 会改变行为」的**误判排除**(§2.4) | 登记 |
| R-4 | 两类阻塞的**解除条件**(§1.4 / §2.5) | 登记 |

本件**不产生**新测试、不改产品码、不重烘。⇒ **无回归可跑**(见 §5)。

---

## §4 一个附带发现(不在本轮裁决面,如实登记)

### 4.1 `4-DC-1…6` 校验器在生产路径**只在烘焙期跑过一次**,运行期零调用点

```
$ grep -rn "InteractionKindTableValidator.Validate" unity/Assets --include=*.cs | grep -v "/Tests/"
unity/Assets/Editor.Tools.Bake/InteractionKindBinder.cs:128:  InteractionKindTableValidator.Validate(rows, rInteract, worldW, worldH, worldD);
  ⇒ 生产面唯一命中 = **烘焙期**。
```

而 `AddressablesDataProvider` 对**任何**数据集**零语义校验** —— `InteractionKindCookedCodec.Read`
只查魔数 / 版本 / 载荷长度 / `AssertFullyConsumed`,**不查** kind 闭集、优先级互异、
`routes_to` 已登记、`duration_owner` 合法、`r_interact` 区间。

### 4.2 为什么登记而不是顺手做

把该校验器接到装载点,确实能让**5 个死字段**(`r_interact` · `routes_to` · `suppresses_motor` ·
`duration_owner` · `duration_owner_system_id`,含 2 个**丙档**)获得真的生产消费者 —— 它是会 `throw` 的消费者,
不是「塞进去没人读」。

**但**:

1. 这是**全库第一个数据集运行期语义校验** —— 新先例,须用户裁定(本轮问过,用户选「全登记,不动代码」);
2. 它**不解除** §1 的阻塞 —— 校验器读字段 ≠ 4 在跑。接上它之后,`routes_to` 仍然没有**路由**消费者,
   只是多了一个**校验**消费者。两者不可混为一谈(禁借绿:不得把「字段被校验」写成「字段被使用」);
3. 若将来做,须配**证伪轮**证明每条断言有判别力(承本项目一贯纪律)。

⇒ 本件**只登记**,不改判是否为缺口,也不立故事。

---

## §5 回归

**无回归可跑** —— 本件零代码改动(无 `.cs` / `.json` / `.cooked` 变更)。
末次全量 EditMode = `7d563a2` 轮的 `total=4012 passed=3976 failed=0 inconclusive=0 skipped=36`
(截至 `2026-10-20 13:25 CST` · 批次 `sprint-15 甲档剩余项接线轮` · commit `001de75` + 8 个未提交改动)。

⚠️ **该数不得被引用为「本轮的回归结果」** —— 本件没有产生任何可回归的改动。
引用它仅表示「乙档登记前的最后已知基线」,不构成对本轮的证明。

---

## §6 完成度(Landed / Proven)

| # | 内容 | Landed | Proven | Proven-by |
|---|---|---|---|---|
| R-1 | 三项阻塞的可复算证据 | ✅ | ✅ | §1.2 的 A / B / C / D 四条命令及输出(可重跑) |
| R-2 | `single_dose_max` 两因子分析 | ✅ | ✅ | `entities.yaml:1756` 逐字 `待定` + `DiseaseVitalsService:260` 七参缺省 |
| R-3 | 「传单剂值会改变行为」的误判排除 | ✅ | ⚠️ **推理,非实测** | GDD `:874` 的界定义(两因子相乘)——**未跑数值实验**,如实标注 |
| R-4 | 两类阻塞的解除条件 | ✅ | ✅ | §1.4 四条 / §2.5 两条,均可枚举核对 |
| — | 代码改动 | **无** | — | 用户裁定「不动代码」 |

**R-3 如实标注的理由**:「传 `single_dose_max` 会截断多剂」是从 GDD 界定义推出的**逻辑结论**,
本轮**没有**构造多剂实验去实测它。不写「已实测」。

---

## §7 未结事项

1. **§1 三项** —— 归 **4 的运行期装配轮**(§1.4 四条前置)。
2. **§2 一项** —— `MAX_ACTIVE_DOSE` 值归**数值轮(用户)**;装载面归 **9 的装载轮**。
3. **§4 附带发现** —— 运行期 `4-DC` 校验门**未立**;是否立须用户另裁(本轮裁定为「不」)。
4. **丙档 3 项**(`suppresses_motor` / `duration_owner` / `intent_uplink`)—— 状态未变,不在本件面内。

---

## §8 落账

| 件 | 状态 |
|---|---|
| 本登记件 | ✅ |
| `tests/integration/item_database/README.md`(账本) | ✅ §2026-10-20 更新⑦ |
| `production/session-state/active.md`(Session Extract) | ✅ 第六段 |
| 代码 / 数据 / 产物 | **零改动**(用户裁定) |
| commit | **未做**(不经用户指令不 commit) |
