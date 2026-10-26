// 门链**接线判据**(C-1 · `TD-gate-01` / `TD-gate-02` 的执行体 · 2026-10-21)
//
// ══════════════════════════════════════════════════════════════════════════
// 为什么本文件存在 —— 「零红」是**单调判据**
// ══════════════════════════════════════════════════════════════════════════
// `assembly_gate_runall_test.cs:31` 断的是 `errs.Count == 0`。它有**方向性**:
//   只覆盖「多了不该有的红」,**对「少了一个门」完全失明**。
// 实测(S16-4 QA 轮,2026-10-21):删掉 `AssemblyGates.cs:130` 那一行
// (`errs.AddRange(FsWriteBoundaryGate.RunAll("."))`)后,
// **17 条 `FsWriteBoundaryGateTest` 依然全绿** —— 连同 2 条端到端共 19 条判据,一条都不红。
// ⇒ 门从门链掉线时,**现有全部判据保持绿色**。这正是本文件补的方向。
//
// ══════════════════════════════════════════════════════════════════════════
// 判据 = 双向差集,清单**手写**
// ══════════════════════════════════════════════════════════════════════════
// 每个入口:  源文本实际调用的门  ↔  `EntryWiringManifest` 手写声明
//            任一方向非空 ⇒ 红。
//          ① 源有、清单无 ⇒ **新接了一门但没声明**(或清单漏更新)
//          ② 清单有、源无 ⇒ **门从链上掉线** ← 这条是 RunAll 端到端测试抓不到的
//
// 每个门类:  目录里声明的 public static class ↔ `ClassConsumerManifest` 手写判决
//            新加一个门类而不登记 ⇒ 红(`TD-gate-02` 的「逐门核」)。
//
// ⚠️ **清单禁从源文本生成** —— 那会让「源 ↔ 清单」的比对退化成恒等式(零判别力)。
//    这是 `TD-gate-01` 修法条文里逐字写死的约束:本文件的 `EntryWiringManifest` 与
//    `ClassConsumerManifest` 全部是**手写常量**,与源文本**独立维护**。
//
//    ⚠️ 诚实登记:这一条**没有**机械判据 —— 「这份常量是不是手写的」无法从产物反推
//    (生成出来的常量与手写的长得一样)。可获得的最近似证据是
//    `test_extractor_actuallyExtractsKnownMembers`(提取器确实在抽,不是空转)与
//    `test_classManifest_hasNoPhantomEntries`(清单不随源自动膨胀)。
//    ⇒ 把清单改成 `GateChainSource` 的产物**不会**让任何断言转红,只会让本判据静默归零。
//    这是本件的**已知残余风险**,已登记于 `production/qa/evidence/impl-c1-gate-wiring-2026-10-21.md`。
//
// ══════════════════════════════════════════════════════════════════════════
// 为什么解析器住测试装配而不是 Editor.Tools.Gates
// ══════════════════════════════════════════════════════════════════════════
// 若做成 `GateWiringGate` 并接进 `RunAll`,它就在检查「自己是否在链上」:
//   **它自己被删的那一次,也正好是它不再运行的那一次**。「谁监督监督者」的递归
//   必须有底,底只能落在**独立入口** —— 即本测试。这也是 C-1 被记成
//   「测试缺口」而不是「门缺口」的原因。
//
// ⚠️ 本文件**只读文件系统**(读 `Assets/Editor.Tools.Gates/*.cs`),不写任何文件
//    —— 承 `.claude/rules/test-standards.md` 第 3 条 + `FsWriteBoundaryGate` 的 R2 判据。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace DaYiJingCheng.Tests.Unit.Sim
{
    /// <summary>
    /// 门链接线判据:三入口 × 双向差集 + 门类全量判决。
    /// <para>见文件头注 —— 本件补的是「零红」这个单调判据的**反向**。</para>
    /// </summary>
    [TestFixture]
    public class GateWiringManifestTest
    {
        /// <summary>工程根 = `Application.dataPath` 的上级(含 `Assets/` 的那一层)。</summary>
        private static string RepoRoot() => GateChainSource.ProjectRoot(Application.dataPath);

        // ══════════════════════════════════════════════════════════════════════
        // 一、入口 manifest(**手写** —— 禁从源生成,见文件头注)
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// 三个**强制入口**各自应调用的门(**手写真源**)。
        /// <para>⚠️ 手写。禁改成 `GateChainSource.InvokedMembers(...)` 的产物 ——
        /// 那样两边同源,比对恒真,判据归零。</para>
        /// <para>维护:新增一条门调用 ⇒ **同批**在本表对应入口里加一行,否则红。</para>
        /// </summary>
        private static readonly Dictionary<string, string[]> EntryWiringManifest =
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                // RunAll = 菜单 / 域重载 / 构建前门三处共用的合流入口。
                ["RunAll"] = new[]
                {
                    "CheckManifestClosure",          // b3 装配封闭性
                    "CheckReferenceDuplicates",      // b3-refdup
                    "CheckGateA",                    // b2 门 A
                    "CheckPayloadRefCallsites",      // b6 载荷手搓
                    "WriterExistenceGate.RunAll",    // b7 写者存在性
                    "DiseaseConstantRegistryGate.RunAll", // 9 常量登记(PRE-S18-B)· 2026-10-22 接线上线
                    "MedConsFloatGate.RunAll",       // 53 静态面(零浮点/零 31 符号)
                    "FsWriteBoundaryGate.RunAll",    // 测试不得写仓库(S16-4)
                    "PlayerAssemblyGate.RunAll",     // PlayMode 装配 UnityEditor 裸用(TD-testasm-01 · 2026-10-21)
                    "SaveWritePathGate.RunAll",      // AC-7a-08③ 存档写盘可达面 IL 门 · 2026-10-25 接线上线
                    "SaveAsyncBoundaryGate.RunAll",  // AC-7a-07 后台写盘路径零 Unity API IL 门 · 2026-10-25
                    "SaveVocabularyGate.RunAll",     // AC-7a-09 忘词令符号门(规则七判据层)· 2026-10-25
                    "InventoryDtoFloatGate.RunAll",  // AC-20-15 20 输出 DTO 双层零浮点门 · 2026-10-26
                },

                // RunMenu = 菜单项「大医精诚/Validation/Run Assembly Gates」。
                // ⚠️ 本入口调 RunAll(= 含上表 7 条),此处只列**自己额外接的**。
                ["RunMenu"] = new[]
                {
                    "RunAll",                        // 合流入口(上方 7 条经此生效)
                    "CheckPayloadRefCallsites",      // ⚠️ 与 RunAll 重复(见 test 段的登记)
                    "CheckToFloatCallsites",         // b4 ToFloat 调用点
                    "RunAudioBoundary",              // b5 44 装配边界
                    "DiagnosisBoundaryGates.RunAll", // 8 边界门
                    "InputBoundaryGates.RunAll",     // 3 意图边界门
                    "PrescriptionWriterGates.RunAll",// 11 写者门
                    "EcozoneIntegerGates.RunAll",    // AC-6-22 · 2026-10-21 接线上线
                    "EmergencyIntegerGates.RunAll",  // AC-10-02 / -03 · 同上
                    "RuleNineGate.RunAll",           // AC ⑥ 规则九(源码 + IL)· 2026-10-22 接线上线
                },

                // BuildGate = IPreprocessBuildWithReport.OnPreprocessBuild(**构建前 fail-fast**)。
                ["BuildGate"] = new[]
                {
                    "RunAll",
                    "CheckToFloatCallsites",
                    "RunAudioBoundary",
                    "DiagnosisBoundaryGates.RunAll",
                    "InputBoundaryGates.RunAll",
                    "PrescriptionWriterGates.RunAll",
                    "EcozoneIntegerGates.RunAll",      // AC-6-22(BLOCKING)· 2026-10-21 接线上线
                    "EmergencyIntegerGates.RunAll",    // AC-10-02 / -03(BLOCKING)· 同上
                    "RuleNineGate.RunAll",             // AC ⑥ 规则九(BLOCKING)· 2026-10-22 接线上线
                },
            };

        // ══════════════════════════════════════════════════════════════════════
        // 二、门类 manifest(全量判决 —— `TD-gate-02` 的「逐门核」)
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>门类「谁强制它」的五种形态。<para>判据不是「都得在链上」——
        /// IL 级门(Cecil + 真 DLL)放不进 reload 路径(会读陈旧产物),那不是缺陷而是设计。
        /// 本枚举的作用是让**每一种形态都必须有具名的强制点**,而不是消失在无人区。</para></summary>
        public enum WiringKind
        {
            /// <summary>门链宿主自身。</summary>
            Host,
            /// <summary>在 `AssemblyGates` 门链上(经 RunAll / RunMenu / BuildGate 强制)。</summary>
            Chain,
            /// <summary>生产侧代码直接调用 —— 强制点 = 调用点(如烘焙期 binder 内联调用)。</summary>
            Library,
            /// <summary>只有 EditMode 测试驱动,**无构建期强制点**。⚠️ 若其对应 AC 是 BLOCKING,
            /// 这一栏即是必须暴露的缺口(见 <see cref="KnownTestDrivenBlockingGaps"/>)。</summary>
            TestDriven,
            /// <summary>有独立菜单项(手工触发),不经门链。</summary>
            Menu,
            /// <summary>批处理 / 工具,非校验门。</summary>
            Tool,
        }

        /// <summary>一个门类的判决。</summary>
        public sealed class ClassVerdict
        {
            /// <summary>类名(须与源文件里的 `public static class` 逐字一致)。</summary>
            public string ClassName;

            /// <summary>谁强制它。</summary>
            public WiringKind Kind;

            /// <summary>
            /// 强制点(**可证伪出处**):
            /// <list type="bullet">
            /// <item><see cref="WiringKind.Chain"/> / <see cref="WiringKind.Host"/> ⇒ 填 `AssemblyGates` 的入口名;</item>
            /// <item><see cref="WiringKind.Library"/> / <see cref="WiringKind.TestDriven"/> ⇒ 填**相对工程根的文件路径**,
            /// 该文件须真引用本类(测试会核);</item>
            /// <item><see cref="WiringKind.Menu"/> ⇒ 填菜单项路径;</item>
            /// <item><see cref="WiringKind.Tool"/> ⇒ 填工具入口(人工/CI 调用点)。</item>
            /// </list>
            /// </summary>
            public string Consumer;

            /// <summary>判决理由 / 已知缺口(可为空)。</summary>
            public string Note;
        }

        /// <summary>
        /// **全量**门类判决 —— 覆盖 `Editor.Tools.Gates/` 下每一个 `public static class`。
        /// <para>⚠️ 手写。新增门类 ⇒ **同批**在此登记,否则
        /// `test_classManifest_coversEveryDeclaredClass` 红。</para>
        /// </summary>
        private static readonly ClassVerdict[] ClassConsumerManifest =
        {
            // ── 宿主 ──
            new ClassVerdict { ClassName = "AssemblyGates", Kind = WiringKind.Host,
                Consumer = "RunAll/RunMenu/BuildGate",
                Note = "门链宿主;三入口由入口 manifest 逐条钉住" },

            // ── 链上(6)──
            new ClassVerdict { ClassName = "DiagnosisBoundaryGates", Kind = WiringKind.Chain,
                Consumer = "RunMenu+BuildGate" },
            new ClassVerdict { ClassName = "FsWriteBoundaryGate", Kind = WiringKind.Chain,
                Consumer = "RunAll",
                Note = "S16-4 落;接线层判据由本文件补(C-1)" },
            new ClassVerdict { ClassName = "InputBoundaryGates", Kind = WiringKind.Chain,
                Consumer = "RunMenu+BuildGate" },
            new ClassVerdict { ClassName = "MedConsFloatGate", Kind = WiringKind.Chain,
                Consumer = "RunAll" },
            new ClassVerdict { ClassName = "PlayerAssemblyGate", Kind = WiringKind.Chain,
                Consumer = "RunAll",
                Note = "TD-testasm-01 落(2026-10-21);IL2CPP 腿自 2026-10-13 结构性坏 8 天,唯一发现途径曾是跑一次构建" },
            new ClassVerdict { ClassName = "PrescriptionWriterGates", Kind = WiringKind.Chain,
                Consumer = "RunMenu+BuildGate" },
            new ClassVerdict { ClassName = "SaveAsyncBoundaryGate", Kind = WiringKind.Chain,
                Consumer = "RunAll",
                Note = "AC-7a-07 落(2026-10-25):后台写盘路径的**方法级** IL 门" +
                       "(`SaveWriteQueue` / `SaveBufferPool` 两类型的方法体内零引擎 API 被调)。" +
                       "⚠️ **b2 门绿时本门不可能红** —— 它是 b2 的推论,不是独立判据;" +
                       "价值在方法级判据面与搬家检测。已跑探针轮证其在构建期真的执行" +
                       "(`build-StandaloneLinux64-1792913448749.log`:探针串 + OnPreprocessBuild 栈帧 + Result: Failure)" },
            new ClassVerdict { ClassName = "SaveWritePathGate", Kind = WiringKind.Chain,
                Consumer = "RunAll",
                Note = "AC-7a-08③ 落(2026-10-25):存档写盘可达面 IL 门。AC-7a-08 的第三方守" +
                       "(IL 正面断言「仅 SaveService 可达写盘 API」)此前**从未有执行体**,本条即其落点" },
            new ClassVerdict { ClassName = "SaveVocabularyGate", Kind = WiringKind.Chain,
                Consumer = "RunAll",
                Note = "AC-7a-09 判据层落(2026-10-25):忘词令 —— 运行期 8 装配符号表零 q 系列标识符" +
                       "(Cecil 符号名扫描,注释/字符串天然不可见 ⇒ AC-7a-04 白名单结构性为零)。" +
                       "原 blocked-by TD-ci-01 的前提已变:RunAll 三处强制点即本地强制点," +
                       "与 ADR-012 CI 门不互斥。半守(接口反射)在 folded_predicate_test:655" },
            new ClassVerdict { ClassName = "WriterExistenceGate", Kind = WiringKind.Chain,
                Consumer = "RunAll" },
            new ClassVerdict { ClassName = "DiseaseConstantRegistryGate", Kind = WiringKind.Chain,
                Consumer = "RunAll",
                Note = "PRE-S18-B 机械半边(2026-10-22 立)。查 `entities.yaml` 三阈值 + " +
                       "干预窗口各有独立 `name:` 块及其六项必填字段。" +
                       "⚠️ 只查登记存在性,不查数值 —— 数值归用户数值轮,GDD §五 default 全 `*待定*`;" +
                       "把「值没裁」判红会让本门恒红(判据对合法输入类误判)" },

            // ── 库式:生产侧直调(强制点 = 调用点)──
            new ClassVerdict { ClassName = "AudioAssetGroupGates", Kind = WiringKind.Library,
                Consumer = "Assets/Editor.Tools.Bake/DataBakeMenu.cs",
                Note = "烘焙菜单路径;AudioImportSettingsGate 委派其 IsWavName" },
            new ClassVerdict { ClassName = "AudioEventTableGates", Kind = WiringKind.Library,
                Consumer = "Assets/Editor.Tools.Bake/AudioEventTableBinder.cs",
                Note = "阶段 2 绑定器内联调用;运行期 AudioEventDataSet 侧同样消费" },
            new ClassVerdict { ClassName = "ConservationGates", Kind = WiringKind.Library,
                Consumer = "Assets/Editor.Tools.Bake/ItemDatabaseBaker.cs" },
            new ClassVerdict { ClassName = "DrugProfileGates", Kind = WiringKind.Library,
                Consumer = "Assets/Editor.Tools.Bake/ItemDatabaseBaker.cs" },
            new ClassVerdict { ClassName = "ItemDbValidation", Kind = WiringKind.Library,
                Consumer = "Assets/Editor.Tools.Bake/ItemDatabaseBaker.cs",
                Note = "行模型校验库;被 RecipeValidationGates / ItemValidationGates 复用" },
            new ClassVerdict { ClassName = "ItemValidationGates", Kind = WiringKind.Library,
                Consumer = "Assets/Editor.Tools.Bake/ItemDatabaseBaker.cs" },
            new ClassVerdict { ClassName = "MixerTopologyGates", Kind = WiringKind.Library,
                Consumer = "Assets/Editor.Tools.Gates/MixerAssetGenerator.cs",
                Note = "混音器生成器在生成后自查拓扑" },
            new ClassVerdict { ClassName = "OrdinalBaselineGate", Kind = WiringKind.Library,
                Consumer = "Assets/Editor.Tools.Bake/ItemDatabaseBaker.cs",
                Note = "ADR-014 §五 承接条③ append-only 硬门(2026-10-26 立)。强制点 = " +
                       "`RunOrdinalGates` 在 `BakeFromSourceText` 内无条件调 `ValidateTableOrdinals`" +
                       "(段①),生产路径另调 `Validate`(段② 基线比对)⇒ 任一表改号/改义/复用即聚合 throw" },
            new ClassVerdict { ClassName = "RecipeValidationGates", Kind = WiringKind.Library,
                Consumer = "Assets/Editor.Tools.Bake/ItemDatabaseBaker.cs" },
            new ClassVerdict { ClassName = "TextureBindingGates", Kind = WiringKind.Library,
                Consumer = "Assets/Editor.Tools.Gates/SkeuomorphicUiGates.cs",
                Note = "SkeuomorphicUiGates.ValidateAll 聚合其 5 条校验 → 菜单可达" },

            // ── 链上(6 → 8,2026-10-21 `TD-gate-02` 收口)──
            // 原注记「可进 BuildGate 而现未接」—— 该判断本身是对的,**是我(2026-10-21 首轮)
            // 误读成「放不进」**。复查发现 `RunAudioBoundary`(同为 Cecil IL 门、同取 dllPath)
            // 早已在 `OnPreprocessBuild` 上,且陈旧产物守卫就住在它内部 ⇒ 接线形态完全可复刻。
            // 用户裁定「接 —— 复刻 b5 先例」后两门已上线,Kind 由 TestDriven 改判 Chain。
            new ClassVerdict { ClassName = "EcozoneIntegerGates", Kind = WiringKind.Chain,
                Consumer = "RunMenu+BuildGate",
                Note = "AC-6-22 BLOCKING。2026-10-21 接线上线:守卫(scriptCompilationFailed)" +
                       "住在其 RunAll() 内,复刻 RunAudioBoundary 形态" },
            new ClassVerdict { ClassName = "EmergencyIntegerGates", Kind = WiringKind.Chain,
                Consumer = "RunMenu+BuildGate",
                Note = "AC-10-02/10-03 BLOCKING。2026-10-21 接线上线:同款守卫;" +
                       "扫两装配(AC-10-02→Gameplay.Input / AC-10-03→Sim)" },
            new ClassVerdict { ClassName = "RuleNineGate", Kind = WiringKind.Chain,
                Consumer = "RunMenu+BuildGate",
                Note = "S18-1 AC ⑥(BLOCKING · `R2-AC6`)。2026-10-22 立:此前长期 NOT-RUN" +
                       " —— S18-1 测试注释自述「有 AST 式断言」,实测该断言从未存在(第五个假绿形态)。" +
                       "判据形态按 qa-plan:80 = 源码 + IL 双证,两半都在 RunAll 内合流" },

            // ── 仅测试驱动(**无构建期强制点**)──
            new ClassVerdict { ClassName = "PodTypeScanner", Kind = WiringKind.TestDriven,
                Consumer = "Assets/Tests/EditMode/ItemDatabase/schema_types_primary_key_test.cs",
                Note = "扫描库(非门):名字不含 Gate 是刻意 —— AC-21a-27 的执行体由测试驱动" },
            new ClassVerdict { ClassName = "PresentationDtoGuard", Kind = WiringKind.TestDriven,
                Consumer = "Assets/Tests/EditMode/SkeuomorphicUI/dto_boundary_and_modal_test.cs",
                Note = "反射守卫(非门);六个测试件消费" },
            new ClassVerdict { ClassName = "InventoryDtoFloatGate", Kind = WiringKind.Chain,
                Consumer = "RunAll",
                Note = "AC-20-15 双层零浮点门(2026-10-26 S20-11 落):层 1 递归反射 + 层 2 Cecil IL," +
                       "两层共用「按源目录枚举」的类型清单。" +
                       "**初判 TestDriven 被两道门驳回后改判 Chain** —— " +
                       "① `test_classManifest_coversEveryDeclaredClass` 要求新门类有判决;" +
                       "② `test_testDrivenKind_isClosedSet_notDefault` 禁止把 BLOCKING 判据判成无强制点档" +
                       "(该表自带规矩:「升格 BLOCKING ⇒ 先找强制点」)。⇒ 补 `RunAll(string)` 入口并接进" +
                       "`AssemblyGates.RunAll`(菜单 / reload / 构建前三处共用),强制点自此真实存在。" +
                       "⚠️ **构建期是否真的执行未跑探针轮** —— 按 CLAUDE.md §BuildGate「构建绿 ≠ 门跑了」" +
                       "口径,本门在构建期的执行**尚未证实**,不记绿。" },

            // ── 菜单(独立入口)──
            new ClassVerdict { ClassName = "SkeuomorphicUiGates", Kind = WiringKind.Menu,
                Consumer = "大医精诚/Validation/Run Skeuomorphic UI Gates" },
            new ClassVerdict { ClassName = "RoslynAnalyzerLabel", Kind = WiringKind.Menu,
                Consumer = "大医精诚/Validation/Label Input Analyzers",
                Note = "标注工具(非门);零程序引用的原因是**只经菜单与测试串驱动**" },

            // ── 工具(批处理)──
            new ClassVerdict { ClassName = "MixerAssetGenerator", Kind = WiringKind.Tool,
                Consumer = "MixerAssetGenerator.Create(batch)",
                Note = "混音器资产生成器(非门);手工/CI batch 调用" },
        };

        /// <summary>
        /// 已知的「BLOCKING AC 但只有测试驱动」缺口 —— `TD-gate-02` 的逐门核结论。
        /// <para>本表**不是白名单**(不豁免任何检查),而是把缺口**具名**:
        /// `test_knownTestDrivenBlockingGaps_areStillGaps` 断言的正是「它们**仍然**是缺口」——
        /// 一旦有人把其中一条接进 BuildGate,**该断言会转红并要求同步删行**。
        /// 这样缺口不会被静默补齐、也不会被静默遗忘。</para>
        /// <para>🔁 **2026-10-21 已清空**:两条 BLOCKING 缺口(`EcozoneIntegerGates` /
        /// `EmergencyIntegerGates`)**均已接进 BuildGate**(用户裁定「接 —— 复刻 b5 先例」)
        /// ⇒ 按本表自身的协议,登记须同步删除。**清空 ≠ 判据失效** ——
        /// `test_knownTestDrivenBlockingGaps_areStillAccuratelyRecorded` 在空表上遍历零次,
        /// 其判别力由下面<b>新增</b>的 <see cref="ExpectedTestDrivenClasses"/> 接续承担。</para>
        /// </summary>
        private static readonly Dictionary<string, string> KnownTestDrivenBlockingGaps =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                // 2026-10-21:两行已删除(缺口补齐)。新增缺口须在此登记并附 AC 号。
            };

        /// <summary>
        /// `TestDriven` 是**例外**,不是默认值 —— 现存仅这两个类应落在该档。
        /// <para>⚠️ **本表是 `KnownTestDrivenBlockingGaps` 清空后补的判别力载体**:
        /// 缺口表清空后,`areStillAccuratelyRecorded` 遍历零条恒绿 —— 若此时有人把某个
        /// **已上线**的门悄悄改回 `TestDriven`,缺口表仍是空的,**全绿、无人报警**。
        /// 本表把「哪些类**允许**无强制点」写成封闭清单:**不在表内的类被判 TestDriven ⇒ 红**。</para>
        /// <para>两员的共同点:**都不承载 BLOCKING AC 的构建期判据**(`PodTypeScanner` 是扫描库、
        /// `PresentationDtoGuard` 由 42/37 侧测试驱动)。若将来某条升格为 BLOCKING,
        /// 须先给它找强制点,再动本表。</para>
        /// </summary>
        private static readonly string[] ExpectedTestDrivenClasses =
        {
            "PodTypeScanner",
            "PresentationDtoGuard",
        };

        // ══════════════════════════════════════════════════════════════════════
        // 三、入口级:双向差集(本件的核心判据)
        // ══════════════════════════════════════════════════════════════════════

        private static Dictionary<string, string[]> ResolveActual()
        {
            var errors = new List<string>();
            var actual = GateChainSource.ResolveEntryPoints(RepoRoot(), errors);
            Assert.That(errors, Is.Empty,
                "解析失败(文件缺失 / 签名变更 / 方法体不配平):\n" + string.Join("\n", errors));
            Assert.That(actual.Count, Is.EqualTo(GateChainSource.EntryPointNames.Length),
                "三个入口须全部解析到 —— 少一个 = 有入口的接线完全无人监督");
            return actual;
        }

        /// <summary>
        /// **本件的核心判据**:每个强制入口的「源实际调用集」与「手写清单」必须**双向相等**。
        /// <para>任一侧多出条目即红,并分别给出诊断方向 —— 两个方向的失败含义完全不同:</para>
        /// </summary>
        [Test]
        public void test_entryWiring_matchesManifest_bidirectionally()
        {
            Dictionary<string, string[]> actual = ResolveActual();
            var problems = new List<string>();

            foreach (string entry in GateChainSource.EntryPointNames)
            {
                if (!EntryWiringManifest.TryGetValue(entry, out string[] declared))
                {
                    problems.Add($"[{entry}] 入口在 manifest 里**完全没有声明** —— " +
                                 "新增了强制入口?须同时补 EntryWiringManifest 与其签名");
                    continue;
                }

                string[] real = actual[entry];

                foreach (string missing in declared.Except(real, StringComparer.Ordinal))
                    problems.Add($"[{entry}] **门从链上掉线**:manifest 声明 `{missing}`,源文本里没有。" +
                                 "⇒ 要么恢复该调用,要么从 manifest 删除(且须说明为什么这门不再强制)");

                foreach (string extra in real.Except(declared, StringComparer.Ordinal))
                    problems.Add($"[{entry}] **新接线未声明**:源文本调用了 `{extra}`,manifest 里没有。" +
                                 "⇒ 在同批把该条目写进 EntryWiringManifest");
            }

            Assert.That(problems, Is.Empty,
                $"门链接线与 manifest 不一致({problems.Count} 处):\n" + string.Join("\n", problems));
        }

        /// <summary>
        /// **反空转守卫**:提取器必须真能抽到已知成员。
        /// <para>没有这条,「两边都解析成空集」也会让双向差集通过 —— 那是标准的 0 == 0 假绿。</para>
        /// </summary>
        [Test]
        public void test_extractor_actuallyExtractsKnownMembers()
        {
            Dictionary<string, string[]> actual = ResolveActual();

            // 逐条点名 —— 这三个是不同调用形态的代表:
            //   自调 `CheckGateA`        (无限定,自身方法)
            //   限定 `MedConsFloatGate.RunAll`(跨类)
            //   `FsWriteBoundaryGate.RunAll`  (S16-4 的接线,正是 C-1 的起因)
            Assert.That(actual["RunAll"], Has.Member("CheckGateA"),
                "提取器抽不到自调 `CheckGateA` ⇒ 解析面是空的,双向差集退化成 0 == 0");
            Assert.That(actual["RunAll"], Has.Member("MedConsFloatGate.RunAll"),
                "提取器抽不到限定调 `MedConsFloatGate.RunAll`");
            Assert.That(actual["RunAll"], Has.Member("FsWriteBoundaryGate.RunAll"),
                "提取器抽不到 `FsWriteBoundaryGate.RunAll` —— 这正是 C-1 立案的那条线");
            Assert.That(actual["BuildGate"], Has.Member("DiagnosisBoundaryGates.RunAll"),
                "提取器抽不到 BuildGate(构建前门)的接线 —— 该入口在嵌套类里,解析须穿透");

            foreach (string entry in GateChainSource.EntryPointNames)
                Assert.That(actual[entry].Length, Is.GreaterThan(0),
                    $"[{entry}] 调用集为空 —— 空集不是「干净」,是「没解析到」");
        }

        /// <summary>
        /// 提取器必须**剥掉注释里的调用** —— 否则文档/历史注记里的门名会被当成真接线。
        /// <para>纯字符串夹具:不碰文件系统。</para>
        /// </summary>
        [Test]
        public void test_extractor_ignoresCommentedOutCalls()
        {
            const string src = @"
public static List<string> RunAll()
{
    // errs.AddRange(GhostGate.RunAll());      ← 注释里的不算
    /* errs.AddRange(SpectreGate.RunAll()); */  ← 块注释里的不算
    errs.AddRange(RealGate.RunAll());
    return errs;
}";
            string cleaned = GateChainSource.StripCommentsAndLiterals(src);
            string[] members = GateChainSource.InvokedMembers(
                cleaned, "public static List<string> RunAll()", out string body);

            Assert.That(body, Is.Not.Null, "夹具方法体应能解出");
            Assert.That(members, Has.Member("RealGate.RunAll"), "真调用须抽到");
            Assert.That(members, Has.No.Member("GhostGate.RunAll"),
                "行注释里的调用**不得**被抽到 —— 否则注释即接线,判据形同虚设");
            Assert.That(members, Has.No.Member("SpectreGate.RunAll"),
                "块注释里的调用**不得**被抽到");
        }

        /// <summary>
        /// 提取器必须**剥掉字符串字面量里的调用** —— `AssemblyGates` 的错误报文里
        /// 大量出现门名(如 `""[b6] … new PayloadRef( …""`),不剥会把报文当接线。
        /// </summary>
        [Test]
        public void test_extractor_ignoresCallsInsideStringLiterals()
        {
            const string src = @"
public static List<string> RunAll()
{
    errs.Add(""参见 GhostGate.RunAll() 与 FakeGate.Check()"");
    errs.Add(""b6 拦的是 new PayloadRef("");
    errs.AddRange(RealGate.RunAll());
    return errs;
}";
            string cleaned = GateChainSource.StripCommentsAndLiterals(src);
            string[] members = GateChainSource.InvokedMembers(
                cleaned, "public static List<string> RunAll()", out _);

            Assert.That(members, Has.Member("RealGate.RunAll"));
            Assert.That(members, Has.No.Member("GhostGate.RunAll"),
                "字符串字面量里的调用**不得**被抽到 —— 报文提到门名 ≠ 调用了该门");
            Assert.That(members, Has.No.Member("FakeGate.Check"));
        }

        /// <summary>签名不存在 ⇒ 返回 null(不是空数组)—— 让「解析失败」与「真的没有调用」可分。</summary>
        [Test]
        public void test_methodBody_unknownSignature_returnsNull()
        {
            string cleaned = GateChainSource.StripCommentsAndLiterals("class X { void A() { } }");
            string body = GateChainSource.MethodBody(cleaned, "public static List<string> RunAll()");
            Assert.That(body, Is.Null,
                "签名不存在须返回 null —— 返回空串会让「找不到方法」伪装成「方法体为空」");
        }

        // ══════════════════════════════════════════════════════════════════════
        // 四、类级:全量判决(`TD-gate-02`)
        // ══════════════════════════════════════════════════════════════════════

        private static string[] DeclaredClasses()
            => GateChainSource.DeclaredStaticClasses(
                Path.Combine(RepoRoot(), GateChainSource.GateDirRelativePath));

        /// <summary>
        /// 目录里的每个 `public static class` 都必须有判决。
        /// <para>这一条补的正是 `TD-gate-02`:**新加一个门类而不接链、不登记,现有判据全绿**。</para>
        /// </summary>
        [Test]
        public void test_classManifest_coversEveryDeclaredClass()
        {
            string[] declared = DeclaredClasses();
            Assert.That(declared.Length, Is.GreaterThan(0),
                "扫不到任何类 ⇒ 目录路径错了或解析面空了(空集冒充绿)");

            string[] manifested = ClassConsumerManifest.Select(v => v.ClassName).ToArray();

            string[] unjudged = declared.Except(manifested, StringComparer.Ordinal).ToArray();
            Assert.That(unjudged, Is.Empty,
                "以下类在 `Editor.Tools.Gates/` 里有声明,但 ClassConsumerManifest **没有判决**:\n  " +
                string.Join("\n  ", unjudged) +
                "\n⇒ 新增门类须同批登记 Kind + Consumer(Consumer 必须是可证伪的强制点)");
        }

        /// <summary>反向:manifest 里的每条都须真有声明(防「类被删/改名但清单没跟着改」)。</summary>
        [Test]
        public void test_classManifest_hasNoPhantomEntries()
        {
            string[] declared = DeclaredClasses();
            string[] manifested = ClassConsumerManifest.Select(v => v.ClassName).ToArray();

            string[] phantom = manifested.Except(declared, StringComparer.Ordinal).ToArray();
            Assert.That(phantom, Is.Empty,
                "以下条目在 manifest 里,但 `Editor.Tools.Gates/` 里**没有对应声明**:\n  " +
                string.Join("\n  ", phantom) + "\n⇒ 类被删/改名了?清单须同批订正");
        }

        /// <summary>manifest 内不得重名(重名会让双向差集静默吞掉一条)。</summary>
        [Test]
        public void test_classManifest_hasNoDuplicateClassNames()
        {
            var dupes = ClassConsumerManifest
                .GroupBy(v => v.ClassName, StringComparer.Ordinal)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToArray();
            Assert.That(dupes, Is.Empty, "manifest 内重名:" + string.Join(", ", dupes));
        }

        /// <summary>每条判决都须有非空 Consumer(空 = 未判决)。</summary>
        [Test]
        public void test_classManifest_everyEntryNamesAConsumer()
        {
            foreach (ClassVerdict v in ClassConsumerManifest)
            {
                Assert.That(v.Consumer, Is.Not.Null.And.Not.Empty,
                    $"`{v.ClassName}` 的 Consumer 为空 —— 空 = 未判决,等于没登记");
            }
        }

        /// <summary>
        /// `Kind == Chain` 的类必须**真的**出现在某个入口的接线集里。
        /// <para>反向保护:把 `Kind` 写成 `Chain` 却不接链 = 用声明冒充事实。</para>
        /// </summary>
        [Test]
        public void test_classManifest_chainKindEntriesAreActuallyWired()
        {
            Dictionary<string, string[]> actual = ResolveActual();
            var allWired = new HashSet<string>(
                actual.Values.SelectMany(x => x), StringComparer.Ordinal);

            var liars = new List<string>();
            foreach (ClassVerdict v in ClassConsumerManifest.Where(v => v.Kind == WiringKind.Chain))
            {
                // 该类须以「`类名.方法`」形态出现在某个入口的接线集里
                bool present = allWired.Any(m =>
                    m.StartsWith(v.ClassName + ".", StringComparison.Ordinal));
                if (!present)
                    liars.Add($"{v.ClassName}(声明 Chain,但三个入口的接线集里都没有 `{v.ClassName}.*`)");
            }

            Assert.That(liars, Is.Empty,
                "以下类标了 Kind=Chain 但实际不在链上(声明 ≠ 事实):\n  " + string.Join("\n  ", liars));
        }

        /// <summary>
        /// `Kind == Library` / `TestDriven` 的 Consumer 是**文件路径**,须真存在且真引用了该类。
        /// <para>这条让 Consumer 栏成为可证伪判据,而不是一句散文。</para>
        /// </summary>
        [Test]
        public void test_classManifest_libraryAndTestConsumersAreRealReferences()
        {
            var problems = new List<string>();

            foreach (ClassVerdict v in ClassConsumerManifest.Where(v =>
                         v.Kind == WiringKind.Library || v.Kind == WiringKind.TestDriven))
            {
                string path = Path.Combine(RepoRoot(), v.Consumer.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(path))
                {
                    problems.Add($"{v.ClassName}: Consumer 路径不存在 `{v.Consumer}`");
                    continue;
                }

                string code = GateChainSource.StripCommentsAndLiterals(File.ReadAllText(path));
                bool references = Regex.IsMatch(code, @"\b" + Regex.Escape(v.ClassName) + @"\s*\.");
                if (!references)
                    problems.Add($"{v.ClassName}: `{v.Consumer}` 的**代码**(剥注释后)里没有 `{v.ClassName}.` " +
                                 "—— 强制点已消失,该门现在无人调用");
            }

            Assert.That(problems, Is.Empty,
                "库式 / 测试驱动门的强制点失效:\n  " + string.Join("\n  ", problems) +
                "\n⇒ 要么恢复调用,要么把该条目改判为 TestDriven/Tool 并写清理由");
        }

        /// <summary>
        /// `Kind == Menu` 的 Consumer 是菜单路径,须与源文件里的 `[MenuItem("…")]` 逐字一致。
        /// </summary>
        [Test]
        public void test_classManifest_menuConsumersMatchMenuItemAttributes()
        {
            string gateDir = Path.Combine(RepoRoot(), GateChainSource.GateDirRelativePath);
            var problems = new List<string>();

            foreach (ClassVerdict v in ClassConsumerManifest.Where(v => v.Kind == WiringKind.Menu))
            {
                string path = Path.Combine(gateDir, v.ClassName + ".cs");
                if (!File.Exists(path))
                {
                    problems.Add($"{v.ClassName}: 源文件不存在 `{v.ClassName}.cs`");
                    continue;
                }

                string src = File.ReadAllText(path);
                if (!src.Contains("[MenuItem(\"" + v.Consumer + "\")"))
                    problems.Add($"{v.ClassName}: Consumer 写的是菜单 `{v.Consumer}`,但源文件里" +
                                 "没有该 [MenuItem] 路径 —— 菜单被改名了?");
            }

            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        // ══════════════════════════════════════════════════════════════════════
        // 五、缺口登记:`TD-gate-02` 的逐门核结论,具名而非豁免
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// 已登记的「BLOCKING AC 但无构建期强制点」缺口。
        /// <para>⚠️ 这条**故意**在缺口仍存在时保持绿 —— 它守的是**记录的准确性**:
        /// 某条被接进 BuildGate 之后,本断言转红,要求把该行从
        /// <see cref="KnownTestDrivenBlockingGaps"/> 里删除(并留下接线的证据)。
        /// ⇒ 缺口不会被静默补齐(那样登记就撒谎了),也不会被静默遗忘(登记还在)。</para>
        /// </summary>
        [Test]
        public void test_knownTestDrivenBlockingGaps_areStillAccuratelyRecorded()
        {
            Dictionary<string, string[]> actual = ResolveActual();
            var buildGateWired = new HashSet<string>(actual["BuildGate"], StringComparer.Ordinal);

            var stale = new List<string>();
            foreach (var kv in KnownTestDrivenBlockingGaps)
            {
                bool nowWired = buildGateWired.Any(m =>
                    m.StartsWith(kv.Key + ".", StringComparison.Ordinal));
                if (nowWired)
                    stale.Add($"{kv.Key}({kv.Value})现在**已**在 BuildGate 上 —— " +
                              "把它从 KnownTestDrivenBlockingGaps 删掉,并把 Kind 改判为 Chain");
            }

            Assert.That(stale, Is.Empty,
                "缺口登记已过期(缺口被补齐但记录没更新):\n  " + string.Join("\n  ", stale));
        }

        /// <summary>
        /// 缺口登记的**反向**:每条登记的类在 manifest 里必须是 `TestDriven`。
        /// <para>防止「登记说它是缺口,但 manifest 悄悄标成别的」。</para>
        /// </summary>
        [Test]
        public void test_knownTestDrivenBlockingGaps_agreeWithManifestKind()
        {
            var mismatched = new List<string>();
            foreach (string cls in KnownTestDrivenBlockingGaps.Keys)
            {
                ClassVerdict v = ClassConsumerManifest.FirstOrDefault(x => x.ClassName == cls);
                if (v == null)
                {
                    mismatched.Add($"{cls}: 在缺口登记里,但 ClassConsumerManifest 没有该条目");
                    continue;
                }
                if (v.Kind != WiringKind.TestDriven)
                    mismatched.Add($"{cls}: 缺口登记说它无构建期强制点,但 manifest 标的是 {v.Kind}");
            }

            Assert.That(mismatched, Is.Empty, string.Join("\n", mismatched));
        }

        /// <summary>
        /// **`TestDriven` 封闭性** —— 不在 <see cref="ExpectedTestDrivenClasses"/> 里的类,
        /// 一律不得被判 `TestDriven`。
        /// <para>本断言是**清空缺口表之后**补的判别力载体(见
        /// <see cref="KnownTestDrivenBlockingGaps"/> 的 2026-10-21 注)。它守的是
        /// **接线回退方向**:把已上线的门改回 `TestDriven`,本断言转红。</para>
        /// <para>⚠️ 与 `test_classManifest_coversEveryDeclaredClass` 的分工:那条管
        /// 「每个类都要有判决」,本条管「判决可否落到最弱的一档」。**两者都必要** ——
        /// 前者允许把新类直接判 TestDriven,后者禁止。</para>
        /// </summary>
        [Test]
        public void test_testDrivenKind_isClosedSet_notDefault()
        {
            var allowed = new HashSet<string>(ExpectedTestDrivenClasses, StringComparer.Ordinal);

            // ① 反向:manifest 里判 TestDriven 的,必须在允许清单内。
            var unexpected = ClassConsumerManifest
                .Where(v => v.Kind == WiringKind.TestDriven && !allowed.Contains(v.ClassName))
                .Select(v => $"{v.ClassName}: 被判 TestDriven,但不在 ExpectedTestDrivenClasses 内" +
                             " —— 要么给它找强制点(改 Chain/Menu/Library),要么显式登记进该清单并写明理由")
                .ToList();

            // ② 正向:清单里列的必须真的存在且真的判 TestDriven(防清单腐烂成幽灵条目)。
            foreach (string cls in ExpectedTestDrivenClasses)
            {
                ClassVerdict v = ClassConsumerManifest.FirstOrDefault(x => x.ClassName == cls);
                if (v == null)
                    unexpected.Add($"{cls}: 在 ExpectedTestDrivenClasses 内,但 manifest 无此条目(幽灵条目)");
                else if (v.Kind != WiringKind.TestDriven)
                    unexpected.Add($"{cls}: 在 ExpectedTestDrivenClasses 内,但 manifest 判的是 {v.Kind}" +
                                   " —— 清单与事实不符,须同步");
            }

            Assert.That(unexpected, Is.Empty,
                "TestDriven 封闭性被破坏:\n  " + string.Join("\n  ", unexpected));
        }
    }
}
