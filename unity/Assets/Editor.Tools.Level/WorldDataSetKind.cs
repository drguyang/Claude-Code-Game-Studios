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
//     ⇒ `FileNameFor(NavChunk, …)` 显式抛,切片命名归 **S5**。

using System;
using System.Collections.Generic;

namespace DaYiJingCheng.EditorTools.Level
{
    /// <summary>ADR-022 §三 的 7 类世界数据集(闭集)。</summary>
    /// <remarks>
    /// <para>序数是**取值的一部分**(用于确定性遍历序);**不得重排、不得插入中间值** ——
    /// 要加第 8 类就追加在末尾。</para>
    /// <para>⚠️ 本枚举的「闭集」是**契约面**闭集,不是「已实现」闭集:今日
    /// <see cref="BuildSlots"/> / <see cref="Geometry"/> / <see cref="Terrain"/> 三有序列化器,
    /// 其余四类由 <see cref="WorldDataSets.UnimplementedByStory"/> 逐条点名归属 story。</para>
    /// </remarks>
    public enum WorldDataSetKind
    {
        /// <summary>建造槽位骨架(S20-20 起)。</summary>
        BuildSlots = 0,

        /// <summary>世界几何(尺寸 + 每格可走性 + `LATTICE_SIZE`)。**S2(story-002)起已实现**。</summary>
        Geometry = 1,

        /// <summary>生态区多边形。归属 = S3。</summary>
        Ecozones = 2,

        /// <summary>POI 定义。归属 = S4。</summary>
        Poi = 3,

        /// <summary>资源点。归属 = S4。</summary>
        Resources = 4,

        /// <summary>地形表(`TERRAIN_TABLE` / `slopeLimit` / `stepOffset` 的逻辑侧)。
        /// **S2(story-002)起已实现**。</summary>
        Terrain = 5,

        /// <summary>导航格(**按 chunk 切片** —— 后缀参数是 chunk,不是 level)。归属 = S5。</summary>
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
        public static readonly IReadOnlyDictionary<WorldDataSetKind, string> UnimplementedByStory =
            new Dictionary<WorldDataSetKind, string>
            {
                { WorldDataSetKind.Ecozones,  "待 S3 产出(生态区多边形 + C4/C5)" },
                { WorldDataSetKind.Poi,       "待 S4 产出(POI 定义导出 + 运行期装载)" },
                { WorldDataSetKind.Resources, "待 S4 产出(资源点导出 + 运行期装载)" },
                { WorldDataSetKind.NavChunk,  "待 S5 产出(导航格按 chunk 切片)" },
            };

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

        /// <summary>该类数据集**是否已有序列化器**。</summary>
        public static bool IsImplemented(WorldDataSetKind kind) => Serializers.ContainsKey(kind);

        /// <summary>
        /// 按类别序列化关卡源(AC-3 的分派面)。
        /// <para>新增一类 = **加一条 <see cref="Serializers"/> 行 + 一个序列化方法**,
        /// 不改本方法体,也不改导出器主体。</para>
        /// </summary>
        /// <exception cref="NotSupportedException">该类尚无序列化器(报文点名归属 story)。</exception>
        public static string Serialize(WorldDataSetKind kind, LevelSource src)
        {
            if (src == null) throw new ArgumentNullException(nameof(src));
            if (Serializers.TryGetValue(kind, out Func<LevelSource, string> serializer))
                return serializer(src);

            string owner = UnimplementedByStory.TryGetValue(kind, out string reason)
                ? reason
                : "归属 story 未登记(闭集与归属表不同步 —— 见 UnimplementedByStory)";
            throw new NotSupportedException(
                $"[关卡工具] 数据集类别 {kind}({BaseName(kind)})尚未实现:{owner}");
        }

        /// <summary>
        /// `kind → 序列化器` 的分派表(**单一出处**)。
        /// <para>⚠️ 键集与 <see cref="UnimplementedByStory"/> 的键集**互补**:并集 = <see cref="All"/>,
        /// 交集 = 空。夹具 `level_source_generalization_test` 钉住这两条。</para>
        /// </summary>
        private static readonly IReadOnlyDictionary<WorldDataSetKind, Func<LevelSource, string>> Serializers =
            new Dictionary<WorldDataSetKind, Func<LevelSource, string>>
            {
                { WorldDataSetKind.BuildSlots, LevelExporter.ExportBuildSlots },
                { WorldDataSetKind.Geometry,   LevelExporter.ExportGeometry },
                { WorldDataSetKind.Terrain,    LevelExporter.ExportTerrain },
            };
    }
}
