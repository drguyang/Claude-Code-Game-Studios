// 奇遇步骤表(ADR-045 · N5 的判据闭集)—— 序章五条 PA-01…PA-05。
//
// 权威:`design/levels/prologue-beat-script.md` §3.1(五件组件 + 四条硬规则 + 数值 6/4/5/1/2)
//      `docs/architecture/adr-045-quest-stream.md` §D2(两个 Kind)· §D4(驱动纯读)
//
// ⚠️ **P0 的五条以代码常量表落地**(不是 `assets/data/adventure_steps.json`)——
//    理由是硬的:判据闭集只有两种、数值是用户在 2026-11-14 当场给定的**固定值**、
//    表里没有一个 `Fix`(ADR-014 的「必须外部化」收窄到承载 `Fix` 的字段)。
//    外部化的触发条件:① 条数超过 10;② 出现需要作者反复调参的数值;③ 通用任务系统立项。
//    在此之前,**这张表就是真源**,改它 = 改本文件(登记于 ADR-045 §Consequences)。
//
// ⚠️ 判据闭集 = 两种,**第三种须另裁**:
//    · `GatherInRect`:在矩形区域内的采集点完成 n 次采集;
//    · `Defeat`:击败某类敌人 n 个 —— **今日无生产者**(`EnemySpawn` 生产零命中,ADR-045 §R2),
//      故 PA-04 / PA-05 会长期停在「0/1」—— 这不是 bug,是已知缺件。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>奇遇的完成判据种类(闭集)。</summary>
    public enum AdventureCondition
    {
        /// <summary>在矩形区域内完成 n 次采集。</summary>
        GatherInRect = 0,

        /// <summary>击败某类敌人 n 个(⚠️ 今日无生产者)。</summary>
        Defeat = 1,
    }

    /// <summary>一条奇遇步骤(纯数据)。</summary>
    public readonly struct AdventureStep
    {
        /// <summary>奇遇 id(`= 表内行序`;入载荷)。</summary>
        public readonly int Id;

        /// <summary>完成时授予的组件(21a 物品表 ordinal)。</summary>
        public readonly int ItemKey;

        /// <summary>判据种类。</summary>
        public readonly AdventureCondition Condition;

        /// <summary>区域矩形(仅 <see cref="AdventureCondition.GatherInRect"/>,闭区间)。</summary>
        public readonly int X0, Z0, X1, Z1;

        /// <summary>需要多少次(≥ 1)。</summary>
        public readonly int Required;

        /// <summary>敌人类型(仅 <see cref="AdventureCondition.Defeat"/>,今日无生产者)。</summary>
        public readonly int EnemyKind;

        /// <summary>构造。</summary>
        public AdventureStep(int id, int itemKey, AdventureCondition condition,
                             int x0, int z0, int x1, int z1, int required, int enemyKind = 0)
        {
            if (id < 0) throw new ArgumentOutOfRangeException(nameof(id), id, "奇遇 id 须 ≥ 0");
            if (itemKey < 0) throw new ArgumentOutOfRangeException(nameof(itemKey), itemKey, "item_key 须 ≥ 0");
            if (required < 1) throw new ArgumentOutOfRangeException(nameof(required), required, "required 须 ≥ 1");
            if (condition == AdventureCondition.GatherInRect)
            {
                if (x1 < x0 || z1 < z0)
                    throw new ArgumentException($"奇遇 {id} 的区域矩形倒置:({x0},{z0})..({x1},{z1})");
            }
            Id = id; ItemKey = itemKey; Condition = condition;
            X0 = x0; Z0 = z0; X1 = x1; Z1 = z1;
            Required = required; EnemyKind = enemyKind;
        }

        /// <summary>该格是否在区域内(闭区间;仅采集型有意义)。</summary>
        public bool Contains(int x, int z) => x >= X0 && x <= X1 && z >= Z0 && z <= Z1;
    }

    /// <summary>序章五条奇遇(P0 的常量表 —— 见文件头「为什么不是 JSON」)。</summary>
    public static class AdventureSteps
    {
        /// <summary>
        /// PA-01…PA-05。区域与数值逐字取自 `prologue-beat-script.md` §3.1:
        /// 废墟 A(教堂 `x4..15 / z14..29`)· 废墟 B(药铺废墟 POI 1 `(24,8)`)· 七里路(`x31..96`)。
        /// </summary>
        public static readonly IReadOnlyList<AdventureStep> PrologueFive = new[]
        {
            // PA-01 门板:废墟 A 采集 6 次
            new AdventureStep(0, itemKey: 9, AdventureCondition.GatherInRect,
                              x0: 4, z0: 14, x1: 15, z1: 29, required: 6),
            // PA-02 钉子:废墟 B(药铺废墟四周半径 3)采集 4 次
            new AdventureStep(1, itemKey: 10, AdventureCondition.GatherInRect,
                              x0: 21, z0: 5, x1: 27, z1: 11, required: 4),
            // PA-03 房梁:七里路采集 5 次
            new AdventureStep(2, itemKey: 11, AdventureCondition.GatherInRect,
                              x0: 31, z0: 0, x1: 96, z1: 39, required: 5),
            // PA-04 布:击败兵痞 1 名(⚠️ 无生产者)
            new AdventureStep(3, itemKey: 12, AdventureCondition.Defeat,
                              x0: 0, z0: 0, x1: 0, z1: 0, required: 1, enemyKind: 0),
            // PA-05 油布:击败野狗 2 只(⚠️ 无生产者;设计上走 52 随机事件)
            new AdventureStep(4, itemKey: 13, AdventureCondition.Defeat,
                              x0: 0, z0: 0, x1: 0, z1: 0, required: 2, enemyKind: 1),
        };
    }
}
