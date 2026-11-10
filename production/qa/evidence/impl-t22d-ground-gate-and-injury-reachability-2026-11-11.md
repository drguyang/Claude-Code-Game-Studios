# T22 批四 —— 地面覆盖门 + 非致死伤情可达性 证据原件

**日期**:2026-11-11
**批次**:T22 批四(用户「开工 T22 批四(丁 · 非致死伤情)」→ 复勘发现该卡面已过期 → 用户改选 **丙 + 甲**)
**构建**:`PlayerBuildCli.BuildDevelopment` → `Result: Succeeded`(19.2 s · 206 769 666 B)
**运行**:`DISPLAY=:0`,1280×800,`-tutorialdiag`;存档根走查前移开、走查后原样还原(本轮存档留 `saves.t22d-run/`)

---

## 0. 先说清楚:原卡面为什么作废

| 事实 | 判据 |
|---|---|
| `TD-sim-03` **已于 2026-10-30 结案** | 债册索引行 `status = closed` |
| 注册表今日有 **7 件伤情,其中 2 件非致死** | `INJ_BLUNT`(钝击伤,自限型)· `INJ_DRUG_HARM`(药毒,自限型) |
| `INJ_BLUNT` 的技法门有解锁路径 | `TechniqueUnlockRules.Default` 的 `ForCase(2, 2)`(处置完一例刀伤 ⇒ 清创缝合) |

⇒ 「非致死伤情」的**数据面**早已做完。剩下来的是**另一半**:它能不能在实玩里被撞见 ——
`TD-medcons-15` 结案时逐字把这件事甩给 playtest(「未做实际游玩验证 —— 归 playtest,不挂本债」)。
本批就是去回答那一半,结果见 §3。

---

## 1. 丙:`TD-building-06` 的结构性解除(地面覆盖门)

**做了什么**:新增 `unity/Assets/Editor.Tools.Gates/GroundCoverageGate.cs`,并接进
`AssemblyGates.RunAll`(菜单 / 域重载 / 构建前三处共用)+ `GateWiringManifestTest` 的两份手写清单
(`EntryWiringManifest["RunAll"]` / `ClassConsumerManifest`)。

**判据五条**(任一不满足即红,全部 fail-closed):

| # | 判据 | 为什么它是独立的 |
|---|---|---|
| C1 | 找得到 `Ground` 对象 | 找不到 ⇒ **红**,不是「跳过」—— 扫描面丢失必须响 |
| C2 | 有 `MeshCollider`(启用 / 非 trigger / 与渲染网格同一网格) | 「渲得出、走不上去」是另一种不可用,故与 C4 分开 |
| C3 | 未旋转(四元数为单位) | 倾斜面不构成可走平面;继续算 AABB 只会给出**看起来对**的错数 |
| C4 | 覆盖面 ⊇ 世界格外接(每格 `L` 米,格心在整数坐标 ⇒ 需求区间 `[-L/2, (size-1)·L + L/2]`)+ 顶面齐平 `y = 0` | 这就是债本身 |
| C5 | 网格 fileID 在本门的内置表里(`Plane 10209` / `Quad 10210` / `Cube 10202`) | 未登记 ⇒ 静态判不出包围盒 ⇒ 红,而不是当作通过 |

**为什么读场景 YAML 而不 `OpenScene`**:本门要在**构建前回调**与**域重载**里跑,加开场景会给
编辑器场景栈加副作用;YAML 是稳定文本、零副作用、且能被夹具用合成字符串直测判据本体。
代价已写进文件头:**它读不到运行期实例化的地面** —— 将来若改成运行期生成,须改判「生成器的输入」,
**不得删门了事**。

**合入前先被自己的门抓了一次**:`ClassConsumerManifest` 漏登记 ⇒
`test_classManifest_coversEveryDeclaredClass` 转红(见 §4 M2)。这是清单机制的应有之义,如实记。

---

## 2. 丙 的判据与证伪

### 2.1 夹具 `ground_coverage_gate_test`(8 条)

| # | 测试 | 钉的性质 |
|---|---|---|
| 1 | `test_realScene_coversWorld_zeroErrors` | 真场景 + 真 `world_geometry` 产物 ⇒ 绿 |
| 2 | `test_originalDefect_planeAtOrigin_scaleOne_red` | **重演 2026-11-06 之前那份 Ground**(Plane / `pos(0,0,0)` / `scale(1,1,1)`)⇒ 红 |
| 3 | `test_worldGrows_groundUnchanged_red` | 同一份场景、世界改大 ⇒ 红(**「改世界尺寸」这条复发路径被挡**);并钉住「恰好覆盖」是绿(闭区间 + 容差) |
| 4 | `test_noMeshCollider_red_rendersButCannotWalk` | 删碰撞体 ⇒ 红 |
| 5 | `test_rotatedGround_red_failClosed` | 转 90° ⇒ 红(不瞎算) |
| 6 | `test_unknownMesh_failClosed` | 未登记网格 ⇒ 红 |
| 7 | `test_missingGroundObject_red_notSilentPass` | 场景里没有 `Ground` ⇒ 红 |
| 8 | `test_cubeGround_topOffsetAccounted` | `Cube` 的顶面偏移被真的算进去(`-0.5 + 0.5×1 = 0` 绿;抬高 1 m 红) |

### 2.2 证伪轮(四条,还原 = SHA-256 逐字节)

| 变异 | 改法 | 结果 |
|---|---|---|
| **M1** | **数据**:把 `World.unity` 的 Ground 改回 `pos(0,0,0)` / `scale(1,1,1)` | **1 红**:`test_realScene…`,报文逐字给出 `Plane × scale(1,1) 覆盖 x∈[-5,5] … 世界格需 x∈[-0.5,15.5]` |
| **M1b** | 同 M1,改跑 `AssemblyGateRunAllTest` | **1 红**:`AssemblyGates.RunAll 红行 1 条` —— **证明门在真链上、且吃的是真场景** |
| **M2** | 删掉 `RunAll` 里的 `Add("地面覆盖", …)` 接线 | **2 红**:`entryWiring_matchesManifest_bidirectionally`(门从链上掉线)· `classManifest_coversEveryDeclaredClass` |
| **M3(首枪·未红)** | 在碰撞体分支里加 `if (b.ClassId == 64) { }` | **0 红** —— **变异打错靶**:这是空 if,行为一字未改 |
| **M3b(二枪)** | `if (!haveCollider)` → `if (false && !haveCollider)` | **1 红**:`test_noMeshCollider_red_rendersButCannotWalk` |
| **M4** | 覆盖比较整体短路(`if (false && …)`) | **2 红**:原缺陷 + 世界变大 |
| **M5** | `surfaceY = pos.Y`(丢掉 `TopOffsetY`) | **1 红**:`test_cubeGround_topOffsetAccounted` |

**M3 首枪的教训如实登记**:变异写完没跑就以为会红 —— 而它连行为都没改。
这是 `tools/falsify/README.md`「变异打错靶」的教科书形态;**M3b 才是对那条判据的证明**。

四文件 SHA-256 逐字节还原(`World.unity` / `AssemblyGates.cs` / `GroundCoverageGate.cs` / 夹具),
`grep -rn "FALSIFY" unity/Assets` 零命中。

---

## 3. 甲:非致死伤情「可撞见」—— **答案是不能**,而且根因不是技法门

### 3.1 真包实测

```text
[tutorialdiag] 病人出现 #0 id=1 disease=2 注册表=17 hook=0
[tutorialdiag] 病人出现 #1 id=2 disease=3 注册表=17 hook=0
（此后约 55 秒墙钟零新增；`在场=2` 恒定）
⚠️ 这段墙钟里有一段世界被结案页冻结（模态摊开 ⇒ tick 停），故「零新增」本身不构成判据；
   真正的判据是下一节的**硬上限常量**（`MaxPatientsDuringWiring = 2`）—— 它不依赖观察时长。
```

### 3.2 两条入口都不通向伤情

| 入口 | 事实 | 判据 |
|---|---|---|
| **接线驱动** `PatientAppearedDriver` | 每会话上限 `MaxPatientsDuringWiring = 2`;轮转从注册表**首位**起扫 ⇒ 前两例恒为 `2`(刺伤)/ `3`(切割伤) | 常量在 `PatientAppearedDriver.cs:70`;实测 `#0 disease=2 / #1 disease=3` |
| **52 事件池** | `medical` 条目只映射病种 `{7, 9, 11, 17}`,**零伤情**;其余条目 `disease_id = 0` | `python3 -c` 读 `assets/data/52_event_pool.json` 的 `entries[].disease_id` |

⇒ `INJ_BLUNT`(注册表第 14 位之后)与 `INJ_DRUG_HARM`(更后)**在本会话内不可达**,
而**技法 2 解不解锁都改变不了这件事** —— 卡点不是技法门,是「两人上限 + 轮转起点固定」。

⚠️ **未验证的一步(如实登记)**:本轮**没有**真的走通「结案 ⇒ 技法 2 解锁」这条链 ——
结案页要求「窗口内存在处置事件」(`病未处置` 拒绝行在屏上实拍到了),而本轮只做了逐行查体、
没做处置动作。技法 2 的解锁路径因此仍是**代码依据**(`TechniqueUnlockRules.Default` 的 `ForCase(2,2)`),
不是本轮的观测。它对本节结论无影响(见上:卡点在人数上限),但不得被读成「已实测解锁」。

### 3.3 结论

**「药害 / 非致死伤情在 P0 可撞见」今日 = 否。** `TD-medcons-15` 把这件事甩给 playtest 是合理的,
但需要补一句:在**今天这份构建**上,playtest 也撞不见 —— 不是玩家没找到,是**没有入口**。
已登记新债 `TD-sim-07`(见债册)。

---

## 4. 回归(三元组)

| 套件 | 结果 | 截至时刻 | 批次 | commit |
|---|---|---|---|---|
| EditMode | `7419 / 7382 / 0 / 0 / 37` | `2026-11-10T17:36:16Z`–`17:38:22Z` | T22 批四 | `e2b11e51` + 工作树 |
| PlayMode | `376 / 371 / 0 / 2 / 3` | `2026-11-10T17:39:00Z`–`17:39:22Z` | T22 批四 | `e2b11e51` + 工作树 |

基线(T22 批三):EditMode `7411 / 7374 / 0 / 0 / 37` · PlayMode 同。
**等式**:`7419 − 7411 = 8` = 本批新增夹具 `ground_coverage_gate_test` 的 `[Test]` 计数;PlayMode 零变化。

---

## 5. 本批没有做的(不得借绿)

| 项 | 状态 |
|---|---|
| 「结案 ⇒ 解锁技法 2」实测 | **NOT-RUN**(需先做处置动作;结案页的 `病未处置` 拒绝行已实拍到) |
| 药害(禁忌 / 过量)真跑 | **NOT-RUN** —— 且按 §3 今日**无入口** |
| `INJ_BLUNT` 的数值定稿 | 仍是草案(归数值轮) |

---

## 6. 证据文件索引(本地,不入库)

| 文件 | 内容 |
|---|---|
| `unity/Logs/t22d-run.log` | 甲 的全程(零异常) |
| `unity/Logs/shots/t22d/05-after-submit.png` / `08-close-now.png` | 结案页 + `病未处置` 拒绝行 |
| `unity/Logs/t22d-M{1,1b,2,3,3b,4,5}.xml` | 七轮变异的测试结果 |
| `unity/Logs/t22d-build.log` | 构建读数 |
