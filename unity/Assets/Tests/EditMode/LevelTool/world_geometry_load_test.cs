// 关卡工具 story-002 —— **世界几何 / 地形表**(导出 → 烘焙 → 运行期装载)的夹具。
//
// 权威来源:
//   `production/epics/level-tool/story-002-world-geometry-and-terrain.md`(AC-1 … AC-6)
//   ADR-022 §三(导出契约)· §二(唯一作者 = 关卡工具)· §四 C2(量化判据)
//   ADR-014 §二(两阶段烘焙 · `Fix` 写字符串 · 运行期零 JSON 解析器)· §五(data-core 单组预载)
//   GDD `world-and-ecozones.md` F-6-1(`LATTICE_SIZE`)/ F-6-2(`K_TERRAIN_MAX` / 空表硬失败)
//   · AC-6-08(每行三列 > 0;`K_speed == 1` 恰好一行)
//   AC-0 spike 实测件:`production/qa/evidence/spike-terrain-api-6.3-2026-10-29.md`
//
// ⚠️ **零写盘**:本件只读 `Assets/DataCooked/` 与仓内 `assets/data/`,不落任何文件
//    (`FsWriteBoundaryGate` 无关)。
// ⚠️ **零随机 / 零时间依赖**:所有「合成产物」都由 `BakeFromSourceText` 从**字面量 JSON** 现烘,
//    字节因此每次相同。
//
// ⚠️ 本件**不证**:C1 / C2 的比对实现(S6)· 视觉层地形是否存在(美术轮)·
//    `K_TERRAIN_MAX` 的**数值**是否正确(用户数值轮)。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using DaYiJingCheng.EditorTools.Bake;
using DaYiJingCheng.EditorTools.Level;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.Presentation;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;
using NUnit.Framework;
using UnityEngine;

namespace DaYiJingCheng.Tests.LevelTool
{
    /// <summary>story-002:世界几何 / 地形表的导出形状、烘焙确定性与运行期装载面。</summary>
    [TestFixture]
    public class world_geometry_load_test
    {
        private static readonly string RepoRoot = ComputeRepoRoot();

        private static string ComputeRepoRoot(
            [System.Runtime.CompilerServices.CallerFilePath] string thisFile = "")
            => Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(thisFile) ?? ".", "..", "..", "..", "..", ".."));

        private static string CookedDir => Path.Combine(RepoRoot, "unity", "Assets", "DataCooked");

        // ══════════════════════ AC-1:world_geometry 的导出形状 ══════════════════════

        /// <summary>AC-1:产出的 `world_geometry.json` 里**零浮点字面量**。</summary>
        /// <remarks><b>判别力</b>:让导出器把 `lattice_size_mm` 写成 `1000.0`(或任何小数)⇒ 本测红。
        /// 判据是**整份文本**扫 `\d+\.\d+`,不是只扫某个字段 —— 漏一个字段就漏一类浮点泄漏。</remarks>
        [Test]
        public void test_ac1_geometryExport_hasZeroFloatLiterals()
        {
            string json = LevelExporter.ExportGeometry(LevelCatalog.Define(P0ClinicLevel.LevelId));

            Assert.That(Regex.IsMatch(json, @"[0-9]+\.[0-9]+"), Is.False,
                "产物里出现了小数写法 —— ADR-015 §三 / AC-1 要求零浮点;实际产物:\n"
                + json.Substring(0, Math.Min(json.Length, 400)));
        }

        /// <summary>AC-1:尺寸 / `LATTICE_SIZE` / 每格可走性**逐项来自 `LevelSource`**,不是常量。</summary>
        /// <remarks><b>判别力</b>:导出器写死 `16 / 1 / 16` 或 `1000` ⇒ 本测红(用的是 4×2×4 / 777)。</remarks>
        [Test]
        public void test_ac1_geometryExport_sizeWalkableLatticeComeFromSource()
        {
            const int w = 4, h = 2, d = 4;
            var walkable = new int[w * h * d];
            for (int i = 0; i < walkable.Length; i++) walkable[i] = 1;
            walkable[Index(w, h, d, 0, 0, 0)] = 0;           // 单格不可走(证可走性来自数据)
            walkable[Index(w, h, d, 3, 1, 3)] = 0;

            LevelSource src = MakeSource("probe_geom", w, h, d, walkable, terrainRows: 1, latticeSizeMm: 777);

            string json = LevelExporter.ExportGeometry(src);

            Assert.That(json, Does.Contain("\"world_size\": [4, 2, 4]"), json);
            Assert.That(json, Does.Contain("\"lattice_size_mm\": 777"), json);
            Assert.That(json, Does.Not.Contain("\"world_size\": [16, 1, 16]"),
                "产物出现了 P0 医馆的尺寸 ⇒ 尺寸仍来自常量");

            int[] emitted = ExtractIntArray(json, "walkable");
            Assert.That(emitted.Length, Is.EqualTo(walkable.Length), "可走性数组长度须等于格总数");
            Assert.That(emitted, Is.EqualTo(walkable), "可走性须逐格等于源声明");
        }

        /// <summary>AC-1:未声明几何的关卡 ⇒ 导出**硬失败**,不合成「全可走」。</summary>
        /// <remarks><b>判别力</b>:把导出器改成「`Walkable == null` 就按格总数填 1」⇒ 本测红。
        /// 那条合成会让 AC-4 的「可走性来自产物」在任何夹具上都不可判别(同值变异)。</remarks>
        [Test]
        public void test_ac1_geometryExport_undeclaredWalkable_throws()
        {
            var src = new LevelSource("probe_no_geometry", 2, 1, 2, new List<SlotSpec>
            {
                new SlotSpec(1, 0, 1, SlotType.Decor, false),
            });

            var ex = Assert.Throws<InvalidOperationException>(() => LevelExporter.ExportGeometry(src));
            StringAssert.Contains("可走性", ex.Message);
        }

        // ══════════════════════ AC-2:world_terrain 的导出形状 ══════════════════════

        /// <summary>AC-2:`k_accel` / `k_decel` 在 JSON 里是**字符串**,不是 JSON 数字。</summary>
        /// <remarks><b>判别力</b>:导出器去掉引号(写 `"k_accel": 1`)⇒ 本测红。
        /// 判据用**带引号的字面量**做正向断言 + 用**不带引号**做反向断言 —— 只做后者会被
        /// `"k_accel": "1"` 里的子串骗过。</remarks>
        [Test]
        public void test_ac2_terrainExport_fixFieldsAreJsonStrings()
        {
            string json = LevelExporter.ExportTerrain(LevelCatalog.Define(P0ClinicLevel.LevelId));

            Assert.That(json, Does.Contain("\"k_accel\": \"1\""), json);
            Assert.That(json, Does.Contain("\"k_decel\": \"1\""), json);
            Assert.That(json, Does.Not.Contain("\"k_accel\": 1"), "`Fix` 写成了 JSON 数字(ADR-014 §二违例)");
            Assert.That(json, Does.Not.Contain("\"k_decel\": 1"), "同上");
        }

        /// <summary>AC-2:非整值的 `Fix` 写成「分子/分母」且能被 `FixParse` **逐位**读回。</summary>
        /// <remarks><b>判别力</b>:把 `FixLiteral` 改成写小数(`"0.75"`)⇒ `FixParse` 抛 ⇒ 本测红。</remarks>
        [Test]
        public void test_ac2_terrainExport_nonIntegerFix_roundTripsThroughFixParse()
        {
            const int w = 2, h = 1, d = 2;
            Fix accel = Fix.FromRational(3, 4);
            Fix decel = Fix.FromRational(7, 8);
            LevelSource src = MakeSource("probe_terrain", w, h, d, AllWalkable(w, h, d),
                terrainRows: 1, latticeSizeMm: 1000, accel: accel, decel: decel);

            string json = LevelExporter.ExportTerrain(src);

            string accelLiteral = ExtractStringField(json, "k_accel");
            string decelLiteral = ExtractStringField(json, "k_decel");
            Assert.That(FixParse.Parse(accelLiteral).Raw, Is.EqualTo(accel.Raw),
                $"\"{accelLiteral}\" 读回的不是原值 —— 往返必须逐位无损");
            Assert.That(FixParse.Parse(decelLiteral).Raw, Is.EqualTo(decel.Raw));
        }

        /// <summary>AC-2:未声明地形的关卡 ⇒ 导出**硬失败**(F-6-2:max over 空集无定义)。</summary>
        [Test]
        public void test_ac2_terrainExport_undeclaredTerrain_throws()
        {
            var src = new LevelSource("probe_no_terrain", 2, 1, 2, new List<SlotSpec>
            {
                new SlotSpec(1, 0, 1, SlotType.Decor, false),
            }, walkable: AllWalkable(2, 1, 2), latticeSizeMm: 1000);

            var ex = Assert.Throws<InvalidOperationException>(() => LevelExporter.ExportTerrain(src));
            StringAssert.Contains("TERRAIN_TABLE", ex.Message);
        }

        // ══════════════════════ AC-3:烘焙确定性 + data-core 登记 ══════════════════════

        /// <summary>AC-3:同一份源文本两次烘焙 ⇒ **逐位一致**(ADR-014 §二)。</summary>
        [Test]
        public void test_ac3_bakeTwice_isBitIdentical()
        {
            string geomJson = File.ReadAllText(
                Path.Combine(RepoRoot, "assets", "data", WorldGeometryBake.FileName));
            string terrJson = File.ReadAllText(
                Path.Combine(RepoRoot, "assets", "data", WorldTerrainBake.FileName));

            Assert.That(WorldGeometryBake.BakeFromSourceText(geomJson).Cooked,
                Is.EqualTo(WorldGeometryBake.BakeFromSourceText(geomJson).Cooked),
                "world_geometry 两次烘焙字节不同 —— 确定性被破坏(字典迭代序 / 本地时间 / 区域性)");
            Assert.That(WorldTerrainBake.BakeFromSourceText(terrJson).Cooked,
                Is.EqualTo(WorldTerrainBake.BakeFromSourceText(terrJson).Cooked),
                "world_terrain 两次烘焙字节不同");
        }

        /// <summary>AC-3:两份产物的 address 都登记在 `data-core` 预载表里,且盘上有产物。</summary>
        /// <remarks><b>判别力</b>:把某一条从 `DataCorePreloader.DataCoreAddresses` 删掉 ⇒ 本测红。
        /// 注意 `DataCoreAddresses` 是 `internal` ⇒ 走反射(与 `world_slots_provider_wiring_test`
        /// 的注入缝取法同源)。
        /// <para>⚠️ 本条**不**证明 catalog 里查得到(那需要真 Addressables locator);
        /// 「组条目数 +2」的实跑证据在 `production/qa/evidence/impl-level-tool-story-002-*.md`。</para></remarks>
        [Test]
        public void test_ac3_bothAddressesAreInDataCorePreloadList_andProductsExist()
        {
            FieldInfo field = typeof(DataCorePreloader).GetField(
                "DataCoreAddresses", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.That(field, Is.Not.Null, "`DataCorePreloader.DataCoreAddresses` 不见了 —— 预载表被改名/删除");
            var addresses = (string[])field.GetValue(null);

            Assert.That(addresses, Has.Member(AddressablesDataProvider.WorldGeometryAddress));
            Assert.That(addresses, Has.Member(AddressablesDataProvider.WorldTerrainAddress));

            Assert.That(File.Exists(Path.Combine(CookedDir, WorldGeometryDataSet.CookedFileName)), Is.True,
                "盘上缺 world_geometry 产物 —— 先跑「烘焙 world_geometry」菜单");
            Assert.That(File.Exists(Path.Combine(CookedDir, WorldTerrainDataSet.CookedFileName)), Is.True,
                "盘上缺 world_terrain 产物 —— 先跑「烘焙 world_terrain」菜单");
        }

        // ══════════════════════ AC-4:运行期装载面 ══════════════════════

        /// <summary>AC-4:几何的尺寸 / 可走性**来自产物** —— 换一份 4×1×4 的产物,世界就是 4×1×4。</summary>
        /// <remarks><b>判别力</b>:把 `MinimalWorldFactory` 退回「按常量造 16×1×16 全可走」⇒ 三条断言
        /// (尺寸 / 该格可走 / 该格不可走)全红。这是本件**最强**的一条:注入的产物与任何常量都不同值。</remarks>
        [Test]
        public void test_ac4_geometryComesFromArtifact_notFromConstants()
        {
            const int w = 4, h = 1, d = 4;
            var walkable = AllWalkable(w, h, d);
            walkable[Index(w, h, d, 0, 0, 0)] = 0;

            byte[] geom = WorldGeometryBake.BakeFromSourceText(GeometryJson("probe_small", w, h, d, walkable)).Cooked;
            byte[] terr = WorldTerrainBake.BakeFromSourceText(
                TerrainJson("probe_small", w, h, d, rows: 1, perCell: _ => 0)).Cooked;
            byte[] slots = WorldBuildSlotsBake.BakeFromSourceText(SlotsJson("probe_small", w, h, d)).Cooked;

            World world = MinimalWorldFactory.Create(MakeProvider(geom, terr, slots));

            Assert.That(world.Geometry.SizeX, Is.EqualTo(w), "世界 X 尺寸须等于产物声明的 4(而非常量 16)");
            Assert.That(world.Geometry.SizeY, Is.EqualTo(h));
            Assert.That(world.Geometry.SizeZ, Is.EqualTo(d));
            Assert.That(world.IsEffectivelyWalkable(new WorldPos(1, 0, 1)), Is.True,
                "产物里 (1,0,1) 声明为 1 ⇒ 须可走");
            Assert.That(world.IsEffectivelyWalkable(new WorldPos(0, 0, 0)), Is.False,
                "产物里 (0,0,0) 声明为 0 ⇒ 须不可走(全可走的合成世界会在这里露馅)");
        }

        /// <summary>AC-4(真产物):盘上那份 `world_geometry` 的可走性**不是全 true**。</summary>
        /// <remarks><b>判别力</b>:把 `P0ClinicLevel` 的外墙环改回全可走 ⇒ 本测红。
        /// 该判据就是故事卡「不是 16×1×16 全 true」那一句的字面兑现。</remarks>
        [Test]
        public void test_ac4_realProduct_walkabilityIsNotAllTrue()
        {
            WorldGeometryDataSet geo = DataCookedDisk.LoadWorldGeometry();

            int walkableCount = 0;
            for (int i = 0; i < geo.Walkable.Length; i++) if (geo.Walkable[i] != 0) walkableCount++;

            Assert.That(walkableCount, Is.GreaterThan(0), "全不可走 —— Binder / codec 的域门本应拦住");
            Assert.That(walkableCount, Is.LessThan(geo.Walkable.Length),
                "全可走 ⇒ 与旧的合成世界同形,「可走性来自产物」失去观察面(AC-4)");
            Assert.That(geo.SizeX, Is.EqualTo(16));
            Assert.That(geo.SizeY, Is.EqualTo(1));
            Assert.That(geo.SizeZ, Is.EqualTo(16));
        }

        // ══════════════════════ AC-5:空 TERRAIN_TABLE = 装载期硬失败 ══════════════════════

        /// <summary>AC-5:空表产物 ⇒ 装载**抛**,且**抛之前下游零调用**(槽位装载一次都没被请求)。</summary>
        /// <remarks>
        /// <para><b>判别力</b>:把装载改成「空表 ⇒ 按全 walkable 继续」⇒ 第一断言红(不抛);
        /// 把 terrain 加载挪到槽位之后 ⇒ 第二断言红(槽位已被请求)。</para>
        /// <para>⚠️ 判据**不是**只断异常类型 —— 只断类型会被下层同类型异常冒充
        /// (承 `feedback_fail_fast_needs_sideeffect_assert`)。</para>
        /// </remarks>
        [Test]
        public void test_ac5_emptyTerrainTable_throwsBeforeAnySlotLoad()
        {
            const int w = 2, h = 1, d = 2;
            byte[] geom = WorldGeometryBake.BakeFromSourceText(
                GeometryJson("probe_empty_terrain", w, h, d, AllWalkable(w, h, d))).Cooked;
            byte[] slots = WorldBuildSlotsBake.BakeFromSourceText(
                SlotsJson("probe_empty_terrain", w, h, d)).Cooked;

            var requested = new List<string>();
            AddressablesDataProvider provider = MakeProvider(address =>
            {
                requested.Add(address);
                if (address == AddressablesDataProvider.WorldGeometryAddress) return geom;
                if (address == AddressablesDataProvider.WorldBuildSlotsAddress) return slots;
                if (address == AddressablesDataProvider.WorldTerrainAddress)
                    throw new InvalidDataException(
                        "world_terrain.cooked.bytes row_count = 0 —— TERRAIN_TABLE 不得为空");
                throw new InvalidOperationException($"本夹具不该请求 {address}");
            });

            // ⚠️ 期望的是 `InvalidOperationException` 而不是 `InvalidDataException` ——
            //    `AddressablesDataProvider.FetchBytes` 会把**注入字节源**抛出的异常包一层
            //    `[E-13] … 注入字节源抛出`(实测:首跑按 `InvalidDataException` 断言 ⇒ 红)。
            //    ⇒ 判据改成「外层是 E-13 包装 + 内层是真因 + 报文点名 TERRAIN_TABLE」,
            //    比只断类型更严:它连「包装把真因吃掉」也一并钉住。
            var ex = Assert.Throws<InvalidOperationException>(() => MinimalWorldFactory.Create(provider));
            StringAssert.Contains("TERRAIN_TABLE", ex.Message, "报文须点名真因(空表)");
            Assert.That(ex.InnerException, Is.InstanceOf<InvalidDataException>(),
                "内层须仍是装载链的真因类型(不是被包装层换掉的新异常)");

            Assert.That(requested, Has.No.Member(AddressablesDataProvider.WorldBuildSlotsAddress),
                "地形表已失败却仍去装了槽位 —— 装载序把可失败项排在了不可失败项之后," +
                "现场会看到「槽位目录正常但世界不存在」的错位状态");
        }

        /// <summary>AC-5(第二道门):**读侧 codec** 独立拒空表,不依赖烘焙侧。</summary>
        /// <remarks><b>判别力</b>:删掉 `WorldTerrainCookedCodec.Read` 的 `rowCount &lt; 1` 门 ⇒ 本测红
        /// (手工构造的 0 行字节会被静默读成「零行地形」)。</remarks>
        [Test]
        public void test_ac5_codecRejectsZeroRowTerrainPayload()
        {
            const int w = 2, h = 1, d = 2;
            byte[] payload;
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write(0);        // row_count = 0  ← 本测的靶
                bw.Write(w); bw.Write(h); bw.Write(d);
                bw.Write(w * h * d);
                for (int i = 0; i < w * h * d; i++) bw.Write(0);
                bw.Write(4500);     // slope_limit_deg_x100
                bw.Write(300);      // step_offset_mm
                bw.Flush();
                payload = ms.ToArray();
            }
            byte[] cooked = WithHeader(payload, WorldTerrainCookedCodec.ExpectedSchemaVersion);

            var ex = Assert.Throws<InvalidDataException>(() => WorldTerrainCookedCodec.Read(cooked));
            StringAssert.Contains("TERRAIN_TABLE", ex.Message);
        }

        // ══════════════════════ AC-6:KSpeedIdx 有消费者 ══════════════════════

        /// <summary>AC-6:两行地形表 ⇒ 每格 `KSpeedIdx` 等于产物里编码的行下标(不是恒定 0)。</summary>
        /// <remarks><b>判别力</b>:把工厂改成「一律写 `new TerrainCell(walkable, 0)`」⇒ 本测红。</remarks>
        [Test]
        public void test_ac6_loadedTerrainCells_carryEncodedSpeedIndex()
        {
            const int w = 3, h = 1, d = 3;
            byte[] geom = WorldGeometryBake.BakeFromSourceText(
                GeometryJson("probe_speed", w, h, d, AllWalkable(w, h, d))).Cooked;
            // (0,0,0) 用 terrain_id 1(第二行);其余用 0。
            byte[] terr = WorldTerrainBake.BakeFromSourceText(
                TerrainJson("probe_speed", w, h, d, rows: 2,
                    perCell: p => p.X == 0 && p.Z == 0 ? 1 : 0)).Cooked;
            byte[] slots = WorldBuildSlotsBake.BakeFromSourceText(
                SlotsJson("probe_speed", w, h, d)).Cooked;

            World world = MinimalWorldFactory.Create(MakeProvider(geom, terr, slots));

            Assert.That(world.Geometry.GetTerrain(new WorldPos(0, 0, 0)).KSpeedIdx, Is.EqualTo(1),
                "该格在产物里指向 terrain_id 1(= 表内下标 1)");
            Assert.That(world.Geometry.GetTerrain(new WorldPos(1, 0, 1)).KSpeedIdx, Is.EqualTo(0));
        }

        /// <summary>AC-6:越界 `KSpeedIdx` 被**量程校验**拒掉(而不是留到速度求值时错行查表)。</summary>
        /// <remarks><b>判别力</b>:删掉 `ValidateTerrainSpeedIndices` 的两个上/下界判据 ⇒ 本测红。
        /// <para>⚠️ 生产数据打不到这条分支(读侧 codec 已先拒越界下标)—— 这正是它 **`public`** 的理由:
        /// 若藏成 `private`,这份判据就是一条无人能证伪的断言(承 `IsRecoverableSlotLoadFailure` 同款)。</para></remarks>
        [Test]
        public void test_ac6_outOfRangeSpeedIndex_isRejected()
        {
            var ok = new TerrainCell[1, 1, 2];
            ok[0, 0, 0] = new TerrainCell(true, 0);
            ok[0, 0, 1] = new TerrainCell(true, 1);
            Assert.DoesNotThrow(() => MinimalWorldFactory.ValidateTerrainSpeedIndices(ok, 2),
                "前置:合法下标须放行(否则本测无法区分「拒得对」与「什么都拒」)");

            var tooBig = new TerrainCell[1, 1, 1];
            tooBig[0, 0, 0] = new TerrainCell(true, 2);
            var big = Assert.Throws<InvalidDataException>(
                () => MinimalWorldFactory.ValidateTerrainSpeedIndices(tooBig, 2));
            StringAssert.Contains("KSpeedIdx", big.Message);

            var negative = new TerrainCell[1, 1, 1];
            negative[0, 0, 0] = new TerrainCell(true, -1);
            Assert.Throws<InvalidDataException>(
                () => MinimalWorldFactory.ValidateTerrainSpeedIndices(negative, 2));

            var zeroRows = new TerrainCell[1, 1, 1];
            zeroRows[0, 0, 0] = new TerrainCell(true, 0);
            Assert.Throws<InvalidDataException>(
                () => MinimalWorldFactory.ValidateTerrainSpeedIndices(zeroRows, 0),
                "行数 0 ⇒ `max over 空集无定义`,须拒");
        }

        // ══════════════════════ 夹具工具 ══════════════════════

        /// <summary>`(x*H + y)*D + z` —— 与 `LevelSource.CellIndex` / `WorldGeometry.ToIndex` 同式。</summary>
        private static int Index(int w, int h, int d, int x, int y, int z) => (x * h + y) * d + z;

        private static int[] AllWalkable(int w, int h, int d)
        {
            var a = new int[w * h * d];
            for (int i = 0; i < a.Length; i++) a[i] = 1;
            return a;
        }

        /// <summary>造一个声明了完整几何的关卡源(单行 / 双行地形表按 <paramref name="terrainRows"/>)。</summary>
        private static LevelSource MakeSource(
            string levelId, int w, int h, int d, int[] walkable,
            int terrainRows, int latticeSizeMm, Fix? accel = null, Fix? decel = null)
        {
            int total = w * h * d;
            var rows = new List<TerrainRowSpec>();
            for (int i = 0; i < terrainRows; i++)
                rows.Add(new TerrainRowSpec(
                    terrainId: i,
                    name: "terrain_" + i,
                    kSpeed: i == 0 ? 1 : 2,          // AC-6-08:恰好一行 k_speed == 1
                    kAccel: accel ?? Fix.FromRational(1, 1),
                    kDecel: decel ?? Fix.FromRational(1, 1)));

            var cellTerrainId = new int[total];      // 全 0
            var terrain = new TerrainSource(
                rows, cellTerrainId, P0ClinicLevel.SlopeLimitDegX100, P0ClinicLevel.StepOffsetMm, total);

            return new LevelSource(levelId, w, h, d, new List<SlotSpec>
            {
                new SlotSpec(w / 2, 0, d / 2, SlotType.Decor, false),
            }, walkable: walkable, terrain: terrain, latticeSizeMm: latticeSizeMm);
        }

        private static string GeometryJson(string levelId, int w, int h, int d, int[] walkable)
            => "{\"schema_version\":1,\"level_id\":\"" + levelId + "\",\"world_size\":[" + w + "," + h + "," + d
             + "],\"lattice_size_mm\":1000,\"walkable\":[" + string.Join(",", walkable) + "]}";

        /// <summary>造一份 `world_terrain` 源文本(<paramref name="rows"/> 行,逐格 id 由 <paramref name="perCell"/> 给)。</summary>
        private static string TerrainJson(
            string levelId, int w, int h, int d, int rows, Func<(int X, int Y, int Z), int> perCell)
        {
            var table = new List<string>();
            for (int i = 0; i < rows; i++)
                table.Add("{\"terrain_id\":" + i + ",\"name\":\"t" + i + "\",\"k_speed\":" + (i == 0 ? 1 : 2)
                          + ",\"k_accel\":\"1\",\"k_decel\":\"1\"}");

            var cells = new List<int>();
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    for (int z = 0; z < d; z++) cells.Add(perCell((x, y, z)));

            return "{\"schema_version\":1,\"level_id\":\"" + levelId + "\",\"world_size\":[" + w + "," + h + "," + d
                 + "],\"terrain_table\":[" + string.Join(",", table) + "],\"cell_terrain_id\":["
                 + string.Join(",", cells) + "],\"slope_limit_deg_x100\":4500,\"step_offset_mm\":300}";
        }

        private static string SlotsJson(string levelId, int w, int h, int d)
            => "{\"schema_version\":2,\"level_id\":\"" + levelId + "\",\"world_size\":[" + w + "," + h + "," + d
             + "],\"slots\":[{\"cell\":[" + (w / 2) + ",0," + (d / 2)
             + "],\"slot_type\":\"Decor\",\"baked_initial\":false,\"module_id\":null}]}";

        /// <summary>拼一个 20 字节头(格式与 `ClinicMachineCookedWriter.FinishHeader` 逐字节一致)。</summary>
        private static byte[] WithHeader(byte[] payload, uint schemaVersion)
        {
            var output = new byte[20 + payload.Length];
            output[0] = (byte)'D'; output[1] = (byte)'Y'; output[2] = (byte)'J'; output[3] = (byte)'C';
            WriteU32(output, 4, 1u);                 // CookedFormat.FormatVersion
            WriteU32(output, 8, schemaVersion);
            WriteU32(output, 12, 0u);                // configVersion(本测不关心)
            WriteU32(output, 16, (uint)payload.Length);
            Buffer.BlockCopy(payload, 0, output, 20, payload.Length);
            return output;
        }

        private static void WriteU32(byte[] b, int offset, uint value)
        {
            b[offset] = (byte)(value & 0xFF);
            b[offset + 1] = (byte)((value >> 8) & 0xFF);
            b[offset + 2] = (byte)((value >> 16) & 0xFF);
            b[offset + 3] = (byte)((value >> 24) & 0xFF);
        }

        /// <summary>按 address 供字节;未列出的 address ⇒ 明确失败(口径同 `world_slots_provider_wiring_test`)。</summary>
        private static AddressablesDataProvider MakeProvider(byte[] geom, byte[] terr, byte[] slots)
            => MakeProvider(address =>
            {
                if (address == AddressablesDataProvider.WorldGeometryAddress) return geom;
                if (address == AddressablesDataProvider.WorldTerrainAddress) return terr;
                if (address == AddressablesDataProvider.WorldBuildSlotsAddress) return slots;
                throw new InvalidOperationException($"本夹具只供世界三件产物,不该请求 {address}");
            });

        /// <summary>经内部构造缝造 provider(internal ⇒ 反射;先例 `world_slots_provider_wiring_test`)。</summary>
        private static AddressablesDataProvider MakeProvider(Func<string, byte[]> byteSource)
        {
            ConstructorInfo ctor = typeof(AddressablesDataProvider).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(Func<string, byte[]>) },
                modifiers: null);

            Assert.That(ctor, Is.Not.Null,
                "`AddressablesDataProvider(Func<string, byte[]>)` 内部构造不见了 —— 装载面夹具的注入缝被移除");

            return (AddressablesDataProvider)ctor.Invoke(new object[] { byteSource });
        }

        /// <summary>从导出 JSON 里抽出某个一维整数数组(导出器只产出这一种形状,故用最小扫描)。</summary>
        private static int[] ExtractIntArray(string json, string key)
        {
            string marker = "\"" + key + "\": [";
            int start = json.IndexOf(marker, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), $"产物里找不到 {key} 数组:\n{json}");
            start += marker.Length;
            int end = json.IndexOf(']', start);
            Assert.That(end, Is.GreaterThan(start), $"`{key}` 数组未闭合");

            string body = json.Substring(start, end - start);
            return body.Split(',')
                       .Select(s => s.Trim())
                       .Where(s => s.Length > 0)
                       .Select(int.Parse)
                       .ToArray();
        }

        /// <summary>从导出 JSON 里抽出某个字符串字段的值(去引号)。</summary>
        private static string ExtractStringField(string json, string key)
        {
            var m = Regex.Match(json, "\"" + key + "\":\\s*\"([^\"]*)\"");
            Assert.That(m.Success, Is.True, $"产物里找不到字符串字段 {key}:\n{json}");
            return m.Groups[1].Value;
        }
    }
}
