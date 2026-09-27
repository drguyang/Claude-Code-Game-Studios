// 权威来源:production/epics/audio-system/story-005-snr-analysis-noise-floor.md
//   · AC-44-05 ①:ComputeSnrDb 纯函数按 F-44.2(dB 线性化相减,钳位 ±24 dB)
//   · AC-44-06:noise_floor_db/contact_noise_db 均有限且 ≥ NOISE_FLOOR_DB_MIN;∀T: SNR ≥ −24 dB
// GDD:design/gdd/audio-system.md F-44.2(:318,2026-09-18 量纲修复版) · F-44.1(:278,六列)
//      · AC-44-05/06 原文(:1031-1043)· :331-332(噪声永不为零,dB 侧正确表述)
// ADR-018 §四 需求③(Stethoscope 总线暴露接触噪声底与信噪比)
// TR-audio-005(接触噪声/信噪比是暴露参数)
//
// ⚠️ 分析域与渲染域分离(GDD F-44.2 末注):本文件只实现**分析域**(构建期门);
//    F-44.6 gain_scale 的渲染乘子归 Story 006,两域不得互写。
// ⚠️ F-44.2 公式逐字实现(2026-09-18 修订版):
//    SNR_dB = SIGNAL_dB − 10·log10( 10^(NOISE_FLOOR_dB/10) + 10^(CONTACT_NOISE_dB/10) )
//    原式 SIGNAL_GAIN / (NOISE_FLOOR + CONTACT_NOISE_floor) 已废(dB 是対数量,dB/dB 无意义)。
// ⚠️ 分母真数恒正:10^(n/10) > 0 且 10^(c/10) > 0 对任意有限 n/c 成立;
//    两者同为 −∞ 时分母 = 0,log10(0) = −∞,SNR = +∞,钳位到 +24 —— 无除零路径。
// ⚠️ NaN 输入:任一输入为 NaN ⇒ 输出 NaN ⇒ 由 ValidateTierMapSnr 拒收(非法输入 ⇒ 拒)。

using System;
using System.Collections.Generic;
using System.Globalization;

namespace DaYiJingCheng.Gameplay.Presentation.Audio
{
    /// <summary>SNR 分析域纯函数(F-44.2)+ tier_map 构建期校验门(AC-44-05/06)。
    /// <para>本类只交付**分析域**(构建期断言);F-44.6 <c>gain_scale</c> 渲染乘子归 Story 006。</para>
    /// <para>全部方法:纯函数 · 无 I/O · 无静态可变态 · 零 throw(NaN 输入返回 NaN,由调用方拒收)。</para></summary>
    public static class SnrAnalysis
    {
        /// <summary>SNR 输出钳位下界(GDD F-44.2:钳位 −24 … +24 dB)。</summary>
        public const float SnrClampMin = -24.0f;

        /// <summary>SNR 输出钳位上界(GDD F-44.2:钳位 −24 … +24 dB)。</summary>
        public const float SnrClampMax = 24.0f;

        /// <summary>噪声底的**有限下界**(dB 侧)—— AC-44-06 ①:背景噪声永不为零的量化形态。
        /// <para>⚠️ 语义反警示(GDD :331-332):这是**线性幅度 &gt; 0 的 dB 侧形态</c>,
        /// **不是**「dB 值 ≠ 0」(dB = 0 是最响)。值域归用户调(本类只载常量)。</para>
        /// <para>与 8 侧 <c>READ_FLOOR_MIN &gt; 0</c> 方向对齐(两者均断言噪声线性幅度 &gt; 0)。</para></summary>
        public const float NoiseFloorDbMin = -60.0f;

        // ══════════ AC-44-05 ①:ComputeSnrDb 纯函数 ══════════

        /// <summary>按 F-44.2 计算 SNR(dB)—— 两个噪声源**先线性化再相加**,信号与合并噪声**相减**。
        /// <para>公式(2026-09-18 量纲修复版):
        /// <c>SNR_dB = SIGNAL_dB − 10·log10( 10^(NOISE_FLOOR_dB/10) + 10^(CONTACT_NOISE_dB/10) )</c></para>
        /// <para>钳位在**派生值**上(先算后钳)。NaN 输入 ⇒ NaN 输出(由 <see cref="ValidateTierMapSnr"/> 拒收)。</para>
        /// <para>⚠️ 分母真数恒正:10^(n/10) + 10^(c/10) &gt; 0 对任意有限 n/c 成立;
        /// 两者同为 −∞ 时分母 = 0 ⇒ log10(0) = −∞ ⇒ SNR = +∞ ⇒ 钳位 +24 —— **无除零路径**。</para>
        /// <para><b>API 来源</b>:(a) <c>System.Math.Log10</c> / <c>Math.Pow</c> 为 BCL 长期稳定 API;
        /// (b) 推断 —— Unity 6.3 EditMode 可用(纯 BCL,无引擎依赖)。</para></summary>
        /// <param name="signalDb">信号电平(dB),直取 <c>TierMap[T].signal_db</c>。</param>
        /// <param name="noiseFloorDb">背景噪声底(dB),直取 <c>TierMap[T].noise_floor_db</c>。</param>
        /// <param name="contactNoiseDb">接触噪声底(dB),直取 <c>TierMap[T].contact_noise_floor_db</c>。</param>
        /// <returns>SNR(dB),钳位到 [−24, +24];NaN 输入 ⇒ NaN。</returns>
        public static float ComputeSnrDb(float signalDb, float noiseFloorDb, float contactNoiseDb)
        {
            // 线性化:10^(dB/10) 得线性幅度
            float noiseLinear = (float)Math.Pow(10.0, noiseFloorDb / 10.0);
            float contactLinear = (float)Math.Pow(10.0, contactNoiseDb / 10.0);

            // 合并噪声(线性相加)
            float combinedLinear = noiseLinear + contactLinear;

            // 相减:SIGNAL_dB − 10·log10(合并线性噪声)
            // ⚠️ combinedLinear = 0 仅在两噪声同为 −∞ 时发生;Math.Log10(0) = −∞(BCL 语义,不抛异常)
            float snrDb = signalDb - (float)(10.0 * Math.Log10(combinedLinear));

            // 钳位(派生值上)
            if (snrDb < SnrClampMin) return SnrClampMin;
            if (snrDb > SnrClampMax) return SnrClampMax;
            return snrDb;
        }

        // ══════════ AC-44-06 ①:噪声底下界校验 ══════════

        /// <summary>检查单个噪声底值是否**有限且 ≥ <see cref="NoiseFloorDbMin"/>**(AC-44-06 ①)。
        /// <para>NaN / ±∞ / 低于下界 ⇒ false。</para></summary>
        public static bool IsNoiseFloorValid(float noiseFloorDb)
        {
            if (float.IsNaN(noiseFloorDb) || float.IsInfinity(noiseFloorDb)) return false;
            return noiseFloorDb >= NoiseFloorDbMin;
        }

        // ══════════ AC-44-05 ① + AC-44-06 ②:tier_map SNR 行校验 ══════════

        /// <summary>校验 <c>tier_map</c> 全部行的 SNR 三输入(AC-44-05 ① + AC-44-06 ②)。
        /// <para>三输入**直取列</c>(<c>signal_db</c> / <c>noise_floor_db</c> / <c>contact_noise_floor_db</c>);
        /// 列缺失由 Story 002 <c>ValidateTierMap</c> 拦(schema 门),本函数不重复拦。</para>
        /// <para>校验项:① 两噪声列均有限且 ≥ <see cref="NoiseFloorDbMin"/>(AC-44-06 ①);
        /// ② 三列均可解析为 float(NaN/±∞ ⇒ 拒);③ ∀T: <see cref="ComputeSnrDb"/>(行) ∈ [−24, +24](AC-44-05 ①)。</para>
        /// <para><b>注意</b>:③ 的钳位已在 <see cref="ComputeSnrDb"/> 内实现,故输出恒 ∈ [−24, +24];
        /// 此处的显式断言作为**双保险**(防钳位实现漂移)。</para></summary>
        /// <param name="tierMap">设备级滤波表(档位键 → 列名 → 原文);null / 缺列 = NOT-RUN 守卫。</param>
        /// <returns>错误列表(空 = 通过);NOT-RUN 守卫错误与校验失败均可区分。</returns>
        public static IReadOnlyList<string> ValidateTierMapSnr(
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> tierMap)
        {
            if (tierMap == null)
            {
                return new[]
                {
                    "[NOT-RUN 守卫] tier_map 字段缺失(null)—— SNR 校验不可执行;" +
                    "schema 门(Story 002)已拦,此处不重复报",
                };
            }

            var errors = new List<string>();
            string[] noiseColumns = { "noise_floor_db", "contact_noise_floor_db" };

            foreach (KeyValuePair<string, IReadOnlyDictionary<string, string>> tier in tierMap)
            {
                string tierKey = tier.Key;
                IReadOnlyDictionary<string, string> columns = tier.Value;
                if (columns == null)
                {
                    errors.Add($"[SNR] tier_map[\"{tierKey}\"] 缺列对象体 —— SNR 校验不可执行");
                    continue;
                }

                // ① 两噪声列:有限且 ≥ NOISE_FLOOR_DB_MIN(AC-44-06 ①)
                foreach (string col in noiseColumns)
                {
                    if (!columns.TryGetValue(col, out string raw) || raw == null)
                    {
                        // 列缺失由 Story 002 ValidateTierMap 拦;此处 NOT-RUN 守卫
                        errors.Add($"[NOT-RUN 守卫] tier_map[\"{tierKey}\"].{col} 缺失 —— " +
                                   "AC-44-06 ① 不可执行(schema 门已拦)");
                        continue;
                    }

                    if (!TryParseFloat(raw, out float noiseValue))
                    {
                        errors.Add($"[SNR] tier_map[\"{tierKey}\"].{col} = \"{raw}\" 无法解析为 float —— " +
                                   "非法输入(AC-44-06 ①:须有限)");
                        continue;
                    }

                    if (float.IsNaN(noiseValue) || float.IsInfinity(noiseValue))
                    {
                        errors.Add($"[SNR] tier_map[\"{tierKey}\"].{col} = \"{raw}\" 非有限值 —— " +
                                   "AC-44-06 ①:噪声底须有限(线性幅度 > 0 的 dB 侧形态)");
                        continue;
                    }

                    if (noiseValue < NoiseFloorDbMin)
                    {
                        errors.Add($"[SNR] tier_map[\"{tierKey}\"].{col} = {noiseValue} dB < " +
                                   $"{NoiseFloorDbMin} dB(NOISE_FLOOR_DB_MIN)—— " +
                                   "AC-44-06 ①:背景噪声永不为零(线性幅度 > 0)");
                    }
                }

                // ② signal_db 列:可解析为 float(NaN/±∞ ⇒ 拒)
                if (!columns.TryGetValue("signal_db", out string signalRaw) || signalRaw == null)
                {
                    errors.Add($"[NOT-RUN 守卫] tier_map[\"{tierKey}\"].signal_db 缺失 —— " +
                               "AC-44-05 ① 不可执行(schema 门已拦)");
                    continue;
                }

                if (!TryParseFloat(signalRaw, out float signalValue))
                {
                    errors.Add($"[SNR] tier_map[\"{tierKey}\"].signal_db = \"{signalRaw}\" 无法解析为 float —— " +
                               "非法输入");
                    continue;
                }

                // 三列均可解析后,取两噪声列(若上一步已报缺失则跳过)
                bool noiseOk = true;
                float noiseVal = 0, contactVal = 0;
                if (!TryParseFloat(columns.TryGetValue("noise_floor_db", out string nRaw) ? nRaw : null, out noiseVal) ||
                    !TryParseFloat(columns.TryGetValue("contact_noise_floor_db", out string cRaw) ? cRaw : null, out contactVal))
                {
                    noiseOk = false;
                }

                if (!noiseOk) continue; // 缺失已报,跳过 ③

                // ③ ComputeSnrDb(行) ∈ [−24, +24](AC-44-05 ①;钳位在派生值上)
                float snr = ComputeSnrDb(signalValue, noiseVal, contactVal);
                if (float.IsNaN(snr))
                {
                    errors.Add($"[SNR] tier_map[\"{tierKey}\"]:ComputeSnrDb ⇒ NaN —— " +
                               "三列含 NaN 输入(非法)");
                    continue;
                }

                if (snr < SnrClampMin || snr > SnrClampMax)
                {
                    // 双保险:ComputeSnrDb 已钳位,此行不应到达;若到达则钳位实现漂移
                    errors.Add($"[SNR] tier_map[\"{tierKey}\"]:ComputeSnrDb = {snr} dB ∉ " +
                               $"[{SnrClampMin}, {SnrClampMax}] —— 钳位实现漂移(双保险断言)");
                }
            }

            return errors;
        }

        // ══════════ 内部工具 ══════════

        /// <summary>不变文化 float 解析(零 I/O、零第三方;对齐 ADR-014 纪律的测试侧对应)。</summary>
        private static bool TryParseFloat(string raw, out float value)
        {
            if (raw == null)
            {
                value = 0;
                return false;
            }
            return float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }
    }
}
