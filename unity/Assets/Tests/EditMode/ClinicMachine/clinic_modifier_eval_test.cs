// 权威来源:GDD design/gdd/clinic-machine.md 公共定义 / F-24-1 / F-24-2
//          · `Sim/Clinic/ClinicModifierEvaluator.cs`(被测物)
//          · story-002 `ClinicRoomExtractionResult`(输入形)· story-001 `ClinicAdjTable`(表)
//
// ⚠️ **命名限定**(卡面 QA plan 回填 §5):系统 5 的 `EnvMod_raw` 与本故事 F-24-1 同名
//    ⇒ 本文件全部用 `Clinic*` 前缀,`grep -rn 'EnvMod'` 不得串到系统 5。
//
// ⚠️ 数值一律**符号化小值**(卡面 §Implementation Notes 5:逐值归用户数值轮)。
//    唯一例外 = GDD 药房算例(`env_score = 28000`),它是**权威算例**,作黄金夹具。
//
// 🔁 **第二条例外(2026-10-21 性质测试补写轮)**:⑦ 段的性质测**刻意取大值**(±400000 / ±100000)。
//    理由与「符号化小值」不冲突 —— 那条约的是**定点例子**的取值纪律(避免把调参值当判据);
//    性质测要判的是「有没有 clamp」,样本必须**打到域外**才可观测,小值会让测变假绿
//    (实测踩过:见 ⑦ 段开头的缺口登记)。数值仍未定稿,这里的大值是**判别力需要**,不是调参取值。

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
            /// <summary>无模块标识 —— 本夹具只喂代理判据析出。</summary>
            public string GetModuleId(int structureIndex) => null;
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

        /// <summary>缺口哨兵:**2026-10-21 已兑现**(`O-24-1` 兑现轮)。
        /// <para>🔁 原测 `test_clinic_furnTraitsProductionReady_sentinelFalse` 断言 false —— 那是
        /// 「生产侧无实现者」的诚实登记。现生产实现 =
        /// <see cref="ClinicFurnitureTraitsSource"/>(21a 家具目录的运行期表 +
        /// 23 的只读视图),故断言翻为 true,并**同时钉住实现者存在且可用**:
        /// 只断言一个 bool 常量会被「改常量不改实现」满足。</para></summary>
        [Test]
        public void test_clinic_furnTraitsProductionReady_sentinelTrue_andImplementationUsable()
        {
            Assert.That(ClinicModifierEvaluator.FurnTraitsProductionReady, Is.True,
                "O-24-1(FURN_TABLE)已兑现 ⇒ 哨兵须为 true;若退回 false,说明生产属性源被撤");

            // 判别力:造一个最小布局 + 目录,生产源必须给出正确的三值答案。
            var catalogue = new ClinicFurnitureCatalogue(
                new[]
                {
                    new ClinicFurnitureEntry("bed", 1, new Fix(Fix.OneRaw / 64)),
                }, 3);
            var view = new FakeView(new WorldPos(0, 0, 0), new WorldPos(5, 5, 0));
            var source = new ClinicFurnitureTraitsSource(view, catalogue);

            // 夹具的 GetModuleId 恒 null ⇒ 两格都「有占用但无模块标识」= NonFurniture,
            // 未占用格 = Unknown。这正钉住三值语义(没被拍平成 bool)。
            Assert.That(source.Lookup(new WorldPos(0, 0, 0), out _), Is.EqualTo(ClinicCellLookup.NonFurniture));
            Assert.That(source.Lookup(new WorldPos(9, 9, 0), out _), Is.EqualTo(ClinicCellLookup.Unknown));
        }

        // ══════════════════ ⑦ 性质测试(AC-24-01 / AC-24-02 的明文组成部分)══════════════════
        //
        // 🔁 **2026-10-21 评审轮补写** —— GDD `design/gdd/clinic-machine.md:370` / `:372` 把
        //    「**性质测试**」写成 AC-24-01 / AC-24-02 的**组成部分**(两条均 `[B]` 级):
        //      AC-24-01:「**性质测试**:对任意 `MIN < MAX` 与任意输入,24 的产出 = 线性加权和,**不含 clamp**」
        //      AC-24-02:「`EquipMod ∈ [0, EQUIP_MOD_CAP]` 恒成立(**性质测试**,同 AC-24-01)」
        //    story-003 卡面转写时**漏了这一句**(卡面 `grep -c "性质测试"` = 0),原测试只有 3 条定点例子。
        //    **定点例子只能证伪全称命题,不能证明它** ⇒ 本组补上全称面。
        //    评审原件:`production/qa/evidence/review-s17-2-clinic003-2026-10-21.md` §三 Q7。
        //
        // ⚠️ **本组首版有一处假绿,由证伪轮 M1 抓出(2026-10-21)** —— 记在此防复发:
        //    `anyMinMax` 首版令 `expected = min + 3·max`,窄域两组 (-1,1) / (-1000,1000) 的和只有
        //    **2 / 2000**,而实现里唯一可能的钳制**恰好就是钳到冻结界 ±32768** ⇒ 这两组对 M1
        //    **毫无判别力**(M1 实测只红 5/7)。修法 = 加一格常量 `MinMaxSlackRaw`。
        //    **教训:性质测试的样本必须落在「变异能改变结果」的区间里,否则样本再多也是假的。**

        /// <summary>确定性伪随机(LCG)· 返回 `[-limit, +limit]` 内的 <see cref="long"/>。</summary>
        /// <remarks>⚠️ 禁 `System.Random` / 时钟 —— 测试须同一次构建内任意次运行结果相同。
        /// 固定种子的确定性生成符合 test-standards「no random seeds」的本意(禁的是**不可复现**)。
        /// <para>⚠️ 取 **高位**(`&gt;&gt; 32`):LCG 的低 k 位周期只有 2^(k+1),
        /// 取低位会让小模数(如 `n ∈ [1,8]`)在几百次采样内原地打转 —— 那样的「性质测试」样本退化。</para></remarks>
        private static long NextLong(ref ulong state, long limit)
        {
            state = state * 6364136223846793005UL + 1442695040888963407UL;
            return (long)((state >> 32) % (ulong)(2 * limit + 1)) - limit;
        }

        /// <summary>
        /// 「任意 MIN &lt; MAX」参数化用的**常量抬升量**(raw)。
        /// <para>⚠️ 这个值被改过两次,每次都是证伪轮抓出来的 —— 记在此防复发:</para>
        /// <list type="number">
        /// <item><b>100000(首版)</b>:只够打穿冻结界 `±32768`,对证伪 M2(钳到 `±262144`)不可观测
        /// ⇒ 四组里三组恒绿。</item>
        /// <item><b>4000000(现行)</b>:保证**四组全部** `|expected| &gt; 262144`,
        /// 对「钳到冻结界(±32768)」与「钳到 ±4.0(±262144)」两种变异**都可观测**。</item>
        /// </list>
        /// <para><b>取值纪律</b>:slack 的判据不是「够大就行」,而是「
        /// 任一 MIN/MAX 组合下和都要超过**被测变异可能引入的最宽钳制界**」。
        /// 定这个界前先看证伪轮打算试哪些钳制 —— 否则测出来的绿是空绿。</para>
        /// </summary>
        private const long MinMaxSlackRaw = 4000000L;

        /// <summary>确定性整数 ∈ `[minInclusive, maxInclusive]`。</summary>
        private static int NextInt(ref ulong state, int minInclusive, int maxInclusive)
        {
            state = state * 6364136223846793005UL + 1442695040888963407UL;
            int span = maxInclusive - minInclusive + 1;
            return minInclusive + (int)((state >> 33) % (ulong)span);
        }

        /// <summary>
        /// **AC-24-01 性质面**:对**任意**输入,`ClinicEnvModOf` 的产出恒等于
        /// `base_env + Σ e_env + adj_sum` 的线性求和,**不含任何 clamp / 饱和 / 归一化**。
        /// </summary>
        /// <remarks>
        /// <b>为什么是性质而非例子</b>:用固定种子 LCG 生成 **512 组**输入(量级 ±400000 raw,
        /// 远超 `ENV_MOD_MIN/MAX` = ∓32768 raw),逐组**独立手算**期望值再比对。
        /// 定点例子覆盖「这几个值对了」;本测覆盖「任意输入都对了」—— 后者才能证「不含 clamp」这个全称命题。
        /// <para><b>判别力</b>:在求值链任何位置引入 clamp / 饱和 / 归一化 ⇒ 越域那一批
        /// `actual ≠ expected` ⇒ 红。末段的采样守卫另钉「本测确实有足够多的越域样本」,
        /// 防止将来生成器参数被改小后退化成「全在域内 ⇒ 对 clamp 无判别力」。</para>
        /// </remarks>
        [Test]
        public void test_clinic_envMod_property_anyInput_equalsLinearSum_noClamp()
        {
            const long EnvModMinRaw = -32768L;      // ENV_MOD_MIN = -1/2 ⇒ raw
            const long EnvModMaxRaw = 32768L;       // ENV_MOD_MAX = +1/2 ⇒ raw
            const string TypeA = "a";
            const string TypeB = "b";

            ulong state = 0x243F6A8885A308D3UL;     // 固定种子
            long outOfRangeCases = 0;

            for (int i = 0; i < 512; i++)
            {
                long baseEnv = NextLong(ref state, 400000L);
                long e0 = NextLong(ref state, 400000L);
                long e1 = NextLong(ref state, 400000L);
                long w = NextLong(ref state, 30000L);

                // 生成器上界 400000 ≪ int64 上界 ⇒ 求和不可能溢出,直接算(不加 try/catch 掩盖失败)
                long expected = baseEnv + e0 + e1 + w;

                // C0 +x C1 ⇒ 恰一条相邻边 ⇒ adj_sum = w(只查 +x/+y,每条边计一次)
                ClinicRoomExtractionResult room = Room(C0, C1);
                var traits = new FakeTraits(new[]
                {
                    (C0, TypeA, 0, e0),
                    (C1, TypeB, 0, e1),
                });
                ClinicAdjTable adj = AdjTable(-30000, 30000, 64, (TypeA, TypeB, w));

                long actual = ClinicModifierEvaluator
                    .ClinicEnvModOf(room, traits, adj, new Fix(baseEnv)).Raw;

                Assert.That(actual, Is.EqualTo(expected),
                    $"第 {i} 组:base={baseEnv} / e_env={e0},{e1} / w={w} ⇒ 须原样出线性和 {expected}" +
                    "(任何 clamp 都会让它不等)");

                if (expected < EnvModMinRaw || expected > EnvModMaxRaw) outOfRangeCases++;
            }

            // 采样守卫 —— 若绝大多数样本都落在域内,本测对「是否 clamp」毫无判别力
            Assert.That(outOfRangeCases, Is.GreaterThan(400),
                $"须有足够多的样本落在 [ENV_MOD_MIN, ENV_MOD_MAX] 之外(实得 {outOfRangeCases}/512);" +
                "否则本测对 clamp 无判别力");
        }

        /// <summary>
        /// **AC-24-01 的「对任意 `MIN < MAX`」面**:24 侧**不引用** `ENV_MOD_MIN` / `ENV_MOD_MAX`,
        /// 故对**任一组** `MIN &lt; MAX`,越域输入的产出都仍是线性加权和 —— **不落进该区间**。
        /// </summary>
        /// <remarks>
        /// <b>编码方式</b>:24 的签名里没有 MIN/MAX 形参,故「任意 MIN &lt; MAX」只能在**期望式**一侧
        /// 参数化 —— 对每一组 (MIN, MAX),构造一条和**严格大于该组 MAX** 的输入,
        /// 断言产出 = 未钳制的线性和**且严格越过该组 MAX**。
        /// <para><b>⚠️ 首版有判别力缺口,已由证伪轮 M1 暴露并修(2026-10-21)</b>:首版令
        /// `expected = minRaw + 3·maxRaw`,对窄域两组(-1/1、-1000/1000)和只有 2 / 2000 ——
        /// **落在冻结界 `±32768` 之内**,而实现里唯一可能的钳制恰好就是钳到该冻结界
        /// ⇒ M1(在 `:121` 前加 `±32768` 钳制)对这**两组无判别力**,实测只红 2/4。
        /// 修法 = 追加一格常量贡献 <see cref="MinMaxSlackRaw"/>(100000),使**任何** (MIN, MAX)
        /// 下的和都同时满足两个条件:① 严格越过本组 MAX(原命题)② 落在冻结界之外(对 M1 有判别力)。
        /// 修后重跑 M1 ⇒ **4/4 全红**。</para>
        /// <para>⚠️ **判据边界(如实)**:这是对 MIN/MAX 的**采样**覆盖(4 组),不是形式化证明;
        /// 真正的「对任意」由上面那条 512 组输入的性质测承担。</para>
        /// </remarks>
        [TestCase(-1L, 1L)]
        [TestCase(-1000L, 1000L)]
        [TestCase(-32768L, 32768L)]        // 21a 已裁定的定值(ENV_MOD_MIN/MAX = ∓1/2)
        [TestCase(-1000000L, 1000000L)]
        public void test_clinic_envMod_property_anyMinMax_outputStillUnclamped(long minRaw, long maxRaw)
        {
            Assume.That(minRaw < maxRaw, "夹具前提:MIN < MAX");

            // 四格:三格各贡献 maxRaw,第四格贡献常量 Slack;base 从 minRaw 起步。
            // ⇒ 期望 = minRaw + 3·maxRaw + Slack:对本表四组分别 = 4000002 / 4002000 / 4065536 / 6000000
            //   —— **每一组都严格越过该组 MAX,且都落在 ±262144 之外**(故对 ±32768 与 ±262144 两种
            //   钳制变异都可观测;见 MinMaxSlackRaw 的两次修正记录)。
            ClinicRoomExtractionResult room = Room(C0, C1, C2, C3);
            var traits = new FakeTraits(new[]
            {
                (C0, "a", 0, maxRaw),
                (C1, "a", 0, maxRaw),
                (C2, "a", 0, maxRaw),
                (C3, "a", 0, MinMaxSlackRaw),
            });

            long expected = minRaw + 3 * maxRaw + MinMaxSlackRaw;   // 无邻接(NoPairsTable)⇒ adj = 0

            long actual = ClinicModifierEvaluator
                .ClinicEnvModOf(room, traits, NoPairsTable(), new Fix(minRaw)).Raw;

            Assert.That(actual, Is.EqualTo(expected),
                $"MIN={minRaw} / MAX={maxRaw}:产出须为未钳制的线性和 {expected}");
            Assert.That(actual, Is.GreaterThan(maxRaw),
                $"产出须**严格越过**本组的 MAX={maxRaw} —— 若 24 侧钳制则越不过");
        }

        /// <summary>
        /// **AC-24-02 性质面**:对**任意**档位向量,`ClinicEquipModOf` 的产出恒落在
        /// `[0, EQUIP_MOD_CAP]` 闭区间内 —— 同时独立核对精确值。
        /// </summary>
        /// <remarks>
        /// <b>为什么是性质而非例子</b>:固定种子 LCG 生成 **256 组**档位向量(格数 1–8、
        /// `tier ∈ [0, TIER_MAX]` 域内),逐组独立手算 `clamp(Σtier × 65536, 0, CAP)` 再比对。
        /// <para><b>判别力</b>:漏掉上钳 ⇒ `score ≥ 1` 时 `score×65536 ≥ 65536 > CAP` ⇒ 越界 ⇒ 红;
        /// 漏掉「tier=0 不进和」⇒ 期望式与实际同错时仍可能相等,故**另**由
        /// `test_clinic_equipMod_allZeroTier_returnsZero` 与 `sumAboveCap_pinsToCap` 分头钉住。</para>
        /// </remarks>
        [Test]
        public void test_clinic_equipMod_property_anyTierVector_inClosedRange()
        {
            const int TierMax = 3;                    // TIER_MAX(冻结值)
            ulong state = 0x452821E638D01377UL;       // 固定种子
            int pinnedCases = 0;

            for (int iter = 0; iter < 256; iter++)
            {
                int n = NextInt(ref state, 1, 8);

                var cells = new List<WorldPos>();
                var rows = new List<(WorldPos, string, int, long)>();
                long expectedScore = 0;

                for (int i = 0; i < n; i++)
                {
                    var cell = new WorldPos(i, 0, 0);   // 4-连通链 ⇒ 恰 1 簇
                    cells.Add(cell);
                    int tier = NextInt(ref state, 0, TierMax);
                    rows.Add((cell, "t", tier, 0L));
                    if (tier > 0) expectedScore += tier;  // tier = 0 不进和(F-24-2)
                }

                ClinicRoomExtractionResult room = Room(cells.ToArray());
                Assert.That(room.HasRoom, Is.True, $"第 {iter} 组:n={n} 的链须析出为单房间");

                long actual = ClinicModifierEvaluator
                    .ClinicEquipModOf(room, new FakeTraits(rows)).Raw;

                // ① 全称不变量:恒在闭区间内
                Assert.That(actual, Is.GreaterThanOrEqualTo(0L),
                    $"第 {iter} 组(n={n}, Σtier={expectedScore}):EquipMod 须 ≥ 0");
                Assert.That(actual, Is.LessThanOrEqualTo(ClinicModifierEvaluator.EquipModCapRaw),
                    $"第 {iter} 组(n={n}, Σtier={expectedScore}):EquipMod 须 ≤ EQUIP_MOD_CAP");

                // ② 精确值:clamp(Σtier × 65536, 0, CAP),独立手算
                long expectedRaw = expectedScore * Fix.OneRaw;
                if (expectedRaw > ClinicModifierEvaluator.EquipModCapRaw)
                    expectedRaw = ClinicModifierEvaluator.EquipModCapRaw;

                Assert.That(actual, Is.EqualTo(expectedRaw),
                    $"第 {iter} 组(n={n}, Σtier={expectedScore}):须为 clamp 后的 {expectedRaw}");

                if (expectedRaw == ClinicModifierEvaluator.EquipModCapRaw) pinnedCases++;
            }

            // 采样守卫:CAP = 1/4 < 1 ⇒ 任何 Σtier ≥ 1 都钉顶;本组绝大多数样本须落在此形态,
            // 否则「恒等 CAP」这一半没被真正覆盖。
            Assert.That(pinnedCases, Is.GreaterThan(200),
                $"须有足够多的样本钉顶(实得 {pinnedCases}/256);否则上钳面未被判别");
        }

        /// <summary>
        /// **Q8 配套**(2026-10-21 评审):实现 `ClinicModifierEvaluator.cs:150` 主动写了
        /// `if (tier &lt;= 0) continue;`,但原有夹具的 `tier` 取值全是非负 ⇒ 负值分支**零判据**。
        /// 若该行被改成 `tier == 0`,负档位会**减分**且无测可察。本测补上该判别面。
        /// </summary>
        /// <remarks>⚠️ `tier &lt; 0` 属**域外**输入(域 = `[0, TIER_MAX]`);本测被测的是
        /// **实现主动写下的防御分支**,不是域内语义。</remarks>
        [Test]
        public void test_clinic_equipMod_negativeTier_excludedFromScore()
        {
            ClinicRoomExtractionResult room = Room(C0, C1);
            var traits = new FakeTraits(new[]
            {
                (C0, "t", 2, 0L),
                (C1, "t", -100, 0L),      // 负档:域外,但实现写明不计入
            });

            Fix equip = ClinicModifierEvaluator.ClinicEquipModOf(room, traits);

            // Σtier 若把 −100 算进去 = −98 ⇒ 钳下界归 0;正确实现 = 2 ⇒ 钉顶。
            Assert.That(equip.Raw, Is.EqualTo(ClinicModifierEvaluator.EquipModCapRaw),
                "负档位不得进和:若进和则档位和被拉成负数 ⇒ 下钳到 0(≠ CAP)");
        }

        // ══════════════════════════════════════════════════════════════════════
        // CheckedAdd 守卫本体(2026-10-21 QA 策略轮补 —— 覆盖缺口 ①)
        // ══════════════════════════════════════════════════════════════════════
        //
        // **为什么补这三条**:qa-lead 在 sprint-17 签核轮实测
        //   `grep -rn "CheckedAdd" unity/Assets/Tests/EditMode/ClinicMachine/*.cs` = **零命中**,
        // 而生产侧 `ClinicModifierEvaluator.cs` 有 3 处调用(:115 / :119 / :151)。
        // 卡面 AC 第 6 条改写后的主语是「**每处乘加均经显式域守卫**」——
        // 但守卫的**越界分支**(throw)此前无任何独立载体:既有两条越域夹具
        // (`largeScore` / `nMaxBoundary`)打到的是 **clamp 钉顶**,不是 `CheckedAdd` 的 throw。
        //
        // ⚠️ **判别力来源**:若把 `CheckedAdd` 退回裸 `a + b`(C# 未检查上下文下**回绕**),
        //   上溢用例会得到一个**负数**、下溢用例会得到一个**正数**,断言必然红。
        //   这不是「域内会不会溢出」的命题 —— 域内(`n_max × C_max` 等)有 schema 门守,
        //   本组钉的是**实现主动写下的防御分支**(与 `negativeTier` 那条同款纪律)。
        //
        // ⚠️ **域外输入,如实登记**:下列 fixture 的 `EEnvRaw` 取 `long.MaxValue` / `long.MinValue`,
        //   远超 `EnvContributionMaxRaw = 1/8`。与既有两条越域夹具同格。

        /// <summary>
        /// `CheckedAdd` **上溢分支**:`base_env` 已接近 `long.MaxValue`,再加正的 `e_env` ⇒ throw。
        /// <para>判别力:退回裸 `a + b` ⇒ 回绕成负数 ⇒ 不抛 ⇒ 本测红。</para>
        /// </summary>
        [Test]
        public void test_clinic_envMod_sumOverflowUpper_throws()
        {
            // EEnvRaw = long.MaxValue - 1,baseEnv = 1 ⇒ 1 + (MaxValue-1) = MaxValue(不溢);
            // 再加一格 +1 就溢。故此处给两格:EEnv 各 (MaxValue/2),baseEnv = 2。
            long half = long.MaxValue / 2;
            var traits = new FakeTraits(new[]
            {
                (C0, "t", 0, half),
                (C1, "t", 0, half),
            });
            ClinicRoomExtractionResult two = Room(C0, C1);

            // Σe_env = 2 × (MaxValue/2) = MaxValue - 1;baseEnv = 2 ⇒ 溢出。
            Assert.Throws<OverflowException>(() =>
                    ClinicModifierEvaluator.ClinicEnvModOf(two, traits, NoPairsTable(), new Fix(2)),
                "Σe_env + base_env 越 int64 上界 ⇒ 必须 throw,不得回绕(ADR-012 F7)");
        }

        /// <summary>
        /// `CheckedAdd` **下溢分支**:`base_env` 已接近 `long.MinValue`,再加负的 `e_env` ⇒ throw。
        /// <para>判别力:退回裸 `a + b` ⇒ 回绕成正数 ⇒ 不抛 ⇒ 本测红。</para>
        /// </summary>
        [Test]
        public void test_clinic_envMod_sumOverflowLower_throws()
        {
            long half = long.MinValue / 2;               // 负
            var traits = new FakeTraits(new[]
            {
                (C0, "t", 0, half),
                (C1, "t", 0, half),
            });
            ClinicRoomExtractionResult two = Room(C0, C1);

            Assert.Throws<OverflowException>(() =>
                    ClinicModifierEvaluator.ClinicEnvModOf(two, traits, NoPairsTable(), new Fix(-2)),
                "Σe_env + base_env 越 int64 下界 ⇒ 必须 throw,不得回绕(ADR-012 F7)");
        }

        /// <summary>
        /// **边界正例**:恰好等于 `long.MaxValue` 时**不抛**(守卫是 `>` 不是 `>=`)。
        /// <para>判别力:若把 `a > long.MaxValue - b` 写成 `>=`,本测会抛 ⇒ 红。
        /// 反向:若写成 `a > long.MaxValue` 这种永假式,上溢测不抛 ⇒ 那条红。</para>
        /// </summary>
        [Test]
        public void test_clinic_envMod_sumExactlyMax_doesNotThrow()
        {
            long half = long.MaxValue / 2;               // MaxValue/2 × 2 = MaxValue - 1
            var traits = new FakeTraits(new[]
            {
                (C0, "t", 0, half),
                (C1, "t", 0, half),
            });
            ClinicRoomExtractionResult two = Room(C0, C1);

            // Σe_env = MaxValue - 1;baseEnv = 1 ⇒ 恰为 MaxValue,不越界 ⇒ 不得抛。
            Fix env = ClinicModifierEvaluator.ClinicEnvModOf(
                two, traits, NoPairsTable(), new Fix(1));

            Assert.That(env.Raw, Is.EqualTo(long.MaxValue),
                "恰好等于 int64 上界属**合法**,守卫判据是 `>` 不是 `>=`");
        }
    }
}
