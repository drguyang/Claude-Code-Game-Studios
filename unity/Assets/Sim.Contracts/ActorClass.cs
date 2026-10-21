// 权威来源:GDD disease-simulation.md:1212-1230(IsCombatant 真值表)
//           · GDD combat-and-weapon-lines.md:1154(actor_class 闭集,逐值相同)
//           · 用户裁定 2026-10-21(S18-1 候选 A:Sim.Contracts 新增枚举)
//           · ADR-016 §二(非致命模型自动适用敌人:归零 = 昏迷,不死)
//
// ⚠️ 为什么需要这个类型(2026-10-21 实测):
//   全库此前**没有任何实体类别枚举** —— `ActorClass`(CombatActionSchema.cs:25)与
//   `EntityKind`(EnemyBehaviorProgram.cs:26)两者都是 **`string`** 且只校验非空;
//   `PlayerId` 类型亦不存在(PayloadCommon.cs:13-14 自陈,玩家 id 一律裸 `int`)。
//   ⇒ `IsCombatant(entity)` 没有可接受的形参类型,谓词物理上无法签名。
//
// ⚠️ 零值陷阱(**与 DiseaseKind 同款,但方向相反**):
//   GDD 真值表把 `Player` 列为 combatant = **false**,而 C# 默认值 = 0。
//   若把 `Player` 定为 0,则「漏填 / 未初始化」会**静默**得到「非战斗员」——
//   对 `LethalFor` 而言这是**放行致死**(最危险的方向)。
//   ⇒ 故 **0 留给 `Unspecified`**,由调用方显式声明类别;谓词对 `Unspecified` 的处置
//   见 `LethalFor` / `SelfLimited` 的注释(**fail-loud,不猜**)。

namespace DaYiJingCheng.Sim.Contracts
{
    /// <summary>
    /// 实体类别 —— 逐实体判死谓词(<c>LethalFor</c> / <c>SelfLimited</c>)的形参类型。
    ///
    /// <para><b>闭集与 25 的 <c>actor_class</c> 逐值相同</b>(<c>combat-and-weapon-lines.md:1154</c>:
    /// <c>Player</c> / <c>HumanEnemy</c> / <c>Beast</c>)—— 刻意同名同序,免生第二套词汇。</para>
    ///
    /// <para><b>⚠️ 本枚举落地 ≠ 数据面可用</b>:<c>ai_enemy.json</c> 不存在
    /// (<c>EntityKind</c> 的数据载体还没有),敌人实体的类别目前**无数据来源**。
    /// 谓词本体与测试用合成夹具即可验;真接线随 27 的数据表落地。</para>
    /// </summary>
    public enum ActorClass
    {
        /// <summary>
        /// 未声明 —— <b>不是合法实体类别</b>。
        /// <para>谓词遇到它必须 <b>fail-loud</b>(抛),不得当作 <c>Player</c>(会放行致死)
        /// 也不得当作 <c>HumanEnemy</c>(会静默关闭致死)。见 <c>LethalFor</c> 注释。</para>
        /// </summary>
        Unspecified = 0,

        /// <summary>玩家 —— GDD 真值表:combatant = <b>false</b>(可被疾病 / 战伤致死)。</summary>
        Player = 1,

        /// <summary>人形敌人 —— combatant = <b>true</b>(归零 = 昏迷,不死;ADR-016 §二)。</summary>
        HumanEnemy = 2,

        /// <summary>兽类 —— combatant = <b>true</b>(同上)。</summary>
        Beast = 3,
    }
}
