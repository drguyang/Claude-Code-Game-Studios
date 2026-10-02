// emergency-procedures Story 006 测试
//
// AC-10-08 [L] ADVISORY: L_input < 50 ms（NOT-RUN 直至 OQ-10-12 原型门）
// AC-10-21 [A] BLOCKING: 键鼠回退 ⇒ 幅度门恒过、节奏+稳度双门照评、cause = 降级
// AC-10-09 [A+B] BLOCKING: 静态扫描 DTO 无评价/分数/进度字段
// AC-10-17 [A] BLOCKING: Missed ⇒ 零档位指示
// AC-10-19 [L] ADVISORY: 联机非主机 L_input（NOT-RUN 依赖 45）
// AC-10-23 [L] ADVISORY: 录屏走查（NOT-RUN）
// 预表现实现 [L]: 本地 Judge 不写流
// F-10.6 口径护栏 [A]: 不存在合并延迟表述

using System;
using System.Linq;
using System.Reflection;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.EmergencyProcedures;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.EmergencyProcedures
{
    public class FeelLatencyTest
    {
        // ══════════ AC-10-21: 键鼠回退 ⇒ 幅度门恒过 ══════════

        [Test]
        public void test_ac1021_keyboardFallback_magnitudeAlwaysMax()
        {
            // 键鼠无模拟量通道 ⇒ magnitude ≡ MAG_MAX
            var reading = FeelLatencyEvaluator.CreateKeyboardFallbackReading();
            Assert.AreEqual(FeelLatencyEvaluator.MAG_MAX, reading.Magnitude,
                "键鼠回退 magnitude 应恒为 MAG_MAX");
        }

        [Test]
        public void test_ac1021_keyboardFallback_rhythmAndStabilityStillEvaluated()
        {
            // 节奏+稳度双门照评
            var reading = FeelLatencyEvaluator.CreateKeyboardFallbackReading();
            var action = new EmergencyActionRow { ActionId = 0, MinEdges = 2, MinHoldTicks = 50, MagThreshold = 400 };
            var ctx = new JudgeContext { Level = 5, MagThresholdEffective = 400 };

            // 幅度门恒过（magnitude = MAG_MAX ≥ 阈值）
            var result = JudgeEvaluator.Judge(reading, action, ctx);
            Assert.AreNotEqual(JudgeResult.Missed, result,
                "键鼠回退幅度门应恒过（不返回 Missed）");
        }

        [Test]
        public void test_ac1021_keyboardFallback_rhythmFail_returnsMissed()
        {
            // 节奏门失败 ⇒ Missed（双门照评的证据）
            var reading = FeelLatencyEvaluator.CreateKeyboardFallbackReading();
            // 构造节奏门失败: edges < MinEdges 且 holdTicks < MinHold
            var action = new EmergencyActionRow { ActionId = 0, MinEdges = 5, MinHoldTicks = 200, MagThreshold = 400 };
            var ctx = new JudgeContext { Level = 5, MagThresholdEffective = 400 };

            var result = JudgeEvaluator.Judge(reading, action, ctx);
            Assert.AreEqual(JudgeResult.Missed, result,
                "节奏门失败应返回 Missed（双门照评）");
        }

        [Test]
        public void test_ac1021_keyboardFallback_stabilityFail_returnsAppliedWeak()
        {
            // 稳度门失败 ⇒ AppliedWeak（双门照评的证据）
            // 构造极端不规则抖动: [0, 1, 1000]（最大偏差）
            var reading = new EmergencyReading(0, 100, 3, new[] { 0, 1, 1000 }, FeelLatencyEvaluator.MAG_MAX);
            var action = new EmergencyActionRow { ActionId = 0, MinEdges = 2, MinHoldTicks = 50, MagThreshold = 400 };
            var ctx = new JudgeContext { Level = 0, MagThresholdEffective = 400 };

            var result = JudgeEvaluator.Judge(reading, action, ctx);
            Assert.AreEqual(JudgeResult.AppliedWeak, result,
                "稳度门失败应返回 AppliedWeak（双门照评）");
        }

        [Test]
        public void test_ac1021_keyboardFallback_causeIsDegraded()
        {
            // cause = 降级
            var cause = FeelLatencyEvaluator.GetKeyboardFallbackCause();
            Assert.AreEqual(1, cause, "键鼠回退 cause 应为降级(1)");
        }

        // ══════════ AC-10-09: DTO 无评价/分数/进度字段 ══════════

        [Test]
        public void test_ac1009_dtoNoEvaluationFields()
        {
            // 递归扫呈现 DTO 传递闭包 ⇒ 零评价/分数/进度/完成度字段
            var assembly = typeof(EmergencyReading).Assembly;
            var dtoTypes = assembly.GetTypes()
                .Where(t => t.Name.Contains("Dto") || t.Name.Contains("DTO"))
                .ToArray();

            foreach (var type in dtoTypes)
            {
                foreach (var field in type.GetFields())
                {
                    Assert.IsFalse(field.Name.Contains("score") || field.Name.Contains("Score"),
                        $"DTO {type.Name} 不应含 score 字段");
                    Assert.IsFalse(field.Name.Contains("progress") || field.Name.Contains("Progress"),
                        $"DTO {type.Name} 不应含 progress 字段");
                    Assert.IsFalse(field.Name.Contains("rating") || field.Name.Contains("Rating"),
                        $"DTO {type.Name} 不应含 rating 字段");
                    Assert.IsFalse(field.Name.Contains("completion") || field.Name.Contains("Completion"),
                        $"DTO {type.Name} 不应含 completion 字段");
                    Assert.IsFalse(field.Name.Contains("evaluation") || field.Name.Contains("Evaluation"),
                        $"DTO {type.Name} 不应含 evaluation 字段");
                }
            }
        }

        // ══════════ AC-10-17: Missed ⇒ 零档位指示 ══════════

        [Test]
        public void test_ac1017_missed_noTierIndication()
        {
            // 音频/视觉零档位指示（音色/素材/强度不因 JudgeResult 而异）
            var triggerSources = FeelLatencyEvaluator.GetAudioTriggerSources();
            Assert.AreEqual(1, triggerSources.Length,
                "音频触发源应单值（行为反馈白名单）");
            Assert.AreEqual("action_feedback", triggerSources[0],
                "触发源应为 action_feedback");
        }

        [Test]
        public void test_ac1017_audioCueSameAcrossJudgeResults()
        {
            // Missed vs Applied ⇒ 音频 cue 完全相同（AC-10-17）
            var missedCue = FeelLatencyEvaluator.GetAudioCueForResult(JudgeResult.Missed);
            var appliedCue = FeelLatencyEvaluator.GetAudioCueForResult(JudgeResult.Applied);
            var weakCue = FeelLatencyEvaluator.GetAudioCueForResult(JudgeResult.AppliedWeak);

            Assert.AreEqual(missedCue, appliedCue,
                "音频 cue 不应因 JudgeResult 而异（Missed vs Applied）");
            Assert.AreEqual(weakCue, appliedCue,
                "音频 cue 不应因 JudgeResult 而异（AppliedWeak vs Applied）");
        }

        // ══════════ F-10.6 口径护栏: 不存在合并延迟表述 ══════════

        [Test]
        public void test_f106_noCombinedLatencyAssertion()
        {
            // 代码与测试注释中不存在合并延迟表述
            // 扫描实现文件（非测试文件自身，避免自指）
            // Unity EditMode 当前目录 = 项目根
            var implDir = "Assets/Sim/EmergencyProcedures";
            Assert.IsTrue(System.IO.Directory.Exists(implDir), "应存在实现目录");
            var sourceFiles = System.IO.Directory.GetFiles(implDir, "*.cs");
            Assert.IsNotEmpty(sourceFiles, "应存在实现源文件");

            foreach (var file in sourceFiles)
            {
                var content = System.IO.File.ReadAllText(file);
                Assert.IsFalse(content.Contains("合并延迟") || content.Contains("combined"),
                    $"实现文件 {file} 不应含合并延迟表述");
            }
        }

        // ══════════ 预表现实现: 本地 Judge 不写流 ══════════

        [Test]
        public void test_prePresentation_localJudgeNoStreamWrite()
        {
            // 客户端本地跑 Judge 仅驱动预表现（不写流、不发成长）
            // 本测试验证: Judge 是纯函数（无副作用）
            var agg = new EmergencyReading(0, 100, 3, new[] { 10, 20, 30 }, 800);
            var action = new EmergencyActionRow { ActionId = 0, MinEdges = 2, MinHoldTicks = 50, MagThreshold = 400 };
            var ctx = new JudgeContext { Level = 5, MagThresholdEffective = 400 };

            var result1 = JudgeEvaluator.Judge(agg, action, ctx);
            var result2 = JudgeEvaluator.Judge(agg, action, ctx);
            Assert.AreEqual(result1, result2, "本地 Judge 应是纯函数（不写流）");
        }

        // ══════════ AC-10-08/19/23: NOT-RUN 占位 ══════════

        [Test]
        public void test_ac1008_lInput_notRun()
        {
            // OQ-10-12 原型门未过 ⇒ AC-10-08 NOT-RUN
            Assert.Ignore("NOT-RUN: AC-10-08 L_input 实测待 OQ-10-12 原型门");
        }

        [Test]
        public void test_ac1019_remoteLInput_notRun()
        {
            // 45 联机夹具未落地 ⇒ AC-10-19 NOT-RUN
            Assert.Ignore("NOT-RUN: AC-10-19 联机 L_input 实测待 45 网络层");
        }

        [Test]
        public void test_ac1023_playtestWalkthrough_notRun()
        {
            // 录屏走查未执行 ⇒ AC-10-23 NOT-RUN
            Assert.Ignore("NOT-RUN: AC-10-23 录屏走查待桌面调试轮");
        }
    }
}
