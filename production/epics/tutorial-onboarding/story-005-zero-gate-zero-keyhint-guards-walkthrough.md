# Story 005: 零设门与零按键提示守卫及走查

> **Epic**: 教学与引导
> **Status**: Ready
> **Layer**: Feature
> **Type**: Integration
> **Estimate**: 4h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-09-28

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

- [ ] **AC-48-17 零设门**:48 公开 API 反射扫描,标识符黑名单 ∈ {`progress`,`unlocked`,`gate`,`inTutorial`,`tutorialState`,`canAccess`,…} 零命中;无任何返回「教学进度」的公开查询面(调度意图出参除外,其类型不携带门槛语义)
- [ ] **AC-48-01 / AC-48-02 叠加层与素材闭集**:48 程序集 + 资产清单闭集扫描:零 HUD 叠加层预制 / UXML 浮层,零新增 `Sprite`/`Texture`(纸 = 世界内物件纹理,不在禁列);闭集外的资产引用 = 构建失败
- [ ] **零按键提示**(联合 AC-8-44 / AC-3-F1a / 39 规则八):48 资产无按键字形 / 「按X」词族(词族扫描器复用 39 Story 004 的同一实现)
- [ ] **AC-48-12 零状态播报音**:48 触发源全量 ∈ 行为反馈白名单(44 的 AC-44-09 断言跑 48 子集);无「学会了 X」jingle / sting
- [ ] **AC-48-07 [L] 无视教学零惩罚走查**:全程不响应任何教学媒体,六步照常可达、病例照常生成、建造照常;走查单 + 录屏 [L]
- [ ] **AC-48-05 [L] 手柄六步全程走查**:纯手柄完成「被口述引到 → 读纸 → 做六步」自然路径(不靠教学也通,靠教学也好通;两路都无死角)[L]
- [ ] 守卫负夹具:故意加一个 `GetProgress()` / 一个浮层资产 ⇒ 扫描必红(证明守卫有牙齿)

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

---

## Test Evidence

**Story Type**: Integration
**Required evidence**: `unity/Assets/Tests/EditMode/Tutorial/tutorial_zero_gate_guard_test.cs`(守卫组)+ `production/qa/evidence/tutorial-onboarding/story-005-no-gate-walkthrough.md`(AC-48-05/07 [L],录屏截图 + 签核) — must exist
**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 003(触发引擎)+ Story 004(媒体链可跑,走查前置)· 39 Story 004(共享扫描器夹具源)
- Unlocks: Epic 收口 · `/ux-review` 48 轮(节奏 spec 补齐后回刷走查)

---

## Completion Notes

*(empty — fill at story completion via `/story-done`)*
