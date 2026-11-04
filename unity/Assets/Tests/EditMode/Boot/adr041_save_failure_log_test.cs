// ADR-041 §D8 / §Validation **V-9**:落笔失败的日志措辞与载入失败**分开**。
//
// 为什么单开一件:ADR-041 之前两条失败共用一个 sink(`LogLoadFailure`)⇒「覆写被拒」
// 会打成 `[7b][载入] 槽位 N 载入失败`,**文字与行为不符**。D8 把它拆成 `LogSaveFailure`。
// 只改代码不留判据 ⇒ 任何人把两条再合回去都**不会红**(自述顶替门,承 `.claude/docs/coding-standards.md`
// §假说形态 F6)。本件就是那条「合回去会红」的判据。
//
// ⚠️ 夹具**直接调生产方法**(经反射,`BootRoot.LogSaveFailure` / `LogLoadFailure`)——
//    不是复刻一份日志格式再断言自己(`F1 自造断言`),也不是构造假 sink 冒充(`F2 假件零命中`)。
//    若方法被改名/删除,`SetUp` 的 `Assert.IsNotNull` 会当场点名。
//
// 判据的形态 = **正则锚定在前缀上**(`^\[7b\]\[落笔\] …`):
//   一旦日志用错前缀,`LogAssert.Expect` 匹配不上 ⇒ 那条日志变成「未预期的错误」⇒ 测试红。
//   ⇒ 「不出现 `[7b][载入]` 措辞」是**机械可判**的,不靠人工读日志。

using System;
using System.Reflection;
using System.Text.RegularExpressions;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Sim.Codec;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DaYiJingCheng.Tests.Boot
{
    [TestFixture]
    public sealed class Adr041SaveFailureLogTest
    {
        private static MethodInfo _logSaveFailure;
        private static MethodInfo _logLoadFailure;

        [SetUp]
        public void SetUp()
        {
            _logSaveFailure = typeof(BootRoot).GetMethod(
                "LogSaveFailure", BindingFlags.NonPublic | BindingFlags.Static);
            _logLoadFailure = typeof(BootRoot).GetMethod(
                "LogLoadFailure", BindingFlags.NonPublic | BindingFlags.Static);

            Assert.IsNotNull(_logSaveFailure,
                "找不到 BootRoot.LogSaveFailure —— 若被改名/删除,ADR-041 §D8 的判据随之失去载体");
            Assert.IsNotNull(_logLoadFailure,
                "找不到 BootRoot.LogLoadFailure —— 载入侧措辞的对照面");
        }

        private static void LogSaveFailure(uint slot, Exception ex)
            => _logSaveFailure.Invoke(null, new object[] { slot, ex });

        private static void LogLoadFailure(uint slot, Exception ex)
            => _logLoadFailure.Invoke(null, new object[] { slot, ex });

        // ══════════════════════════════════════════════════════════════════
        // ① 落笔失败:前缀 `[7b][落笔]` + 门名;绝不出现 `[7b][载入]`
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_saveFailure_narrativeGate_logsOwnPrefixAndGateName()
        {
            // 锚定前缀 + 槽号 + 门名三段:任一段变了都匹配不上
            LogAssert.Expect(LogType.Error, new Regex(
                @"^\[7b\]\[落笔\] 槽位 3 落笔被拒\(门:叙事上下文 / 装配缺失\):"));

            LogSaveFailure(3u, new InvalidOperationException(
                "不在叙事上下文内,拒绝写入 slot=3(AC-7a-19 (c))"));

            // 期望恰好消耗一次;若上面那条日志其实打成了 `[7b][载入]`,这里会报「未预期的错误日志」
            LogAssert.NoUnexpectedReceived();
        }

        // ══════════════════════════════════════════════════════════════════
        // ② 门名要点名(槽号非法 / 数据非法),不能只打异常全文
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_saveFailure_slotZero_gateIsNamedSlotInvalid()
        {
            LogAssert.Expect(LogType.Error, new Regex(
                @"^\[7b\]\[落笔\] 槽位 0 落笔被拒\(门:槽号非法\):"));

            LogSaveFailure(0u, new System.IO.InvalidDataException(
                "槽号 0 不是合法槽位:1 = 退出保存 · 2 = 自动存档 · 3+ = 手动存档"));

            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void test_saveFailure_otherInvalidData_gateIsNamedDataInvalid()
        {
            LogAssert.Expect(LogType.Error, new Regex(
                @"^\[7b\]\[落笔\] 槽位 4 落笔被拒\(门:数据非法\):"));

            LogSaveFailure(4u, new System.IO.InvalidDataException("组版失败:某些字段越界"));

            LogAssert.NoUnexpectedReceived();
        }

        // ══════════════════════════════════════════════════════════════════
        // ③ 载入侧原样:空槽仍是**普通日志**(正常态),真失败仍打 `[7b][载入]`
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_loadFailure_keepsItsOwnWording_andEmptySlotStaysInfoLevel()
        {
            // 空槽 = 正常态 ⇒ Log 级,不是错误(2026-10-31 裁定「甲」的成果,不得回归)
            LogAssert.Expect(LogType.Log, new Regex(
                @"^\[7b\]\[载入\] 槽位 5 是空的\(从未存过\)—— 正常态,不是损坏:"));

            LogLoadFailure(5u, new SaveNotFoundException(new SaveSlot(5u)));
            LogAssert.NoUnexpectedReceived();

            // 真失败 ⇒ 错误级,且前缀是 `[7b][载入]`(与落笔侧**互不冒充**)
            LogAssert.Expect(LogType.Error, new Regex(
                @"^\[7b\]\[载入\] 槽位 6 载入失败\(界面已受理、判定归 7a\):"));

            LogLoadFailure(6u, new InvalidOperationException("槽位 slot=6 已锁"));
            LogAssert.NoUnexpectedReceived();
        }
    }
}
