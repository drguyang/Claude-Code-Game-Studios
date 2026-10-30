// `TD-sim-02` / `ADR-035 §⑥` 过量支 —— 53 侧结算器(`OverdoseSettler`)夹具。
//
// 权威:
//   ADR-035 §①(四层所有权:9 出药量 / 53 定档与后果)· §④(载荷形状)· §⑥(解除条件)
//   用户 2026-10-30 两项裁定:9 走独立状态量 · 「过量」按同一味药累积
//
// ⚠️ **本件用盘上真材料**:
//   代价表 = `53_cost_table.cooked.bytes` 经**生产解码器**解出(含过量档位线);
//   写出的 `TreatmentHarmOnset` 用**生产编码器**编码。
//   唯一「人造」的是上游 `VitalsDto.PeakDrugLoad` —— 它的真求值由
//   `PeakDrugLoadTest`(9 侧纯函数)与 `VitalsDto` 的接线共同覆盖。
//
// ⚠️ 本件证的是「**判据链能跑通**」,不是「过量在实玩中会发生」——
//   后者取决于药的 `drug_potency` / `half_life` 与阈值的关系(数值轮已给值)。

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
    /// <summary>过量结算器 —— 真表真查、两档映射、只升不降、幂等、tick 边沿。</summary>
    [TestFixture]
    public sealed class OverdoseSettlerTest
    {
        private static string RepoRoot
            => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));

        private static IReadOnlyList<DiseaseRegistryEntry> Registry()
            => DiseaseRegistryBinder.Bind(
                File.ReadAllText(Path.Combine(RepoRoot, "assets", "data", "disease_registry.json"))).Rows;

        private static CostTable53DataSet RealData()
            => CostTable53CookedCodec.Read(
                File.ReadAllBytes(Path.Combine(Application.dataPath, "DataCooked",
                    DataBakeMenu.CostTableCookedAssetName)), Registry());

        private static int InjuryIdOf(string key)
        {
            foreach (DiseaseRegistryEntry e in Registry())
                if (e != null && e.DiseaseKey == key) return e.DiseaseId;
            Assert.Fail($"注册表里没有 {key} —— 夹具前提不成立");
            return -1;
        }

        /// <summary>`Fix` 从「整数分子 / 分母」构造(禁浮点字面量)。</summary>
        private static Fix Ratio(int num, int den) => new Fix((long)num * Fix.OneRaw / den);

        /// <summary>流 + 写入面是**同一个对象**(游标与写入必须同一条流,同禁忌支的理由)。</summary>
        private sealed class RecordingStream : IReadOnlyList<SimEvent>, IEventSink
        {
            private readonly List<SimEvent> _events = new List<SimEvent>();
            public int Count => _events.Count;
            public SimEvent this[int i] => _events[i];
            public void Append(in SimEvent e) => _events.Add(e);
            public IEnumerator<SimEvent> GetEnumerator() => _events.GetEnumerator();
            IEnumerator IEnumerable.GetEnumerator() => _events.GetEnumerator();
        }

        /// <summary>按病人给 `PeakDrugLoad` 的桩;记录被查询的病人(证「无用药不检查」)。</summary>
        private sealed class StubVitals : IVitalsQuery
        {
            private readonly Dictionary<int, Fix> _loads = new Dictionary<int, Fix>();
            public readonly List<int> Queried = new List<int>();

            public StubVitals Set(int patient, Fix load) { _loads[patient] = load; return this; }

            public VitalsDto GetVitals(PatientId p)
            {
                Queried.Add(p.Value);
                Fix load = _loads.TryGetValue(p.Value, out Fix v) ? v : Fix.Zero;
                return new VitalsDto(0f, 0f, signChannelMask: 0, signCount: 0, peakDrugLoad: load);
            }
        }

        /// <summary>
        /// 一次装配的三个件:**流 / 池 / 结算器**。
        /// <para>⚠️ encoder 与 decoder **必须共用同一个 <see cref="InMemoryBlobPool"/>** ——
        /// 编码写入池、解码按 `BlobId` 从池里取;两个池会让解码查无此 id(实测踩过)。</para>
        /// </summary>
        private sealed class Harness
        {
            public readonly RecordingStream Stream = new RecordingStream();
            public readonly InMemoryBlobPool Pool = new InMemoryBlobPool();
            public readonly IPayloadEncoder Encoder;
            public readonly OverdoseSettler Settler;

            public Harness(StubVitals vitals, CostTable53DataSet data)
            {
                Encoder = new PayloadEncoder(Pool);
                Settler = new OverdoseSettler(Stream, new PayloadDecoder(Pool), vitals, data.Table,
                    data.OverdoseBandThresholds, Encoder, Stream);
            }

            /// <summary>往流里塞一条用药事件 —— 只用于**驱动结算器的脏标记**,载荷内容与药量无关。</summary>
            public void AddDrugMarker(long tick, int patient)
            {
                var drug = new DrugTreatmentAppliedPayload(tick, treatmentId: 1, actorId: 7, polarity: 0,
                    drugPotency: new Fix(Fix.OneRaw), halfLife: 60L, seq: 0L,
                    contraindicationBand: DrugTreatmentAppliedPayload.NoContraindication);
                Stream.Append(new SimEvent(tick, new PatientId(patient), -1,
                    EventKind.DrugTreatmentApplied,
                    Encoder.Encode(EventKind.DrugTreatmentApplied, drug)));
            }

            public TreatmentHarmOnsetPayload LastHarm()
            {
                SimEvent harm = Stream[Stream.Count - 1];
                Assert.That(harm.Kind, Is.EqualTo(EventKind.TreatmentHarmOnset));
                return new PayloadDecoder(Pool).Decode<TreatmentHarmOnsetPayload>(
                    EventKind.TreatmentHarmOnset, in harm.Payload);
            }
        }

        // ══════════════════════ 一、真表正查(两档)══════════════════════

        [Test]
        public void test_band0_settlesToRealTableRow()
        {
            var h = new Harness(new StubVitals().Set(3, Ratio(1, 1)), RealData());
            h.AddDrugMarker(tick: 100, patient: 3);

            Assert.That(h.Settler.Step(100), Is.EqualTo(1), "越档 0 应结算出一条伤害");

            Assert.That(h.Stream[h.Stream.Count - 1].Tick, Is.EqualTo(100L));
            Assert.That(h.Stream[h.Stream.Count - 1].Patient.Value, Is.EqualTo(3));

            TreatmentHarmOnsetPayload p = h.LastHarm();
            Assert.That(p.HarmKind, Is.EqualTo((int)CostHarmKind.Overdose), "harm_kind 须是过量");
            Assert.That(p.InjuryId, Is.EqualTo(InjuryIdOf("INJ_DRUG_HARM")));
            Assert.That(p.SeverityBand, Is.EqualTo(3), "档 0 = 药毒 / 程度 3");
        }

        [Test]
        public void test_band1_settlesToRealTableRow()
        {
            var h = new Harness(new StubVitals().Set(4, Ratio(3, 2)), RealData());
            h.AddDrugMarker(tick: 200, patient: 4);

            Assert.That(h.Settler.Step(200), Is.EqualTo(1), "越过第二档应结算出一条伤害");

            TreatmentHarmOnsetPayload p = h.LastHarm();
            Assert.That(p.InjuryId, Is.EqualTo(InjuryIdOf("INJ_DRUG_HARM")));
            Assert.That(p.SeverityBand, Is.EqualTo(4), "档 1 = 药毒 / 程度 4");
        }

        // ══════════════════════ 二、不该发的情形 ══════════════════════

        [Test]
        public void test_belowThreshold_emitsNothing()
        {
            var h = new Harness(new StubVitals().Set(3, Ratio(1, 4)), RealData());
            h.AddDrugMarker(tick: 100, patient: 3);

            int before = h.Stream.Count;
            Assert.That(h.Settler.Step(100), Is.Zero, "未达最低档位线 ⇒ 零事件");
            Assert.That(h.Stream.Count, Is.EqualTo(before), "不得写入任何事件");
        }

        [Test]
        public void test_noDrugEvent_noEvaluation()
        {
            // 脏标记只由**用药事件**点亮 —— 没有用药的病人一次都不该被查
            // (这是「每 tick 遍历全部病人」与「只查有用药的」的判据)。
            var vitals = new StubVitals().Set(9, Ratio(2, 1));
            var h = new Harness(vitals, RealData());

            Assert.That(h.Settler.Step(100), Is.Zero);
            Assert.That(vitals.Queried, Is.Empty, "无用药事件时不得查询任何病人");
            Assert.That(h.Stream.Count, Is.Zero);
        }

        [Test]
        public void test_tickEdge_doesNotSettleEarly()
        {
            var h = new Harness(new StubVitals().Set(3, Ratio(2, 1)), RealData());
            h.AddDrugMarker(tick: 100, patient: 3);

            Assert.That(h.Settler.Step(99), Is.Zero, "tick 未到 ⇒ 不得提前结算");
            Assert.That(h.Settler.Step(100), Is.EqualTo(1), "到点后结算");
        }

        // ══════════════════════ 三、只升不降 / 幂等 ══════════════════════

        [Test]
        public void test_bandDecayBackDown_doesNotResettle()
        {
            // 药量回落后再回到同档 ⇒ 不得重复发(已造成的伤害不可逆)。
            var vitals = new StubVitals().Set(3, Ratio(1, 1));
            var h = new Harness(vitals, RealData());
            h.AddDrugMarker(tick: 100, patient: 3);

            Assert.That(h.Settler.Step(100), Is.EqualTo(1), "首次越档");

            vitals.Set(3, Fix.Zero);
            h.AddDrugMarker(tick: 200, patient: 3);
            Assert.That(h.Settler.Step(200), Is.Zero, "药量回落 ⇒ 不发");

            vitals.Set(3, Ratio(1, 1));
            h.AddDrugMarker(tick: 300, patient: 3);
            Assert.That(h.Settler.Step(300), Is.Zero, "回到同一档 ⇒ 不重发(只升不降)");
        }

        [Test]
        public void test_higherBandAfterLowerBand_isEmitted()
        {
            // 但**更高档**必须发 —— 「只升不降」不是「只发一次」。
            var vitals = new StubVitals().Set(3, Ratio(1, 1));
            var h = new Harness(vitals, RealData());
            h.AddDrugMarker(tick: 100, patient: 3);
            Assert.That(h.Settler.Step(100), Is.EqualTo(1));

            vitals.Set(3, Ratio(3, 2));
            h.AddDrugMarker(tick: 200, patient: 3);
            Assert.That(h.Settler.Step(200), Is.EqualTo(1), "升到档 1 须再发一条");
            Assert.That(h.Settler.EmittedCount, Is.EqualTo(2));

            TreatmentHarmOnsetPayload p = h.LastHarm();
            Assert.That(p.SeverityBand, Is.EqualTo(4), "第二条须是档 1(不是把档 0 又发一遍)");
        }

        /// <summary>**真重放**:同一条流上重建结算器(模拟读档)⇒ 已有伤害不得重发。</summary>
        [Test]
        public void test_replayOnSameStream_doesNotEmitTwice()
        {
            var vitals = new StubVitals().Set(3, Ratio(3, 2));
            var h = new Harness(vitals, RealData());
            h.AddDrugMarker(tick: 100, patient: 3);
            Assert.That(h.Settler.Step(100), Is.EqualTo(1));

            // 用同一个 RecordingStream 重建结算器(模拟读档:流已在,内存状态重建)
            var encoder = new PayloadEncoder(h.Pool);
            var replayed = new OverdoseSettler(h.Stream, new PayloadDecoder(h.Pool), vitals,
                RealData().Table, RealData().OverdoseBandThresholds, encoder, h.Stream);

            Assert.That(replayed.Step(100), Is.Zero,
                "重建后同档 ⇒ 幂等跳过(判据 = 流里已有的 TreatmentHarmOnset)");
        }

        // ══════════════════════ 四、未启用的惰性形态 ══════════════════════

        [Test]
        public void test_emptyThresholds_neverEmits()
        {
            // 过量支未启用(空阈值)⇒ 与空表同款:任何药量都不发。
            CostTable53DataSet real = RealData();
            var data = new CostTable53DataSet(real.Table, real.ConfigVersion, Array.Empty<Fix>());
            var vitals = new StubVitals().Set(3, Ratio(10, 1));
            var h = new Harness(vitals, data);
            h.AddDrugMarker(tick: 100, patient: 3);

            Assert.That(h.Settler.Step(100), Is.Zero, "阈值空 ⇒ 过量支未启用,零事件");
        }

        [Test]
        public void test_nullArguments_throw()
        {
            Assert.Throws<ArgumentNullException>(() => new OverdoseSettler(
                null, null, null, null, null, null, null));
        }
    }

    /// <summary>`OverdoseEvaluator` —— 定档纯函数的边界(与结算器解耦,可单测)。</summary>
    [TestFixture]
    public sealed class OverdoseEvaluatorTest
    {
        private static Fix Ratio(int num, int den) => new Fix((long)num * Fix.OneRaw / den);

        private static readonly Fix[] Bands = { Ratio(1, 1), Ratio(3, 2) };

        [Test]
        public void test_emptyThresholds_returnsSentinel()
        {
            Assert.That(OverdoseEvaluator.Evaluate(Ratio(10, 1), null),
                Is.EqualTo(OverdoseEvaluator.NoOverdose), "null 阈值 ⇒ 过量支未启用");
            Assert.That(OverdoseEvaluator.Evaluate(Ratio(10, 1), Array.Empty<Fix>()),
                Is.EqualTo(OverdoseEvaluator.NoOverdose), "空阈值 ⇒ 同上");
        }

        [Test]
        public void test_exactlyAtThreshold_hits()
        {
            // 判据是 `load ≥ threshold`(**闭区间下界**)—— 恰等于须命中。
            // 若把 `>=` 写成 `>`,本条转红。
            Assert.That(OverdoseEvaluator.Evaluate(Ratio(1, 1), Bands), Is.EqualTo(0),
                "恰等于档 0 下界 ⇒ 命中档 0");
            Assert.That(OverdoseEvaluator.Evaluate(Ratio(3, 2), Bands), Is.EqualTo(1),
                "恰等于档 1 下界 ⇒ 命中档 1");
        }

        [Test]
        public void test_betweenThresholds_returnsLowerBand()
        {
            // 处于 1 与 1.5 之间 ⇒ 档 0(未到档 1 的下界)
            Assert.That(OverdoseEvaluator.Evaluate(Ratio(5, 4), Bands), Is.EqualTo(0));
        }

        [Test]
        public void test_belowLowest_returnsSentinel()
        {
            Assert.That(OverdoseEvaluator.Evaluate(Ratio(3, 4), Bands),
                Is.EqualTo(OverdoseEvaluator.NoOverdose), "未达最低档线 ⇒ 哨兵(不是档 0)");
        }

        [Test]
        public void test_aboveAll_returnsHighestBand()
        {
            Assert.That(OverdoseEvaluator.Evaluate(Ratio(100, 1), Bands), Is.EqualTo(1),
                "越过全部档线 ⇒ 最高档(不是越界抛)");
        }

        [Test]
        public void test_zeroLoad_isSentinel()
        {
            Assert.That(OverdoseEvaluator.Evaluate(Fix.Zero, Bands),
                Is.EqualTo(OverdoseEvaluator.NoOverdose), "无药量 ⇒ 哨兵");
        }
    }
}
