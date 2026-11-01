// S23-3 · story-003 技能掉级 F-29-1(整数截断)—— AC 闭夹具(2026-11-01)
//
// ─────────────────────────────────────────────────────────────────────────────
// 读哪些仓库文件 / 写哪里(test-standards 具名声明义务)
// ─────────────────────────────────────────────────────────────────────────────
// 读(只读,零写盘):
//   ① `unity/Assets/Sim/DeathSkillDownSettlement.cs`、`unity/Assets/Sim/DeathCandidateConsumer.cs`、
//      `unity/Assets/Sim.Contracts/SkillSystem/DeathPenalty.cs` 的**源码**(剥注释后扫描)——
//      为什么必须读真源:「掉级路径零浮点 / 零 Fix.Div / 零 floor_Fix(Mul)」(AC-29-04)与
//      「29 零公式重复实现 / 零掉级 Kind Append 点」(AC-29-14 / 16)是**跨文件的源码级
//      契约**,类型面表达不了,只能源码断言(与 S23-1 唯一 Append 点扫描同法)。
// 写:**零写盘**(流与装配全内存)。
//
// ── 与既有件的分工(不重复)────────────────────────────────────────────────────
//   `death_penalty_test.cs`(SkillSystem):30 侧纯函数 F-29-1 表逐位 + 陷阱钉 —— 本件
//       只补**29 侧编排面**(真链触发 / 豁免两极端 / 时序 / 源码契约),不重复表值断言。
//   `skill_grown_stream_test.cs`(SkillSystem):SkillGrown 路由 / round-trip —— 不重复。
//   `death_drop_stream_test.cs`(S23-2):掉落结算段 —— 本件的时序测试只钉「掉落之后」
//       这一半关系,不重复掉落语义。
//
// ── AC → 测试映射(story-003)──────────────────────────────────────────────────
//   AC-29-05(29 侧半边;全链随 G-1 门行收口核)→ test_fiveValueTable_throughRealChain…(真冷却门链)
//   AC-29-04                                    → test_ac2904_staticScan_intTruncationOnly
//   AC-29-14                                    → test_ac2914_noFormulaDuplication_in29Side
//   AC-29-16(两次喂入半;零掉级 Kind 半 = ⑪)
//                                               → test_rebuildTwice_replayedStream_identicalLevels
//   判断类豁免(R-4-W-6 待裁;两极端登记)      → test_exemptionBranchAllDown… / …AllKept…
//   同 tick 时序(掉落 → 掉级)                  → test_settlementSegment_dropBeforeSkillDown…
//   零变化零事件                                → test_ungrownActor… / test_levelZeroRecord…
//   消费者未接线(002 行为不变)                 → test_consumerUnwired_skillDown_002BehaviorPreserved
//
// ── 2026-11-01 权威面订正(如实登记)──────────────────────────────────────────
//   30 侧 DeathPenalty.Apply 原为 Fix 路径(与 F-29-1 表值差一)—— 本卡实施的同批
//   订正把 Apply 本体改为 F-29-1 权威式(整数截断),29 照卡面消费之 ⇒ AC-29-14
//   「唯一出处」在**订正后的**权威面上成立。详见结算面文件头与证据原件。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.Contracts.SkillSystem;
using DaYiJingCheng.Sim.Foraging;
using NUnit.Framework;
using UnityEngine;

namespace DaYiJingCheng.Tests.DeathRespawn
{
    /// <summary>story-003 的 AC 闭:真链触发、豁免两极端、时序、源码契约。</summary>
    [TestFixture]
    public sealed class DeathSkillDownTest
    {
        private CompositionRootServices _bag;
        private DeathSkillDownSettlement _settlement;

        private const int PlayerId = 7;
        private const long Tick = 100;

        [SetUp]
        public void Setup()
        {
            _bag = CompositionRoot.Assemble();
            _settlement = NewSettlement(isExempt: null);   // 生产装配形态(null = 全掉)
        }

        private DeathSkillDownSettlement NewSettlement(Func<int, bool> isExempt)
            => new DeathSkillDownSettlement(
                new SkillGrownEmitterAdapter(_bag.EventSink, _bag.Encoder),
                () => _bag.Stream.Events, _bag.Decoder, isExempt);

        /// <summary>给玩家播种技能等级(走真流事件:SkillGrown 携带绝对等级 ——
        /// 与生产的等级真源同源;ids 2..6 避开判断类候选 0/1 的语义噪声)。</summary>
        private void SeedLevel(int skillId, int level, long tick)
            => new SkillGrownEmitterAdapter(_bag.EventSink, _bag.Encoder)
                .Emit(tick, PlayerId, skillId, 0, NoveltyClass.Normal, level, Fix.Zero);

        private int CountOf(EventKind kind) => _bag.Stream.Events.Count(e => e.Kind == kind);

        /// <summary>全流重建成 (actor, skill, level) 升序三元组表(确定性比较面)。</summary>
        private List<string> LevelTable()
        {
            Dictionary<int, Dictionary<int, int>> levels = SkillStateRebuilder.RebuildLevels(
                _bag.Stream.Events
                    .Where(e => e.Kind == EventKind.SkillGrown)
                    .Select(e => _bag.Decoder.Decode<SkillGrownPayload>(e.Kind, e.Payload)));
            return levels
                .SelectMany(a => a.Value, (a, s) => $"{a.Key}|{s.Key}|{s.Value}")
                .OrderBy(t => t, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>结算段尾部的降级 SkillGrown 的 Level 序(按流序)。</summary>
        private List<int> DowngradedLevels()
            => _bag.Stream.Events
                .Where(e => e.Kind == EventKind.SkillGrown && e.Tick == Tick)
                .Select(e => _bag.Decoder.Decode<SkillGrownPayload>(e.Kind, e.Payload).Level)
                .ToList();

        private static string RepoFile(string relative) =>
            Path.Combine(Application.dataPath, "..", "..", relative.Replace('/', Path.DirectorySeparatorChar));

        private static string StripComments(string src)
        {
            string noBlock = Regex.Replace(src, @"/\*.*?\*/", " ", RegexOptions.Singleline);
            return Regex.Replace(noBlock, @"//[^\n]*", " ");
        }

        // ══════════════════ AC-29-05(29 侧半边)—— 五档表走真链 ══════════════════

        /// <summary>F-29-1 五档表经**真冷却门链**(谓词 + 冷却 + 写者 + 结算回调)逐位兑现。
        /// ⚠️ 「29 侧半边」口径:S23-1 已证真链可行;G-1 门行收口核归 epic 收口(全链绿)。</summary>
        [Test]
        public void test_fiveValueTable_throughRealChain_perF291()
        {
            int[] seeded = { 1, 10, 20, 40, 60 };
            int[] expected = { 0, 9, 19, 38, 57 };
            for (int i = 0; i < seeded.Length; i++)
                SeedLevel(2 + i, seeded[i], 1 + i);

            PlayerDiedWriter writer = new PlayerDiedWriter(_bag.EventSink, _bag.Encoder);
            var consumer = new DeathCandidateConsumer(
                writer,
                deathAllowed: _bag.DeathCooldown.DeathAllowed,
                actorClassOf: _ => ActorClass.Player,
                entryOf: _ => LethalEntry(),
                deathCellOf: _ => new WorldPos(0, 1, 0),
                dropSettlement: null,
                skillDown: (playerId, tick) => _settlement.Settle(tick, playerId));

            int playerDiedBefore = CountOf(EventKind.PlayerDied);
            consumer.OnDeathCandidate(PlayerId, Tick);

            Assert.AreEqual(playerDiedBefore + 1, CountOf(EventKind.PlayerDied),
                "真链前置:PlayerDied 恰写一条(结算回调只在 Written 后跑)");
            Assert.AreEqual(seeded.Length, DowngradedLevels().Count, "五技能 ⇒ 五条降级事件");
            Assert.AreEqual(expected, DowngradedLevels(),
                "F-29-1 表逐位:{1,10,20,40,60} → {0,9,19,38,57}(int 截断,非定点)");
        }

        /// <summary>PlayerDied 与降级 SkillGrown 同 tick(结算段在死亡结算段内)。</summary>
        [Test]
        public void test_downgradeEvents_sameTickAsPlayerDied()
        {
            SeedLevel(2, 20, 1);
            PlayerDiedWriter writer = new PlayerDiedWriter(_bag.EventSink, _bag.Encoder);
            var consumer = new DeathCandidateConsumer(
                writer, _bag.DeathCooldown.DeathAllowed,
                _ => ActorClass.Player, _ => LethalEntry(), _ => new WorldPos(0, 1, 0),
                skillDown: (playerId, tick) => _settlement.Settle(tick, playerId));

            consumer.OnDeathCandidate(PlayerId, Tick);

            long diedTick = _bag.Stream.Events
                .First(e => e.Kind == EventKind.PlayerDied).Tick;
            CollectionAssert.IsNotEmpty(DowngradedLevels(), "有降级事件(前置)");
            Assert.IsTrue(DowngradedLevels().Count > 0);
            Assert.AreEqual(diedTick, Tick,
                "降级事件 tick 与 PlayerDied 同 tick(掉级结算段不另取时刻)");
        }

        // ══════════════════ 零变化零事件 ══════════════════

        [Test]
        public void test_ungrownActor_zeroEvents()
        {
            int before = CountOf(EventKind.SkillGrown);

            int emitted = _settlement.Settle(Tick, PlayerId);

            Assert.AreEqual(0, emitted, "无成长记录 ⇒ 全 0 级 ⇒ 零事件");
            Assert.AreEqual(before, CountOf(EventKind.SkillGrown), "流零增量");
        }

        /// <summary>已登记但 0 级:Apply(0)=0 恒等 ⇒ 零事件(0 级技能不掉不刷)。</summary>
        [Test]
        public void test_levelZeroRecord_zeroChange_noEvent()
        {
            SeedLevel(2, 0, 1);
            int before = CountOf(EventKind.SkillGrown);

            int emitted = _settlement.Settle(Tick, PlayerId);

            Assert.AreEqual(0, emitted, "0 → 0 无变化 ⇒ 零事件(死亡不刷恒等行)");
            Assert.AreEqual(before, CountOf(EventKind.SkillGrown));
        }

        // ══════════════════ 判断类豁免 —— 两极端登记(R-4-W-6 口径待裁)══════════════════

        /// <summary>正控:无豁免(null)= 全掉 —— 生产装配现值。</summary>
        [Test]
        public void test_exemptionBranchAllDown_settlesEverySkill()
        {
            SeedLevel(2, 20, 1);
            SeedLevel(3, 40, 2);
            SeedLevel(4, 60, 3);

            int emitted = _settlement.Settle(Tick, PlayerId);   // isExempt = null

            Assert.AreEqual(3, emitted, "无豁免 ⇒ 每个有成长的技能各掉一次");
            CollectionAssert.AreEqual(new[] { 19, 38, 57 }, DowngradedLevels());
        }

        /// <summary>负控(F4,独立于正例):全豁免 ⇒ 零事件 —— 两极端的行为差异登记,
        /// 豁免口径(R-4-W-6)未裁,本件不实现选择。</summary>
        [Test]
        public void test_exemptionBranchAllKept_zeroDowngrades()
        {
            SeedLevel(2, 20, 1);
            SeedLevel(3, 40, 2);
            int before = CountOf(EventKind.SkillGrown);

            DeathSkillDownSettlement exempting = NewSettlement(_ => true);
            int emitted = exempting.Settle(Tick, PlayerId);

            Assert.AreEqual(0, emitted, "全豁免 ⇒ 零降级(与全掉分支同一播种态,行为不同)");
            Assert.AreEqual(before, CountOf(EventKind.SkillGrown));
        }

        // ══════════════════ 结算段时序 ══════════════════

        /// <summary>结算段序:掉落 → 掉级(都在 PlayerDied Written 之后)。</summary>
        [Test]
        public void test_settlementSegment_dropBeforeSkillDown()
        {
            var calls = new List<string>();
            PlayerDiedWriter writer = new PlayerDiedWriter(_bag.EventSink, _bag.Encoder);
            var consumer = new DeathCandidateConsumer(
                writer, _bag.DeathCooldown.DeathAllowed,
                _ => ActorClass.Player, _ => LethalEntry(), _ => new WorldPos(0, 1, 0),
                dropSettlement: (id, cell, tick) => calls.Add("drop"),
                skillDown: (id, tick) => calls.Add("skill"));

            consumer.OnDeathCandidate(PlayerId, Tick);

            CollectionAssert.AreEqual(new[] { "drop", "skill" }, calls,
                "结算段序 = 掉落后掉级(story-005:46;复活命令归 story-005)");
        }

        /// <summary>未接线(skillDown = null)⇒ 零降级 —— 002 形态行为不变(可选参数纪律)。</summary>
        [Test]
        public void test_consumerUnwired_skillDown_002BehaviorPreserved()
        {
            SeedLevel(2, 20, 1);
            PlayerDiedWriter writer = new PlayerDiedWriter(_bag.EventSink, _bag.Encoder);
            var consumer = new DeathCandidateConsumer(
                writer, _bag.DeathCooldown.DeathAllowed,
                _ => ActorClass.Player, _ => LethalEntry(), _ => new WorldPos(0, 1, 0));
            int before = CountOf(EventKind.SkillGrown);

            consumer.OnDeathCandidate(PlayerId, Tick);

            Assert.AreEqual(1, CountOf(EventKind.PlayerDied), "001/002 行为:PlayerDied 照写");
            Assert.AreEqual(before, CountOf(EventKind.SkillGrown), "未接线 ⇒ 零降级事件");
        }

        // ══════════════════ AC-29-16(半边)—— 两次喂入逐位一致 ══════════════════

        /// <summary>同一 PlayerDied + SkillGrown 序列喂入两次(重放进独立装配的同构流)
        /// ⇒ Level(s)@t 逐位一致 —— F-29-4 流重建的确定性面(折叠豁免半由
        /// `HistoryFolder.Erase` 的 SkillGrown 免抹除承载,7a 侧夹具覆盖,不在此重复)。</summary>
        [Test]
        public void test_rebuildTwice_replayedStream_identicalLevels()
        {
            SeedLevel(2, 20, 1);
            SeedLevel(3, 40, 2);
            PlayerDiedWriter writer = new PlayerDiedWriter(_bag.EventSink, _bag.Encoder);
            var consumer = new DeathCandidateConsumer(
                writer, _bag.DeathCooldown.DeathAllowed,
                _ => ActorClass.Player, _ => LethalEntry(), _ => new WorldPos(0, 1, 0),
                skillDown: (id, tick) => _settlement.Settle(tick, id));
            consumer.OnDeathCandidate(PlayerId, Tick);

            List<SimEvent> sequence = _bag.Stream.Events.ToList();
            List<string> first = LevelTable();
            List<string> second = LevelTable();
            CollectionAssert.AreEqual(first, second, "同序列两次重建立即一致");

            // 重放进独立装配(B):把 A 流的 SkillGrown 序列**解码后经 30 成长面原样重写**
            // (同 S23-2 重放法 —— 裸 Append 跨池会断 blob 寻址;字段逐项保真,Level 原值)。
            CompositionRootServices bagB = CompositionRoot.Assemble();
            var replayEmitter = new SkillGrownEmitterAdapter(bagB.EventSink, bagB.Encoder);
            foreach (SimEvent e in sequence)
            {
                if (e.Kind != EventKind.SkillGrown)
                    continue;
                SkillGrownPayload p = _bag.Decoder.Decode<SkillGrownPayload>(e.Kind, e.Payload);
                replayEmitter.Emit(e.Tick, p.ActorId, p.SkillId, p.ObjectId,
                    (NoveltyClass)p.NoveltyClass, p.Level, p.XpGain);
            }
            Dictionary<int, Dictionary<int, int>> rebuilt = SkillStateRebuilder.RebuildLevels(
                bagB.Stream.Events
                    .Where(e => e.Kind == EventKind.SkillGrown)
                    .Select(e => bagB.Decoder.Decode<SkillGrownPayload>(e.Kind, e.Payload)));

            Assert.IsTrue(rebuilt.TryGetValue(PlayerId, out Dictionary<int, int> bySkill));
            Assert.AreEqual(19, bySkill[2], "重放后技能 2 = 19(与原装配逐位同)");
            Assert.AreEqual(38, bySkill[3], "重放后技能 3 = 38");
            Assert.AreEqual(2, bySkill.Count, "重放面 = 该玩家两条成长记录,零多零少");
        }

        // ══════════════════ AC-29-04 —— 掉级路径静态扫描 ══════════════════

        /// <summary>掉级路径(29 结算面 + 30 纯函数)剥注释后:零浮点字面量、零 Fix.Div、
        /// 零 Fix.Mul(= floor_Fix(Mul) 的载体)、零 float;正判:权威式走 int 截断除法。</summary>
        [Test]
        public void test_ac2904_staticScan_intTruncationOnly()
        {
            string settlement = StripComments(File.ReadAllText(RepoFile(
                "unity/Assets/Sim/DeathSkillDownSettlement.cs")));
            string penalty = StripComments(File.ReadAllText(RepoFile(
                "unity/Assets/Sim.Contracts/SkillSystem/DeathPenalty.cs")));
            string path = settlement + "\n" + penalty;

            Assert.That(path, Does.Not.Contain("Fix.Div"), "F-29-1:禁 Fix.Div(9.5 会舍成 10)");
            Assert.That(path, Does.Not.Contain(".Mul("), "F-29-1:禁 floor_Fix(Mul)(量化丢低位)");
            Assert.That(path.Contains("float"), Is.False, "零 float 关键字");
            Assert.That(Regex.IsMatch(path, @"\d+\.\d+"), Is.False, "零浮点字面量");

            Assert.That(penalty, Does.Contain("/ LossDen"),
                "权威式 = (Level × 19) / 20 的 int 截断除法(正判)");
            Assert.That(settlement, Does.Contain("DeathPenalty.Apply("),
                "29 消费 30 的权威面(AC-29-14 正判)");
        }

        // ══════════════════ AC-29-14 / AC-29-16(零掉级 Kind 半边)—— 检索断言 ══════════════════

        /// <summary>29 侧掉级文件零公式重复实现(检索):比值算式只住 DeathPenalty.cs;
        /// 结算面零 Append 点(掉级只经 30 的既有成长面 SkillGrown 表达,零第二真源)。</summary>
        [Test]
        public void test_ac2914_noFormulaDuplication_in29Side()
        {
            string settlement = StripComments(File.ReadAllText(RepoFile(
                "unity/Assets/Sim/DeathSkillDownSettlement.cs")));
            string consumer = StripComments(File.ReadAllText(RepoFile(
                "unity/Assets/Sim/DeathCandidateConsumer.cs")));

            Assert.That(settlement.Contains("LossNum") || settlement.Contains("* 19")
                || settlement.Contains("/ 20"), Is.False,
                "掉级比值算式不得在 29 侧再现(唯一出处 = DeathPenalty.Apply)");
            Assert.That(consumer.Contains("LossNum") || consumer.Contains("* 19")
                || consumer.Contains("/ 20"), Is.False, "消费者层同样零公式");
            Assert.That(settlement.Contains(".Append(") || settlement.Contains("IEventSink"),
                Is.False, "结算面零 Append 点 —— 掉级不落新 Kind,经 30 成长面表达(AC-29-16)");
        }

        // ══════════════════ 参数守卫 ══════════════════

        [Test]
        public void test_settle_invalidArguments_throw()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _settlement.Settle(-1, PlayerId));
            Assert.Throws<ArgumentOutOfRangeException>(() => _settlement.Settle(Tick, -1));
            Assert.Throws<ArgumentNullException>(() =>
                new DeathSkillDownSettlement(null, () => _bag.Stream.Events, _bag.Decoder));
            Assert.Throws<ArgumentNullException>(() =>
                new DeathSkillDownSettlement(
                    new SkillGrownEmitterAdapter(_bag.EventSink, _bag.Encoder), null, _bag.Decoder));
            Assert.Throws<ArgumentNullException>(() =>
                new DeathSkillDownSettlement(
                    new SkillGrownEmitterAdapter(_bag.EventSink, _bag.Encoder),
                    () => _bag.Stream.Events, null));
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
