// 权威来源:production/epics/audio-system/story-003-mixer-topology-snapshots.md(八轮 QA/评审
//   BLOCKING:生成器约 1400 行 0 断言 —— 纯字符串函数可直接单测,零 batch 依赖)
// 覆盖:
//   · PruneForeignSnapshots —— ① 5 员齐不改 ② `Default - Copy`(含 `-`)必裁 ③ REC 回归:
//     允许集须含 `reverb_preset_*` 前缀(否则将来补建的 preset 快照被当野员删)
//   · RepairDanglingSnapshotSlots —— 回归:`m_TargetSnapshot` 指向不存在 fileID ⇒ 重指存留快照
//   · PruneOrphanEffects —— 回归:**负 fileID** 孤儿必裁(AmpFileId 漏负号 = 静默跳过的假绿)
//   · NormalizeExposedGuids ——(2026-09-26 新契约,用户裁定)暴露 guid = **同名组 m_Volume
//     哈希**(真参数落点);查不到组 / 名 ⇒ LogError + 硬 throw;旧 b500… 合成条序值**修复**
//     (旧契约「全零⇒b500…/非零保留」已作废 —— 合成 guid 对不上真实参数 = 哑 SetFloat)
// 纪律:命名承 mixer_topology_test 先例 · arrange/act/assert · 夹具**内联**(纯字符串函数,
//   不落 fixtures 文件)· 无随机(确定性 hex / 固定 fileID)· 无时间依赖 · 零 I/O。

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
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

        // ══════════════ NormalizeExposedGuids(2026-09-26 新契约:真哈希对齐)════════════

        /// <summary>新契约正例:暴露 guid = **同名组 m_Volume 哈希**(全零输入 ⇒ 对齐到真哈希);
        /// 7 条互异(各组哈希互异);幂等(已是真哈希不再改写)。</summary>
        [Test]
        public void test_normalizeExposedGuids_guidEqualsGroupVolumeHash()
        {
            // Arrange:7 组文档(各带真哈希)+ 7 条全零 exposed(真资产旧实况)
            string text = GroupsBlockText() + ExposedBlockText(new string('0', 32));

            // Act
            string once = MixerAssetGenerator.NormalizeExposedGuids(text, new List<string>());
            string twice = MixerAssetGenerator.NormalizeExposedGuids(once, new List<string>());

            // Assert
            List<string> guids = ExtractGuids(once);
            Assert.That(guids, Has.Count.EqualTo(7), "夹具自证:7 条");
            for (int i = 0; i < guids.Count; i++)
            {
                Assert.That(guids[i], Is.EqualTo(FixtureVolumeHashes[i]),
                    $"第 {i} 条 guid 须 = 同名组 m_Volume 哈希(真参数落点)");
            }

            var distinct = new HashSet<string>(guids, StringComparer.Ordinal);
            Assert.That(distinct.Count, Is.EqualTo(7), "7 条两两互异(组哈希互异)");
            Assert.That(twice, Is.EqualTo(once), "幂等:已是真哈希不再改写");
        }

        /// <summary>新契约修复回归:<c>b500…</c> 合成条序值(旧事故形态,**非零也改写**)
        /// ⇒ 修复为真哈希(旧「非零原样保留」契约作废 —— 合成值正是哑 SetFloat 根因)。</summary>
        [Test]
        public void test_normalizeExposedGuids_synthesizedGuid_repairedToRealHash()
        {
            // Arrange:旧事故的 b5000000000000000000000000000001 形态
            string text = GroupsBlockText() +
                          ExposedBlockText("b5" + new string('0', 28) + "01");

            // Act
            string output = MixerAssetGenerator.NormalizeExposedGuids(text, new List<string>());

            // Assert
            List<string> guids = ExtractGuids(output);
            Assert.That(guids, Has.Count.EqualTo(7));
            for (int i = 0; i < guids.Count; i++)
                Assert.That(guids[i], Is.EqualTo(FixtureVolumeHashes[i]),
                    "合成 b500… 必须被修复为真哈希");
            Assert.That(output, Does.Not.Contain("b5" + new string('0', 28)),
                "合成前缀须全清(事故形态不得残留)");
        }

        /// <summary>新契约失败面:exposed 名查不到同名组 ⇒ LogError + **硬 throw**
        /// (合成条序路径退役;拒绝静默写假值 —— 本次事故的根因防线)。</summary>
        [Test]
        public void test_normalizeExposedGuids_unknownExposedName_throws()
        {
            // Arrange:7 组在场,但 exposed 名是 tier_*(该组不存在)
            string text = GroupsBlockText() +
                          "--- !u!241 &1000\n" +
                          "AudioMixerController:\n" +
                          "  m_ExposedParameters:\n" +
                          "  - guid: " + new string('0', 32) + "\n" +
                          "    name: tier_passband_center_hz\n";

            // Act + Assert:先 Expect 防线日志,再收 throw
            LogAssert.Expect(LogType.Error, new Regex("查不到同名组"));
            Assert.Throws<InvalidOperationException>(
                () => MixerAssetGenerator.NormalizeExposedGuids(text, new List<string>()));
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

        /// <summary>7 条暴露参数对应的组文档(暴露名 == 组名;各带互异 m_Volume 哈希 ——
        /// 真 guid 的夹具来源,新契约 NormalizeExposedGuids 的查表输入)。</summary>
        private static string GroupsBlockText()
        {
            string[] names = ExposedNames();
            string docs = "";
            for (int i = 0; i < names.Length; i++)
            {
                docs += "--- !u!243 &" + (2001 + i) + "\n" +
                        "AudioMixerGroupController:\n" +
                        "  m_Name: " + names[i] + "\n" +
                        "  m_Volume: " + FixtureVolumeHashes[i] + "\n";
            }

            return docs;
        }

        /// <summary>暴露名 7 员(与 <c>ExposedBlockText</c> 的条目顺序一致)。</summary>
        private static string[] ExposedNames() => new[]
        {
            "bus_volume_master", "bus_volume_music", "bus_volume_ambience",
            "bus_volume_voice", "bus_volume_sfx", "bus_volume_stethoscope",
            "bus_volume_uicue",
        };

        /// <summary>夹具真哈希(32 hex,7 条互异;仅测试用,非真实资产值)。</summary>
        private static readonly string[] FixtureVolumeHashes =
        {
            "aa01bb02cc03dd04ee05ff06aa07bb08",
            "aa11bb12cc13dd14ee15ff16aa17bb18",
            "aa21bb22cc23dd24ee25ff26aa27bb28",
            "aa31bb32cc33dd34ee35ff36aa37bb38",
            "aa41bb42cc43dd44ee45ff46aa47bb48",
            "aa51bb52cc53dd54ee55ff56aa57bb58",
            "aa61bb62cc63dd64ee65ff66aa67bb68",
        };

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
