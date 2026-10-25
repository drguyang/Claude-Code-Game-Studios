# 实现轮证据件 —— TD-persist-04 义务② Load 接线轮(读档缝合)

**日期**:2026-10-25 · **轮次**:sprint-19 · **commit**(回归时点):`017d425` 工作树
**三裁(2026-10-25 用户全取推荐)**:① 范围 = 窄缝合轮(全量派生态重建另立项)② 触发点 = 公开方法·通电待信 ③ 兜底计数 = `NotifyExternalCheckpoint()` 清零
**权威件**:`Sim.Codec/AnchorCheckpointTrigger.cs:31`(义务② 原文:「生产 Load 接线落地时,读档路径必须调 `ResetCursor()`」)· 债册二十七订 · `production/qa/evidence/impl-td-persist04-2026-10-25.md`(动作① 轮)

## 一、接线落点(义务②结清)

| # | 件 | 落点 | 判据 |
|---|---|---|---|
| 1 | 读档缝合序列器 | `unity/Assets/Gameplay.Boot/LoadSequencer.cs`(`Load` = Load→ResetCursor→NotifyExternalCheckpoint 三步序;类头注载**派生态未重建清单**) | `grep -c "ResetCursor\|NotifyExternalCheckpoint" … = 2` |
| 2 | 服务袋发放 | `CompositionRootServices.cs` 属性 `LoadSequencer` + 可选尾参(`clinicCells` 之后) | `grep -n "LoadSequencer { get; }" CompositionRootServices.cs` |
| 3 | 组合根构造(同 null 语义) | `CompositionRoot.cs`(if 块内、三件存档侧组件之后;`saveBasePath` 为 null ⇒ 同 null) | `grep -n "new LoadSequencer" CompositionRoot.cs` |
| 4 | 生产触发点(裁定②) | `BootRoot.cs` `RequestLoad(Sim.Codec.SaveSlot)` —— fail-loud(部分装配抛,不静默吞显式请求) | `grep -n "public void RequestLoad" BootRoot.cs` |
| 5 | 夹具 | `Tests/EditMode/Boot/load_sequencer_test.cs`(4 条)+ `composition_root_test.cs` 装配同源钉 1 条(净增 2 条含既有测试改) | 见 §二 |

**缝合顺序论证**:`ResetCursor` 与 `NotifyExternalCheckpoint` 都在 `Load` **成功之后** —— `Load` 抛(Gating 拒收 / Corrupt 态)时缝合侧零改动(原子序,SC 变异所钉)。`SaveService.Load` 内部已含 `_ledger.OnLoaded`(读档即锁),序列器不重复。

**「通电待信」态(如实登记,禁借绿)**:生产 `RequestLoad` 今日零调用方(UI 侧 `SaveSlotItem` 是纯视觉元件,存档位界面未接线)。存档位 UI 接线轮落地即激活,零代码改动。playtest「无法读档」不是缺陷登记。

**派生态未重建清单(裁定① 窄轮)**:读档 Restore 只重建三流与折叠行 —— 在场视图(PresenceRegistry,Restore 恢复闭包绕 CAP 的原因)/ `PatientAppearedDriver` 内存计数 / `PatientVisualSpawner` 视觉 / `RandomEventsDirectorDriver._lastDrivenTick`(判等防御不拦 tick 回退,无累积危害)均不在本轮重建面,全量重建归独立大轮(须另对 ADR-008/023 口径)。清单逐字落 `LoadSequencer.cs` 类头注。

## 二、回归(基线三元组齐备)

| 项 | 值 |
|---|---|
| **本轮全量回归** | **`5033/4998/0/35`**(EditMode total/passed/failed/skipped) |
| **基线** | `5028/4993/0/35` @ `2026-10-25T11:33 UTC` · 52 出诊启动接线轮(全量第 2 跑)· `017d425` 工作树 |
| **本轮三元组** | `2026-10-25T11:49 UTC` · TD-persist-04 义务② Load 接线轮 · commit `017d425` 工作树(含本轮未提交改动) |
| **增量等式** | 5033 − 5028 = **+5** = 新夹具数(4 LoadSequencerTest + 1 装配钉)· 4998 − 4993 = **+5** · failed **0**(零回归) |
| **复算命令** | `python3 -c "import xml.etree.ElementTree as ET; r=ET.parse('unity/Logs/loadseq-editmode-full.xml').getroot(); print({k:r.get(k) for k in ('total','passed','failed','skipped')})"` |

**首验红波(已修,均修夹具非产品码)**:① `IReadOnlyList` 缺 using + `folded:`/`compose:` 第二参是 `IFoldedPredicate` 非 lambda(roundtrip 先例 `PredicateOf` 形态);② `JudgmentRecordedPayload` 需 freehand 具名重载 ⇒ 锚点 Kind 改 `DiseaseOnset`(Drain 只按 Kind 匹配,白名单成员夹具可自定)。

## 三、证伪轮(S12-4 · 三条守卫变异 · 同命令改→跑→还原 · SHA-256 逐字节还原全过)

| # | 变异(停用什么) | 期望红 | 实测 |
|---|---|---|---|
| SA | `ResetCursor()` 停用(`if (false)` 短路) | 义务② 判据 | ✅ 2 红(resetsCursorAndFallback + nullFallback) |
| SB | 兜底清零停用(`_fallback?.Notify…` → `_ = _fallback;`) | 裁定③ 判据 | ✅ 1 红(resetsCursorAndFallback) |
| SC | 原子序倒置(缝合三步中 Load 移到末位) | loadFails | ✅ 1 红(loadFails_seamsUntouched;正路径两测不动 —— Load 成功时序换结果同,恰证变异打中原子性而非正路径) |

三轮均:锚串恰命中 1 次(脚本断言)· SHA-256 逐字节还原 · 还原后 `grep "if (false)\|_ = _fallback"` 零残留 · 现哈希 `bd4f2a27…`。
过滤器 = `DaYiJingCheng.Tests.Boot.LoadSequencerTest`(重跑该 fixture 口径)。

## 四、安全属性 → 测试映射

| 属性 | 测试 | 状态 |
|---|---|---|
| 义务②:Load 成功 ⇒ 游标归零(再 Drain 同批**重触发** = 行为观测) | `load_resetsCursorAndFallback` / `nullFallback_loadStillSeamsCursor`(SA 反证钉) | ✅ RUN |
| 裁定③:Load 成功 ⇒ 兜底计数清零 | `load_resetsCursorAndFallback`(SB 反证钉) | ✅ RUN |
| 原子序:Load 抛 ⇒ 缝合侧零改动 | `loadFails_seamsUntouched`(SC 反证钉) | ✅ RUN |
| null fallback 合法(部分装配) | `nullFallback_loadStillSeamsCursor` | ✅ RUN |
| 同源钉(序列器的触发器 == 服务袋实例) | `composition_root_test.test_assemble_loadSequencer_sameTriggerInstance`(`AreSame`) | ✅ RUN |
| 同 null 语义(saveBasePath null ⇒ 序列器 null) | `composition_root_test.test_assemble_noSavePath_…`(既有测试扩一条断言) | ✅ RUN |

## 五、未跑 / 未声称(禁借绿)

- `BootRoot.RequestLoad` 的**运行时**行为(真启动序、真读档)未做 PlayMode 实测 —— EditMode 钉的是序列器语义与装配形状;真存档位 UI 接线轮到来时随其 PlayMode 走查。
- 生产 `RequestLoad` 零调用方(裁定② 通电待信)⇒ 「调用点存在且被 UI 调」无证据,也不声称。
- 派生态重建(在场视图等四项)未做 —— 裁定① 明确出轮,见 `LoadSequencer.cs` 类头注清单。
- 迁移链(`SaveVersion < Current` 分支)在 Load 内部,本轮未触碰亦未测(归 TD-persist-05 面)。
