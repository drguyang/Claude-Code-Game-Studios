// 权威来源:ADR-014 §三/§五(两阶段烘焙入口 · ConfigVersion = 源数据集内容哈希派生)
//          · ADR-038 §D1(时间参数外部化)
//
// ⚠️ 本件刻意**不碰 UnityEngine / Addressables**:纯文件 I/O + 绑定 + 编码,
//    故菜单(编辑器)与 EditMode 测试可**驱动同一台生产机器**(反空转)。

using System;
using System.Collections.Generic;
using System.IO;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>`5_time.json` 两阶段烘焙的**仓根装载器**(阶段 1 词法 + 阶段 2 绑定 + 编码)。</summary>
    public static class TimeParamsBaker
    {
        /// <summary>作者态源文件名(单一出处;测试与菜单共用)。</summary>
        public const string TimeFileName = "5_time.json";

        /// <summary>产物文件名(cooked;`DataCorePreloader` 的 address 与此同值)。</summary>
        public const string CookedFileName = "5_time.cooked.bytes";

        /// <summary>源文件所在目录(相对仓根)。</summary>
        public const string DataDirName = "assets/data";

        /// <summary>烘焙成功产物。</summary>
        public readonly struct BakeOutput
        {
            /// <summary>cooked 字节(含 20 字节头)。</summary>
            public readonly byte[] Cooked;

            /// <summary>源文本内容哈希派生的 ConfigVersion(u32)。</summary>
            public readonly uint ConfigVersion;

            /// <summary>绑定结果(供调用方核值,不落盘)。</summary>
            public readonly TimeParamsBinder.BindResult Bound;

            public BakeOutput(byte[] cooked, uint configVersion, TimeParamsBinder.BindResult bound)
            {
                Cooked = cooked;
                ConfigVersion = configVersion;
                Bound = bound;
            }
        }

        /// <summary>从仓根烘焙(读源文件 → 绑定 + 校验 → 编码)。</summary>
        /// <param name="repoRoot">仓根(含 `assets/data/`)。</param>
        /// <exception cref="BakeValidationException">词法 / 绑定 / 值域任一失败(聚合)。</exception>
        public static BakeOutput BakeFromRepo(string repoRoot)
        {
            if (repoRoot == null) throw new ArgumentNullException(nameof(repoRoot));

            string path = Path.Combine(repoRoot, DataDirName, TimeFileName);
            if (!File.Exists(path))
                throw new BakeValidationException(new List<string> { $"源文件不存在:{path}" });

            string json = File.ReadAllText(path);
            TimeParamsBinder.BindResult bound = TimeParamsBinder.Bind(json);

            uint configVersion = ConfigVersionUtility.DeriveConfigVersion(new[]
            {
                new KeyValuePair<string, string>(TimeFileName, json),
            });

            byte[] cooked = TimeParamsCookedWriter.Write(bound, configVersion);
            return new BakeOutput(cooked, configVersion, bound);
        }
    }
}
