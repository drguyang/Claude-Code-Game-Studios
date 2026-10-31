// story-005 · AC-17-09 / 09b —— 呈现层 DTO 的**反幻想**与「cue 无品质维」。
//
// 权威:
//   design/gdd/foraging.md AC-17-09(BLOCKING)「递归 type-surface 反射扫描,无『原始品级/本可采到』
//     字段(AC-37-15 同型;**非按名 grep**)」· AC-17-09b(BLOCKING)「AudioCueDto 载荷不含品质维」
//   ADR-018 §六 无提示音铁律 · ADR-013 §9 C3(42 只渲染,永不持状态)
//
// ⚠️ 本件**先证守卫会红**(负控用本文件内的本地 DTO —— 不污染生产类型),
//    **再证生产 DTO 是绿的**。只做后者 = 对守卫的判别力零覆盖(F4)。

using DaYiJingCheng.EditorTools.Gates;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.EditMode.Foraging
{
    /// <summary>AC-17-09 / 09b:17 呈现面不得泄漏「本可采到」与余量数字。</summary>
    [TestFixture]
    public sealed class ForagingDtoGuardTest
    {
        // ══════════ 判别力:守卫必须抓得到 ══════════

        /// <summary>负控用的违规 DTO —— 只在测试内,不进生产树。</summary>
        private readonly struct BadAffordance
        {
            public readonly int RawQuality;        // 原始品级:不该呈现
            public readonly bool PotentialYield;   // 本可采到:不该呈现
        }

        [Test]
        public void test_ac1709_negativeControl_guardCatchesForbiddenFields()
        {
            var errs = AntiFantasyDtoGate.Scan(typeof(BadAffordance));
            Assert.That(errs, Is.Not.Empty,
                "守卫对 `RawQuality` / `PotentialYield` 失明 ⇒ 生产 DTO 的「绿」毫无意义");

            Assert.That(string.Join("\n", errs), Does.Contain("RawQuality"));
            Assert.That(string.Join("\n", errs), Does.Contain("PotentialYield"));
        }

        [Test]
        public void test_ac1709_nullRoot_throws_notEmptySet()
        {
            // 返回空集会被读成「干净」;null 入口必须抛
            Assert.Throws<System.ArgumentNullException>(() => AntiFantasyDtoGate.Scan(null));
        }

        [Test]
        public void test_ac1709_tokenMatching_isNormalized()
        {
            // 下划线 / 大小写归一化:`raw_quality` 与 `RawQuality` 必须同判
            Assert.That(AntiFantasyDtoGate.HasForbiddenToken("raw_quality"), Is.True);
            Assert.That(AntiFantasyDtoGate.HasForbiddenToken("RAWQUALITY"), Is.True);
            Assert.That(AntiFantasyDtoGate.HasForbiddenToken("NodeKindOrdinal"), Is.False);
        }

        // ══════════ 生产 DTO:必须是绿的 ══════════

        [Test]
        public void test_ac1709_gatherAffordanceDto_isAntiFantasyClean()
        {
            var errs = AntiFantasyDtoGate.Scan(typeof(GatherAffordanceDto));
            Assert.That(errs, Is.Empty,
                "17 的采集提示 DTO 出现反幻想字段:\n" + string.Join("\n", errs));
        }

        [Test]
        public void test_ac1709_gatherAffordanceDto_carriesNoRemainingNumber()
        {
            // Implementation Note 4 逐字:「无余量数字」。用**类型面**再确认一遍:
            // 该结构体全部实例字段只允许 int / Int3 / bool。
            var fields = typeof(GatherAffordanceDto).GetFields(
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            Assert.That(fields.Length, Is.EqualTo(4),
                "字段数变了 ⇒ 契约变更,须先核 AC-17-09(无余量数字)与 42 的消费面");

            foreach (var f in fields)
                Assert.That(
                    f.FieldType == typeof(int) || f.FieldType == typeof(bool) || f.FieldType == typeof(Int3),
                    Is.True, $"字段 {f.Name} 类型 = {f.FieldType.Name};采集提示只允许 int / Int3 / bool");
        }

        [Test]
        public void test_ac1709b_audioCueDto_hasNoQualityDimension()
        {
            // ADR-018 §六:成功音内部不分档 —— cue 载荷不得携带品质维
            Assert.That(AntiFantasyDtoGate.Scan(typeof(AudioCueDto)), Is.Empty,
                "AudioCueDto 出现品质 / 反幻想字段 —— 违 ADR-018 §六 无提示音铁律");

            foreach (var f in typeof(AudioCueDto).GetFields(
                         System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                Assert.That(AntiFantasyDtoGate.HasForbiddenToken(f.Name), Is.False,
                    $"AudioCueDto.{f.Name} 携带品质维");
        }
    }
}
