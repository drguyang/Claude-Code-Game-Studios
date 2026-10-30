// ADR-035 T2 收口的最后一段 —— 9 的 **F2 体征投影**接线(`TD-sim-04` / GDD `D-9-R`)。
//
// 权威:`disease-simulation.md` F2(`Signⱼ = Project(Progress, 病种)ⱼ`)+ `:1561` 的 `D-9-R` 订正注
//       · `RegistrySchema` R1-37(`appear_at ∈ (0,1]`)
//
// ⚠️ 本件证的是**投影规则**,不是「药害能被玩家撞见」—— 后者取决于病程走到够深的位置,
//   归内容 / 数值轮(见 `TD-sim-04` 的关账口径)。
//
// ⚠️ 用的是**真注册表行 + 真 `CompositionRoot.Assemble`**,不是 Fake —— 否则「接线了没」无从判。

using System.Collections.Generic;
using System.IO;
using DaYiJingCheng.EditorTools.Bake;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;
using UnityEngine;

namespace DaYiJingCheng.Tests.DiseaseSimulation
{
    /// <summary>F2 体征投影 —— 逐词条阈值门是否真的在起作用。</summary>
    [TestFixture]
    public sealed class F2SignProjectionTest
    {
        private static string RepoRoot
            => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));

        private static IReadOnlyList<DiseaseRegistryEntry> Registry()
            => DiseaseRegistryBinder.Bind(
                File.ReadAllText(Path.Combine(RepoRoot, "assets", "data", "disease_registry.json"))).Rows;

        private static DiseaseRegistryEntry Row(string key)
        {
            foreach (DiseaseRegistryEntry e in Registry())
                if (e != null && e.DiseaseKey == key) return e;
            Assert.Fail($"注册表里没有 {key} —— 夹具前提不成立");
            return null;
        }

        /// <summary>真装配 + 建档;按序驱动到目标 tick(单调,与生产逐边沿语义一致)。</summary>
        private static (DiseaseVitalsService Svc, PatientId Patient) Open(DiseaseRegistryEntry entry)
        {
            CompositionRootServices bag = CompositionRoot.Assemble(new[] { entry });
            PatientId p = bag.PatientSpawner.SpawnNext(entry.DiseaseId, tick: 0);
            return (bag.VitalsService, p);
        }

        /// <summary>单调驱动器 —— 游标只前进(`OnTickEdge` 重放过往 tick 会破坏病程状态)。</summary>
        private sealed class Driver
        {
            private readonly DiseaseVitalsService _svc;
            private readonly PatientId _p;
            private long _cursor = -1;

            public Driver(DiseaseVitalsService svc, PatientId p)
            {
                _svc = svc; _p = p;
            }

            /// <summary>按序驱动到 <paramref name="target"/>(含);已过去则不重放。</summary>
            public VitalsDto DriveTo(long target)
            {
                while (_cursor < target)
                {
                    _cursor++;
                    _svc.OnTickEdge(_cursor);
                }
                return _svc.GetVitals(_p);
            }
        }

        /// <summary>
        /// **阈值真的在门控**:同一条伤情里,阈值低的体征先出现,阈值高于可达 position 的**永不出现**。
        /// </summary>
        /// <remarks>
        /// `INJ_DRUG_HARM` 的三条体征:`sign_pallor` 1/5(通道 1)· `sign_fatigue` 1/5(通道 64)·
        /// `sign_delirium` 3/5(通道 2)。该行 `a_peak 30 / scale 100` ⇒ **position 上界 = 0.30**
        /// ⇒ `sign_delirium`(0.60)**结构性不可达**。这条断言同时证明两件事:
        /// ① 出现阈值在起作用(不是「一律全出」);② 不可达阈值确实不出现(不是「一律全无」)。
        /// </remarks>
        [Test]
        public void test_thresholdsGateSigns_perSign()
        {
            DiseaseRegistryEntry entry = Row("INJ_DRUG_HARM");
            (DiseaseVitalsService svc, PatientId p) = Open(entry);

            VitalsDto v = new Driver(svc, p).DriveTo(180L);   // = τ_peak,position 达上界 0.30

            Assert.That(v.Position, Is.GreaterThan(0.2f), "夹具前提:position 须越过低阈值 0.20");
            Assert.That(v.SignChannelMask, Is.EqualTo(SignChannel.Complexion | SignChannel.Subjective),
                "只应有 面色(1) + 主观(64);谵妄(通道 2,阈值 0.60 > 上界 0.30)不得出现");
            Assert.That(v.SignCount, Is.EqualTo(2));
        }

        /// <summary>
        /// **对 position 单调**:同一 position 下掩码唯一;position 上升时掩码只增不减。
        /// </summary>
        /// <remarks>
        /// ⚠️ 掩码**不是**对 tick 单调的 —— 过了 τ_peak 后 `Progress` 回落,体征会**正确地**消失。
        /// 所以判据只能挂在 `position` 上;按 tick 断「只增不减」会把正确行为判成红。
        /// 本测同时是**投影真的在生效**的证据(`prev` 最终非零 + 至少发生一次扩张)。
        /// </remarks>
        [Test]
        public void test_maskIsMonotoneInPosition_notInTick()
        {
            DiseaseRegistryEntry entry = Row("INJ_DRUG_HARM");
            (DiseaseVitalsService svc, PatientId p) = Open(entry);

            var driver = new Driver(svc, p);
            float maxPosition = -1f;
            int maskAtMax = 0;
            int expansions = 0;

            for (long t = 0; t <= 600; t += 5)
            {
                VitalsDto v = driver.DriveTo(t);
                if (v.Position > maxPosition)
                {
                    // position 上升 ⇒ 掩码不得回缩
                    Assert.That(v.SignChannelMask & maskAtMax, Is.EqualTo(maskAtMax),
                        $"position {maxPosition} → {v.Position} 时掩码回缩" +
                        $"({maskAtMax} → {v.SignChannelMask})—— 阈值门须对 position 单调");
                    if (v.SignChannelMask != maskAtMax) expansions++;
                    maxPosition = v.Position;
                    maskAtMax = v.SignChannelMask;
                }
                else
                {
                    // position 持平 / 回落 ⇒ 掩码只能是当时的子集
                    Assert.That(v.SignChannelMask & ~maskAtMax, Is.Zero,
                        $"position 未上升却出现新通道({v.SignChannelMask} ⊄ {maskAtMax})");
                }
            }

            Assert.That(maskAtMax, Is.Not.Zero, "驱动全程零掩码 ⇒ 投影未生效(接线假绿)");
            Assert.That(expansions, Is.GreaterThan(0), "全程零扩张 ⇒ 阈值门没在起作用");
        }

        /// <summary>
        /// **端到端**:真实 `DiseaseVitalsService` 的掩码 ⇒ 11 的禁忌判据给出**非哨兵档**。
        /// </summary>
        /// <remarks>
        /// <para>这是 `TD-sim-04` 的**关账判据**:本债的原文要求「用**真实** `DiseaseVitalsService`
        /// (非 `StubVitals`)跑出非零档」。此前 11 的判据只能靠注入掩码驱动 —— 生产上掩码恒 0
        /// ⇒ 判据恒返回哨兵 ⇒ 整条药害链在生产上不可能触发。</para>
        /// <para>判据取**哨兵的补集**而不是写死某个档:本行可达的体征是 面色(1)+主观(64),
        /// 三条规则里只有「面色 ⇒ 档 0」成立 ⇒ 期望恰为 0(档 0 = 轻,是**合法档**,
        /// 不能与 `NoContraindication = -1` 混为一谈 —— 这正是当初选负数当哨兵的理由)。</para>
        /// </remarks>
        [Test]
        public void test_realVitals_produceANonSentinelBand()
        {
            DiseaseRegistryEntry entry = Row("INJ_DRUG_HARM");
            (DiseaseVitalsService svc, PatientId p) = Open(entry);
            VitalsDto v = new Driver(svc, p).DriveTo(180L);

            // 与 `prescription_actions.json` 的 salicylic_acid 同形的三档规则
            var rules = new[]
            {
                new ContraindicationRule(SignChannel.Complexion, 0),
                new ContraindicationRule(SignChannel.Complexion | SignChannel.Voice, 1),
                new ContraindicationRule(
                    SignChannel.Complexion | SignChannel.Breathing | SignChannel.Subjective, 2),
            };

            int band = ContraindicationEvaluator.Evaluate(rules, v.SignChannelMask);

            Assert.That(band, Is.Not.EqualTo(ContraindicationEvaluator.NoContraindication),
                "真实体征掩码下判据仍返回哨兵 ⇒ F2 投影没接上(本债未消解)");
            Assert.That(band, Is.EqualTo(0), "可达的只有「面色」一条规则 ⇒ 轻档 0");
        }

        /// <summary>**单体征行**:只投影它自己的那一个通道(不得把别行的通道带进来)。</summary>
        [Test]
        public void test_singleSignRow_projectsOnlyThatChannel()
        {
            DiseaseRegistryEntry entry = Row("INJ_BLUNT");   // 仅 sign_moan(通道 2,阈值 1/20)
            (DiseaseVitalsService svc, PatientId p) = Open(entry);

            VitalsDto v = new Driver(svc, p).DriveTo(120L);   // = τ_peak,position 达上界 0.20
            Assert.That(v.SignChannelMask, Is.EqualTo(SignChannel.Voice), "只有 sign_moan ⇒ 通道 2");
            Assert.That(v.SignCount, Is.EqualTo(1));
        }
    }
}
