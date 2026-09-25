# 物品与配方数据库 (Item & Recipe Database) — 21a

> **Status**: ✅ **Approved**(2026-09-18 用户裁定接受三轮修订 —— **覆盖评审日志的「仍 In Review」结论**,显式风险接受结案)
> **⚠️ 2026-09-18 结案口径**:评审日志(三轮)末段明写「21a 仍 **In Review,不得标 Approved**」,并建议「第四轮复核以『逐条核对冻结裁决表 ↔ 可执行体』为唯一靶子」。**用户裁定:接受三轮修订落盘状态、以显式风险接受标记 Approved,不开第四轮**(承 37 / 42 / 51 的「免二轮 = 显式风险接受」先例)。**这意味着** —— 三轮指出的**「一处声明、另一处漏改」类传导缺口**(§D-21-x / §Tuning Knobs / §AC / `entities.yaml` 四方漂移)在 Approval 时**未被独立复核清点**,由实现期第一道构建期门兜底。
> **重开触发条件(五者任一)**:① 实现期构建期门(守恒律上界 / `EFF_MIN` / `ordinal` 类)拦下运行时击穿 = 冻结裁决表与可执行体不一致;② `instance_id` / `structure_id` 高水位在迁移后出现复用;③ F5 偏移落入 9 的噪声带(可感知地板失效);④ 9 侧字段名与 `drug_potency` / `half_life` 对不上(D-21-25);**⑤ 2026-09-19 新增(由 11 二轮评审提出):`D-21-34` —— F5 下界断言 `> 0` 与 11 的 `≥ MIN_USABLE_HALF_LIFE` 不是同一把尺 + `drug_potency` 无声明域**(合法 `Axis_effective = 2` 静默退化为「无药效」而 11 侧 AC 全过)。⚠️ **本条为已 Approved 件的具名重开条件,非补丁** —— 修 F5 断言 = 改 21a 已批机制 |
> **实现期门(不阻塞 Approval,阻塞开工)**:`D-21-21`(守恒律两侧封)· `D-21-24`(F5 可感知地板)· `D-21-25`(9 侧字段名对齐)· `D-21-26`/`D-21-27`(`instance_id` 权威契约)· `D-21-28`(Craft 事件总序键)+ `AC-21a-53`(改型后)/ `AC-21a-56…62`
> **Author**: dr_guyang + game-designer / technical-director(开工前盲点复核 + Q1–Q5 裁决)
> **Last Updated**: 2026-09-19(18 首轮评审涟漪:D-21-30/31/32 落盘 + `Recipe.owner` + `EnvMod_total` 钳制式 + 守恒极值式 + AC-21a-65/66;此前 2026-09-14 三轮)
> **Implements Pillar**: 支柱一(判断为骨)· **超硬约束:专家受众首次接触不出戏**(见 D-21-12)
> **上游**: 无工作流依赖 —— 全案唯一零依赖的 Foundation 系统(仅两条**常量/契约**引用:30 的 `SKILL_CAP`、ADR-006)
> **下游**: 9 个系统读它(11 处方 · 12 药物槽 · 16 中药选项 · 17 采集 · 18 炮制 · 19 制作 · 20 库存 · 42 拟物 UI · 7a 持久化)
> **定向复核**: 盲点复核 + Formulas 裁决均由独立 Opus `technical-director` 出具(2026-09-13)
> **首轮复核**: `/design-review` full(七名专家)2026-09-14 → `NEEDS REVISION`(Scope M/L)
> **二轮复核**: `/design-review` full(五名专家 + Opus 高阶综合)2026-09-14 → `MAJOR REVISION NEEDED`(Scope L)
>   —— 本轮**新发现** 10 项 blocking(前轮修订引入了新缺陷:F1 伪不变量、`EFF` 死变量、
>   `weight` 类型冲突、Fix 无法 Unity 序列化、F5 不交付锚点二……);用户三项裁定见 D-21-14/15/16
> **三轮复核**: `/design-review` full(七名专家 + 高阶综合)2026-09-14 → `MAJOR REVISION NEEDED`(Scope L)
>   —— **性质从「轴错了」变为「兑现没做完」**:三轮**不动任何已裁轴**,收敛到「冻结裁决表跑在可执行体前面」
>   —— 守恒律构建期上界漏产出侧乘子(可被运行期击穿)、§Schema 不含自身公式变量(`BaseQty`/`Offset`)、
>   AC/注册表/门控三方漂移、`instance_id` 权威缺契约、锚点二/三无可感知层。
>   用户四项裁定见 **D-21-21…D-21-24**
> **欠下游的账**: 见 §Debt Register(唯一台账，跨系统债务在此登记)
> **CD-GDD-ALIGN**: *CONCERNS —— 二轮综合裁定;非 phase gate(review mode = lean)*

> ## 🔒 已冻结的裁决
>
> | # | 裁决 | 依据 |
> |---|------|------|
> | **D-21-1** | **P0 采集 = 药用植物原料;「炮制」= 提取 / 干燥 / 标准化;产出西药成品** | 用户裁决 2026-09-13;口径同步已落 `game-concept.md` |
> | **D-21-2** | **物品主键 = `(base_id, processing_state)` 复合键**,`processing_state` 是枚举 | Opus 盲点复核;唯一会真流血的点 |
> | **D-21-3** | **品级是物品实例属性,不是物品类型** | 同上 |
> | **D-21-4** | **药品与普通物品同一张表**,`category` 区分,`drug_profile` 为可空扩展块 | 同上 |
> | **D-21-5** | **配方表用通用 `inputs[] → outputs[]`**;炮制是 `n=1, m=1` 的特例 | 同上(否则 18 与 19 形状冲突) |
> | **D-21-6** | **`drug_profile` 自 P0 起预留时间轴字段**(起效 / 达峰 / 半衰期 / 消除),P0 可为空 | 防止 P1a 加字段时 11 处方已写死读法 |
> | **D-21-7** | **伤情不入物品表**;武器物品用 `inflicts_injury` 外键指向 `9 疾病与伤情模拟` | 与 `systems-index.md` §9 C4 一致 |
> | **D-21-8** | **本系统一分为二**:21a(契约 / Schema,本文件)与 21b(P0 物品清单 + 史实出处,另立) | Opus 复核:范围由 S 实为 M |
> | **D-21-9** | **整数边界**:F1/F2/F4 出参即 `int`;存档中永不出现 `float`;`drug_profile` 的 `Offset`/时间轴四字段/品级档位表是 **Q16.16 整数字面量**(`weight` **除外**,见 D-21-17)。**契约详见 ADR-006**。⚠️ **`Offset` 已改名 `drug_potency`(见 D-21-22)** | 2026-09-14 用户裁定④;堵 `drug_profile → 9` 的跨域空洞 |
> | **D-21-10** | **守恒律**:转化**必**损失质量 —— `Σ(weight × OutputQty) ≤ EFF_MAX × Σ(weight × ActualConsumed)` 且 `EFF_MAX ≤ 1` | 2026-09-14 用户裁定③;锚点三「转化是有代价」的机制落地。**⚠️ 二轮修正量纲**:两侧须乘 `weight` 归一到重量单位(否则「一粒丹 = 一斤药材」),且投入侧取 `ActualConsumed`(D-21-15)而非基数 |
> | **D-21-11** | **品级有非数量出口**:`quality` 以**时间轴档位**调制作用点(见 F5),不是乘数。**⚠️ 二轮改轴:原「药效幅值」→ 现「时间轴作用点」(D-21-14 覆盖其轴,性质保留)** | 2026-09-14 用户裁定②;品级不再只是产量微调 |
> | **D-21-12** | **§命名规范 移出本文件** → 21b 内容规范(21a 是 schema 文件);本文件只留一条指针 | 2026-09-14 用户裁定①;解「对玩家无正向后果」与「自毁」两诤 |
> | **D-21-13** | **`processing_state` 跨持久化只用稳定字符串名**,禁 int 编码 | 2026-09-14 复核 blk #9;防 P1a 插值时老存档静默重映射 |
> | **D-21-14** | **F5 作用轴 = 时间轴 / 作用点**(品级偏移 `onset`/`peak`/`half_life`/`elimination` 之一),**不再调药效幅值** | 2026-09-14 **用户裁定⑥**(二轮 blocking #7):「见效更快」与「见效更足」是**不同种类**的差别;幅值同轴 = 换皮,不交付锚点二 |
> | **D-21-15** | **技能回报 = 投入端可变**:`Recipe.inputs[]` 是**基数**,结算按 `EFF` 实际消耗(`ActualConsumed = Ceil(Base / EFF)`);技能高 = **省料** | 2026-09-14 **用户裁定⑦**(二轮 blocking #2):`EFF` 从死变量变为**运行期出口**;规则八的「烧更多原料」由此**可实现** |
> | **D-21-16** | **品级身份标签**:`gather_profile.quality_character[]`(每档一个**定性修饰**,string),由 42 以**外观 / 药签措辞**呈现 | 2026-09-14 **用户裁定⑧**(二轮 blocking #7/#8):锚点二「品级的质地」的**呈现载体**;是 string,**不碰定点域**。**⚠️ 2026-09-25 用户裁定(R13 = 甲)改判半边**:原「P0 可空」→ **`MAX_QUALITY > 1` 时最小非空**(长度 = `MAX_QUALITY` 且逐档非空);**成药侧 `D-21-24` 的 `drug_quality_character[]` 同批同裁**(理由 = 可空退路「质地靠 F5」与本件「F5 摸不到原料」自相矛盾,且 42 走查 U-5/U-6 无物可呈必红) |
> | **D-21-17** | **`weight` 口径 = `int`(最小单位个数)**,**不是 `Fix`** —— ADR-006 须修正其 `Fix` 解析集 | 2026-09-14 二轮 blocking #3:F4/20/52 全需整数计数;原 AC-21a-41 与 ADR-006:113 把它误列入 `Fix`,不可满足 |
> | **D-21-18** | **`Fix` 不可经 Unity 序列化器承载** —— 必须走自定义编码器(ADR-006 修正案) | 2026-09-14 二轮 blocking #5:ADR-005 的 `Fix` 是 `readonly struct`+`private long`,Unity 跳过 ⇒ **静默归零** |
> | **D-21-21** | **守恒律两侧都封(严格质量守恒)**:`Σ(weight × 产出量) ≤ EFF_MAX × Σ(weight × 实耗量)`,**产出量含 `QtyMultiplier`**;构建期上界须写 `QTY_MULT_MAX × Σ(weight × 产出基数) ≤ EFF_MAX × Σ(weight × 输入基数)` | 2026-09-14 **用户裁定⑨**(三轮 blocking #1,5/7 专家命中):原构建期检查漏产出侧乘子,可被运行期击穿(「凭空造物」)。两侧皆在整数域内、**先乘后比、禁逐项舍入** |
> | **D-21-22** | **药效幅值 `drug_potency`(原 `Offset`)归 21a** —— 入 §Schema B(**Q16.16 int**),是 **9 处置事件 `Offset` 的来源**;**它是静态基础幅值,不是二轮被杀的品级幅值轴**(D-21-14 只改品级的**调制轴**,未废除幅值本身) | 2026-09-14 **用户裁定⑩**(三轮 blocking #2,21a↔9 契约断裂):二轮换轴时误删,导致 9 点名的来源在 21a 无字段。**改名以绝混淆**;9 侧字段名对齐归 **D-21-25** |
> | **D-21-23** | **F5 的 `quality_axis` 在 P0 收窄为 `half_life`**(唯一在 9 有落点的轴);`onset / peak / elimination` 标 **P1a**,待 9 扩处置载荷后开 | 2026-09-14 **用户裁定⑪**(三轮 blocking #4):9 的处置载荷只有 `polarity / Offset / tau_half`,`onset/peak/elimination` **产出即被丢弃** —— 收窄以避免「枚举有四值、实际只有一值」的静默 |
> | **D-21-24** | **成药侧品级感知层**:新增 `drug_quality_character[]`(string[],长度 = `MAX_QUALITY`)+ **U-6** 呈现契约;并加**可感知地板** —— F5 偏移量须大于 9 的噪声带 | 2026-09-14 **用户裁定⑫**(三轮 blocking #5):F5 只交付「时间轴偏移」而 U-1/U-2 禁数字,**成药侧无合法感知通道**;偏移小于噪声即等于未发生。**⚠️ 2026-09-25 随 R13 = 甲 同批**:本列 **`MAX_QUALITY > 1` 时最小非空**(原「可空」改判;与 `D-21-16` 原料侧对称,U-6 才有物可呈) |

---

## Overview

物品与配方数据库是全案唯一零工作流依赖的系统 —— 它定义**世界上存在哪些东西**,
以及**它们如何互相转化**。两张表构成全部内容:
- **物品表** —— 每一条可被采集、持有、使用或开具的实体(药用植物原料、成药、器械、建造件)。
  主键是 **`(base_id, processing_state)` 复合键**:同一株植物在 **生料 / 干燥 / 提取物 / 酊剂**
  四种形态下是**同一条 base 的四个条目** —— 这样 P1a 加中药只是扩枚举,不动结构。
- **配方表** —— 通用形状 **`inputs[] → outputs[]`**。**炮制**(单品加工)是它在
  `n=1, m=1` 的特例,**制作**(多料合成)是 `n>1` 的特例。一张表同时喂两个下游系统。

玩家永远不会打开这个数据库 —— 但玩家手里的每一味药、每一件器械、每一次
「这株草能变成什么」,都由它决定。它是**七个子系统共同的词汇表**:
17 采集读它的品种与品级,18 炮制读它的转化率,11 处方读它的药物档案,
20 库存读它的堆叠与重量。**本系统的价值不在它能做什么,而在它让另外七个系统
不必各自发明一套。**

> **本文件 = 21a(契约层)**:只定 Schema、主键与命名规范。
> **P0 具体物品清单与史实出处 = 21b**(另立文件,须逐条考据)。

## Player Fantasy

玩家永远不会看到这个系统 —— **他看到的只有后果**,而后果恰好是三个「不用想」的时刻。
数据契约做对了,**它就该是隐形的**:

1. **「认得出来」的前提是它前后一致** —— 你在山里采到毛地黄,判断链会告诉你该拿它做什么。
   而「该做什么」不是当场凭空生成的:全案对毛地黄只有一个说法。**数据库让它前后一致。**
2. **品级的质地** —— 同一味药,手采的和药铺买的不一样。你看不出数值,
   但你知道这次的比上次的好。**品级是实例属性**(D-21-3)正是为了交付这一层:
   「**这一次的这株**」有质地,而「毛地黄」这个品类没有。
   > **机制凭据(2026-09-14 二轮复核重写)**:上轮把质地押在 F5「药效幅值档位」上
   > —— **二轮推翻**:幅值只是**同一根标量上的又一个乘子**,与它要取代的「产量微调」
   > 同轴,且**原料根本摸不到 F5**(`category = material` 无 `drug_profile`)。
   > 现取的**双载体**:
   > - **看得见的种类(D-21-16)**:`quality_character[]` 给每档一个**定性修饰**
   >   (「新采带露」「陈放」「虫蛀」),由 42 以**外观 / 药签措辞**呈现 —— 玩家看出**不一样**,
   >   但看不出数值(合 U-1/U-2)。
   > - **感得到的作用点(D-21-14)**:F5 改为**时间轴偏移** —— 高品级的药**见效更快 / 更持久**。
   >   「**这一剂下得更快**」与「这一剂下得更足」是**不同种类**的差别,这才是「质地」。
   > **可被主角拿在手里的呈现**归 42 / 20(见 §UI Requirements),**21a 不规定装置**。
3. **转化是有代价的** —— 三斤柳树皮换不出一钱水杨酸。**配方表里的转化率
   把「炮制」变成了一个真实的决定**:要不要为了提炼这一味,把采来的原料全投进去。
   > **机制凭据(2026-09-14 二轮复核重写)**:代价的**真实形态是投入损耗**,不是「有概率全废」。
   > 但上轮只把损耗写成一条**构建期静态不等式**(`Σ outputs ≤ Σ inputs × EFF_MAX`)——
   > 而 `inputs[]` 是静态声明量,技能改不动它,**规则八的意图在数学上不可表达**。
   > **二轮修正(D-21-15)**:**投入端变成运行期量** —— `inputs[]` 是**基数**,
   > 结算时按 `EFF` 实际扣料(`ActualConsumed = Ceil(Base / EFF)`):
   > **技能高 = 同样的产出烧掉更少的原料;技能低 = 恒定的产出吃光双倍的料**。
   > 这就是「**投入产出比**」的真实梯度,不再是一句口号。
   > 与 Q4「产出永不归零」**同时成立**:产出有下限(F1 的 `max(1, ·)`),
   > 但**烧掉的原料**没有 —— 代价落在投入端,不落在产出端。

> **反幻想**:这不是「收集图鉴」游戏。玩家**永远不会**看到
> 「已解锁 47 / 共 120」这样的进度条 —— 那正是本作拟物 UI 明确要避免的东西。
> **数据库的完整性是开发者的事,不是玩家的事。**

## Detailed Design

### Core Rules

**规则一:两张表 + 扩展块。** 物品表 + 配方表。**没有第三张表。**
- 一切「附加信息」都是**这两张表上的可空扩展块** —— `drug_profile`(11 用)、
  `gather_profile`(17 用)、`tcm_profile`(16 用,P0 恒空)、`tcm_profile` 之外的 P1a 块同理。
- **块的完整字段定义见 §Schema**(本章只定规则)。**禁止**下游自行新增字段而不回到本节登记。

**规则二:主键是复合键。**
```
item_key = (base_id, processing_state)
```
- `base_id` —— 稳定的字符串标识(如 `willow_bark`、`digitalis_leaf`)
- `processing_state` —— **全局枚举** `raw | dried | extracted | tincture | pill` *(P1a 追加 `honey_fried` / `dry_fried`)*
- **同一 base 的不同 state 是不同条目** —— 库存里占不同格、可堆叠性独立、取舍独立
- P1a 加中药 = **扩枚举**,不动结构
- **持久化编码(D-21-13)**:`processing_state` 跨存档**只用稳定字符串名**,**禁止 int 编码** ——
  C# enum 默认序列化成 int,P1a 在枚举中段插入新值会让老存档**静默重映射**。
  持久化键格式固定为 `"base_id:state_name"`。此条与「只增不删」(Edge Cases)配套。

**规则三:品级是实例属性,不是物品类型。**
- 物品表里**没有** `willow_bark_grade_3` 这种条目
- 品级挂在**物品实例**上:`ItemInstance = { instance_id, item_key, quality, qty }`
  —— **`instance_id` 与 `qty` 是承重字段,不是省略号**(见 §Schema)
- **后果**:20 库存必须处理「同一 `item_key`、不同 `quality`」的堆叠策略(见规则六)
- **品级有非数量出口**(D-21-11 + D-21-14,二轮改轴):见 **F5** —— 品级偏移**时间轴作用点**

**规则四:药品与普通物品同一张表。**
- `category` 区分:`material | drug | tool | weapon | build_part | food`
  *(2026-09-14 复核补 `weapon` —— 规则七的「武器物品」此前无处归类)*
- **`build_part` 的消费者 = 23 模块化建造**(`modular-building.md` Dependencies 表,反向引用
  2026-09-17 补):`build_part` 物品承载**模块目录的入口**(`module_id` 映射),模块目录本体
  = **数据**(目录校验归 21a,`O-23-1` ✅) —— 23 只消费 `module_id` + `cost(m)`,不持有物品行
- `drug_profile` 是**可空扩展块**,仅 `category = drug` 时非空
- `drug_profile` **自 P0 起就预留时间轴字段**:`onset / peak / half_life / elimination`
  —— P0 可为空,**但字段必须在**,且**必为整数**(D-21-6 + D-21-9)

**规则五:配方表用通用形状。**
```
Recipe = { recipe_id, owner, inputs: [{ item_key, qty }], outputs: [{ item_key, qty }],
           duration_ticks, skill_gate, min_quality, boundary_state[] }
```
- **`owner ∈ {process, craft, build}`** —— **配方归属系统的显式字段**(2026-09-19,由 18 首轮评审
  **D-21-30** 登记,用户裁定 [甲]):子集判据**不得**再依赖「`processing_state` 是否变化」——
  `item_key = (base_id, processing_state)` 是复合主键,建造件 / 制作件的 state 变化与炮制链
  在数据上**结构性不互斥**(18 R-18-B 举证)。义务:`{process, craft, build}` 三子集
  **两两不相交、并为全集**(18 的 `AC-18-18` 验此);装载期校验,缺字段 = 硬失败(ADR-014)。
  state 变化判据降为**单向校验**(owner=process ⇒ 必有 state 变化;反向不成立)。
- **炮制** = `n=1, m=1` 的特例 · **制作** = `n>1` 的特例
- **一张表,一个求解器** —— 否则 18 与 19 上线时会各自长出一套
- **`inputs[].qty` 是基数,不是实耗**(D-21-15):结算是 `ActualConsumed = Ceil(qty / EFF)`
  (见 F1)。**技能高 → 实耗更少**;低 → 实耗更多。`outputs[].qty` 则是**产出基数**
  (经 F1 的 `QtyMultiplier` 缩放)。
- **`m > 1` 时 F1 逐条套用**(三轮 blocking #2):**每个 `outputs[i]` 各自**算
  `OutputQty_i = max(1, Round(outputs_i.qty × QtyMultiplier))`;`outputs_i.qty` **就是该条的 `BaseQty`**。
  不存在一个「配方级」的单一 `BaseQty` —— 原稿 F1 写单数、schema 写复数,是**形状断裂**。
- **字段定义与校验**(2026-09-14 复核补 —— 此前 `duration` / `skill_gate` 只出现一次、全文无定义):
  - `duration_ticks` —— 单次结算耗时的**逻辑 tick 数**,`int > 0`,单位是 tick(**不是秒**)。
    秒换算的唯一来源是 `TICK_SECONDS`(见 9 的 Tuning Knobs),21a **不持有**它。
  - `skill_gate` —— **门槛,不是系数**:`int ∈ [0, SKILL_CAP]`。低于此等级**不可发起**该配方
    (区别于 F1 的连续修正 —— 门槛制造决策,系数只制造最优解)。
  - `min_quality` —— **输入品级下限**:`int ∈ [1, MAX_QUALITY]`。低于此品级的原料**不可用作输入**。
    ⚠️ **这不是品级的出口** —— 出口见 **F5**(时间轴档位,见下)。
    `min_quality` 只做**准入闸**(「这方子只收好料」),**不改药效**。
    两者存在的理由不同:闸门制造**取舍**,档位制造**回报**;若把前者当出口,
    则 `MAX_QUALITY` 档的玩家只剩「能不能做」而没有「做了更好」。
  - `boundary_state[]` —— 该配方**合法声明的** state 对;未声明者**校验拒绝**(见 Edge Cases)。
  - **`n>1` / `m>1` 的聚合规则**(2026-09-14 二轮复核补 —— 此前 19 制作的核心形态无定义):
    - **输入品级**:`InputQuality = min(各输入的 quality)` —— 最差料定上限
      (与 F2「输入定上限」同纪律;**不取平均**,否则劣料可被好料稀释)。
    - **输入基数**:`ΣM` 的 `QualityMod` 用上面的 `InputQuality` 单值;**实耗逐项算**
      (`ActualConsumed_i = Ceil(inputs_i.qty / EFF)`),每项独立。
    - **输出品级**:`outputs[]` 中**每一条**各自套 F2(`OutputQuality = clamp(...)`),
      **共享同一个 `InputQuality`**。**不按数量加权** —— 品级是逐输出的结果,不是可分配的总量。

**规则六:堆叠规则。**
- **堆叠键 = `(item_key, quality)`** —— 同 key 同品质才可堆
- `stack_max` 由物品表逐条定义
- **重量用整数最小单位**(D-21-9):`weight` 是 `int`(最小单位的个数),**禁 float** ——
  否则 20 的负重阈值判定会随累加顺序翻转。**展示单位**与**存储单位**是两件事(见 Tuning Knobs)。
- **堆叠基数警告**:槽位基数 = `base 数 × state 数 × MAX_QUALITY`。21a **必须把预期基数交付 20**
  (20 的容量预算被本规则提前决定);且因拟物 UI 禁数字,**42 需一份非数字化的品级呈现契约**(见 §UI Requirements)。

**规则七:伤情用外键,不驻留。**
- 物品表**不含**伤情定义
- 可施加伤情的物品(武器)带 `inflicts_injury: injury_id` —— `category = weapon`(规则四)
  ⚠️ **2026-09-17 语义降级(承 25 · R13 / A20)**:本字段**不是「哪次命中造成哪个伤」的真源** ——
  逐次命中的伤情由 **25 的动作行 `maps_to_injury`** 指定(函数映射)。`inflicts_injury` 读作
  「**该武器所属线可产生的伤情集合**」的**约束校验**:构建期断言 21a 的集合 **⊇** 该线全部动作的
  `maps_to_injury`(A20),防两表漂移;它回答「这把刀能造成哪些伤」,不回答「这一刀是什么伤」。
- `injury_id` 的权威定义在 **`9 疾病与伤情模拟`**(合 `systems-index.md` §9 C4)

**规则八:守恒律(严格质量守恒 —— D-21-21)。**
- **不变量**:对任意配方、任意 `EFF`、**任意 `QtyMultiplier`**,
  ```
  Σ( weight × OutputQty_i )  ≤  EFF_MAX × Σ( weight × ActualConsumed_j )
  ```
  且 **`EFF_MAX ≤ 1`**。**两侧都必须在「产出已含 `QtyMultiplier`」的状态下求值。**
- **构建期上界(硬失败)**:因为运行期 `QtyMultiplier ≤ QTY_MULT_MAX` 且 `ActualConsumed ≥ 基数`,
  最坏情形是「产出取最大乘子、投入取基数」:
  ```
  QTY_MULT_MAX × Σ( weight × outputs_i.qty )  ≤  EFF_MAX × Σ( weight × inputs_j.qty )
  ```
  **这一条是原稿的破口(三轮 blocking #1,5/7 专家命中)**:原式两侧都用基数、**漏了产出侧乘子**,
  于是构建期 9 ≤ 10 通过、运行期 18 > 10 击穿(实测反例:`w_in=10, w_out=9, EFF_MAX=1, QTY_MULT_MAX=2`)。
- **运行期求值(整数域)**:两侧升为 Q16.16 后**先乘后比、禁逐项舍入** ——
  比较 `Σ(w×out_i) × QTY_MULT_RAW ≤ Σ(w×in_j) × EFF_MAX_RAW`(全 `long`),
  避免「`Σ(EFF_MAX × x)` 与 `EFF_MAX × Σx` 舍入不同」导致同一配方在主机/客户端**合法性与否翻转**。
  `Σ(w × InstanceWeight)` 另受 **int64 溢出上界**约束(见 AC-21a-64)。
- **损耗口径(修正措辞)**:`ActualConsumed = Ceil(基数 / EFF)`,`EFF ≤ EFF_MAX ≤ 1` ⇒
  `ActualConsumed ≥ 基数` —— **损耗只增不减,满技能 `EFF = EFF_MAX = 1` 时取等号**。
  原稿写「损耗**必然**发生」在 `EFF = 1` 处为**假**(那是零损耗);代价曲线**上端无约束**这条张力已登记
  (见 §Tuning Knobs 的 `EFF_MAX` 行 + D-21-21)。
- ⚠️ **单位口径**:守恒律只能在**同量纲**内求值。对**异质 `item_key`**(柳树皮 → 水杨酸)
  计「个数」无共同分母 ⇒ 恒真、不约束任何东西。故**按 `weight` 最小单位求值**
  (D-21-17 让 `weight` 是整数最小单位,恰好可通约)。
  ⚠️ **该检查依赖 `weight` 数值已填**;`weight` 计数值归用户(见 §Tuning Knobs / D-21b-2 冻结令)。
- 这是锚点三「转化是有代价」的机制落地(D-21-10):**技能低 = 同样产出烧掉更多原料**。
- 与 Q4「产出永不归零」不冲突 —— 下限管的是**产出**(F1 的 `max(1, ·)`),守恒律管的是**投入与质量**。

**规则九:整数边界(数据的确定性)。**
- **F1 / F2 / F4 的出参即 `int`** —— 中间量可用定点,但**落盘与出参不得出现 `float`**。
- **`drug_profile` 的 `drug_potency`(药效幅值,原 `Offset`,D-21-22)
  / 时间轴四字段(`onset`/`peak`/`half_life`/`elimination`)
  / 品级时间轴档位 `axis_offset_by_quality[]` 是 Q16.16 整数字面量**(D-21-9 + 二轮 R2):
  9 在主机加载期把它一次性转 `Fix`,**舍入向下保守**(合 ADR-005 舍入纪律)。
  **`drug_potency` 与 `axis_offset_by_quality[]` 的每档元素必须与 `Axis_base` 同量纲(Q16.16)** ——
  作者写 `分子/分母`,经 `FixParse`;**「整数档位」的措辞是错的**(会让加法的量纲错 65536 倍)。
  ⚠️ **`drug_potency` 是静态基础幅值,不受品级调制** —— 品级只调**时间轴**(F5)。
- **⚠️ `weight` 是普通 `int`,不是 `Fix`**(D-21-17,2026-09-14 二轮裁定):
  它是**最小单位个数**(F4),进 20 的负重比较与 52 的 `CarryLoad`。
  不得写成分数字面量,不列入 `FixParse` 解析集。**原 AC-21a-41 与 ADR-006:113 把它误列,已修正。**
- **⚠️ `Fix` 字段不得经 Unity 序列化器承载**(D-21-18,2026-09-14 二轮裁定):
  ADR-005 的 `Fix` 是 `readonly struct` + `private readonly long _raw`(无 `[SerializeField]`),
  Unity 序列化器**跳过**此类字段 ⇒ 落成 `default(Fix)`(**静默归零药物偏移与半衰期,不报错**)。
  **存档中承载 `Fix` 的字段必须走自定义编码器**(把 `_raw` 的 `long` 显式写出/读入),
  **不得依赖** MonoBehaviour / ScriptableObject / `JsonUtility` / prefab 的字段序列化。
  **不得**给 `Fix` 加 `[SerializeField]` 来「修」它 —— 那会把 `UnityEngine` 依赖塞进 Core 模拟类型,违反 ADR-005。
  契约见 ADR-006 **§Decision 五**。(⚠️ Unity 6.3 跳过 readonly/private 字段属 post-cutoff 知识,
  **须一条 EditMode 探针实测后钉死**,见 AC-21a-53。)
- **严禁任何 `float` 进入 9 的病史事件流** —— 静态断言 `float in SimEvent` 守门(见 AC-21a-19)。
- **权威边界**:18/19/20 一律**主机唯一结算**,客户端只收结果、**不得本地跑 F1/F2 产出物品**。
  **17 采集的 `instance_id` 铸造同样归主机唯一**(三轮 blocking #4:D-21-27 —— 原清单**漏了 17**,
  客户端本地铸号会与主机撞号,且**静默**:不崩溃,只表现为「两件物品合并/丢失」)。
  见 **D-21-26**(`IIdAuthority` 须扩 `ItemInstanceId Next()`)与 **AC-21a-63**。
- **契约全文见 `ADR-006`**(本裁决的落盘处,2026-09-14 用户裁定④;二轮追加修正案见 D-21-17/18)。


### 命名规范(指针)

**物品名怎么写 —— 不在本文件。**

> **📤 已移出(D-21-12,2026-09-14)**:原 §命名铁律 改称 §命名规范并迁至 **21b 内容规范**。
> 理由有二,同根 —— 它**寄居在错误的文件里**:① 21a 是 schema 文件,「内容怎么写」
> 属 21b;② 原表述**自毁**(✅ 列的「麻黄碱」本身含「麻黄」二字,既违铁律又必被
> 自己的 AC 判失败),且「判定法」**不可判定**(未指名哪部药典、哪版)。
>
> **21b 撰写时须落定三件事**(否则内容作者无判据,详见 §Debt Register **D-21b-1**):
> 1. **指名一部带版次的参照药典**(《中华药典》?《中华本草》?《中药大辞典》?)
> 2. **药品名与植物名开豁免**(「麻黄碱」是化学药名,不是中医药典条目)
> 3. **给可枚举的保留词表**(外置 `tcm_reserved_terms.yaml`,逐条带出处)
>
> **本文件只约束一件事**:**物品的显示名字符串由 21 拥有**(见 §UI Requirements)。
> 命名规范的具体内容由 21b 定义,21a 的 schema 对其有形式约束(必须是稳定字符串 id + 显示名两字段)。

> **⚠️ 一条已登记的张力(不得省略)**:P0 只有纯西医切片(D-21-1,你 2026-09-13 的裁决)。
> 「P0 无一味中药饮片」**背离 1900–1930 中国实况**(彼时行医者绝大多数是中医)。
> 这条切片的**服务对象是超硬约束「专家受众首次接触不出戏」,不是支柱五** ——
> 原文件把署名写成「Implements Pillar 五」是错的,已改(见页首)。**代价已付,张力显式登记于此。**


### Schema(契约的正文)

> **本节是 21a 存在的理由**(2026-09-14 复核 blk #1:此前 `gather_profile` / `tcm_profile` /
> `drug_profile` 内部字段、`duration` / `skill_gate`、`ItemInstance{…}` 全都只有名字,
> 七个下游各自猜字段)。**本节之外出现的字段一律视为未定义。**

**A. `ItemDef`(物品表的一条)**

| 字段 | 类型 | 可空 | 说明 |
| --- | --- | --- | --- |
| `base_id` | string | 否 | 稳定标识(如 `willow_bark`);**只增不删** |
| `processing_state` | enum | 否 | `raw｜dried｜extracted｜tincture｜pill`(+ P1a 两项) |
| `display_name` | string | 否 | 显示名(归 21;内容规范见「命名规范」指针) |
| `category` | enum | 否 | `material｜drug｜tool｜weapon｜build_part｜food` |
| `stack_max` | int ≥ 1 | 否 | 堆叠上限 |
| `stackable` | bool(**派生,不存储**) | — | **= `stack_max > 1`** —— 列于此仅为闭合 20 的接口面(三轮 blocking #2:`stackable` 被 §Interactions / §Dependencies 消费却无定义) |
| `weight` | **int > 0**(**非 `Fix`**) | 否 | **整数最小单位个数**(D-21-17);展示单位另见 Tuning Knobs |
| `deprecated` | bool | 否 | 废弃标记;废弃条目不物理删除 |
| `legal_transitions[]` | string[] | 否(可为空) | 本条目**声明**的合法 `processing_state` 通路(见 §States and Transitions) |
| `drug_profile` | block | 是 | 仅 `category = drug` 非空 |
| `gather_profile` | block | 是 | 仅 `category = material` 非空 |
| `tcm_profile` | block | 是 | **P0 恒空**(P1a 才填) |
| `inflicts_injury` | string \| string[] | 是 | 外键 → 9 的 `injury_id`;**仅 `category = weapon`**。⚠️ **2026-09-17 语义降级(25 · R13 / A20)**:读作「该武器所属线**可产生的伤情集合**」(约束校验字段,非逐次命中的真源 —— 真源 = 25 动作行 `maps_to_injury`);取单值还是列表**归数值/数据一轮(OQ-21a 侧)定**,P0 校验语义按集合 ⊇ 线动作映射集执行 |

> ⚠️ **授权载体是硬规定(2026-09-14 二轮复核补,D-21-13 的补强)**:
> **`ItemDef` / `Recipe` 的唯一合法作者态 = `assets/data/*.json` 文本**。
> **禁止**把它做成 ScriptableObject / prefab / 任何 Unity 序列化承载类型 ——
> 原因:C# enum 在 `.asset` / `.prefab` / `.unity` YAML 与 `JsonUtility` 中**默认序列化为 int**,
> 枚举一旦按 int 落盘,**P1a 在中段插入 `honey_fried` / `dry_fried` 就会静默重映射老存档**
> (D-21-13)。JSON 里 `processing_state` 是**明文字符串**,插入新值不影响旧值。
> **AC-21a-26 因此必须上移到数据产物层**(扫 `.json` / `.asset` 有无 int 编码的 state),
> **不能只查代码字段类型** —— 后者结构上抓不到已落盘的 `.asset`。

**B. `drug_profile`(扩 11 处方 / 12 药物槽)**

| 字段 | 类型 | P0 | 说明 |
| --- | --- | --- | --- |
| `indications[]` | string[] | 可空 | 适应症(指向 9 的病种 id) |
| `contraindications[]` | string[] | 可空 | 禁忌 |
| `dose_range` | {int, int} | 可空 | 剂量范围 |
| `drug_potency` | **Q16.16 int** | 可空 | **药效幅值(D-21-22,原 `Offset`)** —— **9 处置事件 `Offset` 的来源**;静态基础幅值,**不受品级调制**(品级只调时间轴,见 F5) |
| `onset` | **Q16.16 int** | 可空 | 起效(时间轴) |
| `peak` | **Q16.16 int** | 可空 | 达峰 |
| `half_life` | **Q16.16 int** | 可空 | **半衰期 = 9 的 `τ_half` 的来源** |
| `elimination` | **Q16.16 int** | 可空 | 消除 |
| `quality_axis` | enum | 可空 | **F5 作用轴**(D-21-14 / **D-21-23**):`half_life`(**P0 唯一可用**);`onset｜peak｜elimination` 标 **P1a** —— 9 的载荷暂无落点,构建期在 P0 期拒绝非 `half_life` 值 |
| `axis_offset_by_quality[]` | **Q16.16 int[]** | 可空 | **F5**:品级 → 时间轴档位偏移(长度 = `MAX_QUALITY`,见 F5) |
| `drug_quality_character[]` | **string[]** | **`MAX_QUALITY > 1` 时必填**(2026-09-25 R13 = 甲 同批,原「可空」改判;块级其余字段仍可空) | **成药侧品级定性修饰**(D-21-24,长度 = `MAX_QUALITY`);由 42 以**药签措辞 / 外观**呈现,见 **U-6** |

> **D-21-6 的要点是「字段必须在」,不是「块非空」** —— P0 允许上表全为空,
> 但**字段本身必须存在**,且时间轴四字段**类型必须是 Q16.16 int**(D-21-9)。
> **⚠️ 2026-09-25 R13 = 甲 例外**:上表「全为空」**不再无条件成立** ——
> `drug_quality_character[]` 在 `MAX_QUALITY > 1` 时**必填**(块级豁免就地收窄,
> 其余字段仍随块可空)。
>
> **二轮新增(D-21-14)** —— **F5 的轴从「药效幅值」改为「时间轴作用点」**:
> `quality_axis` 指定品级偏移落在哪一个时间轴参数上(如 `onset`),`axis_offset_by_quality[]`
> 给出各档的偏移量(**Q16.16**,与 `Axis_base` 同量纲,不再是「整数档位」)。
> **`axis_offset_by_quality[]` 已加入 `FixParse` 解析集**(逐元素经 `FixParse`),
> 且**长度必须 = `MAX_QUALITY`**(构建期校验,见 §Edge Cases)。
>
> **三轮澄清(D-21-22 / D-21-23 / D-21-24)** —— 二轮换轴时**误删了幅值字段**(9 仍点名要它),
> 本轮补回并**改名 `drug_potency`**以绝与「品级幅值轴」混淆:
> **幅值本身不废,废的是「用品级去调幅值」** —— 品级只调时间轴(F5),幅值是**静态基础值**。
> 且 `quality_axis` **在 P0 收窄为 `half_life`**(其余三轴在 9 的处置载荷中无落点,产出即被丢弃)。
> 成药侧品级的**定性感知**另立 `drug_quality_character[]`(D-21-24),与原料侧的
> `quality_character[]`(§Schema C)成对。

**B2. `quality_character[]`(品级的定性载体 —— D-21-16)**

| 字段 | 类型 | 位置 | 说明 |
| --- | --- | --- | --- |
| `quality_character[]` | **string[]** | `gather_profile`(原料侧) | 每档一个**定性修饰**,长度 = `MAX_QUALITY`。⚠️ **原示例 `["新采带露","干燥","陈放","虫蛀"]` = 示范废例,勿抄**(2026-09-25):仅 4 项(长度 5)且**含 OQ-17-9 禁词「陈放」**(采后储藏属性,P0 无仓储不可达)—— 该例正是禁词的来源流。**P0 实词见 `assets/data/item_database_items.json`**(词集经用户审定 + OQ-17-9 筛) |

> **这是锚点二「品级的质地」的呈现载体**(D-21-16,2026-09-14 用户裁定⑧)。
> **不是数字,不碰定点域** —— 是 `string[]`,由 **42 拟物 UI** 以**外观 / 药签措辞**呈现
> (合 U-1/U-2:禁数字、禁线性刻度)。**玩家看出「这一株不一样」,但读不出数值。**
> ~~**P0 可空**(此时退回按 `quality_distribution` 抽档,质地靠 F5 的时间轴偏移在病程上体现)。~~
> **✅ 2026-09-25 用户裁定(R13 = 甲):`MAX_QUALITY > 1` 时最小非空**(长度 = `MAX_QUALITY`
> 且逐档非空)—— 原「可空」改判。**改判理由即下方 ⚠️ 自证的矛盾**:可空退路写「质地靠 F5」,
> 而 F5 摸不到原料 ⇒ 空列时原料侧品级**零合法感知通道**(42 走查 U-5 必红)。
> ~~**回写执行义务(21a 实现轮,本裁定落笔不代绿)**:① P0 出货条目数据填词(原料 × `MAX_QUALITY` 档);
> ② 门扩(空列 ⇒ 构建期硬失败)+ 负向 fixture;③ 测试翻绿。~~
> **✅ 回写执行完成(2026-09-25 同日)**:① `willow_bark/raw` 填 5 档
>(`枯脆细碎/皮薄色暗/条匀皮厚/条肥色正/皮厚丝丰`,过 OQ-17-9 语义筛,词集经用户审定);
> ② `DrugProfileGates` 空列/空白档硬失败 + 负向 fixture ×2(`invalid_gather/drug_char_empty.json`);
> ③ EditMode **484/484** + PlayMode **15/15** + 烘焙(items cooked 444→604B,ConfigVersion `0x12F85A18`)。
> ⚠️ **F5 只管成药**(`category = drug` 有 `drug_profile`);**原料侧的质地由本字段承担** ——
> 这正是二轮推翻「F5 单轴承担锚点二」的原因:F5 摸不到原料。

**C. `gather_profile`(扩 17 采集)**

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `ecosystem` | string | 产地生态区(**外键 → 6 世界与生态区**) |
| `parts[]` | string[] | 可采部位 |
| `quality_distribution` | block | 品级分布(**形状由 17 定**;支撑必须 ⊆ `[1, MAX_QUALITY]`) |
| `qty_per_node` | int > 0 | 单次采量基数(**21a 给基数,17 给动作**) |
| `quality_character[]` | **string[]** | 每档定性修饰(长度 = `MAX_QUALITY`;**`MAX_QUALITY > 1` 时最小非空 —— 2026-09-25 R13 = 甲 改判,原「P0 可空」废止**)—— 见 §Schema B2 / D-21-16 |

**D. `tcm_profile`(扩 16 中药选项)** —— **P0 恒空**。字段表由 P1a 撰写时补,本节此处仅占位。

**E. `ItemInstance`(运行时实例 —— **闭集纯 POD**)**

```
ItemInstance = { instance_id: long, item_key: (base_id, processing_state), quality: int, qty: int,
                 children: long[] }
```

- **`instance_id`** —— 由 `IIdAuthority` 单调发放(与 9 的 `Seq`/`PatientId` 同模式);**必须入快照**,
  否则重复写入无法去重。
  ⚠️ **该权威目前在 ADR-005 里不存在**(`IIdAuthority` 只有 `PatientId Next()`)——
  主机迁移后计数器若复位即**静默重号**(两件物品共用一个 id,**不崩溃、不回放失配,只表现为「物品悄悄合并/丢失」**)。
  **契约缺口已登记 D-21-26**,17 采集的铸造权缺口登记 **D-21-27**,验证见 **AC-21a-63**。
- **`qty`** —— **必须入快照**;**裁定:物化存储,不在读档时重算**(重算 F1 会让存档往返改数量)。
- **禁任何 `UnityEngine` 类型字段**(`ScriptableObject` / `Sprite` / `GameObject` / 预制体引用)。
  Unity 序列化**乐意**把 SO 引用存成引用,会静默违反「存快照不存引用」。
- **容器(药箱)用「子实例 id 列表」表达**,由 20 解析 —— **不得**嵌套实例(会造出递归引用图)。
  列表承载字段 = **`children: long[]`**(三轮 blocking #2 补 —— 此前 §Edge Cases / AC-21a-34 要求
  「子实例 id 列表」,而 Schema E 的闭集 POD **无此字段**,契约缺自己要求的形状)。
  - **非容器实例的 `children` 为空数组**,不是 `null`(禁可空,减一个静默状态)。
  - **容器的 `qty` 恒为 1、`quality` 恒为 1**(容器自身不堆叠、无品级);**`InstanceWeight` 是否折算子件
    由 20 决定**(21a 只给形状,见 52 的 `CarryLoad` 归属)。
  - **`children` 里的每个 id 必须在同一快照内有对应实例**(闭环不变式,由 7a 校验)。
- ⚠️ **静态断言须递归走类型图**(2026-09-14 二轮复核补):只查「字段声明类型是否 `UnityEngine.*`」
  **漏掉三类** —— ① `[SerializeReference]` / `interface` / `abstract` / `object` 字段可装任意子类,
  子类里塞 `Sprite` 照样序列化;② Unity 序列化器**乐意**把引用存成引用;③ 嵌套在 SO 里时引用从父级渗入。
  **断言必须禁止 `[SerializeReference]` / 接口 / 抽象字段,并递归检查类型图**(见 AC-21a-27)。
- ⚠️ **实例中若含 `Fix` 字段(如疗效快照),不得经 Unity 序列化器**(D-21-18):见规则九 / ADR-006 **§Decision 五**。

**F. `Recipe`(配方表的一条)** —— 字段见规则五;完整字段表:

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `recipe_id` | string | 稳定标识 |
| `owner` | enum `{process, craft, build}` | **配方归属系统的显式字段**(2026-09-19,D-21-30,18 R-18-B):子集划分唯一判据;缺字段 = 装载硬失败,校验见 **AC-21a-66** |
| `inputs[]` | `{item_key, qty>0}[]` | **非空**(否则是凭空造物);`qty` = **基数,非实耗**(D-21-15,实耗 = `Ceil(qty/EFF)`) |
| `outputs[]` | `{item_key, qty>0}[]` | **非空**(销毁不归配方表);`qty` = **产出基数** —— **逐条即 F1 的 `outputs_i.qty`**(三轮 blocking #1/#2:此前**不在任何 schema**;原稿 §Schema F 写复数、F1 却写单数 `Recipe.BaseQty`,是**形状断裂**) |
| `duration_ticks` | int > 0 | 逻辑 tick 数(单位是 tick,**不是秒**) |
| `skill_gate` | int ∈ [0, SKILL_CAP] | **门槛**:低于此不可发起 |
| `min_quality` | int ∈ [1, MAX_QUALITY] | **准入闸**:输入品级下限(**非品级出口**,出口见 F5) |
| `boundary_state[]` | state 对[] | 本配方声明的合法 state 通路 |

> ⚠️ **`inputs[].qty` 语义变更(D-21-15,2026-09-14 用户裁定⑦)**:它是**基数**,
> 结算是 `ActualConsumed = Ceil(qty / EFF)` —— **技能高 → 实耗更少**。
> **基数进静态数据(`assets/data/*.json`),实耗进 `Craft` 事件载荷**(AC-21a-52)——
> 把实耗冻进静态数据,技能值一变就与重算不符。**回放由「基数 + 当时的 `EFF`」重演实耗**,
> 而不是回读一个已冻结的数。

### States and Transitions


`processing_state` 是**全局词汇表**,但**哪些转化合法是逐条物品的数据**:

| 从 | 到 | 加工名 | 归属系统 |
| ---- | ---- | ---- | ---- |
| `raw` | `dried` | 干燥 | 18 炮制 |
| `dried` | `extracted` | 提取 | 18 炮制 |
| `dried` | `tincture` | 浸制 | 18 炮制 |
| `extracted` | `pill` | 成型 | 18 炮制 |
| *(P1a)* `raw` | `honey_fried` | 蜜炙 | 18 炮制(中药) |
| *(P1a)* `raw` | `dry_fried` | 清炒 | 18 炮制(中药) |

> ⚠️ **这张图不是每种物品都全通。** 柳树皮可能只有 `raw → dried → extracted`;
> 毛地黄可能有 `raw → dried → tincture`。**合法序列由物品表逐条声明**,
> 全局枚举只提供**词汇**,不提供**通路**。这防止「所有植物都能随便加工成任何形态」
> 这种把采集变成刷子游戏的退化。

### Interactions with Other Systems

| 系统 | 方向 | 接口与数据流 |
| ---- | ---- | ---- |
| **20 库存** | 读 | `stackable / stack_max / weight / category`;**品级是实例属性**(规则三) |
| **17 采集** | 读 | `gather_profile`:产地生态区、可采部位、品级分布、单次采量 |
| **18 炮制** | 读 + 写 | 读 `Recipe`(炮制子集);**写**「消耗 inputs / 产出 outputs」 |
| **11 处方用药** | 读 | `drug_profile`:适应症、禁忌、剂量范围、时间轴 |
| **12 药物槽** *(P1a)* | 读 | `drug_profile` 的**时间轴曲线**(不是单个数字,见 D-21-6) |
| **16 中药选项** *(P1a)* | 读 | `tcm_profile`(P0 **为空**) |
| **19 制作与器械** *(P1a)* | 读 | `Recipe`(制作子集) |
| **9 疾病与伤情模拟** | **双向** | **入向**:物品带 `inflicts_injury` 外键(9 拥有伤情枚举,规则七 / C4)。**出向**:`drug_profile` 的 **`drug_potency` → 9 的处置事件 `Offset`**、**`half_life` → 9 的 `tau_half`**(D-21-22 / D-21-23;字段名对齐归 **D-21-25**) |
| **7a 持久化服务** | 读 | 必须能序列化 `ItemInstance`(含 `quality`);**不存 `item_key` 的引用,存快照**;`instance_id` 的权威契约见 **D-21-26** |

> **反向约束(重要)**:12 药物槽需要**时间轴**,不是「药效强度」一个数 ——
> 这**反向强迫** `drug_profile` 从 P0 起就不能只有标量。这就是 D-21-6 的由来。

## Formulas

> **权威边界(Q1 裁决)**:21 持有**唯一配方结算求解器**。
> **17 采集 / 18 炮制 / 19 制作只产出输入参数,不得自建结算逻辑。**
> 这是 `systems-index.md` §9 **C5** 的推广 —— C5 管的是 10/11(判定 + 熟练度),
> 形状完全相同:两套算法 = 「两种路径都满足」破产。
>
> **🔒 整数域纪律(D-21-9 · ADR-006)**:以下三式的**出参与落盘量一律是 `int`**。
> 中间量可用定点(scale 见 ADR-006),但**存档中永不出现 `float`**。
> **舍入口径统一**:`ROUND_HALF_AWAY_FROM_ZERO`(四舍五入、.5 远离零),
> **在整数域完成**(先在定点域算到目标精度,再一次性舍入),**禁止依赖 `Math.Round` 的默认 ties-to-even**。

---

### F1 配方结算(唯一求解器)

The recipe settlement formula is defined as:**逐 `outputs[i]` 套用**(三轮 blocking #2 —— 配方是 `outputs[]`,不是一个标量):

`OutputQty_i = max( 1 , Round( outputs_i.qty × QtyMultiplier ) )`   ← **`outputs_i.qty` 就是该条的 `BaseQty`**

`QtyMultiplier = clamp( 1 + ΣM , QTY_MULT_MIN , QTY_MULT_MAX )`   ← **一条配方一个,不逐条**

`ΣM = SkillMod + QualityMod + EquipMod + EnvMod_total`

`EnvMod_total = clamp( EnvMod_climate + EnvMod_clinic , ENV_MOD_MIN , ENV_MOD_MAX )`
  ← **2026-09-19(18 首轮评审 R-18-C / D-21-31)**:F1 的入参是**两个未钳制分量**
  —— `EnvMod_climate`(5 的 `EnvMod_raw`,块哈希)+ `EnvMod_clinic`(24 的医馆分量);
  **求和与唯一钳制发生在 F1 正文(本节),不落 5、不落 24、不落 18**(18 只原样透传)。
  钳制此前全文**无处执行**(5:395 与 AC-5-19 互相指认、21a `:484` 是入参断言非操作)——
  本式补齐执行落点。

`ActualConsumed_j = Ceil( inputs_j.qty / EFF )`   ← **投入端实耗(D-21-15;EFF 的运行期出口)**

**守恒律(规则八 · D-21-21)**:`Σ(weight × OutputQty_i) ≤ EFF_MAX × Σ(weight × ActualConsumed_j)`,`EFF_MAX ≤ 1`。
**两侧都封** —— 产出侧**含 `QtyMultiplier`**(否则运行期可击穿构建期检查)。

**Variables:**

| Variable | Symbol | Type | Range | Source | Description |
|----------|--------|------|-------|--------|-------------|
| 配方基础产出 | `outputs_i.qty` | **int** | **> 0**(校验硬失败) | 配方表 `outputs[]` | **逐条**产出基数;`m = 1` 时即原稿的 `Recipe.BaseQty`。**不存在配方级单一 `BaseQty`**(三轮 blocking #2:原稿 F1 写单数、Schema F 写复数) |
| 技能修正 | `SkillMod` | int(定点中间) | 0 ~ `SKILL_MOD_CAP` | 18 炮制 | `SkillMod = cap × Level / SKILL_CAP` —— **18 只传等级,不传结果**(修正曲线由 21 定义)。`Level` 与 F2 的 `CraftSkill` **是同一个量** |
| 品级修正 | `QualityMod` | int(定点中间) | 0 ~ `QUAL_MOD_CAP` | 输入实例 | `QualityMod = cap × (InQ − 1) / (MAX_QUALITY − 1)` —— **曲线由 21 定义**;`n>1` 时 `InQ = min(各输入 quality)` |
| 设备修正 | `EquipMod` | int(定点中间) | 0 ~ `EQUIP_MOD_CAP` | 19 制作 · **24 医馆机器** | 器具档位、丹房加成 |
| 环境修正 | `EnvMod_total` = `clamp(EnvMod_climate + EnvMod_clinic, ENV_MOD_MIN, ENV_MOD_MAX)` | int(定点中间) | `ENV_MOD_MIN` ~ `ENV_MOD_MAX`(**钳制后**;两分量入参**各自无域**,D-21-31) | 5 时间天气(`EnvMod_climate`)· **24 医馆机器**(`EnvMod_clinic`) | **可为负**(火候难控、背阴)。**求和+钳制在 F1 正文执行** —— 源系统只供未钳制分量 |
| 产出取整 | `Round(·)` | — | — | 本文 | **`ROUND_HALF_AWAY_FROM_ZERO`,整数域完成** |
| 产出非零地板 | `max(1, ·)` | int | ≥ 1 | 本文 | **结构性保证产出 ≥ 1**(见下 🔑) |
| 转化效率 | `EFF` | 定点 | `EFF_MIN` ~ `EFF_MAX` ≤ 1 | F2 | **运行期出口**:实耗的除数(技能高 → 省料) |
| 单料实耗 | `ActualConsumed_j` | int | ≥ `inputs_j.qty` | 本式 | **运行期派生量**(`Ceil(base / EFF)`);**随该次 Craft 历史事件落事件流**,**不写进配方数据文件、也不作为物件实例字段**(D-21-15,AC-21a-52) |

> ⚠️ **四式归属(2026-09-14 复核 blk #4 的兑现)**:上面给出的是**曲线形状**,
> 具体系数(`*_CAP` / `ENV_MOD_*`)是**调参旋钮,数值由用户裁定**。
> **所有四项的源系统都必须回登 §Dependencies** —— 原表漏了 5 与 24,是未登记依赖。

> 🔑 **修正曲线必须在 Q16.16 原始整数域求值,不在 `int` 域**(三轮 blocking #6)。
> `SkillMod = cap × Level / SKILL_CAP` 若**先按 `int` 算**,则 `Level < SKILL_CAP` 时结果**恒为 0**
> (`0.3 × 30 / 60` 在整数除法下 = `0`),`SkillMod` 退化成**两段阶跃**(0 或满值)—— 曲线死亡。
> **正解**:`cap` 本身是 Q16.16 原始量(如 `0.3` → `19661`),
> `SkillMod_raw = cap_raw × Level / SKILL_CAP`(先乘后除,全程 `long`)—— `19661 × 30 / 60 = 9830` ✓。
> ⚠️ **2026-09-24 订正(算式口径)**:上例的 `9830` 是**截断**写法,与本节 `:463` 及
> **ADR-006 §Decision 三**(Accepted)的「全部舍入 `ROUND_HALF_AWAY_FROM_ZERO`」**不一致** ——
> 按舍入契约,`19661 × 30 / 60 = 9830.5` 中点远离零 ⇒ 实际结果 = **`9831`**。
> **规则优先于示例**:实现(Story 003 `CurveScaled`)取 `9831` 为准。本示范数保留原文以留闭环记录,
> 但**不得**据它写黄金期望值(差 1 raw LSB = 1/65536,刀刃情形可使某条 `OutputQty` 差 1)。
> **同一条纪律适用于 `QualityMod` 与 `Retain`/`EFF` 的插值**(`EFF = EFF_MIN + (EFF_MAX − EFF_MIN) × S / SKILL_CAP`)
> —— **只有最后落到出参时才一次性舍入**(ADR-006 §Decision 三)。这就是本文开头那条「整数域纪律」的具体含义。

**为什么是「相加后乘」而不是「逐项相乘」**:相加**与顺序无关** ——
TD 要求「定死叠加顺序」,取相加则顺序问题**自动消失**;且每一项的边际价值不会互相放大,
玩家可分别理解「技能不够」和「器具不行」。

> ⚠️ **前提:定点域内成立**(2026-09-14 复核 blk #5)。**在 `float` 域,「相加与顺序无关」是假的**
> ((a+b)+c ≠ a+(b+c))。本论证**只在 D-21-9 的整数域下有效** —— 这不是附加说明,是它成立的条件。

**Output Range:** 逐条 `1` 到 `Round(outputs_i.qty × QTY_MULT_MAX)`。**下界 = 1(结构性,逐条)。**

> 🔑 **构建期守恒上界(三轮 blocking #1 —— 原式漏产出侧乘子)**:
> ```
> QTY_MULT_MAX × Σ( weight × outputs_i.qty )  ≤  EFF_MAX × Σ( weight × inputs_j.qty )
> ```
> 原稿两侧都用**基数**、**漏了产出侧的 `QtyMultiplier`** ⇒ 构建期通过、运行期击穿(「凭空造物」)。
> **实测反例**:`w_in = 10`、`w_out = 9`、`EFF_MAX = 1`、`QTY_MULT_MAX = 2` —— 构建期 `9 ≤ 10` ✓,
> 运行期 `QtyMultiplier = 2` ⇒ 产出 `18 > 10` ✗。**这一条同时锁死 AC-21a-9 想把 `QTY_MULT_MAX` 抬高的方向**。
>
> ⚠️ **本式仍不充分(2026-09-19,18 首轮评审 D-21-32 / O-18-R3)**:聚合式与运行期
> **逐条** `max(1, Round(·))`(`ROUND_HALF_AWAY_FROM_ZERO`)**不同形**,可被击穿 ——
> **实测反例**:`w_in = 10`、`w_out = 6`、`EFF_MAX = 1`、`QTY_MULT_MAX = 1.5`、单条 `qty = 1` ⇒
> 构建期 `1.5 × 6 × 1 = 9 ≤ 10` ✓;运行期 `Round(1 × 1.5) = 2`(HALF_AWAY 上取整)⇒
> 产出 `2 × 6 = 12`、实耗 `Ceil(1 / 1) = 1`(`1 × 10 = 10`)⇒ **12 > 10** ✗。
> **缝在 `Round`,不在 `max(1,·)`**。
> **修法 = 构建期改跑与运行期同形的逐条极值式**:
> ```
> Σ( weight_out × max(1, Round(outputs_i.qty × QTY_MULT_MAX)) )
>     ≤ EFF_MAX × Σ( weight_in × Ceil(inputs_j.qty / EFF_MAX) )
> ```
> (产出侧:逐条取整后的最坏值;投入侧:实耗的最小值 `Ceil(qty/EFF_MAX)`。
> 逐条式蕴含聚合式在 `max(1,·)` 地板不起作用的子域上等价,在地板/取整起作用的域上更严。)
> **归 21a 烘焙管线的执行 AC = 新 AC-21a-65(本节随 D-21-32 登记,AC 表见下)**;
> 18 侧的守恒 AC(AC-18-19)在本修回写前记 EXTERNAL·BLOCKED-BY,不得记绿。

> 🔑 **`max(1, ·)` 才是 Q4 的机制实现 —— 原稿的 `QTY_MULT_MIN > 0` 不够**(2026-09-14 二轮 blocking #1)。
> 原稿断言「求解器在数学上不可能产出零」,但 `Round(1 × 0.3) = 0`(二轮实测)——
> `QTY_MULT_MIN > 0` **不蕴含** `OutputQty ≥ 1`,且该错误断言已冻结进 `entities.yaml`。
> **现改为 `OutputQty = max(1, Round(·))`**:产出非零**是公式的结构**,不是旋钮的取值承诺。
> 「炮制没有完全失败」于是**无法被任何旋钮破坏** —— 想要「产出归零」必须删掉 `max(1, ·)`,
> 而删了会立刻违反 AC-21a-1。

> 🔑 **代价放在哪:投入损耗,不是产出归零**(D-21-10 + D-21-15)。
> 上轮只把损耗写成静态不等式,而 `inputs[]` 是静态量、技能改不动它 —— **规则八的意图不可表达**。
> 二轮修正:`inputs[].qty` 是**基数**,`ActualConsumed = Ceil(qty / EFF)` 是**实耗**:
> **技能高 → 实耗贴近基数;技能低 → 实耗翻倍**。因为 `EFF ≤ 1` ⇒ `ActualConsumed ≥ base`
> (**满技能 `EFF = EFF_MAX = 1` 时取等号 = 零损耗**;原稿写「恒损」在 `EFF = 1` 处为假,三轮已改) ——
> 「转化是有代价」**逐次结算都成立**,不再是构建期口号。

> 🔑 **四 cap 与 `QTY_MULT_MAX` 的关系**:构建期校验 **`Σ(正的 cap) + max(0, ENV_MOD_MAX) ≤ QTY_MULT_MAX − 1`** ——
> 否则高投入段被 `clamp` **静默截断**(投入无回报);反向若 `Σcap` 远小于上限,则该旋钮是**死值**。
> ⚠️ **`ENV_MOD_MAX` 必须计入**(2026-09-14 二轮补):`EnvMod` 可取正,原式只约束 `*_CAP` 会漏它。

**Example:** *(数值待用户定 —— 此处只示范形状,且**示范为整数**)*
设 `m = 2`:`outputs = [{salycylic_acid, qty: 1}, {tar, qty: 2}]`;技能 +0.3、品级 +0.1、设备 +0.2、环境 −0.1
→ `ΣM = 0.5` → `QtyMultiplier = clamp(1.5, QTY_MULT_MIN, QTY_MULT_MAX) = 1.5`
→ 逐条:`Round(1 × 1.5) = 2`、`Round(2 × 1.5) = 3` → `OutputQty = [2, 3]` —— **`QtyMultiplier` 是配方级、`max(1,·)` 是逐条**。
> 原稿此处写 `OutputQty = 1.5`(标量),而规则五 / F4 说 `Qty` 是 `int` —— **该矛盾已由 D-21-9 消除**;
> 原稿的**单数 `BaseQty`** 与 Schema F 的复数 `outputs[]` 的形状断裂,由**三轮 blocking #2** 消除(逐条 `outputs_i.qty`)。

> **下界示范(三轮修正 —— 原稿该例自相矛盾)**:同一 `outputs_1.qty = 1`,但环境极差 `ΣM = −0.8`
> ⇒ `QtyMultiplier = clamp(0.2, QTY_MULT_MIN = 0.3, ·) = 0.3` → `Round(1 × 0.3) = 0` → `max(1, 0) = 1` ——
> **结构性非零**(这正是二轮 blocking #1 的修法)。
> ⚠️ 原稿写「`ΣM = 0.5` 但 `QTY_MULT_MIN = 0.3`」—— 二者不能同时成立(`0.5` 的 `ΣM` 给出 `1.5`,与 `0.3` 无关),
> **该例无法执行**;此处改为真正能落到 `Round(·) = 0` 的负 `ΣM` 组合。

---

### F2 品级传导(输入定上限,技能定效率,**不出品级出口**)

The quality transmission formula is defined as:

`OutputQuality = clamp( Round( InputQuality × Retain(CraftSkill) ) , 1 , InputQuality )`

`Retain(S) = RETAIN_MIN + (RETAIN_MAX − RETAIN_MIN) × ( S / SKILL_CAP )`

`EFF = EFF_MIN + (EFF_MAX − EFF_MIN) × ( S / SKILL_CAP )`   ← **技能的主出口(在 F1 消费)**

**Variables:**

| Variable | Symbol | Type | Range | Source | Description |
|----------|--------|------|-------|--------|-------------|
| 输入品级 | `InputQuality` | int | 1 ~ `MAX_QUALITY` | 物品实例 | 原料的品级;**`n>1` 时 = `min(各输入 quality)`** |
| 炮制技能等级 | `CraftSkill` | int | 0 ~ `SKILL_CAP`(=60) | 30 技能 | **与 F1 的 `Level` 同一个量** |
| 品级保留率 | `Retain(S)` | 定点 → 出参 int | `RETAIN_MIN` ~ `RETAIN_MAX` | 曲线 | 技能越高保留越多 |
| **转化效率** | `EFF` | 定点 | `EFF_MIN` ~ `EFF_MAX` ≤ 1 | 曲线 | **技能的主出口** —— 但**在 F1 被消费**(`ActualConsumed = Ceil(base/EFF)`),不是本式的出参 |
| 技能上限 | `SKILL_CAP` | int | 60 | `skill-system.md` | **引用,非本表拥有** |

**Output Range:** `1` 到 `InputQuality`。**舍入口径**:`ROUND_HALF_AWAY_FROM_ZERO`,整数域完成。

> 🔑 **clamp 的上界是 `InputQuality`,不是 `MAX_QUALITY`。**
> 这就是 Q3 的「**输入定上限,技能定能保住多少**」——
> **炮制永远不能把品级提上去**,只能尽量不损失。
> 若上界放到 `MAX_QUALITY`,玩家就会拿垃圾原料反复炮制刷品级,采集技能白给。

> 🔑 **技能的主出口不在 F2 —— 在 `EFF`,而 `EFF` 在 F1 被消费**(2026-09-14 二轮修订)。
> 原设计把技能押在 F2 的 5 档阶梯上:`InputQuality = 1` 时 `clamp(·,1,1)` **恒等**
> ⇒ **炮制技能对一级原料零效果**,线体感死亡。
> **首轮修法**把技能的连续出口移到 `EFF` —— 但**只写进了构建期守恒检查,F1/F2 运行期均不消费**
> ⇒ `EFF` 是**死变量**(二轮 blocking #2)。
> **二轮修正(D-21-15)**:`EFF` 进入 F1 的**投入端**(`ActualConsumed = Ceil(base/EFF)`),
> **省料是运行期每一步都在发生的效果**。F2 退为「高品级输入是否保值」这一个离散问题。

**曲线形状**:`RETAIN_MIN`(技能 0 时)到 `RETAIN_MAX`(技能 60 时)。
两者都应为 `(0, 1]` 区间的值 —— **`RETAIN_MAX = 1.0` 表示满技能可以完全保住品级**。
`RETAIN_MIN ≤ RETAIN_MAX` 且 `RETAIN_MAX ≤ 1` 是**构建期校验**(`RETAIN_MAX > 1` 会被 clamp 掩盖,抓不住)。
具体取值 = **调参旋钮,待用户定**。


---

### F3 采集品级分布(21 只提供**数据形状**,17 拥有**选择规则**)

采集**不是转化**,是「从世界提取」,因此**不归 F1 的唯一求解器**。
21 只定义 `gather_profile` 的结构,17 采集决定「这次采到什么品级」。

```
gather_profile = {
  ecosystem,             // 产地生态区(外键 → 6 世界与生态区)
  parts[],               // 可采部位
  quality_distribution,  // 品级分布(形状由 17 定)
  qty_per_node,          // 单次采量基数
  quality_character[]    // 每档定性修饰(D-21-16;长度 = MAX_QUALITY;MAX_QUALITY>1 时必填 —— 2026-09-25 R13=甲 改判,原「P0 可空」废止)
}
```

> **边界声明**:21 拥有**「这株植物在什么生态区、能采到什么品级带」这个事实**;
> 17 拥有**「抽中哪一档」这个动作**。两者不可互换。
>
> ⚠️ **已登记的双向耦合(2026-09-14 复核 blk #8)**:`quality_distribution` 的**支撑**
> 必须 ⊆ `[1, MAX_QUALITY]`,而 `MAX_QUALITY` 由 **21 拥有**、分布形状由 **17 拥有**。
> `MAX_QUALITY` 若从 5 降到 3,**会静默使全部已有分布失效**。已登记 **D-21-7**;
> 构建期校验:分布支撑 ⊆ `[1, MAX_QUALITY]`。另 `qty_per_node → ItemInstance.qty` 的映射由 17 定义。

---

### F4 堆叠与重量

The stacking and weight formulas are defined as:

`InstanceWeight = ItemDef.weight × Qty`

`StackKey = ( item_key , quality )`   ← 见 Core Rules 规则六

**Variables:**

| Variable | Symbol | Type | Range | Source | Description |
|----------|--------|------|-------|--------|-------------|
| 单件重量 | `ItemDef.weight` | **int** | > 0 | 物品表 | **整数最小单位个数**(D-21-9);**禁 float** |
| 数量 | `Qty` | int | 1 ~ `ItemDef.stack_max` | 物品实例 | |
| 实例重量 | `InstanceWeight` | int | — | 本式 | 整数;进 52 的 `CarryLoad` 亦为整数 |
| 重量单位 | `weight_unit` | string | 配置项 | 物品表 | ⚠️ **见下** |

> ⚠️ **重量必须为整数(2026-09-14 复核 blk #6)**:`weight` 会进 20 的负重阈值比较,
> 也会进 52 的 `CarryLoad → StrengthTier → 事件权重`。**float 累加的顺序会翻转 `≤ 上限` 的判定**
> (20 堆 ×99 件按「逐件累加」与「按堆乘」差 ~1e-12)。**故 `weight` 是整数最小单位的个数。**
> **展示单位是另一回事**,见下。

> ⚠️ **重量单位是一个数据层配置项,不是一个写死的名词。**
> 按**支柱五(史实为骨)**:清末民初 **库平两 / 市斤 / 公制** 三套并行,
> 而「1929《度量衡法》市用制」「协和医学院公制教学」「1930《中华药典》采公制」
> 三条断言中**有两条未获独立验证** ——
> **因此 21a 不写任何单位名称**,只声明「重量有单位,单位可配置」。
> 具体名称待专项考据后再定(已登记 Open Questions / D-21b-2)。
>
> ⚠️ **数值冻结令(2026-09-14 复核)**:**单位未定前,21b 不得填写任何重量数值** ——
> 库平两 ↔ 克相差 ~37 倍,**单位一换,全表已填数值作废**。
> 另:**存储单位与展示单位分开** —— 存储用整数最小单位,`weight_unit` 只做**显示换算**。
>
> **展示层(非本系统)**:按视觉锚点**支撑原则二**,重量应当**可被主角拿在手里、亲手读取**,
> **不得以数字 HUD 呈现**。**具体呈现装置由 42 / 20 决定,21a 不规定也不会替它选**(见 §Visual/Audio)。

---

### F5 品级 → 时间轴作用点(档位偏移 · D-21-14)

> **这是品级的非数量出口**,也是 §Player Fantasy 锚点二的机制凭据。
> **二轮重写(D-21-14)**:首轮把 F5 做成「药效幅值档位」—— **同轴换皮,不交付幻想**;
> 现改为 **时间轴作用点偏移** —— 高品级让药**见效更快 / 更持久**,这是**不同种类**的差别。

The quality-to-timeline formula is defined as:

`Axis_effective = Axis_base + axis_offset_by_quality[ quality − 1 ]`

其中 `Axis_base` = `drug_profile` 的 `quality_axis` 所指那个时间轴参数。
**P0 下 `quality_axis` 只允许 `half_life`**(D-21-23)—— 其余三轴(`onset`/`peak`/`elimination`)
**在 9 的处置载荷里没有落点**,产出即被丢弃 ⇒ P0 构建期**拒绝**非 `half_life` 取值。

**Variables:**

| Variable | Symbol | Type | Range | Source | Description |
|----------|--------|------|-------|--------|-------------|
| F5 作用轴 | `quality_axis` | enum | **P0:`half_life`(唯一)**;`onset｜peak｜elimination` 标 **P1a** | `drug_profile` | 品级偏移落在哪一个时间轴参数上。**D-21-23:P0 收窄** —— 9 的处置载荷只有 `polarity / drug_potency / half_life` 三项,其余三轴无接收方 |
| 基础值 | `Axis_base` | **Q16.16 int** | 见 11 处方 | `drug_profile.<axis>` | 该时间轴参数的基础值(**P0 即 `half_life`,与 9 的 `tau_half` 同源**) |
| 品级档位表 | `axis_offset_by_quality[]` | **Q16.16 int[]** | 长度 = `MAX_QUALITY` | `drug_profile` | **各档偏移量**,可能为负(劣药);**绝对值须 ≥ 可感知地板**(见下) |
| 有效值 | `Axis_effective` | **Q16.16 int** | — | 本式 | **进 9 的事件流的值(即 9 的 `tau_half`)** |

**规则**:
- **轴是「作用点」不是「幅值」** —— 品级改变的是**时序形态**(多快起效 / 多久消退),
  不是「强了多少」。这正是锚点二要的「**质地**」:同一味药,这一剂的**性子**不一样。
- **P0 只有 `half_life` 一条轴是真的**(D-21-23)—— 其余三轴写进枚举会让你以为有四个出口,
  实际只有 9 认的那个能活到病史事件流。**收窄是防静默**:枚举写了四值、实际只一值,是最难查的错。
- **可感知地板(三轮 blocking #5 · D-21-24)**:`|axis_offset_by_quality[k]|` 的**非零档**
  **必须大于 9 的噪声带**(9 的病史时间轴上存在 patient 间随机抖动)——
  否则偏移**在统计上不可区分于噪声**,玩家**永远感觉不到**,而 U-1/U-2 又禁止把偏移画成数字
  ⇒ 等于**没有出口**。构建期须校验该下界(数值 = 调参旋钮,见 §Tuning Knobs 的 `F5 偏移可感知地板`)。
- **`axis_offset_by_quality[]` 与 `Axis_base` 同量纲(Q16.16)** —— 逐元素经 `FixParse`,
  **不是「整数档位」**(原措辞是 bug,会让加法量纲错 65536 倍)。
- **长度必须 = `MAX_QUALITY`**(构建期校验)—— 否则 `quality − 1` 越界读,
  **直接污染 9 的病史事件流**(ADR-005 的定点域)。
- **不跨轴、不涉极性**:品级只在 `quality_axis` 选定的那一根轴上偏移,**不触碰其它三根时间轴**;
  本式**根本不读写 `polarity`**(那是 9 的字段)—— 故「极性不变」是**本式的结构事实**,不是 21a 对 9 语义的断言(见 AC-21a-38)。
- **P0 须至少一档非零**(AC-21a-37)—— `quality_axis` 须指向一条真时间轴,
  `axis_offset_by_quality[]` 不得全 0。**全零不是「可接受的 P0 现状」** ——
  那等于锚点二在成药侧落空,**AC 不得替它背书**。
  原料侧的「质地」另有 `quality_character[]`(D-21-16)、成药侧有 `drug_quality_character[]`(D-21-24)承担;
  成药侧**必须靠本式真发生**。**具体各档填什么数 = OQ 待你裁(数值)。**
- **域钳制(三轮补)**:`Axis_effective` 须 **`> 0`** —— 9 的衰减用 `half_life` 作除数,
  **偏移把它推到 ≤ 0 ⇒ 除零 / 反向衰减**。构建期须断言 `Axis_base + min(axis_offset_by_quality) > 0`。
  > ⚠️ **🔴 2026-09-19 登记 `D-21-34`(未见裁定,勿当已改)**:本断言的**尺**与下游 11 的 F-11.2 不一致 ——
  > 11 按 **`≥ MIN_USABLE_HALF_LIFE`** 写(理由:合法极小正数如 `2` 虽不除零,却使衰减快于一个 tick ⇒
  > 玩家看到的仍是「无药效」,而两侧 AC 全过)。**把 `> 0` 升格为 `≥ MIN_USABLE_HALF_LIFE` = 改本件机制**,
  > 须走 21a 的**重开流程**(见文首重开触发条件 ⑤ 与 §Debt Register `D-21-34`),**不得就地改**。
  > 11 侧的登记点是 `prescription-and-medication.md` §Cross-References **`O-11→21a`**(BL-1)。

**Output Range:** `Axis_base + min(axis_offset_by_quality)` 到 `Axis_base + max(axis_offset_by_quality)`,
**且下界必须 `> 0`**(构建期断言;否则 9 的 `half_life` 除数 ≤ 0)。

> ⚠️ **代价(已登记)**:品级经此式**进入了 ADR-005 的定点域** ——
> 这正是 ADR-006 必须存在的原因(见 §Dependencies)。**档位表必须是 Q16.16,禁 float 字面量**。

> ⚠️ **下游影响**:11 处方 / 9 需读**时间轴档位**(它们本就要读时间轴 —— D-21-6 已为它们预留);
> **12 药物槽(P1a)** 读的「时间轴曲线」现在**随品级变化**,这是它要的语义。
> 已登记 **D-21-11**(须在 11 / 9 侧接通)。


## Edge Cases

| 情况 | 处理 | 理由 |
| ---- | ---- | ---- |
| **配方 `inputs` 为空** | **校验拒绝** | 否则是「凭空造物」,不是配方 |
| **配方 `outputs` 为空** | **校验拒绝** | 「销毁」不由配方表实现 —— 丢弃归 20 库存 |
| **输入品级为 0 或负** | 不可能 —— **品级下限恒为 1** | 采集至少产出 1 级;不存在「零级物品」 |
| **F2 算出的 `OutputQuality` ≤ 0** | clamp 到 **1** | 见 F2 |
| **`ΣM` 使 `QtyMultiplier < QTY_MULT_MIN`** | clamp 到 `QTY_MULT_MIN`(> 0),再由 `max(1, ·)` 兜底 | **产出结构性非零** —— Q4 的机制保证(**不是**靠 `QTY_MULT_MIN > 0`) |
| **配方循环(A→B→A)** | **P0 不检测** | P0 配方由开发者手写,天然无环;**21b 物品清单须人工保证无环**。若 P1a 改为数据驱动,再加检测(已登记 Open Questions) |
| **同一配方同时被 18 与 19 读取** | **合法** | 同一张表、同一求解器(D-21-5);差异只在「谁发起」 |
| **物品条目在存档存在,但物品表已删** | **物品表只增不删** | 与 `entities.yaml` 同政策:废弃标 `deprecated`,**不物理删除** —— 否则存档损坏 |
| **`stack_max = 1`** | 合法 —— 不可堆叠 | 器械、药箱 |
| **两个不同 `base_id` 拥有同名 `processing_state`** | **完全正常** | `(base_id, state)` 才是主键 |
| **配方跨 base 转化(柳树皮 → 水杨酸)** | **合法,且是 P0 的核心形态** | 配方表**不要求输入输出同 base** |
| **环境修正为负,导致 `ΣM < 0`** | 合法,由 `QTY_MULT_MIN` 兜底 | 火候难控、背阴都是真实惩罚 |
| **`CraftSkill = 0` 时的品级传导** | `Retain(0) = RETAIN_MIN > 0` → 仍保住一部分,**不归零** | 与 Q4 一致:没有「全废」 |
| **`category = drug` 但 `drug_profile` 未填** | **校验拒绝** | `category = drug` ⇒ `drug_profile` 非空 |
| **`category ≠ drug` 却带 `drug_profile`** | **校验拒绝** | 反向校验,防数据漂移 |
| **`drug_profile` 只填了时间轴占位字段** | **合法**(P0 的正常状态) | D-21-6:字段在,P0 可为空 |
| **`inflicts_injury` 指向 9 中不存在的 `injury_id`** | **校验拒绝** | 外键完整性;9 拥有枚举(C4) |

### Edge Cases(2026-09-14 复核补 —— 写入期校验族)

> **校验时机是硬规定**:全部校验落在**构建期**(Editor 导入 / CI / EditMode 单测),
> **不是运行期意外**(与 9 的 R1.3 同档;原文只写「校验拒绝」未定时机,是 21a 的破口)。
> 每条校验**配一条负向夹具**(AC 断言的是「注入非法值必被抓」)。

| 情况 | 处理 | 理由 |
| ---- | ---- | ---- |
| **任一 `outputs[].qty ≤ 0`**(原稿写作 `Recipe.BaseQty ≤ 0`) | **校验拒绝** | `qty = 0` 会让该条产量恒为 `max(1,0)=1`(条目无意义);负值更错。**`BaseQty` 不再是配方级标量**(三轮 blocking #2)|
| **违反守恒律**:`Σ(weight×产出) > EFF_MAX × Σ(weight×实耗)` | **校验拒绝** | 规则八(单位为 weight 最小单位);`EFF_MAX > 1` 亦拒 |
| **`Σ(正的 cap) + max(0, ENV_MOD_MAX) > QTY_MULT_MAX − 1`** | **校验拒绝** | 否则高投入段被静默截断(投入零回报);**`ENV_MOD_MAX` 可正,须计入**(二轮补) |
| **`axis_offset_by_quality[]` 长度 ≠ `MAX_QUALITY`** | **校验拒绝** | F5:`quality − 1` 越界读会**污染 9 的病史事件流**(二轮 R2) |
| **`quality_character[]` 长度 ≠ `MAX_QUALITY`** | **校验拒绝** | D-21-16:标签与品级档必须一一对应(非空时) |
| **`axis_offset_by_quality[]` 含浮点字面量** | **导入期硬失败** | F5 / ADR-006:逐元素经 `FixParse`;与 `Axis_base` 同量纲 |
| **`ActualConsumed` 被写进配方数据文件或物件实例字段** | **装配期断言失败** | D-21-15:数据文件存**基数** `inputs[].qty`;`ActualConsumed` 是**运行期派生量**,只随该次 Craft **历史事件**走 —— 落进静态数据即意味着「实耗被冻成常数」,技能值一变就与重算不符 |
| **`QTY_MULT_MIN ≥ QTY_MULT_MAX`** | **校验拒绝** | 区间为空 |
| **`RETAIN_MIN > RETAIN_MAX`,`RETAIN_MAX > 1`,或 `RETAIN_MIN ≤ 0`** | **校验拒绝** | `RETAIN_MAX > 1` 会被 clamp 掩盖,**必须显式拒** |
| **`ENV_MOD_MIN > ENV_MOD_MAX`** | **校验拒绝** | 区间为空 |
| **`MAX_QUALITY < 2` 或非整数** | **校验拒绝** | 品级维度必须存在 |
| **`quality_distribution` 支撑 ⊄ `[1, MAX_QUALITY]`** | **校验拒绝** | D-21-7 双向耦合 |
| **`stack_max < 1` 或 `weight ≤ 0`** | **校验拒绝** | 规则六 |
| **配方项 `qty ≤ 0`** | **校验拒绝** | 零量 = 凭空造物 / 静默销毁 |
| **配方项 `item_key` 外键悬空**(inputs/outputs 引用不存在的条目) | **校验拒绝** | 此前只有 `inflicts_injury` 有外键 AC |
| **`duration_ticks ≤ 0`** | **校验拒绝** | 规则五 |
| **`skill_gate ∉ [0, SKILL_CAP]`** | **校验拒绝** | `> 60` 的配方**永不可制**,是数据错误 |
| **`min_quality < 1` 或 `> MAX_QUALITY`** | **校验拒绝** | F5 |
| **同 `(base_id, processing_state)` 重复** | **校验拒绝** | 复合主键唯一性 |
| **`category` / `processing_state` 取枚举外的字面量** | **校验拒绝** | 枚举闭合 |
| **P0 期出现 P1a 值**(`honey_fried` / `dry_fried` / 非空 `tcm_profile`) | **校验拒绝** | 对齐 8 的 AC-8-33 同型纪律 |
| **配方 state 对不在物品声明的 `legal_transitions` 内**(如 `raw → extracted`) | **校验拒绝** | §States and Transitions 的核心规则(「枚举只给词汇不给通路」)—— 原稿**零 AC** 守它 |
| **`category = weapon` 却无 `inflicts_injury`;或 `category ≠ weapon` 却带 `inflicts_injury`** | **校验拒绝** | 规则四 + 规则七 的反向校验 |
| **`processing_state` 以 int 编码持久化**(存档 **或 .asset 作者态产物**) | **装配期断言失败** | D-21-13 + 二轮补强:enum 在 .asset/JsonUtility 默认 int,**须上移到数据产物层扫描**(不只查代码字段) |
| **`ItemDef` / `Recipe` 被做成 ScriptableObject / prefab** | **装配期断言失败** | 二轮补强:唯一合法作者态 = `assets/data/*.json`(见 §Schema A 注);SO 会一次踩中 enum int 与 UnityEngine 引用 |
| **`ItemInstance` 含任何 `UnityEngine` 类型字段**(含 `[SerializeReference]`/接口/抽象字段下的子类) | **静态断言失败** | §Schema E;断言须**递归走类型图**(二轮补) |
| **`Fix` 字段经 MonoBehaviour / SO / `JsonUtility` / prefab 序列化** | **静态断言失败** | D-21-18:Unity 跳过 `readonly struct` 的私有字段 ⇒ 静默归零药效;须走自定义编码器 |

### Edge Cases(2026-09-14 三轮复核补)

| 情况 | 处理 | 理由 |
| ---- | ---- | ---- |
| **`EFF_MIN ≤ 0`** | **校验拒绝** | `EFF` 是 F1 的**除数**(`ActualConsumed = Ceil(base / EFF)`)—— `EFF_MIN = 0` ⇒ **运行期除零**;`EFF_MIN < 0` ⇒ **实耗为负**(凭空造料)。此前只约束了 `EFF_MAX ≤ 1`,**下端无人守**(三轮 blocking #6) |
| **`EFF_MIN > EFF_MAX`** | **校验拒绝** | 区间为空,`clamp` 静默取上界 |
| **`drug_potency` / 时间轴四字段 / `axis_offset_by_quality[]` 写浮点字面量** | **导入期硬失败** | D-21-9/22:逐元素经 `FixParse` —— **原稿只有 `axis_offset_by_quality[]` 有这条守门**,`drug_potency` 与时间轴四字段漏(三轮 blocking #2 的伴生缺口) |
| **`Axis_base + min(axis_offset_by_quality) ≤ 0`** | **校验拒绝** | F5:9 的衰减用 `half_life` 作**除数** —— 偏移把它推到 `≤ 0` ⇒ 除零 / 反向衰减 |
| **F5 非零档偏移 `|offset|` < 可感知地板** | **校验拒绝** | D-21-24:小于 9 的噪声带 ⇒ 偏移**统计上不可区分** ⇒ 玩家感觉不到,而 U-1/U-2 禁数字 ⇒ **无出口**。地板数值 = 调参旋钮 |
| **`drug_quality_character[]` 长度 ≠ `MAX_QUALITY`** | **校验拒绝** | D-21-24:成药侧标签与品级档须一一对应(非空时);与 `quality_character[]` 同型 |
| **`quality_axis` 在 P0 期取 `onset`/`peak`/`elimination`** | **校验拒绝** | D-21-23:P0 只有 `half_life` 在 9 的处置载荷有落点,其余三轴**产出即被丢弃** |
| **容器子实例闭包不成立**(同一 `instance_id` 出现于多个容器 / 出现于自身 `/` 容器图有环 / 子实例在 `children` 中而其归属容器未登记) | **装配期断言失败** | §Schema E:容器是**子实例 id 列表** —— 无闭包断言则「一物两在」或「物随容器删而丢」**静默发生**(三轮 blocking #2 伴生) |
| **`stackable` 被写入数据文件** | **校验拒绝** | 它是 `stack_max > 1` 的**派生量,不存储**(§Schema A)—— 独立存储即可与 `stack_max` 漂移 |

## Dependencies

> **21 是全案唯一的零依赖系统。** 下表除一条外,全部是**出向**;
> 唯一入向是 `9 疾病与伤情模拟`(伤情枚举),那是**设计上的例外**(C4)。

| 系统 | 方向 | 性质 | 接口 |
| ---- | ---- | ---- | ---- |
| **17 采集** | 17 ← 21 | 数据 | `gather_profile`;17 产出的 `ItemInstance` 使用 21 的 `item_key` |
| **18 炮制** | 18 ← 21 | 数据 **+ 求解器** | 读 `Recipe`(炮制子集);**调用 F1/F2**;自身**不实现任何公式** |
| **19 制作与器械** *(P1a)* | 19 ← 21 | 数据 **+ 求解器** | 同上;`n > 1` 特例 |
| **11 处方用药** | 11 ← 21 | 数据 | `drug_profile` |
| **12 药物槽** *(P1a)* | 12 ← 21 | 数据 | `drug_profile` 的**时间轴** |
| **16 中药选项** *(P1a)* | 16 ← 21 | 数据 | `tcm_profile`(P0 **为空**) |
| **20 库存与物品** | 20 ← 21 | 数据 | `stackable / stack_max / weight / category`;品级是实例属性 |
| **9 疾病与伤情模拟** | 双向 | **数据(入向,唯一入向之一)** + **出向契约** | **入向**:`inflicts_injury → injury_id`,**枚举由 9 拥有**(规则七 / C4)。**出向**:`drug_potency → 9 处置事件的 `Offset``、`half_life → 9 的 `tau_half`**(D-21-22/23;9 侧字段名对齐归 **D-21-25**)|
| **7a 持久化服务** | 7a ← 21 的**形状** | 契约 | 必须能序列化 `ItemInstance`(含 `quality`);**存快照,不存引用** |
| **30 技能与熟练度** | 21 → 30 | **常量引用** | `SKILL_CAP`(=60)—— 21 **引用**不拥有,见 `skill-system.md` |
| **5 时间天气** | 5 → 21 | 数据(**运行时入参**) | `EnvMod` 的来源之一(火候 / 背阴)—— **2026-09-14 补登**(原表漏) |
| **24 医馆即机器** | 24 → 21 | 数据(**运行时入参**) | `EquipMod` 与 `EnvMod` 的来源 —— **2026-09-14 补登**(原表漏)。**2026-09-17 补 `O-24-1` / `O-24-4`**:① 家具目录 `FURN_TABLE` 须扩展 `type` / `tier` / `e_env` 字段(与 `cost(m)` 同源);② **`ENV_MOD_MIN < 0` 为承重前提**(否则「满污染已匹配房间」与 `ROOM_NONE` 中性值 0 不可区分)—— 只登记口径,数值归用户 |
| **42 拟物 UI 框架** | 42 ← 21 的**名称 + 品级语义** | 数据 | 物品显示名由 21 拥有;品级**非数字化呈现契约**由 21 交付、42 实现 |
| **6 世界与生态区** | 6 → 21 | 数据(**外键引用**) | `gather_profile.ecosystem` → 生态区 id;**2026-09-14 二轮补登**(原表漏) |
| **52 随机事件导演** | 52 ← 21 的**形状** | 数据 | `weight → InstanceWeight → CarryLoad → StrengthTier`(见 `random-events.md` **F1**);**2026-09-14 二轮补登**(原表漏) |
| **ADR-006 整数边界契约** | 21 ↔ ADR-006 | **契约(数据形状)** | F1/F2/F4 出参 int;`drug_profile` 的 Q16.16 字面量;`float in SimEvent` 禁令;**`weight` 不属 Fix 集(D-21-17)**;**Fix 禁经 Unity 序列化(D-21-18)** |

> ⚠️ **唯一入向的代价**:21 是 Foundation 零依赖(工作流意义上),但它的物品可以「造成伤情」,
> 而伤情枚举住在 9(Core 层);它还要读 30 的 `SKILL_CAP`。
> 这**不构成分层环** —— 21 只存一个**字符串外键**与一个**常量**,不认识任何伤情 / 技能语义。
> 索引 §9 C4 已确认 9 是「通用伤情注册表」。
>
> **措辞更正(2026-09-14)**:原文写「唯一入向是 9」**不准确** ——
> `entities.yaml` 登记的是**两条**入向(`SKILL_CAP` 与伤情枚举),本表亦自述 30。
> 「零依赖」指的是**工作流**(21 可第一个开始写),不是**零引用**。
>
> **二轮更正(2026-09-14)**:入向实为**多条** —— 除 `9`(伤情枚举,设计例外 C4)与 `30`(`SKILL_CAP`)
> 外,还有 **运行时入参**类:`5 时间天气` / `24 医馆`(喂 `EnvMod`/`EquipMod`)、`6 世界与生态区`(`ecosystem` 外键)。
> 这些都**不是工作流依赖**(21 仍可第一个写),但**必须在接口面登记** ——
> 原表漏登记 5 / 24 / 6 / 52 四条边,是「依赖必须双向」纪律的破口(二轮 blocking)。

### §Debt Register(唯一台账)

> **跨系统债务在此登记。** 21 作为 Foundation 层,下游 9 条边全在等它 —— 未还清的账不进 Implement。

| # | 债务 | 归属 | 状态 |
| --- | --- | --- | --- |
| **D-21-6** | `drug_profile` 时间轴字段**类型**须为 Q16.16 int(原文只写「字段必须在」) | 21a | ✅ **已办**(§Schema B) |
| **D-21-7** | `MAX_QUALITY`(21 拥有)↔ `quality_distribution`(17 拥有)双向耦合 | 21a / 17 | ✅ **已登记**(F3 注)+ 构建期校验 |
| **D-21-9** | **ADR-006 整数边界契约**须落盘(21a 侧已声明,ADR 侧待写) | **ADR-006** | ✅ **已办**(2026-09-14 `ADR-006` Accepted —— §Decision 一/二/三) |
| **D-21-10** | 守恒律 `EFF_MAX ≤ 1` 须在 18 / 19 侧实现并校验(**已改 weight 单位**,见规则八) | 18 / 19 | ⏳ 待撰写时回收(规则八已定;常量已登记 `entities.yaml`) |
| **D-21-11** | 品级 → **时间轴作用点**档位须在 **11 处方 / 9** 侧接通(F5 已定形,轴已改 D-21-14) | 11 / 9 | ⏳ 待撰写时回收(F5 公式须改登记 `entities.yaml`) |
| **D-21-12** | **21b 内容规范**须落定三件事:药典版本 · 药品名豁免 · 保留词表 | **21b** | ⏳ **待落盘**(21b 撰写前) |
| **D-21b-1** | 命名规范的可执行化(原「判定法」不可判定) | 21b | ⏳ 待落盘 —— 见「命名规范」指针 |
| **D-21b-2** | `weight_unit` 的**存储/展示单位**考据 + **数值冻结令**解除 | 21b | ⏳ 待考据(支柱五) |
| **D-21b-3** | **21b 的考据量未估算** —— 「唯一零依赖」的赌注全压 21b | 21b / producer | ⏳ **并入 §范围债**(用户裁定⑤;已同步 `systems-index.md` §4) |
| **D-21-13** | `processing_state` 稳定字符串编码须在 7a / 20 侧实现(**须扫数据产物层**) | 7a / 20 | ⏳ 待撰写时回收(规则二已定;AC 已上移) |
| **D-21-19** | **ADR-006 修正案须落盘**:① `weight` 移出 Fix 解析集(D-21-17)② `Fix` 禁经 Unity 序列化(D-21-18)③ 守恒律改 weight 单位 | **ADR-006** | ✅ **已就地落盘(2026-09-14 同轮)** —— 三处修正 + 新增 §Decision 五;`technical-preferences.md` 已同步 |
| **D-21-20** | **`Fix` 序列化编码器须实现** —— 7a 侧把 `Fix._raw` 的 `long` 显式写出/读入 | **7a** | ⏳ 待撰写时回收(D-21-18);**须一条 Unity 序列化探针实测钉死** |
| **D-21-25** | **9 侧处置事件字段名须对齐 `drug_potency`** —— `disease-simulation.md` 的事件元组仍写 `Offset`(9 的点名来源);21a 已改名(D-21-22) | **9 / 11** | ⏳ **待回收** —— 两份 GDD 落笔时统一为 `drug_potency`;`tau_half` 同理对齐 `half_life` |
| **D-21-26** | **`instance_id` 权威缺契约** —— ADR-005 的 `IIdAuthority` **只有 `PatientId Next()`**,无物品 id 来源;主机迁移后计数器若复位即**静默重号**(两件物品共用一个 id,不崩溃、不回放失配,只表现为「物品悄悄合并/丢失」) | **ADR-005 / 7a / 20** | ⏳ **待裁** —— 须与 D-9-E 同机制(计数器 + 高水位可重构),扩 `IIdAuthority` 加 `ItemInstanceId Next()` |
| **D-21-27** | **17 采集的 `instance_id` 铸造权** —— 规则九的「主机唯一」清单**漏了 17**:采集在客户端本地铸造 id 会与主机撞号 | **17 / 45** | ⏳ **待回收** —— 17 落笔时补入主机唯一清单,或走 D-21-26 的 `IIdAuthority` 单一来源 |
| **D-21-28** | **Craft 事件的总序键缺失** —— 同一 tick 内多次结算需要稳定的定序键以保回放一致;9 的事件流有 `Seq`,**21a 的 Craft 事件尚无** | **ADR-005 / 18 / 19 / 45** | ✅ **已裁(2026-09-19,18 首轮评审,用户裁定 [甲])** —— **沿用三流全序键 `(Tick, StreamPriority, Patient, Seq)`**(ADR-008 §一);`Craft` 落世界流,`Patient = PatientId.None = -1` 哨兵位(ADR-007 ④),`Seq` 主机 `Append` 时发号。原建议键的 `ActorId` 位**被吸收**(actor 已是 Craft 载荷字段,18 R-18-A)。回填:18 注④ · `entities.yaml:91` · `adr-009:243/:415`(记源同步订正为「发起方 18 / 求解 21a」) |
| **D-21-29** | **「一株 → 几剂」的换算字段缺失** —— 11 的 `dose` 是**药效刻度**,消耗的是**库存份数**,二者**必须解耦**;而 `drug_profile` 无承载该换算的字段 ⇒ 「够不够剂」这句话在 P0 **无量纲**,11 的「库存不足整体拒绝」判据不可实现 | **21a / 17 / 18**(产出侧;**11 已裁定不做**,OQ-11-10) | ⏳ **待认领** —— 2026-09-18 由 11 首轮评审登记(系本案第 10 次「引用却无登记」);11 侧口径见 `prescription-and-medication.md` 规则十 |
| **D-21-30** | **`Recipe.owner` 字段缺失** —— 三系统(18/19/23)对「配方子集」的判据若靠 `processing_state` 变化约定,在 `item_key` 复合主键下**结构性不互斥**(18 R-18-B 举证) | **21a**(字段落点)/ 18(裁定发起方) | ✅ **已裁(2026-09-19 [甲])并在本轮落盘** —— 规则五已加字段;校验 = **AC-21a-66** |
| **D-21-31** | **`EnvMod` 的求和+钳制全文无执行落点** —— 5:395 与 AC-5-19 互相指认、原 `:484` 变量表是入参断言非操作 ⇒ `:533` 构建期不等式保护了一个**不存在的包络** | **21a** | ✅ **已裁(2026-09-19,18 R-18-C)并在本轮落盘** —— F1 正文新增 `EnvMod_total = clamp(climate + clinic, MIN, MAX)`;5 侧 AC-5-19 收窄为只断自家不钳(涟漪) |
| **D-21-32** | **构建期守恒聚合式与运行期逐条 `Round` 不同形** —— 可被击穿(实测反例 `w_in=10/w_out=6/QM=1.5/qty=1`:构建 `9≤10` ✓、运行 `12>10` ✗;缝在 `Round` 不在 `max(1,·)`) | **21a** | 🟡 **修法已落盘(2026-09-19,承 18 O-18-R3)** —— 逐条同形极值式已写进规则八注,**AC-21a-65** 承接;⏳ **烘焙管线实现未写** ⇒ 18 的 AC-18-19 在此之前不得记绿 |
| **D-21-33** | **`SkillLevel` 具名 newtype 缺失** —— 18 的 AC-18-03 要机器可验「传 `Level` 不传 `cap×Level/SKILL_CAP` 的结果」,但两者都是 `int`,**类型系统不可分辨**;落地前 18 侧只能以运行时恒等断言(spy 夹具)现测 | **21a**(类型定义落点)/ 18(判据发起方) | ⏳ **待认领** —— 2026-09-19 由 18 首轮评审登记(义务 `O-18-R7`);落地后 AC-18-03 升级为类型系统断言,不阻塞 18 验收(恒等断言为现行判据) |
| **D-21-34** | **F5 的下界断言与 `drug_potency` 的声明域双双缺失** —— ① F5 现断言 **`Axis_base + min(axis_offset_by_quality) > 0`**(`:732` / `:810` / `AC-21a-38b`),而下游 11 的 F-11.2 按 **`≥ MIN_USABLE_HALF_LIFE`** 写。**两者不是同一把尺**:合法输入 `Axis_effective = 2`(如 Q16.16 的极小正数)在现断言下**通过**,却在 11 侧退化为「退得比一 tick 还快 ⇒ 无药效」—— **静默失败,且 11 侧 AC 全过**(11 不重复 clamp,刻意掩盖上游失败)。② `drug_profile.drug_potency` 在 §Schema B(`:321`)只有类型 `Q16.16 int`、**无取值范围** ⇒ 11 的 F-11.1「中间积是否必须 128 位」在文档层**不可判定**(Q16.16 合法域 ≤ 2^47 时 `× dose` 不可能溢出;不声明域则可能) | **21a**(F5 断言落点 + §Schema 域声明)/ 11(判据发起方) | ⏳ **待认领(须重开)** —— 2026-09-19 由 11 二轮评审登记(BL-1 + BL-7);11 侧口径见 `prescription-and-medication.md` §Cross-References **`O-11→21a`** |
| **D-9-D** | `SimEvent` 须补 `Seq` + 载荷(ADR-005 自相矛盾) | **ADR-006** | ✅ **已办**(Amendment A)+ ADR-005 已加前向指针 |
| **D-9-E** | `PatientId` 跨权威稳定性 | **ADR-006** | ✅ **已裁**(2026-09-14 机制 A「计数器 + 高水位可重构」,Amendment B);残留实现项归 7a / 45 |
| **D-9-B** | 神经衰弱史实出处(**非时代错误**,缺口是可引用的一手来源) | 考据 | ⏳ 待考据(支柱五,**不得凭空落盘**) |

## Tuning Knobs

> **🔒 数值全冻结(2026-09-14)**:本轮复核**未动任何数值** —— 依硬约束「数值用户自己调」,
> 我只交付公式 + 变量表 + 旋钮。**下表「默认」列一律留空。**
>
> **归属与存放(2026-09-14 复核补 —— 此前缺此表)**:数值分两处,**不得第三处**:
> - **物品/配方数据文件** —— 逐条量(`stack_max` / `weight` / `BaseQty` / `duration_ticks` / `skill_gate` / `min_quality`)
> - **全局常量表** —— 跨条目量(`QTY_MULT_*` / `*_MOD_CAP` / `ENV_MOD_*` / `RETAIN_*` / `EFF_*` / `MAX_QUALITY`)
>
> `assets/data/` 下文件名须合 `.claude/rules/data-files.md` 的 `[system]_[name].json`
> (原文写 `assets/data/items.json` **不合规**,应为如 `item_database_items.json` / `item_database_recipes.json`)。

| 旋钮 | 位置 | 默认 | 安全范围 | 作用 / 极端后果 |
| ---- | ---- | ---- | ---- | ---- |
| `QTY_MULT_MIN` | F1 | **0**(2026-09-25 数值批) | **≥ 0**(产出下限由 `max(1, ·)` 兜底,见下) | 最差产出的下限。**改成 0 不再违反 AC-21a-1** —— 非零由 `max(1, ·)` 结构性保证。**拍 0 的语义**:彻底失败态 = 恒出 1 件地板(直白);备选 `1/4`(与 golden 夹具同值)不取 |
| `QTY_MULT_MAX` | F1 | **2**(2026-09-25 数值轮甲案) | > `QTY_MULT_MIN`,**≥ 1 + Σ(正的 cap) + max(0, ENV_MOD_MAX)**,**且满足守恒上界 `QTY_MULT_MAX × Σ(w×产出基数) ≤ EFF_MAX × Σ(w×输入基数)`** | 最好产出的上限。↓ 到 `Σcap` 以下 = 高投入被静默截断;**↑ 过守恒上界 = 运行期凭空造物**(三轮 blocking #1)—— **两侧同时锁死,不能只满足一边**。**取 2 的两把锁**:AC-21a-9 要求 `QTY_MULT_MAX ≥ 1 + Σ正cap + max(0,ENV_MAX) = 1 + 45/100 + 50/100 = 1.95`(实算门侧 0.95 ≤ 2−1 = 1 ✓);AC-21a-8 守恒上界由 `extract_salicylic` 输入 qty 1→2 同批调整至 Σw入 = 2 ≥ 2 × Σw出 = 2 ✓ |
| `SKILL_MOD_CAP` | F1 | **1/10**(2026-09-25) | ≥ 0 | 炮制技能的最大加成。↑ 则技能主导产出。**回刷**:原夹具 0 = 死值(技能修正零贡献) |
| `QUAL_MOD_CAP` | F1 | **1/10**(2026-09-25) | ≥ 0 | 品级的最大加成。↑ 则「采高品级」更值钱。**回刷**:原夹具 0 = 死值 |
| `EQUIP_MOD_CAP` | F1 | **1/4**(2026-09-25) | ≥ 0 | 器具 / 医馆的最大加成。↑ 则基建回报更明显。⚠️ **CAP < 1 的结构语义(Opus 案卷发现,随本轮拍板一并记账)**:F-24-2 `clamp(整数 score×65536, 0, CAP)` ⇒ **任何 tier>0 布局直接打满 = 「有无像样器具」二值开关**,P0 接受该语义(与 OQ-CP-5 右秤「只上扬/平」一致);要真渐变须改 F-24-2 归一化 = 机制改动,超出本数值轮 |
| `ENV_MOD_MIN` / `ENV_MOD_MAX` | F1 | **−1/2 / +1/2**(2026-09-25) | 可负;`MAX` 若为正须计入 `QTY_MULT_MAX` 约束 | 环境修正带。↓ `MIN` 则恶劣环境惩罚更重(由 `QTY_MULT_MIN` 兜底)。**`MIN < 0` 承重前提满足**;`MAX > 0` 使 24 全部正向旋钮与左秤甲案域([−1/2,+1/2])可用(原夹具 0 = 正向全死) |
| `RETAIN_MIN` | F2 | **1/2**(2026-09-25 数值批) | (0, 1] | 技能 0 时的品级保留率。**必须 > 0**。**拍 1/2**:= 种子 = golden FullCaps 夹具;q5→`Round(5×0.5)=3` 有感降档、**q1→1 不掉档**(half-away 兜住);备选 `3/4` 不取(惩罚感弱) |
| `RETAIN_MAX` | F2 | **1**(2026-09-25 数值批) | [`RETAIN_MIN`, 1] | 技能 60 时的保留率。**= 1.0 表示满技能完全保值**。**> 1 构建期拒**。拍 1 = GDD 直读值(备选 `9/10` 与直读冲突,不取) |
| `EFF_MIN` | F1 / F2 | **1/2**(2026-09-25 数值批) | (0, `EFF_MAX`] | **技能 0 时的转化效率** = 实耗 `Ceil(base/EFF)` 的最大值。↓ 则低技能烧料极凶。拍 `1/2` = 种子 = golden = F1 算例量级(技能 0 烧料翻倍,代价感足) |
| `EFF_MAX` | F1 / F2 | **1**(2026-09-25 数值批) | ≤ 1 | **转化效率上限**。**= 1 = 满技能无损耗**;> 1 构建期拒(守恒律)。**刚性值**:守恒律(D-21-21)锁 ≤1,`= 1` 是零损耗唯一直读 |
| `MAX_QUALITY` | F2 / F5 / D-21-16 / D-21-24 | *待定*(**推迟有因**,2026-09-25 数值批注:现种子 5 与 golden-v1 / AC-28 全域耦合 ⇒ 改 = **golden-v2 全平台重签**,单独立项) | ≥ 2(整数) | 品级档数。↑ 则品级维度更细,但堆叠基数、`axis_offset_by_quality[]` 与 `quality_character[]` **及 `drug_quality_character[]`** 长度同步放大 |
| `quality_axis` | F5 | *待定* | **P0 固定 `half_life`**(D-21-23);其余三轴标 P1a | 品级偏移落在哪根时间轴上(逐条药物)。**P0 收窄** —— 其余三轴在 9 的处置载荷无落点 |
| `axis_offset_by_quality[]` | F5 | *待定* | 长度 = `MAX_QUALITY`;**Q16.16**;**非零档 `|值|` ≥ 可感知地板** | 品级 → 时间轴档位偏移。**全 0 ⇒ AC-21a-37 拒绝**(不是「可接受的现状」—— 那等于锚点二在成药侧落空)|
| `F5 偏移可感知地板` | F5 | *待定* | **> 9 的病史噪声带** | 低于噪声 ⇒ 偏移不可区分 ⇒ 玩家永远感觉不到(D-21-24)。**须与 9 的噪声带宽一起定** |
| `quality_character[]` | D-21-16 | **✅ 已落值 2026-09-25**(P0 词集见 `assets/data/item_database_items.json`,5 档经用户审定 + OQ-17-9 筛;R13=甲 必填) | 长度 = `MAX_QUALITY`;**string**;逐档非空 | **原料侧**每档定性修饰(⚠️ 原示例「陈放」= **OQ-17-9 禁词已废**,见 §B2 废例注);42 以外观/药签呈现 |
| `drug_quality_character[]` | D-21-24 | **✅ 已落值 2026-09-25**(成药词集同批,见数据文件;R13=甲 必填) | 长度 = `MAX_QUALITY`;**string**;逐档非空 | **成药侧**每档定性修饰(示例「炮制得法」为工艺词 ✓,实词以数据文件为准);42 以药签措辞呈现(U-6)。与 `quality_character[]` **互为补充、不可互替** |
| `ROUND_MODE` | F1 / F2 | `ROUND_HALF_AWAY_FROM_ZERO` | 固定 | **不建议改** —— 改它会让所有既有夹具失效 |
| `ItemDef.stack_max` | F4 | *逐条* | ≥ 1 | 堆叠上限 |
| `ItemDef.weight` | F4 | *逐条* | ≥ 1(**整数,非 `Fix`**) | 单件重量(最小单位个数);**数值冻结至单位考据落定**;**守恒律按它求值** |
| `weight_unit` | F4 | **待考据** | — | 重量**展示**单位名。**21a 不填**(支柱五 / D-21b-2) |
| `ActualConsumed` 基数表 | F1 | *逐条* | = `inputs[].qty` | 配方**输入基数**(非实耗);结算 × F1 得真实消耗 |
| `drug_potency` | D-21-22 | *逐条* | **Q16.16 int**(9 的 `Offset` 来源) | 药效**静态基础幅值**(**不受品级调制**)。↑ 则所有品级的这一味药都更强 —— **它的品级差别不在此轴**,在 `axis_offset_by_quality[]` |
| `drug_profile.<时间轴四字段>` | D-21-6 | *逐条* | **Q16.16 int** | `onset` / `peak` / `half_life` / `elimination`;P0 只 `half_life` 有落点 |

> ⚠️ **联动**:`QTY_MULT_MAX` ↑ 会**降低品级的相对价值** ——
> 如果产出量能靠技能拉满,玩家就不再在乎采到的是几级原料,`RETAIN_*` 三条旋钮全部失效。
> **这两组必须一起调**,不能只动一组。
>
> ⚠️ **联动(2026-09-14 补)**:`EFF_*` 与 `QTY_MULT_*` **同属技能回报** ——
> `EFF` 管「省料」(投入端),`QTY_MULT` 管「多产」(产出端)。两条一起拉满 = 技能线过强;
> 一条也不动 = 「精进炮制」无体感。**它们是同一根回报曲线的两端。**

> ⚠️ **`QTY_MULT_MIN` 的安全范围已放宽(二轮)**:原文写「必须 > 0,否则违反 AC-21a-1」——
> **该断言的理据是错的**(`Round(1 × 0.3) = 0`)。现产出非零由 `max(1, ·)` **结构性**保证,
> `QTY_MULT_MIN` 就是纯粹的最差产出旋钮,取值可在 `[0, QTY_MULT_MAX)` 内自由设。

> ⚠️ **`EFF_MIN` 的下界是硬的(三轮补)**:`(0, EFF_MAX]` —— **不含 0**。
> `EFF` 是 F1 的除数,`EFF_MIN = 0` ⇒ 运行期除零,`< 0` ⇒ 实耗为负(凭空造料)。
> 此前只写了 `EFF_MAX ≤ 1` 的上界,**下端无人守**(三轮 blocking #6);构建期须显式拒 `EFF_MIN ≤ 0` 与 `EFF_MIN > EFF_MAX`。

> ⚠️ **`QTY_MULT_MAX` 的上界被守恒律锁死(三轮 blocking #1)**:它不能任意抬高 ——
> 构建期须满足 `QTY_MULT_MAX × Σ(weight × 产出基数) ≤ EFF_MAX × Σ(weight × 输入基数)`(规则八)。
> **原稿只在这条旋钮行写了 `≥ 1 + Σcap`,漏了守恒律这一侧** —— 于是「抬 `QTY_MULT_MAX` 让高投入有回报」
> 与「守恒律禁止凭空造物」在**同一条旋钮上打架**,而只有前一条被写下来。


## Visual/Audio Requirements

**本系统不渲染任何东西。** 它是纯数据层 —— 没有事件、没有特效、没有声音。

| 事件 | 视觉反馈 | 音频反馈 | 优先级 |
| ---- | ---- | ---- | ---- |
| *(无)* | *(无)* | *(无)* | — |

> **所有权**:全部视觉与音频归 **20 库存 / 42 拟物 UI 框架 / 44 音频系统** 拥有。

> **⚠️ 2026-09-14 复核删除项** —— 原文在此处**指定了重量展示的具体装置**
> (「一把黄铜天平、一组砝码,读数刻在砝码上,不刻在 HUD 上」)。
> **已删除。** 两条理由:
> 1. **越权** —— 21a 是数据层,装置选型属 **42 拟物 UI** 的域;数据层不能替表现层拍板。
> 2. **数值冻结令下无意义** —— 重量单位尚未考据落定(§Open Questions),
>    在单位未定前指定「天平 + 砝码」是**为一个不存在的数值设计容器**。
>
> **21a 只保留一条上游约束**(交给 42,不替它选解):
> **「重量必须以拟物方式呈现,玩家可亲手读取,不得以数字 HUD 呈现」**(支撑原则二)。

## UI Requirements

**本系统无 UI。** 它不持有任何玩家可见的界面元素。

| 信息 | 显示位置 | 更新频率 | 条件 |
| ---- | ---- | ---- | ---- |
| *(无 —— 本系统不显示任何东西)* | — | — | — |

### 欠 42 的呈现契约(2026-09-14 新增)

**所有权**:物品的**显示名字符串**由 21 拥有(21b 撰写,受 §命名规范 约束),
**如何呈现**(药签、册页、出诊箱标签)归 **42 拟物 UI 框架**。

以下四条是 **21a 对 42 的硬约束** —— 42 若违反,即破坏本系统的设计意图:

| # | 契约 | 理由 |
| --- | --- | --- |
| **U-1** | **品级不得以数字呈现**(不出现「3 级」「Q=3」「★★★」式计分) | 品级是**手感概念**,数字化后玩家退化为**比对数字**,破原则一(判断为先) |
| **U-2** | **品级不得以线性刻度条呈现** | 刻度条 = 变相数字;且 `MAX_QUALITY` 未定,线性刻度的分辨率无从设计 |
| **U-3** | **重量不得以数字 HUD 呈现** | 见上,支撑原则二(拟物) |
| **U-4** | **同一 `base_id` 的不同 `processing_state` 必须在视觉上可区分** | 否则 `item_key` 的**复合性对玩家不可见** —— 生药与炮制品的区别是整个 18 炮制的前置认知 |
| **U-5** | **品级在原料上须以 `quality_character[]` 的定性修饰呈现**(外观 / 药签措辞) | D-21-16:这是锚点二「品级的质地」的**呈现载体**。**不得**退化为 U-1/U-2 所禁的数字或刻度 |
| **U-6** | **品级在成药上须以 `drug_quality_character[]` 的定性修饰呈现**(药签措辞 / 外观) | D-21-24(三轮 blocking #5):`category = material` 走 U-5,`category = drug` **摸不到 `gather_profile`** —— 成药侧此前**无任何合法感知通道**(F5 是数值级、U-1/U-2 禁数字)⇒ 锚点二在成药侧落空 |

> **U-1/U-2 是本轮新增的正向设计约束** —— 原文档只说「归 42 拥有」,
> 却没说**42 不能怎么做**。所有者声明不等于设计约束。
> **U-5 是二轮新增(D-21-16)** —— 品级的质地需要一个**看得见的种类差别**;
> 若只靠 F5 的时间轴偏移(数值级),在 U-1/U-2 禁数字后玩家**无从感知**。
> **U-6 是三轮新增(D-21-24,伴随 `drug_quality_character[]` 入 Schema B)** ——
> **原料侧与成药侧各有一条称呼通道**,二者**互为补充、不可互相替代**:
> 原料的「新采带露 / 陈放 / 虫蛀」是**来源的质地**,成药的「炮制得法 / 火候稍欠」是**工艺的质地**。
> ⚠️ **F5 的偏移量还须 ≥ 可感知地板**(D-21-24 第二半)—— 否则 U-6 的措辞**名不副实**
> (标签说「得法」而时间轴上量不出差别 = 撒谎)。两条须同时成立。

## Acceptance Criteria

> **2026-09-14 复核整体重写。** 原文的问题不是「不够多」,而是**不可执行**:
> ①**无编号** ⇒ 无法在 review log / 测试报告 / bug 单里引用;
> ②**无分级** ⇒ 视觉项与数学项混在一起,CI 不知道该拦哪个;
> ③**无负向夹具要求** ⇒ 「校验拒绝」类 AC 只测了合法值,非法值无人证明会被抓;
> ④**确定性 AC 是同义反复**(见组四)。

### 图例

| 标记 | 类型 | 证据要求 | 门级 |
| ---- | ---- | ---- | ---- |
| **[L]** | Logic(纯函数 / 公式 / 校验) | **EditMode 自动化单测必须通过** | **BLOCKING** |
| **[I]** | Integration(跨系统) | 集成测试或书面 playtest | **BLOCKING** |
| **[V]** | Visual / Feel | 截图 + 主美签字 | ADVISORY |
| **[U]** | UI / 走查 | 手工走查文档或交互测试 | ADVISORY |
| **[D]** | Config / Data | 冒烟检查 | ADVISORY |

> **负向夹具铁律**:凡「校验拒绝」类 AC,**必须同时提交一个非法夹具**落在
> `tests/unit/item_database/fixtures/invalid_[rule].json`,断言**构建期硬失败**。
> **只验合法值不算通过。** —— 这是原文全组「校验拒绝」AC 的共同破口。

### 组一 · 数学保证

| ID | 级 | 断言 |
| --- | --- | --- |
| **AC-21a-1** | **[L]** | **GIVEN** 任意合法配方(**逐条 `outputs_i`**),**WHEN** 施加任意合法 `ΣM`(含所有修正取最小、`ENV_MOD = ENV_MOD_MIN` 的极端组合),**THEN** `OutputQty_i = max(1, Round(outputs_i.qty × QtyMultiplier)) ≥ 1`(**每条**产出都 ≥ 1)—— 零产出**结构性不可能**(下界由 `max(1, ·)` 兜住,**不依赖 `QTY_MULT_MIN` 取值**;原稿断言依赖 `QTY_MULT_MIN > 0`,而 `Round(1 × 0.3) = 0`,该断言为**假**) |
| **AC-21a-2** | **[L]** | **GIVEN** 任意 `InputQuality ∈ [1, MAX_QUALITY]`、任意 `CraftSkill ∈ [0, SKILL_CAP]`,**WHEN** 调用 F2,**THEN** `OutputQuality ≤ InputQuality` —— 品级永不被抬高(刷品级路径不存在的验证) |
| **AC-21a-3** | **[L]** | **GIVEN** 常量表(**非**某次求解),**WHEN** 校验, **THEN** `Σ(正的 cap) + max(0, ENV_MOD_MAX) ≤ QTY_MULT_MAX − 1` —— 即**常量表自洽性检查**,不是求解器行为(原稿把它写成「WHEN 调用 F1」是**范畴错误**:该不等式与任何输入无关,**对求解器恒真**,是变相的同义反复)。原稿此处还写「`ΣM = … − ...`」,**省略号不是合法表达式,该 AC 无法执行**;已补全为上界式 |
| **AC-21a-4** | **[L]** | **GIVEN** F1 的 `QtyMultiplier` 求解,**WHEN** 打乱各修正项的**施加顺序**,**THEN** 结果**逐位相同** —— 顺序无关性的定点域前提(原稿断言顺序无关却无此前提) |

### 组二 · 求解器唯一性(C5 落地)

| ID | 级 | 断言 |
| --- | --- | --- |
| **AC-21a-5** | **[I]** | **GIVEN** 18 炮制与 19 制作各自发起结算,**WHEN** 传入相同参数集,**THEN** 两者走**同一个函数** —— 代码审查 + 单元测试双重验证 |
| **AC-21a-6** | **[I]** | **GIVEN** 21 的求解器源码,**WHEN** grep 全部结算公式,**THEN** 17 / 18 / 19 **不含任何重复实现** |

### 组三 · 写入期校验(21 条,逐条一个负向夹具)

> 全部落在**构建期**(Editor 导入 / CI / EditMode)。每条对应 §Edge Cases 校验族一行。

| ID | 级 | 校验 | 负向夹具 |
| --- | --- | --- | --- |
| **AC-21a-7** | **[L]** | 任一 `outputs[].qty ≤ 0` 拒绝(原稿写 `Recipe.BaseQty ≤ 0`;`BaseQty` 已改为**逐条** `outputs_i.qty`,三轮 blocking #2) | `invalid_base_qty.json` |
| **AC-21a-8** | **[L]** | **构建期守恒上界**(D-21-21,三轮 blocking #1):`QTY_MULT_MAX × Σ(weight × outputs_i.qty) > EFF_MAX × Σ(weight × inputs_j.qty)` 拒绝 —— **产出侧必须含 `QtyMultiplier` 上界**;原稿 `Σ outputs > Σ inputs × EFF_MAX`(裸计数、两侧基数)**漏了产出侧乘子**,可被运行期击穿(实测反例:`w_in=10,w_out=9,EFF_MAX=1,QTY_MULT_MAX=2` ⇒ 构建期 `9≤10` ✓ 而运行期 `18>10` ✗)。**`EFF_MAX > 1` 亦拒** | `invalid_conservation.json` |
| **AC-21a-9** | **[L]** | `Σ(正的 cap) + **max(0, ENV_MOD_MAX)** > QTY_MULT_MAX − 1` 拒绝 —— **`ENV_MOD_MAX` 必须计入**(原稿只约束 `*_CAP`,漏了可正的 `EnvMod`) | `invalid_cap_sum.json` |
| **AC-21a-10** | **[L]** | `QTY_MULT_MIN ≥ QTY_MULT_MAX` 拒绝 | `invalid_qty_range.json` |
| **AC-21a-11** | **[L]** | `RETAIN_MIN > RETAIN_MAX` / `RETAIN_MAX > 1` / `RETAIN_MIN ≤ 0` 拒绝 | `invalid_retain.json` |
| **AC-21a-12** | **[L]** | `ENV_MOD_MIN > ENV_MOD_MAX` 拒绝 | `invalid_env_range.json` |
| **AC-21a-13** | **[L]** | `MAX_QUALITY < 2` 或非整数拒绝 | `invalid_max_quality.json` |
| **AC-21a-14** | **[L]** | `quality_distribution` 支撑 ⊄ `[1, MAX_QUALITY]` 拒绝 | `invalid_quality_dist.json` |
| **AC-21a-15** | **[L]** | `stack_max < 1` 或 `weight ≤ 0` 拒绝 | `invalid_stack_weight.json` |
| **AC-21a-16** | **[L]** | 配方项 `qty ≤ 0` 拒绝 | `invalid_recipe_qty.json` |
| **AC-21a-17** | **[L]** | 配方项 `item_key` **外键悬空**拒绝 | `invalid_recipe_fk.json` |
| **AC-21a-18** | **[L]** | `duration_ticks ≤ 0` 拒绝 | `invalid_duration.json` |
| **AC-21a-19** | **[L]** | `skill_gate ∉ [0, SKILL_CAP]` 拒绝 | `invalid_skill_gate.json` |
| **AC-21a-20** | **[L]** | `min_quality < 1` 或 `> MAX_QUALITY` 拒绝 | `invalid_min_quality.json` |
| **AC-21a-21** | **[L]** | 同 `(base_id, processing_state)` 重复拒绝 | `invalid_dup_key.json` |
| **AC-21a-22** | **[L]** | `category` / `processing_state` 取枚举外字面量拒绝 | `invalid_enum.json` |
| **AC-21a-23** | **[L]** | P0 期出现 P1a 值(`honey_fried` / `dry_fried` / 非空 `tcm_profile`)拒绝 | `invalid_p1a_leak.json` |
| **AC-21a-24** | **[L]** | 配方 state 对不在 `legal_transitions` 内拒绝(如 `raw → extracted`) | `invalid_transition.json` |
| **AC-21a-25** | **[L]** | `category = weapon` 无 `inflicts_injury`,或非 `weapon` 带 `inflicts_injury`,均拒绝。⚠️ **2026-09-17 措辞订正(25 · R13)**:本条只校验**存在性与类别门**,**不校验语义** —— 「哪次命中造成哪个伤」的判定在 25(`maps_to_injury`);集合漂移门另立:**21a 的 `inflicts_injury` 集合 ⊇ 该线全部动作的 `maps_to_injury`,构建期断言,违例 = 硬失败**(承 25 的 A20,fixture:`injury_set_drift.json`) | `invalid_injury_fk.json` / `injury_set_drift.json` |
| **AC-21a-26** | **[L]** | `item_key` 字段以 **int** 编码 `processing_state` ⇒ **装配期断言失败**(D-21-13) | `invalid_state_int.json` |
| **AC-21a-27** | **[L]** | `ItemInstance` 含任何 `UnityEngine` 类型字段 ⇒ **静态断言失败**(§Schema E) | `invalid_instance_unity_ref.cs` |

### 组四 · 确定性(⚠️ 原稿此组是同义反复)

> **原文写「同一组输入连续调用 F1/F2 各十次结果完全一致」。**
> **这对纯函数是同义反复** —— 没有任何实现能让它失败,它**证明不了任何东西**。
> 与 8 的 AC-8-1 是**同一缺陷**(假确定性断言)。
> 真正的风险不在「同进程内调用十次」,而在**跨编译器/跨平台**:
> Mono(Editor)与 IL2CPP(player)对 `long` 运算、`Math.Round`、以及
> 任何残留浮点的结果**可能不同**。ADR-005 的确定性承诺正建立在这之上。

| ID | 级 | 断言 |
| --- | --- | --- |
| **AC-21a-28** | **[L]** | **GIVEN** 参数表**逐条枚举边界组合**(所有 cap 取 0 / 取满、`ENV_MOD` 取 `MIN`/`MAX`、`quality` 取 `1`/`MAX_QUALITY`、`EFF` 取 `EFF_MIN`/`EFF_MAX`、`outputs` 取 `m=1`/`m>1`),**WHEN** F1/F2/F5 求解,**THEN** 每个组合的 **SplitMix64 哈希**与 `tests/unit/item_database/golden/` 下**已提交且经人工核对**的金标准逐位相同。⚠️ **金标准不得由首次运行自动生成**(那是**自指**:任何实现首次跑都能「冻结」成金标准 ⇒ 恒过);**必须由独立参考实现或手算产出、经评审签字后提交**。原稿「首次通过后冻结」正是这条自指缺陷 |
| **AC-21a-29** | **[I]** | **GIVEN** 同一组参数,**WHEN** 分别在 **Editor(Mono)** 与 **IL2CPP 独立玩家构建** 中求解,**THEN** 哈希**逐位相同**。 ⚠️ **本条当前为 UNVERIFIED** —— ADR-005 自述 IL2CPP 逐位性「需实测」。**在实测通过前,21a 不得签署 Determinism 结论** |
| **AC-21a-30** | **[L]** | **GIVEN** F1…F5 的**全部**中间变量,**WHEN** 静态扫描, **THEN** 除 facade 的 `ToFloat()` 外**无任何 `float`/`double`**:无 `Math.Round`、无 `(float)` 转型、无 `Math.Exp`(需手写定点版) —— ADR-005「Storage 中不出现任何 float」的验证 |

### 组五 · 持久化与堆叠

| ID | 级 | 断言 |
| --- | --- | --- |
| **AC-21a-31** | **[I]** | **GIVEN** 含 `quality` 的 `ItemInstance`,**WHEN** 经 **7a** 持久化往返,**THEN** `item_key` 与 `quality` **完全不变** |
| **AC-21a-32** | **[L]** | **GIVEN** 两件同 `item_key` 不同 `quality` 的实例,**WHEN** 尝试堆叠,**THEN** **不合并** |
| **AC-21a-33** | **[L]** | **GIVEN** 堆叠到 `stack_max` 后继续加入,**THEN** 溢出到新实例,**总量守恒**(见 AC-21a-34) |
| **AC-21a-34** | **[L]** | **GIVEN** 一个容器,内部为**子实例 id 列表**,**WHEN** 任意增删后,**THEN** **无实例被静默丢弃或复制** —— 容器结构的守恒验证 |
| **AC-21a-35** | **[I]** | **GIVEN** 物品表标记 `deprecated` 的条目,**WHEN** 读取存档中的对应实例,**THEN** 仍能正确解析(只增不删的验证) |

### 组六 · F5 品级 → 时间轴作用点(⚠️ 二轮换轴 / 三轮收窄)

> **原稿 F5 是「品级 → 药效幅值」**(`Offset` 加减)。二轮复核裁定:幅度是一个
> **可被玩家换算成数值**的维度,撑不起锚点二「品级的质地」。
> **已换为时间轴 / 作用点**(D-21-14):品级偏移 `onset` / `peak` / `half_life` / `elimination`
> 之一(由 `quality_axis` 指名),**永不调幅值** —— 玩家感到的是
> 「这剂药起效更慢、但更久」,不是「这剂药数字更大」。
> **三轮收窄(D-21-23)**:P0 下 `quality_axis` **只允许 `half_life`** —— 9 的处置载荷只有
> `polarity` / `drug_potency` / `half_life`,其余三轴**产出即被丢弃**。

| ID | 级 | 断言 |
| --- | --- | --- |
| **AC-21a-36** | **[L]** | **GIVEN** `axis_offset_by_quality[]` 与 `quality_axis`,**WHEN** 施加于 F5,`THEN` `Axis_effective = Axis_base + axis_offset_by_quality[quality − 1]`,**且为 `Fix`(Q16.16 整数域,无浮点中间量)**;**P0 下 `quality_axis` 必须 = `half_life`**(D-21-23,否则构建期拒) |
| **AC-21a-37** | **[L]** | **GIVEN** `axis_offset_by_quality[]`,**THEN** **至少一档非零,且每个非零档 `|offset| ≥ 可感知地板`**(> 9 的病史噪声带,D-21-24)。「全零 ⇒ 通过」**不是合格结果** —— P0 若真填零,就是锚点二「品级的质地」在机制上落空,**AC 不得替它背书**;若偏移小于噪声带,则**统计上不可区分于噪声** ⇒ 玩家永远感觉不到,而 U-1/U-2 禁数字 ⇒ 出口名存实亡 |
| **AC-21a-38** | **[L]** | **GIVEN** 任意品级,**WHEN** 计算 F5,**THEN** **仅** `quality_axis` 所指的那一条轴按 `axis_offset_by_quality[]` 偏移,**其余三条时间轴逐位不变**(P0 下其余三轴无落点,仅 P1a 后适用)—— 21a **不拥有** `polarity` / `tau_half` 语义(那是 9 的字段),原稿越权断言「极性不变」已删 |
| **AC-21a-38b** | **[L]** | **GIVEN** 任意合法 `drug_profile`,**WHEN** 校验,**THEN** `Axis_base + min(axis_offset_by_quality) > 0` —— 否则 9 的衰减用 `half_life` 作除数会**除零 / 反向衰减**(三轮补,域钳制) |

### 组七 · 守恒律与整数边界

| ID | 级 | 断言 |
| --- | --- | --- |
| **AC-21a-39** | **[L]** | **GIVEN** 任意合法配方、任意 `EFF ≤ EFF_MAX`、任意 `QtyMultiplier`,**THEN** `Σ(weight × OutputQty_i) ≤ EFF_MAX × Σ(weight × ActualConsumed_j)`(规则八 / D-21-21)—— **两侧同量纲(重量单位),产出侧含 `QtyMultiplier`、投入侧取实耗非基数**;整数域**先乘后比、禁逐项舍入** |
| **AC-21a-40** | **[L]** | **GIVEN** `EFF_MAX > 1` 的任何常量表,**WHEN** 加载,**THEN** 构建期硬失败 |
| **AC-21a-41** | **[L]** | **GIVEN** 任一含 `drug_potency` / `half_life`(及 `onset`/`peak`/`elimination`)/ `axis_offset_by_quality[]` 的数据文件,**WHEN** 解析,**THEN** 读入类型为 **`Fix`(Q16.16 int64)**,**不接受浮点字面量**;**任一字段写浮点即导入期硬失败** —— D-21-9 / D-21-22 的落盘验证。**`weight` 不在此列 —— 它是 `int`(最小单位个数,D-21-17);原稿误将 `weight` 列入 `Fix` 解析集;三轮补:原稿字段名 `Offset`/`τ_half` 已改名 `drug_potency`/`half_life`(D-21-22),且原稿只列了 `axis_offset_by_quality[]` 一个字段,漏了 `drug_potency` 与时间轴四字段** |
| **AC-21a-42** | **[L]** | **GIVEN** `ROUND_HALF_AWAY_FROM_ZERO` 的边界:`Round(0.5)` / `Round(−0.5)` / `Round(1.5)` / `Round(2.5)`,**THEN** 结果分别为 `1 / −1 / 2 / 3` —— **显式切断 `Math.Round` 的 ties-to-even** |

### 组八 · 内容合规

| ID | 级 | 断言 |
| --- | --- | --- |
| **AC-21a-43** | **[D]** | **GIVEN** 21b 的全部 P0 物品名,**WHEN** 逐条对照 §命名规范 与 21b 的保留词表,**THEN** 不出现任何被保留词表命中的词 |
| **AC-21a-44** | **[D]** | **GIVEN** P0 可玩版本,**WHEN** 玩家查看任何物品名,**THEN** 其中不出现被保留词表命中的词 |

### 组九 · 呈现契约(欠 42)

| ID | 级 | 断言 |
| --- | --- | --- |
| **AC-21a-45** | **[U]** | **GIVEN** 42 的物品呈现 UI,**WHEN** 走查任意物品,**THEN** **不出现数字品级、不出现线性刻度条**(U-1 / U-2) |
| **AC-21a-46** | **[U]** | **GIVEN** 同一 `base_id` 的生药与炮制品,**WHEN** 并排走查,**THEN** **视觉可区分**(U-4) |

### 组十 · 工程与欠账

| ID | 级 | 断言 |
| --- | --- | --- |
| **AC-21a-47** | **[D]** | Performance:物品表全量加载 + 单次配方求解 **< 1 ms**(数据量小,非瓶颈) |
| **AC-21a-48** | **[L]** | No hardcoded values —— 全部读数据文件(见 §Tuning Knobs 归属与存放) |
| **AC-21a-49** | **[D]** | **§Debt Register 的每一行都有 `owner` 与状态标记**;任一 ⏳ 行**无 owner** 即失败 —— 欠账不许匿名 |

### 组十一 · 二轮大修新增(MAJOR REVISION 兑现项)

> 本组为 2026-09-14 二轮 full 复核后**新增**的 AC;编号接续,与上文各组的归属不重叠。
> 每条对应一项二轮 blocking 的**落盘验证**。
> **AC-21a-55…64 为三轮复核新增**(2026-09-14):分别兑现 U-6(D-21-24)、
> `EFF_MIN` 下端、`drug_potency`/时间轴字段浮点守门、容器闭包、
> `stackable` 派生性、`quality_axis` P0 收窄、可感知地板、
> `drug_quality_character[]` 长度、`instance_id` 权威(D-21-26/27)、int64 溢出上界。

| ID | 级 | 断言 |
| --- | --- | --- |
| **AC-21a-50** | **[L]** | **GIVEN** `drug_profile.axis_offset_by_quality[]`,**WHEN** 长度 ≠ `MAX_QUALITY`,**THEN** 构建期硬失败 | `invalid_drug_offset_len.json` |
| **AC-21a-50b** | **[L]** | **GIVEN** `gather_profile.quality_character[]`,**WHEN** 长度 ≠ `MAX_QUALITY`,**THEN** 构建期硬失败。**⚠️ 2026-09-25 R13 = 甲 扩充**:`MAX_QUALITY > 1` 且该列 **null / 整列空 ⇒ 同样构建期硬失败**(原「非空时才查长度」豁免废止),且**逐档非空**;**✅ 回写完成(2026-09-25)**:门扩落 `DrugProfileGates` + 负向 fixture ×1 + 翻转/新增测试,EditMode 484/484 绿 | `invalid_gather_char_len.json` + `invalid_gather_char_empty.json` ✅ |
| **AC-21a-51** | **[L]** | **GIVEN** 任一 `axis_offset_by_quality[]` 内出现浮点字面量,**THEN** 导入期硬失败(`FixParse` 拒绝)—— D-21-14/D-21-19 的落盘验证 |
| **AC-21a-52** | **[I]** | **GIVEN** 一次含 `EFF < EFF_MAX` 的结算,**WHEN** 经 7a 持久化往返,**THEN** **配方数据文件与物件实例里只有基数 `inputs[].qty`**,`ActualConsumed` **只出现在该次 Craft 的历史事件载荷中**(由 `EFF` 可再推出)—— 实耗是运行期派生量(`Ceil(Base / EFF)`,D-21-15);把它冻进静态数据,技能值一变即与重算不符 |

> ⚠️ **原 AC-21a-50 的块路径是错的**(三轮 blocking #3):它写「`gather_profile.axis_offset_by_quality[]` 或 `quality_character[]`」——
> 但 `axis_offset_by_quality[]` 住在 **`drug_profile`**(F5),`quality_character[]` 住在 **`gather_profile`**(D-21-16)。
> 一个 AC 指错块 ⇒ 夹具无从落笔;已**拆为两条**,各带正确路径与各自夹具。

| **AC-21a-53** | **[L]** | **GIVEN** `Fix` 的自定义编码器,**WHEN** 往返(`_raw` 的 `long` 显式写出/读入)一组 `drug_potency` / `half_life` / `axis_offset_by_quality[]`,**THEN** 值**逐位复原**(含负值 `axis_offset_by_quality[]`)。⚠️ **本条断言的是「自定义编码器正确」,不是「Unity 内置序列化器必然失败」**—— 原稿把「探针断言字段丢失/归零」写成**通过条件**,等于**把引擎缺陷当成契约**(若 Unity 6.3 某日修好,这条会**因引擎变好而失败**)。内置序列化器的行为(Unity 跳过 `readonly struct` 的私有字段)**由一条 EditMode 探针记录为观察事实**,用于证明 D-21-18 的**动机**,**不作为断言** | `tests/unit/item_database/fix_codec_roundtrip.cs` |
| **AC-21a-54** | **[U]** | **GIVEN** 42 的物品呈现 UI,**WHEN** 走查任一有 `quality_character[]` 的物品,**THEN** 品级以**外观 / 药签措辞**呈现(U-5),**不得**退化为数字品级或线性刻度(U-1 / U-2) |
| **AC-21a-55** | **[U]** | **GIVEN** 42 的物品呈现 UI,**WHEN** 走查任一有 `drug_quality_character[]` 的**成药**,**THEN** 品级以**药签措辞 / 外观**呈现(U-6),**不得**退化为数字品级或线性刻度 —— 成药侧与原料侧**各有一条通道**,二者互为补充 |
| **AC-21a-56** | **[L]** | **GIVEN** 常量表:`EFF_MIN ≤ 0` 或 `EFF_MIN > EFF_MAX`,**WHEN** 校验,**THEN** 构建期硬失败 —— `EFF` 是 F1 的**除数**,下端此前无人守(三轮) | `invalid_eff_range.json` |
| **AC-21a-57** | **[L]** | **GIVEN** 任一 `drug_potency` / `half_life` / `onset` / `peak` / `elimination` 写浮点字面量,**THEN** 导入期硬失败(`FixParse` 拒绝)—— 与 AC-21a-51 同族,补上原稿漏掉的时间轴字段与药效幅值 | `invalid_potency_float.json` |
| **AC-21a-58** | **[L]** | **GIVEN** 任一容器实例,**WHEN** 校验闭包,**THEN** 无 `instance_id` 同时属于两个容器 / 无自引用 / 容器图无环 / `children` 内每个子实例均已被登记 —— 容器守恒的**结构前提**(原稿 AC-34 只验「增删不丢失」,未验**闭包**) | `invalid_container_closure.json` |
| **AC-21a-59** | **[L]** | **GIVEN** 物品表,`stackable` 被**显式写入数据文件**,**WHEN** 校验,**THEN** 校验拒绝 —— 它是 `stack_max > 1` 的**派生量,不存储**(§Schema A) | `invalid_stored_stackable.json` |
| **AC-21a-60** | **[L]** | **GIVEN** `drug_profile.quality_axis` 在 P0 期取值 ∉ `{half_life}`,**WHEN** 校验,**THEN** 构建期硬失败 —— D-21-23 的收窄落盘 | `invalid_axis_p0.json` |
| **AC-21a-61** | **[L]** | **GIVEN** `axis_offset_by_quality[]` 的任一**非零档** `|offset| < 可感知地板`,**WHEN** 校验,**THEN** 构建期硬失败 —— D-21-24 第二半;地板数值待与 **9 的噪声带宽**一起定(§Tuning Knobs) | `invalid_offset_floor.json` |
| **AC-21a-62** | **[L]** | **GIVEN** `drug_quality_character[]` **非空**且长度 ≠ `MAX_QUALITY`,**WHEN** 校验,**THEN** 构建期硬失败 —— 与 AC-21a-50b 同型(成药侧)。**⚠️ 2026-09-25 R13 = 甲 同批扩充**:`MAX_QUALITY > 1` 且该列 **null / 整列空 ⇒ 同样构建期硬失败**(`null` 可空豁免废止,与 AC-21a-50b 对称),且**逐档非空**;**✅ 回写完成(2026-09-25)**:同批门扩 + 负向 fixture ×1 + 测试翻转,484/484 绿 | `invalid_drug_char_len.json` + `invalid_drug_char_empty.json` ✅ |
| **AC-21a-63** | **[I]** | **GIVEN** `instance_id` 的铸造路径,**WHEN** 检索全部铸造点,**THEN** **唯一来源 = `IIdAuthority`**(或主机);**17 采集不得在客户端本地铸造**—— D-21-26/D-21-27 的验证(迁移后重号会让两件物品共用一个 id,**不崩溃、只表现为「物品悄悄合并/丢失」**) | `tests/unit/item_database/id_authority.cs` |
| **AC-21a-64** | **[L]** | **GIVEN** 配方与实例的最大 `weight` / 最大 `stack_max` / 最大 `MAX_QUALITY` 同时取满, **WHEN** 求 `Σ(w × InstanceWeight)`, **THEN** 结果不溢出 `int64` —— 守恒律整数域求值的**上界前提**(三轮:`Σ(w×·)` 先乘后比,须先证不溢出) | `invalid_weight_overflow.json` |
| **AC-21a-65** | **[L]** | **GIVEN** 任一配方, **WHEN** 构建期以**逐条同形极值式**校验:`Σ( weight_out × max(1, Round(outputs_i.qty × QTY_MULT_MAX)) ) ≤ EFF_MAX × Σ( weight_in × Ceil(inputs_j.qty / EFF_MAX) )`, **THEN** 违反即硬失败 —— **D-21-32 的落盘验证**(聚合式 `QTY_MULT_MAX × Σ(w×产出基数) ≤ EFF_MAX × Σ(w×输入基数)` 单独使用可被逐条 `Round` 击穿,反例见规则八注;聚合式保留为**必要非充分**条件,极值式为唯一硬门)。⚠️ 极值式在整数域求值(`Round` = `ROUND_HALF_AWAY_FROM_ZERO`,先乘后比,禁浮点中转 —— ADR-006) | `invalid_conservation_perline.json` |
| **AC-21a-66** | **[L]** | **GIVEN** 全量配方表, **WHEN** 装载期按 `owner ∈ {process, craft, build}` 分三子集, **THEN** 并 = 全表、两两交 = ∅、**任一配方缺 `owner` 即硬失败** —— D-21-30 的落盘验证(18 的 `AC-18-18` 断同一判据的调用方半边) | `invalid_recipe_owner.json` |

> **21a 门控结论**:组三(21 条负向夹具,含三轮新增的 56–62 号)、组四(AC-21a-29 IL2CPP 实测)、
> 组十一(AC-21a-53 `Fix` 编码器往返)、
> 以及 **§Debt Register 中的 D-21-9 / D-21-11 / D-21-12 / D-21-20 / D-21-26 / D-21-27** 未兑现前,
> **21a 可标 In Review,不得标 Approved。**
> (**D-21-19 已于 2026-09-14 同轮就地落盘,不列门控;D-21-25 为字段改名对齐,随 9 / 11 落笔回收。**
> **D-21-28 / D-21-30 / D-21-31 已于 2026-09-19 裁定并落盘(18 首轮评审涟漪),摘除;
> D-21-32 修法已落盘、实现未写 ⇒ 不摘除但降级为「实现期门」。** —— 21a 的 Approved 系
> 2026-09-18 显式风险接受结案(页眉),本行是当时的门控口径存档;本轮三项为**已兑现的债**,
> 按页眉重开触发条件①的同类逻辑,实现期构建期门(AC-21a-65/66)兜底 D-21-32 的未实现部分。)
> ⚠️ **三轮新增门控**:`instance_id` 权威契约(D-21-26)与 17 采集铸造权(D-21-27)是**静默失败**类
> —— 不兑现不会让任何测试变红,只表现为「物品悄悄合并/丢失」,故必须显式门控。

## Open Questions

| Question | Owner | Deadline | Resolution |
| ---- | ---- | ---- | ---- |
| 重量单位名(库平两 / 市斤 / 克) | 用户 + 专项考据 | 21b 撰写前 | **未定** —— Opus 给的三条断言中两条未获独立验证(支柱五,**不得凭空落盘**) |
| `MAX_QUALITY` 取几档 | 用户 | 20 库存设计前 | 未定(**数值**) |
| `stack_max` / `weight` 各物品默认值 | 用户 | 20 库存设计前 | 未定(**数值**) |
| `axis_offset_by_quality[]` 的档位步长(**P0 轴已定 = `half_life`**) | 用户 | 11 处方 / 9 接通前 | 未定(**数值**);F5 已定形,轴已由 D-21-23 定死,只差**填数 + 定可感知地板** |
| `EFF_MIN` / `EFF_MAX` 的取值带 | 用户 | 18 炮制设计前 | 未定(**数值**);硬约束 `EFF_MAX ≤ 1` **与 `EFF_MIN > 0`** 已定(三轮补下端) |
| **F5 可感知地板的数值** | 用户 + 9 | 11 处方 / 9 接通前 | **未定(数值)** —— 须 **> 9 的病史噪声带**;须与 9 的噪声带宽一起定(跨系统常量) |
| 配方循环检测是否需要 | — | 若 P1a 改为数据驱动配方 | P0 不需(手写数据无环,由 21b 人工保证) |
| 品级在拟物 UI 上如何呈现(不得出现数字) | 42 拟物 UI | 42 设计时 | **约束已定形**(U-1…U-4),**解未定** |
| **IL2CPP 逐位性与 Mono 相同?** | 技术 + 硬件实测 | ADR-005 复验时 | **未定** —— AC-21a-29 当前 UNVERIFIED,**gate 21a 签署** |
| **21b 的考据工作量** | producer + 21b 撰写者 | 21b 立项前 | **未估算** —— 已按裁定⑤并入 §范围债(D-21b-3) |
| **P0 遭遇导演的归属** | — | — | **已结案(2026-09-13)**:新立 **#52 随机事件导演**(初判并入 27,因 P1a 起还要调度疫情/篇章/求诊而独立)。危险动物在 **P0 是纯威胁**,**动物药材属 P1a**(随已学药典随机获得),**不进 21b 的 P0 清单** |

---

> **本文件 = 21a(契约层)。** P0 具体物品清单、配方数据与史实出处 = **21b**,
> 须按**支柱五(史实为骨)** 逐条考据后另立。
> **§命名规范**(指针)与 §Schema 对 21b 有约束力;
> 21b 须落定三件事:**药典版本 · 药品名豁免 · 保留词表**(D-21-12)。
