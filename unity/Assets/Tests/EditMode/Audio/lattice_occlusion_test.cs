// 权威来源:docs/tech-debt-register.md `TD-audio-035` ②(遮挡判定恒 false)
//   · GDD `audio-system.md` F-44.1(隔门 ≠ 静音,是变闷)· `AC-44-D8` ①
//   · ADR-015 §一(两层世界纪律:遮挡输入 = 逻辑层整数格,禁读表现态位置)
//   · ADR-018 §四(遮挡 = 44 本地判定,不进流)
//   · 裁定:2026-10-29(用户裁定 **甲案**:双格签名 + 格级视线走查)
//
// ⚠️ **本夹具钉四件事**:
//   ① 视线走查的**几何语义**:墙在中间 ⇒ 挡;绕开 ⇒ 不挡;端点不可走 ⇒ **不算挡**(端点豁免);
//   ② 走查是**真几何**不是常量 —— 同一对格,换一份无墙几何 ⇒ 结论翻转;
//   ③ 44 侧**收的是双格委托**:`IsOccluded` 把(听者格, 声源格)喂进去(停用 ⇒ 遮挡恒 false);
//   ④ 未接缝时**不计数** —— 「未装配」与「判了没挡」在 `OcclusionEvaluations` 上可分。
//
// ⚠️ 测试纪律:arrange/act/assert · 无随机 · 无时间依赖 · 零文件系统 I/O。

using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.Presentation.Audio;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;
using NUnit.Framework;
using UnityEngine;   // AudioClip.Create / DestroyImmediate(夹具造真 clip,不走 Addressables)

namespace DaYiJingCheng.Tests.Unit.Audio
{
    /// <summary>`TD-audio-035` ②:格级视线走查 + 双格遮挡缝。</summary>
    [TestFixture]
    public sealed class LatticeOcclusionTest
    {
        // ══════════════ ① 走查几何语义 ══════════════

        [Test]
        public void test_wallBetweenBlocks()
        {
            // Arrange:5×1×5,中间一列(x=2)全不可走 = 一堵墙
            WorldGeometry geo = Grid(5, 1, 5, blocked: (x, y, z) => x == 2);

            // Act
            bool blocked = LatticeOcclusionProbe.IsBlocked(geo, new Int3(0, 0, 2), new Int3(4, 0, 2));

            // Assert
            Assert.That(blocked, Is.True,
                "x=0 到 x=4 之间整列 x=2 不可走 ⇒ 必被挡(这是「隔门变闷」的几何输入)");
        }

        [Test]
        public void test_openPathDoesNotBlock()
        {
            // Arrange:同一尺寸,只把墙**留一个缺口**
            WorldGeometry geo = Grid(5, 1, 5, blocked: (x, y, z) => x == 2 && z != 2);

            // Act:沿缺口那一行走
            bool blocked = LatticeOcclusionProbe.IsBlocked(geo, new Int3(0, 0, 2), new Int3(4, 0, 2));

            // Assert
            Assert.That(blocked, Is.False, "(2,0,2) 可走 ⇒ 直线视线不被挡");
        }

        [Test]
        public void test_endpointsAreExempt()
        {
            // 🔁 端点豁免:听者格 / 声源格本身不可走是合法的(站在门框格上 / 声源在门口)。
            //    把端点算进走查 ⇒ 这两类正常情形被误判成遮挡。
            WorldGeometry geo = Grid(5, 1, 5, blocked: (x, y, z) => x == 0 || x == 4);

            // Act:两端都不可走,中间全可走
            bool blocked = LatticeOcclusionProbe.IsBlocked(geo, new Int3(0, 0, 2), new Int3(4, 0, 2));

            // Assert
            Assert.That(blocked, Is.False,
                "两端点不可走但中间畅通 ⇒ 不算挡 —— 端点豁免(声源在门口是正常形态)");
        }

        [Test]
        public void test_sameCellNeverBlocked()
        {
            WorldGeometry geo = Grid(3, 1, 3, blocked: (x, y, z) => true);   // 全不可走

            Assert.That(LatticeOcclusionProbe.IsBlocked(geo, new Int3(1, 0, 1), new Int3(1, 0, 1)),
                Is.False, "零长视线不可能被挡(否则同一格的声音会恒变闷)");
        }

        [Test]
        public void test_outOfBoundsCountsAsBlocked()
        {
            // 越界格按 fail-closed = 不可走(与「未驻留 chunk 视为 block」ADR-014 §五 同源)
            WorldGeometry geo = Grid(3, 1, 3, blocked: (x, y, z) => false);

            bool blocked = LatticeOcclusionProbe.IsBlocked(geo, new Int3(1, 0, 1), new Int3(9, 0, 1));

            Assert.That(blocked, Is.True,
                "视线穿出世界边界 ⇒ 越界格按 fail-closed 判不可走 ⇒ 视为阻挡");
        }

        // ══════════════ ② 真几何、非常量 ══════════════

        [Test]
        public void test_samePairFlipsWithDifferentGeometry()
        {
            // 判别力:同一对格,墙 / 无墙两份几何 ⇒ 结论翻转。
            // 若走查退化成常量(恒 false 或恒 true),本测必红。
            WorldGeometry walled = Grid(5, 1, 5, blocked: (x, y, z) => x == 2);
            WorldGeometry open = Grid(5, 1, 5, blocked: (x, y, z) => false);
            var from = new Int3(0, 0, 2);
            var to = new Int3(4, 0, 2);

            Assert.That(LatticeOcclusionProbe.IsBlocked(walled, from, to), Is.True, "有墙 ⇒ 挡");
            Assert.That(LatticeOcclusionProbe.IsBlocked(open, from, to), Is.False, "无墙 ⇒ 不挡");
        }

        // ══════════════ ③ 44 侧收双格委托 ══════════════

        [Test]
        public void test_renderTarget_feedsBothCellsIntoSeam()
        {
            // Arrange:接缝的渲染目标 + 记录每次被问到的格对
            AudioSourcePool pool = new AudioSourcePool();
            AudioClip clip = AudioClip.Create("occlusion-probe", 64, 1, 44100, false);
            try
            {
                Int3 seenFrom = default, seenTo = default;
                int calls = 0;
                var spatial = new SpatializationDirector(latticeSize: 1f, pool);
                // ⚠️ clip 必须非 null —— `PlayOneShot` 在 clip == null 时**早退**
                // (只记 `ClipMissCount`),遮挡缝根本不会被问到 ⇒ 本测会假绿成「calls = 0 是正常的」。
                var target = new AudioSourceRenderTarget(
                    pool, spatial, 1f, _ => clip, busForCue: null,
                    occludedFor: (from, to) => { seenFrom = from; seenTo = to; calls++; return true; });
                target.SetListenerCell(() => new Int3(0, 0, 0));

                // Act
                target.PlayOneShot(new AudioCueDto(cue: 1, intensity: 100, cell: new Int3(3, 0, 4),
                                                  source: 7, looped: false));

                // Assert:两个格都真的被喂进去了(旧 `Func<bool>` 签名下这一断言不可表达)
                Assert.That(calls, Is.EqualTo(1), "遮挡缝须被问一次(Spawn 时)");
                Assert.That(seenFrom, Is.EqualTo(new Int3(0, 0, 0)),
                    "听者格须来自 listenerCell 缝(不是声源格自身)");
                Assert.That(seenTo, Is.EqualTo(new Int3(3, 0, 4)), "声源格须是 cue.Cell");
                Assert.That(target.OcclusionEvaluations, Is.EqualTo(1), "求值次数可观测");

                spatial.Dispose();
            }
            finally
            {
                pool.Dispose();
                if (clip != null) UnityEngine.Object.DestroyImmediate(clip);
            }
        }

        [Test]
        public void test_renderTarget_withoutSeam_doesNotCount()
        {
            // ④ 「未装配」≠「判了没挡」:未接缝时不计数。
            AudioSourcePool pool = new AudioSourcePool();
            AudioClip clip = AudioClip.Create("occlusion-probe2", 64, 1, 44100, false);
            try
            {
                var spatial = new SpatializationDirector(latticeSize: 1f, pool);
                var target = new AudioSourceRenderTarget(pool, spatial, 1f, _ => clip);

                target.PlayOneShot(new AudioCueDto(cue: 1, intensity: 100, cell: new Int3(3, 0, 4),
                                                  source: 7, looped: false));

                Assert.That(target.OcclusionEvaluations, Is.EqualTo(0),
                    "未接遮挡缝 ⇒ 不得计数 —— 否则「未装配」会被读成「判了没挡」");
                spatial.Dispose();
            }
            finally
            {
                pool.Dispose();
                if (clip != null) UnityEngine.Object.DestroyImmediate(clip);
            }
        }

        // ══════════════ 工具 ══════════════

        /// <summary>造一份合成世界几何(<paramref name="blocked"/> 决定哪些格不可走)。</summary>
        private static WorldGeometry Grid(int sx, int sy, int sz,
                                          System.Func<int, int, int, bool> blocked)
        {
            var terrain = new TerrainCell[sx, sy, sz];
            for (int x = 0; x < sx; x++)
                for (int y = 0; y < sy; y++)
                    for (int z = 0; z < sz; z++)
                        terrain[x, y, z] = new TerrainCell(!blocked(x, y, z), 0);

            int total = sx * sy * sz;
            var poi = new int[total];
            var res = new int[total];
            for (int i = 0; i < total; i++) { poi[i] = -1; res[i] = -1; }
            return new WorldGeometry(terrain, poi, res);
        }
    }
}
