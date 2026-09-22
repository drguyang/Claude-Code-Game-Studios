// 权威来源:ADR-009 §Key Interfaces(:464)
//
// 掉落 / 库存 / 建造件共用的实例身份。发放机制 = 计数器 + 高水位可重构(ADR-010 §五 · ADR-009 §三)。

namespace DaYiJingCheng.Sim.Contracts
{
    /// <summary>物品实例 id(掉落 / 库存 / 建造件共用)。落世界流,不落病史流。</summary>
    public readonly struct ItemInstanceId
    {
        public readonly long Value;

        public ItemInstanceId(long value) { Value = value; }
    }
}
