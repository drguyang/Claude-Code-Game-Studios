// ADR-005 / GDD disease-simulation.md §F4 —— 逐实体判死谓词(门 G-1 / TD-sim-01)。
//
// 权威来源:
//   GDD disease-simulation.md:1153-1160  —— LethalFor(entity,d) = lethal(d) ∧ ¬IsCombatant(entity)
//   GDD disease-simulation.md:1162-1210  —— SelfLimited(entity,d) = [self_limit(d) ∨ IsCombatant(entity)]
//                                           ∧ ¬(Player ∧ d.Kind = Injury ∧ lethal(d) ∧ d ∈ 战伤白名单)
//                                           ⚠️ 2026-10-21 GDD 就地订正:原式写 `∧ ¬IsCombatant`,
//                                           代入 combatant 得 false,与「自限门对 combatant 恒开」相反。
//                                           GDD 原文:「实现时不得照旧式写」。
//   GDD disease-simulation.md:1212-1230  —— IsCombatant 真值表(敌人/兽 true;玩家/13 病人/友方 NPC false)
//   GDD death-and-respawn.md:96           —— 规则二:战伤破例(OQ-S18-1 已裁闭集)
//   GDD disease-simulation.md:358         —— 规则九:禁 `if kind == injury` 分支
//   ADR-016 §二                            —— 非致命模型自动适用敌人(归零 = 昏迷,不死)
//
// 核心机制:
//   - `self_limit` 是**逐条目**属性,`LethalFor` / `SelfLimited` 是**逐实体**门,二者正交
//   - combatant(敌人 / 兽)⇒ `LethalFor` 恒 false 且 `SelfLimited` 恒 true
//     ⇒ F4 死亡支关闭、昏迷支承接(ADR-016 §二)
//   - 战伤破例**只对 `Player` + `kind = Injury` + 白名单键**生效(三条件合取)
//   - **零数据依赖**:本谓词只读既有字段(`Kind` / `Curve.SelfLimit` / `Lethality` / `DiseaseKey`),
//     不要求 `disease_registry.json` 存在 ⇒ 合成夹具即可验证
//
// ⚠️ 本文件**不**实现 F4 的求值(越阈 + 干预窗口),只提供三个门。
//    F4 的死亡支求值依赖 `TREATMENT_WINDOW` 等常量,那些值尚未裁定(PRE-S18-B / D)。
//
// 🔁 **2026-10-22 补注 —— 上面那句「理由」已失效,但结论不变(原文不删)**:
//    `TREATMENT_WINDOW`(3600 tick)/ `CARE_GAP`(600 tick)/ `RECOVER_THRESHOLD`(7864 raw)
//    三值已于 2026-10-22 裁定并登记(`entities.yaml` + GDD §五)⇒ **「值没裁」不再是
//    不实现 F4 求值的理由**。F4 求值仍未实现,但性质从「阻塞(前提缺失)」变成
//    **「欠账(可做未做)」** —— 差的是**求值体**,不是**数值**。
//
//    差的三件(实测,非声称):
//    ① **求值体** —— 本类只出 `LethalFor` / `SelfLimited` / `IsCombatant` 三个门;
//       「越阈 ∧ 过窗口」那两半没有落点。`StateMachine.ComputeTransition` **也不承担**
//       (它只做纯阈值分级,且签名里 `lastInterventionTick` / `currentTick` / `diseaseKey`
//       三个形参**函数体一个都不读** —— 见 `StateMachine.cs` 该方法的补注)。
//    ② **`last_intervention` 求值体** —— 全库 `grep -rn "last_intervention\|LastIntervention"
//       Assets/ --include=*.cs | grep -v Tests` **零命中**。GDD `:1126-1133` 要它从流重算
//       (三通道取 max,空则 `onset_tick` 哨兵),而聚合器不存在 ⇒ 连入参都备不出来。
//    ③ **注册表侧 4b** —— `A_peak/SCALE ≥ DEATH_THRESHOLD` 的写入期下界**全库零校验**
//       (见 `RegistrySchema.cs` 的 `R1_CHECK_22` 补注)。缺它,注册表可以收进一条
//       「致死但永远打不到死线」的行。
//
//    ⚠️ **可达性(诚实分级,2026-10-22 第二次订正)** —— 原文写
//    「`ComputeTransition` 当前**生产调用点 = 0**(`InjuryStateChangedWriter` 无生产调用方)」,
//    **这句的括号是错的,前半句也已过时**。原文保留在上方/历史中,此处给出精确口径:
//
//      · `ComputeTransition` **有**生产调用点:`InjuryStateChangedWriter.cs:97`。
//        (`WriterExistenceGate.cs:107` 亦记「本写者是它的第一个生产调用点」。)
//      · 但 `InjuryStateChangedWriter` **自身零生产构造点** —— `new InjuryStateChangedWriter(...)`
//        只在 `Tests/EditMode/DeathRespawn/death_candidate_writers_test.cs:47` 出现。
//        同型的还有 `PlayerDiedWriter`(仅测试构造)与 `DeathCandidateConsumer`(仅测试构造)。
//      ⇒ **整条链在生产上仍不可达**,但「调用点 = 0」这个说法**不准确**,不得再引用。
//      正确说法:**「调用链在生产装配根处断开」**。
//
//    9 的 `Step` 一旦接线,纯阈值判定会让任何实体 `position ≥ 0.58` 即判死,
//    **绕过两门与窗口** ⇒ 敌人/兽被疾病杀死(违 ADR-016 §二)、玩家被白名单伤情
//    直接死而跳过昏迷。⇒ 该缺口须在 9 的 Step 接线**之前**补。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim
{
    /// <summary>
    /// 逐实体判死谓词 —— F4 死亡支的实体门(门 G-1 的兑现件)。
    ///
    /// <para><b>为什么是「逐实体」</b>:注册表里的 <c>self_limit</c> / <c>lethal</c> 是
    /// <b>逐条目</b>属性,但同一条目对不同实体应当有不同结论 —— 敌人 / 兽归零 = 昏迷,
    /// 玩家 / 病人归零可致死(ADR-016 §二)。这两个门就是那条区分的唯一落点。</para>
    ///
    /// <para><b>规则九遵守</b>:本类<b>零</b> <c>if kind == injury</c> 式的类别分支。
    /// <c>Kind</c> 只参与两处数据驱动的成员判定(<see cref="IsBattleInjury"/> 的集合查表
    /// 与 <see cref="SelfLimited"/> 的三条件合取),不改变控制流形态。</para>
    /// </summary>
    public static class EntityDeathGates
    {
        /// <summary>
        /// 战伤白名单闭集(GDD <c>death-and-respawn.md:96</c>,<c>OQ-S18-1</c> 2026-10-21 裁定)。
        ///
        /// <para><b>双条件</b>:破例 = <c>lethal(d) ∧ d ∈ 本集合</c>。单取 <c>lethal</c> 会把
        /// 「现实医学语义」改写成「P0 玩家致死路径」—— <c>INJ_BLUNT</c> / <c>INJ_FRACTURE</c> /
        /// <c>INJ_BURN</c> / <c>INJ_DISLOCATION</c> 现实上均可致死,但它们不在 P0 的玩家致死路径里。</para>
        ///
        /// <para><b>本集合是常量,不是数据行</b>:<c>assets/data/</c> 内 <c>INJ_*</c> 零命中
        /// (PRE-S18-A),故谓词按 <c>DiseaseKey</c> 字符串比对 —— 合成夹具可直接构造
        /// <c>DiseaseKey = "INJ_STAB"</c> 的条目来验,无需真注册表。</para>
        /// </summary>
        private static readonly HashSet<string> BattleInjuryWhitelist = new HashSet<string>(StringComparer.Ordinal)
        {
            "INJ_STAB",
            "INJ_LACERATION",
            "INJ_HEMORRHAGE",
            "INJ_CLAW",
            "INJ_BITE",
        };

        /// <summary>白名单只读视图(测试与 25 侧对账用;顺序不稳定,勿依赖)。</summary>
        public static IReadOnlyCollection<string> BattleInjuryKeys => BattleInjuryWhitelist;

        /// <summary>
        /// 该条目是否在战伤白名单内(GDD <c>:96</c> 双条件的前半)。
        ///
        /// <para>⚠️ 本判据<b>只</b>看键名与 <c>Kind</c>,<b>不</b>看 <c>Lethality</c> ——
        /// 「双条件」的另一半由 <see cref="SelfLimited"/> 的调用点合取,拆开是为了让
        /// 两条判据各自可单独证伪。</para>
        /// </summary>
        public static bool IsBattleInjury(DiseaseRegistryEntry entry)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            // 数据驱动:Kind 是成员判定,不是控制流分支(规则九)。
            return entry.Kind == DiseaseKind.Injury
                && entry.DiseaseKey != null
                && BattleInjuryWhitelist.Contains(entry.DiseaseKey);
        }

        /// <summary>
        /// 实体是否为「战斗员」(敌人 / 兽)—— GDD <c>:1212-1230</c> 真值表。
        ///
        /// <para><c>Player</c> ⇒ <b>false</b>;<c>HumanEnemy</c> / <c>Beast</c> ⇒ <b>true</b>。</para>
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="actorClass"/> = <see cref="ActorClass.Unspecified"/> 时抛出 ——
        /// **fail-loud,不猜**。理由:两个方向都危险 —— 当作 <c>Player</c> 会**放行致死**
        /// (玩家可被疾病杀死),当作 <c>HumanEnemy</c> 会**静默关闭致死**(敌人永远不死)。
        /// 猜错的代价是玩家死亡或敌人无敌,两者都不该由默认值决定。
        /// </exception>
        public static bool IsCombatant(ActorClass actorClass)
        {
            switch (actorClass)
            {
                case ActorClass.Player:     return false;
                case ActorClass.HumanEnemy: return true;
                case ActorClass.Beast:      return true;
                case ActorClass.Unspecified:
                default:
                    throw new ArgumentOutOfRangeException(nameof(actorClass), actorClass,
                        "实体类别未声明 —— 判死谓词拒绝猜测(当作 Player 会放行致死," +
                        "当作 HumanEnemy 会静默关闭致死)");
            }
        }

        /// <summary>
        /// <c>LethalFor(entity, d) = lethal(d) ∧ ¬IsCombatant(entity)</c> —— GDD <c>:1153-1160</c>。
        ///
        /// <para><b><c>lethal(d)</c> 的取法</b>:注册表既有字段是 <c>Lethality ∈ [0,3]</c> 整数,
        /// GDD R1 表写 bool <c>lethal</c> —— 这是<b>既有字段错位</b>(<c>RegistrySchema.cs:398-401</c>
        /// 自陈)。本实现承既有先例取 <c>Lethality &gt; 0</c>(<c>R1_CHECK_22</c> 校验即如此写),
        /// 并登记为待裁项(<c>PRE-S18-D</c>)。</para>
        ///
        /// <para><b>combatant 那一支恒 <c>false</c></b>:敌人 / 兽归零 = 昏迷,不走死亡支
        /// (ADR-016 §二)。</para>
        ///
        /// <para><b>求值次序 = <c>IsCombatant</c> 优先</b>:写成
        /// <c>LethalityOf(entry) &amp;&amp; !IsCombatant(...)</c> 时,非致死条目会让短路跳过
        /// <see cref="IsCombatant"/>,<c>Unspecified</c> 便<b>静默</b>返回 <c>false</c> —— 与本类
        /// fail-loud 契约相反。故先判类别:<c>Unspecified</c> 无论 <c>Lethality</c> 取值都抛。</para>
        /// </summary>
        public static bool LethalFor(ActorClass actorClass, DiseaseRegistryEntry entry)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            return !IsCombatant(actorClass) && LethalityOf(entry);
        }

        /// <summary>
        /// <c>SelfLimited(entity, d) = [self_limit(d) ∨ IsCombatant(entity)] ∧ ¬战伤破例</c> —— GDD <c>:1162-1210</c>,
        /// **叠加战伤破例**(<c>death-and-respawn.md:96</c>)。
        ///
        /// <para><b>⚠️ 公式与文件头同源,均取 2026-10-21 订正后的 <c>∨</c> 式</b>。旧式
        /// <c>self_limit(d) ∧ ¬IsCombatant(entity)</c> 代入 combatant 得 <c>false</c>,与「自限门对
        /// combatant 恒开」相反 —— GDD 明令不得照旧式写。</para>
        ///
        /// <para><b>三段口径</b>:</para>
        /// <list type="number">
        /// <item><b>combatant</b>(敌人 / 兽)⇒ 恒 <c>true</c>(自限门对 combatant 恒开,
        /// 昏迷支承接,死亡支关闭 —— GDD <c>:1146-1149</c>)。</item>
        /// <item><b>战伤破例</b> ⇒ 返回 <c>false</c>。三条件合取:
        /// <c>actorClass = Player</c> ∧ <c>d.Kind = Injury</c> ∧ <c>d ∈ 白名单</c>。
        /// ⚠️ 破例<b>同时关闭玩家的昏迷支</b>(F4 昏迷支曾读本谓词,2026-10-21 已改为无谓词门
        /// —— commit <c>ee12668</c>)⇒ 玩家在 <c>[COMA, DEATH)</c> 区间停在危殆,不下场。</item>
        /// <item><b>其余</b> ⇒ 注册表 <c>self_limit(d)</c> 原语义。</item>
        /// </list>
        ///
        /// <para><b>⚠️ <c>Curve == null</c> 的处置</b>:既有 legacy 占位条目曲线未声明,
        /// 此时 <c>self_limit</c> 无定义。本实现返回 <c>false</c>(视为不自限 ⇒ 不豁免死亡),
        /// 与 <c>Validate</c> 对 <c>Curve == null</c> 整块跳过曲线面校验的既有口径一致。
        /// 真数据一律带 <c>Curve</c>(<c>RegistrySchema.cs:172-177</c>)。</para>
        /// </summary>
        public static bool SelfLimited(ActorClass actorClass, DiseaseRegistryEntry entry)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));

            // ① combatant ⇒ 恒 true(自限门恒开,死亡支关闭)。
            if (IsCombatant(actorClass))
                return true;

            // ② 战伤破例(仅 Player)—— 三条件合取,任一不成立则不破例。
            if (actorClass == ActorClass.Player
                && IsBattleInjury(entry)
                && LethalityOf(entry))
            {
                return false;
            }

            // ③ 注册表原语义。
            return entry.Curve != null && entry.Curve.SelfLimit;
        }

        /// <summary>
        /// <c>lethal(d)</c> 的既有字段代理 —— <c>Lethality &gt; 0</c>。
        /// <para>⚠️ 待裁项(<c>PRE-S18-D</c>):GDD R1 表写 bool <c>lethal</c>,既有字段是
        /// <c>int Lethality ∈ [0,3]</c>。本实现承 <c>R1_CHECK_22</c> 的既有先例;
        /// 若改判为 bool 字段,改动落点只在本方法。</para>
        /// </summary>
        private static bool LethalityOf(DiseaseRegistryEntry entry) => entry.Lethality > 0;
    }
}
