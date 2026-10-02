# Story 001: 边界纪律与程序集归属 —— 零结算类型可达 / 零 `Append` 结构不可达 / 玩法数值隔离 / 无状态纯函数 / 不发档位

> **Epic**: 交互系统
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 6h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-09-28

## Context

**GDD**: `design/gdd/interaction-system.md`
**Requirement**: TR-interaction-001(4 = 纯目标选择器,输出 `(目标, 种类)`,零 gameplay 结算)· TR-interaction-006(4 不写三流)· TR-interaction-008(4 不发相机档位意图)· TR-interaction-012(4 无状态,清空重建结果相等)· TR-interaction-013(4 不接触玩法数值)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*
🔴 本故事是 Epic 的**边界件**,承载首轮评审的**根因改造**:原稿 `[A]` 类 AC 过半写「grep 某符号 = 零命中」,该判据形态有**假阳**(注释/字符串/夹具字面量都算命中)与**假阴**(改私有名或包进 `#if` 即静默绿)两缺陷 ⇒ **一律升为结构断言**(程序集引用集 / 类型反射闭包 / 运行时 spy)—— 这是 `AC-20-03`(ADR-020 §四:判据 = 反射断言不是 grep)在 4 的推广。另一处承重改判:**4 住边界层而非门 A**(Overview 首轮改判:归属判据 = 引用集/读侧 —— 4 读 13 的 `IPresentPatients`、42 的 `IModalState`、10 的 `Armed`,门 A 程序集引用不到任何一项;承 ADR-017 §二),因此 4 **继承 13 的同款三源豁免**:`AC-4-14` 据此收窄为「只覆盖纯选择函数」。

**ADR Governing Implementation**: ADR-005(输入是意图源,不直接驱动模拟 —— 规则一 = 同一裁决在选择层的落实)· ADR-021(POI 状态唯一写者 = 6;「自报 ≠ 写」⇒ `AC-4-02` 的「零 `Append` ≠ 零上行」)· ADR-020 §五(呈现层三件套同构:相机/UI/音频只读不持状态 —— 4 的 `AC-4-04` 是其在选择层的镜像)· ADR-013 §9 C3(42 只渲染 ⇒ 4 不得经 UI 面拿玩法数值)· ADR-017 §二(程序集归属判据 = 引用集 —— 改判的机械依据)· ADR-025(七装配清单;4 落 `Gameplay.Presentation` 族,未登记装配 = 构建失败)
**ADR Decision Summary**: ADR-005 裁"输入不驱动模拟"、ADR-021 裁"写者 = 6",两者都没给出**可执行的守门判据** —— 本故事交付判据本体:类型可达性反射闭包、`IEventSink` spy 计数、引用集交集断言、清空重建等值。全部数值留白 ⇒ 本故事只签**边界形状**,不签任何取值。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW(反射/AST/spy 全为纯 C# 判据,不触 post-cutoff API)
**Engine Notes**: EditMode 测试(UTF / NUnit)即可跑反射闭包与类型图扫描,无须 PlayMode。⚠️ 反射断言的**遍历闭包**须显式定义:公开类型的全部字段/参数/返回类型 + **递归展开泛型实参与被调类型的字段**(承 ADR-013 `PresentationDtoGuard` 「仅扫顶层会漏」的同款纪律)。`Gameplay.Presentation` 程序集(ADR-025)是断言对象锚点;测试装配 = `Gameplay.Tests`。

**Control Manifest Rules (this layer)**:
- Required: 主机唯一执行 `Step`/`CatchUp`,客户端**绝不写回流** — ADR-005(manifest Foundation「事件流与权威」)⇒ 4 的出境面只允许 `IDiscoveryReporter.Request`(story 004),`IEventSink` 在 4 内**结构不可达**
- Required: 程序集归属判据 = **引用集**(门 A `noEngineReferences: true` 恰 {BCL, `Sim.Contracts`})— ADR-017 §二 / ADR-025 §① ⇒ 4 判归边界层,继承 13 的三源豁免(manifest Feature「AI 分层确定性」13 例外注同型)
- Forbidden: **grep 某符号 = 零命中**作为 `[A]` 判据(假阳 + 假阴双缺陷)— GDD §AC 前言「首轮对判据形式的统一改造」;一律升结构断言(引用集 / 反射 / spy)
- Forbidden: `VitalsDto`(全案唯一浮点出口)或其字段类型出现在 4 的可达类型图中 — ADR-016 §六 / `AC-4-05`(规则十 = 驳回 `K_difficulty` 移交的守卫)
- Guardrail: 未登记的装配 = 构建失败(ADR-025 §④ 清单封闭性)—— 本故事的程序集锚点若与清单冲突,须回 ADR 轮,**不得在 story 内改判归属**

---

## Acceptance Criteria

*From GDD `design/gdd/interaction-system.md`, scoped to this story(判据正文照录,修订沿革见 GDD 原文):*

- [ ] **AC-4-01([A])** —— `GIVEN` 4 所在的**程序集**,`WHEN` 读取其编译引用集与**公开类型的全部字段 / 参数 / 返回类型**(反射,闭包递归),`THEN` **不含**任何结算侧类型:`CanCarry` 的宿主接口 / `F1` 结果类型 / `JudgeResult` / `treatable_by` 载荷 / 容量比较入参。**判据 = 类型可达性,不是符号 grep**(规则一)。**⚠️ 首轮改**:原稿 grep 五个标识符 ⇒ 换个私有名即静默通过
- [ ] **AC-4-02([A])** —— `GIVEN` 4 的代码路径,`WHEN` **运行时 spy 替换 `IEventSink`**(注入 recorder),`THEN` **调用计数 = 0**,且**反射断言** 4 的类型图中**不存在** `IEventSink` 字段或构造参(**零 `Append` ≠ 零上行** —— 出境走 `IDiscoveryReporter.Request`,见 AC-4-12)。**⚠️ 首轮改**:原稿 grep `IEventSink.Append` 会**漏掉经封装转发的写入**
- [ ] **AC-4-04([A])** —— `GIVEN` 清空 4 的实例并重建,`WHEN` 喂同一 `(玩家格序列, 世界状态前缀, 输入序列)`,`THEN` **选择结果序列相等**(`F-4.1` 的函数外延相同)。**措辞订正**:「逐位」一词**保留给 ADR-012 的字节对拍夹具**;本条断言**枚举值 / 整数 id 的相等**,非字节相等
- [ ] **AC-4-05([A])** —— `GIVEN` 4 的程序集,`WHEN` 反射断言其**引用集 ∩ {9 / 11 / 8 的 sim 侧类型} = ∅`,`THEN` 成立;并 `WHEN` 反射 `Candidate`,`THEN` **无** `VitalsDto` / `disease_id` / `tier_named` / `drug_profile` / `EnvMod` 类型的字段(规则十)。**⚠️ 本条是驳回 `K_difficulty` 移交的守卫**(规则十二)—— **若 11 的代案 (A) 被采纳,本条须随之重述**
- [ ] **AC-4-11([A])** —— `GIVEN` 4 的程序集,`WHEN` 反射断言**无** `ICameraRig` / 档位枚举类型的字段或调用,`THEN` **零**(规则六)。**⚠️ 若 `OQ-4-2` 裁「需要档位」,本条作废并须重立**(现取「不需要」)
- [ ] **AC-4-12([A],证据形式 `[I]`→`[A]`)** —— `GIVEN` **6 侧的 spy sink** + 4 的 `IDiscoveryReporter`,`WHEN` 驱动「玩家走进 POI 格但**无主动交互输入**」的**帧序列(≥ 3 帧、非整 tick 边界采样)**,`THEN` **零** `Request` 出境 ⇒ 进而**零** `PoiStateChanged`。**(触发 = 主动交互,非碰撞 —— `EC-6-2` 的承接面)**

---

## Implementation Notes

*Derived from 规则一(纯选择器)· 规则十/十一(玩法数值隔离)· Overview 边界层改判 · 首轮判据形式改造:*

- **反射闭包的精确形状(`AC-4-01/05/11` 共用一台机器)**:被测对象 = `Gameplay.Presentation` 内 4 的命名空间闭包;展开规则 = ① 编译引用集(asmdef `references` + IL `TypeRef` 表)② 公开类型的全部字段/属性/方法参数/返回类型,并**递归**进入泛型实参、嵌套类型、接口实现(承 `PresentationDtoGuard` 递归扫描先例);停机条件 = 已访问集防环。违规 = 可达集 ∩ 禁入类型集非空。**禁入集按类型全名注册于测试侧的登记表**(结算侧五类型 + `VitalsDto` 族 + `ICameraRig`/档位枚举),新增成员须同步 GDD 规则表,不得测试侧私加。
- **`AC-4-02` 双判据的两半**:spy 半边 = 注入 recorder 实现跑全套交互场景(story 002/003/004 的夹具复用),`Append` 计数 == 0;结构半边 = 反射断言 4 类型图中**无 `IEventSink` 字段/构造参/方法形参** —— 抓「经封装转发的写入」形态(GDD 点名原稿 grep 的漏网面)。⚠️ **出境走 `IDiscoveryReporter.Request` 不算违例**(AC-4-12/004 承接),注释写明两接口语义差异:请求 ≠ 写入。
- **`AC-4-04` 的实现前提**:4 的实例**零可变字段**(与 `AC-4-13` 共用反射断言:4 类型中无非 `readonly` 实例字段、无 `static` 可变状态);「清空重建」夹具 = `new` 第二实例、同输入序列重放、断言 `(Kind, StableId)` 序列逐元素相等。措辞纪律:**不得写「逐位」**——逐位属 ADR-012 字节对拍(GDD 措辞订正原文)。
- **`AC-4-05` 的引用集交集对象**:{9 `Sim` 侧类型, 11 处方类型, 8 判读类型}的**全名前缀集**登记于测试;交集非空即红。⚠️ 该条同时是**驳回 `K_difficulty` 移交**(规则十二用户裁定驳回,落 `OQ-11-13`/`OQ-11-11`)的机械守卫 —— 若日后 11 的代案 (A) 被采纳,本条重述义务在 GDD 侧,不在实现期私改。
- **`AC-4-11` 的「或调用」半边**:除字段/类型可达外,IL 调用表(callvirt/callnewobj 目标)`∩` `ICameraRig.*` = ∅;`OQ-4-2`(是否发档位)现裁「不需要」⇒ 本条成立;若改判 ⇒ 本条**作废并须重立**(GDD 原文,登记于故事尾注)。
- **`AC-4-12` 的 EditMode 形态**(首轮 `[I]`→`[A]` 改判):**fake `ITickProvider`** 驱动 ≥ 3 帧、采样点**故意落在非整 tick 边界**(GDD 点名的夹具形态 —— 抓「实现把走进格当边沿事件」时帧相位敏感性);玩家走进 POI 格但无输入 ⇒ spy 计数 0。6 侧 spy sink = 对 `IDiscoveryReporter` 的 recorder 替身(story 004 交付真实链路,本故事只交付 spy 面)。
- **归属注记(非判据)**:4 住边界层的完整论证在 GDD Overview(读集含 `IPresentPatients`/`IModalState`/`Armed`);本故事只在测试装配注释登记该归属 + 三源豁免推论(`AC-4-14` 收窄的依据归 story 003)。程序集若未建 ⇒ 依 ADR-025 §④,未登记 asmdef = 构建失败,本故事同时是该装配的**存在性验收点**。
- **规则十二/`OQ-4-18`**:失败反馈外抛(P1b)不在本故事;规则十一的 `OQ-4-10` 上行缺口在 story 005 登记面。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 002:`F-4.1` 三键全序本体与等距决胜(`AC-4-06/14/16`)—— 本故事只交付「4 是一台纯函数机器」的边界,不交付机器数学
- Story 003:`Candidate` 与四源 DTO 的反射类型闭包(`AC-4-03` 的具名产物半边)、经流确立格 vs `pending_cell`(`AC-4-20`)、`BakedInitial` 第四源(`AC-4-22`)
- Story 004:`IDiscoveryReporter` 的真实链路、latch 归 6、有界性、`R_INTERACT` 单源(`AC-4-13/17/18`)—— 本故事只有该接口的 spy 替身
- Story 005:`IModalState.Modal` 读取面(`AC-4-09`)、`Armed` 压制(`AC-4-10`)、`Acquire/Release(Self)`(`AC-4-19`)、上行缺口登记
- Story 006:`4-DC-1…6` 构建期校验与 `[L]` 走查面
- 系统 6 / 23 / 13 / 42 / 10:各拥有方的结算与状态本体 —— 4 永远只路由;`EC-6-2` 的答案(6 侧接受主动交互口径)已 ⭑ 结案,实现归 6
- 任何数值取值(`R_INTERACT` / `KindPriority` / 时长)—— **归用户**(数值冻结纪律)

---

## QA Test Cases

*Written at story creation(lean mode — QL-STORY-READY skipped;specs self-authored from AC text). The developer implements against these — do not invent new test cases during implementation.*

- **AC-4-01 / AC-4-05 / AC-4-11**: 三合一可达集扫描(结构断言)。
  - Given: `Gameplay.Presentation` 程序集加载入测试域;禁入类型登记表(结算侧五类型 + `VitalsDto`/`disease_id`/`tier_named`/`drug_profile`/`EnvMod` + `ICameraRig`/档位枚举)。
  - When: 反射闭包(引用集 + 字段/参数/返回类型递归展开,泛型实参入闭包)+ IL 调用表扫描。
  - Then: 可达集 ∩ 禁入集 == ∅;`AC-4-05` 的引用集 ∩ {9,11,8} == ∅;`AC-4-11` 的调用目标 ∩ `ICameraRig.*` == ∅。
  - Edge cases: **私有字段不在判据面**(GDD 原文判据 = 公开类型的字段/参数/返回 —— 私有名换掉不静默通过的改判逻辑已由类型可达承担,注释写明此边界);泛型包装(`List<VitalsDto>`、`Func<int, JudgeResult>`)须被闭包展开抓到;`#if` 包裹的死代码在 IL 层不可见 ⇒ 引用集半边兜住。
  - Negative fixture: 测试侧构造含禁入字段的影子类型注入扫描器 ⇒ 红且点名类型(证明扫描器非空转)。

- **AC-4-02**: spy + 结构双判据。
  - Given: recorder 型 `IEventSink` 替身;全套交互场景驱动(拾取意图、对病人按交互、对 POI 按交互)。
  - When: 跑场景,读 recorder 计数;反射 4 类型图。
  - Then: `Append` 计数 == 0;4 类型中无 `IEventSink` 字段/构造参/形参;`IDiscoveryReporter.Request` 的出现**不算违例**(注释声明两接口语义分界)。
  - Edge cases: 「经封装转发」形态(4 持一个内部 `WriteWrapper`,wrapper 引 `IEventSink`)⇒ 结构半边红(wrapper 的构造参是可达字段);反向确认计数与结构两半**独立**(摘掉结构违规只靠 spy 计数抓,反之亦然 —— 两夹具各证一半)。
  - Negative fixture: 上述 wrapper 转发形态。

- **AC-4-04**: 清空重建等值。
  - Given: 固定输入三元组(玩家格序列 / 世界状态前缀 / 输入序列,由 story 002 夹具源供给,出处标注纪律见 `AC-4-16` 归 002)。
  - When: 实例 A 跑一遍 → 弃置 → `new` 实例 B 跑同输入。
  - Then: `(Kind, StableId)` 选择序列逐元素相等(整数/枚举比较,**不断言字节**);反射:4 无非 `readonly` 实例字段、无 `static` 可变状态。
  - Edge cases: 实例 A 与 B 交错运行(证无隐藏静态);哈希序干扰(`Dictionary` 遍历源)⇒ 序列稳定即绿(全序本体在 `AC-4-06` 归 002,本条只验无实例态泄漏)。
  - Negative fixture: 加一个 `_lastTarget` 缓存字段 ⇒ 双红(可变字段断言 + 交错序列分叉)。

- **AC-4-12**: 走进 ≠ 交互(EditMode,fake tick)。
  - Given: 6 侧 spy sink;POI 格布局夹具;fake `ITickProvider`。
  - When: 玩家格序列走进 POI 格,采样 ≥ 3 帧且**落在非整 tick 边界**;全程无 `InteractIntent`。
  - Then: `Request` 出境计数 == 0 ⇒ spy 断言 6 侧零 `PoiStateChanged` 写入压力。
  - Edge cases: 恰在 tick 边界走进(GDD 点名的另一采样形态)同样 0;走进+走出一 tick 内往返(边沿事件实现的诱饵);同帧先后 `ActorCellEntered` 两 POI 格 ⇒ 仍 0。
  - Negative fixture: 「碰撞即 Request」实现 ⇒ 红(这正是 `EC-6-2` 承接面的可执行形态)。

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `tests/unit/interaction/boundary_discipline_test.cs` — must exist and pass(AC-4-01/02/04/05/11/12 六条结构断言 + 负夹具;EditMode)

**Status**: [ ] Pending — story not yet implemented(真身落点预期 = `unity/Assets/Tests/EditMode/Interaction/`;登记口径 = `tests/unit/interaction/`;CI 载体归 ADR-012 轮)
⚠️ 不得借绿:本故事六条 AC 均为形状判据,**无外部前置**,可实现即签;`AC-4-11` 的存续前提是 `OQ-4-2` 现裁「不发档位」——若该 OQ 改判,本条作废重立(GDD 原文,不得静默保留旧判据)。禁入类型登记表若缺项 ⇒ 扫描器空转假绿,负夹具是反空转的唯一防线(交付时须附负夹具红态截图/日志)。

---

## Dependencies

- Depends on: ADR-025 装配清单(`Gameplay.Presentation` 存在性;4 的归属注记)/ ADR-013 §十 Amendment A(`IDiscoveryReporter` 消费面不依赖,但 spy 替身的接口签名引用 6 侧契约 —— 该契约已在 ADR-021 登记形状)
- Unlocks: Story 002(纯函数机器边界成立后,全序数学才有被测对象)/ Story 003(`Candidate` 具名产物在本故事扫描器注册表锚定)/ Story 004(spy sink 复用本故事替身)

---

## Completion Notes

**Completed**: _待实现_
**Criteria**: _待填_(交付时须附:禁入类型登记表全文 + 负夹具红态输出 + 「零 `Append` ≠ 零上行」的注释位置)
**Deviations**: _待填_
**Test Evidence**: _待填_
**Code Review**: _待填_
**Manifest**: 版本号已对齐 2026-10-02(⚠️ **仅版本号** —— 抽象点计数订正另立批次,见 control-manifest §传播范围)
