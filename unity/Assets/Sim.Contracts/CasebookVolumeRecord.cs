// ADR-010 §三 义务 13 —— **分册态段**(存档 v7)的域类型。
//
// 权威来源:
//   docs/architecture/adr-010-persistence-save-format.md §一(布局新增「分册态段」槽)·
//     §三 义务 13(「分册键 `player_id` · 两个 tick · 分册的纯呈现量(页码 / 折叠状态);
//     **痕与排序不在其中**(读时计算)」)
//   design/gdd/casebook.md 规则一(「三个时刻中的两个(快照 tick / 上次重查 tick)」归 39)·
//     规则二(每玩家一本,分册键 = `player_id`)· **AC-39-13**(痕 = 病例流读时投影,
//     不在任何分册态存储中) · F-39.2
//   production/epics/casebook/story-001-volume-state-contract-and-save-segment.md
//
// ── 2026-11-08 取件轮的两处口径裁定(用户)────────────────────────────────
// ① **不落「已落笔判断」的内容**(甲案)。卡面 AC-1 原写「已落笔判断的呈现缓存(分册正文)」,
//    与 ADR-010 义务 13 的「痕与排序不在其中」+ AC-39-13 的反射断言**直接冲突**:
//    把判断内容抄进存档 = 病例流的第二真源,重放与补算会分叉。
//    ⇒ 本段的语义面**只有四类**:分册键 · 两个 tick · 页码 · 折叠状态。
// ② **段位置 = 头部 + 配置版本段之后、事件段之前**(同 ADR-037 §① 的理由):
//    尾部追加会破 `SaveMigrationChain.TailBody`(它按末尾等长比对正文),
//    那样 v6→v7 迁移**结构上不可实现**。
//
// ── 为什么住 `Sim.Contracts` ──────────────────────────────────────────
// 写/读两端都在 `Sim.Codec`(存档段),而 39 的呈现侧在 `Gameplay.UI` ——
// 两者的唯一公共引用集是本装配(ADR-025 §①)。呈现半的 DTO **不在这里**:
// 它由 `Gameplay.UI` 自己映射,存档里只走整数域(ADR-006 §五)。
//
// ⚠️ **本文件里不许出现 `float` / `double`**,也不许出现引用类型字段 ——
//   存档字段的整数纪律由 ADR-006 §五 + `casebook_segment_codec_test` 的反射判据共守。

using System;
using System.Collections.Generic;

namespace DaYiJingCheng.Sim.Contracts
{
    /// <summary>
    /// 一本分册的**册级**纯呈现量(ADR-010 义务 13 的「页码」)。
    /// <para>每玩家一条;`PlayerId` 是分册键(规则二),由 `IIdAuthority` 发号,<b>不是</b>槽号。</para>
    /// </summary>
    public readonly struct CasebookVolumeRecord
    {
        /// <summary>分册键 = `player_id`(ADR-006 Amendment B 机制 A 发号;P0 单机亦走发号,非常量 0)。</summary>
        public readonly int PlayerId;

        /// <summary>册内当前页码(纯呈现量;语义归 39,数值归用户数值轮)。</summary>
        public readonly int PageIndex;

        /// <summary>构造。</summary>
        /// <exception cref="ArgumentOutOfRangeException">页码为负(负页码不是合法呈现态)。</exception>
        public CasebookVolumeRecord(int playerId, int pageIndex)
        {
            if (pageIndex < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(pageIndex), pageIndex, "页码不得为负 —— 负页码是无意义呈现态,写进去只会掩盖写侧错误");
            PlayerId = playerId;
            PageIndex = pageIndex;
        }
    }

    /// <summary>
    /// 分册中的**一页**(= 一个病人)的纯呈现态:两个 tick + 折叠状态。
    /// <para>⚠️ <b>这里没有任何「判断内容」</b> —— 已落笔的病名 / 置信度 / 痕都由
    /// `casebook.md` F-39.2 从病例流<b>读时投影</b>,不进存档(AC-39-13)。</para>
    /// </summary>
    public readonly struct CasebookPageRecord
    {
        /// <summary>所属分册键。</summary>
        public readonly int PlayerId;

        /// <summary>该页对应的病人 id。</summary>
        public readonly int PatientId;

        /// <summary>上次快照 tick(`diagnosis-system` 的「旧」态判据输入之一)。</summary>
        public readonly long SnapshotTick;

        /// <summary>上次重查 tick。</summary>
        public readonly long RecheckTick;

        /// <summary>折叠状态:<c>0</c> = 摊开,<c>1</c> = 遮住判断区(二进制,不是枚举表 —— 只有两态)。</summary>
        public readonly int Folded;

        /// <summary>构造。</summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="folded"/> 不是 0 / 1。</exception>
        public CasebookPageRecord(int playerId, int patientId, long snapshotTick, long recheckTick, int folded)
        {
            if (folded != 0 && folded != 1)
                throw new ArgumentOutOfRangeException(
                    nameof(folded), folded, "折叠状态只有两态(0 摊开 / 1 折叠)—— 第三态是写侧错误,不静默归一");
            PlayerId = playerId;
            PatientId = patientId;
            SnapshotTick = snapshotTick;
            RecheckTick = recheckTick;
            Folded = folded;
        }

        /// <summary>折叠态的可读投影(<c>Folded != 0</c>)。</summary>
        public bool IsFolded => Folded != 0;
    }

    /// <summary>
    /// 一整段分册态的只读快照 —— 编解码与装配层的**唯一交换形状**。
    /// <para>两类记录分开存:册级(每玩家一条)/ 页级(每玩家 × 每病人一条)。
    /// 这是 ADR-010 义务 13 的逐字分类,不是实现口味 —— 把页码塞进页级记录会让
    /// 「同一玩家 N 个病人 N 份页码」,写侧稍有分歧就出现两份不同页码。</para>
    /// </summary>
    public sealed class CasebookVolumeSnapshot
    {
        /// <summary>册级记录(<c>null</c> 视同空集)。</summary>
        public IReadOnlyList<CasebookVolumeRecord> Volumes { get; }

        /// <summary>页级记录(<c>null</c> 视同空集)。</summary>
        public IReadOnlyList<CasebookPageRecord> Pages { get; }

        /// <summary>构造。</summary>
        public CasebookVolumeSnapshot(
            IReadOnlyList<CasebookVolumeRecord> volumes,
            IReadOnlyList<CasebookPageRecord> pages)
        {
            Volumes = volumes ?? Array.Empty<CasebookVolumeRecord>();
            Pages = pages ?? Array.Empty<CasebookPageRecord>();
        }

        /// <summary>空快照(<c>count = 0</c> 的合法语义:新世界尚无分册态)。</summary>
        public static CasebookVolumeSnapshot Empty { get; } =
            new CasebookVolumeSnapshot(null, null);

        /// <summary>两类记录都为空。</summary>
        public bool IsEmpty => Volumes.Count == 0 && Pages.Count == 0;
    }
}
