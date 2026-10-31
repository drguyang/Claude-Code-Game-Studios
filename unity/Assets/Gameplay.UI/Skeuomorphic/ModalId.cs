namespace DaYiJingCheng.Gameplay.UI.Skeuomorphic
{
    /// <summary>模态界面标识闭集(8 员 + None)。</summary>
    /// <remarks>
    /// <para>闭集成员必须经本 ADR(ADR-013 §十 Amendment A / B / C)裁定后方可追加。</para>
    /// <para>🔁 2026-10-31:**Amendment C** 增第八员 <see cref="Pause"/>(暂停壳 —— 存档位的正式入口)。
    /// 原「7 员」的口径就此作废,凡引用处按 8 员读。</para>
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
        Pause = 8
    }
}
