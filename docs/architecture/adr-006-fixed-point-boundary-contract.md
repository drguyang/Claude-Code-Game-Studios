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
守恒律的域内表达 · `Fix` 的 Unity 序列化边界),并以修正案(Amendment A–F)回填 ADR-005 的形状与口径缺口 —— A–D 由本 ADR 引入,**Amendment E 由 ADR-009 引入**(升格 C / D 至三流口径),**Amendment F 由系统 1 的 GDD 引入**(2026-09-16,切分「算术域」与「边界域」的适用对象)。
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
| **Ordering Note** | 本 ADR **修正** ADR-005 的 `SimEvent` / `patient_id` / `Seq` 发放域 / 真源口径四处(Amendment A / B / C / D)—— 属**窄修正**,不推翻 ADR-005 的两项核心裁决(整数定点域 / 事件流唯一真源)。ADR-005 保留 Accepted 状态,正文加前向指针。**2026-09-15 起为五道修正案**(A–E,Amendment E 由 ADR-009 引入,升格 C / D 至三流口径) |

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

- ADR-005 §Key Interfaces 定义五个抽象点与该处的 `SimEvent{Tick, Patient, Kind}`。
- ADR-005 §Implementation Guidelines 2 要求「`SimEvent` 必须可全序(`(Tick, Patient, Seq)`)」——
  **但 `SimEvent` 里没有 `Seq` 字段**。ADR-005 **自相矛盾**。
- ADR-005 §Decision 三 规定 `patient_id` 由主机单调计数器分配、「id 随流持久化」,
  但**未规定计数器本身在主机迁移时如何存活**。
- 21a 侧已在 GDD 中声明整数边界(D-21-9)与守恒律(D-21-10),
  **但 21a 无权替 ADR 落盘契约** —— 它是消费者,不是权威。

### Constraints

- **不得引入新依赖**(`.claude/docs/technical-preferences.md` 的 Allowed Libraries 为空,
  第三方定点库须另立 ADR)。
- **不得要求 Unity 侧改动序列化格式** —— 7a 尚未撰写,契约必须在 7a 动工前定死。
  > ⚠️ **2026-09-21 历史注**:「7a 尚未撰写」是本 ADR 撰写时(2026-09-14)的状态,现陈旧 ——
  > 7a 已有 `persistence-service.md`(2026-09-17 落盘)。本约束是**当时的时序理由**,不删,按惯例追加注补。
- **C# 语言约束**:`Fix` 是 `readonly struct`,内部 `long`;
  任何把 `float` 隐式转进 `Fix` 的运算符都是**契约违反**(ADR-005 已声明「刻意不定义 implicit operator float」)。

### Requirements

- 外部数据文件 → `Fix` 的解析路径**必须有唯一入口**,且入口拒绝浮点字面量。
- **计数型字段(`weight` / `stack_max`)不得混入 `Fix` 解析集** —— 它们是 `int`。
- 存档与事件流中**不得出现任何 `float` / `double` 序列化字段**。
- **`Fix` 不得依赖 Unity 内置序列化器承载** —— 须走自定义编码器,且该禁令有可执行的探针。
- 舍入**必须是单一、显式、可复现的模式**,且不得依赖 `Math.Round` 的默认行为。
- 守恒律(`Σ(weight × outputs) ≤ EFF_MAX × Σ(weight × inputs)`)必须在域内有可校验的表达,**且两侧同量纲**。
- `SimEvent` 必须与 ADR-005 §Implementation Guidelines 2 自己的全序要求自洽。
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
  (ADR-005 §Decision 二 与 §Key Interfaces),且该出口**只出不进** —— 浮点回到 sim 层无路径。

#### D-21-13 口径收窄:载荷内枚举 = 版本化整数 ordinal(2026-09-17)

> **原口径**(21a 物品库,2026-09-14)写作「**存档中不出现数值编码的枚举**」,承
> `processing_state` 跨持久化**只用稳定字符串名**。该口径**过宽**,与 ADR-008 的
> `DiseaseIdSet` / `Judgment.lexicon_id` / 21a 的 `ItemId` **直接冲突** —— 后者全部是
> **整数 ordinal**,若按字面收口,等于要求每个物品 id / 病种 id 都改写成字符串,而
> **字符串在事件流里既非定长、也无法参与定点域外的整数比较**。
>
> **收窄后的口径(用户 2026-09-17 裁定「① ordinal + 进 `ConfigVersion`」)—— 两条并行,**按位置分野**:
>
> | 位置 | 形状 | 例 |
> | --- | --- | --- |
> | **事件载荷内的枚举** | **版本化整数 ordinal**(`u16` / `u8`) | `DiseaseIdSet` 的成员 · `Judgment.lexicon_id` · `ItemId` · `SimEvent.Kind` |
> | **跨持久化的配置态枚举** | **稳定字符串名**(原 D-21-13 口径,**保留**) | `processing_state`(21a)—— 它是**配置项**,不是载荷 |
>
> **「版本化」是承重条件,不是修饰词**:ordinal ↔ 名称的映射表**必须进
> `ConfigVersion` 的内容哈希覆盖集**。表一变 ⇒ `ConfigVersion` 变。
> - **🔴 但 `ConfigVersion` 不匹配不是拒载**(ADR-010 §七 / ADR-014 §五:非致命)——
>   故「进覆盖集」**单独不足以**守住 ordinal 重排。**真正的门是 append-only**
>   (见 `adr-014 §五 D-21-13 承接条 ③`:既有条目号**永不重用、永不改义** +
>   构建期对 `_ordinal_baseline.json` 基线断言 ⇒ 改号 / 改义 = **构建期硬失败**)。
> - **分工**:`ConfigVersion` 负责「**改动可追溯**」(排查回放不符);
>   **append-only 构建门**负责「**买断静默错读**」——
>   把「`麻黄汤` 被读成 `桂枝汤`」这类**静默错读**换成**明确的构建失败**。
>   两者缺一不可,**且不能互相替代**。
>
> **不适用于**:`Fix` 字段(仍禁浮点字面量,仍走 `FixParse`)· `weight` / `stack_max`
> (仍是 `int` 计数,见上)· 任何**玩家可见文本**(走本地化词表,非 ordinal)。

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
- **自定义编码器按字段名编码,不得按字段位置编码** ——
  `SimEvent` 一类值 struct 的字段序在历次修正案中变更过(且各 ADR 的抄本曾不一致,
  如 ADR-007 把它写成 `{Tick, Patient, Kind, Seq, Payload}`、Amendment A 则为
  `{Tick, Patient, Seq, Kind, Payload}`)。位置化编码会让**同一 struct 的两种抄本写出两种字节流**,
  且失败是静默的(长度对得上,值错位)。**2026-09-15 复查轮 N-8 补。**
- **禁止**把任何 `Fix` 字段交给 Unity 内置序列化器(禁 `JsonUtility` 直序列化含 `Fix` 的 DTO、
  禁把 `Fix` 放进 `ScriptableObject` / prefab 字段);
- 该禁令**由一条 EditMode 序列化探针守住**(21a AC-21a-53):探针断言「内置序列化器往返
  一个 `Fix` 字段 ⇒ 值丢失」,从而**证明自定义编码器的必需性**,并把该失败模式钉死在 CI 里。

> **为什么这条是必要的**:失败模式是**药物效力被静默清零** ——
> 不崩溃、不报错、回放也不失配(全零全零仍然一致),**只表现为「这剂药没效」**。
> 与 Amendment B 的 `patient_id` 漏洞同属「静默的正确性漏洞」一类。

### Amendment A —— 修正 ADR-005 `SimEvent`(兑现 **D-9-D**)

ADR-005 §Implementation Guidelines 2 要求按 `(Tick, Patient, Seq)` 全序,
但该 ADR 的 Key Interfaces 块却未定义 `Seq` 与载荷。
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
  > **⚠️ 2026-09-17 例外登记(唯一一条)**:`Judgment.freehand_text`(ADR-008 §三
  > 的 `Judgment` 形状)是 `string`,**破此纪律**。理由:它是**玩家自书的自由文本**
  > —— 语义上**不可能**是定长值,且**永不进任何判定**(ADR-008 明写「只进呈现层」)。
  > **例外成立的条件**:该字段的**全部消费者都是呈现层** ⇒ 任何 sim 程序集内的代码
  > **读它就是违例**。守它的 = ADR-008 的构建期扫描 + 37 的 AC-37-15(DTO 递归检查)。
  > **不得以此为模板扩张** —— 其余载荷字段仍须是定长值类型。
- **此修正不改变 ADR-005 的核心裁决**,只补其自相矛盾的形状。

### Amendment B —— `patient_id` 跨权威稳定性(兑现 **D-9-E**)✅ **已裁决**

**问题**:`patient_seed = hash(world_seed, patient_id)`(9 的 §Dependencies「换 authority」段)。
**因此 `patient_id` 一旦在权威迁移后改变,`patient_seed` 必变 ⇒ `Noise(t)` 跳变 ⇒
该病人的全部病程被改写。** `PatientId` **必须在主机迁移前后逐位稳定** —— 硬不变量。

**裁决(用户,2026-09-14):采纳机制 A —— 计数器 + 高水位可重构。** 三条硬不变量:

1. **计数器永不复位为 0**;
2. **任何权威变更后,`next = max(全部已知 patient_id) + 1`** —— 由事件流**重构**,
   **不作独立快照**(少一份可失步的状态 —— 见 Alternative 4);
3. **终态折叠行必须保留 `patient_id`**。

> ⚠️ **第 3 条是本轮复核新发现的漏洞,不是补丁而是必需项。**
> ADR-005 §Implementation Guidelines 5 原折叠元组 `(onset, 病种_id, patient_seed, outcome, t_end)`
> **不含 `patient_id`**,而折叠**丢弃流位置**,`patient_seed` 又是**单向哈希**。
> 于是「所有高 id 病人都已折叠」之后,`max(patient_id)` **不可重构** ⇒
> 新主机重建时必然回退 ⇒ **id 复用 ⇒ 两个病人共用一个 `patient_seed` ⇒ 病程互写**。
> 这是**静默的正确性漏洞**,回放检测不到(全序仍然逐位一致,只是语义已错)。
> **ADR-005 已就地修正为 `(onset, 病种_id, patient_id, patient_seed, outcome, t_end)`。**

> **2026-09-15 ADR-016 §二 注记 —— id 空间语义扩大**:敌人(27)复用 9 的伤情模型,
> 其 id **经 `IIdAuthority` 与病人共用同一 id 空间与同一高水位**(避免第二套计数器)。
> 故本修正案三条硬不变量的**适用范围从「病人 id」扩为「受伤实体 id」**:
> ① 计数器永不复位;② `next = max(全部已知实体 id) + 1`(由事件流重构);
> ③ 终态折叠保留 id。**但第 3 条的折叠谓词仍只作用于病史流的病人行** ——
> 敌人行住世界流(ADR-016 §二),**不适用病史流折叠规则**(套用会错误折叠)。
> 判定本修正案的「高水位唯一守卫」时,须同时扫**两流并集**(ADR-010 §七 已定口径)。

**被否决的机制 B —— 纯哈希派生**(9 的 §Dependencies「P0 预留抽象点」节旧稿提案):
`patient_id = hash(world_seed, 出生上下文)` 表面无状态、迁移天然安全,
但**「出生上下文」必须唯一**才能避免碰撞;而任何能让它唯一的定义
(同 tick / 同病因 / 同地点下的第 n 个病人)**都含一个序号** ——
于是又变回计数器,只是把计数器藏进了哈希的输入里。**它没有消除状态,只是让状态更难看见。**
除此之外:哈希 id **无序**,调试与流压缩都要额外索引。**否决。**

**同步**:9 的正文自身曾有矛盾 —— §Dependencies 抽象点表的 `IIdAuthority` 行写
「主机单调计数器」、同节旧稿散文写「hash 派生」。
**两处已统一为机制 A。**

> **2026-09-21 注记 —— id 空间语义第二次扩大(玩家 id · 用户裁定 2026-09-21)**:
> 39 脉案的「每人一份落笔者笔迹」、48 教学的「各人进度独立」、45 联机的「按玩家分册」
> 都需要一个**跨权威稳定的 `player_id`**,而该量的发号机制**此前全库零定义**
> (`TR-casebook-002` 实测:22 份 ADR + `entities.yaml` 均无)。
> **用户裁定 = 复用 `IIdAuthority`,不新开第二套计数器。** 于是本修正案三条硬不变量的
> **适用范围从「受伤实体 id」再扩为「受伤实体 id + 玩家 id」**:
> ① 计数器永不复位;② 任何权威变更后 `next = max(全部已知 id) + 1` 由事件流重构;
> ③ 终态折叠行保留 id(**玩家行的折叠谓词不适用** —— 与敌人行同理,玩家不落病史流)。
> **铸造时点 = 建世界(单机 = 唯一玩家)与每名玩家加入会话时,由主机经 `IIdAuthority` 发号**;
> 迁移后新主机按②重构高水位 ⇒ 与 `patient_id` 走**完全同一条**稳定性论证。
> ⚠️ **本条不引入新的 id 空间**(玩家与受伤实体共用同一空间 ⇒ `max(id)+1` 高水位守卫仍是**单点**);
> ⚠️ **`PatientId.None = -1` 哨兵**(ADR-007 §四)不得被玩家 id 占用,也不污染高水位。
> **残留执行义务**(不在本注记内落地,登记为待办):
> ① `entities.yaml` 增列 `player_id` 条目(归 ADR-024 补齐轮同批);
> ② 45 网络层的「加入会话 → 铸造 → 上行」契约件(归 45 GDD 轮 + ADR-001 窄修订同批);
> ③ 7a 存档头是否需缓存 `next` 高水位 —— **按②的口径不需要**(可由事件流重构),
> 但 7a 须显式声明「不独立快照玩家 id 计数器」(少一份可失步的状态,承 Alternative 4 理据)。


### Amendment C —— `Seq` 的发放域与跨流全序键(2026-09-15 架构复核 C-4)

**问题**:Amendment A 写下全序键 `(Tick, Patient, Seq)` 与「`Seq` 由主机在 `Append` 时分配,
同一 `(Tick, Patient)` 内从 0 单调递增」,但**未说明两条流(病史 / 病例,ADR-008)之间
`Seq` 如何比较**。ADR-008 §一 写「计数器从 0 起,**按流分别单调**」、§二 又把键写成
`(Tick, 流优先级, Patient, Seq)`、同节末又回到「`Seq` 只在同一 `(Tick, Patient)` 内单调」。
三种说法不能同时成立。

**裁决(消歧,不改语义)**:

1. **`Seq` 的发放域唯一,即 `(Tick, Patient)`** —— 与 Amendment A 一致,`Seq` **不分流**。
   同一 tick 的两位病人各有 `Seq = 0`,因此 `Patient` 项**不可省略**(ADR-008 §二 已据此补上)。
2. **两条流共享同一发放器与同一 `(Tick, Patient)` 计数域** —— ADR-008 的「按流分别单调」
   一语**作废**(它会使 `Seq` 跨流不可比,进而使 `Patient` 之后的比较退化为无定义)。
3. **全序键升格为 `(Tick, StreamPriority, Patient, Seq)`** —— `StreamPriority`
   (病史流 < 病例流)是**显式契约项**,不依赖 `Seq` 的跨流可比值;
   在 `(Tick, Patient)` 相同的跨流情形下,它**决定**顺序。
4. 因而 ADR-008 §一 的括号注「按流分别单调」须删(2026-09-15 已删),§二 的表述保留(它是正确的)。

> **为什么必须显式钉死**:F-37.1 的 fires-once 正确性完全建立在「全序无平局」上。
> 若实现者照 ADR-008 §一 的原理解成「两流各自从 0 计数」,跨流比较即出现真实平局 ⇒
> 模式识别的成员集合在重放中可能换序 ⇒ **逐位仍一致但语义已错**(静默类)。

### Amendment D —— 权威件口径:真源是两条流的并集(2026-09-15 架构复核 C-5)

ADR-005 §Decision 三 写「**病史事件流 = 唯一真源**」,ADR-008 引入第二条逻辑流后,
该句在**权威层**已不准确 —— 真源实为**两条流的并集**,
且 ADR-005 的终态折叠规则**只作用于病史流**(病例流豁免,ADR-008 §六)。

**修正**:ADR-005 §Decision 三 的该表述以本修正案为准,即:

- **真源 = 病史流 ∪ 病例流**(两者均为权威、均可重放、均参与 `max(patient_id)` 重构);
- **终态折叠规则只作用于病史流**;病例流**永不物理折叠**;
- ADR-008 §六 的折叠豁免**优先于** ADR-005 的折叠规则,不构成冲突。

本修正案**只改口径,不改任何数据形状**。

### Amendment E —— 升格至三流口径(2026-09-15 · ADR-009)

ADR-009 引入**第三条逻辑流(世界流)**承载世界状态变更(建造 / 掉落 / Craft / 资源点)。
本修正案升格 Amendment C / D 的取值域,**不改任何数据形状**:

1. **Amendment C 升格**:跨流全序键 `(Tick, StreamPriority, Patient, Seq)` 的
   `StreamPriority` 由二值升为**三值**(病史流 < 病例流 < **世界流**);`Seq` 发放域唯一
   `(Tick, Patient)` **三流共享不变**;世界事件 `Patient = PatientId.None`,与病例流
   世界级事件(`PatternRecognized`)共享 `(Tick, None)` 计数域。
2. **Amendment D 升格**:真源 = **病史流 ∪ 病例流 ∪ 世界流**;三者均为权威、均可重放、
   均参与 `max(patient_id)` 重构(世界流全部 `None` 事件**显式排除**);终态折叠
   **仍只作用于病史流**,病例流与世界流**永不物理折叠**。
3. **本修正案只改口径,不改任何数据形状** —— 既有 Validation Criteria(发放域唯一 /
   跨流全序无平局 / 扫并集重构)在语义上自动扩展为三流,取值域以 **ADR-009 §Decision** 为准。

### Amendment F —— 适用域的切分:算术域 vs 边界域(2026-09-16 · 系统 1 的 GDD)

**问题**:本 ADR 的标题是「**定点域边界数据契约**」,但正文的六节**不全是关于边界的** ——
§一 解析入口 · §三 舍入模式 · §四 守恒律 是关于**定点算术怎么算**;
§二 存档与事件流禁 `float` · §五 序列化器 · (ADR-009 §三) 进流字段全整数 是关于**跨域写出去什么**。

撰写系统 1(玩家控制器与移动)的 GDD 时,原作者写下了「**本系统不受 ADR-006 定点域约束**」。
**该句过宽**:1 是**表现层**系统(连续 `Position` / 速度 / 朝向全是浮点,这是 ADR-020 §四 的裁决),
但 1 同时有**一条写进世界流的路径**(`ActorCellEntered`,ADR-009 Amendment G)——
照原句字面,**一个浮点就获得了进入事件流的许可**。这是**静默类**的口径漏洞。

**裁决(仅澄清适用域,不改任何既有条款)**:

| 本 ADR 的节 | 适用对象 | 系统 1 是否受约束 |
|---|---|---|
| **§一** 解析入口(`FixParse`) | **定点算术域** —— 只约束「进 `Fix` 的数据」 | ❌ **不受**(1 无 `Fix` 字段,不做定点算术) |
| **§三** 舍入模式 | 同上 | ❌ **不受** |
| **§四** 守恒律 | 同上(配方 / 药效的投入产出) | ❌ **不受** |
| **§二** 存档与事件流禁 `float` | **跨域写出的边界** —— 约束**任何**写流 / 落盘的系统 | ✅ **受**(且 1 已合规) |
| **§五** `Fix` 序列化边界 | 同上 | ❌ **N/A**(1 不序列化 `Fix`) |
| **ADR-009 §三** 进流字段全整数 | 同上 | ✅ **受**(且 1 已合规 —— 三字段均整数) |

- **一句话口径**:本 ADR 的**算术**章节只约束「**用定点域的系统**」;
  **边界**章节约束「**任何写入存档或事件流的系统**」—— 与它内部用不用 `Fix` **无关**。
- **对系统 1 的结论**:1 的**算术**是表现层浮点(合法,ADR-020 §四);
  1 的**写流**路径三字段(`actor_id` / `cell(WorldPos)` / `tick`)全整数 ⇒ **无冲突**。
- **对未来的约束**:任何**表现层系统**若新增一条写流 / 落盘路径,须按上表逐项自查
  「边界行是否已满足」—— **不得**援引「我是表现层」而跳过边界行。
  ⚠️ 但表现层内部的浮点**不需要**任何豁免:**本 ADR 从不禁表现层的浮点**,
  它禁的是**浮点跨界**(入 sim / 入流 / 入档)。

> ⚠️ **本修正案不得被读作「表现层可以往流里写浮点」** —— 恰恰相反,上表把边界行
> 明确扩到了**所有写流者**。它的作用只是把「算术域」与「边界域」**在纸面上分开**,
> 使一句「我不受本 ADR 约束」不再能同时免掉两者。

**同步**:系统 1 的 GDD(`player-controller-and-movement.md` §Formulas 开头的口径注)
已按本修正案收窄表述;原文的「不受 ADR-006 定点域约束」**已就地作废**。

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

- **`FixParse` 成为全案单点** —— 它错,所有数值全错。需要它自己的负向夹具。
- 作者写数据文件时**不能顺手写 `0.5`** —— 内容作者的摩擦增加了。
  缓解:提供 `FromRatio(1, 2)` 这类写法,把摩擦变成方言而不是障碍。
- **含 `Fix` 的 DTO 不能直接交给 Unity 序列化**(§Decision 五)—— 7a 必须为每个含 `Fix` 的
  持久化结构各写一段编解码;这是**真实的实现成本**,换来的是消掉「药物效力静默归零」。
- **Amendment B 已结** —— 机制 A 的三条不变量须由 7a 与 45 网络层**同时**遵守;
  少实现一条(尤其第 3 条折叠保留 `patient_id`)即静默失效,且**回放检测不到**。

### Neutral

- 不改变 `Fix` 的内部表示(Q16.16 / int64 仍归 ADR-005)。
- 不改变**本 ADR 起草时**五个抽象点的数量与职责。
  > **2026-09-15 补注(C-9)**:ADR-007 §一 此后追加了**第六个**抽象点 `IEventAuthority`
  > (掷骰权与写入通道语义不同,不并入 `IEventSink`)。该增补**不由本 ADR 背书也不由本 ADR 禁止**
  > —— 本 ADR 只声明它未涉及。全案抽象点现为**六个**。

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
  这与 ADR-005 的「事件流无界增长」风险叠加 —— 由终态折叠(ADR-005 Implementation Guidelines 5)承担,
  **不改变折叠策略**。

## Migration Plan

1. **本 ADR Accepted 后**,21a 侧无需改动(其 GDD 已声明 D-21-9 / D-21-10,本 ADR 是其落盘处)。
2. **ADR-005 加前向指针**指向本 ADR 的 Amendment A / B / C / D,不重写 ADR-005 正文；
   一切**跨 ADR 引用改走章节锚点,不再写行号** —— 旧行号已因历次修正注集体失效(2026-09-15 复查轮 N-5)。
3. **7a 持久化服务撰写时**,`Fix` 的序列化形状以本 ADR **§Decision 五**为准 ——
   **必须自定义编码器**,禁交内置序列化器(§Decision 二 的旧断言已废,勿照旧稿实现)。
4. **Amendment B 已裁决为机制 A** —— ADR-005 §Decision 三 的 `patient_id` 条与 §Implementation
   Guidelines 5 的折叠规则、以及 9 的 §Dependencies 抽象点表 / §Dependencies「换 authority」段
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
- [ ] **载荷内枚举 = 版本化整数 ordinal,且映射表进 `ConfigVersion` 内容哈希覆盖集**(D-21-13 收窄,2026-09-17):
      改一条 ordinal ↔ 名称的映射 ⇒ `ConfigVersion` **必变**;构造一个「改了映射却版本号不变」的夹具 ⇒ 断言失败。
- [ ] **`freehand_text` 例外不扩散**(Amendment A 例外):静态扫描断言 `string` 字段**仅出现在**
      `Judgment.freehand_text` 一处,其余 `Payload` 类型全部为定长值类型。
- [ ] `SimEvent` 含 `Seq` 与 `Payload`,且 `Seq` 随事件持久化(Amendment A)。
- [ ] **`patient_id` 迁移稳定性**(Amendment B):含已折叠终态病人的流,重构出的
      `next = max(patient_id) + 1` **严格大于所有出现过的 id**;全流回放后
      `patient_id → patient_seed` 映射**逐位一致**(证明无 id 复用)。
- [ ] **终态折叠行含 `patient_id`**(Amendment B 第 3 条的落盘验证)。
- [ ] **`Seq` 的发放域唯一**(Amendment C):给定一条含两条流(病史 / 病例,ADR-008)的流,
      断言同一 `(Tick, Patient)` 下的 `Seq` **跨流连续且无重号** ——
      即 `Seq` **不是**按流各自从 0 起。
      > 2026-09-15 复查轮 N-6 补:Amendment C 之前**无任何验收判据**。
- [ ] **跨流全序无平局**(Amendment C):按 `(Tick, StreamPriority, Patient, Seq)` 合并两条流,
      断言 `StreamPriority`(病史 < 病例)在同 `(Tick, Patient)` 的跨流情形下**决定**顺序。
- [ ] **`max(patient_id)` 重构扫两流并集**(Amendment D):构造一个「病史流中该病人已折叠、
      但病例流中仍留有该 `patient_id`」的存档,断言新 authority 重构出的 `next`
      **严格大于两流中出现过的所有 `patient_id`**(哨兵 `PatientId.None` 显式排除)。
      > 2026-09-15 复查轮 N-6 补:Amendment D 之前**无任何验收判据**,而这条正是
      > Amendment B 高水位不变量在双流世界里的**唯一守卫** —— 只扫病史流即静默回退。
      > **2026-09-15 ADR-009 追加(Amendment E 升格)**:重构扫**三流并集** ——
      > 再构造一个「世界流含 `PatientId.None` 事件(建造 / 掉落)」的存档,断言 `next`
      > **严格大于三流中出现过的所有 `patient_id`**,`None` 哨兵显式排除。
- [ ] **IL2CPP 与 Mono 逐位一致性实测通过**(与 ADR-005 同一项,阻塞级)。

## GDD Requirements Addressed

| GDD | 系统 | 需求 | 本 ADR 如何满足 |
| --- | --- | --- | --- |
| `design/gdd/item-database.md` | 21a 物品与配方数据库 | D-21-9:F1/F2/F4 出参即 `int`,存档无 `float`,`Offset`/`τ_half` 为 Q16.16 整数字面量 | §Decision 一 / 二 / 三 |
| `design/gdd/item-database.md` | 21a 物品与配方数据库 | D-21-10:守恒律 `Σ(weight × outputs) ≤ EFF_MAX × Σ(weight × inputs)` 且 `EFF_MAX ≤ 1` | §Decision 四 |
| `design/gdd/item-database.md` | 21a 物品与配方数据库 | D-21-13:`processing_state` 跨持久化只用稳定字符串名 | §Decision 二(**2026-09-17 收窄为「跨持久化的配置态枚举」**;载荷内枚举改走**版本化整数 ordinal + `ConfigVersion` 覆盖集**) |
| `design/gdd/item-database.md` | 21a 物品与配方数据库 | **D-21-17:`weight` / `stack_max` 口径 = `int`(最小单位个数),**不是 `Fix`** | §Decision 一 / 二(移出 `Fix` 解析集) |
| `design/gdd/item-database.md` | 21a 物品与配方数据库 | **D-21-18:`Fix` 不可经 Unity 序列化器承载,须自定义编码器** | §Decision 五(新增) |
| `design/gdd/item-database.md` | 21a 物品与配方数据库 | **D-21-15:技能回报 = 投入端可变,`ActualConsumed = Ceil(base / EFF)`** | §Decision 四(守恒律取 `ActualConsumed` 而非基数) |
| `design/gdd/disease-simulation.md` | 9 疾病与伤情模拟 | `SimEvent` 必须可全序且带载荷(§Dependencies 抽象点表 `SimEvent` 行) | Amendment A(D-9-D) |
| `design/gdd/disease-simulation.md` | 9 疾病与伤情模拟 | `patient_seed = hash(world_seed, patient_id)` ⇒ `PatientId` 迁移稳定(§Dependencies 抽象点表 / §Dependencies「换 authority」段) | Amendment B(D-9-E,✅ **已裁决 —— 机制 A + 折叠保留 `patient_id`**) |
| `design/gdd/case-system.md` | 37 病例系统 | 载荷内枚举(`DiseaseIdSet` / `Judgment.lexicon_id`)的类型口径;`freehand_text` 的例外登记 | §Decision 二 **D-21-13 口径收窄**(2026-09-17)· **Amendment A 例外登记** |
| `docs/architecture/adr-008-case-event-stream.md` | ADR-008 | `Judgment` 形状的定点域边界合规(载荷编码 + Amendment A 例外) | §Decision 二 · Amendment A |
| `docs/architecture/adr-014-data-pipeline-and-json-parser.md` | ADR-014 | D-21-13 收窄的**承载件**:ordinal 映射表的烘焙 · 覆盖集 · append-only 门 | §Decision 二(D-21-13 收窄条 · 指向 adr-014 §五) |

## Related

- **ADR-005 确定性模拟与状态同步模型** —— 本 ADR 是其边界的补完,并窄修正其
  `SimEvent` / `patient_id` / `Seq` 发放域 / 真源口径**四处**(Amendment A / B / C / D)。
  2026-09-15 复查轮补上 C / D 两道修正案的**验收判据**(见 §Validation Criteria 末三条)。
- **ADR-009 世界状态的事件化边界**(Accepted,2026-09-15)—— 其 **Amendment E** 升格
  本 ADR 的 Amendment C / D 至**三流口径**(病史 / 病例 / **世界**):真源 = 三流并集,
  `StreamPriority` 三值,`Seq` 发放域三流共享。本 ADR §Amendment E 承载该升格,
  **不改任何数据形状**。ADR-009 Accepted 后本条生效。
- `design/gdd/item-database.md`(21a)—— 本契约的第一个消费者,§Schema 与 §Tuning Knobs 受其约束。
- `design/gdd/disease-simulation.md`(9)—— Amendment A / B / C / D 的受影响方。
- **ADR-008 病例事件流**(Accepted 2026-09-15)—— 其 §一 / §二 直接依赖 Amendment C,
  §六 的折叠豁免依赖 Amendment D。
- `design/registry/entities.yaml` —— 常数与公式的等价性登记处。
