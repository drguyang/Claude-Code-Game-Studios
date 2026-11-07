# Story 003: Anchor/Completion 两相触发引擎

> **Epic**: 教学与引导
> **Status**: Complete
> **Layer**: Core
> **Type**: Logic
> **Estimate**: 4h
> **Manifest Version**: 2026-11-02
> **Last Updated**: 2026-11-07

## Context

**GDD**: `design/gdd/tutorial-and-onboarding.md`(规则三 六步 · F-48.1 两相求值 · F-48.2 谓词独立可跳步可回头不补播 · AC-48-08 闩播种夹具)
**Requirement**: TR-tutorial-002(Anchor / Completion 两相 = 48 自述机制 —— **gap**,登记不立件;载体即本 story)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-009(主): 派生态(闩 = 进程内易失,不进流不存档,重启从流播种)· ADR-005: 确定性(投影求值只依赖流与 tick 相位,不依赖帧)· ADR-014: 内容(步定义读 `.cooked`)
**ADR Decision Summary**: F-48.1 两相:`Completion_s(p) ⟺ Predicate_s(Proj(p))` 为**单调读时谓词**;`FireAdvance_s` 是**边沿触发**(进程内闩 latch,冷启动从已加载流播种 ⇒ 老进度不重播,**永不重燃**)。F-48.2:六谓词相互独立 —— 可跳步(后步先完成合法)、可回头(不惩罚)、不补播(错过 anchor 口述不追播)。观察步并入 ① 锚(与 judge 共 `CaseOpened` 锚)。闩不是状态:存档里无它(Story 002 零落盘),读档/重启 ⇒ 重播种。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: 纯求值逻辑;tick 消费只经只读快照增量(不引 `Time.*`)。

**Control Manifest Rules (this layer)**:
- Required: 谓词求值 = 投影的纯函数;闩集合 ∈ {六步 id} 闭集;播种发生在「读档完成 → 首帧前」一次性执行
- Forbidden: 每帧全量重扫三流(增量消费或事件回调二选一,实现期定,禁轮询);把闩写成可序列化字段;跳步时强制补播前步
- Guardrail: 同一 (step, phase) 在一次进程生命周期内 `Fire` 至多一次(边沿定义)

---

## Acceptance Criteria

*From GDD `design/gdd/tutorial-and-onboarding.md`, scoped to this story:*

- [x] 六步谓词各自独立求值:observe(并入 ① 锚)/ judge / treat / close / archive / manage,判据事件按规则三映射(`CaseOpened` / `JudgmentRecorded(author=p)` / `EmergencyAttempt`∨`DrugTreatmentApplied(initiator=p)` / `CaseClosed` / `Checkpoint` 写回执 / `StructurePlaced`)
  - 载体:引擎按**步 id 闭集**逐条求值,互不牵连(`test_ac3_skipSteps_onlyCompletedOneFires` / `…noCompensationOutput_forSkippedSteps`);映射本身归 S25-1 的 `Proj`(`test_ac5_*`)
- [x] **AC-48-08 冷播种**:读档后以「流已含 step3 完成事件」夹具播种 ⇒ `Completion` 立真、`FireAdvance` **不重燃**(媒体不重播);逐位:两次读同档播种结果一致
  - 载体:`test_ac48_08_seed_marksLatchWithoutFiring` / `…afterSeed_newCompletionFiresOnlyItself` / `…seedTwice_isIdempotent_noFire`
- [x] 边沿唯一性:同进程内同 (step, phase) 的 Fire 计数 ≤ 1;事件回灌 / 重放不致二次 Fire
  - 载体:`test_ac2_sameViewEvaluatedTwice_firesOnlyOnce` / `…olderViewAfterNewer_doesNotRefireNorUnlatch`
  - ⚠️ **相位维退化**:`F-48.1` 里**会发火的只有 `Anchor`**(`Completion` 是单调读时谓词,不出事件)⇒ 「(step, phase)」在本实现里恰等于「step」。已在类注写明;若将来 GDD 增出第二个发火相再加维,不预先虚构
- [x] **可跳步**:直接完成 manage(建了房)而 judge 未完成 ⇒ 合法,不报错不惩罚;**可回头**:回头翻旧事不重燃 anchor;**不补播**:错过的 anchor 媒体不追播(F-48.2)
  - 载体:`test_ac3_skipSteps_onlyCompletedOneFires` / `…comeBackLater_firesThatStepOnly` / `…noCompensationOutput_forSkippedSteps`
- [x] 谓词单调:已 true 的 Completion 不因后续事件回 false(投影只增;闩只进不退)
  - 载体:`test_ac2_olderViewAfterNewer_doesNotRefireNorUnlatch`(回灌旧视图 ⇒ 不复燃、不撤销闩)
- [x] 联机各玩家各自求值(按 `player_id` 分路,单机双 id 夹具);联机运行面 BLOCKED-BY-45,禁借绿
  - 载体:`test_ac4_twoPlayers_haveIndependentLatches` + `test_guard_crossPlayerView_throws`(串玩家 fail-loud,且抛前零副作用)
  - ⚠️ **只覆盖单机夹具半边**;联机运行面仍 `BLOCKED-BY-45`
- [x] 触发引擎零设门副作用:Fire 只**产出媒体调度意图**(交 Story 004),不拦截 / 不禁用任何玩法(设门守卫本体 = Story 005)
  - 载体:`test_ac5_publicSurface_hasNoGateMembers`(公开面恰为六个成员,多出解锁 / 禁用 / 拦截类即红)+ `test_ac5_seedMouthNeverEmitsIntent`
  - ⚠️ **48 全 API 面的零设门反射守卫**归 Story 005;本条只钉本类

---

## Implementation Notes

*Derived from F-48.1 / F-48.2:*

1. 结构:`TutorialTrigger` = { 投影订阅(增量) → 六谓词求值 → 边沿检测(上一轮值 vs 本轮值)→ Fire(回调出媒体意图) }。全部在 `Gameplay.UI` 侧,无存档、无流写。
2. 播种 = 「冷启动时用当前投影值直接填闩,视同已发生」(不跑 Fire 路径);代码上播种与增量分两条口,禁共用(共用 = 重燃 bug 的温床)。
3. archive 谓词输入 = Story 002 留的 7a checkpoint 回执口(夹具时钟);该口正式化归 7a 轮 —— 本 story TODO 注释指向 TR/义务,不私开第二条通道。
4. 观察并入 ① 锚:observe 的 anchor 与 judge 的 anchor 同由 `CaseOpened` 边沿触发,口述次序由 004 调度表决定(引擎不排序)。
5. ⚠️ **数值冻结**:Fire→媒体的延迟 / 冷却等节奏旋钮全部归用户手感轮;本 story 只出布尔边沿。

---

## Out of Scope

- [Story 004]: 媒体意图的消费(口述 / 示范 / 纸的播放与近景)
- [Story 002]: 投影本体(本 story 是其消费方)
- [Story 005]: 零设门反射守卫(证明本引擎「没锁东西」的守卫件)
- 教学内容的文案与配音(内容轮)

---

## QA Test Cases

**[Logic story — automated test specs]:**

- **AC-1**: 播种不重燃(AC-48-08)
  - Given: 流夹具含 treat 步完成事件;模拟「读档 → 首帧」
  - When: 播种执行
  - Then: `Completion[treat]=true`;`Fire[treat.*]` 计数 = 0
  - Edge cases: 播种后新落一笔 close ⇒ `Fire[close.anchor]`=1(旧步不连带复燃)

- **AC-2**: 边沿唯一
  - Given: 干净进程;逐步投喂六类事件
  - When: 同一事件重复进增量口(回灌故障注入)
  - Then: 每 (step, phase) Fire ≤ 1;谓词值不变
  - Edge cases: tick 乱序到达(全序键重排后求值,同 ADR-008 口径)

- **AC-3**: 跳步 / 回头 / 不补播
  - Given: 只投喂 `StructurePlaced`
  - When: 求值
  - Then: 仅 manage 完成;其余五谓词 false;无任何「请先做 X」输出(引擎面零惩罚)
  - Edge cases: 之后补投 `CaseOpened` ⇒ judge anchor 正常 Fire(回头合法)

- **AC-4**: 双玩家分路
  - Given: p1 落笔、p2 未落笔
  - When: 两路求值
  - Then: `Completion[judge]`(p1)=true,(p2)=false;Fire 各自独立
  - Edge cases: 联机运行面 BLOCKED-BY-45

- **AC-5**: 闩不可序列化 + 播种/增量两口分家(🔁 **2026-11-06 sprint-25 `/qa-plan` 轮补** —— 原 AC-1 只测「播种不重燃」,**没测「闩本身不可被存档」**;Implementation Notes 说「两口禁共用」,原卡无对应用例)
  - Given: 触发引擎的公开成员面
  - When: 类型面断言(字段 / 属性逐项)
  - Then: **无任何可序列化字段承载闩状态**(`[SerializeField]` / 公开可写 / `ISerializable` 零命中)
  - Edge cases: 播种走播种口、冷启动后的新事件走增量口 —— 二者**不得共用同一个方法**:
    往播种口投一笔新事件 ⇒ **不得产生 Fire**(该口只置饱和,不触发边沿)

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `unity/Assets/Tests/EditMode/Tutorial/tutorial_trigger_engine_test.cs` — must exist and pass
**Status**: [x] Created and passing(18 条,2026-11-06)

---

## Dependencies

- Depends on: Story 002(Proj)· Story 001(步定义)
- Unlocks: Story 004(媒体调度消费 Fire)· Story 005(走查中验证「无门」)

---

## Completion Notes

**2026-11-06 实施轮(sprint-25 S25-2)—— 代码面完成**

- **落点**:`unity/Assets/Gameplay.UI/Tutorial/TutorialTrigger.cs` —— 一实例 = 一名玩家;
  `Seed`(播种口,零 Fire)/ `Evaluate`(增量口,产出媒体调度意图)。闩 = `readonly bool[]`,**不可序列化、永不复位**。
- **判据**:新增 **18 条**,全绿;证伪 M1–M4 各自转红(播种跑 Fire / 已闩复燃 / 串玩家 / 播种不置闩),SHA-256 逐字节还原。
- **回归**:EditMode `7016 / 6979 / 0 / 0 / 37`(基线 `6998 / 6961` ⇒ +18/+18,零回归)。
- **设计取舍**:相位维**退化**为「每步至多一次」—— `F-48.1` 里会发火的只有 `Anchor`
  (`Completion` 是单调读时谓词,不出事件)。已在类注写明,不预先虚构第二相。
- **⚠️ 未做(禁借绿)**:
  - **零生产调用点** —— 消费方 = Story 004(媒体调度),那件未做前「教学真的会说话」在生产上是空的(F2 自陈);
  - **播种时机未接线**(「读档完成 → 首帧前」),同样归 Story 004 的装配;
  - 联机运行面 `BLOCKED-BY-45`;
  - `archive` 步依赖 7a 回执口,生产上恒 false(承 S25-1 的登记)。
- **证据**:`production/qa/evidence/impl-s25-2-tutorial-trigger-2026-11-06.md`。

**2026-11-07 收口轮(sprint-25 S25-2)—— 两条「未做」由 S25-4c 关闭,本卡转 Complete**

- 🔁 **原「零生产调用点」(证据件 §6-1,F2 自陈)** —— **已关闭**。生产装配:
  - `unity/Assets/Gameplay.Boot/BootRoot.cs:848` —— 玩家 id 已定后
    `_services.TutorialSeams.Attach(_services.Stream, _services.Decoder, actorId)`;
  - `unity/Assets/Gameplay.Boot/TutorialChainDriver.cs` —— 构造期 `new TutorialTrigger(playerId, _steps)`,
    每 tick 边沿 `Evaluate` → 调度 → 投递;`BootRoot.cs:2886` 驱动。
  ⇒ 触发引擎**不再是测试专用件**。⚠️ 边界不变:`Evaluate` 只在 `BootRoot` 的 tick 边沿被调,
  引擎本身仍不读帧 / 不读 `Time.*`。
- 🔁 **原「播种时机未接线」(证据件 §6-4)** —— **已关闭**。`TutorialChainDriver` 构造期即 `Seed()`
  (`TutorialChainDriver.cs:117`),而 `Attach` 位于「流已就绪 + 玩家 id 已定」之后
  (`BootRoot.cs:842-848` 的注释逐字写明这条次序是硬要求)。换流(读档)时由
  `DetectStreamReplacement()` → `Seed()` 重播种(`:135-136`)。
- **回归**:`production/qa/evidence/impl-s25-2-tutorial-trigger-2026-11-06.md` §7
  (EditMode `7067 / 7030 / 0 / 0 / 37` @ `2026-11-07T06:16:11Z` · PlayMode `364 / 360 / 0 / 2 / 2` @ `06:17:14Z`
  · `3bc99c6d` + 工作树)。**本卡自身无新增夹具**(本轮只补证据链),等式判据归 S25-1 那一条。
- **仍未做(禁借绿)**:联机运行面 `BLOCKED-BY-45` · `archive` 步依赖 7a 回执口(生产恒 false) ·
  相位维退化(= 设计取舍,非缺口;GDD 增第二发火相时才加维)。

**🔁 2026-11-07 晚(S25-5 轮)—— `TutorialTrigger` 由 `public` 内化为 `internal`**

AC-48-17(零设门)的扫描器 `TutorialZeroGate` 判定:`TutorialTrigger.HasFired` / `FireCount`
是**完成状态查询**,而整类公开 ⇒ 玩法系统可直接读「玩家做了几步」。处置 = 整类 `internal`
(配 `AssemblyInfo.cs` 的 `InternalsVisibleTo("Gameplay.Boot")` / `("Sim.Contracts.Tests")`)。

⚠️ **这是「整类内化」而非「接口面设计」** —— 更干净的形态是把两个查询拆出去或只在测试可见。
今日 48 无对外暴露 `TutorialTrigger` 的需求,内化已足够;**若将来有,须重判**这两个成员该不该存在。
发动机的**行为面零改动**(`Seed` / `Evaluate` / 边沿逻辑一字未动),本卡的 18 条判据仍全绿。
