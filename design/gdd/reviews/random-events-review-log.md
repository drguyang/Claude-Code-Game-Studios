# 评审记录 · 系统 52《随机事件导演(Random Event Director)》

## 结案追记(2026-09-18 · 用户裁定)—— ✅ **Approved(覆盖日志结论,显式风险接受)**

二轮复核(2026-09-15)末段明写「仍 In Review,不得标 Approved」,门控项四项:① ~~ADR-007 转 Accepted~~ ✅ 2026-09-15 已裁定;② AC 夹具与实测;③ IL2CPP 实测;④ **承重问题门控在 37**(「P0 的 52 是否只是换了强度输入源的英灵神殿 raid director」)。**用户裁定:接受两轮修订落盘状态、以显式风险接受标记 Approved**(承 37 / 42 / 51 先例)。

**⚠️ 这条结案是显式风险接受,不是「已核对」。** **承重问题(④)在 Approval 时仍为 OPEN** —— 它需 37 的**内容 playtest** 才能判,属 P0 后期的开放风险,非本 GDD 的规格缺陷。门控 ②③(AC 夹具 / IL2CPP 实测)为实现期产物。

**重开触发条件(三者任一)**:① 37 内容 playtest 证明 P0 的 52 无区分度(承重问题判负);② `ReputationMult` 驱动锚点①在 P0 找到输入源(现状:P0 无输入源,见 `AC-52-55`);③ 四轴任一轴的 AC 夹具暴露出规格空洞。

---

| 字段 | 值 |
| ---- | ---- |
| 目标文档 | `design/gdd/random-events.md`(修订前 429 行 · 8 节齐全 + Visual/Audio · UI · Open Questions) |
| 评审日期 | 2026-09-14 |
| 评审轮次 | **首轮**(`design/gdd/reviews/` 此前只有 8 / 9 / 21a 的记录) |
| 模式 | `/design-review` **full**(六名专家并行 + 高阶综合) |
| **裁决** | **MAJOR REVISION NEEDED** |
| Scope Signal | **L**(多系统集成 · 3 条公式全部需改写 · 6 个依赖未落盘 · 跨 27/37/9/23/42 五系统契约;不引入新 ADR,但**受 ADR-005/006 约束而未兑现**) |

## 参评专家

`game-designer`(幻想/锚点)· `systems-designer`(公式/边界值)· `network-programmer`(同步/权威)· `qa-lead`(AC 可测性)· `level-designer`(空间/时机)· `unity-specialist`(引擎/序列化)· `creative-director`(高阶综合)

## 完整度

**8/8 节齐全**,另有 Visual/Audio、UI Requirements、Open Questions 三节超额。
**缺陷**:§Formulas **零 example calculation**(违 `design-docs.md`「变量定义 + 预期值区间 + 示例计算」三件套,缺两件);变量表只有「含义」列,无 **Type / Range**。

## 依赖图

Dependencies 声明 13 个系统,**磁盘上只有 1 个有 GDD**:

- ✓ `9 疾病与伤情模拟` — `disease-simulation.md` 存在
- ✗ `5 时间天气` · `6 世界与生态区` · `27 敌人 AI` · `31 声誉` · `32 生态区推进门槛` · `34 公卫与疫情` · `35 时代事件` · `36 NPC 与村落` · `45 网络层` · `23 建造` · `42 拟物 UI` — **NOT FOUND**
- ✗ `37 病例系统` — **NOT FOUND,且状态「未开始」** —— P0 医疗内容的**唯一所有者**,也是 P0「拼图样 Boss」的载体。**P0 的承重墙当时没有施工方。**
- **漏登(双向性破口)**:`21a 物品库`(`CarryLoad` 来源,`item-database.md:798` 已登记「52 ← 21」,52 本表漏)与 `44 音频`(预告线索实现方)。

## 必改(blocking 10 项)

| # | 发现 | 来源 |
| --- | --- | --- |
| 1 | **随机源 / 定点域 / 时间量纲三处契约全缺席** —— 全文 grep `ADR-005`/`定点`/`Fix`/`SplitMix`/`world_seed` **零命中**。9 已钉死 `patient_seed = hash(world_seed, patient_id)` 且禁 `Random.Range`;52 通篇无种子定义。实现者必落到 `UnityEngine.Random`(全局单例状态)→ 两机抽出不同事件 → 生成不同病人 → `IIdAuthority` 序列错位 → **病史事件流从第一条起永久分叉,且失败是静默的** | network · unity · systems |
| 2 | **F1 的全局乘子在归一化中互相抵消** —— `p_i = w_i·k / Σ(w_j·k) = w_i/Σw_j`;若 `TODMult`/`SeasonMult`/`ReputationMult` 是全池标量,它们对分布**零作用**。自夸的「有名是把双刃」**数学上不可能实现** | systems |
| 3 | **P0 的 Boss 靠彩票降临** —— 支柱四的 P0 Boss = 三案链 = 原型疫情,却挂在**灾难档随机**;F2 规定超预算即**丢弃**;边界表又写「P0 一次都没遇到事件 = 合法」。三者不能同时为真。**承重墙不能靠骰子** | game · creative-director |
| 4 | **因果(规则九/十)设计了但没交付** —— 「你做了什么,世界就回什么」是幻想三锚点之一,却不在 F1、不在状态机、**零 AC**。只剩两条死路:退化成不可观测的权重微调,或被「预算耗尽→直接丢弃」静默吞掉 | game · level |
| 5 | **规则七(医馆损毁)在 P0 无触发源** —— P0 威胁档(兵痞/野兽)**不打建筑**;围困医馆是 P1a。规则七的 F3 公式与三条 AC 挂在**不可达路径**上 | game |
| 6 | **F1 与规则一自相矛盾,registry 是第三口径** —— 规则一「抽池 → 强度定档」;F1 让「强度系数」当**抽一的乘子**;`entities.yaml` 的登记式**不含**它。且 `StrengthTier → 系数` 的映射函数完全未定义 | systems |
| 7 | **强度轴量纲不齐 + 无下限保护** —— `CarryLoad`(10²–10³,无上界)以 1–2 个数量级**淹没** `RelevantSkill`(1–60)与 `SoloFlag`;`SoloFlag` 在 P1b 从 bool 变「队伍规模」(语义漂移);`MIN_tier` 全局常数会把新玩家**向上**拉 | systems · game |
| 8 | **AC 体系不达标,且是复发缺陷** —— 17 条 AC **无编号 / 无 BLOCKING 分级 / 无 Story Type 标注**(21a 二轮已裁定,本文件**未继承**)。四条硬要求**零守门**:可拒绝可绕开 · P0 四档齐全 · 预告窗口 > 0 报错 · 因果可见。`P0 ReputationMult = 1` 是**同义反复死断言** | qa |
| 9 | **F3 三处边界退化** —— `SALVAGE_RATE ∈ (0,1]` **含 1** ⇒ 重建免费,焚毁档失去 stakes;`DamageRatio=1` 时 `RepairCost > RebuildCost` ⇒ **修不如拆**;`RepairCost` 无回收项;焚毁档无成本式 | systems |
| 10 | **分母可为零** —— `SeasonMult` / `ReputationMult` / `KeyGate` 任一使全池权重归零 ⇒ `Σw = 0` ⇒ 加权抽样**除零** | systems |

## 推荐修改(10 项)

1. **F2 无变量表** —— `EventBudgetPerDay` 是**输出**却被塞进旋钮表;`BASE` 与 ΣMod 三分量全无旋钮;`BASE` 名 `PerDay` 而规则六说「每日 / 每次出诊」**两个分母未择一**;ΣMod 的「联机人数」项与「按队伍共享预算」**冲突**
2. **旋钮不全** —— 「机会 : 威胁 = 1:1」只有 `机会档占比` 一个旋钮 ⇒ 比值不可调、占比和可 > 1;`TODMult` 无上界,可垄断池
3. **构建期校验收口不齐** —— `MIN_tier / MAX_tier` 安全范围是「—」,`MIN > MAX` 无守卫;两条「构建期报错」只兑现一条
4. **规则十(疏忽触发)与规则七护栏 1 显式冲突** `[level]` —— 规则十是损毁类后果,而护栏 1 要求损毁类**必须先有预告**;「无人」怎么判、「荒芜」是建造件还是地块,全无定义
5. **位置过滤在 P0 是空操作** `[level]` —— §Interactions 声明「6 → 抽池的位置过滤」,但 F1 式子里**位置项缺席**
6. **时机轴无上下文感知** `[level]` —— 只有「随机间隔 + 昼夜」。设计意图是「**出诊途中**遇敌」,却会变成「回医馆整理脉案时连刷两波」
7. **预告窗口没有空间尺度** `[level]` —— 承诺「玩家有时间回防」,但窗口时长与场景直径的关系无人定义;P0 又无地图(#43 是 P1a)
8. **`52 → 9` 的写入通道不合法** `[unity][network]` —— 进 sim 的唯一通道是主机 `IEventSink.Append(SimEvent)`,52 的「把参数交给 9」未走此路;损毁等级无 `SimEvent` 归属
9. **反幻想与 ADR 对撞** `[unity]` —— 「事件一旦可被计算和刷取,它就不再是世界的一部分」vs ADR-005「必须逐位可计算」。需补一句:反计算是 **UI 层**的否定声明,不是模拟层约束
10. **性能 AC 无守门力** —— 「单次抽取 < 1 ms」在 P0 池约十条时必然通过;真正风险是**每帧轮询**与 roll 路径的堆分配,无对应 AC

## 专家分歧与裁决

| 争点 | 一方 | 另一方 | creative-director 裁决 |
| --- | --- | --- | --- |
| **深水线归谁** | `level-designer`:52 必须补 `DistanceFromClinic`,并改写 AC line 386(它把坐标依赖验证成「必须为零」= 锁死未来接入) | `game-designer`:P0 无空间轴是**设计意图**;`systems-index.md`:深水线挂在 5 · 17 | **第三条路** —— 深水线是 P0 必需,但**52 不是它的载体**(52 的强度输入是玩家侧 `PlayerState`,深水线是空间侧)。由 5 或 17 交付;**52 只登记归属与 P0 兑现方式**。P0 **不接** `DistanceFromClinic` |
| **三案链是否移出随机池** | `game-designer`:**移出**,否则 P0 Boss 变彩票 | 但 52 存在理由之一就是持有这三类模板;§2/§5 已裁定「池条目归 52、内容归 37」 | **移出,且不动摇既有裁定** —— 移出抽池**不是让 37 抢调度权**,而是让触发从「骰子」变「脚本」;调度**仍是 52 职责**。类比 #35 时代事件。52 依然持有池条目(`触发方式=脚本`),「52 不生产内容」的铁律不动 |
| **规则七是 P0 还是 P1a** | `game-designer`:**P1a**(P0 威胁档不打建筑) | `level-designer`:当 P0 处理,并索要空间尺度 | **P1a**(站 game-designer)。规则七的公式与 AC 标 **(P1a)**;P0 只留一句「医馆在 P0 不可被损毁」。level-designer 是把 P1a 的正当诉求误安在 P0 头上 |
| **AC `P0 ReputationMult = 1` 该删还是改写** | `qa-lead`:同义反复死断言,缺陷 | — | **改写,不是删** —— 它承载「P0 无声誉系统」这条设计事实,必须留下可测落点。改为**依赖表守门 + 公式路径守门** |

## 成熟度判断

> **骨架正确、接缝三块缺席、一处自相矛盾。**
> 设计骨架(四轴 / 预告制 / 因果 / 进度锁=行医)全部成立,但**三份最关键的接缝(随机源、定点域、因果机制)完全缺席**,
> 且 P0 的「拼图样 Boss」在文档内被自己的预算规则判定为**可能缺席**。这是结构性错漏,不是措辞打磨。
> **不是设计错了,是文档没把已经钉死的架构约束(ADR-005/006)和已经裁决的设计边界写进去。**

**专家分歧之外,高阶综合另标两处需用户确认**:① 修复集 #4(`ReputationMult` 改按档非对称)是**设计口径改动**而非纯技术修正,应归入用户裁定项;② `game-designer` 的 S8 —— P0 版 52 在无声誉、无进度锁、无损毁源时,是否承认「就是换了强度输入源的英灵神殿 raid director」。

## 处理

用户裁定(2026-09-14):**现在就修订 GDD** · 一次多页问答收口四项设计口径(全部取推荐项)· **不突破 6-9 个月基线**。

---

## 修订落盘(2026-09-14,同日)

**10 项 blocking 已全部兑现 + 10 项推荐已并入 + 4 项用户裁定已落盘。**

### 用户裁定(四项,均取推荐项)

| # | 争点 | 裁定 |
| --- | --- | --- |
| ① | 三案链触发 | **移出抽池,归 37 确定触发** —— 52 池条目 `触发方式=脚本`,调度仍归 52 |
| ② | 因果机制 | **F1 加历史权重乘子 + 可读线索** —— `HistoryMult` + `cause_clue_key`(非独立触发路径,避免被预算丢弃吞掉) |
| ③ | 深水线 | **52 只登记归属,不改强度轴** —— P0 不接 `DistanceFromClinic`;由 5/17 交付 |
| ④ | 强度轴下限 | **加强度上限帽** —— `StrengthCap = min(StrengthTier, f(最高格斗线, 急救))` |

### blocking 逐项落点

| # | 落点 |
| --- | --- |
| 1 | **新增 `## Determinism Contract(确定性契约)`** —— **DC-1** 随机源:`EventRollSeed(t,k) = SplitMix64(WorldSeed, t, k)`,禁 `UnityEngine.Random`,P0 预留 `IEventAuthority`;**DC-2** 定点域:全量 `Fix`,**`W_base` 是 `int`**,无浮点字面量,整数 `Min/Max` 钳位,禁 ScriptableObject 承载 `Fix`;**DC-3** 时间量纲:全走 `ITickProvider`,禁 `Time.time`/协程,日 = `TICKS_PER_DAY`;**DC-4** 事件流归属:降临 = 一条 `SimEvent`,只经主机 `IEventSink.Append`,52 **零** `patient_id` 分配 |
| 2 | **F1 完全重写为三步**(档配额 → 逐条权重 → 定档钳位);新增 `ReputationMult[档] = { 机会: R_opp, 反应: R_react, 威胁: 1, 灾难: 1 }` —— **非对称双参数**,并写明「对称全局乘子做不到」 |
| 3 | **三案链移出抽池**(用户裁定①)—— 规则十一新增裁定块;新增 **`触发方式`** 枚举字段(`随机` / `脚本`);脚本条目**不参与密度预算**;AC-52-30/31 |
| 4 | **规则九新增 `HistoryMult = Π(1 + HIST_W[flag])`** + 因果可见硬要求(`cause_clue_key` 由 42 呈现);**明确不是独立触发路径**;AC-52-17…19 + **AC-52-M1**(人工签核) |
| 5 | **规则七标 `(P1a — 2026-09-14 复核修正)`** —— P0 威胁档不打建筑、P0 无地图 #43 ⇒ 「回防时间」无法交付;P0 只留「医馆在 P0 不可被损毁」。F3 全节标 **P1a** |
| 6 | **规则一拆为两行**(抽池在先、定档在后)+ 新增**池条目 schema 表**(`key` / `W_base` / `档` / `anchor` / `触发方式` / `cause_flag?`);F1 重写后与规则一同口径 |
| 7 | **F1 强度轴全部归一化到 `[0,1]`** + `W_strength[变量]` 权重表 + **`StrengthCap` 上限帽**(用户裁定④)+ **`MIN_tier` 随进度缩放** + 构建期 `MIN ≤ MAX`;新增 **worked example**(甲/乙/丙,含「SoloFlag 淹没小量级项」的数值证据);AC-52-22…25 |
| 8 | **AC 全量重写** —— **41 条 + 1 条人工**,编号 + `[B]/[A]` 分级 + `[E]/[I]/[M]` 层 + 十组分组;grep 式否定断言**升级为 Roslyn 类型引用级断言**(AC-52-03);四条此前零守门的硬要求各得 AC(15/16 · 02 · 14 · 17…19);死断言 `ReputationMult=1` 改为依赖表 + 公式路径**双重守门**(AC-52-20) |
| 9 | **F3 重写** —— `SALVAGE_RATE` 范围收为 **(0, 1)**;`RepairCost = Σ(受损件.base_cost × DamageRatio) − SalvageValue`(补回收项);新增 `RebuildCost_razed` 与 `SALVAGE_RATE_RAZED`;新增 `DAMAGE_MIN > 0`;AC-52-34/35 |
| 10 | **Edge Cases 补 `ΣW_j = 0` 行**(跳过该档配额、不掷骰、不报错);AC-52-38 |

### 推荐项逐项落点

| # | 落点 |
| --- | --- |
| 1 | **F2 重写** —— `EventBudgetPerDay = clamp(BASE + ΣMod, 0, BUDGET_MAX)`;补变量表;**删「联机人数」项**;分母钉死 `TICKS_PER_DAY`;超预算**丢弃不排队** |
| 2 | **Tuning Knobs 全表重写**(加「类型」列,全为 `Fix`/`int`/tick)· `档占比[4]` **四档各一个旋钮** · `TODMult` 补上界要求 · `CONTEXT_*` 三旋钮 |
| 3 | F1 补构建期 `MIN_tier ≤ MAX_tier` 校验;AC-52-14(预告窗口 `=0` 与 `<0` 分别断言)· AC-52-35 |
| 4 | **规则十补三条裁定** —— 规则十**受规则八约束**(损毁类须先预告)· 「无人」判定源 = 23/24 的医馆地块占用 · 时间单位 = `N × TICKS_PER_DAY` |
| 5 | AC-52-22 改写为「P0 无空间纵深是设计意图 + 深水线由 5/17 交付,52 登记」;Dependencies 补**深水线信号**归属行 |
| 6 | **规则六之二 `ContextGate`** 新增 —— `ContextMult` 覆盖 travel / clinic / cooldown;AC-52-29(医馆内威胁档被压制并**延后**,预算不丢弃) |
| 7 | 预告窗口时长单位钉死为 **tick 数**;P0 无地图作为规则七转 P1a 的理据之一 |
| 8 | **DC-4**(同上 #1);Dependencies 补 `IEventSink` / `IEventAuthority` / `ITickProvider` 三行 |
| 9 | **§Player Fantasy 反幻想块补否定声明** —— 「不可被计算」是 **UI 层的否定声明,不是模拟层的约束**;模拟层恰恰要求逐位可计算可回放,两者不冲突 |
| 10 | **AC-52-39**(IL2CPP Release:GC.Alloc = 0 字节 且 P99 < 0.1 ms —— 原文「< 1 ms」无鉴别力)· **AC-52-40**(由 tick 触发求值,无每帧轮询) |

### 同轮并入

- **Dependencies 表**补 6 行(`21a` · `20` · `44` · `ITickProvider` · `IEventSink` · `IEventAuthority` · 深水线信号),并补**双向性缺口警告**(13 个声明系统中**仅 9 有文件**)与**「52 的修订必须先于 37 落笔」**的顺序约束
- **Edge Cases** 新增 8 行:脚本条目与随机条目同时到期 · 医疗 XP 归 30 · 医馆内威胁档 · 停留时长不进 `t` · 战斗中预告 · `ΣW=0` · `MIN_tier > MAX_tier` · 预 告窗口 `≤ 0`
- **规则一**补注:`强度系数` 若写进全池乘子会被归一化约去
- **规则二**补注:52 不持任何内容字段
- **规则四**补 `StrengthCap` 上限帽公式与 P1b `SoloFlag` 语义漂移警告

**未做(留待后续):**
- **42 条 AC 的夹具与测试尚未实际编写**(AC 已要求,夹具是撰写后的工作)
- **AC-52-39 的 IL2CPP 逐位实测**未做 —— 与 21a 的 AC-21a-29 同为 ADR-005「需实测」项
- **数值全部待用户裁定**(Tuning Knobs 表 18 行全为 *待定*)—— 按用户铁律,本 GDD 只交付公式 + 变量表 + 旋钮
- **P1a / P1b 的事件清单未列**,须逐条考据(支柱五)
- **37 病例系统仍不存在** —— 52 已把接口从「随机 vs 脚本未定」收敛为「`触发方式=脚本`」,37 可以开工

**结论:52 的骨架与接缝已补齐,ADR-005/006 的约束已在正文兑现。仍 **In Review,不得标 Approved** —— 门控项为 42 条 AC 的夹具与实际测试,以及 IL2CPP 实测。**
**建议下一步**:先启动 **37 病例系统**(它被 52 的修订解锁,且是 P0 拼图样 Boss 的唯一载体);或按 §6 设计顺序继续。

---

## Review — 2026-09-14 — Verdict: MAJOR REVISION NEEDED
Scope signal: L
Specialists: game-designer · systems-designer · network-programmer · qa-lead · level-designer · unity-specialist · creative-director(高阶综合)
Blocking items: 10 | Recommended: 10
Summary: 设计骨架(四轴 / 预告制 / 因果 / 进度锁=行医)全部成立,但三份最关键的接缝(随机源、定点域、因果机制)完全缺席 —— 全文 grep `ADR-005`/`定点`/`Fix` 零命中,而 9 已钉死种子契约;F1 的全局标量乘子在归一化中被约去,「有名是把双刃」数学上不可能实现;P0 的拼图样 Boss(三案链)挂在灾难档随机池,被自己的预算规则判定为可能缺席。另:规则七在 P0 无触发源、强度轴量纲不齐、AC 体系无编号无分级(21a 二轮已裁定的复发缺陷)、F3 三处边界退化、`Σw = 0` 除零。裁定后 10 项 blocking + 10 项推荐 + 4 项用户裁定全部落盘,骨架未动、接缝补齐。
Prior verdict resolved: First review

---

# 评审记录 · 系统 52《随机事件导演》—— 二轮复核

| 字段 | 值 |
| ---- | ---- |
| 目标文档 | `design/gdd/random-events.md`(修订后 787 行) |
| 评审日期 | **2026-09-15** |
| 评审轮次 | **二轮**(首轮 2026-09-14 `MAJOR REVISION NEEDED`,同日修订落盘) |
| 模式 | `/design-review` **full**(六名专家并行 + Opus 高阶综合) |
| **裁决** | **NEEDS REVISION**(较首轮降一级,但**非 APPROVED**) |
| Scope Signal | **L**(F1 需第三次重写 · 6 个跨系统契约洞 · **两项新 ADR 候选**) |

## 参评专家

`game-designer`(幻想/锚点)· `systems-designer`(公式/边界值)· `qa-lead`(AC 可测性)· `level-designer`(空间/时机)· `network-programmer`(同步/权威)· `unity-specialist`(引擎/序列化)· `creative-director`(高阶综合)

## 首轮修订的成色(先给正面)

**10 项 blocking 全部兑现,且不是措辞打磨** —— DC-1…DC-4 补上了首轮 grep 零命中的三块接缝;F1 重写为档配额 + 逐条乘子;三案链移出抽池;F2 补变量表并删联机项;F3 收边界;AC 从 17 条无编号重写为 42 条带 `[B]/[A]` + `[E]/[I]/[M]` 分级。首轮的两项专家分歧(深水线归属 · 三案链移出)**均被正确遵守**,无翻案。`entities.yaml` 同轮登记七条跨系统常量。

## 必改(blocking 10 项)

| # | 发现 | 来源 |
| --- | --- | --- |
| 1 | **`ReputationMult[档]` 照样被约去 —— 修的是符号,不是实质**。F1 ② (`:438`) 在**档内**抽取,档内 `档` 恒定 ⇒ `R_opp` 对同档全部候选同乘,**与它替换掉的全池标量同构**。规则五招牌幻想「你越有名,来找你的人越多」**零机制**,而 F1 (`:468`) 写着「已修」——**虚假的已解决声明** | `game-designer` · `systems-designer` · `creative-director` |
| 2 | **`HistoryMult = Π(1 + HIST_W[flag])` 无界且与紧邻的散文自相矛盾**。`:229` 是对**全部** flag 连乘;`:234` 说「只作用于该 cause_flag 匹配的条目」⇒ AC-52-17「无关条目权重不变」不成立。20 个 flag × 1.0 ⇒ `2²⁰`,**无 clamp,int64 raw 溢出** | `systems-designer` |
| 3 | **`档占比[4]` → 配额的换算完全未定义**。窗口大小 / 配额分配规则 / 平局裁决 / 被 `ΣW_j=0` 跳过的档配额去向,四项皆缺。窗口=1 配 `{1,1,1,1}` ⇒ 各档 0 条(**欠交付**);`{5,5,5,5}` 窗口=2 ⇒ 4 > 2(**超订**)。两个实现者会确定性地分叉 | `systems-designer` · `level-designer` |
| 4 | **DC-1 的种子表达不了 F1 的加权抽取**。`EventRollSeed(t,k)` 是**逐条**的,但 `k` 是**抽中后**才知道的键;同窗口同档多次抽取复用同一个 `(t,k)`。**`argmax` 实现能通过 AC-52-06 却完全无视权重** | `systems-designer` · `network-programmer` |
| 5 | **ContextGate 的「延后」跨日不可支付**。F2/DC-3 按 `TICKS_PER_DAY` 重置预算,而 AC-52-29 要求「延后且预算不丢弃」——**没有结转字段**。玩家在医馆内跨过日边界 ⇒ 承诺落空或与新一日预算叠加成「雪崩」 | `systems-designer` · `level-designer` · `qa-lead` |
| 6 | **AC-52-26 与 AC-52-29 同为 `[B]` 且直接冲突**。F2 (`:536`) 与边界表 (`:578`) 明写「直接丢弃、**不排队、不延后**」;**延后本身就是一个队列**。两条 BLOCKING AC 不可能同时通过 | 主评审 · `qa-lead` · `game-designer` |
| 7 | **掷骰的全部输入是主机本地且不持久 —— 正是 ADR-006 要杀的那一类静默分叉**。规则九历史标记集(文档自称「52 自身的历史标记集」)· 已用预算 · 延后缓冲 · `MIN_tier` 进度缩放,全在可变导演对象里,**不在事件流中**。主机迁移 ⇒ `HistoryMult` 静默改变 ⇒ 抽出不同事件,不崩溃、回放对不上 | `network-programmer` |
| 8 | **`IEventAuthority` 是第六个抽象点,而前言「不是新裁决」为假**。ADR-005 定义的五个是 `ITickProvider`/`IEventSink`/`IIdAuthority`/`IVitalsQuery`/`SimEvent`。**`WorldSeed` 的生成/持久化在两份 ADR 中均未定义**,此处由 52 单方面断言 —— 那是 7a 拥有的跨域决定 | `network-programmer` · `unity-specialist` |
| 9 | **AC-52-34 是伪断言,不是弱断言**。`DamageRatio = 1` 时 `RepairCost = FullCost×(1−SALVAGE_RATE)` 与 `RebuildCost_mark = FullCost_mark×(1−SALVAGE_RATE)` **恒等**,而 AC 断言严格 `<`。`FullCost_mark` 与 `Σ base_cost` 的关系全文未定 | `systems-designer` · `qa-lead` |
| 10 | **六条要求零 AC 守门**:规则十自称「该判据进 AC」的承诺(`:255`)· `anchor`(被声明为 P0 必需)· P0「医馆不可被损毁」· `CONTEXT_COOLDOWN` · **`TODMult(夜,仅威胁档)`**(正是替换掉全局标量的那个机制!)· 预算按 tick 边界重置 | `qa-lead` |

## 推荐修改(12 项)

| # | 发现 | 来源 |
| --- | --- | --- |
| 11 | `StrengthTier` 的 Fix→int 舍入未指定,算例差整整一档(玩家丙 2.50 ⇒ 3 或 2) | `systems` |
| 12 | DC-2 声称旋钮写法「已改」—— **表格没改**:`:637/639/645/647/648` 仍是 `> 1` / `(0,1)` / `≈ 0` | `unity` |
| 13 | DC-2 的 SO 禁令挂错了数据集 —— 池条目 schema **零 `Fix` 字段**;真正带 `Fix` 的是旋钮表。禁令真正依据是 D-21-13,且它**静默取消了 SO 授权**(`technical-preferences.md` 已批准 Addressables) | `unity` |
| 14 | `事发区域` 无半径、`出诊路径` 在仓库中**不是已定义实体**(仅本文件两处)→ AC-52-13/15 不可测,ContextGate 无数据源 | `level` |
| 15 | `anchor` 枚举缺 聚落 / 生态区全域 成员(P1a 已列流民涌入/疫病爆发/地动);§Interactions (`:360`) 仍声称 F1 没有的「抽池的位置过滤」;无 AC 校验 anchor 归属 | `level` · 主评审 |
| 16 | Roslyn 分析器安装(NuGet-for-Unity vs analyzer-DLL)属 post-cutoff,`docs/engine-reference/unity/` **未覆盖**,需 spike;AC-52-03/41 引用的**白名单全文不存在** | `unity` · `qa` |
| 17 | AC-52-13 与状态表冲突(「避险 视事件可记入声誉」vs「声誉逐字段相等」) | `qa` |
| 18 | 52 从未声明程序集归属与 `noEngineReferences` —— 9 的 GDD 强制该 flag(「不设则零引用静默假通过」) | `unity` |
| 19 | AC-52-24 空过 —— 移除 `CarryLoad` 只把 1.14 变 1.12,档位不动。**文档自己的算例就是它声称要守的那个淹没条件** | `systems` |
| 20 | F3 对 `SalvageValue` 先舍入再相减(**双重舍入**);舍入还会击穿 `DAMAGE_MIN > 0`(`base_cost=1` ⇒ 免费维修) | `systems` |
| 21 | AC-52-02 测的是**池条目计数**而非运行期配额机制,且与文档自身池矛盾(机会 3 条 vs 威胁 2–5 条,取决于粒度) | `qa` |
| 22 | 算例用浮点写(违反 DC-2 / AC-52-08);`档占比` 之和不可整除窗口时无规则 | `network` |

## 专家分歧与裁决

| 争点 | 一方 | 另一方 | creative-director 裁决 |
| --- | --- | --- | --- |
| **强度轴/归一化算不算修好了** | `game-designer`:**修好了** —— 「你不必替我担心 #1 的归一化量级,算例已把它暴露并留了回归 AC」 | `systems-designer` + `qa-lead`:**没修** —— 问题被**命名**但守护**无牙**(AC-52-24 空过 · 无 `[0,1]` 钳位 · `CARRY_CAP ≤ 0` 不在构建期拒绝表 ⇒ 除零) | **站 systems/qa** —— **names-fixed, not enforced-fixed**。AC-52-24 须改为「每个变量移除后档位变化 ≥ 1」,`CARRY_CAP` 须进构建期拒绝 |
| **共位威胁(兵痞劫道)该怎么修** | `level-designer`:`事发区域` 半径 + 把 `出诊路径` 改写成导演侧布尔状态 | `game-designer`:`AnchorGate`(与 `KeyGate` 同形) | **两者都不够,level 更接近根因;两条都要** —— 一个定义好的路径谓词 **+** 一条生成偏移规则(威胁须出现在玩家**前方可读距离**处,而非**脚下**) |

## 承重问题(首轮遗留,本轮仍未关闭)

> **P0 的 52 是否只是「换了强度输入源的英灵神殿 raid director + 一个流浪商人」?**
>
> **答案是:大体是。** 四轴里 时机 / 密度 / 内容注入 **三轴沿用**,唯一改掉的进度锁在 **P0 = 无**(无声誉系统、无 `KeyGate` 来源)。
> 修订只在**内容**维度答了一半,而那一半挂在 **37 —— 它不存在**。
> 最差异化的锚点③(因果:「你救过一个逃犯,官兵后来上了门」)是 **P1a**。
> 锚点②(先听见后看见)**确由节奏设计兑现**,是真差异。
> **净:3 个锚点兑现 1.5 个。** 只有当 37 的内容**确实有区分度**时才可接受 —— 而这一点今天无法验证。
> **问题保持 OPEN,门控在 37。**

## ⚠️ 「52 的修订已解锁 37」不可照字面采信

37 会**对着 F1 的权重语义**撰写医疗内容 —— 而 F1 的 `ReputationMult` 被约去、`HistoryMult` 无界且自相矛盾、档配额换算未定义。
**若 37 照此落笔,而 F1 随后被修,37 的内容假设全部作废。**
**修正口径**:**「52 的 *schema 与边界* 解锁 37;52 的 *F1 公式* 不解锁 —— 37 只撰内容字段,不碰权重行为。」**

## 处理

用户裁定(2026-09-15):**现在就修** · 一次多页问答收口设计口径 · 授权更新追踪记录。

---

## 修订落盘(2026-09-15,同日)

**二轮 10 项 blocking + 12 项推荐已全部兑现;另新开 ADR-007。**

### 用户裁定(四项,均取推荐项)

| # | 争点 | 裁定 |
| --- | --- | --- |
| ① | 声誉乘子放哪 | **移进 F1 步骤①(乘在档配额上)** —— 档内抽取会把档级常量约去,必须改作用域 |
| ② | 医馆内威胁档 | **当日内延后,跳日作废** —— 解 AC-52-26 / AC-52-29 的直接冲突 |
| ③ | 共位威胁 | **半径 + 前方偏移** —— 路径谓词 **+** 生成偏移,两条都要 |
| ④ | 架构欠账 | **新开 ADR-007** —— `IEventAuthority`(第六抽象点)+ `WorldSeed` 归属 + 掷骰输入持久化 |

### blocking 逐项落点

| # | 落点 |
| --- | --- |
| 1 | **`ReputationMult[档]` 移入 F1 步骤①** —— 乘在 `档配额权重[档]` 上,不再进 `W_i`。文档正面写明「首轮修法无效(只换了作用域,没修实质)」。声誉改的是「本窗口来几条」 |
| 2 | **`HistoryMult` 改为单次查表 + 钳位** —— `clamp(1 + HIST_W[cause_flag_i], 1, HIST_MULT_MAX)`。F1 与规则九两处同步;原文的 `Π(1 + HIST_W[flag])` 连乘被显式标注为「与紧邻散文矛盾 + int64 溢出」 |
| 3 | **F1 补档配额换算 = 最大余数法(Hamilton)** —— `WINDOW_SIZE`、余数分配、**平局按档序**(威胁<机会<反应<灾难)、被 `ΣW_j=0` 跳过档的**同窗口重分配**全部定义 |
| 4 | **DC-1 重写** —— `EventRollSeed(t,k)` → `EventRollSeed(win, tier, ordinal)`(原文的 `k` 是抽中后才知道的,`argmax` 实现能通过 AC-52-06 却无视权重);补 **CDF 游走**算法(`r = S mod C`,按 `key` 升序递减)与**掷骰输入持久化表** |
| 5+6 | **规则六之三新增** —— 医馆内威胁档「当日延后、跨日作废」;`DeferredThreatSlot` 有界(`DEFER_MAX`)且须进事件流。**AC-52-26 与 AC-52-29 同步改写**,两条不再冲突 |
| 7 | **掷骰输入持久化** —— DC-1 补一张「输入 → 归属 → 持久化方式」表;新 **AC-52-46** 直接守「任意 tick 迁移主机后抽取逐位不变」 |
| 8 | **前言改正** —— 原「由 ADR-005/006 直接推出,不是新裁决」**两处为假**,已明写:`IEventAuthority` 是第六抽象点、`WorldSeed` 归属未定义;**两者 + 掷骰持久化 = ADR-007** |
| 9 | **AC-52-34 改写** —— 原文取 `DamageRatio = 1` 时 `RepairCost ≡ RebuildCost_mark`,严格 `<` 永不成立(伪断言)。改为取 `DamageRatio < 1`,并**定义 `FullCost_mark = Σ(该件全部部件.base_cost)`** 与构建期断言 |
| 10 | **六条零 AC 的要求各得 AC** —— 新增 **AC-52-42…48**:`anchor` 枚举 · P0 医馆不可损毁(构建期事实) · `CONTEXT_COOLDOWN` · `TODMult(夜,仅威胁档)` · 掷骰输入持久化 · 规则十「无人」判据 · 预算与 slot 跨日重置 |

### 推荐项逐项落点

| # | 落点 |
| --- | --- |
| 11 | `StrengthTier` 的 `Fix → int` 舍入显式命名(`ROUND_HALF_AWAY_FROM_ZERO`,**先舍入后钳位**);算例标注 2.50 → 3 |
| 12 | **Tuning Knobs「安全范围」列整数域重写** —— `> 1` / `(0,1)` / `≈ 0` 全部改写成 `num ≥ den` / `0 < num < den` / `= 0`;原文的「已改」声明为假,本轮才真改 |
| 13 | **DC-2 的 SO 禁令改挂 Tuning Knobs 表**(池条目 schema 零 `Fix` 字段);依据改为 **D-21-13**;明确 **json 经 Addressables 以 `TextAsset` 载入**(不取消 Addressables 授权) |
| 14 | **`anchor` 全枚举补全** —— `SETTLEMENT` / `BIOME_REGION`(P1a);`TRAVEL_PATH` 的谓词定义同规则六之二;**DC-4 ③ 补生成点确定性解析** |
| 15 | **§Interactions 修正** —— 「6 → 抽池的位置过滤」改为「6 → `anchor` 解析(生成点)」(F1 无位置项) |
| 16 | **Roslyn 分析器 spike 登记为 Open Question**;白名单全文缺失显式记账 |
| 17 | **AC-52-13 改写** —— 声誉字段不再是「逐字段相等」;P0 恒等、P1a 允许写入(与状态表一致) |
| 18 | **52 的 `noEngineReferences` 声明登记为 Open Question**(9 已强制,52 未声明 ⇒ 零引用是假通过) |
| 19 | **AC-52-24 改为档位级断言** —— 「移除任一变量后 `StrengthTier` 变化 ≥ 1 档」,原文的「可观测地变化」必空过 |
| 20 | **F3 变量表补 `FullCost_mark` 关系与构建期断言**;AC-52-34 同步(见 blocking #9) |
| 21 | **AC-52-02 改写** —— 断言**运行期配额**而非池条目计数比;规则十一的「1:1」点明分母 |
| 22 | **F1 算例改为整数域书写**;`WINDOW_SIZE` 与档占比不可整除的规则已定义(见 blocking #3) |

### 专家分歧的裁决落点

| 争点 | 裁决 | 落点 |
| --- | --- | --- |
| 强度轴算不算修好 | **names-fixed, not enforced-fixed** | AC-52-24 改档位级;`CARRY_CAP ≤ 0` / `SKILL_CAP ≤ 0` / `REP_CAP ≤ 0` 进构建期拒绝表 |
| 共位威胁怎么修 | **路径谓词 + 前方偏移,两条都要** | 规则六之二的布尔谓词 + DC-4 ③ 的 `SPAWN_AHEAD_DIST` 偏移规则 |

### 同轮并入

- **§承重问题(新节)** —— 把评审记录里的诚实记账写进文档:四轴逐项 + 三锚点逐项,P0 兑现 ~1.5/3;明写「P0 的 52 大体是换了强度输入源的英灵神殿 raid director」;**问题保持 OPEN,门控在 37**。含 AC-52-55。
- **§承重问题内补「解锁 37」的收窄口径** —— 「52 的 *schema 与边界* 解锁 37;F1 公式不解锁」。
- **规则三补注** —— P0 灾难档由脚本条目承担,四档「内容归属」不可为空。
- **规则五补「进度锁挂处置结果不挂诊断正确」** —— 承 `game-concept.md:269-271` + `systems-index.md:369`;`KeyGate` 只读 8 的诊断读数,不得引入「诊断正确性」字段。
- **Tuning Knobs 新增 8 旋钮** —— `WINDOW_SIZE` / `ROLL_INTERVAL` / `HIST_MULT_MAX` / `DEFER_MAX` / `REP_CAP` / `SPAWN_AHEAD_DIST` / `EVENT_PAYLOAD_MAX_BYTES` / `TOD_MULT_MAX`;并补全量构建期拒绝表(原文只兑现两条)。
- **Edge Cases 新增 7 行** —— 威胁生成点落脚下 · `anchor = BIOME_REGION` 且 P0 · 配额未消费 · 档占比不整除 · `ROLL_INTERVAL` 不整除 · `WINDOW_SIZE ≤ 0` · slot 跨日作废;并修正原有「不延后」行。
- **Dependencies 补两行** —— `IEventAuthority`(标注为第六抽象点,归 ADR-007)· `WorldSeed`(所有权归 ADR-007)。
- **Open Questions 补 5 行** —— P0 52 的定位(门控 37)· `WorldSeed` 归属 · `IEventAuthority` 接口形态 · Roslyn spike · `noEngineReferences`。
- **`entities.yaml` 同步** —— `event_roll` 的 expression 与 notes 重写(最大余数法 / 步骤① / CDF / 新种子);`tier_quota_ratio` 与 `reputation_multipliers` 约束改写;新增 `WINDOW_SIZE` / `REP_CAP` 两条常量;`rebuild_cost` 的 `FullCost_mark` 关系。

### 新建

- **`docs/architecture/adr-007-event-authority-and-roll-state.md`**(**Status: ✅ `Accepted` —— 2026-09-15 用户裁定,四项裁决均照准**)——
  ① `IEventAuthority` = 第六个 P0 抽象点 · ② `WorldSeed` 归 7a 存档头(非「一条 `SimEvent`」)·
  ③ **掷骰输入必须可从事件流重构**(核心不变量) · ④ 世界级事件用 `PatientId.None` 哨兵。
  **Blocks**:52 的代码实现。**不阻塞** 37 的内容撰写。
- `.claude/docs/technical-preferences.md` 的 Architecture Decisions Log 登记 **ADR-007**。

**未做(留待后续):**
- **AC 的夹具与实际测试仍未编写**(AC 已要求,夹具是撰写后的工作)
- ✅ **ADR-007 已于 2026-09-15 用户裁定转 `Accepted`**(四项裁决均照准)
- **Roslyn 分析器 spike 未做**(post-cutoff 依赖,`docs/engine-reference/unity/` 未覆盖)
- **IL2CPP 逐位实测未做**
- **数值全部待用户裁定**(Tuning Knobs 全表 *待定*)
- **37 病例系统仍不存在** —— 它是 P0 承重问题的唯一解码器

**结论:52 的二轮 blocking 已全部兑现,且三处「虚假已修」声明的机制已真正重写(不是换符号)。新增 ADR-007 把 52 越权断言的两个跨域决定收归正式裁决。**
**仍 In Review,不得标 Approved —— 门控项:① ~~ADR-007 转 Accepted~~ ✅ 2026-09-15 已裁定;② AC 夹具与实测;③ IL2CPP 实测;④ 承重问题门控在 37。**
**建议下一步**:启动 **37 病例系统**(52 的 schema 与边界已解锁它);或先做 **52 的 AC 夹具**。

---

## Review — 2026-09-15 — Verdict: NEEDS REVISION
Scope signal: L
Specialists: game-designer · systems-designer · qa-lead · level-designer · network-programmer · unity-specialist · creative-director(高阶综合)
Blocking items: 10 | Recommended: 12
Summary: 首轮的 10 项 blocking 确实全部兑现、非措辞打磨(DC-1…DC-4 补齐 · F1/F2/F3 重写 · AC 从无编号到 42 条分级),首轮两项专家分歧(深水线归属 · 三案链移出)均被正确遵守。**但修订在它声称修好的机制里引入了新一类缺陷**:三处「已修」是虚假的已解决声明 —— ① `ReputationMult[档]` 因档内抽取被照样约去(规则五招牌幻想零机制);② `HistoryMult` 是对全部 flag 连乘、无界且与紧邻散文矛盾(AC-52-17 不成立);③ `档占比`→配额换算完全未定义(欠交付与超订皆可达)。另:DC-1 的逐条种子表达不了加权抽取(argmax 能通过 AC-52-06)、ContextGate 延后跨日不可支付、AC-52-26 与 AC-52-29 两条 BLOCKING 直接冲突、掷骰输入不持久(主机迁移静默分叉)、`IEventAuthority` 是第六抽象点而 `WorldSeed` 归属从未定义、AC-52-34 为伪断言、六条要求零 AC 守门。承重问题「P0 的 52 是否只是换了强度输入源的英灵神殿 raid director」**仍为 OPEN,门控在 37**;「52 的修订已解锁 37」须收窄为「解锁的是 schema 与边界,不是 F1 公式」。
Prior verdict resolved: **Partial** —— 10 项 blocking 全部落盘且方向正确,但 3 项(ReputationMult/乘子语义 · AC 可测性 · 档配额机制)以新形态复发。
