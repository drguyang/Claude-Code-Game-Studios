// 权威来源:GDD 规则十三(戥子输入契约四条)· 规则十二(方笺 = 39 同一本书,已裁)
//          · UI-11.1/UI-11.2 · AC-11-12/13/18/21 · TR-prescription-008/011/014/017
//          · ADR-011(量化在 3 / 呈现层;11 只见整数)· ADR-013 §五/§十-B
//          · ADR-020 §四 AC-20-03 先例(判据 = **反射断言,不是 grep**)
//          · ADR-018(无提示音铁律,AC-11-21 联合)
//          · Story: production/epics/prescription-medication/story-005-*.md
//
// 本件是 story-005 的**结构半边**(自动化可判部分)。它**不能顶替**走查半边:
//   AC-11-12(零数字零推荐)· AC-11-13(色盲 + 静音分辨相邻档)· AC-11-21(静音听测)
//   三条 [L] 判据须实现轮人工执行 + 签核,本件**零断言**覆盖它们。
//
// ⚠️ NOT-RUN 登记(禁借绿 —— 覆盖缺口,非安全洞):
//   1. **AC-11-18 ②**(42 侧无「下一档」焦点落点 + hi 档纸面反馈)——
//      BLOCKED-BY-42 元件落地(戥子黄铜元件本 epic 不实现);本件只证**消费方**序列长度正确。
//   2. **手柄(无指针)路径** —— BLOCKED-BY 桌面调试集中轮 + ADR-013 假设 6 spike(半可信)。
//   3. **AC-11-13 / AC-11-12 / AC-11-21 走查子项** —— 可判 ≠ 已判,须实现轮人工执行。
//   4. **真实 UX 夹具下的焦点单栈门实跑** —— 本件只做**结构**断言(装配面零第二 EventSystem);
//      引擎运行期双 EventSystem 的实跑判据归 42 侧 spike。
//   5. **`materia_lexicon.cooked` 真装载** —— 21a 产出方未落 C# 字段(承 story-004 NOT-RUN 3);
//      本件以烘焙 JSON 源件 + 结构扫描走通,产出方落位后仅换实现。
//   6. **`IsFocusEnabled = false` 分支** —— 生产件的门控位已可从装载期注入(本件有判据),
//      但**判定源**(戥子满档 / 缺药)归 20 库存扣减面,**尚未落地** ⇒ 恒 true 路径是当前唯一实跑路径。
//   7. **`PrescribeStrokeResult` / `PrescribeStrokeIntent` 的生产消费方** —— 全库零生产接线
//      (仅定义处 + 本测试);落笔 → 11 的接线归 8 侧 `S-8.4` 路线甲 + 39 方笺页落地。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using DaYiJingCheng.Gameplay.Presentation.Skeuomorphic;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.Prescription;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.PlayMode.PrescriptionMedication
{
    [TestFixture]
    public class dentch_input_test
    {
        private static string RepoRoot => ComputeRepoRoot();

        private static string ComputeRepoRoot([CallerFilePath] string thisFile = "")
            => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile) ?? ".",
                "..", "..", "..", "..", ".."));

        private static string UiDir => Path.Combine(RepoRoot, "unity", "Assets", "Gameplay.UI", "Skeuomorphic");
        private static string SelectorPath => Path.Combine(UiDir, "DentchDoseSelector.cs");
        private static string PresentationDir => Path.Combine(RepoRoot, "unity", "Assets", "Gameplay.Presentation");
        private static string SimPrescriptionDir =>
            Path.Combine(RepoRoot, "unity", "Assets", "Sim", "Prescription");
        private static string DataDir => Path.Combine(RepoRoot, "assets", "data");

        private static DrugProfile ProfileWith(DoseRange? range)
            => new DrugProfile { DoseRange = range };

        // ══ AC-11-18 ① / QA 卡「无 hi+1 落点」:落点数 == hi − lo + 1 ══════════

        [Test]
        public void test_detents_countEqualsHiMinusLoPlusOne()
        {
            var sel = new DentchDoseSelector(ProfileWith(new DoseRange(2, 5)), DentchDoseSelector.RegisteredMaxDoseDetents);
            // ⚠️ 订正:story 卡 QA 行写「dose_range=(2,5) ⇒ 长度 3」是**算术笔误**
            //    (5−2+1 = 4)。GDD 规则十三「`dose_range` 每一档 = 一个焦点落点」为权威公式。
            Assert.AreEqual(4, sel.Detents.Count, "dose_range=(2,5) ⇒ 落点数须 = 5−2+1 = 4(AC-11-18 ①)。");
            Assert.AreEqual(4, sel.TickCount, "黄铜刻度数须与落点数同源。");
        }

        [Test]
        public void test_detents_ordinalsAreOneBasedContiguous()
        {
            var sel = new DentchDoseSelector(ProfileWith(new DoseRange(3, 7)), DentchDoseSelector.RegisteredMaxDoseDetents);
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5 }, sel.Detents.Select(d => d.Ordinal).ToArray(),
                "档序数须 1 起连续 —— 不得出现第 hi+1 落点(AC-11-18 ①)。");
        }

        [Test]
        public void test_detents_focusRankIsSurjectiveAndInjective()
        {
            var sel = new DentchDoseSelector(ProfileWith(new DoseRange(1, 6)), DentchDoseSelector.RegisteredMaxDoseDetents);
            int k = sel.Detents.Count;
            // 真判据(值域恰 1..K):集合逐项相等 —— 42 侧的 AssertRankDataSurjective 只查
            // 「有无 null」,**不校验值域**(其源码自陈),单靠它满射半边是空的。
            CollectionAssert.AreEqual(Enumerable.Range(1, k).ToArray(),
                sel.Detents.Select(d => d.FocusRank).ToArray(), $"FocusRank 值域须恰为 1..{k}(AC-42-B1 满射)。");
            CollectionAssert.AreEqual(Enumerable.Range(1, k).ToArray(),
                sel.Detents.Select(d => d.Ordinal).ToArray(), "档序数值域须恰为 1..K。");
            Assert.AreEqual(k, sel.Detents.Select(d => d.FocusRank).Distinct().Count(), "rank 须互异(单射)。");
            // 42 侧既有断言照走(接口面 + null 守卫)。
            Assert.DoesNotThrow(() => FocusBoundaryAssertions.AssertRankDataSurjective(sel.Detents));
            Assert.DoesNotThrow(() => FocusBoundaryAssertions.AssertRankDataInjective(sel.Detents));
        }

        [Test]
        public void test_regularDetents_areNotWholeDose()
        {
            var sel = new DentchDoseSelector(ProfileWith(new DoseRange(2, 5)), DentchDoseSelector.RegisteredMaxDoseDetents);
            Assert.IsTrue(sel.Detents.All(d => !d.IsWholeDose),
                "常规档(dose_range 非空)的每个落点都**不得**标为整剂(整剂语义只属空域路径)。");
        }

        [Test]
        public void test_presentationLabel_containsNoDigits()
        {
            // AC-11-12 零数字读数:可见读数由黄铜形态承载,标签不得含阿拉伯数字。
            var regular = new DentchDoseSelector(ProfileWith(new DoseRange(2, 5)), DentchDoseSelector.RegisteredMaxDoseDetents);
            var whole = new DentchDoseSelector(ProfileWith(null), DentchDoseSelector.RegisteredMaxDoseDetents);
            foreach (var d in regular.Detents.Concat(whole.Detents))
            {
                Assert.IsNotEmpty(d.PresentationLabel, "标签不得为空。");
                Assert.IsFalse(d.PresentationLabel.Any(char.IsDigit),
                    $"标签「{d.PresentationLabel}」含数字 —— 破 AC-11-12 零数字读数。");
            }
            Assert.AreEqual("整剂", whole.Detents[0].PresentationLabel, "整剂落点的标签须为「整剂」。");
        }

        [Test]
        public void test_detents_atTopDetent_hasNoNextDetent()
        {
            var sel = new DentchDoseSelector(ProfileWith(new DoseRange(2, 5)), DentchDoseSelector.RegisteredMaxDoseDetents);
            Assert.IsTrue(sel.IsAtTopDetent(4), "顶档(ordinal = 落点数)须报「已到顶」。");
            Assert.IsFalse(sel.IsAtTopDetent(3), "非顶档不得报「已到顶」。");
            // 「下一档」的语义 = ordinal + 1 越出集合 ⇒ 消费方无落点可去(焦点不动)。
            Assert.Greater(5, sel.Detents.Count, "hi 之后不得存在第 5 个落点(AC-11-18 ①)。");
        }

        // ══ 单落点整剂(联动 AC-11-17)══════════════════════════════════════

        [Test]
        public void test_wholeDose_nullRange_yieldsExactlyOneDetent()
        {
            var sel = new DentchDoseSelector(ProfileWith(null), DentchDoseSelector.RegisteredMaxDoseDetents);
            Assert.IsTrue(sel.IsWholeDose);
            Assert.AreEqual(1, sel.Detents.Count, "空 dose_range ⇒ 恰 1 个「整剂」落点,非 0 落点(AC-11-17 呈现侧同形)。");
            Assert.IsTrue(sel.Detents[0].IsWholeDose, "该唯一落点须承载「整剂」语义。");
            Assert.AreEqual(1, sel.Detents[0].Ordinal, "整剂落点 ordinal = 1(呈现上不是「0 档」)。");
        }

        [Test]
        public void test_focusEnabled_injectedFalse_propagatesToAllDetents()
        {
            // 门控位可从装载期注入(判定源归 20,登记 NOT-RUN 6)。
            var on = new DentchDoseSelector(ProfileWith(new DoseRange(1, 3)), DentchDoseSelector.RegisteredMaxDoseDetents);
            Assert.IsTrue(on.Detents.All(d => d.IsFocusEnabled), "默认门控位须为 true。");

            var off = new DentchDoseSelector(ProfileWith(new DoseRange(1, 3)), DentchDoseSelector.RegisteredMaxDoseDetents,
                                             isFocusEnabled: false);
            Assert.IsTrue(off.Detents.All(d => !d.IsFocusEnabled),
                "门控位注入 false 须传播到全部落点(否则该分支不可表达)。");
        }

        [Test]
        public void test_focusEnabled_wholeDosePath_respectsInjection()
        {
            var off = new DentchDoseSelector(ProfileWith(null), DentchDoseSelector.RegisteredMaxDoseDetents,
                                             isFocusEnabled: false);
            Assert.IsFalse(off.Detents[0].IsFocusEnabled, "整剂路径亦须尊重门控位注入。");
        }

        [Test]
        public void test_wholeDose_doseValueIsOne()
        {
            var sel = new DentchDoseSelector(ProfileWith(null), DentchDoseSelector.RegisteredMaxDoseDetents);
            Assert.AreEqual(1, sel.DoseValueOf(1), "整剂 ⇒ 档值 1(与 11 的 ResolveEffectiveDose 同域,AC-11-17)。");
        }

        // ══ 档值换算:整数、无 clamp ═══════════════════════════════════════

        [Test]
        public void test_doseValue_isLoPlusOrdinalMinusOne()
        {
            var sel = new DentchDoseSelector(ProfileWith(new DoseRange(2, 5)), DentchDoseSelector.RegisteredMaxDoseDetents);
            Assert.AreEqual(2, sel.DoseValueOf(1));
            Assert.AreEqual(3, sel.DoseValueOf(2));
            Assert.AreEqual(4, sel.DoseValueOf(3));
            Assert.AreEqual(5, sel.DoseValueOf(4), "顶档 ⇒ 档值 = hi(不得溢出到 hi+1)。");
        }

        [Test]
        public void test_doseValue_outOfRange_throwsInsteadOfClamping()
        {
            var sel = new DentchDoseSelector(ProfileWith(new DoseRange(2, 5)), DentchDoseSelector.RegisteredMaxDoseDetents);
            // 零 clamp 的**行为**判据:越界不得被静默夹回 [lo,hi],须如实抛。
            Assert.Throws<ArgumentOutOfRangeException>(() => sel.DoseValueOf(5),
                "ordinal 越界须抛(零 clamp 路径)—— 静默夹回 = AC-11-18 要防的失败模式。");
            Assert.Throws<ArgumentOutOfRangeException>(() => sel.DoseValueOf(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => sel.DoseValueOf(-1));
        }

        // ══ MAX_DOSE_DETENTS 旋钮(合成 hi−lo 扫 1…9)════════════════════════

        [Test]
        public void test_maxDoseDetents_syntheticSweep_oneToNine_generatesDeterministically()
        {
            // QA 卡:合成 hi−lo 扫 1…9 证明焦点序列随档数确定生成。
            for (int k = 1; k <= DentchDoseSelector.RegisteredMaxDoseDetents; k++)
            {
                var sel = new DentchDoseSelector(ProfileWith(new DoseRange(1, k)), k);
                Assert.AreEqual(k, sel.Detents.Count, $"档数 {k} ⇒ 落点数须 = {k}。");
                CollectionAssert.AreEqual(Enumerable.Range(1, k).ToArray(),
                    sel.Detents.Select(d => d.Ordinal).ToArray(), $"档数 {k} 的序数须 1..{k}。");
                Assert.DoesNotThrow(() => FocusBoundaryAssertions.AssertRankDataInjective(sel.Detents));
            }
        }

        [Test]
        public void test_maxDoseDetents_exceeded_throwsInsteadOfTruncating()
        {
            // 上限 = 3,域给 4 档 ⇒ 硬失败(不静默截断 —— 截断 = 玩家以为选到了 hi)。
            var ex = Assert.Throws<InvalidOperationException>(
                () => new DentchDoseSelector(ProfileWith(new DoseRange(1, 4)), 3));
            StringAssert.Contains("MAX_DOSE_DETENTS", ex.Message);
        }

        [Test]
        public void test_maxDoseDetents_nonPositive_throwsAtAssembly()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new DentchDoseSelector(ProfileWith(new DoseRange(1, 3)), 0),
                "旋钮 ≤ 0 = 装配错误,须在装载期硬失败。");
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new DentchDoseSelector(ProfileWith(new DoseRange(1, 3)), -1));
        }

        [Test]
        public void test_doseRange_invertedBounds_throwsInsteadOfEmptySet()
        {
            var ex = Assert.Throws<InvalidOperationException>(
                () => new DentchDoseSelector(ProfileWith(new DoseRange(5, 2)), DentchDoseSelector.RegisteredMaxDoseDetents));
            StringAssert.Contains("dose_range", ex.Message);
        }

        // ══ 正控:夹具真能触发(防恒真)═══════════════════════════════════

        [Test]
        public void test_positiveControl_mutatedReferenceDiffersFromProduction()
        {
            // 真正控:用一个**独立参考实现**(本测自写,不经被测件)复算正确公式,
            // 再算变异公式(hi−lo+2 / 漏 −1),证两者与生产件输出可分辨 ——
            // 「改坏一行 ⇒ 断言红」由此在**测试侧**可证,而非只复算生产件常量。
            var production = DentchDoseSelector.BuildDetents(new DoseRange(2, 5), DentchDoseSelector.RegisteredMaxDoseDetents);

            int ReferenceCount(int lo, int hi) => hi - lo + 1;            // 正确
            int MutatedCountA(int lo, int hi) => hi - lo + 2;             // 变异:多一个落点
            Assert.AreEqual(ReferenceCount(2, 5), production.Length, "生产件落点数须等于独立参考公式。");
            Assert.AreNotEqual(MutatedCountA(2, 5), production.Length,
                "正控:变异公式(hi−lo+2)须与生产件输出可分辨 ⇒ 计数断言非恒真。");

            var sel = new DentchDoseSelector(ProfileWith(new DoseRange(2, 5)), DentchDoseSelector.RegisteredMaxDoseDetents);
            int ReferenceDose(int lo, int ordinal) => lo + (ordinal - 1);  // 正确
            int MutatedDose(int lo, int ordinal) => lo + ordinal;          // 变异:漏 −1
            for (int ord = 1; ord <= sel.Detents.Count; ord++)
                Assert.AreEqual(ReferenceDose(2, ord), sel.DoseValueOf(ord), $"档 {ord} 的档值须等于参考公式。");
            Assert.AreNotEqual(MutatedDose(2, 1), sel.DoseValueOf(1),
                "正控:变异公式(漏 −1)须可分辨 ⇒ 档值断言非恒真。");
        }

        // ══ 输入契约(规则十三):11 入参无浮点 ══════════════════════════════

        [Test]
        public void test_prescribeRequest_hasNoFloatingPointInPublicSurface()
        {
            // 承 AC-20-03 先例:判据 = **反射断言,不是 grep**。
            Type req = typeof(PrescribeRequest);
            foreach (var f in req.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                AssertNoFloatingPoint(f.FieldType, $"{req.Name}.{f.Name}");

            foreach (var c in req.GetConstructors())
                foreach (var p in c.GetParameters())
                    AssertNoFloatingPoint(p.ParameterType, $"{req.Name}..ctor({p.Name})");
        }

        [Test]
        public void test_prescribeStrokeIntent_hasNoFloatingPoint()
        {
            Type t = typeof(PrescribeStrokeIntent);
            foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                AssertNoFloatingPoint(f.FieldType, $"{t.Name}.{f.Name}");
        }

        [Test]
        public void test_strokeIntent_doseOrdinalIsInt_notFloat()
        {
            var f = typeof(PrescribeStrokeIntent).GetField("DoseOrdinal");
            Assert.IsNotNull(f, "落笔意图须有 DoseOrdinal 字段。");
            Assert.AreEqual(typeof(int), f.FieldType, "档序数须为 int —— float→int 量化住 3 / 表现层(规则十三)。");
        }

        [Test]
        public void test_dentchDoseSelector_publicSurfaceHasNoFloatingPoint()
        {
            Type t = typeof(DentchDoseSelector);
            foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                AssertNoFloatingPoint(p.PropertyType, $"{t.Name}.{p.Name}");
            foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (m.IsSpecialName) continue;
                AssertNoFloatingPoint(m.ReturnType, $"{t.Name}.{m.Name}()");
                foreach (var p in m.GetParameters())
                    AssertNoFloatingPoint(p.ParameterType, $"{t.Name}.{m.Name}({p.Name})");
            }
        }

        private static void AssertNoFloatingPoint(Type t, string path)
        {
            if (t.IsArray) t = t.GetElementType();
            if (t.IsGenericType) foreach (var a in t.GetGenericArguments()) AssertNoFloatingPoint(a, path);
            Assert.AreNotEqual(typeof(float), t, $"{path} 携带 float(规则十三:11 只见整数)。");
            Assert.AreNotEqual(typeof(double), t, $"{path} 携带 double(规则十三:11 只见整数)。");
            Assert.AreNotEqual(typeof(decimal), t, $"{path} 携带 decimal(规则十三:11 只见整数)。");
        }

        // ══ 零 clamp / 零浮点 源码扫描(含正控)═══════════════════════════

        [Test]
        public void test_dentchSelector_sourceHasNoClampCall()
        {
            string src = StripCommentsAndStrings(File.ReadAllText(SelectorPath));
            var clamp = new Regex(@"\b(Math\.(Min|Max|Clamp)|Clamp)\s*\(");
            var hits = clamp.Matches(src).Cast<Match>().Select(m => m.Value).ToList();
            CollectionAssert.IsEmpty(hits,
                "戥子链路须零 clamp 调用(AC-11-18 ①:限位由档位集合给出,非运行期 clamp)。");
        }

        [Test]
        public void test_dentchSelector_sourceHasNoFloatingPoint()
        {
            string src = StripCommentsAndStrings(File.ReadAllText(SelectorPath));
            var floaty = new Regex(@"\b(float|double|decimal)\b|\b\d+\.\d+[fdm]?\b");
            var hits = floaty.Matches(src).Cast<Match>().Select(m => m.Value).ToList();
            CollectionAssert.IsEmpty(hits, "戥子链路须零浮点(ADR-006 边界 / 规则十三)。");
        }

        [Test]
        public void test_stripHelper_positiveControl_removesCommentButKeepsCode()
        {
            // 正控:剥离器若被改成恒返回空,上面两条断言将真空绿 ⇒ 此处证剥离器真在工作。
            const string sample = "// Math.Clamp(1,2,3)\nint x = 1; // float f\n";
            string stripped = StripCommentsAndStrings(sample);
            StringAssert.DoesNotContain("Math.Clamp", stripped, "注释须被剥离。");
            StringAssert.DoesNotContain("float f", stripped, "行尾注释须被剥离。");
            StringAssert.Contains("int x = 1;", stripped, "代码 token 须保留(不得把整行剥掉)。");
        }

        [Test]
        public void test_brassDir_hasNoNumericBadgeToken()
        {
            // 真正会挂角标的是 Brass/ 目录(黄铜侧读数元件),原稿只扫 SelectorPath 单文件 ⇒ 面过窄。
            string brassDir = Path.Combine(UiDir, "Brass");
            var files = Directory.GetFiles(brassDir, "*.cs", SearchOption.AllDirectories);
            Assert.IsNotEmpty(files, "Brass/ 扫描面为空 ⇒ 零命中假绿。");
            foreach (var f in files)
            {
                string src = StripCommentsAndStrings(File.ReadAllText(f));
                foreach (var banned in new[] { "Badge", "Counter", "Numeric", "Label3D", "ProgressFill" })
                    StringAssert.DoesNotContain(banned, src,
                        $"{Path.GetFileName(f)} 含降级读数节点「{banned}」(两栈皆禁,无例外)。");
            }
        }

        [Test]
        public void test_forbiddenTokenScan_positiveControl_detectsKnownBadgeFragment()
        {
            // 正控:上面两处「零命中」若词面表被改成恒不匹配,将真空绿 ⇒ 此处证词面真能命中。
            const string sample = "var Badge = new VisualElement(); // Badge";
            string stripped = StripCommentsAndStrings(sample);
            StringAssert.Contains("Badge", stripped, "正控:代码面须保留 Badge token(证词面可命中)。");
            StringAssert.DoesNotContain("Badge", StripCommentsAndStrings("// Badge only in comment"),
                "正控:注释里的 token 须被剥离(证判据面 = 代码面)。");
        }

        [Test]
        public void test_scanSurface_isNotEmpty_andIncludesSelector()
        {
            // 守卫:扫描面若为空,上面的「零命中」是假绿。
            var files = Directory.GetFiles(UiDir, "*.cs", SearchOption.AllDirectories);
            Assert.IsNotEmpty(files, "扫描面为空 ⇒ 零命中断言假绿。");
            CollectionAssert.Contains(files, SelectorPath, "DentchDoseSelector.cs 须在扫描面内。");
        }

        // ══ 方笺 = 同一本书(ModalId 闭集仍 7 员 / 零相机意图)══════════════

        [Test]
        public void test_modalId_closedSetIsNonePlusSeven()
        {
            var names = Enum.GetNames(typeof(ModalId));
            Assert.AreEqual(8, names.Length, "ModalId = None + 7 员(方笺**不**增员,ADR-013 §十-B)。");
            Assert.AreEqual(7, names.Count(n => n != "None"), "闭集实员须仍为 7。");
        }

        [Test]
        public void test_modalId_hasNoCasebookPageMember()
        {
            // 方笺 = 脉案同一本书内的一页 ⇒ 不得出现新模态成员。
            var names = Enum.GetNames(typeof(ModalId));
            foreach (var banned in new[] { "Prescription", "Formula", "FangJian", "Dentch", "Dose" })
                CollectionAssert.DoesNotContain(names, banned,
                    $"方笺不得成为新模态成员「{banned}」(规则十二:同一本书)。");
        }

        [Test]
        public void test_modalId_casebookExists_distinctFromPaperCloseup48()
        {
            // 方笺的宿主 = Casebook(39),且第七员 PaperCloseup48 与本条无关(两处「勿混读」)。
            Assert.IsTrue(Enum.IsDefined(typeof(ModalId), "Casebook"), "方笺宿主须为 Casebook(39 脉案)。");
            Assert.IsTrue(Enum.IsDefined(typeof(ModalId), "PaperCloseup48"),
                "第七员 PaperCloseup48 须在集内(与本条无关 —— 勿混读)。");
            Assert.AreNotEqual((int)ModalId.Casebook, (int)ModalId.PaperCloseup48,
                "Casebook 与 PaperCloseup48 是两个成员(勿混读)。");
        }

        [Test]
        public void test_prescriptionSurface_hasNoCameraIntent()
        {
            // TR-prescription-014:11 零相机档位意图;39 是 Casebook 档唯一请求方。
            // 判据 = 反射(承 AC-20-03),扫 11 与戥子两处公开面。
            foreach (var t in new[] { typeof(PrescribeRequest), typeof(PrescribeStrokeIntent), typeof(DentchDoseSelector) })
            {
                foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                {
                    if (m.IsSpecialName) continue;
                    Assert.AreNotEqual("ICameraRig", m.ReturnType.Name, $"{t.Name}.{m.Name} 返回相机档位意图。");
                    foreach (var p in m.GetParameters())
                        Assert.AreNotEqual("ICameraRig", p.ParameterType.Name, $"{t.Name}.{m.Name}({p.Name}) 接收相机档位意图。");
                }
                foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                    Assert.AreNotEqual("ICameraRig", f.FieldType.Name, $"{t.Name}.{f.Name} 携带相机档位意图。");
            }
        }

        [Test]
        public void test_selectorSource_hasNoCameraOrModalIntent()
        {
            string src = StripCommentsAndStrings(File.ReadAllText(SelectorPath));
            foreach (var banned in new[] { "ICameraRig", "RequestModal", "OpenModal", "ModalId." })
                StringAssert.DoesNotContain(banned, src, $"戥子链路不得出现「{banned}」(零相机意图 / 不成新模态)。");
        }

        // ══ 焦点官方桥:零第二 EventSystem / 零自实现焦点算法 ══════════════

        [Test]
        public void test_selector_doesNotImplementFocusAlgorithm()
        {
            // ADR-013 §五:焦点走官方桥(引擎自动邻居);不自实现焦点算法。
            string src = StripCommentsAndStrings(File.ReadAllText(SelectorPath));
            // ⚠️ 判据面只收**焦点算法实现**类符号 —— 实现 IFocusable 接口是合法的
            //    (承 AC-42-B1 rank 契约),不得把接口名当违例(否则断言过宽 = 逼着不实现契约)。
            foreach (var banned in new[] { "NavigationMoveEvent", "FocusController", "EventSystem",
                                           "FocusableNavigation", "UnityEngine.UIElements" })
                StringAssert.DoesNotContain(banned, src,
                    $"戥子链路不得引用「{banned}」—— 焦点算法归官方桥,消费方只出落点数据(ADR-013 §五)。");
            // 反向守卫:接口实现须真在(否则上面的「零命中」可由「啥都没实现」冒充绿)。
            StringAssert.Contains("IFocusable", src, "戥子落点须实现 IFocusable rank 契约(AC-42-B1)。");
        }

        [Test]
        public void test_presentationSurface_exposesNoNeighborEnumeration()
        {
            // AC-42-B4:IFocusNavigationPresenter 导出面仅 IsFocusActive,无邻居枚举 / 几何查询。
            var members = typeof(IFocusNavigationPresenter)
                .GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => m.MemberType == MemberTypes.Property || m.MemberType == MemberTypes.Method)
                .Select(m => m.Name)
                .ToList();
            CollectionAssert.IsEmpty(members.Where(n => n.Contains("Neighbor") || n.Contains("Geometry") || n.Contains("Project")),
                "呈现契约不得导出邻居枚举 / 几何查询入口(AC-42-B4)。");
        }

        [Test]
        public void test_eventSystemPredicate_positiveControl_dualEventSystemFixtureIsDetected()
        {
            // 故事卡 QA 卡点名要求的**双 EventSystem 夹具**:证 `IsOrDerivesFrom` 谓词
            // 真能命中 EventSystem 派生类型(否则上一条「零第二 EventSystem」是恒真空真)。
            var probe = typeof(DualEventSystemProbe);
            Assert.IsTrue(IsOrDerivesFrom(probe, "EventSystem"),
                "正控:EventSystem 派生类型须被谓词命中 —— 否则零第二 EventSystem 断言无证伪力。");
            Assert.IsFalse(IsOrDerivesFrom(typeof(DentchDoseSelector), "EventSystem"),
                "反控:普通类型不得被误判为 EventSystem 派生。");
        }

        [Test]
        public void test_assemblySurface_hasNoSecondEventSystemType()
        {
            // 结构半边:全库不得出现第二个 EventSystem 派生类型(焦点单栈门 / 两栈共用同一 EventSystem)。
            // ⚠️ 引擎运行期实跑判据归 42 侧 spike(NOT-RUN 4)。
            var asm = typeof(DentchDoseSelector).Assembly;
            var derived = asm.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && IsOrDerivesFrom(t, "EventSystem"))
                .Select(t => t.FullName)
                .ToList();
            CollectionAssert.IsEmpty(derived, "Gameplay.UI 内不得出现 EventSystem 派生类型(禁第二 EventSystem)。");
        }

        // ══ 词表呈现侧(TR-prescription-011):功效词 + DTO 洁净 ═══════════

        [Test]
        public void test_lexiconBakedSource_hasEfficacyWordField_andNoDiseaseKey()
        {
            string json = File.ReadAllText(Path.Combine(DataDir, "materia_lexicon.json"));
            // ① 功效词为字符串字段(烘焙期转出);② 源件内不得出现病种键。
            StringAssert.Contains("功效词", json, "本草词表须含「功效词」字段(TR-prescription-011)。");
            foreach (var banned in new[] { "disease_id", "diseaseId", "treatable_by", "indication_match" })
                StringAssert.DoesNotContain(banned, json, $"词表源件不得含「{banned}」(病种 id 止步构建期)。");
            // ③ 零自动匹配逻辑:词表条目不得含命中 / 匹配 / 排序字段。
            foreach (var banned in new[] { "\"match\"", "\"hit\"", "\"score\"", "\"rank\"" })
                StringAssert.DoesNotContain(banned, json, $"词表不得含匹配语义字段「{banned}」(零自动匹配)。");
        }

        [Test]
        public void test_strokeIntent_dtoIsCleanUnderRecursiveGuard()
        {
            // 真判据:走 42/44 共用的递归扫描器(AC-37-15 · AC-44-B2),非自写词面扫描。
            // ⚠️ 守卫住 Editor.Tools.Gates(编辑期门,includePlatforms = Editor)⇒
            //    PlayMode 程序集**不可编译期引用**(Unity 禁止非编辑期 asmdef 引编辑期 asmdef)。
            //    故经反射取用:编辑器中跑 PlayMode 时编辑期程序集已装载,判据面等价。
            var errs = ScanWithDtoGuard(typeof(PrescribeStrokeIntent));
            CollectionAssert.IsEmpty(errs,
                "落笔意图 DTO 须零病种 / 诊断语义(递归扫描):" + string.Join(" | ", errs));
        }

        [Test]
        public void test_strokeResult_dtoIsCleanUnderRecursiveGuard()
        {
            var errs = ScanWithDtoGuard(typeof(PrescribeStrokeResult));
            CollectionAssert.IsEmpty(errs, "落笔回执 DTO 须零病种语义:" + string.Join(" | ", errs));
        }

        [Test]
        public void test_dtoGuard_positiveControl_flagsDiagnosticBearingType()
        {
            // 正控:守卫若被改成恒返回空,上面两条将真空绿 ⇒ 此处证守卫真能抓到。
            var errs = ScanWithDtoGuard(typeof(DiagnosticBearingProbe));
            Assert.IsNotEmpty(errs, "正控:携带诊断语义的探针类型须被守卫命中(否则守卫恒真空绿)。");
        }

        /// <summary>经反射调用 <c>PresentationDtoGuard.Scan</c>(编辑期门,PlayMode 不可编译期引用)。
        /// 守卫缺席 ⇒ 硬失败(不得静默返回空集冒充绿)。</summary>
        private static IReadOnlyList<string> ScanWithDtoGuard(Type dtoRoot)
        {
            var guard = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("DaYiJingCheng.EditorTools.Gates.PresentationDtoGuard", throwOnError: false))
                .FirstOrDefault(t => t != null);
            Assert.IsNotNull(guard,
                "PresentationDtoGuard 未装载(Editor.Tools.Gates)—— 守卫缺席不得静默通过(禁借绿)。");
            var scan = guard.GetMethod("Scan", BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(scan, "PresentationDtoGuard.Scan(Type) 未找到。");
            return (IReadOnlyList<string>)scan.Invoke(null, new object[] { dtoRoot });
        }

        [Test]
        public void test_strokeIntent_doseOrdinalSharesDomainWithPrescribeRequest()
        {
            // 生产件注释声称「本类型是呈现层交给 11 的唯一输入形状」⇒ 两者的剂量域须同型。
            var stroke = typeof(PrescribeStrokeIntent).GetField("DoseOrdinal");
            var req = typeof(PrescribeRequest).GetField("SelectedDose");
            Assert.IsNotNull(stroke); Assert.IsNotNull(req);
            Assert.AreEqual(req.FieldType, stroke.FieldType,
                "落笔意图的档序数须与 11 的 SelectedDose 同域(同型整数)。");
        }

        [Test]
        public void test_strokeIntent_carriesNoHitOrRecommendationSemantics()
        {
            // 零「命中」勾选 / 高亮 / 排序(TR-prescription-011 ②)。
            var fields = typeof(PrescribeStrokeIntent)
                .GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Select(f => f.Name).ToList();
            foreach (var banned in new[] { "Hit", "Match", "Recommended", "Highlight", "Rank", "Score", "ContraindicationWarning" })
                CollectionAssert.DoesNotContain(fields, banned, $"落笔意图不得含「{banned}」语义。");
        }

        // ══ 零降级读数(两栈皆禁,无例外)═══════════════════════════════

        [Test]
        public void test_selector_hasNoNumericBadgeOrCountNode()
        {
            // ⚠️ 判据面 = **代码 token**(注释与字符串已剥离)—— 本目录另受 AC-42-F3
            //    词面门扫原文(world_billboard_test),那门连注释也扫,故生产件里连
            //    「用于声明禁止」的禁用词本身都不能出现;本测不重复那门的词面面,
            //    只补它做不到的**剥注释后的代码面**(承 story-004 n4 判据面等价性口径)。
            string src = StripCommentsAndStrings(File.ReadAllText(SelectorPath));
            foreach (var banned in new[] { "Badge", "Counter", "Numeric", "Label3D", "ProgressFill" })
                StringAssert.DoesNotContain(banned, src, $"戥子元件不得含降级读数节点「{banned}」(两栈皆禁,无例外)。");
        }

        [Test]
        public void test_brassScaleElement_isBrassBranch_negativeControl()
        {
            // AC-42-F3:材质分支须把戥子读数元件归**黄铜分支**。
            // 真元件在 42 侧(BrassScaleElement,黄铜分支);此处断言其 class 标记 + 反控(墨侧标记不存在)。
            var t = typeof(DaYiJingCheng.Gameplay.UI.Skeuomorphic.Brass.BrassScaleElement);
            var instance = (UnityEngine.UIElements.VisualElement)Activator.CreateInstance(t);
            Assert.IsTrue(instance.ClassListContains("brass"), "戥子读数元件须归黄铜分支(AC-42-F3)。");
            Assert.IsTrue(instance.ClassListContains("brass-scale"), "须带 brass-scale 标记。");
            Assert.IsFalse(instance.ClassListContains("ink"), "戥子读数元件不得归墨侧分支(反控)。");
        }

        [Test]
        public void test_loadInto_brassElement_tickCountIsBound()
        {
            // 真跨组件绑定:刻度数由本类**装载**进黄铜侧元件(禁第二真源)。
            // ⚠️ 原稿断言 `Detents.Count == TickCount` 是 `x == x` 恒真(两者同源同一字段);
            //    本测改走真绑定路径 —— 未调 LoadInto 前元件刻度数须为默认,调后须等于落点数。
            var sel = new DentchDoseSelector(ProfileWith(new DoseRange(1, 5)), DentchDoseSelector.RegisteredMaxDoseDetents);
            var element = new DaYiJingCheng.Gameplay.UI.Skeuomorphic.Brass.BrassScaleElement();
            Assert.AreEqual(0, element.TickCount, "未装载前元件刻度数须为默认值(反控:证本测非恒真)。");

            sel.LoadInto(element);
            Assert.AreEqual(sel.Detents.Count, element.TickCount,
                "装载后黄铜元件刻度数须等于落点数(禁第二真源)。");
            Assert.AreEqual(5, element.TickCount, "dose_range=(1,5) ⇒ 5 个落点。");
        }

        [Test]
        public void test_loadInto_nullElement_throws()
        {
            var sel = new DentchDoseSelector(ProfileWith(new DoseRange(1, 3)), DentchDoseSelector.RegisteredMaxDoseDetents);
            Assert.Throws<ArgumentNullException>(() => sel.LoadInto(null));
        }

        // ══ 零进程态可重建(11/呈现层无「当前剂量」驻留)═════════════════

        [Test]
        public void test_selector_holdsNoMutableSelectedDoseState()
        {
            // 状态在表现层焦点上(引擎 FocusController),本类**不得**缓存「当前剂量」。
            var settable = typeof(DentchDoseSelector)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanWrite).Select(p => p.Name).ToList();
            CollectionAssert.IsEmpty(settable, "戥子选择器须全只读 —— 零「当前剂量」驻留(承 ADR-013 §9 C3)。");

            var fields = typeof(DentchDoseSelector)
                .GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(f => !f.IsInitOnly).Select(f => f.Name).ToList();
            CollectionAssert.IsEmpty(fields, "戥子选择器不得有可写实例字段(零进程态)。");
        }

        [Test]
        public void test_rebuildFromSameProfile_yieldsIdenticalView()
        {
            // 重开方笺 ⇒ 由库存 + 已落流事件重建视图(不读缓存):同输入两次构造须逐项相等。
            var a = new DentchDoseSelector(ProfileWith(new DoseRange(2, 5)), DentchDoseSelector.RegisteredMaxDoseDetents);
            var b = new DentchDoseSelector(ProfileWith(new DoseRange(2, 5)), DentchDoseSelector.RegisteredMaxDoseDetents);
            CollectionAssert.AreEqual(
                a.Detents.Select(d => (d.Ordinal, d.FocusRank, d.IsFocusEnabled, d.IsWholeDose)).ToArray(),
                b.Detents.Select(d => (d.Ordinal, d.FocusRank, d.IsFocusEnabled, d.IsWholeDose)).ToArray(),
                "同档案两次重建须逐项相等(零缓存 / 确定重建)。");
        }

        // ══ 11 侧零 clamp(AC-11-18 ① 的 11 半边,消费方复证)══════════════

        [Test]
        public void test_simPrescription_sourceHasNoClampCall()
        {
            var files = Directory.GetFiles(SimPrescriptionDir, "*.cs", SearchOption.AllDirectories);
            Assert.IsNotEmpty(files, "扫描面为空 ⇒ 零命中假绿。");
            var clamp = new Regex(@"\b(Math\.(Min|Max|Clamp)|Clamp)\s*\(");
            foreach (var f in files)
            {
                string src = StripCommentsAndStrings(File.ReadAllText(f));
                var hits = clamp.Matches(src).Cast<Match>().Select(m => m.Value).ToList();
                CollectionAssert.IsEmpty(hits, $"{Path.GetFileName(f)} 含 clamp 调用(AC-11-18 ①)。");
            }
        }

        // ── 辅助 ──────────────────────────────────────────────────────────

        private static bool IsOrDerivesFrom(Type t, string simpleName)
        {
            for (var c = t; c != null && c != typeof(object); c = c.BaseType)
                if (c.Name == simpleName) return true;
            return false;
        }

        /// <summary>剥离注释与字符串字面量(与 story-004 同款判据面:字符串里的被禁 token 不再误命中)。</summary>
        private static string StripCommentsAndStrings(string src)
        {
            string noStrings = Regex.Replace(src, "\"(@?)(\\\\.|[^\"\\\\])*\"", "\"\"");
            noStrings = Regex.Replace(noStrings, @"/\*.*?\*/", " ", RegexOptions.Singleline);
            noStrings = Regex.Replace(noStrings, @"//[^\n]*", " ");
            return noStrings;
        }

        /// <summary>正控探针:类型名携带诊断语义 ⇒ 守卫须命中(证明守卫非恒真)。</summary>
        private sealed class DiagnosticBearingProbe
        {
            public int DiseaseId;
        }

        /// <summary>双 EventSystem 夹具(故事卡 QA 卡点名):供谓词正控使用。</summary>
        private sealed class DualEventSystemProbe : UnityEngine.EventSystems.EventSystem { }
    }
}
