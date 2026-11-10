// 地面覆盖门夹具(`TD-building-06` 的结构性解除 · T22 批四 · 2026-11-11)
//
// 权威:`TD-building-06`(P0 地面 10×10 vs 世界格 16×16 ⇒ 玩家走出去即自由落体)
//       · ADR-015 §三(世界格 = 16×1×16,1 格 = 1 米;`LATTICE_SIZE` 以整数毫米表达)
//       · 门本体 `unity/Assets/Editor.Tools.Gates/GroundCoverageGate.cs`
//
// ── 本文件要证明的六件事 ─────────────────────────────────────────────────────
//   ① **真场景 + 真产物**绿(判据面没有把正确的东西判红);
//   ② **重演原缺陷**红:内置 Plane、`pos (0,0,0)`、`scale (1,1,1)` —— 就是 2026-11-06 之前那份场景;
//   ③ **与世界尺寸联动**红:同一份场景、把世界放大 ⇒ 红(否则「改了世界尺寸」这条复发路径没被挡);
//   ④ 三条 fail-closed 前置各自红:无碰撞体 / 被旋转 / 未登记网格;
//   ⑤ 扫描面丢失**不静默**(找不到 `Ground` ⇒ 红,不是「无需判」);
//   ⑥ Cube 的 `TopOffsetY` 被真的算进去(不是把 Plane 的算式套到所有网格上)。
//
// ── 读哪些仓库文件(测试标准 §只读外部夹具的具名声明义务)──────────────────
// ① `unity/Assets/Scenes/World.unity` —— 被测对象本身就是这份**作者态场景**;
//    用合成件替代它,等于只测夹具自己写的 YAML(假绿形态 F1)。
// ② `unity/Assets/DataCooked/world_geometry.cooked.bytes` —— 世界尺寸的**唯一真源**
//    (经 `DataCookedDisk.LoadWorldGeometry`,与运行期同一 codec)。尺寸若内联成字面量,
//    门就再也抓不到「世界改了、地面没改」这件真实的事。
// ⚠️ 本文件**零写盘**;②–⑥ 用**合成场景文本**直调判据本体 `Check(...)`,不碰磁盘。

using System.Collections.Generic;
using DaYiJingCheng.EditorTools.Gates;
using NUnit.Framework;
using UnityEngine;

namespace DaYiJingCheng.Tests.ModularBuilding
{
    /// <summary>地面覆盖门 —— 真场景快照 + 五路合成反例。</summary>
    [TestFixture]
    public sealed class ground_coverage_gate_test
    {
        // ══════════════════════════════════════════════════════════════════════
        //  合成场景件(与 `World.unity` 的块头 / 字段名同形)
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>造一份只有一个 `Ground` 的最小场景文本。</summary>
        private static string Scene(float px, float py, float pz,
                                    float sx, float sy, float sz,
                                    float qx = 0f, float qy = 0f, float qz = 0f, float qw = 1f,
                                    long meshFileId = 10209,
                                    bool withCollider = true,
                                    bool colliderEnabled = true,
                                    bool colliderIsTrigger = false)
        {
            string Collider = withCollider
                ? "--- !u!64 &4000000005\n" +
                  "MeshCollider:\n" +
                  "  m_GameObject: {fileID: 4000000001}\n" +
                  "  m_IsTrigger: " + (colliderIsTrigger ? 1 : 0) + "\n" +
                  "  m_Enabled: " + (colliderEnabled ? 1 : 0) + "\n" +
                  "  m_Mesh: {fileID: " + meshFileId + ", guid: " + GroundCoverageGate.BuiltinGuid + ", type: 0}\n"
                : "";

            return
                "--- !u!1 &4000000001\n" +
                "GameObject:\n" +
                "  m_Component:\n" +
                "  - component: {fileID: 4000000002}\n" +
                "  m_Name: " + GroundCoverageGate.GroundObjectName + "\n" +
                "  m_IsActive: 1\n" +
                "--- !u!4 &4000000002\n" +
                "Transform:\n" +
                "  m_GameObject: {fileID: 4000000001}\n" +
                "  m_LocalRotation: {x: " + Fmt(qx) + ", y: " + Fmt(qy) + ", z: " + Fmt(qz) + ", w: " + Fmt(qw) + "}\n" +
                "  m_LocalPosition: {x: " + Fmt(px) + ", y: " + Fmt(py) + ", z: " + Fmt(pz) + "}\n" +
                "  m_LocalScale: {x: " + Fmt(sx) + ", y: " + Fmt(sy) + ", z: " + Fmt(sz) + "}\n" +
                "--- !u!33 &4000000003\n" +
                "MeshFilter:\n" +
                "  m_GameObject: {fileID: 4000000001}\n" +
                "  m_Mesh: {fileID: " + meshFileId + ", guid: " + GroundCoverageGate.BuiltinGuid + ", type: 0}\n" +
                Collider +
                "--- !u!23 &4000000004\n" +
                "MeshRenderer:\n" +
                "  m_GameObject: {fileID: 4000000001}\n" +
                "  m_Enabled: 1\n";
        }

        private static string Fmt(float v)
            => v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

        private static List<string> Check(string scene, int sx = 16, int sz = 16, int latticeMm = 1000)
            => GroundCoverageGate.Check(scene, sx, sz, latticeMm);

        /// <summary>断言红行里出现某段文字(直接 `Assert.That(has)` 会丢掉报文,故带消息)。</summary>
        private static void AssertRed(List<string> errs, string mustContain)
        {
            Assert.That(errs, Is.Not.Empty, "这一形态必须转红,但门报了通过");
            Assert.That(string.Join("\n", errs), Does.Contain(mustContain),
                "红行里没有预期字样 —— 可能红错了地方。实际:\n" + string.Join("\n", errs));
        }

        // ══════════════════════════════════════════════════════════════════════
        //  ① 真场景 + 真产物
        // ══════════════════════════════════════════════════════════════════════
        [Test]
        public void test_realScene_coversWorld_zeroErrors()
        {
            string root = System.IO.Path.GetFullPath(
                System.IO.Path.Combine(Application.dataPath, ".."));

            List<string> errs = GroundCoverageGate.RunAll(root);

            Assert.That(errs, Is.Empty,
                "真场景被判红 —— 要么地面真的盖不住世界,要么判据算错了。红行:\n" +
                string.Join("\n", errs));
        }

        // ══════════════════════════════════════════════════════════════════════
        //  ② 重演原缺陷(2026-11-06 之前的那份 Ground)
        // ══════════════════════════════════════════════════════════════════════
        [Test]
        public void test_originalDefect_planeAtOrigin_scaleOne_red()
        {
            // 内置 Plane(10×10)· pos (0,0,0) · scale (1,1,1) —— 地板只覆盖 x,z ∈ [-5,5]
            List<string> errs = Check(Scene(0f, 0f, 0f, 1f, 1f, 1f));

            AssertRed(errs, "地面覆盖不足");
        }

        // ══════════════════════════════════════════════════════════════════════
        //  ③ 与世界尺寸联动(同一个场景,世界变大 ⇒ 红)
        // ══════════════════════════════════════════════════════════════════════
        [Test]
        public void test_worldGrows_groundUnchanged_red()
        {
            // 生产现状那份地面(Plane 10 × scale 1.8 = 18×18、中心 7.5 ⇒ [-1.5,16.5])
            // 在 16×16 世界下够,在 32×32 世界下不够 ——
            // 这一条钉的正是「下次改世界尺寸会不会红」这条复发路径。
            string scene = Scene(7.5f, 0f, 7.5f, 1.8f, 1f, 1.8f);

            Assert.That(Check(scene, 16, 16), Is.Empty, "前置:16×16 世界下生产那份地面应当够");

            AssertRed(Check(scene, 32, 32), "地面覆盖不足");

            // 判据的边界是**闭**的(含 `Eps` 容差):恰好的 32×32 地面覆盖 32×32 世界 ⇒ 绿。
            // 钉住它是因为第一版把这里写成「应当红」—— 差分在于「够不够」与「宽不宽裕」是两件事。
            Assert.That(Check(Scene(15.5f, 0f, 15.5f, 3.2f, 1f, 3.2f), 32, 32), Is.Empty,
                "恰好的覆盖应当通过(闭区间 + 容差)");
        }

        // ══════════════════════════════════════════════════════════════════════
        //  ④ 三条 fail-closed 前置
        // ══════════════════════════════════════════════════════════════════════
        [Test]
        public void test_noMeshCollider_red_rendersButCannotWalk()
        {
            AssertRed(Check(Scene(7.5f, 0f, 7.5f, 1.8f, 1f, 1.8f, withCollider: false)),
                      "MeshCollider");
        }

        [Test]
        public void test_rotatedGround_red_failClosed()
        {
            // 绕 x 轴转 90°(q = (√2/2, 0, 0, √2/2))—— 平面立起来 = 不是可走平面
            AssertRed(Check(Scene(7.5f, 0f, 7.5f, 1.8f, 1f, 1.8f, qx: 0.70710678f, qw: 0.70710678f)),
                      "被旋转过");
        }

        [Test]
        public void test_unknownMesh_failClosed()
        {
            AssertRed(Check(Scene(7.5f, 0f, 7.5f, 1.8f, 1f, 1.8f, meshFileId: 12345)),
                      "不在本门的内置表里");
        }

        // ══════════════════════════════════════════════════════════════════════
        //  ⑤ 扫描面丢失不静默
        // ══════════════════════════════════════════════════════════════════════
        [Test]
        public void test_missingGroundObject_red_notSilentPass()
        {
            const string noGround =
                "--- !u!1 &1\nGameObject:\n  m_Name: Directional Light\n--- !u!4 &2\nTransform:\n" +
                "  m_GameObject: {fileID: 1}\n  m_LocalPosition: {x: 0, y: 3, z: 0}\n";

            AssertRed(Check(noGround), "找不到对象");
        }

        // ══════════════════════════════════════════════════════════════════════
        //  ⑥ Cube 的顶面偏移被真的算进去
        // ══════════════════════════════════════════════════════════════════════
        [Test]
        public void test_cubeGround_topOffsetAccounted()
        {
            // Cube(1×1×1)在 y=-0.5 处、scale 20 ⇒ 顶面恰在 y = -0.5 + 0.5×1 = 0,
            // 覆盖 x,z ∈ [-0.5+... ]:中心 7.5、半宽 10 ⇒ [-2.5, 17.5] ⊇ 需求 [-0.5, 15.5]。
            string ok = Scene(7.5f, -0.5f, 7.5f, 20f, 1f, 20f, meshFileId: 10202);
            Assert.That(Check(ok), Is.Empty, "Cube 顶面与世界面齐平 ⇒ 应当通过(前置)");

            // 同一份地面整体抬高 1 米 ⇒ 顶面在 y = 1 ⇒ 红(悬空)。
            AssertRed(Check(Scene(7.5f, 0.5f, 7.5f, 20f, 1f, 20f, meshFileId: 10202)),
                      "地面顶面在 y=");
        }
    }
}
