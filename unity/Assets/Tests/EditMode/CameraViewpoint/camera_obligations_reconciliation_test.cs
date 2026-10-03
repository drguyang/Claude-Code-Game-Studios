// camera-viewpoint Story 006 测试 —— 跨系统义务对账(AC-2-22 六项)+ EXTERNAL 登记
//
// AC-2-22 [B]: O-12/O-13/O-14/O-15/O-16 已落 —— 判据 = 各对方 GDD §Dependencies 内**反向引用**
// AC-2-21 [EXTERNAL]: PITCH_MAX 取值归用户(OQ-1-14)
// AC-2-23 [EXTERNAL]: O-13 验收只可能由相机 spike 给出
// AC-2-03/24/26 [ADVISORY]: 舒适度 playtest 签核面(非自动化)
//
// 权威来源: GDD camera-and-viewpoint.md AC-2-22 · design-docs 规则「依赖必须双向」
//           ADR-020 §Migration 第 5 条(spike)· systems-index.md(接收方权威表)

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace DaYiJingCheng.Tests.CameraViewpoint
{
    public class CameraObligationsReconciliationTest
    {
        private static string GddDir => Path.GetFullPath(
            Path.Combine(Application.dataPath, "..", "..", "design", "gdd"));

        private static string ReadGdd(string name)
        {
            var p = Path.Combine(GddDir, name + ".md");
            return File.Exists(p) ? File.ReadAllText(p) : null;
        }

        /// <summary>
        /// AC-2-22 的判据本体:对方 GDD 内出现对本 GDD 的反向引用。
        /// </summary>
        private static void AssertReverseReference(string gddName, string obligation)
        {
            var text = ReadGdd(gddName);
            Assert.IsNotNull(text,
                $"[{obligation}] 对方 GDD「{gddName}.md」**不存在** ⇒ 反向引用判据不可执行(不借绿)");
            Assert.IsTrue(text.Contains("camera-and-viewpoint"),
                $"[{obligation}] 「{gddName}.md」须在其 §Dependencies 内**反向引用** " +
                "`camera-and-viewpoint.md`(design-docs 规则:依赖必须双向)");
        }

        // ══════════════ AC-2-22 六项 ══════════════

        [Test]
        public void test_ac222_1_emergencyProcedures_reverseReference()
        {
            // ① 10 在急救开始/结束(含跳过路径)发 Treatment / Explore
            AssertReverseReference("emergency-procedures", "AC-2-22①");
        }

        [Test]
        public void test_ac222_2_casebook_reverseReference()
        {
            // ② 39(唯一请求方)在脉案打开/关闭时发 Casebook / Explore(8 不再发)
            AssertReverseReference("casebook", "AC-2-22②");
        }

        [Test]
        public void test_ac222_4_playerController_reverseReference()
        {
            // ④ 系统 1 侧确认 YawBasis 的**帧内次序契约**(O-14)
            AssertReverseReference("player-controller-and-movement", "AC-2-22④");
        }

        [Test]
        public void test_ac222_5_skeuomorphicUi_declaresFovV()
        {
            // ⑤ 42 已声明其读取的档位 FOV_v(O-15;2026-09-22 履行)
            AssertReverseReference("skeuomorphic-ui", "AC-2-22⑤");
        }

        [Test]
        public void test_ac222_6_inputSystem_reverseReference()
        {
            // ⑥ 系统 3 保证手柄 Navigate 与 Look 不共享物理控件(O-16)
            AssertReverseReference("input-system", "AC-2-22⑥");
        }

        [Test]
        public void test_ac222_3_levelContent_hasNoGdd_notApplicable()
        {
            // 🔴 **AC-2-22③ 的接收方「关卡内容 + ADR-015 §一 烘焙逻辑层几何」**
            //    —— 评审订正明说二者**均无 GDD / 无独立系统条目**
            //    ⇒ **③ 在「各对方 GDD §Dependencies 反向引用」这一判据形式下不可签**
            //      (没有 GDD 就没有 §Dependencies 可查)。
            //    ⇒ 与 AC-2-23(EXTERNAL)同型:**义务已定义,裁决点在别处**(相机 spike)。
            //    本测**显式登记该不可签性**,不以「文件不存在 ⇒ 跳过」静默记绿。
            var worldGdd = ReadGdd("world-and-ecozones");
            Assert.IsNotNull(worldGdd, "world-and-ecozones.md 须存在(6 的 GDD)");
            Assert.IsFalse(worldGdd.Contains("camera-and-viewpoint"),
                "6 的 GDD **当前未**反向引用本 GDD —— 若已引用,说明接收方已变,须复核 ③ 的归属");

            Assert.Ignore(
                "NOT-APPLICABLE: AC-2-22③ 的接收方(关卡内容 + ADR-015 §一 烘焙逻辑层几何)" +
                "**无 GDD** ⇒ 「对方 GDD 反向引用」判据形式**不可执行**;" +
                "该义务的验收归 **AC-2-23(EXTERNAL)的相机 spike**(ADR-020 §Migration 第 5 条)。" +
                "**不借绿** —— 此处显式登记不可签性,而非静默通过。");
        }

        [Test]
        public void test_ac222_obligationsHaveDeclaredReceivers()
        {
            // 机械前提:六项义务的接收方须**逐一有出处**(不是「大概有人管」)
            var doc = File.ReadAllText(Path.Combine(GddDir, "camera-and-viewpoint.md"));
            foreach (var o in new[] { "O-12", "O-13", "O-14", "O-15", "O-16" })
                Assert.IsTrue(doc.Contains(o),
                    $"义务 {o} 须在本 GDD 内被点名(AC-2-22 的六子义务登记面)");
        }

        // ══════════════ AC-2-21 / AC-2-23:EXTERNAL 登记面 ══════════════

        [Test]
        public void test_ac221_external_pitchMaxAwaitsUserDecision()
        {
            // AC-2-21(EXTERNAL):PITCH_MAX 取值须回填系统 1 的 OQ-1-14 并结案
            // ⚠️ EXTERNAL = 「义务已定义、裁决点在别处」⇒ **不计入就绪度,但须显式登记**
            var pc = ReadGdd("player-controller-and-movement");
            Assert.IsNotNull(pc, "系统 1 的 GDD 须存在");

            bool closed = pc.Contains("OQ-1-14") &&
                          (pc.Contains("已结案") || pc.Contains("已裁"));
            if (!closed)
            {
                Assert.Ignore(
                    "EXTERNAL: AC-2-21 的裁决点**在用户手里** —— `PITCH_MAX` 取值须回填 " +
                    "系统 1 的 `OQ-1-14` 并结案。**该义务不因未结案而消失**,只是移出就绪度计数;" +
                    "**不得记为本 Epic 的绿**。");
            }
        }

        [Test]
        public void test_ac223_external_spikeIsSoleAcceptance()
        {
            // AC-2-23(EXTERNAL):O-13 的验收**只可能**由相机 spike 给出
            var adr = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..",
                "docs", "architecture", "adr-020-player-controller-and-camera.md"));
            Assert.IsTrue(File.Exists(adr), "ADR-020 须存在");
            var text = File.ReadAllText(adr);

            Assert.IsTrue(text.Contains("spike"),
                "ADR-020 §Migration 须含相机 spike 条款(AC-2-23 的唯一验收依据)");

            Assert.Ignore(
                "EXTERNAL: AC-2-23 的验收 = **相机 spike 输出**(ADR-020 §Migration 第 5 条);" +
                "2 侧**不可签署** ⇒ 登记外部依赖,**不计入就绪度**。**不得记为绿**。");
        }

        // ══════════════ AC-2-03 / 24 / 26:ADVISORY 签核面 ══════════════

        [Test]
        public void test_ac203_comfort_isAdvisoryNotBlocking()
        {
            // AC-2-03(ADVISORY):「不晕」是**平面模式舒适度的唯一门**且是 ADVISORY
            // ⇒ P0 **无可签署的舒适度 BLOCKING 门**(有意为之),
            //   但**须在 spike 报告里显式签核**,否则「不晕」变成无人负责的口号。
            var doc = File.ReadAllText(Path.Combine(GddDir, "camera-and-viewpoint.md"));
            Assert.IsTrue(doc.Contains("AC-2-03"),
                "AC-2-03 须在 GDD 内登记(舒适度门)");

            // ⚠️ 本测**不得**把 ADVISORY 混入 BLOCKING 计数,也不得用机械判据「替代」签核
            Assert.Ignore(
                "ADVISORY: AC-2-03「不晕」须 **playtest 签核**(非自动化断言);" +
                "P0 舒适度**无 BLOCKING 门**是有意设计,但须在 spike 报告内**显式签核** —— " +
                "否则「不晕」成为无人负责的口号。**不得用机械判据替代**。");
        }

        [Test]
        public void test_ac226_advisoryGatesAreThree_notMixedIntoBlocking()
        {
            // 三条 ADVISORY 手感门(03/24/26)= P0 舒适度的**全部签核面**
            var doc = File.ReadAllText(Path.Combine(GddDir, "camera-and-viewpoint.md"));
            foreach (var ac in new[] { "AC-2-03", "AC-2-24", "AC-2-26" })
                Assert.IsTrue(doc.Contains(ac), $"ADVISORY 门 {ac} 须在 GDD 内登记");

            // 机械前提:这三条在 GDD 内须标 ADVISORY(不得混入 BLOCKING 计数)
            foreach (var ac in new[] { "AC-2-03", "AC-2-24", "AC-2-26" })
            {
                int i = doc.IndexOf(ac, StringComparison.Ordinal);
                Assert.Greater(i, 0);
                string line = doc.Substring(i, Math.Min(200, doc.Length - i));
                Assert.IsTrue(line.Contains("ADVISORY"),
                    $"{ac} 须标 **ADVISORY**(禁混入 BLOCKING 计数;GDD 明文「两者应答的问题不同」)");
            }
        }

        // ══════════════ VR 接口面(P0 只落枚举与结构预留)══════════════

        [Test]
        public void test_vrInterface_firstPersonEnumExists_planeChainIndependent()
        {
            // TR-camera-002:P0 只落 `CameraMode.FirstPerson` 枚举与链冻结的结构预留
            Assert.IsTrue(Enum.IsDefined(typeof(DaYiJingCheng.Gameplay.Presentation.Camera.CameraMode),
                                        "FirstPerson"),
                "CameraMode.FirstPerson 须存在(P0 只落枚举,实现推 P1a)");

            // ⚠️ OQ-2-6(`ICameraRig.Camera` 单相机 vs 双眼)的接口扩容须**不破坏平面链**;
            //    P0 只需保证这一点 ⇒ 本测只验平面链仍完好(接口扩容归 P1a)
            var rig = typeof(DaYiJingCheng.Gameplay.Presentation.Camera.ICameraRig);
            Assert.IsNotNull(rig.GetProperty("YawBasis"), "平面链的 YawBasis 须完好");
            Assert.IsNotNull(rig.GetProperty("Yaw"));
            Assert.IsNotNull(rig.GetProperty("Pitch"));
        }
    }
}
