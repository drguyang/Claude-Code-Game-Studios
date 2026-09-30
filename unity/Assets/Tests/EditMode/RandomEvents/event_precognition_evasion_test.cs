// random-events Story 005 测试
//
// AC-52-12: 无直降
// AC-52-13/15: 避险一等公民
// AC-52-16: 可脱离
// AC-52-18: 因果线索
// AC-52-19: 零数值

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.RandomEvents
{
    public class EventPrecognitionEvasionTest
    {
        // AC-52-12: 无直降
        [Test]
        public void test_noDirectDrop()
        {
            var states = new List<PrecognitionState>
            {
                new PrecognitionState(1, 100, 200, "clue_1"),
                new PrecognitionState(2, 300, 400, "clue_2"),
            };

            Assert.IsTrue(EventPrecognition.ValidateNoDirectDrop(states),
                "每个降临必须有先行的预告");
        }

        // AC-52-12: 直降检测
        [Test]
        public void test_directDropDetected()
        {
            var states = new List<PrecognitionState>
            {
                new PrecognitionState(1, 200, 100, "clue_1"), // 降临在预告之前
            };

            Assert.IsFalse(EventPrecognition.ValidateNoDirectDrop(states),
                "降临在预告之前应被检测为直降");
        }

        // AC-52-13/15: 避险一等公民
        [Test]
        public void test_shouldEvade_threeConditions()
        {
            Assert.IsTrue(EventPrecognition.ShouldEvade(true, true, true),
                "在医馆 ∧ 在出诊路径上 ∧ 处于危险 ⇒ 避险");
            Assert.IsFalse(EventPrecognition.ShouldEvade(false, true, true),
                "不在医馆 ⇒ 不避险");
            Assert.IsFalse(EventPrecognition.ShouldEvade(true, false, true),
                "不在出诊路径上 ⇒ 不避险");
            Assert.IsFalse(EventPrecognition.ShouldEvade(true, true, false),
                "不处于危险 ⇒ 不避险");
        }

        // AC-52-16: 可脱离
        [Test]
        public void test_canDisengage()
        {
            Assert.IsTrue(EventPrecognition.CanDisengage(false, false),
                "未被包围 ⇒ 可脱离");
            Assert.IsTrue(EventPrecognition.CanDisengage(true, true),
                "有撤退路径 ⇒ 可脱离");
            Assert.IsFalse(EventPrecognition.CanDisengage(true, false),
                "被包围且无撤退路径 ⇒ 不可脱离");
        }

        // AC-52-18: 因果线索
        [Test]
        public void test_causeClueKey_nonEmpty()
        {
            var cue = EventPrecognition.CreateCue(1, "storm_warning", 0);
            Assert.IsNotNull(cue.CauseClueKey);
            Assert.IsNotEmpty(cue.CauseClueKey);
        }

        // AC-52-19: 零数值
        [Test]
        public void test_clueKey_noNumeric()
        {
            Assert.IsTrue(EventPrecognition.ValidateNoNumeric("storm_warning"),
                "线索 key 不应包含数字");
            Assert.IsFalse(EventPrecognition.ValidateNoNumeric("storm_1"),
                "包含数字的 key 应被拒绝");
        }

        // 线索 DTO 创建
        [Test]
        public void test_createCue()
        {
            var cue = EventPrecognition.CreateCue(1, "flood_warning", 1);
            Assert.AreEqual(1, cue.EventKey);
            Assert.AreEqual("flood_warning", cue.CauseClueKey);
            Assert.AreEqual(1, cue.TierKind);
        }
    }
}
