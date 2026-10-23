// 权威来源:ADR-014 §三/§五(两阶段烘焙入口 · ConfigVersion = 源数据集内容哈希派生)
//          · `DiseaseActionAxisBaker`(同构先例)
//
// ⚠️ 本件 = 9 注册表的烘焙入口:读源文件 → 绑定 + 校验 → 编码。
//    在此之前 9 的注册表**没有任何烘焙入口** —— `disease_registry.cooked` 从未产出过。

using System;
using System.Collections.Generic;
using System.IO;
using DaYiJingCheng.Gameplay.Presentation;   // DiseaseRegistryDataSet / DiseaseRegistryCookedCodec(真往返)
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;           // CookedFormat(头契约断言)

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>`assets/data/disease_registry.json` → `disease_registry.cooked` 的烘焙入口。</summary>
    public static class DiseaseRegistryBaker
    {
        /// <summary>源文件所在目录(相对仓根)。</summary>
        public const string DataDirName = "assets/data";

        /// <summary>源文件名。</summary>
        public const string SourceFileName = "disease_registry.json";

        /// <summary>产物文件名(⚠️ `.bytes` 扩展 —— ADR-014 的导入修正:
        /// 自定义扩展默认导入为 DefaultAsset,Addressables 无法按 TextAsset 载入二进制;
        /// `.bytes` 走内建 TextAsset 导入器)。</summary>
        public const string CookedFileName = "disease_registry.cooked.bytes";

        /// <summary>烘焙产物。</summary>
        public readonly struct BakeOutput
        {
            /// <summary>确定字节流(含 20 字节头)。</summary>
            public readonly byte[] Cooked;

            /// <summary>源数据集内容哈希派生的 ConfigVersion(u32)。</summary>
            public readonly uint ConfigVersion;

            /// <summary>绑定后的行集(供调用方断言)。</summary>
            public readonly List<DiseaseRegistryEntry> Rows;

            /// <summary>声明了但未绑定进行的键(透传 <see cref="DiseaseRegistryBinder.BindResult.UnboundKeys"/>)。
            /// <para>⚠️ 这是**丢失登记**,不是错误 —— 但它必须可见,否则「白名单内的键静默丢字段」
            /// 就又回到无人能看见的状态。测试断言其恰为预期集合。</para></summary>
            public readonly List<string> UnboundKeys;

            /// <summary>
            /// 全局 <c>W</c>(趋势差分窗口,tick;GDD Tuning §四 <c>:1743</c>)。
            /// <c>0</c> = 未配置(数值轮未裁)⇒ F4 的痊愈支 / 迁延支不求值。
            /// <para>✅ <b>2026-10-22 已透传进 cooked 字节</b>(载荷前两个量,见
            /// <see cref="DiseaseRegistryCookedWriter.Write"/>)—— 生产路径经
            /// <c>DiseaseRegistryCookedCodec.Read</c> 拿得到值。
            /// 🔁 <b>原文(2026-10-22 铺管轮)写「不透传进 cooked 字节 … 这是已知缺口」——
            /// 已失效(原文不删)</b>:那是 B1 的②号断点,本批补的就是它。</para>
            /// </summary>
            public readonly long TrendWindowTicks;

            /// <summary>全局 <c>TREND_EPS</c>(Q16.16 raw;GDD Tuning §四 <c>:1744</c>)。<c>0</c> = 未配置。
            /// <para>✅ 同上,已透传进 cooked。</para></summary>
            public readonly long TrendEpsRaw;

            public BakeOutput(byte[] cooked, uint configVersion, List<DiseaseRegistryEntry> rows,
                              List<string> unboundKeys,
                              long trendWindowTicks = 0L, long trendEpsRaw = 0L)
            {
                Cooked = cooked; ConfigVersion = configVersion; Rows = rows; UnboundKeys = unboundKeys;
                TrendWindowTicks = trendWindowTicks; TrendEpsRaw = trendEpsRaw;
            }
        }

        /// <summary>从源文本烘焙(不起 IO —— 供测试直接驱动)。</summary>
        /// <param name="json">`disease_registry.json` 原文。</param>
        /// <param name="causalTreatmentByDiseaseKey">
        /// <c>handle</c> 派生的输入(见 <see cref="DiseaseRegistryBinder.Bind"/>)。
        /// <b>null = 轴未接线</b> ⇒ <c>Handle</c> 留 null。
        /// <para>⚠️ <b>本重载刻意不自己读轴文件</b>:它是「不起 IO」的入口,
        /// 让它在内部找仓根 = 偷偷引入一条调用方看不见的依赖。
        /// 生产路径走 <see cref="BakeFromRepo"/>(它读两个文件并算这张表)。</para>
        /// </param>
        public static BakeOutput BakeFromJson(
            string json, IReadOnlyDictionary<string, bool> causalTreatmentByDiseaseKey = null)
        {
            if (json == null) throw new ArgumentNullException(nameof(json));

            DiseaseRegistryBinder.BindResult bound =
                DiseaseRegistryBinder.Bind(json, causalTreatmentByDiseaseKey);

            // ConfigVersion = 源文本集内容哈希(排序按文件名,确定性;ADR-014 §五)。
            // ⚠️ **只有本产物自己的源文本进哈希**,`disease_action_axis.json` 不进 ——
            //   它是**另一份产物**(`disease_action_axis.cooked.bytes`)的源,有自己的
            //   ConfigVersion。把轴的内容塞进本产物的版本号,会让「只改了轴」也令
            //   本产物的 ConfigVersion 变化 —— 而本产物的字节里**没有任何轴内容**
            //   (treatable_by 不烘,见 Binder 注释),那是一个**对不上字节的版本号**。
            //   ⚠️ 唯一的例外是 `handle` —— 它是轴派生**并写进字节**的。
            //   故 `causalTreatmentByDiseaseKey` 变化确实会改本产物字节;它由
            //   `cooked_drift_all_artifacts_test` 的逐字节门兜住(轴改 ⇒ 本产物也须重烘),
            //   而不是靠 ConfigVersion。
            uint configVersion = ConfigVersionUtility.DeriveConfigVersion(new[]
            {
                new KeyValuePair<string, string>(SourceFileName, json),
            });

            byte[] cooked = DiseaseRegistryCookedWriter.Write(
                bound.Rows, bound.SchemaVersion, configVersion,
                bound.TrendWindowTicks, bound.TrendEpsRaw);
            return new BakeOutput(cooked, configVersion, bound.Rows, bound.UnboundKeys,
                                  bound.TrendWindowTicks, bound.TrendEpsRaw);
        }

        /// <summary>从仓根烘焙(读**两个**源文件 → 绑定 + 校验 → 编码)。</summary>
        /// <param name="repoRoot">仓根(含 `assets/data/`)。</param>
        /// <exception cref="BakeValidationException">词法 / 绑定 / R1 校验任一失败(聚合)。</exception>
        /// <remarks>
        /// <para><b>⚠️ 为什么本入口必须读 `disease_action_axis.json`</b>:<c>handle</c> 是
        /// **派生字段**(GDD <c>:329</c>),其输入「∃ 对因处置」住在轴上,而轴是极性的
        /// **单一真源**(<c>D-9-K</c>)。不读轴 ⇒ <c>handle</c> 恒 null ⇒
        /// GDD <c>:294</c> 的 `care` / `none` / `causal` 三档在产物里**永远不出现**,
        /// 8 的「这病我有没有牌」呈现恒不可用。</para>
        ///
        /// <para><b>⚠️ 只读轴的 `treatable_by` 段,不读 `actions` 段</b>:后者已由
        /// <see cref="DiseaseActionAxisBaker"/> 负责烘成另一份产物。本入口对轴文件
        /// **不做完整校验**(那是轴自己的 baker 的活)—— 只抽 <c>causal</c> 行。
        /// 若轴文件损坏到抽不出来,`DiseaseActionAxisBinder.Bind` 会抛,
        /// 那时**两份产物都烘不出来**,这是对的。</para>
        /// </remarks>
        public static BakeOutput BakeFromRepo(string repoRoot)
        {
            if (repoRoot == null) throw new ArgumentNullException(nameof(repoRoot));

            string dir = Path.Combine(repoRoot, DataDirName);
            string path = Path.Combine(dir, SourceFileName);
            string axisPath = Path.Combine(dir, DiseaseActionAxisBaker.AxisFileName);

            var errors = new List<string>();
            if (!File.Exists(path)) errors.Add($"源文件不存在:{path}");
            if (!File.Exists(axisPath))
                errors.Add($"处置轴源文件不存在:{axisPath} —— `handle` 派生需要它(GDD :329)");
            if (errors.Count > 0) throw new BakeValidationException(errors);

            // 轴走**自己的 binder**(零重实现):同一份校验、同一份行集。
            // ⚠️ 它抛 `BakeValidationException` —— 不 catch,让它原样冒出去:
            //    「轴坏了」与「注册表坏了」对调用方是同一类失败(都是源数据不合格)。
            DiseaseActionAxisBinder.BindResult axis =
                DiseaseActionAxisBinder.Bind(File.ReadAllText(axisPath));

            return BakeFromJson(File.ReadAllText(path), CausalByDiseaseKey(axis.TreatableBy));
        }

        /// <summary>
        /// 从轴的 <c>treatable_by</c> 行集算「每病种是否至少有一条对因处置」。
        /// </summary>
        /// <remarks>
        /// <para>⚠️ <b>只出现 <c>symptomatic</c> 行的病种也会进表</b>(值为 <c>false</c>)——
        /// 这是刻意的:表里「查不到」与「查到了但全是对症」在派生上**都**意味着
        /// <c>hasCausal = false</c>,但前者会让读者误以为「轴里没有这个病」。
        /// 显式写入 <c>false</c> 使表本身可读。</para>
        /// <para>⚠️ 病种**完全不在轴里**(如全部 <c>INJ_*</c> 伤情)⇒ 表里也没有它 ⇒
        /// 派生时 <c>TryGetValue</c> 落空 ⇒ <c>hasCausal = false</c>。
        /// 对伤情,这给出 <c>symptomatic_only</c>(它们不在照护集、不是破伤风)——
        /// 与作者态 <c>handle: "symptomatic_only"</c> 的既有声明一致。</para>
        /// </remarks>
        private static IReadOnlyDictionary<string, bool> CausalByDiseaseKey(
            IReadOnlyList<TreatableByRow> rows)
        {
            var map = new Dictionary<string, bool>(StringComparer.Ordinal);
            for (int i = 0; i < rows.Count; i++)
            {
                TreatableByRow row = rows[i];
                if (row.DiseaseKey == null) continue;

                bool isCausal = string.Equals(row.Polarity, "causal", StringComparison.Ordinal);
                if (map.TryGetValue(row.DiseaseKey, out bool existing))
                    map[row.DiseaseKey] = existing || isCausal;
                else
                    map[row.DiseaseKey] = isCausal;
            }
            return map;
        }

        /// <summary>
        /// 烘焙输出 → 运行期数据集(**走真往返**:字节经
        /// <see cref="DiseaseRegistryCookedCodec.Read"/> 解回,再做跨侧逐项等值断言)。
        /// </summary>
        /// <remarks>
        /// <para><b>为什么必须有(承 `TelemetryAnalyticsBake.Materialize` 同款理由)</b>:
        /// 写方(`Editor.Tools.Bake.DiseaseRegistryCookedWriter`)与读方
        /// (`Gameplay.Presentation.DiseaseRegistryCookedCodec`)是**两份手写布局**,分居两个程序集。
        /// 只比 `BakeOutput.Rows` 与内存结构**不会**发现布局分叉 —— 那是同一个对象自己跟自己比。
        /// 本入口是两侧布局的**唯一自动兜现**:任一侧多写 / 少写 / 错序一个字段,这里立刻红。</para>
        /// <para>⚠️ 比的是**逐字段等值**,不是「解出来不抛」。解出来字段全 0 也能不抛。</para>
        /// </remarks>
        /// <param name="output">烘焙输出(<see cref="BakeFromRepo"/> / <see cref="BakeFromJson"/> 的产物)。</param>
        /// <returns>解回的运行期数据集。</returns>
        /// <exception cref="BakeValidationException">头部契约不符 / 跨侧逐项等值失败(布局分叉 / 编码丢字段)。</exception>
        public static DiseaseRegistryDataSet Materialize(in BakeOutput output)
        {
            AssertHeaderContract(output.Cooked);

            DiseaseRegistryDataSet dataSet;
            try
            {
                dataSet = DiseaseRegistryCookedCodec.Read(output.Cooked);
            }
            catch (Exception ex) when (!(ex is BakeValidationException))
            {
                throw new BakeValidationException(new List<string>
                {
                    $"真往返失败 —— 读方拒收了本件写出的字节(布局分叉 / 头契约不符):{ex.GetType().Name}: {ex.Message}",
                });
            }

            var errors = new List<string>();

            if (dataSet.ConfigVersion != output.ConfigVersion)
                errors.Add($"cooked 头槽 ConfigVersion 0x{dataSet.ConfigVersion:X8} ≠ 烘焙期派生值 " +
                           $"0x{output.ConfigVersion:X8} —— 编码路径与派生路径分叉");

            Compare("trend_window_ticks", output.TrendWindowTicks, dataSet.TrendWindowTicks, errors);
            Compare("trend_eps", output.TrendEpsRaw, dataSet.TrendEpsRaw, errors);

            if (dataSet.Rows == null || dataSet.Rows.Count != output.Rows.Count)
            {
                errors.Add($"行数往返后 {dataSet.Rows?.Count.ToString() ?? "null"} ≠ 绑定行数 {output.Rows.Count}");
            }
            else
            {
                for (int i = 0; i < output.Rows.Count; i++)
                    CompareRow(i, output.Rows[i], dataSet.Rows[i], errors);
            }

            if (errors.Count > 0) throw new BakeValidationException(errors);
            return dataSet;
        }

        /// <summary>逐字段比对一行(字段清单 = 写方 <c>WriteRow</c> 的书写序,一一对应)。</summary>
        private static void CompareRow(int i, DiseaseRegistryEntry expected, DiseaseRegistryEntry actual,
                                       List<string> errors)
        {
            string w = $"rows[{i}](id={expected.DiseaseId})";
            Compare(w + ".id", expected.DiseaseId, actual.DiseaseId, errors);
            if (!string.Equals(expected.DiseaseKey, actual.DiseaseKey, StringComparison.Ordinal))
                errors.Add($"{w}.key 往返后 \"{actual.DiseaseKey}\" ≠ 绑定值 \"{expected.DiseaseKey}\"");
            Compare(w + ".kind", (int)expected.Kind, (int)actual.Kind, errors);
            Compare(w + ".polarity", expected.Polarity, actual.Polarity, errors);
            Compare(w + ".severity", expected.Severity, actual.Severity, errors);
            Compare(w + ".contagion", expected.Contagion, actual.Contagion, errors);
            Compare(w + ".lethality", expected.Lethality, actual.Lethality, errors);
            Compare(w + ".treatment_difficulty", expected.TreatmentDifficulty, actual.TreatmentDifficulty, errors);
            Compare(w + ".recovery_time", expected.RecoveryTime, actual.RecoveryTime, errors);
            Compare(w + ".relapse_chance", expected.RelapseChance, actual.RelapseChance, errors);
            Compare(w + ".comorbidity_factor", expected.ComorbidityFactor, actual.ComorbidityFactor, errors);
            Compare(w + ".seasonal_mod", expected.SeasonalMod, actual.SeasonalMod, errors);
            Compare(w + ".age_mod", expected.AgeMod, actual.AgeMod, errors);
            Compare(w + ".gender_mod", expected.GenderMod, actual.GenderMod, errors);
            Compare(w + ".occupation_mod", expected.OccupationMod, actual.OccupationMod, errors);
            Compare(w + ".region_mod", expected.RegionMod, actual.RegionMod, errors);
            Compare(w + ".climate_mod", expected.ClimateMod, actual.ClimateMod, errors);
            Compare(w + ".scale", expected.Scale.Raw, actual.Scale.Raw, errors);
            Compare(w + ".trauma_half_life", expected.TraumaHalfLife, actual.TraumaHalfLife, errors);

            if ((expected.Curve != null) != (actual.Curve != null))
            {
                errors.Add($"{w}.curve 有无往返后不一致(绑定 {(expected.Curve != null ? "有" : "无")}," +
                           $"解回 {(actual.Curve != null ? "有" : "无")})");
                return;
            }
            if (expected.Curve != null)
            {
                string c = w + ".natural_progress";
                Compare(c + ".incubation", expected.Curve.Incubation, actual.Curve.Incubation, errors);
                Compare(c + ".a_peak", expected.Curve.APeak.Raw, actual.Curve.APeak.Raw, errors);
                Compare(c + ".tau_rise", expected.Curve.TauRise.Raw, actual.Curve.TauRise.Raw, errors);
                Compare(c + ".tau_peak", expected.Curve.TauPeak, actual.Curve.TauPeak, errors);
                Compare(c + ".tau_fall", expected.Curve.TauFall.Raw, actual.Curve.TauFall.Raw, errors);
                if (expected.Curve.Plateau != actual.Curve.Plateau)
                    errors.Add($"{c}.plateau 往返后 {actual.Curve.Plateau} ≠ 绑定值 {expected.Curve.Plateau}");
                if (expected.Curve.SelfLimit != actual.Curve.SelfLimit)
                    errors.Add($"{c}.self_limit 往返后 {actual.Curve.SelfLimit} ≠ 绑定值 {expected.Curve.SelfLimit}");
                if (expected.Curve.Sigma.HasValue != actual.Curve.Sigma.HasValue)
                    errors.Add($"{c}.sigma 有无往返后不一致");
                else if (expected.Curve.Sigma.HasValue)
                    Compare(c + ".sigma", expected.Curve.Sigma.Value.Raw, actual.Curve.Sigma.Value.Raw, errors);
                if (!string.Equals(expected.Curve.BoundaryMode, actual.Curve.BoundaryMode, StringComparison.Ordinal))
                    errors.Add($"{c}.boundary_mode 往返后 \"{actual.Curve.BoundaryMode}\" ≠ " +
                               $"绑定值 \"{expected.Curve.BoundaryMode}\"");
            }

            // ── 2026-10-23 新编码的三块(handle / relapse / signs)──
            //  ⚠️ 这三块**必须**逐字段比:只比「不抛」等于不比 —— 解出来全 null 也能不抛。
            if (!string.Equals(expected.Handle, actual.Handle, StringComparison.Ordinal))
                errors.Add($"{w}.handle 往返后 \"{actual.Handle}\" ≠ 绑定值 \"{expected.Handle}\"");

            if ((expected.Relapse != null) != (actual.Relapse != null))
            {
                errors.Add($"{w}.relapse 有无往返后不一致(绑定 " +
                           $"{(expected.Relapse != null ? "有" : "无")},解回 " +
                           $"{(actual.Relapse != null ? "有" : "无")})");
            }
            else if (expected.Relapse != null)
            {
                string rl = w + ".relapse";
                Compare(rl + ".a_rel", expected.Relapse.ARel.Raw, actual.Relapse.ARel.Raw, errors);
                Compare(rl + ".interval", expected.Relapse.Interval, actual.Relapse.Interval, errors);
                Compare(rl + ".tau_fall", expected.Relapse.TauFall.Raw, actual.Relapse.TauFall.Raw, errors);
            }

            if ((expected.Signs == null) != (actual.Signs == null))
            {
                errors.Add($"{w}.signs 有无往返后不一致(绑定 " +
                           $"{(expected.Signs == null ? "null" : "数组")},解回 " +
                           $"{(actual.Signs == null ? "null" : "数组")})—— " +
                           "null(未声明)与空数组(显式空集)是两种语义,GDD :291 只允许后者为空");
            }
            else if (expected.Signs != null)
            {
                if (expected.Signs.Length != actual.Signs.Length)
                {
                    errors.Add($"{w}.signs 行数往返后 {actual.Signs.Length} ≠ 绑定值 {expected.Signs.Length}");
                }
                else
                {
                    for (int k = 0; k < expected.Signs.Length; k++)
                    {
                        string sg = $"{w}.signs[{k}]";
                        if ((expected.Signs[k] == null) != (actual.Signs[k] == null))
                        {
                            errors.Add($"{sg} 有无往返后不一致");
                            continue;
                        }
                        if (expected.Signs[k] == null) continue;
                        if (!string.Equals(expected.Signs[k].SignKey, actual.Signs[k].SignKey,
                                           StringComparison.Ordinal))
                            errors.Add($"{sg}.sign_key 往返后 \"{actual.Signs[k].SignKey}\" ≠ " +
                                       $"绑定值 \"{expected.Signs[k].SignKey}\"");
                        Compare(sg + ".channel_mask", expected.Signs[k].ChannelMask,
                                actual.Signs[k].ChannelMask, errors);
                    }
                }
            }

            // ── treatable_by:**不进产物,故断言两侧都是 null** ──
            //  ⚠️ 这是「刻意不携带」的机械表达:若哪天有人往写方加了一段 treatable_by,
            //    读方仍置 null ⇒ 这里立刻红。若读方也加了 ⇒ 本断言需同改,
            //    而改它会撞到 `ITreatableByLookup` 的「单一真源」纪律。
            if (expected.TreatableBy != null || actual.TreatableBy != null)
                errors.Add($"{w}.treatable_by 不得进产物(轴是单一真源,D-9-K)—— " +
                           $"绑定侧 {(expected.TreatableBy == null ? "null" : "非 null")}," +
                           $"解回侧 {(actual.TreatableBy == null ? "null" : "非 null")}");
        }

        private static void Compare(string field, long expected, long actual, List<string> errors)
        {
            if (expected != actual)
                errors.Add($"{field} 往返后 {actual} ≠ 绑定值 {expected}");
        }

        /// <summary>cooked 头契约断言(魔数 / formatVersion / schemaVersion / payloadLength)。</summary>
        private static void AssertHeaderContract(byte[] cooked)
        {
            var errors = new List<string>();
            if (cooked == null || cooked.Length < CookedFormat.HeaderSize)
            {
                errors.Add("cooked 短于头部 —— 编码路径损坏");
                throw new BakeValidationException(errors);
            }
            if (cooked[0] != 'D' || cooked[1] != 'Y' || cooked[2] != 'J' || cooked[3] != 'C')
                errors.Add("cooked 魔数不符(≠ DYJC)");
            uint formatVersion = ReadU32(cooked, 4);
            if (formatVersion != CookedFormat.FormatVersion)
                errors.Add($"cooked formatVersion={formatVersion} ≠ 契约 {CookedFormat.FormatVersion}");
            uint payloadLength = ReadU32(cooked, 16);
            if (payloadLength != cooked.Length - CookedFormat.HeaderSize)
                errors.Add($"cooked payloadLength={payloadLength} ≠ 实际 {cooked.Length - CookedFormat.HeaderSize}");
            if (errors.Count > 0) throw new BakeValidationException(errors);
        }

        private static uint ReadU32(byte[] buffer, int offset)
            => (uint)(buffer[offset] | (buffer[offset + 1] << 8) | (buffer[offset + 2] << 16) | (buffer[offset + 3] << 24));
    }
}

// ══════════════════════════════════════════════════════════════════════════
// §未实现登记(如实列出,禁借绿)
// ══════════════════════════════════════════════════════════════════════════
//   ① ✅ **菜单入口已接**(2026-10-22)—— `DataBakeMenu.BakeDiseaseRegistry`
//      (「大医精诚/数据管线/烘焙 disease_registry(9 病种注册表)」)。原文「无菜单入口」**已失效**。
//   ② ✅ **产物已落盘**(2026-10-22)—— `unity/Assets/DataCooked/disease_registry.cooked.bytes`,
//      并已从 `cooked_drift_all_artifacts_test.KnownUnbaked` 移入 `BakedArtifacts` 受逐字节漂移门保护。
//      原文「`assets/data/disease_registry.cooked.bytes` 当前不存在」**已失效**(原文不删;
//      ⚠️ 原路径写错了:产物落 `unity/Assets/DataCooked/`,**不是** `assets/data/`)。
//   ③ ✅ **运行期已加载**(2026-10-22)—— `CompositionRoot` 经 `DiseaseRegistryDataSet` 装载
//      (provider ⇒ Addressables;provider == null ⇒ `DataCookedDisk` 磁盘兜底,同一份字节同一 codec),
//      并把 `W` / `TREND_EPS` 灌进 `DiseaseVitalsService`。原文「仍用 CreateFallbackRegistry」**已失效**。
//      ⚠️ **fallback 仍然存在**且**仍会被用到**:`registry == null` 且**两条装载路径都不可用**时
//      (`dataProvider == null` 且磁盘产物缺失)—— 那是 EditMode 极早期 / 产物未烘的环境。
//      它的语义**未变**:接线载体,非数值真源。
//   ⇒ 本件的兑现自本批起 = **「管线端到端已通」**,不再只是「能烘焙出确定字节」。
