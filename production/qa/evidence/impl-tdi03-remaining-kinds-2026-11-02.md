# 实现原件 —— `TD-interaction-03` 收口:剩下三种(`Switch` / `Utensil` / `ClinicPanel`)

> **对象**:`docs/tech-debt-register.md` 的 `TD-interaction-03` 残余三种
> **批次**:`S23-18`(sprint-23 追加;用户指令「修 TD-interaction-03 剩下三种」)
> **截至时刻**:`2026-11-02 12:06 CST`(三元组见 §四)· **commit**:本批未提交工作树(基于 `6b089f6b`)
> **结论**:三种 kind **全部获得数据行 + 第四源分类 + 路由分派**;`ClinicPanel` / `Switch`
> 的消费面为**生产实装**,`Utensil` 的消费面**设计侧被 42 阻塞**(`O-18-R6` / `AC-18-10b`),
> 如实留为残余 —— `TD-interaction-03` 维持 **`partial`**,残余从「三种全无」收窄为「一种消费面待 42」。

---

## 一、勘察(先于改动)

| # | 事实 | 复算 |
|---|---|---|
| ① | 三种都没有数据行:`build_modules.json` 无对应 `module_type`;`world_buildslots.json` 无槽位 | `Counter(m['module_type'])`(改前无 Utensil/ClinicPanel/Switch) |
| ② | `BakedInitialSource` 的三个切片是**硬编码空数组** | `grep -n "Array.Empty<UtensilDto>" …`(改前 1 命中) |
| ③ | `ClinicPanel` 的屏幕**已存在**(42) | `Assets/Gameplay.UI/Skeuomorphic/Screens/ClinicPanelScreen.cs` + `ModalId.ClinicPanel = 6` |
| ④ | `Utensil` 的消费面**设计侧被阻塞**:18 的炮制面板归 42 屏清单认领,尚未认领 | `design/gdd/processing.md:664` / `:732`(`AC-18-10b` **EXTERNAL · BLOCKED-BY-42**)· `O-18-R6` |
| ⑤ | `CraftIgnitionTransaction.Process` 零生产调用点 | `grep -rn "CraftIgnitionTransaction\." unity/Assets \| grep -v /Tests/ \| grep -v Sim/Processing` ⇒ 零命中 |

⇒ 三种的**选择面**都可以做;消费面只有两种有生产落点。**不发明第三种消费面**。

---

## 二、改了什么

| # | 文件 | 变更 |
|---|---|---|
| 1 | `Sim/World/BuildSlotCatalog.cs` | `SlotType.Utensil = 9` / `ClinicPanel = 10` / `Switch = 11`(闭集 8 → 11) |
| 2 | `assets/data/build_modules.json` | 模块 12 / 13 / 14(`furn_type = null` ⇒ 不参与 24 的房间析出) |
| 3 | `Editor.Tools.Level/P0ClinicLevel.cs` | 三个开档槽位:器具 `(10,0,5)` · 医馆面板 `(5,0,9)` · 开关 `(2,0,4)` |
| 4 | `Gameplay.Presentation/Interaction/BakedInitialSource.cs` | 四切片收成一个 `CollectSlots(SlotType)`(分类依据 = 模块目录的 `SlotType`) |
| 5 | `Gameplay.Boot/DoorInteractionDriver.cs` → **`StructureToggleDriver.cs`** | 门专用泛化为「门 / 开关共用」:构造参 `expectedType` 钉住可切换的模块类型 |
| 6 | `Gameplay.Boot/BootRoot.cs` | 建两个切换器(`Door` / `Switch`);接 `ModalId.ClinicPanel`;**`enterProcessing: null`** |
| 7 | `Gameplay.Boot/InteractionPressRouter.cs` | 新增 `case Switch` / `case ClinicPanel` / `case Utensil` 与三个可空消费面 |
| 8 | `assets/data/world_buildslots.json` + 两份 cooked | 关卡工具重导 + 重烘(18 槽) |
| 9 | 测试 | `baked_initial_source_test` +4(三种切片 + 互斥)· `interaction_press_door_container_test` +4 · `door_interaction_test` +1(类型守卫) |
| 10 | 既有 pin | 闭集 8 → 11 · 掩码上界 511 → 4095 · 模块行数 11 → 14 · 槽位 15 → 18 · 金标准 SHA 1426 → 1697 B |

### 语义裁定(本轮就地,未经 UX 复审)

- **开关 = 两态结构**,与门同一写路径(`Variant` 0/1 + `StructureModified`),**但不参与可通行性**。
  P0 **无下游读者**(无电路 / 灯具系统)⇒ 本类今日「可被选出并路由,状态进流,无人消费」。
  **不假装有读者**:没有为它造第二套判据。
- **器具**的消费面**不伪造**:`enterProcessing` 在生产装配里恒 `null`,
  选中 `Utensil` 返回 false(按键落空)。18 的面板一旦被 42 认领,接线点 = `BootRoot` 的
  `enterProcessing:` 实参(一处)。
- **医馆面板**开 `ModalId.ClinicPanel` —— 该屏与模态闭集早已存在(42 的 Amendment B)。

---

## 三、证伪轮(6 条变异 · 改→跑→还原同一命令内 · SHA-256 逐字节还原 6/6)

| 变异 | 打什么 | 实测红 | 形态 |
|---|---|---|---|
| **N1** | 分类不按 `SlotType`(任意模块都进切片) | 6 红(三个新切片 + 门两条 + 互斥) | **超集**(四条切片共用同一分类点) |
| **N2** | `Utensils` 取成 `ClinicPanel` 切片 | 1 红:`test_utensils_baselineUtensil_isIncluded` | **exact** |
| **N3** | `case ClinicPanel` 不打开面板 | 1 红:`test_clinicPanelOnCell_routesClinicPanel_withStructureId` | **exact** |
| **N4** | `case Switch` 不分派 | 1 红:`test_switchOnCell_routesSwitch_withStructureId` | **exact** |
| **N5** | `case Utensil` 无消费面也报「已消费」 | 2 红(诚实降级 + 有消费面时的路由) | **超集** |
| **N6** | 切换器不校验 `expectedType` | 2 红(开关切门 + 门驱动器切非门) | **超集**(同一守卫两处断言) |

**结果:6/6 转红,6/6 SHA-256 逐字节还原**(`RESTORE-OK` 全文见运行输出)。
⚠️ **N5 的第二条红是 `ArgumentOutOfRangeException`**(消费面未被调用 ⇒ 列表为空 ⇒ 取 `[0]`)——
它仍是**判据红**(断言失败的前置),不是「变异写坏」。

---

## 四、回归(三元组 + 等式)

| 套 | 读数 | 文件 |
|---|---|---|
| **EditMode 全量** | **`6590 / 6553 / 0 / 0 / 37`** @ `2026-11-02 12:03:44 CST` | `unity/Logs/tdi03b-em-full.xml` |
| **PlayMode 全量** | **`297 / 293 / 0 / 2 / 2`** @ `2026-11-02 12:04:51 CST` | `unity/Logs/tdi03b-pm-full.xml` |

**等式(EditMode)**:`6590 − 6581 = 9`,逐类从 XML 实数为证(不靠算术推断):

```bash
python3 - <<'PY'
import xml.etree.ElementTree as ET
r=ET.parse('unity/Logs/tdi03b-em-full.xml').getroot()
for c in ('baked_initial_source_test','interaction_press_door_container_test','door_interaction_test'):
    print(c, sum(1 for tc in r.iter('test-case') if c in tc.get('fullname','')))
PY
# 8 / 9 / 6 —— 前次(AC-4-22 轮)为 4 / 5 / 5 ⇒ +4 / +4 / +1 = 9
```

**PlayMode `+0`** —— 本轮无 PlayMode 夹具(三处新增全在 EditMode)。

**基线来源**:`6581/6544/0/0/37` 与 `297/293/0/2/2` = 上一批 `S23-17`(`AC-4-22` 轮,
`unity/Logs/ac422-pm-final.xml` 实测同三元组 @ `2026-11-02 03:46:47Z`)。

### ⚠️ 两条**如实登记**的偏离(不借绿)

1. **`PM exit=2` 不是回归信号** —— 本仓 PlayMode 跑**历来** exit 2(四次历史运行
   `bd9moyv7s` / `bcw9tiotv` / `bmmeopsc0` / 本轮 均 exit 2,其中 `bcw9tiotv` 就是上一批
   `S23-17` 的 `297/293/0/2/2`)。判据 = **XML 的 `failed` 计数**,不是进程退出码。
   ⚠️ 它在 `bx35oltmi`(failed=2)上同样是 2 ⇒ **退出码对失败零判别力**,不能当判据用。
2. **全量 EditMode 三元组取自**「订正前树」—— 跑完后我订正了 `door_interaction_test.cs:24`
   (类 `<summary>` 仍写改名前的 `DoorInteractionDriver`)。该改动**纯注释、零判据**,
   但文件字节确实变了 ⇒ 另跑一次过滤回归作为订正后回证(读数见下表)。

| 订正后回证 | 读数 | 文件 |
|---|---|---|
| `DaYiJingCheng.Tests.ModularBuilding` | **`242 / 242 / 0 / 0 / 0`** @ `2026-11-02 12:06:09 CST` | `unity/Logs/tdi03b-mb-postedit.xml` |

> 该过滤跑在**订正后树**上执行(编译 + 判据双回证),与订正前的同过滤跑
> (`242/242`)读数相同 ⇒ 注释订正确实**零判据影响**。
> ⚠️ 但它**不能**替代全量 EditMode —— 全量三元组仍取自订正前树,这一点保留在案。

---

## 五、`TD-interaction-03` 的真实状态(不借绿)

- **候选源**:十种 kind **全部**有生产源(Drop / ForageSpot / Patient / PoiCell / Container /
  BuildSlot / Utensil / ClinicPanel / Door / Switch)。
- **消费面**:九种有生产落点(`Utensil` 除外)。`Utensil` 的缺**不在 4 侧** ——
  4 已按 `StableId` 路由,缺的是 18 的屏,而它的前置是 **42 屏清单认领**(`O-18-R6`)。
- ⇒ 债**不关闭**:`partial`,残余收窄为「`Utensil` 消费面待 42」。

---

## 六、证明不了的事(禁借绿)

1. **开关「状态进流但无人读」没有下游断言** —— 本轮的断言止于「路由 + 恰一条
   `StructureModified`」;「将来的电路系统能读到它」不可证。
2. **`Utensil` 的选中在实机上不可达** —— 数据与路由都在,但消费面 null ⇒ 玩家按 E
   不会发生任何事(这是**如实降级**,不是缺陷;但也不能宣称「器具可交互」)。
3. **实机走查未跑**(与上一轮同):证据是 EditMode 的真装配,不是玩家按 E 的端到端。
4. **`ClinicPanel` 打开后 24 的数据面**未在本轮验证(屏能开 ≠ 面板读数正确)。

---

## 七、同批的**非判据**改动(如实单列)

| # | 内容 | 为什么不算判据 |
|---|---|---|
| 1 | `Tests/EditMode/ModularBuilding/door_interaction_test.cs:24` 类 `<summary>` 由 `DoorInteractionDriver` 订正为 `StructureToggleDriver`(并注改名出处) | **纯 XML 文档注释**,不参与任何断言;在全量 EditMode 之后才改 ⇒ 三元组取自订正前树,已由 §四 的过滤回证补偿 |
| 2 | `Gameplay.Boot/StructureToggleDriver.cs.meta` 由 Unity 资产刷新生成 | 引擎派生,非手写 |
