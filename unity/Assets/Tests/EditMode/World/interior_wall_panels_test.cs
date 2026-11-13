// 内景墙面板的**开洞**夹具(2026-11-13 · §8.11 / §8.12)。
//
// 权威:`unity/Assets/Gameplay.Presentation/World/InteriorWallPanels.cs`
//       `InteriorWindows`(窗洞的唯一作者表)
//
// ⚠️ 本件钉的是**洞的形状与位置**(面上一片区域是否还有三角形),不是画面。
//   画面只有真包截图能判(见 `production/qa/evidence/`)。
// ⚠️ 依赖 URP Lit shader(`Shader.Find`);找不到就 `Assert.Ignore` 并写明原因 ——
//   不能因为「shader 不在」而静默全绿(F5 跳过冒充)。

using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Presentation.World;
using NUnit.Framework;
using UnityEngine;

namespace DaYiJingCheng.Tests.Unit.World
{
    /// <summary>墙面板开洞:洞内无三角形、洞外有三角形。</summary>
    [TestFixture]
    public class interior_wall_panels_test
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            if (Shader.Find("Universal Render Pipeline/Lit") == null)
                Assert.Ignore("URP Lit shader 不可用(不需要它也能跑别的夹具,但本件必须真建墙)");

            int created = InteriorWallPanels.Build();
            Assert.That(created, Is.EqualTo(5), "五面墙");

            foreach (string name in new[] { "Wall_West", "Wall_North", "Wall_South",
                                            "Wall_EastSouth", "Wall_EastNorth",
                                            "Wall_WestWindowFrame", "Wall_NorthWindowFrame" })
            {
                GameObject go = GameObject.Find(name);
                if (go != null) _spawned.Add(go);
            }
            GameObject root = GameObject.Find("InteriorWallPanels");
            if (root != null) _spawned.Add(root);
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _spawned.Count; i++)
                if (_spawned[i] != null) Object.DestroyImmediate(_spawned[i]);
            _spawned.Clear();
        }

        /// <summary>该点在墙面上是否被某个三角形覆盖。
        /// <para>⚠️ 投影面**随墙走**:南北墙在 xy 面上(U 沿 x),东西墙在 zy 面上(U 沿 z)。
        /// 首版一律按 xy 投 ⇒ 西墙的「窗台以下仍是墙」判成空(那不是洞,是投错了面)。</para></summary>
        private static bool Covered(string meshName, float u, float v, bool uIsZ)
        {
            GameObject go = GameObject.Find(meshName);
            Assert.That(go, Is.Not.Null, $"{meshName} 未生成");
            Mesh mesh = go.GetComponent<MeshFilter>().sharedMesh;
            Vector3[] verts = mesh.vertices;
            int[] tris = mesh.triangles;

            for (int i = 0; i < tris.Length; i += 3)
            {
                Vector3 a = verts[tris[i]], b = verts[tris[i + 1]], c = verts[tris[i + 2]];
                Vector2 pa = new Vector2(uIsZ ? a.z : a.x, a.y);
                Vector2 pb = new Vector2(uIsZ ? b.z : b.x, b.y);
                Vector2 pc = new Vector2(uIsZ ? c.z : c.x, c.y);
                if (InTriangle(new Vector2(u, v), pa, pb, pc)) return true;
            }
            return false;
        }

        private static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Cross(p - a, b - a);
            float d2 = Cross(p - b, c - b);
            float d3 = Cross(p - c, a - c);
            bool neg = d1 < 0f || d2 < 0f || d3 < 0f;
            bool pos = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(neg && pos);
        }

        private static float Cross(Vector2 u, Vector2 v) => u.x * v.y - u.y * v.x;

        /// <summary>西墙大窗:矩形洞 —— 洞内两点无面,洞外三点有面。</summary>
        [Test]
        public void test_westWindow_rectangular_holeIsOpen()
        {
            InteriorWindows.Opening w = InteriorWindows.West;
            float cx = (w.A0 + w.A1) * 0.5f;
            float cy = (w.Y0 + w.Y1) * 0.5f;

            Assert.That(Covered("Wall_West", cx, cy, uIsZ: true), Is.False, "洞心必须没有面");
            Assert.That(Covered("Wall_West", w.A0 + 0.05f, w.Y1 - 0.05f, uIsZ: true), Is.False, "洞右上角内侧");
            Assert.That(Covered("Wall_West", cx, w.Y0 - 0.10f, uIsZ: true), Is.True, "窗台以下仍是墙");
            Assert.That(Covered("Wall_West", cx, w.Y1 + 0.10f, uIsZ: true), Is.True, "窗顶以上仍是墙");
            Assert.That(Covered("Wall_West", w.A0 - 0.15f, cy, uIsZ: true), Is.True, "洞口左侧仍是墙");
            Assert.That(Covered("Wall_West", w.A1 + 0.15f, cy, uIsZ: true), Is.True, "洞口右侧仍是墙");
        }

        /// <summary>北墙拱窗:半圆拱 —— 拱肩(矩形四角)有面,拱心里无面。</summary>
        [Test]
        public void test_northWindow_arched_holeFollowsTheSemicircle()
        {
            InteriorWindows.Opening w = InteriorWindows.North;
            Assert.That(w.IsArched, Is.True, "北墙必须是有拱的那一扇");

            float cx = (w.A0 + w.A1) * 0.5f;
            float r = w.ArchRadius;

            // 矩形那一段(窗台→起拱):整片都该是空的
            Assert.That(Covered("Wall_North", w.A0 + 0.05f, w.Y0 + 0.05f, uIsZ: false), Is.False, "洞口左下");
            Assert.That(Covered("Wall_North", w.A1 - 0.05f, w.ArchY - 0.05f, uIsZ: false), Is.False, "洞口右上");

            // 拱心:半径 0.5r 处仍是空的
            Assert.That(Covered("Wall_North", cx, w.ArchY + r * 0.5f, uIsZ: false), Is.False, "拱心");

            // 拱肩(洞口的两个上角,半圆之外):必须是墙
            Assert.That(Covered("Wall_North", w.A0 + 0.04f, w.ArchY + r * 0.92f, uIsZ: false), Is.True,
                "左上拱肩 —— 半圆之外,应当是墙(否则拱会漏成方洞)");
            Assert.That(Covered("Wall_North", w.A1 - 0.04f, w.ArchY + r * 0.92f, uIsZ: false), Is.True,
                "右上拱肩");

            // 拱顶以上
            Assert.That(Covered("Wall_North", cx, w.Y1 + 0.10f, uIsZ: false), Is.True, "拱顶以上仍是墙");
            Assert.That(Covered("Wall_North", cx, w.Y0 - 0.10f, uIsZ: false), Is.True, "窗台以下仍是墙");
        }
    }
}
