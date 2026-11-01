# 实现原件 —— emergency-procedures/story-011 移动压制租约生产接线

> **对象**:`production/epics/emergency-procedures/story-011-motor-suppression-lease-wiring.md`
> **批次**:`emergency-procedures` storming(catalog-2026-11-02 后追加;**未排 sprint** —— 见 §六)
> **截至时刻**:`2026-11-02 03:xx CST` · **commit**:`11e29dbe` + 本批未提交工作树
> **裁定**:用户 2026-11-02「甲,开工」⇒ **R-11.4 甲案**(1 的读面一并接)

---

## 一、交付物

| 文件 | 变更 |
|---|---|
| `Gameplay.Presentation/Player/PlayerController.cs` | `_motorLease` 字段(**全案唯一实例**)+ 只读 `MotorLease` 属性;`Move` 在 `ValidateMoveInput` **之后**、`vTarget` **之前**读 `IsSuppressed` 清零输入(**不 early-return**) |
| `Gameplay.Presentation/Player/EmergencyMotorSuppression.cs` | **新**:10 侧写面。住 Presentation 是 `AC-1-23` 机械门的**硬要求**(见 §四) |
| `Gameplay.Boot/EmergencyChannelDriver.cs` | `BindMotorLease` / `HasMotorLease`;三个 helper 级 Acquire/Release,挂四个出口 |
| `Gameplay.Boot/BootRoot.cs` | 玩家建立后 `BindMotorLease(_player.MotorLease)` |
| `Sim/EmergencyProcedures/ModalPhaseEvaluator.cs` | **两个 bool 载体退役**(`R-11.5`) |
| `Tests/EditMode/EmergencyProcedures/modal_phase_test.cs` | 删 4 条返回值断言(3 条 `ac1013` + 1 条 `ac1018`) |
| `Tests/EditMode/EmergencyProcedures/emergency_motor_lease_wiring_test.cs` | **新**,10 测(L1 租约位) |
| `Tests/PlayMode/EmergencyProcedures/emergency_motor_suppression_wiring_test.cs` | **新**,2 测(L2 真机走不动) |
| `production/epics/player-controller/EPIC.md` | 跨 epic 登记(租约与读面首次接上生产) |

---

## 二、AC 逐条记分

| AC | 判定 | 载体 |
|---|---|---|
| **AC-10-13** 置位 / 清除 | ✅ **已证** | L1 `test_s11_01/02/03_*`(真 `MotorLease` 位)+ L2 `test_s11_07_*`(真机位移) |
| **S11-01** 开始置位(含负例) | ✅ | `test_s11_01_arm_acquiresEmergencyBit` / `test_s11_01_armOnArmed_throws_andDoesNotAcquire` |
| **S11-02** 三出口清位 | ✅ | `withSamples` / `zeroSamples` / `skipAction` |
| **S11-03** 两中止路清位 | ✅ | `abortAction` / `tickWatchdogAbort` |
| **S11-04** 只碰自己那一位 | ✅ | `test_s11_04_onlyTouchesEmergencyBit_neverOthers` |
| **S11-05** 唯一构造点 | ✅ | `test_s11_05_motorLease_hasExactlyOneProductionConstruction` |
| **S11-06** 后置绑定发生 | ✅ | PlayMode `test_s11_06_productionBoot_*` |
| **S11-07** 玩家真的走不动 | ✅ | PlayMode `test_s11_07_*` |
| **S11-08** 写面收敛 + 白名单落位 | ✅ | `test_s11_08_writeSurface_isConfinedToAllowedAssembly` |

---

## 三、证伪轮(7 条 · 改→跑→还原同命令内完成 · SHA-256 逐字节还原 7/7)

| 变异 | 打什么 | 实测 | 形态 |
|---|---|---|---|
| **M1** | 删 `EndAction` 的 `ReleaseMotorSuppression()` | **4 红** —— 两条 S11-02 + `S11-01`(其前置靠 EndAction 清位)+ `S11-08`(源面数三出口调用数) | **超集**,如实记 |
| **M2** | 删 `AbortAction` 的 Release | **4 红** —— 两条 S11-03 + `S11-04` + `S11-08` | **超集** |
| **M3** | 删 `SkipAction` 的 Release | **2 红** —— `S11-02_skipAction` + `S11-08` | **超集** |
| **M4** | `Acquire` 提到 `_channel.Arm` **之前** | **1 红**:`test_s11_01_armOnArmed_throws_andDoesNotAcquire` | exact(本卡最危险那条) |
| **M5** | 写面 `Release` 打成 `LeaseSource.Self` | **7 红** —— S11-04 + 全部清位路径 | **超集**,靶点在集内 |
| **M6** | 删 `BootRoot` 的 `BindMotorLease` | **2 红** —— S11-06 + S11-07 | **超集**(不绑定 ⇒ 连压制都不存在) |
| **M7** | 撤掉 `PlayerController.Move` 的 `IsSuppressed` 读 | **1 红**:`test_s11_07_*`(`S11-06` 仍绿) | exact(**读面的唯一守卫**) |

**M1/M2/M3/M5/M6 是超集,不是 exact** —— 两处耦合已查清,如实登记:
- `S11-08` 断「三条出口的调用数 ≥ 3」⇒ 任何删一条出口调用的变异都会把它带上;
- `S11-01` 的**前置**依赖 `EndAction` 清位 ⇒ M1 顺带打红它。
两者都**不是假绿**(它们确实对各自的属性有判别力),但**不得**把它们记成「单属性判别」。

**M7 是本轮的归因链核心**:读面撤掉 ⇒ 租约置位、绑定、L1 全绿,**只有「玩家走不动」那一条红** ——
与 story-010 的 M7(删 `CameraRig.Tick`)同构,证明「接线在」与「接线上有行为」是两件事。

还原:`cp` 回备份 + `sha256sum` 逐字节比对(7/7 一致);
`grep -rn "FALSIFY-" unity/Assets --include=*.cs` ⇒ **0**。

---

## 四、本轮被机械门抓到的两件事(如实登记)

### ① `AC-1-23` 的门把我判红了 —— 处置是搬位置,不是扩白名单

初版把 `MotorLease.Acquire / Release` 直接写在 `Gameplay.Boot/EmergencyChannelDriver.cs` 里。
全量 EditMode 首轮 **1 红**:

```
MotorLeaseTest.test_ac123_callSitesSubsetOfWhitelist
  lease 调用者不在白名单 {4, 10, 25} 内(AC-1-23):
  Gameplay.Boot:EmergencyChannelDriver.AcquireMotorSuppression
  Gameplay.Boot:EmergencyChannelDriver.ReleaseMotorSuppression
```

那把门对**调用点所在程序集**做白名单,而 `Gameplay.Boot` 不在其列
(白名单的注释原文是「`Gameplay.Presentation` —— 4 交互 / 10 急救 / 25 战斗的宿主程序集」)。

**处置**:新增 `Gameplay.Presentation/Player/EmergencyMotorSuppression.cs` 作为 10 的写面,
驱动改为经它调用。**没有**把 `Gameplay.Boot` 加进白名单 —— 装配层什么都装得下,
一旦进白名单,门就失去「只有那三个系统能压」这句判据。

> ⚠️ **这是本轮最有价值的一条**:门红的位置信息(哪个程序集)直接指出了正确的架构落位。
> 若当时图省事扩白名单,这条门就变成了一张废纸。

### ② 立件稿在 R-11.4 的插入点上自相矛盾

卡面原文先问「放在 `ValidateMoveInput` **之前**还是之后」,答「**之前**」,
但给的理由是「它校验的是**外部输入**的合法性,不该因为是压制路径就跳过校验」——
理由要求校验**原输入**,结论却是**先清零再校验**(等于把校验废掉)。
**实现按理由走(之后),并把卡面就地订正**,原文保留。

---

## 五、回归(三元组)

| 套件 | 本批 | 基线(story-010 收口) | 等式 |
|---|---|---|---|
| EditMode | `6500 / 6463 / 0 / 0 / 37` | `6492 / 6455 / 0 / 0 / 37` | `6500 − 6492 = 8 = 12 新增 − 4 删除` ✅ |
| PlayMode | `279 / 275 / 0 / 2 / 2` | `277 / 273 / 0 / 2 / 2` | `279 − 277 = 2` ✅ |

`@ 2026-11-02 03:1x–03:4x CST` · 批次 `story-011 实现轮` · commit `11e29dbe` + 工作树。
PlayMode `exit=2` 伴 `failed=0`,系既有 2 条 `inconclusive`(与基线同形,非新失败)。

**复算**:

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
python3 -c "
import xml.etree.ElementTree as ET
for f in ['unity/Logs/s11-em-full.xml','unity/Logs/s11-pm-full.xml']:
    r=ET.parse(f).getroot()
    print(f, {k:r.get(k) for k in ('total','passed','failed','skipped','inconclusive')})
"
```

---

## 六、sprint 归属

**`S23-8`**(2026-11-02 用户指令「排进 sprint-23」)。

`S23-8` 与 `S23-7` 同属 **sprint-23 的收口后追加件** —— sprint-23 的件集(`S23-1/2/3/5/6`)已于
2026-11-01 全部 `done` 且 §七 收口判定已出;**本节与这两件都不进 §七 的判据面**,
读数各记在 `sprint-23.md` §八的专属段落里。sprint-24 排的是 `processing-002/004/005 + foraging-002`,与本卡无交集。

---

## 七、证明不了的事(禁借绿)

1. **4(`LeaseSource.Self`)与 25(`Combat`)仍零生产接线** —— `ModalGate.SetMotorSuppression`
   生产调用点仍是 **0**。本卡只接 10 那一位。「三位互不知晓、OR 聚合」的**多写者**场景
   只在 L1 夹具里用合成位验证过,生产上仍只有一个写者。
2. **`LocomotionEvaluator` 的乘数链仍只是注释** —— `Move` 直接算 `SpeedWalk × |moveInput|`,
   没有 `K_terrain` / `K_context`。本卡只碰了 `K_suppressed`。
3. **真机观感未走查** —— 「按 F 期间玩家确实推不动」是逻辑与物理层面的断言,
   没有人工确认手感(尤其在**斜坡 / 空中**等 `CharacterController` 边界状态下的表现)。
