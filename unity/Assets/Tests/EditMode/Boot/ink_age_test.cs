// sprint-06 G2.2 · 墨龄派生态(EditMode 判据 · 2026-10-11)
//
// 判据(可证伪):
//   ① 落笔后 InkAgeTicks = 当前 tick − **该病人开案**最近一条判断事件 Tick(>0,随 tick 推进递增);
//   ② 未落笔 ⇒ 0(刚落笔形态,湿墨);
//   ③ 复查改写(JudgmentRevised)⇒ 从**最新一条**重算(GDD J(c) 存新值最后一条胜);
//   ④ 病例流事件视图 = EventStream.Events(组合根注入同一实例);
//   ⑤ 🔴 跨病人 —— 病人 B 的判断更晚落笔,**不得**拉低病人 A 的墨龄
//      (2026-10-15 S11-7 补 · story-002 BLK-8 实害链终点;旧实现下本测必红)。
//
// 纯逻辑 EditMode:真 EventStream + 真 CaseOpenWriter + 真 JudgmentWriter +
// 真 SimTickDriver —— 全生产路径。照 JudgmentWriter 测试先例。

using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Boot
{
    /// <summary>墨龄派生态判据(G2.2 —— CasebookVitalsSource.InkAgeTicks)。</summary>
    internal sealed class InkAgeTest
    {
        private CompositionRootServices _bag;
        private CasebookVitalsSource _source;
        private JudgmentWriter _writer;
        private PatientId _patient;

        [SetUp]
        public void SetUp()
        {
            _bag = CompositionRoot.Assemble();
            _source = _bag.CasebookSource;
            _writer = new JudgmentWriter(_bag.EventSink, _bag.Encoder, _bag.Stream.Events,
                                         authorPlayerId: 9001);

            // 建档 + 在场 + 开案(全前置齐)
            _patient = _bag.PatientSpawner.SpawnNext(diseaseId: 1, tick: 0);
            _bag.Presence.AddPresent(_patient, new WorldPos(0, 0, 0));
            _bag.VitalsService.OnTickEdge(0);
            var caseWriter = new CaseOpenWriter(_bag.EventSink, _bag.Presence, _bag.Encoder, _bag.Stream.Events);
            Assert.AreEqual(CaseOpenWriteResult.Opened, caseWriter.TryOpen(1, _patient, default));

            _source.ChangePatient(_patient);
        }

        [Test]
        public void test_inkAge_zero_beforeWrite()
        {
            Assert.AreEqual(0L, _source.InkAgeTicks, "未落笔 ⇒ 墨龄 0(刚落笔姿态)");
        }

        [Test]
        public void test_inkAge_countsSinceLastJudgment()
        {
            // 推进到 current=3 → 落笔于 tick=3 → 再推进到 current=5 ⇒ 墨龄 = 2
            AdvanceTicks(3);
            Assert.AreEqual(JudgmentWriteResult.Recorded,
                _writer.TryRecord(tick: _bag.TickDriver.CurrentTick, _patient, lexiconId: 7, confidence: 2));

            AdvanceTicks(2);   // current → 5
            Assert.AreEqual(2L, _source.InkAgeTicks,
                "墨龄 = 当前tick(5) − 最近Judgment tick(3)= 2");
        }

        [Test]
        public void test_inkAge_rereadsFromLatestRewrite()
        {
            // 初次落笔 tick=3 → 推进到 current=8 → 二次落笔 tick=8 → 推进到 current=9
            AdvanceTicks(3);
            _writer.TryRecord(tick: _bag.TickDriver.CurrentTick, _patient, lexiconId: 7, confidence: 2);
            AdvanceTicks(5);   // current → 8

            _writer.TryRecord(tick: _bag.TickDriver.CurrentTick, _patient, lexiconId: 9, confidence: 3);
            AdvanceTicks(1);   // current → 9

            Assert.AreEqual(1L, _source.InkAgeTicks,
                "改写后从最新一条(8)重算 ⇒ 9−8 = 1(不是最早的 3)");
        }

        [Test]
        public void test_inkAge_clampsNonNegative()
        {
            // 极限:tick 倒流(输入未来 tick)⇒ 差值负,防御钳 0
            _writer.TryRecord(tick: 10, _patient, lexiconId: 7, confidence: 2);
            Assert.AreEqual(0L, _source.InkAgeTicks,
                "落笔 tick > 当前 ⇒ 差值钳 0(时间反演防御,AC-27 同族)");
        }

        /// <summary>🔴 S11-7 判别夹具(story-002 BLK-8 **实害链终点**):跨病人 ——
        /// 病人 B 的判断**更晚**落笔,病人 A 的墨龄**不得**被它拉低。
        /// <para>修复前 <c>LastJudgmentOf</c> 的 <c>caseId</c> 死参 ⇒ 取「整条病例流最后一条
        /// 判断事件」= 病人 B 的(tick=4)⇒ 病人 A 的墨龄算成 1(而非 2)。
        /// 本测走**全生产路径**(CompositionRoot 装配),与 SetUp 同源。</para></summary>
        [Test]
        public void test_inkAge_crossPatient_usesOwnJudgmentNotOtherPatients()
        {
            // 病人 A(SetUp 已建档 / 在场 / 开案)在 tick=3 落笔
            AdvanceTicks(3);                        // current → 3
            Assert.AreEqual(JudgmentWriteResult.Recorded,
                _writer.TryRecord(tick: 3, _patient, lexiconId: 7, confidence: 2));

            // 病人 B:建档 + 在场 + 开案,并在**更晚**的 tick=4 落笔
            PatientId other = _bag.PatientSpawner.SpawnNext(diseaseId: 1, tick: 3);
            _bag.Presence.AddPresent(other, new WorldPos(1, 0, 0));
            var caseWriter = new CaseOpenWriter(_bag.EventSink, _bag.Presence, _bag.Encoder,
                                                _bag.Stream.Events);
            Assert.AreEqual(CaseOpenWriteResult.Opened, caseWriter.TryOpen(3, other, default),
                "病人 B 须开案成功(落笔前置)");
            Assert.AreEqual(JudgmentWriteResult.Recorded,
                _writer.TryRecord(tick: 4, other, lexiconId: 9, confidence: 3));

            AdvanceTicks(2);                        // current → 5

            _source.ChangePatient(_patient);
            Assert.AreEqual(2L, _source.InkAgeTicks,
                "病人 A:5 − 3 = 2;旧实现取全流末条(病人 B 的 tick=4)⇒ 1 —— 墨龄取错病人");

            _source.ChangePatient(other);
            Assert.AreEqual(1L, _source.InkAgeTicks, "病人 B:5 − 4 = 1(反向对照,防「一律返 0」)");
        }

        private void AdvanceTicks(int steps)
        {
            for (int i = 0; i < steps; i++)
                _bag.TickDriver.Advance(0.05d);
        }
    }
}