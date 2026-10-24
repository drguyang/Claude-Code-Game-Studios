# Story 004: 死亡冷却 F-29-2(从流重算 · 零计数器)

> **Epic**: 死亡与复活
> **Status**: Complete
> **Layer**: Core
> **Type**: Logic
> **Estimate**: 4h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-10-24(实现轮:谓词 + 装配 + 18 条测试 + 7 轮证伪;**AC-29-08 / AC-29-10 全链仍 BLOCKED-BY-9,不记绿** —— 见 Completion Notes)
## Context

**GDD**: `design/gdd/death-and-respawn.md`(F-29-2 · EC 表「DEATH_COOLDOWN 内再次致死」行 · OQ-29-4/OQ-29-5)
**Requirement**: TR-death-002 的消费面(PlayerDied 既是产物也是冷却唯一真源)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-005(主): 确定性模拟(状态从流重算)
**ADR Decision Summary**: `DeathAllowed(actor,t) ⟺ t − last_death_tick(actor) ≥ DEATH_COOLDOWN`;**`last_death_tick` 唯一来源 = 世界流 `PlayerDied` 重算,零独立计数器**(AC-29-15);`DEATH_COOLDOWN` **未定(OQ-29-5,用户平衡轮;须 > 0,= 0 即「自杀清负重」掉落经济漏洞)**;冷却内再次致死 ⇒ **不结算代价、不发 PlayerDied、不传送**(回合继续,EC 行);冷却内二杀语义 = OQ-29-4 **实现期另裁**,本 story 只登记。 🔁 **2026-10-21 订正(原文不删,就地加注)** —— 本句**两处已过期**:① `DEATH_COOLDOWN` **已于 2026-10-06 代拍裁定 = `2400` tick(= 120 s @20 Hz,值可调)**,见 `design/gdd/death-and-respawn.md:280`(逐字:`✅ **2400**(= 120 s @20 Hz · 2026-10-06 代拍,值可调);**须 > 0**,否则「无限刷死」= 掉落经济漏洞`)与 `:535`(`OQ-29-5` 结案行);② 冷却内二杀语义 **已于 2026-10-07 裁定**,见 `:336`(EC 表本行)与 `:534`(`OQ-29-4` 结案行)= **零事件,含不发 `PlayerDied`;「强制传送」候选因制造无意图传送(第四来源,违 ADR-016 §一 三源不变量)被否决** ⇒ 不再是「实现期另裁」,而是**已定且可判**的语义。⚠️ 原文的「须 > 0」硬约束**仍然有效**(它约束取值域,不随取值确定而失效)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: int tick 减法,纯 BCL。

**Control Manifest Rules (this layer)**:
- Required: 谓词为流前缀纯函数;判据 `≥`(恰界即放);每个入结算的 candidate 先过 DeathAllowed
- Forbidden: 字段/计数器存 `last_death_tick`;墙钟/渲染帧参与冷却;在冷却拒绝路径发任何事件
- Guardrail: DEATH_COOLDOWN 以接口注入(值归数值轮),构建/测试夹具用符号化值;若注入 ≤0 ⇒ 显式配置错误(非静默)

---

## Acceptance Criteria

*From GDD `design/gdd/death-and-respawn.md`, scoped to this story:*

- [ ] AC-29-15 [A]:`last_death_tick` 唯一来源 = 世界流 PlayerDied 重算;反射/引用扫描断言 29 无独立计数器字段
- [ ] AC-29-08 [A] ⚠️ BLOCKED-BY-9(完整链):冷却内再次致死 ⇒ 不结算代价、**不发 PlayerDied**;桩 candidate 面测谓词半边,联调不记绿
- [ ] AC-29-10 [A] ⚠️ BLOCKED-BY-9:复活来源的 `ActorCellEntered` 条数上界 = `玩家数 / DEATH_COOLDOWN`(有界性;与 story-005 合验)
- [ ] 谓词边界:`t − last == DEATH_COOLDOWN` 放行;`−1` 拒绝(夹具符号化)
- [ ] 空流(首死):`last_death_tick = −∞` 语义 ⇒ 首个 candidate 恒允许(不发哨兵事件)
- [ ] OQ-29-4(冷却内二杀的确切呈现语义)在代码与文档双侧标「待裁」,不做行为发明 🔁 **2026-10-21 订正(原文不删,就地加注)** —— **`OQ-29-4` 已于 2026-10-07 裁定**,`design/gdd/death-and-respawn.md:534`(结案行)+ `:336`(EC 表本行)逐字 = **免罚但继续:零事件,连 `PlayerDied` 都不发(`last_death_tick` 不变),回合继续;「强制传送」候选被否决**(理由 = 无意图的位置变更属第四来源,违 ADR-016 §一 三源不变量)。⇒ 本条 AC **不再是「标待裁、不做行为发明」,而是已定且可判**:判据 = `DeathAllowed == false` 期间,世界流中该 actor 的 `PlayerDied` 条数**不增**(`:534` 末段自陈的可证伪判据),且**零代价结算、零传送**。**照原文实现会把一个已裁语义做成未裁** —— 那是反向借绿(该绿的做成不该绿的)。

---

## Implementation Notes

*Derived from ADR-005 Implementation Guidelines:*

1. `LastDeathTick(actor) := max{ e.tick | e ∈ 世界流 ∧ e.Kind = PlayerDied ∧ e.actor = actor }`(空集哨兵处理);读流,不缓存(缓存归表现层 Memoize,与 24 同纪律)。
2. 入结算管线顺序(story-001 编排):DeathCandidate → **DeathAllowed 门** → PlayerDied → 掉落/掉级/复活编排 —— 门在任何写流之前。
3. 联机多玩家:逐 actor 谓词天然并行(各自读流),无全局冷却字段。
4. 迁移重放:新主机 CatchUp 后 DeathAllowed 取值逐位不变(纯函数性质测试)。
5. 值注入:`DEATH_COOLDOWN_TICKS` 走配置(ADR-014 int 字段,JSON 整数,非 Fix —— 计数类量移出 Fix 解析集,D-21-17 口径)。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- [Story 001]: PlayerDied 发射与结算编排(本 story 是其前置门)
- [Story 002]: 掉落
- [Story 005]: 复活传送(「不传送」分支的效果在此登记,实现归 005 的调用点收敛)
- 系统 9:DeathCandidate 产生(与 29 冷却无涉)

---

## QA Test Cases

*Written at story creation (lean mode — QL-STORY-READY skipped; specs self-authored from AC text).*

- **AC-1(谓词边界)**: 恰界放行
  - Given: 符号化 CD=10;last=100
  - When: candidate@109 / @110
  - Then: 109 拒(无 PlayerDied);110 放
  - Edge cases: 首死(无历史 PlayerDied)任意 t 放行
- **AC-2(零计数器)**: 状态面扫描
  - Given: 29 的 sim 类型
  - When: 反射扫描字段
  - Then: 无 last_death/cooldown 类存储字段;值皆由流算出
  - Edge cases: 表现层计时器不受本断言管(门 A 外)
- **AC-3(拒绝路径静默)**: 冷却内二杀
  - Given: 刚死亡(t=100,CD=10),t=105 再注入 candidate
  - When: 结算管线
  - Then: 世界流零新增;代价不结;OQ-29-4 标注在案(行为 = 静默 continue,不发明) 🔁 **2026-10-21 订正** —— 本条是同一处过期登记的**第三个文本落点**(另两处 `:17` / `:38`)。行为「静默 continue」**与裁定一致**(`:336` = 零事件、不发 `PlayerDied`、不传送),但「标注在案 / 不发明」的**理由已不成立** —— `OQ-29-4` 已于 2026-10-07 裁定。⇒ 夹具的 Then 应写成**可判断言**:该 actor 的 `PlayerDied` 条数不增 + 零代价结算 + 零传送;而不是「已标注待裁」这类元断言。
  - Edge cases: 多玩家并行冷却互不影响

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `unity/Assets/Tests/EditMode/DeathRespawn/death_cooldown_test.cs` — must exist and pass(谓词半边);AC-29-08/10 全链 BLOCKED-BY-9
**Status**: [x] **已创建并通过**(2026-10-24)—— 18 条测试,`DeathCooldownTest` 全绿。
证据原件 = `production/qa/evidence/impl-story-004-death-cooldown-2026-10-24.md`。
⚠️ 本条只覆盖**谓词半边**;AC-29-08 / AC-29-10 的**全链**仍 BLOCKED-BY-9(见下方 Completion Notes)。

---

## Dependencies

- Depends on: Story 001(PlayerDied Kind 与流存在)
- Unlocks: Story 005(复活率上界合验)、联机压测夹具

---

## Completion Notes

**2026-10-24 —— 实现轮完成(Status: `Ready` → `Complete`;AC 逐条见下)**

### 一、AC 逐条判定

| AC | 判定 | 落点 / 判据 |
|---|---|---|
| **AC-29-15** `last_death_tick` 唯一来源 = 世界流重算;反射断言无独立计数器 | ✅ **满足** | `Sim/DeathCooldownGate.cs:155-188`(`TryLastDeathTick` 每次现读流,零缓存)。判据双条:**结构**`test_zeroCounter_noPerActorStorageField`(字段名 + 容器类型双扫)+ **行为**`test_zeroCounter_rereadsStreamOnEveryEvaluation`(流变 ⇒ 答案变)。⚠️ 只有结构判据会被「改名的缓存」绕过,只有行为判据会被「重算但结果碰巧一样」骗过 ⇒ 两条都写。 |
| **谓词边界** `t − last == CD` 放行;`−1` 拒绝 | ✅ **满足** | `DeathCooldownGate.cs:137`(`>=`)。判据 `test_deathAllowed_exactlyAtCooldown_isAllowed` / `test_deathAllowed_oneTickBeforeCooldown_isDenied`。证伪轮 **M1**(`>=` → `>`)⇒ 4 条转红。 |
| **空流(首死)** `last_death_tick = −∞` ⇒ 首死恒允许,**不发哨兵事件** | ✅ **满足** | `DeathCooldownGate.cs:134-135`(返回 `true`,**零 `Append`**)。判据 `test_deathAllowed_emptyStream_firstDeathAlwaysAllowed`(同时断言流长度 = 0)/ `test_tryLastDeathTick_emptyStream_returnsFalseNotSentinel`。证伪轮 **M3** ⇒ 转红。 |
| **OQ-29-4**(冷却内二杀语义) | ✅ **已裁并落地为可判** | 行为 = 零事件(`PlayerDiedWriteResult.RejectCooldown`,`PlayerDiedWriter.cs:114-115` 在写流**之前**返回)。判据 `death_candidate_writers_test.test_playerDiedWriter_rejectsWhenCooldownClosed`(世界流零增)。⚠️ 原 AC 文本的「标待裁」口径已于 2026-10-21 就地订正(见该 AC 的 🔁 注)。 |
| **AC-29-08** 冷却内再次致死 ⇒ 不结算代价、不发 `PlayerDied` | ⚠️ **谓词半边满足,全链 BLOCKED-BY-9** | 谓词半边 = 上一条。**全链**要求 9 的 F4 在生产上真推候选,而 `DiseaseVitalsService.PublishDeathCandidateIfTransferred` 虽已有生产调用点(`:459`),**类别面今日恒 Patient**(除 `BootRoot` 登记的玩家外无人是 Player),且玩家尚无染病路径 ⇒ 「玩家在冷却内再次致死」这一**场景**在生产上不可达。**不记绿。** |
| **AC-29-10** 复活来源的 `ActorCellEntered` 条数上界 | ⚠️ **BLOCKED-BY-9**(与 story-005 合验) | 本件不产出 `ActorCellEntered`(AC-29-09 明禁 29 追加),故本件**结构上**无法违反该上界;但「上界成立」的正面证据须等 story-005 的复活路径落地。**不记绿。** |

### 二、落地清单(已落 / 已证 分列)

| ID | Landed | Proven | File(s) | Proven-by |
|---|---|---|---|---|
| F-29-2 谓词实现 | ✅ | ✅ | `Sim/DeathCooldownGate.cs`(新,188 行) | 18 条 `DeathCooldownTest` + 证伪 M1/M2/M3 |
| `DEATH_COOLDOWN` 常量登记 | ✅ | ✅ | `design/registry/entities.yaml`(`DEATH_COOLDOWN` 块,2026-10-24 补登记) | `test_registry_deathCooldownBlockExistsWithAllFields`(六字段)+ `test_registry_valueMatchesImplementationConst`(值 = C# const)+ 证伪 **M5**(登记值改 2401 ⇒ 红) |
| 消费侧装配(`Register` 调用点) | ✅ | ✅ | `CompositionRoot.cs:341` | `test_assemble_sinkIsRegisteredByAssembly` + 证伪 **M4**(拆 `Register` ⇒ 红) |
| 类别查询面接真(玩家花名册) | ✅ | ✅ | `Sim/PlayerActorRegistry.cs`(新)· `CompositionRoot.cs:293` · `BootRoot.cs`(生成玩家后登记) | `test_registeredPlayer_producesOnePlayerDiedEndToEnd` + 证伪 **M7**(花名册恒 Patient ⇒ 红) |
| `death_cell` 的流前缀查询 | ✅ | ✅ | `Sim/DeathCellQuery.cs`(新) | 经 `test_registeredPlayer_producesOnePlayerDiedEndToEnd` 端到端覆盖(载荷 `death_cell` 可解码);**独立单测未写** —— 见下方诚实边界 |
| `deathAllowed` 签名加 `tick` | ✅ | ✅ | `Sim/DeathCandidateConsumer.cs:45,70-82,99-101` | 编译期(一元面无法表达 F-29-2 谓词)+ 上述端到端测 |

### 三、证伪轮(7 轮,全部精确命中)

方法 = 变异 → 跑 → `cp` 还原 → **SHA-256 逐字节比对**(`sha256sum -c`);**未用** `git checkout` / `git stash`。
变异窗口 = `DeathCooldownGate.cs` / `CompositionRoot.cs` / `PlayerActorRegistry.cs` / `DiseaseVitalsService.cs` / `entities.yaml`;窗口内未读被变异文件;锚串唯一性由脚本 `count == 1` 强制。

| 轮 | 变异 | 红 | 红集 |
|---|---|---|---|
| **M1** | `>=` → `>` | 4 | `exactlyAtCooldown` / `actorsAreIndependent` / `zeroCounter_rereadsStream` / `assemble_exposesGate` |
| **M2** | 取 `max` → 取首条 | 2 | `takesMaxNotLast` / `zeroCounter_rereadsStream` |
| **M3** | 空流首死 `true` → `false` | 1 | `emptyStream_firstDeathAlwaysAllowed` |
| **M4** | 装配层拆 `Register` | 1 | `test_assemble_sinkIsRegisteredByAssembly` |
| **M5** | 登记值 `2400` → `2401` | 1 | `test_registry_valueMatchesImplementationConst` |
| **M6** | 拆产出侧类别闸 | 1 | `test_patientDeath_producesNoCandidate` |
| **M7** | 花名册恒 `Patient`(装了等于没装) | 1 | `test_registeredPlayer_producesOnePlayerDiedEndToEnd` |

> ⚠️ **M2 首轮是空的,它抓出了我自己的一个假绿夹具**(原文不删):原夹具写
> `{At(300), At(100)}`,而「取首条」在那里**也**返回 300 ⇒ 该变异对 `takesMaxNotLast` 无效。
> 改为 `{At(100), At(300), At(200)}` 后三种实现读数两两可分(首=100 错 / 末=200 错 / max=300 对),M2 复跑转红 2 条。
> **教训**:「夹具刻意乱序」不等于「夹具能区分实现」—— 乱序的**位置**决定了它区分哪两种。

### 四、诚实边界(禁借绿)

1. **`DeathCellQuery` 无独立单测。** 它经端到端测间接覆盖(铸出的 `PlayerDied` 载荷 `death_cell` 可解码),但「最后一条 `ActorCellEntered` 胜」这条语义**没有**专门夹具。归 story-005(复活传送是它的主要消费者)补。
2. **`WorldSpawnCell = (0,1,0)` 是兜底,不是设计。** 玩家若从未跨格就死亡,`death_cell` 取该值(它是 `BootRoot.Teleport(new Vector3(0,1,0))` 经 `FloorToInt` 真实产生的格,不是发明的)。**正确的修法**是 1 在初始化时发一条 `ActorCellEntered`,但 AC-29-09 明禁 29 追加该事件 ⇒ 归 1 的初始化路径。
3. **`DEATH_COOLDOWN` 未走 ADR-014 配置注入。** 今日无 `death_cooldown` 的作者态 JSON / 烘焙产物 ⇒ 值是 ctor 默认参数。注入缝已留(`cooldownTicks` 形参),数据面归数值轮。
4. **AC-29-08 / AC-29-10 不记绿**(见 §一)。本件的完成**不等于**死亡链在生产上端到端可用 —— 缺的是「玩家会染病」这条路径(9 侧)。

### 五、复算命令

```bash
# 18 条谓词测试
grep -c '^\s*\[Test\]' unity/Assets/Tests/EditMode/DeathRespawn/death_cooldown_test.cs   # ⇒ 18

# 装配层消费侧注册点
grep -n 'deathCandidates.Register(deathConsumer)' unity/Assets/Gameplay.Boot/CompositionRoot.cs

# 玩家登记点
grep -n 'PlayerActors.Register' unity/Assets/Gameplay.Boot/BootRoot.cs

# 常量登记块
grep -n '^  - name: DEATH_COOLDOWN' design/registry/entities.yaml
```
