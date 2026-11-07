// sprint-08 S1.0 · 病名册词表烘焙管线测试(2026-10-12)
//
// 判据(sprint-08 §2 S1.0):
//   ① bake 双跑逐位一致(确定性)· ② 解码 ≥3 行 · ConfigVersion 非零 · roundtrip
//   ③ AC-37-35 词表侧:全行单链互异(双射候选)⇒ 硬失败;孤儿条目存在 ⇒ 通过
//   ④ lexicon_id = 0 拒(空栏哨兵)· 非递增拒 · 重复拒 · 空 text 拒 · 未知键拒
//
// ⚠️ 测试驱动**同一台生产机器**(JudgmentLexiconBaker.BakeFromRepo)—— 零 Fake 数据。

using System;
using System.IO;
using NUnit.Framework;
using DaYiJingCheng.EditorTools.Bake;
using DaYiJingCheng.Gameplay.Presentation.Casebook;
using System.Runtime.CompilerServices;

namespace DaYiJingCheng.Tests.CaseSystem
{
    [TestFixture]
    public class judgment_lexicon_bake_test
    {
        private static string RepoRoot => ComputeRepoRoot();

        private static string ComputeRepoRoot([CallerFilePath] string thisFile = "")
            => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile) ?? ".",
                "..", "..", "..", "..", ".."));

        [Test]
        public void test_bake_fromRepo_doubleRun_bitwiseIdentical()
        {
            JudgmentLexiconBaker.BakeOutput a = JudgmentLexiconBaker.BakeFromRepo(RepoRoot);
            JudgmentLexiconBaker.BakeOutput b = JudgmentLexiconBaker.BakeFromRepo(RepoRoot);

            Assert.AreEqual(a.Cooked.Length, b.Cooked.Length, "双跑产物长度不一致");
            CollectionAssert.AreEqual(a.Cooked, b.Cooked, "同源两次烘焙未逐位一致(确定性违例)");
            Assert.AreNotEqual(0u, a.ConfigVersion, "ConfigVersion 恒零 = 内容哈希未派生");
        }

        [Test]
        public void test_bake_roundtrip_threeRows_withOrphan()
        {
            JudgmentLexiconBaker.BakeOutput bake = JudgmentLexiconBaker.BakeFromRepo(RepoRoot);
            JudgmentLexiconDataSet ds = JudgmentLexiconCookedCodec.Read(bake.Cooked);

            Assert.GreaterOrEqual(ds.Rows.Count, 3, "源 JSON 至少 3 行");
            // 源数据形状:1 号关联病种 10;2/3 号孤儿(AC-37-35 结构保证)
            // 🔁 2026-11-07:外键由悬空的 `1` 修为 `10`(`伤寒` = `DIS_TYPHOID`;
            //    真注册表 id 段 = 2..17,**没有 1** ⇒ 原值让病名册在生产里恒空,
            //    真包实测 `pointable=0`。本断言随之改钉新值 —— 它钉的就是**源数据**,
            //    源数据一改它必须跟着动(否则就是自指判据的反面:钉死一个错值)。
            JudgmentLexemeRow r1 = ds.Rows[0];
            Assert.AreEqual(1, r1.LexiconId);
            Assert.AreEqual("伤寒", r1.Text);
            CollectionAssert.AreEqual(new[] { 10 }, r1.DiseaseIds);
            Assert.AreEqual(0, ds.Rows[1].DiseaseIds.Length, "2 号应为孤儿条目");
            Assert.AreEqual(0, ds.Rows[2].DiseaseIds.Length, "3 号应为孤儿条目");

            // 🔁 2026-11-07(用户裁定「让诊室默认来一个刺伤病人」):补第 4 行「刺伤」→ 病种 2。
            //    病因:出厂默认病人就是注册表第 0 条 = id 2(`INJ_STAB` 刺伤),
            //    而词表原先三个词条全是病名、没有伤情 ⇒ **不改启动旋钮就一本词条都翻不到**,
            //    教学链 `judge` 步在新手默认路径上永远走不到。
            //    ⚠️ 这条钉的是**源数据**(词表作者态):行被删 / 外键被改 ⇒ 立刻红。
            Assert.GreaterOrEqual(ds.Rows.Count, 4, "源 JSON 至少 4 行(第 4 行 = 刺伤)");
            JudgmentLexemeRow r4 = ds.Rows[3];
            Assert.AreEqual(4, r4.LexiconId);
            Assert.AreEqual("刺伤", r4.Text);
            CollectionAssert.AreEqual(new[] { 2 }, r4.DiseaseIds,
                "刺伤 = 注册表 id 2(INJ_STAB)—— 出厂默认病人的病种");

            Assert.AreEqual(bake.ConfigVersion, ds.ConfigVersion, "ConfigVersion roundtrip 失配");
        }

        // ── AC-37-35 词表侧(违例夹具各红)─────────────────────────────────

        [Test]
        public void test_ac3735_bijectiveCandidate_rejected()
        {
            const string json = @"{
                ""schema_version"": 1,
                ""entries"": [
                    { ""lexicon_id"": 1, ""text"": ""甲"", ""disease_ids"": [1] },
                    { ""lexicon_id"": 2, ""text"": ""乙"", ""disease_ids"": [2] }
                ]
            }";
            var ex = Assert.Throws<BakeValidationException>(() => JudgmentLexiconBinderProbe.Bake(json));
            StringAssert.Contains("AC-37-35", string.Join(";", ex.Errors));
        }

        [Test]
        public void test_lexiconIdZero_rejected()
        {
            const string json = @"{
                ""schema_version"": 1,
                ""entries"": [
                    { ""lexicon_id"": 0, ""text"": ""甲"", ""disease_ids"": [] }
                ]
            }";
            var ex = Assert.Throws<BakeValidationException>(() => JudgmentLexiconBinderProbe.Bake(json));
            StringAssert.Contains("lexicon-zero", string.Join(";", ex.Errors));
        }

        [Test]
        public void test_nonAscending_rejected()
        {
            const string json = @"{
                ""schema_version"": 1,
                ""entries"": [
                    { ""lexicon_id"": 2, ""text"": ""甲"", ""disease_ids"": [] },
                    { ""lexicon_id"": 1, ""text"": ""乙"", ""disease_ids"": [] }
                ]
            }";
            var ex = Assert.Throws<BakeValidationException>(() => JudgmentLexiconBinderProbe.Bake(json));
            StringAssert.Contains("lexicon-order", string.Join(";", ex.Errors));
        }

        [Test]
        public void test_duplicateId_rejected()
        {
            const string json = @"{
                ""schema_version"": 1,
                ""entries"": [
                    { ""lexicon_id"": 1, ""text"": ""甲"", ""disease_ids"": [] },
                    { ""lexicon_id"": 1, ""text"": ""乙"", ""disease_ids"": [] }
                ]
            }";
            var ex = Assert.Throws<BakeValidationException>(() => JudgmentLexiconBinderProbe.Bake(json));
            StringAssert.Contains("lexicon-dup", string.Join(";", ex.Errors));
        }

        [Test]
        public void test_emptyText_rejected()
        {
            const string json = @"{
                ""schema_version"": 1,
                ""entries"": [
                    { ""lexicon_id"": 1, ""text"": """", ""disease_ids"": [] }
                ]
            }";
            var ex = Assert.Throws<BakeValidationException>(() => JudgmentLexiconBinderProbe.Bake(json));
            StringAssert.Contains("lexicon-text", string.Join(";", ex.Errors));
        }

        [Test]
        public void test_unknownKey_rejected()
        {
            const string json = @"{
                ""schema_version"": 1,
                ""entries"": [
                    { ""lexicon_id"": 1, ""text"": ""甲"", ""disease_ids"": [], ""bogus"": 1 }
                ]
            }";
            var ex = Assert.Throws<BakeValidationException>(() => JudgmentLexiconBinderProbe.Bake(json));
            StringAssert.Contains("unknown-key", string.Join(";", ex.Errors));
        }

        [Test]
        public void test_orphanOnlyTable_passes()
        {
            const string json = @"{
                ""schema_version"": 1,
                ""entries"": [
                    { ""lexicon_id"": 1, ""text"": ""甲"", ""disease_ids"": [] },
                    { ""lexicon_id"": 2, ""text"": ""乙"", ""disease_ids"": [] }
                ]
            }";
            Assert.DoesNotThrow(() => JudgmentLexiconBinderProbe.Bake(json),
                "全孤儿词表(零关联)= 结构性非双射,应通过");
        }

        // ── 上界夹具:u8 承载(静默截断守卫)───────────────────────────────

        [Test]
        public void test_diseaseIdsOver255_rejected()
        {
            // 256 个互异病种 id(1..256):超 u8 上界 ⇒ 校验器须硬失败,
            // 否则 writer 的 (byte)256 → 0 会静默截断成错行。
            var sb = new System.Text.StringBuilder();
            sb.Append(@"{""schema_version"":1,""entries"":[{""lexicon_id"":1,""text"":""甲"",""disease_ids"":[");
            for (int i = 1; i <= 256; i++)
            {
                if (i > 1) sb.Append(',');
                sb.Append(i);
            }
            sb.Append(@"]}]}");

            var ex = Assert.Throws<BakeValidationException>(
                () => JudgmentLexiconBinderProbe.Bake(sb.ToString()));
            StringAssert.Contains("lexicon-disease-count", string.Join(";", ex.Errors));
        }

        [Test]
        public void test_diseaseIdsAt255_accepted()
        {
            // 边界正例:恰 255 个 ⇒ 合法(上界为闭区间)。
            var sb = new System.Text.StringBuilder();
            sb.Append(@"{""schema_version"":1,""entries"":[{""lexicon_id"":1,""text"":""甲"",""disease_ids"":[");
            for (int i = 1; i <= 255; i++)
            {
                if (i > 1) sb.Append(',');
                sb.Append(i);
            }
            sb.Append(@"]}]}");

            Assert.DoesNotThrow(() => JudgmentLexiconBinderProbe.Bake(sb.ToString()),
                "恰 255 个 disease_ids = 上界闭区间内,应通过");
        }
    }
}
