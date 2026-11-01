// S23 · story-006 呈现白名单与零状态播报 —— 守卫夹具(2026-11-01)
//
// ─────────────────────────────────────────────────────────────────────────────
// 读哪些仓库文件 / 写哪里(test-standards 具名声明义务)
// ─────────────────────────────────────────────────────────────────────────────
// 读(只读,零写盘):
//   ① `unity/Assets/Sim/DeathTransitionQuery.cs`、`unity/Assets/Sim.Contracts/DeathTransitionDto.cs`
//      的**源码**(剥注释扫描)—— 为什么必须读真源:「零写路径」与「零 Append」是跨文件
//      调用约定,类型面表达不了(S23-1/2/5 同法)。
//   ② `design/ux/death-transition-29.md` —— UX Flag(story-006 硬前置)的**文档面判据**:
//      spec 已产出且 Approved,读内联副本会失去「测的就是真 spec」价值。
// 写:**零写盘**。
//
// ── 载体非空集纪律(AC-29-07 edge case)────────────────────────────────────────
// DTO 已建(有真字段),PresentationDtoGuard.Scan 收真类型 —— 无「载体未建 NOT-RUN」形态;
// 负控(F4)独立:测试本地的违例 DTO / 违例类型必须各自转红。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.EditorTools.Gates;
using DaYiJingCheng.Gameplay.Presentation.DeathTransition;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;
using UnityEngine;

namespace DaYiJingCheng.Tests.DeathRespawn
{
    /// <summary>story-006 守卫:白名单闭集、DTO 纯度、投影真链、零写路径、UX 前置。</summary>
    [TestFixture]
    public sealed class DeathPresentationGuardTest
    {
        private CompositionRootServices _bag;

        private const int PlayerId = 7;
        private const long Tick = 100;
        private static readonly WorldPos DeathCell = new WorldPos(5, 0, 3);
        private static readonly WorldPos BedCell = new WorldPos(0, 1, 0);

        [SetUp]
        public void Setup()
        {
            _bag = CompositionRoot.Assemble();
        }

        private static string RepoFile(string relative) =>
            Path.Combine(Application.dataPath, "..", "..", relative.Replace('/', Path.DirectorySeparatorChar));

        private static string StripComments(string src)
        {
            string noBlock = Regex.Replace(src, @"/\*.*?\*/", " ", RegexOptions.Singleline);
            return Regex.Replace(noBlock, @"//[^\n]*", " ");
        }

        /// <summary>代价数值词面(AC-29-07:掉落/掉级/死亡次数均不得出现;词面子串判)。</summary>
        private static readonly string[] CostTokens = { "drop", "lost", "kill", "score", "penalty", "count" };

        private static IReadOnlyList<string> ScanCostFields(Type dtoRoot)
        {
            var errs = new List<string>();
            if (dtoRoot == null)
            {
                errs.Add("[CostGuard] dtoRoot == null —— 拒以空集冒充绿");
                return errs;
            }
            foreach (FieldInfo f in dtoRoot.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                string lower = f.Name.ToLowerInvariant();
                if (CostTokens.Any(t => lower.Contains(t)))
                    errs.Add($"[CostGuard] 代价数值词面命中:{dtoRoot.Name}.{f.Name}");
                if (f.FieldType != typeof(int))
                    errs.Add($"[CostGuard] 字段非 int:{dtoRoot.Name}.{f.Name}({f.FieldType.Name})—— DTO 全整数");
            }
            return errs;
        }

        // ══════════════════ AC-29-07 —— DTO 纯度(载体非空集 + 独立负控)══════════════════

        [Test]
        public void test_dto_recursiveScan_zeroDiseaseToken_zeroCostFields()
        {
            // 真守卫(生产面):disease 词面递归扫描(与 VitalsDto 同格)。
            var diseaseErrs = PresentationDtoGuard.Scan(typeof(DeathTransitionDto));
            Assert.IsEmpty(diseaseErrs, "PresentationDtoGuard 递归扫描零违例(载体非空:DTO 有两字段)");

            // AC-29-07 面:零代价数值 + 全整数。
            var costErrs = ScanCostFields(typeof(DeathTransitionDto));
            Assert.IsEmpty(costErrs, string.Join("; ", costErrs));
        }

        /// <summary>负控(F4,独立于正例):违例 DTO 必须红 —— 否则本面是恒绿空转。</summary>
        [Test]
        public void test_dtoScan_negativeControls_turnRed()
        {
            Assert.IsNotEmpty(ScanCostFields(typeof(FakeGameOverDto)),
                "含 float 代价字段的 DTO 必须红(掉落数)—— 负控独立于正例");
            Assert.IsNotEmpty(PresentationDtoGuard.Scan(typeof(FakeDiagnosisDto)),
                "disease 词面字段必须被真守卫拦下");
        }

        /// <summary>违例样本:统计面板的最小形态(哪怕只显掉落数同样红)。</summary>
        private sealed class FakeGameOverDto
        {
            public float DropCount;   // float + 代价词面,双违例
        }

        /// <summary>违例样本:诊断词面(真守卫 PresentationDtoGuard 的拦截面)。</summary>
        private sealed class FakeDiagnosisDto
        {
            public int DiagnosisId;
        }

        // ══════════════════ AC-29-06 —— 白名单闭集(正向 + 违例红)══════════════════

        /// <summary>死亡呈现路径的类型闭集 —— 白名单正向断言;违例名单示例红。</summary>
        [Test]
        public void test_whitelist_closedSet_positive_andViolationRed()
        {
            // 白名单(story-006 AC-29-06):死亡呈现产物 ∈ {过渡动画, 环境音, 42 拟物元件}。
            // sim 投影面类型闭集 = {DTO, Query};42 视觉装配面 = {Phases(相位机), Presenter(呈现器)}
            // (2026-11-01 视觉装配批扩员 —— 程序化水墨 + 相位机,零写路径)。
            var whitelist = new HashSet<Type>
            {
                typeof(DeathTransitionDto), typeof(DeathTransitionQuery),
                typeof(DeathTransitionDriver), typeof(DeathTransitionPresenter),
            };

            Assert.IsTrue(whitelist.Contains(typeof(DeathTransitionDto)), "DTO ∈ 白名单");
            Assert.IsTrue(whitelist.Contains(typeof(DeathTransitionQuery)), "Query ∈ 白名单");
            Assert.IsTrue(whitelist.Contains(typeof(DeathTransitionDriver)), "相位机 ∈ 白名单(过渡动画)");
            Assert.IsTrue(whitelist.Contains(typeof(DeathTransitionPresenter)), "呈现器 ∈ 白名单(过渡动画)");

            // 违例名单示例:GameOver 面板 / 统计面板 —— 不在白名单,出现即红。
            Assert.IsFalse(whitelist.Contains(typeof(GameOverPanel)), "GameOver 面板 ∉ 白名单(违例示例)");
            Assert.IsFalse(whitelist.Contains(typeof(DeathStatisticsPanel)),
                "统计面板 ∉ 白名单(哪怕只显掉落数)");
        }

        /// <summary>违例桩件类型(仅存在于测试内,证明「出现即红」的判别面)。</summary>
        private sealed class GameOverPanel { }
        private sealed class DeathStatisticsPanel { }

        // ══════════════════ 投影真链(Phase 判据)══════════════════

        [Test]
        public void test_projection_none_thenDowned_thenReviving_throughRealChain()
        {
            var writer = new PlayerDiedWriter(_bag.EventSink, _bag.Encoder);

            // ① 未死 ⇒ PhaseNone
            Assert.AreEqual(DeathTransitionDto.PhaseNone,
                DeathTransitionQuery.Project(_bag.Stream.Events, _bag.Decoder, PlayerId, 1).Phase,
                "查无死亡事实 ⇒ 零叠层(Φ0)");

            // ② 真写 PlayerDied(S23-1 的写者,allowed 直给)⇒ PhaseDowned
            var result = writer.TryWrite(Tick, PlayerId, ActorClass.Player, LethalEntry(),
                DeathCell, true);
            Assert.AreEqual(PlayerDiedWriteResult.Written, result, "[前置] PlayerDied 真写");
            Assert.AreEqual(DeathTransitionDto.PhaseDowned,
                DeathTransitionQuery.Project(_bag.Stream.Events, _bag.Decoder, PlayerId, 1).Phase,
                "死亡后无复活落格 ⇒ 倒下(sim 面Φ1/Φ2)");

            // ③ 死亡之后复活落格(ActorCellEntered @ 更晚 tick)⇒ PhaseReviving
            var payload = new ActorCellEnteredPayload(PlayerId, BedCell, Tick + 10);
            var payloadRef = _bag.Encoder.Encode(EventKind.ActorCellEntered, payload);
            _bag.EventSink.Append(new SimEvent(Tick + 10, PatientId.None, -1,
                EventKind.ActorCellEntered, payloadRef));
            Assert.AreEqual(DeathTransitionDto.PhaseReviving,
                DeathTransitionQuery.Project(_bag.Stream.Events, _bag.Decoder, PlayerId, 1).Phase,
                "死亡后的落格 = 醒来 sim 面(Φ3)");
        }

        /// <summary>活人的日常跨格**不**误报 Reviving(判据锚在最近死亡之后)。</summary>
        [Test]
        public void test_projection_aliveActorDailyCellEvents_phaseNone()
        {
            var payload = new ActorCellEnteredPayload(PlayerId, BedCell, Tick);
            var payloadRef = _bag.Encoder.Encode(EventKind.ActorCellEntered, payload);
            _bag.EventSink.Append(new SimEvent(Tick, PatientId.None, -1,
                EventKind.ActorCellEntered, payloadRef));

            Assert.AreEqual(DeathTransitionDto.PhaseNone,
                DeathTransitionQuery.Project(_bag.Stream.Events, _bag.Decoder, PlayerId, 1).Phase,
                "没死过的人走不到相位 ② —— 日常跨格零误报");
        }

        [Test]
        public void test_projection_invalidArgs_throw()
        {
            Assert.Throws<ArgumentNullException>(() =>
                DeathTransitionQuery.Project(null, _bag.Decoder, PlayerId, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                DeathTransitionQuery.Project(_bag.Stream.Events, _bag.Decoder, -1, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                DeathTransitionQuery.Project(_bag.Stream.Events, _bag.Decoder, PlayerId, 2));
        }

        // ══════════════════ 呈现不持状态(零写路径)+ UX 前置 ══════════════════

        [Test]
        public void test_deathPresentation_zeroWritePath_sourceScan()
        {
            string dto = StripComments(File.ReadAllText(RepoFile(
                "unity/Assets/Sim.Contracts/DeathTransitionDto.cs")));
            string query = StripComments(File.ReadAllText(RepoFile(
                "unity/Assets/Sim/DeathTransitionQuery.cs")));
            string phases = StripComments(File.ReadAllText(RepoFile(
                "unity/Assets/Gameplay.Presentation/DeathTransition/DeathTransitionPhases.cs")));
            string presenter = StripComments(File.ReadAllText(RepoFile(
                "unity/Assets/Gameplay.Presentation/DeathTransition/DeathTransitionPresenter.cs")));

            Assert.That(dto.Contains(".Append(") || dto.Contains("IEventSink"), Is.False,
                "DTO 零写路径");
            Assert.That(query.Contains(".Append(") || query.Contains("IEventSink"), Is.False,
                "投影查询零写路径(重开呈现层不改 sim 事实)");
            Assert.That(phases.Contains(".Append(") || phases.Contains("IEventSink"), Is.False,
                "相位机零写路径(纯表现态)");
            Assert.That(presenter.Contains(".Append(") || presenter.Contains("IEventSink"), Is.False,
                "呈现器零写路径(只渲染;2026-11-01 视觉装配批同守)");
        }

        // ══════════════════ 相位机(42 视觉装配批 · spec §6)══════════════════

        /// <summary>Φ1 渐入 → Φ2 躺定:Downed 基准下 alpha 升满并保持。</summary>
        [Test]
        public void test_phases_downed_fadesIn_thenLies()
        {
            var d = DeathTransitionDriver.Inactive;
            d.Step(1, 0.3f);   // Downed 首帧:开始渐入
            Assert.AreEqual(DeathTransitionVisualPhase.Falling, d.Phase);
            Assert.That(d.Alpha, Is.GreaterThan(0f).And.LessThan(1f), "渐入中(非瞬间黑屏)");

            for (int i = 0; i < 200; i++) d.Step(1, 0.05f);   // 10 s ≫ FallSeconds
            Assert.AreEqual(DeathTransitionVisualPhase.Lying, d.Phase, "渐入满 ⇒ 躺定");
            Assert.AreEqual(1f, d.Alpha, "墨色满屏");
        }

        /// <summary>Φ2 **不自行醒来**:sim 迟迟不复活 ⇒ 保持躺定(spec §6 相位触发语义)。</summary>
        [Test]
        public void test_phases_lying_holdsUntilRevive_noAutoWake()
        {
            var d = DeathTransitionDriver.Inactive;
            d.Step(1, 0.1f);
            for (int i = 0; i < 400; i++) d.Step(1, 0.05f);   // 20 s,一直 Downed
            Assert.AreEqual(DeathTransitionVisualPhase.Lying, d.Phase,
                "静默段计满也不自行醒来 —— Φ3 只由复活落地触发");
            Assert.AreEqual(1f, d.Alpha);
        }

        /// <summary>Φ3 由 Reviving 触发 → Φ4 消散 → Φ0;总序列完整且单向。</summary>
        [Test]
        public void test_phases_reviving_wakeThenFadeToInactive()
        {
            var d = DeathTransitionDriver.Inactive;
            d.Step(1, 0.05f);
            for (int i = 0; i < 100; i++) d.Step(1, 0.05f);
            Assert.AreEqual(DeathTransitionVisualPhase.Lying, d.Phase, "[前置] 已躺定");

            d.Step(2, 0.05f);   // 复活落地
            Assert.AreEqual(DeathTransitionVisualPhase.Waking, d.Phase, "Reviving ⇒ 醒来(Φ3)");

            for (int i = 0; i < 20; i++) d.Step(2, 0.05f);   // 1.0 s < WakeSeconds
            Assert.AreEqual(DeathTransitionVisualPhase.Waking, d.Phase, "醒来保持中");

            for (int i = 0; i < 20; i++) d.Step(2, 0.05f);   // 累计 2.0 s ≥ WakeSeconds
            Assert.AreEqual(DeathTransitionVisualPhase.Fading, d.Phase, "醒来保持计满 ⇒ 消散(Φ4)");
            Assert.That(d.Alpha, Is.LessThan(1f), "消散中");

            for (int i = 0; i < 100; i++) d.Step(2, 0.05f);
            Assert.AreEqual(DeathTransitionVisualPhase.Inactive, d.Phase, "消散毕 ⇒ 零叠层(Φ0)");
            Assert.AreEqual(0f, d.Alpha);
        }

        /// <summary>AC-UX-03:静默段设计时长 = 最短段(四段比较,钉住形状约束)。</summary>
        [Test]
        public void test_phases_lieSeconds_isShortestSegment()
        {
            float lie = DeathTransitionDriver.LieSeconds;
            Assert.That(lie, Is.LessThan(DeathTransitionDriver.FallSeconds), "静默 < 渐入");
            Assert.That(lie, Is.LessThan(DeathTransitionDriver.WakeSeconds), "静默 < 醒来");
            Assert.That(lie, Is.LessThan(DeathTransitionDriver.FadeSeconds), "静默 < 消散");
        }

        /// <summary>Φ1 渐入非瞬间:alpha 在渐入段**单调递增且恒 ≤ 1**,不出现越冲。</summary>
        /// <remarks>
        /// ⚠️ **判据点钉在「渐入段」**(2026-11-01 证伪轮 M-B 实测):转 Lying 那一刻
        /// `Alpha` 被**满屏赋值覆盖**(1f)⇒ 钳位那行不参与终值 ⇒ 停用 `Clamp01` 也测不出
        /// (假绿,F8 形态:行为等价掩盖结构分叉)。
        /// ⇒ 本测改为在**渐入段内**逐帧取值,要求 alpha 严格递增、恒 ≤ 1、
        /// 且**渐入未计满前不提前转 Lying**;这三点合起来才把「渐入时长 = FallSeconds」
        /// 与「不过冲」两条性质钉住。
        /// </remarks>
        [Test]
        public void test_phases_alphaClamped_noOvershoot()
        {
            var d = DeathTransitionDriver.Inactive;
            d.Step(1, 0.1f);   // 首帧:进 Φ1 渐入(Elapsed 重置后同帧消费 0.1 s)
            Assert.AreEqual(DeathTransitionVisualPhase.Falling, d.Phase, "仍在渐入段");

            float prev = d.Alpha;
            Assert.That(prev, Is.GreaterThan(0f).And.LessThan(1f), "首帧渐入中(非瞬间黑屏)");

            // 逐帧推进到渐入段末端前:alpha 必须**严格递增**且恒 ≤ 1(不得越冲)。
            for (int i = 0; i < 10; i++)
            {
                d.Step(1, 0.1f);
                Assert.That(d.Alpha, Is.LessThanOrEqualTo(1f),
                    $"渐入段 alpha 不得越过 1(实际 {d.Alpha})—— 渐入钳位");
                Assert.That(d.Alpha, Is.GreaterThan(prev),
                    $"渐入段 alpha 必须严格递增(实际 {prev} → {d.Alpha})");
                prev = d.Alpha;
                if (d.Phase != DeathTransitionVisualPhase.Falling) break;
            }

            // 渐入计满 ⇒ 转 Lying 且满屏(alpha = 1,不越冲)。
            for (int i = 0; i < 20 && d.Phase == DeathTransitionVisualPhase.Falling; i++)
                d.Step(1, 0.1f);
            Assert.AreEqual(DeathTransitionVisualPhase.Lying, d.Phase, "渐入计满 ⇒ 躺定");
            Assert.That(d.Alpha, Is.LessThanOrEqualTo(1f), "满屏 alpha ≤ 1");
        }

        // ══════════════════ 1 侧传送实装(2026-11-01 接线兑付)══════════════════

        /// <summary>sink 守卫:未绑定请求抛(fail-loud,禁静默吞复活);二次绑定抛。</summary>
        [Test]
        public void test_teleportSink_unboundRequest_throws_doubleBindThrows()
        {
            var sink = new DaYiJingCheng.Gameplay.Presentation.Player.TeleportCommandSink();
            Assert.Throws<InvalidOperationException>(
                () => sink.RequestTeleport(7, new WorldPos(0, 1, 0)), "未绑定即请求 ⇒ 抛");

            sink.BindPlayer(() => null);   // 绑定读数为 null 的委托(玩家未生成的形态)
            Assert.Throws<InvalidOperationException>(
                () => sink.RequestTeleport(7, new WorldPos(0, 1, 0)), "绑定读数 null ⇒ 抛(fail-loud)");

            var sink2 = new DaYiJingCheng.Gameplay.Presentation.Player.TeleportCommandSink();
            sink2.BindPlayer(() => null);
            Assert.Throws<InvalidOperationException>(() => sink2.BindPlayer(() => null),
                "二次绑定 = 静默换真源,拒绝");
        }

        /// <summary>UX Flag(story-006 硬前置):全屏过渡 spec 已产出且 Approved。</summary>
        [Test]
        public void test_uxFlag_specExists_andApproved()
        {
            string spec = File.ReadAllText(RepoFile("design/ux/death-transition-29.md"));
            StringAssert.Contains("**Approved**", spec,
                "死亡/复活过渡 spec 已产出且过 /ux-review(先行于本 story 实现)");
        }

        // ── 夹具(纯数据)──────────────────────────────────────────────────

        private static DiseaseRegistryEntry LethalEntry()
            => new DiseaseRegistryEntry
            {
                DiseaseId = 1,
                DiseaseKey = "DIS_PLAGUE",
                Kind = DiseaseKind.Disease,
                Lethality = 3,
            };
    }
}
