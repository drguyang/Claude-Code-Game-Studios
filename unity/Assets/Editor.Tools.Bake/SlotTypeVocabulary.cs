// 槽位类型词表 —— **由 `Sim.World.SlotType` 派生**(唯一真源)。
//
// 立此件缘由(2026-10-28 代码评审 **A-21 · BLOCK**):
//   全仓曾有 **四份** 手写名字表 —— `Editor.Tools.Level` 的枚举镜像 + `LevelExporter.SlotTypeNames`
//   + `WorldBuildSlotsBinder.SlotTypeNames` + `BuildModulesBinder.SlotTypeNames`。
//   两份 Binder 都用「名字表下标 + 1」自产**序数**,而运行期
//   `Sim.World.BuildSlotCatalog` 的 `BitMask.Single(type) => new BitMask(1 << (int)type)`
//   用的是**枚举值**。两侧唯一的联系是「`SlotType` 的数值恰好 = 下标 + 1」——
//   **一个从未被任何门断言过的巧合**。Sim 侧在枚举中间插一个类型 ⇒ 四份表照旧编译、
//   五条域门全绿、掩码与类型位**整体错位** ⇒ 「Plot 槽位接受 Decor 模块」的静默错配。
//
// 现口径:**名字与序数都从 CLR 枚举取**,手写表归零。漂移在**编译期**即不可能
// (枚举改了 ⇒ 这里自动跟着改;改坏了 ⇒ 下面的位域断言在类型初始化时炸)。
//
// 权威来源:ADR-014 §四(作者态写**字符串名**,不写 CLR 枚举序数 —— 那条仍成立:
//   本条只改「**哪来的名字**」,不改「JSON 里写的是名字」这件事)·
//   ADR-022 §③(导出契约)· ADR-015 §三(整数格 / 位域)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.World;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>槽位类型名字 ↔ 枚举序数的**唯一**词表(从 <see cref="SlotType"/> 派生)。</summary>
    internal static class SlotTypeVocabulary
    {
        /// <summary>合法名字(不含 `None`;顺序 = 枚举值升序)。</summary>
        internal static readonly string[] Names;

        /// <summary>名字 → 枚举序数(`int` 值,不是下标)。</summary>
        private static readonly Dictionary<string, int> OrdinalByName;

        static SlotTypeVocabulary()
        {
            Array values = Enum.GetValues(typeof(SlotType));
            var names = new List<string>(values.Length);
            var map = new Dictionary<string, int>(values.Length, StringComparer.Ordinal);

            foreach (SlotType t in values)
            {
                int v = (int)t;
                if (t == SlotType.None) continue;

                // ⚠️ 位域守卫:`BuildSlot.AllowedMask` 是 int ⇒ 可用位 [1,30]。
                //    超界**在类型初始化时**炸(编辑期工具 ⇒ 烘焙开工即失败,不是静默截断)。
                if (v < 1 || v > 30)
                    throw new InvalidOperationException(
                        $"[槽位词表] SlotType.{t} = {v} 超出位掩码可用范围 [1,30] —— " +
                        "`BuildSlot.AllowedMask` 是 int,超界位不可表示;" +
                        "请改枚举值或改掩码宽度(二者须同时裁,不得静默截断)。");

                string name = t.ToString();
                if (map.ContainsKey(name))
                    throw new InvalidOperationException(
                        $"[槽位词表] SlotType 出现重名 \"{name}\" —— 词表会静默丢弃一条");

                names.Add(name);
                map.Add(name, v);
            }

            Names = names.ToArray();
            OrdinalByName = map;
        }

        /// <summary>名字 → 枚举序数(严格:大小写敏感,`None` 不在表内)。</summary>
        internal static bool TryGetOrdinal(string name, out int ordinal)
        {
            ordinal = 0;
            return name != null && OrdinalByName.TryGetValue(name, out ordinal);
        }

        /// <summary>错误报文用的闭集字面(`{Bed, Table, …}` 的内层)。</summary>
        internal static string ClosedSetText() => string.Join(", ", Names);
    }
}
