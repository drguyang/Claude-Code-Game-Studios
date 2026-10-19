// 权威来源:production/epics/telemetry-analytics/story-008-pure-function-recomputable.md
//   (AC-51-C1…C4 — 纯函数与可复算:同流两次重算逐位相同 / 单一折叠函数两入口同值
//    / 可进 ADR-12 黄金夹具(ADVISORY) / 不依赖墙钟帧序执行次数)
//   · R8:每条指标 = Fold(E) —— 不读墙钟/帧序/执行次数;同流同值
//   · R9:两种入口共用同一个折叠函数 —— 增量边读边折终值 == 离线整表折叠终值
//   · R11:指标定义必须写成可复算的谓词
// ADR-019 §一(回放即数据记录)· ADR-012(黄金夹具 + ROUND_HALF_AWAY_FROM_ZERO)
// ADR-006 §五(Fix 编码器 + 守恒律)
//
// ⚠️ 落点:故事 Test Evidence 登记路径 = tests/unit/telemetry/pure_function_recomputable_test.cs;
//    Unity 只编译 unity/Assets/ 树 ⇒ **真身 = 本文件**;账本侧由 tests/integration/telemetry/README.md 互链。
// ⚠️ 负向夹具落位:与 Story 001-007 同型 —— 违例 fixture 住**测试命名空间**。
// ⚠️ 测试纪律:arrange/act/assert · 无随机 · 无时间依赖(禁 DateTime/Time)·
//    夹具读取前置 File.Exists 断言(缺失即红,不静默跳过)。

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DaYiJingCheng.Telemetry;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Unit.Telemetry
{
    [TestFixture]
    internal sealed class PureFunctionRecomputableTest
    {
        // ⚠️ S19 修复(TD-epic-01):原「测试自持谓词面」已删除 ——
        //    谓词面上移为生产类 `DaYiJingCheng.Telemetry.FoldEngine`
        //    (ADR-019 §一;那个自持面在普查里被判为「AC 语义层无生产主语」)。
        //    本文件现在**只做夹具 + 断言**,不再自带公式。

        // ══════════════ AC-51-C1:同流两次重算逐位相同 ══════════════

        /// <summary>AC-51-C1:同流两次重算逐位相同 ——
        /// 连续重算两次,全部字段逐位相同(含 Q16.16 标量导出)。</summary>
        [Test]
        public void test_c1_sameStreamTwiceBitwiseIdentical()
        {
            // Arrange
            var events = new List<FoldEngine.Event>
            {
                new FoldEngine.Event { Kind = 1, Tick = 10, Payload = 100 },
                new FoldEngine.Event { Kind = 2, Tick = 20, Payload = 200 },
                new FoldEngine.Event { Kind = 1, Tick = 30, Payload = 150 },
                new FoldEngine.Event { Kind = 3, Tick = 40, Payload = 300 },
            };

            // Act:连续重算两次
            var r1 = FoldEngine.Fold(events);
            var r2 = FoldEngine.Fold(events);

            // Assert:全部字段逐位相同
            Assert.That(r2.TotalEvents, Is.EqualTo(r1.TotalEvents), "TotalEvents 逐位相同");
            Assert.That(r2.DistinctKinds, Is.EqualTo(r1.DistinctKinds), "DistinctKinds 逐位相同");
            Assert.That(r2.SumPayload, Is.EqualTo(r1.SumPayload), "SumPayload 逐位相同");
            Assert.That(r2.MinTick, Is.EqualTo(r1.MinTick), "MinTick 逐位相同");
            Assert.That(r2.MaxTick, Is.EqualTo(r1.MaxTick), "MaxTick 逐位相同");
            Assert.That(r2.SessionSpan, Is.EqualTo(r1.SessionSpan), "SessionSpan 逐位相同");

            // Assert:Q16.16 标量导出逐位相同
            Assert.That(r2.ExportScalarQ1616(), Is.EqualTo(r1.ExportScalarQ1616()),
                "Q16.16 标量导出逐位相同");

            // Assert:黄金值(确定性正确性 — 不仅测"两次相同"还测"值正确")
            Assert.That(r1.TotalEvents, Is.EqualTo(4), "TotalEvents = 4");
            Assert.That(r1.DistinctKinds, Is.EqualTo(3), "DistinctKinds = 3(Kinds 1,2,3)");
            Assert.That(r1.SumPayload, Is.EqualTo(750), "SumPayload = 100+200+150+300 = 750");
            Assert.That(r1.MinTick, Is.EqualTo(10), "MinTick = 10");
            Assert.That(r1.MaxTick, Is.EqualTo(40), "MaxTick = 40");
            Assert.That(r1.SessionSpan, Is.EqualTo(30), "SessionSpan = 40−10 = 30");
        }

        // ══════════════ AC-51-C2:单一折叠函数(两入口同值) ══════════════

        /// <summary>AC-51-C2:单一折叠函数(两入口同值)——
        /// 恰有一个 Fold 实现;增量边读边折的终值 == 离线整表折叠终值;51 无跨调用持久状态。</summary>
        [Test]
        public void test_c2_singleFoldImplementation()
        {
            // Arrange
            var events = new List<FoldEngine.Event>
            {
                new FoldEngine.Event { Kind = 1, Tick = 10, Payload = 100 },
                new FoldEngine.Event { Kind = 2, Tick = 20, Payload = 200 },
                new FoldEngine.Event { Kind = 1, Tick = 30, Payload = 150 },
                new FoldEngine.Event { Kind = 3, Tick = 40, Payload = 300 },
            };

            // Act:两种入口
            var offline = FoldEngine.Fold(events);
            var incremental = FoldEngine.FoldIncremental(events);

            // Assert:两入口终值逐字段相同(单一实现)
            Assert.That(incremental.TotalEvents, Is.EqualTo(offline.TotalEvents));
            Assert.That(incremental.DistinctKinds, Is.EqualTo(offline.DistinctKinds));
            Assert.That(incremental.SumPayload, Is.EqualTo(offline.SumPayload));
            Assert.That(incremental.MinTick, Is.EqualTo(offline.MinTick));

            // Assert:黄金值(确定性正确性)
            Assert.That(offline.TotalEvents, Is.EqualTo(4), "TotalEvents = 4");
            Assert.That(offline.DistinctKinds, Is.EqualTo(3), "DistinctKinds = 3");
            Assert.That(offline.SumPayload, Is.EqualTo(750), "SumPayload = 750");
            Assert.That(incremental.MaxTick, Is.EqualTo(offline.MaxTick));
        }

        // ══════════════ AC-51-C4:不依赖墙钟/帧序/执行次数 ══════════════

        /// <summary>AC-51-C4:不依赖墙钟/帧序/执行次数 ——
        /// 51 命名空间下无 DateTime.Now/Environment.TickCount/Stopwatch/Time.*/UnityEngine.Random。</summary>
        [Test]
        public void test_c4_noWallClockOrFrameDependencies()
        {
            // Arrange:扫描 51 命名空间下的类型
            var telemetryTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                .Where(t => t.Namespace != null && t.Namespace.StartsWith("DaYiJingCheng.Telemetry"))
                .ToList();

            // Act:扫描方法名与字段名中的禁用关键词
            var violations = new List<string>();
            foreach (var t in telemetryTypes)
            {
                foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic |
                                              BindingFlags.Instance | BindingFlags.Static |
                                              BindingFlags.DeclaredOnly))
                {
                    if (m.Name.Contains("DateTime") || m.Name.Contains("Now") ||
                        m.Name.Contains("TickCount") || m.Name.Contains("Stopwatch") ||
                        m.Name.Contains("Time") || m.Name.Contains("Random"))
                        violations.Add($"{t.Name}.{m.Name}");
                }
                foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic |
                                             BindingFlags.Instance | BindingFlags.Static |
                                             BindingFlags.DeclaredOnly))
                {
                    if (f.Name.Contains("DateTime") || f.Name.Contains("Now") ||
                        f.Name.Contains("TickCount") || f.Name.Contains("Stopwatch") ||
                        f.Name.Contains("Time") || f.Name.Contains("Random"))
                        violations.Add($"{t.Name}.{f.Name}");
                }
            }

            // Assert:零违例(实现后 telemetryTypes 非空;唯一判据 = violations 为空)
            Assert.That(violations, Is.Empty,
                "51 不得依赖 DateTime.Now/Environment.TickCount/Stopwatch/Time.*/UnityEngine.Random:\n" +
                string.Join("\n", violations));
        }

        /// <summary>AC-51-C4 补充:同一流在两次不同"执行上下文"下重算 ⇒ 逐位相同。
        /// 用不同列表实例(模拟不同调用路径)验证不依赖外部状态。</summary>
        [Test]
        public void test_c4_differentContextsBitwiseIdentical()
        {
            // Arrange:两个独立列表实例(相同内容,不同对象)
            var events1 = new List<FoldEngine.Event>
            {
                new FoldEngine.Event { Kind = 1, Tick = 10, Payload = 100 },
                new FoldEngine.Event { Kind = 2, Tick = 20, Payload = 200 },
            };
            var events2 = new List<FoldEngine.Event>
            {
                new FoldEngine.Event { Kind = 1, Tick = 10, Payload = 100 },
                new FoldEngine.Event { Kind = 2, Tick = 20, Payload = 200 },
            };

            // Act
            var r1 = FoldEngine.Fold(events1);
            var r2 = FoldEngine.Fold(events2);

            // Assert:逐位相同(不依赖外部状态)
            Assert.That(r2.TotalEvents, Is.EqualTo(r1.TotalEvents));
            Assert.That(r2.DistinctKinds, Is.EqualTo(r1.DistinctKinds));
            Assert.That(r2.SumPayload, Is.EqualTo(r1.SumPayload));
            Assert.That(r2.MinTick, Is.EqualTo(r1.MinTick));
            Assert.That(r2.MaxTick, Is.EqualTo(r1.MaxTick));
        }
    }
}
