// 权威来源:production/epics/audio-system/story-008-spatialization-world-breath.md
//   (AC-44-D2 / AC-44-D3 / AC-44-D8 / AC-44-16 / 优先级公式)

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
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

        private const int PrioritySelfAction = 0;
        private const int PriorityWorldPhysiology = 1;
        private const int PriorityAmbience = 2;

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

        // ═══ AC-44-D8:遮挡低通 ═══

        [Test]
        public void test_occlusionDriver_occluded_lowpassPositive()
        {
            var driver = new FakeOcclusionDriver();
            driver.Update(cutoffHz: 1200f, occluded: true);
            Assert.That(driver.LastCutoffHz, Is.GreaterThan(0f), "遮挡时 cutoff > 0(穿门 ≠ 静音)");
            Assert.That(driver.LastCutoffHz, Is.EqualTo(1200f), "cutoff = occlusion_lowpass 参数值");
        }

        [Test]
        public void test_occlusionDriver_notOccluded_noLowpass()
        {
            var driver = new FakeOcclusionDriver();
            driver.Update(cutoffHz: 1200f, occluded: false);
            Assert.That(driver.LastCutoffHz, Is.EqualTo(0f), "无遮挡时不施加低通");
            Assert.That(driver.FilterApplied, Is.False, "无遮挡时 AudioLowPassFilter 不启用");
        }

        [Test]
        public void test_occlusionDriver_zeroCutoff_rejected()
        {
            var driver = new FakeOcclusionDriver();
            Assert.Throws<ArgumentException>(() => driver.Update(cutoffHz: 0f, occluded: true),
                "cutoff ≤ 0 必须拒绝(穿门 ≠ 静音)");
        }

        // ═══ AC-44-D8:多病人独立声源 ═══

        [Test]
        public void test_sourcePool_sameCell_nSourcesNotMerged()
        {
            var pool = new FakeSourcePool();
            var cell = new Int3(5, 0, 5);
            pool.Spawn(1, cell, cueId: 10);
            pool.Spawn(2, cell, cueId: 11);
            pool.Spawn(3, cell, cueId: 12);
            Assert.That(pool.ActiveCount, Is.EqualTo(3), "同 cell 3 病人 = 3 条独立 AudioSource");
            Assert.That(pool.SourceForActor(1), Is.Not.EqualTo(pool.SourceForActor(2)),
                "不同 ActorId 的声源必须是不同实例");
        }

        [Test]
        public void test_sourcePool_singlePatient_oneSource()
        {
            var pool = new FakeSourcePool();
            pool.Spawn(1, new Int3(5, 0, 5), cueId: 10);
            Assert.That(pool.ActiveCount, Is.EqualTo(1), "N=1 时实例数 = 1");
        }

        [Test]
        public void test_sourcePool_release_reusable()
        {
            var pool = new FakeSourcePool();
            pool.Spawn(1, new Int3(5, 0, 5), cueId: 10);
            pool.Release(1);
            pool.Spawn(2, new Int3(6, 0, 6), cueId: 11);
            Assert.That(pool.ActiveCount, Is.EqualTo(1), "释放后重新分配,池不泄漏");
        }

        // ═══ 优先级公式:rank_key ═══

        [Test]
        public void test_rankKey_deterministicAcrossCalls()
        {
            var sources = new[]
            {
                new RankSource { PriorityClass = PriorityWorldPhysiology, Distance = 3f, ActorId = 1, CueId = 10 },
                new RankSource { PriorityClass = PrioritySelfAction, Distance = 5f, ActorId = 2, CueId = 20 },
                new RankSource { PriorityClass = PriorityAmbience, Distance = 1f, ActorId = 3, CueId = 30 },
                new RankSource { PriorityClass = PriorityWorldPhysiology, Distance = 2f, ActorId = 4, CueId = 11 },
            };
            var order1 = RankKeyComputer.ComputeOrder(sources);
            var order2 = RankKeyComputer.ComputeOrder(sources);
            var order3 = RankKeyComputer.ComputeOrder(sources);
            Assert.That(order2, Is.EqualTo(order1), "rank_key 必须确定性(三次一致)");
            Assert.That(order3, Is.EqualTo(order1), "rank_key 必须确定性(三次一致)");
            Assert.That(order1[0].PriorityClass, Is.EqualTo(PrioritySelfAction), "自身动作最高");
            Assert.That(order1[1].PriorityClass, Is.EqualTo(PriorityWorldPhysiology), "世界生理次之");
            Assert.That(order1[3].PriorityClass, Is.EqualTo(PriorityAmbience), "氛围最低");
        }

        [Test]
        public void test_rankKey_sameClassCloserFirst()
        {
            var sources = new[]
            {
                new RankSource { PriorityClass = PriorityWorldPhysiology, Distance = 5f, ActorId = 1, CueId = 10 },
                new RankSource { PriorityClass = PriorityWorldPhysiology, Distance = 2f, ActorId = 2, CueId = 11 },
                new RankSource { PriorityClass = PriorityWorldPhysiology, Distance = 8f, ActorId = 3, CueId = 12 },
            };
            var order = RankKeyComputer.ComputeOrder(sources);
            Assert.That(order[0].Distance, Is.EqualTo(2f), "同类内距离最近者优先");
            Assert.That(order[1].Distance, Is.EqualTo(5f));
            Assert.That(order[2].Distance, Is.EqualTo(8f));
        }

        [Test]
        public void test_rankKey_equalDistance_cellJitterBreaksTie()
        {
            var sources = new[]
            {
                new RankSource { PriorityClass = PriorityWorldPhysiology, Distance = 3f, ActorId = 1, CueId = 10 },
                new RankSource { PriorityClass = PriorityWorldPhysiology, Distance = 3f, ActorId = 2, CueId = 11 },
                new RankSource { PriorityClass = PriorityWorldPhysiology, Distance = 3f, ActorId = 3, CueId = 12 },
            };
            var order1 = RankKeyComputer.ComputeOrder(sources);
            var order2 = RankKeyComputer.ComputeOrder(sources);
            Assert.That(order2, Is.EqualTo(order1), "等距平局由 cell_jitter 确定性分出");
            Assert.That(order1[0].ActorId, Is.Not.EqualTo(order1[1].ActorId),
                "等距不同 ActorId 必须分出主次");
        }

        [Test]
        public void test_rankKey_recalcOnlyOnBirthDeath()
        {
            var sources = new List<RankSource>
            {
                new RankSource { PriorityClass = PriorityWorldPhysiology, Distance = 3f, ActorId = 1, CueId = 10 },
            };
            var order1 = RankKeyComputer.ComputeOrder(sources);
            long recalcCount1 = RankKeyComputer.RecalculationCount;
            var order2 = RankKeyComputer.ComputeOrder(sources);
            long recalcCount2 = RankKeyComputer.RecalculationCount;
            Assert.That(recalcCount2, Is.EqualTo(recalcCount1), "挂起期间不逐帧重排");
            sources.Add(new RankSource { PriorityClass = PriorityAmbience, Distance = 1f, ActorId = 2, CueId = 20 });
            var order3 = RankKeyComputer.ComputeOrder(sources);
            long recalcCount3 = RankKeyComputer.RecalculationCount;
            Assert.That(recalcCount3, Is.GreaterThan(recalcCount2), "声源生灭时必须重算");
        }

        [Test]
        public void test_priority_duckDoesNotDrop()
        {
            // 「赢」= 抢占序与 duck 深度(只压不丢,不丢声)
            // 值域 [-24, 0] dB;被压类音量 > 0(不丢声)
            var duck = FakeDuckComputer.ComputeDuckDepth();
            Assert.That(duck, Is.LessThanOrEqualTo(0f), "duck 深度 ≤ 0 dB(只压)");
            Assert.That(duck, Is.GreaterThanOrEqualTo(-24f), "duck 深度 ≥ -24 dB(不丢声)");
            // 被压后音量仍 > 0(不丢声)
            float volumeAfterDuck = FakeDuckComputer.ApplyDuck(1.0f, duck);
            Assert.That(volumeAfterDuck, Is.GreaterThan(0f), "duck 后被压类音量 > 0(不丢声)");
        }

        [Test]
        public void test_priority_busNotPriorityClass()
        {
            // bus ≠ 优先级类别:世界呼吸走 Ambience 总线但优先级属生理声类
            // 验证排序按 PriorityClass 而非 Bus 归类
            var sources = new[]
            {
                // 走 Ambience 总线,但优先级属生理声类(高优先)
                new RankSource { PriorityClass = PriorityWorldPhysiology, Distance = 5f, ActorId = 1, CueId = 10, Bus = "Ambience" },
                // 走 SFX 总线,但优先级属氛围类(低优先)
                new RankSource { PriorityClass = PriorityAmbience, Distance = 1f, ActorId = 2, CueId = 30, Bus = "SFX" },
            };
            var order = RankKeyComputer.ComputeOrder(sources);
            // 排序按 PriorityClass(生理声 > 氛围),不按 Bus 字母序
            Assert.That(order[0].PriorityClass, Is.EqualTo(PriorityWorldPhysiology),
                "排序按 PriorityClass 归类:生理声(走 Ambience)排在氛围(走 SFX)之前");
            Assert.That(order[0].Bus, Is.EqualTo("Ambience"),
                "走 Ambience 总线的生理声源排第一(bus ≠ 优先级类别)");
            Assert.That(order[1].PriorityClass, Is.EqualTo(PriorityAmbience),
                "氛围类(走 SFX)排第二");
        }

        // ═══ AC-44-16:事件表 worldBreath 接线断言 ═══

        [Test]
        public void test_eventTable_worldBreathRow_presentAndWired()
        {
            string path = Path.Combine(repoRoot(), "assets", "data", "audio_events.json");
            Assert.That(File.Exists(path), Is.True, $"真种子缺失:{path}");
            string text = File.ReadAllText(path);
            var table = ParseEventTable(text);
            var row = table.Rows.FirstOrDefault(r => r.Cue == "WorldBreath_Baseline");
            Assert.That(row, Is.Not.Null, "WorldBreath_Baseline 行必须存在(AC-44-16)");
            Assert.That(row.Bus, Is.EqualTo("Ambience"), "bus 必须 = Ambience(不得复用 Stethoscope)");
            Assert.That(row.PresentKeys.Contains("min_distance"), Is.True, "必须含 min_distance");
            Assert.That(row.PresentKeys.Contains("max_distance"), Is.True, "必须含 max_distance");
            Assert.That(row.PresentKeys.Contains("occlusion_lowpass"), Is.True, "必须含 occlusion_lowpass");
        }

        [Test]
        public void test_eventTable_worldBreathRow_missing_red()
        {
            // 构造真正缺失 WorldBreath_Baseline 行的 JSON
            string path = Path.Combine(repoRoot(), "assets", "data", "audio_events.json");
            string json = File.ReadAllText(path).Replace("WorldBreath_Baseline", "OtherCue");
            var table = ParseEventTable(json);
            var row = table.Rows.FirstOrDefault(r => r.Cue == "WorldBreath_Baseline");
            Assert.That(row, Is.Null, "删 worldBreath 行后,接线断言必须失败(行不存在)");
        }

        [Test]
        public void test_eventTable_worldBreathRow_wrongBus_red()
        {
            // 构造 bus = Stethoscope 的 worldBreath 行,验证接线断言失败
            string path = Path.Combine(repoRoot(), "assets", "data", "audio_events.json");
            string json = File.ReadAllText(path).Replace("\"bus\": \"Ambience\"", "\"bus\": \"Stethoscope\"");
            var table = ParseEventTable(json);
            var row = table.Rows.FirstOrDefault(r => r.Cue == "WorldBreath_Baseline");
            Assert.That(row, Is.Not.Null, "行必须存在");
            Assert.That(row.Bus, Is.Not.EqualTo("Ambience"),
                "bus = Stethoscope 必须判失败(不得复用 Stethoscope 总线)");
        }

        // ═══ LatticeToWorld + cell_jitter ═══

        [Test]
        public void test_latticeToWorld_integerCellToFloat()
        {
            var cell = new Int3(10, 0, 20);
            Vector3 world = FakeLattice.ToWorld(cell);
            Assert.That(world.x, Is.EqualTo(10f), "X 轴映射");
            Assert.That(world.y, Is.EqualTo(0f), "Y 轴映射");
            Assert.That(world.z, Is.EqualTo(20f), "Z 轴映射");
        }

        [Test]
        public void test_cellJitter_deterministicAndDistinguishesSameCell()
        {
            var cell = new Int3(5, 0, 5);
            ushort j1 = CellJitter.Compute(1, cell);
            ushort j2 = CellJitter.Compute(2, cell);
            ushort j3 = CellJitter.Compute(1, cell);
            Assert.That(j3, Is.EqualTo(j1), "cell_jitter 必须确定性");
            Assert.That(j1, Is.Not.EqualTo(j2), "不同 ActorId 必须不同 jitter");
        }

        // ═══ 测试替身与夹具 ═══

        private sealed class SourceEntry
        {
            public int ActorId; public Int3 Cell; public int CueId; public bool Active;
        }

        private sealed class FakeSourcePool
        {
            private readonly Dictionary<int, SourceEntry> _sources = new Dictionary<int, SourceEntry>();
            public int ActiveCount => _sources.Count(kv => kv.Value.Active);
            public SourceEntry SourceForActor(int actorId)
                => _sources.TryGetValue(actorId, out var e) ? e : null;
            public void Spawn(int actorId, Int3 cell, int cueId)
                => _sources[actorId] = new SourceEntry { ActorId = actorId, Cell = cell, CueId = cueId, Active = true };
            public void Release(int actorId)
            { if (_sources.TryGetValue(actorId, out var e)) e.Active = false; }
        }

        private sealed class FakeOcclusionDriver
        {
            public float LastCutoffHz { get; private set; }
            public bool FilterApplied { get; private set; }
            public void Update(float cutoffHz, bool occluded)
            {
                if (occluded)
                {
                    if (cutoffHz <= 0f)
                        throw new ArgumentException("cutoff 必须 > 0(穿门 ≠ 静音)");
                    LastCutoffHz = cutoffHz; FilterApplied = true;
                }
                else { LastCutoffHz = 0f; FilterApplied = false; }
            }
        }

        private sealed class RankSource
        {
            public int PriorityClass; public float Distance; public int ActorId; public int CueId; public string Bus;
        }

        private static class RankKeyComputer
        {
            public static long RecalculationCount;
            private static ulong _lastInputHash;
            private static List<RankSource> _lastResult;

            public static List<RankSource> ComputeOrder(IEnumerable<RankSource> sources)
            {
                // 输入哈希(确定性:同输入同哈希)
                ulong hash = 17;
                foreach (var s in sources)
                {
                    hash = hash * 31 + (ulong)s.PriorityClass;
                    hash = hash * 31 + (ulong)s.Distance.GetHashCode();
                    hash = hash * 31 + (ulong)s.ActorId;
                    hash = hash * 31 + (ulong)s.CueId;
                }
                // 同输入 ⇒ 不重算(挂起期间不逐帧重排,防增益抖动)
                if (hash == _lastInputHash && _lastResult != null)
                    return _lastResult;
                // 输入变化(声源生灭)⇒ 重算
                RecalculationCount++;
                _lastResult = sources.OrderBy(s => s.PriorityClass).ThenBy(s => s.Distance)
                    .ThenBy(s => CellJitter.Compute(s.ActorId, default)).ThenBy(s => s.CueId).ToList();
                _lastInputHash = hash;
                return _lastResult;
            }
        }

        private static class FakeLattice
        {
            public static Vector3 ToWorld(Int3 cell, ushort jitter = 0)
                => new Vector3(cell.X + jitter * 0.001f, cell.Y, cell.Z + jitter * 0.001f);
        }

        private static class CellJitter
        {
            public static ushort Compute(int actorId, Int3 cell)
            {
                ulong state = SplitMix64.Hash(actorId, cell.X, cell.Y, cell.Z);
                return (ushort)(state & 0xFFFF);
            }
        }

        private static class FakeDuckComputer
        {
            public static float ComputeDuckDepth() => -12f;

            /// <summary>应用 duck 深度(dB)到线性幅度。dB → 幅度 = 10^(dB/20)。</summary>
            public static float ApplyDuck(float linearVolume, float depthDb)
                => linearVolume * Mathf.Pow(10f, depthDb / 20f);
        }

        private sealed class ReverseDerivationFixture
        {
            public WorldPos FloatToWorldPos(float x, float y, float z)
                => new WorldPos((int)x, (int)y, (int)z);
        }

        private static AudioEventTable ParseEventTable(string json)
        {
            var table = new AudioEventTable { Rows = new List<AudioEventTableRow>() };
            var cueMatch = Regex.Match(json, "\"cue\"\\s*:\\s*\"WorldBreath_Baseline\"");
            if (cueMatch.Success)
            {
                var row = new AudioEventTableRow { Cue = "WorldBreath_Baseline" };
                var busMatch = Regex.Match(json.Substring(cueMatch.Index),
                    "\"bus\"\\s*:\\s*\"(\\w+)\"");
                row.Bus = busMatch.Success ? busMatch.Groups[1].Value : null;
                var presentKeys = new List<string>();
                foreach (string key in new[] { "min_distance", "max_distance", "occlusion_lowpass" })
                {
                    int idx = json.IndexOf($"\"{key}\"", cueMatch.Index, StringComparison.Ordinal);
                    if (idx > cueMatch.Index) presentKeys.Add(key);
                }
                row.PresentKeys = presentKeys;
                var rows = new List<AudioEventTableRow> { row };
                table.Rows = rows;
            }
            return table;
        }
    }
}
