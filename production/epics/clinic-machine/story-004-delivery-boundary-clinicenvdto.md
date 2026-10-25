# Story 004: 乘子交付边界与 ClinicEnvDto

> **Epic**: 医馆即机器
> **Status**: Complete  <!-- 2026-10-25 /story-done 收口(lean);详见 Completion Notes -->
> **Layer**: Core
> **Type**: Integration
> **Estimate**: 5h
> **Manifest Version**: 2026-10-15  <!-- 2026-10-25 /dev-story 用户裁定 [A]:自 2026-10-02 刷至现行版(增量仅 Cross-Cutting 之七/之八两节) -->
> **Last Updated**: 2026-10-25
## Context

**GDD**: `design/gdd/clinic-machine.md`(O-24-3 ClinicEnvDto · 规则三 K_CONTEXT_MAX 上交 · 交付 9/21a/1 · AC-24-07 白名单 · AC-24-09 移 42)
**Requirement**: TR-clinic-009(语义不泄漏:9 只收乘子,不收「医馆」语义概念) · TR-clinic-010(24 零呈现引用 · 零音频直接触发) · TR-clinic-003(EnvMod 边界不中转 float)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-025(主): 契约程序集清单;ADR-013: 拟物 UI 框架
**ADR Decision Summary**: `IClinicEnvQuery.GetEnv(room)` + `ClinicEnvDto` 住 `Sim.Contracts`(OQ-CP-4 甲案);`Sim` 引用集恰 = {BCL, Sim.Contracts};DTO 字段 roomName/contexts[]/envMod/equipMod,`envMod`/`equipMod` 为 Q16.16 raw,零 `disease_id`、零 float,受 `PresentationDtoGuard` 递归反射扫描(AC-37-15 同机制)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: 交付面是纯 C# 契约 + 反射断言,不触任何 post-cutoff API;门 A 白名单断言(AC-24-07)是编译期/构建期机制。

**Control Manifest Rules (this layer)**:
- Required: 9 侧只收 `Fix` 恢复率乘子;21a 收 F-24-1/F-24-2 运行时输入(该侧为唯一 clamp 点);1 收 `K_CONTEXT_MAX`;呈现侧只读 `ClinicEnvDto`
- Forbidden: `disease_id` 或「医馆/房间」语义进 9 的判定输入;「医馆加成」参与 9 的 F4 护理决策(规则明令 9 只用 EnvMod 作恢复率);24 直接触发音频/持有 UI
- Guardrail: 程序集引用集白名单断言(AC-24-07)—— 24 的 sim 程序集零 `UnityEngine`、零几何库;违反 = 构建失败

---

## Acceptance Criteria

*From GDD `design/gdd/clinic-machine.md`, scoped to this story:*

- [ ] AC-24-07:24 的 sim 程序集引用白名单断言通过(恰 ∈ {BCL, Sim.Contracts});`UnityEngine`/几何库出现 = 构建失败
- [ ] AC-24-01b 配对联测:超界 env 值经 24 原样出、在 21a F1 被 clamp —— 两端联测证明「唯一 clamp 在 21a」
- [ ] `ClinicEnvDto` 字段完备:roomName / contexts[] / envMod / equipMod,`envMod`/`equipMod` 为定点 raw long,零 float 字段
- [ ] `PresentationDtoGuard` 递归扫描 ClinicEnvDto 及嵌套/集合元素类型:零 `disease_id`、零浮点(与 AC-37-15 同一守卫复用)
- [ ] `K_CONTEXT_MAX = max(K_speed)` 经接口上交系统 1(非硬编码常量)
- [ ] AC-24-09(呈现侧走查)按 [A] 级**登记移交 42 epic**,本 story 只保证 DTO 可被只读消费,不实现任何 UI

---

## Implementation Notes

*Derived from ADR-025/013 Implementation Guidelines:*

1. `IClinicEnvQuery` 定义于 `Sim.Contracts`(只读,无副作用);`Sim` 内实现返回 story-003 的求值结果。
2. 交付三向各立只读接口/事件消费者:9(恢复率乘子)、21a(F1 输入 + clamp)、1(K_CONTEXT_MAX)—— 禁 24 反向调用三者。
3. 引用白名单断言形态 = 程序集引用集枚举比对(承 ADR-017 §二 门 A 硬化先例),不是 grep。
4. DTO 守卫测试直接复用 `PresentationDtoGuard` 递归扫描器(skill-system/itemdb 已有先例路径),新增断言目标 = ClinicEnvDto。
5. 「医馆语义不进 9」的机械化 = 9 侧接口签名只收定点乘子类型,无 room/context 参数;评审对照该签名。
6. 数值(乘子具体值域)归用户数值轮;本 story 交接的是**类型与边界**。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- [Story 001]: 表烘焙
- [Story 002]: 房间析出
- [Story 003]: 乘子求值公式
- [Story 005]: 纯函数重放 / Memoize
- 42 epic:AC-24-09 脉案/纸面的实际渲染走查([A])

---

## QA Test Cases

*Written at story creation (lean mode — QL-STORY-READY skipped; specs self-authored from AC text).*

> **QA plan 对齐(2026-10-25 /qa-plan sprint-20)** —— 证据件:`unity/Assets/Tests/EditMode/ClinicMachine/clinic_env_delivery_test.cs`(账本树 `tests/integration/clinic-machine/` 并列);估 ~8 条。AC-1 即 AC-24-01b(两侧定点往返,**夹具须先坐实未配 clamp 时侧向超界可见**,防恒真);AC-2 即 PresentationDtoGuard **递归**扫描(AC-37-15 一致性);AC-3 即 AC-24-07(反射断言非 grep,传递引用不计)。另需:`K_CONTEXT_MAX` 经接口上交(24 不持魔数);contexts[] 空/满两界;**AC-24-09 移交 42 epic = EXTERNAL,不在本件验证**。21a clamp 契约未就绪 ⇒ 接口桩联测,BLOCKED-BY 不记绿。QA plan 全文:`production/qa/qa-plan-sprint-20-2026-10-25.md`。

- **AC-1(唯一 clamp 配对)**: 24 出 / 21a clamp 端到端
  - Given: 布局使 env_score 超 ENV_MAX
  - When: 24 求值 → 经契约交 21a F1
  - Then: 24 侧返回未饱和值;21a 侧返回饱和值;两侧皆定点
  - Edge cases: 低于 ENV_MIN 对称;恰好界上不截
- **AC-2(DTO 无泄漏)**: 递归反射扫描
  - Given: `ClinicEnvDto` 及其全部嵌套类型/集合元素类型
  - When: `PresentationDtoGuard` 扫描
  - Then: 无 `disease_id`、无 `float`/`double` 字段
  - Edge cases: 新增字段即被扫描捕获(反射非硬编码字段名清单)
- **AC-3(门 A 白名单)**: 引用集断言
  - Given: 24 的 sim 程序集
  - When: 构建期枚举引用
  - Then: 恰 ⊆ {BCL, Sim.Contracts};注入一个 `UnityEngine` 引用 fixture = 构建失败
  - Edge cases: 传递引用不计(白名单看直接引用集)

---

## Test Evidence

**Story Type**: Integration
**Required evidence**: `unity/Assets/Tests/EditMode/ClinicMachine/clinic_env_delivery_test.cs` — must exist and pass;门 A 断言另附构建期检查落点(与 ADR-017 硬化项同批)
**Status**: [x] Created & passing(2026-10-25 · **9 测 9/9 绿** `Logs/s20-1-run3.xml`;全量回归 5045/5010/0/35 @ 2026-10-25 · S20-1 收口轮 · `a3242b6` 工作树,基线 tddc-full2 5036/5001 增量 +9 = 新测数)—— 证据件 `production/qa/evidence/impl-s20-1-clinic004-2026-10-25.md`;评审原件 `production/qa/evidence/review-s20-1-clinic004-2026-10-25.md`(APPROVED WITH SUGGESTIONS,零 BLOCKING)
**反证(S12-4)**:F1 白名单定义域收窄(Sim.asmdef+类型使用双注入 ⇒ 靶测红)· F2 停用 PresentationDtoGuard 词面检测 ⇒ 负例红 · F3 撤 21a 钳 ⇒ 两条配对测红(SHA-256 逐字节还原全验证)

---

## Dependencies

- Depends on: Story 003(乘子);系统 21a F1 clamp 契约、系统 1 的 K 消费接口(未就绪时以接口桩联测,标注 BLOCKED-BY 不记绿)
- Unlocks: Story 005(交付稳定后测重放);42 epic 的病例/医馆页只读消费

---

## Completion Notes

**Completed**: 2026-10-25
**Criteria**: 6/6 收口(5 COVERED + 1 EXTERNAL 按卡登记)

| AC | 测试 | 状态 |
|---|---|---|
| AC-24-07 白名单(恰 ∈ {BCL, Sim.Contracts}) | `test_ac2407_*`(:209-240,枚举比对非 grep,防空集 + 正控制) | COVERED |
| AC-24-01b 配对联测(唯一 clamp 在 21a) | 3 测(:247-300,前置防恒真 + 正/负腿 + 恰等界) | COVERED |
| ClinicEnvDto 字段完备 / raw long / 零 float | 闭合集反射断言(:146-148 等) | COVERED |
| PresentationDtoGuard 递归扫描 | 干净测 + 负例测(:163-187) | COVERED |
| K_CONTEXT_MAX 经接口上交 | 双表差分神谕(:312-360) | COVERED(表派生 + 只读交付口;**「上交系统 1」一腿 = 装配轮 NOT-RUN**,证据件 §六) |
| AC-24-09 移交 42 epic | — | EXTERNAL(按卡登记,非本卡面) |

**Falsifiability Gate(S12-4)**:命中三形态(定义域收窄 / 守卫停用 / 阈值边界),反证 F1/F2/F3 全部转红 ⇒ **PASS**(证据件 §四)。
**Deviations**:
- ADVISORY:QA plan 预设「21a 桩 + Ignore(BLOCKED-BY)」**不适用** —— `EnvModTotal` 契约实测已就绪,配对走真件联测(严格更强;上方 QA 对齐注的桩预案原文不删,以本注为准)。
- ADVISORY:F1 证伪发现 b2 门对「声明未使用」asmdef 引用盲 ⇒ **TD-gate-04**(修法候选待用户裁);评审顺带发现 story-003 夹具自指断言 ⇒ **TD-clinic-19**(复核债)。两条均已入债册(三十三/三十四订)。
- ADVISORY(评审 minor,结账表「未落」列):① 「零浮点」判据归属 = 测试①闭合枚举,**非** `PresentationDtoGuard` 本体(其只查疾病词面);② `ClinicContextTable` 构造负例(头≠行 ⇒ 红)归 001/002 表卡 backlog;③ `StartsWith("System")` 宽松面已并入 TD-gate-04 面。
**Test Evidence**:Integration —— `unity/Assets/Tests/EditMode/ClinicMachine/clinic_env_delivery_test.cs`(9 测)+ 账本树 `tests/integration/clinic-machine/` 并列注明;证据件 + 评审原件路径见 Test Evidence 节。
**Code Review**:Complete(`review-s20-1-clinic004-2026-10-25.md`,unity-specialist APPROVED WITH SUGGESTIONS + qa-tester TESTABLE,零 BLOCKING)。

> ⚠️ NOT-RUN 残余(禁借绿,详见证据件 §六):`IClinicEnvQuery` 生产接线两处语义未裁(int 房号 ↔ `ClinicRoomId(High,Low)` 映射 / `ContextIds` 词表 size)归装配轮;9 侧消费点未存在(TR-clinic-009 gap「登记不立件」);IL2CPP 对拍归 ADR-012 批。
