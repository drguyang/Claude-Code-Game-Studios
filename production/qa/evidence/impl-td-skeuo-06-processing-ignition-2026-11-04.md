# TD-skeuo-04(④ 半边)+ TD-skeuo-06 结清 —— 设置壳入口 · 炮制点火

> **日期**: 2026-11-04 · **sprint-24** · commit 基线 `75a3a9c4` + 工作树
> **触发**: 用户指令「继续把 TD-skeuo-04 和 06 做掉」,并在两个分叉上裁定:
> ① 设置壳入口 = **暂停壳加第三行**;② 纸近景 = **拟增第 11 项 kind**(本轮未落,见 §4)。
> **判据面**: 每条给可跑命令与实跑输出。

---

## 1. TD-skeuo-04 · ④ 设置壳 —— 生产可达

**病因(债册原文复述,实测确认)**:`ModalId.SettingsShell` 的屏载体与目录登记齐
(`SkeuoRuntimeDriver.ScreenFactories` / `ModalScreenNames`),但全仓**零生产 `OpenModal` 调用点**
⇒ 玩家在构建里永远看不见。`settings-shell-42.md:54` 原写两个入口,而两者都被更晚的 Approved 件排除:

| 原入口 | 被谁排除 | 依据 |
|---|---|---|
| ① 主菜单「调治」项 | `main-menu-42.md:85-86`(Approved 2026-11-01) | 逐字「本屏**不提供设置 / 退出 / 制作名单**」;OQ-MM-5「P0 按单行起草」 |
| ② 暂停壳内项 | `pause-shell-42.md` OQ-PS-3(**用户裁定 2026-10-31**) | 逐字「**只放继续和存档位**」 |

⇒ 用户在二分中裁定**取 ②**(覆盖 OQ-PS-3 的「设置」一项),主菜单侧不动。

**落点**:

- `PauseScreen`:`_rows` 2 → **3**;行名 `pause-resume` / `pause-save-slots` / **`pause-settings`**;
  文案表 `{ "继续", "存档位", "调治" }`;`Activate(2)` → `_sink.OpenSettings()`。
- `IPauseCommandSink` + `DriverPauseCommandSink`:`OpenSettings()` → `OpenModal(ModalId.SettingsShell)`。
- 关屏去向 = `ADR-013 §十-F` 的**壳类压栈**:`SettingsShell` 在 `IsShellModal` 内 ⇒ 关屏回暂停壳。
- 规格:`pause-shell-42.md` §5(三行 + ASCII)· OQ-PS-3(覆盖登记)· `settings-shell-42.md` §3/§4
  (入口唯一 + 导航图)· `skeuomorphic-ui.md` `AC-42-F1` 加注。

**夹具**(`Tests/EditMode/Boot/modal_opener_test.cs`,+2):

| 判据 | 断的是什么 |
|---|---|
| `test_pauseSettingsRow_reachesSettingsShell_throughAssemblySink` | 整条入口链:第三行 → 装配 sink → `CurrentModal == SettingsShell` 且屏类型正确 |
| `test_pauseSettingsRow_isTheOnlyProductionEntry` | 第三行**存在**、元件名 = `pause-settings`、**有可见文字**且文案 = 「调治」 |

⚠️ 两处既有夹具随之订正(都是**真断言**,不是放宽):
`pause.RowCount == 2` → `3`(并写明裁定来源);`expected = { "继续", "存档位" }` → 加 `"调治"`
(后者正是「按 `RowCount` 迭代 + 期望表写死」的必然结果 —— 行数变了它就该红)。

---

## 2. TD-skeuo-06 · 炮制屏点火 —— 生产实现

**病因**:`IProcessingCommandSink` 生产实现数为**零** ⇒ `_sink?.Ignite(...)` / `_sink?.Close()`
两次调用都是 `?.` 静默跳过:按配方行**无变化、无报错、无日志、零 `Craft` 落流**。

**落点**:`Gameplay.Boot/DriverProcessingCommandSink.cs`(新)

- `Close()` → `SkeuoRuntimeDriver.CloseModal()`
- `Ignite(ordinal)` → 组 `CraftIgnitionRequest` 并调 `CraftIgnitionTransaction.Process`
  (18 的唯一点火事务入口;**本类零结算逻辑**)
- 接线:`BootRoot` 开炮制屏时 `SetTool(structureId)` + `screen.BindCommandSink(_processingSink)`;
  `ToolCell` 经 `World.Structures.TryGet` 解析(F-5.4 锚点;**不拿玩家格顶替**)
- 依赖面**全部走委派**(器具格 / 料位 / 单件重量 / 载重)⇒ 夹具可在不装载世界时驱动它

**配套改动**:

| 文件 | 改什么 | 为什么 |
|---|---|---|
| `ContainerSelectionStore.SnapshotSelection` | 一趟遍历产出**等长**的 `(ids, qualities)` | 分两次取会因「已消失实例」跳过而**错位**,品级错位只表现为 min 变了(静默) |
| `CraftCompletionSettler.UnitWeightOfOutputLine` | 公开「产出行 → 单件重量」 | 该映射的两张表**已在**本类;在点火侧另写 = 第二真源 |

**夹具 5 条**(`Tests/EditMode/Boot/processing_ignition_sink_test.cs`)—— 判据全部落在**流的副作用**:

| 判据 | 预期 |
|---|---|
| 准入 / 单炉 / 容量全过 | `Ignite == true` · **恰 1 条** `Craft` · id 由发号器铸造 |
| 器具格解析不出 | `false` · **零事件** |
| 未选料 | `false` · **零事件** |
| 两集合不等长 | `false` · **零事件** |
| `Close()` | `CurrentModal == None` |

⚠️ **不是「方法被调用了」的断言** —— 只断返回值会是自造断言(假绿形态 F1)。

---

## 3. 新发现:5 的 `EnvMod_raw` 与 F1 **不是同一个量纲**(新立 `TD-processing-09`)

`Sim/EnvMod.cs` 的中性值是**乘性** `Fix.One`(1.0),而 F1 的
`EnvMod_total = clamp(EnvMod_climate + EnvMod_clinic, …)` 是**相加的增量**(中性 **0**,可为负)。
把它接进点火链会让每炉平白 +1.0 修正 ⇒ `QtyMultiplier = clamp(1 + ΣM)` 约翻倍。

**本轮处置**:三分量一律传 `Fix.Zero`(加法中性),首次点火打一条显式告警;
**不接** `EnvMod.ComputeEnvModRaw`(接它是**错的**,不是「暂时不准」)。语义裁决归 5 / 用户。

复算(2026-11-04 实跑):

```bash
grep -rn "ComputeEnvModRaw" unity/Assets --include=*.cs | grep -v /Tests/
# 期望:恰 1 行(定义处)—— 零生产调用
grep -rn "new ClinicEnvironmentMemo" unity/Assets --include=*.cs | grep -v /Tests/
# 期望:零命中
```

---

## 4. TD-skeuo-04 的 ②(纸近景)—— **未做**,如实登记

用户裁定「做:增第 11 项」。**勘察后停手**,因为真实前置远大于债册那一行:

1. `InteractableKind` 增第 11 项 ⇒ `interaction_kinds.json` + `KindPriority` + `RoutedSystems`
   (**48 未登记**) ⇒ **`interaction_kinds.cooked.bytes` 必须重烘**
   (不重烘 = 枚举 11 / 表 10 ⇒ `KindRouteTable` 的双向闭合断言**在装载期抛**,全局红)。
2. 需**第五候选源** `PaperProps`(`CandidateSources` / `CandidateSetLoader`),今日四源无纸。
3. **世界摆点不存在**:`world_poi` / `world_buildslots` / `world_resources` / `world_containers`
   都没有纸道具,`48_tutorial_content.json` 的 `papers[]` **无 cell 字段**(且其 cooked 布局是
   「跨侧字段序契约」,加字段须同改 writer + decoder + 夹具)。
4. 48 的锚点引擎 `tutorial-onboarding/story-003` 仍 `Ready`。

⇒ 只做 ① 而不做 ②③ = 造一个**永不出现**的 kind(「引用却无登记」的**反向**形态)。
**宁可不做**,也不留一个看起来完成、实际不可达的枚举成员。

---

## 5. 回归(三元组)

**截至** `2026-11-04T02:44:51Z`(本地 10:44)· **批次** `sprint-24 TD-04/06 轮` · **commit** `75a3a9c4` + 工作树

| 套件 | 结果 | 对照 |
|---|---|---|
| EditMode | `6831 / 6794 通过 / 0 失败 / 37 跳过` | 上轮 `6824/6787/0/37` ⇒ **+7 = 点火 5 + 暂停 2**,零失败 |
| PlayMode | `331 / 327 通过 / 0 失败 / 2 不确定 / 2 跳过` | 上轮同值 ⇒ 零回归 |

债务变化:

- `TD-skeuo-06` **closed**(六十五订)。
- `TD-skeuo-04` 维持 `open`(④ 已结,⑤⑦ 未做)。
- 新立 `TD-processing-09`(`open` / P0 面 **Y**)。

---

## 6. 如实登记的残留(不借绿)

1. **炮制端到端无 PlayMode 判据** —— 「真开档 → 按 E → 点配方 → 流里多一条 `Craft`」仍只有
   EditMode 的接口级夹具;`BootRoot` 整段与其它 `OnTickEdge` 同状态。
2. **环境 / 设备三分量恒 0**(`TD-processing-09`)。
3. **料位不按配方过滤** —— 与准入门口径一致(都只看品级 min),归 20 的规则六轮。
4. **设置壳的走查未执行** —— 可达性已解除,`AC-42-F1` ④ 仍需手柄走查。
5. **纸近景 / 教学屏仍不可达**(§4)。
