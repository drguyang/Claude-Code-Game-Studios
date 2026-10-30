// T1 · S9 —— 53 用药代价表的**烘焙链 + 构建前门**夹具(`CostTable53Bake` / `CostTableCookedWriter` /
// `CostTableGate`)。
//
// 权威:ADR-035 §Migration Plan **S9** · §Validation **V-5 / V-6 / V-7**(「构建 Failure」的宾语)
//       · ADR-014 §二(同源两次烘焙逐位一致)· §五(ConfigVersion = 源内容哈希)
//
// ⚠️ **本件证的是「门」,不是「构建」**:`CostTableGate.RunFromJson` 返回的错误集 = 构建门前门的
//   判据面。真正的「构建 `Result: Failure`」须走探针轮(插无条件失败项 ⇒ 构建红),
//   判据见 `CLAUDE.md` §构建前门 —— **本件不主张那一面已过**。
//
// ⚠️ 正向夹具沿用 `cost_table_binder_test` 的同一手法:对真实注册表做**单点改写**
//   (`INJ_HEMORRHAGE` → `lethal: false` ∧ `self_limit: true`,两处都得改,否则撞 R1-22),
//   而不是手搓假注册表 —— 手搓会绕过 `DiseaseRegistryBinder` 的既有校验。

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DaYiJingCheng.EditorTools.Bake;
using NUnit.Framework;
using UnityEngine;

namespace DaYiJingCheng.Tests.MedConsequences
{
    /// <summary>53 代价表烘焙链 + 构建前门。</summary>
    [TestFixture]
    public sealed class CostTableBakeTest
    {
        // ══════════════════════ 夹具基建(与 cost_table_binder_test 同源)══════════════════════

        private static string RepoRoot()
            => MedConsRepoPaths.RepoRoot;

        private static string RealCostTable()
            => File.ReadAllText(Path.Combine(RepoRoot(), "assets/data/53_cost_table.json"));

        private static string RealRegistry()
            => File.ReadAllText(Path.Combine(RepoRoot(), "assets/data/disease_registry.json"));

        /// <summary>把 <paramref name="key"/> 那一行改成**非致死 + 自限**(schema 允许的唯一形态)。</summary>
        private static string RegistryWithOneNonLethalInjury(string key = "INJ_HEMORRHAGE")
        {
            string json = RealRegistry();
            int atKey = json.IndexOf("\"key\": \"" + key + "\"", StringComparison.Ordinal);
            Assert.That(atKey, Is.GreaterThan(0), $"夹具前提不成立:真实注册表里没有 {key}");
            json = ReplaceFirstAfter(json, atKey, "\"lethal\": true", "\"lethal\": false");
            json = ReplaceFirstAfter(json, atKey, "\"self_limit\": false", "\"self_limit\": true");
            return json;
        }

        private static string ReplaceFirstAfter(string json, int after, string needle, string replacement)
        {
            int at = json.IndexOf(needle, after, StringComparison.Ordinal);
            Assert.That(at, Is.GreaterThan(0), $"夹具前提不成立:该行没有 `{needle}`");
            return json.Substring(0, at) + replacement + json.Substring(at + needle.Length);
        }

        /// <param name="extraRoot">额外根键片段(**不带尾逗号**)。</param>
        private static string CostTable(string rows = "",
                                        string enabled = "[\"contraindication\"]",
                                        string maxSeverity = "null",
                                        string bands = "{ \"contraindication\": null }",
                                        string extraRoot = "")
            => "{ \"schema_version\": 1"
             + ", \"harm_kind_enabled\": " + enabled
             + ", \"bands\": " + bands
             + ", \"max_severity_band\": " + maxSeverity
             + (extraRoot.Length > 0 ? ", " + extraRoot.TrimEnd(',', ' ') : "")
             + ", \"rows\": [" + rows + "] }";

        private static string Row(int band = 0, int severity = 0, string injury = "INJ_HEMORRHAGE",
                                  string kind = "contraindication")
            => $"{{ \"harm_kind\": \"{kind}\", \"band\": {band}, \"injury_id\": \"{injury}\", \"severity_band\": {severity} }}";

        /// <summary>从 cooked 字节里读出 row_count 与逐行 `(harm_kind, band, injury_id, severity_band)`。</summary>
        private static List<(int HarmKind, int Band, int InjuryId, int SeverityBand)> DecodeRows(byte[] cooked)
        {
            Assert.That(cooked.Length, Is.GreaterThanOrEqualTo(20), "cooked 短于 20 字节头");
            Assert.That(Encoding.ASCII.GetString(cooked, 0, 4), Is.EqualTo("DYJC"), "魔数须为 DYJC");
            int offset = 20;
            int count = ReadI32Le(cooked, ref offset);
            Assert.That(count, Is.GreaterThanOrEqualTo(0), "row_count 不得为负");
            var rows = new List<(int, int, int, int)>(count);
            for (int i = 0; i < count; i++)
            {
                int hk = ReadI32Le(cooked, ref offset);
                int band = ReadI32Le(cooked, ref offset);
                int injury = ReadI32Le(cooked, ref offset);
                int sev = ReadI32Le(cooked, ref offset);
                rows.Add((hk, band, injury, sev));
            }
            // 尾段(2026-10-30):threshold_count: i32 + 逐项 i64。
            // ⚠️ 漏读这一段会让下方「长度自洽」断言转红 —— 那正是尾段式**能被读方发现**的理由。
            int thresholdCount = ReadI32Le(cooked, ref offset);
            Assert.That(thresholdCount, Is.GreaterThanOrEqualTo(0), "过量档位线数不得为负");
            offset += thresholdCount * 8;

            Assert.That(offset, Is.EqualTo(cooked.Length), "载荷长度须与 row_count + 尾段严格自洽");
            return rows;
        }

        /// <summary>显式 little-endian 读 i32(**不用 `BitConverter`** —— 它的字节序随架构)。</summary>
        private static int ReadI32Le(byte[] buffer, ref int offset)
        {
            int value = buffer[offset]
                      | (buffer[offset + 1] << 8)
                      | (buffer[offset + 2] << 16)
                      | (buffer[offset + 3] << 24);
            offset += 4;
            return value;
        }

        /// <summary>注册表里 `INJ_HEMORRHAGE` 的整数 id(从源文本里读,不抄常量)。</summary>
        private static int RegistryIdOf(string key)
        {
            string json = RealRegistry();
            int atKey = json.IndexOf("\"key\": \"" + key + "\"", StringComparison.Ordinal);
            Assert.That(atKey, Is.GreaterThan(0));
            // 该行的 `"id": N` 写在 key 之前,故向前找最近的一个
            int atId = json.LastIndexOf("\"id\":", atKey, StringComparison.Ordinal);
            Assert.That(atId, Is.GreaterThan(0));
            int colon = json.IndexOf(':', atId);
            int start = colon + 1;
            while (json[start] == ' ') start++;
            int end = start;
            while (char.IsDigit(json[end])) end++;
            return int.Parse(json.Substring(start, end - start));
        }

        // ══════════════════════ 一、烘焙链(今日态)══════════════════════

        /// <summary>真实表能烘焙:三行真实代价,绑定集与编码产物**行数一致**,头完整。</summary>
        /// <remarks>
        /// 🔁 <b>2026-10-30 改判</b>:原夹具是 `test_realSkeleton_bakesEmptyTable`,断言真实表
        /// **零行**(S2 骨架)。用户裁定填入三档真实行后该前提已假 ⇒ 改为断言**三行且往返一致**。
        /// ⚠️ 「空表合法」这条形态**没有丢**:它由
        /// `CostTable53CookedCodecTest.test_emptyTable_decodesToLazyEmpty_notAnError` 与
        /// 下面本文件里的合成空表夹具继续守着 —— 真实表不再空,不等于「空表不再合法」。
        /// </remarks>
        [Test]
        public void test_realTable_bakesRealRows()
        {
            // 🔁 2026-10-30:过量支解除后真实表 = 3 禁忌 + 2 过量 = 5 行(原断言 3)。
            CostTable53Bake.BakeOutput o = CostTable53Bake.BakeFromJson(RealCostTable(), RealRegistry());

            Assert.That(o.Cooked, Is.Not.Null);
            Assert.That(o.Bound.Rows, Has.Count.EqualTo(5), "真实表 = 三禁忌档 + 两过量档");
            Assert.That(DecodeRows(o.Cooked), Has.Count.EqualTo(5), "cooked 行数须与绑定集一致");
            Assert.That(o.Bound.OverdoseBandThresholds, Has.Count.EqualTo(2),
                "真实表须带两条过量档位线");
        }

        /// <summary>同源两次烘焙逐位一致(ADR-014 §二)。</summary>
        [Test]
        public void test_determinism_sameInputsSameBytes()
        {
            string cost = CostTable(Row(0, 1) + "," + Row(1, 2));
            string reg = RegistryWithOneNonLethalInjury();

            byte[] a = CostTable53Bake.BakeFromJson(cost, reg).Cooked;
            byte[] b = CostTable53Bake.BakeFromJson(cost, reg).Cooked;

            Assert.That(a, Is.EqualTo(b), "同源两次烘焙须逐位一致");
        }

        /// <summary>行序与输入顺序无关:两行的声明次序交换 ⇒ **载荷**逐位相同。</summary>
        /// <remarks>
        /// ⚠️ **比的是载荷,不是整串字节**。整串字节**本来就不该相同** —— 头槽里的
        /// `configVersion` 是**源文本**的内容哈希,而两种行序是两份不同的源文本。
        /// 首版夹具比了整串,首跑即红;这不是实现错,是断言错(把它记在这里,
        /// 免得下一个人照着「整串相同」再写一遍)。
        /// </remarks>
        [Test]
        public void test_rowOrder_isCanonicalNotInputOrder()
        {
            string reg = RegistryWithOneNonLethalInjury();
            string forward = CostTable(Row(band: 0, severity: 1) + "," + Row(band: 1, severity: 2));
            string reversed = CostTable(Row(band: 1, severity: 2) + "," + Row(band: 0, severity: 1));

            byte[] a = CostTable53Bake.BakeFromJson(forward, reg).Cooked;
            byte[] b = CostTable53Bake.BakeFromJson(reversed, reg).Cooked;

            Assert.That(DecodeRows(a), Is.EqualTo(DecodeRows(b)),
                "行序须按 (harm_kind, band) 规范化,与输入顺序无关");
            Assert.That(a.Length, Is.EqualTo(b.Length), "载荷等长");

            var rows = DecodeRows(a);
            Assert.That(rows.Count, Is.EqualTo(2));
            Assert.That(rows[0].Band, Is.LessThan(rows[1].Band), "升序");
        }

        /// <summary>作者态字符串外键在烘焙期解析成注册表的**整数** id(ADR-014「玩家构建零解析器」)。</summary>
        [Test]
        public void test_injuryId_isResolvedToRegistryInteger()
        {
            string reg = RegistryWithOneNonLethalInjury();
            byte[] cooked = CostTable53Bake.BakeFromJson(CostTable(Row(band: 3, severity: 1)), reg).Cooked;

            var rows = DecodeRows(cooked);
            Assert.That(rows.Count, Is.EqualTo(1));
            Assert.That(rows[0].InjuryId, Is.EqualTo(RegistryIdOf("INJ_HEMORRHAGE")),
                "产物里须是注册表的整数 id,不是作者态字符串的某种编码");
            Assert.That(rows[0].Band, Is.EqualTo(3));
            Assert.That(rows[0].SeverityBand, Is.EqualTo(1));
            Assert.That(rows[0].HarmKind, Is.EqualTo(0), "contraindication = 0(CostHarmKind)");
        }

        /// <summary>ConfigVersion 也覆盖注册表:只动注册表文本 ⇒ 版本号变(ADR-010 §七 的比对前提)。</summary>
        [Test]
        public void test_configVersion_coversRegistryToo()
        {
            string cost = CostTable(Row());
            string reg = RegistryWithOneNonLethalInjury();

            uint v1 = CostTable53Bake.BakeFromJson(cost, reg).ConfigVersion;
            uint v2 = CostTable53Bake.BakeFromJson(cost, reg + "\n").ConfigVersion;

            Assert.That(v2, Is.Not.EqualTo(v1),
                "只改注册表文本就必须改 ConfigVersion —— 否则存档头比对对注册表改动是瞎的");
        }

        // ══════════════════════ 二、构建前门(S9 的判据面)══════════════════════

        /// <summary>真实仓里的两张源表 ⇒ 门绿(零错误)。</summary>
        [Test]
        public void test_gate_realRepo_isGreen()
        {
            List<string> errs = CostTableGate.RunAll(RepoRoot());
            Assert.That(errs, Is.Empty, "真实源表须过门;实得:" + string.Join(" | ", errs));
        }

        /// <summary>仓根不存在 ⇒ 门**返回**错误(不抛)。</summary>
        [Test]
        public void test_gate_missingSources_returnsErrorNotThrow()
        {
            List<string> errs = CostTableGate.RunAll(Path.Combine(Path.GetTempPath(), "no-such-repo-dyjc"));
            Assert.That(errs, Is.Not.Empty);
            Assert.That(string.Join(" | ", errs), Does.Contain("源文件不存在").Or.Contain("注册表不存在"));
        }

        /// <summary>
        /// **V-5 的判据面**:注入指向 `lethal = true` 伤情的行 ⇒ 门红且报文点名 **N-1**。
        /// </summary>
        /// <remarks>⚠️ 本夹具证的是**门返回错误**,不是「构建 Failure」—— 后者须探针轮。</remarks>
        [Test]
        public void test_gate_lethalRow_isRed_andNamesN1()
        {
            List<string> errs = CostTableGate.RunFromJson(CostTable(Row()), RealRegistry());

            Assert.That(errs, Is.Not.Empty, "指向致死伤情必须让门红");
            Assert.That(string.Join(" | ", errs), Does.Contain("N-1"));
            Assert.That(string.Join(" | ", errs), Does.Contain("INJ_HEMORRHAGE"),
                "报文须点名该行(行号或 injury_id)");
        }

        /// <summary>**V-6 的判据面**:`rows` 非空而 `harm_kind_enabled` 为空 ⇒ 门红(R4)。</summary>
        [Test]
        public void test_gate_rowsWithoutEnabledKind_isRed()
        {
            string reg = RegistryWithOneNonLethalInjury();
            List<string> errs = CostTableGate.RunFromJson(CostTable(Row(), enabled: "[]"), reg);

            Assert.That(errs, Is.Not.Empty);
            Assert.That(string.Join(" | ", errs), Does.Contain("harm_kind_enabled"));
        }

        /// <summary>**V-7 的判据面**(🔁 2026-10-30 改判):过量**启用而无档位线** ⇒ 门红。</summary>
        /// <remarks>
        /// <para><b>原文(保留)</b>:「启用 `overdose` ⇒ 门红(§⑥ 过量支挂起),报文须点名
        /// `TD-sim-02`」。§⑥ 解除后「启用即红」已不成立。</para>
        /// <para><b>现判据</b>:门仍须对**未完成的过量配置**红 —— 启用 `overdose` 却不给
        /// `overdose_band_thresholds` ⇒ 该 kind 的行永不生效(静默跳过同型)。</para>
        /// <para>⚠️ 判别力:删掉 `ReadOverdoseThresholds` 的联动分支 ⇒ 本测转红(errs 为空)。</para>
        /// </remarks>
        [Test]
        public void test_gate_overdoseEnabledWithoutThresholds_isRed()
        {
            string reg = RegistryWithOneNonLethalInjury();
            List<string> errs = CostTableGate.RunFromJson(
                CostTable(enabled: "[\"contraindication\",\"overdose\"]"), reg);

            Assert.That(errs, Is.Not.Empty);
            Assert.That(string.Join(" | ", errs), Does.Contain("overdose_band_thresholds"),
                "启用过量而无档位线 ⇒ 门须红并点名缺的键");
        }

        /// <summary>🔁 2026-10-30:过量**完整配置** ⇒ 门绿(原判据下这里恒红)。</summary>
        [Test]
        public void test_gate_overdoseFullyConfigured_isGreen()
        {
            string reg = RegistryWithOneNonLethalInjury();
            List<string> errs = CostTableGate.RunFromJson(
                CostTable(enabled: "[\"contraindication\",\"overdose\"]",
                          bands: "{ \"contraindication\": 3, \"overdose\": 1 }",
                          extraRoot: "\"overdose_band_thresholds\": [\"1\"]",
                          rows: Row(0, 1, kind: "overdose")),
                reg);
            Assert.That(errs, Is.Empty,
                "过量配置完整(启用 + 档数 + 档位线 + 行)时门不得报错;实得:" +
                string.Join(" | ", errs));
        }
    }
}
