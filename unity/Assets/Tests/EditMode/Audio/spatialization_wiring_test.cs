// S15-4 —— TD-audio-028「空间化链接入生产」的验收夹具。
//   权威依据:ADR-028 ①③⑤ · GDD F-44.4(锚点)/ F-44.7(世界语境)/ 优先级公式(:848)
//   story-008 §NOT-RUN「三件套 pin 归装配轮」· AC-018-4(单池不变量)
//
// ⚠️ **本夹具补的是 story-018 的 S3-a 缺口**:评审实测 14 例证伪**无一针对**
//    「自建第二池 ⇒ 转红」分支(`runtime_assembly_test.cs::test_ac018_4_spatializationDirector_
//    sharedPool_keepsSingleRoot` 只测**共享池**重载)⇒ 第二池失效形态此前实际不可见。
//    本文件 `test_td028_selfBuiltPool_wouldBreakSinglePoolInvariant` 就是那条负例。
//
// ⚠️ **本夹具测的是「接线存在且位置被写」,不是「听感」** —— 距离衰减 / 遮挡音色
//    的人耳半边归听测批(TD-audio-101/102),此处一律 NOT-RUN,禁借绿。
//
// ⚠️ **被测对象全部是真生产件**(承 runtime_assembly_test 同口径,零自造替身):
//    `AudioSourceRenderTarget` / `AudioSourcePool` / `SpatializationDirector` /
//    `CompositionRoot.Assemble` / `BootRoot.BuildAudioSeams`。
//    注入的假件只有**生产代码自己声明的外部缝**:`IDataProvider`(数据装载面)·
//    `Func<AudioClip>` / `Func<bool>`(素材 / 遮挡缝)· `Transform`(听者,生产 = 主相机)。
//    唯一字面量 = 混合器资产路径(`Assets/Audio/DaYiJingCheng.mixer`,与 mixer_topology_test 同值)。
//
// ⚠️ **静态状态纪律**:`PlayerBusVolumeInitializer.MixerResolver` 是全局静态缝 ——
//    `BuildAudioSeams` 会写它 ⇒ `[SetUp]` 快照 / `[TearDown]` 还原(同 runtime_assembly_test)。
//
// 测试纪律:arrange/act/assert · 无随机 · 无时间依赖 · 零文件系统写。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using UnityEditor;                            // AssetDatabase(真混音器资产 = 生产装载面的替身)
using UnityEngine;
using UnityEngine.Audio;
using DaYiJingCheng.EditorTools.Bake;         // AudioEventTableBake(走真烘焙链路)
using DaYiJingCheng.Gameplay.Boot;            // CompositionRoot / AudioAssemblySeams / BootRoot
using DaYiJingCheng.Gameplay.Presentation.Audio;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Tests.Unit.Audio
{
    [TestFixture]
    internal sealed class SpatializationWiringTest
    {
        // 本文件在 unity/Assets/Tests/EditMode/Audio/ ⇒ 仓库根 = 5 层
        // (写 4 层会落在 unity/ 下,作者态数据全报「缺失」—— 承 runtime_assembly_test 注)。
        private static string RepoRoot([CallerFilePath] string thisFile = "")
            => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile) ?? ".",
                "..", "..", "..", "..", ".."));

        private const string MixerAssetPath = "Assets/Audio/DaYiJingCheng.mixer";

        private readonly List<UnityEngine.Object> _cleanup = new List<UnityEngine.Object>();
        private Func<AudioMixer> _mixerResolverSnapshot;

        [SetUp]
        public void SetUp() => _mixerResolverSnapshot = PlayerBusVolumeInitializer.MixerResolver;

        [TearDown]
        public void TearDown()
        {
            PlayerBusVolumeInitializer.MixerResolver = _mixerResolverSnapshot;

            for (int i = 0; i < _cleanup.Count; i++)
                if (_cleanup[i] != null) UnityEngine.Object.DestroyImmediate(_cleanup[i]);
            _cleanup.Clear();

            // 池根是 `new GameObject` 产物,不进引用链 ⇒ 按名兜底(同 runtime_assembly_test 口径)。
            foreach (GameObject root in PoolRoots())
                UnityEngine.Object.DestroyImmediate(root);
        }

        private static GameObject[] PoolRoots()
            => Resources.FindObjectsOfTypeAll<GameObject>()
                .Where(g => g != null && g.name == AudioSourcePool.RootName && g.scene.IsValid())
                .ToArray();

        private AudioClip MakeClip(string name = "spatial-test-clip")
        {
            AudioClip clip = AudioClip.Create(name, 64, 1, 44100, false);
            _cleanup.Add(clip);
            return clip;
        }

        private AudioSourceRenderTarget MakeTarget(AudioSourcePool pool, SpatializationDirector spatial,
                                                   float latticeSize = 1f, AudioClip clip = null,
                                                   Func<Int3, Int3, bool> occluded = null)
        {
            // ⚠️ 总线缝传 null(**不挂组**)—— 本夹具的池不带 `AudioBusResolver`,
            //    传非 null 总线会被池 fail-loud 拒(禁静默落 Master,A-6)。总线归属归 A-6,
            //    与空间化无关,故此处刻意不覆盖。
            // 🔁 2026-10-29(`TD-audio-035` ②):遮挡缝签名由 `Func<bool>` 改为
            //    `Func<Int3, Int3, bool>`(双格入参)—— 本夹具随之收窄,判据面不变。
            AudioClip c = clip ?? MakeClip();
            return new AudioSourceRenderTarget(pool, spatial, latticeSize, _ => c, busForCue: null, occluded);
        }

        private static AudioCueDto Cue(int source, int x, int y, int z, bool looped = false)
            => new AudioCueDto(cue: 3, intensity: 200, cell: new Int3(x, y, z), source: source, looped: looped);

        private static float WorldOf(int cell, float latticeSize) => cell * latticeSize;

        [Test]
        public void test_td028_oneShot_writesWorldPosition_fromCueCell()
        {
            // Arrange:格 (3,0,4)、格边长 2 m ⇒ 锚点 (6,0,8);jitter 是**格内**偏移
            var pool = new AudioSourcePool();
            _cleanup.Add(pool.Root);
            var spatial = new SpatializationDirector(latticeSize: 2f, pool: pool);
            AudioSourceRenderTarget target = MakeTarget(pool, spatial, latticeSize: 2f);

            // Act
            target.PlayOneShot(Cue(source: 7, x: 3, y: 0, z: 4));
            Vector3 p = pool.SourceForActor(7).transform.position;

            // Assert
            Assert.That(target.SpatializedCount, Is.EqualTo(1), "可观测面未计数 —— Place 未执行");
            Assert.That(p.x, Is.InRange(WorldOf(3, 2f), WorldOf(3, 2f) + 2f),
                $"X 不在格 3 × 2 m 的格内 —— 实得 {p.x}");
            Assert.That(p.y, Is.EqualTo(0f).Within(1e-5), "Y 不参与格内偏移");
            Assert.That(p.z, Is.InRange(WorldOf(4, 2f), WorldOf(4, 2f) + 2f),
                $"Z 不在格 4 × 2 m 的格内 —— 实得 {p.z}");
        }

        [Test]
        public void test_td028_startLoop_writesWorldPosition_onTheLoopSource()
        {
            // ⚠️ **本测钉的是接线里最容易错的一格**:循环源在池里的键 = `LoopActorOf(handleId)`
            //    (负偏移),与 `cue.Source` **不同**。若 Place 把原 cue 交给导演,导演按
            //    `cue.Source` 取池条目 ⇒ 位置写到另一条源(或新建一条幽灵源),而真正在响的
            //    循环源留在原点 —— 呼吸层是所有声源里最长响的那一层。
            // Arrange
            var pool = new AudioSourcePool();
            _cleanup.Add(pool.Root);
            var spatial = new SpatializationDirector(latticeSize: 1f, pool: pool);
            AudioSourceRenderTarget target = MakeTarget(pool, spatial);

            // Act
            target.StartLoop(Cue(source: 9, x: 5, y: 0, z: 1, looped: true), handleId: 1);

            // Assert ①:循环源(负键)的位置在格 (5,·,1)
            AudioSource loopSource = pool.SourceForActor(-2);     // LoopActorOf(1) = -1-1
            Assert.That(loopSource, Is.Not.Null, "前置:循环源已生");
            Assert.That(loopSource.transform.position.x, Is.InRange(WorldOf(5, 1f), WorldOf(5, 1f) + 1f),
                "循环源位置未写 —— 呼吸层会一直响在原点");
            Assert.That(loopSource.transform.position.z, Is.InRange(WorldOf(1, 1f), WorldOf(1, 1f) + 1f));

            // Assert ②:不得在 `cue.Source` 处造幽灵源(那是池键混淆的最直接证据)
            Assert.That(pool.SourceForActor(9), Is.Null,
                "cue.Source 处出现了活动源 —— Place 用了原 cue 而不是池键(位置写错对象)");

            // Assert ③:排名输入登记的是循环源键
            Assert.That(spatial.Order.Single().ActorId, Is.EqualTo(-2));
            Assert.That(target.SpatializedCount, Is.EqualTo(1));
        }

        // ══════════════════════════════════════════════════════════════════
        // 三件套 pin(池创建点统一 pin,story-008 §NOT-RUN 的兑现)
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_td028_poolPinsSpatialTriple_atCreationPoint()
        {
            // Arrange
            var pool = new AudioSourcePool();
            _cleanup.Add(pool.Root);
            var spatial = new SpatializationDirector(latticeSize: 1f, pool: pool);
            AudioSourceRenderTarget target = MakeTarget(pool, spatial);

            // Act
            target.PlayOneShot(Cue(source: 11, x: 1, y: 0, z: 1));
            AudioSource src = pool.SourceForActor(11);

            // Assert
            Assert.That(src.spatialBlend, Is.EqualTo(1f).Within(1e-6),
                "spatialBlend ≠ 1 ⇒ 声音是 2D 的,transform.position 写了也不生效");
            Assert.That(src.rolloffMode, Is.EqualTo(AudioRolloffMode.Logarithmic),
                "rolloff 非 Logarithmic ⇒ 远处不是「听不清」而是骤停,形同门限");
            Assert.That(src.dopplerLevel, Is.EqualTo(0f).Within(1e-6),
                "多普勒未关 ⇒ 玩家移动会拉出音高变化(可听辨的伪事件)");
            Assert.That(src.minDistance, Is.EqualTo(AudioTuning.SpatialMinDistance).Within(1e-5));
            Assert.That(src.maxDistance, Is.EqualTo(AudioTuning.SpatialMaxDistance).Within(1e-5));
        }

        // ══════════════════════════════════════════════════════════════════
        // **单池不变量**(AC-018-4 / ADR-028 ①)
        //   ⚠️ 负例是 story-018 S3-a 缺口的补:**此前没测**「自建池会建第二个根」。
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_td028_selfBuiltPool_wouldBreakSinglePoolInvariant()
        {
            // Arrange:零池根 → 共享池 = 恰 1 根
            Assert.That(PoolRoots().Length, Is.EqualTo(0), "前置:起点零池根");
            var sharedPool = new AudioSourcePool();
            _cleanup.Add(sharedPool.Root);
            Assert.That(PoolRoots().Length, Is.EqualTo(1), "前置:共享池已建一个根");

            // Act:**反例** —— 走自建池重载(生产**禁止**的那条)
            var rogue = new SpatializationDirector(latticeSize: 1f);
            try
            {
                // Assert:2 根 = 「静默分裂」的形态本身
                Assert.That(PoolRoots().Length, Is.EqualTo(2),
                    "自建池重载本应建第二个根 —— 若这里不是 2,说明被测对象变了,须重裁本测");
            }
            finally
            {
                rogue.Dispose();   // 只销毁它自建的那个根
            }

            Assert.That(PoolRoots().Length, Is.EqualTo(1), "反例清理后应回到单根");
        }

        [Test]
        public void test_td028_productionAssembly_withSeams_sharesOnePool_withDirector()
        {
            // ⚠️ 本测直驱**组合根生产分支**(audio.HasAny ⇒ 建导演 + 真渲染目标)。
            //    判别力:把 `new SpatializationDirector(latticeSize, audioPool)` 换成自建重载,
            //    或把渲染目标的池换成第二个池 ⇒ 本条转红。
            // Arrange
            Assert.That(PoolRoots().Length, Is.EqualTo(0), "前置:夹具起点零池根");
            AudioAssemblySeams seams = BootRoot.BuildAudioSeams(
                new TableProvider(BakedTable()), mixerLoader: RealMixer);
            Assert.That(seams, Is.Not.Null, "前置:缝已接");
            // 🔁 2026-10-29(`TD-audio-035` ①):缝的格边长默认已是 **0 = 未供给**
            //    (旧默认 1f 是悄悄生效的 44 自造比例)。本测改用 6 的值 2500 mm ⇒ 2.5 m,
            //    使下面那条「渲染目标的格边长 == 缝给的格边长」**有判别力**
            //    (组合根若忽略缝值回落 1 m,断言即红)。
            AudioAssemblySeams.ApplyLatticeFromWorldGeometry(seams, 2500);

            // Act
            CompositionRootServices bag = CompositionRoot.Assemble(audio: seams);

            // Assert ①:渲染面真装配,导演存在,且**共享**袋里那只池
            Assert.That(bag.AudioRenderAssembled, Is.True);
            Assert.That(bag.Spatialization, Is.Not.Null, "接了缝却没建空间化导演 —— TD-audio-028 未接线");
            Assert.That(bag.RenderTarget, Is.Not.Null);
            Assert.That(bag.Spatialization.ActiveCount, Is.EqualTo(bag.AudioPool.ActiveCount),
                "导演报的在响数与袋内池不一致 —— 两者不是同一个池");
            Assert.That(bag.RenderTarget.LatticeSize, Is.EqualTo(seams.LatticeSize).Within(1e-6),
                "渲染目标的格边长与缝给的格边长不一致 —— 会与导演各算一套比例");

            // Assert ②:运行期池根恰一个(整条装配没有外加池)
            GameObject[] roots = PoolRoots();
            Assert.That(roots.Length, Is.EqualTo(1), "单池不变量:装配后池根恰一");
            Assert.That(roots[0], Is.SameAs(bag.AudioPool.Root));
        }

        [Test]
        public void test_td028_productionAssembly_withoutSeams_buildsNoDirector()
        {
            // Arrange + Act:无参重载(62 处既有调用形态)
            CompositionRootServices bag = CompositionRoot.Assemble();

            // Assert:未装配渲染面 ⇒ 不建导演(可观测,不静默)
            Assert.That(bag.AudioRenderAssembled, Is.False);
            Assert.That(bag.Spatialization, Is.Null, "未装配渲染面却建了导演 —— 会白建一个池的借用方");
            Assert.That(bag.RenderTarget, Is.Null);
            Assert.That(PoolRoots().Length, Is.EqualTo(1), "池仍恰一个(无第二个来源)");
        }

        // ══════════════════════════════════════════════════════════════════
        // 听者注入:距离经**派生的世界坐标** + 注入的 Transform ⇒ 影响真人耳
        //   判别力:把 SetListenerPosition 的方法体清空 ⇒ 距离恒 0 ⇒ 本条转红。
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_td028_listenerInjection_movesTheSourceRelativeToListener()
        {
            // Arrange:未注入听者 ⇒ 距离 0(降级的可观测面)
            var pool = new AudioSourcePool();
            _cleanup.Add(pool.Root);
            var spatial = new SpatializationDirector(latticeSize: 1f, pool: pool);
            AudioSourceRenderTarget target = MakeTarget(pool, spatial);

            var listenerGo = new GameObject("listener-probe");
            _cleanup.Add(listenerGo);
            listenerGo.transform.position = new Vector3(10f, 0f, 0f);
            PoolRoots();   // 池是场景根产物;听者也是场景根产物 —— 二者无需父子关系

            // Act ①:注入前发一条 cue
            target.PlayOneShot(Cue(source: 21, x: 0, y: 0, z: 0));
            float before = DistanceOf(spatial, 21);

            // Act ②:注入听者 + 再发一条(位置仍是原点附近)
            target.SetListenerPosition(listenerGo.transform);
            target.PlayOneShot(Cue(source: 21, x: 0, y: 0, z: 0));
            float after = DistanceOf(spatial, 21);

            // Assert:注入前后距离不同,且后者 ≈ 听者到锚点的距离(10 m,误差 = 格内偏移 ≤ 1 m)
            Assert.That(before, Is.EqualTo(0f), "未注入听者 ⇒ 距离应为 0(明确降级,不是 NaN)");
            Assert.That(after, Is.InRange(9f, 11f),
                $"注入听者后距离 = {after} —— 应 ≈ 10 m(听者在 (10,0,0),源在原点格内)");
        }

        [Test]
        public void test_td028_withoutListener_distanceIsZero_notNaN()
        {
            // Arrange
            var pool = new AudioSourcePool();
            _cleanup.Add(pool.Root);
            var spatial = new SpatializationDirector(latticeSize: 1f, pool: pool);
            AudioSourceRenderTarget target = MakeTarget(pool, spatial);

            // Act
            target.PlayOneShot(Cue(source: 23, x: 3, y: 0, z: 3));
            float d = DistanceOf(spatial, 23);

            // Assert:0 是**明确的降级** —— NaN / 负数会污染 rank_key 的比较链
            Assert.That(d, Is.EqualTo(0f));
            Assert.That(float.IsNaN(d), Is.False);
        }

        private static float DistanceOf(SpatializationDirector spatial, int actorId)
        {
            RankedSource? entry = spatial.Order.FirstOrDefault(r => r.ActorId == actorId);
            Assert.That(entry.HasValue, Is.True, $"排名输入里没有 actor {actorId} —— Spawn 未登记");
            return entry.Value.Distance;
        }

        // ══════════════════════════════════════════════════════════════════
        // 遮挡缝:本轮生产恒 false ⇒ 低通**不得**被施加
        //   ⚠️ 真 raycast NOT-RUN(用户 2026-10-18 裁定);本组只钉「缝的两个方向都通」。
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_td028_occludedFalse_appliesNoLowpass()
        {
            // Arrange
            var pool = new AudioSourcePool();
            _cleanup.Add(pool.Root);
            var spatial = new SpatializationDirector(latticeSize: 1f, pool: pool);
            AudioSourceRenderTarget target = MakeTarget(pool, spatial, occluded: (from, to) => false);

            // Act
            target.PlayOneShot(Cue(source: 31, x: 0, y: 0, z: 0));
            var filter = pool.SourceForActor(31).GetComponent(typeof(AudioLowPassFilter)) as AudioLowPassFilter;

            // Assert
            Assert.That(filter == null || !filter.enabled, Is.True,
                "遮挡判定为 false 却施加了低通 —— 「穿门变闷」被无依据地施加");
        }

        [Test]
        public void test_td028_occludedTrue_appliesLowpass_throughSameSeam()
        {
            // Arrange:缝本身可用(今天生产恒传 false,接真 raycast 时不必再改 44)
            var pool = new AudioSourcePool();
            _cleanup.Add(pool.Root);
            var spatial = new SpatializationDirector(latticeSize: 1f, pool: pool);
            AudioSourceRenderTarget target = MakeTarget(pool, spatial, occluded: (from, to) => true);

            // Act
            target.PlayOneShot(Cue(source: 33, x: 0, y: 0, z: 0));
            var filter = pool.SourceForActor(33).GetComponent(typeof(AudioLowPassFilter)) as AudioLowPassFilter;

            // Assert
            Assert.That(filter, Is.Not.Null, "遮挡为真却未挂低通 —— 隔门变成静音(违 AC-44-D8 ①)");
            Assert.That(filter.enabled, Is.True);
            Assert.That(filter.cutoffFrequency, Is.GreaterThan(0f), "cutoff ≤ 0 = 静音语义");
        }

        // ══════════════════════════════════════════════════════════════════
        // 未接空间化的旧构造:行为不变(不写位置、不计数)
        //   这是「既有 62 处调用不被本改动影响」的回归面。
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_td028_legacyConstructor_noSpatialization_keepsOldBehaviour()
        {
            // Arrange
            var pool = new AudioSourcePool();
            _cleanup.Add(pool.Root);
            AudioClip clip = MakeClip();
            // ⚠️ 显式类型:两个既有构造的 lambda 形参类型不同,裸 `cue => clip` 会 CS0121 歧义。
            //    总线缝 null 同理(池无 resolver)。
            var target = new AudioSourceRenderTarget(pool, (int cue) => clip, busForCue: null);

            // Act
            target.PlayOneShot(Cue(source: 41, x: 5, y: 0, z: 5));

            // Assert
            Assert.That(target.SpatializedCount, Is.Zero, "旧构造不应写空间化锚点");
            Assert.That(target.LatticeSize, Is.Zero, "未接空间化 ⇒ 格边长不可用(0)");
            AudioSource src = pool.SourceForActor(41);
            Assert.That(src, Is.Not.Null, "旧构造仍须正常取源");
            Assert.That(src.transform.position, Is.EqualTo(Vector3.zero),
                "旧构造写了位置 —— 与「行为不变」相抵");
        }

        // ══════════════ 外部缝(生产代码自己声明的注入点)══════════════

        private static AudioEventDataSet BakedTable()
            => AudioEventTableBake.Materialize(AudioEventTableBake.BakeFromRepo(RepoRoot()));

        /// <summary>把运行期表包成 `IDataProvider`(生产 = `AddressablesDataProvider`)。</summary>
        private sealed class TableProvider : IDataProvider
        {
            private readonly AudioEventDataSet _table;
            public TableProvider(in AudioEventDataSet table) { _table = table; }
            public TDataSet Load<TDataSet>() where TDataSet : struct
            {
                object boxed = _table;
                return (TDataSet)boxed;
            }
        }

        private static AudioMixer RealMixer()
        {
            AudioMixer mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerAssetPath);
            Assert.That(mixer, Is.Not.Null, $"混音器资产装载失败:{MixerAssetPath}");
            return mixer;
        }
    }
}
