// F1 上下文求值器 —— `TD-randomevents-05` 的判据(2026-11-05)。
//
// 卡面要求的四件事(债册 `TD-randomevents-05` 解除动作 ①–④):
//   ① `IsInClinic` 接 23 / 24 的真实地块集 → 本件钉「玩家格从**流**里取,再问谓词」
//   ② `IsOnExpedition` 按规则六之二 ③ 求值 → 本件钉「(有活跃出诊目标) ∧ (不在医馆)」
//   ③ `InCombatCooldown` → **P0 无来源**,恒 false(见 `F1ContextEvaluator` 文件头)
//   ④ 至少一条**真装配件**证明门能为真 → 本文件的被测类**就是**生产闭包调用的那个函数
//      (`CompositionRoot` 的 `contextOf` 现在只剩一行 `F1ContextEvaluator.Evaluate(...)`),
//      ⇒ 这里跑了什么,生产就跑什么。**不是**另写一份等价逻辑。
//
// ⚠️ **本文件证到什么、没证到什么**(禁借绿):
//   · 证到:求值器在给定流下给出的三个旗标正确;三项各自的 fail-safe。
//   · **没证到**:`isOnExpedition` 在**生产**里能为真 —— 出诊类 `event_key` 集今日是空集
//     (`TD-randomevents-06`),生产恒 false。本文件第 6 条用**合成 key 集**证明「集合有值
//     就能为真」,那是**能力证明**,不是「生产已生效」。
//
// 权威:GDD random-events.md 规则六之二(③ 出诊路径的定义)/ 规则十二 12.2 /
//   AC-52-29(BLOCKING,医馆只压威胁档)/ ADR-020 §四(玩家位移的表现态唯一投影)/ ADR-015 §三

using System.Collections.Generic;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.RandomEvents
{
    [TestFixture]
    public class F1ContextEvaluatorTest
    {
        private const int PlayerActorId = 1;
        private const int OtherActorId = 2;
        private const int HouseCallEventKey = 7;
        private const int Tick = 1200;

        private static readonly WorldPos ClinicCell = new WorldPos(5, 0, 1);
        private static readonly WorldPos OutsideCell = new WorldPos(9, 0, 9);

        private InMemoryBlobPool _pool;
        private PayloadEncoder _encoder;
        private PayloadDecoder _decoder;
        private ClinicStub _clinic;
        private List<SimEvent> _stream;

        [SetUp]
        public void Setup()
        {
            _pool = new InMemoryBlobPool();
            _encoder = new PayloadEncoder(_pool);
            _decoder = new PayloadDecoder(_pool);
            _clinic = new ClinicStub();
            _clinic.Cells.Add(ClinicCell);
            _stream = new List<SimEvent>();
        }

        // ── 替身 ──────────────────────────────────────────────────────────

        /// <summary>医馆格判据替身(显式格集合)。同源义务的「单实例发放」归装配轮,stub 不构成「已同源」。</summary>
        private sealed class ClinicStub : IClinicCellSource
        {
            public readonly HashSet<WorldPos> Cells = new HashSet<WorldPos>();
            public bool IsClinicCell(WorldPos cell) => Cells.Contains(cell);
        }

        // ── 流构造(急召 / onset 的合法写者归 9 / 52;这里只物化)──────────────

        private void AddEntered(long tick, WorldPos cell, int actorId = PlayerActorId)
        {
            var p = _encoder.Encode(EventKind.ActorCellEntered,
                new ActorCellEnteredPayload(actorId, cell, tick));
            _stream.Add(new SimEvent(tick, PatientId.None, -1, EventKind.ActorCellEntered, p));
        }

        private void AddSummons(long tick, int eventKey = HouseCallEventKey)
        {
            var p = _encoder.Encode(EventKind.EventArrived,
                new EventArrivedPayload(eventKey, 0, OutsideCell, -1));
            _stream.Add(new SimEvent(tick, PatientId.None, -1, EventKind.EventArrived, p));
        }

        private void AddOnset(long tick, int patientId)
        {
            var p = _encoder.Encode(EventKind.DiseaseOnset,
                new DiseaseOnsetPayload(tick, 3, patientId, tick * 31 + patientId, -1));
            _stream.Add(new SimEvent(tick, PatientId.None, -1, EventKind.DiseaseOnset, p));
        }

        /// <summary>一条可用的急召 = EventArrived + 同 tick 的 DiseaseOnset(规则十二 12.2 链接)。</summary>
        private void AddActiveSummons(long tick)
        {
            AddSummons(tick);
            AddOnset(tick, patientId: 3);
        }

        private F1PipelineInput Eval(int playerActorId = PlayerActorId,
                                     IReadOnlyCollection<int> houseCallKeys = null,
                                     long tick = Tick)
        {
            return F1ContextEvaluator.Evaluate(
                tick, _stream, _decoder, _clinic, playerActorId,
                houseCallKeys ?? new[] { HouseCallEventKey }, isNight: false);
        }

        // ── ① IsInClinic ────────────────────────────────────────────────

        [Test]
        public void test_playerOnClinicCell_isInClinic()
        {
            AddEntered(Tick, ClinicCell);

            F1PipelineInput input = Eval();

            Assert.IsTrue(input.IsInClinic,
                "玩家格 ∈ 医馆地块集 ⇒ IsInClinic = true。" +
                "改前此值在装配层硬编码为 false(医馆永远「不在」)");
            Assert.IsFalse(input.IsOnExpedition, "在医馆内不得同时判成出诊(规则六之二 ③)");
        }

        [Test]
        public void test_playerOutsideClinic_notInClinic()
        {
            AddEntered(Tick, OutsideCell);

            Assert.IsFalse(Eval().IsInClinic, "玩家格 ∉ 医馆地块集 ⇒ false");
        }

        [Test]
        public void test_lastCellWins_notFirst()
        {
            // 先走进医馆,再走出去 —— 判定必须看**最后一条**,不是第一条。
            AddEntered(Tick, ClinicCell);
            AddEntered(Tick + 1, OutsideCell);

            Assert.IsFalse(Eval(tick: Tick + 1).IsInClinic,
                "最后一条 ActorCellEntered 决定此刻位置(取第一条 = 用旧位置判新时刻)");
        }

        [Test]
        public void test_otherActorCell_doesNotCount()
        {
            // 别的实体站在医馆里 ≠ 玩家站在医馆里。
            AddEntered(Tick, ClinicCell, actorId: OtherActorId);

            Assert.IsFalse(Eval().IsInClinic, "跨格事件须按玩家 actorId 筛");
        }

        [Test]
        public void test_noCellInStream_failsClosed()
        {
            Assert.IsFalse(Eval().IsInClinic, "流里还没有玩家的格 ⇒ false(fail-safe:不知道在哪按「不在医馆」)");
            Assert.IsFalse(Eval().IsOnExpedition, "同上");
        }

        [Test]
        public void test_unboundPlayer_failsClosed()
        {
            AddEntered(Tick, ClinicCell);

            Assert.IsFalse(Eval(playerActorId: F1ContextEvaluator.NoPlayer).IsInClinic,
                "未绑定玩家(-1)不得拿哨兵去扫流 —— 必须短路成 false");
        }

        // ── ② IsOnExpedition ───────────────────────────────────────────

        [Test]
        public void test_activeSummonsAndOutside_isOnExpedition()
        {
            AddActiveSummons(tick: 100);
            AddEntered(Tick, OutsideCell);

            F1PipelineInput input = Eval();

            Assert.IsTrue(input.IsOnExpedition,
                "有活跃出诊目标 ∧ 不在医馆 ⇒ 在出诊路径上(规则六之二 ③)");
            Assert.IsFalse(input.IsInClinic, "对照:不在医馆");
        }

        [Test]
        public void test_activeSummonsButInClinic_isNotOnExpedition()
        {
            AddActiveSummons(tick: 100);
            AddEntered(Tick, ClinicCell);

            Assert.IsFalse(Eval().IsOnExpedition,
                "两项是合取 —— 在医馆时即便有急召也不算出诊路径");
        }

        [Test]
        public void test_noSummons_isNotOnExpedition()
        {
            AddEntered(Tick, OutsideCell);

            Assert.IsFalse(Eval().IsOnExpedition, "没有活跃出诊目标 ⇒ false");
        }

        [Test]
        public void test_emptyHouseCallKeys_neverOnExpedition()
        {
            // 🔁 这是**生产今日的真实形态**(`TD-randomevents-06`:集合归 37 内容件回填)。
            AddActiveSummons(tick: 100);
            AddEntered(Tick, OutsideCell);

            Assert.IsFalse(Eval(houseCallKeys: new int[0]).IsOnExpedition,
                "出诊类 key 集为空 ⇒ 任何 EventArrived 都不算急召 ⇒ 恒 false。" +
                "此时『等数据』而非『等代码』—— 集合一填即真(上一条用例已证)");
        }

        // ── ③ InCombatCooldown(P0 无来源)────────────────────────────────

        [Test]
        public void test_combatCooldown_isFalseInP0()
        {
            AddEntered(Tick, OutsideCell);

            Assert.IsFalse(Eval().InCombatCooldown,
                "P0 无「战斗结算后冷却窗」的来源 ⇒ 恒 false 是**当下正确值**,不是占位");
        }

        // ── ④ 门:医馆通过、冷却不过(AC-52-29 的落点)────────────────────

        [Test]
        public void test_gate_openInClinic_andQuotaZeroesThreat()
        {
            // 端到端意:求值器的输出直接喂 F1 —— 医馆里**门通过**,只是威胁档配额为 0。
            // ⚠️ 求值器交出的 `WindowSize = 0`(它只产**上下文旗标**);真实调用方
            //    (`RandomEventSpawnChain` ⑤)会用数据集里的 `WINDOW_SIZE` 覆盖后再 `Execute`。
            //    本用例照同一条路走,不拿 0 去调 `HamiltonSplit`(那是 AC-52-36 的构建期拒收)。
            AddEntered(Tick, ClinicCell);

            F1PipelineInput flags = Eval();
            var input = new F1PipelineInput(
                window: 0, windowSize: 10,
                isNight: flags.IsNight, isOnExpedition: flags.IsOnExpedition,
                isInClinic: flags.IsInClinic, inCombatCooldown: flags.InCombatCooldown,
                seasonIndex: flags.SeasonIndex);

            Assert.IsTrue(EventF1Pipeline.EvaluateContextGate(input),
                "AC-52-29:医馆内门必须通过(机会 / 反应档照常生成)");

            F1PipelineOutput output = EventF1Pipeline.Execute(input, minTier: 0, maxTier: 3);
            Assert.AreEqual(0, output.Quotas[0], "威胁档被 ContextMult 压成 0");
            Assert.Greater(output.Quotas[1], 0, "机会档照常");
        }
    }
}
