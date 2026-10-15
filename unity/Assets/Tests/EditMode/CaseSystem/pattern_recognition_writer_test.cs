// M2 接线轮 · 阶段 B —— PatternRecognized 写者(37)端到端测试(EditMode,真装配,禁 Fake)。
//
// 覆盖:
//   ① 真装配袋(真 EventStream + 真 PayloadEncoder + 真 blob 池)调用写者 ⇒ 每个达标 D 恰写 1 条
//      PatternRecognized;Kind / 流别 / 载荷 round-trip / 事件头逐项断言;
//   ② anchor_case = 成员集**末位**(跨过阈值那一例)· member_set 的病种分量 = 该 D 本身;
//   ③ **同 tick 多 D 按 salted_key 升序写**(AC-37-27)—— 夹具刻意令「流中首现序」与「期望序」
//      **相反**(TB-1 病灶的对策:删 PatternDetector.SortBySaltedKey 即红);
//   ④ 前置:不足阈值 ⇒ 零写入;fires-once(已发 / 第 4 例)⇒ 永不重发(幂等分支);
//   ⑤ 边界:纯伤情案(病种空集)不入检测;共病一案进多组;负 tick / 空依赖 / 非正阈值 fail-loud;
//   ⑥ **同 tick 批量跨阈值(S12-6,2026-10-15)** —— 恰一条 / 重复调用幂等 / 同 tick 后续病例不重发 /
//      恰达阈边界 / 同 tick 异构 D / 链外不占名额 / 阈值 2 放行(判据真源 = AC-37-29 写者侧)。
//
// 权威:design/gdd/case-system.md F-37.1 / F-37.1b / AC-37-07/08/09/27/29 ·
//      design/registry/entities.yaml SimEvent.Kind.PatternRecognized(stream: case · author: 37)·
//      ADR-008 §一/§三/§五 · ADR-007 §二/§四 · ADR-029 §③ · ADR-025 §①
//
// ⚠️ 铺流不变量(fixture 易踩 —— 2026-10-15 首版曾全红于此):
//    `EventStream.Append` 硬不变量 = **流按 Tick 非降**(违约 fail-loud,BCD-码-3)。
//    而 `PatientSpawner.SpawnNext(tick: 0)` **自身即 Append 一条 tick=0 的事件**。
//    ⇒ 铺流纪律:**所有 SpawnPatients 必须在任何 CaseClosed 之前一次铺完**(全部 tick 0),
//       其后所有 CaseClosed 按 tick **单调非降** append。
//       (下方各测的「先 SpawnPatients(n) 再逐条 AppendClosed」即兑现此纪律。)
//
//    ⚠️ **S12-6 的同 tick 各测例外**:它们直接构造 `PatientId` 字面量、**不经 `SpawnPatients`**
//       (故不 Append 任何 DiseaseOnset),全部 CaseClosed 用**同一 tick** ⇒ 非降律平凡满足。
//       这是刻意的:同 tick 批量形态要求「插入序」可控,而 `SpawnNext` 按 `NextPatientId()`
//       单调发号 ⇒ 无法产出逆全序的插入序(判别力会归零)。
//
// ⚠️ 两个解码委托(diseaseExtractor / saltedKeyExtractor)由**本测试(装配侧)**给出 ——
//    解码归 Sim.Codec,Sim 引用集禁引(ADR-025 §①);写者只按 PatternDetector.NextEmissions
//    原签名透传,不由写者改写「要发哪些」的列表(否则 CB-1 病灶复发)。

using System;
using System.Collections.Generic;
using System.Linq;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.CaseSystem
{
    /// <summary><see cref="PatternRecognitionWriter"/> 生产写入通道测试。</summary>
    [TestFixture]
    public sealed class PatternRecognitionWriterTest
    {
        private CompositionRootServices _bag;
        private PatternRecognitionWriter _writer;

        private const long WorldSeed = 123456789L;
        private const int Threshold = 3;   // P0(GDD F-37.1)
        private const int Version = 7;     // ordinal 映射表版本(夹具内容值)

        [SetUp]
        public void Setup()
        {
            _bag = CompositionRoot.Assemble();
            _writer = new PatternRecognitionWriter(
                _bag.EventSink, _bag.Encoder, _bag.Decoder, _bag.Stream.Events,
                WorldSeed, Threshold,
                scriptedChains: null, version: Version);
        }

        // ══════════ 断言辅助(2026-10-15 接线轮:写者已持 _bag.Decoder 自解码;
        //   本处两个委托已从构造器签名删除,只留**断言侧**回读)══════════

        private int[] DiseaseOrdinalsOf(SimEvent e)
        {
            var p = _bag.Decoder.Decode<CaseClosedPayload>(EventKind.CaseClosed, e.Payload);
            var ordinals = new List<int>();
            ulong bits = p.DiseaseSet.Bits;              // DiseaseIdSet 只暴露 Bits(bitmask)
            for (int i = 0; i < 64; i++)
                if ((bits & (1UL << i)) != 0) ordinals.Add(i);
            return ordinals.ToArray();
        }

        private ulong SaltedKeyOf(SimEvent e)
            => _bag.Decoder.Decode<PatternRecognizedPayload>(
                EventKind.PatternRecognized, e.Payload).SaltedKey;

        // ══════════ 夹具(真生产路径;铺流纪律见文件头 ⚠️)══════════

        /// <summary>一次铺完全部病人(tick 0)—— **必须**在任何 CaseClosed 之前调用
        /// (SpawnNext 自身 Append 一条 tick=0 事件,交错调用即违流非降不变量)。</summary>
        private PatientId[] SpawnPatients(int count)
        {
            var patients = new PatientId[count];
            for (int i = 0; i < count; i++)
                patients[i] = _bag.PatientSpawner.SpawnNext(diseaseId: 0, tick: 0);
            return patients;
        }

        /// <summary>铺一条 CaseClosed(tick 须 ≥ 已入流最大 tick —— 各测按升序调用)。</summary>
        private void AppendClosed(PatientId p, long tick, DiseaseIdSet diseaseSet)
        {
            var payload = new CaseClosedPayload(p.Value, new CaseId(tick, p.Value, 0), diseaseSet, true);
            _bag.EventSink.Append(new SimEvent(
                tick, p, -1, EventKind.CaseClosed, _bag.Encoder.Encode(EventKind.CaseClosed, payload)));
        }

        private static DiseaseIdSet Only(int diseaseOrdinal) => new DiseaseIdSet(1UL << diseaseOrdinal);

        private List<SimEvent> FiredEvents()
            => _bag.Stream.Events.Where(e => e.Kind == EventKind.PatternRecognized).ToList();

        private int PatternRecognizedCount() => FiredEvents().Count;

        /// <summary>从 PatternRecognized 载荷的 member_set 病种分量取出唯一 ordinal({D})。</summary>
        private int OrdinalOf(SimEvent e)
        {
            PayloadCodec.TryGetPayload<PatternRecognizedPayload>(e, _bag.BlobPool, out var p);
            ulong bits = p.MemberDiseaseSet.Bits;
            for (int i = 0; i < 64; i++)
                if ((bits & (1UL << i)) != 0) return i;
            return -1;
        }

        private CaseId AnchorOf(SimEvent e)
        {
            PayloadCodec.TryGetPayload<PatternRecognizedPayload>(e, _bag.BlobPool, out var p);
            return p.AnchorCase;
        }

        // ══════════ ① 真装配:恰增 1 条 + 全字段 round-trip ══════════

        [Test]
        public void test_patternRecognized_realBag_writesOneEvent_roundTrip()
        {
            const int d = 0;
            var ps = SpawnPatients(3);
            // 3 例同 D,全序升(patient@1, @2, @3)⇒ 末位 = @3 = 跨过阈值那一例
            AppendClosed(ps[0], 1, Only(d));
            AppendClosed(ps[1], 2, Only(d));
            AppendClosed(ps[2], 3, Only(d));

            var lastClosed = _bag.Stream.Events.Where(e => e.Kind == EventKind.CaseClosed).Last();
            var expectedAnchor = new CaseId(lastClosed.Tick, lastClosed.Patient.Value, lastClosed.Seq);

            int beforeAll = _bag.Stream.Count;
            var result = _writer.TryRecognize(4);

            Assert.AreEqual(PatternRecognitionWriteResult.Recognized, result, "达标须真正写入");
            Assert.AreEqual(beforeAll + 1, _bag.Stream.Count, "恰增 1 条");

            var evt = _bag.Stream.Events[_bag.Stream.Count - 1];
            Assert.AreEqual(EventKind.PatternRecognized, evt.Kind);
            Assert.AreEqual(StreamId.Case, StreamRouting.Of(evt.Kind), "落病例流(ADR-008 §一)");
            Assert.AreEqual(PatientId.None, evt.Patient, "世界级事件 ⇒ Patient = None(ADR-007 §四)");
            Assert.AreEqual(-1, evt.Patient.Value, "None 值 = -1,不污染 max(patient_id)");
            Assert.AreEqual(4L, evt.Tick, "本事件自身 Tick = 当前 tick");
            Assert.AreEqual(0L, evt.Seq, "该 (Tick, Patient=None) 首号 = 0(AC-37-27 起序)");

            // ── 载荷 round-trip(字节真进池)──
            Assert.IsTrue(
                PayloadCodec.TryGetPayload<PatternRecognizedPayload>(evt, _bag.BlobPool, out var decoded),
                "载荷须能经池取回(手搓伪引用会在此 false)");
            Assert.AreEqual(PatientId.None.Value, decoded.PatientId, "payload.patient_id = -1(世界级)");
            Assert.AreEqual(expectedAnchor, decoded.AnchorCase,
                "anchor_case = 成员集末位(跨过阈值那一例,ADR-008 §三)");
            Assert.AreEqual(PatternDetector.ComputeSaltedKey(WorldSeed, d), decoded.SaltedKey,
                "salted_key = 逐 D 派生(ADR-008 §五)");
            Assert.AreEqual(new DiseaseIdSet(1UL << d), decoded.MemberDiseaseSet,
                "member_set 病种分量 = {D}(逐 D 一条)");
            Assert.AreEqual(Version, decoded.Version, "ordinal 映射表版本");
        }

        // ══════════ ③ 同 tick 多 D:按 salted_key 升序 ══════════

        [Test]
        public void test_patternRecognized_multiFired_writtenInSaltedKeyAscendingOrder()
        {
            // 选两 D,使 salted 序 与 流中首现序 **相反** ⇒ 删 SortBySaltedKey 即红(TB-1 对策:
            // 夹具的输入序 ≠ 期望序,而非 TB-1 的「夹具期望序 ≡ 插入序」)
            ulong k0 = PatternDetector.ComputeSaltedKey(WorldSeed, 0);
            ulong k1 = PatternDetector.ComputeSaltedKey(WorldSeed, 1);
            int dLow = k0 < k1 ? 0 : 1;
            int dHigh = k0 < k1 ? 1 : 0;

            var ps = SpawnPatients(6);
            // 流序:salted **大**者先出现(刻意与期望的升序相反);tick 单调非降(1…6)
            AppendClosed(ps[0], 1, Only(dHigh));
            AppendClosed(ps[1], 2, Only(dHigh));
            AppendClosed(ps[2], 3, Only(dHigh));
            AppendClosed(ps[3], 4, Only(dLow));
            AppendClosed(ps[4], 5, Only(dLow));
            AppendClosed(ps[5], 6, Only(dLow));

            var result = _writer.TryRecognize(7);

            Assert.AreEqual(PatternRecognitionWriteResult.Recognized, result);
            var fired = FiredEvents();
            Assert.AreEqual(2, fired.Count, "两 D 同 tick 各发一条");

            Assert.Less(SaltedKeyOf(fired[0]), SaltedKeyOf(fired[1]),
                "同 tick 多 D 按 salted_key 整数升序写(AC-37-27)");
            Assert.AreEqual(dLow, OrdinalOf(fired[0]), "小 salted 者先 —— 输入序已刻意与之相反");
            Assert.AreEqual(dHigh, OrdinalOf(fired[1]), "大 salted 者后");
        }

        // ══════════ ④ 前置:阈值 / fires-once ══════════

        [Test]
        public void test_patternRecognized_belowThreshold_noWrite()
        {
            var ps = SpawnPatients(2);
            AppendClosed(ps[0], 1, Only(0));   // 每 D 只 2 例(< 阈值 3,AC-37-07)
            AppendClosed(ps[1], 2, Only(0));

            int before = _bag.Stream.Count;
            var result = _writer.TryRecognize(3);

            Assert.AreEqual(PatternRecognitionWriteResult.BelowThreshold, result,
                "长度 < 阈值不触发(GDD F-37.1)");
            Assert.AreEqual(before, _bag.Stream.Count, "零写入");
            Assert.AreEqual(0, PatternRecognizedCount());
        }

        [Test]
        public void test_patternRecognized_secondCall_firesOnce_noResend()
        {
            const int d = 0;
            var ps = SpawnPatients(4);   // 第 4 位留作「第 4 例」(tick 后置)
            AppendClosed(ps[0], 1, Only(d));
            AppendClosed(ps[1], 2, Only(d));
            AppendClosed(ps[2], 3, Only(d));
            Assert.AreEqual(PatternRecognitionWriteResult.Recognized, _writer.TryRecognize(4), "首触发");
            Assert.AreEqual(1, PatternRecognizedCount());

            var anchorBefore = AnchorOf(FiredEvents()[0]);

            // 第 4 例(同 D)到位 —— 已发 ⇒ 永不重发(GDD F-37.1b / AC-37-09)
            AppendClosed(ps[3], 5, Only(d));
            int beforeSecond = _bag.Stream.Count;

            var again = _writer.TryRecognize(6);

            Assert.AreEqual(PatternRecognitionWriteResult.AlreadyRecognized, again,
                "达标但已发过 ⇒ 幂等分支(GDD F-37.1b «一次性»)");
            Assert.AreEqual(beforeSecond, _bag.Stream.Count, "零写入");
            Assert.AreEqual(1, PatternRecognizedCount(), "第 4 例不重发 —— 仍只 1 条");

            // member_set 冻结于触发 tick,不随第 4 例增长(条目未追加、原锚点不变)
            Assert.AreEqual(anchorBefore, AnchorOf(FiredEvents()[0]), "anchor_case 冻结不变");
        }

        // ══════════ ⑤ 边界 ══════════

        [Test]
        public void test_patternRecognized_emptyDiseaseSet_contributesToNoGroup()
        {
            var ps = SpawnPatients(5);
            var empty = new DiseaseIdSet(0UL);     // 纯伤情案:病种空集
            AppendClosed(ps[0], 1, empty);
            AppendClosed(ps[1], 2, empty);
            AppendClosed(ps[2], 3, empty);
            AppendClosed(ps[3], 4, Only(0));       // 该 D 仍只 2 例
            AppendClosed(ps[4], 5, Only(0));

            int before = _bag.Stream.Count;
            var result = _writer.TryRecognize(6);

            Assert.AreEqual(PatternRecognitionWriteResult.BelowThreshold, result,
                "纯伤情案(病种空集)不入检测,不凑数(GDD F-37.1)");
            Assert.AreEqual(before, _bag.Stream.Count, "零写入");
        }

        [Test]
        public void test_patternRecognized_comorbidCase_countsInEachDiseaseGroup()
        {
            const int a = 0, b = 1;
            var ps = SpawnPatients(5);
            // P1 共病{A,B} ⇒ 同时进 A 组与 B 组(各占一名额)
            AppendClosed(ps[0], 1, new DiseaseIdSet((1UL << a) | (1UL << b)));
            AppendClosed(ps[1], 2, Only(a));
            AppendClosed(ps[2], 3, Only(a));
            AppendClosed(ps[3], 4, Only(b));
            AppendClosed(ps[4], 5, Only(b));

            var result = _writer.TryRecognize(6);

            Assert.AreEqual(PatternRecognitionWriteResult.Recognized, result);
            Assert.AreEqual(2, PatternRecognizedCount(), "共病一案进多组:A / B 各成一图样");
        }

        [Test]
        public void test_patternRecognized_negativeTick_throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _writer.TryRecognize(-1),
                "tick 入参非法 = 编程错误,fail-loud(与 CaseOpenWriter 同口径)");
        }

        // ══════════ 注入面 fail-loud ══════════

        [Test]
        public void test_patternRecognitionWriter_nullDependency_throws()
        {
            Assert.Throws<ArgumentNullException>(() => new PatternRecognitionWriter(
                null, _bag.Encoder, _bag.Decoder, _bag.Stream.Events, WorldSeed, Threshold,
                null, Version));
            Assert.Throws<ArgumentNullException>(() => new PatternRecognitionWriter(
                _bag.EventSink, null, _bag.Decoder, _bag.Stream.Events, WorldSeed, Threshold,
                null, Version));
            Assert.Throws<ArgumentNullException>(() => new PatternRecognitionWriter(
                _bag.EventSink, _bag.Encoder, null, _bag.Stream.Events, WorldSeed, Threshold,
                null, Version));
        }

        /// <summary>阈值下界护栏:0 / -1 / **1** 一律拒(GDD 旋钮安全范围 ≥ 2)。
        /// <para>⚠️ <b>2026-10-15 TA-8(ADVISORY 清理轮)</b> —— 原稿只测 0 / -1,而构造器当时
        /// 也只校验 <c>&lt; 1</c> ⇒ **阈值 1 可通过**,与 GDD `case-system.md:1004`
        /// 「安全范围 ≥ 2(**&lt; 2 构建期拒绝**)」不符(护栏漏配)。现构造器改 <c>&lt; 2</c>,
        /// 本测补 <b>1</b> 这一格。</para>
        /// <para><b>可捕获的变异</b>:构造器守卫改回 <c>&lt; 1</c> ⇒ 第三条断言红。</para>
        /// </summary>
        [Test]
        public void test_patternRecognitionWriter_thresholdBelowTwo_throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new PatternRecognitionWriter(
                _bag.EventSink, _bag.Encoder, _bag.Decoder, _bag.Stream.Events, WorldSeed, 0,
                null, Version), "阈值 0 须拒");
            Assert.Throws<ArgumentOutOfRangeException>(() => new PatternRecognitionWriter(
                _bag.EventSink, _bag.Encoder, _bag.Decoder, _bag.Stream.Events, WorldSeed, -1,
                null, Version), "阈值 -1 须拒");
            Assert.Throws<ArgumentOutOfRangeException>(() => new PatternRecognitionWriter(
                _bag.EventSink, _bag.Encoder, _bag.Decoder, _bag.Stream.Events, WorldSeed, 1,
                null, Version),
                "阈值 1 须拒 —— GDD 旋钮安全范围 ≥ 2(原构造器只拒 < 1,漏配)");
        }

        // ══════════════════════════════════════════════════════════════════
        // 【S12-6 · 2026-10-15】同 tick 批量跨阈值 —— AC-37-29 的**写者侧**
        //
        // 缺口来源:`fix-s11-17-case-system-advisories-2026-10-15.md` §六②-5 具名登记 ——
        //   写者 9 测**全部逐 tick 递增**(见上方各测的 `AppendClosed(ps[i], 1…6, …)`),
        //   「同 tick 批量跨阈值」这一维在**写者侧从未被证伪**。检测器侧由 TA-7 的
        //   `test_ac3734_sameTickFourthCase_membersAreFirstThreeInFullOrder` 覆盖(S11-9 交付);
        //   两者是**分层**关系,不得互相顶替 —— 两者都绿才闭合 AC-37-29。
        //
        // 判据真源:design/gdd/case-system.md:1143 AC-37-29「恰好触发一次,`MemberSet` = 指定的
        //   那三例…**再**让更大的同 D 病例结案,**THEN** 永不重发(一次性,不可重现)」。
        //
        // 铺流注意:同 tick 多例的**全序键** = (Tick, Patient, Seq);同 tick 时 Patient 为第二键
        //   ⇒ 全序 = Patient 升序。各测刻意令**插入序逆全序**(否则成员/锚点断言对「有无排序」
        //   零判别力 —— 承 TB-1 的假绿形态)。
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 同 tick N 例(N &gt; 阈值)一次性跨阈 ⇒ **恰一条**(AC-37-29「恰好触发一次」),
        /// 且成员取全序前 threshold 例、`anchor_case` = 第 threshold 例。
        /// <para><b>可捕获的变异</b>:删 <c>ComputeCandidateSeq</c> 的 <c>candidates.Sort</c> ⇒
        /// 成员次序退回插入序 ⇒ 锚点随插入序变 ⇒ 红(插入序已刻意逆全序)。</para>
        /// </summary>
        [Test]
        public void test_patternRecognized_sameTickBatchOverThreshold_writesExactlyOne()
        {
            const int d = 0;
            const long tick = 10;
            // 插入序 [3,5,1,4,2] —— 逆全序(全序 = Patient 升序 [1,2,3,4,5])
            AppendClosed(new PatientId(3), tick, Only(d));
            AppendClosed(new PatientId(5), tick, Only(d));
            AppendClosed(new PatientId(1), tick, Only(d));
            AppendClosed(new PatientId(4), tick, Only(d));
            AppendClosed(new PatientId(2), tick, Only(d));

            int before = _bag.Stream.Count;
            var result = _writer.TryRecognize(tick);

            Assert.AreEqual(PatternRecognitionWriteResult.Recognized, result,
                "同 tick 五例批量跨阈 ⇒ 达标");
            Assert.AreEqual(before + 1, _bag.Stream.Count, "恰一条(非五条)");
            Assert.AreEqual(1, PatternRecognizedCount());

            var evt = FiredEvents()[0];
            Assert.AreEqual(tick, evt.Tick, "本事件 Tick = 触发 tick(同 tick 多例共享之)");
            Assert.AreEqual(new CaseId(tick, 3, 0), AnchorOf(evt),
                "anchor_case = 全序第 3 例(病人 3)—— 未排序则末位随插入序变 ⇒ 红");
        }

        /// <summary>
        /// **本组的核心判据**:同 tick 批量跨阈后,**重复调用**零新发射。
        /// <para>写者**无 tick 内部状态** —— «已发» 的判据只能来自流中同 <c>salted_key</c> 的
        /// <c>PatternRecognized</c>(<c>IsFired</c> 门)。本测即该门的直接证据。</para>
        /// <para><b>可捕获的变异</b>:删 <c>NextEmissions</c> 内的
        /// <c>if (IsFired(…)) continue;</c> ⇒ 第二次调用再发一条 ⇒ 计数 2 ⇒ 红。</para>
        /// </summary>
        [Test]
        public void test_patternRecognized_sameTickBatchRepeatCall_firesOnce()
        {
            const int d = 0;
            const long tick = 10;
            AppendClosed(new PatientId(1), tick, Only(d));
            AppendClosed(new PatientId(2), tick, Only(d));
            AppendClosed(new PatientId(3), tick, Only(d));

            Assert.AreEqual(PatternRecognitionWriteResult.Recognized, _writer.TryRecognize(tick),
                "首触发");
            Assert.AreEqual(1, PatternRecognizedCount());

            int beforeSecond = _bag.Stream.Count;
            var again = _writer.TryRecognize(tick);

            Assert.AreEqual(PatternRecognitionWriteResult.AlreadyRecognized, again,
                "同 tick 重复调用 ⇒ 幂等分支(判据来自流,非写者内部 tick 记忆)");
            Assert.AreEqual(beforeSecond, _bag.Stream.Count, "零新发射");
            Assert.AreEqual(1, PatternRecognizedCount(),
                "删「一次性」门 ⇒ 本行得 2 ⇒ 红(AC-37-29「恰好触发一次」)");
        }

        /// <summary>
        /// 同 tick 内**后续**病例结案 ⇒ 零新发射(AC-37-29 后半句「再让更大的同 D 病例结案
        /// ⇒ 永不重发」),且 <c>member_set</c> 冻结于首触发。
        /// <para><b>可捕获的变异</b>:删 <c>IsFired</c> 门 ⇒ 本测计数得 2 ⇒ 红。</para>
        /// </summary>
        [Test]
        public void test_patternRecognized_sameTickBatchMoreCases_noResend()
        {
            const int d = 0;
            const long tick = 10;
            AppendClosed(new PatientId(1), tick, Only(d));
            AppendClosed(new PatientId(2), tick, Only(d));
            AppendClosed(new PatientId(3), tick, Only(d));
            Assert.AreEqual(PatternRecognitionWriteResult.Recognized, _writer.TryRecognize(tick));

            // 同 tick 再结案两例同 D(全序更大 —— 即 AC-37-29 的「更大的同 D 病例」)
            AppendClosed(new PatientId(8), tick, Only(d));
            AppendClosed(new PatientId(9), tick, Only(d));

            int before = _bag.Stream.Count;
            var result = _writer.TryRecognize(tick);

            Assert.AreEqual(PatternRecognitionWriteResult.AlreadyRecognized, result,
                "同 tick 内后续病例 ⇒ 永不重发");
            Assert.AreEqual(before, _bag.Stream.Count, "零写入");
            Assert.AreEqual(1, PatternRecognizedCount());
            Assert.AreEqual(new CaseId(tick, 3, 0), AnchorOf(FiredEvents()[0]),
                "member_set 冻结于首触发,不随同 tick 后续病例增长");
        }

        /// <summary>
        /// 同 tick 的**阈值边界两格**:恰 1 例(不发射)与恰 <c>threshold</c> 例(发射 1)。
        /// <para>既有 <see cref="test_patternRecognized_belowThreshold_noWrite"/> 是**跨 tick** 两例;
        /// 本测补**同 tick** 形态的边界下沿。</para>
        /// <para><b>可捕获的变异</b>:达标判据由 <c>&gt;= patternThreshold</c> 改 <c>&gt;</c> ⇒
        /// 恰 3 例不发射 ⇒ 第二条断言红。</para>
        /// </summary>
        [Test]
        public void test_patternRecognized_sameTickExactlyThreshold_writesOne()
        {
            const int d = 0;
            const long tick = 10;
            AppendClosed(new PatientId(1), tick, Only(d));   // 同 tick 仅 1 例

            Assert.AreEqual(PatternRecognitionWriteResult.BelowThreshold, _writer.TryRecognize(tick),
                "同 tick 恰 1 例 ⇒ 不发射(阈值下沿之下)");
            Assert.AreEqual(0, PatternRecognizedCount());

            AppendClosed(new PatientId(2), tick, Only(d));
            AppendClosed(new PatientId(3), tick, Only(d));   // 恰 = Threshold(3)

            Assert.AreEqual(PatternRecognitionWriteResult.Recognized, _writer.TryRecognize(tick),
                "同 tick 恰达阈值 ⇒ 触发(边界下沿,含等号)");
            Assert.AreEqual(1, PatternRecognizedCount(), "恰一条");
            Assert.AreEqual(new CaseId(tick, 3, 0), AnchorOf(FiredEvents()[0]),
                "恰达阈时 anchor = 第 3 例(跨过阈值那一例)");
        }

        /// <summary>
        /// 同 tick **异构 D**:两病种各三例同 tick 结案 ⇒ 各发一条(共 2 条),按 <c>salted_key</c>
        /// 升序(AC-37-27)。
        /// <para><b>可捕获的变异</b>:若「一次性」门误写成 per-tick(而非 per-salted_key),
        /// 同 tick 第二病种被吞 ⇒ 计数 1 ⇒ 红。</para>
        /// </summary>
        [Test]
        public void test_patternRecognized_sameTickMultiDisease_writesOneEach()
        {
            const int a = 0, b = 1;
            ulong ka = PatternDetector.ComputeSaltedKey(WorldSeed, a);
            ulong kb = PatternDetector.ComputeSaltedKey(WorldSeed, b);
            int dLow = ka < kb ? a : b;
            int dHigh = ka < kb ? b : a;

            const long tick = 10;
            // 插入序:大 salted 者先出现(逆期望的升序)
            AppendClosed(new PatientId(1), tick, Only(dHigh));
            AppendClosed(new PatientId(2), tick, Only(dHigh));
            AppendClosed(new PatientId(3), tick, Only(dHigh));
            AppendClosed(new PatientId(4), tick, Only(dLow));
            AppendClosed(new PatientId(5), tick, Only(dLow));
            AppendClosed(new PatientId(6), tick, Only(dLow));

            var result = _writer.TryRecognize(tick);

            Assert.AreEqual(PatternRecognitionWriteResult.Recognized, result);
            var fired = FiredEvents();
            Assert.AreEqual(2, fired.Count, "同 tick 两 D 各一条(非六条)");
            Assert.Less(SaltedKeyOf(fired[0]), SaltedKeyOf(fired[1]),
                "同 tick 多 D 按 salted_key 整数升序写(AC-37-27)");
            Assert.AreEqual(dLow, OrdinalOf(fired[0]), "小 salted 者先 —— 插入序已刻意与之相反");
            Assert.AreEqual(dHigh, OrdinalOf(fired[1]), "大 salted 者后");
        }

        /// <summary>
        /// AC-37-29(定义域收窄)的**写者侧**:同 tick 下链外同 D 病例**不消耗**脚本链名额。
        /// <para>链外病例的 <c>patient_id</c> 刻意取更小值(1 / 2 &lt; 链成员 10/11/12)⇒
        /// 若收窄失效,它们会**抢占全序前三**。</para>
        /// <para><b>可捕获的变异</b>:删 <c>ComputeCandidateSeq</c> 内的
        /// <c>if (ChainEligible.TryChainOf(…)) { …narrowed… }</c> 块 ⇒ 成员混入病人 1/2
        /// ⇒ 锚点由 12 变 10 ⇒ 红。</para>
        /// </summary>
        [Test]
        public void test_patternRecognized_chainQuotaSameTickRandomCases_notConsumed()
        {
            const int d = 0;
            const long tick = 10;
            var chains = new[] { new ScriptedChain(d, new[] { 10, 11, 12 }) };
            var writer = new PatternRecognitionWriter(
                _bag.EventSink, _bag.Encoder, _bag.Decoder, _bag.Stream.Events,
                WorldSeed, Threshold, chains, Version);

            // 链外同 D 两例(全序更小 ⇒ 无收窄则必抢占名额)
            AppendClosed(new PatientId(1), tick, Only(d));
            AppendClosed(new PatientId(2), tick, Only(d));
            // 链成员三例
            AppendClosed(new PatientId(10), tick, Only(d));
            AppendClosed(new PatientId(11), tick, Only(d));
            AppendClosed(new PatientId(12), tick, Only(d));

            var result = writer.TryRecognize(tick);

            Assert.AreEqual(PatternRecognitionWriteResult.Recognized, result,
                "指定三例全结 ⇒ 触发");
            Assert.AreEqual(1, PatternRecognizedCount(), "恰一条");
            Assert.AreEqual(new CaseId(tick, 12, 0), AnchorOf(FiredEvents()[0]),
                "成员 = 链成员三例 ⇒ anchor = 链内第 3 例(病人 12);" +
                "删定义域收窄 ⇒ 成员混入病人 1/2、锚点变 10 ⇒ 红");
        }

        /// <summary>
        /// 阈值守卫的**上沿格**:<c>patternThreshold = 2</c> 放行(GDD 旋钮安全范围「≥ 2」)。
        /// <para>与紧邻的 <see cref="test_patternRecognitionWriter_thresholdBelowTwo_throws"/>
        /// (下沿三格:0 / -1 / 1 拒)构成边界两格 —— 承 S11-17 教训「阈值守卫差一格即假绿」。</para>
        /// <para><b>可捕获的变异</b>:守卫由 <c>&lt; 2</c> 改 <c>&lt; 3</c> ⇒ 构造抛异常 ⇒ 红。</para>
        /// </summary>
        [Test]
        public void test_patternRecognitionWriter_thresholdTwo_allowed()
        {
            const int d = 0;
            const long tick = 10;
            var writer = new PatternRecognitionWriter(
                _bag.EventSink, _bag.Encoder, _bag.Decoder, _bag.Stream.Events,
                WorldSeed, 2, null, Version);

            AppendClosed(new PatientId(1), tick, Only(d));
            AppendClosed(new PatientId(2), tick, Only(d));   // 恰 = 2

            var result = writer.TryRecognize(tick);

            Assert.AreEqual(PatternRecognitionWriteResult.Recognized, result,
                "阈值 2 放行 —— GDD 安全范围下沿「≥ 2」(边界上沿侧)");
            Assert.AreEqual(1, PatternRecognizedCount(), "恰一条");
            Assert.AreEqual(new CaseId(tick, 2, 0), AnchorOf(FiredEvents()[0]),
                "阈值 2 时 anchor = 第 2 例(跨过阈值那一例)");
        }
    }
}
