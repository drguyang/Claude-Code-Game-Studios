// diagnosis-system Story 003 —— F-8.1 可读地板与 F-8.2 精度档槽(required evidence)。
//
// 登记落点: tests/unit/diagnosis_system/README.md(AC→测映射)
// 真身落点: unity/Assets/Tests/EditMode/DiagnosisSystem/read_floor_slots_test.cs
//
// 权威来源:
//   GDD design/gdd/diagnosis-system.md —— §F-8.1(READ_FLOOR 曲线 + 三锚点 + C-1)/
//     §F-8.2(SLOT_BOUNDS · TierIndex 计数式 · 空白档回退)/ 规则三(档位)/ 规则四(精度)/
//     §Edge Cases(体征客观存在但低于门槛 ⇒ 已查 · 阴性形态,不是未查)/
//     AC-8-5(端点与单调)· AC-8-7(G-3 整数档位)· AC-8-8(空白档回退)· AC-8-9(手段不上锁)/
//     AC-8-17(真值不翻转)· AC-8-22(已查+读不出 ≠ 未查)· AC-8-46(掉级回升)
//   ADR-026(FixPow 唯一整数幂;8 侧 = 预计算定表)· ADR-006(舍入 HALF_AWAY_FROM_ZERO)
//   Story: production/epics/diagnosis-system/story-003-read-floor-and-precision-slots.md
//
// ⚠️ 承重面 = **定表生成器(编辑期)+ 求值器(运行期)的存在与形状**:
//   ① 端点/单调 = 测试级硬约束(违反即失败,不是运行期兜底);
//   ② 双条件「与」门 = 四象限表驱动(可读集恰 {(1,1)});
//   ③ 档位判定走整数等级(G-3),浮点边界注入**不改** slot;
//   ④ 空白档**向下**回退(koplik 粗/中为空 ⇒ 在粗/中档读不出,细档起出词);
//   ⑤ 运行期零幂运算(G-1)—— 求值器只查表 + 整数比较。
//
// ⚠️ 驱动**生产**接缝:DiagnosisReadFloorBaker.BakeFromRepo(菜单与测试共用同一台机器)、
//   DiagnosisReadFloorBinderProbe.Bake/TableOf(夹具直喂)—— 不是测试侧的重实现。
//
// ═════════════════════════════════════════════════════════════════════════
// ⚠️ NOT-RUN 显式登记(禁借绿 —— story Test Evidence 要求):
//   · **AC-8-F3 的 `≥ Project(σ)` 联动子句 NOT-RUN** —— `Project(` 在 `unity/Assets/**.cs`
//     **零命中**(D-8-4 的 9 侧投影归 disease-simulation story 004,未落地)⇒ 本 story 只判
//     `READ_FLOOR_MIN > 0` 半边(回归锚);σ 联动待 9 给出 `Project` 后接(禁借绿)。
//   · **AC-8-9 的「EmitGrowth 实际门控调用」半边归 story 005**(Out of Scope 明写)——
//     本 story 只判「不存在锁闭判据」+「五手段在最小合法 Skill 有读数」。
//   · **跨会话/跨平台烘焙逐位一致 NOT-RUN**(承 story-002 / interaction story-007 口径)——
//     本文件只证**同进程**内双跑一致;跨平台浮点面归 AC-8-F5(story 004 矩阵)。
//   · **AC-8-46 的病名持久化半边归 37 / story 005**(本 story 只判曲线单测半边)。
//   · **AC-8-46 的「词变粗」子句 NOT-RUN**(QA m-5)—— 四档只配三档词(粗/中/细),
//     满档(3)结构上必回退到细档(2)⇒ 满→细同词,「词变粗」在本数据形状下**不可观测**;
//     本 story 只证 floor 回升 + slot 3→2。词面粗化归呈现层 story 006。
// ═════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using DaYiJingCheng.EditorTools.Bake;
using DaYiJingCheng.Gameplay.Presentation.Diagnosis;
using DaYiJingCheng.Sim.Contracts.SkillSystem;
using NUnit.Framework;
using SignChannel = DaYiJingCheng.Gameplay.Presentation.Diagnosis.SignChannel;

namespace DaYiJingCheng.Tests.DiagnosisSystem
{
    [TestFixture]
    internal sealed class ReadFloorSlotsTest
    {
        // ═══════════════════════════════════════════════════════════
        //  仓根 / 夹具路径(承 sign_table_test 同款回溯)
        // ═══════════════════════════════════════════════════════════

        private static string RepoRoot => ComputeRepoRoot();

        private static string ComputeRepoRoot([CallerFilePath] string thisFile = "")
            => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile) ?? ".",
                "..", "..", "..", "..", ".."));

        private static string FixtureDir =>
            Path.Combine(RepoRoot, "tests", "unit", "diagnosis_system", "fixtures");

        private static string ReadFixture(string name)
        {
            string path = Path.Combine(FixtureDir, name);
            Assert.IsTrue(File.Exists(path), $"夹具不存在:{path}");
            return File.ReadAllText(path);
        }

        private static string SeedJson() => File.ReadAllText(
            Path.Combine(RepoRoot, "assets", "data", DiagnosisReadFloorBaker.ReadFloorFileName));

        /// <summary>夹具/种子 → 定表(经生产绑定 + 生成 + 编码 + 解码;同一台机器)。</summary>
        private static DiagnosisReadFloorTable LoadTable(string json)
        {
            byte[] cooked = DiagnosisReadFloorBinderProbe.Bake(json);
            return DiagnosisReadFloorCookedCodec.Read(cooked).Table;
        }

        private static DiagnosisReadFloorTable SeedTable()
        {
            byte[] cooked = DiagnosisReadFloorBaker.BakeFromRepo(RepoRoot).Cooked;
            return DiagnosisReadFloorCookedCodec.Read(cooked).Table;
        }

        /// <summary>违例夹具驱动(端到端黑盒,承 sign_table_test BakeFails 同款):
        /// 必须抛 <see cref="BakeValidationException"/>、**恰一条错误**(排他)、且错误文本触底规则 tag。</summary>
        private static BakeValidationException BakeFails(string json, params string[] tags)
        {
            var ex = Assert.Throws<BakeValidationException>(
                () => DiagnosisReadFloorBinderProbe.Bake(json), "违例源须聚合硬失败(绝不静默烘出)");
            Assert.AreEqual(1, ex.Errors.Count,
                "单违例夹具须恰一条错误(排他)—— 夹具串味 / 多错折叠即此抓\n实得:\n" +
                string.Join("\n", ex.Errors));
            string joined = string.Join("\n", ex.Errors);
            foreach (string tag in tags)
                Assert.IsTrue(joined.Contains(tag),
                    $"错误须触底到规则「{tag}」—— 实得:\n{joined}");
            return ex;
        }

        /// <summary>造一条最小合法词条行(三档词可控,供回退 / 极性测试)。</summary>
        private static SignLexemeRow Row(
            string id, string[] words, SignChannel channel, int tier, SignPolarity polarity)
            => new SignLexemeRow(
                id, words, channel, new[] { RevealMethod.Inspection }, tier, polarity, null);

        // ═══════════════════════════════════════════════════════════
        //  AC-8-5 · C-1 端点与单调(扫描全整数档)
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_ac85_repoSeed_endpointsAndMonotonic()
        {
            DiagnosisReadFloorTable table = SeedTable();

            Assert.AreEqual(SkillRegistry.SKILL_CAP + 1, table.Count,
                "定表须覆盖 Skill ∈ [0, SKILL_CAP](全整数档)");
            Assert.AreEqual(SkillRegistry.SKILL_CAP, table.SkillCap, "定表 skill_cap 与 30 侧同源");

            // 上端锚:READ_FLOOR(0) = BASE_READ
            Assert.AreEqual(table.BaseReadRaw, RawOf(table.ReadFloor(0)),
                "READ_FLOOR(0) 须 == BASE_READ(raw 整数域比对 —— 不经浮点)");
            // 下端锚:READ_FLOOR(SKILL_CAP) = READ_FLOOR_MIN
            Assert.AreEqual(table.ReadFloorMinRaw, RawOf(table.ReadFloor(SkillRegistry.SKILL_CAP)),
                "READ_FLOOR(SKILL_CAP) 须 == READ_FLOOR_MIN");
            // C-1:BASE_READ > READ_FLOOR_MIN > 0
            Assert.Greater(table.BaseReadRaw, table.ReadFloorMinRaw, "C-1:BASE_READ > READ_FLOOR_MIN");
            Assert.Greater(table.ReadFloorMinRaw, 0L, "C-1:READ_FLOOR_MIN > 0(门槛永不为零)");

            // 单调:Skill₁ < Skill₂ ⇒ READ_FLOOR(Skill₁) ≥ READ_FLOOR(Skill₂)(全 60 对)
            for (int s = 0; s < SkillRegistry.SKILL_CAP; s++)
                Assert.GreaterOrEqual(table.ReadFloor(s), table.ReadFloor(s + 1),
                    $"单调性违反:READ_FLOOR({s}) < READ_FLOOR({s + 1}) —— Skill↑ 门槛须不升");
        }

        /// <summary>定表 float → raw Q16.16(仅供端点比对;值域 [0,1] 且小数位 ≤16 ⇒ 精确)。</summary>
        private static long RawOf(float v) => (long)Math.Round(v * 65536.0);

        [Test]
        public void test_ac8f3_readFloorMin_strictlyPositive()
        {
            // 回归锚:地板不为 0 ⇒ 满技能也留「读不出」的合法空间(AC-8-F3 可判半边)。
            DiagnosisReadFloorTable table = SeedTable();
            Assert.Greater(table.ReadFloor(SkillRegistry.SKILL_CAP), 0f,
                "满技能 READ_FLOOR 须 > 0 —— 满技能的医生也不是全知");
            Assert.Greater(table.ReadFloorMinRaw, 0L, "系数面 READ_FLOOR_MIN > 0");

            // 构造 Sign_j < READ_FLOOR_MIN ⇒ 满技能仍读不出(§F-8.1 硬约束的用例面)
            SignLexemeRow row = Row("sign_probe", new[] { "粗", "中", "细" },
                SignChannel.FaceColor, 1, SignPolarity.Positive);
            float justBelow = table.ReadFloor(SkillRegistry.SKILL_CAP) * 0.5f;
            var outcome = DiagnosisReadFloorEvaluator.Evaluate(
                table, row, SkillRegistry.SKILL_CAP, justBelow);
            Assert.IsFalse(outcome.IsReadable,
                "Sign_j < READ_FLOOR_MIN ⇒ 满技能仍读不出(AC-8-F3)");
            Assert.AreEqual(SignReadState.UnreadableNegative, outcome.State,
                "读不出须记阴性形态,不是阳性(§Edge Cases)");
        }

        // ═══════════════════════════════════════════════════════════
        //  双条件「与」门 · 四象限表驱动(可读集恰 {(1,1)})
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_ac85_andGate_fourQuadrants_readableExactlyWhenBoth()
        {
            DiagnosisReadFloorTable table = SeedTable();
            // tier_named = 20 的体征(tier 门槛清晰):Skill 取 5(<20)/ 25(≥20)
            SignLexemeRow row = Row("sign_probe", new[] { "粗", "中", "细" },
                SignChannel.Breathing, 20, SignPolarity.Positive);
            float floorAt5 = table.ReadFloor(5);
            float floorAt25 = table.ReadFloor(25);

            // 象限(skill≥tier?, sign≥floor?)
            // ① (true, true)  ⇒ 可读
            Assert.IsTrue(
                DiagnosisReadFloorEvaluator.Evaluate(table, row, 25, floorAt25 + 0.1f).IsReadable,
                "四象限①:Skill≥tier ∧ Sign≥FLOOR ⇒ 可读");
            // ② (true, false) ⇒ 不可读(值不值一读,规则四)
            Assert.IsFalse(
                DiagnosisReadFloorEvaluator.Evaluate(table, row, 25, floorAt25 - 0.1f).IsReadable,
                "四象限②:Skill≥tier ∧ Sign<FLOOR ⇒ 不可读(值低于门槛)");
            // ③ (false, true) ⇒ 不可读(**熟练度不发明体征**;即使 Sign 满值)
            Assert.IsFalse(
                DiagnosisReadFloorEvaluator.Evaluate(table, row, 5, 1.0f).IsReadable,
                "四象限③:Skill<tier ∧ Sign 满值 ⇒ 不可读(熟练度不发明体征)");
            // ④ (false, false) ⇒ 不可读
            Assert.IsFalse(
                DiagnosisReadFloorEvaluator.Evaluate(table, row, 5, floorAt5 - 0.1f).IsReadable,
                "四象限④:Skill<tier ∧ Sign<FLOOR ⇒ 不可读");

            // 边界恰值:Skill == tier ∧ Sign == FLOOR ⇒ 可读(≥ 是闭区间)
            Assert.IsTrue(
                DiagnosisReadFloorEvaluator.Evaluate(table, row, 20, table.ReadFloor(20)).IsReadable,
                "边界恰值:Skill==tier ∧ Sign==FLOOR ⇒ 可读(两条件均为 ≥)");
        }

        // ═══════════════════════════════════════════════════════════
        //  有词 ≠ 可读(两输出独立)
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_ac85_wordPresent_readableIndependent()
        {
            DiagnosisReadFloorTable table = SeedTable();
            // sign_rales 语义:tier_named = 10,三档全有词
            SignLexemeRow row = Row("sign_rales_probe", new[] { "胸里有声,说不清", "肺里有水声", "双肺底细湿啰音" },
                SignChannel.Breathing, 10, SignPolarity.Positive);

            // Skill=5(<tier):纸上有粗档词,但不可读 —— 两个输出在夹具中可分
            var o = DiagnosisReadFloorEvaluator.Evaluate(table, row, 5, 1.0f);
            Assert.IsNotNull(o.DisplayWord, "有词:即使不可读,纸上仍有粗档词(slot_j 决定)");
            Assert.IsFalse(o.IsReadable, "不可读:Skill < tier_named 即使 Sign 满值也不可读");
            Assert.AreEqual(SignReadState.UnreadableNegative, o.State, "形态 = 已查 · 读不出");

            // Skill=25(≥tier):同一条目转为可读 —— 两输出独立可证伪
            var o2 = DiagnosisReadFloorEvaluator.Evaluate(table, row, 25, 1.0f);
            Assert.IsNotNull(o2.DisplayWord, "有词");
            Assert.IsTrue(o2.IsReadable, "可读:Skill ≥ tier ∧ Sign ≥ FLOOR");
        }

        // ═══════════════════════════════════════════════════════════
        //  AC-8-7 · G-3 档位判定走整数等级(切点邻居 + 浮点扰动不变)
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_ac87_slotBoundaries_integerTierIndex()
        {
            // 每个切点 T ∈ {10,20,50}:Skill=T−1 与 Skill=T 落相邻两档
            int[] bounds = { 10, 20, 50 };
            for (int i = 0; i < bounds.Length; i++)
            {
                int t = bounds[i];
                int before = DiagnosisReadFloorEvaluator.TierIndex(t - 1);
                int at = DiagnosisReadFloorEvaluator.TierIndex(t);
                Assert.AreEqual(before + 1, at,
                    $"切点 T={t}:TierIndex(T−1)={before} 与 TierIndex(T)={at} 须相邻(差 1)");
                Assert.AreNotEqual(
                    DiagnosisReadFloorEvaluator.SlotOf(before),
                    DiagnosisReadFloorEvaluator.SlotOf(at),
                    $"切点 T={t}:两值须落相邻两档");
            }

            // 全档覆盖自证(0..60 无空洞、无越界)
            Assert.AreEqual(0, DiagnosisReadFloorEvaluator.TierIndex(0), "Skill=0 ⇒ 粗档");
            Assert.AreEqual(3, DiagnosisReadFloorEvaluator.TierIndex(SkillRegistry.SKILL_CAP),
                "Skill=SKILL_CAP ⇒ 满档");
            for (int s = 0; s <= SkillRegistry.SKILL_CAP; s++)
            {
                int ti = DiagnosisReadFloorEvaluator.TierIndex(s);
                Assert.That(ti, Is.InRange(0, 3), $"TierIndex({s}) 须 ∈ [0,3](计数天然钳位)");
            }
        }

        [Test]
        public void test_ac87_floatPerturbation_doesNotChangeSlot()
        {
            // G-3:档位判定不经 Precision 浮点 ⇒ 浮点边界注入不改变 slot_j。
            // ⚠️ 本判据为**代理**(非 AC-8-7 字面的「构造 Precision(T) 浮点边界用例」):
            //    因 `TierIndex` 只吃 int,浮点边界场景在**编译期即不可表达** —— 故以
            //    「参数类型反射断 int + 相邻整数落相邻档」代替,二者合取即 G-3 的可判面
            //    (若有人把参数改 float,参型断言转红)。AC 字面用例待 D-8-4 的
            //    `Precision`(9 侧)落地后补齐(与 AC-8-F3 的 σ 联动同批)。
            int t = 20;
            int slotBefore = DiagnosisReadFloorEvaluator.TierIndex(t - 1);
            int slotAt = DiagnosisReadFloorEvaluator.TierIndex(t);
            Assert.AreEqual(1, slotBefore, "Skill=19 ⇒ 中档(索引 1)");
            Assert.AreEqual(2, slotAt, "Skill=20 ⇒ 细档(索引 2)");

            // 无浮点入口:求值器的档位 API 只收 int(编译期即可证伪 —— 传浮点不编译)。
            // 此处以反射确认 TierIndex 参数类型为 int32。
            var mi = typeof(DiagnosisReadFloorEvaluator).GetMethod(
                nameof(DiagnosisReadFloorEvaluator.TierIndex));
            Assert.IsNotNull(mi, "TierIndex 须存在");
            Assert.AreEqual(typeof(int), mi.GetParameters()[0].ParameterType,
                "G-3:TierIndex 参数须为 int(禁浮点插值 —— 否则 19.9999999 的边界抖动)");
        }

        // ═══════════════════════════════════════════════════════════
        //  AC-8-8 · 空白档向下回退(koplik)+ 全空 ⇒ 阴性形态哨兵
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_ac88_koplikEmptyCoarseMedium_fallsDownToFineOnly()
        {
            // sign_koplik(实况):display_词 = [null, null, "口腔黏膜斑(Koplik 斑)"] ——
            // 粗 / 中档为空,细档起有词。**向下回退**:粗 / 中档向下找不到非空 ⇒ 读不出(null);
            // 细档起出词。这正是 GDD R-8.2「要想到去查」的语义(koplik 只在细档指名)。
            SignLexemeRow koplik = Row("sign_koplik",
                new string[] { null, null, "口腔黏膜斑(Koplik 斑)" },
                SignChannel.FaceColor, 1, SignPolarity.Positive);

            Assert.IsNull(DiagnosisReadFloorEvaluator.DisplayWord(koplik, 0),
                "粗档(Skill=0):向下无更粗档且本档空 ⇒ 读不出(null)");
            Assert.IsNull(DiagnosisReadFloorEvaluator.DisplayWord(koplik, 15),
                "中档(Skill=15):向下无更粗档非空 ⇒ 读不出(null)");
            Assert.AreEqual("口腔黏膜斑(Koplik 斑)",
                DiagnosisReadFloorEvaluator.DisplayWord(koplik, 25),
                "细档(Skill=25):本档有词 ⇒ 出词");
            Assert.AreEqual("口腔黏膜斑(Koplik 斑)",
                DiagnosisReadFloorEvaluator.DisplayWord(koplik, 55),
                "满档(Skill=55):向下回退到细档(满档在数据中不存在,回退到最近非空档)");
        }

        [Test]
        public void test_ac88_allSlotsEmpty_returnsUnreadableNegative()
        {
            // 全空档词条:任何 Skill 都读不出 ⇒ 阴性形态(不是阳性;AC-8-8)。
            SignLexemeRow empty = Row("sign_all_empty", new string[] { null, null, null },
                SignChannel.FaceColor, 1, SignPolarity.Positive);

            DiagnosisReadFloorTable table = SeedTable();
            var o = DiagnosisReadFloorEvaluator.Evaluate(table, empty, 55, 1.0f);
            Assert.IsNull(o.DisplayWord, "全空档 ⇒ 无词可回退");
            Assert.IsFalse(o.IsReadable, "全空档 ⇒ 读不出");
            Assert.AreEqual(SignReadState.UnreadableNegative, o.State,
                "全空档读不出须记阴性形态(不是阳性,不是未查)");
        }

        [Test]
        public void test_ac88_fallback_neverGoesUp()
        {
            // 回退只向下(往更粗档),不向上 —— 向上会泄漏「比你的精度更细」的词(规则四)。
            SignLexemeRow coarseOnly = Row("sign_coarse_only", new string[] { "只有粗档词", null, null },
                SignChannel.FaceColor, 1, SignPolarity.Positive);
            // Skill=25(细档索引 2):本档空、向下回退到粗档(索引 0)
            Assert.AreEqual("只有粗档词",
                DiagnosisReadFloorEvaluator.DisplayWord(coarseOnly, 25),
                "细档空 ⇒ 向下回退到粗档(不向上)");
        }

        // ═══════════════════════════════════════════════════════════
        //  哨兵三值可分(字母表钉死;承 AC-8-21/22)
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_sentinel_blank_unreadable_positive_distinct()
        {
            // BLANK(待查)/ UNREADABLE_NEGATIVE(已查·读不出)/ POSITIVE 三值互异。
            Assert.AreNotEqual(SignReadState.Blank, SignReadState.UnreadableNegative);
            Assert.AreNotEqual(SignReadState.UnreadableNegative, SignReadState.Positive);
            Assert.AreNotEqual(SignReadState.Blank, SignReadState.Positive);
            Assert.AreNotEqual(SignReadState.UnreadableNegative, SignReadState.Negative,
                "读不出 ≠ 真阴性(真值半边 AC-8-17)");

            // 求值器**不产出** Blank(待查由调用方给 —— 玩家没做手段;AC-8-23)
            DiagnosisReadFloorTable table = SeedTable();
            SignLexemeRow row = Row("sign_probe", new[] { "a", "b", "c" },
                SignChannel.FaceColor, 1, SignPolarity.Positive);
            var readable = DiagnosisReadFloorEvaluator.Evaluate(table, row, 55, 1.0f);
            var unreadable = DiagnosisReadFloorEvaluator.Evaluate(table, row, 55, 0f);
            Assert.AreNotEqual(SignReadState.Blank, readable.State);
            Assert.AreNotEqual(SignReadState.Blank, unreadable.State,
                "求值器只产 Positive/Negative/UnreadableNegative —— Blank 归调用方");
        }

        // ═══════════════════════════════════════════════════════════
        //  AC-8-9 · 手段不上锁(裁定⑨)—— 最小合法 Skill 可执行
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_ac89_allRevealMethods_availableAtMinSkill()
        {
            // 手段不上锁(裁定⑨):∀ Skill 不存在「手段不可用/锁闭」。判据面 =
            // ① 求值器输出字母表(SignReadState)零「不可用/锁闭」成员;
            // ② 每个 reveal_by 值在**最小合法 Skill(=0)**都**得真读数**(不抛 + 有词 + 状态非锁闭)。
            // ⚠️ 「不存在锁闭态」在代码上结构性为真(枚举无该成员)——故须以**结构**断言
            //    (枚举值域)而非「DoesNotContain(...,99)」的空转式(后者按定义恒真)。
            string[] stateNames = Enum.GetNames(typeof(SignReadState));
            Assert.AreEqual(5, stateNames.Length, "SignReadState 恰 5 成员(字母表钉死)");
            foreach (string forbidden in new[] { "Unavailable", "Locked", "Disabled", "Greyed" })
                CollectionAssert.DoesNotContain(stateNames, forbidden,
                    $"裁定⑨:不得存在「{forbidden}」锁闭态(手段永不上锁)");

            DiagnosisReadFloorTable table = SeedTable();
            var reveals = (RevealMethod[])Enum.GetValues(typeof(RevealMethod));
            Assert.AreEqual(5, reveals.Length, "P0 五法(视/触/叩/听/问)");
            foreach (RevealMethod m in reveals)
            {
                // 每法在最小合法 Skill 都能执行 → **有读数**(不抛 + 有词 + 状态 ∈ 字母表)
                SignLexemeRow row = new SignLexemeRow(
                    "sign_probe_" + m, new[] { "粗", "中", "细" },
                    SignChannel.FaceColor, new[] { m }, 1, SignPolarity.Positive, null);
                DiagnosisReadFloorEvaluator.Outcome o =
                    DiagnosisReadFloorEvaluator.Evaluate(table, row, 0, 0.5f);
                Assert.IsNotNull(o.DisplayWord,
                    $"手段 {m} 在 Skill=0 须**有读数**(有词)—— 手段不上锁,手段不空白");
                CollectionAssert.Contains((SignReadState[])Enum.GetValues(typeof(SignReadState)), o.State,
                    $"手段 {m} 的状态须 ∈ 字母表(非字母表外 = 锁闭态回潮)");
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  掉级回升(AC-8-46 曲线单测半边)
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_ac846_skillDrop_floorRises_slotFallsBack()
        {
            DiagnosisReadFloorTable table = SeedTable();

            // Skill 50 → 49 跨切点(满档 → 细档):floor 回升、slot 跌回低档
            float floor50 = table.ReadFloor(50);
            float floor49 = table.ReadFloor(49);
            Assert.GreaterOrEqual(floor49, floor50,
                "AC-8-46:掉级跨切点 ⇒ READ_FLOOR 回升(49 的门槛 ≥ 50 的)");
            Assert.AreEqual(3, DiagnosisReadFloorEvaluator.TierIndex(50), "Skill=50 ⇒ 满档");
            Assert.AreEqual(2, DiagnosisReadFloorEvaluator.TierIndex(49), "Skill=49 ⇒ 细档(跌回)");

            // 同一病人同一动作 ⇒ slot_j 跌回低档(满档 3 → 细档 2)。
            // ⚠️ AC-8-46 的「词变粗」子句**本 story 不可证**:四档只配三档词(粗/中/细),
            //    满档(3)结构上必回退到细档(2)⇒ 满→细同词(见文件头注 NOT-RUN 登记);
            //    本处只判 slot 档位跌落,词面粗化归呈现层 story 006。
            SignLexemeRow row = Row("sign_probe", new[] { "粗档", "中档", "细档" },
                SignChannel.FaceColor, 1, SignPolarity.Positive);
            Assert.AreNotEqual(
                DiagnosisReadFloorEvaluator.SlotOfSkill(50),
                DiagnosisReadFloorEvaluator.SlotOfSkill(49),
                "掉级跨切点 ⇒ slot 跌回低档(满 3 → 细 2)");
        }

        // ═══════════════════════════════════════════════════════════
        //  G-1 定表化(运行期零幂运算;定表命中 + 行集自证)
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_g1_tableLookup_deterministicAndSelfEvident()
        {
            DiagnosisReadFloorTable table = SeedTable();
            Assert.Greater(table.Count, 0, "定表非空(防空过滤跑假绿)");
            Assert.AreEqual(SkillRegistry.SKILL_CAP + 1, table.Count,
                "定表覆盖 [0, SKILL_CAP] 全整数档(61 项)");

            // 定表**命中**的可证伪面:每条读出的都是**预计算表项**,不是现算。
            // ⚠️ 「同参两次自等」按定义恒真(纯函数),不构成判据 —— 此处代之以
            //    「表项与其**存储值**逐一相同」+「跨档确有区分」,后者若有人把读表
            //    换成现算(且实现有误)会红。
            for (int s = 0; s <= SkillRegistry.SKILL_CAP; s++)
                Assert.AreEqual(table.FloorAt(s), table.ReadFloor(s),
                    $"定表命中:READ_FLOOR({s}) 须等于表项存储值(查表,非现算)");
            Assert.AreNotEqual(table.ReadFloor(0), table.ReadFloor(SkillRegistry.SKILL_CAP),
                "端点差异 ⇒ 确在读表曲线值(C-1 非退化;全表同值必红)");

            // 运行期求值器**无幂运算**(G-1):反射确认求值器**及其依赖类型**零幂/指/开方方法。
            // ⚠️ 边界门 [D-G1](DiagnosisBoundaryGates)是**真守卫**(扫 IL 调用点);
            //    本反射为**廉价前置**,名字变体(Power / MathF.Pow / 别类型 helper)
            //    非其覆盖面 —— 那由边界门兜底,此处不重复主张。
            string[] powerNames = { "Pow", "Power", "Exp", "Exp2", "Sqrt", "Cbrt", "Log", "Log2", "Log10" };
            foreach (var mi in typeof(DiagnosisReadFloorEvaluator).GetMethods(
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static |
                System.Reflection.BindingFlags.NonPublic))
                CollectionAssert.DoesNotContain(powerNames, mi.Name,
                    $"G-1:求值器不得含幂/指/开方方法(实得 {mi.Name})");
        }

        [Test]
        public void test_bakeDeterminism_doubleRun_byteEqual()
        {
            // ⚠️ 范围:**同进程**内两次烘焙逐位一致(承 story-002 口径)。
            byte[] a = DiagnosisReadFloorBaker.BakeFromRepo(RepoRoot).Cooked;
            byte[] b = DiagnosisReadFloorBaker.BakeFromRepo(RepoRoot).Cooked;
            CollectionAssert.AreEqual(a, b, "同源两次烘焙(同进程)须逐位一致(ADR-014 §二)");
        }

        [Test]
        public void test_roundTrip_codecByteStable()
        {
            // 编码 → 解码 → 再编码:逐位稳定(镜像编解码的可证伪面)。
            byte[] cooked = DiagnosisReadFloorBinderProbe.Bake(SeedJson());
            DiagnosisReadFloorDataSet ds = DiagnosisReadFloorCookedCodec.Read(cooked);
            Assert.AreEqual(1u, ds.SchemaVersion, "schema 版本回声");
            Assert.AreEqual(SkillRegistry.SKILL_CAP + 1, ds.Table.Count, "定表长度");
            // 解出的定表与直接生成的定表逐档相同
            IReadOnlyList<float> direct = DiagnosisReadFloorBinderProbe.TableOf(SeedJson());
            for (int s = 0; s < direct.Count; s++)
                Assert.AreEqual(direct[s], ds.Table.ReadFloor(s),
                    $"档 {s}:codec 解码值与生成值须逐位相同");
        }

        // ═══════════════════════════════════════════════════════════
        //  C-1 / C-5 违例夹具(生成期硬失败)
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_c1_readFloorMinZero_red()
        {
            BakeFails(ReadFixture("read_floor_min_zero_red.json"), "C-1");
        }

        [Test]
        public void test_c1_baseBelowMin_red()
        {
            BakeFails(ReadFixture("read_floor_base_below_min_red.json"), "C-1");
        }

        [Test]
        public void test_c1_endpointEqual_red()
        {
            BakeFails(ReadFixture("read_floor_endpoint_equal_red.json"), "C-1");
        }

        [Test]
        public void test_c5_gammaThird_red()
        {
            BakeFails(ReadFixture("read_floor_gamma_third_red.json"), "C-5");
        }

        [Test]
        public void test_skillcap_drift_red()
        {
            BakeFails(ReadFixture("read_floor_skillcap_drift_red.json"), "SKILL_CAP");
        }

        [Test]
        public void test_gamma_floatToken_red()
        {
            BakeFails(ReadFixture("read_floor_gamma_float_token_red.json"), "ADR-014");
        }

        [Test]
        public void test_unknownKey_red()
        {
            BakeFails(ReadFixture("read_floor_unknown_key_red.json"), "unknown-key");
        }

        // ═══════════════════════════════════════════════════════════
        //  合成系数形态夹具(单调严格 / 平段 / 半整数指数;Guardrail)
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_shape_strictMonotonicFixture()
        {
            DiagnosisReadFloorTable t = LoadTable(ReadFixture("read_floor_strict_monotonic.json"));
            Assert.AreEqual(0, t.SkillCap + 1 - t.Count, "定表覆盖 [0,60]");
            bool strictDrop = false;
            for (int s = 0; s < SkillRegistry.SKILL_CAP; s++)
            {
                Assert.GreaterOrEqual(t.ReadFloor(s), t.ReadFloor(s + 1), $"单调({s})");
                if (t.ReadFloor(s) > t.ReadFloor(s + 1)) strictDrop = true;
            }
            Assert.IsTrue(strictDrop, "严格单调夹具:至少一处严格下降(否则退化常数曲线)");
        }

        [Test]
        public void test_shape_plateauFixture_monotonicStillHolds()
        {
            // 平段形态:跨度极小(base_read 仅比 min 大 1/65536)⇒ 全表近乎平;
            // 单调(≥)仍须成立 —— 防「单调断言被平台噪声假绿」。
            DiagnosisReadFloorTable t = LoadTable(ReadFixture("read_floor_plateau_tiny_span.json"));
            for (int s = 0; s < SkillRegistry.SKILL_CAP; s++)
                Assert.GreaterOrEqual(t.ReadFloor(s), t.ReadFloor(s + 1), $"平段单调({s})");
        }

        [Test]
        public void test_shape_halfGamma_fixture()
        {
            // 半整数指数(1/2):走 FixPow 的半整数支(Pow(整数) × Sqrt)—— C-5 合法。
            DiagnosisReadFloorTable t = LoadTable(ReadFixture("read_floor_half_gamma.json"));
            Assert.GreaterOrEqual(t.ReadFloor(0), t.ReadFloor(SkillRegistry.SKILL_CAP),
                "半整数指数曲线仍单调");
            Assert.AreEqual(t.BaseReadRaw, RawOf(t.ReadFloor(0)), "端点锚不变");
        }

        // ═══════════════════════════════════════════════════════════
        //  AC-8-35 常量金标
        //  ⚠️ 覆盖声明的**准确边界**(结构侧 MAJOR-1):story-003 把
        //     **运行期枚举 / 映射常量**(DiagnosisSlot / SignReadState 枚举字面 +
        //     DiagnosisChannelMaskMap 静态数组)扩入扫描面并重钉;
        //     **F-8.1 曲线系数**(BASE_READ / READ_FLOOR_MIN / READ_GAMMA,住
        //     `assets/data/diagnosis_read_floor.json`)**不入此面** —— 它们是**数据**,
        //     由产物 ConfigVersion(内容哈希)覆盖,非前缀 ns 代码常量。
        //     ⇒ AC-8-35 的「F-8.1 **参数**半边」**仍 NOT-RUN**(与 sign_table_test 头注一致)。
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_ac835_constantsGolden_sharedScan()
        {
            List<string> lines = DiagnosisGoldenScan.BuildConstantLines();
            Assert.That(lines, Is.Not.Empty, "常量扫描面自证(空集 ⇒ 反射键失配,假绿面)");
            Assert.AreEqual(DiagnosisGoldenScan.GoldenConstantsHash, DiagnosisGoldenScan.Fnv1aHex(lines),
                "AC-8-35 常量表哈希须等于金标(story-003 扩**枚举/映射常量**面后重钉;" +
                "**曲线系数**半边仍 NOT-RUN,归 ConfigVersion);" +
                "前缀侧常量 / 枚举字面 / DIAG_TIERS 任一改动 ⇒ 红");
        }

        [Test]
        public void test_ac835_sensitivity_syntheticEntryReds()
        {
            List<string> lines = DiagnosisGoldenScan.BuildConstantLines();
            string baseHash = DiagnosisGoldenScan.Fnv1aHex(lines);
            var withExtra = new List<string>(lines)
            {
                "DaYiJingCheng.Tests.DiagnosisSystem.SyntheticProbe=42;",
            };
            Assert.AreNotEqual(baseHash, DiagnosisGoldenScan.Fnv1aHex(withExtra),
                "多一条常量行 ⇒ 哈希必须变(否则金标比的是别的东西)");
        }
    }
}
