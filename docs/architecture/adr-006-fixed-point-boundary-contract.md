# ADR-006: 定点域边界数据契约(及 ADR-005 两处修正)

## Status

Accepted

## Date

2026-09-14

## Last Verified

2026-09-14(同日二轮复核修正,就地改 §Decision 一 / 二 / 四,新增 §Decision 五)

## Decision Makers

dr_guyang(用户裁定④:新立本 ADR)· technical-director(起草与裁决)
· systems-designer(21a 公式侧)· network-programmer(D-9-D / D-9-E)
· 系统 21a 物品与配方数据库 GDD 作者 · 系统 9 疾病与伤情模拟 GDD 作者

## Summary

ADR-005 把**全部模拟数学**锁进整数定点域(int64 / Q16.16),却**没有定义这个域的边界**:
外部数据文件怎么把数交给 `Fix`、存档里允不允许出现 `float`、舍入怎么定、
以及被 ADR-005 自己引用的 `SimEvent` 全序键 `Seq` 到底是什么。
本 ADR 补齐 **21a ↔ 9 之间的定点数据契约**(整数出参 · 禁浮点存档 · 单一舍入模式 ·
守恒律的域内表达 · `Fix` 的 Unity 序列化边界),并以两处修正案(Amendment A / B)回填 ADR-005 的形状缺口。
**不落盘此契约,21a 的 `drug_profile` 一旦被 11 处方按浮点读走,定点域即从边界处漏空。**

> **2026-09-14 二轮复核修正(ADR-006 自身的三处口径错误)**:① `weight` 是 `int` 计数,
> **不属于 `Fix` 解析集**(原 §Decision 一 误列);② **`Fix` 不可经 Unity 内置序列化器承载**
> —— 原 §Decision 二 断言「`Fix` 序列化为内部 `long`」**为假**,须自定义编码器(新 §Decision 五);
> ③ 守恒律 `Σ outputs ≤ Σ inputs × EFF_MAX` **量纲不齐**,须乘 `weight` 归一(§Decision 四)。
> 三处均为**静默**失败模式,由 21a 的 AC-21a-41 / 51 / 53 与 AC-21a-39 守住。

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS (URP) |
| **Domain** | Core(数据契约 / 序列化 / 定点) |
| **Knowledge Risk** | **MEDIUM** —— 契约本身只用 `long` / 整数运算与 C# 语言特性,**不依赖任何 post-cutoff API**;风险集中在 IL2CPP 对整数的转译保证 |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md` · `docs/architecture/adr-005-deterministic-sim.md` |
| **Post-Cutoff APIs Used** | **None** |
| **Verification Required** | **IL2CPP 与 Mono 的整数逐位一致性实测**(与 ADR-005 同一项)。本 ADR 的 Validation Criteria 建在该实测之上 —— 实测未过则本契约**只保证同编译器内一致**,须重新评估 |

> **Note**: Knowledge Risk MEDIUM ⇒ 项目升级引擎版本时须重读本 ADR。

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | **ADR-005**(Accepted 2026-09-13)—— 本 ADR 是其边界的补完;ADR-005 未 Accepted 则本 ADR 无意义 |
| **Enables** | 21a 物品与配方数据库进 Implement · 11 处方用药 GDD · 18 炮制 / 19 制作 · 7a 持久化服务 |
| **Blocks** | **21a 不得标 Approved**(门控项见其 §Acceptance Criteria 末注);**系统 9 的 D-9-D / D-9-E 已由本 ADR 兑现**(2026-09-14),其 Implement 门**已解除** |
| **Ordering Note** | 本 ADR **修正** ADR-005 的 `SimEvent` 与 `patient_id` 两处 —— 属**窄修正**,不推翻 ADR-005 的两项核心裁决(整数定点域 / 事件流唯一真源)。ADR-005 保留 Accepted 状态,正文加前向指针 |

## Context

### Problem Statement

ADR-005 规定「全部模拟数学在整数定点域」「Storage 中不出现任何 float」,
但它**只规定了域内怎么算,没规定域边界怎么进**。21a 是全案唯一的零工作流依赖系统,
它的 `drug_profile` 里住着 `Offset` 与 `τ_half` —— 这两个量**直接是 9 的 F1 求值参数**。
如果 21a 的数据文件里写的是 `0.5f`,那么:

1. 解析那一刻浮点已进入内存;
2. 11 处方把它当 `float` 读走、算完再塞回 `Offset`;
3. 定点域**从边界处漏空**,而域内的所有纪律(E1/E2 守护、`Fix` 无隐式转换)全部照常通过。

**这是 ADR-005 的验证判据拦不住的一类泄漏** —— 因为它不发生在域内。

### Current State

- ADR-005 `:174-178` 定义五个抽象点,`:181-186` 定义 `SimEvent{Tick, Patient, Kind}`。
- ADR-005 `:192` 要求「`SimEvent` 必须可全序(`(Tick, Patient, Seq)`)」——
  **但 `SimEvent` 里没有 `Seq` 字段**。ADR-005 **自相矛盾**。
- ADR-005 `:126-127` 规定 `patient_id` 由主机单调计数器分配、「id 随流持久化」,
  但**未规定计数器本身在主机迁移时如何存活**。
- 21a 侧已在 GDD 中声明整数边界(D-21-9)与守恒律(D-21-10),
  **但 21a 无权替 ADR 落盘契约** —— 它是消费者,不是权威。

### Constraints

- **不得引入新依赖**(`.claude/docs/technical-preferences.md` 的 Allowed Libraries 为空,
  第三方定点库须另立 ADR)。
- **不得要求 Unity 侧改动序列化格式** —— 7a 尚未撰写,契约必须在 7a 动工前定死。
- **C# 语言约束**:`Fix` 是 `readonly struct`,内部 `long`;
  任何把 `float` 隐式转进 `Fix` 的运算符都是**契约违反**(ADR-005 已声明「刻意不定义 implicit operator float」)。

### Requirements

- 外部数据文件 → `Fix` 的解析路径**必须有唯一入口**,且入口拒绝浮点字面量。
- **计数型字段(`weight` / `stack_max`)不得混入 `Fix` 解析集** —— 它们是 `int`。
- 存档与事件流中**不得出现任何 `float` / `double` 序列化字段**。
- **`Fix` 不得依赖 Unity 内置序列化器承载** —— 须走自定义编码器,且该禁令有可执行的探针。
- 舍入**必须是单一、显式、可复现的模式**,且不得依赖 `Math.Round` 的默认行为。
- 守恒律(`Σ(weight × outputs) ≤ EFF_MAX × Σ(weight × inputs)`)必须在域内有可校验的表达,**且两侧同量纲**。
- `SimEvent` 必须与 ADR-005 `:192` 自己的全序要求自洽。
- `patient_id` 必须在主机迁移前后**稳定**(否则 `patient_seed` 变 ⇒ 病程被改写)。

## Decision

### 一、解析边界:唯一入口,拒绝浮点

**所有外部数据 → `Fix` 的转换只经一个 API**:

```csharp
public static class FixParse
{
    // 唯一的字符串 → Fix 入口。不接受小数点后的非二进制分数。
    public static Fix Parse(string literal, RoundMode mode = RoundMode.HalfAwayFromZero);

    // 整数 + 分母形式(唯一推荐的作者写法)
    public static Fix FromRatio(long numerator, long denominator);

    // 刻意不提供:Parse(float) / implicit operator Fix(float) / (Fix)0.5
}
```

- 数据文件里 `Offset` / `τ_half` / `axis_offset_by_quality[]` **写作整数字面量或 `分子/分母`**,
  由 `FixParse` 在**导入期**一次性转成 Q16.16 内部 `long`;
  > ⚠️ **2026-09-14 二轮修正(D-21-17)**:`weight` **已移出本解析集**。它的口径是
  > **`int`(最小单位个数)**,不是 `Fix` —— 原稿把 `weight` 列入 `Fix` 解析集是**类型口径错误**,
  > 会把一个整数计数塞进定点域(ADR-006 后文亦需据此修正,见 §Decision 五)。
- **浮点字面量在 schema 层即被拒**(21a 的 AC-21a-41 是这条的落盘验证);
- 运行期**不存在**从 `float` 到 `Fix` 的路径 —— 编译期就不可表达。

> **为什么是「导入期转换」而不是「运行期存字符串」**:运行期存字符串会让
> 「读一个数」带上解析失败分支,而解析失败在 9 的 `Step` 里**无处返回错误**。
> 把转换推到导入期,失败就是**构建失败**,不是运行期未定义行为。

### 二、存储边界:存档与事件流禁 float

- **存档中不出现任何 `float` / `double`**:`Fix` 的**唯一合法落盘形状是其内部 `long`**(Q16.16 的 raw bits),**但该 `long` 必须由自定义编码器显式写出/读入 —— 不得依赖 Unity 内置序列化器**(见 §Decision 五);
- `drug_profile.Offset` / `half_life` / `elimination` / `onset` / `peak` **全部是 `Fix`**;
- **`weight` / `stack_max` 不是 `Fix`,是 `int`** —— 它们是**计数**(最小单位个数 / 上限),不参与定点算术,只参与守恒律的整数比较(见 §Decision 四);
- **`Fix` 的唯一浮点出口是 `IVitalsQuery.GetVitals()` 返回的 `VitalsDto`**
  (ADR-005 `:178` / `:156`),且该出口**只出不进** —— 浮点回到 sim 层无路径。

### 三、舍入:单一模式,显式命名

- 全部舍入使用 **`ROUND_HALF_AWAY_FROM_ZERO`**;
- **禁用 `Math.Round` 的默认行为**(默认 ties-to-even,即 `Round(2.5) = 2`)——
  它是**静默的行为依赖**,且 .NET 版本间曾有过变化;
- 舍入在**整数域内**完成(Q32.32 中间 → Q16.16 存储时截断/舍入一次),不借道浮点;
- `RoundMode` 是**全局常量**,不是逐调用点参数 —— 逐调用点会让两处公式不一致而无人察觉。

### 四、守恒律的域内表达

- **`Σ(weight × outputs) ≤ EFF_MAX × Σ(weight × inputs)`,且 `EFF_MAX ≤ 1`**;
  > ⚠️ **2026-09-14 二轮修正(D-21-19③)**:原稿写法 `Σ outputs ≤ Σ inputs × EFF_MAX`
  > **量纲不齐** —— `outputs` / `inputs` 是**条目**(不同物品重量不同),
  > 直接相加等于「一粒丹 = 一斤药材」。**必须乘 `weight` 归一到同一量纲**(重量单位),
  > 且 `weight` 是 `int`(§Decision 二),乘积走整数域。
- 运行期投入端另按 `ActualConsumed_i = Ceil( inputs_i.qty / EFF )` 记实耗(D-21-15),
  守恒律的 `inputs` 项取 **`ActualConsumed`** 而非配方基数 —— 否则「技能高 = 省料」这一
  **回报**在守恒检查里看不见,守恒律会退化为对静态数据的空转校验;
- `EFF_MAX > 1` ⇒ **构建期硬失败**(它是「凭空造物」的边界,不是平衡旋钮);
- 该式在**整数域内**求值(乘法走 Q32.32 中间或纯 `long` 乘,比较在整数上做),
  **不用浮点比较** —— 浮点比较在临界点上会因舍入给出不可复现的 `true/false`。

### 五、Unity 序列化边界:`Fix` 不可经内置序列化器

> **2026-09-14 二轮新增(D-21-18)。** 原稿 `:124` 断言「`Fix` 序列化为内部 `long`」,
> 这个断言**不成立** —— 它把「内部表示是 `long`」误当成了「Unity 会序列化该 `long`」。

`Fix` 的定义(ADR-005)是:

```csharp
public readonly struct Fix
{
    private readonly long _raw;   // 无 [SerializeField],且是 private readonly
}
```

Unity 内置序列化器(`JsonUtility` / `[SerializeField]` / `ScriptableObject` / prefab)
对 `Fix` 字段的行为:

1. **`readonly struct` + 无 `[Serializable]`** ⇒ 若无显式标注,序列化器不识别其字段;
2. **`private readonly long _raw`** ⇒ 即便加了 `[SerializeField]`,`readonly` 字段在**反序列化
   赋值**上受限(Unity 在较新版本对 `readonly` 的支持**属 post-cutoff 知识,须实测**);
3. **失败是静默的** —— 不会抛异常,而是把该字段**当作零值 / 跳过**。

**因此契约是**:

- **数据文件**(JSON)与**存档 / 事件流**中承载 `Fix` 的字段,**必须走自定义编码器**:
  把 `_raw` 的 `long` 显式写出,读入时显式构造 `Fix` 或直接写 `_raw`(见 7a 的实现);
- **禁止**把任何 `Fix` 字段交给 Unity 内置序列化器(禁 `JsonUtility` 直序列化含 `Fix` 的 DTO、
  禁把 `Fix` 放进 `ScriptableObject` / prefab 字段);
- 该禁令**由一条 EditMode 序列化探针守住**(21a AC-21a-53):探针断言「内置序列化器往返
  一个 `Fix` 字段 ⇒ 值丢失」,从而**证明自定义编码器的必需性**,并把该失败模式钉死在 CI 里。

> **为什么这条是必要的**:失败模式是**药物效力被静默清零** ——
> 不崩溃、不报错、回放也不失配(全零全零仍然一致),**只表现为「这剂药没效」**。
> 与 Amendment B 的 `patient_id` 漏洞同属「静默的正确性漏洞」一类。

### Amendment A —— 修正 ADR-005 `SimEvent`(兑现 **D-9-D**)

ADR-005 `:192` 要求按 `(Tick, Patient, Seq)` 全序,`:181-186` 却未定义 `Seq` 与载荷。
**修正为**(与 9 侧 `:870` 的形状一致):

```csharp
public readonly struct SimEvent
{
    public readonly long         Tick;
    public readonly PatientId    Patient;
    public readonly long         Seq;        // ← 新增:同一 (Tick, Patient) 内的单调序号
    public readonly EventKind    Kind;
    public readonly EventPayload Payload;    // ← 新增:病种_id / 处置_id / 施予者 / polarity / Offset / τ_half
}
```

- `Seq` 由**主机在 `Append` 时分配**,同一 `(Tick, Patient)` 内从 0 单调递增;
- `Seq` **必须随事件持久化**(否则回放无法重建顺序);
- `Payload` 是**值 struct**,无引用字段 —— 与 `Fix` 同纪律;
- **此修正不改变 ADR-005 的核心裁决**,只补其自相矛盾的形状。

### Amendment B —— `patient_id` 跨权威稳定性(兑现 **D-9-E**)✅ **已裁决**

**问题**:`patient_seed = hash(world_seed, patient_id)`(`disease-simulation.md:143`)。
**因此 `patient_id` 一旦在权威迁移后改变,`patient_seed` 必变 ⇒ `Noise(t)` 跳变 ⇒
该病人的全部病程被改写。** `PatientId` **必须在主机迁移前后逐位稳定** —— 硬不变量。

**裁决(用户,2026-09-14):采纳机制 A —— 计数器 + 高水位可重构。** 三条硬不变量:

1. **计数器永不复位为 0**;
2. **任何权威变更后,`next = max(全部已知 patient_id) + 1`** —— 由事件流**重构**,
   **不作独立快照**(少一份可失步的状态 —— 见 Alternative 4);
3. **终态折叠行必须保留 `patient_id`**。

> ⚠️ **第 3 条是本轮复核新发现的漏洞,不是补丁而是必需项。**
> ADR-005 `:197-198` 原折叠元组 `(onset, 病种_id, patient_seed, outcome, t_end)`
> **不含 `patient_id`**,而折叠**丢弃流位置**,`patient_seed` 又是**单向哈希**。
> 于是「所有高 id 病人都已折叠」之后,`max(patient_id)` **不可重构** ⇒
> 新主机重建时必然回退 ⇒ **id 复用 ⇒ 两个病人共用一个 `patient_seed` ⇒ 病程互写**。
> 这是**静默的正确性漏洞**,回放检测不到(全序仍然逐位一致,只是语义已错)。
> **ADR-005 已就地修正为 `(onset, 病种_id, patient_id, patient_seed, outcome, t_end)`。**

**被否决的机制 B —— 纯哈希派生**(9 的 `:887` 提案):
`patient_id = hash(world_seed, 出生上下文)` 表面无状态、迁移天然安全,
但**「出生上下文」必须唯一**才能避免碰撞;而任何能让它唯一的定义
(同 tick / 同病因 / 同地点下的第 n 个病人)**都含一个序号** ——
于是又变回计数器,只是把计数器藏进了哈希的输入里。**它没有消除状态,只是让状态更难看见。**
除此之外:哈希 id **无序**,调试与流压缩都要额外索引。**否决。**

**同步**:9 的正文自身曾有矛盾 —— `:868` 表写「主机单调计数器」、`:887` 散文写「hash 派生」。
**两处已统一为机制 A。**

## Alternatives Considered

### Alternative 1: 允许数据文件写浮点,域内再转

**否决。** 这正是本 ADR 要堵的漏。浮点一旦进入解析路径,
「域内无浮点」的静态断言**全部照常通过**,而泄漏已经发生 ——
验证判据与真实风险错位,是最坏的一类防护。

### Alternative 2: 存档存字符串,运行期解析

**否决。** 见 §Decision 一:把解析失败推到 run-time,而 `Step` 无错误返回通道。
导入期失败 = 构建失败,是唯一能在 CI 里拦住的时机。

### Alternative 3: 引入成熟的第三方定点 / 数学库

**否决(本轮)。** `.claude/docs/technical-preferences.md` 的 Allowed Libraries 为空,
引入须另立 ADR;且 ADR-005 已刻意选择「不依赖任何 post-cutoff API」的最小面。
**但 `Fix` 的缺项(定点 `exp`)是真实成本** —— 9 的 F0 已登记 `Exp` 需手写 250–400 行。
若该成本超出预期,**重新打开本 Alternative**。

### Alternative 4(针对 Amendment B):保留计数器,但迁移时按 `max(id)+1` 重建而非快照

**✅ 已采纳 —— 这就是机制 A 的落地实现。** 它把「快照计数器」这条额外纪律
**化为可由事件流自身推导的幂等运算**,比「记得存计数器」更抗遗忘:
快照会失步(两份状态就有两种真相),而 `max(id)+1` 是**从唯一真源算出来的纯函数**。
**前置条件**:折叠行必须保留 `patient_id`(Amendment B 第 3 条),否则该纯函数无输入。

## Consequences

### Positive

- 定点域的**边界**与内部同等受约束 —— 之前是「域内严格、边界敞开」。
- 21a 与 9 之间的 `Offset` / `τ_half` 有了**唯一、可校验**的数据形状。
- ADR-005 的两处自相矛盾(`Seq` 未定义、`patient_id` 迁移未定)**均已兑现**。
- **Amendment B 顺带补上了一个静默的正确性漏洞** —— 终态折叠丢掉 `patient_id`
  会让高水位不可重构,进而在权威迁移后**静默复用 id、改写病程**。
  该漏洞不表现为崩溃或回放不一致,只表现为「某个病人的病程悄悄变了」,
  **若无本次复核,大概率到联机测试后期才会被发现,届时已无从归因**。
- 舍入模式成为**全局常量**,消掉了一类「两处公式各自舍入」的隐性分歧。

### Negative

### Negative

- **`FixParse` 成为全案单点** —— 它错,所有数值全错。需要它自己的负向夹具。
- 作者写数据文件时**不能顺手写 `0.5`** —— 内容作者的摩擦增加了。
  缓解:提供 `FromRatio(1, 2)` 这类写法,把摩擦变成方言而不是障碍。
- **含 `Fix` 的 DTO 不能直接交给 Unity 序列化**(§Decision 五)—— 7a 必须为每个含 `Fix` 的
  持久化结构各写一段编解码;这是**真实的实现成本**,换来的是消掉「药物效力静默归零」。
- **Amendment B 已结** —— 机制 A 的三条不变量须由 7a 与 45 网络层**同时**遵守;
  少实现一条(尤其第 3 条折叠保留 `patient_id`)即静默失效,且**回放检测不到**。

### Neutral

- 不改变 `Fix` 的内部表示(Q16.16 / int64 仍归 ADR-005)。
- 不改变五个抽象点的数量与职责。

## Risks

| 风险 | 概率 | 影响 | 缓解 |
| --- | --- | --- | --- |
| `FixParse` 有实现 bug,错误静默通过导入 | 中 | **高**(全案数值) | 负向夹具(AC-21a-41 / 42)+ 与 `FromRatio` 交叉验算 |
| **`Fix` 被交给 Unity 内置序列化器**,字段静默归零 | **中** | **高**(药物效力消失) | §Decision 五 + EditMode 探针(AC-21a-53);**不崩溃、不回放失配,只能靠探针发现** |
| **守恒律两侧量纲不齐**(漏乘 `weight`),校验通过但语义为空 | 中 | 中 | §Decision 四 的 weight 归一 + AC-21a-39 |
| 内容作者绕过 `FixParse`,直接在代码里写字面量 | 中 | 中 | 21a 的 AC-21a-48(No hardcoded values)已覆盖 |
| IL2CPP 与 Mono 整数行为不一致 | **低** | **高** | ADR-005 已有的实测项;本 ADR 的 Validation Criteria 依赖它 |
| **折叠规则漏实现 `patient_id`**(Amendment B 第 3 条),高水位静默回退 | 中 | **高** | 只表现为「病程悄悄变了」,不崩溃、不回放失配 ⇒ **必须由 AC 显式守住**(ADR-005 Validation Criteria 末条),不能指望调试发现 |

## Performance Implications

- `FixParse` 只在**导入期**运行,不进入运行期热路径 —— **零运行期成本**。
- `Fix` 的算术成本与 ADR-005 相同(整数运算),本 ADR **不新增**任何运行期开销。
- 事件流因 `Seq` + `Payload` 而变宽:**每条事件多 8 字节 + 一个值 struct 的宽度**。
  这与 ADR-005 的「事件流无界增长」风险叠加 —— 由终态折叠(ADR-005 `:197-198`)承担,
  **不改变折叠策略**。

## Migration Plan

1. **本 ADR Accepted 后**,21a 侧无需改动(其 GDD 已声明 D-21-9 / D-21-10,本 ADR 是其落盘处)。
2. **ADR-005 加前向指针**指向本 ADR 的 Amendment A / B,不重写 ADR-005 正文。
3. **7a 持久化服务撰写时**,`Fix` 的序列化形状以本 ADR **§Decision 五**为准 ——
   **必须自定义编码器**,禁交内置序列化器(§Decision 二 的旧断言已废,勿照旧稿实现)。
4. **Amendment B 已裁决为机制 A** —— ADR-005 `:126-127` 与折叠规则、以及 9 的 `:868` / `:887`
   **三处已同步为同一机制**(2026-09-14)。7a 与 45 实现时须遵守三条不变量。
5. **11 处方用药撰写时**,`Offset` 的读写必须以 `Fix` 为类型,不得自行转换。
6. **21a 的数据 schema 撰写时**,`weight` / `stack_max` 一律为 `int`,**不得**与 `Fix` 字段混排在同一解析路径(§Decision 一 / 二)。

## Validation Criteria

- [ ] 静态扫描:`sim` 程序集与全部数据 schema 中**无 `float` / `double` 字段**(ADR-005 判据的边界延伸)。
- [ ] `FixParse` 的负向夹具:浮点字面量 ⇒ **导入期硬失败**。
- [ ] **`weight` / `stack_max` 的 schema 类型为 `int`,不在 `Fix` 解析集内**(D-21-17)。
- [ ] `ROUND_HALF_AWAY_FROM_ZERO` 的边界用例:`Round(±0.5)` / `Round(±1.5)` / `Round(±2.5)` 全部符合预期,
      且与 `Math.Round` 默认行为**可区分**(证明未误用)。
- [ ] **守恒律两侧同量纲**:`Σ(weight × outputs) ≤ EFF_MAX × Σ(weight × ActualConsumed)`,
      在整数域求值(D-21-19③)。
- [ ] **`Fix` 不被 Unity 内置序列化器承载**(D-21-18):一条 EditMode 探针断言
      `JsonUtility` / `[SerializeField]` / `ScriptableObject` 往返一个 `Fix` 字段 ⇒ **值丢失**,
      并断言自定义编码器往返 ⇒ **逐位还原**。
- [ ] `EFF_MAX > 1` ⇒ 构建期硬失败。
- [ ] `SimEvent` 含 `Seq` 与 `Payload`,且 `Seq` 随事件持久化(Amendment A)。
- [ ] **`patient_id` 迁移稳定性**(Amendment B):含已折叠终态病人的流,重构出的
      `next = max(patient_id) + 1` **严格大于所有出现过的 id**;全流回放后
      `patient_id → patient_seed` 映射**逐位一致**(证明无 id 复用)。
- [ ] **终态折叠行含 `patient_id`**(Amendment B 第 3 条的落盘验证)。
- [ ] **IL2CPP 与 Mono 逐位一致性实测通过**(与 ADR-005 同一项,阻塞级)。

## GDD Requirements Addressed

| GDD | 系统 | 需求 | 本 ADR 如何满足 |
| --- | --- | --- | --- |
| `design/gdd/item-database.md` | 21a 物品与配方数据库 | D-21-9:F1/F2/F4 出参即 `int`,存档无 `float`,`Offset`/`τ_half` 为 Q16.16 整数字面量 | §Decision 一 / 二 / 三 |
| `design/gdd/item-database.md` | 21a 物品与配方数据库 | D-21-10:守恒律 `Σ(weight × outputs) ≤ EFF_MAX × Σ(weight × inputs)` 且 `EFF_MAX ≤ 1` | §Decision 四 |
| `design/gdd/item-database.md` | 21a 物品与配方数据库 | D-21-13:`processing_state` 跨持久化只用稳定字符串名 | §Decision 二(存档中不出现数值编码的枚举) |
| `design/gdd/item-database.md` | 21a 物品与配方数据库 | **D-21-17:`weight` / `stack_max` 口径 = `int`(最小单位个数),**不是 `Fix`** | §Decision 一 / 二(移出 `Fix` 解析集) |
| `design/gdd/item-database.md` | 21a 物品与配方数据库 | **D-21-18:`Fix` 不可经 Unity 序列化器承载,须自定义编码器** | §Decision 五(新增) |
| `design/gdd/item-database.md` | 21a 物品与配方数据库 | **D-21-15:技能回报 = 投入端可变,`ActualConsumed = Ceil(base / EFF)`** | §Decision 四(守恒律取 `ActualConsumed` 而非基数) |
| `design/gdd/disease-simulation.md` | 9 疾病与伤情模拟 | `SimEvent` 必须可全序且带载荷(`:870`) | Amendment A(D-9-D) |
| `design/gdd/disease-simulation.md` | 9 疾病与伤情模拟 | `patient_seed = hash(world_seed, patient_id)` ⇒ `PatientId` 迁移稳定(`:143` / `:887`) | Amendment B(D-9-E,✅ **已裁决 —— 机制 A + 折叠保留 `patient_id`**) |

## Related

- **ADR-005 确定性模拟与状态同步模型** —— 本 ADR 是其边界的补完,并窄修正其 `SimEvent` 与 `patient_id` 两处。
- `design/gdd/item-database.md`(21a)—— 本契约的第一个消费者,§Schema 与 §Tuning Knobs 受其约束。
- `design/gdd/disease-simulation.md`(9)—— Amendment A / B 的受影响方。
- `design/registry/entities.yaml` —— 常数与公式的等价性登记处。
