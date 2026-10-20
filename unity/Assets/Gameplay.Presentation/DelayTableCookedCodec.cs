// 权威来源:ADR-014 §二/§五(运行期读烘焙产物)· CookedFormat.cs(布局契约)
//          · 写方严格镜像:`Editor.Tools.Bake/DelayTableCookedWriter.cs`
//          · 表构造 / 三条拒收**单一出处**:`DaYiJingCheng.Sim.Consequences.DelayTable53.CreateTable`
//            (定义域白名单 · delay_ticks > 0 · 全覆盖)—— 本件**不重做**这些校验,零漂移。
//
// ⚠️ 载荷布局(全部 little-endian):
//    row_count : i32(= 定义域大小,P0 恒 3)
//    row_count × { outcome: i32(枚举序数), delay_ticks: i64 }
//
// ⚠️ 行序 = outcome 序数升序(写方已排序);读方按序读入后交 `DelayTable53.Table` 构造,
//    **不依赖**读入序做任何计算 —— 序只影响诊断输出。
//
// ⚠️ 任何字段缺失 / 魔数不符 / 长度不符 / 构造拒收 ⇒ 显式 throw(启动期硬失败,E-13 口径)。

using System;
using System.Collections.Generic;
using System.IO;
using DaYiJingCheng.Sim.Consequences;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Presentation
{
    /// <summary>53 延迟表烘焙产物(<c>53_delay_table.cooked.bytes</c>)的运行期解码器。
    /// <para>产物解码后直接物化为 <see cref="DelayTable53.Table"/>(Sim 侧唯一表类型)。</para>
    /// </summary>
    public static class DelayTableCookedCodec
    {
        /// <summary>读延迟表。</summary>
        /// <exception cref="InvalidDataException">头部损坏 / 长度不符 / 行数 &lt; 1 /
        /// 表构造拒收(域外 key · delay_ticks ≤ 0 · 缺行)—— 启动期硬失败。</exception>
        public static DelayTable53DataSet Read(byte[] bytes)
        {
            using (BinaryReader r = CookedReadUtil.OpenChecked(bytes, "53_delay_table", out uint configVersion))
            {
                int rowCount = r.ReadInt32();
                if (rowCount < 1)
                    throw new InvalidDataException(
                        $"[cooked] 53_delay_table 行数 = {rowCount} —— 须 ≥ 1" +
                        "(空表 ⇒ 53 永不发任何后果 = 静默失败;E-13)");

                var rows = new Dictionary<int, long>(rowCount);
                for (int i = 0; i < rowCount; i++)
                {
                    int outcomeOrdinal = r.ReadInt32();
                    long delayTicks = r.ReadInt64();

                    if (rows.ContainsKey(outcomeOrdinal))
                        throw new InvalidDataException(
                            $"[cooked] 53_delay_table outcome 序数 {outcomeOrdinal} 重复 —— 产物损坏(E-13)");
                    rows[outcomeOrdinal] = delayTicks;
                }

                CookedReadUtil.AssertFullyConsumed(r, "53_delay_table");

                try
                {
                    DelayTable53.Table table = DelayTable53.CreateTable(rows);
                    return new DelayTable53DataSet(table, configVersion);
                }
                catch (ArgumentException ex)
                {
                    // Sim 侧三条拒收(域外 key / ≤0 / 缺行)—— 产物级损坏 ⇒ 统一包成启动期硬失败。
                    throw new InvalidDataException(
                        $"[cooked] 53_delay_table 表构造被拒:{ex.Message}", ex);
                }
            }
        }
    }

    /// <summary>53 延迟表烘焙产物的解码形。
    /// <para><see cref="Table"/> 是 Sim 侧的 <see cref="DelayTable53.Table"/>(唯一表类型);
    /// <see cref="ConfigVersion"/> = 源数据集内容哈希派生(u32,ADR-014 §五)。</para></summary>
    public struct DelayTable53DataSet
    {
        /// <summary>延迟表(可直接交给 <c>DelayTable53.Table.TryDelayTicks</c> 查询)。</summary>
        public DelayTable53.Table Table;

        /// <summary>烘焙时从源 JSON 文本派生的内容版本号(u32)。</summary>
        public uint ConfigVersion;

        /// <summary>构造解码形。</summary>
        public DelayTable53DataSet(DelayTable53.Table table, uint configVersion)
        {
            Table = table; ConfigVersion = configVersion;
        }
    }
}
