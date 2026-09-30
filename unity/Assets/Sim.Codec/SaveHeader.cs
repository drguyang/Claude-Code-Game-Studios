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

using System;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.Codec
{
    /// <summary>存档头部。全二进制,显式小端。</summary>
    public struct SaveHeader
    {
        public const uint Magic = 0x514A5944; // "DYJQ" 小端
        public const uint CurrentSaveVersion = 1;

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
