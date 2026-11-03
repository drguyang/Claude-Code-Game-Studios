// `52_random_events` 数据集的 `$ref` 绑定夹具(ADR-038 §D2 / §D4 · **V-2 / V-3 的执行体**)。
//
// 权威来源:
//   ADR-038 §D2(跨表引用形式 `{"$ref": "<artifact>.<field>"}`,解析后须与 5 同值)
//   ADR-038 §D4(四值 `6 / 2 / 6 / 6000`,2026-11-03 用户裁定)
//   GDD `design/gdd/random-events.md` §F1 / §规则六 / DC-3 / `:1075`(不整除 ⇒ 构建期报错)
//   AC-5-05 的 `THEN` 第二半 —— 52 数据集**零字面副本**
//
// ⚠️ **本文件读仓库内真表**(`assets/data/52_random_events.json` 与 `assets/data/5_time.json`)
//    以及已提交产物 `unity/Assets/DataCooked/52_random_events.cooked.bytes`。
//    具名声明(承 `.claude/rules/test-standards.md` §只读外部夹具的声明义务):
//      · 为什么必须读真表而非内联 —— 本夹具的**职责**就是断言「作者态写的是 `$ref` 而不是
//        字面副本」「四值确实是裁定值」。内联一份 JSON 等于断言夹具自己(自指判据 · F3)。
//      · 产物一侧断言「已提交 cooked == 现场烘焙」,防止手改产物绕过 binder。
//    零写盘:本文件只读。
//
// ⚠️ **负控必须走真 binder**:V-3 的负例把真源 JSON 的 `$ref` 换成数字再喂同一个
//    `RandomEventsBinder.Bind` —— 不是另写一个「看起来像 binder」的谓词(F1 假绿形态)。

using System.Collections.Generic;
using System.IO;
using DaYiJingCheng.EditorTools.Bake;
using DaYiJingCheng.Gameplay.Presentation;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.TimeWeather
{
    /// <summary>`52_random_events` 的 `$ref` 解析 / 四值 / 产物对拍。</summary>
    [TestFixture]
    public class RandomEventsBinderTest
    {
        private static readonly string RepoRoot = ComputeRepoRoot();

        private static string ComputeRepoRoot(
            [System.Runtime.CompilerServices.CallerFilePath] string thisFile = "")
            => Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(thisFile) ?? ".", "..", "..", "..", "..", ".."));

        private static string SourcePath =>
            Path.Combine(RepoRoot, "assets", "data", "52_random_events.json");

        private static string TimeSourcePath =>
            Path.Combine(RepoRoot, "assets", "data", "5_time.json");

        /// <summary>真引用表 —— 值取自 `5_time.json` 的**绑定结果**(与生产 baker 同一来源)。</summary>
        private static IReadOnlyDictionary<string, long> RealRefs()
        {
            TimeParamsBinder.BindResult time =
                TimeParamsBinder.Bind(File.ReadAllText(TimeSourcePath));
            return new Dictionary<string, long>
            {
                { RandomEventsBaker.TicksPerDayRefKey, time.TicksPerDay },
            };
        }

        // ══════════════════ V-2 · 引用解析后与 5 同值 ══════════════════

        [Test]
        public void test_ref_resolvesToTheSameValueAs5()
        {
            var refs = RealRefs();
            RandomEventsBinder.BindResult bound =
                RandomEventsBinder.Bind(File.ReadAllText(SourcePath), refs);

            // 字面量哨兵:不得写成 `Is.EqualTo(refs[...])` —— 那是拿同一来源自证(自指判据 · F3)。
            Assert.That(bound.TicksPerDay, Is.EqualTo(36000L),
                "52 的 ticks_per_day 解析后须 = 5 的 36000(DC-3 / AC-5-05)");
        }

        [Test]
        public void test_fourValues_areTheAdjudicatedOnes()
        {
            RandomEventsBinder.BindResult bound =
                RandomEventsBinder.Bind(File.ReadAllText(SourcePath), RealRefs());

            Assert.That(bound.RollInterval, Is.EqualTo(6000L));
            Assert.That(bound.WindowSize, Is.EqualTo(6));
            Assert.That(bound.Base, Is.EqualTo(2));
            Assert.That(bound.BudgetMax, Is.EqualTo(6));
        }

        [Test]
        public void test_schemaVersion_isOne()
        {
            RandomEventsBinder.BindResult bound =
                RandomEventsBinder.Bind(File.ReadAllText(SourcePath), RealRefs());
            Assert.That(bound.SchemaVersion, Is.EqualTo(1u));
        }

        // ══════════════════ V-3 · 字面副本必须烘焙失败 ══════════════════

        [Test]
        public void test_literalNumber_isRejected()
        {
            // 把真源的 `{"$ref": ...}` 换成裸数字 `36000` —— 其余字段原样。
            string json = System.Text.RegularExpressions.Regex.Replace(
                File.ReadAllText(SourcePath),
                "\"ticks_per_day\"\\s*:\\s*\\{[^}]*\\}",
                "\"ticks_per_day\": 36000");
            StringAssert.DoesNotContain("$ref", json,
                "前置:负例里不得再有 $ref(否则测的不是字面副本)");
            StringAssert.Contains("\"ticks_per_day\": 36000", json,
                "前置:替换必须真的落地(否则下面的断言会在一个没改过的输入上恒绿)");

            var ex = Assert.Throws<BakeValidationException>(
                () => RandomEventsBinder.Bind(json, RealRefs()));

            StringAssert.Contains("字面副本", ex.Message,
                "报文须点名 AC-5-05 —— 只报「类型错」会让作者以为是笔误");
        }

        [Test]
        public void test_unresolvableRef_isRejected()
        {
            string json = File.ReadAllText(SourcePath)
                .Replace("5_time.ticks_per_day", "5_time.ticks_per_day_typo");

            var ex = Assert.Throws<BakeValidationException>(
                () => RandomEventsBinder.Bind(json, RealRefs()));

            StringAssert.Contains("无法解析", ex.Message);
        }

        [Test]
        public void test_refObjectWithExtraKeys_isRejected()
        {
            string json = File.ReadAllText(SourcePath).Replace(
                "\"$ref\": \"5_time.ticks_per_day\"",
                "\"$ref\": \"5_time.ticks_per_day\", \"fallback\": 36000");

            var ex = Assert.Throws<BakeValidationException>(
                () => RandomEventsBinder.Bind(json, RealRefs()));

            StringAssert.Contains("恰含", ex.Message,
                "多一个 fallback 键 = 第二份副本,规范形不唯一");
        }

        // ══════════════════ 值域(§D4 / GDD `:1075`)══════════════════

        [Test]
        public void test_nonDivisibleRollInterval_isRejected()
        {
            string json = File.ReadAllText(SourcePath).Replace("\"roll_interval\": 6000",
                                                               "\"roll_interval\": 7000");

            var ex = Assert.Throws<BakeValidationException>(
                () => RandomEventsBinder.Bind(json, RealRefs()));

            StringAssert.Contains("不整除", ex.Message);
        }

        [Test]
        public void test_budgetMaxBelowBase_isRejected()
        {
            string json = File.ReadAllText(SourcePath).Replace("\"budget_max\": 6",
                                                               "\"budget_max\": 1");

            var ex = Assert.Throws<BakeValidationException>(
                () => RandomEventsBinder.Bind(json, RealRefs()));

            StringAssert.Contains("budget_max", ex.Message);
        }

        [Test]
        public void test_windowSizeZero_isRejected()
        {
            string json = File.ReadAllText(SourcePath).Replace("\"window_size\": 6",
                                                               "\"window_size\": 0");

            var ex = Assert.Throws<BakeValidationException>(
                () => RandomEventsBinder.Bind(json, RealRefs()));

            StringAssert.Contains("window_size", ex.Message);
        }

        [Test]
        public void test_unknownKey_isRejected()
        {
            string json = File.ReadAllText(SourcePath).Replace(
                "\"schema_version\": 1", "\"schema_version\": 1, \"windw_size\": 6");

            var ex = Assert.Throws<BakeValidationException>(
                () => RandomEventsBinder.Bind(json, RealRefs()));

            StringAssert.Contains("未知键", ex.Message);
        }

        // ══════════════════ 产物 ↔ 现场烘焙 ══════════════════

        [Test]
        public void test_cookedOnDisk_matchesFreshBake_andDecodes()
        {
            string cookedPath = Path.Combine(RepoRoot, "unity", "Assets", "DataCooked",
                                             RandomEventsBaker.CookedFileName);
            Assert.That(File.Exists(cookedPath), Is.True,
                $"已提交产物不存在:{cookedPath}(先跑 DataBakeMenu.BakeRandomEvents)");

            RandomEventsBaker.BakeOutput fresh = RandomEventsBaker.BakeFromRepo(RepoRoot);
            byte[] onDisk = File.ReadAllBytes(cookedPath);

            CollectionAssert.AreEqual(fresh.Cooked, onDisk,
                "已提交产物必须与现场烘焙**逐字节**相同 —— 否则有人绕过 binder 手改过产物");

            RandomEventsDataSet decoded = RandomEventsCookedCodec.Read(onDisk);
            Assert.That(decoded.TicksPerDay, Is.EqualTo(36000L), "解码后仍是解析值");
            Assert.That(decoded.RollInterval, Is.EqualTo(6000L));
            Assert.That(decoded.WindowSize, Is.EqualTo(6));
            Assert.That(decoded.Base, Is.EqualTo(2));
            Assert.That(decoded.BudgetMax, Is.EqualTo(6));
        }

        [Test]
        public void test_baker_rejectsWhen5IsPerturbed()
        {
            // 引用值的唯一来源是 5 的**绑定结果**:把 5 的作者态改坏(此处用假 refs 更直接),
            // 52 的解析值必须随之失败而不是悄悄用旧值。
            var empty = new Dictionary<string, long>();
            var ex = Assert.Throws<BakeValidationException>(
                () => RandomEventsBinder.Bind(File.ReadAllText(SourcePath), empty));

            StringAssert.Contains("无法解析", ex.Message,
                "引用表为空 ⇒ 必须失败。若这里静默成功,说明 binder 内部藏了默认值(F6 形态)");
        }
    }
}
