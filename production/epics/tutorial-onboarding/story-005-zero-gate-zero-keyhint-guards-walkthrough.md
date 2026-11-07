# Story 005: 零设门与零按键提示守卫及走查

> **Epic**: 教学与引导
> **Status**: Complete
> **Layer**: Feature
> **Type**: Integration
> **Estimate**: 4h
> **Manifest Version**: 2026-11-02
> **Last Updated**: 2026-11-07

## Context

**GDD**: `design/gdd/tutorial-and-onboarding.md`(规则五 零设门 · 第一铁律 禁止形态表 · AC-48-01 / AC-48-02 / AC-48-05 [L] / AC-48-07 [L] / AC-48-12 / AC-48-17)
**Requirement**: TR-tutorial-007(零设门反射断言 —— **gap**,登记不立件;载体 = AC-48-17 本 story)· TR-tutorial-001(零落盘的守卫面延伸)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-013(主): 拟物 UI 框架(叠加层元件的宿主栈)· ADR-018 §六: 无提示音铁律白名单(AC-48-12 联合 AC-44-09)· ADR-011(规则八零按键提示的三系统联合 BLOCKING 之一,与 3/42/39 同批)
**ADR Decision Summary**: 两条「不可能做事」的机器化:① **零设门**(AC-48-17):48 公开 API 面**不暴露**任何 `progress / unlocked / inTutorial / gate` 类查询 —— 反射断言,「想设门也拿不到接口」;教学期任何玩法(病例生成 / 建造 / 出诊)照常,无视教学零惩罚。② **零按键提示叠加层**(第一铁律):48 资产面零 HUD 叠加元件、零按键提示文本;禁止形态表(含被否决的「教学期豁免」)全项构建期扫描;零「学会了 X」提示音(触发源 ∈ 行为反馈白名单外即败,联合 AC-44-09)。走查项:AC-48-05 [L](手柄六步全程)/ AC-48-07 [L](无视教学零惩罚)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: MEDIUM
**Engine Notes**: 反射 / 资产扫描 = 编辑期能力;六步走查需 Story 003/004 链路可跑(字幕管线子项 BLOCKED-BY 音频 epic)。

**Control Manifest Rules (this layer)**:
- Required: 守卫 = 可执行断言(构建失败级 / EditMode 败),判据「做不到」不是「没显示」;[L] 项走查单 + 主创签核
- Forbidden: 豁免清单(「教学期可以有提示」已被用户裁定否决 —— 扫描器无豁免分支);用运行时 flag 关提示替代 API 面不存在
- Guardrail: 黑名单词族与 39 Story 004 扫描器共享夹具源(一处扩两处生效,禁分叉)

---

## Acceptance Criteria

*From GDD `design/gdd/tutorial-and-onboarding.md`, scoped to this story:*

- [x] **AC-48-17 零设门**:48 公开 API 反射扫描,标识符黑名单 ∈ {`progress`,`unlocked`,`gate`,`inTutorial`,`tutorialState`,`canAccess`,…} 零命中;无任何返回「教学进度」的公开查询面(调度意图出参除外,其类型不携带门槛语义)
  - ✅ **2026-11-07 落地,且发现并修掉一处真实违规**:扫描器 `TutorialZeroGate`(G1 标识符 / G2 **返回类型**)首跑即红 —— `TutorialProgressView` 是**公开类型**,`TutorialProjection.Project` 公开返回它 ⇒ 规则五当时**不成立**。处置 = **内化**(`internal` + `InternalsVisibleTo("Gameplay.Boot")` / `("Sim.Contracts.Tests")`):投影 / 进度视图 / 步骤完成视图 / 触发引擎全部改为 internal。**不是改名** —— 改名只换词不换可达性。
- [x] **AC-48-01 / AC-48-02 叠加层与素材闭集**:48 程序集 + 资产清单闭集扫描:零 HUD 叠加层预制 / UXML 浮层,零新增 `Sprite`/`Texture`(纸 = 世界内物件纹理,不在禁列);闭集外的资产引用 = 构建失败
  - ✅ `TutorialZeroGate.ScanAssetIndex`:48 目录只允许 `.cs/.asmdef/.meta/.md`,**今日实测零违规**(48 无自有资产);负夹具(临时目录放 `Tooltip.uxml` + `hint.png`)证明它会红
- [x] **零按键提示**(联合 AC-8-44 / AC-3-F1a / 39 规则八):48 资产无按键字形 / 「按X」词族(词族扫描器复用 39 Story 004 的同一实现)
  - ✅ **同一张词表,不另写一份**:判据直接用 `TutorialContentSchema.ExplicitInstructionTokens`(烘焙校验 5 的同一个常量对象)⇒ 一处扩充两处生效。扫描面 = 48 内容表里**全部用户可见文本**(step.title / narration.text / paper.title / paper.body),实测零命中
- [x] **AC-48-12 零状态播报音**:48 触发源全量 ∈ 行为反馈白名单(44 的 AC-44-09 断言跑 48 子集);无「学会了 X」jingle / sting
  - ✅ 48 的每条口述 cue 逐条回查 44 事件表:`whitelist_category` ∈ `AudioEventTableGates.WhitelistCategories`、`trigger_source` ∉ `ForbiddenTriggerSources`(后者 = 状态播报形态)⇒ 48 子集在 44 的白名单断言下成立
- [ ] **AC-48-07 [L] 无视教学零惩罚走查**:全程不响应任何教学媒体,六步照常可达、病例照常生成、建造照常;走查单 + 录屏 [L]
  - ⚠️ **NOT-RUN** —— `[L]` 人工走查,本轮未跑,**不代签**。前件:六步里 `archive` 生产恒不成立(7a 无回执读面)、示范 / 纸两腿无触发面 ⇒ 走查对象不全
- [ ] **AC-48-05 [L] 手柄六步全程走查**:纯手柄完成「被口述引到 → 读纸 → 做六步」自然路径(不靠教学也通,靠教学也好通;两路都无死角)[L]
  - ⚠️ **BLOCKED-BY-SPEC + 对象不全**:`OQ-C4` 三媒体节奏 spec 仍欠;**纸屏今日无生产开口**(`OpenModal(PaperCloseup48)` 零调用点)⇒ 「读纸」那一步走不了。**不代签**
- [x] 守卫负夹具:故意加一个 `GetProgress()` / 一个浮层资产 ⇒ 扫描必红(证明守卫有牙齿)
  - ✅ 四条变异各自单红(M1 词表失效 / M2 把进度视图改回 public / M3 停用返回类型判定 / M4 放行 `.uxml`),SHA-256 逐字节还原

---

## Implementation Notes

*Derived from ADR-013 / ADR-018 Implementation Guidelines:*

1. 三个扫描器一族落点:API 反射面(48 程序集)/ 资产闭集(`.meta` 清单 + Addressables 标签)/ 音频触发源(交 44 白名单断言跑子集)。全部 EditMode + 构建期双跑。
2. 资产闭集 = 显式白名单枚举(48 的全部合法资产在清单内),「闭集外即失败」方向(承 ADR-017 门 A 白名单形制);清单源 = Story 001 的 `.cooked` 引用集导出(单一出处)。
3. 走查脚本预写:07 走查的「惩罚」判据 = 可观察量(病例是否照生成、建造槽是否照放、收入是否照发)—— 与无教学基线存档逐项对比,禁「感觉没惩罚」。
4. 与 3 输入系统联动:零按键提示的「旁路」(tooltip、手柄图标自动显示)也在扫描面 —— InputSystem UI 组件默认生成 binding 提示须关(实现细节留代码评审,判据在本 AC)。
5. ⚠️ **数值冻结**:无新旋钮(全守卫件)。

---

## Out of Scope

- [Story 004]: 媒体调度本体(本 story 扫它的产物)
- 3 / 42 / 39 各自的零按键提示守卫本体(各 epic 的 AC;本 story 只保证 48 侧不违联合 BLOCKING)
- 44 白名单断言框架本体(音频 epic;本 story 是 48 子集的挂载方)
- [L] 走查的**执行排期**(QA 轮;本 story 交付走查单 + 负夹具)

---

## QA Test Cases

**[Integration story — automated guards + walkthrough]:**

- **AC-1**: 零设门反射 + 负夹具(AC-48-17)
  - Given: 48 程序集公开面
  - When: 标识符/返回类型黑名单扫描;再注入 `int GetProgress()` 负夹具重跑
  - Then: 正向零命中;负夹具必红(构建失败级)
  - Edge cases: 属性 / 委托字段 / 扩展方法同扫(不留门面旁路)

- **AC-2**: 叠加层与素材闭集(AC-48-01/02)
  - Given: 48 资产清单 + 引用图
  - When: 闭集扫描
  - Then: 零浮层、零新增贴图引用;注入一个 `Tooltip.uxml` 引用 ⇒ 失败
  - Edge cases: 纸纹理走 6 的世界资产清单(合法路径),不记在 48 名下

- **AC-3**: 音频触发源白名单(AC-48-12)
  - Given: 48 全部 cue 触发点
  - When: 44 白名单断言子集跑
  - Then: 全 ∈ {口述 cue, 纸翻页声类};jingle/sting 形态零
  - Edge cases: 「完成一步」不许新增专属音(完成反馈 = 游戏行为本声,非教学层音)

- **AC-4**: 无视教学零惩罚走查(AC-48-07 [L])
  - Given: 双存档基线:同种子,一玩家全程理会教学、一玩家全程背对 / 无视
  - When: 各跑 30 分钟等效脚本(病例 / 采集 / 建造动作序一致)
  - Then: 关键可观察量(病例生成数 / 可建造槽数 / 资源收入)逐项相等;走查单落证据 [L]
  - Edge cases: 无视者后期回头 ⇒ anchor 不补播(Story 003 判据)、completion 照常 Fire

- **AC-5**: 手柄六步全程(AC-48-05 [L])
  - Given: 干净档,仅手柄
  - When: 走完六步自然流
  - Then: 每步无指针依赖、媒体可读;录屏 + 主创签核 [L]
  - Edge cases: 三媒体节奏 spec(OQ-C4 欠账)未补则本走查标 BLOCKED-BY-SPEC,禁借绿

- **AC-6**: 零按键提示词族扫描(🔁 **2026-11-06 sprint-25 `/qa-plan` 轮补** —— 原 AC-2 测的是「资产闭集 / 浮层」,
  而卡面 AC 里还有一条**独立的**「零按键提示(按 X 词族)」,原卡无用例)
  - Given: 48 的全部资产与程序集字符串面
  - When: 词族扫描(复用 39 Story 004 的**同一实现**,不另写一份)
  - Then: 零按键字形、零「按 X / 点击 / 按住」词族;含**旁路**:InputSystem 默认生成的绑定提示须关闭
  - Edge cases: 「走近再说」这类**世界内动作**不算按键提示(它不指向输入设备);
    跨语言（译文）里的按键词族同样在扫描面内

---

## Test Evidence

**Story Type**: Integration
**Required evidence**: `unity/Assets/Tests/EditMode/Tutorial/tutorial_zero_gate_guard_test.cs`(守卫组)+ `production/qa/evidence/tutorial-onboarding/story-005-no-gate-walkthrough.md`(AC-48-05/07 [L],录屏截图 + 签核) — must exist
**Status**: ⚠️ **半成立** —— 守卫组**已落**(10 条判据,全绿);走查件 `story-005-no-gate-walkthrough.md` **不存在且本轮不造**。

| AC | 载体 | 状态 |
|---|---|---|
| AC-48-17 零设门 | `TutorialZeroGate.cs`(构建期)+ `tutorial_zero_gate_guard_test.cs` 正控 / G1 / G2 / 干净正控 / 空扫描面 | ✅ 有载体,4 条变异各自单红 |
| AC-48-01/02 素材闭集 | `TutorialZeroGate.ScanAssetIndex` + 真 48 目录用例 + 临时目录负夹具 | ✅ |
| 零按键提示 | 同上件,复用 `TutorialContentSchema.ExplicitInstructionTokens` | ✅ |
| AC-48-12 音频白名单 | 同上件,逐 cue 回查 44 事件表 | ✅ |
| AC-48-07 / AC-48-05 [L] | — | ⚠️ **NOT-RUN / BLOCKED-BY-SPEC**,不代签 |

⚠️ **「守卫组全绿」不等于「AC-48-07 走查通过」** —— 前者是「48 不可能设门」的静态判据,后者是「玩起来真的没惩罚」的动态观测。两者不同层,不得互相顶替。

---

## Dependencies

- Depends on: Story 003(触发引擎)+ Story 004(媒体链可跑,走查前置)· 39 Story 004(共享扫描器夹具源)
- Unlocks: Epic 收口 · `/ux-review` 48 轮(节奏 spec 补齐后回刷走查)

---

## Completion Notes

**2026-11-07 收口。口径 = 本 story 自身范围(自动化守卫)完成;[L] 走查两件 NOT-RUN。**

### 落了什么

1. **`TutorialZeroGate.cs`(新,构建期门)**:G1 标识符黑名单 + **G2 返回类型黑名单** + 资产后缀闭集。
   接进 `AssemblyGates.RunAll()`(接线三处:调用、`EntryWiringManifest`、`ClassVerdict` —— 首跑红 1 即「新入口未声明」,证明 manifest 门在工作)。
2. **`tutorial_zero_gate_guard_test.cs`(新,10 条)**:真 48 装配正控 · 非空洞守卫 · G1 夹具 · G2 夹具 · 干净正控 · 空扫描面 · 真 48 目录闭集 · 临时目录负夹具 · 零按键词族 · AC-48-12 音频白名单。
3. **`tutorial_assembly_guard_test.cs` 增 1 条**:`test_ac48_11_buildGateWhitelist_matchesTheAsmdefFace` —— 把 S25-1 的 `b2-exact` 表与 asmdef 源做双向对账(期望值写字面量,不引被测表,避 F3 自指判据)。

### 🔴 本轮发现并修掉的一处**真实违规**(不是新写判据发现的「设计缺陷」,是既有代码违反既有规则)

`TutorialProgressView` / `TutorialProjection` / `TutorialStepCompletion` / `TutorialTrigger` 原本是 **public**。
`TutorialProjection.Project` 公开返回 `TutorialProgressView` ⇒ 任何玩法系统都能拿到「玩家走到第几步」。
**规则五(零设门)当时不成立**,而 GDD 与 UX spec 都写着「48 的 API 面禁止暴露进度查询」。

处置 = **内化**:四类型改 `internal`,新增 `AssemblyInfo.cs` 声明
`InternalsVisibleTo("Gameplay.Boot")`(驱动方)与 `("Sim.Contracts.Tests")`(夹具面)。
⚠️ **不是改名** —— 改名只把 `GetProgress` 换成 `Get()` 而不改可达性,G2 盲测那条旁路;内化让「想设门也拿不到接口」在**编译期**成立。

**连带代价(如实记)**:`tutorial_assembly_guard_test.test_ac48_04_exportedSurface_hasNoSimTypes` 的**锚点类型**原本是 `TutorialProjection`,内化后它不再在公开面 ⇒ 锚点改为 `TutorialDispatcher`。
AC-48-04 的另一半(编译产物引用集)是**装配级**判据,不受影响。已在夹具内就地加注。

### 证伪轮(4 条,各自单红)

| # | 变异 | 期望转红 | 实测 |
|---|---|---|---|
| M1 | `HitToken` 恒 false(词表永不匹配) | G1 命名夹具 + 真装配正控 | ✅ 2 条红 |
| M2 | 把 `TutorialProgressView` + `TutorialStepCompletion` 改回 public | AC-48-17 正控 | ✅ 红 |
| M3 | 停用 G2 返回类型判定 | G2 改名旁路夹具 | ✅ 红 |
| M4 | `AllowedFileSuffixes` 放行 `.uxml` | 资产闭集负夹具 | ✅ 红 |

还原 = **备份 + `cp` 回 + SHA-256 逐字节比对**(不用 `git checkout`):
`TutorialZeroGate.cs` = `9b70f2a3dcfaa9e8384caa5eb6bd2169fed721251bc22641728a1a40120b8f01` ·
`TutorialProjection.cs` = `cf1c78908cc981b1e516e40eaf08d1da66d2fed41e3ed664358ee7a2cb8eba06`;`grep -rn "FALSIFY" unity/Assets` = 0。

### 回归三元组

EditMode `7086 / 7049 / 0 / 0 / 37` @ `2026-11-07T14:42:13Z → 14:44:12Z` · 批次 `sprint-25 收口后 S25-5` · commit `923890a5` + 工作树
(基线 `7076 / 7039` @ 同日 13:46:21Z ⇒ **+10 / +10,零回归**)。

**等式判据**:`7086 − 7076 = 10` = 新文件 `tutorial_zero_gate_guard_test.cs` 的 `[Test]` 计数(**实测 `grep -c "\[Test\]"` = 10**)。
⚠️ `tutorial_assembly_guard_test.cs` 本轮**只改了锚点**(`git diff --stat` = 6 插 2 删,**零新增 `[Test]`**),
其 `test_ac48_11_buildGateWhitelist_matchesTheAsmdefFace` 早在 `cdbefccc`(S25-1 轮)入库并已计入 `7076` 基线 —— 故不计入本轮的差额。

PlayMode `365 / 361 / 0 / 2 / 2` @ `2026-11-07T14:32:13Z → 14:32:34Z`(零回归)。

### 仍未做(不得借绿)

1. **AC-48-07 [L] 无视教学零惩罚走查** —— NOT-RUN。前件不全:六步里 `archive` 生产恒不成立(7a 无回执读面)、示范 / 纸两腿无触发面。
2. **AC-48-05 [L] 手柄六步全程走查** —— **BLOCKED-BY-SPEC**:`OQ-C4` 三媒体节奏 spec 仍欠;纸屏无生产开口。
3. **`TutorialTrigger.HasFired` / `FireCount` 的归属** —— 本轮只把 `TutorialTrigger` 整类内化以避开 G1;更干净的形态是**把这两个查询拆出去**或改为只在测试可见。今日内化已足够(玩法拿不到),但这是「整类内化」而非「接口面设计」,记作**将来若 48 需要对外暴露 `TutorialTrigger` 则须重判**。
4. **联机面** —— BLOCKED-BY-45。

### 与 S25-1 的关系(勿混记)

S25-1 的 `b2-exact` 管的是「48 引用了谁」(引用集恰 = `{Gameplay.Presentation}`);本件的 `TutorialZeroGate` 管的是「48 暴露了什么」(公开面零门禁词)。
**两条判据正交**,任一单独存在都不足以支撑规则五 —— 前者防「48 自己设门」,后者防「48 把门卖给别人」。
