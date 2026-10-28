// story-006 AC-1 / AC-2 / AC-3 / AC-4 / AC-5 的夹具。
//
// 权威来源:
//   `production/epics/level-tool/story-006-c1-c2-c2prime-and-ci.md` §Acceptance Criteria
//   ADR-022 §四(C1 告警 · C2 / C2' 硬失败)· §Validation(CI 可跑)
//   ADR-006(`ROUND_HALF_AWAY_FROM_ZERO`;禁 ties-to-even)
//
// ═══════════════════════════════════════════════════════════════════════════
// 三条检查各自的「假绿」形态不一样,夹具因此不对称
// ═══════════════════════════════════════════════════════════════════════════
//   · C2(阈值边界):只测「超了会红」时,一个阈值写得很松的实现也能过
//     ⇒ 必须有**三个点**(0 / 1 量子 / 2 量子),两点夹住边界。
//   · C2'(例外路径):只测「高的会红」时,一个「恒红」的实现也能过
//     ⇒ 必须有「有声明 ⇒ 不红」的那一条。
//   · C1(告警):只测「漂移会报」时,一个「恒报」的实现也能过;
//     只测「干净不报」时,一个「永不报」的实现也能过
//     ⇒ **两个方向 + 一个干净基线**,三条合起来才钉住。

using System;                        // InvalidOperationException / ArgumentNullException
using System.Collections.Generic;
using System.Linq;
using DaYiJingCheng.EditorTools.Level;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;
using NUnit.Framework;
using UnityEngine;

namespace DaYiJingCheng.Tests.LevelTool
{
    /// <summary>C1 / C2 / C2' / CI 入口。</summary>
    [TestFixture]
    public class consistency_c1_c2_c2prime_test
    {
        // ══════════════ 夹具 ══════════════

        /// <summary>造一个 2×1×2 的小关卡;可选地形参数 / 视觉层 / 足迹声明。</summary>
        private static LevelSource MakeSource(
            int slopeLimitDegX100 = 4500, int stepOffsetMm = 300,
            VisualLayerSource visual = null, IReadOnlyList<ModuleFootprintSpec> footprints = null,
            bool bakedAtOrigin = false)
        {
            const int w = 2, h = 1, d = 2;
            int total = w * h * d;

            var rows = new List<TerrainRowSpec>
            {
                new TerrainRowSpec(0, "wood_floor", 1, Fix.FromRational(1, 1), Fix.FromRational(1, 1)),
            };
            var terrain = new TerrainSource(rows, new int[total], slopeLimitDegX100, stepOffsetMm, total);

            var slots = new List<SlotSpec>
            {
                new SlotSpec(0, 0, 0, SlotType.Decor, bakedAtOrigin, bakedAtOrigin ? 7 : SlotSpec.NoModule),
            };

            return new LevelSource(
                "c1c2", w, h, d, slots,
                walkable: new[] { 1, 1, 1, 1 }, terrain: terrain, latticeSizeMm: 1000,
                chunkSizeX: w, chunkSizeY: h, chunkSizeZ: d,
                visualLayer: visual, moduleFootprints: footprints);
        }

        /// <summary>全零视觉层(<paramref name="solidAt"/> 指定的格除外,高度 100 mm)。</summary>
        private static VisualLayerSource MakeVisual(int[] solidAt = null)
        {
            var heights = new int[4];
            if (solidAt != null)
                foreach (int i in solidAt) heights[i] = 100;
            return new VisualLayerSource(heights, 4);
        }

        private sealed class StubCatalog : IModuleCatalog
        {
            private readonly Dictionary<int, ModuleDefinition> _defs = new Dictionary<int, ModuleDefinition>();

            public StubCatalog Add(int moduleId, params (int X, int Y, int Z)[] cells)
            {
                var occ = cells.Select(c => new WorldPos(c.X, c.Y, c.Z)).ToList();
                _defs[moduleId] = new ModuleDefinition(
                    moduleId, SlotType.Decor, occ, new[] { 0 }, cost: 1, buildItemKey: 1);
                return this;
            }

            public ModuleDefinition? GetModuleDefinition(int moduleId)
                => _defs.TryGetValue(moduleId, out ModuleDefinition v) ? v : (ModuleDefinition?)null;

            public bool ContainsModule(int moduleId) => _defs.ContainsKey(moduleId);

            public IReadOnlyCollection<int> GetAllModuleIds() => _defs.Keys.ToArray();
        }

        private static bool Has(IReadOnlyList<string> items, string fragment)
        {
            for (int i = 0; i < items.Count; i++)
                if (items[i].Contains(fragment)) return true;
            return false;
        }

        // ══════════════ AC-1 · C2 阈值边界(两个量各一套) ══════════════

        /// <summary>`slopeLimit`:偏差 0 / 1 量子 ⇒ 不抛;2 量子 ⇒ 抛。</summary>
        /// <remarks><b>判别力</b>:把阈值从「≤ 1」放宽到「≤ 2」⇒ 第三条转绿 ⇒ 本测红;
        /// 收紧到「≤ 0」⇒ 第二条转红 ⇒ 本测红。<b>三个点缺一不可。</b></remarks>
        [Test]
        public void test_ac1_slopeLimit_zeroOneTwoQuantum_boundaries()
        {
            const int baked = 4500;   // 45.00°

            Assert.That(Violations(baked, 300, baked, 300, out _), Is.Empty,
                "偏差 0 量子须通过");
            Assert.That(Violations(baked + 1, 300, baked, 300, out _), Is.Empty,
                "偏差恰 1 量子须通过(容差就是为取整误差留的)");
            Assert.That(Violations(baked + 2, 300, baked, 300, out _), Is.Not.Empty,
                "偏差 2 量子须抛 —— 若这条不红,说明阈值被放宽了");
        }

        /// <summary>`stepOffset`:同三条边界,**独立于** slopeLimit。</summary>
        /// <remarks><b>判别力</b>:停用 stepOffset 那一条判据 ⇒ 本测第三条红(而 slopeLimit 那条不受影响)。</remarks>
        [Test]
        public void test_ac1_stepOffset_zeroOneTwoQuantum_boundaries()
        {
            const int baked = 300;   // 0.300 m

            Assert.That(Violations(4500, baked, 4500, baked, out _), Is.Empty, "偏差 0 量子须通过");
            Assert.That(Violations(4500, baked - 1, 4500, baked, out _), Is.Empty, "偏差恰 1 量子须通过(负方向)");
            Assert.That(Violations(4500, baked + 2, 4500, baked, out _), Is.Not.Empty, "偏差 2 量子须抛");
        }

        /// <summary>只有 slopeLimit 漂移时,报文点名 slopeLimit,**不**点名 stepOffset。</summary>
        /// <remarks><b>判别力</b>:把两条判据合并成一条(两个量任一超差都报同一句)⇒ 本测的
        /// 「不点名 stepOffset」断言红 —— 合并会让夹具失去区分度,这正是「一属性一条变异」要防的。</remarks>
        [Test]
        public void test_ac1_slopeDriftOnly_reportsSlopeNotStep()
        {
            List<string> v = Violations(4500 + 5, 300, 4500, 300, out _);

            Assert.That(v.Count, Is.EqualTo(1), "只该有一条违例(两个量各自独立判)");
            Assert.That(Has(v, WalkabilityParityCheck.SlopeLimitKey), Is.True, "报文须点名 slopeLimit 那一半");
            Assert.That(Has(v, WalkabilityParityCheck.StepOffsetKey), Is.False,
                "stepOffset 那一半没漂移,不得被连带报出");
        }

        /// <summary>C2 无引擎侧输入 ⇒ 进 `NotRun`,**不**进 `Warnings`、**不**进 `Violations`。</summary>
        /// <remarks><b>判别力</b>:把「无输入」写成一条告警 ⇒ 本测的 `NotRun` 断言红;
        /// 写成一条违例 ⇒ `IsGreen` 断言红。这一条钉的是「三态可分」。</remarks>
        [Test]
        public void test_ac1_noEngineParams_isNotRun_notGreenNotRed()
        {
            CheckReport report = LevelConsistencyCheck.Check(MakeSource());


            Assert.That(report.IsGreen, Is.True, "没有引擎侧输入不是缺陷 ⇒ 不得判红");
            Assert.That(Has(report.NotRun, "C2 可走性同源"), Is.True, "须登记为「未跑」");
            Assert.That(Has(report.Warnings, "C2"), Is.False, "未跑不是告警(告警 = 查到的问题)");
        }

        private static List<string> Violations(int engineSlope, int engineStep, int bakedSlope, int bakedStep,
            out LevelSource src)
        {
            src = MakeSource(slopeLimitDegX100: bakedSlope, stepOffsetMm: bakedStep);
            CheckReport report = LevelConsistencyCheck.Check(
                src, engineParams: new LatticeEngineParams(engineSlope, engineStep));
            return report.Violations.ToList();
        }

        // ══════════════ AC-2 · C2' collider 足迹 ══════════════

        /// <summary>高 collider **无**声明 ⇒ 硬失败。</summary>
        /// <remarks><b>判别力</b>:停用高度判据 ⇒ 本测红。</remarks>
        [Test]
        public void test_ac2_highCollider_withoutDeclaration_reportsViolation()
        {
            LevelSource src = MakeSource(stepOffsetMm: 300, footprints: new[]
            {
                new ModuleFootprintSpec(7, new[] { new WorldPos(0, 0, 0) }, colliderHeightMm: 800),
            });
            CheckReport report = LevelConsistencyCheck.Check(src, engineParams: new LatticeEngineParams(4500, 300));

            Assert.That(report.IsGreen, Is.False, "800 mm > 300 mm 且无声明 ⇒ 必须红");
            Assert.That(Has(report.Violations, ColliderFootprintCheck.HighColliderKey), Is.True);
        }

        /// <summary>高 collider **有**声明 ⇒ 不红(例外路径可达,证明不是「恒抛」)。</summary>
        /// <remarks><b>判别力</b>:把判据写成无条件触发 ⇒ 本测红。</remarks>
        [Test]
        public void test_ac2_highCollider_withDeclaration_isClean()
        {
            LevelSource src = MakeSource(stepOffsetMm: 300, footprints: new[]
            {
                new ModuleFootprintSpec(7, new[] { new WorldPos(0, 0, 0) }, colliderHeightMm: 800,
                    walkOverDeclared: true),
            });
            CheckReport report = LevelConsistencyCheck.Check(src, engineParams: new LatticeEngineParams(4500, 300));

            Assert.That(report.IsGreen, Is.True,
                "显式声明可走过 ⇒ 不得判失败(否则 C2' 没有例外路径):\n" +
                string.Join("\n", report.Violations));
        }

        /// <summary>collider 高度**恰等于** `stepOffset` ⇒ 不红(判据是严格大于)。</summary>
        /// <remarks><b>判别力</b>:把 `>` 写成 `>=` ⇒ 本测红。这是阈值的**另一侧**边界;
        /// 上一测钉的是「例外声明」,本测钉的是「等于」—— 两者不是同一条。</remarks>
        [Test]
        public void test_ac2_heightEqualToStepOffset_isClean()
        {
            LevelSource src = MakeSource(stepOffsetMm: 300, footprints: new[]
            {
                new ModuleFootprintSpec(7, new[] { new WorldPos(0, 0, 0) }, colliderHeightMm: 300),
            });
            CheckReport report = LevelConsistencyCheck.Check(src, engineParams: new LatticeEngineParams(4500, 300));

            Assert.That(report.IsGreen, Is.True,
                "恰好等于 stepOffset = 「一步能迈上去」的上限 ⇒ 不该判失败:\n" +
                string.Join("\n", report.Violations));
        }

        /// <summary>足迹与目录占用格不一致 ⇒ 硬失败。</summary>
        /// <remarks><b>判别力</b>:停用足迹比对(只留高度判据)⇒ 本测红。</remarks>
        [Test]
        public void test_ac2_footprintMismatch_reportsViolation()
        {
            LevelSource src = MakeSource(stepOffsetMm: 300, footprints: new[]
            {
                new ModuleFootprintSpec(7, new[] { new WorldPos(0, 0, 0), new WorldPos(1, 0, 0) }, 100),
            });
            IModuleCatalog catalog = new StubCatalog().Add(7, (0, 0, 0));

            CheckReport report = LevelConsistencyCheck.Check(
                src, engineParams: new LatticeEngineParams(4500, 300), moduleCatalog: catalog);

            Assert.That(report.IsGreen, Is.False, "声明多一格 ⇒ 足迹不同源 ⇒ 必须红");
            Assert.That(Has(report.Violations, ColliderFootprintCheck.FootprintKey), Is.True);
        }

        /// <summary>无模块目录 ⇒ 足迹半边进 `NotRun`,高度半边照跑。</summary>
        /// <remarks><b>判别力</b>:把「缺目录」写成静默跳过(什么都不记)⇒ 本测红。</remarks>
        [Test]
        public void test_ac2_noCatalog_footprintHalfIsNotRun()
        {
            LevelSource src = MakeSource(stepOffsetMm: 300, footprints: new[]
            {
                new ModuleFootprintSpec(7, new[] { new WorldPos(0, 0, 0) }, 100),
            });
            CheckReport report = LevelConsistencyCheck.Check(src, engineParams: new LatticeEngineParams(4500, 300));

            Assert.That(report.IsGreen, Is.True);
            Assert.That(Has(report.NotRun, "C2' 足迹一致性"), Is.True, "缺目录 ⇒ 须登记未跑");
        }

        // ══════════════ AC-3 · C1 两层漂移(告警) ══════════════

        /// <summary>逻辑层有开档家具、视觉层空 ⇒ **报告警**且**不使构建失败**。</summary>
        /// <remarks><b>判别力</b>:① 停用方向①检测 ⇒ 本测红;② 把告警升格为违例 ⇒ `IsGreen` 断言红。</remarks>
        [Test]
        public void test_ac3_logicSolidVisualEmpty_warnsAndStaysGreen()
        {
            LevelSource src = MakeSource(bakedAtOrigin: true, visual: MakeVisual());
            CheckReport report = LevelConsistencyCheck.Check(src, engineParams: new LatticeEngineParams(4500, 300));

            Assert.That(Has(report.Warnings, LayerDriftCheck.DriftKey), Is.True, "漂移须被**报出**");
            Assert.That(report.IsGreen, Is.True, "C1 是告警 ⇒ 不得使构建失败(ADR-022 §四)");
        }

        /// <summary>视觉层有实体、逻辑层该格无开档占用 ⇒ **反方向**也报。</summary>
        /// <remarks><b>判别力</b>:只实现方向①(逻辑 → 视觉)⇒ 本测红。这一半覆盖的是
        /// 「视觉多出一堵墙」—— 玩家最常看见的那种漂移。</remarks>
        [Test]
        public void test_ac3_visualSolidLogicEmpty_warns()
        {
            LevelSource src = MakeSource(bakedAtOrigin: false, visual: MakeVisual(new[] { 3 }));
            CheckReport report = LevelConsistencyCheck.Check(src, engineParams: new LatticeEngineParams(4500, 300));

            Assert.That(Has(report.Warnings, LayerDriftCheck.DriftKey), Is.True,
                "视觉层有实体、逻辑层是空地 ⇒ 须报(反方向)");
            Assert.That(report.IsGreen, Is.True, "仍是告警,不使构建失败");
        }

        /// <summary>两侧一致 ⇒ **零告警**(「恒报」的实现会在这里红)。</summary>
        /// <remarks><b>判别力</b>:把告警写成无条件追加 ⇒ 本测红。它是「恒报」的唯一杀手。</remarks>
        [Test]
        public void test_ac3_noDrift_noWarning()
        {
            LevelSource src = MakeSource(bakedAtOrigin: true, visual: MakeVisual(new[] { 0 }));
            CheckReport report = LevelConsistencyCheck.Check(src, engineParams: new LatticeEngineParams(4500, 300));

            Assert.That(report.Warnings.Count, Is.EqualTo(0),
                "两侧都声明「这格有东西」⇒ 无漂移:\n" + string.Join("\n", report.Warnings));
        }

        /// <summary>无视觉层 ⇒ C1 进 `NotRun`(不是「无告警」)。</summary>
        /// <remarks><b>判别力</b>:把「没跑」与「跑过且干净」写成同一个状态 ⇒ 本测红。</remarks>
        [Test]
        public void test_ac3_noVisualLayer_isNotRun()
        {
            CheckReport report = LevelConsistencyCheck.Check(
                MakeSource(bakedAtOrigin: true), engineParams: new LatticeEngineParams(4500, 300));

            Assert.That(Has(report.NotRun, "C1 两层漂移"), Is.True, "无视觉层 ⇒ 未跑");
            Assert.That(report.Warnings.Count, Is.EqualTo(0), "未跑不得写成告警");
        }

        // ══════════════ AC-4 · CI 入口(退出码分级) ══════════════

        /// <summary>干净源 ⇒ 退出码 0,且**打印**未跑项。</summary>
        /// <remarks><b>判别力</b>:把 `NotRun` 从 `Render()` 里删掉 ⇒ 本测的文本断言红 ——
        /// 静默跳过是这一类最典型的借绿。</remarks>
        [Test]
        public void test_ac4_cleanSource_exitZero_andRendersNotRun()
        {
            LevelSource src = MakeSource();
            LevelCiResult r = LevelCiRunner.Run(src, controller: null);

            Assert.That(r.ExitCode, Is.EqualTo(LevelCiRunner.ExitGreen));
            string text = r.Render();
            Assert.That(text, Does.Contain("未跑"), "未跑项必须出现在日志里(不是静默跳过)");
            Assert.That(text, Does.Contain("退出码 = 0"));
        }

        /// <summary>注入 C2 缺陷 ⇒ 退出码 1,且报文点名是哪一条。</summary>
        /// <remarks><b>判别力</b>:把退出码恒置 0 ⇒ 本测红。这是 AC-4 的核心判据
        /// (「命令存在」≠「命令会红」)。</remarks>
        [Test]
        public void test_ac4_injectedC2Defect_exitOne_andNamesCheck()
        {
            LevelSource bad = MakeSource(stepOffsetMm: 300, footprints: new[]
            {
                new ModuleFootprintSpec(7, new[] { new WorldPos(0, 0, 0) }, colliderHeightMm: 9999),
            });

            LevelCiResult r = LevelCiRunner.Run(bad, controller: null);

            Assert.That(r.ExitCode, Is.EqualTo(LevelCiRunner.ExitViolation), "有硬失败 ⇒ 退出码 1");
            Assert.That(r.Render(), Does.Contain(ColliderFootprintCheck.HighColliderKey),
                "报文须点名是哪一条检查(「命令存在」≠「命令会红」)");
        }

        /// <summary>关卡源为 null ⇒ 退出码 **2**(链跑不起来),不是 1。</summary>
        /// <remarks><b>判别力</b>:把「门坏了」与「代码红了」合成同一个码 ⇒ 本测红。</remarks>
        [Test]
        public void test_ac4_nullSource_exitTwo_fatal()
        {
            LevelCiResult r = LevelCiRunner.Run(null, controller: null);

            Assert.That(r.ExitCode, Is.EqualTo(LevelCiRunner.ExitFatal));
            Assert.That(r.Fatal, Is.Not.Null.And.Not.Empty, "致命错误须有报文");
            Assert.That(r.Render(), Does.Contain("未能跑起来"));
        }

        // ══════════════ AC-5 · 未实现项归零 ══════════════

        /// <summary>C1–C6 七项全部有实现 ⇒ `UnimplementedChecks` 为空。</summary>
        /// <remarks><b>判别力</b>:往数组里再塞一条未实现项 ⇒ 本测红(禁把未实现伪装成已实现,
        /// 也禁把已实现说成未实现)。</remarks>
        [Test]
        public void test_ac5_unimplementedChecks_isEmpty()
        {
            Assert.That(LevelConsistencyCheck.UnimplementedChecks, Is.Empty,
                "C1–C6 七项均已有实现(story-003/005/006):" +
                string.Join(" / ", LevelConsistencyCheck.UnimplementedChecks));
        }

        // ══════════════ 量化口径(ADR-006) ══════════════

        /// <summary>半量子**向远离 0 的方向**取整(ADR-006),不是 ties-to-even。</summary>
        /// <remarks><b>判别力</b>:把 `Quantize` 换成 `Math.Round`(ties-to-even)⇒ 前两条红。
        /// 这一条是 ADR-006「单一舍入模式」在关卡工具侧的落地判据。
        /// <para>⚠️ <b>2026-10-29 证伪轮 F13 抓到的假绿,已修夹具</b>(原文判据保留在上述描述里):
        /// 首版用的是 `0.0125f × 1000`,期望 13。**但 `0.0125f` 不是精确的 1/80** —— 它是
        /// `0.012500000186…`,转 `double` 后乘 1000 得 `12.500000186`,**根本不在中点**,
        /// 所以 `Math.Round` 同样给 13 ⇒ 变异与原文同结果 ⇒ 该夹具对「舍入模式」零判别力。
        /// ⇒ 现改用**在 double 里精确等于 .5** 的输入(`0.5f` 是二进制精确值,乘 1 仍是 0.5)。
        /// **教训**:边界夹具的输入必须自己先离线算一遍**在目标精度下**是不是真的在中点。</para></remarks>
        [Test]
        public void test_quantize_halfQuantum_roundsAwayFromZero()
        {
            // 0.5 在二进制里**精确**,乘 1 仍是精确的 0.5 ⇒ 这是真中点。
            // away-from-zero ⇒ 1;ties-to-even ⇒ **0**。两者必然分得开。
            Assert.That(CharacterControllerParamProbe.Quantize(0.5f, 1), Is.EqualTo(1),
                "0.5 恰为中点 ⇒ 须向远离 0 取整 = 1(ties-to-even 会给 0)");
            Assert.That(CharacterControllerParamProbe.Quantize(-0.5f, 1), Is.EqualTo(-1),
                "负半值同样向远离 0 取整(对称)");

            // 非边界:确认量化在正常值上仍是对的(否则「恒返回 1」也能过上面两条)。
            Assert.That(CharacterControllerParamProbe.Quantize(45f, 100), Is.EqualTo(4500));
            Assert.That(CharacterControllerParamProbe.Quantize(0.3f, 1000), Is.EqualTo(300));
        }

        /// <summary>真 `CharacterController` 的探针:读得到,且量化值可用。</summary>
        /// <remarks><b>判别力</b>:把 `TryRead` 写成恒 false ⇒ 本测红;把两个量对调 ⇒ 本测红。</remarks>
        [Test]
        public void test_probe_readsRealController_andKeepsFieldsApart()
        {
            var go = new GameObject("cc_probe_test");
            try
            {
                var cc = go.AddComponent<CharacterController>();
                cc.slopeLimit = 45f;
                cc.stepOffset = 0.3f;

                Assert.That(CharacterControllerParamProbe.TryRead(cc, out LatticeEngineParams p, out _), Is.True);
                Assert.That(p.SlopeLimitDegX100, Is.EqualTo(4500));
                Assert.That(p.StepOffsetMm, Is.EqualTo(300));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        /// <summary>没有控制器 ⇒ `TryRead` 回 false 并给出理由(不是抛、不是默认 0)。</summary>
        [Test]
        public void test_probe_noController_returnsFalseWithReason()
        {
            Assert.That(CharacterControllerParamProbe.TryRead(null, out _, out string reason), Is.False);
            Assert.That(reason, Does.Contain("CharacterController"));
        }

        // ══════════════ 评审修复回归(2026-10-29 · B-1 / B-2 / B-3) ══════════════

        /// <summary><b>B-2</b>:引擎侧有值但**烘焙侧缺地形** ⇒ C2 进 `NotRun`,不是静默绿。</summary>
        /// <remarks><b>判别力</b>:去掉 `src.Terrain == null` 那一支 ⇒ 本测红(报告里 C2 既无违例也无未跑,
        /// 读者会当成通过 —— 这正是评审抓到的形态)。</remarks>
        [Test]
        public void test_b2_engineParamsWithoutTerrain_isNotRun_notSilentGreen()
        {
            // 只声明槽位、不声明地形的关卡是合法形态(LevelSource 允许 terrain = null)。
            var src = new LevelSource("no_terrain", 2, 1, 2, new List<SlotSpec>
            {
                new SlotSpec(0, 0, 0, SlotType.Decor, false),
            });

            CheckReport report = LevelConsistencyCheck.Check(
                src, engineParams: new LatticeEngineParams(4500, 300));

            Assert.That(report.IsGreen, Is.True, "缺地形不是缺陷本身 ⇒ 不得判红");
            Assert.That(Has(report.NotRun, "C2 可走性同源"), Is.True,
                "烘焙侧没有值可比 ⇒ 必须登记「未跑」,不能什么都不记");
            Assert.That(Has(report.Violations, "C2"), Is.False, "缺一侧不得被伪造成违例");
        }

        /// <summary><b>B-3</b>:没有 NavMesh 采样器 ⇒ C3 也进 `NotRun`(与 C1/C2/C2' 口径一致)。</summary>
        /// <remarks><b>判别力</b>:删掉 C3 的 `else` 分支 ⇒ 本测红 —— 届时「C3 没跑」与
        /// 「C3 跑了且无告警」在报告上不可分。</remarks>
        [Test]
        public void test_b3_noNavSampler_isNotRun_forC3()
        {
            CheckReport report = LevelConsistencyCheck.Check(MakeSource(),
                engineParams: new LatticeEngineParams(4500, 300));

            Assert.That(Has(report.NotRun, "C3"), Is.True, "C3 无输入须登记未跑");
            Assert.That(report.IsGreen, Is.True, "C3 缺输入不是硬失败");
        }

        /// <summary><b>B-1</b>:绿源时 `CheckOrThrow` 把报告**交还**调用方,并把未跑项写进日志。</summary>
        /// <remarks>🔁 <b>2026-10-29 复评 B-7 修复</b>:原 remarks 声称「不喂 engineParams ⇒ 第一条断言红」,
        /// **该断言为假** —— 不喂时 C2 自身也进 `NotRun`,`NotRun.Count > 0` 照样成立。
        /// 现把「喂/不喂」的判别力**交给下面那条** `test_b1_checkOrThrow_logsBeforeThrowing`。
        /// <para><b>判别力</b>:删掉 `CheckOrThrow` 里的 `NotRun` 聚合日志 ⇒ `LogAssert.Expect` 无人满足 ⇒ 红。</para></remarks>
        [Test]
        public void test_b1_checkOrThrow_surfacesNotRun_toCallerAndLog()
        {
            LevelSource src = MakeSource(bakedAtOrigin: true);

            UnityEngine.TestTools.LogAssert.Expect(
                UnityEngine.LogType.Log, new System.Text.RegularExpressions.Regex("NOT-RUN"));

            CheckReport report = LevelConsistencyCheck.CheckOrThrow(
                src, new LatticeEngineParams(4500, 300));

            Assert.That(report, Is.Not.Null, "须把报告交还给调用方(导出器/夹具可核)");
            Assert.That(report.NotRun.Count, Is.GreaterThan(0),
                "本关卡无视觉层 / 无足迹声明 ⇒ 必有未跑项;若为 0,说明它们又被丢了");
        }

        /// <summary><b>B-1 之顺序</b>:日志必须发生在 `throw` **之前** —— 否则一次硬失败会盖掉
        /// 「另外几条根本没跑」这条信息。</summary>
        /// <remarks>🔁 <b>2026-10-29 复评修复(Agent A 残留)</b>:此性质此前**零断言**
        /// (原夹具喂的是绿源,`throw` 分支根本没走)。
        /// <para><b>判别力</b>:把 `CheckOrThrow` 里的日志块移到 `if (!IsGreen) throw` **之后** ⇒
        /// 本测的 `LogAssert.Expect` 无人满足 ⇒ 红。这是唯一能钉住顺序的形态。</para></remarks>
        [Test]
        public void test_b1_checkOrThrow_logsBeforeThrowing()
        {
            // 同时具备:① 必抛的违例(重复格)② 若干未跑项(无视觉层 / 无足迹 / 无采样器)
            var bad = new LevelSource("bad", 2, 1, 2, new List<SlotSpec>
            {
                new SlotSpec(0, 0, 0, SlotType.Decor, false),
                new SlotSpec(0, 0, 0, SlotType.Shell, false),
            });

            UnityEngine.TestTools.LogAssert.Expect(
                UnityEngine.LogType.Log, new System.Text.RegularExpressions.Regex("NOT-RUN"));

            Assert.Throws<InvalidOperationException>(
                () => LevelConsistencyCheck.CheckOrThrow(bad),
                "重复格须抛(前置:本测确实走到 throw 分支)");
        }

        /// <summary><b>B-1 之告警面</b>:告警也必须在导出路径上被说出来。</summary>
        /// <remarks><b>判别力</b>:删掉 `CheckOrThrow` 里的告警聚合日志 ⇒ 本测红。
        /// 这一条补的是复评指出的「三类日志里只有 NotRun 被钉住」。</remarks>
        [Test]
        public void test_b1_checkOrThrow_surfacesWarnings_toLog()
        {
            // 逻辑层有开档家具、视觉层空 ⇒ C1 漂移(告警,不抛)
            LevelSource src = MakeSource(bakedAtOrigin: true, visual: MakeVisual());

            UnityEngine.TestTools.LogAssert.Expect(
                UnityEngine.LogType.Warning, new System.Text.RegularExpressions.Regex("告警"));

            CheckReport report = LevelConsistencyCheck.CheckOrThrow(
                src, new LatticeEngineParams(4500, 300));

            Assert.That(report.Warnings.Count, Is.GreaterThan(0), "前置:C1 漂移确实产生了告警");
            Assert.That(report.IsGreen, Is.True, "告警不使构建失败");
        }

        /// <summary>C2 的烘焙侧为 `null` ⇒ **抛**(fail-closed),不假装通过。</summary>
        /// <remarks>🔁 <b>2026-10-29 复评修复(Agent A 残留)</b>:此前 `terrain == null` 直接
        /// `return true` —— 把「没有比对对象」表达成「比对通过」。调用方当时靠外层守卫绕开,
        /// 但那是调用方记得,不是契约。</remarks>
        [Test]
        public void test_c2_nullTerrain_throws_insteadOfPassingSilently()
        {
            Assert.Throws<ArgumentNullException>(() =>
                WalkabilityParityCheck.TryCheck(null, new LatticeEngineParams(4500, 300), out _));
        }

        /// <summary><b>B-1 之导出面</b>:探针读到的量化值**稳定且精确**(不是一个「> 0」的模糊断言)。</summary>
        /// <remarks>🔁 <b>2026-10-29 复评 B-6 修复</b>:原断言只写 `> 0` / `>= 0`,值漂移抓不到。
        /// <para><b>判别力</b>:把两个字段对调、或把量化单位改成 1、或把 away-from-zero 换成
        /// 截断 ⇒ 本测红。导出器 7 处接线由 `grep -c "ProbeCurrent()" LevelExporter.cs == 7` 复算。</para></remarks>
        [Test]
        public void test_b1_probeCurrent_returnsExactEngineDefaults()
        {
            LatticeEngineParams? p = CharacterControllerParamProbe.ProbeCurrent();

            Assert.That(p.HasValue, Is.True, "临时探针须读得到参数");
            // Unity 6.3 `CharacterController` 默认:slopeLimit = 45°、stepOffset = 0.3 m
            // (AC-0 spike 实测,见 LatticeParamQuantization 的 remarks)。
            Assert.That(p.Value.SlopeLimitDegX100, Is.EqualTo(4500), "45.00° ⇒ 4500(0.01 度量子)");
            Assert.That(p.Value.StepOffsetMm, Is.EqualTo(300), "0.300 m ⇒ 300(1 mm 量子)");
        }
    }
}
