// 关卡工具 —— **视觉层**的作者侧声明(C1 的比对对象)。
//
// 权威来源:
//   ADR-022 §四 C1(两层漂移 = **告警**,漂移只影响表现)· §一 的两层世界图
//   ADR-015 §一(确定性整数逻辑层 + 纯视觉层 Terrain;**视觉层采样禁入 sim**)
//   GDD `world-and-ecozones.md` **V-6-1 / V-6-2 / V-6-6 / V-6-7**(视觉是 POI 与边界的主要表达通道)
//
// ═══════════════════════════════════════════════════════════════════════════
// 为什么 C1 需要一个「声明」而不是直接读 Terrain
// ═══════════════════════════════════════════════════════════════════════════
// ADR-015 明写**视觉层采样禁入 sim** —— 但这里不是 sim:是**编辑期工具**,读 Terrain 完全合法。
// 问题在于**今天没有 Terrain 资产**(`World.unity` 只有 `Directional Light` + `Ground`)。
// ⇒ 与 C2' 同一处取「乙路」:先钉住**形状**,资产到位后由 reader 填。
//
// ⚠️ **本件读的是「声明」,不是 Terrain 的 float**。理由:C1 的判据是「不矛盾」,
//    而拿 float 高度去比整数占用格,必须先定一个**高度阈值** —— 那个阈值是数值轮的事。
//    用**整数毫米**声明"这一格视觉上有东西/没东西",判据就退化成布尔比较,
//    不再引入第二个待裁的数值。Terrain reader 落地时负责把 float 高度过阈值转成这张表。

using System;
using System.Collections.Generic;

namespace DaYiJingCheng.EditorTools.Level
{
    /// <summary>
    /// 视觉层的**逐格占用声明**(整数毫米;`0` = 视觉上这格是空的)。
    /// <para>⚠️ 它只承载「有没有实体、多高」,**不承载材质 / 颜色 / 贴图** ——
    /// 那些是美术资产面,不是一致性检查的输入。</para>
    /// </summary>
    public sealed class VisualLayerSource
    {
        /// <summary>逐格的视觉高度(毫米;长度须 = 世界格数;索引 = `(x*H+y)*D+z`)。</summary>
        public readonly IReadOnlyList<int> CellHeightMm;

        /// <summary>世界格总数(用于长度对账)。</summary>
        public readonly int TotalCells;

        /// <summary>构造逐格视觉高度声明。</summary>
        /// <param name="cellHeightMm">逐格高度(毫米;`≥ 0`)。</param>
        /// <param name="totalCells">世界格数(须与数组长度相等)。</param>
        /// <exception cref="ArgumentNullException"><paramref name="cellHeightMm"/> 为 null。</exception>
        /// <exception cref="ArgumentException">长度不符 / 含负值。</exception>
        public VisualLayerSource(IReadOnlyList<int> cellHeightMm, int totalCells)
        {
            if (cellHeightMm == null) throw new ArgumentNullException(nameof(cellHeightMm));
            if (totalCells < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(totalCells), $"世界格数 = {totalCells} 须 ≥ 1");
            if (cellHeightMm.Count != totalCells)
                throw new ArgumentException(
                    $"视觉层数组长度 = {cellHeightMm.Count},须等于世界格数 {totalCells} —— " +
                    "长度不齐 = 视觉层与逻辑层不是同一张图纸(C1 会比到别人的格上)", nameof(cellHeightMm));

            var copy = new int[cellHeightMm.Count];
            for (int i = 0; i < cellHeightMm.Count; i++)
            {
                if (cellHeightMm[i] < 0)
                    throw new ArgumentException(
                        $"视觉层第 {i} 格高度 = {cellHeightMm[i]} mm 不得为负 —— " +
                        "「地下」在本声明里不表达(地表是格面的基准);负值只会是量纲写错",
                        nameof(cellHeightMm));
                copy[i] = cellHeightMm[i];
            }

            CellHeightMm = Array.AsReadOnly(copy);
            TotalCells = totalCells;
        }

        /// <summary>该格视觉上是否有实体(高度 &gt; 0)。</summary>
        /// <param name="index">一维索引。</param>
        public bool HasSolidAt(int index) => CellHeightMm[index] > 0;
    }
}
