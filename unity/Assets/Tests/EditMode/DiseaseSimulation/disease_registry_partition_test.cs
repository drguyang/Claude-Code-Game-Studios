// 病种注册表**分区**判据(2026-11-09 · T2)。
//
// 权威:`design/gdd/reviews/technique-unlock-map-2026-11-09.md` §7 T2
//      · GDD 规则九(`disease-simulation.md:356-358`:伤情与病种同一张 schema)
//      · 承 T1(源文件拆 `diseases` / `injuries` 两数组,commit 027b065d)
//
// 本文件要证明的四件事:
//   ① **两侧不丢不重** —— `Rows` 恰 = `Diseases ∪ Injuries`,且两侧无交;
//   ② **两侧只收自己那侧** —— 病行进 `injuries` / 伤行进 `diseases` 均构建期拒收
//      (放错侧是静默错位;反向是 2026-11-09 T4 收紧的,原文只写单向);
//   ③ **缺 `kind` 仍被拒**(T1 前既有行为,此处置为回归锚);
//   ④ **两个新字段真的进字节** —— `min_skill_level` / `teach_point` 往返后逐值相等;
//      且越界 / 空串各自被拒(否则「字段存在」与「字段被校验」无法区分)。
//
// ⚠️ 本文件**只读**仓库文件(不写盘)⇒ 不触发 `FsWriteBoundaryGate`。

using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using DaYiJingCheng.EditorTools.Bake;
using DaYiJingCheng.Gameplay.Presentation;
using DaYiJingCheng.Sim;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Unit.Sim
{
    /// <summary>注册表分区:两侧不丢不重 · injuries 只收伤 · 新字段往返与值域。</summary>
    [TestFixture]
    public sealed class DiseaseRegistryPartitionTest
    {
        private static string RepoRoot([CallerFilePath] string thisFile = "")
            => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile) ?? ".",
                "..", "..", "..", "..", ".."));

        /// <summary>一行合法 JSON。`kind` 传 null ⇒ **不写该键**(测「缺 kind」)。</summary>
        private static string Row(int id, string key, string kind, string extra = null)
        {
            string kindLine = kind == null ? "" : $"      \"kind\": \"{kind}\",\n";
            return "    {\n" +
                   $"      \"id\": {id},\n" +
                   $"      \"key\": \"{key}\",\n" +
                   kindLine +
                   "      \"severity\": 3,\n" +
                   "      \"lethal\": false,\n" +
                   "      \"terminal_state\": false,\n" +
                   "      \"scale\": \"100\",\n" +
                   (extra == null ? "" : extra) +
                   "      \"natural_progress\": {\n" +
                   "        \"incubation\": 0,\n" +
                   "        \"a_peak\": \"50\",\n" +
                   "        \"tau_rise\": \"40\",\n" +
                   "        \"tau_peak\": 300,\n" +
                   "        \"tau_fall\": \"600\",\n" +
                   "        \"plateau\": false,\n" +
                   "        \"self_limit\": true,\n" +
                   "        \"boundary_mode\": \"monotone\"\n" +
                   "      }\n" +
                   "    }";
        }

        private static string Doc(string diseasesJson, string injuriesJson) => @"{
  ""schema_version"": 3,
  ""diseases"": [" + diseasesJson + @"],
  ""injuries"": [" + injuriesJson + @"]
}";

        // ══════════════════════════════════════════════════════════════════════
        // ① 真源:两侧不丢不重
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// <para><b>判别力</b>:若 `DiseaseRegistryDataSet` 的分桶把某一侧漏掉(例如只遍历
        /// `Rows` 的前 N 行、或把 `Poison` 当成伤),「并集 == Rows」当场红;
        /// 若把伤行也塞进 `Diseases`(不过滤),「两侧无交」当场红。</para>
        /// </summary>
        [Test]
        public void test_realSource_partitionIsCompleteAndDisjoint()
        {
            DiseaseRegistryBaker.BakeOutput outp = DiseaseRegistryBaker.BakeFromRepo(RepoRoot());
            DiseaseRegistryDataSet ds = DiseaseRegistryBaker.Materialize(outp);

            Assert.That(ds.Injuries.Count, Is.GreaterThan(0),
                "前置:真表伤侧须非空(否则下面的「只收伤」断言在空集上恒真)");
            Assert.That(ds.Diseases.Count, Is.GreaterThan(0),
                "前置:真表病侧须非空");

            // 两侧无交 + 只收对应 kind
            Assert.That(ds.Injuries.All(r => r.Kind == DiseaseKind.Injury), Is.True,
                "`Injuries` 里出现了非 injury 行 —— 分桶写反或漏过滤");
            Assert.That(ds.Diseases.Any(r => r.Kind == DiseaseKind.Injury), Is.False,
                "`Diseases` 里出现了 injury 行 —— 病人池会抽到伤情");

            // 不丢不重
            Assert.That(ds.Diseases.Count + ds.Injuries.Count, Is.EqualTo(ds.Rows.Count),
                "两侧之和 ≠ Rows ⇒ 分桶丢了行或重复计入");
            int[] union = ds.Diseases.Concat(ds.Injuries).Select(r => r.DiseaseId).OrderBy(x => x).ToArray();
            int[] all = ds.Rows.Select(r => r.DiseaseId).OrderBy(x => x).ToArray();
            Assert.That(union, Is.EqualTo(all), "两侧并集 ≠ Rows(按 id 比较)");
        }

        // ══════════════════════════════════════════════════════════════════════
        // ② injuries 只收伤
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// <para><b>判别力</b>:删掉 `BindRows` 里的分区一致性门 ⇒ 本条转绿失败(不再抛),
        /// 从而暴露「放错侧静默通过」。</para>
        /// </summary>
        [Test]
        public void test_injuriesArray_withDiseaseKind_isRejected()
        {
            string bad = Doc(Row(7, "DIS_A", "disease"), Row(31, "DIS_WRONG_SIDE", "disease"));

            var ex = Assert.Throws<BakeValidationException>(
                () => DiseaseRegistryBaker.BakeFromJson(bad));
            Assert.That(ex.Message, Does.Contain("injuries"),
                "报文须点名是 `injuries` 数组放错侧(否则读者会去查 diseases):" + ex.Message);
        }

        /// <summary>
        /// 反向(**T4 起收紧**):把伤行放进 `diseases` ⇒ 同样构建期拒收。
        /// <para><b>判别力</b>:删掉 `BindRows` 里的 `else if (… kind == Injury)` 分支 ⇒
        /// 本条转绿(不再抛),暴露「伤行留在病侧静默通过」—— 那是 T2 当时**刻意留的**
        /// 口子(T2 的判据只写单向),T4 的投放门以 `Rows` 为候选面,故必须双向。</para>
        /// </summary>
        [Test]
        public void test_diseasesArray_withInjuryKind_isRejected()
        {
            string bad = Doc(Row(31, "INJ_WRONG_SIDE", "injury"), Row(7, "DIS_A", "disease"));

            var ex = Assert.Throws<BakeValidationException>(
                () => DiseaseRegistryBaker.BakeFromJson(bad));
            Assert.That(ex.Message, Does.Contain("diseases"),
                "报文须点名是 `diseases` 数组放错侧:" + ex.Message);
        }

        /// <summary>缺 <c>kind</c> ⇒ 仍被拒(T1 前的既有行为,这里是回归锚)。</summary>
        [Test]
        public void test_missingKind_isRejectedOnEitherSide()
        {
            Assert.Throws<BakeValidationException>(
                () => DiseaseRegistryBaker.BakeFromJson(Doc(Row(7, "DIS_A", null), "")),
                "diseases 侧缺 kind 须拒");
            Assert.Throws<BakeValidationException>(
                () => DiseaseRegistryBaker.BakeFromJson(Doc(Row(7, "DIS_A", "disease"), Row(31, "INJ_A", null))),
                "injuries 侧缺 kind 须拒");
        }

        // ══════════════════════════════════════════════════════════════════════
        // ③ 新字段:往返 + 值域
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// <para><b>判别力</b>:若写方不写 / 读方不读这两个字段,往返比对(见
        /// `DiseaseRegistryBaker.CompareRow`)当场红 —— 本条的显式断言是第二道,
        /// 保证即便那边被删也能看见。</para>
        /// </summary>
        [Test]
        public void test_minSkillLevelAndTeachPoint_surviveCookedRoundTrip()
        {
            string extra = "      \"min_skill_level\": 12,\n" +
                           "      \"teach_point\": \"死人的是脱水,不是腹泻\",\n";
            string json = Doc(Row(7, "DIS_A", "disease", extra), "");

            var outp = DiseaseRegistryBaker.BakeFromJson(json);
            DiseaseRegistryDataSet ds = DiseaseRegistryBaker.Materialize(outp);

            Assert.That(ds.Rows[0].MinSkillLevel, Is.EqualTo(12));
            Assert.That(ds.Rows[0].TeachPoint, Is.EqualTo("死人的是脱水,不是腹泻"));
        }

        /// <summary>未写 <c>teach_point</c> ⇒ 往返后仍是 <c>null</c>(「未撰写」不被伪装成空串)。</summary>
        [Test]
        public void test_absentTeachPoint_staysNull()
        {
            var outp = DiseaseRegistryBaker.BakeFromJson(Doc(Row(7, "DIS_A", "disease"), ""));
            DiseaseRegistryDataSet ds = DiseaseRegistryBaker.Materialize(outp);
            Assert.That(ds.Rows[0].TeachPoint, Is.Null);
            Assert.That(ds.Rows[0].MinSkillLevel, Is.EqualTo(0), "缺省 = 0(开局可投放)");
        }

        /// <summary>越界 <c>min_skill_level</c> ⇒ 拒(否则该病**永远进不了池**,且静默)。</summary>
        [Test]
        public void test_minSkillLevel_outOfRange_isRejected()
        {
            foreach (int bad in new[] { -1, 61 })
            {
                string json = Doc(Row(7, "DIS_A", "disease", $"      \"min_skill_level\": {bad},\n"), "");
                var ex = Assert.Throws<BakeValidationException>(
                    () => DiseaseRegistryBaker.BakeFromJson(json), $"min_skill_level={bad} 须拒");
                Assert.That(ex.Message, Does.Contain("min_skill_level"), ex.Message);
            }
        }

        /// <summary>空串 <c>teach_point</c> ⇒ 拒(空串与漏写不可区分 ⇒ 「有没有科普点」永远判不出)。</summary>
        [Test]
        public void test_emptyTeachPoint_isRejected()
        {
            string json = Doc(Row(7, "DIS_A", "disease", "      \"teach_point\": \"  \",\n"), "");
            var ex = Assert.Throws<BakeValidationException>(() => DiseaseRegistryBaker.BakeFromJson(json));
            Assert.That(ex.Message, Does.Contain("teach_point"), ex.Message);
        }
    }
}
