// AC-7a-09 忘词令门(SaveVocabularyGate)的行为钉子(2026-10-25)。
//
// 存在理由:门只有「清单登记」(gate_wiring_manifest_test)时零行为证据。
// 本文件 = 谓词的绿断言 + 三条守卫自检 + **Cecil 内存注入负例**(证明门真的会红)。
//
// ⚠️ 负例为什么能成立:`CheckSymbolsIn` 是纯函数层(吃 `AssemblyDefinition`),
// 测试可**程序化构造**一个含 `DoQuickSave` 方法的类型注入 —— 不需要改仓库源码、
// 不需要真 dll。这是「门会红」的可复算证明(承 S12-4 证伪纪律:夹具须有判别力)。
//
// ⚠️ 本文件源码**不含连续触发词字面** —— 触发词以源码级拼接形态出现(`"quick" + "save"`),
// 防未来任何源码文本级工具误伤本文件。⚠️ 判据**不得**从 `SaveVocabularyGate.ForbiddenTerms`
// 取值构造 —— 那是自指判据(证伪轮 MV3 实测:词表失真时探针跟着动 ⇒ 零红逃逸)。

using System;
using DaYiJingCheng.EditorTools.Gates;
using Mono.Cecil;
using Mono.Cecil.Cil;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Unit.Sim
{
    /// <summary>
    /// AC-7a-09:忘词令门的行为钉子。
    /// </summary>
    public class SaveVocabularyGateTest
    {
        /// <summary>对真实运行期装配跑门链入口 ⇒ 零红。</summary>
        [Test]
        public void test_vocabularyGate_greenOnAllRuntimeAssemblies()
        {
            var errs = SaveVocabularyGate.RunAll();
            Assert.IsEmpty(errs,
                "忘词令对运行期 8 装配应零红,实测:\n" + string.Join("\n", errs));
        }

        /// <summary>
        /// 词表内容钉死(不只是非空):规则七点名的三个触发词必须始终在表内。
        /// <para>⚠️ 判据用**源码级拼接字面量**(`"quick" + "save"`),不经
        /// <c>ForbiddenTerms</c> 取值 —— 否则词表失真时判据跟着一起动(自指判据,
        /// 证伪轮 MV3 实测零红逃逸)。拼接形态同时保住本文件「不含连续触发词字面」的约束。</para>
        /// </summary>
        [Test]
        public void test_vocabularyGate_forbiddenTerms_notEmpty()
        {
            Assert.IsTrue(SaveVocabularyGate.ForbiddenTerms.Length > 0,
                "忘词令词表不得为空(空 ⇒ 门零判别)—— 规则七点名的三个触发词必须始终在表内");
            CollectionAssert.Contains(SaveVocabularyGate.ForbiddenTerms, "quick" + "save",
                "词表缺 quick 系第一词(规则七点名)—— 词表被失真时本判据必须红");
            CollectionAssert.Contains(SaveVocabularyGate.ForbiddenTerms, "quick" + "load",
                "词表缺 quick 系第二词(规则七点名)");
            CollectionAssert.Contains(SaveVocabularyGate.ForbiddenTerms, "save" + "now",
                "词表缺 save-now 形态(规则七点名;标识符中只可能是 SaveNow 拼写)");
        }

        /// <summary>扫描集非空(清空扫描集 ⇒ 门零覆盖 —— 同上)。</summary>
        [Test]
        public void test_vocabularyGate_runtimeAssemblies_notEmpty()
        {
            Assert.IsTrue(SaveVocabularyGate.RuntimeAssemblies.Length > 0,
                "扫描装配清单不得为空(空 ⇒ 门零覆盖)");
            // 且运行期装配至少涵盖存档服务所在的 Sim.Codec(忘词令的主语)
            CollectionAssert.Contains(SaveVocabularyGate.RuntimeAssemblies, "Sim.Codec",
                "忘词令扫描集必须含 Sim.Codec(7a 服务所在地)");
        }

        /// <summary>
        /// **负例(核心)**:内存构造一个含 `DoQuickSave` 方法的类型 ⇒ 谓词必须红。
        /// <para>这是「门真的会红」的可复算证明;若此断言失败,门对违规符号零判别力。</para>
        /// </summary>
        [Test]
        public void test_vocabularyGate_quickSaveSymbol_injected_isRed()
        {
            using (var asm = BuildProbeAssembly(
                       probeTypeName: "ProbeTypes.ProbeCartridge",
                       probeMemberName: "Do" + ("quick" + "save").ToUpperInvariant() + "Now"))
            {
                var errs = SaveVocabularyGate.CheckSymbolsIn(asm, out int scanned, "VocabProbe");
                Assert.IsTrue(scanned > 0, "探针装配须至少扫到 1 个类型");
                Assert.IsTrue(errs.Count > 0,
                    "含 q 系列符号的装配必须被门打红 —— 零命中即门零判别力,实测:\n" +
                    string.Join("\n", errs));
                StringAssert.Contains("忘词令命中", errs[0]);
            }
        }

        /// <summary>
        /// 负例的对照:同样的探针装配,**不含**违规符号 ⇒ 谓词绿。
        /// <para>(缺了它,负例的红可能只是「谓词对任何东西都红」。)</para>
        /// </summary>
        [Test]
        public void test_vocabularyGate_cleanProbe_isGreen()
        {
            using (var asm = BuildProbeAssembly(
                       probeTypeName: "ProbeTypes.ProbeCartridge",
                       probeMemberName: "LoadCartridge"))
            {
                var errs = SaveVocabularyGate.CheckSymbolsIn(asm, out int scanned, "VocabProbeClean");
                Assert.IsTrue(scanned > 0);
                Assert.IsEmpty(errs,
                    "干净探针装配必须绿(否则门是「永远红」的假门),实测:\n" +
                    string.Join("\n", errs));
            }
        }

        /// <summary>Cecil 内存构造一个含单类型单方法的探针装配(不写盘)。</summary>
        private static AssemblyDefinition BuildProbeAssembly(string probeTypeName, string probeMemberName)
        {
            var asmName = new AssemblyNameDefinition("VocabProbe", new Version(1, 0));
            var asm = AssemblyDefinition.CreateAssembly(asmName, "vocabprobe", ModuleKind.Dll);
            var module = asm.MainModule;

            var type = new TypeDefinition(
                probeTypeName.Split('.')[0],
                probeTypeName.Split('.')[1],
                TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Sealed,
                module.TypeSystem.Object);
            var method = new MethodDefinition(
                probeMemberName,
                MethodAttributes.Public | MethodAttributes.Static,
                module.TypeSystem.Void);
            method.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
            type.Methods.Add(method);
            module.Types.Add(type);

            return asm;
        }
    }
}
