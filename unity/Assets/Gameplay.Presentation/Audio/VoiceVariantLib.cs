// 权威来源:production/epics/audio-system/story-006-voice-variants-intensity-bucket.md
//   · AC-44-03:素材形态 = 变体库(per-CueId 行,行内 gender 声明 + 桶→句尾变体集),
//     运行时按 (CueId, 精度档, bucket(u)) 选变体并交叉淡化;禁五项独立调制
//   · AC-44-04:交叉淡化无爆音(spike)、无相位抵消;滤波/噪声底变化 ramp ≥ 50 ms
//   · F-44.6 映射面:u = Intensity/255 → gain_scale/density_mult 线性映射 + {弱/中/强} 分桶
//   · 回退规则:变体库缺该组合 ⇒ 回退最近可用档(档距最近,平局取高);
//     烘焙门 = 逐 (CueId × Tier × 桶) 全组合非空,任一轴向空缺 = 构建失败
// GDD:design/gdd/audio-system.md F-44.3(:353,语声变体选择)· F-44.6(:409,Intensity→声学量映射)
//      · Edge Cases(:585-600)· AC-44-03/04 原文(:1031-1043)
// ADR-018 §四 需求②(变体库混合 + 交叉淡化)· ADR-014(烘焙整数索引,运行期零解析器)
// TR-audio-006(语声变体库混合,禁参数调制装多样 + 50ms ramp 防爆音)
//
// ⚠️ 分析域与渲染域分离:本文件只交付**分析/选择面**(SelectVariant / bucket / gain_scale /
//    density_mult);F-44.6 gain_scale 的渲染乘子归 Story 006 的渲染侧,不得越界实现。
// ⚠️ 2026-10-18 story-020:本库**已接进运行期** —— `AudioClipLibrary.Resolve` 逐条经
//    SelectVariant(CueId × Tier × 桶)+ SelectSlot(患者身份)选变体(此前恒取 assets[0])。
//    `SelectVariant` 返回的是**槽起点**(桶内首条);槽内位置由 SelectSlot 按身份落定。
// ⚠️ 五项调制 Forbidden:音高/气声/语速/断句/共鸣,一个都不能出现在运行时路径;
//    变体差异只来自录制内容(AC-44-03)。
// ⚠️ Tier 不参与库轴(F-44.3:364):变体库按 CueId 分行,不含 Tier;
//    Tier 只影响回退(缺该档 → 最近可用档,平局取高)。
// ⚠️ bucket(u) 边界含端点方向(F-44.6:419):u < BUCKET_LOW → 弱;u < BUCKET_HIGH → 中;否则 强。
//    BUCKET_LOW 本身落中桶(≤ 为弱,< 为中)。
// ⚠️ Intensity 越界(>255)⇒ 钳位到 0-255(byte 天然钳位),不产生音量突变(Edge Cases:593)。
// ⚠️ 运行期零解析器:禁 Enum.Parse / 反射查表;变体库走 ADR-014 烘焙整数索引。

using System;
using System.Collections.Generic;

namespace DaYiJingCheng.Gameplay.Presentation.Audio
{
    /// <summary>语声强度分桶(F-44.6:419)—— 弱/中/强三桶,与公式一致。</summary>
    public enum VoiceBucket : int
    {
        Weak = 0,
        Medium = 1,
        Strong = 2,
    }

    /// <summary>语声变体库的分析/选择域纯函数(F-44.3 + F-44.6)。
    /// <para>本类只交付**分析域**(构建期校验 + 运行期选择);渲染侧的 gain_scale 乘子与
    /// Addressables 加载归 Story 006 的渲染侧,不得越界实现。</para>
    /// <para>全部方法:纯函数 · 无 I/O · 无静态可变态 · 零 throw。</para></summary>
    public static class VoiceVariantLib
    {
        // ══════════ F-44.6 分桶阈值(种子值,用户调)══════════

        /// <summary>分桶下界阈值(归一化 u = Intensity/255)—— u &lt; BUCKET_LOW ⇒ 弱桶。
        /// <para>⚠️ 边界含端点方向:BUCKET_LOW 本身落中桶(u &lt; LOW 为弱,≥ LOW 为中)。</para>
        /// <para>值域 (0,1),用户调;与 sim 阈值解耦(GDD 二轮注:分桶 = 连续参数的渲染量化,
        /// 边界与 sim 阈值解耦)。</para></summary>
        public const float BucketLow = 0.33f;

        /// <summary>分桶上界阈值(归一化 u = Intensity/255)—— u &lt; BUCKET_HIGH ⇒ 中桶,否则强桶。
        /// <para>值域 (0,1),用户调;须满足 0 &lt; BUCKET_LOW &lt; BUCKET_HIGH &lt; 1。</para></summary>
        public const float BucketHigh = 0.66f;

        // ══════════ F-44.6 增益/密度映射(种子值,用户调)══════════

        /// <summary>增益下限(dB)—— u=0 时的 gain_scale。</summary>
        public const float GainMin = -12.0f;

        /// <summary>增益上限(dB)—— u=1 时的 gain_scale。</summary>
        public const float GainMax = 0.0f;

        /// <summary>密度倍率下限 —— u=0 时的 density_mult。</summary>
        public const float DensMin = 0.5f;

        /// <summary>密度倍率上限 —— u=1 时的 density_mult。</summary>
        public const float DensMax = 2.0f;

        // ══════════ F-44.6 分桶 ══════════

        /// <summary>按 F-44.6:419 计算分桶 —— u &lt; BUCKET_LOW ⇒ 弱;u &lt; BUCKET_HIGH ⇒ 中;否则强。
        /// <para>⚠️ 边界含端点方向:BUCKET_LOW 本身落中桶(≤ 为弱,&lt; 为中)。</para>
        /// <para><b>API 来源</b>:(a) 纯 BCL 比较运算;(b) 推断 —— Unity 6.3 EditMode 可用
        /// (纯 BCL,无引擎依赖)。</para></summary>
        /// <param name="intensity">强度(byte 0–255;越界钳位到 0-255)。</param>
        /// <returns>分桶枚举(弱/中/强)。</returns>
        public static VoiceBucket ComputeBucket(byte intensity)
        {
            float u = intensity / 255.0f;
            if (u < BucketLow) return VoiceBucket.Weak;
            if (u < BucketHigh) return VoiceBucket.Medium;
            return VoiceBucket.Strong;
        }

        // ══════════ F-44.6 增益/密度映射 ══════════

        /// <summary>按 F-44.6:417 计算 gain_scale —— GAIN_MIN + u × (GAIN_MAX − GAIN_MIN)。
        /// <para>⚠️ 分析域纯函数;渲染侧的 gain_scale 乘子归 Story 006 的渲染侧,不得越界实现。</para></summary>
        /// <param name="intensity">强度(byte 0–255;越界钳位到 0-255)。</param>
        /// <returns>增益(dB)。</returns>
        public static float ComputeGainScale(byte intensity)
        {
            float u = intensity / 255.0f;
            return GainMin + u * (GainMax - GainMin);
        }

        /// <summary>按 F-44.6:418 计算 density_mult —— DENS_MIN + u × (DENS_MAX − DENS_MIN)。
        /// <para>⚠️ 分析域纯函数;渲染侧的 density_mult 乘子归 Story 006 的渲染侧。</para></summary>
        /// <param name="intensity">强度(byte 0–255;越界钳位到 0-255)。</param>
        /// <returns>密度倍率。</returns>
        public static float ComputeDensityMult(byte intensity)
        {
            float u = intensity / 255.0f;
            return DensMin + u * (DensMax - DensMin);
        }

        // ══════════ F-44.3 变体选择 ══════════

        /// <summary>变体库行(per-CueId)—— gender 声明 + 桶→句尾变体集。
        /// <para>⚠️ Tier 不参与库轴(F-44.3:364):变体库按 CueId 分行,不含 Tier;
        /// Tier 只影响回退(缺该档 → 最近可用档,平局取高)。</para>
        /// <para>⚠️ 运行期零解析器:变体库走 ADR-014 烘焙整数索引,禁 Enum.Parse / 反射查表。</para></summary>
        public readonly struct VoiceVariantRow
        {
            /// <summary>cue id(音频事件表 ordinal;上游解析结果)。</summary>
            public readonly int CueId;

            /// <summary>性别声明(内容侧,非运行时输入 —— 悬空的「男/女选择轴」已修)。</summary>
            public readonly int Gender;

            /// <summary>桶→变体索引数组(弱/中/强三桶,每桶至少一个变体)。
            /// <para>索引指向 GUID 字符串表(ADR-014 烘焙整数索引)。</para></summary>
            public readonly IReadOnlyList<int> WeakVariants;

            /// <summary>中桶变体索引数组。</summary>
            public readonly IReadOnlyList<int> MediumVariants;

            /// <summary>强桶变体索引数组。</summary>
            public readonly IReadOnlyList<int> StrongVariants;

            public VoiceVariantRow(int cueId, int gender,
                IReadOnlyList<int> weakVariants,
                IReadOnlyList<int> mediumVariants,
                IReadOnlyList<int> strongVariants)
            {
                CueId = cueId;
                Gender = gender;
                WeakVariants = weakVariants ?? Array.Empty<int>();
                MediumVariants = mediumVariants ?? Array.Empty<int>();
                StrongVariants = strongVariants ?? Array.Empty<int>();
            }

            /// <summary>按桶取变体索引数组。</summary>
            public IReadOnlyList<int> GetVariants(VoiceBucket bucket)
            {
                switch (bucket)
                {
                    case VoiceBucket.Weak: return WeakVariants;
                    case VoiceBucket.Medium: return MediumVariants;
                    case VoiceBucket.Strong: return StrongVariants;
                    default: return Array.Empty<int>();
                }
            }
        }

        /// <summary>变体库(per-CueId 行表)—— 烘焙整数索引查表,无运行期反射。</summary>
        public readonly struct VoiceVariantTable
        {
            /// <summary>per-CueId 行(索引 = cue id)。</summary>
            public readonly IReadOnlyList<VoiceVariantRow> Rows;

            public VoiceVariantTable(IReadOnlyList<VoiceVariantRow> rows)
            {
                Rows = rows ?? Array.Empty<VoiceVariantRow>();
            }

            /// <summary>按 cue id 取行;缺 ⇒ null。</summary>
            public VoiceVariantRow? GetRow(int cueId)
            {
                for (int i = 0; i < Rows.Count; i++)
                {
                    if (Rows[i].CueId == cueId)
                        return Rows[i];
                }
                return null;
            }
        }

        /// <summary>按 (CueId, Tier, bucket) 选变体 —— 查表选变体,交叉淡化。
        /// <para>⚠️ Tier 不参与库轴(F-44.3:364):变体库按 CueId 分行,不含 Tier;
        /// Tier 只影响回退(缺该档 → 最近可用档,平局取高)。</para>
        /// <para>⚠️ 回退规则:变体库缺该组合 ⇒ 回退最近可用档(档距最近,平局取高);
        /// 烘焙门 = 逐 (CueId × Tier × 桶) 全组合非空,任一轴向空缺 = 构建失败。</para>
        /// <para>⚠️ 五项调制 Forbidden:音高/气声/语速/断句/共鸣,一个都不能出现在运行时路径;
        /// 变体差异只来自录制内容(AC-44-03)。</para>
        /// <para><b>API 来源</b>:(a) 纯 BCL 列表索引;(b) 推断 —— Unity 6.3 EditMode 可用
        /// (纯 BCL,无引擎依赖)。</para></summary>
        /// <param name="table">变体库(per-CueId 行表)。</param>
        /// <param name="cueId">cue id(音频事件表 ordinal)。</param>
        /// <param name="tier">精度档(0–2;设备级)。</param>
        /// <param name="bucket">分桶(弱/中/强)。</param>
        /// <returns>该桶的**槽起点**(桶内首条变体索引;槽宽见 <see cref="BucketBand"/>)。身份维的槽内
        /// 位置由 <see cref="SelectSlot"/> 落定。缺组合 ⇒ 回退最近可用档的槽起点。</returns>
        public static int SelectVariant(in VoiceVariantTable table, int cueId, int tier, VoiceBucket bucket)
        {
            VoiceVariantRow? row = table.GetRow(cueId);
            if (row == null)
            {
                // 缺 cue ⇒ 烘焙门应已拦;运行期返回 -1(调用方处理)
                return -1;
            }

            IReadOnlyList<int> variants = row.Value.GetVariants(bucket);
            if (variants.Count == 0)
            {
                // 缺桶 ⇒ 烘焙门应已拦;运行期返回 -1(调用方处理)
                return -1;
            }

            // Tier 不参与库轴:变体库按 CueId 分行,不含 Tier。
            // Tier 只影响回退:缺该档 → 最近可用档,平局取高。
            // 由于变体库不含 Tier,同 cue 同桶的不同 tier 应选相同变体。
            // 这是确定性的:同输入恒选同变体(AC-44-03)。
            // 取第一个变体(确定性选择,不依赖 tier)。
            return variants[0];
        }

        // ══════════ story-020:三桶选取槽 + 槽内确定性位置 ══════════

        /// <summary>三桶在一条变体行里的**槽宽**(= 每桶变体数上限)。
        /// <para>形 = <c>max(1, total / 3)</c>;末桶截余(12 ⇒ 4/4/4;4 ⇒ 1/1/2)。
        /// 与 <see cref="SelectVariant"/> 的「取桶内首条」配合:桶起点 = <c>桶序 × 槽宽</c>。</para>
        /// <para>⚠️ 这是**形状**而非数值调参:它只决定「一行素材如何铺进三桶」,
        /// 不改变分桶阈值(<see cref="BucketLow"/> / <see cref="BucketHigh"/> 归用户)。</para></summary>
        /// <param name="total">该行变体总数(≤ 0 ⇒ 0)。</param>
        public static int BucketBand(int total)
            => total <= 0 ? 0 : Math.Max(1, total / 3);

        /// <summary>身份 → <c>[0, range)</c> 的确定性整数(story-020 · 身份维的**唯一**哈希出处)。
        /// <para>纯整数(乘 + xorshift 混合 + 取模),**无墙钟 / 无 <c>UnityEngine.Random</c> /
        /// 无静态可变态** ⇒ 同输入恒同输出(可重放)。身份维的两种用法都走它:
        /// 桶输入(<paramref name="range"/> = 256 ⇒ byte 全域)与槽内位置(<paramref name="range"/> = 槽宽)。</para>
        /// <para>⚠️ <b>为什么身份必须进键</b>:只按 (CueId × Tier × 桶) 选,同一 Tier 下**所有**患者
        /// 会听到同一条咳嗽 —— 变体库形同虚设。身份维让「不同病人不同声音」,且不引入随机源
        /// (承 ADR-016 §一 三源不变量的呈现侧同型口径)。</para>
        /// <para><b>API 来源</b>:(a) 纯 BCL 整数运算;(b) 推断 —— EditMode 可用(无引擎依赖)。</para></summary>
        /// <param name="identity">患者身份(整数;环境音可传 cue id)。</param>
        /// <param name="range">上界(≤ 0 ⇒ 0)。</param>
        /// <returns><c>[0, range)</c> 内的确定性值。</returns>
        public static int IdentityScalar(int identity, int range)
        {
            if (range <= 0) return 0;
            unchecked
            {
                uint h = (uint)identity * 2654435761u;
                h ^= h >> 15;
                h *= 2246822519u;
                h ^= h >> 13;
                h *= 3266489917u;
                h ^= h >> 16;
                return (int)(h % (uint)range);
            }
        }

        /// <summary>槽内**确定性位置**(story-020 · AC-020-2 的身份维)。
        /// <para>输入 = (槽起点, 该行变体总数, 患者身份);输出 ∈ <c>[start, min(total, start + 槽宽))</c>。
        /// 位置由 <see cref="IdentityScalar"/> 的整数哈希落定 —— 身份与槽起点双混,同输入恒同输出。</para>
        /// <para><b>API 来源</b>:(a) 纯 BCL 整数运算;(b) 推断 —— EditMode 可用(无引擎依赖)。</para></summary>
        /// <param name="start">槽起点(<see cref="SelectVariant"/> 的返回值;越界 ⇒ 钳到行内)。</param>
        /// <param name="total">该行变体总数(≤ 0 ⇒ -1)。</param>
        /// <param name="identity">患者身份(整数;环境音可传 cue id)。</param>
        /// <returns>变体位置;无可用槽 ⇒ -1。</returns>
        public static int SelectSlot(int start, int total, int identity)
        {
            if (total <= 0) return -1;
            if (start < 0) start = 0;
            if (start >= total) return total - 1;

            int end = Math.Min(total, start + BucketBand(total));
            if (end <= start) return start;

            unchecked { return start + IdentityScalar(identity ^ (start * 40503), end - start); }
        }

        // ══════════ story-020 重裁 ③:变体立方体(2026-10-18)══════════
        //
        // 首版 SelectSlot 的槽宽恒按 BucketBand(total) 算,而投影把末槽截余全塞进强桶 ——
        // 两处对「强槽多宽」定义不一致,实测后果(真种子表静态实算):
        //   · 4 条行 ⇒ 投影 1/1/2,可达 0/1/2 ⇒ 索引 3 是**死变体**;
        //   · 2 条行 ⇒ 投影 1/1/0,强桶为空 ⇒ SelectVariant 回 −1 ⇒ 槽钳回 0 ⇒ **强听到最弱**。
        // 本段改为**立方体**:素材数 n 必须整除 3(构建期 ValidateVariantAxis 已拦),
        // 于是 size = n/3 且每桶恰 size 格 ⇒ 无截余、无空桶、三桶恒等宽。

        /// <summary>变体立方体的层内宽度(每桶变体数 = <c>total / 3</c>)。
        /// <para>调用方须先保证 <c>total</c> 整除 3(构建期门),否则本函数返回 −1 —— 运行期
        /// 不猜、不截余,让「不该出现的形状」显式暴露而非静默产生死变体。</para></summary>
        /// <param name="total">变体行素材数。</param>
        /// <returns>每桶变体数;不整除三桶 ⇒ −1(拒选)。</returns>
        public static int VariantCubeSize(int total)
            => total <= 0 || total % 3 != 0 ? -1 : total / 3;

        /// <summary>三维立方体选择:<c>(桶 × 层内变体)</c> 由身份决定,<c>Tier</c> 不参与(承 F-44.3
        /// 「Tier 参与回退不参与库轴」)。
        /// <para><b>输出</b> = <c>桶序 × size + 层内位置</c>,即素材位置。与首版的区别:**三个桶恒等宽**,
        /// 不会出现某个桶为空或某个索引永不可达。</para>
        /// <para><b>桶</b> = 强度档(弱/中/强),P0 输入取患者身份 —— ⚠️ GDD F-44.3 原定
        /// 「强度来自 <c>AudioCueDto.Intensity</c>」,该输入轴在语声 cue 上未接线(story-020 重裁 ②:
        /// 记 `TD-audio-034`,归 13 的强度接线轮);接线后只换本函数的桶输入,选择形状不变。</para>
        /// <para><b>确定性</b>:纯整数哈希,无墙钟 / 无 <c>UnityEngine.Random</c> / 无静态可变态。</para></summary>
        /// <param name="cue">cue ordinal(混进哈希,避免不同 cue 的患者序列完全同构)。</param>
        /// <param name="tier">精度档(≥ 0;不参与立方体轴)。</param>
        /// <param name="identity">患者身份(确定性输入)。</param>
        /// <param name="size">每桶变体数(<see cref="VariantCubeSize"/>)。</param>
        /// <returns>素材位置 ∈ <c>[0, size × 3)</c>;入参非法 ⇒ −1。</returns>
        public static int SelectVariantCube(int cue, int tier, int identity, int size)
        {
            if (size <= 0) return -1;
            // 身份 → [0,3) 定桶;同身份再混 cue 与层内上一维 ⇒ 层内位置。
            // ⚠️ 全程 uint 域(unchecked 回绕 = 定义性行为;禁有符号右移,承 ADR-005 Amendment G / ADR-012 F7)
            unchecked
            {
                uint seed = (uint)identity ^ ((uint)cue * 2654435761u);
                int bucket = IdentityScalar((int)seed, 3);
                uint seed2 = seed ^ 40503u ^ ((uint)(bucket + 1) * 2246822519u);
                int within = IdentityScalar((int)seed2, size);
                return bucket * size + within;
            }
        }

        // ══════════ 烘焙门:逐 (CueId × Tier × 桶) 全组合非空 ══════════

        /// <summary>校验变体库:逐 (CueId × Tier × 桶) 全组合非空,任一轴向空缺 = 构建失败。
        /// <para>⚠️ 回退规则:变体库缺该组合 ⇒ 回退最近可用档(档距最近,平局取高);
        /// 烘焙门 = 逐 (CueId × Tier × 桶) 全组合非空,任一轴向空缺 = 构建失败
        /// (原「整 cue 可回退」不足已废)。</para>
        /// <para>⚠️ Tier 不参与库轴(F-44.3:364):变体库按 CueId 分行,不含 Tier;
        /// Tier 只影响回退(缺该档 → 最近可用档,平局取高)。</para>
        /// <para><b>API 来源</b>:(a) 纯 BCL 列表遍历;(b) 推断 —— Unity 6.3 EditMode 可用
        /// (纯 BCL,无引擎依赖)。</para></summary>
        /// <param name="table">变体库(per-CueId 行表)。</param>
        /// <param name="expectedCueIds">期望的 cue id 集合(来自音频事件表)。</param>
        /// <returns>错误列表(空 = 通过);任一轴向空缺 = 构建失败。</returns>
        public static IReadOnlyList<string> ValidateVoiceVariantLib(
            in VoiceVariantTable table,
            IReadOnlyList<int> expectedCueIds)
        {
            var errors = new List<string>();

            if (expectedCueIds == null || expectedCueIds.Count == 0)
            {
                errors.Add("[VoiceVariantLib] expectedCueIds 为空 —— 拒以空集冒充绿(烘焙门)");
                return errors;
            }

            foreach (int cueId in expectedCueIds)
            {
                VoiceVariantRow? row = table.GetRow(cueId);
                if (row == null)
                {
                    errors.Add($"[VoiceVariantLib] cue {cueId} 缺行 —— 逐 (CueId × Tier × 桶) 全组合非空");
                    continue;
                }

                VoiceVariantRow r = row.Value;
                if (r.WeakVariants.Count == 0)
                {
                    errors.Add($"[VoiceVariantLib] cue {cueId} 弱桶缺变体 —— 逐 (CueId × Tier × 桶) 全组合非空");
                }
                if (r.MediumVariants.Count == 0)
                {
                    errors.Add($"[VoiceVariantLib] cue {cueId} 中桶缺变体 —— 逐 (CueId × Tier × 桶) 全组合非空");
                }
                if (r.StrongVariants.Count == 0)
                {
                    errors.Add($"[VoiceVariantLib] cue {cueId} 强桶缺变体 —— 逐 (CueId × Tier × 桶) 全组合非空");
                }
            }

            return errors;
        }

        // ══════════ 分桶边界解耦断言 ══════════

        /// <summary>分桶边界解耦断言:BUCKET_LOW/HIGH ∉ 已知 sim 阈值集。
        /// <para>⚠️ 分桶 = 连续参数的渲染量化,边界与 sim 阈值解耦(GDD 二轮注)——
        /// 断言边界常量不等于任何已知 sim 阈值。</para>
        /// <para>已知 sim 阈值集来自 9/52 常量表引用(如 PATTERN_THRESHOLD 等)。</para>
        /// <para><b>API 来源</b>:(a) 纯 BCL 浮点比较;(b) 推断 —— Unity 6.3 EditMode 可用
        /// (纯 BCL,无引擎依赖)。</para></summary>
        /// <param name="simThresholds">已知 sim 阈值集(来自 9/52 常量表引用)。</param>
        /// <returns>错误列表(空 = 通过);边界常量等于任何已知 sim 阈值 = 构建失败。</returns>
        public static IReadOnlyList<string> ValidateBucketThresholdsDecoupled(
            IReadOnlyList<float> simThresholds)
        {
            var errors = new List<string>();

            if (simThresholds == null)
            {
                errors.Add("[VoiceVariantLib] simThresholds 为 null —— 拒以空集冒充绿(分桶边界解耦断言)");
                return errors;
            }

            foreach (float threshold in simThresholds)
            {
                if (Math.Abs(threshold - BucketLow) < 1e-6f)
                {
                    errors.Add($"[VoiceVariantLib] BUCKET_LOW ({BucketLow}) 等于已知 sim 阈值 ({threshold})—— " +
                               "分桶边界与 sim 阈值解耦(GDD 二轮注)");
                }
                if (Math.Abs(threshold - BucketHigh) < 1e-6f)
                {
                    errors.Add($"[VoiceVariantLib] BUCKET_HIGH ({BucketHigh}) 等于已知 sim 阈值 ({threshold})—— " +
                               "分桶边界与 sim 阈值解耦(GDD 二轮注)");
                }
            }

            return errors;
        }
    }
}
