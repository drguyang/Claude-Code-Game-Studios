// 权威来源:Sim.Contracts.CookedFormat(20 B 头布局 + 载荷内字符串 = i32 字节长 + UTF-8)
//          · ADR-014 §二/§五(确定性产物 · ConfigVersion)
//
// ⚠️ **单一出处**:本文件是 4 个新 codec(`prescription_actions` / `disease_action_axis` /
//    53_outcome_table / 53_delay_table)的头部校验与字符串读取的**唯一**实现。
//
// ⚠️ 为什么另立而不是照抄第 5 遍:既有 4 个 codec(`CookedCodec` / `DiagnosisSign` /
//    `DiagnosisReadFloor` / `DiagnosisNegativeConfidence` / `InteractionKind`)各自内联了一份
//    `OpenChecked` + `ReadU32Le`(**五份逐字重复**)。再抄四份会让「改一处必须改五处」
//    变成「改一处必须改九处」—— 本次新增的四个走共享件,存量五个**不动**(不重开已签面)。
//
// ⚠️ 任何失败一律 throw(启动期硬失败,E-13 口径),绝不返回默认值。

using System;
using System.IO;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Presentation
{
    /// <summary>cooked 产物的通用读面(头部校验 · i32 长度字符串 · 载荷耗尽断言)。
    /// <para>所有失败路径都带产物名 —— 报文能直接指出是哪份产物损坏。</para>
    /// </summary>
    internal static class CookedReadUtil
    {
        /// <summary>校验 20 B 头并返回停在载荷起点的 reader。</summary>
        /// <param name="bytes">产物全字节。</param>
        /// <param name="productName">产物基名(仅用于错误报文)。</param>
        /// <param name="configVersion">头槽 offset 12 的内容版本号。</param>
        /// <exception cref="InvalidDataException">字节不足 / 魔数不符 / formatVersion 过新 / 载荷长度不符。</exception>
        public static BinaryReader OpenChecked(byte[] bytes, string productName, out uint configVersion)
        {
            if (bytes == null || bytes.Length < CookedFormat.HeaderSize)
                throw new InvalidDataException(
                    $"[cooked] {productName} 字节不足头部({CookedFormat.HeaderSize} B) —— 产物损坏(E-13)");

            if (bytes[0] != (byte)'D' || bytes[1] != (byte)'Y'
                || bytes[2] != (byte)'J' || bytes[3] != (byte)'C')
                throw new InvalidDataException(
                    $"[cooked] {productName} 魔数不符(期待 \"{CookedFormat.Magic}\") —— 拒收(E-13)");

            uint formatVersion = ReadU32Le(bytes, 4);
            if (formatVersion > CookedFormat.FormatVersion)
                throw new InvalidDataException(
                    $"[cooked] {productName} formatVersion={formatVersion} > 读方 " +
                    $"{CookedFormat.FormatVersion} —— 读方过旧,拒收(E-13)");

            configVersion = ReadU32Le(bytes, 12);
            uint payloadLength = ReadU32Le(bytes, 16);
            uint actual = (uint)(bytes.Length - CookedFormat.HeaderSize);
            if (actual != payloadLength)
                throw new InvalidDataException(
                    $"[cooked] {productName} 载荷长度不符(头声明 {payloadLength},实际 {actual}) —— 产物损坏(E-13)");

            var stream = new MemoryStream(bytes, CookedFormat.HeaderSize, (int)payloadLength, writable: false);
            return new BinaryReader(stream);
        }

        /// <summary>读一个 i32 字节长 + UTF-8 的字符串(<c>-1</c> = null)。
        /// <para>⚠️ 这是 <see cref="CookedFormat"/> 明文规定的载荷内字符串形状。
        /// <b>不是</b> <see cref="BinaryWriter.Write(string)"/> 的 7-bit 变长编码 ——
        /// 两者对同一段字节给出不同结果(2026-10-20 实测踩过,见下)。</para></summary>
        /// <exception cref="InvalidDataException">长度为负且非 -1 / 越出载荷尾。</exception>
        public static string ReadString32(BinaryReader r, string fieldName)
        {
            int length = r.ReadInt32();
            if (length == -1) return null;
            if (length < 0)
                throw new InvalidDataException(
                    $"[cooked] {fieldName} 字符串长度为 {length} —— 产物损坏(E-13)");

            // 读满:BinaryReader.ReadBytes 在越过流尾时返回**短于请求**的数组,不抛。
            byte[] raw = r.ReadBytes(length);
            if (raw.Length != length)
                throw new InvalidDataException(
                    $"[cooked] {fieldName} 字符串声明 {length} B,实际只读到 {raw.Length} B —— 产物损坏(E-13)");
            return System.Text.Encoding.UTF8.GetString(raw);
        }

        /// <summary>断言载荷已被**恰好**读完(不多不少)。
        /// <para><b>为什么必须有</b>:读方少读一个字段 ⇒ 后续全部错位却可能「看起来解析成功」;
        /// 多读 ⇒ 越界才炸。本断言把「布局与写方不再镜像」变成**立刻可见**的失败,
        /// 而不是等到某个字段取到垃圾值才发作。</para></summary>
        /// <exception cref="InvalidDataException">载荷有剩余字节。</exception>
        public static void AssertFullyConsumed(BinaryReader r, string productName)
        {
            long remaining = r.BaseStream.Length - r.BaseStream.Position;
            if (remaining != 0)
                throw new InvalidDataException(
                    $"[cooked] {productName} 载荷读完后仍剩 {remaining} B —— 读方与写方布局不再镜像(E-13)");
        }

        private static uint ReadU32Le(byte[] b, int offset) =>
            (uint)(b[offset] | (b[offset + 1] << 8) | (b[offset + 2] << 16) | (b[offset + 3] << 24));
    }
}
