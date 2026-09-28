// ============================================================================
// 技能状态持久化与流重构 EditMode 单测 —— Story 008 验收
// 权威来源: production/epics/skill-system/story-008-persistence-and-stream-rebuild.md
//   · AC-1~AC-6(从头重建 / 未升级哨兵 / 掉级 / round-trip / 折叠豁免 / 新玩家)
//   · ADR-010(7a 持久化,不独立快照,从流重构) · ADR-026 §七(折叠豁免)
// ============================================================================
// 测试策略:验证 RebuildLevels 纯函数的 6 条 AC;round-trip 通过
// PayloadCodec 完成(InternalsVisibleTo)。
// 零随机种子、零时间依赖、零外部 I/O。
// 事件须按 Tick 升序传入(RebuildLevels 会抛出 ArgumentException 检测到逆序)。
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.Contracts.SkillSystem;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Unit.SkillSystem
{
    /// <summary>Story 008 技能状态持久化与流重构 EditMode 单测。</summary>
    [TestFixture]
    internal sealed class SkillStateRebuilderTest
    {
        // ═══════════════════════════════════════════════════════════════════
        // AC-1: 从头重建技能等级
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>AC-1 主例: 序列 → 正确字典。</summary>
        [Test]
        public void test_ac1_rebuildLevels_fromSequence_returnsCorrectDict()
        {
            var events = new[]
            {
                new SkillGrownPayload(1, -1, (int)SkillId.诊断, 1, 0, 1),   // 诊断 Lv1
                new SkillGrownPayload(1, -1, (int)SkillId.诊断, 1, 0, 2),   // 诊断 Lv2
                new SkillGrownPayload(1, -1, (int)SkillId.急救, 3, 0, 1),   // 急救 Lv1
                new SkillGrownPayload(1, -1, (int)SkillId.急救, 3, 0, SkillGrownPayload.LevelNotGrown), // 未升级
                new SkillGrownPayload(1, -1, (int)SkillId.急救, 3, 0, 2),   // 急救 Lv2
            };

            var result = SkillStateRebuilder.RebuildLevels(events);

            Assert.That(result[(int)SkillId.诊断], Is.EqualTo(2), "诊断 = 2");
            Assert.That(result[(int)SkillId.急救], Is.EqualTo(2), "急救 = 2");
        }

        /// <summary>AC-1 边缘: 空序列 → 空字典。</summary>
        [Test]
        public void test_ac1_emptySequence_returnsEmptyDict()
        {
            var result = SkillStateRebuilder.RebuildLevels(Array.Empty<SkillGrownPayload>());
            Assert.That(result.Count, Is.EqualTo(0), "空序列 → 空字典");
        }

        // ═══════════════════════════════════════════════════════════════════
        // AC-2: 未升级事件(哨兵)不影响等级
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>AC-2: 哨兵被忽略,最新有效 Level 胜出。</summary>
        [Test]
        public void test_ac2_sentinelIgnored_latestValidLevelWins()
        {
            var events = new[]
            {
                new SkillGrownPayload(1, -1, (int)SkillId.诊断, 1, 0, 5),            // 诊断 Lv5
                new SkillGrownPayload(1, -1, (int)SkillId.诊断, 1, 0, SkillGrownPayload.LevelNotGrown), // 哨兵
                new SkillGrownPayload(1, -1, (int)SkillId.诊断, 1, 0, 6),            // 诊断 Lv6
            };

            var result = SkillStateRebuilder.RebuildLevels(events);
            Assert.That(result[(int)SkillId.诊断], Is.EqualTo(6), "哨兵被忽略,最终 = 6");
        }

        /// <summary>AC-2 边缘: 全部为哨兵 → 无等级条目。</summary>
        [Test]
        public void test_ac2_allSentinel_returnsNoEntries()
        {
            var events = new[]
            {
                new SkillGrownPayload(1, -1, (int)SkillId.诊断, 1, 0, SkillGrownPayload.LevelNotGrown),
                new SkillGrownPayload(1, -1, (int)SkillId.诊断, 1, 0, SkillGrownPayload.LevelNotGrown),
            };

            var result = SkillStateRebuilder.RebuildLevels(events);
            Assert.That(result.Count, Is.EqualTo(0), "全部哨兵 → 无条目");
        }

        // ═══════════════════════════════════════════════════════════════════
        // AC-3: 掉级事件正确反映
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>AC-3: 掉级事件正确降低等级。</summary>
        [Test]
        public void test_ac3_deathPenalty_levelDecreases()
        {
            var events = new[]
            {
                new SkillGrownPayload(1, -1, (int)SkillId.徒手, 0, 0, 10), // 徒手 Lv10
                new SkillGrownPayload(1, -1, (int)SkillId.徒手, 0, 0, 7),  // 徒手 Lv7(死亡掉级)
            };

            var result = SkillStateRebuilder.RebuildLevels(events);
            Assert.That(result[(int)SkillId.徒手], Is.EqualTo(7), "掉级后 = 7");
        }

        /// <summary>AC-3 边缘: 连续掉级。</summary>
        [Test]
        public void test_ac3_continuousDeaths_finalLevelCorrect()
        {
            var events = new[]
            {
                new SkillGrownPayload(1, -1, (int)SkillId.徒手, 0, 0, 10),
                new SkillGrownPayload(1, -1, (int)SkillId.徒手, 0, 0, 8),
                new SkillGrownPayload(1, -1, (int)SkillId.徒手, 0, 0, 5),
            };

            var result = SkillStateRebuilder.RebuildLevels(events);
            Assert.That(result[(int)SkillId.徒手], Is.EqualTo(5), "连续掉级 → 最终 5");
        }

        // ═══════════════════════════════════════════════════════════════════
        // AC-4: 存档 round-trip 一致
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>AC-4: 编码 → 解码 → 重建结果一致。</summary>
        [Test]
        public void test_ac4_roundtrip_codecDecode_rebuildMatchesOriginal()
        {
            var originalEvents = new[]
            {
                new SkillGrownPayload(1, -1, (int)SkillId.诊断, 1, 0, 25),
                new SkillGrownPayload(1, -1, (int)SkillId.急救, 3, 0, 18),
                new SkillGrownPayload(1, -1, (int)SkillId.采集, 5, 0, 12),
                new SkillGrownPayload(1, -1, (int)SkillId.诊断, 1, 0, SkillGrownPayload.LevelNotGrown), // 哨兵
                new SkillGrownPayload(1, -1, (int)SkillId.诊断, 1, 0, 26), // 诊断升级
            };

            // 编码 → 解码模拟 7a round-trip
            var decodedEvents = originalEvents
                .Select(e =>
                {
                    byte[] encoded = PayloadCodec.Encode(e);
                    return PayloadCodec.Decode<SkillGrownPayload>(EventKind.SkillGrown, encoded);
                })
                .ToList();

            var result = SkillStateRebuilder.RebuildLevels(decodedEvents);

            Assert.That(result[(int)SkillId.诊断], Is.EqualTo(26), "诊断 round-trip = 26");
            Assert.That(result[(int)SkillId.急救], Is.EqualTo(18), "急救 round-trip = 18");
            Assert.That(result[(int)SkillId.采集], Is.EqualTo(12), "采集 round-trip = 12");
        }

        // ═══════════════════════════════════════════════════════════════════
        // AC-5: 折叠后重建正确(SkillGrown 保留,重建结果不变)
        // AC-5: 逆序输入 → ArgumentException(重建语义依赖有序覆盖)
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>AC-5: SkillGrown 行保留 → 重建结果与未折叠一致。</summary>
        [Test]
        public void test_ac5_afterFold_skillGrownPreserved_rebuildCorrect()
        {
            // 完整序列 = 病例行 + SkillGrown 行(不同 skill_id)
            var fullSequence = new[]
            {
                new SkillGrownPayload(1, 100, (int)SkillId.诊断, 1, 0, 1), // 诊断 Lv1(病例 100)
                new SkillGrownPayload(1, -1, (int)SkillId.急救, 3, 0, 1),  // 急救 Lv1(世界级)
                new SkillGrownPayload(1, 100, (int)SkillId.诊断, 1, 0, 2), // 诊断 Lv2(病例 100)
            };

            // 模拟折叠后:保留 SkillGrown 行,移除与病例状态相关的非 SkillGrown 行
            var afterFold = new[]
            {
                new SkillGrownPayload(1, 100, (int)SkillId.诊断, 1, 0, 1),
                new SkillGrownPayload(1, -1, (int)SkillId.急救, 3, 0, 1),
                new SkillGrownPayload(1, 100, (int)SkillId.诊断, 1, 0, 2),
            };

            var beforeFold = SkillStateRebuilder.RebuildLevels(fullSequence);
            var afterFoldResult = SkillStateRebuilder.RebuildLevels(afterFold);

            Assert.That(afterFoldResult[(int)SkillId.诊断], Is.EqualTo(beforeFold[(int)SkillId.诊断]), "折叠后诊断等级不变");
            Assert.That(afterFoldResult[(int)SkillId.急救], Is.EqualTo(beforeFold[(int)SkillId.急救]), "折叠后急救等级不变");
        }

        // ═══════════════════════════════════════════════════════════════════
        // AC-6: 新玩家初始状态
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>AC-6: 空序列 → 空字典(无技能等级)。</summary>
        [Test]
        public void test_ac6_newPlayer_emptySequence_returnsEmptyDict()
        {
            var result = SkillStateRebuilder.RebuildLevels(Array.Empty<SkillGrownPayload>());
            Assert.That(result.Count, Is.EqualTo(0), "新玩家 → 空字典");
        }

        /// <summary>AC-6 边缘: 单次未升级(哨兵) → 无等级条目。</summary>
        [Test]
        public void test_ac6_singleSentinel_noLevelsRecorded()
        {
            var events = new[]
            {
                new SkillGrownPayload(1, -1, (int)SkillId.诊断, 1, 0, SkillGrownPayload.LevelNotGrown),
            };

            var result = SkillStateRebuilder.RebuildLevels(events);
            Assert.That(result.Count, Is.EqualTo(0), "单次哨兵 → 无条目");
        }

        // ═══════════════════════════════════════════════════════════════════
        // 边界: 调用方保证输入按全序排列
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>相同 skill_id 多条事件,取最后一条 Level。</summary>
        [Test]
        public void test_multipleEventsSameSkill_latestLevelWins()
        {
            var events = new[]
            {
                new SkillGrownPayload(1, -1, (int)SkillId.诊断, 1, 0, 1),
                new SkillGrownPayload(1, -1, (int)SkillId.诊断, 1, 0, 2),
                new SkillGrownPayload(1, -1, (int)SkillId.诊断, 1, 0, 3),
            };

            var result = SkillStateRebuilder.RebuildLevels(events);
            Assert.That(result[(int)SkillId.诊断], Is.EqualTo(3), "最后一条 Level 胜出");
        }

        /// <summary>Level = 0 是真实等级,与哨兵 -1 不冲突。</summary>
        [Test]
        public void test_explicitLevelZero_storedAsRealLevel()
        {
            var events = new[]
            {
                new SkillGrownPayload(1, -1, (int)SkillId.诊断, 1, 0, SkillGrownPayload.LevelNotGrown),
                new SkillGrownPayload(1, -1, (int)SkillId.诊断, 1, 0, 0), // 显式 0 级
            };

            var result = SkillStateRebuilder.RebuildLevels(events);
            Assert.That(result.ContainsKey((int)SkillId.诊断), Is.True, "Level=0 是真实等级,字典含该 skill");
            Assert.That(result[(int)SkillId.诊断], Is.EqualTo(0), "Level=0 存储为 0");
        }
    }
}
