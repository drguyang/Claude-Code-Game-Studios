// Go/No-Go ② 的直测断言(批次 F · 2026-10-10)—— `AssemblyGates.RunAll()` 真树零红。
//
// 存在理由(「门须有强制点」口径):RunAll = b3 装配封闭 + **b2 门 A(Sim 引用集白名单)**
// + b6 载荷手搓 + b7 写者存在性 四门的唯一合流入口(菜单 / reload / 构建前门三处共用),
// 但测试侧此前**零直调** —— b4/b6 有各自反射直调(assembly_gate_b4/b6_test)、b7 有
// WriterGate 双入口(writer_existence_gate_test:278),**b2 从未被任何测试直接执行**
// (reload 只跑 b3/b2 且只落日志,不构成测试证据)。本测试把 RunAll 整体接成可复跑判据,
// 供 Go/No-Go ②「b6 门 + Sim 引用集门绿」逐字取绿(与编辑器日志证据互补,不互相替代)。
//
// 权威:ADR-017 §二(b2 门 A 硬化)· ADR-025 §①/§④(b3)· ADR-029 §③(b6)·
//      writer_existence_gate(b7,2026-10-10)· sprint-04.md:154 Go/No-Go ②。

using System.Linq;
using DaYiJingCheng.EditorTools.Gates;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Unit.Sim
{
    /// <summary>
    /// `AssemblyGates.RunAll` 端到端绿断言(b3 + b2 + b6 + b7 合流)。
    /// </summary>
    public class AssemblyGateRunAllTest
    {
        /// <summary>
        /// RunAll 零红 —— 任一门(b3 装配封闭 / b2 Sim 引用集 / b6 载荷手搓 / b7 写者存在性)
        /// 报错即断言失败,红行全文进失败消息(可直接定位)。
        /// </summary>
        [Test]
        public void test_assemblyGates_runAll_zeroErrors_b3_b2_b6_b7()
        {
            var errs = AssemblyGates.RunAll();

            Assert.That(errs, Is.Empty,
                () => $"AssemblyGates.RunAll 红行 {errs.Count} 条:\n" +
                      string.Join("\n", errs));
        }

        /// <summary>
        /// 防空转假绿:RunAll 的四门扫描面须非空(空集 ⇒ 门形同虚设,同 b7 三假绿防护口径)。
        /// 反射取 b6 扫描目录字段断言其非空(b2/b3/b7 的扫描面由各自测试守护,此处取一即可证
        /// RunAll 内部门体不是空壳)。
        /// </summary>
        [Test]
        public void test_assemblyGates_runAll_scanSurfaceIsNonEmpty()
        {
            var dirsField = typeof(AssemblyGates).GetField(
                "PayloadRefScanDirs",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.Static);
            Assert.IsNotNull(dirsField, "PayloadRefScanDirs 字段应在(b6 扫描面登记点)");
            var dirs = dirsField.GetValue(null) as System.Collections.IEnumerable;
            Assert.IsNotNull(dirs, "b6 扫描目录不可为 null");
            Assert.IsTrue(dirs.Cast<object>().Any(),
                "b6 扫描目录非空 —— 空集 = 门扫不到任何文件(假绿防护)");
        }
    }
}
