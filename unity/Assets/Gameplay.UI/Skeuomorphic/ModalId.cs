namespace DaYiJingCheng.Gameplay.UI.Skeuomorphic
{
    /// <summary>模态界面标识闭集(9 员 + None)。</summary>
    /// <remarks>
    /// <para>闭集成员必须经本 ADR(ADR-013 §十 Amendment A / B / C)裁定后方可追加。</para>
    /// <para>🔁 2026-10-31:**Amendment C** 增第八员 <see cref="Pause"/>(暂停壳 —— 存档位的正式入口)。
    /// 原「7 员」的口径就此作废,凡引用处按 8 员读。</para>
    /// <para>🔁 2026-11-02:**Amendment D(ADR-013 §十-E)** 增第九员 <see cref="Processing"/>
    /// (炮制屏 —— `O-18-R6` / `AC-18-10b` 的落地载体)。原「8 员」口径作废,按 **9 员**读。</para>
    /// <para>成员集与 <see cref="IModalState"/> 的 <see cref="IModalState.Modal"/> getter 返回值一一对应。</para>
    /// <para>消费方(如系统 4 交互意图)须引用本枚举,不得在自身代码内维护一份「哪些界面算模态」的清单。</para>
    /// </remarks>
    public enum ModalId : byte
    {
        /// <summary>无模态界面摊开。</summary>
        None = 0,

        /// <summary>脉案界面(系统 39) + 方笺(同页,2026-09-21 Amendment B 裁定)。</summary>
        Casebook = 1,

        /// <summary>存档位选择界面(系统 7b)。</summary>
        SaveSlots = 2,

        /// <summary>库存容器界面(系统 20)。</summary>
        InventoryContainer = 3,

        /// <summary>设置界面壳(系统 42 自身)。</summary>
        SettingsShell = 4,

        /// <summary>教学走查屏(系统 48,纸堆翻页)。</summary>
        Tutorial = 5,

        /// <summary>医馆面板(系统 24)。</summary>
        ClinicPanel = 6,

        /// <summary>教学纸近景模态(系统 48,走近 → 42 近景模态,2026-09-21 Amendment B 增员)。</summary>
        PaperCloseup48 = 7,

        /// <summary>
        /// 暂停壳(系统 42 自身;2026-10-31 **Amendment C 增员**,闭集第八员)。
        /// <para>存档位界面的**正式入口**(`save-slots-7b.md:189`):玩家从这里进 7b,
        /// 不再依赖临时按键。</para>
        /// <para>⚠️ 它必须在闭集内:暂停壳挡住世界、要冻结世界意图 —— 规则七的门只认
        /// `Modal ≠ None`,非模态叠层等于给同一种形态发明第二套判据。</para>
        /// </summary>
        Pause = 8,

        /// <summary>
        /// 炮制屏(系统 18 的数据面;2026-11-02 **Amendment D 增员**,闭集第九员)。
        /// <para>载体 = `design/gdd/processing.md` 的 `O-18-R6`(18 → 42 的只读准入接缝)+
        /// `AC-18-10b`(「不可发起的配方**不出现(非灰置)**」)。</para>
        /// <para>⚠️ 它与 <see cref="InventoryContainer"/> **不同屏**:后者语义 = 「箱里有什么」
        /// (搜箱 / 容量 / 内容),本屏语义 = 「能做什么 + 点火」(候选集 + 发起)。
        /// 拟物 UI 无指针 ⇒ 手柄焦点序是唯一导航路径,混屏即两套语义共用一条序。</para>
        /// </summary>
        Processing = 9,

        /// <summary>
        /// 结案页(系统 37 的结案形态;2026-11-10 **T20 增员**,闭集第十员)。
        /// <para>载体 = `design/ux/casebook-39.md:370` 的 **`OQ-CB-7`**(「结案页『已处置』勾选」
        /// 的交互形态,归 39 定形态 / 42 落地)+ `case-system.md:687` 规则八的
        /// `可结案(c) := 开 ∧ 已处置(c) ∧ 玩家已勾选「已处置」`。</para>
        /// <para>⚠️ **为什么是独立屏而不是脉案内的一行**:39 §5.3 明令本屏禁「复选框」,
        /// 而 GDD 的字面是「勾选」—— 形态裁定 = **纸面墨勾**(与折边 / 铅笔小勾同族的墨侧手法,
        /// 不是现代控件)。放在脉案内会让它挤进已经排满的五行焦点序;独立页保住两屏各自的焦点语言。</para>
        /// </summary>
        CaseClose = 10
    }
}
