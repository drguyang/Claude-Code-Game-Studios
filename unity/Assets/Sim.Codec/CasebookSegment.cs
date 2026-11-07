// ADR-010 §三 义务 13 —— 存档 **v7** 的「分册态段」编解码。
//
// 权威来源:
//   docs/architecture/adr-010-persistence-save-format.md §一(段槽)· §三 义务 13(字段清单)
//   design/gdd/casebook.md 规则一 / 规则二 · AC-39-13(痕不落盘)
//   ADR-037 §①(「插在头部之后」的理由:尾部追加会破迁移链的 TailBody 断言)
//   production/epics/casebook/story-001-volume-state-contract-and-save-segment.md
//
// ── 段形状(定长记录 + count 前缀,自描述)─────────────────────────────────
//
//   volumeCount : i32
//   volumeCount × { playerId : i32 , pageIndex : i32 }                        // 8 B / 条
//   pageCount   : i32
//   pageCount   × { playerId:i32 , patientId:i32 , snapshotTick:i64 ,
//                   recheckTick:i64 , folded:i32 }                            // 24 B / 条
//
// 自描述 ⇒ **无需头部偏移字段**(同 ADR-010 v1 增补 ③ 的折叠段先例)。
//
// ── 三条硬判据(解码侧执行,不靠写侧自觉)──────────────────────────────────
// 1. **规范形唯一**:册级按 `PlayerId` **严格升序**;页级按 `(PlayerId, PatientId)`
//    **严格升序**。乱序 / 重复 ⇒ 拒收。理由同 ADR-037 §②:同态存档在两台机上必须
//    逐字节相同,否则字节级对拍无从成立。
// 2. **`count = 0` 是合法语义**(新世界尚无分册态),不是损坏;但**负 count 是损坏**。
// 3. **不静默容错**:越界 / 负 count / `folded ∉ {0,1}` ⇒ 抛 `InvalidDataException`。
//    把「不知道」写成「空分册」是本段最坏的失败模式 —— 读侧会以为玩家从没翻过册子。
//
// ⚠️ **本段里没有「已落笔判断」的任何字节**(用户 2026-11-08 裁定甲案)。
//    卡面 AC-1 原写的「呈现缓存(分册正文)」与 ADR-010 义务 13 + AC-39-13 冲突,
//    已按架构口径收窄;正文 = 病例流的读时投影(F-39.2),不进存档。

using System;
using System.Collections.Generic;
using System.IO;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.Codec
{
    /// <summary>存档 v7 分册态段的编解码(纯函数,零 I/O)。</summary>
    public static class CasebookSegment
    {
        /// <summary>册级记录定长 = <c>playerId:i32 + pageIndex:i32</c>。</summary>
        public const int VolumeRecordSize = 4 + 4;

        /// <summary>页级记录定长 = <c>playerId:i32 + patientId:i32 + snapshotTick:i64 + recheckTick:i64 + folded:i32</c>。</summary>
        public const int PageRecordSize = 4 + 4 + 8 + 8 + 4;

        /// <summary>空段的字节数 = 两个 <c>count</c> 前缀。</summary>
        public const int EmptySize = 4 + 4;

        /// <summary>
        /// 编码一段。**按规范形写出**(册级按 `PlayerId`、页级按 `(PlayerId, PatientId)` 升序),
        /// 与输入序无关。
        /// </summary>
        /// <param name="snapshot"><c>null</c> ⇒ 写出空段(<c>count = 0</c> / <c>count = 0</c>)。</param>
        /// <exception cref="ArgumentException">册级 `PlayerId` 重复,或页级 `(PlayerId, PatientId)` 重复。</exception>
        public static byte[] Encode(CasebookVolumeSnapshot snapshot)
        {
            int volumeCount = snapshot?.Volumes?.Count ?? 0;
            int pageCount = snapshot?.Pages?.Count ?? 0;

            var volumes = new CasebookVolumeRecord[volumeCount];
            for (int i = 0; i < volumeCount; i++) volumes[i] = snapshot.Volumes[i];
            Array.Sort(volumes, (a, b) => a.PlayerId.CompareTo(b.PlayerId));
            for (int i = 1; i < volumes.Length; i++)
            {
                if (volumes[i - 1].PlayerId == volumes[i].PlayerId)
                    throw new ArgumentException(
                        $"分册态段:分册键 {volumes[i].PlayerId} 出现两次 —— 一名玩家只能有一本分册(规则二)",
                        nameof(snapshot));
            }

            var pages = new CasebookPageRecord[pageCount];
            for (int i = 0; i < pageCount; i++) pages[i] = snapshot.Pages[i];
            Array.Sort(pages, ComparePage);
            for (int i = 1; i < pages.Length; i++)
            {
                if (pages[i - 1].PlayerId == pages[i].PlayerId &&
                    pages[i - 1].PatientId == pages[i].PatientId)
                {
                    throw new ArgumentException(
                        $"分册态段:页级 (player={pages[i].PlayerId}, patient={pages[i].PatientId}) 出现两次 " +
                        "—— 同一病人只能有一页",
                        nameof(snapshot));
                }
            }

            var w = new CodecWriter(EmptySize + volumeCount * VolumeRecordSize + pageCount * PageRecordSize);

            w.WriteInt32LittleEndian(volumeCount);
            for (int i = 0; i < volumes.Length; i++)
            {
                w.WriteInt32LittleEndian(volumes[i].PlayerId);
                w.WriteInt32LittleEndian(volumes[i].PageIndex);
            }

            w.WriteInt32LittleEndian(pageCount);
            for (int i = 0; i < pages.Length; i++)
            {
                w.WriteInt32LittleEndian(pages[i].PlayerId);
                w.WriteInt32LittleEndian(pages[i].PatientId);
                w.WriteInt64LittleEndian(pages[i].SnapshotTick);
                w.WriteInt64LittleEndian(pages[i].RecheckTick);
                w.WriteInt32LittleEndian(pages[i].Folded);
            }

            return w.ToArray();
        }

        /// <summary>
        /// 从 <paramref name="data"/> 的 <paramref name="offset"/> 处读一段。
        /// </summary>
        /// <param name="consumed">实际消耗的字节数(含两个 count 前缀)—— 供调用方推进游标。</param>
        /// <exception cref="InvalidDataException">负 count / 越界 / 非严格升序 / 重复 / `folded` 越界。</exception>
        public static CasebookVolumeSnapshot Decode(byte[] data, int offset, out int consumed)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (offset < 0 || offset > data.Length)
                throw new ArgumentOutOfRangeException(nameof(offset));

            var r = new CodecReader(new ReadOnlySpan<byte>(data, offset, data.Length - offset));

            if (r.Remaining < 4)
                throw new InvalidDataException(
                    $"分册态段缺失:偏移 {offset} 处仅剩 {r.Remaining} 字节(段头 4 字节)");
            int volumeCount = r.ReadInt32LittleEndian();
            if (volumeCount < 0)
                throw new InvalidDataException($"分册态段 volumeCount={volumeCount} 为负 —— 已损坏");
            // 分配前按剩余字节复验(防损坏 count 触发巨额分配)
            if ((long)volumeCount * VolumeRecordSize > r.Remaining)
                throw new InvalidDataException(
                    $"分册态段 volumeCount={volumeCount} 至少需 {volumeCount * (long)VolumeRecordSize} 字节," +
                    $"仅剩 {r.Remaining} —— 已损坏");

            var volumes = new CasebookVolumeRecord[volumeCount];
            for (int i = 0; i < volumeCount; i++)
            {
                int playerId = r.ReadInt32LittleEndian();
                int pageIndex = r.ReadInt32LittleEndian();

                if (i > 0 && volumes[i - 1].PlayerId >= playerId)
                {
                    throw new InvalidDataException(
                        $"分册态段分册键非严格升序:{volumes[i - 1].PlayerId} → {playerId}" +
                        "(规范形唯一是判据;乱序 / 重复说明写侧或字节已漂移)");
                }

                // 复用构造器的守卫(负页码在这里也要拒 —— 但**不**静默归一成 0)
                try
                {
                    volumes[i] = new CasebookVolumeRecord(playerId, pageIndex);
                }
                catch (ArgumentOutOfRangeException ex)
                {
                    throw new InvalidDataException(
                        $"分册态段第 {i} 条(分册键 {playerId})字段非法:{ex.Message}", ex);
                }
            }

            if (r.Remaining < 4)
                throw new InvalidDataException(
                    $"分册态段缺页级 count 前缀(仅剩 {r.Remaining} 字节)—— 已损坏");
            int pageCount = r.ReadInt32LittleEndian();
            if (pageCount < 0)
                throw new InvalidDataException($"分册态段 pageCount={pageCount} 为负 —— 已损坏");
            if ((long)pageCount * PageRecordSize > r.Remaining)
                throw new InvalidDataException(
                    $"分册态段 pageCount={pageCount} 至少需 {pageCount * (long)PageRecordSize} 字节," +
                    $"仅剩 {r.Remaining} —— 已损坏");

            var pages = new CasebookPageRecord[pageCount];
            for (int i = 0; i < pageCount; i++)
            {
                int playerId = r.ReadInt32LittleEndian();
                int patientId = r.ReadInt32LittleEndian();
                long snapshotTick = r.ReadInt64LittleEndian();
                long recheckTick = r.ReadInt64LittleEndian();
                int folded = r.ReadInt32LittleEndian();

                if (i > 0)
                {
                    int cmp = pages[i - 1].PlayerId != playerId
                        ? pages[i - 1].PlayerId.CompareTo(playerId)
                        : pages[i - 1].PatientId.CompareTo(patientId);
                    if (cmp >= 0)
                    {
                        throw new InvalidDataException(
                            $"分册态段页级非严格升序:(player={pages[i - 1].PlayerId}, " +
                            $"patient={pages[i - 1].PatientId}) → (player={playerId}, patient={patientId})" +
                            " —— 规范形唯一是判据");
                    }
                }

                try
                {
                    pages[i] = new CasebookPageRecord(playerId, patientId, snapshotTick, recheckTick, folded);
                }
                catch (ArgumentOutOfRangeException ex)
                {
                    throw new InvalidDataException(
                        $"分册态段页级第 {i} 条(player={playerId}, patient={patientId})字段非法:{ex.Message}", ex);
                }
            }

            consumed = r.Position;
            return new CasebookVolumeSnapshot(volumes, pages);
        }

        private static int ComparePage(CasebookPageRecord a, CasebookPageRecord b)
        {
            int c = a.PlayerId.CompareTo(b.PlayerId);
            return c != 0 ? c : a.PatientId.CompareTo(b.PatientId);
        }
    }
}
