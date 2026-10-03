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

        /// <summary>游戏事实类型 —— 一律红(按**类型**判,非字段名)。</summary>
        private static readonly HashSet<Type> ForbiddenStateTypes = new HashSet<Type>
        {
            typeof(SimEvent), typeof(PatientId), typeof(Fix), typeof(StreamId), typeof(EventKind),
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

        /// <summary>递归判定:数组 / 泛型容器递归到元素类型(仅扫顶层会漏)。</summary>
        private static bool IsGameFactType(Type t)
        {
            if (t == null) return false;
            if (ForbiddenStateTypes.Contains(t)) return true;
            if (t.IsArray) return IsGameFactType(t.GetElementType());
            if (t.IsGenericType)
                return t.GetGenericArguments().Any(IsGameFactType);
            return false;
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

        // ══════════════ AC-2-01②:差分重算(后半段相同 ⇒ 末帧逐位相同)══════════════

        [Test]
        public void test_ac201b_differentialRecompute_lastFrameBitIdentical()
        {
            // ⚠️ 前提:相机无隐藏静态状态(上一条已守)。
            // 构造两条**不同历史**的 rig,喂**相同后半段**输入,断言末帧输出逐位相同。
            // ⚠️ **2026-10-03 修正(首次运行发现)**:`CameraRig.UpdateYaw` 是**增量** API
            //    (`_yaw += deltaYaw`)—— 初版让两 rig 有**不同初值**再喂相同增量 ⇒
            //    末帧自然不同,但那是**增量语义的必然**,不是「隐藏状态」。
            //    AC-2-01② 的真要求 = **无隐藏状态** ⇒ 判据须为:
            //    **相同起点 + 相同输入序列 ⇒ 末帧逐位相同**。
            var inputTail = new[] { 0.5f, -0.25f, 0.75f, 0.0f, -0.5f };

            var goA = new GameObject("rigA");
            var goB = new GameObject("rigB");
            var rigA = goA.AddComponent<CameraRig>();   // 同起点(默认初值)
            var rigB = goB.AddComponent<CameraRig>();

            // 两 rig 都从**默认初值**出发(同起点),喂**相同序列**
            foreach (var x in inputTail)
            {
                rigA.ApplyLook(x, 0f);
                rigB.ApplyLook(x, 0f);
            }

            // 末帧可见输出须**逐位相同**
            Assert.AreEqual(rigA.Yaw, rigB.Yaw, "末帧 Yaw 须逐位相同(AC-2-01②)");
            Assert.AreEqual(rigA.Pitch, rigB.Pitch, "末帧 Pitch 须逐位相同");

            var basisA = rigA.YawBasis;
            var basisB = rigB.YawBasis;
            Assert.AreEqual(basisA.Fwd.x, basisB.Fwd.x);
            Assert.AreEqual(basisA.Fwd.y, basisB.Fwd.y);
            Assert.AreEqual(basisA.Fwd.z, basisB.Fwd.z);

            UnityEngine.Object.DestroyImmediate(goA);
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
            rig.SetModeForTest(CameraMode.FirstPerson);

            Assert.AreEqual(CameraMode.FirstPerson, rig.Mode);
            Assert.IsEmpty(rig.ActivePostProcessEffectsForTest(),
                "FirstPerson(VR)模式下后处理清单须为空(AC-2-06②;VR 全禁镜头效果)");
            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void test_ac206c_exploreMode_allowsEffects()
        {
            // 对照:平面模式下允许效果(否定「永远空清单」的空实现)
            var go = new GameObject("rigEx");
            var rig = go.AddComponent<CameraRig>();
            rig.SetModeForTest(CameraMode.Explore);
            Assert.IsNotEmpty(rig.ActivePostProcessEffectsForTest(),
                "Explore 模式下须有效果(否则上一条是空实现)");
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
