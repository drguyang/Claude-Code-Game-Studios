// 阶段 1 词法器 —— 结构性拒收判据(2026-10-22 立)。
//
// ══════════════════════════════════════════════════════════════════════════
// 立此文件的缘由(实测,不是假想)
// ══════════════════════════════════════════════════════════════════════════
// 2026-10-22 写 9 注册表烘焙管线时,注入测试喂进 `{ "schema_version": 1, ` ——
// **截断的 JSON**。预期阶段 1 词法拒。实际:词法**接受**了它,
// 一路走到绑定层报「缺 diseases 数组」。
//
// 根因:`ReadValue` 的对象/数组分支用 `while (reader.Read())` 收元素,
// 循环退出有**两条**路径 —— `EndObject`(合法)与 `Read()` 返回 false(输入耗尽),
// 而旧代码**不区分**二者 ⇒ 截断的容器被当成完整容器收下。
//
// ⚠️ 后果不是「报错不够早」,是**报错指向的地方不是坏掉的地方**。
//    作者看到「缺 diseases」会去补字段,而真正的问题是少了一个 `}`。
//    这是共享词法器,所有 baker 都踩得到。
//
// 权威:ADR-014 §三(阶段 1 仅词法)· `JsonStage1Lexer.cs`

using DaYiJingCheng.EditorTools.Bake;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Unit.Sim
{
    /// <summary>词法层:截断容器必须被拒,且错误须指向**截断**而非下游缺字段。</summary>
    [TestFixture]
    public sealed class JsonStage1LexerTruncationTest
    {
        /// <summary>对象未闭合 ⇒ 拒,错误含「未闭合」。</summary>
        [Test]
        public void test_truncatedObject_isRejectedAsUnclosed()
        {
            bool ok = JsonStage1Lexer.TryParse("{ \"a\": 1, ", out JsonNode root, out string err);

            Assert.That(ok, Is.False, "截断对象被词法接受 —— 错误会飘到下游,指向错误的位置");
            Assert.That(root, Is.Null);
            Assert.That(err, Does.Contain("未闭合"));
        }

        /// <summary>数组未闭合 ⇒ 拒。</summary>
        [Test]
        public void test_truncatedArray_isRejectedAsUnclosed()
        {
            bool ok = JsonStage1Lexer.TryParse("{ \"a\": [1, 2", out _, out string err);

            Assert.That(ok, Is.False, "截断数组被词法接受");
            Assert.That(err, Does.Contain("未闭合"));
        }

        /// <summary>嵌套两层,内层截断 ⇒ 拒(防只修了顶层)。</summary>
        [Test]
        public void test_truncatedNestedObject_isRejected()
        {
            bool ok = JsonStage1Lexer.TryParse("{ \"a\": { \"b\": 1 ", out _, out string err);

            Assert.That(ok, Is.False);
            Assert.That(err, Does.Contain("未闭合"));
        }

        // ══════════════════════════════════════════════════════════════════════
        // 反例:完整输入不得被新判据误伤(判据过宽 = 假红)
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>完整对象照常通过 —— 空对象也要通过。</summary>
        [Test]
        public void test_wellFormedObjects_stillParse()
        {
            Assert.That(JsonStage1Lexer.TryParse("{}", out _, out string e1), Is.True, e1);
            Assert.That(JsonStage1Lexer.TryParse("[]", out _, out string e2), Is.True, e2);
            Assert.That(JsonStage1Lexer.TryParse(
                "{ \"a\": 1, \"b\": [1, {\"c\": 2}], \"d\": null }", out _, out string e3), Is.True, e3);
        }

        /// <summary>尾随内容仍被拒(既有判据,新改动不得让它失效)。</summary>
        [Test]
        public void test_trailingContent_stillRejected()
        {
            Assert.That(JsonStage1Lexer.TryParse("{ \"a\": 1 } { \"b\": 2 }", out _, out _), Is.False);
        }

        /// <summary>空输入仍被拒(既有判据)。</summary>
        [Test]
        public void test_emptyInput_stillRejected()
        {
            Assert.That(JsonStage1Lexer.TryParse("   ", out _, out _), Is.False);
            Assert.That(JsonStage1Lexer.TryParse(null, out _, out _), Is.False);
        }
    }
}
