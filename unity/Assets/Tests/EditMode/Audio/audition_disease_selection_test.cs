// 权威来源:docs/tech-debt-register.md `TD-listen-01` 的 **L-3**(病程被占位曲线压到 1.55 s)
//   裁定(2026-10-29 用户):**乙案** —— 另配一条听测专用病种,`DiseaseId = 1` 原封不动守 Q1。
//
// ⚠️ **本夹具钉两件事**(缺任一件,L-3 就只是「代码改了」而不是「真的能选到」):
//   ① `PatientAppearedDriver(registryIndex: 1)` 实际 spawn 出的 `DiseaseOnset.DiseaseId == 2`
//     —— 停用 `registryIndex`(恒取 0)⇒ 本测恰红;
//   ② 生产注册表**确有第二条**,且它的 `TauRise` 严格长于第一条(听测曲线的存在性,
//     而不是只有一句注释)。
//
// ⚠️ 测试纪律:arrange/act/assert · 无随机 · 无时间依赖 · 零文件系统 I/O。

using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.PatientAI;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.DiseaseSimulation;
using DaYiJingCheng.Sim.World;

namespace DaYiJingCheng.Tests.Unit.Audio
{
    /// <summary>`TD-listen-01` L-3 乙案:听测专用病种的可选性与存在性。</summary>
    [TestFixture]
    public sealed class AuditionDiseaseSelectionTest
    {
        /// <summary>记录型 sink(生产契约就是一行 `Append`)。</summary>
        private sealed class RecordingSink : IEventSink
        {
            public readonly List<SimEvent> Events = new List<SimEvent>();
            public void Append(in SimEvent e) => Events.Add(e);
        }

        private static DiseaseRegistryEntry Entry(int id, string key)
            => new DiseaseRegistryEntry { DiseaseId = id, DiseaseKey = key };

        /// <summary>按 `registryIndex` spawn 一个病人,回读 `DiseaseOnset` 的 disease_id。</summary>
        private static int SpawnDiseaseIdWith(int registryIndex)
        {
            var pool = new InMemoryBlobPool();
            var encoder = new PayloadEncoder(pool);
            var decoder = new PayloadDecoder(pool);
            var sink = new RecordingSink();
            var spawner = new PatientSpawner(new IdAuthority(), sink, encoder, 12345UL);
            var presence = new PresenceRegistry();
            var registry = new[] { Entry(1, "fallback"), Entry(2, "fallback_audition") };

            new PatientAppearedDriver(spawner, presence, registry, registryIndex).OnTickEdge(0);

            Assert.That(sink.Events.Count, Is.EqualTo(1), "前置:一拍须恰写一条 DiseaseOnset");
            DiseaseOnsetPayload p = decoder.Decode<DiseaseOnsetPayload>(
                EventKind.DiseaseOnset, sink.Events[0].Payload);
            return p.DiseaseId;
        }

        [Test]
        public void test_registryIndexOfOne_spawnsAuditionDisease()
        {
            Assert.That(SpawnDiseaseIdWith(1), Is.EqualTo(2),
                "registryIndex = 1 ⇒ 必须 spawn 注册表第 2 条(DiseaseId = 2)。" +
                "取到 1 = registryIndex 没被用上,L-3 的听测病种永远选不到");
        }

        [Test]
        public void test_registryIndexOfZero_spawnsWiringDisease()
        {
            // 反向配对:证明「索引真的在被选」,而不是「一律取最后一条」。
            Assert.That(SpawnDiseaseIdWith(0), Is.EqualTo(1),
                "registryIndex = 0 ⇒ 必须是接线病种(DiseaseId = 1)。" +
                "取到 2 = 索引被忽略,生产行为已被悄悄改掉");
        }

        [Test]
        public void test_productionFallbackRegistry_containsLongerAuditionCurve()
        {
            MethodInfo m = typeof(CompositionRoot).GetMethod(
                "CreateFallbackRegistry", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(m, Is.Not.Null, "前置:生产注册表工厂须存在(改名 ⇒ 本测须同步更新)");

            var registry = (IReadOnlyList<DiseaseRegistryEntry>)m.Invoke(null, null);

            Assert.That(registry.Count, Is.EqualTo(2), "生产注册表须有 2 条(接线 + 听测)");
            Assert.That(registry[0].DiseaseId, Is.EqualTo(1), "第 0 条 = 接线病种,Q1 判据所钉");
            Assert.That(registry[1].DiseaseId, Is.EqualTo(2), "第 1 条 = 听测病种");
            Assert.That(registry[1].Curve, Is.Not.Null, "听测病种须带真曲线");
            Assert.That(registry[1].Curve.TauRise.Raw,
                Is.GreaterThan(registry[0].Curve.TauRise.Raw),
                "听测曲线的上升常数须**严格长于**接线曲线 —— " +
                "否则可听窗口仍被压在 1 s 内,L-3 等于没修");
        }

        // ══════════════════════════════════════════════════════════════════════
        // 装配层 —— 上面三条只证「驱动支持索引」,**不证「装配层能把它设成 1」**
        // ══════════════════════════════════════════════════════════════════════
        //
        // 🔁 **2026-10-29 补(用户裁定「4 处前置全修」)**。原形态是
        //   `const bool AuditionDiseaseForListening = false` —— **编译期常量**,
        //   听测须改代码重编,而「改完忘改回」会把听测曲线漏进出货包(静默)。
        //   现改为装配期形参 `patientRegistryIndexOverride`(与 L-2 同构)。
        //
        // ⚠️ 判别力:把 `patientRegistryIndexOverride ?? 0` 改回 `int registryIndex = 0`
        //   (即无视形参)⇒ 下面第一条红。

        /// <summary>装配层**未给覆盖** ⇒ spawn 出的病人是接线病种(`DiseaseId = 1`)。
        /// <para>钉的是「109 处既有调用方零行为变化」—— 它们全不传该形参。</para></summary>
        [Test]
        public void test_assemble_noOverride_spawnsWiringDisease()
        {
            Assert.That(SpawnDiseaseIdViaAssemble(null), Is.EqualTo(1),
                "未给覆盖 ⇒ 必须是接线病种(DiseaseId = 1)。取到 2 = 生产行为被悄悄改掉了");
        }

        /// <summary>装配层**给覆盖 = 1** ⇒ spawn 出的病人是听测病种(`DiseaseId = 2`)。
        /// <para>这是 L-3 的**核心判据**:没有它,「听测病种可选」只停留在驱动层。</para></summary>
        [Test]
        public void test_assemble_override1_spawnsAuditionDisease()
        {
            Assert.That(SpawnDiseaseIdViaAssemble(1), Is.EqualTo(2),
                "给覆盖 = 1 ⇒ 必须是听测病种(DiseaseId = 2)。取到 1 = 装配层没把形参传下去");
        }

        /// <summary>装配层**越界索引 ⇒ fail-loud**,不静默钳位。
        /// <para>静默钳到 0 会让「我设了 5 但听的是 0」成为静默故障(与 L-2 同一判据)。</para></summary>
        [Test]
        public void test_assemble_outOfRangeIndex_failsLoud()
        {
            Assert.Throws<System.InvalidOperationException>(
                () => SpawnDiseaseIdViaAssemble(5),
                "越界索引须 fail-loud(不静默钳位)");
        }

        /// <summary>经**真装配**驱动一拍,回读 `DiseaseOnset.DiseaseId`。
        /// <para>⚠️ **必须让装配出来的 `PatientAppearedDriver` 挑病种** —— 若本辅助方法自己
        /// 按 `overrideIndex` 调 `SpawnNext`,那测的就是本方法自己,形参有没有传到驱动**完全测不到**
        /// (自指夹具,承 `feedback_self_referential_fixture`)。</para>
        /// <para>走 `CompositionRoot.Assemble`(caller 传合成注册表 ⇒ ① 级),
        /// 不用真 dataProvider —— 本测不关心其余数据表。</para></summary>
        private static int SpawnDiseaseIdViaAssemble(int? overrideIndex)
        {
            var registry = new[] { Entry(1, "fallback"), Entry(2, "fallback_audition") };
            CompositionRootServices bag = CompositionRoot.Assemble(
                registry: registry,
                patientRegistryIndexOverride: overrideIndex);

            // 装配出来的驱动跑一拍 —— 病种由**它的** registryIndex 决定,不由本测决定。
            bag.PatientAppearedDriver.OnTickEdge(0);

            SimEvent? onset = null;
            for (int i = 0; i < bag.Stream.Events.Count; i++)
                if (bag.Stream.Events[i].Kind == EventKind.DiseaseOnset) onset = bag.Stream.Events[i];
            Assert.That(onset.HasValue, Is.True, "前置:驱动一拍后须有恰好一条 DiseaseOnset");

            DiseaseOnsetPayload p = bag.Decoder.Decode<DiseaseOnsetPayload>(
                EventKind.DiseaseOnset, onset.Value.Payload);
            return p.DiseaseId;
        }
    }
}
