// inventory story-002 —— instance_id 铸造权威与高水位重构(S21-2)
//
// 权威来源:
//   design/gdd/inventory-and-items.md 规则二(:215)· EC 行 :648(扫描集 = 三流 ∪ BakedInitial)
//   ADR-010 §五(机制 A)· §三 义务 12(structure_id 共用 IIdAuthority 空间)
//   ADR-006 Amendment B(三条不变量)· ADR-005(主机唯一)
//   production/epics/inventory-items/story-002-id-authority-and-highwater.md
//     AC-20-01 · AC-20-02 · AC-20-03 前置子件(R3)· AC-20-04
//
// ⚠️ **AC 记分(禁把「夹具绿」读成「四条全绿」)**:
//     ✅ AC-20-01  唯一铸造源(源面扫描 + 空扫描守卫)
//     ✅ AC-20-02  客户端零铸(按**程序集归属**判;联机半边 NOT-RUN,见末节)
//     ⚠️ AC-20-03  **只落地「扫描集形状」** —— 三流 ∪ BakedInitial 的重载已实现且可判;
//                  **但 R3 未落地**(54 未导出基线的实例 id 位、7a 未接入)⇒ 生产路径
//                  今天仍只能传 null。本文件交付的是**可复现撞号的证据**,不是「撞号已修」。
//     ✅ AC-20-04  id 空间与堆叠键不相混
//
// ⚠️ **本文件不证明的事**(如实,禁借绿):
//   · 联机「意图上行 / 主机回填」半边 —— R1(ADR-001 窄修订)未立,`NOT-RUN`
//   · `BakedInitial` 里**真的**存在物品实例 id —— 今天的世界数据只有结构基线,没有物品基线
//   · `structure_id` 与 `instance_id` **真的共用同一空间** —— 实现上 `StructureKinds` 用的是
//     独立计数器(`_nextStructureId`,起点 1),与 ADR-010 §三 义务 12 不符 ⇒ 已外抛登记
//
// 📖 **只读外部夹具声明**(承 `.claude/rules/test-standards.md` 的具名声明义务):
//   本文件**读**仓库内 `.cs` 源文件(不写):
//     · `unity/Assets/**/*.cs`(排除 `/Tests/` 与 `/Editor.Tools*`)—— AC-20-01 的「谁在 `new
//       ItemInstanceId(`」与「有没有第二计数器」只能从**生产源码全文**判定,内联字面量无法表达;
//     · `unity/Assets/Gameplay.Presentation/**` 与 `unity/Assets/Gameplay.UI/**` —— AC-20-02 的
//       「客户端程序集零铸造通道」同理,**扫的是真树**,内联清单会在新增文件时静默失效。
//   ⇒ 必须读真树:判据的对象就是「当前仓库里有没有这一行」,任何内联副本都会随树漂移。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.EditMode.Inventory
{
    [TestFixture]
    public sealed class IdAuthorityHighWaterTest
    {
        private InMemoryBlobPool _pool;
        private PayloadEncoder _encoder;
        private PayloadDecoder _decoder;

        [SetUp]
        public void SetUp()
        {
            _pool = new InMemoryBlobPool();
            _encoder = new PayloadEncoder(_pool);
            _decoder = new PayloadDecoder(_pool);
        }

        // ══════════════════════════════════════════════════════════════
        // AC-20-01 —— 唯一铸造源(源面扫描)
        // ══════════════════════════════════════════════════════════════

        /// <summary>AC-20-01:生产树里 `new ItemInstanceId(` **只允许**出现在 `IdAuthority` 实现内。
        /// <para><b>为什么扫源码而不是反射</b>:`new` 是语法事实,反射看不出「谁 new 的」。
        /// 本判据要抓的正是「有人写了一行本地铸造」,源码面才是它出现的唯一形态。</para>
        /// <para><b>空扫描 = 红</b>:删/移走扫描面会让本判据空转通过(假绿)。</para>
        /// </summary>
        [Test]
        public void test_ac2001_onlyAuthorityMintsItemInstanceId()
        {
            List<string> files = ProductionSources();
            Assert.That(files.Count, Is.GreaterThan(200),
                $"扫描面异常小({files.Count} 个 .cs)—— 判据会空转通过(假绿)");

            var violations = new List<string>();
            int hits = 0;
            foreach (string path in files)
            {
                string code = StripComments(File.ReadAllText(path));
                if (!code.Contains("new ItemInstanceId(", StringComparison.Ordinal)) continue;
                hits++;
                if (!path.EndsWith(Path.Combine("Sim", "ItemDatabase", "IdAuthority.cs"), StringComparison.Ordinal))
                    violations.Add(Rel(path));
            }

            Assert.That(hits, Is.GreaterThan(0),
                "生产树里零个 `new ItemInstanceId(` —— 扫描面失真(枚举器没看对地方),判据无意义");
            Assert.That(violations, Is.Empty,
                "`ItemInstanceId` 只许由 `IIdAuthority.ItemInstanceId.Next()` 发放(规则二)。" +
                "以下文件自行铸造 ⇒ 读档后必撞号:\n" + string.Join("\n", violations));
        }

        /// <summary>AC-20-01 的另一面:**20 侧不得有私有实例计数器**。
        /// <para>判据 = 生产树里除 `IdAuthority` 外,零个「名字像物品实例计数器」的私有字段。
        /// 抓的是「自己维护 `_nextItemId++`」这种绕过发号权威的写法。</para>
        /// </summary>
        [Test]
        public void test_ac2001_noPrivateInstanceCounterOutsideAuthority()
        {
            List<string> files = ProductionSources();
            Assert.That(files.Count, Is.GreaterThan(200), "扫描面异常小 —— 判据会空转");

            string[] patterns = { "_nextItem", "_nextInstance", "_itemCounter", "_instanceCounter" };
            var violations = new List<string>();
            foreach (string path in files)
            {
                if (path.EndsWith(Path.Combine("Sim", "ItemDatabase", "IdAuthority.cs"), StringComparison.Ordinal))
                    continue;
                string code = StripComments(File.ReadAllText(path));
                foreach (string p in patterns)
                    if (code.Contains(p, StringComparison.Ordinal))
                        violations.Add($"{Rel(path)}: {p}");
            }

            Assert.That(violations, Is.Empty,
                "发现疑似本地物品实例计数器(规则二禁止第二计数器):\n" + string.Join("\n", violations));
        }

        // ══════════════════════════════════════════════════════════════
        // AC-20-02 —— 客户端零铸(按程序集归属)
        // ══════════════════════════════════════════════════════════════

        /// <summary>AC-20-02:客户端程序集(`Gameplay.Presentation` / `Gameplay.UI`)**
        /// 零 `IIdAuthority` 引用、零 `NextItemInstanceId` 调用**。
        /// <para><b>为什么按程序集归属判</b>:P0 单机「客户端 = 同进程」,按运行时判不出客户端身份;
        /// 程序集归属是唯一可判的边界,也与 ADR-025 的清单封闭性同源。</para>
        /// <para>联机「意图上行 / 主机回填」半边 <b>NOT-RUN</b>(R1 未立),本条不代它作保。</para>
        /// </summary>
        [Test]
        public void test_ac2002_clientAssembliesNeverMint()
        {
            string[] clientDirs = { "Gameplay.Presentation", "Gameplay.UI" };
            var files = new List<string>();
            foreach (string d in clientDirs)
            {
                string dir = Path.Combine(UnityAssetsRoot(), d);
                Assert.That(Directory.Exists(dir), Is.True, $"客户端程序集目录不存在 —— {dir}");
                files.AddRange(Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories));
            }
            Assert.That(files.Count, Is.GreaterThan(50),
                $"客户端扫描面异常小({files.Count})—— 判据会空转通过(假绿)");

            string[] forbidden = { "IIdAuthority", "NextItemInstanceId" };
            var violations = new List<string>();
            foreach (string path in files)
            {
                string code = StripComments(File.ReadAllText(path));
                foreach (string token in forbidden)
                    if (code.Contains(token, StringComparison.Ordinal))
                        violations.Add($"{Rel(path)}: {token}");
            }

            Assert.That(violations, Is.Empty,
                "客户端程序集出现铸造通道(规则二:客户端零自铸,id 由主机回填):\n" +
                string.Join("\n", violations));
        }

        // ══════════════════════════════════════════════════════════════
        // AC-20-03 —— 高水位扫描集 = 三流 ∪ BakedInitial
        // ══════════════════════════════════════════════════════════════

        /// <summary>正例:基线参与时 `next = max(三流 ∪ 基线) + 1`。
        /// 三流 {5,9,12} + 基线 {20} ⇒ **21**。</summary>
        [Test]
        public void test_ac2003_highWaterIncludesBakedInitial()
        {
            var authority = new IdAuthority();
            ItemInstanceHighWaterCollector.CollectAndReconstruct(
                Streams(5, 9, 12), _decoder, new long[] { 20 }, authority);

            Assert.That(authority.NextItemInstanceId().Value, Is.EqualTo(21L),
                "扫描集含 BakedInitial 时,next 必须越过基线最大 id(AC-20-03)");
        }

        /// <summary>对照:基线**不参与**时 `next = max(三流) + 1` = **13**。
        /// <para>对照组自身不是缺陷 —— 它钉的是「两个重载真的不同」(否则正例可能因别的原因通过)。</para>
        /// </summary>
        [Test]
        public void test_ac2003_withoutBakedInitial_controlIsThirteen()
        {
            var authority = new IdAuthority();
            ItemInstanceHighWaterCollector.CollectAndReconstruct(Streams(5, 9, 12), _decoder, authority);

            Assert.That(authority.NextItemInstanceId().Value, Is.EqualTo(13L),
                "不含基线时 next = max(三流) + 1 = 13");
        }

        /// <summary>**R3 反例交付**:基线缺失时,撞号**可复现**。
        /// <para>构造:基线里有 id = 20 的预摆物,而重建时未把基线算进去 ⇒ 从 13 起发号,
        /// 第 8 次发放恰好得到 **20** ⇒ 与基线预摆物**同号**。</para>
        /// <para>⚠️ **本用例是债的证据,不是验收** —— 它在 R3 落地(基线 id 进扫描集)后
        /// 仍应通过(R3 让它「不再是生产路径」,而不是让这条构造失效)。</para>
        /// </summary>
        [Test]
        public void test_ac2003_missingBakedInitial_collisionIsReproducible()
        {
            var authority = new IdAuthority();
            ItemInstanceHighWaterCollector.CollectAndReconstruct(Streams(5, 9, 12), _decoder, authority);

            long minted = -1;
            for (int i = 0; i < 8; i++) minted = authority.NextItemInstanceId().Value;

            Assert.That(minted, Is.EqualTo(20L),
                "R3 未落地时,发号必然走到基线预摆物的 id —— 这是撞号的**构造性证明**");
        }

        /// <summary>零复用:重构后发出的号**严格大于**扫描到的最大 id。</summary>
        [Test]
        public void test_ac2003_zeroReuseAfterReconstruct()
        {
            var authority = new IdAuthority();
            ItemInstanceHighWaterCollector.CollectAndReconstruct(
                Streams(5, 9, 12), _decoder, new long[] { 20 }, authority);

            var minted = new List<long>();
            for (int i = 0; i < 5; i++) minted.Add(authority.NextItemInstanceId().Value);

            Assert.That(minted.Min(), Is.GreaterThan(20L), "不得复用任何已见 id(机制 A 不变量②)");
            Assert.That(minted, Is.Ordered, "发号必须单调递增");
            Assert.That(minted.Distinct().Count(), Is.EqualTo(minted.Count), "发号不得重复");
        }

        /// <summary>负哨兵不进高水位:基线里混入 -1 不改变结果。</summary>
        [Test]
        public void test_ac2003_negativeSentinelInBakedInitialIgnored()
        {
            var authority = new IdAuthority();
            ItemInstanceHighWaterCollector.CollectAndReconstruct(
                Streams(5, 9, 12), _decoder, new long[] { 20, -1, -999 }, authority);

            Assert.That(authority.NextItemInstanceId().Value, Is.EqualTo(21L),
                "负值是哨兵(ADR-007 §四),不进 id 空间、不污染高水位");
        }

        /// <summary>`null` 基线 == 空基线(语义已写进重载的 doc):两者都必须等于对照组 13。
        /// <para>⚠️ 这条钉的是「null 不被当成一个神秘的大值」,不是「null 是对的」。</para>
        /// </summary>
        [Test]
        public void test_ac2003_nullBakedEqualsEmpty()
        {
            var a = new IdAuthority();
            ItemInstanceHighWaterCollector.CollectAndReconstruct(Streams(5, 9, 12), _decoder,
                (IEnumerable<long>)null, a);
            var b = new IdAuthority();
            ItemInstanceHighWaterCollector.CollectAndReconstruct(Streams(5, 9, 12), _decoder,
                Array.Empty<long>(), b);

            Assert.That(a.NextItemInstanceId().Value, Is.EqualTo(13L));
            Assert.That(b.NextItemInstanceId().Value, Is.EqualTo(13L));
        }

        // ══════════════════════════════════════════════════════════════
        // AC-20-04 —— id 空间与堆叠键不相混
        // ══════════════════════════════════════════════════════════════

        /// <summary>AC-20-04:同一 `item_key` 的两件物品,**instance_id 仍唯一**。
        /// <para>堆叠键 `(item_key, quality)` 的唯一出处是 21a,且它**非唯一**;
        /// `instance_id` 全序唯一。两者是不同索引,**不得互换**。</para>
        /// </summary>
        [Test]
        public void test_ac2004_sameStackingKeyStillDistinctInstanceIds()
        {
            // 两件同 key(1)、同数量 —— 堆叠键相同,实例 id 必须不同
            SimEvent e1 = DropSpawned(instanceId: 7, itemKey: 1);
            SimEvent e2 = DropSpawned(instanceId: 8, itemKey: 1);

            List<long> ids = ItemInstanceHighWaterCollector.Collect(new[] { e1, e2 }, _decoder);

            Assert.That(ids, Is.EqualTo(new List<long> { 7L, 8L }),
                "堆叠键相同不影响 instance_id 的独立性(AC-20-04)");
            Assert.That(ids.Distinct().Count(), Is.EqualTo(ids.Count),
                "instance_id 必须全序唯一,不得按堆叠键合并");
        }

        /// <summary>AC-20-04 的反面:高水位**只看 id**,不看堆叠键 —— 同 key 两件不让高水位跳两格。</summary>
        [Test]
        public void test_ac2004_highWaterIsIdDrivenNotKeyDriven()
        {
            var authority = new IdAuthority();
            ItemInstanceHighWaterCollector.CollectAndReconstruct(
                new[] { DropSpawned(instanceId: 7, itemKey: 1), DropSpawned(instanceId: 8, itemKey: 1) },
                _decoder, authority);

            Assert.That(authority.NextItemInstanceId().Value, Is.EqualTo(9L),
                "高水位 = max(instance_id) + 1,与堆叠键无关");
        }

        // ══════════════════════════════════════════════════════════════
        // 辅助
        // ══════════════════════════════════════════════════════════════

        private SimEvent DropSpawned(long instanceId, long itemKey = 1)
            => new SimEvent(0L, PatientId.None, -1, EventKind.DropSpawned,
                _encoder.Encode(EventKind.DropSpawned,
                    new DropSpawnedPayload(instanceId, new WorldPos(0, 0, 0), (int)itemKey, 1)));

        /// <summary>造一条含三个 `DropSpawned` 的流(id 乱序给出,验证不依赖流序)。</summary>
        private IReadOnlyList<SimEvent> Streams(params long[] ids)
            => ids.Select(id => DropSpawned(id)).ToList();

        /// <summary>生产树 = `unity/Assets/**/*.cs` 去掉 `/Tests/` 与 `/Editor.Tools/`(工具不进构建)。</summary>
        private static List<string> ProductionSources()
        {
            string root = UnityAssetsRoot();
            Assert.That(Directory.Exists(root), Is.True, $"unity/Assets 不存在 —— {root}");
            var list = new List<string>();
            foreach (string f in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                string p = f.Replace('\\', '/');
                if (p.Contains("/Tests/")) continue;
                if (p.Contains("/Editor.Tools")) continue;
                list.Add(f);
            }
            list.Sort(StringComparer.Ordinal);
            return list;
        }

        private static string UnityAssetsRoot(
            [System.Runtime.CompilerServices.CallerFilePath] string thisFile = "")
        {
            string dir = Path.GetDirectoryName(thisFile) ?? ".";
            // Inventory → EditMode → Tests → Assets
            return Path.GetFullPath(Path.Combine(dir, "..", "..", ".."));
        }

        private static string Rel(string abs)
        {
            string root = Path.GetFullPath(Path.Combine(UnityAssetsRoot(), ".."));
            return abs.StartsWith(root, StringComparison.Ordinal)
                ? abs.Substring(root.Length).TrimStart('/', '\\')
                : abs;
        }

        /// <summary>去 `//` 行注释与 `/* */` 块注释 —— 本类自己的 doc 里就写着这些名字,
        /// 不去注释会**自指打红**(承 `TD-itemdb-02` 的防自指教训)。</summary>
        private static string StripComments(string src)
        {
            var sb = new System.Text.StringBuilder(src.Length);
            int i = 0;
            while (i < src.Length)
            {
                if (i + 1 < src.Length && src[i] == '/' && src[i + 1] == '*')
                {
                    int close = src.IndexOf("*/", i + 2, StringComparison.Ordinal);
                    i = close < 0 ? src.Length : close + 2;
                    continue;
                }
                if (i + 1 < src.Length && src[i] == '/' && src[i + 1] == '/')
                {
                    int nl = src.IndexOf('\n', i);
                    i = nl < 0 ? src.Length : nl;
                    continue;
                }
                sb.Append(src[i]);
                i++;
            }
            return sb.ToString();
        }
    }
}
