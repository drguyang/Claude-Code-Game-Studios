// 权威来源:ADR-014 §二/§五(运行期读烘焙产物)· CookedFormat.cs(布局契约)
//          · 写方严格镜像:`Editor.Tools.Bake/OutcomeTableCookedWriter.cs`
//          · 行键编码 / 表构造**单一出处**:`DaYiJingCheng.Sim.Consequences.Outcome53`
//            (`RowKeyOf` / `CreateTable`)—— 本件**不重算**键公式,零漂移。
//
// ⚠️ 载荷布局(全部 little-endian):
//    u_band_count    : i32
//    default_outcome : i32(枚举序数)
//    row_count       : i32(= 轴组合数 × u_band_count;default 行不落行集)
//    row_count × { key: i32, outcome: i32 }
//
// ⚠️ **载荷不携带 has_default 标志** —— 写方不写、读方无从判别。
//    这与 `OutcomeTableBinder.ValidateCoverage` 的硬校验同向:无 default 行的表**烘不出来**
//    ⇒ 一切合法产物的 has_default 恒为 true。本件据此传 `hasDefault: true`,
//    并在 `OutcomeTable53DataSet` 注释里如实登记「该位由烘焙期不变量推出,非载荷携带」。
//    (将来若要读方独立判别,须升 `CookedFormat.FormatVersion` —— 不在本件面内。)
//
// ⚠️ 任何字段缺失 / 魔数不符 / 行数不符 / 枚举越界 ⇒ 显式 throw(启动期硬失败,E-13 口径)。

using System;
using System.Collections.Generic;
using System.IO;
using DaYiJingCheng.Sim.Consequences;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Presentation
{
    /// <summary>53 判定表烘焙产物(<c>53_outcome_table.cooked.bytes</c>)的运行期解码器。
    /// <para>产物解码后直接物化为 <see cref="Outcome53.Table"/>(Sim 侧唯一表类型)—— 本件不另立表类。</para>
    /// </summary>
    public static class OutcomeTableCookedCodec
    {
        /// <summary>轴组合数(P × C × D)—— 从枚举闭集**派生**,不写字面 18。</summary>
        private static int AxisCombinationCount =>
            Enum.GetValues(typeof(Outcome53.JudgmentLane)).Length
            * Enum.GetValues(typeof(Outcome53.CareLane)).Length
            * Enum.GetValues(typeof(Outcome53.RecoveryLane)).Length;

        /// <summary>读判定表。</summary>
        /// <exception cref="InvalidDataException">头部损坏 / 长度不符 / 行数 ≠ 轴组合数 × 档数 /
        /// 枚举序数越界 / 键越界 / 表构造拒收(结局轴行值等)—— 启动期硬失败。</exception>
        public static OutcomeTable53DataSet Read(byte[] bytes)
        {
            using (BinaryReader r = CookedReadUtil.OpenChecked(bytes, "53_outcome_table", out uint configVersion))
            {
                int uBandCount = r.ReadInt32();
                int defaultOrdinal = r.ReadInt32();
                int rowCount = r.ReadInt32();

                if (uBandCount < 1)
                    throw new InvalidDataException(
                        $"[cooked] 53_outcome_table u_band_count={uBandCount} —— 须 ≥ 1" +
                        "(0 会让行键乘子为 0,全部组合键坍缩;E-13)");

                int expectedRowCount = AxisCombinationCount * uBandCount;
                if (rowCount != expectedRowCount)
                    throw new InvalidDataException(
                        $"[cooked] 53_outcome_table 行数不符(载荷 {rowCount},轴组合 {AxisCombinationCount} × " +
                        $"档数 {uBandCount} = {expectedRowCount})—— 产物损坏(E-13)");

                var defaultOutcome = (Outcome53.Outcome)defaultOrdinal;
                if (!Enum.IsDefined(typeof(Outcome53.Outcome), defaultOutcome))
                    throw new InvalidDataException(
                        $"[cooked] 53_outcome_table default_outcome 序数 {defaultOrdinal} ∉ Outcome 闭集(E-13)");

                var rows = new Dictionary<int, Outcome53.Outcome>(rowCount);
                for (int i = 0; i < rowCount; i++)
                {
                    int key = r.ReadInt32();
                    int outcomeOrdinal = r.ReadInt32();

                    if (key < 0 || key >= expectedRowCount)
                        throw new InvalidDataException(
                            $"[cooked] 53_outcome_table 行键 {key} 越出 [0,{expectedRowCount}) —— 产物损坏(E-13)");

                    var outcome = (Outcome53.Outcome)outcomeOrdinal;
                    if (!Enum.IsDefined(typeof(Outcome53.Outcome), outcome))
                        throw new InvalidDataException(
                            $"[cooked] 53_outcome_table 行键 {key} 的 outcome 序数 {outcomeOrdinal} " +
                            "∉ Outcome 闭集(E-13)");

                    if (rows.ContainsKey(key))
                        throw new InvalidDataException(
                            $"[cooked] 53_outcome_table 行键 {key} 重复 —— 产物损坏(E-13)");
                    rows[key] = outcome;
                }

                CookedReadUtil.AssertFullyConsumed(r, "53_outcome_table");

                // hasDefault:见文件头 —— 由烘焙期不变量推出(写方不落该位)。
                try
                {
                    Outcome53.Table table = Outcome53.CreateTable(rows, uBandCount, true, defaultOutcome);
                    return new OutcomeTable53DataSet(table, configVersion);
                }
                catch (ArgumentException ex)
                {
                    // Sim 侧纵深防御(结局轴行值 / 档数为 0)—— 产物级损坏 ⇒ 统一包成启动期硬失败。
                    throw new InvalidDataException(
                        $"[cooked] 53_outcome_table 表构造被拒:{ex.Message}", ex);
                }
            }
        }
    }

    /// <summary>53 判定表烘焙产物的解码形。
    /// <para><see cref="Table"/> 是 Sim 侧的 <see cref="Outcome53.Table"/>(唯一表类型);
    /// <see cref="ConfigVersion"/> = 源数据集内容哈希派生(u32,ADR-014 §五)。</para></summary>
    public struct OutcomeTable53DataSet
    {
        /// <summary>判定表(可直接交给 <see cref="Outcome53.Judge.Evaluate"/>)。</summary>
        /// <remarks>⚠️ <c>HasDefaultRow</c> 恒为 <c>true</c> —— **由烘焙期校验推出,非载荷携带**(见文件头)。</remarks>
        public Outcome53.Table Table;

        /// <summary>烘焙时从源 JSON 文本派生的内容版本号(u32)。</summary>
        public uint ConfigVersion;

        /// <summary>构造解码形。</summary>
        public OutcomeTable53DataSet(Outcome53.Table table, uint configVersion)
        {
            Table = table; ConfigVersion = configVersion;
        }
    }
}
