# Story 006: 跨系统义务对账与舒适度签核面 —— AC-2-22 六子义务 / EXTERNAL 挂账 / ADVISORY playtest 面 / VR 接口

> **Epic**: 摄像机与视角
> **Status**: Ready
> **Layer**: Feature
> **Type**: Visual/Feel
> **Estimate**: 6h(+ playtest 场次,不可自动化部分不占实现工时)
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-28

## Context

**GDD**: `design/gdd/camera-and-viewpoint.md`
**Requirement**: TR-camera-002(VR = 第一人称、不承担开放世界移动;**P0 只落接口** —— 本故事交付枚举/接口面与冻结语义的登记,不落实现)· TR-camera-004 / TR-camera-005 的**对账侧**(效果归属与挂点的下游义务闭合核对;判据本体在 story 001)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*
🔴 本故事是 Epic 的**收尾对账件**:它不写机器,它验收「机器之外的账是否齐」—— `AC-2-22` 的六子义务(**BLOCKING,缺一即失败**,判据 = 对方 GDD §Dependencies 反向引用)、两条 EXTERNAL 挂账(`AC-2-21`/`AC-2-23`,不计入就绪度但必须显式登记)、三条 ADVISORY 手感门(`03/24/26`,P0 **舒适度的全部签核面**——`AC-2-03` 补注原话:平面舒适度没有任何 BLOCKING 门,这是有意的,但须 spike 报告**显式签核**,否则「不晕」变成无人负责的口号)。

**ADR Governing Implementation**: ADR-020(§三 视角双路径:VR 独立第一人称实现推 P1a;§Migration 第 5 条相机 spike = `AC-2-23`/`O-13` 的**唯一验收证据**;`AC-20-12` 相机 spike + `ux-designer` 签核 —— GDD 点名该 ADR↔GDD 覆盖缺口:**`AC-20-06/07/11/12` 在本 GDD 无对应 AC**,`AC-20-12` 正是本故事的签核载体)· ADR-013(`ModalId` 闭集 = 39 发 `Casebook` 的呈现侧;42 的 `FOV_v` 引用已履行)· ADR-015(§一 烘焙逻辑层几何 = `O-13` 落点侧;接收方按评审订正 = **「关卡内容工作 + ADR-015 §一」**,原文「26 关卡/24 医馆即机器」为不存在的接收方 —— 以 `systems-index.md` 权威表为准)· ADR-011(手柄绑重:`Navigate`/`Look` 互斥的对侧已登记于 `input-system.md` 规则三,2026-09-20 回刷)
**ADR Decision Summary**: EXTERNAL 的定义(GDD 收尾口径)= **「义务已定义、裁决点在别处」** —— 与系统 1 的 `AC-1-22(EXTERNAL)` 同型。`AC-2-21` 与 `AC-2-09` 的配对关系是裁决的关键一刀:「填了就必须对」(BLOCKING,story 002/003 已签形状)与「还没填 ⇒ 1 的 `OQ-1-14` 仍悬着」(EXTERNAL,本故事登记)是两条不同问题,**不可互相替代**;EXTERNAL 改判(2026-09-16 由 BLOCKING)**不使义务消失,只把它移出就绪度计数**。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW(对账与枚举面)/ 签核面不涉引擎 API(playtest/spike 为人工+报告)
**Engine Notes**: VR 接口面 P0 只落 `CameraMode.FirstPerson` 枚举与链冻结的**结构预留**(平面链可被整体 stop —— EC-2-11 语义);`OQ-2-6`(`ICameraRig.Camera` 单相机 vs 双眼)的接口扩容须**不破坏平面链**(GDD 原文:P0 只需保证这一点;现在改接口近乎零成本,推到 P1a 要动 ADR-020)。spike 的取景/查询计数数据由 story 004/005 的夹具提供输入,但**spike 报告本身是人工签核件**(coding-standards:手感/视觉保真不可自动化)。

**Control Manifest Rules (this layer)**:
- Required: VR = 独立第一人称,不承担开放世界移动(晕动症);平面链与 VR 路径**独立不共享状态**(EC-2-11:共享状态 = 「摘头显回平面」画面跳变)— ADR-020 §三
- Required: 依赖必须双向 —— `AC-2-22` 判据本体:各对方 GDD §Dependencies 内出现对本 GDD 的反向引用,缺一即失败 — design-docs 规则
- Forbidden: 把 ADVISORY 三条混入 BLOCKING 计数,或用 BLOCKING 机械判据"替代"手感签核(GDD 原文:两者应答的问题不同)
- Forbidden: 把 EXTERNAL 两条记成本 Epic 的绿(不计入就绪度 ≠ 已履行)
- Guardrail: 数值/取值类裁决(PITCH_MAX、取景延迟预算、第四档与否)**永久在用户与 playtest 手里**,本故事只交付登记面与判据形状

---

## Acceptance Criteria

*From GDD `design/gdd/camera-and-viewpoint.md`, scoped to this story(判据正文照录,修订沿革见 GDD 原文):*

- [ ] **AC-2-22(BLOCKING)** —— **`O-12` / `O-13` / `O-14` / `O-15` / `O-16` 已落**:
  ① 10 在急救**开始 / 结束(含跳过路径)** 发 `Treatment` / `Explore`;
  ② **39**(⭑ 2026-09-19 唯一请求方)在脉案打开 / 关闭时发 `Casebook` / `Explore`(**8 不再发**);
  ③ **关卡内容与 ADR-015 §一 烘焙逻辑层几何**声明诊疗台的可用越肩位(2 侧持有相机 spike,其输出即验收依据);
  ④ 系统 1 侧确认 `YawBasis` 的**帧内次序契约**(`O-14`,注意已非「一帧内恒定」而是**次序不变量**);
  ⑤ ✅ 42 已声明其读取的档位 `FOV_v`(`O-15`;**2026-09-22 履行** —— `OQ-42-1` = 甲「恒定屏幕尺寸」激活该义务,42 侧已在 §Formulas F6 / §Dependencies §三 声明);
  ⑥ **系统 3 保证手柄 `Navigate` 与 `Look` 不共享物理控件**(`O-16`,2026-09-16 新增)。
  **判据 = 各对方 GDD 的 §Dependencies 内出现对本 GDD 的反向引用**,**缺一即失败**(承 `design-docs` 规则「依赖必须双向」)。
  *(当前实测(2026-09-19/09-20/09-22 三次回刷):六项义务**现已全部有对侧登记**;② 的 39 已成稿且为唯一请求方(8 已剔除),⑤ 已闭合,⑥ 的 `input-system.md` 规则三明写互斥 ⇒ ⑥ 可由红转验。⚠️ 2026-09-21 订正:`AC-2-22①` 的**判据面已可签**(载体除外)—— 原归因「主语系统无 GDD」不成立(10 已成稿 Approved)。)*
- [ ] **AC-2-21(EXTERNAL)** —— **`O-11` 的裁决点在用户手里,不在本系统**:`PITCH_MAX` 的**实际取值**须回填系统 1 的 `OQ-1-14` 并**结案**(退化分支保留为防御性代码)。判据 = 系统 1 的 `OQ-1-14` 表格行标注「已结案」且**引用本 GDD 的 `PITCH_MAX` 取值来源**。
  *(2026-09-16 评审由 BLOCKING 改判 EXTERNAL:硬约束是「数值用户自己调」⇒ 永久留白 ⇒ 用户给值前不可签署。**与 `AC-2-09` 配对**:那条管「填了就必须对」,本条管「还没填 ⇒ 1 的 `OQ-1-14` 仍悬着」。)*
- [ ] **AC-2-23(EXTERNAL)** —— **`O-13` 的验收只可能由相机 spike(ADR-020 §Migration 第 5 条)给出** —— 该 spike 的输出是 `O-13` 的唯一验收依据。
  *(2 侧不可签署:关卡内容工作 + ADR-015 §一 均无 GDD/无独立系统条目 ⇒ 与 `AC-1-22` 同型,登记外部依赖,不计入就绪度。🔴 评审订正:原文接收方「26 关卡/24 医馆即机器」**不存在**(#26 = 制服机制已并入 25;24 的依赖是 23/9/1 不是关卡几何)⇒ 接收方改述以 `systems-index.md` 权威表为准。)*
- [ ] **AC-2-03(ADVISORY)** —— **不晕**:长时 playtest(含 VR,P1a)无晕动症报告;判据 = playtest 签核,**非**自动化断言。
  *(2026-09-16 补注:本条是**平面模式舒适度的唯一门**且是 ADVISORY ⇒ P0 无可签署的舒适度 BLOCKING 门 —— 有意为之,但**须在 spike 报告里显式签核**,否则「不晕」变成无人负责的口号。)*
- [ ] **AC-2-24(ADVISORY)** —— **取景可预期**:玩家能在**不看画面**的情况下预测转头后看到什么(Player Fantasy 第二条第 1 点);判据 = playtest 签核。
- [ ] **AC-2-26(ADVISORY · 2026-09-16 评审新增)** —— **取景延迟预算**(creative-director 的重框):相机在判断链「观察」环里是**门级手感项** —— 取景到位太慢,医生**看不到**要点的人。判据 = playtest 中测 **`Look → 画面到位`** 与 **`档位请求 → 画面到位`** 两条延迟,**须在用户给定的预算内**(预算由用户定,不在 GDD 内给值);并须覆盖**转场期间输入是否被吞**(转场中 `Look` 是否仍生效 —— R-2-5 未禁)。
  *(补条理由:原 §四「相机不服务肉,只服务骨」被判定过度 —— 取景延迟在原文**零 AC**,本条补上;**不进 BLOCKING 计数**。)*
- [ ] **TR-camera-002 接口面(P0 只落接口,非编号 AC)** —— `CameraMode.FirstPerson` 枚举 + EC-2-11 冻结语义的结构预留:平面链可整体停更新、平面相机可关渲染、`AudioListener` 迁头显的**挂点归属已声明**(ADR-020 §七 由 story 001 `AC-2-06④` 守计数);**OQ-2-6 的接口扩容不得破坏平面链**(P0 义务)。

---

## Implementation Notes

*Derived from §Dependencies 登记表 · OQ 表 · 收尾口径 · ADR↔GDD 覆盖缺口注:*

- **`AC-2-22` 交付 = 一张对账表(机器可检)+ 六个反向引用的实测**:逐子项 grep 对方 GDD 的 §Dependencies 节(`emergency-procedures.md` / `casebook.md` / 关卡内容工作登记 / `player-controller-and-movement.md` O-14 行 / `skeuomorphic-ui.md` F6+§三 / `input-system.md` 规则三),断言**反向引用文本存在且指向本 GDD**。④ 的注意点(AC 原文):**次序契约**已非「一帧内恒定」—— 对账时若 1 侧仍引用旧措辞,登记为**陈旧引用**须回刷(回刷义务在 1 侧,本表只记差异)。
- **对账表的自动化形态** = EditMode 文档扫描测试(读仓库内 `design/gdd/*.md`,非游戏代码)—— 载体纪律同 1 Epic:文档在仓,扫描可跑;CI 门归 ADR-012 轮。⚠️ 该测试的失效模式是**假绿**(grep 到系统名≠反向引用成立)⇒ 判据精确为:对方 §Dependencies 节内**行级**含「camera-and-viewpoint」或「系统 2」+ 义务编号(O-12…O-16)至少其一;只 grep 文件名全文命中不算。
- **EXTERNAL 两条的登记面**:`AC-2-21` = 在系统 1 Epic 的挂账与 `OQ-1-14` 行状态间建立**互指**(本故事登记,不代裁数值、不代回填);`AC-2-23` = 相机 spike 的**验收依赖声明**(spike 输出 = 唯一证据;其夹具输入来自 story 004 的 GT 几何 + story 005 的查询计数器)。**两条均不得记入本 Epic 就绪度**(GDD 原文),evidence 目录留占位不建空文件。
- **ADVISORY 三条的签核载体**:spike 报告(ADR-020 §Migration 第 5 条 + `AC-20-12` 的 ux-designer 签核 —— GDD 点名 **`AC-20-06/07/11/12` 在本 GDD 无对应 AC** 的覆盖缺口,本故事把该缺口**登记**而非填补:填它须 ADR 轮,不在 story 权限内)。`AC-2-26` 的两条延迟测量**方法**可预定义(`Look` 阶跃 → 画面到位帧差;`SetMode` → 到位帧差,用 story 005 计数器/转场 `t` 做时间戳),**预算数值永久归用户**;转场中输入是否被吞 = playtest 观察项 + 现行为登记(R-2-5 未禁 ⇒ 默认不吞,spike 记录实测形态)。
- **VR 接口的最小交付**:枚举成员 + 「平面链整体可冻结」的结构断言(P0 夹具:强制 `FirstPerson` ⇒ 跟随/绕点/出臂/收缩四段零推进、查询计数 0 —— 与 story 005 `AC-2-27②` 的计数器共用夹具;`Casebook` 与 `FirstPerson` 都表现为 0 但语义不同,注释写明差异);**不实现**任何 XR 运行时(OpenXR 在 P0 零引用)。`OQ-2-5/2-6` 登记为 P1a 前置(与 1 的 `OQ-1-3` 同批裁 —— 两条 OQ 是同一问题的两侧)。
- **OQ 登记面的当前态(拆解注 2026-09-28)**:`OQ-2-3` 的 23 侧**已裁**(P0 建造共享 Explore,不加第四档)⇒ 本故事只登记剩余(43/采药,随 43 GDD 开写);`OQ-2-1`/`2-7`/`2-8` 同批触发 = **首次 playtest(相机 spike)**;`OQ-2-2` 触发 = 美术方向定稿且 P0 开工前(投影若改 ⇒ story 004 出臂几何与 42 的 F6 算法重核 —— 登记联动面);`OQ-2-4` 触发 = 脉案界面可用后的首次 playtest。**若 playtest 判定基建幻想不成立,问题不在相机侧**(EC-2-3 是几何问题不是代码问题)—— 该归因链写进 spike 报告模板,防"回头改收缩逻辑"的误修。
- **R-2-9 冻结链的验收半边**:「两条路径独立;共享状态会让摘头显回平面时跳变」⇒ P0 的结构断言 = 平面链状态(anchor/yaw/d/档位/t)在 `FirstPerson` 夹具下**无写入**(复用 story 001 的字段扫描 + 005 的计数器,零新夹具)。
- **本故事不动任何机器代码**;若对账发现某子项缺对侧登记 ⇒ 登记为 `O-xx` 差异并回 producer 协调,**不得由 2 侧代笔对方 GDD**(协调规则 4)。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 001:`AC-2-06④` AudioListener 计数的**判据本体**(本故事只声明 VR 迁挂点的归属与 P1a 接口面)
- Story 002/003:`AC-2-09①` 的装载期硬界(「填了就必须对」半边);`OQ-1-14` 的 1 侧结案流程归玩家 Epic
- Story 004:spike 所需 GT 几何夹具的**实现**;`OQ-2-8` 缓释的机械前提(瞬时收缩本体 —— 若裁缓解须**另开 ADR**,与 R-2-6 冲突不得在本 GDD 打补丁)
- Story 005:档位状态机与 `Casebook` 冻结(本故事对账的是**对方发请求的义务**,不是相机的响应)
- 系统 10/39/42/3/关卡内容:**对方 GDD 的文本本身**(本故事只验收反向引用存在,不代写不代改)
- VR/OpenXR 运行时实现(P1a)、第四档取景(43/采药,随 43 GDD)、淡出/半透明第二手段(`OQ-2-1`,spike 后裁)
- 取景延迟预算数值、`PITCH_MAX` 取值 —— **归用户**;playtest 的组织实施 —— 归 producer/qa 排期

---

## QA Test Cases

*Written at story creation(lean mode — QL-STORY-READY skipped;specs self-authored from AC text). The developer implements against these — do not invent new test cases during implementation.*

- **AC-2-22①–⑥**: 六子义务对账(文档级 EditMode 扫描)。
  - Given: 六份对方 GDD 的 §Dependencies 节(路径按上注);扫描器逐节抽取。
  - When: 行级断言反向引用(含 `camera-and-viewpoint` 或「系统 2」+ O 编号)。
  - Then: 六节**全部命中**(缺一即失败,输出点名的缺失项);⑤ 额外断言 42 侧声明**读哪一档**(F6 文本含档位名或「随档位」语义);② 额外断言 39 = **唯一**请求方且 8 无相机请求残留。
  - Edge cases: ④ 的措辞陈旧检测:1 侧若仍写「一帧内恒定」而非「次序不变量」⇒ 对账输出**差异行**(不静默通过,评审订正点名);⑥ 的绑重互斥若只在设计说明出现而不在 §Dependencies ⇒ 仍红(判据位置被 AC 钉死)。
  - Negative fixture: 临时移除某节的反向引用行 ⇒ 该项红且其余五项不受牵连(逐项独立断言,防整表短路)。

- **AC-2-21 / AC-2-23(EXTERNAL)**: 挂账登记(非自动化断言,交付物 = 登记行)。
  - Given: 本 Epic 就绪度表。
  - When: 审计。
  - Then: 两条**均不在** BLOCKING 完成清单内;`AC-2-21` 互指玩家 Epic `OQ-1-14` 行;`AC-2-23` 声明 spike 为唯一验收证据且接收方措辞 = 「关卡内容工作 + ADR-015 §一」(旧「26/24」作废文本不得复现)。
  - Edge cases: 用户日后填入 `PITCH_MAX` ⇒ `AC-2-21` 的结案半边转 1 侧流程,2 侧 `AC-2-09` 半边自动生效(story 002 形状已签)。
  - Negative fixture: 就绪度表把任一 EXTERNAL 计入绿 ⇒ 审计红(禁借绿)。

- **AC-2-03 / 24 / 26(ADVISORY)**: playtest 签核面(不可自动化;交付 = 报告模板与测量方法)。
  - Given: 可玩构建(stories 001–005 完成)+ 报告模板含三签名位(不晕 / 取景可预期 / 两条延迟在预算内)+ `AC-20-12` 的 ux-designer 签核位。
  - When: 长时 playtest;延迟测量:`Look` 阶跃 → 画面到位帧差、`SetMode` → 到位帧差(story 005 计数器/转场 `t` 提供时间戳);转场中输入吞否观察。
  - Then: 报告存在且三 ADVISORY 各有显式 verdict(签核 or 违例记录);**预算数值留白 ⇒ verdict 记「实测值已录,预算待用户」**。
  - Edge cases: VR 段(P1a 前)仅平面长时;`timeScale=0` 暂停场景不在测项(已裁照常,story 003)。
  - Negative fixture: 无自动负例(ADVISORY 的本质);**空报告 = 未签核**,不得以"005 全绿"替代。

- **TR-camera-002 接口面**: FirstPerson 结构预留。
  - Given: 夹具强制 `Mode = FirstPerson`(P0 无真 XR 会话)。
  - When: 喂移动/Look/请求全输入。
  - Then: 平面链四段零推进;查询计数 == 0(与 005② 共用计数器);链状态字段无写入(001 扫描夹具);`ICameraRig.Camera` 签名未因接口扩容讨论被改动(OQ-2-6 注释在位)。
  - Edge cases: 退出夹具回 Explore ⇒ 四段恢复无跳变(独立路径语义;P0 单层夹具不模拟真摘戴);`Casebook`(计数 0)vs `FirstPerson`(计数 0 + 四段冻结)语义差异有注释。
  - Negative fixture: `FirstPerson` 下继续跟随积分(共享状态形态)⇒ 无写入断言红。

---

## Test Evidence

**Story Type**: Visual/Feel
**Required evidence**:
- 文档扫描:`tests/unit/camera/obligation_ledger_test.cs` — `AC-2-22` 六子项行级反向引用断言(EditMode,读仓内 GDD;载体纪律同前:CI 归 ADR-012 轮)
- 结构预留:`tests/unit/camera/vr_interface_freeze_test.cs` — FirstPerson 夹具四段零推进 + 计数 0 + 无写入
- Manual: `production/qa/evidence/camera-viewpoint/spike-report-<date>.md` — 三 ADVISORY verdict + `AC-20-12` ux-designer 签核位 + `OQ-2-1/2-7/2-8` 的 spike 输入(报告不存在 ⇒ ADVISORY 三项记 NOT-RUN,**不得记绿**)

**Status**: [ ] Pending — story not yet implemented(真身落点预期 = `unity/Assets/Tests/EditMode/Camera/`;登记口径 = `tests/unit/camera/`)
⚠️ 就绪度口径:**两条 EXTERNAL 不计入本 Epic 就绪度**(GDD 原文);三条 ADVISORY 的可执行前提 = 可玩构建 + playtest 排期,当前一律 NOT-RUN;`AC-2-22` 的 ①–⑥ 判据面现已可签(2026-09-19/20/22 对侧登记已实测齐 + 2026-09-21 订正归因),载体外无阻塞。

---

## Dependencies

- Depends on: Stories 001–005(对账对象与夹具复用源:001 扫描面 / 002 EPS 与契约措辞 / 004 GT 几何 / 005 计数器与转场 `t`)/ TR-camera-002 无 ADR 阻塞(ADR-020 §三 Accepted)
- Unlocks: 本 Epic 的 Epic Status 翻 Green 的**最后一公里**(对账表齐 + spike 排期落定);相机 spike(`AC-20-12`/`AC-2-23` 载体)的输入完备;`OQ-2-1/2-7/2-8` 的裁决(经 spike 报告)

---

## Completion Notes

**Completed**: _待实现_
**Criteria**: _待填_(交付时须附:六子项对账输出 + ④ 措辞差异行有无 + spike 报告的 ADVISORY verdict 页)
**Deviations**: _待填_
**Test Evidence**: _待填_
**Code Review**: _待填_
**Manifest**: story Manifest Version 2026-09-21 = 当前 manifest(2026-09-21),无陈旧
