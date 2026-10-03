# QA Lead 评审 — camera-viewpoint Story 003 锚跟随与绕点

**Date**: 2026-10-03
**Reviewer**: qa-lead
**Object**: `production/epics/camera-viewpoint/story-003-anchor-follow-orbit.md`
**Implementation**: `unity/Assets/Gameplay.Presentation/Camera/AnchorFollower.cs` · `CameraRig.ApplyOrbit`
**Tests**: `unity/Assets/Tests/EditMode/CameraViewpoint/anchor_follow_orbit_test.cs`
**Story Status at review**: Complete ✅ (4/4 AC;自称「11 例」)

## Unity 实跑证据（本轮亲跑，非转述）

```
unity -batchmode -nographics -runTests -testPlatform EditMode -projectPath unity \
  -testFilter AnchorFollowOrbitTest -logFile unity/Logs/qa-story003-anchor.log \
  -resultFile /tmp/qa-story003-anchor.xml
```
结果：`unity/TestResults-639266569884253560.xml` —— **`total="12" passed="12" failed="0"`**（2026-10-03T12:43:08Z），编译零错误。
⇒ 「11 例」是**计数误**（实为 **12** 例）；测试**确实全绿**。但**全绿 ≠ 判据全真在验**（见下）。

## Verdict

**不应维持 Complete** —— 存在 **1 条 BLOCKING**（AC-2-12②「MAX_DT 同源」判据空转且实现未接源）+ 4 条 ADVISORY（AC-2-13 期望值耦合实现、EC-2-5 前半空转、组 5c 名单式扫描、AC-2-11 抓错力偏窄）。其余 8 例判据经 float32 复算坐实为**真因果**。

## 逐例测试体检(恒真 / 空转 / 抓错力)

| # | 测试 | 判据性质 | 抓错力（复算实测） | 判定 |
|---|---|---|---|---|
| 1 | ac211_noOvershoot_beyondEps | **真不变量**：逐帧 `e_k=dot(P−a,v)` 最小值 ≥ −ε（ε 注入自 `cfg.AnchorOvershootEps`，非硬编码） | 阻尼 0.2w/-2w、弹簧符号翻转 ⇒ worst_e 达 −242/−inf 抓得住；阻尼 1.8w ⇒ e=−2.3e-7 亦绿（容差抓不出） | ✅ 有效（抓错力度有限） |
| 2 | ac211_convergesToTarget | 真收敛断言 | 前向欧拉亦收敛(实测 dist=1.9e-6)——非其职责 | ✅ 有效 |
| 3 | ac211_stabilityHolds_atHugeOmega | 真有界断言(ω=1e4 不 NaN/不 Infinity/|x|<10) | 前向欧拉亦稳(x=1.0)；**文件内注释已自陈「稳定性区分不出两种积分器」** | ✅ 有效（判据已自我设限） |
| 4 | ac211_semiImplicitOrder_notForwardEuler | **真反空转**：首帧位移 | 半隐式 0.1778 / 前向欧拉 **0.0000**——阈值 1e-3 **能抓**两行写反 | ✅ 有效（本批最关键） |
| 5 | ac212a_dtSpike_clampedToBudget | 真因果(单帧位移 ≤ 预算) | 无钳位 ⇒ 位移远超预算，抓得住 | ✅ 有效 |
| 6 | ac212a_hugeDt_equalsClampedDt | 差分重算 | 无钳位 ⇒ huge=50.0 vs clamped=16.32(差 33.7) 抓得住；有钳位 ⇒ 差 0 ✅ | ✅ 有效 |
| 7 | ac212b_maxDt_singleSource_notSecondDefinition | **空转**：只验「可注入」+「两注入互异」+「另一类型有 int 字段 MaxDtMs」 | **对「2 自造第二份 MAX_DT」零抓错力**；实现层 prod 读的是自持字段 `_cfg.MaxDtMs`(=硬编码 100)，**全文无一处读 `WorldLatticeParams.MaxDtMs`** | 🔴 **假绿 / AC 未兑现** |
| 8 | ac213_pitchClamped_yawStillAccumulates | pitch 确被钳(前置断言真) + 负向夹具(整体输入门)能红 | **但期望值 `Dx*N=1.0` 耦合当前实现**：prod `ApplyOrbit` **丢弃 `dt`**(`_ = dtSeconds;`)；若按 GDD F-2-2 补 `Look.x×SENS×dt` ⇒ 期望应为 `0.1×10×SENS/60` ≠ 1.0 ⇒ **测试会红**，即测试把非规格实现钉死 | ⚠️ ADVISORY（非恒真，但耦合实现/规格分叉） |
| 9 | ec24_beyondSnap_teleportsAndZeroesVelocity | 真因果 | 吸附/零速直接可证伪 | ✅ 有效 |
| 10 | ec24_withinSnap_doesNotTeleport | 真判据(弱) | 渐进必 <10（冗余）但仍能抓「错误瞬移」；**恰界点未测** | ✅ 有效（弱） |
| 11 | ec25_noFreezeBranch_onZeroTimeScale | 前半**重言式**(两条独立实例喂同值) + 后半源码正则扫描真 | 源码扫描是真防线；前半任何确定性实现都绿 | ⚠️ ADVISORY（半空转） |
| 12 | group5c_substepCount_isDerived_notTunable | 公式值真(n=2/1) + **名单式**字段扫描 | 只匹 4 个硬编码名 ⇒ 改名(`Substeps`/`NH`)即**静默漏报**；只扫 config 不扫 follower 本体 | ⚠️ ADVISORY（黑名单冒充白名单型） |

> 计数核实：文件实为 **12** 例（`grep -c '\[Test\]'` = 12；Unity `testcasecount="12"`），Story Completion Notes 自述「11 例」——**计数不符**。

## 重点条目

### AC-2-11 过冲容差
ε **来自夹具字段**（`AnchorOvershootEps`，注入 1e-2；prod 默认 1e-3），**非硬编码**。判据为**真不变量**（逐帧采样，非恒真）。抓错力实测（float32 复算全链）见上表 1：能抓阻尼严重错误与符号翻转；对 1.8w 级微偏放过——属正常容差语义，故记 ADVISORY 级「抓错力度」而非缺陷。

### AC-2-12 ①位移预算 / ② MAX_DT 同源  ← 主要 BLOCKING 面
- **①有效**：T5/T6 均能在「无钳位」种类缺陷下变红，且 T6 的差分重算证明钳位后等价 `dt:=MAX_DT`（带容差 = 正确口径）。
- **②🔴 未兑现且假绿**：
  1. 实现侧 `AnchorFollower.cs:92` 读 `_cfg.MaxDtMs`；`AnchorFollowConfig.MaxDtMs = 100` **硬编码**。全文 grep：读 `WorldLatticeParams.MaxDtMs` 的**生产代码 = 0 处**（唯一消费者在测试）。AC 要求的「读系统 1 那**一个**常量」在实现层**不存在**。
  2. 判据侧 T7 的四条断言（`injected.MaxDtMs==250` / `cfg.MaxDtMs != injected.MaxDtMs` / 反射存在 / 类型 int）**没有一条**能证伪「2 侧自造第二份常量」。AC 自陈的负向夹具（2 内复制 `const float MAX_DT = 0.05f`）会**照样通过**。
  3. 测试第 24 行注释「`MaxDtMs = 100, // = WorldLatticeParams.MaxDtMs 的形态(单一来源)`」——**测试自己又抄了一遍值**，正是 GDD 组 6 要防的静默失配形态。
  ⇒ **AC-2-12② 这一 BLOCKING 子条既无实现也无有效判据。**

### AC-2-13 解耦
前置断言真（pitch 确被钳到 PITCH_MAX）；负向夹具「`if (pitchClamped) return;`」**能抓**（该门会让 10 帧 yaw 零累积 ⇒ 断言红）。**但**断言期望值 `Dx*N=1.0` 恰好等于「丢弃 dt」的当前实现；prod `ApplyOrbit` 体为 `UpdateYaw(lookX); UpdatePitch(lookY); _ = dtSeconds;`——**`dt` 被丢弃**，与注释「此处以 dt 线性缩放」及 GDD `yaw += Look.x×SENS×dt` **不一致**。⇒ 一旦按规格补回 dt，本测试**反而变红**。测试**耦合实现而非规格**。另：未覆盖 yaw 回绕跨界点。

### 组 5c 派生量
`SubstepCount(0.1)=2`、`SubstepCount(0.01)=1` 为真公式判据。字段扫描为**名单式**（4 个字段名硬编码）⇒ 命名漂移即漏报；且只扫 `AnchorFollowConfig`。弱防线，非空转。

### EC-2-4 吸附
有效。T9 直接证伪「平滑追赶」；T10 方向正确（弱）。**「恰界 `== TELEPORT_SNAP_DIST`」边界点未测**——故事 QA 明列三点（`<`/`==`/`>`），缺 `==` 一点。

### EC-2-5 无冻结分支
**实现成立**（`Step` 只按 `dt` 积分，不读 `timeScale`）。判据：前半（f1/f2 同 dt 序列）为**重言式**；真正的防线是**源码正则扫描**（去注释后断言不含 `timeScale`）。⚠️ 该扫描依赖 `File.ReadAllText(Application.dataPath/...)`，实跑已通过（文件存在、无 timeScale）。AC 的「恢复首帧不得有位移补偿」未直接测（实现无状态补偿，但无判据）。

## Blockers

- **B1（BLOCKING）AC-2-12② MAX_DT 同源** —— 实现读自持硬编码字段（零处读 `WorldLatticeParams.MaxDtMs`）；T7 判据对「2 自造第二份」零抓错力（AC 自陈负向夹具会通过）。**实现与判据双向未落地**。修法：`AnchorFollower`/装配点从 `WorldLatticeParams` 取值；判据改为**反射/加载期断言读取路径**（或断言 2 侧除单一注入缝外无同义常量），并让「复制值」负向夹具变红。

## 非阻断（ADVISORY）

- **A1 AC-2-13**：期望值耦合当前「丢弃 dt」实现，与 F-2-2 公式分叉；按规格补 dt 后测试会红。需澄清 `ApplyOrbit` 是否应含 `SENS×dt`，并让期望值由规格推导。缺 yaw 回绕跨界点。
- **A2 EC-2-5**：前半判据为重言式（同值差分）；仅源码扫描有效。补「恢复首帧位移分布」判据。
- **A3 组 5c**：字段扫描为名单式，改名即漏；扩为「配置类不得出现除白名单外任何 float/int 标量」或反射全字段语义检查。
- **A4 AC-2-11**：抓错力偏窄（阻尼 1.8w 级微偏不报）——如属预期请登记容差语义，否则收紧。
- **A5** Story Completion Notes 的 `Criteria`/`Deviations`/`Test Evidence`/`Code Review` 四栏仍 `_待填_`；例数「11」应为「12」。

## 复现命令

```bash
# B1：MAX_DT 无生产消费者（同源未落地）
grep -rn "MaxDtMs" unity/Assets --include=*.cs | grep -v "WorldLattice.cs"
grep -n "MaxDtMs" unity/Assets/Gameplay.Presentation/Camera/AnchorFollower.cs   # 92: _cfg.MaxDtMs（自持）

# AC-2-13：prod 丢弃 dt
sed -n '152,159p' unity/Assets/Gameplay.Presentation/Camera/CameraRig.cs        # _ = dtSeconds;

# 程序集引用（已核实：EditMode.asmdef 经 GUID 49b3… 引 Sim，编译无碍）
grep -n "49b36e3ee94a392fe97bd7fe76bb27fb" unity/Assets/Tests/EditMode/EditMode.asmdef

# Unity EditMode 实跑（已执行）
unity -batchmode -nographics -runTests -testPlatform EditMode -projectPath unity \
  -testFilter AnchorFollowOrbitTest -logFile unity/Logs/qa-story003-anchor.log \
  -resultFile /tmp/qa-story003-anchor.xml
# ⇒ unity/TestResults-639266569884253560.xml : total=12 passed=12 failed=0
```
