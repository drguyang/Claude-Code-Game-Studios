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
        /// <para>🔁 <b>v2 → 3(2026-10-31,ADR-037)</b>:v3 在**头部之后、事件段之前**插入
        /// <b>配置版本段</b>(<c>count:i32 + count × {addrLen:i32, addr:utf8, version:u32}</c>),
        /// 使「哪一张表变了」从不可推断变成可判。插在头部之后(而非尾部)是**可迁移性**要求的:
        /// 迁移链按「从末尾算的字节」比对正文,尾部追加必破该断言(ADR-037 §①)。
        /// v2 档由 `SaveMigrationV2ToV3` 插入 <c>count = 0</c>(= 未知,不得伪造)后升到 v3。</para>
        /// <para>🔁 <b>v3 → 4(2026-11-03,ADR-038 §D3)</b>:v4 在**头部末尾**追加三个 i64 ——
        /// <c>TicksPerDay</c> / <c>NightStart</c> / <c>NightSpan</c>(共 24 B),使读档能
        /// **用存档记下的时间参数重放**,而不是用当前构建的数据值。
        /// 追加在头部末尾(**不是**正文末尾)同样是为了可迁移性:迁移链按尾部锚定比对正文,
        /// 尾部追加必破该断言(与 ADR-037 §① 同一条理由)。
        /// v3 档由 `SaveMigrationV3ToV4` 在旧头部末插入 <c>36000 / 27000 / 9000</c> 后升到 v4。</para>
        /// <para>🔁 <b>v4 → 5(2026-11-03,ADR-038 Amendment A)</b>:v5 在头部末尾**再追加**四个 i64 ——
        /// <c>TicksPerSeason</c> / <c>SeasonsPerYear</c> / <c>WeatherBlockTicks</c> / <c>IntensityMax</c>
        /// (共 32 B)。理由与 D3 同一条:这四个量都是**模拟 / 掷骰的输入**,不随档走会静默改变重放。
        /// v4 档由 `SaveMigrationV4ToV5` 在 v4 头部末插入用户裁定的四值后升到 v5。</para>
        /// </summary>
        public const uint CurrentSaveVersion = 5;

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

        /// <summary>一个世界日的 tick 数(ADR-038 §D3 · v4 起)。读档时**以此为准**重放。</summary>
        /// <remarks>⚠️ v3 及更早的档**没有**这三个字段 ⇒ `SaveHeaderCodec.Read` 在版本 &lt; 4 时
        /// 给它们填 <c>0</c>,由迁移脚本补值后**重新读一次**头(见 `SaveService.Load`)。</remarks>
        public long TicksPerDay;

        /// <summary>夜的起点(日内相位;ADR-038 §D3 · v4 起)。</summary>
        public long NightStart;

        /// <summary>夜长(ADR-038 §D3 · v4 起)。</summary>
        public long NightSpan;

        /// <summary>一季的 tick 数(ADR-038 Amendment A · v5 起)。</summary>
        public long TicksPerSeason;

        /// <summary>一年几季(ADR-038 Amendment A · v5 起)。</summary>
        public long SeasonsPerYear;

        /// <summary>天气相干块长(ADR-038 Amendment A · v5 起)。</summary>
        public long WeatherBlockTicks;

        /// <summary>天气强度上界(ADR-038 Amendment A · v5 起)。</summary>
        public long IntensityMax;

        public SaveHeader(ulong worldSeed, uint configVersion, long tick, ulong snapshotOffset)
        {
            MagicValue = Magic;
            Checksum = new byte[32];
            SaveVersion = CurrentSaveVersion;
            WorldSeed = worldSeed;
            ConfigVersion = configVersion;
            Tick = tick;
            SnapshotOffset = snapshotOffset;
            // ⚠️ 时间参数**不在此处赋默认值** —— 由 `SaveHeaderCodec.WriteWithoutChecksum`
            // 的调用方显式给出。留 0 会让「忘了传」看起来像一个合法值(0 是非法日长,
            // 但只有消费者校验时才暴露);调用方缺失是编译期就能挡的。
            TicksPerDay = 0L;
            NightStart = 0L;
            NightSpan = 0L;
            TicksPerSeason = 0L;
            SeasonsPerYear = 0L;
            WeatherBlockTicks = 0L;
            IntensityMax = 0L;
        }
    }
}
