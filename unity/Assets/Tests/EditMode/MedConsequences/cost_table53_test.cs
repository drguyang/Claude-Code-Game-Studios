// T1 · S4 —— `CostTable53`(53 代价表的 Sim 侧只读视图)的纵深防御夹具。
//
// 权威:ADR-035 §Decision ③(L3 = 运行期纵深防御,「复述同一拒收」)/ ⑥(过量支挂起)/ ②(单射)
//
// 与 `cost_table_binder_test` 的分工:那条测**构建期主校验**(字符串域),本条测**运行期**
// (整数域、可被绕过 Bake 直构)。两侧判据须一致 —— 本文件不放松任何一条。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Consequences;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.MedConsequences
{
    /// <summary>代价表运行期视图 —— 九条拒收 + 三条反例。</summary>
    [TestFixture]
    public sealed class CostTable53Test
    {
        // ══════════════════════ 夹具 ══════════════════════

        private static DiseaseRegistryEntry Entry(int id, string key, DiseaseKind kind, int lethality)
            => new DiseaseRegistryEntry { DiseaseId = id, DiseaseKey = key, Kind = kind, Lethality = lethality };

        /// <summary>一件**非致死伤情** + 一件致死伤情 + 一个非致死病种(诱饵)。</summary>
        private static List<DiseaseRegistryEntry> Registry() => new List<DiseaseRegistryEntry>
        {
            Entry(1, "INJ_HEMORRHAGE", DiseaseKind.Injury, 0),
            Entry(2, "INJ_STAB", DiseaseKind.Injury, 1),
            Entry(3, "DIS_MEASLES", DiseaseKind.Disease, 0),
        };

        private static int Kind(CostHarmKind k) => (int)k;

        private static CostTable53.Row LegalRow(int band = 0, int severity = 0, int injury = 1)
            => new CostTable53.Row(Kind(CostHarmKind.Contraindication), band, injury, severity);

        private static ArgumentException AssertRejected(IReadOnlyList<CostTable53.Row> rows, int? max = null)
            => Assert.Throws<ArgumentException>(
                () => CostTable53.CreateTable(Registry(), rows, max));

        private static void AssertMessageHas(ArgumentException ex, string needle)
            => Assert.That(ex.Message, Does.Contain(needle), "报文须点名该判据:" + ex.Message);

        // ══════════════════════ 一、正向与反例 ══════════════════════

        /// <summary>空表 = 合法惰性形态(连注册表都可以没有)。</summary>
        [Test]
        public void test_emptyTable_isEmpty_andLookupMisses()
        {
            CostTable53.Table t = CostTable53.CreateTable(null, null, null);

            Assert.That(t.IsEmpty, Is.True);
            Assert.That(t.RowCount, Is.Zero);
            Assert.That(t.TryLookup(Kind(CostHarmKind.Contraindication), 0, out _, out _), Is.False,
                "空表查询须 miss(惰性),不得抛");
        }

        /// <summary>一条合法行能查到,值原样返回。</summary>
        [Test]
        public void test_legalRow_bindsAndLooksUp()
        {
            CostTable53.Table t = CostTable53.CreateTable(
                Registry(), new[] { LegalRow(band: 2, severity: 1, injury: 1) }, maxSeverityBand: 3);

            Assert.That(t.TryLookup(Kind(CostHarmKind.Contraindication), 2, out int injury, out int sev), Is.True);
            Assert.That(injury, Is.EqualTo(1));
            Assert.That(sev, Is.EqualTo(1));
            Assert.That(t.TryLookup(Kind(CostHarmKind.Contraindication), 3, out _, out _), Is.False,
                "未声明的 band 须 miss");
        }

        /// <summary>反例:同一伤情出现在两个不同档 ⇒ 合法(单射判据不得误伤)。</summary>
        [Test]
        public void test_sameInjuryOnDifferentBands_isLegal()
        {
            CostTable53.Table t = CostTable53.CreateTable(
                Registry(), new[] { LegalRow(band: 0), LegalRow(band: 1) }, null);
            Assert.That(t.RowCount, Is.EqualTo(2));
        }

        /// <summary>反例:构造期**拷贝**行集 —— 外部 list 事后改写不影响已构造的表。</summary>
        [Test]
        public void test_ctorCopiesRows_externalMutationDoesNotAffect()
        {
            var rows = new List<CostTable53.Row> { LegalRow(band: 0, injury: 1) };
            CostTable53.Table t = CostTable53.CreateTable(Registry(), rows, null);

            rows.Clear();
            rows.Add(LegalRow(band: 9, injury: 3)); // 事后塞一条非法/别处的行

            Assert.That(t.RowCount, Is.EqualTo(1), "表须持有自己的拷贝(DelayTable53 同口径)");
            Assert.That(t.TryLookup(Kind(CostHarmKind.Contraindication), 0, out int injury, out _), Is.True);
            Assert.That(injury, Is.EqualTo(1));
        }

        // ══════════════════════ 二、N-1 / 外键 / kind ══════════════════════

        /// <summary>N-1:指向 `lethal = true` 的伤情 ⇒ 拒。</summary>
        [Test]
        public void test_n1_lethalInjury_isRejected()
        {
            AssertMessageHas(AssertRejected(new[] { LegalRow(injury: 2) }), "N-1");
        }

        /// <summary>外键:注册表里没有的 id ⇒ 拒。</summary>
        [Test]
        public void test_unknownInjuryId_isRejected()
        {
            AssertMessageHas(AssertRejected(new[] { LegalRow(injury: 99) }), "外键失效");
        }

        /// <summary>落点须是伤情:指向 `lethal = false` 的**病种** ⇒ 仍拒(防「只查 lethal 不查 kind」)。</summary>
        [Test]
        public void test_diseaseRowId_isRejected()
        {
            AssertMessageHas(AssertRejected(new[] { LegalRow(injury: 3) }), "≠ injury");
        }

        // ══════════════════════ 三、§⑥ / 单射 / 上界 / 空注册表 ══════════════════════

        /// <summary>§⑥:过量支挂起 ⇒ 表里出现 `Overdose` 行一律拒。</summary>
        [Test]
        public void test_overdoseKind_isRejected()
        {
            var row = new CostTable53.Row(Kind(CostHarmKind.Overdose), 0, 1, 0);
            ArgumentException ex = AssertRejected(new[] { row });
            AssertMessageHas(ex, "Overdose");
            AssertMessageHas(ex, "TD-sim-02");
        }

        /// <summary>闭集:未知 `harm_kind` ⇒ 拒。</summary>
        [Test]
        public void test_unknownHarmKind_isRejected()
        {
            var row = new CostTable53.Row(42, 0, 1, 0);
            AssertMessageHas(AssertRejected(new[] { row }), "∉ 闭集");
        }

        /// <summary>单射:`(harm_kind, band)` 重复 ⇒ 拒。</summary>
        [Test]
        public void test_duplicateKindBand_isRejected()
        {
            AssertMessageHas(AssertRejected(new[] { LegalRow(band: 1), LegalRow(band: 1) }), "单射");
        }

        /// <summary>负档序数 ⇒ 拒。</summary>
        [Test]
        public void test_negativeBand_isRejected()
        {
            AssertMessageHas(AssertRejected(new[] { LegalRow(band: -1) }), "< 0");
        }

        /// <summary>`severity_band` 超上界 ⇒ 拒(`max` 声明时)。</summary>
        [Test]
        public void test_severityAboveMax_isRejected()
        {
            AssertMessageHas(AssertRejected(new[] { LegalRow(severity: 4) }, max: 3), "max_severity_band");
        }

        /// <summary>有行却没有注册表 ⇒ `ArgumentNullException`(N-1 不可执行,不得静默放行)。</summary>
        [Test]
        public void test_rowsWithoutRegistry_isRejected()
        {
            Assert.Throws<ArgumentNullException>(
                () => CostTable53.CreateTable(null, new[] { LegalRow() }, null));
        }
    }
}
