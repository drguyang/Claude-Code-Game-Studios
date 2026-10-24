// ADR-010 §六 —— ISaveService 接口定义。
//
// 权威来源:
//   ADR-010 §六 —— Checkpoint(SaveSlot) 唯一写入口
//   ADR-010 §四 —— 校验和 + 自动回退
//   ADR-010 §三 —— 义务 1:WorldSeed 生成 + 存档头持久化

using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.Codec
{
    /// <summary>槽位身份。只进不退,slot_seq 为唯一排序键。</summary>
    public struct SaveSlot
    {
        public uint SlotSeq;
        public SaveSlot(uint slotSeq) { SlotSeq = slotSeq; }
    }

    /// <summary>
    /// 存档服务接口。
    /// <para><b>低层原语</b>(<see cref="SaveBytes"/> / <see cref="LoadBytes"/>)只做文件 I/O;
    /// <b>高层</b>(<see cref="Checkpoint"/> / <see cref="SaveOnExit"/> / <see cref="Load"/>)
    /// 叠加门与迁移链。</para>
    /// <para>⚠️ <b>ADR-010 `:331` 的反射断言</b>:公开方法参数集恰等于 <c>{SaveSlot}</c> ——
    /// 除 <see cref="SaveBytes"/> 外,其余方法的参数集须恰为单个 <see cref="SaveSlot"/>
    /// (或空)。<b>不得给 <see cref="Checkpoint"/> / <see cref="Load"/> 加形参</b>;
    /// 需要的外部输入一律走构造函数注入。</para>
    /// </summary>
    public interface ISaveService
    {
        /// <summary>原子写字节到存档文件。</summary>
        void SaveBytes(SaveSlot slot, byte[] data);

        /// <summary>读档字节。校验和验证 + 自动回退 bak。</summary>
        byte[] LoadBytes(SaveSlot slot);

        /// <summary>
        /// 存档(唯一写入口)。门 → 组版 → 原子写。
        /// <para>门 = 叙事上下文 + 槽位只进不退(`AC-7a-19 (b)(c)`);
        /// 组版 = 构造注入的 `compose` 委托(折叠与字节布局都在委托内,
        /// 见 S19-1 三家专家合成裁定);写 = <see cref="SaveBytes"/>。</para>
        /// <para>⚠️ 未注入驱动源 ⇒ <b>fail-closed 抛具名异常</b>,禁静默跳过。</para>
        /// </summary>
        void Checkpoint(SaveSlot slot);

        /// <summary>
        /// 退出保存。同步 / 限时 join(ADR-010 §六:退出钩子,非玩家可选槽位)。
        /// <para>与 <see cref="Checkpoint"/> **同组版、不同门**:① 不评估叙事上下文钩子的
        /// <b>值</b>(只要求已注入);② 不推进槽位高水位(退出保存不占玩家槽)。</para>
        /// <para>⚠️ 原文写「同门同组版」为假 —— 见 `SaveService.SaveOnExit` 的 remarks。</para>
        /// </summary>
        void SaveOnExit();

        /// <summary>
        /// 读档。`Gating` → `Verifying` → 头部解析 → 迁移链三分支 → `Replaying` 前半段。
        /// <para><b>真重放(列表 → sim 状态)不在本方法内</b> —— 归 `TD-persist-03`。
        /// 本方法把事件列表经构造注入的 `replaySink` 交出。</para>
        /// <para>⚠️ 未注入 `replaySink` ⇒ 进 `Replaying` 前<b>抛具名异常</b>,禁静默跳过。</para>
        /// </summary>
        void Load(SaveSlot slot);
    }
}
