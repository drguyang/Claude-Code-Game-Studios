// 权威来源:ADR-014 §三/§五(两阶段烘焙入口 · ConfigVersion = 源数据集内容哈希派生)
//          · `docs/tech-debt-register.md` `TD-audio-032` ①
//          · 承 `DiagnosisReadFloorBaker` 同纪律
//
// ⚠️ 本件刻意**不碰 UnityEngine / Addressables**:纯文件 I/O + 绑定 + 编码,
//    故菜单(编辑器)与 EditMode 测试可**驱动同一台生产机器**(反空转)。

using System;
using System.Collections.Generic;
using System.IO;
using DaYiJingCheng.Gameplay.PatientAI;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>`ai_patient.json` 两阶段烘焙的**仓根装载器**(阶段 1 词法 + 阶段 2 绑定 + 编码)。
    /// </summary>
    public static class PatientAiBaker
    {
        /// <summary>作者态源文件名(单一出处;测试与菜单共用)。</summary>
        public const string AiPatientFileName = "ai_patient.json";

        /// <summary>源文件所在目录(相对仓根;承既有先例 `assets/data/`)。</summary>
        public const string DataDirName = "assets/data";

        /// <summary>烘焙成功产物(<c>ai_patient.cooked.bytes</c> 内容)。</summary>
        public readonly struct BakeOutput
        {
            /// <summary>cooked 字节(含 20 字节头)。</summary>
            public readonly byte[] Cooked;

            /// <summary>源数据集内容哈希派生的 ConfigVersion(u32)。</summary>
            public readonly uint ConfigVersion;

            /// <summary>生成后的调参表(供调用方核形状,不落盘)。</summary>
            public readonly PatientAiDataSet Data;

            public BakeOutput(byte[] cooked, uint configVersion, PatientAiDataSet data)
            {
                Cooked = cooked;
                ConfigVersion = configVersion;
                Data = data;
            }
        }

        /// <summary>从仓根烘焙(读源文件 → 绑定 + 校验 → 编码)。</summary>
        /// <param name="repoRoot">仓根(含 `assets/data/`)。</param>
        /// <exception cref="BakeValidationException">词法 / 绑定 / 硬约束任一失败(聚合)。</exception>
        public static BakeOutput BakeFromRepo(string repoRoot)
        {
            if (repoRoot == null) throw new ArgumentNullException(nameof(repoRoot));

            string path = Path.Combine(repoRoot, DataDirName, AiPatientFileName);
            if (!File.Exists(path))
                throw new BakeValidationException(new List<string> { $"源文件不存在:{path}" });

            string json = File.ReadAllText(path);
            PatientAiBinder.BindResult bound = PatientAiBinder.Bind(json);

            // ConfigVersion = 源文本内容哈希(ADR-014 §五;单文件集,按文件名排序)。
            uint configVersion = ConfigVersionUtility.DeriveConfigVersion(new[]
            {
                new KeyValuePair<string, string>(AiPatientFileName, json),
            });

            byte[] cooked = PatientAiCookedWriter.Write(bound, configVersion);

            var data = new PatientAiDataSet(
                bound.CueIntervals, bound.BehaviorBands, configVersion, bound.SchemaVersion);
            return new BakeOutput(cooked, configVersion, data);
        }
    }
}
