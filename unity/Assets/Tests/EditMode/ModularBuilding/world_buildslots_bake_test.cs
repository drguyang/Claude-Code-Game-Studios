// `world_buildslots.json`(建造槽位骨架 · ADR-022 §三)的烘焙 / 域门 / 读方镜像 / 端到端测试。
//
// 权威来源:
//   ADR-022 §二(唯一作者 = 关卡工具)· §三(整数格 + `BakedInitial`)· §四(C1–C6)
//   ADR-014 §三/§四(白名单 · 聚合 throw)· ADR-015 §三(单一整数格)
//
// ⚠️ **判别力纪律**(承 `build_modules_bake_test.cs` / `TD-clinic-01`):每条负向夹具都对应
//   一条**具体域门**,断言打的是错误报文里的**判据关键词**(字面量),不是「抛了就行」。
//   ⚠️ 关键词一律写字面量,**不**引用被测类的常量 —— 自指判据在变异时会两边一起动,
//   零红逃逸。
//
// ⚠️ **写盘纪律**:负向夹具在 `Path.GetTempPath()` 下造最小仓根(承 `.claude/rules/test-standards.md`
//   与 `FsWriteBoundaryGate`);真仓库 `assets/data/` 一字不动。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using DaYiJingCheng.EditorTools.Bake;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.Presentation;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;
using UnityEngine;

namespace DaYiJingCheng.Tests.ModularBuilding
{
    /// <summary>建造槽位骨架:域门 / 确定性 / 读方镜像 / 生产装载端到端。</summary>
    [TestFixture]
    public class world_buildslots_bake_test
    {
        private static string RepoRoot
            => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));

        // ══════════════════ 一 · 真种子(正向半边)══════════════════

        /// <summary>真种子过五道域门,且 Decor / Shell 两个分支都可达。</summary>
        /// <remarks><b>判别力</b>:任一门误判(把合法行当越界)⇒ 本测当场红;
        /// 导出器把某个区域丢掉 ⇒ 「Decor 与 Shell 均在」那条断言红。</remarks>
        [Test]
        public void test_worldBuildSlots_realSeed_passesAllGates()
        {
            WorldBuildSlotsBake.BakeOutput output = WorldBuildSlotsBake.BakeFromRepo(RepoRoot);

            Assert.That(output.Bound.Rows.Count, Is.GreaterThan(0), "真种子须非空");
            Assert.That(output.Cooked.Length, Is.GreaterThan(20), "产物须含 20 字节头 + 载荷");
            Assert.That(output.ConfigVersion, Is.Not.EqualTo(0u), "ConfigVersion 须由内容哈希派生");
            Assert.That(output.Bound.Width, Is.EqualTo(16), "声明范围 X 须 = 合成世界 16");
            Assert.That(output.Bound.Height, Is.EqualTo(1), "声明范围 Y 须 = 合成世界 1");
            Assert.That(output.Bound.Depth, Is.EqualTo(16), "声明范围 Z 须 = 合成世界 16");

            // Decor = SlotType 序数 5;Shell = 6(写 401 字面量,不引用被测枚举 —— 自指判据会逃逸)。
            Assert.That(output.Bound.Rows.Any(r => r.TypeOrdinal == 5), Is.True,
                "种子须含 Decor 槽位(否则 Decor 分支不可达)");
            Assert.That(output.Bound.Rows.Any(r => r.TypeOrdinal == 6), Is.True,
                "种子须含 Shell 槽位(否则 Shell 分支不可达)");

            // `BakedInitial` **两侧都要有真实取值** —— 全 true = 「开档医馆已建满」= 玩家无可建格,
            // 与本轮目的相反;全 false = 该字段恒假,等于没测。两侧都断,任一侧塌掉本测即红。
            Assert.That(output.Bound.Rows.Any(r => r.BakedInitial), Is.True,
                "种子须至少一格 `baked_initial = true`(外壳在开档时就存在)");
            Assert.That(output.Bound.Rows.Any(r => !r.BakedInitial), Is.True,
                "种子须至少一格 `baked_initial = false`(空地可建;全 true ⇒ 玩家建不了任何东西)");
        }

        // ══════════════════ 二 · 生产核真的会拒(负向,走 DataBakeMenu)══════════════════

        /// <summary>越界格 ⇒ 生产核抛,且报文点名「超出声明范围」+ 坏格坐标。</summary>
        /// <remarks><b>判别力</b>:把 `ParseRow` 的「格超出声明范围」分支改成 `if (false)`
        /// ⇒ 本测红(不再抛)。</remarks>
        [Test]
        public void test_worldBuildSlots_productionCore_rejectsOutOfBoundsCell()
        {
            string tempRoot = MakeTempRepoRoot(
                "{\"schema_version\":1,\"level_id\":\"tmp\",\"world_size\":[4,1,4]," +
                "\"slots\":[{\"cell\":[9,0,0],\"slot_type\":\"Decor\",\"baked_initial\":false}]}");
            try
            {
                var ex = Assert.Throws<BakeValidationException>(
                    () => DataBakeMenu.BakeWorldBuildSlotsCore(tempRoot));

                Assert.That(ex.Errors.Any(e => e.IndexOf("超出声明范围", StringComparison.Ordinal) >= 0),
                    Is.True, "报文须点名判据「超出声明范围」:\n" + string.Join("\n", ex.Errors));
                Assert.That(ex.Errors.Any(e => e.IndexOf("(9,0,0)", StringComparison.Ordinal) >= 0),
                    Is.True, "报文须点名坏格 (9,0,0)(否则读者不知道该改哪一行)");
                // ⚠️ 报文前缀是 `{FileName}:`,即 `world_buildslots.json:` —— 首版这里写成
                //    `world_buildslots:`(少了 `.json`),首次实跑即红。**是夹具写错,不是生产错**;
                //    订正依据 = `WorldBuildSlotsBinder` 全部 `errors.Add($"{FileName}:…")`。
                Assert.That(ex.Errors.Any(e => e.StartsWith("world_buildslots.json:", StringComparison.Ordinal)),
                    Is.True, "报文须以 `world_buildslots.json:` 起头(多表聚合时读者要靠它定位是哪张表):\n"
                             + string.Join("\n", ex.Errors));
            }
            finally
            {
                if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, recursive: true);
            }
        }

        /// <summary>同一最小仓根换成合法行 ⇒ 生产核**放行**(证明上一条不是「核恒抛」)。</summary>
        /// <remarks><b>判别力</b>:若生产核被改成无条件抛,上一条仍绿而本条红 —— 成对使用。</remarks>
        [Test]
        public void test_worldBuildSlots_productionCore_acceptsValidRow()
        {
            string tempRoot = MakeTempRepoRoot(
                "{\"schema_version\":1,\"level_id\":\"tmp\",\"world_size\":[4,1,4]," +
                "\"slots\":[{\"cell\":[1,0,1],\"slot_type\":\"Decor\",\"baked_initial\":false}]}");
            try
            {
                WorldBuildSlotsBake.BakeOutput output =
                    DataBakeMenu.BakeWorldBuildSlotsCore(tempRoot);

                Assert.That(output.Bound.Rows.Count, Is.EqualTo(1));
                Assert.That(output.Bound.Rows[0].X, Is.EqualTo(1));
                Assert.That(output.Bound.Rows[0].TypeOrdinal, Is.EqualTo(5), "Decor 序数须为 5");
                Assert.That(output.Bound.Rows[0].BakedInitial, Is.False);
            }
            finally
            {
                if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, recursive: true);
            }
        }

        // ══════════════════ 三 · 其余域门(负向)══════════════════

        /// <summary>空 `slots[]` ⇒ 拒。</summary>
        [Test]
        public void test_worldBuildSlots_emptyTable_isRejected()
            => AssertRejected(
                "{\"schema_version\":1,\"level_id\":\"t\",\"world_size\":[4,1,4],\"slots\":[]}",
                "为空表");

        /// <summary>重复格 ⇒ 拒。</summary>
        [Test]
        public void test_worldBuildSlots_duplicateCell_isRejected()
            => AssertRejected(
                "{\"schema_version\":1,\"level_id\":\"t\",\"world_size\":[4,1,4],\"slots\":[" +
                "{\"cell\":[1,0,1],\"slot_type\":\"Decor\",\"baked_initial\":false}," +
                "{\"cell\":[1,0,1],\"slot_type\":\"Shell\",\"baked_initial\":false}]}",
                "重复");

        /// <summary>`slot_type = "None"` ⇒ 拒(哨兵不是合法类型)。</summary>
        [Test]
        public void test_worldBuildSlots_noneSlotType_isRejected()
            => AssertRejected(
                "{\"schema_version\":1,\"level_id\":\"t\",\"world_size\":[4,1,4],\"slots\":[" +
                "{\"cell\":[1,0,1],\"slot_type\":\"None\",\"baked_initial\":false}]}",
                "SlotType 闭集");

        /// <summary>`baked_initial` 缺席 ⇒ 拒(「没写」≠「明写 false」)。</summary>
        [Test]
        public void test_worldBuildSlots_missingBakedInitial_isRejected()
            => AssertRejected(
                "{\"schema_version\":1,\"level_id\":\"t\",\"world_size\":[4,1,4],\"slots\":[" +
                "{\"cell\":[1,0,1],\"slot_type\":\"Decor\"}]}",
                "baked_initial");

        /// <summary>未知键 ⇒ 拒(ADR-014 §三)。</summary>
        [Test]
        public void test_worldBuildSlots_unknownKey_isRejected()
            => AssertRejected(
                "{\"schema_version\":1,\"level_id\":\"t\",\"world_size\":[4,1,4],\"slots\":[" +
                "{\"cell\":[1,0,1],\"slot_type\":\"Decor\",\"baked_initial\":false,\"tier\":2}]}",
                "未知键");

        // ══════════════════ 四 · 确定性 + 读方 ↔ 写方镜像 ══════════════════

        /// <summary>同源两次烘焙逐位一致(ADR-014 §二)。</summary>
        [Test]
        public void test_worldBuildSlots_bakeIsDeterministic()
        {
            byte[] a = WorldBuildSlotsBake.BakeFromRepo(RepoRoot).Cooked;
            byte[] b = WorldBuildSlotsBake.BakeFromRepo(RepoRoot).Cooked;
            Assert.That(a, Is.EqualTo(b), "同源两次烘焙字节不等 ⇒ 有字典迭代序 / 本地时间进了产物");
        }

        /// <summary>cooked 往返:编码 → 解码 → 行逐字段相等(行序双方均 (x,y,z) 升序)。</summary>
        /// <remarks><b>判别力</b>:写方 `CompareCell` 与读方字段序任一错位 ⇒ 本测红。</remarks>
        [Test]
        public void test_worldBuildSlots_cookedRoundTrip_isFieldwiseIdentical()
        {
            WorldBuildSlotsBake.BakeOutput output = WorldBuildSlotsBake.BakeFromRepo(RepoRoot);
            WorldBuildSlotsDataSet decoded = WorldBuildSlotsCookedCodec.Read(output.Cooked);

            var expected = new List<WorldBuildSlotsBinder.SlotRow>(output.Bound.Rows);
            expected.Sort((p, q) =>
            {
                int cmp = p.X.CompareTo(q.X);
                if (cmp != 0) return cmp;
                cmp = p.Y.CompareTo(q.Y);
                if (cmp != 0) return cmp;
                return p.Z.CompareTo(q.Z);
            });

            Assert.That(decoded.Rows.Count, Is.EqualTo(expected.Count), "行数须一致");
            Assert.That(decoded.Width, Is.EqualTo(output.Bound.Width));
            Assert.That(decoded.Height, Is.EqualTo(output.Bound.Height));
            Assert.That(decoded.Depth, Is.EqualTo(output.Bound.Depth));
            Assert.That(decoded.ConfigVersion, Is.EqualTo(output.ConfigVersion),
                "头槽 ConfigVersion 须与烘焙输出一致");
            Assert.That(decoded.SchemaVersion, Is.EqualTo(1u), "头槽 schema_version 须 = 1");

            for (int i = 0; i < expected.Count; i++)
            {
                WorldBuildSlotRow got = decoded.Rows[i];
                WorldBuildSlotsBinder.SlotRow want = expected[i];
                Assert.That(got.Cell.X, Is.EqualTo(want.X), $"rows[{i}].x");
                Assert.That(got.Cell.Y, Is.EqualTo(want.Y), $"rows[{i}].y");
                Assert.That(got.Cell.Z, Is.EqualTo(want.Z), $"rows[{i}].z");
                Assert.That(got.AllowedMask, Is.EqualTo(1 << want.TypeOrdinal),
                    $"rows[{i}].allowed_mask 须 = 1 << {want.TypeOrdinal}");
                Assert.That(got.BakedInitial, Is.EqualTo(want.BakedInitial), $"rows[{i}].baked_initial");
            }
        }

        /// <summary>盘上产物与现烘字节**逐位相同**(忘了重烘 ⇒ 红)。</summary>
        [Test]
        public void test_worldBuildSlots_diskMatchesFreshBake()
        {
            string path = Path.Combine(
                RepoRoot, "unity", "Assets", "DataCooked", "world_buildslots.cooked.bytes");
            Assert.That(File.Exists(path), Is.True,
                $"产物不存在:{path} —— 先跑关卡工具导出 + 「烘焙 world_buildslots」菜单");

            byte[] disk = File.ReadAllBytes(path);
            byte[] fresh = WorldBuildSlotsBake.BakeFromRepo(RepoRoot).Cooked;
            Assert.That(disk, Is.EqualTo(fresh), "盘上产物与现烘字节不一致 ⇒ 忘了重烘");
        }

        // ══════════════════ 五 · 端到端(生产装配路径)══════════════════

        /// <summary>
        /// `MinimalWorldFactory.Create()` 的目录**非空**,且真实槽位跑
        /// `PlaceableChecker.Check` 不再返回 `SlotNotFound`。
        /// </summary>
        /// <remarks>
        /// <para><b>这是本任务的目的位</b>:此前 `RegisterSlot` 零生产调用点 ⇒ 恒 `SlotNotFound`。</para>
        /// <para><b>判别力</b>:把 `MinimalWorldFactory.Create()` 里的 `RegisterSlot` 循环删掉
        /// ⇒ 本测红(`Count == 0` 与 `SlotNotFound` 两条断言同时红)。</para>
        /// </remarks>
        [Test]
        public void test_worldBuildSlots_minimalWorld_catalogNonEmptyAndCheckNotSlotNotFound()
        {
            World world = MinimalWorldFactory.Create();

            Assert.That(world.SlotCatalog.Count, Is.GreaterThan(0),
                "生产装配的槽位目录须非空 —— 否则玩家建不了任何东西");
            Assert.That(world.SlotCatalog.Count, Is.EqualTo(
                    WorldBuildSlotsBake.BakeFromRepo(RepoRoot).Bound.Rows.Count),
                "目录格数须等于烘焙产物行数(装载不得丢行)");

            // Decor 3×3 区里的一格**空地**(种子定义见 P0ClinicLevel:6..8 × 6..8;
            // (6,0,6) 是唯一那格 `baked_initial = true` 的已占用格 —— 刻意不用它:
            // 运行期今日不应用初始占用,用它会让本测在「占用真的开始生效」那天悄悄变味)。
            var anchor = new WorldPos(7, 0, 7);
            Assert.That(world.SlotCatalog.IsValidSlot(anchor), Is.True,
                "Decor 区的锚点格须在目录内(种子与装载的坐标对不上 ⇒ 数据与世界不是同一张图纸)");

            var checker = new PlaceableChecker(
                world.SlotCatalog,
                new OneModuleCatalog(1, SlotType.Decor, new[] { new WorldPos(0, 0, 0) }),
                new AlwaysHasStock(),
                new NeverOccupied(),
                new NobodyPresent());

            PlaceableResult result = checker.Check(1, anchor);

            Assert.That(result, Is.Not.EqualTo(PlaceableResult.SlotNotFound),
                "真实槽位 + 匹配模块不得再返回 SlotNotFound(本条 = 本任务的目的判据)");
            Assert.That(result, Is.EqualTo(PlaceableResult.Success),
                $"全条件应满足(实得 {result})");
        }

        // ══════════════════ 辅助 ══════════════════

        /// <summary>在 `Path.GetTempPath()` 下造一个只含 `world_buildslots.json` 的最小仓根。</summary>
        private static string MakeTempRepoRoot(string json)
        {
            string tempRoot = Path.Combine(
                Path.GetTempPath(), "djc-buildslots-" + Guid.NewGuid().ToString("N"));
            string dataDir = Path.Combine(tempRoot, "assets", "data");
            Directory.CreateDirectory(dataDir);
            File.WriteAllText(Path.Combine(dataDir, "world_buildslots.json"), json);
            return tempRoot;
        }

        /// <summary>断言绑定被拒,且错误报文里含指定**字面量**判据关键词。</summary>
        private static void AssertRejected(string json, string expectedKeyword)
        {
            var ex = Assert.Throws<BakeValidationException>(
                () => WorldBuildSlotsBake.BakeFromSourceText(json));
            Assert.That(
                ex.Errors.Any(e => e.IndexOf(expectedKeyword, StringComparison.Ordinal) >= 0),
                Is.True,
                $"错误报文须点名判据「{expectedKeyword}」(否则任何 throw 都能让本夹具变绿):\n" +
                string.Join("\n", ex.Errors));
        }

        /// <summary>只有一个模块的目录(端到端用;不做别的判定)。</summary>
        private sealed class OneModuleCatalog : IModuleCatalog
        {
            private readonly ModuleDefinition _definition;

            public OneModuleCatalog(int moduleId, SlotType type, IReadOnlyList<WorldPos> occupancy)
            {
                _definition = new ModuleDefinition(
                    moduleId, type, occupancy, new[] { 0 }, cost: 1, buildItemKey: 0);
            }

            public ModuleDefinition? GetModuleDefinition(int moduleId)
                => moduleId == _definition.ModuleId ? _definition : (ModuleDefinition?)null;

            public bool ContainsModule(int moduleId) => moduleId == _definition.ModuleId;

            public IReadOnlyCollection<int> GetAllModuleIds() => new[] { _definition.ModuleId };
        }

        private sealed class AlwaysHasStock : IStockQuery
        {
            public bool HasStock(int moduleId) => true;
        }

        private sealed class NeverOccupied : IOccupancyQuery
        {
            public bool IsOccupied(WorldPos cell) => false;
        }

        /// <summary>无人 / 无病人(端到端用;`IPresenceQuery` 四个成员全实现 —— 漏一个即编译失败)。</summary>
        private sealed class NobodyPresent : IPresenceQuery
        {
            public bool IsPresent(PatientId patientId) => false;
            public int PresentCount => 0;
            public bool IsPresentAt(WorldPos cell) => false;
            public IReadOnlyCollection<int> PresentPatientIds() => Array.Empty<int>();
        }
    }
}
