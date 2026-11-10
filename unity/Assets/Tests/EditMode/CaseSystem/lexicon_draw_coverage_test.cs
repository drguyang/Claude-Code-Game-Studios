// 病名册词表 ↔ 开局投放面 的**覆盖门**(2026-11-10 · T22 批三 · 承 `TD-casebook-03`)
//
// 权威:
//   `design/gdd/casebook.md` §5.2 / §12 —— 病名册 = 词表「已见过」过滤视图,反查键 = `disease_ids`
//   `docs/tech-debt-register.md` `TD-casebook-03` —— 解除判据 = 真包 `病名册环 pointable ≥ 2`
//   开局投哪两例 = `TechniqueUnlockBootstrap.StartingTechniques` + `PatientDrawEligibility.Decide`
//     + 注册表行序(`DiseaseRegistryDataSet.Rows`,生产 `PatientAppearedDriver._rotation` 的同源)
//
// ── 立门缘由(2026-11-08 真包实测)──────────────────────────────────────────
// 医师查过**两名**病人(`病名册环 seen=2`),病名册却只有**一条**词(`pointable=1`):
// 词表当时只覆盖病种 `10`(伤寒)与 `2`(刺伤),而开局前两例拿到的是 `2` 与 `3`
// ⇒ 第二例无词可指认 ⇒「换词改判 / 旧笔同屏」在生产里**不可达**,而**构建、烘焙、全部夹具都是绿的**。
// 根因不是某一侧写错,是**没有任何判据把两件事连起来** —— 词表覆盖与开局投放各测各的。
// 本门就是那根线:一边读词表产物,一边按生产同款判据算出「开局会投哪两例」,要求前者覆盖后者。
//
// ⚠️ 本门**不**替代 `LexiconRegistryCrossRefGate`(悬空外键)也不替代 `cooked_drift`(源 ↔ 产物):
//    那两条管「词表自身一致性」,本门管「词表与**投放面**的关系」。三者互补,缺一不可。
//
// ── 读哪些仓库文件(测试标准 §只读外部夹具的具名声明义务)──────────────────
// 读 `unity/Assets/DataCooked/` 下三件产物:`disease_registry.cooked.bytes` ·
//   `disease_action_axis.cooked.bytes` · `case_judgment_lexicon.cooked.bytes`(经 `DataCookedDisk`
//   + 生产 codec,与运行期同一份字节)。
// 为什么必须读真产物:本门量的就是**这一份作者态内容**的覆盖面 —— 内联一张假表,
//   夹具只能证明自己写的谓词(假绿形态 F1),而这条债恰恰是「夹具全绿、真包不绿」。
// ⚠️ 本文件零写盘。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.Presentation;
using DaYiJingCheng.Gameplay.Presentation.Casebook;
using DaYiJingCheng.Sim;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.CaseSystem
{
    /// <summary>
    /// 开局投放面必须落在病名册词表的覆盖范围内(`TD-casebook-03` 的机械判据)。
    /// </summary>
    [TestFixture]
    public sealed class lexicon_draw_coverage_test
    {
        /// <summary>开局前几例 —— 取 2 与生产接线期上限哨(`PatientWiringPositions.Count`)一致。</summary>
        private const int FirstNPatients = 2;

        // ── 真产物装载(与生产同一 codec)────────────────────────────────────
        private static DiseaseRegistryDataSet Registry() => DataCookedDisk.LoadDiseaseRegistry();

        private static TechniqueDrawIndex DrawIndex()
        {
            DiseaseActionAxisDataSet axis = DiseaseActionAxisCookedCodec.Read(
                DataCookedDisk.ReadAll(AddressablesDataProvider.DiseaseActionAxisAddress));
            return new TechniqueDrawIndex(axis.TreatableBy);
        }

        private static JudgmentLexiconDataSet Lexicon() => DataCookedDisk.LoadJudgmentLexicon();

        /// <summary>
        /// 按**生产同款**判据算开局前 <paramref name="n"/> 例的病种 id。
        /// <para>⚠️ 行序取 `registry.Rows`(生产 <c>PatientAppearedDriver</c> 的 `_rotation` 同源);
        /// 资格判据取纯函数 <see cref="PatientDrawEligibility.Decide"/>(生产
        /// <c>PatientAdmissionPolicy</c> 的下游)。钩子改判(`hookAllowed`)在开局**零配额**下不生效,
        /// 故此处不传。</para>
        /// </summary>
        private static List<int> FirstDrawableDiseaseIds(DiseaseRegistryDataSet registry,
                                                          TechniqueDrawIndex index, int n)
        {
            var unlocked = new HashSet<int>(TechniqueUnlockBootstrap.StartingTechniques);
            var ids = new List<int>();
            for (int i = 0; i < registry.Rows.Count && ids.Count < n; i++)
            {
                DiseaseRegistryEntry row = registry.Rows[i];
                PatientDrawDecision decision = PatientDrawEligibility.Decide(
                    row, index, playerPrescriptionLevel: 0,
                    techniqueId => unlocked.Contains(techniqueId), playerBound: true);
                if (decision.Admitted) ids.Add(row.DiseaseId);
            }
            return ids;
        }

        // ══════════════════════════════════════════════════════════════════
        //  C1 + C2:开局前两例都要有词,且至少两条
        // ══════════════════════════════════════════════════════════════════
        [Test]
        public void test_firstTwoDrawablePatients_eachHaveALexiconWord()
        {
            DiseaseRegistryDataSet registry = Registry();
            TechniqueDrawIndex index = DrawIndex();
            var book = new LexiconBookSource(Lexicon().Rows);

            List<int> drawn = FirstDrawableDiseaseIds(registry, index, FirstNPatients);
            TestContext.WriteLine("[TD-casebook-03] 开局前 " + FirstNPatients + " 例病种 = [" +
                                  string.Join(", ", drawn) + "]");

            // 守卫 G1/G2(判据不可执行 ≠ 通过)——
            Assert.That(registry.Rows.Count, Is.GreaterThan(0), "注册表 0 行 ⇒ 本门无可判对象");
            Assert.That(book.TotalRows, Is.GreaterThan(0), "词表 0 行 ⇒ 反查恒空,判据不可执行");
            Assert.That(drawn.Count, Is.EqualTo(FirstNPatients),
                "开局可投放病种不足 " + FirstNPatients + " 例 —— 与 `draw_surface_audit_test` " +
                "的开局面快照一并复核");

            // C2:至少两条词(逐条覆盖 ⇒ 至少两条;此处先钉「够两条」这个语义)
            int[] pointable = book.PointableLexiconIds(drawn.ToArray());
            Assert.That(pointable.Length, Is.GreaterThanOrEqualTo(2),
                "开局前两例凑不出两条词 ⇒ 病名册在生产里只有一条可指认(" +
                "`TD-casebook-03`:真包 `pointable` 必须 ≥ 2)");

            // C1(钉死当下值):改词表 / 改投放面都必须回来复核本行。
            // 4 = 刺伤(病种 2)· 5 = 割伤(病种 3) —— 与外键门、`judgment_lexicon_bake_test` 同源。
            Assert.That(pointable, Is.EqualTo(new[] { 4, 5 }),
                "开局前两例的可指认词条变了 —— 词表或投放面改过?复核后同步本断言与 " +
                "`production/epics/casebook/story-005-*` 的 `TD-casebook-03` 记录");
        }

        // ══════════════════════════════════════════════════════════════════
        //  负控:C2 的判据本身有判别力(空集 ⇒ 0 条;只喂一例 ⇒ 1 条)
        // ══════════════════════════════════════════════════════════════════
        [Test]
        public void test_pointableCount_reflectsCoverage_notAlwaysTwo()
        {
            var book = new LexiconBookSource(Lexicon().Rows);

            // 空集 === 未查体(病名册空)——
            Assert.That(book.PointableLexiconIds(Array.Empty<int>()), Is.Empty,
                "未见过任何病 ⇒ 病名册必须为空(反幻想二)");

            // 只见过一例(病种 2)⇒ 只能指认一条 —— 证明上一条的「2」不是常数
            int[] one = book.PointableLexiconIds(new[] { 2 });
            Assert.That(one, Is.EqualTo(new[] { 4 }),
                "只见病种 2 ⇒ 只该有「刺伤」(lexicon 4)");
        }
    }
}
