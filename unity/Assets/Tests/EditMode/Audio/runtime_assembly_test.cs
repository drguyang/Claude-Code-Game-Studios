// 权威来源:production/epics/audio-system/story-018-runtime-audio-assembly.md
//   (AC-018-1 真渲染目标 · AC-018-2 总线路由 · AC-018-3 组合根装配 · AC-018-4 单池不变量
//    · AC-018-5 混音器 / 玩家音量 · AC-018-6 cue 源接线 · AC-018-7 处方音效白名单)
// ADR-018 §一(44 只触发 / 只渲染)· §二(AudioCueDto 唯一入口)· §三(七总线)
// ADR-028 ①(声源池唯一所有者 = 44)· ⑤(cue 驱动生命周期)· ADR-025 §①(装配清单)
// ADR-024 §⑥(白名单 / 常量单一出处 —— 本夹具不另抄字面量)
//
// ⚠️ 落点:story §Test Evidence 登记路径 = `unity/Assets/Tests/EditMode/Audio/runtime_assembly_test.cs`
//    (Unity 只编译 `unity/Assets/` 树 ⇒ 真身即本文件;账本侧由
//     `tests/integration/audio_system/README.md` 互链)。
//
// ⚠️ **本夹具的被测对象全部是真生产件** —— 零自造替身:
//    `AudioSourceRenderTarget` / `AudioSourcePool` / `AudioBusResolver` / `CueRenderSink` /
//    `AudioClipLibrary` / `PatientCueDriver` / `CompositionRoot.Assemble` / `BootRoot.BuildAudioSeams`。
//    注入的假件只有**三类外部缝**,每类都是生产代码**自己声明的**注入点:
//    `IVitalsQuery`(9 的取数面)· `Func<int, AudioClip>`(素材装载面)· `IAudioCueSink` 计数壳(观测量)。
//    ⚠️ 夹具**不复制**任何生产谓词(承 sprint-13 评审原件 §2.4 判 🔴 的病根:
//    「测试自造断言顶替 AC 语义层」)。唯一允许的字面量 = **GDD 点名的 cue 名**
//    (`Page_Turn` / `Cabinet_Prop` —— 它们是**题面**(被测数据里的行),不是谓词。
//
// ⚠️ 测试纪律:arrange/act/assert · 无随机 · 无时间依赖(禁 DateTime / Time)·
//    零文件系统**写**(只读仓库作者态数据 = 本夹具的题面,承 pipeline_gate_test 同口径)。
//
// ⚠️ **静态状态纪律**:`PlayerBusVolumeInitializer.MixerResolver` 是**全局静态缝** ——
//    本夹具的 AC-018-5 会写它 ⇒ `[SetUp]` 快照 / `[TearDown]` 还原,否则跨夹具污染
//    (2026-10-17 PlayMode 9 红同款病灶:静态状态跨轮残留)。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using UnityEditor;                              // AssetDatabase(真混音器资产 = 生产装载面的替身)
using UnityEngine;
using UnityEngine.Audio;
using DaYiJingCheng.EditorTools.Bake;           // AudioEventTableBake(AC-018-7 走真烘焙链路)
using DaYiJingCheng.EditorTools.Gates;          // AudioEventTableGates(白名单单一出处)
using DaYiJingCheng.Gameplay.Boot;              // CompositionRoot / BootRoot.BuildAudioSeams
using DaYiJingCheng.Gameplay.PatientAI;         // PatientCueDriver 的上游(13)
using DaYiJingCheng.Gameplay.Presentation.Audio;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Tests.Unit.Audio
{
    [TestFixture]
    internal sealed class RuntimeAssemblyTest
    {
        // ══════════════ 基础设施 ══════════════

        // 本文件在 unity/Assets/Tests/EditMode/Audio/ ⇒ Audio→EditMode→Tests→Assets→unity→
        // 仓库根 = 5 层(写 4 层 ⇒ root 落在 unity/ 下,作者态数据全报「缺失」)。
        private static string RepoRoot([CallerFilePath] string thisFile = "")
            => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile) ?? ".",
                "..", "..", "..", "..", ".."));

        /// <summary>真混音器资产路径(与 `mixer_topology_test` 同一路径常量 —— 资产落点唯一)。</summary>
        private const string MixerAssetPath = "Assets/Audio/DaYiJingCheng.mixer";

        /// <summary>GDD 点名的处方相关 cue 名(`TD-audio-024` 的断言对象)。
        /// <para>⚠️ 这是**题面**(被测数据里的行名),不是谓词 —— 谓词(合法类别集 / 禁时机集)
        /// 一律取自 <see cref="AudioEventTableGates"/>(单一出处,ADR-024 §⑥)。</para></summary>
        private static readonly string[] PrescriptionCues = { "Page_Turn", "Cabinet_Prop" };

        private Func<AudioMixer> _savedMixerResolver;
        private readonly List<UnityEngine.Object> _cleanup = new List<UnityEngine.Object>();

        [SetUp]
        public void SetUp()
        {
            _savedMixerResolver = PlayerBusVolumeInitializer.MixerResolver;
        }

        [TearDown]
        public void TearDown()
        {
            PlayerBusVolumeInitializer.MixerResolver = _savedMixerResolver;

            for (int i = 0; i < _cleanup.Count; i++)
                if (_cleanup[i] != null) UnityEngine.Object.DestroyImmediate(_cleanup[i]);
            _cleanup.Clear();

            // 池根是 `new GameObject` 产物(不进 `_cleanup` 的引用链)⇒ 按名兜底清理,
            // 否则跨夹具残留同名根 ⇒ AC-018-4 的「根只有一个」断言假红。
            foreach (GameObject root in PoolRoots())
                UnityEngine.Object.DestroyImmediate(root);
        }

        /// <summary>场景内所有名为 <see cref="AudioSourcePool.RootName"/> 的 GameObject。
        /// <para>⚠️ <b>必须走 <see cref="Resources.FindObjectsOfTypeAll{T}"/>,不能用
        /// <c>Object.FindObjectsByType</c></b> —— 池根带 <c>HideFlags.DontSave</c>
        /// (池构造器刻意设的:运行期产物不进场景序列化),而 `FindObjectsByType` **不返回**
        /// DontSave 对象 ⇒ 断言会得到 0(2026-10-17 实测:本条首版即因此假红)。
        /// `FindObjectsOfTypeAll` 连 DontSave 一起给,再用 <c>scene.IsValid()</c> 滤掉
        /// 工程资产(预制体等,其 scene 无效)。</para></summary>
        private static GameObject[] PoolRoots()
            => Resources.FindObjectsOfTypeAll<GameObject>()
                .Where(g => g != null && g.name == AudioSourcePool.RootName && g.scene.IsValid())
                .ToArray();

        /// <summary>真 clip(EditMode 可造:空波形,只为「有 clip ⇒ 播」这一条判据)。</summary>
        private AudioClip MakeClip(string name = "rt-test-clip")
        {
            AudioClip clip = AudioClip.Create(name, 64, 1, 44100, false);
            _cleanup.Add(clip);
            return clip;
        }

        private static AudioCueDto Cue(int ordinal, int source = 7, int x = 1, int y = 0, int z = 2,
                                       bool looped = false)
            => new AudioCueDto(ordinal, 128, new Int3(x, y, z), source, looped);

        // ── 三类外部缝的假件(每类都是生产代码自己声明的注入点)──

        /// <summary>9 的体征查询假件(常数读数)。</summary>
        private sealed class FakeVitals : IVitalsQuery
        {
            private readonly float _position;
            private readonly float _trend;
            public FakeVitals(float position, float trend = 0f) { _position = position; _trend = trend; }
            public VitalsDto GetVitals(PatientId p) => new VitalsDto(_position, _trend, 0, 0);
        }

        /// <summary>素材装载面假件(cue ordinal → clip 查表)。
        /// <para>⚠️ 生产侧的解析缝是**委托** `Func&lt;int, AudioClip&gt;`,不是接口 ——
        /// AC-44-B3 入口门只放行 `IAudioCueSink`(见 `AudioSourceRenderTarget` 头注)。
        /// 故本假件同样是委托(`Clips(...)` 工厂),不再声明任何实现接口的类型。</para></summary>
        private static Func<int, AudioClip> Clips(params (int cue, AudioClip clip)[] entries)
        {
            var map = new Dictionary<int, AudioClip>();
            for (int i = 0; i < entries.Length; i++) map[entries[i].cue] = entries[i].clip;
            return cue => map.TryGetValue(cue, out AudioClip c) ? c : null;
        }

        /// <summary>计数壳:`IAudioCueSink` 的观测量(生产实装 = `CueRenderSink`,已在别处判)。</summary>
        private sealed class CountingSink : IAudioCueSink
        {
            public int EmitCount;
            public int BeginLoopCount;
            public int EndLoopCount;
            public int LastCue = AudioEventDataSet.NoOrdinal;
            public int LastLoopCue = AudioEventDataSet.NoOrdinal;

            public void Emit(in AudioCueDto cue) { EmitCount++; LastCue = cue.Cue; }
            public AudioCueHandle BeginLoop(in AudioCueDto cue)
            {
                BeginLoopCount++; LastLoopCue = cue.Cue;
                return new AudioCueHandle(BeginLoopCount);
            }
            public void EndLoop(AudioCueHandle handle) { EndLoopCount++; }
            public void SetTier(TierSource source) { }
        }

        /// <summary>烘焙产物的运行期表(AC-018-3/5 的装配输入 —— 走**真烘焙 + 真往返**)。</summary>
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
            Assert.That(mixer, Is.Not.Null,
                $"混音器资产装载失败:{MixerAssetPath}(跑 MixerAssetGenerator.Create batch 生成)");
            return mixer;
        }

        // ══════════════════════════════════════════════════════════════════
        // AC-018-1 —— 真渲染目标(关 A-2)
        // 判别力:让 `PlayOneShot` 变成空实现 ⇒ test_ac018_1_*_activeCount 转红。
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_ac018_1_playOneShot_spawnsActiveSourceInPool()
        {
            // Arrange
            var pool = new AudioSourcePool();
            var target = new AudioSourceRenderTarget(pool, Clips((3, MakeClip())));
            Assert.That(pool.ActiveCount, Is.EqualTo(0), "前置:新池零活动源");

            // Act
            target.PlayOneShot(Cue(ordinal: 3, source: 7));

            // Assert —— 「cue 触发后池内 ActiveCount 增」(story AC-018-1 的判别力断言)
            Assert.That(pool.ActiveCount, Is.EqualTo(1));
            Assert.That(target.ClipMissCount, Is.EqualTo(0));
            AudioSource source = pool.SourceForActor(7);
            Assert.That(source, Is.Not.Null, "一次性 cue 的 actor 键 = 声源实体 id");
            Assert.That(source.clip, Is.Not.Null, "载入 clip 是「有声」的前提");
            Assert.That(source.loop, Is.False);
        }

        [Test]
        public void test_ac018_1_playOneShot_clipMiss_doesNotSpawn_andIsObservable()
        {
            // Arrange:池已装配,但 clip 面解析不到(clipOf = null ⇒ 恒 miss,可观测)
            var pool = new AudioSourcePool();
            var target = new AudioSourceRenderTarget(pool, clipOf: null);

            // Act
            target.PlayOneShot(Cue(ordinal: 3));

            // Assert:不静默 —— miss 计数可观测,且**不占池**(缺素材不该造出空转声源)
            Assert.That(pool.ActiveCount, Is.EqualTo(0));
            Assert.That(target.ClipMissCount, Is.EqualTo(1));
        }

        [Test]
        public void test_ac018_1_startLoop_stopLoop_poolLifecycleFollowsHandles()
        {
            // Arrange
            var pool = new AudioSourcePool();
            var target = new AudioSourceRenderTarget(pool, Clips((1, MakeClip())));

            // Act:起循环
            target.StartLoop(Cue(ordinal: 1, source: 4, looped: true), handleId: 11);

            // Assert:循环占池 + 是 loop 源
            Assert.That(target.ActiveLoopCount, Is.EqualTo(1));
            Assert.That(pool.ActiveCount, Is.EqualTo(1));
            AudioSource loopSource = pool.SourceForActor(-11 - 1);   // 循环键空间 = 负偏移
            Assert.That(loopSource, Is.Not.Null);
            Assert.That(loopSource.loop, Is.True);

            // Act:停循环
            target.StopLoop(11);

            // Assert:句柄与池条目同生共死
            Assert.That(target.ActiveLoopCount, Is.EqualTo(0));
            Assert.That(pool.ActiveCount, Is.EqualTo(0));
        }

        [Test]
        public void test_ac018_1_stopLoop_staleHandle_isNoOp()
        {
            // Arrange
            var pool = new AudioSourcePool();
            var target = new AudioSourceRenderTarget(pool, Clips((1, MakeClip())));
            target.StartLoop(Cue(ordinal: 1, source: 4, looped: true), handleId: 5);
            target.StopLoop(5);

            // Act:对已停句柄再停一次(承 CueRenderSink「过期 EndLoop 忽略」同口径)
            target.StopLoop(5);

            // Assert:不抛、不误伤(仍 0)
            Assert.That(target.ActiveLoopCount, Is.EqualTo(0));
            Assert.That(pool.ActiveCount, Is.EqualTo(0));
        }

        // ══════════════════════════════════════════════════════════════════
        // AC-018-2 —— 总线路由(A-6 · 运行期 `outputAudioMixerGroup`)
        // 判别力:删掉 `ApplyBus` 里的赋值 ⇒ bus 归属断言转红。
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_ac018_2_spawnWithBus_assignsMixerGroupFromRegistry()
        {
            // Arrange
            AudioMixer mixer = RealMixer();
            var resolver = new AudioBusResolver(mixer);
            var pool = new AudioSourcePool(busResolver: resolver);
            const string bus = "Voice";                      // MixerRegistry.BusNames 成员

            // Act
            AudioSource source = pool.Spawn(actorId: 9, 1, 0, 2, cueId: 0, bus: bus);

            // Assert:源真挂到了该总线的组(唯一写 outputAudioMixerGroup 的地方)
            AudioMixerGroup expected = resolver.GroupFor(bus);
            Assert.That(source.outputAudioMixerGroup, Is.Not.Null,
                "A-6:不挂组 ⇒ 全部走 Master 直通,七总线拓扑形同虚设");
            Assert.That(source.outputAudioMixerGroup.name, Is.EqualTo(expected.name));
            Assert.That(pool.BusForActor(9), Is.EqualTo(bus));
        }

        [Test]
        public void test_ac018_2_spawnWithBus_withoutResolver_throws()
        {
            // Arrange:池**未装配**解析器(装配错),但调用方指定了 bus
            var pool = new AudioSourcePool();

            // Act + Assert:fail-loud,禁静默落 Master
            Assert.Throws<InvalidOperationException>(() => pool.Spawn(9, 1, 0, 2, 0, "Voice"));
        }

        [Test]
        public void test_ac018_2_unknownBus_isRejectedByResolver()
        {
            // Arrange
            var resolver = new AudioBusResolver(RealMixer());

            // Act + Assert:未知 bus 名 = 装配错(承 AudioBusResolver 头注:禁静默落 Master)
            Assert.Throws<ArgumentException>(() => resolver.GroupFor("NotABus"));
        }

        [Test]
        public void test_ac018_2_busName_sourceIsEventTableColumn_notCodeCopy()
        {
            // Arrange:真烘焙表 → 真 clip 库(库的 BusOf 读**事件表 bus 列**)
            AudioEventDataSet table = BakedTable();
            // ⚠️ S15-5(TD-audio-032 ⑤):空表前置 —— 表若烘成 0 行,下面的循环恒不执行 ⇒ 本测恒绿。
            Assert.That(table.RowCount, Is.GreaterThan(0),
                "烘焙表 0 行 ⇒ 逐行对拍循环恒空 ⇒ 本测零判别力(空表不是「没有分叉」,是没测)");
            var library = new AudioClipLibrary(table);
            try
            {
                // Assert:库报出的 bus 与表内该行**逐行等值**(证明 44 没另抄一份映射)
                for (int ordinal = 0; ordinal < table.RowCount; ordinal++)
                {
                    string fromLibrary = library.BusOf(ordinal);
                    string fromTable = table.Row(ordinal).Bus;
                    Assert.That(fromLibrary, Is.EqualTo(
                        string.IsNullOrEmpty(fromTable) ? null : fromTable),
                        $"ordinal {ordinal}({table.CueAt(ordinal)})的 bus 分叉 —— " +
                        "A-6 的总线归属真源 = 事件表 bus 列(ADR-024 §⑥ 禁第二份)");
                }
            }
            finally { library.Dispose(); }
        }

        // ══════════════════════════════════════════════════════════════════
        // AC-018-3 —— 组合根装配(关 A-1 / A-4)
        // 判别力:把音频装配段删掉 ⇒ `AudioSink` 非 null 且非空转断言转红。
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_ac018_3_assemble_withoutSeams_nullRenderTargetChain_butObservable()
        {
            // Arrange + Act:无参重载(62 处既有调用形态)
            CompositionRootServices bag = CompositionRoot.Assemble();

            // Assert:音频链**恒装配**(契约语义完整),但渲染面未接 —— 且「未接」**可观测**
            Assert.That(bag.AudioSink, Is.Not.Null, "A-1:CueRenderSink 必须有生产构造点");
            Assert.That(bag.AudioPool, Is.Not.Null, "A-4:声源池必须有生产构造点");
            Assert.That(bag.AudioRenderAssembled, Is.False,
                "无缝 ⇒ 走 NullRenderTarget;此标志即「装了没装」的可观测面(不静默)");
            Assert.That(bag.PatientCueDriver, Is.Not.Null, "AC-018-6:病人 cue 驱动须在袋内");
        }

        [Test]
        public void test_ac018_3_assemble_withSeams_rendersAndExposesDriver()
        {
            // Arrange:真烘焙表 + 真混音器(经 BootRoot.BuildAudioSeams —— 生产同一条装配路径)
            AudioAssemblySeams seams = BootRoot.BuildAudioSeams(
                new TableProvider(BakedTable()), mixerLoader: RealMixer);
            Assert.That(seams, Is.Not.Null);

            // Act
            CompositionRootServices bag = CompositionRoot.Assemble(audio: seams);

            // Assert ①:标志(由**已构造的渲染目标**派生,见 CompositionRoot 该实参处注)
            Assert.That(bag.AudioRenderAssembled, Is.True, "有缝 ⇒ 渲染面真装配");
            Assert.That(bag.AudioSink, Is.Not.Null);
            Assert.That(bag.AudioPool, Is.Not.Null);
            Assert.That(bag.PatientCueDriver, Is.Not.Null);

            // Assert ②:**行为探针在 EditMode 不可判 —— NOT-RUN 哨兵**(禁借绿)。
            //  ⚠️ 2026-10-18 实测:EditMode 下 Addressables **不装载 AudioClip**
            //     (`seams.Clips.LoadedCount == 0`)⇒ 无从发出「可播」的 cue ⇒
            //     「经 sink 发 cue ⇒ 池内多出活动源」这条探针在本环境**跑不出判别力**。
            //     **载体 = PlayMode 半边**:`audio_wiring_test.cs::
            //     test_ac018_6_cueReachesRenderTarget_poolBecomesActive`(真启动序 + 真 catalog,
            //     同一条断言)。
            //  ⚠️ 本哨兵是**有意可红**的:哪天 EditMode 也能装载 clip(或装载环境改变),
            //     它就转红 ⇒ 须把探针从「登记」升为「真断言」,而**不是**把哨兵改掉。
            Assert.That(seams.Clips.LoadedCount, Is.EqualTo(0),
                "NOT-RUN 哨兵:EditMode 实测 Addressables 不装载 AudioClip(LoadedCount 应为 0)。" +
                "此值非 0 ⇒ 装载环境已变,须把下面的行为探针改为真断言(载体见注释)。");

            // ⚠️ 判别力说明:本条 AC 对「渲染目标被换成 NullRenderTarget」的判别力来自
            //   **Assert ①**(标志已改为由**已构造的渲染目标**派生 —— 见 CompositionRoot 该实参处注;
            //   证伪轮 F4 实测:原版重算标志 ⇒ 该测不转红 = 假绿,已修)。
            //   真链路的端到端判别力由 PlayMode 半边承载。
        }

        [Test]
        public void test_ac018_3_assemble_secondCall_producesIndependentPool()
        {
            // Arrange + Act:两次装配(测试间常见形态)—— 各自的池互不引用
            CompositionRootServices a = CompositionRoot.Assemble();
            CompositionRootServices b = CompositionRoot.Assemble();

            // Assert:袋是**只读产物**,不复用全局单例(承 CompositionRootServices 头注)
            Assert.That(a.AudioPool, Is.Not.SameAs(b.AudioPool));
            Assert.That(a.AudioSink, Is.Not.SameAs(b.AudioSink));
        }

        // ══════════════════════════════════════════════════════════════════
        // AC-018-4 —— 单池不变量
        // 判别力:让 `SpatializationDirector` 自建第二个池 ⇒ 断言转红。
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_ac018_4_assemble_createsExactlyOnePoolRoot()
        {
            // Arrange
            Assert.That(PoolRoots().Length, Is.EqualTo(0), "前置:夹具起点零池根");

            // Act
            CompositionRootServices bag = CompositionRoot.Assemble();

            // Assert:装配产物的池根**恰一个**(ADR-028 ②「零场景预摆」的对称面)
            GameObject[] roots = PoolRoots();
            Assert.That(roots.Length, Is.EqualTo(1),
                "单池不变量:运行期任意时刻池根只有一个");
            Assert.That(roots[0], Is.SameAs(bag.AudioPool.Root));
        }

        [Test]
        public void test_ac018_4_spatializationDirector_sharedPool_keepsSingleRoot()
        {
            // Arrange
            CompositionRootServices bag = CompositionRoot.Assemble();

            // Act:走**共享池重载**(承 AC-018-4 的判据:共用同一实例,或后者不被构造)
            var director = new SpatializationDirector(latticeSize: 1f, pool: bag.AudioPool);

            // Assert
            Assert.That(PoolRoots().Length, Is.EqualTo(1),
                "SpatializationDirector 共享池 ⇒ 不得再建第二个根");
            Assert.That(director.ActiveCount, Is.EqualTo(bag.AudioPool.ActiveCount));
        }

        // ══════════════════════════════════════════════════════════════════
        // AC-018-5 —— 混音器装载 + 玩家总线音量(关 A-5 / A-7)
        // 判别力:删掉 `MixerResolver` 赋值 ⇒ Master 音量 = 出厂默认断言转红。
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_ac018_5_mixerUnresolved_failsLoudWithReadableDiagnostics()
        {
            // Arrange:provider 有表(缝该接),但混音器解析不到 —— A-7 未补的形态
            var provider = new TableProvider(BakedTable());
            PlayerBusVolumeInitializer.MixerResolver = null;

            // Act
            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => BootRoot.BuildAudioSeams(provider, mixerLoader: () => null));

            // Assert:诊断**可读**(点名 A-7 + address + 修法),且**没有**静默跳过
            Assert.That(ex.Message, Does.Contain("A-7"));
            Assert.That(ex.Message, Does.Contain(AudioClipLibrary.AudioMixerAddress));
            Assert.That(ex.Message, Does.Contain("Addressables"),
                "诊断须指向修法(组登记 / catalog 重建),否则只报「null」不可行动");
            Assert.That(PlayerBusVolumeInitializer.MixerResolver, Is.Null,
                "抛之前不得留下半装配的 resolver");
        }

        [Test]
        public void test_ac018_5_noProvider_returnsNullSeam_notThrow()
        {
            // Arrange + Act:预载失败(provider == null)= 环境降级面,**不是**装配错
            AudioAssemblySeams seams = BootRoot.BuildAudioSeams(null);

            // Assert:返回 null(组合根据此走 NullRenderTarget 链,可观测)—— 不抛
            Assert.That(seams, Is.Null);
        }

        [Test]
        public void test_ac018_5_realMixer_assignsResolverAndAppliesFactoryDefaults()
        {
            // Arrange
            PlayerBusVolumeInitializer.MixerResolver = null;
            var provider = new TableProvider(BakedTable());
            AudioMixer mixer = RealMixer();

            // ⚠️ **本条 AC 的判别力落点(2026-10-18 二次自查后的结论,勿再改回「先破坏再复原」)**:
            //    出厂默认 = 线性 1.0 = **0 dB 中性**,而混音器资产内的初值**恰好也是 0 dB**
            //    ⇒ 「跑了 ApplyDefaults」与「没跑」**值相同** ⇒ 值断言对变异**零判别力**(假绿)。
            //    原打算用「先把 7 参数拨到 −80 再验复原」造判别力 —— **实测不可行**:
            //    `AudioMixer.SetFloat` 在 **EditMode 下不生效**(写 −80 后 `GetFloat` 仍读回 0;
            //    音频 DSP 未运行,参数写入不落地)。⇒ 改由**生产数据**承载判别力:
            //    `seams.BusVolumeErrors` 是 `ApplyDefaults` 的**真实返回值**,非 null 即证明该调用执行过。
            //    值断言保留为**端态断言**(与 PlayMode 半边互补),但**不再声称**它对删除变异有判别力。

            // Act
            AudioAssemblySeams seams = BootRoot.BuildAudioSeams(provider, mixerLoader: () => mixer);

            // Assert ①:缝齐(clip 面 / 总线面 / 表)
            Assert.That(seams.Clips, Is.Not.Null);
            Assert.That(seams.Buses, Is.Not.Null);
            Assert.That(seams.Table.HasValue, Is.True);
            Assert.That(seams.HasAny, Is.True);

            // Assert ②:resolver 被赋值(判别力:删赋值 ⇒ 本断言红)
            Assert.That(PlayerBusVolumeInitializer.MixerResolver, Is.Not.Null,
                "生产路径必须赋值 MixerResolver —— 否则 Bootstrap 静默让位(A-5)");
            Assert.That(PlayerBusVolumeInitializer.MixerResolver(), Is.SameAs(mixer));

            // Assert ③:**显式 ApplyDefaults 真的被调用过**(判别力:删掉该调用 ⇒ `BusVolumeErrors`
            //  恒 null ⇒ 本断言红)。这是本条 AC 的判别力锚点。
            Assert.That(seams.BusVolumeErrors, Is.Not.Null,
                "启动序未执行 ApplyDefaults —— `BusVolumeErrors` 恒 null(A-5 的显式那一步缺失)");
            Assert.That(seams.BusVolumeErrors, Is.Empty,
                "ApplyDefaults 报错(非空即半初始化已回退)—— " +
                string.Join(" / ", seams.BusVolumeErrors ?? new List<string>()));

            // Assert ④:**端态** —— 启动后 7 参数 = 出厂默认。
            //  ⚠️ 如实登记:本断言在 EditMode 下**无独立判别力**(资产初值 = 出厂默认,
            //     且 EditMode 下 SetFloat 不生效 ⇒ 无法造出可分辨的偏离)。
            //     它证明的是「读得回、且与出厂默认一致」这一端态,不是「ApplyDefaults 跑过」。
            var expected = new Dictionary<string, float>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, float> pair in PlayerBusVolumeInitializer.FactoryDefaults)
                expected[pair.Key] = pair.Value;
            Assert.That(expected.Count, Is.EqualTo(MixerRegistry.BusVolumeParameters.Count),
                "出厂默认集须覆盖注册表 7 参数(键覆盖校验的前置)");

            foreach (string param in MixerRegistry.BusVolumeParameters)
            {
                Assert.That(mixer.GetFloat(param, out float actual), Is.True,
                    $"混音器缺 exposed 参数「{param}」");
                Assert.That(actual, Is.EqualTo(expected[param]).Within(0.01f),
                    $"「{param}」读回值 ≠ 出厂默认 —— 端态不一致");
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // AC-018-6 —— cue 源接线(关 A-3)
        // 判别力:删掉 `PatientCueEmit.Emit` 调用 ⇒ cue 触发计数断言转红。
        // ══════════════════════════════════════════════════════════════════

        /// <summary>造一个「在场 + 已建档 + 重档」的病人驱动器。</summary>
        private static PatientCueDriver MakeDriver(IAudioCueSink sink, PresenceRegistry presence,
                                                   int patientId, float position,
                                                   int? coughCue, int? breathCue,
                                                   Func<PatientId, bool> hasCourse = null)
        {
            var vitals = new FakeVitals(position);
            return new PatientCueDriver(
                sink, vitals, presence, new PatientBehaviorDirector(vitals, BehaviorBands.Default),
                intervals: CueIntervals.Default,
                coughCue: coughCue, breathCue: breathCue,
                knowsClinic: KnowsClinicForTest,       // story-021:缝必填(AC-021-1)
                hasCourse: hasCourse);
        }

        private static PresenceRegistry PresenceWith(int patientId)
        {
            var presence = new PresenceRegistry();
            presence.AddPresent(new PatientId(patientId), new WorldPos(1, 0, 2));
            return presence;
        }

        [Test]
        public void test_ac018_6_emitBridge_hasProductionCaller_andFiresOnSchedule()
        {
            // Arrange:position 0.5 ≥ seekMin(0.45)⇒ Severe 档 ⇒ 间隔 40 tick(首拍在 40 内必到)
            var sink = new CountingSink();
            PresenceRegistry presence = PresenceWith(patientId: 7);
            PatientCueDriver driver = MakeDriver(sink, presence, 7, position: 0.5f,
                                                 coughCue: 0, breathCue: 1);

            // Act:推进 200 tick 边沿(远超 Severe 间隔 40)
            for (int tick = 0; tick < 200; tick++) driver.OnTickEdge(tick);

            // Assert ①:一次性 cue 真发出去了(**A-3 的判别力断言**)
            Assert.That(driver.EmitCount, Is.GreaterThanOrEqualTo(1),
                "A-3:`PatientCueEmit` 必须有生产调用方 —— 病人要会咳");
            Assert.That(sink.EmitCount, Is.EqualTo(driver.EmitCount));
            Assert.That(sink.LastCue, Is.EqualTo(0), "ordinal 取自事件表解析值,不是 (int)CueKind 猜的");

            // Assert ②:呼吸层循环起了一次(ADR-018 §四:咳嗽不停呼吸层)
            Assert.That(driver.ActiveBreathCount, Is.EqualTo(1));
            Assert.That(sink.BeginLoopCount, Is.EqualTo(1), "重复 BeginLoop 不该发生(13 的调度去重)");
            Assert.That(sink.LastLoopCue, Is.EqualTo(1));
        }

        [Test]
        public void test_ac018_6_emitInterval_isRespected_betweenFires()
        {
            // Arrange
            var sink = new CountingSink();
            PresenceRegistry presence = PresenceWith(7);
            PatientCueDriver driver = MakeDriver(sink, presence, 7, 0.5f, coughCue: 0, breathCue: null);

            // Act:只推 20 tick(< Severe 间隔 40 ⇒ 首拍最多一次)
            for (int tick = 0; tick < 20; tick++) driver.OnTickEdge(tick);

            // Assert:未到间隔 ⇒ 至多一次(一次性/幂等形态 —— 防「每 tick 都发」的退化)
            Assert.That(driver.EmitCount, Is.LessThanOrEqualTo(1));
        }

        [Test]
        public void test_ac018_6_courseGate_excludesNotYetRegisteredPatient()
        {
            // Arrange:在场但**未建档**(DiseaseOnset 下一拍才被应用)—— hasCourse 恒 false
            var sink = new CountingSink();
            PresenceRegistry presence = PresenceWith(7);
            PatientCueDriver driver = MakeDriver(sink, presence, 7, 0.5f,
                                                 coughCue: 0, breathCue: 1,
                                                 hasCourse: _ => false);

            // Act
            for (int tick = 0; tick < 200; tick++) driver.OnTickEdge(tick);

            // Assert:定义域收窄 —— 未建档者**完全不进调度**(否则撞 GetVitals 的 fail-loud)
            Assert.That(driver.EmitCount, Is.EqualTo(0));
            Assert.That(driver.ActiveBreathCount, Is.EqualTo(0));
            Assert.That(sink.EmitCount, Is.EqualTo(0));
        }

        [Test]
        public void test_ac018_6_nullCoughOrdinal_breathStillStarts_emitStaysZero()
        {
            // Arrange:缝**半接**(呼吸 ordinal 有、咳嗽 ordinal 无)—— 装配期表未解析到咳嗽名的形态
            var sink = new CountingSink();
            PresenceRegistry presence = PresenceWith(7);
            PatientCueDriver driver = MakeDriver(sink, presence, 7, 0.5f,
                                                 coughCue: null, breathCue: 1);

            // Act
            for (int tick = 0; tick < 200; tick++) driver.OnTickEdge(tick);

            // Assert:调度**照跑**(呼吸层起了 —— 证明确实进了 Decide),但一次性 cue 被 ordinal 门挡住。
            //  ⚠️ 这半条是「缝未接 ⇒ 静默无声」的**可观测**形态,不是静默失败。
            Assert.That(driver.ActiveBreathCount, Is.EqualTo(1));
            Assert.That(driver.EmitCount, Is.EqualTo(0));
        }

        [Test]
        public void test_ac018_6_pruneAbsent_endsBreathLoopAndClearsBookkeeping()
        {
            // Arrange:呼吸层已起
            var sink = new CountingSink();
            PresenceRegistry presence = PresenceWith(7);
            PatientCueDriver driver = MakeDriver(sink, presence, 7, 0.5f, coughCue: 0, breathCue: 1);
            driver.OnTickEdge(0);
            Assert.That(driver.ActiveBreathCount, Is.EqualTo(1), "前置:呼吸层已起");

            // Act:病人离场后清理
            presence.RemovePresent(new PatientId(7));
            driver.PruneAbsent(1);

            // Assert:句柄收尾(否则「离场病人还在呼吸」)
            Assert.That(driver.ActiveBreathCount, Is.EqualTo(0));
            Assert.That(sink.EndLoopCount, Is.EqualTo(1));

            // Act ②:再推一拍 —— 离场者不得复活
            driver.OnTickEdge(2);

            // Assert ②
            Assert.That(driver.ActiveBreathCount, Is.EqualTo(0));
            Assert.That(sink.BeginLoopCount, Is.EqualTo(1), "离场后不得重起呼吸层");
        }

        [Test]
        public void test_ac018_6_stopAll_endsEveryLoop()
        {
            // Arrange
            var sink = new CountingSink();
            PresenceRegistry presence = PresenceWith(7);
            PatientCueDriver driver = MakeDriver(sink, presence, 7, 0.5f, coughCue: 0, breathCue: 1);
            driver.OnTickEdge(0);

            // Act
            driver.StopAll();

            // Assert:ADR-018 §一「句柄由唯一持有者负责收尾」—— 本驱动即该持有者
            Assert.That(driver.ActiveBreathCount, Is.EqualTo(0));
            Assert.That(sink.EndLoopCount, Is.EqualTo(1));
        }

        [Test]
        public void test_ac018_6_driver_ctor_rejectsBrokenIntervals()
        {
            // Arrange:破硬约束的间隔表(0 < Severe < Mild ≤ Recover)
            var broken = new CueIntervals(severe: 90, mild: 40, recover: 40);

            // Act + Assert:构造期硬校验(与 PatientBehaviorDirector 破表即抛同口径)
            Assert.Throws<ArgumentException>(() => new PatientCueDriver(
                new CountingSink(), new FakeVitals(0.5f), PresenceWith(7),
                new PatientBehaviorDirector(new FakeVitals(0.5f), BehaviorBands.Default),
                intervals: broken, coughCue: 0, breathCue: 1,
                knowsClinic: KnowsClinicForTest));
        }

        [Test]
        public void test_ac018_6_driver_rejectsNegativeTick()
        {
            // Arrange
            PatientCueDriver driver = MakeDriver(new CountingSink(), PresenceWith(7), 7, 0.5f, 0, 1);

            // Act + Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => driver.OnTickEdge(-1));
        }

        // ══════════════════════════════════════════════════════════════════
        // AC-018-7 —— 处方音效白名单验证(TD-audio-024)
        // 判别力:把某处方 cue 改成状态播报类 ⇒ 断言转红。
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_ac018_7_prescriptionCues_categoryInWhitelist_andNoStateAnnouncement()
        {
            // Arrange:真烘焙(源 → 绑定 + 门)
            IReadOnlyList<AudioEventTableRow> rows =
                AudioEventTableBake.BakeFromRepo(RepoRoot()).Bound.Table.Rows;
            var byName = new Dictionary<string, AudioEventTableRow>(StringComparer.Ordinal);
            for (int i = 0; i < rows.Count; i++) byName[rows[i].Cue] = rows[i];

            // Act + Assert:逐条处方 cue
            foreach (string cue in PrescriptionCues)
            {
                Assert.That(byName.ContainsKey(cue), Is.True,
                    $"处方 cue「{cue}」不在音频事件表内 —— TD-audio-024 的断言对象消失(禁借绿)");
                AudioEventTableRow row = byName[cue];

                // ① 内容侧类别 ∈ 白名单(GDD 五类)
                Assert.That(AudioEventTableGates.WhitelistCategories, Does.Contain(row.WhitelistCategory),
                    $"处方 cue「{cue}」的 whitelist_category「{row.WhitelistCategory}」∉ 白名单");

                // ② 时机侧 ∉ 状态播报禁集(AC-44-09 无提示音铁律:零 sting / jingle)
                Assert.That(AudioEventTableGates.ForbiddenTriggerSources,
                    Does.Not.Contain(row.TriggerSource),
                    $"处方 cue「{cue}」的 trigger_source「{row.TriggerSource}」是状态播报 —— " +
                    "AC-44-09 禁「以音报状态」(sting / jingle / ducking / 素材切换)");
            }
        }

        [Test]
        public void test_ac018_7_wholeTable_noForbiddenTriggerSource()
        {
            // Arrange
            IReadOnlyList<AudioEventTableRow> rows =
                AudioEventTableBake.BakeFromRepo(RepoRoot()).Bound.Table.Rows;
            Assert.That(rows.Count, Is.GreaterThan(0), "空表夹具 ⇒ 断言恒真(禁借绿)");

            // Act
            var offenders = new List<string>();
            foreach (AudioEventTableRow row in rows)
                if (AudioEventTableGates.ForbiddenTriggerSources.Contains(row.TriggerSource))
                    offenders.Add($"{row.Cue}:{row.TriggerSource}");

            // Assert:全表零状态播报(白名单的**运行期侧**复验 —— 构建期门已判一次,
            //  本断言钉的是「进到运行期的那份表」与门读的是**同一份数据」)
            Assert.That(offenders, Is.Empty,
                "AC-44-09:音频不得用于报状态 —— 命中:" + string.Join(", ", offenders));
        }

        [Test]
        public void test_ac018_7_prescriptionCues_areNotLoops()
        {
            // Arrange:一次性动作音(翻页 / 取物)若被写成 loop ⇒ 停不下来的环境音
            IReadOnlyList<AudioEventTableRow> rows =
                AudioEventTableBake.BakeFromRepo(RepoRoot()).Bound.Table.Rows;
            var byName = new Dictionary<string, AudioEventTableRow>(StringComparer.Ordinal);
            for (int i = 0; i < rows.Count; i++) byName[rows[i].Cue] = rows[i];

            // Act + Assert
            foreach (string cue in PrescriptionCues)
                Assert.That(byName[cue].Loop, Is.False,
                    $"处方 cue「{cue}」被标为 loop —— 一次性动作音不得起循环");
        }

        // ══════════════════════════════════════════════════════════════════
        // AC-019-3…5 —— story-019 面 10-b:无突变音效 + 停顿/停止可分 + 尾音衰减
        // 判别力:每条注明「停用什么 ⇒ 哪条红」(证伪记录登记在 impl-s19-*.md)。
        // ⚠️ 走**真** PatientCueDriver + 真 PatientCueSchedule + 真 BreathTailRamp;
        //    注入的假件仅 `IVitalsQuery`(9 的取数面,可编程轨迹)与 `IAudioCueSink`(观测量壳)。
        // ══════════════════════════════════════════════════════════════════

        /// <summary>可编程体征轨迹假件 —— **只**在取数面替换 9 的查询(生产缝已声明)。
        /// <para>⚠️ 轨迹是**题面**(被测数据),不是谓词:行为判定仍全归 13 的真 `BehaviorMap`。</para></summary>
        private sealed class ScriptedVitals : IVitalsQuery
        {
            public float Position;
            public float Trend;
            public ScriptedVitals(float position, float trend = 0f) { Position = position; Trend = trend; }
            public VitalsDto GetVitals(PatientId p) => new VitalsDto(Position, Trend, 0, 0);
        }

        /// <summary>增益消息观测壳 —— 记下每次 `Emit` 的 Intensity(尾音 = 只写增益,不发音效)。</summary>
        private sealed class TailSink : IAudioCueSink
        {
            public readonly List<int> Intensities = new List<int>();

            /// <summary>全部 <c>Emit</c> 的原始 DTO(判「停顿期只写增益、不出声」用)。</summary>
            public readonly List<AudioCueDto> Emitted = new List<AudioCueDto>();
            public int BeginLoopCount;
            public int EndLoopCount;
            public int LastLoopCue = AudioEventDataSet.NoOrdinal;

            public void Emit(in AudioCueDto cue)
            {
                Intensities.Add(cue.Intensity);
                Emitted.Add(cue);
            }
            public AudioCueHandle BeginLoop(in AudioCueDto cue)
            {
                BeginLoopCount++;
                LastLoopCue = cue.Cue;
                return new AudioCueHandle(1000 + BeginLoopCount);
            }
            public void EndLoop(AudioCueHandle handle) => EndLoopCount++;
            public void SetTier(TierSource source) { }
        }

        /// <summary>造「在场 + 可编程体征」的驱动器。</summary>
        private static PatientCueDriver MakeScriptedDriver(IAudioCueSink sink, PresenceRegistry presence,
                                                           ScriptedVitals vitals, int patientId,
                                                           int? coughCue, int? breathCue)
            => new PatientCueDriver(
                sink, vitals, presence, new PatientBehaviorDirector(vitals, BehaviorBands.Default),
                intervals: CueIntervals.Default,
                coughCue: coughCue, breathCue: breathCue,
                knowsClinic: KnowsClinicForTest);

        /// <summary>story-021 起的**必填**注入缝:本夹具只测 cue 调度 / 尾音,
        /// `KnowsClinic` 取恒假(病人停在 Idle —— 与 story-021 前硬编码 `false` 的行为等价,
        /// 不影响本夹具的任何判据)。缝本身的判别力归 `knowsclinic_seam_test`。</summary>
        private static bool KnowsClinicForTest(DaYiJingCheng.Sim.Contracts.PatientId _) => false;

        /// <summary>推进 [from, to) 的 tick 边沿,每拍按给定脚本改体征。</summary>
        private static void RunTicks(PatientCueDriver driver, ScriptedVitals vitals, int from, int to,
                                     Func<int, float> positionAt)
        {
            for (int tick = from; tick < to; tick++)
            {
                vitals.Position = positionAt(tick);
                driver.OnTickEdge(tick);
            }
        }

        [Test]
        public void test_ac019_3_oneShotIntensity_followsVitals_neverFixedVolume()
        {
            // 判据(本 story 的「无突变音效」面):一次性 cue 的强度 = 该拍体征的**连续函数**
            //   (position × 255),不得被换成与体征脱钩的固定值(= 提示音形态)。
            //   ⚠️ 一次性 cue 的**调度**仍归 13 —— 平稳档定期发咳嗽是 13 的既有裁决,不在本测判面。
            var sink = new TailSink();
            PresenceRegistry presence = PresenceWith(patientId: 9);
            var vitals = new ScriptedVitals(0.2f);
            PatientCueDriver driver = MakeScriptedDriver(sink, presence, vitals, 9, coughCue: 0, breathCue: 1);

            var expected = new List<int>();
            // 全程缓升(**含前 200 拍**):若前段恒值,「固定音强」变异在该段上与真值不可分 ⇒ 夹具漏判
            //   (2026-10-18 证伪轮实测:首版前 200 拍固定 0.2,把 Intensity 改成常量后本测仍绿)。
            for (int tick = 0; tick < 400; tick++)
            {
                float p = 0.2f + tick * 0.001f;
                vitals.Position = p;
                driver.OnTickEdge(tick);
                expected.Add(PatientCue.Intensity(p));
            }

            Assert.That(driver.ActiveBreathCount, Is.EqualTo(1), "前置:呼吸 loop 已起,否则本测是空跑");
            Assert.That(sink.BeginLoopCount, Is.EqualTo(1));
            // 只看**一次性** cue(尾音增益同走 Emit —— 混进集合会掩盖「固定音强」变异)
            var oneShot = new List<int>();
            foreach (AudioCueDto c in sink.Emitted)
                if (!c.Looped) oneShot.Add(c.Intensity);
            Assert.That(oneShot.Count, Is.EqualTo(driver.EmitCount));
            Assert.That(oneShot.Count, Is.GreaterThan(0),
                "前置:过渡期确有一次性 cue 发生 —— 否则『强度连续』无从判别(假绿)");
            for (int i = 0; i < oneShot.Count; i++)
                Assert.That(expected, Does.Contain(oneShot[i]),
                    $"第 {i} 条一次性 cue 强度 = {oneShot[i]},不在任意拍体征派生值集合内 —— " +
                    "出现与体征脱钩的固定音强(突变音效形态,AC-019-3 / AC-44-10)");
        }

        /// <summary>AC-019-3 主判据:平稳 → 急促 → 停顿的轨迹上,**过渡本身零新增一次性 cue**
        /// (13 的既有咳嗽调度照旧;新增的只有呼吸 loop 的参数连续变化)。</summary>
        [Test]
        public void test_ac019_3_transitionToPause_addsNoNewOneShotCue()
        {
            // Arrange
            var sink = new TailSink();
            PresenceRegistry presence = PresenceWith(patientId: 21);
            var vitals = new ScriptedVitals(0.5f);
            PatientCueDriver driver = MakeScriptedDriver(sink, presence, vitals, 21, coughCue: 0, breathCue: 1);

            // Act:平稳 200 拍 → 急促 40 拍(缓升,仍活着)→ 终局 40 拍
            RunTicks(driver, vitals, 0, 200, _ => 0.5f);
            int afterSteady = driver.EmitCount;
            Assert.That(afterSteady, Is.GreaterThan(0),
                "前置:平稳期确有既有一次性 cue —— 否则『过渡不新增』无从判别(假绿)");

            RunTicks(driver, vitals, 200, 240, t => 0.5f + (t - 200) * 0.005f);
            int afterRapid = driver.EmitCount;
            RunTicks(driver, vitals, 240, 280, _ => BehaviorBands.Default.DeathBandMin);
            int afterTerminal = driver.EmitCount;

            // Assert ①:急促段新增的 cue 仍走 13 的既有调度(不设上界,只要求它没被「过渡」另开一路)
            Assert.That(afterRapid - afterSteady, Is.GreaterThanOrEqualTo(0));
            // Assert ②:进入终局(停顿)后**零新增** —— 阈值跨越没有被映射成离散音效
            Assert.That(afterTerminal, Is.EqualTo(afterRapid),
                $"终局之后又发了 {afterTerminal - afterRapid} 条一次性 cue —— 阈值跨越被映射成离散音效(AC-44-10)");
            // Assert ③:呼吸 loop 全程存续(过渡只改参数,不换声源)
            Assert.That(driver.ActiveBreathCount, Is.EqualTo(1));
            Assert.That(sink.BeginLoopCount, Is.EqualTo(1), "过渡不得重起 loop(重起 = 可听辨的『换了个声音』)");
        }

        [Test]
        public void test_ac019_4_pauseKeepsBreathAndStopEndsIt()
        {
            // Arrange:平稳 → 转危(急促)→ 停顿(死亡侧终局锁存)→ 停止(离场)
            var sink = new TailSink();
            PresenceRegistry presence = PresenceWith(patientId: 11);
            var vitals = new ScriptedVitals(0.5f);
            // ⚠️ `coughCue` 必须非 null —— 否则「停顿期零一次性 cue」恒真(2026-10-18 证伪轮实测假绿)。
            PatientCueDriver driver = MakeScriptedDriver(sink, presence, vitals, 11, coughCue: 0, breathCue: 1);

            // 平稳期跑足 200 拍(10 s):13 的既有咳嗽调度在其中至少发过一次 ⇒ 「过渡零新增」有判别力
            RunTicks(driver, vitals, 0, 200, _ => 0.5f);
            Assert.That(driver.ActiveBreathCount, Is.EqualTo(1), "前置:呼吸 loop 已起");
            Assert.That(driver.EmitCount, Is.GreaterThan(0),
                "前置:平稳期 13 的既有调度确实发过一次性 cue —— 否则『过渡不新增 cue』无从判别");

            int emitsBeforePause = sink.Intensities.Count;
            int oneShotsBeforeTerminal = driver.EmitCount;
            RunTicks(driver, vitals, 200, 230, _ => BehaviorBands.Default.DeathBandMin);

            // AC-019-3 主判据:进入终局(过渡)之后**零新增**一次性 cue
            Assert.That(driver.EmitCount, Is.EqualTo(oneShotsBeforeTerminal),
                "过渡(平稳 → 终局)之后又发了一次性 cue —— 阈值跨越被映射成离散音效(AC-44-10)");

            Assert.That(driver.ActiveBreathCount, Is.EqualTo(1),
                "停顿(昏迷/终局在场)不得停呼吸 loop —— 停了就与『停止』不可分");
            Assert.That(sink.EndLoopCount, Is.Zero);
            Assert.That(driver.PausedBreathCount, Is.EqualTo(1), "应进入停顿态簿记");
            Assert.That(sink.Intensities.Count, Is.GreaterThan(emitsBeforePause),
                "停顿期必须有增益衰减写入 —— 否则玩家听不到『极轻气流尾』");
            Assert.That(driver.EmitCount, Is.EqualTo(oneShotsBeforeTerminal),
                "停顿期不得出一声性 cue(无临终一声)");

            // 停顿期的写入必须是**尾音增益**(从满幅收敛到 -36 dB 底),不是「又咳了一声」的即时强度:
            //   ⇒ 形态判据 = 相对首步的比值 ≈ (floor)^(k/n),与体征即时强度(position×255)差异可达数量级。
            int pauseEmits = sink.Intensities.Count - emitsBeforePause;
            Assert.That(pauseEmits, Is.GreaterThanOrEqualTo(3), "停顿期写入太少 ⇒ 曲线无从判别");
            double floor = BreathTailRamp.DbToLinear(AudioTuning.BreathTailGainFloorDb) * 255.0;
            Assert.That(sink.Intensities[sink.Intensities.Count - 1], Is.LessThan((int)(255 * 0.75)),
                "停顿段末尾强度仍在满幅附近 —— 未走尾音衰减");
            Assert.That(sink.Intensities[sink.Intensities.Count - 1], Is.GreaterThanOrEqualTo((int)floor - 1),
                "停顿段强度低于尾音底值 —— 不是 BreathTailRamp.TargetVolume 给的曲线");

            int emitsBeforeStop = sink.Intensities.Count;
            presence.RemovePresent(new PatientId(11));
            driver.PruneAbsent(230);

            Assert.That(sink.EndLoopCount, Is.EqualTo(1), "停止 = EndLoop");
            Assert.That(driver.ActiveBreathCount, Is.Zero);
            Assert.That(driver.PausedBreathCount, Is.Zero, "停止后不得残留尾音簿记");
            Assert.That(sink.Intensities.Count, Is.EqualTo(emitsBeforeStop),
                "停止后不得再有增益写入 —— GDD:571-573「停止 = 完全无声」");
        }

        [Test]
        public void test_ac019_4_pauseLifespan_isBoundedByTailRamp()
        {
            // 停顿态的**寿命**须由 AudioTuning 的尾音时长定;尾音到位后 loop 仍存续。
            //   ⚠️ 必须先平稳一拍把 loop 立起来 —— 若首拍就终局,13 给 `None`(不 Begin),无 loop 可谈停顿。
            var sink = new TailSink();
            PresenceRegistry presence = PresenceWith(patientId: 17);
            var vitals = new ScriptedVitals(0.5f);
            PatientCueDriver driver = MakeScriptedDriver(sink, presence, vitals, 17, coughCue: null, breathCue: 1);
            RunTicks(driver, vitals, 0, 1, _ => 0.5f);
            Assert.That(driver.ActiveBreathCount, Is.EqualTo(1), "前置:呼吸 loop 已起");

            int ticksPerTail = (int)Math.Ceiling(AudioTuning.BreathTailRampSeconds / PatientCueDriver.TickSeconds);

            // ⚠️ `TryGetBreathTail` 读的是**当下簿记**,不是历史 ⇒ 必须在推进过程中逐拍采样,
            //    否则事后回查只会拿到最后一段的起点(本节首版即因此把判据读成 0)。
            int probeEnd = ticksPerTail + 4;
            var has = new bool[probeEnd + 1];
            var elapsed = new double[probeEnd + 1];
            var gains = new int[probeEnd + 1];
            for (int tick = 1; tick <= probeEnd; tick++)
            {
                vitals.Position = BehaviorBands.Default.DeathBandMin;
                driver.OnTickEdge(tick);
                has[tick] = driver.TryGetBreathTail(17, tick, out double e);
                elapsed[tick] = e;
                gains[tick] = driver.BreathGainUpdateCount;
            }

            // 段起点由首个终局拍的 elapsed 反推(不硬编码相位)
            Assert.That(has[1], Is.True, "终局拍未开停顿段 —— 停顿态根本不存在");
            int segStart = 1 - (int)Math.Round(elapsed[1] / PatientCueDriver.TickSeconds);
            int due = segStart + ticksPerTail;

            // Assert ① 段内推进:到期前一拍已接近尾音时长(不是停在 0,也不是超过)
            Assert.That(has[due - 1], Is.True, "到期前一拍簿记缺失");
            Assert.That(elapsed[due - 1], Is.GreaterThan(AudioTuning.BreathTailRampSeconds * 0.9),
                $"到期前一拍推进 {elapsed[due - 1]}s,未接近尾音时长 {AudioTuning.BreathTailRampSeconds}s");
            Assert.That(elapsed[due - 1], Is.LessThanOrEqualTo(AudioTuning.BreathTailRampSeconds + 1e-12));

            // Assert ② 到期拍停表(停表 ≠ 停止):簿记清空,loop 仍存续
            Assert.That(has[due], Is.False, "到期拍簿记未清 —— 尾音永不停表");
            Assert.That(driver.ActiveBreathCount, Is.EqualTo(1), "尾音期间 loop 仍存续(停顿 ≠ 停止)");
            Assert.That(driver.EmitCount, Is.Zero, "停顿期不得出一声性 cue");

            // Assert ③ **尾音只衰减一次**(用户 2026-10-18 裁定,取代旧「到期后重开新段」语义):
            //   13 在终局锁存期每拍都给 `End`,实现须挡住重开 —— 到期后簿记恒缺失。
            //   ⚠️ 旧断言在此处要求 `has[due+1] = true` 且 elapsed 从 0 起算 —— 那正是「一阵一阵」的
            //      形态在测试侧的固化。实测坐实后删除;反向判据(停用挡重开的守卫 ⇒ 本条红)见证伪轮。
            Assert.That(driver.IsBreathTailDone(17), Is.True, "到期后未标记「尾音已完成」");
            for (int tick = due + 1; tick <= probeEnd; tick++)
                Assert.That(has[tick], Is.False,
                    $"第 {tick} 拍簿记又出现 —— 尾音重开了新段(一阵一阵);尾音只应衰减一次");
            Assert.That(driver.PausedBreathCount, Is.Zero, "到期后停顿簿记须恒零(不重开)");
            Assert.That(driver.EmitCount, Is.Zero, "尾音全程不得出一声性 cue");

            // Assert ④ 全生**只有一段**:增益写入步数 = 段内步数,到期后不再增长(段间不累计)
            Assert.That(gains[due] - gains[due - 1], Is.EqualTo(1), "到期拍应写最后一次增益(封口值)");
            Assert.That(gains[probeEnd], Is.EqualTo(gains[due]),
                $"到期后又写了 {gains[probeEnd] - gains[due]} 次增益 —— 尾音重复写(重开段)");
        }

        [Test]
        public void test_ac019_6_breathTail_decaysOnce_neverRestarts_whileTerminalPersists()
        {
            // 回归测(2026-10-18「尾音只衰减一次」修复):病人在终局锁存态持续在场时,
            // 尾音**全生只衰减一遍**;衰减完 = 停在底值,不再从满幅重来。
            // ⚠️ 前置断言(本测的判别力来源):13 在终局锁存期**每拍**都给 `End`。
            //    若 13 改了这条语义,前置会红 —— 那时「挡重开」的守卫成了空转,须重裁本测。
            var sink = new TailSink();
            PresenceRegistry presence = PresenceWith(patientId: 19);
            var vitals = new ScriptedVitals(0.5f);
            var behavior = new PatientBehaviorDirector(vitals, BehaviorBands.Default);
            PatientCueDriver driver = new PatientCueDriver(
                sink, vitals, presence, behavior,
                intervals: CueIntervals.Default,
                coughCue: null, breathCue: 1,
                knowsClinic: KnowsClinicForTest);
            RunTicks(driver, vitals, 0, 5, _ => 0.5f);
            Assert.That(driver.ActiveBreathCount, Is.EqualTo(1), "前置:呼吸 loop 已起");

            // 前置:13 的决策表在终局锁存期确实每拍 `End`
            RunTicks(driver, vitals, 5, 40, _ => BehaviorBands.Default.DeathBandMin);
            PatientBehavior b = behavior.For(new PatientId(19));
            CueDispatch d1 = PatientCueSchedule.Decide(
                b.Current, b.Terminal, 40, -1, new PatientId(19),
                b.Tier, vitals.Position, CueIntervals.Default, breathAlive: true, CueKind.Cough, 0);
            CueDispatch d2 = PatientCueSchedule.Decide(
                b.Current, b.Terminal, 41, -1, new PatientId(19),
                b.Tier, vitals.Position, CueIntervals.Default, breathAlive: true, CueKind.Cough, 0);
            Assert.That(b.Terminal.Latched, Is.True, "前置:13 已终局锁存");
            Assert.That(d1.Breath, Is.EqualTo(BreathAction.End), "前置:第 40 拍 13 判 End");
            Assert.That(d2.Breath, Is.EqualTo(BreathAction.End), "前置:第 41 拍 13 仍判 End(每拍重复)");

            // 跑足 3 倍尾音时长 —— 若实现会重开段,这里必然出现多段
            int ticksPerTail = (int)Math.Ceiling(AudioTuning.BreathTailRampSeconds / PatientCueDriver.TickSeconds);
            int emitsAtTailEnd = sink.Intensities.Count;
            int gainsAtTailEnd = driver.BreathGainUpdateCount;
            RunTicks(driver, vitals, 40, 40 + 3 * ticksPerTail, _ => BehaviorBands.Default.DeathBandMin);

            Assert.That(driver.BreathGainUpdateCount - gainsAtTailEnd, Is.Zero,
                $"尾音到期后又写了 {driver.BreathGainUpdateCount - gainsAtTailEnd} 次增益 —— 尾音重开了");
            Assert.That(driver.PausedBreathCount, Is.Zero, "尾音到期后停顿簿记须恒零");
            Assert.That(driver.IsBreathTailDone(19), Is.True);
            Assert.That(driver.ActiveBreathCount, Is.EqualTo(1), "尾音完 ≠ 停止:loop 仍存续");
            Assert.That(sink.EndLoopCount, Is.Zero, "尾音完 ≠ 停止:不得 EndLoop");
            Assert.That(driver.EmitCount, Is.Zero, "停顿期不得出一声性 cue");

            // 形态:尾音段**单调不升**,末值 = 底值(一段,不是一个周期函数)
            var seg = sink.Intensities.GetRange(emitsAtTailEnd - ticksPerTail, ticksPerTail);
            for (int i = 1; i < seg.Count; i++)
                Assert.That(seg[i], Is.LessThanOrEqualTo(seg[i - 1]),
                    $"尾音第 {i} 步回升 —— 段内出现重开(一阵一阵)");
            double floor = BreathTailRamp.DbToLinear(AudioTuning.BreathTailGainFloorDb) * 255.0;
            Assert.That(seg[seg.Count - 1], Is.InRange((int)floor - 1, (int)floor + 1),
                "尾音封口值 ≠ 底值");

            // 停止(离场)⇒ 清簿记;之后同 id 再出现走全新一生(只衰减一次是「每生一次」,不是「全局一次」)
            int endTick = 40 + 3 * ticksPerTail;
            presence.RemovePresent(new PatientId(19));
            driver.PruneAbsent(endTick);
            Assert.That(driver.IsBreathTailDone(19), Is.False, "停止后封口态不得残留");
            Assert.That(sink.EndLoopCount, Is.EqualTo(1), "停止 = EndLoop");

            // 「再出现」:同 id 重新入场 ⇒ 必须能起新 loop + 开新尾音段(封口态若随「停止」存活,
            //   这里 guard 会挡住重开 ⇒ 无增益写入;见证伪轮 F2)。
            // ⚠️ 14 拍不足以跳出 13 的滞回带 —— 等 `ResetForLoad` 清掉该病人的陈旧行为态(= 读档语义),
            //    再验证「新一生」;这同时钉住「封口态的清理与 13 的派生态清理同口径」。
            int emitsAfterStop = sink.Intensities.Count;
            presence.AddPresent(new PatientId(19), new WorldPos(1, 0, 2));
            behavior.ResetForLoad();
            RunTicks(driver, vitals, endTick + 1, endTick + 6, _ => 0.5f);
            Assert.That(driver.ActiveBreathCount, Is.EqualTo(1), "再出现后未起新 loop");
            RunTicks(driver, vitals, endTick + 6, endTick + 10, _ => BehaviorBands.Default.DeathBandMin);
            Assert.That(driver.BreathGainUpdateCount - gainsAtTailEnd, Is.GreaterThan(0),
                "再出现后零增益写入 —— 尾音段没开");
            Assert.That(sink.Intensities.Count, Is.GreaterThan(emitsAfterStop),
                "再出现后尾音未重开 —— 封口态跨「停止」存活了");
            Assert.That(driver.PausedBreathCount, Is.GreaterThan(0), "再出现后的尾音段须入簿记");
        }

        [Test]
        public void test_ac019_4_stop_leavesNoTail_andEndsLoop()
        {
            var sink = new TailSink();
            PresenceRegistry presence = PresenceWith(patientId: 13);
            var vitals = new ScriptedVitals(0.5f);
            PatientCueDriver driver = MakeScriptedDriver(sink, presence, vitals, 13, coughCue: null, breathCue: 1);

            RunTicks(driver, vitals, 0, 5, _ => 0.5f);
            Assert.That(driver.ActiveBreathCount, Is.EqualTo(1), "前置:呼吸 loop 已起");
            int emitsBefore = sink.Intensities.Count;

            presence.RemovePresent(new PatientId(13));
            driver.PruneAbsent(5);
            driver.OnTickEdge(6);

            Assert.That(sink.EndLoopCount, Is.EqualTo(1), "停止 = EndLoop,且只一次");
            Assert.That(driver.ActiveBreathCount, Is.Zero);
            Assert.That(driver.PausedBreathCount, Is.Zero, "停止后不得残留尾音簿记");
            Assert.That(sink.Intensities.Count, Is.EqualTo(emitsBefore),
                "停止后不得再有增益写入 —— 死亡不播尾音(GDD:571-573「停止 = 完全无声」)");
        }

        [Test]
        public void test_ac019_4_stopAll_clearsTailBookkeeping()
        {
            var sink = new TailSink();
            PresenceRegistry presence = PresenceWith(patientId: 15);
            var vitals = new ScriptedVitals(0.5f);
            PatientCueDriver driver = MakeScriptedDriver(sink, presence, vitals, 15, coughCue: null, breathCue: 1);
            RunTicks(driver, vitals, 0, 5, _ => 0.5f);

            // 前置:先走出一个**已封口**的尾音(否则 `_tailDone` 为空,「清没清」无从判 —— 见证伪轮 F4)
            int ticksPerTail = (int)Math.Ceiling(AudioTuning.BreathTailRampSeconds / PatientCueDriver.TickSeconds);
            RunTicks(driver, vitals, 5, 5 + ticksPerTail + 1, _ => BehaviorBands.Default.DeathBandMin);
            Assert.That(driver.IsBreathTailDone(15), Is.True, "前置:尾音已封口");

            driver.StopAll();

            Assert.That(sink.EndLoopCount, Is.EqualTo(sink.BeginLoopCount));
            Assert.That(driver.ActiveBreathCount, Is.Zero);
            Assert.That(driver.PausedBreathCount, Is.Zero);
            Assert.That(driver.IsBreathTailDone(15), Is.False, "StopAll 后封口态须清(读档 = 新一生)");
        }

        [Test]
        public void test_ac019_5_tailRamp_isMonotone_andMultiplicativelyUniform()
        {
            BreathTailRamp.State s = BreathTailRamp.Begin(fromVolume: 1.0);
            const double dt = 0.05;
            var values = new List<double>();
            while (s.Active && values.Count < 1000) values.Add(BreathTailRamp.Step(ref s, dt));

            Assert.That(values.Count, Is.GreaterThan(2), "尾音步数过少 ⇒ 断言不可判");
            for (int i = 1; i < values.Count; i++)
                Assert.That(values[i], Is.LessThanOrEqualTo(values[i - 1] + 1e-15),
                    $"尾音增益在第 {i} 步回升 —— 衰减序列必须单调");

            double ratio = values[1] / values[0];
            for (int i = 2; i < values.Count; i++)
                Assert.That(values[i] / values[i - 1], Is.EqualTo(ratio).Within(1e-9),
                    $"第 {i} 步倍率与首步不一致 —— 出现拐点(可听辨的『换了个声音』= 变相报状态)");

            Assert.That(values[values.Count - 1],
                Is.EqualTo(BreathTailRamp.DbToLinear(AudioTuning.BreathTailGainFloorDb)).Within(1e-12));
        }

        [Test]
        public void test_ac019_5_degenerateInputs_neverProduceBurst()
        {
            BreathTailRamp.State zero = BreathTailRamp.Begin(0.0);
            BreathTailRamp.State zeroTime = BreathTailRamp.Begin(1.0, durationSeconds: 0.0, floorDb: -36.0);
            BreathTailRamp.State nan = BreathTailRamp.Begin(double.NaN);

            Assert.That(zero.Active, Is.False);
            Assert.That(zeroTime.Active, Is.False);
            Assert.That(nan.Active, Is.False);
        }

        /// <summary>AC-019-5「零新 clip」:过渡路径**不引入任何新素材名** ——
        /// 三行呼吸 cue(基础层 / 附加层 / 世界语境)的 <c>assets[]</c> 各恰 1 条,且全表别的行
        /// 不引用它们(过渡 = 既有 loop 的增益衰减,**没有**「停顿专用 wav」「死亡专用 wav」)。
        /// <para>素材文件本体存在性归 D6 素材门;本条只钉「没有新 clip」这一半边。</para></summary>
        [Test]
        public void test_ac019_5_breathRows_introduceNoNewClipName()
        {
            // Arrange
            IReadOnlyList<AudioEventTableRow> rows =
                AudioEventTableBake.BakeFromRepo(RepoRoot()).Bound.Table.Rows;
            Assert.That(rows.Count, Is.GreaterThan(0), "空表夹具 ⇒ 断言恒真(禁借绿)");
            var byName = new Dictionary<string, AudioEventTableRow>(StringComparer.Ordinal);
            for (int i = 0; i < rows.Count; i++) byName[rows[i].Cue] = rows[i];

            string[] breathCues = { "BreathLayer_Base_Calm", "BreathLayer_Adventitious_Fine", "WorldBreath_Baseline" };
            var breathAssets = new Dictionary<string, string>(StringComparer.Ordinal);

            // Act + Assert ①:每行恰 1 条素材(多一条 = 该行可用素材切换「报」过渡 = 变相提示音)
            foreach (string cue in breathCues)
            {
                Assert.That(byName.ContainsKey(cue), Is.True, $"呼吸 cue「{cue}」不在表内");
                IReadOnlyList<string> assets = byName[cue].Assets;
                Assert.That(assets, Is.Not.Null, $"呼吸 cue「{cue}」缺 assets 字段");
                Assert.That(assets.Count, Is.EqualTo(1),
                    $"呼吸 cue「{cue}」素材数 {assets.Count} ≠ 1 —— 多素材 = 可由素材切换报状态(AC-44-10)");
                breathAssets[cue] = assets[0];
            }

            // Assert ②:呼吸素材名不再出现在任何**非呼吸**行上(过渡没有自己的 wav)
            foreach (AudioEventTableRow row in rows)
            {
                if (Array.IndexOf(breathCues, row.Cue) >= 0 || row.Assets == null) continue;
                foreach (string asset in breathAssets.Values)
                    Assert.That(row.Assets, Does.Not.Contain(asset),
                        $"非呼吸行「{row.Cue}」复用了呼吸素材「{asset}」");
            }
        }
    }
}
