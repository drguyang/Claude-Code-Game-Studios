// 关卡工具 story-001 —— **关卡源泛化 + 数据集类别**(AC-1 … AC-5)的夹具。
//
// 权威来源:
//   `production/epics/level-tool/story-001-level-source-generalization.md`(AC-1…AC-5)
//   ADR-022 §三(导出契约 7 类)· §五(导航格按 chunk 切片 —— 已有「参数化后缀」先例)
//
// ⚠️ **本件不写盘**(不触 `FsWriteBoundaryGate`):全部断言走
//    `LevelExporter.ExportBuildSlots` / `LevelExporter.Export`(只返回字符串)。
//    两条 batch 入口的**实跑**不属于夹具面(需要 `-executeMethod`),由证据件的实跑记录承担;
//    本件只钉「入口存在且形状正确」,如实登记该边界。
//
// ⚠️ **判据一律用字面量,不引用被测类的常量**(自指判据:变异时两边一起动 ⇒ 零红逃逸)。
//    典型:`"world_buildslots.json"` 直接写死,而不是 `LevelExporter.BuildSlotsFileName`。
//
// ⚠️ **AC-1 的判别力边界(如实登记)**:「构造 8×2×8 导出 ⇒ world_size 为 [8,2,8]」这条判据
//    在**改动前就已经成立**(`ExportBuildSlots` 一直读 `src.Width/Height/Depth`)。
//    它对「尺寸来自数据」**没有判别力**;真正的判别面是另两条:
//    ① 反射钉住 `P0ClinicLevel` **不再声明** `Width`/`Height`/`Depth`(「第二条路」被删掉);
//    ② 反证轮里把导出器写死 `[16,1,16]` ⇒ 本判据转红(证明它**能**抓住那类回归)。

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using DaYiJingCheng.EditorTools.Level;
using DaYiJingCheng.Sim.World;   // SlotType

namespace DaYiJingCheng.Tests.LevelTool
{
    /// <summary>关卡源泛化(多关卡 · 尺寸来自数据 · 数据集类别分派)。</summary>
    [TestFixture]
    public class level_source_generalization_test
    {
        /// <summary>
        /// AC-3 的金标准:**重构前** `assets/data/world_buildslots.json` 的 SHA-256
        /// (`2026-10-29` 改动前实测;文件 1249 B · 无 BOM · 纯 LF)。
        /// <para>⚠️ 该值**不是**被测类的常量 —— 它是**改动前产物**的指纹 ⇒ 重构若改了任何一个字节都会红。</para>
        /// <para>⚠️ 若将来 P0 种子**合法变更**(槽位表真的改了),本字面量须与
        /// `assets/data/world_buildslots.json` **同批重签**(承 ADR-012 金标准「版本化刷新」纪律,
        /// 禁止只改一边)。</para>
        /// </summary>
        private const string PreRefactorBuildSlotsSha256 =
            "1c946bff60177ff294f4e3bcf4a235e67b4e5067ba07db105047d57762074c6d";

        // ─────────────────────────── AC-1 ───────────────────────────

        /// <summary>AC-1:**尺寸来自数据** —— 手造 8×2×8 的关卡源,导出后 `world_size` 恰为 `[8, 2, 8]`。</summary>
        /// <remarks><b>判别力</b>:把导出器改成写死 `[16, 1, 16]`(或改成读 `P0ClinicLevel` 的旧常量)
        /// ⇒ 本测红。⚠️ 该判据在改动前**已经成立**,见文件头「AC-1 的判别力边界」。</remarks>
        [Test]
        public void test_ac1_sizeComesFromData_eightByTwoByEightExport()
        {
            var src = new LevelSource("probe_level", 8, 2, 8, new List<SlotSpec>
            {
                new SlotSpec(0, 0, 0, SlotType.Decor, false),
                new SlotSpec(7, 1, 7, SlotType.Shell, true, 8),
            });

            string json = LevelExporter.ExportBuildSlots(src);

            Assert.That(json, Does.Contain("\"world_size\": [8, 2, 8]"),
                "尺寸须逐轴取自 LevelSource,实际产物:\n" + json);
            Assert.That(json, Does.Not.Contain("[16, 1, 16]"),
                "产物不得出现 P0 医馆的尺寸 —— 出现即说明尺寸仍来自关卡常量,而非数据");
            Assert.That(json, Does.Contain("\"level_id\": \"probe_level\""),
                "关卡 id 亦须取自 LevelSource");
        }

        /// <summary>AC-1:`P0ClinicLevel` **不再声明** `Width` / `Height` / `Depth` 三个尺寸成员。</summary>
        /// <remarks><b>为什么这条才是 AC-1 的真判别面**:AC-1 要的不是「能导出 8×2×8」(本来就能),
        /// 而是「**第二条读尺寸的路**被删掉」—— 那三个公开常量正是那条路。
        /// <b>判别力</b>:把任一常量加回来 ⇒ 本测红。</remarks>
        [Test]
        public void test_ac1_p0ClinicLevel_noLongerDeclaresSizeConstants()
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic
                                     | BindingFlags.Static | BindingFlags.DeclaredOnly;
            foreach (string name in new[] { "Width", "Height", "Depth" })
            {
                Assert.That(typeof(P0ClinicLevel).GetMember(name, flags), Is.Empty,
                    $"P0ClinicLevel 仍声明了 `{name}` —— 尺寸的唯一载体必须是 LevelSource" +
                    "(story-001 AC-1;留常量 = 留着绕过 LevelSource 的路)");
            }
        }

        /// <summary>AC-1(回归面):基准关卡**仍然**是 16×1×16 —— 删常量不等于把种子改小。</summary>
        [Test]
        public void test_ac1_canonicalLevel_keepsItsSixteenByOneBySixteenSize()
        {
            LevelSource src = LevelCatalog.Define("p0_clinic");

            Assert.That(src.Width, Is.EqualTo(16));
            Assert.That(src.Height, Is.EqualTo(1));
            Assert.That(src.Depth, Is.EqualTo(16));
            Assert.That(src.Slots.Count, Is.EqualTo(13), "P0 种子 = 9 Decor + 4 Shell(既有事实)");
        }

        // ─────────────────────────── AC-2 ───────────────────────────

        /// <summary>AC-2:**至少两个关卡并存**,尺寸不同,都过一致性检查,导出内容不同。</summary>
        [Test]
        public void test_ac2_twoLevels_differInSize_bothPassConsistencyCheck()
        {
            Assert.That(LevelCatalog.LevelIds.Count, Is.GreaterThanOrEqualTo(2),
                "AC-2 要求至少两个关卡:[" + string.Join(", ", LevelCatalog.LevelIds) + "]");

            LevelSource a = LevelCatalog.Define("p0_clinic");
            LevelSource b = LevelCatalog.Define("scaffold_second");

            Assert.That((a.Width, a.Height, a.Depth), Is.Not.EqualTo((b.Width, b.Height, b.Depth)),
                "AC-2 的反例明写:复制一份改个名字不算 —— 两关的尺寸必须不同");

            CheckReport ra = LevelConsistencyCheck.Check(a);
            CheckReport rb = LevelConsistencyCheck.Check(b);
            Assert.That(ra.IsGreen, Is.True, "关卡 A 须全绿:\n" + string.Join("\n", ra.Violations));
            Assert.That(rb.IsGreen, Is.True, "关卡 B 须全绿:\n" + string.Join("\n", rb.Violations));

            Assert.That(LevelExporter.ExportBuildSlots(a), Is.Not.EqualTo(LevelExporter.ExportBuildSlots(b)),
                "两关的导出内容须不同(尺寸行 + 槽位行)");
        }

        /// <summary>AC-2(反例守卫):第二关卡**不是**第一关的复制品 —— 它在第二层(y = 1)有槽位。</summary>
        /// <remarks>基准关卡是单层(`Height = 1` ⇒ 全部槽位 `y = 0`)。「复制改名字」的产物
        /// 不可能有 `y = 1` 的槽位 ⇒ 本条是对 AC-2 反例最直接的机械判据。</remarks>
        [Test]
        public void test_ac2_secondLevel_isNotACopyOfFirst_hasSlotsOnSecondLayer()
        {
            LevelSource b = LevelCatalog.Define("scaffold_second");

            bool hasUpperLayer = false;
            foreach (SlotSpec s in b.Slots)
            {
                if (s.Y == 1) hasUpperLayer = true;
                if (s.Y < 0 || s.Y >= b.Height)
                    Assert.Fail($"槽位 y = {s.Y} 超出本关声明高度 {b.Height}");
            }

            Assert.That(hasUpperLayer, Is.True,
                "第二关卡须在 y = 1 有槽位 —— 否则它只是第一关的复制品(AC-2 的反例)");
        }

        /// <summary>未知关卡 id ⇒ 抛,且报文点名**已登记的 id**(不是空话)。</summary>
        [Test]
        public void test_ac2_unknownLevelId_throwsListingKnownIds()
        {
            var ex = Assert.Throws<ArgumentException>(
                () => LevelCatalog.Define("no_such_level"));

            Assert.That(ex.Message, Does.Contain("p0_clinic"));
            Assert.That(ex.Message, Does.Contain("scaffold_second"));
            Assert.That(ex.Message, Does.Contain("no_such_level"));
        }

        // ─────────────────────────── AC-3 ───────────────────────────

        /// <summary>AC-3(金标准):走**新分派路径**导出的字节 = **重构前**盘上产物的 SHA-256。</summary>
        /// <remarks><b>这是本 story 唯一的「零行为变化」判据</b>(承 `feedback_refactor_falsifiability_ceiling`:
        /// 新旧写法值相等时夹具分不出对错,唯一可用的哨兵是字面量哈希)。
        /// <b>判别力</b>:新路径漏任一字段(`module_id` 写成省略而非 `null`)⇒ 字节不同 ⇒ 红。</remarks>
        [Test]
        public void test_ac3_newDispatchPath_bytesEqualPreRefactorGolden()
        {
            string viaDispatch = LevelExporter.Export(WorldDataSetKind.BuildSlots, LevelCatalog.Define("p0_clinic"));
            byte[] bytes = new UTF8Encoding(false).GetBytes(viaDispatch);
            string actual = Sha256Hex(bytes);

            Assert.That(actual, Is.EqualTo(PreRefactorBuildSlotsSha256),
                "分派路径的产物与重构前的 `assets/data/world_buildslots.json` 不再逐字节相同 —— " +
                "这是**行为变化**,不是重构。产物长度 = " + bytes.Length);
        }

        /// <summary>AC-3(等价):分派路径与直调序列化器的返回值**逐字符相同**。</summary>
        [Test]
        public void test_ac3_dispatchPath_equalsDirectSerializer()
        {
            LevelSource src = LevelCatalog.Define("p0_clinic");
            Assert.That(LevelExporter.Export(WorldDataSetKind.BuildSlots, src),
                        Is.EqualTo(LevelExporter.ExportBuildSlots(src)));
        }

        /// <summary>AC-3:`WorldDataSetKind` 闭集 = 7 类,且「已实现 ∪ 未实现」恰等于闭集、两者不交。</summary>
        [Test]
        public void test_ac3_kindClosedSet_partitionsIntoImplementedAndUnimplemented()
        {
            Assert.That(WorldDataSets.All.Count, Is.EqualTo(7),
                "ADR-022 §三 要求 7 类数据集");

            var distinct = new HashSet<WorldDataSetKind>(WorldDataSets.All);
            Assert.That(distinct.Count, Is.EqualTo(7), "闭集内不得有重复成员");

            foreach (WorldDataSetKind kind in WorldDataSets.All)
            {
                bool implemented = WorldDataSets.IsImplemented(kind);
                bool declaredUnimplemented = WorldDataSets.UnimplementedByStory.ContainsKey(kind);
                Assert.That(implemented ^ declaredUnimplemented, Is.True,
                    $"{kind} 同时「有序列化器」与「登记为未实现」,或两者都没有 —— 两个清单必须互补");
            }

            Assert.That(WorldDataSets.IsImplemented(WorldDataSetKind.BuildSlots), Is.True,
                "BuildSlots 自 S20-20 起已实现");
            // 🔁 **2026-10-29(关卡工具 story-002)**:Geometry / Terrain 也转已实现。
            //    ⚠️ 本条此前只钉 BuildSlots;现在必须逐条钉「该实现的都实现了」——
            //    否则「序列化器登记漏一条」会让 @story-002 的导出入口静默退回 NotSupported。
            Assert.That(WorldDataSets.IsImplemented(WorldDataSetKind.Geometry), Is.True,
                "Geometry 自 story-002 起已实现");
            Assert.That(WorldDataSets.IsImplemented(WorldDataSetKind.Terrain), Is.True,
                "Terrain 自 story-002 起已实现");
            // 🔁 **2026-10-29(关卡工具 story-003)**:Ecozones 也转已实现。
            Assert.That(WorldDataSets.IsImplemented(WorldDataSetKind.Ecozones), Is.True,
                "Ecozones 自 story-003 起已实现");
            Assert.That(WorldDataSets.UnimplementedByStory.Count, Is.EqualTo(3),
                "未实现类别 = Poi / Resources / NavChunk 三类 —— " +
                "条数变了说明有类别移了队却没同步另一侧");
        }

        /// <summary>AC-3:未实现类别 ⇒ 抛,报文点名**归属 story**(不是「未实现」四个字了事)。</summary>
        /// <remarks>⚠️ **本条的靶会随实现进度前移,这是刻意的**:它判的是「未实现类别 ⇒ 抛 + 点名归属」,
        /// 而「哪一类未实现」是**随时间变的**。
        /// · story-001:`Geometry` → story-002 实现后换 `Ecozones`
        /// · story-002:`Ecozones` → story-003 实现后换 `Poi`(归 S4)
        /// ⇒ 每条实现轮都得把它前移一格。**判据形态一字未变**(抛 + 报文含归属 story 号 + 含文件名)。</remarks>
        [Test]
        public void test_ac3_unimplementedKind_throwsNamingOwningStory()
        {
            LevelSource src = LevelCatalog.Define("p0_clinic");

            var ex = Assert.Throws<NotSupportedException>(
                () => LevelExporter.Export(WorldDataSetKind.Poi, src));

            Assert.That(ex.Message, Does.Contain("S4"), "报文须点名归属 story(S4)");
            Assert.That(ex.Message, Does.Contain("world_poi"));
        }

        // ─────────────────────────── AC-4 ───────────────────────────

        /// <summary>AC-4:`UnimplementedChecks` **仍是 7 条**(本 story 不实现任何检查),但措辞不再指向「本批」。</summary>
        /// <remarks><b>判别力</b>:把任一条改成已实现而删掉 ⇒ 长度 ≠ 7 ⇒ 红(越界);把「本批」写回 ⇒ 红。</remarks>
        [Test]
        public void test_ac4_unimplementedChecks_stillSeven_andNoLongerSaysThisBatch()
        {
            string[] items = LevelConsistencyCheck.UnimplementedChecks;

            // 🔁 **2026-10-29(关卡工具 story-003 AC-6)判据订正 7 → 5**,原文不删:
            //    story-001 时本项判「仍 7 条」—— 那时一条 C1–C6 都没实现。
            //    story-003 实现了 **C4 / C5** ⇒ 两条移出本表,剩 C1 / C2 / C2' / C3 / C6 五条。
            //    ⚠️ 判据形态不变:**变少 = 越界**(没实现却从表里删),**变多 = 闭集漂移**。
            Assert.That(items.Length, Is.EqualTo(5),
                "story-003 起 C4 / C5 已实现 ⇒ 未实现项须恰为 5 条(C1 / C2 / C2' / C3 / C6);" +
                "变少 = 越界(没实现却删了),变多 = 闭集漂移");

            foreach (string item in items)
            {
                Assert.That(item, Does.Not.Contain("本批"),
                    "「本批」指的是已不存在的 S20-20 最小卡;该措辞会让读者以为这些检查永远不会有:" + item);
                Assert.That(item, Does.Contain("待 S"),
                    "每条须点名归属 story(待 Sx 产出):" + item);
            }
        }

        // ─────────────────────────── AC-5 ───────────────────────────

        /// <summary>AC-5:两条 batch 入口**都存在**且形状正确(public static · 无参 · void)。</summary>
        /// <remarks>⚠️ **本测只钉形状,不钉「实跑成功」** —— 实跑要 `-executeMethod`,
        /// 归证据件的实跑记录。此处如实登记该边界(不为它借绿)。</remarks>
        [Test]
        public void test_ac5_twoParameterlessBatchEntriesExist()
        {
            foreach (string name in new[] { "ExportP0ClinicToRepo", "ExportScaffoldLevelToStaging" })
            {
                MethodInfo m = typeof(LevelExporter).GetMethod(
                    name, BindingFlags.Public | BindingFlags.Static);
                Assert.That(m, Is.Not.Null, $"batch 入口 `{name}` 不存在");
                Assert.That(m.GetParameters().Length, Is.EqualTo(0),
                    $"batch 入口 `{name}` 须无参(`-executeMethod` 只能调无参静态方法)");
                Assert.That(m.ReturnType, Is.EqualTo(typeof(void)), $"batch 入口 `{name}` 须返回 void");
            }
        }

        /// <summary>AC-5:文件命名 —— 基准关卡**无后缀**(ADR-022 §三 契约字面),其他关卡带 `_{level_id}`。</summary>
        [Test]
        public void test_ac5_fileNames_canonicalUnsuffixed_othersSuffixed()
        {
            Assert.That(WorldDataSets.FileNameFor(WorldDataSetKind.BuildSlots, "p0_clinic"),
                Is.EqualTo("world_buildslots.json"),
                "基准关卡的文件名 = ADR-022 §三 的契约字面(不得加后缀)");
            Assert.That(WorldDataSets.FileNameFor(WorldDataSetKind.BuildSlots, "scaffold_second"),
                Is.EqualTo("world_buildslots_scaffold_second.json"));
            Assert.That(WorldDataSets.FileNameFor(WorldDataSetKind.Ecozones, "scaffold_second"),
                Is.EqualTo("world_ecozones_scaffold_second.json"));
        }

        /// <summary>AC-5:`world_nav` **不得**按 level 命名 —— 它的后缀参数是 chunk(ADR-022 §五)。</summary>
        [Test]
        public void test_ac5_navChunkFileName_refusesLevelSuffix()
        {
            var ex = Assert.Throws<NotSupportedException>(
                () => WorldDataSets.FileNameFor(WorldDataSetKind.NavChunk, "p0_clinic"));

            Assert.That(ex.Message, Does.Contain("chunk"),
                "报文须说清后缀参数是 chunk 而非 level —— 否则会产出 world_nav_<level>.json 这种「看起来合理」的错名");
        }

        // ─────────────────────────── 工具 ───────────────────────────

        private static string Sha256Hex(byte[] bytes)
        {
            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(bytes);
                var sb = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }
    }
}
