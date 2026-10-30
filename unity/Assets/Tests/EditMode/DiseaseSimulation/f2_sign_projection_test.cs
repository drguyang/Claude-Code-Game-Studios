// ADR-035 T2 收口的最后一段 —— 9 的 **F2 体征投影**接线(`TD-sim-04` / GDD `D-9-R`)。
//
// 权威:`disease-simulation.md` F2(`Signⱼ = Project(Progress, 病种)ⱼ`)+ `:1561` 的 `D-9-R` 订正注
//       · `RegistrySchema` R1-37(`appear_at ∈ (0,1]` **且** `< A_peak/SCALE`)
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
        /// **阈值真的在门控**:同一条伤情里,阈值低的两条先出现,**阈值最高的一条后出现**。
        /// </summary>
        /// <remarks>
        /// <para>`INJ_DRUG_HARM` 的三条体征(2026-10-30 数值轮按行相位派生后):
        /// `sign_pallor` 3/40 = 0.075(通道 1)· `sign_fatigue` 3/40 = 0.075(通道 64)·
        /// `sign_delirium` 51/200 = 0.255(通道 2)。该行 `a_peak 30 / scale 100` ⇒ **峰值 position = 0.30**
        /// ⇒ 三条**全部可达**,但分两批出现。</para>
        /// <para>这条断言同时证明三件事:① 阈值在起作用(不是「一律全出」);
        /// ② 三条**都不是**死词条(2026-10-30 加 R1-37 可达性门后,死词条已不可能入库);
        /// ③ 出现是**分批**的 —— 低阈值的先出、高阈值的后出,不是「过某一点全出」。</para>
        /// <para>⚠️ 阈值在此写**字面量**(0.075 / 0.255)而**不**从 `entry.Signs[j].AppearAt` 读 ——
        /// 从被测对象派生期望值会让变异两边一起动,零红逃逸(自指判据)。</para>
        /// </remarks>
        [Test]
        public void test_thresholdsGateSigns_perSign()
        {
            DiseaseRegistryEntry entry = Row("INJ_DRUG_HARM");
            (DiseaseVitalsService svc, PatientId p) = Open(entry);

            var driver = new Driver(svc, p);
            int maskAtLow = -1;
            int maskAtPeak = 0;
            float posAtLow = -1f;
            float peakPos = -1f;

            for (long t = 0; t <= 180L; t++)   // τ_peak = 180,走到峰值即止
            {
                VitalsDto v = driver.DriveTo(t);

                // 取「已明确越过最低门槛」的第一帧:0.09 留出 0.075 的定点舍入余量,
                // 且远低于谵妄的 0.255 —— 这一帧应当只有两条低阈值体征在场。
                if (posAtLow < 0f && v.Position >= 0.09f)
                {
                    posAtLow = v.Position;
                    maskAtLow = v.SignChannelMask;
                }

                if (v.Position > peakPos)
                {
                    peakPos = v.Position;
                    maskAtPeak = v.SignChannelMask;
                }
            }

            Assert.That(posAtLow, Is.GreaterThan(0f), "夹具前提:波形须越过最低阈值 0.075");
            Assert.That(maskAtLow, Is.EqualTo(SignChannel.Complexion | SignChannel.Subjective),
                $"position {posAtLow} 时只应有 面色(1) + 主观(64);" +
                "谵妄(通道 2,阈值 0.255)不得提前出现");

            Assert.That(peakPos, Is.GreaterThan(0.255f), "夹具前提:峰值须越过谵妄阈值 0.255");
            Assert.That(maskAtPeak,
                Is.EqualTo(SignChannel.Complexion | SignChannel.Voice | SignChannel.Subjective),
                $"达峰(position {peakPos})后三条体征应全出");
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
        /// **端到端**:真实 `DiseaseVitalsService` 的掩码 ⇒ 11 的禁忌判据给出**非哨兵档**,
        /// 且档位随病程**逐级爬升**。
        /// </summary>
        /// <remarks>
        /// <para>这是 `TD-sim-04` 的**关账判据**:本债的原文要求「用**真实** `DiseaseVitalsService`
        /// (非 `StubVitals`)跑出非零档」。此前 11 的判据只能靠注入掩码驱动 —— 生产上掩码恒 0
        /// ⇒ 判据恒返回哨兵 ⇒ 整条药害链在生产上不可能触发。</para>
        /// <para>🔁 **2026-10-30 数值轮改判**:上一版断言「峰值档 = 0」,理由是「本行可达的体征只有
        /// 面色(1)+主观(64)」。按行相位派生阈值后 **谵妄(通道 2)也可达** ⇒ 峰值掩码变成
        /// `1|2|64 = 67`,第二条规则(面色∧语声)随之命中 ⇒ 期望档由 0 升为 **1**。
        /// 这不是放宽判据,是**加强**:现在同时断言**两级**都在动 ——
        /// 早期(只有面色+主观)命中档 0、峰值(再加语声)爬升到档 1。</para>
        /// <para>第三档(需 面色∧呼吸∧主观)在本行**仍不可达**:`INJ_DRUG_HARM` 没有呼吸通道体征。
        /// 这条「够不着」是**数据事实**而非缺陷 —— 它保证了本测不会把「三档恒满」误判成通过。</para>
        /// </remarks>
        [Test]
        public void test_realVitals_produceANonSentinelBand()
        {
            DiseaseRegistryEntry entry = Row("INJ_DRUG_HARM");
            (DiseaseVitalsService svc, PatientId p) = Open(entry);

            // 与 `prescription_actions.json` 的 salicylic_acid 同形的三档规则
            var rules = new[]
            {
                new ContraindicationRule(SignChannel.Complexion, 0),
                new ContraindicationRule(SignChannel.Complexion | SignChannel.Voice, 1),
                new ContraindicationRule(
                    SignChannel.Complexion | SignChannel.Breathing | SignChannel.Subjective, 2),
            };

            var driver = new Driver(svc, p);
            int bandAtLow = -1;
            int bandAtPeak = -1;

            for (long t = 0; t <= 180L; t++)
            {
                VitalsDto v = driver.DriveTo(t);

                // 0.09 = 越过两条 3/40 门槛且留舍入余量(远低于谵妄的 0.255)
                if (bandAtLow < 0 && v.Position >= 0.09f)
                    bandAtLow = ContraindicationEvaluator.Evaluate(rules, v.SignChannelMask);

                // 0.27 = 明确越过谵妄 0.255、仍在峰值 0.30 以内
                if (v.Position >= 0.27f)
                    bandAtPeak = ContraindicationEvaluator.Evaluate(rules, v.SignChannelMask);
            }

            Assert.That(bandAtPeak, Is.Not.EqualTo(ContraindicationEvaluator.NoContraindication),
                "真实体征掩码下判据仍返回哨兵 ⇒ F2 投影没接上(本债未消解)");
            Assert.That(bandAtLow, Is.EqualTo(0),
                "早期只有 面色(1)+主观(64) ⇒ 只命中「面色」规则 ⇒ 轻档 0");
            Assert.That(bandAtPeak, Is.EqualTo(1),
                "达峰后 面色+语声 同时在场 ⇒ 第二条规则命中 ⇒ 档 1" +
                "(第三档需呼吸通道,本行无该体征 ⇒ 不得命中)");
        }

        /// <summary>**单体征行**:只投影它自己的那一个通道(不得把别行的通道带进来)。</summary>
        [Test]
        public void test_singleSignRow_projectsOnlyThatChannel()
        {
            DiseaseRegistryEntry entry = Row("INJ_BLUNT");   // 仅 sign_moan(通道 2,阈值 1/100)
            (DiseaseVitalsService svc, PatientId p) = Open(entry);

            VitalsDto v = new Driver(svc, p).DriveTo(120L);   // = τ_peak,position 达上界 0.20
            Assert.That(v.SignChannelMask, Is.EqualTo(SignChannel.Voice), "只有 sign_moan ⇒ 通道 2");
            Assert.That(v.SignCount, Is.EqualTo(1));
        }
    }
}
