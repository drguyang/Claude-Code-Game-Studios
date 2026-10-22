// S18-1 AC ⑥ —— 规则九门(源码 + IL 双证)的判据测试。
//
// ══════════════════════════════════════════════════════════════════════════
// 本文件要证明的三件事(缺一即 AC ⑥ 未兑现)
// ══════════════════════════════════════════════════════════════════════════
//   ① **真工程绿** —— 对当前 `Assets/Sim` + `Assets/Sim.Contracts` 跑源码半,
//      对 `Sim.dll` + `Sim.Contracts.dll` 跑 IL 半,**都必须零违例**。
//      (现存两处合法形态 —— `EntityDeathGates.IsBattleInjury` 的 `return` 合取、
//       `RegistrySchema.Validate` 的 `throw` 守卫 —— 是 S2 白名单的**活体样本**;
//       它们若被判红,说明白名单写坏了。)
//   ② **门真会红**(非空转)—— 用**工程外探针目录**(`Path.GetTempPath()`)注入
//      四种违例形态,要求**每一种都被对应的规则号抓到**。
//      ⚠️ 没有这一条,「零违例」与「扫描面是空的」无法区分 —— 这正是 AC ⑥ 当初
//      被一句自述顶替掉的那个假绿形态。
//   ③ **两半各自有判别力** —— IL 半的 I1/I2/I3 用**真 DLL** 验:
//      `Sim.Contracts.dll` 内的合法代码应零命中,而对测试装配自身
//      (`Sim.Contracts.Tests.dll`,内含故意违例的夹具类型)应命中。
//      ⇒ 证明 IL 半不是「永远返回空」的空转。
//
// ⚠️ 测试写盘一律落 `Path.GetTempPath()`(承 `.claude/rules/test-standards.md` 第 3 条
//    与 `FsWriteBoundaryGate` 的 R1 判据 —— 本文件写的是**临时探针源文件**,不是仓库)。
//
// 权威:design/gdd/disease-simulation.md:243-245 / :356-358 ·
//      production/qa/qa-plan-sprint-18-2026-10-21.md:80(判据形态 = IL + 源码双证)·
//      unity/Assets/Editor.Tools.Gates/RuleNineGate.cs(被测对象)

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DaYiJingCheng.EditorTools.Gates;
using DaYiJingCheng.Sim;          // DiseaseRegistryEntry / DiseaseKind(IL 注入夹具用)
using NUnit.Framework;
using UnityEngine;

namespace DaYiJingCheng.Tests.Unit.Sim
{
    /// <summary>规则九门:真工程绿 + 四种注入形态各自转红 + IL 半非空转。</summary>
    [TestFixture]
    public sealed class RuleNineGateTest
    {
        private string _probeRoot;

        /// <summary>工程根 = `Application.dataPath` 的上级(含 `Assets/` 的那一层)。</summary>
        private static string UnityRoot() => Path.GetDirectoryName(Application.dataPath);

        [SetUp]
        public void Setup()
        {
            // 探针目录住临时区 —— 不写仓库(承 FsWriteBoundaryGate R1)。
            _probeRoot = Path.Combine(Path.GetTempPath(),
                "rulenine-probe-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(Path.Combine(_probeRoot, "Assets", "Sim"));
        }

        [TearDown]
        public void Teardown()
        {
            try { if (Directory.Exists(_probeRoot)) Directory.Delete(_probeRoot, true); }
            catch (IOException) { /* 临时目录清理失败不构成测试失败 */ }
        }

        /// <summary>把一段源码写成探针文件(`Assets/Sim/Probe.cs`)。</summary>
        private void WriteProbe(string source)
            => File.WriteAllText(Path.Combine(_probeRoot, "Assets", "Sim", "Probe.cs"), source);

        // ══════════════════════════════════════════════════════════════════════
        // ① 真工程绿(两半都跑)
        // ══════════════════════════════════════════════════════════════════════

        [Test]
        public void test_ruleNine_realTree_sourceHalfIsClean()
        {
            List<string> errs = RuleNineGate.CheckSource(UnityRoot());

            Assert.That(errs, Is.Empty,
                "真工程源码半出现规则九违例:\n  " + string.Join("\n  ", errs) +
                "\n⇒ 若这是**新增**的合法形态(数据成员判定 / 校验守卫),须把它的形态补进 " +
                "RuleNineGate.S2 白名单并写明理由;不得直接放宽判据。");
        }

        [Test]
        public void test_ruleNine_realTree_ilHalfIsClean()
        {
            var errs = new List<string>();
            foreach (string asmName in RuleNineGate.IlScanAssemblies)
            {
                string dll = AssemblyGates.ScriptAssemblyPath(asmName);
                Assert.That(File.Exists(dll), Is.True,
                    $"编译产物不存在「{dll}」—— IL 半无扫描面,不能判绿");
                errs.AddRange(RuleNineGate.CheckIl(dll, asmName));
            }

            Assert.That(errs, Is.Empty,
                "真工程 IL 半出现规则九违例:\n  " + string.Join("\n  ", errs));
        }

        /// <summary>
        /// **白名单活体样本** —— 现存两处合法形态必须**不被判红**。
        /// <para>这条钉的是「白名单过窄」方向:若有人收紧 S2 只认 `return`,
        /// `RegistrySchema.Validate` 的 `throw` 守卫会转红,本断言接住。</para>
        /// </summary>
        [Test]
        public void test_ruleNine_existingLegalForms_areNotFlagged()
        {
            List<string> errs = RuleNineGate.CheckSource(UnityRoot());

            // 两处合法形态的锚点(源文件 + 规则号不该出现在违例里)
            Assert.That(errs.Where(e => e.Contains("EntityDeathGates.cs")), Is.Empty,
                "EntityDeathGates.IsBattleInjury 的 `return` 合取是 S2 白名单的活体样本," +
                "被判红说明白名单写坏了");
            Assert.That(errs.Where(e => e.Contains("RegistrySchema.cs")), Is.Empty,
                "RegistrySchema.Validate 的 R1-31 `throw` 守卫是 S2 白名单的活体样本");
        }

        // ══════════════════════════════════════════════════════════════════════
        // ② 门真会红 —— 四种注入形态(非空转判据)
        // ══════════════════════════════════════════════════════════════════════

        [Test]
        public void test_ruleNine_S1_switchOnKind_isRed()
        {
            WriteProbe(@"
class Probe {
    void M(DiseaseRegistryEntry e) {
        switch (e.Kind) {
            case DiseaseKind.Injury: A(); break;
            default: B(); break;
        }
    }
    void A() {} void B() {}
}");
            List<string> errs = RuleNineGate.CheckSource(_probeRoot);
            Assert.That(errs.Any(e => e.Contains("[RuleNine/S1]")), Is.True,
                "S1 未抓到 `switch (e.Kind)` —— 门空转。实得:\n  " + string.Join("\n  ", errs));
        }

        [Test]
        public void test_ruleNine_S2_multiStatementIfOnKind_isRed()
        {
            WriteProbe(@"
class Probe {
    int M(DiseaseRegistryEntry e) {
        if (e.Kind == DiseaseKind.Injury) {
            int x = 1;
            return x;
        }
        return 0;
    }
}");
            List<string> errs = RuleNineGate.CheckSource(_probeRoot);
            Assert.That(errs.Any(e => e.Contains("[RuleNine/S2]")), Is.True,
                "S2 未抓到「`if (Kind == …)` 且分支体多条语句」—— 门空转。实得:\n  "
                + string.Join("\n  ", errs));
        }

        [Test]
        public void test_ruleNine_S3_ternaryOnKind_isRed()
        {
            WriteProbe(@"
class Probe {
    int M(DiseaseRegistryEntry e) {
        int v = e.Kind == DiseaseKind.Injury ? 1 : 2;
        return v;
    }
}");
            List<string> errs = RuleNineGate.CheckSource(_probeRoot);
            Assert.That(errs.Any(e => e.Contains("[RuleNine/S3]")), Is.True,
                "S3 未抓到以 kind 为条件的三元 —— 门空转。实得:\n  " + string.Join("\n  ", errs));
        }

        [Test]
        public void test_ruleNine_S4_hardcodedDiseaseKey_isRed()
        {
            WriteProbe(@"
class Probe {
    bool M(DiseaseRegistryEntry e) {
        return e.DiseaseKey == ""INJ_STAB"";
    }
}");
            List<string> errs = RuleNineGate.CheckSource(_probeRoot);
            Assert.That(errs.Any(e => e.Contains("[RuleNine/S4]")), Is.True,
                "S4 未抓到具体病种名硬编码 —— 这是规则九最直接的违例形态。实得:\n  "
                + string.Join("\n  ", errs));
        }

        /// <summary>
        /// **反向**:合法形态在探针里也必须放行 —— 否则门变成「见 Kind 就红」,
        /// 那是把数据驱动的正确写法一起禁掉,判据过宽 = 另一种静默失败。
        /// </summary>
        [Test]
        public void test_ruleNine_legalReturnConjunction_inProbe_isGreen()
        {
            WriteProbe(@"
class Probe {
    bool M(DiseaseRegistryEntry e) {
        return e.Kind == DiseaseKind.Injury && e.DiseaseKey != null;
    }
}");
            List<string> errs = RuleNineGate.CheckSource(_probeRoot);
            Assert.That(errs, Is.Empty,
                "单条 `return` 合取(数据成员判定)被判红 —— 判据过宽。" +
                "这会禁掉规则九**要求**的写法。实得:\n  " + string.Join("\n  ", errs));
        }

        /// <summary>
        /// **同名尾巴不得触发门** —— 这是首版实测的假红形态,必须钉住。
        /// <para>`EventKind` 是全仓 35 支事件的**路由**判据(ADR-024),
        /// `ClinicRoomClauseKind` 是医馆房间子句类型;两者都与病种类别无关。
        /// 若它们被判红,门会把 ADR-024 的按 Kind 路由一起禁掉 —— 判据过宽。</para>
        /// </summary>
        [Test]
        public void test_ruleNine_sameNamedKindTail_isNotFlagged()
        {
            WriteProbe(@"
class Probe {
    int A(SimEvent e) {
        switch (e.Kind) {
            case EventKind.CareApplied: return 1;
            case EventKind.PlayerDied: return 2;
            default: return 0;
        }
    }
    bool B(ClinicRoomClause c) {
        switch (c.Kind) {
            case ClinicRoomClauseKind.MinCount: return true;
            default: return false;
        }
    }
    int C(SimEvent e) {
        return e.Kind == EventKind.PlayerDied ? 1 : 0;
    }
}");
            List<string> errs = RuleNineGate.CheckSource(_probeRoot);
            Assert.That(errs, Is.Empty,
                "`EventKind` / `ClinicRoomClauseKind` 被当成规则九违例 —— 判据过宽(假红)。" +
                "这会禁掉 ADR-024 的按 Kind 路由。实得:\n  " + string.Join("\n  ", errs));
        }

        /// <summary>
        /// **注释不得触发门** —— 注记里写 `if (kind == injury)` 是文档,不是代码。
        /// </summary>
        [Test]
        public void test_ruleNine_commentedViolation_isGreen()
        {
            WriteProbe(@"
class Probe {
    // 反例:if (e.Kind == DiseaseKind.Injury) { A(); B(); }
    /* switch (e.Kind) { default: break; } */
    int M() { return 0; }
}");
            List<string> errs = RuleNineGate.CheckSource(_probeRoot);
            Assert.That(errs, Is.Empty,
                "注释里的违例被当成真代码 —— 门会误报,注释即违规。实得:\n  "
                + string.Join("\n  ", errs));
        }

        // ══════════════════════════════════════════════════════════════════════
        // ③ IL 半非空转
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// 产物缺失 ⇒ **记红**,不静默通过(拒以空集冒充绿)。
        /// </summary>
        [Test]
        public void test_ruleNine_ilHalf_missingDll_isRed()
        {
            string bogus = Path.Combine(_probeRoot, "NoSuch.dll");
            List<string> errs = RuleNineGate.CheckIl(bogus, "NoSuch");

            Assert.That(errs, Is.Not.Empty, "产物缺失时 IL 半返回空 —— 空集冒充绿");
            Assert.That(errs[0], Does.Contain("编译产物缺失"));
        }

        /// <summary>
        /// **IL 半判别力** —— 对**测试装配自身**跑 IL 半。
        /// <para>本文件内不含违例夹具(测试代码不写违例),故期望零命中;
        /// 关键判据是**扫描真跑到了方法体**(`methodCount &gt; 0`)——
        /// 否则「零命中」与「没扫到」不可分。</para>
        /// </summary>
        [Test]
        public void test_ruleNine_ilHalf_actuallyScansMethodBodies()
        {
            string dll = AssemblyGates.ScriptAssemblyPath("Sim.Contracts.Tests");
            Assert.That(File.Exists(dll), Is.True,
                $"测试装配产物不存在「{dll}」—— IL 半的判别力无法验证");

            // 先证明「扫描真跑了」:对真 DLL 跑,不应报「零方法体」
            List<string> errs = RuleNineGate.CheckIl(dll, "Sim.Contracts.Tests");
            Assert.That(errs.Any(e => e.Contains("零方法体")), Is.False,
                "IL 半扫真 DLL 却报零方法体 —— 扫描面丢失。实得:\n  " + string.Join("\n  ", errs));
        }

        /// <summary>
        /// **I1 / I2 / I3 的注入级反证** —— 让 <see cref="RuleNineGate.CheckIl"/> 扫
        /// **本测试装配自己的产物**(`Sim.Contracts.Tests.dll`),其中故意含三条违例方法。
        ///
        /// <para>⚠️ 为什么这样做(而不是 Cecil 注入):</para>
        /// <list type="number">
        /// <item>违例夹具是「按 `DiseaseKind` 分派的真生产代码」,**不能进 `Assets/Sim`**
        /// (否则门自己先红)—— 而本门的扫描面刻意不含 `Assets/Tests`
        /// (测试**应当**能用具体病种构造夹具,见门头注)。</item>
        /// <item>测试装配**没有** `Mono.Cecil` 的编译期引用(它是 `Editor.Tools.Gates` 的
        /// precompiled 依赖,不传递)⇒ 首版尝试用 Cecil 注入,编译期即 111 条 CS0234。
        /// 而「把违例写在测试源码里」让**编译器**替我生成 IL —— 更简单也更可信:
        /// 这是**真编译器**产出的 IL,不是手工拼的字节。</item>
        /// </list>
        ///
        /// <para>这条补齐了首版登记的**残余证据缺口**:此前 IL 半只有
        /// 「真工程零命中 + 扫描确实执行」两条支撑,**没有反证证明它真会红** ——
        /// 与 AC ⑥ 当初被一句自述顶替属同型风险。现在三条规则各有注入反证。</para>
        /// </summary>
        [Test]
        public void test_ruleNine_ilHalf_injectedViolations_areRed()
        {
            string dll = AssemblyGates.ScriptAssemblyPath("Sim.Contracts.Tests");
            Assert.That(File.Exists(dll), Is.True,
                $"测试装配产物不存在「{dll}」—— IL 半的注入级反证无扫描面");

            List<string> errs = RuleNineGate.CheckIl(dll, "Sim.Contracts.Tests");

            Assert.That(errs.Any(e => e.Contains("[RuleNine/I1]")), Is.True,
                "本装配内注入的 `switch (DiseaseKind)` 未被 I1 抓到 —— I1 是空转。实得:\n  "
                + string.Join("\n  ", errs));
            Assert.That(errs.Any(e => e.Contains("[RuleNine/I2]")), Is.True,
                "本装配内注入的 3 个 DiseaseKind 常量未被 I2 抓到 —— I2 是空转。实得:\n  "
                + string.Join("\n  ", errs));
            Assert.That(errs.Any(e => e.Contains("[RuleNine/I3]")), Is.True,
                "本装配内注入的 DiseaseKey 常量比较未被 I3 抓到 —— I3 是空转。实得:\n  "
                + string.Join("\n  ", errs));
        }

        // ══════════════════════════════════════════════════════════════════════
        // IL 注入夹具(故意违例 —— **只在测试装配内**,不进 Assets/Sim)
        // ══════════════════════════════════════════════════════════════════════
        // ⚠️ 以下三个方法**故意违反规则九**,唯一用途是给 `test_ruleNine_ilHalf_injectedViolations_areRed`
        //    提供真编译器产出的违例 IL。**绝不可**被当作可复制的写法 —— 生产侧的正确形态见
        //    `EntityDeathGates.IsBattleInjury`(单条 `return` 合取)。
        // ⚠️ 它们不进 `Assets/Sim` ⇒ 源码半扫不到(扫描面 = Sim / Sim.Contracts),
        //    只有 IL 半扫 `Sim.Contracts.Tests.dll` 时才命中。**这正是 IL 半独立价值的证明**:
        //    源码半对此完全失明,IL 半抓住了。

        /// <summary>I1 夹具:`switch` 按 `DiseaseKind` 分派。</summary>
        private static int InjectSwitchOnDiseaseKind(DiseaseRegistryEntry e)
        {
            switch (e.Kind)
            {
                case DiseaseKind.Injury: return 1;
                case DiseaseKind.Disease: return 2;
                case DiseaseKind.Poison: return 3;
                default: return 0;
            }
        }

        /// <summary>I2 夹具:同一方法内 ≥3 个 `DiseaseKind` 常量比较(穷举式)。</summary>
        private static bool InjectExhaustiveKindComparisons(DiseaseRegistryEntry e)
        {
            bool a = e.Kind == DiseaseKind.Injury;
            bool b = e.Kind == DiseaseKind.Disease;
            bool c = e.Kind == DiseaseKind.Poison;
            return a || b || c;
        }

        /// <summary>I3 夹具:`DiseaseKey` 与字符串常量做相等比较(硬编码病种名)。</summary>
        private static bool InjectHardcodedKeyComparison(DiseaseRegistryEntry e)
            => e.DiseaseKey == "INJ_STAB";

        /// <summary>
        /// **I1/I2/I3 判据号存在性**(与注入级反证互补:注入证明「会红」,本条证明「都实现了」)。
        /// </summary>
        /// <summary>
        /// **锚点活体守卫** —— IL 半锚在 `FieldType == DiseaseKind` 上。
        /// <para>若 `DiseaseKind` 改名 / 换命名空间 / `DiseaseRegistryEntry.Kind` 换类型,
        /// IL 半会**静默失去判别力**(永远零命中 ⇒ 永远绿)。本断言钉住:
        /// ① `DiseaseKindFullName` 常量与真类型一致;② 真 DLL 里 `DiseaseRegistryEntry.Kind`
        /// 的字段类型确实是它。</para>
        /// </summary>
        [Test]
        public void test_ruleNine_ilHalf_diseaseKindAnchorIsLive()
        {
            // ① 常量 ↔ 真类型(反射,不靠字符串信任)
            Type t = typeof(DaYiJingCheng.Sim.DiseaseKind);
            Assert.That(RuleNineGate.DiseaseKindFullName, Is.EqualTo(t.FullName),
                "DiseaseKindFullName 常量与真类型不符 —— IL 半的锚点已失效(会永远零命中)");

            // ② 真 DLL 里 DiseaseRegistryEntry.Kind 的字段类型
            var entryType = typeof(DaYiJingCheng.Sim.DiseaseRegistryEntry);
            var field = entryType.GetField("Kind");
            Assert.That(field, Is.Not.Null, "DiseaseRegistryEntry.Kind 字段不见了");
            Assert.That(field.FieldType, Is.EqualTo(t),
                "DiseaseRegistryEntry.Kind 不再是 DiseaseKind —— 规则九的管辖面变了," +
                "本门须重新裁定锚点");

            // ③ 源码半的锚点是字面 `DiseaseKind.` —— 确认全仓零裸写形态
            //    (裸写会让收紧后的判据漏报;若日后出现,须扩判据而不是放过)
            var offenders = new List<string>();
            foreach (string dir in RuleNineGate.ScanDirs)
            {
                string full = Path.Combine(UnityRoot(), dir);
                if (!Directory.Exists(full)) continue;
                foreach (string f in Directory.GetFiles(full, "*.cs", SearchOption.AllDirectories))
                {
                    if (System.Text.RegularExpressions.Regex.IsMatch(
                            File.ReadAllText(f), @"using\s+static\s+[\w.]*DiseaseKind\s*;"))
                        offenders.Add(f);
                }
            }
            Assert.That(offenders, Is.Empty,
                "出现 `using static …DiseaseKind;` ⇒ 裸写成员名会成为合法写法," +
                "源码半的 `DiseaseKind.` 锚点将漏报。须扩判据:\n  " + string.Join("\n  ", offenders));
        }

        [Test]
        public void test_ruleNine_ilHalf_hasThreeRuleFamilies()
        {
            // 判据存在性:三个规则号的报错串必须写在源里(否则「零命中」是因为没实现)
            string src = File.ReadAllText(Path.Combine(
                UnityRoot(), "Assets/Editor.Tools.Gates/RuleNineGate.cs"));

            Assert.That(src, Does.Contain("[RuleNine/I1]"), "I1 判据未实现");
            Assert.That(src, Does.Contain("[RuleNine/I2]"), "I2 判据未实现");
            Assert.That(src, Does.Contain("[RuleNine/I3]"), "I3 判据未实现");
            Assert.That(src, Does.Contain("[RuleNine/S1]"), "S1 判据未实现");
            Assert.That(src, Does.Contain("[RuleNine/S2]"), "S2 判据未实现");
            Assert.That(src, Does.Contain("[RuleNine/S3]"), "S3 判据未实现");
            Assert.That(src, Does.Contain("[RuleNine/S4]"), "S4 判据未实现");
        }

        // ══════════════════════════════════════════════════════════════════════
        // ④ 接线(门链上真的被调用)
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// `RunAll` 必须把**两半都**跑 —— 只有源码半 = AC ⑥ 的「IL + 源码双证」只兑现一半。
        /// <para>判据 = 读门源文本,`RunAll` 方法体内同时出现 `CheckSource` 与 `CheckIl`。</para>
        /// </summary>
        [Test]
        public void test_ruleNine_runAll_coversBothHalves()
        {
            string path = Path.Combine(UnityRoot(), "Assets/Editor.Tools.Gates/RuleNineGate.cs");
            string code = GateChainSource.StripCommentsAndLiterals(File.ReadAllText(path));

            // ⚠️ 不用 GateChainSource.MethodBody:它按「签名 + 第一个 {」定位,
            //   而本方法签名含默认参数 `= "."`(内含引号,已被 StripCommentsAndLiterals 剥成
            //   占位)—— 首版用它解析返回 null。改为按方法名锚定后自取花括号体。
            int sig = code.IndexOf("List<string> RunAll(string projectRoot", StringComparison.Ordinal);
            Assert.That(sig, Is.GreaterThanOrEqualTo(0), "RunAll 签名变了 —— 本断言须同步");

            int open = code.IndexOf('{', sig);
            Assert.That(open, Is.GreaterThanOrEqualTo(0), "RunAll 无方法体");

            int depth = 0, close = -1;
            for (int i = open; i < code.Length; i++)
            {
                if (code[i] == '{') depth++;
                else if (code[i] == '}' && --depth == 0) { close = i; break; }
            }
            Assert.That(close, Is.GreaterThan(open), "RunAll 花括号不配平");

            string body = code.Substring(open, close - open + 1);
            Assert.That(body, Does.Contain("CheckSource"), "RunAll 未跑源码半");
            Assert.That(body, Does.Contain("CheckIl"), "RunAll 未跑 IL 半 —— 双证只兑现一半");
        }
    }
}
