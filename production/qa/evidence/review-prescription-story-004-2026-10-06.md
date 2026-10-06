# 评审报告原件 — prescription-medication story-004(Prescribe 五步流程)

**日期**: 2026-10-06
**评审对象**: `unity/Assets/Sim/Prescription/PrescribeFlow.cs` + `unity/Assets/Tests/EditMode/PrescriptionMedication/prescribe_flow_test.cs`
**评审轮次**: 单轮(承「评审只做一轮」协议)

## 交付物

### 生产代码
- `unity/Assets/Sim/Prescription/PrescribeFlow.cs` — 五步编排(域检查 → F-11.1/F-11.2 求值 → 20 扣减 → 发事件 → 成长门)
- `unity/Assets/Sim/Prescription/PrescribeFlow.cs.meta`

### 测试代码
- `unity/Assets/Tests/EditMode/PrescriptionMedication/prescribe_flow_test.cs` — **53 条测试**
- `unity/Assets/Tests/EditMode/PrescriptionMedication/prescribe_flow_test.cs.meta`

## 测试结果

- **filter(评审前)**: `unity/Logs/prescribe_flow_3.xml` = **41 / 41 passed / 0 failed**
- **filter(修复后)**: `unity/Logs/prescribe_flow_5.xml` = **53 / 53 passed / 0 failed**
- **PrescriptionMedication 全目录(修复后)**: `unity/Logs/prescribe_flow_all4.xml` = **103 / 103 passed / 0 failed**
- **全量 EditMode(修复后)**: `unity/Logs/prescribe_flow_full5.xml` = **2826 passed / 0 failed / 46 skipped / 1 inconclusive**(exit 8 源自既有 Skipped/Inconclusive,非失败)

## 评审结果

### 结构侧评审(unity-specialist)

**判定**: **APPROVED WITH SUGGESTIONS**(0 BLOCKING · **1 MAJOR** · 6 MINOR · 4 NIT)

> 核心契约面逐条实测通过:五步顺序、AC-11-04 零副作用、AC-11-10 零乘子、AC-11-17 整剂路径、
> ADR-029 §③ 唯一构造路径、门 A 引用集、混堆标量端口。唯一 MAJOR 是**文档口径分歧**,非代码 bug。

### QA 侧评审(qa-lead)

**判定**: **CHANGES REQUIRED**(0 BLOCKING · **4 MAJOR** · 6 MINOR · 5 NIT)

> 核心产出 = 变异测试清单:「把生产件里对应的一行改坏,这个测试会红吗?」—— M1/M2/M3/M4 四条均为
> **现有测试抓不住的变异**(恒真断言 / 无正控 / 实参无界)。

## 修复记录(结构侧)

| # | 问题 | 落点 |
|---|------|------|
| **M1** | AC-11-05③(GDD `:942`)与 Edge Case(`:645`)写「给错药 ⇒ `SkillGrown` 零发出」,与 F-11.5 **BL-3 改判后的现口径**(门只读 11 自持量,**不读 `treatable_by`**)直接冲突 ⇒ 逼着一条**结构上不可满足**的验收条 | **零代码改动**,收口文档:GDD `:942` / `:645` + story 卡 `:73` 三处陈旧字面订正为「**成长照发**」并注明 BL-3 改判依据(与文件自身「订正」惯例一致;代码行为 = F-11.5 权威件口径,判为正确) |
| **m1** | `IPortionsConversion.PortionsFor(itemKey, dose)` 把 `dose × portions_per_dose` 推到 11 之外,与 GDD `:408`「求值在 11」冲突;11 的 `portions < 1` 下界守卫由**实现方契约**提供,本侧无法证 | 端口改 `int PortionsPerDose(ItemKey)`(**只查表**);乘法移入 `PrescribeFlow` 步骤①(宽算防溢出 + 下界/上界守卫) |
| **m2** | 两处**近恒真反射断言**:`noSecondMultiplier_symbols` 比对 9 个**固定类型名**;`noDiseaseNameFields_inSignature` 用**手写类型数组** —— 对所列类型恒真 | 改枚举 11 自声明**全部成员符号名**(类型/方法/属性/字段/参数)做差集 + 反向守卫(枚举面非空);第二处改从 `typeof(PrescribeRequest).GetConstructors()[0]` **真取形参** |
| **m3** | `test_kDifficulty_doesNotReadSeverity` 只扫**形参类型名**,弱 | 改为形参名 + 返回/形参类型**递归可达**的全部类型名(含数组元素 / 泛型实参,承 AC-11-14 递归反射先例)+ 源码面零 `severity`/`Severity` |
| **m4** | `PrescribePorts` 不校验 `doseBase > 0` ⇒ 误配 0 时错误被推迟到**步骤⑤**(事件**已进流**),装配错误伪装成「缺参不回滚」 | 构造体加 `if (doseBase <= 0) throw`(与 `DoseCalculator.cs:49-51` 同款);补 2 条装配期负测 |
| **m5** | `PrescribeOutcome` remarks 称客户端「**产出上行意图**」,实际回执**不含任何可传输对象**(`TreatmentEvent` 恒 null) | remarks 改为「本地编排 + 返回 `Applied/GateHit/Portions` 回执;上行意图物化归 45(BLOCKED)」 |
| **m6** | 混堆两条测试对「确定性归属」**过度声明**(桩恒返常量 ⇒ `sameScalar` 不可能失败) | 注释收窄为「11 侧仅保证**接口面不传序列**;集合选择确定性归 20」;`sameScalar` 测试标注为**文档性,非判据** |
| **n1** | 主机成功路径未断言 `TreatmentEvent != null` | `test_prescribe_exactStock_accepted` 补该断言 |
| **n2** | 载荷 `seq: 0` 占位无测试锁定 | `payloadSevenFields_encoded` 显式断言 `Seq == 0` 并加注「占位,归 45/7a」 |
| **n4** | 两处源码扫描剥离策略不一致(一处剥字符串、一处剥注释) | `StripComments` 统一为**注释与字符串皆剥**(判据更强:字符串里的 `"new PayloadRef("` 也不再误命中),并加注与 `PrescriptionFloatScan` 的判据面等价性 |
| **n3** | `HasPortions` 与 `ConsumePortions` 之间的 TOCTOU 窗口 | **接受不修**(P0 单机主机无害;靠 ③ 原子失败整体拒绝兜底);归 20 侧契约 |

## 修复记录(QA 侧)

| # | 问题 | 落点 |
|---|------|------|
| **M1** | `noSecondMultiplier_symbols` 是**恒真断言**(镜像测试自己写的类型表) | 同结构侧 m2 —— 改真程序集符号面扫描 |
| **M2** | `noDiseaseNameFields_inSignature` 反射半边**恒真**(未反射真实签名) | 同结构侧 m2 —— 改真构造签名反射 |
| **M3** | 零浮点扫描**无正控**(self-guarding gate):`Scan` 若被改成恒空,story-002/003/004 **三处共享断言全部真空通过** | 新增 `test_prescribe_noFloat_scanHasPositiveControl`:临时目录写已知浮点片段 ⇒ 断言扫描器**能返回非空**且 `type` / `literal` 两路都命中 |
| **M4** | `HasPortions` 实参**从未被有界验证**:全部用例 `PerDose = 1` ⇒ `portions == dose`,把实参误写成 `effectiveDose` 的变异**不会被任何测试抓到**(落在 AC-11-04 BLOCKING 路径) | `StubStore` 记录 `LastHasPortions`;新增 2 条(`PerDose=5, dose=2` × {Available=10 ⇒ 接受并断言实参 **10**,Available=9 ⇒ 拒绝}) |
| **m1** | `noHandRolledPayloadRef` 无正控 + 无「扫到 0 文件」守卫 | 加 `Assert.IsNotEmpty(files)` + 断言 `PrescribeFlow.cs` 在扫描集内 + 剥离 helper 正控 |
| **m2** | `EvaluateGateHit` 的 `doseLegal == false` 分支**永不执行**(集成路径被步骤① 短路) | 新增 `test_prescribe_gateHit_doseOutOfRangeIsFalse`(越界 ⇒ false;边界值 min/max ⇒ true) |
| **m3** | 五处空值守卫**无任何测试**触碰 | 新增 5 条负测(`Prescribe` 的 ports/sink/encoder · `EvaluateGateHit` 的 skills · `PrescribePorts` 四端口) |
| **m4** | 测试侧 `StripComments` 不剥字符串 ⇒ 两条**否定**源码断言有假绿风险 | 同结构侧 n4 —— 二者皆剥 + 3 条正控(注释须被剥、代码 token 须保留) |
| **m5** | `sameScalar_samePayload` 是**确定性同义反复**(同输入跑两遍) | 改名 `test_prescribe_mixedStack_deterministic_sameInput`(名副其实)+ 标注文档性 |
| **m6** | 同 tick 双剂「**Seq 各不同**」既未实现也未声明 NOT-RUN | 文件头 NOT-RUN 由 5 处扩至 **6 处**,显式登记「发号权在主机 Append,归 45/7a」;测试内加注不得由测名暗示已覆盖 |
| **n1/n2/n5** | 近恒真用例 / 注释强于断言 / 浮点正则误报行尾注释 | n1 加注为「填了字段的常规路径照常」;n5 **接受不修**(false positive 比 false negative 安全,与 story-003 m5/m6 同裁) |

## 变异证明(修复后补测的证伪力)

| 变异 | 描述 | 结果 |
|------|------|------|
| **MUT-B** | 删 `PrescribePorts` 的 `doseBase <= 0` 装配期守卫 | **2 红**(两条装配期负测) |
| **MUT-C** | 在生产件声明被禁符号 `SkillMul` | **2 红**(反射符号面 + 源码面)—— 证 m2/M1 修法**生效**(原稿恒真的反射断言现在真能抓到) |
| **MUT-D** | 移除 11 侧的 `dose × per-dose` 乘法 | **7 红** —— 证 m1 修法生效(乘法确实住在 11 且被覆盖) |
| **MUT-E** | `HasPortions` 实参误传 `effectiveDose` 而非 `portions` | **2 红**(QA M4 的两条新测)—— 原稿 **0 红** |
| **MUT-F** | 零浮点扫描器改成恒返回空 | **1 红**(QA M3 的正控)—— 原稿 **0 红**(三处共享断言全部真空绿) |

> 命令模板:
> `unity test unity --mode EditMode --filter "DaYiJingCheng.Tests.PrescriptionMedication.PrescribeFlowTest" --output unity/Logs/<name>.xml`
> 变异运行日志:`prescribe_flow_mut.xml`(MUT-B/C)· `prescribe_flow_mutd.xml`(MUT-D)· `prescribe_flow_mutef.xml`(MUT-E/F);
> 变异后均以 `diff` 验证**干净回滚**。

## NOT-RUN 登记(禁借绿 —— 覆盖缺口,非安全洞)

测试文件头显式登记 **6 处**,全部保持未勾:

1. **AC-11-16 正式对拍** — 契约三方(甲 入口登记 / 乙 `OQ-11-13` 已裁 / 丙 签名登记)均已闭合,但 30 侧 `EmitGrowth` **实现未落地** ⇒ 理由已从「契约缺失」变为「测试未跑」;**BLOCKED-BY-30 实现**。
2. **AC-11-15 三格子句** — BLOCKED-BY-ADR-012(CI 矩阵未激活);本文件只证 Mono 侧自洽。
3. **换算表真源** — 21a 未落 C# 字段(`DrugProfile.cs` 无 `portions_per_dose`,实测确认);本 story 以**影子 schema** 端口走通,真实产出方落位后仅换实现;**BLOCKED-BY-OQ-11-10 产出方**。
4. **省料数值** — 21a EFF 唯一出处 + 30 拥映射,值归数值轮(BL-6)⇒ 夹具用合成表。
5. **非主机传输半边** — BLOCKED-BY-45(网络 epic);本文件只证本地零 Append 分支。
6. **同 tick 两剂「各得不同 Seq」** — 生产件两笔同 tick 事件均写 `Seq = 0` 占位(与 10 的 `HostEmergencyProcessor` 同现状),发号权在主机 `Append` ⇒ 归 45 / 7a。

## 跨故事缺口(本故事范围外,但影响判据完整性)

- **story-003 卡要求的 `drug_event_test.cs` 全库不存在**(QA 评审实测 `find` 无结果)⇒ AC-11-01①(递归签名)/ AC-11-22(写者独占 IL 扫描)/ AC-11-10(assembly 引用集)**三处 BLOCKING 目前无真判据**;story-004 测试中对应的「流程侧」断言**不能顶替**。建议在 story-003 收口前不把这三条 AC 记为已有证据。
- **AC-11-22(写者独占)** 未在本 story 测试面出现,故事卡 `Out of Scope` 未列、`Test Evidence` 未提 —— 建议 epic 收口时确认承担方,别让它落空。

## 亮点(应保留)

- 五步**行为断言扎实**:边界值(dose 恰为 min/max)、失败分支(缺货 / 扣减失败 / 离场 / 越界)、`ConsumeFails → 零事件`、客户端零 Append/零扣减/零成长、`qualityAlwaysFromStore_notFromRequest`(真源隔离,可证伪)、`emptyDoseRange` 旁路 F-11.1、`portSeamExposesNoSequence`(**真反射**,可证伪)、`kDifficulty_derivedFromDrugPotency`(钉死「域保持除法 vs raw 整数除法」)。
- 测试对「11 越权」的 **scoped 化处理是有意的、正确的**:`noSecondMultiplier_symbols` 与 `noHandRolledPayloadRef` 都显式把扫描面收在 `Sim/Prescription/`,并注明「撒到全 `Sim/` 会命中 10 的 `HostEmergencyProcessor` —— 那不是 11 越权」。与 ADR-029 §③ / ADR-024 的「逐系统 scoped 断言」一致,可作其他系统写同类断言的模板。
- 两处证据边界声明诚实:文件头 NOT-RUN 与故事卡 `Test Evidence` 一致,**未见借绿**。
