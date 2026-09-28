# Story 002: 确定性全序目标选择 —— F-4.1 三键 / `d∞` int64 加宽 / 等距必有唯一胜者 / 夹具出处纪律

> **Epic**: 交互系统
> **Status**: Ready
> **Layer**: Core
> **Type**: Logic
> **Estimate**: 6h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-28

## Context

**GDD**: `design/gdd/interaction-system.md`
**Requirement**: TR-interaction-004(确定性全序三键,禁遍历序/哈希序决胜)· TR-interaction-005(`d∞` 切比雪夫纯整数,与 1 格语义同源)· TR-interaction-014(四级消歧 + `KindPriority` 全序烘焙表 —— ⚠️ registry 态 **partial**:消歧顺序/取值属 GDD 内部 schema 无 ADR 承接半边,本故事交付形状不得记该条转绿)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*
🔴 本故事是 4 的心脏:`F-4.1` 的三键全序 `⟨d∞, KindPriority, StableId⟩`。两处承重改判在此落地:① **`d∞` 升 int64** —— 原稿 i32 下 `dx = int.MinValue` 的 `Math.Abs` 回绕为负 ⇒ 「距离」变负数、排序静默反转(三审订正:先转 long 再取绝对值);② **判定式无朝向项** —— 「身位即光标」,目光可跟随呈现但**不得暗示参与判定**(欺骗性反馈红线,AC-4-07 的 `[L]` 面归 story 006)。

**ADR Governing Implementation**: ADR-015 §三(单一整数格:`d∞` 与 1 的跨格度量同源,`R_INTERACT` 单位 = 格数;上界三审订正 `min(W,H,D)−1` —— `max` 方向放过 Tuning 表要防的「半径大过世界格」失效)· ADR-006(`StableId` 标量 = int64;整数域纪律;「逐位」措辞保留给 ADR-012 —— `AC-4-04` 已钉)· ADR-014(`KindPriority` 十项来自 `interaction_kinds.json` 烘焙表,JSON 数值写字符串经 `FixParse`/整数解析,禁 `JsonConvert.DeserializeObject<T>`)· ADR-012(三格重放的夹具纪律 —— `AC-4-14` 的载体;IL2CPP 逐位属该轮,本故事只交付纯 C# 整数算法)
**ADR Decision Summary**: ADR-015 只裁「世界格共用」,ADR-006 只裁「整数域 + 单一舍入」—— **平局如何决胜从未在 ADR 层裁决**,这是 GDD `F-4.1` 的交付面:任何两个候选必可比、可比必有唯一胜者(反对称 + 完全 + 传递由三键字典序构造性成立)。本故事交付该算法与其穷举验证;`KindPriority` 的**具体序**归用户(Tuning 表)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW(纯整数算法,零引擎 API;`int64` 回绕在 IL2CPP C++ 侧为 UB 属 ADR-012 F7 面 —— 本算法**无乘法溢出路径**(三差分取 max 全程 int64),F7 风险结构性不在本故事,注释声明)
**Engine Notes**: `Math.Abs(int)` 在 `int.MinValue` 上回绕是**已知 BCL 陷阱**(GDD 三审点名)⇒ 实现必须先 `long dx = (long)a.x - b.x` 再 `Math.Abs(dx)`;EditMode 穷举夹具直接以 `(int.MinValue, 某格)` 对拍。测试夹具跑 Mono 即可;跨平台逐位半边归 `AC-4-14` 的 ADR-012 三格(载体未建 ⇒ 该半边 BLOCKED-BY,见 Test Evidence)。

**Control Manifest Rules (this layer)**:
- Required: 空间距离在**整数格域**求值,`d∞` 与系统 1 的跨格度量同一格语义 — ADR-015 §三(manifest Core「世界几何与格」:空间位置一律整数格,亚格精度用整数细分不引入 `Fix`)
- Required: 事件/比较的**确定性全序**由显式键构造(禁依赖 `GetHashCode()` 序、容器遍历序)— manifest Foundation「`PatientId` 持久化与全序走 `Value` 不用 `GetHashCode()`」同型纪律(ADR-007 §Key Interfaces)
- Forbidden: 判定式引入**朝向/视线项**(目光不参与判定 —— 「身位即光标」是规则,不是实现细节)— GDD `F-4.1` + `AC-4-07` 呈现面
- Forbidden: `JsonConvert.DeserializeObject<T>` 读 `KindPriority` 表 — manifest Core 表(ADR-014 §三:173,数字经 double 中转 = 浮点泄漏)
- Guardrail: `KindPriority` 十项取值与 `R_INTERACT` 真值**归用户**;「取值一旦存在即被守住」——本故事用注入假表签判据本体,真表 INCONCLUSIVE

---

## Acceptance Criteria

*From GDD `design/gdd/interaction-system.md`, scoped to this story(判据正文照录,修订沿革见 GDD 原文):*

- [ ] **AC-4-06([A])** —— `GIVEN` `4-DC-3` 已过硬校验(**前置**),`WHEN` 单测等距并列场景(同种 / 异种 / 三键全 tied),`THEN` 恒定给出**唯一**胜者且**跨运行不抖**。**前置未落 ⇒ 本条 `NOT-RUN`,不得标 ✅**(规则三)
- [ ] **AC-4-14([A])** —— `GIVEN` 同一 **`(WorldSeed, 四源格序列, 输入序列)`**(**⚠️ 13 的病人格序列须作为显式夹具输入**,承 F-4.1b / `AC-4-16`),`WHEN` 跨平台(ADR-012 三格)重放,`THEN` **`F-4.1` 的选择序列相同**。**⚠️ 首轮收窄作用域**:本条**只覆盖纯选择函数**,**不覆盖 `Accept`**(其输入 `Armed` / `ModalOpen` 是第四来源,见 F-4.2 —— 改由 AC-4-20 立判)。**原稿把两者混在一条里 ⇒ 含模态的序列无从验证**
- [ ] **AC-4-16([A])** —— `GIVEN` 本 AC 的**夹具数值全部来源可追**(每个 `patient_id` / `cell` 标注来自 13 的格序列生成器**还是**手工 fixture),`WHEN` 审阅夹具,`THEN` **零**「凭空写的数」。⚠️ **原稿的验算例数字来源不可考** ⇒ unity-specialist 误判「`F-13.7` float 驱动」的直接后果(规则二 · F-4.1b)。**这是「假绿」的形式判据:无出处的数字 = 无法复核**

---

## Implementation Notes

*Derived from F-4.1 全节 · F-4.1b 三验算例 · 三审 int64 改判 · 规则三:*

- **三键字典序逐字实现**:`cmp(a,b) := (d∞_a, P(Kind_a), id_a) vs (d∞_b, P(Kind_b), id_b)` 字典序,小者胜。`d∞ := max(|Δx|,|Δy|,|Δz|)`,**Δ 先升 int64 再 Abs**(三审订正的机械落点;`int.MinValue` 夹具为必测项);`P(Kind)` 从烘焙表读(story 006 交付校验,本故事读注入表);`StableId` = **单一标量 int64**(`StableIdSource` 枚举标记来源,三审裁「标量非元组」—— 元组比较序无法在全表唯一)。
- **全序性质穷举夹具**:同一候选集构造器下断言 ① 自反否定(`cmp(a,a) == Equal` 仅当三键全等)② 反对称 ③ 传递(三元组穷举)④ **完全性:任意两元素可比且非 Equal ⇒ 恰一个 Less**(等距同 Kind 由 StableId 决胜;三键全 tied 在同一实例上**不可能**—— id 唯一性由源保证,夹具须注入「id 冲突」负例验证 4-DC-3 侧的拒绝,4 侧只声明前提)。
- **F-4.1b 三个验算例照抄为断言**(GDD 原文三例:同种等距 / 异种等距 / 全 tied 边界),每个 `patient_id`/`cell` 标注出处(`AC-4-16` 交付 = 夹具文件头部 `source:` 注释 + 审阅脚本;13 的病人格序列**显式入参** —— 不得由测试内 `Random` 现生成)。
- **`AC-4-06` 前置链**:`4-DC-3`(Kind 闭集 10 项互异优先级,构建期硬校验)归 story 006 —— **006 未落 ⇒ 本条 NOT-RUN**(GDD 原文「不得标 ✅」)。本故事可先签「注入合法表后的算法性质」,该部分**不豁免** `AC-4-06` 本体(它点名的是真校验链)。
- **`AC-4-14` 的跨平台半边 BLOCKED-BY**:ADR-012 三格矩阵载体未建(CI 未配 `UNITY_LICENSE` secret,见 session-state Blockers)⇒ 本故事交付重放夹具的**单机可跑形态**(同二进制二次运行选择序列相同),三格半边记 `BLOCKED-BY: ADR-012 CI 矩阵`;作用域收窄(只纯选择,不含 `Accept`)是首轮订正,`Accept` 的替代判据在 story 003 的 `AC-4-20`。
- **等距同格多目标夹具**:`Drop`×2 同格(StableId 决胜)、`Drop`+`Patient` 同格(KindPriority 决胜,小者胜 —— 注入表让 Patient 优先的组与反序组各跑一遍,证明选择真的读表而非硬编码)、三键全 tied 负例(id 重复 → 期望:该夹具**不该出现**,由 `4-DC-3` 上游拒绝 —— 注释写明责任边界)。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 001:纯函数边界与禁入类型扫描(本故事假设其成立,夹具复用其 spy 机器)
- Story 003:候选集**从哪来**(四源构造、`Candidate` DTO 反射、经流确立格)—— 本故事只处理「给定候选集,选谁」
- Story 004:`R_INTERACT` 邻域裁剪与出境(半径过滤发生在 argmin **之前**,story 003/004 的边界;本故事的夹具候选集已裁剪)
- Story 005:`Accept` 门(被拒意图 = 不存在的出境,`AC-4-20` 归 003/005 的交接面)
- Story 006:`4-DC-1…6` 构建期校验本体(本故事的 `AC-4-06` 前置)、`Kind` 枚举对拍(`AC-4-18`)
- `KindPriority` 十项的实际序、`R_INTERACT` 真值 —— **归用户**(Tuning 表;本故事注入假表)
- ADR-012 三格 CI 矩阵与黄金夹具刷新(载体归该轮;本故事只交付可被矩阵跑的夹具)

---

## QA Test Cases

*Written at story creation(lean mode — QL-STORY-READY skipped;specs self-authored from AC text). The developer implements against these — do not invent new test cases during implementation.*

- **AC-4-06**: 等距并列唯一胜者(前置未落 ⇒ NOT-RUN,夹具先行)。
  - Given: 注入合法 `interaction_kinds` 假表(十项互异优先级);等距夹具三组(同种 / 异种 / 三键全 tied 负例声明)。
  - When: 每组建 200 次运行(打乱候选**加入序**与 `Dictionary` 桶序,`StableId` 值域随机分布)。
  - Then: 每次输出同一胜者;跨运行零抖动。**⚠️ 真链签署前提:`4-DC-3` 硬校验落地(story 006)—— 未落本条记 NOT-RUN,不得标 ✅(GDD 原文)**。
  - Edge cases: `int.MinValue` 坐标差夹具(`d∞` 加宽路径:先 long 后 Abs,断言 `d∞ ≥ 0` 恒成立);零候选(返回 None 不抛);单候选;全体出半径(裁剪后空集 → None)。
  - Negative fixture: 以 `GetHashCode()` 序决胜的实现 ⇒ 打乱桶序后胜者漂移,红(该夹具证明「禁哈希序」有真实可抓形态)。

- **AC-4-14**: 纯选择重放(单机半边先跑,三格 BLOCKED-BY)。
  - Given: `(WorldSeed, 四源格序列, 输入序列)` 三元组,13 病人格序列**显式入参**(夹具文件,出处标注齐);fake `ITickProvider` 驱动。
  - When: 同二进制二次运行(及两实例交错);三格矩阵落地后扩为 Linux-x64-Mono / IL2CPP ×2 重放。
  - Then: `F-4.1` 选择序列逐元素相等;`Accept` 态**不进本夹具**(作用域收窄订正 —— 含模态序列无从验证的原稿错误不得复现)。
  - Edge cases: 世界状态前缀含同 tick 多源事件(全序由键本身而非到达序 —— 到达序打乱夹具);长序列(≥ 10⁴ tick)无累积漂移(纯函数本性,但夹具证明无隐藏状态累加)。
  - Negative fixture: 缓存「上一个目标」实现就近偏好 ⇒ 二次运行分叉,红。

- **AC-4-16**: 夹具出处审阅。
  - Given: 本故事全部夹具文件(含 F-4.1b 三验算例)。
  - When: 审阅脚本/人工核对每个 `patient_id`/`cell`:标注 ∈ {13 格序列生成器(生成器版本/seed 注明), 手工 fixture(手标 `manual:` + 选取理由)}。
  - Then: 零「凭空写的数」;出处缺失即红(假绿形式判据 —— 无出处数字 = 无法复核)。
  - Edge cases: 生成器输出的数值**再手抄进夹具**须带再生成命令(可复核);三验算例与 GDD 原文逐字一致(数值/坐标对拍 GDD 表)。
  - Negative fixture: 故意隐去一个数字出处 ⇒ 审阅脚本报该条。

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `tests/unit/interaction/target_selection_test.cs` — must exist and pass(F-4.1b 三验算例 + 全序四性质穷举 + `int.MinValue` 加宽 + 桶序打乱稳定性)
- Logic: `tests/unit/interaction/replay_selection_test.cs` — 单机重放半边(三格半边待 ADR-012 矩阵)

**Status**: [ ] Pending — story not yet implemented(真身落点预期 = `unity/Assets/Tests/EditMode/Interaction/`;登记口径 = `tests/unit/interaction/`)
⚠️ 不得借绿账本:**`AC-4-06` = NOT-RUN**(前置 `4-DC-3` 归 story 006,GDD 原文「前置未落 ⇒ 不得标 ✅」);**`AC-4-14` 跨平台半边 = BLOCKED-BY ADR-012 三格矩阵**(CI `UNITY_LICENSE` 未配,单机重放绿不豁免三格);`AC-4-06` 注入假表签的是算法性质,真 `KindPriority` 序 INCONCLUSIVE(值留白归用户)。

---

## Dependencies

- Depends on: Story 001(4 = 纯函数机器的边界成立)/ ADR-015 格常量(与 1 同源 `LATTICE` 语义;`AC-4-14` 夹具输入含 13 格序列 = 玩家 Epic/病人侧生成器可用)
- Unlocks: Story 003(argmin 机器就位,候选集灌入即成选择)/ Story 004(半径裁剪 + 广播自报消费同一 `d∞`)/ Story 006(`4-DC` 校验的「通过后行为」断言以本故事夹具为基准)

---

## Completion Notes

**Completed**: _待实现_
**Criteria**: _待填_(交付时须附:`int.MinValue` 夹具输出 + 桶序打乱稳定性统计 + 三验算例与 GDD 对拍记录 + 出处审阅脚本)
**Deviations**: _待填_
**Test Evidence**: _待填_
**Code Review**: _待填_
**Manifest**: story Manifest Version 2026-09-21 = 当前 manifest(2026-09-21),无陈旧
