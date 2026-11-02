// 20 容器屏的只读数据面(`inventory-items/story-006` 的内容面 · 2026-11-02)
//
// 权威来源:
//   design/ux/inventory-container-20.md
//     · §5.2 摆放函数 —— `instance_id` 升序、`COLUMNS = 4` / `ROWS_PER_PAGE = 3`(一屏 ≤12)
//     · §5.2 换页条件 = `row ≥ ROWS_PER_PAGE`
//     · §6 载重四档 = `slack = cap − carry_load` 的单调不增函数(阈值归 42)
//     · §5.3 器物「**是**(焦点 + 行级动作)」—— 行必须可交互
//     · §11 品级通道 = **质地 + 药签措辞**(禁颜色单通道)
//   design/gdd/inventory-and-items.md
//     · 规则十 翻页制 · AC-20-20(同一箱 + 同一方向键 ⇒ 同一焦点目标)
//     · AC-20-10(禁负重条 / 百分比 / 数字)
//   ADR-013 §9 C3 —— 42 **只渲染,永不持有游戏状态**
//   ADR-025 §① —— 住 `Sim.Contracts`(`Gameplay.UI` 的引用集恰 = {Sim.Contracts, Gameplay.Presentation})
//
// ─────────────────────────────────────────────────────────────────────────────
// 为什么 DTO 在 `Sim.Contracts` 而不是 `Gameplay.UI`
// ─────────────────────────────────────────────────────────────────────────────
// 与 `IProcessingAdmissionQuery` 同因:`Gameplay.UI` **看不见 `Sim`**,而投影
// (`InventoryProjection`)住 `Sim`。⇒ 42 只能经 `Sim.Contracts` 的 DTO 读数据,
// 由装配层把 `InventoryProjection` 投影成这些 DTO。**这是既有 42 屏的同一形状**
// (脉案走 `ICasebookDtoSource`,音频走 `AudioCueDto`)。
//
// ⚠️ **本 DTO 不含 `Owner`** —— 容器屏只显示**当前箱主**名下的器物,归属已由装配层筛过。
//    把 `Owner` 放进来会让 42 有机会「自己再筛一次」= 第二判据。
//
// ⚠️ **本 DTO 不含任何载重数字** —— AC-20-10 明令禁数字/百分比/进度条。
//    载重只以**四档枚举**出现(§6),分档函数归 42(阈值 = `design/ux/inventory-container-20.md:98`)。

namespace DaYiJingCheng.Sim.Contracts
{
    /// <summary>载重四档(§6:`slack = cap − carry_load` 的单调不增分档)。</summary>
    /// <remarks>
    /// ⚠️ **满档的判据来自 20**(<see cref="InventoryContainerDto.IsFull"/>,BL-27:「20 出谓词」);
    /// **余下三档由 42 分**(「42 出分档」—— 中间两档的切点是布局参数,不是机制值)。
    /// 本枚举只是**结果形状** —— 装配层不预先分档(那会把分档函数搬进装配层 = 第二处阈值),
    /// 42 也不自己求满档谓词(那会与 `CarryLoadSolver` 形成第二份判据 —— F8)。
    /// </remarks>
    public enum CarryBand : int
    {
        /// <summary>空(箱内无器物)。</summary>
        Empty = 0,

        /// <summary>半满。</summary>
        Half = 1,

        /// <summary>近满。</summary>
        NearFull = 2,

        /// <summary>满(盖合到一半被顶住 —— §5.2 ③ 的物理承载)。</summary>
        Full = 3,
    }

    /// <summary>箱内一件器物的**只读呈现行**(§5.3「器物实物」)。</summary>
    /// <remarks>
    /// <para>⚠️ <b>字段刻意最小</b>:`instance_id`(落位序 + 选中身份)· 物品序号(素材档)·
    /// 品级(质地/药签通道)· 数量(堆叠角)。**不含** `ItemKey` 字符串 —— 那要 21a 的
    /// 显示名,而归显示名轮(与 `Recipe` 无 `DisplayName` 同款缺口)。</para>
    /// <para>⚠️ <b>品级禁单靠颜色</b>(§5.3 记号纪律 + AC-20-23):`Quality` 是**数值**,
    /// 42 须以质地 / 药签措辞承载,不得只染个颜色。</para>
    /// </remarks>
    public readonly struct ContainerItemDto
    {
        /// <summary>实例 id(落位序 = 该字段升序,§5.2;也是选中的身份键)。</summary>
        public readonly long InstanceId;

        /// <summary>物品序号(21a 表;素材档的键)。</summary>
        public readonly int ItemOrdinal;

        /// <summary>品级(1..MAX_QUALITY;呈现走质地/药签,不走颜色单通道)。</summary>
        public readonly int Quality;

        /// <summary>堆叠数量(≥ 1;> 1 时 42 画叠角记号)。</summary>
        public readonly int Qty;

        /// <summary>构造。</summary>
        public ContainerItemDto(long instanceId, int itemOrdinal, int quality, int qty)
        {
            InstanceId = instanceId;
            ItemOrdinal = itemOrdinal;
            Quality = quality;
            Qty = qty;
        }
    }

    /// <summary>一个容器(箱)的只读快照 —— 42 每次开屏 / 换页重读,**不缓存**。</summary>
    /// <remarks>
    /// <para>⚠️ <b>`Items` 必须已按 `instance_id` 升序</b>(§5.2 的落位函数以该序为唯一权威)。
    /// 装配层排序,42 **不再排** —— 两处排序 = 第二真源(且「42 持状态」的边界会被踩)。</para>
    /// <para>⚠️ <b>翻页在 42 内做</b>(`COLUMNS` / `ROWS_PER_PAGE` 是布局参数,§5.2 明写归 ux-design 域)。
    /// 故本 DTO 给**整箱**器物,不给单页。</para>
    /// </remarks>
    public readonly struct InventoryContainerDto
    {
        /// <summary>箱的稳定 id(容器 = 物品实例,GDD 规则二 ⇒ 这里是 `instance_id`)。</summary>
        public readonly long ContainerInstanceId;

        /// <summary>箱内器物(**已按 `instance_id` 升序**;空箱 = 空数组,非 null)。</summary>
        public readonly ContainerItemDto[] Items;

        /// <summary>当前载重(**只供 42 分档用,禁直接显示数字** —— AC-20-10)。
        /// <para>⚠️ <b>类型 = `long`,与 20 的 `CarryRatioDto.CarryLoad` 逐字一致</b>。
        /// F-20.1 的求和结果定义域是 **int64**(GDD `:449` 的溢出界改判);收窄到 int32 会让
        /// 「载重超 int32」这一状态**静默回绕成一个小正数** —— 本项目已抓过五次的静默失败形态,
        /// 省 4 字节不值。</para></summary>
        public readonly long CarryLoad;

        /// <summary>载重上限(**只供 42 分档用**;唯一来源 = 20 的 `CARRY_CAP`)。</summary>
        public readonly int CarryCap;

        /// <summary>
        /// **满档谓词的结果**(20 的 `CarryLoadSolver.IsFullForAnyAddition`,即
        /// `carry_load &gt; cap − W_MIN`;AC-20-18)。
        /// </summary>
        /// <remarks>
        /// <para>⚠️ <b>为什么结果由装配层算、而不让 42 自己算</b>:BL-27 的分工逐字是
        /// 「**20 出谓词、42 出分档**」。谓词的本体住 `Sim`(`CarryLoadSolver`),而 `Gameplay.UI`
        /// **看不见 `Sim`**(ADR-025 §①)⇒ 42 若要自己求值,就必须**再写一遍**
        /// `load &gt; cap − W_MIN` —— 那正是 F8「行为等价掩盖结构分叉」的典型形态:
        /// 两份判据今天碰巧同值,任一侧改口径(例如将来 `W_MIN` 语义变化)就静默分叉。</para>
        /// <para>⇒ 谓词在 20 侧求值一次,42 只做**余下三档的分档**(slack 阈值 = 布局参数域)。
        /// 装配层的求值点 = `BootRoot.BuildContainerDto`,调用的是 `CarryLoadSolver` 本体,
        /// 不是复制品。</para>
        /// <para>⚠️ <b>`false` 有两种来源,须分辨</b>:① 确实未满;② 容量真值未装配
        /// (`CompositionRootServices.CarryCap == null`,夹具假 provider)。两种都不画满档,
        /// 但 ② 时 `CarryCap` 会同时为 `0` —— 42 可据此判定「未知」而非「空」。</para>
        /// </remarks>
        public readonly bool IsFull;

        /// <summary>构造。</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="items"/> 为 null。</exception>
        public InventoryContainerDto(long containerInstanceId, ContainerItemDto[] items,
                                     long carryLoad, int carryCap, bool isFull)
        {
            if (items == null)
                throw new System.ArgumentNullException(nameof(items),
                    "器物数组为 null —— 拒绝把 null 当成空箱(那会让「没数据」与「空箱」不可区分)");
            ContainerInstanceId = containerInstanceId;
            Items = items;
            CarryLoad = carryLoad;
            CarryCap = carryCap;
            IsFull = isFull;
        }
    }
}
