// S18-1 / TD-sim-01 / 门 G-1 —— 逐实体判死谓词的验收面。
//
// 权威来源:
//   GDD disease-simulation.md:1153-1160 —— LethalFor(entity,d) = lethal(d) ∧ ¬IsCombatant(entity)
//   GDD disease-simulation.md:1162-1210 —— SelfLimited(entity,d) = [self_limit(d) ∨ IsCombatant(entity)]
//                                          ∧ ¬战伤破例(**2026-10-21 GDD 订正**;旧式 `∧ ¬IsCombatant` 已作废)
//   GDD disease-simulation.md:1212-1230 —— IsCombatant 真值表
//   GDD death-and-respawn.md:96          —— 战伤破例(OQ-S18-1 2026-10-21 裁定闭集)
//   GDD disease-simulation.md:358        —— 规则九:禁 `if kind == injury` 分支
//   ADR-016 §二                           —— 非致命模型:敌人归零 = 昏迷,不死
//
// ⚠️ 本文件的验收范围(如实登记,禁借绿):
//   AC ①–⑤(谓词逻辑 / 正交性 / 破例三条件 / 零涟漪)= **行为面有测**。
//   ⚠️ **AC ⑥(规则九)无机械断言** —— 2026-10-22 代码评审 R-2 实测:本文件此前声称
//   「文件末尾有 AST 式断言」,**该断言从未存在**,全仓 `Editor.Tools.Gates` 亦无规则九门。
//   现有 `test_ruleNine_*` 只验「Kind 是数据、结论随数据变」,**不**能抓 `if kind == injury`
//   式硬分支 ⇒ **AC ⑥ = NOT-RUN**(唯一证据 = 人工读 `EntityDeathGates.cs` 的源码)。
//   补机械门归 Tooling 层(见评审原件 §Required R-2)。
//   **AC ⑦⑧(`DeathCandidate` 生产写者 / `InjuryStateChanged` 写者)不在本文件** ——
//   二者是生产接线,须 `WriterExistenceGate` 条目转绿,当前 NOT-RUN。
//   本文件**不**验证 F4 求值(越阈 + 干预窗口)—— 那依赖 `TREATMENT_WINDOW` 等未裁定常量。
//
// 夹具策略:全部用**合成条目**(直构 `DiseaseRegistryEntry`),不依赖 `disease_registry.json`
//   (该文件不存在,PRE-S18-A)。这是**刻意的**:谓词只读既有字段,零数据依赖即可验。

using System;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.DiseaseSimulation
{
    /// <summary>逐实体判死谓词的验收夹具。</summary>
    public class EntityDeathGatesTest
    {
        // ── 夹具构造:只填谓词读到的字段,其余留默认(谓词不读它们)──
        private static DiseaseRegistryEntry Entry(
            string key,
            DiseaseKind kind,
            bool selfLimit,
            int lethality)
        {
            return new DiseaseRegistryEntry
            {
                DiseaseId = 1,
                DiseaseKey = key,
                Kind = kind,
                Lethality = lethality,
                Curve = new NaturalProgressCurve { SelfLimit = selfLimit },
            };
        }

        // ══ AC ②:IsCombatant 真值表(GDD :1212-1230)══════════════════════

        [Test]
        public void test_isCombatant_playerIsNotCombatant()
        {
            Assert.IsFalse(EntityDeathGates.IsCombatant(ActorClass.Player),
                "玩家不是战斗员 —— 可被疾病 / 战伤致死(GDD :1212-1230)");
        }

        [Test]
        public void test_isCombatant_enemyAndBeastAreCombatants()
        {
            Assert.IsTrue(EntityDeathGates.IsCombatant(ActorClass.HumanEnemy), "人形敌人是战斗员");
            Assert.IsTrue(EntityDeathGates.IsCombatant(ActorClass.Beast), "兽类是战斗员");
        }

        [Test]
        public void test_isCombatant_unspecifiedFailsLoud()
        {
            // 零值陷阱守卫:Unspecified 必须抛,不得静默落成任一方向。
            // 猜 Player ⇒ 放行致死;猜 HumanEnemy ⇒ 静默关闭致死。两者都不可接受。
            Assert.Throws<ArgumentOutOfRangeException>(
                () => EntityDeathGates.IsCombatant(ActorClass.Unspecified),
                "未声明类别须 fail-loud");
        }

        // ══ AC ②(续):combatant ⇒ LethalFor 恒 false ══════════════════════

        [Test]
        public void test_lethalFor_combatantAlwaysFalse_evenWhenLethal()
        {
            // 反例半边:同一 lethal 条目对玩家 = true,对敌人/兽 = false。
            var lethalDisease = Entry("DIS_TYPHOID", DiseaseKind.Disease, selfLimit: false, lethality: 3);

            Assert.IsTrue(EntityDeathGates.LethalFor(ActorClass.Player, lethalDisease),
                "同一条目对玩家应可致死");
            Assert.IsFalse(EntityDeathGates.LethalFor(ActorClass.HumanEnemy, lethalDisease),
                "敌人归零 = 昏迷,不走死亡支(ADR-016 §二)");
            Assert.IsFalse(EntityDeathGates.LethalFor(ActorClass.Beast, lethalDisease),
                "兽类同上");
        }

        [Test]
        public void test_lethalFor_nonLethalEntryAlwaysFalse()
        {
            // Lethality = 0 ⇒ lethal(d) = false ⇒ 对任何非 combatant 也不致死。
            var mild = Entry("DIS_MILD", DiseaseKind.Disease, selfLimit: true, lethality: 0);
            Assert.IsFalse(EntityDeathGates.LethalFor(ActorClass.Player, mild),
                "Lethality = 0 不致死");
        }

        [Test]
        public void test_lethalFor_unspecifiedFailsLoud_evenOnNonLethalEntry()
        {
            // 2026-10-22 评审 R-4:求值次序若写成 `LethalityOf(entry) && !IsCombatant(...)`,
            // 非致死条目会让短路跳过 IsCombatant ⇒ Unspecified **静默**返回 false,
            // 击穿 fail-loud 契约。本测试专钉该不对称路径:致死与非致死两条都必须抛。
            var lethal = Entry("DIS_TYPHOID", DiseaseKind.Disease, selfLimit: false, lethality: 3);
            var mild = Entry("DIS_MILD", DiseaseKind.Disease, selfLimit: true, lethality: 0);

            Assert.Throws<ArgumentOutOfRangeException>(
                () => EntityDeathGates.LethalFor(ActorClass.Unspecified, lethal),
                "致死条目 + Unspecified 须抛");
            Assert.Throws<ArgumentOutOfRangeException>(
                () => EntityDeathGates.LethalFor(ActorClass.Unspecified, mild),
                "非致死条目 + Unspecified 同样须抛 —— 短路不得跳过类别校验");
        }

        // ══ AC ①:同条目、不同实体,SelfLimited 结论不同(本件最核心)════════

        [Test]
        public void test_selfLimited_sameEntryDiffersByActorClass()
        {
            // 最核心一条:同一条目 self_limit = false(不自限),玩家与敌人结论必须相反。
            // ⚠️ 判别力要求:两种实体**其余入参完全相同**,只差 ActorClass。
            var nonSelfLimiting = Entry("DIS_TYPHOID", DiseaseKind.Disease, selfLimit: false, lethality: 2);

            Assert.IsFalse(EntityDeathGates.SelfLimited(ActorClass.Player, nonSelfLimiting),
                "玩家:self_limit=false ⇒ 不自限");
            Assert.IsTrue(EntityDeathGates.SelfLimited(ActorClass.HumanEnemy, nonSelfLimiting),
                "敌人:自限门恒开 ⇒ 恒 true(昏迷支承接)");
        }

        // ══ AC ③:正交性 —— LethalFor 与 SelfLimited 互不蕴含 ══════════════

        // 四组合真值表(Player 侧,按 GDD 公式逐格手算):
        //   LethalFor   = lethality > 0 ∧ ¬IsCombatant(Player)=true  ⇒ 只由 lethality 决定
        //   SelfLimited = self_limit   ∧ ¬IsCombatant(Player)=true  ⇒ 只由 self_limit 决定
        // ⇒ 两门互不蕴含(正交性):每一门都恰有一个输入,且输入不同。
        [TestCase(true, 3, true, true, TestName = "test_orthogonality_selfLimit_true_lethal_true")]
        [TestCase(true, 0, false, true, TestName = "test_orthogonality_selfLimit_true_lethal_false")]
        [TestCase(false, 3, true, false, TestName = "test_orthogonality_selfLimit_false_lethal_true")]
        [TestCase(false, 0, false, false, TestName = "test_orthogonality_selfLimit_false_lethal_false")]
        public void test_orthogonality_lethalAndSelfLimitedIndependent(
            bool selfLimit, int lethality, bool expectLethal, bool expectSelfLimited)
        {
            // 四个组合各一测 —— 只测两个组合无法排除「实现把两者绑死」。
            // 用 Player(唯一同时受两门约束的类别)避免 combatant 的恒定值掩盖正交性。
            var entry = Entry("DIS_MATRIX", DiseaseKind.Disease, selfLimit, lethality);

            Assert.AreEqual(expectLethal, EntityDeathGates.LethalFor(ActorClass.Player, entry),
                $"LethalFor(self_limit={selfLimit}, lethality={lethality})");
            Assert.AreEqual(expectSelfLimited, EntityDeathGates.SelfLimited(ActorClass.Player, entry),
                $"SelfLimited(self_limit={selfLimit}, lethality={lethality})");
        }

        // ══ AC ④:战伤破例 —— 三条件合取,各做否例 ════════════════════════

        [Test]
        public void test_battleInjuryExemption_allThreeConditionsHold_returnsFalse()
        {
            // 正例:Player ∧ Injury ∧ 白名单键 ∧ lethal ⇒ 破例(返回 false,即使 self_limit = true)。
            var injury = Entry("INJ_STAB", DiseaseKind.Injury, selfLimit: true, lethality: 2);

            Assert.IsFalse(EntityDeathGates.SelfLimited(ActorClass.Player, injury),
                "三条件齐 ⇒ 破例,自限门关闭");
        }

        [Test]
        public void test_battleInjuryExemption_neg1_notPlayer_returnsRegistryValue()
        {
            // 否例 ①:去掉 Player 条件(敌人)。
            // ⚠️ 敌人走 combatant 分支恒 true —— 破例不成立,但不是「回到注册表值」而是「恒 true」。
            var injury = Entry("INJ_STAB", DiseaseKind.Injury, selfLimit: true, lethality: 2);
            Assert.IsTrue(EntityDeathGates.SelfLimited(ActorClass.HumanEnemy, injury),
                "非 Player ⇒ 破例不成立(combatant 恒 true)");
        }

        [Test]
        public void test_battleInjuryExemption_neg2_notInjuryKind_returnsRegistryValue()
        {
            // 否例 ②:去掉 kind = Injury 条件(同名键但标成 Disease)。
            var mislabeled = Entry("INJ_STAB", DiseaseKind.Disease, selfLimit: true, lethality: 2);
            Assert.IsTrue(EntityDeathGates.SelfLimited(ActorClass.Player, mislabeled),
                "Kind = Disease ⇒ 破例不成立,回到注册表 self_limit = true");
        }

        [Test]
        public void test_battleInjuryExemption_neg3_notInWhitelist_returnsRegistryValue()
        {
            // 否例 ③:去掉白名单条件(伤情但不在五项闭集里)。
            var offList = Entry("INJ_FRACTURE", DiseaseKind.Injury, selfLimit: true, lethality: 2);
            Assert.IsTrue(EntityDeathGates.SelfLimited(ActorClass.Player, offList),
                "INJ_FRACTURE 不在闭集 ⇒ 破例不成立,回到注册表值");
        }

        [Test]
        public void test_battleInjuryExemption_neg4_notLethal_returnsRegistryValue()
        {
            // 否例 ④:双条件的另一半 —— 白名单键但 lethal = false。
            var nonLethal = Entry("INJ_STAB", DiseaseKind.Injury, selfLimit: true, lethality: 0);
            Assert.IsTrue(EntityDeathGates.SelfLimited(ActorClass.Player, nonLethal),
                "lethal(d) = false ⇒ 破例不成立(双条件)");
        }

        [Test]
        public void test_battleInjuryExemption_selfLimitFalseIsNoOp()
        {
            // 边界:破例对 self_limit = false 的伤情**无意义**(本来就不自限)。
            // 结论仍为 false —— 不是「破例生效」,而是两条路殊途同归。
            var injury = Entry("INJ_STAB", DiseaseKind.Injury, selfLimit: false, lethality: 2);
            Assert.IsFalse(EntityDeathGates.SelfLimited(ActorClass.Player, injury));
        }

        [Test]
        public void test_battleInjuryWhitelist_isExactlyTheFiveKeys()
        {
            // 闭集完整性:五项且仅五项(OQ-S18-1 裁)。多一项 = 扩了致死面;少一项 = 缩了。
            var expected = new[] { "INJ_STAB", "INJ_LACERATION", "INJ_HEMORRHAGE", "INJ_CLAW", "INJ_BITE" };
            CollectionAssert.AreEquivalent(expected, EntityDeathGates.BattleInjuryKeys,
                "战伤白名单须恰为 OQ-S18-1 裁定的五项");
        }

        // ══ AC ⑤:零涟漪 —— 谓词不读 Curve 之外的条目状态,不修改入参 ══════

        [Test]
        public void test_zeroRipple_predicateDoesNotMutateEntry()
        {
            // 谓词是纯函数:调用前后条目状态逐字段不变(否则会污染共享注册表)。
            var entry = Entry("INJ_STAB", DiseaseKind.Injury, selfLimit: true, lethality: 2);
            var beforeKind = entry.Kind;
            var beforeSelfLimit = entry.Curve.SelfLimit;
            var beforeLethality = entry.Lethality;

            EntityDeathGates.LethalFor(ActorClass.Player, entry);
            EntityDeathGates.SelfLimited(ActorClass.Player, entry);

            Assert.AreEqual(beforeKind, entry.Kind, "Kind 未被修改");
            Assert.AreEqual(beforeSelfLimit, entry.Curve.SelfLimit, "self_limit 未被修改");
            Assert.AreEqual(beforeLethality, entry.Lethality, "Lethality 未被修改");
        }

        [Test]
        public void test_zeroRipple_legacyNullCurveTreatedAsNotSelfLimiting()
        {
            // 既有 legacy 占位条目 Curve == null ⇒ self_limit 无定义。
            // 口径:返回 false(不自限 ⇒ 不豁免死亡),与 Validate 跳过曲线面校验一致。
            var legacy = new DiseaseRegistryEntry
            {
                DiseaseId = 1,
                DiseaseKey = "fallback",
                Kind = DiseaseKind.Disease,
                Lethality = 0,
            };
            Assert.IsFalse(EntityDeathGates.SelfLimited(ActorClass.Player, legacy),
                "Curve == null ⇒ self_limit 视作 false");
        }

        // ══ AC ⑥:规则九 —— 无 `if kind == injury` 式类别分支 ══════════════

        [Test]
        public void test_ruleNine_kindIsDataDrivenNotControlFlow()
        {
            // 判据形态:把**同一个**白名单键改成不同 Kind,破例结论须随之改变。
            //
            // ⚠️ **本测试不构成 AC ⑥(规则九)的证据**(2026-10-22 评审 R-2):它只验
            // 「Kind 是数据、结论随数据变」;**抓不到** `if kind == injury` 式硬分支
            // (那种实现下本测试照样绿)。规则九**无任何机械断言** —— 此前注释声称的
            // 「文件末尾 AST 式断言」从未存在,已删。补门归 Tooling 层。
            var asInjury = Entry("INJ_STAB", DiseaseKind.Injury, selfLimit: true, lethality: 2);
            var asDisease = Entry("INJ_STAB", DiseaseKind.Disease, selfLimit: true, lethality: 2);

            Assert.IsFalse(EntityDeathGates.SelfLimited(ActorClass.Player, asInjury),
                "Kind = Injury 时破例生效");
            Assert.IsTrue(EntityDeathGates.SelfLimited(ActorClass.Player, asDisease),
                "Kind = Disease 时破例不生效 —— Kind 是数据,结论随数据变");
        }
    }
}
