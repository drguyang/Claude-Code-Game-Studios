// 权威来源:docs/tech-debt-register.md `TD-audio-034` ①(语声变体桶输入轴未接线)
//   · GDD `audio-system.md:377` `variant = SelectVariant(CueId, Tier, bucket(u))`,`u` = `AudioCueDto.Intensity`
//   · GDD `audio-system.md:441` `bucket = (u < BUCKET_LOW) ? 弱 : (u < BUCKET_HIGH) ? 中 : 强`
//   · 裁定:2026-10-29(本债 ① 轴接线轮)
//
// ⚠️ **本夹具钉两件事**:
//   ① 桶**由 `Intensity` 决定** —— 三个代表强度分别落到 弱 / 中 / 强(停用强度输入 ⇒ 恰红);
//   ② 桶**不再由身份决定** —— 同一强度下扫 64 个 identity,桶恒定
//     (把桶改回 `IdentityScalar(seed,3)` ⇒ 恰红)。② 是本次改判的**反向守卫**:
//     只钉 ① 不钉 ②,「桶仍来自身份但强度碰巧也能变」那种半吊子改法会逃逸。
//
// ⚠️ 测试纪律:arrange/act/assert · 无随机 · 无时间依赖 · 零文件系统 I/O。

using NUnit.Framework;
using DaYiJingCheng.Gameplay.Presentation.Audio;

namespace DaYiJingCheng.Tests.Unit.Audio
{
    /// <summary>`TD-audio-034` ①:变体桶的输入轴 = `AudioCueDto.Intensity`。</summary>
    [TestFixture]
    public sealed class IntensityBucketSelectionTest
    {
        /// <summary>12 条素材 ⇒ 每(性别 × 桶)格 2 条(`VariantSlotWidth` 的构建期门保证整除 2×3)。</summary>
        private const int Total = 12;

        private static int BucketOf(int intensity, int identity)
        {
            int size = VoiceVariantLib.VariantSlotWidth(Total);
            Assert.That(size, Is.EqualTo(2), "前置:12 条应给出每(性别 × 桶)格 2 条");
            // 布局 = gender 优先 ⇒ 桶须先剥掉性别段(3×size)
            int pos = VoiceVariantLib.SelectVariantCube(0, 0, identity, size, intensity);
            return pos % (VoiceVariantLib.BucketCount * size) / size;
        }

        [Test]
        public void test_bucket_comesFromIntensity()
        {
            // 三个代表强度(ComputeBucket 的阈值 = BucketLow .33 / BucketHigh .66 × 255)
            Assert.That(BucketOf(0, 7), Is.EqualTo(0), "intensity = 0 ⇒ 弱桶");
            Assert.That(BucketOf(128, 7), Is.EqualTo(1), "intensity = 128 ⇒ 中桶");
            Assert.That(BucketOf(255, 7), Is.EqualTo(2), "intensity = 255 ⇒ 强桶");
        }

        [Test]
        public void test_bucket_noLongerComesFromIdentity()
        {
            // 同一强度下身份扫 64 个:桶必须恒定 —— 否则桶仍在按身份分(旧语义)
            foreach (int intensity in new[] { 0, 128, 255 })
            {
                int expected = BucketOf(intensity, 0);
                for (int identity = 1; identity < 64; identity++)
                    Assert.That(BucketOf(intensity, identity), Is.EqualTo(expected),
                        $"intensity={intensity} identity={identity} 时桶变了 —— " +
                        "桶不该再来自身份(TD-audio-034 ① 的反向守卫)");
            }
        }

        [Test]
        public void test_withinBucket_stillVariesByIdentity()
        {
            // 身份**仍在**:同强度同桶下,不同身份应能取到桶内不同位置(否则变体库退化)
            int size = VoiceVariantLib.VariantSlotWidth(Total);
            var positions = new System.Collections.Generic.HashSet<int>();
            for (int identity = 0; identity < 64; identity++)
                positions.Add(VoiceVariantLib.SelectVariantCube(0, 0, identity, size, 255) % size);

            Assert.That(positions.Count, Is.GreaterThan(1),
                "桶内位置全同 —— 身份维掉了,同一强度的所有病人会听到同一条变体");
        }
        [Test]
        public void test_gender_comesFromIdentity_bothLayersReachable()
        {
            // 🔁 2026-10-29(`TD-audio-034` ② 的**直钉**夹具 —— 此前只有间接覆盖):
            //    性别层由身份最低位确定性派生,同一 intensity 下两性都必须被分到。
            int size = VoiceVariantLib.VariantSlotWidth(Total);
            int genderSpan = VoiceVariantLib.BucketCount * size;   // 3 × 2 = 6,即两性段的分界

            var seen = new System.Collections.Generic.HashSet<int>();
            for (int identity = 0; identity < 16; identity++)
            {
                int pos = VoiceVariantLib.SelectVariantCube(0, 0, identity, size, 128);
                int layer = pos / genderSpan;      // 0 = 男段,1 = 女段
                Assert.That(layer, Is.EqualTo(VoiceVariantLib.GenderOf(identity)),
                    $"identity={identity}:位置 {pos} 落在性别层 {layer},而 GenderOf 说 " +
                    $"{VoiceVariantLib.GenderOf(identity)} —— 性别层与派生值不一致");
                seen.Add(layer);
            }

            Assert.That(seen.Count, Is.EqualTo(VoiceVariantLib.GenderLayers),
                $"16 个身份只落到 {seen.Count} 个性别层 —— 另一性别的 6 条素材永不可达(死变体)");
        }

        [Test]
        public void test_gender_isStableForSameIdentity()
        {
            // 同一病人同一强度 ⇒ 恒定同一性(不随 cue 之外的任何东西抖动)
            int size = VoiceVariantLib.VariantSlotWidth(Total);
            for (int identity = 0; identity < 8; identity++)
            {
                int first = VoiceVariantLib.SelectVariantCube(0, 0, identity, size, 200);
                for (int repeat = 0; repeat < 4; repeat++)
                    Assert.That(VoiceVariantLib.SelectVariantCube(0, 0, identity, size, 200),
                        Is.EqualTo(first), $"identity={identity} 的选择不稳定 —— 违反确定性");
            }
        }

    }
}
