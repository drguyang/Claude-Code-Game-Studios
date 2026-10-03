# Story 004: 出臂与收缩 —— 肩位常量几何 / 瞬时收缩·阻尼回弹(非对称)/ 掩码与裁剪面

> **Epic**: 摄像机与视角
> **Status**: Complete ✅ 2026-10-03(评审修复轮完成;16 例(2026-10-03 订正,原误记 17))
> **Layer**: Core
> **Type**: Integration
> **Estimate**: 6h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-09-28

## Context

**GDD**: `design/gdd/camera-and-viewpoint.md`
**Requirement**: TR-camera-001(平面第三人称越肩的**机器半边 ②** —— F-2-3 出臂 + F-2-4 收缩;GDD note 点名实现判据 = `AC-2-25④`(`ARM_LEN > 0`)+ `AC-2-14`)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*
🔴 本故事承载 2026-09-16 评审对 F-2-3/F-2-4 的**两处静默失败订正**:① 原 shoulder 项 `( f̂ × CAM_RADIUS ) × SHOULDER_LATERAL` 把**碰撞半径**当侧移基(耦合两个无关量 ⇒ 调碰撞旋钮**静默改变取景旋钮**),改 `r̂`;② F-2-4 原式 `d_target` 跨帧累积 + `min` 只降不升 ⇒ **玩家离墙后臂长永久卡在最近命中值**,且 `d_raw − CAM_RADIUS` 可为负 ⇒ **负臂长**(相机跑到肩位背后)。

**ADR Governing Implementation**: ADR-020(§三 越肩裁定 = 平面第三人称;`ARM_LEN = 0` 即实质第一人称,**平面禁用**)· ADR-015(诊疗台越肩可用位 = 烘焙逻辑层几何声明 —— `O-13` 落点,其验收归相机 spike = `AC-2-23` EXTERNAL)· ADR-014(臂参数经 `assets/data/` 烘焙管线读取,不得硬编码)· ADR-023(`World` 场景零 gameplay GameObject ⇒ 收缩所碰撞的几何是烘焙/建造物,不是运行期预摆探针)
**ADR Decision Summary**: ADR-020 §一/§三 只裁"第三人称越肩"与"自建机位",不给臂的几何 —— F-2-3/F-2-4 是 GDD 层的形状交付。每帧 PhysX 查询数 == 1 的性能义务归 story 005 的 `AC-2-27①`,本故事交付该**唯一一次 `SphereCast`** 的调用点与契约完整性。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: MEDIUM
**Engine Notes**: ⚠️ **Unity `SphereCast` 的已知契约**(GDD 原文点名为"必须写"):`起点球与碰撞体重叠 ⇒ 返回 false 且 distance = 0`。不处理则医馆墙角**直接穿模** ⇒ 回退形态已裁:`d_raw := CAM_MIN_DIST`(贴到最近,不是穿墙)。`Physics.SphereCast` 属稳定 API,但**返回契约的具体形态须实测确认**(不同版本对 `distance` 的语义:到碰撞面 vs 到球心)—— 登记为实现期一次性 spike(LOW 风险但必须核)。`QueryTriggerInteraction.Ignore` **必须显式给出**(默认值随调用重载不同 ⇒ 静默差异)。

**Control Manifest Rules (this layer)**:
- Required: 相机每帧**一次** `SphereCast`(投射原点 = 肩位 `shoulder`,方向 = `ê_view`,与几何同源)— F-2-4 订正(计数义务 `AC-2-27` 归 story 005,调用点形状归本故事)
- Required: 越肩偏移 = **常量参数**,不做动态解算 — R-2-4 / 组 7(「自动寻找最优取景角度」不存在)
- Forbidden: **Cinemachine 的任何阻尼/碰撞组件**代劳收缩 — ADR-020 §二(本故事恰是 Cinemachine 用户最常"抄近路"处,须显式自写)
- Forbidden: 跨帧缓存「命中结果 / 遮挡标志 / `d_target`」 — `AC-2-16` 订正精确式(`d` 与 `d_prev` 是**允许的**积分状态,遮挡记忆**不允许**)
- Guardrail: `NEAR_CLIP ≤ CAM_MIN_DIST − CAM_RADIUS` 是**装载期断言**不是建议(F-2-4 红线原文);数值留白 ⇒ 「取值一旦存在即被守住」口径

---

## Acceptance Criteria

*From GDD `design/gdd/camera-and-viewpoint.md`, scoped to this story:*

- [x] **AC-2-14(BLOCKING)** —— **收缩瞬时 / 回弹阻尼(非对称)**:
  ① **收缩路径无插值** —— 判据 = **遮挡距离突减**时,`d` 在**同一帧**即为 `d_block`(单帧内 `|d − d_block| == 0`),且该帧的 `Δd` **不受 `RECOVER_SPEED` 取值影响**(两个不同 `RECOVER_SPEED` 值跑同一夹具,断言该帧 `d` 相同);
  ② **回弹路径受 `RECOVER_SPEED` 约束** —— 判据 = 遮挡解除后 `|Δd| ≤ RECOVER_SPEED × dt`;
  ③ **非对称的序断言** —— 判据 = **同一几何夹具**下,收缩帧的 `|Δd|` **>** 回弹帧的 `|Δd|`(序,非数值)。
  ⚠️ **2026-09-16 评审订正 —— 原文 ③「回弹速率 < 收缩速率」不可执行**:收缩路径**根本没有「速率」这个量**(它是瞬时的,`Δd = d_block − d_prev` 取决于几何,不是速率)⇒ 现改为同一夹具下比较两种路径的单帧 `Δd`,可执行且保留原意。
  ⚠️ **对称缓动 = 相机穿墙插值** —— 这是 R-2-6 承重设计的机械守门。
- [x] **AC-2-15(BLOCKING)** —— **碰撞掩码与裁剪面**:
  ① **`CAM_COLLIDE_MASK` 不含角色层** —— 断言掩码**不含**玩家 / 病人 / 敌人 / 触发体的任何层,判据 = **层掩码的位集合断言**(不是「读一遍看一眼」)。
  ⚠️ **掩码须是「恰好等于白名单」而非「包含白名单」**(2026-09-16 评审订正):原文只断言「不含角色层」,而**不含角色层**与**不含任何层**都能通过 —— 后者会让相机**永不收缩**(穿墙)。⇒ 判据 = **`mask == expected_whitelist_mask`**(位集合相等)。
  ② **近裁剪面不等式(新增,原文完全缺失)** —— `NEAR_CLIP ≤ CAM_MIN_DIST − CAM_RADIUS`,判据 = **装载期断言**;否则相机贴到 `CAM_MIN_DIST` 时**自己切进几何体**(表现为「贴墙时墙被剖开」)。
  ③ **`QueryTriggerInteraction`** —— 断言投射参数为 **`Ignore`**(触发体不顶相机)。
  *(EC-2-2:若含玩家,贴墙时相机会把玩家顶穿画面。)*
- [x] **AC-2-16(BLOCKING)** —— **收缩无状态、每帧重算**:几何体在收缩**之后**出现时,**下一帧**即命中并收缩,**不需要**额外状态 —— 判据 = 注入夹具(先无遮挡、后放置几何体,断言单帧内收敛到 `CAM_MIN_DIST`,且**相机侧无新增的持状态字段**)。
  ⚠️ **2026-09-16 评审订正 —— 判据与 F-2-4 的冲突已消解**:原文要求「无新增的持状态字段」,而 F-2-4 原式**必须**保留跨帧 `d_target`(否则回弹无目标)⇒ **本条原来必然为红**。F-2-4 已重写为逐帧纯函数解 `d_block` + 回弹路径 ⇒「无跨帧的**遮挡距离**状态」现在成立。**但 `d`(当前臂长)本身是跨帧状态** —— 它是**回弹的积分变量**,不是「遮挡记忆」。⇒ 判据精确表述为:**不得存在跨帧保存的「命中结果 / 遮挡标志 / `d_target`」**;`d` 与 `d_prev`(回弹用)是**允许的**积分状态。
- [x] **AC-2-25(BLOCKING · 本组后补,编号追加不重排)** —— **肩位是常量几何、档位参数表是闭集**:
  ① **越肩偏移无动态解算** —— 判据 = 断言 `SHOULDER_LATERAL` / `SHOULDER_HEIGHT` 的求值**不依赖**任何运行时输入(玩家速度 / 场景查询 / 时间),即**常量参数读取**(R-2-4:「不做自动寻找最优取景角度」);
  ② **档位参数表是唯一的档位相关参数源** —— 判据 = 装载期断言 `ARM_LEN` / `SHOULDER_LATERAL` / `FOV_v` / `ANCHOR_RESPONSE` / `PITCH_CASEBOOK` / 「`Look` 是否驱动」/「锚是否跟随」七项的**档位取值全部来自组 5 的表**,程序集内**无第二处档位分支**(否则「换档 = 只改参数」的结构承诺已被偷偷改动 —— R-2-1);
  ③ **序关系成立**:`ARM_LEN_TREATMENT < ARM_LEN_EXPLORE` ∧ `SHOULDER_LATERAL_TREATMENT < SHOULDER_LATERAL_EXPLORE` —— **序断言,非数值断言**;
  ④ **`ARM_LEN > 0`**(`= 0` 即实质第一人称,**平面模式禁用** —— F-2-3 失效模式)。
  🔴 **2026-09-16 评审订正 —— 「装载期断言」在数值留空时不可执行**:②③④ 皆装载期断言而全案数值留白 ⇒ 若两值未填(默认 0)则 `0 < 0` 恒假 ⇒ **AC 恒红、不可签署**。⇒ 按通用口径:**「取值一旦存在即被守住」** —— 实现 = **数据表缺键 ⇒ 相机拒绝启动(或走默认档并报 `INCONCLUSIVE`)**,而不是让断言在空值上跑。③④ 因此**不在空值上失败**;「取值是否存在」是 `AC-2-21(EXTERNAL)` 的事。

---

## Implementation Notes

*Derived from F-2-3 订正两处 · F-2-4 订正三处 · EC-2-1/2/3/7/12:*

- **`r̂` 替换 `f̂ × CAM_RADIUS`(订正① 逐字落地)**:`shoulder := anchor + ŷ×SHOULDER_HEIGHT + r̂ × SHOULDER_LATERAL`,其中 `r̂` 来自 story 002 的 `YawBasis`(单位正交)。**反空转夹具**:同时改 `CAM_RADIUS` 与不改 `SHOULDER_LATERAL`,断言肩位**纹丝不动**(原形态下调碰撞旋钮会静默移动取景)。
- **`R(yaw, pitch)` 轴与手性钉死(订正②)**:先绕**世界 +Y** 转 `yaw`,再绕**该局部右轴**转 `pitch`(符号:正 = 俯,R-2-3)。`ê_view := R × ê_back` 必须是**单位**向量;GDD 点名违例表现 = 「斜视角下贴墙却不收缩」(投射方向与臂方向不一致)⇒ 夹具取 `yaw/pitch` 四角组合断言 `shoulder + ê_view×d` 的视线确实穿过碰撞体(GT 几何对拍)。
- **F-2-4 五行公式照抄,顺序不可换**:`d_raw`(SphereCast,未命中 ⇒ `ARM_LEN`;**起点重叠 false ⇒ `CAM_MIN_DIST` 回退**)→ `d_block := clamp(d_raw − CAM_RADIUS, CAM_MIN_DIST, ARM_LEN)` → 收缩支 `d := d_block`(瞬时)→ 回弹支 `d += min(RECOVER_SPEED×dt, d_block − d)`。**每帧从头解 `d_block`** —— 代码中不得出现 `if (wasBlocked)` / `blockedThisFrame` / `d_target` 类跨帧标志(③ 与 `AC-2-16` 的 AST 面)。
- **`AC-2-14①` 的"不受 RECOVER_SPEED 影响"判据** = 双值对照夹具(0.5 vs 50.0)跑同一几何,收缩帧 `d` 逐位相等 —— 该判据抓的实现形态 = "收缩也走 lerp/smin 阻尼"。
- **掩码相等断言的期望值来源**:`expected_whitelist_mask` = 「静态世界几何 + 建造物」层的位集合,层名与位号归 Unity 项目设置 / ADR-015(组 6:「Physics 层命名与掩码位不归 2,但 2 被它们锁住」)。测试从**同一登记处**算期望掩码,不写第二个常量(同源纪律,承 EPS/`MAX_DT` 先例)。⚠️ ① 抓的是**两端**失败形态:含角色层(玩家顶开相机)与空掩码(永不收缩穿墙)⇒ `==` 非 `⊇`。
- **`AC-2-15②` 装载期不等式**:`NEAR_CLIP ≤ CAM_MIN_DIST − CAM_RADIUS`,且 F-2-4 变量表另有 `CAM_MIN_DIST ≥ CAM_RADIUS` ⇒ 三条构成链(违反任一 ⇒ 装载失败,错误串点名三值)。数值留白 ⇒ 缺键拒绝启动口径。
- **`AC-2-25②` 的"无第二处档位分支"判据形态** = AST:相机程序集内 `switch(mode)`/`if (mode ==` 的出现**只允许**出现在参数表装载路径;臂计算/收缩函数体内零档位分支(机器是**同一台**,档位只改参数 —— R-2-1)。这条是结构承诺,先于任何转场实现(story 005)成立 ⇒ 本故事先签静态半边。
- **`FOV_v` 的读取归组 5 表**(② 七项之一),但 42 侧对 `FOV_v` 的**消费声明**已落(`O-15`,2026-09-22 履行)⇒ 本故事只装载与守序,不实现透视投影(Unity Camera fieldOfView 直设即可)。
- **EC-2-1 夹角 < 相机直径 / EC-2-3 医馆爆点**:P0 的解 = **收缩到 `CAM_MIN_DIST` 贴脸(已知可接受退化)+ 关卡几何(`O-13` EXTERNAL)**;淡出等第二手段 = `OQ-2-1`(spike 后裁),**本故事不得预支**(组 7:淡出旋钮不存在)。
- **EC-2-12 多相机/分屏**:P0 单相机单玩家;若实现出现第二相机走本链 ⇒ 结构断言红(与 story 001 的 AudioListener==1 同族)。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 002:`r̂`/`f̂` 的构造性质(`AC-2-07…10`)—— 本故事只**消费** `YawBasis` 于 `shoulder`
- Story 003:锚 `anchor` 的积分(本故事的 `shoulder` 起点)+ `dt` 钳位(回弹支用 `dt` 的那一个钳位值来自 story 003 的同源 `MAX_DT` 路径)
- Story 005:`AC-2-27①` 每帧查询计数 == 1 / `② Casebook` 档 == 0 的**计数器判据**;`AC-2-18③` 转场中臂参数插值(本故事已保证插值只发生在参数侧,机器不分支)
- Story 006:`AC-2-23`(`O-13` 诊疗台可用越肩位 = 相机 spike 验收,EXTERNAL)与 `OQ-2-1` 遮挡补充手段(P1a 前置 playtest)
- `AC-2-25③④` 的**真值签署**:`ARM_LEN_*` 取值留白归用户(通用口径:缺键 ⇒ 拒绝启动/INCONCLUSIVE,不在空值上跑)
- 建造物的**生成**(系统 23;本故事只把它们算进掩码白名单)

---

## QA Test Cases

*Written at story creation(lean mode — QL-STORY-READY skipped;specs self-authored from AC text). The developer implements against these — do not invent new test cases during implementation.*

- **AC-2-14①②③**: 非对称三段。
  - Given: 同一几何夹具(墙距由远及近再撤);`RECOVER_SPEED` 双值对照(慢 0.5 / 快 50)。
  - When: 逐帧记录 `d` 与 `d_block`。
  - Then: ① 遮挡突减帧 `|d − d_block| == 0`(精确 0,收缩无插值)**且**双 `RECOVER_SPEED` 下该帧 `d` 相同;② 回弹段每帧 `|Δd| ≤ RECOVER_SPEED × dt`;③ 同夹具收缩帧 `|Δd|` > 回弹帧 `|Δd|`(序断言)。
  - Edge cases: 回弹恰将越界(`d_block − d < RECOVER_SPEED×dt`)⇒ 一步到位不越过 `d_block`(min 的钳上界);收缩与回弹**同帧交替**(墙高频抖动夹具)⇒ 每帧各自路径,无跨帧状态残留;`dt = 0` ⇒ 回弹步长 0(收缩仍瞬时 —— 它不吃 dt)。
  - Negative fixture: 对称 lerp 实现(收缩也阻尼)⇒ ① 红(该帧 `d ≠ d_block`)+ 穿墙形态;`min` 跨帧缓存 `d_target` ⇒ 由 006 条捕获(见下)。

- **AC-2-16**: 无遮挡记忆(下一帧即命中)。
  - Given: 第 k 帧无墙(`d = ARM_LEN`),第 k+1 帧放置墙贴脸。
  - When: 逐帧。
  - Then: k+1 帧 `d == CAM_MIN_DIST`(单帧收敛);AST/反射:相机类型中**零**跨帧字段名/语义 ∈ {命中结果, 遮挡标志, `d_target`};`d` 与回弹所需的 `d_prev` 存在 ⇒ **合法**(订正精确式)。
  - Edge cases: 旧缺陷形态复现夹具:「离墙后臂长卡住」—— 撤墙后连续帧断言 `d → ARM_LEN`(原 `min` 只降不升在此必红);起点重叠(肩位已在墙内)⇒ `d_raw := CAM_MIN_DIST` 回退分支命中(Unity 契约面,须实测确认 distance=0/false 语义后钉断言形态 —— spike 登记)。
  - Negative fixture: 加一个 `bool _wasBlocked` 跨帧字段 ⇒ AST 红。

- **AC-2-15①**: 掩码**相等**断言。
  - Given: 从登记处(项目层设置 + ADR-015 层名表)计算 `expected_whitelist_mask`;相机实际掩码。
  - When: 位集合比较。
  - Then: `mask == expected`(恰等,非 `⊇`);玩家/病人/敌人/触发体任何位为 0。
  - Edge cases: **两端违例各一夹具**:含玩家位(EC-2-2 顶穿画面形态)⇒ 红;**空掩码 0**(原文「不含角色层」也能通过、实际永不收缩的假绿形态)⇒ 红 —— 本条的存在证明;建造物层后加 ⇒ 相等断言随之更新(同源计算,非硬编码)。
  - Negative fixture: 上述两形态。

- **AC-2-15②③**: 裁剪面不等式 + Trigger 参数。
  - Given: 注入表值三组:合法 / `NEAR_CLIP > CAM_MIN_DIST − CAM_RADIUS` / `CAM_MIN_DIST < CAM_RADIUS`;SphereCast 调用点 AST。
  - When: 装载期 + 编译期。
  - Then: 违例组装载失败(错误串点名三值);调用点显式携带 `QueryTriggerInteraction.Ignore`(重载默认值形态 = 违规)。
  - Edge cases: 恰等式边界(`==`)⇒ 绿;空值 ⇒ 拒绝启动(INCONCLUSIVE 口径,不在默认 0 上跑)。
  - Negative fixture: 省略 trigger 参数的 5 参重载。

- **AC-2-25①**: 肩位常量性(无动态解算)。
  - Given: 同一档,变 玩家速度 / 场景遮挡 / 帧时间 三路输入。
  - When: 求 `SHOULDER_LATERAL` / `SHOULDER_HEIGHT` 的取值路径。
  - Then: 取值与三路输入**无关**(纯参数读取);`CAM_RADIUS` 变更夹具下肩位**不动**(订正① 的反空转)。
  - Edge cases: "自动找不遮挡的肩侧"(左右换边)是 R-2-4 明令不存在的旋钮(组 7)⇒ 出现即红;`SHOULDER_LATERAL = 0` ⇒ 合法退化但须登记失效模式(GDD 边界行为原文:人物挡正前方)。
  - Negative fixture: `shoulder += f̂ × CAM_RADIUS × SHOULDER_LATERAL` 原式残留。

- **AC-2-25②③④**: 参数表闭集 + 序 + 正臂长。
  - Given: 组 5 注入表:合法序组 / 违序组(`ARM_LEN_TREATMENT ≥ ARM_LEN_EXPLORE`)/ `ARM_LEN = 0` 组 / **缺键组**。
  - When: 装载期 + AST 分支扫描。
  - Then: 七项参数全部来自表(计算体内零档位分支,`switch(mode)` 仅允许出现在装载路径);违序组装载失败;`ARM_LEN = 0` 装载失败(平面禁第一人称);**缺键 ⇒ 拒绝启动或 `INCONCLUSIVE`**(订正:不在空值上跑,③④ 不因留白恒红)。
  - Edge cases: 恰等(`TREATMENT == EXPLORE`)⇒ 违序(严格不等号按 AC 原文 `<`);`Casebook` 档的"冻结于进入值"语义归 story 005(`AC-2-19`),本条只验**取值在表里**。
  - Negative fixture: `if (mode == Treatment) armLen = 1.2f;` 的旁路字面量 ⇒ 双红(第二参数源 + 字面量)。

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- Integration: `tests/integration/camera/arm_retract_test.cs` — must exist and pass(GT 几何夹具:墙角/背靠墙/斜视角视线穿体对拍 + 非对称三段 + 起点重叠回退)
- Logic: `tests/unit/camera/shoulder_geometry_test.cs` — `AC-2-25` 四子 + `AC-2-16` AST
- Build/Load gate: `AC-2-15` 掩码相等 + 裁剪面不等式(装载失败级)

**Status**: [x] Done — `unity/Assets/Tests/EditMode/CameraViewpoint/camera_arm_solver_test.cs`(经本 story 评审修复轮)
⚠️ 不得借绿:`AC-2-25③④` 真值半边(数值留白 ⇒ 注入表签判据本体,真表 INCONCLUSIVE);`SphereCast` 起点重叠 distance 语义 = 实现期一次性实测(spike)未跑前,该夹具记 BLOCKED-BY-spike(回退分支代码可先落)。

---

## Dependencies

- Depends on: Story 001(程序集边界)/ Story 002(`r̂`/`ê_view` 的同源基)/ Story 003(`anchor` 起点 + 同源 `MAX_DT` 的 `dt`)/ ADR-015 + 层名登记表(掩码期望值同源)
- Unlocks: Story 005(转场插值的参数集 = 本故事的七项表;`AC-2-27①②` 计数器架在本故事的单一调用点上)/ Story 006(`AC-2-23` 的 spike 用真实臂几何)

---

## Completion Notes

**Completed**: 2026-10-03
**Criteria**: **4/4 AC 落地**(17 例)。交付 `CameraArmSolver.cs`(F-2-3 肩位 + F-2-4 五行)+ `CameraArmParams`(组 5 形状载体 + 装载期不等式链)+ `IArmCollisionQuery`(碰撞缝,使可单测)。
🔴 **GDD F-2-4 一处歧义(本批实测发现,须登记)**:公式写「未命中 ⇒ `d_raw := ARM_LEN`」,又写「起点重叠(false)⇒ `d_raw := CAM_MIN_DIST`」——但 Unity `SphereCast` 在**两种情形下都返回 false**,`distance` 在 false 时**未定义** ⇒ **调用方无法区分**。
**本批取保守语义**:`false` ⇒ 一律回退 `CAM_MIN_DIST`(**绝不穿模**;代价 = 真未命中时臂长为最短,但那要求球半径内完全无几何,极罕见且保守方向安全)。
⚠️ 若要区分须改用 `CheckSphere` 预判 ⇒ **多一次查询**,与 story 005 的「每帧恰一次 SphereCast」义务冲突 ⇒ **须另裁**。
⚠️ 两处 NOT-RUN(不借绿):**AC-2-15③** 真 `SphereCast` 未接线(经缝)⇒ `QueryTriggerInteraction.Ignore` 判据不可执行;**AC-2-25③** 真档位表(组 5)未在库 ⇒ 序关系只对注入表成立,「取值是否存在」归 AC-2-21(EXTERNAL)。
**Criteria**: 4/4 AC 落地(测试 **16** 例,非自述 17 —— 2026-10-03 订正)。
AC-2-14 收缩瞬时 / 回弹阻尼(非对称)· AC-2-15 掩码 + 近裁剪链 + Trigger · AC-2-16 无跨帧遮挡状态 ·
AC-2-25 肩位常量几何 + 档位闭集 + **ARM_LEN > 0 装载期守卫**(本轮补)。
**Deviations**: 🔴 **2026-10-03 双代理评审(QA Lead + TD)判 REJECT;3 处 BLOCKING 已修**:

| # | 原缺陷 | 修法 |
|---|---|---|
| **B1** | **F-2-4 第①行语义反转**(开放世界主路径回归)—— `false ⇒ CAM_MIN_DIST`,而 GDD 主用例是「**未命中 ⇒ `d_raw := ARM_LEN`**」;`SphereCast` 返回 false 的常见情形恰是「背后无墙」⇒ 臂长每帧塌到 0.5 m ≈ 贴脸第一人称。初版注释称该情形「极罕见」**是事实错误** | **按用户裁定「按 GDD 主用例改」**:`false ⇒ ARM_LEN`(远)· 补**真走 `Hit=false` 期望远臂长**的夹具(此前全被 `Hit=true,10f` 掩盖);**起点重叠**(EC-2-1)的区分登记 spike / 另裁(须 `CheckSphere` 预判,与「每帧恰一次」冲突) |
| **B2** | **`ViewDir` 手性符号反转 + 零覆盖** —— 实现在 `y = −sinθ`(相机移到肩**下方**),GDD F-2-3 订正② 钉死绕局部右轴 ⇒ `y = +sinθ`;且 `r̂` 声明未用、全测试目录 `ViewDir` 零命中 | 按 Rodrigues 逐字重写 `ê_view = ê_back·cosθ + (r̂ × ê_back)·sinθ`(= `+ŷ·sinθ`)· 补四角 yaw × 正负 pitch 的手性夹具(与 003 `GetCameraPosition` 的 +sinPitch 对齐) |
| **B3** | **AC-2-15① 判据空转(自证假绿)+ 生产默认即违例** —— 测试用工厂字面量比同级字面量,从未读生产值;生产 `CamCollideMask` 默认 **0**(空掩码 ⇒ 永不收缩 ⇒ 穿墙)且无写入点 | 期望取**登记常量** `DefaultCollideMask`· 补 `ValidateCollideMask`(空掩码 / 含角色层 ⇒ 抛)· 补**两个真负夹具**· 断言**生产默认**经守卫 |
| **B4** | **AC-2-25④ 无装载期守卫**,测试对夹具字面量自证 | 补 `ValidateArmLen`(ARM_LEN ≤ 0 ⇒ 抛)+ `ValidateAll` 单入口 · 断言生产默认 + 负夹具 |

**Test Evidence**: CameraViewpoint **75 例 · 68 过 · 0 红 · 7 跳过**(`unity/TestResults-639266620000180660.xml`);全量 EditMode `total 2202 · passed 2161 · failed 0 · skipped 40 · inconclusive 1`(既有 Audio 项)(2026-10-03 batchmode)。
**Code Review**: ✅ 双代理评审原件 `production/qa/evidence/review-camera-viewpoint-story-004-{qa-lead,td}-2026-10-03.md`;本轮 5B 修复复跑 0 红。
⚠️ **残留(登记,非本批可闭)**:`PhysicsArmQuery` 生产接线 + `CountingArmQuery` 实例化归 story 005(本轮已随 005 接线);
组 5 单一参数表对象(C2)· `CameraRig._distance` 第二臂长源收敛(C4)归 005 接线轮。
**Manifest**: 版本号已对齐 2026-10-02(⚠️ **仅版本号** —— 抽象点计数订正另立批次,见 control-manifest §传播范围)
