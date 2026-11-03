// 权威来源:ADR-014 §二/§五(运行期读烘焙产物,零 JSON 解析;E-13 口径)
//          · CookedFormat.cs(布局共参照)· Editor.Tools.Bake.RandomEventsCookedWriter(写方,严格镜像)
//          · ADR-038 §D2/§D4
//
// ⚠️ 任何字段缺失 / 魔数不符 / 长度不符 / schemaVersion 不匹配 ⇒ 显式 throw(E-13 口径)。
//
// ⚠️ **本 codec 不做值域校验**(整除 / 上界那些)—— 那是烘焙期 `RandomEventsBinder` 的义务。
//    运行期若要再校验一次,须另裁(今日该数据集**零生产消费方**,校验只有在被读时才有意义)。

using System;
using System.IO;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Presentation
{
    /// <summary>`52_random_events.cooked.bytes` 的运行期解码器。</summary>
    public static class RandomEventsCookedCodec
    {
        /// <summary>读方认领的 <c>schema_version</c>。</summary>
        public const uint ExpectedSchemaVersion = 1u;

        private const string ProductName = "52_random_events";

        /// <summary>载荷固定字节数(2 × i64 + 3 × i32)。</summary>
        public const int PayloadBytes = 28;

        /// <summary>解码。</summary>
        /// <exception cref="InvalidDataException">头部损坏 / schemaVersion 不匹配 / 载荷长度不符。</exception>
        public static RandomEventsDataSet Read(byte[] bytes)
        {
            if (bytes == null || bytes.Length < CookedFormat.HeaderSize)
                throw E13($"字节不足头部({CookedFormat.HeaderSize} B)");

            uint schemaVersion = ReadU32Le(bytes, 8);
            if (schemaVersion != ExpectedSchemaVersion)
                throw E13($"schemaVersion={schemaVersion} ≠ 读方认领 {ExpectedSchemaVersion} —— 版本不匹配,拒收");

            uint payloadLength = ReadU32Le(bytes, 16);
            if (payloadLength != PayloadBytes)
                throw E13($"载荷长度 {payloadLength} B ≠ 固定 {PayloadBytes} B —— 产物损坏");

            using (BinaryReader r = CookedReadUtil.OpenChecked(bytes, ProductName, out uint configVersion))
            {
                var data = new RandomEventsDataSet
                {
                    TicksPerDay = r.ReadInt64(),
                    RollInterval = r.ReadInt64(),
                    WindowSize = r.ReadInt32(),
                    Base = r.ReadInt32(),
                    BudgetMax = r.ReadInt32(),
                    ConfigVersion = configVersion,
                    SchemaVersion = schemaVersion,
                };
                CookedReadUtil.AssertFullyConsumed(r, ProductName);
                return data;
            }
        }

        private static uint ReadU32Le(byte[] b, int offset) =>
            (uint)(b[offset] | (b[offset + 1] << 8) | (b[offset + 2] << 16) | (b[offset + 3] << 24));

        private static InvalidDataException E13(string what) =>
            new InvalidDataException($"[E-13] {ProductName} cooked:{what}");
    }
}
