// AC-7a-08 ③ 测试 —— 存档写盘可达面的 IL 门。
//
// 权威来源:
//   design/gdd/persistence-service.md:517-522 —— AC-7a-08(BLOCKING)三方守之第三:
//     「IL 正面断言(仅 `SaveService` 类可达 `FileStream.Write` / `File.Move`)」
//   ADR-010 §四 —— 原子写 = tmp + flush + rename
//
// 被测件:Editor.Tools.Gates/SaveWritePathGate.cs(2026-10-25 新立)
//
// ⚠️ 本文件**不得**写仓库(FsWriteBoundaryGate):零文件 I/O、零 `Application.dataPath`
//    —— 唯一例外是 `AssemblyGates.ScriptAssemblyPath`,它是**读** Library 下的产物,
//    且已在 `Declarations` 里有先例声明(`assembly_gate_b6_test`)。见文件尾负例说明。

using System.Collections.Generic;
using System.Linq;
using DaYiJingCheng.EditorTools.Gates;
using NUnit.Framework;
using UnityEditor;

// ⚠️ 命名空间须是既有约定 `DaYiJingCheng.Tests.Unit.Sim`(本目录其余 12 件同款)。
//    首版误写 `DaYiJingCheng.Tests.Sim` —— 它**遮蔽**了 `DaYiJingCheng.Sim`,
//    导致别处 `Sim.Contracts.X` 被解析成 `DaYiJingCheng.Tests.Sim.Contracts` ⇒
//    `prescribe_flow_test.cs:498` 报 CS0234(与本文件毫无关系的文件转红)。
namespace DaYiJingCheng.Tests.Unit.Sim
{
    /// <summary>`SaveWritePathGate` 的正面绿 + 负面红 + 形状守卫三面。</summary>
    public class SaveWritePathGateTest
    {
        private static string CodecDll() => AssemblyGates.ScriptAssemblyPath(
            SaveWritePathGate.CodecAssemblyName);

        // ══════════════════════════════════════════════════════════════════
        // 正面:真树零红(AC-7a-08③ 的生产态)
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_ac7a08c_realTree_zeroRed()
        {
            Assert.That(EditorUtility.scriptCompilationFailed, Is.False,
                "跑 Cecil 扫描前提:编译成功(扫上一版 DLL = 假绿)");

            var errs = SaveWritePathGate.CheckSaveWritePathIl(CodecDll());

            Assert.That(errs, Is.Empty,
                "`Sim.Codec` 内写盘调用须全部落在 SaveService::WriteAtomic 一处:\n"
                + string.Join("\n", errs));
        }

        // ══════════════════════════════════════════════════════════════════
        // 形状守卫:扫描面非空(防空集冒充绿)
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_ac7a08c_writeSurfaceIsNotEmpty()
        {
            Assert.That(EditorUtility.scriptCompilationFailed, Is.False, "编译成功前提");

            var errs = SaveWritePathGate.CheckSaveWritePathIl(CodecDll(), out var hits);
            Assert.That(errs, Is.Empty, string.Join("\n", errs));

            Assert.That(hits, Is.Not.Empty,
                "写盘命中清单不得为空 —— 空集 = 门扫的是空气(这正是本门存在的理由之一)");

            // 命中须真的落在白名单那一处(形状判据,不只是「有命中」)。
            // ⚠️ 排除**类级允许、方法级不约束**的那一类(目录准备,见门内
            //    `ClassLevelOnlyApiNames` 的判据)—— 它们**允许**出现在别的方法里,
            //    但**不允许**出现在别的类里(那由 test_ac7a08c_realTree_zeroRed 守)。
            string writePrefix = SaveWritePathGate.AllowedWriteTypeFullName + "::" +
                                 SaveWritePathGate.AllowedWriteMethodName;

            var writingHits = hits
                .Where(h => SaveWritePathGate.ClassLevelOnlyApiNames.All(a => !h.Contains(a)))
                .ToList();

            Assert.That(writingHits, Is.Not.Empty,
                "「写存档」类命中不得为空 —— 只剩目录准备意味着写盘被搬走了");

            Assert.That(writingHits.All(h => h.StartsWith(writePrefix)), Is.True,
                "全部**写存档**命中须落在 SaveService::WriteAtomic 一处;实测:\n" +
                string.Join("\n", hits));
        }

        [Test]
        public void test_ac7a08c_whitelistedMethodReallyWritesToDisk()
        {
            // 「白名单方法存在」不足以证明门有判别力 —— 它必须**真的**含写盘调用。
            // 这一条正面钉住「原子写原语不是空壳」。
            var errs = SaveWritePathGate.CheckSaveWritePathIl(CodecDll(), out var hits);
            Assert.That(errs, Is.Empty, string.Join("\n", errs));

            string prefix = SaveWritePathGate.AllowedWriteTypeFullName + "::" +
                            SaveWritePathGate.AllowedWriteMethodName;

            Assert.That(hits.Any(h => h.StartsWith(prefix) && h.EndsWith("File.WriteAllBytes")),
                Is.True,
                "WriteAtomic 须含 File.WriteAllBytes(写 tmp);实测命中:\n" + string.Join("\n", hits));
            Assert.That(hits.Any(h => h.StartsWith(prefix) && h.EndsWith("File.Move")),
                Is.True,
                "WriteAtomic 须含 File.Move(tmp → final 的原子替换);实测命中:\n"
                + string.Join("\n", hits));
        }

        // ══════════════════════════════════════════════════════════════════
        // 负面:产物缺失 ⇒ 红(拒以空集冒充绿)
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_ac7a08c_missingDll_isRed_notSilentlyGreen()
        {
            var errs = SaveWritePathGate.CheckSaveWritePathIl(
                "/nonexistent/path/Sim.Codec.dll");

            Assert.That(errs, Is.Not.Empty,
                "产物缺失须记红 —— 静默返回空列表 = 门恒绿(与 EcozoneIntegerGates 同纪律)");
            Assert.That(errs.Any(e => e.Contains("拒以空集冒充绿")), Is.True,
                "报错须点名「拒以空集冒充绿」口径");
        }

        // ══════════════════════════════════════════════════════════════════
        // 白名单常量的形态(改它 = 改 AC 判面,须整批改)
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_ac7a08c_whitelistShape_isSingleTypeSingleMethod()
        {
            Assert.AreEqual("DaYiJingCheng.Sim.Codec.SaveService",
                SaveWritePathGate.AllowedWriteTypeFullName,
                "白名单类型须是 SaveService(改它 = 改 AC-7a-08 的判面)");
            Assert.AreEqual("WriteAtomic",
                SaveWritePathGate.AllowedWriteMethodName,
                "白名单方法须是唯一的原子写原语 WriteAtomic(W4 补正后的单一实现)");
        }

        [Test]
        public void test_ac7a08c_writeApiList_isSharedWithFsWriteBoundaryGate()
        {
            // 两份字面量会漂移,而漂移是静默的 —— 本门从 FsWriteBoundaryGate 派生名单。
            CollectionAssert.AreEquivalent(
                FsWriteBoundaryGate.WriteApiNames, SaveWritePathGate.WriteApiNames,
                "写盘 API 名单须是同一份(单一真源)");
            Assert.That(SaveWritePathGate.WriteApiNames, Is.Not.Empty,
                "名单不得为空 —— 空名单 ⇒ 永远扫不到任何写盘调用 ⇒ 门恒绿");
        }
    }
}
