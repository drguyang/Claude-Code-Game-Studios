// #2 的第三半 —— 「新游戏可创建会话:`IIdAuthority` 计数器**从 0 起**」
//
// 权威:
//   `smoke-tests` 关键路径 #2(A 组):「新游戏可创建会话:存档头写入、WorldSeed 落头、
//     `IIdAuthority` 计数器从 0 起」;
//   `ADR-006 Amendment B`(2026-09-14 用户裁定):`patient_id` 发号权威 = **机制 A**
//     「计数器 + 高水位可重构」,**计数器永不复位 0**、迁移后 `next = max(patient_id) + 1`;
//   `EventStream.GetNextPatientId()` = 该高水位读数(`unity/Assets/Sim/EventStream.cs`)。
//
// ── 本文件补的是哪一半 ──────────────────────────────────────────────────────
// 原判据三半:①存档头写入 ②`WorldSeed` 落头 ③计数器从 0 起。
// ①② 由 `PersistenceService/save_composer_wiring_test.cs` 承担(断言 `h.WorldSeed == 0xABCD`);
// **③ 此前全仓无夹具** —— 现有的 `GetNextPatientId` 夹具断的都是「重构后的高水位」(3 / 6 / 8),
// 没有一条断「**新会话首号 = 0**」。
//
// ⚠️ 为什么「首号 = 0」是要紧的:ADR-006 Amendment B 的高水位重构是
// `next = max(patient_id) + 1` ⇒ 若空流的读数不是 0 而是别的数,**读档与开新档会走两条
// 不同的发号起点**,而那正是 Amendment B 要消灭的形态。
//
// ── 真装配,零 Fake ─────────────────────────────────────────────────────────
// `CompositionRoot.Assemble` → 真 `EventStream` + 真 `IIdAuthority` + 真 `PatientSpawner`。

using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Tests.DiseaseSimulation;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Boot
{
    /// <summary>新会话的病人 id 发号起点(`IIdAuthority` 从 0 起)。</summary>
    [TestFixture]
    public sealed class NewSessionIdAuthorityTest
    {
        /// <summary>
        /// 全新装配(等价「开新档」)⇒ 首号 0;发过号之后逐 1 递增,且与流的高水位读数一致。
        /// </summary>
        [Test]
        public void test_newSession_firstPatientId_isZero_thenIncrements()
        {
            CompositionRootServices bag = CompositionRoot.Assemble(new[] { SyntheticDiseaseFixture.Create() });

            // ① 空流的高水位读数 = 0(不是 -1、不是 1)
            Assert.That(bag.Stream.GetNextPatientId().Value, Is.EqualTo(0),
                "新会话(空流)的病人 id 高水位须为 0 —— ADR-006 Amendment B 的 `next = max + 1` 在 max = ∅ 时");

            // ② 首号真的发 0
            PatientId p0 = bag.PatientSpawner.SpawnNext(
                diseaseId: SyntheticDiseaseFixture.Create().DiseaseId, tick: 0);
            Assert.That(p0.Value, Is.EqualTo(0), "新会话的第一个病人 id 须为 0");

            // ③ 逐 1 递增
            PatientId p1 = bag.PatientSpawner.SpawnNext(
                diseaseId: SyntheticDiseaseFixture.Create().DiseaseId, tick: 0);
            Assert.That(p1.Value, Is.EqualTo(1), "第二个病人 id 须为 1(不得跳号)");

            // ④ 发号后高水位跟着走(不是常量)
            Assert.That(bag.Stream.GetNextPatientId().Value, Is.EqualTo(2),
                "发过 0 / 1 之后,高水位须为 2 —— 否则下一次发号会撞号");
        }

        /// <summary>
        /// 反例(防「断言恒真」):`Clear()` 之后高水位回到 0 —— 证明 ④ 的 2 不是常量。
        /// </summary>
        /// <remarks>
        /// ⚠️ `EventStream.Clear()` 是**测试用**接口(`EventStream.cs` 的 doc 已注明)。
        /// 本条只用它证明「读数会动」,不主张生产路径会清流。
        /// </remarks>
        [Test]
        public void test_afterClear_highWater_returnsToZero()
        {
            CompositionRootServices bag = CompositionRoot.Assemble(new[] { SyntheticDiseaseFixture.Create() });
            int diseaseId = SyntheticDiseaseFixture.Create().DiseaseId;

            bag.PatientSpawner.SpawnNext(diseaseId: diseaseId, tick: 0);
            bag.PatientSpawner.SpawnNext(diseaseId: diseaseId, tick: 0);
            Assert.That(bag.Stream.GetNextPatientId().Value, Is.EqualTo(2), "前置:此时高水位应为 2");

            bag.Stream.Clear();

            Assert.That(bag.Stream.GetNextPatientId().Value, Is.EqualTo(0),
                "清流后高水位须回到 0 —— 若不回,则上面那条的 2 可能来自别处(断言恒真)");
        }
    }
}
