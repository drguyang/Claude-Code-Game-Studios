# T22 批八 —— 炮制三分量接真值(`TD-processing-09`)证据原件

**日期**:2026-11-11
**批次**:T22 批八(用户「开工 T22 批八」→ 选 **甲 · 炮制环境修正**;两项裁定:**EnvMod 语义 = 甲 · 加法增量**、**接线范围 = 乙 · 再补生态区基线**)
**债**:`TD-processing-09`(`open` → `closed`)

---

## 0. 开工复勘:债册的三条实测,今天两条已不成立

| 债册 §实测 | 开工实测 | 结论 |
|---|---|---|
| ① `ComputeEnvModRaw` 零生产调用 | 仍零调用 —— 但**它本身是错的**(乘性中性 + 凭空系数) | 语义须改,不是「接上就行」 |
| ② `EnvModClimate` / `EnvModClinic` 零生产赋值点 | 仍零 —— 因为 `new ClinicEnvironmentMemo` 生产上零构造 | 须补装配点 |
| ③ `new ClinicEnvironmentMemo` 生产零命中 | **本批改掉了**(`CompositionRoot` 的 `if (structures != null)` 段) | — |
| ④ `WeatherRoll` 无运行期消费方 | **仍成立**(ADR-038 §D7 未变) | ⇒ 气候分量今天只能吃**生态区基线**这一项 |

⇒ 本批 = 语义改判(用户裁定甲)+ 数据源与装配(裁定乙),**不**碰天气链。

---

## 1. 用户两项裁定

| # | 裁定 | 内容 |
|---|---|---|
| ① | **EnvMod 语义 = 甲 · 加法增量** | 中性 = **0**(不是 `Fix.One`)、**可为负**、**零钳制**;输出改名 `EnvModDelta`(方法 `ComputeEnvModDelta`) |
| ② | **接线范围 = 乙 · 再补生态区基线** | 给 6 的生态区造「气候 → EnvMod 分量」表(新字段 + 烘焙 + ConfigVersion 变),使 5 的 `g(·)` 至少有一项**真有数据源** |

---

## 2. 改了什么

### 2.1 5 的语义(`Sim/EnvMod.cs`,重写)

**旧(已删)**:`ComputeEnvModRaw` = `1 × 生态区基线 × Fix(kind*1000+intensity) × (夜晚 ? 0.9 : 1) × Fix(1 + season*0.05)`。
三处问题:量纲错(乘性 vs F1 的加法)/ 公式无出处(`OQ-5-4`「未定」,值归用户)/ 中性名与 24 的 `(0,0)` 不同数。

**新**:

```csharp
public static Fix ComputeEnvModDelta(in EnvModInput input) => input.EcozoneBaseline;
```

- `Neutral => Fix.Zero`(与 24 的中性出口同数);
- **其余四项(天气 kind/强度、昼夜、季节)保留入参形状但不消费** —— `g(·)` 的映射形状是
  `OQ-5-4` 未裁面,**不得由实现轮自造系数**;
- 零 `clamp`(B-3 / AC-5-19 由「函数体里没有钳制」结构性地满足)。

### 2.2 6 的生态区气候基线(`world_ecozones` schema 1 → 2)

| 件 | 改动 |
|---|---|
| `assets/data/world_ecozones.json` | 根加 `default_climate_env_mod`;每区加 `climate_env_mod` —— **由关卡工具导出,非手写**(ADR-022 §二) |
| `Editor.Tools.Level/EcozoneSource.cs` | `EcozoneZoneSpec.ClimateEnvMod`(必填,无默认值)+ `EcozoneSource.DefaultClimateEnvMod` |
| `Editor.Tools.Level/LevelExporter.cs` | `EcozonesSchemaVersion = 2u`;写出两个新字段(`Fix` 写字符串) |
| `P0ClinicLevel` / `ScaffoldSecondLevel` | 声明处显式传 `Fix.Zero` |
| `Editor.Tools.Bake/WorldEcozonesBinder.cs` | 白名单 + `climate_env_mod` 走 `ReadRequiredFix`(FixParse);根键同理 |
| `Editor.Tools.Bake/WorldEcozonesCookedWriter.cs` | 载荷 = `i32 count; i64 default; count × { id, vcount, verts…, i64 climate }` |
| `Gameplay.Presentation/WorldEcozonesCookedCodec.cs` | `ExpectedSchemaVersion = 2u`;读两个新字段;`WorldEcozoneRow.ClimateEnvMod` / `DataSet.DefaultClimateEnvMod` |

**数值 = 0**(中性占位)。取值归**数值轮**,本批只建**数据位与链路**。
⚠️ 这不是「该区气候无影响」的裁定 —— 是「值未裁时的中性占位」。

### 2.3 运行期取值链

| 件 | 改动 |
|---|---|
| `Gameplay.Presentation/EcozoneClimateBaseline.cs`(**新**) | 「格 → `EcozoneOf` → 该区基线」;**无生态区 ⇒ 全局默认**(AC-5-12,**非最近区**);**多边形表有而气候表没有 ⇒ 抛**(不降级为默认) |
| `Gameplay.Presentation/DataCookedDisk.cs` | 新增 `LoadClinicContext` / `LoadClinicAdj`(此前只有 provider 面能取到) |
| `Gameplay.Boot/CompositionRoot.cs` | `LoadEcozoneRegistry` 拆出 `BuildEcozoneRegistry(data)`(同一份产物装载一次);`if (structures != null)` 段构造 `ClinicEnvironmentMemo`(八件全齐)+ `EcozoneClimateBaseline` |
| `Gameplay.Boot/CompositionRootServices.cs` | 服务袋新增 `ClinicEnvironment` / `EcozoneClimate`(同 null 性) |
| `Gameplay.Boot/DriverProcessingCommandSink.cs` | 新 `ProcessingEnvMods` 结构 + 可选 `Func<WorldPos, ProcessingEnvMods>` 缝;`Ignite` 用真值;缝为 null 才退回中性 0 **并告警** |
| `Gameplay.Boot/BootRoot.cs` | `BuildProcessingEnvMods()`:① 取值前补齐 `Structure*` 失效(EC-24-05 的缓存纪律)② 24 的两分量取 `memo.GetDto()` ③ 5 的分量取 `EnvMod.ComputeEnvModDelta(climate.Of(炮制格))` |

⚠️ **24 的缓存失效必须补**:本工程的驱动各扫各的流(无中央事件泵),若不在取值前把「键新于
`memo.CachedKey`」的结构事件喂一遍,memo 会在**第一次**求值后永远命中 —— 玩家造完家具后
炮制仍按旧布局算(静默陈旧值)。

---

## 3. 判据映射

| 判据 | 载体 |
|---|---|
| `TD-processing-09` 解除动作 ①(语义裁定) | 用户裁定甲 + `Sim/EnvMod.cs` 重写 |
| ②(按裁定改 5) | `ComputeEnvModDelta` + 8 条 `EnvModRawTest` |
| ③(5 的运行期输入链) | `EcozoneClimateBaseline` + `world_ecozones` schema 2;天气 / 季节 / 昼夜**仍无值**(如实) |
| ④(24 的 memo 落生产构造点) | `CompositionRoot` 的 `if (structures != null)` 段 |
| ⑤(三分量均来自各自系统 + 夹具钉「非中性输入改变产出」) | `processing_ignition_sink_test` 的 `test_ignite_nonNeutralMods_changeCraftOutputQuantity` + `test_ignite_envModsSeam_receivesToolCell` |
| AC-5-12(无生态区 ⇒ 全局默认,非最近区) | `test_climate_ofCellOutsideEveryZone_returnsGlobalDefault_notNearest`(两区中点格) |
| AC-5-19 / 规则六(5 侧零钳制、负值原样) | `test_envmodDelta_negativeBaseline_passesThroughUnclamped`(−0.9 原样) |
| `OQ-5-4` 未裁面不得自造系数 | `test_envmodDelta_undecidedInputs_doNotInventCoefficients` |

---

## 4. 证伪轮(五枪,每枪钉一条**不同**的性质)

| 枪 | 变异点 | 定向套件 | 结果 |
|---|---|---|---|
| **M1** | `EnvMod.ComputeEnvModDelta` → `Fix.One`(退回乘性中性) | `EnvModRawTest` | **5 红**(中性 / 全局默认 / 负值原样 / 加法合成 / 未裁项) |
| **M2** | `ComputeEnvModDelta` → `基线 + (IsNight ? 1 : 0)`(自造昼夜系数) | `EnvModRawTest` | **1 红**(`undecidedInputs_doNotInventCoefficients`) |
| **M3** | `EcozoneClimateBaseline.Of` 的兜底判据极性反了(`!IsValid` → `IsValid`) | `EcozoneClimateBaselineTest` | **3 红**(区内 / 区外 / 错配) |
| **M4** | 错配分支**降级为返回全局默认**(不抛) | `EcozoneClimateBaselineTest` | **1 红**(`zoneInPolygonTableButMissingHere_throws`) |
| **M5** | `Ignite` 拿到真值后**丢弃**,仍传 `ProcessingEnvMods.Neutral` | `ProcessingIgnitionSinkTest` | **1 红**(`nonNeutralMods_changeCraftOutputQuantity`) |

**还原**:五枪全部以「变异前备份 cp 回 + SHA-256 逐字节比对」还原(`git checkout` 全程未用),
终态三文件哈希逐个一致(`9deff599…` / `b6ab11c1…` / `7bd703ab…`)。

### 4.1 M4 首枪**打错靶**(如实登记)

| 版本 | 变异 | 结果 | 错在哪 |
|---|---|---|---|
| v1 | `if (idx < 0)` → `if (idx < -1)` | **0 红** | `List.BinarySearch` 未命中返回的是**补码** `~insertionPoint`,不是 `-1`。本例 `insertionPoint = 1` ⇒ `idx = -2`,而 `-2 < -1` 仍成立 ⇒ **变异等价于原实现**,一条判据都没被触发 |
| v2(终版) | 错配分支改成 `return _globalDefault;`,下面的 `throw` 挪进 `if (idx < -99999)` | **1 红** | ✓ |

⇒ 教训与前几批同型:**「改一个比较符」不等于「改掉了被钉的行为」**;变异必须先想清它
在**什么输入**上与原实现分叉,否则 0 红只能说明「没打到靶」,不能说明「夹具没牙」。

⚠️ **未做的证伪面(不得借绿)**:`BootRoot.BuildProcessingEnvMods`(缓存失效补齐 + 两分量组装)
**无任何夹具**,故没有对应的变异枪 —— 它今天只有「代码在」这一条证据。

---

## 5. 回归(三元组)

| 套件 | 结果 | 截至时刻(UTC) | 批次 | commit |
|---|---|---|---|---|
| EditMode | `7437 / 7400 / 0 / 0 / 37` | `2026-11-11T02:07:38Z`–`02:10:17Z` | T22 批八 | `5d15dbdd` + 工作树 |
| PlayMode | `380 / 375 / 0 / 2 / 3` | `2026-11-11T02:10:33Z`–`02:11:36Z` | T22 批八 | `5d15dbdd` + 工作树 |

基线(T22 批七):EditMode `7428 / 7391 / 0 / 0 / 37` · PlayMode `380 / 375 / 0 / 2 / 3`。
**等式**:EditMode `7437 − 7428 = 9` = 本批新增 `[Test]`(EnvModRawTest +2 / EcozoneClimateBaselineTest +5 / ProcessingIgnitionSinkTest +2)。
**PlayMode 与基线逐项相同**(total / passed / failed / inconclusive / skipped 全等)⇒ 零回归。

---

## 6. 债册

| 动作 | 结果 |
|---|---|
| `TD-processing-09` | **`open` → `closed`**(附闭合记录) |
| `debtcount --check` | exit 0 · 条目 **207**(结案不加条目)· closed **174 → 175** · open **23 → 22** · **未闭 P0 面 12 → 11** · `processing` epic 清零(7/31 → 6/31) |

⚠️ **关闭的边界**:见 §7 —— `g(·)` 的其余四项、气候基线数值、`BootRoot` glue 的装配级判据
都不在本次关闭面内。

---

## 7. 本批没有做的(不得借绿)

| 项 | 状态 |
|---|---|
| **`g(·)` 的五项映射** | 只实现「基线项透传」。天气 / 昼夜 / 季节的**系数形状**=`OQ-5-4`,**未裁**;天气本身连运行期取值链都没有(`WeatherRoll` 零生产调用) |
| **气候基线数值** | 每区与全局默认**都是 0**(中性占位)。真实气候差异归**数值轮**,须由关卡工具改作者态后重烘(改值 = 改 `ConfigVersion`) |
| **`BootRoot.BuildProcessingEnvMods` 的装配级判据** | **无**(与 42 的其它 `OnTickEdge` 接线同状态)。本批钉的是「缝被调用 / 格对 / 非中性输入改变产出」三条,不是「真启动序里三分量取到了真值」 |
| **`EnvModInput` 的四个未消费字段** | 保留为 F-5.4 的入参形状;`OQ-5-4` 裁定前它们**不得**被赋予系数 |
| **天气 / 季节链** | 不在本批面内 |
