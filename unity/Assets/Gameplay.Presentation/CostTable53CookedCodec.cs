// ADR-035 T2 前置 —— 53 用药代价表烘焙产物(`53_cost_table.cooked.bytes`)的运行期解码器。
//
// 权威来源:
//   ADR-014 §二/§五(运行期读烘焙产物)· CookedFormat.cs(布局契约)
//   写方严格镜像:`Editor.Tools.Bake/CostTableCookedWriter.cs`
//   表构造 / 纵深防御拒收**单一出处**:`DaYiJingCheng.Sim.Consequences.CostTable53.CreateTable`
//     (N-1 致死 / kind 须伤情 / 单射 / §⑥ 过量挂起 / 槽外键)—— 本件**不重做**这些校验,零漂移。
//
// ⚠️ 载荷布局(全部 little-endian;与写方逐字对称):
//    row_count : i32(0 = 空表,**合法** —— S2 的惰性形态)
//    row_count × { harm_kind: i32, band: i32, injury_id: i32, severity_band: i32 }
//
// ⚠️ **行序**:写方按 `(harm_kind, band)` 规范化过;读方按序读入后交 `CreateTable`,
//   **不依赖读入序做任何计算** —— 序只影响诊断输出。
//
// ⚠️ **L3 的一处刻意缺口(如实登记,不假装没有)**:产物里**不含** `max_severity_band`
//   (写方也不写 —— S9 定稿的布局)。⇒ 运行期 `CreateTable` 的 `maxSeverityBand` 实参只能是
//   `null`,**上界检查在运行期不发生**。理由与代价:
//     · 上界是**烘焙期**性质:产物里的每一行都已通过写方对 `max_severity_band` 的检查
//       ⇒ 「合规表」逐行在界内是**构造保证**;
//     · 运行期 L3 剩下的职责是「**结构**非法」(未知伤情 / 致死 / kind 错 / 单射 / 负档),
//       这几条仍全部执行;
//     · 缺口仅在「产物被**人为篡改**且只把 severity_band 顶破上界」这一种形态。
//   ⇒ 要闭合须改产物布局(加 4 字节)+ 重烘。**未做**,因为改的是 S9 已结案的格式。
//
// ⚠️ 任何字段缺失 / 魔数不符 / 长度不符 / 构造拒收 ⇒ 显式 throw(启动期硬失败,E-13 口径)。

using System;
using System.Collections.Generic;
using System.IO;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Consequences;

namespace DaYiJingCheng.Gameplay.Presentation
{
    /// <summary>53 用药代价表烘焙产物的运行期解码器。</summary>
    public static class CostTable53CookedCodec
    {
        /// <summary>产物文件名(= <c>DataBakeMenu.CostTableCookedAssetName</c> 的同值字面串;
        /// 边界程序集不引用编辑期装配,故此处自持常量 —— 承 `DelayTableAddress` 同款注记)。</summary>
        public const string CookedFileName = "53_cost_table.cooked.bytes";

        /// <summary>读代价表。</summary>
        /// <param name="bytes">cooked 字节。</param>
        /// <param name="registry">9 的注册表行(**必需** —— `CreateTable` 的外键 / N-1 / kind 判据都建在它上面)。</param>
        /// <exception cref="ArgumentNullException"><paramref name="registry"/> 为 null。</exception>
        /// <exception cref="InvalidDataException">头部损坏 / 长度不符 / 表构造拒收 —— 启动期硬失败。</exception>
        public static CostTable53DataSet Read(byte[] bytes, IReadOnlyList<DiseaseRegistryEntry> registry)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));

            using (BinaryReader r = CookedReadUtil.OpenChecked(bytes, "53_cost_table", out uint configVersion))
            {
                int rowCount = r.ReadInt32();
                if (rowCount < 0)
                    throw new InvalidDataException(
                        $"[cooked] 53_cost_table 行数 = {rowCount} —— 不得为负(空表须写 0;E-13)");

                var rows = new List<CostTable53.Row>(rowCount);
                for (int i = 0; i < rowCount; i++)
                {
                    int harmKind = r.ReadInt32();
                    int band = r.ReadInt32();
                    int injuryId = r.ReadInt32();
                    int severityBand = r.ReadInt32();
                    rows.Add(new CostTable53.Row(harmKind, band, injuryId, severityBand));
                }

                CookedReadUtil.AssertFullyConsumed(r, "53_cost_table");

                try
                {
                    // maxSeverityBand = null:产物不含该值(见文件头的 L3 缺口登记)。
                    CostTable53.Table table = CostTable53.CreateTable(registry, rows, null);
                    return new CostTable53DataSet(table, configVersion);
                }
                catch (ArgumentException ex)
                {
                    // Sim 侧纵深防御拒收(未知伤情 / 致死 / kind 错 / 单射 / 过量挂起)——
                    // 产物级损坏 ⇒ 统一包成启动期硬失败。
                    throw new InvalidDataException(
                        $"[cooked] 53_cost_table 表构造被拒:{ex.Message}", ex);
                }
            }
        }
    }

    /// <summary>53 用药代价表烘焙产物的解码形。</summary>
    public readonly struct CostTable53DataSet
    {
        /// <summary>代价表(Sim 侧唯一表类型;空表 = 惰性形态,`IsEmpty` 为真)。</summary>
        public readonly CostTable53.Table Table;

        /// <summary>烘焙时从源数据集派生的内容版本号(u32,ADR-014 §五)。</summary>
        public readonly uint ConfigVersion;

        /// <summary>构造解码形。</summary>
        public CostTable53DataSet(CostTable53.Table table, uint configVersion)
        {
            Table = table;
            ConfigVersion = configVersion;
        }
    }
}
