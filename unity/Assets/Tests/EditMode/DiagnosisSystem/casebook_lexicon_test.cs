// diagnosis-system Story 006 —— 动作词表 + 脉案呈现静态面(EditMode)。
//
// 权威来源:production/epics/diagnosis-system/story-006-casebook-presentation-routing-anti-fantasy.md
//   · GDD §S-8.4(路由表 + B-1~B-4)· UI-8.1/8.2(布局与焦点序)
//
// 覆盖(自动化静态半边 —— [V]/[U] 截图签核半边归 evidence 走查件,禁借绿):
//   · AC-8-51[A] 四粗状态裸 Interact 语义恒就诊;Armed 期压制计数 0;未立案 ⇒ OpenCase
//   · AC-8-52[A] ① 4 出境形状不变(两 InteractIntent 全整数)+ 无 TreatmentIntent 枚举膨胀
//                ② 词表裁决输入闭集(零新裁决输入)+ 8 侧零 Modal 写面
//   · TR-diag-019/020[A] 脉案呈现 DTO 面 PresentationDtoGuard 递归扫(含影子负夹具)
//   · UI-8.1 结构序在 UXML 资产面的静态断言(文件文本;运行树在 PlayMode casebook_render_test)
//
// ⚠️ PresentationDtoGuard 自杀陷阱:扫描根**不得**用 Diagnosis* 命名的类型
//    (类型名含 "diagnos" token ⇒ 自红)—— 本文件的扫描根全部走 DTO 形状类型。
// ⚠️ **禁词集同步(评审 q9/q15)**:`ForbiddenUiTokens` 与 PlayMode
//    `casebook_render_test` 的树遍历禁词集是**同一集合的两面**(UXML 属性/文本面 ↔
//    运行树 name/文本面)—— 改任一侧须同步改对侧,两处头注互指。
// ⚠️ 双真源:UXML spec 面 ↔ BuildUI 代码面的交叉断言在 PlayMode
//    `test_cross_uxmlSpec_buildUi_nameSequence_equivalent`(本文件只断 UXML 自身结构)。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using DaYiJingCheng.Gameplay.Input.Intents;
using DaYiJingCheng.Gameplay.Interaction;
using DaYiJingCheng.Gameplay.Presentation.Diagnosis;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.EditorTools.Gates;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.DiagnosisSystem
{
    internal sealed class CasebookLexiconTest
    {
        // 仓锚约定同 texture_binding_gate_test:DefaultRepoRoot = <repo>/unity,相对段不含 unity/ 前缀
        private const string UxmlRelPath =
            "Assets/Gameplay.UI/Skeuomorphic/Screens/Casebook39.uxml";

        // ══════════════════════════════════════════════════════════════════════════
        // AC-8-51[A]:四粗状态语义恒就诊 · Armed 计数 0 · 未立案 ⇒ OpenCase
        // ══════════════════════════════════════════════════════════════════════════
        [Test]
        public void test_ac8_51_fourCoarseStates_pressed_alwaysVisitArmedZero()
        {
            // Arrange:四态各按一次裸 Interact
            var states = new[]
            {
                PatientCoarseState.NotOpened,
                PatientCoarseState.OpenedUnread,
                PatientCoarseState.Written,
                PatientCoarseState.ArmedSuppressed,
            };

            // Act:逐态路由
            var routes = states
                .Select(s => DiagnosisActionLexicon.Resolve(interactPressed: true, s))
                .ToArray();

            // Assert:① 前三态恒就诊(Visit / OpenCase 同语义 —— 不漂移)
            Assert.IsTrue(DiagnosisActionLexicon.IsVisitSemantics(routes[0]),
                "未立案 ⇒ 就诊语义(OpenCase 归属就诊)");
            Assert.AreEqual(VisitRoute.OpenCase, routes[0],
                "未立案 ⇒ OpenCase(37 写 CaseOpened —— 8 只出路由)");
            Assert.IsTrue(DiagnosisActionLexicon.IsVisitSemantics(routes[1]),
                "已立案未查 ⇒ 就诊语义恒定");
            Assert.AreEqual(VisitRoute.Visit, routes[1], "已立案未查 ⇒ Visit(不重复立案)");
            Assert.IsTrue(DiagnosisActionLexicon.IsVisitSemantics(routes[2]),
                "已落笔 ⇒ 就诊语义恒定(脉案不改路由语义)");
            Assert.AreEqual(VisitRoute.Visit, routes[2], "已落笔 ⇒ Visit");

            // ② Armed 期:压制 = 计数 0(就诊语义计数不含它)
            Assert.AreEqual(VisitRoute.Suppressed, routes[3], "Armed 期 ⇒ 压制");
            int visitCount = routes.Count(DiagnosisActionLexicon.IsVisitSemantics);
            Assert.AreEqual(3, visitCount,
                "就诊计数 = 3(四态各按一次,Armed 期计数 0 —— AC-8-51)");
            Assert.IsFalse(DiagnosisActionLexicon.IsVisitSemantics(routes[3]),
                "Suppressed 不属就诊语义");

            // ③ IsVisitSemantics 对非就诊两值恒 false(判别力双侧自证)
            Assert.IsFalse(DiagnosisActionLexicon.IsVisitSemantics(VisitRoute.Ignore));
            Assert.IsFalse(DiagnosisActionLexicon.IsVisitSemantics(VisitRoute.Suppressed));
            Assert.IsTrue(DiagnosisActionLexicon.IsVisitSemantics(VisitRoute.Visit));
            Assert.IsTrue(DiagnosisActionLexicon.IsVisitSemantics(VisitRoute.OpenCase));

            // ④ 枚举闭合(评审 q2:词表四项穷尽 —— 新增第五态 / 治疗类路由成员 ⇒ 此处红)
            CollectionAssert.AreEquivalent(
                new[] { "NotOpened", "OpenedUnread", "Written", "ArmedSuppressed" },
                Enum.GetNames(typeof(PatientCoarseState)),
                "粗态闭集恰四(AC-8-51 四项穷尽)");
            CollectionAssert.AreEquivalent(
                new[] { "Ignore", "Suppressed", "Visit", "OpenCase" },
                Enum.GetNames(typeof(VisitRoute)),
                "路由闭集恰四(不存在第五种治疗类语义 —— 施治走方笺/11,不经本词表)");
        }

        [Test]
        public void test_ac8_51_notPressed_allStatesIgnore()
        {
            foreach (PatientCoarseState s in Enum.GetValues(typeof(PatientCoarseState)))
            {
                Assert.AreEqual(VisitRoute.Ignore,
                    DiagnosisActionLexicon.Resolve(interactPressed: false, s),
                    $"未按下 ⇒ Ignore({s})");
            }
        }

        // ══════════════════════════════════════════════════════════════════════════
        // AC-8-52①[A]:4 出境形状不变(两 InteractIntent 全整数)+ 无 TreatmentIntent
        // ══════════════════════════════════════════════════════════════════════════
        [Test]
        public void test_ac8_52_1_interactIntentShapes_allIntegral_treatmentIntentAbsent()
        {
            // ── B-1 反射对象 ①:帧事件标记(意图即事实,无载荷)──
            var frame = typeof(DaYiJingCheng.Gameplay.Input.Intents.InteractIntent);
            Assert.IsEmpty(frame.GetFields(BindingFlags.Public | BindingFlags.NonPublic
                                            | BindingFlags.Instance | BindingFlags.DeclaredOnly),
                "Input.Intents.InteractIntent 零字段(B-1:载荷不膨胀)");

            // ── B-1 反射对象 ②:**按消费点绑定**(评审 q3:按全名解析孤儿类型 = 假绿)──
            Type selectorIntent = ResolveSelectorInteractIntent();
            FieldInfo[] fields = selectorIntent.GetFields(BindingFlags.Public
                                                           | BindingFlags.NonPublic
                                                           | BindingFlags.Instance);
            CollectionAssert.AreEquivalent(
                new[] { "PlayerCell", "Pressed", "Tick" },
                fields.Select(f => f.Name).ToArray(),
                "选择器侧 InteractIntent 出境载荷字段集逐位不变(B-1)");

            // 叶子类型全整数 —— 引用类型叶子 / 非整数基元**显式红**
            // (评审 q3:递归遇到 string/class 须失败,不得零叶子空转漏判)
            foreach (FieldInfo f in fields)
                AssertIntegralShape(f.FieldType, $"{selectorIntent.Name}.{f.Name}");
            var cellLeafs = LeafNumericTypes(
                fields.Single(f => f.Name == "PlayerCell").FieldType).ToArray();
            CollectionAssert.AreEquivalent(new[] { typeof(int), typeof(int), typeof(int) },
                cellLeafs, "WorldPos = (int x, int y, int z) 三整数");

            // ── 词表住 8 ⇒ 4 侧无 TreatmentIntent 枚举膨胀 ──
            var treatmentHits = new List<string>();
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                string name = asm.GetName().Name ?? "";
                if (!(name.StartsWith("Gameplay", StringComparison.Ordinal)
                      || name.StartsWith("DaYiJingCheng", StringComparison.Ordinal)))
                    continue;
                foreach (Type t in SafeGetTypes(asm))
                {
                    if (t.Name.IndexOf("TreatmentIntent", StringComparison.OrdinalIgnoreCase) >= 0)
                        treatmentHits.Add($"{asm.GetName().Name}::{t.FullName}");
                }
            }
            Assert.IsEmpty(treatmentHits,
                "AC-8-52①:全库零 TreatmentIntent 类型 —— 词表住 8,4 只见 InteractIntent 整数");
        }

        // ══════════════════════════════════════════════════════════════════════════
        // AC-8-52②[A]:裁决输入闭集 + 8 侧零 Modal 写面
        // ══════════════════════════════════════════════════════════════════════════
        [Test]
        public void test_ac8_52_2_lexiconInputs_closedSet_noModalWriteSurface()
        {
            // ── 词表公开 API 裁决输入 = 闭集 {bool, PatientCoarseState, VisitRoute} ──
            //    (B-2/AC-8-52②:零新裁决输入 —— 不认识 UI 类型、不认识 IModalState)
            var allowedParams = new HashSet<Type>
            {
                typeof(bool),
                typeof(PatientCoarseState),
                typeof(VisitRoute),
            };
            MethodInfo[] api = typeof(DiagnosisActionLexicon)
                .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
            Assert.IsNotEmpty(api, "词表存在公开 API(机器活性)");
            foreach (MethodInfo m in api)
            {
                Assert.IsTrue(allowedParams.Contains(m.ReturnType),
                    $"返回类型 {m.ReturnType.Name} 须在闭集内({m.Name})");
                foreach (ParameterInfo p in m.GetParameters())
                {
                    Assert.IsTrue(allowedParams.Contains(p.ParameterType),
                        $"参数 {p.Name}:{p.ParameterType.Name} 须在闭集内({m.Name})—— " +
                        "裁决输入集不扩大(AC-8-52②)");
                }
            }

            // ── 8 侧零 Modal 写面(词表/状态机不认识模态;模态真源 = 42 IModalState)──
            var modalHits = new List<string>();
            foreach (Type t in DiagnosisPrefixTypes())
            {
                foreach (MemberInfo mi in t.GetMembers(BindingFlags.Public | BindingFlags.NonPublic
                                                        | BindingFlags.Instance
                                                        | BindingFlags.Static
                                                        | BindingFlags.DeclaredOnly))
                {
                    if (mi.Name.IndexOf("modal", StringComparison.OrdinalIgnoreCase) >= 0)
                        modalHits.Add($"{t.Name}.{mi.Name}");
                }
            }
            Assert.IsEmpty(modalHits,
                "8 前缀类型零 Modal 成员 —— 裁决输入只读已落盘 IModalState.Modal(读面在 42," +
                "只读断言在 PlayMode casebook_render_test)");

            // ── 类型面(评审 S-4:名字扫描的漏检面)—— 8 前缀成员的字段/属性/参数/
            //    返回类型不得来自 Gameplay.UI(成员名不含 modal 但类型是 ModalId 形态 ⇒ 此处红)──
            var uiTypeHits = new List<string>();
            const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic
                                    | BindingFlags.Instance | BindingFlags.Static
                                    | BindingFlags.DeclaredOnly;
            foreach (Type t in DiagnosisPrefixTypes())
            {
                foreach (FieldInfo f in t.GetFields(F))
                    CollectUiTypes(f.FieldType, $"{t.Name}.{f.Name}", uiTypeHits);
                foreach (PropertyInfo p in t.GetProperties(F))
                    CollectUiTypes(p.PropertyType, $"{t.Name}.{p.Name}", uiTypeHits);
                foreach (MethodInfo m in t.GetMethods(F))
                {
                    CollectUiTypes(m.ReturnType, $"{t.Name}.{m.Name}()→", uiTypeHits);
                    foreach (ParameterInfo p in m.GetParameters())
                        CollectUiTypes(p.ParameterType, $"{t.Name}.{m.Name}({p.Name})",
                            uiTypeHits);
                }
            }
            Assert.IsEmpty(uiTypeHits,
                "8 前缀成员类型零 Gameplay.UI —— 裁决输入不扩大(AC-8-52② 类型面);" +
                $"命中:{string.Join("; ", uiTypeHits)}");

            // ── 词表零字段(纯静态,不自存状态 —— B-2 同源)──
            Assert.IsEmpty(typeof(DiagnosisActionLexicon)
                    .GetFields(BindingFlags.Public | BindingFlags.NonPublic
                               | BindingFlags.Instance | BindingFlags.Static
                               | BindingFlags.DeclaredOnly),
                "词表零字段(纯静态)");
        }

        // ══════════════════════════════════════════════════════════════════════════
        // TR-diag-019/020[A]:脉案呈现 DTO 面递归扫(影子负夹具判别力)
        // ══════════════════════════════════════════════════════════════════════════
        [Test]
        public void test_tr_diag_019_020_casebookDtoSurface_noDiagnosticTokens_shadowRed()
        {
            // 扫描根 = 当前呈现字段集实际内容(39 脉案 DTO 未定稿 ⇒ 现有面全扫)
            Type[] roots =
            {
                typeof(VitalsDto),
                typeof(ReadingSnapshot),
                typeof(ReadingTransition),
                typeof(JudgmentCursor),
            };
            foreach (Type root in roots)
            {
                var errs = PresentationDtoGuard.Scan(root);
                Assert.IsEmpty(errs,
                    $"TR-diag-019/020:{root.Name} 呈现字段集零 disease/diagnos/symptom " +
                    $"token —— disease_id 不进呈现层(命中:{string.Join("; ", errs)})");
            }

            // 影子负夹具:disease 字段必红(扫描机器判别力自证)
            var shadowErrs = PresentationDtoGuard.Scan(typeof(ShadowCasebookDto));
            Assert.IsNotEmpty(shadowErrs, "影子 DTO 含 disease 字段 ⇒ 扫描必报(机器活性)");
        }

        /// <summary>影子负夹具(类型名零诊断 token —— 红必须来自字段,不是类型名)。</summary>
        public sealed class ShadowCasebookDto
        {
            public string disease_id = "";
        }

        // ══════════════════════════════════════════════════════════════════════════
        // UI-8.1/8.2:UXML 资产面静态结构(元素/属性面 —— XDocument 解析,注释不参与;
        // 评审 q11:原文 IndexOf 会被注释误红/假绿。运行树在 PlayMode casebook_render_test)
        // ══════════════════════════════════════════════════════════════════════════
        [Test]
        public void test_ui81_casebook39uxml_fivePlusInquiry_orderAndNegativeTokens()
        {
            string path = Path.Combine(TextureBindingGates.DefaultRepoRoot, UxmlRelPath);
            Assert.IsTrue(File.Exists(path), $"UXML 资产存在:{UxmlRelPath}");
            XDocument doc = XDocument.Parse(File.ReadAllText(path));

            // ── 结构序(UI-8.2 订正面):具名元素按文档序恰为契约序列 ──
            string[] named = doc.Descendants()
                .Where(e => e.Attribute("name") != null)
                .Select(e => (string)e.Attribute("name"))
                .ToArray();
            CollectionAssert.AreEqual(new[]
            {
                "casebook-root", "casebook-title", "five-channels",
                "channel-complexion", "channel-voice", "channel-posture",
                "channel-breathing", "channel-palpation", "channel-inquiry",
                "right-column", "channel-disease-name", "confidence-mark",
                "confidence-question",
            }, named, "UXML 具名元素结构序(UI-8.1 布局 + UI-8.2 焦点序 = 树序)");

            // 病名归右栏(铁律③)—— five-channels 子树内无病名、有问诊
            XElement five = doc.Descendants()
                .First(e => (string)e.Attribute("name") == "five-channels");
            string[] fiveNames = five.Descendants()
                .Where(e => e.Attribute("name") != null)
                .Select(e => (string)e.Attribute("name"))
                .ToArray();
            Assert.IsFalse(fiveNames.Contains("channel-disease-name"),
                "病名不在通道区内(UI-8.1 铁律③)");
            Assert.IsTrue(fiveNames.Contains("channel-inquiry"),
                "问诊栏在体征区(OQ-8-9 第 6 行)");

            // 焦点行恰 7;confidence-mark 不聚焦(OQ-CB-5 K=6)
            int focusable = doc.Descendants()
                .Count(e => (string)e.Attribute("focusable") == "true");
            Assert.AreEqual(7, focusable,
                "focusable 行恰 7(面色/语声/姿态/呼吸/触感/问诊/病名)");
            XElement conf = doc.Descendants()
                .First(e => (string)e.Attribute("name") == "confidence-mark");
            Assert.AreNotEqual("true", (string)conf.Attribute("focusable"),
                "置信度不落独立焦点控件(OQ-CB-5)");

            // 「?」恰一次且只在病名/置信度处(AC-8-39;父 = confidence-mark)
            List<XElement> qmarks = doc.Descendants()
                .Where(e => e.Attribute("text") != null
                            && ((string)e.Attribute("text")).Contains("?"))
                .ToList();
            Assert.AreEqual(1, qmarks.Count, "「?」全文件恰一次");
            Assert.AreEqual("confidence-mark", (string)qmarks[0].Parent.Attribute("name"),
                "「?」只在病名/置信度区(AC-8-39)");
            // 体征区零「?」(直接子树面)
            Assert.IsFalse(five.Descendants().Any(e =>
                    e.Attribute("text") != null && ((string)e.Attribute("text")).Contains("?")),
                "体征区零「?」(AC-8-39 UXML 面)");

            // ── 负向声明(AC-8-43 UXML 属性/文本面;与 PlayMode 树遍历禁词集同步)──
            foreach (XElement e in doc.Descendants())
            {
                var strings = e.Attributes().Select(a => a.Value)
                    .Concat(e.Nodes().OfType<XText>().Select(n => n.Value));
                foreach (string s in strings)
                {
                    foreach (string token in ForbiddenUiTokens)
                    {
                        Assert.IsFalse(s.Contains(token),
                            $"UI-8.4:元素「{(string)e.Attribute("name")}」零「{token}」(AC-8-43)");
                    }
                }
            }
        }

        /// <summary>
        /// UI-8.4 / AC-8-43 负向禁词集(**与 PlayMode casebook_render_test 同集合的
        /// UXML 面** —— 两处头注互指,改一处须同步另一处;评审 q9/q15)。
        /// </summary>
        private static readonly string[] ForbiddenUiTokens =
        {
            "ProgressBar", "progress-bar", "进度", "完成度", "未查", "已查",
            "X/5", "百分", "%", "图鉴", "词条", "已发现", "对错",
            "精度", "READ_FLOOR", "技能等级", "补全",
        };

        // ══════════════════════════════════════════════════════════════════════════
        // AC-8-38 [L] 断言半边:阴性渲染路径不触碰世界层(13 动画/shader)
        // ══════════════════════════════════════════════════════════════════════════
        [Test]
        public void test_ac8_38_negativeRenderPath_8prefix_zeroWorldLayerTypes()
        {
            // 正面:8 前缀生产类型的字段/属性/参数/返回类型零世界渲染层引用
            var hits = new List<string>();
            const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic
                                    | BindingFlags.Instance | BindingFlags.Static
                                    | BindingFlags.DeclaredOnly;
            foreach (Type t in DiagnosisPrefixTypes())
            {
                foreach (FieldInfo f in t.GetFields(F)) CheckWorldType(f.FieldType, t.Name, hits);
                foreach (PropertyInfo p in t.GetProperties(F))
                    CheckWorldType(p.PropertyType, t.Name, hits);
                foreach (MethodInfo m in t.GetMethods(F))
                {
                    CheckWorldType(m.ReturnType, t.Name, hits);
                    foreach (ParameterInfo p in m.GetParameters())
                        CheckWorldType(p.ParameterType, t.Name, hits);
                }
            }
            Assert.IsEmpty(hits,
                "AC-8-38:阴性渲染路径不触碰世界层 —— 8 前缀零 13 动画/shader 类型;" +
                $"命中:{string.Join("; ", hits)}");

            // 负夹具判别力(同机):带 shader 类型字段的影子 ⇒ 必报
            var shadowHits = new List<string>();
            CheckWorldType(typeof(FakeWorldShaderType), "ShadowRenderPath", shadowHits);
            Assert.IsNotEmpty(shadowHits, "影子含 shader 类型字段 ⇒ 必报(机器活性)");
        }

        private static readonly string[] WorldLayerTypeTokens =
            { "Animator", "Animation", "Shader", "Material", "ParticleSystem",
              "SkinnedMeshRenderer" };

        private static void CheckWorldType(Type t, string owner, List<string> hits)
        {
            if (t == null)
                return;
            if (t.IsArray)
            {
                CheckWorldType(t.GetElementType(), owner, hits);
                return;
            }
            if (t.IsGenericType)
            {
                foreach (Type a in t.GetGenericArguments())
                    CheckWorldType(a, owner, hits);
            }
            string full = t.FullName ?? t.Name;
            foreach (string token in WorldLayerTypeTokens)
            {
                if (full.IndexOf(token, StringComparison.Ordinal) >= 0)
                {
                    hits.Add($"{owner} → {full}");
                    return;
                }
            }
        }

        /// <summary>影子负夹具:类型名含 Shader token(证明世界层扫描可红)。</summary>
        public sealed class FakeWorldShaderType { }

        // ══════════════════════════════════════════════════════════════════════════
        // helpers
        // ══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// 解析「选择器**实际消费**的 InteractIntent」—— 绑定
        /// <c>InteractionSelector.Select</c> 的 intent 形参(评审 q3:按全名字符串解析
        /// 会打在孤儿类型上假绿;现按消费点取类型,Select 换参 ⇒ 此处即红/失配)。
        /// 该结构与 Input.Intents 的空标记结构**同名双胞**,B-1 两个都要守住。
        /// </summary>
        private static Type ResolveSelectorInteractIntent()
        {
            // ⚠️ `in` 形参的 ParameterType 是按引用类型(Name 带 "&")—— 先解引用
            List<Type> consumed = typeof(InteractionSelector)
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic
                            | BindingFlags.Instance | BindingFlags.Static)
                .Where(m => m.Name == "Select")
                .SelectMany(m => m.GetParameters())
                .Select(p => p.ParameterType.IsByRef
                    ? p.ParameterType.GetElementType()
                    : p.ParameterType)
                .Where(t => t != null && t.Name == "InteractIntent")
                .Distinct()
                .ToList();
            Assert.AreEqual(1, consumed.Count,
                "InteractionSelector.Select 恰一个 InteractIntent 形参(消费点绑定)");
            return consumed[0];
        }

        /// <summary>
        /// 整数形状断言(B-1):引用类型叶子 / 非整数基元 / 零字段值类型 ⇒ **显式红**
        /// (评审 q3:空转漏判的修复面)。枚举按其整型底层判;值类型递归公开实例字段。
        /// </summary>
        private static void AssertIntegralShape(Type t, string path)
        {
            if (t.IsEnum)
            {
                Type u = Enum.GetUnderlyingType(t);
                Assert.IsTrue(u == typeof(int) || u == typeof(long) || u == typeof(byte)
                                || u == typeof(short),
                    $"{path}:枚举 {t.Name} 底层须整型");
                return;
            }
            if (t.IsPrimitive)
            {
                Assert.IsTrue(t == typeof(int) || t == typeof(long) || t == typeof(bool)
                                || t == typeof(byte) || t == typeof(short),
                    $"{path}:基元 {t.Name} 须整数域(B-1)");
                return;
            }
            if (t.IsValueType)
            {
                FieldInfo[] fs = t.GetFields(BindingFlags.Public | BindingFlags.Instance);
                Assert.IsNotEmpty(fs,
                    $"{path}:值类型 {t.Name} 零公开字段 —— 无法证整数形状(白名单外)");
                foreach (FieldInfo f in fs)
                    AssertIntegralShape(f.FieldType, $"{path}.{f.Name}");
                return;
            }
            Assert.Fail($"{path}:引用类型 {t.FullName} —— B-1 出境载荷须全整数");
        }

        /// <summary>收集成员类型中来自 Gameplay.UI 程序集的(评审 S-4 类型面)。</summary>
        private static void CollectUiTypes(Type t, string where, List<string> hits)
        {
            if (t == null)
                return;
            if (t.IsArray)
            {
                CollectUiTypes(t.GetElementType(), where, hits);
                return;
            }
            if (t.IsGenericType)
            {
                foreach (Type a in t.GetGenericArguments())
                    CollectUiTypes(a, where, hits);
            }
            string asmName = t.Assembly.GetName().Name ?? "";
            string ns = t.Namespace ?? "";
            if (asmName == "Gameplay.UI"
                || ns.StartsWith("DaYiJingCheng.Gameplay.UI", StringComparison.Ordinal))
                hits.Add($"{where}: {t.FullName}");
        }

        /// <summary>字段类型的叶子数值类型(WorldPos 展开为 int 三元;bool 自身即叶子)。</summary>
        private static IEnumerable<Type> LeafNumericTypes(Type t)
        {
            if (t.IsPrimitive)
            {
                yield return t;
                yield break;
            }

            // struct 递归展开(值类型字段/只读字段;string 等引用类型直接跳过)
            if (t.IsValueType)
            {
                foreach (FieldInfo f in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    foreach (Type leaf in LeafNumericTypes(f.FieldType))
                        yield return leaf;
                }
            }
        }

        /// <summary>8 前缀 Diagnosis 命名空间的**生产**类型(测试装配影子不入面)。</summary>
        private static IEnumerable<Type> DiagnosisPrefixTypes()
            => typeof(DiagnosisActionLexicon).Assembly.GetTypes()
                .Where(t => t.Namespace != null
                            && t.Namespace.StartsWith(
                                "DaYiJingCheng.Gameplay.Presentation.Diagnosis",
                                StringComparison.Ordinal));

        private static IEnumerable<Type> SafeGetTypes(Assembly asm)
        {
            try { return asm.GetTypes(); }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(t => t != null)!;
            }
            catch { return Array.Empty<Type>(); }
        }

        private static int CountOccurrences(string text, string token)
        {
            int count = 0, idx = 0;
            while ((idx = text.IndexOf(token, idx, StringComparison.Ordinal)) >= 0)
            {
                count++;
                idx += token.Length;
            }
            return count;
        }
    }
}
