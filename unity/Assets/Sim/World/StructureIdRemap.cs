// ADR-033 §①/②/③/⑤ —— 结构 id 撞号的**载入期归一化**(纯函数,零 codec 依赖)。
//
// 权威来源:docs/architecture/adr-033-structure-id-collision-on-load.md
//
// 落点 = **重放入口**(`WorldLoadService.Rebuild()` 的解码之后、`RebuildFromDecoded` 之前)。
// ⚠️ **不得**做成 `ISaveMigration`:迁移链的 `TailBody` 按**末尾等长**取段比对
//    (`SaveMigrationChain.cs:174-184`)⇒ 任何「改正文」的迁移步都会直接抛(ADR-033 §⑤)。
//    本件**不是迁移**,不经过那条链。
//
// ⚠️ 住 `Sim`:只吃 `StructureRestoreOp` 序列(C6 —— 零 codec 依赖),不认 `SimEvent` / `PayloadRef`。

using System;
using System.Collections.Generic;

namespace DaYiJingCheng.Sim.World
{
    /// <summary>
    /// 基线诞生**之前**写下的存档,其 <c>Placed</c> 的 <c>structure_id</c> 从 1 起号 ——
    /// 与关卡基线的初始家具**撞号**。本类给出 ADR-033 的裁定:**两边都留** ——
    /// 基线恒占 <c>1..N</c>,存档结构整体**平移** <c>+N</c>(<c>K := N</c>)。
    /// </summary>
    /// <remarks>
    /// <para><b>判据是充要的,不是启发式</b>(ADR-033 §②):后基线存档的 <c>Placed</c> 必取
    /// <c>≥ N+1</c>(发号器由基线推高)⇒ <c>Placed</c> 的 id ≤ N **当且仅当**它写在基线诞生之前。
    /// ⚠️ **只扫 <c>Placed</c>** —— 放宽成「任一操作 id ∈ [1,N]」会把**每一份正常的后基线存档**
    /// (玩家拆过任何一件初始家具)误判为前基线存档并整体平移,玩家建造的 id 会无故跳号。</para>
    /// <para><b>平移量只依赖基线件数</b>(ADR-033 §③):不依赖存档内容 ⇒ 同一份
    /// <c>Placed(1..M)</c> **每次载入都得** <c>N+1..N+M</c>,不随存档增长而暴涨。</para>
    /// <para>本类**不做固化** —— 把结果写回事件流是调用方的义务(ADR-033 §④之补:
    /// 必须**同一次载入内**完成,「下次自然固化」不够,且不固化的存档是**混合态**)。</para>
    /// </remarks>
    public static class StructureIdRemap
    {
        /// <summary>
        /// 撞号判据(ADR-033 §②):<c>∃ op.Kind = Placed ∧ 1 ≤ op.StructureId ≤ baselineCount</c>。
        /// </summary>
        /// <param name="ops">已解码的重放操作序列(按事件全序)。</param>
        /// <param name="baselineCount">基线件数 <c>N</c>(<c>World.BakedInitialStructureCount</c>)。</param>
        /// <returns>命中 ⇒ <c>true</c>(须平移)。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="ops"/> 为 null。</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="baselineCount"/> 为负。</exception>
        public static bool Collides(IReadOnlyList<StructureRestoreOp> ops, int baselineCount)
        {
            if (ops == null) throw new ArgumentNullException(nameof(ops));
            if (baselineCount < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(baselineCount), baselineCount,
                    "基线件数不可能为负 —— 负值会把「基线为空」与「调用方算错」混为一谈");

            if (baselineCount == 0) return false;   // 空基线 ⇒ [1,0] 是空区间 ⇒ 无从撞号

            for (int i = 0; i < ops.Count; i++)
            {
                StructureRestoreOp op = ops[i];
                if (op.Kind == StructureRestoreKind.Placed &&
                    op.StructureId >= 1 && op.StructureId <= baselineCount)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 把序列里**全部**操作的 <c>StructureId</c> 平移 <c>+baselineCount</c>,其余字段原样。
        /// </summary>
        /// <remarks>
        /// <para><b>为什么 <c>Removed</c> / <c>Modified</c> 也要平移</b>:它们引用的号必须与
        /// <c>Placed</c> 指向同一条件,否则平移后它们指的就不是原来那件结构了(ADR-033 §③)。</para>
        /// <para><b>幂等的前提是固化</b>:平移后写出的存档其 <c>Placed</c> 全部 <c>&gt; N</c>
        /// ⇒ 判据不再触发 ⇒ 二次载入是恒等变换。**不做固化则会逐轮再涨**(ADR-033 §④)。</para>
        /// </remarks>
        /// <param name="ops">已解码的重放操作序列(按事件全序)。</param>
        /// <param name="baselineCount">平移量 <c>K := N</c>。</param>
        /// <exception cref="ArgumentNullException"><paramref name="ops"/> 为 null。</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="baselineCount"/> 为负。</exception>
        /// <exception cref="OverflowException">
        /// <c>id + N</c> 溢出 <see cref="long"/>。现实中不可达,但**不设守卫会让「不可达」变成假设**
        /// (ADR-033 §③)⇒ 用 <c>checked</c> 判,一旦发生即抛。
        /// </exception>
        public static IReadOnlyList<StructureRestoreOp> Apply(
            IReadOnlyList<StructureRestoreOp> ops, int baselineCount)
        {
            if (ops == null) throw new ArgumentNullException(nameof(ops));
            if (baselineCount < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(baselineCount), baselineCount, "平移量不可能为负");

            var shifted = new StructureRestoreOp[ops.Count];
            for (int i = 0; i < ops.Count; i++)
            {
                StructureRestoreOp op = ops[i];
                long newId = checked(op.StructureId + baselineCount);

                switch (op.Kind)
                {
                    case StructureRestoreKind.Placed:
                        shifted[i] = StructureRestoreOp.Placed(
                            newId, op.Anchor, op.ModuleId, op.Orientation, op.Variant);
                        break;
                    case StructureRestoreKind.Removed:
                        shifted[i] = StructureRestoreOp.Removed(
                            newId, op.Anchor, op.ModuleId);
                        break;
                    case StructureRestoreKind.Modified:
                        shifted[i] = StructureRestoreOp.Modified(
                            newId, op.Orientation, op.Variant);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(
                            nameof(ops), op.Kind,
                            $"未知的 StructureRestoreKind={(int)op.Kind} —— 新增支须同批补本映射");
                }
            }
            return shifted;
        }
    }
}
