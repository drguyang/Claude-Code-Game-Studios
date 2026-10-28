# 实现证据 —— `LevelConsistencyCheck.Check` 拆分轮(纯重构)

> **对象**:`unity/Assets/Editor.Tools.Level/LevelConsistencyCheck.cs`
> **日期**:2026-10-29 · **类型**:纯重构(无可观测行为变更)
> **由来**:story-006 评审 `M-5` / 复评 `B-5` 登记的**唯一未结项** ——
> `Check` 方法 **163 行 > `coding-standards` 的 ≤40 界**。当时**故意不混进修复批**,
> 理由逐字:「混批会让证伪窗口覆盖两件事,归因变难」。本轮单开。
> **上下游**:`production/qa/evidence/review-level-tool-story-006-2026-10-29.md` §8.1 `B-5` / §8.5 ①

---

## §一 拆分结果(逐方法行数,**实测**)

复算命令:

```bash
python3 - <<'PY'
import re
p="unity/Assets/Editor.Tools.Level/LevelConsistencyCheck.cs"
lines=open(p,encoding="utf-8").read().split("\n")
starts=[i for i,l in enumerate(lines) if re.match(r'^        (public|private) static .*\(', l)]
starts.append(len(lines))
for a,b in zip(starts[:-1],starts[1:]):
    d=0;o=False;e=None
    for j in range(a,min(b+1,len(lines))):
        d+=lines[j].count("{")-lines[j].count("}")
        if "{" in lines[j]: o=True
        if o and d==0: e=j;break
    print(re.search(r'(\w+)\s*\(',lines[a]).group(1), e-a+1)
PY
```

| 方法 | 行数 | 职责 |
|---|---|---|
| `Check` | **25** | 编排:建三表 → 六次调用 → 构造报告 |
| `CheckSlots` | 12 | 槽位循环,逐条委派 |
| `CheckSlotCell` | **35** | ①重复格 ③越界 ③之补不可走格 |
| `IsOutOfBounds` | 4 | ③ 的判据式(抽出以压分支数) |
| `CheckSlotType` | 11 | ②类型越界 |
| `CheckSlotInitialModule` | 27 | ④⑤ `baked_initial` ↔ `module_id` 双向矛盾 |
| `CheckEcozones` | 8 | C4 |
| `CheckNavMesh` | 17 | C3(**告警**) |
| `CheckWalkability` | 29 | C2(硬失败) |
| `CheckColliderFootprints` | 16 | C2'(硬失败) |
| `CheckLayerDrift` | 11 | C1(**告警**) |

**最大 35 行**(`CheckSlotCell`),全部 ≤40。文件 344 → 402 行(增量全在每方法的
`<summary>` 与分区注释;实现语句未增一行)。

---

## §二 等价性论证(**按构造**,不是按观察)

| # | 原形态 | 新形态 | 为什么等价 |
|---|---|---|---|
| 1 | 单个 `Check` 体 | 六个私有方法顺序调用 | 顺序**逐字保持**:slots → C4 → C3 → C2 → C2' → C1(决定 `Violations` / `NotRun` 的**追加次序**) |
| 2 | `warnings` 惰性 `null` | 常驻 `List` + 返回处 `Count == 0 ? null : warnings` | 折回后构造实参与原式同形 |
| 3 | ③之补 的守卫里**重复判了一次界** | ③ 命中即 `return`,③之补 不再判界 | ③ 与「不在 `[0,W)×[0,H)×[0,D)`」是**同一个谓词**;③ 已命中 ⇒ ③之补 的守卫在原式下也恒假 |
| 4 | `if (c1.Count > 0) { ... }` | 无条件 `AddRange(c1)` | 空表 `AddRange` 是 no-op |

> **※ 如实登记一处不可观测的微差**:拆分前若 `navSampler != null` 且 C3 零告警,
> 传出的是**空 `List`**;现在是 `Array.Empty`。两者 `Count == 0`、枚举结果相同,
> 经 `IReadOnlyList<string>` 无法区分 ⇒ 不构成行为变更,但**不是**「逐字相同」。
> 已在源码就地登记(`LevelConsistencyCheck.cs` 的返回处注释)。

---

## §三 拆分**引入**的结构风险,以及为什么补了一条夹具

第 3 条等价性有个副作用:**原来「越界格不会去索引可走表」是由守卫里的重复判界兜住的,
现在只靠那一句 `return`。**

而盘上**没有任何夹具**覆盖「越界槽位 + 已声明可走性」这个组合 ——
既有的 `test_levelTool_outOfRangeCell_reportsViolation` 构造 `LevelSource` 时**没声明 `walkable`**,
走的是 `Walkable == null` 那条早退,与这句 `return` 无关。
⇒ 删掉 `return` 只会得到异常,**没有任何一条测试变红**。「重构后等价」这句话在机器上不可判。

**新增夹具**:`unity/Assets/Tests/EditMode/LevelTool/level_tool_consistency_test.cs`
`test_levelTool_outOfRangeCell_withWalkableDeclared_doesNotIndexWalkable`

三条断言(缺一都不够):
1. `Assert.DoesNotThrow` —— 不得越界索引;
2. `IsGreen == false` + 报文点名「超出声明范围」—— 不得因为要防崩就把违例也吞掉;
3. **`Has(report, "不可通行") == false`** —— 越界格**不是**「不可通行格」,
   两者是不同判据,不得互相冒充(否则一个「越界一律当不可走」的实现也能过前两条)。

---

## §四 证伪轮(**S12-4 形态**)

**预判先写(跑之前)**:删掉 ③ 命中后的 `return;`
⇒ `…_doesNotIndexWalkable` **红**(异常),同类老夹具 `…_outOfRangeCell_reportsViolation` **仍绿**。

| 项 | 值 |
|---|---|
| 变异 | `LevelConsistencyCheck.cs` 中 ③ 命中后的 `return;` **删除** |
| 靶 | `DaYiJingCheng.Tests.LevelTool.level_tool_consistency_test` 全类 |
| **实得** | **`total=10 passed=9 failed=1`** —— 唯一红 = `test_levelTool_outOfRangeCell_withWalkableDeclared_doesNotIndexWalkable`,报文 `Expected: No Exception to be thrown / But was: <System.ArgumentOutOfRangeException…>` |
| 对照 | `test_levelTool_outOfRangeCell_reportsViolation` **仍绿**(符合预判:它没声明 `walkable`) |
| 还原判据 | **SHA-256 逐字节** `ebf261233f7ed10e07f51d3478f26716b67958eeb874131a6d759d623dde38c6`,比对一致 ✅ |

> ⚠️ 实得异常类型是 `ArgumentOutOfRangeException` 而非预估的 `IndexOutOfRangeException`
> —— `LevelSource.Walkable` 的索引走的是自定义访问器。**红/绿的预判完全命中**,
> 只有异常类型的预判错了,如实登记。

---

## §五 回归(**三元组:截至时刻 · 批次 · commit**)

| 套件 | 结果 | 对照基线(同形三元组) |
|---|---|---|
| EditMode 全量 | **`5850 / 5813 / 0 / 0 / 37`** @ 2026-10-29 07:45 CST · `Check` 拆分轮 · `77090ee3`+工作树 | `5849 / 5812 / 0 / 37` @ 2026-10-29 07:30 CST · story-006 修复轮 ⇒ **+1 = 新增的那条夹具**,failed 0 → 0 |
| LevelTool 子集 | **`126 / 124 / 0 / 2`** | 拆分前 `125 / 123 / 0 / 2` ⇒ **+1** |
| PlayMode | **`238 / 234 / 0 / 2 / 2`** @ 2026-10-29 07:45 CST · 同批 · 同 commit | 与基线**逐项相同**。进程退出码 2 由那 2 条 **Inconclusive** 引起,XML `failed = 0`,**与本轮无关**(承 story-006 实现轮的同一登记) |

等式:`5850 − 5849 = 1` = 本文件 §三 新增的 `[Test]` 计数。

---

## §六 如实边界(**本件证明不了的事**)

1. **纯重构没有「新属性」可证伪** —— 旧写法与新写法值相等时,任何夹具都分不出。
   本轮能钉住的只有 §三 那**一处**由拆分引入的结构风险;
   **其余 10 个方法的等价性建立在「逐行阅读 + §二 的按构造论证」上,不是机器判据。**
   按本仓 `feedback_refactor_falsifiability_ceiling` 的口径:**Proven 如实记 ✗,只记 Landed ✅**。
2. **`Check` 之外仍有 18 个方法 >40 行**(全树实测,复算见 §七)。**本轮未动** ——
   评审 `M-5` 只点了其中几处,其余是更早的代码;混进来会让本轮的证伪窗口失去归因力。
3. 未跑 `/code-review`。纯重构 + 全量回归 + 一条反证,以此为据;若用户要评审轮,另开。

---

## §七 复算命令(全树 >40 行方法)

```bash
python3 - <<'PY'
import re,glob,os
for p in sorted(glob.glob("unity/Assets/Editor.Tools.Level/*.cs")):
    lines=open(p,encoding="utf-8").read().split("\n")
    for a,l in enumerate(lines):
        if not re.match(r'^        (public|private|internal) .*\($', l.rstrip()): continue
        d=0;o=False;e=None
        for j in range(a,len(lines)):
            d+=lines[j].count("{")-lines[j].count("}")
            if "{" in lines[j]: o=True
            if o and d==0: e=j;break
        if e and e-a+1>40:
            print(f"{os.path.basename(p):34s} {re.search(r'(\w+)\s*\(',lines[a]).group(1):28s} {e-a+1:4d}")
PY
```

输出(2026-10-29 实测):`EcozoneSource.TryCheck 47` · `EcozoneSource.CheckOneZone 55` ·
`LayerDriftCheck.TryCheck 56` · `LevelCiRunner.Render 43` · `LevelExporter.ExportBuildSlots 51` ·
`ExportTerrain 50` · `ExportResources 59` · `ExportNavChunks 41` · `LevelSource.ctor 88` ·
`NavChunkSource.Slice 50` · `NavChunkSource.TryCheck 69` · `NavMeshConsistencyCheck.TryCheck 50` ·
`NavMeshProbe.Run 51` · `TerrainApiSpike` 的三条 Probe 62/75/41 · `TerrainSource.ctor 90`。
**`LevelConsistencyCheck` 零命中。**
