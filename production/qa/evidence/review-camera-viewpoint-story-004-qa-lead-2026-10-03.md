# QA Lead 评审 — camera-viewpoint Story 004(出臂与收缩)

- **日期**: 2026-10-03 · **评审人**: qa-lead
- **对象**: `unity/Assets/Gameplay.Presentation/Camera/CameraArmSolver.cs`
- **测试**: `unity/Assets/Tests/EditMode/CameraViewpoint/camera_arm_solver_test.cs`
- **故事**: `production/epics/camera-viewpoint/story-004-arm-geometry-and-retraction.md`
- **Story Type**: Integration(证据要求:Integration **BLOCKING** + Logic + 装载门)
- **实测**(Unity 6.3.24f1 batchmode,`/tmp/story004-arm.xml`):
  `total=16 · passed=14 · failed=0 · skipped=2`(2 例 = `Assert.Ignore` NOT-RUN)

## 结论:🟥 **不应维持 Complete**(Reopen)

四条硬伤:① AC-2-15① 是**自证假绿**(且生产默认值恰为该 AC 要防的违例值);② AC-2-15② 装载期断言**未接线**;
③ `ê_view` 全链**零覆盖**(Integration 故事的核心失效模式「斜视角贴墙却不收缩」无人守);
④ 故事号称 17 例,实测 **16 例**;Completion Notes 四栏 `_待填_`、Test Evidence `[ ] Pending` 而 Status=Complete。
⑤ `d_raw` 保守语义在开放世界**默认态坍缩**,非注释所称「极罕见」——须另裁(见 §5)。

---

## 1. AC-2-14 收缩瞬时 / 回弹上界 / 非对称序

| 子项 | 判定 | 依据 |
|---|---|---|
| ① 收缩瞬时(同帧 == `d_block`) | ✅ **真判据** | `test_ac214a_retractionIsInstantaneous_sameFrame`:`4 → clamp(1.0−0.3)=0.7`,容差 `1e-5`。「收缩也走 lerp」形态(`k≈0.3`)首帧得 ≈3.01 ≠ 0.7 ⇒ **会被抓**。 |
| ① 不受 `RECOVER_SPEED` 影响 | ✅ **真判据** | 双值 0.5 vs 50,若收缩误走阻尼:0.0083 vs 0.833 ⇒ 首帧 d 不同 ⇒ **会被抓**。非「夹具两值同」。 |
| ② 回弹上界 | ⚠️ **弱** | 只测**单帧** `delta ≤ 4×dt+1e-6`(0.0667)。AC 原文「回弹**段每帧**」;故事 QA 列的边缘(**越界钳上界** / `dt=0` / 交替帧)全部未测。 |
| ③ 非对称序 | ✅ **真判据** | 同夹具 收缩 Δ=1.3 > 回弹 Δ=0.0667,`Greater` 真实成立。 |

数字复核(python 重算)与 4 例断言全部一致。

## 2. AC-2-15 掩码相等 / 近裁剪链 / Trigger

- **① 掩码相等 —— 🔴 假绿(自证)**:`test_ac215a` 的 `p` 由本文件工厂 `P()` 产出,工厂写 `CamCollideMask = 0b1010`;
  断言期望 `ExpectedWhitelist = 0b1010` **也是本文件字面量**;`CharacterLayerMask = 0b0101` 同理。
  ⇒ 断言的是「测试自己的字面量 == 测试自己的字面量」,**从未读故事要求的「同一登记处」(项目层设置/ADR-015)**
  ——故事 Implementation Notes 明写「不写第二个常量」。**负夹具(含玩家位 / 空掩码)缺**,因为生产侧根本**没有掩码校验函数**可投坏值。
  **更重**:生产 `CameraArmParams.CamCollideMask` 默认 `0`(第 47 行),全库**无任何非测试写入点**(grep 证实)
  ⇒ **出厂掩码 = 0 = 空掩码 = 相机永不收缩穿墙** —— 恰是本 AC 存在的理由。测试注入 0b1010 后转绿,真表未接。
- **② 近裁剪链 —— ⚠️ 未接线**:`ValidateNearClipChain()` 的两例(合法不抛 / 违反抛)**断言本体真实**,
  但全库**无任何非测试调用**(grep 证实)⇒ 故事要求的「**装载期**断言、违反则拒绝启动」**不存在**。测试只测了一个孤儿方法。
  错误串「点名三值」只 `Contains("NEAR_CLIP")`,边界 `==`(恰等式)未测。
- **③ `QueryTriggerInteraction` —— ⚠️ `Assert.Ignore`(实跑确认 skipped)**:不借绿的声明**属实**。
  且即便真 `SphereCast` 接线,判据形态是**整目录字符串 `Contains`**(非「挂在该调用上」)⇒ 任一无关文件出现该串即假绿;**弱谓词**。
  `FakeQuery.LastTrigger` 字段声明后**从未被断言**(死夹具)。

## 3. AC-2-16 无跨帧遮挡状态

- `test_ac216_geometryAppears_nextFrameRetracts`:过往 `4 → 0.5` 单帧收敛,**真判据**。
- `test_ac216_noCrossFrameOcclusionState`:反射字段名黑名单 {blocked,hittest,wasblock,dtarget,occlu} + 源码串扫。
  **能抓**故事点名的 `_wasBlocked`,但 `_lastCast` / `_prevHit` / `_cache` 等可绕过 —— **只查存在**型弱谓词。
- ⚠️ **语义漂移**:为迁就 §5 的保守语义,「先无遮挡」被表达为 `Hit=true, Distance=10`(而非 `Hit=false`)
  ⇒ 测试**绕开了真 `hit=false` 路径**;故事 QA 点名的「撤墙后 d→ARM_LEN」旧缺陷夹具**未落**。

## 4. AC-2-25 常量几何 / 档位闭集 / 序 / 正臂长

- **① 肩位**:`test_ac225a_shoulderIsConstantGeometry` 期望式 = 实现式(重言式),但**若残留原式 `f̂×CAM_RADIUS×L` 会红**
  ⇒ 对订正①有效;**②** `test_ac225a_shoulderUnaffectedByCamRadius`(改半径肩位不动)对订正①**因果有效**(原式下必红)。两例 ✅。
- **② 无第二处档位分支**:源码扫描 `Contains("switch (") && Contains("Mode")` + `"if (_mode"|"if (mode =="`。
  弱:`switch(mode)`(无空格)、`Mode is CameraMode.Treatment` 型匹配、`this.Mode==` 均可绕过;且**七项参数「全部来自表」从未被验**。⚠️ 弱。
- **③ 序关系**:`Assert.Ignore` + 前面是**两条字面量比较**(2<4)=(纯注入)—— 实跑确认 skipped。✅ 声明属实,但**零信息量**。
- **④ `ARM_LEN > 0`**:`Greater(p.ArmLen,0)`,`p` 为测试默认 4f —— **对测试自己的字面量自证**;生产**无** `ArmLen>0` 装载守卫(grep 证实)。🔴 同型假绿。

## 5. EC-2-1 起点重叠回退 与 `dRaw` 保守语义 —— ⚠️ 须另裁(评估:处置**不可接受**)

- `test_ec21` 断言 `Hit=false ⇒ d=CAM_MIN_DIST`,**真判据**(能抓「false ⇒ ARM_LEN」实现)。
- 🔴 **但该处置有实害**:GDD `F-2-4`(line 473)主规则是 **未命中 ⇒ `d_raw := ARM_LEN`**;line 488 才是起点重叠回退。
  实现把**两种不可分情形一律**回退 `CAM_MIN_DIST`(0.5)⇒ **开放世界无 4 m 内几何时,臂长每帧坍缩到 `CAM_MIN_DIST`,
  越肩第三人称退化为贴脸**。代码注释称该情形「极罕见」**为假** —— 平原望向地平线正是**默认态**。
  这是**主路径回归**,不是安全退化;且测试用「`Hit=true` 远距」伪装未命中 ⇒ **该回归无一条测试能发现**。
- **遗漏**:正确解要么 `CheckSphere` 预判(代价 = 多一次查询,**与 story 005「每帧恰一次」冲突 ⇒ 须架构裁定**),
  要么改裁「未命中 ⇒ `ARM_LEN`」并把重叠交给肩位外推/几何约束。**本批「取保守值」不能自行定案**,故事自己也标了「须另裁」——
  低残留的风险极高,须先裁后实现。`SphereCast` 真契约(overlap 是 `false,0` 还是 `true,0`)仍是 **BLOCKED-BY-spike 未跑**。

## 6. 失效模式九型扫描

| 型 | 命中处 |
|---|---|
| ① 容差吞抓错力 | 未见(`1e-5/1e-6` 恰当) |
| ② 夹具两值同 / ⑤ 断言常量工厂 | **AC-2-15①**、**AC-2-25④**、AC-2-25③(纯注入) |
| ③ 直传入参无因果 | 未作为结构问题出现(签名层面无动态输入) |
| ④ 只查存在 | AC-2-15③ 目录 `Contains`、AC-2-25② 串扫、AC-2-16 字段名黑名单 |
| ⑤ NOT-RUN 桩硬编码 true | 两处 `Assert.Ignore` **声明属实**(实测 skipped),无借绿措辞 |
| ⑥ 空集真空真 | AC-2-15①「不含角色层」对生产**空掩码**恒真的旁支已由 `AreNotEqual(0)` 挡住 —— 但仅对注入值 |
| ⑦ 桩生产码孤儿求值器 | **命中**:`ArmLen/ShoulderLateral…` 生产表**从未装载**;`ValidateNearClipChain`、`ViewDir` 皆**生产零调用**(孤儿) |

## 7. 结论与建议(移交 producer / lead-programmer)

**判定:不应维持 Complete。** 处理建议(按优先):

1. **P0 · AC-2-15① 重做**:掩码期望值必须从**登记处**(项目层设置 / ADR-015 层表)同源计算,并补**两个真负夹具**(含玩家位 / 空掩码)。
   同时**必须把生产 `CamCollideMask` 从 `0` 接上装载路径**(否则出厂即穿墙)。当前绿 = 假绿。
2. **P0 · `dRaw` 保守语义另裁**:开放世界默认态坍缩是主路径回归;标注「极罕见」不成立。裁前该 AC 记 **BLOCKED**,
   并补一条**真 `hit=false`** 夹具(现在被 `Hit=true,10f` 掩盖)。`SphereCast` 真契约 spike 仍未跑。
3. **P0 · `ê_view`/`ViewDir` 补测**:故事 Implementation Notes 点名的「yaw/pitch 四角 + 视线穿体 GT 对拍」「单位向量」**零测试**;
   `ViewDir` 生产零消费者(孤儿)。Integration 故事缺此半边不可签。
4. **P1 · Integration BLOCKING 证据缺**:故事声明的 `tests/integration/camera/arm_retract_test.cs`、`tests/unit/camera/shoulder_geometry_test.cs` **均不存在**;
   仓内仅 EditMode 单测,无任何 GT 几何对拍。`Test Evidence` 仍 `[ ] Pending`。
5. **P1 · 收口手续**:故事 Status=Complete 但 Completion Notes 四栏 `_待填_`;「17 例」应为 **16 例**(14 passed / 2 skipped)——订正引用。
6. **P2 · 弱谓词加固**:AC-2-25②/AC-2-16 的源码串扫改 AST/反射;AC-2-25④ 的 `ArmLen>0` 从字面量改接真表并补装载守卫;
   AC-2-15② `ValidateNearClipChain` 接上启动路径并补 `==` 边界例。

**证据**:`/tmp/story004-arm.xml`(Unity 6.3.24f1 batchmode 实跑)· 复核脚本见本报告 §1 数字重算。
