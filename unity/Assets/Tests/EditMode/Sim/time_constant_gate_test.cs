// `TimeConstantSingleDefinitionGate` 的夹具 —— AC-5-05 的执行体判据(2026-11-03 立)。
//
// 权威来源:
//   design/gdd/time-and-weather.md §规则四 / AC-5-05(`TICKS_PER_DAY` 代码中只有一个定义点)
//
// 立门缘由(实测):本轮一度同时存在 `Sim/Time/WorldTime`(36000)与 `Sim/World/TimeBase`(480)
//   两份日长、两套昼夜公式,**而两边各自有绿灯夹具** —— 没有任何东西会红。
//
// 本文件断**十条**,缺一即判据空洞(红向与绿向必须同时存在):
//   ①  真树上绿(门不是恒红)             —— test_gate_greenOnRealTree
//   ②  别处出现第二条 ⇒ 红(门真的判)     —— test_gate_detectsSecondDefinitionOutsideAllowedFile
//   ③  一条都没有 ⇒ 红(正控,防「删光即绿」) —— test_gate_detectsZeroDeclarations
//   ④  允许文件内写两条 ⇒ 红              —— test_gate_detectsDuplicateInsideAllowedFile
//   ⑤  **引用**不得误报                   —— test_gate_doesNotFlagReferences
//   ⑥  扫描面不存在 ⇒ 红(路径错 ≠ 判据通过) —— test_gate_redWhenScanRootMissing
//   ⑦  行注释里的声明不得误报             —— test_gate_ignoresDeclarationInLineComment
//   ⑧  字符串字面量里的声明不得误报        —— test_gate_ignoresDeclarationInStringLiteral
//   ⑨  块注释里的声明不得误报             —— test_gate_ignoresDeclarationInBlockComment
//   ⑩  剥离后**行号仍准确**               —— test_gate_reportsCorrectLineAfterMultiLineComment
//   ⑪  数据集里的字面副本 ⇒ 红            —— test_gateDataSet_detectsLiteralCopy
//   ⑫  三种写法都命中(下划线/驼峰/连字符) —— test_gateDataSet_detectsAllNameForms
//   ⑬  判据边界:同值换名**看不见**(盲区) —— test_gateDataSet_isNameBasedNotValueBased
//   ⑭  数据集目录不存在 ⇒ 红               —— test_gateDataSet_redWhenDataRootMissing
//
// ⚠️ ⑦–⑩ 是**首跑实测逼出来的**:剥离前,门自己的头注释与夹具里的字符串实参被双双判成
//    第二定义点 ⇒ 真树上恒红。恒红的门与恒绿的门同为空洞,故这四条是**判据的一部分**,
//    不是锦上添花。
//
// ⚠️ 本文件直驱 `Evaluate`(纯函数)—— 门链取文件那一层由 ① 与 ⑥ 覆盖。

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
        private const string AllowedPathLiteral = "Assets/Sim/World/TimeBase.cs";

        private static List<(string, string)> Files(params (string, string)[] pairs)
            => new List<(string, string)>(pairs);

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
            // 门链入口,真实工程树:"." = 工程根(unity/)
            List<string> errs = TimeConstantSingleDefinitionGate.RunAll(".");
            Assert.That(errs, Is.Empty,
                "真实树上必须绿;红行:\n" + string.Join("\n", errs));
        }

        [Test]
        public void test_gate_detectsSecondDefinitionOutsideAllowedFile()
        {
            // 模拟本轮的真实事故:`TimeBase` 之外又写了一份
            List<string> errs = TimeConstantSingleDefinitionGate.Evaluate(Files(
                (AllowedPathLiteral, "public const long TICKS_PER_DAY = 36000L;"),
                ("Assets/Sim/Time/WorldTime.cs", "public const int TicksPerDay = 36000;")));

            Assert.That(errs, Is.Not.Empty, "第二定义点必须红");
            Assert.That(string.Join("\n", errs), Does.Contain("Assets/Sim/Time/WorldTime.cs"),
                "红行须点名违规文件");
        }

        [Test]
        public void test_gate_detectsZeroDeclarations()
        {
            // 空洞正控:把常量删光不得变绿
            List<string> errs = TimeConstantSingleDefinitionGate.Evaluate(Files(
                ("Assets/Sim/World/TimeBase.cs", "public static class TimeBase { }")));

            Assert.That(errs, Is.Not.Empty, "零条声明 ⇒ 必须红(否则「删掉即变绿」)");
        }

        [Test]
        public void test_gate_detectsDuplicateInsideAllowedFile()
        {
            List<string> errs = TimeConstantSingleDefinitionGate.Evaluate(Files(
                (AllowedPathLiteral,
                 "public const long TICKS_PER_DAY = 36000L;\n" +
                 "public const long TicksPerDay = 36000L;")));

            Assert.That(errs, Is.Not.Empty, "同一文件两条声明同样是第二定义点");
        }

        [Test]
        public void test_gate_doesNotFlagReferences()
        {
            // 只**引用**不声明 ⇒ 不得误报(否则任何消费者都会被判红)
            List<string> errs = TimeConstantSingleDefinitionGate.Evaluate(Files(
                (AllowedPathLiteral, "public const long TICKS_PER_DAY = 36000L;"),
                ("Assets/Sim/WeatherRoll.cs",
                 "long p = FDiv(tick, TimeBase.TICKS_PER_DAY);\n" +
                 "// 说明文字里提到 TICKS_PER_DAY 也不算定义")));

            Assert.That(errs, Is.Empty,
                "引用与注释不得命中;红行:\n" + string.Join("\n", errs));
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
        // 剥离面(⑦–⑩):注释与字面量里的同名文本**不得**判成定义点。
        // 这四条的失败形态不是「漏判」而是「恒红」—— 首跑真树上就是这么红的。
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public void test_gate_ignoresDeclarationInLineComment()
        {
            // 本门自己的头注释就是这个形状
            List<string> errs = TimeConstantSingleDefinitionGate.Evaluate(Files(
                (AllowedPathLiteral, "public const long TICKS_PER_DAY = 36000L;"),
                ("Assets/Sim/Notes.cs",
                 "// 历史:const int TICKS_PER_DAY = 480;  ← 已删\n" +
                 "public static class Notes { }")));

            Assert.That(errs, Is.Empty,
                "行注释里的声明不得误报;红行:\n" + string.Join("\n", errs));
        }

        [Test]
        public void test_gate_ignoresDeclarationInStringLiteral()
        {
            // 夹具里 `"public const long TICKS_PER_DAY = 36000L;"` 就是这个形状
            List<string> errs = TimeConstantSingleDefinitionGate.Evaluate(Files(
                (AllowedPathLiteral, "public const long TICKS_PER_DAY = 36000L;"),
                ("Assets/Tests/FakeFixture.cs",
                 "static string S = \"public const long TICKS_PER_DAY = 36000L;\";\n" +
                 "static string V = @\"public const int TicksPerDay = 1;\";")));

            Assert.That(errs, Is.Empty,
                "字符串字面量里的声明不得误报;红行:\n" + string.Join("\n", errs));
        }

        [Test]
        public void test_gate_ignoresDeclarationInBlockComment()
        {
            List<string> errs = TimeConstantSingleDefinitionGate.Evaluate(Files(
                (AllowedPathLiteral, "public const long TICKS_PER_DAY = 36000L;"),
                ("Assets/Sim/Notes.cs",
                 "/*\n * const long TICKS_PER_DAY = 480L;\n */\npublic static class Notes { }")));

            Assert.That(errs, Is.Empty,
                "块注释里的声明不得误报;红行:\n" + string.Join("\n", errs));
        }

        [Test]
        public void test_gate_reportsCorrectLineAfterMultiLineComment()
        {
            // 剥离若把换行吃掉,行号就会指错 —— 这条钉住「抹字符但保换行」
            const string Body =
                "/* 第一行\n" +
                "   第二行\n" +
                "   第三行 */\n" +
                "public const long TICKS_PER_DAY = 36000L;";

            List<string> errs = TimeConstantSingleDefinitionGate.Evaluate(Files(
                ("Assets/Sim/Elsewhere.cs", Body)));

            Assert.That(errs, Is.Not.Empty, "真实声明必须被抓住");
            Assert.That(string.Join("\n", errs), Does.Contain("Assets/Sim/Elsewhere.cs:4"),
                "行号须为 4(块注释占前三行);实际红行:\n" + string.Join("\n", errs));
        }

        // ─────────────────────────────────────────────────────────────────────
        // 数据侧(⑪–⑭):AC-5-05 的「52 数据集中零字面副本」那半边。
        // ⚠️ 该半边的**当日状态是空真**(assets/data 对日长零命中),不是「已验证」;
        //    ⑪–⑫ 证明的是「一旦出现就红」,不是「52 的数据集正确」。
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public void test_gateDataSet_detectsLiteralCopy()
        {
            List<string> errs = TimeConstantSingleDefinitionGate.EvaluateData(Files(
                ("assets/data/52_random_events.json",
                 "{\n  \"ticks_per_day\": 36000\n}")));

            Assert.That(errs, Is.Not.Empty, "52 数据集里出现日长字面副本必须红");
            Assert.That(string.Join("\n", errs),
                Does.Contain("assets/data/52_random_events.json:2"),
                "红行须点名文件与行号;实际:\n" + string.Join("\n", errs));
        }

        [Test]
        public void test_gateDataSet_detectsAllNameForms()
        {
            // 下划线 / 驼峰 / 连字符三种写法是同一个量,必须都命中
            foreach (string key in new[] { "TICKS_PER_DAY", "ticksPerDay", "ticks-per-day" })
            {
                List<string> errs = TimeConstantSingleDefinitionGate.EvaluateData(Files(
                    ("assets/data/x.json", "{ \"" + key + "\": 36000 }")));

                Assert.That(errs, Is.Not.Empty, $"写法 `{key}` 必须命中");
            }
        }

        [Test]
        public void test_gateDataSet_isNameBasedNotValueBased()
        {
            // **判据边界**(不是「通过」):本门按**名字**判,不按值判。
            // 52 若把日长写成 `{"day_length": 36000}`,本门看不见 —— 这是已登记的盲区。
            // ⚠️ 本测试的存在意义是**钉住边界的形状**,不是为盲区背书;
            //    若将来设计了按值/按 schema 的判据,本测试须随之改写或删除。
            List<string> errs = TimeConstantSingleDefinitionGate.EvaluateData(Files(
                ("assets/data/x.json", "{ \"day_length\": 36000, \"timeout\": 120 }")));

            Assert.That(errs, Is.Empty,
                "换名同值是已知盲区 —— 若这条突然变红,说明判据被扩成了按值匹配(须重估误报面)");
        }

        [Test]
        public void test_gateDataSet_redWhenDataRootMissing()
        {
            List<string> errs = TimeConstantSingleDefinitionGate.ScanDataSide(
                "/no/such/assets/data/__gate_test__");

            Assert.That(errs, Is.Not.Empty,
                "数据集目录不存在 ⇒ 必须红(`assets/data/` 已入库 47 件,缺面 ≠ 通过)");
        }
    }
}
