// 权威来源:ADR-014 §一(运行期读烘焙产物,零 JSON 解析)· IDataProvider(Sim.Contracts)
//          · Story 008(物品种子表的数据集形状)
//
// ⚠️ 本型住 Sim.Contracts(门 A:BCL only)—— 领域 struct 与取数形状同住契约层,
//    实现方(Gameplay.Presentation 边界程序集)把字节解码成本型后交给 sim。

namespace DaYiJingCheng.Sim.Contracts
{
    /// <summary>物品种子表数据集(烘焙产物 <c>item_database_items.cooked.bytes</c> 的解码形)。
    /// <para><see cref="ConfigVersion"/> = 源数据集内容哈希派生(u32,ADR-014 §五);
    /// 与存档头的比对走 ADR-010 §七(不匹配非致命,只记录排查)。</para>
    /// <para>加载唯一入口 = <see cref="IDataProvider.Load{TDataSet}"/>;
    /// 失败 = 启动期硬失败(E-13),绝不返回默认值。</para></summary>
    public struct ItemDataSet
    {
        /// <summary>全量物品种子(§Schema A 绑定完成、全部门通过后的行集)。</summary>
        public ItemDef[] Items;

        /// <summary>烘焙时从源 JSON 字节派生的内容版本号(u32)。</summary>
        public uint ConfigVersion;
    }
}
