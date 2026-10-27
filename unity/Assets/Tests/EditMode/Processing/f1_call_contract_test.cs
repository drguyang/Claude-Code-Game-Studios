// S20-8 · processing story-002 —— F-18.1 调用契约 / 四修正项供料 / 装载期防御
//
// 权威来源:
//   design/gdd/processing.md
//     · F-18.1(:416-434)调用形状 · 表 :423-429 各变量归属
//     · 规则九(:243-245)18 不认识药(`drug_profile` / `polarity` / `treatable_by`)
//     · AC-18-01 / 02 / 03 / 04 / 06 / 14
//   ADR-014 §一(18 供料 / 21a 定曲线)· ADR-006(EnvMod_total 唯一钳制落点在 21a)
//   ADR-025 §①(`Sim` 引用集恰 = {BCL, Sim.Contracts})
//   story production/epics/processing/story-002-f1-call-contract-and-mod-inputs.md
//
// ─────────────────────────────────────────────────────────────────────────────
// AC 记分(禁把「本体跑通」读成「六条全绿」)
// ─────────────────────────────────────────────────────────────────────────────
//   ✅ AC-18-01 ① 恰 1 次(Spy 计数 + IL 符号面)
//               ③ 第二求解实现 = 零(**IL 符号引用扫描** + 文本扫描 + 各配正控)
//   ⚠️ AC-18-01 ② **半绿**(2026-10-27 评审修复轮降级,原记 ✅ 是记分卡比证据强):
//               · **绿**:7 个出参原样透传 —— 2 个数组 `ReferenceEquals`(`:217-220`),
//                 5 个标量值恒等(`test_ac1801_scalarOutputsPassedThroughVerbatim`)。
//               · **NOT-RUN**:「18 **交给 20**」这一腿 —— 生产侧**零调用点**
//                 (`test_ac1801_hasProductionCallSite` 钉住该事实),没有 20 侧接收者
//                 ⇒ 现有断言只证了「Settle 的返回值 == solver 的返回值」,
//                 那是 `return solver(...)` 的平凡推论。补全归 Story 003 接线。
//
// 🔁 **2026-10-27 更新(S20-13 / Story 003 接线轮)**:生产调用点**已落地** ——
//    `Sim/Processing/CraftIgnitionTransaction.cs` 的第 ③ 步调 `Settle`,并把出参整体
//    转发给写者 `Sim/World/CraftEventWriter.cs`。`test_ac1801_hasProductionCallSite`
//    **已按它自己的指示翻转为 `Is.Not.Empty` + 点名该文件**。
//    ⚠️ **但 AC-18-01② 的记分不变**:「交给 **20**」那条腿仍 **NOT-RUN** ——
//    20 侧接收者(`Apply`)归 inventory-005(S20-14),现在的下游是**18 自己的写者**。
//    翻转判据的是「18 侧有真调用点」,不是「20 侧有人接」。**禁把本次翻绿读成该 AC 全绿。**
//   ⚠️ AC-18-01 Edge「失败路径 F1 零调用」**半边 NOT-RUN**(同上):原 `failurePath` 夹具是
//               **恒真断言**(断一个刚 new 的 spy 的计数 = 0),已重写为
//               `test_ac1801_settleDoesNotRejudgeAdmission`(测「18 不重复判准入」,真可测)。
//               「调用方在准入不过时不调 Settle」需 Story 001↔002 接线,当前无载体。
//   ✅ AC-18-02 结算路径 `Ceil`/`Round`/`Clamp`/定点乘除 = 零
//               (**IL 指令面 + IL 成员调用面** + 文本扫描,三面各配正控)
//   ✅ AC-18-03 `L` 入参 == 同一次 `QueryLevel` 返回值(逐位;**另断「不是换算结果」**)
//   ✅ AC-18-04 `drug_profile` / `polarity` / `treatable_by` 读取 = 零
//               (**IL 符号引用扫描**:call 目标 + ldfld/stfld 字段名;另配文本扫描)
//   ✅ AC-18-06 两 EnvMod 分量各自越界 ⇒ 逐位原样透传(零加法零 clamp)
//               零加法的**符号面** = `test_ac1806_il_settlementClassHasNoAddSub`
//               (2026-10-27 补:`add`/`sub` 原先两层扫描都看不见,只有行为层守)
//   ✅ AC-18-14 装载期破 cap 表 ⇒ 硬失败(`throw`,非 `Debug.Assert`)
//   ⚠️ AC-18-10b [L] **EXTERNAL · BLOCKED-BY-42** —— 42 炮制面板未交付,
//                走查不可能执行。**本文件不记该条为绿**(禁以「机制侧供数正确」代走查)。
//
// ─────────────────────────────────────────────────────────────────────────────
// 三条自我约束(承 S20-4 `admissible_predicate_test.cs` 同款,为什么这样写)
// ─────────────────────────────────────────────────────────────────────────────
// ① **零字面量调参** —— 常量表一律夹具自建并标注「夹具值」,不抄真表数值。
// ② **判据不引用被测常量** —— 断言里不拿 `ProcessingSettlementInputs.CraftSkillId` 当期望值,
//    改用字面量 7 与 `(int)SkillId.炮制` 双向钉;否则变异「改 CraftSkillId」时两边一起动
//    ⇒ 零红逃逸(自指判据形态,本项目已抓过)。
// ③ **扫描器必须自证有判别力** —— 每个「零命中」断言都配一条正控,
//    且正控**走与判据同一条链路**(不是另写一份 `IndexOf`);
//    没有正控的零命中与「扫描器什么都没扫到」逐位不可区分。
//
// ⚠️ 零随机 / 零时间依赖 / 零写盘(只有 `File.ReadAllText` + 只读 Cecil 装载,不触 FsWriteBoundaryGate)。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using NUnit.Framework;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.Contracts.SkillSystem;
using DaYiJingCheng.Sim.Processing;

namespace DaYiJingCheng.Tests.EditMode.Processing
{
    /// <summary>processing story-002 —— AC-18-01/02/03/04/06/14 的 EditMode 夹具。</summary>
    [TestFixture]
    internal sealed class F1CallContractTest
    {
        // ══════════════════════════════════════════════════════════════════
        // 夹具常量(全部为**夹具值** —— 不是真表数值,也不抄 GDD 示例)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>夹具值:玩家 id。</summary>
        private const int Actor = 42;

        /// <summary>夹具值:输入品级。</summary>
        private const int InQ = 3;

        /// <summary>夹具值:炮制技能等级。</summary>
        private const int Level = 30;

        /// <summary>炮制技能 id 的**字面量**形 —— 与 <c>(int)SkillId.炮制</c> 双向钉;
        /// 刻意不引用 <c>ProcessingSettlementInputs.CraftSkillId</c>(自指判据禁令)。</summary>
        private const int CraftSkillLiteral = 7;

        // ══════════════════════════════════════════════════════════════════
        // 测试替身
        // ══════════════════════════════════════════════════════════════════

        /// <summary>等级查询替身 —— 记录调用次数与查询过的技能 id。</summary>
        private sealed class SpySkillLevels : ISkillLevelQuery
        {
            private readonly int _level;

            public int CallCount { get; private set; }
            public List<int> QueriedSkillIds { get; } = new List<int>();
            public List<int> QueriedActorIds { get; } = new List<int>();

            public SpySkillLevels(int level) => _level = level;

            public int QueryLevel(int actorId, int skillId)
            {
                CallCount++;
                QueriedSkillIds.Add(skillId);
                QueriedActorIds.Add(actorId);
                return _level;
            }
        }

        /// <summary>F1 求解器替身 —— 记录入参、调用次数,并返回**可辨识**的结果。</summary>
        /// <remarks>
        /// ⚠️ <b>返回值刻意用可辨识的数组实例</b>(非 <c>Solve</c> 的真结果):AC-18-01② 断的是
        /// 「20 拿到的 == F1 返回的」,**不是**「值算对了」。若用真求解器,「值相等」会让
        /// 「18 悄悄重算一遍」与「18 原样转发」逐位不可区分 —— 那正是 AC-18-01② 要防的。
        /// </remarks>
        private sealed class SpySolver
        {
            private readonly int[] _outputQty;
            private readonly int[] _actualConsumed;
            private readonly int _outputQuality;

            public int CallCount { get; private set; }
            public RecipeSettlementRequest LastRequest { get; private set; }

            public SpySolver(int[] outputQty, int[] actualConsumed, int outputQuality)
            {
                _outputQty = outputQty;
                _actualConsumed = actualConsumed;
                _outputQuality = outputQuality;
            }

            /// <summary>可传给 <c>Settle</c> 的委托形。</summary>
            public RecipeSettlementFn Fn => Solve;

            private RecipeSettlementResult Solve(
                in RecipeSettlementRequest request, in RecipeSettlementConstants constants)
            {
                CallCount++;
                LastRequest = request;

                // 返回**持有的同一批数组实例** —— 夹具据此断 ReferenceEquals。
                //
                // ⚠️ 五个标量刻意取**两两互异且非零**的值(2026-10-27 评审修复轮):
                //   原先 Efficiency / QtyMultiplier = 1、EnvModTotal / SumOfModifiers = 0。
                //   后两个**恰等于 default(Fix)** ⇒ 「18 把出参换成 default」的变异前后同值,
                //   断言零判别力;而四个标量当时**压根没有断言**(`OutputQuality` 也零命中)。
                //   现取 1/2 · 3/2 · -1/4 · 1/8:两两互异 ⇒ 互换也抓得到;
                //   全非零 ⇒ 换成 default 必红。
                return new RecipeSettlementResult(
                    _outputQty, _outputQuality, FixParse.Parse("1/2"), _actualConsumed,
                    FixParse.Parse("3/2"), FixParse.Parse("-1/4"), FixParse.Parse("1/8"));
            }

            /// <summary>暴露持有的数组,供 ReferenceEquals 断言。</summary>
            public int[] OutputQtyRef => _outputQty;
            public int[] ActualConsumedRef => _actualConsumed;
        }

        // ══════════════════════════════════════════════════════════════════
        // 夹具工厂
        // ══════════════════════════════════════════════════════════════════

        /// <summary>造一条夹具配方。</summary>
        private static Recipe MakeRecipe()
            => new Recipe
            {
                RecipeId = "fx_process",
                Owner = RecipeOwner.Process,
                SkillGate = 5,
                MinQuality = 3,
                DurationTicks = 10,
                Inputs = new[] { new RecipeEntry(new ItemKey("willow_bark", ProcessingState.Raw), 2) },
                Outputs = new[] { new RecipeEntry(new ItemKey("salicylic", ProcessingState.Extracted), 1) },
                BoundaryState = new[] { new ProcessingTransition(ProcessingState.Raw, ProcessingState.Extracted) },
            };

        /// <summary>造夹具常量表(夹具值;满足 cap 和不等式)。</summary>
        private static RecipeSettlementConstants MakeConstants(
            string qtyMultMax = "5/2", string envModMax = "1/2", string skillModCap = "3/10")
            => new RecipeSettlementConstants(
                FixParse.Parse("1/2"),        // QtyMultMin
                FixParse.Parse(qtyMultMax),   // QtyMultMax
                FixParse.Parse(skillModCap),  // SkillModCap
                FixParse.Parse("1/10"),       // QualModCap
                FixParse.Parse("1/5"),        // EquipModCap
                FixParse.Parse("-1/2"),       // EnvModMin
                FixParse.Parse(envModMax),    // EnvModMax
                FixParse.Parse("1/2"),        // RetainMin
                FixParse.Parse("1"),          // RetainMax
                FixParse.Parse("1/2"),        // EffMin
                FixParse.Parse("1"),          // EffMax
                5,                            // MaxQuality(夹具值)
                60);                          // SkillCap(夹具值)

        /// <summary>造一份供料参数包(`recipe` 缺省 = 夹具配方)。</summary>
        /// <remarks>
        /// ⚠️ <c>Recipe</c> 是**值类型**(`Sim.Contracts/ItemDatabase/Recipe.cs:35`),故形参用
        /// <c>Recipe?</c> 而非 <c>Recipe = null</c> —— 后者编译不过。
        /// </remarks>
        private static ProcessingInputs Inputs(
            string climate = "-3/5", string clinic = "1/2", string equip = "1/5", Recipe? recipe = null)
            => new ProcessingInputs(
                recipe ?? MakeRecipe(), Actor, InQ,
                FixParse.Parse(equip), FixParse.Parse(climate), FixParse.Parse(clinic));

        private static SpySolver DefaultSpy()
            => new SpySolver(new[] { 2 }, new[] { 3 }, 4);

        // ══════════════════════════════════════════════════════════════════
        // AC-18-01 · 恰 1 次 + 同引用 + 零第二实现
        // ══════════════════════════════════════════════════════════════════

        /// <summary>① F1 恰被调用 1 次。</summary>
        [Test]
        public void test_ac1801_solverCalledExactlyOnce()
        {
            SpySolver spy = DefaultSpy();
            ProcessingSettlementInputs.Settle(Inputs(), MakeConstants(), new SpySkillLevels(Level), spy.Fn);

            Assert.That(spy.CallCount, Is.EqualTo(1), "F1 必须恰被调用 1 次");
        }

        /// <summary>② 出参与 F1 返回值为**同一引用**(零再加工)。</summary>
        /// <remarks>
        /// ⚠️ 用 <c>ReferenceEquals</c> 而**不是** <c>Is.EqualTo</c>:值相等允许了
        /// 「18 复制一份数组再填相同的数」—— 那正是 AC-18-01② 要防的再加工。
        /// </remarks>
        [Test]
        public void test_ac1801_outputArraysAreSameReference_notCopies()
        {
            SpySolver spy = DefaultSpy();
            RecipeSettlementResult result =
                ProcessingSettlementInputs.Settle(Inputs(), MakeConstants(), new SpySkillLevels(Level), spy.Fn);

            Assert.That(ReferenceEquals(result.OutputQty, spy.OutputQtyRef), Is.True,
                "OutputQty 必须是 F1 返回的同一数组实例(值相等不足以证明零加工)");
            Assert.That(ReferenceEquals(result.ActualConsumed, spy.ActualConsumedRef), Is.True,
                "ActualConsumed 必须是 F1 返回的同一数组实例");
        }

        /// <summary>② 补:**五个标量出参**也必须原样透传(值恒等)。</summary>
        /// <remarks>
        /// <para>⚠️ <b>这条是 2026-10-27 评审修复轮补的,补之前 AC-18-01② 只钉了 2/7 个出参</b> ——
        /// `OutputQuality` 在测试文件里**零命中**,四个 `Fix` 出参零断言。GDD `:717` 点名的三个出参
        /// (`consumed[]` / `produced[]` / `outQuality`)里,`outQuality` 当时完全没测。</para>
        /// <para><b>为什么是「值恒等」而非「同一引用」</b>:`OutputQuality` 是 <c>int</c>、
        /// 四个修正是 <c>Fix</c>(都是值类型),**没有引用可言**。AC-18-01② 的「同一引用」
        /// 对数组成立、对标量应读作「逐位相同、零再加工」。这是措辞层面的口径澄清,不是放宽 ——
        /// 若 18 把某个标量换成常量或 `default`,本条必红。</para>
        /// <para><b>为什么五个值两两互异</b>(见 <see cref="SpySolver"/> 的构造注释):
        /// 值相同则「拿错了字段」也照样绿。互异 + 全非零 ⇒ 互换与置零两种退化都抓得到。</para>
        /// </remarks>
        [Test]
        public void test_ac1801_scalarOutputsPassedThroughVerbatim()
        {
            SpySolver spy = DefaultSpy();
            RecipeSettlementResult result =
                ProcessingSettlementInputs.Settle(Inputs(), MakeConstants(), new SpySkillLevels(Level), spy.Fn);

            Assert.That(result.OutputQuality, Is.EqualTo(4),
                "OutputQuality 必须是 F1 给的值(GDD :717 点名的三个出参之一)");
            Assert.That(result.Efficiency.Raw, Is.EqualTo(FixParse.Parse("1/2").Raw),
                "Efficiency 必须原样透传,不得重算或置默认");
            Assert.That(result.QtyMultiplier.Raw, Is.EqualTo(FixParse.Parse("3/2").Raw),
                "QtyMultiplier 必须原样透传");
            Assert.That(result.EnvModTotal.Raw, Is.EqualTo(FixParse.Parse("-1/4").Raw),
                "EnvModTotal 必须原样透传(**负值保留** —— 18 不得 clamp,承 AC-18-06 同源纪律)");
            Assert.That(result.SumOfModifiers.Raw, Is.EqualTo(FixParse.Parse("1/8").Raw),
                "SumOfModifiers 必须原样透传(18 不得重算 ΣM)");
        }

        /// <summary>② 补:请求里的 Outputs / Inputs 也是配方的**同一引用**(不复制)。</summary>
        [Test]
        public void test_ac1801_requestArraysAreSameReferenceAsRecipe()
        {
            Recipe recipe = MakeRecipe();
            SpySolver spy = DefaultSpy();
            ProcessingSettlementInputs.Settle(
                Inputs(recipe: recipe), MakeConstants(), new SpySkillLevels(Level), spy.Fn);

            Assert.That(ReferenceEquals(spy.LastRequest.Outputs, recipe.Outputs), Is.True,
                "Outputs 必须直接转发配方的数组,不复制");
            Assert.That(ReferenceEquals(spy.LastRequest.Inputs, recipe.Inputs), Is.True,
                "Inputs 必须直接转发配方的数组,不复制");
        }

        /// <summary>③ 18 程序集内第二求解实现 = 零(符号扫描)。</summary>
        /// <remarks>
        /// <para>扫描面 = `Sim/Processing/**`(18 的系统目录)。判据 = 唯一求解器的
        /// **公式体标识符**一个都不出现在该目录里 —— 出现了就是「18 自建了第二份结算」。</para>
        /// <para>⚠️ <b>标识符表不是本文件自造的</b>(story §Implementation Notes 4:
        /// 「与 item-database story-003 AC-21a-6 同一扫描器,零第二份扫描基建」)——
        /// 本夹具**从权威件 `recipe_settlement_solver_test.cs` 解析**那张表。
        /// 权威件改表 ⇒ 本夹具自动跟随;权威件结构变了导致解析不出 ⇒ 本夹具**红**(不是静默零命中)。</para>
        /// <para>⚠️ 18 目录**没有**白名单条目,且必须没有 —— 见下一条独立断言。</para>
        /// </remarks>
        [Test]
        public void test_ac1801_noSecondSettlementImplementationInProcessingAssembly()
        {
            string[] canonical = CanonicalFormulaIdentifiers();
            Assert.That(canonical, Is.Not.Empty,
                "未能从权威件解析出公式体标识符表 —— 权威件结构变了?此时零命中不可信");

            List<string> hits = ScanProcessingSources(canonical);
            Assert.That(hits, Is.Empty,
                "18 侧不得出现任何 F1/F2 公式体标识符(第二求解实现):\n" + string.Join("\n", hits));

            // 扫描面自证:确认真的扫到了文件(否则「零命中」恒真)
            Assert.That(ProcessingSources().Count, Is.GreaterThanOrEqualTo(2),
                "扫描面应至少含 18 侧的两个生产文件 —— 少于 2 说明扫描面塌了");
        }

        /// <summary>18 目录不得出现在权威件的白名单里(白名单 = 豁免,豁免即门失明)。</summary>
        /// <remarks>
        /// 承 `WriterExistenceGate` 的「stale waiver is red」同源纪律:白名单条目是**豁免**,
        /// 而 AC-18-01③ 要的正是「18 侧零豁免」。若哪天有人为了让它变绿而把
        /// `Sim/Processing/...` 加进权威件白名单,本条转红。
        /// </remarks>
        [Test]
        public void test_ac1801_canonicalWhitelistHasNoProcessingEntry()
        {
            string canonicalSrc = StripComments(File.ReadAllText(Path.Combine(
                RepoRoot(), "unity", "Assets", "Tests", "PlayMode",
                "recipe_settlement_solver_test.cs")));

            Assert.That(canonicalSrc.Contains("Sim/Processing/"), Is.False,
                "权威件的白名单里出现了 Sim/Processing/ 条目 —— 18 侧被豁免了,AC-18-01③ 失效");
        }

        /// <summary>正控:把公式标识符注入合成源,扫描器必须命中(否则它什么都没扫到)。</summary>
        /// <remarks>
        /// ⚠️ 正控**走真链路**(源码文本 → `StripComments` → `IdentifierOccurrences`),
        /// 不是只喂字面量做 `IndexOf` —— 后者是本项目已抓过的「空洞正控」形态
        /// (S20-4:把判据的 `'*'` 改成 `'+'` 它照样绿)。
        /// </remarks>
        [Test]
        public void test_ac1801_scannerPositiveControl_detectsInjectedFormula()
        {
            string[] canonical = CanonicalFormulaIdentifiers();
            Assert.That(canonical, Is.Not.Empty, "正控需要权威标识符表,但解析为空");

            const string synthetic =
                "class X { void M() { var q = QtyMultiplier; var s = SumOfModifiers; } }";
            int hits = IdentifierOccurrences(StripComments(synthetic), canonical[0]).Count();

            Assert.That(hits, Is.GreaterThan(0),
                $"扫描器必须能命中注入的「{canonical[0]}」(正控)—— 否则上条零命中不可信");
        }

        // ══════════════════════════════════════════════════════════════════
        // AC-18-02 · 结算路径零算式
        // ══════════════════════════════════════════════════════════════════

        /// <summary>结算路径上 `Ceil` / `Round` / `Clamp` / 定点乘除 = 零。</summary>
        /// <remarks>
        /// 边界(豁免)= 装载校验 / 呈现换算 —— 18 侧的 `ValidateConstantTable` 属装载面,
        /// 但它**只委托**不运算,故仍应零命中;若将来真需豁免,须在本夹具的白名单里具名登记
        /// 并配「白名单条目仍活」断言(承 S20-4 的过期即红纪律)。
        /// </remarks>
        [Test]
        public void test_ac1802_noArithmeticInSettlementPath()
        {
            List<string> hits = ScanProcessingSources(ArithmeticIdentifiers);
            Assert.That(hits, Is.Empty,
                "18 结算路径不得含 Ceil/Round/Clamp/定点乘除(AC-18-02):\n" + string.Join("\n", hits));
        }

        /// <summary>正控:注入一处 `Clamp`,扫描器必须命中。</summary>
        /// <remarks>走真链路(`StripComments` → `IdentifierOccurrences`),非字面量 `IndexOf`。</remarks>
        [Test]
        public void test_ac1802_scannerPositiveControl_detectsInjectedArithmetic()
        {
            const string synthetic = "class X { Fix M(Fix a) { return Clamp(a); } }";
            int hits = IdentifierOccurrences(StripComments(synthetic), "Clamp(").Count();

            Assert.That(hits, Is.GreaterThan(0), "扫描器必须能命中注入的 Clamp(正控)");
        }

        /// <summary>另一半:只出现在**注释**里的算式符号不得命中(否则文档性提及伪造违例)。</summary>
        [Test]
        public void test_ac1802_arithmeticInCommentOnly_doesNotHit()
        {
            const string onlyComment =
                "class X { Fix M(Fix a) {\n  // 此处不 Clamp(也不 Round(\n  return a;\n} }";
            string stripped = StripComments(onlyComment);

            int hits = 0;
            foreach (string id in ArithmeticIdentifiers)
                hits += IdentifierOccurrences(stripped, id).Count();

            Assert.That(hits, Is.EqualTo(0),
                "注释里的算式符号不得命中 —— 否则解释性注释会伪造 AC-18-02 违例");
        }

        // ══════════════════════════════════════════════════════════════════
        // AC-18-03 · 传等级不传结果
        // ══════════════════════════════════════════════════════════════════

        /// <summary>`L` 入参恒等于同一次 `QueryLevel` 的返回值(逐位)。</summary>
        [Test]
        public void test_ac1803_skillLevelPassedThrough_equalsQueryResult()
        {
            SpySkillLevels levels = new SpySkillLevels(Level);
            SpySolver spy = DefaultSpy();
            ProcessingSettlementInputs.Settle(Inputs(), MakeConstants(), levels, spy.Fn);

            Assert.That(spy.LastRequest.CraftSkillLevel, Is.EqualTo(Level),
                "L 入参必须 == QueryLevel 的返回值(逐位)");
            Assert.That(levels.CallCount, Is.EqualTo(1), "等级恰查一次");
        }

        /// <summary>`L` 不是「换算后的结果」—— 多个不同等级逐一透传,无一被改写。</summary>
        /// <remarks>
        /// ⚠️ 这条是 AC-18-03 的**判别力所在**:只测一个等级时,「原样透传」与
        /// 「某个恰好等于它的换算」逐位不可区分。多档取值 + 断言全等,才把「预除」逼出来
        /// (GDD :508-514 的失败形态:`Level &lt; SKILL_CAP` 时整数除法会压成 0)。
        /// </remarks>
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(30)]
        [TestCase(59)]
        [TestCase(60)]
        public void test_ac1803_levelNotPreDivided_anyLevelPassesThroughVerbatim(int level)
        {
            SpySolver spy = DefaultSpy();
            ProcessingSettlementInputs.Settle(
                Inputs(), MakeConstants(), new SpySkillLevels(level), spy.Fn);

            Assert.That(spy.LastRequest.CraftSkillLevel, Is.EqualTo(level),
                $"等级 {level} 必须原样进入 F1 —— 任何换算都会在这里露馅");
        }

        /// <summary>查的是**炮制**技能,不是别的技能 id。</summary>
        [Test]
        public void test_ac1803_queriesCraftSkill_notAnyOther()
        {
            SpySkillLevels levels = new SpySkillLevels(Level);
            ProcessingSettlementInputs.Settle(Inputs(), MakeConstants(), levels, DefaultSpy().Fn);

            Assert.That(levels.QueriedSkillIds, Is.EqualTo(new[] { CraftSkillLiteral }),
                "必须查炮制技能(字面量 7;不引用被测常量以免自指)");
        }

        /// <summary>`CraftSkillId` 与 `SkillId.炮制` 逐位一致(双向钉)。</summary>
        [Test]
        public void test_ac1803_craftSkillId_matchesRegistryEnum()
        {
            Assert.That(ProcessingSettlementInputs.CraftSkillId, Is.EqualTo((int)SkillId.炮制));
            Assert.That(ProcessingSettlementInputs.CraftSkillId, Is.EqualTo(CraftSkillLiteral));
        }

        /// <summary>查的是**本玩家**的等级 —— `ActorId` 原样进入查询,不被替换。</summary>
        /// <remarks>
        /// 承 AC-18-03 同一纪律:等级必须来自「这个玩家」。若实现误传 `0` 或别的 id,
        /// 等级仍是 `QueryLevel` 的返回值(上一条照样绿)—— 但查的是**另一个人**的技能。
        /// </remarks>
        [Test]
        public void test_ac1803_queriesTheInputActor_notAnyOther()
        {
            SpySkillLevels levels = new SpySkillLevels(Level);
            ProcessingSettlementInputs.Settle(Inputs(), MakeConstants(), levels, DefaultSpy().Fn);

            Assert.That(levels.QueriedActorIds, Is.EqualTo(new[] { Actor }),
                "必须查入参玩家的等级 —— 查错人时上一条的恒等断言不会红");
        }

        // ══════════════════════════════════════════════════════════════════
        // AC-18-04 · 不认识药
        // ══════════════════════════════════════════════════════════════════

        /// <summary>`drug_profile` / `polarity` / `treatable_by` 读取 = 零。</summary>
        [Test]
        public void test_ac1804_neverReadsDrugIdentitySymbols()
        {
            List<string> hits = ScanProcessingSources(DrugIdentityIdentifiers);
            Assert.That(hits, Is.Empty,
                "18 不认识药(规则九)—— 不得读取药物档案符号:\n" + string.Join("\n", hits));
        }

        /// <summary>正控:注入 `polarity` 读取,扫描器必须命中。</summary>
        /// <remarks>走真链路(`StripComments` → `IdentifierOccurrences`),非字面量 `IndexOf`。</remarks>
        [Test]
        public void test_ac1804_scannerPositiveControl_detectsInjectedDrugSymbol()
        {
            const string synthetic = "class X { int M() { return p.polarity; } }";
            int hits = IdentifierOccurrences(StripComments(synthetic), "polarity").Count();

            Assert.That(hits, Is.GreaterThan(0), "扫描器必须能命中注入的 polarity(正控)");
        }

        // ══════════════════════════════════════════════════════════════════
        // AC-18-06 · 两 EnvMod 分量原样透传
        // ══════════════════════════════════════════════════════════════════

        /// <summary>两分量各自越界 ⇒ 逐位原样透传(负值保留,禁 clamp 到 0)。</summary>
        /// <remarks>
        /// 夹具刻意让 climate = 2×ENV_MOD_MAX、clinic = 2×ENV_MOD_MIN(越界),且**一正一负**
        /// —— 若 18 侧做了「防御性 clamp」或「相加」,任一动作都会让入参与输入不等。
        /// </remarks>
        [TestCase("-1", "1/2")]      // climate 越下限、clinic 在上界内
        [TestCase("1", "-1/2")]      // climate 越上限、clinic 越下限
        [TestCase("-2", "-2")]       // 两分量同号越界
        [TestCase("3/2", "-3/2")]    // 一正一负、均越界
        public void test_ac1806_envModComponentsPassedThroughVerbatim(string climate, string clinic)
        {
            Fix climateFix = FixParse.Parse(climate);
            Fix clinicFix = FixParse.Parse(clinic);

            SpySolver spy = DefaultSpy();
            ProcessingSettlementInputs.Settle(
                Inputs(climate: climate, clinic: clinic), MakeConstants(),
                new SpySkillLevels(Level), spy.Fn);

            Assert.That(spy.LastRequest.EnvModClimate.Raw, Is.EqualTo(climateFix.Raw),
                "EnvMod_climate 必须逐位原样透传(零钳制零加法)");
            Assert.That(spy.LastRequest.EnvModClinic.Raw, Is.EqualTo(clinicFix.Raw),
                "EnvMod_clinic 必须逐位原样透传(零钳制零加法)");
        }

        /// <summary>EquipMod 同样原样透传(24 只读,18 不合成数值)。</summary>
        [Test]
        public void test_ac1806_equipModPassedThroughVerbatim()
        {
            SpySolver spy = DefaultSpy();
            ProcessingSettlementInputs.Settle(
                Inputs(equip: "7/5"), MakeConstants(), new SpySkillLevels(Level), spy.Fn);

            Assert.That(spy.LastRequest.EquipMod.Raw, Is.EqualTo(FixParse.Parse("7/5").Raw),
                "EquipMod 必须原样透传");
        }

        /// <summary>`InQ` 原样进入请求(归 20 供给,18 不改写、不自行取 min)。</summary>
        /// <remarks>
        /// ⚠️ 这是 F-18.2 BL-9 补注① 的静默失效形态:18 若「顺手」从 `Recipe.Inputs` 行取
        /// min 品级,值往往**恰好也合法** ⇒ 没有断言就看不出它换了源。多档取值逐一钉住。
        /// </remarks>
        [TestCase(1)]
        [TestCase(3)]
        [TestCase(5)]
        public void test_ac1806_inputQualityPassedThroughVerbatim(int inQ)
        {
            var inputs = new ProcessingInputs(
                MakeRecipe(), Actor, inQ,
                FixParse.Parse("0"), FixParse.Parse("0"), FixParse.Parse("0"));
            SpySolver spy = DefaultSpy();
            ProcessingSettlementInputs.Settle(
                inputs, MakeConstants(), new SpySkillLevels(Level), spy.Fn);

            Assert.That(spy.LastRequest.InputQuality, Is.EqualTo(inQ),
                "InQ 必须原样透传 —— 18 不得自行从配方行取 min(那会换掉 20 的供料)");
        }

        // ══════════════════════════════════════════════════════════════════
        // AC-18-14 · 装载期防御硬失败
        // ══════════════════════════════════════════════════════════════════

        /// <summary>违反 cap 和不等式的常量表 ⇒ **硬失败**(`throw`,非 `Debug.Assert`)。</summary>
        /// <remarks>
        /// 夹具值:`QtyMultMax = 3/2`(右端 − 1 = 1/2)、`SkillModCap = 3/10` + `QualModCap 1/10`
        /// + `EquipModCap 1/5` = 3/5、`EnvModMax = 1/2` ⇒ 左端 = 11/10 &gt; 1/2 ⇒ 必抛。
        /// </remarks>
        [Test]
        public void test_ac1814_inconsistentCapTable_throwsHard()
        {
            RecipeSettlementConstants bad = MakeConstants(
                qtyMultMax: "3/2", envModMax: "1/2", skillModCap: "3/10");

            Assert.Throws<InvalidOperationException>(
                () => ProcessingSettlementInputs.ValidateConstantTable(bad),
                "破 cap 和的常量表必须硬失败(AC-18-14 的 18 侧半边)");
        }

        /// <summary>**与 21a 共用同一个负向夹具** —— story §QA Test Cases 明写的「两边同夹具双跑」。</summary>
        /// <remarks>
        /// <para>⚠️ <b>这条是 2026-10-27 评审修复轮补的缺口。</b>story `:97` 原文:
        /// 「等号临界过;主执行(21a 烘焙)在另一侧,**两边同夹具双跑**
        /// (镜像 AC-21a-9/3 共用 `invalid_cap_sum.json` 先例)」。修复前本文件**全是自建表**,
        /// 对 `tests/unit/item_database/fixtures/invalid_cap_sum.json` **零引用** ——
        /// 「双跑」这一句没有落地。</para>
        /// <para><b>为什么共用夹具比自建表强</b>:自建表只证「18 侧能拒某种坏表」;
        /// 共用夹具证「18 侧与 21a 侧拒的是**同一张**坏表」—— 两边的判据一旦漂移
        /// (比如一边漏算 `max(0, ENV_MOD_MAX)`),双跑会立刻不同步。该夹具的设计点正是
        /// 「三个 cap 全小(和 3/4 ≤ 1,单独不超),仅 EnvModMax 把它推过界」
        /// ⇒ 漏算 EnvMod 分量的实现会**放过**它。</para>
        /// <para><b>读的是同一份文件,不是复制一份数值</b> —— 复制会让两边各自漂移而测试仍绿。</para>
        /// </remarks>
        [Test]
        public void test_ac1814_sharedNegativeFixture_sameTableAs21a()
        {
            string path = Path.Combine(
                RepoRoot(), "tests", "unit", "item_database", "fixtures", "invalid_cap_sum.json");
            Assert.That(File.Exists(path), Is.True,
                "共用负向夹具缺失(21a story-006 QA 指定):" + path);

            // 从 JSON 解析出常量表 —— 字段名与夹具 schema 一一对应(全为 Fix 字符串,承 ADR-014)。
            string json = File.ReadAllText(path);
            Fix Cap(string key) => FixParse.Parse(JsonString(json, key));

            var shared = new RecipeSettlementConstants(
                FixParse.Parse(JsonString(json, "qty_mult_min")),
                FixParse.Parse(JsonString(json, "qty_mult_max")),
                Cap("skill_mod_cap"),
                Cap("qual_mod_cap"),
                Cap("equip_mod_cap"),
                FixParse.Parse(JsonString(json, "env_mod_min")),
                FixParse.Parse(JsonString(json, "env_mod_max")),
                FixParse.Parse("1/4"),   // retain_min  —— 夹具未含(不参与 AC-21a-3),取合法值
                FixParse.Parse("1"),     // retain_max
                FixParse.Parse("1/4"),   // eff_min     —— 同上
                FixParse.Parse("1"),     // eff_max
                3,                       // max_quality —— 同上
                60);                     // skill_cap   —— 同上

            // 夹具自证:三个 cap 之和单独**不**超界 —— 只有 EnvModMax 能把式子推过去。
            // 缺了这一步,「18 拒了」就可能是因为别的原因,共用夹具的意义随之落空。
            long capsOnly = shared.SkillModCap.Raw + shared.QualModCap.Raw + shared.EquipModCap.Raw;
            Assert.That(capsOnly, Is.LessThanOrEqualTo(shared.QtyMultMax.Raw - Fix.OneRaw),
                "夹具自证:三 cap 和单独不超界(该夹具的设计点 = 仅 EnvModMax 推过界)");
            Assert.That(shared.EnvModMax.Raw, Is.GreaterThan(0L),
                "夹具自证:EnvModMax 必须为正 —— 否则 max(0,·) 项消失,本夹具退化成「cap 和超界」");

            Assert.Throws<InvalidOperationException>(
                () => ProcessingSettlementInputs.ValidateConstantTable(shared),
                "18 侧必须与 21a 侧**拒同一张**坏表(共用 invalid_cap_sum.json)");
        }

        /// <summary>从扁平 JSON 里取一个字符串字段的值(夹具 schema 极简,无需完整解析器)。</summary>
        private static string JsonString(string json, string key)
        {
            int at = json.IndexOf("\"" + key + "\"", StringComparison.Ordinal);
            Assert.That(at, Is.GreaterThanOrEqualTo(0), $"夹具缺字段 {key}");
            int colon = json.IndexOf(':', at);
            int firstQuote = json.IndexOf('"', colon + 1);
            int secondQuote = json.IndexOf('"', firstQuote + 1);
            Assert.That(secondQuote, Is.GreaterThan(firstQuote), $"字段 {key} 的值不是字符串");
            return json.Substring(firstQuote + 1, secondQuote - firstQuote - 1);
        }

        /// <summary>等号临界(左端 == 右端)**逐位相等**时必须过(判据是 ≤,不是 &lt;)。</summary>
        /// <remarks>
        /// ⚠️ <b>临界值必须在 raw 域精确构造,不能靠十进制直觉</b> ——
        /// <c>FixParse</c> 走 <c>ROUND_HALF_AWAY_FROM_ZERO</c>,<c>3/10</c> 这类分数的 raw 是
        /// 舍入后的整数,「0.3+0.1+0.2 = 0.6」在 raw 域**并不成立**(实为 52429 ≠ 39322)。
        /// 故此处全取 2 的幂分母,raw 精确:
        /// 左端 = SkillModCap 1/2(32768)+ QualModCap 0 + EquipModCap 1/4(16384)
        ///        + max(0, EnvModMax 1/4)(16384) = **65536**;
        /// 右端 = QtyMultMax 2(131072)− OneRaw(65536) = **65536** ⇒ 恰等。
        /// </remarks>
        [Test]
        public void test_ac1814_boundaryEqualityPasses()
        {
            RecipeSettlementConstants edge = new RecipeSettlementConstants(
                FixParse.Parse("1/2"),     // QtyMultMin
                FixParse.Parse("2"),       // QtyMultMax  ⇒ 右端 65536
                FixParse.Parse("1/2"),     // SkillModCap 32768
                FixParse.Parse("0"),       // QualModCap  0
                FixParse.Parse("1/4"),     // EquipModCap 16384
                FixParse.Parse("-1/2"),    // EnvModMin
                FixParse.Parse("1/4"),     // EnvModMax   16384 ⇒ 左端 65536
                FixParse.Parse("1/2"), FixParse.Parse("1"), FixParse.Parse("1/2"), FixParse.Parse("1"),
                5, 60);

            Assert.DoesNotThrow(() => ProcessingSettlementInputs.ValidateConstantTable(edge),
                "左端恰等于右端时应当通过(判据是 ≤,不是 <)");
        }

        /// <summary>临界值**减一档**必须抛 —— 证明上一条的绿不是「怎么都过」。</summary>
        /// <remarks>
        /// 上一条若因扫描/比较写错而恒过,本条的失败态会把它揭穿:
        /// 把 <c>QtyMultMax</c> 从 <c>2</c> 降到 <c>1</c>,右端 = 0 &lt; 左端 65536 ⇒ 必抛。
        /// </remarks>
        [Test]
        public void test_ac1814_oneStepBelowBoundaryThrows()
        {
            RecipeSettlementConstants below = new RecipeSettlementConstants(
                FixParse.Parse("1/2"), FixParse.Parse("1"), FixParse.Parse("1/2"),
                FixParse.Parse("0"), FixParse.Parse("1/4"),
                FixParse.Parse("-1/2"), FixParse.Parse("1/4"),
                FixParse.Parse("1/2"), FixParse.Parse("1"), FixParse.Parse("1/2"), FixParse.Parse("1"),
                5, 60);

            Assert.Throws<InvalidOperationException>(
                () => ProcessingSettlementInputs.ValidateConstantTable(below),
                "右端低于左端一档必须抛 —— 否则「临界过」与「怎么都过」不可区分");
        }

        /// <summary>自洽常量表不抛(负样例的对侧 —— 防「永远抛」蒙混过关)。</summary>
        [Test]
        public void test_ac1814_consistentTableDoesNotThrow()
        {
            Assert.DoesNotThrow(
                () => ProcessingSettlementInputs.ValidateConstantTable(MakeConstants()),
                "自洽的常量表不得抛 —— 否则本判据对「永远抛」的实现零判别力");
        }

        /// <summary>判据本体**委托**唯一实现,不重抄算术(禁第二份不等式定义)。</summary>
        [Test]
        public void test_ac1814_delegatesToSinglePredicate_noDuplicatedInequality()
        {
            // 若 18 侧重抄了不等式,源码里会出现 Positive / PositiveCapsRaw 之类的私有求和助手。
            List<string> hits = ScanProcessingSources(new[] { "PositiveCapsRaw", "Positive(" });
            Assert.That(hits, Is.Empty,
                "18 侧不得重抄 cap 求和算术(判据本体归 RecipeSettlementConstantTableValidator):\n"
                + string.Join("\n", hits));
        }

        // ══════════════════════════════════════════════════════════════════
        // 边界:fail-closed
        // ══════════════════════════════════════════════════════════════════

        /// <summary>缺配方的两个数组 / 缺查询 ⇒ 抛,不静默继续。</summary>
        /// <remarks>
        /// ⚠️ 「缺配方」的形态是 **<c>default(Recipe)</c>**(值类型,没有 null 态),
        /// 它使 <c>Outputs</c> / <c>Inputs</c> 双双为 <c>null</c>。另两个用例分别只缺一个数组
        /// —— 若实现只挡其中一个,这两条会各自抓到。
        /// </remarks>
        [Test]
        public void test_missingRecipeArraysOrQuery_failClosed()
        {
            var noArrays = new ProcessingInputs(default, Actor, InQ,
                FixParse.Parse("0"), FixParse.Parse("0"), FixParse.Parse("0"));

            Assert.Throws<ArgumentNullException>(
                () => ProcessingSettlementInputs.Assemble(noArrays, new SpySkillLevels(Level)),
                "default(Recipe) ⇒ 两数组皆 null ⇒ 必须拒");

            // ⚠️ 下面两条**各自只打一条守卫**(2026-10-27 评审 A-5 的判据来源):
            //   `default(Recipe)` 那条只证「至少有一条守卫在」—— 它先撞 Outputs 守卫,
            //   Inputs 守卫不可达。要证两条**各自**承重,必须构造「只缺一半」的配方。
            //   这两条正是 `_note` ⓪④ 所说「停用任一条另一条仍抛」的反面证据。
            Recipe onlyOutputs = MakeRecipe();
            onlyOutputs.Inputs = null;
            ArgumentNullException onlyOutEx = Assert.Throws<ArgumentNullException>(
                () => ProcessingSettlementInputs.Assemble(
                    Inputs(recipe: onlyOutputs), new SpySkillLevels(Level)),
                "只有 Outputs、Inputs 为 null ⇒ 必须拒(不得当成空投入继续)");
            Assert.That(onlyOutEx.ParamName, Does.Contain("Inputs"),
                "异常须点名**哪个数组**为 null —— 否则栈追踪分不出是哪条守卫(评审 A-4)");

            Recipe onlyInputs = MakeRecipe();
            onlyInputs.Outputs = null;
            ArgumentNullException onlyInEx = Assert.Throws<ArgumentNullException>(
                () => ProcessingSettlementInputs.Assemble(
                    Inputs(recipe: onlyInputs), new SpySkillLevels(Level)),
                "只有 Inputs、Outputs 为 null ⇒ 必须拒(不得当成零产出继续)");
            Assert.That(onlyInEx.ParamName, Does.Contain("Outputs"),
                "异常须点名**哪个数组**为 null —— 否则栈追踪分不出是哪条守卫(评审 A-4)");

            Assert.Throws<ArgumentNullException>(
                () => ProcessingSettlementInputs.Assemble(Inputs(), null),
                "缺等级查询 ⇒ 必须拒(否则等于以零级结算,AC-18-03 的静默失效)");
        }

        /// <summary>缺求解器 ⇒ 抛,不返回 default 结果。</summary>
        [Test]
        public void test_nullSolver_failClosed()
        {
            Assert.Throws<ArgumentNullException>(
                () => ProcessingSettlementInputs.Settle(
                    Inputs(), MakeConstants(), new SpySkillLevels(Level), null));
        }

        /// <summary>`default(ProcessingInputs)` 不得被当成「配方为空但可跑」放行。</summary>
        /// <remarks>
        /// 承 S20-4 的 `default(struct)` 教训:`readonly struct` 的构造器守卫对
        /// `default(T)` **不生效**(它绕过构造器)。fail-closed 必须在**消费侧**再挡一次。
        /// </remarks>
        [Test]
        public void test_defaultStruct_failsClosedAtConsumptionSite()
        {
            ProcessingInputs empty = default;
            Assert.Throws<ArgumentNullException>(
                () => ProcessingSettlementInputs.Assemble(empty, new SpySkillLevels(Level)),
                "default(ProcessingInputs) 的 Recipe 两数组皆 null ⇒ 消费侧必须拒");
        }

        // ══════════════════════════════════════════════════════════════════
        // 纯函数性(可重放前提)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>同输入多次求值 ⇒ 同入参(零跨调用状态)。</summary>
        [Test]
        public void test_pureFunction_sameInputSameRequest()
        {
            Recipe recipe = MakeRecipe();
            SpySolver first = DefaultSpy();
            SpySolver second = DefaultSpy();

            ProcessingSettlementInputs.Settle(
                Inputs(recipe: recipe), MakeConstants(), new SpySkillLevels(Level), first.Fn);
            ProcessingSettlementInputs.Settle(
                Inputs(recipe: recipe), MakeConstants(), new SpySkillLevels(Level), second.Fn);

            Assert.That(second.LastRequest.CraftSkillLevel, Is.EqualTo(first.LastRequest.CraftSkillLevel));
            Assert.That(second.LastRequest.InputQuality, Is.EqualTo(first.LastRequest.InputQuality));
            Assert.That(second.LastRequest.EnvModClimate.Raw, Is.EqualTo(first.LastRequest.EnvModClimate.Raw));
            Assert.That(second.LastRequest.EnvModClinic.Raw, Is.EqualTo(first.LastRequest.EnvModClinic.Raw));
            Assert.That(second.LastRequest.EquipMod.Raw, Is.EqualTo(first.LastRequest.EquipMod.Raw));

            // ⚠️ 2026-10-27 补:两个**数组**此前无断言(只比了 5 个标量)。
            //    两次调用传的是**同一个** recipe 实例 ⇒ 18 若不复制,两次 request 的数组引用必然相同。
            //    若 18 每次 clone,本条红 —— 而那正是 AC-18-01② 的「零再加工」被破坏。
            Assert.That(ReferenceEquals(second.LastRequest.Outputs, first.LastRequest.Outputs), Is.True,
                "同配方两次结算 ⇒ Outputs 必须是同一数组引用(18 不得每次 clone —— AC-18-01②)");
            Assert.That(ReferenceEquals(second.LastRequest.Inputs, first.LastRequest.Inputs), Is.True,
                "同配方两次结算 ⇒ Inputs 必须是同一数组引用(18 不得每次 clone —— AC-18-01②)");
        }

        /// <summary><c>Settle</c> **不重复判准入** —— 它无条件结算,拒绝与否由调用方在调它之前决定。</summary>
        /// <remarks>
        /// <para>⚠️ <b>本条在 2026-10-27 评审修复轮被**整条重写**,原因见下。</b></para>
        /// <para><b>原形态是恒真断言(两位评审独立抓到)</b>:旧版本先 <c>SpySolver spy = DefaultSpy();</c>
        /// 再断 <c>spy.CallCount == 0</c> —— 一个刚 new 出来、从未传给任何东西的 spy,
        /// 计数**定义上**就是 0。整步不碰任何生产码,把 <c>Settle</c> 整个删掉它也照样绿。
        /// 测试名承诺「失败路径 ⇒ 不调 F1」,而代码里**根本没构造失败路径**
        /// (没调 <c>AdmissiblePredicate.Judge</c>,没造 <c>AdmissionRejection</c>)。
        /// 这与本项目「自造断言顶替 AC 语义层」的病灶同型。</para>
        /// <para><b>改成了什么</b>:<c>Settle</c> 的真实契约是「**我不判准入**」——
        /// 准入是 Story 001 的 <c>AdmissiblePredicate</c>,调用方先判再调。故可测的形态是:
        /// 构造一个**必然被准入拒绝**的输入(技能 0 级、配方门槛拉高),直接调 <c>Settle</c>,
        /// 断言它**照样把活干了**(F1 恰被调 1 次)—— 因为「会不会被拒」不归它管。
        /// 若将来有人把准入判定搬进 18(第二实现),这条会红。</para>
        /// <para><b>仍未覆盖的半边(如实登记,不记绿)</b>:「调用方在准入不过时**不调** <c>Settle</c>」
        /// 这一句需要 Story 001 ↔ 002 的**接线**存在才能测。当前 <c>ProcessingSettlementInputs</c>
        /// 全仓零生产调用点(见 <c>test_ac1801_hasProductionCallSite</c>),故该半边是
        /// <b>NOT-RUN</b>,不是 PASS。</para>
        /// </remarks>
        [Test]
        public void test_ac1801_settleDoesNotRejudgeAdmission()
        {
            // 构造一个**必然被准入拒绝**的输入:技能等级 0,而夹具配方的 skill_gate 高。
            // 若 Settle 内部偷偷判了准入,它要么不调 F1、要么抛 —— 两种都会被下面抓到。
            Recipe gated = MakeRecipe();
            SpySolver spy = DefaultSpy();

            RecipeSettlementResult result = ProcessingSettlementInputs.Settle(
                Inputs(recipe: gated), MakeConstants(), new SpySkillLevels(0), spy.Fn);

            // ⚠️ 断言是 `> 0` 而**不是** `== 1`(2026-10-27 第 5 轮证伪轮订正)。
            //   本测的属性是「Settle **不判准入**」—— 只要它照常干活即可。
            //   「恰 1 次」是 AC-18-01① 的属性,由 `test_ac1801_solverCalledExactlyOnce`
            //   与 IL 面那条钉。写成 `== 1` 会让本测**兼职**别人的属性:
            //   M1(调两次)一施加,它红得与本测属性无关 —— 这正是我在
            //   `test_ac1801_failurePath_neverCallsSolver` 上刚犯过、并已订正的同一个错。
            Assert.That(spy.CallCount, Is.GreaterThan(0),
                "Settle 不判准入 ⇒ 即使技能 0 级它也照常结算" +
                "(判准入是调用方的事;若此处变成 0 次,说明 18 偷偷判了准入 = 第二实现)");
            Assert.That(result.OutputQty, Is.Not.Null,
                "Settle 必须返回真结果,不得因「准入不过」而返回 default");
        }

        /// <summary>生产侧必须**有**调用点 —— 否则本文件的 51 条断言测的是一个没人用的 API。</summary>
        /// <remarks>
        /// <para>⚠️ <b>本测的方向在 2026-10-27 翻转了(S20-13 / processing story-003 接线轮)</b>,
        /// 依据正是它自己原来那句「Story 003 接线后此断言须翻转」:</para>
        /// <list type="bullet">
        /// <item><b>翻转前</b>(S20-8 评审 Q-3 补):断言 <c>Is.Empty</c>,钉住「生产侧零调用点」
        /// 这一**当前事实**,把「未接线」从口头已知变成机械可观测。</item>
        /// <item><b>翻转后</b>(本批):断言 <c>Is.Not.Empty</c>,并要求**点名**那条调用点文件。
        /// 判据从「当下没有」变成「必须一直有」—— 谁把接线删了,本测立刻红。</item>
        /// </list>
        /// <para>⚠️ <b>本测变绿**不等于** AC-18-01② 的「交给 20」腿已覆盖</b>:
        /// 现在的调用方是 `CraftIgnitionTransaction`(18 自己的点火事务),它把出参整体转发给
        /// **写者** `CraftEventWriter`;20 侧的接收者(`Apply`)归 inventory-005(S20-14)。
        /// ⇒ AC-18-01② 的记分维持 **半绿**(7 个出参透传 ✅ / 「交给 20」腿 NOT-RUN),
        /// 翻转判据的是「18 侧有真调用点」,不是「20 侧有人接」。</para>
        /// </remarks>
        [Test]
        public void test_ac1801_hasProductionCallSite()
        {
            string simRoot = Path.Combine(RepoRoot(), "unity", "Assets", "Sim");
            string[] files = Directory.GetFiles(simRoot, "*.cs", SearchOption.AllDirectories);
            Assert.That(files.Length, Is.GreaterThan(50),
                "扫描面自证:Sim 下应扫到大量 .cs —— 否则下面的「零命中」是空转");

            List<string> callSites = new List<string>();
            foreach (string f in files)
            {
                // 生产码定义处本身不算调用点。
                if (f.EndsWith("ProcessingSettlementInputs.cs", StringComparison.Ordinal)) continue;
                string src = StripComments(File.ReadAllText(f));
                if (src.Contains("ProcessingSettlementInputs"))
                    callSites.Add(f.Substring(simRoot.Length).TrimStart('/', '\\'));
            }

            // ① 必须**至少**有一个生产调用点(接线已落)。
            Assert.That(callSites, Is.Not.Empty,
                "生产侧零调用点 —— F-18.1 的供料 / 结算无人使用,AC-18-01② 的「18 交给 20」" +
                "结构上无载体。Story 003 的接线(`CraftIgnitionTransaction`)被删了?");

            // ② 点名:点火事务必须是其中之一(否则「有调用点」可能来自某个不相干的探针)。
            Assert.That(callSites, Has.Some.Contains("CraftIgnitionTransaction"),
                "调用点清单里没有点火事务 —— 接线被移到别处 / 被删。实得:" +
                string.Join(", ", callSites));
        }

        /// <summary>重试两次 = 两次**独立**调用(spy 各计 1,结果实例不同 —— 无幂等去重、无缓存)。</summary>
        /// <remarks>
        /// <para>story §QA Test Cases 明写「重试两次 = 两次独立调用(允许)」。这条钉的是:
        /// 18 侧**没有**任何「同输入去重 / 结果缓存」逻辑 —— 那会让第二次点火白烧而不扣料。</para>
        /// <para>⚠️ 两次用**不同**的 spy 实例分别调用,再断言各自计数都是 1 —— 这比「同一个 spy
        /// 数到 2」更强:它同时证明「无跨调用状态」(纯函数性)。</para>
        /// </remarks>
        [Test]
        public void test_ac1801_retryTwice_isTwoIndependentCalls()
        {
            SpySolver first = DefaultSpy();
            ProcessingSettlementInputs.Settle(Inputs(), MakeConstants(), new SpySkillLevels(Level), first.Fn);

            SpySolver second = DefaultSpy();
            ProcessingSettlementInputs.Settle(Inputs(), MakeConstants(), new SpySkillLevels(Level), second.Fn);

            Assert.That(first.CallCount, Is.EqualTo(1), "第一次点火 ⇒ 第一次结算恰 1 次");
            Assert.That(second.CallCount, Is.EqualTo(1), "第二次点火 ⇒ 第二次结算恰 1 次(独立计数)");
            Assert.That(ReferenceEquals(first.OutputQtyRef, second.OutputQtyRef), Is.False,
                "两次调用必须产出**不同**的数组实例 —— 同一实例意味着结果被缓存复用(第二次没真烧)");
        }

        // ══════════════════════════════════════════════════════════════════
        // IL 面:AC-18-01③ / 02 / 04 的**符号引用**扫描(GDD :717-718 原文要求)
        // ══════════════════════════════════════════════════════════════════
        //
        // 为什么需要这一层(而不是满足于上面的文本扫描):
        //   GDD AC-18-01③ / AC-18-02 / AC-18-04 的判据原文写的是
        //   「**Roslyn/IL 符号引用扫描,非 grep 字符串匹配**」(:717 / :718)。
        //   上面那组 `StripComments` + 词边界扫描比朴素 grep 强(注释不误报),
        //   但**仍不是符号引用**:它看不见「换个写法绕过」——
        //   把 `cap.Raw * bounded` 改成 `Fix.Multiply(cap, bounded)` 就整条逃逸,
        //   把 `Clamp(x)` 换成 `Bound(x)` 也一样。
        //   IL 层看见的是真实的 call 目标与 mul/div 指令,那才是符号引用。
        //
        // ⚠️ 两层**不互替**:文本层能看见 `drug_profile` 这类**字段/枚举名**的字面出现
        //   (IL 里那是 `ldfld` 的操作数,需另一套解析);IL 层能看见**算术指令与调用目标**。
        //   故两者并列,各证一半。

        /// <summary>AC-18-01③ 符号面:`Settle` 体内对注入委托的 `Invoke` **恰 1 次**。</summary>
        /// <remarks>
        /// 运行时 spy 计数证的是**行为**;IL 证的是**代码里只有一处调用**。两者不可互替:
        /// 运行时计数漏得掉「某个分支里调了两次」(只要夹具不走那个分支),IL 漏得掉「循环里调一次」。
        /// </remarks>
        [Test]
        public void test_ac1801_il_settleInvokesSolverExactlyOnce()
        {
            int n = CountCallsInType(SimDll(), ProcessingNs,
                                     nameof(ProcessingSettlementInputs),
                                     nameof(ProcessingSettlementInputs.Settle),
                                     "RecipeSettlementFn");
            Assert.That(n, Is.EqualTo(1),
                "Settle 体内对 RecipeSettlementFn.Invoke 的 call 必须恰 1 处(符号级「恰一次」)");
        }

        /// <summary>AC-18-01③ 符号面:18 侧**零**对唯一求解器的直接调用(第二实现 = 零)。</summary>
        /// <remarks>
        /// 若出现 `call RecipeSettlementSolver::Solve`,说明 18 绕过了注入的委托自己调了求解器 ——
        /// 那会让 spy 数到 1 而真求解器还被调了一次,「恰一次」就此失真。
        /// </remarks>
        [Test]
        public void test_ac1801_il_noDirectCallToUniqueSolver()
        {
            foreach (string m in ProcessingEntryPoints)
            {
                int n = CountCallsInType(SimDll(), ProcessingNs,
                                         nameof(ProcessingSettlementInputs), m,
                                         "RecipeSettlementSolver");
                Assert.That(n, Is.EqualTo(0),
                    $"{m} 体内出现了对 RecipeSettlementSolver 的直接调用 —— 18 不得自带第二求解实现");
            }
        }

        /// <summary>AC-18-02 符号面:18 全部方法体内**零**算术 —— 指令面 + 成员调用面**两半都断**。</summary>
        /// <remarks>
        /// <para>⚠️ <b>为什么必须两半</b>(2026-10-27 起草期自查修正):<c>Fix</c> 是 <c>struct</c> 且
        /// 重载了 <c>operator *</c> / <c>operator /</c>(<c>Fix.cs:76</c> / <c>:139</c>)⇒
        /// <c>a * b</c> 在 IL 里是 <c>call Fix::op_Multiply</c>,<b>不是</b> <c>mul</c> 指令。
        /// <b>只扫 opcode 会漏掉真正的定点乘除</b> —— 那正是 AC-18-02 点名要禁的东西。</para>
        /// <para>⇒ 判据 = ① 零 <c>mul</c>/<c>div</c>/<c>rem</c> 指令(覆盖裸 <c>int</c>/<c>long</c> 算术);
        /// ② 零调用 <c>Fix</c> 的算术成员(<c>op_Multiply</c> / <c>op_Division</c> / <c>MulRaw</c> /
        /// <c>Pow</c> / <c>Sqrt</c> / <c>Exp</c> / <c>Round</c>)与舍入钳制成员
        /// (<c>Ceil</c> / <c>Round</c> / <c>Clamp</c> 的任何声明类型)。
        /// 两者合起来才等价于 GDD :717 的「定点乘除 / Ceil / Round / Clamp 的调用 = 零」。</para>
        /// <para>⚠️ 扫的是**整个 18 命名空间的全部方法**(含编译器生成的 getter / 运算符),
        /// 不只是三个入口 —— 第二实现最可能藏在新加的辅助方法里。</para>
        /// <para>⚠️ 边界(GDD AC-18-02 原文「装载校验 / 呈现换算不算」):18 侧目前**没有**
        /// 呈现换算,装载校验(<c>ValidateConstantTable</c>)只委托不运算 ⇒ 全部豁免面为空,
        /// 故本条按「零」断言。若将来真需豁免,须在本夹具具名登记豁免方法名并配「豁免仍活」断言。</para>
        /// </remarks>
        [Test]
        public void test_ac1802_il_noArithmeticInProcessingNamespace()
        {
            List<string> opcodes = ArithmeticOpcodesInType(SimDll(), ProcessingNs);
            Assert.That(opcodes, Is.Empty,
                "18 侧不得出现 mul/div/rem 指令(AC-18-02 指令面):\n" + string.Join("\n", opcodes));

            List<string> calls = ArithmeticMemberCallsInProcessing();
            Assert.That(calls, Is.Empty,
                "18 侧不得调用定点算术 / 舍入 / 钳制成员(AC-18-02 调用面):\n" + string.Join("\n", calls));
        }

        /// <summary>AC-18-06 符号面:<c>ProcessingSettlementInputs</c> 体内**零 `add`/`sub`**。</summary>
        /// <remarks>
        /// <para>⚠️ <b>这条是 2026-10-27 评审 A-1 补的缺口。</b>原扫描只按 `mul`/`div`/`rem`
        /// 前缀匹配,而 AC-18-06 要的「18 侧**零加法**」在 raw 域写法下编译成两条 `ldfld` + `add`
        /// —— **两层扫描都看不见**。它恰是「两 EnvMod 分量相加」最可能的规避形态
        /// (求和 + clamp 的唯一合法落点是 21a F1 正文,承 R-18-C / D-21-31)。</para>
        /// <para><b>为什么只扫这一个类,不扫整个命名空间</b>:实测
        /// <c>IgnitionAdmission.cs:141</c> 有 `load = checked(load + ContainerWeightFold.InstanceWeight(...))`
        /// —— 那是**载重累加的合法业务加法**(F-20.2 `CanCarry`),与定点结算无关。
        /// 全命名空间扫 `add` 会把它误报成违例。故本条的扫描面**收窄到结算供料这一个类**:
        /// 该类是 AC-18-02/06 的承重面,而载重累加不在其内。</para>
        /// <para><b>为什么这个收窄不是放水</b>:AC-18-06 管的是「18 向 F1 供料时不得相加」,
        /// 承重面就是 <c>ProcessingSettlementInputs</c>。<c>IgnitionAdmission</c> 属
        /// AC-18-12(点火准入),不在本 AC 面内。</para>
        /// </remarks>
        [Test]
        public void test_ac1806_il_settlementClassHasNoAddSub()
        {
            List<string> hits = ArithmeticOpcodesInType(
                SimDll(), ProcessingNs, true, nameof(ProcessingSettlementInputs));

            Assert.That(hits, Is.Empty,
                "ProcessingSettlementInputs 体内不得出现 add/sub 指令 —— " +
                "两 EnvMod 分量必须原样转发,求和归 21a F1 正文(AC-18-06 / R-18-C):\n"
                + string.Join("\n", hits));
        }

        /// <summary>AC-18-06 正控:同一扫描器在**含加法**的类里必须命中。</summary>
        /// <remarks>
        /// 没有正控的「零命中」与「扫描器看不见 add」逐位不可区分 —— 本测选的正是
        /// <c>IgnitionAdmission</c>(实测含 <c>checked(load + ...)</c>),它既是扫描面的
        /// **反例**,也是本条的**判别力来源**。
        /// </remarks>
        [Test]
        public void test_ac1806_il_addSubScannerPositiveControl()
        {
            List<string> hits = ArithmeticOpcodesInType(
                SimDll(), ProcessingNs, true, "IgnitionAdmission");

            Assert.That(hits, Is.Not.Empty,
                "IgnitionAdmission 含 checked(load + InstanceWeight) ⇒ 扫描器必须命中;" +
                "此处为空说明 add 扫描是空转(正控)");
        }

        /// <summary>AC-18-02 正控:唯一求解器体内**必须**有 `mul` —— 证明扫描器看得见算术。</summary>
        /// <remarks>
        /// 没有正控的「零命中」与「扫描器什么都没扫到」逐位不可区分。
        /// 选 <c>RecipeSettlementSolver.Solve</c>:它是全案**唯一该有算术**的地方。
        /// ⚠️ 它与 18 同住 <c>DaYiJingCheng.Sim</c> 命名空间(非 <c>.ItemDatabase</c>)——
        /// 扫描器按**类型名**收窄,故同命名空间的其他类型不会污染本正控。
        /// </remarks>
        [Test]
        public void test_ac1802_il_scannerPositiveControl_seesArithmeticInRealSolver()
        {
            List<string> hits = ArithmeticOpcodesInType(
                SimDll(), "DaYiJingCheng.Sim", "RecipeSettlementSolver");
            Assert.That(hits, Is.Not.Empty,
                "唯一求解器体内必须有 mul/div 指令 —— 否则本组 IL 扫描器是空转(正控)");
        }

        /// <summary>AC-18-02 调用面正控:同一 <c>ScanArithmeticMembers</c> 必须能在求解器里命中。</summary>
        /// <remarks>
        /// ⚠️ 正控**必须与判据走同一条链路** —— 这里调的是判据用的同一个
        /// <c>ScanArithmeticMembers</c>,不是另写一份 `IndexOf`。否则是「空洞正控」
        /// (本项目已抓过:S20-4 把判据的 `'*'` 改成 `'+'`,空洞正控照样绿)。
        /// </remarks>
        [Test]
        public void test_ac1802_il_scannerPositiveControl_seesArithmeticCallsInRealSolver()
        {
            List<string> hits = ScanArithmeticMembers(
                SimDll(), "DaYiJingCheng.Sim", "RecipeSettlementSolver");
            Assert.That(hits, Is.Not.Empty,
                "唯一求解器体内必须调用 Fix 的算术成员 —— 否则调用面扫描器空转(正控)");
        }

        /// <summary>AC-18-04 符号面:18 侧**零**引用药物身份符号(`drug_profile` / `polarity` / `treatable_by`)。</summary>
        /// <remarks>
        /// <para>⚠️ <b>符号层扫的是「类型 + 成员名」两半,不是只扫类型</b> —— 实测本项目里
        /// <c>polarity</c> 的真实符号形态是<b>字段</b>(<c>TreatableByEntry.Polarity</c>),
        /// 读它编译成 <c>ldfld</c> 而非 <c>call</c>。只扫 <c>call</c> 目标会**整条漏掉**,
        /// 那是「扫描器瞎了却报绿」的典型形态。</para>
        /// <para>⇒ 本扫描器同时看:① <c>call</c>/<c>callvirt</c> 的声明类型名;
        /// ② <c>ldfld</c>/<c>ldsfld</c>/<c>stfld</c>/<c>stsfld</c> 的字段名与声明类型名。
        /// 两者按**符号引用**匹配,正是 GDD :718 的口径。</para>
        /// </remarks>
        [Test]
        public void test_ac1804_il_neverReferencesDrugIdentitySymbols()
        {
            List<string> hits = DrugIdentityReferencesInProcessing();
            Assert.That(hits, Is.Empty,
                "18 不认识药(规则九)—— 符号层不得引用药物身份符号:\n" + string.Join("\n", hits));
        }

        /// <summary>AC-18-04 正控:同一扫描器在**该**读药的系统里必须命中。</summary>
        /// <remarks>
        /// 选 <c>ProgressionEvaluator</c>(规则九的另一侧)—— 它体内确有
        /// <c>TreatableByEntry[] tb = registry.TreatableBy</c> 的字段读取。
        /// 若这里也零命中,说明扫描器瞎了,上条的零命中不可信。
        /// ⚠️ 正控**必须与判据走同一条链路**(同一 <c>ScanDrugIdentity</c> 函数),
        /// 否则是「空洞正控」—— 本项目已抓过的形态。
        /// </remarks>
        [Test]
        public void test_ac1804_il_scannerPositiveControl_seesDrugSymbolsInTheSystemThatOwnsThem()
        {
            List<string> hits = ScanDrugIdentity(SimDll(), "DaYiJingCheng.Sim",
                                                 "ProgressionEvaluator");
            Assert.That(hits, Is.Not.Empty,
                "药效系统体内必须有对 TreatableBy / Polarity 的符号引用 —— 否则 IL 扫描器空转(正控)");
        }

        // ── IL 扫描辅助 ────────────────────────────────────────────────────

        /// <summary>18 侧的命名空间(GDD AC-18-01③ 的「18 程序集」实际落点)。</summary>
        /// <remarks>
        /// ⚠️ **18 没有独立程序集** —— 它与 21a 求解器同住 <c>Sim</c>(ADR-025 §① 的六装配清单里
        /// 无 18 条目)。故「18 程序集内」按**命名空间**定位。这样定位的好处:
        /// 将来 18 若拆出独立程序集,本组断言**仍成立**(扫描面自动跟随类型所在程序集)。
        /// </remarks>
        private const string ProcessingNs = "DaYiJingCheng.Sim.Processing";

        /// <summary>18 侧三个入口方法名(<c>ValidateConstantTable</c> 属装载面,仍应零算术)。</summary>
        private static readonly string[] ProcessingEntryPoints =
        {
            nameof(ProcessingSettlementInputs.Assemble),
            nameof(ProcessingSettlementInputs.Settle),
            nameof(ProcessingSettlementInputs.ValidateConstantTable),
        };

        /// <summary>药物身份符号在 IL 层的可见形态 —— **声明类型名 + 成员名**两半。</summary>
        /// <remarks>
        /// <para>⚠️ 只扫类型名会漏掉真实形态:本项目 `polarity` 是**字段**
        /// (<c>TreatableByEntry.Polarity</c>),读它编译成 <c>ldfld</c>,其操作数只带字段名与声明类型,
        /// **不带** <c>call</c>。故成员名必须单列一张表并参与匹配。</para>
        /// <para>⚠️ 匹配口径 = 「声明类型名 ∈ <see cref="DrugIdentityIlTypes"/>」
        /// **或**「成员名 ∈ <see cref="DrugIdentityIlMembers"/>(忽略大小写)」——
        /// 二者任一命中即违例。</para>
        /// </remarks>
        private static readonly string[] DrugIdentityIlTypes =
        {
            "DrugProfile", "IDrugProfile", "TreatableByEntry", "ITreatableByLookup",
        };

        /// <summary>药物身份成员名(小写比对)。`drug_profile` / `polarity` / `treatable_by` 三词的
        /// 各种落盘形态 —— 字段 / 属性 / 方法名皆覆盖。</summary>
        private static readonly string[] DrugIdentityIlMembers =
        {
            "polarity", "treatableby", "treatable_by", "drugprofile", "drug_profile",
        };

        /// <summary>在 18 侧全命名空间扫药物身份符号引用。</summary>
        private static List<string> DrugIdentityReferencesInProcessing()
            => ScanDrugIdentity(SimDll(), ProcessingNs, null);

        /// <summary>扫某命名空间(可再收窄到单类型)内**全部**方法体中的药物身份符号引用。</summary>
        /// <remarks>
        /// 三处要看:`call`/`callvirt` 的方法引用、`ldfld`/`stfld`/`ldsfld`/`stsfld` 的字段引用、
        /// 以及 `ldtoken`/`newobj` 的类型引用。全部按**符号**判定,不做字符串匹配。
        /// </remarks>
        private static List<string> ScanDrugIdentity(string dll, string ns, string typeName)
        {
            var hits = new List<string>();
            using (ModuleDefinition mod = ModuleDefinition.ReadModule(dll))
            {
                foreach (TypeDefinition t in mod.GetTypes().Where(x => x.Namespace == ns))
                {
                    if (typeName != null && t.Name != typeName) continue;
                    foreach (MethodDefinition m in t.Methods)
                    {
                        if (!m.HasBody) continue;
                        foreach (Instruction ins in m.Body.Instructions)
                        {
                            string what = null;

                            if ((ins.OpCode.Code == Code.Call || ins.OpCode.Code == Code.Callvirt) &&
                                ins.Operand is MethodReference mr)
                            {
                                if (IsDrugType(mr.DeclaringType?.Name) || IsDrugMember(mr.Name))
                                    what = $"{mr.DeclaringType?.Name}.{mr.Name}";
                            }
                            else if ((ins.OpCode.Code == Code.Ldfld || ins.OpCode.Code == Code.Stfld ||
                                      ins.OpCode.Code == Code.Ldsfld || ins.OpCode.Code == Code.Stsfld) &&
                                     ins.Operand is FieldReference fr)
                            {
                                if (IsDrugType(fr.DeclaringType?.Name) || IsDrugMember(fr.Name))
                                    what = $"{fr.DeclaringType?.Name}.{fr.Name}";
                            }

                            if (what != null) hits.Add($"{t.Name}.{m.Name}: {what}");
                        }
                    }
                }
            }
            return hits;
        }

        private static bool IsDrugType(string n)
            => n != null && DrugIdentityIlTypes.Contains(n, StringComparer.Ordinal);

        private static bool IsDrugMember(string n)
            => n != null && DrugIdentityIlMembers.Contains(n.ToLowerInvariant(), StringComparer.Ordinal);

        // ── AC-18-02 调用面:定点算术 / 舍入 / 钳制成员 ──────────────────────

        /// <summary>`Fix` 的算术成员名 —— <c>struct</c> 运算符重载在 IL 里是普通静态方法。</summary>
        /// <remarks>
        /// ⚠️ <b>这张表是 AC-18-02 的承重件,不是可选装饰</b>:<c>Fix</c> 重载了
        /// <c>operator *</c> / <c>operator /</c>(<c>Fix.cs:76</c> / <c>:139</c>)⇒ 调用点编译成
        /// <c>call Fix::op_Multiply</c> / <c>op_Division</c>,<b>不产生</b> <c>mul</c>/<c>div</c> 指令。
        /// 只扫 opcode 会整条漏掉定点乘除。表内名字取自 <c>Fix.cs</c> 的实际声明,
        /// 由 <c>test_ac1802_il_scannerPositiveControl_...</c> 证明其非空转。
        /// </remarks>
        private static readonly string[] ArithmeticMemberNames =
        {
            "op_Multiply", "op_Division", "op_Addition", "op_Subtraction",
            "MulRaw", "DivRaw", "Pow", "Sqrt", "Exp", "Round", "Ceil", "CeilDiv", "Clamp",
        };

        /// <summary>在 18 侧全命名空间扫算术 / 舍入 / 钳制成员调用。</summary>
        private static List<string> ArithmeticMemberCallsInProcessing()
            => ScanArithmeticMembers(SimDll(), ProcessingNs, null);

        /// <summary>扫某命名空间(可收窄到单类型)内全部 `call`/`callvirt` 的算术成员引用。</summary>
        private static List<string> ScanArithmeticMembers(string dll, string ns, string typeName)
        {
            var hits = new List<string>();
            using (ModuleDefinition mod = ModuleDefinition.ReadModule(dll))
            {
                foreach (TypeDefinition t in mod.GetTypes().Where(x => x.Namespace == ns))
                {
                    if (typeName != null && t.Name != typeName) continue;
                    foreach (MethodDefinition m in t.Methods)
                    {
                        if (!m.HasBody) continue;
                        foreach (Instruction ins in m.Body.Instructions)
                        {
                            if (ins.OpCode.Code != Code.Call && ins.OpCode.Code != Code.Callvirt)
                                continue;
                            if (ins.Operand is MethodReference mr && mr.Name != null &&
                                ArithmeticMemberNames.Contains(mr.Name, StringComparer.Ordinal))
                                hits.Add($"{t.Name}.{m.Name}: {mr.DeclaringType?.Name}.{mr.Name}");
                        }
                    }
                }
            }
            return hits;
        }

        /// <summary>`Sim` 程序集的 DLL 路径(18 与 21a 同居其中)。</summary>
        private static string SimDll()
        {
            string loc = typeof(ProcessingSettlementInputs).Assembly.Location;
            Assert.That(File.Exists(loc), Is.True, $"Sim 程序集未落盘:{loc}");
            return loc;
        }

        /// <summary>数「命名空间 ns 下名为 typeName 的类型」中「名为 methodName 的方法」体内
        /// `call`/`callvirt` 到**声明类型名为 declTypeName** 的次数。</summary>
        private static int CountCallsInType(string dll, string ns, string typeName,
                                            string methodName, string declTypeName)
        {
            int n = 0;
            using (ModuleDefinition mod = ModuleDefinition.ReadModule(dll))
            {
                foreach (TypeDefinition t in mod.GetTypes()
                                                .Where(x => x.Namespace == ns && x.Name == typeName))
                    foreach (MethodDefinition m in t.Methods)
                    {
                        if (m.Name != methodName || !m.HasBody) continue;
                        foreach (Instruction ins in m.Body.Instructions)
                        {
                            if (ins.OpCode.Code != Code.Call && ins.OpCode.Code != Code.Callvirt)
                                continue;
                            if (ins.Operand is MethodReference mr &&
                                mr.DeclaringType != null &&
                                mr.DeclaringType.Name == declTypeName)
                                n++;
                        }
                    }
            }
            return n;
        }

        /// <summary>数「命名空间 ns 下名为 typeName 的类型」全部方法体内
        /// `call`/`callvirt` 到**声明类型名为 declTypeName** 的总次数。</summary>
        private static int CountCallsInType(string dll, string ns, string typeName, string declTypeName)
        {
            int n = 0;
            using (ModuleDefinition mod = ModuleDefinition.ReadModule(dll))
            {
                foreach (TypeDefinition t in mod.GetTypes()
                                                .Where(x => x.Namespace == ns && x.Name == typeName))
                    foreach (MethodDefinition m in t.Methods)
                    {
                        if (!m.HasBody) continue;
                        foreach (Instruction ins in m.Body.Instructions)
                        {
                            if (ins.OpCode.Code != Code.Call && ins.OpCode.Code != Code.Callvirt)
                                continue;
                            if (ins.Operand is MethodReference mr &&
                                mr.DeclaringType != null &&
                                mr.DeclaringType.Name == declTypeName)
                                n++;
                        }
                    }
            }
            return n;
        }

        /// <summary>收集某命名空间（可再限定单个类型）内全部 `mul` / `div` / `rem` 指令。</summary>
        /// <remarks>
        /// ⚠️ 按**前缀**匹配助记符(`mul` / `mul.ovf` / `mul.ovf.un` / `div` / `div.un` / `rem` / `rem.un`),
        /// 不按精确名 —— 漏掉 `mul.ovf` 就等于给溢出检查版开了后门。
        /// <para>⚠️ <b>`add` / `sub` 默认**不扫**</b>(2026-10-27 评审 A-1 的处置)。理由见
        /// <see cref="ArithmeticOpcodePrefixes"/>。</para>
        /// </remarks>
        private static List<string> ArithmeticOpcodesInType(string dll, string ns,
                                                            params string[] typeNames)
            => ArithmeticOpcodesInType(dll, ns, false, typeNames);

        /// <param name="includeAddSub">是否把 `add` / `sub` 一并算违例。</param>
        private static List<string> ArithmeticOpcodesInType(string dll, string ns,
                                                            bool includeAddSub,
                                                            params string[] typeNames)
        {
            var found = new List<string>();
            var wanted = typeNames != null && typeNames.Length > 0
                ? new HashSet<string>(typeNames, StringComparer.Ordinal)
                : null;
            string[] prefixes = includeAddSub
                ? new[] { "mul", "div", "rem", "add", "sub" }
                : new[] { "mul", "div", "rem" };

            using (ModuleDefinition mod = ModuleDefinition.ReadModule(dll))
            {
                foreach (TypeDefinition t in mod.GetTypes().Where(x => x.Namespace == ns))
                {
                    if (wanted != null && !wanted.Contains(t.Name)) continue;
                    foreach (MethodDefinition m in t.Methods)
                    {
                        if (!m.HasBody) continue;
                        foreach (Instruction ins in m.Body.Instructions)
                        {
                            string op = ins.OpCode.Name;
                            foreach (string p in prefixes)
                            {
                                if (!op.StartsWith(p, StringComparison.Ordinal)) continue;
                                found.Add($"{t.Name}.{m.Name}: {op}");
                                break;
                            }
                        }
                    }
                }
            }
            return found;
        }

        // ══════════════════════════════════════════════════════════════════
        // 扫描器本体 + 标识符表
        // ══════════════════════════════════════════════════════════════════

        /// <summary>从权威件 <c>recipe_settlement_solver_test.cs</c> 解析 AC-21a-6 的公式体标识符表。
        /// </summary>
        /// <remarks>
        /// <para>⚠️ <b>不抄一份常量表</b>(story §Implementation Notes 4「零第二份扫描基建」):
        /// 抄一份 = 两条可各自漂移的定义,而漂移方向恰是「权威件加了新标识符、18 侧那份没加」
        /// ⇒ 18 自建的第二实现从新标识符溜过去。故此处**解析**而非抄写。</para>
        /// <para>解析形:权威件里 <c>private static readonly string[] FormulaIdentifiers = { ... };</c>
        /// 的双引号内容,按出现序。结构一旦改变(改名 / 改容器类型),本方法返回空数组,
        /// 调用方断言非空 ⇒ **红**,不静默零命中。</para>
        /// </remarks>
        private static string[] CanonicalFormulaIdentifiers()
        {
            string path = Path.Combine(RepoRoot(), "unity", "Assets", "Tests", "PlayMode",
                "recipe_settlement_solver_test.cs");
            Assert.That(File.Exists(path), Is.True, $"权威件缺失:{path}");

            string src = File.ReadAllText(path);
            int decl = src.IndexOf("string[] FormulaIdentifiers", StringComparison.Ordinal);
            Assert.That(decl, Is.GreaterThanOrEqualTo(0),
                "权威件里找不到 `string[] FormulaIdentifiers` 声明 —— 结构已变,本夹具的解析前提失效");

            int open = src.IndexOf('{', decl);
            int close = src.IndexOf("};", open, StringComparison.Ordinal);
            Assert.That(open, Is.GreaterThan(0), "找不到标识符表的 `{`");
            Assert.That(close, Is.GreaterThan(open), "找不到标识符表的 `};`");

            string body = src.Substring(open + 1, close - open - 1);

            var ids = new List<string>();
            int i = 0;
            while (i < body.Length)
            {
                int q1 = body.IndexOf('"', i);
                if (q1 < 0) break;
                int q2 = body.IndexOf('"', q1 + 1);
                if (q2 < 0) break;
                ids.Add(body.Substring(q1 + 1, q2 - q1 - 1));
                i = q2 + 1;
            }
            return ids.ToArray();
        }

        /// <summary>结算算式的符号表(AC-18-02)。
        /// <para>含三类:① 舍入 `Ceil(`/`Round(`/`Math.Ceiling`/`Math.Round`;
        /// ② 钳制 `Clamp(`/`Math.Clamp`;③ 定点乘除 `MulRaw`/`Fix.Pow`/`Fix.Sqrt`/`Fix.Exp`。
        /// 第三类是本条与 AC-18-01③ **不重合**的那一半 —— 公式标识符表里没有乘法符号。</para>
        /// </summary>
        private static readonly string[] ArithmeticIdentifiers =
        {
            "Ceil(", "CeilDiv", "Round(", "Clamp(", "Math.Ceiling", "Math.Round", "Math.Clamp",
            "MulRaw", "Fix.Pow", "Fix.Sqrt", "Fix.Exp",
        };

        /// <summary>药物档案符号表(AC-18-04,规则九)。</summary>
        private static readonly string[] DrugIdentityIdentifiers =
        {
            "drug_profile", "polarity", "treatable_by", "DrugProfile",
        };

        /// <summary>18 侧生产源(相对 <c>unity/</c>)。</summary>
        private static List<string> ProcessingSources()
        {
            string unityRoot = Path.Combine(RepoRoot(), "unity");
            string dir = Path.Combine(unityRoot, "Assets", "Sim", "Processing");
            return Directory.GetFiles(dir, "*.cs", SearchOption.TopDirectoryOnly)
                .Select(f => f.Substring(unityRoot.Length)
                              .TrimStart(Path.DirectorySeparatorChar)
                              .Replace(Path.DirectorySeparatorChar, '/'))
                .OrderBy(p => p, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>在 18 侧全部源里扫一组标识符,返回 `文件:行:标识符` 形态的命中。</summary>
        private static List<string> ScanProcessingSources(IReadOnlyList<string> identifiers)
        {
            string root = RepoRoot();
            var hits = new List<string>();

            foreach (string rel in ProcessingSources())
            {
                string code = StripComments(File.ReadAllText(Path.Combine(root, "unity", rel)));
                foreach (string id in identifiers)
                    foreach (int idx in IdentifierOccurrences(code, id))
                        hits.Add($"{rel}:{LineOf(code, idx)}: {id}");
            }
            return hits;
        }

        /// <summary>标识符出现位置(按**词边界**匹配 —— 防 `Retain(` 命中 `RetainMin`)。</summary>
        /// <remarks>
        /// ⚠️ 带 `(` 的条目按**原文子串**匹配(它本身就是带括号的调用形态);
        /// 不带括号的按标识符边界匹配。
        /// </remarks>
        private static IEnumerable<int> IdentifierOccurrences(string code, string identifier)
        {
            bool isCallForm = identifier.EndsWith("(", StringComparison.Ordinal);
            int from = 0;
            while (true)
            {
                int idx = code.IndexOf(identifier, from, StringComparison.Ordinal);
                if (idx < 0) yield break;
                from = idx + 1;

                if (isCallForm) { yield return idx; continue; }

                // 词边界:前后不得是标识符字符
                bool leftOk = idx == 0 || !IsIdentChar(code[idx - 1]);
                int end = idx + identifier.Length;
                bool rightOk = end >= code.Length || !IsIdentChar(code[end]);
                if (leftOk && rightOk) yield return idx;
            }
        }

        private static bool IsIdentChar(char c)
            => char.IsLetterOrDigit(c) || c == '_';

        private static string RepoRoot()
            => Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", ".."));

        /// <summary>剥除 `//` 与 `/* */` 注释(字符串 / 字符字面量内容保留)。</summary>
        private static string StripComments(string src)
        {
            var sb = new System.Text.StringBuilder(src.Length);
            bool inLine = false, inBlock = false, inStr = false, inChar = false, esc = false;

            for (int i = 0; i < src.Length; i++)
            {
                char c = src[i];
                char n = i + 1 < src.Length ? src[i + 1] : '\0';

                if (inLine) { if (c == '\n') { inLine = false; sb.Append(c); } continue; }
                if (inBlock) { if (c == '*' && n == '/') { inBlock = false; i++; } continue; }
                if (inStr)
                {
                    sb.Append(c);
                    if (esc) { esc = false; continue; }
                    if (c == '\\') { esc = true; continue; }
                    if (c == '"') inStr = false;
                    continue;
                }
                if (inChar)
                {
                    sb.Append(c);
                    if (esc) { esc = false; continue; }
                    if (c == '\\') { esc = true; continue; }
                    if (c == '\'') inChar = false;
                    continue;
                }

                if (c == '/' && n == '/') { inLine = true; i++; continue; }
                if (c == '/' && n == '*') { inBlock = true; i++; continue; }
                if (c == '"') { inStr = true; sb.Append(c); continue; }
                if (c == '\'') { inChar = true; sb.Append(c); continue; }
                sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>字符偏移 → 行号(1 起;诊断用)。</summary>
        private static int LineOf(string text, int index)
        {
            int line = 1;
            for (int i = 0; i < index && i < text.Length; i++)
                if (text[i] == '\n') line++;
            return line;
        }
    }
}
