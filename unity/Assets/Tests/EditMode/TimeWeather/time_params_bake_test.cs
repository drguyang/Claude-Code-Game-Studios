// `5_time` 数据集的烘焙 / 解码夹具(ADR-038 §D1 · V-1 / V-3 的执行体)。
//
// 权威来源:
//   ADR-038 §D1(三值外部化)· §D2(未知键 —— 尤其「值未裁定」的那三个 —— 必须拒收)
//   design/gdd/time-and-weather.md §Tuning Knobs(`:516` / `:519` 的三值,2026-11-03 用户裁定)
//
// ⚠️ **本文件读仓库内真表** `assets/data/5_time.json`(以及 `unity/Assets/DataCooked/` 的产物)。
//    具名声明(承 `.claude/rules/test-standards.md` §只读外部夹具的声明义务):
//      · `assets/data/5_time.json` —— 为什么必须读真表而非内联:本夹具的**职责**
//        就是断言「作者态文件里写的确实是裁定值」;内联一份等于断言夹具自己(自指判据 · F3)。
//      · `unity/Assets/DataCooked/5_time.cooked.bytes` —— 同上,断言「已提交产物 == 现烘产物」。
//    零写盘:本文件只读,不落任何文件。

using System.Collections.Generic;
using System.IO;
using DaYiJingCheng.EditorTools.Bake;
using DaYiJingCheng.Gameplay.Presentation;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.TimeWeather
{
    /// <summary>`5_time` 的两阶段烘焙 + 解码。</summary>
    [TestFixture]
    public class TimeParamsBakeTest
    {
        private static readonly string RepoRoot = ComputeRepoRoot();

        private static string ComputeRepoRoot(
            [System.Runtime.CompilerServices.CallerFilePath] string thisFile = "")
            => Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(thisFile) ?? ".", "..", "..", "..", "..", ".."));

        // ══════════════════ 真表的三个值 ══════════════════

        [Test]
        public void test_sourceFile_holdsTheAdjudicatedValues()
        {
            // 字面量哨兵 —— 不得写成 `Is.EqualTo(bound.TicksPerDay)`(那是自指判据)
            TimeParamsBinder.BindResult bound = TimeParamsBinder.Bind(
                File.ReadAllText(Path.Combine(RepoRoot, "assets", "data", "5_time.json")));

            Assert.That(bound.TicksPerDay, Is.EqualTo(36000L),
                "TICKS_PER_DAY = 36000(20 Hz ⇒ 30 分钟一日)是 2026-11-03 用户裁定");
            Assert.That(bound.NightStart, Is.EqualTo(27000L));
            Assert.That(bound.NightSpan, Is.EqualTo(9000L));
        }

        [Test]
        public void test_sourceFile_ticksPerDay_matchesHalfHourAt20Hz()
        {
            // 量纲自检:20 Hz × 60 s × 30 min = 36000。tick 频率若被重裁,
            // 本条会把「日长还是旧值」照出来。
            const int hz = 20, secondsPerMinute = 60, minutesPerDay = 30;
            TimeParamsBinder.BindResult bound = TimeParamsBinder.Bind(
                File.ReadAllText(Path.Combine(RepoRoot, "assets", "data", "5_time.json")));

            Assert.That(bound.TicksPerDay, Is.EqualTo(hz * secondsPerMinute * minutesPerDay),
                "日长必须与「20 Hz · 30 分钟一日」自洽");
        }

        // ══════════════════ 产物往返 ══════════════════

        [Test]
        public void test_cooked_onDisk_matchesFreshBake_andDecodes()
        {
            TimeParamsBaker.BakeOutput fresh = TimeParamsBaker.BakeFromRepo(RepoRoot);

            string committedPath = Path.Combine(
                RepoRoot, "unity", "Assets", "DataCooked", TimeParamsBaker.CookedFileName);
            Assert.That(File.Exists(committedPath), Is.True,
                $"盘上产物缺失:{committedPath} —— 须跑「烘焙 5_time」菜单 / CLI 后入库");
            byte[] committed = File.ReadAllBytes(committedPath);

            // ① 字节漂移:改源不重烘 ⇒ 这里红
            Assert.That(committed, Is.EqualTo(fresh.Cooked),
                "盘上产物与现烘字节不一致 —— 作者态改过但没重烘");

            // ② 往返:写方 / 读方布局镜像(TickPerDay/NightStart/NightSpan 三字段序)
            TimeParamsDataSet data = TimeParamsCookedCodec.Read(committed);
            Assert.That(data.TicksPerDay, Is.EqualTo(36000L));
            Assert.That(data.NightStart, Is.EqualTo(27000L));
            Assert.That(data.NightSpan, Is.EqualTo(9000L));
            Assert.That(data.SchemaVersion, Is.EqualTo(TimeParamsCookedCodec.ExpectedSchemaVersion));
            Assert.That(data.ConfigVersion, Is.EqualTo(fresh.ConfigVersion),
                "产物头的 ConfigVersion 须等于源文本哈希派生值");
        }

        // ══════════════════ 负控(烘焙期必须拒收)══════════════════

        [Test]
        public void test_undecidedKey_isRejected_withItsOwnMessage()
        {
            // ADR-038 §D1:`ticks_per_season` 等三值**未裁定**、刻意不进 schema。
            // 塞进来必须**专名报错**(提示「还没裁」),不得笼统算作「未知键」。
            const string json =
                "{\"schema_version\":1,\"ticks_per_day\":36000,\"night_start\":27000," +
                "\"night_span\":9000,\"ticks_per_season\":1080000}";

            var ex = Assert.Throws<BakeValidationException>(() => TimeParamsBinder.Bind(json));
            Assert.That(string.Join("\n", ex.Errors), Does.Contain("尚未裁定"),
                "报错须点明「值尚未裁定」;实际:\n" + string.Join("\n", ex.Errors));
        }

        [Test]
        public void test_nightSpanNotLessThanTicksPerDay_isRejected()
        {
            // AC-5-11 的「NIGHT_SPAN ≥ TICKS_PER_DAY ⇒ 硬失败」在**烘焙期**的同构判据
            const string json =
                "{\"schema_version\":1,\"ticks_per_day\":1000,\"night_start\":0,\"night_span\":1000}";

            var ex = Assert.Throws<BakeValidationException>(() => TimeParamsBinder.Bind(json));
            Assert.That(string.Join("\n", ex.Errors), Does.Contain("NIGHT_SPAN"));
        }

        [Test]
        public void test_fixStringValue_isRejected_notCoerced()
        {
            // 三个量都不是 `Fix` ⇒ 就该是 JSON 整数;写成字符串必须拒(禁隐式转换)
            const string json =
                "{\"schema_version\":1,\"ticks_per_day\":\"36000\",\"night_start\":27000,\"night_span\":9000}";

            var ex = Assert.Throws<BakeValidationException>(() => TimeParamsBinder.Bind(json));
            Assert.That(string.Join("\n", ex.Errors), Does.Contain("整数"));
        }

        [Test]
        public void test_missingNightSpan_isRejected()
        {
            const string json =
                "{\"schema_version\":1,\"ticks_per_day\":36000,\"night_start\":27000}";

            var ex = Assert.Throws<BakeValidationException>(() => TimeParamsBinder.Bind(json));
            Assert.That(string.Join("\n", ex.Errors), Does.Contain("night_span"));
        }
    }
}
