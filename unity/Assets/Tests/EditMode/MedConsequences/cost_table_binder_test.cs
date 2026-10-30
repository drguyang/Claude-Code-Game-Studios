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
// 🔁 **2026-10-30 订正(原文不删)**:上段「今日真实注册表 5 件伤情**全部 `lethal = true`**」
//    已**不再成立**。用户裁定新增 `INJ_BLUNT`(自限型 / 非致死,id 15)⇒ 真实注册表**已有**一件
//    合法非致死伤情。连带两处:
//      ① 「真实骨架必报 V-10」的夹具已反转为「**不得**报」,V-10 的正向覆盖改由注入全致死注册表承担
//         (两条合起来才证明「告警有条件」);
//      ② 下文 `RegistryWithOneNonLethalInjury` 的**存在理由**(真实表里造不出合法行)**已消失** ——
//         保留它是因为它同时是 R1-22 形态的活文档,且多条既有夹具仍用它,删除属无收益改动。
//    ⇒ `TD-sim-03` 的缺口由「注册表无材料」收窄为「**表里仍无真实行**」(数值轮 / 53 结算未落)。
//
// ⚠️ 每条拒收夹具都标了它钉的是哪一条判据;反例夹具(应通过)用来防判据过宽。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            => MedConsRepoPaths.RepoRoot;

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
        /// <param name="extraRoot">额外根键片段(**不带尾逗号**),如 <c>"overdose_band_thresholds": ["1"]</c>。</param>
        private static string CostTable(
            string enabled = "[\"contraindication\"]",
            string maxSeverity = "null",
            string rows = "",
            string bands = "{ \"contraindication\": null }",
            string schemaVersion = "1",
            string extraRoot = "")
            => "{ \"schema_version\": " + schemaVersion
             + ", \"harm_kind_enabled\": " + enabled
             + ", \"bands\": " + bands
             + ", \"max_severity_band\": " + maxSeverity
             + (extraRoot.Length > 0 ? ", " + extraRoot.TrimEnd(',', ' ') : "")
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

        /// <summary>真实两张表:绑定出**三行真实代价**(三档阶梯),且不带 V-10 告警。</summary>
        /// <remarks>
        /// <para>钉的是 `ADR-035 §Validation V-10` 的**条件性**:告警只在「注册表零 `lethal == false` 伤情」
        /// 时出现,不是一条恒发的噪声。</para>
        /// <para>🔁 <b>2026-10-30 两次改判</b>(原文不删):① 原夹具断言「真实注册表 ⇒ **必须**告警并点名
        /// `TD-sim-03`」,前提 = 5 件伤情全 `lethal=true`;新增 `INJ_BLUNT` 后该前提已假 ⇒ 断言反转。
        /// ⚠️ 反转不是弱化:V-10 的正向覆盖由
        /// <see cref="test_v10_warning_fires_whenRegistryHasNoNonLethalInjury"/> 接手(注入全致死注册表)。
        /// ② 同日再填真实行(用户裁定三档 2/3/4)⇒ 原断言「`Rows` 为空」**已假**,改为断言:
        /// **三行、档 0..2 连续、每档查得到、且 injury_id 与外键在真实注册表里存在**。
        /// ⚠️ 这三行是**数据**,不是「机制已跑」—— 结算与判定见各自轮次。</para>
        /// </remarks>
        [Test]
        public void test_realTable_bindsThreeBandLadder_andHasNoV10Warning()
        {
            CostTableBinder.BindResult r = CostTableBinder.Bind(RealCostTable(), RealRegistry());

            Assert.That(r.SchemaVersion, Is.EqualTo(1u));

            // 🔁 2026-10-30:过量支解除后真实表 = 3 禁忌档 + 2 过量档 = 5 行。
            //   按 harm_kind 分组断言,不依赖源文件里的行排布 —— 行序是作者态,不该被钉死。
            Assert.That(r.Rows, Has.Count.EqualTo(5), "真实表 = 三禁忌档 + 两过量档");

            List<CostTableBinder.CostRow> contra =
                r.Rows.Where(x => x.HarmKind == "contraindication").ToList();
            Assert.That(contra, Has.Count.EqualTo(3), "禁忌支 = 三档阶梯(档 0..2)");
            Assert.That(contra.Select(x => x.Band), Is.EqualTo(new[] { 0, 1, 2 }), "档序数须自 0 起连续");
            Assert.That(contra.Select(x => x.InjuryId),
                Is.EqualTo(new[] { "INJ_BLUNT", "INJ_DRUG_HARM", "INJ_DRUG_HARM" }),
                "禁忌档映射:0=钝击(轻),1/2=药毒(中/重)");
            Assert.That(contra.Select(x => x.SeverityBand), Is.EqualTo(new[] { 2, 3, 4 }));

            List<CostTableBinder.CostRow> overdose =
                r.Rows.Where(x => x.HarmKind == "overdose").ToList();
            Assert.That(overdose, Has.Count.EqualTo(2), "过量支 = 两档");
            Assert.That(overdose.Select(x => x.Band), Is.EqualTo(new[] { 0, 1 }));
            Assert.That(overdose.Select(x => x.InjuryId),
                Is.EqualTo(new[] { "INJ_DRUG_HARM", "INJ_DRUG_HARM" }), "过量两档均落药毒");
            Assert.That(overdose.Select(x => x.SeverityBand), Is.EqualTo(new[] { 3, 4 }));

            Assert.That(r.OverdoseBandThresholds, Has.Count.EqualTo(2),
                "真实表须带两条过量档位线(档数与 bands.overdose 同源)");
            Assert.That(string.Join(" | ", r.Warnings), Does.Not.Contain("TD-sim-03"),
                "注册表现有非致死伤情 ⇒ 不得再报 V-10(该告警是条件性的);实得:" +
                string.Join(" | ", r.Warnings));
        }

        // ══════════════════════ 一之二、bands × rows 交叉校验(2026-10-30 新立)══════════════════════

        /// <summary>`band ≥ bands[kind]` ⇒ 拒(否则该行永不生效 = 静默跳过)。</summary>
        [Test]
        public void test_bandAtOrAboveDeclaredCount_isRejected()
        {
            BakeValidationException ex = AssertRejected(
                CostTable(bands: "{ \"contraindication\": 2 }",
                          rows: Row(injury: "INJ_BLUNT", band: 2)),
                RealRegistry());

            AssertMessageHas(ex, "bands.contraindication=2");
        }

        /// <summary>反例:**未声明档数**(`bands` 为 `null`)⇒ 不校上界(不得误伤)。</summary>
        [Test]
        public void test_bandsNull_doesNotConstrainBand()
        {
            CostTableBinder.BindResult r = CostTableBinder.Bind(
                CostTable(bands: "{ \"contraindication\": null }",
                          rows: Row(injury: "INJ_BLUNT", band: 9)),
                RealRegistry());

            Assert.That(r.Rows, Has.Count.EqualTo(1), "档数未定 ⇒ 档序数不设上界");
            Assert.That(r.Rows[0].Band, Is.EqualTo(9));
        }

        /// <summary>反例:声明 3 档 ⇒ 档 2 仍合法(上界是**开区间**的右端之外才算越界)。</summary>
        [Test]
        public void test_bandAtDeclaredCountMinusOne_isLegal()
        {
            CostTableBinder.BindResult r = CostTableBinder.Bind(
                CostTable(bands: "{ \"contraindication\": 3 }",
                          rows: Row(injury: "INJ_BLUNT", band: 2)),
                RealRegistry());

            Assert.That(r.Rows, Has.Count.EqualTo(1), "band 2 < 3 应合法");
        }

        /// <summary>V-10 的正向覆盖:注入一张**全 `lethal == true`** 注册表 ⇒ 必须告警并点名 `TD-sim-03`。</summary>
        /// <remarks>
        /// <para>注入手法 = 把真实注册表里**每一件**非致死伤情的 `lethal` 都翻成 `true`
        /// (它们都是自限型,R1-22 对自限型无 `lethal` 约束 ⇒ 翻后仍合法)。</para>
        /// <para>🔁 <b>2026-10-30 订正(原文不删)</b>:原实现只翻 `INJ_BLUNT` 一件。真实表填入三行代价后,
        /// 第 0 行指向 `INJ_BLUNT` ⇒ 翻完立刻被 **N-1** 拒(`BakeValidationException`),
        /// 走不到 V-10 的告警分支(实跑红)。现改为:① **翻遍所有非致死伤情**;② 用**空行**的代价表 ——
        /// 本夹具要证的是「注册表无合法落点 ⇒ 告警」,**有行**的形态由 N-1 负责,两者不该混在一条夹具里。</para>
        /// <para>⚠️ 若将来注册表**没有**任何非致死伤情,本条注入会因「一件都找不到」而断言失败 ——
        /// 那是如实报缺口(前提不成立),不是测试损坏。</para>
        /// </remarks>
        [Test]
        public void test_v10_warning_fires_whenRegistryHasNoNonLethalInjury()
        {
            string json = RealRegistry();
            var nonLethalKeys = new List<string>();
            foreach (string key in new[] { "INJ_BLUNT", "INJ_DRUG_HARM" })
            {
                int atKey = json.IndexOf("\"key\": \"" + key + "\"", StringComparison.Ordinal);
                if (atKey < 0) continue;      // 该件已不在库 ⇒ 不是本夹具的注入点
                nonLethalKeys.Add(key);
                json = ReplaceFirstAfter(json, atKey, "\"lethal\": false", "\"lethal\": true");
            }
            Assert.That(nonLethalKeys, Is.Not.Empty,
                "夹具前提不成立:注册表里一件非致死伤情都没有 —— 全致死注册表无注入点");

            // 空行:本条测的是「注册表侧无合法落点」,不带行 —— 带行会先撞 N-1(见 remarks)。
            CostTableBinder.BindResult r = CostTableBinder.Bind(CostTable(rows: ""), json);

            string joined = string.Join(" | ", r.Warnings);
            Assert.That(joined, Does.Contain("TD-sim-03"),
                "全致死注册表 ⇒ 必须告警并点名 TD-sim-03(V-10);实得:" + joined);
            Assert.That(joined, Does.Contain("无输入"),
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

        /// <summary>🔁 2026-10-30:启用 `overdose` 而**缺** `overdose_band_thresholds` ⇒ 拒。原名
        /// <c>test_overdoseEnabled_isRejected</c>(原判据 = 只要启用就拒,已于 §⑥ 解除后撤销)。</summary>
        /// <remarks>
        /// <para><b>原文(保留)</b>:「§⑥:启用 `overdose` ⇒ 拒(过量支挂起,解除条件是两条债)」。</para>
        /// <para><b>现判据</b>:启用本身合法,但**启用而没有档位线**等于「表里那几行永不生效」——
        /// 与 `ADR-035 §Risks R4` 的静默跳过同型,故仍拒,只是理由换了。</para>
        /// <para>⚠️ 本条的判别力:若把 `ReadOverdoseThresholds` 里那条联动删掉,本测转红
        /// (启用过量 + 无阈值 ⇒ 不再抛)。它**不**覆盖「阈值非递增 / 档数不一致」两条,
        /// 那两条有各自的夹具。</para>
        /// </remarks>
        [Test]
        public void test_overdoseEnabled_withoutThresholds_isRejected()
        {
            BakeValidationException ex = AssertRejected(
                CostTable(enabled: "[\"contraindication\",\"overdose\"]"),
                RegistryWithOneNonLethalInjury());
            AssertMessageHas(ex, "overdose_band_thresholds");
        }

        /// <summary>🔁 2026-10-30:启用 `overdose` **且**给了递增档位线 ⇒ 通过。</summary>
        [Test]
        public void test_overdoseEnabled_withThresholds_isAccepted()
        {
            CostTableBinder.BindResult r = CostTableBinder.Bind(
                CostTable(enabled: "[\"contraindication\",\"overdose\"]",
                          extraRoot: "\"overdose_band_thresholds\": [\"1\", \"3/2\"],",
                          rows: Row(kind: "overdose", band: 0)),
                RegistryWithOneNonLethalInjury());
            Assert.That(r.OverdoseBandThresholds, Has.Count.EqualTo(2), "两条档位线须原样绑定");
            Assert.That(r.OverdoseBandThresholds[1].Raw, Is.GreaterThan(r.OverdoseBandThresholds[0].Raw),
                "须保持升序(构建期已拒非递增)");
        }

        /// <summary>档位线**非严格递增** ⇒ 拒(后者结构性不可达,与 R1-37 的死词条同型)。</summary>
        [Test]
        public void test_overdoseThresholds_notStrictlyIncreasing_isRejected()
        {
            BakeValidationException ex = AssertRejected(
                CostTable(enabled: "[\"contraindication\",\"overdose\"]",
                          extraRoot: "\"overdose_band_thresholds\": [\"1\", \"1\"],"),
                RegistryWithOneNonLethalInjury());
            AssertMessageHas(ex, "严格递增");
        }

        /// <summary>档位线项数与 `bands.overdose` 不一致 ⇒ 拒(某档要么无下界、要么永不命中)。</summary>
        [Test]
        public void test_overdoseThresholdCount_mismatchesBands_isRejected()
        {
            BakeValidationException ex = AssertRejected(
                CostTable(enabled: "[\"contraindication\",\"overdose\"]",
                          bands: "{ \"contraindication\": 3, \"overdose\": 3 }",
                          extraRoot: "\"overdose_band_thresholds\": [\"1\", \"3/2\"],"),
                RegistryWithOneNonLethalInjury());
            AssertMessageHas(ex, "不一致");
        }

        /// <summary>档位线非空但 `harm_kind_enabled` 不含 `overdose` ⇒ 拒(声明了却没人查)。</summary>
        [Test]
        public void test_overdoseThresholdsWithoutEnabledKind_isRejected()
        {
            BakeValidationException ex = AssertRejected(
                CostTable(enabled: "[\"contraindication\"]",
                          extraRoot: "\"overdose_band_thresholds\": [\"1\"],"),
                RegistryWithOneNonLethalInjury());
            AssertMessageHas(ex, "不含");
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
