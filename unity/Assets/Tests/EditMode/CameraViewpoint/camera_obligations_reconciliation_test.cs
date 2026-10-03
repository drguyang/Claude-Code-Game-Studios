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

        /// <summary>取 §Dependencies 节正文(定位 `## Dependencies` → 下一个 `## `)。</summary>
        private static string DependenciesSection(string text)
        {
            var m = System.Text.RegularExpressions.Regex.Match(
                text, @"^##\s+Dependencies\s*$", System.Text.RegularExpressions.RegexOptions.Multiline);
            if (!m.Success) return null;
            var rest = text.Substring(m.Index + m.Length);
            var nx = System.Text.RegularExpressions.Regex.Match(
                rest, @"^##\s+", System.Text.RegularExpressions.RegexOptions.Multiline);
            return nx.Success ? rest.Substring(0, nx.Index) : rest;
        }

        /// <summary>
        /// AC-2-22 的判据本体:对方 GDD 的 **§Dependencies 节内**(行级)出现对本 GDD 的反向引用。
        /// 🔴 **2026-10-03 评审修复(B-1)**:初版是**整档全文 `Contains`** ⇒ 正文里随便提一句即绿
        /// (故事自陈的 Negative fixture「删 deps 行」会**照样通过**)。现改为
        /// **节内定位 + 行级匹配**,并断言义务编号在文档中登记。
        /// </summary>
        private static void AssertReverseReference(string gddName, string obligation, string oNum)
        {
            var text = ReadGdd(gddName);
            Assert.IsNotNull(text,
                $"[{obligation}] 对方 GDD「{gddName}.md」**不存在** ⇒ 反向引用判据不可执行(不借绿)");

            var deps = DependenciesSection(text);
            Assert.IsNotNull(deps,
                $"[{obligation}] 「{gddName}.md」须有 `## Dependencies` 节(§Deps 内反向引用是判据对象)");

            // ① §Deps 节内**行级**引用(仅节内 —— 正文里的提及不算)
            bool lineHit = false;
            foreach (var line in deps.Split('\n'))
                if (line.Contains("camera-and-viewpoint") || line.Contains("系统 2"))
                { lineHit = true; break; }
            Assert.IsTrue(lineHit,
                $"[{obligation}] 「{gddName}.md」的 **§Dependencies 节内**须有**行级**引用 " +
                "`camera-and-viewpoint` 或「系统 2」(整档 grep 不算;B-1)");

            // ② 义务编号须在该 GDD 内登记(§Deps 或正文皆可 —— 部分义务本登记在正文)
            Assert.IsTrue(text.Contains(oNum),
                $"[{obligation}] 义务编号 `{oNum}` 须在「{gddName}.md」内登记");
        }

        // ══════════════ AC-2-22 六项 ══════════════

        [Test]
        public void test_ac222_1_emergencyProcedures_reverseReference()
        {
            // ① 10 在急救开始/结束(含跳过路径)发 Treatment / Explore
            AssertReverseReference("emergency-procedures", "AC-2-22①", "O-12");
        }

        [Test]
        public void test_ac222_2_casebook_reverseReference()
        {
            // ② 39(唯一请求方)在脉案打开/关闭时发 Casebook / Explore(8 不再发)
            AssertReverseReference("casebook", "AC-2-22②", "O-12");
            // 额外断言(story:95 要求):39 = 唯一请求方登记
            var cb = ReadGdd("casebook");
            Assert.IsTrue(cb.Contains("唯一请求方") || cb.Contains("唯一"),
                "AC-2-22②:「39 = 档位意图唯一请求方」须在 casebook.md 内声明");
        }

        [Test]
        public void test_ac222_4_playerController_reverseReference()
        {
            // ④ 系统 1 侧确认 YawBasis 的**帧内次序契约**(O-14)
            AssertReverseReference("player-controller-and-movement", "AC-2-22④", "O-14");
            // B-2:陈旧措辞检测 —— 本 GDD(2 侧)已改「次序不变量」,
            // 若对侧仍写旧措辞「一帧内恒定」⇒ 输出差异行(不静默通过)
            var pc = ReadGdd("player-controller-and-movement");
            var cam = File.ReadAllText(Path.Combine(GddDir, "camera-and-viewpoint.md"));
            bool camRewritten = cam.Contains("次序不变量");
            bool pcStale = pc.Contains("一帧内恒定") || pc.Contains("帧内恒定");
            if (camRewritten && pcStale)
            {
                // 回刷轮:对侧 GDD 已就旧措辞加注(「已非『一帧内恒定』」)⇒ 视为已对齐
                bool annotated = pc.Contains("次序不变量");
                Assert.IsTrue(annotated,
                    "AC-2-22④ 措辞差异:2 侧已改「次序不变量」而对侧仍写旧措辞「一帧内恒定」" +
                    " ⇒ 须回刷为「次序不变量」(或加注已对齐);不静默通过(B-2)");
            }
        }

        [Test]
        public void test_ac222_5_skeuomorphicUi_declaresFovV()
        {
            // ⑤ 42 已声明其读取的档位 FOV_v(O-15;2026-09-22 履行)
            AssertReverseReference("skeuomorphic-ui", "AC-2-22⑤", "O-15");
            // 额外断言(story:95 要求):42 侧 F6 声明读哪一档的 FOV_v
            var ui = ReadGdd("skeuomorphic-ui");
            Assert.IsTrue(ui.Contains("FOV_v"),
                "AC-2-22⑤:42 须声明其读取的 `FOV_v`(O-15)");
        }

        [Test]
        public void test_ac222_6_inputSystem_reverseReference()
        {
            // ⑥ 系统 3 保证手柄 Navigate 与 Look 不共享物理控件(O-16)
            AssertReverseReference("input-system", "AC-2-22⑥", "O-16");
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
            // 🔴 **2026-10-03 评审修复(B-6)**:初版是**自指断言**(断言本 GDD 含它自己登记的 O 编号)
            //    —— 与「义务**接收方**已声明」无关;且无 null guard(文件缺失时抛异常而非有意义失败)。
            //    现:① null guard;② 断言**接收方**在 systems-index / 各对方 GDD 中有落点。
            var doc = ReadGdd("camera-and-viewpoint");
            Assert.IsNotNull(doc, "本 GDD 须存在(否则判据不可执行;不静默抛)");
            foreach (var o in new[] { "O-12", "O-13", "O-14", "O-15", "O-16" })
                Assert.IsTrue(doc.Contains(o),
                    $"义务 {o} 须在本 GDD 内被点名(登记面)");

            // 接收方落点:各义务的接收方 GDD 须存在
            var receivers = new (string obligation, string gdd)[]
            {
                ("O-12", "emergency-procedures"), ("O-12", "casebook"),
                ("O-14", "player-controller-and-movement"),
                ("O-15", "skeuomorphic-ui"), ("O-16", "input-system"),
            };
            foreach (var (o, g) in receivers)
                Assert.IsNotNull(ReadGdd(g), $"{o} 的接收方 GDD「{g}.md」须存在(接收方已声明)");
        }

        // ══════════════ AC-2-21 / AC-2-23:EXTERNAL 登记面 ══════════════

        [Test]
        public void test_ac221_external_pitchMaxAwaitsUserDecision()
        {
            // AC-2-21(EXTERNAL):PITCH_MAX 取值须回填系统 1 的 OQ-1-14 并结案
            // ⚠️ EXTERNAL = 「义务已定义、裁决点在别处」⇒ **不计入就绪度,但须显式登记**
            // 🔴 **2026-10-03 评审修复(B-3)**:初版守卫 `pc.Contains("OQ-1-14") && (pc.Contains("已裁")||
            //    pc.Contains("已结案"))` 是**全文 OR** ⇒ 常量**恒真**(`OQ-1-14` 命中 8 次、`已裁` 19 次)
            //    ⇒ `Ignore` **永不触发** ⇒ EXTERNAL 义务在报告里显示为 PASS(绿),违反「不得记为绿」红线。
            //    现守卫**锚定 OQ-1-14 那一行**的状态列。
            var pc = ReadGdd("player-controller-and-movement");
            Assert.IsNotNull(pc, "系统 1 的 GDD 须存在");

            // 找 OQ-1-14 所在行,判其状态列
            string oeLine = null;
            foreach (var line in pc.Split('\n'))
                if (line.Contains("OQ-1-14") && line.Contains("|"))
                { oeLine = line; break; }

            bool closed = oeLine != null &&
                          (oeLine.Contains("已结案") || oeLine.Contains("✅ 已裁") ||
                           oeLine.Contains("已裁 ·") || oeLine.Contains("已结案 ✅"));
            if (!closed)
            {
                Assert.Ignore(
                    "EXTERNAL: AC-2-21 的裁决点**在用户手里** —— `PITCH_MAX` 取值须回填 " +
                    "系统 1 的 `OQ-1-14` 并结案(行状态 = 未结案)。**该义务不因未结案而消失**," +
                    "只移出就绪度计数;**不得记为本 Epic 的绿**(B-3:守卫锚定行,非全文 OR)。");
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

            // 🔴 **2026-10-03 评审修复(B-7)**:**「禁混入 BLOCKING 计数」这半句**此前**零断言**
            //    (只验标签在场)。现断言 EPIC 就绪度表**真的**把三条排除在 BLOCKING 计数外。
            var epic = File.ReadAllText(Path.Combine(GddDir, "..", "..", "production", "epics",
                "camera-viewpoint", "EPIC.md"));
            Assert.IsTrue(epic.Contains("ADVISORY 3"),
                "EPIC 须显式声明 ADVISORY 计数 = 3(不混入 BLOCKING)");
            Assert.IsTrue(epic.Contains("BLOCKING 22"),
                "EPIC 须显式声明 BLOCKING 计数(且不含 03/24/26)");
            // BLOCKING 括号内的枚举不得出现 ADVISORY 三员
            var bm = System.Text.RegularExpressions.Regex.Match(epic, @"BLOCKING\s*22\s*\**\s*\(([^)]*)\)");
            Assert.IsTrue(bm.Success, "EPIC 的 BLOCKING 计数须带枚举(可判)");
            // 先剔除 story 编号(00X,其子串含 "03")再判 ADVISORY 三员是否混入
            string enumOnly = System.Text.RegularExpressions.Regex.Replace(bm.Groups[1].Value, @"00\d", "");
            foreach (var ac in new[] { "03", "24", "26" })
                Assert.IsFalse(enumOnly.Contains(ac),
                    $"ADVISORY 门 {ac} 不得出现在 BLOCKING 枚举内(B-7:禁混入 BLOCKING 计数)");
        }

        // ══════════════ VR 接口面(P0 只落枚举与结构预留)══════════════

        [Test]
        public void test_vrInterface_firstPersonEnumExists_planeChainIndependent()
        {
            // TR-camera-002:P0 只落 `CameraMode.FirstPerson` 枚举与链冻结的结构预留
            Assert.IsTrue(Enum.IsDefined(typeof(DaYiJingCheng.Gameplay.Presentation.Camera.CameraMode),
                                        "FirstPerson"),
                "CameraMode.FirstPerson 须存在(P0 只落枚举,实现推 P1a)");

            // 🔴 **2026-10-03 评审修复(B-4)**:初版只验「类型 / 成员**存在**」(反射执符),
            //    不验任何结构语义。现补:ADR-020 §Key Interfaces 四成员齐 + `OQ-2-6` doc comment 在位。
            var rig = typeof(DaYiJingCheng.Gameplay.Presentation.Camera.ICameraRig);
            Assert.IsNotNull(rig.GetProperty("YawBasis"), "平面链的 YawBasis 须完好");
            Assert.IsNotNull(rig.GetProperty("Yaw"));
            Assert.IsNotNull(rig.GetProperty("Pitch"));
            // ADR-020 §Key Interfaces 四成员
            Assert.IsNotNull(rig.GetProperty("Mode"), "ADR-020 明列的 Mode 须在接口上");
            Assert.IsNotNull(rig.GetProperty("Camera"), "ADR-020 明列的 Camera 须在接口上");
            Assert.IsNotNull(rig.GetMethod("SetMode"), "ADR-020 明列的 SetMode 须在接口上");
            Assert.IsNotNull(rig.GetMethod("Tick"), "ADR-020 明列的 Tick 须在接口上");

            // `OQ-2-6` 的 doc comment 须落在接口源码(偿付「零成本窗口」)
            string iface = File.ReadAllText(Path.Combine(GddDir, "..", "..", "unity", "Assets",
                "Gameplay.Presentation", "Camera", "ICameraRig.cs"));
            Assert.IsTrue(iface.Contains("OQ-2-6"),
                "`OQ-2-6`(Camera 的 VR 立体语义)须在 ICameraRig 源码内注明(P1a 前置;story:116)");
        }

        // ══════════════ AC-2-24 / AC-2-26:ADVISORY 独立 NOT-RUN 登记(B-5)══════════════

        [Test]
        public void test_ac224_advisory_notRunRegistered()
        {
            // AC-2-24(ADVISORY):「不晕」的手感签核面 —— 显式登记 NOT-RUN,不得记绿
            var doc = ReadGdd("camera-and-viewpoint");
            Assert.IsNotNull(doc);
            Assert.IsTrue(doc.Contains("AC-2-24"), "AC-2-24 须在 GDD 内登记");
            Assert.Ignore("ADVISORY: AC-2-24 须 playtest 签核(非自动化);登记 NOT-RUN,不计入绿。");
        }

        [Test]
        public void test_ac226_advisory_notRunRegistered()
        {
            // AC-2-26(ADVISORY):手感签核面 —— 显式登记 NOT-RUN,不得记绿
            var doc = ReadGdd("camera-and-viewpoint");
            Assert.IsNotNull(doc);
            Assert.IsTrue(doc.Contains("AC-2-26"), "AC-2-26 须在 GDD 内登记");
            Assert.Ignore("ADVISORY: AC-2-26 须 playtest 签核(非自动化);登记 NOT-RUN,不计入绿。");
        }
    }
}
