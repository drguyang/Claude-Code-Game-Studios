# 实现证据件:TD-persist-05 动作 ④ —— AC-7a-20 定时兜底半边(2026-10-25)

> 对应债册:`docs/tech-debt-register.md` `TD-persist-05`(解除动作 ④ / 附)与
> `TD-persist-04` 动作 ① 的**实现轮兑现件**。裁定记录原文见债册两条目同日注,本件不复制全文。
> 前序件:`impl-td-persist-05-act2-2026-10-25.md`(② 兑现 + ④ 契约裁定轮)。

## 〇 裁定记录(2026-10-25 用户两裁定,AskUserQuestion)

1. **契约形态 = 甲案「具名接口」**:瞬时源 = 具名只读接口住 `Sim.Contracts`
   (与八个 P0 抽象点同构;⚠️ **是否计入「第九抽象点」未裁,不代裁**)。
   语义 = 规则十四「**此刻**是否无未决医疗动作」(瞬时量,内存态,不在流里 —— 裁定丙不可再议);
   10 落地后由 Boot 注入真实现,**10 未落地前生产维持 `narrativeContext = null`(fail-closed)**。
2. **`CHECKPOINT_INTERVAL` 本批裁结构 + 初值**:
   - 初值 = **5 min = `18000` tick**(@ 50 ms)—— `OQ-7a-5` 已于 2026-10-06 裁定,本批为**确认非复裁**;
   - 结构 = 兜底触发判据「**interval 到期 且 瞬时源 true**」;**到期不安全 ⇒ 顺延**
     (下一 tick 重查,**不重置 interval**)—— 从 GDD `:137`「崩溃损失 ≤ 一个间隔**恒成立**」推导:
     跳过窗口会把最坏损失放宽到 2× 间隔,与该不变量相抵;顺延保住它。

## 一 接口契约:`Sim.Contracts/INarrativeContextQuery.cs`

- `INarrativeContextQuery` —— 单方法 `bool InNarrativeContext()`(**瞬时求值,每次调用重答**;
  顺延语义就建立在这一点上)。
- `FuncNarrativeContextQuery` —— 委托适配器(测试与装配用):`null` 委托**拒收**;
  静态预置 `AlwaysIn` / `NeverIn`。⚠️ `NeverIn` **不是 null 的替身**:它语义是「可用但此刻拒绝」
  (fail-closed 的**运行态**),null 是「配置错误」(fail-closed 的**装配态**)—— 两态刻意分家,
  与裁定丙「null ⇒ 不可构造」一致。
- 裁定丙的「不构造」表达:`CheckpointFallback` 构造期对 null 瞬时源**全拒**
  ⇒ 10 未落地时生产**根本不构造该类**(装配根 `narrativeContext = null` ⇒ `Checkpoint` 必抛,
  兜底无从谈起),而不是「构造后放行」。
- SHA-256:`b4b00bd097533f2517bfd31724201ce804c6c06105826e62e72be96029283473`。

## 二 兜底调度器:`Sim.Codec/CheckpointFallback.cs`

- **是调度策略,不是存档入口**(AC-7a-20 逐字):触发的是调用方给的闭包
  (装配根接「`Checkpoint(下一个可写槽位)`」);本类不认识 `SaveService` / `SaveSlot`,不写任何字节。
- 驱动 = **sim tick**(`ITickProvider` 节奏,不由渲染帧驱动 —— O-10-3 / tick 相位纪律);
  **生产接线点今日为零**(裁定丙的有意终态),测试由夹具直接驱动 `OnTick`。
- 构造:`(Action checkpoint, INarrativeContextQuery context, int intervalTicks = 18000)`;
  null 闭包 / null 瞬时源 ⇒ `ArgumentNullException`;`intervalTicks < 1` ⇒ `ArgumentOutOfRangeException`。
- `OnTick()`:计数推进 → 未到期返回 → 到期先问瞬时源,不安全 ⇒ **顺延**(不清零);
  安全 ⇒ 触发闭包并清零。`TicksSinceLastFire` 为测试观测面。
- `DefaultIntervalTicks = 18000`(OQ-7a-5 已裁值,非本类新裁)。
- SHA-256:`f0fd264107374d3500e00d9b20abcbb78e1e581cde632dd881d984cddb18262c`。

## 三 换缝清单(缝从 `Func<bool>` 换成具名接口)

| 落点 | 改动 |
|---|---|
| `Sim.Codec/SaveService.cs` | 字段 / ctor 形参 `INarrativeContextQuery narrativeContext = null` / `Load` 门 `!_narrativeContext.InNarrativeContext()` |
| `Sim.Codec/SaveSlotLedger.cs` | `Snapshot`(`_narrativeContext.InNarrativeContext()`)/ `EnsureWritable` / ctor 四处换缝 |
| `Gameplay.Boot/CompositionRoot.cs` | `:514` 缝类型换 `INarrativeContextQuery`(仍 `= null`,裁定丙);`:469` 口径句同步 |
| 测试实参平移 **28 处** | `save_async_write_test`(12)· `save_exit_retry_and_flag_test`(3)· `save_replay_roundtrip_test`(2)· `save_slot_contract_test`(helper 形参 + 11 处 `AlwaysIn`/`NeverIn`)—— `() => true` → `FuncNarrativeContextQuery.AlwaysIn` 等;`new SaveSlotLedger(null)` 一处**不动**(null 装配态夹具) |
| `folded_predicate_test.cs` 签名钉子 | 按该文件 🔁 惯例逐版本加注:`ps[3].ParameterType` 断言 `Func<bool>` → `typeof(INarrativeContextQuery)`(第四形参位置不变) |

## 四 首验

- 8 夹具组(PersistenceService + FoldedPredicate)155 测试:唯一红 =
  `test_saveService_takesFoldedPredicateViaConstructor_only`(签名钉子,预期中的缝变更)。
- 按钉子惯例更新后复跑 FoldedPredicateTest + CheckpointFallbackTest = **44/44 绿**(`EXIT=0`)。

## 五 证伪轮(驱动器 `falsify_fallback.py`,目标 `CheckpointFallback.cs`,filter = CheckpointFallbackTest)

| 变异 | 停用什么 | 结果 |
|---|---|---|
| MF1 | 删顺延检查(不安全也照发) | 红 3(defersWhileUnsafe / neverFires / resetsCounter) |
| MF2 | 顺延误作重置计数 | 红 2 |
| MF3 | 到期判据 off-by-one(`<` → `<=`) | 红 2 |
| MF4 | 删触发后清零 | 红 2 |
| MF5 | 删 null 瞬时源拒收 | 红 1 |
| MF6 | 删 interval < 1 拒收 | 红 1 |
| MF7 | 默认间隔漂移 18000 → 9000 | 红 2 |

**7 变异全红、零假绿**;每轮后 `CheckpointFallback.cs` SHA-256 逐字节还原 =
`f0fd264107374d35…`(7 次一致)。

## 六 回归三元组

- 本轮全量 EditMode:**`4978 / 4943 / 0 / 35`**(`td10` · `2026-10-25T09:31 UTC` · commit `5dc9369` 工作树,**未提交**)。
- 基线:`4971/4936/0/35` @ `td09` · `2026-10-25T09:12 UTC` · `19626d5` 工作树
  (引自 `impl-td-persist-05-act2-2026-10-25.md` §六)。
- 等式:`4978 − 4971 = 7` = 恰好 `CheckpointFallbackTest` 7 条新增 ✓ **零回归**。
  复算:`python3 -c "import xml.etree.ElementTree as ET; r=ET.parse('$HOME/.claude/jobs/181888b1/tmp/td10-full.xml').getroot(); print(r.get('total'), r.get('passed'), r.get('failed'), r.get('skipped'))"` ⇒ `4978 4943 0 35`。

## 七 缺口与如实登记(禁借绿)

1. **生产接线今日为零 —— 有意终态**(裁定丙):Boot 不构造 `CheckpointFallback`,
   10 落地前 `narrativeContext = null`。⇒ **AC-7a-20 整体仍 `PARTIAL`**:
   兜底半边本件兑现,**事件触发半边**(8 / 37 / 52 三条锚点接线)归 `TD-persist-04` 动作 ①,仍 open。
2. **Boot 未来接线义务(登记)**:装配根构造 `CheckpointFallback` 时,`context` 实参**必须与
   `SaveService` 同一 `INarrativeContextQuery` 实例**(规则十四「一个量」)—— 两个实例 = 两个瞬时源,
   读档门与兜底门会对同一时刻给出不同回答。归 Boot 接线轮(10 落地时)。
3. **`INarrativeContextQuery` 是否计入 P0 抽象点编号(八 → 九)未裁** —— 文件头如实注明,不代裁。
4. `AC-7a-15 (a)` 回退**提示**仍缺(归 42 / 7b,承 act1 件末注,不在本件面内)。
