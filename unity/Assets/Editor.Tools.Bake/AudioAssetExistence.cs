// S14-6 · story-010 —— 音频素材**存在性谓词**(AC-44-D6 的执行体,单一出处)。
//
// 权威来源:
//   AC-44-D6:素材缺失 = **编辑期烘焙门拒绝**(事件表 `assets[]` ↔ 文件存在谓词,
//             入 ADR-014 阶段 2 谓词集;负向:删一个 wav ⇒ 构建失败,不是运行期降级)
//   ADR-014 §三(阶段 2 per-schema 绑定 · 校验聚合 throw)
//   GDD design/gdd/audio-system.md :695「素材本体 = `assets/audio/`(Addressables 流式)」
//
// ⚠️ **素材根 = `unity/Assets/Audio/`(物理),`assets/audio/` 是逻辑路径**(2026-10-16 用户裁定 ②):
//   Unity 工程根是 `unity/`,Addressables / `AudioClip` 都**无法寻址仓根 `assets/` 下的文件**。
//   与 `assets/data/*.json` → `unity/Assets/DataCooked/*.cooked.bytes` 同型(作者态 → 工程内),
//   差别在音频**不经变换**(wav 本体即出货资产)⇒ 物理落点直接就是工程内。
//   GDD :258 / :695 / :1092 与 ADR-018 §五 `:153` 的 `assets/audio/` 已就地加注订正
//   (承 ADR-015 修订 ADR-009 §四 的先例:原文保留,加节首声明)。
//   ⚠️ `assets[]` 条目本身是 **bare 文件名**(如 `sfx_breath_base_loop_small.wav`),
//     无目录前缀 ⇒ 「逻辑路径 → 物理路径」的映射退化为「根 + 名」,零映射代码。
//
// ⚠️ **本件是唯一的存在性判据**:测试**禁**内联 `File.Exists`(story-010 原稿的两个 🔴
//   假绿测试正是「自建文件 + 内联 File.Exists」—— 谓词逻辑是测试的、不是生产的)。
//   `AudioEventTableBinder` 引用本件;负向夹具经 `InjectedMissing` 注入根下的缺失名。

using System;
using System.Collections.Generic;
using System.IO;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>音频素材文件存在性谓词(AC-44-D6;ADR-014 阶段 2 谓词集的「素材文件存在」一项)。</summary>
    public static class AudioAssetExistence
    {
        /// <summary>作者态 / GDD 登记的**逻辑**路径(GDD :695;仅用于诊断措辞与 GDD 对齐,不参与 I/O)。</summary>
        public const string LogicalRoot = "assets/audio";

        /// <summary>素材**物理**根(相对仓根)—— Addressables 与 `AudioClip` 的唯一可寻址面。</summary>
        public const string AssetRootRelative = "unity/Assets/Audio";

        /// <summary>解析素材根(仓根 + <see cref="AssetRootRelative"/>)。</summary>
        /// <param name="repoRoot">仓库根(调用方给出,避免本件猜路径深度)。</param>
        public static string ResolveRoot(string repoRoot)
        {
            if (repoRoot == null) throw new ArgumentNullException(nameof(repoRoot));
            return Path.Combine(repoRoot, "unity", "Assets", "Audio");
        }

        /// <summary>单个素材名在根下是否存在。</summary>
        /// <param name="assetName">事件表 <c>assets[]</c> 的条目(bare 文件名)。</param>
        /// <param name="assetRoot">素材根(绝对路径;见 <see cref="ResolveRoot"/>)。</param>
        public static bool Exists(string assetName, string assetRoot)
        {
            if (string.IsNullOrEmpty(assetName)) return false;
            if (string.IsNullOrEmpty(assetRoot)) return false;
            return File.Exists(Path.Combine(assetRoot, assetName));
        }

        /// <summary>求缺失集(**保序去重** —— 错误顺序确定,便于逐条比对)。
        /// <para>空 = 该行素材全在;<see cref="Missing"/> 返回空列表。</para></summary>
        /// <param name="assetNames">某行的 <c>assets[]</c>(null ⇒ 返回空:字段缺失归 schema 门,不归本谓词)。</param>
        /// <param name="assetRoot">素材根(绝对路径)。</param>
        /// <exception cref="ArgumentNullException"><paramref name="assetRoot"/> 为 null。
        /// ⚠️ **刻意不提供「根可空 ⇒ 跳过」的形态**(2026-10-16 用户裁定 ①:门强制)——
        /// 可选门正是 story-010 原稿 B1「`Assert.Ignore` 假绿」的翻版。</exception>
        public static IReadOnlyList<string> Missing(IReadOnlyList<string> assetNames, string assetRoot)
        {
            if (assetRoot == null) throw new ArgumentNullException(nameof(assetRoot));

            var missing = new List<string>();
            if (assetNames == null) return missing;

            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < assetNames.Count; i++)
            {
                string name = assetNames[i];
                if (string.IsNullOrEmpty(name)) continue;
                if (!seen.Add(name)) continue;
                if (!Exists(name, assetRoot)) missing.Add(name);
            }
            return missing;
        }
    }
}
