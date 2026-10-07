// diagnosis-system Story 006 —— 动作词表路由(S-8.4 路线甲 · 模态分流)。
//
// 权威来源:
//   GDD design/gdd/diagnosis-system.md §S-8.4(:783-826)
//     —— 路由表:① 就诊/立案 = 世界空间裸 Interact → 37(CaseOpened);
//        ② 查体 = 模态内行级动作 → 8;③ 施治落笔 = 方笺 → 11;④ 急救 = 10 Armed 直读通道
//     —— 三条结构理由:医学顺序即输入顺序 / 零新裁决输入 / 与 ADR-009 §七 拾取同构
//     —— B-1:4 出境载荷逐位不变 = (病人, InteractIntent) 全整数;
//        B-2:行级动作住模态不经 4 世界路由;B-3:路由表非数值件;B-4:P1a 扩表只加行
//   AC-8-51[A]:裸 Interact 打四种粗状态语义恒「就诊」不漂移;Armed 期压制(10 联动);
//                未立案 ⇒ 37 CaseOpened
//   AC-8-52[A]:① 4 出境形状不变(词表在 8 侧,4 无 TreatmentIntent 枚举膨胀);
//                ② 裁决输入集不扩大 —— 只读已落盘 IModalState.Modal
//   ADR-011 Amendment B(联机判定归主机;本地判定 = 预表现)
//   ADR-009 §七(意图不带语义,语义由当下上下文定 —— 本词表即「当下上下文」的表)
//
// ⚠️ **词表住 8,4 只见 InteractIntent 整数**(B-1):本类型不认识 4 的路由机器,
//    也不认识 UI 类型 —— 裁决输入 = pressed + 粗状态枚举,**零新裁决输入**(B-2/AC-8-52②)。
// ⚠️ **纯静态零字段**(铁律③同形):状态由调用方(4/37 接线)现取,本表不存。
// ⚠️ **Armed 压制的真身在 4**(`ModalGate.Accept` = ¬Armed ∧ ¬ModalOpen,story 005 已实现)
//    —— 本表的 `ArmedSuppressed` 粗态是**接线侧会喂给词表的第四态**,两处语义必须一致:
//    词表判压制 ⇒ 4 的门也判压制 ⇒ 意图根本不进世界路由(双重一致,单轮评审核对点)。

namespace DaYiJingCheng.Gameplay.Presentation.Diagnosis
{
    /// <summary>
    /// 病人粗状态(S-8.4 词表的输入面,AC-8-51 点名的四态 + 未立案细分)。
    /// <para>「粗」= 世界空间路由视角只关心的最小区分:**病例是否已立案** 与
    /// **10 是否在 Armed 压制期**。查体读数细节归 39,不进本枚举。</para>
    /// </summary>
    public enum PatientCoarseState : byte
    {
        /// <summary>未立案(裸 Interact ⇒ 就诊 + 37 CaseOpened)。</summary>
        NotOpened = 0,

        /// <summary>已立案未查(裸 Interact ⇒ 就诊,不重复立案)。</summary>
        OpenedUnread = 1,

        /// <summary>已落笔(裸 Interact ⇒ 就诊语义恒定 —— 脉案不改路由语义)。</summary>
        Written = 2,

        /// <summary>10 Armed 压制期(该意图被压制,计数 0)。</summary>
        ArmedSuppressed = 3,
    }

    /// <summary>裸 Interact 的路由去向(词表输出面)。</summary>
    public enum VisitRoute : byte
    {
        /// <summary>未按下(或非病人目标)⇒ 忽略。</summary>
        Ignore = 0,

        /// <summary>Armed 压制 ⇒ 意图不进世界路由(计数 0,AC-8-51)。</summary>
        Suppressed = 1,

        /// <summary>就诊(已立案)⇒ 就诊语义,不新开病例。</summary>
        Visit = 2,

        /// <summary>就诊(未立案)⇒ 就诊 + 37 CaseOpened(以该 tick 为立案时刻)。</summary>
        OpenCase = 3,
    }

    /// <summary>
    /// 动作词表(路线甲 —— 模态分流):**世界空间裸 Interact 的唯一语义 = 就诊**。
    /// <para>查体(模态行级)与急救(Armed 直读)不经本表 —— 前者住 42/8 的行级 Submit,
    /// 后者是 10 的独立直读通道(B-2/ADR-011)。本表只回答一个问题:
    /// 「这一帧的裸 Interact,按病人粗状态该去哪」。</para>
    /// <para><b>B-1</b>:4 的出境载荷逐位不变(本表不认识 4,4 也不认识本表 ——
    /// 接线点在 37/4 的组合处,归接线 story,登记见卡 Completion Notes)。
    /// <b>B-4 忠实转述</b>(GDD B-4 原文「望闻问切进表时只加行,不得把岔口改回空间分流」):
    /// P1a 望闻问切(14 辨证)是**模态内行级动作**,进 S-8.4 路由表 = 新**动作行**,
    /// **不经本 <see cref="Resolve"/>(世界空间裸 Interact 行)、也不以扩
    /// <see cref="PatientCoarseState"/> 粗态的方式接入(粗态扩枚举 = 空间分流回退,
    /// 恰是 B-4 明禁方向);本类只承接路由表第 ① 行。</para>
    /// </summary>
    public static class DiagnosisActionLexicon
    {
        /// <summary>
        /// 路由判定(S-8.4 ①「就诊/立案」行)。
        /// <para>· 未按下 ⇒ <see cref="VisitRoute.Ignore"/>;
        /// · Armed 压制 ⇒ <see cref="VisitRoute.Suppressed"/>(AC-8-51:计数 0);
        /// · 未立案 ⇒ <see cref="VisitRoute.OpenCase"/>(37 写 CaseOpened —— 8 只出路由,
        ///   不直接写流,写权归 37 接线);
        /// · 已立案(未查/已落笔)⇒ <see cref="VisitRoute.Visit"/>(语义同为就诊)。</para>
        /// <para>⚠️ **Armed 与未立案同帧的次序**:压制优先 —— 10 的动作窗口不被立案抢跑
        /// (与 4 的门序一致:¬Armed 先于一切)。</para>
        /// </summary>
        /// <param name="interactPressed">本帧裸 Interact 是否按下(全整数意图的 Pressed 位)。</param>
        /// <param name="state">病人粗状态(调用方现取,本表不存 —— B-2 零新裁决输入)。</param>
        /// <returns>路由去向。</returns>
        public static VisitRoute Resolve(bool interactPressed, PatientCoarseState state)
        {
            if (!interactPressed)
                return VisitRoute.Ignore;

            // ① Armed 压制优先(与 4 的 ModalGate 门序一致)
            if (state == PatientCoarseState.ArmedSuppressed)
                return VisitRoute.Suppressed;

            // ② 未立案 ⇒ 就诊 + 立案;已两态 ⇒ 就诊(语义不漂移)
            return state == PatientCoarseState.NotOpened
                ? VisitRoute.OpenCase
                : VisitRoute.Visit;
        }

        /// <summary>
        /// 是否「就诊」语义(AC-8-51 的恒定断言面):<see cref="VisitRoute.Visit"/> 与
        /// <see cref="VisitRoute.OpenCase"/> 同属就诊 —— **两种粗状态输出不漂移**。
        /// </summary>
        /// <param name="route">路由去向。</param>
        /// <returns><c>true</c> = 就诊语义。</returns>
        public static bool IsVisitSemantics(VisitRoute route)
            => route == VisitRoute.Visit || route == VisitRoute.OpenCase;
    }
}
