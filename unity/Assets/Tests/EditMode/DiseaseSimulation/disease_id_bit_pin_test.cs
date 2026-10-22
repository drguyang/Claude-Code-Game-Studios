// DiseaseId ↔ DiseaseIdSet bit index —— 位序契约的机械 pin(2026-10-22 立)。
//
// 权威来源:
//   `unity/Assets/Sim.Contracts/Payloads/PayloadCommon.cs` —— `DiseaseIdSet` 的位序契约块
//     (`bit index = DiseaseId − 1`;「ordinal」一词停用)
//   `unity/Assets/Sim/RegistrySchema.cs` —— `R1_CHECK_33`(DiseaseId ∈ [1,64] 值域守卫)
//   `unity/Assets/Gameplay.Boot/TelemetryReporter.cs:128/:596` —— 唯一的 bit→DiseaseId 转换点
//     (`diseases.Add(bit + 1)`)
//   `assets/data/disease_registry.json` —— 真表,id 从 **2** 起(`id = 1` 归 fallback)
//
// ── 立件缘由(为什么光有注释不够)──────────────────────────────────────────────
// 2026-10-22 实测:同一条位掩码有两个说法 ——
//   · `PatternDetector.DiseaseOrdinalsOf` 返回 **0-based bit**(不加 1);
//   · `TelemetryReporter` 注释称「DiseaseId = i + 1」,**代码也确实 +1**。
// 两者**都对**,但它们是**两个不同的量**(bit index vs DiseaseId),而「ordinal」一词被同时
// 用来指这两者 ⇒ 读的人无从判断某处该不该 +1。
//
// 更麻烦的是:**既有夹具恰好看不出这个区别** —— `f2_wiring_test` 的注册表 id 从 **1** 起,
// 而真表从 **2** 起。在「id = 1,2,3…」的夹具里,`bit = id − 1` 与 `bit = 行序` **数值相同**
// ⇒ 两种读法都绿。这类夹具是**假绿**的标准形态:它证明的不是契约,是巧合。
//
// ⇒ 本文件用**真表**(id 从 2 起)钉契约,并显式钉住「id 起点 ≠ 1」这个使夹具失效的事实。
//
// ── 三条判据 ─────────────────────────────────────────────────────────────────
//   P1 真表 id 集合 → `bit = id − 1` → 反解 `bit + 1` = 原 id(往返闭合)
//   P2 真表 id 集合**不等于** 1..N(即 id 有空洞且不从 1 起)⇒ 「bit = 行序」会错位
//   P3 `R1_CHECK_33` 对 `DiseaseId = 0` / `65` 均拒收(越界静默回绕的值域守卫真的在)

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using DaYiJingCheng.EditorTools.Bake;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Unit.Sim
{
    [TestFixture]
    internal sealed class DiseaseIdBitPinTest
    {
        /// <summary>仓库根 —— 由本文件路径上溯(`[CallerFilePath]` 编译期常量,不受 cwd 影响)。</summary>
        private static string RepoRoot([CallerFilePath] string thisFile = "")
            => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile) ?? ".",
                "..", "..", "..", "..", ".."));

        /// <summary>`assets/data/disease_registry.json` 绝对路径。</summary>
        private static string DataPath =>
            Path.Combine(RepoRoot(), "assets", "data", "disease_registry.json");

        /// <summary>走**真 binder**(不经手写最小读取器 —— 那类读取器会静默跳过非 injury 行)。</summary>
        private static IReadOnlyList<DiseaseRegistryEntry> RealRows()
        {
            Assert.That(File.Exists(DataPath), Is.True, $"真表缺失:{DataPath}");
            // Bind 违例时抛 BakeValidationException ⇒ 不静默返回半成型行集。
            return DiseaseRegistryBinder.Bind(File.ReadAllText(DataPath)).Rows;
        }

        // ══════════════ P1:往返闭合 ══════════════

        /// <summary>
        /// <b>P1</b>:真表每个 <c>DiseaseId</c> ⇒ <c>bit = DiseaseId − 1</c> ⇒ 反解 <c>bit + 1</c>
        /// 必须回到原 id,且 bit 全落在 <c>[0, 63]</c>。
        ///
        /// <para><b>判别力</b>:若有人把 <c>bit</c> 定义成行序(0,1,2…),本条对真表**转红** ——
        /// 因为真表 id 从 2 起,行序 0 对应 id 2,反解得 1 ≠ 2。</para>
        /// </summary>
        [Test]
        public void test_p1_realRegistry_bitIsDiseaseIdMinusOne_roundTrips()
        {
            var rows = RealRows();
            Assert.That(rows.Count, Is.GreaterThanOrEqualTo(1), "R1-17:至少 1 个病种");

            foreach (var row in rows)
            {
                int bit = row.DiseaseId - 1;

                Assert.That(bit, Is.InRange(0, 63),
                    $"{row.DiseaseKey}: DiseaseId={row.DiseaseId} ⇒ bit={bit} 越出 ulong 的 64 位");

                // 掩码往返:置位后必须能反解出原 id(这正是 TelemetryReporter 的 bit + 1 路径)。
                ulong mask = 1UL << bit;
                int decodedId = -1;
                for (int i = 0; i < 64; i++)
                {
                    if ((mask & (1UL << i)) != 0UL) { decodedId = i + 1; break; }
                }

                Assert.That(decodedId, Is.EqualTo(row.DiseaseId),
                    $"{row.DiseaseKey}: bit={bit} 反解得 {decodedId},应回 {row.DiseaseId}");
            }
        }

        // ══════════════ P2:id 起点 ≠ 1(夹具失效的实证) ══════════════

        /// <summary>
        /// <b>P2</b>:真表的 <c>DiseaseId</c> 集合<b>不等于</b> <c>1..N</c> —— 具体地,
        /// 最小值 = 2(<c>id = 1</c> 归 <c>CompositionRoot.CreateFallbackRegistry</c>,不进 JSON)。
        ///
        /// <para><b>为什么这条本身是判据</b>:它把「<c>bit = id − 1</c>」与「<c>bit = 行序</c>」
        /// 这两种读法**在真表上区分开**。若哪天有人把 fallback 条目也塞进 JSON(使 id 从 1 连续),
        /// 本条转红 ⇒ 提醒「你的夹具又能掩盖这个区别了」。</para>
        ///
        /// <para><b>⚠️ 反向断言的意义</b>:本条**不是**在说「id 必须从 2 起」是永久规则
        /// (那是数据决定的事);它说的是「**今天** id 起点 ≠ 1,所以任何 id 从 1 起的夹具
        /// 都**不能**用来验证位序契约」。后者是本条真正要钉的东西。</para>
        /// </summary>
        [Test]
        public void test_p2_realRegistry_idsAreNotContiguousFromOne()
        {
            var rows = RealRows();
            var ids = rows.Select(r => r.DiseaseId).OrderBy(x => x).ToList();

            Assert.That(ids[0], Is.EqualTo(2),
                "真表最小 DiseaseId 应为 2(id = 1 归 CreateFallbackRegistry 的占位条目)" +
                " —— 若此断言红,请复核 fallback 与 JSON 的 id 分工是否被改动");

            bool contiguousFromOne = ids.SequenceEqual(Enumerable.Range(1, ids.Count));
            Assert.That(contiguousFromOne, Is.False,
                "真表 id 恰好连续从 1 起 ⇒ 「bit = id−1」与「bit = 行序」在真表上**不可区分**," +
                "本文件的 P1 随之失去判别力。须改用带空洞的 id 集合,或换别的判据。");
        }

        // ══════════════ P3:R1_CHECK_33 值域守卫真的在 ══════════════

        /// <summary>
        /// <b>P3</b>:<c>R1_CHECK_33</c> 对 <c>DiseaseId = 0</c> 与 <c>65</c> 均拒收。
        ///
        /// <para><b>判别力</b>:删掉 <c>Validate</c> 里的 R1-33 分支 ⇒ 本条两半**都**转红
        /// (<c>DoesNotThrow</c> 会抛,<c>Throws</c> 不抛)。这是「注释声称有门」的补票:
        /// <c>DiseaseIdSet</c> 长期自陈「超界 = 数据层构建失败」,而那个失败此前不存在。</para>
        ///
        /// <para><b>为什么 65 而不是 64</b>:<c>64</c> 是**合法**上界(bit index 63)。
        /// 越界的是 <c>65</c> ⇒ bit index 64 ⇒ <c>1UL &lt;&lt; 64</c> ⇒ C# 按 <c>&amp; 0x3F</c>
        /// 静默取 <c>1UL &lt;&lt; 0</c>。本条同时正向验证 64 合法,防「一刀切成 ≤63」的过宽修法。</para>
        /// </summary>
        [Test]
        public void test_p3_r1_33_rejectsOutOfRangeDiseaseId()
        {
            Assert.That(RegistrySchemaValidator.DiseaseIdMin, Is.EqualTo(1));
            Assert.That(RegistrySchemaValidator.DiseaseIdMax, Is.EqualTo(64));

            // 正向:1 与 64 都是合法边界(不得过宽)。
            Assert.DoesNotThrow(() => RegistrySchemaValidator.Validate(new List<DiseaseRegistryEntry>
            {
                MinimalEntry(1), MinimalEntry(64),
            }), "DiseaseId 1 / 64 是合法边界,不得被 R1-33 拒收");

            // 下界外:0 ⇒ bit index = −1 ⇒ 左移负数(未定义行为)。
            var exLow = Assert.Throws<RegistryValidationException>(
                () => RegistrySchemaValidator.Validate(new List<DiseaseRegistryEntry> { MinimalEntry(0) }),
                "DiseaseId = 0 必须被拒(bit index = −1,左移负数为未定义行为)");
            Assert.That(exLow!.RuleNumber, Is.EqualTo(RegistrySchemaValidator.R1_CHECK_33));

            // 上界外:65 ⇒ bit index = 64 ⇒ 移位按 & 0x3F 回绕到 bit 0(静默撞 id 1)。
            var exHigh = Assert.Throws<RegistryValidationException>(
                () => RegistrySchemaValidator.Validate(new List<DiseaseRegistryEntry> { MinimalEntry(65) }),
                "DiseaseId = 65 必须被拒(bit index = 64,ulong 移位静默回绕)");
            Assert.That(exHigh!.RuleNumber, Is.EqualTo(RegistrySchemaValidator.R1_CHECK_33));
        }

        /// <summary>最小合法条目(只填 R1-33 之前/无关的必填项;Curve = null ⇒ 曲线面整块跳过)。</summary>
        private static DiseaseRegistryEntry MinimalEntry(int diseaseId) => new DiseaseRegistryEntry
        {
            DiseaseId = diseaseId,
            DiseaseKey = $"DIS_PIN_{diseaseId}",
            Kind = DiseaseKind.Disease,
            Polarity = 2,          // 平
            Severity = 1,
            Contagion = 0,
            Lethality = 0,
            TreatmentDifficulty = 1,
            RecoveryTime = 1,
            Scale = new Fix(Fix.OneRaw),
            Curve = null,          // 曲线面校验整块跳过(既有 legacy 口径)
        };
    }
}
