# 实现原件 —— 第二源补齐(POI / 建造槽位接线) · `TD-interaction-03` → partial

> **对象**:`docs/tech-debt-register.md` 的 `TD-interaction-03`
> **批次**:`S23-16`(sprint-23 追加;用户指令「修 TD-interaction-03」,并选定范围 **C**)
> **截至时刻**:`2026-11-02 09:42 CST`(三元组见 §五)· **commit**:`36cd1c8a` + 本批未提交工作树
> **结论**:POI 与建造槽位两条**接通到真实消费面**;另五种(`Door` / `Switch` / `ClinicPanel` /
> `Utensil` / `Container`)**没有任何数据行**,债务转 **`partial`**,不宣称关闭。

---

## 一、勘察结论(先于改动)

债册原写「六种候选源未装配」。实测阻塞**比那更靠上游**,三条都是可复算的事实:

| # | 事实 | 复算 |
|---|---|---|
| ① | 生产选择器收的是 **`UnwiredDiscoveryReporter.Instance`**(空实现),且 6 的 `PoiStateMachine` **在组合根里从未被构造** | `grep -rn "new PoiStateMachine(" unity/Assets --include=*.cs \| grep -v Tests` 修改前 ⇒ 零命中 |
| ② | `BuildSlot` 的既有入口是 23 自己的 `BuildInputDriver`(独立键 / 建造模式),不是 E | `grep -n "BuildInputDriver" unity/Assets/Gameplay.Boot/BootRoot.cs` |
| ③ | `Door` / `Switch` / `ClinicPanel` / `Utensil` / `Container` **无数据行**:`build_modules.json` 只有 Bed/Table/Cabinet/Decor/Shell/Tent;`world_buildslots.json` 只有 Decor/Shell/Tent | `python3 -c "import json,collections;d=json.load(open('assets/data/build_modules.json'));print(collections.Counter(m['module_type'] for m in d['modules']))"` |

⇒ 「只补源」会让 POI 选中后什么也不发生(空 reporter),而建造槽位会抢按键却无人做事。
故本轮按用户裁定的 **C**:连消费面一起接,并把没有数据的五种如实留在债里。

---

## 二、改了什么(生产 7 件 + 夹具 4 件 = 3 新 + 1 改)

| # | 文件 | 变更 |
|---|---|---|
| 1 | **新** `Gameplay.Presentation/Interaction/WorldBakedLogicalSource.cs` | 第二源三切片(POI / 采集点 / 建造槽位);槽位线性键 `x + W·(y + H·z)`(long 运算,W/H 取产物头声明范围) |
| 2 | **新** `Gameplay.Boot/PoiDiscoveryReporter.cs` | `IDiscoveryReporter` 的生产实装 → `PoiStateMachine.TryDiscover(poiId, tick)`;**未绑定 ⇒ 抛**(不再静默丢) |
| 3 | `Gameplay.Boot/CompositionRoot.cs` | 构造 6 的 `PoiStateMachine`(`LoadPoiIds` 定义侧 id 集)并挂服务包;选择器改收生产 reporter;`pressCandidates` 的第二源换成三切片全量(采集自用的那份**仍是资源点单切片**) |
| 4 | `Gameplay.Boot/CompositionRootServices.cs` | 新增 `PoiStateMachine`(可空;`null` 与 `PressCandidates == null` 同源) |
| 5 | `Gameplay.Boot/BuildInputDriver.cs` | 新增 `AimAt(WorldPos)` + `_aimCell`;`DoPlace` 锚点 = 瞄准格 ?? 玩家格;退出建造模式清瞄准 |
| 6 | `Gameplay.Boot/InteractionPressRouter.cs` | `case BuildSlot` → `_aimBuild(cell)`;`case PoiCell` → 返回 true(出境已在 `Select` 内发生);`aimBuild` 为可空构造参 |
| 7 | `Gameplay.Boot/BootRoot.cs` | 路由的 `aimBuild` 接 `BuildInputDriver.AimAt` |
| 8 | **新** `Tests/EditMode/Interaction/interaction_press_slot_poi_test.cs` | 4 条(POI 出境 / 槽位瞄准 / 未接消费面 / 同格优先级) |
| 9 | **新** `Tests/EditMode/Boot/poi_discovery_reporter_test.cs` | 4 条(推进 + 恰一条事件 / 幂等 / 未登记 id / 未绑定抛) |
| 10 | **新** `Tests/EditMode/Interaction/world_baked_source_test.cs` | 4 条(三切片字段 / 线性键 / 轴次序覆盖 / W 非正抛) |
| 11 | `Tests/EditMode/ModularBuilding/build_controller_test.cs` | +2(`AimAt` 落点 / 退出清瞄准) |

---

## 三、语义裁定(本轮就地定,未经 UX / 数值轮复审)

**E 打到建造槽位 = 进入建造模式,并把幽灵锚到该格;不放置。**

- 依据:4 的全序选中 `BuildSlot` ⇒ 玩家意图是「我要在这一格建」,不是「把建造模式开一下」。
  放置仍归 23 的放置键(误触即落一块家具的代价太大)。
- 后果:建造模式**有两个入口**(23 的 toggle 键 + E 打到槽位)。已知重叠,登记在 §七.2。
- 退出建造模式 ⇒ 瞄准格失效(下次进入回到玩家格),由 `test_aimAt_toggleOff_clearsAim` 钉住。

---

## 四、证伪轮(5 条 · 改→跑→还原同一命令内 · SHA-256 逐字节还原 5/5)

| 变异 | 打什么 | 实测红 | 形态 |
|---|---|---|---|
| **M1** | 槽位线性键轴次序错(`x + H·y + W·z`) | 1 红:`test_slotSlice_linearKeyMatchesLiteralFormula` | **exact**(修夹具后,见下) |
| **M2** | 发现上报永不发 | 2 红:含 `test_request_discoverState_andWritesExactlyOneEvent` | **超集**(两条都断言「发了」,幂等测试的前置也一起红) |
| **M3** | 槽位分派丢格(`aim(default)`) | 1 红:`test_buildSlotOnCell_routesBuildSlot_andAimsThatCell` | **exact** |
| **M4** | 放置忽略瞄准格 | 1 红:`test_aimAt_placeUsesAimedCell_notPlayerCell` | **exact** |
| **M5** | POI 不记消费(返回 false) | 1 红:`test_poiOnCell_routesPoiCell_andEmitsOneDiscoveryRequest` | **exact** |

### ⚠️ M1 首轮是**假存活**(如实登记)

首版夹具的两个槽位都在 **`y = 0`** ⇒ `x + W·y + W·H·z` 与 `x + H·y + W·z` **同值**,
变异不可观测(4/4 全绿)。修夹具:第二格改 `y = 2`,并留注说明原因。改后同一变异 **1 红**。
⇒ 与本仓 F4(空洞正控)同族:**不是断言写假,是输入构造让变异前后同结果。**

---

## 五、回归(三元组 + 等式)

| 套件 | 本批 | 基线(`S23-15`) | 等式 |
|---|---|---|---|
| EditMode | `6554 / 6517 / 0 / 0 / 37` | `6540 / 6503 / 0 / 0 / 37` | `+14 = 4(POI/槽位路由)+ 4(发现上报)+ 4(第二源)+ 2(建造瞄准)` ✅ |
| PlayMode | `297 / 293 / 0 / 2 / 2` | `297 / 293 / 0 / 2 / 2` | `+0`(本批无 PlayMode 改动) ✅ |

`@ 2026-11-02 09:42 CST` · 批次 `S23-16` · commit `36cd1c8a` + 工作树。
PlayMode `inconclusive=2` 与基线同形。

**复算**:

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
python3 -c "
import xml.etree.ElementTree as ET
for f in ['unity/Logs/td-i03-em-full2.xml','unity/Logs/td-i03-pm-full.xml']:
    r=ET.parse(f).getroot()
    print(f, {k:r.get(k) for k in ('total','passed','failed','inconclusive','skipped')})
"
# 期望: EditMode 6554/6517/0/0/37 · PlayMode 297/293/0/2/2
```

---

## 六、`AC-4-22` 的真实状态(不借绿)

`AC-4-22` 逐字:`GIVEN` 诊所**开档自带**的门与柜(23 的 `BakedInitial`),`WHEN` 玩家站到其格旁
按交互,`THEN` 可被选出并路由。

- `interaction-system/story-003` 已按**读方形状**(注入替身)签 ✅,并自注「不得借绿:
  对侧真身联调归各自 Epic」。
- **本轮之后仍未满足**:门与柜对应的 `Door` / `Container`(以及 `Switch` / `Utensil` /
  `ClinicPanel`)**没有数据行**,`CandidateSources.BakedInitial` 在生产装配里仍是 `null`。
- ⇒ 该 AC 在**实机**语义下仍不可通过;本轮**不**以它作为关闭依据。

---

## 七、证明不了的事(禁借绿)

1. **五种 kind 仍不可交互** —— `Door` / `Switch` / `ClinicPanel` / `Utensil` / `Container`
   无数据、无消费面。`TD-interaction-03` 维持 **`partial`**。
2. **建造模式有两个入口**(23 的 toggle 键 + E 打到槽位)。二者是否应合一(例如 E 打在槽位上
   等同于 toggle 的「选中该格」语义)、以及幽灵在退出后是否该保留瞄准格,**未经 UX 轮复审**。
3. **POI 状态机的**其余**消费面未接** —— `TryAdvance`(主机推进到已解决 / 战斗后清算)
   仍无生产调用点;本轮只接了 `TryDiscover`。
4. **PlayMode 未覆盖**「真实启动序里按 E 打到 POI ⇒ 世界流出现 `PoiStateChanged`」——
   本轮证据是 EditMode 的组合根 + 真 `PoiStateMachine`;真启动序的对侧(`BootRoot` 的 Addressables
   装载路径)未单独夹。
5. **槽位线性键的 W/H 取产物头声明范围** —— 若关卡工具改了 `world_size` 而烘焙头未同步,
   键会错但不崩(只影响同距平局的次序);归 ADR-022 的 C5/C6 一致性检查。
