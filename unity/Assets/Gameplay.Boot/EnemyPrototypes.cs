// 敌人原型表(P0 · 序章的两种遭遇实体)—— 兵痞 / 野狗。
//
// 权威:
//   GDD enemy-ai.md 规则五(六态机)· `PR-27-1`(P0 = **一套行为程序 × 两类参数行**)
//   ADR-016 §九(「单原型」= 行为程序表达力上限,不是实体种类数)
//   `design/levels/prologue-beat-script.md` §3.1(PA-04 = 兵痞 1 · PA-05 = 野狗 2)
//
// ⚠️ **本表住代码,不住 JSON**(与 `AdventureSteps` 的 §D9′ 同一条裁定):
//   P0 只有两行、数值是出场编排的固定值、表里没有一个 `Fix`。外部化触发条件:
//   ① 行数 > 10;② 出现需要反复调参的数值;③ 27 的 `ai_enemy.json` 烘焙管线立项
//   —— 届时本表整体迁入该数据集,不保留第二份。
//
// ⚠️ **本表不含血量**:25 的模型里敌人与病人共用伤情状态机(ADR-016 §二),
//   「倒地」= 累计 PS 达 `StateMachine.COMA_THRESHOLD`,不是另一套 HP。

using System;
using System.Collections.Generic;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>一条敌人原型(P0 的最小呈现/归类面)。</summary>
    public readonly struct EnemyPrototype
    {
        /// <summary>原型 id(`EncounterStarted.proto_id`;也是 `AdventureStep.EnemyKind`)。</summary>
        public readonly int Id;

        /// <summary>显示名(字幕 / 调试读数用)。</summary>
        public readonly string DisplayName;

        /// <summary>倒地后的下游类(`Healable` = 人形 / `Salvageable` = 野兽;27 规则十八)。</summary>
        public readonly string DownClass;

        /// <summary>构造一行。</summary>
        public EnemyPrototype(int id, string displayName, string downClass)
        {
            Id = id; DisplayName = displayName; DownClass = downClass;
        }
    }

    /// <summary>P0 的两行原型表。</summary>
    public static class EnemyPrototypes
    {
        /// <summary>兵痞(人形 · `Healable`)。</summary>
        public const int Thug = 0;

        /// <summary>野狗(野兽 · `Salvageable`)。</summary>
        public const int WildDog = 1;

        /// <summary>全部原型(声明序 = id 序)。</summary>
        public static readonly IReadOnlyList<EnemyPrototype> All = new[]
        {
            new EnemyPrototype(Thug, "兵痞", "Healable"),
            new EnemyPrototype(WildDog, "野狗", "Salvageable"),
        };

        /// <summary>按 id 取一行(越界 ⇒ 抛 —— 调用方给的是内容常量,不是用户输入)。</summary>
        /// <exception cref="ArgumentOutOfRangeException">id 不在表内。</exception>
        public static EnemyPrototype Get(int id)
        {
            for (int i = 0; i < All.Count; i++)
                if (All[i].Id == id) return All[i];
            throw new ArgumentOutOfRangeException(nameof(id), id, "原型 id 不在 P0 表内");
        }
    }
}
