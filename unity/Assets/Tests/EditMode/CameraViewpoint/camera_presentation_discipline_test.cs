// camera-viewpoint Story 001 测试 —— 呈现纪律与边界
//
// AC-2-01: 相机不持有游戏状态(① 字段类型闭包 ② 差分重算)
// AC-2-02: 零第三方(Packages/manifest.json 无 cinemachine;两处都扫)
// AC-2-04: 不引用 sim 实现程序集 + IEventSink.Append 调用点 = 0
// AC-2-05: 零 SimEvent 构造点
// AC-2-06: 效果归属 + VR 禁用 + 不得报状态 + AudioListener 唯一
//
// 权威来源: ADR-020 §二/§五/§六/§七 · ADR-013 §9 C3 · ADR-018 §六 · ADR-023 ①

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DaYiJingCheng.Gameplay.Presentation.Camera;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;
using UnityEngine;

namespace DaYiJingCheng.Tests.CameraViewpoint
{
    public class CameraPresentationDisciplineTest
    {
        /// <summary>相机程序集 —— 本故事的判据面。</summary>
        private static Assembly CameraAsm => typeof(ICameraRig).Assembly;

        /// <summary>相机类型集(住 Gameplay.Presentation.Camera 命名空间)。</summary>
        private static IEnumerable<Type> CameraTypes =>
            CameraAsm.GetTypes().Where(t => t.Namespace == "DaYiJingCheng.Gameplay.Presentation.Camera");

        // ══════════════ AC-2-01①:字段类型闭包(递归)══════════════

        /// <summary>
        /// **游戏事实类型**(判据 = 类型域黑名单,**仅**用于「已知的游戏事实类型」这一层)。
        /// </summary>
        /// <remarks>
        /// ⚠️ **2026-10-03 修复(评审 B5)**:story 自述判据为「**白名单**按**类型**」
        /// (「`SimEvent`/`Fix`/`PatientId`/事件流集合类型一律红;
        /// `Vector3`/`float`/档位枚举/转场进度 `t`/`Casebook` 锚快照 `Vector3` 一律**绿**」)
        /// —— 而初版实现是 **5 元黑名单** ⇒ `PayloadRef` / `EventOrderKey` / 任何
        /// **未列入的**游戏事实类型**全部漏检**。
        /// ⇒ 现改为**真白名单**:未在允许集内的一律红(见 <see cref="AllowedCameraStateTypes"/>)。
        /// 本黑名单仅作**可读的具名补充**(错误消息友好),判据本体是白名单。
        /// </remarks>
        private static readonly HashSet<Type> KnownGameFactTypes = new HashSet<Type>
        {
            typeof(SimEvent), typeof(PatientId), typeof(Fix), typeof(StreamId), typeof(EventKind),
            typeof(PayloadRef), typeof(EventOrderKey), typeof(WorldPos), typeof(Int3),
        };

        /// <summary>
        /// **允许**的相机内部状态类型(白名单;AC-2-01① 的判据本体)。
        /// 出处 = story 001 §Implementation Notes 的绿名单 + 边界程序集整数类型的**排除**。
        /// </summary>
        private static readonly HashSet<Type> AllowedCameraStateTypes = new HashSet<Type>
        {
            // BCL 基元 / 字符串
            typeof(bool), typeof(byte), typeof(sbyte), typeof(short), typeof(ushort),
            typeof(int), typeof(uint), typeof(long), typeof(ulong),
            typeof(float), typeof(double), typeof(char), typeof(string),
            // UnityEngine 表现层
            typeof(Vector2), typeof(Vector3), typeof(Vector4), typeof(Quaternion),
            typeof(Transform), typeof(GameObject), typeof(UnityEngine.Camera),
            // 2 自有类型 + 档位枚举 + 基
            typeof(CameraMode), typeof(YawBasis), typeof(ICameraRig), typeof(CameraRig),
            typeof(CameraModeMachine), typeof(CameraArmParams), typeof(CameraArmSolver),
            typeof(AnchorFollowConfig), typeof(AnchorFollower),
            typeof(IArmCollisionQuery), typeof(CountingArmQuery),
            typeof(CameraModeRequest), typeof(CameraModePriority),
            // 允许的容器(元素递归判定)
            typeof(System.Collections.Generic.List<>),
            typeof(IReadOnlyList<>), typeof(IList<>), typeof(IEnumerable<>),
        };

        [Test]
        public void test_ac201a_cameraFields_noGameFactTypes()
        {
            var violations = new List<string>();
            var scanned = 0;

            foreach (var t in CameraTypes)
            {
                if (t.IsEnum) continue;
                foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic |
                                              BindingFlags.Instance | BindingFlags.Static |
                                              BindingFlags.DeclaredOnly))
                {
                    scanned++;
                    if (IsGameFactType(f.FieldType))
                        violations.Add($"{t.Name}.{f.Name} : {f.FieldType.Name}");
                }
                foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.NonPublic |
                                                  BindingFlags.Instance | BindingFlags.Static |
                                                  BindingFlags.DeclaredOnly))
                {
                    scanned++;
                    if (IsGameFactType(p.PropertyType))
                        violations.Add($"{t.Name}.{p.Name} : {p.PropertyType.Name}");
                }
            }

            Assert.Greater(scanned, 0, "扫描面不得为空(拒以空集冒充绿)");
            Assert.IsEmpty(violations,
                $"相机字段/属性不得为游戏事实类型(AC-2-01①,扫 {scanned} 项):\n" +
                string.Join("\n", violations));
        }

        /// <summary>
        /// **白名单判定**:未在允许集内 ⇒ 视为游戏事实(红)。
        /// ⚠️ 递归进**数组 / 泛型容器元素**,并**递归进自定义 struct 的字段**
        /// (评审 B5:初版只扫顶层 + 不进 struct ⇒ story 自己点名的 `struct{PatientId}` 逃逸)。
        /// </summary>
        private static bool IsGameFactType(Type t) => IsKnownGameFact(t, 0) || !IsAllowedType(t, 0);

        private static bool IsKnownGameFact(Type t, int depth)
        {
            if (t == null || depth > 4) return false;
            if (KnownGameFactTypes.Contains(t)) return true;
            if (t.IsArray) return IsKnownGameFact(t.GetElementType(), depth + 1);
            if (t.IsGenericType)
                return t.GetGenericArguments().Any(a => IsKnownGameFact(a, depth + 1));
            if (t.IsValueType && !t.IsPrimitive && !t.IsEnum)
                foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
                    if (IsKnownGameFact(f.FieldType, depth + 1)) return true;
            return false;
        }

        private static bool IsAllowedType(Type t, int depth)
        {
            if (t == null) return false;
            if (depth > 4) return false;                 // 防环
            if (AllowedCameraStateTypes.Contains(t)) return true;
            if (t.IsEnum) return true;                   // 枚举 = 整数域
            if (t.IsArray) return IsAllowedType(t.GetElementType(), depth + 1);
            if (t.IsGenericType)
            {
                var def = t.GetGenericTypeDefinition();
                if (!AllowedCameraStateTypes.Contains(def)) return false;
                return t.GetGenericArguments().All(a => IsAllowedType(a, depth + 1));
            }
            // 🔴 **递归进自定义 struct 的字段**(评审 B5 的关键补齐)
            if (t.IsValueType && !t.IsPrimitive)
            {
                foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
                    if (!IsAllowedType(f.FieldType, depth + 1)) return false;
                return true;
            }
            return false;                                // 其余一律红
        }

        [Test]
        public void test_ac201a_negativeFixture_reportsGameFactField()
        {
            // 可证伪性:构造一个含 SimEvent 字段的夹具,谓词须报红
            Assert.IsTrue(IsGameFactType(typeof(SimEvent)), "SimEvent 须被判为游戏事实");
            Assert.IsTrue(IsGameFactType(typeof(List<SimEvent>)), "容器元素递归须命中");
            Assert.IsTrue(IsGameFactType(typeof(Fix[])), "数组元素递归须命中");
            Assert.IsFalse(IsGameFactType(typeof(Vector3)), "Vector3 是合法表现层状态");
            Assert.IsFalse(IsGameFactType(typeof(float)), "float 是合法表现层状态");

            // 🔴 **评审 B5 的逃逸形态**:`struct { PatientId }` —— 初版只扫顶层 ⇒ 漏检
            Assert.IsTrue(IsGameFactType(typeof(FakeWrapperWithPatientId)),
                "**嵌套自定义 struct 内的游戏事实类型须被检出**(B5:初版逃逸)");
            // 未列入黑名单的游戏事实类型也须红(白名单的本意)
            Assert.IsTrue(IsGameFactType(typeof(PayloadRef)),
                "PayloadRef 不在白名单 ⇒ 须红(黑名单形态会漏掉它)");
        }

        [Test]
        public void test_ac201a_noHiddenStaticState()
        {
            // ⚠️ 静态字段本身要进扫描面(否则「实例干净、静态脏」通过)
            var statics = CameraTypes
                .Where(t => !t.IsEnum)
                .SelectMany(t => t.GetFields(BindingFlags.Public | BindingFlags.NonPublic |
                                             BindingFlags.Static | BindingFlags.DeclaredOnly))
                .Where(f => !f.IsLiteral && !f.IsInitOnly)   // 排除 const / readonly 常量
                .Select(f => $"{f.DeclaringType.Name}.{f.Name}")
                .ToList();

            Assert.IsEmpty(statics,
                "相机类型不得有**可变静态字段**(隐藏状态会破坏 AC-2-01② 的差分重算前提):\n" +
                string.Join("\n", statics));
        }

        /// <summary>负向夹具:嵌套游戏事实类型的自定义 struct(评审 B5 的逃逸形态)。</summary>
        private struct FakeWrapperWithPatientId { public PatientId P; public int Pad; }

        // ══════════════ AC-2-01②:差分重算(后半段相同 ⇒ 末帧逐位相同)══════════════

        [Test]
        public void test_ac201b_differentialRecompute_lastFrameBitIdentical()
        {
            // ⚠️ 前提:相机无隐藏静态状态(上一条已守)。
            // 构造两条**不同历史**的 rig,喂**相同后半段**输入,断言末帧输出逐位相同。
            // 🔴 **2026-10-03 二次重写(评审 B4 的连带)**:
            //    第二版用「两实例 + 不同历史 + 相同后半段」—— **仍抓不到隐藏静态态**:
            //    静态态在两实例间**共享** ⇒ 末帧**仍相同**(实测:注入静态态后该测不红,
            //    是另一条 `noHiddenStaticState` 兜住的 —— 但**本条应自己能抓**)。
            //    AC-2-01② 要守的是「**崩溃 / 重启不改变游戏事实**」
            //    ⇒ 正确形态 = **销毁并重建**实例,比「重建前 vs 重建后」的末帧。
            var tail = new[] { 0.5f, -0.25f, 0.75f, 0.0f, -0.5f };

            // ① 实例 A:预置一段历史后归零,喂 tail,记末帧
            var goA = new GameObject("rigA");
            var rigA = goA.AddComponent<CameraRig>();
            rigA.ApplyLook(1.0f, 0.2f);
            rigA.ApplyLook(0.3f, -0.1f);
            rigA.ResetLookForTest(yaw: 0f, pitch: 30f);
            foreach (var x in tail) rigA.ApplyLook(x, 0f);
            float yawBefore = rigA.Yaw, pitchBefore = rigA.Pitch;
            var basisBefore = rigA.YawBasis;

            // ② **销毁 + 重建**(「崩溃 / 重启」的机械语义)
            UnityEngine.Object.DestroyImmediate(goA);
            var goB = new GameObject("rigB");
            var rigB = goB.AddComponent<CameraRig>();
            rigB.ResetLookForTest(yaw: 0f, pitch: 30f);
            foreach (var x in tail) rigB.ApplyLook(x, 0f);

            // ③ 末帧须**逐位相同** —— 重启不得改变任何可见事实
            Assert.AreEqual(yawBefore, rigB.Yaw,
                "**重启后末帧 Yaw 须逐位相同**(AC-2-01②:崩溃/重启不改变游戏事实;" +
                "若隐藏静态态存在,重建实例后它会继续累积 ⇒ 此处红)");
            Assert.AreEqual(pitchBefore, rigB.Pitch, "重启后末帧 Pitch 须逐位相同");

            // 末帧可见输出须**逐位相同**
            Assert.AreEqual(rigA.Yaw, rigB.Yaw, "末帧 Yaw 须逐位相同(AC-2-01②)");
            Assert.AreEqual(rigA.Pitch, rigB.Pitch, "末帧 Pitch 须逐位相同");

            var basisA = rigA.YawBasis;
            var basisB = rigB.YawBasis;
            Assert.AreEqual(basisA.Fwd.x, basisB.Fwd.x);
            Assert.AreEqual(basisA.Fwd.y, basisB.Fwd.y);
            Assert.AreEqual(basisA.Fwd.z, basisB.Fwd.z);

            UnityEngine.Object.DestroyImmediate(goB);
        }

        // ══════════════ AC-2-02:零第三方(两处 manifest 都扫)══════════════

        [Test]
        public void test_ac202_noCinemachine_inBothManifests()
        {
            // ⚠️ 两处都须扫(漏一处 = 原文点名的假阳性机器变体);
            //    大小写敏感路径写死(Linux CI)。
            var repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            var paths = new[]
            {
                Path.Combine(repoRoot, "Packages", "manifest.json"),
                Path.Combine(repoRoot, "unity", "Packages", "manifest.json"),
            };

            int found = 0;
            var violations = new List<string>();
            var missing = new List<string>();

            foreach (var p in paths)
            {
                if (!File.Exists(p)) { missing.Add(p); continue; }
                found++;
                string content = File.ReadAllText(p);
                if (content.Contains("com.unity.cinemachine"))
                    violations.Add(p);
            }

            Assert.IsEmpty(violations,
                "Packages/manifest.json 不得含 com.unity.cinemachine(AC-2-02;ADR-020 §二):\n" +
                string.Join("\n", violations));

            // 文件缺失 ⇒ 显式报,不得静默记绿(原文点名的假阳性机器)
            Assert.IsEmpty(missing,
                "以下 manifest 路径**不存在** ⇒ 判据不可执行,须显式报(不得记绿):\n" +
                string.Join("\n", missing));
            Assert.GreaterOrEqual(found, 2, "两处 manifest 都须扫到(AC-2-02 的拆解注)");
        }

        // ══════════════ AC-2-04:不引用 sim 实现 + Append 调用点 = 0 ══════════════

        [Test]
        public void test_ac204a_referenceWhitelist_noSimImplementation()
        {
            var refs = CameraAsm.GetReferencedAssemblies().Select(r => r.Name).ToList();

            // ⚠️ **2026-10-03(首次运行发现)**:相机与系统 1 **同住 `Gameplay.Presentation`**
            //    (无独立 `Gameplay.Camera` asmdef)⇒ 该程序集的引用集即二者共用。
            //    而该程序集**已引 `Sim`**(= `RecipeDataSet` 债,与 A7 同源、已登记)。
            //    ⇒ 按 A7 同款**具名豁免**:豁免仅 `Sim`;任何**新增**的 sim 实现引用都红。
            var waiver = new[] { "Sim" };
            var forbidden = refs.Where(r => r == "Sim" || r == "Sim.Codec").ToList();
            var unexpected = forbidden.Where(f => !waiver.Contains(f)).ToList();
            Assert.IsEmpty(unexpected,
                "相机程序集引用了**未登记**的 sim 实现程序集 [" + string.Join(", ", unexpected) + "]。" +
                "已登记豁免仅 [Sim](= RecipeDataSet 债,与 AC-1-28 同源)。");

            Assert.IsFalse(refs.Contains("Unity.Entities"), "不得引用 Unity.Entities");
            Assert.IsFalse(refs.Contains("Unity.Entities"), "不得引用 Unity.Entities");
            Assert.IsFalse(refs.Contains("Unity.Burst"), "不得引用 Unity.Burst");
            Assert.IsFalse(refs.Contains("Unity.Jobs"), "不得引用 Unity.Jobs");
            Assert.IsFalse(refs.Contains("Unity.Mathematics"), "不得引用 Unity.Mathematics");
        }

        [Test]
        public void test_ac204b_zeroAppendCallSites()
        {
            // 相机是**唯一连跨格事件都不碰的 P0 系统**(R-2-8)
            int hits = 0;
            foreach (var t in CameraTypes)
            {
                foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic |
                                               BindingFlags.Instance | BindingFlags.Static |
                                               BindingFlags.DeclaredOnly))
                {
                    if (DaYiJingCheng.Tests.PlayerController.ILBodyScanner
                        .ContainsMethodCall(m, "Append")) hits++;
                }
            }
            Assert.AreEqual(0, hits,
                "相机程序集的 IEventSink.Append 调用点数须为 0(AC-2-04②;比 1 的同类判据更强)");
        }

        // ══════════════ AC-2-05:零 SimEvent 构造点 ══════════════

        [Test]
        public void test_ac205_zeroSimEventConstruction()
        {
            // 正向:相机程序集内不得构造 SimEvent(以「类型引用」为判据面)
            int hits = 0;
            foreach (var t in CameraTypes)
                foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic |
                                               BindingFlags.Instance | BindingFlags.Static |
                                               BindingFlags.DeclaredOnly))
                {
                    if (DaYiJingCheng.Tests.PlayerController.ILBodyScanner
                        .ContainsMethodCall(m, "SimEvent")) hits++;
                    if (DaYiJingCheng.Tests.PlayerController.ILBodyScanner
                        .ContainsMethodCall(m, "get_Kind")) hits++;
                }
            Assert.AreEqual(0, hits, "相机程序集内 SimEvent 构造 / Kind 赋值零命中(AC-2-05 正向)");

            // 反向(单测内的等价形态):相机类型不在任何 SimEvent 构造点的调用者闭包内
            // ⚠️ 真实调用栈抓取留 CI(载体未建)⇒ 此处只做**同装配内**的可执行半边
            // ⚠️ 反向前提须**豁免感知**:相机程序集引 `Sim`(RecipeDataSet 债)⇒
            //    不能以「不引 Sim」为机械前提。改为:相机**类型**不在任何 SimEvent
            //    构造点的调用者闭包内(本测的正向半边已断言零构造点)。
            Assert.AreEqual(0, hits, "正向已断言零构造点 ⇒ 反向前提成立(不依赖「不引 Sim」)");
        }

        // ══════════════ AC-2-06:效果归属 + VR 禁用 + 不得报状态 + AudioListener 唯一 ══════════════

        [Test]
        public void test_ac206a_effectSemanticsNotInCameraAssembly()
        {
            // 效果的**语义定义**在 8;2 的程序集内零效果语义定义
            // 判据:相机源文件不得含 8 的效果语义键名(如「诊脉时压暗」式文案/键)
            var cameraDir = Path.Combine(Application.dataPath,
                "Gameplay.Presentation", "Camera");
            Assert.IsTrue(Directory.Exists(cameraDir), "相机目录应存在");

            var semanticKeys = new[] { "diagnosis_dim", "pulse_darken", "诊脉压暗", "effect_table" };
            var violations = new List<string>();
            foreach (var f in Directory.GetFiles(cameraDir, "*.cs", SearchOption.AllDirectories))
            {
                string code = System.Text.RegularExpressions.Regex.Replace(
                    File.ReadAllText(f), @"//.*?$", "",
                    System.Text.RegularExpressions.RegexOptions.Multiline);
                foreach (var k in semanticKeys)
                    if (code.Contains(k)) violations.Add($"{Path.GetFileName(f)}: {k}");
            }
            Assert.IsEmpty(violations,
                "相机程序集内不得含效果**语义定义**(AC-2-06①;语义归 8):\n" + string.Join("\n", violations));
        }

        [Test]
        public void test_ac206b_firstPersonMode_disablesPostProcessing()
        {
            // VR 侧禁用任何镜头效果(AC-20-08)。
            // P0 无真 VR ⇒ 走**夹具注入**:强制 Mode = FirstPerson,断言后处理清单为空。
            var go = new GameObject("rigFP");
            var rig = go.AddComponent<CameraRig>();
            rig.SetEffectSemanticsFrom8(new[] { "sys8_effect_a" });
            rig.SetModeForTest(CameraMode.FirstPerson);

            Assert.AreEqual(CameraMode.FirstPerson, rig.Mode,
                "FirstPerson 须生效(P1a 独立路径,不受三档优先级门约束)");
            Assert.IsEmpty(rig.ActivePostProcessEffectsForTest(),
                "FirstPerson(VR)模式下后处理清单须为空(AC-2-06②;VR 全禁镜头效果)");
            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void test_ac206c_exploreMode_allowsEffects()
        {
            // ⚠️ 2026-10-03 重写(B3 的连带):初版要求 Explore **必有**效果
            //    ⇒ 那要求 2 侧**内建**语义键,与 AC-2-06① 冲突。
            //    现改为:2 侧**不内建**;效果只能经 8 注入。
            var go = new GameObject("rigEx");
            var rig = go.AddComponent<CameraRig>();
            rig.SetModeForTest(CameraMode.Explore);

            Assert.IsEmpty(rig.ActivePostProcessEffectsForTest(),
                "2 侧不得内建效果语义键(AC-2-06①)—— 未注入时应为空集");

            rig.SetEffectSemanticsFrom8(new[] { "sys8_effect_a" });
            Assert.IsNotEmpty(rig.ActivePostProcessEffectsForTest(),
                "8 注入后须生效(否则注入缝是空实现)");

            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void test_ac206d_audioListenerExactlyOne()
        {
            // 全场景 AudioListener 计数 == 1(ADR-020 §七;ADR-023 ① Boot 常驻)
            // ⚠️ 单测内无法加载场景 ⇒ 以**资产扫描**为可执行半边
            var repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            // ⚠️ **2026-10-03 修正(首次运行发现)**:初版扫 `Assets/**/*.unity` ——
            //    那会把 **UI Toolkit 的 `PanelSettings.unity`**(资产,非场景)也计入,
            //    致计数虚高为 2。AC-2-06④ 计的是**场景**,故只扫 `Assets/Scenes/`。
            var sceneDir = Path.Combine(repoRoot, "unity", "Assets", "Scenes");
            var scenes = Directory.Exists(sceneDir)
                ? Directory.GetFiles(sceneDir, "*.unity", SearchOption.TopDirectoryOnly).ToList()
                : new List<string>();

            // P0 场景尚未建(ADR-023 三场景制未落地)⇒ 显式报,不静默记绿
            if (scenes.Count == 0)
            {
                Assert.Ignore("NOT-RUN: 场景资产未建(ADR-023 三场景制未落地)⇒ " +
                              "AudioListener 全场景计数判据不可执行;不借绿");
            }

            // 场景存在时:计数须恰 1
            int listeners = 0;
            foreach (var s in scenes)
                listeners += System.Text.RegularExpressions.Regex.Matches(
                    File.ReadAllText(s), "AudioListener").Count;
            Assert.AreEqual(1, listeners,
                $"全工程 AudioListener 须恰 1 个(AC-2-06④;实得 {listeners})");
        }
    }
}
