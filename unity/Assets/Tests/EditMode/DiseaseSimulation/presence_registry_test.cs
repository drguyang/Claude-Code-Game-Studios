// O-2 修复(2026-10-09)—— PresenceRegistry(IPresenceQuery 生产实装)测试。
//
// 验证:
//   - 读面(IsPresent / PresentCount / IsPresentAt)与写面(AddPresent / RemovePresent / MovePresent)闭环
//   - 幂等:重复进入不叠加;离开不存在者无副作用
//   - 同格多实体:一人离开不夺他人占格
//   - MovePresent 不隐式入场
//   - ResetForLoad 清空(读档重新灌入)

using System.Collections.Generic;
using DaYiJingCheng.Gameplay.PatientAI;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.DiseaseSimulation
{
    public class PresenceRegistryTest
    {
        private PresenceRegistry _registry;

        [SetUp]
        public void Setup() => _registry = new PresenceRegistry();

        [Test]
        public void test_o2_addPresent_readableViaQueryFace()
        {
            var p = new PatientId(3);
            var cell = new WorldPos(1, 0, 2);

            Assert.IsFalse(_registry.IsPresent(p), "初始不在场");

            _registry.AddPresent(p, cell);

            Assert.IsTrue(_registry.IsPresent(p), "AddPresent 后应在场");
            Assert.AreEqual(1, _registry.PresentCount);
            Assert.IsTrue(_registry.IsPresentAt(cell), "占格应可见");
        }

        [Test]
        public void test_o2_addPresent_idempotent()
        {
            var p = new PatientId(0);
            _registry.AddPresent(p, new WorldPos(0, 0, 0));
            _registry.AddPresent(p, new WorldPos(0, 0, 0)); // 重复

            Assert.AreEqual(1, _registry.PresentCount, "重复进入不叠加");
        }

        [Test]
        public void test_o2_removePresent_clearsPresenceAndCell()
        {
            var p = new PatientId(1);
            var cell = new WorldPos(5, 0, 5);
            _registry.AddPresent(p, cell);

            _registry.RemovePresent(p);

            Assert.IsFalse(_registry.IsPresent(p));
            Assert.AreEqual(0, _registry.PresentCount);
            Assert.IsFalse(_registry.IsPresentAt(cell), "离开后让格");
        }

        [Test]
        public void test_o2_removeAbsent_noSideEffect()
        {
            _registry.RemovePresent(new PatientId(99)); // 不存在
            Assert.AreEqual(0, _registry.PresentCount);
        }

        [Test]
        public void test_o2_sameCell_twoPatients_oneLeavesCellKept()
        {
            var a = new PatientId(1);
            var b = new PatientId(2);
            var cell = new WorldPos(3, 0, 3);
            _registry.AddPresent(a, cell);
            _registry.AddPresent(b, cell); // 同格

            _registry.RemovePresent(a);

            Assert.IsTrue(_registry.IsPresentAt(cell), "同格仍有他人 ⇒ 格不空");
            Assert.IsTrue(_registry.IsPresent(b));
        }

        [Test]
        public void test_o2_movePresent_updatesCells()
        {
            var p = new PatientId(4);
            var from = new WorldPos(0, 0, 0);
            var to = new WorldPos(1, 0, 0);
            _registry.AddPresent(p, from);

            _registry.MovePresent(p, to);

            Assert.IsFalse(_registry.IsPresentAt(from), "旧格让出");
            Assert.IsTrue(_registry.IsPresentAt(to), "新格占上");
            Assert.IsTrue(_registry.IsPresent(p), "在场不因跨格丢失");
        }

        [Test]
        public void test_o2_moveAbsent_doesNotImplicitlyEnter()
        {
            var p = new PatientId(7);
            _registry.MovePresent(p, new WorldPos(2, 0, 2)); // 不在场

            Assert.IsFalse(_registry.IsPresent(p), "Move 不得隐式入场");
            Assert.AreEqual(0, _registry.PresentCount);
        }

        // 评审 B1:from==to 短路 —— 独占格患者自移不得让出自格
        [Test]
        public void test_o2_movePresent_sameCell_keepsCellOccupied()
        {
            var p = new PatientId(4);
            var cell = new WorldPos(0, 0, 0);
            _registry.AddPresent(p, cell);

            _registry.MovePresent(p, cell); // from == to

            Assert.IsTrue(_registry.IsPresentAt(cell), "自格跨格不得让出独占格");
            Assert.IsTrue(_registry.IsPresent(p), "在场不丢");
        }

        // 评审 B2:Move 共享格守卫 —— A 移走不得夺 B 的同格
        [Test]
        public void test_o2_movePresent_sharedCell_coOccupantKeepsCell()
        {
            var a = new PatientId(1);
            var b = new PatientId(2);
            var x = new WorldPos(3, 0, 3);
            var y = new WorldPos(4, 0, 4);
            _registry.AddPresent(a, x);
            _registry.AddPresent(b, x); // 同格

            _registry.MovePresent(a, y);

            Assert.IsTrue(_registry.IsPresentAt(x), "同格仍有 B ⇒ A 移走后 X 不空");
            Assert.IsTrue(_registry.IsPresentAt(y), "A 在 Y 占上");
            Assert.IsTrue(_registry.IsPresent(a));
            Assert.IsTrue(_registry.IsPresent(b));
        }

        // 评审 BLOCKING #1 甲案:玩家 / 敌人占格不入在场集 ⇒ 不计 CAP
        [Test]
        public void test_o2_setOccupant_playerVisibleButNotCountedTowardCap()
        {
            const int playerId = 0; // 与病人共用实体 id 空间(ADR-016 §二)
            var cell = new WorldPos(0, 0, 0);

            _registry.SetOccupant(playerId, cell);

            Assert.IsTrue(_registry.IsPresentAt(cell), "玩家占格应对 PlaceableChecker 条件⑥ 可见");
            Assert.AreEqual(0, _registry.PresentCount, "玩家不计入 AC-15 CAP 分母");
            Assert.IsFalse(_registry.IsPresent(new PatientId(playerId)), "玩家不是在场病人");

            // 玩家在格上时,病人同格进入再离开 ⇒ 格仍被玩家占(占格集含非病人实体)
            _registry.AddPresent(new PatientId(50), cell);
            _registry.RemovePresent(new PatientId(50));
            Assert.IsTrue(_registry.IsPresentAt(cell), "病人离开后玩家仍占格");

            _registry.RemoveOccupant(playerId);
            Assert.IsFalse(_registry.IsPresentAt(cell), "玩家离格后让格");
            Assert.AreEqual(0, _registry.PresentCount, "玩家离格不影响 CAP 分母");
        }

        // 评审 A1:幽灵占格 —— A 移除后 B 进同格再移除 ⇒ 格必须释放
        // (若 _cellOf.Remove 被删,A 残留 ⇒ B 离开时 CellOccupiedByOther 误判有人 ⇒ 格永不释放)
        [Test]
        public void test_o2_removePresent_thenOtherEntersSameCell_cellReleases()
        {
            var a = new PatientId(1);
            var b = new PatientId(2);
            var cell = new WorldPos(6, 0, 6);
            _registry.AddPresent(a, cell);
            _registry.RemovePresent(a);

            _registry.AddPresent(b, cell);
            _registry.RemovePresent(b);

            Assert.IsFalse(_registry.IsPresentAt(cell), "最后一人离开后格应释放(无幽灵占格)");
        }

        // 评审 A2:重复 Add 异格 —— 保留旧格(幂等不改格),跨格须走 MovePresent
        [Test]
        public void test_o2_addPresent_duplicateDifferentCell_keepsOriginalCell()
        {
            var p = new PatientId(1);
            var oldCell = new WorldPos(0, 0, 0);
            var newCell = new WorldPos(9, 9, 9);
            _registry.AddPresent(p, oldCell);

            _registry.AddPresent(p, newCell); // 重复 Add(异格)

            Assert.AreEqual(1, _registry.PresentCount, "不叠加");
            Assert.IsTrue(_registry.IsPresentAt(oldCell), "重复 Add 保留旧格");
            Assert.IsFalse(_registry.IsPresentAt(newCell), "重复 Add 不占新格(跨格走 MovePresent)");
        }

        // 评审 ADVISORY #6:PresentChanged 通知面 —— 组合层同步 13 侧视图
        [Test]
        public void test_o2_presentChanged_firesOnAddAndRemoveOnly()
        {
            var seen = new List<int>();
            _registry.PresentChanged += id => seen.Add(id);

            _registry.AddPresent(new PatientId(1), new WorldPos(0, 0, 0));
            _registry.AddPresent(new PatientId(1), new WorldPos(0, 0, 0)); // 幂等重复 ⇒ 不触发
            _registry.MovePresent(new PatientId(1), new WorldPos(1, 0, 0)); // 跨格 ⇒ 不触发(在场集未变)
            _registry.RemovePresent(new PatientId(99));                    // 不存在 ⇒ 不触发
            _registry.RemovePresent(new PatientId(1));

            Assert.AreEqual(2, seen.Count, "仅进 / 出各触发一次");
            Assert.AreEqual(1, seen[0]);
            Assert.AreEqual(1, seen[1]);
        }

        [Test]
        public void test_o2_resetForLoad_clearsAll()
        {
            _registry.AddPresent(new PatientId(1), new WorldPos(0, 0, 0));
            _registry.AddPresent(new PatientId(2), new WorldPos(1, 0, 0));

            _registry.ResetForLoad();

            Assert.AreEqual(0, _registry.PresentCount);
            Assert.IsFalse(_registry.IsPresentAt(new WorldPos(0, 0, 0)), "占格全清");
        }

        // O-2 × O-1 × O-3 闭环:生产 IdAuthority + 真 EventStream + 登记簿灌满 CAP
        [Test]
        public void test_o2_o3_capFull_appendThrows_andIdRolledBack()
        {
            var authority = new DaYiJingCheng.Sim.IdAuthority();
            var stream = new DaYiJingCheng.Sim.EventStream(authority, _registry);
            var spawner = new DaYiJingCheng.Sim.DiseaseSimulation.PatientSpawner(
                authority, stream, NewEncoder(), worldSeed: 1UL);

            // 灌满 24 个在场病人(id 从 100 起,避开待发号 0 —— 否则新病人「已在场」跳过 CAP 检查)
            for (int i = 100; i < 100 + DaYiJingCheng.Sim.EventStream.PATIENT_APPEARANCE_CAP; i++)
                _registry.AddPresent(new PatientId(i), new WorldPos(i, 0, 0));

            // CAP 满 ⇒ SpawnNext 抛 AC-15,且 O-3 回滚:号不消耗
            Assert.Throws<System.InvalidOperationException>(
                () => spawner.SpawnNext(diseaseId: 1, tick: 0),
                "CAP 满应拒收新病人");

            // 让出一格 ⇒ 同一病人号可再发(证明回滚成功,无空洞)
            _registry.RemovePresent(new PatientId(100));
            var p = spawner.SpawnNext(diseaseId: 1, tick: 1);
            Assert.AreEqual(0, p.Value, "回滚后首个号应重发 0(无 ID 空洞)");
            Assert.AreEqual(1, stream.Count, "事件应写入");
        }

        private static DaYiJingCheng.Sim.Contracts.IPayloadEncoder NewEncoder()
        {
            var pool = new DaYiJingCheng.Sim.Codec.InMemoryBlobPool();
            return new DaYiJingCheng.Sim.Codec.PayloadEncoder(pool);
        }
    }
}
