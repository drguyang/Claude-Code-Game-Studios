// 阶段 1 词法器 —— **重复属性名**拒收(`TD-medcons-10` 结案夹具,2026-10-29 立)。
//
// ══════════════════════════════════════════════════════════════════════════
// 立此文件的缘由(债册原文 + 实测)
// ══════════════════════════════════════════════════════════════════════════
// `ReadValue` 把对象属性收进 `List<KeyValuePair<string, JsonNode>>` —— **同名键可同时存在**,
// 旧代码 `Props.Add` 前零检查。后果不是「报错不够早」,是**同一份 JSON 有两种读法**:
// 下游各 binder 有的线性取**首个**、有的建 `Dictionary` 让**后者覆盖** ⇒ 「哪一份生效」
// 取决于读者实现,改一个 binder 的取法就能让同一数据换含义 = 静默丢字段。
// JSON 本身要求名字唯一(RFC 8259 §4),重复即作者态错误 ⇒ 在**词法层**当场红。
//
// ⚠️ 共享词法器 ⇒ 这是 10+ 个 binder 的共同入口(ADR-014 阶段 1),不是某一系统的私有面。
//
// 权威:ADR-014 §三(阶段 1 仅词法)· `JsonStage1Lexer.cs` · 债册 `TD-medcons-10`

using DaYiJingCheng.EditorTools.Bake;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Unit.Sim
{
    /// <summary>词法层:对象内同名属性必须被拒,且错误须点名**重复的键**。</summary>
    [TestFixture]
    public sealed class JsonStage1LexerDuplicateKeyTest
    {
        /// <summary>顶层对象重复键 ⇒ 拒,错误含键名与「重复」。</summary>
        /// <remarks>判别力:删掉 `Props.Add` 前的重名循环 ⇒ 本测转红(旧行为是静默收下两条)。</remarks>
        [Test]
        public void test_duplicateKey_topLevel_isRejected_namingTheKey()
        {
            bool ok = JsonStage1Lexer.TryParse("{ \"schema_version\": 1, \"schema_version\": 2 }",
                out JsonNode root, out string err);

            Assert.That(ok, Is.False, "重复键被词法静默接受 —— 下游「取首 / 取后」会得不同值");
            Assert.That(root, Is.Null);
            Assert.That(err, Does.Contain("schema_version"), "错误须点名重复的键:" + err);
            Assert.That(err, Does.Contain("重复"), "错误须说明「重复」(而非泛化的语法错):" + err);
        }

        /// <summary>嵌套对象(数组元素内)重复键 ⇒ 同样拒(防只修了顶层)。</summary>
        [Test]
        public void test_duplicateKey_nestedInsideArray_isRejected()
        {
            bool ok = JsonStage1Lexer.TryParse(
                "{ \"rows\": [ { \"outcome\": \"residual\", \"outcome\": \"recurrence\" } ] }",
                out _, out string err);

            Assert.That(ok, Is.False, "嵌套层重复键漏网 = 判据只覆盖顶层");
            Assert.That(err, Does.Contain("outcome"));
        }

        /// <summary>重复键在**第二处**才出现 ⇒ 仍拒(判据不是「首个属性」的特例)。</summary>
        [Test]
        public void test_duplicateKey_secondOpening_isRejected_too()
        {
            bool ok = JsonStage1Lexer.TryParse(
                "{ \"a\": 1, \"b\": 2, \"c\": 3, \"b\": 4 }", out _, out string err);

            Assert.That(ok, Is.False);
            Assert.That(err, Does.Contain("\"b\""));
        }

        // ══════════════════════════════════════════════════════════════════════
        // 反例:判据过宽 = 假红。下列输入**必须**照常通过。
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// 同名字段出现在**不同对象**里是合法的(数组两行都有 `outcome`)——
        /// 这条是判据正确性的关键反例:把「重名检查」误做成「全文出现过一次即拒」会在这里红。
        /// </summary>
        [Test]
        public void test_sameKeyInDifferentObjects_stillParses()
        {
            Assert.That(JsonStage1Lexer.TryParse(
                "{ \"rows\": [ { \"outcome\": 1 }, { \"outcome\": 2 } ] }", out _, out string e), Is.True, e);
        }

        /// <summary>键名互为前缀 / 大小写不同 ⇒ 不是重复(精确比较,不做归一化)。</summary>
        [Test]
        public void test_keyPrefixOrCaseDifference_isNotDuplicate()
        {
            Assert.That(JsonStage1Lexer.TryParse(
                "{ \"delay\": 1, \"delay_ticks\": 2, \"Delay\": 3 }", out _, out string e), Is.True, e);
        }

        /// <summary>空对象 / 单属性对象照常通过。</summary>
        [Test]
        public void test_emptyAndSingleProperty_stillParse()
        {
            Assert.That(JsonStage1Lexer.TryParse("{}", out _, out string e1), Is.True, e1);
            Assert.That(JsonStage1Lexer.TryParse("{ \"a\": 1 }", out _, out string e2), Is.True, e2);
        }
    }
}
