// 权威来源:design/gdd/inventory-and-items.md 规则三 BL-7③ / BL-28(:246-254 P0 范围门)
//          · story-003 P0 范围门第三条(「`build_part` 实例 `quality` 恒 1」)
//          · ADR-006 §Decision 一 D-21-17(category 是数据侧枚举,非 Fix)
//          · ADR-025 §①(纯 sim 逻辑 → `Sim` 装配,引用集 {BCL, Sim.Contracts})
//
// ⚠️ **本条是 P0 范围门的第三条,也是本件唯一可施加的一条**。P0 范围门共三条:
//    ① 容器 qty 恒 1 · ② 容器 quality 恒 1 · ③ **`build_part` quality 恒 1**。
//    ①② 在 P0 **没有判据载体** —— 「这是容器吗」不可判:21a 的 `ItemCategory` 无 `container` 值,
//    `ItemInstance` 无 `is_container` 字段,`assets/data/item_database_items.json` 零容器条目;
//    `StackingSolver.cs:72-76` 已自陈该缺口并把它路由给 Story 010。
//    ⇒ ①② 挂 **BLOCKED-BY Story 010**,本件**不记绿**(禁以 `Children.Length > 0` 顶替 ——
//    那会把「空容器」判成非容器,正是 StackingSolver 登记的同一个洞)。
//
// ⚠️ **③ 为什么可施加**:`build_part` 是 `ItemCategory` 的**已存在取值**(`ItemCategory.cs:43`),
//    判据 = 「实例的 item 属于 build_part 类」。这条与容器无关,不依赖 `is_container`。
//
// ⚠️ **范围债(BL-28)**:建材无品级差异 ⇒ **P1a 材料经济开时须重开**(届时走 BL-28 的三条候选:
//    provenance 进 `StructurePlaced` / 返还固定基准档 / 继续恒 1)。本条是**显式范围债的执法体**,
//    不是永久不变量 —— 解除时改的是**数据 + 本判据的调用点**,不改判定形状。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim
{
    /// <summary>P0 范围门第三条:`build_part` 实例的 <c>quality</c> 恒 1(BL-7③ / BL-28)。
    /// <para><b>纯函数</b>:入参即全部输入,不读运行时对象、不写事件流。</para>
    /// </summary>
    public static class BuildPartQualityGate
    {
        /// <summary>P0 期间 <c>build_part</c> 实例的唯一合法品级(BL-7③ 的裁定值,非数值旋钮 ——
        /// 它是**范围门**,不是平衡参数)。</summary>
        public const int P0BuildPartQuality = 1;

        /// <summary>校验一批实例:P0 期间不得出现 <c>build_part</c> 且 <c>quality != 1</c> 的实例。
        /// <para>返回违例清单(空 = 通过)。<b>不 throw</b> —— 由调用方决定抛 / 聚合
        /// (照 <c>PresentationDtoGuard</c> / <c>ConservationGates</c> 的既有形态)。</para>
        /// <para>⚠️ <b>非 build_part 实例一律放行**(包括任意 quality)—— 本条只管建材那一轴;
        /// 药品 / 原料的品级合法性归 21a 的 <c>MAX_QUALITY</c> 校验(AC-21a-15),不在此重复。</para>
        /// </summary>
        /// <param name="instances">待校验的实例集(可为空 = 无违例)。传入顺序不影响结果。</param>
        /// <param name="defOf">物品键 → 物品条目(非 null;读其 <see cref="ItemDef.Category"/>)。
        /// 查不到 = 静态数据缺陷,须抛(见 <see cref="InstanceResolver"/> 的只增不删口径)。</param>
        /// <returns>违例行清单(空 = 通过)。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="instances"/> 或
        /// <paramref name="defOf"/> 为 null(拒以空集冒充绿)。</exception>
        /// <example><c>build_part</c> 实例 <c>quality = 1</c> ⇒ 空清单;
        /// <c>build_part</c> 实例 <c>quality = 3</c> ⇒ 1 行(点名 instance_id 与品级)。</example>
        public static IReadOnlyList<string> Validate(IReadOnlyList<ItemInstance> instances,
                                                    Func<ItemKey, ItemDef> defOf)
        {
            if (instances == null) throw new ArgumentNullException(nameof(instances));
            if (defOf == null) throw new ArgumentNullException(nameof(defOf));

            var errs = new List<string>();
            for (int i = 0; i < instances.Count; i++)
            {
                ItemInstance inst = instances[i];
                ItemDef def = defOf(inst.Key);
                if (def.Category != ItemCategory.BuildPart) continue;
                if (inst.Quality == P0BuildPartQuality) continue;

                errs.Add(
                    $"[P0 范围门·BL-7③] build_part 实例 {inst.InstanceId}({inst.Key})的 quality = " +
                    $"{inst.Quality} ≠ {P0BuildPartQuality} —— 建材品级轴 P0 恒 1(BL-28 范围债:" +
                    "拆除返还的品级来源全案无定义 —— `StructureRemoved` 载荷无 quality 字段," +
                    "取最高档 = 免费升档洗练,取基准 = 隐性材料汇;P1a 材料经济开时重开)");
            }
            return errs;
        }

        /// <summary>便利入口:违例即抛(消息含**全部**违例,便于一次修完)。</summary>
        /// <param name="instances">待校验的实例集。</param>
        /// <param name="defOf">物品键 → 物品条目(非 null)。</param>
        /// <exception cref="InvalidOperationException">存在任一违例。</exception>
        /// <exception cref="ArgumentNullException">入参为 null。</exception>
        public static void RequireValid(IReadOnlyList<ItemInstance> instances, Func<ItemKey, ItemDef> defOf)
        {
            IReadOnlyList<string> errs = Validate(instances, defOf);
            if (errs.Count == 0) return;
            throw new InvalidOperationException(
                "P0 范围门(BL-7③)未通过:" + errs.Count + " 处违例。逐条:\n  " +
                string.Join("\n  ", errs));
        }
    }
}
