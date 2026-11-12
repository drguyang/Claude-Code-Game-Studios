# 3D 面数门实现原件(`PolyBudgetGates`)

> **日期**:2026-11-12
> **对象**:`art-bible.md` §8.10.6.5「质量门(构建期,失败即拒)」的**执行体**
> **结论**:**门已落地并证明会红**;但**三角面测量本身仍为 0 条**(两件 spec 均 `asset: pending`)。
> **性质**:实现 + 探针轮证据。**不是**「3D 资产合规」的证明。

---

## 1 为什么建

`§8.10.6.5` 的表格一直自称「构建期 fail-closed 门」,而实测:

```bash
grep -rn "vertexCount\|sharedMesh\|GetIndexCount" unity/Assets/Editor.Tools.Gates/*.cs
# 零命中
```

⇒ 3D 资产的面数上限**从来没有执行体**。Seed3D 试件以 ~50 万面通过,不是因为「批过了」,
而是因为**没有任何东西会红**。

---

## 2 改了什么

| 文件 | 性质 |
|---|---|
| `unity/Assets/Editor.Tools.Gates/PolyBudgetGates.cs` | **新增** · 解析 + 判决 + 生产入口 |
| `unity/Assets/Tests/EditMode/Sim/poly_budget_gate_test.cs` | **新增** · 15 条夹具 |
| `unity/Assets/Editor.Tools.Gates/AssemblyGates.cs` | 接入 `RunAll`(菜单 / reload / 构建前三处共用) |
| `unity/Assets/Tests/EditMode/Sim/gate_wiring_manifest_test.cs` | 两份**手写**清单各补一条 |
| `design/assets/specs/{church-wall,tent}.md` | 各加一个 `budget` 块 |

**输入形态**(单一真源 = spec,不另建清单):

```
asset: pending            # 或 Assets/Art/.../x.fbx
family: building-module
tier: hero
scene: city-ruins
```

## 3 判据

| # | 判据 |
|---|---|
| P1 | `family` 必属 §8.5 的 11 档 |
| P2 | `tier ∈ {normal, hero}`,缺省 `normal` |
| P3 | hero 只对 `building-module` 合法 |
| P4 | 每场景 hero ≤ 3;hero 未写 `scene` ⇒ 红 |
| P5 | 三角面 > 上限 ⇒ 红;`asset: pending` ⇒ **只记未测,不算通过** |
| P6 | 声明 `Assets/...` 但文件不存在 ⇒ 红(fail-closed) |
| P7 | 同一 asset 重复声明 ⇒ 红 |
| P8 | 声明数 = 0 ⇒ 红(输入面丢失) |

---

## 4 夹具

```bash
Unity -batchmode -runTests -projectPath unity -testPlatform EditMode \
  -testFilter PolyBudget -testResults /tmp/poly.xml
```

结果:**15 / 15 Passed**(2026-11-12)。

> **首跑 14/15**,唯一失败是**测试自己的字面量写错**:报文用 `N0` 千分位输出 `8,001`,
> 断言写的是 `"8001"`。门判对了,测试判错了 —— 已按字面量改断言,未改门。

---

## 5 接线

接进 `AssemblyGates.RunAll`(不是 `RunMenu`)。

> **本轮真实抓错**:首版把调用加进了 `RunMenu`,`gate_wiring_manifest_test` 当场报
> 「`[RunAll]` 门从链上掉线 / `[RunMenu]` 新接线未声明」。
> 若没有那张手写清单,症状会是:**菜单里点一下能跑、真构建不会跑,而构建依然绿。**
> 这正是 `TD-gate-01` 立清单的理由。修后 `GateWiringManifestTest` **15/15**。

---

## 6 探针轮(证明「构建时真的会跑」)

**变异窗口**:`unity/Assets/Editor.Tools.Gates/PolyBudgetGates.cs`(窗口内不读该文件)。
在 `RunAllDetailed` 入口插无条件 `[PROBE]` 错误并提前返回。

构建命令:

```bash
Unity -batchmode -quit -projectPath unity \
  -executeMethod DaYiJingCheng.EditorTools.Bake.PlayerBuildCli.Build
```

日志实测:

```
:1100 [PROBE] PolyBudgetGates.RunAllDetailed reached
:1121 BuildFailedException: [AssemblyGates] 构建前门失败 1 条 —— 失败门组:3D 面数
:1197 Build Finished, Result: Failure.
```

**还原判据 = SHA-256 逐字节**:

```
df1f1c0e66f909ab84210120c0d9b2b0f70cf0e7477b69853e9f5151b3d0b502  (变异前记录)
df1f1c0e66f909ab84210120c0d9b2b0f70cf0e7477b69853e9f5151b3d0b502  (还原后实测)
PROBE 出现次数: 0
```

---

## 7 干净构建

```
[PolyBudget] …/church-wall.md:10 asset=pending ⇒ 三角面未测(不是通过);上界 20000(building-module/hero)
[PolyBudget] …/tent.md:10        asset=pending ⇒ 三角面未测(不是通过);上界 20000(building-module/hero)
[PolyBudgetGates] 声明 2 / 实测 0 / 未测 2
Build Finished, Result: Success.
```

**「声明 2 / 实测 0」被显式打出** —— 不是「2 件合格」,也不是静默跳过。

---

## 8 回归

| 截至时刻 | 批次 | commit | 数字 |
|---|---|---|---|
| `2026-11-12T05:11:52Z` | 面数门实现轮 · 全量 EditMode | `831ad706` + 工作树 | **`7468 / 7431 / 0 / 37`** |

- 0 失败、0 inconclusive。
- **不记「+15 增量」**:上一份可查基线是 2026-11-09 的 `7233/7196/0/37`,其间另有批次加测试;
  本轮确定贡献 = 新增 15 条面数门夹具(单跑 `PolyBudget` = 15/15)。
- `GateWiringManifestTest` 15/15。

---

## 9 本件**不**证明

- **3D 资产是否合规** —— 两件 spec 均 `pending`,**实测 0 条**;
  三角面比较的代码路径在真实资产上**从未被执行过**。
- **Unity 侧三角面计数是否正确** —— `CountTrianglesInUnity` 走 `MeshFilter.sharedMesh` + `GetIndexCount`,
  逻辑简单,但**没有用真 FBX 跑过**(本工程此刻无 3D 资产)。首次接真资产时须补一条实测。
- **§8.5 的数值本身是否合理** —— 那是设计裁定,门只执行它。

---

## 10 下一步(触发条件)

| 动作 | 触发 |
|---|---|
| 把 `asset: pending` 改为真实 `Assets/...` 路径 | 任一 3D 资产进工程时 |
| 补一条「真 FBX → 计数 → 比较」的夹具 | 同上(当前计数路径零实测) |
| 与 `TD-art-01` 联动:hero 名额 >3 或卡片方案落地后复核 | P1a 美术量产前 |
