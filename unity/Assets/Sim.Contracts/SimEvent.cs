// 权威来源:ADR-005 §Key Interfaces(:252-259)· ADR-006 Amendment A(:246 起)
//          · ADR-024 §①(Kind 清单的真源 = design/registry/entities.yaml,共 34 支)
//
// ⚠️ 与 ADR-005:257 的**字面**有一处必要偏离,登记如下:
//   ADR-005 写 `public readonly EventKind Kind;`(字段名 Kind,类型名 EventKind);
//   ADR-024 的生成器语法写 `SimEvent.Kind.*`(Kind 是嵌套类型)。
//   **两者在 C# 中不可能同时成立** —— 同一 struct 内「字段 Kind」与「嵌套类型 Kind」同名 = CS0102。
//   落地取 **类型住命名空间作用域、名为 `EventKind`**,理由:
//   ① ADR-005 的字段声明自此**逐字可编译**(它是被五份 ADR 引用的形状);
//   ② ADR-024 的 A5 双向差集比的是**末段成员名**(`.InjuryOnset` 等),不比类型名前缀,
//      故真源纪律不受影响;kindgen 侧的对应口径登记为 **b6 实现期义务**。
//   若验收要「`SimEvent.Kind.` 字面可编译」,则须反过来牺牲 ① —— 那属 B 路,待裁。
//
// ⚠️ EventPayload 当前是**占位空结构体**。b1b 已裁定形态 =「每 Kind 一个扁平 struct」
//   (承 ADR-008 实况),故 `Payload` 字段类型须换成能承载 34 支异构载荷的联合形态。
//   该定型归 b1b;本批(b1a)验收判据只有「编译通过」。

namespace DaYiJingCheng.Sim.Contracts
{
    /// <summary>
    /// 三逻辑流的统一事件。值类型、全整数载荷(ADR-006 §五 编码器落盘)。
    /// 可全序性(ADR-005:265-266):跨流按 <c>(Tick, StreamPriority, Patient, Seq)</c>,
    /// 单流内按 <c>(Tick, Patient, Seq)</c>。
    /// </summary>
    public readonly struct SimEvent
    {
        /// <summary>逻辑 tick(20 Hz ⇒ 50 ms/tick)。全案唯一时间量纲,禁真实墙钟。</summary>
        public readonly long Tick;

        /// <summary>受伤实体身份。世界级事件用 <see cref="PatientId.None"/>(ADR-007 §四)。</summary>
        public readonly PatientId Patient;

        /// <summary>(Tick, Patient) 内由主机 Append 时发放的单调流水号。</summary>
        public readonly long Seq;

        public readonly EventKind Kind;

        /// <summary>⚠️ 占位。定型归 b1b(per-Kind 扁平 struct 族)。</summary>
        public readonly EventPayload Payload;

        public SimEvent(long tick, PatientId patient, long seq, EventKind kind, EventPayload payload)
        {
            Tick = tick; Patient = patient; Seq = seq; Kind = kind; Payload = payload;
        }
    }

    /// <summary>
    /// ⚠️ 占位实现。ADR-024 断言 A2 要求「载荷 ∈ 整数域」;真形态 = 每 Kind 一个扁平 struct(b1b)。
    /// </summary>
    public readonly struct EventPayload
    {
    }
}
