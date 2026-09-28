# Story 003: Anchor/Completion 两相触发引擎

> **Epic**: 教学与引导
> **Status**: Ready
> **Layer**: Core
> **Type**: Logic
> **Estimate**: 4h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-28

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

- [ ] 六步谓词各自独立求值:observe(并入 ① 锚)/ judge / treat / close / archive / manage,判据事件按规则三映射(`CaseOpened` / `JudgmentRecorded(author=p)` / `EmergencyAttempt`∨`DrugTreatmentApplied(initiator=p)` / `CaseClosed` / `Checkpoint` 写回执 / `StructurePlaced`)
- [ ] **AC-48-08 冷播种**:读档后以「流已含 step3 完成事件」夹具播种 ⇒ `Completion` 立真、`FireAdvance` **不重燃**(媒体不重播);逐位:两次读同档播种结果一致
- [ ] 边沿唯一性:同进程内同 (step, phase) 的 Fire 计数 ≤ 1;事件回灌 / 重放不致二次 Fire
- [ ] **可跳步**:直接完成 manage(建了房)而 judge 未完成 ⇒ 合法,不报错不惩罚;**可回头**:回头翻旧事不重燃 anchor;**不补播**:错过的 anchor 媒体不追播(F-48.2)
- [ ] 谓词单调:已 true 的 Completion 不因后续事件回 false(投影只增;闩只进不退)
- [ ] 联机各玩家各自求值(按 `player_id` 分路,单机双 id 夹具);联机运行面 BLOCKED-BY-45,禁借绿
- [ ] 触发引擎零设门副作用:Fire 只**产出媒体调度意图**(交 Story 004),不拦截 / 不禁用任何玩法(设门守卫本体 = Story 005)

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

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `unity/Assets/Tests/EditMode/Tutorial/tutorial_trigger_engine_test.cs` — must exist and pass
**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 002(Proj)· Story 001(步定义)
- Unlocks: Story 004(媒体调度消费 Fire)· Story 005(走查中验证「无门」)

---

## Completion Notes

*(empty — fill at story completion via `/story-done`)*
