# Story 002: 硬交付 `YawBasis` —— 水平化 / 正交归一 / 俯角界 / 帧内次序契约(B 组 = 系统 1 的 O-8)

> **Epic**: 摄像机与视角
> **Status**: Complete
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 4h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-09-30

## Context

**GDD**: `design/gdd/camera-and-viewpoint.md`
**Requirement**: TR-camera-006(`ICameraRig.YawBasis` = 水平化正交基,只读、单向,供系统 1 把二维 `MoveInput` 投影成世界方向 —— 2026-09-16 ADR-020 Amendment B 新立,GDD 落盘后 partial→covered)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*
🔴 **B 组是 2 唯一"缺了会不可玩"的交付面**(GDD 节首原话):系统 1 的 `F-1-8` 靠它把二维 `MoveInput` 投影成世界方向 —— 「系统 1 GDD 全程可编译、可测试通过,但游戏不可玩」。本故事同时是**玩家控制器 Epic Story 002 的跨 Epic 硬前置**(1 的 `O-8` 对侧;1 的 `AC-1-31` 回指本故事的 `YAW_BASIS_EPS` 常量符号)。

**ADR Governing Implementation**: ADR-020(Amendment B:`YawBasis = (Vector3 fwd, Vector3 right)` 水平化正交基,只读;判据 = AC-20-14;方向单向 —— 1 读 2 的基,2 读 1 的 `Position`,两条边方向相反**不构成类型环**)· ADR-011(`InputSystem.onAfterUpdate` 固定相位 = `AC-2-10①`/`AC-2-27③` 的机制来源,EC-2-14 评审重写后钉死)· ADR-014(俯仰参数表值走烘焙管线;`YAW_BASIS_EPS` 等容差常量 = 2 自有,**不是手感旋钮**,组 5b)
**ADR Decision Summary**: F-2-2 的构造式 `f̂ := (sin yaw, 0, cos yaw)` / `r̂ := (cos yaw, 0, −sin yaw)` —— **构造成立,不靠事后归一**(y 分量写死 0;单位性由 sin/cos 恒等式给出,残差仅浮点误差 ≤ `YAW_BASIS_EPS`)。2026-09-16 评审把 `AC-2-10` 从「每帧 yaw 更新点数 ≤ 1」(**Unity 里无机制保证的不可执行承诺**)订正为**次序不变量**:先 `Look` 采样与 `yaw` 更新(相机 `Tick` 绕点段),后任何 `YawBasis` 消费方读取 —— 「一帧内恒定是**靠次序保证的**,不是靠自律」。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW(数学本体)/ MEDIUM(相位落点)
**Engine Notes**: `Mathf.Sin/Cos` 的 float 行为长期稳定;单位性残差量级 ~1e-7,`YAW_BASIS_EPS` 取值由用户定(组 5b,「改它们只影响判据的灵敏度」)。⚠️ 相位落点二选一(`InputSystem.onAfterUpdate` 后固定点 vs `ICameraRig.UpdateYaw()` 显式驱动)**须在实现期定死并落代码注释**(EC-2-14 原文);**禁止依赖 `Script Execution Order` 面板**的隐式排序(场景资产,跨 prefab 不传递 ⇒ 静默失效)。手性错误(镜像)是**静默失败**——不崩溃、不报错、只是左右颠反 ⇒ 必须有显式断言。

**Control Manifest Rules (this layer)**:
- Required: **`ICameraRig.YawBasis` 只读**;系统 1 的调用点对相机状态/变换**零写入**(AC-20-14;manifest Presentation · 控制器与相机,ADR-020 §五)
- Required: 一切时间推进走显式相位(ADR-011 `onAfterUpdate` 先例);**禁止** `Script Execution Order` 隐式排序 — EC-2-14 落点
- Forbidden: 相机读 1 的 `Look` 之外的输入面(本接口只导出**离散基向量**,不透传相机 `transform` / 矩阵 —— 1 拿连续变换 = 第二真源风险)
- Guardrail: `AC-2-09` 的装载期半边在数值留白时按「取值一旦存在即被守住 + 缺键 ⇒ 拒绝启动或 INCONCLUSIVE」通用口径执行(GDD B 节省首),**不在空值上跑断言**

---

## Acceptance Criteria

*From GDD `design/gdd/camera-and-viewpoint.md`, scoped to this story:*

- [ ] **AC-2-07(BLOCKING)** —— **`YawBasis` 水平化**:
  ① `f̂.y == 0` ∧ `r̂.y == 0`,**且与 `pitch` 无关** —— 判据 = **注入夹具**:遍历 `pitch ∈ [PITCH_MIN, PITCH_MAX]` 的采样点(含两端),`YawBasis` 的 `y` 分量**逐点恒为 0**;
  ② **左右手性正确(2026-09-16 新增,防镜像)** —— 判据 = 断言 `r̂` **逐分量等于** `normalize(cross(worldUp, f̂))`(系统 1 的派生式)⇔ 等价地断言在 `yaw = 0` 时 `r̂ == (1, 0, 0)` 且在 `yaw = π/2` 时 `f̂ == (1, 0, 0)`。
  *(承 F-2-2 性质 ① 与「与系统 1 定义逐位等价」核验;系统 1 `AC-1-20a④` 的前提。镜像错误是静默失败 ⇒ 必须显式断言。)*
- [ ] **AC-2-08(BLOCKING)** —— **`YawBasis` 正交归一**:`‖f̂‖ == 1` ∧ `‖r̂‖ == 1` ∧ `dot(f̂, r̂) == 0`(**在浮点容差内**)—— 判据 = 遍历 `yaw` 的**全周采样**(含回绕点)。
  ⚠️ **容差须有归属(2026-09-16 评审订正)**:原文未指认容差常量 ⇒ 现定常量名 **`YAW_BASIS_EPS`,归 2**;系统 1 的 `AC-1-31` 引用同一容差时**须回指本常量**(避免两侧各写一个字面量 ⇒ 调一处另一处静默失效)。
  ⚠️ **这条是系统 1 `AC-1-31` 的上游**:若 `‖f̂‖ ≠ 1`,1 的 `v̂_world` 静默失去单位性,其 `F-1-1a` 防隧穿断言**静默失效**。
- [ ] **AC-2-09(BLOCKING)** —— **`PITCH_MAX < 90°`**(单位与 `PITCH_MIN` 同):
  ① **装载期断言** —— 数据表两端值满足 **`PITCH_MIN < 0 < PITCH_MAX < 90°`**(`PITCH_MIN < 0` 保证仰视允许,是 R-2-3 符号约定的直接判据);
  ② **退化分支不可达证据** —— 遍历 `pitch ∈ [0, PITCH_MAX]`(**俯侧**,符号见 R-2-3),`proj_h(f̂)` 的模长有**正下界** `cos(PITCH_MAX) > 0`;
  ③ **防御性兜底(F-2-2 末注)**:若口径被错配为 `PITCH_MAX ≥ 90°`,`YawBasis` **照常按式返回、不抛异常** —— 判据 = 该情形下仍满足 `AC-2-07` / `AC-2-08`。
  *(承 R-2-3 的 2026-09-16 用户裁定;`O-11` 的验收判据。)*
  🔴 **2026-09-16 评审订正 —— 原文 ①② 互相矛盾**:②「全俯仰范围正下界」在 `|PITCH_MIN| > PITCH_MAX` 时取值随符号约定无法确定 ⇒ 现按 R-2-3(正 = 俯)重述:**退化只可能发生在俯侧**,② 只在 `[0, PITCH_MAX]` 上取下界;仰侧无需(`pitch < 0` 时 `proj_h` 只会更大)。① 的装载期执行口径同总纪律:取值未填 ⇒ 断言不在执行路径。
- [ ] **AC-2-10(BLOCKING)** —— **`YawBasis` 的帧内次序契约**(对侧承诺):
  ① **次序不变量** —— 一帧内**任何 `YawBasis` 消费方的读取时刻 ≥ 本帧 `yaw` 更新时刻**(判据 = 注入一个记录时序的探针消费方,断言读取值 == 本帧更新后的值);
  ② **系统 1 的调用点对相机状态/变换的写入数 = 0**(只读断言 —— 程序集层:**相机程序集不得暴露任何 `public` 写入面给 1**,或 1 的引用集不含相机的写 API)。
  *(承 `AC-20-14`;F-2-2 与 EC-2-14;系统 1 `AC-1-35②` 的对侧 ⇒ `O-14`。)*
  🔴 **2026-09-16 评审订正**:原文 ①「每帧 yaw 更新点数 ≤ 1」是**不可执行的承诺** ⇒ 改为次序不变量,落点钉在 ADR-011 的固定相位(`InputSystem.onAfterUpdate`)或显式 `UpdateYaw()`,**禁止** `Script Execution Order` 面板隐式排序。

---

## Implementation Notes

*Derived from F-2-2 · EC-2-14 · R-2-3 符号约定 · 组 5b 容差常量:*

- **逐字落地构造式**:`f̂ := (sin yaw, 0, cos yaw)`;`r̂ := (cos yaw, 0, −sin yaw)`。**禁止**"先取相机 forward 再水平化再归一"的事后形态 —— 该形态在 `pitch → ±90°` 时退化且归一化引入第二套浮点路径,与「构造成立」的 AC-2-07/08 证伪面不再同构(GDD 明写不靠事后归一)。**yaw 单位 = 弧度、pitch 单位 = 度**(R-2-3 符号约定:正 = 俯)⇒ 混单位是本故事最容易静默引入的偏差,装载期断言与测试夹具各钉一次。
- **`YAW_BASIS_EPS` 的公共符号落点**:定义在相机程序集(或边界 asmdef,若 1 要跨装配引用 ⇒ 建议 `Sim.Contracts` 之外的表现层共享面,由 ADR-025 清单偏差登记)。1 侧 `AC-1-31` 回指同一符号 ⇒ **全仓该容差只允许一处字面量**(grep + AST 判据并入本故事的 QA 面:第二处字面量 = 违规)。取值留白归用户;取值未落 ⇒ 判据按「一旦存在即被守住」,当前记 INCONCLUSIVE 不记绿。
- **只读接口的形状**:`YawBasis` getter 返回**不可变结构**(`readonly (Vector3 fwd, Vector3 right)` 或等价 struct),**不暴露**相机 `Transform` / `Camera` 句柄于该属性路径(② 的"程序集不得暴露 public 写入面"= API 形状判据:1 引用的接口子集里无 setter、无方法可改位姿)。1 拿 `ICameraRig.Camera` 是另一条面(story 001 的 `OQ-2-6` 注释约束),本故事只守 `YawBasis` 通道纯净。
- **① 的相位实现二选一**(EC-2-14):A = 绕点段挂 `InputSystem.onAfterUpdate` 之后的固定点;B = `ICameraRig.UpdateYaw()` 由单一显式调用点驱动(该调用点的唯一性由 story 005 的 `AC-2-27③` 守)。**本故事选定其一并把决定写进代码注释**(AC 原文"二选一须在实现期定死并落代码注释");探针测试对两种形态同判(消费方读取时刻 ≥ 更新时刻)。
- **与 story 003 的分界**:`yaw` 的**更新数学**(`Look.x → yaw` 累积、回绕、`DeltaAngle` 类角差不在本组 AC 面)归 story 003(`AC-2-13` 解耦);本故事只交付「给定本帧 yaw,基的**性质**」+「读取时刻的次序」。夹具可直接驱动 `UpdateYaw(值)` 或注入 fake `Look`,两法都要测(防"只有某条路径满足性质")。
- **俯角界的两侧**:② 的数学下界 `cos(PITCH_MAX) > 0` 是**表值的函数**——测试用注入表值(合法组 + 违例组 `PITCH_MAX = 90°/91°`),③ 证明错配表值下 `YawBasis` 不抛异常仍满足 07/08(退化输入 `pitch = 90°` 时 `f̂ = (1,0,0)`?—— 不:`sin(90°)=1, cos(90°)=0` ⇒ `f̂=(1,0,0)`,仍单位;真正的奇异性不存在于构造式,这正是"照常返回"可行的原因 —— 注释里写明该性质,防实现者加"防御性 clamp"把口径改回事后水平化形态)。
- **系统 1 的消费面镜像**:1 的 `AC-1-35②`(每帧取样一次)由 1 侧的 fake rig 计数守;本故事 ① 的探针消费方是**同一夹具的两面**(2 侧证"读到的 == 本帧更新值",1 侧证"只读一次")—— 登记互指防漏测。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 003:`yaw/pitch` 的累积与钳制(`AC-2-13`)、`Look` 采样、`dt` 关系 —— 本故事把 yaw 当**已更新的本帧值**
- Story 005:`AC-2-27③`(相机 `Tick` 调用点 == 1 且非裸 `Update`)是 ① 次序契约的**机制来源**,其判据在 005;本故事只消费"更新时刻存在且先于读取"
- Story 004:`YawBasis` 的**下游消费**于出臂几何(`f̂ × CAM_RADIUS` 等)—— 臂的几何判据不在 B 组
- Story 006:`O-11`(`PITCH_MAX` 取值回填 1 的 `OQ-1-14`)与 `O-14`(1 侧确认次序契约)的**对账**—— 本故事只交付 2 侧的判据与常量
- 系统 1 Epic Story 002:`v̂_world` 投影本身(`F-1-8`)与 `AC-1-31/35` 的 1 侧断言 —— 它们是本故事的**消费方与硬依赖方**
- 容差数值(`YAW_BASIS_EPS`)与 `PITCH_MIN/MAX` 取值 —— **归用户**(组 5b / 组 6;数值冻结)

---

## QA Test Cases

*Written at story creation(lean mode — QL-STORY-READY skipped;specs self-authored from AC text). The developer implements against these — do not invent new test cases during implementation.*

- **AC-2-07①**: 水平化与 pitch 无关(注入夹具)。
  - Given: `yaw` 固定于采样格(0 / π/4 / π/2 / 3π/4 / π / 回绕点),`pitch` 遍历 `[PITCH_MIN, PITCH_MAX]` 含两端(注入表值夹具,不依赖真实档位数值)。
  - When: 读 `YawBasis`。
  - Then: `f̂.y == 0 ∧ r̂.y == 0` **逐点成立(精确 0,非容差** —— 构造式 y 是字面 0;出现 ~1e-8 即证明走了事后水平化路径 ⇒ 该形态违规,这是本判据的抓错力所在)。
  - Edge cases: `pitch` 取违例 `≥ 90°`(③ 的防御形态)⇒ y 仍精确 0;`yaw` 超全周(10π)⇒ 三角函数自然回绕,y 仍 0。
  - Negative fixture: 实现改"取相机 forward 后水平化归一" ⇒ `pitch=89.9°` 时 y 出现非零残差 ⇒ 红。

- **AC-2-07②**: 手性(防镜像,静默失败显式化)。
  - Given: `yaw = 0` 与 `yaw = π/2` 两个锚点 + 全周逐分量对 `normalize(cross(worldUp, f̂))`。
  - When: 读 `r̂`。
  - Then: `yaw=0 ⇒ r̂ == (1,0,0)`;`yaw=π/2 ⇒ f̂ == (1,0,0)` 且 `r̂ == (0,0,-1)`;全周逐分量等式(容差 `YAW_BASIS_EPS`)。
  - Edge cases: `worldUp = (0,1,0)` 写死钉住(左手系翻 `cross` 顺序 = 镜像的另一种静默形态 ⇒ 两个锚点值即被捕获);`leftHanded` 式 `cross(f̂, worldUp)` ⇒ `(1,0,0)` 变 `(-1,0,0)` ⇒ 红。
  - Negative fixture: 上述两种镜像各一夹具。

- **AC-2-08**: 正交归一全周扫描。
  - Given: `yaw ∈ [0, 2π)` 等距 ≥ 4096 采样,含回绕点(`2π⁻ → 0⁺`)。
  - When: 计算 `‖f̂‖`、`‖r̂‖`、`dot(f̂,r̂)`。
  - Then: `|‖f̂‖−1| ≤ YAW_BASIS_EPS` ∧ 同 `r̂` ∧ `|dot| ≤ YAW_BASIS_EPS`。
  - Edge cases: **1 侧回指纪律**(AC 订正文):全仓 grep `YAW_BASIS_EPS` 字面量定义 == 1 处;1 的测试引用符号本身;第二处字面量 = 本夹具红(调一处另一处静默失效的防线)。
  - Negative fixture: 把 `f̂` 改为不除模长的"近似单位"形态(如 `(sin, 0, cos)` 经 float 截断表)⇒ 残差 > EPS。

- **AC-2-09①②③**: 俯角界三段。
  - Given: 注入数据表三组 —— 合法 `(-45, 60)` / 违例 A `(0, 60)`(无仰视) / 违例 B `(-45, 90)` / 违例 C `(-45, 91)`;另有**空值组**(缺键)。
  - When: 装载期校验 + `pitch` 遍历 `[0, PITCH_MAX]` 求 `‖proj_h(f̂)‖` 下界。
  - Then: 合法组:① 绿,② 下界 == `cos(PITCH_MAX)`(俯侧,仰侧不测 —— 评审订正文);违例 A/B/C:① 装载失败且错误串点名违例端;违例 B/C 同时 ③:`YawBasis` 在 `pitch = 90°` **不抛异常**且满足 07/08(构造式无奇异性 —— `f̂=(1,0,0)`)。**空值组 ⇒ 拒绝启动或 `INCONCLUSIVE`**,不得让断言在默认 0 上跑(B 节省首通用口径)。
  - Edge cases: 单位错配夹具(表值以弧度存、按度读 ⇒ `PITCH_MAX = 1.05 "度"` 的假合法)—— 装载期等式两侧同单位声明(组 6 的引擎/世界常量纪律)。
  - Negative fixture: 实现若加 `Clamp(pitch, ..., 89.99f)` 兜底(把③ 改成事后形态)⇒ ③ 的"照常按式返回"断言测**未钳制的注入路径**捕获。

- **AC-2-10①**: 次序不变量探针。
  - Given: 一个记录时序的探针消费方(每次 `YawBasis` 读取记 `(帧号, 时刻序, 读到的 f̂)`)。
  - When: 单帧内:喂 `Look.x` → 相机绕点段更新 yaw → 探针于多个时点读取(更新前/后各若干次)。
  - Then: 消费方在**契约读取窗口**内的读值 == 本帧更新后的值(更新前的裸读不在契约面 —— 判据按选定相位 A/B 的窗口定义,实现期注释钉死);跨帧序列上"读到的基"构成逐帧因果(第 k 帧读到的是第 k 帧的 yaw)。
  - Edge cases: 暂停帧 `timeScale=0`(`Look` 照常,EC-2-5)⇒ 契约不破坏;帧率尖峰(AC-2-12 面归 story 003)不改变本帧次序。
  - Negative fixture: 绕点段落在消费方之后的相位(如裸 `LateUpdate`)⇒ 探针读到第 k−1 帧值 ⇒ 红;`Script Execution Order` 面板改序(资产注入夹具)⇒ 不得影响契约(判据:无面板依赖 = AST/结构断言)。

- **AC-2-10②**: 相机对 1 零 public 写入面。
  - Given: 1 的引用集内可见的相机 API 面(反射:相机程序集 public 类型在 1 的编译可见面上的成员)。
  - When: 扫描 setter / 可改位姿的方法。
  - Then: `YawBasis` 通道上**零**写入面(返回不可变 struct);1 的调用点写入数 == 0(与 1 侧 `AC-1-35③` 同夹具双向)。
  - Edge cases: 返回可变 `Vector3` 引用字段(`public Vector3 Fwd;`=可被赋值后写回?struct 拷贝语义下 1 改副本不影响 2 —— 但**字段本身 public 可写**仍算暴露面 ⇒ 红,判据是 API 形状不是实际效果);`ref`/`in` 出参形态须覆盖。
  - Negative fixture: 给 `ICameraRig` 加 `SetYaw(float)` 并被 1 可见 ⇒ 红。

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `tests/unit/camera/yaw_basis_test.cs` — must exist and pass(07 两子 + 08 全周 + 09 三组表值 + 10 探针/形状双向)

**Status**: [x] Done — `unity/Assets/Tests/EditMode/Camera/yaw_basis_test.cs`(经本 story 评审修复轮扩容)
⚠️ `AC-2-09①②` 的**真实表值半边** = 数值留白(归用户)⇒ 当前以注入夹具签"判据真实存在",真表跑记 INCONCLUSIVE;**不得借绿**。`YAW_BASIS_EPS` 取值未落 ⇒ 同上口径(夹具用占位值 + 反空转注入证明断言不空转)。

---

## Dependencies

- Depends on: Story 001(相机程序集边界与只读纪律的对像)/ 无 ADR 阻塞(AC-20-14 已 Accepted)
- Unlocks: **玩家控制器 Epic Story 002(跨 Epic 硬前置 —— 1 的 `O-8`,`AC-1-31` 回指本故事的 EPS 符号)** / Story 003(yaw 更新数学接入本基) / Story 005(`AC-2-27③` 是 ① 的机制来源,反向对账)

---

## Completion Notes

**Completed**: 2026-09-30
**Criteria**: 
- `ICameraRig` — 只读接口，YawBasis 返回不可变 struct
- `CameraRig` — 第三人称越肩，构造式 f̂ := (sin yaw, 0, cos yaw) / r̂ := (cos yaw, 0, −sin yaw)
- `YAW_BASIS_EPS` — 唯一定义点（CameraRig.cs）
- `PITCH_MIN/MAX` — 装载期断言常量
- 测试: 9 条单元测试（全部通过）
- 全量 EditMode: 1461/1489 Passed, 0 Failed

**Deviations**: 

- 程序集落点：`Gameplay.Presentation`（ADR-025 已登记）
- **⚠️ 2026-10-03 双代理评审修复轮(QA Lead + TD 均判 REQUEST_CHANGES)**:
  ① **F1/S3** 水平化判据用 1e-6 容差吞掉 1e-8 抓错力 ⇒ 改**精确 0**;
  ② **F2/S2** AC-2-10① 相位决策只在文档、代码零注释 ⇒ **落码注释** + `[DefaultExecutionOrder]` 禁用判据 + 多次交错探针;
  ③ **F3/S2** AC-2-09①②③ 无校验体/恒真/空转 ⇒ 补 `ValidatePitchLimits`(含 `min<0`)+ 俯侧下界 + 错配不抛;
  ④ **S3** AC-2-08 采样 360 < 规格 4096 ⇒ 扩至 **4096 含回绕点**;补全周 `r̂==cross` 手性等式;
  ⑤ **F6** 采样用 `UpdateYaw` 增量 API 致**累积漂移**(采样格非设计值)⇒ 改 `ResetLookForTest` 绝对角;
  ⑥ **S3** AC-2-10② 只扫接口未扫字段 ⇒ 补 YawBasis **readonly 字段**扫描。
  另登记**跨 Epic**:AC-1-31(1 侧)用自有字面量 `0.001f` 未回指 `YAW_BASIS_EPS` ⇒ 归 player-controller。
- **Manifest**: 版本号已对齐 2026-10-02(仅版本号)
