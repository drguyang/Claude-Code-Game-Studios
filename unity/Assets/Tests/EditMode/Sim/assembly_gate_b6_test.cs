// ADR-029 §③ 收口 —— b6 载荷手搓门(AssemblyGates)的判据测试
//
// 权威来源:
//   ADR-029 §③ —— Sim/ 内 `new PayloadRef(` = 违例,唯一合法路径 = IPayloadEncoder
//   ADR-006 Amendment G-2 —— PayloadRef { BlobId, Offset, Length },Offset = 字节偏移
//   Story: modular-building story-007 · world-ecozones story-006 的收口件

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Gates = DaYiJingCheng.EditorTools.Gates.AssemblyGates;

namespace DaYiJingCheng.Tests.Unit.Sim
{
    /// <summary>
    /// b6 门的判据测试。⚠️ 本类**不**断言「门恒过」——
    /// 那种断言是空转;它断言的是**扫描面与剥离逻辑真的在跑**,且**当前真实代码面通过**。
    /// </summary>
    public class AssemblyGateB6Test
    {
        private static MethodInfo GateMethod =>
            typeof(Gates).GetMethod("CheckPayloadRefCallsites",
                BindingFlags.NonPublic | BindingFlags.Static);

        private static MethodInfo StripMethod =>
            typeof(Gates).GetMethod("StripCommentsForScan",
                BindingFlags.NonPublic | BindingFlags.Static);

        [Test]
        public void test_b6_gateMethodExists()
        {
            Assert.IsNotNull(GateMethod,
                "b6 门方法须存在(ADR-029 §③)—— 缺失 = 判据无强制点");
            Assert.IsNotNull(StripMethod, "剥注释 helper 须存在");
        }

        [Test]
        public void test_b6_currentScanSourcePasses()
        {
            // 当前扫描面(Sim/ + Gameplay.Presentation/)须零命中
            // (两处接线支已把手搓面清除;O-6 表现层两写者已改走 IPayloadEncoder)
            var errs = new System.Collections.Generic.List<string>();
            GateMethod.Invoke(null, new object[] { errs });

            Assert.IsEmpty(errs,
                "扫描面内不得有 `new PayloadRef(`(ADR-029 §③):\n" + string.Join("\n", errs));
        }

        [Test]
        public void test_b6_scanDirsCoverPresentation()
        {
            // ⚠️ O-6(2026-10-09)回归面:原扫描面仅 `Assets/Sim` ⇒ 表现层两写者
            // (PlayerController / CellTransitionDetector)手搓伪引用门完全看不见。
            // 判据 = 扫描面数组须含这两个目录;从数组删任一项 ⇒ 该面退回零门 = 本测红。
            var dirsField = typeof(Gates).GetField("PayloadRefScanDirs",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(dirsField, "扫描面数组须存在(显式登记,不得内联魔法值)");

            var dirs = (string[])dirsField.GetValue(null);
            CollectionAssert.Contains(dirs, "Assets/Sim",
                "扫描面必须含 Assets/Sim(ADR-029 §③ 原面)—— 删除 = 原门失效");
            CollectionAssert.Contains(dirs, "Assets/Gameplay.Presentation",
                "扫描面必须含 Assets/Gameplay.Presentation(O-6 扩面)—— 删除 = 表现层退回零门");
        }

        [Test]
        public void test_b6_stripComments_removesLineAndBlockComments()
        {
            // ⚠️ 剥离逻辑必须真的工作 —— 否则文档里引用规则本身会被误判。
            // 实测:PoiStateMachine.cs / StructureKinds.cs 的 XML doc 里**就有**
            // 「本类内不得出现 `new PayloadRef(`」这句话;不剥离 ⇒ 门恒红。
            string src = "// new PayloadRef(1,2,3)\n"
                       + "/* new PayloadRef(4,5,6) */\n"
                       + "var x = 1;\n";
            string stripped = (string)StripMethod.Invoke(null, new object[] { src });

            Assert.IsFalse(stripped.Contains("new PayloadRef("),
                "行注释与块注释内的 `new PayloadRef(` 须被剥离(否则规则文档自伤)");
            Assert.IsTrue(stripped.Contains("var x = 1;"), "非注释代码须保留");
        }

        [Test]
        public void test_b6_stripComments_keepsRealCode()
        {
            // 负向半边:剥离**不得**把真代码也剥掉 —— 否则门变成恒过(空转)。
            string src = "var p = new PayloadRef(0, 0, 8);";
            string stripped = (string)StripMethod.Invoke(null, new object[] { src });

            Assert.IsTrue(stripped.Contains("new PayloadRef("),
                "真代码里的 `new PayloadRef(` **必须**保留 —— 否则门空转");
        }

        [Test]
        public void test_b6_entryUsesScanDirs_andDirsExist()
        {
            // ⚠️ 测试面 F-1 BLOCKING(2026-10-09 O-6 轮):既有三条判据各断言**数据**,
            //    没有一条证明「入口 CheckPayloadRefCallsites 真的把 PayloadRefScanDirs
            //    传给扫描体」,也没人管「扫描面目录不存在 ⇒ 门首行 continue 静默空转」。
            //    逃逸变异:入口改成硬编码 {"Assets/Sim"} 或空实现 ⇒ 既往三条全绿,
            //    O-6 原病(门看不见表现层)静默复发。
            var dirsField = typeof(Gates).GetField("PayloadRefScanDirs",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(dirsField, "扫描面数组须存在");
            var dirs = (string[])dirsField.GetValue(null);
            Assert.IsNotEmpty(dirs, "扫描面为空 ⇒ 门恒过");

            // (a) 扫描面目录必须真的存在 —— 不存在 ⇒ IsEmpty 无法区分
            //     「零违例」与「零扫描」(反空转)。
            foreach (var d in dirs)
                Assert.IsTrue(Directory.Exists(d),
                    $"扫描面目录不存在 ⇒ b6 门静默空转:`{d}`"
                    + $"(cwd = {Directory.GetCurrentDirectory()})");

            // (b) 入口接线的负向证明:数组**元素**可写(引用 readonly 不挡元素)——
            //     把首元素指向**子目录探针**,走**入口**方法(而非扫描体),断言恰 1 条。
            string tmp = Path.Combine(Path.GetTempPath(),
                "o6_b6_entry_" + Guid.NewGuid().ToString("N"));
            string sub = Path.Combine(tmp, "Sub");
            Directory.CreateDirectory(sub);
            var snapshot = (string[])dirs.Clone();
            try
            {
                // 探针放子目录 —— 兼验 SearchOption.AllDirectories(逃逸变异:TopDirectoryOnly)
                File.WriteAllText(Path.Combine(sub, "Bad.cs"),
                    "class Bad { void M() { var r = new PayloadRef(1, 2, 3); } }");
                dirs[0] = tmp;

                var errs = new System.Collections.Generic.List<string>();
                GateMethod.Invoke(null, new object[] { errs });

                Assert.AreEqual(1, errs.Count,
                    "入口须把 PayloadRefScanDirs 传给扫描体(硬编码/空实现 ⇒ 本测红),实测 "
                    + errs.Count + " 条:\n" + string.Join("\n", errs));
                StringAssert.Contains("Bad.cs", errs[0],
                    "探针须在**子目录**被递归扫到(深度判据)");
            }
            finally
            {
                for (int i = 0; i < snapshot.Length; i++) dirs[i] = snapshot[i];
                if (Directory.Exists(tmp)) Directory.Delete(tmp, true);
            }
        }

        [Test]
        public void test_b6_codecIsExcludedFromScan()
        {
            // Sim.Codec 是**唯一合法**构造处(编码器实现体)。判据 = **行为**断言:
            // 门跑完后,错误集里**不得**出现 Sim.Codec 的路径 —— 若扫描面误含它,
            // PayloadEncoder 自身会被判违例(门恒红)。
            var errs = new System.Collections.Generic.List<string>();
            GateMethod.Invoke(null, new object[] { errs });

            Assert.IsFalse(errs.Any(e => e.Contains("Sim.Codec")),
                "门不得报 Sim.Codec 内的构造(那是合法处):\n" + string.Join("\n", errs));

            // 且 Sim.Codec 里**确实有** `new PayloadRef(` —— 证明排除是必要的,不是空设
            string codecDir = Path.Combine(Application.dataPath, "Sim.Codec");
            bool codecHasCtor = Directory.GetFiles(codecDir, "*.cs", SearchOption.AllDirectories)
                .Any(f => File.ReadAllText(f).Contains("new PayloadRef("));
            Assert.IsTrue(codecHasCtor,
                "Sim.Codec 内应有 `new PayloadRef(` —— 若没有,本测的排除前提失效,须复核");
        }

        [Test]
        public void test_b6_waiversAreNamedAndBounded()
        {
            // 豁免须是**具名 baseline**(具体路径 + 行号),不是整文件/整目录豁免 ——
            // 否则门在该文件上永久失效(空转)。
            var waiversField = typeof(Gates).GetField("PayloadRefWaivers",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(waiversField, "豁免表须存在(即使为空,也须显式)");

            // ⚠️ ValueTuple 的字段名经反射不可靠(编译器可能只留 Item1/Item2/Item3)——
            //    故**按位置**取,并显式断言元数,而非按名取。
            var waivers = (System.Collections.IEnumerable)waiversField.GetValue(null);
            int count = 0;
            foreach (var w in waivers)
            {
                count++;
                var t = w.GetType();
                var fields = t.GetFields();
                Assert.AreEqual(3, fields.Length,
                    $"豁免条目须为三元组(路径/行号/原因),实测 {fields.Length} 元");

                int line = (int)fields[1].GetValue(w);
                string reason = (string)fields[2].GetValue(w);

                Assert.Greater(line, 0, "豁免须点名**具体行号**(不得整文件豁免)");
                Assert.IsNotEmpty(reason, "豁免须附原因与出口条件");
                StringAssert.Contains("出口条件", reason, "豁免须写明出口条件(否则成永久债务)");
            }

            // ✅ 2026-10-03(story-007):豁免表**已清空** —— 手搓面全部改走 IPayloadEncoder。
            // 空表 = 全库零手搓,门在**无豁免**状态下强制。
            Assert.AreEqual(0, count,
                $"豁免表应为空(现 {count} 条)—— 新增豁免 = 新增已登记债务,须显式复核");
        }
    }
}
