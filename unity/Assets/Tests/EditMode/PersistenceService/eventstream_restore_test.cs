// `EventStream.Restore` 的三关取舍与折叠行登记 —— TD-persist-03 动作 ①② 测试面。
//
// 权威来源:
//   docs/tech-debt-register.md TD-persist-03(动作 ① 抽象点形状 = 不立新点,BCL 委托 +
//     `EventStream.Restore`;动作 ② 三关三分化 —— 用户 2026-10-24 裁定)
//   ADR-006 Amendment B —— patient_id 高水位含折叠行
//   Sim/EventStream.cs AppendCore —— 去重 / 单调 / 发号器推进与 Append 完全同路
//
// 测试命名: `test_[system]_[scenario]_[expected]`(test-standards);
// 本文件零写盘、零外部状态(fake presence / fake id authority)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Tests.DiseaseSimulation;   // FakeIdAuthority / FakePresenceQuery
using NUnit.Framework;

namespace DaYiJingCheng.Tests.PersistenceService
{
    public class EventStreamRestoreTest
    {
        private EventStream _stream;
        private FakePresenceQuery _presence;

        [SetUp]
        public void SetUp()
        {
            _presence = new FakePresenceQuery();
            _stream = new EventStream(new FakeIdAuthority(), _presence);
        }

        private static SimEvent Ev(long tick, int patient, long seq, EventKind kind)
            => new SimEvent(tick, new PatientId(patient), seq, kind, default);

        private static FoldRow Row(int patientId, int diseaseId)
            => new FoldRow(10L, diseaseId, patientId, 77L, (int)PatientOutcome.Died, 20L);

        // ══════════════════ 三关三分化 ══════════════════

        [Test]
        public void test_restore_absentPatientAtFullCap_doesNotThrow_capBypassed()
        {
            // CAP 绕过(动作 ② 三分化的第一半):读档时在场视图尚未重建 ⇒
            // 「不在场且满 CAP」的恢复事件若走 Append 必误抛;Restore 不得。
            // Arrange —— 在场满 CAP,且恢复事件全部不在场。
            for (int i = 0; i < EventStream.PATIENT_APPEARANCE_CAP; i++)
                _presence.Add(new PatientId(100 + i));

            var events = new[] { Ev(5L, 1, 0L, EventKind.DiseaseOnset) };

            // 同一输入走 Append ⇒ 抛(对照:证明 CAP 关真的存在,不是假对照)。
            Assert.Throws<InvalidOperationException>(
                () => _stream.Append(Ev(5L, 1, -1L, EventKind.DiseaseOnset)),
                "前提对照:同输入走 Append 须撞 CAP(在场满且病人不在场)");

            // Act + Assert
            Assert.DoesNotThrow(() => _stream.Restore(events, new List<FoldRow>()),
                "Restore 绕过 CAP —— 读档不是新病人到场");
            Assert.AreEqual(1, _stream.Count);
        }

        [Test]
        public void test_restore_outOfOrderTicks_throws_monotonicKept()
        {
            // 单调保留(三分化第二半):恢复序 Tick 回退 = 档损坏,fail-loud。
            // ⚠️ 批非原子(替换语义 + 重试自愈,见 Restore doc):违例抛出时
            //    前面条目已入流 —— 断言「逐条应用 + 抛出即停」,不断言整批回滚。
            // Arrange —— 存档序 Tick 非降被破坏。
            var events = new[] { Ev(20L, 1, 0L, EventKind.DiseaseOnset),
                                 Ev(10L, 2, 0L, EventKind.DiseaseOnset) };

            // Act + Assert
            var ex = Assert.Throws<InvalidOperationException>(
                () => _stream.Restore(events, new List<FoldRow>()),
                "恢复序 Tick 回退须抛(BCD-码-3 是流不变量,恢复不豁免)");
            StringAssert.Contains("BCD-码-3", ex.Message);
            Assert.AreEqual(1, _stream.Count,
                "批非原子:第一条已应用,第二条违例即停;重试 Load 经替换语义自愈");
        }

        [Test]
        public void test_restore_duplicateEvent_isRejected_dedupKept()
        {
            // 去重保留(三分化第二半):存档里的重复事件 = 档损坏,幂等拒收。
            var events = new[] { Ev(5L, 1, 0L, EventKind.DiseaseOnset),
                                 Ev(5L, 1, 0L, EventKind.DiseaseOnset) };

            _stream.Restore(events, new List<FoldRow>());

            Assert.AreEqual(1, _stream.Count,
                "重复事件按 Append 同款去重拒收(第二条静默丢)—— 流不变量");
        }

        [Test]
        public void test_restore_sequencedEvents_keepTheirSeq_noReissue()
        {
            // Seq 不重发(三分化第三半):恢复事件已带 Seq ≥ 0 ⇒ 原样保留。
            // Arrange —— Seq 故意「错开」:若重发号会变成 0,1;保留则是 5,9。
            var events = new[] { Ev(5L, 1, 5L, EventKind.DiseaseOnset),
                                 Ev(6L, 1, 9L, EventKind.DiseaseOnset) };

            _stream.Restore(events, new List<FoldRow>());

            Assert.AreEqual(5L, _stream.Events[0].Seq, "已带 Seq 不重发(事件身份不动)");
            Assert.AreEqual(9L, _stream.Events[1].Seq);
        }

        [Test]
        public void test_restore_thenAppend_sameTickPatient_getsNextSeq()
        {
            // 发号器播种(组最大号 + 1,只抬不压):恢复后同 (Tick, Patient) 的新事件
            // 不得领到已被恢复事件占用的号(同 Kind 即撞去重键被静默吞)。
            // ⚠️ 承既有行为(计数器对同组每条事件都前进),恢复组后的首个新号
            //    可能跳一个(3 空缺)—— 身份唯一性是判据,密度不是。
            // Arrange —— 恢复 (tick=5, patient=1, seq=2) ⇒ 播种 _currentSeq = 3。
            _stream.Restore(new[] { Ev(5L, 1, 2L, EventKind.DiseaseOnset) },
                            new List<FoldRow>());

            // Act —— 恢复后新事件:同 tick 同病人,Seq 哨兵 -1。
            _stream.Append(Ev(5L, 1, -1L, EventKind.SkillGrown));

            // Assert
            Assert.AreEqual(4L, _stream.Events[1].Seq,
                "新事件 ≥ 组最大号 + 1(2 已被恢复事件占用;3 被计数器前进跳过)——不撞号即判据");
        }

        // ══════════════════ 折叠行登记 ══════════════════

        [Test]
        public void test_restore_foldedRows_feedPatientHighWater()
        {
            // ADR-006 Amendment B:被折病人的事件已抹,id 只活在折叠行 ⇒
            // 高水位漏掉它们 = 读档后新病人复用已折叠 id。
            // Arrange —— 流内最大病人 id = 9;折叠行里藏着 id = 41。
            var events = new[] { Ev(5L, 9, 0L, EventKind.DiseaseOnset) };
            var rows = new[] { Row(41, 21) };

            // Act
            _stream.Restore(events, rows);
            PatientId next = _stream.GetNextPatientId();

            // Assert
            Assert.AreEqual(42, next.Value,
                "高水位 = max(流内, 折叠行) + 1 —— 折叠行 41 必须计入");
        }

        [Test]
        public void test_restore_duplicateFoldRow_throws()
        {
            // 同 (patientId, diseaseId) 两行 = 档损坏(ADR-010 §二 一行 = 病人 × 病种)。
            var rows = new[] { Row(41, 21), Row(41, 21) };

            var ex = Assert.Throws<InvalidOperationException>(
                () => _stream.Restore(new List<SimEvent>(), rows),
                "折叠行重复 = 档损坏,fail-loud");
            StringAssert.Contains("折叠行重复", ex.Message);
            Assert.AreEqual(0, _stream.FoldedRows.Count, "违例抛出时折叠登记零污染");
        }

        [Test]
        public void test_restore_replacesPriorContent()
        {
            // 替换语义:Restore = 读档重建,先清后恢复 —— 二次 Restore 不得叠加。
            _stream.Restore(new[] { Ev(5L, 1, 0L, EventKind.DiseaseOnset) },
                            new[] { Row(41, 21) });
            _stream.Restore(new[] { Ev(6L, 2, 0L, EventKind.DiseaseOnset) },
                            new List<FoldRow>());

            Assert.AreEqual(1, _stream.Count, "二次 Restore = 替换,不叠加");
            Assert.AreEqual(0, _stream.FoldedRows.Count,
                "新存档零折叠行 ⇒ 旧折叠行随替换消失(流与折叠行同寿命)");
        }

        [Test]
        public void test_restore_foldedRows_exposedViaReadOnlyView()
        {
            // FoldedRows 只读视图:Compose 的回写入参、未来 outcome 消费方都从这里取。
            var rows = new[] { Row(41, 21), Row(42, 22) };
            _stream.Restore(new List<SimEvent>(), rows);

            Assert.AreEqual(2, _stream.FoldedRows.Count);
            Assert.AreEqual(41, _stream.FoldedRows[0].PatientId);
            Assert.AreEqual(22, _stream.FoldedRows[1].DiseaseId);
        }
    }
}
