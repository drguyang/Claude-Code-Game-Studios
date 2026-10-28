// 关卡工具 story-003 —— **生态区多边形**(C4 五项 + C5 单一源 + 运行期装载面)的夹具。
//
// 权威来源:
//   `production/epics/level-tool/story-003-world-ecozones-and-c4-c5.md`(AC-1 … AC-6)
//   ADR-022 §三(导出契约)· §四 **C4 / C5**(硬失败)· ADR-014 §二(两阶段烘焙)
//   GDD `world-and-ecozones.md` **F-6-3**(开集语义 + 裁决序 min(id))
//     · **AC-6-19** 五类退化 · **EC-6-7** · **EC-6-10**(`NONE` 是合法结果)
//     · **R-6-15 缝 2**(边界一次定义、两次派生)
//
// ⚠️ **零写盘 / 零随机 / 零时间依赖**。
// ⚠️ **C4 五项各一条独立夹具**(Story AC-2 逐字要求):一个夹具验五项会让停用任一分支时
//    其余项仍红 ⇒ 判别面不可分(承 `feedback_falsify_one_guard_per_property`)。
//
// ⚠️ 本件**不证**:生态区数量 / 形状是否合理(关卡设计)· 视觉层遮罩是否存在(美术轮)·
//    `EcozoneOf` 的跨平台逐位一致性(ADR-012 黄金夹具矩阵)。

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using DaYiJingCheng.EditorTools.Level;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.Presentation;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.LevelTool
{
    /// <summary>story-003:生态区多边形的导出形状、C4 五项、C5 单一源、运行期装载面。</summary>
    [TestFixture]
    public class ecozone_polygon_check_test
    {
        private static readonly string RepoRoot = ComputeRepoRoot();

        private static string ComputeRepoRoot(
            [System.Runtime.CompilerServices.CallerFilePath] string thisFile = "")
            => Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(thisFile) ?? ".", "..", "..", "..", "..", ".."));

        private static string CookedDir => Path.Combine(RepoRoot, "unity", "Assets", "DataCooked");

        // ══════════════════════ AC-1:world_ecozones 的导出形状 ══════════════════════

        /// <summary>AC-1:产物里**零浮点字面量**。</summary>
        /// <remarks><b>判别力</b>:让导出器把某坐标写成 `1.0` ⇒ 本测红。</remarks>
        [Test]
        public void test_ac1_export_hasZeroFloatLiterals()
        {
            string json = LevelExporter.ExportEcozones(LevelCatalog.Define(P0ClinicLevel.LevelId));

            Assert.That(Regex.IsMatch(json, @"[0-9]+\.[0-9]+"), Is.False,
                "产物里出现了小数写法 —— AC-1 要求零浮点;实际产物:\n" + json);
        }

        /// <summary>AC-1:顶点是 **`[x, z]` 二元组**(不是三元组),且 `ecozone_id` 唯一、≥ 0。</summary>
        /// <remarks><b>判别力</b>:导出器改成写 `[x, 0, z]` ⇒ 本测红(GDD F-6-3 §四:y 不参与)。</remarks>
        [Test]
        public void test_ac1_export_verticesAreXZPairs_idsUnique()
        {
            string json = LevelExporter.ExportEcozones(LevelCatalog.Define(P0ClinicLevel.LevelId));

            Assert.That(json, Does.Contain("\"vertices\": [[1, 1], [8, 1], [8, 14], [1, 14]]"), json);
            Assert.That(json, Does.Not.Contain("[1, 0, 1]"),
                "顶点被写成了三元组 —— GDD F-6-3 §四 明写 y 不参与判定;" +
                "把恒 0 的 y 写进文件会造出一个「看起来像字段的常量」");

            MatchCollection ids = Regex.Matches(json, "\"ecozone_id\": (-?[0-9]+)");
            var seen = new HashSet<int>();
            foreach (Match m in ids)
            {
                int id = int.Parse(m.Groups[1].Value);
                Assert.That(id, Is.GreaterThanOrEqualTo(0),
                    "ecozone_id 须 ≥ 0(-1 是 EcozoneId.None 哨兵,GDD :634)");
                Assert.That(seen.Add(id), Is.True, $"ecozone_id {id} 重复");
            }
            Assert.That(seen.Count, Is.GreaterThanOrEqualTo(2), "真种子须至少两个区(AC-5 的区内/区外两方向)");
        }

        /// <summary>AC-1:未声明生态区的关卡 ⇒ 导出**硬失败**,不合成空表。</summary>
        [Test]
        public void test_ac1_undeclaredEcozones_throws()
        {
            var src = new LevelSource("probe_no_ecozone", 2, 1, 2, new List<SlotSpec>
            {
                new SlotSpec(1, 0, 1, SlotType.Decor, false),
            });

            var ex = Assert.Throws<InvalidOperationException>(() => LevelExporter.ExportEcozones(src));
            StringAssert.Contains("生态区", ex.Message);
        }

        // ══════════════════════ AC-2:C4 五项(各一条独立夹具)══════════════════════

        /// <summary>C4 ① **内部重叠** ⇒ 抛。</summary>
        /// <remarks><b>判别力</b>:停用 `EcozonePolygonCheck` 的「内部相交」判据 ⇒ 本测红
        /// (其余四项不红 —— 夹具的多边形其余方面全合法)。</remarks>
        [Test]
        public void test_ac2_overlap_interiorsIntersect_throws()
        {
            // 两个 (1,1)-(6,6) 与 (3,3)-(9,9) 的方块:内部相交,但**没有**共享边。
            var src = Source(
                Zone(0, 1, 1, 6, 1, 6, 6, 1, 6),
                Zone(1, 3, 3, 9, 3, 9, 9, 3, 9));

            AssertViolation(src, "内部", "相交");
        }

        /// <summary>C4 ② **顶点 &lt; 3** ⇒ 抛。</summary>
        /// <remarks><b>判别力</b>:停用「顶点 ≥ 3」判据 ⇒ 本测红。</remarks>
        [Test]
        public void test_ac2_fewerThanThreeVertices_throws()
        {
            var src = Source(Zone(0, 1, 1, 5, 1));

            AssertViolation(src, "顶点数", "≥ 3");
        }

        /// <summary>C4 ③ **自交** ⇒ 抛。</summary>
        /// <remarks><b>判别力</b>:停用「非相邻边真交叉」判据 ⇒ 本测红。</remarks>
        [Test]
        public void test_ac2_selfIntersection_throws()
        {
            // 蝴蝶结:(1,1) → (6,6) → (6,1) → (1,6) —— 两条非相邻边在 (3.5,3.5) 交叉。
            var src = Source(Zone(0, 1, 1, 6, 6, 6, 1, 1, 6));

            AssertViolation(src, "自交");
        }

        /// <summary>C4 ④ **零面积**(全部顶点共线)⇒ 抛。</summary>
        /// <remarks><b>判别力</b>:停用 shoelace 零面积判据 ⇒ 本测红。</remarks>
        [Test]
        public void test_ac2_zeroArea_throws()
        {
            // 三点共线于 z = 3:没有内部。
            var src = Source(Zone(0, 1, 3, 4, 3, 7, 3));

            AssertViolation(src, "面积");
        }

        /// <summary>C4 ⑤ **重合顶点**(同一多边形内相邻顶点相同)⇒ 抛。</summary>
        /// <remarks><b>判别力</b>:停用「相邻顶点重合」判据 ⇒ 本测红。</remarks>
        [Test]
        public void test_ac2_coincidentAdjacentVertices_throws()
        {
            var src = Source(Zone(0, 1, 1, 5, 1, 5, 1, 5, 5, 1, 5));

            AssertViolation(src, "重合");
        }

        /// <summary>C4 的**开集口径**:相邻区**共享一条边**⇒ **不**失败(口径甲的判别面)。</summary>
        /// <remarks><b>判别力</b>:把重叠判据换成「闭包相交」(乙口径)⇒ 本测红。
        /// 这是 AC-2 的边界另一半 —— 只测五种违例、不测「合法贴边不被误报」,
        /// 一个「什么都拒」的实现会全绿。</remarks>
        [Test]
        public void test_ac2_sharedEdge_neighbouringZones_doNotFail()
        {
            // 左 (1,1)-(5,5) 与右 (5,1)-(9,5):共享 x = 5 这条边。
            var src = Source(
                Zone(0, 1, 1, 5, 1, 5, 5, 1, 5),
                Zone(1, 5, 1, 9, 1, 9, 5, 5, 5));

            Assert.That(EcozonePolygonCheck.TryCheck(src, out List<string> v), Is.False,
                "贴边相邻是**合法**的(F-6-3 §二:『唯一』由裁决序 min(id) 保证,不靠禁止共享边);" +
                "实际违例:\n" + string.Join("\n", v));
        }

        // ══════════════════════ AC-3:C5 边界单一源 ══════════════════════

        /// <summary>AC-3 ①:同一份多边形两次生成遮罩 ⇒ **逐字节相同**。</summary>
        /// <remarks><b>判别力</b>:让生成器掺入任何非确定性(字典迭代序 / 时间)⇒ 本测红。</remarks>
        [Test]
        public void test_ac3_mask_generatedTwice_bitIdentical()
        {
            EcozoneSource src = LevelCatalog.Define(P0ClinicLevel.LevelId).Ecozones;

            byte[] a = EcozoneMaskGenerator.Generate(src, 16, 16).ToBytes();
            byte[] b = EcozoneMaskGenerator.Generate(src, 16, 16).ToBytes();

            Assert.That(a, Is.EqualTo(b), "两次生成字节不同 —— 生成路径不可复算(AC-3 ①)");
            Assert.That(a.Length, Is.EqualTo(8 + 16 * 16 * 4), "遮罩字节长度须 = 头 8 B + W·D·4");
        }

        /// <summary>AC-3 ②:注入一份**与多边形不一致**的遮罩 ⇒ 抛,报文点名第一个不一致的格。</summary>
        /// <remarks><b>判别力</b>:把 `EcozoneMaskCheck.Verify` 改成直接 `return` ⇒ 本测红。</remarks>
        [Test]
        public void test_ac3_mask_inconsistentInjection_throws()
        {
            EcozoneSource src = LevelCatalog.Define(P0ClinicLevel.LevelId).Ecozones;
            EcozoneMask expected = EcozoneMaskGenerator.Generate(src, 16, 16);

            // 「手画第二份」:把 (4,4) 这一格判成了区 1(生成结果应是区 0)。
            var tampered = (int[])expected.ZoneId.Clone();
            tampered[4 * 16 + 4] = 1;
            var actual = new EcozoneMask(16, 16, tampered);

            var ex = Assert.Throws<InvalidOperationException>(
                () => EcozoneMaskCheck.Verify(expected, actual, "夹具手画遮罩"));
            StringAssert.Contains("(4,4)", ex.Message, "报文须点名第一个不一致的格");
            StringAssert.Contains("不同源", ex.Message);
        }

        /// <summary>AC-3 的边界:尺寸不符同样是「不同源」,且报文点名两侧尺寸。</summary>
        [Test]
        public void test_ac3_mask_sizeMismatch_throws()
        {
            EcozoneSource src = LevelCatalog.Define(P0ClinicLevel.LevelId).Ecozones;
            EcozoneMask expected = EcozoneMaskGenerator.Generate(src, 16, 16);
            var smaller = new EcozoneMask(8, 8, new int[8 * 8]);

            var ex = Assert.Throws<InvalidOperationException>(
                () => EcozoneMaskCheck.Verify(expected, smaller, "夹具小图"));
            StringAssert.Contains("尺寸", ex.Message);
        }

        // ══════════════════════ AC-4:烘焙 + 进组 ══════════════════════

        /// <summary>AC-4:address 登记在 `data-core` 预载表,且盘上有产物。</summary>
        /// <remarks><b>判别力</b>:从 `DataCoreAddresses` 删掉该条 ⇒ 本测红。
        /// <para>⚠️ 「组条目数 +1」的**实跑**证据在 `production/qa/evidence/impl-level-tool-story-003-*.md`
        /// (菜单自检输出),不在本测 —— EditMode 无 Addressables locator。</para></remarks>
        [Test]
        public void test_ac4_addressInPreloadList_andProductExists()
        {
            FieldInfo field = typeof(DataCorePreloader).GetField(
                "DataCoreAddresses", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.That(field, Is.Not.Null, "`DataCorePreloader.DataCoreAddresses` 不见了");
            var addresses = (string[])field.GetValue(null);

            Assert.That(addresses, Has.Member(AddressablesDataProvider.WorldEcozonesAddress));
            Assert.That(File.Exists(Path.Combine(CookedDir, WorldEcozonesDataSet.CookedFileName)), Is.True,
                "盘上缺 world_ecozones 产物 —— 先跑「烘焙 world_ecozones」菜单");
        }

        // ══════════════════════ AC-5:运行期装载面 ══════════════════════

        /// <summary>AC-5:装载路径给出**两个方向** —— 区内格回该区 id,区外格回 `None`。</summary>
        /// <remarks><b>判别力</b>:把 `LoadEcozoneRegistry` 退回 `new EcozoneRegistry()`(空表)⇒
        /// 「区内格回 0 / 1」两条断言同时红。只测区内会让「恒回 0」逃逸,故**两方向都测**。</remarks>
        [Test]
        public void test_ac5_loadEcozoneRegistry_insideReturnsId_outsideReturnsNone()
        {
            EcozoneRegistry registry = CompositionRoot.LoadEcozoneRegistry(null);   // 磁盘兜底 = 真产物

            Assert.That(registry.Count, Is.GreaterThanOrEqualTo(2),
                "真产物至少两个区;装载到空表 = 装载面没接");

            Assert.That(registry.EcozoneOf(new WorldPos(4, 0, 4)), Is.EqualTo(new EcozoneId(0)),
                "(4,4) 在 clinic_ward(x∈[1,8]) 内部 ⇒ 须回 0");
            Assert.That(registry.EcozoneOf(new WorldPos(10, 0, 4)), Is.EqualTo(new EcozoneId(1)),
                "(10,4) 在 herb_courtyard(x∈[8,14]) 内部 ⇒ 须回 1(只回 0 的实现会在这里红)");
            Assert.That(registry.EcozoneOf(new WorldPos(0, 0, 0)), Is.EqualTo(EcozoneId.None),
                "(0,0) 在两区之外 ⇒ 须回 None(`NONE` 是合法结果,GDD EC-6-10)");
        }

        /// <summary>AC-5:装配根**真的走了**装载路径(不是仍挂着空表)。</summary>
        /// <remarks><b>判别力</b>:把 `AssembleCore` 改回 `new EcozoneRegistry()`,或删掉
        /// `LoadEcozoneRegistry(dataProvider)` 调用 ⇒ 本测红。
        /// <para>⚠️ 判据是**源码扫描**(承 `world_assembly_wiring_test` 对 `BootRoot` 的同款做法):
        /// 「装出来的表非空」无法区分「装载了」与「别处塞了数据」,而这里要钉的是**接线**。</para></remarks>
        [Test]
        public void test_ac5_compositionRoot_wiresLoadEcozoneRegistry_notEmptyTable()
        {
            string path = Path.Combine(RepoRoot, "unity", "Assets", "Gameplay.Boot", "CompositionRoot.cs");
            Assert.That(File.Exists(path), Is.True, $"找不到 {path}");
            string code = File.ReadAllText(path);

            Assert.That(code, Does.Contain("LoadEcozoneRegistry(dataProvider)"),
                "装配根须调用 `LoadEcozoneRegistry(dataProvider)`(story-003 AC-5)");
            Assert.That(Regex.IsMatch(code, @"RegistryClinicKnowledgeSource\(\s*new EcozoneRegistry\(\)\s*\)"),
                Is.False,
                "装配根仍在直接 `new EcozoneRegistry()` 喂给知识源 —— 那就是空表,AC-5 未兑现");
        }

        // ══════════════════════ 工具 ══════════════════════

        private static EcozoneZoneSpec Zone(int id, params int[] xzPairs)
        {
            var v = new WorldPos[xzPairs.Length / 2];
            for (int i = 0; i < v.Length; i++)
                v[i] = new WorldPos(xzPairs[2 * i], 0, xzPairs[2 * i + 1]);
            return new EcozoneZoneSpec(id, "zone_" + id, v);
        }

        private static EcozoneSource Source(params EcozoneZoneSpec[] zones)
            => new EcozoneSource(new List<EcozoneZoneSpec>(zones));

        /// <summary>断言 C4 报出**含指定关键词**的违例(不抛形的 `TryCheck` 面)。</summary>
        private static void AssertViolation(EcozoneSource src, params string[] keywords)
        {
            Assert.That(EcozonePolygonCheck.TryCheck(src, out List<string> v), Is.True,
                "该退化**须**被 C4 拒掉,实际全绿 —— 判据缺失或打错靶");

            string joined = string.Join("\n", v);
            foreach (string k in keywords)
                Assert.That(joined, Does.Contain(k),
                    $"违例报文须点名判据关键词「{k}」;实际:\n{joined}");

            // 抛形是生产路径(ADR-022 §②:C4 是构建期失败通道,不是告警)。
            Assert.Throws<InvalidOperationException>(() => EcozonePolygonCheck.CheckOrThrow(src));
        }
    }
}
