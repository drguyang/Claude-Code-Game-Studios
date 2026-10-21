// 权威来源:GDD design/gdd/clinic-machine.md 公共定义 / F-24-1 / F-24-2
//          · `Sim/Clinic/ClinicModifierEvaluator.cs`(被测物)
//          · story-002 `ClinicRoomExtractionResult`(输入形)· story-001 `ClinicAdjTable`(表)
//
// ⚠️ **命名限定**(卡面 QA plan 回填 §5):系统 5 的 `EnvMod_raw` 与本故事 F-24-1 同名
//    ⇒ 本文件全部用 `Clinic*` 前缀,`grep -rn 'EnvMod'` 不得串到系统 5。
//
// ⚠️ 数值一律**符号化小值**(卡面 §Implementation Notes 5:逐值归用户数值轮)。
//    唯一例外 = GDD 药房算例(`env_score = 28000`),它是**权威算例**,作黄金夹具。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Clinic;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Unit.ClinicMachine
{
    /// <summary>
    /// 24 乘子求值(F-24-1 EnvMod / F-24-2 EquipMod / adj_sum)的判据。
    /// <para>⚠️ 表对称性**不在本件测面**(由烘焙期 schema 断言保证,归 clinic-001);
    /// 本件只测「24 忠实使用表值 + 该钳的钳、不该钳的不钳」。</para>
    /// </summary>
    [TestFixture]
    public class ClinicModifierEvalTest
    {
        // ══════════════════ 夹具 ══════════════════

        private sealed class FakeView : IPlacedStructureReadView
        {
            private readonly WorldPos[][] _instances;

            public FakeView(params WorldPos[] cells)
            {
                _instances = new WorldPos[cells.Length][];
                for (int i = 0; i < cells.Length; i++) _instances[i] = new[] { cells[i] };
            }

            public int StructureCount => _instances.Length;
            public IReadOnlyList<WorldPos> GetOccupiedCells(int structureIndex) => _instances[structureIndex];
        }

        private sealed class FakeTraits : IClinicFurnitureTraitsSource
        {
            private readonly Dictionary<WorldPos, ClinicFurnitureTraits> _byCell;
            private readonly HashSet<WorldPos> _nonFurniture;
            private readonly HashSet<WorldPos> _unknown;

            public FakeTraits(
                IEnumerable<(WorldPos Cell, string Type, int Tier, long EEnvRaw)> furniture,
                IEnumerable<WorldPos> nonFurniture = null,
                IEnumerable<WorldPos> unknown = null)
            {
                _byCell = new Dictionary<WorldPos, ClinicFurnitureTraits>();
                foreach ((WorldPos cell, string type, int tier, long eEnvRaw) in furniture)
                    _byCell[cell] = new ClinicFurnitureTraits(type, tier, new Fix(eEnvRaw));

                _nonFurniture = new HashSet<WorldPos>(nonFurniture ?? Array.Empty<WorldPos>());
                _unknown = new HashSet<WorldPos>(unknown ?? Array.Empty<WorldPos>());
            }

            public ClinicCellLookup Lookup(WorldPos cell, out ClinicFurnitureTraits traits)
            {
                if (_unknown.Contains(cell)) { traits = default; return ClinicCellLookup.Unknown; }
                if (_nonFurniture.Contains(cell)) { traits = default; return ClinicCellLookup.NonFurniture; }
                if (_byCell.TryGetValue(cell, out traits)) return ClinicCellLookup.Furniture;
                traits = default;
                return ClinicCellLookup.Unknown;
            }
        }

        private static readonly WorldPos C0 = new WorldPos(0, 0, 0);
        private static readonly WorldPos C1 = new WorldPos(1, 0, 0);
        private static readonly WorldPos C2 = new WorldPos(2, 0, 0);
        private static readonly WorldPos C3 = new WorldPos(3, 0, 0);

        private static ClinicRoomExtractionResult Room(params WorldPos[] cells)
            => ClinicRoomExtraction.Extract(new FakeView(cells));

        /// <summary>建 ADJ_TABLE:每条按**双向**写入(表对称性由烘焙门保证,夹具照抄该形状)。
        /// <para>⚠️ 自对 `a == b` 只写一条 —— 它的"镜像"就是它自己(烘焙侧同样只有一行;
        /// 写两条会命中「重复有向键」判据)。</para></summary>
        private static ClinicAdjTable AdjTable(long wMinRaw, long wMaxRaw, long nMax,
            params (string A, string B, long W)[] pairs)
        {
            var list = new List<ClinicAdjPair>();
            foreach ((string a, string b, long w) in pairs)
            {
                list.Add(new ClinicAdjPair(a, b, new Fix(w)));
                if (!string.Equals(a, b, StringComparison.Ordinal))
                    list.Add(new ClinicAdjPair(b, a, new Fix(w)));
            }
            return new ClinicAdjTable(list, nMax, new Fix(wMinRaw), new Fix(wMaxRaw));
        }

        /// <summary>无相关对的表:构造器要求 ≥ 1 条(F-24-1「空表 = 整表静默失效」),
        /// 故放一条与夹具类型无关、权重 0 的对。</summary>
        private static ClinicAdjTable NoPairsTable()
            => AdjTable(-1, 1, 64, ("__unused_a", "__unused_b", 0L));

        // ══════════════════ ① GDD 黄金算例(F-24-1)══════════════════

        /// <summary>药房 4 格算例:药柜 +12000 / 研磨台 +8000 / 烘干架 +6000 / 便器 −20000;
        /// 邻接 研磨台↔烘干架 +4000、药柜↔便器 −12000;base = 30000 ⇒ **env_score = 28000**。</summary>
        /// <remarks><b>判别力</b>:漏算 e_env / 漏算 adj / 把边计两次(14000 而非 −8000)/
        /// 把 base_env 也进邻接 —— 任一错法都给出不同的 raw。</remarks>
        [Test]
        public void test_clinic_envMod_gddWorkedExample_matches28000()
        {
            // 排布:药柜 − 便器 − 研磨台 − 烘干架(相邻对恰为算例指定的两对)
            ClinicRoomExtractionResult room = Room(C0, C1, C2, C3);
            var traits = new FakeTraits(new[]
            {
                (C0, "herb_cabinet", 0, 12000L),
                (C1, "chamber_pot", 0, -20000L),
                (C2, "grinder", 0, 8000L),
                (C3, "drying_rack", 0, 6000L),
            });
            ClinicAdjTable adj = AdjTable(-30000, 30000, 64,
                ("herb_cabinet", "chamber_pot", -12000L),
                ("grinder", "drying_rack", 4000L));

            Fix env = ClinicModifierEvaluator.ClinicEnvModOf(room, traits, adj, new Fix(30000));

            Assert.That(env.Raw, Is.EqualTo(28000), "F-24-1 药房算例:GDD 期望 env_score = 28000");
        }

        // ══════════════════ ② AC-24-01b:env 侧零钳制 ══════════════════

        /// <summary>和超出 ENV_MOD_MAX 也**原样透传**(24 不 clamp;唯一钳制点在 21a F1)。</summary>
        /// <remarks><b>判别力</b>:在 24 侧加任何 clamp / 饱和 ⇒ 返回值被压回域内 ⇒ 红。</remarks>
        [Test]
        public void test_clinic_envMod_aboveEnvModMax_passesThroughUnclamped()
        {
            ClinicRoomExtractionResult room = Room(C0);
            var traits = new FakeTraits(new[] { (C0, "polluter", 0, 900000L) });

            Fix env = ClinicModifierEvaluator.ClinicEnvModOf(
                room, traits, NoPairsTable(), new Fix(500000));

            Assert.That(env.Raw, Is.EqualTo(1400000L),
                "越界求和必须原样出(若被 clamp 到 ENV_MOD_MAX 则不等)");
        }

        /// <summary>全负和同样原样透传(ENV_MOD_MIN 的承重前提:钳到 0 会与 ROOM_NONE 中性值撞车)。</summary>
        [Test]
        public void test_clinic_envMod_allNegative_passesThroughUnclamped()
        {
            ClinicRoomExtractionResult room = Room(C0, C1);
            var traits = new FakeTraits(new[]
            {
                (C0, "polluter", 0, -900000L),
                (C1, "polluter", 0, -900000L),
            });

            Fix env = ClinicModifierEvaluator.ClinicEnvModOf(
                room, traits, NoPairsTable(), new Fix(-300000));

            Assert.That(env.Raw, Is.EqualTo(-2100000L), "全负和必须原样出(禁饱和到 ENV_MOD_MIN)");
        }

        // ══════════════════ ③ ROOM_NONE ⇒ 中性 (0, 0)══════════════════

        /// <summary>EC-24-01 / EC-24-02 / EC-24-07:未析出房间 ⇒ 三个函数全部返回 0
        /// (**含 env**,不返回 base_env)。</summary>
        [Test]
        public void test_clinic_roomNone_returnsNeutralZero()
        {
            ClinicRoomExtractionResult none = Room();               // 空布局 ⇒ ROOM_NONE
            Assert.That(none.HasRoom, Is.False);

            var traits = new FakeTraits(Array.Empty<(WorldPos, string, int, long)>());
            ClinicAdjTable adj = NoPairsTable();

            Assert.That(ClinicModifierEvaluator.ClinicEnvModOf(none, traits, adj, new Fix(50000)).Raw,
                Is.EqualTo(0), "ROOM_NONE 的 env 必须是 0,不是 base_env");
            Assert.That(ClinicModifierEvaluator.ClinicEquipModOf(none, traits).Raw, Is.EqualTo(0));
            Assert.That(ClinicModifierEvaluator.ClinicAdjSum(none, traits, adj).Raw, Is.EqualTo(0));
        }

        /// <summary>EC-24-07:≥ 2 连通簇 ⇒ **整盘** ROOM_NONE ⇒ 三函数全 0(不取最大簇、不逐块求和)。</summary>
        /// <remarks><b>判别力</b>:若析出器改成「取最大簇」或求值器自己拼格集,本测的 0 会变成非 0。</remarks>
        [Test]
        public void test_clinic_twoClusters_returnsNeutralZero()
        {
            ClinicRoomExtractionResult split = Room(C0, C2);         // 不相邻 ⇒ 两个簇
            Assert.That(split.HasRoom, Is.False, "两簇布局须整盘 ROOM_NONE");
            Assert.That(split.ConnectedClusterCount, Is.EqualTo(2));

            var traits = new FakeTraits(new[] { (C0, "bed", 5, 900L), (C2, "bed", 5, 900L) });
            ClinicAdjTable adj = AdjTable(-30000, 30000, 64, ("bed", "bed", 7000L));

            Assert.That(ClinicModifierEvaluator.ClinicEnvModOf(split, traits, adj, new Fix(1234)).Raw,
                Is.EqualTo(0), "部分生效 = 跨房间项,必须整盘中性");
            Assert.That(ClinicModifierEvaluator.ClinicEquipModOf(split, traits).Raw, Is.EqualTo(0));
            Assert.That(ClinicModifierEvaluator.ClinicAdjSum(split, traits, adj).Raw, Is.EqualTo(0));
        }

        // ══════════════════ ④ adj_sum 边界 ══════════════════

        /// <summary>n = 1 ⇒ 无邻居 ⇒ adj_sum = 0(QA 回填 §5)。</summary>
        [Test]
        public void test_clinic_adjSum_singleCell_isZero()
        {
            ClinicRoomExtractionResult room = Room(C0);
            var traits = new FakeTraits(new[] { (C0, "bed", 0, 0L) });

            Fix adj = ClinicModifierEvaluator.ClinicAdjSum(room, traits, NoPairsTable());

            Assert.That(adj.Raw, Is.EqualTo(0));
        }

        /// <summary>一对相邻格:边**恰计一次**(只查 +x/+y ⇒ 不许 A→B 与 B→A 各计一遍)。</summary>
        /// <remarks><b>判别力</b>:若实现遍历四邻域,同一对会被计两次 ⇒ 10000 而非 5000。</remarks>
        [Test]
        public void test_clinic_adjSum_adjacentPair_countedOnce()
        {
            ClinicRoomExtractionResult room = Room(C0, C1);
            var traits = new FakeTraits(new[]
            {
                (C0, "bed", 0, 0L),
                (C1, "shelf", 0, 0L),
            });
            ClinicAdjTable adj = AdjTable(-30000, 30000, 64, ("bed", "shelf", 5000L));

            Fix sum = ClinicModifierEvaluator.ClinicAdjSum(room, traits, adj);

            Assert.That(sum.Raw, Is.EqualTo(5000), "每对相邻格恰计一次");
        }

        /// <summary>ADJ_TABLE 缺该对 ⇒ 权重 0(F-24-1「缺失 = 0」,不是抛)。</summary>
        [Test]
        public void test_clinic_adjSum_missingPair_isZero()
        {
            ClinicRoomExtractionResult room = Room(C0, C1);
            var traits = new FakeTraits(new[]
            {
                (C0, "bed", 0, 0L),
                (C1, "shelf", 0, 0L),
            });
            ClinicAdjTable adj = AdjTable(-30000, 30000, 64, ("bed", "table", 5000L));

            Assert.That(ClinicModifierEvaluator.ClinicAdjSum(room, traits, adj).Raw, Is.EqualTo(0));
        }

        /// <summary>非家具格(外壳 / 地板)不进 S 的求和面:既不计 e_env,也不参与邻接。</summary>
        /// <remarks><b>判别力</b>:若把 NonFurniture 当 Furniture 处理(或反之抛),本测红。
        /// 注意 S 由 23 的**代理判据**(占用格)析出 ⇒ 夹具里含非家具格正是真实的过渡形态。</remarks>
        [Test]
        public void test_clinic_adjSum_nonFurnitureCellExcluded()
        {
            ClinicRoomExtractionResult room = Room(C0, C1, C2);      // 三格连通
            var traits = new FakeTraits(
                new[]
                {
                    (C0, "bed", 0, 100L),
                    (C2, "bed", 0, 100L),
                },
                nonFurniture: new[] { C1 });
            // 若无中间格,C0 与 C1 / C1 与 C2 的边都不该算;bed-bed 权重即便存在也不得计。
            ClinicAdjTable adj = AdjTable(-30000, 30000, 64, ("bed", "bed", 7000L));

            Assert.That(ClinicModifierEvaluator.ClinicAdjSum(room, traits, adj).Raw, Is.EqualTo(0),
                "非家具格断开邻接链");
        }

        // ══════════════════ ⑤ F-24-2 EquipMod ══════════════════

        /// <summary>全 tier = 0 ⇒ 0(tier 0 = 无档,不计入)。</summary>
        [Test]
        public void test_clinic_equipMod_allZeroTier_returnsZero()
        {
            ClinicRoomExtractionResult room = Room(C0, C1);
            var traits = new FakeTraits(new[]
            {
                (C0, "bed", 0, 0L),
                (C1, "shelf", 0, 0L),
            });

            Assert.That(ClinicModifierEvaluator.ClinicEquipModOf(room, traits).Raw, Is.EqualTo(0));
        }

        /// <summary>档位和 = 4 ⇒ `4 × 65536 = 262144` &gt; CAP ⇒ 钉 `EQUIP_MOD_CAP`(16384)。
        /// <para>GDD 算例里 `4 × 65536 = 262144` 是**乘法示范**,它在 CAP = 1/4 下必然被钳 ——
        /// 本测钉的是「钳后值」,不把算例的中间量当输出。</para></summary>
        [Test]
        public void test_clinic_equipMod_sumAboveCap_pinsToCap()
        {
            ClinicRoomExtractionResult room = Room(C0, C1, C2, C3);
            var traits = new FakeTraits(new[]
            {
                (C0, "a", 2, 0L),
                (C1, "b", 1, 0L),
                (C2, "c", 1, 0L),
                (C3, "d", 0, 0L),           // tier 0 不进和
            });

            Fix equip = ClinicModifierEvaluator.ClinicEquipModOf(room, traits);

            Assert.That(equip.Raw, Is.EqualTo(ClinicModifierEvaluator.EquipModCapRaw));
            Assert.That(ClinicModifierEvaluator.EquipModCapRaw, Is.EqualTo(16384), "CAP = 1/4 ⇒ raw 16384");
        }

        /// <summary>`CAP &lt; 1` 的二值语义:score ≥ 1 一律钉顶,**不存在 1 与 CAP 之间的中间值**。</summary>
        /// <remarks><b>判别力</b>:若实现写成 `clamp(score, 0, CAP)`(漏了 ×65536)或做了归一化
        /// (`score/n`),score = 1..6 会给出不同值 ⇒ 红。</remarks>
        [Test]
        public void test_clinic_equipMod_positiveScores_haveNoIntermediateValue()
        {
            for (int score = 1; score <= 6; score++)
            {
                var cells = new List<WorldPos>();
                var rows = new List<(WorldPos, string, int, long)>();
                for (int i = 0; i < score; i++)
                {
                    var cell = new WorldPos(i, 0, 0);
                    cells.Add(cell);
                    rows.Add((cell, "t", 1, 0L));
                }

                ClinicRoomExtractionResult room = Room(cells.ToArray());
                var traits = new FakeTraits(rows);
                Fix equip = ClinicModifierEvaluator.ClinicEquipModOf(room, traits);

                Assert.That(equip.Raw, Is.EqualTo(ClinicModifierEvaluator.EquipModCapRaw),
                    $"score = {score} 必须钉顶(CAP < 1 的二值语义)");
            }
        }

        /// <summary>`equip_score × 65536` 在 **int64** 域求积:大档位和仍钳顶,**不回绕成负数**。</summary>
        /// <remarks><b>判别力</b>:若乘积写在 int32(`unchecked`)里,`100000 × 65536` 回绕成负数
        /// ⇒ 下界钳把结果压成 0 ⇒ 红。
        /// <para>⚠️ <b>本夹具刻意越域</b>:`tier = 100000` 远超冻结的 `TIER_MAX = 3`。**域内**的最大档位和
        /// 是 `n_max(64) × TIER_MAX(3) = 192`,`192 × 65536 = 12 582 912` **int32 装得下** ⇒ 在域内
        /// int32 / int64 两种实现在本夹具上**不可区分**。故本条证明的是「实现选用了 int64」这一**实现要求**
        /// (卡面 AC 第 6 条),**不是**「域内值会溢出」—— 后者为假,不主张。域界本身由 story-001 的
        /// schema 断言(`n_max × TIER_MAX < 2^31` 等)守,本件不重复测。</para></remarks>
        [Test]
        public void test_clinic_equipMod_largeScore_int64NoWrap()
        {
            ClinicRoomExtractionResult room = Room(C0);
            var traits = new FakeTraits(new[] { (C0, "t", 100000, 0L) });

            Fix equip = ClinicModifierEvaluator.ClinicEquipModOf(room, traits);

            Assert.That(equip.Raw, Is.EqualTo(ClinicModifierEvaluator.EquipModCapRaw),
                "100000 × 65536 须在 int64 内算,再钳顶(回绕会给出 0)");
        }

        /// <summary>域边界 fixture:`n = 64`(OQ-24-5 已裁 `n_max = 64`)× 大档位和 ⇒ 仍在 int64 内无环绕。</summary>
        /// <remarks><b>判别力</b>:`64 × 1000 = 64000`,`64000 × 65536 ≈ 4.19e9` &gt; int32 上界
        /// (2.1e9)⇒ 若乘积落在 int32 已回绕;int64 实现给出钳顶值。
        /// <para>⚠️ 同 `largeScore`:格数 `64` **在域内**(= `n_max`),但每格 `tier = 1000` **越域**
        /// (`TIER_MAX = 3`)。域内上界 `64 × 3 × 65536 = 12 582 912` 在 int32 内,故「域内会不会溢出」
        /// 不是本条的命题;本条只钉「实现走 int64」。</para></remarks>
        [Test]
        public void test_clinic_equipMod_nMaxBoundary_noWrap()
        {
            var cells = new List<WorldPos>();
            var rows = new List<(WorldPos, string, int, long)>();
            for (int i = 0; i < 64; i++)                       // 4-连通链 ⇒ 恰 1 簇
            {
                var cell = new WorldPos(i, 0, 0);
                cells.Add(cell);
                rows.Add((cell, "tiered", 1000, 0L));
            }

            ClinicRoomExtractionResult room = Room(cells.ToArray());
            Assert.That(room.CellCount, Is.EqualTo(64), "夹具须恰 64 格(n_max 边界)");
            Assert.That(room.HasRoom, Is.True);

            Fix equip = ClinicModifierEvaluator.ClinicEquipModOf(room, new FakeTraits(rows));

            Assert.That(equip.Raw, Is.EqualTo(ClinicModifierEvaluator.EquipModCapRaw));
        }

        // ══════════════════ ⑥ 硬失败面 ══════════════════

        /// <summary>属性源返回 Unknown ⇒ 硬失败(禁静默跳过 —— 跳过会让 e_env / 档位静默少算)。</summary>
        [Test]
        public void test_clinic_traitsUnknown_throwsInsteadOfSkipping()
        {
            ClinicRoomExtractionResult room = Room(C0);
            var traits = new FakeTraits(Array.Empty<(WorldPos, string, int, long)>());

            var ex = Assert.Throws<InvalidOperationException>(
                () => ClinicModifierEvaluator.ClinicEnvModOf(room, traits, NoPairsTable(), Fix.Zero));
            Assert.That(ex.Message, Does.Contain("Unknown"));
        }

        /// <summary>`|S| &gt; n_max` ⇒ 拒收(越出 F-24-1 的 int64 域前置;OQ-24-5 = 64)。</summary>
        [Test]
        public void test_clinic_roomExceedsNMax_throws()
        {
            ClinicRoomExtractionResult room = Room(C0, C1, C2);
            var traits = new FakeTraits(new[]
            {
                (C0, "a", 0, 0L), (C1, "b", 0, 0L), (C2, "c", 0, 0L),
            });
            ClinicAdjTable tiny = AdjTable(-1, 1, 2, ("a", "b", 0L));

            Assert.Throws<ArgumentException>(
                () => ClinicModifierEvaluator.ClinicAdjSum(room, traits, tiny));
        }

        // ══════════════════ ⑦ 确定性 + 缺口哨兵 ══════════════════

        /// <summary>同一输入两次求值 ⇒ 逐位相同的 raw(纯函数,无隐藏状态)。</summary>
        [Test]
        public void test_clinic_evaluator_sameInput_isBitIdenticalTwice()
        {
            ClinicRoomExtractionResult room = Room(C0, C1);
            var traits = new FakeTraits(new[]
            {
                (C0, "bed", 3, 1200L),
                (C1, "shelf", 1, -700L),
            });
            ClinicAdjTable adj = AdjTable(-30000, 30000, 64, ("bed", "shelf", 900L));

            long a = ClinicModifierEvaluator.ClinicEnvModOf(room, traits, adj, new Fix(100)).Raw;
            long b = ClinicModifierEvaluator.ClinicEnvModOf(room, traits, adj, new Fix(100)).Raw;
            long ea = ClinicModifierEvaluator.ClinicEquipModOf(room, traits).Raw;
            long eb = ClinicModifierEvaluator.ClinicEquipModOf(room, traits).Raw;

            Assert.That(a, Is.EqualTo(b));
            Assert.That(ea, Is.EqualTo(eb));
        }

        /// <summary>缺口哨兵:`FURN_TABLE` 的生产实现仍未兑现(`O-24-1` / `TD-clinic-01`)。
        /// <para>⚠️ 哨兵转红 = **被要求的行为**(兑现时须同时改判据),不得当回归修掉。</para></summary>
        [Test]
        public void test_clinic_furnTraitsProductionReady_sentinelFalse()
        {
            Assert.That(ClinicModifierEvaluator.FurnTraitsProductionReady, Is.False,
                "O-24-1(FURN_TABLE)已兑现 ⇒ 本哨兵须转 true 并实现生产属性源;在此之前保持 false");
        }
    }
}
