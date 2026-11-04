// 料位选中态的唯一持有者(20 → 18 的接缝 · 2026-11-02)
//
// ─────────────────────────────────────────────────────────────────────────────
// 为什么需要这个类,以及为什么它住装配层
// ─────────────────────────────────────────────────────────────────────────────
// `AC-18-10` 的准入判据要**品级集**(`consumedQualities`)—— 那是「玩家点了哪几味药」。
// 而「点了哪几味」发生在 **20 的容器屏**(玩家在箱里选实例),消费于 **18 的炮制屏**
// (`ProcessingAdmissionQuery` 的 `consumedQualitiesOf` 回调)。
//
// ⚠️ **两屏都不能持这个状态**:
//   · 20 屏若持 ⇒ 关屏后选中态消失,再开炮制屏拿不到;
//   · 18 屏若持 ⇒ 42 持游戏状态,破 ADR-013 §9 C3。
// ⇒ 唯一合法的宿主是**装配层**(它本来就是跨屏时序的裁定处)。
//
// ⚠️ **这不是「游戏状态」**:它不进流、不存档、不参与确定性重放 —— 它是**玩家意图的暂存**
// (承 BL-23 ④「消耗意图直接点名实例集」)。关闭游戏即丢失是**正确**行为
// (与「选中了几味药」这个临时动作同寿命)。
//
// ⚠️ **本类零判据**:只存 id 集 + 提供品级集的**投影**。任何「够不够 / 能不能烧」都不在这里 ——
// 那归 `ProcessingCandidateSet`。在这里写判据 = 第二真源。
//
// ⚠️ **品级从哪来**:`InventoryProjection.TryGet(instanceId)` ⇒ `InventoryRow.Quality`。
// 投影是**派生态**(每次 `Rebuild` 重算)⇒ 本类持 `Func<InventoryProjection>` 回调,
// 每次取品级时重取 —— 持快照会让「玩家丢了一件药」之后仍按旧品级判准入。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>料位选中态(20 屏写入 / 18 炮制屏读取)。</summary>
    /// <remarks>
    /// <para>⚠️ <b>线程 / 时序</b>:两屏都在主线程、都由玩家动作驱动 ⇒ 无需同步原语。</para>
    /// <para>⚠️ <b>选中集是 id 的集合,不是「实例 + 数量」</b> —— BL-23 ④ 的载荷形状是
    /// `[(instance_id, qty)]`,但**qty 归 20 的 `Apply`**(玩家在屏上选的是「哪几栈」,
    /// 用多少份由配方定)。本类只承「哪几栈」这一半,不越权承 qty。</para>
    /// </remarks>
    public sealed class ContainerSelectionStore
    {
        private readonly Func<InventoryProjection> _projection;
        private readonly HashSet<long> _selected = new HashSet<long>();
        private readonly List<long> _sorted = new List<long>();

        /// <summary>构造。</summary>
        /// <param name="projection">**取当前投影**的回调(每次调用重取,不缓存 —— 见类注)。</param>
        /// <exception cref="ArgumentNullException"><paramref name="projection"/> 为 null。</exception>
        public ContainerSelectionStore(Func<InventoryProjection> projection)
        {
            _projection = projection ?? throw new ArgumentNullException(nameof(projection),
                "未注入投影取法 —— 拒绝把品级集当作常量(投影是派生态,每 tick 可变)");
        }

        /// <summary>当前选中的实例 id(**按 id 升序**;空 = 空数组,非 null)。</summary>
        public IReadOnlyList<long> SelectedInstanceIds
        {
            get
            {
                _sorted.Clear();
                _sorted.AddRange(_selected);
                _sorted.Sort();
                return _sorted;
            }
        }

        /// <summary>替换整个选中集(20 屏的 `OnSelectionChanged` 直接转交)。</summary>
        /// <param name="instanceIds">新选中集(<c>null</c> ⇒ 空集)。</param>
        public void Replace(IReadOnlyList<long> instanceIds)
        {
            _selected.Clear();
            if (instanceIds == null) return;
            for (int i = 0; i < instanceIds.Count; i++) _selected.Add(instanceIds[i]);
        }

        /// <summary>清空选中集(关屏 / 换箱时调用)。</summary>
        public void Clear() => _selected.Clear();

        /// <summary>
        /// 当前选中实例的**品级集**(`AC-18-10` 的 `consumedQualities` 形参)。
        /// </summary>
        /// <param name="actorId">玩家 id(**仅用于日志 / 诊断**;选中集本身与玩家无关 —— 见注)。</param>
        /// <returns>品级数组(**空集 = 无选中**,合法输入,不是 null)。</returns>
        /// <remarks>
        /// ⚠️ <b>`actorId` 形参刻意不用</b>:本类的选中集是**单玩家**的(拟物 UI 一次只摊一个箱)。
        /// 保留该形参是为了与 `ProcessingAdmissionQuery` 的 `consumedQualitiesOf(actorId)` 签名对齐
        /// —— 后者按 `actorId` 取,本类不筛。**这条不对称是设计**,已在 `ProcessingAdmissionQuery` 注记。
        /// <para>⚠️ <b>已消失的实例被静默跳过</b>:玩家选了一件药、随后它被消耗 / 掉落 ⇒
        /// 投影里查不到 ⇒ 本方法跳过它。**不抛** —— 那是合法竞态(选中与消耗之间隔了一帧),
        /// 抛会让「正在烧的炉」把整个面板打成崩溃。</para>
        /// </remarks>
        /// <summary>
        /// 取**对齐**的 (实例 id, 品级) 两份清单 —— 供点火事务的
        /// `InputInstanceIds` / `InputQualities`(F1 要求两集合**逐位对位**)。
        /// </summary>
        /// <param name="instanceIds">被点名实例 id(**按 id 升序**;空集 = 无选中,非 null)。</param>
        /// <param name="qualities">与 <paramref name="instanceIds"/> **逐位对位**的品级集。</param>
        /// <remarks>
        /// <para>⚠️ <b>为什么不能拿 <see cref="SelectedInstanceIds"/> + <see cref="ConsumedQualitiesOf"/>
        /// 各取一次</b>:后者会**跳过已消失的实例**(合法竞态),而前者不会 ——
        /// 两次调用的结果可能不等长,`InputInstanceIds[i]` 与 `InputQualities[i]` 从此错位。
        /// 错位的品级集在 `AdmissiblePredicate` 里只表现为「min 变了」,**不抛、不报** ——
        /// 静默放行或静默拒绝。本方法用**一趟遍历**产出两份等长清单,错位在结构上不可表达。</para>
        /// <para>⚠️ 已消失的实例在**两份清单里同时**跳过(不是只从品级侧跳过)。</para>
        /// </remarks>
        public void SnapshotSelection(out long[] instanceIds, out int[] qualities)
        {
            InventoryProjection proj = _projection();

            _sorted.Clear();
            _sorted.AddRange(_selected);
            _sorted.Sort();

            var ids = new List<long>(_sorted.Count);
            var qs = new List<int>(_sorted.Count);
            for (int i = 0; i < _sorted.Count; i++)
            {
                long id = _sorted[i];
                if (proj == null || !proj.TryGet(id, out InventoryRow row)) continue;
                ids.Add(id);
                qs.Add(row.Quality);
            }

            instanceIds = ids.ToArray();
            qualities = qs.ToArray();
        }

        public IReadOnlyList<int> ConsumedQualitiesOf(int actorId)
        {
            if (_selected.Count == 0) return Array.Empty<int>();

            InventoryProjection proj = _projection();
            if (proj == null) return Array.Empty<int>();

            var qualities = new List<int>(_selected.Count);
            foreach (long id in _selected)
            {
                if (!proj.TryGet(id, out InventoryRow row)) continue;   // 已消失 ⇒ 跳过
                qualities.Add(row.Quality);
            }
            return qualities;
        }
    }
}
