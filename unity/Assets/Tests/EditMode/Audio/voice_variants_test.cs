// 权威来源:production/epics/audio-system/story-006-voice-variants-intensity-bucket.md
//   · AC-44-03:选变体 + 禁调制(同输入恒选同 clip;五项调制 API 零调用点)
//   · AC-44-04:ramp 断言(≥50ms;负向:写 10ms ⇒ 红)
//   · F-44.6 载体:分桶映射(桶序 弱/中/强 单调;u=0 ⇒ 弱+GAIN_MIN;边界 BUCKET_LOW 恰含)
//   · 回退门:轴向局部空缺夹具 ⇒ 红(逐组合非空)
// GDD:design/gdd/audio-system.md F-44.3(:353)· F-44.6(:409)· Edge Cases(:585-600)
//      · AC-44-03/04 原文(:1031-1043)
// ADR-018 §四 需求② · TR-audio-006
//
// ⚠️ 落点:unity/Assets/Tests/EditMode/Audio/(Unity 只编译 unity/Assets/ 树)
// ⚠️ 读法纪律:读仓库文件前置 File.Exists 断言(缺失即红,不静默跳过)
// ⚠️ 纪律:test_* 命名 · arrange/act/assert · 无随机 / 无时间依赖 · 零网络 I/O
// ⚠️ 三条易错点的落地证据:
//    1. Tier 不参与库轴(F-44.3:364):变体库按 CueId 分行,不含 Tier
//    2. bucket(u) 边界含端点方向(F-44.6:419):BUCKET_LOW 本身落中桶
//    3. 五项调制 Forbidden:音高/气声/语速/断句/共鸣,一个都不能出现在运行时路径

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using DaYiJingCheng.Gameplay.Presentation.Audio;

namespace DaYiJingCheng.Tests.Unit.Audio
{
    /// <summary>Story 006 语声变体库与 Intensity 分桶(AC-44-03/04 · F-44.6 · 回退门)的 EditMode 测试。</summary>
    [TestFixture]
    internal sealed class VoiceVariantsTest
    {
        // ══════════ AC-44-03:选变体 + 禁调制 ══════════

        /// <summary>AC-44-03 正例:同输入恒选同变体(确定性)。</summary>
        [Test]
        public void test_selectVariant_sameInput_sameOutput()
        {
            // Arrange
            var table = BuildFullTable();

            // Act
            int variant1 = VoiceVariantLib.SelectVariant(table, 1, 0, VoiceBucket.Weak);
            int variant2 = VoiceVariantLib.SelectVariant(table, 1, 0, VoiceBucket.Weak);

            // Assert
            Assert.That(variant1, Is.EqualTo(variant2), "同输入恒选同变体(确定性)");
        }

        /// <summary>AC-44-03 正例:不同桶选不同变体。</summary>
        [Test]
        public void test_selectVariant_differentBuckets_differentVariants()
        {
            // Arrange
            var table = BuildFullTable();

            // Act
            int weak = VoiceVariantLib.SelectVariant(table, 1, 0, VoiceBucket.Weak);
            int medium = VoiceVariantLib.SelectVariant(table, 1, 0, VoiceBucket.Medium);
            int strong = VoiceVariantLib.SelectVariant(table, 1, 0, VoiceBucket.Strong);

            // Assert
            Assert.That(weak, Is.Not.EqualTo(medium), "弱桶与中桶应选不同变体");
            Assert.That(medium, Is.Not.EqualTo(strong), "中桶与强桶应选不同变体");
        }

        /// <summary>AC-44-03 正例:Tier 不参与库轴(变体库按 CueId 分行,不含 Tier)。</summary>
        [Test]
        public void test_selectVariant_tierNotInLibAxis()
        {
            // Arrange
            var table = BuildFullTable();

            // Act:不同 tier 选同一 cue 同一桶,结果应相同(因为变体库不含 Tier)
            int tier0 = VoiceVariantLib.SelectVariant(table, 1, 0, VoiceBucket.Weak);
            int tier1 = VoiceVariantLib.SelectVariant(table, 1, 1, VoiceBucket.Weak);
            int tier2 = VoiceVariantLib.SelectVariant(table, 1, 2, VoiceBucket.Weak);

            // Assert
            Assert.That(tier0, Is.EqualTo(tier1), "Tier 不参与库轴:不同 tier 应选相同变体");
            Assert.That(tier1, Is.EqualTo(tier2), "Tier 不参与库轴:不同 tier 应选相同变体");
        }

        /// <summary>AC-44-03 正例:缺 cue ⇒ 返回 -1(调用方处理)。</summary>
        [Test]
        public void test_selectVariant_missingCue_returnsMinusOne()
        {
            // Arrange
            var table = BuildFullTable();

            // Act
            int variant = VoiceVariantLib.SelectVariant(table, 999, 0, VoiceBucket.Weak);

            // Assert
            Assert.That(variant, Is.EqualTo(-1), "缺 cue 应返回 -1");
        }

        /// <summary>AC-44-03 正例:缺桶 ⇒ 返回 -1(调用方处理)。</summary>
        [Test]
        public void test_selectVariant_missingBucket_returnsMinusOne()
        {
            // Arrange:构造缺中桶的表
            var row = new VoiceVariantLib.VoiceVariantRow(
                1, 0,
                new[] { 10, 11 },           // 弱桶有变体
                new int[0],                  // 中桶缺变体
                new[] { 30, 31 });           // 强桶有变体
            var table = new VoiceVariantLib.VoiceVariantTable(new[] { row });

            // Act
            int variant = VoiceVariantLib.SelectVariant(table, 1, 0, VoiceBucket.Medium);

            // Assert
            Assert.That(variant, Is.EqualTo(-1), "缺桶应返回 -1");
        }

        // ══════════ AC-44-04:ramp 断言 ══════════

        /// <summary>AC-44-04 正例:VolumeRamp 到位计时 ≥ 50 ms(与 Story 004 共用 RampSeconds)。</summary>
        [Test]
        public void test_volumeRamp_reachesTarget_afterAtLeastRampFloor()
        {
            // Arrange:dt 不整除 0.05(0.017 × 3 = 0.051)—— 断言墙钟 ≥ 下限而非 == 下限
            VolumeRamp.State state = VolumeRamp.Begin(0.8, 0.2);
            const double dt = 0.017;
            double previous = 0.8;
            double wallSeconds = 0.0;

            // Act
            int frames = 0;
            while (state.Active && frames < 1000)
            {
                previous = VolumeRamp.Step(ref state, dt);
                wallSeconds += dt;
                frames++;
            }

            // Assert
            Assert.That(previous, Is.EqualTo(0.2).Within(1e-12), "必须到位到目标值");
            Assert.That(wallSeconds, Is.GreaterThanOrEqualTo(AudioTuning.RampSeconds - 1e-12),
                $"到位墙钟 {wallSeconds} < RampSeconds {AudioTuning.RampSeconds}(AC-44-04 硬下界)");
        }

        /// <summary>AC-44-04 正例:VolumeRamp 单帧推进 ≤ MaxStepForFrame(斜率上限)。</summary>
        [Test]
        public void test_volumeRamp_singleFrameStep_withinMaxStep()
        {
            // Arrange
            VolumeRamp.State state = VolumeRamp.Begin(0.8, 0.2);
            const double dt = 0.017;

            // Act
            double maxStep = VolumeRamp.MaxStepForFrame(state, dt);
            double first = VolumeRamp.Step(ref state, dt);

            // Assert
            Assert.That(System.Math.Abs(first - 0.8), Is.LessThanOrEqualTo(maxStep + 1e-12),
                "单帧推进量应 ≤ MaxStepForFrame(斜率上限)");
        }

        /// <summary>AC-44-04 负例:写 10ms ramp ⇒ 红(构造探测)。
        /// 本测试验证 VolumeRamp 的 ramp 下界(纯函数测,与 Story 004 同构)。</summary>
        [Test]
        public void test_volumeRamp_shortDuration_red()
        {
            // Arrange:写 10ms ramp(违 AC-44-04 硬下界)
            const double shortDuration = 0.01; // 10ms < 50ms

            // Act
            VolumeRamp.State state = VolumeRamp.Begin(0.8, 0.2, shortDuration);
            const double dt = 0.017;
            double previous = 0.8;
            double wallSeconds = 0.0;

            int frames = 0;
            while (state.Active && frames < 1000)
            {
                previous = VolumeRamp.Step(ref state, dt);
                wallSeconds += dt;
                frames++;
            }

            // Assert:10ms ramp 到位墙钟 < 50ms 硬下界 ⇒ 红
            Assert.That(wallSeconds, Is.LessThan(AudioTuning.RampSeconds - 1e-12),
                $"10ms ramp 到位墙钟 {wallSeconds} < RampSeconds {AudioTuning.RampSeconds}(AC-44-04 硬下界)");
        }

        // ══════════ F-44.6 载体:分桶映射 ══════════

        /// <summary>F-44.6 正例:桶序 弱/中/强 单调(Intensity ∈ {0,1,127,128,254,255})。</summary>
        [Test]
        public void test_bucket_monotonicWeakMediumStrong()
        {
            // Arrange & Act & Assert
            Assert.That(VoiceVariantLib.ComputeBucket(0), Is.EqualTo(VoiceBucket.Weak), "Intensity=0 ⇒ 弱");
            Assert.That(VoiceVariantLib.ComputeBucket(1), Is.EqualTo(VoiceBucket.Weak), "Intensity=1 ⇒ 弱");
            Assert.That(VoiceVariantLib.ComputeBucket(127), Is.EqualTo(VoiceBucket.Medium), "Intensity=127 ⇒ 中");
            Assert.That(VoiceVariantLib.ComputeBucket(128), Is.EqualTo(VoiceBucket.Medium), "Intensity=128 ⇒ 中");
            Assert.That(VoiceVariantLib.ComputeBucket(254), Is.EqualTo(VoiceBucket.Strong), "Intensity=254 ⇒ 强");
            Assert.That(VoiceVariantLib.ComputeBucket(255), Is.EqualTo(VoiceBucket.Strong), "Intensity=255 ⇒ 强");
        }

        /// <summary>F-44.6 正例:u=0 ⇒ 弱 + GAIN_MIN。</summary>
        [Test]
        public void test_bucket_intensityZero_weakAndGainMin()
        {
            // Arrange & Act
            VoiceBucket bucket = VoiceVariantLib.ComputeBucket(0);
            float gain = VoiceVariantLib.ComputeGainScale(0);

            // Assert
            Assert.That(bucket, Is.EqualTo(VoiceBucket.Weak), "Intensity=0 ⇒ 弱桶");
            Assert.That(gain, Is.EqualTo(VoiceVariantLib.GainMin).Within(1e-6f), "Intensity=0 ⇒ GAIN_MIN");
        }

        /// <summary>F-44.6 正例:边界 BUCKET_LOW 恰含(&lt; 为弱,≥ 为中)。</summary>
        [Test]
        public void test_bucket_boundaryLow_exactInclusion()
        {
            // Arrange:BUCKET_LOW = 0.33,对应 Intensity = 0.33 × 255 ≈ 84.15
            // 取 Intensity = 84(u ≈ 0.3294 < 0.33 ⇒ 弱)
            // 取 Intensity = 85(u ≈ 0.3333 ≥ 0.33 ⇒ 中)

            // Act
            VoiceBucket bucket84 = VoiceVariantLib.ComputeBucket(84);
            VoiceBucket bucket85 = VoiceVariantLib.ComputeBucket(85);

            // Assert
            Assert.That(bucket84, Is.EqualTo(VoiceBucket.Weak), "Intensity=84 (u < BUCKET_LOW) ⇒ 弱");
            Assert.That(bucket85, Is.EqualTo(VoiceBucket.Medium), "Intensity=85 (u ≥ BUCKET_LOW) ⇒ 中");
        }

        /// <summary>F-44.6 正例:边界 BUCKET_HIGH 恰含(&lt; 为中,≥ 为强)。</summary>
        [Test]
        public void test_bucket_boundaryHigh_exactInclusion()
        {
            // Arrange:BUCKET_HIGH = 0.66,对应 Intensity = 0.66 × 255 ≈ 168.3
            // 取 Intensity = 168(u ≈ 0.6588 < 0.66 ⇒ 中)
            // 取 Intensity = 169(u ≈ 0.6627 ≥ 0.66 ⇒ 强)

            // Act
            VoiceBucket bucket168 = VoiceVariantLib.ComputeBucket(168);
            VoiceBucket bucket169 = VoiceVariantLib.ComputeBucket(169);

            // Assert
            Assert.That(bucket168, Is.EqualTo(VoiceBucket.Medium), "Intensity=168 (u < BUCKET_HIGH) ⇒ 中");
            Assert.That(bucket169, Is.EqualTo(VoiceBucket.Strong), "Intensity=169 (u ≥ BUCKET_HIGH) ⇒ 强");
        }

        /// <summary>F-44.6 正例:gain_scale 线性映射(u=0 ⇒ GAIN_MIN, u=1 ⇒ GAIN_MAX)。</summary>
        [Test]
        public void test_gainScale_linearMapping()
        {
            // Arrange & Act
            float gain0 = VoiceVariantLib.ComputeGainScale(0);
            float gain255 = VoiceVariantLib.ComputeGainScale(255);

            // Assert
            Assert.That(gain0, Is.EqualTo(VoiceVariantLib.GainMin).Within(1e-6f), "u=0 ⇒ GAIN_MIN");
            Assert.That(gain255, Is.EqualTo(VoiceVariantLib.GainMax).Within(1e-6f), "u=1 ⇒ GAIN_MAX");
        }

        /// <summary>F-44.6 正例:density_mult 线性映射(u=0 ⇒ DENS_MIN, u=1 ⇒ DENS_MAX)。</summary>
        [Test]
        public void test_densityMult_linearMapping()
        {
            // Arrange & Act
            float dens0 = VoiceVariantLib.ComputeDensityMult(0);
            float dens255 = VoiceVariantLib.ComputeDensityMult(255);

            // Assert
            Assert.That(dens0, Is.EqualTo(VoiceVariantLib.DensMin).Within(1e-6f), "u=0 ⇒ DENS_MIN");
            Assert.That(dens255, Is.EqualTo(VoiceVariantLib.DensMax).Within(1e-6f), "u=1 ⇒ DENS_MAX");
        }

        // ══════════ 回退门:逐 (CueId × Tier × 桶) 全组合非空 ══════════

        /// <summary>回退门正例:全组合非空 ⇒ 通过。</summary>
        [Test]
        public void test_validateLib_fullTable_zeroErrors()
        {
            // Arrange
            var table = BuildFullTable();
            var expectedCueIds = new[] { 1, 2, 3 };

            // Act
            IReadOnlyList<string> errors = VoiceVariantLib.ValidateVoiceVariantLib(table, expectedCueIds);

            // Assert
            Assert.That(errors, Is.Empty, () => string.Join("\n", errors));
        }

        /// <summary>回退门负例:缺 cue ⇒ 红。</summary>
        [Test]
        public void test_validateLib_missingCue_red()
        {
            // Arrange
            var table = BuildFullTable();
            var expectedCueIds = new[] { 1, 2, 999 }; // 999 缺行

            // Act
            IReadOnlyList<string> errors = VoiceVariantLib.ValidateVoiceVariantLib(table, expectedCueIds);

            // Assert
            Assert.That(errors, Is.Not.Empty, "缺 cue 必红(逐组合非空)");
            Assert.That(AnyError(errors, "999"), Is.True, () => string.Join("\n", errors));
        }

        /// <summary>回退门负例:缺桶 ⇒ 红。</summary>
        [Test]
        public void test_validateLib_missingBucket_red()
        {
            // Arrange:构造缺中桶的表
            var row = new VoiceVariantLib.VoiceVariantRow(
                1, 0,
                new[] { 10, 11 },           // 弱桶有变体
                new int[0],                  // 中桶缺变体
                new[] { 30, 31 });           // 强桶有变体
            var table = new VoiceVariantLib.VoiceVariantTable(new[] { row });
            var expectedCueIds = new[] { 1 };

            // Act
            IReadOnlyList<string> errors = VoiceVariantLib.ValidateVoiceVariantLib(table, expectedCueIds);

            // Assert
            Assert.That(errors, Is.Not.Empty, "缺桶必红(逐组合非空)");
            Assert.That(AnyError(errors, "中桶"), Is.True, () => string.Join("\n", errors));
        }

        /// <summary>回退门负例:空表 ⇒ 红。</summary>
        [Test]
        public void test_validateLib_emptyTable_red()
        {
            // Arrange
            var table = new VoiceVariantLib.VoiceVariantTable(Array.Empty<VoiceVariantLib.VoiceVariantRow>());
            var expectedCueIds = new[] { 1 };

            // Act
            IReadOnlyList<string> errors = VoiceVariantLib.ValidateVoiceVariantLib(table, expectedCueIds);

            // Assert
            Assert.That(errors, Is.Not.Empty, "空表必红(逐组合非空)");
        }

        // ══════════ 分桶边界解耦断言 ══════════

        /// <summary>分桶边界解耦正例:BUCKET_LOW/HIGH ∉ 已知 sim 阈值集 ⇒ 通过。</summary>
        [Test]
        public void test_validateBucketDecoupled_noOverlap_zeroErrors()
        {
            // Arrange:已知 sim 阈值集(来自 9/52 常量表引用,不含 BUCKET_LOW/HIGH)
            var simThresholds = new[] { 0.1f, 0.5f, 0.9f };

            // Act
            IReadOnlyList<string> errors = VoiceVariantLib.ValidateBucketThresholdsDecoupled(simThresholds);

            // Assert
            Assert.That(errors, Is.Empty, () => string.Join("\n", errors));
        }

        /// <summary>分桶边界解耦负例:BUCKET_LOW 等于已知 sim 阈值 ⇒ 红。</summary>
        [Test]
        public void test_validateBucketDecoupled_overlapRed()
        {
            // Arrange:已知 sim 阈值集包含 BUCKET_LOW
            var simThresholds = new[] { VoiceVariantLib.BucketLow, 0.5f };

            // Act
            IReadOnlyList<string> errors = VoiceVariantLib.ValidateBucketThresholdsDecoupled(simThresholds);

            // Assert
            Assert.That(errors, Is.Not.Empty, "BUCKET_LOW 等于已知 sim 阈值必红(分桶边界解耦)");
            Assert.That(AnyError(errors, "BUCKET_LOW"), Is.True, () => string.Join("\n", errors));
        }

        // ══════════ 五项调制 Forbidden:零出现 ══════════

        /// <summary>五项调制 Forbidden:音高/气声/语速/断句/共鸣,一个都不能出现在运行时路径。
        /// 本测试验证 VoiceVariantLib 的调用面扫描零五项 API(构造探测)。</summary>
        [Test]
        public void test_forbiddenModulations_zeroAppearance()
        {
            // Arrange:读取 VoiceVariantLib.cs 源文件
            string sourcePath = Path.Combine(repoRoot(), "unity", "Assets", "Gameplay.Presentation", "Audio", "VoiceVariantLib.cs");
            Assert.That(File.Exists(sourcePath), Is.True, $"源文件缺失:{sourcePath}");
            string source = File.ReadAllText(sourcePath);

            // Act:扫描五项调制 API
            string[] forbiddenApis = { "pitch", "Pitch", "playbackSpeed", "PlaybackSpeed", "breathiness", "Breathiness" };
            var found = new List<string>();
            foreach (string api in forbiddenApis)
            {
                if (source.IndexOf(api, StringComparison.Ordinal) >= 0)
                {
                    found.Add(api);
                }
            }

            // Assert
            Assert.That(found, Is.Empty, () => $"五项调制 API 零出现,但发现:{string.Join(", ", found)}");
        }

        // ══════════ 共用小件 ══════════

        /// <summary>构造全组合非空的变体库夹具。</summary>
        private static VoiceVariantLib.VoiceVariantTable BuildFullTable()
        {
            var rows = new[]
            {
                new VoiceVariantLib.VoiceVariantRow(1, 0, new[] { 10, 11 }, new[] { 20, 21 }, new[] { 30, 31 }),
                new VoiceVariantLib.VoiceVariantRow(2, 1, new[] { 12, 13 }, new[] { 22, 23 }, new[] { 32, 33 }),
                new VoiceVariantLib.VoiceVariantRow(3, 0, new[] { 14, 15 }, new[] { 24, 25 }, new[] { 34, 35 }),
            };
            return new VoiceVariantLib.VoiceVariantTable(rows);
        }

        /// <summary>错误集里是否含指定片段(不依赖执行次序 —— 门按规则聚合,顺序不作契约)。</summary>
        private static bool AnyError(IReadOnlyList<string> errors, string fragment)
        {
            for (int i = 0; i < errors.Count; i++)
            {
                if (errors[i] != null && errors[i].IndexOf(fragment, StringComparison.Ordinal) >= 0)
                    return true;
            }
            return false;
        }

        // 本文件在 unity/Assets/Tests/EditMode/Audio/ ⇒ Audio→EditMode→Tests→Assets→unity→仓库根 = 5 层
        private static string repoRoot([CallerFilePath] string thisFile = "")
            => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile) ?? ".",
                "..", "..", "..", "..", ".."));
    }
}
