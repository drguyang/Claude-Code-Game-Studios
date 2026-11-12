// 出生点的**两处常量同值 + 落点可走**夹据(P1-b2 补)。
//
// 权威来源:
//   `design/levels/chapter-1-tent-clinic.md` §7(出生 / 相机锚点 = A2 教堂前庭)
//   `CompositionRoot.WorldSpawnCell`(装配层:respawn 兜底 / RespawnCommandIssuer)
//   `PlayerController.SpawnPosition`(表现层:坠落重置目标)
//
// ⚠️ **为什么需要它**:两处散在**两个程序集**(`Gameplay.Boot` / `Gameplay.Presentation`),
//    各自是 `private static readonly`,编译期没有任何东西保证它们同值。换绑时若要改,
//    漏改一处就得到「坠落重置把玩家丢进不可走区」—— 而症状是玩家在远处坠落,离病根极远。
// ⚠️ 本件用**反射**读私有字段(与 `adr041_save_failure_log_test` 同款做法):
//    目的是钉住**当下这两处字面量**,不是给它们开公开访问器。

using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.Presentation;
// ⚠️ 用全名而**不** `using ...Presentation.Player`:测试树里存在
//    `DaYiJingCheng.Tests.PlayerController` 命名空间,短名会先解析成命名空间(CS0118)。
using PresentationPlayerController =
    DaYiJingCheng.Gameplay.Presentation.Player.PlayerController;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;

namespace DaYiJingCheng.Tests.LevelTool
{
    /// <summary>出生点:两处常量同值,且该格在真产物里可走。</summary>
    [TestFixture]
    public class player_spawn_wiring_test
    {
        private const BindingFlags PrivateStatic =
            BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly;

        /// <summary>
        /// 装配层与表现层的出生点**逐分量相等**,且该格的**地面层**(y = 0)在几何产物里可走。
        /// <para>⚠️ 判别力:改任一处 ⇒ 第一条红;把出生点挪到不可走格(如换绑前的 `(0,0,0)`)
        /// ⇒ 第二条红。这两条都是换绑时真实踩过的形态。</para>
        /// </summary>
        [Test]
        public void test_spawnCell_assemblyAndPresentationAgree_andLandingIsWalkable()
        {
            // ── 两处常量(反射读;任一处消失 ⇒ 本测红,不允许「悄悄删掉」)──
            FieldInfo worldField = typeof(CompositionRoot).GetField("WorldSpawnCell", PrivateStatic);
            Assert.IsNotNull(worldField,
                "`CompositionRoot.WorldSpawnCell` 不存在 —— 装配层的 respawn 兜底被删了?");
            var assemblyCell = (WorldPos)worldField.GetValue(null);

            FieldInfo playerField = typeof(PresentationPlayerController).GetField("SpawnPosition", PrivateStatic);
            Assert.IsNotNull(playerField,
                "`PlayerController.SpawnPosition` 不存在 —— 坠落重置的目标点被删了?");
            var presentationCell = (Vector3)playerField.GetValue(null);

            Assert.AreEqual(assemblyCell.X, (int)presentationCell.x, "出生格 X 两处必须同值");
            Assert.AreEqual(assemblyCell.Y, (int)presentationCell.y, "出生格 Y 两处必须同值");
            Assert.AreEqual(assemblyCell.Z, (int)presentationCell.z, "出生格 Z 两处必须同值");

            // ── 落点可走(读真产物;⚠️ 用地面层 y = 0 查,`WorldPos.Y = 1` 是「站在地面上」的写法)──
            WorldGeometryDataSet geo = DataCookedDisk.LoadWorldGeometry();
            Assert.That(assemblyCell.X, Is.InRange(0, geo.SizeX - 1), "出生格 X 越界");
            Assert.That(assemblyCell.Z, Is.InRange(0, geo.SizeZ - 1), "出生格 Z 越界");

            int index = (assemblyCell.X * geo.SizeY + 0) * geo.SizeZ + assemblyCell.Z;
            Assert.AreEqual(1, geo.Walkable[index],
                $"出生格 ({assemblyCell.X},0,{assemblyCell.Z}) 在几何产物里**不可走** —— " +
                "玩家一出生就掉出世界 / 或撞上阻挡");
        }
    }
}
