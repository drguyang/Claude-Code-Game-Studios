// 权威来源:ADR-008 §Key Interfaces(:288)· ADR-009 §三(第三条逻辑流)· ADR-045(第四条:任务流)
//
// ⚠️ 成员序即 StreamPriority 序:History(0) < Case(1) < World(2) < Quest(3)。
// 跨流全序键 (Tick, StreamPriority, Patient, Seq) 依赖这一序(ADR-006 Amendment C / ADR-008 §二)。
// 改成员顺序 = 改全序语义,须另裁 ADR。

namespace DaYiJingCheng.Sim.Contracts
{
    /// <summary>四条逻辑流。成员序 = StreamPriority 升序(病史 < 病例 < 世界 < 任务)。</summary>
    public enum StreamId
    {
        History = 0,   // 病史流(9 的主场;可随终态折叠 —— ADR-010)
        Case    = 1,   // 病例流(37;不物理折叠 —— ADR-008 §六)
        World   = 2,   // 世界流(6 / 23 / 52 …;不折叠 + 有界性论证 —— ADR-009 §六)
        // 🔁 2026-11-14(ADR-045 §D1):**追加在末尾**,既有三值零位移。
        //    优先级 = 3 ⇒ 同 tick 跨流全序里排在**最后** —— 奇遇的完成是对世界动作的
        //    **解读**,必须晚于产生它的那些事件。**不折叠**(同 Case / World)。
        Quest   = 3,   // 任务流(奇遇 Accepted / Completed;不折叠 —— ADR-045)
    }
}
