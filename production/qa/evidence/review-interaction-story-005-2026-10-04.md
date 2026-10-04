# 评审原件 —— interaction-system story-005(模态门路由)

- **对象**:`unity/Assets/Gameplay.Presentation/Interaction/ModalGate.cs`(生产)· `unity/Assets/Tests/EditMode/Interaction/modal_gate_test.cs`(测试)
- **权威**:GDD `design/gdd/interaction-system.md` 规则七/八/九 · F-4.2 · AC-4-09/10/19;ADR-013 §十 Amendment A/B;ADR-025 §①;ADR-020
- **轮次**:**单轮**(承用户「评审只做一轮」)
- **日期**:2026-10-04
- **方法**:双代理并行(结构侧 = lead-programmer · QA 侧 = qa-lead)+ 主会话独立复核

---

## 一、原判定

### 结构侧(lead-programmer)—— **CHANGES REQUIRED**

| id | 严重度 | 判定 | 落点 | 修复 |
| --- | --- | --- | --- | --- |
| F-1 | **BLOCKING** | 「丢弃而非排队」测空转:原测在 `NotPressed()` 上断言,`InteractionSelector.Select` 自带 `!intent.Pressed ⇒ None` 首门 ⇒ 排队实现亦可绿 | 原 `:170-193` | **已修**:改用注入 `CountingReporter` 的选择器,**观测选择器「真跑到自报步」的次数**(`InteractionSelector.cs:105-108` 发 `Request`);门闭 ⇒ 0 次,关闸新按 ⇒ 恰 1 次。MUT-B 证可红 |
| F-2 | MAJOR | `ModalOrdinal` 是生产调用面上的死代码(零读者) | `ModalGate.cs:52` | **已修**:从 `IModalGateState` **删除** ordinal getter ⇒ 契约面只剩一个布尔 |
| F-3 | MAJOR | 扫描器成员清单**手抄**闭集 = 犯其所禁令;闭集增员则静默失覆盖 | `:407-411` | **已修**:改类级 `ClosedSetMembers` + **机械基数守卫** `CountModalIdMembers`(读 `ModalId.cs` 真源数成员,与清单同基数)。MUT-D 证可红 |
| F-4 | MAJOR | 单 bool 负夹具断言在**局部变量**上(自证空转) | `:300-323` | **已修**:引入**可执行影子** `SingleBoolSuppression`,真走 Acquire/Release,证单 bool 确丢 10 的位;与生产 `MotorLease` 同序列对照。MUT-C 证可红 |
| F-5 | MAJOR | `IArmedState` 无常驻实现体(10 侧 `ModalPhaseEvaluator` 无 `Armed` 态) | — | **登记 NOT-RUN**:方向① 的实现体归 10,本故事不产;见下「未闭登记」 |
| F-6 | MAJOR | doc 注释里 `IModalState` 的 `<see cref>` 与「Presentation 不引用 UI」措辞不符 | `ModalGate.cs:31` | **已修**:cref 改 `<c>`,补「反转配对」说明 |
| F-7 | MINOR | `ScanForMutableState` 是第 4 份手抄,「逐字同式」claim 不字面成立 | `:374-393` | 保留(行为与 story-001 一致);措辞已注明 |
| F-8 | MINOR | 扫描器白名单 `ModalId.None` 实为「复制」的漏网形态 | `:343` | **已修**:加「4 侧生产代码**零出现 `ModalId` 类型名**」断言。MUT-E2 证可红 |
| F-9 | MINOR | ordinal 循环的反空转门 `leaked` 量的是枚举基数,非循环覆盖 | `:151-158` | **已修**:循环前加正向对照(`Open=false ⇒ 必通过`),任一 `IsOpen` 门删即红 |
| F-10 | MINOR | `SetMotorSuppression` 落点与 1 侧白名单「碰巧同装配」 | `:136-144` | 保留;doc 已挂 AC-1-23 |
| F-11 | INFO | `Accept(in InteractIntent)` 不用其参数 | `:95-105` | 保留(AC 是纯门输入谓词);F-1 修复后其非用不再掩盖缺陷 |

### QA 侧(qa-lead)—— **ACCEPT-WITH-FIXES**

| id | 严重度 | 判定 | 落点 | 修复 |
| --- | --- | --- | --- | --- |
| F1 | MAJOR | `singleBoolWriterLosesUpdate` 前半断言在测试局部 bool 上(半空转) | `:305-308` | **已修**(同结构 F-4) |
| F2 | MAJOR | AC-4-09「新增第 8 屏 ⇒ 4 侧零改动」**动态半边**只论证未执行 | `:218-229` | **部分**:结构半边(F-8 零出现 + F-3 基数守卫)已强化;动态半边 = 结构论证(方向:4 侧根本够不着 `ModalId`,MUT-E 编译错实证)。登记为**结构形态承担** |
| F3 | MINOR | ordinal 循环装饰性(生产不读 `ModalOrdinal`) | `:152-158` | **已修**:随 F-2 删 ordinal,循环退役为单断言 + 正向对照 |
| F4 | MINOR | AC-4-10「3 侧零状态」是散文非验证 | `:124-137` | **登记 NOT-RUN**:3 侧扫描归 story-006 边界故事 |
| F5 | INFO | 两门接口皆对**替身**测,无生产实现体 | — | **登记 NOT-RUN**(见下) |
| F6 | INFO | `ModalGate` 零生产调用者 | — | **登记**:本故事 = 接缝故事(seam),消费方归后续 |

### 主会话独立复核(自证轮)—— 见 `unity/Logs/.s005-review-notes.md`

| 编号 | 发现 | 处置 |
| --- | --- | --- |
| 自证 #1 | AC-4-09 正测曾**空转假绿**(`TestContext.TestDirectory` 指向 `Library/ScriptAssemblies`,两候选源路径皆不存在 ⇒ 扫 0 文件) | **已修**:`ReadInteractionSources` 主锚 `Directory.GetCurrentDirectory()`(= `unity/`)+ 反空转门(≥1 源文件且含 `ModalGate.cs`) |
| 自证 #2 | 增补**代码级**零出现判据(剥注释后 4 侧生产不含 `ModalId`) | 已落(F-8) |
| 自证 #3 | `ScanForMutableState` 曾折叠两条等价 readonly 分支 | 已逐字对齐 story-001 |

---

## 二、变异证明(全部落盘 `unity/Logs/`,生产已还原 `a95deb8d`)

| 变异 | 结果 | 红点 |
| --- | --- | --- |
| MUT-A:删方向②(模态门) | 恰 2 红 | `test_ac409_anyModalOpenDiscardsIntent` + `discardIsNotQueue`(**证 F-1 接缝真能红**) |
| MUT-B:排队替代丢弃(缓存被拒意图) | 恰 2 红 | `discardIsNotQueue` + `rule9_modalGateHasNoMutableState`(**双守卫**) |
| MUT-C:`Release` 清全位图(单 bool 语义) | 恰 3 红 | 全 AC-4-19(含 `singleBoolWriterLosesUpdate` —— **证 F-4 修复真能红**) |
| MUT-D:`ModalId` 增第 8 员 | 恰 1 红 | `test_ac409_fourSideDoesNotCopyModalIdMemberList`(**证 F-3 基数守卫真能红**) |
| MUT-E:生产**代码级**引用 `ModalId` 类型 | 编译错(CS0234) | **方向性实证** —— 4 侧程序集**够不着** `ModalId`(AC-4-09 最强形态,构建期即拒) |
| MUT-E2:生产代码级字符串 `"ModalId"`(可编译) | 恰 3 红 | 含 `fourSideDoesNotCopyModalIdMemberList`(**证 F-8 零出现断言真能红**) |

---

## 三、实跑基线

- `unity/Logs/interaction-s005-final.xml` = **101 total / 98 passed / 0 failed / 3 skipped**
- 3 skipped = story-004 的三条 NOT-RUN(`Assert.Ignore` 机检),非本故事失败

---

## 四、未闭登记(NOT-RUN —— 禁借绿)

| 项 | 内容 | 归属 |
| --- | --- | --- |
| NR-1 | 方向① 的**生产实现体**(`IArmedState`)—— 10 急救动作侧落地 | 系统 10 GDD 轮 |
| NR-2 | 方向② 的**适配器**(42 `IModalState` → `IModalGateState`) | 系统 42 装配 |
| NR-3 | AC-4-10「**3 侧零状态**」扫描(3 侧命名空间零焦点态字段) | story-006 边界故事 |
| NR-4 | 「新增第 8 屏 ⇒ 4 侧零改动」的**动态**半边(模拟加员) | 由**结构形态**承担(方向 + F-8 零出现);动态夹具待闭集真增员时自然获得 |
| NR-5 | `ModalGate` 的**运行期消费者**(接缝未接线) | 后续系统装配故事 |

---

## 五、签字

| AC | 判定 | 依据 |
| --- | --- | --- |
| **AC-4-09**(引用而非复制 + 丢弃而非排队) | ✅ **SIGN** | 生产 `ModalGate.cs:98/102/122` 由真测驱动;丢弃接缝由 MUT-B 证可红;零出现结构半边由 MUT-E/E2 证;基数守卫由 MUT-D 证 |
| **AC-4-10**(Armed ⇒ 压制) | ✅ **SIGN**(4 侧形态) | MUT-A 证可红;3 侧半边 NOT-RUN(归 story-006) |
| **AC-4-19**(per-source 位图) | ✅ **SIGN** | 经真调用面 `SetMotorSuppression` 对真 `MotorLease`;MUT-C 三红 |

**综合判定:APPROVED**(修复轮已闭;单轮评审无复审)。

- 结构侧原判 **CHANGES REQUIRED** ⇒ F-1(BLOCKING)已修 + 变异证明可红 ⇒ 解除
- QA 侧原判 **ACCEPT-WITH-FIXES** ⇒ F1/F3 已修;F2 部分(结构形态);F4/F5/F6 登记 NOT-RUN
