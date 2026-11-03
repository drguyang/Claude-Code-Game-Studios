// 权威来源:ADR-014 §三/§五(两阶段烘焙入口 · ConfigVersion = 源数据集内容哈希派生)
//          · ADR-038 §D2(跨表引用:52 的 `$ref` 指向 5 的同值,构建期断言)
//          · §D4(52 四值)
//
// ⚠️ 纯文件 I/O + 绑定 + 编码:**不碰 UnityEngine / Addressables**,故菜单与 EditMode 测试
//    可驱动同一台生产机器。
//
// ⚠️ **引用解析源 = 同一次仓根读取里的 `5_time.json`**:
//    本 baker 先绑 5 再绑 52 ⇒ 若 5 的作者态写坏了(值域非法),52 的烘焙会先撞 5 的错,
//    不会静默拿到一个坏值。这**不是**冗余:ADR-038 §D2 要求「引用解析后与 5 同值」,
//    而唯一可信的「5 的值」就是 5 的绑定结果本身。

using System;
using System.Collections.Generic;
using System.IO;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>`52_random_events.json` 两阶段烘焙的**仓根装载器**。</summary>
    public static class RandomEventsBaker
    {
        /// <summary>作者态源文件名。</summary>
        public const string RandomEventsFileName = "52_random_events.json";

        /// <summary>产物文件名(address 与此同值)。</summary>
        public const string CookedFileName = "52_random_events.cooked.bytes";

        /// <summary>源文件所在目录(相对仓根)。</summary>
        public const string DataDirName = "assets/data";

        /// <summary>`5_time.ticks_per_day` 的引用键(与作者态 `$ref` 逐字一致)。</summary>
        public const string TicksPerDayRefKey = "5_time.ticks_per_day";

        /// <summary>烘焙成功产物。</summary>
        public readonly struct BakeOutput
        {
            /// <summary>cooked 字节(含 20 字节头)。</summary>
            public readonly byte[] Cooked;

            /// <summary>源文本内容哈希派生的 ConfigVersion(u32)。</summary>
            public readonly uint ConfigVersion;

            /// <summary>绑定结果(供调用方核值,不落盘)。</summary>
            public readonly RandomEventsBinder.BindResult Bound;

            public BakeOutput(byte[] cooked, uint configVersion, RandomEventsBinder.BindResult bound)
            {
                Cooked = cooked;
                ConfigVersion = configVersion;
                Bound = bound;
            }
        }

        /// <summary>从仓根烘焙(先绑 5 取引用值 → 再绑 52 → 编码)。</summary>
        /// <param name="repoRoot">仓根(含 `assets/data/`)。</param>
        /// <exception cref="BakeValidationException">5 或 52 任一违例(聚合)。</exception>
        public static BakeOutput BakeFromRepo(string repoRoot)
        {
            if (repoRoot == null) throw new ArgumentNullException(nameof(repoRoot));

            string dataDir = Path.Combine(repoRoot, DataDirName);
            string timePath = Path.Combine(dataDir, TimeParamsBaker.TimeFileName);
            string eventsPath = Path.Combine(dataDir, RandomEventsFileName);

            var missing = new List<string>();
            if (!File.Exists(timePath)) missing.Add($"源文件不存在:{timePath}");
            if (!File.Exists(eventsPath)) missing.Add($"源文件不存在:{eventsPath}");
            if (missing.Count > 0) throw new BakeValidationException(missing);

            // ① 先把 5 绑出来 —— 引用表的值必须来自**同一套校验**。
            TimeParamsBinder.BindResult time = TimeParamsBinder.Bind(File.ReadAllText(timePath));
            var refs = new Dictionary<string, long>(StringComparer.Ordinal)
            {
                { TicksPerDayRefKey, time.TicksPerDay },
            };

            // ② 再绑 52(`$ref` 在这里被解析成整数)。
            string json = File.ReadAllText(eventsPath);
            RandomEventsBinder.BindResult bound = RandomEventsBinder.Bind(json, refs);

            // ③ 同值断言(ADR-038 §D2 的判据句)—— 绑定层已保证,这里再做一次**逐位**复核。
            if (bound.TicksPerDay != time.TicksPerDay)
                throw new BakeValidationException(new List<string>
                {
                    $"52 的 ticks_per_day({bound.TicksPerDay}) ≠ 5 的({time.TicksPerDay}) —— " +
                    "AC-5-05 要求引用解析后**与 5 同值**(DC-3)",
                });

            uint configVersion = ConfigVersionUtility.DeriveConfigVersion(new[]
            {
                new KeyValuePair<string, string>(RandomEventsFileName, json),
            });

            byte[] cooked = RandomEventsCookedWriter.Write(bound, configVersion);
            return new BakeOutput(cooked, configVersion, bound);
        }
    }
}
