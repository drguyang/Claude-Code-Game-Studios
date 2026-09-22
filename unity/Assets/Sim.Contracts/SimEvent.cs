// 权威来源:ADR-005 §Key Interfaces(:252-259)· ADR-006 Amendment A(:246 起)
//          · ADR-024 §①(Kind 清单的真源 = design/registry/entities.yaml,共 34 支)
//          · **b1b 支 0 裁定(2026-09-22 批)**:载荷联合形态 = 甲「header/blob 分家」
//
// ⚠️ 与 ADR-005:257 的**字面**有一处必要偏离,登记如下(b1a 原文保留):
//   ADR-005 写 `public readonly EventKind Kind;`(字段名 Kind,类型名 EventKind);
//   ADR-024 的生成器语法写 `SimEvent.Kind.*`(Kind 是嵌套类型)。
//   **两者在 C# 中不可能同时成立** —— 同一 struct 内「字段 Kind」与「嵌套类型 Kind」同名 = CS0102。
//   落地取 **类型住命名空间作用域、名为 `EventKind`**,理由:
//   ① ADR-005 的字段声明自此**逐字可编译**(它是被五份 ADR 引用的形状);
//   ② ADR-024 的 A5 双向差集比的是**末段成员名**(`.InjuryOnset` 等),不比类型名前缀,
//      故真源纪律不受影响;kindgen 侧的对应口径 = b6 已实现(StreamRouting.g.cs 用 `EventKind.X`)。
//
// **b1b 改写记录(原 :14-16 的「EventPayload 占位待定型」就此兑现)**:
//   `Payload` 字段类型 = `PayloadRef`(全整数 header,见 Payloads/PayloadCommon.cs)。
//   per-Kind 强类型载荷 = Payloads/ 下 34 支 struct,由 Sim.Codec 按 registry schema 解码。
//   本形状与 ADR-006 Amendment A「Payload 是值 struct 无引用字段」的读法变更,
//   归 **X-2 修正案回写 ADR-006**(卡 u0-b-b1b §支 5,Amendment G)。

namespace DaYiJingCheng.Sim.Contracts
{
    /// <summary>
    /// 三逻辑流的统一事件**头部**。值类型、全整数(载荷经 <see cref="PayloadRef"/> 寻址,
    /// 落盘形状 = ADR-010 全二进制 codec)。可全序性(ADR-005:265-266):跨流按
    /// <c>(Tick, StreamPriority, Patient, Seq)</c>,单流内按 <c>(Tick, Patient, Seq)</c>。
    /// 字段序 = ADR-006 Amendment A 唯一权威抄本(ADR-007 旧抄本已由其 :92-93 就地作废)。
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

        /// <summary>载荷引用(header/blob 分家,支 0 甲)。解码归 Sim.Codec。</summary>
        public readonly PayloadRef Payload;

        public SimEvent(long tick, PatientId patient, long seq, EventKind kind, PayloadRef payload)
        {
            Tick = tick; Patient = patient; Seq = seq; Kind = kind; Payload = payload;
        }
    }
}
