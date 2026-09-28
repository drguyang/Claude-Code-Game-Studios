// 权威来源:production/epics/audio-system/story-013-main-channel-listen-acceptance.md
//   (AC-44-17 [L] 双盲 forced-choice 听测:病人三态呼吸隔门可辨)
//   · AC-44-17 是 [L](人工听测),不是 BLOCKING 断言 —— 硬标 [L] BLOCKING = 谎报可测试性
//   · 协议 = 双盲 forced-choice(n ≥ 6 × 三对 ≥ 8 次);判对率 ≥ 75% 且三态均可辨
//   · 本 story 几乎零代码 —— 验收 Story 004/008/009 的合成效果
// ADR-018(音频架构:主通道有总线 AC-44-D8)· GDD §Player Fantasy「门后的人还在喘气」
// coding-standards:Visual/Feel → Screenshot + lead sign-off → ADVISORY
//
// ⚠️ 落点:故事 Test Evidence 登记路径 = tests/unit/audio_system/main_channel_listen_test.cs;
//    Unity 只编译 unity/Assets/树 ⇒ **真身 = 本文件**(承 Story 001-012/014 同一先例);
//    账本侧由 tests/unit/audio_system/README.md 互链。
// ⚠️ 本 story 是 Visual/Feel 类型 —— EditMode 测试覆盖**文档判据**(AC-44-17 存在且挂点正确),
//    不覆盖听测本身(双盲 forced-choice = 人工执行,无法自动化)。
// ⚠️ 测试纪律:arrange/act/assert · 无随机 · 无时间依赖(禁 DateTime/Time)·
//    夹具读取前置 File.Exists 断言(缺失即红,不静默跳过)。

using System;
using System.IO;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using DaYiJingCheng.EditorTools.Gates;

namespace DaYiJingCheng.Tests.Unit.Audio
{
    [TestFixture]
    internal sealed class MainChannelListenTest
    {
        // ══════════════ 基础设施 ══════════════

        // 本文件在 unity/Assets/Tests/EditMode/Audio/ ⇒ Audio→EditMode→Tests→Assets→unity→
        // 仓库根 = 5 层(写 4 层 ⇒ root 落在 unity/ 下,夹具 / GDD / 源树全报「缺失」)。
        private static string repoRoot([CallerFilePath] string thisFile = "")
            => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile) ?? ".",
                "..", "..", "..", "..", ".."));

        // ══════════════ AC-44-17:文档判据 ══════════════

        /// <summary>AC-44-17 [L]:GDD 存在 AC-44-17 条目且挂点正确。
        /// 验证 GDD 含 AC-44-17 且标注 [L](人工听测,非 BLOCKING 断言)。</summary>
        [Test]
        public void test_ac4417_existsInGdd()
        {
            // Arrange:读 GDD
            string path = Path.Combine(repoRoot(), "design", "gdd", "audio-system.md");
            Assert.That(File.Exists(path), Is.True, $"GDD 缺失:{path}");
            string text = File.ReadAllText(path);

            // Act + Assert:AC-44-17 存在
            Assert.That(text.Contains("AC-44-17"), Is.True,
                "AC-44-17 必须存在(GDD:主通道听测验收)");
        }

        /// <summary>AC-44-17 [L]:标注为 [L](人工听测,非 BLOCKING 断言)。
        /// 硬标 [L] BLOCKING = 谎报可测试性。</summary>
        [Test]
        public void test_ac4417_markedAsL()
        {
            // Arrange:读 GDD
            string path = Path.Combine(repoRoot(), "design", "gdd", "audio-system.md");
            Assert.That(File.Exists(path), Is.True, $"GDD 缺失:{path}");
            string text = File.ReadAllText(path);

            // Act + Assert:AC-44-17 行含 [L] 标注
            int ac4417Idx = text.IndexOf("AC-44-17", StringComparison.Ordinal);
            Assert.That(ac4417Idx, Is.GreaterThanOrEqualTo(0), "AC-44-17 必须存在");
            int searchEnd = Math.Min(ac4417Idx + 200, text.Length);
            string ac4417Block = text.Substring(ac4417Idx, searchEnd - ac4417Idx);
            Assert.That(ac4417Block.Contains("[L]"), Is.True,
                "AC-44-17 必须标注 [L](人工听测,非 BLOCKING 断言;硬标 [L] BLOCKING = 谎报可测试性)");
        }

        /// <summary>AC-44-17:协议 = 双盲 forced-choice(n ≥ 6 × 三对 ≥ 8 次)。
        /// 验证 GDD 含协议描述。</summary>
        [Test]
        public void test_ac4417_protocol_described()
        {
            // Arrange:读 GDD
            string path = Path.Combine(repoRoot(), "design", "gdd", "audio-system.md");
            Assert.That(File.Exists(path), Is.True, $"GDD 缺失:{path}");
            string text = File.ReadAllText(path);

            // Act:定位 AC-44-17 上下文窗口(500 字符,因 AC-44-17 行跨约 350 字符)
            int ac4417Idx = text.IndexOf("AC-44-17", StringComparison.Ordinal);
            Assert.That(ac4417Idx, Is.GreaterThanOrEqualTo(0), "AC-44-17 必须存在");
            int searchEnd = Math.Min(ac4417Idx + 500, text.Length);
            string ac4417Block = text.Substring(ac4417Idx, searchEnd - ac4417Idx);

            // Assert:协议描述在 AC-44-17 上下文中
            Assert.That(ac4417Block.Contains("forced-choice"), Is.True,
                "协议必须含 forced-choice(双盲听测)");
            Assert.That(ac4417Block.Contains("n ≥ 6"), Is.True,
                "协议必须含 n ≥ 6(被试数下限)");
            Assert.That(ac4417Block.Contains("≥ 75%"), Is.True,
                "协议必须含判对率 ≥ 75%(三态可辨)");
            Assert.That(ac4417Block.Contains("三对"), Is.True,
                "协议必须含三对判别结构(0,1)(1,2)(0,2)");
            Assert.That(ac4417Block.Contains("≥ 8"), Is.True,
                "协议必须含每对 ≥ 8 次(forced-choice 重复次数下限)");
        }

        /// <summary>AC-44-17:三态(平稳 → 急促 → 停顿)可辨。
        /// 验证 GDD 含三态描述。</summary>
        [Test]
        public void test_ac4417_threeStates_distinguishable()
        {
            // Arrange:读 GDD
            string path = Path.Combine(repoRoot(), "design", "gdd", "audio-system.md");
            Assert.That(File.Exists(path), Is.True, $"GDD 缺失:{path}");
            string text = File.ReadAllText(path);

            // Act:定位 AC-44-17 上下文窗口(200 字符)
            int ac4417Idx = text.IndexOf("AC-44-17", StringComparison.Ordinal);
            Assert.That(ac4417Idx, Is.GreaterThanOrEqualTo(0), "AC-44-17 必须存在");
            int searchEnd = Math.Min(ac4417Idx + 200, text.Length);
            string ac4417Block = text.Substring(ac4417Idx, searchEnd - ac4417Idx);

            // Assert:三态描述在 AC-44-17 上下文中
            Assert.That(ac4417Block.Contains("平稳"), Is.True, "三态之一:平稳(AC-44-17 上下文)");
            Assert.That(ac4417Block.Contains("急促"), Is.True, "三态之一:急促(AC-44-17 上下文)");
            Assert.That(ac4417Block.Contains("停顿"), Is.True, "三态之一:停顿(AC-44-17 上下文)");
        }

        // ══════════════ 证据文档登记 ══════════════

        /// <summary>证据文档存在:production/qa/evidence/main-channel-listen-evidence.md。
        /// 听测数据 + 签署(ADVISORY 门级)。当前阶段登记为待执行(需被试排期)。</summary>
        [Test]
        public void test_evidenceDoc_exists()
        {
            // Arrange
            string path = Path.Combine(repoRoot(), "production", "qa", "evidence",
                "main-channel-listen-evidence.md");

            // Act + Assert:证据文档存在
            Assert.That(File.Exists(path), Is.True,
                $"证据文档缺失:{path}(AC-44-17:听测数据 + 签署)");
        }

        /// <summary>证据文档含听测协议(双盲 forced-choice)。</summary>
        [Test]
        public void test_evidenceDoc_protocolPresent()
        {
            // Arrange
            string path = Path.Combine(repoRoot(), "production", "qa", "evidence",
                "main-channel-listen-evidence.md");
            Assert.That(File.Exists(path), Is.True, $"证据文档缺失:{path}");
            string text = File.ReadAllText(path);

            // Act + Assert:协议在文档中
            Assert.That(text.Contains("forced-choice"), Is.True,
                "证据文档必须含 forced-choice 协议");
            Assert.That(text.Contains("判对率"), Is.True,
                "证据文档必须含判对率记录");
            Assert.That(text.Contains("≥ 75%") || text.Contains(">= 75%"), Is.True,
                "证据文档必须含判对率阈值 75%");
        }

        /// <summary>证据文档含当前状态(待执行/需被试排期)。
        /// 听测是人工执行,当前阶段无法自动化。</summary>
        [Test]
        public void test_evidenceDoc_statusPresent()
        {
            // Arrange
            string path = Path.Combine(repoRoot(), "production", "qa", "evidence",
                "main-channel-listen-evidence.md");
            Assert.That(File.Exists(path), Is.True, $"证据文档缺失:{path}");
            string text = File.ReadAllText(path);

            // Act + Assert:状态在文档中
            Assert.That(text.Contains("待执行") || text.Contains("BLOCKED") || text.Contains("被试"),
                Is.True,
                "证据文档必须含当前状态(待执行/需被试排期;听测是人工执行,当前阶段无法自动化)");
        }
    }
}
