// 关卡工具 story-004 —— **POI 定义 / 资源点**(AC-1 … AC-6)的夹具。
//
// 权威来源:
//   `production/epics/level-tool/story-004-world-poi-and-resources.md`(AC-1 … AC-6)
//   ADR-022 §三(导出契约)· §六(边界表:「POI 状态:✖ 不碰」)· ADR-021 §①(定义 = 派生态)
//   ADR-015 §三(逻辑层只有整数格)
//
// ⚠️ **零写盘 / 零随机 / 零时间依赖**。
//
// ⚠️ **两个方向都测**(AC-4 / AC-5 逐字要求):只测「有 POI 的格」时,一个
//    「全填同一个 id」的实现会全绿 —— 必须同时测「非 POI 格仍是 -1」。
//
// ⚠️ 本件**不证**:POI 状态转移是否真发生(归 world-ecozones/story-005 的走查)·
//    资源是否真可采(归 17 的实现轮)· `guard_ref` 档位是否合理(数值轮)。

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using DaYiJingCheng.EditorTools.Bake;    // WorldPoiBake / WorldResourcesBake(烘焙确定性 + 坏产物构造)
using DaYiJingCheng.EditorTools.Level;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.Presentation;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.LevelTool
{
    /// <summary>story-004:POI 定义 / 资源点的导出形状、烘焙确定性、装载面两方向、工具边界。</summary>
    [TestFixture]
    public class poi_and_resource_load_test
    {
        private static readonly string RepoRoot = ComputeRepoRoot();

        private static string ComputeRepoRoot(
            [System.Runtime.CompilerServices.CallerFilePath] string thisFile = "")
            => Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(thisFile) ?? ".", "..", "..", "..", "..", ".."));

        private static string CookedDir => Path.Combine(RepoRoot, "unity", "Assets", "DataCooked");

        // ══════════════════════ AC-1:world_poi 的导出形状 ══════════════════════

        /// <summary>AC-1:`poi_id` 唯一且 ≥ 0,格全整数,同一格不出现两条,零浮点。</summary>
        /// <remarks><b>判别力</b>:导出器把 `poi_id` 写成常量 ⇒ 唯一性那条红;
        /// 顶点写成 `1.0` ⇒ 零浮点那条红。</remarks>
        [Test]
        public void test_ac1_export_poiIdsUnique_nonNegativeCellsDistinct_zeroFloat()
        {
            string json = LevelExporter.ExportPoi(LevelCatalog.Define(P0ClinicLevel.LevelId));

            Assert.That(Regex.IsMatch(json, @"[0-9]+\.[0-9]+"), Is.False,
                "产物里出现了小数写法 —— 逻辑层禁浮点(ADR-015 §三);实际产物:\n" + json);

            MatchCollection ids = Regex.Matches(json, "\"poi_id\": (-?[0-9]+)");
            var seenIds = new HashSet<int>();
            foreach (Match m in ids)
            {
                int id = int.Parse(m.Groups[1].Value);
                Assert.That(id, Is.GreaterThanOrEqualTo(0),
                    "poi_id 须 ≥ 0(-1 是「本格无 POI」的哨兵)");
                Assert.That(seenIds.Add(id), Is.True, $"poi_id {id} 重复");
            }
            Assert.That(seenIds.Count, Is.GreaterThanOrEqualTo(2),
                "真种子须至少两条 POI(AC-4 的「有 POI 的格」才有对象)");

            // 格三元组:全整数,且同一格不出现两次。
            MatchCollection cells = Regex.Matches(json, "\"cell\": \\[(-?[0-9]+), (-?[0-9]+), (-?[0-9]+)\\]");
            Assert.That(cells.Count, Is.EqualTo(ids.Count),
                "每行须带一个 [x, y, z] 三元组 —— 数目与 poi_id 不符说明形状漂了");
            var seenCells = new HashSet<string>();
            foreach (Match m in cells)
            {
                Assert.That(seenCells.Add(m.Value), Is.True,
                    $"同一格声明了两条 POI:{m.Value}(会变成「取哪一条」的隐式规则)");
            }
        }

        /// <summary>AC-1:`poi_id` 重复 ⇒ 构造即抛。</summary>
        [Test]
        public void test_ac1_duplicatePoiId_throws()
        {
            var ex = Assert.Throws<ArgumentException>(() => new PoiSource(new List<PoiSpec>
            {
                new PoiSpec(7, new WorldPos(1, 0, 1), 1, 0),
                new PoiSpec(7, new WorldPos(2, 0, 2), 1, 0),
            }));
            StringAssert.Contains("重复", ex.Message);
        }

        /// <summary>AC-1:POI 的格越出关卡范围 ⇒ 导出硬失败(不钳位)。</summary>
        /// <remarks><b>判别力</b>:停用 `PoiSource.ValidateAgainstLevel` 的 `bad.Count &gt; 0` 哨兵 ⇒ 本测红。</remarks>
        [Test]
        public void test_ac1_poiCellOutOfLevelRange_throws()
        {
            var src = new LevelSource(
                "probe_poi_oor", 4, 1, 4,
                new List<SlotSpec> { new SlotSpec(1, 0, 1, SlotType.Decor, false) },
                poi: new PoiSource(new List<PoiSpec>
                {
                    new PoiSpec(0, new WorldPos(9, 0, 9), 1, 0),
                }));

            var ex = Assert.Throws<InvalidOperationException>(() => LevelExporter.ExportPoi(src));
            StringAssert.Contains("越界", ex.Message);
        }

        /// <summary>AC-1:同一格声明两条 POI ⇒ 构造即抛。</summary>
        [Test]
        public void test_ac1_twoPoisOnSameCell_throws()
        {
            var ex = Assert.Throws<ArgumentException>(() => new PoiSource(new List<PoiSpec>
            {
                new PoiSpec(0, new WorldPos(3, 0, 3), 1, 0),
                new PoiSpec(1, new WorldPos(3, 0, 3), 2, 0),
            }));
            StringAssert.Contains("两", ex.Message);
        }

        /// <summary>AC-1:`poi_id` 为负 ⇒ 构造即抛(`-1` 是「本格无 POI」的哨兵)。</summary>
        /// <remarks><b>判别力</b>:停用 `PoiId &lt; 0` 判据 ⇒ 本测红(构造成功,`Assert.Throws` 失败)。</remarks>
        [Test]
        public void test_ac1_negativePoiId_throws()
        {
            var ex = Assert.Throws<ArgumentException>(() => new PoiSource(new List<PoiSpec>
            {
                new PoiSpec(-1, new WorldPos(1, 0, 1), 1, 0),
            }));
            StringAssert.Contains("-1", ex.Message);
        }

        // ══════════════════════ AC-2:world_resources 的导出形状 ══════════════════════

        /// <summary>AC-2:资源 id ≥ 0,格全整数且在声明范围内,零浮点。</summary>
        [Test]
        public void test_ac2_export_resourceIdsNonNegative_cellsInRange_zeroFloat()
        {
            LevelSource src = LevelCatalog.Define(P0ClinicLevel.LevelId);
            string json = LevelExporter.ExportResources(src);

            Assert.That(Regex.IsMatch(json, @"[0-9]+\.[0-9]+"), Is.False,
                "产物里出现了小数写法 —— 逻辑层禁浮点;实际产物:\n" + json);

            MatchCollection ids = Regex.Matches(json, "\"resource_id\": (-?[0-9]+)");
            Assert.That(ids.Count, Is.GreaterThanOrEqualTo(2), "真种子须至少两个资源点");
            foreach (Match m in ids)
                Assert.That(int.Parse(m.Groups[1].Value), Is.GreaterThanOrEqualTo(0),
                    "resource_id 须 ≥ 0(-1 是「本格无资源」的哨兵)");

            MatchCollection cells = Regex.Matches(json, "\"cell\": \\[(-?[0-9]+), (-?[0-9]+), (-?[0-9]+)\\]");
            foreach (Match m in cells)
            {
                int x = int.Parse(m.Groups[1].Value);
                int y = int.Parse(m.Groups[2].Value);
                int z = int.Parse(m.Groups[3].Value);
                Assert.That(x >= 0 && x < src.Width && y >= 0 && y < src.Height &&
                            z >= 0 && z < src.Depth, Is.True,
                    $"格 ({x},{y},{z}) 超出 {src.Width}×{src.Height}×{src.Depth}");
            }
        }

        /// <summary>AC-2③:资源点落在 POI 格上 ⇒ 导出硬失败。</summary>
        /// <remarks><b>判别力</b>:停用 `ExportResources` 的 POI 格交叉判据 ⇒ 本测红
        /// (`ExportResources` 不抛,`Assert.Throws` 失败)。</remarks>
        [Test]
        public void test_ac2_resourceOnPoiCell_throws()
        {
            var src = new LevelSource(
                "probe_clash", 4, 1, 4,
                new List<SlotSpec> { new SlotSpec(1, 0, 1, SlotType.Decor, false) },
                poi: new PoiSource(new List<PoiSpec> { new PoiSpec(0, new WorldPos(2, 0, 2), 1, 0) }),
                resources: new ResourceSource(new List<ResourceSpec>
                {
                    new ResourceSpec(1, new WorldPos(2, 0, 2)),   // 与上面的 POI 同格
                }));

            var ex = Assert.Throws<InvalidOperationException>(() => LevelExporter.ExportResources(src));
            StringAssert.Contains("冲突", ex.Message);
        }

        /// <summary>AC-2:资源 id 为负 ⇒ 构造即抛(`-1` 是「本格无资源」的哨兵)。</summary>
        /// <remarks><b>判别力</b>:停用 `ResourceId &lt; 0` 判据 ⇒ 本测红。</remarks>
        [Test]
        public void test_ac2_negativeResourceId_throws()
        {
            var ex = Assert.Throws<ArgumentException>(() => new ResourceSource(new List<ResourceSpec>
            {
                new ResourceSpec(-1, new WorldPos(1, 0, 1)),
            }));
            StringAssert.Contains("-1", ex.Message);
        }

        /// <summary>AC-2①:格越出关卡范围 ⇒ 导出硬失败(不钳位)。</summary>
        [Test]
        public void test_ac2_cellOutOfLevelRange_throws()
        {
            var src = new LevelSource(
                "probe_oor", 4, 1, 4,
                new List<SlotSpec> { new SlotSpec(1, 0, 1, SlotType.Decor, false) },
                resources: new ResourceSource(new List<ResourceSpec>
                {
                    new ResourceSpec(1, new WorldPos(9, 0, 9)),
                }));

            var ex = Assert.Throws<InvalidOperationException>(() => LevelExporter.ExportResources(src));
            StringAssert.Contains("越界", ex.Message);
        }

        // ══════════════════════ AC-3:烘焙 + 进组 ══════════════════════

        /// <summary>AC-3:同源两次烘焙 ⇒ 逐字节相同(两份产物各一次)。</summary>
        /// <remarks><b>判别力</b>:让排序键掺入非确定性(例如只按 id 排、相等元素次序不定)⇒ 红。</remarks>
        [Test]
        public void test_ac3_bakeTwice_bothProductsBitIdentical()
        {
            LevelSource src = LevelCatalog.Define(P0ClinicLevel.LevelId);

            byte[] poiA = WorldPoiBake.BakeFromSourceText(LevelExporter.ExportPoi(src)).Cooked;
            byte[] poiB = WorldPoiBake.BakeFromSourceText(LevelExporter.ExportPoi(src)).Cooked;
            Assert.That(poiA, Is.EqualTo(poiB), "world_poi 两次烘焙字节不同(ADR-014 §二)");

            byte[] resA = WorldResourcesBake.BakeFromSourceText(LevelExporter.ExportResources(src)).Cooked;
            byte[] resB = WorldResourcesBake.BakeFromSourceText(LevelExporter.ExportResources(src)).Cooked;
            Assert.That(resA, Is.EqualTo(resB), "world_resources 两次烘焙字节不同(ADR-014 §二)");
        }

        /// <summary>AC-3:两个 address 都在 `data-core` 预载表里,且盘上有产物。</summary>
        /// <remarks><b>判别力</b>:从 `DataCoreAddresses` 删任一条 ⇒ 红。
        /// <para>⚠️ 「组条目数 +2」的实跑证据在证据件(菜单自检输出),不在本测 —— EditMode 无 locator。</para></remarks>
        [Test]
        public void test_ac3_bothAddressesInPreloadList_andProductsExist()
        {
            FieldInfo field = typeof(DataCorePreloader).GetField(
                "DataCoreAddresses", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.That(field, Is.Not.Null, "`DataCorePreloader.DataCoreAddresses` 不见了");
            var addresses = (string[])field.GetValue(null);

            Assert.That(addresses, Has.Member(AddressablesDataProvider.WorldPoiAddress));
            Assert.That(addresses, Has.Member(AddressablesDataProvider.WorldResourcesAddress));
            Assert.That(File.Exists(Path.Combine(CookedDir, WorldPoiDataSet.CookedFileName)), Is.True,
                "盘上缺 world_poi 产物 —— 先跑「烘焙 world_poi」菜单");
            Assert.That(File.Exists(Path.Combine(CookedDir, WorldResourcesDataSet.CookedFileName)), Is.True,
                "盘上缺 world_resources 产物 —— 先跑「烘焙 world_resources」菜单");
        }

        // ══════════════════════ AC-4:POI 装载面 ══════════════════════

        /// <summary>AC-4①:POI 格的 `PoiGrid` == 该 poi_id,**非 POI 格仍是 -1**(两个方向)。</summary>
        /// <remarks><b>判别力</b>:把工厂改回「全 -1」⇒ 第一条红;改成「全填同一个 id」⇒ 第二条红。</remarks>
        [Test]
        public void test_ac4_poiGrid_poiCellReturnsId_nonPoiCellReturnsMinusOne()
        {
            World world = MinimalWorldFactory.Create(null);   // 磁盘兜底 = 真产物

            Assert.That(world.Geometry.GetPoiId(new WorldPos(4, 0, 4)), Is.EqualTo(0),
                "(4,0,4) 在真产物里是 poi_id 0");
            Assert.That(world.Geometry.GetPoiId(new WorldPos(11, 0, 7)), Is.EqualTo(1),
                "(11,0,7) 在真产物里是 poi_id 1 —— 只回 0 的实现会在这里红");
            Assert.That(world.Geometry.GetPoiId(new WorldPos(0, 0, 0)), Is.EqualTo(-1),
                "非 POI 格须回 -1(「全填同一个 id」的实现会在这里红)");
        }

        /// <summary>AC-4②:装载面给出的 id 集能驱动 `PoiStateMachine` 发 `PoiStateChanged`。</summary>
        /// <remarks>
        /// <para><b>判别力</b>:让 `LoadPoiIds` 返回空集 ⇒ `TryAdvance` 回 `PoiNotFound`、零事件 ⇒ 红。</para>
        /// <para>⚠️ <b>本测刻意**不经** `CompositionRoot.Assemble`</b>:`PoiStateMachine` 的构造需要一个
        /// <see cref="IEventAuthority"/> 实装,而全仓**零生产实装**(`SeenDiseaseProjector.cs:23-26`
        /// 把它登记为 P1b 缺口)。在装配根里塞一个生产占位 = 私自关掉那条已登记的缺口;
        /// 故本 story 只接**定义侧**(`LoadPoiIds`),机器本身在夹具里用测试替身构造。
        /// 这条边界已登记进证据件 §七。</para>
        /// </remarks>
        [Test]
        public void test_ac4_poiStateMachine_idsFromArtifact_canEmitPoiStateChanged()
        {
            IReadOnlyList<int> ids = CompositionRoot.LoadPoiIds(null);
            Assert.That(ids.Count, Is.GreaterThanOrEqualTo(2),
                "真产物至少两条 POI;装载到空集 = 装载面没接");

            var sink = new DaYiJingCheng.Tests.WorldEcozones.SpyEventSink();
            var pool = new InMemoryBlobPool();
            var machine = new PoiStateMachine(sink, new FakeEventAuthority(), ids, new PayloadEncoder(pool));

            Assert.That(machine.Count, Is.EqualTo(ids.Count),
                "登记的 id 集须原样进 `_poiIdSet`");

            PoiStateTransferResult result = machine.TryAdvance(ids[0], PoiState.Discovered, tick: 5);
            Assert.That(result, Is.EqualTo(PoiStateTransferResult.Success),
                "已登记的 poi_id 须能转移;回 PoiNotFound = 定义集没进机器");
            Assert.That(sink.AppendedEvents.Count, Is.EqualTo(1), "恰一条 PoiStateChanged");
            Assert.That(sink.AppendedEvents[0].Kind, Is.EqualTo(EventKind.PoiStateChanged));
        }

        /// <summary>AC-4:`LoadPoiIds` 的 id 集 == 作者态产物的 `poi_id` 集(单向一致)。</summary>
        [Test]
        public void test_ac4_loadPoiIds_matchesProductDefinitions()
        {
            IReadOnlyList<int> ids = CompositionRoot.LoadPoiIds(null);
            string json = File.ReadAllText(
                Path.Combine(RepoRoot, "assets", "data", "world_poi.json"), System.Text.Encoding.UTF8);

            MatchCollection matches = Regex.Matches(json, "\"poi_id\": (-?[0-9]+)");
            var expected = new List<int>();
            foreach (Match m in matches) expected.Add(int.Parse(m.Groups[1].Value));
            expected.Sort();

            Assert.That(ids, Is.EqualTo(expected),
                "装载面的 id 集与作者态产物不一致 —— 产物陈旧或装载面读错了文件");
        }

        // ══════════════════════ AC-5:资源装载面 ══════════════════════

        /// <summary>AC-5:资源格的 `ResourceGrid` == 该 resource_id,**非资源格仍是 -1**(两个方向)。</summary>
        [Test]
        public void test_ac5_resourceGrid_resourceCellReturnsId_nonResourceCellReturnsMinusOne()
        {
            World world = MinimalWorldFactory.Create(null);

            Assert.That(world.Geometry.GetResourceId(new WorldPos(2, 0, 2)), Is.EqualTo(1),
                "(2,0,2) 在真产物里是 resource_id 1");
            Assert.That(world.Geometry.GetResourceId(new WorldPos(10, 0, 2)), Is.EqualTo(2),
                "(10,0,2) 是 resource_id 2 —— 只回一个 id 的实现会在这里红");
            Assert.That(world.Geometry.GetResourceId(new WorldPos(0, 0, 0)), Is.EqualTo(-1),
                "非资源格须回 -1");
        }

        /// <summary>AC-5(读侧纵深防御):产物里同格两行 ⇒ 解码硬失败。</summary>
        /// <remarks>
        /// <para>工具侧 `ResourceSource` 构造期已拒;本测打的是**读侧**那一层 ——
        /// 产物可以被手工改(ADR-022 §二 明写「不得手写」,但门要能抓住它)。</para>
        /// <para>⚠️ 构造这份坏产物需要绕过写入器 ⇒ 直接拼字节(20 B 头 + 2 行同格)。</para>
        /// </remarks>
        [Test]
        public void test_ac5_codec_rejectsDuplicateCell()
        {
            byte[] good = WorldResourcesBake.BakeFromSourceText(
                LevelExporter.ExportResources(LevelCatalog.Define(P0ClinicLevel.LevelId))).Cooked;

            // 头部 20 B 原样保留;载荷换成「两行同格」。
            int header = 20;
            byte[] payload = new byte[4 + 2 * 20];
            using (var ms = new MemoryStream(payload))
            using (var w = new BinaryWriter(ms))
            {
                w.Write(2);                       // res_count = 2
                w.Write(1); w.Write(3); w.Write(0); w.Write(3); w.Write(0);   // 行 1 @ (3,0,3)
                w.Write(2); w.Write(3); w.Write(0); w.Write(3); w.Write(1);   // 行 2 @ (3,0,3) —— 同格
            }
            var bad = new byte[header + payload.Length];
            Array.Copy(good, 0, bad, 0, header);
            Array.Copy(payload, 0, bad, header, payload.Length);
            // ⚠️ 头部 offset 16 是**载荷长度**(CookedFormat 契约),`OpenChecked` 会拿它和实际长度对账。
            //    只换载荷而不改它 ⇒ 抛的是「载荷长度不符」,本测就会**因为错的理由**变绿/变红。
            int len = payload.Length;
            bad[16] = (byte)(len & 0xFF);
            bad[17] = (byte)((len >> 8) & 0xFF);
            bad[18] = (byte)((len >> 16) & 0xFF);
            bad[19] = (byte)((len >> 24) & 0xFF);

            var ex = Assert.Throws<InvalidDataException>(
                () => WorldResourcesCookedCodec.Read(bad));
            StringAssert.Contains("两行", ex.Message);
        }

        // ══════════════════════ AC-6:工具不碰状态 ══════════════════════

        /// <summary>AC-6:`Editor.Tools.Level` 全量源文本(**剥注释后**)零 `PoiState` 符号。</summary>
        /// <remarks>
        /// <para>⚠️ **剥注释是判据的一部分**:注释里提到 `PoiState`(解释「为什么不碰」)是**正常**的,
        /// 不剥会把说明文字判成违例。</para>
        /// <para>⚠️ 本判据是**文本级**的,不是语义级 —— 它证明「工具源码里没有 `PoiState` 符号」,
        /// **不证明**「工具在语义上真的不需要状态」。若将来需要,该 AC 会红,
        /// 那时须回 ADR-022 追加裁决,不是改判据。</para>
        /// </remarks>
        [Test]
        public void test_ac6_toolAssembly_hasNoPoiStateSymbol()
        {
            string toolDir = Path.Combine(RepoRoot, "unity", "Assets", "Editor.Tools.Level");
            Assert.That(Directory.Exists(toolDir), Is.True, $"找不到 {toolDir}");

            string[] files = Directory.GetFiles(toolDir, "*.cs", SearchOption.TopDirectoryOnly);
            Assert.That(files.Length, Is.GreaterThan(0), "工具程序集里零 .cs 文件 —— 路径写错了");

            var offenders = new List<string>();
            foreach (string f in files)
            {
                string code = StripComments(File.ReadAllText(f, System.Text.Encoding.UTF8));
                if (code.Contains("PoiState")) offenders.Add(Path.GetFileName(f));
            }

            Assert.That(offenders, Is.Empty,
                "工具程序集里出现了 `PoiState` 符号(剥注释后):" + string.Join(", ", offenders) +
                " —— ADR-022 §六:「POI 状态:✖ 不碰」。真需要读状态须回 ADR 追加裁决");
        }

        /// <summary>剥掉 `//` 行注释与 `/* */` 块注释(字符串字面量里的 `//` 不误伤)。</summary>
        private static string StripComments(string src)
        {
            var sb = new System.Text.StringBuilder(src.Length);
            bool inLine = false, inBlock = false, inString = false, inChar = false;

            for (int i = 0; i < src.Length; i++)
            {
                char c = src[i];

                if (inLine) { if (c == '\n') { inLine = false; sb.Append(c); } continue; }
                if (inBlock)
                {
                    if (c == '*' && i + 1 < src.Length && src[i + 1] == '/') { inBlock = false; i++; }
                    continue;
                }
                if (inString)
                {
                    sb.Append(c);
                    if (c == '\\' && i + 1 < src.Length) { sb.Append(src[++i]); continue; }
                    if (c == '"') inString = false;
                    continue;
                }
                if (inChar)
                {
                    sb.Append(c);
                    if (c == '\\' && i + 1 < src.Length) { sb.Append(src[++i]); continue; }
                    if (c == '\'') inChar = false;
                    continue;
                }

                if (c == '/' && i + 1 < src.Length && src[i + 1] == '/') { inLine = true; i++; continue; }
                if (c == '/' && i + 1 < src.Length && src[i + 1] == '*') { inBlock = true; i++; continue; }
                if (c == '"') { inString = true; sb.Append(c); continue; }
                if (c == '\'') { inChar = true; sb.Append(c); continue; }

                sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
