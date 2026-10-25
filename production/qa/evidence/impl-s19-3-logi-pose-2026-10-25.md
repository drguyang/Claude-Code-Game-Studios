# S19-3 实现轮证据原件 —— `LogiPose` + `ITameable`/`TameState` 形状 + 逐 tick 整数推进

**日期**:2026-10-25 · **执行**:AutoClaw 会话(`181888b1`)
**对象**:sprint-19 §S19-3(G-6 · `TD-enemy-01` 兑现)· 测试载体 `unity/Assets/Tests/EditMode/EnemyAI/logi_pose_test.cs`(新,15 条)
**落点裁定(判据 ①,实施前 2026-10-25)**:`LogiPose` / `ITameable` / `TameState` / `TameableFields` 落 **`Sim.Contracts`**
—— sprint-status note 的默认指向(「按 ADR-025 §① 应住 `Sim.Contracts`;若裁定落 `Sim`,须给出理由」),无偏离理由:
它是 13(表现层,引用集不含 `Sim`)与 27 共享的**跨装配契约数据**(13 的投影链输入,
`PresentationProjection.cs` 头注已把 `LogiPose` 列为投影输入);ADR-025 §① 契约类型住 `Sim.Contracts`。

---

## 一、改动清单(Landed)

| 文件 | 动作 | 内容 |
|---|---|---|
| `unity/Assets/Sim.Contracts/LogiPose.cs` | 新 | `LogiPose` readonly struct(`WorldPos Cell` + `byte Facing`;0..5 = +X/−X/+Y/−Y/+Z/−Z,与 `EnemyPathing` 邻接序一致)· `TameState` 枚举(4 值:Wild/Taming/Tamed/Owned,0=Wild)· `ITameable` 接口(`Tame` + `OwnerId` 只读,无 set)· `TameableFields` 占位(readonly struct,恒返回 Wild/None —— **零值陷阱结构性免疫**:`PatientId.None = -1` 而默认值 0 非 None,可变字段会静默得到非法归属 id,故 P0 不变式由类型结构保证,不靠纪律) |
| `unity/Assets/Sim/EnemyLocomotion.cs` | 新 | GDD F-27-4 **权威形态**推进算子:`Advance(in LogiPose, ref Fix acc, path, ref cursor, moving, speed)` —— **while 循环沿路径逐格**(非 `StepAccumulator` 直线逼近)+ **冻结分支 acc 保持不累加不归零**(F-27-3 同型穿墙错误的守卫)+ Facing = `DirFrom(cell, Path[min(cursor+1,…)])` 语义;**纯函数**(零 IEventSink / SimEvent 引用) |
| `.meta` ×2 | 新 | guid 各自独立 |

**不动面**:`EnemyPathing.StepAccumulator` 原样(直线逼近是「朝目标走」的简化语义,与沿路径推进并存;合并与否归后续收敛轮,同 TD-player-03 型)· `EnemyStateMachine` / `EnemyEncounter` 原样 · 无 `Kind` 增删(推进是派生态,**不进流** —— ADR-016 §一,零 registry 改动)。

## 二、测试面(15 条新增,全绿)

| 测试 | 钉什么 |
|---|---|
| `test_logipose_landsInSimContracts` / `test_tameable_landsInSimContracts` | 判据 ①:装配名 = Sim.Contracts + 值类型/接口/占位形状 |
| `test_locomotion_noTransformOrVector3_sources` | 判据 ② 源码判:`EnemyLocomotion` + `LogiPose` 的**代码**(剥注释/字符串后)零 Transform/Vector3/UnityEngine |
| `test_logipose_noFloatFields` | 判据 ② 反射判:LogiPose 恰两整数域字段,零 float/double/decimal |
| `test_tameable_p0FieldsArePlaceholder` | 判据 ③:P0 占位恒 Wild/None(AC-27-33 值面) |
| `test_tameable_noMutableTamePaths` | 判据 ③ 反射面:接口无 set accessor · 占位零实例字段/零方法 · 枚举闭集 4 值 |
| `test_advance_oneCellPerOverflow_alongPath` / `test_advance_fractionalAccumulation_carriesRemainder` | F-27-4:溢出沿路径恰一格;定点余数跨 tick 存续 |
| `test_advance_frozenKeepsAcc_notZeroing` | F-27-4 冻结分支:acc 保持(不累加**不归零**),解冻首 tick 正常消费零瞬移 |
| `test_advance_pathExhausted_staysAndClampsAcc` | 路径耗尽:原地 + acc 收口([0,1) 不变式) |
| `test_advance_facingTracksNextPathCell` / `test_facingBetween_sixDirectionsMatchAdjacencyOrder` | DirFrom 语义:朝向 = 路径下一格方向;终点保持;六向映射 |
| `test_advance_pureFunction_noStreamWrites` | 判据 ④ 前半:推进全程零 Append(夹具 sink 计数恒 0) |
| `test_crossCellEffect_goesToStream_notDecision` | 判据 ④ 后半:跨格效果以 `ActorCellEntered` 显式写世界流(编码器真入池 + 解码往返 + `StreamRouting` 路由断言) |
| `test_locomotion_routeDecisionAndEffectSeparation_mechanical` | 判据 ④ 机械面:剥注释后零 IEventSink/Append/SimEvent 字样(连依赖都不存在) |

**⚠️ 夹具修正两轮(如实登记)**:第一版源码判夹具**未剥注释** ⇒ 被 `EnemyLocomotion.cs` 头注的自我声明
(「零 `Transform` / `IEventSink`」字样)误伤 2 红 —— 词面在注释 ≠ 代码在引用。
修 = `StripCommentsAndStrings`(线性状态机剥 `//`、`/* */`、字符串/字符字面量)后再扫。

## 三、回归数字(三元组)

- **全量 EditMode**:`4846 / 4811 / 0 / 35` @ `2026-10-25 01:42 CST` · 批次 `s193`(全量)· commit `6198671`
  (前基线 `4831/4796/0/35` @ `2026-10-24 23:54` · `s192` · `6198671` ⇒ **total +15 = 本轮新测数,零失败零回归**;`4831+15=4846` 可复算)
  - 全名级差集:新增 15(全部 LogiPoseTest)· 消失 0 · 翻转 0 · 红 0
  - 复算:`grep -o 'total="[0-9]*" passed="[0-9]*" failed="[0-9]*" inconclusive="[0-9]*" skipped="[0-9]*"' /home/gu/.claude/jobs/181888b1/tmp/s193-full.xml | head -1`
- **夹具终验**(证伪轮后):`15/15/0/0` @ `unity/Logs/s193-final.xml`
- **PlayMode**:未触(纯 sim/contracts 逻辑测)—— 如实登记 NOT-RUN
- ⚠️ **跑法旗标差异(环境红 4 条的根因,与代码无关)**:先跑带了 `-runSynchronously` ⇒ `MovementFeedTest` ×3 + `PlayerMotorTest.test_tick_delegatesToMove` ×1 红(dt 趋零 ⇒ 位移前提崩);换**昨天同款跑法**(不带该旗标)复跑同夹具 `29/29/0/0` 全绿、全量零红。昨天基线 XML(`s192-full.xml`)里该 4 条均为 Passed。**教训登记**:`-runSynchronously` 改变 EditMode 下 `Time.deltaTime` 的帧计时行为,凡涉物理位移前提的夹具**不得**带该旗标跑(与「同款跑法」基线纪律)。

## 四、证伪轮 M1–M8(S12-4 形态;变异 → 跑 LogiPoseTest → 还原同命令内完成)

**纪律**:还原判据 = SHA-256 逐字节(基准 `EnemyLocomotion.cs` `e5cfa843…` / `LogiPose.cs` `dbf40e36…`,备份 `fz-s193/`);锚串唯一性断言。

> 🔁 **2026-10-25 QA 轮加注(承签核 C-5,原文不删)**:上记备份目录 `fz-s193/` **已不在盘上**
> (`ls -d fz-s19*` 实跑只有 `fz-s194`)。⇒ **还原过程不可复演**。
> ⚠️ 但**还原结果今天仍可验**:`sha256sum unity/Assets/Sim/EnemyLocomotion.cs unity/Assets/Sim.Contracts/LogiPose.cs`
> 实跑 = `e5cfa8434609389d…` / `dbf40e36f6d21561f…`,与基准前 8 位逐位一致
> ⇒ 两文件当前内容 = 被变异前的基准内容(此后无改动)。
> **判据降级口径**:本轮的「SHA-256 逐字节还原」由 **PASS** 降为 **PARTIAL** ——
> 结果可验 ✅ / 过程不可复演 ✗。下游引用本件时**不得**把它读作「完整可复现的证伪轮」。

| # | 变异(停用哪个守卫) | 结果 | 转红的测试 |
|---|---|---|---|
| M1 | 冻结分支 acc 归零(插在 return **后**) | 0 红 | —— **无效变异**(不可达代码,编译器忽略;如实登记) |
| M1' | 冻结分支 acc 归零(插在 return **前**) | 1 红 | `test_advance_frozenKeepsAcc_notZeroing` |
| M2 | acc 前进脱离 Moving 门控(无条件累加) | 4 红 | fractional / frozenKeepsAcc / oneCellPerOverflow / crossCellEffect |
| M3 | 朝向判定失效(恒 +X) | 1 红 | `test_facingBetween_sixDirectionsMatchAdjacencyOrder` |
| M4 | OwnerId 返回 0(≠ None;零值陷阱方向)——首版插重复属性定义 = 编译炸(CS0102),改替换式 | 1 红 | `test_tameable_p0FieldsArePlaceholder` |
| M5 | 死分支引入 `UnityEngine.Vector3` | **编译即炸**(CS0103)—— **比夹具更硬的结构门**:`Sim.asmdef` 引用集恰 = `{Sim.Contracts}` + `noEngineReferences: true`,引擎类型在 Sim 装配**编译期不可表达**(ADR-025 §① 的实证) |
| M5'' | LogiPose 渗入 float 字段(含构造器补赋值)——前两版分别因构造器未全赋值(CS0171)编译炸 | 1 红 | `test_logipose_noFloatFields`(反射判) |
| M6 | while 降级 if(大步长丢格)——首版编译炸(CS0139 break 失宿主),改首迭代即 break | 7 红 | 全部 advance 系 + crossCellEffect |
| M7 | TameableFields 渗入驯化方法 `EnterTaming()` | 1 红 | `test_tameable_noMutableTamePaths` |
| M8 | LogiPose.cs 物理移进 Sim 装配(判据 ① 落点守卫) | 2 红 | `test_logipose_landsInSimContracts` + `test_tameable_landsInSimContracts` |

**判别力自证**:8 轮(不含 M1 无效轮)全数转红;两轮编译炸本身就是 ADR-025 §① / readonly struct 不变式的**编译期实证**。
证伪后终验 `15/15` 绿,两文件 SHA-256 与基准逐位一致。

## 五、残余与边界(禁借绿)

- **消费者仍为零**:`LogiPose` 今天只被测试与推进算子引用;27 的行为程序/遭遇生命周期**尚未接线** LogiPose(它们是派生态求值,接线归各自 story);13 的投影链消费随 patient-ai 后续轮。S19-3 只落形状 + 推进算子,不发明消费者。
- **`EnemyPathing.StepAccumulator` 与 `EnemyLocomotion.Advance` 双实现并存** —— 直线逼近 vs 沿路径,语义不同;合并/删除归后续收敛轮(与 TD-player-03 同型;**未入债册**,若用户裁「入」再立条)。
- **M1 的 0 红**是**变异打错靶**(不可达代码),非夹具假绿 —— M1' 修正后同守卫 1 红已证判别力。
- **联机 / LOD / 冻结全链**:冻结判据(`d2` / `in_combat`)与 Moving(e) 的完整六态判定归 27 的行为程序接线轮;本轮只保证算子对 `moving=false` 的冻结语义正确。

## 六、复算命令

```bash
# 全量回归
grep -o 'total="[0-9]*" passed="[0-9]*" failed="[0-9]*" inconclusive="[0-9]*" skipped="[0-9]*"' \
  /home/gu/.claude/jobs/181888b1/tmp/s193-full.xml | head -1
# 差集(对照 s192-full)
python3 - <<'EOF'
import xml.etree.ElementTree as ET
old = {tc.get('fullname') for tc in ET.parse('/home/gu/.claude/jobs/181888b1/tmp/s192-full.xml').iter('test-case')}
new = {tc.get('fullname') for tc in ET.parse('/home/gu/.claude/jobs/181888b1/tmp/s193-full.xml').iter('test-case')}
print(len(new - old), len(old - new))   # 期望 15 0
EOF
```
