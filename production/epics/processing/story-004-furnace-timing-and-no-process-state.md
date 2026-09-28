# Story 004: 执行时序:单炉并发模型、无进程态、重放等值

> **Epic**: 炮制
> **Status**: Ready
> **Layer**: Core
> **Type**: Logic
> **Estimate**: 6h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-28

## Context

**GDD**: `design/gdd/processing.md`(规则八 时序与并发 · OQ-18-3[甲] · OQ-18-5 已结清 · 规则七 · AC-18-12/13/17/23/15)
**Requirement**: TR-processing-012(完成时点 = Tick(start)+duration_ticks,tick 计零墙钟零浮点)· TR-processing-015(并发模型:每玩家单炉、不可取消、拒绝零事件、跨玩家并发允许,⚠️ partial —— OQ-18-8 器具互斥归 24)· TR-processing-016(发起即落流、无进程态,7a 只存事件流)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-005(主): 主机唯一 Step/Append + 事件流唯一真源 ⇒ 「进行中」= 派生量,非状态 · ADR-010(次): 存档 = 头部+三流+快照,18 无段 · ADR-011 Amendment B(次): 点火意图归主机终裁
**ADR Decision Summary**: `CompleteTick = start_tick + duration_ticks`(F-18.3,纯 tick 域,`ITickProvider` 驱动,20 Hz 已标定 ⇒ 1 tick = 50 ms 量纲);单炉不可取消(OQ-18-3[甲]):第二点火意图 ⇒ 拒绝 + 零事件;同 tick 两玩家各一点火 ⇒ 两条 `Craft` 均落流(器具互斥归 24 = OQ-18-8,18 不判);点火时点产出装不下 ⇒ 20 整体拒绝 ⇒ **零 `Craft` 落流、库存逐位不变、炉不点燃**(R-18-D 前半,AC-18-12);无进程态三重门(AC-18-17):18 程序集零 `[Serializable]` + 7a 段清单无 18 记录 + 重放重建 `CompleteTick` 与直算逐位相同。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: 全部判据为门 A 纯 C# + 程序集属性扫描,零引擎 API;AC-18-15 跨平台三出参逐位属 ADR-012 矩阵(EXTERNAL·BLOCKED-BY,矩阵不存在 + F7 未跑 ⇒ 本故事只交 Mono 侧重放等值)。

**Control Manifest Rules (this layer)**:
- Required: 炉状态 = `Craft` 流前缀的派生(每玩家「最后一炉」由流重放算出,禁独立持久字段跨存档存活 —— 内存缓存允许但键须纯 (流前缀, tick));`duration_ticks` 为烘焙 int;`ITickProvider` 步相位(承 tick 裁定 ③:每 tick 恰一次 Step,不由渲染帧驱动)
- Forbidden: `DateTime` / `Stopwatch` / `Time.*`(墙钟与渲染帧符号)入结算/完成路径(IL 零符号引用);取消语义(P0 不可取消);18 自建「进行中进度」持久态;为性能把炉表写进快照
- Guardrail: 器具共享语义(OQ-18-8)未裁 ⇒ 18 只判「玩家自身单炉」,不判器具占用(该半边外抛 24,禁私裁);AC-18-12 的拒绝发生在时点(裁决前),结构上不存在「中途回滚」路径

---

## Acceptance Criteria

*From GDD `design/gdd/processing.md`, scoped to this story:*

- [ ] **AC-18-13**: `CompleteTick` 路径 IL 层扫描 ⇒ 零浮点、零墙钟 API(`DateTime`/`Stopwatch`/`Time.*` = 零符号引用)
- [ ] **AC-18-12**: 点火时点产出不装下 ⇒ 主机终裁**零 `Craft` 事件落流**、库存逐位不变、炉不点燃(R-18-D 前半:拒绝在时点)
- [ ] **AC-18-23**: 某玩家已有一炉在烧 ⇒ 第二点火意图被拒 + 零事件(单炉,OQ-18-3[甲]);同 tick 两玩家各一点火 ⇒ 两条 `Craft` 均落流(跨玩家并发允许;器具互斥归 24,18 不判)
- [ ] **AC-18-17**: 一炉进行中 ⇒ ① 18 程序集零 `[Serializable]` 类型;② 7a 存档段清单不含任何 18 记录;③ 重放重建的 `CompleteTick` 与直算逐位相同(OQ-18-5;措辞按 GDD 订正 = 程序集属性 + 段清单两处机器可断言面)
- [ ] **AC-18-15** [I]: 同一 `(WorldSeed, recipe, 输入实例集, 两 EnvMod 分量)` 三格 CI 重放 ⇒ 三出参逐位相同。**⚠️ EXTERNAL · BLOCKED-BY-ADR-012**(矩阵不存在 + F7 spike 未跑 ⇒ 不得记绿;夹具限定每 tick 单 `Craft`,D-21-28 已裁[甲]但 Seq 发号跨平台逐位同属实测面)

---

## Implementation Notes

*Derived from ADR-005 §Decision(主)/ ADR-010 §一:*

1. 炉状态查询 `IsBusy(actor, tick)` / `CompleteTickOf(actor, tick)` = `Craft` 前缀纯函数(取该 actor 最新一条 `Craft`:`busy ⟺ tick < start+duration`);单实例内存缓存合法但实现「从空重建等值」测试(同 foraging Story 004 前缀纪律,共用扫描基建)。
2. 完成判定 = tick 比较(Story 005 的调度消费本条谓词);整型加法无溢出路径(duration 上限烘焙期校验,装载纪律同 Story 002 的硬失败族)。
3. 点火事务(承 Story 003 序):准入 → **本故事单炉判定** → F1 → 20.Apply(容量先验)→ Append。AC-18-12 用 spy-sink 断「Apply 拒绝分支上零 Append」;「炉不点燃」= 单炉谓词在拒绝后仍 false(时点拒绝无副作用)。
4. 墙钟扫描:IL/符号引用扫描 `System.DateTime`/`Diagnostics.Stopwatch`/`UnityEngine.Time` 在 18 结算路径零命中(扫描器与 AC-18-02 同一驱动,新增标识符表)。
5. 7a 段清单断言:读 ADR-010 §三 义务汇总表生成物(若 7a 尚无机器可读清单,以「存档格式常量表不含 Processing 段」静态断言替代,接缝回写义务登记 `O-18-R8` 类注记,不阻塞①③两面)。
6. 重放等值(Mono 侧半):同事件流两次重建 `CompleteTick`/`IsBusy` 序列逐位同;跨平台半边留 `EXTERNAL(ADR-012)` 标注。
7. 取消 = 无路径:18 不定义取消 API(禁「预留但不断言」的灰色接口 —— YAGNI + AC-18-23 判据即拒绝路径覆盖)。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 003:点火落流与铸造(本故事只做「能不能点/点完没有」的派生谓词)
- Story 005:完成时刻的调度执行、起货呈现 cue、溢出落地(本故事只交 `CompleteTick` 谓词)
- 24 医馆即机器:器具占用/OQ-18-8(18 不判)
- ADR-012 落地轮:三格矩阵、F7 spike、Seq 发号跨平台逐位(AC-18-15 的 EXTERNAL 半边)
- 7a(ADR-010):存档 codec 本体(本故事只断「无 18 段」的形状事实)

---

## QA Test Cases

*Written at story creation (lean mode — QL-STORY-READY skipped; specs self-authored from AC text).*

- **AC-18-13**: 墙钟/浮点零符号。
  - Given: 18 程序集 IL。
  - When: 扫描三类符号 + 浮点指令。
  - Then: 结算路径零命中;负样例注入 `Stopwatch` 必红。
  - Edge cases: 测试代码与编辑器工具路径不在扫描集(按程序集归属)。
- **AC-18-12**: 容量不足时点拒绝。
  - Given: F1 产出总量 > 20 剩余容量(合成临界)。
  - When: 点火意图。
  - Then: spy-sink 零 `Craft`;`InventoryOf` 前后逐位相同;再查 `IsBusy == false`。
  - Edge cases: 临界恰等(装得下 ⇒ 成功);F1 已算但 Apply 拒 ⇒ F1 调用发生但零事件(允许:纯函数无副作用)。
- **AC-18-23**: 单炉与并发。
  - Given: actor A 一炉在烧(tick ∈ [start, complete));A 第二意图同 tick 到达;另 actor B 同 tick 一意图。
  - When: 主机裁决。
  - Then: A 第二意图拒绝零事件;B 的 `Craft` 正常落流(两条同 tick,Seq 递增)。
  - Edge cases: A 完成 tick 当拍新点火(`tick < complete` 为 false ⇒ 空闲,边界取等可点);A 取消不存在(无 API)。
- **AC-18-17**: 三重无进程态门。
  - Given: 一炉进行中;18 程序集反射 + 7a 段清单常量。
  - When: 断言。
  - Then: ① `[Serializable]` 类型计数 = 0;② 段清单零 18 记录;③ 从事件流重放,`CompleteTick` 与直算逐位等(100 点采样)。
  - Edge cases: 跨「存档往返」后仍由流重建(与 persistence 测试交叉的最小样本;codec 真身归 7a 故事)。
- **AC-18-15**(EXTERNAL): Mono 侧等值先测。
  - Given: 同参数二次求值(干净进程)。
  - When: 比对三出参。
  - Then: 逐位同(本仓可证半边);`Ignore("EXTERNAL BLOCKED-BY-ADR-012")` 的跨平台项留桩。
  - Edge cases: 夹具每 tick 单 Craft 约束显式写入夹具生成器。

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `unity/Assets/Tests/EditMode/Processing/furnace_timing_no_process_state_test.cs` — must exist and pass
- Integration 交叉(AC-18-12 与 20):`unity/Assets/Tests/PlayMode/craft_atomicity_test.cs`(与 inventory-items epic 共用样本流)

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 003(`Craft` 落流形状 —— 前缀谓词的输入),Story 001(准入),inventory-items Story 001/002(`InventoryOf` fold + 20.Apply 容量先验)
- Unlocks: Story 005(完成调度谓词),重放集成回归(全 epic 等值基线)

---

## Completion Notes

*(placeholder — to be filled at story completion)*
