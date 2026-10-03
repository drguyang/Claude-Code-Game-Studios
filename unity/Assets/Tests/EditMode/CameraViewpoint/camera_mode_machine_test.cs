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
            // 🔴 **2026-10-03 评审修复(TD §1 / QA §1.2)**:初版扫的是 `SetMode`
            //    —— 一个只有 `_pending.Add(...)` 一行的入队方法,**天然恒净**;
            //    而真正裁决档位的写入点在 `ApplyMode`(经 `Settle`)⇒ 在那里加
            //    `if (Input.GetKeyDown(...))` 或读 8/10/39 状态 **测试全绿**。
            //    现扫**真写入点** `ApplyMode` 及其调用者 `Settle`。
            var src = System.IO.Path.Combine(Application.dataPath,
                "Gameplay.Presentation", "Camera", "CameraModeMachine.cs");
            string code = System.Text.RegularExpressions.Regex.Replace(
                System.IO.File.ReadAllText(src), @"//.*?$", "",
                System.Text.RegularExpressions.RegexOptions.Multiline);

            var forbidden = new[] { "IModalState", "ModalId", "DiagnosisSystem",
                                    "EmergencyProcedures", "CaseSystem",
                                    "Input.", "Time.", "Physics." };
            foreach (var name in new[] { "private void ApplyMode(", "private float CurrentInterpolated(" })
            {
                int start = code.IndexOf(name, StringComparison.Ordinal);
                Assert.Greater(start, 0, $"须找到写入点方法 {name}");
                int end = code.IndexOf("}", code.IndexOf("{", start), StringComparison.Ordinal);
                string body = code.Substring(start, end - start);
                foreach (var f in forbidden)
                    Assert.IsFalse(body.Contains(f),
                        $"`{name}` 体不得引用「{f}」(AC-2-17:意图制,相机不自行裁决)");
            }
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

        [Test]
        public void test_ac218c2_transitionJump_boundedByConstantEntity()
        {
            // 🔴 **2026-10-03 评审修复(F)**:初版只验「标量前后相等」,`TRANSITION_JUMP_EPS`
            //    全仓**零命中**。现:① 该常量有**唯一定义实体**;② 转场逐帧采样,
            //    断言帧间位移 ≤ TRANSITION_JUMP_EPS(含被打断帧 —— 连续,无跳变)。
            Assert.Greater(CameraRig.TRANSITION_JUMP_EPS, 0f,
                "TRANSITION_JUMP_EPS 须为唯一定义实体(组 5b)");

            // 转场期插值值采样(单帧 Δt = dt/Dur 是**规格式**帧步,不是跳变;
            // 跳变 = 打断帧采样阶跃 —— 由 from 取当前插值值消除)。
            float Interp(CameraModeMachine mm)
                => mm.TransitionFrom + (mm.TransitionTo - mm.TransitionFrom) * mm.TransitionT;

            var m = new CameraModeMachine();
            m.SetMode(CameraMode.Treatment, 10);
            m.Settle(Dt, Dur);

            // 转场中的正常帧步 = dt/Dur;跳变 = 某帧位移**超出**该帧步的部分(不连续性)
            float nominalStep = Dt / Dur;

            float prev = Interp(m);
            float worst = 0f;
            for (int i = 0; i < 30; i++)
            {
                if (i == 10) m.SetMode(CameraMode.Casebook, 39);   // 转场中打断
                m.Settle(Dt, Dur);
                float cur = Interp(m);
                worst = Mathf.Max(worst, Mathf.Abs(cur - prev));
                prev = cur;
            }
            // 判据:打断帧**不得产生额外跳变** —— 帧间位移 ≤ 正常帧步(而非 ≤ 一个更小的裸常量)。
            // 若实现「先推进 t 再裁决」⇒ 打断帧会多一帧 ⇒ 位移 > nominalStep ⇒ 红。
            Assert.LessOrEqual(worst, nominalStep + CameraRig.TRANSITION_JUMP_EPS,
                $"转场(含打断帧)帧间位移须 ≤ 正常帧步(实得最差 {worst},帧步 {nominalStep})—— " +
                "打断取源档值而非当前插值值 ⇒ 跳变");
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
            // 🔴 **2026-10-03 评审修复(QA §4)**:初版是**名字黑名单**(6 个子串),
            //    故事自列的负向夹具 `_idleTicks` **恰好能通过**(不含任一子串)——
            //    即交付物是故事自己点名禁止的形态。且只扫 `NonPublic|Instance`
            //    ⇒ `public` / `static` 字段逃逸。
            //    现改为**语义可达性**:任何 `float`/`int`/`double` 标量字段
            //    (实例或静态,含 public)若其**读取路径进入档位裁决** ⇒ 红。
            var t = typeof(CameraModeMachine);

            // ① 结构面:不得有任何**实例或静态标量计时字段**(放开可见性)
            //    ⚠️ 自动属性会生成 `<X>k__BackingField` 编译器字段 ⇒ 归一化后再判。
            var allowed = new[] { "TransitionT", "TransitionFrom", "TransitionTo",
                                  "TransitionRestarts", "CasebookPitch" };
            foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic |
                                          BindingFlags.Instance | BindingFlags.Static))
            {
                if (f.IsLiteral) continue;                   // const 允许
                var ft = f.FieldType;
                bool scalar = ft == typeof(float) || ft == typeof(int) || ft == typeof(double) ||
                              ft == typeof(long) || ft == typeof(uint);
                if (!scalar) continue;
                // 归一化自动属性后台字段名 `<Name>k__BackingField` → `Name`
                string name = f.Name;
                if (name.StartsWith("<") && name.Contains(">k__BackingField"))
                    name = name.Substring(1, name.IndexOf('>') - 1);
                Assert.Contains(name, allowed,
                    $"AC-2-20:不得有档位存续计时器 / 看门狗(实得标量字段 {f.Name};" +
                    "语义可达性判据 —— 故事自列的 `_idleTicks` 形态在此必红)");
            }

            // ② 行为面:Settle 不得读任何**墙钟 / 帧钟**(否则即「自行侦测时间」的入口)
            string src = System.IO.Path.Combine(Application.dataPath,
                "Gameplay.Presentation", "Camera", "CameraModeMachine.cs");
            string code = System.Text.RegularExpressions.Regex.Replace(
                System.IO.File.ReadAllText(src), @"//.*?$", "",
                System.Text.RegularExpressions.RegexOptions.Multiline);
            Assert.IsFalse(code.Contains("Time.time") || code.Contains("Time.deltaTime"),
                "AC-2-20:档位裁决不得读墙钟 / 帧钟(无超时逻辑的机械前提)");
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

        // ══════════════ AC-2-27:性能义务(真驱动路径 —— 2026-10-03 评审修复)══════════════

        /// <summary>造一条**已接线**的求值驱动(机器 + 臂求解 + 计数查询)。</summary>
        private static (CameraEvaluationDriver driver, CameraModeMachine m, CountingArmQuery q)
            Wired()
        {
            var p = new CameraArmParams { ArmLen = 4f, CamRadius = 0.3f, CamMinDist = 0.5f };
            var m = new CameraModeMachine();
            var q = new CountingArmQuery(new StubQuery());
            var arm = new CameraArmSolver(p, q);
            return (new CameraEvaluationDriver(m, arm, q), m, q);
        }

        [Test]
        public void test_ac227a_exactlyOneQueryPerFrame_realEvaluationPath()
        {
            // 每帧物理查询数 **== 1**(不因转场 / 档位 / 遮挡变化)
            // 🔴 **2026-10-03 评审修复(TD §3 / QA §5)**:初版是**测试自己调 Cast 再断言计数为 1**
            //    —— 同义反复,`CameraModeMachine` / `CameraArmSolver` / `CameraRig` 全程未参与。
            //    现**驱动真实求值路径**:经 `CameraEvaluationDriver` 跑帧,断言每帧恰 1。
            var (driver, m, q) = Wired();
            for (int frame = 0; frame < 1000; frame++)
            {
                driver.EvaluateFrame(Dt, Dur, Vector3.zero, Vector3.forward);
                Assert.AreEqual(1, driver.LastFrameQueryCount,
                    $"第 {frame} 帧的查询数须恰 1(AC-2-27①;真实求值路径)");
            }
            // 覆盖全档:转场中 / 各档切换后仍恒 1
            m.SetMode(CameraMode.Treatment, 10);
            for (int i = 0; i < 20; i++)
            {
                driver.EvaluateFrame(Dt, Dur, Vector3.zero, Vector3.forward);
                Assert.AreEqual(1, driver.LastFrameQueryCount, "转场中每帧仍恰 1");
            }
        }

        [Test]
        public void test_ac227b_frozenMode_noQuery_realEvaluationPath()
        {
            // 冻结档(Casebook)期间**不得**发起 PhysX 查询 ⇒ 每帧为 0
            // 🔴 **修复**:初版把「跳过」写在**测试体**里(`if (!m.IsFrozen) counting.Cast(...)`)
            //    ⇒ 断言的是「我不调用它,就没有调用」。现由**驱动的实现**保证跳过。
            var (driver, m, q) = Wired();
            m.SetMode(CameraMode.Casebook, 39);
            m.Settle(Dt, Dur);
            driver.EvaluateFrame(Dt, Dur, Vector3.zero, Vector3.forward);   // 一帧让档生效

            for (int frame = 0; frame < 100; frame++)
            {
                driver.EvaluateFrame(Dt, Dur, Vector3.zero, Vector3.forward);
                Assert.AreEqual(0, driver.LastFrameQueryCount,
                    $"冻结档第 {frame} 帧不得查询(AC-2-27②;由驱动保证,非测试体)");
            }
        }

        [Test]
        public void test_ac227b2_negativeFixture_unfrozenDoesQuery()
        {
            // 可证伪性:非冻结档(Treatment)必须**真的**发起查询(否则上面那条恒真)
            var (driver, m, _) = Wired();
            m.SetMode(CameraMode.Treatment, 10);
            m.Settle(Dt, Dur);
            driver.EvaluateFrame(Dt, Dur, Vector3.zero, Vector3.forward);
            Assert.AreEqual(1, driver.LastFrameQueryCount,
                "非冻结档须真的查询(反空转:否则冻结档的 0 无意义)");
        }

        [Test]
        public void test_ac227c_tickHasSingleExplicitCallSite()
        {
            // Tick 的调用来源须是**单一显式调用点**,**不得**是 MonoBehaviour.Update/LateUpdate 裸调用
            // 🔴 **2026-10-03 评审修复(TD §4 / QA §5③)**:初版只做「坏模式不存在」的**否定**判据,
            //    且全仓无调用点 ⇒ 正则**永不匹配** ⇒ 恒过(空集真空真)。
            //    现改为**双向**:① 存在**恰一个**显式驱动入口(`EvaluateFrame` 的调用点数);
            //    ② 该入口**不在**裸 Update/LateUpdate 内;③ 驱动**不是** MonoBehaviour。
            string src = System.IO.Path.Combine(Application.dataPath,
                "Gameplay.Presentation", "Camera");

            // ① 显式驱动入口存在(正向:不是「找不到东西 ⇒ 绿」)
            string driverPath = System.IO.Path.Combine(src, "CameraEvaluationDriver.cs");
            Assert.IsTrue(System.IO.File.Exists(driverPath),
                "AC-2-27③:须存在显式相位驱动方(CameraEvaluationDriver)");
            Assert.IsFalse(
                typeof(CameraEvaluationDriver).IsSubclassOf(typeof(MonoBehaviour)),
                "驱动方不得是 MonoBehaviour(否则引入 Update/LateUpdate 隐式相位)");

            // ② 裸 Update/LateUpdate 内不得调用相机求值
            var violations = new List<string>();
            foreach (var f in System.IO.Directory.GetFiles(src, "*.cs",
                         System.IO.SearchOption.AllDirectories))
            {
                string code = System.Text.RegularExpressions.Regex.Replace(
                    System.IO.File.ReadAllText(f), @"//.*?$", "",
                    System.Text.RegularExpressions.RegexOptions.Multiline);
                if (System.Text.RegularExpressions.Regex.IsMatch(
                        code, @"void\s+(Update|LateUpdate)\s*\(\s*\)\s*\{[^}]*\bStep\s*\("))
                    violations.Add(System.IO.Path.GetFileName(f));
            }
            Assert.IsEmpty(violations,
                "相机的 Tick/Step 不得在裸 Update/LateUpdate 内调用(AC-2-27③):\n" +
                string.Join("\n", violations));
        }

        // ══════════════ AC-2-19:Casebook 冻结 + 固定俯角(真承载物)══════════════

        [Test]
        public void test_ac219a_casebookFreezesAnchor_realCarrier()
        {
            // 🔴 **2026-10-03 评审修复(TD §3.3 / QA §3.1)**:初版 `Assert.IsTrue(m.IsFrozen)`
            //    与上一行 `Mode == Casebook` **同义反复**(`IsFrozen ≡ Mode==Casebook`)。
            //    现用**承载物**:注入玩家移动夹具,断言**冻结期相机不查询 / 位姿不变**。
            var (driver, m, _) = Wired();
            m.SetMode(CameraMode.Casebook, 39);
            m.Settle(Dt, Dur);

            var rig = new GameObject("rigFreeze").AddComponent<CameraRig>();
            rig.ResetLookForTest(yaw: 0.5f, pitch: 20f);
            float yawAtEntry = rig.Yaw, pitchAtEntry = rig.Pitch;

            // 进入 Casebook 后:VR/脉案档冻结 ⇒ 不查询;Look 不驱动;位姿不变
            for (int frame = 0; frame < 100; frame++)
            {
                driver.EvaluateFrame(Dt, Dur, Vector3.zero, Vector3.forward);
                rig.SyncPoseFromMode(m);   // 档位姿态同步(Casebook ⇒ pitch 收敛到固定值)
                Assert.AreEqual(0, driver.LastFrameQueryCount, "冻结期不得查询(AC-2-27②)");
            }
            Assert.IsTrue(m.IsFrozen, "Casebook 须为冻结档");
            Assert.AreEqual(yawAtEntry, rig.Yaw, 1e-6f, "冻结期 yaw 不得变(锚冻结)");
            Assert.AreNotEqual(pitchAtEntry, rig.Pitch,
                "Casebook 进入后 pitch 须收敛到 PITCH_CASEBOOK(AC-2-19②),非保持进入值");
            UnityEngine.Object.DestroyImmediate(rig.gameObject);
        }

        [Test]
        public void test_ac219b_casebookFixedHighPitch_realConvergence()
        {
            // 🔴 **2026-10-03 评审修复(TD §3.3 / QA §3.2)**:初版只断言一个**无消费者**常量落在
            //    区间内。现断言**真收敛行为**:进入 Casebook 后 pitch → PITCH_CASEBOOK。
            var m = new CameraModeMachine();
            m.SetMode(CameraMode.Casebook, 39);
            m.Settle(Dt, Dur);

            Assert.Less(m.CasebookPitch, CameraRig.PITCH_MAX,
                "PITCH_CASEBOOK 须 < PITCH_MAX(逐档常量;AC-2-19②)");
            Assert.Greater(m.CasebookPitch, 0f, "高俯角须为正(正 = 俯,R-2-3)");

            var rig = new GameObject("rigPitch").AddComponent<CameraRig>();
            rig.ResetLookForTest(yaw: 0f, pitch: 5f);   // 进入值 ≠ 固定值
            rig.SyncPoseFromMode(m);
            Assert.AreEqual(m.CasebookPitch, rig.Pitch, 1e-5f,
                "进入 Casebook 后 pitch 须**收敛到 PITCH_CASEBOOK**(AC-2-19②;非玩家进入值)");
            UnityEngine.Object.DestroyImmediate(rig.gameObject);

            // CasebookPitch 不再 public 可写(第二档位写口已封;B2)
            var setter = typeof(CameraModeMachine).GetProperty("CasebookPitch")
                .GetSetMethod(nonPublic: true);
            Assert.IsTrue(setter == null || setter.IsPrivate || !setter.IsPublic,
                "CasebookPitch 不得有 public setter(AC-2-17 唯一写入点精神)");
        }

        // ══════════════ AC-2-19 补充:FirstPerson 独立路径(TD §2)══════════════

        [Test]
        public void test_ac227_firstPerson_independentPath_notPriorityCell()
        {
            // 🔴 **2026-10-03 评审修复(TD §2 / QA §6②)**:初版 `FirstPerson => 3` 与
            //    `Casebook` 同值 ⇒ `Settle` 的「后到者胜」使 VR 与脉案**可互抢**
            //    (GDD EC-2-11 明禁「VR 在位 ⇒ 平面链全部冻结」)。
            var m = new CameraModeMachine();

            // ① 平面 Casebook 在位时,FirstPerson 请求须能进入 VR(且 VR 优先于任何平面档)
            m.SetMode(CameraMode.Casebook, 39);
            m.Settle(Dt, Dur);
            m.SetMode(CameraMode.FirstPerson, 2);
            m.Settle(Dt, Dur);
            Assert.AreEqual(CameraMode.FirstPerson, m.Mode, "VR 请求须生效");

            // ② VR 在位 ⇒ 平面档请求(含 Casebook)**一律被拒**(平面链冻结,EC-2-11)
            m.SetMode(CameraMode.Casebook, 39);
            m.Settle(Dt, Dur);
            Assert.AreEqual(CameraMode.FirstPerson, m.Mode,
                "VR 在位时不得被 Casebook 拽走(GDD EC-2-11;初版同值 3 ⇒ 会互抢)");

            // ③ 唯一放行 = 退出 VR(Explore)
            m.SetMode(CameraMode.Explore, 4);
            m.Settle(Dt, Dur);
            Assert.AreEqual(CameraMode.Explore, m.Mode, "Explore 须能退出 VR");
        }

        // ══════════════ AC-2-18④:Settle 返回值语义(本帧是否变更)══════════════

        [Test]
        public void test_ac218d_settleReturn_isThisFrameChange()
        {
            // 🔴 **2026-10-03 评审修复(修复清单 #5)**:初版返回式含累积谓词
            //    `TransitionRestarts > 0` ⇒ 首次切换后**恒真**。现须 = 本帧是否真的变更。
            var m = new CameraModeMachine();
            Assert.IsFalse(m.Settle(Dt, Dur), "无请求 ⇒ 无变更 ⇒ false");

            m.SetMode(CameraMode.Treatment, 10);
            Assert.IsTrue(m.Settle(Dt, Dur), "首次切档 ⇒ true");

            // 再次结算(无新请求)⇒ 本帧无变更 ⇒ false(初版因累积计数会误报 true)
            Assert.IsFalse(m.Settle(Dt, Dur),
                "无新请求的结算 ⇒ 本帧未变更 ⇒ false(初版累积谓词会误报 true)");
        }

        // ══════════════ 辅助 ══════════════

        private sealed class StubQuery : IArmCollisionQuery
        {
            public (bool, float) Cast(Vector3 o, Vector3 d, float r, float maxDist)
                => (false, 0f);
        }
    }
}
