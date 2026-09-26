// 权威来源:production/epics/audio-system/story-003-mixer-topology-snapshots.md(八轮 QA/评审
//   BLOCKING:生成器约 1400 行 0 断言 —— 纯字符串函数可直接单测,零 batch 依赖)
// 覆盖:
//   · PruneForeignSnapshots —— ① 5 员齐不改 ② `Default - Copy`(含 `-`)必裁 ③ REC 回归:
//     允许集须含 `reverb_preset_*` 前缀(否则将来补建的 preset 快照被当野员删)
//   · RepairDanglingSnapshotSlots —— 回归:`m_TargetSnapshot` 指向不存在 fileID ⇒ 重指存留快照
//   · PruneOrphanEffects —— 回归:**负 fileID** 孤儿必裁(AmpFileId 漏负号 = 静默跳过的假绿)
//   · NormalizeExposedGuids —— 全零 ⇒ 确定性非零互异;非零不变(幂等);重复跑结果相同
// 纪律:命名承 mixer_topology_test 先例 · arrange/act/assert · 夹具**内联**(纯字符串函数,
//   不落 fixtures 文件)· 无随机(确定性 hex / 固定 fileID)· 无时间依赖 · 零 I/O。

using System;
using System.Collections.Generic;
using NUnit.Framework;
using DaYiJingCheng.EditorTools.Gates;

namespace DaYiJingCheng.Tests.Unit.Audio
{
    /// <summary>MixerAssetGenerator 纯字符串函数的 EditMode 测试(不跑 batch、不读资产)。</summary>
    [TestFixture]
    internal sealed class MixerAssetGeneratorTest
    {
        // ══════════════ PruneForeignSnapshots ══════════════

        /// <summary>正例:五员齐 ⇒ 原样返回(幂等:再跑一次仍相同)。</summary>
        [Test]
        public void test_pruneSnapshots_fiveMembersPresent_unchanged()
        {
            // Arrange
            string text = FiveMemberMixerText();
            var log = new List<string>();

            // Act
            string once = MixerAssetGenerator.PruneForeignSnapshots(text, log);
            string twice = MixerAssetGenerator.PruneForeignSnapshots(once, log);

            // Assert
            Assert.That(once, Is.EqualTo(text), "五员齐的合法文本不得被改动");
            Assert.That(twice, Is.EqualTo(once), "幂等:二次裁剪结果相同");
            Assert.That(log, Is.Empty, () => string.Join("\n", log));
        }

        /// <summary>负例:<c>Default - Copy</c>(名含 `-`,禁前缀匹配放过)⇒ 文档与
        /// <c>m_Snapshots</c> 引用双双被裁,其余五员无损。</summary>
        [Test]
        public void test_pruneSnapshots_wildCopyMember_removed()
        {
            // Arrange
            string text = FiveMemberMixerText() +
                          SnapshotDoc("Default - Copy", 3999000000000001L) +
                          "  - {fileID: 3999000000000001}\n";
            Assert.That(text, Does.Contain("Default - Copy"), "夹具自证:野员在场");

            // Act
            string output = MixerAssetGenerator.PruneForeignSnapshots(text, new List<string>());

            // Assert
            Assert.That(output, Does.Not.Contain("Default - Copy"),
                "野员「Default - Copy」必裁 —— 整行 m_Name 精确比对,前缀匹配会放过含 `-` 的名");
            Assert.That(output, Does.Not.Contain("{fileID: 3999000000000001}"),
                "被裁快照在 m_Snapshots: 的引用行须一并摘除");
            Assert.That(output, Does.Contain("StethoscopeFocus"), "五员不得误删");
            Assert.That(output, Does.Contain("m_Name: Default"), "Default 不得误删");
        }

        /// <summary>REC 回归:允许集 = 五员 ∪ <c>reverb_preset_</c> 前缀 ——
        /// preset 快照由生成器占位(MixerRegistry 自述),不得被当野员删。</summary>
        [Test]
        public void test_pruneSnapshots_reverbPresetPrefix_kept()
        {
            // Arrange
            string text = FiveMemberMixerText() + SnapshotDoc("reverb_preset_indoor", 3888000000000001L);

            // Act
            string output = MixerAssetGenerator.PruneForeignSnapshots(text, new List<string>());

            // Assert
            Assert.That(output, Does.Contain("reverb_preset_indoor"),
                "允许集须含 reverb_preset_ 前缀(五员 ∪ 前缀,见 PruneForeignSnapshots 头注 REC)");
            Assert.That(output, Does.Contain("m_Name: Paused"), "对照:五员仍在");
        }

        // ══════════════ RepairDanglingSnapshotSlots(回归)══════════════

        /// <summary>回归:<c>m_TargetSnapshot</c> 指向**不存在**的 fileID ⇒ 重指到存留快照
        /// (评审修复的 bug —— 悬空槽位会让 Unity 快照切换打空)。</summary>
        [Test]
        public void test_repairDanglingTargetSlot_repointsToSurvivor()
        {
            // Arrange:悬空 4242… 槽位 + 存留快照 &3000
            const string dangling = "4242424242424242";
            string text =
                "--- !u!241 &1000\n" +
                "AudioMixerController:\n" +
                "  m_MasterGroup: {fileID: 2000}\n" +
                "  m_TargetSnapshot: {fileID: " + dangling + "}\n" +
                "  m_Snapshots:\n" +
                "  - {fileID: 3000}\n" +
                SnapshotDoc("Default", 3000);
            Assert.That(text, Does.Contain(dangling), "夹具自证:悬空槽在场");

            // Act
            string output = MixerAssetGenerator.RepairDanglingSnapshotSlots(text, new List<string>());

            // Assert
            Assert.That(output, Does.Not.Contain(dangling),
                "悬空 fileID 必须被重指(AmpFileId/槽位修复回归)");
            Assert.That(output, Does.Contain("m_TargetSnapshot: {fileID: 3000}"),
                "应重指到 m_Snapshots 列表内的存留快照");
        }

        // ══════════════ PruneOrphanEffects(负 fileID 回归)══════════════

        /// <summary>回归:**负 fileID** 头行的孤儿 effect(<c>&amp;-3543…</c> +
        /// <c>m_SendTarget ≠ 0</c> + 未挂任何组)必被裁 —— <c>AmpFileId</c> 漏负号 ⇒
        /// 解析成 0 ⇒ 静默跳过(假绿)。已挂组 effect 不得误删。</summary>
        [Test]
        public void test_pruneOrphanEffects_negativeFileId_removed()
        {
            // Arrange
            const string negativeId = "-3543781766084107196";
            string text =
                "--- !u!243 &2000\n" +
                "AudioMixerGroupController:\n" +
                "  m_Name: Master\n" +
                "  m_Effects:\n" +
                "  - {fileID: 24410}\n" +
                "--- !u!244 &24410\n" +
                "AudioMixerEffectController:\n" +
                "  m_EffectName: OwnedAttenuation\n" +
                "  m_SendTarget: {fileID: 0}\n" +
                "--- !u!244 &" + negativeId + "\n" +
                "AudioMixerEffectController:\n" +
                "  m_EffectName: OrphanCopy\n" +
                "  m_SendTarget: {fileID: -6089743339191151598}\n";

            // Act
            string output = MixerAssetGenerator.PruneOrphanEffects(text, new List<string>());

            // Assert
            Assert.That(output, Does.Not.Contain(negativeId),
                "负 fileID 孤儿必裁 —— AmpFileId 须解析负号,否则此处漏裁(回归)");
            Assert.That(output, Does.Not.Contain("OrphanCopy"));
            Assert.That(output, Does.Contain("OwnedAttenuation"), "已挂组 effect 不得误删");
            Assert.That(output, Does.Contain("{fileID: 24410}"), "组的 m_Effects 引用不得误删");
        }

        // ══════════════ NormalizeExposedGuids ══════════════

        /// <summary>全零 GUID ⇒ 确定性非零互异(<c>b5</c> 前缀 / 32 位 / 7 条两两不同);
        /// 重复跑结果相同(幂等)。</summary>
        [Test]
        public void test_normalizeExposedGuids_allZero_becomesDistinctNonZero()
        {
            // Arrange:7 条全零(真资产实况)
            string text = ExposedBlockText(new string('0', 32));

            // Act
            string once = MixerAssetGenerator.NormalizeExposedGuids(text, new List<string>());
            string twice = MixerAssetGenerator.NormalizeExposedGuids(once, new List<string>());

            // Assert
            List<string> guids = ExtractGuids(once);
            Assert.That(guids, Has.Count.EqualTo(7), "夹具自证:7 条");
            foreach (string guid in guids)
            {
                Assert.That(guid, Is.Not.All.EqualTo('0'), $"GUID 仍全零:{guid}");
                Assert.That(guid, Has.Length.EqualTo(32), "32 位 hex");
                Assert.That(guid.StartsWith("b5", StringComparison.Ordinal), Is.True,
                    $"确定性前缀 b5:{guid}");
            }
            var distinct = new HashSet<string>(guids, StringComparer.Ordinal);
            Assert.That(distinct.Count, Is.EqualTo(7), "7 条须两两互异(全零 = Unity 视为同一参数)");
            Assert.That(twice, Is.EqualTo(once), "幂等:非零不再改写");
        }

        /// <summary>非零 GUID 保持不变(不误改 Unity / 用户已写的值)。</summary>
        [Test]
        public void test_normalizeExposedGuids_nonZeroPreserved()
        {
            // Arrange:1 条自定义非零 + 1 条全零
            const string custom = "ab12cd34ef56ab78cd90ef12ab34cd56";
            string text =
                "--- !u!241 &1000\n" +
                "AudioMixerController:\n" +
                "  m_ExposedParameters:\n" +
                "  - guid: " + custom + "\n" +
                "    name: bus_volume_master\n" +
                "  - guid: " + new string('0', 32) + "\n" +
                "    name: bus_volume_music\n";

            // Act
            string output = MixerAssetGenerator.NormalizeExposedGuids(text, new List<string>());

            // Assert
            Assert.That(output, Does.Contain(custom), "非零 GUID 必须原样保留");
            List<string> guids = ExtractGuids(output);
            Assert.That(guids.Count, Is.EqualTo(2));
            Assert.That(guids[0], Is.EqualTo(custom));
            Assert.That(guids[1], Is.Not.All.EqualTo('0'), "全零那条须被规整");
        }

        // ══════════════ 内联夹具小件 ══════════════

        /// <summary>五员齐的最小 mixer 文本(根 + 5 快照文档,m_Snapshots 引用齐全)。</summary>
        private static string FiveMemberMixerText()
        {
            var names = new[] { "Default", "StethoscopeFocus", "DialogueFocus", "Paused", "VRComfort" };
            string root =
                "--- !u!241 &1000\n" +
                "AudioMixerController:\n" +
                "  m_MasterGroup: {fileID: 2000}\n" +
                "  m_Snapshots:\n";
            for (int i = 0; i < names.Length; i++)
                root += "  - {fileID: " + (3000 + i) + "}\n";

            string docs = "";
            for (int i = 0; i < names.Length; i++)
                docs += SnapshotDoc(names[i], 3000 + i);
            return root + docs;
        }

        private static string SnapshotDoc(string name, long fileId) =>
            "--- !u!245 &" + fileId + "\n" +
            "AudioMixerSnapshotController:\n" +
            "  m_Name: " + name + "\n" +
            "  m_FloatValues: {}\n";

        /// <summary>7 条全零 GUID 的 m_ExposedParameters 块(真资产实况形态:两行一条)。</summary>
        private static string ExposedBlockText(string guid) =>
            "--- !u!241 &1000\n" +
            "AudioMixerController:\n" +
            "  m_ExposedParameters:\n" +
            ExposedEntry(guid, "bus_volume_master") +
            ExposedEntry(guid, "bus_volume_music") +
            ExposedEntry(guid, "bus_volume_ambience") +
            ExposedEntry(guid, "bus_volume_voice") +
            ExposedEntry(guid, "bus_volume_sfx") +
            ExposedEntry(guid, "bus_volume_stethoscope") +
            ExposedEntry(guid, "bus_volume_uicue");

        private static string ExposedEntry(string guid, string name) =>
            "  - guid: " + guid + "\n    name: " + name + "\n";

        private static List<string> ExtractGuids(string text)
        {
            var guids = new List<string>();
            foreach (string line in text.Replace("\r\n", "\n").Split('\n'))
            {
                string trimmed = line.Trim();
                // 真形态是 YAML 列表项 `- guid: <hex>`(trim 后以 "- " 开头)——
                // 只匹配裸 `guid:` 开头会恒返回 0 条(2026-09-26 实测假红)。
                int key = trimmed.IndexOf("guid:", StringComparison.Ordinal);
                if (key < 0) continue;
                string value = trimmed.Substring(key + "guid:".Length).Trim();
                if (value.Length > 0) guids.Add(value);
            }
            return guids;
        }
    }
}
