# W / TREND_EPS 数据管道 —— 铺设轮自述(2026-10-22)

> ⚠️ **一句话结论:管道只通了 2 段中的 1 段半。`W` / `TREND_EPS` 仍为 0 ⇒ 生产路径上
> 痊愈支 / 迁延支仍不求值。**
>
> ⚠️ **行号声明**:本文件的行号分两类 ——
> **(V)** = 本次用 `grep` 实测看到的行号;
> **(A)** = **改动前**的锚点行号,**改动后未复核**(本轮被要求停止勘察,未回读)。
> 凡标 (A) 的行号可能因插入而位移,定位请以「锚点文本」为准。

---

## 1. 落地清单

| # | 项 | 状态 | 证据(file:line) |
|---|---|---|---|
| 1 | 根键白名单加 `trend_window_ticks` / `trend_eps` | 已落盘 | `unity/Assets/Editor.Tools.Bake/DiseaseRegistryBinder.cs:34`(A)—— `RootKeys` 数组,原 2 键 → 4 键 |
| 2 | `BindResult` 增 `TrendWindowTicks` / `TrendEpsRaw` 两字段 | 已落盘 | `DiseaseRegistryBinder.cs:~80`(A)—— `public readonly uint SchemaVersion;` 之后;构造函数同步扩到 5 参 |
| 3 | `Bind()` 读两键 + 调 `ValidateTrendWindow` | 已落盘 | `DiseaseRegistryBinder.cs:~118`(A)—— `uint schemaVersion = ReadSchemaVersion(root, errors);` 之后 |
| 4 | `R1_CHECK_36` 常量 + `ValidateTrendWindow` 方法 | 已落盘 | `unity/Assets/Sim/RegistrySchema.cs:~400`(A)—— 插在 `public static void Validate(List<DiseaseRegistryEntry> entries)`(原 :398)之前 |
| 5 | JSON 顶层加两键,值 0 | 已落盘 | `assets/data/disease_registry.json:11`(V)`"trend_window_ticks": 0,` / `:12`(V)`"trend_eps": "0",` |

JSON 改动后已用 `python3 -c "json.load(...)"` 验证**可解析**,`diseases` 仍 13 行(V)。
`trend_eps` 写成**字符串** `"0"`(ADR-014 §四硬规则 1:Fix 字段禁 JSON 数字)。

---

## 2. 接线现状(最关键)

### 改了什么

`unity/Assets/Gameplay.Boot/DiseaseVitalsService.cs` 三处:

| 处 | 改动 |
|---|---|
| 字段区(构造器前,~:78(A)) | 新增 `private readonly long _trendWindowTicks;` / `private readonly long _trendEpsRaw;` |
| 构造函数(:82(A)) | 签名尾部加**带默认值**的两个形参 `long trendWindowTicks = 0, long trendEpsRaw = 0`;体内原样赋值,不做校验、不代拍 |
| `:401`(A) 的 `ComputeTrendBackward(...)` | `windowTicks: 0, trendEpsRaw: 0` 字面量 → `windowTicks: _trendWindowTicks, trendEpsRaw: _trendEpsRaw` |
| `:413`(A) 的 `F4OutcomeEvaluator.Evaluate(...)` | `default(RecoveryWindowInput)` → `new RecoveryWindowInput(_trendWindowTicks, _trendEpsRaw, 0L)` |

### 这条链一共几段、断在哪

`disease_registry.json` → `Binder.BindResult` → `DiseaseVitalsService` → `RecoveryWindowInput` → `F4OutcomeEvaluator`

| 段 | 状态 | 位置 / 说明 |
|---|---|---|
| ① JSON → `BindResult` | **通** | `DiseaseRegistryBinder.Bind()` 读两键、校验、装进 `BindResult` |
| ② `BindResult` → `DiseaseVitalsService` | **★ 断** | **`unity/Assets/Gameplay.Boot/CompositionRoot.cs:182-183`(A)** —— 调用点仍是 `new DiseaseVitalsService(stream, blobPool, fallbackRegistry, worldSeed)`,**不传** `BindResult.TrendWindowTicks` / `TrendEpsRaw` ⇒ 实参取默认值 0 |
| ③ `Service` → `RecoveryWindowInput` | **通** | `DiseaseVitalsService.cs:~401 / :413`(A) |
| ④ `RecoveryWindowInput` → 求值器 | **通(既有)** | `F4OutcomeEvaluator.cs:91`(A)`IsConfigured` 门 |

**断点 ② 为什么不接** —— **不是没时间,是结构性阻塞,且有两条,任一单独成立就接不了:**

1. **装配路径根本没读 JSON。** `CompositionRoot` 走的是 `CreateFallbackRegistry()`
   (`CompositionRoot.cs:~650`(A))—— 一个**内联手写**的 1 条病种条目,**完全不经过
   `DiseaseRegistryBinder`**。所以 `BindResult` 这个对象在运行时**压根不存在**,
   没有东西可以传。要接 ②,须先让组合根改走烘焙产物装载路径。
2. **烘焙产物不携带这两个字段。** `unity/Assets/Editor.Tools.Bake/DiseaseRegistryCookedWriter.cs:48-65`(A)
   的 `WriteRow` 逐字段写死(`Scale` raw / `TraumaHalfLife` / 曲线块 …),
   **没有** `trend_window_ticks` / `trend_eps` 的位置;且这两个量是**全局**的,
   本应写在文件头,而文件头布局(`FinishHeader`,20 字节:行数 + schemaVersion + configVersion)
   也没有它们的位置。⇒ 即使改走 cooked 路径,值也会在路上丢掉。
   **要补须改 cooked 布局 ⇒ 撞 `CookedFormat.FormatVersion` 版本号,影响
   `cooked_drift_all_artifacts_test` 等既有对拍** —— 超出「铺数据管道」的授权面。

⇒ 我**只做到「值一旦进了 service 构造参数就生效」**,没做到「值从数据面自动流进来」。
这一段是**故意停手的**,不是遗漏。

### 另一条同样重要的断点(第 ⑥ 条恒不成立)

`RecoveryWindowInput` 的第三参 `LowPositionHeldTicks` 我**恒传 0**(`DiseaseVitalsService.cs:~410`(A))。
理由:它**不是配置量,是跨 tick 状态**(「`position_agg` 已连续低于痊愈门多少 tick」),
需要一个按病人保存的累计器,而**全库没有这个累计器**。

后果必须写明:**即使数值轮填了 `W > 0`,痊愈支仍然不会成立** ——
`F4OutcomeEvaluator.cs:280`(A) 的 `if (window.LowPositionHeldTicks < window.WindowTicks) return null;`
在 `held = 0 < W` 下恒真。**只有迁延支会开始生效。**
⇒ 「数值一填即生效」这句话对**迁延支**成立,对**痊愈支不成立**。累计器归 9 的后续实现轮。

---

## 3. `0 = 未配置` 这个约定的出处

分两层,必须拆开说:

**① `IsConfigured` 以 0 为哨兵 —— 不是我定的,本来就存在。**
`unity/Assets/Sim/F4OutcomeEvaluator.cs:91`(A):
`public bool IsConfigured => WindowTicks > 0 && TrendEpsRaw >= 0;`
以及 `F4OutcomeEvaluator.cs:~50-56`(A) 的结构体文档:
「传 `default`(全零)时痊愈支与迁延支不求值 …… 这样『数值未定』在类型上可判」。
这两处都在**上一轮**就写好了,本轮**未改** `F4OutcomeEvaluator.cs` 一个字节。

**② 「在 JSON 里也把 0 当作未配置的哨兵值写下去」—— 这个是我定的。**
GDD Tuning §四 `:1743-1744` 给的安全区间是 `W > 0`(且偶数)、`TREND_EPS > 0`
—— **`0` 不是 GDD 的合法值**。所以:

- 我在 `RegistrySchema.cs` 的 `R1_CHECK_36` 注释里明写「`0` 是『未配置』哨兵,不是 GDD 的合法值」
  与「『`W = 0` 也放行』是**工程约定,GDD 无此条**」;
- 相应地,`ValidateTrendWindow` **放行 0**,只拒**负值**与**奇数 `W`**。

⇒ 这是**一处对 GDD 安全区间的自觉偏离**,理由是:数值轮未裁,若不放行 0 则 JSON 无法存在
(必须填一个非 0 值 = 代拍数值,违反本轮硬约束)。**该偏离应由用户追认或改判**,我不主张它是正确形态。

**载体裁定(每病种 vs 全局)= 全局。** 依据:
- GDD `:1564`(V)明写「本节是**全局旋钮** + 跨旋钮约束」,而 `W` / `TREND_EPS` 位于
  §四「噪声与读数」(`:1737-1744`,V),**不属** §二「病程曲线(**逐病种** · R1.3 schema)」;
- R1.3 的字段表(`:316-332`,V)逐字段列了 `curve.*` / `relapse.*` / `scale` / `handle`,
  **没有** `w` / `trend_eps`。
⇒ 全局,放顶层。**这是从 GDD 结构读出来的,不是 GDD 明文写「全局」二字** —— 属于推断,
若用户本意是逐病种,本条裁定要推翻(改动面:JSON 顶层 2 键 → 13 行各 2 键;
`BindResult` → `DiseaseRegistryEntry`;`DiseaseVitalsService` 从全局读 → 按 `entry` 读)。

---

## 4. 测试

**没写。未跑。**

- 新增测试文件:**0 个**。`unity/Assets/Tests/EditMode/DiseaseSimulation/` 下无本轮新文件。
- 要求覆盖的三条(「配置为 0 ⇒ 不求值」「负值 ⇒ schema 拒收」「字段往返」)**全部缺失**。
- **未跑任何测试**,未跑 Unity batch(按指令)。

⇒ 按本项目口径,本轮所有判据一律 **`NOT-RUN`**,不得计入任何「已实现」面。

---

## 5. 明确未验证项(禁借绿)

1. **编译未验证** —— 本轮改了 4 个 C# 文件,一次都没编译。
   最可能出问题的地方:`DiseaseRegistryBinder` 里我用了 `ReadInt(root, ..., 0)` 的**四参重载**
   (带缺省值),而 `ReadInt` 的既有签名我只看到三参调用形态(`ReadInt(node, "id", errors, where)`)
   与 `ReadInt(node, "trauma_half_life", errors, where, 0)` 五参形态 —— **缺省值重载是否存在未经确认**。
   若不存在 ⇒ **编译红**。
2. **`BindResult` 构造签名变更的连锁面未查** —— 我把构造从 3 参改成 5 参。
   `DiseaseRegistryBakePipelineTest` / `cooked_drift_all_artifacts_test` 等若直接 `new BindResult(...)`
   ⇒ 编译红。**未 grep 全部调用点**。
3. **`R1_CHECK_36` 编号冲突未查** —— 我按「现网最大 35 → 取 36」编号。
   未确认全库是否已有人在别处用过 36。
4. **cooked 布局未改** —— `DiseaseRegistryCookedWriter` 不写这两个字段 ⇒ **烘焙产物丢值**。
   即第 2 节的断点 ②-b,未修。
5. **`CompositionRoot` 未改** —— 第 2 节的断点 ②-a,未修。生产装配路径上这两个值**恒为 0**。
6. **痊愈支在 `W > 0` 后仍不成立**(`LowPositionHeldTicks` 恒 0)—— 见第 2 节末。
   这一条**不是**「等数值轮」,是**另一个独立的实现缺口**。
7. **`W` 为偶数这条 GDD 约束我实现了守卫,但没验证「偶数」在 `ComputeTrendBackward` 里
   真的被正确消费** —— 该函数注释说「须为偶数(取 `W/2` 做中心差分)」,但它实际用的是
   `trend_backward = (P(t) − P(t−W))/W`,**根本不做 `W/2`**。⇒ 守卫守的约束与消费者用的语义
   **可能对不上**(守卫说「偶数」,消费者对奇数其实也能算)。**这一处我没有结论,标为疑点。**
8. **GDD 未改、`F4OutcomeEvaluator.cs` 未改** —— 这两条约束遵守了。

---

## 6. 载体裁定与校验编号(速查)

- **载体:全局**(顶层 2 键)。GDD 依据 = Tuning `:1564` 段首「全局旋钮」+ `:1737` 段标题
  「噪声与读数」+ R1.3 字段表无此二键。**属推断,非 GDD 明文。**
- **新增校验编号:`R1_CHECK_36`** —— 守:① `W ≥ 0`(放行 0 作未配置);② `W` 为偶数;
  ③ `TREND_EPS ≥ 0`(放行 0)。**GDD 对应条** = Tuning §四 `:1743-1744`;
  但「放行 0」一条**GDD 无对应,是本轮的工程约定**(已在常量注释里明写)。
