// 权威来源:ADR-014 §三/§五(两阶段烘焙入口 · ConfigVersion = 源数据集内容哈希派生)
//          · ADR-038 Amendment A(天气参数外部化)
//
// ⚠️ 纯文件 I/O + 绑定 + 编码:不碰 UnityEngine / Addressables。

using System;
using System.Collections.Generic;
using System.IO;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>`5_weather.json` 两阶段烘焙的仓根装载器。</summary>
    public static class WeatherParamsBaker
    {
        /// <summary>作者态源文件名。</summary>
        public const string WeatherFileName = "5_weather.json";

        /// <summary>产物文件名(address 与此同值)。</summary>
        public const string CookedFileName = "5_weather.cooked.bytes";

        /// <summary>源文件所在目录(相对仓根)。</summary>
        public const string DataDirName = "assets/data";

        /// <summary>烘焙成功产物。</summary>
        public readonly struct BakeOutput
        {
            /// <summary>cooked 字节(含 20 字节头)。</summary>
            public readonly byte[] Cooked;

            /// <summary>内容哈希派生的 ConfigVersion。</summary>
            public readonly uint ConfigVersion;

            /// <summary>绑定结果。</summary>
            public readonly WeatherParamsBinder.BindResult Bound;

            public BakeOutput(byte[] cooked, uint configVersion, WeatherParamsBinder.BindResult bound)
            {
                Cooked = cooked;
                ConfigVersion = configVersion;
                Bound = bound;
            }
        }

        /// <summary>从仓根烘焙。</summary>
        public static BakeOutput BakeFromRepo(string repoRoot)
        {
            if (repoRoot == null) throw new ArgumentNullException(nameof(repoRoot));

            string path = Path.Combine(repoRoot, DataDirName, WeatherFileName);
            if (!File.Exists(path))
                throw new BakeValidationException(new List<string> { $"源文件不存在:{path}" });

            string json = File.ReadAllText(path);
            WeatherParamsBinder.BindResult bound = WeatherParamsBinder.Bind(json);

            uint configVersion = ConfigVersionUtility.DeriveConfigVersion(new[]
            {
                new KeyValuePair<string, string>(WeatherFileName, json),
            });

            byte[] cooked = WeatherParamsCookedWriter.Write(bound, configVersion);
            return new BakeOutput(cooked, configVersion, bound);
        }
    }
}
