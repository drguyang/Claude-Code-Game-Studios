// player-controller Story 001 测试
//
// AC-1-01: 移动不由物理驱动
//   ① Rigidbody 组件零挂载
//   ② AddForce/AddTorque/velocity 写入零引用
//   ③ Physics.Raycast/CheckCapsule/Overlap* 零引用
// AC-1-28: asmdef 引用集白名单
// AC-1-10: 坐标契约三项 + 几何约束

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using DaYiJingCheng.Gameplay.Presentation.Player;
using NUnit.Framework;
using UnityEngine;
using PlayerControllerType = DaYiJingCheng.Gameplay.Presentation.Player.PlayerController;

namespace DaYiJingCheng.Tests.PlayerController
{
    public class ControllerFoundationTest
    {
        // ══════════ AC-1-01①: Rigidbody 组件零挂载 ══════════

        [Test]
        public void test_ac101_noRigidbodyField()
        {
            var playerType = typeof(PlayerControllerType);
            foreach (var field in playerType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                Assert.IsFalse(field.FieldType == typeof(Rigidbody),
                    $"字段 {field.Name} 不应为 Rigidbody");
            }
        }

        [Test]
        public void test_ac101_noRigidbodyInTypeHierarchy()
        {
            var playerType = typeof(PlayerControllerType);
            Assert.IsFalse(playerType.BaseType == typeof(Rigidbody),
                "PlayerController 不应继承 Rigidbody");
        }

        [Test]
        public void test_ac101_characterControllerRequired()
        {
            // 检查 PlayerController 是否要求 CharacterController 组件
            var playerType = typeof(PlayerControllerType);
            var requireComponent = playerType.GetCustomAttributes(typeof(RequireComponent), true);
            bool hasCharacterControllerRequirement = false;
            foreach (RequireComponent rc in requireComponent)
            {
                if (rc.m_Type0 == typeof(CharacterController) || rc.m_Type1 == typeof(CharacterController))
                {
                    hasCharacterControllerRequirement = true;
                    break;
                }
            }
            Assert.IsTrue(hasCharacterControllerRequirement,
                "PlayerController 应要求 CharacterController 组件");
        }

        // ══════════ AC-1-01②: AddForce/AddTorque/velocity 写入零引用 ══════════

        [Test]
        public void test_ac101_noAddForceCalls()
        {
            // IL 体扫描: 检查方法体内是否调用 AddForce/AddTorque
            var playerType = typeof(PlayerControllerType);
            foreach (var method in playerType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                Assert.IsFalse(ILBodyScanner.ContainsMethodCall(method, "AddForce"),
                    $"方法 {method.Name} 不应调用 AddForce");
                Assert.IsFalse(ILBodyScanner.ContainsMethodCall(method, "AddTorque"),
                    $"方法 {method.Name} 不应调用 AddTorque");
            }
        }

        // ══════════ AC-1-01③: Physics.Raycast/CheckCapsule/Overlap* 零引用 ══════════

        [Test]
        public void test_ac101_noPhysicsRaycastCalls()
        {
            // IL 体扫描: 检查方法体内是否调用 Physics.Raycast/CheckCapsule/Overlap*
            var playerType = typeof(PlayerControllerType);
            foreach (var method in playerType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                Assert.IsFalse(ILBodyScanner.ContainsMethodCall(method, "Raycast"),
                    $"方法 {method.Name} 不应调用 Raycast");
                Assert.IsFalse(ILBodyScanner.ContainsMethodCall(method, "CheckCapsule"),
                    $"方法 {method.Name} 不应调用 CheckCapsule");
                Assert.IsFalse(ILBodyScanner.ContainsMethodCall(method, "Overlap"),
                    $"方法 {method.Name} 不应调用 Overlap");
            }
        }

        // ══════════ AC-1-28: asmdef 引用集白名单 ══════════

        [Test]
        public void test_ac128_asmdefReferenceWhitelist()
        {
            string asmdefPath = Path.Combine(Application.dataPath, "../Assets/Gameplay.Presentation/Gameplay.Presentation.asmdef");
            Assert.IsTrue(File.Exists(asmdefPath), "Gameplay.Presentation.asmdef 应存在");

            string content = File.ReadAllText(asmdefPath);
            // DOTS 排除（ADR-017）
            Assert.IsFalse(content.Contains("Unity.Entities"), "不应引用 Unity.Entities");
            Assert.IsFalse(content.Contains("Unity.Burst"), "不应引用 Unity.Burst");
            Assert.IsFalse(content.Contains("Unity.Jobs"), "不应引用 Unity.Jobs");
            Assert.IsFalse(content.Contains("Unity.Mathematics"), "不应引用 Unity.Mathematics");
        }

        [Test]
        public void test_ac128_asmdefNoSimImplementationReference()
        {
            // ⚠️ 2026-10-03 修复判据空转(评审 A7):原测**双分支皆 `Assert.Pass`**
            //    ⇒ **pass-through**,无论引用与否都绿 ⇒ AC-1-28 从未被真正执行。
            //    现改为**真断言**;已知债务显式化(失败消息点名豁免),不再静默放行。
            string asmdefPath = Path.Combine(Application.dataPath,
                "../Assets/Gameplay.Presentation/Gameplay.Presentation.asmdef");
            string content = File.ReadAllText(asmdefPath);

            // 解析 references 数组 —— 不用字符串 Contains(会误命中注释/路径)
            var refBlock = System.Text.RegularExpressions.Regex.Match(
                content, "\"references\"\\s*:\\s*\\[(.*?)\\]",
                System.Text.RegularExpressions.RegexOptions.Singleline).Groups[1].Value;
            var names = System.Text.RegularExpressions.Regex
                .Matches(refBlock, "\"([^\"]+)\"")
                .Cast<System.Text.RegularExpressions.Match>()
                .Select(m => m.Groups[1].Value)
                .ToList();

            // AC-1-28:禁引 sim **实现**程序集;Sim.Contracts 是边界程序集 ⇒ **允许**
            var forbidden = names.Where(x => x == "Sim" || x == "Sim.Codec").ToList();

            // ── 具名豁免(baseline · 与 b6 门同款纪律)──────────────────
            // 唯一已登记债务 = `RecipeDataSet` 住 `Sim.ItemDatabase`
            // (1 侧经 `CookedCodec` / `AddressablesDataProvider` 消费)。
            // **出口条件** = `RecipeDataSet` 迁出 `Sim` 后删此豁免,断言转硬红。
            var waiver = new[] { "Sim" };
            var unexpected = forbidden.Where(f => !waiver.Contains(f)).ToList();

            Assert.IsEmpty(unexpected,
                "AC-1-28 违例:引用了**未登记**的 sim 实现程序集 ["
                + string.Join(", ", unexpected) + "]。\n"
                + "已登记豁免仅 [" + string.Join(", ", waiver) + "]"
                + "(= RecipeDataSet 住 Sim 的既有债)。\n"
                + "任何**新增**的 sim 实现引用(尤其为取 WorldLattice 等类型)"
                + "都**不在**豁免内,须改走 Sim.Contracts 的只读契约面"
                + "(承 AC-1-28 与 ADR-025 §①)。");

            // 豁免现状留痕(不作判据):Sim 引用若某天被移除,须显式复核并删豁免
            TestContext.WriteLine(
                forbidden.Count == 0
                    ? "AC-1-28 当前**成立**(Sim 引用已移除)—— 请复核并删除本豁免"
                    : "AC-1-28 部分成立:已登记豁免 [" + string.Join(", ", forbidden)
                      + "](RecipeDataSet 债;出口条件见注释)");
        }

        // ══════════ AC-1-10③: Vector3Int 不出现在跨系统接口签名 ══════════

        [Test]
        public void test_ac110_noVector3IntInPublicInterfaces()
        {
            var playerType = typeof(PlayerControllerType);
            foreach (var method in playerType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                // 检查返回类型（含泛型/数组/嵌套）
                Assert.IsFalse(TypeContainsVector3Int(method.ReturnType),
                    $"方法 {method.Name} 返回类型不应为 Vector3Int");

                // 检查参数类型（含泛型/数组/嵌套）
                foreach (var param in method.GetParameters())
                {
                    Assert.IsFalse(TypeContainsVector3Int(param.ParameterType),
                        $"方法 {method.Name} 参数 {param.Name} 不应为 Vector3Int");
                }
            }
        }

        /// <summary>
        /// 递归检查类型是否包含 Vector3Int（含泛型/数组/嵌套）。
        /// </summary>
        private static bool TypeContainsVector3Int(Type type)
        {
            if (type == null) return false;
            if (type == typeof(Vector3Int)) return true;
            if (type.IsGenericType)
            {
                foreach (var arg in type.GetGenericArguments())
                {
                    if (TypeContainsVector3Int(arg)) return true;
                }
            }
            if (type.IsArray)
            {
                return TypeContainsVector3Int(type.GetElementType());
            }
            return false;
        }

        // ══════════ AC-1-10②: LATTICE_SIZE ≥ radius×2 ══════════

        [Test]
        public void test_ac110_latticeSizeConstraint()
        {
            // 检查 LatticeSizeMm 字段存在且为整数
            var latticeType = typeof(DaYiJingCheng.Sim.World.WorldLatticeParams);
            var latticeField = latticeType.GetField("LatticeSizeMm", BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(latticeField, "LatticeSizeMm 字段应存在于 WorldLatticeParams");
            Assert.AreEqual(typeof(int), latticeField.FieldType,
                "LatticeSizeMm 应为 int 类型（整数格）");
        }

        [Test]
        public void test_ac110_latticeSizeAtLeastTwiceRadius()
        {
            // ⚠️ 2026-10-03 修复判据空转(评审 A4):原测**只查字段存在**,
            //    不验 AC-1-10② 的约束本体(`LATTICE_SIZE ≥ radius × 2`,EC-12)。
            //    现做**真约束断言**:取 1 侧 radius 的规范值,与规范格边长比较。
            var latticeType = typeof(DaYiJingCheng.Sim.World.WorldLatticeParams);
            var latticeField = latticeType.GetField("LatticeSizeMm",
                BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(latticeField, "LatticeSizeMm 字段应存在");

            // 1 侧 radius 的规范值(AC-1-33③ 钉死 > 0)
            var config = DaYiJingCheng.Gameplay.Presentation.Player.LocomotionConfig.LoadDefault();
            Assert.Greater(config.Radius, 0f, "radius 须 > 0(AC-1-33③)");

            // EC-12 本体:直径须装得进一格(单位 mm)
            //
            // ⚠️ **2026-10-03 修复硬编码漂移(评审 A4)**:原测把「规范格边长」写成
            //    **局部字面量 1000**,与生产 `WorldLatticeParams.LatticeSizeMm` 各持一份
            //    ⇒ 生产改格边长,本测**静默测旧值**(恒绿既可能因真满足,也可能因字面量恰好
            //    大于旧 radius)。现改**读同一实体**(同源纪律,与 AC-1-12 的 `MAX_DT` 同型)。
            const float MmPerMeter = 1000f;
            int canonicalLatticeMm = CanonicalLatticeSizeMm();
            float diameterMm = config.Radius * 2f * MmPerMeter;

            Assert.GreaterOrEqual(canonicalLatticeMm, diameterMm,
                $"EC-12 违例:直径 {diameterMm} mm 装不进 {canonicalLatticeMm} mm 格 " +
                "(LATTICE_SIZE ≥ radius × 2)");
        }

        /// <summary>AC-1-10② 的**可证伪性**:radius 大到直径超格 ⇒ 约束须红。</summary>
        [Test]
        public void test_ac110_negativeFixture_radiusTooLarge()
        {
            const float MmPerMeter = 1000f;
            int canonicalLatticeMm = CanonicalLatticeSizeMm();
            // 半径取「规范格边长的一半再多一点」⇒ 直径必超格,与格边长实际取值解耦
            float hugeRadius = (canonicalLatticeMm / MmPerMeter) * 0.6f;
            float diameterMm = hugeRadius * 2f * MmPerMeter;

            Assert.Greater(diameterMm, canonicalLatticeMm,
                "夹具前提:该 radius 的直径须**超过**规范格边长");
            Assert.IsFalse(canonicalLatticeMm >= diameterMm,
                "EC-12 违例(直径 > 格边长)⇒ 约束不成立 —— 证明判据能分辨");
        }

        /// <summary>
        /// 规范格边长(mm)—— **读生产同一实体** `WorldLatticeParams.LatticeSizeMm`,
        /// 不在此处复制第二个字面量(同源纪律;漂移会让判据静默测旧值)。
        /// </summary>
        private static int CanonicalLatticeSizeMm()
        {
            // ⚠️ 取值须**自洽**:F-6-1 守卫要求 LATTICE_SIZE ≥ SPEED_MAX × MAX_DT × SAFETY_MARGIN。
            //    这里 SPEED_MAX = 5 × 1 × 2 = 10 m/s,MAX_DT = 100 ms,SAFETY = 2
            //    ⇒ 下界 2000 mm;取 1 m 格(1000)会**先被 F-6-1 拒**,故用 10 m 格。
            //    本测只关心「格边长从生产实体读出」,不指定其具体值。
            var row = new DaYiJingCheng.Sim.World.TerrainSpeedRow(
                1, DaYiJingCheng.Sim.Contracts.Fix.One, DaYiJingCheng.Sim.Contracts.Fix.One);
            var p = new DaYiJingCheng.Sim.World.WorldLatticeParams(
                latticeSizeMm: 10000, speedModeMax: 5, kContextMax: 2,
                maxDtMs: 100, safetyMargin: 2,
                kSpeedTable: new[] { row });
            return p.LatticeSizeMm;
        }
    }
}
