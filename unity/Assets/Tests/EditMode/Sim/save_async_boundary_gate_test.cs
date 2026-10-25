// AC-7a-07 门行为钉子(2026-10-25 证伪轮 MA20 的直接产物)。
//
// 存在理由:门此前只有**清单登记**(gate_wiring_manifest_test 的字符串条目),零行为测试
// —— 「有门」的自述没有能证明它真的会红/真的会绿的命令。证伪轮 MA20(清空
// WorkerTypeFullNames)期望覆盖守卫转红,实测**绿** ⇒ 暴露「空清单逃逸」真缺口;
// 修门(空清单守卫)后,本测试 = MA20 的转红载体:变异态下门红 ⇒ `test_green` 红。
//
// 判据口径(承 CLAUDE.md「某件有门的自述须给 file:line + 可跑命令」):
//   · 门谓词 = SaveAsyncBoundaryGate.CheckAsyncBoundaryIl(SaveAsyncBoundaryGate.cs:104)
//   · 本文件 = 谓词的绿断言 + 两条不绿断言(产物缺失 / 断言面自检)
//   · 变异转红记录见 production/qa/evidence/impl-td-persist-05-act1-2026-10-25.md §七

using System.Linq;
using DaYiJingCheng.EditorTools.Gates;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Unit.Sim
{
    /// <summary>
    /// AC-7a-07 IL 门(SaveAsyncBoundaryGate)的行为钉子。
    /// </summary>
    public class SaveAsyncBoundaryGateTest
    {
        /// <summary>
        /// 对**真实** Sim.Codec.dll 跑门谓词 ⇒ 零错误。
        /// <para>⚠️ 这条同时是 MA20 变异的转红载体:清单被清空 ⇒ 覆盖守卫(⓪)报
        /// 「清单被清空」⇒ 本测红。</para>
        /// </summary>
        [Test]
        public void test_asyncBoundaryGate_greenOnRealCodecDll()
        {
            string dll = AssemblyGates.ScriptAssemblyPath(SaveAsyncBoundaryGate.CodecAssemblyName);
            var errs = SaveAsyncBoundaryGate.CheckAsyncBoundaryIl(dll);
            Assert.IsTrue(errs.Count == 0,
                "AC-7a-07 门对真实 Sim.Codec.dll 应零红,实测:\n" + string.Join("\n", errs));
        }

        /// <summary>
        /// 产物缺失不得静默绿(拒以空集冒充绿)。
        /// </summary>
        [Test]
        public void test_asyncBoundaryGate_missingDll_isNotSilentlyGreen()
        {
            var errs = SaveAsyncBoundaryGate.CheckAsyncBoundaryIl(
                "/nonexistent/__no_such_dll__/Sim.Codec.dll");
            Assert.IsTrue(errs.Count > 0, "产物缺失须报错,不得以空错误列表冒充绿。");
            Assert.IsTrue(errs[0].Contains("扫描面不存在"),
                "缺失报错须说明「扫描面不存在」,实测:" + errs[0]);
        }

        /// <summary>
        /// 清单非空自检:空清单 ⇒ 门零覆盖(承 MA20 的缺口修法 —— 覆盖守卫 ⓪ 只在
        /// dll 存在时才可达,这里从测试侧直接钉清单不变量)。
        /// </summary>
        [Test]
        public void test_asyncBoundaryGate_workerList_coversRealTypes()
        {
            Assert.IsTrue(SaveAsyncBoundaryGate.WorkerTypeFullNames.Length > 0,
                "WorkerTypeFullNames 不得为空(空 ⇒ 门零覆盖)。");
            string dll = AssemblyGates.ScriptAssemblyPath(SaveAsyncBoundaryGate.CodecAssemblyName);
            var errs = SaveAsyncBoundaryGate.CheckAsyncBoundaryIl(dll);
            Assert.IsTrue(errs.Count == 0,
                "覆盖守卫应在清单类型与真实类型不一致时转红,实测:\n" + string.Join("\n", errs));
        }
    }
}
