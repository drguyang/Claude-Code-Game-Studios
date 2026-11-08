// 病人出现的**病种轮转**(2026-11-08 · S26-4 续三)
//
// 病因(真包走查):接线期两名病人恒取注册表**第一条**病种 ⇒ 医师只见得到**一个病种**
// ⇒ 病名册恒 1 条词 ⇒「换词改判 / 旧笔同屏」在真包里**结构上不可达**
// (`[tutorialdiag] 病名册环 … seen=1`,两个病人各查一遍仍是 1)。
//
// 判据:第 n 个病人取 `_diseaseIds[n % Count]`,序列以 `registryIndex` 为起点绕一圈。
// ⚠️ 与 `TD-listen-01` L-3 的 `registryIndex` **不是同一件事**:那个选**起点**,
//    本件定**后续怎么走**;两个测试互补(该件钉「第一个是谁」,本件钉「第二个不同」)。

using System.Collections.Generic;
using NUnit.Framework;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.PatientAI;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.DiseaseSimulation;

namespace DaYiJingCheng.Tests.DiseaseSimulation
{
    /// <summary>接线期病人应拿到**不同**病种(否则病名册永远只有一条词)。</summary>
    [TestFixture]
    public sealed class PatientDiseaseRotationTest
    {
        private sealed class RecordingSink : IEventSink
        {
            public readonly List<SimEvent> Events = new List<SimEvent>();
            public void Append(in SimEvent e) => Events.Add(e);
        }

        private static DiseaseRegistryEntry Entry(int id) => new DiseaseRegistryEntry
        {
            DiseaseId = id,
            DiseaseKey = "k" + id,
        };

        /// <summary>用给定注册表 spawn 两个病人,回读两条 `DiseaseOnset` 的 disease_id。</summary>
        private static int[] SpawnTwo(int registryIndex, params int[] ids)
        {
            var pool = new InMemoryBlobPool();
            var encoder = new PayloadEncoder(pool);
            var decoder = new PayloadDecoder(pool);
            var sink = new RecordingSink();
            var spawner = new PatientSpawner(new IdAuthority(), sink, encoder, 12345UL);
            var presence = new PresenceRegistry();

            var registry = new List<DiseaseRegistryEntry>();
            foreach (int id in ids) registry.Add(Entry(id));

            var driver = new PatientAppearedDriver(spawner, presence, registry, registryIndex);
            driver.OnTickEdge(0);        // 第 1 个
            driver.OnTickEdge(60);       // 第 2 个(SpawnIntervalTicks)

            Assert.That(sink.Events.Count, Is.EqualTo(2),
                "前置:两拍须恰写两条 DiseaseOnset(否则下面的 id 断言读的是空)");

            var got = new int[2];
            for (int i = 0; i < 2; i++)
                got[i] = decoder.Decode<DiseaseOnsetPayload>(EventKind.DiseaseOnset, sink.Events[i].Payload)
                                .DiseaseId;
            return got;
        }

        /// <summary>
        /// 两条病种的注册表 ⇒ 两名病人**不同病种**(病名册才有第二条词)。
        /// <para>反证:把 `_diseaseIds[_spawnedCount % …]` 改回 `registry[registryIndex]`
        /// (轮转前的取法)⇒ 本条红,且红在「第二个病人拿到同一个 id」。</para>
        /// </summary>
        [Test]
        public void test_twoDiseasesRegistry_twoPatients_getDifferentDiseases()
        {
            Assert.That(SpawnTwo(0, 1, 2), Is.EqualTo(new[] { 1, 2 }),
                "第 1 个病人 = 首条,第 2 个病人 = 下一条(否则病名册永远只有一条词)");
        }

        /// <summary>起点 = `registryIndex`(轮转**从它开始**,不是永远从 0)。</summary>
        [Test]
        public void test_rotationStartsAtRegistryIndex()
        {
            Assert.That(SpawnTwo(1, 1, 2, 3), Is.EqualTo(new[] { 2, 3 }),
                "registryIndex = 1 ⇒ 首个病人是第 2 条,第二个是第 3 条(绕圈:不是钳位)");
        }

        /// <summary>
        /// 单条注册表 ⇒ 两名病人同一个病种(**退化**,与轮转前逐位相同;不得崩、不得越界)。
        /// <para>这是「测试 fixture 只有一条病种」时的正常路径,不是错误态。</para>
        /// </summary>
        [Test]
        public void test_singleDiseaseRegistry_bothPatientsSame_noThrow()
        {
            Assert.That(SpawnTwo(0, 7), Is.EqualTo(new[] { 7, 7 }),
                "单条注册表 ⇒ 全部同一个(取模不得把 0 变成越界)");
        }
    }
}
