// S20-15 · foraging story-006 —— 采集侧新颖度接线 + 编排器生产装配
//
// 权威来源:
//   design/gdd/skill-system.md 规则二(新颖度加权)· §3.2 对象粒度表「采集」行
//     · `:500` 按玩家独立 · `:502` 必须亲手采过 · `:577-579` 三条验收标准
//   design/gdd/foraging.md 规则二(三事件)· 规则五(成长:对象 = 品种)
//   ADR-026 §⑥(`NoveltyTracker` = 运行期加速缓存,真源在病史流;**不独立快照**)
//   ADR-005(主机唯一 Append)· ADR-009 §四(派生态 —— lastSeenTick 来自 tick 序列,非墙钟)
//   story production/epics/foraging/story-006-novelty-wiring-and-orchestrator-assembly.md
//
// ⚠️ **本文件的记分面(禁把「接线」读成「玩家采得到」)**:
//     ✅ AC-17-14 / 14b / 14c / 14d / 14e / 14f / 14g  —— 本文件主体
//     ⚠️ 生产触发点(「对着一株药材按 E」)仍归 4 的交互装配轮 ——
//        本文件证的是「编排器**被调用时**行为正确」+「它在生产装配里**存在**」,
//        不证「玩家按得到」。
//
// ⚠️ 零随机 / 零时间依赖 / 零文件 I/O;全部 tick 写死。
//    ⚠️ 唯一读盘的一处 = `test_ac1714g_*`(扫 `CompositionRoot.cs` 源码数构造点)——
//       它是「单装配点」这条 AC 的**唯一可用判据形态**(构造函数调用点留不进类型面),
//       读的是仓库内源文件、**不写盘**(FsWriteBoundaryGate 只管写面)。

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.Contracts.SkillSystem;
using DaYiJingCheng.Sim.Foraging;
using DaYiJingCheng.Sim.World;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.EditMode.Foraging
{
    /// <summary>
    /// 新颖度求值「移入编排器内部」后的行为面(AC-17-14 系列)+ 装配面(14f / 14g)。
    /// </summary>
    [TestFixture]
    public sealed class HarvestNoveltyWiringTest
    {
        private CompositionRootServices _bag;
        private ResourceHarvestWriter _harvestWriter;
        private SpyGrowthEmitter _growth;
        private RecordingDropWriter _dropWriter;
        private NoveltyTracker _novelty;
        private HarvestOrchestrator _orchestrator;

        /// <summary>GDD / ADR-026 的默认冷却期(24000 tick = 20 min @ 20 Hz)。</summary>
        private const int Cooldown = 24000;

        [SetUp]
        public void Setup()
        {
            _bag = CompositionRoot.Assemble();
            _harvestWriter = new ResourceHarvestWriter(
                _bag.EventSink, _bag.Encoder, _bag.Decoder, _bag.Stream.Events);
            _growth = new SpyGrowthEmitter();
            _dropWriter = new RecordingDropWriter();
            _novelty = new NoveltyTracker();
            _orchestrator = new HarvestOrchestrator(
                _bag.IdAuthority, _harvestWriter, _dropWriter, _growth, _novelty);
        }

        // ══════════════════════════════════════════════════════════════
        // AC-17-14 —— 首采 = First(×3.0)
        // ══════════════════════════════════════════════════════════════

        [Test]
        public void test_ac1714_firstHarvestOfASpecies_yieldsFirst()
        {
            _orchestrator.Resolve(Req(tick: 100, actorId: 1, objectId: 3));

            Assert.That(_growth.Calls.Single().Novelty, Is.EqualTo(NoveltyClass.First),
                "首次采某品种 ⇒ novelty_class = First(×3.0)");
            Assert.That(_novelty.Count, Is.EqualTo(1),
                "首采后追踪器须恰 1 条记录(冻结了「求值真的发生过」—— 若编排器没调 Query," +
                "这里会是 0,而载荷仍可能是调用方填的 First)");
        }

        // ══════════════════════════════════════════════════════════════
        // AC-17-14b —— 冷却内 = Stale(×0.2),且 lastSeenTick 不推进
        // ══════════════════════════════════════════════════════════════

        [Test]
        public void test_ac1714b_withinCooldown_yieldsStale_andDoesNotAdvanceLastSeen()
        {
            _orchestrator.Resolve(Req(tick: 100, actorId: 1, objectId: 3));
            _orchestrator.Resolve(Req(tick: 100 + 1000, actorId: 1, objectId: 3));

            Assert.That(_growth.Calls[1].Novelty, Is.EqualTo(NoveltyClass.Stale),
                "冷却期内再采同品种 ⇒ Stale(×0.2)");

            // ⚠️ **本断言是本 AC 的核心,不是补充**。`lastSeenTick` 若被推进到 1100:
            //   · 表面看「第二条也是 Stale」⇒ 只断类别的话,两种实现全绿;
            //   · 但下一次冷却判定会从 1100 起算 ⇒ 首次奖励被**反复延长**,
            //     而 GDD 的语义是「冷却锁在**第一次**见它的时刻」。
            // 判据形态 = 在「距首采已过冷却、但距第二次采未过冷却」的 tick 上问一次:
            //   从 100 起算 ⇒ 100+24000=24100 已过冷却 ⇒ Normal
            //   从 1100 起算 ⇒ 24100-1100=23000 < 24000 ⇒ Stale
            Assert.That(_novelty.Peek(1, (int)SkillId.采集, 3, 100 + Cooldown),
                Is.EqualTo(NoveltyClass.Normal),
                "冷却锁定在**首采** tick(100):到 100+" + Cooldown + " 已过冷却 ⇒ Normal。" +
                "若这里得到 Stale,说明第二次采集把 lastSeenTick 推到了 1100 —— " +
                "那是「冷却内也推进」的实现,会让首次奖励被无限延长");
        }

        // ══════════════════════════════════════════════════════════════
        // AC-17-14c —— 冷却外 = Normal(×1.0),且 lastSeenTick 推进
        // ══════════════════════════════════════════════════════════════

        [Test]
        public void test_ac1714c_afterCooldown_yieldsNormal_andAdvancesLastSeen()
        {
            _orchestrator.Resolve(Req(tick: 100, actorId: 1, objectId: 3));
            _orchestrator.Resolve(Req(tick: 100 + Cooldown, actorId: 1, objectId: 3));

            Assert.That(_growth.Calls[1].Novelty, Is.EqualTo(NoveltyClass.Normal),
                "超出冷却期后再采同品种 ⇒ Normal(×1.0)");

            // 推进判据:在「距第二次已过冷却、但距首采未过」的 tick 问 —— 此时不该是 Normal
            //   已推进到 24100 ⇒ 24100+24000-1 = 48099 仍未过冷却 ⇒ Stale
            //   未推进(仍 100)⇒ 48099-100 ≫ 24000 ⇒ Normal
            Assert.That(_novelty.Peek(1, (int)SkillId.采集, 3, 100 + Cooldown + Cooldown - 1),
                Is.EqualTo(NoveltyClass.Stale),
                "lastSeenTick 须被推进到第二次采集的 tick(24100)。若这里得到 Normal," +
                "说明冷却外**没有**推进 —— 冷却永远不会重新开始,该品种此后恒定 Normal");

            // 再等满一个冷却 ⇒ 回到 Normal(边界另一侧)
            Assert.That(_novelty.Peek(1, (int)SkillId.采集, 3, 100 + Cooldown + Cooldown),
                Is.EqualTo(NoveltyClass.Normal),
                "从 24100 起算满 24000 ⇒ 恰在边界上 ⇒ Normal(冷却判定用 ≥,不是 >)");
        }

        // ══════════════════════════════════════════════════════════════
        // AC-17-14d —— 按玩家独立(GDD :500,不共享)
        // ══════════════════════════════════════════════════════════════

        [Test]
        public void test_ac1714d_twoPlayersFirstHarvestSameSpecies_bothGetFirst()
        {
            _orchestrator.Resolve(Req(tick: 100, actorId: 1, objectId: 3));
            _orchestrator.Resolve(Req(tick: 100, actorId: 2, objectId: 3));

            Assert.That(_growth.Calls[0].Novelty, Is.EqualTo(NoveltyClass.First), "玩家 1 首采");
            Assert.That(_growth.Calls[1].Novelty, Is.EqualTo(NoveltyClass.First),
                "玩家 2 首采同一品种也是 First —— 新颖度按玩家独立(GDD :500),不共享字典");
            Assert.That(_novelty.Count, Is.EqualTo(2),
                "两个 actor 各一条记录(键含 actorId)");
        }

        // ══════════════════════════════════════════════════════════════
        // AC-17-14e —— 失败采集不消耗「首次」奖励
        // ══════════════════════════════════════════════════════════════

        [Test]
        public void test_ac1714e_failedHarvest_leavesNoveltyStateUntouched()
        {
            // ⚠️ **非法入参的选法就是本夹具的全部判别力**(2026-10-27 证伪轮 M3 首轮实测)。
            //
            // 原稿用 `objectId: -1`,那是**假绿**:`NoveltyTracker.Query` **自己也守卫**
            // `objectId < 0`(`NoveltyTracker.cs:73-75`),且该守卫在 `_lastSeenTicks[key] = …`
            // **之前** ⇒ 即使 `Query` 被挪到 `Validate` 之前, 它也只是抛、**不留痕**。
            // 首轮 M3 红了 7 条,但**没有一条**来自这里的 `Count == 0` —— 红的全是
            // 「Query 被调两次」的副作用 ⇒ **「定序」这个属性当时零覆盖**。
            //
            // ⇒ 非法入参取 **`qty: 0`**:它是 `Validate` **独有**的守卫(`Query` 不看 qty)。
            //   `Query` 若早于 `Validate`,便会**正常跑完并把本键写进字典** ⇒ `Count` 变 1 ⇒ 红。
            // ✅ 边界复算(离线):qty=0 只命中 `Validate` 的 `r.Qty < 1`(`:362`);
            //    `tick/actorId/nodeId/itemKey/outQuality/skillId/objectId` 全合法(均 ≥ 0 / ≥ 1),
            //    故异常**必定**来自 qty 那一条,不可能是别的守卫抢先。
            // ⚠️ actorId / objectId **与下方合法请求同键**(1, 采集, 3)—— 这样「首次奖励已被
            //    烧掉」不仅在 `Count` 上可见,还在第二条断言的**载荷**上可见(会拿到 Stale)。
            var bad = new HarvestRequest(
                tick: 100, actorId: 1, nodeId: 0, nodeCell: new WorldPos(0, 0, 0),
                itemKeyOrdinal: 0, qty: 0, outQuality: 2,
                skillId: (int)SkillId.采集, objectId: 3, xpGain: Fix.One);

            Assert.Throws<ArgumentOutOfRangeException>(() => _orchestrator.Resolve(bad));

            Assert.That(_novelty.Count, Is.EqualTo(0),
                "失败的采集**零副作用** —— 追踪器仍空。若这里非 0,说明 `Query` 排在 " +
                "`Validate` **之前**:一次非法请求白白烧掉了该品种的「首次」奖励");

            // 随后一次合法采集仍须拿到 First(奖励没被烧掉的行为面)
            _orchestrator.Resolve(Req(tick: 200, actorId: 1, objectId: 3));
            Assert.That(_growth.Calls.Single().Novelty, Is.EqualTo(NoveltyClass.First),
                "失败之后的首采仍是 First —— 与上一条类型面断言互为行为面证据");
        }

        // ══════════════════════════════════════════════════════════════
        // AC-17-14f —— 类型面无入参(反射,非 grep)
        // ══════════════════════════════════════════════════════════════

        [Test]
        public void test_ac1714f_harvestRequestHasNoNoveltyMember()
        {
            // ⚠️ **反射而非 grep**:改字段名 / 加属性 / 换大小写都逃不掉这个扫描面。
            //    grep "Novelty" 会漏掉 `public NoveltyClass NoveltyHint { get; }` 这类改名,
            //    也会把注释里的 "novelty" 误算成命中。
            var t = typeof(HarvestRequest);
            var members = t.GetMembers(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.DeclaringType == t)
                .Where(m => m.Name.IndexOf("novelty", StringComparison.OrdinalIgnoreCase) >= 0)
                .Select(m => m.MemberType + " " + m.Name)
                .ToArray();

            Assert.That(members, Is.Empty,
                "HarvestRequest 不得有任何 novelty 名下的成员(AC-17-14f)。" +
                "求值只在编排器内部发生 ⇒ 调用方**在类型层面无法**传错。实测残留:" +
                string.Join(", ", members));
        }

        [Test]
        public void test_ac1714f_orchestratorTakesNoveltyTrackerAndQueriesItItself()
        {
            // 类型面:构造函数须有 `NoveltyTracker` 形参(否则它无处求值),
            // 且**不得**有 `NoveltyClass` 形参(那会把求值权交回调用方)。
            var ctor = typeof(HarvestOrchestrator).GetConstructors().Single();
            var paramTypes = ctor.GetParameters().Select(p => p.ParameterType).ToArray();

            Assert.That(paramTypes.Any(p => p == typeof(NoveltyTracker)), Is.True,
                "编排器须经构造注入 NoveltyTracker");
            Assert.That(paramTypes.Any(p => p == typeof(NoveltyClass)), Is.False,
                "编排器不得收 NoveltyClass 入参 —— 那等于把求值交回调用方");

            // 行为面补充:载荷里的 novelty **不是**调用方传的 —— 两个不同 tick 的同一请求
            // 得到不同的 novelty,这只能由内部状态推出。调用方两次传的入参**完全相同**。
            _orchestrator.Resolve(Req(tick: 100, actorId: 1, objectId: 3));
            _orchestrator.Resolve(Req(tick: 200, actorId: 1, objectId: 3));

            Assert.That(_growth.Calls[0].Novelty, Is.EqualTo(NoveltyClass.First));
            Assert.That(_growth.Calls[1].Novelty, Is.EqualTo(NoveltyClass.Stale),
                "同一份入参(除 tick)两次调用得到不同 novelty ⇒ 求值确实在编排器内部," +
                "由追踪器状态推出,而非调用方指定");
        }

        // ══════════════════════════════════════════════════════════════
        // AC-17-14g —— 生产装配恰一个点
        // ══════════════════════════════════════════════════════════════

        [Test]
        public void test_ac1714g_compositionRoot_hasExactlyOneOrchestratorConstructionSite()
        {
            // ⚠️ **为什么扫源码**:`new HarvestOrchestrator(` 的调用点**留不进类型面**
            //    (构造函数调用不留痕,反射只能看到「有这个构造函数」)。
            //    判定「装配点有几个」的唯一可用形态 = 数调用点。
            //    改类型名 / 加别名 / 换命名空间都改不了「这行文本在不在」。
            string path = Path.Combine(RepoRoot(), "unity", "Assets", "Gameplay.Boot",
                                       "CompositionRoot.cs");
            Assert.That(File.Exists(path), Is.True, "生产装配根缺失:" + path);

            string src = File.ReadAllText(path);
            int sites = CountCodeOccurrences(src, "new HarvestOrchestrator(");

            // 下界:剥注释这一步若把真代码也剥掉了,`0` 会被误读成「未接线」——
            // 加一条「扫描面未失效」的守卫,让剥注释逻辑的 bug 与「真·零装配点」可分。
            Assert.That(CountCodeOccurrences(src, "class CompositionRoot"), Is.EqualTo(1),
                "扫描面自检:剥注释后仍须见到 `class CompositionRoot` —— 为 0 说明" +
                "剥注释的实现在啃掉真代码(那时下面的断言结果无意义)");

            Assert.That(sites, Is.EqualTo(1),
                "生产装配点须**恰 1** 处 —— 零处 = 未接线(采集链在运行期不存在);" +
                "多处 = 第二真源(两个编排器各自推进 NoveltyTracker,冷却状态分叉)。实测 " +
                sites + " 处:" + path);
        }

        [Test]
        public void test_ac1714g_compositionRoot_hasExactlyOneNoveltyTrackerConstructionSite()
        {
            // 同上,但针对追踪器本身 —— 两处 `new NoveltyTracker(` 会让诊断与采集
            // 各持一个追踪器,那是「按玩家独立」在**装配层**被破坏(比实现错误更隐蔽)。
            string path = Path.Combine(RepoRoot(), "unity", "Assets", "Gameplay.Boot",
                                       "CompositionRoot.cs");
            string src = File.ReadAllText(path);
            int sites = CountCodeOccurrences(src, "new NoveltyTracker(");

            Assert.That(sites, Is.EqualTo(1),
                "`new NoveltyTracker(` 须恰 1 处 —— 诊断与采集共用同一实例" +
                "(键含 skillId,两域永不共键 ⇒ 共用安全且必需)。实测 " + sites + " 处");
        }

        [Test]
        public void test_ac1714g_productionBag_exposesHarvest_wiredToTheSameNoveltyTracker()
        {
            // 装配袋的可观测面:Harvest 非 null,且它用的追踪器**就是**袋里那一个。
            Assert.That(_bag.Harvest, Is.Not.Null,
                "生产装配须暴露 HarvestOrchestrator —— null 即「采集链在运行期不存在」");
            Assert.That(_bag.Novelty, Is.Not.Null, "生产装配须暴露 NoveltyTracker");

            // 同源判据:**行为面**,不是比引用(引用比不了私有字段)。
            // 经由袋里的 Harvest 采一次,再问袋里的 Novelty ——若两者共用实例,状态可见。
            _bag.Harvest.Resolve(Req(tick: 500, actorId: 9, objectId: 4));

            Assert.That(_bag.Novelty.Peek(9, (int)SkillId.采集, 4, 500),
                Is.Not.EqualTo(NoveltyClass.First),
                "经 `bag.Harvest` 采一次后,`bag.Novelty` 须能看到该 (actor, skill, object) —— " +
                "若仍是 First,说明两者**不是**同一个追踪器(装配层建了第二个)");
        }

        [Test]
        public void test_ac1714g_productionBag_harvestWritesThroughToTheStream()
        {
            // 生产装配的**端到端**可跑性:经袋里的 Harvest 采一次 ⇒ 四条事件各就各位。
            // ⚠️ 这一步同时是 AC-17-01 的**生产装配面**证据 —— 与
            //    `harvest_orchestrator_test.test_ac1701_productionAdapter_writesAllThreeKinds_inOrder`
            //    (手工构造适配器)互补:本条走**装配袋**,那条走**手工接线**。
            int before = _bag.Stream.Events.Count;
            _bag.Harvest.Resolve(Req(tick: 600, actorId: 9, nodeId: 1, objectId: 2));

            var kinds = _bag.Stream.Events.Skip(before).Select(e => e.Kind).ToArray();
            Assert.That(kinds, Is.EqualTo(new[]
                {
                    EventKind.DropSpawned,
                    EventKind.ResourceHarvested,
                    EventKind.DropClaimed,
                    EventKind.SkillGrown,
                }),
                "生产装配下一次采集须落四条(三世界 + 一病史),实测:" +
                string.Join(" → ", kinds));
        }

        // ══════════════════════════════════════════════════════════════
        // 夹具
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// 数 **代码行**里出现的构造点次数 —— **先剥注释**,再匹配。
        /// </summary>
        /// <remarks>
        /// ⚠️ **不剥注释会自指**(2026-10-27 首跑实测):`CompositionRoot.cs` 里为了讲清
        /// 「本处须恰一个装配点」,**注释里写了那个字面串**;裸 `IndexOf` 把注释一起数进去
        /// ⇒ 实测 2 处,而真实的代码构造点只有 1 处。夹具因此在**生产码完全正确**时报红。
        /// 这与 `feedback_self_referential_fixture` 同族但方向相反:那条讲「断言引用被测常量
        /// ⇒ 变异两边一起动」,本条是「**夹具的判据面包含了它自己的说明文字**」。
        /// ⇒ 判据必须落在**代码面**:剥掉 `//` 之后的部分(本文件无字符串里含 `//` 的行,
        /// 且 `CompositionRoot.cs` 亦无 —— 该前提由下面的「剥后仍须 ≥ 1」下界断言兜住)。
        /// </remarks>
        private static int CountCodeOccurrences(string source, string needle)
        {
            int count = 0;
            foreach (string raw in source.Split('\n'))
            {
                string line = raw;
                int comment = line.IndexOf("//", StringComparison.Ordinal);
                if (comment >= 0) line = line.Substring(0, comment);

                int idx = 0;
                while ((idx = line.IndexOf(needle, idx, StringComparison.Ordinal)) >= 0)
                {
                    count++;
                    idx += needle.Length;
                }
            }
            return count;
        }

        /// <summary>仓库根 —— 与本文件同名的先例 `<c>clinicmachine_tables_schema_test.cs</c>`
        /// 用同样的相对跳法(<c>Assets/Tests/EditMode/Foraging</c> ⇒ 上五级)。</summary>
        private static string RepoRoot()
        {
            string thisFile = new System.Diagnostics.StackTrace(true)
                .GetFrame(1).GetFileName();
            return Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(thisFile) ?? ".", "..", "..", "..", "..", ".."));
        }

        private static HarvestRequest Req(
            long tick = 10, int actorId = 1, int nodeId = 0,
            WorldPos? cell = null, int itemKey = 0, int qty = 1,
            int objectId = 0, Fix? xpGain = null)
            => new HarvestRequest(
                tick, actorId, nodeId, cell ?? new WorldPos(3, 0, 7),
                itemKey, qty, outQuality: 2,
                skillId: (int)SkillId.采集, objectId, xpGain ?? Fix.One);

        /// <summary>记录成长调用的 spy(与 <c>harvest_orchestrator_test</c> 的同名件同形)。</summary>
        private sealed class SpyGrowthEmitter : IGrowthEmitter
        {
            public readonly System.Collections.Generic.List<
                (long Tick, int ActorId, int SkillId, int ObjectId,
                 NoveltyClass Novelty, int? Level, Fix XpGain)> Calls
                = new System.Collections.Generic.List<(long, int, int, int, NoveltyClass, int?, Fix)>();

            public void Emit(long tick, int actorId, int skillId, int objectId,
                             NoveltyClass novelty, int? level, Fix xpGain)
                => Calls.Add((tick, actorId, skillId, objectId, novelty, level, xpGain));
        }

        /// <summary>记录 17 提出的 Drop 要求(不落流)。本文件的 AC 都不断 Drop 面。</summary>
        private sealed class RecordingDropWriter : IDropWriter
        {
            public int SpawnCount { get; private set; }
            public int ClaimCount { get; private set; }

            public void Spawn(long tick, ItemInstanceId instanceId, WorldPos spawnAnchor, int itemKey, int qty)
                => SpawnCount++;

            public void Claim(long tick, ItemInstanceId instanceId, int claimer)
                => ClaimCount++;
        }
    }
}
