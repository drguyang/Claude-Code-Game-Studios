// Story 006 · clinic-machine —— 房间类型匹配(ROOM_TABLE 子句求值)+ AC-24-03。
//
// 卡面:production/epics/clinic-machine/story-006-room-type-matching.md
// 权威来源:
//   GDD design/gdd/clinic-machine.md 规则二(房间定义 = ROOM_TABLE;未满足任何房间类型 ⇒ ROOM_NONE)
//     · 规则二 2026-09-17 复核(邻接子句的方向确定性「同 adj_sum 的 +x/+y 单向查法」)
//     · 规则二 2026-10-21 补记(多匹配未定义,登记 `OQ-24-7`;暂定 = 声明序首个匹配)
//     · EC-24-01(空布局 ⇒ ROOM_NONE)· EC-24-02(有家具但谓词全不成立 ⇒ 中性)
//     · EC-24-07(≥ 2 个 4-不连通簇 ⇒ 整盘 ROOM_NONE)
//   ADR-015 §三(单一整数格)· §五(4 邻接判定,非物理 / 非 NavMesh)
//   ADR-005(纯函数派生 / 无状态 / 可重放)· ADR-016 §一(三源不变量)
//
// ⚠️ 被测对象 = **生产代码**(`DaYiJingCheng.Sim.Clinic.ClinicRoomMatcher`),不是本文件的副本。
//   本文件只含夹具(测试内局部替身)与断言。
//
// ⚠️ 测试纪律:零写盘 · 无随机种子 · 无时间依赖 · 无文件 I/O
//   (`.claude/rules/test-standards.md`;亦避 FsWriteBoundaryGate 的 R1/R2 面)。
//
// ⚠️ **AC-24-03 的双向载体**(卡面 AC 第 1 条明令「不得只测一个就宣称覆盖」):
//   ① `test_roomNone_emptyLayout_returnsFalse` —— EC-24-01(空布局);
//   ② `test_roomNone_furniturePresentButNoPredicateHolds_returnsFalse` —— EC-24-02(有家具但谓词全不成立)。
//   EC-24-01 是 EC-24-02 的**特例**(空布局必然谓词全不成立),反向不成立
//   ⇒ 只测 ① 会把「子句求值整段坏了」伪装成通过。
//
// ⚠️ **`ROOM_NONE` 短路的可观测面 = 诊断全零**(不是「返回 false」):
//   空 `RoomCells` 下子句本来就全不成立,删掉短路返回值不变 —— 因此卡面证伪表里
//   「删 `HasRoom == false` 短路 ⇒ EC-24-01 测红」这一条,只能由
//   `diagnostics` 的「ROOM_NONE ⇒ 不进入子句求值 ⇒ 未知类型计数恒 0」承载。
//   见 `ClinicRoomMatchDiagnostics` 的备注。

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DaYiJingCheng.Sim.Clinic;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.ClinicMachine
{
    /// <summary>24「房间类型匹配」的 EditMode 判据(AC-24-03 · 三子句型 · 合取 · 多匹配定序 · 纯函数 · 未知类型)。</summary>
    [TestFixture]
    public sealed class ClinicRoomMatcherTest
    {
        // ════════════════════════════════════════════════════════════════════
        // 夹具基建 —— 只读视图与格属性源的测试内局部替身
        // ════════════════════════════════════════════════════════════════════

        /// <summary>「每格一件单格结构」的只读视图替身(协作对象,不是生产实现)。</summary>
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

            /// <summary>无模块标识 —— 本文件走代理判据析出(真判据归 story-002 的重载)。</summary>
            public string GetModuleId(int structureIndex) => null;
        }

        /// <summary>格属性源替身(三值分类:家具 / 非家具 / 查不到)。</summary>
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

        // ════════════════════════════════════════════════════════════════════
        // 便捷构造
        // ════════════════════════════════════════════════════════════════════

        private static WorldPos Cell(int x, int y, int z = 0) => new WorldPos(x, y, z);

        /// <summary>按格集析出房间(走生产 `ClinicRoomExtraction`,不在测试里复制析出逻辑)。</summary>
        private static ClinicRoomExtractionResult Room(params WorldPos[] cells)
            => ClinicRoomExtraction.Extract(new FakeView(cells));

        /// <summary>每格一件家具(type 给定,tier / e_env 取 0 —— 本件不测乘子)。</summary>
        private static FakeTraits Traits(params (WorldPos Cell, string Type)[] cells)
            => new FakeTraits(cells.Select(c => (c.Cell, c.Type, 0, 0L)));

        private static ClinicRoomClause MinCount(string type, int min)
            => new ClinicRoomClause(ClinicRoomClauseKind.MinCount, type, null, ClinicRoomClauseDirection.None, min);

        private static ClinicRoomClause HasType(string type)
            => new ClinicRoomClause(ClinicRoomClauseKind.HasType, type, null, ClinicRoomClauseDirection.None, 0);

        private static ClinicRoomClause Adjacent(
            string type, string neighborType, ClinicRoomClauseDirection direction, int min = 1)
            => new ClinicRoomClause(ClinicRoomClauseKind.AdjacencyMinCount, type, neighborType, direction, min);

        private static ClinicRoomDef RoomDef(string roomType, params ClinicRoomClause[] clauses)
            => new ClinicRoomDef(roomType, clauses);

        private static ClinicRoomTable Table(params ClinicRoomDef[] rooms) => new ClinicRoomTable(rooms);

        // ════════════════════════════════════════════════════════════════════
        // AC-24-03(双向)—— ① 空布局(EC-24-01)· ② 有家具但谓词全不成立(EC-24-02)
        //                       ③ ≥ 2 簇(EC-24-07,另一个 ROOM_NONE 入口)
        // ════════════════════════════════════════════════════════════════════

        /// <summary>EC-24-01:空布局 ⇒ ROOM_NONE。**不是错误**,是开局空医馆的合法状态。</summary>
        /// <remarks><b>判别力</b>:删 `if (!room.HasRoom) return false;` 短路 ⇒ 空 `RoomCells` 下
        /// 子句仍全不成立(返回值不变),但**诊断**会从 (0,0) 变成 (0, N) —— 未知类型计数被点亮
        /// ⇒ 本测最后一条断言转红。这正是 `ROOM_NONE ⇒ 不进入子句求值` 契约的载体。</remarks>
        [Test]
        public void test_roomNone_emptyLayout_returnsFalse()
        {
            ClinicRoomExtractionResult room = Room();
            FakeTraits traits = Traits();
            ClinicRoomTable table = Table(RoomDef("ward", MinCount("bed", 2), HasType("bed")));

            bool matched = ClinicRoomMatcher.TryMatchRoom(
                room, traits, table, out string roomType, out ClinicRoomMatchDiagnostics diagnostics);

            Assert.That(room.HasRoom, Is.False, "前提:空布局 ⇒ 未析出房间");
            Assert.That(matched, Is.False, "AC-24-03 ①(EC-24-01):空布局 ⇒ ROOM_NONE");
            Assert.That(roomType, Is.Null, "ROOM_NONE 的表示 = false + null(不引入可空字符串类型)");
            Assert.That(diagnostics.MatchedRoomCount, Is.EqualTo(0));
            Assert.That(diagnostics.UnknownClauseTypeCount, Is.EqualTo(0),
                "ROOM_NONE 短路 ⇒ 不进入子句求值 ⇒ 诊断全零(删短路后本值变非零)");
        }

        /// <summary>EC-24-02:房间已析出,但表中**无任一**房间类型的谓词成立 ⇒ ROOM_NONE。</summary>
        /// <remarks><b>判别力</b>:与 EC-24-01 的区别在 `HasRoom == true` —— 若「子句求值」整段坏了
        /// (恒不成立),本测仍绿而 EC-24-01 也绿 ⇒ 两条**必须**分别存在。本测还额外钉住
        /// `UnknownClauseTypeCount == 0`:`bed` 在布局里**存在**,本条与「未知类型」无关。</remarks>
        [Test]
        public void test_roomNone_furniturePresentButNoPredicateHolds_returnsFalse()
        {
            ClinicRoomExtractionResult room = Room(Cell(0, 0));
            FakeTraits traits = Traits((Cell(0, 0), "bed"));
            ClinicRoomTable table = Table(RoomDef("ward", MinCount("bed", 2)));

            bool matched = ClinicRoomMatcher.TryMatchRoom(
                room, traits, table, out string roomType, out ClinicRoomMatchDiagnostics diagnostics);

            Assert.That(room.HasRoom, Is.True, "前提:有家具且单连通 ⇒ 房间已析出(与 EC-24-01 的分界)");
            Assert.That(matched, Is.False, "AC-24-03 ②(EC-24-02):谓词全不成立 ⇒ ROOM_NONE");
            Assert.That(roomType, Is.Null);
            Assert.That(diagnostics.MatchedRoomCount, Is.EqualTo(0));
            Assert.That(diagnostics.UnknownClauseTypeCount, Is.EqualTo(0),
                "bed 在布局里存在 ⇒ 不是「未知类型」;本字段区分「不成立」与「引用缺失」");
        }

        /// <summary>EC-24-07:≥ 2 个 4-不连通簇 ⇒ 整盘 ROOM_NONE ⇒ 匹配必然不成立。</summary>
        [Test]
        public void test_roomNone_twoClusters_returnsFalse()
        {
            ClinicRoomExtractionResult room = Room(Cell(0, 0), Cell(3, 0));
            FakeTraits traits = Traits((Cell(0, 0), "bed"), (Cell(3, 0), "bed"));
            ClinicRoomTable table = Table(RoomDef("ward", MinCount("bed", 2)));

            bool matched = ClinicRoomMatcher.TryMatchRoom(
                room, traits, table, out string roomType, out ClinicRoomMatchDiagnostics diagnostics);

            Assert.That(room.HasRoom, Is.False, "前提:2 簇 ⇒ 整盘 ROOM_NONE");
            Assert.That(matched, Is.False, "EC-24-07:多簇布局不得被匹配成任何房间类型");
            Assert.That(roomType, Is.Null);
            Assert.That(diagnostics.UnknownClauseTypeCount, Is.EqualTo(0),
                "同 EC-24-01:ROOM_NONE ⇒ 不进入子句求值");
        }

        // ════════════════════════════════════════════════════════════════════
        // 三种子句类型 —— 每种至少一正一反
        // ════════════════════════════════════════════════════════════════════

        /// <summary>`min_count` 反例:1 张床 < min 2 ⇒ 不成立。</summary>
        /// <remarks><b>判别力</b>:把 `CountOfType(...) >= clause.Min` 改成 `>= 1` ⇒ 本测红。</remarks>
        [Test]
        public void test_clause_minCount_oneBed_doesNotMatch()
        {
            ClinicRoomExtractionResult room = Room(Cell(0, 0));
            FakeTraits traits = Traits((Cell(0, 0), "bed"));
            ClinicRoomTable table = Table(RoomDef("ward", MinCount("bed", 2), HasType("bed")));

            bool matched = ClinicRoomMatcher.TryMatchRoom(
                room, traits, table, out string roomType, out ClinicRoomMatchDiagnostics diagnostics);

            Assert.That(matched, Is.False, "min_count bed ≥ 2:1 张床不成立");
            Assert.That(roomType, Is.Null);
            Assert.That(diagnostics.MatchedRoomCount, Is.EqualTo(0));
        }

        /// <summary>`min_count` 正例:2 张床(4 邻接,构成一个房间)≥ 2 ⇒ 成立。</summary>
        [Test]
        public void test_clause_minCount_twoBeds_matches()
        {
            ClinicRoomExtractionResult room = Room(Cell(0, 0), Cell(1, 0));
            FakeTraits traits = Traits((Cell(0, 0), "bed"), (Cell(1, 0), "bed"));
            ClinicRoomTable table = Table(RoomDef("ward", MinCount("bed", 2), HasType("bed")));

            bool matched = ClinicRoomMatcher.TryMatchRoom(room, traits, table, out string roomType);

            Assert.That(matched, Is.True);
            Assert.That(roomType, Is.EqualTo("ward"));
        }

        /// <summary>`has_type` 正例:存在 1 个 `surgery_table` 即成立(无阈值语义)。</summary>
        [Test]
        public void test_clause_hasType_singleSurgeryTable_matches()
        {
            ClinicRoomExtractionResult room = Room(Cell(0, 0));
            FakeTraits traits = Traits((Cell(0, 0), "surgery_table"));
            ClinicRoomTable table = Table(RoomDef("surgery", HasType("surgery_table")));

            bool matched = ClinicRoomMatcher.TryMatchRoom(room, traits, table, out string roomType);

            Assert.That(matched, Is.True);
            Assert.That(roomType, Is.EqualTo("surgery"));
        }

        /// <summary>`has_type` 反例:布局里没有 `surgery_table` ⇒ 不成立,且该引用被记为「未知类型」。</summary>
        [Test]
        public void test_clause_hasType_absent_doesNotMatch()
        {
            ClinicRoomExtractionResult room = Room(Cell(0, 0));
            FakeTraits traits = Traits((Cell(0, 0), "bed"));
            ClinicRoomTable table = Table(RoomDef("surgery", HasType("surgery_table")));

            bool matched = ClinicRoomMatcher.TryMatchRoom(
                room, traits, table, out string roomType, out ClinicRoomMatchDiagnostics diagnostics);

            Assert.That(matched, Is.False);
            Assert.That(roomType, Is.Null);
            Assert.That(diagnostics.UnknownClauseTypeCount, Is.EqualTo(1),
                "布局里没有任何一格承载 surgery_table ⇒ 该引用计 1 条(可观测)");
        }

        /// <summary>`adjacency_min_count` 正例(+x):手术台在 +x 侧邻接无菌柜 ⇒ 成立。</summary>
        [Test]
        public void test_clause_adjacency_plusX_neighbourMatches()
        {
            ClinicRoomExtractionResult room = Room(Cell(0, 0), Cell(1, 0));
            FakeTraits traits = Traits((Cell(0, 0), "surgery_table"), (Cell(1, 0), "sterile_cabinet"));
            ClinicRoomTable table = Table(RoomDef("surgery",
                HasType("surgery_table"),
                Adjacent("surgery_table", "sterile_cabinet", ClinicRoomClauseDirection.PlusX)));

            bool matched = ClinicRoomMatcher.TryMatchRoom(room, traits, table, out string roomType);

            Assert.That(matched, Is.True);
            Assert.That(roomType, Is.EqualTo("surgery"));
        }

        /// <summary>`adjacency_min_count` **方向反例**:邻居在 −x 侧 ⇒ **不计数**(+x 单向查法)。</summary>
        /// <remarks><b>判别力</b>:把 +x 查法改成双向(也查 `x-1`)⇒ 本测由 false 变 true ⇒ 红。
        /// 方向正是 GDD 规则二 2026-09-17 复核点名要「确定性」的那一项 —— 双向查等于把它废掉。</remarks>
        [Test]
        public void test_clause_adjacency_neighbourOnMinusX_doesNotMatch()
        {
            ClinicRoomExtractionResult room = Room(Cell(-1, 0), Cell(0, 0));
            FakeTraits traits = Traits((Cell(-1, 0), "sterile_cabinet"), (Cell(0, 0), "surgery_table"));
            ClinicRoomTable table = Table(RoomDef("surgery",
                HasType("surgery_table"),
                Adjacent("surgery_table", "sterile_cabinet", ClinicRoomClauseDirection.PlusX)));

            bool matched = ClinicRoomMatcher.TryMatchRoom(
                room, traits, table, out string roomType, out ClinicRoomMatchDiagnostics diagnostics);

            Assert.That(matched, Is.False, "无菌柜在 −x 侧 ⇒ +x 单向查法查不到 ⇒ 不成立");
            Assert.That(roomType, Is.Null);
            Assert.That(diagnostics.MatchedRoomCount, Is.EqualTo(0));
        }

        /// <summary>`adjacency_min_count` 正例(+y):药柜在 +y 侧邻接药罐 ⇒ 成立(种子 `herb_room` 形状)。</summary>
        [Test]
        public void test_clause_adjacency_plusY_neighbourMatches()
        {
            ClinicRoomExtractionResult room = Room(Cell(0, 0), Cell(0, 1));
            FakeTraits traits = Traits((Cell(0, 0), "herb_cabinet"), (Cell(0, 1), "chamber_pot"));
            ClinicRoomTable table = Table(RoomDef("herb_room",
                MinCount("herb_cabinet", 1),
                Adjacent("herb_cabinet", "chamber_pot", ClinicRoomClauseDirection.PlusY)));

            bool matched = ClinicRoomMatcher.TryMatchRoom(room, traits, table, out string roomType);

            Assert.That(matched, Is.True);
            Assert.That(roomType, Is.EqualTo("herb_room"));
        }

        /// <summary>`adjacency_min_count` **+y 方向反例**:药罐在 −y 侧 ⇒ 不计数。</summary>
        /// <remarks><b>判别力</b>:把 +y 查法改成双向 ⇒ 本测红(与 +x 反例成对,两个方向各有载体)。</remarks>
        [Test]
        public void test_clause_adjacency_neighbourOnMinusY_doesNotMatch()
        {
            ClinicRoomExtractionResult room = Room(Cell(0, -1), Cell(0, 0));
            FakeTraits traits = Traits((Cell(0, -1), "chamber_pot"), (Cell(0, 0), "herb_cabinet"));
            ClinicRoomTable table = Table(RoomDef("herb_room",
                MinCount("herb_cabinet", 1),
                Adjacent("herb_cabinet", "chamber_pot", ClinicRoomClauseDirection.PlusY)));

            bool matched = ClinicRoomMatcher.TryMatchRoom(room, traits, table, out string roomType);

            Assert.That(matched, Is.False, "药罐在 −y 侧 ⇒ +y 单向查法查不到 ⇒ 不成立");
            Assert.That(roomType, Is.Null);
        }

        // ════════════════════════════════════════════════════════════════════
        // 谓词 = 子句合取(任一不成立 ⇒ 不匹配;无「部分匹配」)
        // ════════════════════════════════════════════════════════════════════

        /// <summary>合取反例:两条子句只成立一条 ⇒ **不匹配**(不得「部分匹配」)。</summary>
        /// <remarks><b>判别力</b>:把 `if (!ClauseHolds(...)) return false;` 改成
        /// `if (ClauseHolds(...)) return true;`(合取变析取)⇒ 本测由 false 变 true ⇒ 红。</remarks>
        [Test]
        public void test_conjunction_onlyOneClauseHolds_doesNotMatch()
        {
            ClinicRoomExtractionResult room = Room(Cell(0, 0), Cell(1, 0));
            FakeTraits traits = Traits((Cell(0, 0), "bed"), (Cell(1, 0), "bed"));
            ClinicRoomTable table = Table(RoomDef("combo",
                MinCount("bed", 1),
                HasType("surgery_table")));

            bool matched = ClinicRoomMatcher.TryMatchRoom(
                room, traits, table, out string roomType, out ClinicRoomMatchDiagnostics diagnostics);

            Assert.That(matched, Is.False, "min_count bed ≥ 1 成立但 has_type surgery_table 不成立 ⇒ 整条不成立");
            Assert.That(roomType, Is.Null);
            Assert.That(diagnostics.MatchedRoomCount, Is.EqualTo(0));
        }

        /// <summary>合取正例:两条子句都成立 ⇒ 匹配。</summary>
        [Test]
        public void test_conjunction_allClausesHold_matches()
        {
            ClinicRoomExtractionResult room = Room(Cell(0, 0), Cell(1, 0), Cell(2, 0));
            FakeTraits traits = Traits(
                (Cell(0, 0), "bed"), (Cell(1, 0), "bed"), (Cell(2, 0), "surgery_table"));
            ClinicRoomTable table = Table(RoomDef("combo",
                MinCount("bed", 1),
                HasType("surgery_table")));

            bool matched = ClinicRoomMatcher.TryMatchRoom(room, traits, table, out string roomType);

            Assert.That(matched, Is.True);
            Assert.That(roomType, Is.EqualTo("combo"));
        }

        /// <summary>「部分匹配」**不计入** `MatchedRoomCount` —— 只有全子句成立的房间才计数。</summary>
        [Test]
        public void test_conjunction_partialMatch_isNotCountedAsMatched()
        {
            ClinicRoomExtractionResult room = Room(Cell(0, 0), Cell(1, 0), Cell(2, 0));
            FakeTraits traits = Traits(
                (Cell(0, 0), "bed"), (Cell(1, 0), "bed"), (Cell(2, 0), "surgery_table"));
            ClinicRoomTable table = Table(
                RoomDef("ward", MinCount("bed", 2), HasType("bed")),
                RoomDef("surgery",
                    HasType("surgery_table"),
                    Adjacent("surgery_table", "sterile_cabinet", ClinicRoomClauseDirection.PlusX)));

            bool matched = ClinicRoomMatcher.TryMatchRoom(
                room, traits, table, out string roomType, out ClinicRoomMatchDiagnostics diagnostics);

            Assert.That(matched, Is.True);
            Assert.That(roomType, Is.EqualTo("ward"));
            Assert.That(diagnostics.MatchedRoomCount, Is.EqualTo(1),
                "surgery 只成立 has_type 一条 ⇒ 不是匹配;计数必须是 1 而非 2");
        }

        // ════════════════════════════════════════════════════════════════════
        // 多匹配裁决 = 声明序首个(OQ-24-7 暂定)+ 缺口哨兵
        // ════════════════════════════════════════════════════════════════════

        /// <summary>同一布局同时满足两个房间类型 ⇒ 取 `Rooms` 中**行序靠前**那个。</summary>
        /// <remarks><b>判别力</b>:把遍历改成逆序(`for (i = Count - 1; i >= 0; i--)`)⇒
        /// 本测返回 `wardish` 而非 `ward` ⇒ 红。</remarks>
        [Test]
        public void test_multiMatch_declarationOrderFirstWins()
        {
            ClinicRoomExtractionResult room = Room(Cell(0, 0), Cell(1, 0));
            FakeTraits traits = Traits((Cell(0, 0), "bed"), (Cell(1, 0), "bed"));
            ClinicRoomTable table = Table(
                RoomDef("ward", MinCount("bed", 2)),
                RoomDef("wardish", HasType("bed")));

            bool matched = ClinicRoomMatcher.TryMatchRoom(
                room, traits, table, out string roomType, out ClinicRoomMatchDiagnostics diagnostics);

            Assert.That(matched, Is.True);
            Assert.That(diagnostics.MatchedRoomCount, Is.EqualTo(2), "前提:两个房间类型同时成立");
            Assert.That(roomType, Is.EqualTo("ward"), "OQ-24-7 暂定:声明序首个匹配(不排序 / 不按名字 / 不取最大 base_env)");
        }

        /// <summary>把同一组房间**反序声明** ⇒ 胜出者随之变成另一个 —— 证明裁决**只**依赖声明序。</summary>
        /// <remarks><b>判别力</b>:若实现改成「按名字序」「按 `base_env` 最大」或逆序遍历,
        /// 本测与 <see cref="test_multiMatch_declarationOrderFirstWins"/> **至少一条**红。</remarks>
        [Test]
        public void test_multiMatch_reversedDeclarationOrder_winsTheOtherOne()
        {
            ClinicRoomExtractionResult room = Room(Cell(0, 0), Cell(1, 0));
            FakeTraits traits = Traits((Cell(0, 0), "bed"), (Cell(1, 0), "bed"));
            ClinicRoomTable table = Table(
                RoomDef("wardish", HasType("bed")),
                RoomDef("ward", MinCount("bed", 2)));

            bool matched = ClinicRoomMatcher.TryMatchRoom(
                room, traits, table, out string roomType, out ClinicRoomMatchDiagnostics diagnostics);

            Assert.That(matched, Is.True);
            Assert.That(diagnostics.MatchedRoomCount, Is.EqualTo(2));
            Assert.That(roomType, Is.EqualTo("wardish"), "声明序反了 ⇒ 胜出者也反");
        }

        /// <summary>缺口哨兵:多匹配裁决**仍是暂定的**(`OQ-24-7` 未裁)。</summary>
        /// <remarks>⚠️ **`OQ-24-7` 裁定后本测必须转红** —— 那是提醒,不是回归。
        /// 届时须同步改实现与 `MultiMatchPolicyIsProvisional`,并**如实登记改判**,
        /// 不得把它当「坏掉的测试」静默改绿(与 `FurnTraitsProductionReady` 同款纪律)。</remarks>
        [Test]
        public void test_multiMatch_policyIsProvisional_sentinel()
        {
            Assert.That(ClinicRoomMatcher.MultiMatchPolicyIsProvisional, Is.True,
                "OQ-24-7 未裁 ⇒ 暂定裁决可观测;裁定后本测转红即提醒");
        }

        // ════════════════════════════════════════════════════════════════════
        // 纯函数(无缓存 / 无隐藏状态)—— ADR-005 / ADR-016 §一
        // ════════════════════════════════════════════════════════════════════

        /// <summary>同一布局 + 同一表,两次独立求值 ⇒ 逐项相同。</summary>
        [Test]
        public void test_purity_sameInputsTwice_identicalResult()
        {
            ClinicRoomExtractionResult room = Room(Cell(0, 0), Cell(1, 0));
            FakeTraits traits = Traits((Cell(0, 0), "bed"), (Cell(1, 0), "bed"));
            ClinicRoomTable table = Table(
                RoomDef("ward", MinCount("bed", 2)),
                RoomDef("wardish", HasType("bed")));

            bool m1 = ClinicRoomMatcher.TryMatchRoom(
                room, traits, table, out string t1, out ClinicRoomMatchDiagnostics d1);
            bool m2 = ClinicRoomMatcher.TryMatchRoom(
                room, traits, table, out string t2, out ClinicRoomMatchDiagnostics d2);

            Assert.That(m2, Is.EqualTo(m1));
            Assert.That(t2, Is.EqualTo(t1));
            Assert.That(d2.MatchedRoomCount, Is.EqualTo(d1.MatchedRoomCount));
            Assert.That(d2.UnknownClauseTypeCount, Is.EqualTo(d1.UnknownClauseTypeCount));
        }

        /// <summary>
        /// **无缓存**:先求值布局 A(⇒ `ward`),再求值**不同**布局 B(⇒ `surgery`);
        /// B 必须给出 B 的答案,不得被 A 的结果污染。
        /// </summary>
        /// <remarks><b>判别力</b>:引入一个「不按输入分键」的静态缓存(第二次调用直接返回首次结果)
        /// ⇒ 本测返回 `ward` 而非 `surgery` ⇒ 红。这钉的是 control-manifest 的
        /// 「Forbidden:在 sim 程序集内留匹配结果缓存」在**可观测面**上的那一半
        /// (一个按 `RoomId` 正确分键的缓存是不可观测的,故本测只能钉住「泄漏」这一种失败)。</remarks>
        [Test]
        public void test_purity_noCache_acrossDifferentLayouts()
        {
            ClinicRoomTable table = Table(
                RoomDef("ward", MinCount("bed", 2)),
                RoomDef("surgery", HasType("surgery_table")));

            ClinicRoomExtractionResult layoutA = Room(Cell(0, 0), Cell(1, 0));
            FakeTraits traitsA = Traits((Cell(0, 0), "bed"), (Cell(1, 0), "bed"));

            ClinicRoomExtractionResult layoutB = Room(Cell(10, 0));
            FakeTraits traitsB = Traits((Cell(10, 0), "surgery_table"));

            ClinicRoomMatcher.TryMatchRoom(layoutA, traitsA, table, out string typeA);
            ClinicRoomMatcher.TryMatchRoom(layoutB, traitsB, table, out string typeB);

            Assert.That(typeA, Is.EqualTo("ward"), "前提:布局 A ⇒ ward");
            Assert.That(typeB, Is.EqualTo("surgery"), "布局 B 必须给出 B 的答案 —— 不得被上一次求值污染");
        }

        // ════════════════════════════════════════════════════════════════════
        // 未知类型:不抛 · 按「不成立」处理 · 可观测
        // ════════════════════════════════════════════════════════════════════

        /// <summary>子句引用了布局里不存在的类型 ⇒ **不抛**,按「不成立」处理,并计入诊断。</summary>
        /// <remarks>⚠️ 本条的「未知类型」= **布局里没有任何一格承载该 `type`**;
        /// 「该 `type` 不在 `FURN_TYPES` 闭集」是**烘焙期**判据(归 story-001),
        /// 匹配期无从判定 —— 见 `ClinicRoomMatchDiagnostics.UnknownClauseTypeCount` 的备注。</remarks>
        [Test]
        public void test_unknownType_doesNotThrow_treatedAsNotSatisfied()
        {
            ClinicRoomExtractionResult room = Room(Cell(0, 0));
            FakeTraits traits = Traits((Cell(0, 0), "bed"));
            ClinicRoomTable table = Table(RoomDef("ghost", HasType("ghost_type")));

            // 不抛 —— 若抛,本行直接以异常失败(正是要检测的行为)。
            bool matched = ClinicRoomMatcher.TryMatchRoom(
                room, traits, table, out string roomType, out ClinicRoomMatchDiagnostics diagnostics);

            Assert.That(matched, Is.False, "未知类型按「不成立」处理,不得抛");
            Assert.That(roomType, Is.Null);
            Assert.That(diagnostics.UnknownClauseTypeCount, Is.EqualTo(1),
                "该引用须**可观测** —— 否则「表写错类型名」与「布局真没摆」无从区分");
        }

        /// <summary>未知类型的房间**不阻断**其它房间的匹配;计数如实反映(邻接子句两端各算一条)。</summary>
        [Test]
        public void test_unknownType_doesNotBlockOtherRooms_andCountsEachRef()
        {
            ClinicRoomExtractionResult room = Room(Cell(0, 0), Cell(1, 0));
            FakeTraits traits = Traits((Cell(0, 0), "bed"), (Cell(1, 0), "bed"));
            ClinicRoomTable table = Table(
                RoomDef("ghost",
                    HasType("ghost_type"),
                    Adjacent("ghost_type", "ghost_neighbour", ClinicRoomClauseDirection.PlusX)),
                RoomDef("ward", MinCount("bed", 2)));

            bool matched = ClinicRoomMatcher.TryMatchRoom(
                room, traits, table, out string roomType, out ClinicRoomMatchDiagnostics diagnostics);

            Assert.That(matched, Is.True);
            Assert.That(roomType, Is.EqualTo("ward"), "ghost 不成立不得拖累 ward");
            Assert.That(diagnostics.MatchedRoomCount, Is.EqualTo(1));
            Assert.That(diagnostics.UnknownClauseTypeCount, Is.EqualTo(3),
                "ghost_type(has_type 1 条)+ ghost_type / ghost_neighbour(邻接两端各 1 条)= 3");
        }

        /// <summary>
        /// ⚠️ **与本 AC 相邻但语义不同**:格属性源返回 `Unknown`(该格没有任何实例占用)
        /// 是**源不完整**信号 ⇒ **硬失败**,与 `ClinicModifierEvaluator` 同一判据。
        /// 「子句类型未知」(上两条)= 布局里没有该类型 ⇒ 不成立,不抛。两者不得混为一谈。
        /// </summary>
        [Test]
        public void test_unknownCellLookup_throws_incompleteSource()
        {
            ClinicRoomExtractionResult room = Room(Cell(0, 0));
            var traits = new FakeTraits(
                Array.Empty<(WorldPos, string, int, long)>(),
                unknown: new[] { Cell(0, 0) });
            ClinicRoomTable table = Table(RoomDef("ward", MinCount("bed", 1)));

            bool threw = false;
            try
            {
                ClinicRoomMatcher.TryMatchRoom(room, traits, table, out _);
            }
            catch (InvalidOperationException)
            {
                threw = true;
            }

            Assert.That(threw, Is.True,
                "格属性 Unknown ⇒ 硬失败(静默跳过会让子句计数静默少算),与 ClinicModifierEvaluator 同判据");
        }

        /// <summary>非家具格(`NonFurniture`)不参与任何子句计数(非格 = 不计分、不连通)。</summary>
        [Test]
        public void test_nonFurnitureCell_excludedFromClauseCounts()
        {
            ClinicRoomExtractionResult room = Room(Cell(0, 0), Cell(1, 0));
            var traits = new FakeTraits(
                new[] { (Cell(0, 0), "bed", 0, 0L) },
                nonFurniture: new[] { Cell(1, 0) });
            ClinicRoomTable table = Table(RoomDef("ward", MinCount("bed", 2)));

            bool matched = ClinicRoomMatcher.TryMatchRoom(
                room, traits, table, out string roomType, out ClinicRoomMatchDiagnostics diagnostics);

            Assert.That(matched, Is.False, "非家具格不算 bed ⇒ 只有 1 张床 < 2");
            Assert.That(roomType, Is.Null);
            Assert.That(diagnostics.MatchedRoomCount, Is.EqualTo(0));
        }

        // ════════════════════════════════════════════════════════════════════
        // 输入面边界(卡面 §Implementation Notes 5)
        // ════════════════════════════════════════════════════════════════════

        /// <summary>公开签名**不接受** `ClinicAdjTable` / `ClinicContextTable` —— 邻接子句不复用 `adj_sum` 的权重查表。</summary>
        /// <remarks>卡面 §Implementation Notes 5 明令:「不得复用 `adj_sum` 的权重查表 ——
        /// 邻接子句问的是『有没有那个类型的邻居』,不是『权重和』」。
        /// 把它做成**类型层面不可表达**,而不是靠调用纪律。</remarks>
        [Test]
        public void test_signature_takesNoAdjTableOrContextTable()
        {
            var allowed = new HashSet<string>(StringComparer.Ordinal)
            {
                nameof(ClinicRoomExtractionResult),
                nameof(IClinicFurnitureTraitsSource),
                nameof(ClinicRoomTable),
                "String",
                nameof(ClinicRoomMatchDiagnostics),
            };

            MethodInfo[] overloads = typeof(ClinicRoomMatcher)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name == nameof(ClinicRoomMatcher.TryMatchRoom))
                .ToArray();

            Assert.That(overloads.Length, Is.EqualTo(2), "两档重载:带诊断面 / 不带诊断面");
            foreach (MethodInfo m in overloads)
            {
                Assert.That(m.ReturnType, Is.EqualTo(typeof(bool)), "输出形 = bool(false 即 ROOM_NONE)");
                foreach (ParameterInfo p in m.GetParameters())
                {
                    string name = p.ParameterType.Name.TrimEnd('&');   // 去掉 in / out 的 by-ref 后缀
                    Assert.That(allowed.Contains(name), Is.True,
                        $"参数 `{p.Name}` 的类型 `{name}` 不在白名单 —— 邻接子句不得复用 adj_sum 的 ClinicAdjTable");
                }
            }
        }
    }
}
