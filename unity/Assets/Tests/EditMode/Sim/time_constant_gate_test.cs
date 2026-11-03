// `TimeConstantSingleDefinitionGate` 的夹具 —— AC-5-05 的执行体判据(2026-11-03 立;ADR-038 换向)。
//
// 权威来源:
//   design/gdd/time-and-weather.md §规则四 / AC-5-05(值只有一个定义点)
//   **ADR-038 §D1** —— 三值外部化到 `assets/data/5_time.json`;代码里零 `const`
//
// 立门缘由(实测两次):
//   ① 本轮一度同时存在 `Sim/Time/WorldTime`(36000)与 `Sim/World/TimeBase`(480)
//      两份日长、两套昼夜公式,**而两边各自有绿灯夹具** —— 没有任何东西会红。
//   ② 首版门自己漏判:`public long TICKS_PER_DAY;`(无初始化)完全不可见,门绿着。
//
// 本文件断**十二条**,缺一即判据空洞:
//   ①  允许落点是钉死的字面量          —— test_allowedPath_isTheLiteralWePinned
//   ②  真树上绿(门不是恒红)           —— test_gate_greenOnRealTree
//   ③  `const` 声明 ⇒ 红(值不在代码里)  —— test_gate_redOnConstDeclaration
//   ④  `NIGHT_START` 的 `const` 同样红  —— test_gate_guardsAllThreeNames
//   ⑤  载体文件外写裸字段 ⇒ 红          —— test_gate_redOnFieldOutsideAllowedFile
//   ⑥  允许文件内的裸字段 ⇒ 绿(正控)    —— test_gate_greenOnBareFieldInAllowedFile
//   ⑦  一条都没有 ⇒ 红(防「删光即绿」)  —— test_gate_redOnZeroDeclarations
//   ⑧  允许文件内写两条 ⇒ 红            —— test_gate_redOnDuplicateInAllowedFile
//   ⑨  **引用**不得误报                 —— test_gate_doesNotFlagReferences
//   ⑩  注释里的声明不得误报             —— test_gate_ignoresDeclarationInLineComment
//   ⑪  字符串字面量里的声明不得误报      —— test_gate_ignoresDeclarationInStringLiteral
//   ⑫  剥离后**行号仍准确**             —— test_gate_reportsCorrectLineAfterMultiLineComment
//   ⑬  扫描面不存在 ⇒ 红                —— test_gate_redWhenScanRootMissing
//
// ⚠️ 本文件直驱 `Evaluate`(纯函数)—— 门链取文件那一层由 ② 与 ⑬ 覆盖。

using System.Collections.Generic;
using DaYiJingCheng.EditorTools.Gates;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Unit.Sim
{
    [TestFixture]
    public class TimeConstantGateTest
    {
        /// <summary>
        /// 允许落点的**字面量**副本 —— 故意不引用被测常量做断言(自指判据 · F3):
        /// 若有人改了门的允许路径,下面这条会红,而不是跟着一起动。
        /// </summary>
        private const string AllowedPathLiteral = "Assets/Sim/TimeParams.cs";

        private static List<(string, string)> Files(params (string, string)[] pairs)
            => new List<(string, string)>(pairs);

        /// <summary>合法载体(允许文件里的裸字段)。多数用例拿它当「绿的那一半」。</summary>
        private static (string, string) GoodCarrier()
            => (AllowedPathLiteral,
                "public long TICKS_PER_DAY;\npublic long NIGHT_START;\npublic long NIGHT_SPAN;");

        [Test]
        public void test_allowedPath_isTheLiteralWePinned()
        {
            Assert.That(TimeConstantSingleDefinitionGate.AllowedRelativePath,
                Is.EqualTo(AllowedPathLiteral),
                "允许落点变了 ⇒ 本夹具的靶场失效(必须同步改本文件的字面量)");
        }

        [Test]
        public void test_gate_greenOnRealTree()
        {
            List<string> errs = TimeConstantSingleDefinitionGate.RunAll(".");
            Assert.That(errs, Is.Empty,
                "真实树上必须绿;红行:\n" + string.Join("\n", errs));
        }

        [Test]
        public void test_gate_redOnConstDeclaration()
        {
            // ADR-038 §D1:值不在代码里。哪怕它落在「允许文件」也一样红。
            List<string> errs = TimeConstantSingleDefinitionGate.Evaluate(Files(
                (AllowedPathLiteral, "public const long TICKS_PER_DAY = 36000L;")));

            Assert.That(errs, Is.Not.Empty, "`const` 声明必须红(值应在 5_time.json)");
            Assert.That(string.Join("\n", errs), Does.Contain("const"),
                "红行须点明是 const 形态;实际:\n" + string.Join("\n", errs));
        }

        [Test]
        public void test_gate_guardsAllThreeNames()
        {
            // 只守 TICKS_PER_DAY 是不够的:昼夜参数同样进数据、同样会被复制
            foreach (string name in new[] { "NIGHT_START", "NIGHT_SPAN" })
            {
                List<string> errs = TimeConstantSingleDefinitionGate.Evaluate(Files(
                    ("Assets/Sim/World/Elsewhere.cs", "public const long " + name + " = 1L;")));

                Assert.That(errs, Is.Not.Empty, $"`{name}` 的 const 必须红");
            }
        }

        [Test]
        public void test_gate_redOnFieldOutsideAllowedFile()
        {
            List<string> errs = TimeConstantSingleDefinitionGate.Evaluate(Files(
                GoodCarrier(),
                ("Assets/Sim/World/Elsewhere.cs", "public long TICKS_PER_DAY;")));

            Assert.That(errs, Is.Not.Empty, "载体文件之外的裸字段 = 第二份载体");
            Assert.That(string.Join("\n", errs), Does.Contain("Assets/Sim/World/Elsewhere.cs"),
                "红行须点名违规文件;实际:\n" + string.Join("\n", errs));
        }

        [Test]
        public void test_gate_greenOnBareFieldInAllowedFile()
        {
            // 正控:合法形态必须绿 —— 否则「一律红」也能骗过上面那些「必须红」的用例
            List<string> errs = TimeConstantSingleDefinitionGate.Evaluate(Files(GoodCarrier()));

            Assert.That(errs, Is.Empty,
                "载体文件里的裸字段是**合法形态**;红行:\n" + string.Join("\n", errs));
        }

        [Test]
        public void test_gate_redOnZeroDeclarations()
        {
            // 空洞正控:把载体删光不得变绿
            List<string> errs = TimeConstantSingleDefinitionGate.Evaluate(Files(
                (AllowedPathLiteral, "public sealed class TimeParams { }")));

            Assert.That(errs, Is.Not.Empty, "零条声明 ⇒ 必须红(否则「删掉即变绿」)");
        }

        [Test]
        public void test_gate_redOnDuplicateInAllowedFile()
        {
            List<string> errs = TimeConstantSingleDefinitionGate.Evaluate(Files(
                (AllowedPathLiteral,
                 "public long TICKS_PER_DAY;\npublic long TICKS_PER_DAY;")));

            Assert.That(errs, Is.Not.Empty, "同一载体槽写两遍同样是第二真源");
        }

        [Test]
        public void test_gate_doesNotFlagReferences()
        {
            // 只**引用**不声明 ⇒ 不得误报(否则任何消费者都会被判红)
            List<string> errs = TimeConstantSingleDefinitionGate.Evaluate(Files(
                GoodCarrier(),
                ("Assets/Sim/WeatherRoll.cs",
                 "long p = TimeBase.FDiv(tick, params_.TICKS_PER_DAY);\n" +
                 "// 说明文字里提到 TICKS_PER_DAY 也不算声明")));

            Assert.That(errs, Is.Empty,
                "引用与注释不得命中;红行:\n" + string.Join("\n", errs));
        }

        [Test]
        public void test_gate_ignoresDeclarationInLineComment()
        {
            // 门自己的头注释就是这个形状
            List<string> errs = TimeConstantSingleDefinitionGate.Evaluate(Files(
                GoodCarrier(),
                ("Assets/Sim/Notes.cs",
                 "// 历史:const int TICKS_PER_DAY = 480;  ← 已删\n" +
                 "public static class Notes { }")));

            Assert.That(errs, Is.Empty,
                "行注释里的声明不得误报;红行:\n" + string.Join("\n", errs));
        }

        [Test]
        public void test_gate_ignoresDeclarationInStringLiteral()
        {
            List<string> errs = TimeConstantSingleDefinitionGate.Evaluate(Files(
                GoodCarrier(),
                ("Assets/Tests/FakeFixture.cs",
                 "static string S = \"public const long TICKS_PER_DAY = 36000L;\";\n" +
                 "static string V = @\"public const int NIGHT_START = 1;\");")));

            Assert.That(errs, Is.Empty,
                "字符串字面量里的声明不得误报;红行:\n" + string.Join("\n", errs));
        }

        [Test]
        public void test_gate_reportsCorrectLineAfterMultiLineComment()
        {
            // 剥离若把换行吃掉,行号就会指错 —— 这条钉住「抹字符但保换行」
            const string Body =
                "/* 第一行\n" +
                "   第二行\n" +
                "   第三行 */\n" +
                "public long TICKS_PER_DAY;";

            List<string> errs = TimeConstantSingleDefinitionGate.Evaluate(Files(
                ("Assets/Sim/Elsewhere.cs", Body)));

            Assert.That(errs, Is.Not.Empty, "真实声明必须被抓住");
            Assert.That(string.Join("\n", errs), Does.Contain("Assets/Sim/Elsewhere.cs:4"),
                "行号须为 4(块注释占前三行);实际红行:\n" + string.Join("\n", errs));
        }

        [Test]
        public void test_gate_redWhenScanRootMissing()
        {
            List<string> errs = TimeConstantSingleDefinitionGate.RunAll(
                "Assets/__no_such_dir_for_gate_test__");

            Assert.That(errs, Is.Not.Empty,
                "扫描面不存在 ⇒ 必须红:路径写错与「门绿」不可分(空洞正控)");
        }

        // ─────────────────────────────────────────────────────────────────────
        // 数据侧(ADR-038 §D1/§D2):值的唯一定义点在 5_time.json,其他数据集只许 $ref
        // ─────────────────────────────────────────────────────────────────────

        private static (string, string) DefFile()
            => (TimeConstantSingleDefinitionGate.DataDefinitionFile,
                "{\n  \"schema_version\": 1,\n  \"ticks_per_day\": 36000,\n  \"night_start\": 27000,\n  \"night_span\": 9000\n}");

        [Test]
        public void test_dataSide_greenOnRealTree()
        {
            List<string> errs = TimeConstantSingleDefinitionGate.RunDataSide(".");
            Assert.That(errs, Is.Empty,
                "真实数据树必须绿;红行:\n" + string.Join("\n", errs));
        }

        [Test]
        public void test_dataSide_greenWhenOtherFileUsesRef()
        {
            List<string> errs = TimeConstantSingleDefinitionGate.EvaluateData(Files(
                DefFile(),
                ("assets/data/52_random_events.json",
                 "{ \"ticks_per_day\": { \"$ref\": \"5_time.ticks_per_day\" } }")));

            Assert.That(errs, Is.Empty,
                "`$ref` 是合法形态;红行:\n" + string.Join("\n", errs));
        }

        [Test]
        public void test_dataSide_redWhenOtherFileHasLiteralCopy()
        {
            // AC-5-05 的字面副本 —— 这正是「52 自己也写一份 36000」的形态
            List<string> errs = TimeConstantSingleDefinitionGate.EvaluateData(Files(
                DefFile(),
                ("assets/data/52_random_events.json", "{ \"ticks_per_day\": 36000 }")));

            Assert.That(errs, Is.Not.Empty, "字面副本必须红");
            Assert.That(string.Join("\n", errs), Does.Contain("52_random_events.json"),
                "红行须点名文件;实际:\n" + string.Join("\n", errs));
        }

        [Test]
        public void test_dataSide_redWhenDefinitionFileMissing()
        {
            List<string> errs = TimeConstantSingleDefinitionGate.EvaluateData(Files(
                ("assets/data/52_random_events.json",
                 "{ \"ticks_per_day\": { \"$ref\": \"5_time.ticks_per_day\" } }")));

            Assert.That(errs, Is.Not.Empty, "缺定义文件 ⇒ 红");
        }

        [Test]
        public void test_dataSide_redWhenDefinitionFileMissesAField()
        {
            List<string> errs = TimeConstantSingleDefinitionGate.EvaluateData(Files(
                (TimeConstantSingleDefinitionGate.DataDefinitionFile,
                 "{ \"schema_version\": 1, \"ticks_per_day\": 36000, \"night_start\": 27000 }")));

            Assert.That(errs, Is.Not.Empty, "三值缺一 ⇒ 红");
            Assert.That(string.Join("\n", errs), Does.Contain("night_span"));
        }
    }
}
