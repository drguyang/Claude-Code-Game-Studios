# camera-viewpoint Story 003–006 评审修复轮(五步循环 · 5B 修复)

- **日期**: 2026-10-03
- **范围**: `production/epics/camera-viewpoint/story-00{3,4,5,6}`
- **前置**: 双代理评审原件(8 份)已落 `production/qa/evidence/review-camera-viewpoint-story-00*-{qa-lead,td}-2026-10-03.md`
  —— 四 story **均判「不应维持 Complete」**
- **用户裁定(三轮)**:
  1. **004 B1** = **按 GDD 主用例改**(`false ⇒ ARM_LEN`;起点重叠区分登记 spike)
  2. **005 AC-2-27** = **本轮补齐生产接线**(`PhysicsArmQuery` + 相位驱动方)
  3. **VR/接口面**(006 §2 + 005 A)= **本轮一并修**(`ICameraRig` 四成员 + `OQ-2-6` 注释 + `FirstPerson` 独立路径)
- **结论**: **四 story 全 BLOCKING 已闭,复跑 0 红**;各自转 `Complete ✅`

---

## 1. 逐 BLOCKING 修复对账

> 判据 = 「原判定 → 修复落点 → 验证命令」。命令均可在本仓复现。

### Story 003(B1 + A1)

| 原判定 | 修复落点 | 验证 |
|---|---|---|
| **B1** `MAX_DT` 违反单一来源红线且判据假绿 | `AnchorFollower.cs`:`MaxDtMs` **去字面量默认** + 新增 `FromWorldLattice(in WorldLatticeParams)` **唯一装载路径**;测试改三面判据 | `grep -n "MaxDtMs" unity/Assets/Gameplay.Presentation/Camera/AnchorFollower.cs` 无 `= 100`;`test_ac212b_*` |
| **A1** `ApplyOrbit` 丢弃 `dt`/`SENS`;期望值钉死非规格实现 | `CameraRig.cs`:`ApplyOrbit` 按 GDD F-2-2 式落地;新增 `LOOK_SENS_X/Y`;测试期望由规格推导 | `sed -n '/ApplyOrbit/,/^        }/p' unity/Assets/Gameplay.Presentation/Camera/CameraRig.cs` |

### Story 004(B1–B4)

| 原判定 | 修复落点 | 验证 |
|---|---|---|
| **B1** `false ⇒ CAM_MIN_DIST` 反转 GDD 主用例 | `CameraArmSolver.cs:Step`:`dRaw = hit ? dist : _p.ArmLen`;补真走 `Hit=false` 夹具 | `test_f24_miss_returnsArmLen_openWorldNoOcclusion` |
| **B2** `ViewDir` 手性符号反转 + 零覆盖 | `CameraArmSolver.ViewDir`:改逐字 Rodrigues(`ê_back·cosθ + (r̂×ê_back)·sinθ`);补手性夹具 | `test_ac225_viewDir_*` |
| **B3** AC-2-15① 自证假绿 + 生产默认 `CamCollideMask = 0` | `CameraArmParams`:`DefaultCollideMask` / `CharacterLayerMask` 常量 + `ValidateCollideMask`;测试取登记常量 + 两负夹具 | `test_ac215a_maskEqualsWhitelist_notSuperset` |
| **B4** AC-2-25④ 无装载期守卫 | `CameraArmParams.ValidateArmLen` + `ValidateAll` | `test_ac225d_armLenPositive_planeNotFirstPerson` |

### Story 005(A + B/C/D + E/F/H + 收口)

| 原判定 | 修复落点 | 验证 |
|---|---|---|
| **A** `FirstPerson => 3` 与 Casebook 撞车 | `CameraModeMachine`:FirstPerson 退出优先级格;`Settle` 早退建 VR 独立路径;`ApplyMode` 收为唯一写入点 | `test_ac227_firstPerson_independentPath_notPriorityCell` |
| **B/C/D** AC-2-27 三判据假绿 + 生产接线缺席 | 新增 `PhysicsArmQuery` + `CameraEvaluationDriver`(单一相位)+ `CountingArmQuery.BeginFrame(frameId)` 严格递增帧号 | `test_ac227a/b/b2/c` |
| **E** AC-2-17② 扫错方法 + 死代码 | 判据扫真写入点 `ApplyMode` | `test_ac217_setMode_doesNotReadOtherSystems` |
| **F** `TRANSITION_JUMP_EPS` 零命中 | `CameraRig.TRANSITION_JUMP_EPS` 唯一定义实体 + 逐帧采样夹具 | `test_ac218c2_transitionJump_boundedByConstantEntity` |
| **H** `CasebookPitch` public 可写 | 改 `private set` + `ConfigureCasebookPitch` | `test_ac219b_casebookFixedHighPitch_realConvergence` |
| **AC-2-19①② / 20 / Settle 返回** | `SyncPoseFromMode` 真收敛路径 · 语义可达性扫描 · Settle 返回本帧变更 | 对应夹具 |
| **K** 例数 13→12 + `_待填_` | story 文档订正 | `grep -n "_待填_" story-005*.md` 空 |

### Story 006(B-1…B-7 + EPIC)

| 原判定 | 修复落点 | 验证 |
|---|---|---|
| **B-1** 反向引用整档 `Contains` | `AssertReverseReference` 改 **§Dependencies 节内 + 行级** + 义务编号断言 | `test_ac222_1/2/4/5/6` |
| **B-2** ④ 陈旧措辞未检 | 补差异检测;回刷 `player-controller-and-movement.md` 的 `O-14` 行 | `test_ac222_4_playerController_reverseReference` |
| **B-3** AC-2-21 守卫恒真(EXTERNAL 恒绿) | 守卫锚定 `OQ-1-14` 行状态列 | `test_ac221_external_pitchMaxAwaitsUserDecision` |
| **B-4** VR 接口面只验存在 | 补 `ICameraRig` 四成员齐 + `OQ-2-6` doc comment 在位断言 | `test_vrInterface_*` |
| **B-5** AC-2-24/26 无 NOT-RUN 登记 | 各补 `Assert.Ignore` | `test_ac224/226_advisory_notRunRegistered` |
| **B-6/B-7** 自指空转 / 计数 / 禁混入半句零断言 | null guard + 接收方断言 + EPIC 就绪度表级断言 | `test_ac222_obligationsHaveDeclaredReceivers` / `test_ac226_advisoryGatesAreThree_notMixedIntoBlocking` |
| **EPIC** 三处自相矛盾 | 头行 / §Epic Status / Stories 表统一为 `In Progress` | `grep -n "Status" production/epics/camera-viewpoint/EPIC.md` |

---

## 2. 验证(可复现)

```bash
UNITY=~/Unity/Hub/Editor/6000.3.24f1/Editor/Unity
# CameraViewpoint 全组
$UNITY -batchmode -nographics -projectPath unity -runTests -testPlatform EditMode \
  -testFilter "DaYiJingCheng.Tests.CameraViewpoint" \
  -logFile unity/Logs/cam-fix-editmode5.log -resultFile /tmp/cam-fix-editmode5.xml
# 结果:total=75 passed=68 failed=0 skipped=7(2026-10-03)

# 全量 EditMode(回归)
$UNITY -batchmode -nographics -projectPath unity -runTests -testPlatform EditMode \
  -logFile unity/Logs/cam-fix-full2.log -resultFile /tmp/cam-fix-full2.xml
```

## 3. 残留(登记,不借绿)

| 项 | 归属 |
|---|---|
| 起点重叠(EC-2-1)与「真未命中」的区分 —— 须 `CheckSphere` 预判(与「每帧恰一次」冲突) | spike / 另裁 |
| `PhysicsArmQuery` 的场景级集成实测(本批只接线,场景资产未就位) | 后续 |
| `SetModeForTest` 仍公开(生产码不得调用的结构断言未加) | 后续 |
| 组 5 单一参数表对象 · `CameraRig._distance` 第二臂长源收敛 | 005 接线轮 |
| AC-2-21 / 23(EXTERNAL)· AC-2-03/24/26(ADVISORY playtest) | 用户 / playtest |

> ⚠️ **本件是修复轮对账件,不是评审原件** —— 原判定见 8 份 `review-camera-viewpoint-story-00*-2026-10-03.md`。
