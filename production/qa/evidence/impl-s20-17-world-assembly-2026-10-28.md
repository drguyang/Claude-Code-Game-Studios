# S20-17 · World 场景装配轮 —— 实施与验证原件

**日期**:2026-10-28
**卡面**:`production/sprints/sprint-20.md` §十 · S20-17
**判定**:AC-1…AC-5 全部**已落 + 已证**;证伪轮 **5/5 恰如预期**(第三轮)。
**挂标不记绿**:5 条(见 §五),逐条照抄卡面,一条不勾。

---

## 一、为什么做这一轮

`unity/Assets/Gameplay.Boot/CompositionRoot.cs` 的注释(卡面引作 `:787-796`,现随插入上移)逐字记着:

> `structures` 从哪来:23 的 `StructureInstanceRegistry` 今日在生产树**零构造**
> (`new World(` 同样零构造,见 `TD-building-01` 实测 ①)。……注释指名「**`BootRoot` 的 World 场景装配轮**」。

`impl-s20-2a-building01-2026-10-25.md:176` 的 **N5** 条把这件记为「本件未做」,处置写「后续装配轮」;
而 `TD-building-01` 已于 2026-10-25 `closed` 且 **N5 未转成新债** ⇒ 该缺口**零排期载体**。
本卡即补该载体。

**边界(用户 2026-10-28 裁定)**:最小**合成**世界,不装载烘焙数据 —— 因为
`assets/data/` 零份 `world_*` JSON、`unity/Assets/DataCooked/` 的 25 份 cooked 产物里零份世界数据,
而世界数据的唯一作者(ADR-022 关卡工具)已被裁「不立 epic、不排期」(`p0-remaining-roadmap-2026-10-20.md:1088`)。
**装配不依赖这些数据** —— `World` 的两个实参都能合成。

---

## 二、AC 结账表(Landed / Proven 分列)

| # | 判据 | Landed(已落) | Proven(已证) | Proven-by(可复算) |
|---|---|---|---|---|
| **AC-1** | 生产树 `new World(` 0 → 1 | ✅ `Gameplay.Boot/MinimalWorldFactory.cs:184` | ✅ | `test_ac201_worldConstructedExactlyOnceInProductionTree`;反证 M1 |
| **AC-1b** | 该构造点**真被装配根调用**(不是死工厂) | ✅ `Gameplay.Boot/BootRoot.cs:219` | ✅ | `test_ac201b_bootRootActuallyCallsTheWorldFactory` |
| **AC-2** | 生产树 `new StructureWriter(` 0 → 1 | ✅ `Gameplay.Boot/CompositionRoot.cs:941` | ✅ | `test_ac202_structureWriterConstructedExactlyOnceInProductionTree`;反证 M2 |
| **AC-3** | 经生产装配路径后 `ClinicRoomMatching != null` | ✅ 同上(注册表取自 `world.Structures`,**`:882`** / 服务构造 **`:920`**) | ✅ | `test_ac203_assembleWithWorld_yieldsClinicRoomMatching`;反证 M3 |
| **AC-4** | `World` 端到端经**装配根产出的服务**:`Place → IsEffectivelyWalkable == false` / `Remove → true` | ✅ 写者 `occupancy` = 同一 `World` 实例 | ✅ | `test_ac204_placeThenRemove_throughAssembledServices`;反证 M4 |
| **AC-5** | `StructureWriter` 构造 fail-closed:`DropEventWriter` 未注入 ⇒ 构造期抛 | ✅ 既有 `Sim/World/StructureKinds.cs:281`(ctor 起于 `:270`) | ✅ | `test_ac205_structureWriterWithoutDropWriter_throwsBeforeAnyDownstreamCall`;反证 M5 |
| 附加 | 同给 `structures` 与 `world` 且不同实例 ⇒ 抛(防两份真源) | ✅ `CompositionRoot.cs` 的 `ReferenceEquals` 守卫 | ✅ | `test_ac203c_worldAndForeignRegistry_throws`;反证 M1 |
| 附加 | 不传世界 ⇒ 两者仍 null(既有 100+ 调用方零行为变化) | ✅ | ✅ | `test_ac203b_assembleWithoutWorld_yieldsNulls` |

> ⚠️ **判据命令的可复算性曾被自己的注释破坏,已修**(2026-10-28 收口前实测):
> 首版在四处**注释**里写了 `new World(` / `new StructureWriter(` 字样,导致卡面那条
> `grep -rn "new World(" … | grep -v "/Tests/"` 的命中数变成 **2 而不是 1** ——
> 即**判据命令本身**被新写入的注释弄假。处置:把注释里的字面量去掉左括号(改写成
> 「`new World` 的构造」),使卡面命令**原样成立、无需剥注释**。
> 复算(期望各恰 1 行,且落点分别为 `MinimalWorldFactory.cs:184` / `CompositionRoot.cs:941`):
> ```bash
> grep -rn "new World("          unity/Assets --include=*.cs | grep -v "/Tests/"
> grep -rn "new StructureWriter(" unity/Assets --include=*.cs | grep -v "/Tests/"
> ```
> 夹具侧仍**剥注释**再计数(双保险);注释清理后两侧口径一致。

> **AC-1 的落点与卡面原文有出入**,已在卡内「偏差 C」留痕:卡写「落点 = `BootRoot.cs`」,
> 实际构造行在 `MinimalWorldFactory.cs:184`、由 `BootRoot.cs:219` 调用。
> 理由:`WorldLattice.cs:295-298` 明令 `WorldGeometry` 不得有「按尺寸造空白世界」入口 ⇒
> 「合成」必须显式具名 + 带响亮日志,300 行的启动序里塞不下这段论证。
> **判据本身不变**(可复算命令照样恰 1 命中),另加 `ac201b` 钉住「不是死工厂」。

---

## 三、证伪轮(S12-4 · 一属性一变异 · SHA-256 逐字节还原)

**spec**:`tools/falsify/specs/s20-17-world-assembly-wiring.json`
**复算**:

```bash
python3 tools/falsify/unity_falsify.py --check-only \
  --spec tools/falsify/specs/s20-17-world-assembly-wiring.json      # 锚点校验(纯读,零写盘)

python3 tools/falsify/unity_falsify.py \
  --spec tools/falsify/specs/s20-17-world-assembly-wiring.json \
  --guard unity/Assets/DataCooked --guard unity/Assets/AddressableAssetsData
# 期望末行:══ 证伪轮通过:5 个变异全部恰如预期 ══
```

### 第三轮(定稿)实测

| 变异 | 打什么 | 期望红 | 实测 | 判定 |
|---|---|---|---|---|
| **M1** | 删 `MinimalWorldFactory.cs:184` 的 `new World(` | ac201 / ac203 / ac203c / ac204 | 完全一致 | ✅ |
| **M2** | 删 `CompositionRoot.cs:941` 的 `new StructureWriter(` | ac202 / ac204 | 完全一致 | ✅ |
| **M3** | 断 `structures = world.Structures` 这条线 | ac203 / ac204 | 完全一致 | ✅ |
| **M4** | 停 `StructureKinds.cs:260` 的 `_occupancy.OccupyCells(occupied);` | ac204 | 完全一致 | ✅ |
| **M5** | 停 `StructureKinds.cs:281` 的 `dropWriter ?? throw` | ac205 | 完全一致 | ✅ |

还原:`SHA-256 逐字节校验通过`;受保护路径(`DataCooked` / `AddressableAssetsData`)指纹**未变**。

### 前两轮为什么没过 —— 两次都不是「预期写对了」,而是判据面被先后证伪

| 轮 | 现象 | 根因 | 归谁 |
|---|---|---|---|
| **第 1 轮** | M2 **逃逸**:ac202 没红 | 变异形态 = 「把 `if (world != null)` 改成 `if (false)`」。而 AC-2 判的是**源码文本里字面量恰 1 处** —— 停用守卫改不动文本 ⇒ 对这条判据零判别力。当时构造是**跨两行**的,单行替换删不掉它(删首行 ⇒ 孤立实参 ⇒ 编译失败)。 | **变异形态** |
| **第 2 轮** | M2 转红,但**多红一条 ac203** | 首版 `ac203` 同时断言 `ClinicRoomMatching != null` **和** `StructureWriter != null` —— 一个夹具担两个属性;M2 删写者 ⇒ 连带弄红 AC-3 的夹具。 | **夹具边界** |
| **第 3 轮** | 5/5 恰如预期 | ① 把写者构造合成**单行**(`CompositionRoot.cs:941`),使 `structureWriter = null;` 既能编译又能让字面量消失;② `ac203` 拆窄为只判匹配服务,写者存在性归 ac202 / ac204。 | — |

两轮处置都**没有**用「改预期去迁就实测」:
第 1 轮改的是**变异形态**(并为此让生产代码写成单行,源码处已留注说明这是可读性换判据可证伪);
第 2 轮改的是**夹具边界**(而不是把 ac203 塞进 M2 的 expectRed —— 那会把两个属性糊成一个判别面)。
两轮的完整实况留在 spec 的 `_history_2026-10-28_firstRun` / `_history_2026-10-28_secondRun` 里。

> ⚠️ **本轮的元教训(值得单列)**:AC 的判据若是「**源码文本某形态恰 N 处**」,
> 变异形态必须**真能改掉那段文本** —— 「停用守卫」是**行为**变异,对**文本**判据天然无效。
> 这类 AC 一旦配上行为变异,会得到「绿」而实际零反证,而且**看起来完全正常**。

---

## 四、全量回归

**基线溯源三元组**:截至时刻 `2026-10-28 04:26 CST` · 批次 `S20-17 收口 · 全量` · commit `a42d2df` + 本批未提交改动

| 套件 | 退出码 | 结果(XML 原值) | 基线(批 4 · `a42d2df`) | 差 |
|---|---|---|---|---|
| EditMode | `0` | `total=5575 passed=5540 failed=0 inconclusive=0 skipped=35` | `5567 / 5532 / 0 / 35` | **+8 / +8 / 0 / 0** |
| PlayMode | `2` | `total=238 passed=234 failed=0 inconclusive=2 skipped=2` | `238 / 234 / 0 / 2 / 2` | **0 / 0 / 0 / 0 / 0** |

> ⚠️ **本表是「最终树」上的数**,不是改注释之前那一跑:收口前把四处注释里的
> `new World(` / `new StructureWriter(` 字样去掉了(见 §二 的注),树随之变了 ⇒
> 重跑全量。两次跑**逐条相同**(EditMode 5575 条、PlayMode 238 条,用例名与结果零差异),
> 但只有这一跑对应最终代码。XML = `unity/Logs/s20-17-final2-{editmode,playmode}.xml`。

⚠️ **EditMode 的 +8 也不能只看汇总差,须用例级比对**(汇总 +8 可能掩盖「加 10 减 2」):

```bash
python3 - <<'EOF'
import xml.etree.ElementTree as ET
def load(p):
    t=ET.parse(p); return {(tc.get('fullname'),tc.get('result')) for tc in t.iter('test-case')}
new=load('unity/Logs/s20-17-final2-editmode.xml'); old=load('unity/Logs/editmode-batch4.xml')
print(len(new), len(old), "仅在new:", sorted(new-old), "仅在old:", sorted(old-new))
EOF
```

实跑输出(`5575 5567`,仅在 new = 8 条全名为 `…world_assembly_wiring_test.test_ac20*`,全部 `Passed`;
仅在 old = **空集**)⇒ **只增不减**,零回归。那 8 条正是本批新增的夹具
(`world_assembly_wiring_test`),逐条计入。

⚠️ **PlayMode 退出码 = 2 须单独说明,不能当绿看**:Unity 的 `-runTests` 在**存在 inconclusive / skipped**
时也返回非零。本轮的判据以 **XML 为准**,并做了**用例级严格比对**(不是只看汇总数):

```bash
python3 - <<'EOF'
import xml.etree.ElementTree as ET
def load(p):
    t=ET.parse(p); return {(tc.get('fullname'),tc.get('result')) for tc in t.iter('test-case')}
new=load('unity/Logs/s20-17-final2-playmode.xml'); old=load('unity/Logs/playmode-batch4-final.xml')
print(len(new), len(old), "仅在new:", new-old, "仅在old:", old-new)
EOF
# 实跑输出:238 238 仅在new: set() 仅在old: set()
```

⇒ 238 条的**用例名与结果逐条相同**,零差异;那 2 条 inconclusive / 2 条 skipped 是基线就有的。
**本批未引入任何 PlayMode 变化。**



**跑法**(两个 batch **串行**,Unity batch 独占工程)。下表数值由 `final2-` 这一对产物产生
(`final-` 是改注释之前那一跑,留存仅作「两次逐条相同」的对照):

```bash
cd unity
U=~/Unity/Hub/Editor/6000.3.24f1/Editor/Unity
"$U" -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform EditMode \
     -testResults "$PWD/Logs/s20-17-final2-editmode.xml" -logFile "$PWD/Logs/s20-17-final2-editmode.log"
"$U" -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform PlayMode \
     -testResults "$PWD/Logs/s20-17-final2-playmode.xml" -logFile "$PWD/Logs/s20-17-final2-playmode.log"
```

⚠️ `-runTests` **不得**带 `-quit`。

---

## 五、挂标不记绿(照抄卡面,一条不勾)

1. **世界烘焙数据** —— `world_geometry` / `world_buildslots` 仍零产出;`BuildSlotCatalog` 是空目录
   ⇒ `AC-23-08` 第三半边(占用格 ⊆ `BuildSlotRegion`)**仍无载体**,本卡**不解除**。
2. **`StructureWriter.Place` / `Remove` 的生产发起方** —— 归 42 的建造交互(`O-23-6`),**不在面内**。
   ⇒ 本卡交付 = 「写者已装配、可被调用」,**不是**「玩家能拆房」。
3. **`ClinicCellSource` 接真源** —— 装配后**具备条件**(registry 不再为 null),但接线归 24 侧,不在面内。
4. **`World` 的生命周期归属** —— 已在卡内裁定为「归 `BootRoot`」(见 §六);其**后果**
   (世界不随场景卸载而销毁 ⇒ 换关 / 读档后「旧世界还在」)**不处理**,归 ADR-023 拆序轮的实现期义务。
5. **`PlaceableChecker` 是否接进装配** —— 已在卡内裁定为「**不接**」(见 §六)。

---

## 六、实施轮新增的两条裁定(卡面第 4 / 5 项强制要求留痕)

**裁定 A · `World` 的生命周期归 `BootRoot`。** 实例在 `BootRoot.Start()` 启动序里由
`MinimalWorldFactory.Create()` 造出 → 交 `CompositionRoot.Assemble(world:)` → 经
`CompositionRootServices.World` 长期持有;**不**挂 `World.unity` 的 additive 根。理由:
① ADR-023 §② 要求 `World.unity` 零 gameplay GameObject(场景里摆持有者 = 第二真源);
② 拆序六步要 `ReleaseInstance` 全部产物并断言登记簿空,世界挂场景根会与「服务袋仍持引用」冲突;
③ 世界是**装配产物**而非 sim 状态,与 `SaveService` / `LoadSequencer` 同格。

**裁定 B · `PlaceableChecker` 本轮不接。** `StructureWriter.Place` 今日本就不调用它(只算足迹 + 写占用);
接进装配需要「建造合法性」判定面,而空槽位目录会让**每一次放置都被判非法** ⇒ `AC-4` 的 `Place` 永远失败。
⇒ 只装写者、不装合法性门。**这是「不接」,不是「忘接」。**

---

## 七、改动的文件

| 文件 | 性质 |
|---|---|
| `unity/Assets/Gameplay.Boot/MinimalWorldFactory.cs` | **新增**(合成世界工厂,含文件头论证) |
| `unity/Assets/Gameplay.Boot/BootRoot.cs` | 改(造世界 + `world:` 实参 + `IsSyntheticWorld`) |
| `unity/Assets/Gameplay.Boot/CompositionRoot.cs` | 改(两重载 + `AssembleCore` 增 `world`;注册表派生;写者构造;两份真源守卫) |
| `unity/Assets/Gameplay.Boot/CompositionRootServices.cs` | 改(两属性 + 两形参) |
| `unity/Assets/Tests/EditMode/ModularBuilding/world_assembly_wiring_test.cs` | **新增**(8 条夹具) |
| `tools/falsify/specs/s20-17-world-assembly-wiring.json` | **新增**(5 变异 + 两轮失败留档) |
| `production/sprints/sprint-20.md` · `production/sprint-status.yaml` | 卡面裁定留痕 / 状态 |

**无生产行为变化面**:既有 100+ 处 `Assemble()` 调用方不传 `world` ⇒ `World` / `StructureWriter` /
`ClinicRoomMatching` 三者仍 null(由 `test_ac203b` 钉住)。唯一的行为变化在 `BootRoot` 启动序。
