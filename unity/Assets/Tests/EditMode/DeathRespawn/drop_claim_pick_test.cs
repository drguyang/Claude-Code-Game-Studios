// S23-6 · sprint-23 拾取三段式(smoke #8 载体)—— AC 闭夹具(2026-11-01)
//
// ─────────────────────────────────────────────────────────────────────────────
// 读哪些仓库文件 / 写哪里(test-standards 具名声明义务)
// ─────────────────────────────────────────────────────────────────────────────
// 读(只读,零写盘):
//   ① `unity/Assets/Sim/DropClaimOrchestrator.cs` 的**源码**(剥注释后扫描)——
//      为什么必须读真源:「零铸造」(载荷 id = 投影行原样)与「零直接 Append」
//      (claim 经 20 写者)是**跨文件调用约定**,类型面表达不了(S23-1/S23-2 同法)。
// 写:**零写盘**(流与装配全内存)。
//
// ── ⚠️ 合成输入驱动,禁双重空集假绿(S22-4 同条)─────────────────────────────
// 每条正例**先断 seed 生效**(投影里确有地面候选),再断 claim 行为;每条负例
// **独立于正例**(不是「流本来就空」的空洞正控 —— 负例先证明同型正例能过或
// 先断 seed 在场)。宽容半径 = 生产装配常量 `CompositionRoot.PickToleranceCells = 1`:
// 边界测试钉的就是该语义(半径改值须同批改本测 —— 值与判据是同一裁定)。
//
// ── smoke #8 绑定(critical_paths.json 同批改)────────────────────────────────
//   #8「拾取:意图事件 + 当下判距 + 宽容半径 —— 离屏拾取被拒(非崩溃)」
//   → status: test;check = 本件 test_smoke8_pickTriple…(正例全链)+
//     test_smoke8_offRange_rejected_zeroEvents(负例零事件)。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;
using NUnit.Framework;
using UnityEngine;

namespace DaYiJingCheng.Tests.DeathRespawn
{
    /// <summary>story S23-6 拾取三段式:意图 → 判距 → 宽容半径;离屏拒且零事件。</summary>
    [TestFixture]
    public sealed class DropClaimPickTest
    {
        private CompositionRootServices _bag;
        private DropEventWriter _drops;
        private DropClaimOrchestrator _claim;

        private const int PlayerId = 7;
        private const long Tick = 100;
        private static readonly WorldPos DropCell = new WorldPos(2, 0, 2);

        [SetUp]
        public void Setup()
        {
            _bag = CompositionRoot.Assemble();
            _drops = new DropEventWriter(_bag.EventSink, _bag.Encoder, _bag.IdAuthority);
            _claim = _bag.DropClaimOrchestrator;   // 生产装配形态(容差 = 装配常量)
        }

        /// <summary>在地上放一件掉落物(真流事件;不 claim —— 留在 NoOwner)。</summary>
        private long SeedDrop(WorldPos cell, int itemKey = 3, int qty = 1)
            => _drops.RecordSpawned(Tick - 10, cell, itemKey, qty).Value;

        private InventoryProjection Projection()
            => InventoryFold.Fold(_bag.Stream.Events, _bag.Decoder, InventoryFoldContext.None).Projection;

        private int CountOf(EventKind kind) => _bag.Stream.Events.Count(e => e.Kind == kind);

        private static string RepoFile(string relative) =>
            Path.Combine(Application.dataPath, "..", "..", relative.Replace('/', Path.DirectorySeparatorChar));

        private static string StripComments(string src)
        {
            string noBlock = Regex.Replace(src, @"/\*.*?\*/", " ", RegexOptions.Singleline);
            return Regex.Replace(noBlock, @"//[^\n]*", " ");
        }

        // ══════════════════ smoke #8 正例 —— 三段式全链 ══════════════════

        /// <summary>意图(E 沿 → TryClaimInstance)+ 当下判距(经流确立格)+ 宽容半径内
        /// ⇒ claim **被指名**的那一件;载荷 id = 投影行**原样**(零铸造);库存恢复经 20 fold。
        /// <para>🔁 2026-11-02(`TD-interaction-02`):「打给哪一件」已归 4 的目标选择器
        /// (F-4.1 全序),本类只结算被指名的那一件 ⇒ 夹具随之改为传 instance_id。</para></summary>
        [Test]
        public void test_smoke8_pickTriple_inRange_claimsGroundInstance()
        {
            long seededId = SeedDrop(DropCell);
            // [前置] seed 生效:投影里确有该地面候选(禁「空集正控」)。
            Assert.IsTrue(Projection().TryGet(seededId, out InventoryRow seeded));
            Assert.AreEqual(InventoryProjection.NoOwner, seeded.Owner, "[前置] 候选未归属(在地上)");
            int beforeClaimed = CountOf(EventKind.DropClaimed);

            bool consumed = _claim.TryClaimInstance(PlayerId, Tick, DropCell, seededId);

            Assert.IsTrue(consumed, "半径内 ⇒ 按键被拾取链消费");
            Assert.AreEqual(beforeClaimed + 1, CountOf(EventKind.DropClaimed), "恰一条 DropClaimed");

            SimEvent claimed = _bag.Stream.Events.Last(e => e.Kind == EventKind.DropClaimed);
            DropClaimedPayload p = _bag.Decoder.Decode<DropClaimedPayload>(claimed.Kind, claimed.Payload);
            Assert.AreEqual(seededId, p.InstanceId, "载荷 id = 播种行 id 原样(零铸造)");
            Assert.AreEqual(PlayerId, p.Claimer, "归属 = 拾取者");

            // 库存恢复经 20 fold(归属转移),非本类私账。
            Assert.IsTrue(Projection().TryGet(seededId, out InventoryRow after));
            Assert.AreEqual(PlayerId, after.Owner, "fold 重建:实例归拾取者");
        }

        /// <summary>smoke #8 负例:**离屏拾取被拒(非崩溃)且零事件** —— 独立于正例
        /// (先断同型正例可过,再断离屏不可过)。</summary>
        [Test]
        public void test_smoke8_offRange_rejected_zeroEvents()
        {
            long seededId = SeedDrop(new WorldPos(9, 0, 9));   // Chebyshev 距离 9 ≫ 容差 1
            Assert.IsTrue(Projection().TryGet(seededId, out _), "[前置] 候选在场(负例非空洞)");
            int before = _bag.Stream.Events.Count;

            bool consumed = _claim.TryClaimInstance(PlayerId, Tick, new WorldPos(0, 0, 0), seededId);

            Assert.IsFalse(consumed, "离屏 ⇒ 拒(非崩溃 —— 返回 false)");
            Assert.AreEqual(before, _bag.Stream.Events.Count, "拒收零事件(意图未落地不落流)");
            Assert.AreEqual(InventoryProjection.NoOwner, Projection().TryGet(seededId, out InventoryRow r)
                ? r.Owner : -2, "候选仍在地上(归属未变)");
        }

        /// <summary>宽容半径边界:恰在半径上 = 允许;半径外一格 = 拒
        /// (半径 = 生产装配常量 PickToleranceCells = 1;改值须同批改本测)。</summary>
        [Test]
        public void test_smoke8_boundaryTolerance_atRadius_allowed_beyond_rejected()
        {
            long atRadius = SeedDrop(new WorldPos(1, 0, 0));    // Chebyshev 距离 1 == 容差
            long beyond = SeedDrop(new WorldPos(2, 0, 0));      // 距离 2 > 容差

            Assert.IsTrue(_claim.TryClaimInstance(PlayerId, Tick, new WorldPos(0, 0, 0), atRadius),
                "恰在半径上 = 允许(≤ 容差,含边界)");
            Assert.IsTrue(Projection().TryGet(atRadius, out InventoryRow first)
                && first.Owner == PlayerId, "半径上实例已 claim");

            // 🔁 2026-11-02(`TD-interaction-02`):结算**被指名**的实例 ⇒ 本段不再依赖
            //   「谁是第一个可拾」的扫描序 —— 半径外那一件被点名也照样拒。
            bool second = _claim.TryClaimInstance(PlayerId, Tick + 1, new WorldPos(0, 0, 0), beyond);
            Assert.IsFalse(second, "半径外一格 = 拒");
            Assert.AreEqual(InventoryProjection.NoOwner, Projection().TryGet(beyond, out InventoryRow b)
                ? b.Owner : -2, "半径外实例仍在地上");
        }

        /// <summary>幂等:同一实例二次 claim ⇒ 拒且零事件(归属面已变)。</summary>
        [Test]
        public void test_secondClaim_sameInstance_rejected_idempotent()
        {
            long id = SeedDrop(DropCell);
            Assert.IsTrue(_claim.TryClaimInstance(PlayerId, Tick, DropCell, id), "[前置] 首次可拾");
            int before = _bag.Stream.Events.Count;

            bool again = _claim.TryClaimInstance(PlayerId, Tick + 1, DropCell, id);

            Assert.IsFalse(again, "已归属 ⇒ 二次 claim 拒(幂等)");
            Assert.AreEqual(before, _bag.Stream.Events.Count, "二次拒零事件");
        }

        /// <summary>流里查无该实例的出生锚 ⇒ false 零事件(独立于正例的负例)。
        /// <para>🔁 2026-11-02(`TD-interaction-02`):旧名为 `test_noGroundCandidates_*` ——
        /// 当时「地面无候选」既可能是空场、也可能是 id 不匹配;现结算按 id 指名,判据收窄为
        /// 「这一件不存在」,语义更准(空场由 `TryClaimInstance` 的 `CanClaim` 同一个门覆盖)。</para></summary>
        [Test]
        public void test_unknownInstance_false_zeroEvents()
        {
            int before = _bag.Stream.Events.Count;

            const long UnknownInstance = 424242L;   // 流里从未出现的 id
            bool consumed = _claim.TryClaimInstance(PlayerId, Tick, DropCell, UnknownInstance);

            Assert.IsFalse(consumed, "查无出生锚 ⇒ false(不编造、不静默拾别件)");
            Assert.AreEqual(before, _bag.Stream.Events.Count, "零事件");
            Assert.AreEqual(0, CountOf(EventKind.DropClaimed));
        }

        // 🔁 2026-11-02(`TD-interaction-01`):原「E 沿路由」三条移到
        //   `Casebook/interaction_press_router_test.cs` —— 路由本体已从
        //   `InteractClaimRoute`(外挂比距)换成 `InteractionPressRouter`(一次 `Select` 定种类),
        //   本文件只剩 20 的拾取编排本体(下面这些)。

        // ══════════════════ 结构判据 / 参数守卫 ══════════════════

        /// <summary>零铸造 + 零直接 Append(源码扫描;claim 经 20 写者 —— 唯一入流通道)。</summary>
        [Test]
        public void test_claimOrchestrator_zeroMint_zeroDirectAppend()
        {
            string src = StripComments(File.ReadAllText(RepoFile(
                "unity/Assets/Sim/DropClaimOrchestrator.cs")));

            Assert.That(src.Contains("new ItemInstanceId("), Is.False,
                "零铸造(载荷 id = 投影行原样;AC-17-05 IL 门同口径)");
            Assert.That(src.Contains(".Append(") || src.Contains("IEventSink"), Is.False,
                "零直接 Append(claim 经 20 的 DropEventWriter —— 唯一入流通道)");
        }

        [Test]
        public void test_claimOrchestrator_invalidArgs_throw()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => _claim.TryClaimInstance(-1, Tick, DropCell, 1));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => _claim.TryClaimInstance(PlayerId, -1, DropCell, 1));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => _claim.TryClaimInstance(PlayerId, Tick, DropCell, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new DropClaimOrchestrator(() => _bag.Stream.Events, _bag.Decoder,
                    () => Projection(), _drops, tolerance: -1));
            Assert.Throws<ArgumentNullException>(() =>
                new DropClaimOrchestrator(null, _bag.Decoder, () => Projection(), _drops, 1));
            Assert.Throws<ArgumentNullException>(() =>
                new DropClaimOrchestrator(() => _bag.Stream.Events, _bag.Decoder, () => Projection(), null, 1));
        }
    }
}
