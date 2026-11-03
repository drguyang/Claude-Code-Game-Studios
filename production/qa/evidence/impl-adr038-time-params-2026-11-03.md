# ADR-038 实现轮证据原件 —— 时间参数外部化 · 跨表 `$ref` · 存档 v4

- **日期**:2026-11-03
- **ADR**:`docs/architecture/adr-038-time-params-externalization.md`(Accepted 2026-11-03)
- **用户裁定**(逐条照准):范围 = A+B 一起做 · 存档头**记三个**时间参数(升 v4)·
  52 四值 `6 / 2 / 6 / 6000` · 开工按三批
- **§D5–D8 由实现轮就地裁定**,未经用户逐条确认(同 ADR-034 / 037 口径)

---

## 一、这一轮到底补上了什么

| 缺口 | 补之前 | 补之后 | 判据载体 |
|---|---|---|---|
| AC-5-05 的 `THEN` 第二半(「52 引用解析后与 5 同值且零字面副本」) | **无可判对象**:52 无数据集,跨表引用形式 ADR-014 从未定义 | `assets/data/52_random_events.json` 写 `{"$ref": "5_time.ticks_per_day"}`;烘焙期解析 + 逐位相等断言;**写数字 = 构建期拒收** | `random_events_binder_test`(12 条) |
| AC-5-11 的「装载路径」 | 校验器**零生产调用方**(没有装载) | `assets/data/5_time.json` → cooked → `BootRoot.ResolveTimeParams` → `TimeParams.Validate()` 启动期硬失败 | `time_params_bake_test`(7)· `time_params_validate_test`(7) |
| 日长从 `const` 变数据后与存档撞车 | 读旧档会用当前构建值重放 ⇒ 日边界 / 预算窗口错位,而 `ConfigVersion` 不匹配按 ADR-010 §七**非致命** | 存档 **v4** 头记 `TICKS_PER_DAY / NIGHT_START / NIGHT_SPAN`;读档 hydrate 取**档内值**;非法值拒收 | `save_v4_time_params_test`(10 条) |
| 全仓**三份**时间实现并存 | `Sim/TimeBase.cs`(零消费)· `Sim/World/TimeBase.cs`(唯一生产面)· 本轮新造第三份 | 删 `Sim/TimeBase.cs`;`TimeBase` 唯一实现改为**显式收 `TimeParams`**;常量唯一落 `Sim/TimeParams.cs` | `TimeConstantSingleDefinitionGate`(AC-5-05 代码侧)+ `time_constant_gate_test`(18 条) |

---

## 二、落盘清单(按层)

**作者态数据**
- `assets/data/5_time.json` —— `ticks_per_day=36000` / `night_start=27000` / `night_span=9000`
- `assets/data/52_random_events.json` —— `$ref` + `roll_interval=6000` / `window_size=6` / `base=2` / `budget_max=6`

**编辑期烘焙(Tooling,不进构建)**
- `Editor.Tools.Bake/TimeParamsBinder.cs` · `TimeParamsBaker.cs` · `TimeParamsCookedWriter.cs`
- `Editor.Tools.Bake/RandomEventsBinder.cs` · `RandomEventsBaker.cs` · `RandomEventsCookedWriter.cs`
- `Editor.Tools.Bake/DataBakeMenu.cs` —— 两个 `Bake*` 菜单方法

**契约 / 运行期取数**
- `Sim.Contracts/TimeParamsDataSet.cs` · `Sim.Contracts/RandomEventsDataSet.cs`
- `Gameplay.Presentation/TimeParamsCookedCodec.cs` · `RandomEventsCookedCodec.cs`
- `Gameplay.Presentation/AddressablesDataProvider.cs` · `DataCorePreloader.cs` —— 41 条 address(22 → 41 的累计,本轮 +2)

**sim 侧**
- `Sim/TimeParams.cs` —— 三值 + 两季参数(季参数**未裁定**,不进 schema)
- `Sim/World/TimeBase.cs` —— 唯一实现,全部方法收 `TimeParams`(无全局态)

**存档**
- `Sim.Codec/SaveHeader.cs`(`CurrentSaveVersion = 4`,`TicksPerDay/NightStart/NightSpan`)
- `Sim.Codec/SaveHeaderCodec.cs`(`FixedSize` 保持 36 · `FixedSizeV4 = 60` · `HeaderSizeV4 = 92` ·
  `FixedSizeFor` / `HeaderSizeFor` / `ReadSaveVersion`)
- `Sim.Codec/SaveTimeParams.cs`(写档入参,刻意**无** Legacy 常量)
- `Sim.Codec/SaveMigrationV3ToV4.cs`(在 36 处插 24 B;唯一允许写 `36000/27000/9000` 的生产位置)
- `Sim.Codec/SaveService.cs`(`LastLoadedTimeParams` · 迁移后重读头 · `RequireTimeParams` · 按版本取正文起点)
- `Sim.Codec/SaveSlotProbe.cs`(读 `min(文件长, 92)`;v4 档带三值)
- `Gameplay.Boot/SaveComposer.cs`(`timeParams` 必填,缺 ⇒ 抛)
- `Gameplay.Boot/BootRoot.cs`(`ResolveTimeParams` / `HydrateTimeParams` / `RequireDivisibleWindow`)
- `Gameplay.Boot/CompositionRoot.cs`(注册 `V3ToV4`;写档取不到时间参数 ⇒ `[E-13][persist]`)

**夹具**
- `Tests/EditMode/TimeWeather/time_params_bake_test.cs`(7)
- `Tests/EditMode/TimeWeather/time_params_validate_test.cs`(7)
- `Tests/EditMode/TimeWeather/random_events_binder_test.cs`(12)
- `Tests/EditMode/Boot/save_v4_time_params_test.cs`(10)

---

## 三、§Validation V-1…V-10 逐条

| # | 判据 | 载体 | 状态 |
|---|---|---|---|
| V-1 | `5_time` 产物解码后三值 == JSON 三值 | `time_params_bake_test.test_cooked_onDisk_matchesFreshBake_andDecodes` | **PASS** |
| V-2 | 52 的 `$ref` 解析后 == `5_time` 同值 | `random_events_binder_test.test_ref_resolvesToTheSameValueAs5`(字面量哨兵 36000) | **PASS** |
| V-3 | 52 把 `ticks_per_day` 写成数字 ⇒ 烘焙失败 | `test_literalNumber_isRejected`(前置断言确认替换真的落地) | **PASS** |
| V-4 | AC-5-11 五条逐参数硬失败 | `time_params_validate_test`(1 正控 + 5 负控) | **PASS(见 §五 R4)** |
| V-5 | 存档 v4 写出三值、读回三值 | `save_v4_time_params_test.test_v4_roundTripsTheThreeArchiveValues` | **PASS** |
| V-6 | v3→v4 迁移:三值 = 36000/27000/9000,正文 / `WorldSeed` 逐位不变 | `test_v3ToV4_migrationWritesLegacyValues_andKeepsBody`(正文由链自身断言) | **PASS** |
| V-7 | hydrate 取**档内**值 | `test_hydrate_takesArchiveValues_overCurrentDatasetValues`(档内 18000 ≠ 数据集 36000) | **PASS** |
| V-8 | 非法头部三值 ⇒ 拒收 | `test_load_rejectsZeroTicksPerDay` · `test_load_rejectsNightSpanNotLessThanDay` | **PASS** |
| V-9 | `saveTPD % roll_interval != 0` ⇒ 拒收(R2) | `test_divisibleWindow_*`(1 正控 + 1 负控) | **PASS(纯函数层)** |
| V-10 | AC-5-05 门「无声明 ⇒ 红」 | `time_constant_gate_test.test_gate_redOnZeroDeclarations` | **PASS**(上一轮已立,本轮未改) |

### V-9 的口径限定(不借绿)

`RequireDivisibleWindow` 已接进 `BootRoot.HydrateTimeParamsAfterLoad`,但**今日生产上不可达**:
写档者与读档者用同一份 52 数据(都在 `36000 / 6000`),不整除只在「日长跨构建漂移」时出现。
⇒ 判据存在且可直测,但**没有端到端输入**。这不是缺陷,是 R2 的「今日不可达」原文。

---

## 四、证伪轮(5 条有效变异 + 1 条打错靶)

窗口:2026-11-03 03:47–04:05 CST。每条在**同一条命令内**改 → 跑 → `cp` 还原,
以 **SHA-256 逐字节相同**为还原判据(未用 `git checkout` / `stash`)。

| # | 变异 | 靶夹具 | 结果 | 还原 |
|---|---|---|---|---|
| M1 | `RandomEventsBinder`:字面副本走 `value = n.Int; return true` | `RandomEventsBinderTest` | **`test_literalNumber_isRejected` 红**(11/12 pass) | SHA 相同 |
| ~~M2~~ | ~~`FixedSizeFor` 恒返回 36~~ | `SaveV4TimeParamsTest` | **未转红(打错靶)** —— 正文起点走的是 `HeaderSizeFor`,`FixedSizeFor` 只服务迁移链尾部取段 | SHA 相同 |
| M2b | `HeaderSizeFor` 恒返回 68 | `SaveV4TimeParamsTest` | **2 条红**:`test_v4_roundTripsTheThreeArchiveValues` · `test_v3ToV4_migrationWritesLegacyValues_andKeepsBody`(8/10 pass) | SHA 相同 |
| M3 | `HydrateTimeParams` 优先用 `current` 值 | `SaveV4TimeParamsTest` | **`test_hydrate_takesArchiveValues_overCurrentDatasetValues` 红** | SHA 相同 |
| M4 | `SaveMigrationV3ToV4.LegacyNightSpan` 9000 → 9001 | `SaveV4TimeParamsTest` | **`test_v3ToV4_migrationWritesLegacyValues_andKeepsBody` 红** | SHA 相同 |
| M5 | `RequireDivisibleWindow` 的取模判据置 `false` | `SaveV4TimeParamsTest` | **`test_divisibleWindow_rejectsWhenNotDivisible` 红** | SHA 相同 |

**M2 作废的理由(如实登记,承 `tools/falsify/README.md` 的「变异打错靶」)**:
`FixedSizeFor` 与 `HeaderSizeFor` 是两个**用途不同**的函数 —— 前者给迁移链取「正文从哪开始」,
后者给存档解析取「完整头部多长」。M2 打在前者上,靶夹具走的是后者 ⇒ 变异在靶场里**不可达**,
绿不能读成「判据无效」。M2b 打在正确的函数上即转红。

**SHA-256(还原后)**:
```
1bc7fd11…52112fa  unity/Assets/Sim.Codec/SaveHeaderCodec.cs
75ab954a…f66628d1 unity/Assets/Sim.Codec/SaveMigrationV3ToV4.cs
cc94d13f…dd78af9  unity/Assets/Gameplay.Boot/BootRoot.cs
9093ea2c…5b5cef9  unity/Assets/Editor.Tools.Bake/RandomEventsBinder.cs
```

---

## 五、回归三元组与未做项

### 5.1 三元组

| 套件 | 结果 | 截至时刻 | 批次 | commit |
|---|---|---|---|---|
| EditMode | `6724 / 6687 / 0 / 0 / 37`(total/passed/failed/inconclusive/skipped) | 2026-11-03 11:52 CST | sprint-24 · ADR-038 实现轮(证伪轮还原**之后**的终跑) | `50f17a7d` + 工作树 |
| PlayMode | `324 / 320 / 0 / 2 / 2` | 2026-11-03 11:45 CST | 同上(全量重编译轮) | 同上 |
| 编译警告 | **0** | 2026-11-03 11:45 CST | `touch` **1339** 个 `.cs` 后的全量重编译日志 | 同上 |

**等式(可核)**:`6724 − 6702 = 22 = 12(random_events_binder_test)+ 10(save_v4_time_params_test)`。
`6702` = 本批次**加这两个文件之前**的全量跑(同一工作树,`unity/Logs/editmode-v4.xml`)。
⚠️ **不声称**与批量 2 的 `6689` 之间的差额等式 —— 那一段还包含本轮对既有夹具的改动
(门数据侧 / drift 两个产物 / `save_v3` 与 seam 夹具的 v4 适配),我没有逐条重算,不编算式。

### 5.2 未做 / 盲区(逐项,不借绿)

| 项 | 状态 | 说明 |
|---|---|---|
| 52 数据集的**生产消费** | **零** | `EventRng.ComputeWindowStart` 仍无生产调用点;本轮的 52 数据集只服务构建期同值断言(ADR-038 §D7) |
| R2 整除门的**端到端**输入 | 不可达 | 见 §三 V-9 |
| AC-5-11 的**三条**参数(季 ×2 + 天气块)装载期 | **NOT-RUN** | 值未裁定、不进 schema ⇒ `ValidateSeason()` 只由夹具驱动(ADR-038 §D1 / R4) |
| AC-5-05 门的**构建期探针轮** | **NOT-RUN** | 与上一轮同:接线在 `AssemblyGates` 里且被 manifest 钉住,但未插无条件失败项做探针轮 ⇒ 不声称构建时生效。承 `impl-ac5-05-time-constant-gate-2026-11-03.md` §七 |
| 存档 v3 档的**回写** | 不做 | `Load` 只迁内存;盘上那份仍是 v3。下一次 Checkpoint 才会写成 v4 —— 与 ADR-010 §六 的存档时机一致 |
| `SaveService.ApplyMigrationsIfNeeded` 的旧注释 | **已过期** | 其 XML 注释仍写「当前 `CurrentSaveVersion = 2`」;属历史沿革注释,本轮**未改**(改动会牵动一段带 `TD-persist-10` 结案记录的长注释)。登记为文档债,不在实现轮顺手改 |

---

## 六、复算命令(自包含)

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
# ① 两个新夹具的条数
grep -c "public void test_" unity/Assets/Tests/EditMode/TimeWeather/random_events_binder_test.cs   # 期望 12
grep -c "public void test_" unity/Assets/Tests/EditMode/Boot/save_v4_time_params_test.cs           # 期望 10

# ② 52 的作者态必须是 $ref,不是数字
grep -n 'ticks_per_day' assets/data/52_random_events.json        # 期望含 "$ref": "5_time.ticks_per_day"

# ③ 52 产物里的解析值
python3 - <<'PY'
import struct
b=open('unity/Assets/DataCooked/52_random_events.cooked.bytes','rb').read()
pl=struct.unpack_from('<I',b,16)[0]
v=struct.unpack_from('<qqiii',b,20)
print("payloadLen",pl,"->",v)   # 期望 28 -> (36000, 6000, 6, 2, 6)
PY

# ④ 存档版本号与头部常量
grep -n "CurrentSaveVersion = " unity/Assets/Sim.Codec/SaveHeader.cs           # 期望 4
grep -n "FixedSizeV4 = \|HeaderSizeV4 = " unity/Assets/Sim.Codec/SaveHeaderCodec.cs  # 期望 60 / 92

# ⑤ 无变异残留
grep -rn "MUTATION M" unity/Assets --include=*.cs | wc -l                       # 期望 0
```
