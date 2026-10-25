// 「无未决医疗动作」瞬时源生产实装 —— `NoPendingMedicalActionQuery` 行为钉子
// (2026-10-25 用户裁定「生产接线今日不为零,现在修」)
//
// 权威来源:
//   GDD persistence-service.md:216-218 —— 载入安全上下文门(「无未决医疗动作」,瞬时量)
//   W5 裁定轨迹(TD-persist-04)—— 瞬时源 = 独立于流的内存态;流内累积钩子已证伪
//   Sim.Contracts/INarrativeContextQuery.cs —— 裁定甲具名接口(2026-10-25)
//
// 钉的安全属性:
//   ① **fail-closed**:未 Bind ⇒ 恒 false(装配未完成 = 不可用,不是「可用」);
//   ② **瞬时性**:同一实例随底层 Armed 态翻转**每次调用重答**(W5 累积形态的否证钉);
//   ③ **一个量**:Bind 只许一次(热替换 = 读档门与兜底门分叉)。

using System;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.Interaction;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Boot
{
    /// <summary>
    /// `NoPendingMedicalActionQuery`:fail-closed / 瞬时性 / 单次 Bind 三面钉子。
    /// </summary>
    public class NoPendingMedicalActionQueryTest
    {
        /// <summary>可切换 Armed 态的 <see cref="IArmedState"/> 替身(瞬时量的夹具形态)。</summary>
        private sealed class ArmedStub : IArmedState
        {
            public bool IsArmed { get; set; }
        }

        /// <summary>① fail-closed:未 Bind ⇒ 恒「不安全」—— 与裁定丙「null ⇒ 不可用」同向。</summary>
        [Test]
        public void test_noPendingAction_unbound_inNarrativeContextAlwaysFalse()
        {
            var query = new NoPendingMedicalActionQuery();
            Assert.IsFalse(query.IsBound, "构造后未 Bind");
            for (int i = 0; i < 5; i++)
                Assert.IsFalse(query.InNarrativeContext(),
                    "未 Bind 期每次调用都须 false(fail-closed,不是「可用」)");
        }

        /// <summary>Armed 态即「未决医疗动作进行中」⇒ 不在叙事上下文;Idle ⇒ 在。</summary>
        [Test]
        public void test_noPendingAction_bound_armedBlocks_idleAllows()
        {
            var stub = new ArmedStub { IsArmed = false };
            var query = new NoPendingMedicalActionQuery();
            query.Bind(stub);

            Assert.IsTrue(query.InNarrativeContext(), "Idle(无未决动作)⇒ 在叙事上下文");
            stub.IsArmed = true;
            Assert.IsFalse(query.InNarrativeContext(), "Armed(急救进行中)⇒ 不在(AC-7a-19 (c) 的源语义)");
        }

        /// <summary>
        /// ② 瞬时性:同一实例**每次调用重答** —— W5 累积形态(「曾出现过 ⇒ 恒 true」)的否证钉。
        /// </summary>
        [Test]
        public void test_noPendingAction_transient_answersFreshEachCall()
        {
            var stub = new ArmedStub { IsArmed = true };
            var query = new NoPendingMedicalActionQuery();
            query.Bind(stub);

            for (int i = 0; i < 3; i++)
                Assert.IsFalse(query.InNarrativeContext(), "Armed 期恒 false");
            stub.IsArmed = false;
            Assert.IsTrue(query.InNarrativeContext(), "解除 Armed 后**下一个调用**即翻 true —— 零缓存");
            stub.IsArmed = true;
            Assert.IsFalse(query.InNarrativeContext(), "再入 Armed 后**下一个调用**即翻 false —— 零缓存");
        }

        /// <summary>③ Bind(null) ⇒ 拒(「恒不安全」的正道是不 Bind,不是绑空源)。</summary>
        [Test]
        public void test_noPendingAction_bindNull_throws()
        {
            var query = new NoPendingMedicalActionQuery();
            Assert.Throws<ArgumentNullException>(() => query.Bind(null));
            Assert.IsFalse(query.InNarrativeContext(), "被拒的 Bind 不得改变未绑态(fail-closed 保持)");
        }

        /// <summary>
        /// ③ Bind 只许一次 —— 热替换会让同一时刻的读档门与兜底门给出不同答案(规则十四「一个量」)。
        /// </summary>
        [Test]
        public void test_noPendingAction_doubleBind_throws()
        {
            var query = new NoPendingMedicalActionQuery();
            var first = new ArmedStub { IsArmed = true };   // 首绑源取 Armed,让「未换源」可观测(false)
            query.Bind(first);
            Assert.IsFalse(query.InNarrativeContext(), "首绑源 Armed ⇒ false");

            Assert.Throws<InvalidOperationException>(() => query.Bind(new ArmedStub()),
                "二次 Bind = 编程错误:换源须换新装配,不许半途换答案");
            Assert.IsFalse(query.InNarrativeContext(),
                "被拒的二次 Bind 不得换源 —— 仍答首绑源(若换成了 Idle 替身,这里会翻 true)");
        }
    }
}
