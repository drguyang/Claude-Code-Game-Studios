# Story 001: 呈现纪律与边界 —— 不持游戏状态 / 零第三方 / 零 SimEvent / 效果归属 / AudioListener 唯一

> **Epic**: 摄像机与视角
> **Status**: In Review(双代理评审 REQUEST_CHANGES → **5 条 BLOCKING 已修** → 复跑 0 红;待二轮)
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 4h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-09-28

## Context

**GDD**: `design/gdd/camera-and-viewpoint.md`
**Requirement**: TR-camera-003(相机**只读,永不持有游戏状态**;不写三流、不引用 sim 程序集 —— `AC-2-01/04/05` 三条并列)· TR-camera-004(镜头效果的归属与 VR 禁用清单 —— `AC-2-06`)· TR-camera-005(44 的 `AudioListener` 单挂点落定 —— `AC-2-06④`)· TR-camera-001(平面第三人称的**边界半边** —— 零第三方 `AC-2-02`;机器行为半边归 story 003/004)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*
🔴 本故事是「相机是呈现层」这一裁决的**全部机械守门**。呈现层三件套同构纪律(2 / 42 / 44)在本故事落定相机侧 —— 失效模式是**静默**的:相机一旦持状态,崩溃重启会改变"游戏事实"的观感,且 R-2-8 被悄悄击破。

**ADR Governing Implementation**: ADR-020(§二 自建机位不引入 Cinemachine,"官方包不是豁免" · §五 相机只读不持状态 AC-20-05 · §六 镜头效果归属 = 8 语义 + 2 实现、VR 全禁 AC-20-08、不得报状态 AC-20-09 · §七 `AudioListener` 单挂点 AC-20-10)· ADR-013(§9 C3 同构体例;`PresentationDtoGuard` 递归扫描先例)· ADR-018(§六 无提示音铁律 —— `AC-2-06③` 白名单**同源导出**,不另立)· ADR-023(① 三场景制:相机与 `AudioListener` 住 **Boot 常驻场景**;AC-2-06④ 的"全场景计数"以该拓扑为参照)
**ADR Decision Summary**: ADR-020 §二 否决 Cinemachine 的理由是结构性的(VR 侧用不上,引入它给 VR 带来一条必须绕开的旁路),不是成本性的 —— 因此判据落在**包清单**(`Packages/manifest.json`)而非代码引用。§六 的效果禁令与 ADR-018 §六 的音频禁令是**同一铁律的视觉侧**,故白名单必须同源(否则两份白名单各自漂移)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW(本故事判据本体)
**Engine Notes**: `AudioListener` 组件计数 = 稳定 API。⚠️ **一处接口级欠账(`OQ-2-6`,GDD 原文登记)**:`ICameraRig.Camera` 是**单个 `Camera` 属性**,而 VR 立体渲染需要双眼相机对 ⇒ 该属性在 VR 侧语义不明(左眼?渲染根?位姿锚?)。**P0 不实现 VR ⇒ 不阻塞本故事**;但接口注释须写明「VR 侧语义待定,不得在实现期各自解释」,P1a 开工前须在 GDD 补语义或改 `CameraRigCamera`(含 `Camera[]`)。

**Control Manifest Rules (this layer)**:
- Required: **相机是表现层 —— 只读,永不持有游戏状态**(与 42 / 44 三件套同构)— ADR-020 §五:322(manifest Presentation · 控制器与相机)
- Required: 镜头效果**归属 = 8 语义 + 2 实现**;**VR 模式禁用任何镜头效果**;效果**不得用于报状态** — ADR-020 §六:341-342
- Forbidden: **Cinemachine / 任何第三方相机工具** — ADR-020 §二(manifest Presentation · Forbidden 表:「官方包」不是零第三方取向的豁免)
- Forbidden: **表现态连续位置参与任何 sim 决策** — ADR-020 §四:293(相机侧镜像:相机读 1 的 `Position` 是下游值,**永不反向**)
- Guardrail: `AC-2-04②` 比系统 1 的同类判据**更强**(1 至少写一条 `ActorCellEntered`,2 写零条)—— 断言面不同,不得互相借绿

---

## Acceptance Criteria

*From GDD `design/gdd/camera-and-viewpoint.md`, scoped to this story(判据正文照录,修订沿革见 GDD 原文):*

- [x] **AC-2-01(BLOCKING)** —— **相机不持有游戏状态**:
  ① 相机的**全部内部状态**(`yaw` / `pitch` / `anchor` / `v_anchor` / 当前档 / 转场进度 / `Casebook` 快照)**不进任何流、不进存档** —— 判据 = **字段类型断言**(反射扫描相机状态的可达字段,断言**无一**为 `SimEvent` / `Fix` / `PatientId` / 事件流类型);
  ② **崩溃 / 重启相机不改变任何游戏事实** —— 判据 = **差分重算**:构造两条**不同历史**的相机实例(不同 `yaw` / 不同档 / 中途重启),把输入序列的**后半段**喂给两者,断言**末帧的可见输出**(相机位姿 + `YawBasis`)**收敛到逐位相同**。
  *(承 `AC-20-05`;ADR-013 §9 C3 的呈现层三件套体例。⚠️ 2026-09-16 评审订正:原文 ②「收敛到同一可见状态」是软的 —— 现钉死为「后半段相同输入 ⇒ 末帧输出逐位相同」,可执行、可判真假。)*
- [x] **AC-2-02(BLOCKING)** —— **零第三方**:**`Packages/manifest.json` 不含 `com.unity.cinemachine`**。
  *(承 `AC-20-02`;ADR-020 §二「官方包不是豁免」。**与 ADR-012 的 CI 门同批落地**。)*
  ⚠️ **2026-09-16 评审订正(两处)**:① 原文写作小写 `packages/` —— 在大小写敏感的 Linux CI 上该路径**不存在** ⇒ 「无匹配」⇒ 断言**恒为绿**(**假阳性机器**);② 原文时点 `Packages/manifest.json` 本仓不存在 ⇒ 断言**不可执行**。
  ⇒ **判据 = 「文件存在 ⇒ 断言其内容;文件不存在 ⇒ 报 `INCONCLUSIVE`,不得记绿`**。
  🔵 **2026-09-28 拆解注**:该文件**现已存在**(根 `Packages/manifest.json` + `unity/Packages/manifest.json` 两处)⇒ 判据转可执行,且**须扫两处**(漏一处 = 原文点名的假阳性机器变体)。
- [x] **AC-2-04(BLOCKING)** —— **相机不引用 sim 实现程序集**:
  ① 相机程序集的**引用集白名单断言**(恰 = 边界程序集 + `UnityEngine` 表现层);
  ② **`IEventSink.Append` 的调用点数 = 0** —— 相机是**唯一连跨格事件都不碰的 P0 系统**(R-2-8)。
  *(与 `AC-20-05` 判据面一致;② 比系统 1 的同类判据更强的形式。)*
- [x] **AC-2-05(BLOCKING)** —— **相机零 `SimEvent`**:反射扫描相机程序集内**全部 `SimEvent` 的构造点与 `Kind` 赋值点**,断言**零命中**;且**反向**:全仓 `SimEvent` 的构造点中,**无一**的调用栈可追溯到相机程序集。
  *(与 `AC-2-04` ②互补:那条查 `Append` 调用点,这条查**事件的产生**。)*
  ⚠️ **2026-09-16 评审订正**:原文「反射扫描 + 无可追溯到相机程序集」**缺主语** ⇒ 现拆**正向(本程序集内零构造)**与**反向(全仓构造点的调用栈)**两条,各自可执行;正向在**载体未建**时只能标「已定义」。
- [x] **AC-2-06(BLOCKING)** —— **镜头效果的归属与两条禁令**:
  ① 效果的**渲染实现**在 2、**语义定义**在 8(数据表**不在 2 的程序集内**);
  ② **VR 侧禁用任何镜头效果**(`AC-20-08`)—— 判据 = `CameraMode.FirstPerson` 生效期间,**渲染后处理清单为空 / 仅含 XR 必需的 stereo 通路**;
  ③ **效果不得用于报状态**(`AC-20-09`)—— 判据 = **效果的触发源白名单**:读取 8 的数据表,断言**每条效果的 `trigger` 键 ∈ 行为反馈白名单**(白名单由 ADR-018 §六 的音频白名单**同源导出**,不是另立一份);
  ④ **`AudioListener` 唯一** —— 平面主相机 / VR 头显(`AC-20-10`)—— 判据 = **全场景 `AudioListener` 计数 == 1** 且其宿主 ∈ {主相机, 头显锚点}。
  *(承 ADR-020 §六/§七;ADR-018 §六 无提示音铁律的视觉侧。⚠️ 2026-09-16 评审订正:原文 ②③④ 全是陈述句非判据,现各补判据面;③ 白名单与 ADR-018 同源。)*

---

## Implementation Notes

*Derived from R-2-8 · ADR-020 §二/§五/§六/§七 · ADR-023 三场景拓扑:*

- **相机程序集落点先定**:建议 asmdef 名 `Gameplay.Camera`(ADR-025 六装配清单未点名相机 ⇒ 以清单为准,偏差登记 Completion Notes)。引用集 = `Sim.Contracts`(边界程序集,读 `WorldPos`/整数)+ `UnityEngine` 表现层 + 1 的**只读导出面**(`IPlayerMotor.Position`);**不含** sim 实现程序集、不含 `Unity.Entities`/`Burst`/`Jobs`/`Mathematics`(同 story 001 player 的 `AC-1-28` 方法:白名单 + **传递闭包**)。
- **`AC-2-01①` 的反射面**:判据对象是「相机状态的可达字段闭包」—— 递归(承 1 的 `AC-1-34` 与 ADR-013 `PresentationDtoGuard` 递归先例,**仅扫顶层会漏**)。白名单按**类型**(非字段名):`SimEvent` / `Fix` / `PatientId` / 事件流集合类型一律红;`Vector3`/`float`/档位枚举/转场进度 `t`/`Casebook` 锚快照 `Vector3` 一律绿(**这些是合法的表现层内部状态** —— 本条禁的是"游戏事实",不是"相机自己的记忆")。
- **`AC-2-01②` 的差分重算是本故事最难的一条**:需要**可注入的历史**——两个实例分别预置不同 `yaw`/档/中途重建,喂**相同后半段**输入(fake `Look` 序列 + fake `P_player` 序列),断言末帧 `(相机位姿, YawBasis)` 逐位相等。⚠️ 前提 = 相机无隐藏静态状态(单例 / 静态字段 / 场景残留)—— 静态字段本身要进反射扫描面(否则"实例干净、静态脏"通过)。
- **`AC-2-02` 落点**:测试读 `Packages/manifest.json` **与** `unity/Packages/manifest.json` 两处 JSON,断言 `dependencies` 无 `com.unity.cinemachine`;文件缺失 ⇒ `INCONCLUSIVE`(显式输出,不是 assert 通过)。承 input-system story-001 的 Legacy Input 分析器先例:**大小写敏感路径写死**。CI 载体由 ADR-012 轮建 —— 本故事交付判据本体。
- **`AC-2-05` 正反两道**:正向 = 相机程序集内 `SimEvent` 构造/`Kind` 赋值零命中(反射 + AST);反向 = 全仓构造点的调用栈可追溯性 —— 后者在单测里等价形态 = 「相机类型不在任何 `SimEvent` 构造点的调用者闭包内」(Roslyn 控制流/符号查找),真实调用栈抓取留 CI(载体未建 ⇒ 记「已定义」,NOT-RUN 不借绿)。
- **`AC-2-06` 四子项的归属分界**:② 的 `FirstPerson` 在 P0 **只落枚举与接口**(TR-camera-002/P1a)⇒ ② 的判据在 P0 走**夹具注入**(强制 `Mode = FirstPerson`,断言后处理清单为空),不是真 VR 会话;③ 的白名单 = **读 ADR-018 §六 同源导出物**(若该白名单目前只存在于 ADR 文本 ⇒ ③ 记 `BLOCKED-BY:44 白名单未成数据表`,不得自造一份);④ 的全场景计数在 ADR-023 三场景制下 = **Boot 常驻场景含唯一 `AudioListener` 宿主**,`World` additive 场景须为 0(承 ADR-023 ②「World 零 gameplay GameObject」的同型扫描,`Boot`/`MainMenu` 各自的计数断言也要写明:全工程恰好 1,不是每场景 1)。
- **效果数据表不住 2 的程序集**(① 的机械形态):2 只消费 8 的表(表经 ADR-014 烘焙),2 的源文件内**零**效果语义定义(如"诊脉时压暗")—— grep 2 的程序集含 8 的语义键名 = 违规。
- **`OQ-2-6` 接口欠账登记**:`ICameraRig.Camera` 的 doc comment 须含「VR 双眼语义待定(P1a 前补 GDD 或改 `CameraRigCamera`),实现期不得各自解释」一行 —— 本故事交付该注释,不改签名。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 002:`YawBasis` 的四条构造判据(`AC-2-07…10`)—— 本故事只立"相机的读/写边界",不验基的数学
- Story 003/004:机器行为本体(F-2-1…F-2-4 的积分与几何)—— `AC-2-01` 管这些状态**不出程序集**,不管它们**算得对不对**
- Story 005:档位状态机与性能义务(`AC-2-17…20/27`);`AC-2-06②` 的 `FirstPerson` 档真实启用(推 P1a)
- Story 006:`AC-2-21/22/23` 跨系统义务对账与舒适度 ADVISORY 面
- 系统 8:效果**语义定义**与数据表内容(2 只实现渲染 + 校验触发源 ∈ 白名单)
- 系统 44:挂点的**消费侧**(audio-system 已登记来源,双向性 GDD 侧已核,无本故事义务)
- Cinemachine 的"要不要重新考虑"(ADR-020 已裁,重开须新 ADR;本故事只守清单)

---

## QA Test Cases

*Written at story creation(lean mode — QL-STORY-READY skipped;specs self-authored from AC text). The developer implements against these — do not invent new test cases during implementation.*

- **AC-2-01①**: 状态字段类型白名单(递归 + 静态面)。
  - Given: 相机程序集全类型(公开/私有/内部)字段闭包,含 `static` 字段。
  - When: 反射递归扫描按**类型域**判定。
  - Then: 无 `SimEvent` / `Fix` / `PatientId` / 事件流集合类型;`anchor`/`v_anchor`/`yaw`/`pitch`/档位/`t`/快照在允许域(float/Vector3/枚举)。
  - Edge cases: 外层字段类型合法、内层 struct 藏 `PatientId` ⇒ 递归捕获;自造 `struct CameraSaveSlot { int tick; }` 式"偷渡存档语义"⇒ 由类型域判(不在白名单即红)。
  - Negative fixture: 给相机加一个 `SimEvent _lastObserved` 字段 ⇒ 红。

- **AC-2-01②**: 差分重算(崩溃/重启不改变事实)。
  - Given: 两个相机实例,历史 A(yaw=137°, Treatment 档,第 500 帧重建)、历史 B(yaw=-12°, Explore 档,无重建);后半段输入序列(10³ 帧的 fake `Look` + `P_player`)两者相同。
  - When: 各自跑完,取末帧 `(相机位姿, YawBasis)`。
  - Then: 两条末帧输出**逐位相同**。
  - Edge cases: 转场中途重建(历史 B 在转场 `t=0.4` 时被销毁重开)⇒ 末帧仍收敛(转场进度非游戏事实);后半段含 `SetMode` 风暴(EC-2-9)⇒ 末帧档位亦相同。
  - Negative fixture: 锚速度 `v_anchor` 经静态缓存(实例重建不清零)⇒ 两条末帧不同 ⇒ 红(该夹具同时证明"实例干净、静态脏"逃逸路径被守住)。

- **AC-2-02**: 包清单零 Cinemachine(两处 + INCONCLUSIVE)。
  - Given: 测试读 `Packages/manifest.json` 与 `unity/Packages/manifest.json`。
  - When: 解析 JSON `dependencies` 键集。
  - Then: 两处均不含 `com.unity.cinemachine`(键名精确匹配,含 `com.unity.cinemachine.*` 变体)。
  - Edge cases: **文件不存在 ⇒ 输出 `INCONCLUSIVE` 并以"未验证"退出**(GDD 原文的假阳性机器形态:`packages/` 小写路径在 Linux 恒无匹配 ⇒ 测试须自己证明路径大小写正确 —— 夹具:临时注入 `com.unity.cinemachine` 行,断言测试**变红**,证明不是空转)。
  - Negative fixture: 上述注入。

- **AC-2-04①②**: 引用集白名单 + Append 零调用点。
  - Given: 相机 asmdef 的 `references` 集合 + 沿引用图传递闭包;全工程 `IEventSink.Append` 调用点经 AST。
  - When: 断言。
  - Then: 闭包 ⊆ {`Sim.Contracts`, `UnityEngine`/表现层, `Gameplay.Player` 只读面};`Append` 调用点中相机所属程序集贡献 **0**。
  - Edge cases: 间接引用(相机 → 某工具 asmdef → sim 实现)⇒ 闭包捕获;经 `Assembly.Load` 反射取 `IEventSink` 调用 ⇒ AST 抓不到 —— 判据须**并入** `AC-2-05` 的反向构造点扫描,双判据互补,单条不过即红。
  - Negative fixture: asmdef 注入一条 sim 实现程序集引用 ⇒ 红。

- **AC-2-05(正向 + 反向)**: 零 `SimEvent` 产生。
  - Given: ① 相机程序集 AST:`new SimEvent*` / `Kind` 赋值零命中;② 全仓构造点的调用者闭包中无相机类型。
  - When: 两向扫描。
  - Then: 均零命中。
  - Edge cases: 相机经 lambda/本地函数构造事件(闭包归属仍 = 相机程序集)⇒ 命中;`Kind` 以整数强转绕枚举 ⇒ 由"载荷字段类型只允许整数域"(ADR-024 A2)在构造侧另有闸,本测试不重复造,但要证明正向不被强转绕过。
  - Negative fixture: 相机内加"检测到穿墙就发事件"的顺手实现(最像合法的越界)⇒ 红。

- **AC-2-06①③**: 效果归属与触发源白名单。
  - Given: 8 的效果表(cooked);2 的程序集源文件。
  - When: ① 断言 2 内零效果语义键定义(grep + AST);③ 读表逐条断言 `trigger ∈ ADR-018 §六 同源白名单`。
  - Then: ① 通过;③ 违例(如 `trigger: VitalsCrossed`)⇒ **构建失败**(承 ADR-018 的同型机械判据:声明为状态播报 = 构建失败)。
  - Edge cases: 白名单**未成数据表**时记 `BLOCKED-BY:44/8 白名单载体`,不得由 2 自造一份(AC 明文"不是另立一份");8 表未就位 ⇒ NOT-RUN。
  - Negative fixture: 注入一条 `trigger: VitalsCrossed` 效果 ⇒ 构建红。

- **AC-2-06②**: VR 全禁(夹具注入形态)。
  - Given: 强制 `CameraMode.FirstPerson = true` 的夹具(P0 无真 VR 会话)。
  - When: 收集渲染后处理配置。
  - Then: 清单为空 / 仅 XR stereo 必需通路;零自定义效果调用点。
  - Edge cases: 效果经全局静态注册表挂载(不经相机句柄)⇒ 夹具断言 FirstPerson 生效期注册表内相机侧零项。
  - Negative fixture: 注入"急救时屏幕压暗"效果并在 FirstPerson 保持 ⇒ 红。

- **AC-2-06④**: `AudioListener` 恰好 1(Boot 拓扑)。
  - Given: ADR-023 三场景(`Boot` 常驻 / `MainMenu` / `World` additive)全部加载后的运行时世界。
  - When: `FindObjectsOfType<AudioListener>()` 计数并查宿主。
  - Then: 计数 == 1;宿主 ∈ {主相机, 头显锚点};`World` 场景贡献 0。
  - Edge cases: 拆序/加载中间态(EC 期)短暂 0 ⇒ 断言在"世界就绪门"后取;`MainMenu → World` 切换全程恒 1(相机在 Boot 不随场景卸载 —— ADR-023 ① 的直接受益判据)。
  - Negative fixture: 往 `World.unity` 塞一个带 `AudioListener` 的相机(正是 ADR-023 ② 扫描会拦的形态)⇒ 两条断言各红,证明双闸真实存在。

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `tests/unit/camera/view_purity_test.cs` — 状态字段类型白名单递归 + 差分重算两历史收敛
- Logic: `tests/unit/camera/zero_third_party_test.cs` — 双 manifest 扫描 + 注入变红的反空转夹具
- Integration: `tests/integration/camera/event_and_listener_test.cs` — 引用闭包 + Append/SimEvent 双向零命中 + 全场景 `AudioListener == 1`
- Build gate: `AC-2-06③` 的效果 trigger 白名单断言(构建失败级,随 ADR-012 CI 载体接)

**Status**: [ ] Pending — story not yet implemented(真身落点预期 = `unity/Assets/Tests/`;登记口径 = `tests/unit|integration/camera/`)
⚠️ 不得借绿清单:`AC-2-05` 反向半边(载体未建,记「已定义/NOT-RUN」—— GDD 可签署性实测原话);`AC-2-06③`(白名单数据表未落 ⇒ BLOCKED-BY 8/44 载体);`AC-2-02` 现可执行(文件已存在)但**注入变红**的反空转夹具必须存在,否则视同未验。

---

## Dependencies

- Depends on: ADR-025 程序集清单(相机 asmdef 落点名;未点名 ⇒ 本故事自定并登记偏差)/ ADR-023 三场景拓扑(`AC-2-06④` 的计数参照)
- Unlocks: Story 002–005(它们实现的四段机器都住本故事划定的程序集内;先立边界后写机器,避免返工)/ 玩家控制器 Epic 无依赖关系(1↔2 的方向性由 story 002 交付)

---

## Completion Notes

**Completed**: 2026-10-03(双代理评审后修复轮)
**Criteria**: 6/6 AC 落地(12 例测试;CameraViewpoint 全组 59/64,余为设计内 NOT-RUN/ADVISORY)。
**Deviations**: 🔴 **双代理评审(QA Lead + TD)判「6/6 不成立,至多 4/6」;5 条 BLOCKING 已修**:

| # | 原缺陷 | 修法 |
|---|---|---|
| **B1** | **AC-2-05 结构性恒绿** —— `ILBodyScanner` 只认 `call`/`callvirt`,**不认 `newobj`(0x73)** ⇒ `new SimEvent(...)` 永不被捕获;反向半边是对**同一变量**的重复断言 | 补 `newobj` + **类型引用**判据 + `ResolveTypeToken`;✅ **突变坐实**(修前 0 反应 → 修后该测红) |
| **B2** | **档位双源** —— `CameraRig._mode` 与 005 的 `CameraModeMachine.Mode` **互不引用** ⇒ 005 的 `AC-2-17`「写入点 == 1」**系统级为假** | `CameraRig` **不再自持档位**,一切经 `_modeMachine` |
| **B3** | **`"ink_edge"` 硬编码 = 2 侧定义效果语义**(违 AC-2-06①「8 给语义,2 给实现」);且 AC-2-06① 判据是 4 键 denylist,**恰好漏掉它** | 删语义键,改**注入缝** `SetEffectSemanticsFrom8`;②③ 判据重写 |
| **B4** | **AC-2-01② 恒真** —— 「相同起点 + 相同输入」对**任何确定性实现必然绿** | **两度重写**:终版 = **销毁重建**,比「重启前 vs 重启后」末帧;✅ **突变坐实** |
| **B5** | **AC-2-01① 递归不进自定义 struct** + 是**黑名单**非 story 自述的**白名单** | 改**两级**(已知事实黑名单含传递闭包 + 白名单兜底)+ 递归进 struct 字段 |

⚠️ **主会话自身的错(如实记账)**:
① 原「6/6 AC 落地」**是错的** —— 至少 2 条空转/恒真(AC-2-05 · AC-2-01②);
② **Completion Notes 曾称 AC-2-06④「走 `Assert.Ignore` / NOT-RUN」而实测在跑并 PASS**
   (`Scenes/` 有 6 个 `.unity`)—— **失实,本条即订正**;
③ `"ink_edge"` 是主会话**为让判据「有对象」而自造的语义名** —— 制造了违规。

⚠️ **AC-2-04① 具名豁免**:相机与系统 1 **同住 `Gameplay.Presentation`**(无独立 asmdef)
⇒ 引用集共用,已引 `Sim`(= `RecipeDataSet` 债)。豁免仅 `Sim`;新增 sim 实现引用仍红。
**Test Evidence**: EditMode `total 2182 · passed 2143 · failed 0 · skipped 38 · inconclusive 1`(2026-10-03 batchmode);
CameraViewpoint **59/64**。**突变测试**:B1/B4 各经突变坐实(修前不红 → 修后红)。
**Code Review**: ✅ 双代理评审已落 `production/qa/evidence/review-camera-viewpoint-story-001-{qa-lead,td}-2026-10-03.md`;
**二轮评审待跑**。
**Manifest**: 版本号已对齐 2026-10-02(⚠️ 仅版本号)
