// 权威来源:ADR-014 §三/§五(两阶段烘焙入口 · ConfigVersion = 源数据集内容哈希派生)
//          · GDD random-events.md 规则二 / 规则十一
//
// ⚠️ 纯文件 I/O + 绑定 + 编码:**不碰 UnityEngine / Addressables**,故菜单与 EditMode 测试
//    可驱动同一台生产机器。

using System;
using System.Collections.Generic;
using System.IO;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>`52_event_pool.json` 两阶段烘焙的**仓根装载器**。</summary>
    public static class EventPoolBaker
    {
        /// <summary>作者态源文件名。</summary>
        public const string PoolFileName = "52_event_pool.json";

        /// <summary>产物文件名(address 与此同值)。</summary>
        public const string CookedFileName = "52_event_pool.cooked.bytes";

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
            public readonly EventPoolBinder.BindResult Bound;

            public BakeOutput(byte[] cooked, uint configVersion, EventPoolBinder.BindResult bound)
            {
                Cooked = cooked;
                ConfigVersion = configVersion;
                Bound = bound;
            }
        }

        /// <summary>从仓根烘焙。</summary>
        /// <param name="repoRoot">仓根(含 `assets/data/`)。</param>
        /// <exception cref="BakeValidationException">任一违例(聚合)。</exception>
        public static BakeOutput BakeFromRepo(string repoRoot)
        {
            if (repoRoot == null) throw new ArgumentNullException(nameof(repoRoot));

            string path = Path.Combine(repoRoot, DataDirName, PoolFileName);
            if (!File.Exists(path))
                throw new BakeValidationException(new List<string> { $"源文件不存在:{path}" });

            string json = File.ReadAllText(path);
            EventPoolBinder.BindResult bound = EventPoolBinder.Bind(json);

            uint configVersion = ConfigVersionUtility.DeriveConfigVersion(new[]
            {
                new KeyValuePair<string, string>(PoolFileName, json),
            });

            byte[] cooked = EventPoolCookedWriter.Write(bound, configVersion);
            return new BakeOutput(cooked, configVersion, bound);
        }
    }
}
