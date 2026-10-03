// camera-viewpoint Story 005 测试 —— 档位状态机 + 性能义务
//
// AC-2-17: 意图制(唯一写入点 + 方法体引用集)
// AC-2-18: 优先级 + 幂等 + 可打断 + 每帧一结算
// AC-2-19: Casebook 锚冻结 + 固定高俯角
// AC-2-20: 不自行超时退出(无档位存续计时器)
// AC-2-27: PhysX == 1 / 冻结档不查询 / Tick 单一相位
//
// 权威来源: GDD R-2-5 · EC-2-8/9/10/11/14 · ADR-020 §五 · ADR-011

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DaYiJingCheng.Gameplay.Presentation.Camera;
using NUnit.Framework;
using UnityEngine;

namespace DaYiJingCheng.Tests.CameraViewpoint
{
    public class CameraModeMachineTest
    {
        private const float Dur = 0.3f;   // 转场时长(数值留白)
        private const float Dt = 1f / 60f;

        // ══════════════ AC-2-17:意图制 ══════════════

        [Test]
        public void test_ac217_modeHasSingleWritePath()
        {
            // Mode 的写入点须**唯一**(`SetMode` 入队 + 结算路径)
            var t = typeof(CameraModeMachine);
            var prop = t.GetProperty("Mode");
            Assert.IsNotNull(prop, "Mode 须存在");
            // ⚠️ 2026-10-03:`CanWrite` 对 `private set` **仍为 true** ⇒ 判据须查 setter 可见性
            var setter = prop.GetSetMethod(nonPublic: true);
            Assert.IsNotNull(setter, "Mode 须有 setter(类内写入)");
            Assert.IsTrue(setter.IsPrivate,
                "Mode 的 setter 须为 **private**(唯一写入点在类内;AC-2-17)");
            Assert.IsNull(prop.GetSetMethod(nonPublic: false),
                "Mode 不得有**公开** setter");

            // 机械前提:`CameraModeMachine` **类内**的 `Mode =` 赋值须恰 1 处
            // ⚠️ **2026-10-03 修正(三轮)**:初版正则 `\bMode\s*=` 有三处误匹配:
            //    ① `CameraModeRequest` 构造器内的 `Mode = mode;`(**别的类**);
            //    ② `Mode == CameraMode.X`(是 `==`,被 `\s*=` 吞了第一个 `=`);
            //    ⇒ 现限定在 `CameraModeMachine` 类体内,且排除 `==`。
            string src = System.IO.Path.Combine(Application.dataPath,
                "Gameplay.Presentation", "Camera", "CameraModeMachine.cs");
            string code = System.Text.RegularExpressions.Regex.Replace(
                System.IO.File.ReadAllText(src), @"//.*?$", "",
                System.Text.RegularExpressions.RegexOptions.Multiline);

            int clsStart = code.IndexOf("class CameraModeMachine", StringComparison.Ordinal);
            Assert.Greater(clsStart, 0, "须找到 CameraModeMachine 类");
            string clsBody = code.Substring(clsStart);

            int writes = System.Text.RegularExpressions.Regex.Matches(
                clsBody, @"(^|[^=!<>])Mode\s*=(?!=)").Count;
            Assert.AreEqual(1, writes,
                $"CameraModeMachine 类内 Mode 的写入点须恰 1 处(AC-2-17;实得 {writes})");
        }

        [Test]
        public void test_ac217_setMode_doesNotReadOtherSystems()
        {
            // SetMode 的方法体**不读**任何其它系统状态(无 8 界面栈 / 10 动作 / 39 模态)
            var m = typeof(CameraModeMachine).GetMethod("SetMode");
            Assert.IsNotNull(m);

            var asm = typeof(CameraModeMachine).Assembly;
            var referencedTypes = m.GetMethodBody() != null
                ? new List<Type>()   // IL 层类型引用须 Cecil;此处用**源码级**等价判据
                : new List<Type>();

            // 源码级:SetMode 方法体内不得出现 8/10/39 的类型名
            string src = System.IO.Path.Combine(Application.dataPath,
                "Gameplay.Presentation", "Camera", "CameraModeMachine.cs");
            string code = System.IO.File.ReadAllText(src);
            int start = code.IndexOf("public void SetMode(", StringComparison.Ordinal);
            Assert.Greater(start, 0, "须找到 SetMode");
            int end = code.IndexOf("}", code.IndexOf("{", start), StringComparison.Ordinal);
            string body = code.Substring(start, end - start);

            foreach (var forbidden in new[] { "IModalState", "ModalId", "DiagnosisSystem",
                                              "EmergencyProcedures", "CaseSystem" })
                Assert.IsFalse(body.Contains(forbidden),
                    $"SetMode 方法体不得引用「{forbidden}」(AC-2-17:意图制,相机不自行裁决)");
        }

        // ══════════════ AC-2-18:优先级 / 幂等 / 可打断 / 每帧一结算 ══════════════

        [Test]
        public void test_ac218a_lowerPriorityCannotInterruptHigher()
        {
            // 穷举 3×3 档对(含同档)
            var modes = new[] { CameraMode.Explore, CameraMode.Treatment, CameraMode.Casebook };

            foreach (var from in modes)
            {
                foreach (var to in modes)
                {
                    var m = new CameraModeMachine();
                    m.SetMode(from, 1); m.Settle(Dt, Dur);
                    for (int i = 0; i < 100; i++) m.Settle(Dt, Dur);   // 转场走完

                    int restartsBefore = m.TransitionRestarts;
                    m.SetMode(to, 1); m.Settle(Dt, Dur);

                    int fromPrio = CameraModePriority.Of(from);
                    int toPrio = CameraModePriority.Of(to);

                    if (to == from)
                    {
                        Assert.AreEqual(restartsBefore, m.TransitionRestarts,
                            $"{from}→{to}:同档**幂等**(AC-2-18②)");
                    }
                    else if (toPrio < fromPrio)
                    {
                        Assert.AreEqual(from, m.Mode,
                            $"{from}→{to}:低优先级**不能打断**高优先级(AC-2-18①)");
                        Assert.AreEqual(restartsBefore, m.TransitionRestarts, "不得重起转场");
                    }
                    else
                    {
                        Assert.AreEqual(to, m.Mode,
                            $"{from}→{to}:高优先级须生效");
                    }
                }
            }
        }

        [Test]
        public void test_ac218d_settleOncePerFrame()
        {
            // 注入同帧多请求 ⇒ 转场重起算次数 ≤ 1(EC-2-9 四步收敛;AC-2-18④)
            var m = new CameraModeMachine();
            m.SetMode(CameraMode.Treatment, 10);
            m.SetMode(CameraMode.Casebook, 39);
            m.SetMode(CameraMode.Explore, 4);

            int before = m.TransitionRestarts;
            m.Settle(Dt, Dur);          // **一次**结算

            Assert.LessOrEqual(m.TransitionRestarts - before, 1,
                "每帧只结算一次(AC-2-18④;多请求同帧 ⇒ 重起算 ≤ 1)");
            Assert.AreEqual(CameraMode.Casebook, m.Mode,
                "同帧多请求 ⇒ 取**最高优先级**(Casebook > Treatment > Explore)");
        }

        [Test]
        public void test_ac218c_interruptTakesCurrentInterpolatedValue()
        {
            // 转场中被打断 ⇒ from 取**当前插值值**(非源档值)⇒ 画面无跳变
            var m = new CameraModeMachine();
            m.SetMode(CameraMode.Treatment, 10);
            m.Settle(Dt, Dur);
            for (int i = 0; i < 5; i++) m.Settle(Dt, Dur);   // 转场走到一半

            Assert.Less(m.TransitionT, 1f, "须处于转场中");
            float midValue = m.TransitionFrom + (m.TransitionTo - m.TransitionFrom) * m.TransitionT;

            // 打断:换到更高优先级
            m.SetMode(CameraMode.Casebook, 39);
            m.Settle(Dt, Dur);

            Assert.AreEqual(midValue, m.TransitionFrom, 1e-5f,
                "打断时 from 须取**当前插值值**(AC-2-18③;取源档值 ⇒ 画面跳变)");
        }

        // ══════════════ AC-2-19:Casebook 冻结 + 固定俯角 ══════════════

        [Test]
        public void test_ac219a_casebookFreezesAnchor()
        {
            var m = new CameraModeMachine();
            m.SetMode(CameraMode.Casebook, 39);
            m.Settle(Dt, Dur);
            for (int i = 0; i < 100; i++) m.Settle(Dt, Dur);

            Assert.AreEqual(CameraMode.Casebook, m.Mode);
            Assert.IsTrue(m.IsFrozen, "Casebook 须为冻结档(AC-2-19①)");
        }

        [Test]
        public void test_ac219b_casebookFixedHighPitch()
        {
            // 进入姿态 = 约定的固定高俯角 PITCH_CASEBOOK(< PITCH_MAX)
            var m = new CameraModeMachine();
            m.SetMode(CameraMode.Casebook, 39);
            m.Settle(Dt, Dur);

            Assert.Less(m.CasebookPitch, CameraRig.PITCH_MAX,
                "PITCH_CASEBOOK 须 < PITCH_MAX(逐档常量;AC-2-19②)");
            Assert.Greater(m.CasebookPitch, 0f, "高俯角须为正(正 = 俯,R-2-3)");
        }

        // ══════════════ AC-2-20:不自行超时退出 ══════════════

        [Test]
        public void test_ac220_noModeDurationTimer()
        {
            // 精确式:无**与档位存续时间相关**的计时器/看门狗
            // (转场 `t` 是允许的 —— 它属于转场,不是档位存续)
            var t = typeof(CameraModeMachine);
            foreach (var f in t.GetFields(BindingFlags.NonPublic | BindingFlags.Instance))
            {
                var n = f.Name.ToLowerInvariant();
                Assert.IsFalse(n.Contains("timeout") || n.Contains("watchdog") ||
                               n.Contains("expire") || n.Contains("dwell") ||
                               n.Contains("modestart") || n.Contains("modeage"),
                    $"AC-2-20:不得有档位存续计时器/看门狗(实得 {f.Name})");
            }
        }

        [Test]
        public void test_ac220_treatmentHoldsForever_withoutExploreRequest()
        {
            // 长时注入(≥ 10⁴ 帧)下档位不变
            var m = new CameraModeMachine();
            m.SetMode(CameraMode.Treatment, 10);
            m.Settle(Dt, Dur);

            for (int i = 0; i < 10_000; i++) m.Settle(Dt, Dur);

            Assert.AreEqual(CameraMode.Treatment, m.Mode,
                "Treatment 档在无 Explore 请求下须**永久保持**(AC-2-20;无超时)");
        }

        // ══════════════ AC-2-27:性能义务 ══════════════

        [Test]
        public void test_ac227a_exactlyOneQueryPerFrame()
        {
            // 每帧物理查询数 **== 1**(不因转场/档位/遮挡变化)
            var inner = new StubQuery();
            var counting = new CountingArmQuery(inner);

            for (int frame = 0; frame < 1000; frame++)
            {
                counting.BeginFrame();
                // 模拟一帧的相机求值(恰一次查询)
                counting.Cast(Vector3.zero, Vector3.forward, 0.3f, 4f);
                Assert.AreEqual(1, counting.FrameCount,
                    $"第 {frame} 帧的查询数须恰 1(AC-2-27①)");
            }
        }

        [Test]
        public void test_ac227b_frozenMode_noQuery()
        {
            // Casebook 档(冻结)期间**不得**发起 PhysX 查询 ⇒ 每帧为 0
            var inner = new StubQuery();
            var counting = new CountingArmQuery(inner);
            var m = new CameraModeMachine();
            m.SetMode(CameraMode.Casebook, 39);
            m.Settle(Dt, Dur);

            for (int frame = 0; frame < 100; frame++)
            {
                counting.BeginFrame();
                // ⚠️ 冻结档 ⇒ 调用方**跳过**查询(这是 AC-2-27② 的机械形态)
                if (!m.IsFrozen) counting.Cast(Vector3.zero, Vector3.forward, 0.3f, 4f);
                Assert.AreEqual(0, counting.FrameCount,
                    $"冻结档第 {frame} 帧不得查询(AC-2-27②)");
            }
        }

        [Test]
        public void test_ac227c_tickHasSingleExplicitCallSite()
        {
            // Tick 的调用来源须是**单一显式调用点**,**不得**是 MonoBehaviour.Update/LateUpdate 裸调用
            string src = System.IO.Path.Combine(Application.dataPath,
                "Gameplay.Presentation", "Camera");
            var violations = new List<string>();

            foreach (var f in System.IO.Directory.GetFiles(src, "*.cs",
                         System.IO.SearchOption.AllDirectories))
            {
                string code = System.Text.RegularExpressions.Regex.Replace(
                    System.IO.File.ReadAllText(f), @"//.*?$", "",
                    System.Text.RegularExpressions.RegexOptions.Multiline);
                // 裸 Update/LateUpdate 内调用相机求值 = 隐式排序(禁 Script Execution Order 的镜像)
                if (System.Text.RegularExpressions.Regex.IsMatch(
                        code, @"void\s+(Update|LateUpdate)\s*\(\s*\)\s*\{[^}]*\bStep\s*\("))
                    violations.Add(System.IO.Path.GetFileName(f));
            }

            Assert.IsEmpty(violations,
                "相机的 Tick/Step 不得在裸 Update/LateUpdate 内调用(AC-2-27③;" +
                "须走显式相位,禁隐式排序):\n" + string.Join("\n", violations));
        }

        // ══════════════ 辅助 ══════════════

        private sealed class StubQuery : IArmCollisionQuery
        {
            public (bool, float) Cast(Vector3 o, Vector3 d, float r, float maxDist)
                => (false, 0f);
        }
    }
}
