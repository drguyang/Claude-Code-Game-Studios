# 采集 (Foraging & Gathering) — 17

> **Status**: ✅ **Approved**(2026-09-18 成稿 → 2026-09-19 首轮 `/design-review` = 🔴
> **MAJOR REVISION NEEDED**(scope **L**,边界偏 XL · 7 根因)→ 当日全量修订落盘(ADR-009 **Amendment K** + 涟漪六件)→
> **2026-09-19 用户裁定「接受修订、免二轮」= ✅ Approved**)
>
> > **⚠️ 免二轮 = 显式风险接受,不是「已核对」**。重开触发条件四者任一:
> > ① `OQ-17-1`(深水线 `Capacity` / `RegrowWindow` / `SEASON_MULT[]` **值**)用户裁定落地;
> > ② `OQ-17-6`(ADR-001 意图上行通道)由 45 的 GDD 轮裁定,与 `OQ-10-9` / 20-BL-4 同批;
> > ③ 42 的**采前线索呈现规格**在实现期证明不可读(根因 P0-7 的呈现事实不可分辨);
> > ④ `OQ-17-12`(`min(各输入 quality)` 链式截断)在 21a 侧被证伪。
> > 详见 `reviews/foraging-review-log.md`。
> **Author**: dr_guyang + game-designer
> **Last Updated**: 2026-09-19
> **Implements Pillar**: 支柱三(还原 × 整体 —— 「同一味药,手采的和药铺买的不一样」)· 支柱一(判断为骨 —— 辨识是高技能玩家的隐性回报)
> **上游**: 6 世界与生态区(✅ ADR-015 逻辑整数层)· 21a 物品与配方数据库(✅ `item-database.md`,提供 `gather_profile`)
> **下游**: 18 炮制(消耗原料)· 20 库存与物品(承载 `ItemInstance`)· 11 处方用药(经 18/20 间接)· 42 拟物 UI(呈现品级质地)· 7a 持久化(序列化 `ItemInstance`)
> **横向**: 30 技能与熟练度(`采集` 技能 · ✅ **Approved** —— 见 §Dependencies 注②)· 5 时间与天气(`season_index` 由 F-5.2 提供)· 3 输入与设备(采集动作)· **4 交互系统(目标选择:`ForageSpot` 路由到 17)** · 45 网络层(主机唯一铸造 `instance_id` · **意图上行通道未立**)
> **架构约束**: `adr-015-world-geometry-fixed-world-lattice.md`(§一 两层世界 · §三 单一整数格)· `adr-005-deterministic-sim.md`(病史流 · `IEventSink` · `ITickProvider`)· `adr-007-event-authority-and-roll-state.md`(掷骰的每个输入须可从事件流重构)· `adr-009-world-state-event-boundary.md`(§二 三态分类 · §三 世界流 Kind 骨架 · **Amendment K**)
> **上游文档**: `design/gdd/game-concept.md`(§P0 的边界:采集对象 = 药用植物原料)· `design/gdd/systems-index.md`(§2 #17 注 · §6 深水线归属)· `concept-benchmark.md` 断裂②(「看得见的深水线」)

---

## Summary

采集是玩家的**第一个动词**。玩家在世界里接近一株药用植物、执行采集动作、得到一件
**带品级的 `ItemInstance`**。它的全部难度在**辨识**——同一株药,低技能的玩家采下的是「陈放」,
高技能的玩家采下的是「新采带露」,而**系统从不告诉他这株本该是哪一档**。
**确定性**:品级由 `SplitMix64(WorldSeed, …)` 抽取,逐位可重放(ADR-007)。

> **⚠️ 2026-09-19 首轮评审改写(根因 P0-3「幻想代持」)**:原文把**三层**幻想全写在 17 名下,
> 而其中第一层(看得见的种类)归 **21a + 42**、第二层(感得到的作用点)归 **18** ——
> **17 只拥有第三层**,且原文从未定义「后来的对比」是什么。§Player Fantasy 已按**17 实际拥有面**
> 重写,并把另两层**显式标为 EXTERNAL**(承 `AC-5-07`/`AC-5-21` 先例)。

---

## Overview

17 是**基地层采集系统**,只做一件事:**把世界逻辑层的一株资源变成玩家背包里的一件原料**。

它的边界是**严的**:17 **不定义**品种(归 21a `gather_profile`)、**不定义**资源点在哪(归 6 的逻辑层)、
**不定义**品级分布的形状(21a 给 `quality_distribution`,形状由本节定)、**不做**结算之外的任何转化
(转化归 18 炮制 —— `item-database.md:450`「17 / 18 / 19 只产出输入参数,不得自建结算逻辑」)。
17 的职责 = **动作 + 一次确定性的品级抽取 + 一次唯一的身份铸造 + 一条采集事实事件**。

**采集的产物是原料,不是中药。** P0 采的是**药用植物原料**(柳树皮 / 毛地黄 / 金鸡纳树皮),
「炮制」在 P0 = 提取 / 干燥 / 标准化;**甘草 / 黄芪** 是 P1a 示例(承 `item-database.md` §命名铁律 ·
`skill-system.md:144-145`)。这不是 17 的选择,是 D-21-1 已裁的口径。

**17 是「深水线」的承载者之一。** 对标复核断裂②指出:固定世界弃用了空间难度轴,
玩家需要一条**看得见的难度曲线**;裁定该曲线 = **采集衰减 + 季节修正**,归属 **5 时间与天气 · 17 采集**
(`systems-index.md:456-460`)。⚠️ **该曲线的具体数值本轮未裁**(OQ-17-1 / `OQ-8-11`)——
本节只定型**它挂在哪**、**它的数据来源**、以及**它的形状必须能产出「衰减」而非「循环」**
(见 §Formulas F-17-3 与根因 P0-4)。

---

## Player Fantasy

**「这株和上一株不一样,而我知道。」**

### 17 **拥有**的那一层:认得出的熟练度(隐性)

高技能的玩家**采得到更高档**,低技能的玩家**把好料当普通料采了**(`QualityCap` 截断)。
这条是**隐性**的:系统不报「你浪费了」,玩家只在**后来的对比**里自己发现。

**「后来的对比」的对象由此定名**(首轮评审根因 P0-3 —— 原文只说「对比」不定对象 ⇒ 不可走通的推理环):

```
采到的药 → 经 18 炮制 → 服下 → 见效更快 / 更持久(21a F5 时间轴偏移)
                                    ↑
              这条差异可**逆推**回「我这一段药的品级低了」——
              而低技能玩家的品级**系统性偏低**(QualityCap 截断,非随机噪声)
```

推理环成立的**充要条件**有三条,缺一即「静默失败」而非「隐性回报」:
1. **差异大于噪声** —— 21a **D-21-24 的可感知地板**(F5 偏移须 > 9 的噪声带)是硬前置;
2. **截断是系统性的,不是随机的** —— 低技能玩家的品级分布**整体下移**(非「有时高有时低」);
3. **对比对象可比** —— 同一株植物、同一炮制法,只有采集者技能不同。

⚠️ **已知会破坏该环的下游机制**:21a F1 的 `InQ = min(各输入 quality)`(混料时取下限)
—— 玩家把高低品级药材混在一起炮制,差异被抹平,**归属链断**。这不是 17 的缺陷,
但 **17 的幻想第 3 层依赖它**。⇒ 登记为**跨系统前置**,见 §Dependencies 注⑤。

**AC 覆盖**:AC-17-13(差分夹具 —— 同种子同节点、两个 `QueryLevel`、比对 `out_quality`)。

### 17 **不拥有**的两层(EXTERNAL —— 17 侧不得记绿)

| 层 | 内容 | 谁交付 | 17 侧状态 |
| --- | --- | --- | --- |
| **看得见的种类** | `quality_character[]` 给每档一个**定性修饰**(「新采带露」「干燥」…),由 42 以外观 / 药签措辞呈现 | **21a(数据)· 42(呈现)** | ⚠️ **EXTERNAL · BLOCKED-BY-R13 / D-21-16** —— `quality_character[]` 是 **P0 可空**(`item-database.md` §Schema B2);17 **不得**要求其非空(那是 20 的 BL-25 越权,已由用户 2026-09-19 裁定「暂不裁」并外抛给 21a 下一轮,**见注⑥**) |
| **感得到的作用点** | 品级经 18 炮制后以**时间轴偏移**体现(F5),**不是**药效幅值 | **18 炮制 + 21a F5** | ⚠️ **EXTERNAL** —— 17 只负责把 `quality` 写进 `ItemInstance`;偏移端 17 无接口 |

### 立即可读的**采前**线索(EXTERNAL · BLOCKING —— 首轮评审裁定)

**问题**(根因 P0-3 的决策层):品级在**采集当下**才掷,UI 硬约束禁数字 ⇒
「蹲下去、看一眼、判断这一株值不值得采」**不是规则创造的决策** —— 玩家能判断的只有**品种**
(3 项闭集),而「这一株值不值得采」在按下采集键之前**无任何可读信号**。

**裁定(2026-09-19 用户裁定)**:登记为 **42 的阻断呈现规格** ——
**必须**为采集提供一个**采前可读**的「这一株值不值得采」线索(**非数字**、
承 42 拟物框架与 ADR-013)。**17 的职责边界 = 提供该线索的整数输入**,不设计其形态。

> ⚠️ **该线索不得泄露 `raw_quality`**(否则规则四的反幻想破)。合法形态候选:
> 节点的**外观状态**(承 `quality_character[]` 档位措辞的**可见对应物**,如叶面状态)、
> 或**余量 / 衰减**的可见对应物(承 F-17-3 `YieldDecay`)。**具体归 42 + `/ux-design`**;
> 17 侧登记为**阻断前置**(见 §UI Requirements 的 UX Flag 与 §Dependencies 注⑦)。

> **⚠️ 17 的幻想不含「稀缺」的即时反馈。** 「药越来越难采」属于深水线(5/17,OQ-17-1),
> 本轮定型**形状**(F-17-3 的 `YieldDecay` 项)但不定值。P0 的采集手感 = **辨识**,不是**搜寻**。

---

## Detailed Rules

### 规则一:采集对象 = 药用植物原料,不是中药

P0 采集的对象是**药用植物原料**,品种集是**闭集**(由 21a `item_key` 表定义,经 ADR-014 烘焙):

- **P0**:柳树皮 · 毛地黄 · **金鸡纳树皮**
- **P1a**:甘草 · 黄芪 等(承 `skill-system.md:144-145`)

**17 不得自建品种表** —— 品种、可采部位(`parts[]`)、品级分布(`quality_distribution`)、
单次采量基数(`qty_per_node`)全部读 **21a `gather_profile`**(`item-database.md:353-367`)。

> **⚠️ 2026-09-19 就地订正(P0 品种集口径)**:原稿规则一列 **3** 品种,而规则五(新颖度对象)
> 只列 **2**(漏 `金鸡纳树皮`)⇒ **文档内自相矛盾**。且 `skill-system.md:138` 的新颖度对象表
> 亦只列 2,而同档 `:144` 的正文列出 3 ⇒ **30 侧同款矛盾**。**本稿统一取 3**
> (以 21a 的 `item_key` 表为准);`skill-system.md:138` 的表已由本节回刷(见 §Cross-References)。
> **AC-17-01b** 守此闭集。

### 规则二:资源点 = 派生态定义 + 派生态余量;采集事实走 `ResourceHarvested`(Amendment K)

**资源点(node)= 定义 + 余量,两者都是派生态,均不进流。**

| 面 | 内容 | 来源 | 进流? |
| --- | --- | --- | --- |
| **定义** | `node_id` · `ecosystem` · `item_key`(品种)· `cell`(`WorldPos` 整数格) | ADR-015 逻辑层(经 ADR-014 烘 `world_resources.cooked`),**由 54 关卡工具导出** | ❌ 派生态 |
| **余量** | 该节点当前可采量 | **世界流的纯函数**(见 F-17-3) | ❌ 派生态(可重建) |

> **这是 ADR-021 同一条纪律的第二次应用**:POI「定义 = 派生态(烘焙)/ 状态 = 模拟态(进流)」;
> 资源点**更弱** —— 它的**余量也能从世界流重算**,故**连状态都不需要**。

**采集动作的产物 = 一条采集事实 + 一对身份事件(均为既有 Kind):**

玩家采集成功时,17 **发三条既有 `Kind`** —— **一对身份 + 一条采集事实**:

```
ResourceHarvested { instance_id, node_id, gather_seq, qty, out_quality }   // 采集事实(17 的 Kind)
DropSpawned       { instance_id, spawn_anchor = node.cell, item_key, qty } // 身份出生
DropClaimed       { instance_id, claimer = 采集者 }                         // 立即归属
```

> **⚠️ 2026-09-19 改判(根因 P0-1「避涟漪式改判」)—— 原稿称「零新 Kind」并只发后两条。**
>
> **为什么原口径不成立**:
> ① ADR-009 §二 三态表**已把「资源点消耗状态」归类为模拟态 → 世界流 `ResourceHarvested`**,
> §七 Guidelines 4 **明写写者 = 17**(`adr-009:193` / `:448`)。原稿从未发它 ⇒ **一个已注册
> 的 Kind 无生产者,而一个 GDD 单方改判了一份 Accepted ADR 的分类**(违 `coordination-rules` 5);
> ② **原载荷承载不了 17 自己需要的事实** —— `DropSpawned.Payload = { instance_id, spawn_anchor,
> item_key, qty }`(`adr-009:329`)**无 `node_id`、无 `quality`** ⇒ F-17-1 的 `gather_seq`
> (「该节点已发计数」)与 F-17-3 的余量重算(`Σ{ DropSpawned.e ∈ node }`)**不可实现**;
> `spawn_anchor` 是**格坐标**,而 4 的 `OQ-4-11` 明许**同格同种多点** ⇒ 用锚点当节点键会撞;
> ③ **`gather_seq` 可被无关掉落污染** —— 死亡掉落 / 18 起货溢出(`ADR-009 Amendment J ④`)
> / 52 的 `spawn_anchor` 点**都发 `DropSpawned`**,任何一条都平移该节点后续的全部品级序列;
> 玩家还可在药丛上丢东西**盲刷**序列。
>
> **改判(用户裁定 B)**:以 **`ResourceHarvested` 承载采集事实** —— 恢复孤悬的 Kind、
> `gather_seq` 有**过滤过的**唯一来源、复用 ADR-009 Amendment I 已批准的
> **「一次动作 = 多条流事件」**形态。**规避涟漪不是省下涟漪,而是连本带息地还**
> (此处表现为:一个孤悬 Kind + 一份被静默改判的 ADR + 四条不可实现的 AC)。
> 契约落 **ADR-009 Amendment K**;`ResourceHarvested` 载荷归本 GDD(承 §三「骨架先行」纪律)。

**三条事件的写者 / 时刻**:

| 事件 | 写者 | 时刻 | 载荷归 |
| --- | --- | --- | --- |
| `ResourceHarvested` | **17(主机)** | 主机裁决通过、铸 id 之后**同一 tick 连发** | **本 GDD**(ADR-009 Amendment K) |
| `DropSpawned` | 17(主机)| 同上(身份出生)| ADR-009 §五(既有)|
| `DropClaimed` | 17(主机)| 同上(立即归属)| ADR-009 §七(既有)|

> **为什么 `out_quality` 落流而 `raw_quality` 不必**:`raw_quality = CDFWalk(quality_distribution,
> U)` 而 `U = f(WorldSeed, node_id, gather_seq)` —— 三项**均在流里** ⇒ `raw_quality`
> **可重算**,无须存(存 = 第二真源)。`out_quality` 则**必须存**:它经 `QualityCap` 截断,
> 而截断需要**采集者当刻的技能等级** —— 同 21a 已裁的「`qty` 是写入时物化,非加载时重算」同一逻辑
> (`item-database.md:645` 尾注)。**AC-17-04 据此收窄为「可重算」而非「存了」。**

### 规则三:`instance_id` 铸造权 = 主机唯一(**D-21-27**)

**17 采集不得在客户端本地铸造 `instance_id`。** 唯一来源 = `IIdAuthority.Next<ItemInstanceId>()`
(ADR-010 §五 机制 A:计数器 + 高水位可重构)。

**铸造点 = `DropSpawned`(`ResourceHarvested` 同 tick 引用同一 id)。**
采集请求上行主机 → **主机掷骰 + 主机铸 id** → 主机发三条事件 → 广播。

> **这是已登记的缺口**:`item-database.md:246` 三轮 blocking #4 —— 规则九的「主机唯一」清单**漏了 17**,
> 缺口编号 **D-21-27**,验证见 **AC-21a-63**。**17 侧的兑现 = 规则三本身。**
>
> **⚠️ 2026-09-19 双向记账结清**:20 的 **R11**(「17 的铸造点口径未回写」,**BL-1 改判的下游义务**)
> 现由本节兑现 —— 铸造点 = `DropSpawned`(出生即铸),**非**「拾取时铸」;
> 20 侧 R11 已就地关闭(见 `inventory-and-items.md:868`)。
> **禁**客户端「先本地铸 id,再对账」—— 那会在迁移期**静默重号**(两件药共用一个 id,
> 表现为「物品悄悄合并」,不崩溃、不回放失配)。

### 规则四:品级抽取 = 确定性 CDF + 技能识别上限

**抽取**:21a `gather_profile.quality_distribution` 给出各档**权重**(形状由本节定,见 F-17-1),
17 以 **`SplitMix64(WorldSeed, "gather", node_id, gather_seq)`** 走 CDF,
**输入全部可从事件流重构**(承 ADR-007 §三 核心不变量)。

**识别上限**:`采集` 技能等级映射一个**可识别品级上限** `QualityCap(skill)`;
抽取结果**高于上限则被截断到上限**(你认不出好料,把它当普通料采了)。
**抽取的原始值仍由事件流可重构** —— 截断只是**玩家可见的产出**,不是掷骰本身。

**技能回报方向(承 `skill-system.md:62`)**:「可识别更高品级;单次采集药量随技能提升」——
**两条出口**:①`QualityCap` 随技能升;②单次采量随技能升(F-17-2)。

### 规则五:新颖度对象 = 药用植物品种

每次**成功采集**发**恰好一次**成长事件,对象 = **品种**(`skill-system.md:138` 明定):
**P0**:柳树皮、毛地黄、**金鸡纳树皮**各算一个对象 · **P1a**:甘草、黄芪。
成长经 30 的 `EmitGrowth(采集, 品种, novelty)`(`novelty_class` 由 30 定义,含冷却期 `stale`)。
**事件落病史流**(30 的三轮修订 B3:`SkillGrown` 落病史流 —— `skill-system.md:400`)。

> **「恰好一次」是硬约束**(首轮评审补齐 AC):同一次采集动作**不得**因多条产出事件
> (`ResourceHarvested` + `DropSpawned` + `DropClaimed`)而多发成长事件。**AC-17-05b** 守此。

### 规则六:采集动作 = 输入动作,非 UI

采集是一个**世界动作**(由 3 输入与设备的动作资产提供),**不经过 42 的焦点头**
—— 它是**对着世界实体做**,不是菜单导航。手柄与键鼠走**同一动作**。
> ⚠️ **「不经过 42」≠「不经过 4」** —— 「这一帧对哪一株」由 **4** 的 `F-4.1` 选,见 §Dependencies 注④。

**动作原子性**:只有**完整完成**的采集动作才发三条事件。
被打断(受伤 / 移动 / 目标失效)⇒ **零效果**,**不消耗节点余量**。

> **⚠️ 2026-09-19 订正(根因 P0-7 呈现事实)**:区分两件被原稿混同的事 ——
> **① 目标选择** = 4 的 `F-4.1`(**已登记**,注④);
> **② 目标提示的呈现载体** = 42,而 4 的首轮评审**已废除准星**(「脚与身位即光标」,
> `interaction-system.md:90-99`)⇒ 原 §UI Requirements 的「准星」是**已废止的载体**,见 §UI Requirements。
> **本条只承诺「同一动作」,不承诺任何指针 / 准星。**

---

## Formulas

> **本节公式在整数 / Q16.16 定点域**(承 ADR-005 / ADR-006)。**任何浮点字面量非法** ——
> 权重与系数经 `FixParse` 或整数常量进入 sim。**数值(权重、系数、上限)归用户调**
> (`technical-preferences.md` 的「数值用户自己调」纪律),本节只给**形状 + 变量表 + 调参旋钮**。

### F-17-1 · 品级抽取与识别上限

```
raw_quality   = CDFWalk( quality_distribution[], U )          // U 见下
QualityCap    = CapTable[ QueryLevel(采集) ]                  // 逐档整数表,形状见下
out_quality   = clamp( raw_quality, 1, QualityCap )
```

其中 `U` = 掷骰源的**整数归一化投影**(定死整数形式,消除原稿的「→ [0,1)(Fix)」歧义):

```
h = SplitMix64( WorldSeed, "gather", node_id, gather_seq )    // 64-bit 无符号整数
U = h >> 16                                                   // 高 48 位 → 整数域 [0, 2^48)

// CDF: 对累积权重做整数比较,半开区间右端
raw_quality = max{ q ∈ [1, MAX_QUALITY] :
                 U × Σw  <  Σ_{q' ≤ q} w[q'] × 2^48 }         // 见下「边界口径」
```

`CDFWalk` = 对**累积权重**做一次**整数比较**的分档(承 ADR-012 黄金夹具的 `CDF walk` 算子;
⚠️ 该算子在 `tr-registry.yaml` 的算子清单**仍为 `gap`** —— 见 §Open Questions **OQ-17-10**)。

**边界口径(定死,消除原稿的 `<` / `≤` 未定)**:
- 累积和 `C_q = Σ_{q' ≤ q} w[q']`(整数,精确);
- `U ∈ [0, 2^48)` 与 `C_q × 2^48 / Σw` 比较 —— **全部整数乘除**,禁浮点;
- 取**满足 `U × Σw < C_q × 2^48` 的最小 `q`**(**严格 `<`**)⇒ 各档区间为左闭右开,
  `q = 1` 的区间为 `[0, C_1)`,无重无漏;
- **`Σw = 0` 为非法输入**(见下「装载期校验」)—— `U × 0 < 0` 恒假 ⇒ 原式会**静默返回
  `max` 档**或除零,**必须在装载期拒绝**。

| 变量 | 类型 | 来源 | 说明 |
| --- | --- | --- | --- |
| `quality_distribution[]` | **int 数组(权重,个数)** | 21a `gather_profile` | **类型定死为 `int`**(承 **D-21-17**:`weight` 是整数最小单位**个数**,**移出 `Fix` 解析集**;原稿写「int / Fix」= 复活该类错误);支撑 ⊆ `[1, MAX_QUALITY]`(21a 约束)+ **`Σw ≥ 1`**(本节新增,见装载期校验)|
| `node_id` | int | ADR-015 逻辑层 | 节点稳定标识 |
| `gather_seq` | int | **该节点**已发出的 `ResourceHarvested` 计数(**世界流纯函数**) | 保证同一节点多次采集的抽取序列可重放;**只数 17 自己的 Kind** ⇒ 不被掉落 / 起货溢出污染(见规则二) |
| `CapTable[]` | **int 数组(长度 61 = `SKILL_CAP + 1`)** | 本节 | `查询等级 → 可识别品级上限`;**单调不减**;`CapTable[0] ≥ 1`;`CapTable[L] ≤ MAX_QUALITY`(全部档位) |
| `raw_quality` | int ∈ `[1, MAX_QUALITY]` | — | **不落流**(可由 `h` 重算);仅中间量 |
| `out_quality` | int ∈ `[1, MAX_QUALITY]` | — | 落 `ItemInstance.quality` + `ResourceHarvested.out_quality` |

> **装载期校验(本节新增 —— 原稿三处真空)**:
> ① **`Σw ≥ 1`** —— 21a 只校验「支撑 ⊆ `[1, MAX_QUALITY]`」,全零分布**双向皆过**
>    ⇒ 必须在此**硬失败**(显式 `throw`,非 `Debug.Assert`),否则 `CDFWalk` 除零 / 静默返回最大档;
> ② **`CapTable` 长度 = 61** 且**单调不减**且 **`CapTable[0] ≥ 1`** 且 **`≤ MAX_QUALITY`**
>    —— 原稿只断「单调 + `CapTable[0] == 最低档`」,`CapTable[0] = 0` 会让 `clamp(x, 1, 0)`
>    在 C# 抛异常(`min > max`);
> ③ **`MAX_QUALITY` 同值契约** —— `CapTable` 各值 ≤ 21a 的 `MAX_QUALITY`(D-21-7 双向耦合)。

> **为什么 `gather_seq` 用「该节点已采次数」而不是全局计数器**:全局计数器 = 第二个真源
> (须存档、须迁移)。**已采次数可从世界流重算**(数该 `node_id` 上的 `ResourceHarvested`),
> **零新状态**。这是 ADR-009 §一 Q1 判据的直接应用。

### F-17-2 · 单次采量

```
qty = Mul( qty_per_node, GatherMul( QueryLevel(采集) ) )
```

| 变量 | 类型 | 来源 | 说明 |
| --- | --- | --- | --- |
| `qty_per_node` | int > 0 | 21a `gather_profile` | **基数**(21a 给基数,17 给动作) |
| `GatherMul(L)` | `Fix` ∈ `[1, GATHER_MUL_CAP]` | 本节 | 技能乘子;**单调不减**;`GatherMul(0) = 1`(低技能不惩罚,只少给);**上界 `GATHER_MUL_CAP` 须定**(原稿「上界待定」⇒ 长程通胀无界,见 §Open Questions)|
| `qty` | int ≥ 1 | — | 落 `ResourceHarvested.qty` + `DropSpawned.qty` |

**求值口径(原稿未定,首轮评审补齐)**:
- **提升为定点**:`qty_per_node`(int)在乘法前**提升为 `Fix`**(`Fix.FromInt`),乘积为 `Fix`;
- **唯一舍入点是 `Mul` 之后的取整**,模式承 ADR-006 **`ROUND_HALF_AWAY_FROM_ZERO`**(显式命名算子,
  **禁 `Math.Round` 默认 ties-to-even**);
- **`GatherMul ≥ 1` + 单调不减 + 正向上舍 ⇒ `qty` 对 L 单调不减**(此性质 F-17-2 **保持成立**,
  经三轮评审复核 = 本条唯一无缺陷项);
- **`qty ≥ 1` 由 `qty_per_node ≥ 1 ∧ GatherMul(0) = 1` 保证**(半程上舍入不会降到 0)。

### F-17-3 · 节点余量(派生态 · **OQ-17-1 未裁值**)

```
EffCapacity(node, t) = BaseCapacity(node) × SEASON_MULT[season_index(t)] / MUL_ONE
                                    × YieldDecay( Σ_all qty @ node )      // ← 新增:衰减项
remaining(node, t)   = max( 0 , EffCapacity(node, t) − Fix( Σ_window qty @ node ) )

Σ_window qty @ node = Σ{ e.qty : e ∈ ResourceHarvested @ node , t − Tick(e) < RegrowWindow }
Σ_all    qty @ node = Σ{ e.qty : e ∈ ResourceHarvested @ node }            // 全时段,永不回退
yieldDecay(n)       = max( DECAY_FLOOR , MUL_ONE − Mul(n, DECAY_RATE) )    // 单调不增,有下限
season_index(t)     由 5 的 F-5.2 提供(2026-09-18 · `OQ-5-1`)
```

| 变量 | 归属 | 状态 |
| --- | --- | --- |
| `Capacity(node, t)` | 每节点容量(**含季节项**) | ⚠️ **值未裁** —— 深水线(OQ-17-1) |
| `BaseCapacity(node)` | 与季节无关的基数 | ⚠️ **值未裁** —— 深水线(OQ-17-1) |
| **`YieldDecay(n)`** · `DECAY_RATE` · `DECAY_FLOOR` | **本节**(形状) | ✅ **形状已定(2026-09-19 首轮评审)**;**值未裁**(数值轮) |
| `RegrowWindow` | 再生窗 | ⚠️ **值未裁**(OQ-17-1);**形状约束 `> 0`**(见下) |
| **`SEASON_MULT[]`** | **本节**(长度 = 5 的 `SEASONS_PER_YEAR`,Q16.16)| ✅ **形状已定**(`OQ-5-1` 2026-09-18);**值未裁** |
| `season_index(t)` | **5 的 F-5.2** | ✅ 已定(整数下标,**5 的唯一交付物**) |

**求值口径(原稿三处不可实现,首轮评审补齐 —— 根因 P0-4)**:

1. **量纲齐一**:`EffCapacity` 是 `Fix`;`Σ_window qty` 是 **int 计数**
   ⇒ **先 `Fix.FromInt` 提升再相减**(原稿 `Fix − int` 是 D-21-19 同类的量纲不齐);
   `/ MUL_ONE` 的**截断舍入须显式命名**(承 ADR-006 单一舍入模式),**禁隐式整除**;
2. **采前闸是 `remaining ≥ Fix(qty)`,不是 `remaining > 0`** —— 原稿写「`remaining == 0` ⇒ no-op」,
   但 `remaining ∈ (0, 1)` 时原式**放行**一次 `qty ≥ 1` 的采集 ⇒ **余量被采穿**(欠账)。
   改为:**`remaining(node,t) ≥ Fix(qty)` 才可采**;取等 = 刚好采完(合法);
3. **`RegrowWindow ≤ 0` 装载期硬失败** —— 否则「无再生」退化为「无限采集」(静默);
4. **`remaining` 是「世界流在 `t` 的**前缀**的纯函数」** —— 原稿与 AC-17-06 都丢了 `t` 参数;
   `ResourceHarvested` 只在**已确立前缀**内计数(承 ADR-010 / 45 的 `MaxLag` 口径:
   客户端本地副本可能滞后一条 ⇒ **判据只由主机在裁决时求值**,见 §Open Questions OQ-17-6)。

> **⚠️ 2026-09-19 形状改判(根因 P0-4「深度线形状自相矛盾」)**
>
> **原稿的形状产不出它承诺的曲线**:F-17-3 只有**滑窗 + 周期季节**两项 ——
> 窗口一过则余量**完全恢复**,季节项**周期回归** ⇒ 该式**结构上不可能**产出
> 「药越来越难采」(深水线 `concept-benchmark.md:82` 的承诺)。
> 那是**形状缺陷**,不是数值待定 ⇒ **不得推给数值轮**。
>
> **改判**:拆成**两项** ——
> - **`Σ_window`(再生项)= 短周期恢复**(采过一丛,过阵子又长回来)= 原稿语义;
> - **`YieldDecay(Σ_all)`(衰减项)= 单调不增、永不回退** = 深水线的「衰减」半截。
>
> **`Σ_all` 可重建**(只数 `ResourceHarvested`)⇒ 派生态判据仍成立,**零新状态**。
> 衰减项的形状**逐节点累积**;若用户要**全局衰减**(整山采秃),那是**另一种形状**
> (须再引入世界级累积量),登记为 OQ-17-1 的形状二选一。**两种形状的值都归用户。**

> **✅ 2026-09-18 回填(`OQ-5-1` 结清 —— 季节项落点)**:深水线的**季节修正**半截**形式已定**:
> **5 出 `season_index`(整数下标),17 查 `SEASON_MULT[]` 表并做乘法** —— **表归本节(17),不归 5**。
> **⚠️ 定点域纪律**:`SEASON_MULT` 是 **Q16.16 `Fix`**,故**必须住 `assets/data/` 的 JSON、
> 以 `FixParse` 可接受的字符串分数写法进**(ADR-014 / ADR-006),**禁浮点字面量**。
> **⚠️ `FixParse` 只在烘焙期执行** —— 玩家构建**零解析器**;改表值 ⇒ **推导出的 `ConfigVersion`
> 随之改变**(ADR-014 §五)⇒ 影响存档兼容比对(ADR-010 §七)。**⚠️ 5 侧不得出现任何季节系数字段**(`AC-5-16`)。

### 边界探针(systems-designer 要求:代极值,报退化)

| 极值输入 | 原稿行为 | 修订后行为 |
| --- | --- | --- |
| `quality_distribution = [0, 0, 0]` | 除零 / 静默返回最大档 | **装载期硬失败**(`Σw ≥ 1`) |
| `CapTable[0] = 0` | `clamp(x, 1, 0)` 抛异常 | **装载期硬失败**(`CapTable[0] ≥ 1`) |
| `CapTable[60] > MAX_QUALITY` | `out_quality` 越界读 | **装载期硬失败** |
| `RegrowWindow = 0` | 退化为「无限采集」 | **装载期硬失败**(`> 0`) |
| `remaining = 0.5`(Fix),`qty = 1` | **放行**(采穿) | **拒**(`remaining ≥ qty` 不成立) |
| `GatherMul(L) = 1` 全档 | `qty` 恒 = `qty_per_node` | 合法(低技能不惩罚) |
| 单档 `w = [1, 0, 0, 0]` | `out_quality` 恒 1 | 合法(但「单档 100% = 品级无信息」,见 Tuning Knobs 警告) |

---

## Edge Cases

| 场景 | 结果 | 依据 |
| --- | --- | --- |
| **库存满载** | 采集**失败**:不发 `ResourceHarvested` / `DropSpawned` / `DropClaimed`,**不消耗节点余量**,不发成长事件 | 20 的容量预算是上游;17 不得静默吞物 |
| **节点余量不足**(`remaining < qty`)| 采集动作 **no-op**(发**拒绝反馈——「余量不足」类**,见 §UI Requirements;**零事件**) | F-17-3 求值口径 ② |
| **多玩家同 tick 采同一节点** | **主机串行裁决**,`gather_seq` 按裁决顺序递增;第二个玩家**采到的是递减后的余量**下的一次独立抽取;**仲裁失败方 = 零事件 + 拒绝反馈**(见下「失败收敛」)| 主机唯一 Append(ADR-005);`gather_seq` 保证可重放 |
| **采集途中被打断**(受伤 / 移动 / 目标失效) | **零效果**:不发事件、不消耗余量、不发成长 | 规则六 原子性 |
| **客户端本地铸造 `instance_id`** | **禁止** —— 见规则三 / AC-21a-63;违反 = 静默重号 | D-21-27 |
| **`quality_character[]` 为空(P0 可空)** | 采集照常(品级仍在);42 退回按档位的**非文字**呈现 —— ⚠️ **但该回退无所有者 / 无规格 / 无 AC**,登记为 **EXTERNAL**(见 §Dependencies 注⑥)| `item-database.md` §Schema B2 |
| **`QualityCap` 截断后 `raw_quality` 丢失** | **玩家不可感知,但事件流可重算**(`h` 由 `WorldSeed` / `node_id` / `gather_seq` 三项重算)—— 17 **不呈现**「你本可采到更高档」 | 规则四 / 规则二 注 |
| **采集 P1a 品种** | **不发生** —— P0 品种集是闭集(规则一) | 规则一 |
| **同一节点被反复采集**(刷子) | 允许,但受 `remaining`(含 `YieldDecay` 衰减)**与**新颖度冷却(30 的 `stale` ×0.2)双重约束 | F-17-3 + `skill-system.md` §3.2 |
| **`WorldSeed` 未知(首次世界)** | 不存在 —— `WorldSeed` 是存档头字段(ADR-007 §二 / ADR-010 §一),世界创建时即定 | ADR-007 §二 |
| **采集产物的 `qty` 溢出 20 的堆叠键** | 归 20 处理(`(item_key, quality)` 堆叠);17 只发单次量 | 20 的 GDD |
| **玩家在药丛格上丢东西**(原稿的盲刷路径) | **不再影响抽取** —— `gather_seq` 只数 `ResourceHarvested`,不数 `DropSpawned` | 规则二(根因 P0-1) |

### 失败收敛(首轮评审新增 —— 根因 P0-2)

**问题**:客户端的采集是**预表现**(动作动画立刻起),而裁决在主机。当主机**拒绝**
(库存满载 / 余量不足 / 仲裁失败)时,流里**零事件**(20 规则八**禁**向 45 要 ack),
⇒ 玩家看到**完成的动作 + 空手**,而 「**失败**」「**消息丢了**」「**主机没处理**」
**三者不可区分**。这违反本仓的「no hand-waving」纪律。

**裁决**:17 必须**定义可区分的失败收敛**,三条**各自具名、各有载体**:

| 失败类 | 玩家可见(17 侧的**语义**,载体归 42/44) | 正确响应 |
| --- | --- | --- |
| **库存满载** | 「**箱子满了**」类反馈 | 清箱子 |
| **余量不足** | 「**这株没有了**」类反馈 | 换一株 |
| **超时未裁决**(上行丢 / 主机未处理) | 「**没成**」类反馈(动作回滚) | 重试 |

> **⚠️ 两类失败的正确响应相反,but 原稿把二者塞进同一条「可播放」的可选反馈**
> ⇒ 语义不可分。**「可」播放 ⇒ 反馈本身可选** = 手-wave;修订为**三条必经反馈**,
> 每条要一条 AC(**AC-17-07b** / **AC-17-07c**)。
> **载体(动画 / 音效 / 触感)归 42 / 44**;17 拥有**区分**与**语义**。
> **超时未裁决**依赖意图上行通道的可靠性 ⇒ **EXTERNAL · BLOCKED-BY-OQ-17-6**。

---

## Dependencies

### 上游(17 依赖的)

| 上游 | 方向 | 性质 | 要什么 | 状态 |
| --- | --- | --- | --- | --- |
| **6 世界与生态区** | 6 → 17 | 数据(派生态) | 资源点 `node` 定义(`ecosystem` / `cell` / `item_key`),由 54 关卡工具导出、ADR-014 烘焙 | ✅ ADR-015(6 的 GDD 未写) |
| **21a 物品与配方数据库** | 21a → 17 | 数据 | `gather_profile`(品种 / `parts[]` / `quality_distribution` / `qty_per_node` / `quality_character[]`) | ✅ `item-database.md` |
| **30 技能与熟练度** | 30 → 17 | 接口 | `QueryLevel(采集)` · `EmitGrowth(采集, 品种, novelty)` | ✅ **Approved**(2026-09-18) |
| **4 交互系统** | 4 → 17 | 意图路由 | **目标选择**:采集点(`ForageSpot`)与掉落物(`Drop`)的「这一帧对谁做」由 4 的 `F-4.1` 定,路由到 17 / 20 | ✅🟡 `interaction-system.md` |
| **3 输入与设备** | 3 → 17 | 动作 | 采集动作(binding) | ✅ `input-system.md` |
| **1 玩家控制器** | 1 → 17 | 运行时 | 采集者的身份 / 位置(经世界流跨格事件) | ✅ `player-controller-and-movement.md` |
| **`IDataProvider`(边界接口)** | 载体 → 17 | 装载 | 读 `world_resources.cooked` / `gather_profile` / `SEASON_MULT[]`(经 **ADR-014** 烘焙产物;sim **不得**直接触 Addressables)| ⚠️ **EXTERNAL** —— 门 A 边界程序集,归 ADR-014 §五 / 实现期 |
| **世界流查询接口** | 载体 → 17 | 运行时 | `Σ_window / Σ_all qty @ node` · `gather_seq`(计数 `ResourceHarvested`)| ⚠️ **EXTERNAL** —— 见 OQ-17-7 |

> **⚠️ 2026-09-19 新增两行(根因 P0-6)**:原稿的依赖表**无装载 / 无流查询边界**。
> F-17-3 需要**任何可达节点**的 `BaseCapacity(node)`,而 sim **不得直接**读 Addressables
> (门 A:sim 零 `UnityEngine`)⇒ 装载必须经具名边界接口(`IDataProvider`);
> `Σ / gather_seq` 须经具名**流查询**接口(不得裸扫)。**缺此二行 ⇒ 17 照原稿不可实现。**

### 下游(依赖 17 的)

| 下游 | 17 给它什么 | 状态 |
| --- | --- | --- |
| **18 炮制** | 原料 `ItemInstance`(`item_key` + `quality` + `qty`)| 未写 |
| **20 库存与物品** | 同上(承载 + 堆叠键 `(item_key, quality)`);⚠️ **20 的 `InventoryOf` fold 须并入 `ResourceHarvested`** 以取回 `quality`(见注⑧)| 🟡 `inventory-and-items.md` |
| **7a 持久化** | `ItemInstance` 序列化(经 ADR-010 §三 义务 9/10)| ✅ `persistence-service.md` |
| **42 拟物 UI** | 品级的**定性**呈现输入(`quality_character[]`)+ **采前线索的整数输入**(§Player Fantasy)| ✅ `skeuomorphic-ui.md` |
| **45 网络层** | 三条世界流事件的传输(P1b)| P1b 未写 |
| **5 时间与天气** | **深水线的另一半**(季节修正)—— 17 出「采集衰减」,5 **只出 `season_index`(F-5.2)** | ✅ `time-and-weather.md` |

> **注①(双向性)**:21a 的「下游」清单已列 17(`item-database.md:11` / `:824`),反向依赖成立;
> 30 的「下游」清单已列「采集 / 炮制系统」(`skill-system.md:455`),反向依赖成立。
> **注②(30 的状态 · 2026-09-19 订正)**:原记 30 为 **Needs Revision** = **陈旧** ——
> 30 现已 ✅ **Approved**(2026-09-18 免二轮,`skill-system.md:3`)。17 **消费**其接口形状
> (`EmitGrowth` / `QueryLevel`),若 30 后续修订改名,17 侧按契约同步(`OQ-17-4`)。
> **注③(5 的状态 · 2026-09-19 订正)**:原记「5 无 GDD」= 陈旧 —— 5 已成稿且 ✅ Approved;
> F-5.2 的 `season_index` 交付口径与本节 F-17-3 一致(`OQ-5-1` 结清)。
> **深水线的曲线值仍未裁(OQ-17-1,数值轮)** —— 缺的只是值。
> **注④(4 的边)**:规则六只排除 42,**没说 4**;「这一帧对准哪一株」由 4 的 `F-4.1` 定。
> ⚠️ 4 侧揭出的**呈现缺口**:同格同种的多个采集点并列时,4 **无法让玩家挑**
> (只能由稳定 id 定胜)—— 该债挂在 `interaction-system.md` 的 **`OQ-4-11`**。
> **自本轮起 17 把它登记为 playtest 门**(原稿写「不归 17」= 推责;见 OQ-17-11)。
> **注⑤(幻想第 3 层的下游依赖 · 2026-09-19 新增)**:21a F1 的 `InQ = min(各输入 quality)`
> (`item-database.md:496` / `:596`)在混料时**抹平品级差异** ⇒ **断掉** §Player Fantasy 的推理环。
> 这是 21a 的机制,17 不越权改;登记为**幻想成立的前置**(playtest 门,OQ-17-12)。
> **注⑥(`quality_character[]` 的**两件不同的事** —— 不得捆绑 · 2026-09-19 新增)**:
> ① **不得要求非空** —— `quality_character[]` P0 可空是 21a 已裁的 **D-21-16**;
> 20 的 **BL-25** 曾要求「非空」= **越权改 21a 已批准之条**,已被用户 2026-09-19 裁定
> 「暂不裁,外抛 21a 下一轮(**R13**)」。17 **不做**同一要求。
> ② **但可登记一条独立的新发现给 21a**:`quality_character[]` 的**取值语义**在本系统不成立 ——
> 「**陈放**」「**虫蛀**」是**采后 / 储藏期**属性,而 **P0 无仓储系统、无腐坏机制** ⇒
> 这两档修饰**在 P0 不可达**(医学专家受众的**首次出戏点**,硬约束)。
> ⇒ 登记 **`OQ-17-9`**,4 项 / 42 / 21a 同批。
> **注⑦(42 的**采前线索** = 阻断前置 · 2026-09-19 新增)**:§Player Fantasy 的「采前可读线索」
> 是**42 的阻断呈现规格** —— 「蹲下判断」这一幻想在机制上成立**当且仅当**该线索存在。
> 未定形态前,17 的 epics **不得**开工(见 §UI Requirements 的 UX Flag)。
> **注⑧(20 的 fold 涟漪 · 2026-09-19 新增)**:`quality` 现落 **`ResourceHarvested.out_quality`**
> (非 `DropSpawned`)⇒ **20 的规则一 fold 谓词须并入 `ResourceHarvested`**,
> 否则 `(item_key, quality)` 堆叠键**重建不出** `quality`,`AC-20-03` 的逐位重建**不成立**。
> 登记为 20 的下游义务(见 `inventory-and-items.md` 的 R11 关闭注 + 新增涟漪行)。

### 架构依赖

| ADR | 关系 | 落点 |
| --- | --- | --- |
| **ADR-015** | 契约 | 资源点住逻辑整数层 · `cell` = `WorldPos` 单一整数格 |
| **ADR-005** | 契约 | 主机唯一执行 `Step` / Append;`ITickProvider` 供 `t` |
| **ADR-007** | 契约 | 掷骰每个输入可从事件流重构(核心不变量)|
| **ADR-009** | 契约 | 世界流三态分类 · Kind 骨架 · **`ResourceHarvested` 载荷 = Amendment K(2026-09-19)** |
| **ADR-010** | 契约 | `IIdAuthority.Next<ItemInstanceId>()` 机制 A;`ConfigVersion` 派生(经 ADR-014)|
| **ADR-012** | 契约 | `SplitMix64` / `CDF walk` 的跨平台逐位性(黄金夹具 —— **尚未存在**,见 AC-17-02)|
| **ADR-014** | 契约 | `world_resources.cooked` 烘焙 · `SEASON_MULT` 走字符串字面量 · 玩家构建零解析器 |
| **ADR-006** | 契约 | `ROUND_HALF_AWAY_FROM_ZERO` 单一舍入模式 · `weight` 是 `int`(D-21-17)|

---

## Tuning Knobs

> **数值用户自己调** —— 本节只给**旋钮形状与边界**,不给定值。每条标注「改它有谁会破」。

| 旋钮 | 默认 | 安全区间 | 改谁 | 改坏的后果 |
| --- | --- | --- | --- | --- |
| `CapTable[]`(→ F-17-1)| 见 OQ-17-2(未定)| **长度 61** · 单调不减 · **`CapTable[0] ≥ 1`** · **各值 ≤ `MAX_QUALITY`** | 品级识别曲线 | 非单调 = 升级反而采到更差(直接出戏);`CapTable[0] = 0` = `clamp` 抛异常 |
| `quality_distribution[]`(→ F-17-1,归 21a)| 见 21a | 权重 **`int ≥ 0`** · 支撑 ⊆ `[1, MAX_QUALITY]` · **`Σw ≥ 1`** | 品级分布 | 全零 = 除零(装载期拒);单档 100% = 品级无信息(锚点二失效) |
| `GatherMul(L)`(→ F-17-2)| 未定 | `≥ 1` · 单调不减 · **上界 `GATHER_MUL_CAP`(待裁, OQ-17-2)** | 单次采量曲线 | `GatherMul(0) < 1` = 双重惩罚;无上界 = 长程通胀 |
| `qty_per_node`(归 21a)| 见 21a | `> 0` | 采量基数 | — |
| `BaseCapacity` / `RegrowWindow`(→ F-17-3)| **未定(OQ-17-1)** | `RegrowWindow > 0` | **深水线**(再生半截)| `RegrowWindow ≤ 0` = 无限采集(装载期拒)|
| **`YieldDecay`** · `DECAY_RATE` · `DECAY_FLOOR`(→ F-17-3 · **本节持有 · 2026-09-19 新增**)| **形状已定,值未定**(数值轮)| 单调不增 · `DECAY_FLOOR > 0` | **深水线**(衰减半截)| 值错 = 世界经济崩溃 / 药永远采不完;**无此项 = 深水线不闭合** |
| **`SEASON_MULT[]`**(→ F-17-3 · **本节持有**)| **形状已定,值未定**(数值轮)| 表长 = 5 的 `SEASONS_PER_YEAR`;Q16.16 | 季节调制采集 | ⚠️ **归本节不归 5**(`OQ-5-1`);改值 ⇒ 改推导 `ConfigVersion`(存档比对)|
| 采集动作时长 | 未定 | — | 手感 | 见 §Game Feel |

---

## Visual/Audio Requirements

- **采集的视觉 = 动作 + 品级的隐性呈现。** 17 **不新增任何 VFX 规格**;
  采集动作的动画与「采到什么」的反馈走 **42**(拟物 UI)与 **44**(音频,`AudioCueDto`)。
- **品级绝不数字化**(承 21a U-1/U-2 视觉锚点):`quality_character[]` 的**文字 / 外观**由 42 呈现;
  **17 不产出任何「品级 = 数字」的呈现**。
- **音频归属**:采集成功 / 失败 ∈ **行为反馈白名单**(ADR-018 §六);**禁**用音效**播报品级高低**
  (那是「报状态」,违反无提示音铁律 —— AC-44-09)。
  > ⚠️ **2026-09-19 补强(首轮评审)**:原稿只有**散文禁令**。需一条**可断言**的判据 ——
  > 自然实现选择「好料听起来更好」会**绕过** ADR-018 §六(其 `trigger_source` 断言只抓**时机**,
  > 不抓**内容参数随品级变化**)。**成功 / 失败可听是合法的**;被禁的是**成功音内部按品级分档**。
  > 登记 **AC-17-09b**(在 `AudioCueDto` 上断言:采集音的载荷**不含品质维**)。

> **本节按采集体量只给边界,不产出素材规格** —— 素材清单归 `/art-bible` / 44 的 sound-bible。

---

## Game Feel

| 面 | 目标 | 依据 |
| --- | --- | --- |
| **Feel Reference** | 蹲下 → 手起 → 药入手,一气呵成;**拒绝反馈要短促**,不卡顿 | 承 42 拟物 |
| **Input Responsiveness** | 采集是**普通动作**,不受 `TR-concept-007`(<50 ms)约束(那是急救)| `technical-preferences.md:70` |
| **Animation Feel Targets** | 采集动作**有前后摇**;打断点在**中段之前**可零效果中止 | 规则六 原子性 |
| **Impact Moments** | 「采到」的瞬间 = 药签 / 纸页声音(**行为反馈**,非状态播报)| ADR-018 §六 白名单 |
| **Weight and Responsiveness** | 采药**轻**;重的是**判断**(采不采、采哪株),不是动作 | 支柱一 |

**Feel Acceptance Criteria:** 见 AC-17-12。

---

## UI Requirements

| 信息 | 呈现位置 | 更新频率 | 条件 |
| --- | --- | --- | --- |
| **可采提示** | ⚠️ **世界内,承 4 的「脚与身位即光标」**(见下 2026-09-19 订正)**不是准星** | 站位进入节点邻域时 | 节点 `remaining ≥ qty` |
| **采前线索**(「值不值得采」)| ⚠️ **42 的阻断呈现规格** —— 非数字(EXTERNAL,见 §Player Fantasy)| 靠近时 | — |
| **品级质地** | 药签 / 外观(经 42)| 采到后 | `quality_character[]` 非空 |
| **采集失败**(满载 / 余量不足 / 超时)| **三条各自具名的反馈**,非弹窗、非文字(见 §Edge Cases 失败收敛)| 失败时 | 三类各一 |

> **⚠️ 2026-09-19 订正(根因 P0-7 · 「准星」是被 4 废除的载体)**:原稿写「**准星** / 世界内」。
> 但 **4 的首轮评审已改判为「脚与身位即光标」**(`interaction-system.md:90-99`):
> 目标指示 = **身形落点与身位姿态**,并明写「**描边高亮 + 按键标签**」是
> 「**欺骗性反馈,比无反馈更糟**」(`:966`);`F-4.1` 的三个键 `(格距, 种类优先级, 稳定 id)`
> **无朝向项** ⇒ **准星在机制上已不存在**,且手柄本就是无指针。
> ⇒ **17 的「准星」是文档里的陈旧事实,已改为承 4 的「身位」模型。**
> 载体形态归 **42 + `/ux-design`**(见 UX Flag)。

> **硬约束(反幻想)**:
> - **绝不**显示「本可采到更高品级」的提示(规则四 · AC-17-09)。
> - **绝不**显示节点的剩余量数字(F-17-3 是内部量)。
> - **绝不**显示采集经验条 / 升级提示(承 `game-concept.md` 视觉锚点:技能非数值呈现)。

> **📌 UX Flag —— 采集(2026-09-19 重写)**:本系统有**世界内**交互呈现需求。
> **两项阻断前置**由 42 交付,且**在未定形态前不得开工**:
> ① **采前线索**(「这株值不值得采」,**非数字** —— §Player Fantasy 的决策层承载);
> ② **「可采」提示的载体**(承 4 的「身位即光标」,**禁准星 / 禁描边高亮**);
> ③ **三类失败反馈的载体**(§Edge Cases 失败收敛)。
> **⚠️ 本 Flag 不得把机制裁定外抛**:4 的首轮评审已确立「同格候选**要不要区分**」
> 是**机制裁定**(归 `OQ-4-11`),不是美术题 —— 17 只**登记**它(OQ-17-11),
> **不推给 `/ux-design`**。**手柄路径**须在 flag 内明写(手柄无指针)。

---

## Cross-References

| 引用 | 位置 | 内容 |
| --- | --- | --- |
| `item-database.md` | §Schema C(`gather_profile`)· §D-21-16 / D-21-17 / D-21-27 · §AC-21a-63 | 品种 / 品级分布 / 铸造权缺口 / `weight` 是 `int` |
| `item-database.md` | §D-21-1 / §D-21-24 / §Schema B2 · §F1 `min(quality)` | 采集对象 = 原料 · `quality_character[]` · 幻想第 3 层的混料断链 |
| `skill-system.md` | §3.1 · §3.2(新颖度对象表)· §455 | 采集技能 · 对象 = 品种(**其 P0 对象表漏 `金鸡纳树皮`,与本节规则一订正对齐**)| 
| `inventory-and-items.md` | R11(已关闭)· 规则一 fold · AC-20-03 | 铸造点口径结清 · **fold 须并入 `ResourceHarvested`** |
| `interaction-system.md` | `:90-99`(身位即光标)· `OQ-4-11` | 呈现载体改判 · 同格候选的呈现缺口 |
| `world-and-ecozones.md` | ADR-015 §一 / §三 | 逻辑整数层 · 单一整数格 |
| `adr-009` | §二(三态表)· §三(Kind 骨架)· **Amendment K** | 物品实例身份 → 世界流 · **`ResourceHarvested` 载荷** |
| `adr-010` | §三 义务 9/10 · §五 机制 A | `ItemInstance` 序列化 · `IIdAuthority` |
| `adr-014` | §五(载 / 数据边界)· `ConfigVersion` 派生 | `IDataProvider` · 烘焙 ·
| `systems-index.md` | §2 #17 注 · §6 深水线归属 | 边界 · 深水线归 5/17(OQ-17-1) |
| `concept-benchmark.md` | 断裂②(「看得见的深水线」)| 形状须能产出**衰减**(F-17-3 `YieldDecay`)|
| `random-events.md`(52)| `:213-215` / `:1001` | 「52 不接深水线坐标分量」的反向登记 |

> **⚠️ 双向性待补(登记)**:6 的 GDD **尚未撰写** —— 撰写时须在 §Dependencies 补一行
> 「**17 采集** | 6 → 17 | 数据 | 资源点逻辑层定义」。

---

## Acceptance Criteria

> **分组 · 级别列(承 5 首轮评审 B-8 的全表改造纪律)**:
> 证据类型 **`[A]` = 可自动化**(单测 / 静态检查 / 反射)· **`[L]` = 需人工听测 / 走查**;
> 级别 **`BLOCKING` / `ADVISORY` / `EXTERNAL`**。
> **`EXTERNAL` = 判据成立与否取决于 17 之外的件 ⇒ 17 侧不得记绿**(承「借来的绿」禁令,
> 先例 `AC-5-07` / `AC-5-21`)。

### A 组 · 事件与身份(规则二 / 三)

| ID | 类型 | 级别 | 判据 |
| --- | --- | --- | --- |
| **AC-17-01** | [A] | BLOCKING | `GIVEN` 一次成功采集,`WHEN` 检索产出路径,`THEN` 恰发 **`ResourceHarvested` + `DropSpawned` + `DropClaimed`** 三条既有 Kind,**零新增 Kind**(规则二)|
| **AC-17-01b** | [A] | BLOCKING | `GIVEN` P0 品种集,`WHEN` 与 21a `item_key` 表比对,`THEN` = {柳树皮, 毛地黄, 金鸡纳树皮} **三者**(规则一;守文档内 3 vs 2 矛盾)|
| **AC-17-02** | [A] | **EXTERNAL · BLOCKED-BY-ADR-012** | `GIVEN` 同一 `(WorldSeed, node_id, gather_seq)`,`WHEN` 跨平台重放,`THEN` `out_quality` 逐位相同。⚠️ **判据载体 = ADR-012 三格矩阵,而它尚不存在**(`tests/` / `.github/workflows/` 缺)+ **F7 spike 未跑**(`SplitMix64` 的 `z *= …` 在 IL2CPP 为 UB)⇒ **17 侧不得记绿**(F-17-1)|
| **AC-17-03** | [A] | BLOCKING | `GIVEN` `quality_distribution` 的边界(单档 100%),`WHEN` 走 `CDFWalk`,`THEN` `out_quality` 恒等该档,无除零 / 越界;⚠️ **`QualityCap` 低时该夹具须写为「恒 = 该档 ∩ ≤ Cap」**(原稿结论在低 Cap 下为假)|
| **AC-17-04** | [A] | BLOCKING | `GIVEN` `raw_quality > QualityCap`,`WHEN` 结算,`THEN` `out_quality == QualityCap`,**且 `raw_quality` 可从 `(WorldSeed, node_id, gather_seq)` 重算**(规则四 / 规则二 注)|
| **AC-17-05** | [A] | BLOCKING | `GIVEN` 采集在客户端执行,`WHEN` **反射扫描 sim 程序集的 `ItemInstanceId` 构造点**(type-surface 反射,**非 grep**),`THEN` 唯一来源 = `IIdAuthority`,**客户端零铸造**(D-21-27)|
| **AC-17-05b** | [A] | BLOCKING | `GIVEN` 一次成功采集,`WHEN` 检索成长事件,`THEN` **恰好一条** `SkillGrown`(对象 = 品种),不因三条产出事件多发(规则五)|
| **AC-17-05c** | [A] | **EXTERNAL · BLOCKED-BY-R13** | `GIVEN` 21a 交付的 `quality_character[]`,`WHEN` 人工走查,`THEN` **取值在本系统语义成立**(无「陈放」「虫蛀」类**采后属性**)。⚠️ **`quality_character[]` P0 可空是 21a 已裁之条**;逾期未裁 ⇒ 17 侧不得记绿(注⑥ / OQ-17-9)|

### B 组 · 余量与动作(规则六 / F-17-2 / F-17-3)

| ID | 类型 | 级别 | 判据 |
| --- | --- | --- | --- |
| **AC-17-06** | [A] | BLOCKING | `GIVEN` `remaining(node, t) < Fix(qty)`,`WHEN` 发起采集,`THEN` **零事件**(不发 `ResourceHarvested` / 成长)(F-17-3 求值 ②)|
| **AC-17-07** | [A] | BLOCKING | `GIVEN` 库存满载,`WHEN` 采集,`THEN` 失败且不消耗余量、不发成长(EC 满载行)|
| **AC-17-07b** | [L] | BLOCKING | `GIVEN` 三类失败(满载 / 余量不足 / 超时),`WHEN` 玩家走查,`THEN` **三类反馈可区分且各自可见**(§Edge Cases 失败收敛)|
| **AC-17-07c** | [A] | BLOCKING | `GIVEN` 17 的失败收敛路径,`WHEN` 静态检查,`THEN` 三类失败**各有具名枚举 / 分支**,非单一可选反馈(no hand-waving)|
| **AC-17-08** | [A] | BLOCKING | `GIVEN` 采集中途被打断,`WHEN` 动作终止,`THEN` 零效果(无事件、余量不变)(规则六)|
| **AC-17-08b** | [A] | BLOCKING | `GIVEN` `RegrowWindow` / `Σw` / `CapTable` 的非法值(`≤ 0` / 全零 / 长度 ≠ 61 / 越界),`WHEN` 装载,`THEN` **硬失败**(显式 `throw`,非 `Assert`)(F-17-1 / F-17-3 装载期校验)|

### C 组 · 反幻想与定点纪律

| ID | 类型 | 级别 | 判据 |
| --- | --- | --- | --- |
| **AC-17-09** | [A] | BLOCKING | `GIVEN` 全部呈现层 DTO,`WHEN` **递归 type-surface 反射扫描**,`THEN` 无任何「原始品级 / 本可采到」字段(AC-37-15 同型;**非按名 grep**)|
| **AC-17-09b** | [A] | BLOCKING | `GIVEN` 采集成功 / 失败音的 `AudioCueDto`,`WHEN` 反射断载荷字段类型,`THEN` **载荷不含品质维**(成功音内部不分档)(ADR-018 §六)|
| **AC-17-10** | [A] | BLOCKING | `GIVEN` `F-17-1` / `F-17-2` / `F-17-3` 的求值路径,`WHEN` **Roslyn 字面量扫描 + 类型签名断言**,`THEN` sim 字段零 `float` / `double`、零浮点字面量;⚠️ **`FixParse` 在烘焙期,不在运行期**(原稿把烘焙期义务写成运行期,ADR-014)|

### D 组 · 曲线与手感(值多未裁)

| ID | 类型 | 级别 | 判据 |
| --- | --- | --- | --- |
| **AC-17-11** | [A] | BLOCKING | `GIVEN` `gather_seq` 的定义,`WHEN` 反射 / 静态检索,`THEN` 唯一来源 = **`ResourceHarvested` 的节点计数**,**零独立计数器**(F-17-1 注)|
| **AC-17-12** | [L] | ADVISORY | `GIVEN` P0 三品种,`WHEN` 人工走查,`THEN` 采集动作的前后摇 + 打断窗口手感成立(**证据存档于 `production/qa/evidence/`**)|
| **AC-17-13** | [A] | **BLOCKED-BY-OQ-17-2** | `GIVEN` 同种子同节点、两个不同 `QueryLevel`,`WHEN` 差分夹具比对 `out_quality`,`THEN` 高技能者 ≥ 低技能者(差分自动,**ADR-019「回放即记录」使此为免费**)|
| **AC-17-13b** | [L] | ADVISORY | `GIVEN` AC-17-13 的差分结果,`WHEN` **去标注盲测**,`THEN` 玩家能**觉察**差异(可读性;**由 42 交付**,EXTERNAL 半)|
| **AC-17-14** | [A] | BLOCKING | `GIVEN` `CapTable[]`,`WHEN` 单测,`THEN` 长度 61 · 单调不减 · `CapTable[0] ≥ 1` · 各值 ≤ `MAX_QUALITY`(防「升级变差」+ 防 `clamp` 抛异常)|
| **AC-17-15** | [A] | BLOCKING | `GIVEN` `GatherMul(L)`,`WHEN` 单测 + 边界,`THEN` `GatherMul(0) == 1` · 单调不减 · ≤ `GATHER_MUL_CAP` · `Mul` 后取整用 `ROUND_HALF_AWAY_FROM_ZERO`(防低技能双重惩罚)|
| **AC-17-16** | [A] | BLOCKING | `GIVEN` `SEASON_MULT[]`,`WHEN` 装载,`THEN` 表长 = 5 的 `SEASONS_PER_YEAR`(不等 ⇒ 装载期硬失败,非越界读)|
| **AC-17-17** | [A] | BLOCKING | `GIVEN` `YieldDecay(n)`,`WHEN` 单测,`THEN` **单调不增** · `YieldDecay(0) == MUL_ONE` · 有下限(≥ `DECAY_FLOOR`)(深水线**衰减**半截的机械证据)|

> **⚠️ 删条说明**:原 **AC-17-16 `[I]`**(不在图例 + 后半断言 **5 的产物属性** = 与 `AC-5-17` 重复计数)
> 已删除其第二半;季节表长的同值契约改为 **AC-17-16**(本节可自测)。
> **原 `[I]` 标签已废除**(图例无此项)。

---

## Open Questions

| # | 问题 | 归谁 | 何时裁 | 不裁的后果 |
| --- | --- | --- | --- | --- |
| **OQ-17-1** | **深水线曲线的值** —— `BaseCapacity` / `RegrowWindow` / `SEASON_MULT[]` / **`YieldDecay` 的 `DECAY_RATE` / `DECAY_FLOOR`**;**形状二选一**:衰减项**逐节点累积**(本稿)vs **全局累积**(整山采秃)| **用户**(数值轮)| 首次 playtest 前 | P0 没有「看得见的难度曲线」(断裂② 不闭合)|
| **OQ-17-2** | `CapTable[]` / `GatherMul(L)` 的表值 + `GATHER_MUL_CAP` | 用户(平衡期)| 首次 playtest 前 | 品级 / 采量曲线无值(形状已定,可先写代码)|
| **OQ-17-3** | 采集**是否消耗时间**(动作时长 / 打断窗口定义)| 用户 + 17 的实现轮 | 实现期 | 动作原子性的窗口边界未定 |
| **OQ-17-4** | 30 修订若改接口名,17 侧的同步口径 | 30 的修订轮 | 30 复审后 | 17 的成长调用点须改(非阻断)|
| **OQ-17-5** | 资源点余量的**实现落点** —— 每帧读世界流现算 vs 缓存(须失效谓词)| `lead-programmer` | 实现期 | 现算 = O(n) 每帧;缓存 = 须证明可重建。**⚠️ 升为正确性问题**:缓存键须纯 `(流前缀, tick)`,并过**从空重建等值测试**(仿 `AC-20-03`),否则缓存 = 第二真源 |
| **OQ-17-6** | **采集意图的上行通道**(+ 预表现 / 失败收敛的「超时未裁决」支)| 用户 + 45 | **与 `OQ-10-9` / `OQ-4-10` / `OQ-18-7` / 20-BL-4 同批** —— 一次窄的 **ADR-001 修订**(P1b 前)| 「外抛即结案」**第四次复发**;17 是第五个消费者 |
| **OQ-17-7** | **资源点数据的驻留 / 切片** —— `world_resources.cooked` 是否随 chunk 切、未驻留 chunk 如何语义 | 用户 + 54 / ADR-014 | 实现期前 | `Step` 内触发 Addressables 载 = E-13 抛(硬崩);否则假定全域驻留 = 撞 ADR-022 + 爆内存(承 `OQ-6-8` 先例)|
| **OQ-17-9** | `quality_character[]` 的**取值语义**(「陈放」「虫蛀」在 P0 无仓储 / 无腐坏 ⇒ 不可达)| 用户 + 21a / 42 | 21a 下一轮(与 **R13** 同批,**不捆绑**)| 医学专家受众首次出戏点(硬约束)|
| **OQ-17-10** | **`CDF walk` 算子的登记缺口** —— `tr-registry.yaml` 中该算子仍 `gap`;本稿的整数口径须落为可对拍单元 | 架构轮 | ADR-012 CI 落地时 | AC-17-02 / AC-17-03 无对拍基准 |
| **OQ-17-11** | **`OQ-4-11`(同格同种候选不可挑)作为 17 的 playtest 门** | 用户 + 4 / 42 | 17 的 playtest | 玩家「采错了一株」无反馈 = 「吃指令」|
| **OQ-17-12** | **21a F1 的 `min(各输入 quality)`** 断掉幻想第 3 层的推理环 | 用户 + 21a | 21a 下一轮 | 17 的隐性回报**不可归因**(幻想第 3 层失效)|

---

## 附:首轮评审修订记录(2026-09-19)

**裁决**:`MAJOR REVISION NEEDED`(scope L,边界偏 XL)· 7 根因 · 特别专家 7(haiku 并行)+ creative-director(Opus 串行)。

| 根因 | 名称 | 落点 |
| --- | --- | --- |
| **P0-1** | **避涟漪式改判**(新变体)| 规则二改判:发 `ResourceHarvested`(用户裁定 B);ADR-009 **Amendment K**;修 `gather_seq` 污染 + 载荷承载 |
| **P0-2** | **外抛即结案**(第 4 次复发)| `OQ-17-6` 登记并批;§Edge Cases 失败收敛 |
| **P0-3** | **幻想代持**(新)| §Player Fantasy 按 17 拥有面重写;三层 EXTERNAL 化;采前线索 = 42 阻断规格 |
| **P0-4** | 定点域口径未闭合 | F-17-1 的 `U` 整数化 / `quality_distribution` 定 `int` / `Σw ≥ 1` / F-17-3 量纲齐一 + 形状拆两项 |
| **P0-5** | 借来的绿 + 记账面 | AC 全表改造(级别列 · AC-17-02 EXTERNAL · 反射替代 grep · 删 `[I]`);6 规则补覆盖 |
| **P0-6** | 资源点驻留面未登记 | §Dependencies 新增 `IDataProvider` / 流查询两行;`OQ-17-7` |
| **P0-7** | 呈现事实(准星)+ 拒绝反馈不可分辨 | §UI Requirements 订正为「身位即光标」;失败收敛三类具名 |

**未闭合(归用户 / 各轮)**:`OQ-17-1` / `2` / `3` / `5` / `6` / `9` / `10` / `11` / `12` + 21a 侧 R13。
