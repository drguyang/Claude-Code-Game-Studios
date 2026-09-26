// 权威来源:
//   Story: production/epics/audio-system/story-001-assembly-boundary-dto.md
//     · AC-44-B1(BLOCKING)—— b5 三段:① 44 类型级 IL/metadata 扫描(+①b 源文本层)
//         ② 装配引用集 vs 登记集 / 引擎白名单 / 既有基线  ③ DOTS 黑名单
//     · AC-44-B2 —— PresentationDtoGuard 递归扫描 AudioCueDto ⇒ 无 disease_id(AC-37-15 一致性)
//     · AC-44-B3 —— 入口类型白名单反射(状态入口仅 AudioCueDto;公开接口仅 IAudioCueSink)
//     · AC-44-B4 —— 44 全类型字段类型白名单(不持游戏状态,只持「声音正在发生什么」)
//   TR: docs/architecture/tr-registry.yaml TR-audio-001(44 只触发/只渲染,输入 = AudioCueDto)
//                           + TR-audio-009(cue 载荷不复制三源事实 —— 由 B1 的 sim 权限类型
//                             黑名单与 B4 字段白名单共同托底:44 不持 sim 量即不镜像)
//   ADR: ADR-018 §一/§二(主)· ADR-025 §①(登记集)· ADR-017 §三(DOTS)· ADR-013 §三(守卫)
//
// ⚠️ 落点:故事头账本路径 = tests/integration/audio_system/assembly_boundary_test.cs;
//    **Unity 只编译 unity/Assets/ 树** ⇒ 真身 = 本文件(承 item_database Story 001/003 同一先例;
//    账本侧 README = tests/integration/audio_system/README.md)。
//
// ⚠️ 负向夹具落位(三类,均不污染生产扫描面):
//    · B1 端到端负例 = 本测试装配(Sim.Contracts.Tests)内44 剽前缀 fixture 类型
//      (文件末命名空间块)—— 对 Library/ScriptAssemblies/Sim.Contracts.Tests.dll 跑
//      同一个 CheckAudioAssemblyIl 谓词:**真编译产物、真 IL、零新增 asmdef**
//      (新增 asmdef 会被 b3 Manifest 封闭性拒)。直接在生产树注入 = 会把工程编译搞红,
//      禁止;
//    · B1 源文本负例 = 合成文本喂 CheckAudioSourceText(同一谓词,免写坏源文件);
//    · B2 负例 = 本类私有夹具类型(**不在** 44 剽前缀下 —— 守卫负例不需要扫描键);
//    · B3/B4 负例 = 显式传入类型列表(纯函数面)。
// ⚠️ 扫描键单一出处 = AssemblyGates.AudioModuleNamespacePrefix(测试不重复定义字面量)。
// ⚠️ 零 UnityEngine 副作用 / 零随机 / 零时间依赖;确定性 = 反射枚举序 + Ordinal 判定。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using DaYiJingCheng.EditorTools.Gates;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Tests.Unit.Audio
{
    [TestFixture]
    internal sealed class AssemblyBoundaryTest
    {
        private const string Prefix = AssemblyGates.AudioModuleNamespacePrefix;

        // 状态 / 渲染参数入口白名单(AC-44-B3 二轮 Q2 收窄后的三分口径:
        // 状态入口 = AudioCueDto 唯一;渲染参数 = TierSource(SetTier)/ AudioCueHandle(循环句柄)/
        // IAudioCueSink(唯一公开接口)/ IPositionalChannel(只读第二 QoS,Story 007 消费纪律);
        // BCL / 引擎类型(mixer 暴露参数等)不在本白名单判定面 —— 它们天然非项目类型。)
        private static readonly Type[] EntryAllowlist =
        {
            typeof(AudioCueDto), typeof(TierSource), typeof(AudioCueHandle),
            typeof(IAudioCueSink), typeof(IPositionalChannel),
        };

        // 2026-09-26 修:产物路径单一出处 = 门的 ScriptAssemblyPath(原测试自拼 Path.Combine
        // 少了 Library 段 ⇒ 「产物缺失」假红;路径逻辑不许在两处各写一份)。
        private static string ScriptDll(string assemblyName)
            => AssemblyGates.ScriptAssemblyPath(assemblyName);

        private static bool IsProjectNamespace(string ns)
            => !string.IsNullOrEmpty(ns) &&
               ns.StartsWith("DaYiJingCheng", StringComparison.Ordinal);

        private static bool IsAudioPrefix(string ns)
            => AssemblyGates.IsInNamespacePrefix(ns, Prefix);

        /// <summary>生产 44 类型:只取 Gameplay.Presentation 装配(测试装配内的剽前缀
        /// fixture 刻意不入生产断言面 —— 负例走显式传入)。</summary>
        private static List<Type> ProductionAudioTypes()
        {
            var asm = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == AssemblyGates.PresentationAssemblyName);
            Assert.That(asm, Is.Not.Null,
                "Gameplay.Presentation 必须已加载(EditMode.asmdef 引用它)");
            try
            {
                return asm.GetTypes().Where(t => IsAudioPrefix(t.Namespace)).ToList();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(t => t != null && IsAudioPrefix(t.Namespace)).ToList();
            }
        }

        // 类型展开:byref/指针/数组取元素,泛型展开实参(与 b5 VisitTypeRefs / 守卫递归同构 ——
        // List<VitalsDto> 的违例在实参上,不展开 = 漏报)
        private static IEnumerable<Type> Flatten(Type t)
        {
            if (t == null) yield break;
            if (t.IsByRef || t.IsPointer || t.IsArray)
            {
                foreach (var e in Flatten(t.GetElementType())) yield return e;
                yield break;
            }
            yield return t;
            if (!t.IsGenericType) yield break;
            foreach (var a in t.GetGenericArguments())
                foreach (var e in Flatten(a)) yield return e;
        }

        private static string AssemblyNameOf(Type t) => t.Assembly.GetName().Name;

        private static bool IsBclAssembly(string an)
            => an == "mscorlib" || an == "netstandard" || an == "System" || an == "Mono" ||
               an.StartsWith("System.", StringComparison.Ordinal);

        // ═══════════════ AC-44-B1(BLOCKING)—— b5 三段 ═══════════════

        [Test]
        public void test_fullGate_currentTree_zeroErrors()
        {
            // arrange:编译必须成功(假绿防护前置 —— 拒扫陈旧产物)
            Assert.That(EditorUtility.scriptCompilationFailed, Is.False,
                "跑 b5 前提:编译成功(scriptCompilationFailed ⇒ 读上一版 DLL = 假绿)");

            // act
            var errs = AssemblyGates.RunAudioBoundary(out var warns);

            // assert:当前树三段全绿;WARN(44 零命中 / Sim 工程债基线)不阻断
            Assert.That(errs, Is.Empty, () => "b5 红行:\n" + string.Join("\n", errs));
            // 2026-09-26 审查 Required-7:观察面断言 —— Sim 工程债基线 WARN 恒在(警示不丢);
            // 44 零命中 WARN 仅实现前存在(实现后消失属正常,不断言其存在性)。
            Assert.That(warns.Any(w => w.Contains("工程债")), Is.True,
                "基线工程债 WARN 必须持续可见(摘除归装配轮的警示面)");
            TestContext.WriteLine("[b5 WARN]\n" + string.Join("\n", warns));
            // 拒扫分支(scriptCompilationFailed=true ⇒ 拒扫红)依赖全局编译态,测试不可伪 ——
            // 豁免登记(仓规豁免注):由 RunAudioBoundary 内部自断言 + 直接调用方守。
        }

        [Test]
        public void test_sourceScan_contractTokenWithoutPrefix_escapesRed()
        {
            // 2026-09-26 审查 Required-1(前缀逃逸):含 44 契约 token 却未声明前缀 = 红。
            var escapeSrc = "using DaYiJingCheng.Sim.Contracts;\n" +
                            "class RootLedger { AudioCueDto cue; void M(IAudioCueSink s) {} }";
            var errs = AssemblyGates.CheckAudioEscapeText("root.cs", escapeSrc);
            Assert.That(errs, Has.Count.EqualTo(1), () => string.Join("\n", errs));
            Assert.That(errs[0], Does.Contain("逃逸"));

            // 对照:声明了前缀的文件不走逃逸段(归 CheckAudioSourceText 管)
            var declSrc = "namespace DaYiJingCheng.Gameplay.Presentation.Audio { class Ok { AudioCueDto c; } }";
            Assert.That(AssemblyGates.CheckAudioEscapeText("decl.cs", declSrc), Is.Empty);

            // 对照:无 44 token 的根命名空间文件不误杀(L4 既有债文件)
            var benign = "using DaYiJingCheng.Sim; class L4Thing { RecipeDataSet d; }";
            Assert.That(AssemblyGates.CheckAudioEscapeText("l4.cs", benign), Is.Empty,
                "不含44 契约 token 的文件不进逃逸面(L4 的 Sim 引用属既有债)");
        }

        [Test]
        public void test_ilScan_negativeFixtureInTestAssembly_flagged()
        {
            // act:对**测试装配**产物跑同一谓词 —— 装内 IlScanNegativeFixture 住44 剜前缀,
            // 带 IEventSink 参数(契约禁名)与 RecipeDataSet 返回值(Sim 装配黑名单)
            var dll = ScriptDll("Sim.Contracts.Tests");
            var errs = AssemblyGates.CheckAudioAssemblyIl(dll, Prefix, out var matched);

            // assert:夹具必须落在扫描键内(落空 = 假绿),两类黑名单各命中一次
            Assert.That(File.Exists(dll), Is.True, $"产物缺失:{dll}");
            Assert.That(matched, Is.GreaterThanOrEqualTo(1),
                "负例 fixture 必须在44 剜前缀之下(夹具落空 = 假绿)");
            Assert.That(errs.Any(e => e.Contains("IEventSink")), Is.True,
                "契约禁名 IEventSink 必红(AC-44-B1 ① 含 IEventSink/IEventAuthority/SimEvent)");
            Assert.That(errs.Any(e => e.Contains("RecipeDataSet")), Is.True,
                "Sim 装配类型必红(AC-44-B1 ① 零 TypeRef/MemberRef 解析到 Sim/Sim.Codec)");
            Assert.That(errs.Any(e => e.Contains("IlScanNegativeFixture")), Is.True,
                "违例必须点名44 类型(可定位)");
        }

        [Test]
        public void test_referenceSet_baselineSim_warnsNotRed()
        {
            // act:既有基线(Sim = Presentation→Sim 工程债)= WARN 不红
            var errs = AssemblyGates.CheckPresentationReferenceSet(new[] { "Sim" }, out var warns);

            // assert
            Assert.That(errs, Is.Empty, "基线项不红(防新漂移而不动既有债 —— 摘除归装配轮)");
            Assert.That(warns, Has.Count.EqualTo(1));
            Assert.That(warns[0], Does.Contain("工程债"));
            Assert.That(warns[0], Does.Contain("装配轮"));
        }

        [Test]
        public void test_referenceSet_newDrift_reportsRedWithThreeSegments()
        {
            // act:登记集之外的新增引用 = 红,且错误文本三段并列(实际集 / 基线 / 登记集)
            var errs = AssemblyGates.CheckPresentationReferenceSet(
                new[] { "Sim.Contracts", "Unity.Addressables", "Some.ThirdParty.Lib" },
                out var warns);

            // assert
            Assert.That(errs, Has.Count.EqualTo(1));
            Assert.That(errs[0], Does.Contain("Some.ThirdParty.Lib"));
            Assert.That(errs[0], Does.Contain("ADR-025"), "三段并列须含登记集(要求先回写)");
            Assert.That(errs[0], Does.Contain("基线"), "三段并列须含基线");
            Assert.That(errs[0], Does.Contain("实际集"), "三段并列须含实际集");
            Assert.That(warns, Is.Empty);
        }

        [Test]
        public void test_referenceSet_dotsFamily_reportsRedUnconditionally()
        {
            foreach (var dots in new[]
                     { "Unity.Entities", "Unity.Burst", "Unity.Jobs", "Unity.Mathematics",
                       "Unity.Collections", "Unity.Physics", "Unity.Transforms" })
            {
                var errs = AssemblyGates.CheckPresentationReferenceSet(new[] { dots }, out _);
                Assert.That(errs, Has.Count.EqualTo(1), $"{dots} 必红");
                Assert.That(errs[0], Does.Contain("黑名单"), dots);
                Assert.That(errs[0], Does.Contain("无条件红"), dots);
                Assert.That(errs[0], Does.Contain("ADR-017"), dots);
            }
        }

        [Test]
        public void test_referenceSet_simCodecBlacklist_reportsRed()
        {
            // Sim.Codec 在 ADR-025 §① 是登记装配,但对本装配是黑名单 —— 无条件红,不因
            // 「在清单里」放行(黑名单 ∩ 登记集的交叉按黑名单判)
            var errs = AssemblyGates.CheckPresentationReferenceSet(new[] { "Sim.Codec" }, out var warns);

            Assert.That(errs, Has.Count.EqualTo(1));
            Assert.That(errs[0], Does.Contain("黑名单"));
            Assert.That(errs[0], Does.Contain("无条件红"));
            Assert.That(warns, Is.Empty, "黑名单不进基线 WARN 面");
        }

        [Test]
        public void test_referenceSet_engineAndBcl_pass()
        {
            var errs = AssemblyGates.CheckPresentationReferenceSet(
                new[]
                {
                    "Sim.Contracts", "UnityEngine", "UnityEngine.AudioModule",
                    "Unity.Addressables", "Unity.ResourceManager", "Unity.RenderPipelines.Universal.Core",
                    "System.Runtime", "netstandard", "mscorlib", "Mono",
                },
                out var warns);

            Assert.That(errs, Is.Empty, () => string.Join("\n", errs));
            Assert.That(warns, Is.Empty);
        }

        [Test]
        public void test_sourceScan_forbiddenUsingAndTypeName_reportsRed()
        {
            // act:合成源文本 —— 故事 QA「在 44 命名空间临时加 using DaYiJingCheng.Sim;
            // 引用 IEventSink ⇒ 测试必红」的可执行兑现(免写坏生产源文件)
            var lines = new[]
            {
                "using DaYiJingCheng.Sim;",
                "using DaYiJingCheng.Sim.Contracts;",
                "namespace DaYiJingCheng.Gameplay.Presentation.Audio {",
                "  class X { void M(IEventSink s) { } }",
                "}",
            };
            var errs = AssemblyGates.CheckAudioSourceText("fixture.cs", lines);

            // assert:恰两条 —— Sim 命名空间一条 + IEventSink 一条;Sim.Contracts 合法不误杀
            Assert.That(errs, Has.Count.EqualTo(2), () => string.Join("\n", errs));
            Assert.That(errs.Any(e => e.Contains("fixture.cs:1")), Is.True, "using Sim 必红");
            Assert.That(errs.Any(e => e.Contains("IEventSink")), Is.True, "禁名必红");
            Assert.That(errs.Any(e => e.Contains(":2")), Is.False, "using Sim.Contracts 不误杀");
        }

        [Test]
        public void test_sourceScan_commentMention_notFlagged()
        {
            // 文档性提及不算(承 schema_types_primary_key_test 注释剥离先例)
            var lines = new[]
            {
                "// 禁引 IEventSink;using DaYiJingCheng.Sim 会红 —— 见 AC-44-B1",
                "/* 块注释:DaYiJingCheng.Sim.EventOrder 同禁 */",
                "using DaYiJingCheng.Sim.Contracts;",
                "class C { }",
            };
            var errs = AssemblyGates.CheckAudioSourceText("comment.cs", lines);

            Assert.That(errs, Is.Empty, () => string.Join("\n", errs));
        }

        [Test]
        public void test_sourceScan_reflectionString_reportsRed()
        {
            // 反射字符串形态(metadata 层看不见 ldstr;源文本层兜底 —— story QA 边界例)
            var lines = new[]
            {
                "var t = System.Type.GetType(\"DaYiJingCheng.Sim.EventOrder\");",
            };
            var errs = AssemblyGates.CheckAudioSourceText("reflect.cs", lines);

            Assert.That(errs, Is.Not.Empty, "反射字符串形态必红(双层扫描的源文本层)");
            Assert.That(errs[0], Does.Contain("DaYiJingCheng.Sim"));
        }

        // ═══════════════ AC-44-B2 —— PresentationDtoGuard 递归扫描 ═══════════════

        [Test]
        public void test_dtoGuard_audioCueDto_noDiseaseField()
        {
            var errs = PresentationDtoGuard.Scan(typeof(AudioCueDto));

            Assert.That(errs, Is.Empty, () => string.Join("\n", errs));
            Assert.DoesNotThrow(() => PresentationDtoGuard.AssertNoDiseaseId(typeof(AudioCueDto)));
        }

        [Test]
        public void test_dtoGuard_nestedPrivateField_reportsRed()
        {
            // 负例①:嵌套 struct 含 private _diseaseId(嵌套 + 私有,双轴)
            var errs = PresentationDtoGuard.Scan(typeof(GuardNestedFixture));

            Assert.That(errs, Is.Not.Empty, "嵌套私有 disease 字段必红");
            Assert.That(errs.Any(e => e.Contains("_diseaseId")), Is.True,
                () => string.Join("\n", errs));
        }

        [Test]
        public void test_dtoGuard_listElementField_reportsRed()
        {
            // 负例②:List<Inner> 元素含 disease_id(泛型实参递归 —— 仅扫顶层会漏)
            var errs = PresentationDtoGuard.Scan(typeof(GuardListFixture));

            Assert.That(errs, Is.Not.Empty, "List 元素内的 disease 字段必红");
            Assert.That(errs.Any(e => e.Contains("disease_id")), Is.True,
                () => string.Join("\n", errs));
        }

        [Test]
        public void test_dtoGuard_baseClassPrivateField_reportsRed()
        {
            // 负例③:基类 private 字段(DeclaredOnly 链逐层展开)
            var errs = PresentationDtoGuard.Scan(typeof(GuardDerivedFixture));

            Assert.That(errs, Is.Not.Empty, "基类私有 disease 字段必红");
            Assert.That(errs.Any(e => e.Contains("_diseaseId")), Is.True,
                () => string.Join("\n", errs));
        }

        [Test]
        public void test_dtoGuard_diagnosticFamilyTokens_reportsRed()
        {
            // 2026-09-26 审查 Required-4:词面集 = {diseas, diagnos, symptom} ——
            // 单 "disease" 下 diagnosisId / symptom 假绿。
            var dErrs = PresentationDtoGuard.Scan(typeof(DiagnosisIdFixture));
            Assert.That(dErrs.Any(e => e.Contains("diagnosisId")), Is.True,
                () => string.Join("\n", dErrs));

            var sErrs = PresentationDtoGuard.Scan(typeof(SymptomFixture));
            Assert.That(sErrs.Any(e => e.Contains("symptom")), Is.True,
                () => string.Join("\n", sErrs));
        }

        [Test]
        public void test_dtoGuard_assertThrows_onViolation()
        {
            Assert.Throws<InvalidOperationException>(
                () => PresentationDtoGuard.AssertNoDiseaseId(typeof(GuardNestedFixture)),
                "有违例必须 throw(ADR-013 §三 草图签名)");
            Assert.DoesNotThrow(
                () => PresentationDtoGuard.AssertNoDiseaseId(typeof(AudioCueDto)));
            Assert.That(PresentationDtoGuard.Scan(null), Is.Not.Empty,
                "null 入口 = 红(拒以空集冒充绿)");
        }

        // ═══════════════ AC-44-B3 —— 入口类型白名单(公开接口仅 IAudioCueSink)═══════════════

        private static List<string> ScanEntryPoints(IEnumerable<Type> moduleTypes)
        {
            var errs = new List<string>();
            const BindingFlags declaredPublic =
                BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance |
                BindingFlags.Static;

            foreach (var t in moduleTypes)
            {
                // ① 实现的项目接口 ⊆ {IAudioCueSink}(「公开接口只有 IAudioCueSink」;
                // 引擎接口 ISerializationCallbackReceiver 等天然非项目,不在此断言面)
                foreach (var i in t.GetInterfaces())
                {
                    if (!IsProjectNamespace(i.Namespace)) continue;
                    if (i == typeof(IAudioCueSink)) continue;
                    errs.Add($"[B3] {t.FullName} 实现了 IAudioCueSink 之外的项目接口「{i.FullName}」" +
                             "—— 44 的公开接口只有 IAudioCueSink(只入不出;AC-44-B3)");
                }

                // ② 公开方法签名(参数 / 返回)—— 项目类型 ∈ 白名单;Sim/Sim.Codec 单列
                foreach (var m in t.GetMethods(declaredPublic))
                {
                    foreach (var x in Flatten(m.ReturnType).Concat(
                                 m.GetParameters().SelectMany(p => Flatten(p.ParameterType))))
                    {
                        var an = AssemblyNameOf(x);
                        if (an == "Sim" || an == "Sim.Codec")
                        {
                            errs.Add($"[B3] {t.FullName}.{m.Name} 签名出现「{an}」类型" +
                                     $"「{x.FullName}」—— 44 无上游 sim 签名(AC-44-B3)");
                            continue;
                        }
                        if (!IsProjectNamespace(x.Namespace)) continue;   // BCL / 引擎 = 渲染参数面
                        if (EntryAllowlist.Contains(x)) continue;
                        errs.Add($"[B3] {t.FullName}.{m.Name} 签名出现非白名单项目类型" +
                                 $"「{x.FullName}」—— 状态入口仅 AudioCueDto,渲染参数 = " +
                                 "TierSource/AudioCueHandle/IAudioCueSink/IPositionalChannel" +
                                 "(VitalsDto 族 2026-09-18 已删;AC-44-B3 / ADR-018 §一)");
                    }
                }
            }
            return errs;
        }

        [Test]
        public void test_entryPoints_production44_noUnknownEntry()
        {
            var errs = ScanEntryPoints(ProductionAudioTypes());

            Assert.That(errs, Is.Empty, () => string.Join("\n", errs));
        }

        [Test]
        public void test_entryPoints_vitalsDtoParam_reportsRed()
        {
            // VitalsDto 族 = 被删除的第二入口(ADR-018 §一 2026-09-18 幽灵入口删除)
            var errs = ScanEntryPoints(new[] { typeof(BadEntryFixture) });

            Assert.That(errs.Any(e => e.Contains("VitalsDto")), Is.True,
                () => string.Join("\n", errs));
            Assert.That(errs[0], Does.Contain("AC-44-B3"));
        }

        [Test]
        public void test_entryPoints_simSignature_reportsRed()
        {
            var errs = ScanEntryPoints(new[] { typeof(BadSimEntryFixture) });

            Assert.That(errs.Any(e => e.Contains("「Sim」")), Is.True,
                () => string.Join("\n", errs));
        }

        [Test]
        public void test_entryPoints_wrongInterface_reportsRed()
        {
            // 反向查询上游的接口(IVitalsQuery)=「44 无反向查询上游的接口」的负例
            var errs = ScanEntryPoints(new[] { typeof(BadInterfaceFixture) });

            Assert.That(errs.Any(e => e.Contains("IVitalsQuery")), Is.True,
                () => string.Join("\n", errs));
        }

        [Test]
        public void test_audioCueSink_onlyIntoNoStateOut()
        {
            // 契约面断言:IAudioCueSink 只入不出 —— 返回 ⊆ {void, AudioCueHandle},
            // 参数 ⊆ {AudioCueDto, TierSource, AudioCueHandle}(不吐任何状态)
            foreach (var m in typeof(IAudioCueSink).GetMethods())
            {
                var ret = m.ReturnType;
                Assert.That(ret == typeof(void) || ret == typeof(AudioCueHandle), Is.True,
                    $"{m.Name} 返回「{ret.FullName}」—— 只入不出(AC-44-B3)");
                foreach (var p in m.GetParameters())
                {
                    var pt = p.ParameterType.IsByRef ? p.ParameterType.GetElementType()
                                                     : p.ParameterType;
                    Assert.That(
                        new[] { typeof(AudioCueDto), typeof(TierSource), typeof(AudioCueHandle) }
                            .Contains(pt),
                        Is.True, $"{m.Name} 参数「{p.ParameterType.FullName}」—— 入口白名单外" +
                                 "(AC-44-B3)");
                }
            }
            Assert.That(typeof(IAudioCueSink).GetMethods().Length, Is.EqualTo(4),
                "契约面漂移监控:Emit / BeginLoop / EndLoop / SetTier 四方法(ADR-018 §二)");
        }

        // ═══════════════ AC-44-B4 —— 字段类型白名单(不持游戏状态)═══════════════

        // 白名单(2026-09-26 审查口径):BCL / DaYiJingCheng.Sim.Contracts.*(别写「DTO 族」字面
        // —— IAudioCueSink / IPositionalChannel / WorldPosLatest 字段会假红)/ UnityEngine.Audio*
        // / UnityEngine.Object 子类 / 44 前缀自身。
        private static bool IsAllowedFieldLeaf(Type t)
        {
            if (IsBclAssembly(AssemblyNameOf(t))) return true;
            var ns = t.Namespace ?? "";
            if (ns.StartsWith("DaYiJingCheng.Sim.Contracts", StringComparison.Ordinal)) return true;
            if (IsAudioPrefix(ns)) return true;
            if (ns.StartsWith("UnityEngine.Audio", StringComparison.Ordinal)) return true;
            if (typeof(UnityEngine.Object).IsAssignableFrom(t)) return true;
            return false;
        }

        private static List<string> ScanFieldTypes(IEnumerable<Type> moduleTypes)
        {
            var errs = new List<string>();
            const BindingFlags fb = BindingFlags.DeclaredOnly | BindingFlags.Public |
                                     BindingFlags.NonPublic | BindingFlags.Instance |
                                     BindingFlags.Static;
            foreach (var t in moduleTypes)
            for (var cur = t; cur != null && cur != typeof(object); cur = cur.BaseType)
            {
                if (!IsProjectNamespace(cur.Namespace)) break;   // 引擎 / BCL 基类不属 44 持有面
                foreach (var f in cur.GetFields(fb))
                foreach (var leaf in Flatten(f.FieldType))
                {
                    if (IsAllowedFieldLeaf(leaf)) continue;
                    errs.Add($"[B4] {t.FullName}.{f.Name} 的字段类型「{leaf.FullName}」∉ 白名单 " +
                             "{BCL, Sim.Contracts.*, UnityEngine.Audio*, UnityEngine.Object 子类, " +
                             "44 自身} —— 44 不持游戏状态,只持声音状态(AC-44-B4)");
                }
            }
            return errs;
        }

        [Test]
        public void test_fieldTypes_production44_withinWhitelist()
        {
            // ∀ 类型 ∀ 字段形态:当前 44 命名空间为空集 ⇒ 平凡成立(空集不豁免负例,
            // 负例见下一条 —— 负例必须有,是本故事 QA 的硬要求)
            var errs = ScanFieldTypes(ProductionAudioTypes());

            Assert.That(errs, Is.Empty, () => string.Join("\n", errs));
        }

        [Test]
        public void test_fieldTypes_simStateAndEngineNonAudioField_reportsRed()
        {
            var errs = ScanFieldTypes(new[] { typeof(FieldLeakFixture) });

            Assert.That(errs.Any(e => e.Contains("RecipeDataSet")), Is.True,
                "Sim 装配类型字段 = 游戏状态,必红(AC-44-B4;故事 QA「DiseaseState 字段 ⇒ 红」同族)");
            Assert.That(errs.Any(e => e.Contains("Vector3")), Is.True,
                "引擎非音频类型不在白名单(UnityEngine.Audio* 之外须红 —— 白名单收窄是刻意的)");
            Assert.That(errs.Any(e => e.Contains("VitalsDto")), Is.False,
                "VitalsDto 是 Sim.Contracts 类型,B4 白名单放行;它由 B1 类型名黑名单拦" +
                " —— 层间分工,不在本断言面");
        }

        // ═══════════════ B2 / B3 / B4 负例夹具(住测试命名空间,不污染44 扫描键)═══════════════

        private struct GuardNestedFixture
        {
            private GuardLeaf _leaf;
        }

        private struct GuardLeaf
        {
            private int _diseaseId;
        }

        private sealed class GuardListFixture
        {
            public List<GuardLeaf2> Items;
        }

        private struct GuardLeaf2
        {
            public int disease_id;
        }

        private class GuardBaseFixture
        {
            private int _diseaseId;
        }

        private sealed class GuardDerivedFixture : GuardBaseFixture
        {
            public int Ok;
        }

        private sealed class BadEntryFixture
        {
            public void Play(in VitalsDto v) { }
        }

        private sealed class BadSimEntryFixture
        {
            public DaYiJingCheng.Sim.RecipeDataSet Load() => default;
        }

        private sealed class BadInterfaceFixture : IVitalsQuery
        {
            public VitalsDto GetVitals(PatientId p) => default;
        }

        private struct DiagnosisIdFixture
        {
            private int diagnosisId;
        }

        private struct SymptomFixture
        {
            private int _symptomCode;
        }

        private sealed class FieldLeakFixture
        {
            public DaYiJingCheng.Sim.RecipeDataSet Recipes;
            public VitalsDto Vitals;
            public Vector3 Pos;
        }
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// B1 端到端负例:住44 扫描键命名空间之下(对本装配 dll 跑 CheckAudioAssemblyIl
// ⇒ 真编译产物、真 IL 必红)。**刻意不在生产树注入** —— 那会把工程编译搞红;
// 也**不新增 asmdef**(b3 Manifest 封闭性拒未登记装配),借测试装配 Sim.Contracts.Tests 编译。
// ══════════════════════════════════════════════════════════════════════════════
namespace DaYiJingCheng.Gameplay.Presentation.Audio
{
    internal sealed class IlScanNegativeFixture
    {
        // 契约禁名:IEventSink 住 Sim.Contracts(对 44 是写通道旁路)—— 名字黑名单命中
        internal void Emit(DaYiJingCheng.Sim.Contracts.IEventSink sink) { }

        // 装配黑名单:Sim 装配类型 —— 零 TypeRef 解析到 Sim(AC-44-B1 ①)
        internal DaYiJingCheng.Sim.RecipeDataSet Load() => default;
    }
}
