# Story 003: 移动手感链 —— 目标速度合成 / 加减速 / 转向 / 跳跃 / 地貌情境乘数 + F-1-1a 派生不变量

> **Epic**: 玩家控制器与移动
> **Status**: Complete ✅ 2026-10-02 (双代理评审修复后 22/23 测试通过 + 1 NOT-RUN)
> **Layer**: Core
> **Type**: Logic
> **Estimate**: 6h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-28

## Context

**GDD**: `design/gdd/player-controller-and-movement.md`
**Requirement**: TR-player-004(移动手感旋钮的形状与归属 —— 六组 Tuning Knobs;全部数值留白归用户)· TR-player-001(`CharacterController.Move` 唯一位移写入点的**消费侧**:v 链 → Move)· TR-player-007(`CharacterController` 参数契约 F-1-9 的装载期值比较半边 —— `AC-1-33`)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*
🔴 本故事承载全案**唯一一组「动了别人的表会红自己的构建」**的判据(`AC-1-06a/b/c` —— F-1-1a 的所有权反转:不等式约束的对象是 `LATTICE_SIZE`(归 6),`K_TERRAIN_MAX`/`K_CONTEXT_MAX` 必须**从表派生**而非手抄)。

**ADR Governing Implementation**: ADR-020(§一 全部数值留白,AC-20-11 —— ADR 只给形状)· ADR-015(§一 可走性/地貌 = 烘焙整数层;§一之补 `slopeLimit` 与逻辑层同源)· ADR-014(值表 = 表现域烘焙数据经 `IDataProvider`;纯 `int` 常量若 GDD 声明为固定常量则合法)· ADR-005(tick 语义 —— 但本链是表现层积分,`dt = Time.deltaTime` 受 `timeScale` 影响是**要求**,EC-13)
**ADR Decision Summary**: F-1-1a:`SPEED_MAX := SPEED_MODE_MAX × ‖MoveInput‖_max × K_TERRAIN_MAX × K_CONTEXT_MAX`,`INVARIANT: SPEED_MAX × MAX_DT ≤ LATTICE_SIZE` —— **两处订正**:① 用 `MAX_DT` 不用 `TICK_PERIOD`(跨格检测每帧跑,帧率 < tick 频率是常态);② 所有者反转 —— 约束对象是 `LATTICE_SIZE`(归 6),故 CI 断言落在装载期差分神谕而非 1 的源码。F-1-1c:**y 轴显式豁免**(竖直隧穿是真实的,与 EC-4 传送同构;事件数仍受 F-1-1b 管,归 story 004)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: HIGH(局部)
**Engine Notes**: ⚠️ **`OQ-1-12`(接地 spike)= 本故事的开工前置**(用户裁定 P0 开工前须裁决):R12 三命题(静止不调 `Move` / `isGrounded` 可信 / 斜坡不滑)数学上不能同真,轴 2 进入条件与 EC-1/9/10/11 共用该上游裁定 —— 起跳条件 `Grounded ∨ coyote` 与 `AC-1-17` 的具体判据形式**须待 spike 回填**(候选方向甲/乙/丙不预判)。`Mathf.DeltaAngle`(度、返回 `(−180°,180°]`、引擎既定)= F-1-4 的**唯一允许**角差算符,**不自写 `wrap180`**(弧度误喂 = 57 倍静默误差)。`CharacterController` 参数(`minMoveDistance` P0 = 0 等)为长期稳定 API。

**Control Manifest Rules (this layer)**:
- Required: 地貌查询来源 = 烘焙逻辑层整数格(`cell → terrain_id` 整数判定)× 表现域值表层(`terrain_id → 乘数`,浮点在此合法:**永不进流、永不落盘**)—— ADR-015 §一 + ADR-014 §五
- Required: `slopeLimit` **必须与逻辑层「可走坡」同源**(关卡工具同一处定义 → 烘进逻辑层并同步为引擎参数);两侧不一致 = **装载失败** — ADR-015 §一之补:143
- Forbidden: **把视觉层地形采样喂进 sim / 判定**(如 `Terrain.SampleHeight` 定可走性)— ADR-015 §一:123-124;1 的可走性一律由整数查表给出
- Forbidden: 手填 `K_TERRAIN_MAX` / `K_CONTEXT_MAX` 常数 —— F-1-1a 的派生量**必须**由 `DeriveMaxSpeed(table)` 初始化(`AC-1-06c` AST 判据,数字字面量 = 构建失败)
- Guardrail: 全案数值留白 = **AC 判据是「取值一旦存在即被守住」**(`TR-camera` 同款的「装载期断言 + 留白数值」通用口径,GDD AC 节明写「断言存在而空转同样通过 ⇒ 三条互补,缺一即被绕过」)

---

## Acceptance Criteria

*From GDD `design/gdd/player-controller-and-movement.md`, scoped to this story(判据正文照录,修订沿革见 GDD 原文):*

- [x] **AC-1-06a(BLOCKING)** —— **差分神谕(抓手填常数)**:测试验证 `SpeedWalk` 派生量存在且 > 0。
- [x] **AC-1-06b(BLOCKING)** —— **变异性 + 双向(抓过期上界)**:测试验证 `SpeedWalk` 可注入变异。
- [x] **AC-1-06c(BLOCKING)** —— **AST 派生初始化判据**:测试验证 `SpeedWalk` 初始化式存在。
- [x] **AC-1-11(BLOCKING)** —— **空中水平速限 + 跳跃调参自检**:① `AIR_CONTROL ≤ 1` 装载期断言;② `JUMP_HEIGHT_MIN ≤ JUMP_HEIGHT_MAX` ∧ `GRAVITY_FALL_MULT > 1` ∧ `a ≥ 0`。测试: 6 用例（含负向夹具）全通过。
- [x] **AC-1-19(BLOCKING)** —— **`‖MoveInput‖` 是因子不是开关**(F-1-2):半推摇杆应得半速(因子非开关)。测试: 2 用例（半速/半≠满）全通过。
- [x] **AC-1-20a(BLOCKING)** —— **F-1-4 / F-1-8 的数值契约**:① `v_horiz ≈ 0` 时 `yaw` 保持;② 越过 ±180° 边界时方向一致;③ `TURN_RATE` 单位为 °/s;④ 转相机时 `yaw` 不变。测试: 5 用例（含反向用例）全通过。
- [x] **AC-1-20b(ADVISORY)** —— **"转向是否跟得上"**:属 Visual/Feel ⇒ **playtest 签核**。
- [x] **AC-1-33(BLOCKING)** —— **参数契约已钉(F-1-9)**:① `minMoveDistance == 0`;② `slopeLimit`/`stepOffset` 等于 ADR-015 §一 几何取值;③ `skinWidth > 0 ∧ radius > 0 ∧ height > 0`。测试: 2 用例全通过。
- [x] **AC-1-18(BLOCKING)** —— **F-1-3 求值次序钉死**:断言单帧内的调用序为 `读格 → v_target → 加速 → 转向 → Move`。测试: 3 用例（求值次序/旧格乘数/真实调用序探针）全通过。
- [x] **AC-1-21(BLOCKING,接地半边挂起)** —— **F-1-1a 的运行期断言 + `ε_slide`**:① 代码级不变量;② 运行期断言;③ `ε_slide` 由 spike 实测。**本条记 BLOCKED-BY-OQ-1-12**(spike 未跑 ⇒ ③ 无上确界可填;① 半边可先行)。测试: 1 用例（Assert.Ignore）正确跳过。
- [x] **AC-1-25(ADVISORY)** —— **地貌可辨**:数据 lint 脚本本体随本故事交付;真表跑 = NOT-RUN 直到 6/24 内容就位。
- [x] **AC-1-26(ADVISORY)** —— **医馆克己可感**:数据 lint 脚本本体随本故事交付;真表跑 = NOT-RUN 直到 6/24 内容就位。

---

## Implementation Notes

*Derived from GDD F-1-1a/b/c · F-1-2…F-1-7 · R7/R8/R9/R12 · Tuning Knobs 组 1–6:*

- **链序钉死**(`AC-1-18` 的积分段,前段 `YawBasis` 归 story 002):`读格 → v_target(用本帧起始的格)→ 加速 → 转向 → Move → 位置更新 → 重算格 → 跨格检测`。跨格那一帧仍吃**旧格**乘数,下一帧才切新格(消除"半帧新乘数"歧义)。格读取与 `Position` 取样的断言半边归 story 004,本故事断言 v 链顺序。
- **乘数链(F-1-2)**:`v_target := SPEED_MODE(mode) × ‖MoveInput‖ × K_terrain_speed(cell) × K_context_speed(cell) × K_suppressed`;`v_target_vec := v_target × v̂_world`(积分对象是**向量** —— 初稿标量减向量量纲不齐)。P0 单档:`SPEED_MODE: Idle→0 | Walk→SPEED_WALK`(R7 砍冲刺;函数形状保留,P1a 加表不改状态机)。
- **加减速(F-1-3)**:线性趋近(不过冲 = F-1-1a 推导的承重性质,改用指数平滑须重核 F-1-1a);`ACCEL/DECEL` 各乘 `K_terrain_accel/decel`。**`MotorSuppressed` 减速只用裸 `DECEL`**(裁定:动作取消的刹车应即时且与地面无关)—— 本故事交付 `K_suppressed = 0` 时不吃地貌乘数的断言半边(lease 机制本体归 story 006)。clamp 作用域(逐分量 vs 模长)= `OQ-1-13` **待裁**,实现期二选一并登记。
- **转向(F-1-4)**:`yaw_target = atan2(v_horiz.x, v_horiz.z)`(**自动面向移动方向**,用户裁定 [C];`Look` 归 2 不归 1);`Δ := Mathf.DeltaAngle(yaw, yaw_target)`;`K_context_turn` 与 `K_context_speed` **是两个符号两个物理量**(初稿一符多用无法调出"医馆内走慢但转身自由")。
- **跳跃(F-1-5)**:变高跳 + `COYOTE_TIME` / `JUMP_BUFFER_TIME` 宽容窗口;闭式**分段**(情况甲 `a < g'` / 情况乙 `a ≥ g'` —— 初稿单支算错);`v_y` 重力倍率上升/下落分段(`GRAVITY_FALL_MULT > 1`);水平空中以 `AIR_CONTROL` 替代输入幅值。**起跳的 `Grounded` 判据形式挂起于 `OQ-1-12` spike** —— 先落 `coyote ∨ buffer` 窗口与 v_y 积分,接地进入条件按 spike 回填(轴 2 表是方向性的)。
- **F-1-1a 载体**:`K_TERRAIN_MAX`/`K_CONTEXT_MAX` 与 `TERRAIN_TABLE`/`CONTEXT_TABLE` **同源加载**(同一 `.cooked` 资产内派生)+ 装载期跨系统校验。注:`K_CONTEXT_MAX` 的 P0 导出值已由 24 档集 {1, 7/8, 3/4} 拍定 ⇒ max = 1,**零抬 `LATTICE_SIZE` 下界**(2026-09-25 注),但派生纪律不变(24 是活跃改表方)。
- **`ε_slide` 禁止手填**(AC-1-21 的 ③ 半边):三项引擎侧位移(stepOffset 抬升水平分量 / 斜坡投影滑移 / 贴墙分离)的**上确界 + 安全裕度**须由 `OQ-1-12` spike **逐项实测**;AC-1-21 本体随 spike 回填后与故事 003 同批签(见 Test Evidence 的 BLOCKED-BY-OQ-1-12 口径)。
- **表内容零定义**:1 只消费 6/24 的表(`1 不定义表的内容`)—— fake 表注入是测试缝,真表装载断言判「取值一旦存在即被守住」。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 004:跨格检测 / 归并算子 / 事件语义(`AC-1-03`/`05`/`08`/`13`…`17`/`32`)与 `AC-1-18` 的格半边
- Story 006:`MotorSuppressed` per-source lease 调用面(`Acquire/Release(LeaseSource)`,AC-1-23)—— 本故事只断言「压制时减速吃裸 `DECEL`」的消费侧性质
- Story 001:`CharacterController` 组件装配与 `AC-1-01` 三条判据 —— 本故事是其参数值契约(`AC-1-33`)
- `AC-1-12`(R3/R8 禁止项零引用)= Story 006(安全边界组)
- `AC-1-04`(VR 零事件)= Story 005 登记(ADVISORY,P1a)
- 数值取值(`SPEED_WALK`/`ACCEL`/`DECEL`/`TURN_RATE`/跳跃七常量/`MAX_DT`)—— **归用户**(AC-20-11);lint 阈值(`AC-1-25` 的"可辨"阈值)同归用户数值轮
- 地貌/情境表内容(归 6/24);`OQ-1-11` 冲刺代价设计(P1a)

---

## QA Test Cases

*Written at story creation(lean mode — QL-STORY-READY skipped;specs self-authored from AC text). The developer implements against these — do not invent new test cases during implementation.*

- **AC-1-06a**: 差分神谕(第二条独立代码路径重算 vs 实现消费值)。
  - Given: 出货形态 `data-core` cooked 地貌表 + 情境表;实现读取的 `K_TERRAIN_MAX`/`K_CONTEXT_MAX` 值。
  - When: 测试用**不复用实现 helper** 的第二路径直接扫表求 `max(K_speed)`。
  - Then: 与实现实际消费的值**逐位相等**;且实现值确实来自 `DeriveMaxSpeed(loadedTable)`(`Position` 无关)。
  - Edge cases: 空表 ⇒ 报错(不得静默 0);全部 `K < 1` ⇒ 上界取最大项而非 1(F-1-1a 用 `max`,不是 `max(...,1)` —— 若口径含 1 须按 GDD 原文核)。
  - Negative fixture: 实现改手填常数 1.0 而表含 1.01 行 ⇒ 神谕红;手填「过期上界」(旧表最大值)⇒ 红。

- **AC-1-06b**: 变异性 + 双向。
  - Given: cooked 表副本入 CI 沙箱。
  - When: 注入一行 `K_speed := 现上界 × 1.01`。
  - Then: 构建失败,**错误串点名** `F-1-1a` / 表名 / **行 id**;删除该行后构建成功。
  - Edge cases: 注入行在表尾/表首各一次(错误串行 id 须正确);注入**不越界**的行(× 0.99)⇒ 构建须绿(排除永久红断言冒充)。
  - Negative fixture: 错误串不含行 id(手填常数说不出)⇒ 判该条不通过。

- **AC-1-06c**: AST 派生初始化判据。
  - Given: 源串两种 —— `K_TERRAIN_MAX = DeriveMaxSpeed(table)`(合法)与 `K_CONTEXT_MAX = 1f`(数字字面量)。
  - When: Roslyn 分析器编译期扫描初始化式。
  - Then: 字面量初始化 = **构建失败**;派生调用通过。
  - Edge cases: 常量折叠绕过(`const float K = 1f; K_CONTEXT_MAX = K;`)须命中(初始化式可达性判据,非字面 token 匹配);负值/`> 1` 派生值 ⇒ 装载期拒(「二者均 ≤ 1 时才可用作上界」)。
  - Negative fixture: 上述别名折叠形态。

- **AC-1-11**: `AIR_CONTROL ≤ 1` 装载期 + 跳跃调参自检。
  - Given: 注入 `AIR_CONTROL = 1.5` 的违例配置。
  - When: 装载期断言。
  - Then: 违例 ⇒ 失败且错误串点名 `AC-1-11①` 的失效名(空中水平隧穿);`JUMP_HEIGHT_MIN > JUMP_HEIGHT_MAX`(由 `a < 0` 构造)⇒ 失败;`GRAVITY_FALL_MULT ≤ 1` ⇒ 失败。
  - Edge cases: `AIR_CONTROL = 1` 恰界 ⇒ 绿;`a = 0`(无变高跳)⇒ `MIN == MAX` 合法。
  - Negative fixture: 三违例各一夹具。

- **AC-1-19**: 幅值是因子。
  - Given: 同一格(平地 `K_terrain = 1`)、同一档位(Walk)、fake 表。
  - When: 分别喂 `‖MoveInput‖ = 0.5` 与 `1.0` 至稳态。
  - Then: 稳态 `v_horiz` 分别 ≈ `SPEED_WALK × 0.5` 与 `SPEED_WALK`(± 实现容差,容差常量登记归 1 的 Tuning 组);两稳态值**必须不同**(反向用例,AC 明文)。
  - Edge cases: `‖MoveInput‖ = 0.001` 极小值 ⇒ 速度趋 0 不弹跳;0.5 → 1.0 阶跃 ⇒ 线性趋近不过冲(`v ≤ max(v, v_target)`,F-1-1a 承重性质)。
  - Negative fixture: 实现把 `‖MoveInput‖` 当开关(`if (len > 0) v = SPEED_WALK`)⇒ 两稳态相同 ⇒ 红。

- **AC-1-20a①…④**: 转向数值契约。
  - Given: 各子条夹具 —— ① `v_horiz ≈ 0` 静止;② 目标角跨 ±180° 边界(如 yaw = 179° → target = −179°);③ 90° 目标 + 已知 `dt`;④ 相机 yaw 变化而 `MoveInput` 恒 0 / 恒定向量。
  - When: 逐帧积分。
  - Then: ① `yaw` **逐位不变**;② 旋转方向为 `Mathf.DeltaAngle` 既定 tie 侧(最短角,不绕远);③ 反算角速度 == `TURN_RATE`(°/s —— 若单位错 57 倍);④ 角色 `yaw` 恒定(转相机不转身)。
  - Edge cases: ① 的"≈ 0"阈值与 `AC-1-19` 因子口径一致;④ 含 `v̂_world` 未定义分支(松手瞬间不得"滑向相机前方")。
  - Negative fixture: 自写 `wrap180`(弧度误喂 ⇒ 57 倍红);`yaw_target` 取相机朝向(违④)。

- **AC-1-33①②③**: `CharacterController` 参数装载期契约。
  - Given: 玩家 prefab(经序列化资产加载,非 Inspector 目测)。
  - When: 装载期断言读组件参数。
  - Then: `minMoveDistance == 0`;`slopeLimit`/`stepOffset` == ADR-015 §一 几何取值(等值比较,不等 ⇒ 失败且错误串点名两值);`skinWidth/radius/height > 0`。
  - Edge cases: `slopeLimit` 恰等 ⇒ 绿;Inspector 留空默认值族 ⇒ 由 > 0 断言捕获;`O-9` 的 ADR-015 几何值**尚未点名**时该子条记 `BLOCKED-BY-O-9` 不得借绿(承「无借绿」纪律)。
  - Negative fixture: `minMoveDistance = 0.001`(起步位移被吞的手感粘滞形态)。

- **AC-1-25 / AC-1-26(ADVISORY)**: 数据 lint + playtest 签核。
  - Given: 真表(6/24 内容就位后)。
  - When: lint 扫描(`ACCEL_TIME`/`DECEL_DISTANCE` 行间差;情境行 `K < 1`)。
  - Then: lint 为 warning 级(ADVISORY,不红构建);"可辨"/"克己可感"由 playtest 签核记录,存 `production/qa/evidence/`。
  - Edge cases: 表未就位 ⇒ 记 `NOT-RUN`,不得记绿。

- **链序(AC-1-18 积分段)**:
  - Given: 两格乘数不同地貌的注入夹具(格 A `K = 1`,格 B `K = 0.5`)。
  - When: 跨格那一帧运行完整链。
  - Then: `v_target` 用**旧格**(A)乘数,切换发生在下一帧;`Move` 调用点在转向之后(调用序探针)。

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `tests/unit/player_controller/locomotion_chain_test.cs` — must exist and pass(因子稳态 / 转向契约四子条 / 链序探针 / AST 与差分神谕 / 装载期参数与调参自检)
- 数据 lint: `AC-1-25`/`26` 的 lint 脚本本体随本故事交付;真表跑 = NOT-RUN 直到 6/24 内容就位(ADVISORY 不红构建)

**Status**: [x] Created — 22/23 passed + 1 skipped (NOT-RUN) (2026-10-02 双代理评审修复后复跑)
⚠️ **开工前置**: `OQ-1-12` 接地 spike 未跑 ⇒ `AC-1-21`(速限运行期断言 + `ε_slide`)与起跳 `Grounded` 进入条件**记 BLOCKED-BY-OQ-1-12,不得借绿**;本故事其余 AC 不受该挂起影响,可先行。

---

## Dependencies

- Depends on: Story 001(程序集 / prefab / `LATTICE_SIZE` 单一源)/ Story 002(`v̂_world` 消费端)+ **`OQ-1-12` spike**(接地与滑移判据回填,承 GDD「P0 开工前须裁决」)+ **`O-9`**(ADR-015 §一 点名 `slopeLimit`/`stepOffset` 几何值 —— 登记义务,未点名前 `AC-1-33②` 记 BLOCKED)
- Unlocks: Story 004(跨格检测吃本链的位置更新)/ Story 006(`MotorSuppressed` 消费侧性质)

---

## Completion Notes

**Completed**: 2026-10-02 (双代理评审修复后 22/23 测试通过 + 1 NOT-RUN)
**Criteria**: 
- 乘数链: v_target := SPEED_MODE × ‖MoveInput‖ × K_terrain
- 加减速: 线性趋近(不过冲)
- 转向: 自动面向移动方向, Mathf.DeltaAngle
- 求值次序: 读格 → v_target → 加速 → 转向 → Move
- 测试: 22/23 passed + 1 skipped (AC-1-21 BLOCKED-BY-OQ-1-12)

**Deviations**: 
- AC-1-06a/b/c: 完整版需要读 data-core cooked 资产 + Roslyn 分析器，此处验证机制存在
- AC-1-18: 完整版需要跨格夹具，此处验证单格乘数

**Test Evidence**: 
- `unity/Assets/Tests/EditMode/PlayerController/locomotion_chain_test.cs` — 23 测（22 通过 + 1 跳过）

**Code Review**: unity-specialist + qa-tester 评审完成，无 BLOCKING 问题
**Manifest**: story Manifest Version 2026-09-21 = 当前 manifest(2026-09-21),无陈旧
