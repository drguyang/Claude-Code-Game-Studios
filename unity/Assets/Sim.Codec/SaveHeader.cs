// ADR-010 §一 —— 存档头部结构。
//
// 权威来源:
//   ADR-010 §一 —— 存档格式:头部 + 三流 + 快照段 + 分册态段
//   ADR-007 §二 —— WorldSeed 归存档头(非 SimEvent)
//   ADR-014 §五 —— ConfigVersion 进存档头
//
// 字段序(黄金夹具钉死):
//   magic "DYJQ" (4B) · SHA256 (32B) · SaveVersion (u32) · WorldSeed (u64)
//   · ConfigVersion (u32) · tick (i64) · SnapshotOffset (u64)
//
// 校验和覆盖范围 = checksum 字段之后的全部字节(头部其余字段 + 三流 + 快照 + 分册态)。
//
// 🔁 2026-10-28 ADR-034:v2 在折叠段之后追加**载荷段**。头部**零新增字段** ——
//    载荷段自描述(count 前缀)、且恒为最后一段(与 ADR-010 §一「折叠段自描述 ⇒
//    无需偏移字段」同一原则),故 SnapshotOffset 仍写 0 哨兵。

using System;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.Codec
{
    /// <summary>存档头部。全二进制,显式小端。</summary>
    public struct SaveHeader
    {
        public const uint Magic = 0x514A5944; // "DYJQ" 小端

        /// <summary>
        /// 当前存档格式版本。
        /// <para>🔁 <b>v1 → 2(2026-10-28,ADR-034)</b>:v2 在折叠段之后追加
        /// <b>载荷段</b>(<c>count:i32 + count × {blobId:i32, length:i32, byte[length]}</c>),
        /// 使 blob 池可跨进程重建。v1 及更早的存档**没有**载荷字节(从未写出),
        /// 信息论上补不出来 ⇒ <b>不迁移,显式拒绝</b>(见 <see cref="PayloadSegmentMinVersion"/>)。</para>
        /// <para>⚠️ 改此值 = 改存档布局。禁「只改版本号不改布局」—— 那会让旧档被按新布局解析。</para>
        /// </summary>
        public const uint CurrentSaveVersion = 2;

        /// <summary>
        /// 首个**带载荷段**的存档版本(ADR-034 §Decision ⑥)。
        /// <para><c>SaveVersion &lt; 本值</c> ⇒ 读档须报「v1 无载荷字节」并拒绝,
        /// <b>不得</b>下沉到迁移链(那会抛「没有对应迁移脚本」—— 读起来像「漏写脚本」,
        /// 而真实原因是**信息从未存在**)。</para>
        /// </summary>
        public const uint PayloadSegmentMinVersion = 2;

        public uint MagicValue;        // 0x514A5944
        public byte[] Checksum;        // SHA256(其后全部字节) — 32B
        public uint SaveVersion;       // 存档格式版本
        public ulong WorldSeed;        // ADR-007 §二
        public uint ConfigVersion;     // ADR-014 §五
        public long Tick;              // 当前 tick
        public ulong SnapshotOffset;   // 快照段偏移

        public SaveHeader(ulong worldSeed, uint configVersion, long tick, ulong snapshotOffset)
        {
            MagicValue = Magic;
            Checksum = new byte[32];
            SaveVersion = CurrentSaveVersion;
            WorldSeed = worldSeed;
            ConfigVersion = configVersion;
            Tick = tick;
            SnapshotOffset = snapshotOffset;
        }
    }
}
