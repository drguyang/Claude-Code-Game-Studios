// S13 · story-003 —— 53 延迟表的 deterministic cooked 写入器(阶段 2 烘焙的写面)。
//
// 权威来源:ADR-014 §二(同源两次烘焙逐位一致:固定字段顺序 · 零字典迭代 · 零本地时间)
//          · CookedFormat.cs(20 字节头布局的唯一共参照)· DelayTableBinder.BindResult(行集来源)
//
// ⚠️ 载荷布局(全部 little-endian;固定字段顺序):
//    row_count : i32(= 定义域大小,P0 恒 3)
//    row_count × { outcome: i32(枚举序数), delay_ticks: i64 }
//
// ⚠️ **行序 = outcome 枚举序数升序**(全序,唯一)。
//   2026-10-15 story-002 返工(A)的教训照搬:原判定表写方用 <c>List.Sort</c>(不稳定)按单键排
//   ⇒ 键相同时次序取决于实现内部 ⇒ 同源两次烘焙字节可能不一致(违 ADR-014 §二)。
//   本表键 = outcome 序数,构建期已保证互异 ⇒ 单键全序**已充分**,故无需二级破平键;
//   但仍显式按「序数升序」排(不靠 Dictionary 迭代序)。
//
// ⚠️ **schema_version 只住头槽**(offset 8,单一出处)。
//   story-002 的 BLOCKING R-1 教训:原实现头槽硬写 0、真值编进载荷首字段 ⇒
//   ① 违 CookedFormat 头契约(读方 CookedCodec.OpenChecked 只读头槽比对,不符 = 启动期硬失败);
//   ② 同一真值两处存 = 第二真源。本件头槽写 <c>bound.SchemaVersion</c>,载荷**无** schema 字段。

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DaYiJingCheng.Sim.Consequences;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>53 延迟表的 deterministic cooked 写入器。</summary>
    internal static class DelayTableCookedWriter
    {
        /// <summary>编码延迟表(含 20 字节头)。</summary>
        /// <exception cref="InvalidOperationException">绑定行集为空(Binder 全覆盖校验下不可达;
        /// 空集落盘 = 静默产出「永不发后果」的表,fail-loud)。</exception>
        public static byte[] Write(in DelayTableBinder.BindResult bound, uint configVersion)
        {
            var rowList = new List<(int Outcome, long DelayTicks)>();
            if (bound.Rows != null)
                foreach (var kv in bound.Rows)
                    rowList.Add(((int)kv.Key, kv.Value));

            if (rowList.Count == 0)
                throw new InvalidOperationException(
                    "延迟表绑定行集为空 ⇒ 拒绝落盘(Binder 的全覆盖校验应已拦;" +
                    "空表会让 53 永不发任何后果 = 静默失败)");

            rowList.Sort((a, b) =>
            {
                int cmp = a.Outcome.CompareTo(b.Outcome);
                if (cmp != 0) return cmp;
                return a.DelayTicks.CompareTo(b.DelayTicks);   // 防御破平(构建期键互异 ⇒ 恒不触)
            });

            using (var payload = new MemoryStream())
            using (var w = new BinaryWriter(payload, Encoding.UTF8))
            {
                w.Write(rowList.Count);
                for (int i = 0; i < rowList.Count; i++)
                {
                    w.Write(rowList[i].Outcome);
                    w.Write(rowList[i].DelayTicks);
                }

                w.Flush();
                return FinishHeader(payload.ToArray(), bound.SchemaVersion, configVersion);
            }
        }

        /// <summary>头(magic + formatVersion + schemaVersion + configVersion + payloadLength)+ 载荷。</summary>
        private static byte[] FinishHeader(byte[] payload, uint schemaVersion, uint configVersion)
        {
            var output = new byte[CookedFormat.HeaderSize + payload.Length];
            string magic = CookedFormat.Magic;
            output[0] = (byte)magic[0];
            output[1] = (byte)magic[1];
            output[2] = (byte)magic[2];
            output[3] = (byte)magic[3];
            WriteU32Le(output, 4, CookedFormat.FormatVersion);
            WriteU32Le(output, 8, schemaVersion);   // R-1:头槽承载(非 0,非载荷首字段)
            WriteU32Le(output, 12, configVersion);
            WriteU32Le(output, 16, (uint)payload.Length);
            Buffer.BlockCopy(payload, 0, output, CookedFormat.HeaderSize, payload.Length);
            return output;
        }

        private static void WriteU32Le(byte[] buffer, int offset, uint value)
        {
            buffer[offset] = (byte)(value & 0xFF);
            buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
            buffer[offset + 2] = (byte)((value >> 16) & 0xFF);
            buffer[offset + 3] = (byte)((value >> 24) & 0xFF);
        }
    }
}
