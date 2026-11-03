// 权威来源:ADR-014 §二/§五(运行期读烘焙产物,零 JSON 解析;E-13 启动期硬失败口径)
//          · CookedFormat.cs(布局共参照)· GDD random-events.md 规则二 / 规则十一
//
// ⚠️ 任何字段缺失 / 魔数不符 / 长度不符 / 版本不匹配 ⇒ 显式 throw,绝不返回默认值。
// ⚠️ 本 codec **不做值域校验** —— 那是 Binder 的职责(烘焙期);读方只守**形状**。
// ⚠️ 与 `Editor.Tools.Bake.EventPoolCookedWriter` **严格镜像** —— 改一处必须改两处。

using System;
using System.Collections.Generic;
using System.IO;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Presentation
{
    /// <summary>`52_event_pool.cooked.bytes` 的运行期解码器(与写入器镜像)。</summary>
    public static class EventPoolCookedCodec
    {
        /// <summary>读方认领的 <c>schema_version</c>。</summary>
        public const uint ExpectedSchemaVersion = 1u;

        private const string ProductName = "52_event_pool";

        /// <summary>固定头部字节数(5 × i32)。</summary>
        public const int FixedPayloadBytes = 20;

        /// <summary>每条条目字节数(7 × i32)。</summary>
        public const int EntryBytes = 28;

        /// <summary>解码。</summary>
        /// <exception cref="InvalidDataException">头部损坏 / schemaVersion 不匹配 / 载荷长度不符 / 条目越界。</exception>
        public static EventPoolDataSet Read(byte[] bytes)
        {
            if (bytes == null || bytes.Length < CookedFormat.HeaderSize)
                throw E13($"字节不足头部({CookedFormat.HeaderSize} B)");

            uint schemaVersion = ReadU32Le(bytes, 8);
            if (schemaVersion != ExpectedSchemaVersion)
                throw E13($"schemaVersion={schemaVersion} ≠ 读方认领 {ExpectedSchemaVersion} —— 版本不匹配,拒收");

            uint payloadLength = ReadU32Le(bytes, 16);
            if (payloadLength < FixedPayloadBytes)
                throw E13($"载荷长度 {payloadLength} B < 固定头 {FixedPayloadBytes} B —— 产物损坏");

            using (BinaryReader r = CookedReadUtil.OpenChecked(bytes, ProductName, out uint configVersion))
            {
                int nightNum = r.ReadInt32();
                int nightDen = r.ReadInt32();
                int maxNum = r.ReadInt32();
                int maxDen = r.ReadInt32();
                int count = r.ReadInt32();

                if (count < 0)
                    throw E13($"条目数 {count} 为负 —— 产物损坏");
                long expected = FixedPayloadBytes + (long)count * EntryBytes;
                if (expected != payloadLength)
                    throw E13($"载荷长度 {payloadLength} B ≠ {FixedPayloadBytes} + {count} × {EntryBytes} = {expected} B");

                var rows = new List<EventPoolEntryData>(count);
                for (int i = 0; i < count; i++)
                {
                    int key = r.ReadInt32();
                    int wBase = r.ReadInt32();
                    int tier = r.ReadInt32();
                    int anchor = r.ReadInt32();
                    int trigger = r.ReadInt32();
                    int handler = r.ReadInt32();
                    int causeFlagKey = r.ReadInt32();

                    if (handler != (int)PoolHandler.Encounter && handler != (int)PoolHandler.Medical)
                        throw E13($"条目 {key} 的 handler={handler} 不在闭集 {{0,1}} —— 产物损坏");

                    rows.Add(new EventPoolEntryData(key, wBase, tier, anchor, trigger,
                                                    (PoolHandler)handler, causeFlagKey));
                }

                CookedReadUtil.AssertFullyConsumed(r, ProductName);
                return new EventPoolDataSet(rows, nightNum, nightDen, maxNum, maxDen,
                                            configVersion, schemaVersion);
            }
        }

        private static uint ReadU32Le(byte[] b, int offset) =>
            (uint)(b[offset] | (b[offset + 1] << 8) | (b[offset + 2] << 16) | (b[offset + 3] << 24));

        private static InvalidDataException E13(string what) =>
            new InvalidDataException($"[E-13] {ProductName} cooked:{what}");
    }
}
