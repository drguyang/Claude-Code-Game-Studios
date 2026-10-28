# S20-20 实现证据 —— 最小关卡工具 + 建造槽位骨架全链

**日期**: 2026-10-28
**卡**: `production/sprints/sprint-20.md` §十 · **S20-20**(兑现 S20-19 挂标 #3)
**执行**: `unity-specialist` 子代理起草 → 主会话复核 / 修缺陷 / 跑证伪轮 / 补夹具
**用户裁定**: ① 只做挂标 #3;② **先做最小关卡工具(合规)**,不手写种子数据

---

## §一 交付物(11 件)

| # | 文件 | 作用 | 行数 |
|---|---|---|---|
| 1 | `unity/Assets/Editor.Tools.Level/LevelSource.cs` | 声明式关卡源 + `SlotType` **镜像**枚举(本程序集刻意零引用) | 95 |
| 2 | `unity/Assets/Editor.Tools.Level/P0ClinicLevel.cs` | P0 医馆种子:9 Decor + 4 Shell = 13 格 | 76 |
| 3 | `unity/Assets/Editor.Tools.Level/LevelExporter.cs` | 确定性 JSON(无 BOM · 行序 (x,y,z) 升序);导出前跑检查 | 145 |
| 4 | `unity/Assets/Editor.Tools.Level/LevelConsistencyCheck.cs` | 槽位子集 3 条硬失败 + **7 条未实现项(返回值里的数据)** | 115 |
| 5 | `unity/Assets/Editor.Tools.Bake/WorldBuildSlotsBinder.cs` | 五道域门,聚合 `throw` | 329 |
| 6 | `unity/Assets/Editor.Tools.Bake/WorldBuildSlotsCookedWriter.cs` | deterministic cooked,行序 (x,y,z) 升序 | 78 |
| 7 | `unity/Assets/Editor.Tools.Bake/WorldBuildSlotsBake.cs` | 烘焙入口 | 68 |
| 8 | `unity/Assets/Editor.Tools.Bake/DataBakeMenu.cs`(**改**) | `BakeWorldBuildSlotsCore` + 菜单项 | +65 |
| 9 | `unity/Assets/Gameplay.Presentation/WorldBuildSlotsCookedCodec.cs` | 读方,与写方镜像 | 190 |
| 10 | `unity/Assets/Gameplay.Presentation/DataCookedDisk.cs`(**改**) | `LoadBuildSlots()` | +8 |
| 11 | `unity/Assets/Gameplay.Boot/MinimalWorldFactory.cs`(**改**) | `Create()` 装载并 `RegisterSlot`;失败降级 + `LogError` | +55 |

**夹具**:`world_buildslots_bake_test.cs`(12 条)· `level_tool_consistency_test.cs`(7 条)。
**真产物**:`assets/data/world_buildslots.json`(13 格)+ `unity/Assets/DataCooked/world_buildslots.cooked.bytes`(296 B,`ConfigVersion = 0x146822D2`)—— 均由工具导出 + 烘焙,**零手写**。

---

## §二 主会话在子代理交付后做的四处订正(**均留痕,不是一次写对**)

| # | 问题 | 判据 | 处置 |
|---|---|---|---|
| 1 | `NobodyPresent` 只实现 `IPresenceQuery` 的一个成员 ⇒ **编译失败**(子代理在轮次上限前正要修) | `IPresenceQuery` 有 4 个成员 | 补齐 4 个 |
| 2 | 夹具断言报文前缀 `world_buildslots:`,实为 `world_buildslots.json:` | 首跑 `EXIT=2` / 1 红 | 改夹具(生产无错),断言改 `StartsWith` |
| 3 | **13 格全 `baked_initial = true`** ⇒ 语义是「开档整间医馆已建满」= 玩家**无可建格**,与本轮目的相反 | 读种子语义 | Shell 4 格 `true`;Decor 9 格 = 1 `true` + 8 `false`;补 C-7 断言(两侧都要有) |
| 4 | 新测试件含 `Application.dataPath` + 写盘调用,但**未在 `FsWriteBoundaryGate.Declarations` 登记** | 该门 R2;此前已三发同型 | 补第 9 条声明(写目标 `Path.GetTempPath()`) |

> ⚠️ 第 3 条是**语义缺陷而非编译缺陷** —— 它不会让任何夹具变红(运行期今日不应用
> `BakedInitial`),但会把「玩家能建东西」这件事在数据层提前否掉。**没有夹具能抓它**,
> 只有读语义能抓。

---

## §三 证伪轮(S12-4)

### §三之一 建造槽位全链 —— `s20-20-world-buildslots.json`

| 变异 | 靶点 | 预期红 | 第一轮实得 |
|---|---|---|---|
| `M1-registerSlotLoopDisabled` | `MinimalWorldFactory.cs:189` | 端到端目的判据 | ✅ exact |
| `M2-outOfBoundsGateDisabled` | `WorldBuildSlotsBinder.cs:223` | 越界域门 | ✅ exact |
| `M3-duplicateCellGateDisabled` | `WorldBuildSlotsBinder.cs:232` | 重复格域门 | ✅ exact |
| `M4-bakedInitialRequiredDisabled` | `WorldBuildSlotsBinder.cs:311` | `baked_initial` 必填 | ❌ **超集(1 → 9)** |
| `M5-cookedRowOrderReversed` | `WorldBuildSlotsCookedWriter.cs:43` | 往返 + 盘上一致 | ✅ exact(2 条) |

**M4 超集的根因 = 规格缺陷(不是夹具、不是生产)**:
第一版把 `if` 改成**单行带块**,其后的 `{ errors.Add(...); return false; }` 于是降级为
**无条件裸块语句** ⇒ 变异体退化成「**恒拒**」⇒ 凡走行解析的 9 条夹具全红。
判据:9 条的报文**全是「缺 baked_initial」**,不是「断言不符」。
**修法**:多写一行 `if (false)` 把原拒绝块变成它永不进入的子句。
**第二版单跑**(`s20-20-m4-single.json`)⇒ **exact**(1 红,`missingBakedInitial_isRejected`)。

⇒ **属性覆盖 5/5,但分两批(4 + 1),不合并记账。**
⚠️ **本 spec 整体从未 5/5 一次跑过** —— 第二版 M4 只做过单跑,不得声称「一次通过」。

### §三之二 关卡工具槽位子集 —— `s20-20-level-tool.json`

| 变异 | 靶点 | 预期红 | 第一轮实得 |
|---|---|---|---|
| `L1-duplicateCellCheckDisabled` | `LevelConsistencyCheck.cs:79` | 重复格 | ❌ **超集(1 → 2)** |
| `L2-noneSlotTypeCheckDisabled` | `LevelConsistencyCheck.cs:85` | `None` 哨兵 | ✅ exact |
| `L3-outOfRangeCheckXYDisabled` | `LevelConsistencyCheck.cs:94` | 越界格 | ✅ exact |
| `L4-exportSkipsConsistencyCheck` | `LevelExporter.cs:44` | 导出前跑检查 | ✅ exact |
| `L5-exportPrimaryKeyReversed` | `LevelExporter.cs:50` | 行序升序 | ✅ exact |
| `L6-unimplementedListTrimmed` | `LevelConsistencyCheck.cs:62` | 未实现项 7 条 | ✅ exact |

**L1 超集的根因 = 夹具耦合(不是变异过宽、不是夹具假绿)**:
`export_runsConsistencyCheck` 首版的违例源用的是**重复格** ⇒ 它证明的其实是
「重复格守卫」,不是「导出路径跑检查」⇒ 停用重复格守卫后它必然同红。
**修法 = 解耦夹具**(违例源改 `(SlotType)99` 闭集外值),**不是把预期改宽** ——
改宽会把「一条夹具同时钉两条属性」当成正常,那正是判别面不清的定义。
解耦后**单跑** L1(`s20-20-l1-single.json`)⇒ **exact**(1 红)。

### §三之三 结果汇总(**分三批,不合并记账**)

| 批 | 覆盖属性 | 结果 |
|---|---|---|
| 建造槽位全链 · 主轮 | M1 / M2 / M3 / M5 | 4 exact |
| 建造槽位全链 · M4 单跑 | M4 | 1 exact(第一版曾超集) |
| 关卡工具 · 主轮 | L2 / L3 / L4 / L5 / L6 | 5 exact |
| 关卡工具 · L1 单跑 | L1 | 1 exact(第一轮曾超集) |

⇒ **属性覆盖 11/11 全部反证转红,但四批不合并成「11/11 一次通过」。**
两处超集**都是规格 / 夹具缺陷,不是生产缺陷,也不是假绿夹具**:
- M4:变异写法使原拒绝块降级为**无条件裸块** ⇒ 变异体「恒拒」;
- L1:一条夹具**同时依赖两条守卫** ⇒ 停用任一条都会红。

⚠️ **两条教训已写进 spec 的 `_round1_note`**:① 把 `if (cond) stmt;` 改成单行带块后,
紧随其后的块会从 if 的 body **降级为裸块**;② 一条夹具只钉一条属性,否则反证会超集。

---

## §四 回归

**三元组**:截至时刻 `2026-10-28T02:30Z` · 批次 `S20-20 + 代码评审修复轮(A-21/A-22/A-23)后 · 全量` · commit `3ae5599e` + **未提交工作树**

| 套件 | 退出码 | XML | 对照基线(同带三元组) |
|---|---|---|---|
| EditMode | 0 | `total=5676 passed=5641 failed=0 inconclusive=0 skipped=35` | `5641/5606/0/0/35` @ `2026-10-28T01:25Z` · `S20-19 收口轮` · `3ae5599e` + 未提交工作树(**引用自** `impl-s20-19-build-initiator-2026-10-28.md`) |
| PlayMode | 2 | `total=238 passed=234 failed=0 inconclusive=2 skipped=2` | 同上:`238/234/0/2/2` ⇒ **零回归** |

> ⚠️ **PlayMode `exit=2` 不是失败**:`failed=0`,`inconclusive=2` 是既有的
> `test_u1assump6_judgement{1,2}_…_notRun` 两条(基线里同样是 2 条)。

**等式核账(可复算)**:`5676 − 5641 = 35`,两项来源相加恰等 ——

| 来源 | 条数 | 复算 |
|---|---|---|
| S20-20 新建 `world_buildslots_bake_test` | 12 | XML 按 fixture 计数 |
| S20-20 新建 `level_tool_consistency_test` | 7 | 同上 |
| **本修复轮** `slot_type_vocabulary_test` | 3 | 同上 |
| **本修复轮** `world_buildslots_codec_validation_test` | 9 | 同上 |
| **本修复轮** `minimal_world_slot_load_failure_test` | 3 | 同上 |
| `cooked_drift_all_artifacts_test` 的 `drift[world_buildslots.cooked]` 用例 | 1 | 登记 `BakedArtifacts` 后由 0 → 1 |
| **合计** | **35** | = `5676 − 5641` ✅ |

通过数同量:`5641 − 5606 = 35`(本轮**零失败**)。

复算命令:

```bash
python3 - <<'EOF'
import xml.etree.ElementTree as ET, collections
def snap(p):
    r = ET.parse(p).getroot()
    c = collections.Counter(t.get("fullname").rsplit(".",1)[0] for t in r.iter("test-case"))
    return {k: r.get(k) for k in ("total","passed","failed","inconclusive","skipped")}, c
now, _ = snap("unity/Logs/full-EditMode.xml")
print(now)                       # 期望 total=5676 passed=5641 failed=0 skipped=35
print(snap("unity/Logs/full-PlayMode.xml")[0])   # 期望 238/234/0/2/2
EOF
```

> ⚠️ **`unity/Logs/full-editmode.xml`(10:04 那次)** `total=5660 passed=5622 failed=3` ——
> 那是**抓出三条红**的运行(幽灵产物 / `assets/data` 字面量 / 漂移登记),**不是基线**。
> 误把它当基线会得到 `5676 − 5660 = 16` 的假增量。此注为防重犯而留。

---

## §五 代码评审修复轮(A-21 / A-22 / A-23,A-18 部分)

本件交付物在评审后按用户指令「**A-21 那几条一并改掉**」做了修复轮;
判定、反证与逐条结账见 `review-s20-19-2026-10-28.md` §5.3 / §5.5。摘要:

| ID | 改法 | 反证 |
|---|---|---|
| **A-21**(BLOCK) | **消镜像**:`Editor.Tools.Level` 引 `Sim`;新建 `SlotTypeVocabulary`(名/序数均派生自 `Sim.World.SlotType`);`WorldBuildSlotsBinder` / `BuildModulesBinder`(**第四份**副本)共用;`LevelExporter` 删掉零消费方的名字表 | `s20-20-a21-vocabulary.json` **3/3 exact** |
| **A-22** | `allowed_mask` 值域**双侧**收紧(`<= 0 \|\| > MaxAllowedMask`,上界由枚举派生);`baked_initial` 严格 0/1 | `s20-20-a22-codec.json` **2/2 exact** |
| **A-23** | catch 判据抽成 `public static IsRecoverableSlotLoadFailure`(排除两个 `ArgumentException` 子类) | `s20-20-a23-catch.json` **2/2 exact** |
| **A-18** | `Num()` 走 `InvariantCulture`,导出体 8 处 `Append(int)` 改道 | **无法取证**(Proven ✗,理由见评审件 §5.5) |
