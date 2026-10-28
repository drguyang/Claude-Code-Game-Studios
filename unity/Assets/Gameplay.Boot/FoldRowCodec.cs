// 折叠行段编解码(TD-persist-03 动作 ⑤ · 用户 2026-10-24 四裁定实现轮)。
//
// 权威来源:
//   ADR-010 §二 —— 折叠行 = (onset, 病种_id, patient_id, patient_seed, outcome, t_end)
//   ADR-010 §一 增补(本批,2026-10-24)—— 存档体 v1 布局 = 头部 + 事件段 + 折叠段
//     (三流合为单列表;折叠段紧随事件段,count 前缀自描述,**无需头部偏移字段**)
//   🔁 ADR-034(2026-10-28)—— v2 在折叠段之后**再加载荷段** ⇒ 折叠段不再是最后一段,
//     读侧必须按 count 精确读(行宽单一源移到 Sim.Codec/SaveLayout.FoldRowSize)
//   Sim/FoldRow.cs —— 六字段形状与逐字次序(黄金夹具按此序钉)
//   ADR-006 Amendment B —— patient_id 必留(高水位重构依赖)
//   docs/tech-debt-register.md TD-persist-03 动作 ⑤(六条实测,本类兑现 B-2)
//
// ─────────────────────────────────────────────────────────────────────────────
// 为什么本类住 `Gameplay.Boot`(而不是 `Sim.Codec`)
// ─────────────────────────────────────────────────────────────────────────────
// `FoldRow` 住 `Sim`,`Sim.Codec` 的 references 恰 = `{Sim.Contracts}`(ADR-025 §①)
// ⇒ **`Sim.Codec` 物理上看不见 `FoldRow`**,编码器无处安放。而读档侧
// (`SaveService.Load`,Sim.Codec)只交**折叠段原始字节**给注入委托(用户裁定:
// 不立新抽象点,BCL 委托),解码落在本装配 —— 与 `SaveComposer` 同一个
// 「唯一同时看得见 Sim 与 Sim.Codec」的装配。同型先例:`SaveComposer.cs` 文件头。
//
// ─────────────────────────────────────────────────────────────────────────────
// 布局(逐字,黄金夹具钉)
// ─────────────────────────────────────────────────────────────────────────────
// 折叠段 := [rowCount : i32 LE][row × rowCount]
// row    := onset:i64 · diseaseId:i32 · patientId:i32 · patientSeed:i64 ·
//           outcome:i32 · tEnd:i64   —— 全小端,定长 36 字节,次序 = ADR-010 §二 逐字。
//
// ⚠️ **定长位置布局是刻意的,与 ADR-010 §一「按字段名/tag 编码,禁位置打包」不冲突**:
//    tag 规则为 SimEvent 而设(Kind 间字段可选、形状多变);FoldRow 六字段**恒在、
//    无一可选、形状恒定**(ADR-010 §二 逐字次序本就是位置语义,其注释自陈
//    「按位置读折叠行的下游」)。定长行的确定性(同输入同字节)与 tag 编码等价。
//
// ⚠️ **outcome 解码期即拒 0 与未知值**:合法域 = {Recovered = 1, Died = 2}。
//    0 = 零值哨兵(ADR-032 ②「折叠行撒谎且无法回退」的防线),未知值 = 档损坏
//    或跨版本布局漂移 —— 都不得静默进 sim。
// ═════════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.IO;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>
    /// 折叠行段的容器编解码(字节 ↔ <see cref="FoldRow"/> 列表)。
    /// <para>布局见文件头;本类纯函数,零 I/O。写侧 = <see cref="SaveComposer"/>,
    /// 读侧 = 装配根注入 <c>SaveService</c> 的折叠行委托(读档时把原始字节解回行)。</para>
    /// </summary>
    public static class FoldRowCodec
    {
        /// <summary>单行定长字节数 = 8 + 4 + 4 + 8 + 4 + 8(六字段,全定宽)。</summary>
        /// <para>🔁 <b>2026-10-28 ADR-034 §③:本常量改为 <see cref="SaveLayout.FoldRowSize"/> 的别名</b>,
        /// 不再是字面量 36。病因:v2 起 <c>SaveService.ParseBody</c>(Sim.Codec)必须按
        /// <c>count × 行宽</c> **精确**读出折叠段(否则载荷段无处可放),而它看不见本类;
        /// 两处各写一个 36 = 静默漂移风险。⇒ 单一源移到 Sim.Codec(两侧都可见的装配)。</para>
        public const int RowSize = SaveLayout.FoldRowSize;

        /// <summary>合法 outcome 域:<see cref="PatientOutcome.Recovered"/>(1)·
        /// <see cref="PatientOutcome.Died"/>(2)。0 = 哨兵,解码期即拒。</summary>
        private static readonly HashSet<int> ValidOutcomes = new HashSet<int> { 1, 2 };

        /// <summary>写一行(36 字节,次序 = ADR-010 §二 逐字)。</summary>
        public static void WriteRow(in FoldRow row, ref CodecWriter w)
        {
            w.WriteInt64LittleEndian(row.Onset);
            w.WriteInt32LittleEndian(row.DiseaseId);
            w.WriteInt32LittleEndian(row.PatientId);
            w.WriteInt64LittleEndian(row.PatientSeed);
            w.WriteInt32LittleEndian(row.Outcome);
            w.WriteInt64LittleEndian(row.TEnd);
        }

        /// <summary>读一行。outcome 出合法域即抛(见文件头「解码期即拒」)。</summary>
        /// <exception cref="InvalidDataException">outcome ∉ {1, 2}。</exception>
        public static FoldRow ReadRow(ref CodecReader r)
        {
            long onset = r.ReadInt64LittleEndian();
            int diseaseId = r.ReadInt32LittleEndian();
            int patientId = r.ReadInt32LittleEndian();
            long patientSeed = r.ReadInt64LittleEndian();
            int outcome = r.ReadInt32LittleEndian();
            long tEnd = r.ReadInt64LittleEndian();

            if (!ValidOutcomes.Contains(outcome))
                throw new InvalidDataException(
                    $"折叠行 outcome={outcome} 出合法域 {{1=Recovered, 2=Died}} —— " +
                    "0 是「未终态」哨兵、未知值是档损坏/布局漂移,均不得静默进 sim(ADR-032 ②)");

            return new FoldRow(onset, diseaseId, patientId, patientSeed, outcome, tEnd);
        }

        /// <summary>
        /// 写折叠段:<c>[rowCount:i32][row × rowCount]</c>。空列表合法(count=0)。
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="rows"/> 为 null。</exception>
        public static byte[] EncodeSegment(IReadOnlyList<FoldRow> rows)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));

            var w = new CodecWriter(4 + rows.Count * RowSize);
            w.WriteInt32LittleEndian(rows.Count);
            for (int i = 0; i < rows.Count; i++)
            {
                FoldRow row = rows[i]; // in 形参不能直传索引器返回值(CS8156)
                WriteRow(in row, ref w);
            }
            return w.ToArray();
        }

        /// <summary>
        /// 从存档体游标读折叠段(<c>count:i32 + count × 定长行</c>),**只消费折叠段本身**,
        /// <b>不</b>要求读尽 —— 其后可能还有载荷段(ADR-034 §③,2026-10-28 新增)。
        /// </summary>
        /// <remarks>
        /// 与 <see cref="DecodeSegment(ReadOnlySpan{byte})"/> 的分工:那个吃「恰好一段的字节」,
        /// 要求段末读尽(<c>SaveService.Load</c> 交出的就是这个切片);本重载吃
        /// 「游标 + 段在游标处」,段后可以有别的段。⇒ **装配侧**用前者,**能看到整个体的调用方**
        /// (测试 / 未来的调试工具)用后者。两者共享同一行宽与同一 outcome 域校验。
        /// </remarks>
        /// <exception cref="InvalidDataException">count 为负 / 超出剩余字节上界 / 某行 outcome 非法。</exception>
        public static IReadOnlyList<FoldRow> ReadSegment(ref CodecReader r)
        {
            int count = r.ReadInt32LittleEndian();
            if (count < 0)
                throw new InvalidDataException($"折叠段 count={count} 为负");
            if (count > r.Remaining / RowSize)
                throw new InvalidDataException(
                    $"折叠段 count={count} 超出剩余字节可容纳的上界({r.Remaining / RowSize})");

            var rows = new FoldRow[count];
            for (int i = 0; i < count; i++)
                rows[i] = ReadRow(ref r);
            return rows;
        }

        /// <summary>
        /// 读折叠段。段须恰好读尽 —— 多一个字节即抛(与 <c>SimEventListCodec</c> 同款
        /// 「防长度写错但前 N 行对」的静默错读防线)。
        /// </summary>
        /// <exception cref="InvalidDataException">
        /// count 为负 / count 超出剩余字节上界 / 某行 outcome 非法 / 段未读尽。
        /// </exception>
        public static IReadOnlyList<FoldRow> DecodeSegment(ReadOnlySpan<byte> segment)
        {
            var r = new CodecReader(segment);
            // 🔁 2026-10-28 ADR-034 §③:读体抽到 ReadSegment(与「游标版」共用一套 count 复验
            //    与行解码);本重载只多加一条「段末必读尽」。
            IReadOnlyList<FoldRow> rows = ReadSegment(ref r);

            if (r.Remaining != 0)
                throw new InvalidDataException(
                    $"折叠段未读尽,剩余 {r.Remaining} 字节(布局与 count 不符)");
            return rows;
        }
    }
}
