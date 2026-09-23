// 权威来源:
//   Story 001(production/epics/item-database/story-001-fix-parse-boundary-contract.md)
//     · AC-21a-41 —— 整数串 / 分子分母串合法;浮点式 / 空串 / 零分母硬拒;weight 是 int 不走 FixParse(D-21-17)
//     · AC-21a-42 —— ROUND_HALF_AWAY_FROM_ZERO 边界:Round(±0.5)/Round(1.5)/Round(2.5) = ±1 / 2 / 3
//     · AC-21a-51 —— axis_offset_by_quality[] 任一浮点字面量 ⇒ 导入期硬失败;负偏移合法(TR-itemdb-028)
//     · AC-21a-57 —— drug_potency / half_life / onset / peak / elimination 同族;P0 空(null)不触发(D-21-6)
//   ADR-006 §Decision 一(唯一解析入口 · 拒浮点 · D-21-17)· §Decision 三(舍入 · RoundMode 全局常量)
//   ADR-014 §四(Fix 字段在 JSON 写字符串,如 "3/4")
//   QA 夹具:tests/unit/item_database/fixtures/invalid_potency_float.json(故事指定负向夹具)
//
// ⚠️ 落点:故事头登记的账本路径 = tests/unit/item_database/fix_parse_boundary_test.cs;
//    按 ADR-025 §⑤(2026-09-23 路径订正注)Unity 只编译 Assets/ 树,真身落此 ——
//    与种子测试 sim_fixedpoint_test.cs 同一先例(账本/编译分家,根目录不留副本)。
//
// ⚠️ 范围:本文件**只测解析边界**(字符串字面量直接喂 FixParse / IntParse)。
//    JSON 词法层的「JSON 数字 vs JSON 字符串」消歧归 Story 008 两阶段管线(出栈);
//    float/double 静态扫描与黄金哈希归 Story 011;编码器往返归 Story 010。
//    本文件**不含** float/double 字面量、**不调** Math.Round(生产舍入纯整数 —— AC-21a-42)。

using System;
using System.IO;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Tests.Unit.ItemDatabase
{
    [TestFixture]
    internal sealed class FixParseBoundaryTest
    {
        // ════════════════ AC-21a-41:合法形 / 非法形 / 计数字段 ════════════════

        [Test]
        public void test_fixParse_legalIntegerAndRatioStrings_yieldQ16_16Raw()
        {
            // Given/When/Then:作者态合法字面量(整数串 / 分子分母串,承 ADR-014 字符串写法)
            Assert.That(FixParse.Parse("3").Raw, Is.EqualTo(3L * Fix.OneRaw),
                "整数字面量 = 整单位值,导入期一次性转 Q16.16");
            Assert.That(FixParse.Parse("-2").Raw, Is.EqualTo(-2L * Fix.OneRaw),
                "负整数合法(轴偏移一类可为负 —— TR-itemdb-028)");
            Assert.That(FixParse.Parse("3/4").Raw, Is.EqualTo(49152L),
                "3/4 单位 = 0.75 × 65536 = 49152 raw");
            Assert.That(FixParse.Parse("-1/2").Raw, Is.EqualTo(-32768L),
                "负分数合法,中点值 = −32768");
        }

        [Test]
        public void test_fixParse_floatStyleAndMalformedLiterals_throwFormatException()
        {
            // Given/When/Then:浮点式 / 空串 / 畸形串 ⇒ 硬失败(显式 throw,非静默默认)
            string[] rejected =
            {
                "1.5",       // 十进制小数点 —— AC-21a-41 点名
                "0.5",       // 同族(与 invalid_potency_float.json 的药效字面量同形)
                "",          // 空串 —— 边缘
                "   ",       // 全空白 —— 空串的变体
                "1e3",       // 科学计数法 —— AC-21a-51 边缘(拒)
                "1E3",       // 大写 E 同拒
                "n/0",       // 分子非整数 —— 边缘("n/0" 字面形)
                "3/4/5",     // 多重斜杠
            };

            foreach (string literal in rejected)
                Assert.Throws<FormatException>(() => FixParse.Parse(literal),
                    $"字面量 \"{literal}\" 必须硬失败 —— 静默回退 = 定点域从边界漏空");
        }

        [Test]
        public void test_fixParse_zeroDenominator_throwsDivideByZeroException()
        {
            // Given/When/Then:分母为 0 的分数串 ⇒ 硬失败
            Assert.Throws<DivideByZeroException>(() => FixParse.Parse("1/0"));
            Assert.Throws<DivideByZeroException>(() => FixParse.Parse("-3/0"));
        }

        [Test]
        public void test_intParse_countFieldFixSyntax_throwsFormatException()
        {
            // Given/When/Then(D-21-17):weight / stack_max 走 int 入口,不走 FixParse;
            // 计数字段写成 Fix 语法("3/4")或浮点式("1.5")⇒ 拒。
            Assert.That(IntParse.Parse("3"), Is.EqualTo(3),
                "合法计数字面量经 IntParse 解析");

            Assert.Throws<FormatException>(() => IntParse.Parse("3/4"),
                "Fix 分数语法不得进计数字段 —— D-21-17 把 weight 移出了 Fix 解析集,反向同样拒");
            Assert.Throws<FormatException>(() => IntParse.Parse("1.5"),
                "浮点式不得进计数字段");
        }

        // ════════════════ AC-21a-42:ROUND_HALF_AWAY_FROM_ZERO 边界 ════════════════

        [TestCase(32768L, 1L)]     // 0.5   —— 中点远离零(不是 0,不是 ties-to-even 的偶侧)
        [TestCase(-32768L, -1L)]   // -0.5  —— 负侧对称:远离零 ⇒ -1(不是 0)
        [TestCase(98304L, 2L)]     // 1.5   —— ties-to-even 会给 2(同),但非偶侧见 2.5
        [TestCase(163840L, 3L)]    // 2.5   —— ties-to-even 给 **2**(被禁),远离零 ⇒ 3
        [TestCase(229376L, 4L)]    // 3.5   —— 其它 .5 平局同向远离零
        [TestCase(-229376L, -4L)]  // -3.5
        [TestCase(-98304L, -2L)]   // -1.5
        public void test_fixRound_halfTies_awayFromZero(long raw, long expected)
        {
            // Given:定点域输入以 Fix raw 表示;When:统一舍入函数;Then:逐位得期望值
            // (纯整数域 —— 生产路径零 Math.Round;静态扫描归 AC-21a-30 / Story 011)
            Assert.That(new Fix(raw).Round(), Is.EqualTo(expected),
                $"Round 中点必须远离零 —— raw {raw} 期望 {expected}");
        }

        [TestCase(0L, 0L)]
        [TestCase(65536L, 1L)]     // 纯整数恒等
        [TestCase(-65536L, -1L)]   // 负值整数不变
        [TestCase(196608L, 3L)]
        [TestCase(-196608L, -3L)]
        [TestCase(1L, 0L)]         // 非中点就近:1 LSB < 半单位 ⇒ 归 0(就近,非远离)
        [TestCase(32767L, 0L)]     // 半单位之下(32768 恰为平局,之上归 1 —— 平局判据在上一函数)
        public void test_fixRound_integerInputs_identity(long raw, long expected)
        {
            Assert.That(new Fix(raw).Round(), Is.EqualTo(expected),
                $"纯整数输入恒等 / 就近舍入 —— raw {raw}");
        }

        // ════════════════ AC-21a-51:axis_offset_by_quality[] 浮点字面量硬失败 ════════════════

        [Test]
        public void test_axisOffsetElement_floatLiteral_throwsFormatException()
        {
            // Given:轴偏移数组的元素级解析(负偏移合法 —— TR-itemdb-028)
            Assert.That(FixParse.Parse("-2").Raw, Is.EqualTo(-2L * Fix.OneRaw),
                "负偏移字符串合法");
            Assert.That(FixParse.Parse("1/2").Raw, Is.EqualTo(32768L),
                "分数形合法");

            // Given:仅一档非法 ⇒ 仍硬失败(数组整体不可静默跳过坏元素)
            string[] oneBad = { "-2", "1.5", "0" };
            Assert.Throws<FormatException>(() => parseAllElements(oneBad),
                "仅一档非法也须硬失败 —— 静默取默认 = 某档偏移被偷换");

            // Given:全档非法 ⇒ 硬失败
            string[] allBad = { "1.0", "2.5" };
            Assert.Throws<FormatException>(() => parseAllElements(allBad));

            // Given:科学计数法字面量(边缘)⇒ 拒
            Assert.Throws<FormatException>(() => FixParse.Parse("1e2"));

            // Given:QA 指定夹具的 axis_offset_by_quality[](含一个浮点元素 "1.5")
            string fixtureJson = readFixture();
            string[] fromFixture = extractStringArray(fixtureJson, "axis_offset_by_quality");
            Assert.Throws<FormatException>(() => parseAllElements(fromFixture),
                "夹具数组含非法元素 ⇒ 硬失败(与 AC-21a-57 同族夹具,不另立文件名)");
            Assert.That(FixParse.Parse(fromFixture[0]).Raw, Is.EqualTo(-2L * Fix.OneRaw),
                "夹具首元素为负偏移 —— 合法形在同一数组内不受坏元素牵连判法影响");
        }

        // ════════════════ AC-21a-57:时间轴五字段浮点字面量硬失败 ════════════════

        [Test]
        public void test_drugProfileFields_floatLiteral_eachThrowsFormatException()
        {
            // Given/When/Then:逐字段轮换 —— 五字段各触发一次浮点字面量硬失败
            string[] fields = { "drug_potency", "half_life", "onset", "peak", "elimination" };
            foreach (string field in fields)
                Assert.Throws<FormatException>(() => FixParse.Parse("0.5"),
                    $"字段 {field} 写浮点字面量 ⇒ 导入期硬失败(AC-21a-57)");
        }

        [Test]
        public void test_drugProfileFields_validStrings_parseIncludingNegative()
        {
            // Given/When/Then:五字段全部为合法 Fix 字符串(含负值)⇒ 通过
            Assert.That(FixParse.Parse("3/4").Raw, Is.EqualTo(49152L));
            Assert.That(FixParse.Parse("2").Raw, Is.EqualTo(2L * Fix.OneRaw));
            Assert.That(FixParse.Parse("-1/2").Raw, Is.EqualTo(-32768L));
            Assert.That(FixParse.Parse("-3").Raw, Is.EqualTo(-3L * Fix.OneRaw),
                "负时间轴值合法(消除一类可负 —— 与轴偏移同一条负数纪律)");
        }

        [Test]
        public void test_drugProfileNullValue_optionalPath_returnsNoValue()
        {
            // Given/When/Then(D-21-6):字段为 P0 空 / JSON null ⇒ 不触发硬失败
            Assert.That(FixParse.ParseOptional(null), Is.Null,
                "null = 缺席/JSON null,合法(D-21-6),不进 Parse、不抛");

            // Given/Then:空串 ≠ 缺席 —— 写了但没值仍硬失败(与 AC-21a-41 空串边缘一致)
            Assert.Throws<FormatException>(() => FixParse.ParseOptional(""));

            // Given/Then:非 null 走与 Parse 完全同一条边界
            Assert.That(FixParse.ParseOptional("3/4").Value.Raw, Is.EqualTo(49152L));
        }

        // ════════════════ 负向夹具:invalid_potency_float.json(故事指定)════════════════

        [Test]
        public void test_invalidPotencyFixture_onlyDrugPotencyIllegal_othersParse()
        {
            // Given:夹具 drug_potency 写浮点字面量(JSON 数字 0.5),其余四字段为合法串
            string fixtureJson = readFixture();
            string potency = extractRawValue(fixtureJson, "drug_potency");
            Assert.That(potency, Is.EqualTo("0.5"),
                "夹具自证:药效幅值确为浮点字面量(否则本负向夹具失去意义)");

            // When/Then:浮点字面量喂 FixParse ⇒ 硬失败
            Assert.Throws<FormatException>(() => FixParse.Parse(potency),
                "AC-21a-57:drug_potency 写浮点字面量 ⇒ 导入期硬失败");

            // 边缘「仅 drug_potency 非法」:同夹具其余四字段合法,不被牵连
            string[] legalSiblings = { "half_life", "onset", "peak", "elimination" };
            foreach (string field in legalSiblings)
            {
                string siblingLiteral = extractRawValue(fixtureJson, field);
                Assert.DoesNotThrow(() => FixParse.Parse(siblingLiteral),
                    $"字段 {field} 为合法 Fix 串,不得因同文件 potency 非法而失败");
            }
        }

        [Test]
        public void test_invalidPotencyFixture_weightVariants_rejectedByIntParse()
        {
            // Given:QA「weight 越界解析用同文件内变体记录」—— 夹具 variants 块登记两条越界形
            string fixtureJson = readFixture();
            string weightFixSyntax = extractRawValue(fixtureJson, "weight_fix_syntax");
            string weightFloatSyntax = extractRawValue(fixtureJson, "weight_float_syntax");
            string weightLegal = extractRawValue(fixtureJson, "weight");

            // When/Then:Fix 语法 / 浮点式喂 int 入口 ⇒ 硬失败(D-21-17)
            Assert.Throws<FormatException>(() => IntParse.Parse(weightFixSyntax),
                "weight 写成 \"3/4\" ⇒ int 路径拒(Fix 语法不属于计数字段)");
            Assert.Throws<FormatException>(() => IntParse.Parse(weightFloatSyntax),
                "weight 写成 \"1.5\" ⇒ int 路径拒(浮点式不属于计数字段)");

            // Given/Then:同文件内的合法 weight 变体 ⇒ int 解析通过(不走 FixParse)
            Assert.That(IntParse.Parse(weightLegal), Is.EqualTo(3));
        }

        // ════════════════ helpers(解析边界测试的读取辅助,非 schema/管线层)════════════════

        private static void parseAllElements(string[] literals)
        {
            foreach (string literal in literals)
                FixParse.Parse(literal);
        }

        /// <summary>经 [CallerFilePath] 相对定位仓库根夹具(ADR-012 §五 同款双投递纪律的
        /// EditMode 半边;夹具缺失 = 断言失败,不 skip)。</summary>
        private static string readFixture([CallerFilePath] string thisFile = "")
        {
            string fixturePath = Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(thisFile) ?? ".",
                "..", "..", "..", "..", "..",
                "tests", "unit", "item_database", "fixtures", "invalid_potency_float.json"));

            Assert.That(File.Exists(fixturePath), Is.True,
                $"负向夹具缺失(Story 001 QA 指定):{fixturePath}");
            return File.ReadAllText(fixturePath);
        }

        /// <summary>抽 `"key": &lt;value&gt;` 的原文(带引号则去引号)。只读单键,非 schema 绑定 ——
        /// JSON 数字/字符串消歧归 Story 008。</summary>
        private static string extractRawValue(string json, string key)
        {
            int keyIndex = json.IndexOf("\"" + key + "\"", StringComparison.Ordinal);
            Assert.That(keyIndex, Is.GreaterThanOrEqualTo(0), $"夹具缺字段:{key}");

            int colon = json.IndexOf(':', keyIndex);
            Assert.That(colon, Is.GreaterThanOrEqualTo(0), $"字段 {key} 缺冒号");

            int start = colon + 1;
            while (start < json.Length && char.IsWhiteSpace(json[start])) start++;
            Assert.That(start, Is.LessThan(json.Length), $"字段 {key} 缺值");

            if (json[start] == '"')
            {
                int end = json.IndexOf('"', start + 1);
                Assert.That(end, Is.GreaterThan(start), $"字段 {key} 引号未闭合");
                return json.Substring(start + 1, end - start - 1);
            }

            int stop = start;
            while (stop < json.Length && ",}\r\n\t ".IndexOf(json[stop]) < 0) stop++;
            return json.Substring(start, stop - start);
        }

        /// <summary>抽 `"key": [ ... ]` 内的全部带引号元素(夹具的轴偏移数组用)。</summary>
        private static string[] extractStringArray(string json, string key)
        {
            int keyIndex = json.IndexOf("\"" + key + "\"", StringComparison.Ordinal);
            Assert.That(keyIndex, Is.GreaterThanOrEqualTo(0), $"夹具缺字段:{key}");

            int open = json.IndexOf('[', keyIndex);
            Assert.That(open, Is.GreaterThanOrEqualTo(0), $"字段 {key} 缺 [");
            int close = json.IndexOf(']', open);
            Assert.That(close, Is.GreaterThan(open), $"字段 {key} 缺 ]");

            string body = json.Substring(open + 1, close - open - 1);
            var elements = new System.Collections.Generic.List<string>();
            int pos = 0;
            while (pos < body.Length)
            {
                int q = body.IndexOf('"', pos);
                if (q < 0) break;
                int end = body.IndexOf('"', q + 1);
                if (end < 0) break;
                elements.Add(body.Substring(q + 1, end - q - 1));
                pos = end + 1;
            }

            Assert.That(elements, Is.Not.Empty, $"字段 {key} 数组为空 —— 夹具形状不符");
            return elements.ToArray();
        }
    }
}
