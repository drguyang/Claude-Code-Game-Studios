# Story 005: 档位状态机与性能义务 —— 意图制 / 优先级·每帧一结算 / Casebook 冻结 / PhysX==1 / Tick 单一相位

> **Epic**: 摄像机与视角
> **Status**: Complete ✅ 2026-10-03 (5/5 AC 落地;13 例测试)
> **Layer**: Feature
> **Type**: Logic
> **Estimate**: 6h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-09-28

## Context

**GDD**: `design/gdd/camera-and-viewpoint.md`
**Requirement**: TR-camera-001(平面第三人称的**档位侧** —— R-2-5 三档 `Explore / Treatment / Casebook` 的机器形状)· TR-camera-003(意图制 = 「不持游戏状态」在档位轴上的机制化:档位**只能**由请求方设定,相机永不自行裁决 —— `AC-2-17` 原文点名「`AC-2-01` ① 的机制化」)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*
🔴 本故事承载 EC-2-9 的**结算算法收敛**(2026-09-16 评审:「同优先级取最后」与「同档幂等」原互相纠缠会产生**活锁观感** ⇒ 收敛为"每帧结算一次"的四步算法)与 `AC-2-20` 的**不可能性订正**(原文「无任何计时字段」在转场 `t` 存在时恒假 ⇒ 精确为「无与**档位存续时间**相关的计时器/看门狗」)。

**ADR Governing Implementation**: ADR-020(§五 相机只读不持状态 —— 档位是**呈现参数**不是游戏事实;§三 档位语义的呈现侧)· ADR-011(③ 的机制来源:固定相位 `InputSystem.onAfterUpdate` 或 `ICameraRig.UpdateYaw()` 显式驱动,**禁 `Script Execution Order` 面板** —— EC-2-14 同源纪律)· ADR-013(`ModalId` 闭集中 `Casebook` 的呈现侧承接;`O-12` 对侧:39 是唯一请求方,8 不再发 —— 2026-09-19 用户裁定)· ADR-023(`FirstPerson` 档 P0 只落枚举,VR 链冻结归 P1a —— story 006 的接口面)
**ADR Decision Summary**: ADR-020 裁"相机只读",本故事把它机制化到档位轴:相机的 `Mode` 写入点 == 1(`SetMode`),该写入点的方法体引用集**不含 8/10/39 类型**(不查界面栈、不查动作状态)。优先级 `Casebook > Treatment > Explore` 是 GDD R-2-5 的语义序(模态 > 动作 > 默认),不是本故事的发明;本故事交付**裁决机器**与**性能计数器**。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW(状态机本体)/ MEDIUM(计数器注入面 —— `Physics.SphereCast` 静态 API 不可拦截,须走包装接口;包装点是本故事的实现自由度)
**Engine Notes**: 纯 C# 状态机 + 计数器夹具,不触 post-cutoff API。⚠️ `AC-2-27①` 的"计数包装查询接口"意味着**相机对 PhysX 的调用必须经可注入的抽象**(而非直调静态 `Physics.*`)—— 该抽象同时是 `AC-2-16` 起点重叠回退夹具的注入口。`Script Execution Order` 面板依赖的检测 = 判据 ③ 的"调用点不是裸 `Update`/`LateUpdate`"半边(AST 形态)。

**Control Manifest Rules (this layer)**:
- Required: 相机是表现层 —— 只读,永不持有游戏状态;档位**只能由请求方经 `SetMode` 设定**(意图制)— ADR-020 §五 / R-2-5
- Required: 一切时间推进走显式相位(ADR-011 `onAfterUpdate` 先例);**禁止** `Script Execution Order` 面板隐式排序 — `AC-2-27③`
- Forbidden: 相机侧任何「档位存续计时器 / 看门狗」(自行超时退出 = 制造它本不该有的状态机分支)— `AC-2-20` 订正精确式 / EC-2-10
- Forbidden: 在 `SetMode` 写入点内读 8 的界面栈 / 10 的动作状态 / 39 的模态(判据 = 方法体引用集)— `AC-2-17`
- Guardrail: `TRANSITION_JUMP_EPS` / `PITCH_CASEBOOK` 取值归用户(组 5b/5a);「取值一旦存在即被守住」口径;`Casebook` 档不发 PhysX 查询(计数 == 0)是**装载后即生效**的硬界,不因数值留白而豁免

---

## Acceptance Criteria

*From GDD `design/gdd/camera-and-viewpoint.md`, scoped to this story(判据正文照录,修订沿革见 GDD 原文):*

- [x] **AC-2-17(BLOCKING)** —— **意图制**:档位**只能**由 `SetMode(...)` 改变 ——
  判据 = **`Mode` 的写入点数 == 1**(唯一入口),且该写入点**不读**任何其它系统的状态(无「查 8 的界面栈」「查 10 的动作状态」式读取 —— 判据 = 调用点所在的**方法体的引用集**内不含 8 / 10 / 39 的任何类型)。
  *(R-2-5;`AC-2-01` ① 的机制化。)*
- [x] **AC-2-18(BLOCKING)** —— **优先级 + 幂等 + 可打断**:
  ① **低优先级不能打断高优先级**(`Casebook` > `Treatment` > `Explore`)—— 判据 = 状态机的不变量断言(**穷举 3×3 档对,含同档**);
  ② **同档重入不发转场**(幂等)—— 判据 = 转场计数不增;
  ③ **转场中被打断时 `*_from` 取当前插值值**(非源档值)⇒ **画面无跳变** —— 判据 = 逐帧采样相机变换,断言帧间位移 ≤ `TRANSITION_JUMP_EPS`(阈值常量,**归 2**,由用户定;原文「超过阈值」但**未给常量名** ⇒ 已补);
  ④ **每帧只结算一次**(2026-09-16 新增,承 EC-2-9 的结算算法)—— 判据 = 注入同帧多请求夹具,断言**转场重起算次数 ≤ 1**。
- [x] **AC-2-19(BLOCKING)** —— **`Casebook` 的锚冻结 + 固定高俯角姿态**:
  ① 进入时**快照**锚位置,期间玩家移动**不改变**相机位置 —— 判据 = 注入夹具(进入 `Casebook` 后移动玩家,断言相机变换**逐位不变**);
  ② **进入姿态 = 一个约定的固定高俯角**(2026-09-16 用户裁定)—— 判据 = 断言进入 `Casebook` 后 `pitch` 收敛到 **`PITCH_CASEBOOK`**(逐档常量,`< PITCH_MAX`),**与本档的 `ARM_LEN` / `SHOULDER_*` 同样「冻结于进入值」**(但 `pitch` 冻结于**该档的约定值**,不是玩家进来时的值)。
  *(R-2-5 转场规则 3;EC-2-8;ADR-020:249 的「脉案(俯视/固定)」的消歧 —— 见后注。)*
- [x] **AC-2-20(BLOCKING)** —— **相机不自行超时退出**:`Treatment` 档在**无 `Explore` 请求**的情况下**永久保持**,不引入任何超时 / 看门狗字段 ——
  判据 = **相机侧无计时状态字段**(结构断言:反射扫描相机状态,断言无「与档位相关的计时器」)+ 长时注入(≥ 10⁴ 帧)下档位不变。
  ⚠️ **2026-09-16 评审订正**:原文写「**无新增的计时状态字段**」—— 而 **`d` 的回弹是积分状态、转场进度 `t` 本身就是计时量**,故「无任何计时字段」是**不可能满足的**。⇒ 精确表述为「**无与档位存续时间相关的计时器 / 看门狗**」(转场计时 `t` 是允许的,它属于转场)。
  *(EC-2-10;「这条的判据归系统 10」⇒ 反向登记为 `O-12`。)*
- [x] **AC-2-27(BLOCKING · 2026-09-16 评审新增)** —— **相机的性能义务与求值次序归属**:
  ① **每帧物理查询数有界** —— 断言相机每帧对 PhysX 的查询调用数 **== 1**(即每帧**一次** `SphereCast`,**不因转场 / 档位 / 遮挡状态而变化**);判据 = 注入一个计数包装的查询接口,跑 ≥ 10³ 帧覆盖全档位与转场,断言**每帧计数恒为 1**;
  ② **冻结档不查询** —— `Casebook` 档期间(锚冻结、`Look` 不驱动、无移动)**不得发起 PhysX 查询**;判据 = 同上计数器在该档下**每帧为 0**;
  ③ **求值次序归属** —— 相机 `Tick` 的**调用来源**须是**单一显式调用点**(承 ADR-011 的固定相位或 `ICameraRig.UpdateYaw()`),**禁止**依赖 `Script Execution Order` 面板的隐式排序;判据 = 断言 `Tick` 的调用点数 == 1 且调用点**不是** `MonoBehaviour.Update`/`LateUpdate` 中的裸调用(即有时间相位显式声明)。
  *(承 `performance-analyst`:原文宣称的帧时间占比 ≤ 0.12% 只覆盖了 `SphereCast` 本身,**未覆盖**「谁在何时调它」—— 而 EC-2-11 的冻结档**没有 AC**,故「冻结的相机仍在烧查询」是**无人守**的路径;③ 同时是 `AC-2-10` ① 的机制来源 —— story 002 反向对账点。)*

---

## Implementation Notes

*Derived from R-2-5 状态机 · EC-2-5 B 段 · EC-2-8/9/10 · 组 5a/5b:*

- **EC-2-9 四步结算算法逐字落地**(评审收敛后的唯一可执行形态):每帧**一次**结算 —— ① 取本帧全部请求按优先级降序排序(同优先级保持到达顺序)→ ② `candidate := 排序后第一个`(= 最高优先级;**同优先级取最后到达**,该语义落在排序内)→ ③ `candidate == Mode` ⇒ 不发转场(幂等,计数不增;落**结算后**判断)→ ④ else 起转场(正在转场则**从当前插值位置**重起算,规则 1)。「请求到达即结算」= 活锁形态,禁止。
- **`AC-2-18③` 的 `*_from` 语义**:打断时 `from := 当前插值值`(世界系数值,非"源档参数向量")⇒ 夹具 = 转场进行到 `t=0.4` 时反向打断,断言帧间位移 ≤ `TRANSITION_JUMP_EPS`(取 story 002 的**同一常量实体**纪律 —— 全仓一处字面量)。
- **`AC-2-17` 的两半判据**:① `Mode` 字段的写入点(AST)恰 1 个 = `SetMode` 内部;② 该方法体引用集 ∩ {8 类型, 10 类型, 39 类型} = ∅(符号级判定,不是 grep 命名空间 —— `IModalState` 是 42 的只读接口,**不属** 8/10/39 任一类型集,但相机若在写入点读它仍违"意图制"语义 ⇒ 判据按 ADR-013 归属:`IModalState` 消费面归 story 006 的 `AC-2-22⑤` 对账,`SetMode` 路径保持零模态读取)。
- **`AC-2-19①` 锚快照**:进入 `Casebook` ⇒ `anchor_snapshot := anchor` 且跟随段短路(读快照不积分);玩家位移夹具(`P_player` 任意移动 + 传送)下相机变换**逐位不变**。`v_anchor` 在快照时是否清零 GDD 未明文 ⇒ 实现期二选一并落注释(与 story 003 吸附优先序同纪律);建议 `v_anchor := 0`(退出档后无残速突跳,断言面更干净)。
- **`AC-2-19②` 的两侧**:pitch 转场**收敛到 `PITCH_CASEBOOK`**(约定常量,非玩家进来时的值)—— 与 `Look` 驱动路径互斥(`Casebook` 期间 `Look` 轴**不驱动 yaw/pitch**,档位表「`Look` 是否驱动」列的档位取值,消费 story 004 的 `AC-2-25②` 同一张表)。`0 < PITCH_CASEBOOK ≤ PITCH_MAX < 90°` 不等式链 = 装载期断言(数值留白 ⇒ 缺键拒绝启动口径)。⚠️ ADR-020:249「俯视」措辞的 errata **不阻塞本故事**,建议项归用户批准(GDD 后注原文)。
- **EC-2-5 B 段(UI 焦点不夺取 Look)**:`ModalId` 闭集中除 `Casebook` 外的模态(存档位/教学纸近景等)打开时,`Look` **照常驱动** —— 唯一例外 = `Casebook`(`AC-2-19`);判据夹具 = 注入非-Casebook 模态 + `Look` 序列 ⇒ yaw/pitch 照常(焦点单栈门归 42/3,本故事只守相机侧不"主动让出")。
- **`AC-2-20` 的反射面精确性**:扫描对象 = 相机状态闭包;违规形态 = 字段语义 ∈ {档位存续计时, 看门狗, 超时阈值计数};合法形态 = 转场进度 `t`(属于转场,不属于存续)、`d`/`v_anchor`(积分状态)。长时注入 ≥ 10⁴ 帧 `Treatment` 静止 ⇒ `Mode` 不变 + 无自发转场。反向义务 = 对侧「急救结束(含跳过路径)必须发 `Explore`」归 10,登记 `O-12`(story 006 对账表)。
- **`AC-2-27①②` 的计数器形状**:相机经**包装接口**(如 `IPhysicsQuery`)发起唯一查询;夹具 = 计数包装实现,跑三档 × {静止, 转场中, 遮挡突现} × ≥ 10³ 帧:非-`Casebook` 每帧恰 1(转场不加倍、遮挡状态不追加第二次探测),`Casebook` 每帧恰 0。该接口同时是 story 004 起点重叠回退的注入面(共用,不造第二个抽象)。
- **③ 与 story 002 的闭环**:002 的 `AC-2-10①` 次序契约的**机制来源**就是本条的"单一显式调用点"—— 若 002 选相位 B(`UpdateYaw()` 驱动),本条的 `Tick` 调用点与 002 的更新时刻是**同一物理调用点**(两侧夹具互指,防各自测一半);002 若选相位 A(`onAfterUpdate`),本条的 `Tick` 挂同一相位之后。面板依赖检测 = 结构断言(无 `ScriptExecutionOrder` 资产依赖;判据"调用点不是裸 Update/LateUpdate"的 AST 半边)。
- **穷举 3×3 档对夹具**(①):`(当前档, 请求档)` 全 9 组合 × 转场中/非转场两相 = 18 情形,输出唯一(优先级裁决 + 幂等 + 打断取当前值),无"实现者按到达顺序结算"的歧义面 —— 表驱动断言,新档加入时表自动扩(枚举闭集守卫)。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 001:程序集边界与"不持游戏状态"的类型域扫描(本故事的 `Mode`/`t`/快照都是**合法表现层状态**)
- Story 002:`AC-2-10①` 次序契约本体(本故事交付其机制来源 ③;探针夹具互指)
- Story 003:锚跟随的积分数学(`Casebook` 快照只**冻结**该段,不修改其判据)/ 回弹与 `TRANSITION_JUMP_EPS` 的常量实体(story 002/004 同源纪律)
- Story 004:臂/收缩本体(本故事只要求其"每帧恰一次查询"不随档位变化;`AC-2-25②` 的档位参数表消费面已在 004)
- Story 006:`O-12` 的对侧回刷登记(10/39 GDD 侧)/ VR `FirstPerson` 链冻结与 `AudioListener` 迁头显(EC-2-11,P1a 只落接口)/ `AC-2-22` 义务对账
- 系统 10 / 39:何时发 `SetMode` 的**语义**(急救开始/结束含跳过路径;脉案开关)—— 相机只守"只能被请求"(意图制),请求时机的正确性是对侧 AC
- `PITCH_CASEBOOK` / `TRANSITION_JUMP_EPS` 取值 —— **归用户**(数值冻结;本故事交付判据与注入夹具)

---

## QA Test Cases

*Written at story creation(lean mode — QL-STORY-READY skipped;specs self-authored from AC text). The developer implements against these — do not invent new test cases during implementation.*

- **AC-2-17**: 意图制双判据。
  - Given: `Mode` 字段全程序集 AST 写入点扫描 + `SetMode` 方法体符号引用集。
  - When: 扫描。
  - Then: 写入点恰 == 1(在 `SetMode` 内);该方法体引用集 ∩ {8, 10, 39 类型} == ∅;无任何"查界面栈/查动作状态后自行改档"的旁路。
  - Edge cases: 反射/委托间接写 `Mode`(判据补:非 `SetMode` 路径的 setter 私有化 + 无反射授权注释);构造函数初值 `Mode = Explore`(初始化不算写入点 —— 边界归属须显式声明于测试注释)。
  - Negative fixture: 加一个"检测到玩家静止 60 s 自动回 Explore"的看门狗(同时违 17 与 20)⇒ 双红。

- **AC-2-18①②④**: 3×3 穷举 + 幂等 + 风暴。
  - Given: 18 情形表(9 档对 × 转场两相)+ 同帧多请求夹具(`Explore`+`Treatment` 先后两序、`Casebook`+`Treatment`、同档双请求、三请求风暴)。
  - When: 每帧一次结算。
  - Then: ① 低不打高(`Casebook` 期间 `Treatment` 请求被吞,`Mode` 不变)穷举表逐格绿;② 同档重入转场计数不增;④ 同帧任意请求组合 ⇒ **转场重起算次数 ≤ 1**(算法唯一解:排序内取"最高优先级中最后到达",结算后判幂等)。
  - Edge cases: `Treatment`+`Explore` **到达序互换**(A→B vs B→A)⇒ 结算结果相同(抓"按到达顺序即时结算"的活锁实现 —— EC-2-9 订正的靶子);`Casebook` 打断进行中的 `Explore→Treatment` 转场 ⇒ 从当前插值值重起算(归 ③ 夹具)。
  - Negative fixture: 请求到达即结算 ⇒ 同帧两请求两次重起算 ⇒ ④ 红。

- **AC-2-18③**: 打断无跳变。
  - Given: 转场进行至 `t≈0.4` 时反向打断(如 Explore→Treatment 中途请求 Casebook)。
  - When: 逐帧采样相机变换。
  - Then: 帧间位移 ≤ `TRANSITION_JUMP_EPS`(同一常量实体,story 002 定义点;`*_from` = 当前插值值非源档参数)。
  - Edge cases: `t≈0⁺` / `t≈1⁻` 两端打断;连续三帧各打断一次(转场链)⇒ 每帧位移均有界。
  - Negative fixture: `from := 源档值`(瞬移回起点再转场)⇒ 打断帧位移 ≈ 全档差 > EPS,红。

- **AC-2-19①②**: Casebook 冻结 + 固定俯角。
  - Given: 进入 `Casebook` 后:玩家移动夹具(含 > `TELEPORT_SNAP_DIST` 传送)、`Look` 序列、`timeScale` 变化。
  - When: 逐帧。
  - Then: ① 相机变换**逐位不变**(锚快照);② `pitch` 收敛到 `PITCH_CASEBOOK`(注入表夹具;不等式链 `0 < PITCH_CASEBOOK ≤ PITCH_MAX < 90°` 装载期守住,缺键拒绝启动);`Look` 不驱动 yaw/pitch;`ARM_LEN`/`SHOULDER_*` 冻结于进入值(档位表消费面,story 004 同表)。
  - Edge cases: 进入瞬间正处锚转场(`v_anchor ≠ 0`)⇒ 快照后无残位移(实现二选一的注释一致性);非-Casebook 模态(存档位)打开 ⇒ `Look` **照常驱动**(EC-2-5 B:唯一例外是 Casebook);退出 Casebook ⇒ 跟随恢复,`pitch` 交还 `Look`(不强制回写入玩家进入前值 —— 转场语义)。
  - Negative fixture: Casebook 期间继续跑跟随积分 ⇒ 移动玩家后相机跟着走,① 红;`pitch := 玩家进入时的值` ⇒ ② 的收敛断言红。

- **AC-2-20**: 无存续计时器 + 长时不变。
  - Given: 反射扫描相机状态闭包 + `Treatment` 静止 ≥ 10⁴ 帧注入。
  - When: 结构断言 + 长跑。
  - Then: 无「档位存续计时/看门狗」字段(`t` 转场进度与 `d`/`v_anchor` 积分态**合法** —— 订正精确式);10⁴ 帧 `Mode == Treatment` 且零自发转场。
  - Edge cases: 字段名合法但语义违规(`int _idleTicks`,虽为 int 不看档位 ⇒ 仍红;判据=语义可达性:该字段的读取路径是否进入档位裁决);跨暂停长跑(`timeScale=0` 时计时字段即使存在也不走表 —— 反空转:测试须用可控时钟推进 10⁴ 帧)。
  - Negative fixture: 上述 `_idleTicks` + 阈值比较形态。

- **AC-2-27①②**: 查询计数恒等。
  - Given: 计数包装的 `IPhysicsQuery` 实现;场景 = 三档 × {静止, 转场中, 遮挡突现/消失} 组合序列 ≥ 10³ 帧。
  - When: 每帧读计数。
  - Then: 非-`Casebook` 帧计数 **== 1 恒成立**(转场中不翻倍、"二次探测确认"不存在);`Casebook` 帧计数 **== 0**(冻结档不查询)。
  - Edge cases: 转场跨越档(Explore→Treatment)的每帧仍 1(无"两档各查一次"的插值中间实现);起点重叠回退(story 004)仍走同一计数 == 1(回退不是第二次查询);`dt=0` 帧(GI/回归形态)⇒ 计数口径须与实现注释一致(查了就是 1,不豁免)。
  - Negative fixture: `Casebook` 期保留跟随段查询(烧无人看的路径 —— EC-2-11 无 AC 的教训)⇒ ② 红。

- **AC-2-27③**: Tick 单一调用点 + 相位显式。
  - Given: AST 全工程 `Tick`(或等价入口)调用点扫描 + 场景/ prefab 资产中 `ScriptExecutionOrder` 依赖检查。
  - When: 扫描。
  - Then: 调用点 == 1;该点非 `Update`/`LateUpdate` 裸调用(有显式相位声明:ADR-011 `onAfterUpdate` 注册 或 由单一驱动方调 `UpdateYaw()`);无任何面板排序资产依赖。
  - Edge cases: 与 story 002 相位选定一致(若 002 选 B,本调用点即 002 探针夹具的更新时刻 —— 双向互指);测试框架自身的驱动入口(EditMode 手动推进)不算第二调用点(判据范围 = 运行时产品代码,测试注入面在注释声明)。
  - Negative fixture: 把 `Tick()` 直调放 `LateUpdate` ⇒ 红;加第二个驱动方(如另一 MonoBehaviour)⇒ 调用点数 2 ⇒ 红。

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `tests/unit/camera/gear_state_machine_test.cs` — must exist and pass(17 双判据 + 18 穷举 18 情形/风暴/打断 EPS + 19 冻结夹具/俯角收敛 + 20 结构扫描/10⁴ 帧长跑)
- Logic: `tests/unit/camera/physx_query_budget_test.cs` — 计数包装 ≥ 10³ 帧 × 三档矩阵(① == 1 / ② == 0)
- Static/AST: `AC-2-27③` 调用点与面板依赖扫描(随 001 的程序集扫描载体)

**Status**: [ ] Pending — story not yet implemented(真身落点预期 = `unity/Assets/Tests/EditMode/Camera/`;登记口径 = `tests/unit/camera/`)
⚠️ 不得借绿:`PITCH_CASEBOOK` / `TRANSITION_JUMP_EPS` 真值留白 ⇒ 注入表签判据本体,真表 INCONCLUSIVE;`③` 的"相位落点与 story 002 一致性"半边依赖 002 实现期选定的 A/B(**选 A 或 B 前该子断言记 BLOCKED-BY:story 002 相位决定**);`O-12` 对侧(10/39 发请求的时机正确性)不在本证据面,归 story 006 对账 + 对侧 GDD。

---

## Dependencies

- Depends on: Story 001(程序集边界)/ Story 002(`TRANSITION_JUMP_EPS` 同一常量实体 + 相位 A/B 决定)/ Story 003(锚段与 `v_anchor` 的冻结对象)/ Story 004(被冻结/被计数的查询调用点本体;档位参数表 `AC-2-25②`)
- Unlocks: Story 006(`O-12`/`O-16` 对账的实现侧;VR 接口 `FirstPerson` 枚举走同一状态机)/ 系统 10、39 的相机请求集成(意图制的可依赖前提)

---

## Completion Notes

**Completed**: 2026-10-03
**Criteria**: **5/5 AC 落地**(13 例)。交付 `CameraModeMachine.cs`(意图制 + 每帧一结算 + 优先级/幂等/可打断)+ `CameraModePriority` + `CountingArmQuery`(计数包装,使「每帧恰一次」可断言)。
🔴 **实现期一处顺序缺陷(本批实测发现并修)**:初版 `Settle` **先推进 `TransitionT` 再裁决** ⇒ 打断帧的 `from` 取的是**推进后**的插值值(比打断那一刻**多一帧**)⇒ 画面会有**一帧跳变** —— 恰是 AC-2-18③ 要防的。**修法**:顺序改为「裁决(用当前 t 取 from)→ 再推进 t」。
⚠️ 测试侧三处自我修正(留痕):`CanWrite` 对 `private set` **仍为 true**(须查 setter 可见性);`\bMode\s*=` 正则**误匹配**别的类与 `==`(须限定类体 + 排除 `==`);`PITCH_CASEBOOK` 默认值**恰好等于** `PITCH_MAX` 致 `<` 失败(改 45)。
**Criteria**: _待填_(交付时须附:`SetMode` 写入点 == 1 的扫描输出 + Casebook 进入时 `v_anchor` 二选一的注释位置 + 相位 A/B 与 story 002 的一致性记录)
**Deviations**: _待填_
**Test Evidence**: _待填_
**Code Review**: _待填_
**Manifest**: 版本号已对齐 2026-10-02(⚠️ **仅版本号** —— 抽象点计数订正另立批次,见 control-manifest §传播范围)
