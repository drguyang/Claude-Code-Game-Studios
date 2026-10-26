// S20-3 · story-001 —— 采集数据装载校验(AC-17-01b / AC-17-08b / AC-17-14 / AC-17-16)。
//
// 权威来源:
//   GDD design/gdd/foraging.md
//     · 规则一 `:126` —— P0 品种闭集 = {柳树皮, 毛地黄, 金鸡纳树皮, 止血草}
//     · F-17-1 `:265-310` + 装载期校验 `:305-311`(CapTable 四性质 · Σw ≥ 1)
//     · F-17-3 `:338-360`(RegrowWindow > 0 · SEASON_MULT 表长 = SEASONS_PER_YEAR)
//     · 边界探针表 `:397-400`(四类非法值 ⇒ 装载期硬失败)
//   story production/epics/foraging/story-001-variety-closure-and-data-load-validation.md
//     AC-17-01b / AC-17-08b / AC-17-14 / AC-17-16(AC-17-05c 是 [L] 人工走查,不在本件)
//   ADR-014 §三/§四(白名单 · FixParse · Fix 字段写字符串)
//   ADR-006 D-21-17(weight / 计数 = int,移出 Fix 解析集)
//
// ⚠️ **两侧都测**:Binder(构建期主校验点)+ Sim 侧 `ForagingConstants`(运行期纵深防御)。
//   两侧判据逐条同源 —— 只测一侧会让另一侧成为「没被任何夹具钉住的第二真源」。
//
// ⚠️ **负面夹具用合成 JSON 字符串**(不碰真实 `assets/data/foraging_constants.json`)——
//   真实表是作者态,测试不得依赖其具体数值(数值归用户数值轮,改值不该让测试红)。
//
// ⚠️ **真实表也有一个测**:`test_realTable_loadsAndSatisfiesAllShapeInvariants` ——
//   它只断**形状**(不数值),故改值不红、改形状才红。这是「表在盘上且合法」的唯一夹具。
//
// ⚠️ **AC-17-01b 不 Ignore**:21a 的四品种行已于 2026-10-26 落地并重烘
//   (`item_database_items.json` 6 → 9 条;cooked 604B → 1289B;测试 `384/384/0/0`)。
//   卡面原写的 `Ignore("BLOCKED-BY: 21a")` 前置已解除,本件按真断言落地。

using System;
using System.Collections.Generic;
using System.IO;
using DaYiJingCheng.EditorTools.Bake;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.Foraging;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Unit.Foraging
{
    /// <summary>采集调参表的装载校验(AC-17-08b / 14 / 16 + P0 品种闭集 AC-17-01b)。</summary>
    [TestFixture]
    public class foraging_data_load_test
    {
        // ══════════════════════════════════════════════════════════════
        // 夹具:一份合法的基线 JSON,各负面用例只改一处
        // ══════════════════════════════════════════════════════════════

        // ⚠️ 合成夹具专用常量(评审 M-2):**只**用于合成 JSON 的负面/正例构造,
        //    **不得**用于「真实表 × 真实 21a 常量」的测试 —— 那条走 Read21aMaxQuality()。
        //    两处混用的后果:「21a 把 MAX_QUALITY 从 5 改成 6」会以**错误理由**红(归因错)。
        private const int MaxQuality = 5;      // 合成夹具的品级上界(非生产值来源)
        private const int SeasonsPerYear = 5;  // 5 的 SEASONS_PER_YEAR

        /// <summary>合法基线:61 项 CapTable(字面式 1+min(L,20)/20)+ 5 项 SEASON_MULT。</summary>
        private static string BaselineJson()
        {
            var caps = new List<string>(61);
            for (int level = 0; level < 61; level++)
                caps.Add((1 + Math.Min(level, 20) / 20).ToString());

            return "{" +
                   "\"schema_version\": 1," +
                   "\"seasons_per_year\": " + SeasonsPerYear + "," +
                   "\"cap_table\": [" + string.Join(", ", caps) + "]," +
                   "\"season_mult\": [\"1\", \"5/4\", \"3/4\", \"3/2\", \"3/4\"]," +
                   "\"regrow_window_ticks\": 1200," +
                   "\"base_capacity\": 5," +
                   "\"decay_rate\": \"1/40\"," +
                   "\"decay_floor\": \"1/2\"," +
                   "\"gather_mul_cap\": \"3\"" +
                   "}";
        }

        /// <summary>对基线做一次字符串替换(负面夹具的唯一构造手段)。</summary>
        private static string Mutate(string from, string to)
        {
            string baseline = BaselineJson();
            Assert.That(baseline.Contains(from), Is.True,
                $"夹具自检:基线中必须含锚串「{from}」—— 否则本变异打空靶");
            return baseline.Replace(from, to);
        }

        /// <summary>
        /// 把基线的 <c>cap_table</c> 数组整体换掉(负面夹具用)。
        /// <para>⚠️ 用**定位 + 拼接**而非字面锚串:基线是 61 项生成值,写死锚串会在
        /// 「改基线生成式」时静默打空靶(本夹具首版即栽在此 —— 自检捕获)。</para>
        /// </summary>
        private static string MutateCapTable(string newArrayBody)
        {
            string baseline = BaselineJson();
            const string open = "\"cap_table\": [";
            int start = baseline.IndexOf(open, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), "夹具自检:基线须含 cap_table 键");
            int bodyStart = start + open.Length;
            int close = baseline.IndexOf(']', bodyStart);
            Assert.That(close, Is.GreaterThan(bodyStart), "夹具自检:基线须含 cap_table 闭合括号");

            return baseline.Substring(0, bodyStart) + newArrayBody + baseline.Substring(close);
        }

        /// <summary>断言 Binder 抛 <see cref="BakeValidationException"/> 且消息含指定片段。</summary>
        private static void AssertBinderRejects(string json, string expectedFragment, string because)
        {
            var ex = Assert.Throws<BakeValidationException>(
                () => ForagingConstantsBinder.Bind(json, MaxQuality), because);
            Assert.That(ex.Message, Does.Contain(expectedFragment),
                $"{because} —— 异常消息须点名字段,实得:{ex.Message}");
        }

        // ══════════════════════════════════════════════════════════════
        // 评审 M-4 · 缺键只报一次(RequiredKeys 是唯一登记处)
        // ⚠️ 本组钉的是「同一缺键不得在错误列表里出现两次」——
        //    M-4 之前 ReadRequiredXxx 与 RequiredKeys 循环各报一遍,
        //    一轮修完的可用性被破坏(错误数虚高,人会以为有多处问题)。
        // ══════════════════════════════════════════════════════════════

        [Test]
        public void test_m4_missingRequiredKey_reportedExactlyOnce()
        {
            // 把 base_capacity 整行删掉(含前后逗号,保持 JSON 合法)
            string json = Mutate("\"base_capacity\": 5,", "");

            var ex = Assert.Throws<BakeValidationException>(
                () => ForagingConstantsBinder.Bind(json, MaxQuality),
                "缺必填键 ⇒ 构建期硬失败");

            int hits = 0;
            foreach (string e in ex.Errors)
                if (e.Contains("base_capacity", StringComparison.Ordinal)) hits++;

            Assert.That(hits, Is.EqualTo(1),
                "同一缺键须**恰报一次**(M-4:RequiredKeys 是唯一登记处,ReadRequiredXxx 缺失时静默)。" +
                $"实得 {hits} 条。全部错误:{string.Join(" | ", ex.Errors)}");
        }

        [Test]
        public void test_m4_missingMultipleKeys_allReportedInOnePass()
        {
            // 一次删两个必填键 —— 钉住「一次报全,便于一轮修完」不被 M-4 的改动破坏
            string json = Mutate("\"base_capacity\": 5,", "")
                              .Replace("\"regrow_window_ticks\": 1200,", "");

            var ex = Assert.Throws<BakeValidationException>(
                () => ForagingConstantsBinder.Bind(json, MaxQuality),
                "缺多个必填键 ⇒ 一次报全");

            var missing = new List<string>();
            foreach (string e in ex.Errors)
                if (e.Contains("缺必填键", StringComparison.Ordinal)) missing.Add(e);

            Assert.That(missing.Count, Is.EqualTo(2),
                "两个缺键须在**同一轮**各报一次(一次报全)。" +
                $"实得 {missing.Count} 条。全部错误:{string.Join(" | ", ex.Errors)}");
            Assert.That(string.Join(" | ", missing), Does.Contain("base_capacity"));
            Assert.That(string.Join(" | ", missing), Does.Contain("regrow_window_ticks"));
        }

        // ══════════════════════════════════════════════════════════════
        // AC-17-14 · CapTable 四性质(长度 61 / 单调不减 / [0] ≥ 1 / 各值 ≤ MAX_QUALITY)
        // ══════════════════════════════════════════════════════════════

        [Test]
        public void test_ac1714_baselineCapTable_passesAllFourInvariants()
        {
            // Given:合法基线(61 项,单调不减,全 ∈ [1, MAX_QUALITY])
            // When:绑定
            ForagingConstantsBinder.BindResult r = ForagingConstantsBinder.Bind(BaselineJson(), MaxQuality);

            // Then:四条形状判据逐条成立
            Assert.That(r.CapTable.Length, Is.EqualTo(61), "① 长度须 = SKILL_CAP + 1 = 61");
            for (int i = 1; i < r.CapTable.Length; i++)
                Assert.That(r.CapTable[i], Is.GreaterThanOrEqualTo(r.CapTable[i - 1]),
                    $"② 单调不减:cap_table[{i}] ≥ cap_table[{i - 1}]");
            Assert.That(r.CapTable[0], Is.GreaterThanOrEqualTo(1), "③ CapTable[0] ≥ 1");
            foreach (int v in r.CapTable)
                Assert.That(v, Is.LessThanOrEqualTo(MaxQuality), "④ 各值 ≤ MAX_QUALITY");
        }

        [Test]
        public void test_ac1714_capTableLength60_rejected()
        {
            // 长度 60(少一档)—— 必须硬失败,不是越界读
            var caps = new List<string>(60);
            for (int level = 0; level < 60; level++) caps.Add("1");
            string json = "{" +
                          "\"schema_version\": 1,\"seasons_per_year\": 5," +
                          "\"cap_table\": [" + string.Join(", ", caps) + "]," +
                          "\"season_mult\": [\"1\", \"1\", \"1\", \"1\", \"1\"]," +
                          "\"regrow_window_ticks\": 1200,\"base_capacity\": 5," +
                          "\"decay_rate\": \"1/40\",\"decay_floor\": \"1/2\",\"gather_mul_cap\": \"3\"}";

            AssertBinderRejects(json, "cap_table 长度 60",
                "AC-17-14:长度 ≠ 61 ⇒ 装载期硬失败");
        }

        [Test]
        public void test_ac1714_capTableNonMonotonic_rejected()
        {
            // 单调破坏:第 30 档比第 29 档小
            string json = MutateCapTable(CapsWithDip());

            AssertBinderRejects(json, "非单调不减",
                "AC-17-14:非单调 = 升级反而采到更差(直接出戏)");
        }

        [Test]
        public void test_ac1714_capTableZero_rejected()
        {
            // CapTable[0] = 0 ⇒ clamp(x, 1, 0) 抛异常(min > max)
            string json = MutateCapTable(CapsText(0));

            AssertBinderRejects(json, "cap_table[0] = 0 < 1",
                "AC-17-14:CapTable[0] = 0 会让 clamp(x, 1, 0) 抛异常");
        }

        [Test]
        public void test_ac1714_capTableOverMaxQuality_rejected()
        {
            // 某档 > MAX_QUALITY ⇒ out_quality 越界读
            string json = MutateCapTable(CapsText(1, overAt: 60));

            AssertBinderRejects(json, "> MAX_QUALITY",
                "AC-17-14 / D-21-7:各值 ≤ 21a 的 MAX_QUALITY");
        }

        /// <summary>构造 61 项全同值的 CapTable 文本(可选某档写越界值)。</summary>
        private static string CapsText(int value, int overAt = -1)
        {
            var caps = new List<string>(61);
            for (int i = 0; i < 61; i++) caps.Add(i == overAt ? (MaxQuality + 1).ToString() : value.ToString());
            return string.Join(", ", caps);
        }

        /// <summary>构造 61 项、在第 30 档处下陷的 CapTable 文本。</summary>
        private static string CapsWithDip()
        {
            var caps = new List<string>(61);
            for (int i = 0; i < 61; i++) caps.Add(i == 30 ? "1" : "2");
            return string.Join(", ", caps);
        }

        // ══════════════════════════════════════════════════════════════
        // AC-17-16 · SEASON_MULT 表长 = SEASONS_PER_YEAR
        // ══════════════════════════════════════════════════════════════

        [Test]
        public void test_ac1716_seasonMultLengthMatchesSeasonsPerYear()
        {
            // 正例:长度 5 == SEASONS_PER_YEAR
            ForagingConstantsBinder.BindResult r = ForagingConstantsBinder.Bind(BaselineJson(), MaxQuality);
            Assert.That(r.SeasonMult.Length, Is.EqualTo(SeasonsPerYear));
            Assert.That(r.SeasonsPerYear, Is.EqualTo(SeasonsPerYear));
        }

        [Test]
        public void test_ac1716_seasonMultTooShort_rejected()
        {
            // 长度 4(少一季)⇒ 硬失败,不是越界读
            string json = Mutate(
                "\"season_mult\": [\"1\", \"5/4\", \"3/4\", \"3/2\", \"3/4\"]",
                "\"season_mult\": [\"1\", \"5/4\", \"3/4\", \"3/2\"]");

            AssertBinderRejects(json, "season_mult 长度 4",
                "AC-17-16:表长 ≠ SEASONS_PER_YEAR ⇒ 装载期硬失败");
        }

        [Test]
        public void test_ac1716_seasonMultTooLong_rejected()
        {
            // 长度 6(多一季)⇒ 同样硬失败(方向无关)
            string json = Mutate(
                "\"season_mult\": [\"1\", \"5/4\", \"3/4\", \"3/2\", \"3/4\"]",
                "\"season_mult\": [\"1\", \"5/4\", \"3/4\", \"3/2\", \"3/4\", \"1\"]");

            AssertBinderRejects(json, "season_mult 长度 6",
                "AC-17-16:表长 ≠ SEASONS_PER_YEAR(两个方向都拒)");
        }

        // ══════════════════════════════════════════════════════════════
        // AC-17-08b · 非法值装载硬失败(四类负面夹具,均须显式 throw)
        // ══════════════════════════════════════════════════════════════

        [Test]
        public void test_ac1708b_regrowWindowZero_rejected()
        {
            string json = Mutate("\"regrow_window_ticks\": 1200", "\"regrow_window_ticks\": 0");

            AssertBinderRejects(json, "regrow_window_ticks = 0",
                "AC-17-08b / GDD :400:RegrowWindow ≤ 0 ⇒ 无限采集");
        }

        [Test]
        public void test_ac1708b_decayFloorAboveOne_rejected()
        {
            string json = Mutate("\"decay_floor\": \"1/2\"", "\"decay_floor\": \"3/2\"");

            AssertBinderRejects(json, "decay_floor raw",
                "F-17-3:DECAY_FLOOR > 1 会让衰减项反而放大");
        }

        [Test]
        public void test_ac1708b_gatherMulCapBelowOne_rejected()
        {
            string json = Mutate("\"gather_mul_cap\": \"3\"", "\"gather_mul_cap\": \"1/2\"");

            AssertBinderRejects(json, "gather_mul_cap raw",
                "F-17-2:GATHER_MUL_CAP < 1 会让高技能反而采得更少(双重惩罚)");
        }

        // ══════════════════════════════════════════════════════════════
        // Binder 侧补件 · 证伪轮 M10 暴露的分支缺口(S20-3 第一轮)
        //
        // ⚠️ **为什么补**:M10 停用 `if (df.Raw <= 0 || df.Raw > Fix.OneRaw)` **整条**守卫,
        //   结果只有 `test_ac1708b_decayFloorAboveOne_rejected` 转红 —— **下界分支
        //   (`df.Raw <= 0`)零覆盖**。这是「一属性一条变异」的具体失效形态:
        //   合取判据只钉住了一半,另一半停用了也没人喊。
        // ⚠️ 顺带补 `decay_rate <= 0` / `seasons_per_year < 1` / `season_mult[i] <= 0`
        //   —— 三条 Binder 侧守卫在证伪轮前同样**零夹具**。
        // ══════════════════════════════════════════════════════════════

        [Test]
        public void test_ac1708b_decayFloorZero_rejected()
        {
            // DECAY_FLOOR = 0 ⇒ 节点触底后归零(采枯竭)。**区间下界分支**
            // ⚠️ 与 test_ac1708b_decayFloorAboveOne_rejected 是两个独立分支,
            //    单靠那一条会让「≤ 0」这半边永远不被执行到。
            string json = Mutate("\"decay_floor\": \"1/2\"", "\"decay_floor\": \"0\"");

            AssertBinderRejects(json, "decay_floor raw = 0",
                "F-17-3:DECAY_FLOOR ≤ 0 会让节点触底后归零(采枯竭)");
        }

        [Test]
        public void test_ac1708b_decayRateZero_rejected()
        {
            // DECAY_RATE = 0 ⇒ yieldDecay 恒为 1(深水线消失,退化成浅水线)
            string json = Mutate("\"decay_rate\": \"1/40\"", "\"decay_rate\": \"0\"");

            AssertBinderRejects(json, "decay_rate raw = 0",
                "F-17-3:DECAY_RATE ≤ 0 会让 yieldDecay 恒为 1(深水线消失)");
        }

        [Test]
        public void test_adr014_seasonsPerYearZero_rejected()
        {
            // 5 的 SEASONS_PER_YEAR = 0 ⇒ 17 装载即失败(而非越界读)
            string json = Mutate("\"seasons_per_year\": 5", "\"seasons_per_year\": 0");

            AssertBinderRejects(json, "seasons_per_year = 0 < 1",
                "5 的 SEASONS_PER_YEAR 非法 ⇒ 17 装载硬失败");
        }

        [Test]
        public void test_ac1716_seasonMultNonPositive_rejected()
        {
            // 季节系数 = 0 ⇒ 该季节点容量归零(F-17-3 量纲齐一)
            string json = Mutate(
                "\"season_mult\": [\"1\", \"5/4\", \"3/4\", \"3/2\", \"3/4\"]",
                "\"season_mult\": [\"1\", \"5/4\", \"0\", \"3/2\", \"3/4\"]");

            AssertBinderRejects(json, "season_mult[2] raw = 0",
                "F-17-3:季节系数非正会让该季节点容量归零或为负");
        }

        [Test]
        public void test_ac1708b_baseCapacityZero_rejected()
        {
            string json = Mutate("\"base_capacity\": 5", "\"base_capacity\": 0");

            AssertBinderRejects(json, "base_capacity = 0",
                "F-17-3:BaseCapacity 须 > 0");
        }

        // ══════════════════════════════════════════════════════════════
        // ADR-014 §四 · Fix 字段必须字符串(数值 token 结构性硬拒)
        // ══════════════════════════════════════════════════════════════

        [Test]
        public void test_adr014_fixFieldAsJsonNumber_rejected()
        {
            // decay_rate 写成 JSON 数字 0.025 ⇒ 词法器已把它转成 CLR 基元(浮点泄漏)
            string json = Mutate("\"decay_rate\": \"1/40\"", "\"decay_rate\": 0.025");

            AssertBinderRejects(json, "decay_rate 须为 JSON 字符串",
                "ADR-014 §四:Fix 字段必须字符串,数值 token 即浮点泄漏");
        }

        [Test]
        public void test_adr014_unknownKey_rejected()
        {
            // 白名单外的键(含 quality_distribution —— 它住 21a,写进本表 = 造第二真源)
            string json = Mutate(
                "\"gather_mul_cap\": \"3\"",
                "\"gather_mul_cap\": \"3\", \"quality_distribution\": {}");

            AssertBinderRejects(json, "未登记键 \"quality_distribution\"",
                "ADR-014 §三 白名单外 ⇒ 硬失败;quality_distribution 住 21a(GDD 六处一致)");
        }

        // ══════════════════════════════════════════════════════════════
        // Sim 侧纵深防御(ForagingConstants 构造期拒收 —— 与 Binder 判据同源)
        // ══════════════════════════════════════════════════════════════

        [Test]
        public void test_simSide_capTableLength60_rejected()
        {
            // 绕过 Binder 直构表(模拟「产物被改」/「绕过 Bake」)
            var caps = new int[60];
            for (int i = 0; i < 60; i++) caps[i] = 1;

            var ex = Assert.Throws<ArgumentException>(() => new ForagingConstants(
                caps, LegalSeasonMult(), SeasonsPerYear, 1200, 5,
                FixParse.Parse("1/40"), FixParse.Parse("1/2"), FixParse.Parse("3"), MaxQuality));
            Assert.That(ex.Message, Does.Contain("CapTable 长度 60"));
        }

        [Test]
        public void test_simSide_regrowWindowZero_rejected()
        {
            var ex = Assert.Throws<ArgumentException>(() => new ForagingConstants(
                LegalCapTable(), LegalSeasonMult(), SeasonsPerYear, 0, 5,
                FixParse.Parse("1/40"), FixParse.Parse("1/2"), FixParse.Parse("3"), MaxQuality));
            Assert.That(ex.Message, Does.Contain("RegrowWindow = 0"));
        }

        [Test]
        public void test_simSide_seasonMultLengthMismatch_rejected()
        {
            // 长度 4 而 seasonsPerYear = 5
            var sm = new[] { FixParse.Parse("1"), FixParse.Parse("1"), FixParse.Parse("1"), FixParse.Parse("1") };
            var ex = Assert.Throws<ArgumentException>(() => new ForagingConstants(
                LegalCapTable(), sm, SeasonsPerYear, 1200, 5,
                FixParse.Parse("1/40"), FixParse.Parse("1/2"), FixParse.Parse("3"), MaxQuality));
            Assert.That(ex.Message, Does.Contain("SEASON_MULT 长度 4"));
        }

        // ══════════════════════════════════════════════════════════════
        // Sim 侧纵深防御 · 全守卫覆盖(S20-3 证伪轮 M1/M2/M3 暴露的缺口补件)
        //
        // ⚠️ **为什么必须逐条补**:证伪轮把 Sim 侧构造器每条守卫逐个停用,实测
        //   M1(`value < 1`)/ M2(`value > maxQuality`)/ M3(单调)三条 **RED = 0** ——
        //   因为 `test_ac1714_*` 系列测的是 **Binder 侧**,而这三条守卫在 **Sim 侧构造器**。
        //   文件头已声明「两侧都测,只测一侧会让另一侧成为没被钉住的第二真源」,
        //   本组夹具即兑现该声明。**每条守卫一个夹具**(含 `decayFloor` 的两分支各一条)。
        // ══════════════════════════════════════════════════════════════

        [Test]
        public void test_simSide_capTableNull_rejected()
        {
            var ex = Assert.Throws<ArgumentException>(() => new ForagingConstants(
                null, LegalSeasonMult(), SeasonsPerYear, 1200, 5,
                FixParse.Parse("1/40"), FixParse.Parse("1/2"), FixParse.Parse("3"), MaxQuality));
            Assert.That(ex.Message, Does.Contain("CapTable 为 null"));
        }

        [Test]
        public void test_simSide_maxQualityBelowOne_rejected()
        {
            // MAX_QUALITY = 0 ⇒ CapTable 各值的上界非法
            var ex = Assert.Throws<ArgumentException>(() => new ForagingConstants(
                LegalCapTable(), LegalSeasonMult(), SeasonsPerYear, 1200, 5,
                FixParse.Parse("1/40"), FixParse.Parse("1/2"), FixParse.Parse("3"), 0));
            Assert.That(ex.Message, Does.Contain("MAX_QUALITY = 0 < 1"));
        }

        [Test]
        public void test_simSide_capTableValueZero_rejected()
        {
            // AC-17-14 ③:CapTable[0] = 0 ⇒ clamp(x, 1, 0) 抛异常(min > max)
            // ⚠️ 本测钉的是 **Sim 侧构造器**(M1 变异靶);Binder 侧同源判据由
            //    test_ac1714_capTableZero_rejected 钉住 —— 两条不可互相替代。
            var caps = LegalCapTable();
            caps[0] = 0;

            var ex = Assert.Throws<ArgumentException>(() => new ForagingConstants(
                caps, LegalSeasonMult(), SeasonsPerYear, 1200, 5,
                FixParse.Parse("1/40"), FixParse.Parse("1/2"), FixParse.Parse("3"), MaxQuality));
            Assert.That(ex.Message, Does.Contain("CapTable[0] = 0 < 1"));
        }

        [Test]
        public void test_simSide_capTableValueOverMaxQuality_rejected()
        {
            // AC-17-14 ④ / D-21-7:某档 > MAX_QUALITY ⇒ out_quality 越界读
            var caps = LegalCapTable();
            caps[60] = MaxQuality + 1;

            var ex = Assert.Throws<ArgumentException>(() => new ForagingConstants(
                caps, LegalSeasonMult(), SeasonsPerYear, 1200, 5,
                FixParse.Parse("1/40"), FixParse.Parse("1/2"), FixParse.Parse("3"), MaxQuality));
            Assert.That(ex.Message, Does.Contain($"> MAX_QUALITY = {MaxQuality}"));
        }

        [Test]
        public void test_simSide_capTableNonMonotonic_rejected()
        {
            // AC-17-14 ②:第 30 档比第 29 档小 ⇒ 升级反而采到更差
            var caps = LegalCapTable();
            caps[30] = 1;   // LegalCapTable 的 [29] = 2

            var ex = Assert.Throws<ArgumentException>(() => new ForagingConstants(
                caps, LegalSeasonMult(), SeasonsPerYear, 1200, 5,
                FixParse.Parse("1/40"), FixParse.Parse("1/2"), FixParse.Parse("3"), MaxQuality));
            Assert.That(ex.Message, Does.Contain("CapTable 非单调不减"));
        }

        [Test]
        public void test_simSide_seasonsPerYearBelowOne_rejected()
        {
            // 5 的 SEASONS_PER_YEAR 非法 ⇒ 17 装载即失败(而非两处各写一个 5)
            var ex = Assert.Throws<ArgumentException>(() => new ForagingConstants(
                LegalCapTable(), LegalSeasonMult(), 0, 1200, 5,
                FixParse.Parse("1/40"), FixParse.Parse("1/2"), FixParse.Parse("3"), MaxQuality));
            Assert.That(ex.Message, Does.Contain("seasons_per_year = 0 < 1"));
        }

        [Test]
        public void test_simSide_seasonMultNull_rejected()
        {
            var ex = Assert.Throws<ArgumentException>(() => new ForagingConstants(
                LegalCapTable(), null, SeasonsPerYear, 1200, 5,
                FixParse.Parse("1/40"), FixParse.Parse("1/2"), FixParse.Parse("3"), MaxQuality));
            Assert.That(ex.Message, Does.Contain("SEASON_MULT 为 null"));
        }

        [Test]
        public void test_simSide_seasonMultNonPositive_rejected()
        {
            // 季节系数 = 0 ⇒ 该季节点容量归零(F-17-3 量纲齐一)
            var sm = LegalSeasonMult();
            sm[2] = Fix.Zero;

            var ex = Assert.Throws<ArgumentException>(() => new ForagingConstants(
                LegalCapTable(), sm, SeasonsPerYear, 1200, 5,
                FixParse.Parse("1/40"), FixParse.Parse("1/2"), FixParse.Parse("3"), MaxQuality));
            Assert.That(ex.Message, Does.Contain("SEASON_MULT[2] raw = 0"));
        }

        [Test]
        public void test_simSide_baseCapacityZero_rejected()
        {
            var ex = Assert.Throws<ArgumentException>(() => new ForagingConstants(
                LegalCapTable(), LegalSeasonMult(), SeasonsPerYear, 1200, 0,
                FixParse.Parse("1/40"), FixParse.Parse("1/2"), FixParse.Parse("3"), MaxQuality));
            Assert.That(ex.Message, Does.Contain("BaseCapacity = 0"));
        }

        [Test]
        public void test_simSide_decayRateZero_rejected()
        {
            // DECAY_RATE = 0 ⇒ yieldDecay 恒为 1(深水线消失)
            var ex = Assert.Throws<ArgumentException>(() => new ForagingConstants(
                LegalCapTable(), LegalSeasonMult(), SeasonsPerYear, 1200, 5,
                Fix.Zero, FixParse.Parse("1/2"), FixParse.Parse("3"), MaxQuality));
            Assert.That(ex.Message, Does.Contain("DECAY_RATE raw = 0"));
        }

        [Test]
        public void test_simSide_decayFloorZero_rejected()
        {
            // DECAY_FLOOR = 0 ⇒ 节点触底后归零(采枯竭)。**区间下界分支**。
            var ex = Assert.Throws<ArgumentException>(() => new ForagingConstants(
                LegalCapTable(), LegalSeasonMult(), SeasonsPerYear, 1200, 5,
                FixParse.Parse("1/40"), Fix.Zero, FixParse.Parse("3"), MaxQuality));
            Assert.That(ex.Message, Does.Contain("DECAY_FLOOR raw = 0"));
        }

        [Test]
        public void test_simSide_decayFloorAboveOne_rejected()
        {
            // DECAY_FLOOR = 3/2 > 1 ⇒ 衰减项反而放大。**区间上界分支**。
            // ⚠️ 与上一条是两个独立分支 —— 「一属性一条变异」要求两分支各有一条反证。
            var ex = Assert.Throws<ArgumentException>(() => new ForagingConstants(
                LegalCapTable(), LegalSeasonMult(), SeasonsPerYear, 1200, 5,
                FixParse.Parse("1/40"), FixParse.Parse("3/2"), FixParse.Parse("3"), MaxQuality));
            Assert.That(ex.Message, Does.Contain("DECAY_FLOOR raw ="));
        }

        [Test]
        public void test_simSide_gatherMulCapBelowOne_rejected()
        {
            // GATHER_MUL_CAP = 1/2 < 1 ⇒ 高技能反而采得更少(双重惩罚)
            var ex = Assert.Throws<ArgumentException>(() => new ForagingConstants(
                LegalCapTable(), LegalSeasonMult(), SeasonsPerYear, 1200, 5,
                FixParse.Parse("1/40"), FixParse.Parse("1/2"), FixParse.Parse("1/2"), MaxQuality));
            Assert.That(ex.Message, Does.Contain("GATHER_MUL_CAP raw ="));
        }

        // ══════════════════════════════════════════════════════════════
        // 索引前守卫(AC-17-16 的「非越界读」判据)
        // ══════════════════════════════════════════════════════════════

        [Test]
        public void test_qualityCapAt_outOfRange_throwsInsteadOfReading()
        {
            ForagingConstants c = LegalConstants();
            Assert.Throws<ArgumentOutOfRangeException>(() => c.QualityCapAt(-1), "等级 −1 越界");
            Assert.Throws<ArgumentOutOfRangeException>(() => c.QualityCapAt(61), "等级 61 越界(上界 = 60)");
            Assert.That(c.QualityCapAt(0), Is.GreaterThanOrEqualTo(1), "边界:等级 0 合法");
            Assert.That(c.QualityCapAt(60), Is.LessThanOrEqualTo(MaxQuality), "边界:等级 60 合法");
        }

        [Test]
        public void test_seasonMultiplierAt_outOfRange_throwsInsteadOfReading()
        {
            ForagingConstants c = LegalConstants();
            Assert.Throws<ArgumentOutOfRangeException>(() => c.SeasonMultiplierAt(-1), "季节 −1 越界");
            Assert.Throws<ArgumentOutOfRangeException>(() => c.SeasonMultiplierAt(5), "季节 5 越界(上界 = 4)");
            Assert.That(c.SeasonMultiplierAt(0).Raw, Is.GreaterThan(0), "边界:季节 0 合法");
            Assert.That(c.SeasonMultiplierAt(4).Raw, Is.GreaterThan(0), "边界:季节 4 合法");
        }

        // ══════════════════════════════════════════════════════════════
        // 真实表在盘上且形状合法(只断形状,不断数值 —— 数值归用户轮)
        // ══════════════════════════════════════════════════════════════

        [Test]
        public void test_realTable_loadsAndSatisfiesAllShapeInvariants()
        {
            // ⚠️ MAX_QUALITY 从 21a 的**真 binder**读,不用夹具常量 5(评审 M-2)。
            //    理由:本测断的是「真实表 × 真实 21a 常量」这一对能否装载;
            //    用夹具常量会让「21a 把 MAX_QUALITY 从 5 改成 6」以**错误理由**红
            //    (`cap_table` 各值 > 6 不成立,但报的是夹具常量不匹配)—— 归因错。
            int maxQuality21a = Read21aMaxQuality();

            ForagingConstantsBinder.BindResult r =
                ForagingConstantsBinder.Bind(ReadRepoData(ForagingConstantsBinder.FileName), maxQuality21a);

            // 只断形状(改数值不该让本测红)
            Assert.That(r.CapTable.Length, Is.EqualTo(61), "真实表 CapTable 长度须 61");
            Assert.That(r.SeasonMult.Length, Is.EqualTo(r.SeasonsPerYear),
                "真实表 SEASON_MULT 长度须 = seasons_per_year");
            Assert.That(r.RegrowWindowTicks, Is.GreaterThan(0), "真实表 RegrowWindow 须 > 0");
            Assert.That(r.DecayFloor.Raw, Is.InRange(1L, Fix.OneRaw), "真实表 DECAY_FLOOR ∈ (0, 1]");
        }

        /// <summary>
        /// 从 21a 的 `item_database_constants.json` 读 `max_quality`(经 21a 真 binder,不另写解析)。
        /// <para>⚠️ 本方法是 M-2 的兑现件:测试对 21a 常量的依赖**走 21a 自己的绑定路径**,
        /// 而不是在测试里复刻一份读数逻辑 —— 复刻即第二真源。</para>
        /// </summary>
        private static int Read21aMaxQuality()
        {
            string itemsJson = ReadRepoData(ItemDatabaseBaker.ItemsFileName);
            string recipesJson = ReadRepoData(ItemDatabaseBaker.RecipesFileName);
            string constantsJson = ReadRepoData(ItemDatabaseBaker.ConstantsFileName);

            BindPackage pkg =
                ItemDatabaseBinder.BindAll(
                    Lex(itemsJson, ItemDatabaseBaker.ItemsFileName),
                    Lex(recipesJson, ItemDatabaseBaker.RecipesFileName),
                    Lex(constantsJson, ItemDatabaseBaker.ConstantsFileName));

            Assert.That(pkg.Errors, Is.Empty,
                "21a 三表须无绑定错误(MAX_QUALITY 读不出来):" + string.Join(" | ", pkg.Errors));
            Assert.That(pkg.Constants, Is.Not.Null, "21a constants 段缺失");

            // RawMaxQuality 是字符串(21a 允许字面形 "4" / 字符串形 "4")—— 此处是**测试**,
            // 用 int.TryParse 读它不构成「生产路径经浮点中转」(生产路径 = 21a 的 ValidateMaxQuality 门)。
            Assert.That(int.TryParse(pkg.Constants.RawMaxQuality, out int mq), Is.True,
                $"21a max_quality = \"{pkg.Constants.RawMaxQuality}\" 不是整数");
            Assert.That(mq, Is.GreaterThan(0), $"21a max_quality = {mq} 非法");
            return mq;
        }

        // ══════════════════════════════════════════════════════════════
        // AC-17-01b · P0 品种闭集 = 四品种(21a item_key 行已落地)
        // ══════════════════════════════════════════════════════════════

        [Test]
        public void test_ac1701b_p0VarietySet_matches21aRawMaterials()
        {
            // Given:17 侧的 P0 品种闭集(GDD 规则一 :126)
            string[] p0 = { "willow_bark", "foxglove", "cinchona_bark", "yarrow" };

            // When:走 21a 的**真 binder** 绑定三份源表(与生产烘焙同一条路径,
            //       不另写一份 JSON 解析 —— 那样断的是「我的解析器」不是「21a 的表」)。
            string itemsJson = ReadRepoData(ItemDatabaseBaker.ItemsFileName);
            string recipesJson = ReadRepoData(ItemDatabaseBaker.RecipesFileName);
            string constantsJson = ReadRepoData(ItemDatabaseBaker.ConstantsFileName);

            BindPackage pkg =
                ItemDatabaseBinder.BindAll(
                    Lex(itemsJson, ItemDatabaseBaker.ItemsFileName),
                    Lex(recipesJson, ItemDatabaseBaker.RecipesFileName),
                    Lex(constantsJson, ItemDatabaseBaker.ConstantsFileName));

            Assert.That(pkg.Errors, Is.Empty,
                "21a 三表须无绑定错误:" + string.Join(" | ", pkg.Errors));

            // 取 category=Material ∧ state=Raw ∧ gather_profile 非空 的行
            var actual = new List<string>();
            foreach (BoundItem bi in pkg.Items)
            {
                if (bi.Def.Category != ItemCategory.Material) continue;
                if (bi.Def.ProcessingState != ProcessingState.Raw) continue;
                if (bi.Def.GatherProfile == null) continue;
                actual.Add(bi.Def.BaseId);
            }

            // Then:两集合完全相等(无缺、无多)
            Assert.That(actual.Count, Is.EqualTo(p0.Length),
                $"21a 的可采品种数须 = {p0.Length},实得 {actual.Count}:{string.Join(", ", actual)}");
            foreach (string v in p0)
                Assert.That(actual, Does.Contain(v),
                    $"AC-17-01b:21a 缺品种「{v}」的 gather_profile 行(四品种闭集,规则一 :126)");
            foreach (string v in actual)
                Assert.That(p0, Does.Contain(v),
                    $"AC-17-01b:21a 多出品种「{v}」—— 第 5 条目混入即失败");
        }

        /// <summary>走 21a 的**阶段 1 词法器**(与生产烘焙同一条路径)。</summary>
        private static JsonNode Lex(string json, string fileName)
        {
            Assert.That(JsonStage1Lexer.TryParse(json, out JsonNode root, out string err), Is.True,
                $"{fileName} 词法失败:{err}");
            return root;
        }

        // ══════════════════════════════════════════════════════════════
        // AC-17-08b 的 Σw 半边 · quality_distribution 形状校验(F-17-1 装载期校验 ①)
        // ⚠️ 本组是**唯一**能钉住 Σw ≥ 1 的夹具 —— 生产数据侧目前无该字段
        //    (21a 四行 gather_profile 均不含;binder 拒收其任何字段)。见 QualityDistributionShape 文件头。
        // ══════════════════════════════════════════════════════════════

        [Test]
        public void test_ac1708b_qualityDistribution_allZero_rejected()
        {
            // GDD :397 边界探针:[0, 0, 0, 0, 0] ⇒ 装载期硬失败(CDFWalk 会除零)
            var ex = Assert.Throws<ArgumentException>(
                () => QualityDistributionShape.Validate(new[] { 0, 0, 0, 0, 0 }, MaxQuality));
            Assert.That(ex.Message, Does.Contain("Σw = 0 < 1"));
        }

        [Test]
        public void test_ac1708b_qualityDistribution_singleWeightOne_passes()
        {
            // Σw = 1 是合法下界(单档 100% 分布 —— 品级无信息但**合法**)
            Assert.DoesNotThrow(
                () => QualityDistributionShape.Validate(new[] { 1, 0, 0, 0, 0 }, MaxQuality));
        }

        [Test]
        public void test_qualityDistribution_wrongLength_rejected()
        {
            // 长度 ≠ MAX_QUALITY ⇒ 档位错位(D-21-16 的 quality_character 同款契约)
            var ex = Assert.Throws<ArgumentException>(
                () => QualityDistributionShape.Validate(new[] { 1, 1, 1 }, MaxQuality));
            Assert.That(ex.Message, Does.Contain("长度 3 ≠ MAX_QUALITY = 5"));
        }

        [Test]
        public void test_qualityDistribution_negativeWeight_rejected()
        {
            // 负权重 ⇒ 累积和倒退 ⇒ CDFWalk 单调前提破
            var ex = Assert.Throws<ArgumentException>(
                () => QualityDistributionShape.Validate(new[] { 1, -1, 1, 1, 1 }, MaxQuality));
            Assert.That(ex.Message, Does.Contain("quality_distribution[1] = -1 < 0"));
        }

        [Test]
        public void test_qualityDistribution_null_rejected()
        {
            Assert.Throws<ArgumentNullException>(
                () => QualityDistributionShape.Validate(null, MaxQuality));
        }

        [Test]
        public void test_qualityDistribution_isValid_matchesValidate()
        {
            // 非抛形与抛形两条路径须同判(否则「先问再决」的调用方会看到与硬失败不同的答案)
            Assert.That(QualityDistributionShape.IsValid(new[] { 1, 0, 0, 0, 0 }, MaxQuality, out _), Is.True);
            Assert.That(QualityDistributionShape.IsValid(new[] { 0, 0, 0, 0, 0 }, MaxQuality, out string why),
                Is.False);
            Assert.That(why, Does.Contain("Σw = 0 < 1"));
        }

        // ══════════════════════════════════════════════════════════════
        // 辅助
        // ══════════════════════════════════════════════════════════════

        private static Fix[] LegalSeasonMult()
            => new[] { FixParse.Parse("1"), FixParse.Parse("5/4"), FixParse.Parse("3/4"),
                       FixParse.Parse("3/2"), FixParse.Parse("3/4") };

        private static int[] LegalCapTable()
        {
            var caps = new int[61];
            for (int i = 0; i < 61; i++) caps[i] = 1 + Math.Min(i, 20) / 20;
            return caps;
        }

        private static ForagingConstants LegalConstants()
            => new ForagingConstants(
                LegalCapTable(), LegalSeasonMult(), SeasonsPerYear, 1200, 5,
                FixParse.Parse("1/40"), FixParse.Parse("1/2"), FixParse.Parse("3"), MaxQuality);

        // 本文件在 unity/Assets/Tests/EditMode/Foraging/ ⇒ 仓库根 = 上 5 层
        // (Foraging→EditMode→Tests→Assets→unity→仓根)。承 clinicmachine_tables_schema_test.cs:43 先例。
        private static string RepoRoot([System.Runtime.CompilerServices.CallerFilePath] string thisFile = "")
            => Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(thisFile) ?? ".", "..", "..", "..", "..", ".."));

        /// <summary>读 <c>assets/data/</c> 下的作者态源表(缺失即红 —— 静默跳过会让判据形同虚设)。</summary>
        private static string ReadRepoData(string name)
        {
            string path = Path.Combine(RepoRoot(), "assets", "data", name);
            Assert.That(File.Exists(path), Is.True, $"作者态源表缺失:{path}");
            return File.ReadAllText(path);
        }
    }
}
