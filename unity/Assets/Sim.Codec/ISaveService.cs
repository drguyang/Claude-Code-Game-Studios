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
    /// <para>本接口 = <b>高层三方法</b>(<see cref="Checkpoint"/> / <see cref="SaveOnExit"/> /
    /// <see cref="Load"/>),与 ADR-010 §Key Interfaces 的 <c>interface ISaveService</c> **逐字对齐**。</para>
    /// <para>⚠️ <b>AC-7a-08 / AC-7a-09 判据 ① 的反射断言</b>:<b>全部</b>公开方法参数集
    /// ∈ <c>{ {SaveSlot}, {} }</c> —— <b>不得给任何方法加第二个形参</b>;
    /// 需要的外部输入一律走构造函数注入。</para>
    /// <para>🔁 <b>2026-10-25 裁定(原文不删,保存 `SaveBytes` / `LoadBytes` 的移出记录)</b>:
    /// 本接口此前另有两个双参成员 <c>SaveBytes(SaveSlot, byte[])</c> /
    /// <c>LoadBytes(SaveSlot)</c>。它们是 <b>低层文件 I/O 原语</b>,已被移出公开面
    /// (改 <c>internal</c>,经 <c>InternalsVisibleTo("Sim.Contracts.Tests")</c> 供夹具使用)。
    /// <b>裁定依据</b>:① GDD `:147` / `:522` / `:531` 三处字面 = 「公开可触发写入方法参数集
    /// <b>恰等于</b> <c>{SaveSlot}</c>」,而两参的 <c>SaveBytes</c> 落在字面内 ⇒ 违反;
    /// ② ADR-010 `:361-363` 逐字「<c>Checkpoint(SaveSlot)</c> 是<b>唯一写入口</b>」,
    /// 低层原语本不在 Key Interfaces 内;③ <c>SaveBytes</c> 绕过 <c>Checkpoint</c> 的**全部门**
    /// (叙事上下文 / 槽位只进不退)⇒ 它是**闸门未关**,不是第二条路。
    /// <b>与 ADR-010 原文的一处硬矛盾已就地加注</b> —— 见该件 **§Key Interfaces 代码块之后**
    /// 的「🔁 2026-10-25 注」(明写「**不构成修订**」)。</para>
    /// </summary>
    public interface ISaveService
    {
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
