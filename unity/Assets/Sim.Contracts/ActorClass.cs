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
    /// <para><b>🔁 2026-10-22 订正</b>:上句原写「逐值相同」,现新增 <see cref="Patient"/>
    /// 后不再逐值相同 —— 本枚举比 25 的闭集<b>多一个值</b>。前三个值仍逐值同名同序;
    /// 新增值的理由见其自身的 XML 注释。</para>
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

        /// <summary>
        /// 病人 / 友方 NPC —— combatant = <b>false</b>(可被疾病致死;GDD <c>:1219</c> 真值表独立一行)。
        ///
        /// <para><b>🔁 2026-10-22 新增(用户裁定)</b>。此前本枚举只有 25 的
        /// <c>{Player, HumanEnemy, Beast}</c> 三值(承 <c>combat-and-weapon-lines.md:1154</c>),
        /// 而 GDD 的 <c>IsCombatant</c> 真值表把「13 病人 / 友方 NPC」列为<b>独立一行</b>
        /// (<c>false</c>)—— 25 的动作表里没有「病人」这个概念,9 的病程求值却必须有。
        /// 缺这一值 ⇒ F4 对病人**无类别可传**,只能借 <c>Player</c> 顶上,而
        /// <c>SelfLimited</c> 的战伤破例是 <c>Player</c>-专属 ⇒ 病人会被错误地
        /// 套上玩家的白名单破例。</para>
        ///
        /// <para><b>⚠️ 与 25 的 <c>actor_class</c> 闭集不同步</b> —— 这是<b>刻意的</b>:
        /// 25 的闭集是「谁会用这条动作」,本枚举是「谁会被判死」。两者在
        /// <c>{Player, HumanEnemy, Beast}</c> 上重合,<c>Patient</c> 只在后者出现。
        /// 25 的数据表若写入 <c>Patient</c> 应被其自身校验拒绝(不在 25 的闭集内),
        /// 不属于本枚举的职责。</para>
        /// </summary>
        Patient = 4,
    }
}
