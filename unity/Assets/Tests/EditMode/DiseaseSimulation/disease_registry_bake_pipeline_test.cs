// 9 注册表烘焙管线 —— 判据测试(2026-10-22 立)。
//
// ══════════════════════════════════════════════════════════════════════════
// 本文件要证明的四件事
// ══════════════════════════════════════════════════════════════════════════
//   ① **真源文件能烘** —— `assets/data/disease_registry.json` 走
//      `DiseaseRegistryBaker.BakeFromRepo` 产出非空字节,零违例。
//   ② **产物确定** —— 同一源文本两次烘焙 ⇒ **逐位相同**字节(ADR-014 §二)。
//   ③ **头契约正确** —— magic "DYJC" / formatVersion / schemaVersion / payloadLength
//      逐字段核对,且 payloadLength + HeaderSize == 总长。
//   ④ **管线会红** —— 注入违例(JSON 数字写 Fix / 未知键 / kind 拼错 /
//      非 injury 行漏 trauma_half_life …)要求**各自**被拒。
//      没有这一条,「烘焙成功」与「binder 什么都没检查」无法区分。
//
// ⚠️ 本文件**只读**仓库文件(不写盘)⇒ 不触发 `FsWriteBoundaryGate`。
//
// 权威:ADR-014 §二/§三/§四/§五 · `CookedFormat.cs` · `DiseaseRegistryBinder.cs`

using System.IO;
using System.Runtime.CompilerServices;
using DaYiJingCheng.EditorTools.Bake;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Unit.Sim
{
    /// <summary>注册表烘焙:真源可烘 + 字节确定 + 头契约 + 违例各自被拒。</summary>
    [TestFixture]
    public sealed class DiseaseRegistryBakePipelineTest
    {
        private static string RepoRoot([CallerFilePath] string thisFile = "")
            => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile) ?? ".",
                "..", "..", "..", "..", ".."));

        private static string SourcePath =>
            Path.Combine(RepoRoot(), "assets", "data", "disease_registry.json");

        /// <summary>取一份**最小合法**源文本(单行 injury),供注入测试改写。</summary>
        private static string MinimalJson() => @"{
  ""schema_version"": 1,
  ""diseases"": [
    {
      ""id"": 2,
      ""key"": ""INJ_PROBE"",
      ""kind"": ""injury"",
      ""severity"": 4,
      ""lethal"": true,
      ""scale"": ""100"",
      ""trauma_half_life"": 60,
      ""natural_progress"": {
        ""incubation"": 0,
        ""a_peak"": ""100"",
        ""tau_rise"": ""40"",
        ""tau_peak"": 300,
        ""tau_fall"": ""600"",
        ""plateau"": false,
        ""self_limit"": false,
        ""boundary_mode"": ""monotone""
      }
    }
  ]
}";

        // ══════════════════════════════════════════════════════════════════════
        // ① 真源文件能烘
        // ══════════════════════════════════════════════════════════════════════

        [Test]
        public void test_realSource_bakesWithoutError()
        {
            Assert.That(File.Exists(SourcePath), Is.True, $"源文件不存在:{SourcePath}");

            var outp = DiseaseRegistryBaker.BakeFromRepo(RepoRoot());

            Assert.That(outp.Cooked, Is.Not.Null.And.Not.Empty, "烘焙产出空字节");
            Assert.That(outp.ConfigVersion, Is.Not.Zero, "ConfigVersion 不得为 0(内容哈希派生)");

            // ⚠️ 不写死行数 —— 写死会让「加一行数据」变成「改一个测试」,
            //    而这两件事的语义完全不同。改为断言**内容结构**:
            //    ① 至少 1 行(R1-17);② 伤情与病种两类都在;③ 每行都有曲线。
            Assert.That(outp.Rows.Count, Is.GreaterThanOrEqualTo(1), "R1-17:至少 1 个病种");

            var keys = new System.Collections.Generic.HashSet<string>();
            foreach (var row in outp.Rows) keys.Add(row.DiseaseKey);

            Assert.That(keys, Does.Contain("INJ_STAB"), "R2 白名单伤情缺失");
            Assert.That(keys, Does.Contain("DIS_MALARIA"), "R3.3 病种缺失");
            Assert.That(keys, Does.Contain("DIS_TETANUS"), "R3.3 病种缺失");

            foreach (var row in outp.Rows)
                Assert.That(row.Curve, Is.Not.Null, $"{row.DiseaseKey} 缺 natural_progress 曲线(R1.2 铁律)");
        }

        /// <summary>最小合法源文本也必须能烘 —— 防「只对真文件碰巧能跑」。</summary>
        [Test]
        public void test_minimalValidSource_bakes()
        {
            var outp = DiseaseRegistryBaker.BakeFromJson(MinimalJson());
            Assert.That(outp.Cooked, Is.Not.Empty);
            Assert.That(outp.Rows.Count, Is.EqualTo(1));
        }

        // ══════════════════════════════════════════════════════════════════════
        // ② 产物确定(逐位)
        // ══════════════════════════════════════════════════════════════════════

        [Test]
        public void test_sameSource_producesBitIdenticalBytes()
        {
            string json = File.ReadAllText(SourcePath);

            byte[] a = DiseaseRegistryBaker.BakeFromJson(json).Cooked;
            byte[] b = DiseaseRegistryBaker.BakeFromJson(json).Cooked;

            Assert.That(a.Length, Is.EqualTo(b.Length), "两次烘焙长度不同");
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i])
                    Assert.Fail($"第 {i} 字节不同:{a[i]:X2} vs {b[i]:X2} —— 产物非确定");
            }
        }

        /// <summary>ConfigVersion 相同 ⇒ 字节相同(内容哈希派生的一致性)。</summary>
        [Test]
        public void test_sameContent_sameConfigVersion()
        {
            string json = File.ReadAllText(SourcePath);
            uint v1 = DiseaseRegistryBaker.BakeFromJson(json).ConfigVersion;
            uint v2 = DiseaseRegistryBaker.BakeFromJson(json).ConfigVersion;
            Assert.That(v1, Is.EqualTo(v2));
        }

        /// <summary>内容变了 ⇒ ConfigVersion 变(否则存档头比对失效)。</summary>
        [Test]
        public void test_changedContent_changesConfigVersion()
        {
            string json = File.ReadAllText(SourcePath);
            uint before = DiseaseRegistryBaker.BakeFromJson(json).ConfigVersion;

            // 改一个数(INJ_BITE 的 tau_peak 700 → 701)
            string mutated = json.Replace("\"tau_peak\": 700", "\"tau_peak\": 701");
            Assert.That(mutated, Is.Not.EqualTo(json), "夹具没改到东西");

            uint after = DiseaseRegistryBaker.BakeFromJson(mutated).ConfigVersion;
            Assert.That(after, Is.Not.EqualTo(before),
                "内容变了 ConfigVersion 没变 —— 存档头比对会失效(ADR-014 §五)");
        }

        // ══════════════════════════════════════════════════════════════════════
        // ③ 头契约
        // ══════════════════════════════════════════════════════════════════════

        [Test]
        public void test_header_matchesCookedFormat()
        {
            var outp = DiseaseRegistryBaker.BakeFromJson(MinimalJson());
            byte[] b = outp.Cooked;

            Assert.That(b.Length, Is.GreaterThan(DaYiJingCheng.Sim.Contracts.CookedFormat.HeaderSize));

            // magic "DYJC"
            Assert.That(b[0], Is.EqualTo((byte)'D'));
            Assert.That(b[1], Is.EqualTo((byte)'Y'));
            Assert.That(b[2], Is.EqualTo((byte)'J'));
            Assert.That(b[3], Is.EqualTo((byte)'C'));

            Assert.That(ReadU32Le(b, 4), Is.EqualTo(DaYiJingCheng.Sim.Contracts.CookedFormat.FormatVersion),
                "formatVersion 不符");
            Assert.That(ReadU32Le(b, 8), Is.EqualTo(1u), "schemaVersion 应为源文件声明的 1");
            Assert.That(ReadU32Le(b, 12), Is.EqualTo(outp.ConfigVersion), "configVersion 不符");

            uint payloadLen = ReadU32Le(b, 16);
            Assert.That(DaYiJingCheng.Sim.Contracts.CookedFormat.HeaderSize + payloadLen,
                Is.EqualTo(b.Length), "payloadLength 与实际长度不符(头契约破裂)");
        }

        private static uint ReadU32Le(byte[] b, int off) =>
            (uint)(b[off] | (b[off + 1] << 8) | (b[off + 2] << 16) | (b[off + 3] << 24));

        // ══════════════════════════════════════════════════════════════════════
        // ④ 管线会红
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// **Fix 字段写成 JSON 数字 ⇒ 硬拒**(ADR-014 §四 硬规则 1)。
        /// <para>这是整条管线最要紧的一条判据:数字 token 经 double/decimal 中转
        /// = 浮点进内存,绕 `FixParse` —— 而且它是**静默**的。</para>
        /// </summary>
        [Test]
        public void test_fixAsJsonNumber_isRejected()
        {
            string bad = MinimalJson().Replace("\"a_peak\": \"100\"", "\"a_peak\": 100");

            var ex = Assert.Throws<BakeValidationException>(() => DiseaseRegistryBaker.BakeFromJson(bad));
            Assert.That(ex.Message, Does.Contain("须为**字符串**").Or.Contain("浮点泄漏"),
                "Fix 写成 JSON 数字未被拒 —— 浮点会静默进定点域");
        }

        /// <summary>Fix 字段写成浮点字面量字符串 ⇒ 被 FixParse 拒(禁小数)。</summary>
        [Test]
        public void test_fixAsFloatString_isRejected()
        {
            string bad = MinimalJson().Replace("\"a_peak\": \"100\"", "\"a_peak\": \"0.75\"");
            Assert.Throws<BakeValidationException>(() => DiseaseRegistryBaker.BakeFromJson(bad));
        }

        /// <summary>未知键 ⇒ 硬失败(ADR-014 §三,防拼写错误静默丢字段)。</summary>
        [Test]
        public void test_unknownKey_isRejected()
        {
            string bad = MinimalJson().Replace("\"severity\": 4", "\"severty\": 4");
            var ex = Assert.Throws<BakeValidationException>(() => DiseaseRegistryBaker.BakeFromJson(bad));
            Assert.That(ex.Message, Does.Contain("未知键").Or.Contain("severty"));
        }

        /// <summary>`_` 前缀的作者态注记**不**算未知键(防判据过宽)。</summary>
        [Test]
        public void test_underscorePrefixedKeys_areAllowed()
        {
            string withNote = MinimalJson().Replace(
                "\"schema_version\": 1,",
                "\"schema_version\": 1,\n  \"_note\": \"作者态注记\",");
            Assert.DoesNotThrow(() => DiseaseRegistryBaker.BakeFromJson(withNote),
                "`_` 前缀注记被当成未知键 —— 判据过宽");
        }

        /// <summary>`kind` 拼错 / 不在闭集 ⇒ 被拒。</summary>
        [Test]
        public void test_unknownKind_isRejected()
        {
            string bad = MinimalJson().Replace("\"kind\": \"injury\"", "\"kind\": \"wound\"");
            var ex = Assert.Throws<BakeValidationException>(() => DiseaseRegistryBaker.BakeFromJson(bad));
            Assert.That(ex.Message, Does.Contain("wound"));
        }

        /// <summary>injury 行漏 `trauma_half_life` ⇒ `R1_CHECK_32` 拒。</summary>
        [Test]
        public void test_injuryMissingTraumaHalfLife_isRejected()
        {
            string bad = MinimalJson().Replace("\"trauma_half_life\": 60,", "");
            var ex = Assert.Throws<BakeValidationException>(() => DiseaseRegistryBaker.BakeFromJson(bad));
            Assert.That(ex.Message, Does.Contain("R1-32"));
        }

        /// <summary>`A_peak > Scale` ⇒ `R1_CHECK_23` 拒。</summary>
        [Test]
        public void test_apexAboveScale_isRejected()
        {
            string bad = MinimalJson().Replace("\"a_peak\": \"100\"", "\"a_peak\": \"150\"");
            var ex = Assert.Throws<BakeValidationException>(() => DiseaseRegistryBaker.BakeFromJson(bad));
            Assert.That(ex.Message, Does.Contain("R1-23"));
        }

        /// <summary>急性保持型但 `lethal: false` ⇒ `R1_CHECK_22` 拒。</summary>
        [Test]
        public void test_acuteHoldWithoutLethal_isRejected()
        {
            string bad = MinimalJson().Replace("\"lethal\": true", "\"lethal\": false");
            var ex = Assert.Throws<BakeValidationException>(() => DiseaseRegistryBaker.BakeFromJson(bad));
            Assert.That(ex.Message, Does.Contain("R1-22"));
        }

        /// <summary>缺 `natural_progress` ⇒ 拒(R1.2 铁律:必须存在且可序列化)。</summary>
        [Test]
        public void test_missingNaturalProgress_isRejected()
        {
            string bad = MinimalJson().Replace("\"natural_progress\": {", "\"_removed_np\": {");
            var ex = Assert.Throws<BakeValidationException>(() => DiseaseRegistryBaker.BakeFromJson(bad));
            Assert.That(ex.Message, Does.Contain("natural_progress"));
        }

        /// <summary>空 `diseases` 数组 ⇒ `R1_CHECK_17` 拒(至少 1 个)。</summary>
        [Test]
        public void test_emptyRegistry_isRejected()
        {
            string bad = @"{
  ""schema_version"": 1,
  ""diseases"": []
}";
            var ex = Assert.Throws<BakeValidationException>(() => DiseaseRegistryBaker.BakeFromJson(bad));
            Assert.That(ex.Message, Does.Contain("R1-17"));
        }

        /// <summary>语法坏掉的 JSON ⇒ 阶段 1 词法拒(不是崩)。</summary>
        [Test]
        public void test_malformedJson_isRejectedAtStage1()
        {
            var ex = Assert.Throws<BakeValidationException>(
                () => DiseaseRegistryBaker.BakeFromJson("{ \"schema_version\": 1, "));
            Assert.That(ex.Message, Does.Contain("阶段1").Or.Contain("词法"));
        }

        // ══════════════════════════════════════════════════════════════════════
        // ⑤ 丢失登记 —— 白名单内但未绑定的键必须**可见**
        // ══════════════════════════════════════════════════════════════════════
        //
        // ADR-014 §三 的未知键门只挡「拼错的键」。一个**拼对的、白名单内的、
        // 却没人读的**键,丢字段是静默的 —— 门看不见它。
        // 下面两条把那种丢失变成可断言的量。

        /// <summary>
        /// 真源文件当前有 5 个键声明了但未绑定。断言**恰是这 5 个** ——
        /// 少一个(有人补了绑定)或多一个(有人加了新键却没绑)都会红。
        /// <para>⚠️ 本条**不**主张这 5 个键的丢失是可接受的。它只保证:
        /// 丢失的量**不随时间静默变化**。清单本身的缩减归各自实现轮。</para>
        /// </summary>
        [Test]
        public void test_realSource_unboundKeySetIsExactlyKnown()
        {
            var outp = DiseaseRegistryBaker.BakeFromRepo(RepoRoot());

            string[] expected = { "causes", "handle", "signs", "treatable_by", "zh" };
            Assert.That(outp.UnboundKeys, Is.EqualTo(expected),
                "未绑定键集与登记不符 —— 要么有人补了绑定(改本断言),要么有人加了新键没绑(补绑定或登记)");
        }

        /// <summary>全部键都被绑定时,<c>UnboundKeys</c> 为空(防「恒定非空」的假判据)。</summary>
        [Test]
        public void test_minimalSource_hasNoUnboundKeys()
        {
            var outp = DiseaseRegistryBaker.BakeFromJson(MinimalJson());
            Assert.That(outp.UnboundKeys, Is.Empty,
                "最小合法源文本的每个键都应被绑定 —— 非空说明 BoundRowKeys 与 BindRow 脱节");
        }

        /// <summary>`_` 前缀注记不计入未绑定(它们按设计就不绑定)。</summary>
        [Test]
        public void test_authorNotes_areNotCountedAsUnbound()
        {
            string withNote = MinimalJson().Replace(
                "\"schema_version\": 1,",
                "\"schema_version\": 1,\n  \"_note\": \"注记\",");
            var outp = DiseaseRegistryBaker.BakeFromJson(withNote);
            Assert.That(outp.UnboundKeys, Is.Empty);
        }
    }
}
