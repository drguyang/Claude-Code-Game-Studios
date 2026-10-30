// ADR-035 T2 —— 禁忌结算器(`ContraindicationSettler`)夹具。
//
// 权威:ADR-035 §①B(触发条件面归 11)/ ②(表键 `(harm_kind, band)`)/ ④(载荷形状)/ ⑤(写者面)
//
// ⚠️ 本件是**机制正面路径第一次跑真实材料**:
//   代价表 = 盘上真产物(`53_cost_table.cooked.bytes`,3 行)经**生产解码器**解出;
//   注册表 = 真 `disease_registry.json`;
//   写出的 `TreatmentHarmOnset` 用**生产编码器**编码。
//   唯一「人造」的部分是上游那条 `DrugTreatmentApplied` 的 `ContraindicationBand` ——
//   因为 11 的判定仍未实现(它是本链的**输入**,不是本链的产物)。

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using DaYiJingCheng.EditorTools.Bake;
using DaYiJingCheng.Gameplay.Presentation;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Consequences;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;
using UnityEngine;

namespace DaYiJingCheng.Tests.MedConsequences
{
    /// <summary>禁忌结算器 —— 真表真查、三档映射、哨兵 / miss / 幂等 / tick 边沿。</summary>
    [TestFixture]
    public sealed class ContraindicationSettlerTest
    {
        private static string RepoRoot
            => MedConsRepoPaths.RepoRoot;

        private static IReadOnlyList<DiseaseRegistryEntry> Registry()
            => DiseaseRegistryBinder.Bind(
                File.ReadAllText(Path.Combine(RepoRoot, "assets", "data", "disease_registry.json"))).Rows;

        /// <summary>盘上**真**代价表(经生产解码器 —— 与生产装配走同一条路径)。</summary>
        private static CostTable53.Table RealTable()
            => CostTable53CookedCodec.Read(
                File.ReadAllBytes(MedConsRepoPaths.CookedFile(DataBakeMenu.CostTableCookedAssetName)), Registry()).Table;

        private static int InjuryIdOf(string key)
        {
            foreach (DiseaseRegistryEntry e in Registry())
                if (e != null && e.DiseaseKey == key) return e.DiseaseId;
            Assert.Fail($"注册表里没有 {key} —— 夹具前提不成立");
            return -1;
        }

        /// <summary>流 + 写入面**是同一个对象**(结算器的游标与写入必须同一条流,见 Step 的 remarks)。</summary>
        private sealed class RecordingStream : IReadOnlyList<SimEvent>, IEventSink
        {
            private readonly List<SimEvent> _events = new List<SimEvent>();
            public int Count => _events.Count;
            public SimEvent this[int i] => _events[i];
            public void Append(in SimEvent e) => _events.Add(e);
            public IEnumerator<SimEvent> GetEnumerator() => _events.GetEnumerator();
            IEnumerator IEnumerable.GetEnumerator() => _events.GetEnumerator();
        }

        private static ContraindicationSettler Build(
            RecordingStream stream, InMemoryBlobPool pool, CostTable53.Table table,
            out IPayloadEncoder encoder)
        {
            encoder = new PayloadEncoder(pool);
            var decoder = new PayloadDecoder(pool);
            return new ContraindicationSettler(stream, decoder, table, encoder, stream);
        }

        /// <summary>往流里塞一条带指定档的用药事件(11 的产物形状)。</summary>
        private static void AddDrug(RecordingStream stream, IPayloadEncoder encoder, long tick, int patient,
                                    int band)
        {
            var drug = new DrugTreatmentAppliedPayload(tick, treatmentId: 1, actorId: 7, polarity: 0,
                drugPotency: new Fix(65536L), halfLife: 60L, seq: 0L, contraindicationBand: band);
            stream.Append(new SimEvent(tick, new PatientId(patient), -1, EventKind.DrugTreatmentApplied,
                encoder.Encode(EventKind.DrugTreatmentApplied, drug)));
        }

        // ══════════════════════ 一、真表正查 ══════════════════════

        /// <summary>三档逐条查真表:档 0 → 钝击/2,档 1 → 药毒/3,档 2 → 药毒/4。</summary>
        [Test]
        public void test_threeBands_settleToRealTableRows()
        {
            foreach ((int band, string injuryKey, int severity) in new[]
                     {
                         (0, "INJ_BLUNT", 2),
                         (1, "INJ_DRUG_HARM", 3),
                         (2, "INJ_DRUG_HARM", 4),
                     })
            {
                var stream = new RecordingStream();
                var pool = new InMemoryBlobPool();
                ContraindicationSettler s = Build(stream, pool, RealTable(), out IPayloadEncoder enc);
                AddDrug(stream, enc, tick: 100, patient: 3, band: band);

                Assert.That(s.Step(100), Is.EqualTo(1), $"档 {band} 应结算出一条伤害");

                var decoder = new PayloadDecoder(pool);
                SimEvent harm = stream[stream.Count - 1];
                Assert.That(harm.Kind, Is.EqualTo(EventKind.TreatmentHarmOnset));
                Assert.That(harm.Tick, Is.EqualTo(100L), "伤害事件的时间 = 用药事件的时间");
                Assert.That(harm.Patient.Value, Is.EqualTo(3));

                var p = decoder.Decode<TreatmentHarmOnsetPayload>(EventKind.TreatmentHarmOnset, in harm.Payload);
                Assert.That(p.InjuryId, Is.EqualTo(InjuryIdOf(injuryKey)), $"档 {band} 的 injury_id");
                Assert.That(p.HarmKind, Is.EqualTo((int)CostHarmKind.Contraindication));
                Assert.That(p.SeverityBand, Is.EqualTo(severity), $"档 {band} 的 severity_band");
                Assert.That(p.PatientId, Is.EqualTo(3));
                Assert.That(p.Tick, Is.EqualTo(100L));
            }
        }

        // ══════════════════════ 二、不该发的情形(逐条反例)══════════════════════

        /// <summary>档 = 哨兵(-1)⇒ 不写任何事件。**这是今日生产路径的常态**(11 判定未实现)。</summary>
        [Test]
        public void test_noContraindicationSentinel_emitsNothing()
        {
            var stream = new RecordingStream();
            ContraindicationSettler s = Build(stream, new InMemoryBlobPool(), RealTable(), out IPayloadEncoder enc);
            AddDrug(stream, enc, tick: 100, patient: 3,
                band: DrugTreatmentAppliedPayload.NoContraindication);

            Assert.That(s.Step(100), Is.Zero);
            Assert.That(stream.Count, Is.EqualTo(1), "流里只应有那条用药事件");
        }

        /// <summary>档在表里查不到 ⇒ **合法 miss**,不抛、不写。</summary>
        [Test]
        public void test_bandWithoutRow_isLegalMissNotAnError()
        {
            var stream = new RecordingStream();
            ContraindicationSettler s = Build(stream, new InMemoryBlobPool(), RealTable(), out IPayloadEncoder enc);
            AddDrug(stream, enc, tick: 100, patient: 3, band: 7);

            Assert.That(s.Step(100), Is.Zero, "表里没有档 7 ⇒ miss");
            Assert.That(stream.Count, Is.EqualTo(1));
        }

        /// <summary>空表 = 惰性形态:任何档都 miss(与「机制未启用」逐位一致)。</summary>
        [Test]
        public void test_emptyTable_isLazy()
        {
            var stream = new RecordingStream();
            CostTable53.Table empty = CostTable53.CreateTable(Registry(), new List<CostTable53.Row>(), null);
            ContraindicationSettler s = Build(stream, new InMemoryBlobPool(), empty, out IPayloadEncoder enc);
            AddDrug(stream, enc, tick: 100, patient: 3, band: 1);

            Assert.That(s.Step(100), Is.Zero);
        }

        /// <summary>tick 边沿:未到点不结算,到点才结算(与 9 的 `ApplyNewEvents` 同口径)。</summary>
        [Test]
        public void test_tickEdge_doesNotSettleEarly()
        {
            var stream = new RecordingStream();
            ContraindicationSettler s = Build(stream, new InMemoryBlobPool(), RealTable(), out IPayloadEncoder enc);
            AddDrug(stream, enc, tick: 500, patient: 3, band: 1);

            Assert.That(s.Step(499), Is.Zero, "500 的事件不得在 499 结算");
            Assert.That(s.Step(500), Is.EqualTo(1));
        }

        /// <summary>幂等:同一条用药事件重放(读档重建)不得重复发伤害。</summary>
        [Test]
        public void test_replay_doesNotEmitTwice()
        {
            var stream = new RecordingStream();
            var pool = new InMemoryBlobPool();
            var encoder = new PayloadEncoder(pool);
            AddDrug(stream, encoder, tick: 100, patient: 3, band: 1);

            // 第一次结算 → 发出一条伤害
            var first = new ContraindicationSettler(
                stream, new PayloadDecoder(pool), RealTable(), encoder, stream);
            Assert.That(first.Step(100), Is.EqualTo(1));
            int afterFirst = stream.Count;

            // 重放:新建结算器(游标归零),流里**已含**那条伤害 ⇒ 不得再发
            var second = new ContraindicationSettler(
                stream, new PayloadDecoder(pool), RealTable(), encoder, stream);
            Assert.That(second.Step(100), Is.Zero, "重放须靠去重键挡住,不得重复发");
            Assert.That(stream.Count, Is.EqualTo(afterFirst), "流长不得变");
        }

        /// <summary>构造参数缺失 ⇒ 立即抛(不静默降级成「不结算」)。</summary>
        [Test]
        public void test_nullArguments_throw()
        {
            var stream = new RecordingStream();
            var pool = new InMemoryBlobPool();
            var decoder = new PayloadDecoder(pool);
            var enc = new PayloadEncoder(pool);
            CostTable53.Table table = RealTable();

            Assert.Throws<ArgumentNullException>(() => new ContraindicationSettler(null, decoder, table, enc, stream));
            Assert.Throws<ArgumentNullException>(() => new ContraindicationSettler(stream, null, table, enc, stream));
            Assert.Throws<ArgumentNullException>(() => new ContraindicationSettler(stream, decoder, null, enc, stream));
            Assert.Throws<ArgumentNullException>(() => new ContraindicationSettler(stream, decoder, table, null, stream));
            Assert.Throws<ArgumentNullException>(() => new ContraindicationSettler(stream, decoder, table, enc, null));
        }
    }
}
