// `emergency_action.cooked.bytes` 的运行期解码器(T14 · 2026-11-09)
//
// 权威:ADR-014 §二/§五(运行期读烘焙产物,零 JSON 解析;E-13 启动期硬失败)
//       · `design/gdd/emergency-procedures.md` `:843`(动作表列)
//
// ⚠️ 任何字段缺失 / 魔数不符 / 长度不符 / 版本不匹配 ⇒ 显式 throw,绝不返回默认值。
// ⚠️ 本 codec **不做值域校验** —— 那是 `EmergencyActionSchema`(DC-1…DC-6)的职责,
//    已在**构建期**(binder)执行;运行期只解字节。

using System;
using System.Collections.Generic;
using System.IO;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.EmergencyProcedures;

namespace DaYiJingCheng.Gameplay.Presentation
{
    /// <summary>`emergency_action.cooked.bytes` 的解码形。</summary>
    public readonly struct EmergencyActionDataSet
    {
        /// <summary>产物文件名(= Addressables address;写盘名与装载名共用本常量)。</summary>
        public const string CookedFileName = "emergency_action.cooked.bytes";

        /// <summary>动作行(按 `action_id` 升序)。</summary>
        public readonly IReadOnlyList<EmergencyActionRow> Rows;

        /// <summary>头槽 offset 8 的 schema_version。</summary>
        public readonly uint SchemaVersion;

        /// <summary>头槽 offset 12 的 configVersion(源内容哈希派生)。</summary>
        public readonly uint ConfigVersion;

        /// <summary>构造。</summary>
        public EmergencyActionDataSet(IReadOnlyList<EmergencyActionRow> rows,
                                      uint schemaVersion, uint configVersion)
        {
            Rows = rows ?? throw new ArgumentNullException(nameof(rows));
            SchemaVersion = schemaVersion;
            ConfigVersion = configVersion;
        }
    }

    /// <summary>`emergency_action.cooked.bytes` 的运行期解码器(与写入器镜像)。</summary>
    public static class EmergencyActionCookedCodec
    {
        /// <summary>读方认领的 <c>schema_version</c>。</summary>
        public const uint ExpectedSchemaVersion = 1u;

        private const string ProductName = "emergency_action";

        /// <summary>每行固定字节数(7 个 i32 + 1 个 i64 + 3 个 i64 = 60)。</summary>
        public const int BytesPerRow = 4 * 7 + 8 + 8 * 3;

        /// <summary>解码。</summary>
        /// <exception cref="InvalidDataException">头部损坏 / schemaVersion 不匹配 / 载荷长度不符。</exception>
        public static EmergencyActionDataSet Read(byte[] bytes)
        {
            if (bytes == null || bytes.Length < CookedFormat.HeaderSize)
                throw E13($"字节不足头部({CookedFormat.HeaderSize} B)");

            uint schemaVersion = ReadU32Le(bytes, 8);
            if (schemaVersion != ExpectedSchemaVersion)
                throw E13($"schemaVersion={schemaVersion} ≠ 读方认领 {ExpectedSchemaVersion} —— 版本不匹配,拒收");

            uint payloadLength = ReadU32Le(bytes, 16);

            using (BinaryReader r = CookedReadUtil.OpenChecked(bytes, ProductName, out uint configVersion))
            {
                int rowCount = r.ReadInt32();
                if (rowCount <= 0)
                    throw E13($"row_count={rowCount} —— 空动作表会让生产第一笔急救 fail-loud,拒收" +
                              "(合法性判据在构建期 binder 已执行;能走到这里说明产物被改坏了)");

                // 载荷 = row_count(i32) + 每行 BytesPerRow —— 这两个部分都要算进去
                int expected = 4 + rowCount * BytesPerRow;
                if (payloadLength != expected)
                    throw E13($"载荷长度 {payloadLength} B ≠ 4 + row_count({rowCount}) × {BytesPerRow} = {expected} B");

                var rows = new List<EmergencyActionRow>(rowCount);
                for (int i = 0; i < rowCount; i++)
                {
                    var row = new EmergencyActionRow
                    {
                        ActionId = r.ReadInt32(),
                        Polarity = r.ReadInt32(),
                        BasePotency = r.ReadInt64(),
                        MagThreshold = r.ReadInt32(),
                        MinEdges = r.ReadInt32(),
                        MinHoldTicks = r.ReadInt32(),
                        HalfLifeTicks = r.ReadInt32(),
                        DurationTicks = r.ReadInt32(),
                        ResultMul = new long[3],
                    };
                    for (int t = 0; t < 3; t++) row.ResultMul[t] = r.ReadInt64();
                    rows.Add(row);
                }

                CookedReadUtil.AssertFullyConsumed(r, ProductName);
                return new EmergencyActionDataSet(rows, schemaVersion, configVersion);
            }
        }

        private static uint ReadU32Le(byte[] b, int offset) =>
            (uint)(b[offset] | (b[offset + 1] << 8) | (b[offset + 2] << 16) | (b[offset + 3] << 24));

        private static InvalidDataException E13(string what) =>
            new InvalidDataException($"[E-13] {ProductName} cooked:{what}");
    }
}
