// 权威来源:ADR-014 §二/§五(运行期读烘焙产物,零 JSON 解析)· CookedFormat.cs(布局契约)
//          · Story 007(NR-1 的读面;与 Editor.Tools.Bake.InteractionKindCookedWriter 严格镜像)
//
// ⚠️ 任何字段缺失 / 魔数不符 / 长度不符 / 版本不匹配 ⇒ 显式 throw(启动期硬失败,E-13 口径),
//    绝不返回默认值,绝不静默降级。
//
// ⚠️ 住 Gameplay.Presentation(边界程序集)—— 读产物、喂契约行给 sim 侧是本装的合法用途
//    (与 CookedCodec / AddressablesDataProvider 同族)。

using System;
using System.Collections.Generic;
using System.IO;
using DaYiJingCheng.Gameplay.Interaction;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Presentation
{
    /// <summary>交互数据契约行集的读方(与 <c>InteractionKindCookedWriter</c> 镜像)。
    /// <para>只用 BCL(BinaryReader,little-endian);全部字段 = 整数 / 枚举序数 / 布尔,零解析器。</para>
    /// <example><code>var ds = InteractionKindCookedCodec.Read(bytes); // 失败即 throw</code></example>
    /// </summary>
    public static class InteractionKindCookedCodec
    {
        /// <summary>读契约行集(含 <c>ConfigVersion</c> 与行序)。</summary>
        /// <exception cref="InvalidDataException">头部损坏 / 版本不匹配 / 载荷长度不符 —— 启动期硬失败。</exception>
        public static InteractionKindDataSet Read(byte[] bytes)
        {
            using (BinaryReader r = OpenChecked(bytes, out uint configVersion))
            {
                int rInteract = r.ReadInt32();
                int worldW = r.ReadInt32();
                int worldH = r.ReadInt32();
                int worldD = r.ReadInt32();

                int count = r.ReadInt32();
                if (count < 0)
                    throw new InvalidDataException(
                        "[cooked] interaction_kinds 计数为 null 标记 —— 产物损坏,启动期硬失败(E-13)");

                var rows = new List<KindContractRow>(count);
                for (int i = 0; i < count; i++)
                    rows.Add(ReadRow(r));

                return new InteractionKindDataSet
                {
                    Rows = rows, ConfigVersion = configVersion,
                    RInteract = rInteract, WorldW = worldW, WorldH = worldH, WorldD = worldD,
                };
            }
        }

        private static KindContractRow ReadRow(BinaryReader r)
        {
            var kind = (InteractableKind)r.ReadByte();
            int priority = r.ReadInt32();
            var source = (StableIdSource)r.ReadByte();
            int routesTo = r.ReadInt32();
            bool suppresses = r.ReadByte() != 0;
            var durOwner = (DurationOwnerKind)r.ReadByte();
            int durOwnerId = r.ReadInt32();
            int w = r.ReadInt32();
            int h = r.ReadInt32();
            int d = r.ReadInt32();
            var uplink = (IntentUplink)r.ReadByte();

            return new KindContractRow(kind, priority, source, routesTo, suppresses,
                                       durOwner, durOwnerId, w, h, d, uplink);
        }

        /// <summary>头部校验 + 载荷长度验后,返回停在载荷起点的 reader。</summary>
        private static BinaryReader OpenChecked(byte[] bytes, out uint configVersion)
        {
            if (bytes == null || bytes.Length < CookedFormat.HeaderSize)
                throw new InvalidDataException(
                    $"[cooked] interaction_kinds 字节不足头部({CookedFormat.HeaderSize} B) —— 产物损坏(E-13)");

            if (bytes[0] != (byte)'D' || bytes[1] != (byte)'Y' || bytes[2] != (byte)'J' || bytes[3] != (byte)'C')
                throw new InvalidDataException("[cooked] interaction_kinds 魔数不符(期待 \"DYJC\") —— 拒收(E-13)");

            uint formatVersion = ReadU32Le(bytes, 4);
            if (formatVersion > CookedFormat.FormatVersion)
                throw new InvalidDataException(
                    $"[cooked] interaction_kinds formatVersion={formatVersion} > 读方 {CookedFormat.FormatVersion} —— 读方过旧,拒收(E-13)");

            configVersion = ReadU32Le(bytes, 12);
            uint payloadLength = ReadU32Le(bytes, 16);
            if (bytes.Length - CookedFormat.HeaderSize != payloadLength)
                throw new InvalidDataException(
                    $"[cooked] interaction_kinds 载荷长度不符(头声明 {payloadLength},实际 {bytes.Length - CookedFormat.HeaderSize}) —— 产物损坏(E-13)");

            var stream = new MemoryStream(bytes, CookedFormat.HeaderSize, (int)payloadLength, writable: false);
            return new BinaryReader(stream);
        }

        private static uint ReadU32Le(byte[] b, int offset) =>
            (uint)(b[offset] | (b[offset + 1] << 8) | (b[offset + 2] << 16) | (b[offset + 3] << 24));
    }

    /// <summary>交互数据契约行集的解码形(烘焙产物 <c>interaction_kinds.cooked.bytes</c>)。
    /// <para><see cref="ConfigVersion"/> = 源数据集内容哈希派生(u32,ADR-014 §五)。</para></summary>
    public struct InteractionKindDataSet
    {
        /// <summary>全量契约行(序 = 源 JSON 序;校验通过后的行集)。</summary>
        public List<KindContractRow> Rows;

        /// <summary>烘焙时从源 JSON 字节派生的内容版本号(u32)。</summary>
        public uint ConfigVersion;

        /// <summary>烘焙时随产物落盘的世界维度四元(4-DC-1 / 4-DC-4 ② 的输入)。</summary>
        public int RInteract;

        /// <inheritdoc cref="RInteract"/>
        public int WorldW;

        /// <inheritdoc cref="RInteract"/>
        public int WorldH;

        /// <inheritdoc cref="RInteract"/>
        public int WorldD;
    }
}
