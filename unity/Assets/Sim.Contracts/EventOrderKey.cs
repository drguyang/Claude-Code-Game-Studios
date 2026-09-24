// 权威来源:ADR-008 §跨流全序键 (Tick, StreamPriority, Patient, Seq)(§一 · §二 · §Key Interfaces)
//          · ADR-006 Amendment C(三流全序键的字段定型)
//          · ADR-007 §四(PatientId.None = -1 哨兵;世界级 / 病史级事件不污染高水位)
//          · Story 009 AC-21a-52 ⑤(Craft 全序键可排全序;actor_id 位由载荷吸收,**不进键** ——
//            D-21-28 裁定 [甲]:21a 曾建议的 (Tick, ActorId, Seq) 被本键吸收否决)
//
// 排序语义 = 逐分量字典序:Tick 升 → StreamPriority 升 → Patient.Value 升 → Seq 升。
//   · 同流事件 StreamPriority 恒相同 ⇒ 单流内退化为 (Tick, Patient, Seq)
//     (ADR-005:265-266 的单流序)—— 与跨流序一致、不冲突。
//   · StreamPriority 恒由 StreamId 成员值派生(成员序 = 优先序,StreamId.cs 头注钉死,
//     改成员序 = 改全序语义须另裁 ADR)。
//   · 键的可比性**不依赖 EventKind 枚举值**(EventKind.cs 头注:成员序不承载语义)。
//
// ⚠️ 住 Sim.Contracts(BCL-only 面)是刻意的:7a 持久化折叠排序、45 网络重排
//    (ReorderBuffer)、51 回放读取器都要这条键,而它们引用不到某个具体系统程序集。
//    Kind → StreamId 的解析(需 kindgen 路由表)不在本文件 —— 那一半住 Sim 的 EventOrder。

using System;

namespace DaYiJingCheng.Sim.Contracts
{
    /// <summary>三流事件的跨流全序键(ADR-008)。全整数、不可变、可排序;禁 float。</summary>
    public readonly struct EventOrderKey : IComparable<EventOrderKey>, IEquatable<EventOrderKey>
    {
        /// <summary>逻辑 tick(20 Hz ⇒ 50 ms/tick)。全案唯一时间量纲。</summary>
        public readonly long Tick;

        /// <summary>流优先级 = (int)<see cref="StreamId"/>(History 0 &lt; Case 1 &lt; World 2)。</summary>
        public readonly int StreamPriority;

        /// <summary>病人 / 受伤实体位。世界级与「无病人」事件 = <see cref="PatientId.None"/>(-1)。</summary>
        public readonly PatientId Patient;

        /// <summary>主机 Append 时发放的单调流水号(同 (Tick, Stream, Patient) 内唯一)。</summary>
        public readonly long Seq;

        /// <param name="tick">逻辑 tick。</param>
        /// <param name="stream">所属逻辑流(优先级由成员值派生,不可手填 —— 防第二真源)。</param>
        /// <param name="patient">病人位;无病人语义用 <see cref="PatientId.None"/>。</param>
        /// <param name="seq">同 (Tick, Stream, Patient) 内的主机流水号。</param>
        public EventOrderKey(long tick, StreamId stream, PatientId patient, long seq)
        {
            Tick = tick;
            StreamPriority = (int)stream;
            Patient = patient;
            Seq = seq;
        }

        /// <summary>逐分量字典序:Tick → StreamPriority → Patient → Seq。</summary>
        public int CompareTo(EventOrderKey other)
        {
            int c = Tick.CompareTo(other.Tick);
            if (c != 0) return c;
            c = StreamPriority.CompareTo(other.StreamPriority);
            if (c != 0) return c;
            c = Patient.Value.CompareTo(other.Patient.Value);
            if (c != 0) return c;
            return Seq.CompareTo(other.Seq);
        }

        public bool Equals(EventOrderKey other) => CompareTo(other) == 0;
        public override bool Equals(object obj) => obj is EventOrderKey k && Equals(k);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = Tick.GetHashCode();
                h = (h * 397) ^ StreamPriority;
                h = (h * 397) ^ Patient.GetHashCode();
                return (h * 397) ^ Seq.GetHashCode();
            }
        }

        public static bool operator ==(EventOrderKey a, EventOrderKey b) => a.Equals(b);
        public static bool operator !=(EventOrderKey a, EventOrderKey b) => !a.Equals(b);
        public static bool operator <(EventOrderKey a, EventOrderKey b) => a.CompareTo(b) < 0;
        public static bool operator >(EventOrderKey a, EventOrderKey b) => a.CompareTo(b) > 0;
        public static bool operator <=(EventOrderKey a, EventOrderKey b) => a.CompareTo(b) <= 0;
        public static bool operator >=(EventOrderKey a, EventOrderKey b) => a.CompareTo(b) >= 0;
    }
}
