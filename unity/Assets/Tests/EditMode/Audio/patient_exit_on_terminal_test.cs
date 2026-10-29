// 权威来源:docs/tech-debt-register.md `TD-listen-01` 的 **L-1**(无病人离场路径)
//   · 裁定链:2026-10-29 用户两轮裁定「表现侧终态锁存驱动」 → 首版实测 6 红(与 AC-019-6 冲突)
//     → 改判**甲案**:移除**推迟到尾音收口之后**。
//   · 相关 AC:AC-019-6(「终态持续期间尾音只衰减一次、永不重启」—— 它要求病人**继续在场**)
//
// ⚠️ **本夹具钉的是「终态 ⇒ 离场」这一件事**,两侧都断:
//   ① 终态 ∧ 无呼吸句柄 ⇒ **必须**离场(停用 `_exitScratch` 的排入 ⇒ 本测恰红);
//   ② 非终态 ⇒ **不得**离场(把终态判据写成恒真 ⇒ 本测恰红)。
//   只断一侧 = 把「什么都不做」也判成通过。
//
// ⚠️ 测试纪律:arrange/act/assert · 无随机 · 无时间依赖 · 零文件系统 I/O。

using System;
using System.Collections.Generic;
using NUnit.Framework;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.PatientAI;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;

namespace DaYiJingCheng.Tests.Unit.Audio
{
    /// <summary>`TD-listen-01` L-1 甲案:终态病人在尾音收口后离场。</summary>
    [TestFixture]
    public sealed class PatientExitOnTerminalTest
    {
        private const int PatientIdValue = 7;

        /// <summary>可编程体征源(生产缝 = `IVitalsQuery`)。</summary>
        private sealed class RiggedVitals : IVitalsQuery
        {
            public float Position;
            public RiggedVitals(float position) => Position = position;
            public VitalsDto GetVitals(PatientId p) => new VitalsDto(Position, 0f, 0, 0);
        }

        /// <summary>空壳 sink:本夹具不判 cue,只让驱动可装配。</summary>
        private sealed class NoopSink : IAudioCueSink
        {
            public AudioCueHandle BeginLoop(in AudioCueDto cue) => new AudioCueHandle(1);
            public void EndLoop(AudioCueHandle handle) { }
            public void Emit(in AudioCueDto cue) { }
            public void SetTier(TierSource source) { }
        }

        /// <summary>造一个**不起呼吸**的驱动(`breathCue: null` ⇒ 无呼吸句柄 ⇒ 终态即刻满足移除判据)。</summary>
        private static PatientCueDriver Driver(IVitalsQuery vitals, PresenceRegistry presence)
            => new PatientCueDriver(
                new NoopSink(), vitals, presence,
                new PatientBehaviorDirector(vitals, BehaviorBands.Default),
                coughCue: null, breathCue: null, knowsClinic: _ => true);

        [Test]
        public void test_terminalPatient_leavesPresence_afterTailSettles()
        {
            // Arrange:病人已登记在场,体征落在死亡带 ⇒ 13 的终态锁存 (BehaviorMap.EvaluateTerminal)
            var presence = new PresenceRegistry();
            var id = new PatientId(PatientIdValue);
            presence.AddPresent(id, new WorldPos(0, 0, 0));
            var vitals = new RiggedVitals(BehaviorBands.Default.DeathBandMin);

            // Act:一拍(无呼吸句柄 ⇒ 移除判据即刻成立,尾音队列为空)
            Driver(vitals, presence).OnTickEdge(0);

            // Assert
            var present = new List<int>(presence.PresentPatientIds());
            Assert.That(present.Contains(PatientIdValue), Is.False,
                "终态 ∧ 尾音已收口 ⇒ 必须离场(L-1 甲案)。仍含该 id = 离场路径没接上");
        }

        [Test]
        public void test_livePatient_staysInPresence()
        {
            // Arrange:体征在死带**之下**(非终态)
            var presence = new PresenceRegistry();
            var id = new PatientId(PatientIdValue);
            presence.AddPresent(id, new WorldPos(0, 0, 0));
            var vitals = new RiggedVitals(0.5f);

            // Act
            Driver(vitals, presence).OnTickEdge(0);

            // Assert:反过来钉住 —— 判据错写成「一律移除」时本测恰红
            var present = new List<int>(presence.PresentPatientIds());
            Assert.That(present.Contains(PatientIdValue), Is.True,
                "非终态病人**不得**被移出在场集 —— 否则是「一律移除」而非「终态离场」");
        }
    }
}
