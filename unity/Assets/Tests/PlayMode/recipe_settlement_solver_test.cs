// 权威来源:
//   Story 003(production/epics/item-database/story-003-recipe-settlement-solver.md)
//     · AC-21a-5 [I] —— 18 炮制与 19 制作各自发起结算,传入相同参数集 ⇒ 两者走**同一个函数**
//                        (反射/结构断言两入口收敛到同一 MethodInfo,非各自实现)
//     · AC-21a-6 [I] —— grep 21 求解器源码,17/18/19 **不含任何重复结算实现**
//   GDD:design/gdd/item-database.md §Formulas 节首「权威边界(Q1 裁决)」:
//        「17 采集 / 18 炮制 / 19 制作只产出输入参数,不得自建结算逻辑」
//        (systems-index §9 C5 的推广 —— 两套算法 = 「两种路径都满足」破产)
//   ADR-005(主):确定性模拟 —— 同参数集必须逐位产出同结果
//   ADR-025 §①(`Sim` 装配)/ §⑤(测试装配:PlayMode → `Gameplay.Tests`)
//
// ⚠️ 落点:故事头登记的账本路径 = tests/integration/item_database/recipe_settlement_solver_test.cs;
//    Unity 只编译 unity/Assets/ 树 ⇒ 真身 = 本文件(与 Story 001/002 同一先例)。
//    **本文件住 PlayMode**(asmdef `Gameplay.Tests`)—— 承 tests/integration/README.md
//    「Unity 侧实际编译进 PlayModeTests.asmdef(待 ADR #2)」,装配名已由 ADR-025 §⑤ 钉为 `Gameplay.Tests`。
//    PlayMode 装配的引用集**已增** Sim / Sim.Contracts 两条 GUID(否则本文件不可编译)——
//    该增补 = ADR-025 §⑤「测试装配族」范围内,不动六装配清单(§④ 封闭性不受影响)。
//
// ⚠️ AC-21a-5 的入口接缝:18 / 19 的系统程序集**尚未落地**,故本文件对
//    `ProcessingSettlementEntry` / `CraftingSettlementEntry`(Sim/ItemDatabase/RecipeSettlementEntryPoints.cs)
//    断言 —— 它们是 18/19 落地后必须收敛到的**同一个** MethodInfo。18/19 出现后本文件不改判据。
//
// ⚠️ AC-21a-6 的扫描面 = `unity/Assets/**` 的 C# 源(排除 Tests 装配),**大小写敏感**裸标识符匹配。
//    白名单**恰 = 唯一求解器文件一个**;入口接缝文件**不在白名单**(它被当作 18/19 侧对待)。
//    17 的 F3(采集品级分布)是 17 拥有的动作(GDD 明文「不归 F1 的唯一求解器」)⇒ 单独白名单,
//    防误报 —— 判据取 F3 的**形状标识符**(gather_profile / quality_distribution),
//    不取 F1 公式体标识符。
//    扫描是项目**已登记例外**(允许 File I/O;先例 =
//    `schema_types_primary_key_test.cs::test_tuningKnobIdentifiers_hardcodedInSourceCode_none`)。
//    17/18/19 目录当前**不存在** ⇒ 扫描器须容忍缺失(只扫存在的树)。
//
// 测试纪律:test_[scenario]_[expected];无随机种子、无时间依赖(AC-21a-5 的遍历是枚举式);
//    数值全部为**测试夹具值,非游戏数值**(GDD §Tuning Knobs「默认」列留空 ⇒ 数值待用户)。

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using NUnit.Framework;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Tests.Integration.ItemDatabase
{
    [TestFixture]
    internal sealed class RecipeSettlementSolverIntegrationTest
    {
        /// <summary>夹具常量表 —— **夹具值,非游戏数值;数值待用户**(GDD §Tuning Knobs)。</summary>
        [Description("夹具常量表:各 cap = 1/10、ENV_MOD_MAX = 1/2、QTY_MULT_MAX = 2 ⇒ 左侧 0.8 ≤ 右侧 1。" +
                     "**夹具值,非游戏数值;数值待用户**。")]
        private static RecipeSettlementConstants fixture() => new RecipeSettlementConstants(
            qtyMultMin: FixParse.Parse("1/4"),
            qtyMultMax: FixParse.Parse("2"),
            skillModCap: FixParse.Parse("1/10"),
            qualModCap: FixParse.Parse("1/10"),
            equipModCap: FixParse.Parse("1/10"),
            envModMin: FixParse.Parse("-1"),
            envModMax: FixParse.Parse("1/2"),
            retainMin: FixParse.Parse("1/2"),
            retainMax: FixParse.Parse("1"),
            effMin: FixParse.Parse("1/2"),
            effMax: FixParse.Parse("1"),
            maxQuality: 5,
            skillCap: 60);

        private static RecipeEntry[] entries(int count, string prefix, int qty)
        {
            var result = new RecipeEntry[count];
            for (int i = 0; i < count; i++)
                result[i] = new RecipeEntry(new ItemKey(prefix + "_" + i, ProcessingState.Raw), qty);
            return result;
        }

        private static MethodInfo solverMethod() => typeof(RecipeSettlementSolver).GetMethod("Solve");

        // ══════════════ AC-21a-5:两入口收敛到同一函数 ══════════════

        [Test]
        public void test_bothEntryPoints_delegateToSameMethodInfo()
        {
            // Given:唯一求解器的真实方法(反射取)
            MethodInfo canonical = solverMethod();

            // When:读两个入口各自暴露的委托
            MethodInfo viaProcessing = ProcessingSettlementEntry.Solver.Method;
            MethodInfo viaCrafting = CraftingSettlementEntry.Solver.Method;

            // Then:三者为**同一 MethodInfo**(非「签名相同」—— 是同一个方法实体)
            Assert.That(canonical, Is.Not.Null, "求解器入口方法必须存在(否则本断言无意义)");
            Assert.That(viaProcessing, Is.EqualTo(canonical),
                "18 炮制侧入口必须委托到 21 的**唯一**求解器(AC-21a-5)");
            Assert.That(viaCrafting, Is.EqualTo(canonical),
                "19 制作侧入口必须委托到 21 的**唯一**求解器(AC-21a-5)");
            Assert.That(viaProcessing, Is.EqualTo(viaCrafting),
                "两入口必须指向同一 MethodInfo —— 各自实现即「两种路径都满足」破产");
        }

        [Test]
        public void test_bothEntryPoints_sameParameters_bitIdenticalResults()
        {
            // Given:同一配方、同一组输入参数(n = 2 产出、m = 2 投入)
            RecipeSettlementConstants c = fixture();
            RecipeSettlementRequest request = new RecipeSettlementRequest(
                outputs: entries(2, "out", 3),
                inputs: entries(2, "in", 2),
                craftSkillLevel: 30,
                inputQuality: 3,
                equipMod: FixParse.Parse("1/20"),
                envModClimate: FixParse.Parse("2/5"),
                envModClinic: FixParse.Parse("-1/10"));

            // When:分别经 18 侧入口与 19 侧入口发起
            RecipeSettlementResult viaProcessing = ProcessingSettlementEntry.Solve(request, c);
            RecipeSettlementResult viaCrafting = CraftingSettlementEntry.Solve(request, c);

            // Then:输出逐位相同(Fix 无 implicit operator float ⇒ 比 `.Raw` long)
            Assert.That(viaProcessing.QtyMultiplier.Raw, Is.EqualTo(viaCrafting.QtyMultiplier.Raw));
            Assert.That(viaProcessing.EnvModTotal.Raw, Is.EqualTo(viaCrafting.EnvModTotal.Raw));
            Assert.That(viaProcessing.SumOfModifiers.Raw, Is.EqualTo(viaCrafting.SumOfModifiers.Raw));
            Assert.That(viaProcessing.Efficiency.Raw, Is.EqualTo(viaCrafting.Efficiency.Raw));
            Assert.That(viaProcessing.OutputQuality, Is.EqualTo(viaCrafting.OutputQuality));
            Assert.That(viaProcessing.OutputQty, Is.EqualTo(viaCrafting.OutputQty),
                "OutputQty 数组须逐元素相等");
            Assert.That(viaProcessing.ActualConsumed, Is.EqualTo(viaCrafting.ActualConsumed),
                "ActualConsumed 数组须逐元素相等");
        }

        [Test]
        public void test_bothEntryPoints_singleOutputSingleInput_bitIdentical()
        {
            // Given:n = 1、m = 1 特例(QA Edge 点名「n=1,m=1 特例与 n>1 特例同参数对拍」)
            RecipeSettlementConstants c = fixture();
            RecipeSettlementRequest request = new RecipeSettlementRequest(
                outputs: entries(1, "out", 1),
                inputs: entries(1, "in", 1),
                craftSkillLevel: 0,
                inputQuality: 1,
                equipMod: new Fix(0L),
                envModClimate: FixParse.Parse("-1/2"),
                envModClinic: new Fix(0L));

            // When
            RecipeSettlementResult viaProcessing = ProcessingSettlementEntry.Solve(request, c);
            RecipeSettlementResult viaCrafting = CraftingSettlementEntry.Solve(request, c);

            // Then:特例同样逐位相同,且产出地板生效
            Assert.That(viaProcessing.QtyMultiplier.Raw, Is.EqualTo(viaCrafting.QtyMultiplier.Raw));
            Assert.That(viaProcessing.OutputQty, Is.EqualTo(viaCrafting.OutputQty));
            Assert.That(viaProcessing.OutputQty[0], Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        public void test_entryPointDelegates_haveNoFormulaBodyOfTheirOwn()
        {
            // Given:入口接缝的两个类型
            Type[] entries =
            {
                typeof(ProcessingSettlementEntry),
                typeof(CraftingSettlementEntry),
            };

            // When:反射枚举其**声明**成员(公式体若存在,必然是公开 static 方法或字段)
            // Then:公开成员面恰 = {Solver 委托字段, Solve 方法} —— 无第三条公式面
            foreach (Type entry in entries)
            {
                var publicStaticMethods = new List<string>();
                foreach (MethodInfo m in entry.GetMethods(BindingFlags.Public | BindingFlags.Static |
                                                          BindingFlags.DeclaredOnly))
                    publicStaticMethods.Add(m.Name);

                Assert.That(publicStaticMethods, Is.EquivalentTo(new[] { "Solve" }),
                    $"入口 {entry.Name} 的公开静态方法面应恰为 Solve(只转调,不含公式体)");

                FieldInfo solverField = entry.GetField("Solver", BindingFlags.Public | BindingFlags.Static);
                Assert.That(solverField, Is.Not.Null, $"{entry.Name}.Solver 委托字段必须存在");
                Assert.That(solverField.FieldType, Is.EqualTo(typeof(RecipeSettlementFn)),
                    "Solver 的类型须是唯一求解器签名 RecipeSettlementFn");
            }
        }

        // ══════════════ AC-21a-6:公式体仅存在于唯一求解器 ══════════════

        /// <summary>21 求解器的公式体标识符(显式登记 —— 防换名绕过)。
        /// <para>⚠️ <b>命名变体由 <see cref="StringComparison.OrdinalIgnoreCase"/> 覆盖</b>:
        /// 18 侧若写 <c>qtyMultiplier</c> / <c>outputQuality</c>(C# 局部变量天然 camelCase)
        /// 同样命中 —— 大小写不敏感即同义词表的第一层,不另设登记处。</para>
        /// <para>⚠️ 每条都是**合法 C# 代码文本**(不是 GDD 记号):<c>EnvMod_total</c> /
        /// <c>max(1,</c> 这类 GDD 写法**不是**可出现的源码形态,收录它们 = 死项
        /// (永不可命中,只给扫描面制造虚假宽度)。曲线改用真实标识符 <c>Retain(</c> /
        /// <c>CeilDiv</c> 等。</para></summary>
        private static readonly string[] FormulaIdentifiers =
        {
            "QtyMultiplier",      // F1 配方级乘子
            "SumOfModifiers",     // F1 ΣM
            "OutputQuality",      // F2 品级出参
            "ActualConsumed",     // F1 投入端实耗
            "Retain(",            // F2 保留率曲线(带括号防误命中 RetainMin/RetainMax 字段名)
            "QualityMod",         // F1 品级修正曲线
            "EnvModTotal",        // F1 环境合计(含唯一钳制)
            "Efficiency",         // F2 转化效率曲线
            "CeilDiv",            // F1 实耗的上取整实现
        };

        /// <summary>白名单(逐条带理由 —— 不静默放行任何文件)。</summary>
        private static Dictionary<string, string> Whitelist() => new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // ① 唯一求解器 = 公式体的**唯一**合法落点。
            { "Sim/ItemDatabase/RecipeSettlementSolver.cs",
              "唯一求解器 —— 公式体的唯一合法落点" },
            // ② ③ = `Craft` 世界流事件的**载荷字段名**(registry schema,ADR-024)与其编码器 ——
            //    它们出现 `ActualConsumed` / `OutputQuality` 是**数据形状**,不是公式体。
            //    先例:Story 002 的 AC-21a-48 白名单已把 `ActualConsumed` 登记为
            //    「registry schema 字段,非旋钮值」。此处按**文件**收窄,理由同样显式。
            { "Sim.Contracts/Payloads/WorldPayloads.cs",
              "Craft 事件载荷字段名(registry schema,ADR-024)—— 数据形状非公式体" },
            { "Sim.Codec/PayloadCodec.World.cs",
              "Craft 载荷编码器(字段名 ↔ tag 对位,ADR-006 §五)—— 数据形状非公式体" },
            // ④ 守恒律谓词宿主(Story 005)—— 结构上是唯一求解器的**消费者**,不是重复体:
            //    `RecipeSettlementSolver.ActualConsumed(...)` 是 D-21-32 单一实现纪律要求的
            //    **委托调用**(构建期与运行期同一函数体),`qtyMultiplier` / `efficiency`
            //    是参数名(大小写不敏感匹配的副产品),三者均非自建公式体。
            { "Sim/ItemDatabase/ConservationSolver.cs",
              "守恒律谓词宿主 —— 委托调用唯一求解器(ActualConsumed/OutputQty,D-21-32 单一实现),参数名非公式体" },
        };

        [Test]
        public void test_formulaBodyIdentifiers_duplicatedOutsideUniqueSolver_none()
        {
            // Given:17 的 F3(采集品级分布)是 17 拥有的动作(GDD 明文「不归 F1 的唯一求解器」),
            //   防误报 —— 判据取 F3 的**形状标识符**(非 F1 公式体),二者结构上不相交 ⇒
            //   本扫描天然不命中;下方断言自证该不相交(而非静默假设)。
            string[] f3ShapeIdentifiers = { "quality_distribution", "qty_per_node", "gather_profile" };
            foreach (string shape in f3ShapeIdentifiers)
                Assert.That(Array.IndexOf(FormulaIdentifiers, shape), Is.LessThan(0),
                    $"F3 形状标识符「{shape}」不得进入 F1 公式体扫描面 —— " +
                    "17 的 F3 是 17 拥有的动作,扫它即误报(AC-21a-6 Edge)");

            // When:扫 unity/Assets/** 的 C# 源(排除 Tests 装配;注释剥离 —— 判据是「当公式体用」,
            //   文档性提及不算;先例 = AssemblyGates.CheckToFloatCallsites 的文本扫)
            string scanRoot = Path.Combine(repoRoot(), "unity", "Assets");
            Assert.That(Directory.Exists(scanRoot), Is.True, $"扫描根缺失:{scanRoot}");
            List<string> violations = ScanForDuplicates(scanRoot, FormulaIdentifiers, Whitelist());

            // Then:零命中 —— 公式体仅存在于 21 求解器
            Assert.That(violations, Is.Empty,
                "17/18/19 不得含任何重复结算实现 —— 出现重复体即失败(GDD §Formulas Q1 裁决):\n" +
                string.Join("\n", violations));
        }

        [Test]
        public void test_formulaBodyScanner_selfProvesItCanDetectDuplication()
        {
            // Given:合成违例源(模拟 18 侧自建结算逻辑 —— camelCase 局部名,QA 复核 §5.3 的绕过形)
            string synthetic = "public static Fix Calc(int qty, Fix qtyMultiplier) " +
                               "{ var sumOfModifiers = qtyMultiplier; return sumOfModifiers; } " +
                               "// 自建 OutputQuality 与 ActualConsumed 求解";

            // When:调用**与上条同一**的匹配谓词(非测试体内另抄一份遍历 ——
            //   否则上条的真扫描器被禁用后,本自证仍绿 = 伪证,QA 复核 §5.2)
            List<string> hits = MatchIdentifiers(synthetic, FormulaIdentifiers);

            // Then:匹配器须能抓到(自证:否则上条的「零命中」是假绿)
            Assert.That(hits, Is.Not.Empty,
                "扫描器对合成违例必须命中 —— 否则 test_formulaBodyIdentifiers... 的零命中不可信");
        }

        [Test]
        public void test_formulaBodyScan_missingNeighbourSystemDirs_tolerated()
        {
            // Given:17/18/19 的系统目录当前**不存在**(尚未落地)
            string assets = Path.Combine(repoRoot(), "unity", "Assets");

            // When:枚举实际存在的子目录
            var present = new HashSet<string>();
            foreach (string dir in Directory.GetDirectories(assets))
                present.Add(Path.GetFileName(dir));

            // Then:扫描器不得因缺失目录失败 —— 只扫存在的树(承 Story 001/002 的先例纪律)
            Assert.That(present.Contains("Gather17"), Is.False,
                "夹具自证:「Gather17」(17 采集)当前不应存在 —— 若已落地,本条的容忍断言须复核");
            Assert.That(present.Contains("Gameplay.Process18"), Is.False,
                "夹具自证:「Gameplay.Process18」当前不应存在");
            Assert.That(present.Contains("Gameplay.Craft19"), Is.False,
                "夹具自证:「Gameplay.Craft19」当前不应存在");
            Assert.That(Directory.GetFiles(assets, "*.cs", SearchOption.AllDirectories).Length,
                Is.GreaterThan(0), "扫描面须非空,否则 AC-21a-6 是空转");
        }

        // ══════════════ 私有扫描助手(编辑期文本扫描 —— 项目已登记例外)══════════════

        /// <summary>单一匹配谓词 —— **真扫描与自证测试共用**(QA 复核 §5.2:自证若不调用
        /// 真谓词,禁用真谓词后自证仍绿 = 伪证)。大小写不敏感(命名变体防护,§5.3)。</summary>
        private static List<string> MatchIdentifiers(string code, string[] identifiers)
        {
            var hits = new List<string>();
            foreach (string identifier in identifiers)
                if (code.IndexOf(identifier, StringComparison.OrdinalIgnoreCase) >= 0)
                    hits.Add(identifier);
            return hits;
        }

        /// <summary>遍历扫描树 → 排除 Tests → 白名单跳过 → 剥注释 → 匹配。
        /// 返回违例描述列表(空 = 无重复实现)。</summary>
        private static List<string> ScanForDuplicates(
            string scanRoot, string[] identifiers, Dictionary<string, string> whitelist)
        {
            var violations = new List<string>();
            string normalizedRoot = scanRoot.Replace('\\', '/').TrimEnd('/');

            foreach (string file in Directory.GetFiles(scanRoot, "*.cs", SearchOption.AllDirectories))
            {
                string normalized = file.Replace('\\', '/');
                if (normalized.IndexOf("/Assets/Tests/", StringComparison.Ordinal) >= 0)
                    continue;   // 测试装配排除(本文件自身含这些标识符,不构成违例)

                // 相对路径自扫描根算起(不 IndexOf("/Assets/") —— 前段路径含 "Assets" 会错切)
                string relative = normalized.Substring(normalizedRoot.Length).TrimStart('/');
                if (whitelist.ContainsKey(relative))
                    continue;   // 显式白名单(逐条带理由,见上)

                string code = stripComments(File.ReadAllText(file));
                foreach (string hit in MatchIdentifiers(code, identifiers))
                    violations.Add(
                        $"{relative}: 公式体标识符「{hit}」出现在唯一求解器之外(AC-21a-6)");
            }
            return violations;
        }

        /// <summary>剥注释:块注释 + 行注释(文档性提及不算「当公式体用」)。</summary>
        private static string stripComments(string source)
        {
            var sb = new StringBuilder(source.Length);
            bool inBlock = false;
            for (int i = 0; i < source.Length; i++)
            {
                if (inBlock)
                {
                    if (i + 1 < source.Length && source[i] == '*' && source[i + 1] == '/')
                    { inBlock = false; i++; }
                    continue;
                }
                if (i + 1 < source.Length && source[i] == '/' && source[i + 1] == '*')
                { inBlock = true; i++; continue; }
                if (i + 1 < source.Length && source[i] == '/' && source[i + 1] == '/')
                {
                    while (i < source.Length && source[i] != '\n') i++;
                    sb.Append('\n');
                    continue;
                }
                sb.Append(source[i]);
            }
            return sb.ToString();
        }

        /// <summary>仓根定位:本文件 = &lt;root&gt;/unity/Assets/Tests/PlayMode/… ⇒ 上跳 4 级。
        /// 先 Assert 存在,防「静默扫空目录 = 假绿」(承 Story 002 的 [CallerFilePath] 纪律)。</summary>
        private static string repoRoot([CallerFilePath] string callerPath = "")
        {
            DirectoryInfo dir = new FileInfo(callerPath).Directory;
            for (int i = 0; i < 4 && dir != null; i++) dir = dir.Parent;
            Assert.That(dir, Is.Not.Null, "[CallerFilePath] 上溯失败 —— 仓根定位不可用");
            Assert.That(Directory.Exists(Path.Combine(dir.FullName, "unity", "Assets")), Is.True,
                $"仓根判定错误:{dir.FullName} 下无 unity/Assets");
            return dir.FullName;
        }
    }
}
