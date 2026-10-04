# 评审报告原件 — `interaction-system` story-004(POI 自报链路)

> **对象**:`production/epics/interaction-system/story-004-discovery-report-chain.md`
> **日期**:2026-10-04
> **评审形态**:双代理评审(结构侧 = 一般代码评审;QA 侧 = 验收签字)**一轮**(承用户「评审只做一轮」)
> **代码基线**:`interaction-s004-final.xml` = 82 tests / 82 passed → 修复轮后 `interaction-s004-final2.xml` = **86 / 83 passed / 3 skipped(NOT-RUN)** / **0 failed**
>
> ⚠️ 本件是**原件**(BLOCKING 级评审的落盘义务,承 `.claude/docs/coding-standards.md` §Review Evidence
> Standards)。它记 **原判定 → 修复落点 → 验证命令**。

---

## 一、双代理原判定(评审轮,修复前)

### 结构侧评审 — 总判 **APPROVED WITH SUGGESTIONS**(无 BLOCKING)

> 生产代码健全:零 float、零 argmin 耦合、闭合校验正确、确定性排序、`in`/lambda 合法。
> 三条 MAJOR **全在测试层**:① 自称「共用扫描器」实为**已分叉的超集**;② latch 度量**测错量**,
> AC 自己的「每 POI」边缘无法表达;③ 帧率测试**名的断言非其实断的断言**(含死变量)。

| 编号 | 级别 | 判定 |
| --- | --- | --- |
| 结构 #1 | MAJOR | `discovery_report_test.ScanMutableState` **多扫可写属性**,story-001 的 `ScanForMutableState` 不扫 ⇒ 文件头 `:17` 与 doc `:316/:322` 的「共用同一台」**为假**(实为超集)⇒ AC-4-04 与 AC-4-13 保护面静默分叉 |
| 结构 #2 | MAJOR | `SpyPerTickLatch.MaxPerTickKindCount` 数的是「每 tick 不同 POI 数」,而 AC 判据是「每 **POI** 每 tick 准入数」;单 POI 夹具下二者恰好重合 ⇒ **空转**;若补两 POI 边缘,旧断言会**误红正确实现** |
| 结构 #3 | MAJOR | `test_f44_doublingFrameRateDoesNotChangePerTickCount`:`latch1` 在 `spy1` 跑完后才接线 ⇒ **死代码**;末断言用另一条 1 帧/tick 的 `latch1b` ⇒ **同义反复**;「帧率翻倍」比较**从未做在翻倍流上** |
| 结构 #4 | MINOR | AC-4-17 扫描器:无条件跳 `InteractionRadius`(持有者内部第二声明漏)、只认整型、命名启发式可被改名规避 ⇒ 是启发式,**非「声明点 == 1」的证明** |
| 结构 #5 | MINOR | AC-4-18 接受「语义错但已登记」的路由(如 `Patient → 20`);负夹具只覆盖**未登记**号 |
| 结构 #6 | PLAUSIBLE | 胜者 POI **潜在双重出境**(`InteractionSelector.Select` 与 `NeighbourhoodReporter` 各发一次);6 的 `(tick,poi)` latch 使其流层收敛 |
| 结构 #7 | INFO | AC-4-17 正测只驱动 4 侧消费(6 侧为字段读);已诚实登记 BLOCKED-BY |

### QA 侧评审 — 总判 **ACCEPT-WITH-FIXES**

> AC-4-18 / F-4.3b 干净签绿;但 **AC-4-13 的 per-tick 子条(F-A/F-B)与 AC-4-17 的两消费者子条(F-E)**
> 由**自证替身**签绿,须在 story-004 声称 AC-4-13/4-17 绿**之前**改登记 `NOT-RUN / BLOCKED-BY 系统 6`。

| 编号 | 级别 | 判定 |
| --- | --- | --- |
| F-A | BLOCKING | AC-4-13「每 tick ≤1」由 `SpyPerTickLatch`(测试自造替身)签字;删任何生产行为仍绿 ⇒ **验收对象不存在**。须在测试内 `[Ignore("BLOCKED-BY 系统 6")]`(注释非登记) |
| F-B | BLOCKING | AC-4-13/F-4.4 的「自报」**从未门控在主动交互**上;`ReportNeighbourhood` 被无条件调用,无 `InteractIntent`。GDD `:362-363` 要求主动交互 ⇒ 须补「无输入 ⇒ 零出境」负夹具 |
| F-E | BLOCKING | AC-4-17「读取两处」以 `int sixSideRead = radius.Value;`(同测试自造实例的局部读)签字 ⇒ **空转**。须改登记 NOT-RUN 或撤claim |
| F-F | MAJOR | 帧率无关断言 = 死/改标签比较(结构 #3 同源) |
| F-C | MAJOR | 4-DC-1 **上界**在 story-006 的 `4-DC-1…6` 矩阵中**无名额**(只列了 `R_INTERACT = 0` 下界) ⇒ 接收端登记漏 |
| F-G | MINOR | AC-4-18 QA 文字「构建期拒载」**过度声明**(测试实为构造期 throw) |
| F-D | INFO | `(InteractableKind)99` 夹具近乎同义反复,但**合法**(GDD `:908` 点名「JSON 里出现枚举外的 kind 字面量」)且**真触底 `extra` 分支** |
| F-I | INFO | `ReportNeighbourhood` 的 non-POI 过滤未单测(低风险) |
| F-H | INFO | 「替身签形状」故事的标题被动声明 |
| F-J | INFO | 变异证明**无原件**(XML 已删)⇒ 断言级而非证据级 |

### 主会话独立复核(评审并行期,自证)

| 编号 | 判定 |
| --- | --- |
| 主 PF-1 | `KindRouteTable.RegisteredSystems` 原为 7 值,漏 GDD 规则一 Patient 行的 `8/10/11`(4-DC-5 全集 = 10 值)⇒ **合法表被误拒** |
| 主 PF-4 | `NeighbourhoodReporter.Chebyshev` 是同陷阱**第二实现点**,零覆盖 |
| 主 PF-6 | 6 半度量 = sightings 而非 admissions;两 POI 边缘缺失 |
| 主 PF-5 | 「行含 Player」违例**结构性不可构造**;最近形态 = `(InteractableKind)99` |

---

## 二、修复落点(修复轮,逐条)

| 原判定 | 修复落点 | 形态 |
| --- | --- | --- |
| 结构 #1 / — | `discovery_report_test.cs` `ScanMutableState`: **删除可写属性分支**(与 story-001 逐字同式);文件头 `:17`/doc 文案由「共用同一台」订正为「**同源同式**」,并登记「跨文件共享 scanner 需可见性改造」为后续重构 | 生产面**未**动 |
| 结构 #2 / F-A | `test_ac413_sixSideLatchAbsorbsToAtMostOnePerTickPerPoi` → 重写为 `test_ac413_sixSideLatchIsNotRunBlockedBySystem6`(**`Assert.Ignore` 机检登记**,非注释);`SpyPerTickLatch` 仅余于 `test_f43_*` 作**形状记录**(不再作承载判据) | NOT-RUN 登记 |
| 结构 #3 / F-F | `test_f44_doublingFrameRateDoesNotChangePerTickCount` → 重写为 `test_f44_frameRateDoublingDoesNotChangeEgress`:三路径(1×/1×/2× 调用率),**删死 `latch1`**,承重判据 = 「出境计数**恰** = 调用次数」(4 无帧派生项),并显式说明「上界论证的对象是 6 侧落流,非 4 侧发出」 | 4 侧可签事实 |
| F-B | **新增** `test_ac413_negativeFixture_absenceOfIntentYieldsZeroEgress` + 门控/无门控两调用方替身 ⇒ 无输入 ⇒ 零出境;并生产侧 `NeighbourhoodReporter` 方法 doc 钉死「触发前提 = 主动交互」为**调用方契约** | 新测试 + doc |
| F-E | `test_ac417_bothConsumersReadOneValueNotTwoFields` → 重写为 `test_ac417_fourSideConsumesInjectedRadiusValue`:只签 4 侧**真消费**(d∞==R 命中),6 侧半条 **`Assert.Ignore` NOT-RUN** | NOT-RUN 登记 |
| 结构 #4 | `radius_single_source_test.ScanForSecondRadiusDeclaration` 顶部**登记两条已知边界**(命名启发式可改名规避 / 只认整型);`InteractionRadius` 跳过处登记「持有者内部第二声明 = 未覆盖缝」⇒ 扫描器**非「声明点 == 1」的证明** | 已知限制登记 |
| 结构 #6 | 设计气味——**保留**(6 的 `(tick,poi)` latch 使流层收敛);story Out of Scope 已述广播半边归 `NeighbourhoodReporter`;不做代码改动,登记为后续联调观察点 | 保留 + 登记 |
| F-C | 4-DC-1 上界:story-004 侧 deferral 注释已在;接收端(story-006)矩阵无名额 ⇒ **story-004 Completion Notes 显式登记**该漏,呈报后续 batch | 登记 |
| F-G | AC-4-18 「构建期拒载」措辞 ⇒ QA 文字已在 story 的「4 侧消费点的装载失败面」carve-out 内;`Completion Notes` 记明「实测 = 构造期 throw(EditMode 运行期),非构建期」 | 措辞登记 |
| F-J | 变异证明原件:本轮 `unity/Logs/mut-cheb.xml`(1 red)+ `mut-gate.xml`(1 red)落盘;历史轮 XML 保留策略登记为后续 | 原件落盘 |
| 主 PF-1 | `KindRouteTable.RegisteredSystems` **7 → 10 值**(补 `8/10/11`);新增 `test_dc5_registeredSystemSetIsNotNarrowerThanGddRuleOneTable` + 负夹具 | 生产 + 测试 |
| 主 PF-4 | **新增** `test_f44_edge_chebyshevWidensInt64BeforeAbs_intMinValueTrap` | 新测试 |
| 主 PF-6 | `SpyPerTickLatch` 度量改为 `(tick,poi)` **准入**数;补两 POI 边缘 | 度量订正 |
| 主 PF-5 | 违例表清单「行含 Player」**不可构造**,以 `(InteractableKind)99` 为最近形态(测试内已注) | 登记 |

---

## 三、验证命令(可证伪)

```bash
# 全量(修复轮后)
unity test unity --mode EditMode --filter "DaYiJingCheng.Tests.Interaction" \
  --output "$PWD/unity/Logs/interaction-s004-final2.xml"
# ⇒ total 86 / passed 83 / failed 0 / skipped 3(NOT-RUN 机检登记)

# 变异证明 A:丢掉 NeighbourhoodReporter.Chebyshev 的 int64 拓宽 ⇒ 恰 1 红(PF-4 夹具)
#   → unity/Logs/mut-cheb.xml: 82 passed / 1 failed
#   红点 = test_f44_edge_chebyshevWidensInt64BeforeAbs_intMinValueTrap

# 变异证明 B:删 F-B 门控调用方的 `if (!intent.Pressed) return 0;` ⇒ 恰 1 红
#   → unity/Logs/mut-gate.xml: 82 passed / 1 failed
#   红点 = test_ac413_negativeFixture_absenceOfIntentYieldsZeroEgress
```

**三条 NOT-RUN(skipped,机检)**
- `test_ac413_sixSideLatchIsNotRunBlockedBySystem6` — AC-4-13 6 半(6 的 latch/幂等)
- `test_f43_sameTickTwoClientsProduceTwoDistinctEgressesThatConvergeOnlyAtSystem6` — F-4.3 收敛半(6 幂等)
- `test_ac417_fourSideConsumesInjectedRadiusValue` 的 6 消费半(6 触发判距)

---

## 四、签字

| 面 | 原判定 | 修复后状态 |
| --- | --- | --- |
| 生产代码(4 侧形状) | 结构侧 APPROVED WITH SUGGESTIONS(无 BLOCKING) | ✅ 通过(仅 `KindRouteTable` 集扩至 10 值 —— PF-1) |
| AC-4-18 | 双方**clean** | ✅ 签绿 |
| F-4.3b | QA **clean** | ✅ 签绿 |
| AC-4-13 4 半(每帧照报 + 零记账) | QA 已签(有非空转证明) | ✅ 签绿 |
| **AC-4-13 6 半(每 tick ≤1 落流)** | QA **NOT SIGNED** | ⛔ **NOT-RUN / BLOCKED-BY 系统 6**(机检登记) |
| **AC-4-17 两消费者半** | QA **PARTIALLY SIGNED** | ⛔ **6 消费半 NOT-RUN / BLOCKED-BY 系统 6**;4 消费半 ✅ |
| F-4.4 有界性(最坏密度 + 边缘) | QA 已签 | ✅ 签绿 |
| F-4.4 帧率无关 | QA 死比较 | ✅ 重写为「计数 = 调用次数」的可判形态 |

**总判(修复后)**:story-004 的 **4 侧形状**全部签绿;**6 侧二子条**(AC-4-13 latch、AC-4-17 两消费者)
**显式 NOT-RUN**,不得借绿 —— 其转绿前提 = 系统 6 的 Epic 联调。
