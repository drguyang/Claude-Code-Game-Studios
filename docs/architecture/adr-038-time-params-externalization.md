# ADR-038 — 时间参数外部化(5 的数据集)· 跨表引用形式 · 存档 v4

| Field | Value |
|-------|-------|
| **Status** | Accepted(2026-11-03 · 用户裁定四事:范围 = A+B 一起做 · 存档头**记三个**时间参数 · 52 四值取 `6 / 2 / 6 / 6000` · 开工按三批;§Decision **D5–D8** 由本轮就地裁定,未经用户逐条确认 —— 同 ADR-034 / 037 口径,实现轮之前可改) |
| **Date** | 2026-11-03 |
| **Supersedes** | 无。**但就地取代** ADR-005 / ADR-014 隐含的「时间参数是编译期常量」读法 |
| **Related** | ADR-014 §二/§五(两阶段烘焙 · `IDataProvider`)· ADR-010 §一/§七(存档头 · ConfigVersion 非致命)· ADR-037(存档 v3 逐产物版本)· ADR-034(载荷段,迁移先例)· ADR-005(确定性)· GDD `time-and-weather.md` AC-5-05 / AC-5-11 · GDD `random-events.md` DC-3 |

---

## Context

### 1. AC-5-05 的 `THEN` 有两半,今天只机械化了第一半

AC-5-05 原文(`time-and-weather.md:668`):

> 构建脚本读取 **52 的数据集**,断言其**引用解析后与 5 同值**且 **52 数据集中零字面副本**;不一致 ⇒ 硬失败。

2026-11-03 已立的 `TimeConstantSingleDefinitionGate` 只覆盖**代码侧定义点唯一**
(`.cs` 里恰一条声明且必须落在 `Sim/World/TimeBase.cs`)。**数据侧那半无从判定**:

- 52 **没有数据集**(`assets/data/` 里 52 侧零文件);
- **跨表引用形式**在 ADR-014 里没有定义 —— 5 怎么导出、52 怎么写引用、构建期怎么解析,全空白。

### 2. 全仓曾同时存在**三份**时间实现,其中一份正是为数据路线准备的

2026-11-03 实测(`git log --follow` 可复算):

| 落点 | 出身 | 日长形态 | 消费面 |
|---|---|---|---|
| `Sim/TimeBase.cs`(`DaYiJingCheng.Sim`) | Story 001 `8231a515` | `TimeParams` **字段,自陈「从烘焙数据装载」** | `TimeParams` **零消费方**;`ComputePhase/ComputeSeasonIndex/ComputeState/TimeState` 零消费;**仅 `FDiv` 被 52 的 `WeatherRoll:59` / `EventRng:209` 使用** |
| `Sim/World/TimeBase.cs`(`DaYiJingCheng.Sim.World`) | `c291873d`「解决 stash pop 冲突并新增 TimeBase」 | `const`(480 → 36000) | 唯一有生产消费的实现(`BootRoot:1706/1710`) |
| `Sim/Time/WorldTimeConstants.cs` | 本轮我造的 | `const 36000` | 已删除(`50f17a7d`) |

⇒ **设计意图(数据驱动)与落地现实(编译期常量)分叉**,而 `TimeParams` 这个为数据路线准备的
形状从未接上管线。本 ADR 把这条分叉收口。

### 3. AC-5-11 今日 NOT-RUN 的根因是没有装载路径

AC-5-11 要求「装载期校验器,注入五种非法配置,每一种都硬失败」。`TimeParams.Validate()` 与
`WeatherParams.Validate()` **已存在**(`Sim/TimeBase.cs:29-39` · `Sim/WeatherRoll.cs:27-28`),
但它们**没有生产调用方** —— 因为没有装载。走数据路线后校验器才第一次有执行体。

### 4. 日长一变成运行时数据,就和存档撞上了

现在是 `const`,存档天然无歧义。变成数据后,读一份「用 36000 记下的档」而当前构建装载 18000 ⇒
日边界与事件预算全错位,而 ADR-010 §七 明写 `ConfigVersion` 不匹配**非致命**(只记日志)。
这是静默腐蚀,必须在存档层封死。

**为什么 `NIGHT_START` / `NIGHT_SPAN` 也要一起封**:`isNight` 是 52 的 `TODMult` 的输入
(`random-events.md:623` 依赖表 · `:878` 乘子表),即**掷骰输入**。只冻日长而放任昼夜参数漂移,旧档重放出的世界
同样与玩家经历的不同。⇒ 三个量必须同进同出。

---

## Decision

### D1 — 时间参数外部化:`assets/data/5_time.json`,**只放已裁定的三值**

| 字段 | 值 | 来源 |
|---|---|---|
| `ticks_per_day` | **36000** | 用户 2026-11-03 |
| `night_start` | **27000** | 用户 2026-11-03 |
| `night_span` | **9000** | 用户 2026-11-03 |
| `ticks_per_season` | **不进 schema** | 值未裁(`time-and-weather.md:517`) |
| `seasons_per_year` | **不进 schema** | 值未裁(`:518`) |
| `weather_block_ticks` | **不进 schema** | 值未裁(F-5.3 无落点) |

**⚠️ 为什么不用占位值把它们塞进 schema**:占位值与「已裁定值」在数据文件里**同形**,
而 `TimeParams.Validate()` 只校验关系(`> 0` / `∈ (0, TPD)`),占位值天然过校验
⇒ 一旦有占位,「这个数是不是裁过」就再也无从分辨。这正是 `480` 那次事故的形态
(其注释谎称「GDD 裁定 480」)。⇒ **宁可缺字段**,由 5 侧在缺失时显式 throw。

**后果(如实)**:AC-5-11 的五条非法注入里,`TICKS_PER_SEASON = 0` / `SEASONS_PER_YEAR < 2` /
`WEATHER_BLOCK_TICKS = 0` 三条**今日不可达**(对应字段不在 schema 里)。它们仍可用**直接注入
`TimeParams` / `WeatherParams`** 的方式写夹具(AC 原文就是「注入」),故三条**可以**闭合;
但「由装载路径触发」这一层对三条是 NOT-RUN。**不得**把「夹具能注入」写成「装载路径已覆盖」。

`TimeBase.ComputeSeasonIndex` 里的 `× 30` 与 `4` 两个字面量**保持占位**并保留现有登记 ——
本 ADR 不替未裁定的值做决定。

### D2 — 跨表引用形式 = `{"$ref": "<artifact>.<field>"}`

52 侧需要日长时,**不得**写字面数字,只能写:

```json
{ "ticks_per_day": { "$ref": "5_time.ticks_per_day" } }
```

**判据(构建期,阶段 2)**:

1. schema 规定 `ticks_per_day` 字段**只接受**对象形 `{"$ref": …}`,**恰一个键**。
   出现数字 / 字符串 / 多键对象 ⇒ **烘焙失败**(`零字面副本` 的机械表达)。
2. `$ref` 解析:目标 artifact 必须**在同一次烘焙批里已产出**(按依赖序),否则**烘焙失败** ——
   不得回退到默认值或跳过。
3. 解析后**逐值比对**:52 解析到的值必须与 `5_time` 产出的值**逐位相等**,否则**烘焙失败**。

**为什么不是「同名字段约定」**(52 自己写 `ticks_per_day: 36000`,靠构建脚本去比):
那正是 AC 明令禁止的**字面副本**。它今天比得上,明天改 5 的值而 52 忘了改就漂移,
而漂移的判据恰好是「有人记得同步」—— 那是纪律,不是门。

**为什么不引入通用 `$ref` 语法**(类型检查、数组引用、表达式):本仓库只需要**一个量**的
引用。通用语法是给还不存在的问题建基础设施(与 ADR-019 驳回 Alt 3 同型)。

### D3 — 存档 **v4**:头部追加三个时间参数

**头部字段序**(`SaveHeader` 的黄金夹具随之重签):

```
magic(4) · SHA256(32) · SaveVersion(u32) · WorldSeed(u64) · ConfigVersion(u32)
· tick(i64) · SnapshotOffset(u64)
· TicksPerDay(i64) · NightStart(i64) · NightSpan(i64)      ← v4 新增,共 24 字节
```

- `CurrentSaveVersion`: **3 → 4**。`PayloadSegmentMinVersion` **保持 2**(v1 仍拒收)。
- **迁移 `SaveMigrationV3ToV4`**:在旧 `FixedSize` 处插入 `36000 / 27000 / 9000`,版本写 4。
  正文**一字节不动** ⇒ `SaveMigrationChain.TailBody` 不变量通过(与 v2→v3 同一手法)。
  **写 `36000` 是记录事实而非伪造**:480 从未出货、36000 是唯一曾存在于真实存档的值;
  今日零真实存档(`ADR-030 R3` 同族事实),该路径实际不会被走到。
- **读档语义(hydrate)**:装载完成后,运行时 `TimeParams` 的 `TICKS_PER_DAY / NIGHT_START /
  NIGHT_SPAN` **以存档头为准覆盖数据集值**;数据集值只在**新局**生效。
- **失败语义(fail-closed)**:头部三值任一不合法(`TPD ≤ 0` / `NightSpan ∉ (0, TPD)` /
  `NightStart ∉ [0, TPD)`)⇒ **拒收该档**,不得回落数据集值。
- **与数据集不一致**:允许,但**记一条 drift 日志**(复用 `ConfigVersionDriftRecord` 的口径),
  并**继续用存档的值** —— 这正是不漂移的保证。

### D4 — 52 数据集:`assets/data/52_random_events.json`,四值 `6 / 2 / 6 / 6000`

| 字段 | 值 | 约束 |
|---|---|---|
| `window_size` | **6** | `≥ 1` |
| `base` | **2** | `≥ 0`,`≤ budget_max` |
| `budget_max` | **6** | `≥ base` |
| `roll_interval` | **6000** | `> 0` 且 `ticks_per_day % roll_interval == 0` |
| `ticks_per_day` | `{"$ref": "5_time.ticks_per_day"}` | D2 |

`36000 % 6000 == 0` ✓(一个游戏日 = 现实 30 分钟,每 5 分钟看一次,一日至多 6 条)。

**⚠️ 新增边界(本 ADR 抓到的一条)**:日长在 v4 后**可由存档覆盖**,而
`roll_interval` 仍来自当前构建的数据集 ⇒ **两者可能不整除**。
处置:读档 hydrate 后**重跑一次整除断言**,不整除 ⇒ **拒收该档**
(GDD `:1075` 的「构建期报错」只覆盖构建期;运行期的对应判据此前**不存在**)。

### D5 —(就地裁定)`TimeParams` 的落点:留在 `Sim`,不进 `Sim.Contracts`

理由:`TimeParams` 含 `Validate()` 的**领域规则**(`NIGHT_SPAN ∈ (0, TPD)` 等),那是 sim 的语义;
`Sim.Contracts` 只放跨装配共享的**形状**。数据集类型 `TimeParamsDataSet`(纯字段 struct)进
`Sim.Contracts` 以满足 `IDataProvider.Load<T>() where T : struct`;`TimeParams` 留在 `Sim` 并在
装载后由 `Gameplay.Boot` 组装。门 A(`Sim` 引用集恰 = `{BCL, Sim.Contracts}`)不受影响。

### D6 —(就地裁定)删除 `Sim/TimeBase.cs`,`FDiv` / `FMod` 迁入唯一实现

`Sim/TimeBase.cs` 的 `TimeParams` 被保留并成为唯一参数载体后,该文件**其余部分**(`TimeBase`
静态类、`TimeState`)**与 `Sim/World/TimeBase` 完全重复**,且零生产消费。

- `FDiv` / `FMod` 只保留 **`Sim.World.TimeBase` 一份**;52 的 `WeatherRoll:59` /
  `EventRng:209` 改指向它。
- **⚠️ 行为差异须就地选一**:`Sim.TimeBase.FDiv` 抛 `ArgumentException`,
  `Sim.World.TimeBase.FDiv` 抛 `DivideByZeroException`。**取后者**(语义更准:`b == 0` 就是除零),
  并把「改异常类型」写进迁移清单 —— 现有夹具若断言前者须同步改。
- `TimeState` 取 `Sim.World.TimeBase` 的三字段版本(唯一有形状需求的一方在 `ComputeState`)。

### D7 —(就地裁定)52 的数据集**今日零生产消费**,如实登记

`EventRng.ComputeWindowStart` **零生产调用点**(只有夹具);
`RandomEventsDirectorDriver` 尚未消费任何 52 配置。⇒ 本次落地的 52 数据集是**引用形式的载体**,
不是「跑起来的配置」。**不得**把它写成「52 已数据驱动」。
52 的运行期消费归 52 的实现轮;本 ADR 只保证「一旦有人读,读到的必须是 5 的同值」。

**🔁 2026-11-03 收口轮的部分推进(原文不删)**:`ROLL_INTERVAL` 已有**一个真实的生产消费点**
—— `BootRoot.RequireCrossDatasetWindow` 在启动期读它,与 5 的日长做整除校验(R2);
这条校验也在读档 hydrate 时重跑。**其余三值**(`WINDOW_SIZE` / `BASE` / `BUDGET_MAX`)
仍零消费 —— 它们要等 52 的预算 / 抽池主循环(`EventBudget.ComputeDailyBudget` 同样零生产调用点),
归「52 运行期消费」ADR,不在本件面内。

### D8 —(就地裁定)AC-5-05 门的补洞

2026-11-03 立门时,`DefinitionPattern` 要求「类型 + 名字 + `=`」,因此**看不见**
`public long TICKS_PER_DAY;`(无初始化声明)—— 而 `Sim/TimeBase.cs:23` 正是这个形态。
**假绿**。本 ADR 要求把判据扩为「`=` 或 `;` 二者皆可」,并在 D6 删除该文件后仍保持转红能力
(夹具须含一条「无初始化声明 ⇒ 红」)。

---

### D9 — Amendment A(2026-11-03 用户裁定):季 / 天气四值同期外置,并存档 v5

**裁定**:AC-5-11 的三条季 / 天气参数要做到**与时间三值同层** —— 外置 + 装载期校验
+ **进存档**。理由是 D3 的同一条:这四个量都是**模拟 / 掷骰的输入**(季长 → 17 的采集;
天气块长 → 5 的天气掷骰),不随档走 ⇒ 旧档重放出的世界与玩家经历的不同,
而 `ConfigVersion` 不匹配按 ADR-010 §七 **非致命**。

**值**(用户同日裁定):

| 参数 | 值 | 说明 |
|---|---|---|
| `TICKS_PER_SEASON` | **252000** | = 7 × 36000(7 日/季;约 3.5 小时实时) |
| `SEASONS_PER_YEAR` | **5** | 与 17 的 `SEASON_MULT[]` 表长相等(`AC-17-16` 要求);含长夏 |
| `WEATHER_BLOCK_TICKS` | **6000** | = 5 分钟实时,且整除 36000 |
| `INTENSITY_MAX` | **100** | 强度上界 |

**形状**:
- `5_time.json` **schema_version 2**:追加 `ticks_per_season` / `seasons_per_year`;
  烘焙产物载荷 24 → **40 B**。
- 新产物 `5_weather.json` → `5_weather.cooked.bytes`(载荷 16 B,两值)。
  天气是 5 的**另一张表** ⇒ 单表单文件,不塞进 `5_time.json`。
- 存档 **v5**:头部在 v4 的 24 B 之后**再追加 32 B**(四值 i64);
  `FixedSizeV5 = 92` / `HeaderSizeV5 = 124`;新增 `SaveMigrationV4ToV5`(插 32 B + 写版本 5)。
- `BootRoot.ResolveWeatherParams` 与 `ResolveTimeParams` 同口径(非法 ⇒ 启动期硬失败)。
- `RequireTimeParams` 扩到七值;`SaveTimeParams` 扩到七值;
  `IsSpecified` **只判非零**、不判关系 —— 关系归 `Validate()` / `ValidateSeason()` /
  `WeatherParams.Validate()`。两层分开的理由:否则「值非法」会伪装成「没给值」,
  归因指向错误的修复方向(实现时实测踩到)。

**同时结清的两件**:
- **R2 的端到端可达性**:整除判据并入 `HydrateTimeParams(loaded, rollInterval)`,
  并新增启动期 `RequireCrossDatasetWindow`(读 52 的 `ROLL_INTERVAL`)——
  52 数据集自此有**一个真实的生产消费点**(其余三值仍待「52 运行期消费」ADR,见 D7)。
- `AC-5-11` 五条的装载路径层:①–④ 经 `ResolveTimeParams`、⑤ 经 `ResolveWeatherParams`,
  每条都有「直接注入」与「经装载路径」两条夹具。

**未做(不得借绿)**:天气两值在**运行期仍无消费方**(`WeatherRoll` 无生产调用点)——
存档记录它们是为「一旦接线即可重放」,不表示天气已跑起来。

## Consequences

- **正面**:时间参数第一次有单一数据源与装载路径;AC-5-11 首次可执行;AC-5-05 第二半有**可落地**
  的判据形状(等 52 实现轮接上即生效);`DataCoreAddresses` 22 → 24(+`5_time` +`52_random_events`),
  全局 `ConfigVersion` 随之变化(ADR-037 的逐产物段能指出是哪两份)。
- **负面**:
  - 存档布局再变一次(第三次),黄金夹具重签;
  - 启动路径新增两份产物的装载与解码(体量都是几十字节,可忽略);
  - `BootRoot` / `WeatherRoll` / `EventRng` 三处签名变化。
- **中性**:`TICKS_PER_SEASON` / `SEASONS_PER_YEAR` / `WEATHER_BLOCK_TICKS` 仍无值 —— 本 ADR
  **不解决**它们,只保证它们落地时走同一条管线。

---

## Alternatives

| # | 方案 | 否决理由 |
|---|---|---|
| Alt 1 | **维持编译期常量**(不补数据集) | AC-5-05 第二半永不可判;AC-5-11 永无装载期;`TimeParams` 永远死代码。且用户已裁数据路线 |
| Alt 2 | **只补 5 的数据集,不补 52** | AC-5-05 的 `THEN` 读的是 **52 的数据集**;只补 5 等于判据没有宾语 |
| Alt 3 | **给 52 建一份「只有 `$ref`、不放四值」的表** | 四值全在「待裁」时这是唯一诚实的形态,但 schema 会把残缺表当非法产物 ⇒ 要么放宽 schema(等于给门开洞),要么伪造值。用户已裁四值,此案不再必要 |
| Alt 4 | **存档头只记 `TICKS_PER_DAY`** | `isNight` 喂 52 的 `TODMult`(掷骰输入)⇒ 只封一半。用户已裁「三个全记」 |
| Alt 5 | **v3→v4 迁移写「未知」,载入时按 36000 解释** | 引入「未知」这一第三态,而该路径今日不可达 ⇒ 为一个走不到的分支增加状态空间。写 36000 是事实 |
| Alt 6 | **通用 `$ref` 语法**(类型化、数组、表达式) | 只需一个量的引用;通用语法是给不存在的问题建基础设施(同 ADR-019 驳回 Alt 3) |
| Alt 7 | **日长仍为 const,只把 52 的引用做成构建期断言** | 那条断言只需比对两个 `const` —— 门已有;数据路线被架空。用户已裁数据路线 |

---

## Risks

| # | 风险 | 处置 |
|---|---|---|
| **R1** | **读档 hydrate 覆盖面不全** —— 若某处仍在用数据集的值而非存档值,则会漂移 | hydrate 后**单一入口**:`TimeParams` 由 `BootRoot` 建一次并注入所有消费者;夹具断言「读档后 `TimeParams` 三值 == 存档头三值」 |
| **R2** | **`roll_interval` 与存档日长不整除**(D4 的边界) | 读档后重跑整除断言 ⇒ 拒收。**今日不可达**(只存在过 36000),但判据必须在 |
| **R3** | **迁移写 36000 是事实,但未来升 v5 时会被误读成「当时可能有别的值」** | ADR 正文与本条明确记录:480 从未出货 |
| **R4** | ~~`AC-5-11` 三条参数的「装载路径」层仍 NOT-RUN~~ → ✅ **2026-11-03 Amendment A 结清**:季/天气四值已进 schema 与装载路径,五条全部可经 `ResolveTimeParams` / `ResolveWeatherParams` 触发 | 每条都有「直接注入」+「经装载路径」两条夹具 |
| **R5** | **`Sim/TimeBase.cs` 删除后,`FDiv` 异常类型变化**可能让某条既有夹具变红 | D6 要求同批改夹具并在提交里点名 |
| **R6** | 52 数据集**零生产消费**(D7),易被后续读者当成「52 已接数据」 | D7 已明写;数据集文件头同款声明 |

---

## ADR Dependencies

| 关系 | ADR | 说明 |
|---|---|---|
| **依赖** | ADR-014 §二/§五 | 两阶段烘焙管线、`*.cooked` 格式、`IDataProvider` 取数形状、`DataCoreAddresses` 与组合 `ConfigVersion` —— 本 ADR 全部沿用,不新造 |
| **依赖** | ADR-010 §一/§七 | 存档头布局与「`ConfigVersion` 不匹配非致命」;本 ADR 的 v4 字段是该头部的第三次扩列 |
| **依赖** | ADR-037 | 逐产物 `ConfigVersion` 段已存在 ⇒ 读档时可精确定位「是 `5_time` 变了」,不必升级该段 |
| **依赖** | ADR-034 | `ISaveMigration` / `SaveMigrationChain.TailBody` 的尾部锚定不变量 —— v3→v4 的可迁移性依据 |
| **依赖** | ADR-005 / ADR-006 | 确定性口径:时间参数进 sim,必须逐位可重建 |
| **被依赖(将来)** | 待立的「52 运行期消费」ADR | 52 的 `WINDOW_SIZE` / `ROLL_INTERVAL` 真正被 `RandomEventsDirectorDriver` 读取时,才会触发该轮 |
| **就地取代** | 无 ADR 被 Superseded | 但本 ADR **取代**「时间参数是编译期常量」这一隐含读法(此前无 ADR 明写,故无正式 Supersede 对象) |

---

## Validation(实现轮前全部未跑,**禁借绿**)

| # | 判据 | 载体 |
|---|---|---|
| V-1 | `5_time` 烘焙产物解码后三值 == JSON 三值(Amendment A 后追加季二值) | `time_params_bake_test` |
| V-2 | `52_random_events` 的 `$ref` 解析后 == `5_time` 的同值 | `random_events_binder_test` |
| V-3 | 52 的 JSON 把 `ticks_per_day` 写成数字 ⇒ **烘焙失败** | 同上(负控) |
| V-4 | AC-5-11 五条逐参数硬失败 | `time_params_validate_test`(5 条独立 `[Test]`) |
| V-5 | 存档 v4 写出三值、读回三值(往返) | `save_v4_time_params_test` |
| V-6 | v3→v4 迁移:三值 = 36000/27000/9000,正文逐位不变,`WorldSeed` 逐位不变 | 同上 |
| V-7 | 读档 hydrate:存档三值 ≠ 数据集三值 ⇒ 运行时取**存档值** + 记 drift | 同上 |
| V-8 | 非法头部三值 ⇒ 拒收 | 同上 |
| V-9 | `saveTPD % roll_interval != 0` ⇒ 拒收(R2) | 同上 |
| V-10 | AC-5-05 门:补「无初始化声明 ⇒ 红」的变异 | `time_constant_gate_test` 扩条 |
| **V-11**(Amd A) | 存档 v5 写出七值、读回七值 | `save_v5_runtime_params_test.test_v5_roundTripsAllSevenValues` |
| **V-12**(Amd A) | v4→v5 迁移写入裁定四值;v3→v5 三级链成立 | 同上(两条) |
| **V-13**(Amd A) | 档头任一季/天气值非法 ⇒ 拒收 | 同上(三条:季长 0 / 年 1 季 / 块长 0) |
| **V-14**(Amd A) | AC-5-11 五条**均经装载路径**触发 | `time_params_validate_test`(5 条 load-path 用例 + 正控) |
| **V-15**(Amd A) | 启动期交叉校验(5 日长 vs 52 的 roll_interval)可注入非法值 | `save_v5_runtime_params_test.test_crossDatasetWindow_*` |

---

## Engine Compatibility

**LOW**。纯 BCL 字节布局、纯 C# 结构、无任何 post-cutoff 引擎 API。
Addressables 侧只新增两个 address 条目(既有机制,`DataCorePreloader` 已跑通 22 份)。

---

## GDD Requirements Addressed

| AC | 本 ADR 的关系 |
|---|---|
| `AC-5-05` | 提供**数据侧的判据形状**(D2 / D8)。**本 ADR 不结该 AC** —— 实现轮的 V-2 / V-3 / V-10 全绿后才可勾 |
| `AC-5-11` | 提供**装载路径**。🔁 **Amendment A(2026-11-03)**:五条**全部**经装载路径触发
(①–④ `ResolveTimeParams`、⑤ `ResolveWeatherParams`)⇒ R4 的「三条只到夹具注入层」**已结清** |
| `AC-52-*` 的数值 | 定 `6 / 2 / 6 / 6000`(用户 2026-11-03);形态侧归 52 的实现轮 |
| `AC-7a-14` | 存档 v4 的迁移走既有链,两条不变量不变 |
