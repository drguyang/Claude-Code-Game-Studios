// T1 · S3 —— `CostTableBinder`(53 禁忌 / 过量代价表的阶段 2 绑定器)夹具。
//
// 权威:ADR-035 §Decision ②(表形状)/ ③(N-1 · N-3 · N-4′)/ ⑥(过量支挂起)/
//       §Risks R2(compounds 零输入可达 ⇒ canary)/ R4(空表窗口)/ R5 + §Validation V-10(零非致死伤情 ⇒ 告警)
//
// ⚠️ **正向夹具为什么要「注入」注册表**:今日真实注册表 5 件伤情**全部 `lethal = true`**
//    (`TD-sim-03`)⇒ 合法行**一行也构造不出来**。正向夹具因此对真实注册表做**单点改写**
//    (把 `INJ_HEMORRHAGE` 的 `lethal` 由 true 改成 false),而不是手搓一张假注册表 ——
//    手搓会绕过 `DiseaseRegistryBinder` 的全部既有校验,夹具与生产路径分叉。
//    ⚠️ 这**不是**借绿:反向夹具(真实注册表 ⇒ N-1 拒收)同时在场,两侧都在。
//
// ⚠️ 每条拒收夹具都标了它钉的是哪一条判据;反例夹具(应通过)用来防判据过宽。

using System;
using System.Collections.Generic;
using System.IO;
using DaYiJingCheng.EditorTools.Bake;
using NUnit.Framework;
using UnityEngine;

namespace DaYiJingCheng.Tests.MedConsequences
{
    /// <summary>53 代价表绑定器 —— 七条拒收 + 一条告警 + 四条反例。</summary>
    [TestFixture]
    public sealed class CostTableBinderTest
    {
        // ══════════════════════ 夹具基建 ══════════════════════

        private static string RepoRoot()
            => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));

        private static string RealCostTable() => File.ReadAllText(Path.Combine(RepoRoot(), "assets/data/53_cost_table.json"));
        private static string RealRegistry() => File.ReadAllText(Path.Combine(RepoRoot(), "assets/data/disease_registry.json"));

        /// <summary>
        /// 把 `INJ_HEMORRHAGE` 那一行改成**非致死 + 自限**(`lethal: false` ∧ `self_limit: true`)——
        /// 造出唯一一件**schema 允许**的非致死伤情。
        /// </summary>
        /// <remarks>
        /// <para>⚠️ <b>两处都得改</b>:只翻 `lethal` 会被 `DiseaseRegistryBinder` 的 **R1-22** 拒 ——
        /// 「急性保持型(`self_limit=false ∧ plateau=false`)仅当 `lethal=true`」。
        /// 首次实跑就撞上了这条(19 跑 6 红里 5 条是它),**这是实测,不是预判**。</para>
        /// <para>⇒ 该事实同时是 `TD-sim-03` 的性质订正:注册表不是「没建非致死伤情」,
        /// 而是**当前校验不允许**急性保持型伤情非致死。本条夹具因此走 schema 允许的形态
        /// (自限型),而不是把一条会被拒的行当「合法行」硬塞。</para>
        /// <para>定位方式 = 先找该行 `"key"`,再找其后第一个目标串,故对**行序变化免疫**。</para>
        /// </remarks>
        private static string RegistryWithOneNonLethalInjury(string key = "INJ_HEMORRHAGE")
        {
            string json = RealRegistry();
            int atKey = json.IndexOf("\"key\": \"" + key + "\"", StringComparison.Ordinal);
            Assert.That(atKey, Is.GreaterThan(0), $"夹具前提不成立:真实注册表里没有 {key}");

            json = ReplaceFirstAfter(json, atKey, "\"lethal\": true", "\"lethal\": false");
            json = ReplaceFirstAfter(json, atKey, "\"self_limit\": false", "\"self_limit\": true");
            return json;
        }

        /// <summary>把 <paramref name="after"/> 之后**第一次**出现的 <paramref name="needle"/> 换掉。</summary>
        private static string ReplaceFirstAfter(string json, int after, string needle, string replacement)
        {
            int at = json.IndexOf(needle, after, StringComparison.Ordinal);
            Assert.That(at, Is.GreaterThan(0), $"夹具前提不成立:该行没有 `{needle}`");
            return json.Substring(0, at) + replacement + json.Substring(at + needle.Length);
        }

        /// <summary>在一行注册表条目里插入一个 `compounds` 键(canary 夹具用)。</summary>
        private static string RegistryWithCompoundsKey()
        {
            string json = RealRegistry();
            int at = json.IndexOf("\"key\": \"INJ_HEMORRHAGE\"", StringComparison.Ordinal);
            Assert.That(at, Is.GreaterThan(0));
            return json.Substring(0, at) + "\"compounds\": [],\n      " + json.Substring(at);
        }

        /// <summary>拼一张代价表(JSON 文本;字段刻意用调用方给的原文,便于注入非法 token)。</summary>
        private static string CostTable(
            string enabled = "[\"contraindication\"]",
            string maxSeverity = "null",
            string rows = "",
            string bands = "{ \"contraindication\": null }",
            string schemaVersion = "1")
            => "{ \"schema_version\": " + schemaVersion
             + ", \"harm_kind_enabled\": " + enabled
             + ", \"bands\": " + bands
             + ", \"max_severity_band\": " + maxSeverity
             + ", \"rows\": [" + rows + "] }";

        /// <summary>一行合法代价(指向非致死伤情;须配 <see cref="RegistryWithOneNonLethalInjury"/>)。</summary>
        private static string Row(string injury = "INJ_HEMORRHAGE", int band = 0, int severity = 0,
                                  string kind = "contraindication")
            => $"{{ \"harm_kind\": \"{kind}\", \"band\": {band}, \"injury_id\": \"{injury}\", \"severity_band\": {severity} }}";

        private static BakeValidationException AssertRejected(string cost, string registry)
            => Assert.Throws<BakeValidationException>(() => CostTableBinder.Bind(cost, registry));

        private static string Joined(BakeValidationException ex) => string.Join(" | ", ex.Errors);

        private static void AssertMessageHas(BakeValidationException ex, string needle)
            => Assert.That(Joined(ex), Does.Contain(needle), "报文须点名该判据:" + Joined(ex));

        // ══════════════════════ 一、真实骨架(今日态)══════════════════════

        /// <summary>真实两张表:绑定成功、零行、**且必须带上 V-10 告警**(点名 `TD-sim-03`)。</summary>
        /// <remarks>钉的是 `ADR-035 §Validation V-10`:N-1 的绿灯须能一眼看出是「无输入」。</remarks>
        [Test]
        public void test_realSkeleton_binds_andWarnsNoNonLethalInjury()
        {
            CostTableBinder.BindResult r = CostTableBinder.Bind(RealCostTable(), RealRegistry());

            Assert.That(r.Rows, Is.Empty, "S2 空表的 rows 应为空");
            Assert.That(r.SchemaVersion, Is.EqualTo(1u));
            Assert.That(string.Join(" | ", r.Warnings), Does.Contain("TD-sim-03"),
                "真实注册表零非致死伤情 ⇒ 必须告警并点名 TD-sim-03(V-10);实得:" +
                string.Join(" | ", r.Warnings));
            Assert.That(string.Join(" | ", r.Warnings), Does.Contain("无输入"),
                "告警须把绿灯说成「无输入」,否则会被读成「药害不致死已获证明」");
        }

        /// <summary>反例:全空(`enabled=[]` + `rows=[]`)= 合法惰性形态(不得因此拒收)。</summary>
        [Test]
        public void test_emptyEverything_isLegal()
        {
            CostTableBinder.BindResult r = CostTableBinder.Bind(
                CostTable(enabled: "[]", bands: "{}", rows: ""), RealRegistry());
            Assert.That(r.Rows, Is.Empty);
        }

        // ══════════════════════ 二、正向路径(注入一件非致死伤情)══════════════════════

        /// <summary>注入 `INJ_HEMORRHAGE` 为非致死 ⇒ 一行合法代价被绑定。</summary>
        [Test]
        public void test_happyPath_withInjectedNonLethalInjury_bindsRow()
        {
            string registry = RegistryWithOneNonLethalInjury();
            CostTableBinder.BindResult r = CostTableBinder.Bind(CostTable(rows: Row()), registry);

            Assert.That(r.Rows, Has.Count.EqualTo(1));
            Assert.That(r.Rows[0].InjuryId, Is.EqualTo("INJ_HEMORRHAGE"));
            Assert.That(r.Rows[0].SeverityBand, Is.Zero);
            Assert.That(string.Join(" | ", r.Warnings), Does.Not.Contain("TD-sim-03"),
                "有非致死伤情时不应再报 V-10 告警");
        }

        /// <summary>反例:同一伤情出现在两个不同档 ⇒ 合法(单射判据不得误伤)。</summary>
        [Test]
        public void test_sameInjuryOnDifferentBands_isLegal()
        {
            string registry = RegistryWithOneNonLethalInjury();
            CostTableBinder.BindResult r = CostTableBinder.Bind(
                CostTable(rows: Row(band: 0) + "," + Row(band: 1)), registry);
            Assert.That(r.Rows, Has.Count.EqualTo(2));
        }

        /// <summary>反例:`max_severity_band = 0` + `severity_band = 0` ⇒ 合法(0 = 无程度档)。</summary>
        [Test]
        public void test_maxSeverityBandZero_isLegal()
        {
            string registry = RegistryWithOneNonLethalInjury();
            CostTableBinder.BindResult r = CostTableBinder.Bind(
                CostTable(maxSeverity: "0", rows: Row(severity: 0)), registry);
            Assert.That(r.Rows, Has.Count.EqualTo(1));
        }

        // ══════════════════════ 三、N-1 / N-3 / 外键 ══════════════════════

        /// <summary>N-1:指向真实注册表里的 **lethal = true** 伤情 ⇒ 拒(不注入时的默认态)。</summary>
        [Test]
        public void test_n1_lethalInjury_isRejected()
        {
            BakeValidationException ex = AssertRejected(CostTable(rows: Row()), RealRegistry());
            AssertMessageHas(ex, "N-1");
            AssertMessageHas(ex, "lethal");
        }

        /// <summary>外键:注册表里没有的 id ⇒ 拒。</summary>
        [Test]
        public void test_unknownInjuryId_isRejected()
        {
            BakeValidationException ex = AssertRejected(
                CostTable(rows: Row(injury: "INJ_NOT_A_THING")), RegistryWithOneNonLethalInjury());
            AssertMessageHas(ex, "外键失效");
        }

        /// <summary>kind 闸:指向一件 **lethal = false 的病种**(`DIS_MEASLES`)⇒ 仍拒(落点须是伤情)。</summary>
        /// <remarks>这条防的是「只查 lethal 不查 kind」的过宽实现 —— 病种行 lethal=false 会蒙混过关。</remarks>
        [Test]
        public void test_n3_diseaseRowId_isRejected()
        {
            BakeValidationException ex = AssertRejected(
                CostTable(rows: Row(injury: "DIS_MEASLES")), RealRegistry());
            AssertMessageHas(ex, "kind ≠ injury");
        }

        /// <summary>N-3:结局轴符号(53 自己的枚举当 id)⇒ 拒,且报文点名 N-3。</summary>
        [Test]
        public void test_n3_outcomeAxisSymbol_isRejected()
        {
            foreach (string sym in new[] { "patient_lives", "PatientLoses" })
            {
                BakeValidationException ex = AssertRejected(
                    CostTable(rows: Row(injury: sym)), RegistryWithOneNonLethalInjury());
                AssertMessageHas(ex, "N-3");
            }
        }

        // ══════════════════════ 四、N-4′ canary ══════════════════════

        /// <summary>canary:注册表出现 `compounds` 键 ⇒ 拒,且报文必须指向 **N-4 / 复查**。</summary>
        /// <remarks>钉 `ADR-035 §Risks R2`:canary 今天是绿的,但第一次有人加 `compounds` 就必红。</remarks>
        [Test]
        public void test_n4prime_compoundsKeyInRegistry_isRejected_pointingToReview()
        {
            BakeValidationException ex = AssertRejected(
                CostTable(rows: Row()), RegistryWithCompoundsKey());
            AssertMessageHas(ex, "N-4");
            AssertMessageHas(ex, "复查");
        }

        /// <summary>反例:注册表**没有** `compounds` ⇒ canary 不得误伤(真实绑定的默认路径)。</summary>
        [Test]
        public void test_n4prime_absentCompounds_doesNotFire()
        {
            Assert.DoesNotThrow(() => CostTableBinder.Bind(
                CostTable(rows: Row()), RegistryWithOneNonLethalInjury()));
        }

        // ══════════════════════ 五、R4 / §⑥ / 单射 / 未知键 / 类型 ══════════════════════

        /// <summary>R4:声明了行但 `harm_kind_enabled` 为空 ⇒ 拒(静默跳过形态)。</summary>
        [Test]
        public void test_r4_rowsNonEmptyButEnabledEmpty_isRejected()
        {
            BakeValidationException ex = AssertRejected(
                CostTable(enabled: "[]", rows: Row()), RegistryWithOneNonLethalInjury());
            AssertMessageHas(ex, "harm_kind_enabled 为空");
        }

        /// <summary>逐行闸:行里的 kind 未启用 ⇒ 拒(**不得静默跳过**)。</summary>
        [Test]
        public void test_rowKindNotEnabled_isRejected_notSilentlySkipped()
        {
            BakeValidationException ex = AssertRejected(
                CostTable(enabled: "[]", rows: Row(kind: "contraindication")),
                RegistryWithOneNonLethalInjury());
            AssertMessageHas(ex, "未启用");
        }

        /// <summary>§⑥:启用 `overdose` ⇒ 拒(过量支挂起,解除条件是两条债)。</summary>
        [Test]
        public void test_overdoseEnabled_isRejected()
        {
            BakeValidationException ex = AssertRejected(
                CostTable(enabled: "[\"contraindication\",\"overdose\"]"),
                RegistryWithOneNonLethalInjury());
            AssertMessageHas(ex, "overdose");
            AssertMessageHas(ex, "TD-sim-02");
        }

        /// <summary>单射:`(harm_kind, band)` 重复 ⇒ 拒。</summary>
        [Test]
        public void test_duplicateKindBand_isRejected()
        {
            BakeValidationException ex = AssertRejected(
                CostTable(rows: Row(band: 0) + "," + Row(band: 0)),
                RegistryWithOneNonLethalInjury());
            AssertMessageHas(ex, "单射");
        }

        /// <summary>未知根键 ⇒ 拒(ADR-014 §三)。</summary>
        [Test]
        public void test_unknownRootKey_isRejected()
        {
            string cost = CostTable().Replace("\"rows\":", "\"row\": [], \"rows\":");
            BakeValidationException ex = AssertRejected(cost, RealRegistry());
            AssertMessageHas(ex, "未知键");
        }

        /// <summary>未知行键 ⇒ 拒(拼写错误会静默丢字段)。</summary>
        [Test]
        public void test_unknownRowKey_isRejected()
        {
            string badRow = "{ \"harm_kind\": \"contraindication\", \"band\": 0, " +
                            "\"injury_id\": \"INJ_HEMORRHAGE\", \"severity_band\": 0, \"lethal\": false }";
            BakeValidationException ex = AssertRejected(
                CostTable(rows: badRow), RegistryWithOneNonLethalInjury());
            AssertMessageHas(ex, "未知键");
        }

        /// <summary>浮点 token ⇒ 结构性拒收(零 float,ADR-006 边界)。</summary>
        [Test]
        public void test_floatSeverityBand_isRejected()
        {
            string badRow = "{ \"harm_kind\": \"contraindication\", \"band\": 0, " +
                            "\"injury_id\": \"INJ_HEMORRHAGE\", \"severity_band\": 0.5 }";
            BakeValidationException ex = AssertRejected(
                CostTable(rows: badRow), RegistryWithOneNonLethalInjury());
            AssertMessageHas(ex, "浮点");
        }

        /// <summary>schema_version 与运行时头契约不符 ⇒ 拒。</summary>
        [Test]
        public void test_schemaVersionMismatch_isRejected()
        {
            BakeValidationException ex = AssertRejected(
                CostTable(schemaVersion: "2"), RealRegistry());
            AssertMessageHas(ex, "schema_version");
        }
    }
}
