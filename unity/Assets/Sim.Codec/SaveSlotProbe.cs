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
// ⚠️ **与 `LoadBytes` 的回退链同源**:final 读不出 ⇒ 再看 `.bak`,**全部槽型一致**。
//    判据归 `SaveSlotLedger.BakCountsAsSave`(唯一定义处),本类、`SaveService` 与
//    「最近一份」三处都调它。不同源就会出现「屏上说有档、读档说没有」这种最难查的分叉。
//    🔁 **2026-11-05(ADR-041 §D4 修订)**:此前手动槽的 `.bak` 不参与,理由是「写侧产不出来,
//    盘上有一份必是旧残留」。覆写功能上线后该前提失效(`.bak` 就是这一槽的上一版),
//    而拒绝恢复的代价是「主档没了 ⇒ 那份世界不可达」。已按行业惯例统一。
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

        /// <summary>
        /// 该档记录的时间三参数(ADR-038 §D3;v3 及更早的档为 0 = 未知)。
        /// <para>⚠️ **每份档用自己的**(不是当前构建的)—— 否则改过日长之后,
        /// 旧档行会按新值画出「第几日的昼夜」,与玩家当时看到的不是同一天。</para>
        /// </summary>
        public readonly SaveTimeParams TimeParams;

        /// <summary>
        /// 这份档的字节**来自哪个文件** —— <c>Final</c> = 正式档,<c>Bak</c> = 只剩备份。
        /// <para>🔁 **2026-11-05 新增(ADR-041 §D4 修订)**:探测与载入统一认 `.bak` 之后,
        /// 「这个槽有档」不再能区分「主档在」与「只剩备份」。差值本身是玩家/客服都要知道的事实
        /// (行业惯例:恢复自备份的档可以照常显示,但要说得出它来自备份)。**取值归 7a,
        /// 要不要显示、怎么显示归 42**(拟物语言里不得降级成徽章 / 红字,`AC-7b-07`)。</para>
        /// </summary>
        public readonly SaveLoadSource Source;

        /// <summary>构造(v3 及更早:无时间参数 ⇒ 全 0)。</summary>
        public SaveSlotSummary(SaveSlotState state, long tick, uint saveVersion)
            : this(state, tick, saveVersion, default(SaveTimeParams), SaveLoadSource.None)
        {
        }

        /// <summary>带来源的构造(见 <see cref="Source"/>)。</summary>
        public SaveSlotSummary(SaveSlotState state, long tick, uint saveVersion,
                               SaveTimeParams timeParams, SaveLoadSource source)
        {
            State = state;
            Tick = tick;
            SaveVersion = saveVersion;
            TimeParams = timeParams;
            Source = source;
        }

        /// <summary>复制并改写来源(探测时「先读文件、再标来源」用)。</summary>
        public SaveSlotSummary WithSource(SaveLoadSource source)
            => new SaveSlotSummary(State, Tick, SaveVersion, TimeParams, source);

        /// <summary>构造(v4:带该档记录的时间三参数;来源留待调用方标)。</summary>
        public SaveSlotSummary(SaveSlotState state, long tick, uint saveVersion,
                               SaveTimeParams timeParams)
            : this(state, tick, saveVersion, timeParams, SaveLoadSource.None)
        {
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

            // ⚠️ 路径命名**必须**与 SaveService 的 FinalNameOf / SuffixNameOf 逐字一致。
            //    两处各写一份是本项目反复抓到的「双实现必然漂移」形态;
            //    此处刻意照抄同一套规则,并由夹具 `test_probe_pathMatchesSaveService`
            //    以**真的存一份再探测**来钉它(不是比字符串)。
            // 🔁 2026-11-03(`TD-persist-17`):槽号现在**有三种形状** ——
            //    1 = 退出保存(`save_exit.bin`)· 2 = 自动存档(`save_auto.bin`)· 3+ = 手动(`save_{n}.bin`)。
            //    文件名常量**引用** `SaveService` 的公开常量,不复制字面量。
            bool reserved = SaveSlotLedger.IsReservedSlotSeq(slotSeq);
            string stem = slotSeq == SaveSlotLedger.ExitSlotSeq ? SaveService.ExitSaveFileName
                        : slotSeq == SaveSlotLedger.AutoSlotSeq ? SaveService.AutoSaveFileName
                        : $"save_{slotSeq}.bin";
            string finalPath = Path.Combine(basePath, stem);
            // 保留槽的旁路文件是 `xxx.bin.bak`,手动槽是 `save_{n}.bak` —— 两种形状必须分开。
            // 🔁 **2026-11-05(ADR-041 §D4 修订):全部槽型都认 `.bak`**,与读档同向。
            //    判据唯一出处 = `SaveSlotLedger.BakCountsAsSave`(恒 true),本处不再自带分派 ——
            //    「探测认不认 bak」与「读档认不认 bak」是一件事,分成两处就必然有一天分叉。
            string bakPath = Path.Combine(basePath, reserved ? stem + ".bak" : $"save_{slotSeq}.bak");

            try
            {
                if (File.Exists(finalPath))
                    return ReadOne(finalPath).WithSource(SaveLoadSource.Final);

                // 主档没了但备份在 ⇒ **这是一份可恢复的档**,不是空槽(行业惯例:
                // 备份的语义就是「主档没了用它」)。来源标出来,供呈现层决定要不要说明。
                if (File.Exists(bakPath))
                    return ReadOne(bakPath).WithSource(SaveLoadSource.Bak);

                return new SaveSlotSummary(SaveSlotState.Empty, 0L, 0U);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                // 「我不知道」不是「没有」。折成 Unknown,呈现侧画零字。
                return new SaveSlotSummary(SaveSlotState.Unknown, 0L, 0U);
            }
        }

        /// <summary>
        /// 探一个**具体文件**(不按槽号拼路径)。
        /// <para>用途:`SaveService.TryGetMostRecentSave` 要在「退出 / 自动 / 手动」三类候选里
        /// 比 tick,而前两者的文件名不在 `save_{N}.bin` 命名空间里 —— 路径由 7a 自己给,
        /// 命名约定仍只在 7a 内部。</para>
        /// <para>语义与 <see cref="Probe"/> 同:文件不存在 ⇒ <c>Empty</c>;读不出 ⇒ <c>Unknown</c>;
        /// 在但解不出 ⇒ <c>Corrupt</c>。**不抛**。</para>
        /// </summary>
        public static SaveSlotSummary ProbeFile(string path)
        {
            if (string.IsNullOrEmpty(path))
                return new SaveSlotSummary(SaveSlotState.Unknown, 0L, 0U);
            try
            {
                return File.Exists(path)
                    ? ReadOne(path)
                    : new SaveSlotSummary(SaveSlotState.Empty, 0L, 0U);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                return new SaveSlotSummary(SaveSlotState.Unknown, 0L, 0U);
            }
        }

        /// <summary>读一份文件的头部;文件在但解不出 ⇒ <see cref="SaveSlotState.Corrupt"/>。</summary>
        private static SaveSlotSummary ReadOne(string path)
        {
            byte[] buf;
            int read = 0;
            try
            {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read,
                                               FileShare.ReadWrite))
                {
                    if (fs.Length < SaveHeaderCodec.HeaderSize)
                        return new SaveSlotSummary(SaveSlotState.Corrupt, 0L, 0U);

                    // ADR-038 §D3 / Amendment A:v4 头 +24 B、v5 头 +32 B。按**文件实际长度**取
                    // (不超过 v5 头),这样 v3 / v4 档照样能读 —— 短读不构成损坏;
                    // 声明 v5 而读不满才由 `SaveHeaderCodec.Read` 的「长度不足」判 Corrupt。
                    int want = (int)System.Math.Min(fs.Length, SaveHeaderCodec.HeaderSizeV5);
                    buf = new byte[want];

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
                SaveTimeParams time = header.SaveVersion >= 5
                    ? new SaveTimeParams(header.TicksPerDay, header.NightStart, header.NightSpan,
                                         header.TicksPerSeason, header.SeasonsPerYear,
                                         header.WeatherBlockTicks, header.IntensityMax)
                    : default(SaveTimeParams);
                return new SaveSlotSummary(SaveSlotState.Occupied, header.Tick,
                                           header.SaveVersion, time);
            }
            catch (InvalidDataException)
            {
                // magic 不符 ⇒ 这个文件不是我们的存档(或已损坏)。
                return new SaveSlotSummary(SaveSlotState.Corrupt, 0L, 0U);
            }
        }
    }
}
