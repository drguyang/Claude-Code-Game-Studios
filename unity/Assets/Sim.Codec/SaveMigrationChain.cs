// ADR-010 §七 —— 迁移链驱动(逐版本 + 两不变量断言)。
//
// 权威来源:
//   ADR-010 §七 —— 逐版本升级 + `WorldSeed` / 已发生事件原样保留
//   GDD `persistence-service.md:563-567` —— AC-7a-14
//   ADR-007 §二 —— `WorldSeed` 被改 ⇒ 全案病程与事件流同时作废
//
// ─────────────────────────────────────────────────────────────────────────────
// 这个类存在的理由:AC-7a-14 的两条不变量必须**被执行体守住**,不是靠约定
// ─────────────────────────────────────────────────────────────────────────────
// AC-7a-14 的判据是一句话:「逐版本执行 + `WorldSeed` 与已发生事件逐位不变」。
// 若把它交给每个迁移脚本自己遵守,那么**任何一个**脚本忘了守,破坏都是静默的:
// `WorldSeed` 一变,全案病程与掷骰同时作废(ADR-007 §二);事件段一变,
// 读出来的世界与玩家经历过的世界不是同一个,而**没有任何报错**。
//
// ⇒ 链在**每一级**的进出处各断言一次,任一条不成立当场抛。
//   这是「不可利用 > 不可用」的同族处置(`sprint-19.md` §S19-1 ⑧b):
//   宁可迁移失败(玩家看到明确报错),不可迁出一个语义已被破坏的世界。
//
// ─────────────────────────────────────────────────────────────────────────────
// 跨版本「事件段」在哪:本类按**尾部**处理,不按偏移 —— 这是刻意的
// ─────────────────────────────────────────────────────────────────────────────
// 迁移改的通常是**头部字段**(新增字段 / 改字段宽)。正文(事件段 + 折叠段)在
// 头部定长变化时整体平移 ⇒ 用固定偏移取事件段会在**跨版本**时取错位置。
// 本类改用**尾部锚定**:正文是「存档末尾的那一段」,迁前取其**从末尾算的字节数**,
// 迁后要求同一长度、同一内容。⇒ 与头部长度如何变化**无关**,正好是 AC 要的口径。
// ⚠️ 如果某个真实迁移**确实要改正文**(如事件载荷加字段),那条迁移**不满足**
//    AC-7a-14 的「已发生事件原样」⇒ 它要么不该存在,要么须先改 AC。
//    本类的断言会把这条路**堵死并报错**,而不是放行 —— 这正是它该做的。

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace DaYiJingCheng.Sim.Codec
{
    /// <summary>
    /// 迁移链驱动。把一份旧版本存档沿注册的 <see cref="ISaveMigration"/> 逐版本升到目标版本。
    /// <para><b>纯函数</b>(无 I/O、无墙钟、无全局态):同样的输入 + 同样的链 ⇒ 同样的字节。</para>
    /// </summary>
    public static class SaveMigrationChain
    {
        /// <summary>校验和槽长度(SHA256)。链工作在「已剥掉校验和」形态。</summary>
        private const int ChecksumSize = 32;

        /// <summary>
        /// 把 <paramref name="dataWithoutChecksum"/> 从它的当前版本逐级升到
        /// <paramref name="targetVersion"/>。
        /// </summary>
        /// <param name="dataWithoutChecksum">
        /// 完整存档字节,<b>不含</b>校验和槽(形如 <see cref="SaveService.AddChecksum"/> 的入参)。
        /// </param>
        /// <param name="targetVersion">目标版本(生产路径恒 = `SaveHeader.CurrentSaveVersion`)。</param>
        /// <param name="migrations">单步脚本集合(顺序无关 —— 链按 `FromVersion` 查表)。</param>
        /// <returns>升到目标版本后的字节(仍不含校验和槽)。</returns>
        /// <exception cref="ArgumentNullException">入参为 null。</exception>
        /// <exception cref="InvalidOperationException">
        /// 链断了(某一级没有对应脚本)· 版本出现倒退 · 某级未把版本号写成 `v+1` ·
        /// 或某一级破坏了 `WorldSeed` / 事件段。
        /// </exception>
        public static byte[] Migrate(
            byte[] dataWithoutChecksum,
            uint targetVersion,
            IReadOnlyList<ISaveMigration> migrations)
        {
            if (dataWithoutChecksum == null) throw new ArgumentNullException(nameof(dataWithoutChecksum));
            if (migrations == null) throw new ArgumentNullException(nameof(migrations));

            uint from = ReadSaveVersion(dataWithoutChecksum);

            if (from > targetVersion)
                throw new InvalidOperationException(
                    $"迁移链:存档版本 {from} > 目标 {targetVersion} —— 前向不兼容," +
                    "链只升不降(降级须另开契约,不在 AC-7a-14 面内)");

            if (from == targetVersion)
                return Clone(dataWithoutChecksum);

            // 查表:FromVersion → 脚本。重复注册同一级 = 静默覆盖一条迁移 ⇒ 直接拒。
            var byFrom = new Dictionary<uint, ISaveMigration>();
            foreach (var m in migrations)
            {
                if (m == null)
                    throw new InvalidOperationException("迁移链:脚本集合含 null 项");
                if (byFrom.ContainsKey(m.FromVersion))
                    throw new InvalidOperationException(
                        $"迁移链:FromVersion={m.FromVersion} 注册了不止一个脚本 —— " +
                        "后者会静默覆盖前者,使某条迁移永不执行");
                byFrom.Add(m.FromVersion, m);
            }

            byte[] current = Clone(dataWithoutChecksum);

            // ── 预断言:进入链之前的基线 ──
            ulong seedBefore = ReadWorldSeed(current);
            byte[] bodyBefore = TailBody(current);

            for (uint v = from; v < targetVersion; v++)
            {
                if (!byFrom.TryGetValue(v, out var step))
                    throw new InvalidOperationException(
                        $"迁移链断裂:版本 {v} 没有对应迁移脚本(目标 {targetVersion})" +
                        $"—— 已注册 [{string.Join(",", Describe(byFrom))}]。" +
                        "断链必须显式失败,不得跳过中间版本按目标布局解析(那正是静默错读)");

                current = step.Migrate(current)
                    ?? throw new InvalidOperationException(
                        $"迁移脚本 FromVersion={v} 返回 null —— 迁移失败须抛,不得以 null 顶替");

                // ── 每级断言①:版本号必须真的被写成 v+1 ──
                uint now = ReadSaveVersion(current);
                if (now != v + 1)
                    throw new InvalidOperationException(
                        $"迁移脚本 FromVersion={v} 执行后版本号为 {now},应为 {v + 1} —— " +
                        "脚本没把自己的版本号写进头部。若放行,下一级查表会 miss," +
                        "而 miss 的表现(迁不动)与真实缺陷不可分");

                // ── 每级断言②:WorldSeed 逐位不变(ADR-007 §二 硬约束)──
                ulong seedNow = ReadWorldSeed(current);
                if (seedNow != seedBefore)
                    throw new InvalidOperationException(
                        $"迁移脚本 FromVersion={v} 改变了 WorldSeed:" +
                        $"{seedBefore} ⇒ {seedNow} —— 违反 AC-7a-14 / ADR-007 §二 " +
                        "(WorldSeed 被改 ⇒ 全案病程与事件流同时作废)");

                // ── 每级断言③:已发生事件逐位不变(尾部锚定,与头部长度变化无关)──
                byte[] bodyNow = TailBody(current, bodyBefore.Length);
                if (!CryptographicOperations.FixedTimeEquals(bodyNow, bodyBefore))
                    throw new InvalidOperationException(
                        $"迁移脚本 FromVersion={v} 改变了事件正文 —— 违反 AC-7a-14 " +
                        "「已发生事件原样保留」。注意断言按**末尾等长**取段:" +
                        "若本步确实要改正文(如事件载荷加字段),那样的是 AC 变更,不是迁移");
            }

            return current;
        }

        /// <summary>读「不含校验和」形态的版本号(固定位置,版本无关)。</summary>
        /// <exception cref="InvalidOperationException">长度不足以容纳头部定长部分。</exception>
        public static uint ReadSaveVersion(byte[] dataWithoutChecksum)
        {
            RequireFixedHeader(dataWithoutChecksum, nameof(dataWithoutChecksum));
            return Bedrock.BinaryPrimitivesReadUInt32Le(
                dataWithoutChecksum, SaveHeaderCodec.SaveVersionOffsetFixed);
        }

        /// <summary>读「不含校验和」形态的 `WorldSeed`(固定位置,版本无关)。</summary>
        /// <exception cref="InvalidOperationException">长度不足以容纳头部定长部分。</exception>
        public static ulong ReadWorldSeed(byte[] dataWithoutChecksum)
        {
            RequireFixedHeader(dataWithoutChecksum, nameof(dataWithoutChecksum));
            return Bedrock.BinaryPrimitivesReadUInt64Le(
                dataWithoutChecksum, SaveHeaderCodec.WorldSeedOffsetFixed);
        }

        // ══════════════════════════════════════════════════════════════════
        // 内部:长度守卫 / 字节读取 / 尾部取段
        // ══════════════════════════════════════════════════════════════════

        private static void RequireFixedHeader(byte[] data, string name)
        {
            if (data == null) throw new ArgumentNullException(name);
            if (data.Length < SaveHeaderCodec.FixedSize)
                throw new InvalidOperationException(
                    $"迁移链:存档仅 {data.Length} 字节 < 头部定长 {SaveHeaderCodec.FixedSize}," +
                    "无法读版本号 / WorldSeed");
        }

        /// <summary>
        /// 取「正文」= 存档末尾那一段。
        /// <para>无参重载用于基线:正文起点 = 头部定长(此刻的存档必是当前已知布局)。</para>
        /// </summary>
        private static byte[] TailBody(byte[] data, int length = -1)
        {
            if (length < 0) length = data.Length - SaveHeaderCodec.FixedSize;
            if (length < 0 || length > data.Length)
                throw new InvalidOperationException(
                    $"迁移链:正文长度 {length} 超出存档长度 {data.Length}");

            var body = new byte[length];
            Array.Copy(data, data.Length - length, body, 0, length);
            return body;
        }

        private static byte[] Clone(byte[] data)
        {
            var copy = new byte[data.Length];
            Array.Copy(data, copy, data.Length);
            return copy;
        }

        private static IEnumerable<string> Describe(Dictionary<uint, ISaveMigration> byFrom)
        {
            var keys = new List<uint>(byFrom.Keys);
            keys.Sort();
            foreach (var k in keys) yield return k.ToString();
        }

        /// <summary>
        /// 小端读取的**唯一落点**。
        /// <para>承 ADR-010 §一「小端编码强制 `BinaryPrimitives` 显式小端、禁 `BitConverter`」
        /// (AC-7a-04 的单独 IL 扫描面)—— 本类不经 `CodecReader`(那个读的是**完整存档**,
        /// 而其偏移语义随版本变),故在此直调 `BinaryPrimitives` 并**集中一处**,
        /// 使「禁 `BitConverter`」这条在新增代码面上有唯一可查的落点。</para>
        /// </summary>
        private static class Bedrock
        {
            internal static uint BinaryPrimitivesReadUInt32Le(byte[] data, int offset)
                => System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(
                       new ReadOnlySpan<byte>(data, offset, 4));

            internal static ulong BinaryPrimitivesReadUInt64Le(byte[] data, int offset)
                => System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(
                       new ReadOnlySpan<byte>(data, offset, 8));
        }
    }
}
