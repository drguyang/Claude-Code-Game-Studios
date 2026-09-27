namespace DaYiJingCheng.Gameplay.UI.Skeuomorphic
{
    /// <summary>
    /// 模态开集只读契约。
    /// <para>供系统 4(交互意图) / 系统 10(急救动作)等消费方读取「是否有模态界面摊开」。</para>
    /// <para>只读,零写侧外暴。模态的开合由 42 渲染层自身维护。</para>
    /// </summary>
    /// <remarks>
    /// 42 不对外暴露 setter;模态状态的真源是「哪个界面当前可见」,
    /// 本身是 42 渲染职责内的量,不落游戏模拟态。
    /// </remarks>
    public interface IModalState
    {
        /// <summary>当前打开的模态界面标识;<see cref="Global.ModalId.None"/> 表示无模态。</summary>
        ModalId Modal { get; }
    }
}
