// 权威来源:GDD 规则四/五/八 · 11-DC DC-1…DC-7 · AC-11-02/07/20
//          · ADR-014(两阶段烘焙)· ADR-024(Kind 已登记)
//
// 测试处方表与本草词表的构建期校验(DC-1…DC-7)。
//
// NOT-RUN 声明(禁借绿):
// - DC-2:action_id 闭集 = 处置注册表全值,取决于 OQ-11-2(枚举定值待数值/内容轮)
// - DC-6:依赖 9 侧 NOISE_BAND_9 常量(BL-2,O-11→9)
// - AC-11-07 双表 polarity 交叉硬门:9 的 disease_registry.json 不存在(9 侧未建)

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using DaYiJingCheng.EditorTools.Bake;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.PrescriptionMedication
{
    [TestFixture]
    public class PrescriptionTablesTest
    {
        private static string RepoRoot => ComputeRepoRoot();

        private static string ComputeRepoRoot([CallerFilePath] string thisFile = "")
            => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile) ?? ".",
                "..", "..", "..", "..", ".."));

        private static string DataDir => Path.Combine(RepoRoot, "assets", "data");

        // ── 辅助 ──────────────────────────────────────────────────────────

        private static string ReadActionsJson() =>
            File.ReadAllText(Path.Combine(DataDir, "prescription_actions.json"));

        private static string ReadLexiconJson() =>
            File.ReadAllText(Path.Combine(DataDir, "materia_lexicon.json"));

        private static string ReadItemsJson() =>
            File.ReadAllText(Path.Combine(DataDir, "item_database_items.json"));

        // ── DC-1:处方表 item_key ⊂ ItemDef 闭集 ───────────────────────────

        [Test]
        public void test_dc1_itemKeySubsetOfItemDef()
        {
            // 驱动生产绑定器:DC-1 由 PrescriptionActionsBinder.Bind 校验
            // 正向路径:当前数据集应通过(无 DC-1 错误)
            string actionsJson = ReadActionsJson();
            string lexiconJson = ReadLexiconJson();
            string itemsJson = ReadItemsJson();

            var ex = RecordException(() => PrescriptionActionsBinderProbe.Bind(actionsJson, lexiconJson, itemsJson));
            if (ex != null)
            {
                Assert.Fail($"DC-1 正向路径应通过,实际抛出:{string.Join("; ", ex.Errors)}");
            }
        }

        // ── DC-3:polarity ∈ {causal, symptomatic} ──────────────────────────

        [Test]
        public void test_dc3_polarityBinaryClosed()
        {
            // 驱动生产绑定器:DC-3 由 PrescriptionActionsBinder.Bind 校验
            string actionsJson = ReadActionsJson();
            string lexiconJson = ReadLexiconJson();
            string itemsJson = ReadItemsJson();

            var ex = RecordException(() => PrescriptionActionsBinderProbe.Bind(actionsJson, lexiconJson, itemsJson));
            if (ex != null)
            {
                Assert.Fail($"DC-3 正向路径应通过,实际抛出:{string.Join("; ", ex.Errors)}");
            }
        }

        // ── DC-5:词表每药恰一条 ───────────────────────────────────────────

        [Test]
        public void test_dc5_lexiconExactlyOnePerDrug()
        {
            // 驱动生产绑定器:DC-5 由 PrescriptionActionsBinder.Bind 校验
            string actionsJson = ReadActionsJson();
            string lexiconJson = ReadLexiconJson();
            string itemsJson = ReadItemsJson();

            var ex = RecordException(() => PrescriptionActionsBinderProbe.Bind(actionsJson, lexiconJson, itemsJson));
            if (ex != null)
            {
                Assert.Fail($"DC-5 正向路径应通过,实际抛出:{string.Join("; ", ex.Errors)}");
            }
        }

        // ── DC-7:0 < DOSE_BASE ≤ 65536 × hi ───────────────────────────────

        [Test]
        public void test_dc7_doseBasePositive()
        {
            // 驱动生产绑定器:DC-7 由 PrescriptionActionsBinder.Bind 校验
            string actionsJson = ReadActionsJson();
            string lexiconJson = ReadLexiconJson();
            string itemsJson = ReadItemsJson();

            var ex = RecordException(() => PrescriptionActionsBinderProbe.Bind(actionsJson, lexiconJson, itemsJson));
            if (ex != null)
            {
                Assert.Fail($"DC-7 正向路径应通过,实际抛出:{string.Join("; ", ex.Errors)}");
            }
        }

        [Test]
        public void test_dc7_doseBaseWithinUpperBound()
        {
            string actionsJson = ReadActionsJson();
            int doseBase = ExtractDoseBase(actionsJson);

            // 内联构造带非 null dose_range 的夹具(不依赖当前数据集)
            // 当前数据集 salicylic_acid 的 dose_range = null ⇒ 上界断言零执行(空集真空真)
            // 故此处内联两个夹具:一个 hi=1(上界 65536),一个 hi=2(上界 131072)
            var fixtures = new[]
            {
                (itemKey: "test_drug_a", hi: 1),
                (itemKey: "test_drug_b", hi: 2),
            };

            foreach (var (itemKey, hi) in fixtures)
            {
                long bound = 65536L * hi;
                Assert.LessOrEqual((long)doseBase, bound,
                    $"DC-7 违反:药 '{itemKey}' DOSE_BASE = {doseBase} > 65536 × {hi} = {bound}");
            }
        }

        // ── AC-11-02:零重定义 ──────────────────────────────────────────────

        [Test]
        public void test_ac1102_zeroRedefinition()
        {
            // AC-11-02 由 RejectUnknownKeys 隐式满足 —— 白名单不含 drug_potency/half_life/axis_offset 等
            // 正向路径:当前数据集应通过(无未知键错误)
            string actionsJson = ReadActionsJson();
            string lexiconJson = ReadLexiconJson();
            string itemsJson = ReadItemsJson();

            var ex = RecordException(() => PrescriptionActionsBinderProbe.Bind(actionsJson, lexiconJson, itemsJson));
            if (ex != null)
            {
                Assert.Fail($"AC-11-02 正向路径应通过,实际抛出:{string.Join("; ", ex.Errors)}");
            }
        }

        // ── AC-11-20:本草词表存在性 ────────────────────────────────────────

        [Test]
        public void test_ac1120_lexiconCoversAllDrugs()
        {
            // 驱动生产绑定器:AC-11-20 由 PrescriptionActionsBinder.Bind 校验
            string actionsJson = ReadActionsJson();
            string lexiconJson = ReadLexiconJson();
            string itemsJson = ReadItemsJson();

            var ex = RecordException(() => PrescriptionActionsBinderProbe.Bind(actionsJson, lexiconJson, itemsJson));
            if (ex != null)
            {
                Assert.Fail($"AC-11-20 正向路径应通过,实际抛出:{string.Join("; ", ex.Errors)}");
            }
        }

        // ── 烘焙确定性:同源集双跑字节相等 ─────────────────────────────────

        [Test]
        public void test_bakeDeterminism_sameInputSameOutput()
        {
            // 同源集双跑字节相等(烘焙确定性)
            var result1 = PrescriptionActionsBaker.BakeFromRepo(RepoRoot);
            var result2 = PrescriptionActionsBaker.BakeFromRepo(RepoRoot);

            Assert.AreEqual(result1.Cooked, result2.Cooked, "烘焙确定性:同源两次烘焙字节不等");
            Assert.AreEqual(result1.ConfigVersion, result2.ConfigVersion, "烘焙确定性:同源两次 ConfigVersion 不等");
        }

        // ── 负夹具:DC-1 外键违反 ───────────────────────────────────────────

        [Test]
        public void test_dc1_negative_unknownItemKey()
        {
            // 负夹具:item_key 不在 ItemDef 闭集中 ⇒ 应被拒
            // 构造违反条件的数据:item_key = "__unknown_drug__"
            string badActionsJson = @"{
                ""schema_version"": 1,
                ""dose_const"": { ""DOSE_BASE"": 65536, ""MAX_DOSE_DETENTS"": 5 },
                ""actions"": [
                    { ""item_key"": ""__unknown_drug__"", ""action_id"": 1, ""polarity"": ""symptomatic"" }
                ]
            }";
            string goodLexiconJson = ReadLexiconJson();

            var ex = Assert.Throws<BakeValidationException>(() =>
                PrescriptionActionsBinderProbe.Bind(badActionsJson, goodLexiconJson, ReadItemsJson()));
            Assert.IsTrue(ContainsError(ex, "DC-1") || ContainsError(ex, "item_key"),
                $"负夹具:DC-1 违反应被拒,实际错误:{string.Join("; ", ex.Errors)}");
        }

        // ── 负夹具:DC-3 极性违反 ───────────────────────────────────────────

        [Test]
        public void test_dc3_negative_invalidPolarity()
        {
            // 负夹具:polarity 不在闭集中 ⇒ 应被拒
            string badActionsJson = @"{
                ""schema_version"": 1,
                ""dose_const"": { ""DOSE_BASE"": 65536, ""MAX_DOSE_DETENTS"": 5 },
                ""actions"": [
                    { ""item_key"": ""salicylic_acid"", ""action_id"": 1, ""polarity"": ""invalid_polarity"" }
                ]
            }";
            string goodLexiconJson = ReadLexiconJson();

            var ex = Assert.Throws<BakeValidationException>(() =>
                PrescriptionActionsBinderProbe.Bind(badActionsJson, goodLexiconJson, ReadItemsJson()));
            Assert.IsTrue(ContainsError(ex, "DC-3") || ContainsError(ex, "polarity"),
                $"负夹具:DC-3 违反应被拒,实际错误:{string.Join("; ", ex.Errors)}");
        }

        // ── 负夹具:DC-5 词表重复 ───────────────────────────────────────────

        [Test]
        public void test_dc5_negative_duplicateLexiconEntry()
        {
            // 负夹具:同一药在词表中出现多次 ⇒ 应被拒
            string goodActionsJson = ReadActionsJson();
            string badLexiconJson = @"{
                ""schema_version"": 1,
                ""lexicon"": [
                    { ""item_key"": ""salicylic_acid"", ""功效词"": ""苦寒清热"", ""体征轴"": [""heat""] },
                    { ""item_key"": ""salicylic_acid"", ""功效词"": ""泻火解毒"", ""体征轴"": [""inflammation""] }
                ]
            }";

            var ex = Assert.Throws<BakeValidationException>(() =>
                PrescriptionActionsBinderProbe.Bind(goodActionsJson, badLexiconJson, ReadItemsJson()));
            Assert.IsTrue(ContainsError(ex, "DC-5") || ContainsError(ex, "多次"),
                $"负夹具:DC-5 违反应被拒,实际错误:{string.Join("; ", ex.Errors)}");
        }

        // ── 负夹具:DC-7 DOSE_BASE 边界 ─────────────────────────────────────

        [Test]
        public void test_dc7_negative_doseBaseZero()
        {
            // 负夹具:DOSE_BASE = 0 ⇒ 应被拒
            string badActionsJson = @"{
                ""schema_version"": 1,
                ""dose_const"": { ""DOSE_BASE"": 0, ""MAX_DOSE_DETENTS"": 5 },
                ""actions"": [
                    { ""item_key"": ""salicylic_acid"", ""action_id"": 1, ""polarity"": ""symptomatic"" }
                ]
            }";
            string goodLexiconJson = ReadLexiconJson();

            var ex = Assert.Throws<BakeValidationException>(() =>
                PrescriptionActionsBinderProbe.Bind(badActionsJson, goodLexiconJson, ReadItemsJson()));
            Assert.IsTrue(ContainsError(ex, "DC-7") || ContainsError(ex, "DOSE_BASE"),
                $"负夹具:DC-7 违反应被拒,实际错误:{string.Join("; ", ex.Errors)}");
        }

        // ── 辅助:错误列表搜索 ─────────────────────────────────────────────

        private static bool ContainsError(BakeValidationException ex, string keyword)
        {
            foreach (string e in ex.Errors)
                if (e.Contains(keyword)) return true;
            return false;
        }

        private static BakeValidationException RecordException(Action action)
        {
            try
            {
                action();
                return null;
            }
            catch (BakeValidationException ex)
            {
                return ex;
            }
        }

        // ── 辅助:JSON 字段提取 ─────────────────────────────────────────────

        private static List<string> ExtractItemKeys(string json)
        {
            var keys = new List<string>();
            var regex = new Regex(@"""item_key""\s*:\s*""([^""]+)""");
            foreach (Match m in regex.Matches(json))
                keys.Add(m.Groups[1].Value);
            return keys;
        }

        private static List<string> ExtractPolarities(string json)
        {
            var polarities = new List<string>();
            var regex = new Regex(@"""polarity""\s*:\s*""([^""]+)""");
            foreach (Match m in regex.Matches(json))
                polarities.Add(m.Groups[1].Value);
            return polarities;
        }

        private static int ExtractDoseBase(string json)
        {
            // DOSE_BASE 在 dose_const 对象内,需要匹配嵌套结构
            var regex = new Regex(@"""dose_const""\s*:\s*\{[^}]*""DOSE_BASE""\s*:\s*(\d+)");
            Match m = regex.Match(json);
            Assert.IsTrue(m.Success, "DOSE_BASE 字段未找到");
            return int.Parse(m.Groups[1].Value);
        }

        private static List<string> ExtractLexiconKeys(string json)
        {
            var keys = new List<string>();
            var regex = new Regex(@"""item_key""\s*:\s*""([^""]+)""");
            foreach (Match m in regex.Matches(json))
                keys.Add(m.Groups[1].Value);
            return keys;
        }

        private static List<string> ExtractBaseIds(string json)
        {
            var ids = new List<string>();
            var regex = new Regex(@"""base_id""\s*:\s*""([^""]+)""");
            foreach (Match m in regex.Matches(json))
                ids.Add(m.Groups[1].Value);
            return ids;
        }

        private static List<string> ExtractDrugIds(string json)
        {
            var ids = new List<string>();
            var regex = new Regex(@"""base_id""\s*:\s*""([^""]+)""");
            var categoryRegex = new Regex(@"""category""\s*:\s*""([^""]+)""");
            var matches = regex.Matches(json);
            var categoryMatches = categoryRegex.Matches(json);
            for (int i = 0; i < matches.Count; i++)
            {
                if (i < categoryMatches.Count && categoryMatches[i].Groups[1].Value == "drug")
                    ids.Add(matches[i].Groups[1].Value);
            }
            return ids;
        }

        private static List<(string itemKey, (int lo, int hi)? doseRange)> ExtractDrugProfiles(string json)
        {
            var result = new List<(string, (int, int)?)>();
            // 简化:只处理当前数据集(单药 salicylic_acid,dose_range = null)
            var regex = new Regex(@"""base_id""\s*:\s*""([^""]+)""");
            var matches = regex.Matches(json);
            foreach (Match m in matches)
            {
                string baseId = m.Groups[1].Value;
                // 检查是否有 drug_profile 且 dose_range 非 null
                int profileStart = json.IndexOf($"\"{baseId}\"", m.Index);
                if (profileStart < 0) continue;
                int profileEnd = json.IndexOf("}", profileStart);
                string profile = json.Substring(profileStart, profileEnd - profileStart);
                if (profile.Contains("\"drug_profile\"") && !profile.Contains("\"drug_profile\": null"))
                {
                    // 有 drug_profile,检查 dose_range
                    if (profile.Contains("\"dose_range\": null"))
                    {
                        result.Add((baseId, null));
                    }
                    else
                    {
                        // 解析 dose_range
                        var drRegex = new Regex(@"""dose_range""\s*:\s*\[(\d+),\s*(\d+)\]");
                        Match drMatch = drRegex.Match(profile);
                        if (drMatch.Success)
                        {
                            int lo = int.Parse(drMatch.Groups[1].Value);
                            int hi = int.Parse(drMatch.Groups[2].Value);
                            result.Add((baseId, (lo, hi)));
                        }
                    }
                }
            }
            return result;
        }
    }
}
