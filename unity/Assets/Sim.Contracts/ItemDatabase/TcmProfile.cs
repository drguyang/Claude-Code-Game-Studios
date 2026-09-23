// 权威来源:design/gdd/item-database.md §Schema D(tcm_profile —— P0 恒空占位)

namespace DaYiJingCheng.Sim.Contracts
{
    /// <summary><c>tcm_profile</c> 占位 —— **P0 恒空**(P1a 才填;字段表由 P1a 撰写时补,
    /// §Schema D 明文「本节此处仅占位」)。空 struct 合法;P0 期出现非空本块的拒收
    /// = AC-21a-23(归 Story 006)。挂点见 <see cref="ItemDef.TcmProfile"/>。</summary>
    public readonly struct TcmProfile
    {
        // P0 占位:不发明任何字段(「本节之外出现的字段一律视为未定义」)。
    }
}
