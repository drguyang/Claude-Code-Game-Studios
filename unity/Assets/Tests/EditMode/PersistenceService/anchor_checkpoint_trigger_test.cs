// AC-7a-20 事件触发半边 —— `AnchorCheckpointTrigger` 行为钉子(TD-persist-04 动作① 实现轮)
//
// 权威来源:
//   GDD `persistence-service.md` 规则六 —— 锚点白名单 `EVENT_CHECKPOINT_ANCHORS`(只增不删);
//   AC-7a-20 —— 「WHEN 白名单事件被提交,THEN 在该事件的 Step 边界触发一次 checkpoint」;
//   债册 TD-persist-04 裁定块(2026-10-25 四裁定):集中观察 · Kind 映射 ·
//     复用带门入口(不安全 ⇒ 该次不触发,兜底顺延补上)· 同 tick 多锚点一次 ·
//     成功 ⇒ 兜底计数清零(**失败 ⇒ 不清** —— 否则「崩溃损失 ≤ 一个间隔」放宽到近 2×)。
//
// ─────────────────────────────────────────────────────────────────────────────
// 本文件测的是**调度策略**,不是存档入口:触发的是夹具给的闭包(记录调用与返回值),
// 不经过 `SaveService`。白名单内容钉(`DefaultAnchors` 三员)在本文件末尾。
// ─────────────────────────────────────────────────────────────────────────────

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.PersistenceService
{
    /// <summary>
    /// `AnchorCheckpointTrigger`:后缀扫描 / 单次触发 / 门预问 / 失败不清兜底 / 游标钳制 五面钉子。
    /// </summary>
    public class AnchorCheckpointTriggerTest
    {
        private List<SimEvent> _events;
        private int _fires;
        private int _notifies;
        private bool _inContext;
        private bool _checkpointSucceeds;
        private AnchorCheckpointTrigger _trigger;

        [SetUp]
        public void SetUp()
        {
            _events = new List<SimEvent>();
            _fires = 0;
            _notifies = 0;
            _inContext = true;
            _checkpointSucceeds = true;
            _trigger = NewTrigger();
        }

        private AnchorCheckpointTrigger NewTrigger()
        {
            return new AnchorCheckpointTrigger(
                () => { _fires++; return _checkpointSucceeds; },
                new FuncNarrativeContextQuery(() => _inContext),
                AnchorCheckpointTrigger.DefaultAnchors,
                onFired: () => _notifies++);
        }

        private static SimEvent Ev(long tick, EventKind kind)
            => new SimEvent(tick, PatientId.None, tick, kind, default);

        // ── 触发与单次性 ──

        [Test]
        public void test_anchorCheckpoint_anchorInSuffix_firesOnce_neverRefires()
        {
            // Arrange:白名单事件落流
            _events.Add(Ev(1, EventKind.ActorCellEntered));
            _events.Add(Ev(2, EventKind.CaseClosed));

            // Act:两次 drain(第二次无新事件)
            _trigger.Drain(_events);
            _trigger.Drain(_events);

            // Assert:恰一次;通知(兜底清零)也恰一次
            Assert.AreEqual(1, _fires);
            Assert.AreEqual(1, _notifies);
        }

        [Test]
        public void test_anchorCheckpoint_nonAnchorEvents_neverFire()
        {
            // Arrange:全为白名单外事件
            _events.Add(Ev(1, EventKind.ActorCellEntered));
            _events.Add(Ev(2, EventKind.ResourceHarvested));
            _events.Add(Ev(3, EventKind.PatternRecognized));

            // Act
            _trigger.Drain(_events);

            // Assert:零触发 —— 非「曾出现过锚点」的累积语义(W5 教训),只扫新尾段
            Assert.AreEqual(0, _fires);
        }

        [Test]
        public void test_anchorCheckpoint_multipleAnchorsSameDrain_singleFire()
        {
            // Arrange:同批两个锚点(裁定④:同 tick 多锚点 = 一次)
            _events.Add(Ev(1, EventKind.JudgmentRecorded));
            _events.Add(Ev(1, EventKind.CaseClosed));

            // Act
            _trigger.Drain(_events);

            // Assert
            Assert.AreEqual(1, _fires);
        }

        [Test]
        public void test_anchorCheckpoint_cursorScansOnlyNewSuffix()
        {
            // Arrange:先 drain 一批无锚点事件(游标推进)
            _events.Add(Ev(1, EventKind.ActorCellEntered));
            _trigger.Drain(_events);

            // Act:追加锚点后再 drain
            _events.Add(Ev(2, EventKind.HouseCallStarted));
            _trigger.Drain(_events);

            // Assert:新尾段的锚点被看到;且再 drain 不重触发
            Assert.AreEqual(1, _fires);
            _trigger.Drain(_events);
            Assert.AreEqual(1, _fires);
        }

        // ── 门预问(裁定③)──

        [Test]
        public void test_anchorCheckpoint_gateUnsafe_skipsFire_consumesAnchor()
        {
            // Arrange:Armed 期(瞬时源 false = 不安全)
            _inContext = false;
            _events.Add(Ev(1, EventKind.CaseClosed));

            // Act:不安全期 drain
            _trigger.Drain(_events);

            // Assert:不触发、不清兜底
            Assert.AreEqual(0, _fires);
            Assert.AreEqual(0, _notifies);

            // Act + Assert:转安全后再 drain(无新事件)—— 锚点已消费,**不补触发**
            // (补触发 = 本类另记「欠账」,与裁定③「由定时兜底顺延补上」相抵)
            _inContext = true;
            _trigger.Drain(_events);
            Assert.AreEqual(0, _fires);
        }

        [Test]
        public void test_anchorCheckpoint_gateSafe_firesAndNotifiesFallback()
        {
            // Arrange + Act:安全期锚点
            _events.Add(Ev(1, EventKind.JudgmentRecorded));
            _trigger.Drain(_events);

            // Assert:触发 + 通知(兜底窗口清零)
            Assert.AreEqual(1, _fires);
            Assert.AreEqual(1, _notifies);
        }

        // ── 失败不清兜底(头注 ⚠️:不变量保护)──

        [Test]
        public void test_anchorCheckpoint_checkpointReportsFalse_doesNotNotify()
        {
            // Arrange:闭包返回 false(生产 = 异常已记日志、存档未发生)
            _checkpointSucceeds = false;
            _events.Add(Ev(1, EventKind.CaseClosed));

            // Act:不得抛(异常已在闭包内兜住,触发器只看返回值)
            Assert.DoesNotThrow(() => _trigger.Drain(_events));

            // Assert:闭包被调用(锚点确实命中),但兜底窗口**不**清零
            Assert.AreEqual(1, _fires);
            Assert.AreEqual(0, _notifies);
        }

        // ── 游标与流替换(EventStream.Restore,读档)──

        [Test]
        public void test_anchorCheckpoint_streamReplacedShorter_noSpuriousFire_newAnchorsStillFire()
        {
            // (原方法名 `..._clampsCursor_...` —— 2026-10-25 AT4 证伪轮实测开头钳制冗余已删:
            //  流尾无条件对齐本身就自愈,改名如实。钉的属性不变。)
            // Arrange:长流无锚点,游标推到 6;读档换流(变短,且尾部恰是锚点)
            for (long t = 1; t <= 6; t++)
                _events.Add(Ev(t, EventKind.ActorCellEntered));
            _trigger.Drain(_events);
            Assert.AreEqual(0, _fires);

            // Act:Restore 后的短流(3 条,第 3 条是锚点 —— 已在存档里,不得触发)
            var restored = new List<SimEvent>
            {
                Ev(1, EventKind.DiseaseOnset),
                Ev(2, EventKind.ActorCellEntered),
                Ev(3, EventKind.CaseClosed),
            };
            _trigger.Drain(restored);

            // Assert:游标钳制 ⇒ 不重放历史锚点
            Assert.AreEqual(0, _fires);

            // Act + Assert:换流后的**新**锚点正常触发(游标从新流尾继续)
            restored.Add(Ev(4, EventKind.JudgmentRecorded));
            _trigger.Drain(restored);
            Assert.AreEqual(1, _fires);
            Assert.AreEqual(1, _notifies);
        }

        [Test]
        public void test_anchorCheckpoint_resetCursor_rescansFromStart()
        {
            // Arrange:换流后未 drain 即 ResetCursor(Load 接线路径的义务调用)
            for (long t = 1; t <= 4; t++)
                _events.Add(Ev(t, EventKind.ActorCellEntered));
            _trigger.Drain(_events);
            var restored = new List<SimEvent> { Ev(1, EventKind.CaseClosed) };

            // Act
            _trigger.ResetCursor();
            _trigger.Drain(restored);

            // Assert:游标归零 ⇒ 载入流的锚点被看到(Load 接线方决定这是否合意;
            // 本钉只钉「ResetCursor 生效」,不裁 Load 路径是否调用)
            Assert.AreEqual(1, _fires);
        }

        // ── 构造期拒收 ──

        [Test]
        public void test_anchorCheckpoint_emptyWhitelist_nullDeps_throws()
        {
            // Arrange + Act + Assert:空白名单 = 事件触发半边恒哑 = 配置错误
            Assert.Throws<ArgumentException>(() => new AnchorCheckpointTrigger(
                () => true, FuncNarrativeContextQuery.AlwaysIn,
                new EventKind[0]));
            Assert.Throws<ArgumentNullException>(() => new AnchorCheckpointTrigger(
                null, FuncNarrativeContextQuery.AlwaysIn, AnchorCheckpointTrigger.DefaultAnchors));
            Assert.Throws<ArgumentNullException>(() => new AnchorCheckpointTrigger(
                () => true, null, AnchorCheckpointTrigger.DefaultAnchors));
            Assert.Throws<ArgumentNullException>(() => new AnchorCheckpointTrigger(
                () => true, FuncNarrativeContextQuery.AlwaysIn, AnchorCheckpointTrigger.DefaultAnchors)
                .Drain(null));
        }

        // ── 白名单内容钉(EVENT_CHECKPOINT_ANCHORS 成员 → Kind 映射,裁定②)──

        [Test]
        public void test_anchorCheckpoint_defaultAnchors_whitelistMatchesRegistryMapping()
        {
            // Arrange + Act
            var anchors = AnchorCheckpointTrigger.DefaultAnchors;

            // Assert:恰三员,逐员字面断言(映射 = 2026-10-25 裁定②;白名单只增不删 ——
            // 删员 = 事件触发失效退回首轮粒度洞,增员须 registry `EVENT_CHECKPOINT_ANCHORS` 同批)
            Assert.AreEqual(3, anchors.Count);
            Assert.Contains(EventKind.JudgmentRecorded, (System.Collections.ICollection)anchors);
            Assert.Contains(EventKind.CaseClosed, (System.Collections.ICollection)anchors);
            Assert.Contains(EventKind.HouseCallStarted, (System.Collections.ICollection)anchors);
        }
    }
}
