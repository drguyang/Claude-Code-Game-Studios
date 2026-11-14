// 区域触发(N1)夹具。
//
// 权威:`design/levels/prologue-beat-script.md` §1(N1:「一张表 {cellRect, onEnter, once}
//       + 每 tick 比对玩家格;触发即回调」)· `RegionTriggerDriver` 类首的边界三条
//
// ⚠️ 本件钉的是**驱动语义**(进入边沿 / 一次性闩锁 / 此刻 vs 进过 / 闭区间 / 次序),
//    不是具体坐标 —— 坐标归 `PrologueAnchors`,改坐标不该让本件红。
//
// ⚠️ 断言一律用**字面量格**,不写 `RegionTrigger.Around(...)` —— 后者会与被测构造
//    一起漂移,构成「自指判据」(假绿形态 F3)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Boot
{
    /// <summary>区域触发驱动:边沿 / 闩锁 / 闭区间 / 校验。</summary>
    [TestFixture]
    public class region_trigger_test
    {
        // ── 1. 进入边沿与一次性闩锁 ────────────────────────────────────────

        /// <summary>进入报一次;待着不动不重复;离开报 exit;再进来不再报(Once)。</summary>
        [Test]
        public void test_enterOnce_firesOnce_staysEntered()
        {
            var enters = new List<string>();
            var exits = new List<string>();
            WorldPos cell = new WorldPos(0, 0, 0);

            var driver = new RegionTriggerDriver(
                new[] { RegionTrigger.Around("R", 10, 10, radius: 1) },
                () => cell,
                enters.Add, exits.Add);

            driver.OnTickEdge(1);                                  // 初始在外面
            Assert.That(enters, Is.Empty, "初始不在区域内不得报 onEnter");
            Assert.That(driver.HasEntered("R"), Is.False);

            cell = new WorldPos(10, 0, 10);                        // 进入
            driver.OnTickEdge(2);
            Assert.That(enters, Is.EqualTo(new[] { "R" }));

            driver.OnTickEdge(3);                                  // 待着不动
            Assert.That(enters.Count, Is.EqualTo(1), "电平不变不得重复报 onEnter");

            cell = new WorldPos(0, 0, 0);                          // 离开
            driver.OnTickEdge(4);
            Assert.That(exits, Is.EqualTo(new[] { "R" }));

            cell = new WorldPos(10, 0, 10);                        // 再进来
            driver.OnTickEdge(5);
            Assert.That(enters.Count, Is.EqualTo(1), "Once=true 时二次进入不得再报 onEnter");
            Assert.That(driver.HasEntered("R"), Is.True);
            Assert.That(driver.IsInside("R"), Is.True);
        }

        /// <summary>`Once=false` ⇒ 每次进入都报。</summary>
        [Test]
        public void test_onceFalse_firesOnEveryEntry()
        {
            var enters = new List<string>();
            WorldPos cell = new WorldPos(0, 0, 0);

            var driver = new RegionTriggerDriver(
                new[] { RegionTrigger.Around("R", 10, 10, radius: 1, once: false) },
                () => cell, enters.Add);

            cell = new WorldPos(10, 0, 10); driver.OnTickEdge(1);
            cell = new WorldPos(0, 0, 0);   driver.OnTickEdge(2);
            cell = new WorldPos(10, 0, 10); driver.OnTickEdge(3);

            Assert.That(enters, Is.EqualTo(new[] { "R", "R" }), "Once=false 时两次进入须各报一次");
        }

        // ── 2. 「此刻」vs「进过」—— 两者不可互相替代 ───────────────────────

        /// <summary>
        /// 离开后 `IsInside` 立刻假,而 `HasEntered` 恒真。
        /// <para>这条钉的是**语义分家**:节拍判定用前者(站着按 E),一次性演出用后者。</para>
        /// </summary>
        [Test]
        public void test_isInside_tracksNow_whileHasEntered_latches()
        {
            WorldPos cell = new WorldPos(10, 0, 10);
            var driver = new RegionTriggerDriver(
                new[] { RegionTrigger.Around("R", 10, 10, radius: 1) },
                () => cell);

            driver.OnTickEdge(1);
            Assert.That(driver.IsInside("R"), Is.True);
            Assert.That(driver.HasEntered("R"), Is.True);

            cell = new WorldPos(0, 0, 0);
            driver.OnTickEdge(2);
            Assert.That(driver.IsInside("R"), Is.False, "离开后「此刻」必须为假");
            Assert.That(driver.HasEntered("R"), Is.True, "「进过」只置不清");

            cell = new WorldPos(10, 0, 10);
            driver.OnTickEdge(3);
            Assert.That(driver.IsInside("R"), Is.True);
        }

        // ── 3. 闭区间 ──────────────────────────────────────────────────────

        /// <summary>矩形是**闭区间**:四个角都在内,越界一格就出去。</summary>
        [Test]
        public void test_bounds_areInclusive()
        {
            var t = new RegionTrigger("R", 4, 6, 20, 22);

            Assert.That(t.Contains(4, 20), Is.True, "左下角须在内(闭区间)");
            Assert.That(t.Contains(6, 22), Is.True, "右上角须在内(闭区间)");
            Assert.That(t.Contains(5, 21), Is.True);

            Assert.That(t.Contains(3, 20), Is.False, "X 下界外一格");
            Assert.That(t.Contains(7, 22), Is.False, "X 上界外一格");
            Assert.That(t.Contains(4, 19), Is.False, "Z 下界外一格");
            Assert.That(t.Contains(6, 23), Is.False, "Z 上界外一格");
        }

        /// <summary>`Around(radius: 0)` = 只有一个格(切比雪夫半径 0)。</summary>
        [Test]
        public void test_around_radiusZero_isSingleCell()
        {
            var t = RegionTrigger.Around("R", 7, 9, radius: 0);

            Assert.That(t.Contains(7, 9), Is.True);
            Assert.That(t.Contains(8, 9), Is.False);
            Assert.That(t.Contains(7, 10), Is.False);
            Assert.That(t.Contains(6, 8), Is.False);
        }

        /// <summary>半径 1 = 3×3(含本格与八邻格)。</summary>
        [Test]
        public void test_around_radiusOne_is3x3()
        {
            var t = RegionTrigger.Around("R", 7, 9, radius: 1);

            Assert.That(t.Contains(6, 8), Is.True);
            Assert.That(t.Contains(8, 10), Is.True);
            Assert.That(t.Contains(5, 9), Is.False);
            Assert.That(t.Contains(7, 11), Is.False);
        }

        // ── 4. 多区域:各自独立 + 同 tick 回调序 = 表序 ────────────────────

        /// <summary>两区重叠时同 tick 各报一次,次序 = 声明序(可复现)。</summary>
        [Test]
        public void test_overlappingRegions_fireIndependently_inDeclarationOrder()
        {
            var enters = new List<string>();
            var exits = new List<string>();
            WorldPos cell = new WorldPos(16, 0, 16);

            var a = new RegionTrigger("A", 10, 20, 10, 20);
            var b = new RegionTrigger("B", 15, 25, 15, 25);
            var driver = new RegionTriggerDriver(new[] { a, b }, () => cell, enters.Add, exits.Add);

            driver.OnTickEdge(1);
            Assert.That(enters, Is.EqualTo(new[] { "A", "B" }), "同 tick 内须按表序报");
            Assert.That(driver.InsideCount, Is.EqualTo(2));

            cell = new WorldPos(0, 0, 0);
            driver.OnTickEdge(2);
            Assert.That(exits, Is.EqualTo(new[] { "A", "B" }));
            Assert.That(driver.InsideCount, Is.EqualTo(0));

            // 只有 A 命中时,B 不受影响
            cell = new WorldPos(11, 0, 11);
            driver.OnTickEdge(3);
            Assert.That(driver.IsInside("A"), Is.True);
            Assert.That(driver.IsInside("B"), Is.False);
        }

        // ── 5. 查询面 ──────────────────────────────────────────────────────

        /// <summary>不存在的 id / 空 id ⇒ 两个查询都返回 false,不抛。</summary>
        [Test]
        public void test_unknownId_returnsFalse()
        {
            var driver = new RegionTriggerDriver(
                new[] { RegionTrigger.Around("R", 10, 10, radius: 1) },
                () => new WorldPos(10, 0, 10));

            driver.OnTickEdge(1);

            Assert.That(driver.IsInside("NOPE"), Is.False);
            Assert.That(driver.HasEntered("NOPE"), Is.False);
            Assert.That(driver.IsInside(null), Is.False);
            Assert.That(driver.HasEntered(""), Is.False);
        }

        /// <summary>诊断读数随 tick 更新;`LastTick` 未跑过 = -1。</summary>
        [Test]
        public void test_diagnostics_trackState()
        {
            WorldPos cell = new WorldPos(0, 0, 0);
            var driver = new RegionTriggerDriver(
                new[] { RegionTrigger.Around("R", 10, 10, radius: 1) },
                () => cell);

            Assert.That(driver.LastTick, Is.EqualTo(-1L), "未跑过时 LastTick 须为 -1");
            Assert.That(driver.Triggers.Count, Is.EqualTo(1));

            driver.OnTickEdge(42);
            Assert.That(driver.LastTick, Is.EqualTo(42L));
            Assert.That(driver.Summary, Does.Contain("@tick=42"));
        }

        // ── 6. 构造 / 推进面的 fail-closed ─────────────────────────────────

        /// <summary>空 id、区间反向、负半径一律构造期拒收。</summary>
        [Test]
        public void test_ctor_rejectsInvalidTrigger()
        {
            Assert.Throws<ArgumentException>(() => new RegionTrigger("", 0, 1, 0, 1), "空 id 须拒");
            Assert.Throws<ArgumentException>(() => new RegionTrigger(null, 0, 1, 0, 1), "null id 须拒");
            Assert.Throws<ArgumentException>(() => new RegionTrigger("R", 2, 1, 0, 1), "X0 > X1 须拒");
            Assert.Throws<ArgumentException>(() => new RegionTrigger("R", 0, 1, 2, 1), "Z0 > Z1 须拒");
            Assert.Throws<ArgumentOutOfRangeException>(() => RegionTrigger.Around("R", 0, 0, radius: -1),
                "负半径须拒");
        }

        /// <summary>重复 id 构造期拒收(否则回调只报 id,两条互相遮蔽)。</summary>
        [Test]
        public void test_ctor_rejectsDuplicateIds()
        {
            var ex = Assert.Throws<ArgumentException>(() => new RegionTriggerDriver(
                new[] { RegionTrigger.Around("R", 1, 1), RegionTrigger.Around("R", 5, 5) },
                () => default));

            StringAssert.Contains("重复", ex.Message);
        }

        /// <summary>空表 / 空读取器拒收。</summary>
        [Test]
        public void test_ctor_rejectsNullArguments()
        {
            Assert.Throws<ArgumentNullException>(() => new RegionTriggerDriver(null, () => default));
            Assert.Throws<ArgumentNullException>(() => new RegionTriggerDriver(new RegionTrigger[0], null));
        }

        /// <summary>区域表在构造期**复制** —— 调用方事后改数组不影响驱动。</summary>
        [Test]
        public void test_ctor_clonesTable()
        {
            var source = new[] { RegionTrigger.Around("A", 1, 1) };
            var driver = new RegionTriggerDriver(source, () => default);

            source[0] = RegionTrigger.Around("B", 1, 1);

            Assert.That(driver.Triggers[0].Id, Is.EqualTo("A"), "驱动须持有构造时的副本");
        }

        /// <summary>负 tick 拒收;同 tick 重复调用幂等。</summary>
        [Test]
        public void test_tick_validation_andIdempotence()
        {
            var enters = new List<string>();
            WorldPos cell = new WorldPos(10, 0, 10);
            var driver = new RegionTriggerDriver(
                new[] { RegionTrigger.Around("R", 10, 10, radius: 1, once: false) },
                () => cell, enters.Add);

            Assert.Throws<ArgumentOutOfRangeException>(() => driver.OnTickEdge(-1), "负 tick 须拒");

            driver.OnTickEdge(5);
            Assert.That(enters.Count, Is.EqualTo(1));

            // 同 tick 重复调用:即使玩家格变了也不得二次推进(否则「离开再进入」会被误读)
            cell = new WorldPos(0, 0, 0);
            driver.OnTickEdge(5);
            Assert.That(driver.IsInside("R"), Is.True, "同 tick 重复调用不得推进状态");
        }

        /// <summary>
        /// tick 变小 = **读档**:不抛;成员集按新位置重算;一次性闩锁保留(读档不得重播)。
        /// <para>⚠️ 这条的判据来自**实测**而非推断:2026-11-14 全量 PlayMode 里
        /// `adr041_bak_rescue_walkthrough` 读档后 `tick 70 → 21`,首版本驱动对此抛
        /// 「时间倒流」⇒ 该用例当场红。故本类**刻意不**照抄 sim 侧
        /// `DiseaseVitalsService` 的「倒流即抛」:本驱动不持权威态。</para>
        /// </summary>
        [Test]
        public void test_tickRollback_rebases_keepsOnceLatch()
        {
            var enters = new List<string>();
            var exits = new List<string>();
            WorldPos cell = new WorldPos(10, 0, 10);

            var driver = new RegionTriggerDriver(
                new[] { RegionTrigger.Around("R", 10, 10, radius: 1) },     // Once = true
                () => cell, enters.Add, exits.Add);

            driver.OnTickEdge(70);
            Assert.That(enters.Count, Is.EqualTo(1));

            // 读档:玩家被搬到区域外,tick 拨回 21
            cell = new WorldPos(0, 0, 0);
            Assert.DoesNotThrow(() => driver.OnTickEdge(21), "tick 变小是读档,不是错误");
            Assert.That(driver.IsInside("R"), Is.False, "读档后成员集须按新位置重算");
            Assert.That(exits, Is.Empty, "读档不得产生伪 onExit(旧成员集已作废)");
            Assert.That(driver.HasEntered("R"), Is.True, "一次性闩锁跨读档保留");

            // 读档后再走进去:Once ⇒ 不再重播
            cell = new WorldPos(10, 0, 10);
            driver.OnTickEdge(22);
            Assert.That(enters.Count, Is.EqualTo(1), "读档后不得重播一次性演出");
            Assert.That(driver.IsInside("R"), Is.True);
        }

        /// <summary>回调为 null 时正常推进(静默),不抛。</summary>
        [Test]
        public void test_nullCallbacks_areAllowed()
        {
            WorldPos cell = new WorldPos(0, 0, 0);
            var driver = new RegionTriggerDriver(
                new[] { RegionTrigger.Around("R", 10, 10, radius: 1) },
                () => cell);

            Assert.DoesNotThrow(() => driver.OnTickEdge(1));
            cell = new WorldPos(10, 0, 10);
            Assert.DoesNotThrow(() => driver.OnTickEdge(2));
            cell = new WorldPos(0, 0, 0);
            Assert.DoesNotThrow(() => driver.OnTickEdge(3));

            Assert.That(driver.HasEntered("R"), Is.True, "无回调时状态仍须推进");
        }

        // ── 7. 与锚点表同源(单一出处)────────────────────────────────────

        /// <summary>
        /// `PrologueAnchor.ToRegion()` 与 `PrologueAnchor.IsNear()` 逐格同判。
        /// <para>⚠️ 钉的是**同源**,不是坐标值 —— 两边都读同一个 `CellX/CellZ`,
        /// 哪天真分叉了(有人给区域表写死一份坐标)本件就红。</para>
        /// </summary>
        [Test]
        public void test_prologueAnchors_toRegion_matchesIsNear()
        {
            Assert.That(PrologueAnchors.FirstAct.Length, Is.EqualTo(3), "第一段三件实物(改动即须同步本件)");

            foreach (PrologueAnchor a in PrologueAnchors.FirstAct)
            {
                RegionTrigger r = a.ToRegion();
                Assert.That(r.Id, Is.EqualTo(a.BeatId), "区域 id 须 = 节拍 id");

                for (int x = a.CellX - 3; x <= a.CellX + 3; x++)
                {
                    for (int z = a.CellZ - 3; z <= a.CellZ + 3; z++)
                    {
                        Assert.That(r.Contains(x, z), Is.EqualTo(a.IsNear(x, z)),
                            a.BeatId + " 在 (" + x + "," + z + ") 的两条判定分叉了");
                    }
                }
            }
        }
    }
}
