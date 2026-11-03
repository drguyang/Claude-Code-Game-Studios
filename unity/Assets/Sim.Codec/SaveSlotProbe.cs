// 7b 存档册的**行内状态来源**(2026-11-03 甲方案第一层)。
//
// 权威来源:
//   design/ux/save-slots-7b.md §5.4(行内字段)· §6 状态表(未绑定 / 空册)
//   design/gdd/skeuomorphic-ui.md `AC-42-B5`(空位**可聚焦但零文本**)
//   design/ux/save-slots-7b.md `OQ-SB-5`(「42 不知道哪个槽有存档 ⇒ 空槽与有档槽长得一样」)
//
// ──────────────────────────────────────────────────────────────────────────────
// 为什么需要这个类型
// ──────────────────────────────────────────────────────────────────────────────
// 2026-11-03 用户 smoke 实测:「存档和读档看不出差异」。根因不是渲染没写,
// 而是**42 拿不到「这个槽里有没有档」这一个布尔** —— `SaveService` 只有
// 「整份读写」,没有「只看头部」的便宜路;装配层因此只能把
// `BootRoot.ManualSaveSlots = {1,2,3}` 这个**常量**灌给屏,屏上四条行一模一样。
//
// 本类补的就是那条便宜路:**只读前 `SaveHeaderCodec.HeaderSize` 字节**(68 B),
// 不读正文、不验校验和、不装 blob 池、不进迁移链。
//
// ⚠️ **不是 `Load` 的替代品**:`Load` 的每一道门(校验和 / 版本 / 派生态重建)
//    本类**一道都不复制**,也不提供任何「读出来的东西能用」的承诺 ——
//    它只回答「这个号上有没有一份像样的档,以及它停在哪个 tick」。
//
// ⚠️ **与 `LoadBytes` 的回退链同源**:final 读不出 ⇒ 再看 `.bak`。两者若不同源,
//    会出现「屏上说有档、读档说没有」这种最难查的分叉。
//
// ⚠️ **写盘面零触碰** —— 本类只有 `File.Exists` / `FileStream(...Read)` / `Read`。
//    `SaveWritePathGate` 的 IL 判据扫的是 `Write` / `WriteAsync` / `Flush` / `WriteByte`
//    (按 `System.IO.FileStream` 类型判),本类一条都不命中。

using System;
using System.IO;

namespace DaYiJingCheng.Sim.Codec
{
    /// <summary>槽位的**呈现侧**状态(只陈述可观测事实,不陈述「能不能读」)。</summary>
    public enum SaveSlotState
    {
        /// <summary>
        /// 探测失败(IO 错 / 权限 / 路径未注入)—— **与「空槽」刻意分开**。
        /// <para>把 IO 失败画成空槽 = 对玩家撒谎(「这里什么都没有」),而真相是
        /// 「我不知道」。呈现侧据此画**零字**(未绑定),不画空槽语义。</para>
        /// </summary>
        Unknown = 0,

        /// <summary>final 与 bak **都不存在** ⇒ 这一槽从未写过。</summary>
        Empty = 1,

        /// <summary>头部可解(magic 对、长度够)⇒ 有一份看起来正常的档。</summary>
        Occupied = 2,

        /// <summary>
        /// 文件**在**,但头部解不出(长度不足 / magic 不符)。
        /// <para>⚠️ 同样**不**画成空槽:玩家会以为这里能写新档,而 7a 的
        /// `Load` 会拒。残缺的档与空的槽在物理上是两件事。</para>
        /// </summary>
        Corrupt = 3,
    }

    /// <summary>一个槽位的探测结果。</summary>
    public readonly struct SaveSlotSummary
    {
        /// <summary>呈现侧状态。</summary>
        public readonly SaveSlotState State;

        /// <summary>存档头里的 tick(<see cref="SaveSlotState.Occupied"/> 之外恒 0)。</summary>
        public readonly long Tick;

        /// <summary>存档格式版本(<see cref="SaveSlotState.Occupied"/> 之外恒 0)。</summary>
        public readonly uint SaveVersion;

        /// <summary>构造。</summary>
        public SaveSlotSummary(SaveSlotState state, long tick, uint saveVersion)
        {
            State = state;
            Tick = tick;
            SaveVersion = saveVersion;
        }
    }

    /// <summary>
    /// 槽位探测器 —— 只读存档头部,判「这一槽有没有档」。
    /// <para>纯静态、无状态、可重复调用(装配层每次开册前调一次即可)。</para>
    /// </summary>
    public static class SaveSlotProbe
    {
        /// <summary>探测一个槽位。</summary>
        /// <param name="basePath">存档目录(与 <see cref="SaveService"/> 同一个)。</param>
        /// <param name="slotSeq">槽位号。</param>
        /// <returns>探测结果;**不抛** —— 一切异常都折成 <see cref="SaveSlotState.Unknown"/>。</returns>
        public static SaveSlotSummary Probe(string basePath, uint slotSeq)
        {
            if (string.IsNullOrEmpty(basePath))
                return new SaveSlotSummary(SaveSlotState.Unknown, 0L, 0U);

            // ⚠️ 路径命名**必须**与 SaveService.GetFinalPath / GetBakPath 逐字一致。
            //    两处各写一份是本项目反复抓到的「双实现必然漂移」形态;
            //    此处刻意照抄同一格式串,并由夹具 `test_probe_pathMatchesSaveService`
            //    以**真的存一份再探测**来钉它(不是比字符串)。
            string finalPath = Path.Combine(basePath, $"save_{slotSeq}.bin");
            string bakPath = Path.Combine(basePath, $"save_{slotSeq}.bak");

            try
            {
                if (File.Exists(finalPath))
                    return ReadOne(finalPath);

                if (File.Exists(bakPath))
                    return ReadOne(bakPath);

                return new SaveSlotSummary(SaveSlotState.Empty, 0L, 0U);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                // 「我不知道」不是「没有」。折成 Unknown,呈现侧画零字。
                return new SaveSlotSummary(SaveSlotState.Unknown, 0L, 0U);
            }
        }

        /// <summary>读一份文件的头部;文件在但解不出 ⇒ <see cref="SaveSlotState.Corrupt"/>。</summary>
        private static SaveSlotSummary ReadOne(string path)
        {
            byte[] buf = new byte[SaveHeaderCodec.HeaderSize];
            int read = 0;
            try
            {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read,
                                               FileShare.ReadWrite))
                {
                    if (fs.Length < SaveHeaderCodec.HeaderSize)
                        return new SaveSlotSummary(SaveSlotState.Corrupt, 0L, 0U);

                    while (read < buf.Length)
                    {
                        int n = fs.Read(buf, read, buf.Length - read);
                        if (n <= 0) break;
                        read += n;
                    }
                }
            }
            catch (IOException) { return new SaveSlotSummary(SaveSlotState.Corrupt, 0L, 0U); }
            catch (UnauthorizedAccessException)
            {
                return new SaveSlotSummary(SaveSlotState.Unknown, 0L, 0U);
            }

            if (read < buf.Length)
                return new SaveSlotSummary(SaveSlotState.Corrupt, 0L, 0U);

            try
            {
                SaveHeader header = SaveHeaderCodec.Read(buf);
                return new SaveSlotSummary(SaveSlotState.Occupied, header.Tick,
                                           header.SaveVersion);
            }
            catch (InvalidDataException)
            {
                // magic 不符 ⇒ 这个文件不是我们的存档(或已损坏)。
                return new SaveSlotSummary(SaveSlotState.Corrupt, 0L, 0U);
            }
        }
    }
}
