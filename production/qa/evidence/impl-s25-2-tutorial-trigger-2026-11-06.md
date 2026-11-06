# S25-2 · tutorial-003 两相触发引擎 —— 实现与判据(2026-11-06)

| 字段 | 值 |
|---|---|
| **卡** | `production/epics/tutorial-onboarding/story-003-anchor-completion-trigger-engine.md` |
| **权威** | 48 GDD 规则四 / F-48.1 / F-48.2 · `AC-48-08` · `AC-48-09` · ADR-009 §一 Q1 |
| **commit** | 见本轮提交(工作树在 `68d5d0e6` 之后) |
| **回归** | EditMode **`7016 / 6979 / 0 / 0 / 37`** @ 2026-11-06 19:0x CST(基线 `6998 / 6961` ⇒ **+18 / +18,零回归**) |

---

## 1. 结论

1. **两相拆开落地**:`Completion_s` 继续是 S25-1 的**单调读时谓词**;本件只新增**边沿**那一相 —— `TutorialTrigger` 把「首真」闩住,一次进程生命周期内每步至多发火一次。
2. **两口分家**是硬要求(Implementation Notes 2):`Seed` 只置饱和、**零 Fire**;`Evaluate` 才产出媒体调度意图。四条变异各自证明这条分工真的在起作用(§4)。
3. ⚠️ **本件零生产调用点**(见 §6-1)—— 消费方是 Story 004,不属于本件。

---

## 2. 改动清单

| 文件 | 改了什么 |
|---|---|
| `unity/Assets/Gameplay.UI/Tutorial/TutorialTrigger.cs` | **新增**:一实例 = 一名玩家的两相引擎。`Seed`(播种口)/ `Evaluate`(增量口)/ `HasFired` / `FireCount`;闩 = `readonly bool[]`,不可序列化、永不复位 |
| `unity/Assets/Tests/EditMode/Tutorial/tutorial_trigger_engine_test.cs` | **新增 18 条** |
| `production/sprint-status.yaml` | S25-2 → `in-progress` → `review` |

**边界(未越过)**:
- 引擎**不碰**事件流 / 帧 / `Time.*` —— 调用方把**已算好的投影**喂进来(Control Manifest:禁每帧全量重扫三流);
- 引擎**不设门**:公开面恰六个成员,`Evaluate` 唯一输出 = step id 列表(§3 最后一条);
- 闩不进存档、不进流:`AC-48-03` 的零落盘由 S25-1 的装配引用集守着(48 装配引不到 `Sim` / `Sim.Codec`)。

---

## 3. 判据(18 条)

| AC | 判据 | 结果 |
|---|---|---|
| **AC-48-08** 冷播种不重燃 | `test_ac48_08_seed_marksLatchWithoutFiring` · `…afterSeed_newCompletionFiresOnlyItself` · `…seedTwice_isIdempotent_noFire` | ✅ |
| 边沿唯一 | `test_ac2_sameViewEvaluatedTwice_firesOnlyOnce` · `…olderViewAfterNewer_doesNotRefireNorUnlatch` | ✅ |
| F-48.2 跳步/回头/不补播 | `test_ac3_skipSteps_onlyCompletedOneFires` · `…comeBackLater_firesThatStepOnly` · `…noCompensationOutput_forSkippedSteps` | ✅ |
| 双玩家分路 | `test_ac4_twoPlayers_haveIndependentLatches` · `test_guard_crossPlayerView_throws` | ✅(单机夹具) |
| 闩不可序列化 | `test_ac5_typeCarriesNoSerializableLatch`(非 `[Serializable]` / 非 `ISerializable` / 无公开字段 / 无 `[SerializeField]`) | ✅ |
| 两口分家 | `test_ac5_seedAndEvaluate_areDistinctMethods` · `…seedMouthNeverEmitsIntent` | ✅ |
| 零设门(引擎面) | `test_ac5_publicSurface_hasNoGateMembers`(公开面恰 = {Evaluate, Seed, HasFired, FireCount, PlayerId, StepCount}) | ✅ |
| 入口守卫 | `test_guard_nullSteps_throws` · `test_guard_negativePlayerId_throws` · `test_emptyStepSet_neverFires` | ✅ |
| 端到端 | `test_e2e_realProjection_firesOnFirstTrueEdge`(真 cooked 表 + 真 `TutorialProjection`) | ✅ |

---

## 4. 证伪轮(M1–M4,逐个转红 + SHA-256 还原)

| 变异 | 内容 | 红 |
|---|---|---|
| **M1** | 播种口跑 Fire 路径(两口的「共用」后果) | **4 红**:播种不重燃 / 播种后只发新步 / 播种幂等 / 播种口零意图 |
| **M2** | 已闩仍可再发火(边沿退化为电平) | **5 红**:同视图两次 / 回灌旧视图 / 回头补做 / 播种后只发新步 / 播种口零意图 |
| **M3** | 去掉串玩家守卫 | **1 红**:跨玩家视图必须抛 |
| **M4** | 播种只「看一眼」不置闩 | **3 红**:播种不重燃 / 播种后只发新步 / 播种口零意图 |

**还原判据 = SHA-256 逐字节**:`b10ae752…506d3f`,四次还原后逐位一致。

---

## 5. 回归三元组

| 套件 | 结果 | 截至时刻 | 批次 | commit |
|---|---|---|---|---|
| EditMode | **`7016 / 6979 / 0 / 0 / 37`** | 2026-11-06 19:0x CST | S25-2 收口全量 | 工作树(在 `68d5d0e6` 之上) |

**等式判据**:`7016 − 6998 = 18` = 本件新增 18 条,零回归。

```bash
python3 -c "import xml.etree.ElementTree as ET;r=ET.parse('/home/gu/.claude/jobs/6001633c/tmp/s25-2-editmode-full.xml').getroot();print({k:r.get(k) for k in ('total','passed','failed','inconclusive','skipped')})"
```

---

## 6. 未做 / 必须如实登记的

1. ⚠️ **零生产调用点(假绿形态 F2 的自陈)**:`TutorialTrigger` 今日**只被测试驱动**。它的消费方是 Story 004(媒体调度),那件未做之前,「教学真的会说话」在生产上是**空的**。⇒ 本件不得被读成「教学链已能跑」。
2. **相位维退化**:`F-48.1` 里会发火的只有 `Anchor`,故 AC 写的「(step, phase) 至多一次」在本实现里 = 「每步至多一次」。若 GDD 将来增出第二个发火相,须加维(**不预先虚构**)。
3. **联机运行面**:双玩家真实联机进度独立 `BLOCKED-BY-45`;单机双 id 夹具**不替代**其 closure。
4. **播种时机未接线**:「读档完成 → 首帧前一次性播种」是 Control Manifest 的要求,而今日**没有任何生产代码调用 `Seed`** —— 同上,归 Story 004 的装配。
5. **archive 步的回执**:仍走 S25-1 的 `ITutorialProjectionFeed.HasCheckpointWritten`,生产上恒 false(7a 无回执口)⇒ 该步在生产上不会发火。已在 S25-1 证据件登记,此处不重复计。
