// 关卡工具 —— 槽位骨架的**确定性导出器**(ADR-022 §三)。
//
// 权威来源:
//   ADR-022 §三(`assets/data/world_buildslots.json`,整数格 / 枚举一律整数,`Fix` 写字符串 —— 本表无 Fix)
//   §②(导出前跑一致性检查;失败 = 显式 throw,不降级为告警)
//   ADR-014 §二(作者态源文件是烘焙的**唯一**输入;运行期零 JSON 解析器)
//
// ⚠️ **确定性**:字段序固定 · 槽位按 `(x,y,z)` 升序 · 缩进固定 · LF 行尾 · 零本地时间 /
//   零随机 / 零字典迭代序 ⇒ 同一 `LevelSource` 两次导出**逐字节相同**。
//
// ⚠️ **本文件是 `assets/data/world_buildslots.json` 的唯一作者** —— 手写该 JSON 违反
//    ADR-022 §二(逻辑层「只能由关卡工具编写并导出」)。合法产法:
//      batch(基准关卡 ⇒ `assets/data/`):`Unity -batchmode -projectPath unity -executeMethod
//              DaYiJingCheng.EditorTools.Level.LevelExporter.ExportP0ClinicToRepo`
//      batch(第二关卡 ⇒ `unity/Logs/level-tool/`,gitignored 暂存区):
//              `… -executeMethod …LevelExporter.ExportScaffoldLevelToStaging`
//      代码:`LevelExporter.ExportLevelToRepo(repoRoot, levelId)` / `ExportLevelToStaging(…)`。
//
// 🔁 **2026-10-29(story-001)泛化**:此前本类只有 `ExportToRepo(repoRoot)`:
//    关卡**写死** `P0ClinicLevel.Define()`、文件名**写死** `FileName` 常量 ⇒ 第二关卡 / 第二类数据集
//    都**没有入口**。现在两条都由单一出处给出 —— 关卡走 `LevelCatalog`,文件名走
//    `WorldDataSets.FileNameFor(kind, levelId)`;新增一类数据集**不改本类主体**
//    (序列化器登记在 `WorldDataSets.Serializers`,本类只提供 `BuildSlots` 的那一个)。

using System;
using System.Collections.Generic;
using System.Globalization;   // InvariantCulture —— 跨区域性逐字节确定性(A-18)
using System.IO;
using System.Text;
using DaYiJingCheng.Sim.Contracts;   // Fix(地形表的 K_accel / K_decel;ADR-014 §二)
using DaYiJingCheng.Sim.World;       // SlotType(唯一真源)
using UnityEngine;   // Application.dataPath(本程序集为编辑期工具,不进玩家构建)

namespace DaYiJingCheng.EditorTools.Level
{
    /// <summary>槽位骨架的确定性 JSON 导出器。</summary>
    public static class LevelExporter
    {
        // 🔁 **2026-10-29(story-001)此处删除了 `public const string FileName = "world_buildslots.json";`。**
        //    理由:多关卡之后「文件名」不再是常量 —— 它由 `WorldDataSets.FileNameFor(kind, levelId)` 给出。
        //    留着一个恒定返回基准关卡名的常量 = **两个出处**,而它们的分离是**静默**的
        //    (基准关卡改 id 时,常量不会跟着变)。
        //    ⇒ 名字的字面量现在**只**在 `WorldDataSets.BaseName` 出现一次。
        //    ⚠️ 本类**不**再提供等价的公开访问器:全仓对旧 `FileName` 的消费方 = **零**
        //    (唯一用法在本类自己的两处日志里),再挂一个无消费方的属性只会重演
        //    2026-10-28 评审 A-21 删掉的「零消费方死 API」。

        /// <summary>本表的 schema_version(单一出处 = 本常量;读方 codec 的期望值与它同值)。</summary>
        /// <remarks>⚠️ <b>2026-10-28(S20-20 挂标 #2):1 → 2</b> —— 行对象新增 `module_id`
        /// (开档既有家具的模块 id)。旧 schema 1 的产物**无**该字段 ⇒ 读方必须拒
        /// (`WorldBuildSlotsCookedCodec.ExpectedSchemaVersion` 同值 2)。
        /// ⚠️ 三处**必须同时**改:本常量 / `WorldBuildSlotsCookedCodec.ExpectedSchemaVersion` /
        /// `WorldBuildSlotsBinder.RowKeys` —— 前两者由绑定器的 `ReadSchemaVersion` 交叉校验
        /// (它以 codec 常量为期望值),故本常量若漏改,真种子烘焙**当场**报「schemaVersion ≠ 期望」。</remarks>
        public const uint BuildSlotsSchemaVersion = 2u;

        /// <summary>`world_geometry.json` 的 schema_version(单一出处;读侧 codec 的期望值与它同值)。</summary>
        /// <remarks>⚠️ **每类数据集各有一个 schema 号**(承 ADR-014 §五 的 per-product 口径):
        /// 三类的布局各自演进,共用一个号会让「改 A 却被迫升 B 的号」成为常态,
        /// 而升号 = 旧产物被拒收 = 一次无谓的重烘。</remarks>
        public const uint GeometrySchemaVersion = 1u;

        /// <summary>`world_ecozones.json` 的 schema_version(单一出处;读侧 codec 的期望值与它同值)。</summary>
        public const uint EcozonesSchemaVersion = 1u;

        /// <summary>`world_terrain.json` 的 schema_version(单一出处;读侧 codec 的期望值与它同值)。</summary>
        public const uint TerrainSchemaVersion = 1u;

        /// <summary>`world_poi.json` 的 schema_version(单一出处;读侧 codec 的期望值与它同值)。</summary>
        public const uint PoiSchemaVersion = 1u;

        /// <summary>`world_resources.json` 的 schema_version(单一出处;读侧 codec 的期望值与它同值)。</summary>
        public const uint ResourcesSchemaVersion = 1u;


        // ⚠️ **此处曾有 `SlotTypeNames` / `DeriveSlotTypeNames()`**(名字表 + 名字→序数派生)。
        //    2026-10-28 代码评审 **A-21** 后删除,两半各有归处 ——
        //      · **导出侧的名字**不再需要表:`TypeName()` 直接 `Enum.GetName`(见下);
        //      · **绑定侧的名字→序数**唯一出处 = `Editor.Tools.Bake.SlotTypeVocabulary`
        //        (它派生自同一个 `Sim.World.SlotType`,并带 `[1,30]` 位域守卫)。
        //    删除理由 = 它是**零消费方的死 API**(全仓 grep 零命中),留着等于给「再来一份手写表」
        //    留下榜样。若将来导出侧真的需要闭集文案,请复用 `SlotTypeVocabulary`,
        //    **不要在本文件重建第二份**。

        /// <summary>把关卡源序列化成确定性 JSON 文本(不落盘)。</summary>
        /// <remarks>⚠️ 导出前**先跑一致性检查**(ADR-022 §②):硬失败 ⇒ `InvalidOperationException`。</remarks>
        public static string ExportBuildSlots(LevelSource src)
        {
            if (src == null) throw new ArgumentNullException(nameof(src));

            LevelConsistencyCheck.CheckOrThrow(src);

            // 全序 = (x, y, z) 升序。声明序不承载语义(重排 = 零信息)。
            var ordered = new List<SlotSpec>(src.Slots);
            ordered.Sort((a, b) =>
            {
                int cmp = a.X.CompareTo(b.X);
                if (cmp != 0) return cmp;
                cmp = a.Y.CompareTo(b.Y);
                if (cmp != 0) return cmp;
                return a.Z.CompareTo(b.Z);
            });

            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append("  \"schema_version\": ").Append(Num((int)BuildSlotsSchemaVersion)).Append(",\n");
            sb.Append("  \"level_id\": ").Append(Quote(src.LevelId)).Append(",\n");
            sb.Append("  \"world_size\": [")
              .Append(Num(src.Width)).Append(", ")
              .Append(Num(src.Height)).Append(", ")
              .Append(Num(src.Depth)).Append("],\n");
            sb.Append("  \"slots\": [\n");

            for (int i = 0; i < ordered.Count; i++)
            {
                SlotSpec s = ordered[i];
                sb.Append("    {\"cell\": [")
                  .Append(Num(s.X)).Append(", ").Append(Num(s.Y)).Append(", ").Append(Num(s.Z))
                  .Append("], \"slot_type\": ").Append(Quote(TypeName(s.Type)))
                  .Append(", \"baked_initial\": ").Append(s.BakedInitial ? "true" : "false");
                // ⚠️ `module_id` **恒出现**(schema 2):`baked_initial = false` 的行写 `null`,
                //    而不是省略字段 —— 「省略」与「写了 null」在读者眼里是两件事
                //    (前者像作者遗漏,后者是明确声明「本格无既有家具」),与
                //    `baked_initial` 必填的既有口径同源。
                sb.Append(", \"module_id\": ");
                if (s.BakedInitial)
                    sb.Append(Num(s.ModuleId));
                else
                    sb.Append("null");
                sb.Append('}')
                  .Append(i + 1 < ordered.Count ? ",\n" : "\n");
            }

            sb.Append("  ]\n");
            sb.Append("}\n");
            return sb.ToString();
        }

        /// <summary>
        /// 序列化 `world_geometry.json`(story-002 AC-1)。
        /// <para>形状 = `schema_version` + `level_id` + `world_size` + `lattice_size_mm` + `walkable`。</para>
        /// <para>⚠️ **零浮点**:全部字段都是 JSON 整数(AC-1 的判据;`LATTICE_SIZE` 以**毫米整数**承载 ——
        /// ADR-015 §三 的格边量子就是 mm)。</para>
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// 关卡未声明可走性(`Walkable == null`)或未声明 `LATTICE_SIZE` ——
        /// **不合成「全可走」**:那会让 AC-4 的「可走性来自产物」在任何夹具上不可判别
        /// (同值变异),也把「没数据」伪装成「全都走得通」。</exception>
        public static string ExportGeometry(LevelSource src)
        {
            if (src == null) throw new ArgumentNullException(nameof(src));

            LevelConsistencyCheck.CheckOrThrow(src);

            if (src.Walkable == null)
                throw new InvalidOperationException(
                    $"[关卡工具] {src.LevelId} 未声明每格可走性 ⇒ 拒绝导出 world_geometry。" +
                    "不合成「全可走」:合成值与本关卡的真空值不可分辨," +
                    "「可走性来自产物」就失去观察面(AC-4)");
            if (src.LatticeSizeMm <= 0)
                throw new InvalidOperationException(
                    $"[关卡工具] {src.LevelId} 未声明 LATTICE_SIZE(= {src.LatticeSizeMm} mm ⇒ 须 > 0;GDD F-6-1)");

            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append("  \"schema_version\": ").Append(Num((int)GeometrySchemaVersion)).Append(",\n");
            sb.Append("  \"level_id\": ").Append(Quote(src.LevelId)).Append(",\n");
            sb.Append("  \"world_size\": [")
              .Append(Num(src.Width)).Append(", ")
              .Append(Num(src.Height)).Append(", ")
              .Append(Num(src.Depth)).Append("],\n");
            sb.Append("  \"lattice_size_mm\": ").Append(Num(src.LatticeSizeMm)).Append(",\n");
            sb.Append("  \"walkable\": ");
            AppendFlatIntArray(sb, src.Walkable, perLine: src.Height * src.Depth);
            sb.Append("\n}\n");
            return sb.ToString();
        }

        /// <summary>
        /// 序列化 `world_terrain.json`(story-002 AC-2)。
        /// <para>形状 = `terrain_table`(`terrain_id → {k_speed, k_accel, k_decel}`)
        /// + `cell_terrain_id` + `slope_limit_deg_x100` + `step_offset_mm`。</para>
        /// <para>⚠️ **`K_accel` / `K_decel` 写字符串**(`"1"` / `"3/4"`)—— ADR-014 §二:
        /// `Fix` 字段物理上不可能住 JSON 数字,写数字等于给浮点泄漏开后门。</para>
        /// </summary>
        /// <exception cref="InvalidOperationException">关卡未声明地形表。</exception>
        public static string ExportTerrain(LevelSource src)
        {
            if (src == null) throw new ArgumentNullException(nameof(src));

            LevelConsistencyCheck.CheckOrThrow(src);

            if (src.Terrain == null)
                throw new InvalidOperationException(
                    $"[关卡工具] {src.LevelId} 未声明 TERRAIN_TABLE ⇒ 拒绝导出 world_terrain" +
                    "(F-6-2:K_TERRAIN_MAX = max over 空集**无定义**,装载期硬失败)");

            TerrainSource t = src.Terrain;

            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append("  \"schema_version\": ").Append(Num((int)TerrainSchemaVersion)).Append(",\n");
            sb.Append("  \"level_id\": ").Append(Quote(src.LevelId)).Append(",\n");
            // ⚠️ `world_size` **重复出现在两个产物里**,这是刻意的:它不是「第二真源」,
            //    而是让 `cell_terrain_id` 在**烘焙期**就能自校验(否则「数组长度对不对」只能等装载期
            //    拿几何侧来比),且装载期可以断言两份产物的尺寸**相等** —— 那正是
            //    「地形是从另一版关卡烘的」这类漂移的唯一抓法。
            sb.Append("  \"world_size\": [")
              .Append(Num(src.Width)).Append(", ")
              .Append(Num(src.Height)).Append(", ")
              .Append(Num(src.Depth)).Append("],\n");
            // 表按 `terrain_id` 升序 ⇒ 声明序重排不改变字节流(与 slots 的 (x,y,z) 全序同源)。
            var rows = new List<TerrainRowSpec>(t.Rows);
            rows.Sort((a, b) => a.TerrainId.CompareTo(b.TerrainId));

            sb.Append("  \"terrain_table\": [\n");
            for (int i = 0; i < rows.Count; i++)
            {
                TerrainRowSpec r = rows[i];
                sb.Append("    {\"terrain_id\": ").Append(Num(r.TerrainId))
                  .Append(", \"name\": ").Append(Quote(r.Name))
                  .Append(", \"k_speed\": ").Append(Num(r.KSpeed))
                  .Append(", \"k_accel\": ").Append(Quote(FixLiteral(r.KAccel)))
                  .Append(", \"k_decel\": ").Append(Quote(FixLiteral(r.KDecel)))
                  .Append('}')
                  .Append(i + 1 < rows.Count ? ",\n" : "\n");
            }
            sb.Append("  ],\n");
            sb.Append("  \"cell_terrain_id\": ");
            AppendFlatIntArray(sb, t.CellTerrainId, perLine: src.Height * src.Depth);
            sb.Append(",\n");
            sb.Append("  \"slope_limit_deg_x100\": ").Append(Num(t.SlopeLimitDegX100)).Append(",\n");
            sb.Append("  \"step_offset_mm\": ").Append(Num(t.StepOffsetMm)).Append("\n");
            sb.Append("}\n");
            return sb.ToString();
        }

        /// <summary>
        /// 序列化 `world_ecozones.json`(story-003 AC-1)。
        /// <para>形状 = `schema_version` + `level_id` + `zones: [{ecozone_id, name, vertices}]`。</para>
        /// <para>⚠️ **顶点写 `[x, z]` 二元组,不是三元组** —— GDD F-6-3 §四 明写「横轴是 z,
        /// 不是 y」且 y 不参与判定。把恒 0 的 y 写进文件会造出一个「看起来像字段的常量」,
        /// 读者会以为它有意义(本仓反复清的形态)。</para>
        /// <para>⚠️ **零浮点**;`ecozone_id` 唯一与 C4 五项由 <see cref="LevelConsistencyCheck"/> 统一跑。</para>
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// 关卡未声明生态区 / 任一 C4 违例(GDD AC-6-19 五类退化)。</exception>
        public static string ExportEcozones(LevelSource src)
        {
            if (src == null) throw new ArgumentNullException(nameof(src));

            LevelConsistencyCheck.CheckOrThrow(src);

            if (src.Ecozones == null)
                throw new InvalidOperationException(
                    $"[关卡工具] {src.LevelId} 未声明生态区 ⇒ 拒绝导出 world_ecozones。" +
                    "不合成空表:空表与「未声明」在产物上不可分,而 `EcozoneOf` 恒 None 正是 story-003 要消灭的状态");

            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append("  \"schema_version\": ").Append(Num((int)EcozonesSchemaVersion)).Append(",\n");
            sb.Append("  \"level_id\": ").Append(Quote(src.LevelId)).Append(",\n");
            // 行序 = ecozone_id 升序 ⇒ 作者重排声明不改变字节流(与 slots / terrain_table 同源)。
            List<EcozoneZoneSpec> zones = src.Ecozones.SortedById();

            sb.Append("  \"zones\": [\n");
            for (int i = 0; i < zones.Count; i++)
            {
                EcozoneZoneSpec z = zones[i];
                sb.Append("    {\"ecozone_id\": ").Append(Num(z.Id))
                  .Append(", \"name\": ").Append(Quote(z.Name))
                  .Append(", \"vertices\": [");
                for (int v = 0; v < z.Vertices.Length; v++)
                {
                    if (v > 0) sb.Append(", ");
                    sb.Append('[').Append(Num(z.Vertices[v].X)).Append(", ")
                      .Append(Num(z.Vertices[v].Z)).Append(']');
                }
                sb.Append("]}")
                  .Append(i + 1 < zones.Count ? ",\n" : "\n");
            }
            sb.Append("  ]\n");
            sb.Append("}\n");
            return sb.ToString();
        }

        /// <summary>
        /// 序列化 `world_poi.json`(story-004 AC-1)。
        /// <para>形状 = `schema_version` + `level_id` + `level_size` +
        /// `pois: [{poi_id, cell: [x, y, z], type, guard_ref}]`。</para>
        /// <para>⚠️ **`level_size` 也写进去** —— 绑定器靠它把「格越界」判在**烘焙期**,
        /// 而不是等到运行期填格时才发现下标落空。这与 `world_geometry` 冗余写尺寸同源。</para>
        /// <para>⚠️ **只出定义**;`PoiState` 不出现在本文件能表达的任何类型里(ADR-021 / ADR-022 §六)。</para>
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// 关卡未声明 POI / 任一定义的格越界。</exception>
        public static string ExportPoi(LevelSource src)
        {
            if (src == null) throw new ArgumentNullException(nameof(src));

            LevelConsistencyCheck.CheckOrThrow(src);

            if (src.Poi == null)
                throw new InvalidOperationException(
                    $"[关卡工具] {src.LevelId} 未声明 POI ⇒ 拒绝导出 world_poi。" +
                    "不合成空表:空表与「未声明」在产物上不可分,而状态机的 id 集会零成员、" +
                    "POI 状态变更事件永不发出 —— 那正是 story-004 要消灭的状态");

            src.Poi.ValidateAgainstLevel(src.Width, src.Height, src.Depth);

            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append("  \"schema_version\": ").Append(Num((int)PoiSchemaVersion)).Append(",\n");
            sb.Append("  \"level_id\": ").Append(Quote(src.LevelId)).Append(",\n");
            sb.Append("  \"level_size\": [").Append(Num(src.Width)).Append(", ")
              .Append(Num(src.Height)).Append(", ").Append(Num(src.Depth)).Append("],\n");

            // 行序 = poi_id 升序 ⇒ 作者重排声明不改变字节流。
            List<PoiSpec> pois = src.Poi.SortedById();

            sb.Append("  \"pois\": [\n");
            for (int i = 0; i < pois.Count; i++)
            {
                PoiSpec p = pois[i];
                sb.Append("    {\"poi_id\": ").Append(Num(p.PoiId))
                  .Append(", \"cell\": [").Append(Num(p.Cell.X)).Append(", ")
                  .Append(Num(p.Cell.Y)).Append(", ").Append(Num(p.Cell.Z)).Append(']')
                  .Append(", \"type\": ").Append(Num(p.Type))
                  .Append(", \"guard_ref\": ").Append(Num(p.GuardRef))
                  .Append('}')
                  .Append(i + 1 < pois.Count ? ",\n" : "\n");
            }
            sb.Append("  ]\n");
            sb.Append("}\n");
            return sb.ToString();
        }

        /// <summary>
        /// 序列化 `world_resources.json`(story-004 AC-2)。
        /// <para>形状 = `schema_version` + `level_id` + `level_size` +
        /// `resources: [{resource_id, cell: [x, y, z], ecozone_id}]`。</para>
        /// <para>⚠️ **格与 POI 格冲突 ⇒ 硬失败**(AC-2③):同一格既是 POI 又是资源点,
        /// 在运行期表现为 `PoiGrid[i]` 与 `ResourceGrid[i]` 同时非 `-1`,而两条查询各自
        /// 「看起来都对」—— 那是**静默**的数据错配,只能在导出期拦。</para>
        /// <para>⚠️ 本方法**不**校验 `ecozone_id` 是否存在(跨数据集门归 S6 的 C 系列;
        /// 理由见 `ResourceSource.cs` 文件头)。</para>
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// 关卡未声明资源 / 任一格越界 / 格与 POI 格冲突。</exception>
        public static string ExportResources(LevelSource src)
        {
            if (src == null) throw new ArgumentNullException(nameof(src));

            LevelConsistencyCheck.CheckOrThrow(src);

            if (src.Resources == null)
                throw new InvalidOperationException(
                    $"[关卡工具] {src.LevelId} 未声明资源点 ⇒ 拒绝导出 world_resources。" +
                    "不合成空表(理由同 world_poi:空表与「未声明」不可分,而资源格全 -1 " +
                    "正是 story-004 要消灭的状态)");

            src.Resources.ValidateAgainstLevel(src.Width, src.Height, src.Depth);

            // ── AC-2③:资源格不得与 POI 格重复 ──────────────────────────────────
            if (src.Poi != null)
            {
                var poiCells = new HashSet<(int, int, int)>();
                for (int i = 0; i < src.Poi.Pois.Count; i++)
                    poiCells.Add((src.Poi.Pois[i].Cell.X, src.Poi.Pois[i].Cell.Y, src.Poi.Pois[i].Cell.Z));

                var clashes = new List<string>();
                for (int i = 0; i < src.Resources.Resources.Count; i++)
                {
                    ResourceSpec r = src.Resources.Resources[i];
                    if (poiCells.Contains((r.Cell.X, r.Cell.Y, r.Cell.Z)))
                        clashes.Add($"resources[{i}] resource_id={r.ResourceId} 落在 POI 格 " +
                                    $"({r.Cell.X},{r.Cell.Y},{r.Cell.Z})");
                }
                if (clashes.Count > 0)
                    throw new InvalidOperationException(
                        "[关卡工具] 资源点与 POI 格冲突(一格不能既是 POI 又是资源点):" +
                        string.Join(";", clashes));
            }

            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append("  \"schema_version\": ").Append(Num((int)ResourcesSchemaVersion)).Append(",\n");
            sb.Append("  \"level_id\": ").Append(Quote(src.LevelId)).Append(",\n");
            sb.Append("  \"level_size\": [").Append(Num(src.Width)).Append(", ")
              .Append(Num(src.Height)).Append(", ").Append(Num(src.Depth)).Append("],\n");

            List<ResourceSpec> rows = src.Resources.SortedByKey();

            sb.Append("  \"resources\": [\n");
            for (int i = 0; i < rows.Count; i++)
            {
                ResourceSpec r = rows[i];
                sb.Append("    {\"resource_id\": ").Append(Num(r.ResourceId))
                  .Append(", \"cell\": [").Append(Num(r.Cell.X)).Append(", ")
                  .Append(Num(r.Cell.Y)).Append(", ").Append(Num(r.Cell.Z)).Append(']')
                  .Append(", \"ecozone_id\": ").Append(Num(r.EcozoneId))
                  .Append('}')
                  .Append(i + 1 < rows.Count ? ",\n" : "\n");
            }
            sb.Append("  ]\n");
            sb.Append("}\n");
            return sb.ToString();
        }

        /// <summary>
        /// 一维整数数组的确定性排版:每 <paramref name="perLine"/> 项一行(缩进 4 空格)。
        /// <para>⚠️ `perLine` 取 `Height × Depth` ⇒ 每个 x 一层一行,切片与索引公式
        /// `(x*H + y)*D + z` 的**连续段**重合 —— 作者读 JSON 时看到的行边界就是 x 的边界。</para>
        /// </summary>
        private static void AppendFlatIntArray(StringBuilder sb, IReadOnlyList<int> values, int perLine)
        {
            if (perLine < 1) perLine = 1;
            sb.Append("[\n");
            for (int i = 0; i < values.Count; i++)
            {
                if (i % perLine == 0) sb.Append("    ");
                sb.Append(Num(values[i]));
                bool last = i + 1 == values.Count;
                if (!last) sb.Append(", ");
                if ((i + 1) % perLine == 0 || last) sb.Append('\n');
            }
            sb.Append("  ]");
        }

        /// <summary>
        /// `Fix` → **可被 `FixParse` 读回**的确定性字面量(ADR-014 §二:`Fix` 在 JSON 里写字符串)。
        /// <para>整值(`raw % 65536 == 0`)⇒ `"1"`;否则 ⇒ `"raw/65536"`(分子分母皆整数,
        /// `FixParse.FromRatio` 精确还原 ⇒ 往返**逐位**无损)。</para>
        /// <para>⚠️ 刻意**不**输出 `"0.75"` 这类小数写法 —— `FixParse` 会拒它,而且那正是本仓
        /// 反复抓到的浮点泄漏形态。</para>
        /// </summary>
        private static string FixLiteral(Fix value)
        {
            const long one = Fix.OneRaw;   // 1.0 的 raw 表示(Q16.16)
            long raw = value.Raw;
            if (raw % one == 0) return (raw / one).ToString(CultureInfo.InvariantCulture);
            return raw.ToString(CultureInfo.InvariantCulture) + "/" + one.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 按**数据集类别**序列化(AC-3 的分派面)。
        /// <para>本方法**不含任何 kind 分支** —— 分派表在 `WorldDataSets.Serializers`;
        /// 新增一类只在那里加一行,本类主体一字不动。</para>
        /// </summary>
        /// <exception cref="NotSupportedException">该类尚无序列化器。</exception>
        public static string Export(WorldDataSetKind kind, LevelSource src)
            => WorldDataSets.Serialize(kind, src);

        // ⚠️ **此处曾有二参 `ExportTo(string path, LevelSource src)`**(隐含 `BuildSlots`),实现轮内删除。
        //    与上面删掉的 `FileName`、下面 `LevelCatalog.TryDefine` 同一条纪律(2026-10-28 评审 A-21):
        //    **零消费方的死 API**(`grep -rn 'ExportTo(' --include=*.cs` 排除本文件后零命中)。
        //    保留它还有个具体害处:它是「类别」这一概念的**第二个隐式默认值** ——
        //    调用点看不出自己在导出哪一类,而 AC-3 的全部意义就是让类别**在调用点显式**。

        /// <summary>按类别写盘到任意路径。</summary>
        /// <exception cref="NotSupportedException">该类尚无序列化器。</exception>
        public static void ExportTo(WorldDataSetKind kind, string path, LevelSource src)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("路径为空", nameof(path));
            string dir = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            // UTF-8 **无 BOM**(BOM 会让字节流随编辑器差异漂移,破坏 ConfigVersion 的可复算性)。
            File.WriteAllText(path, Export(kind, src), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }

        /// <summary>
        /// 导出某关卡的**全部已实现类别**到 `<repoRoot>/assets/data/&lt;WorldDataSets.FileNameFor&gt;`。
        /// <para>基准关卡 ⇒ `world_{kind}.json`(契约字面);其他关卡 ⇒ `world_{kind}_{level_id}.json`。</para>
        /// <para>🔁 **2026-10-29(story-002)**:此前只导 `BuildSlots`。几何 / 地形落地后,
        /// 「导出一个关卡」若仍只写槽位,调用方就得记住「还要再导两次」——
        /// 那是一条**必须靠记忆维持**的接线,漏一次就是「产物里少一份、装载期硬失败」。
        /// 现在循环 `WorldDataSets.All` 的已实现子集,新增一类**不改本方法**。</para>
        /// </summary>
        /// <returns>实际写出的文件名(顺序 = <see cref="WorldDataSets.All"/> 的声明序)。</returns>
        /// <exception cref="ArgumentException"><paramref name="levelId"/> 不在 <see cref="LevelCatalog"/> 内。</exception>
        /// <exception cref="InvalidOperationException">一致性检查硬失败 / 某类所需的声明缺失。</exception>
        public static IReadOnlyList<string> ExportLevelToRepo(string repoRoot, string levelId)
        {
            if (string.IsNullOrEmpty(repoRoot)) throw new ArgumentNullException(nameof(repoRoot));
            LevelSource src = LevelCatalog.Define(levelId);

            var written = new List<string>();
            foreach (WorldDataSetKind kind in WorldDataSets.All)
            {
                if (!WorldDataSets.IsImplemented(kind)) continue;
                string fileName = WorldDataSets.FileNameFor(kind, levelId);
                ExportTo(kind, Path.Combine(repoRoot, "assets", "data", fileName), src);
                written.Add(fileName);
            }
            return written;
        }

        /// <summary>
        /// 导出某关卡到**暂存区** `<repoRoot>/unity/Logs/level-tool/&lt;文件名&gt;`(`.gitignore:257` 已忽略整树)。
        /// <para>⚠️ **为什么需要它**:非基准关卡写进 `assets/data/` 会变成一份**没有消费者**的作者态数据
        /// (烘焙入口按 `WorldBuildSlotsBinder.FileName` 单点读基准关卡的文件,永远不会读它)
        /// ⇒ 盘上多一份「看起来像数据、其实没人读」的 JSON。暂存区保证「导出确实跑通」这件事
        /// 可验证,又不在作者态目录留垃圾。</para>
        /// </summary>
        /// <exception cref="ArgumentException"><paramref name="levelId"/> 不在 <see cref="LevelCatalog"/> 内。</exception>
        public static IReadOnlyList<string> ExportLevelToStaging(string repoRoot, string levelId)
        {
            if (string.IsNullOrEmpty(repoRoot)) throw new ArgumentNullException(nameof(repoRoot));
            LevelSource src = LevelCatalog.Define(levelId);

            var written = new List<string>();
            foreach (WorldDataSetKind kind in WorldDataSets.All)
            {
                if (!WorldDataSets.IsImplemented(kind)) continue;
                string fileName = WorldDataSets.FileNameFor(kind, levelId);
                ExportTo(kind, Path.Combine(repoRoot, "unity", "Logs", "level-tool", fileName), src);
                written.Add(fileName);
            }
            return written;
        }

        /// <summary>导出**基准关卡**到 `<repoRoot>/assets/data/`(全部已实现类别)。</summary>
        public static IReadOnlyList<string> ExportToRepo(string repoRoot)
            => ExportLevelToRepo(repoRoot, LevelCatalog.CanonicalLevelId);

        /// <summary>batch 入口:导出**基准关卡**到本工程所在仓库的 `assets/data/`。</summary>
        /// <remarks>⚠️ `Application.dataPath` 仅用于推导仓库根(`unity/Assets` 的上两级)。</remarks>
        public static void ExportP0ClinicToRepo()
        {
            string repoRoot = RepositoryRoot();
            IReadOnlyList<string> written = ExportToRepo(repoRoot);
            for (int i = 0; i < written.Count; i++)
                Debug.Log($"[关卡工具] 已导出 {written[i]} → " +
                          Path.Combine(repoRoot, "assets", "data", written[i]));
        }

        /// <summary>batch 入口:导出**第二关卡(脚手架)**到 `unity/Logs/level-tool/`(gitignored 暂存区)。</summary>
        /// <remarks>AC-5 要求「至少两个关卡各有一条可跑的 batch 命令」;本入口是第二条。
        /// ⚠️ 不写 `assets/data/` 的理由见 <see cref="ExportLevelToStaging"/>。</remarks>
        public static void ExportScaffoldLevelToStaging()
        {
            string repoRoot = RepositoryRoot();
            IReadOnlyList<string> written = ExportLevelToStaging(repoRoot, ScaffoldSecondLevel.LevelId);
            for (int i = 0; i < written.Count; i++)
                Debug.Log($"[关卡工具] 已导出(暂存){ScaffoldSecondLevel.LevelId} → " +
                          Path.Combine(repoRoot, "unity", "Logs", "level-tool", written[i]));
        }

        /// <summary>仓库根(`unity/Assets` 的上两级)。</summary>
        public static string RepositoryRoot()
            => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));

        /// <summary>枚举 → 导出名;`None` 与未定义值一律拒(导出合法值域之外的词是不可读数据)。</summary>
        /// <remarks>⚠️ 名字直接取自 CLR 枚举(`Enum.GetName`),**不查手写表** —— 表与枚举一旦分家,
        /// 「作者写了 `Decor`、导出成 `Bed`」这种错位是静默的(2026-10-28 评审 A-21)。</remarks>
        private static string TypeName(SlotType type)
        {
            if (type != SlotType.None && Enum.IsDefined(typeof(SlotType), type))
                return Enum.GetName(typeof(SlotType), type);
            throw new InvalidOperationException(
                $"[关卡工具] slot_type = {(int)type} 无可读导出名(None / 未定义值不得导出)");
        }

        /// <summary>整数 → **区域性无关**的文本(A-18:导出字节须跨 locale 逐字节相同)。</summary>
        /// <remarks>⚠️ `StringBuilder.Append(int)` 走 `int.ToString()`(当前区域性);
        /// CI 的 `InvariantCulture` 与开发机的某区域性可能产出不同字节 ⇒ `ConfigVersion`
        /// (内容哈希)不同 ⇒ 存档头比对报「配置版本不符」,**错误指向存档而非导出器**。</remarks>
        private static string Num(int value) => value.ToString(CultureInfo.InvariantCulture);

        /// <summary>JSON 字符串转义(本表只可能出现字母数字与下划线,但仍按最小集转义)。</summary>
        private static string Quote(string s)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));
            var sb = new StringBuilder(s.Length + 2);
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default: sb.Append(c); break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }
    }
}
