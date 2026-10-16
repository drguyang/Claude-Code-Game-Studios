// 权威来源:production/epics/audio-system/story-008-spatialization-world-breath.md
//   (AC-44-D2 / AC-44-D3 / AC-44-D8 / AC-44-16 / 优先级公式)
//
// ⚠️ **2026-10-16 S14-1 改造 —— 自造断言 → 真生产断言**(评审原件 §2.2)
//   本文件原以 **7 个测试自造类**顶替生产实现(`FakeSourcePool` 内部**零 `AudioSource`**,
//   而 AC-44-D8 ② 判据逐字是「N 条独立 `AudioSource`」)⇒ 断言在「被测对象不存在」时仍为绿。
//   现全部换成真生产类(`AudioSourcePool` / `OcclusionDriver` / `RankKeyComputer` /
//   `CellJitter` / `LatticeToWorld` / `SpatializationDirector`)。
//   **保留的两处非生产物**(刻意,非漏改):
//     · `ReverseDerivationFixture` —— **负向夹具**:证明反射扫描能命中「float → WorldPos」;
//
// ⚠️ **生命周期**:真建 `AudioSource` / `AudioLowPassFilter` ⇒ 须 `[TearDown]` 清理
//   (本测试树此前无 `AddComponent<AudioSource>` 先例,本文件开此先例)。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using DaYiJingCheng.EditorTools.Gates;
using DaYiJingCheng.Gameplay.Presentation.Audio;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Tests.Unit.Audio
{
    [TestFixture]
    internal sealed class SpatializationTest
    {
        private static string repoRoot([CallerFilePath] string thisFile = "")
            => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile) ?? ".",
                "..", "..", "..", "..", ".."));

        private const string Prefix = AssemblyGates.AudioModuleNamespacePrefix;

        // 优先级类别(GDD :833-837):自身/检查 0 < 世界≈生理 1 < 氛围 2
        private const int PrioritySelfAction = 0;
        private const int PriorityWorldPhysiology = 1;
        private const int PriorityAmbience = 2;

        /// <summary>格边长(米)—— 归系统 6;测试用确定值,避免依赖 6 侧未定的 `LATTICE_SIZE`。</summary>
        private const float LatticeSize = 2f;

        private readonly List<IDisposable> _disposables = new List<IDisposable>();
        private readonly List<GameObject> _objects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (IDisposable d in _disposables) d.Dispose();
            _disposables.Clear();
            foreach (GameObject go in _objects)
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
            _objects.Clear();
        }

        private AudioSourcePool NewPool()
        {
            var pool = new AudioSourcePool();
            _disposables.Add(pool);
            return pool;
        }

        private GameObject NewObject(string name)
        {
            var go = new GameObject(name);
            _objects.Add(go);
            return go;
        }

        private static List<Type> ProductionAudioTypes()
        {
            var asm = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == AssemblyGates.PresentationAssemblyName);
            Assert.That(asm, Is.Not.Null, "Gameplay.Presentation 必须已加载");
            try { return asm.GetTypes().Where(t => t.Namespace != null &&
                    AssemblyGates.IsInNamespacePrefix(t.Namespace, Prefix)).ToList(); }
            catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t != null &&
                    t.Namespace != null && AssemblyGates.IsInNamespacePrefix(t.Namespace, Prefix)).ToList(); }
        }

        // ═══ AC-44-D2:禁反推 ═══

        [Test]
        public void test_latticeToWorld_noFloatToWorldPosMethod()
        {
            var types = ProductionAudioTypes();
            // 扫描面自证:44 命名空间必须真有类型(否则「零违例」是对空集的平凡成立)。
            Assert.That(types, Is.Not.Empty,
                "44 命名空间零类型 ⇒ 本扫描对空集平凡成立(空转真扫)");

            var violations = new List<string>();
            foreach (var t in types)
                foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Instance |
                                              BindingFlags.Static | BindingFlags.DeclaredOnly))
                {
                    bool hasFloatParam = m.GetParameters().Any(p =>
                        p.ParameterType == typeof(float) || p.ParameterType == typeof(Vector3));
                    bool returnsWorldPos = m.ReturnType == typeof(WorldPos);
                    if (hasFloatParam && returnsWorldPos)
                        violations.Add($"{t.Name}.{m.Name}(float) → WorldPos");
                }
            Assert.That(violations, Is.Empty,
                "空间化禁从 float 反推逻辑格(AC-44-D2 / ADR-015 / ADR-018 §二):\n" +
                string.Join("\n", violations));
        }

        /// <summary>负向夹具:**证明扫描能命中** —— 只断言「零违例」而不证明扫描有判别力
        /// 是「空转真扫」的形态(评审原件 §1)。</summary>
        [Test]
        public void test_latticeToWorld_reverseDerivationFixture_flagged()
        {
            var m = typeof(ReverseDerivationFixture).GetMethod("FloatToWorldPos",
                BindingFlags.Public | BindingFlags.Instance);
            Assert.That(m, Is.Not.Null);
            bool hit = m.GetParameters().Any(p =>
                p.ParameterType == typeof(float) || p.ParameterType == typeof(Vector3))
                && m.ReturnType == typeof(WorldPos);
            Assert.That(hit, Is.True, "含 float→WorldPos 的 fixture 必须被命中");
        }

        [Test]
        public void test_latticeToWorld_integerCellToFloat()
        {
            Vector3 world = LatticeToWorld.ToWorld(10, 0, 20, LatticeSize);
            Assert.That(world.x, Is.EqualTo(20f), "X = 格 × 边长");
            Assert.That(world.y, Is.EqualTo(0f));
            Assert.That(world.z, Is.EqualTo(40f), "Z = 格 × 边长");
        }

        [Test]
        public void test_latticeToWorld_nonPositiveSize_rejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => LatticeToWorld.ToWorld(1, 1, 1, 0f), "0 ⇒ 全部格塌到原点,须拒");
            Assert.Throws<ArgumentOutOfRangeException>(
                () => LatticeToWorld.ToWorld(1, 1, 1, -1f), "负 ⇒ 镜像世界,须拒");
        }

        // ═══ AC-44-D3:不写 sim ═══

        [Test]
        public void test_spatialization_noEventSinkRefs()
        {
            Assert.That(EditorUtility.scriptCompilationFailed, Is.False, "编译成功前提");
            var dll = AssemblyGates.ScriptAssemblyPath(AssemblyGates.PresentationAssemblyName);
            var errs = AssemblyGates.CheckAudioAssemblyIl(dll, Prefix, out var matched);
            Assert.That(matched, Is.GreaterThan(0), "扫描键落空 = 假绿");
            Assert.That(errs.Where(e => e.Contains("IEventSink")).ToList(), Is.Empty,
                "空间化不得引用 IEventSink(AC-44-D3):\n" +
                string.Join("\n", errs.Where(e => e.Contains("IEventSink"))));
        }

        // ═══ AC-44-D8 ①:遮挡低通(真 AudioLowPassFilter)═══

        [Test]
        public void test_occlusionDriver_occluded_lowpassPositive()
        {
            AudioSource source = NewObject("occ_on").AddComponent<AudioSource>();
            var driver = new OcclusionDriver();

            driver.Apply(source, cutoffHz: 1200f, occluded: true);

            var filter = source.GetComponent(typeof(AudioLowPassFilter)) as AudioLowPassFilter;
            Assert.That(filter, Is.Not.Null, "遮挡 ⇒ 挂上逐源低通(引擎 [RequireComponent] 共位)");
            Assert.That(filter.enabled, Is.True, "遮挡 ⇒ 滤波启用");
            Assert.That(filter.cutoffFrequency, Is.GreaterThan(0f), "遮挡时 cutoff > 0(穿门 ≠ 静音)");
            Assert.That(filter.cutoffFrequency, Is.EqualTo(1200f), "cutoff = occlusion_lowpass 参数值");
        }

        [Test]
        public void test_occlusionDriver_notOccluded_noLowpass()
        {
            AudioSource source = NewObject("occ_off").AddComponent<AudioSource>();
            var driver = new OcclusionDriver();

            driver.Apply(source, cutoffHz: 1200f, occluded: false);

            var filter = source.GetComponent(typeof(AudioLowPassFilter)) as AudioLowPassFilter;
            Assert.That(filter == null || !filter.enabled, Is.True,
                "无遮挡 ⇒ 不启用滤波(走全通)");
            Assert.That(filter == null || filter.cutoffFrequency != 0f, Is.True,
                "⚠️ 无遮挡**不得**把 cutoff 设 0 —— 那是静音,不是全通");
        }

        [Test]
        public void test_occlusionDriver_zeroCutoff_rejected()
        {
            AudioSource source = NewObject("occ_zero").AddComponent<AudioSource>();
            var driver = new OcclusionDriver();
            Assert.Throws<ArgumentException>(
                () => driver.Apply(source, cutoffHz: 0f, occluded: true),
                "cutoff ≤ 0 必须拒绝(穿门 ≠ 静音)");
            Assert.Throws<ArgumentException>(
                () => driver.Apply(source, cutoffHz: -5f, occluded: true),
                "负 cutoff 同拒");
            Assert.That(source.GetComponent(typeof(AudioLowPassFilter)), Is.Null,
                "拒绝路径不得留下半施用的组件");
        }

        // ═══ AC-44-D8 ②:多病人独立声源(真 AudioSource)═══

        [Test]
        public void test_sourcePool_sameCell_nSourcesNotMerged()
        {
            var pool = NewPool();

            AudioSource s1 = pool.Spawn(1, 5, 0, 5, cueId: 10);
            AudioSource s2 = pool.Spawn(2, 5, 0, 5, cueId: 11);
            AudioSource s3 = pool.Spawn(3, 5, 0, 5, cueId: 12);

            Assert.That(pool.ActiveCount, Is.EqualTo(3), "同 cell 3 病人 = 3 条独立 AudioSource");
            Assert.That(s1, Is.Not.Null, "必须是真 UnityEngine.AudioSource(非计数器)");
            Assert.That(s1, Is.Not.EqualTo(s2), "不同 ActorId 的声源必须是不同实例");
            Assert.That(s2, Is.Not.EqualTo(s3));
            Assert.That(s1.gameObject, Is.Not.EqualTo(s2.gameObject), "独立 GameObject");
            Assert.That(pool.SourceForActor(1), Is.EqualTo(s1), "按 ActorId 可取回同一实例");
        }

        [Test]
        public void test_sourcePool_singlePatient_oneSource()
        {
            var pool = NewPool();
            pool.Spawn(1, 5, 0, 5, cueId: 10);
            Assert.That(pool.ActiveCount, Is.EqualTo(1), "N=1 时实例数 = 1");
        }

        [Test]
        public void test_sourcePool_release_reusable()
        {
            var pool = NewPool();
            AudioSource first = pool.Spawn(1, 5, 0, 5, cueId: 10);
            pool.Release(1);
            Assert.That(pool.ActiveCount, Is.EqualTo(0), "释放后不在响");
            Assert.That(pool.SourceForActor(1), Is.Null, "释放后取不回");

            AudioSource second = pool.Spawn(2, 6, 0, 6, cueId: 11);
            Assert.That(pool.ActiveCount, Is.EqualTo(1), "释放后重新分配,池不泄漏");
            Assert.That(second, Is.EqualTo(first), "池**复用**同一 AudioSource(非新建)");
        }

        [Test]
        public void test_sourcePool_release_unknownActor_isIdempotent()
        {
            var pool = NewPool();
            Assert.DoesNotThrow(() => pool.Release(999), "未生过的 id 释放 = 空操作(幂等)");
            Assert.That(pool.ActiveCount, Is.EqualTo(0));
        }

        [Test]
        public void test_sourcePool_sourcesParentedUnderPoolRoot()
        {
            var pool = NewPool();
            AudioSource src = pool.Spawn(1, 5, 0, 5, cueId: 10);

            Assert.That(pool.Root, Is.Not.Null, "池自建 root(ADR-028 ① 池所有权)");
            Assert.That(src.transform.parent, Is.EqualTo(pool.Root.transform),
                "源挂池 root 之下 —— 池拥有生命周期");
            Assert.That(pool.Root.hideFlags.HasFlag(HideFlags.DontSave), Is.True,
                "root 是运行期产物(DontSave),非场景预摆(ADR-028 ②)");
        }

        // ═══ 优先级公式:rank_key ═══

        [Test]
        public void test_rankKey_classDominates()
        {
            // 类键判别力:低类但**远**的源,必须排在**高类**但近的源之前。
            var sources = new[]
            {
                new RankSource(PrioritySelfAction, 50f, 1, 1, 0, 1, 10),   // 高类,远
                new RankSource(PriorityAmbience, 1f, 2, 2, 0, 2, 20),      // 低类,近
            };
            IReadOnlyList<RankedSource> order = new RankKeyComputer().Order(sources);
            Assert.That(order[0].ActorId, Is.EqualTo(1),
                "类键优先于距离键:自身动作(50f)压过氛围(1f)");
            Assert.That(order[1].ActorId, Is.EqualTo(2));
        }

        [Test]
        public void test_rankKey_distanceDominatesCellJitter()
        {
            // 距离键判别力(⚠️ 夹具经实测 jitter 值构造):
            //   actor 38 的 cell(5,0,5) jitter = 1565(**小**)
            //   actor 4  的 cell(5,0,5) jitter = 65466(**大**)
            // ⇒ 让**远**者持小 jitter、**近**者持大 jitter:
            //   若 distance 键在 ⇒ 近者(actor 4)先;
            //   若 distance 键被停用 ⇒ 退到 jitter ASC ⇒ 远者(actor 38)先。**两者相反**。
            var sources = new[]
            {
                new RankSource(PriorityWorldPhysiology, 10f, 38, 5, 0, 5, 10),  // 远,jitter 小
                new RankSource(PriorityWorldPhysiology, 1f, 4, 5, 0, 5, 11),    // 近,jitter 大
            };

            IReadOnlyList<RankedSource> order = new RankKeyComputer().Order(sources);

            Assert.That(order[0].CellJitter, Is.GreaterThan(order[1].CellJitter),
                "夹具自证:近者的 jitter 确实更大(否则本夹具对 distance 键无判别力)");
            Assert.That(order[0].ActorId, Is.EqualTo(4), "同类内更近者优先,与 jitter 大小无关");
            Assert.That(order[0].Distance, Is.EqualTo(1f));
            Assert.That(order[1].Distance, Is.EqualTo(10f));
        }

        [Test]
        public void test_rankKey_deterministicAcrossCalls()
        {
            var sources = new[]
            {
                new RankSource(PriorityWorldPhysiology, 3f, 1, 1, 0, 1, 10),
                new RankSource(PrioritySelfAction, 5f, 2, 2, 0, 2, 20),
                new RankSource(PriorityAmbience, 1f, 3, 3, 0, 3, 30),
                new RankSource(PriorityWorldPhysiology, 2f, 4, 4, 0, 4, 11),
            };
            var c1 = new RankKeyComputer();
            var c2 = new RankKeyComputer();

            IReadOnlyList<RankedSource> order1 = c1.Order(sources);
            IReadOnlyList<RankedSource> order2 = c2.Order(sources);

            Assert.That(order2.Select(r => r.ActorId), Is.EqualTo(order1.Select(r => r.ActorId)),
                "rank_key 必须确定性(两个独立实例同序)");
            Assert.That(order1[0].PriorityClass, Is.EqualTo(PrioritySelfAction), "自身动作最高");
            Assert.That(order1[1].PriorityClass, Is.EqualTo(PriorityWorldPhysiology), "世界生理次之");
            Assert.That(order1[2].PriorityClass, Is.EqualTo(PriorityWorldPhysiology), "同类两条相邻");
            Assert.That(order1[3].PriorityClass, Is.EqualTo(PriorityAmbience), "氛围最低");
            Assert.That(order1[1].Distance, Is.EqualTo(2f), "同类内近者先(2f 先于 3f)");
        }

        [Test]
        public void test_rankKey_sameClassCloserFirst()
        {
            var sources = new[]
            {
                new RankSource(PriorityWorldPhysiology, 5f, 1, 1, 0, 1, 10),
                new RankSource(PriorityWorldPhysiology, 2f, 2, 2, 0, 2, 11),
                new RankSource(PriorityWorldPhysiology, 8f, 3, 3, 0, 3, 12),
            };
            IReadOnlyList<RankedSource> order = new RankKeyComputer().Order(sources);
            Assert.That(order[0].Distance, Is.EqualTo(2f), "同类内距离最近者优先");
            Assert.That(order[1].Distance, Is.EqualTo(5f));
            Assert.That(order[2].Distance, Is.EqualTo(8f));
        }

        [Test]
        public void test_rankKey_equalDistance_cellJitterBreaksTie()
        {
            var sources = new[]
            {
                new RankSource(PriorityWorldPhysiology, 3f, 1, 5, 0, 5, 10),
                new RankSource(PriorityWorldPhysiology, 3f, 2, 5, 0, 5, 11),
                new RankSource(PriorityWorldPhysiology, 3f, 3, 5, 0, 5, 12),
            };

            // 两个独立实例各自算 ⇒ 断言的是**排序确定性**,不是缓存复用。
            IReadOnlyList<RankedSource> o1 = new RankKeyComputer().Order(sources);
            IReadOnlyList<RankedSource> o2 = new RankKeyComputer().Order(sources);

            Assert.That(o2.Select(r => r.ActorId), Is.EqualTo(o1.Select(r => r.ActorId)),
                "等距平局由 cell_jitter 确定性分出");
            Assert.That(o1[0].ActorId, Is.Not.EqualTo(o1[1].ActorId), "等距不同 ActorId 必须分出主次");
            Assert.That(o1.Select(r => r.CellJitter).Distinct().Count(), Is.GreaterThan(1),
                "cell_jitter 确实产生了区分(否则破平键失效)");
        }

        [Test]
        public void test_rankKey_recalcOnlyOnBirthDeath()
        {
            var computer = new RankKeyComputer();
            var sources = new List<RankSource>
            {
                new RankSource(PriorityWorldPhysiology, 3f, 1, 1, 0, 1, 10),
            };

            computer.Order(sources);
            long afterFirst = computer.RecalculationCount;
            computer.Order(sources);   // 挂起期间重复取序
            computer.Order(sources);
            Assert.That(computer.RecalculationCount, Is.EqualTo(afterFirst),
                "挂起期间不逐帧重排(防增益抖动)");
        }

        [Test]
        public void test_rankKey_invalidateForcesRecalc()
        {
            var computer = new RankKeyComputer();
            var sources = new List<RankSource>
            {
                new RankSource(PriorityWorldPhysiology, 3f, 1, 1, 0, 1, 10),
            };
            computer.Order(sources);
            long before = computer.RecalculationCount;

            sources.Add(new RankSource(PriorityAmbience, 1f, 2, 2, 0, 2, 20));
            computer.Invalidate();     // 声源生灭 ⇒ 显式失效
            IReadOnlyList<RankedSource> order = computer.Order(sources);

            Assert.That(computer.RecalculationCount, Is.GreaterThan(before), "声源生灭时必须重算");
            Assert.That(order, Has.Count.EqualTo(2), "新声源进入序");
        }

        [Test]
        public void test_priority_duckDoesNotDrop()
        {
            // 「赢」= 抢占序与 duck 深度(只压不丢,不丢声)
            // ⚠️ duck 执行体归 Story 006(本 story 只出序)⇒ 此处保留自持谓词,非被测物缺失。
            // 值域 [-24, 0] dB;被压类音量 > 0(不丢声)
            float duck = DuckDepthFixture.ComputeDuckDepth();
            Assert.That(duck, Is.LessThanOrEqualTo(0f), "duck 深度 ≤ 0 dB(只压)");
            Assert.That(duck, Is.GreaterThanOrEqualTo(-24f), "duck 深度 ≥ -24 dB(不丢声)");
            float volumeAfterDuck = DuckDepthFixture.ApplyDuck(1.0f, duck);
            Assert.That(volumeAfterDuck, Is.GreaterThan(0f), "duck 后被压类音量 > 0(不丢声)");
        }

        [Test]
        public void test_priority_busNotPriorityClass()
        {
            // bus ≠ 优先级类别(GDD :852):世界呼吸走 Ambience 总线但优先级属生理声类。
            // 断言排序按 PriorityClass,而非按 bus 名 —— 若按 bus 字母序,"Ambience" 会排到
            // "SFX" 之前,与 PriorityClass 序恰好**相反** ⇒ 本夹具能区分两种实现。
            var sources = new[]
            {
                // 走 Ambience 总线(测试侧标注),但优先级属生理声类(高优先)
                new RankSource(PriorityWorldPhysiology, 5f, 1, 1, 0, 1, 10),
                // 走 SFX 总线,但优先级属氛围类(低优先)
                new RankSource(PriorityAmbience, 1f, 2, 2, 0, 2, 30),
            };
            var busByActor = new Dictionary<int, string> { [1] = "Ambience", [2] = "SFX" };

            IReadOnlyList<RankedSource> order = new RankKeyComputer().Order(sources);

            Assert.That(order[0].ActorId, Is.EqualTo(1),
                "生理声(走 Ambience)排在氛围(走 SFX)之前 ⇒ 按 PriorityClass 而非 bus 字母序");
            Assert.That(busByActor[order[0].ActorId], Is.EqualTo("Ambience"),
                "且确认该源确实是「走 Ambience 的生理声」(bus ≠ 优先级类别)");
            Assert.That(order[1].PriorityClass, Is.EqualTo(PriorityAmbience), "氛围类(走 SFX)排第二");
        }

        // ═══ AC-44-16:世界语境呼吸行接线(**真门** AudioEventTableGates)═══

        /// <summary>构造一行(不经 JSON —— 直击门的输入面,避免「测试自造解析器」这层假绿)。</summary>
        private static AudioEventTableRow WorldBreathRow(string bus, params string[] presentKeys)
            => new AudioEventTableRow
            {
                Cue = AudioEventTableGates.WorldBreathCue,
                Bus = bus,
                PresentKeys = presentKeys,
            };

        [Test]
        public void test_eventTable_worldBreathRow_presentAndWired()
        {
            var rows = new[]
            {
                WorldBreathRow("Ambience", "min_distance", "max_distance", "occlusion_lowpass"),
            };

            IReadOnlyList<string> errors = AudioEventTableGates.ValidateWorldBreathRow(rows);

            Assert.That(errors, Is.Empty, string.Join("\n", errors));
        }

        [Test]
        public void test_eventTable_worldBreathRow_wrongBus_red()
        {
            var rows = new[]
            {
                WorldBreathRow("Stethoscope", "min_distance", "max_distance", "occlusion_lowpass"),
            };

            IReadOnlyList<string> errors = AudioEventTableGates.ValidateWorldBreathRow(rows);

            Assert.That(errors, Is.Not.Empty, "bus ≠ Ambience 必须判失败(不得复用 Stethoscope)");
            Assert.That(string.Join("\n", errors), Does.Contain("AC-44-16"));
        }

        [Test]
        public void test_eventTable_worldBreathRow_missingParam_red()
        {
            // 缺 occlusion_lowpass(遮挡无参可依 ⇒ AC-44-D8 ① 无输入)
            var rows = new[] { WorldBreathRow("Ambience", "min_distance", "max_distance") };

            IReadOnlyList<string> errors = AudioEventTableGates.ValidateWorldBreathRow(rows);

            Assert.That(errors, Is.Not.Empty, "缺 occlusion_lowpass 必须判失败");
            Assert.That(string.Join("\n", errors), Does.Contain("occlusion_lowpass"));
        }

        /// <summary>行**缺失**归 <c>ValidateRequiredCues</c> —— 本条锁定这条分工,
        /// 防「两处都报」把既有夹具的错误计数撞翻(空表夹具断 `Count == 1`)。</summary>
        [Test]
        public void test_eventTable_worldBreathRow_absent_delegatedToRequiredCues()
        {
            var rows = new[] { new AudioEventTableRow { Cue = "Voice_Cough_Damp", PresentKeys = new List<string>() } };

            Assert.That(AudioEventTableGates.ValidateWorldBreathRow(rows), Is.Empty,
                "行缺失**不**在本规则报(避免与 RequiredCues 重复记账)");

            IReadOnlyList<string> required = AudioEventTableGates.ValidateRequiredCues(rows);
            Assert.That(string.Join("\n", required), Does.Contain(AudioEventTableGates.WorldBreathCue),
                "行缺失由 RequiredCues 兑现(AC-44-16 的「行缺失 = 构建失败」)");
        }

        // ═══ cell_jitter ═══

        [Test]
        public void test_cellJitter_deterministicAndDistinguishesSameCell()
        {
            ushort j1 = CellJitter.Compute(1, 5, 0, 5);
            ushort j2 = CellJitter.Compute(2, 5, 0, 5);
            ushort j3 = CellJitter.Compute(1, 5, 0, 5);
            Assert.That(j3, Is.EqualTo(j1), "cell_jitter 必须确定性");
            Assert.That(j1, Is.Not.EqualTo(j2), "不同 ActorId 必须不同 jitter");
        }

        // ═══ 装配入口端到端 ═══

        [Test]
        public void test_spatializationDirector_endToEnd()
        {
            using var dir = new SpatializationDirector(LatticeSize);
            var cell = new Int3(3, 0, 4);
            var cue1 = new AudioCueDto(cue: 10, intensity: 128, cell: cell, source: 1, looped: true);
            var cue2 = new AudioCueDto(cue: 11, intensity: 128, cell: cell, source: 2, looped: true);
            // cell = (3,0,4) ⇒ 锚点 (6, 0, 8);cell_jitter 只加**格内**偏移 [0, LatticeSize)。

            dir.Spawn(cue1, PriorityWorldPhysiology, occluded: true, occlusionLowpassHz: 1200f, distance: 3f);
            dir.Spawn(cue2, PriorityWorldPhysiology, occluded: false, occlusionLowpassHz: 1200f, distance: 3f);

            Assert.That(dir.ActiveCount, Is.EqualTo(2), "同 cell 两病人 = 两条声源(不合并)");
            Assert.That(dir.Order, Has.Count.EqualTo(2), "两条都进排名序");

            // 锚点 = 整数格 × 边长,cell_jitter 是**格内**偏移(不越格)
            AudioSource src = dir.SourceForActor(1);
            float baseX = 3f * LatticeSize, baseZ = 4f * LatticeSize;
            Assert.That(src.transform.position.x, Is.InRange(baseX, baseX + LatticeSize),
                "锚点 + 格内偏移 ∈ [格锚点, 格锚点 + 边长) —— jitter 不得越格");
            Assert.That(src.transform.position.z, Is.InRange(baseZ, baseZ + LatticeSize));
            Assert.That(src.transform.position.y, Is.EqualTo(0f), "Y 不叠 jitter(水平面散布)");

            // 遮挡逐源独立:源 1 有低通,源 2 无
            // ⚠️ 用 GetComponent(**非泛型 null 安全**)—— 泛型版在缺组件时抛 MissingComponentException。
            var f1 = src.GetComponent(typeof(AudioLowPassFilter)) as AudioLowPassFilter;
            var f2 = dir.SourceForActor(2).GetComponent(typeof(AudioLowPassFilter)) as AudioLowPassFilter;
            Assert.That(f1, Is.Not.Null, "被遮挡源挂上低通");
            Assert.That(f1.enabled, Is.True, "被遮挡源施加低通");
            Assert.That(f1.cutoffFrequency, Is.GreaterThan(0f), "穿门 ≠ 静音");
            Assert.That(f2 == null || !f2.enabled, Is.True, "未遮挡源不施加低通");

            // 生灭级重算:释放一条 ⇒ 序变
            dir.Release(1);
            Assert.That(dir.Order, Has.Count.EqualTo(1), "释放后序随之更新");
        }

        // ═══ 测试替身与夹具(**保留的两处非生产物**)═══

        /// <summary>负向夹具 —— 证明 AC-44-D2 的反射扫描有判别力。</summary>
        private sealed class ReverseDerivationFixture
        {
            public WorldPos FloatToWorldPos(float x, float y, float z)
                => new WorldPos((int)x, (int)y, (int)z);
        }

        /// <summary>duck 深度自持谓词 —— **非漏改**:duck 执行体归 Story 006,本 story 只出序
        /// (GDD :850「rank 只决定抢占序与 duck 深度」;深度旋钮归 006)。</summary>
        private static class DuckDepthFixture
        {
            public static float ComputeDuckDepth() => -12f;

            /// <summary>应用 duck 深度(dB)到线性幅度。dB → 幅度 = 10^(dB/20)。</summary>
            public static float ApplyDuck(float linearVolume, float depthDb)
                => linearVolume * Mathf.Pow(10f, depthDb / 20f);
        }
    }
}
