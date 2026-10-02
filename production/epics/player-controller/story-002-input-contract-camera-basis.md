# Story 002: 输入契约与相机相对方向 —— `MoveInput` 边界断言 + F-1-8 基投影

> **Epic**: 玩家控制器与移动
> **Status**: Complete ✅ 2026-10-02 (双代理评审修复后 16/16 测试通过)
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 4h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-28

## Context

**GDD**: `design/gdd/player-controller-and-movement.md`
**Requirement**: TR-player-003(移动 / 旋转轴由 ADR-011 动作映射供给;1 不消费 `Look`;`‖MoveInput‖` 是速度因子不是开关,在 3→1 边界断言 `≤ 1`)· TR-player-005(二维 `MoveInput` → 世界方向的基 = 相机相对,1 读 2 的只读 yaw basis)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*
🔴 本故事兑现系统 1 首轮 `/design-review` **第一根因**(「移动基向量从未定义」——「缺了它能编译、能运行、能测试通过、但游戏玩不了」,列为 P0 最高优先级)与**第三根因的一半**(`Look` 归属:1 不消费,朝向自动面向移动方向,F-1-4)。

**ADR Governing Implementation**: ADR-020(§一 `MoveInput` 输入面 + Amendment B `ICameraRig.YawBasis`)· ADR-011(action-based 动作映射;`Sprint`/`Jump` 已补进 3 的 GDD)· ADR-013(§四 只读单向:1 读 2 的基,零写入面)
**ADR Decision Summary**: ADR-020 §一(Amendment B):`ICameraRig.YawBasis` = `(Vector3 fwd, Vector3 right)` **水平化正交基**,只读;判据 = AC-20-14。方向**单向**:1 读 2 的基(上游值),2 读 1 的 `Position`(下游值)—— 两条边方向相反,**不构成类型环**;1 **不读 `Look` 动作、不读相机变换矩阵**。ADR-011:1 消费 `Move`/`Jump`(P0 两动作;`Sprint` 绑定存在但 P0 无消费者 = `O-7` 挂账)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: 本故事的数学全在 float 表现层(F-1-8 在 sim 边界之外,不触 ADR-006 的整数域纪律 —— 它产出的是**表现层速度向量**)。`Mathf` 长期稳定 API。⚠️ 退化分支(相机近乎垂直 → `+Z`):`AC-2-09②` 使该分支**正常操作下不可达**,但作为防御性代码保留(不抛异常、照常返回),`OQ-1-14` 挂 `PITCH_MAX` 取值回填。

**Control Manifest Rules (this layer)**:
- Required: **action-based 官方路线** —— `MoveInput` 只能来自 Input System 动作资产(manifest Core · 输入(ADR-011))
- Forbidden: Legacy Input Manager / `Input.GetKey` 族(1 直读输入 = 越权,输入归 3)
- Forbidden: **表现态连续位置参与任何 sim 决策**(manifest Presentation · Forbidden,ADR-020 §四:293)—— 本故事的镜像纪律:1 只读相机的**离散导出量** `YawBasis`,不读相机的 `transform` / 矩阵
- Guardrail: `‖MoveInput‖ ≤ 1` 是 **F-1-1a 成立的输入项** —— 坏值失败形态 = **水平隧穿(静默)**,故边界必须硬断言而非 clamp

---

## Acceptance Criteria

*From GDD `design/gdd/player-controller-and-movement.md`, scoped to this story:*

- [x] **AC-1-09(BLOCKING)** —— **3 → 1 边界硬断言**:`‖MoveInput‖ ≤ 1`,**越界即报错**(形态:抛异常 + 拒绝该帧输入;**1 不 clamp**)。测试: 4 用例（越界/边界/零/NaN）全通过。
- [x] **AC-1-31(BLOCKING · 复核新增 —— 根因 1「移动基向量」)** —— **`v̂_world` 单位性 + 水平面内**。测试: 单位性/水平性/零输入/反向用例（带仰角相机）/全 yaw 扫描（36 点 × 3 幅值）全通过。
- [x] **AC-1-35(BLOCKING · 复核新增 —— 求值次序的完整版本,本故事承担 ①②③④ 中方向相关的部分)**:
  ① **基向量在 `v_target` 之前求值** — CountingCameraRig 验证 YawBasis 调用次序;
  ② **`YawBasis` 每帧只取样一次** — CountingCameraRig 验证每帧一次;
  ③ **`ICameraRig.YawBasis` 是只读接口** — readonly struct + 水平化断言;
  ④ 断言 `r̂ := normalize(cross(worldUp, f̂))` **正交** — 正交性断言测试通过。

---

## Implementation Notes

*Derived from GDD F-1-8 / F-1-2 / R2 / §Interactions 的 2·3 行:*

- **投影公式逐字落地**:`v̂_world := normalize(my × f̂ + mx × r̂)`;`r̂` **从 `f̂` 派生**(`normalize(cross(worldUp, f̂))`),**不单独取** `YawBasis.right` —— 若 2 的接口返回 `(fwd, right)` 两分量,1 侧仍须断言 `right == cross` 派生值一致(④ 的正向半边)。**分开取会在相机轻微 roll 时产生非正交基 ⇒ 移动方向被轻微缩放**。
- **`‖MoveInput‖ = 0` 时不做投影**:走 Idle 分支(`v_target = 0`),**不得**用相机朝向填补(那会让松手瞬间身体朝相机前方"滑"一下 —— 与 F-1-4「`v_horiz ≈ 0` 时保持当前 yaw」同一纪律,后者归 story 003)。
- **边界断言形态钉死**(AC-1-09 要求"写死"):取 **抛异常(`DebugAssertException` 式)+ 该帧输入拒绝**,不取静默 clamp;错误串点名 `‖MoveInput‖` 实测值与上界 1。测试缝:`MoveInput` 经构造注入(依赖注入,非单例 —— coding-standards)。
- **退化分支**:`‖proj_h(f̂)‖ ≈ 0` 时退化到世界 `+Z`,**不抛异常**(F-2-2 末注同构纪律:1 不知道也不该知道俯角上限)。正常操作不可达(`AC-2-09②`),保留为防御。
- **取样纪律**:`FrameSample` 结构 —— 每帧开头一次 `var (f̂, r̂) = rig.YawBasis;` 存局部,链上全部下游用该局部值(②);跨格检测读 `Position` 的同样"每帧一次取样"归 story 004/003 的 `AC-1-18`。
- **1 不消费 `Look`**:1 的程序集内**零** `Look` 动作引用(AST 断言并入本故事:否定"顺手拿相机欧拉角当朝向"的第二真源)。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 003:F-1-2 第二步乘数链(`SPEED_MODE × ‖MoveInput‖ × K_terrain × K_context × K_suppressed`)与 F-1-3 加减速 —— 本故事只交付 `v̂_world` 与边界断言
- Story 003:F-1-4 转向(`v_horiz ≈ 0` 时 yaw 保持、±180° 回绕、`TURN_RATE` 单位)—— `AC-1-20a`
- Story 004:`Position` 每帧取样一次的跨格半边(`AC-1-18` 后半)
- 2 侧的 `YawBasis` 构造与 B 组判据(`AC-2-07…10`)= 2 的 Epic Story 002(本故事的**硬前置**,1 侧只验消费面)
- VR 模式下 `Look` 与相机归属(推 P1a,EC-15/OQ-1-3)

---

## QA Test Cases

*Written at story creation(lean mode — QL-STORY-READY skipped;specs self-authored from AC text). The developer implements against these — do not invent new test cases during implementation.*

- **AC-1-09**: `‖MoveInput‖ ≤ 1` 边界硬断言,1 不 clamp。
  - Given: 测试注入 `MoveInput` 幅值 1.0001(及 1.4 曲线外插反例)。
  - When: 1 的帧入口消费该输入。
  - Then: 按写死形态**抛异常 + 拒绝该帧输入**;错误串点名实测值与上界;稳态绿值:`‖MoveInput‖ = 1` 恰边界 ⇒ 通过。
  - Edge cases: `‖MoveInput‖ = 0` ⇒ 合法(走 Idle,不报错);NaN 幅值 ⇒ 拒(不得穿透成 NaN 速度)。
  - Negative fixture: 实现若 clamp(把 1.4 裁成 1)⇒「报错」断言红 —— clamp 掩盖 3 的 bug,AC 明文禁止。

- **AC-1-31**: `v̂_world` 单位性与水平性(含反向用例)。
  - Given: 遍历 `MoveInput` 极角全周 × `‖MoveInput‖ ∈ {0.001, 0.5, 1}` × 相机 yaw 全周。
  - When: 走 F-1-8。
  - Then: `|‖v̂_world‖ − 1| ≤ YAW_BASIS_EPS`(回指 2 的常量符号,1 内**零**自有容差字面量)且 `v̂_world.y == 0`。
  - Edge cases: **反向用例(AC 明文)**:喂带仰角的相机 yaw(`f̂` 被构造为含 y 分量的违例输入,或经真实 rig 抬俯角),断言 `v̂_world.y` 仍为 0 —— 否定"直接用相机 `forward`"的缺陷写法;`‖MoveInput‖ = 0` ⇒ 不投影(走 Idle 分支,不断言单位性)。
  - Negative fixture: `v̂_world := my * rig.forward + mx * rig.right`(未水平化)⇒ 俯角非零时 y ≠ 0 红。

- **AC-1-35①②**: 基向量在 `v_target` 之前求值 + 每帧只取样一次。
  - Given: 注入一个计数/记录时序的 fake `ICameraRig`(每次 `YawBasis` getter 记一次访问)。
  - When: 跑一帧完整链。
  - Then: getter 访问次数 == **1**;求值序 `YawBasis → v̂_world → v_target`(探针记录的调用序断言)。
  - Edge cases: 同一帧内 fake rig 的 yaw 变化两次 ⇒ 1 只读到**首次取样**值(帧内恒定,承 2 的 `O-14` 对侧承诺)。
  - Negative fixture: 链上两处各取一次 `YawBasis` ⇒ 计数 == 2 红。

- **AC-1-35③**: 1 的调用点零对相机状态/变换写入(AST)。
  - Given: 1 的程序集全量源码经 AST 扫描。
  - When: 查找对 `ICameraRig` / `Camera` 的任何属性 setter 或方法调用写路径。
  - Then: **零命中**;`YawBasis` 读取为仅有的接口接触。
  - Edge cases: `rig.Camera.transform.SetPositionAndRotation(...)` 经接口逃逸(拿到 `Camera` 再写)⇒ 命中;故判据是**可达写闭包**,不是只看 `ICameraRig` 的 setter 名。
  - Negative fixture: 注入一行「把相机位置同步给玩家」的实现(真实世界里的常见错误)⇒ 红。

- **AC-1-35④**: 正交性断言(否定分开取)。
  - Given: fake 基集合:`yaw ∈ [0, 2π)` 全周采样 + 轻微 roll 违例(`right` 与 `cross(worldUp, fwd)` 不一致)。
  - When: F-1-8 求 `v̂_world`。
  - Then: 正常情形 `|dot(f̂, r̂)| ≤ EPS` 成立且投影 = 派生值;违例情形 1 侧断言 `right` 与派生值一致 ⇒ **红**(证明 1 用的是派生 `r̂` 而非接口透传的 `right`)。
  - Edge cases: yaw = 0 / π/2 / π / 回绕点(承 2 的 `AC-2-08` 同款采样密度)。
  - Negative fixture: 实现直接线性组合 `rig` 透传的 `right`(未经派生校验)⇒ 违例夹具不红即判据空转。

- **AC-1-09 附带面(1 不消费 `Look`)**:
  - Given: 1 的程序集 AST。
  - When: 查找 `Look` 动作引用或相机欧拉角读取。
  - Then: 零命中(朝向唯一来源 = F-1-4 面向 `v_horiz`,归 story 003)。

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `tests/unit/player_controller/move_basis_test.cs` — must exist and pass(单位性/水平性全周扫描 + 边界断言 + 时序 fake + AST 只读断言)

**Status**: [ ] Pending — story not yet implemented(真身落点预期 = `unity/Assets/Tests/EditMode/PlayerController/`,登记口径 = `tests/unit/player_controller/`)

---

## Dependencies

- Depends on: Story 001(`MoveInput` 注入缝所在的移动程序集与 asmdef 边界)+ **系统 2 Epic Story 002(`YawBasis` 构造成立 + `YAW_BASIS_EPS` 常量符号)—— 跨 Epic 硬前置**(`O-8`/`O-14` 的对侧)
- Unlocks: Story 003(乘数链消费 `v̂_world`;`AC-1-18` 求值序前段)/ Story 004(同一帧取样纪律的镜像)

---

## Completion Notes

**Completed**: _待实现_
**Criteria**: _待填_
**Deviations**: _待填_
**Test Evidence**: _待填_
**Code Review**: _待填_
**Manifest**: story Manifest Version 2026-09-21 = 当前 manifest(2026-09-21),无陈旧
