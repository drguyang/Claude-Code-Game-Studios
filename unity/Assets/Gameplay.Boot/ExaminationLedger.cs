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

        private readonly Dictionary<int, ExaminationSnapshot> _snapshots =
            new Dictionary<int, ExaminationSnapshot>();

        /// <summary>已记录查体的病人数(0 = 未查任何病人)。</summary>
        public int RecordedCount => _snapshots.Count;

        /// <summary>记录一次查体完成(覆盖既有快照 —— 复查覆盖旧读,AC-8-46 语义)。</summary>
        /// <param name="patientId">病人 id。</param>
        /// <param name="tick">完成 tick(由调用方喂 ITickProvider 值;本类零时钟)。</param>
        /// <param name="set">冻结的呈现集。</param>
        public void RecordExamined(int patientId, long tick, ExaminedSet set)
        {
            _snapshots[patientId] = new ExaminationSnapshot(tick, set);
        }

        /// <summary>取该病人最近查体快照;未查 ⇒ <c>false</c>。</summary>
        public bool TryGetSnapshot(int patientId, out ExaminationSnapshot snapshot)
            => _snapshots.TryGetValue(patientId, out snapshot);

        /// <summary>病人是否已查过(「查了没有」的布尔事实 —— 不是「值是多少」)。</summary>
        public bool HasExamined(int patientId) => _snapshots.ContainsKey(patientId);

        /// <summary>清空(测试 / 会话级重置)。</summary>
        public void Clear() => _snapshots.Clear();
    }
}