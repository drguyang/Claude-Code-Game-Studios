// 权威来源:ADR-014 §二/§四/§五(作者态 JSON → 烘焙 → cooked → 运行期只读)
//          GDD design/gdd/clinic-machine.md `:161-162`(S 成员判据 `type(c) ∈ FURN_TYPES`)·
//          `:188`(`e_env ∈ [−C_max, C_max]`)· `:297`(FURN_TABLE 归 21a)
//          O-24-1 / TD-clinic-01 / 路线图 §三 G-8
//
// ⚠️ **本件补的是 O-24-1 的判据面**:`FURN_TABLE` 从「双侧登记、零实现」变成
//   「作者态 JSON → 烘焙门 → cooked → 运行期表 → 生产属性源」的**可执行链**。
//   在此之前,`IClinicFurnitureTraitsSource` 只有测试注入的夹具源,
//   `ClinicModifierEvaluator.FurnTraitsProductionReady` 是 false 哨兵。
//
// ⚠️ 分工:
//   烘焙期拒收(域门)→ 本件 §一(直驱 Binder,不落盘)
//   读方 ↔ 写方镜像     → 本件 §二(cooked 字节 → 运行期表)
//   运行期三值查询       → 本件 §三(生产源 `ClinicFurnitureTraitsSource`)
//   真判据析出           → 本件 §四(`ClinicRoomExtraction.Extract(view, furnTypes)`)
//
// ⚠️ 测试不写盘(承 `.claude/rules/test-standards.md`)。本件只**读**已入库产物 + 内存内现烘。

using System;
using System.Collections.Generic;
using System.IO;
using DaYiJingCheng.EditorTools.Bake;
using DaYiJingCheng.Gameplay.Presentation;
using DaYiJingCheng.Sim.Clinic;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Unit.ClinicMachine
{
    /// <summary>21a 家具目录(<c>FURN_TABLE</c>)的四段判据:烘焙门 / 读方镜像 / 三值查询 / 真判据析出。</summary>
    [TestFixture]
    public class ClinicFurnitureCatalogueTest
    {
        private static readonly string RepoRoot = ComputeRepoRoot();

        private static string ComputeRepoRoot(
            [System.Runtime.CompilerServices.CallerFilePath] string thisFile = "")
            => Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(thisFile) ?? ".", "..", "..", "..", "..", ".."));

        private static string CookedDir
            => Path.Combine(RepoRoot, "unity", "Assets", "DataCooked");

        private static byte[] ReadCommitted()
        {
            string path = Path.Combine(CookedDir, DataBakeMenu.FurnitureCatalogueCookedAssetName);
            Assert.That(File.Exists(path), Is.True,
                $"盘上产物缺失:{path} —— 重烘:菜单「大医精诚/数据管线/烘焙 item-database 家具目录(21a · FURN_TABLE)」");
            return File.ReadAllBytes(path);
        }

        /// <summary>最小合法源(7 行之一;逐测按需改字段)。</summary>
        private static string SourceWith(params string[] rows)
            => "{\"schema_version\":1,\"tier_max\":3,\"furn\":[" + string.Join(",", rows) + "]}";

        private static string Row(string type, int tier, string eEnv)
            => $"{{\"type\":\"{type}\",\"tier\":{tier},\"e_env\":\"{eEnv}\"}}";

        private static void AssertRejected(string json, string expectedFragment)
        {
            var ex = Assert.Throws<BakeValidationException>(
                () => ItemDatabaseFurnitureBake.BakeFromSourceText(json),
                "该源须在烘焙期被拒(AC-24-08:字段缺失 / 越界 = 构建失败)");
            Assert.That(string.Join(" | ", ex.Errors), Does.Contain(expectedFragment),
                $"拒收报文须点名「{expectedFragment}」—— 否则作者只看到「烘焙失败」,不知道改哪");
        }

        // ══════════════════ 一 · 烘焙期域门(AC-24-08)══════════════════

        /// <summary>正向:仓里的真种子必须烘得出来(零错误),且闭集非空。</summary>
        /// <remarks><b>判别力</b>:种子被改坏(如把 `e_env` 写成 JSON 数字而非字符串)⇒ 本测红。</remarks>
        [Test]
        public void test_furniture_seedSource_bakesCleanly()
        {
            ItemDatabaseFurnitureBake.BakeOutput output = ItemDatabaseFurnitureBake.BakeFromRepo(RepoRoot);

            Assert.That(output.Bound.Rows.Count, Is.GreaterThan(0),
                "FURN_TYPES 闭集为空 ⇒ 全部房间谓词恒不匹配(整馆恒 ROOM_NONE)");
            Assert.That(output.Bound.TierMax, Is.EqualTo(ClinicMachineSchema.TierMax),
                "tier_max 与 24 侧 TIER_MAX 不一致 ⇒ 两侧档位域分叉");
            Assert.That(output.Cooked.Length, Is.GreaterThan(CookedFormat.HeaderSize));
        }

        /// <summary>`type` 缺失 ⇒ 拒。</summary>
        [Test]
        public void test_furniture_missingType_isRejected()
            => AssertRejected(SourceWith("{\"tier\":1,\"e_env\":\"1/64\"}"), "type");

        /// <summary>`tier` 缺失 ⇒ 拒(**不是**静默当 0 —— 0 是合法值「无档」)。</summary>
        /// <remarks><b>判别力</b>:把 Binder 的 `TryReadTier` 缺失分支改成 `return true; tier = 0`
        /// ⇒ 本测红。这正是「作者打错字」与「这件家具无档」两种情形的分界。</remarks>
        [Test]
        public void test_furniture_missingTier_isRejected()
            => AssertRejected(SourceWith("{\"type\":\"bed\",\"e_env\":\"1/64\"}"), "tier");

        /// <summary>`tier` 越出 `[0, tier_max]` ⇒ 拒。</summary>
        [Test]
        public void test_furniture_tierAboveMax_isRejected()
            => AssertRejected(SourceWith(Row("bed", 4, "1/64")), "tier_max");

        /// <summary>`tier` 为负 ⇒ 拒。</summary>
        [Test]
        public void test_furniture_negativeTier_isRejected()
            => AssertRejected(SourceWith(Row("bed", -1, "1/64")), "tier_max");

        /// <summary>`e_env` 写成 JSON 数字 ⇒ 拒(ADR-014 §四:Fix 字段必须是字符串)。</summary>
        [Test]
        public void test_furniture_eEnvAsJsonNumber_isRejected()
            => AssertRejected(SourceWith("{\"type\":\"bed\",\"tier\":1,\"e_env\":0.015625}"), "e_env");

        /// <summary>`|e_env| > C_max = 1/64` ⇒ 拒(OQ-24-6 的耦合 `n·C_max ≤ 1`)。</summary>
        /// <remarks><b>判别力</b>:停用 Binder 的 `EnvContributionMax` 比较 ⇒ 本测红。</remarks>
        [Test]
        public void test_furniture_eEnvBeyondCMax_isRejected()
            => AssertRejected(SourceWith(Row("bed", 1, "1/32")), "C_max");

        /// <summary>`type` 重复 ⇒ 拒(重复使闭集大小失真)。</summary>
        [Test]
        public void test_furniture_duplicateType_isRejected()
            => AssertRejected(SourceWith(Row("bed", 1, "1/64"), Row("bed", 2, "1/64")), "重复");

        /// <summary>`tier_max` 与 24 侧 `TIER_MAX` 不一致 ⇒ 拒。</summary>
        [Test]
        public void test_furniture_tierMaxMismatch_isRejected()
        {
            string json = "{\"schema_version\":1,\"tier_max\":4,\"furn\":[" + Row("bed", 1, "1/64") + "]}";
            AssertRejected(json, "tier_max");
        }

        /// <summary>空 `furn[]` ⇒ 拒(空闭集让全部房间谓词恒不匹配)。</summary>
        [Test]
        public void test_furniture_emptyTable_isRejected()
            => AssertRejected("{\"schema_version\":1,\"tier_max\":3,\"furn\":[]}", "furn");

        // ══════════════════ 二 · 读方 ↔ 写方镜像 ══════════════════

        /// <summary>盘上字节与现烘字节解码后逐行相等(含 `tier_max` / 行序 / 逐件属性)。</summary>
        /// <remarks><b>判别力</b>:读方字段序错位(如把 `tier` 与 `e_env_raw` 次序弄反)⇒
        /// 行的 tier / e_env 不等;写方排序被去掉 ⇒ 行序在作者重排后漂移。</remarks>
        [Test]
        public void test_furniture_diskMatchesFreshBake()
        {
            ClinicFurnitureDataSet disk = ClinicFurnitureCookedCodec.Read(ReadCommitted());
            ClinicFurnitureDataSet fresh = ClinicFurnitureCookedCodec.Read(
                ItemDatabaseFurnitureBake.BakeFromRepo(RepoRoot).Cooked);

            Assert.That(disk.ConfigVersion, Is.EqualTo(fresh.ConfigVersion), "ConfigVersion 不一致");
            Assert.That(disk.Catalogue.TierMax, Is.EqualTo(fresh.Catalogue.TierMax));
            Assert.That(disk.Catalogue.Count, Is.EqualTo(fresh.Catalogue.Count), "行数不一致");

            for (int i = 0; i < disk.Catalogue.Count; i++)
            {
                ClinicFurnitureEntry a = disk.Catalogue.Rows[i];
                ClinicFurnitureEntry b = fresh.Catalogue.Rows[i];
                Assert.That(a.Type, Is.EqualTo(b.Type), $"第 {i} 行 type 不一致");
                Assert.That(a.Tier, Is.EqualTo(b.Tier), $"「{a.Type}」tier 不一致");
                Assert.That(a.EEnv.Raw, Is.EqualTo(b.EEnv.Raw), $"「{a.Type}」e_env raw 不一致");
            }
        }

        /// <summary>行序 = `type` 的序数升序(声明序不承载语义)。</summary>
        /// <remarks><b>判别力</b>:去掉写方的 `rows.Sort(CompareType)` ⇒ 本测红(作者重排即漂移)。</remarks>
        [Test]
        public void test_furniture_rowOrder_isOrdinalSorted()
        {
            ClinicFurnitureDataSet disk = ClinicFurnitureCookedCodec.Read(ReadCommitted());
            for (int i = 1; i < disk.Catalogue.Count; i++)
                Assert.That(string.CompareOrdinal(
                        disk.Catalogue.Rows[i - 1].Type, disk.Catalogue.Rows[i].Type),
                    Is.LessThan(0),
                    $"第 {i - 1} 行「{disk.Catalogue.Rows[i - 1].Type}」不小于第 {i} 行" +
                    $"「{disk.Catalogue.Rows[i].Type}」—— 写方排序被去掉或比较器改坏");
        }

        /// <summary>同源两次烘焙逐位一致(ADR-014 §二)。</summary>
        [Test]
        public void test_furniture_bakeIsDeterministic()
        {
            byte[] a = ItemDatabaseFurnitureBake.BakeFromRepo(RepoRoot).Cooked;
            byte[] b = ItemDatabaseFurnitureBake.BakeFromRepo(RepoRoot).Cooked;
            Assert.That(a, Is.EqualTo(b), "同源两次烘焙字节不等 ⇒ 有字典迭代序 / 本地时间进了产物");
        }

        /// <summary>闭集查询:真种子里的 7 个类型全部在 `FURN_TYPES` 内,未知类型不在。</summary>
        [Test]
        public void test_furniture_catalogueContainsSeedTypes_notUnknownType()
        {
            ClinicFurnitureCatalogue c = ClinicFurnitureCookedCodec.Read(ReadCommitted()).Catalogue;

            foreach (string t in new[]
                     {
                         "bed", "surgery_table", "sterile_cabinet", "herb_cabinet",
                         "grinder", "drying_rack", "chamber_pot",
                     })
                Assert.That(c.Contains(t), Is.True, $"「{t}」须在 FURN_TYPES 闭集内");

            Assert.That(c.Contains("wall"), Is.False, "非家具类型不得混进 FURN_TYPES");
            Assert.That(c.Contains(null), Is.False);
            Assert.That(c.FurnTypes.Count, Is.EqualTo(c.Count), "FurnTypes 与行数不等 ⇒ 闭集视图不全");
        }

        /// <summary>`C_max` 两侧同值(烘焙期 Binder ↔ 运行期表)。</summary>
        /// <remarks><b>判别力</b>:任一侧改 `1/64` 为别的值 ⇒ 本测红。
        /// 两处分居两层(编辑期 / 运行期),本测是它们唯一的等值钉。</remarks>
        [Test]
        public void test_furniture_cMax_matchesBinder()
        {
            Assert.That(ClinicFurnitureCatalogue.EnvContributionMaxRaw,
                Is.EqualTo(ItemDatabaseFurnitureBinder.EnvContributionMax.Raw),
                "C_max 在烘焙期 Binder 与运行期表两处不一致 ⇒ 同一域两套界");
        }

        /// <summary>表构造拒收:绕过 Binder 直构(产物被改 / 手搓表)也拦得住。</summary>
        [Test]
        public void test_furniture_catalogue_rejectsOutOfDomainRow()
        {
            var tooBig = new[] { new ClinicFurnitureEntry("bed", 1, new Fix(Fix.OneRaw / 32)) };
            Assert.Throws<ArgumentException>(() => new ClinicFurnitureCatalogue(tooBig, 3),
                "|e_env| > C_max 的运行期表须被拒(Sim 侧纵深防御)");

            var badTier = new[] { new ClinicFurnitureEntry("bed", 4, Fix.Zero) };
            Assert.Throws<ArgumentException>(() => new ClinicFurnitureCatalogue(badTier, 3));

            Assert.Throws<ArgumentException>(
                () => new ClinicFurnitureCatalogue(Array.Empty<ClinicFurnitureEntry>(), 3),
                "空表须被拒");
        }

        /// <summary>`Rows` / `FurnTypes` 不得是裸数组(可被强转后就地改写)。</summary>
        [Test]
        public void test_furniture_catalogue_viewsNotCastableToArray()
        {
            ClinicFurnitureCatalogue c = ClinicFurnitureCookedCodec.Read(ReadCommitted()).Catalogue;
            Assert.That(c.Rows, Is.Not.InstanceOf<ClinicFurnitureEntry[]>(),
                "Rows 暴露裸数组 ⇒ 调用方可强转后就地改写");
            Assert.That(c.FurnTypes, Is.Not.InstanceOf<string[]>(),
                "FurnTypes 暴露裸数组 ⇒ 同上");
        }

        // ══════════════════ 三 · 生产属性源(三值查询)══════════════════

        /// <summary>只读视图的测试内替身(协作对象,不是生产实现)。</summary>
        private sealed class FakeView : IPlacedStructureReadView
        {
            private readonly string[] _moduleIds;
            private readonly WorldPos[][] _cells;

            public FakeView(string[] moduleIds, params WorldPos[][] cells)
            {
                _moduleIds = moduleIds; _cells = cells;
            }

            public int StructureCount => _cells.Length;
            public IReadOnlyList<WorldPos> GetOccupiedCells(int structureIndex) => _cells[structureIndex];
            public string GetModuleId(int structureIndex) => _moduleIds[structureIndex];
        }

        private static ClinicFurnitureCatalogue Catalogue()
            => new ClinicFurnitureCatalogue(
                new[]
                {
                    new ClinicFurnitureEntry("bed", 1, new Fix(Fix.OneRaw / 64)),
                    new ClinicFurnitureEntry("chamber_pot", 0, new Fix(-Fix.OneRaw / 64)),
                }, 3);

        private static WorldPos Cell(int x, int y = 0, int z = 0) => new WorldPos(x, y, z);

        /// <summary>家具格 / 非家具格 / 未知格三值各归各位。</summary>
        /// <remarks><b>判别力</b>:把 `Lookup` 的 `Unknown` 分支改成 `NonFurniture`(即拍平成 bool)
        /// ⇒ 第三条断言红。这正是「源不完整」与「这格不是家具」被合并的静默失效形态。</remarks>
        [Test]
        public void test_traitsSource_threeValuedLookup()
        {
            var view = new FakeView(
                new[] { "bed", "wall" },
                new[] { Cell(0, 0) },
                new[] { Cell(1, 0) });
            var source = new ClinicFurnitureTraitsSource(view, Catalogue());

            Assert.That(source.Lookup(Cell(0, 0), out ClinicFurnitureTraits bed),
                Is.EqualTo(ClinicCellLookup.Furniture));
            Assert.That(bed.Type, Is.EqualTo("bed"));
            Assert.That(bed.Tier, Is.EqualTo(1));
            Assert.That(bed.EEnv.Raw, Is.EqualTo(Fix.OneRaw / 64));

            Assert.That(source.Lookup(Cell(1, 0), out _), Is.EqualTo(ClinicCellLookup.NonFurniture),
                "已占用但 module_id ∉ FURN_TYPES ⇒ 确定的非家具(GDD 的非格判据)");

            Assert.That(source.Lookup(Cell(9, 9), out _), Is.EqualTo(ClinicCellLookup.Unknown),
                "无任何实例占用 ⇒ Unknown(「我不知道」),不得静默答「不是家具」");
        }

        /// <summary>多格结构的**非锚点格**同样是家具格(承陷阱 ①:不可退化成锚点查询)。</summary>
        [Test]
        public void test_traitsSource_multiCellStructure_allFootprintCells()
        {
            var view = new FakeView(new[] { "bed" }, new[] { Cell(0, 0), Cell(1, 0), Cell(2, 0) });
            var source = new ClinicFurnitureTraitsSource(view, Catalogue());

            for (int x = 0; x < 3; x++)
                Assert.That(source.Lookup(Cell(x, 0), out _), Is.EqualTo(ClinicCellLookup.Furniture),
                    $"({x},0) 是同一件床的足迹格,须全部算家具");
        }

        /// <summary>同一格被两件实例占用 = 23 违约 ⇒ 抛(不得静默择一)。</summary>
        /// <remarks><b>判别力</b>:把构造里的重复检测去掉(直接覆盖写)⇒ 本测红,
        /// 且属性会随实例报告序变化(不确定)。</remarks>
        [Test]
        public void test_traitsSource_overlappingOccupancy_throws()
        {
            var view = new FakeView(
                new[] { "bed", "chamber_pot" },
                new[] { Cell(0, 0) },
                new[] { Cell(0, 0) });

            Assert.Throws<InvalidOperationException>(
                () => new ClinicFurnitureTraitsSource(view, Catalogue()));
        }

        /// <summary>生产源能被求值器直接消费(端到端:FURN_TABLE → traits → env/equip)。</summary>
        [Test]
        public void test_traitsSource_feedsEvaluator_endToEnd()
        {
            var view = new FakeView(
                new[] { "bed", "chamber_pot" },
                new[] { Cell(0, 0) },
                new[] { Cell(1, 0) });

            var source = new ClinicFurnitureTraitsSource(view, Catalogue());
            ClinicRoomExtractionResult room = ClinicRoomExtraction.Extract(view);
            Assert.That(room.HasRoom, Is.True, "两件相邻家具 ⇒ 一个连通簇");

            // AC-24-06:无序对须**双向**都有条目(表构造期校验对称性 ⇒ 单向缺项被拒)。
            var adj = new ClinicAdjTable(
                new[]
                {
                    new ClinicAdjPair("bed", "chamber_pot", new Fix(Fix.OneRaw / 8)),
                    new ClinicAdjPair("chamber_pot", "bed", new Fix(Fix.OneRaw / 8)),
                },
                64, new Fix(-Fix.OneRaw / 4), new Fix(Fix.OneRaw / 4));

            // env = base_env(0) + (1/64) + (−1/64) + adj(1/8) = 1/8
            Fix env = ClinicModifierEvaluator.ClinicEnvModOf(room, source, adj, Fix.Zero);
            Assert.That(env.Raw, Is.EqualTo(Fix.OneRaw / 8),
                "生产源喂进求值器的和不对 ⇒ 三值查询或 e_env 取值有偏");

            // equip:score = tier(bed)=1(tier=0 的 chamber_pot 不计)⇒ raw = 1 × 65536 = 65536,
            // 再经 F-24-2 的**单钳制**到 EQUIP_MOD_CAP = 1/4 = 16384(EQUIP_MOD_CAP < 1 的二值语义)。
            Fix equip = ClinicModifierEvaluator.ClinicEquipModOf(room, source);
            Assert.That(equip.Raw, Is.EqualTo(ClinicModifierEvaluator.EquipModCapRaw),
                "tier=0 的家具须被排除 ⇒ score=1 ⇒ 钉顶到 EQUIP_MOD_CAP(raw 16384)");

            // 反向判别:tier=0 若被误计入,score 仍是 1(chamber_pot tier 恰为 0),
            // 故上一条**不能**单独证明排除 —— 用「只有 tier=0 家具」的布局补一条:
            var onlyPot = new ClinicFurnitureTraitsSource(
                new FakeView(new[] { "chamber_pot" }, new[] { Cell(0, 0) }), Catalogue());
            Fix equipPotOnly = ClinicModifierEvaluator.ClinicEquipModOf(
                ClinicRoomExtraction.Extract(new FakeView(new[] { "chamber_pot" }, new[] { Cell(0, 0) })),
                onlyPot);
            Assert.That(equipPotOnly.Raw, Is.EqualTo(0),
                "布局只含 tier=0 的家具 ⇒ equip_score = 0(这条才真正钉住「tier=0 不进和」)");
        }

        // ══════════════════ 四 · 真判据析出(闭集落地后的入口)══════════════════

        /// <summary>真判据与代理判据**不等价** —— 非家具结构不得进 S。</summary>
        /// <remarks><b>判别力</b>:把 `Extract(view, furnTypes)` 改成转发单参重载(忽略闭集)⇒ 本测红。
        /// 这是本轮最核心的一条:闭集落地前,代理判据会把墙 / 装饰件也算成家具格。</remarks>
        [Test]
        public void test_roomExtraction_truePredicate_excludesNonFurnitureStructures()
        {
            // (1,0) 是墙(非家具):代理判据下它把 (0,0) 与 (2,0) 桥接成一个 3 格房间;
            // 真判据下 S = {(0,0), (2,0)} ⇒ 2 个不连通簇 ⇒ 整盘 ROOM_NONE(EC-24-07)。
            var view = new FakeView(
                new[] { "bed", "wall", "bed" },
                new[] { Cell(0, 0) },
                new[] { Cell(1, 0) },
                new[] { Cell(2, 0) });
            var furnTypes = new[] { "bed" };

            ClinicRoomExtractionResult proxy = ClinicRoomExtraction.Extract(view);
            Assert.That(proxy.CellCount, Is.EqualTo(3), "代理判据把墙也算进了 S(已知更宽)");
            Assert.That(proxy.HasRoom, Is.True, "代理判据下三格连通成一个房间");

            ClinicRoomExtractionResult truth = ClinicRoomExtraction.Extract(view, furnTypes);
            Assert.That(truth.CellCount, Is.EqualTo(2), "真判据下 S 只含两件床");
            Assert.That(truth.ConnectedClusterCount, Is.EqualTo(2));
            Assert.That(truth.HasRoom, Is.False,
                "EC-24-07:≥ 2 簇 ⇒ 整盘 ROOM_NONE(禁「就近簇部分生效」)");
        }

        /// <summary>真判据下布局一致时,与代理判据给同一 room id(成员相同时不许分叉)。</summary>
        [Test]
        public void test_roomExtraction_truePredicate_agreesWhenMembershipIdentical()
        {
            var view = new FakeView(
                new[] { "bed", "bed" },
                new[] { Cell(0, 0) },
                new[] { Cell(1, 0) });

            ClinicRoomExtractionResult proxy = ClinicRoomExtraction.Extract(view);
            ClinicRoomExtractionResult truth = ClinicRoomExtraction.Extract(view, new[] { "bed" });

            Assert.That(truth.RoomId, Is.EqualTo(proxy.RoomId),
                "成员集相同时两条入口必须给同一 room id(析出机器不得分叉)");
        }

        /// <summary>闭集为 null ⇒ 退回代理判据(显式退化,不是静默)。</summary>
        [Test]
        public void test_roomExtraction_nullFurnTypes_fallsBackToProxy()
        {
            var view = new FakeView(new[] { "wall" }, new[] { Cell(0, 0) });

            ClinicRoomExtractionResult truth = ClinicRoomExtraction.Extract(view, null);
            ClinicRoomExtractionResult proxy = ClinicRoomExtraction.Extract(view);
            Assert.That(truth.RoomId, Is.EqualTo(proxy.RoomId));
            Assert.That(truth.CellCount, Is.EqualTo(1));
        }

        /// <summary>`module_id` 为空 / null 的实例在真判据下被排除(不是「随便算家具」)。</summary>
        [Test]
        public void test_roomExtraction_truePredicate_excludesMissingModuleId()
        {
            var view = new FakeView(new string[] { null }, new[] { Cell(0, 0) });
            ClinicRoomExtractionResult result = ClinicRoomExtraction.Extract(view, new[] { "bed" });

            Assert.That(result.CellCount, Is.EqualTo(0), "无模块标识 ⇒ 无从证明 ∈ 闭集 ⇒ 排除");
            Assert.That(result.HasRoom, Is.False);
        }

        /// <summary>运行期表可直接喂真判据(闭集的运行期载体与析出入口连通)。</summary>
        [Test]
        public void test_roomExtraction_catalogueFurnTypes_feedTruePredicate()
        {
            ClinicFurnitureCatalogue catalogue =
                ClinicFurnitureCookedCodec.Read(ReadCommitted()).Catalogue;

            var view = new FakeView(
                new[] { "bed", "wall" },
                new[] { Cell(0, 0) },
                new[] { Cell(1, 0) });

            ClinicRoomExtractionResult result =
                ClinicRoomExtraction.Extract(view, catalogue.FurnTypes);

            Assert.That(result.CellCount, Is.EqualTo(1),
                "cooked 闭集喂进真判据后,墙须被排除");
        }

        // ══════════════════ 五 · 生产链路上的闭集强制(O-24-1 的落点判据)══════════════════

        /// <summary>
        /// **生产链路**上闭集真的被强制 —— 打的是菜单用的**同一份代码**
        /// (<see cref="DataBakeMenu.BakeClinicMachineTablesCore"/>),不是复刻品。
        /// </summary>
        /// <remarks>
        /// <b>为什么必须有这条</b>:2026-10-21 核查发现 `DataBakeMenu.BakeClinicMachineTables()`
        /// (带 `[MenuItem]`,batch 下不执行)是闭集注入的**唯一生产调用点**,
        /// 而测试树对它**零命中** ⇒ 「生产路径上闭集真被强制」当时只有烘焙日志可依,没有机器判据。
        /// 这是 `.claude/skills/story-done` 点名的「扫不到被测装配」形态。
        /// <para>⚠️ **判据的边界(如实登记)**:本测直调 Core,菜单本体仍未被执行 ——
        /// 但 Core 是菜单体内的**唯一计算段**(落盘 / Refresh / 日志之外别无他物),
        /// 所以「Core 绿」⇒「菜单的计算段绿」。这与「同构复刻」的区别是可证的:
        /// 复刻品与菜单各写一遍,漂移后测仍绿;**本测与菜单共用同一符号**,漂移不可能发生。</para>
        /// <para>⚠️ 本测**不写盘**(Core 刻意不落盘)—— 承 `.claude/rules/test-standards.md`。</para>
        /// </remarks>
        [Test]
        public void test_productionMenuPath_coreForcesFurnClosure()
        {
            ItemDatabaseFurnitureBake.BakeOutput furn;
            ClinicMachineTablesBake.BakeOutput output =
                DataBakeMenu.BakeClinicMachineTablesCore(RepoRoot, out furn);

            // ① 哨兵:Core 真的注入了闭集来源(而不是像 2026-10-20 那样传 null)。
            Assert.That(output.Room.FurnMembershipEnforced, Is.True,
                "生产链路(菜单 Core)的 FurnMembershipEnforced 须为 true —— " +
                "为 false 说明闭集注入被摘掉/退回传 null(O-24-1 的落点断了)");

            // ② 非空跑:闭集本身来自本次 21a 烘焙,且与 cooked 产物同源。
            Assert.That(furn.Bound.Rows.Count, Is.GreaterThan(0),
                "闭集来源为空 ⇒ 成员资格门会对每个子句都拒绝(或形同虚设),须先修家具目录");
            Assert.That(furn.Cooked.Length, Is.GreaterThan(CookedFormat.HeaderSize),
                "21a 产物字节须非空(与 test_furniture_seedSource_bakesCleanly 同判据)");

            // ③ **真正的语义层**:ROOM_TABLE 里每个被引用的 type 都在闭集内。
            //    只断言 ① 会被「置标志但不过滤」满足(见 test_room_furn_membership_enforced_with_injected_source
            //    旁边的对照测 —— 那条证明替身缺类型时会拒);这里证明**真种子**的每个子句都过得了门。
            var closure = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < furn.Bound.Rows.Count; i++) closure.Add(furn.Bound.Rows[i].Type);

            int referenced = 0;
            for (int r = 0; r < output.Room.Rooms.Count; r++)
            {
                ClinicMachineRoomBinder.RoomDef room = output.Room.Rooms[r];
                for (int c = 0; c < room.Clauses.Count; c++)
                {
                    ClinicMachineRoomBinder.RoomClause clause = room.Clauses[c];
                    if (string.IsNullOrEmpty(clause.Type)) continue;
                    referenced++;
                    Assert.That(closure.Contains(clause.Type), Is.True,
                        $"房间「{room.RoomType}」子句 type「{clause.Type}」∉ FURN_TYPES 闭集 " +
                        "(闭集 = " + string.Join(", ", closure) + ")—— " +
                        "AC-24-12 的强制若生效,这种子句在烘焙期就该被拒");
                    if (!string.IsNullOrEmpty(clause.NeighborType))
                    {
                        referenced++;
                        Assert.That(closure.Contains(clause.NeighborType), Is.True,
                            $"房间「{room.RoomType}」子句 neighbor_type「{clause.NeighborType}」∉ FURN_TYPES 闭集");
                    }
                }
            }

            // ④ 非真空:若种子一个子句都没引用类型,上一条循环是空的 —— 必须如实失败而不是空转绿。
            Assert.That(referenced, Is.GreaterThan(0),
                "ROOM_TABLE 种子零个 type 引用 ⇒ ③ 是空循环,本测对闭集强制**毫无判别力**;须先补种子");
        }
    }
}
