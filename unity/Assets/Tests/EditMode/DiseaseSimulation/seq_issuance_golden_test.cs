// `TD-itemdb-02` —— `Seq` 发号器的**金标准对拍夹具**(golden-v2)
//
// 权威:
//   · `tests/golden/eventstream/golden-v2.txt`(金标准,由 Python 独立参考实现生成)
//   · `tests/golden/eventstream/golden_v2_reference.py`(参考实现,与 C# **零共享代码**)
//   · `unity/Assets/Sim/EventStream.cs:262-295`(被对拍的四条分支)
//   · `design/gdd/disease-simulation.md:196-198`(Seq 发号规则)
//   · ADR-012 裁决③(版本化刷新:新版本目录 + 全体平台同时重签 + 旧版保留)
//   · AC-21a-28(**防自指**):C# 侧**只读**金标准,**永不回写**
//
// ═══════════════════════════════════════════════════════════════════════════
// 本夹具补的是哪条债(`TD-itemdb-02` 原判定)
// ═══════════════════════════════════════════════════════════════════════════
// 既有四份金标准(item_database / medcons / telemetry / `golden_hash_v1_test.cs`)
// **无一族场景是发号** —— `grep -rn -e '_currentSeq' -e 'seq_reset' tests/` 零命中。
// ⇒ 发号器是**全案唯一没有跨平台判据的核心纯函数**(它是 `Seq` 的唯一真源,
//   而 `Seq` 是跨流全序键 `(Tick, StreamPriority, Patient, Seq)` 的末位 —— ADR-008 §二)。
//
// ═══════════════════════════════════════════════════════════════════════════
// 为什么走**公开 API**而不是直调内部计数器
// ═══════════════════════════════════════════════════════════════════════════
// ① **可见性**:`_currentSeq` / `_restoredGroupMax` 是 `private`;
//    走反射去读它们会把夹具钉在**实现形状**上(字段改名即红,而行为未变)。
// ② **可达性(更硬的理由,2026-10-30 起草期实测)**:`_restoredGroupMax` **只能**经
//    `Restore(events, foldedRows)` 填充,而 `Restore` 自己会重放这些事件
//    (`EventStream.cs:171-175`)⇒ 「空流 + 已预置组最大号」这个状态**在公开 API 上不可达**。
//    ⇒ ③(恢复批播种)的唯一真实形态 = **`Restore` 之后增量 `Append`**;
//      金标准的 S05/S06/S07 就是照这个形状造的,S11(纯 Append 场景)则**刻意不含 ③**。
// ③ **判据面**:公开 API 的产物 = `stream.Events[i].Seq` —— 正是消费端读到的那个值。

using System;
using System.Collections.Generic;
using System.IO;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.DiseaseSimulation
{
    /// <summary>`Seq` 发号器 vs `golden-v2` 的逐场景对拍。</summary>
    /// <remarks>
    /// <b>防自指(AC-21a-28)</b>:本类**只读** `golden-v2.txt`,任何地方都**不写**它。
    /// 金标准由 `golden_v2_reference.py` 生成 —— 该脚本不经由任何 C# 代码、不读 C# 运行结果。
    /// </remarks>
    [TestFixture]
    public sealed class SeqIssuanceGoldenTest
    {
        /// <summary>金标准文件名(与参考实现的默认输出名一致)。</summary>
        private const string GoldenFileName = "golden-v2.txt";

        private static string GoldenDir()
        {
            // 与既有金标准夹具同一套定位法:从 TestDirectory 往上找含 `tests/golden/eventstream/` 的祖先。
            DirectoryInfo d = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (d != null)
            {
                string candidate = Path.Combine(d.FullName, "tests", "golden", "eventstream");
                if (Directory.Exists(candidate)) return candidate;
                d = d.Parent;
            }
            Assert.Fail("找不到 tests/golden/eventstream/ —— 判据无从跑(本夹具不内联金标准值," +
                        "内联就等于把金标准抄进 C#,破坏 AC-21a-28 的『零共享代码』)");
            return null;
        }

        /// <summary>读金标准 ⇒ 场景名 → Seq 序列(空串 = 空流)。</summary>
        private static Dictionary<string, long[]> LoadGolden()
        {
            string path = Path.Combine(GoldenDir(), GoldenFileName);
            Assert.That(File.Exists(path), Is.True, $"金标准不存在:{path}(须先跑 golden_v2_reference.py)");

            var map = new Dictionary<string, long[]>();
            foreach (string raw in File.ReadAllLines(path))
            {
                if (raw.StartsWith("#", StringComparison.Ordinal)) continue;
                if (raw.Trim().Length == 0) continue;

                int sp = raw.IndexOf(' ');
                Assert.That(sp, Is.GreaterThan(0), $"金标准行格式错(须 `<name> <seq,...>`):{raw}");
                string name = raw.Substring(0, sp);
                string payload = raw.Substring(sp + 1).Trim();

                var seqs = new List<long>();
                if (payload.Length > 0)
                {
                    foreach (string part in payload.Split(','))
                        seqs.Add(long.Parse(part, System.Globalization.CultureInfo.InvariantCulture));
                }
                map[name] = seqs.ToArray();
            }
            Assert.That(map.Count, Is.GreaterThan(0), "金标准解析出零场景 —— 文件坏了");
            return map;
        }

        // ══════════════════════════════════════════════════════════════════
        // 场景定义 —— **逐条**对应 Python 参考实现里的同名函数
        // ══════════════════════════════════════════════════════════════════

        /// <summary>一个场景 = ① 可选恢复批 ② 一串 Append(tick, patient, 显式 Seq)。</summary>
        private readonly struct Scenario
        {
            public readonly string Name;
            public readonly (long tick, int patient, long seq)[] Restored;
            public readonly (long tick, int patient, long seq)[] Appended;

            public Scenario(string name,
                            (long, int, long)[] restored,
                            (long, int, long)[] appended)
            {
                Name = name; Restored = restored; Appended = appended;
            }
        }

        /// <summary>PatientId.None 哨兵(ADR-007 §四)。</summary>
        private const int NonePatient = -1;

        /// <summary>未发号哨兵(O-1:0 是合法首号,不可与「没发号」混淆)。</summary>
        private const long Unassigned = -1;

        private static readonly Scenario[] Scenarios =
        {
            // ── ①② 组复位 / 同组递增 ──
            new Scenario("S01_group_reset_by_tick", null,
                new[] { (0L, 1, Unassigned), (0L, 1, Unassigned), (1L, 1, Unassigned) }),
            new Scenario("S02_group_reset_by_patient", null,
                new[] { (0L, 1, Unassigned), (0L, 1, Unassigned), (0L, 2, Unassigned) }),
            new Scenario("S03_same_group_increment", null,
                new[] { (0L, 1, Unassigned), (0L, 1, Unassigned), (0L, 1, Unassigned) }),

            // ── `TD-processing-07` 的判别力场景:同 tick 两条,Patient=None ──
            //    ⚠️ 若发号按 (Tick, Patient) 复位且 Patient=None,「每 tick 单条」的夹具会让
            //       每条都是新组 ⇒ Seq 恒 0 ⇒ 断言恒真。**同 tick 两条**才可判别。
            new Scenario("S04_same_tick_two_crafts_patientNone", null,
                new[] { (0L, NonePatient, Unassigned), (0L, NonePatient, Unassigned) }),

            // ── ③ 恢复批播种(真实形态 = Restore 后增量 Append)──
            //    ⚠️ 与 `Tests/EditMode/PersistenceService/eventstream_restore_test.cs:109-125`
            //       的既有夹具**逐位相同**(那里断言 4L,本条期望 `2,4` 的末位 = 4)。
            new Scenario("S05_restore_then_append_sameGroup",
                new[] { (5L, 1, 2L) },
                new[] { (5L, 1, Unassigned) }),
            new Scenario("S06_restored_seed_notOverridden_byExplicitSeq",
                new[] { (5L, 1, 0L) },
                new[] { (5L, 1, Unassigned), (5L, 1, Unassigned) }),
            new Scenario("S07_restore_twoGroups_thenAppend",
                new[] { (5L, 1, 1L), (5L, 2, 4L) },
                new[] { (5L, 1, Unassigned), (5L, 2, Unassigned) }),

            // ── ④ 显式 Seq 门 ──
            new Scenario("S08_explicit_seq_preserved", null,
                new[] { (0L, 1, 7L), (0L, 1, Unassigned) }),
            new Scenario("S09_explicit_seq_still_advances_counter", null,
                new[] { (0L, 1, 100L), (0L, 1, Unassigned) }),

            // ── 哨兵 vs 合法 0(O-1 修复)──
            new Scenario("S10_sentinel_vs_legal_zero", null,
                new[] { (0L, 1, Unassigned), (0L, 1, 0L) }),

            // ── 纯 Append 会话的复合(① ② ④;刻意不含 ③,理由见类头)──
            new Scenario("S11_mixed_append_branches", null,
                new[] { (0L, 1, Unassigned), (0L, 1, Unassigned), (0L, 1, 42L),
                        (0L, 1, Unassigned), (1L, 1, Unassigned),
                        (1L, NonePatient, Unassigned), (1L, NonePatient, Unassigned) }),

            // ── 空流 ──
            new Scenario("S12_empty_stream", null, null),
        };

        // ══════════════════════════════════════════════════════════════════
        // 执行:把场景跑在**真** EventStream 上,取入流后的 Seq 序列
        // ══════════════════════════════════════════════════════════════════

        private sealed class SeqAuthority : IIdAuthority
        {
            private int _n = 100;
            public PatientId NextPatientId() => new PatientId(_n++);
            public ItemInstanceId NextItemInstanceId() => new ItemInstanceId(_n++);
        }

        private sealed class PresenceQuery : IPresenceQuery
        {
            public bool IsPresent(PatientId patientId) => true;
            public int PresentCount => 0;
            public bool IsPresentAt(WorldPos cell) => false;
            public IReadOnlyCollection<int> PresentPatientIds() => Array.Empty<int>();
        }

        /// <summary>把一条场景跑出来 ⇒ 入流 Seq 序列(按入流顺序)。</summary>
        /// <remarks>
        /// ⚠️ <b>每条事件必须带**不同**的 `PayloadRef`</b>(2026-10-30 首轮实测踩到)。
        /// `EventStream.Append` 对**未发号**(`Seq &lt; 0`)事件的去重键会补载荷身份
        /// (`EventStream.cs:245-247`,O-4 修复)—— 若所有场景事件都用 `default` 载荷,
        /// 键就全部相同 ⇒ 同组第二条起**被静默去重拒收** ⇒ 实测序列比期望短
        /// (首轮:S03 期望 `0,1,2` 实得 `0`)。
        /// ⇒ 本夹具给每条事件分配唯一 `BlobId`(递增),使**每条都能真的入流**;
        ///    去重语义本身归 `event_stream_test.cs` 的 AC-15 夹具,不在本条判据面内。
        /// </remarks>
        private static long[] RunScenario(Scenario s)
        {
            var stream = new EventStream(new SeqAuthority(), new PresenceQuery());
            int blobId = 1;   // 唯一载荷身份源(见 remarks)

            // ① 恢复批(若有)
            if (s.Restored != null && s.Restored.Length > 0)
            {
                var restored = new SimEvent[s.Restored.Length];
                for (int i = 0; i < s.Restored.Length; i++)
                {
                    var e = s.Restored[i];
                    restored[i] = new SimEvent(e.tick, new PatientId(e.patient), e.seq,
                                               EventKind.SkillGrown, new PayloadRef(blobId++, 0, 1));
                }
                stream.Restore(restored, new List<FoldRow>());
            }

            // ② 增量 Append
            if (s.Appended != null)
            {
                foreach (var a in s.Appended)
                {
                    stream.Append(new SimEvent(a.tick, new PatientId(a.patient), a.seq,
                                               EventKind.SkillGrown, new PayloadRef(blobId++, 0, 1)));
                }
            }

            var seqs = new long[stream.Count];
            for (int i = 0; i < seqs.Length; i++) seqs[i] = stream.Events[i].Seq;
            return seqs;
        }

        // ══════════════════════════════════════════════════════════════════
        // 判据 ①:逐场景对拍金标准
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// **核心判据** —— 12 条场景的入流 `Seq` 序列与 Python 参考实现**逐位相同**。
        /// <para>失败信息逐场景列出「期望 vs 实际」,便于定位是哪条分支漂了。</para>
        /// </summary>
        [Test]
        public void test_seqIssuance_matchesGoldenV2()
        {
            Dictionary<string, long[]> golden = LoadGolden();
            var mismatches = new List<string>();

            foreach (Scenario s in Scenarios)
            {
                Assert.That(golden.ContainsKey(s.Name), Is.True,
                    $"金标准里没有场景 {s.Name} —— 两侧场景集不同步" +
                    "(改场景须**同时**改 golden_v2_reference.py 并重生成,不得只改一边)");

                long[] actual = RunScenario(s);
                long[] expected = golden[s.Name];

                if (actual.Length != expected.Length)
                {
                    mismatches.Add($"{s.Name}: 长度 期望 {expected.Length} / 实际 {actual.Length}");
                    continue;
                }
                for (int i = 0; i < expected.Length; i++)
                {
                    if (actual[i] != expected[i])
                    {
                        mismatches.Add($"{s.Name}[{i}]: 期望 {expected[i]} / 实际 {actual[i]}");
                        break;
                    }
                }
            }

            Assert.That(mismatches, Is.Empty,
                "发号与金标准不符(逐条见下)。⚠️ 若是**有意的**规则变更 ⇒ 须按 ADR-012 裁决③" +
                "升 golden-v3 + 全体平台同时重签,**不得**就地改 golden-v2:\n  " +
                string.Join("\n  ", mismatches));
        }

        // ══════════════════════════════════════════════════════════════════
        // 判据 ②:场景集与金标准**双向**齐备(防「只跑一半」)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 金标准里的**每一条**都必须被本夹具跑到,反之亦然。
        /// <para>⚠️ 单靠判据①会漏「金标准多了场景而夹具没实现」—— 那种情况下 ① 仍全绿
        /// (它只遍历夹具自己的 <see cref="Scenarios"/>),而金标准里多出的场景**永远不被验证**。
        /// 这正是本项目抓过的「扫不到被测对象」形态。</para>
        /// </summary>
        [Test]
        public void test_scenarioSets_matchBothDirections()
        {
            Dictionary<string, long[]> golden = LoadGolden();

            var implemented = new HashSet<string>();
            foreach (Scenario s in Scenarios)
                Assert.That(implemented.Add(s.Name), Is.True, $"场景名重复:{s.Name}");

            var onlyInGolden = new List<string>();
            foreach (string name in golden.Keys)
                if (!implemented.Contains(name)) onlyInGolden.Add(name);

            var onlyInFixture = new List<string>();
            foreach (string name in implemented)
                if (!golden.ContainsKey(name)) onlyInFixture.Add(name);

            Assert.That(onlyInGolden, Is.Empty,
                "金标准里有、本夹具没跑的场景(它们**从未被验证**):" + string.Join(", ", onlyInGolden));
            Assert.That(onlyInFixture, Is.Empty,
                "本夹具有、金标准里没有的场景(金标准过期):" + string.Join(", ", onlyInFixture));

            Assert.That(implemented.Count, Is.EqualTo(12),
                "场景数变了 —— 须同批改 golden_v2_reference.py 并重生成金标准");
        }

        // ══════════════════════════════════════════════════════════════════
        // 判据 ③:防自指 —— 本夹具**只读**金标准
        // ══════════════════════════════════════════════════════════════════

        /// <summary>去掉 `//` 行注释与 `/* … */` 块注释后的源码(只留代码面)。
        /// <para>⚠️ 与 `Boot/telemetry_debug_view_test.cs` 里的同名方法**同一思路** ——
        /// 判据不得被注释里**引述**的字面量满足或打红。</para>
        /// <para>⚠️ 本实现刻意**简单**(逐行剔 `//`、跨行剔 `/* */`),不做字符串字面量感知:
        /// 本夹具的判据词不会出现在字符串里,过度工程反而引入新的误判面。</para></summary>
        private static string CodeOnly(string source)
        {
            var sb = new System.Text.StringBuilder(source.Length);
            bool inBlock = false;
            foreach (string line in source.Split('\n'))
            {
                string cur = line;
                if (inBlock)
                {
                    int end = cur.IndexOf("*/", StringComparison.Ordinal);
                    if (end < 0) continue;
                    cur = cur.Substring(end + 2);
                    inBlock = false;
                }
                int blockStart = cur.IndexOf("/*", StringComparison.Ordinal);
                int lineStart = cur.IndexOf("//", StringComparison.Ordinal);
                if (blockStart >= 0 && (lineStart < 0 || blockStart < lineStart))
                {
                    inBlock = true;
                    cur = cur.Substring(0, blockStart);
                }
                else if (lineStart >= 0)
                {
                    cur = cur.Substring(0, lineStart);
                }
                sb.Append(cur).Append('\n');
            }
            return sb.ToString();
        }

        /// <summary>
        /// 本夹具的源码里**不得**出现对金标准的写动作(AC-21a-28)。
        /// <para>⚠️ 这条是**源码级**判据,不是行为判据:它抓的是「有人给夹具加了
        /// `File.WriteAllText(goldenPath, …)` 之类的『顺手更新金标准』」——
        /// 那会让金标准从「独立参考实现的产物」退化为「C# 实现的快照」,
        /// **零共享代码**的前提就此消失,而所有对拍夹具仍会全绿。</para>
        /// </summary>
        [Test]
        public void test_fixture_neverWritesGolden()
        {
            // 定位本文件:从 TestDirectory 往上找含 `Assets/Tests/EditMode/DiseaseSimulation/` 的祖先
            // (与 GoldenDir() 同一套定位法;2026-10-30 首轮曾拼错相对路径,红在「找不到源码」)。
            DirectoryInfo d = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            string self = null;
            while (d != null)
            {
                string candidate = Path.Combine(d.FullName, "Assets", "Tests", "EditMode",
                                                "DiseaseSimulation", "seq_issuance_golden_test.cs");
                if (File.Exists(candidate)) { self = candidate; break; }
                d = d.Parent;
            }

            // 找不到源码 ⇒ 判据不成立,须**红**(而不是静默跳过 —— 那是假绿)。
            Assert.That(self, Is.Not.Null,
                "找不到本夹具源码 —— 防自指判据无从跑");

            string code = File.ReadAllText(self);

            // ⚠️ **两级剔除,缺一即误报**(2026-10-30 首轮实测:第一级漏了,判据自己红):
            //   ① 剔注释 —— 本文件的 doc 注释里**引述**了 `File.WriteAllText(...)`
            //      作为「要防的那种写法」的例子;不剔注释就会把这段引述当成真写动作。
            //   ② 剔本方法及其后文 —— 下方 `forbidden` 字面量数组本身含这些词。
            int cut = code.IndexOf("public void test_fixture_neverWritesGolden",
                                   StringComparison.Ordinal);
            Assert.That(cut, Is.GreaterThan(0), "本方法定位失败 —— 判据须先能找到自己");
            string scanned = CodeOnly(code.Substring(0, cut));

            foreach (string forbidden in new[]
                     {
                         "WriteAllText", "WriteAllBytes", "AppendAllText",
                         "StreamWriter", "File.Create", "FileStream",
                     })
            {
                Assert.That(scanned.Contains(forbidden), Is.False,
                    $"本夹具源码(去注释后、判据方法之前的部分)含写动作 `{forbidden}` —— " +
                    "AC-21a-28 要求 C# 侧**只读**金标准;写它会把金标准变成 C# 的快照,零共享代码前提消失");
            }
        }
    }
}
