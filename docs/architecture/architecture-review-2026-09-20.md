# Architecture Review Report — 2026-09-20

> **Mode**: `/architecture-review`(full · 复跑,第二份报告)
> **上一份**: `architecture-review-2026-09-15.md`(判定 FAIL + §13 consistency 复查轮)
> **Engine**: Unity 6.3 LTS (URP) · C# · PC (Steam) 优先 · VR 仅急救 · 1–4 人 coop 架构 P0 预留
> **GDDs Reviewed**: **31** 份系统 GDD(`design/gdd/*.md` 全 35 份减去 `game-concept` · `systems-index`
> · `concept-benchmark` · `gdd-cross-review-2026-09-20` 四份非系统件)
> **ADRs Reviewed**: **22**(`adr-001`…`adr-025`,**全部 Accepted**;`adr-004` 已由 `adr-017` 兑现故无独立文件)
> **Engine Reference**: `docs/engine-reference/unity/`(VERSION · breaking-changes · deprecated-apis · modules/ · plugins/)
> **Engine Specialist**: `unity-specialist`(Phase 5 二次意见已并入 §5;其建议**未自行执行**,见 §7)
> **需求基线**: **387** 条 TR(`tr-registry.yaml` 实测,`status:` 字段为计数真源;本轮**零新增、零删除、零状态翻转**)

---

## 判定:**CONCERNS**

不再是 FAIL。上一份的 FAIL 由两条同时成立:「Foundation/Core 需求未覆盖」+「编译级跨 ADR 冲突」。
本轮复核对两条**都改写了事实**:

- **跨 ADR 冲突**:22 份 ADR 之间**无阻塞性裁定矛盾**;依赖图**无环**(拓扑序覆盖 22/22,见 §4)。
  上轮 C-1…C-18 全部已结(逐条核 `traceability-index.md` 变更历史)。
- **Required ADRs**:§Required ADRs 的 **#1 / #2 / #3 三条已全转 Accepted**(= ADR-023 场景生命周期 /
  ADR-025 契约装配清单 / ADR-024 Kind 单一真源)。TD 条件 **C1–C4 四条结案**。

**但离 PASS 还差三件事,且其中两件本轮无法自行清掉**:

| 维度 | 结果 | 是否卡门 |
|------|------|---------|
| 需求覆盖 | ✅ 245 / ⚠️ 51 / ❌ 91(共 387) | 部分 |
| **Foundation 层逐条覆盖** | ❌ **4 条 gap 不为绿** | **是** —— gate-check 质量项「zero Foundation layer gaps」按**逐条 TR** 判定 |
| 跨 ADR 冲突 | **0 阻塞**(本轮新登 RC-1…RC-8 全是**单件内部**矛盾或论据失效,非 ADR 对打) | 否 |
| ADR 依赖环 | **无**(22/22 拓扑可排,tier 0–11) | 否 |
| 引擎兼容 | 22/22 带 Engine Compatibility 段;**3 项承重引擎风险**(§5.2) | 否(但决定 P0 关键路径成败) |
| **12 项 P0 系统零 TR** | ❌ 登记层缺口(§6) | 间接 —— 门查的是 ADR 覆盖,不是 TR 是否登记;但**它使「245 covered」的分母不可信** |
| 预门控清单 | **10 / 13 ✅**(§9) | ⑧⑨ 两份产物缺 |

**为什么不是 PASS**:PASS 的判据是「All requirements covered」。91 条 gap + 4 条 Foundation 级
未绿 = 字面不满足。**为什么不是 FAIL**:FAIL 的门槛是「Foundation/Core 层需求未覆盖」中的
**Core 层大面积裸缺**,或**阻塞性跨 ADR 冲突**。本轮两者皆无 —— 91 条 gap 的 Core 层部分
经逐条分类后,**过半是「非 ADR 缺口」**(数值轮 / 实测前置 / 已实质裁决但登记未同步,见 §2.2)。

---

## 1. 可追溯性摘要

完整 387 行矩阵见 `docs/architecture/traceability-index.md`;数据权威 = `docs/architecture/tr-registry.yaml`。

**本轮对 registry 的改动 = 两个 `adr:` 值 + 两条 `adr_divergence` 结案 + 若干注记;ID 恒 387,状态分布不变。**

### 1.1 按 GDD 分组的覆盖(21 个有 TR 的组)

| GDD | TR 数 | ✅ | ⚠️ | ❌ |
|-----|------|---|----|----|
| `case-system.md` | 36 | 24 | 6 | 6 |
| `item-database.md` | 32 | 11 | 3 | 18 |
| `random-events.md` | 32 | 15 | 2 | 15 |
| `diagnosis-system.md` | 25 | 12 | 2 | 11 |
| `combat-and-weapon-lines.md` | 24 | 20 | 3 | 1 |
| `patient-ai.md` | 24 | 15 | 4 | 5 |
| `disease-simulation.md` | 22 | 14 | 6 | 2 |
| `emergency-procedures.md` | 21 | 12 | 4 | 5 |
| `input-system.md` | 21 | 13 | 4 | 4 |
| `processing.md` | 18 | 12 | 3 | 3 |
| `prescription-and-medication.md` | 19 | 12 | 3 | 4 |
| `interaction-system.md` | 15 | 10 | 1 | 4 |
| `time-and-weather.md` | 15 | 9 | 4 | 2 |
| `inventory-and-items.md` | 16 | 10 | 1 | 5 |
| `skill-system.md` | 8 | 1 | 0 | 7 |
| `world-and-ecozones.md` | 9 | 6 | 2 | 1 |
| `camera-and-viewpoint.md` | 6 | 5 | 0 | 1 |
| `game-concept.md` | 8 | 5 | 0 | 3 |
| `player-controller-and-movement.md` | 7 | 6 | 0 | 1 |
| `enemy-ai.md` | 21 | 18 | 1 | 2 |
| `adr-022`(Tooling,无 GDD) | 8 | 8 | 0 | 0 |
| **合计** | **387** | **245** | **51** | **91** |

### 1.2 按架构层(domain)分组 —— Foundation 门的真判据

| Domain | 总 | ✅ | ⚠️ | ❌ |
|--------|---|---|----|----|
| Foundation | 20 | 15 | 1 | **4** |
| Core | 303 | 193 | 40 | 70 |
| Presentation | 30 | 20 | 7 | 3 |
| Feature | 12 | 4 | 2 | 6 |
| Performance | 9 | 3 | 1 | 5 |
| Networking | 5 | 2 | 0 | 3 |
| Tooling | 8 | 8 | 0 | 0 |

> `requirements-traceability.md` §Uncovered 登记的 4 条 Foundation gap =
> `TR-itemdb-031`(ADR-009 已实质裁决,**`adr:` 未回写**)· `TR-skill-008`(7a 逐字段)·
> `TR-concept-003` / `-004`(**范围声明,非架构缺口**)。
> ⚠️ **其中 2 条本轮可动、2 条不该动**:见 §2.1 的处置建议。

---

## 2. 覆盖缺口

### 2.1 Foundation 层(门控级)

| TR | 缺什么 | 本轮判定 | 处置 |
|----|--------|---------|------|
| `TR-itemdb-031` | 掉落实体的世界状态事件化边界 | **ADR-009 §一/§二 已实质裁决**(派生态 + 身份进流/位置表现,`technical-preferences` ADR-009 条目可证)—— 缺的只是 registry 的 `adr:` 字段回写 | **登记同步,非新 ADR**。归回写轮;⚠️ 回写**不充当验收**(该条的 AC 落点仍在 21a 实现轮)。✅ **2026-09-21 已回写**(`adr: ADR-009 + ADR-015`;`status` 仍 `gap` 不翻 —— 禁借绿)|
| `TR-skill-008` | 技能成长的存档持久化 | ADR-010 覆盖了**形状**(头部 + 三流 + 快照),**逐字段**待 30 的定点算术 ADR(= §Required ADRs **#4**,未兑现) | **保持 gap**。它是 #4 的可见症状,不该被 ADR-010 顶替记绿 |
| `TR-concept-003` | MVP 的 8 条定义 | 范围声明,家规在 `game-concept.md` / `systems-index.md`,**架构按设计不应承接** | **建议显式降级为「不需 ADR」并转 `covered` 的反面 = 保留 gap 但标 `no-adr-by-design`**(同 §2.2 的 21a 簇口径) |
| `TR-concept-004` | P0 排除项清单 | 同上 | 同上 |

> **给门的一句话结论**:即使把这 4 条全部按上述处置,**Foundation 质量门仍不为绿** ——
> 因为 `-003/-004` 的正确处置是「承认它不是架构缺口」,而 gate-check 读的是 `status:` 字段。
> **要它绿,需要 `no-adr-by-design` 这一新状态进入 gate 判据**(那是 `/gate-check` 技能的规格变更,
> 属用户裁,本轮**未动**)。

### 2.2 非 Foundation 的 91−4 = 87 条 gap 的分类

`architecture.md` §5.4 的 12 簇分类**经本轮逐条集合运算复核**,计数**自洽**(见 §7 前置说明),但
**有一行的 ID 前缀标错**(RC-8,§3)。分类结论照录如下(去重后仍为 91):

| 处置类别 | 条数 | 簇 |
|---------|-----|----|
| **非 ADR 缺口**(GDD 内部 schema / 数据形状) | 18 | 21a |
| **算法与表已定边界、缺实现** | 15 | 52 |
| **缺 39 侧落地,非新 ADR** | 11 | 8 |
| **✅ 真裸缺口 —— 须新 ADR** | **7** | **30 技能成长**(`TR-skill-002` 熟练度成长在整数定点域求值,**无任何 ADR 承接**) |
| 规则语义归 GDD | 6 | 37 |
| 写路径归属须裁(= §Required ADRs #5) | 5 | 13 |
| 数值轮 | 5 | 10 |
| 已在 GDD 内 / 待 21a / 归 P1b | 5 | 20 |
| **实测前置(绑 `/test-setup`,非 ADR)** | 4 | 3 输入 |
| 量纲一致判据 | 4 | 11 |
| 范围声明类 | 3 | concept |
| 定点 / 构建期守卫 | 2 | disease |
| 同上 | 2 | timeweather |
| 同上 | 1 | combat |
| 21a 侧 schema(§5.4 原误标 `TR-interaction-*`) | 3 | processing |

**⇒ 真正需要新 ADR 的只有 7 条(30 技能成长)+ 5 条待裁(13 写路径)= 12 条。**
其余 75 条是「GDD 家规 / 数值轮 / 实测前置 / 登记状态未同步」。
**这把上轮「80 条 gap」的恐慌值实质降到了两位数。**

---

## 3. 跨 ADR 冲突检测(Phase 4)

**ID 族说明**:上轮已占用 **C-1…C-18**(`architecture-review-2026-09-15.md` §13.2)与 **E-1…E-5**、
**B-xx**、**R-1…R-15**、**QQ-01…QQ-15**。⚠️ 本轮**首轮起草时曾误用 C-11/C-12/C-13** —— 与上轮
§13.2 的 C-11(`SimEvent.Seq`)/ C-12(规范性实施指引)/ C-13(「病史流」措辞陈旧)**直接撞号**。
已改族为 **RC-n**(Review-Conflict),本轮报告**自有 ID 空间**。此判正本身即是一处失效模式样本:
**同一仓内 ID 族无中央登记 ⇒ 每轮新报告都可能撞号**(登记为 §8 的 D-R1)。

### 结论:无 ADR-对-ADR 阻塞冲突;8 处单件内部矛盾 / 失效论据

| ID | 件 | 类型 | 严重度 |
|----|----|------|--------|
| **RC-1** | `adr-013` | 内部矛盾(铁律 vs 自己的风险缓解) | 🔴 高 —— 且是**成对失效**:spike 越可能失败,矛盾越必被触发 |
| **RC-2** | `adr-023` | 状态串 + 效力口径自相矛盾 | 🟠 中高 —— 工具链破坏 + 实施序歧义 |
| **RC-3** | `adr-017` | **论据失效**(结论不动) | 🟠 中 —— 执法体只剩一处 |
| **RC-4** | `adr-012` | 风险缓解不可执行(有更便宜的解) | 🟠 中 |
| **RC-5** | `adr-025` | 断言写成事实(「恰 = {BCL,…}」) | 🟡 低中 |
| **RC-6** | `adr-023` ②/③ | 不变量无守护(判据窄于对象) | 🟡 低中 |
| **RC-7** | `adr-014` | 未pin解析器默认值(DateParse/FloatParse) | 🟡 低中 —— **已部分自我防护,非裸缺口**(见条内) |
| **RC-8** | `architecture.md` §5.4 | 登记错误(行标签前缀) | 🟢 低(文本级,本轮已就地修) |

---

#### 🔴 RC-1 · ADR-013 禁止自己规定的降级路径

**冲突位置**(同件内部,非跨件):
- `adr-013:203` —— 「R-6 **不重复实现焦点算法**」(铁律口径)
- `adr-013:432` —— §Risks 缓解措施:「不达预期回**自实现焦点算法**(接口不变)」
- `adr-013:468` —— Validation 项把该回退列为**已接受方案**
- 同件 §6.6 假设 6 自评:UI Toolkit 运行时手柄焦点导航 = **⚠️ 半可信**,原型 spike 为前置

**Impact**:如果 spike 通过,矛盾休眠;如果 spike 失败(本件自己的评级),**唯一被批准的缓解措施
恰好是被铁律禁止的那一个**。后果不是文档难看,是实现期第一个动作就违规 —— 而 42 的焦点导航
是**全案硬约束**:`technical-preferences.md` 的 Platform Notes 明写「拟物 UI(无血条 / 无小地图)
**必须**同时支持键鼠与手柄导航」。

**已有的一处收窄(必须一并读)**:`skeuomorphic-ui.md:14` 的重开触发条件 ② 已登记该降级路径,
并把口径收窄为「自实现焦点算法须验证**只约束导出面**」—— 即 :203 读成「42 **对外契约面**不重复
实现焦点算法」而非「42 内部不得有焦点算法代码」。`skeuomorphic-ui.md:190` + `AC-42-B4`(BLOCKING
· 类型面断言)是该收窄的执行体。
**⇒ 矛盾的真实形状 = 「`adr-013` 正文未反映 42 侧已做的收窄」**,不是「两件事无法同时为真」。

**Resolution options**:
1. **就地修订 `adr-013:203`** —— 把铁律的约束面对象写清(「42 不自实现**引擎已提供的**焦点移动;
   降级期自实现的焦点算法受 `AC-42-B4` 类型面断言约束」),与 42 侧既有收窄对齐。**成本 = 三行文本**。
2. **改判为「焦点算法永远自研」** —— 弃官方 `NavigationMoveEvent` 桥。代价:承 `input-system.md:300`
   的 `AC-3-C2` 同键双触发风险由本项目自担,且 `AC-3-C4` 的 Roslyn 断言面要重写。
3. **不修,等 spike 结果** —— 风险 = spike 是 ADR-013 §6.6 里**最可能失败**的一项,失败时才发现
   要裁一条 ADR,而那时 42 的故事已经在写。

**建议**:**option 1**(纯口径对齐,无新裁决 —— 42 侧收窄已经存在,ADR 只是没同步)。
**⚠️ 本轮未执行。ADR 正文修订属用户裁。**

---

#### 🟠 RC-2 · ADR-023 的状态串与效力口径两处矛盾

- **状态串**:`adr-023:5` 写 `Accepted(附条件,见下)`。`docs/CLAUDE.md` 的生命周期是
  `Proposed → Accepted → Superseded`,而 gate-check 与 story-readiness 的判据是**字符串匹配**
  (`technical-preferences` 的 ADR 日志口径亦按 `Accepted` 记)。**带括号的值是否被下游读成
  Accepted,取决于具体 grep 是否用了精确匹配。**
  同类先例已在仓内被明确处理过:Phase 5b 的 `Needs Revision` 技能规格里写着
  「Status field must be exactly `Needs Revision` — **no parentheticals** (other skills match that
  exact string and parentheticals break the match)」—— **同一失效模式,本件没套用该先例。**
- **效力口径**:`adr-023:53` 「本 ADR 转 Accepted 的**硬前置**」与 `:228` 「Accepted 须等 S1–S7
  至少 S1/S3/S4 有结果」 vs `:10` 用户裁定口径「S1–S4 的回填是**实现故事的前置,不是本裁决的
  效力条件**」。`:10` 与 `:53`/:228` **同件对立**。

**Impact**:两种读法给实施序完全不同的答案 —— 按 `:10`,ADR 已生效、故事可以开;
按 `:53`/:228`,**S1/S3/S4 未跑 ⇒ 本件不能算 Accepted ⇒ 依赖它的 44 / 20 / 42 故事全部自动阻塞**。
`/create-epics` 会在这条上做出与用户裁定相反的选择,而它是**批量生成**,事后回收成本高。

**Resolution options**:
1. 状态串归一为 `Accepted`,把「附条件」的含义**只**留在正文(与 `:10` 的裁定口径一致),
   `:53` / `:228` 就地加注指向 `:10`。
2. 状态串保留,但**新增** `Accepted (conditional)` 进 `docs/CLAUDE.md` 生命周期 + 全下游判据。
3. 维持现状(= 赌下游用模糊匹配)。

**建议**:**option 1**。理由:用户在 2026-09-20 已**明确裁过**这个含义(「不是效力条件」,
见 `technical-preferences.md` ADR-023 条目「附条件的定义(用户裁定口径)」),
`:53` / `:228` 是裁定**之前**写的草稿没回改 —— 这是**登记滞后**,不是待决问题。
**⚠️ 本轮未执行**(仍是 ADR 正文改动)。

---

#### 🟠 RC-3 · ADR-017 §二 的机制论据很可能为假(结论不受影响)

`adr-017:165` —— 「而 `"noEngineReferences": true` 会**编译失败**(这是好事)」
`adr-017:168` —— 「冲突本体… DOTS 栈… 全部是引擎程序集」

`unity-specialist` 的挑战:**`noEngineReferences` 门控的是引擎模块程序集
(`UnityEngine.*Module`),不门控 UPM package 程序集** —— `Unity.Entities` / `Unity.Burst` /
`Unity.Jobs` / `Unity.Mathematics` 是 **package** 程序集,在 asmdef 未显式列 `references` 时
仍可能被自动解析进来。⇒ 该句「会编译失败」的**机制归因**很可能错。

**结论层面**:ADR-017 的裁决(sim 侧不上 DOTS)**依然成立**,但支撑它的不再是"编译器会挡",
而是**同件 §二 自己已经写的另一件事** —— 「硬化要求 = sim 程序集引用集**白名单断言**,
把约定升为构建失败」。**唯一执法体 = 那条断言,不是 asmdef flag。**
这正是 ADR-025 裁定 ④ 的「清单封闭性 = 本法」同构做法。

**为什么仍然重要**:若实现期有人按 :165 的字面理解,会以为「加了 flag 就安全」而**不写白名单断言**
⇒ 门 A 变成纸面约束。这是静默失效。

**Resolution options**:
1. 就地改 `:165`/:168 的**论据**(flag 是必要非充分;充分性归白名单断言),**裁决文本不动**。
2. 立一条最小 spike(编辑期建一个带 flag 的 asmdef + 只 `using Unity.Entities` 的空脚本,
   看是否编译通过),**先实测再改文本** —— 成本约 15 分钟,且它同时是 ADR-025 V-6 的一部分。
3. 不改 —— 保留错误论据。

**建议**:**option 2 先行、option 1 随后**。本件 Engine Knowledge Risk 标的是 **LOW**
(「不依赖任何 post-cutoff API」),而这条恰是一个**依赖引擎事实的断言** —— 若实测为假,
LOW 评级本身也要跟着改。**⚠️ 本轮未执行,且未跑实测(需 Unity 编辑器,本机无)。**

---

#### 🟠 RC-4 · ADR-012 F7 的 BLOCKING spike 有更便宜的结构解

`adr-012:171` 已经承认:FMA 禁令对**全整数 sim** 无杠杆(整数乘加无融合路径)。
同件的 F7 是另一件事 —— 「C# `int64` 溢出为定义性回绕,**IL2CPP 生成的 C++ 有符号溢出为 UB**」,
而 `SplitMix64` 与 Q16.16 中间乘**正踩此线**,故列为 **BLOCKING spike**。

`unity-specialist` 的意见:**无符号整数的溢出在 C# 与 C++ 两边都是定义性回绕(mod 2ⁿ)**。
把 `SplitMix64` 的内部状态、以及 Q16.16 的中间乘积改住 `ulong`(必要时 `unchecked`),
**UB 从"需要实测确认不发生"变成"结构上不存在"** —— 一条 BLOCKING spike 塌缩成一个表示选择。

**Impact**:F7 是 ADR-012 三格常驻矩阵的前置;它若被降级,ADR-012 的实施风险显著下降。
⚠️ 但**必须保留**的是:改 `ulong` 只消解 **UB** 这一项,**不消解**「Mono 与 IL2CPP 逐位一致」
的其余待实测项(黄金夹具矩阵照旧要跑)。

**Resolution options**:
1. ADR-012 就地增补:内部表示用 `ulong` / `unchecked`,F7 从 BLOCKING spike **降级**为
   一条 EditMode 断言(`SplitMix64` 已知向量对拍)。
2. 保留 int64,F7 照跑 —— 结论可能是"IL2CPP 恰好回绕",但那是一条**依赖编译器行为**的不变量。
3. 两者都做(改表示 **且** 跑矩阵),F7 只是不再是前置。

**建议**:**option 3 的降级版** = 改表示 + 矩阵照跑但 F7 不再卡前置。
「不依赖编译器善意」与 ADR-005/006 全案取向(刻意不用任何 post-cutoff API)一致。
**⚠️ 本轮未执行。**

---

#### 🟡 RC-5 · ADR-025 把「断言」写成了「清单事实」

`adr-025` 裁定 ① 写 `Sim` 的引用集「**恰 = {BCL, Sim.Contracts}**」、`Sim.Contracts`「**恰 = BCL**」。
`unity-specialist` 指出:**没有任何 asmdef 字段能产出"BCL-only"的引用集** ——
`"references": []` 的含义**不是**"什么都不引用"。
⇒ 这两句作为**清单事实**不可能成立;它们只能是**被断言的性质**(构建期读
`GetReferencedAssemblies()` 比对)。

这与 RC-3 是**同一个形状的错误**:把"我希望编译器挡的"写成"编译器挡的"。
好消息:ADR-025 裁定 ④ 已经立了"清单封闭性 = 本法"(未登记 asmdef = 构建失败),
所以**执法体已经在**,只需把 :① 的措辞从断言式改成"由 ④ 的断言守"。

附带项(specialist 提):若要真正 BCL-only,另一条路是**用 `dotnet` 出 netstandard2.1 程序集**
再交给 Unity —— 这是"能引用的东西"的实际机制之一,值得在 Alternatives 里留一行。

**建议**:就地改 ① 的措辞为「**期望引用集**…由裁定 ④ 的构建期断言强制」+ Alternatives 补一项。
**⚠️ 本轮未执行。**

---

#### 🟡 RC-6 · ADR-023 ② 的扫描判据挡不住 ③ 立的不变量

`adr-023:133-135`(③)裁决:主相机 + `AudioListener` **住 Boot,全程不销毁**。
`adr-023:~116`(②)的构建期扫描判据:`World.unity` **零 gameplay GameObject**,
白名单只列「环境光 / 雾设置、纯视觉层 Terrain 容器、无逻辑根节点」。

**问题**:扫描**没把 `AudioListener` / 相机列为禁止项** ⇒ 美术在 `World.unity` 里随手挂一台
相机或第二个 `AudioListener`,扫描放行,③ 的"全程不销毁"与 ADR-018 §五 的"每设备一条混音总线"
**同时破**。而 `adr-023:85-87` 显示这个破口曾经就存在(「Boot 无相机栈」被复核为自相矛盾,
现裁决 = Boot 常驻持有)—— 即**同类 bug 已发生一次,判据未收紧**。

**建议**:② 的扫描**增列"相机与 `AudioListener` 在任何非 Boot 场景中出现 = 构建失败"**;
并把它并进 S2(现在 S2 只测"扫描可行性",不测这条)。属**收紧已有判据,非新裁决**。
**⚠️ 本轮未执行。**

---

#### 🟡 RC-7 · ADR-014 词法器未 pin Newtonsoft 的两个默认值

`adr-014` 阶段 1 用 `JsonTextReader`。**风险面**:Newtonsoft 的 `JsonTextReader` 在
`DateParseHandling` 非 `None` 时,会把**看起来像日期的字符串 token** 转成 `DateTime`;
`FloatParseHandling` 决定数字 token 落 `long` / `double` / `decimal`。
`adr-014:202` 已经**自己指出**了后一半(「JSON 数字 token 会被 Newtonsoft 自身解析成
`long` / `double` / `decimal`(`JsonTextReader.Value`)」)—— 所以本条**不是裸缺口**。

**残余**:① 件内未把「构造 `JsonTextReader` 时必须显式设 `DateParseHandling = None` 与
`FloatParseHandling = None`」写成**要求**(§Implementation Guidelines 只写"词法器只用
`JsonTextReader`、对象反序列化 API 全禁");② 前一半(日期字符串 → `DateTime`)件内未提。
本项目 `Fix` 字段按裁定写**字符串**(`"offset": "3/4"`),而 `3/4` 恰好是**合法 M/d 日期格式**
⇒ 这不是假想风险,是**本项目自己选定的编码格式**撞上了它。

**建议**:① 在 Implementation Guidelines 加一条硬要求(两默认值必须显式设 `None`);
② 把「`"3/4"` 类 `Fix` 字面量经词法器后仍是字符串 token」写成一条负向夹具(进 ADR-012 单元级黄金夹具)。
**成本极低,且它是 `FixParse` 单一入口这条铁律的地基。**
**⚠️ 本轮未执行。**

---

#### 🟢 RC-8 · `architecture.md` §5.4「4 交互」行的 ID 前缀标错(本轮已就地修)

§5.4 该行写作「`TR-interaction-*`(残余)· 4」。
**实测**:registry 的 91 条 gap **无任何 `TR-interaction-*`**(interaction 组 15 条 = 10✅/1⚠️/4❌?
—— 逐条核后 4❌ 确实存在但**前缀是 `TR-input-` 与 `TR-processing-`**)。
该行 4 条的真实身份 = `TR-processing-*` 3 条 + `TR-combat-024` 1 条。
⇒ **行的条数对(12 簇仍加总 91),标签错**。
失效模式:与 `consistency-failures.md` 2026-09-20 批次「正文状态断言稳定滞后」同型,
但更具体 —— **「分类表按簇名写、不按 registry 生成 ⇒ 簇名会与 ID 空间漂移」**。
**处置**:本轮**已就地修标签**(纯登记,无裁决面),并登记 D-R2(§8)。

> ⚠️ **本轮自纠记录**:首跑时用 `grep -c "TR-.*$s"` 数每簇,得「21 个有 TR 的组 / 12 项零 TR」**看似成立**
> 但 Foundation 与 91 条的分类**不可复算**;改用 `yaml.safe_load` + 逐条 `status` 集合运算后,
> §5.4 的 93-vs-91 之谜**解开**:**表本来加总 91,是我第一版数错了**(把 `↓(原 17)` 的注记数字
> 也计了进去)。⇒ 上轮报告与本轮回写中「93 vs 91 差 2,归因不明」的疑点**撤销**,代之以 RC-8。

---

## 4. ADR 依赖顺序(Phase 4 后半)

### 4.1 环检测:**无环**

22 份 ADR 的 `Depends On` 边全部解析成功,拓扑排序覆盖 **22/22**。
上轮之后新增的边(ADR-023 / 024 / 025)**未引入回边** —— 特别核过:
`adr-023` → {005, 010, 013, 014, 015, 018, 020}(全部 tier 更低);
`adr-024` → {009, 014, 022};`adr-025` → {005, 006, 017}。

### 4.2 推荐实施序(分层)

```
Tier 0(无依赖 —— 形状定义者):
  1. ADR-005  确定性模拟与状态同步模型        ← 全案数据形状的地基
Tier 1:
  2. ADR-006  定点域边界数据契约
Tier 2:
  3. ADR-007  事件权威与掷骰状态
  4. ADR-012  跨平台确定性 CI 门
  5. ADR-017  DOTS 决策
  6. ADR-025  契约程序集清单与命名            ← asmdef 落地,一切约束的执行体
Tier 3:
  7. ADR-008  病例事件流                       (005,006)
  8. ADR-010  持久化与存档格式                  (005,006,007,008,009)
  9. ADR-014  数据管线与 JSON 解析器            (006,010)
 10. ADR-015  世界几何与整数格                  (002*,003*,005,009,014)
 11. ADR-016  AI 架构                          (005,009,014,015)
     * ADR-002/003 由 ADR-015 兑现,无独立文件
Tier 4:
 12. ADR-009  世界状态事件化边界                (005,006,007,008)
 13. ADR-001  网络 pipe 抽象                    (005)
 14. ADR-013  拟物 UI 框架                      (011)
 15. ADR-018  音频架构                          (013,014)
 16. ADR-019  遥测与隐私                        (005,007,010)
 17. ADR-020  玩家控制器与相机                  (013,015,016,018)
Tier 5+:
 18. ADR-011  输入架构                          (010)
 19. ADR-021  POI 状态所有权                    (009,015)
 20. ADR-022  关卡工具                          (009,014,015,016)
 21. ADR-024  Kind 单一真源                     (009,014,022)
 22. ADR-023  场景生命周期与渲染                (005,010,013,014,015,018,020)
```

> ⚠️ **Tier 分配的一个真实张力**:`ADR-009` 排在 `ADR-008` 之后但 `ADR-015` 依赖它 ——
> 拓扑序在这里有自由度,而 **`ADR-025` 建议提到 tier 2**:asmdef 是**所有**其他 ADR 的
> 约束的落点(RC-3 / RC-5 两处的共同教训 = 没有构建期断言,任何"编译器会挡"都是空话)。
> 该建议与 `technical-preferences` 日志里「ADR-025 是 Required ADR #2 兑现件」的次序一致。

---

## 5. 引擎兼容性审计(Phase 5)

### 5.1 审计结果

```
Engine: Unity 6.3 LTS(项目 pin 2026-02-13 · LLM 训练覆盖 ≈2022 LTS)
ADRs with Engine Compatibility section: 22 / 22  ✅
Deprecated API references: 0 ✅(全 grep:Application.LoadLevel / Legacy Input /
  UGUI-as-primary / GameObjectEntity —— 均只出现在「不使用它」的语境)
Version consistency: 22 / 22 全部写 Unity 6.3 LTS;零 stale 引用 ✅
Post-cutoff APIs used: 集中于 ADR-013(UI Toolkit 焦点桥)/ ADR-023(RenderGraph,
  Addressables 6.2+)/ ADR-011(Input System 6.3)/ ADR-014(Addressables E-13)/ ADR-017(DOTS,未引入)
Knowledge Risk 分布:HIGH 6 · MEDIUM 8 · LOW 8
```

### 5.2 🔴 三项承重引擎风险(按危险度排序)

**R-A · ADR-013 §6.6 假设 6 —— UI Toolkit 运行时手柄焦点导航(评级:半可信,最可能失败)**
`technical-preferences.md` 把它列为**硬约束**(无血条 / 无小地图的拟物 UI ⇒ 脉案 / 出诊箱 /
纸质地图全部需要焦点导航路径,且手柄没有指针)。它同时是 **RC-1 的触发器**。
⇒ **这不是一个 spike,这是 P0 的关键路径上唯一没有退路的一环**(有退路,但退路被同件禁止)。
**须用户裁 RC-1 option 1**(三行文本)才能在 spike 失败时不违规。

**R-B · ADR-017 §二 的 `noEngineReferences` 机制(= RC-3)**
「cheap to verify」但**必验**:本机无 Unity 编辑器 ⇒ 须一条编辑期 spike。
它的**结论不变**,变的是"谁在挡"。

**R-C · ADR-012 F7 的 IL2CPP 有符号溢出 UB(= RC-4)**
唯一一个**用表示选择就能取消**的 BLOCKING 项。不处理 = 让一条本可消除的风险留在矩阵前置。

### 5.3 其余 specialist findings(非承重,但须登记)

- **S-1 · ADR-014**:未 pin `DateParseHandling` / `FloatParseHandling` ⇒ **RC-7**(⚠️ 部分自防已在件内)。
- **S-2 · ADR-025**:「恰 = BCL」是断言非清单事实 ⇒ **RC-5**;`dotnet` 出 netstandard2.1 是真实可选路径。
- **S-3 · ADR-023**:② 扫描判据窄于 ③ 的不变量 ⇒ **RC-6**。
- **S-4 · ADR-023 S3 被低估**:「每次读档漏一个世界」的根因(InstantiateAsync 产物不被
  `UnloadSceneAsync` 销毁)在 S3 的描述里只测了"能否 Release",**未测 bundle 引用计数泄漏**
  —— 而 handle 全 Release 后 bundle 仍被引用是 Addressables 的已知形态。
  ⇒ **S3 的判据文本要补一条"bundle refcount 归零"断言**;第 6 步的运行期断言只查登记簿,**查不到它**。
  ⚠️ 本轮**未改 ADR 正文**(属裁决面文本),登记于此。
- **S-5 · 正面确认**:`ADR-011` / `ADR-022` 的前提被 specialist **确认无引擎侧 blocker**;
  `ADR-014` 的结构面(两阶段工具链、运行期零 JSON 解析器)确认成立。

### 5.4 版本一致性 / 弃用 API

零弃用 API 使用;零 stale 版本引用。上轮的 E-2(`System.Int128` 在玩家构建不存在)与
E-3(FMA 禁令缓解不可执行)已结案(前者改判,后者由 RC-4 承接同一教训)。

---

## 6. GDD 修订标记(Phase 5b · Architecture → Design Feedback)

**本轮发现 1 项,已就地执行(9 处注记,零机制 / 零数值改动)。**

| GDD | 假设 | 现实 | 处置 |
|-----|------|------|------|
| `diagnosis-system.md`(8)+ `patient-ai.md`(13)+ `systems-index.md`(索引侧) | 「**联机时音频精度统一取主机技能**」 | ADR-018 §五 已由 **2026-09-18 用户裁定 D-A 改判** = 各设备取本机玩家技能档(`SetTier(TierSource.Local)`)。原改判理由:「单 `AudioListener` ⇒ 每设备一条混音总线」这一**前提为假** —— `AudioListener` 只约束空间化参考点 | ✅ **9 处 D-A 追加注已落**:`diagnosis-system.md` :10 / V-8.7 表④ / 前提注体 / :1315 D-8-8 行 / :1664 / :1961 / :1975 OQ-8-4 / :2005 降格表② · `patient-ai.md:872` · `systems-index.md:647` |

> **为什么这是 Phase 5b 的样本而不是 Phase 4 的**:失效模式 = **裁定发生了,只有裁定发生地(44)更新了,
> 上游引用件没更新**。`audio-system.md` 的 `AC-44-07` 与 `diagnosis-system.md` 的正文
> **断言相反的事实**,持续 2 天(2026-09-18 → 2026-09-20)。
> 它属于 `consistency-failures.md` 2026-09-20 批次二「正文状态断言稳定滞后」的**跨文档亚型**,
> 值得入该日志(本轮**未写**,登记为待办 T-4)。

**⚠️ systems-index 状态字段本轮未改**:该表的「状态」列语义 = **`/design-review` 评审结案态**
(31 行现全为 `✅ Approved` + 显式风险接受注),而 Phase 5b 要求的 `Needs Revision` 是
**架构反馈态**。二者同列会**破坏评审管线的结案记录**(全 31 项 2026-09-19 已闭卷)。
⇒ 本轮以**就地注记**代替状态翻转。若须正式的 `Needs Revision` 标记,须**另设列或另设字段**(用户裁)。

---

## 7. 架构文档覆盖(Phase 6)

`docs/architecture/architecture.md` 存在且完整。本轮核对:

- ✅ **系统覆盖**:**53 行系统全部有层归属,0 缺失**(逐行对 `systems-index.md`)。
- ✅ **数据流**:三流 + 第二 QoS 通道 + `VitalsDto` / `PresentationDto` / `AudioCueDto` 边界齐。
- ✅ **API 边界**:六抽象点(`ITickProvider` / `IEventSink` / `IIdAuthority` / `IVitalsQuery` /
  `SimEvent` / `IEventAuthority`)+ 第六项之后新增的 `ITeleportCommandSink`(ADR-025 QQ-01 = ①′)。
- ✅ **孤立架构**:无 —— `Tooling` 层行 54 有 ADR-022 + 8 条 TR 支撑。
- 🟡 **§5.4 分类表一处标签错**:`adr-005` 的「门面程序集」等死术语 → **RC-8**,已就地修。
- 🟡 **§Required ADRs**:**#1/#2/#3 已兑现**;**#4**(系统 30 定点算术)/ **#5**(13 的写路径归属)
  未兑现,**非开工阻塞**(technical-preferences 日志口径)。

---

## 8. 登记层缺陷(本轮新发现)

| # | 缺陷 | 证据 | 处置 |
|---|------|------|------|
| **D-R1** | **报告 ID 族无中央登记 ⇒ 每轮新报告都可能撞号** | 本轮首稿用 C-11/12/13,与上轮 §13.2 的 C-11/12/13 **同号异义** | 已改族为 RC-n。**建议**:在 `traceability-index.md` 增设「报告 ID 族登记表」(C- / E- / B- / R- / QQ- / RC- / D-R 各占一行,记"已用至 N") |
| **D-R2** | **§5.4 分类表按簇名手写 ⇒ 与 ID 空间漂移** | RC-8:「4 交互 · `TR-interaction-*`」实为 processing 3 + combat 1 | 已就地修标签。**建议**:该表改为**由 registry 生成**(脚本:按 `id` 前缀分组 + 按 `status` 计数),同 ADR-024 对 Kind 的「单一真源 + 生成器」做法 |
| **D-R3** | **12 项 P0 系统零 TR** | 实测(集合运算):`persistence-service` · `skeuomorphic-ui` · `audio-system` · `foraging` · `casebook` · `save-slot-ui` · `death-and-respawn` · `modular-building` · `clinic-machine` · `tutorial-and-onboarding` · `telemetry-analytics` · `medical-consequences` —— **12 项全部在 `systems-index.md` 标 P0** | 分类:① **6 项已有 GDD + 已有 ADR 覆盖**(persistence / skeuomorphic-ui / audio / save-slot-ui / modular-building / clinic-machine)⇒ **纯登记欠账,可批量回填**;② **4 项有 GDD、ADR 覆盖薄**(foraging / casebook / death-and-respawn / tutorial)⇒ 回填时逐条判 status;③ **2 项非 P0 架构范围**(telemetry-analytics = ADR-019 已裁 P0 最小切面;medical-consequences = P1a 主)。**⇒ 245 covered 的分母不含这 12 项 ⇒ 覆盖率 63.0% 是偏高估计**。**本轮未回填**(批量 TR 新增须专门批次,承 #13 先例) |
| **D-R4** | **计数漏刷(两处,同因)** | `architecture.md` 七处「19 份 ADR / 19/19 / 93 / 17」;`requirements-traceability.md` §Coverage Summary 六处「243 / 93」—— 均系 2026-09-20 回写轮只刷了 index + registry 未刷两上位件 | **✅ 本轮已就地全刷**(19→22 · 93→91 · 52 簇 17→15 · 243→245),并在 §Coverage Summary 下方留失效模式注 |
| **D-R5** | **`adr_divergence` 曾开放两条**(QQ-11) | `TR-case-036` / `TR-interaction-015` 的人读列与 registry `adr:` 不一致 | **✅ 本轮结案,且两条判得相反**:`-036` = **摘要列过度归属**(逐行核 ADR-013 §GDD Requirements Addressed 无一行覆盖本条;真实承载 = `diagnosis-system.md` S-8.4 的 GDD 侧家规)⇒ **registry `adr: null` 是对的,摘要列改了**;`-015` = **registry 漏登**(ADR-011 明列「10 急救 —— 直读通道」+ ADR-013 §十 `IModalState.Modal`)⇒ **补 `adr: ADR-011 + ADR-013`**。**⚠️ 关键教训:两条的"修正方向"相反 ⇒ "以 registry 为准"这条元规则会被证伪**;正确做法 = **逐条回到 ADR 的需求表核验,不预设哪边是真的**。**现开放 `adr_divergence` = 0。** |

### 待办(本轮未做,须用户或后续批次)

| # | 项 | 归属 |
|---|----|------|
| **T-1** | 8 项 ADR 正文修订(RC-1…RC-7 + S-4) | **用户裁 —— 逐项,见 §9** |
| **T-2** | D-R3 的 12 项零 TR 批量回填 | 专门批次(承 #13 先例:一轮一域,带 TR 撰写即注 status) |
| **T-3** | Foundation 门的 `no-adr-by-design` 状态(§2.1) | 用户裁(`gate-check` 规格变更) |
| **T-4** | D-A 跨文档滞后写入 `docs/consistency-failures.md` | 登记义务(该日志由 `/consistency-check` 自动维护,本轮手工补) |
| **T-5** | RC-3 的最小 spike(编辑期 asmdef + `using Unity.Entities`) | 需 Unity 编辑器,**本机不可跑** |

---

## 9. 需用户裁定项(8 条 —— 本轮一律未执行)

> **✅ 2026-09-21 全部结案(全部照建议执行,零例外)** —— 由 `/architecture-review` 复跑实测,
> 报告 = `docs/architecture/architecture-review-2026-09-21.md` §3.1。逐条坐标:
> `RC-1` → `adr-013:204-207`(约束面收窄,铁律作用域 = **42 的对外契约面**而非「内部禁有焦点算法」)·
> `RC-2` → `adr-023:5`(状态串归一 `Accepted`;全仓 **22/22 字面 `Accepted`**)·
> `RC-3` → `adr-017:170`(论据订正:`noEngineReferences` **必要非充分**,充分性归引用集白名单断言)·
> `RC-4` → `adr-012` ×7 处(F7 **降级为表示选择**,改住 `ulong`/`unchecked`)·
> `RC-5` → `adr-025:116-119`(改「引用集**期望** + 构建期断言执法」)·
> `RC-6` → `adr-023:138-141` + `:276`(扫描收紧)·
> `RC-7` → `adr-014:173-176` + `:366`(词法器两 pin + 负向夹具)·
> `S-4` → `adr-023:278-281`(S3 补 bundle refcount 归零断言)。
> ⚠️ **该清单此后不再有效 —— 勿据此判断未决状态。** 本注为**追加注**,下表原文原样保留(承本项目
> 「历史注体以追加注补」先例)。

按危险度排序。**每条都是 ADR 正文修订 ⇒ 不属本 skill 的"就地修正"权限。**

| # | RC | 一句话 | 我的建议 |
|---|----|--------|---------|
| 1 | **RC-1** | `adr-013:203` 铁律 vs `:432` 自实现焦点算法回退 —— spike 失败即违规 | **改文本对齐 42 侧既有收窄**(成本最低,非新裁决) |
| 2 | **RC-2** | `adr-023` 状态串 `Accepted(附条件,见下)` + `:53`/:228` vs `:10` 效力口径 | **归一 `Accepted`,条件只留正文**(你 2026-09-20 已裁过含义) |
| 3 | **RC-3** | `adr-017:165` `noEngineReferences` 机制论据很可能为假 | **先 15 分钟 spike 实测,再改论据**;结论与裁决文本不动 |
| 4 | **RC-4** | `adr-012` F7:改住 `ulong` / `unchecked` 可把 BLOCKING spike 降级 | **改表示 + 矩阵照跑**;F7 不再卡前置 |
| 5 | **RC-5** | `adr-025` 「恰 = {BCL, Sim.Contracts}」是断言非清单事实 | **改措辞指向裁定 ④ 的构建期断言** |
| 6 | **RC-6** | `adr-023` ② 扫描挡不住 ③ 的相机 / `AudioListener` 不变量 | **收紧扫描 + 并进 S2**(非新裁决) |
| 7 | **RC-7** | `adr-014` 未 pin `DateParseHandling/FloatParseHandling = None` | **加一条硬要求 + 一条负向夹具**(`"3/4"` 会被当日期) |
| 8 | **S-4** | `adr-023` S3 判据未测 bundle refcount 泄漏 | **S3 补一条 refcount 归零断言** |

> 其中 **3 条(RC-1 / RC-3 / RC-4)是 specialist 点名的承重风险**,顺序即危险度。
> **RC-5 / RC-6 / RC-7 / S-4 是低成本文本收紧**;RC-2 是**你已裁过的含义没同步到草稿**。

---

## 10. 预门控清单(`/gate-check pre-production` 的 13 项必交件)

| # | 要件 | 状态 |
|---|------|------|
| 1 | 引擎已选(`CLAUDE.md` 非 `[CHOOSE]`) | ✅ Unity 6.3 LTS |
| 2 | 技术偏好已配置 | ✅ 含 2026-09-20 四项裁定(20 Hz / CAP 24 / DOTS 门阈值 / 病种预裁) |
| 3 | `design/art/art-bible.md` §1–4 | ❌ **缺 —— 本轮序列的 ⑧** |
| 4 | ≥3 份 ADR 覆盖 Foundation(场景管理 / 事件架构 / 存档) | ✅ 22 份;ADR-023(场景)/ 005+009(事件)/ 010(存档)三条**正对着** |
| 5 | 引擎参考文档 | ✅ `docs/engine-reference/unity/`(modules + plugins) |
| 6 | `tests/unit/` + `tests/integration/` | ✅ |
| 7 | `.github/workflows/tests.yml` | ✅ ⚠️ `UNITY_LICENSE` secret 须手工配置(不自动化) |
| 8 | ≥1 个示例测试文件 | ✅ `tests/unit/sim/sim_fixedpoint_test.cs` —— ⚠️ **因 asmdef 未落地而尚不被编译**(ADR-025 裁定面已解锁,**实现轮**才真跑);**不充当第 8 项的"functional"证据** |
| 9 | `docs/architecture/architecture.md` | ✅ v 现值,§5.4/5.5 已刷 |
| 10 | `docs/architecture/requirements-traceability.md` | ✅ 计数已刷(§Summary 六处) |
| 11 | `/architecture-review` 报告存在于 `docs/architecture/` | ✅ **本件**(2026-09-15 + 2026-09-20 两份) |
| 12 | `design/accessibility-requirements.md` 带承诺档位 | ✅ Standard + L-1/L-2 |
| 13 | `design/ux/interaction-patterns.md` | ✅ |
| — | `docs/architecture/control-manifest.md` | ❌ **缺 —— 本轮序列的 ⑨**(它是 Pre-Production→Production 门要件,非本门,但序列里一并做) |

**ADR 循环依赖检查:无环 ⇒ 不 FAIL。**
**Foundation 零 gap 质量检查:❌ 4 条不为绿(§2.1)—— 本门唯一"质量项"级红灯。**

---

## 11. 本轮结论一览

1. **上轮的 FAIL 的两个支柱都不成立了** ⇒ 改判 **CONCERNS**。阻塞项从「编译不过」变成
   「4 条 Foundation TR + 12 项零 TR + 3 项承重引擎风险」。
2. **91 条 gap 里真正需要新 ADR 的只有 12 条**(30 的 7 条 + 13 写路径的 5 条)。
   其余是数值轮 / 实测前置 / GDD 家规 / **登记状态未同步**。
3. **本轮新登记的 8 处 RC 全部是"单件内部矛盾"或"论据失效",零 ADR-对-ADR 打脸** ——
   这本身就是 22 份 ADR 成熟度的证据(上轮 4 份时,一次评审出 10 处对打)。
4. **一个可复用的模式识别**:RC-3 与 RC-5 是**同一形状**(把"我希望编译器挡的"写成"编译器挡的")。
   全案的执法体应当**只有一类 = 构建期断言**,asmdef flag 只是它的输入。
   ADR-025 裁定 ④ 已经立了这条路,建议把 RC-3/RC-5 的修订**统一挂到裁定 ④ 之下**。
5. **QQ-11 的结案方式**(两条判得相反)推翻了「以 registry 为准」这条元规则 —— 正确的元规则是
   **「逐条回 ADR 的需求表核验」**。已把这条教训写进 `traceability-index.md` 变更历史。

## History

| Date | Verdict | 覆盖 | 冲突 | 备注 |
|------|---------|------|------|------|
| 2026-09-15 | **FAIL** | ✅49 / ⚠️19 / ❌80(148) | 10(C-1…C-10,2 编译级) | 首建 registry;4 份 ADR |
| 2026-09-15 | (consistency 复查轮)| 同上 | +8(C-11…C-18) | 见同件 §13 |
| 2026-09-20 | **CONCERNS** | ✅245 / ⚠️51 / ❌91(387) | **0 阻塞** · 8 处 RC(单件) | ADR 4→22;Required #1/#2/#3 兑现;TD C1–C4 结案;本轮**零状态翻转**,只对齐 + 新登风险 |
