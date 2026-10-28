// S20-17「World 场景装配轮」的判据载体(2026-10-28)。
//
// 权威来源:
//   `production/sprints/sprint-20.md` §十 · S20-17 卡(AC-1…AC-5)
//   ADR-023 §① 三场景制(世界实例的宿主由此轮裁定为 `BootRoot` 持有)
//   ADR-025 §①(`Gameplay.Boot` = 唯一可同时见 `Sim.Codec` 与 `Sim.World` 的装配)
//   `unity/Assets/Sim/World/WorldLattice.cs:295-298`(`WorldGeometry` 刻意无「按尺寸造空白世界」入口)
//
// ── 本文件补的是什么 ─────────────────────────────────────────────────────────
// `CompositionRoot.cs:787-796` 逐字记着:23 的 `StructureInstanceRegistry` 在生产树**零构造**
// (`new World(` 同样零构造),并指名「`BootRoot` 的 World 场景装配轮」。
// `impl-s20-2a-building01-2026-10-25.md:176` 的 N5 把这件记为「本件未做」,
// 而 `TD-building-01` 已于 2026-10-25 结案 ⇒ 缺口**零载体**。
// 既有 `world_end_to_end_test.cs` 判的是**占用合成**(Place → 不可走),
// 但它的 `World` / `StructureWriter` 都是**测试自建**;本文件判的是
// 「**生产装配路径**能不能把它们造出来、造出来的这两个是不是同一个世界」。
//
// ── 判据为什么这么写(禁借绿)────────────────────────────────────────────────
// · AC-1 / AC-2 的判据是「**该构造行存在且唯一**」⇒ 变异形态必须是**删除该行**
//   (改参数不触及判据)。扫描前**剥掉注释** —— 否则文件头里引用的 `new World(` 字样
//   会把计数虚高(本仓库已有多处这种自述,剥注释才数得准)。
// · AC-3 用 `CompositionRoot.Assemble(world:)` 这条**生产装配入口**,
//   而不是测试自己拼一个 `ClinicRoomMatchingService` —— 后者证明不了接线。
// · AC-4 的两步用**装配根产出的** `services.StructureWriter`,不另建写者;
//   模块 id 取**真烘焙产物**里的行(`build_modules.cooked` 的 2 = `surgery_table`,
//   足迹 = `[(0,0,0),(1,0,0)]`)⇒ 判据不依赖本测自造的目录。
// · AC-5 除断言异常类型外,另断「**抛之前下游零调用**」(注册表仍空、sink 仍空)——
//   只断异常类型会被下层同类型异常冒充。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;
using NUnit.Framework;
using UnityEngine;

namespace DaYiJingCheng.Tests.ModularBuilding
{
    /// <summary>
    /// S20-17:`World` / `StructureWriter` 的生产构造点 + 装配根接线判据。
    /// </summary>
    [TestFixture]
    public class world_assembly_wiring_test
    {
        // ══════════════ AC-1 / AC-2:生产树构造点计数 ══════════════

        /// <summary>AC-1:生产树 `new World(` 恰 1 处,且落在 `Gameplay.Boot/`。</summary>
        [Test]
        public void test_ac201_worldConstructedExactlyOnceInProductionTree()
        {
            List<string> hits = ScanProductionSources("new World(");

            Assert.AreEqual(1, hits.Count,
                "生产树 `new World(` 须**恰 1 处**(S20-17)。实测命中:\n  "
                + string.Join("\n  ", hits));
            StringAssert.Contains("Gameplay.Boot", hits[0],
                "构造点须落在 Gameplay.Boot(装配层)—— 门 A 的 Sim 侧不构造世界");
        }

        /// <summary>AC-2:生产树 `new StructureWriter(` 恰 1 处,且落在 `Gameplay.Boot/`。</summary>
        [Test]
        public void test_ac202_structureWriterConstructedExactlyOnceInProductionTree()
        {
            List<string> hits = ScanProductionSources("new StructureWriter(");

            Assert.AreEqual(1, hits.Count,
                "生产树 `new StructureWriter(` 须**恰 1 处**(S20-17)。实测命中:\n  "
                + string.Join("\n  ", hits));
            StringAssert.Contains("Gameplay.Boot", hits[0],
                "写者的构造点须在装配层 —— `Sim` 侧不该给自己造写者");
        }

        /// <summary>
        /// AC-1 的**接线半边**:`BootRoot.cs` 必须真的**调用**世界工厂。
        /// <para>⚠️ 单靠「`new World(` 恰 1 处」不够 —— 那一处可以在一个**没人调**的死工厂里。
        /// 本条把「装配根真的用了它」钉住;两条合起来才等价于卡里的「`BootRoot` 装配轮」。</para>
        /// </summary>
        [Test]
        public void test_ac201b_bootRootActuallyCallsTheWorldFactory()
        {
            string bootRoot = Path.Combine(Application.dataPath, "Gameplay.Boot", "BootRoot.cs");
            Assert.IsTrue(File.Exists(bootRoot), $"BootRoot.cs 应存在:{bootRoot}");

            string code = StripComments(File.ReadAllText(bootRoot));

            // ⚠️ 2026-10-28(S20-20 装载面):原判据是字面串 `MinimalWorldFactory.Create()` ——
            //    实参加上装载面之后它必然对不上。**改判据而非删断言**:现在同时钉两件事 ——
            //    ① 真的调用工厂;② 实参**不是**无参(无参 ⇒ 只走磁盘兜底 ⇒ 玩家构建槽位目录恒空)。
            Assert.IsTrue(code.Contains("MinimalWorldFactory.Create("),
                "BootRoot 须调用 `MinimalWorldFactory.Create(` —— 否则世界只在一个死工厂里被造");
            Assert.IsTrue(Regex.IsMatch(code, @"MinimalWorldFactory\.Create\(\s*[A-Za-z_]\w*\s*\?"),
                "world 工厂须收到**数据装载面**实参(形如 `preloaded ? DataCorePreloader.Provider : null`)——" +
                "无参调用 ⇒ 槽位骨架只走 `DataCookedDisk` 磁盘兜底 ⇒ 玩家构建里该目录不存在 ⇒" +
                "槽位目录恒空、`PlaceableChecker` 恒 `SlotNotFound`(S20-20 挂标 #1 原形态)");
            Assert.IsTrue(Regex.IsMatch(code, @"world:\s*world"),
                "BootRoot 须把世界以具名实参 `world: world` 交给 `CompositionRoot.Assemble`");
        }

        // ══════════════ AC-3:经生产装配路径 ⇒ ClinicRoomMatching != null ══════════════

        /// <summary>
        /// AC-3:传世界给装配根 ⇒ 24 的匹配服务**首次**非 null。
        /// <para>今日 `structures == null` ⇒ 恒 null(见下方 `..._withoutWorld_...` 的反向腿);
        /// 本腿证明接线真的发生。</para>
        /// </summary>
        [Test]
        public void test_ac203_assembleWithWorld_yieldsClinicRoomMatching()
        {
            World world = MinimalWorldFactory.Create();

            CompositionRootServices services = CompositionRoot.Assemble(world: world);

            Assert.IsNotNull(services.ClinicRoomMatching,
                "传了世界 ⇒ 24 的匹配服务须被构造(它的 `structures` 形参从 world.Structures 取)");
            Assert.AreSame(world, services.World, "服务袋须交回**同一个**世界实例(不另建)");
            // ⚠️ **这里刻意**不**断言 `services.StructureWriter`**(2026-10-28 第二轮证伪轮后订正):
            //   首版本条同时断言了写者非 null ⇒ 证伪轮 M2(删写者构造行)把本测一起弄红,
            //   驱动判「红集与预期不符 ⇒ 判别面不清」。根因是**一个夹具担了两个属性** ——
            //   AC-3 的语义只是「world ⇒ 匹配服务被构造」,写者存在性归 AC-2(源码扫描)
            //   与 AC-4(前置断言 `Assert.IsNotNull(writer)`),不该在此连坐。
            //   ⇒ 删掉那条断言,让 M2 的红集退回 {ac202, ac204}。
        }

        /// <summary>
        /// AC-3 反向腿:不传世界 ⇒ 两者仍 null(既有 100+ 处调用方的行为零变化)。
        /// <para><b>判别力</b>:若哪天 `Assemble` 改成「没给就自己造一个」,本测转红 ——
        /// 那正是「两份世界 / 两份真源」的入口。</para>
        /// </summary>
        [Test]
        public void test_ac203b_assembleWithoutWorld_yieldsNulls()
        {
            CompositionRootServices services = CompositionRoot.Assemble();

            Assert.IsNull(services.ClinicRoomMatching,
                "未注入世界 ⇒ 匹配服务须为 null(可观测的「未接线」,不是空世界)");
            Assert.IsNull(services.StructureWriter,
                "未注入世界 ⇒ 无结构写者(写者的 occupancy 必须就是那个世界)");
            Assert.IsNull(services.World, "未注入世界 ⇒ 服务袋里的 World 须为 null");
        }

        /// <summary>
        /// 两份注册表 = 两份真源 ⇒ 装配根 fail-closed。
        /// <para>这一条不属于 AC 面,是 S20-17 落地时**新增**的守卫;留在此处是为了让
        /// 「同给且不同实例」这条分支有夹具(否则它会是一条零覆盖的 `throw`)。</para>
        /// </summary>
        [Test]
        public void test_ac203c_worldAndForeignRegistry_throws()
        {
            World world = MinimalWorldFactory.Create();
            var foreign = new StructureInstanceRegistry();

            var ex = Assert.Throws<InvalidOperationException>(
                () => CompositionRoot.Assemble(structures: foreign, world: world));

            StringAssert.Contains("两份", ex.Message,
                "异常须点名「两份真源」—— 否则调用方看不出这是注册表分叉,会以为是别的装配问题");
        }

        // ══════════════ AC-4:世界端到端,入口 = 装配根产出的服务 ══════════════

        /// <summary>
        /// AC-4:`Place → IsEffectivelyWalkable == false` / `Remove → true`,
        /// **经装配根产出的 `services.StructureWriter`**(不是测试自建写者)。
        /// <para>模块 id 取真烘焙产物里的 2(`surgery_table`,足迹 `[(0,0,0),(1,0,0)]`)——
        /// 两格足迹使「非锚点格也被占用」可观测,而不是只测了锚点。</para>
        /// </summary>
        [Test]
        public void test_ac204_placeThenRemove_throughAssembledServices()
        {
            // ── Arrange ──────────────────────────────────────────────
            World world = MinimalWorldFactory.Create();
            CompositionRootServices services = CompositionRoot.Assemble(world: world);
            StructureWriter writer = services.StructureWriter;
            Assert.IsNotNull(writer, "前置:装配根须产出结构写者(AC-2 / AC-3)");

            const int moduleSurgeryTable = 2;          // 真产物行:足迹 [(0,0,0),(1,0,0)]
            var anchor = new WorldPos(2, 0, 2);
            var second = new WorldPos(3, 0, 2);        // 非锚点足迹格

            // 前置:未放置 ⇒ 两格可走(否则下面的 false 可能是「本来就 false」)
            Assert.IsTrue(world.IsEffectivelyWalkable(anchor), "前置:未放置 ⇒ 锚点须可走");
            Assert.IsTrue(world.IsEffectivelyWalkable(second), "前置:未放置 ⇒ 非锚点足迹格须可走");
            Assert.AreEqual(0, world.OccupiedCount, "前置:未放置 ⇒ 占用表须空");

            // ── Act 1:Place ─────────────────────────────────────────
            long structureId = writer.Place(anchor, moduleSurgeryTable, 0, 0, tick: 0);

            // Assert 1
            Assert.IsFalse(world.IsEffectivelyWalkable(anchor),
                "放置后锚点须不可走(AC-4 正腿)");
            Assert.IsFalse(world.IsEffectivelyWalkable(second),
                "放置后**非锚点足迹格**也须不可走(C1 同型:只测锚点会漏掉足迹退化)");
            // ⚠️ `OccupiedCount` 数的是**占用格**不是实例(`World.cs:127` 遍历 overlay 计数):
            //    `surgery_table` 足迹两格 ⇒ 2。首轮曾把这里写成 1(按「一条实例」理解),
            //    实跑红「Expected 1 But was 2」—— 是**夹具写错**,不是生产缺陷,已就地订正。
            Assert.AreEqual(2, world.OccupiedCount, "放置后占用格须恰 2(两格足迹)");

            // ── Act 2:Remove ────────────────────────────────────────
            bool removed = writer.Remove(structureId, tick: 1, demolishRefundRatio: 65536);

            // Assert 2
            Assert.IsTrue(removed, "拆除须真的找到并移除该实例");
            Assert.IsTrue(world.IsEffectivelyWalkable(anchor),
                "拆除后锚点须恢复可走(AC-4 反腿)");
            Assert.IsTrue(world.IsEffectivelyWalkable(second),
                "拆除后非锚点足迹格也须恢复可走");
            Assert.AreEqual(0, world.OccupiedCount, "拆除后占用表须回到空");
        }

        // ══════════════ AC-5:写者构造 fail-closed ══════════════

        /// <summary>
        /// AC-5:未注入 `DropEventWriter` ⇒ **构造期**抛,且抛之前**下游零调用**。
        /// <para>后半句是关键 —— 只断 `ArgumentNullException` 会被下层同类型异常冒充
        /// (若构造器先做了别的事再抛,测试照绿)。故同时断注册表仍空、sink 仍空。</para>
        /// </summary>
        [Test]
        public void test_ac205_structureWriterWithoutDropWriter_throwsBeforeAnyDownstreamCall()
        {
            // Arrange:真世界 + 真目录,唯独 dropWriter = null
            World world = MinimalWorldFactory.Create();
            var registry = new StructureInstanceRegistry();
            var events = new List<SimEvent>();
            var sink = new CapturingSink(events);
            IPayloadEncoder encoder = new PayloadEncoder(new InMemoryBlobPool());

            // Act
            var ex = Assert.Throws<ArgumentNullException>(() =>
                new StructureWriter(sink, registry, encoder,
                                    moduleCatalog: null, occupancy: world, dropWriter: null));

            // Assert:异常本身
            Assert.AreEqual("dropWriter", ex.ParamName, "须点名缺的是 dropWriter");
            StringAssert.Contains("DropSpawned", ex.Message,
                "异常须说明后果(拆除返还走 DropSpawned,静默不返还 = 吞玩家材料)");

            // Assert:抛之前下游零调用(副作用判据)
            Assert.AreEqual(0, registry.Count, "构造就抛 ⇒ 不许已经注册过任何结构");
            Assert.AreEqual(0, events.Count, "构造就抛 ⇒ 不许已经往流里写过任何事件");
        }

        // ══════════════ 工具 ══════════════

        /// <summary>
        /// 扫 `Assets/` 下**生产树**(排除 `Tests/`)的 `*.cs`,剥注释后统计某字面量的命中。
        /// <para>判据与卡里那条可复算命令等价:
        /// <c>grep -rn "new World(" unity/Assets --include=*.cs | grep -v "/Tests/"</c>
        /// —— 差别只有「剥注释」(卡里那条会把注释里的自述也算进去)。</para>
        /// </summary>
        /// <returns>命中清单,形如 `相对路径:行号`。</returns>
        private static List<string> ScanProductionSources(string literal)
        {
            var hits = new List<string>();
            string assetsRoot = Application.dataPath.Replace('\\', '/');

            foreach (string file in Directory.GetFiles(assetsRoot, "*.cs", SearchOption.AllDirectories))
            {
                string normalized = file.Replace('\\', '/');
                if (normalized.Contains("/Tests/")) continue;

                string[] lines = File.ReadAllLines(file);
                bool inBlockComment = false;
                for (int i = 0; i < lines.Length; i++)
                {
                    string code = StripCommentsFromLine(lines[i], ref inBlockComment);
                    if (code.Contains(literal))
                        hits.Add($"{normalized.Substring(assetsRoot.Length + 1)}:{i + 1}");
                }
            }

            return hits;
        }

        /// <summary>剥掉整份源码里的注释(块注释 + 行注释)。</summary>
        private static string StripComments(string text)
        {
            var sb = new StringBuilder();
            bool inBlockComment = false;
            foreach (string line in text.Split('\n'))
                sb.AppendLine(StripCommentsFromLine(line, ref inBlockComment));
            return sb.ToString();
        }

        /// <summary>
        /// 剥掉一行里的注释;块注释状态跨行携带。
        /// <para>不处理「字符串字面量里含 `//` 或 `/*`」的形态 —— 本仓无此写法,且被测的
        /// 两条字面量(`new World(` / `new StructureWriter(`)不可能出现在字符串里。</para>
        /// </summary>
        private static string StripCommentsFromLine(string line, ref bool inBlockComment)
        {
            var sb = new StringBuilder();
            int i = 0;
            while (i < line.Length)
            {
                if (inBlockComment)
                {
                    int close = line.IndexOf("*/", i, StringComparison.Ordinal);
                    if (close < 0) return sb.ToString();
                    inBlockComment = false;
                    i = close + 2;
                    continue;
                }

                int lineComment = line.IndexOf("//", i, StringComparison.Ordinal);
                int blockOpen = line.IndexOf("/*", i, StringComparison.Ordinal);

                if (blockOpen < 0 && lineComment < 0) { sb.Append(line, i, line.Length - i); break; }
                if (blockOpen < 0 || (lineComment >= 0 && lineComment < blockOpen))
                {
                    sb.Append(line, i, lineComment - i);
                    break;
                }

                sb.Append(line, i, blockOpen - i);
                inBlockComment = true;
                i = blockOpen + 2;
            }

            return sb.ToString();
        }

        /// <summary>只收事件不落流的 sink(`StructureWriter` 要求 `IEventSink`;本测不判事件内容)。</summary>
        private sealed class CapturingSink : IEventSink
        {
            private readonly List<SimEvent> _sink;
            public CapturingSink(List<SimEvent> sink) { _sink = sink; }
            public void Append(in SimEvent e) { _sink.Add(e); }
        }
    }
}
