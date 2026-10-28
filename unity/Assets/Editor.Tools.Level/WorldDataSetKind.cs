// 关卡工具 —— 数据集类别的**闭集 + 命名单一源 + 序列化分派表**(ADR-022 §三)。
//
// 权威来源:
//   ADR-022 §三(导出契约 7 类:`world_{geometry,ecozones,poi,resources,buildslots,nav_{chunk},terrain}.json`)
//   ADR-022 §五(导航格按 chunk 切片 —— **同一 ADR 里已有「参数化后缀」的先例**)
//   ADR-022 §二(逻辑层整数数据只能由关卡工具导出)
//   `production/epics/level-tool/story-001-level-source-generalization.md` AC-3 / AC-2 / AC-5
//
// ═══════════════════════════════════════════════════════════════════════════
// §命名形状(本 story 的**加性**决定 · 2026-10-29 · 实现轮就地裁定,已在证据件披露)
// ═══════════════════════════════════════════════════════════════════════════
//   · **基准关卡**(`LevelCatalog.CanonicalLevelId` = `p0_clinic`)⇒ `world_{kind}.json`
//     —— **ADR-022 §三 的契约字面一字未改**
//   · **其他关卡** ⇒ `world_{kind}_{level_id}.json`
//
//   ⇒ 多关卡对 ADR-022 §三 是**加性扩展**,不是契约变更:契约点名的那份文件仍在原处、
//     仍是同一份语义。story-001 的 AC-2 允许「ADR-022 §三 允许的等价命名」,本形状即取该口子。
//
//   ⚠️ **为什么不在本 story 里把基准关卡也改成 `_{level_id}` 后缀**(即卡片 §Implementation Notes 的「甲」):
//     那会让 `world_buildslots.json` → `world_buildslots_p0_clinic.json`,而该文件名被
//     **运行期链路的五个常量**硬引用 —— `WorldBuildSlotsBinder.FileName` ·
//     `WorldBuildSlotsBake.CookedFileName` · `WorldBuildSlotsCookedCodec`(`CookedFileName` /
//     `ProductName`)· `DataCookedDisk` · Addressables `DataCoreAddresses` 组条目 +
//     `assets/data/*.json` / `unity/Assets/DataCooked/*.cooked.bytes` 两份盘上产物。
//     改名的连带面 = **重烘 + 重建 Addressables 内容 + S20-20 刚交付的装载面回归**,
//     那是 **S2 / S4**(运行期装载面)的活,不是 S1(M · 纯数据模型与文件布局)的活。
//     ⇒ 本 story 只加**新名字**(非基准关卡),不动**旧名字**;等 S2 做运行期多关卡装载时,
//        基准关卡是否并入统一后缀由那一轮连同 ADR-022 窄修订一起裁。
//
//   ⚠️ **`NavChunk` 刻意不在 `FileNameFor` 里给名字**:它的后缀参数是 **chunk 号**,
//     不是 `level_id`(ADR-022 §五)。用一个「假装是 level」的名字命名它,会产出
//     `world_nav_p0_clinic.json` —— 一个**语义错误但看起来合理**的文件名。
//     ⇒ `FileNameFor(NavChunk, …)` 显式抛(**至今仍然抛**,判据未松)。
//     ✅ **切片命名自 story-005 起由 `NavChunkFileName(levelId, chunkIndex)` 给出**:
//       基准关卡 ⇒ `world_nav_{chunk}.json`;其他关卡 ⇒ `world_nav_{level_id}_{chunk}.json`。
//       两个入口**并存且职责不同**:`FileNameFor` 的后缀是 level,`NavChunkFileName` 的是 chunk。

using System;
using System.Collections.Generic;

namespace DaYiJingCheng.EditorTools.Level
{
    /// <summary>
    /// 一份导出的产物(**文件名 + 文本**)。
    /// <para>🔁 <b>2026-10-29(story-005)</b>:分派表的值从 `Func&lt;LevelSource, string&gt;` 改为
    /// `Func&lt;LevelSource, IReadOnlyList&lt;DataSetProduct&gt;&gt;`。缘由 = `NavChunk`
    /// **一类出 N 份**(每个 chunk 一份,ADR-022 §五),单字符串的值域**表达不了**它;
    /// 而给 `NavChunk` 开一条旁路(在导出器里 `if (kind == NavChunk)`)正是本类存在的理由
    /// 所要消灭的形态(「新增一类不改导出器主体」)。</para>
    /// </summary>
    public readonly struct DataSetProduct
    {
        /// <summary>作者态文件名(如 `world_nav_0.json`)。</summary>
        public readonly string FileName;
        /// <summary>确定性 JSON 文本。</summary>
        public readonly string Json;

        /// <summary>构造。</summary>
        public DataSetProduct(string fileName, string json)
        {
            FileName = fileName ?? throw new ArgumentNullException(nameof(fileName));
            Json = json ?? throw new ArgumentNullException(nameof(json));
        }
    }

    /// <summary>ADR-022 §三 的 7 类世界数据集(闭集)。</summary>
    /// <remarks>
    /// <para>序数是**取值的一部分**(用于确定性遍历序);**不得重排、不得插入中间值** ——
    /// 要加第 8 类就追加在末尾。</para>
    /// <para>⚠️ 本枚举的「闭集」是**契约面**闭集。**2026-10-29(story-005)起全部 7 类都有序列化器**
    /// ⇒ <see cref="WorldDataSets.UnimplementedByStory"/> 已清空。两表**互补**的判据不变:
    /// 新增一类却只登记一边 ⇒ 夹具红。</para>
    /// </remarks>
    public enum WorldDataSetKind
    {
        /// <summary>建造槽位骨架(S20-20 起)。</summary>
        BuildSlots = 0,

        /// <summary>世界几何(尺寸 + 每格可走性 + `LATTICE_SIZE`)。**S2(story-002)起已实现**。</summary>
        Geometry = 1,

        /// <summary>生态区多边形(C4 五项 + C5 单一源)。**S3(story-003)起已实现**。</summary>
        Ecozones = 2,

        /// <summary>POI 定义。**S4(story-004)起已实现**。</summary>
        Poi = 3,

        /// <summary>资源点。**S4(story-004)起已实现**。</summary>
        Resources = 4,

        /// <summary>地形表(`TERRAIN_TABLE` / `slopeLimit` / `stepOffset` 的逻辑侧)。
        /// **S2(story-002)起已实现**。</summary>
        Terrain = 5,

        /// <summary>导航格(**按 chunk 切片** —— 后缀参数是 chunk,不是 level)。**S5(story-005)起已实现**。</summary>
        NavChunk = 6,
    }

    /// <summary>数据集类别的命名与序列化的**单一出处**。</summary>
    public static class WorldDataSets
    {
        /// <summary>闭集全部成员(声明序 = <see cref="WorldDataSetKind"/> 的序数序)。</summary>
        public static readonly IReadOnlyList<WorldDataSetKind> All = new[]
        {
            WorldDataSetKind.BuildSlots,
            WorldDataSetKind.Geometry,
            WorldDataSetKind.Ecozones,
            WorldDataSetKind.Poi,
            WorldDataSetKind.Resources,
            WorldDataSetKind.Terrain,
            WorldDataSetKind.NavChunk,
        };

        /// <summary>
        /// **尚未实现**的类别 → 归属 story 与原因(单一出处;报告与夹具共用)。
        /// <para>⚠️ 这是**返回值里的数据**,不是注释里的自述 —— 新增一类却忘了在这里删条目,
        /// 夹具会红(「已实现」与「未实现」不允许同时成立)。</para>
        /// </summary>
        /// <remarks>🔁 <b>2026-10-29(story-005)</b>:**本表已清空** —— ADR-022 §三 的 7 类
        /// 全部有序列化器。表**保留**(不删):`Serialize` 的无序列化分支仍要读它,
        /// 且将来加第 8 类时它是登记处;删掉它会让「未实现」与「闭集外」合并成一个错误串。
        /// ⚠️ 夹具 `test_ac3_kindClosedSet_…` 相应从「未实现恰 1 条」改为「**闭集内每一类都已实现**」。</remarks>
        public static readonly IReadOnlyDictionary<WorldDataSetKind, string> UnimplementedByStory =
            new Dictionary<WorldDataSetKind, string>();

        /// <summary>文件名主干(`world_buildslots` / `world_nav` …),不含扩展名与后缀。</summary>
        /// <exception cref="ArgumentOutOfRangeException">未定义的枚举值。</exception>
        public static string BaseName(WorldDataSetKind kind)
        {
            switch (kind)
            {
                case WorldDataSetKind.BuildSlots: return "world_buildslots";
                case WorldDataSetKind.Geometry:   return "world_geometry";
                case WorldDataSetKind.Ecozones:   return "world_ecozones";
                case WorldDataSetKind.Poi:        return "world_poi";
                case WorldDataSetKind.Resources:  return "world_resources";
                case WorldDataSetKind.Terrain:    return "world_terrain";
                case WorldDataSetKind.NavChunk:   return "world_nav";
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(kind), (int)kind,
                        "不在 WorldDataSetKind 闭集内 —— 新增类别须同时进 All / BaseName / UnimplementedByStory");
            }
        }

        /// <summary>
        /// 某关卡某类数据集的作者态文件名(见文件头 §命名形状)。
        /// </summary>
        /// <param name="kind">数据集类别;`NavChunk` 会抛(它的后缀参数是 chunk,不是 level)。</param>
        /// <param name="levelId">关卡 id(非空)。</param>
        /// <exception cref="ArgumentException"><paramref name="levelId"/> 为空。</exception>
        /// <exception cref="NotSupportedException"><paramref name="kind"/> = <see cref="WorldDataSetKind.NavChunk"/>。</exception>
        public static string FileNameFor(WorldDataSetKind kind, string levelId)
        {
            if (string.IsNullOrEmpty(levelId))
                throw new ArgumentException("level_id 不得为空", nameof(levelId));
            if (kind == WorldDataSetKind.NavChunk)
                throw new NotSupportedException(
                    "world_nav 的后缀参数是 **chunk 号**,不是 level_id(ADR-022 §五)—— " +
                    "按 level 命名会产出 world_nav_<level>.json(语义错但看起来合理)。切片命名归 S5");

            string baseName = BaseName(kind);
            return levelId == LevelCatalog.CanonicalLevelId
                ? baseName + ".json"
                : baseName + "_" + levelId + ".json";
        }

        /// <summary>
        /// `world_nav` 的**切片文件名**(后缀 = chunk 号,不是 level;ADR-022 §五)。
        /// </summary>
        /// <param name="levelId">关卡 id。</param>
        /// <param name="chunkIndex">chunk 一维索引(≥ 0)。</param>
        /// <exception cref="ArgumentException"><paramref name="levelId"/> 为空 / <paramref name="chunkIndex"/> 为负。</exception>
        public static string NavChunkFileName(string levelId, int chunkIndex)
        {
            if (string.IsNullOrEmpty(levelId))
                throw new ArgumentException("level_id 不得为空", nameof(levelId));
            if (chunkIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(chunkIndex), $"chunk 索引不得为负(实得 {chunkIndex})");

            // 基准关卡 ⇒ `world_nav_{chunk}.json`(ADR-022 §三/§五 的契约字面);
            // 其他关卡 ⇒ `world_nav_{level_id}_{chunk}.json`(与 `FileNameFor` 的后缀口径同源,
            // 承 story-001 §命名形状的加性扩展)。
            return levelId == LevelCatalog.CanonicalLevelId
                ? "world_nav_" + chunkIndex + ".json"
                : "world_nav_" + levelId + "_" + chunkIndex + ".json";
        }

        /// <summary>该类数据集**是否已有序列化器**。</summary>
        public static bool IsImplemented(WorldDataSetKind kind) => Serializers.ContainsKey(kind);

        /// <summary>
        /// 按类别序列化关卡源(AC-3 的分派面;**可出多份**)。
        /// <para>新增一类 = **加一条 <see cref="Serializers"/> 行 + 一个序列化方法**,
        /// 不改导出器主体。</para>
        /// </summary>
        /// <exception cref="NotSupportedException">该类尚无序列化器(报文点名归属 story)。</exception>
        public static IReadOnlyList<DataSetProduct> SerializeAll(WorldDataSetKind kind, LevelSource src)
        {
            if (src == null) throw new ArgumentNullException(nameof(src));
            if (Serializers.TryGetValue(kind, out Func<LevelSource, IReadOnlyList<DataSetProduct>> serializer))
                return serializer(src);

            string owner = UnimplementedByStory.TryGetValue(kind, out string reason)
                ? reason
                : "归属 story 未登记(闭集与归属表不同步 —— 见 UnimplementedByStory)";
            throw new NotSupportedException(
                $"[关卡工具] 数据集类别 {kind}({BaseName(kind)})尚未实现:{owner}");
        }

        /// <summary>
        /// 按类别序列化**单份**产物(便捷面;多份类别如 `NavChunk` 会抛并指路)。
        /// </summary>
        /// <exception cref="NotSupportedException">该类尚无序列化器 / 该类出多份。</exception>
        public static string Serialize(WorldDataSetKind kind, LevelSource src)
        {
            IReadOnlyList<DataSetProduct> products = SerializeAll(kind, src);
            if (products.Count != 1)
                throw new NotSupportedException(
                    $"[关卡工具] 数据集类别 {kind}({BaseName(kind)})出 **{products.Count} 份**,不是单份 —— " +
                    "请用 SerializeAll / ExportLevelToRepo(单份入口表达不了「每 chunk 一个文件」)");
            return products[0].Json;
        }

        /// <summary>
        /// `kind → 序列化器` 的分派表(**单一出处**)。
        /// <para>⚠️ 键集与 <see cref="UnimplementedByStory"/> 的键集**互补**:并集 = <see cref="All"/>,
        /// 交集 = 空。夹具 `level_source_generalization_test` 钉住这两条。</para>
        /// </summary>
        private static readonly IReadOnlyDictionary<WorldDataSetKind, Func<LevelSource, IReadOnlyList<DataSetProduct>>>
            Serializers =
            new Dictionary<WorldDataSetKind, Func<LevelSource, IReadOnlyList<DataSetProduct>>>
            {
                { WorldDataSetKind.BuildSlots, LevelExporter.ExportBuildSlotsProducts },
                { WorldDataSetKind.Geometry,   LevelExporter.ExportGeometryProducts },
                { WorldDataSetKind.Terrain,    LevelExporter.ExportTerrainProducts },
                { WorldDataSetKind.Ecozones,   LevelExporter.ExportEcozonesProducts },
                { WorldDataSetKind.Poi,        LevelExporter.ExportPoiProducts },
                { WorldDataSetKind.Resources,  LevelExporter.ExportResourcesProducts },
                { WorldDataSetKind.NavChunk,   LevelExporter.ExportNavChunkProducts },
            };
    }
}
