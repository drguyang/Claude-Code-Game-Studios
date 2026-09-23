# ADR-026: 30 技能成长的定点化与持久化契约 (Skill Growth Fixed-Point & Persistence Contract)

## Status

Accepted

> **2026-09-23 起草并转 Accepted。** 用户裁定(2026-09-23,照准):**兑现
> `architecture.md` §Required ADRs #4** —— 「30 技能成长的定点化与持久化契约」。
> 本 ADR 是 `TR-skill-001…008` 的**首个 ADR 承接件**(此前 7 条全 `gap`、零 ADR 引用)。
> **数值不裁决** —— `C` / `P` / `BASE[skill]` / `NOVELTY_*` / `DEATH_LOSS` / `MED_COMBAT_MOD` /
> `WeaponMultiplier[line]` 的**取值归用户**(数值轮,与 `OQ-25-7` 同批)。本 ADR 只定
> **求值形式与存储形状**。

## Date

2026-09-23

## Last Verified

2026-09-23

## Decision Makers

dr_guyang(用户 · **2026-09-23 裁定兑现 Required ADR #4**)· technical-director(起草与裁决)
· 30 技能与熟练度(公式所有者)· 9 疾病与伤情模拟(`Exp` / 定点域先例)· 8 诊断与体征揭示
(`K_difficulty` 代入方)· 7a 持久化服务(成长事件序列化 / 折叠)· 25 格斗与武器线
(`CombatPower` 消费方)· 51 遥测(成长读数器)· ADR-012 CI 门(逐位性判据)

## Summary

系统 **30 技能与熟练度**是 §5.4 认定的**唯一真正的 Foundation 级裸缺口** ——
其三条公式(`XP_gain` / `XP_to_next(n) = C × n^P` / `CombatPower`)在首轮评审(2026-09-18)前
**用浮点字面量求值、且零 ADR 引用**,与 ADR-005 门 A / ADR-006 定点纪律直接冲突;
`P = 1.4` 更违反 8 的 G-1(禁 libm 超越函数)。本 ADR 裁决:**全部成长数学在 Q16.16 整数定点域
求值,幂运算有唯一整数实现 `FixPow`(指数 ∈ {整数, 整数+1/2},无 libm / 无 float)**;
`CombatPower` 以 `Fix` 传入 25;**成长事件以 `SkillGrown` 落病史流、`Level` 以 int 承载、
不独立快照**(从流重构);并**结清 `OQ-7a-9`**(终态折叠对 `SkillGrown` 的存续)。

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS (URP) |
| **Domain** | Core(整数定点算术 + BCL 序列化) |
| **Knowledge Risk** | **LOW** —— 整数数学 + BCL;不触及任何 post-cutoff 引擎 API |
| **References Consulted** | `docs/architecture/adr-005-deterministic-sim.md` · `adr-006-fixed-point-boundary-contract.md` · `adr-010-persistence-save-format.md` · `adr-014-data-pipeline-and-json-parser.md` · `adr-016-ai-architecture.md` · `design/gdd/skill-system.md` · `design/gdd/diagnosis-system.md`(C-5 / G-1)· `design/gdd/disease-simulation.md`(定点 `Exp` 先例)· `design/gdd/persistence-service.md`(F-7a-4 · `OQ-7a-9`)· `unity/Assets/Sim.Contracts/Fix.cs` / `FixParse.cs` |
| **Post-Cutoff APIs Used** | **None** —— 整数牛顿迭代求平方根 + BCL 编码器 |
| **Verification Required** | ① `FixPow` 的整数实现跨平台逐位一致(IL2CPP vs Mono)—— 归 ADR-012 黄金夹具矩阵;② `Fix.Div` 与 `FixParse.RoundHalfAwayFromZero` 的舍入一致性探针(EditMode) |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | **ADR-005**(Accepted —— 整数定点域 · `Fix` struct · 唯一真源)· **ADR-006**(Accepted —— 边界契约 · 单一舍入 `ROUND_HALF_AWAY_FROM_ZERO` · `Fix` 不可经内置序列化器)· **ADR-014**(Accepted —— 烘焙管线 · `Fix` 字段 JSON 写字符串 → `FixParse`)· **ADR-010**(Accepted —— 义务汇总表 §三 · 病史流序列化 / 折叠) |
| **Enables** | **30 的实现**(首个架构约束)· **25 的 `CombatPower` 消费**(A16 / `AC-25-6-02`「30 修订后复验」)· **51 的 F4 成长读数**(`AC-51-D2` / `AC-51-B11`)· **37 的 `QueryLevel(诊断)`** |
| **Blocks** | **30 的实现**(在写任何成长公式前须 Accepted)· **11 的 `AC-11-16`** —— 但其残余阻塞是 `OQ-11-13`(`K_difficulty` 代入方),**不在本 ADR 裁决面** |
| **Ordering Note** | 本 ADR **不阻塞**其他 Foundation 系统;它把 ADR-005 的定点泛则**落实到 30 的三条公式**。`FixPow` / `Fix.Div` / `Fix.ISqrt` 是 `Sim.Contracts` 的**新增成员**(承 ADR-025 ① 清单:`Fix` / `FixParse` 已在该程序集内) |

## Context

### Problem Statement

`skill-system.md` 的三条公式在首轮评审(2026-09-18)前**全程用浮点**:
§4.1 `XP_gain = BASE × K_difficulty × K_novelty`、§4.2 `XP_to_next(n) = C × n^P`(`P = 1.4`)、
§4.4 `CombatPower = (CombatSkillLevel × WeaponMultiplier) × (1 + 医术修正)`。
而 30 属 **L2 Sim 层**(`architecture.md` §Module Ownership 行 30),**住在门 A 程序集内** ——
门 B 断言该程序集**零 `float` / `double` 签名**。**浮点出现在 sim 侧 ⇒ IL2CPP 逐位性无从谈起**
(违 ADR-005 §一)。

更严重的是 `P = 1.4`:`n^1.4` 须经 `pow`,而 `pow` / `exp` 是 **libm 超越函数**,末位**不保证跨平台一致**
—— 直接违反 8 的 **G-1**(`diagnosis-system.md:1102`)。若不裁决:**30 的实现会各自决定幂运算怎么写**,
而浮点幂的末位分歧会在**联机 / 迁移 / 重放**里静默分叉(与 ADR-005 的安全论证直接冲突)。

### Current State

- `skill-system.md` §3.2 / §4.1 / §4.2 / §4.3 / §4.4 已由首轮评审补上 **Q16.16 定点域注**
  (B1 落盘),但**求值形式的唯一实现**(幂运算 / 平方根)**尚未定义**。
- `P` 取值集被 G-1 限死为 `{整数, 1/2}`(`{1, 1.5, 2}` 例值),**最终值归用户**;
  原例值 `1.4` **作废**。§4.2 的曲线表(累计经验)是 `P=1.4` 的**旧值,不作实现依据**。
- `Fix` 现有成员仅 `Mul` / `MulRaw`(`unity/Assets/Sim.Contracts/Fix.cs`)—— **`Div` 与平方根缺位**,
  而 §4.4 的 `医术修正` 除法与 §4.2 的 `x^(1/2)` 都需要它们。
- 成长事件的流载体(`SkillGrown`)已登记(`entities.yaml:2447` · ADR-024 补齐轮),载荷含 `level`(int);
  但**其持久化契约与折叠存续未裁**(`OQ-7a-9`)。

### Constraints

- **门 A / 门 B**:30 住 sim 程序集 ⇒ **零 `float` / `double`**,全部成长数学在整数域。
- **单一舍入模式**(ADR-006):除法一律 `ROUND_HALF_AWAY_FROM_ZERO`,**禁** `Math.Round` 默认 ties-to-even。
- **禁 libm 超越函数**(G-1):`pow` / `exp` 禁用;指数**只取整数或 1/2**。
- **`Fix` 不可经 Unity 内置序列化器承载**(ADR-006 §五 D-21-18):成长数据落 JSON 时 `Fix` 字段写字符串。
- **成长事件按玩家独立**(联机,`skill-system.md` §6 横向):新颖度追踪**不可共享**。
- **数值冻结**:本 ADR **不改任何数值**(用户所有)。

### Requirements

- 给出 `XP_to_next(n) = C × n^P` 的**整数域求值形式** —— 幂运算在定点域的**唯一实现**。
- 给出 `CombatPower` / `医术修正` 的定点求值形式(`Div` + 单一舍入),并以 `Fix` 传入 25。
- 运行时调参表的**默认值定点化**(`Fix` 字段 → 字符串 → `FixParse`)。
- 技能门槛 / 解锁 / 档位判定**走整数等级比较**,不经浮点(承 8 的 G-3)。
- 技能成长的**存档持久化**:以 `SkillGrown` 落病史流、`Level` 以 int 承载、**不独立快照**、
  **从流重构**;并把该义务追加进 `ADR-010 §三`(唯一出处)。
- **结清 `OQ-7a-9`**:终态折叠不得抹掉 `SkillGrown`。

## Decision

### ① 幂运算的唯一整数实现 = `FixPow`(指数 ∈ {整数, 整数 + 1/2})

**`XP_to_next(n)` 在 Q16.16 域求值,幂运算由 `Sim.Contracts` 的单一函数 `FixPow` 提供。**
`FixPow` 的指数是**闭集 `{k, k + 1/2 | k ∈ ℤ}`**(由 G-1 限死),实现分两路:

- **整数幂 `k`**:`Mul` 重复自乘(`k` 次),**全程整数、逐位确定**。
- **半整数幂 `k + 1/2`**:`FixPow(base, k) × FixSqrt(base)`,其中 `FixSqrt` = **整数牛顿迭代求平方根**
  (在 `ulong` 上迭代,**精确 floor**,**不调 `Math.Sqrt` / 任何 libm / 任何 float**)。
  牛顿迭代在整数域的收敛判据是**确定的**(单调收敛 + 不动点),无浮点中间量。

> **⚠️ 为什么不走 `Math.Sqrt`**:`Math.Sqrt` 返回 `double` —— 出现在 sim 程序集即违门 B
> (零 `float` / `double`)。G-1 说「`x^(1/2)` = `sqrt`,IEEE-754 强制正确舍入」是**存在性论证**
> (存在一个确定的基本运算),**不是**「可用 BCL `Math.Sqrt`」的许可。**整数牛顿迭代**给出
> **逐位确定的 floor 平方根**,是本仓在门 A 内的合法兑现。

### ② 允许**构建期记忆化定表**,但不得成为第二真源

`n` 的定义域是**有界整数 `1..59`**(`SKILL_CAP = 60`)。允许构建期把 `FixPow` 的输出
**烘成 `XP_to_next` 定表**(`int64[60]`,raw `Fix` 值),运行期查表。**硬约束**:
定表的每个值**必须等于 `FixPow` 的求值结果**(构建期断言)—— 定表是**记忆化**,不是第二定义。
这是 G-1 自身给的「或干脆预计算成定表」分支,但**定义权仍归 `FixPow`**。

### ③ `CombatPower` 的定点求值与对 25 的交接

```
CombatPower = (CombatSkillLevel × WeaponMultiplier) × (1 + 医术修正)
医术修正     = FixDiv(MED_COMBAT_MOD × 关联医术等级, SKILL_CAP)
```

- `CombatSkillLevel`(int)× `WeaponMultiplier`(`Fix`)按定点乘法(承 25 的 `AC-25-6-02` / `A16`)。
- `医术修正` 的除法走 **`Fix.Div` + `ROUND_HALF_AWAY_FROM_ZERO`**(ADR-006 唯一舍入)。
- **`CombatPower` 以 `Fix` 传入 25,禁 int 直乘**(承 25 的 A16)。
- **上限 +20% 是硬约束**(`MED_COMBAT_MOD = 0.20`),可验证。

### ④ 调参表默认值定点化(承 ADR-014)

`assets/data/skills.json` 中:**承载 `Fix` 的字段写字符串**(`"0.95"` / `"19/20"` → `FixParse`,
**拒浮点字面量**);**`int` 计数**(等级 / `SKILL_CAP` / `NOVELTY_COOLDOWN` tick 数 / `DIAG_TIERS`)
写整数。构建期经 ADR-014 两阶段烘成 `*.cooked`(`Fix` = raw `long`)。**玩家构建零 JSON 解析器**。

### ⑤ 门槛 / 解锁 / 档位判定 = 整数等级比较(承 8 的 G-3)

所有技能门槛 / 解锁 / 档位判定(`Skill ≥ tier_named_j`、`DIAG_TIERS` / `SLOT_BOUNDS`、
`SKILL_CAP` 锁定、19 项技能的依赖解锁)**走整数等级比较,不经浮点** —— 避免 `19.9999999`
落在哪一档的边界抖动。`DIAG_TIERS ↔ SLOT_BOUNDS` 的双向耦合(8 的 `D-8-9`)由
**构建期断言 `SLOT_BOUNDS ⊂ DIAG_TIERS`** 守住(义务归 8,此处仅登记口径)。

### ⑥ 成长持久化:落病史流、int 承载、不独立快照、从流重构

- 成长事件以 **`SkillGrown` 落病史流**(ADR-005 · 主机唯一 `Append`),载荷
  `(actor_id, patient_id, skill_id, object_id, novelty_class)` + `level`(int,升级时携带;
  未升级 ⇒ 缺省哨兵,**不写 0**)。
- **`Level` 以 int 承载,不属 `Fix` 解析集。**
- **不独立快照**:技能等级**从病史流的 `SkillGrown` 重构**(与 `max(patient_id)` 高水位同构 ——
  少一份可失步的状态)。
- 该义务**追加进 `ADR-010 §三` 义务汇总表第 14 行**(唯一出处)。

### ⑦ 结清 `OQ-7a-9`:终态折叠对 `SkillGrown` 的存续

**裁决:折叠豁免扩一类 Kind —— `SkillGrown` 不随病人折叠被抹除。**
`persistence-service.md` F-7a-4 的折叠谓词按病人整体折叠病史行,而 `SkillGrown`
是 **29 的 F-29-4 掉级 fold 的输入 + 30 的成长真源** —— 若一并被折掉,`Level(s)@t`
的逐位重放即破(等级永不被死亡夺走 = 30 的机制承诺)。三案中取 **案 1(折叠豁免扩 Kind)**:

- **案 1(采纳)**:折叠谓词**豁免 `SkillGrown` 行** —— 成长事件随流永久保留。
- 案 2(折叠行扩列携带成长摘要)否决:改折叠行形状 = 改 ADR-005 / ADR-010 已冻结的折叠行契约,
  且摘要 = **第二真源**。
- 案 3(fold 读侧从快照取基线)否决:把等级基线搬进快照 = **独立快照**,与 ⑥ 的
  「从流重构」直接冲突。

⇒ `OQ-7a-9` **结案**;29 的 `AC-29-16` 与 51 的成长读数**解除 `BLOCKED`**。

### Key Interfaces

```csharp
// Sim.Contracts(BCL only)—— Fix 的新增成员(承 ADR-025 ① 清单)
public readonly struct Fix
{
    // 既有:FractionalBits / OneRaw / Raw / ToFloat() / operator* / MulRaw

    /// <summary>定点除法,单一舍入 ROUND_HALF_AWAY_FROM_ZERO(ADR-006)。除数为 0 ⇒ 抛异常。</summary>
    public static Fix Div(Fix a, Fix b);

    /// <summary>整数牛顿迭代求平方根(raw 的 floor 平方根,再按 Q16.16 缩放)。
    /// 无 libm / 无 float —— 门 B 允许。逐位确定。</summary>
    public static Fix ISqrt(Fix x);

    /// <summary>幂运算的唯一实现。exponent 只接受 {整数, 整数 + 1/2}(G-1 限死);
    /// 其它指数 ⇒ 抛 ArgumentOutOfRangeException(构建期即拒)。</summary>
    public static Fix Pow(Fix baseValue, Fix exponent);
}
```

```
// 30 的成长事件载荷(已在 Sim.Contracts.Payloads.HistoryPayloads.cs 实现)
SkillGrownPayload { int ActorId; int PatientId; int SkillId; int ObjectId; int NoveltyClass; int Level; }
// Level = int(非 Fix);未升级 ⇒ 哨兵,不写 0
```

### Implementation Guidelines

1. **先 `Fix.Div` / `Fix.ISqrt` / `Fix.Pow`,再写任何成长公式** —— 顺序反了会写出浮点版再改(承 ADR-005 实现准则一)。
2. `Fix.Pow` 的指数**构建期校验** ∈ {整数, 整数 + 1/2} —— 越界即 `throw`(把 G-1 升为构建失败)。
3. 定表(若采用)在构建期由 `Fix.Pow` 生成并断言逐值相等;运行期只读定表。
4. **禁 `Math.Sqrt` / `Math.Pow` / `Math.Round` / `Math.Exp`** 出现在 sim 程序集
   (归 ADR-012 / 门 B 的反射断言)。
5. `SkillGrown` 的 `Level` 未升级时写**哨兵**(`PatientId.None` 同款 `-1` 约定),
   **不得写 0**(0 = 真实等级,语义冲突)。

## Alternatives Considered

### Alternative 1: 保留浮点,靠 IL2CPP 实测逐位性

- **Description**:公式继续用 `double`,赌 IL2CPP 在目标平台逐位一致。
- **Pros**:实现最简;`Math.Pow` 直接可用。
- **Cons**:门 B 断言该程序集零 `float` / `double` 签名 —— **直接构建失败**;
  且 `pow` 末位跨平台**已知不保证**(G-1 的立论),与 ADR-005 的安全论证冲突。
- **Estimated Effort**:低。
- **Rejection Reason**:违门 A / 门 B / G-1 三条硬约束。

### Alternative 2: 只允许整数指数(`P ∈ {1, 2}`)

- **Description**:砍掉 `P = 1.5` 分支,只留重复乘法。
- **Pros**:实现最简,零平方根。
- **Cons**:曲线形状自由度收窄到线性 / 平方;`P = 1.5` 是 G-1 **明确允许**且文档给过的例值。
- **Estimated Effort**:低。
- **Rejection Reason**:**不否决,降级为 `P` 的候选值之一** —— `P` 的最终取值归用户;
  若用户选 `P ∈ {1, 2}`,`FixSqrt` 路径自然不被触发(但接口仍保留,以容纳 `P = 1.5`)。

### Alternative 3: 全部预计算成定表,不要 `Fix.Pow`

- **Description**:直接烘 `int64[60]` 定表,运行期零幂运算。
- **Pros**:运行期最省。
- **Cons**:定表**从哪来**仍是问题 —— 生成它仍需一个幂实现;若生成器用浮点,则跨平台烘焙分歧。
- **Estimated Effort**:低。
- **Rejection Reason**:不否决 —— **作为 ② 的记忆化分支采纳**,但**定义权归 `Fix.Pow`**
  (定表必须等于 `Fix.Pow` 的输出,构建期断言)。

### Alternative 4: 走 `Math.Sqrt` 求 `x^(1/2)`

- **Description**:直接用 BCL `Math.Sqrt`(IEEE-754 强制正确舍入)。
- **Pros**:实现最简。
- **Cons**:`Math.Sqrt` 返回 `double` —— **门 B 禁止 sim 程序集出现 `float` / `double`**。
- **Estimated Effort**:低。
- **Rejection Reason**:门 B 硬约束;整数牛顿迭代给出逐位确定的 floor 平方根。

## Consequences

### Positive

- 30 的三条公式**全部落进整数域**,门 A / 门 B / G-1 三条约束同时满足。
- 幂运算**唯一实现**,联机 / 迁移 / 重放里无浮点末位分叉。
- 成长事件**从流重构**,与高水位机制同构,少一份可失步状态。
- `OQ-7a-9` 结案,29 / 51 的两处 `BLOCKED` 解除。

### Negative

- `Fix.Div` / `Fix.ISqrt` / `Fix.Pow` 是 `Sim.Contracts` 的**新增成员**,须过 ADR-012 黄金夹具矩阵
  (跨平台逐位对拍)。
- 整数牛顿迭代比 `Math.Sqrt` 略慢(但 `n ≤ 59` 且有定表兜底,可忽略)。

### Neutral

- `P` 的最终值仍归用户;本 ADR 只保证**无论选 `{1, 1.5, 2}` 哪个都可求值**。

## Risks

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|-----------|
| `Fix.ISqrt` 牛顿迭代在某输入上不收敛 / 差 1 | 低 | 高(静默错值) | 收敛判据用整数不动点(非 epsilon);EditMode 探针穷举 `1..59^2` 全档 + 边界 |
| `Fix.Div` 舍入与 `FixParse.RoundHalfAwayFromZero` 不一致 | 低 | 中 | 单测断言两路径同输入同输出 |
| 定表与 `Fix.Pow` 漂移(第二真源) | 中 | 中 | 构建期断言逐值相等;禁运行期重算定表 |
| `P` 用户裁定后 §4.2 曲线表重算与 AC 断言不吻合 | 中 | 低 | 曲线表标「待 `P` 定值后重算」,AC 只断言 `P ∈ {整数, 1/2}` |

## Performance Implications

| Metric | Before | Expected After | Budget |
|--------|--------|---------------|--------|
| CPU (frame time) | n/a(未实现) | 单次成长事件:整数乘 / 除 + 至多一次整数平方根 | 远低于 tick 预算(50 ms) |
| Memory | n/a | 定表 `int64[60]` = 480 B(可选) | 可忽略 |
| Load Time | n/a | 无变化(数据走既有 ADR-014 烘焙) | — |
| Network | n/a | 无新增通道(`SkillGrown` 落既有病史流) | — |

## Migration Plan

1. **`Sim.Contracts` 增 `Fix.Div` / `Fix.ISqrt` / `Fix.Pow`** —— 附 EditMode 单测;跑 ADR-012 三格矩阵。
2. **30 的成长公式按定点域实现** —— `XP_gain` / `XP_to_next` / `DeathPenalty` / `CombatPower`。
3. **`skills.json` 的 `Fix` 字段改字符串** —— 过 ADR-014 阶段 2 校验。
4. **`SkillGrown` 落病史流 + 折叠豁免** —— 7a 的折叠谓词加 `SkillGrown` 豁免分支;EditMode 探针实测。
5. **`ADR-010 §三` 追加义务 14** —— 本批执行。

**Rollback plan**:本 ADR 为纯规格 + 新增成员,回滚 = 删 `Fix.Pow` / `ISqrt` / `Div` 并撤销 30 的定点实现。
**无历史数据受影响**(30 尚未实现)。

## Validation Criteria

- [ ] `Fix.Pow` / `Fix.ISqrt` / `Fix.Div` 通过 ADR-012 三格黄金夹具矩阵(跨平台逐位一致)
- [ ] `Fix.ISqrt` 在 `1..59²` 全档给出正确 floor 平方根(EditMode 穷举)
- [ ] `Fix.Pow` 对越界指数(`P = 1.4` 类)`throw`(构建期拒)
- [ ] sim 程序集**零 `float` / `double`**(门 B 反射断言)且**零 `Math.Sqrt` / `Math.Pow` / `Math.Round`**
- [ ] `CombatPower` 以 `Fix` 传入 25(25 侧 `AC-25-6-02` 复验通过)
- [ ] `SkillGrown` 折叠豁免实测:折叠后 `Level(s)@t` 仍可逐位重放(29 的 `AC-29-16` 转可判)
- [ ] `skills.json` 的 `Fix` 字段为字符串、`int` 计数为整数(ADR-014 阶段 2 校验通过)

## GDD Requirements Addressed

| GDD Document | System | Requirement | How This ADR Satisfies It |
|-------------|--------|-------------|--------------------------|
| `design/gdd/skill-system.md` | 30 技能与熟练度 | **TR-skill-001** `XP_to_next(n) = C × n^P`(指数 ∈ {整数, 整数+1/2};旧例值 `1.4` 作废) | `P` 限死为 `{整数, 1/2}`(G-1);幂运算由 `Fix.Pow` 唯一整数实现(① / ②);`1.4` 作废 |
| `design/gdd/skill-system.md` | 30 技能与熟练度 | **TR-skill-002** 熟练度成长在整数定点域内求值 | 全部成长数学落 Q16.16(① / ③ / ④);sim 程序集零 float |
| `design/gdd/skill-system.md` | 30 技能与熟练度 | **TR-skill-003** `CombatPower = (CombatSkillLevel × WeaponMultiplier) × (1 + 医术修正)` | 定点求值 + `Fix` 传入 25(③) |
| `design/gdd/skill-system.md` | 30 技能与熟练度 | **TR-skill-004** `医术修正 = 0.20 × (关联医术等级 / 60)` | `Fix.Div` + `ROUND_HALF_AWAY_FROM_ZERO`(③) |
| `design/gdd/skill-system.md` | 30 技能与熟练度 | **TR-skill-005** 运行时调参表的默认值须定点化 | `Fix` 字段 JSON 写字符串 → `FixParse`(④) |
| `design/gdd/skill-system.md` | 30 技能与熟练度 | **TR-skill-006** 19 项技能的依赖与解锁关系 | 门槛 / 解锁 / 档位判定走整数等级比较(⑤) |
| `design/gdd/skill-system.md` | 30 技能与熟练度 | **TR-skill-007** 30 与 25 格斗线的数据边界 | `CombatPower` 以 `Fix` 传入 25 的单一交接点(③);`DIAG_TIERS` 双向耦合口径(⑤) |
| `design/gdd/skill-system.md` | 30 技能与熟练度 | **TR-skill-008** 技能成长的存档持久化 | `SkillGrown` 落病史流 · int 承载 · 不独立快照 · 从流重构(⑥);折叠豁免(⑦);义务 14 入 ADR-010 §三 |
| `design/gdd/persistence-service.md` | 7a 持久化服务 | `OQ-7a-9` 终态折叠与 `SkillGrown` 的存续 | 裁决 = 折叠豁免扩一类 Kind(⑦) |

## Related

- **兑现** `architecture.md` §Required ADRs **#4**(30 技能成长的定点化与持久化契约)。
- **上游** `ADR-005`(定点域 · `Fix` struct)· `ADR-006`(边界契约 · 单一舍入)· `ADR-014`(烘焙管线)· `ADR-010`(义务汇总表)。
- **同源** `diagnosis-system.md` **G-1 / C-5**(禁 libm 超越函数;指数仅整数或 1/2)·
  `disease-simulation.md` 定点 `Exp` 先例(AC-5b)。
- **追加** `ADR-010 §三` 义务汇总表第 14 行(技能成长持久化)。
- **结清** `OQ-7a-9`;解除 29 的 `AC-29-16` 与 51 成长读数的 `BLOCKED`。
- **未结(不在本 ADR 裁决面)**:`OQ-11-13`(`K_difficulty` 代入方)· `OQ-11-11`(`EmitGrowth`
  入口签名)的 `K_difficulty` 形参 —— 归 11 / 30 的下一轮;30 的「等级 → `EFF`」映射(重开条件 ⑤(a))。
- **实现落点** `unity/Assets/Sim.Contracts/Fix.cs`(`Div` / `ISqrt` / `Pow` 新增)·
  `unity/Assets/Sim.Contracts/Payloads/HistoryPayloads.cs`(`SkillGrownPayload` 已实现)。
