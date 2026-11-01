// 24 GDD 规则六(复活床格选定)—— 纯判定 + 生产查询面(2026-11-01)
//
// ─────────────────────────────────────────────────────────────────────────────
// 读哪些仓库文件 / 写哪里(test-standards 具名声明义务)
// ─────────────────────────────────────────────────────────────────────────────
// 读(只读,零写盘):
//   ① `unity/Assets/Gameplay.Presentation/ClinicReviveCellQuery.cs` 的**源码**
//      (剥注释扫描)—— 为什么必须读真源:「本类零判定逻辑」是跨文件结构约定,
//      类型面表达不了(承 S23-1/2/5 同法)。
//   ② 本测试**不读** `assets/data/*.json`:全部夹具数据在测试内自造 ——
//      规则六的判据(候选集 / 字典序 min / 无候选)是**纯整数与字符串**,
//      与真表的具体行值无关;读真表反而会让「表改了夹具仍绿」成为掩盖面
//      (F1 自造断言顶替的**反面**:这里断言的就是被测纯函数本身)。
// 写:**零写盘**。
//
// ─────────────────────────────────────────────────────────────────────────────
// ⚠️ 为什么既有 `death_respawn_command_test` 的 `FixedBed` 桩**不能**算本件的证据
// ─────────────────────────────────────────────────────────────────────────────
//   那个桩直接返回一个固定格 ⇒ 它测的是 `RespawnCommandIssuer` 的**两分支选择**
//   (有床 → 床格 / 无床 → 出生点),**不测规则六本身**(候选集怎么算、多格取哪个)。
//   本件测的是规则六的**判定体**。两者是不同层 —— 本件的存在正是因为
//   「有 IClinicReviveCell 契约 + 有消费者」但**判据本体零测试**(2026-11-01 实测)。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DaYiJingCheng.Gameplay.Presentation;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Clinic;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;
using NUnit.Framework;
using UnityEngine;

namespace DaYiJingCheng.Tests.DeathRespawn
{
    /// <summary>规则六:床格选定的确定性、字典序、无候选出口、以及生产查询面的接线。</summary>
    [TestFixture]
    public sealed class ClinicReviveCellTest
    {
        private static readonly WorldPos SpawnCell = new WorldPos(0, 0, 0);

        // ══════════════════ 规则六 ②:字典序 min ══════════════════

        /// <summary>
        /// 排序键 = `(y, x, z)`:先层、再 x、再 z(GDD 规则六 ② 逐字)。
        /// </summary>
        /// <remarks>
        /// ⚠️ 三条断言**各自独立**,不能只断「A &lt; B」:键序的三段必须**逐段**验证,
        /// 否则把 `y` 打头换成 `x` 打头(或把 `x` / `z` 交换)时,某一条仍可能偶然成立。
        /// 下面三个用例**只在一个键上不同**,其余两个键**完全相同** ⇒ 每段独立可判。
        /// </remarks>
        [Test]
        public void test_ruleSix_keyOrder_isYThenXThenZ()
        {
            // ① 只 y 不同 ⇒ 低层优先(y 小者在前)。
            Assert.AreEqual(-1, ClinicReviveCellRule.CompareCellOrder(
                new WorldPos(9, 0, 9), new WorldPos(0, 1, 0)),
                "y=0 应先于 y=1 —— 即使 x/z 都更大");

            // ② y 相同、只 x 不同 ⇒ x 小者在前。
            Assert.AreEqual(-1, ClinicReviveCellRule.CompareCellOrder(
                new WorldPos(0, 1, 9), new WorldPos(1, 1, 0)),
                "y 相同 ⇒ 比 x(x=0 先于 x=1)");

            // ③ y / x 都相同、只 z 不同 ⇒ z 小者在前。
            Assert.AreEqual(-1, ClinicReviveCellRule.CompareCellOrder(
                new WorldPos(0, 1, 0), new WorldPos(0, 1, 1)),
                "y/x 相同 ⇒ 比 z(z=0 先于 z=1)");

            // ④ 同格 ⇒ 0。
            Assert.AreEqual(0, ClinicReviveCellRule.CompareCellOrder(
                new WorldPos(2, 3, 4), new WorldPos(2, 3, 4)), "同格 ⇒ 0");
        }

        /// <summary>
        /// **确定性**:同一格集合、**任意输入序**,`Select` 结果逐位相同(ADR-005 纯函数纪律)。
        /// </summary>
        /// <remarks>
        /// ⚠️ 这条是规则六 ② 的**核心反例面**(「禁取第一个床格」)—— 若实现依赖遍历序,
        /// 换一个输入序就会换答案。本测**穷举全部排列**(3! = 6 种),不是抽样。
        /// </remarks>
        [Test]
        public void test_ruleSix_selectionIndependentOfInputOrder()
        {
            var beds = new[]
            {
                new WorldPos(5, 1, 2),
                new WorldPos(1, 0, 3),   // (y,x,z) 字典序最小
                new WorldPos(2, 2, 0),
            };
            var expected = new WorldPos(1, 0, 3);

            int permutations = 0;
            foreach (var perm in Permutations(beds))
            {
                var cells = new List<WorldPos>(perm);
                WorldPos? got = ClinicReviveCellRule.Select(cells, _ => "bed");
                Assert.AreEqual(expected, got,
                    $"输入序 [{string.Join(",", cells.Select(c => $"({c.X},{c.Y},{c.Z})"))}] " +
                    $"应仍选出 ({expected.X},{expected.Y},{expected.Z})");
                permutations++;
            }
            Assert.AreEqual(6, permutations, "3 个元素的全部排列数应为 6");
        }

        /// <summary>
        /// 候选集判据 = 家具类型 `bed`;非床类格**不进**候选集。
        /// </summary>
        [Test]
        public void test_ruleSix_nonBedFurnitureExcluded()
        {
            var cells = new List<WorldPos>
            {
                new WorldPos(0, 0, 0),   // 字典序最小,但**不是**床
                new WorldPos(3, 0, 0),   // 床
            };
            WorldPos? got = ClinicReviveCellRule.Select(cells,
                c => c.Equals(new WorldPos(0, 0, 0)) ? "chamber_pot" : "bed");

            Assert.AreEqual(new WorldPos(3, 0, 0), got,
                "非床类的最小格不得被选中(候选集判据 = furn_type == bed)");
        }

        /// <summary>
        /// 规则六 ③:无候选 ⇒ 返回 <c>null</c>(**不填兜底出生点** —— 兜底归 29)。
        /// </summary>
        /// <remarks>
        /// ⚠️ 这条同时钉「**禁零值哨兵**」:返回 `null` 而非 `WorldPos(0,0,0)`。
        /// 若实现用零值当哨兵,下面第二个用例(合法床格恰在原点)会**分不出**「无床」
        /// 与「床在原点」—— 本测用两个并列用例把这对歧义钉死。
        /// </remarks>
        [Test]
        public void test_ruleSix_noCandidate_returnsNull_notZeroSentinel()
        {
            // ① 房间格存在但无床 ⇒ null。
            var potsOnly = new List<WorldPos> { new WorldPos(1, 0, 1) };
            Assert.IsNull(ClinicReviveCellRule.Select(potsOnly, _ => "chamber_pot"),
                "无床类家具格 ⇒ null(不填兜底格 —— 兜底是 29 的裁决面)");

            // ② 空房间格集 ⇒ null。
            Assert.IsNull(ClinicReviveCellRule.Select(new List<WorldPos>(), _ => "bed"),
                "空格集 ⇒ null");

            // ③ **反歧义**:合法床格恰在原点 (0,0,0) ⇒ 返回非 null 且值 = 原点。
            //    若实现拿零值当哨兵,本用例与 ① 将不可区分。
            var bedAtOrigin = new List<WorldPos> { new WorldPos(0, 0, 0) };
            WorldPos? got = ClinicReviveCellRule.Select(bedAtOrigin, _ => "bed");
            Assert.IsNotNull(got, "床在原点 (0,0,0) 是**合法**候选,不得被当作「无床」");
            Assert.AreEqual(new WorldPos(0, 0, 0), got, "床在原点 ⇒ 选它");
        }

        /// <summary>构造器守卫:null 入参抛(接线错误 fail-loud,不静默返回「无床」)。</summary>
        [Test]
        public void test_ruleSix_nullInputs_throw()
        {
            Assert.Throws<ArgumentNullException>(
                () => ClinicReviveCellRule.Select(null, _ => "bed"), "clinicCells 为 null ⇒ 抛");
            Assert.Throws<ArgumentNullException>(
                () => ClinicReviveCellRule.Select(new List<WorldPos>(), null), "furnTypeAt 为 null ⇒ 抛");
        }

        // ══════════════════ 生产查询面(真注册表 + 真数据集)══════════════════

        /// <summary>
        /// 生产链端到端:**有医馆房间且有床** ⇒ `TryGet` 返回真格;格 = 规则六选定的那一个。
        /// </summary>
        /// <remarks>
        /// ⚠️ 本测用**真** `StructureInstanceRegistry` + **真** `BuildModulesCatalog` +
        /// **真** `ClinicFurnitureCatalogue`,不打桩 —— 打桩会让「链本身接线错」逃逸
        /// (F2「测试跑 Fake,生产类零命中」的反面)。
        /// ⚠️ 房间要成房间:析出要求**恰好 1 个 4-连通块**(GDD 规则一 / EC-24-07)。
        /// 故夹具把床与另一件家具放在**相邻格**(同层 4 邻接),否则 `HasRoom == false`
        /// ⇒ 恒「无床」,测试会以「接线错」的形式假红。
        /// </remarks>
        [Test]
        public void test_query_bedInRoom_returnsDeterministicCell()
        {
            // 布局:床在 (1,0,1),药罐在 (2,0,1)。
            // ⚠️ **必须沿 x 或 y 相邻,不能沿 z** —— 连通判据 = `ABS(dx)+ABS(dy)==1`
            //    (GDD 规则一;z 不参与)⇒ 沿 z 差 1 的两件会成**两个独立簇** ⇒
            //    `ConnectedClusterCount >= 2` ⇒ 整盘 ROOM_NONE(EC-24-07)⇒ 恒「无床」。
            //    首版夹具正是踩了这个坑(测试红在「房间没成」,不是规则六错)。
            var registry = new StructureInstanceRegistry();
            registry.Register(new WorldPos(1, 0, 1), moduleId: 1, orientation: 0, variant: 0,
                occupiedCells: new[] { new WorldPos(1, 0, 1) });
            registry.Register(new WorldPos(2, 0, 1), moduleId: 2, orientation: 0, variant: 0,
                occupiedCells: new[] { new WorldPos(2, 0, 1) });

            var modules = new BuildModulesCatalog(ModulesWithBedAndPot());
            var furniture = new ClinicFurnitureCatalogue(FurnitureWithBedAndPot(), tierMax: 3);

            var query = new ClinicReviveCellQuery(registry, modules, furniture);

            bool ok = query.TryGet(actorId: 0, out WorldPos cell);
            Assert.IsTrue(ok, "房间内有床 ⇒ TryGet 应返回 true");
            Assert.AreEqual(new WorldPos(1, 0, 1), cell, "应返回床格");
        }

        /// <summary>
        /// 生产链:**无床**(房间只有非床类家具)⇒ `TryGet` 返回 false,且 **cell = default**。
        /// </summary>
        /// <remarks>
        /// ⚠️ 这条钉「24 不填兜底」(GDD 规则六 ③)。若实现回退到出生点,本测转红。
        /// </remarks>
        [Test]
        public void test_query_noBed_returnsFalse_noFallbackCell()
        {
            // ⚠️ 同 `test_query_bedInRoom`:沿 **x** 相邻(连通判据 = `ABS(dx)+ABS(dy)==1`,
            //    z 不参与)—— 否则两个独立簇 ⇒ ROOM_NONE ⇒ 本测会红在「没成房间」,
            //    而不是它真正要钉的「有房间但无床 ⇒ 不填兜底」。
            var registry = new StructureInstanceRegistry();
            registry.Register(new WorldPos(0, 0, 0), moduleId: 2, orientation: 0, variant: 0,
                occupiedCells: new[] { new WorldPos(0, 0, 0) });
            registry.Register(new WorldPos(1, 0, 0), moduleId: 2, orientation: 0, variant: 0,
                occupiedCells: new[] { new WorldPos(1, 0, 0) });

            var modules = new BuildModulesCatalog(ModulesWithBedAndPot());
            var furniture = new ClinicFurnitureCatalogue(FurnitureWithBedAndPot(), tierMax: 3);
            var query = new ClinicReviveCellQuery(registry, modules, furniture);

            bool ok = query.TryGet(actorId: 0, out WorldPos cell);
            Assert.IsFalse(ok, "房间内无床 ⇒ false");
            Assert.AreEqual(default(WorldPos), cell,
                "无床时 cell 须为 default —— **不得**填兜底出生点(那是 29 的裁决面)");
        }

        /// <summary>生产链:空布局(无任何结构)⇒ false(不抛 —— 空布局是合法输入,EC-24-01)。</summary>
        [Test]
        public void test_query_emptyLayout_returnsFalse_notThrow()
        {
            var registry = new StructureInstanceRegistry();
            var modules = new BuildModulesCatalog(ModulesWithBedAndPot());
            var furniture = new ClinicFurnitureCatalogue(FurnitureWithBedAndPot(), tierMax: 3);
            var query = new ClinicReviveCellQuery(registry, modules, furniture);

            Assert.IsFalse(query.TryGet(actorId: 0, out _),
                "空布局是合法输入(EC-24-01)⇒ 返回 false,不抛");
        }

        /// <summary>actorId 为负 ⇒ 抛(fail-loud;玩家 id 走机制 A 计数器,不得为负)。</summary>
        [Test]
        public void test_query_negativeActorId_throws()
        {
            var registry = new StructureInstanceRegistry();
            var modules = new BuildModulesCatalog(ModulesWithBedAndPot());
            var furniture = new ClinicFurnitureCatalogue(FurnitureWithBedAndPot(), tierMax: 3);
            var query = new ClinicReviveCellQuery(registry, modules, furniture);

            Assert.Throws<ArgumentOutOfRangeException>(() => query.TryGet(-1, out _),
                "负 actorId ⇒ 抛");
        }

        /// <summary>构造器守卫:任一依赖 null ⇒ 抛(缺件时链只能恒「无床」,静默即失效)。</summary>
        [Test]
        public void test_query_nullDependency_throws()
        {
            var modules = new BuildModulesCatalog(ModulesWithBedAndPot());
            var furniture = new ClinicFurnitureCatalogue(FurnitureWithBedAndPot(), tierMax: 3);
            Assert.Throws<ArgumentNullException>(
                () => new ClinicReviveCellQuery(null, modules, furniture), "registry 为 null ⇒ 抛");
            Assert.Throws<ArgumentNullException>(
                () => new ClinicReviveCellQuery(new StructureInstanceRegistry(), null, furniture),
                "modules 为 null ⇒ 抛");
            Assert.Throws<ArgumentNullException>(
                () => new ClinicReviveCellQuery(new StructureInstanceRegistry(), modules, null),
                "furniture 为 null ⇒ 抛");
        }

        // ══════════════════ 后置注入(装配序约束)══════════════════

        /// <summary>
        /// `AttachClinicCell` 后置注入:注入后落点取床格;二次注入抛;null 抛。
        /// </summary>
        [Test]
        public void test_attachClinicCell_injectsOnce_secondThrows()
        {
            var sink = new RecordingTeleportStub();
            var issuer = new RespawnCommandIssuer(sink, null, SpawnCell);
            Assert.IsFalse(issuer.HasClinicCell, "未注入 ⇒ HasClinicCell = false");

            issuer.AttachClinicCell(new FixedBed(new WorldPos(7, 0, 7)));
            Assert.IsTrue(issuer.HasClinicCell, "注入后 ⇒ HasClinicCell = true");

            issuer.Issue(0);
            Assert.AreEqual(new WorldPos(7, 0, 7), sink.Commands.Single().Cell, "落点取床格");

            Assert.Throws<InvalidOperationException>(
                () => issuer.AttachClinicCell(new FixedBed(new WorldPos(8, 0, 8))),
                "二次注入 ⇒ 抛(两个床格真源)");
            Assert.Throws<ArgumentNullException>(
                () => new RespawnCommandIssuer(sink, null, SpawnCell).AttachClinicCell(null),
                "null ⇒ 抛");
        }

        // ══════════════════ 零判定逻辑(结构判据)══════════════════

        /// <summary>
        /// `ClinicReviveCellQuery` **零判定逻辑**:源码内不得出现字典序比较 / 排序调用。
        /// </summary>
        /// <remarks>
        /// ⚠️ 为什么需要这条:判据住 `Sim/Clinic/ClinicReviveCellRule`(纯函数,门 A 内)。
        /// 若查询面里**再写一份**比较(例如为了「省一次调用」内联 min),就是
        /// **第二真源**(F8「行为等价掩盖结构分叉」的形态:两份实现恰好行为一致 ⇒ 全绿,
        /// 改坏其中一份时另一份仍对 ⇒ 夹具分不出)。
        /// 判据 = 源码里不得出现 `CompareCellOrder` 的**内联替代物**:`< .*\.Y` 这类
        /// 直接比字段的形态,以及任何 `OrderBy` / `Sort` 调用。
        /// </remarks>
        [Test]
        public void test_query_hasNoInlineOrderingLogic()
        {
            string src = StripComments(ReadRepoFile(
                "unity/Assets/Gameplay.Presentation/ClinicReviveCellQuery.cs"));

            // 不得自己比坐标字段(应调用 ClinicReviveCellRule)。
            var inlineCompare = new Regex(@"\.\s*Y\s*[<>]|\.\s*X\s*[<>]|\.\s*Z\s*[<>]");
            Assert.IsFalse(inlineCompare.IsMatch(src),
                "ClinicReviveCellQuery 内出现直接比较坐标字段 —— 判据须住在 " +
                "ClinicReviveCellRule(单一真源);内联 = 第二真源");

            // 不得自己排序(排序 = 规则六 ② 的判定体)。
            var sortCalls = new Regex(@"\b(OrderBy|OrderByDescending|\.Sort\()");
            Assert.IsFalse(sortCalls.IsMatch(src),
                "ClinicReviveCellQuery 内出现排序调用 —— 排序键归 ClinicReviveCellRule");

            // 正控:它**确实**调用了规则六(否则上面两条对空文件也成立 ⇒ 空洞正控 F4)。
            Assert.IsTrue(src.Contains("ClinicReviveCellRule.Select"),
                "本类须经 ClinicReviveCellRule.Select 做判定(正控:证明它真的接线了)");
        }

        // ══════════════════ 夹具 ══════════════════

        private sealed class RecordingTeleportStub : ITeleportCommandSink
        {
            public readonly List<(int ActorId, WorldPos Cell)> Commands = new();
            public void RequestTeleport(int actorId, WorldPos cell) => Commands.Add((actorId, cell));
        }

        private sealed class FixedBed : IClinicReviveCell
        {
            private readonly WorldPos _cell;
            public FixedBed(WorldPos cell) => _cell = cell;
            public bool TryGet(int actorId, out WorldPos cell) { cell = _cell; return true; }
        }

        /// <summary>模块目录:1 = 床(`furn_type: "bed"`),2 = 药罐(非床)。</summary>
        private static BuildModulesDataSet ModulesWithBedAndPot()
        {
            var rows = new[]
            {
                new BuildModuleRow(1, 0, "Bed", "bed", new[] { new WorldPos(0, 0, 0) },
                                   cost: 1, refundable: 1, buildItemKey: 0),
                new BuildModuleRow(2, 0, "Pot", "chamber_pot", new[] { new WorldPos(0, 0, 0) },
                                   cost: 1, refundable: 1, buildItemKey: 0),
            };
            return new BuildModulesDataSet(rows, schemaVersion: 2, configVersion: 1);
        }

        /// <summary>家具目录:含 `bed` 与 `chamber_pot` 两行(`FURN_TYPES` 闭集的真判据输入)。</summary>
        private static ClinicFurnitureEntry[] FurnitureWithBedAndPot() => new[]
        {
            new ClinicFurnitureEntry("bed", tier: 1, eEnv: default),
            new ClinicFurnitureEntry("chamber_pot", tier: 0, eEnv: default),
        };

        private static IEnumerable<WorldPos[]> Permutations(WorldPos[] items)
        {
            if (items.Length <= 1) { yield return items; yield break; }
            for (int i = 0; i < items.Length; i++)
            {
                var rest = new WorldPos[items.Length - 1];
                int k = 0;
                for (int j = 0; j < items.Length; j++) if (j != i) rest[k++] = items[j];
                foreach (var tail in Permutations(rest))
                {
                    var full = new WorldPos[items.Length];
                    full[0] = items[i];
                    Array.Copy(tail, 0, full, 1, tail.Length);
                    yield return full;
                }
            }
        }

        private static string ReadRepoFile(string relative)
        {
            string path = Path.Combine(Application.dataPath, "..", "..",
                relative.Replace('/', Path.DirectorySeparatorChar));
            Assert.IsTrue(File.Exists(path), $"仓库文件不存在:{path}");
            return File.ReadAllText(path);
        }

        private static string StripComments(string src)
        {
            string noBlock = Regex.Replace(src, @"/\*.*?\*/", " ", RegexOptions.Singleline);
            return Regex.Replace(noBlock, @"//[^\n]*", " ");
        }
    }
}
