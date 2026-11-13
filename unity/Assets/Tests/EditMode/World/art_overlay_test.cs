// 正式美术件摆放台(`ArtOverlay`)夹具。
//
// 权威:`ArtOverlay` 头注「缺席是**正常态**」那一段。
//
// ⚠️ 本件**不读** `Resources/Art/church-wall/`(27 MB 本地资产,local-only,别人的机器上没有);
//   全部用注入表 + 不存在的路径 ⇒ 夹具与「本机有没有那件美术」解耦。

using DaYiJingCheng.Gameplay.Presentation.World;
using NUnit.Framework;
using UnityEngine;

namespace DaYiJingCheng.Tests.Unit.World
{
    /// <summary>美术摆放台:缺席不抛、不建空根、连建不叠。</summary>
    [TestFixture]
    public class art_overlay_test
    {
        /// <summary>资源不存在 ⇒ 记缺席、返回 0,**不建**空根节点。</summary>
        [Test]
        public void test_missingResource_countsAndBuildsNothing()
        {
            var overlay = new ArtOverlay(new[]
            {
                new ArtPlacement("Art/__does_not_exist__/nope", 0, 0, 0f, 1f, "缺席样例"),
            });

            int placed = overlay.Build();

            Assert.That(placed, Is.EqualTo(0));
            Assert.That(overlay.MissingCount, Is.EqualTo(1),
                "缺席要**可观测** —— 日志与计数是「接进来了」与「本机没有」的唯一区分面");
            Assert.That(GameObject.Find(ArtOverlay.RootName), Is.Null,
                "一件都没摆上时不得留下空根节点(空根在层级里会被误读成「已接入」)");
        }

        /// <summary>覆盖判据:闭区间矩形,含中心格;越界一格不算。
        /// <para>这是让位的唯一判据面 —— 算宽了这一格的墙会消失,算窄了真件被挡。</para></summary>
        [Test]
        public void test_covers_isInclusiveRectangle()
        {
            var p = new ArtPlacement("Art/__none__/x", 9, 29, 0f, 1f, "样例",
                                     coverHalfX: 1, coverHalfZ: 0);

            Assert.That(p.Covers(9, 29), Is.True, "中心格");
            Assert.That(p.Covers(8, 29), Is.True);
            Assert.That(p.Covers(10, 29), Is.True);
            Assert.That(p.Covers(11, 29), Is.False, "宽 2 m ⇒ 只有 ±1 格");
            Assert.That(p.Covers(9, 30), Is.False, "Z 向半径 0 ⇒ 邻行不算");
        }

        /// <summary>**承重墙不得被美术让位** —— 灰盒是这份数据里唯一带 `MeshCollider` 的东西,
        /// 让位 = 把那格墙**删掉**(不是挡住)。
        /// <para>🗝 立此条缘由(2026-11-13 实测):`wall-window` 原摆在 (9,29)、覆盖 x∈[8,10],
        /// 既被内景南面板挡住(看不见),又让灰盒把这三格整块让位 ⇒ 从 (10,27) 按 W(南)
        /// 2.5 s 直接走到 **(9,33)**,**穿墙出屋**。该件已删,这条钉住「别再摆回来」。</para>
        /// <para>⚠️ 东墙 x=15 **不在**断言面内 —— 那一段本来就由 `church-wall` 真件接管。</para></summary>
        [Test]
        public void test_churchEnclosure_isNeverSuppressedByArt()
        {
            for (int x = 5; x <= 14; x++)
            {
                Assert.That(ArtOverlay.IsCoveredByArt(x, 14), Is.False, $"北墙 (x={x}, z=14) 被让位");
                Assert.That(ArtOverlay.IsCoveredByArt(x, 29), Is.False, $"南墙 (x={x}, z=29) 被让位");
            }
            for (int z = 15; z <= 28; z++)
            {
                Assert.That(ArtOverlay.IsCoveredByArt(4, z), Is.False, $"西墙 (x=4, z={z}) 被让位");
            }
        }

        /// <summary>生产表里的每件都必须声明覆盖范围(默认 0/0 = 只覆盖落位那一格)。</summary>
        [Test]
        public void test_productionPlacements_declareCoverage()
        {
            foreach (ArtPlacement p in ArtOverlay.Placements)
                Assert.That(p.Covers(p.CellX, p.CellZ), Is.True,
                    $"{p.Note}:落位格必须被自己覆盖,否则灰盒会在真件脚下留着");
        }

        /// <summary>生产表非空,且每行的路径非空、缩放为正。</summary>
        [Test]
        public void test_productionPlacements_areWellFormed()
        {
            Assert.That(ArtOverlay.Placements, Is.Not.Empty);
            foreach (ArtPlacement p in ArtOverlay.Placements)
            {
                Assert.That(p.ResourcePath, Is.Not.Null.And.Not.Empty);
                Assert.That(p.Scale, Is.GreaterThan(0f));
            }
        }

        /// <summary>世界切换留下的**残留根**必须被清掉(否则重装配会叠出第二套)。
        /// <para>🗝 这里不需要真资产:先手工造一个同名根(模拟上一轮装配的残留),
        /// 再跑一次全缺席的 Build —— 残留根若还在,就是「叠一套」的那条路径。</para></summary>
        [Test]
        public void test_staleRoot_isClearedOnBuild()
        {
            var stale = new GameObject(ArtOverlay.RootName);
            Assert.That(GameObject.Find(ArtOverlay.RootName), Is.Not.Null);

            var overlay = new ArtOverlay(new[]
            {
                new ArtPlacement("Art/__nope__/nope", 0, 0, 0f, 1f, "缺席"),
            });
            overlay.Build();

            Assert.That(GameObject.Find(ArtOverlay.RootName), Is.Null,
                "残留根不清 = 世界切换后两套并存(画面只是「多了点什么」,没有报错)");
            overlay.DestroyAll();
        }
    }
}
