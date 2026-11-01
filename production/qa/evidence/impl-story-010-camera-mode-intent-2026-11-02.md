# 实现原件 —— emergency-procedures/story-010 档位意图生产接线

> **对象**:`production/epics/emergency-procedures/story-010-camera-mode-intent-wiring.md`
> **批次**:sprint-23 `S23-7`(收口后追加件 · 用户 2026-11-02 指令「排进 sprint-23」)
> **截至时刻**:`2026-11-02 01:08 CST` · **commit**:`00637dc9` + 本批未提交工作树
> **裁定**:用户 2026-11-02「甲+乙,按建议走」(R-10.7);同日另裁「本卡顺带接 Tick」

---

## 一、交付物

| 文件 | 变更 |
|---|---|
| `unity/Assets/Gameplay.Boot/EmergencyChannelDriver.cs` | `RequesterId = 10` 常量 · `BindCameraRig(Func<ICameraRig>)` / `HasCameraRig` · `RequestCameraMode` · 三出口发意图(`Arm`→Treatment / `EndAction`→Explore / `AbortAction`→Explore)· **新方法 `SkipAction`**(先 Explore 再委派 TryManual) |
| `unity/Assets/Gameplay.Boot/CasebookCommandRouter.cs` | `BindEmergencySkip` 宿主 `EmergencyManualDriver` → `EmergencyChannelDriver`;`SubmitEmergencySkip` 改调 `SkipAction`(两条守卫原样保留) |
| `unity/Assets/Gameplay.Boot/BootRoot.cs` | ① `4.65` 换跳过宿主;② `5.0b` 新增 `BindCameraRig(() => _cameraRig)`;③ **`Update` 增 `_cameraRig.Tick(Time.deltaTime)`** |
| `unity/Assets/Sim/EmergencyProcedures/ModalPhaseEvaluator.cs` | 计数载体字段删除 + 4 处构造实参收缩(注释刻意不写旧字段名,以保 S10-05 的 grep 判据) |
| `unity/Assets/Tests/EditMode/EmergencyProcedures/modal_phase_test.cs` | 删 4 条计数断言,原位留说明指向新载体 |
| `unity/Assets/Tests/EditMode/EmergencyProcedures/emergency_skip_entry_wiring_test.cs` | 4 处 `BindEmergencySkip` 宿主改为 `_bag.EmergencyChannel` |
| `unity/Assets/Tests/EditMode/EmergencyProcedures/emergency_camera_mode_intent_test.cs` | **新**,15 测(L1 意图层) |
| `unity/Assets/Tests/PlayMode/EmergencyProcedures/emergency_camera_mode_intent_wiring_test.cs` | **新**,2 测(L2 真机层) |
| `design/gdd/emergency-procedures.md` | 规则八 + `AC-10-11` 各一条带日期注(乙案:不可达性如实登记 + AC 拆半记分) |
| `production/epics/camera-viewpoint/EPIC.md` / `death-respawn/EPIC.md` | 跨 epic 登记:Tick 接线激活 `Casebook` / `Downed` 的真实效果 |
| `production/qa/evidence/s24-camera-downed-two-phase-2026-11-01.md` | §15 补记:本轮追查出「夹具驱动 ≠ 生产驱动」 |

---

## 二、AC 逐条记分(**禁把「勾上了」读成「全绿」**)

| AC | 判定 | 载体 / 依据 |
|---|---|---|
| **AC-10-11**「动作结束必发 `Explore`」半 | ✅ **已证** | L1 `test_s10_02_*` / `test_s10_03_*` + L2 `test_s10_06_*` |
| **AC-10-11**「跳过路径必发 `Explore`」半 | 🟡 **请求在位,但不可失败 ⇒ `NOT-RUN`** | 甲案落 `SkipAction`(L1 `test_s10_10_*` 绿);乙案登记见 GDD 规则八 + `AC-10-11` 注。**依裁定不得记 ✅** |
| **S10-01** 开始 ⇒ Treatment | ✅ | `test_s10_01_arm_*`(含「Arm 抛出那一轮零请求」) |
| **S10-02** 结束两出口 ⇒ Explore | ✅ | `test_s10_02_endAction_withSamples` / `_zeroSamples` |
| **S10-03** 中止两路 ⇒ Explore | ✅ | `test_s10_03_abortAction` / `_tickWatchdogAbort` |
| **S10-04** Idle 误触 ⇒ 零请求 | ✅ | `test_s10_04_endActionWhenIdle` / `_abortWhenIdle` |
| **S10-05** 计数载体退役 | ✅ | `test_s10_05_modalPhaseResult_intMembers_areExactlyWhitelisted`(集合相等,不点名旧字段)+ `grep` 零命中 |
| **S10-06** 真机档位 | ✅ | PlayMode `test_s10_06_armMovesCameraToTreatment_endReturnsToExplore` |
| **S10-06b** 后置绑定发生 | ✅ | PlayMode `test_s10_06b_productionBoot_bindsCameraRigToEmergencyDriver` |
| **S10-07** 机械门零违例 | ✅ | `test_s10_07_cameraSetModeGate_zeroViolationOnProductionTree`(真树 `RunAll(".")` ⇒ 空) |
| **S10-08** 单一常量 | ✅ | `RequesterId` 常量 + `test_s10_01` 里的 `Assert.AreEqual(10, EmergencyChannelDriver.RequesterId)` |
| **S10-09** 真机走查 `[L]` | ⛔ **`NOT-RUN`** | 需图形界面;`-batchmode -nographics` 结构性不可判。**不借绿** |

---

## 三、证伪轮(7 条变异 · 改→跑→还原同命令内完成 · SHA-256 逐字节还原)

| 变异 | 打什么 | 实测 | 形态 |
|---|---|---|---|
| **M1** | 删 `EndAction` 的 `Explore` 请求 | 2 红(`test_s10_02_*` 两条) | exact |
| **M2** | 删 `AbortAction` 的 `Explore` 请求 | 2 红(`test_s10_03_*` 两条) | exact |
| **M3** | `rig.SetMode(mode, RequesterId)` → `-1` | 8 红(含 `test_s10_07_*` —— **门真的抓到了**) | 超集,如实记 |
| **M4** | `Explore` 改成以 `agg != null` 为条件 | **1 红**:`test_s10_02_endAction_zeroSamples_stillRequestsExplore` | exact(本卡核心判别力) |
| **M5** | 删 `BootRoot` 的 `BindCameraRig(...)` 调用 | 2 红 —— ⚠️ **卡面原写「只有 S10-06b 红」,实测 S10-06 也红** | **超集,卡面预期订正** |
| **M6** | 删 `SkipAction` 的 `Explore` 请求 | 1 红(`test_s10_10_skipAction_requestsExploreWithRequesterTen`) | exact |
| **M7** | 把 `Update` 的 `_cameraRig.Tick(...)` 换成注释 | **1 红**:`test_s10_06_*`(`test_s10_06b` 仍绿) | exact(归因干净) |

**M5 的如实订正**:卡面原写「S10-06b(且**只有它** —— 这正是「静默失败」的判别力证明)」。
实测**两条都红** —— 因为不绑定就连请求都发不出去,L2 自然也动不了相机。
**「只有它」的那半是不成立的**,但 **M7 补上了**真正的「只有一条红」的归因:
Tick 掉了 → 绑定仍在(`S10-06b` 绿)、请求仍发(L1 绿)、**但相机不动**(`S10-06` 红)。

还原:`cp` 回备份 + `sha256sum` 逐字节比对(7/7 一致);`grep -rn "FALSIFY-" unity/Assets --include=*.cs` ⇒ **0**。

---

## 四、回归(三元组)

| 套件 | 本批 | 基线 | 等式 |
|---|---|---|---|
| EditMode | `6492 / 6455 / 0 / 0 / 37` | `6481 / 6444 / 0 / 0 / 37` | `6492 − 6481 = 11 = 15 新增 − 4 删除` ✅ |
| PlayMode | `277 / 273 / 0 / 2 / 2` | `275 / 271 / 0 / 2 / 2` | `277 − 275 = 2`(两条 L2)✅ |

`@ 2026-11-02 00:5x–01:0x CST` · 批次 `S23-7` · commit `00637dc9` + 工作树。
PlayMode `exit=2` 伴 `failed=0`,系既有 2 条 `inconclusive`(与基线同形,非新失败)。

**复算**:

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
python3 - <<'PY'
import xml.etree.ElementTree as ET
for f in ["unity/Logs/s10-em-full.xml","unity/Logs/s10-pm-full.xml"]:
    r=ET.parse(f).getroot()
    print(f, {k:r.get(k) for k in ("total","passed","failed","skipped","inconclusive")})
PY
```

---

## 五、本轮查出并处置的两处结构性事实

### ① 跳过路径不在相机链上(R-10.7 · 用户已裁甲+乙)

`CasebookCommandRouter.SubmitEmergencySkip` 直连 `EmergencyManualDriver.TryManual(method:1)`,
而 `R-9.3` 的 Idle 门让候选列表只在 `Idle` 可见 ⇒ 该路径上的 `Explore` **不可失败**。
**处置**:甲 = 新增 `EmergencyChannelDriver.SkipAction` 把跳过拉回相机链;乙 = GDD 与卡面如实登记,
`AC-10-11` 拆两半分开记分。

### ② `CameraRig.Tick` 零生产调用(用户裁定「本卡顺带接」)

`CameraModeMachine.Settle` 从不执行 ⇒ 档位永远锁死 `Explore`。影响 29(`Downed`)、10(`Treatment`)、
39(将来的 `Casebook`)。**处置**:`BootRoot.Update` 增一行 + 日期注 + 跨 epic 登记(camera-viewpoint /
death-respawn 两个 EPIC 各一节)+ 证据件 §15。

⚠️ **它是「夹具驱动 ≠ 生产驱动」的一次实样** —— 2026-11-01 那轮的 PlayMode 走查必须手工调
`_rig.Tick()` 才看得到效果,**当时没有问「生产里谁调它」**。这不是注释冒充门(F6),
是判据越权的另一形态,建议下一版假绿登记表补条。

---

## 六、证明不了的事(禁借绿)

1. **`S10-09` 真机走查看不到** —— batch 无图形界面。收到近景 / 松开回探索的**观感**未验证。
2. **`Downed` 与 `Casebook` 的真机效果** —— 接线后它们**第一次**在生产里生效,本轮只证明了
   「状态机会结算」这一件事;**两段式倒塌的时长/幅度手感、脉案档的转场观感均未走过真机**,
   归 `camera-viewpoint` 与 `death-respawn` 的机位轮。
3. **「跳过路径必发 Explore」不可失败** —— 按裁定永久 `NOT-RUN`;L1 的绿灯只证「请求在位」。
4. **`MotorLease` / `AC-10-13`** —— 本卡 Out of Scope 已登记(全仓零生产实例化),须另立 story。
