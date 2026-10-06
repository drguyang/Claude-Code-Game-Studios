// 权威来源:GDD 规则四/五/八 · 11-DC DC-1…DC-7 · AC-11-02/07/20
//          · ADR-014(两阶段烘焙)· ADR-024(Kind 已登记)
//
// 测试处方表与本草词表的构建期校验(DC-1…DC-7)。
//
// NOT-RUN 声明(禁借绿):
// - DC-2:action_id 闭集 = 处置注册表全值 —— **真源仍缺席**(9 侧 disease_registry.json 不存在、
//   全库无 ACT_* 符号;GDD `:748` 登记「该枚举的 master 住哪一份文件未登记」)。
//   ⚠️ 2026-10-06:OQ-11-2 只裁了**归属**(映射住 11),**未产出 master** ⇒ 判据本体仍 NOT-RUN。
//   现补 = **校验机制 + 负夹具**(影子闭集驱动,证可跑可红);`ShadowRegistryUsed` 显式报出。
// - DC-6:依赖 9 侧 NOISE_BAND_9 常量(BL-2,O-11→9,**仍 ⏳ 待认领**)。
//   现补 = **构建期机制化**(接进烘焙门,求值经 DoseCalculator)+ 影子地板驱动。
// - AC-11-07 双表 polarity 交叉硬门:9 的 disease_registry.json 不存在(9 侧未建)

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using DaYiJingCheng.EditorTools.Bake;
using DaYiJingCheng.Sim.Contracts;   // DoseRange(DC-6 的域入参)
using NUnit.Framework;

namespace DaYiJingCheng.Tests.PrescriptionMedication
{
    [TestFixture]
    public class PrescriptionTablesTest
    {
        private static string RepoRoot => ComputeRepoRoot();

        private static string ComputeRepoRoot([CallerFilePath] string thisFile = "")
            => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile) ?? ".",
                "..", "..", "..", "..", ".."));

        private static string DataDir => Path.Combine(RepoRoot, "assets", "data");

        // ── 辅助 ──────────────────────────────────────────────────────────

        private static string ReadActionsJson() =>
            File.ReadAllText(Path.Combine(DataDir, "prescription_actions.json"));

        private static string ReadLexiconJson() =>
            File.ReadAllText(Path.Combine(DataDir, "materia_lexicon.json"));

        private static string ReadItemsJson() =>
            File.ReadAllText(Path.Combine(DataDir, "item_database_items.json"));

        // ── DC-1:处方表 item_key ⊂ ItemDef 闭集 ───────────────────────────

        [Test]
        public void test_dc1_itemKeySubsetOfItemDef()
        {
            // 驱动生产绑定器:DC-1 由 PrescriptionActionsBinder.Bind 校验
            // 正向路径:当前数据集应通过(无 DC-1 错误)
            string actionsJson = ReadActionsJson();
            string lexiconJson = ReadLexiconJson();
            string itemsJson = ReadItemsJson();

            var ex = RecordException(() => PrescriptionActionsBinderProbe.Bind(actionsJson, lexiconJson, itemsJson));
            if (ex != null)
            {
                Assert.Fail($"DC-1 正向路径应通过,实际抛出:{string.Join("; ", ex.Errors)}");
            }
        }

        // ── DC-3:polarity ∈ {causal, symptomatic} ──────────────────────────

        [Test]
        public void test_dc3_polarityBinaryClosed()
        {
            // 驱动生产绑定器:DC-3 由 PrescriptionActionsBinder.Bind 校验
            string actionsJson = ReadActionsJson();
            string lexiconJson = ReadLexiconJson();
            string itemsJson = ReadItemsJson();

            var ex = RecordException(() => PrescriptionActionsBinderProbe.Bind(actionsJson, lexiconJson, itemsJson));
            if (ex != null)
            {
                Assert.Fail($"DC-3 正向路径应通过,实际抛出:{string.Join("; ", ex.Errors)}");
            }
        }

        // ── DC-5:词表每药恰一条 ───────────────────────────────────────────

        [Test]
        public void test_dc5_lexiconExactlyOnePerDrug()
        {
            // 驱动生产绑定器:DC-5 由 PrescriptionActionsBinder.Bind 校验
            string actionsJson = ReadActionsJson();
            string lexiconJson = ReadLexiconJson();
            string itemsJson = ReadItemsJson();

            var ex = RecordException(() => PrescriptionActionsBinderProbe.Bind(actionsJson, lexiconJson, itemsJson));
            if (ex != null)
            {
                Assert.Fail($"DC-5 正向路径应通过,实际抛出:{string.Join("; ", ex.Errors)}");
            }
        }

        // ── DC-7:0 < DOSE_BASE ≤ 65536 × hi ───────────────────────────────

        [Test]
        public void test_dc7_doseBasePositive()
        {
            // 驱动生产绑定器:DC-7 由 PrescriptionActionsBinder.Bind 校验
            string actionsJson = ReadActionsJson();
            string lexiconJson = ReadLexiconJson();
            string itemsJson = ReadItemsJson();

            var ex = RecordException(() => PrescriptionActionsBinderProbe.Bind(actionsJson, lexiconJson, itemsJson));
            if (ex != null)
            {
                Assert.Fail($"DC-7 正向路径应通过,实际抛出:{string.Join("; ", ex.Errors)}");
            }
        }

        [Test]
        public void test_dc7_doseBaseWithinUpperBound()
        {
            string actionsJson = ReadActionsJson();
            int doseBase = ExtractDoseBase(actionsJson);

            // 内联构造带非 null dose_range 的夹具(不依赖当前数据集)
            // 当前数据集 salicylic_acid 的 dose_range = null ⇒ 上界断言零执行(空集真空真)
            // 故此处内联两个夹具:一个 hi=1(上界 65536),一个 hi=2(上界 131072)
            var fixtures = new[]
            {
                (itemKey: "test_drug_a", hi: 1),
                (itemKey: "test_drug_b", hi: 2),
            };

            foreach (var (itemKey, hi) in fixtures)
            {
                long bound = 65536L * hi;
                Assert.LessOrEqual((long)doseBase, bound,
                    $"DC-7 违反:药 '{itemKey}' DOSE_BASE = {doseBase} > 65536 × {hi} = {bound}");
            }
        }

        // ── AC-11-02:零重定义 ──────────────────────────────────────────────

        [Test]
        public void test_ac1102_zeroRedefinition()
        {
            // AC-11-02 由 RejectUnknownKeys 隐式满足 —— 白名单不含 drug_potency/half_life/axis_offset 等
            // 正向路径:当前数据集应通过(无未知键错误)
            string actionsJson = ReadActionsJson();
            string lexiconJson = ReadLexiconJson();
            string itemsJson = ReadItemsJson();

            var ex = RecordException(() => PrescriptionActionsBinderProbe.Bind(actionsJson, lexiconJson, itemsJson));
            if (ex != null)
            {
                Assert.Fail($"AC-11-02 正向路径应通过,实际抛出:{string.Join("; ", ex.Errors)}");
            }
        }

        // ── AC-11-20:本草词表存在性 ────────────────────────────────────────

        [Test]
        public void test_ac1120_lexiconCoversAllDrugs()
        {
            // 驱动生产绑定器:AC-11-20 由 PrescriptionActionsBinder.Bind 校验
            string actionsJson = ReadActionsJson();
            string lexiconJson = ReadLexiconJson();
            string itemsJson = ReadItemsJson();

            var ex = RecordException(() => PrescriptionActionsBinderProbe.Bind(actionsJson, lexiconJson, itemsJson));
            if (ex != null)
            {
                Assert.Fail($"AC-11-20 正向路径应通过,实际抛出:{string.Join("; ", ex.Errors)}");
            }
        }

        // ── 烘焙确定性:同源集双跑字节相等 ─────────────────────────────────

        [Test]
        public void test_bakeDeterminism_sameInputSameOutput()
        {
            // 同源集双跑字节相等(烘焙确定性)
            var result1 = PrescriptionActionsBaker.BakeFromRepo(RepoRoot);
            var result2 = PrescriptionActionsBaker.BakeFromRepo(RepoRoot);

            Assert.AreEqual(result1.Cooked, result2.Cooked, "烘焙确定性:同源两次烘焙字节不等");
            Assert.AreEqual(result1.ConfigVersion, result2.ConfigVersion, "烘焙确定性:同源两次 ConfigVersion 不等");
        }

        // ── AC-11-09:single_dose_max 烘焙期派生(零手填 + 联动)────────────────

        [Test]
        public void test_singleDoseMax_derivedNotHandFilled()
        {
            // ⚠️ 补做评审 B1:AC-11-09 原稿**全库零实现零测试**。本组补上。
            // 判据 ①:派生值 = max over(全部药 × dose_range.hi) of |dose_potency| ——
            // 由生产派生器给出,且**与手算一致**(证明它真的是从数据算出来的,不是常量)。
            string actionsJson = ReadActionsJson();
            string lexiconJson = ReadLexiconJson();
            string itemsJson = ReadItemsJson();

            long derived = PrescriptionActionsBinderProbe.SingleDoseMaxRawOf(actionsJson, lexiconJson, itemsJson);

            // 独立复算(测试侧重算 = 对拍,不是重实现 —— 用的是同一 F-11.1 生产件):
            // 当前数据集只有 salicylic_acid 带 drug_profile(dose_range = null ⇒ 整剂路径)
            // ⇒ single_dose_max = |drug_potency| = |1/2| = 32768 raw。
            Assert.AreEqual(32768L, derived,
                "single_dose_max 与独立复算不符 ⇒ 派生器读错了源或公式不是 max|dose_potency|");
        }

        [Test]
        public void test_singleDoseMax_tracksDoseRangeHiChange()
        {
            // 判据 ②(AC-11-09「改任一源字段 ⇒ 派生值自动重算」):
            // 把同一味药的 dose_range 从 null 改成 [1, 4] ⇒ 派生值须随之走 F-11.1 除式。
            // 夹具 DOSE_BASE = 65536(= 1.0 Q16.16),potency = 1/2(raw 32768):
            //   hi 档 dose_potency = ROUND_HALF_AWAY(32768 × 4 / 65536) = ROUND(2.0) = 2 raw。
            // 空 dose_range ⇒ 整剂旁路 ⇒ dose_potency = drug_potency = 32768 raw。
            // 两者**相差三个数量级** ⇒ 本测对「派生器真读了 dose_range」有强判别力。
            string actionsJson = ReadActionsJson();
            string lexiconJson = ReadLexiconJson();

            string itemsWithRange = @"{
              ""schema_version"": 1,
              ""items"": [
                { ""base_id"": ""salicylic_acid"", ""category"": ""drug"",
                  ""drug_profile"": { ""drug_potency"": ""1/2"", ""dose_range"": [1, 4] } }
              ]
            }";
            string itemsNoRange = @"{
              ""schema_version"": 1,
              ""items"": [
                { ""base_id"": ""salicylic_acid"", ""category"": ""drug"",
                  ""drug_profile"": { ""drug_potency"": ""1/2"", ""dose_range"": null } }
              ]
            }";

            long withRange = PrescriptionActionsBinderProbe.SingleDoseMaxRawOf(actionsJson, lexiconJson, itemsWithRange);
            long noRange = PrescriptionActionsBinderProbe.SingleDoseMaxRawOf(actionsJson, lexiconJson, itemsNoRange);

            Assert.AreEqual(2L, withRange, "dose_range = [1,4] 时派生值未按 F-11.1 随 hi 重算");
            Assert.AreEqual(32768L, noRange, "空 dose_range 应走整剂路径(dose_potency = drug_potency)");
            Assert.AreNotEqual(withRange, noRange, "改 dose_range 后派生值不变 ⇒ 派生器读的是常量,不是数据");
        }

        [Test]
        public void test_singleDoseMax_configVersionTracksSourceChange()
        {
            // 判据 ③(AC-11-09「哈希进 ConfigVersion」):
            // 源文本变 ⇒ ConfigVersion 变。⚠️ 哈希命名键与生产 BakeFromRepo 逐字同源
            // (仅 actions + lexicon 两键 —— 生产侧是否纳入 items 归 story-001 的域,
            //  本测只证**已纳入的那部分**确实随内容变,不谎称覆盖 items)。
            string actionsJson = ReadActionsJson();
            string lexiconJson = ReadLexiconJson();

            uint baseline = PrescriptionActionsBinderProbe.ConfigVersionOf(actionsJson, lexiconJson);

            string changedActions = actionsJson.Replace("\"MAX_DOSE_DETENTS\": 5", "\"MAX_DOSE_DETENTS\": 6");
            Assert.AreNotEqual(actionsJson, changedActions, "夹具替换未生效 ⇒ 本测真空");
            uint afterChange = PrescriptionActionsBinderProbe.ConfigVersionOf(changedActions, lexiconJson);

            Assert.AreNotEqual(baseline, afterChange,
                "源内容变了而 ConfigVersion 未变 ⇒ 内容哈希派生失效(AC-11-09 的联动半边)");
        }

        [Test]
        public void test_singleDoseMax_noPotencyDrug_throws()
        {
            // 判据 ④(零手填的负向面):药行在而 drug_potency 缺 ⇒ 硬失败,不静默填 0。
            // (静默 0 会让 9 的 F1 clamp 上界退化为 0 ⇒ 全药效被截断,是**静默**数据错误。)
            string actionsJson = ReadActionsJson();
            string lexiconJson = ReadLexiconJson();
            string badItems = @"{
              ""schema_version"": 1,
              ""items"": [
                { ""base_id"": ""salicylic_acid"", ""category"": ""drug"", ""drug_profile"": {} }
              ]
            }";

            var ex = RecordException(() =>
                PrescriptionActionsBinderProbe.SingleDoseMaxRawOf(actionsJson, lexiconJson, badItems));

            Assert.IsNotNull(ex, "药行缺 drug_potency 应硬失败,实际静默通过(派生值会退化为 0)");
            bool mentionsPotency = false;
            foreach (string e in ex.Errors)
                if (e.Contains("drug_potency")) { mentionsPotency = true; break; }
            Assert.IsTrue(mentionsPotency,
                $"错误消息未点出 drug_potency: {string.Join("; ", ex.Errors)}");
        }

        // ── 负夹具:DC-1 外键违反 ───────────────────────────────────────────

        [Test]
        public void test_dc1_negative_unknownItemKey()
        {
            // 负夹具:item_key 不在 ItemDef 闭集中 ⇒ 应被拒
            // 构造违反条件的数据:item_key = "__unknown_drug__"
            string badActionsJson = @"{
                ""schema_version"": 1,
                ""dose_const"": { ""DOSE_BASE"": 65536, ""MAX_DOSE_DETENTS"": 5 },
                ""actions"": [
                    { ""item_key"": ""__unknown_drug__"", ""action_id"": 1, ""polarity"": ""symptomatic"" }
                ]
            }";
            string goodLexiconJson = ReadLexiconJson();

            var ex = Assert.Throws<BakeValidationException>(() =>
                PrescriptionActionsBinderProbe.Bind(badActionsJson, goodLexiconJson, ReadItemsJson()));
            Assert.IsTrue(ContainsError(ex, "DC-1") || ContainsError(ex, "item_key"),
                $"负夹具:DC-1 违反应被拒,实际错误:{string.Join("; ", ex.Errors)}");
        }

        // ── 负夹具:DC-3 极性违反 ───────────────────────────────────────────

        [Test]
        public void test_dc3_negative_invalidPolarity()
        {
            // 负夹具:polarity 不在闭集中 ⇒ 应被拒
            string badActionsJson = @"{
                ""schema_version"": 1,
                ""dose_const"": { ""DOSE_BASE"": 65536, ""MAX_DOSE_DETENTS"": 5 },
                ""actions"": [
                    { ""item_key"": ""salicylic_acid"", ""action_id"": 1, ""polarity"": ""invalid_polarity"" }
                ]
            }";
            string goodLexiconJson = ReadLexiconJson();

            var ex = Assert.Throws<BakeValidationException>(() =>
                PrescriptionActionsBinderProbe.Bind(badActionsJson, goodLexiconJson, ReadItemsJson()));
            Assert.IsTrue(ContainsError(ex, "DC-3") || ContainsError(ex, "polarity"),
                $"负夹具:DC-3 违反应被拒,实际错误:{string.Join("; ", ex.Errors)}");
        }

        // ── 负夹具:DC-5 词表重复 ───────────────────────────────────────────

        [Test]
        public void test_dc5_negative_duplicateLexiconEntry()
        {
            // 负夹具:同一药在词表中出现多次 ⇒ 应被拒
            string goodActionsJson = ReadActionsJson();
            string badLexiconJson = @"{
                ""schema_version"": 1,
                ""lexicon"": [
                    { ""item_key"": ""salicylic_acid"", ""功效词"": ""苦寒清热"", ""体征轴"": [""heat""] },
                    { ""item_key"": ""salicylic_acid"", ""功效词"": ""泻火解毒"", ""体征轴"": [""inflammation""] }
                ]
            }";

            var ex = Assert.Throws<BakeValidationException>(() =>
                PrescriptionActionsBinderProbe.Bind(goodActionsJson, badLexiconJson, ReadItemsJson()));
            Assert.IsTrue(ContainsError(ex, "DC-5") || ContainsError(ex, "多次"),
                $"负夹具:DC-5 违反应被拒,实际错误:{string.Join("; ", ex.Errors)}");
        }

        // ── 负夹具:DC-7 DOSE_BASE 边界 ─────────────────────────────────────

        [Test]
        public void test_dc7_negative_doseBaseZero()
        {
            // 负夹具:DOSE_BASE = 0 ⇒ 应被拒
            string badActionsJson = @"{
                ""schema_version"": 1,
                ""dose_const"": { ""DOSE_BASE"": 0, ""MAX_DOSE_DETENTS"": 5 },
                ""actions"": [
                    { ""item_key"": ""salicylic_acid"", ""action_id"": 1, ""polarity"": ""symptomatic"" }
                ]
            }";
            string goodLexiconJson = ReadLexiconJson();

            var ex = Assert.Throws<BakeValidationException>(() =>
                PrescriptionActionsBinderProbe.Bind(badActionsJson, goodLexiconJson, ReadItemsJson()));
            Assert.IsTrue(ContainsError(ex, "DC-7") || ContainsError(ex, "DOSE_BASE"),
                $"负夹具:DC-7 违反应被拒,实际错误:{string.Join("; ", ex.Errors)}");
        }

        // ══════════════════════════════════════════════════════════════════
        // DC-2:action_id 闭集(2026-10-06 补 —— 此前 action_id 读入后从不校验)
        // ⚠️ 判据本体 NOT-RUN:真源 = 9 的处置 id master,GDD `:748` 登记「未登记」。
        //    本组证「机制可跑 + 负夹具可红 + 空集不冒充绿」,**不证表已合规**。
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_dc2_shadowRegistryMechanism_acceptsClosedSetMember()
        {
            // 机制阳性:闭集内成员 ⇒ 零错
            var errs = PrescriptionActionIdRegistry.ValidateActionId(1);
            Assert.AreEqual(0, errs.Count,
                $"闭集内 action_id 应通过,实际:{string.Join("; ", errs)}");
        }

        [Test]
        public void test_dc2_negative_actionIdOutsideRegistry()
        {
            // 机制阴性:闭集外 ⇒ 报错且点名 DC-2
            var errs = PrescriptionActionIdRegistry.ValidateActionId(9999);
            Assert.IsTrue(errs.Count > 0, "闭集外 action_id 应被拒(此前**任何 int 都放行**)");
            Assert.IsTrue(errs[0].Contains("DC-2"), $"错误须点名 DC-2,实际:{errs[0]}");
        }

        [Test]
        public void test_dc2_emptyRegistry_doesNotPassVacuously()
        {
            // 空集守卫:闭集为空 ⇒ 报错,不得真空真通过(拒以空集冒充绿)
            var errs = PrescriptionActionIdRegistry.ValidateActionId(1, new HashSet<int>());
            Assert.IsTrue(errs.Count > 0, "空闭集必须报错 —— 拒以空集冒充绿");
            Assert.IsTrue(errs[0].Contains("空集"), $"须点名空集,实际:{errs[0]}");
        }

        [Test]
        public void test_dc2_binder_warnsOnOutOfRegistryActionId()
        {
            // 端到端:烘焙门确实接线(此前 action_id 从不校验 ⇒ 本测在原实现下必红)。
            // ⚠️ 影子期落 **Warnings 不落 errors** —— 用无主闭集硬失败 = 对合法输入类误判。
            string badActionsJson = @"{
                ""schema_version"": 1,
                ""dose_const"": { ""DOSE_BASE"": 65536, ""MAX_DOSE_DETENTS"": 5 },
                ""actions"": [
                    { ""item_key"": ""salicylic_acid"", ""action_id"": 9999, ""polarity"": ""symptomatic"" }
                ]
            }";

            var bound = PrescriptionActionsBinderProbe.Bind(badActionsJson, ReadLexiconJson(), ReadItemsJson());
            Assert.IsTrue(ContainsWarning(bound.ShadowWarnings, "DC-2"),
                $"DC-2 的发现须出现在影子诊断里,实际:{string.Join("; ", bound.ShadowWarnings)}");
        }

        [Test]
        public void test_dc2_shadowWarning_doesNotHardFailLegalFixture()
        {
            // ⚠️ 反向守卫(2026-10-06 实测踩中的坑):合法夹具**不得**因影子判据而烘焙失败。
            //    最初把影子发现并入 errors ⇒ 打断了 test_singleDoseMax_tracksDoseRangeHiChange
            //    (其 dose_potency = 1/2 的相邻档差低于影子地板) —— 这正是 GDD 点名的
            //    「判据对合法输入类误判」。本测钉死:影子期不硬失败。
            string legalActionsJson = ReadActionsJson();
            Assert.DoesNotThrow(() =>
                PrescriptionActionsBinderProbe.Bind(legalActionsJson, ReadLexiconJson(), ReadItemsJson()),
                "影子判据不得让合法夹具烘焙失败(用无主门槛硬失败 = 对合法输入类误判)");
        }

        // ══════════════════════════════════════════════════════════════════
        // DC-6:可感知地板(2026-10-06 补 —— 机制此前只在 Sim,未接进烘焙门)
        // ⚠️ 判据本体 NOT-RUN:门槛 `NOISE_BAND_9` 归 9 未立(BL-2)。现用**影子地板**。
        // ══════════════════════════════════════════════════════════════════

        // ⚠️ 夹具口径:F-11.1 是 `dose_potency = drug_potency × dose / DOSE_BASE`
        //    ⇒ **相邻档差 = drug_potency / DOSE_BASE**(不是 drug_potency 本身)。
        //    故「大药效」= drug_potency raw 远大于 DOSE_BASE。

        [Test]
        public void test_dc6_shadowFloorMechanism_acceptsSufficientGap()
        {
            // 机制阳性:相邻档差 = 65536×1000 / 65536 = 1000 ≥ 影子地板 100 ⇒ 零错
            var range = new DoseRange(1, 5);
            var errs = PrescriptionActionIdRegistry.ValidatePerceptibleFloor(
                drugPotencyRaw: 65536L * 1000L, range: range, doseBase: 65536);
            Assert.AreEqual(0, errs.Count, $"档差充足应通过,实际:{string.Join("; ", errs)}");
        }

        [Test]
        public void test_dc6_negative_gapBelowShadowFloor()
        {
            // 机制阴性:相邻档差 = 1000/65536 = 0 < 影子地板 100 ⇒ 报错且点名 DC-6
            var range = new DoseRange(1, 5);
            var errs = PrescriptionActionIdRegistry.ValidatePerceptibleFloor(
                drugPotencyRaw: 1000L, range: range, doseBase: 65536);
            Assert.IsTrue(errs.Count > 0, "相邻档差不足应被拒");
            Assert.IsTrue(errs[0].Contains("DC-6"), $"错误须点名 DC-6,实际:{errs[0]}");
        }

        [Test]
        public void test_dc6_integerDoseRange_nullMeansWholeDose_noAdjacentDetents()
        {
            // 整剂路径(dose_range = null)⇒ 单档 ⇒ 无相邻档 ⇒ 零错(真空真,非借绿)
            var errs = PrescriptionActionIdRegistry.ValidatePerceptibleFloor(
                drugPotencyRaw: 1L, range: null, doseBase: 65536);
            Assert.AreEqual(0, errs.Count, "整剂路径无相邻档可判,不应报错");
        }

        [Test]
        public void test_dc6_positiveControl_comparatorIsActuallyInvoked()
        {
            // 阳性对照:同一输入换门槛 ⇒ 结果翻转,证谓词真在算(非恒真)
            var range = new DoseRange(1, 5);
            var loose = PrescriptionActionIdRegistry.ValidatePerceptibleFloor(
                1000L, range, 65536, floorRaw: 0L);
            var strict = PrescriptionActionIdRegistry.ValidatePerceptibleFloor(
                1000L, range, 65536, floorRaw: 1_000_000L);
            Assert.AreEqual(0, loose.Count, "极松门槛应通过");
            Assert.IsTrue(strict.Count > 0, "极严门槛应失败 —— 若两者同结果则谓词是恒真/恒假");
        }

        // ══════════════════════════════════════════════════════════════════
        // 影子真源的**显式记账**(禁借绿:判据非真判须可见)
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_shadowRegistry_flagIsSurfaced_notSilent()
        {
            // 本次烘焙用的影子真源必须**显式报出** —— 不得静默冒充真判据
            var bound = PrescriptionActionsBinderProbe.Bind(
                ReadActionsJson(), ReadLexiconJson(), ReadItemsJson());
            Assert.IsTrue(bound.ShadowRegistryUsed,
                "DC-2 / DC-6 真源缺席 ⇒ 影子标记须为 true;若已落地请更新本断言与登记");
        }

        [Test]
        public void test_shadowRegistry_realRegistryFileStillAbsent_canary()
        {
            // ⚠️ **真源落地的可执行钉子**(2026-10-07 评审缺陷 5):`ShadowRegistryUsed` 是硬编码
            //    true,若真源落地后忘改,该标记会**静默保持 true** 而无人察觉。
            //    本测盯的是**文件系统事实**(而非那个硬编码布尔):
            //    一旦 `prescription_action_registry.json` 出现,本测即红,强制更新登记与影子闭集。
            string realRegistryPath = Path.Combine(
                DataDir, PrescriptionActionIdRegistry.RealRegistryFileName);
            Assert.IsFalse(File.Exists(realRegistryPath),
                $"真源 '{PrescriptionActionIdRegistry.RealRegistryFileName}' 已出现于 {DataDir} —— " +
                "DC-2 的真源已落地:须撤除影子闭集、把 ValidateActionId 升格为硬失败、更新 EPIC 登记。");
        }

        [Test]
        public void test_shadowWarnings_areConsumedOnProductionPath_notOnlyInTests()
        {
            // ⚠️ 2026-10-07 评审缺陷 1:`ShadowWarnings` / `ShadowRegistryUsed` 此前在**非测试代码中
            //    零消费者** ⇒ 「判据非真判」在生产路径上完全不可见,与影子件自陈矛盾。
            //    本测是**源码面守卫**:菜单(唯一生产调用点)必须读这两列并报出。
            //    (菜单是 Editor 回调,EditMode 下不可直接调用 ⇒ 只能以源码面钉住接线。)
            string menuSrc = ReadSourceFile("DataBakeMenu.cs");
            Assert.IsTrue(menuSrc.Contains("result.ShadowWarnings"),
                "菜单须消费 result.ShadowWarnings —— 否则影子诊断只在测试里活着(生产路径静默)");
            Assert.IsTrue(menuSrc.Contains("result.ShadowRegistryUsed"),
                "菜单须消费 result.ShadowRegistryUsed —— 否则「本次用影子真源」生产路径不可见");
        }

        [Test]
        public void test_shadowRegistry_declaresItsOwnNonAuthority()
        {
            // 影子件必须**自陈非真源** —— 防后来者误读为已闭
            string src = ReadSourceFile("PrescriptionActionIdRegistry.cs");
            Assert.IsTrue(src.Contains("影子注册表,不是真源") || src.Contains("NOT-RUN"),
                "影子件须自陈非真源 / NOT-RUN,防误读为已闭");
        }

        // ══════════════════════════════════════════════════════════════════
        // 机制正确性缺口(2026-10-07 评审缺陷 2 / 3 / 4 的回归测)
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_dc6_reversedDoseRange_isRejectedNotSilentlyPassed()
        {
            // 缺陷 2:逆序域 [4,1] ⇒ DifferenceSequence 的 count = Max−Min = −3 ≤ 0 ⇒ 空序列
            // ⇒ 判据**静默恒通过**。逆序是**非法输入**,不得当「整剂路径」放行。
            var errs = PrescriptionActionIdRegistry.ValidatePerceptibleFloor(
                drugPotencyRaw: 65536L * 1000L, range: new DoseRange(4, 1), doseBase: 65536);
            Assert.IsTrue(errs.Count > 0, "逆序域须报错 —— 静默恒通过 = 「看起来绿但没跑」");
            Assert.IsTrue(errs[0].Contains("逆序"), $"须点名逆序,实际:{errs[0]}");
        }

        [Test]
        public void test_dc6_binder_reversedDoseRange_isHardFailure()
        {
            // 缺陷 2(烘焙门侧):逆序域在 binder 读入时即硬失败(与 42 侧 DentchDoseSelector 同口径)
            string itemsJson = ReadItemsJson().Replace("\"dose_range\": null", "\"dose_range\": [4, 1]");
            Assert.IsTrue(itemsJson.Contains("\"dose_range\": [4, 1]"),
                "夹具前提:须真的替换掉 dose_range(否则本测是真空真)");
            var ex = Assert.Throws<BakeValidationException>(() =>
                PrescriptionActionsBinderProbe.Bind(ReadActionsJson(), ReadLexiconJson(), itemsJson));
            Assert.IsTrue(ContainsError(ex, "逆序"), $"须报逆序,实际:{string.Join("; ", ex.Errors)}");
        }

        [Test]
        public void test_dc6_negativePotency_judgedByMagnitude_matchesDerivedBaker()
        {
            // 缺陷 3:域未声明(BL-7)⇒ 与 PrescriptionDerivedBaker 同口径**取绝对值**。
            // 合法负值药不得因符号被误判「违反」。
            var range = new DoseRange(1, 5);
            long negative = -65536L * 1000L;
            var errs = PrescriptionActionIdRegistry.ValidatePerceptibleFloor(
                drugPotencyRaw: negative, range: range, doseBase: 65536);
            Assert.AreEqual(0, errs.Count,
                $"|−65536×1000| / 65536 = 1000 ≥ 地板 100 ⇒ 负值药应通过,实际:{string.Join("; ", errs)}");
        }

        [Test]
        public void test_dc6_coverage_isAccounted_notSilentlySkipped()
        {
            // 缺陷 4:两条 `continue`(无 dose_range / 无 drug_potency)是合法跳过,但**静默跳过**
            // ⇒ 读者会把「零覆盖」读成「已合规」。覆盖率必须显式记账。
            var bound = PrescriptionActionsBinderProbe.Bind(
                ReadActionsJson(), ReadLexiconJson(), ReadItemsJson());
            Assert.IsTrue(ContainsWarning(bound.ShadowWarnings, "DC-6 覆盖"),
                $"DC-6 覆盖率须显式记账,实际:{string.Join("; ", bound.ShadowWarnings)}");
        }

        [Test]
        public void test_dc6_coverage_zeroEvaluation_isStatedNotImplied()
        {
            // 当前数据集 salicylic_acid 的 dose_range = null ⇒ DC-6 **零求值**。
            // 这种「判据未跑」必须**逐字写出来**,不能留给读者去数数据集。
            var bound = PrescriptionActionsBinderProbe.Bind(
                ReadActionsJson(), ReadLexiconJson(), ReadItemsJson());
            string coverage = null;
            foreach (string w in bound.ShadowWarnings)
                if (w.Contains("DC-6 覆盖")) coverage = w;
            Assert.IsNotNull(coverage, "须有 DC-6 覆盖记账行");
            Assert.IsTrue(coverage.Contains("零求值"),
                $"当前数据集 DC-6 应零求值并逐字声明,实际:{coverage}");
        }

        [Test]
        public void test_dc2_missingActionIdField_isNotReportedAsValueZero()
        {
            // 缺陷 8:字段**缺失**时 ReadInt 回退 0 并另记 error;此时不得再报「0 ∉ 闭集」
            // (那是失真 —— 缺失 ≠ 值为 0)。
            string badActionsJson = @"{
                ""schema_version"": 1,
                ""dose_const"": { ""DOSE_BASE"": 65536, ""MAX_DOSE_DETENTS"": 5 },
                ""actions"": [
                    { ""item_key"": ""salicylic_acid"", ""polarity"": ""symptomatic"" }
                ]
            }";
            var ex = Assert.Throws<BakeValidationException>(() =>
                PrescriptionActionsBinderProbe.Bind(badActionsJson, ReadLexiconJson(), ReadItemsJson()));
            Assert.IsTrue(ContainsError(ex, "action_id"), "缺失须由读件层记账");
        }

        /// <summary>读 `Editor.Tools.Bake` 下某源文件全文(源码面断言用)。</summary>
        private static string ReadSourceFile(string fileName)
            => File.ReadAllText(Path.Combine(
                RepoRoot, "unity", "Assets", "Editor.Tools.Bake", fileName));

        // ── 辅助:错误列表搜索 ─────────────────────────────────────────────

        private static bool ContainsWarning(IReadOnlyList<string> warnings, string keyword)
        {
            if (warnings == null) return false;
            foreach (string w in warnings)
                if (w.Contains(keyword)) return true;
            return false;
        }

        private static bool ContainsError(BakeValidationException ex, string keyword)
        {
            foreach (string e in ex.Errors)
                if (e.Contains(keyword)) return true;
            return false;
        }

        private static BakeValidationException RecordException(Action action)
        {
            try
            {
                action();
                return null;
            }
            catch (BakeValidationException ex)
            {
                return ex;
            }
        }

        // ── 辅助:JSON 字段提取 ─────────────────────────────────────────────

        private static List<string> ExtractItemKeys(string json)
        {
            var keys = new List<string>();
            var regex = new Regex(@"""item_key""\s*:\s*""([^""]+)""");
            foreach (Match m in regex.Matches(json))
                keys.Add(m.Groups[1].Value);
            return keys;
        }

        private static List<string> ExtractPolarities(string json)
        {
            var polarities = new List<string>();
            var regex = new Regex(@"""polarity""\s*:\s*""([^""]+)""");
            foreach (Match m in regex.Matches(json))
                polarities.Add(m.Groups[1].Value);
            return polarities;
        }

        private static int ExtractDoseBase(string json)
        {
            // DOSE_BASE 在 dose_const 对象内,需要匹配嵌套结构
            var regex = new Regex(@"""dose_const""\s*:\s*\{[^}]*""DOSE_BASE""\s*:\s*(\d+)");
            Match m = regex.Match(json);
            Assert.IsTrue(m.Success, "DOSE_BASE 字段未找到");
            return int.Parse(m.Groups[1].Value);
        }

        private static List<string> ExtractLexiconKeys(string json)
        {
            var keys = new List<string>();
            var regex = new Regex(@"""item_key""\s*:\s*""([^""]+)""");
            foreach (Match m in regex.Matches(json))
                keys.Add(m.Groups[1].Value);
            return keys;
        }

        private static List<string> ExtractBaseIds(string json)
        {
            var ids = new List<string>();
            var regex = new Regex(@"""base_id""\s*:\s*""([^""]+)""");
            foreach (Match m in regex.Matches(json))
                ids.Add(m.Groups[1].Value);
            return ids;
        }

        private static List<string> ExtractDrugIds(string json)
        {
            var ids = new List<string>();
            var regex = new Regex(@"""base_id""\s*:\s*""([^""]+)""");
            var categoryRegex = new Regex(@"""category""\s*:\s*""([^""]+)""");
            var matches = regex.Matches(json);
            var categoryMatches = categoryRegex.Matches(json);
            for (int i = 0; i < matches.Count; i++)
            {
                if (i < categoryMatches.Count && categoryMatches[i].Groups[1].Value == "drug")
                    ids.Add(matches[i].Groups[1].Value);
            }
            return ids;
        }

        private static List<(string itemKey, (int lo, int hi)? doseRange)> ExtractDrugProfiles(string json)
        {
            var result = new List<(string, (int, int)?)>();
            // 简化:只处理当前数据集(单药 salicylic_acid,dose_range = null)
            var regex = new Regex(@"""base_id""\s*:\s*""([^""]+)""");
            var matches = regex.Matches(json);
            foreach (Match m in matches)
            {
                string baseId = m.Groups[1].Value;
                // 检查是否有 drug_profile 且 dose_range 非 null
                int profileStart = json.IndexOf($"\"{baseId}\"", m.Index);
                if (profileStart < 0) continue;
                int profileEnd = json.IndexOf("}", profileStart);
                string profile = json.Substring(profileStart, profileEnd - profileStart);
                if (profile.Contains("\"drug_profile\"") && !profile.Contains("\"drug_profile\": null"))
                {
                    // 有 drug_profile,检查 dose_range
                    if (profile.Contains("\"dose_range\": null"))
                    {
                        result.Add((baseId, null));
                    }
                    else
                    {
                        // 解析 dose_range
                        var drRegex = new Regex(@"""dose_range""\s*:\s*\[(\d+),\s*(\d+)\]");
                        Match drMatch = drRegex.Match(profile);
                        if (drMatch.Success)
                        {
                            int lo = int.Parse(drMatch.Groups[1].Value);
                            int hi = int.Parse(drMatch.Groups[2].Value);
                            result.Add((baseId, (lo, hi)));
                        }
                    }
                }
            }
            return result;
        }
    }
}
