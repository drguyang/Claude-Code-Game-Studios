// 任务流(第四条逻辑流)载荷 —— ADR-045 §D2。
//
// 权威:
//   docs/architecture/adr-045-quest-stream.md  §D1(StreamId.Quest)· §D2(两个 Kind 与载荷)
//   · §D5(唯一性:同 (actor, adventure) 至多一条)· §D6(组件经 DropSpawned 落脚下)
//   design/levels/prologue-beat-script.md §3.1(PA-01…PA-05)
//
// ⚠️ 与病史 / 病例 / 世界三族载荷同口径:全整数域、零 Fix、零引用类型(ADR-024 A2);
//    **无 tick / 无 seq**(它们是事件头 `SimEvent.Tick` / `Seq` 的量,入载荷 = 第二真源);
//    **无 patient_id**(奇遇是玩家级事实 ⇒ `Patient = PatientId.None`,与 ADR-007 五支同格)。
//
// ⚠️ `AdventureCompletedPayload` 自带 `item_key` / `qty` 是**刻意的**:完成事件必须自带
//    「给了什么」。只记 adventure_id 会让「给了几件」变成读作者态表推导 —— 而表会改,
//    那是一条静默的第二真源。

namespace DaYiJingCheng.Sim.Contracts
{
    /// <summary>奇遇接取(`AdventureAccepted`)。
    /// <para>只证明「玩家进入过该奇遇的计数范围」—— **不承载任何进度数值**;
    /// 进度是 `ResourceHarvested` / 击败事实的派生量(ADR-045 §D4)。</para></summary>
    public readonly struct AdventureAcceptedPayload
    {
        /// <summary>奇遇 id(`adventure_steps.json` 的行序)。</summary>
        public readonly int AdventureId;

        /// <summary>玩家 id(ADR-006 Amendment B 的机制 A id 空间)。</summary>
        public readonly int ActorId;

        /// <summary>构造。</summary>
        public AdventureAcceptedPayload(int adventureId, int actorId)
        {
            AdventureId = adventureId;
            ActorId = actorId;
        }
    }

    /// <summary>奇遇完成(`AdventureCompleted`)。
    /// <para>同 `(actor, adventure)` 至多一条(ADR-045 §D5)—— 这是「组件只给一次」的机制保证。</para></summary>
    public readonly struct AdventureCompletedPayload
    {
        /// <summary>奇遇 id(`adventure_steps.json` 的行序)。</summary>
        public readonly int AdventureId;

        /// <summary>玩家 id。</summary>
        public readonly int ActorId;

        /// <summary>授予的组件(21a 物品表 ordinal)。</summary>
        public readonly int ItemKey;

        /// <summary>份数(≥ 1)。</summary>
        public readonly int Qty;

        /// <summary>构造。</summary>
        public AdventureCompletedPayload(int adventureId, int actorId, int itemKey, int qty)
        {
            AdventureId = adventureId;
            ActorId = actorId;
            ItemKey = itemKey;
            Qty = qty;
        }
    }
}
