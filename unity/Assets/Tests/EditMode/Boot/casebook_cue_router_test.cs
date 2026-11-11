// T22 批十一 · 2026-11-11 —— `CasebookCueRouter` 判据(`AC-39-12` 第一半的装配翻译面)。
//
// 判据(可证伪):
//   ① 压痕请求 ⇒ 发出**压痕** ordinal(不是翻页的),强度 255,声源 = -1,一次性;
//   ② 翻页请求 ⇒ 发出翻页 ordinal;
//   ③ 空间锚 = 注入的 `cellOf`(玩家格),不是 (0,0,0);
//   ④ ordinal = NoOrdinal(表里没该行)⇒ **不发**,且 `ResolvedCueCount` 如实少一;
//   ⑤ 构造器拒 null 依赖(fail-loud,不静默吞声)。

using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.Presentation.Audio;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.EditMode.Boot
{
    internal sealed class casebook_cue_router_test
    {
        private sealed class RecordingSink : IAudioCueSink
        {
            public readonly List<AudioCueDto> Cues = new List<AudioCueDto>();
            public void Emit(in AudioCueDto cue) => Cues.Add(cue);
            public AudioCueHandle BeginLoop(in AudioCueDto cue) => default;
            public void EndLoop(AudioCueHandle handle) { }
            public void SetTier(TierSource source) { }
        }

        private const int PageTurn = 11;
        private const int Indent = 20;

        [Test]
        public void test_paperIndent_emitsIndentOrdinal_withUiShape()
        {
            var sink = new RecordingSink();
            var router = new CasebookCueRouter(sink, () => new Int3(14, 0, 14), PageTurn, Indent);

            router.RequestPaperIndent();

            Assert.AreEqual(1, sink.Cues.Count, "一次请求 ⇒ 恰一条 cue");
            AudioCueDto cue = sink.Cues[0];
            Assert.AreEqual(Indent, cue.Cue, "发出的须是**压痕** ordinal,不是翻页的");
            Assert.AreEqual(255, cue.Intensity, "UI 声强度恒 255(不随距离 / 状态变化)");
            Assert.AreEqual(-1, cue.Source, "UI 声非实体声源(ADR-018 §四)");
            Assert.IsFalse(cue.Looped, "纸面反馈是一次性 cue");
        }

        [Test]
        public void test_pageTurn_emitsPageTurnOrdinal()
        {
            var sink = new RecordingSink();
            var router = new CasebookCueRouter(sink, () => new Int3(0, 0, 0), PageTurn, Indent);

            router.RequestPageTurn();

            Assert.AreEqual(1, sink.Cues.Count);
            Assert.AreEqual(PageTurn, sink.Cues[0].Cue);
        }

        [Test]
        public void test_cell_comesFromInjectedProvider_notOrigin()
        {
            var sink = new RecordingSink();
            var router = new CasebookCueRouter(sink, () => new Int3(9, 0, 3), PageTurn, Indent);

            router.RequestPageTurn();

            AudioCueDto cue = sink.Cues[0];
            Assert.AreEqual(9, cue.Cell.X, "空间锚须是玩家当前格(锚在原点会被距离衰减吃光)");
            Assert.AreEqual(3, cue.Cell.Z);
        }

        [Test]
        public void test_unresolvedOrdinal_emitsNothing_andCountIsHonest()
        {
            var sink = new RecordingSink();
            var router = new CasebookCueRouter(sink, () => new Int3(1, 0, 1),
                PageTurn, AudioEventDataSet.NoOrdinal);

            Assert.AreEqual(1, router.ResolvedCueCount, "只有翻页可发(压痕表行不存在)");
            router.RequestPaperIndent();
            Assert.AreEqual(0, sink.Cues.Count, "表里没有该行 ⇒ 不发死 cue");

            router.RequestPageTurn();
            Assert.AreEqual(1, sink.Cues.Count, "已解析的那条照常发");
        }

        [Test]
        public void test_nullAudio_failsLoud()
        {
            Assert.Throws<System.ArgumentNullException>(
                () => new CasebookCueRouter(null, () => default, PageTurn, Indent),
                "无 44 入向面 ⇒ 拒构造(不得以 null 冒充「本局静音」)");
        }

        [Test]
        public void test_nullCellProvider_failsLoud()
        {
            Assert.Throws<System.ArgumentNullException>(
                () => new CasebookCueRouter(new RecordingSink(), null, PageTurn, Indent));
        }
    }
}