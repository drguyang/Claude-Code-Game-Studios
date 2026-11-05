// 「出诊类 event_key 集从**数据**派生」的判据 —— `TD-randomevents-06`(2026-11-05)。
//
// ▍只读夹具声明(承 `.claude/rules/test-standards.md` 的具名声明义务)
//   读 **`assets/data/52_event_pool.json`**(作者态)与 **`unity/Assets/DataCooked/52_event_pool.cooked.bytes`**(产物)。
//   **为什么必须读真表而非内联**:本条钉的正是「数据改了、代码要不要跟着改」——
//   内联一个合成集合会把本债要消灭的形态(集合写死在代码里)重新变成绿。
//
// ▍本件钉什么
//   ① 真表绑定后,`is_house_call = true` 的 key 恰是 `{2002, 2003}`(升序);
//   ② 烘焙产物解码后是**同一组** key(数据 → 烘焙 → 读方,整条链);
//   ③ 缺 `is_house_call` ⇒ **构建期拒收**(不是默认 false —— 「没定」与「定了说不是」必须可分);
//   ④ 非布尔 ⇒ 拒收。
//
// ▍本件**不**证什么(禁借绿)
//   · 不证「生产上 `isOnExpedition` 已经能为真」—— 那还要求医疗条目真能产病人(`TD-randomevents-03`)。
//   · 不证值本身是**对的** —— 哪些 key 是急召出诊归内容侧(37 定稿逐条回填);本件只证
//     「真表里标了几条,派生出来就是那几条」。
//
// 权威:GDD random-events.md 规则十二 12.2(`random-events.md:563`)· 规则二(池条目 schema)·
//       ADR-014(两阶段烘焙 / 未知键硬失败)

using System.Collections.Generic;
using System.IO;
using System.Linq;
using DaYiJingCheng.EditorTools.Bake;
using DaYiJingCheng.Gameplay.Presentation;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.RandomEvents
{
    [TestFixture]
    public sealed class house_call_keys_from_pool_test
    {
        private static readonly string RepoRoot = ComputeRepoRoot();

        private static string ComputeRepoRoot(
            [System.Runtime.CompilerServices.CallerFilePath] string thisFile = "")
            => Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(thisFile) ?? ".", "..", "..", "..", "..", ".."));

        private static string SourcePath
            => Path.Combine(RepoRoot, "assets", "data", "52_event_pool.json");

        private static string CookedPath
            => Path.Combine(RepoRoot, "unity", "Assets", "DataCooked", "52_event_pool.cooked.bytes");

        private static int[] HouseCallKeys(IReadOnlyList<EventPoolEntryData> entries)
            => entries.Where(e => e.IsHouseCall).Select(e => e.Key).ToArray();

        // ── ① 真表:派生集合恰为 {2002, 2003} ──────────────────────────────

        [Test]
        public void test_realPool_binder_derivesHouseCallKeys()
        {
            EventPoolBinder.BindResult bound = EventPoolBinder.Bind(File.ReadAllText(SourcePath));

            Assert.That(HouseCallKeys(bound.Entries), Is.EqualTo(new[] { 2002, 2003 }),
                "真表 `is_house_call = true` 的条目 —— 装配层据此派生 `houseCallEventKeys`," +
                "不再传 `Array.Empty<int>()`(改前那个写法让 `ActiveHouseCallQuery` 恒假)");
        }

        [Test]
        public void test_realPool_houseCallEntriesAreMedicalAndOnTravelPath()
        {
            // 不是「断言与实现同义」:它钉的是**语义自洽** —— 急召出诊 = 要你走过去的那种求诊,
            // 所以必然是 medical 且锚在 travel_path。若将来有人把一条 clinic_front 的
            // 遭遇标成急召,这条会红,迫使去核内容。
            EventPoolBinder.BindResult bound = EventPoolBinder.Bind(File.ReadAllText(SourcePath));

            foreach (EventPoolEntryData e in bound.Entries)
            {
                if (!e.IsHouseCall) continue;
                Assert.That(e.Handler, Is.EqualTo(PoolHandler.Medical),
                    $"key {e.Key} 标成急召出诊却不是医疗条目");
                Assert.That(e.SpawnAnchor, Is.EqualTo(1),
                    $"key {e.Key} 标成急召出诊却不在 travel_path 锚(1)——「出诊」的前提是要走出去");
            }
        }

        // ── ② 产物:烘焙 → 解码,仍是同一组 ────────────────────────────────

        [Test]
        public void test_cookedArtifact_roundTripsIsHouseCall()
        {
            byte[] bytes = File.ReadAllBytes(CookedPath);
            EventPoolDataSet decoded = EventPoolCookedCodec.Read(bytes);

            EventPoolBinder.BindResult bound = EventPoolBinder.Bind(File.ReadAllText(SourcePath));

            Assert.That(HouseCallKeys(decoded.Entries), Is.EqualTo(HouseCallKeys(bound.Entries)),
                "烘焙产物与源表必须同组 —— 不一致 = 产物过期(须重跑「烘焙 52 池条目表」)");
            Assert.That(EventPoolCookedCodec.EntryBytes, Is.EqualTo(32),
                "条目宽度 32 B(schema_version 2 追加 is_house_call);" +
                "改回 28 会让读方越界或错位");

            // 装配层取的就是这个方法(`CompositionRoot`: `houseCallEventKeys = eventPool.HouseCallKeys()`)
            // ⇒ 本条同时证「生产派生路径」,不是另写一份等价遍历。
            Assert.That(decoded.HouseCallKeys(), Is.EqualTo(new[] { 2002, 2003 }),
                "解出的池表经 `HouseCallKeys()` 必须给出同一组 —— 装配层就靠它填整数集");
        }

        // ── 派生方法本身:空表 / 全 false / 升序 ─────────────────────────

        [Test]
        public void test_houseCallKeys_defaultEmpty_isEmptyArrayNotNull()
        {
            Assert.That(default(EventPoolDataSet).HouseCallKeys(), Is.Not.Null,
                "`default` 形态须给空数组而不是 null —— null 会在装配层变成 NRE,而它本是合法形态");
            Assert.That(default(EventPoolDataSet).HouseCallKeys(), Is.Empty);
        }

        [Test]
        public void test_houseCallKeys_noneMarked_isEmpty()
        {
            var pool = new EventPoolDataSet(new[]
            {
                new EventPoolEntryData(1, 100, 0, 1, 0, PoolHandler.Encounter, 0, isHouseCall: false),
            }, 3, 2, 2, 1, 0u, 2u);

            Assert.That(pool.HouseCallKeys(), Is.Empty, "一条都没标 ⇒ 空集(与改前的硬编码同形,但**可观测**了)");
        }

        [Test]
        public void test_houseCallKeys_preservesAscendingPoolOrder()
        {
            var pool = new EventPoolDataSet(new[]
            {
                new EventPoolEntryData(2002, 100, 1, 1, 0, PoolHandler.Medical, 0, isHouseCall: true),
                new EventPoolEntryData(2003, 100, 1, 1, 0, PoolHandler.Medical, 0, isHouseCall: true),
                new EventPoolEntryData(3001, 100, 2, 0, 0, PoolHandler.Medical, 0, isHouseCall: false),
            }, 3, 2, 2, 1, 0u, 2u);

            Assert.That(pool.HouseCallKeys(), Is.EqualTo(new[] { 2002, 2003 }),
                "集合按池表序(= key 升序)⇒ 确定性判据的一部分,不许另排一次");
        }

        // ── ③④ 负控:缺字段 / 非布尔 ⇒ 构建期拒收 ──────────────────────────

        private static string PoolWith(string entryBody)
            => "{ \"schema_version\": 2, " +
               "\"night_threat_mult\": { \"num\": 3, \"den\": 2 }, " +
               "\"tod_mult_max\": { \"num\": 2, \"den\": 1 }, " +
               "\"entries\": [ " + entryBody + " ] }";

        private const string EntryHead =
            "{ \"key\": 2002, \"w_base\": 100, \"tier\": \"opportunity\", " +
            "\"spawn_anchor\": \"travel_path\", \"trigger_mode\": \"random\", " +
            "\"handler\": \"medical\", \"cause_flag\": \"\"";

        [Test]
        public void test_missingIsHouseCall_isRejected()
        {
            string json = PoolWith(EntryHead + " }");   // 故意不给 is_house_call

            var ex = Assert.Throws<BakeValidationException>(
                () => EventPoolBinder.Bind(json));

            Assert.That(string.Join("\n", ex.Errors), Does.Contain("is_house_call"),
                "缺字段必须具名报错 —— 默认 false 会让「内容侧还没定」与「定了、说不是」不可分");
        }

        [Test]
        public void test_nonBooleanIsHouseCall_isRejected()
        {
            string json = PoolWith(EntryHead + ", \"is_house_call\": 1 }");   // 整数而非布尔

            var ex = Assert.Throws<BakeValidationException>(
                () => EventPoolBinder.Bind(json));

            Assert.That(string.Join("\n", ex.Errors), Does.Contain("布尔"),
                "整数 0/1 会让作者态不可读 —— 与 `tier`/`handler` 用字符串枚举同一条口径");
        }
    }
}
