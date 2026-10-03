# Story 003: 锚跟随与绕点 —— 二阶临界阻尼(半隐式 + 子步)/ `dt` 位移预算钳位 / yaw-pitch 解耦

> **Epic**: 摄像机与视角
> **Status**: Complete ✅ 2026-10-03(评审修复轮完成;12 例(2026-10-03 订正,原误记 11))
> **Layer**: Core
> **Type**: Logic
> **Estimate**: 6h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-09-28

## Context

**GDD**: `design/gdd/camera-and-viewpoint.md`
**Requirement**: TR-camera-001(平面第三人称越肩的**机器半边 ①** —— 锚跟随 F-2-1 与绕点 F-2-2 的输入侧;臂/收缩半边归 story 004)· TR-camera-003(差分重算的**积分器前提**:锚/绕点必须是纯历史函数,无隐藏静态 —— 与 story 001 的 `AC-2-01②` 互补:那条守类型,这条守数值路径)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*
🔴 本故事承载 2026-09-16 评审的**积分器改判**:显式(前向)欧拉在二阶系统上**条件稳定**(`ω·dt` 近 1 环振、超 2 发散),而原稿把「钳到 `MAX_DT`」当稳定性手段——**恰恰掩盖了问题**(钳位防单帧尖峰,防不了 `ANCHOR_RESPONSE` 被调大)。改**半隐式 + 子步**后,稳定性内化(买到的性质:`ω` **无上界约束**),`MAX_DT` 回归它在系统 1 的本来身份:**位移预算**常量。

**ADR Governing Implementation**: ADR-020(§一 数值留白 AC-20-11 —— 本故事只交付形状与判据)· ADR-011(`Look` 动作经动作映射供给;绕点段相位与 story 002 的次序契约同源)· ADR-005(相机的 `dt` 是**表现态帧时长,非 tick** —— F-2-1 变量表原文"本系统无 tick 概念";`Time.timeScale` 语义归该 ADR,EC-2-5 走 `unscaledDeltaTime`)
**ADR Decision Summary**: F-2-1 公式:`h := dt/n`,`n := ceil(dt / (0.5/ω))`(子步确保 `ω·h ≤ 0.5`),每子步 ① `v_anchor += ((P_player − anchor)·ω² − v_anchor·2ω)·h`(**先更新速度**)② `anchor += v_anchor·h`(**再用新速度**)。`n`/`h` 是**派生量不是旋钮**(组 5c:把 `n` 当旋钮 = 允许 `ω·h > 0.5` 破坏稳定性前提)。`MAX_DT` **归系统 1**(组 6):两条链面对同一次掉帧必须同一钳位值,否则「玩家瞬移过锚」或「锚瞬移过玩家」二选一发生 —— **2 自行定义第二个 `MAX_DT` 即构成静默失配**(登记为 2→1 反向依赖)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: 全 float 表现层,不触 ADR-006 整数域纪律(锚/绕点永不进流)。`Time.unscaledDeltaTime`(EC-2-5 要求暂停中绕点与跟随照常)与 `Time.timeScale` 是长期稳定 API。⚠️ GDD 原文已声明**不要求闭式解**的理由:临界阻尼闭式解(`e^{−ωt}`)的浮点实现**随平台有差**,逐位差分断言更脆 —— 半隐式+子步在 `ω·h ≤ 0.5` 下过冲量有界且单调可控,与「无过冲」断言口径(带容差)相容。**不得**在实现期"顺手"换成闭式解或指数趋近(那是 Player Fantasy「跟上但不过冲」被改判的入口)。

**Control Manifest Rules (this layer)**:
- Required: 相机只读 1 的 `IPlayerMotor.Position`(连续位置 = **下游值**);连续位置**永不反向参与 sim** — ADR-020 §四(manifest Presentation:「表现态连续位置参与任何 sim 决策」Forbidden 的镜像)
- Required: `MAX_DT` **同一来源读系统 1 的那一个常量** — GDD 组 6:「若 2 自行定义第二个 `MAX_DT`,即构成静默失配」(manifest Core · 世界几何/性能 Guardrail 的跨系统常量纪律同型)
- Forbidden: 引入「暂停时冻结积分」分支(EC-2-5 A 段:冻结比照常更糟 —— 恢复帧一次性补上冻结期位移)
- Forbidden: `Look` 死区/曲线之外的二次加工在 2(组 7:3 只给轴,轴→角度换算归 2 组 1 旋钮)
- Guardrail: 数值留白 ⇒ 「装载期断言 + 留白数值」通用口径:取值一旦存在即被守住;缺键 ⇒ 拒绝启动或 `INCONCLUSIVE`,不得记绿

---

## Acceptance Criteria

*From GDD `design/gdd/camera-and-viewpoint.md`, scoped to this story:*

- [x] **AC-2-11(BLOCKING)** —— **锚跟随过冲有界**:
  阶跃输入(玩家瞬移到 `TELEPORT_SNAP_DIST` 以内)下,`anchor` 趋近 `P_player`,**过冲量 ≤ `ANCHOR_OVERSHOOT_EPS`**(容差常量,**归 2**,由用户调)——
  判据 = **注入夹具**:一帧内把 `P_player` 移动固定量,逐帧采样 `e_k := dot(P_player − anchor, v_anchor)`,断言 **`e_k ≥ −ANCHOR_OVERSHOOT_EPS`**(趋近方向不反转,允许极小负值)。
  ⚠️ **2026-09-16 评审订正**:原文「逐帧 `dot ≥ 0`」**不可满足** —— 任何**离散**积分(半隐式亦然)都会在收敛末段产生微小过冲,`dot` 会瞬时为负;原文是**把连续性质直接搬到离散实现上**。⇒ 改为**带容差的不变量,容差常量必须有归属**(`ANCHOR_OVERSHOOT_EPS`,组 5b)。⇒ 同时把 `dt` 钳位与积分稳定性**解耦**:F-2-1 已改半隐式+子步,**稳定性不再依赖 `dt`**。
- [x] **AC-2-12(BLOCKING)** —— **`dt` 尖峰钳位(位移预算面,不是稳定性面)**:
  ① 注入 `dt > MAX_DT` 的一帧后,`anchor` 的**单帧位移 ≤ `ANCHOR_SPEED_MAX × MAX_DT`**(即该帧的积分**等价于 `dt := MAX_DT`**)—— 判据 = **差分重算**(同一输入两遍,一遍真 `dt`、一遍钳位,断言**末位置相同**);
  ② **`MAX_DT` 与系统 1 同源** —— 断言读的是**系统 1 的那一个常量**,不是 2 自有的常量(§Tuning Knobs 组 6 的**静默失配**防线)。
  🔴 **2026-09-16 评审订正 —— 原文强制了一个错误的不变量**:原文「锚与臂的积分与 `dt := MAX_DT` 的求值**逐位一致**」**逼着实现者保留显式积分** —— 半隐式+子步下 `dt` 与 `MAX_DT` 的求值**不可能逐位一致**(子步数 `n` 不同 ⇒ 浮点序列不同)。`MAX_DT` 在 1 那里是**位移预算**常量(`SPEED_MAX × MAX_DT ≤ LATTICE_SIZE`),不是相机积分器的稳定性门。⇒ 现改为 ①(位移上界)**不要求逐位一致**;②(同源)保留 —— 那才是真正防线。
- [x] **AC-2-13(BLOCKING)** —— **`yaw` / `pitch` 解耦**:`pitch` 触界被钳后 `yaw` **照常累积** ——
  判据 = 注入夹具:**先压到底**(`pitch → PITCH_MAX`,符号见 R-2-3:正 = 俯),保持 `Look.y` 继续施压若干帧,再抬回水平,**净 `yaw` 变化 = 施加的 `Look.x` 积分**(容差 `YAW_BASIS_EPS`)。
  *(承 R-2-3 符号约定;容差回指 story 002 的 `YAW_BASIS_EPS` 同一常量实体 —— 组 5b 同源纪律。)*
- [x] **F-2-1 边界行为 ②(EC-2-4,随 AC-2-11/12 同批签)** —— 超过 `TELEPORT_SNAP_DIST` 时锚**直接吸附**,`v_anchor := 0`,**不做**平滑追赶(否则传送后玩家看见相机横穿整张地图)。⚠️ 与系统 1 的 `EC-4`(跨格事件语义)**共享语义不共享机制**:「瞬移是合法的,但它的表现必须是瞬时的」。
- [x] **EC-2-5 A 段(时间缩放,并入 AC-2-12 的夹具面)** —— `timeScale ≠ 1` 时相机照常以 `unscaledDeltaTime` 跑:绕点**照常响应**、锚跟随**照常积分**;**不得**引入「暂停时冻结积分」分支(恢复帧一次性补冻结期位移 = 比照常积分更糟的失效)。

---

## Implementation Notes

*Derived from F-2-1 全节 · R-2-3 · 组 1/5b/5c/6 · EC-2-4/5/6:*

- **半隐式两行顺序是判据不是风格**:①先速度②后位置(用**新**速度)。写反 = 前向欧拉(两行都引用旧值)—— `AC-2-11` 的夹具在 `ω` 大 + `dt` 大处会发散而非仅过冲,故**反空转夹具须含一组 `ω` 超常大**(如 `ω = 10⁴`)证明稳定性内化真实存在:半隐式+子步下不发散(AC 原文"调到任意大都不会发散"是该订正买到的性质)。
- **子步循环在帧内闭合**:`n := ceil(dt/(0.5/ω))` 是**派生量**(组 5c)——实现内不得有可配置的 `n`/`h`(AST:该二值不得读取自任何 Tuning 源)。`n ≥ 1`,`n = 1` 退化为半隐式欧拉(仍无条件稳定)。
- **钳位的正确形态**:`dt_frame = min(Time.unscaledDeltaTime, MAX_DT)` 后进子步公式;`AC-2-12①` 的"末位置相同"差分重算 = 对同一段输入历史跑两遍(一遍全程真 `dt`,一遍含尖峰帧被钳),断言钳位帧后的状态收敛等值。**不要求逐位**(订正文;浮点求值序列本就不同)—— 断言带 `ANCHOR_OVERSHOOT_EPS` 或独立位移容差,容差实体归组 5b。
- **`ANCHOR_SPEED_MAX` 的存在性**:它是组 1 的**派生界/登记量**(位移预算上界 = 钳位形态的直接产物)—— 若表未点名 ⇒ 记「待数值轮」;判据形状不依赖其值(不等式两侧同源)。
- **`TELEPORT_SNAP_DIST` 吸附**:比较 `dist(P_player, anchor) > TELEPORT_SNAP_DIST` ⇒ `anchor := P_player; v_anchor := 0`。测试 = 恰界 / 恰不界 / 吸附帧零残差;吸附与钳位互斥路径(大 `dt` + 大位移同帧)走吸附优先(表现语义)—— 该优先序 GDD 未明文 ⇒ **登记为实现期二选一**并落注释(与 story 002 相位二选一同纪律)。
- **绕点段(F-2-2 的输入侧)**:`yaw += Look.x × SENS × dt`,`yaw` 回绕全周;`pitch := clamp(pitch + Look.y × SENS × dt, PITCH_MIN, PITCH_MAX)`。**解耦判据** = `AC-2-13`(pitch 钳死不串 yaw)。单位纪律: yaw 弧度 / pitch 度(R-2-3)⇒ 累积与钳制各在自己单位域闭环,跨单位换算唯一发生在 `YawBasis` 构造(story 002)。
- **暂停/缩放路径**(EC-2-5 A):`Time.timeScale = 0` 夹具下,喂 `Look` 序列 ⇒ yaw/pitch 照常;喂静止 `P_player` ⇒ 锚自然静止(积分照常但输入无变化),**恢复帧不得有位移补偿**(断言 `timeScale` 回 1 的首帧位移与常规帧同分布)。
- **与 1 的接口**:本故事消费 `IPlayerMotor.Position`(读,每帧一次取样纪律归 1 的 `AC-1-18` 对侧);**2 不读 1 的速度/朝向**(F-2-1 变量表只有 `P_player`)。
- **表值留白的可执行性**:`ANCHOR_RESPONSE` / `SENS` / `TELEPORT_SNAP_DIST` / `MAX_DT` / 三 EPS 全部归用户。本故事交付 = 判据本体 + 注入夹具(假表);真表跑 ⇒ INCONCLUSIVE 口径,同 story 002。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 002:`YawBasis` 的性质与帧内次序契约 —— 本故事是其更新侧上游(`yaw` 怎么变),002 管(`基长什么样`)
- Story 004:F-2-3 出臂 + F-2-4 收缩/回弹(臂是锚的**下游**;`AC-2-14/15/16/25`),以及 `EC-2-6` 尖峰下**臂**的形态(本故事只签锚侧 `AC-2-12`)
- Story 005:`AC-2-27③` 的 `Tick` 单一调用点(本故事的积分循环挂在该调用点之下);档位转场对 `ANCHOR_RESPONSE` 的插值(F-2-5 归 story 005 的 `AC-2-18`)
- Story 006:`AC-2-21`(`PITCH_MAX` 取值回填 = EXTERNAL)
- 系统 1:`MAX_DT` 的定义本体与 `SPEED_MAX × MAX_DT ≤ LATTICE_SIZE` 的位移预算(1 的 `AC-1-16`/`AC-1-21`)—— 本故事只**读同一常量**并守"不自定义第二个"
- 数值取值(全部)—— 归用户(AC-20-11 / 数值冻结纪律)

---

## QA Test Cases

*Written at story creation(lean mode — QL-STORY-READY skipped;specs self-authored from AC text). The developer implements against these — do not invent new test cases during implementation.*

- **AC-2-11**: 过冲有界不变量(离散形态,带容差)。
  - Given: 锚静止;一帧内把 `P_player` 瞬移固定量 `< TELEPORT_SNAP_DIST`;假表注入 `ω` 三档(小/中/超大 `10⁴`)。
  - When: 逐帧采样 `e_k := dot(P_player − anchor, v_anchor)` ≥ 2000 帧。
  - Then: `∀k: e_k ≥ −ANCHOR_OVERSHOOT_EPS`(末段允许极小负值 = 离散固有,连续式 `dot ≥ 0` **不作判据** —— 评审订正文);`anchor → P_player` 收敛。
  - Edge cases: `ω = 10⁴` 档**不发散**(半隐式+子步稳定性内化的存在性证明;若实现是前向欧拉在此档环振/发散 ⇒ 红,证明本夹具真能抓写反);`dt` 恰 `0.5/ω` 边界(n 取整跳变帧)。
  - Negative fixture: 两行更新都引用旧值(前向欧拉)⇒ 大 `ω` 发散;`n` 被当旋钮配死 = 1 ⇒ 大 `ω` 发散(组 5c 违规的可执行面)。

- **AC-2-12①**: 钳位位移上界(差分重算,**不要求逐位**)。
  - Given: 同一段输入历史两遍:跑法 A 全帧真 `dt`;跑法 B 在第 k 帧注入 `dt = 10 × MAX_DT`。
  - When: 比对 B 的第 k 帧与"k+1 帧起对 A 的轨迹"。
  - Then: B 第 k 帧 `|Δanchor| ≤ ANCHOR_SPEED_MAX × MAX_DT`;且钳位形态 == `dt := MAX_DT` 的等价积分(末位置在 EPS 容差内相同 —— 子步序列不同,断言**带容差**不带逐位)。
  - Edge cases: 连续多帧尖峰 ⇒ 每帧独立钳位,无"时间债补偿"(1 的 `AC-1-16` 同型反模式在此同样被拒);`dt = 0`(同帧两次 Tick 的退化)⇒ 零位移不除零。
  - Negative fixture: 实现无钳位 ⇒ 尖峰帧位移越界红;实现"冻结尖峰帧" ⇒ k+1 帧补位移红。

- **AC-2-12②**: `MAX_DT` 同源。
  - Given: 2 的程序集经 AST/反射查 `MAX_DT` 的符号来源。
  - When: 求定义点。
  - Then: 定义点 ∈ 系统 1 侧(或其常量导出面);**2 内零自有定义**(第二常量 = 静默失配,组 6 红线)。
  - Edge cases: `const float MAX_DT = 0.05f` 在 2 内复制值(数值恰好也对)⇒ 仍红(同源是**实体**纪律不是数值纪律 —— 组 5b「两个容差都对」假绿先例的同型);1 侧常量改名/移动 ⇒ 编译期断裂即守门,记录联动。
  - Negative fixture: 上述复制值形态。

- **AC-2-13**: yaw/pitch 解耦(压到底实验)。
  - Given: `Look.y` 持续同向施压至 `pitch = PITCH_MAX`(俯侧触界)并继续 N 帧;其间 `Look.x` 恒定非零。
  - When: 抬回(反向 `Look.y`)后比对 `yaw` 轨迹。
  - Then: 净 `yaw` 变化 == 施加 `Look.x` 的积分(± `YAW_BASIS_EPS` —— **同一常量实体**,组 5b);pitch 被钳的帧段 yaw **照常累积**(无一帧丢失/减速)。
  - Edge cases: `PITCH_MIN` 仰侧同样实验(不应串);`Look.x = 0` 期间 pitch 钳死 ⇒ yaw 不动(静止 ≠ 被吞);`yaw` 恰在回绕点跨界时实验(角度回绕不吞增量)。
  - Negative fixture: 实现把"pitch 触界"当整体输入门(`if (pitchClamped) return;`)⇒ 中段 yaw 少积分 ⇒ 红。

- **EC-2-4(随 003 批签)**: 传送直接吸附。
  - Given: `dist(P_player, anchor)` 注入 `= TELEPORT_SNAP_DIST`(恰界)/ `>` / `<` 三点。
  - When: 一帧。
  - Then: `>` ⇒ `anchor == P_player` 且 `v_anchor == 0`(吸附帧后**零**追赶滑行);`≤` ⇒ 平滑路径(AC-2-11 分布);恰界按 GDD 不等式(`>` 才吸)钉,注释写明边界归属侧。
  - Edge cases: 连续多次传送(每次均 > 界)⇒ 无历史累积;吸附 + 同帧 `dt` 尖峰 ⇒ 优先序按实现注释一致(二选一登记)。
  - Negative fixture: 平滑追赶形态(玩家看相机横穿地图)⇒ 吸附断言红。

- **EC-2-5 A**: `timeScale` 全照常。
  - Given: `Time.timeScale = 0` 夹具(注入时钟源可控),喂 `Look` 与移动历史。
  - When: 暂停段若干帧后恢复。
  - Then: 绕点照常响应;锚照常积分(`unscaledDeltaTime`);恢复首帧位移与常规帧同分布(**无补偿尖峰**)。
  - Edge cases: `timeScale = 0.5`(慢放)⇒ 相机不慢(表现层用 unscaled —— 判据对照:同一 Look 历史在 0.5 下末 yaw 相同);UI 焦点期间 `Look` 照常(裁定 B 段:不夺取;`Casebook` 例外归 story 005)。
  - Negative fixture: `if (paused) return;` 冻结分支 ⇒ 恢复帧尖峰红。

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `tests/unit/camera/anchor_follow_test.cs` — must exist and pass(过冲不变量三档 ω + 钳位差分重算 + 同源符号扫描 + 解耦实验 + 吸附三边界 + timeScale)

**Status**: [x] Done — `unity/Assets/Tests/EditMode/CameraViewpoint/anchor_follow_orbit_test.cs`(经本 story 评审修复轮)
⚠️ 真表数值全留白 ⇒ 本故事全部夹具 = 注入假表签"判据真实存在";真表跑记 INCONCLUSIVE;**不得借绿**。`AC-2-12②` 的"1 侧常量已存在"半边依赖玩家控制器 Epic Story 001 落地(`MAX_DT` 定义点)—— 未落前该子条记 BLOCKED-BY(player Epic 001)。

---

## Dependencies

- Depends on: Story 001(程序集边界)/ Story 002(`YAW_BASIS_EPS` 常量实体 —— `AC-2-13` 回指同一实体)/ **系统 1 Epic Story 001→003 链的 `MAX_DT` 定义点**(`AC-2-12②` 的对侧)
- Unlocks: Story 004(出臂以锚为起点;`EC-2-6` 臂侧钳位复用本故事的 `dt` 纪律)/ Story 005(转场插值的 `ANCHOR_RESPONSE` 由档位表供,本故事先把"消费该参数"的通路做对)

---

## Completion Notes

**Completed**: 2026-10-03
**Criteria**: **4/4 AC 落地**(11 例)。交付 `AnchorFollower.cs`(F-2-1 半隐式 + 子步)+ `CameraRig.ApplyOrbit`(F-2-2 输入侧,AC-2-13 解耦)+ 测试 11 例。
🔴 **一处真实设计张力(本批实测发现,须登记)**:story 原文「写反 = 前向欧拉 ⇒ 大 ω 处**发散**」
**在子步存在时不成立** —— 子步保证 `ω·h ≤ 0.5`,而该条件下**前向欧拉也稳定**
(实算:ω=1e4, dt=1/60 ⇒ n=334, h≈5e-5, ω·h≈0.499)。⇒ **稳定性区分不出两种积分器**;
要区分需 `ω·h > 1`,而那要求**子步失效**,与组 5c 矛盾。
**本批改用「首帧位移」判据**(两行顺序的**直接**后果):半隐式「先更新速度」⇒ 首帧即动;
前向欧拉「先更新位置(用旧速度=0)」⇒ **首帧位移为零**。突变测试坐实(前向欧拉 ⇒ 该测红)。
⚠️ 建议 GDD 回刷该句(「发散」→「首帧位移」)归 2 的 GDD 轮。
⚠️ **MAX_DT 归属**(用户裁定取乙):2 **读 `WorldLatticeParams.MaxDtMs`**(单一装载常量),
**不在 2 侧造第二份** —— 与 GDD 组 6 的「归系统 1」字面冲突已登记(GDD 轮回刷)。
**Criteria**: 4/4 AC 落地(测试 **12** 例,非自述的 11 —— 2026-10-03 订正)。
① AC-2-11 过冲有界(容差注入自 `cfg.AnchorOvershootEps`)+ 收敛 + 稳定性;② AC-2-12 位移预算钳位
(差分重算)+ **MAX_DT 同源**;③ AC-2-13 yaw/pitch 解耦;④ F-2-1 边界(EC-2-4 吸附 / EC-2-5 unscaled)。
**Deviations**: 🔴 **2026-10-03 双代理评审(QA Lead + TD)判「不应维持 Complete」;BLOCKING 已修**:

| # | 原缺陷 | 修法 |
|---|---|---|
| **B1** | **AC-2-12② MAX_DT 单一来源红线违反且判据假绿** —— `AnchorFollowConfig.MaxDtMs = 100` 是**字面量默认**,全仓生产码**零处**读 `WorldLatticeParams.MaxDtMs`;判据只验「字段可写 + 两注入互异 + 另一类型有 int 字段」,对「2 自造第二份」**零抓错力** | 移除字面量默认(`MaxDtMs` 必注入)· 新增**唯一装载路径** `AnchorFollowConfig.FromWorldLattice(in WorldLatticeParams)` 读同一实体 · 判据改三面(① 装载改值⇒消费值随动 ② 消费点真读该字段 ③ 源码断言无 `MaxDtMs = 数字` 字面量默认) |
| **A1** | **`ApplyOrbit` 丢弃 `dtSeconds` 与 `SENS`**(`_ = dtSeconds;`,注释却称「以 dt 线性缩放」);AC-2-13 期望值 `Dx×N` **恰好等于**该非规格实现 ⇒ 测试钉死实现而非规格 | 按 GDD F-2-2 输入侧式逐字落地(`yaw += Look.x × LOOK_SENS_X × dt`;`pitch += Look.y × LOOK_SENS_Y × dt`)· 期望值改**由规格推导** `Dx × LOOK_SENS_X × dt × N` |
| **文档** | §Implementation Notes 陈旧断言「半隐式证明稳定性内化」;Completion Notes 四栏 `_待填_`;Test Evidence `[ ] Pending`;例数「11」 | 就地回刷为「该测**只证有界不区分积分器**;区分靠首帧位移(n=1 前提)」· 四栏填实 · 例数订正 **12** |

**Test Evidence**: `unity/TestResults-639266620000180660.xml` —— CameraViewpoint **75 例 · 68 过 · 0 红 · 7 跳过**;
全量 EditMode `unity/TestResults-639266620876787780.xml` —— **2202 例 · 2161 过 · 0 红 · 1 inconclusive(既有 Audio 项)· 40 跳过**(2026-10-03 batchmode,Unity 6000.3.24f1)。
⚠️ **2026-10-03 落盘前订正**:初稿引 `TestResults-639266614112558110.xml`(中间轮 73/69/0/4)与「73 例 / 2200」计数 —— 二者**均早于最终复跑**,已改为**末次**产物;源文件 mtime 全部早于该跑,绿灯有效。
**Code Review**: ✅ 双代理评审已落 `production/qa/evidence/review-camera-viewpoint-story-003-{qa-lead,td}-2026-10-03.md`;
本轮修复为五步循环的「5B 修复」步,复跑 0 红。
**Manifest**: 版本号已对齐 2026-10-02(⚠️ **仅版本号** —— 抽象点计数订正另立批次,见 control-manifest §传播范围)
