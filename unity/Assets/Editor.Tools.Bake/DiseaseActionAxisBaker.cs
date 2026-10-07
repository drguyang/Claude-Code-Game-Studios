// 权威来源:ADR-014 §三/§五(两阶段烘焙入口 · ConfigVersion = 源数据集内容哈希派生)
//          · Story 007(NR-1:装载路径的**仓根装载器** —— 与菜单 / 测试共用同一台机器)
//
// ⚠️ 本件刻意**不碰 UnityEngine / Addressables**:纯文件 I/O + 绑定 + 编码,
//    故菜单(编辑器)与 EditMode 测试可**驱动同一台生产机器**(反空转:
//    测试断言的必须是生产接缝,不是测试侧重实现)。

using System;
using System.Collections.Generic;
using System.IO;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>`disease_action_axis.json` 两阶段烘焙的**仓根装载器**(阶段 1 词法 + 阶段 2 绑定 + 编码)。
    /// <para>校验器调用点在 <see cref="DiseaseActionAxisBinder.Bind"/> 内(唯一调用点)。</para>
    /// </summary>
    public static class DiseaseActionAxisBaker
    {
        /// <summary>作者态源文件名(单一出处;测试与菜单共用)。</summary>
        public const string AxisFileName = "disease_action_axis.json";

        /// <summary>源文件所在目录(相对仓根;承既有先例 `assets/data/`)。</summary>
        public const string DataDirName = "assets/data";

        /// <summary>烘焙成功产物(<c>disease_action_axis.cooked.bytes</c> 内容)。</summary>
        public readonly struct BakeOutput
        {
            /// <summary>cooked 字节(含 20 字节头)。</summary>
            public readonly byte[] Cooked;

            /// <summary>源数据集内容哈希派生的 ConfigVersion(u32)。</summary>
            public readonly uint ConfigVersion;

            /// <summary>校验通过后的处置轴行集(供调用方核数,不落盘)。</summary>
            public readonly IReadOnlyList<ActionAxisRow> Actions;

            /// <summary>校验通过后的 treatable_by 行集(供调用方核数,不落盘)。</summary>
            public readonly IReadOnlyList<TreatableByRow> TreatableBy;

            /// <summary>9 的 NOISE_BAND_POTENCY_9 具名常量值(Q16.16 raw)—— 供 11 的 DC-6 消费。</summary>
            /// <para>⚠️ 该值住**药效幅值域**,**不得**被读成 9 的 σ(Progress 域)或 21a 品级地板(tick 域)。</para>
            public readonly long NoiseBandPotencyRaw;

            public BakeOutput(byte[] cooked, uint configVersion, IReadOnlyList<ActionAxisRow> actions,
                              IReadOnlyList<TreatableByRow> treatableBy, long noiseBandPotencyRaw)
            {
                Cooked = cooked; ConfigVersion = configVersion; Actions = actions; TreatableBy = treatableBy;
                NoiseBandPotencyRaw = noiseBandPotencyRaw;
            }
        }

        /// <summary>从仓根烘焙(读源文件 → 绑定 + 校验 → 编码)。</summary>
        /// <param name="repoRoot">仓根(含 `assets/data/`)。</param>
        /// <exception cref="BakeValidationException">词法 / 绑定 / 9-DC 校验任一失败(聚合)。</exception>
        public static BakeOutput BakeFromRepo(string repoRoot)
        {
            if (repoRoot == null) throw new ArgumentNullException(nameof(repoRoot));

            string dir = Path.Combine(repoRoot, DataDirName);
            string axisPath = Path.Combine(dir, AxisFileName);

            var errors = new List<string>();
            if (!File.Exists(axisPath)) errors.Add($"源文件不存在:{axisPath}");
            if (errors.Count > 0) throw new BakeValidationException(errors);

            string axisJson = File.ReadAllText(axisPath);

            DiseaseActionAxisBinder.BindResult bound = DiseaseActionAxisBinder.Bind(axisJson);

            // ConfigVersion = 源文本集内容哈希(排序按文件名,确定性;ADR-014 §五)。
            uint configVersion = ConfigVersionUtility.DeriveConfigVersion(new[]
            {
                new KeyValuePair<string, string>(AxisFileName, axisJson),
            });

            byte[] cooked = DiseaseActionAxisCookedWriter.Write(bound.Actions, bound.TreatableBy,
                bound.SchemaVersion, configVersion);

            // 9 的 NOISE_BAND_POTENCY_9 具名常量值(Q16.16 raw)—— 供 11 的 DC-6 消费。
            // ⚠️ 该值住**药效幅值域**,**不得**被读成 9 的 σ(Progress 域)或 21a 品级地板(tick 域)。
            // ⚠️ 2026-10-07 用户裁定:数值归用户,当前值 = 100 raw。
            long noiseBandPotencyRaw = 100L;

            return new BakeOutput(cooked, configVersion, bound.Actions, bound.TreatableBy, noiseBandPotencyRaw);
        }
    }
}
