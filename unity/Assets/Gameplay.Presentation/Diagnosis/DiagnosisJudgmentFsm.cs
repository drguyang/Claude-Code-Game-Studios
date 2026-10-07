// diagnosis-system Story 005 —— 判断状态机(S-8.3 三态 · 落笔无刹车 · 关病例冻结)。
//
// 权威来源:
//   GDD design/gdd/diagnosis-system.md §S-8.3(判断三态 空⇄疑似⇄确定 · 空 = 合法终态 ·
//     改写留痕不计分 · 置信度不进数值)· 规则七(误诊不报错)· 边界铁律⑤
//   AC-8-28(不替玩家踩刹车:零查体落笔 / 全查体不落笔皆允许)·
//   AC-8-29(改写留痕且痕不计分 —— 契约无「改写次数」字段)·
//   AC-8-46(已写病名不回退 —— 本机不持 Skill,等级进不来)·
//   AC-8-47(关病例 ⇒ 判断冻结并交 #53;次序 = 先清读数 → 再冻判断 → 交 #53)
//   Implementation Note 1:判断持久化归 39,8 只产意图;
//   Implementation Note 6:关病例次序写成断言(避免「冻结后仍可写」竞态)
//
// ⚠️ **纯规则零持久状态(铁律③)**:<see cref="JudgmentCursor"/> 是**瞬时读形**
//    (调用方每次传入),8 不持任何跨调用判断状态;持久化归 39。

namespace DaYiJingCheng.Gameplay.Presentation.Diagnosis
{
    /// <summary>判断三态(AC-8-24「病名栏恰三态」;不存在第三档置信度、不存在「不可用」态)。</summary>
    public enum JudgmentState : byte
    {
        /// <summary>空(未落笔)—— **合法终态**(不是错误态,AC-8-28)。</summary>
        Empty = 0,

        /// <summary>疑似(已落笔,未确定)。</summary>
        Suspected = 1,

        /// <summary>确定(已落笔且确定)。</summary>
        Confirmed = 2,
    }

    /// <summary>判断光标的瞬时读形(状态 + 冻结位;调用方持有,8 不存)。</summary>
    public readonly struct JudgmentCursor
    {
        /// <summary>当前判断状态。</summary>
        public readonly JudgmentState State;

        /// <summary>冻结位 —— 37 关病例后置位,此后拒绝一切落笔(AC-8-47)。</summary>
        public readonly bool Frozen;

        public JudgmentCursor(JudgmentState state, bool frozen)
        {
            State = state;
            Frozen = frozen;
        }

        /// <summary>初始光标(空、未冻结)。</summary>
        public static JudgmentCursor Fresh => new JudgmentCursor(JudgmentState.Empty, false);
    }

    /// <summary>一次落笔意图的结果(产意图;持久化归 39)。</summary>
    public readonly struct JudgmentTransition
    {
        /// <summary>落笔后的状态(被拒时 = 原状态)。</summary>
        public readonly JudgmentState State;

        /// <summary>状态是否变化。</summary>
        public readonly bool Changed;

        /// <summary>是否被拒(光标已冻结 ⇒ AC-8-47「冻结后不可写」)。</summary>
        public readonly bool Rejected;

        /// <summary>是否为**改写**(已落笔后改口)—— 留痕标记;⚠️ 本结构**无次数字段**
        /// (AC-8-29 痕不计分:8 与 #53 的输入契约里不存在「改写次数」)。</summary>
        public readonly bool Rewritten;

        public JudgmentTransition(JudgmentState state, bool changed,
                                  bool rejected, bool rewritten)
        {
            State = state;
            Changed = changed;
            Rejected = rejected;
            Rewritten = rewritten;
        }
    }

    /// <summary>关病例处置结果(三步次序的物化 —— Implementation Note 6)。</summary>
    public readonly struct CaseCloseResult
    {
        /// <summary>① 读数全状态清除后的值(恒 <see cref="SignReadState.Blank"/>)。</summary>
        public readonly SignReadState ClearedState;

        /// <summary>② 冻结后的判断光标(<c>Frozen</c> = true,状态原样保留)。</summary>
        public readonly JudgmentCursor FrozenJudgment;

        /// <summary>③ 交 #53 的结算载荷(冻结时刻的判断状态)。</summary>
        public readonly JudgmentState HandoffTo53;

        public CaseCloseResult(SignReadState clearedReading,
                               JudgmentCursor frozenJudgment, JudgmentState handoffTo53)
        {
            ClearedState = clearedReading;
            FrozenJudgment = frozenJudgment;
            HandoffTo53 = handoffTo53;
        }
    }

    /// <summary>判断状态机(S-8.3)—— **纯静态零字段**(AC-8-3 / 铁律③)。</summary>
    public static class DiagnosisJudgmentFsm
    {
        /// <summary>
        /// <b>落笔 / 改写意图</b>(任意时允许、零校验 —— AC-8-28「8 不替玩家踩刹车」):
        /// 既不校验「查够」、也不催、不提示、不给兜底清单。
        /// <para>空⇄疑似⇄确定任意转移;已落笔后改口 ⇒ <c>Rewritten</c> = true(留痕,
        /// 痕由 39 呈现;<b>不计分</b>)。光标冻结 ⇒ 拒(<c>Rejected</c> = true,状态不变)。</para>
        /// <para>⚠️ <b>AC-8-46</b>:签名**无 Skill / 等级参数** —— 掉级不回写判断(结构性排除)。</para>
        /// </summary>
        /// <param name="cursor">当前光标(瞬时读形)。</param>
        /// <param name="target">玩家落笔目标状态。</param>
        /// <returns>转移结果。</returns>
        public static JudgmentTransition OnWriteIntent(in JudgmentCursor cursor, JudgmentState target)
        {
            if (cursor.Frozen)
                return new JudgmentTransition(cursor.State, false, true, false); // 冻结后拒写

            bool changed = target != cursor.State;
            bool rewritten = changed && cursor.State != JudgmentState.Empty;
            return new JudgmentTransition(target, changed, false, rewritten);
        }

        /// <summary>
        /// <b>关病例冻结</b>(AC-8-47 判断半边):状态保留、冻结位置位、交 #53。
        /// </summary>
        /// <param name="cursor">当前光标。</param>
        /// <returns>冻结后的光标。</returns>
        public static JudgmentCursor Freeze(in JudgmentCursor cursor)
            => new JudgmentCursor(cursor.State, true);

        /// <summary>
        /// <b>关病例次序</b>(Implementation Note 6,三步显式物化):
        /// ① 先清读数态 → ② 再冻结判断态 → ③ 再交 #53。
        /// <para>次序的意义:冻结**之后**读数已空,不存在「判断冻结了、读数还在被读」的
        /// 中间态被消费的窗口;<c>HandoffTo53</c> 取冻结**时刻**的判断状态。</para>
        /// </summary>
        /// <param name="judgment">关病例时刻的判断光标。</param>
        /// <param name="reading">关病例时刻的任一读数状态(清前)。</param>
        /// <returns>三步结果。</returns>
        public static CaseCloseResult CloseCase(in JudgmentCursor judgment, SignReadState reading)
        {
            SignReadState cleared = DiagnosisReadingFsm.OnCaseClosed(reading); // ① 清读数
            JudgmentCursor frozen = Freeze(judgment);                          // ② 冻判断
            return new CaseCloseResult(cleared, frozen, frozen.State);         // ③ 交 #53
        }
    }
}
