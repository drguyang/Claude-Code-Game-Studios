// story-004 —— 死亡冷却 F-29-2(`DeathAllowed` 谓词 · 从流重算 · 零计数器),2026-10-24。
//
// 权威来源:
//   design/gdd/death-and-respawn.md:297 —— `DeathAllowed(actor, t) ⟺ (t − last_death_tick(actor)) ≥ DEATH_COOLDOWN`
//   design/gdd/death-and-respawn.md:299 —— `last_death_tick(actor) := max{ Tick(e) | e ∈ 世界流 ∧ Kind(e)=PlayerDied ∧ actor(e)=actor }`
//   design/gdd/death-and-respawn.md:304 —— `DEATH_COOLDOWN = 2400` tick;须 > 0
//   design/gdd/death-and-respawn.md:336 —— EC 行「冷却内再次致死」= 零事件(OQ-29-4 已裁 2026-10-07)
//   production/epics/death-respawn/story-004-death-cooldown-from-stream.md —— AC-29-15 / 谓词边界 / 空流首死 / 实现注 1·3·4·5
//   design/registry/entities.yaml —— `DEATH_COOLDOWN` 常量块(2026-10-24 补登记)
//   ADR-005(状态从流重算)· ADR-016 §一(三源不变量)
//
// ── 本文件钉的是什么 ──────────────────────────────────────────────────────────
//   ① **谓词边界**(`≥` 而非 `>`):`t − last == CD` 放行,`== CD − 1` 拒绝。
//   ② **空流 = 首死恒允许**,且**不发任何哨兵事件**。
//   ③ **零计数器**:不是靠「类里没有叫 last_death 的字段」一句话 —— 而是
//      **两次求值之间流变了,答案跟着变**(行为判据),外加字段面扫描(结构判据)。
//      只有结构判据会被「换个名字的缓存」绕过,只有行为判据会被「每次重算但结果碰巧一样」骗过
//      ⇒ 两条都要。
//   ④ **逐 actor 独立**(多玩家各自读流,无全局冷却字段)。
//   ⑤ **重放确定性**:同一事件序列 ⇒ 逐 tick 答案逐位相同(实现注 4 的迁移重放性质)。
//   ⑥ **配置域**:`cooldownTicks ≤ 0` 构造期显式抛(非静默退化)。
//   ⑦ **登记一致性**:`entities.yaml` 的 `DEATH_COOLDOWN` 块存在、六字段齐、
//      **值 = 实现 const**(改一处忘另一处 ⇒ 红)。
//
// ── 真装配,零 Fake 被测对象 ─────────────────────────────────────────────────
// 被测对象 = `DeathCooldownGate`(生产类)+ `PlayerDiedWriter`(生产类,用来**真造历史**)。
// 事件不是手搓的 `SimEvent` —— 走真 `CompositionRoot.Assemble` 的真 `EventStream` +
// 真 `PayloadEncoder`,这样「冷却门能解码生产写者编出来的载荷」本身就是被验证的一件事。
//
// ── 符号化值 ────────────────────────────────────────────────────────────────
// 夹具一律用 `SymbolicCooldown = 10`,不写 2400 —— 生产值归数值轮,改它不应让本文件转红
// (story-004 Guardrail:「构建/测试夹具用符号化值」)。生产值本身由 §⑦ 的登记一致性钉住。

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using DaYiJingCheng.EditorTools.Gates;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.DeathRespawn
{
    /// <summary>story-004 —— 死亡冷却谓词(F-29-2)。</summary>
    [TestFixture]
    public sealed class DeathCooldownTest
    {
        private const int PlayerId = 7;
        private const int OtherPlayerId = 8;

        /// <summary>符号化冷却长度 —— 与生产 2400 解耦(改生产值不得让本文件转红)。</summary>
        private const long SymbolicCooldown = 10;

        private CompositionRootServices _bag;
        private PlayerDiedWriter _writer;

        [SetUp]
        public void Setup()
        {
            _bag = CompositionRoot.Assemble();
            _writer = new PlayerDiedWriter(_bag.EventSink, _bag.Encoder);
        }

        // ── 夹具 ────────────────────────────────────────────────────────────

        /// <summary>造一条注册表条目(纯数据夹具,非 Fake 被测对象)。</summary>
        private static DiseaseRegistryEntry LethalEntry()
            => new DiseaseRegistryEntry
            {
                DiseaseId = 1,
                DiseaseKey = "DIS_PLAGUE",
                Kind = DiseaseKind.Disease,
                Lethality = 3,
            };

        /// <summary>在 <paramref name="tick"/> 真写一条 <c>PlayerDied</c>(造历史用)。</summary>
        private void Die(int actor, long tick)
        {
            PlayerDiedWriteResult r = _writer.TryWrite(
                tick, actor, ActorClass.Player, LethalEntry(),
                new WorldPos(0, 0, 0), deathAllowed: true);

            // 夹具前提:这一笔必须真进流 —— 否则后面「冷却拒绝」可能只是因为流是空的。
            Assert.AreEqual(PlayerDiedWriteResult.Written, r,
                "夹具构造失败:造历史的这一笔没写进世界流");
        }

        private DeathCooldownGate Gate(long cooldown = SymbolicCooldown)
            => new DeathCooldownGate(() => _bag.Stream.Events, _bag.Decoder, cooldown);

        private int CountPlayerDied()
            => _bag.Stream.Events.Count(e => e.Kind == EventKind.PlayerDied);

        // ══════════════════════════════════════════════════════════════════
        // ① 谓词边界 —— `≥` 而非 `>`
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_deathAllowed_exactlyAtCooldown_isAllowed()
        {
            Die(PlayerId, 100);
            DeathCooldownGate gate = Gate();          // CD = 10

            Assert.IsTrue(gate.DeathAllowed(PlayerId, 110),
                "`t − last == DEATH_COOLDOWN` 须**放行**(GDD :297 的 `⟺` 逐字是 ≥)");
        }

        [Test]
        public void test_deathAllowed_oneTickBeforeCooldown_isDenied()
        {
            Die(PlayerId, 100);
            DeathCooldownGate gate = Gate();

            Assert.IsFalse(gate.DeathAllowed(PlayerId, 109),
                "`t − last == DEATH_COOLDOWN − 1` 须**拒绝** —— 边界写成 `>` 会让 109 也放行");
        }

        [Test]
        public void test_deathAllowed_farBeyondCooldown_isAllowed()
        {
            Die(PlayerId, 100);
            DeathCooldownGate gate = Gate();

            Assert.IsTrue(gate.DeathAllowed(PlayerId, 100 + SymbolicCooldown * 3));
        }

        // ══════════════════════════════════════════════════════════════════
        // ② 空流 = 首死恒允许,且不发哨兵事件
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_deathAllowed_emptyStream_firstDeathAlwaysAllowed()
        {
            DeathCooldownGate gate = Gate();

            Assert.IsTrue(gate.DeathAllowed(PlayerId, 0),
                "从未死过 ⇒ `last_death_tick = −∞` 语义 ⇒ 首死恒允许");
            Assert.IsTrue(gate.DeathAllowed(PlayerId, 999999),
                "同上 —— 不是「t 要够大」,而是根本没有下界");

            Assert.AreEqual(0, _bag.Stream.Events.Count,
                "「−∞」是**语义**不是事件:空流求值**不得**往流里写任何哨兵(AC 逐字「不发哨兵事件」)");
        }

        [Test]
        public void test_tryLastDeathTick_emptyStream_returnsFalseNotSentinel()
        {
            DeathCooldownGate gate = Gate();

            bool found = gate.TryLastDeathTick(PlayerId, out long last);

            Assert.IsFalse(found, "「不存在」由返回的布尔承载");
            Assert.AreEqual(long.MinValue, last,
                "出参给 long.MinValue 只是占位 —— 调用方**不应**依赖它,「不存在」看布尔");
        }

        // ══════════════════════════════════════════════════════════════════
        // ③ 零计数器(结构判据 + 行为判据,两条都要)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 结构判据:29 的冷却侧**不持有**「某 actor 死过没死」的任何存储。
        /// <para>⚠️ 单靠这条不够 —— 把缓存改名成 `_x` 就绕过了。故配下面那条行为判据。</para>
        /// </summary>
        [Test]
        public void test_zeroCounter_noPerActorStorageField()
        {
            Type[] side = { typeof(DeathCooldownGate), typeof(DeathCandidateConsumer) };

            foreach (Type t in side)
            {
                FieldInfo[] fields = t.GetFields(
                    BindingFlags.Instance | BindingFlags.NonPublic |
                    BindingFlags.Public | BindingFlags.DeclaredOnly);

                foreach (FieldInfo f in fields)
                {
                    string lower = f.Name.ToLowerInvariant();
                    Assert.IsFalse(
                        lower.Contains("lastdeath") || lower.Contains("last_death"),
                        $"{t.Name}.{f.Name} —— 不得存 `last_death_tick`(AC-29-15:唯一来源 = 世界流重算)");

                    Assert.IsFalse(typeof(IEnumerable).IsAssignableFrom(f.FieldType),
                        $"{t.Name}.{f.Name} —— 不得持有容器字段:按 actor 存冷却状态 = 与真源失步的第二载体," +
                        "且读档 / 主机迁移后不可重构(ADR-005 单一真源)");
                }
            }
        }

        /// <summary>
        /// 行为判据:**流变了,答案跟着变**。
        /// <para>这条才真正证明「每次现读流」。若实现缓存了第一次的读数,第二段会红。</para>
        /// </summary>
        [Test]
        public void test_zeroCounter_rereadsStreamOnEveryEvaluation()
        {
            DeathCooldownGate gate = Gate();

            Die(PlayerId, 100);
            Assert.IsFalse(gate.DeathAllowed(PlayerId, 105), "前提:第一笔之后冷却未开");

            // 同一个 gate 实例,流再长一笔 —— 答案必须立刻反映新读数。
            Die(PlayerId, 200);

            Assert.IsFalse(gate.DeathAllowed(PlayerId, 205),
                "流新增了 t=200 的 PlayerDied ⇒ 205 须被拒;若仍放行 = 实现缓存了旧读数");
            Assert.IsTrue(gate.DeathAllowed(PlayerId, 210),
                "以**最新**一笔(200)为基准,210 恰界放行 —— 不是以 100 为基准");
        }

        /// <summary>
        /// GDD `:299` 逐字是 `max{ Tick(e) }`,不是「末条」。
        /// <para>⚠️ **为什么这里不能用真流造乱序**:`EventStream.Append` 有 BCD-码-3
        /// (tick 非降断言),真流**不可能**乱序 ⇒ 用真流只能测「末条」,测不出 max 与 last 的差别。
        /// ⇒ 本用例把**流视图**换成手工序列(输入夹具,不是 Fake 被测对象:被测对象是
        /// <see cref="DeathCooldownGate"/>;载荷仍由**真** <c>_bag.Encoder</c> 编码,
        /// 故「门能解码生产编码器产出的字节」这一性质仍被覆盖)。</para>
        /// <para>真实触发场景:主机迁移 / 读档后从快照 + 流后缀重建,写入序不保证等于 tick 序。</para>
        /// </summary>
        [Test]
        public void test_tryLastDeathTick_takesMaxNotLast()
        {
            SimEvent At(long tick)
            {
                var payloadRef = _bag.Encoder.Encode(
                    EventKind.PlayerDied, new PlayerDiedPayload(PlayerId, new WorldPos(0, 0, 0), tick));
                return new SimEvent(tick, PatientId.None, -1, EventKind.PlayerDied, payloadRef);
            }

            // 刻意乱序成 `100 / 300 / 200`:max 在**中间**,首条与末条**都不是**它。
            // ⚠️ 2026-10-24 证伪轮 M2 订正(原文不删):原夹具写 `{300, 100}`,而
            // 「取首条」在那里**也**返回 300 ⇒ 该变异抓不到本测(**我自己的夹具曾是假绿**)。
            // 现形态下三种实现的读数两两可分:取首条 = 100(错)/ 取末条 = 200(错)/ 取 max = 300(对)。
            var shuffled = new List<SimEvent> { At(100), At(300), At(200) };
            var gate = new DeathCooldownGate(() => shuffled, _bag.Decoder, SymbolicCooldown);

            Assert.IsTrue(gate.TryLastDeathTick(PlayerId, out long last));
            Assert.AreEqual(300L, last,
                "GDD :299 是 `max{ Tick(e) }` —— 取末条会在乱序 / 补写时给出更早的读数," +
                "进而让冷却**提前**放行(少冷却 = 掉落经济漏洞)");
        }

        // ══════════════════════════════════════════════════════════════════
        // ④ 逐 actor 独立(多玩家并行冷却)
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_deathAllowed_actorsAreIndependent()
        {
            Die(PlayerId, 100);
            Die(OtherPlayerId, 105);
            DeathCooldownGate gate = Gate();

            Assert.IsTrue(gate.DeathAllowed(PlayerId, 110),
                "玩家 7 自己的 100 + 10 = 110 ⇒ 放行");
            Assert.IsFalse(gate.DeathAllowed(OtherPlayerId, 110),
                "玩家 8 的基准是 105 ⇒ 110 仍在冷却内 —— 不得被 7 的历史污染");
            Assert.IsTrue(gate.DeathAllowed(OtherPlayerId, 115));
        }

        // ══════════════════════════════════════════════════════════════════
        // ⑤ 重放确定性(实现注 4:迁移后取值逐位不变)
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_replay_sameEventSequenceGivesIdenticalAnswers()
        {
            static List<bool> Sweep(DeathCooldownGate g)
            {
                var answers = new List<bool>();
                for (long t = 90; t <= 340; t += 7)
                    answers.Add(g.DeathAllowed(PlayerId, t));
                return answers;
            }

            // 序列 A:本夹具的装配
            Die(PlayerId, 100);
            Die(PlayerId, 260);
            List<bool> a = Sweep(Gate());

            // 序列 B:**另一套装配**(全新 stream / pool / encoder),逐笔重放同一序列
            var bagB = CompositionRoot.Assemble();
            var writerB = new PlayerDiedWriter(bagB.EventSink, bagB.Encoder);
            foreach (long tick in new long[] { 100, 260 })
            {
                writerB.TryWrite(tick, PlayerId, ActorClass.Player, LethalEntry(),
                    new WorldPos(0, 0, 0), deathAllowed: true);
            }
            var gateB = new DeathCooldownGate(() => bagB.Stream.Events, bagB.Decoder, SymbolicCooldown);

            CollectionAssert.AreEqual(a, Sweep(gateB),
                "同一事件序列 ⇒ 逐 tick 答案须**逐位相同**(纯函数性质;迁移 / 读档后重建的唯一依据)");
        }

        // ══════════════════════════════════════════════════════════════════
        // ⑥ 配置域 —— `≤ 0` 是显式配置错误,不是静默退化
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_ctor_zeroCooldownThrows()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new DeathCooldownGate(() => _bag.Stream.Events, _bag.Decoder, 0),
                "CD = 0 ⇒ 谓词恒真 ⇒ 冷却静默退化为不存在,而门的外观完好(GDD :304 须 > 0)");
        }

        [Test]
        public void test_ctor_negativeCooldownThrows()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new DeathCooldownGate(() => _bag.Stream.Events, _bag.Decoder, -1));
        }

        [Test]
        public void test_ctor_nullDependenciesThrow()
        {
            Assert.Throws<ArgumentNullException>(
                () => new DeathCooldownGate(null, _bag.Decoder));
            Assert.Throws<ArgumentNullException>(
                () => new DeathCooldownGate(() => _bag.Stream.Events, null));
        }

        [Test]
        public void test_deathAllowed_negativeActorThrows()
        {
            DeathCooldownGate gate = Gate();

            Assert.Throws<ArgumentOutOfRangeException>(() => gate.DeathAllowed(-1, 0),
                "PatientId.None(-1) 是「世界级事件」哨兵,不是可冷却对象 —— " +
                "当成某个 actor 会静默给出错误答案");
            Assert.Throws<ArgumentOutOfRangeException>(() => gate.TryLastDeathTick(-1, out _));
        }

        // ══════════════════════════════════════════════════════════════════
        // ⑦ 装配面 —— 服务袋里的门须读**装配的这条流**
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_assemble_exposesGateWiredToAssembledStream()
        {
            Assert.IsNotNull(_bag.DeathCooldown, "装配层须构造并暴露 DeathCooldownGate");

            // 生产装配的 CD = 2400(默认值),故这里用 2400 的边界而不是符号化 10 ——
            // 本用例测的是**接线**(门读的是不是这条流),不是谓词边界(§① 已测)。
            const long cd = DeathCooldownGate.DEATH_COOLDOWN;
            Die(PlayerId, 100);

            Assert.IsFalse(_bag.DeathCooldown.DeathAllowed(PlayerId, 100 + cd - 1),
                "服务袋里的门若读的是**另一条**流(或空流),这里会放行 ⇒ 冷却在生产上等于不存在");
            Assert.IsTrue(_bag.DeathCooldown.DeathAllowed(PlayerId, 100 + cd));
        }

        [Test]
        public void test_assemble_gateUsesProductionCooldownByDefault()
        {
            Assert.AreEqual(DeathCooldownGate.DEATH_COOLDOWN, _bag.DeathCooldown.CooldownTicks,
                "生产装配取默认值(注入缝归数值轮 —— story-004 实现注 5)");
        }

        // ══════════════════════════════════════════════════════════════════
        // ⑧ 登记一致性 —— entities.yaml 的 DEATH_COOLDOWN 块 ↔ 实现 const
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// `entities.yaml` 的 `DEATH_COOLDOWN` 块须存在且六字段齐
        /// (status / source / referenced_by / value / unit / constraint)。
        /// <para>⚠️ 判据来源:该常量块 2026-10-24 才补登记 —— 此前它只以**引用**形态
        /// 活在 GDD 正文与 `PlayerDied` 的有界性措辞里,「registry 是唯一真源」在该量上不成立。
        /// 与 `DiseaseConstantRegistryGate` 的 D1/D2 同一判据,但**不**把本常量塞进那个门
        /// (那个门是 9 的常量门,扩进去 = 跨域扩门)。</para>
        /// </summary>
        [Test]
        public void test_registry_deathCooldownBlockExistsWithAllFields()
        {
            string path = DiseaseConstantRegistryGate.EntitiesYamlPath();
            Assert.IsTrue(System.IO.File.Exists(path), $"entities.yaml 不存在:{path}");

            List<string> errs = DiseaseConstantRegistryGate.CheckConstant(
                System.IO.File.ReadAllText(path), "DEATH_COOLDOWN");

            Assert.That(errs, Is.Empty,
                "`DEATH_COOLDOWN` 的登记块有违例:\n  " + string.Join("\n  ", errs));
        }

        /// <summary>
        /// 登记值 = 实现 const。**改一处忘另一处 ⇒ 红**。
        /// <para>这是「登记说 2400、代码跑别的」这类静默分叉的唯一机械闸。</para>
        /// </summary>
        [Test]
        public void test_registry_valueMatchesImplementationConst()
        {
            string path = DiseaseConstantRegistryGate.EntitiesYamlPath();
            string block = DiseaseConstantRegistryGate.ExtractBlock(
                System.IO.File.ReadAllText(path), "DEATH_COOLDOWN");
            Assert.IsNotNull(block, "`DEATH_COOLDOWN` 块缺失 —— 上一条应已报出,本条的解析面为空");

            Match m = Regex.Match(block, @"value:\s*""\s*(\d+)\s*tick");
            Assert.IsTrue(m.Success,
                "`DEATH_COOLDOWN` 的 `value:` 须以 `<整数> tick` 开头,否则本判据无法解析\n块内容:\n" + block);

            long registered = long.Parse(m.Groups[1].Value);
            Assert.AreEqual(DeathCooldownGate.DEATH_COOLDOWN, registered,
                "登记值与实现 const 不一致 —— 两处须同改(见 entities.yaml 该块的 value 注)");
        }
    }
}
