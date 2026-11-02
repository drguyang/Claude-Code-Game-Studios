// sprint-06 G1.2 · 查体分册最小持有者(2026-10-11)
//
// 权威来源:
//   对撞裁决乙案(2026-10-11):revealed = 派生态不进三流;**「查了没有」+ snapshot_tick /
//     last_recheck_tick 住 39 分册(player 私有,铁律⑤「每人一本」)**。
//   GDD casebook-39.md(39 = 脉案持有者;快照 AC-8-25 冻结语义;掉级只改后续精度 AC-8-46)
//   diagnosis-system.md §S-8.2(快照型通道冻结;AC-8-25 采样点逐位一致)
//
// ⚠️ 最小承接,非正式 39:
//   39 侧正式分册(持久化 + 跨存档 + 掉级语义)归后续轮;本类 = **单机运行期最小持有人**,
//   先让「查了没有」的事实可查询、可传给 42。登记 NOT-RUN:跨存档 / 分人分册 /
//   正式 39 查询接口 —— 归 39 代 37 的正式实现。
//
// ⚠️ 持快照 = 持**值**(AC-8-25 冻结语义):RecordExamined 后源(8 求值)再变,
//   快照不变 —— 「查了就是查了,刷回去不改读到的」。
//
// ⚠️ 不写流、零时钟读取:lastExaminedTick 由调用方(ExamineDriver)喂入(来自 ITickProvider),
//   本类不读墙钟 / Time(AC-42-G6 同纪律的呈现侧镜像)。

using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Presentation.Diagnosis;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>
    /// 查体分册最小持有人 —— 每人(本单机玩家)一份「已查 / 未查 + 快照」记录。
    /// <para><b>呈现侧派生态</b>(「查了没有」是非模拟态;对撞乙案):不写流、不持 sim 真值,
    /// 只记「玩家做了哪次查体、当时看到什么」。</para>
    /// </summary>
    public sealed class ExaminationLedger
    {
        /// <summary>一张查体快照(冻结值;AC-8-25)。</summary>
        public readonly struct ExaminationSnapshot
        {
            /// <summary>该病人最近一次查体的完成 tick(供墨龄 / 过期判定)。</summary>
            public readonly long ExaminedTick;

            /// <summary>该次查体冻结的呈现集(值,不回源)。</summary>
            public readonly ExaminedSet Set;

            public ExaminationSnapshot(long examinedTick, ExaminedSet set)
            {
                ExaminedTick = examinedTick;
                Set = set;
            }
        }

        // 🔁 2026-11-02(结清 `TD-diagnosis-01`):**按通道分片**持有。
        //    旧形态 = 每病人**一张**整病人快照 ⇒ 行级查体(脉案五行各自 Submit)会把上一行的
        //    结果整张覆盖掉。设计口径(`S-8.4` 路线甲)是「查体 = 模态行级动作」,
        //    故「查了哪几行」必须分别记。
        private readonly Dictionary<int, Dictionary<SignChannel, ExaminationSnapshot>> _byChannel =
            new Dictionary<int, Dictionary<SignChannel, ExaminationSnapshot>>();

        /// <summary>已记录查体的病人数(0 = 未查任何病人)。</summary>
        public int RecordedCount => _byChannel.Count;

        /// <summary>记录**某一通道**的查体完成(同通道复查覆盖旧读,AC-8-46 语义)。</summary>
        /// <param name="patientId">病人 id。</param>
        /// <param name="tick">完成 tick(由调用方喂 ITickProvider 值;本类零时钟)。</param>
        /// <param name="channel">被查的那一行(脉案六行之一)。</param>
        /// <param name="set">该通道冻结的呈现集。</param>
        public void RecordExamined(int patientId, long tick, SignChannel channel, ExaminedSet set)
        {
            if (!_byChannel.TryGetValue(patientId, out var channels))
            {
                channels = new Dictionary<SignChannel, ExaminationSnapshot>();
                _byChannel[patientId] = channels;
            }
            channels[channel] = new ExaminationSnapshot(tick, set);
        }

        /// <summary>该病人该通道是否已查(「查了哪几行」的布尔事实)。</summary>
        public bool HasExaminedChannel(int patientId, SignChannel channel)
            => _byChannel.TryGetValue(patientId, out var channels) && channels.ContainsKey(channel);

        /// <summary>该病人**已查的通道数**(0..6)。</summary>
        public int ExaminedChannelCount(int patientId)
            => _byChannel.TryGetValue(patientId, out var channels) ? channels.Count : 0;

        /// <summary>
        /// 取该病人**已查各行合并后的**快照;一行未查 ⇒ <c>false</c>。
        /// <para>⚠️ 合并口径(供 42 值区一次性读):词条按通道枚举序拼接 · 掩码按位或 ·
        /// 条数求和 · 精度档取**最高**已查行的档(玩家做过的最细那次)。</para>
        /// <para>`ExaminedTick` = 最近一次查体的 tick(任一通道)。</para>
        /// </summary>
        public bool TryGetSnapshot(int patientId, out ExaminationSnapshot snapshot)
        {
            snapshot = default;
            if (!_byChannel.TryGetValue(patientId, out var channels) || channels.Count == 0)
                return false;

            var rows = new List<SignLexemeRow>();
            int mask = 0;
            int count = 0;
            int slot = 0;
            long tick = -1L;

            // 固定按枚举序拼接 —— 不依赖 Dictionary 的枚举次序(可重放)。
            for (int c = 0; c < ChannelOrder.Length; c++)
            {
                if (!channels.TryGetValue(ChannelOrder[c], out var snap)) continue;

                if (snap.Set.Rows != null) rows.AddRange(snap.Set.Rows);
                mask |= snap.Set.SignChannelMask;
                count += snap.Set.SignCount;
                if (snap.Set.PrecisionSlot > slot) slot = snap.Set.PrecisionSlot;
                if (snap.ExaminedTick > tick) tick = snap.ExaminedTick;
            }

            snapshot = new ExaminationSnapshot(tick,
                new ExaminedSet(rows.AsReadOnly(), mask, count, slot));
            return true;
        }

        /// <summary>固定拼接序(与脉案行序一致;不依赖 `Dictionary` 枚举序)。</summary>
        private static readonly SignChannel[] ChannelOrder =
        {
            SignChannel.FaceColor, SignChannel.Voice, SignChannel.Posture,
            SignChannel.Breathing, SignChannel.Touch, SignChannel.History,
        };

        /// <summary>病人是否已查过任意一行(「查了没有」的布尔事实 —— 不是「值是多少」)。</summary>
        public bool HasExamined(int patientId)
            => _byChannel.TryGetValue(patientId, out var channels) && channels.Count > 0;

        /// <summary>该病人最近一次查体(任一通道)的 tick(测试接缝;未查 ⇒ −1)。
        /// <para>供「按下沿只触发一次」判据:同 tick 覆盖 = 一次,二次覆盖推高。</para></summary>
        public long GetSnapshotTick(int patientId)
        {
            if (!_byChannel.TryGetValue(patientId, out var channels)) return -1L;
            long best = -1L;
            foreach (var snap in channels.Values)
                if (snap.ExaminedTick > best) best = snap.ExaminedTick;
            return best;
        }

        /// <summary>清空(测试 / 会话级重置)。</summary>
        public void Clear() => _byChannel.Clear();
    }
}