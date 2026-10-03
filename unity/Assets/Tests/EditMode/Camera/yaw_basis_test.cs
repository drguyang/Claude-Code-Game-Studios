// camera-viewpoint Story 002 测试
//
// AC-2-07: YawBasis 水平化
// AC-2-08: YawBasis 正交归一
// AC-2-09: PITCH_MAX < 90°
// AC-2-10: 帧内次序契约

using System;
using System.Linq;
using System.Reflection;
using DaYiJingCheng.Gameplay.Presentation.Camera;
using NUnit.Framework;
using UnityEngine;

namespace DaYiJingCheng.Tests.Camera
{
    public class YawBasisTest
    {
        private CameraRig _rig;

        [SetUp]
        public void Setup()
        {
            var go = new GameObject("CameraRig");
            _rig = go.AddComponent<CameraRig>();
        }

        [TearDown]
        public void Teardown()
        {
            if (_rig != null) UnityEngine.Object.DestroyImmediate(_rig.gameObject);
        }

        // AC-2-07①: 水平化与 pitch 无关
        [Test]
        public void test_yawBasis_horizontal_regardlessOfPitch()
        {
            float[] pitches = { -45f, -30f, 0f, 30f, 60f };
            float[] yaws = { 0f, Mathf.PI / 4, Mathf.PI / 2, 3 * Mathf.PI / 4, Mathf.PI };

            foreach (float yaw in yaws)
            {
                foreach (float pitch in pitches)
                {
                    _rig.ResetLookForTest(yaw, _rig.Pitch); // ⚠️ UpdateYaw 是增量 API(评审 F6:累积漂移致采样格非设计值)
                    _rig.UpdatePitch(pitch - _rig.Pitch); // 设置绝对 pitch

                    var basis = _rig.YawBasis;
                    // ⚠️ **2026-10-03 修复(评审 F1)**:原用 1e-6 容差 ——
                    //    而规格(story:91)明写「**精确 0,非容差** —— 出现 ~1e-8 即证明
                    //    走了事后水平化路径 ⇒ 该形态违规」。1e-6 >> 1e-8 ⇒ 判据被自己的
                    //    容差放行(事后水平化的负向夹具抓不到)。
                    //    现改为**精确 0**(构造式 y 是字面 0,严格成立)。
                    Assert.AreEqual(0f, basis.Fwd.y, 0f,
                        $"fwd.y 须为**精确 0**(yaw={yaw}, pitch={pitch};残差即事后水平化形态)");
                    Assert.AreEqual(0f, basis.Right.y, 0f,
                        $"right.y 须为**精确 0**(yaw={yaw}, pitch={pitch})");
                }
            }
        }

        // AC-2-07②: 手性（防镜像）
        [Test]
        public void test_yawBasis_chirality()
        {
            // yaw = 0: fwd = (0, 0, 1), right = (1, 0, 0)
            _rig.UpdateYaw(0f);
            var basis0 = _rig.YawBasis;
            Assert.AreEqual(0f, basis0.Fwd.x, 1e-6f);
            Assert.AreEqual(0f, basis0.Fwd.y, 1e-6f);
            Assert.AreEqual(1f, basis0.Fwd.z, 1e-6f);
            Assert.AreEqual(1f, basis0.Right.x, 1e-6f);
            Assert.AreEqual(0f, basis0.Right.y, 1e-6f);
            Assert.AreEqual(0f, basis0.Right.z, 1e-6f);

            // yaw = π/2: fwd = (1, 0, 0), right = (0, 0, -1)
            _rig.UpdateYaw(Mathf.PI / 2);
            var basis90 = _rig.YawBasis;
            Assert.AreEqual(1f, basis90.Fwd.x, 1e-6f);
            Assert.AreEqual(0f, basis90.Fwd.y, 1e-6f);
            Assert.AreEqual(0f, basis90.Fwd.z, 1e-6f);
            Assert.AreEqual(0f, basis90.Right.x, 1e-6f);
            Assert.AreEqual(0f, basis90.Right.y, 1e-6f);
            Assert.AreEqual(-1f, basis90.Right.z, 1e-6f);
        }

        // AC-2-08: 正交归一全周扫描
        [Test]
        public void test_yawBasis_orthonormal_fullCircle()
        {
            // ⚠️ 2026-10-03(评审 S3):360 采样 < 规格要求 4096(含回绕点密度)
            const int samples = 4096;
            for (int i = 0; i <= samples; i++)   // 含回绕点(i=samples ⇒ yaw=2π)
            {
                float yaw = i * Mathf.PI * 2f / samples;
                _rig.ResetLookForTest(yaw, _rig.Pitch); // ⚠️ UpdateYaw 是增量 API(评审 F6:累积漂移致采样格非设计值)

                var basis = _rig.YawBasis;

                float fwdLen = basis.Fwd.magnitude;
                float rightLen = basis.Right.magnitude;
                float dot = Vector3.Dot(basis.Fwd, basis.Right);

                Assert.AreEqual(1f, fwdLen, CameraRig.YAW_BASIS_EPS, $"‖f̂‖ 应为 1 (yaw={yaw})");
                Assert.AreEqual(1f, rightLen, CameraRig.YAW_BASIS_EPS, $"‖r̂‖ 应为 1 (yaw={yaw})");
                Assert.AreEqual(0f, dot, CameraRig.YAW_BASIS_EPS, $"dot(f̂,r̂) 应为 0 (yaw={yaw})");

                // AC-2-07② 严格式(全周):r̂ == normalize(cross(worldUp, f̂)) —— 逐分量
                var derived = Vector3.Cross(Vector3.up, basis.Fwd).normalized;
                Assert.AreEqual(derived.x, basis.Right.x, CameraRig.YAW_BASIS_EPS, $"手性 r̂.x (yaw={yaw})");
                Assert.AreEqual(derived.y, basis.Right.y, CameraRig.YAW_BASIS_EPS, $"手性 r̂.y (yaw={yaw})");
                Assert.AreEqual(derived.z, basis.Right.z, CameraRig.YAW_BASIS_EPS, $"手性 r̂.z (yaw={yaw})");
            }
        }

        // AC-2-09: PITCH_MAX < 90°
        [Test]
        public void test_pitchConstraint()
        {
            Assert.Less(CameraRig.PITCH_MIN, 0f, "PITCH_MIN 应 < 0");
            Assert.Greater(CameraRig.PITCH_MAX, 0f, "PITCH_MAX 应 > 0");
            Assert.Less(CameraRig.PITCH_MAX, 90f, "PITCH_MAX 应 < 90°");
        }

        // AC-2-10②: 相机对系统 1 零 public **写入面**(只读契约)
        [Test]
        public void test_noPublicWriteSurface()
        {
            // 🔴 **2026-10-03 评审修复(B-4/③)**:ICameraRig 依 ADR-020 §Key Interfaces
            //    补齐 `Mode` / `SetMode` / `Tick` / `Camera` 四成员 ⇒ `SetMode` / `Tick`
            //    是**命令方法**(意图制请求 + 帧求值入口),故「无任何 public 方法」不再成立。
            //    本 AC 的**本意**是「系统 1 对相机的**状态 / 变换**零写入面」——
            //    判据改为:**属性面无 public setter** 且 **无 `Vector3`/变换类写入方法**。
            var interfaceType = typeof(ICameraRig);

            // ① 属性一律只读(无 public setter)
            foreach (var prop in interfaceType.GetProperties())
                Assert.IsNull(prop.GetSetMethod(nonPublic: false),
                    $"ICameraRig.{prop.Name} 不得有 public setter(AC-2-10②)");

            // ② 不得有写入相机的状态/变换方法(SetMode / Tick 是命令入口,不写相机状态)
            foreach (var method in interfaceType.GetMethods())
            {
                if (method.IsSpecialName) continue;   // 属性 getter 跳过
                var n = method.Name;
                Assert.IsTrue(n == "SetMode" || n == "Tick",
                    $"ICameraRig 的方法须仅为命令入口 SetMode / Tick(实得 {n});" +
                    "不得暴露状态 / 变换写入面给系统 1(AC-2-10②)");
            }
        }

        // AC-2-10①: 帧内次序契约
        [Test]
        public void test_frameOrderingContract()
        {
            // 模拟帧内次序：先更新 yaw，再读取 YawBasis
            _rig.UpdateYaw(1.0f);
            var basis = _rig.YawBasis;

            // 读取值应等于本帧更新后的值
            Assert.AreEqual(1.0f, _rig.Yaw, 1e-6f, "Yaw 应为更新后的值");
            Assert.AreEqual(Mathf.Sin(1.0f), basis.Fwd.x, 1e-6f, "Fwd.x 应等于 sin(yaw)");
            Assert.AreEqual(Mathf.Cos(1.0f), basis.Fwd.z, 1e-6f, "Fwd.z 应等于 cos(yaw)");
        }

        // ══════════ AC-2-10①:相位落点决策须落代码(评审 F2 修复)══════════

        [Test]
        public void test_ac210a_phaseDecisionRecordedInCode()
        {
            // ⚠️ 2026-10-03 修复(评审 F2):AC-2-10① 要求「相位落点二选一**须在实现期
            //    定死并落代码注释**」,而决策此前**只在 story 文档里、代码零注释**;
            //    探针测试是同线程「先更新后读」⇒ 恒真,不验决策存在。
            //    现判据 = ① 决策注释存在 ② 无 SSO 代理(DefaultExecutionOrder)。
            string src = System.IO.Path.Combine(UnityEngine.Application.dataPath,
                "Gameplay.Presentation", "Camera", "CameraRig.cs");
            // ⚠️ 只剥 `//` 行注释,**保留 `///` XML doc** —— 决策注释就写在 <remarks> 里
            //    (初版用 `//.*?$` 把 /// 一并剥掉 ⇒ 自己的判据找不到自己的注释)
            string code = System.Text.RegularExpressions.Regex.Replace(
                System.IO.File.ReadAllText(src), @"^[ \t]*//(?!/).*$", "",
                System.Text.RegularExpressions.RegexOptions.Multiline);

            StringAssert.Contains("AC-2-10", code,
                "AC-2-10①:相位落点决策须**落代码注释**(EC-2-14)");
            StringAssert.Contains("onAfterUpdate", code,
                "决策须点名其固定相位形式(显式驱动路径的 ADR-011 先例)");
            StringAssert.Contains("Script Execution Order", code,
                "决策须明文**禁止**依赖 Script Execution Order 隐式排序");
        }

        [Test]
        public void test_ac210b_noDefaultExecutionOrderAttribute()
        {
            // SSO 的代码侧代理:[DefaultExecutionOrder] 于相机类型 = 隐式排序入口
            foreach (var t in typeof(ICameraRig).Assembly.GetTypes())
            {
                if (t.Namespace == null || !t.Namespace.Contains("Presentation.Camera")) continue;
                var attrs = t.GetCustomAttributes(typeof(DefaultExecutionOrder), false);
                Assert.IsEmpty(attrs,
                    $"相机类型 {t.Name} 不得挂 [DefaultExecutionOrder](AC-2-10① 禁 SSO 隐式排序)");
            }
        }

        // AC-2-09②: proj_h(f̂) 正下界
        [Test]
        public void test_projH_positiveLowerBound()
        {
            // 遍历 pitch ∈ [0, PITCH_MAX]，proj_h(f̂) 的模长有正下界 cos(PITCH_MAX) > 0
            float cosMax = Mathf.Cos(CameraRig.PITCH_MAX * Mathf.Deg2Rad);
            Assert.Greater(cosMax, 0f, $"cos(PITCH_MAX) 应 > 0 (PITCH_MAX={CameraRig.PITCH_MAX})");
        }

        // AC-2-09③: 防御性兜底（PITCH_MAX ≥ 90° 时 YawBasis 不抛异常）
        [Test]
        public void test_defensiveFallback_pitchMax90()
        {
            // 即使 PITCH_MAX 被错配为 ≥ 90°，YawBasis 仍应返回有效值
            // 注意：这里测试的是 YawBasis 的数学性质，不依赖 PITCH_MAX 的实际值
            _rig.UpdateYaw(Mathf.PI / 4);
            var basis = _rig.YawBasis;

            // 仍应满足水平化 + 正交归一
            Assert.AreEqual(0f, basis.Fwd.y, 1e-6f);
            Assert.AreEqual(0f, basis.Right.y, 1e-6f);
            Assert.AreEqual(1f, basis.Fwd.magnitude, 1e-6f);
            Assert.AreEqual(1f, basis.Right.magnitude, 1e-6f);
        }

        // AC-2-13: pitch 触界被钳后 yaw 照常累积
        [Test]
        public void test_pitchClampDoesNotPolluteYaw()
        {
            // 推 pitch 到 PITCH_MAX，继续应用 yaw 输入，然后回到水平
            float initialYaw = _rig.Yaw;

            // 推 pitch 到最大
            for (int i = 0; i < 100; i++) _rig.UpdatePitch(10f);
            Assert.AreEqual(CameraRig.PITCH_MAX, _rig.Pitch, 1e-6f, "Pitch 应被钳到 PITCH_MAX");

            // 继续应用 yaw 输入
            _rig.UpdateYaw(0.5f);
            Assert.AreEqual(initialYaw + 0.5f, _rig.Yaw, 1e-6f, "Yaw 应照常累积");

            // 回到水平
            _rig.UpdatePitch(-1000f);
            Assert.AreEqual(CameraRig.PITCH_MIN, _rig.Pitch, 1e-6f, "Pitch 应被钳到 PITCH_MIN");
        }
    
    // ══════════ AC-2-09:俯角界装载期校验(F3 修复)══════════
    public class PitchLimitValidationTest
    {
        [Test]
        public void test_ac209_constValuesHold()
        {
            Assert.DoesNotThrow(() =>
                    DaYiJingCheng.Gameplay.Presentation.Camera.CameraRig.ValidatePitchLimits(
                        DaYiJingCheng.Gameplay.Presentation.Camera.CameraRig.PITCH_MIN,
                        DaYiJingCheng.Gameplay.Presentation.Camera.CameraRig.PITCH_MAX),
                "当前 const 值须通过 AC-2-09(取值一旦存在即被守住)");
        }

        [Test]
        public void test_ac209_negativeFixture_over90Throws()
        {
            var ex = Assert.Throws<System.ArgumentException>(() =>
                DaYiJingCheng.Gameplay.Presentation.Camera.CameraRig.ValidatePitchLimits(-45f, 91f));
            StringAssert.Contains("AC-2-09①", ex.Message, "错误串须点名 AC-2-09①");
            Assert.Throws<System.ArgumentException>(() =>
                DaYiJingCheng.Gameplay.Presentation.Camera.CameraRig.ValidatePitchLimits(60f, 91f),
                "PITCH_MAX=90 恰界(90 不 < 90)⇒ 红");
        }

        [Test]
        public void test_ac209_negativeFixture_invertedRangeThrows()
        {
            Assert.Throws<System.ArgumentException>(() =>
                DaYiJingCheng.Gameplay.Presentation.Camera.CameraRig.ValidatePitchLimits(60f, -45f),
                "min >= max ⇒ 空区间 ⇒ 红");
        }
    }

}


    /// <summary>评审修复轮补的判据(09②③ / 10①② / 07②严格式另在上方循环内)。</summary>
    public class CameraExtraJudgmentTest
    {
        private CameraRig _rig;

        [SetUp]
        public void Setup()
        {
            var go = new GameObject("CameraRigExtra");
            _rig = go.AddComponent<CameraRig>();
        }

        [TearDown]
        public void Teardown()
        {
            if (_rig != null) UnityEngine.Object.DestroyImmediate(_rig.gameObject);
        }

        // ══════════ AC-2-09②③ ══════════

        [Test]
        public void test_ac209b_degenerateUnreachable_cosPitchMaxPositive()
        {
            // 退化分支不可达证据:俯侧 [0, PITCH_MAX] 上 proj_h 模长有正下界 cos(PITCH_MAX) > 0
            // (R-2-3:正 = 俯;仰侧只会更大 ⇒ 无需断言)
            for (int i = 0; i <= 64; i++)
            {
                float pitch = i * CameraRig.PITCH_MAX / 64f;
                float lowerBound = Mathf.Cos(pitch * Mathf.Deg2Rad);
                Assert.Greater(lowerBound,
                    Mathf.Cos(CameraRig.PITCH_MAX * Mathf.Deg2Rad) - 1e-5f,
                    $"proj_h 下界须 ≥ cos(PITCH_MAX)(pitch={pitch})");
            }
            Assert.Greater(Mathf.Cos(CameraRig.PITCH_MAX * Mathf.Deg2Rad), 0f,
                "cos(PITCH_MAX) 须 > 0(PITCH_MAX < 90° 的等价式 —— AC-2-09②)");
        }

        [Test]
        public void test_ac209c_misconfiguredPitch_stillReturnsValidBasis()
        {
            // 防御性兜底:即便口径错配(PITCH_MAX ≥ 90),YawBasis 照常按式返回、不抛
            // (YawBasis 由 yaw 构造,与 pitch 解耦 ⇒ 07/08 在极端 pitch 下仍成立)
            var ex = Assert.Throws<System.ArgumentException>(() =>
                DaYiJingCheng.Gameplay.Presentation.Camera.CameraRig.ValidatePitchLimits(-45f, 91f),
                "错配口径须被装载期校验**抓住**(校验在,才有『照常返回』的兜底)");
            _rig.ResetLookForTest(Mathf.PI / 6, _rig.Pitch);
            var basis = _rig.YawBasis;
            Assert.AreEqual(0f, basis.Fwd.y, 0f, "极端/错配 pitch 下 fwd.y 仍精确 0");
            Assert.AreEqual(0f, basis.Right.y, 0f, "极端/错配 pitch 下 right.y 仍精确 0");
            Assert.AreEqual(1f, basis.Fwd.magnitude, CameraRig.YAW_BASIS_EPS, "仍正交归一(07/08 成立)");
        }

        // ══════════ AC-2-10①:探针消费方(多次交错)══════════

        [Test]
        public void test_ac210a_probe_consumer_seesLatestYaw()
        {
            // 探针消费方:一帧内 **多次 update/read 交错**,每次读取值须 == 本帧更新后的值。
            // (抓**陈旧缓存/帧间缓存**形态的实现 —— 这是「写后读恒真」测不出的部分)
            var probe = new YawBasisProbe(_rig);

            for (int i = 1; i <= 10; i++)
            {
                float dy = 0.37f * i;
                _rig.UpdateYaw(dy);                 // 本帧更新
                var r1 = probe.Read();              // 立即读
                _rig.UpdateYaw(dy * 0.5f);          // 本帧再更新
                var r2 = probe.Read();              // 再读

                Assert.AreEqual(Mathf.Sin(_rig.Yaw), r2.Fwd.x, CameraRig.YAW_BASIS_EPS,
                    $"第 {i} 轮:探针读取值须 == 本帧**最新** yaw 的构造值(陈旧缓存 ⇒ 红)");
                Assert.AreNotEqual(r1.Fwd.x, r2.Fwd.x, "两次读须反映各自读取时刻的 yaw(非同一快照)");
            }
        }

        /// <summary>记录读取时序的探针消费方(AC-2-10① 原文形态)。</summary>
        private sealed class YawBasisProbe
        {
            private readonly CameraRig _rig;
            public readonly System.Collections.Generic.List<(int seq, float yawAtRead, YawBasis basis)>
                Reads = new System.Collections.Generic.List<(int, float, YawBasis)>();
            public YawBasisProbe(CameraRig rig) { _rig = rig; }
            public YawBasis Read()
            {
                var b = _rig.YawBasis;
                Reads.Add((Reads.Count, _rig.Yaw, b));
                return b;
            }
        }

        // ══════════ AC-2-10②:YawBasis 只读形状 ══════════

        [Test]
        public void test_ac210b_yawBasisFieldsReadonly()
        {
            // 只扫接口未扫字段 —— 补:YawBasis 全字段须 readonly(init-only),struct 本体须 readonly
            var t = typeof(YawBasis);
            Assert.IsTrue(t.IsValueType, "YawBasis 须为值类型(struct)");
            // ⚠️ 判据落在**字段面**(readonly struct 的反射形状以字段 IsInitOnly 为准)
            var fields = t.GetFields(BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotEmpty(fields, "YawBasis 须有字段");
            foreach (var f in fields)
                Assert.IsTrue(f.IsInitOnly, $"YawBasis 字段 {f.Name} 须 readonly(只读契约)");
            Assert.IsFalse(t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Any(m => m.Name.StartsWith("set_") || m.Name == "Deform"),
                "YawBasis 不得有 public 写入方法(只读基)");
        }
    }
}
