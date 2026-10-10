// BCD-码-4(2026-10-10 · 登记债收口)—— b4 `ToFloat()` 调用点白名单门的判据测试
//
// 权威来源:
//   ADR-025 §② 甲案 —— `Fix.ToFloat()` 消费约束 = 构建期调用点白名单断言
//     (白名单 = {Sim.Codec, Gameplay.*};`Sim` 内调用 = 构建失败)
//   BCD 评审码-4(2026-10-10)—— 原 `asmDirs` 是方法内局部字典且**缺 Gameplay.Boot**:
//     Boot 内的生产 ToFloat(体征投影桥 `DiseaseVitalsService`)在门下零覆盖 =
//     删面 / 漏白名单都无门可红。
//
// 本文件与 b6(`assembly_gate_b6_test.cs`)/ b7(`writer_existence_gate_test.cs`)同格:
//   结构断言(扫描面 / 白名单含 Boot)+ 行为断言(真树跑门零红 —— Boot 投影出口被
//   白名单放行)。⚠️ 不断言「门恒过」—— 断言的是面与白名单真的在、且真代码面通过。

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Gates = DaYiJingCheng.EditorTools.Gates.AssemblyGates;

namespace DaYiJingCheng.Tests.Unit.Sim
{
    /// <summary>b4 门的判据测试(BCD-码-4 收口件)。</summary>
    public class AssemblyGateB4Test
    {
        private static MethodInfo GateMethod =>
            typeof(Gates).GetMethod("CheckToFloatCallsites",
                BindingFlags.NonPublic | BindingFlags.Static);

        [Test]
        public void test_b4_gateMethodExists()
        {
            Assert.IsNotNull(GateMethod, "b4 门方法须存在(ADR-025 §②)—— 缺失 = 判据无强制点");
        }

        [Test]
        public void test_b4_scanDirs_containsGameplayBoot()
        {
            // ⚠️ BCD-码-4 回归面:原扫描面四字典(Sim / Sim.Codec / Presentation / UI)
            // **不含 Boot** ⇒ 体征投影桥的 ToFloat 门完全不可见。删 Boot 条目 ⇒ 本测红。
            var dirsField = typeof(Gates).GetField("ToFloatScanDirs",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(dirsField, "b4 扫描面须提为字段(显式登记,不得内联魔法值)");

            var dirs = (Dictionary<string, string>)dirsField.GetValue(null);
            Assert.IsTrue(dirs.ContainsKey("Gameplay.Boot"),
                "扫描面必须含 Gameplay.Boot(BCD-码-4)—— 删除 = 体征投影桥退回零门");
            Assert.AreEqual("Assets/Gameplay.Boot", dirs["Gameplay.Boot"],
                "Boot 面路径须指对目录(错路径 = Directory.Exists 恒 false = 静默零扫描)");
        }

        [Test]
        public void test_b4_whitelist_containsGameplayBoot()
        {
            // ⚠️ 白名单与扫描面必须**同批**:只补面不补白名单 ⇒ DiseaseVitalsService 的
            // 生产 ToFloat(:174)立即假红;只补白名单不补面 ⇒ 门仍不可见。删条目 ⇒ 本测红。
            var wlField = typeof(Gates).GetField("ToFloatWhitelistPrefixes",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(wlField, "b4 白名单须存在(ADR-025 §② 甲案)");

            var prefixes = (string[])wlField.GetValue(null);
            Assert.Contains("Gameplay.Boot", prefixes,
                "白名单必须含 Gameplay.Boot(ADR-025 §② 甲案 Gameplay.*;Boot = 全案唯一" +
                "生产 ToFloat 投影出口)—— 删除 = Boot 面进门即假红");
        }

        [Test]
        public void test_b4_currentScanSourcePasses()
        {
            // 真树跑门:当前扫描面(Sim/ + Sim.Codec/ + Presentation/ + UI/ + Boot/)
            // 零 [b4] 红 —— 证 Boot 的 ToFloat(:174)真被白名单放行,而非「没扫到」。
            var errs = new List<string>();
            GateMethod.Invoke(null, new object[] { errs });

            Assert.IsEmpty(errs,
                "扫描面内不得有白名单外的 ToFloat() 调用(ADR-025 §② 甲案):\n" +
                string.Join("\n", errs));
        }
    }
}
