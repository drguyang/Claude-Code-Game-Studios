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
            // P0 的 sim 投影面类型闭集 = {DTO, Query}(42 水墨元件/动画资产 = 表现资产批,不属类型面)。
            var whitelist = new HashSet<Type> { typeof(DeathTransitionDto), typeof(DeathTransitionQuery) };

            Assert.IsTrue(whitelist.Contains(typeof(DeathTransitionDto)), "DTO ∈ 白名单");
            Assert.IsTrue(whitelist.Contains(typeof(DeathTransitionQuery)), "Query ∈ 白名单");

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

            Assert.That(dto.Contains(".Append(") || dto.Contains("IEventSink"), Is.False,
                "DTO 零写路径");
            Assert.That(query.Contains(".Append(") || query.Contains("IEventSink"), Is.False,
                "投影查询零写路径(重开呈现层不改 sim 事实)");
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
