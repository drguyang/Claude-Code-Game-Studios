# Story 005 TD 架构一致性评审 —— 档位状态机与性能义务

> Reviewer: Technical Director
> Date: 2026-10-03
> Object: `production/epics/camera-viewpoint/story-005-gear-state-machine-perf.md`
> Code: `unity/Assets/Gameplay.Presentation/Camera/CameraModeMachine.cs`
> Tests: `unity/Assets/Tests/EditMode/CameraViewpoint/camera_mode_machine_test.cs`
> Verdict: **CONCERNS**

## Verdict

**CONCERNS** —— Story 005 的**核心诉求(档位单一写入点)经独立核实成立**,B2 修复真已落地;
但 **4 条判据的「判据强度」低于其 AC 原文所要求**(其中 2 条实为**空转/恒真**),
**2 处跨 story 接缝的责任无人承担**,**1 项优先级改动架构上站不住**。
均已核实、可定位、可修,不构成推翻 005 交付的理由,故不判 REJECT。

---

## 1. 档位单一写入点 —— **独立核实通过(1 处裂缝)**

**核实结论:`CameraRig._mode` 真已删除,全仓无第二处档位状态。** 证据:

- `CameraRig.cs:41` 现为 `private readonly CameraModeMachine _modeMachine = new CameraModeMachine();`;
  `:100` `public CameraMode Mode => _modeMachine.Mode;`(**只读门面**,无 setter)。
  注释 `:35-39` 自陈这是评审 B2 的修复(初版自持 `_mode` 且 `SetModeForTest` 直写它)。
- 全仓 grep `_mode`(非测试)/`CameraModeMachine`/`IsFrozen`:**唯一档位状态持有者 = `CameraModeMachine`**。
  `PlayerController.cs:48` 的 `_mode` 是 `SimAuthorityMode`(主机/客户端),**与档位无关**,非第二处。
- `CameraRig.SetModeForTest` 已改走 `_modeMachine.SetMode(...)` + `Settle(...)` —— **同一入口**,无旁路。

**裂缝(须修)**:`CameraModeMachine.CasebookPitch`(`:146`)是 `{ get; set; }` **public 可写**的
档位绑定参数,任何人都能改,不经 `SetMode`。`AC-2-17` 的"唯一写入点"精神(档位只能被请求方经意图制改变)
在 `Mode` 上成立,但档位的**属主配置**留了第二个公共写口。**建议**:`private set` + 经构造函数/注入缝设定,
或明确注释其"归组 5 参数表、非档位状态"的归属。

**判据强度缺口**:`test_ac217_modeHasSingleWritePath` 用**类体内 regex** `(^|[^=!<>])Mode\s*=(?!=)`
计数 == 1。该判据**成立但脆弱**:它靠"属性初始化器 `{ get; private set; } = Explore` 恰好不被正则匹配"
来排除初值。story 自己要求"构造函数初值不算写入点的**边界归属须显式声明于测试注释**"——
**该注释不存在**,边界是**偶然成立**而非**显式声明**。属 advisory 级。

**AC-2-17 第二半(方法体引用集)判据实为 grep,非符号级 —— 且查错方法。** 三重问题:

1. story 明写"**符号级判定,不是 grep 命名空间**";`test_ac217_setMode_doesNotReadOtherSystems` 实际做的是
   `body.Contains("IModalState"|"ModalId"|"DiagnosisSystem"|"EmergencyProcedures"|"CaseSystem")` —— **字面名子串**,
   别名/重命名/部分名即失效。测试自身已留注释承认"IL 层类型引用须 Cecil;此处用源码级等价判据",
   且留了一段**死代码**(`referencedTypes` 构造后从未使用)。
2. **对象选错**:`SetMode` 体只有一行入队(`_pending.Add(...)`),**天然恒净**;真正裁决档位的代码在
   **`Settle`**(`CameraModePriority.Of` + `Mode = best.Mode`),而 `Settle` **不在判据面内**。
   这正是"AAC 原文所称的『方法体引用集』"被替换成了"最容易被满足的那个方法"。
3. ⇒ 判据**可过但不可信**。

---

## 2. `CameraModePriority.FirstPerson => 3` —— **架构错误,须改**

**结论:该改动使 `FirstPerson` 与 `Casebook` **同优先级(3)**,而同优先级裁决是"后到者胜" ⇒
VR 与脉案**互相可抢占,由到达序决定**,与 GDD 的两条硬定冲突。**

依据(全部为**权威件原文**):

- `camera-and-viewpoint.md:244` **R-2-9**:"VR 第一人称是**另一条路径,不复用本链**……它**不经过 ①②③④ 任何一段**"。
- `:257` 状态表 `FirstPerson` 行"**不经本链**;头显驱动";`:689` **EC-2-11**:`FirstPerson` 生效 ⇒
  **平面链的全部状态冻结**。`Casebook` 是平面链的一档 ⇒ VR 在位时 `Casebook` **不应可被裁决进入**。

**当前改法的具体失效**:`Settle` 的 `canSwitch = bestPrio >= curPrio && best.Mode != Mode`
(`:117`)+ 同优先级取最后入队者(`:100`)= 同档台面上的四条边全由**到达顺序**决定:
`FirstPerson→Casebook` 会切、`Casebook→FirstPerson` 会切 —— 结果 = "VR 头显里摊开一本平面脉案纸"。
GDD EC-2-11 明禁此态。

**为何数值可选 3 本身就是证据**:`canSwitch` 只要求 `FirstPerson` 优先级 ≥ 1。1/2/3 任取都能过门,
**选 3 是与 `Casebook` 撞车的那一个**。在现有三值格内**不存在不冲突的取值** ——
这恰说明 `FirstPerson` **不属于**这个格(它不是"模态/动作/默认"三分中的任何一类,它是**第四条路径**)。

**建议(二选一,归 P1a 前;P0 无 VR 实现故为潜伏缺陷)**:

- **甲(荐)**:`FirstPerson` **不经 `CameraModePriority`** —— 在 `Settle` 裁决**之前**早退
  (独立路径判据),与 R-2-9"不复用本链"逐字对齐;三档格保持封闭 3 值。
- 乙:给 `FirstPerson` 一个**独立且高于 `Casebook`** 的档位 + **显式冲突规则**
  ("VR 在位时拒收 `Casebook`"),即 Godot/Unity 常见的"模式栈 `Push/Pop`"语义。

现状(同值 3 且无冲突规则)= 甲乙之弊兼有。**归 P1a 开工前必改**;同时 `story-005` 测试注释
`"P1a 独立路径,不受三档优先级门约束"`与它**恰好因优先级 3 才通过**的机制**自相矛盾**,
须一并订正。

---

## 3. `CountingArmQuery` —— **不能证明"每帧恰一次";是调用方自律**

**结论:它证明的是"计数器每次 `Cast` 加 1",不是"相机每帧恰查询 1 次"。**

- `BeginFrame()` 清计数、`FrameCount` 读计数,**两端都由调用方驱动**(`:166` `:163`)。
  类内**无任何**帧边沿自身的判据 —— 调用方不调 `BeginFrame` 就永远累积;调两次就丢一半。
  **判定权在调用方手中,不在被判定对象手中。**
- `test_ac227a` 的断言 `Assert.AreEqual(1, counting.FrameCount)` 是**恒真**:循环体里**只**做了一次
  `Cast`(`:243`),故"1 次 Cast ⇒ 计数 1"是同义反复。它无法抓到任何"生产路径每帧查两次"的实现。
- **接口根本未接线**:全仓 `CameraArmSolver.Step`(真正发查询的那一处,`CameraArmSolver.cs:137`
  `_query.Cast(...)`)**在生产代码中零调用点**(grep `.Step(` 命中的**全部**是测试)。
  ⇒ 计数包装**从未进入真实每帧求值路径**。"每帧恰一次"在**桩**上断言,不在**集成 rig** 上。
- **AC-2-27② 同理**:`test_ac227b` 的冻结跳过写在 **测试体内**(`if (!m.IsFrozen) counting.Cast(...)`),
  不是相机拒绝查询。⇒ 见 §5。

**建议**:`CountingArmQuery` 增加**帧序号**自检(`BeginFrame` 传入/记录外部 frame id,重复调用或漏调用即抛),
并把 `CameraArmSolver` 接进唯一相位驱动方后,在**集成层**重跑 ①。
**在接线完成前,AC-2-27① / ② 应记 `NOT-RUN` 而非借绿。**

---

## 4. `AC-2-27③` Tick 单一相位 —— **判据空转,实质 NOT-RUN**

`test_ac227c_tickHasSingleExplicitCallSite` 扫 `Camera/*.cs` 找
`void\s+(Update|LateUpdate)\s*\(\s*\)\s*\{[^}]*\bStep\s*\(`。
但**相机生产代码里根本没有任何 `Step(` 调用点**(§3 已证)⇒ 该正则**永不匹配** ⇒ `violations` 恒空 ⇒ **恒过**。
判据通过的原因是"它找的东西全仓不存在",不是"调用点正确"。

**AC 原文要求的两件事都未交付**:
- "调用点数 == 1" —— **没有做任何调用点计数**(只做了"是否在裸 Update 内"的**存在性**检查)。
- "有显式相位声明" —— **无任何相位声明落地**。

**与 002 的一致性**:002 已选 **B 路**(`CameraRig.cs:185-191` 注释逐字落码,承 EC-2-14 要求),
即"由单一显式调用点调用 `UpdateYaw()`"。但全仓**没有任何驱动方**调用 `UpdateYaw()`
(生产侧零调用点)。⇒ story 承诺的**"002 ↔ 005 探针互指、防各自测一半"并未实现**;
002 落的是"决定",005 落的是"测试",而**中间驱动方缺席**。
⇒ 建议:③ 记 `BLOCKED-BY: 相位驱动方未落地`;驱动方(P1b 前的 `ITickProvider` / `onAfterUpdate` 注册点)
落地前不得判绿。

> 附:story 要求的 `physx_query_budget_test.cs` **不存在**(全仓 find 零命中)。
> 其承载的"三档 × {静止/转场/遮挡} × ≥10³ 帧矩阵"被压缩为 `camera_mode_machine_test.cs` 里两条
> 1000 帧平凡循环。⇒ Test Evidence 面**低于 story 自订规格**。

---

## 5. `IsFrozen` 与 004 臂收缩 —— **责任无主(跨 story 空洞)**

**结论:`IsFrozen` 在生产侧零消费者;冻结档不查询的责任**不属任何人**。

- `CameraModeMachine.IsFrozen`(`:143`)全仓仅被**测试**读(`camera_mode_machine_test.cs:182,263`)。
- `CameraArmSolver.Step`(《004 的收缩本体》)**无冻结概念** —— `:137` 无条件
  `_query.Cast(...)`,即便调用方想守"冻结不查询"也拦不住。
- 005 说"本故事只要求 004 的『每帧恰一次』**不随档位变化**";004 说 `AC-2-27②`(Casebook == 0)
  "是 story 005 的**计数器判据**"。⇒ **两边互相指认对方** ⇒ 跳过逻辑落在**测试体**里。
- 这正是 `AC-2-27②` 原文自己警告的"**EC-2-11 的冻结档没有 AC ⇒ 无人守**"路径 —— 它**仍然无人守**。

**责任应落**:建议明确归 **004 的 `CameraArmSolver`**(由 `_p` 或独立入参接收"是否冻结"并短路),
或归**唯一相位驱动方**(唤起前查 `IsFrozen`)。**不得留在测试里**。
**顺带一处规格措辞缺陷**:`AC-2-27①` 写"不因**档位**变化",`②` 写"`Casebook` 帧 == 0" ——
① 与 ② 在字面上**互斥**(① 说跨档恒定、② 说 Casebook 变 0)。测试私下把 ① 的作用域收窄为"非冻结档"
才自洽。⇒ ① 的措辞应改为"不因**转场 / 遮挡状态**变化(非冻结档内)"。

---

## 6. 与 001 / 002 / 004 的接缝

| 接缝 | 状态 |
|---|---|
| **001**(程序集边界扫描) | **通过** —— `camera_presentation_discipline_test.cs:67-70` 的 `AllowedCameraStateTypes` 已含 `CameraModeMachine` / `CameraModePriority` / `CameraModeRequest` / `CountingArmQuery`;005 的全部新类型在 001 扫描面内,无逃逸。 |
| **002**(`TRANSITION_JUMP_EPS` 同一常量实体) | **未兑现** —— 全仓 `TRANSITION_JUMP_EPS` **零命中**(find/grep 皆空)。`test_ac218c` 用 `1e-5f` **硬编码字面量**做断言容差,而非引用 story 002 的"全仓一处字面量"。`AC-2-18③` 的"逐帧位移 ≤ `TRANSITION_JUMP_EPS`"**未按原文实现**(只断言 `from` 取值,未做帧间位移采样)。 |
| **002**(相位互指) | **未兑现** —— 见 §4。 |
| **004**(档位参数表 = "唯一档位参数源") | **出现第二载体** —— `CameraArmParams` 自陈是"**唯一**档位参数源",但 `CameraModeMachine.CasebookPitch`(`PITCH_CASEBOOK`)现是**第二处**逐档常量,且 `TransitionFrom/To` 是**单个 float** `[0,1]`。005 声称"转场中臂参数插值(插值只发生在参数侧)",但机器只产出一个标量 `t`,`CameraArmSolver` 不接收 `t`,四个臂参数(`ArmLen`/`ShoulderLateral`/`ShoulderHeight`/`FovV`)**无插值映射**。⇒ 插值缝"已声明未连接"。 |

**补充(AC-2-18 覆盖缺口)**:
- ① 穷举表实为 **9 情形(3×3)**,非 story 承诺的 **18**(缺"转场两相"中的转场中相位)。
- ④ 缺 story QA 明确要求的"`Treatment`+`Explore` **到达序互换 ⇒ 结果相同**"边用例(grep 互换/Reverse 零命中)。
- 这两条正是 `EC-2-9` 订正要抓的对象,缺失使 ④ 的"活锁"防线**只覆盖一半**。

---

## 7. 其他发现(低优先)

1. **Completion Notes 事实性偏差**:称"**13 例**",实测 `[Test]` 计数 = **12**(`grep -c "\[Test\]"` 与
   `grep -c "public void test_"` 均为 12)。
2. **Completion Notes 半空**:第二处 `Criteria:` / `Deviations` / `Test Evidence` / `Code Review`
   四个字段全为 `_待填_`,而 story `Status: Complete ✅`。⇒ 收口记录**不完整**。
3. **`Settle` 返回值语义误导**:`return has && TransitionRestarts > 0 && TransitionT == 0f`
   —— `TransitionRestarts > 0` 是**累计**谓词,首次转场后**恒真**,故该返回值**不等于"本帧是否变更"**。
   当前无消费者,但字段名/文档会误导实现期调用方。建议删或改为真正的"本帧变更"布尔。
4. **`_pending` 无界**:`Settle` 未被调用时 `List` 无上限增长。当前无生产驱动方(§4)⇒ 潜伏;
   接线后须确认"每帧必 `Settle` 一次"由驱动方保证。
5. **`test_ac218c` 的判别力经核实有效**(非恒真):先取打断前 `midValue`,断言打断后 `TransitionFrom == midValue`;
   若实现"先推进 `t` 再裁决"(即 Completion Notes 所述初版缺陷),`from` 会差一帧 ⇒ 断言失败。
   **该测试确能抓到该 bug**,记录在案。

---

## 结论与必改项

**Verdict: CONCERNS。** 005 的**承重交付(意图制单一写入点)已真落地并经独立核实**,
B2 修复可信、001 面覆盖完整 —— 这是本 story 的实质成果。但**判据强度普遍低于其自身 AC 原文**,
且跨 story 责任有真空。按严重度排序:

| # | 必改项 | 级别 | 归属 |
|---|---|---|---|
| A | `FirstPerson => 3` 与 `Casebook` 同值 ⇒ VR/脉案可互抢,违 R-2-9 / EC-2-11 | **BLOCKING(P1a 前)** | 2 侧(建议甲案:独立路径早退) |
| B | `AC-2-27②`(冻结不查询)责任无人承担,跳过逻辑在测试里 | **BLOCKING** | 004 `CameraArmSolver` 或相位驱动方 |
| C | `AC-2-27③` 判据空转(正则恒不匹配)+ 无调用点计数 + 驱动方缺席 | **BLOCKING(须转 NOT-RUN)** | 2 侧 + 相位驱动方 |
| D | `AC-2-27①` 在桩上断言、未接线真实求值路径 | **BLOCKING(须转 NOT-RUN 或接线)** | 2 侧 |
| E | `AC-2-17` 第二半判据为 grep 且查错方法(`SetMode` 而非 `Settle`) | 高 | 2 侧 |
| F | `TRANSITION_JUMP_EPS` 未作为同一常量实体落地;③ 的帧间位移采样未实现 | 高 | 2 侧(承 002) |
| G | 004 档位参数插值缝"已声明未连接"(`t` 标量无映射) | 中 | 004 + 005 |
| H | `CasebookPitch` public 可写(第二档位写口) | 中 | 2 侧 |
| I | ④ 缺到达序互换用例;① 穷举缺转场相位(9 非 18) | 中 | 005 测试 |
| J | `AC-2-27①` 措辞与 ② 字面互斥 | 低 | GDD 措辞 |
| K | Completion Notes"13 例"实为 12;四字段 `_待填_` | 低 | 005 文档 |

**核心判断**:005 不是"没做",而是**"做了但没锁死"** —— 状态机本体形状正确,
风险集中在**性能义务面(§3/§4/§5)的判据空转与责任真空**。这三条若不修,
`AC-2-27` 的三条子判据在**生产集成后可能全部失守而测试仍绿** —— 这正是 001 轮
"闸门虚设"的同一失效模式,须在本轮闭合。

**未验证项**:真 Unity 编译/运行未执行(无 batch 独占条件);
`AC-2-27` 的集成行为(接线后)未实测 —— 上述 B/C/D 的最终严重度取决于接线时的实测结果。
