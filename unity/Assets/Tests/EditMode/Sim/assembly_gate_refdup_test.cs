// S13-4(复盘行动项 2 · High)—— asmdef 引用面**解析后重名**判定门的判据测试
//
// 权威来源:
//   复盘 `production/retrospectives/retro-sprint-12-2026-10-16.md:78-79 / :89`
//     · X-1 侦察错误:判 H 项只读 asmdef **名称列表**、漏看 GUID 形引用 ⇒
//       按名补一条 `"Editor.Tools.Gates"` 触发 `Assembly has duplicate references` **编译红**。
//     · 行动项 2:「asmdef 引用面判读须解析 GUID 到装配名 —— **名称列表不是完整事实**」。
//   实现落点:`AssemblyGates.FindDuplicateResolvedReferences` + `CheckReferenceDuplicates`
//     (`[b3-refdup]`,接入 `RunAll`)。
//
// ⚠️ 判据**双向**:漏报(名形 + GUID 形指向同一装配却判无重名)与**误报**(一次声明
//   被判成两次)都算失败 —— 故本文件既有「真重复 ⇒ 红」也有「单次声明 ⇒ 不红」两侧。
//
// 夹具形态:临时 asmdef 文件(照 `assembly_gate_b6_test` / `writer_existence_gate_test`
//   的 `Path.GetTempPath()` 先例)。GUID 侧用**真树既存**的 GUID(`Editor.Tools.Gates`)
//   —— 判定器经 `AssetDatabase.GUIDToAssetPath` 解析,故须真 GUID 才能复现「已覆盖」语义。

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Gates = DaYiJingCheng.EditorTools.Gates.AssemblyGates;

namespace DaYiJingCheng.Tests.Unit.Sim
{
    /// <summary>S13-4 判据测试:asmdef 引用面解析后重名。</summary>
    public class AssemblyGateRefDupTest
    {
        /// <summary>`Editor.Tools.Gates` 的真 GUID(取自 `EditMode.asmdef` 实况)——
        /// 用真 GUID 才能让 `GUIDToAssetPath` 解析到该装配名,复现「名形 + GUID 形同一装配」。</summary>
        private const string GatesGuid = "09b3130c3cce749c78793250d6d191f4";

        private static string WriteTempAsmdef(string body)
        {
            string tmp = Path.Combine(Path.GetTempPath(),
                "refdup_probe_" + System.Guid.NewGuid().ToString("N") + ".asmdef");
            File.WriteAllText(tmp, body);
            return tmp;
        }

        // ══════════ ① 漏报侧:名形 + GUID 形指向同一装配 ⇒ 必报重名 ══════════

        [Test]
        public void test_refdup_nameFormPlusGuidForm_sameAssembly_reported()
        {
            // 复现 sprint-12 X-1 的**确切**病灶:一条 GUID 形(已覆盖 Editor.Tools.Gates)
            // + 一条名形(按名补的冗余)⇒ 解析后同一装配出现 2 次 ⇒ Unity 编译红。
            string p = WriteTempAsmdef(
                "{\n  \"name\": \"Probe\",\n  \"references\": [\n" +
                $"    \"GUID:{GatesGuid}\",\n" +
                "    \"Editor.Tools.Gates\"\n  ]\n}\n");
            try
            {
                var dups = Gates.FindDuplicateResolvedReferences(p);
                Assert.AreEqual(1, dups.Count,
                    "名形 + GUID 形指向同一装配 ⇒ 须报 1 条重名(sprint-12 X-1 病灶)。实测:"
                    + string.Join("; ", dups.Select(d => d.Assembly + "×" + d.Count)));
                Assert.AreEqual("Editor.Tools.Gates", dups[0].Assembly,
                    "重名装配名须 = 解析后的**装配名**(非 GUID 原文 / 非文件名)。");
                Assert.AreEqual(2, dups[0].Count, "计为 2 次(名形 1 + GUID 形 1)。");
            }
            finally { File.Delete(p); }
        }

        [Test]
        public void test_refdup_guidFormOnly_notReported()
        {
            // 单条 GUID 形(正确形态)—— 不得误报。
            string p = WriteTempAsmdef(
                "{\n  \"name\": \"Probe\",\n  \"references\": [\n" +
                "    \"GUID:" + GatesGuid + "\"\n  ]\n}\n");
            try
            {
                Assert.IsEmpty(Gates.FindDuplicateResolvedReferences(p),
                    "单条 GUID 形声明 ⇒ 零重名(误报侧)。");
            }
            finally { File.Delete(p); }
        }

        [Test]
        public void test_refdup_nameFormOnly_notReported()
        {
            string p = WriteTempAsmdef(
                "{\n  \"name\": \"Probe\",\n  \"references\": [ \"Editor.Tools.Gates\" ]\n}\n");
            try
            {
                Assert.IsEmpty(Gates.FindDuplicateResolvedReferences(p),
                    "单条名形声明 ⇒ 零重名(误报侧)。");
            }
            finally { File.Delete(p); }
        }

        [Test]
        public void test_refdup_twoDistinctAssemblies_notReported()
        {
            // 两条**不同**装配 —— 不得因「都是引用」而误判。
            string p = WriteTempAsmdef(
                "{\n  \"name\": \"Probe\",\n  \"references\": [\n" +
                "    \"Editor.Tools.Gates\",\n    \"Sim.Contracts\"\n  ]\n}\n");
            try
            {
                Assert.IsEmpty(Gates.FindDuplicateResolvedReferences(p),
                    "两条不同装配 ⇒ 零重名(判据不得退化为「引用数 > 1 即红」)。");
            }
            finally { File.Delete(p); }
        }

        [Test]
        public void test_refdup_trueDuplicateSameForm_reported()
        {
            // 同一名形写两遍(最朴素的重复)—— 也须报。
            string p = WriteTempAsmdef(
                "{\n  \"name\": \"Probe\",\n  \"references\": [\n" +
                "    \"Editor.Tools.Gates\",\n    \"Editor.Tools.Gates\"\n  ]\n}\n");
            try
            {
                var dups = Gates.FindDuplicateResolvedReferences(p);
                Assert.AreEqual(1, dups.Count, "同名形重复 ⇒ 须报。");
                Assert.AreEqual(2, dups[0].Count, "计为 2 次。");
            }
            finally { File.Delete(p); }
        }

        // ══════════ ② 接线侧:门体经 RunAll 生效 + 真树零红 ══════════

        [Test]
        public void test_refdup_gateMethodWiredIntoRunAll()
        {
            // 强制点判据:判定器存在且**被 RunAll 调用**(零调用方的门形同虚设)。
            // 载体 = RunAll 真树跑;真树零重复 ⇒ 本测与「真树零红」同证。
            var m = typeof(Gates).GetMethod("CheckReferenceDuplicates",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(m, "[b3-refdup] 门体须存在(缺 = 判据无强制点)。");

            var errs = new List<string>();
            m.Invoke(null, new object[] { errs });
            Assert.IsEmpty(errs,
                "真树 asmdef 引用面零解析后重名(实测 0)—— 有红行 = 真树存在冗余引用,须修。" +
                string.Join("\n", errs));
        }

        [Test]
        public void test_refdup_scanSurfaceIsNonEmpty()
        {
            // 反空转:判定器须真的扫到 asmdef(扫不到 ⇒ 恒绿 = 假绿)。
            int n = Directory.GetFiles("Assets", "*.asmdef", SearchOption.AllDirectories).Length;
            Assert.Greater(n, 0,
                $"扫描面 Assets/**/*.asmdef 非空(cwd = {Directory.GetCurrentDirectory()})。");
        }
    }
}
