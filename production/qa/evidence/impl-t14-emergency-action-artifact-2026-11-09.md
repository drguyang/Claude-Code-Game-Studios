# T14 急救动作表外部化 —— 实施与证据(2026-11-09)

> **范围**:把 GDD `:843` 指定、却**从未存在**的产物 `assets/data/emergency_action.json` 建起来。
> **用户指令**:「走甲」。
> **上游**:T13(`d84ba5a3` 动作表按 `ActionId` 取行 —— 缺行 fail-loud)· ADR-014(两阶段烘焙)。

---

## 1. 病灶:GDD 写了,产物不存在

`design/gdd/emergency-procedures.md:843` 的 10-DC 数据契约:

| 产物 | 路径 | 主键 | 列 |
|---|---|---|---|
| 动作表 | `assets/data/emergency_action.json` | `action_id : int` | `polarity` · `base_potency` · `mag_threshold` · `min_edges` · `min_hold_ticks` · `half_life_ticks` · `duration_ticks` |

实测(**修前**):

```bash
ls assets/data | grep -i emerg              # 零命中
ls unity/Assets/DataCooked | grep -i emerg  # 零命中
ls unity/Assets/Editor.Tools.Bake | grep -i emergency   # 零命中
```

⇒ 没有源文件、没有 binder、没有 cooked、没有 codec。动作 0 的九个字段一直住在代码里
(T13 把它搬进 `EmergencyActionTable.ProductionDefault`,**仍是代码常量**)。
`action_tables_bake_test` 只测 **schema 校验器**(DC-1…DC-6),不碰任何烘焙管线 ——
「有烘焙测试」是个假印象,与 T13 那个「判据测默认表、生产用副本」同族。

---

## 2. 改动清单

| # | 文件 | 层 | 内容 |
|---|---|---|---|
| 1 | `assets/data/emergency_action.json` | **新** | 作者态源;今日 1 行(动作 0) |
| 2 | `Editor.Tools.Bake/EmergencyActionBinder.cs` | **新** | 白名单 / 类型 / 值域;复用 `EmergencyActionSchema` 的 DC 校验器 |
| 3 | `Editor.Tools.Bake/EmergencyActionBaker.cs` | **新** | 仓根装载 + `ConfigVersion`(内容哈希) |
| 4 | `Editor.Tools.Bake/EmergencyActionCookedWriter.cs` | **新** | 载荷 = `row_count:i32` + 每行 60 B |
| 5 | `Gameplay.Presentation/EmergencyActionCookedCodec.cs` | **新** | 解码 + E-13 硬失败;`EmergencyActionDataSet` |
| 6 | `DataCooked/emergency_action.cooked.bytes` | **产物** | **84 B** = 20 头 + 4 + 60;`ConfigVersion = 0x058A6C2D` |
| 7 | `Editor.Tools.Bake/DataBakeMenu.cs` | 菜单 | 「烘焙 emergency_action」 |
| 8 | `Gameplay.Presentation/AddressablesDataProvider.cs` | 装载 | 地址常量 + `EnsureEmergencyActions` + `Preload` |
| 9 | `Gameplay.Presentation/DataCorePreloader.cs` | 地址表 | 22 → **23** 条(注释同步) |
| 10 | `Gameplay.Presentation/DataCookedDisk.cs` | 兜底 | `LoadEmergencyActions()`(同一份字节 + 同一个 codec) |
| 11 | `Gameplay.Boot/CompositionRoot.cs` | 装配 | 动作表改由产物构造(provider 优先,磁盘兜底) |
| 12 | `AddressableAssetsData/AssetGroups/data-core.asset` | 组条目 | +1(44 条) |
| 13 | `Tests/EditMode/DataPipeline/cooked_drift_all_artifacts_test.cs` | 漂移门 | 登记 + 哨兵 43 → **44** |
| 14 | 5 个测试的 `MultiTableProvider` | 夹具 | 补 `EmergencyActionDataSet` 分支 |
| 15 | `Tests/EditMode/EmergencyProcedures/emergency_action_artifact_test.cs` | **新** | **11 条**判据 |

**数值零改动**:动作 0 的九个字段与 T13 逐字段相同;产物只是它们的新家。

---

## 3. 三个设计判断

### 3.1 `result_mul` 进数据,但构建期钉死裁定值

GDD `:1049` 的 remarks 明写「`result_mul` 是 per-action 数据表字段,**不得**改从代码直读常量」;
而 AC-10-16 把三档 `{65536, 32768, 16384}` 定成**绝对裁定值**,`OQ-10-1` 明写「改回 0 须重开该 OQ」。
⇒ 两者都要:**数据承载** + **构建期逐档比对** `EmergencyActionSchema.GetResultMulTiers()`。漂移即拒收。

### 3.2 `ProductionDefault` 留下来了,但只剩一个用途

它现在**不参与生产装配**(`CompositionRoot` 用产物),只服务「无 provider 的上下文」(部分 EditMode 夹具)。
它不是第二真源 —— `test_productionDefault_equalsArtifactRow` **逐字段**把它钉在产物上,分叉即红。

### 3.3 空 `rows` 构建期拒收

空表 = 「一个动作都没有」⇒ 生产第一笔急救就抛。不让它以「还没配」的形态存在于产物里。

---

## 4. 判据(11 条)

| # | 判据 | 判别力 |
|---|---|---|
| 1 | 源 → 产物 → 行,行数 / id 一致 | 写读字段序不一致 ⇒ 红 |
| 2 | **产物行 == `ProductionDefault` 逐字段**(含 `ResultMul`) | 两份副本分叉 ⇒ 红 |
| 3 | `result_mul` == AC-10-16 三档(绝对值 16384 / 32768 / 65536) | Missed 归零 ⇒ 红 |
| 4 | `action_id` 不在闭集 ⇒ 拒收(DC-4) | — |
| 5 | `result_mul` 被改成 `"0"` ⇒ 拒收,报文含 `AC-10-16` **与** `OQ-10-1` | 只说「值不对」会让人去查格式 |
| 6 | Fix 字段写成 JSON 数字 ⇒ 拒收 | 数字经 `double` 中转绕开 `FixParse` |
| 7 | 空 `rows` ⇒ 拒收 | — |
| 8 | 重复 `action_id` ⇒ 拒收 | 迭代序决定用哪行 = 不可重放 |
| 9 | 载荷长度被改 ⇒ 抛 E-13 | 返回半截表 ⇒ 红 |
| 10 | 地址常量 == 产物文件名(provider / 菜单两处) | 装载面找另一份 ⇒ 红 |
| 11 | 磁盘兜底建得出表,动作 0 的 `base_potency` = 65536 | 兜底路径静默返空 ⇒ 红 |

---

## 5. 回归

**三元组**:`2026-11-09T22:51:20Z`(EditMode 结束)/ `22:52:42Z`(PlayMode 结束) · **全量** · `d84ba5a3` + 工作树。

| 套件 | 本次 | 对照基线(T13 后) | 差 |
|---|---|---|---|
| EditMode(全量) | **`7338 / 7301 / 0 / 0 / 37`** | `7326 / 7289 / 0 / 0 / 37` @ `2026-11-09T22:18:23Z` · T13 批 · `d84ba5a3` | **+12 / +12** |
| PlayMode(全量) | **`373 / 368 / 0 / 2 / 3`** | `373 / 368 / 0 / 2 / 3` @ `2026-11-09T22:19:30Z` · T13 批 · `d84ba5a3` | **零变化** |

**等式核对**:EditMode 增量 **12** = 新夹具 `[Test]` **11** 条 + `cooked_drift_all_artifacts_test` 的
**按产物参数化**用例 `drift[emergency_action.cooked.bytes]` **1** 条。
(逐名对照:`git diff` 前后两份 XML 的 `fullname` 集合,新增 12、消失 0。)

### 5.1 首轮 28 红 = 两条真实缺口(都不是「门太严」)

**① 5 个测试的 `MultiTableProvider` 不认识新表**(28 红里的 27 条)

    报错:`MultiTableProvider 无 EmergencyActionDataSet —— 本测不该问到它(真问 = 接线错)`

那些夹具把「装配会问哪些表」写成了**枚举式断言** —— 装配多问一张表就红。这是**设计意图**
(它逼着「新增装载 = 同批补夹具」),不是误报。已给 5 处补分支(与 53 三表同款处置)。

**② catalog 陈旧(43 vs 44)**

    `data-core 组声明了 44 条,构建出的 catalog 只有 43 条 —— 缺:emergency_action.cooked.bytes`

跑「构建 Addressables 内容(BuildPlayerContent)」后 catalog = 44 条,与盘上 44 份产物一致。
⚠️ **该产物落 `unity/Library/`(gitignored,机器本地)** ⇒ **新检出后必须重跑该菜单**,
否则 `Preload()` 查不到 address(E-13 启动期硬失败)。这是本项目的既有约定,不是本件引入的。

**③ 一处实现错(我自己写的)**:`EmergencyActionCookedCodec` 的载荷长度校验写成
`rowCount × BytesPerRow`,漏了 `row_count` 自己的 4 B ⇒ 首跑 4 条红。已改为 `4 + rowCount × BytesPerRow`。

---

## 6. 未做(不得借绿)

| 项 | 状态 | 理由 |
|---|---|---|
| **动作 1/2/3 的行** | **未做** | 需要**数值裁定**。现在补它们**是改 JSON**,不是改代码 —— 这正是本件的目的 |
| 玩家**选择动作**的交互 | **未做** | `Arm(actionOrdinal: 0)` 仍硬编码(归 3 / 4 的输入轮) |
| 动作表与**技法解锁**的联动 | **未做** | 技法已解但无行 ⇒ 现在响亮拒绝(T13),不是静默错判 |
| `result_mul` 是否该迁出代码常量 | **未裁** | 今日是「数据承载 + 构建期钉死」双保险;若哪天认为裁定值只该有一处,再裁 |
