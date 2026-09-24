// 权威来源:ADR-014 §五(ConfigVersion = 源数据集内容哈希派生 u32)· ADR-010 §七(与存档头分离字段比对)
//          · Story 008(TR-itemdb-032:内容哈希随数据变、比对失败非致命)
//
// ⚠️ 哈希只用 BCL 算术(禁 UnityEngine.Hash128 —— ADR-010 纪律);FNV-1a 32 = 跨平台逐位确定。

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>ConfigVersion 比对结果(ADR-010 §七:不匹配**非致命**;仅产物不可读才致命)。</summary>
    public readonly struct ConfigVersionComparison
    {
        /// <summary>是否匹配(存档头 u32 == 产物头 u32)。</summary>
        public readonly bool Match;

        /// <summary>true = 致命(产物 schema/版本不可读,ADR-010 §七 迁移协议的硬失败面)。
        /// 不匹配本身**永不**置 true(措辞不得改成致命 —— 要改须另开 ADR)。</summary>
        public readonly bool Fatal;

        /// <summary>可读说明(空 = 正常)。</summary>
        public readonly string Note;

        public ConfigVersionComparison(bool match, bool fatal, string note)
        {
            Match = match;
            Fatal = fatal;
            Note = note;
        }
    }

    /// <summary>ConfigVersion(u32)派生与比对。
    /// <para>派生 = 对「排序后的 (文件名, 字节) 序列」做 FNV-1a 32:
    /// 每个源依次喂 UTF-8(文件名) + 0x00 + UTF-8(内容) + 0xFF。任一字节变化 ⇒ 哈希变化。</para>
    /// <example>
    /// <code>
    /// uint v = ConfigVersionUtility.DeriveConfigVersion(new[] {
    ///     new KeyValuePair&lt;string,string&gt;("item_database_items.json", itemsJson) });
    /// </code>
    /// </example>
    /// </summary>
    public static class ConfigVersionUtility
    {
        private const uint FnvOffset = 2166136261u;
        private const uint FnvPrime = 16777619u;

        /// <summary>从命名源对派生 u32(按键 Ordinal 排序 —— 与枚举顺序无关,确定性)。</summary>
        /// <param name="namedSources">(文件名, 文本内容)对;null 元素抛 ArgumentNullException。</param>
        /// <returns>u32 内容哈希。</returns>
        public static uint DeriveConfigVersion(IEnumerable<KeyValuePair<string, string>> namedSources)
        {
            if (namedSources == null) throw new ArgumentNullException(nameof(namedSources));

            var sorted = new List<KeyValuePair<string, string>>(namedSources);
            sorted.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));

            uint hash = FnvOffset;
            foreach (KeyValuePair<string, string> pair in sorted)
            {
                if (pair.Key == null || pair.Value == null)
                    throw new ArgumentException("命名源的键与值都不得为 null");

                byte[] nameBytes = Encoding.UTF8.GetBytes(pair.Key);
                byte[] contentBytes = Encoding.UTF8.GetBytes(pair.Value);
                hash = MixBytes(hash, nameBytes);
                hash = MixByte(hash, 0x00);
                hash = MixBytes(hash, contentBytes);
                hash = MixByte(hash, 0xFF);
            }
            return hash;
        }

        /// <summary>从仓库读取 <c>assets/data/item_database_*.json</c>(按文件名排序)派生 —— 烘焙菜单 / 对拍用。</summary>
        /// <param name="repoRoot">仓库根(含 assets/ 的目录)。</param>
        /// <returns>u32 内容哈希;目录不存在抛 DirectoryNotFoundException(启动期硬失败口径)。</returns>
        public static uint DeriveConfigVersionFromRepo(string repoRoot)
        {
            if (repoRoot == null) throw new ArgumentNullException(nameof(repoRoot));
            string dataDir = Path.Combine(repoRoot, "assets", "data");
            if (!Directory.Exists(dataDir))
                throw new DirectoryNotFoundException($"assets/data 不存在:{dataDir}");

            string[] files = Directory.GetFiles(dataDir, "item_database_*.json");
            Array.Sort(files, StringComparer.Ordinal); // 文件名 Ordinal 排序

            var pairs = new List<KeyValuePair<string, string>>(files.Length);
            foreach (string file in files)
                pairs.Add(new KeyValuePair<string, string>(Path.GetFileName(file), File.ReadAllText(file, Encoding.UTF8)));
            return DeriveConfigVersion(pairs);
        }

        /// <summary>存档头 ↔ 产物头比对(ADR-010 §七)。
        /// <para>产物 ConfigVersion 不可读(调用方给 null)⇒ 致命;可读但不匹配 ⇒ <b>非致命</b>,
        /// 由调用方记录日志并按迁移协议排查,不得当作加载失败。</para></summary>
        /// <param name="readableProductConfigVersion">从产物头读出的值;null = 不可读/损坏。</param>
        /// <param name="saveHeaderConfigVersion">存档头字段。</param>
        /// <example><code>var c = ConfigVersionUtility.CompareConfigVersion(prod, save); // c.Fatal 只在 prod 为 null 时 true</code></example>
        public static ConfigVersionComparison CompareConfigVersion(
            uint? readableProductConfigVersion, uint saveHeaderConfigVersion)
        {
            if (!readableProductConfigVersion.HasValue)
            {
                return new ConfigVersionComparison(
                    false, true,
                    "产物 ConfigVersion 不可读(schema/头部损坏)—— ADR-010 §七 致命面,硬失败");
            }

            bool match = readableProductConfigVersion.Value == saveHeaderConfigVersion;
            return new ConfigVersionComparison(
                match, false,
                match
                    ? string.Empty
                    : $"ConfigVersion 不匹配(产物 0x{readableProductConfigVersion.Value:X8} ≠ 存档头 0x{saveHeaderConfigVersion:X8})" +
                      " —— 非致命(ADR-010 §七);记录并排查回放/存档来源不一致");
        }

        private static uint MixBytes(uint hash, byte[] bytes)
        {
            unchecked
            {
                for (int i = 0; i < bytes.Length; i++)
                {
                    hash ^= bytes[i];
                    hash *= FnvPrime;
                }
            }
            return hash;
        }

        private static uint MixByte(uint hash, byte b)
        {
            unchecked
            {
                hash ^= b;
                hash *= FnvPrime;
            }
            return hash;
        }
    }
}
