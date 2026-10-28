// 存档体布局的**共享常量**(ADR-034 §Decision ③)。
//
// 权威来源:docs/architecture/adr-034-save-v2-payload-segment.md
//
// 为什么这些常量住在 Sim.Codec 而不是 Gameplay.Boot:
//   v2 起 `SaveService.ParseBody` 必须**按 count 精确**读出折叠段(否则载荷段无处可放)。
//   而 `SaveService` 看不见 `FoldRow`(它住 Sim,兄弟装配)—— 那正是 TD-persist-03 的
//   设计:折叠段以**原始字节**交出,解码归装配根。
//   ⇒ 行宽这个**纯布局量**必须落在一个两侧都看得见的装配里:Sim.Codec。
//   `Gameplay.Boot/FoldRowCodec.RowSize` 现为**别名**(单一源在此),不再各写一个 36。

namespace DaYiJingCheng.Sim.Codec
{
    /// <summary>存档体布局常量(ADR-010 §一 / ADR-034 §Decision ③)。</summary>
    public static class SaveLayout
    {
        /// <summary>
        /// 折叠行定长字节数 —— <c>FoldRow</c> 六字段逐字序(ADR-010 §二)。
        /// <para>⚠️ <b>唯一源</b>:<c>FoldRowCodec.RowSize</c> 别名到此。改动须同步
        /// `FoldRowCodec` 的编码布局与黄金字节夹具。</para>
        /// </summary>
        public const int FoldRowSize = 36;

        /// <summary>载荷段每条目的定长头 = <c>blobId:i32</c> + <c>length:i32</c>。</summary>
        public const int PayloadEntryHeaderSize = 8;
    }
}
