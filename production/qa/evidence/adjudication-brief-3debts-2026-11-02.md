# 三条「欠裁决」债的裁决简报(2026-11-02)

> **缘由**:用户指令「按你的建议继续修,一个个的不要停」→ 剩余 10 条逐条核后,
> 三条**不是做不动,是欠一个裁决**。本件给每条的**选项 + 后果 + 建议**。
> **本件不裁**,只呈报。

---

## 一、`TD-processing-03` —— 「同键 `Craft`」语义未定义

### 事实(全部可复跑)

| # | 事实 | 命令 |
|---|---|---|
| F1 | ADR-008 §二 逐字:「F-37.1 的全部正确性建在「**全序无平局**」上」 | `sed -n '126,129p' docs/architecture/adr-008-case-event-stream.md` |
| F2 | ADR-008 §Validation 有一条**未勾**项:「单测:跨流全序 `(Tick, StreamPriority, Patient, Seq)` 无平局」 | `grep -n "无平局" docs/architecture/adr-008-case-event-stream.md` ⇒ `:514` |
| F3 | **已有**一条夹具覆盖该项:`test_crossStreamOrder_deterministicNoTies`(构造 6 条事件,断言相邻键严格 `<`) | `grep -n "test_crossStreamOrder_deterministicNoTies" unity/Assets/Tests/EditMode/CaseSystem/case_identity_routing_test.cs` ⇒ `:218` |
| F4 | `Seq` 发号器**每 `(Tick, Patient)` 组复位并递增**(`_currentSeq = 0` / `++`)⇒ 主机正常 Append 的两条同 `(Tick, Patient)` 事件**必然不同号** | `sed -n '262,276p' unity/Assets/Sim/EventStream.cs` |
| F5 | `EventStream` 有**去重**(四元组 `(Kind, Patient, Tick, Seq)`),重复 Append 同键会被挡 | `sed -n '242,244p' unity/Assets/Sim/EventStream.cs` |
| F6 | `FurnaceState.TryGetLatest` 的同键分支**今天不可达**:`at.CompareTo(best) <= 0` 里 `== 0` 只在 F4 被绕过时才可能 | `sed -n '140,146p' unity/Assets/Sim/Processing/FurnaceState.cs` |
| F7 | **该分支仍是一条判据**:变异「改成 `< 0`」(即同键改取后者)会让 `furnace_busy_count_equivalence_test` 的 M3 靶转红 | `production/qa/evidence/impl-td-processing-04-busy-count-2026-11-02.md` §四 |

### 选项

| 案 | 内容 | 后果 | 成本 |
|---|---|---|---|
| **甲** | **取后者**(`<= 0` 继续 `continue` ⇒ 保留先入者;或改成同键时覆盖 ⇒ 取后入者),并在 `FurnaceState` 类注写明「同键在合法流上不可达,此处口径 = 防御性」 | 不动任何 ADR;**ADR-008 §Validation 的无平局项由 F3 的既有夹具覆盖**(可勾) | 1 行注释 + 1 条夹具(钉住同键口径) |
| **乙** | **ADR-008 修订轮**新增一节「同键 = 流损坏,消费方须 fail-loud」,`FurnaceState` 同键分支改为 `throw` | 与 F1「全序无平局」自洽;**但合法流上永远不触发** ⇒ 新增一条**零输入可达的门**(假绿形态之一) | ADR 修订 + 改实现 + 夹具(须造非法流) |
| **丙** | 维持现状(取先入者)+ 补一条夹具钉住该行为 | 最小改动;**但没有权威依据** —— 夹具钉的是「当前实现」,不是「裁定的语义」 | 1 条夹具 |

### 我的建议:取**甲**

**理由**:F4 + F5 已经证明「合法流上同键不可达」是**结构性**的,不是概率性的。乙案会造一条
永远不触发的门(项目已登记过这个假绿形态:ADR-035 N-4「零输入可达的门」)。丙案把「现状」
当裁决,后续任何一次重构都会问「为什么取先入者」而无处可查。

甲案的要害是**把「不可达」写成可查的事实**,并让 F3 那条既有夹具承担 ADR-008 的 Validation 项
—— 那一项今天**其实已经被覆盖了**,只是没人在 ADR 里勾。

⚠️ **若选甲,我会同时做两件**:① 给 `FurnaceState` 同键分支加一条注释(说明不可达 + 口径);
② 在 ADR-008 §Validation 那一项后加注「已由 `case_identity_routing_test::test_crossStreamOrder_deterministicNoTies` 覆盖」。

---

## 二、`TD-building-04` —— `structure_id` 未走 `IIdAuthority`

### 事实(全部可复跑)

| # | 事实 | 命令 |
|---|---|---|
| F1 | ADR-010 §三 义务 12 逐字要求「`structure_id` 高水位(共用 `IIdAuthority` 空间)」 | `grep -n "structure_id" docs/architecture/adr-010-persistence-save-format.md` |
| F2 | 实现用自有 `_nextStructureId`(起点 1),**零 `IIdAuthority` 调用** | `grep -c "IIdAuthority" unity/Assets/Sim/World/StructureKinds.cs` ⇒ 仅注释 |
| F3 | **两空间独立 ⇒ item 与 structure 永不撞号**(今天比 ADR 更安全) | 同上 |
| F4 | **ADR-033 的平移量 `K := N`(基线件数)建立在「基线占 `1..N`、玩家从 `N+1` 起」之上** | `docs/architecture/adr-033-structure-id-collision-on-load.md` §Decision |
| F5 | ADR-033 已于 **2026-10-28 实现并关闭 `TD-building-02`**,含 9 条变异全红 | `production/qa/evidence/impl-adr033-structure-id-remap-2026-10-28.md` |
| F6 | GDD `inventory-and-items.md:648` 设想的「与结构行撞号」**在当前实现下不会发生** | `sed -n '648p' design/gdd/inventory-and-items.md` |

### 选项

| 案 | 内容 | 后果 | 成本 |
|---|---|---|---|
| **甲** | `StructureInstanceRegistry` 改用 `IIdAuthority`(兑现义务 12) | **必须同时重算 ADR-033 的平移量** —— 基线号段可能不再从 1 起,`Placed.id ≤ N` 的**充要性证明**要重做 ⇒ ADR-033 的 9 条变异 + 夹具全要重跑 | 高:跨 2 个 ADR + 已关闭的 `TD-building-02` 回归 |
| **乙** | 修订 **ADR-010 §三 义务 12**,承认 structure 独立空间,并写明「GDD `:648` 的撞号设想不成立」 | **今天更安全的状态被合法化**;ADR-033 一字不动 | 低:1 份 ADR 窄修订 + 1 条 GDD 注 |
| **丙** | 维持现状,只登记(即今天) | ADR 与实现持续不符 —— 下一个人读 ADR-010 会以为结构 id 可重构 | 零 |

### 我的建议:取**乙**

**理由**:甲案的真实成本不在「改几行代码」,在**推翻 ADR-033 的充要性证明** —— 那份 ADR 刚
于 2026-10-28 实现并关闭了 `TD-building-02`,9 条变异全红。为一个「今天比 ADR 更安全」的状态
去重开那条链,收益是负的。

乙案把「独立空间」写成裁决,代价是一份 ADR 窄修订;而且它**必须同时写明 GDD `:648` 的设想
不成立**,否则下一轮会有人按 GDD 去实现撞号处理。

⚠️ **乙案的诚实边界**:它不解决「structure_id 不可重构」这个**机制**问题 —— 它只是**承认**
该机制今天不存在。若将来联机需要跨机结构 id 一致性(ADR-001 三流复制),这一条会重新变成 P0 面。
我会在 ADR 修订里把这条**写成显式的未来触发条件**,不写成「已解决」。

---

## 三、`TD-skeuo-01` —— 九宫格黑块:缺门

### 事实(全部可复跑)

| # | 事实 | 命令 |
|---|---|---|
| F1 | 黑块**已归因**:只在「代码灌注 sprite」路径,且需**两轴同时**小于 border 之和 | `production/qa/evidence/skeuomorphic-ui/nineslice-probe-2026-10-31*` |
| F2 | **生产上唯一的代码灌注 sprite = 焦点环**,border = **2**(`2+2=4` ≪ 行高 44) | `grep -n "StyleBackground(_focusRingSprite)" unity/Assets/Gameplay.UI/Skeuomorphic/SkeuoRuntimeDriver.cs` ⇒ `:287` |
| F3 | `.paper` / `.scroll` / `.seal` 全走 **USS 路径**(归因结论 ③:**结构性免疫**) | 同上探针 |
| F4 | `NineSliceBoundsValidator` **生产零调用**(只有 5 条单元测试调) | `grep -rn "NineSliceBoundsValidator" unity/Assets --include=*.cs \| grep -v /Tests/` ⇒ 仅定义 |
| F5 | C1 门(`SkeuomorphicUiGates.cs:278-300`)**只查 `slice > 0`**,半尺寸断言自陈「暂留内容管线」 | `sed -n '278,282p' unity/Assets/Editor.Tools.Gates/SkeuomorphicUiGates.cs` |
| F6 | `SkeuoElementLibrary.Create` **只挂 USS class,不灌 sprite** ⇒ 元件库本身不触发黑块 | `sed -n '33,48p' unity/Assets/Gameplay.UI/Skeuomorphic/SkeuoElementLibrary.cs` |

### 选项

| 案 | 内容 | 后果 | 成本 |
|---|---|---|---|
| **甲** | **加构建期门**:扫 `Gameplay.UI` 全树,凡 `new StyleBackground(sprite)` 赋值点,要求其元素的**两轴最小值 ≥ sprite border 之和**(静态不可判 ⇒ 只能要求「同文件内有尺寸下限常量」或改运行期断言) | ⚠️ **静态门在这里不可靠**:元素尺寸由布局决定,编译期读不到。**真门只能落在运行期**(42 元件库 `Create` 里加尺寸下限),而那会改元件库对外形状 | 高 + 需裁 |
| **乙** | **让 `NineSliceBoundsValidator` 有生产调用方** —— 在 42 元件库 `Create` 加尺寸下限 | 改元件库对外形状 ⇒ **须裁** | 中 |
| **丙** | **不加门,只补一条运行期夹具**:断言「当前所有代码灌注点都满足两轴 ≥ border 之和」 | 门只覆盖**今天已存在的**灌注点(1 个),新加的漏 | 低 |
| **丁** | 维持现状 + 在 `SkeuoRuntimeDriver` 的 `CreateFocusRing` 处加**运行期守卫**(`if (sprite.border 之和 > min(ring 两轴)) throw`) | 只护住那一个点,但**是 fail-loud 而非静默黑块** | 低 |

### 我的建议:取**丁 + 丙**

**理由**:F2 说明生产上**只有一个**代码灌注点。甲 / 乙都要改元件库对外形状(债册逐字写「须裁」),
而收益是防一个**今天不存在**的退化。丁案把那个唯一点变成 fail-loud(黑块 → 抛),
丙案补一条夹具钉住「今天所有灌注点都安全」。

**丁 + 丙 的诚实边界**:它**不阻止**将来有人新加一个代码灌注点 —— 那会静默黑块。
⇒ 我会在债册把这一点**明写成残余**,并把「加通用门」保留为**下次有第二个灌注点时的触发条件**。
这与 ADR-035 N-4 的处置同型(不造零输入可达的门)。

---

## 汇总:三条建议

| 债 | 建议案 | 需要你确认的 | 我改完后能自证的 |
|---|---|---|---|
| `TD-processing-03` | **甲**(注释 + 勾 ADR-008 Validation 项) | 「不可达」口径接受否 | 夹具钉住同键口径 + 变异转红 |
| `TD-building-04` | **乙**(ADR-010 §三 义务 12 窄修订) | 「承认独立空间」接受否 | ADR 修订落盘 + GDD `:648` 加注 |
| `TD-skeuo-01` | **丁 + 丙**(运行期守卫 + 夹具) | 「不造通用门」接受否 | 守卫 fail-loud + 夹具转红 |

⚠️ **三案都不改变 `未闭 P0 面 = 0`** —— 三条本就是 `N`。
