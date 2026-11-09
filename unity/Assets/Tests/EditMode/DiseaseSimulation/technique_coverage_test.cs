// 技法覆盖清单 —— 每一行病种 / 伤情**要么**在处置轴里声明了技法,**要么**在一份具名豁免表里。
//
// 权威:`design/gdd/reviews/technique-unlock-map-2026-11-09.md` §7 T7(补数据）
//      · T4 的 `PatientDrawEligibility`（三态:Admissible / HookCase / Undeclared）
//      · `emergency-procedures.md:847`（`DC-4` 的枚举内容随 `OQ-10-4` 冻结为 P0 = 2 动作）
//
// ─────────────────────────────────────────────────────────────────────────────
// 读哪些仓库文件(测试标准 §只读外部夹具的具名声明义务)
// ─────────────────────────────────────────────────────────────────────────────
// 读 `assets/data/disease_registry.json` + `assets/data/disease_action_axis.json`,
//   **经生产 binder 读**,不内联最小读取器。为什么必须读真表:本夹具判的正是
//   「**这两份真表之间**的覆盖关系」—— 内联一份假表只能证明本夹具自己写的谓词,
//   那正是假绿形态 F1(自造断言顶替 AC 语义层)。不写盘。
//
// ─────────────────────────────────────────────────────────────────────────────
// 为什么要有这一件（缺口不是「少写了一条断言」）
// ─────────────────────────────────────────────────────────────────────────────
// T4 引入三态判定的**代价**:轴里没声明的行落 `Undeclared`(放行 + 计数)。
// 这个状态本身是对的（严格两态会让世界没病人),但它**也是静默的** ——
// 2026-11-09 T4 落地时,全库 9 行落 `Undeclared`,而没有任何门会响。
// 本夹具把「未覆盖」从**统计事实**变成**具名清单**:
//   · 每行必须有轴行,或出现在 `UndeclaredByDesign` 里(带理由);
//   · 豁免表**不许陈旧**(列出已声明 / 不存在的行 ⇒ 红);
//   · 豁免表**不许长大**而不改本文件(集合相等,不是包含);
//   · 每条豁免**必须带非空理由**(空白理由 = 悄悄把门堵上)。
//
// ─────────────────────────────────────────────────────────────────────────────
// T7 起草期实测:三条「想补」的技法被裁决挡住 → 当日即由用户改判（原文不删,留作闭环）
// ─────────────────────────────────────────────────────────────────────────────
// T7 首次落盘(给四行挂 `清创缝合` / `蛇伤处置`)**当场红了三条夹具**:
//   ① `action_tables_bake_test.test_dc4_enumMatchesAxisOwner10Segment` —— 轴 `owner=="10"`
//      段与 `EmergencyAction` 枚举**双向钉死**;
//   ② `ActionAxisReaderGateTest.test_axis_artifactOwner10Set_matchesEmergencyActionEnum` ——
//      同一件事的产物侧;
//   ③ `test_dc4_enumContent_p0TwoActions` —— 枚举成员数**被 `OQ-10-4` 冻结为 2**。
// 而两门技法属第三章 / 第四章 = P1a(`campaign-arc.md:145`;`adventure-catalog.md:50`)。
// ⇒ 当时整体撤回,并把问题呈报为**裁决**(「P0 要不要开第三、第四门急救动作」)。
// 🔁 **同日用户裁定「开,按你说的改 OQ-10-4」** ⇒ P0 急救动作 2 → **4**,枚举补上
// `Debridement = 2` / `SnakebiteCare = 3`,四行技法**重新落盘**。
// ⚠️ **只开 id 空间,不交执行体** —— 这两个动作的 `EmergencyActionRow` 与 4 的交互接线都不存在。
// 本文件因此判:四件伤情 → `0 止血包扎`;咬伤 / 钝击 → `2 清创缝合`;破伤风 → `2`;蛇毒 → `3`;
// 只剩两行**具名豁免**(神经衰弱 = 豁免类病种 · 药毒 = 自限型无处置)。

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using DaYiJingCheng.EditorTools.Bake;
using DaYiJingCheng.Sim;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.DiseaseSimulation
{
    /// <summary>病种 / 伤情 ↔ 处置轴的覆盖清单(未覆盖必须是**具名**的,不是统计出来的)。</summary>
    [TestFixture]
    public sealed class TechniqueCoverageTest
    {
        private static string RepoRoot([CallerFilePath] string thisFile = "")
            => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile) ?? ".",
                "..", "..", "..", "..", ".."));

        /// <summary>
        /// **设计内无技法**的行 —— 逐条带理由。**这张表只许按裁定修改,不许为了转绿加行。**
        /// <para>⚠️ 改动本表 = 改判据面。所以要写成「集合相等」,让任何增减都在
        /// <see cref="test_allowlist_isExactlyTheRuledSet"/> 上现形。</para>
        /// </summary>
        private static readonly Dictionary<string, string> UndeclaredByDesign = new()
        {
            ["DIS_NEURASTHENIA"] =
                "豁免类病种(R3.6):GDD :447 的 handle = symptomatic_only,P0 无任何处置对它有明确作用。" +
                "挂一门技法 = 凭空发明一味药。",
            ["INJ_DRUG_HARM"] =
                "自限型药毒(GDD :659):自行消退,零处置。给它挂技法等于凭空发明一味药;`[]` 落 Undeclared 是正确状态。",
        };

        // ── 载入(走生产 binder,零 Fake)────────────────────────────────────

        private static IReadOnlyList<DiseaseRegistryEntry> RegistryRows()
            => DiseaseRegistryBaker.BakeFromRepo(RepoRoot()).Rows;

        private static IReadOnlyList<TreatableByRow> AxisRows()
            => DiseaseActionAxisBaker.BakeFromRepo(RepoRoot()).TreatableBy;

        private static Dictionary<string, List<int>> DeclaredByKey()
        {
            var map = new Dictionary<string, List<int>>(System.StringComparer.Ordinal);
            foreach (TreatableByRow row in AxisRows())
            {
                if (row.DiseaseKey == null) continue;
                if (!map.TryGetValue(row.DiseaseKey, out List<int> list))
                    map[row.DiseaseKey] = list = new List<int>();
                list.Add(row.ActionId);
            }
            return map;
        }

        // ══════════════════════════════════════════════════════════════════════
        // ① 每行都有归宿
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// 注册表每一行**要么**有轴行,**要么**在豁免表里。两条都不满足 = 静默未覆盖 ⇒ 红。
        /// <para><b>判别力</b>:造一行既无轴行又不在豁免表 ⇒ 本条失败。</para>
        /// </summary>
        [Test]
        public void test_everyRegistryRow_isEitherAxisDeclaredOrAllowlisted()
        {
            IReadOnlyList<DiseaseRegistryEntry> rows = RegistryRows();
            Assert.That(rows.Count, Is.GreaterThan(0), "前置:注册表须非空(空集上本条恒绿)");

            Dictionary<string, List<int>> declared = DeclaredByKey();
            var uncovered = new List<string>();
            foreach (DiseaseRegistryEntry r in rows)
            {
                if (declared.ContainsKey(r.DiseaseKey)) continue;
                if (UndeclaredByDesign.ContainsKey(r.DiseaseKey)) continue;
                uncovered.Add($"{r.DiseaseKey}(id {r.DiseaseId}, kind {r.Kind})");
            }

            Assert.That(uncovered, Is.Empty,
                "下列行既没在 `disease_action_axis.json` 里声明技法,也不在豁免表里 ——\n" +
                "它们在 T4 的三态里落 `Undeclared`(放行 + 计数),即**不会被任何技法门管住**。\n" +
                "补轴行,或把它加进 `UndeclaredByDesign` 并写清理由:\n  " +
                string.Join("\n  ", uncovered));
        }

        // ══════════════════════════════════════════════════════════════════════
        // ② 豁免表不许陈旧、不许悄悄长大、不许空白理由
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>豁免表里每一条都必须**仍然**是「存在且无轴行」,否则它是过期的挡箭牌。</summary>
        [Test]
        public void test_allowlist_hasNoStaleEntries()
        {
            IReadOnlyList<DiseaseRegistryEntry> rows = RegistryRows();
            Dictionary<string, List<int>> declared = DeclaredByKey();
            var known = new HashSet<string>(rows.Select(r => r.DiseaseKey), System.StringComparer.Ordinal);

            var stale = new List<string>();
            foreach (string key in UndeclaredByDesign.Keys)
            {
                if (!known.Contains(key))
                {
                    stale.Add($"{key} —— 注册表里已经没有这一行了");
                }
                else if (declared.ContainsKey(key))
                {
                    stale.Add($"{key} —— 它现在**有**轴行(actions={string.Join(",", declared[key])}),豁免表却还留着");
                }
            }

            Assert.That(stale, Is.Empty,
                "豁免表有过期条目 —— 它会在下一行真漏覆盖时替人挡红:\n  " + string.Join("\n  ", stale));
        }

        /// <summary>
        /// 豁免表**恰好**是被裁定过的那一组。增删一律要改本文件(并在证据件里说明)。
        /// <para>⚠️ 为什么用集合相等而不是「包含」:包含式断言下,有人加一条豁免
        /// 就永久免检一行,而没有任何门会响 —— 那是 F3(自指判据)的同族形态。</para>
        /// </summary>
        [Test]
        public void test_allowlist_isExactlyTheRuledSet()
        {
            string[] expected = { "DIS_NEURASTHENIA", "INJ_DRUG_HARM" };
            string[] actual = UndeclaredByDesign.Keys
                .OrderBy(k => k, System.StringComparer.Ordinal).ToArray();

            Assert.That(actual, Is.EqualTo(expected.OrderBy(k => k, System.StringComparer.Ordinal).ToArray()),
                "豁免表与被裁定的那一组不符 —— 改它必须是有意为之(见本文件头:这张表只许按裁定修改)");
        }

        /// <summary>
        /// 每条豁免**必须带非空理由**。
        /// <para><b>判别力</b>:把某条理由改成空串 ⇒ 红。防「悄悄把门堵上」——
        /// 豁免表的价值全在理由里,没有理由的豁免与「直接放行」没有区别。</para>
        /// </summary>
        [Test]
        public void test_allowlist_entries_carryNonEmptyReasons()
        {
            var blank = UndeclaredByDesign
                .Where(kv => string.IsNullOrWhiteSpace(kv.Value))
                .Select(kv => kv.Key)
                .ToArray();

            Assert.That(blank, Is.Empty,
                "下列豁免没有理由 —— 空白理由等于「不解释就放行」:" + string.Join(", ", blank));
        }

        // ══════════════════════════════════════════════════════════════════════
        // ③ 轴不许指向不存在的行
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>轴的每个病种键都必须在注册表里 —— 悬空外键会让技法门对一个不存在的病种放行。</summary>
        [Test]
        public void test_axisKeys_allExistInRegistry()
        {
            var known = new HashSet<string>(RegistryRows().Select(r => r.DiseaseKey), System.StringComparer.Ordinal);
            Assert.That(known.Count, Is.GreaterThan(0), "前置:注册表须非空");

            string[] orphan = DeclaredByKey().Keys
                .Where(k => !known.Contains(k))
                .OrderBy(k => k, System.StringComparer.Ordinal)
                .ToArray();

            Assert.That(orphan, Is.Empty,
                "处置轴里有注册表查无此人的病种键(悬空外键):" + string.Join(", ", orphan));
        }

        // ══════════════════════════════════════════════════════════════════════
        // ④ T7 的具体落点(把「补了」与「补对了」分开)
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// 六件伤情各自指向**那一门**技法 —— 不是「有任意技法」。
        /// <para>反证:把 `INJ_BITE` 的 action 由 2 改成 0 ⇒ 本条红
        /// (咬伤不该由止血包扎管,那是另一门技法)。</para>
        /// </summary>
        [Test]
        public void test_injuryRows_pointAtTheirOwnTechnique()
        {
            Dictionary<string, List<int>> declared = DeclaredByKey();

            // 0 = 止血包扎(序章,在起步集 {0,1} 内)· 2 = 清创缝合(第四章 #8)
            var expected = new Dictionary<string, int>(System.StringComparer.Ordinal)
            {
                ["INJ_STAB"] = 0,
                ["INJ_LACERATION"] = 0,
                ["INJ_HEMORRHAGE"] = 0,
                ["INJ_CLAW"] = 0,
                ["INJ_BITE"] = 2,
                ["INJ_BLUNT"] = 2,
            };

            foreach (KeyValuePair<string, int> kv in expected)
            {
                Assert.That(declared.ContainsKey(kv.Key), Is.True,
                    $"{kv.Key} 未在轴里声明技法 —— 伤情会落 Undeclared,技法门对它失效");
                Assert.That(declared[kv.Key], Is.EqualTo(new List<int> { kv.Value }),
                    $"{kv.Key} 的技法不对 —— 期望恰好 {{{kv.Value}}},实得 " +
                    $"{{{string.Join(",", declared[kv.Key])}}}");
            }
        }

        /// <summary>
        /// 破伤风与蛇毒各挂一门技法,而破伤风的 `handle` **仍是 `none`**。
        /// <para>为什么单独立一条:给 `DIS_TETANUS` 挂 `causal` 会静默把它的 handle 改判成
        /// `causal`(派生规则只看「轴里有没有 causal 行」),而 GDD `:294` 把 `none` 钉死为
        /// 「仅 DIS_TETANUS」、`AC-43` 逐病种断言。「补了技法」与「没改裁决」是两件事。</para>
        /// </summary>
        [Test]
        public void test_tetanusAndVenom_carryTechniqueWithoutBreakingHandleSemantics()
        {
            Dictionary<string, List<int>> declared = DeclaredByKey();

            Assert.That(declared.ContainsKey("DIS_TETANUS"), Is.True, "破伤风仍无技法 —— 技法门管不住它");
            Assert.That(declared["DIS_TETANUS"], Is.EqualTo(new List<int> { 2 }),
                "破伤风须恰好挂 `2 清创缝合`(GDD :445「10 清创 · 预防」)");

            Assert.That(declared.ContainsKey("TOX_ENVENOMATION"), Is.True, "蛇毒仍无技法");
            Assert.That(declared["TOX_ENVENOMATION"], Is.EqualTo(new List<int> { 3 }),
                "蛇毒须恰好挂 `3 蛇伤处置`(map §3.1「制动 + 加压包扎」)");

            IReadOnlyList<DiseaseRegistryEntry> rows = RegistryRows();
            Assert.That(rows.First(r => r.DiseaseKey == "DIS_TETANUS").Handle,
                Is.EqualTo(DiseaseHandle.None),
                "破伤风的 handle 必须是 `none`(发病后无解)—— 挂了 causal 行会静默变成 `causal`");
            Assert.That(rows.First(r => r.DiseaseKey == "TOX_ENVENOMATION").Kind,
                Is.EqualTo(DiseaseKind.Poison),
                "蛇毒是 `kind: poison`(GDD :374),不是 injury —— 它归病侧视图,不是伤侧");
        }

        /// <summary>
        /// 轴 `owner=="10"` 段**恰为 `{0, 1, 2, 3}`** —— `OQ-10-4` 二次裁定后的闭集。
        /// <para>与 `action_tables_bake_test` 的 DC-4 是同一裁决的两面,**红法不同**:
        /// 枚举没改、轴改了 ⇒ 那两条红;轴没改、枚举改了 ⇒ 这一条仍绿。
        /// 少任一面,「谁先动的手」就查不出来。</para>
        /// </summary>
        [Test]
        public void test_axisOwner10Segment_matchesTheRuledFourActions()
        {
            DiseaseActionAxisBaker.BakeOutput axis = DiseaseActionAxisBaker.BakeFromRepo(RepoRoot());
            int[] ids = axis.Actions
                .Where(a => a.Owner == "10")
                .Select(a => a.ActionId)
                .OrderBy(x => x)
                .ToArray();

            Assert.That(ids, Is.EqualTo(new[] { 0, 1, 2, 3 }),
                "轴 `owner==\"10\"` 段变了 —— 它在 2026-11-09 二次裁定下为 {0,1,2,3}" +
                "(emergency-procedures.md §一 Amendment)。再增员须同时改 OQ-10-4 与 EmergencyAction 枚举");
        }
    }
}
