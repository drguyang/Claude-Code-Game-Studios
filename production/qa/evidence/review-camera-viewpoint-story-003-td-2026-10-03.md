# TD 评审 — camera-viewpoint Story 003(锚跟随与绕点)

- **日期**: 2026-10-03
- **评审人**: technical-director
- **对象**: `unity/Assets/Gameplay.Presentation/Camera/AnchorFollower.cs`
  · `unity/Assets/Gameplay.Presentation/Camera/CameraRig.cs`(ApplyOrbit/UpdateYaw/UpdatePitch)
  · `unity/Assets/Tests/EditMode/CameraViewpoint/anchor_follow_orbit_test.cs`
- **Story**: `production/epics/camera-viewpoint/story-003-anchor-follow-orbit.md`(Status: Complete ✅)
- **测试实测**: `unity/TestResults-639266569884253560.xml` — `AnchorFollowOrbitTest` 12/12 Passed(0 failed / 0 inconclusive)
- **裁决**: **REJECT(不应维持 Complete)** —— AC-2-12② 判据为**假绿**,实现**违反单一来源红线**

---

## 1. MAX_DT 同源(AC-2-12②)—— **REJECT**

**用户裁定(乙)**:2 **读系统 1 的 `WorldLatticeParams.MaxDtMs`**,**不在 2 侧造第二份**。

**实现事实**(全仓 grep 核实,排除 `Library/` 与 `Tests/`):

| 事实 | 证据 |
|------|------|
| 2 侧定义点是 **`AnchorFollowConfig.MaxDtMs = 100`**(带**字面量默认值**的实例字段) | `AnchorFollower.cs:33` |
| 消费点只读该字段 | `AnchorFollower.cs:92` `_cfg.MaxDtMs / 1000f` |
| **全仓无任何代码把 `WorldLatticeParams.MaxDtMs` 写入 `AnchorFollowConfig`** | grep `.MaxDtMs` 非测试命中仅 `AnchorFollower.cs`(读)与 `WorldLattice.cs:69/174/186`(定义) |
| 2 侧**无**构造器/装载缝接收 `WorldLatticeParams` | `AnchorFollowConfig` 为纯 POCO,零构造参数 |

⇒ 注释「与 `WorldLatticeParams.MaxDtMs` **同一来源**」(`:30`)**是一句注释,不是机制**。「同源」的机械含义(读同一实体)**未成立**;`= 100` 恰是 AC-2-12② Edge Case 点名的**复制值形态**(原文对照:1 侧常量改 150 ms ⇒ 2 侧仍钳 100 ⇒ **静默失配**发生,组 6 红线)。

**判据强度:不足(假绿)** —— `test_ac212b_maxDt_singleSource_notSecondDefinition` 只断言三件与「来源」**无关**的事:
1. 注入 250 ⇒ 读回 250(证明**字段可写**,非来源);
2. `cfg.MaxDtMs != injected.MaxDtMs`(恒真);
3. `WorldLatticeParams` 有名为 `MaxDtMs` 的 `int` 字段(**存在性**,非同源)。

**该测在两常量取 100 vs 999 时照样绿** —— 与故事自己在 AC-2-12② Edge Case 里点名的「两个容差都对」假绿**同型**。故事 §QA 要求的真判据是「**2 的程序集经 AST/反射求 `MAX_DT` 的定义点,须 ∈ 系统 1 侧,2 内零自有定义**」—— **实现里这个探针不存在**。**AC-2-12②(BLOCKING)判据挂空。**

---

## 2. 半隐式两行顺序 —— **PASS(正确性)/ CONCERNS(判据面)**

**逐字落地 ✓** —— `AnchorFollower.cs:103-106`:`accel = (playerPos−Anchor)·ω² − Velocity·(2ω)` → `Velocity += accel·h`(**先速度**)→ `Anchor += Velocity·h`(**用新速度**)。与 F-2-1 权威式逐字一致(`2ω` = `2f*omega` ✓)。

**但突变可抓性弱(实测)**:
- `test_ac211_stabilityHolds_atHugeOmega`(ω=1e4)**对两种积分器都绿**。数值复核:ω=1e4、dt=1/60 ⇒ n=334、ω·h≈0.499;半隐式与**前向欧拉**300 帧后 `max|anchor|=1.0`、均无 NaN ⇒ 该测**零区分力**(它只证有界,而有界是两者共有性质)。故事 Completion Notes 已诚实登记此点,但 §Implementation Notes:57「半隐式+子步下不发散…证明稳定性内化真实存在」**仍是未回刷的陈旧断言**。
- 真正的区分夹具 `test_ac211_semiImplicitOrder_notForwardEuler` 采**首帧位移**:半隐式首帧即动(实测 ω=8 ⇒ +0.1778),前向欧拉首帧位置更新用旧速度 0(实测 ⇒ 0.000000)。**该测确实抓得住**——但其区分力**只在 `n == 1` 时成立**:ω=8、dt=1/60 ⇒ n=1;我把 ω 抬到 1e4 ⇒ n=334,前向欧拉首帧位移也≈10.0 ⇒ **同样判别失效**。⇒ 该夹具**单点脆弱**(依赖 `ANCHOR_RESPONSE` 小到 n=1),故事未登记此边界。

---

## 3. n/h 是派生量非旋钮(组 5c)—— **PASS**

- `SubstepCount`: `n = CeilToInt(dt / (0.5/ω))`,钳 `n ≥ 1` —— **纯派生**;`h = dt/n` 为 `Step` 内局部量(`:97`)。✓
- **字段面证据**:`AnchorFollowConfig` 字段 = `AnchorResponse` / `MaxDtMs` / `TeleportSnapDist` / `AnchorOvershootEps` / `AnchorSpeedMax` —— **无 `n` / `h` 字段**。✓
- `test_group5c` 断言无同名旋钮(名字模式匹配较弱,但真证据是类里根本没有该字段)。✓

*唯一次级观察*:`SubstepCount` 为 `public` 方法(暴露派生查询),非旋钮,不构成违规。

---

## 4. F-2-1 权威式逐字对应 · 与 001/004 接缝 —— **CONCERNS**

**锚公式逐字 ✓**(见 §2)。子步 `n := ceil(dt/(0.5/ω))`、`h := dt/n` ✓。钳位 `min(dtSeconds, maxDt)` ✓。EC-2-4 吸附**先于**钳位且早返回,注释落点 `:82-83` ✓(故事点名的「优先序注释位置」已具备)。

**Applied** 偏差(绕点侧,非 F-2-1 本体):
- 故事 §Implementation Notes 与 GDD 组 1 式 = **`yaw += Look.x × SENS × dt`**;实现 `ApplyOrbit` 为 **`_yaw += lookX`** —— **`SENS` 与 `dt` 双双未参与**(`CameraRig.cs:158` `_ = dtSeconds;` 显式丢弃)。注释写「以 dt 线性缩放」与代码**不符**。
- 连带:`test_ac213` 的幅度断言 `yawAfter − yawBefore == Dx × N`(无 `dt`、无 `SENS`)**编码的是实现而非 GDD 式**;自洽但对错式。组 1「换算归 2」的 `LOOK_SENS_X/Y` 落点**在 2 侧缺位**(可能被推给 story 005,但故事未登记该移交)。
- **解耦性质本身 ✓**:`ApplyOrbit` 先 `UpdateYaw` 后 `UpdatePitch`,无短路;`UpdatePitch` 只 `Clamp` 自身;AC-2-13 实验成立。

**接缝**:
- **与 004(臂锚下游)**:`CameraArmSolver.Shoulder(Vector3 anchor, in YawBasis)` 以 anchor 为入参 ⇒ 方向正确 ✓。**但生产路径零消费者** —— 全仓无 `new AnchorFollower(...)`、无 `ApplyOrbit` 调用点(命中全在测试)⇒ `AnchorFollower`/`ApplyOrbit` 在**出货代码中为孤儿**,锚→臂→`IPlayerMotor.Position` 三条线**只声明不接线**。
- **与 001(档位)**:`ANCHOR_RESPONSE` 经 cfg 注入缝供 005 做档位插值(AC-2-18)—— 形状可用 ✓。
- **与 001(系统 1)**:唯一承重接缝(`MAX_DT`)即 §1 的断裂点。
- 孤儿状态可归 story 005 的 `Tick` 单一调用点,但故事**未在 Out of Scope 之外登记「接线未做」**,却在 Completion Notes 记 Complete ⇒ 见 §5。

---

## 5. story 与实现/测试的偏差 —— **CONCERNS**

| 项 | story 自述 | 实测 | 判决 |
|----|-----------|------|------|
| AC-2-12② 同源 | 「2 读 `WorldLatticeParams.MaxDtMs`…不在 2 侧造第二份」 | **代码不读**(§1) | **陈述与实现对不上** |
| 测试例数 | 「11 例测试通过」 | **12** 例(XML `testcasecount=12`) | 计数陈旧 |
| Completion Notes 模板 | 要求附「ω 超大档实测输出 + 优先序注释位置」 | **`Criteria` / `Deviations` / `Test Evidence` / `Code Review` 四项仍 `_待填_`**;且 `Criteria` 出现**两次**(151 行 4/4 + 161 行 `_待填_`) | 模板未收口却挂 Complete |
| Test Evidence 字段 | `[ ] Pending — story not yet implemented` | 实现与测试均已落盘 | 未回刷 |
| §Implementation Notes:57 | 「半隐式…证明稳定性内化真实存在」 | 该测对前向欧拉**同样绿**(§2) | 陈旧断言未回刷 |
| 正向记录 | 正确识别并登记「前向欧拉在大 ω 不发散 ⇒ 原判据失效」+ 改首帧位移判据 | ✓ 与数值复核一致 | **值得肯定** |

---

## 结论与必改项

**总判 REJECT(不应维持 Complete)** —— 核心物理(半隐式两行、子步派生、吸附、解耦)实现正确且经数值复核;但 **AC-2-12②(BLOCKING)判据假绿 + 实现违反单一来源红线**,且 story 对此陈述与代码不符。

**转 Complete 前置(窄而廉,建议本批内清)**:
1. **AC-2-12② 真判据**:① 实现侧去掉 `MaxDtMs = 100` 字面量默认(或迁移默认值为「必注入」,无注入即拒启动),并把装载缝接到 `WorldLatticeParams.MaxDtMs`;② 测试侧换成**真来源探针**——AST/IL 扫 2 程序集内不得存在 `MAX_DT` 的数值字面量定义 + 断言消费值来自 `WorldLatticeParams` 实例。当前反射「字段存在」不足以守住任何东西。
2. **回刷** story 的「2 读 `WorldLatticeParams.MaxDtMs`」为真实机制描述;§Implementation Notes:57 陈旧断言改为「该测只证有界,不区分积分器;区分靠首帧位移(n=1 前提)」。
3. **补齐 Completion Notes 四个 `_待填_` 字段 + 去重复 `Criteria`**,测试例数改 12。
4. **登记遗留**:`ApplyOrbit` 丢弃 `SENS`/`dt`(组 1 换算落点在 2 侧缺位)、`AnchorFollower`/`ApplyOrbit` 生产零消费者 —— 明确移交 005/004 或本批接线,不得以「形状已测」默认为已交付。

**可放行不动**:§2 的两行顺序、§3 的 n/h 派生、子步/钳位/吸附/解耦的既有实现与容差口径。
