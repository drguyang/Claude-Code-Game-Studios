// 权威来源:ADR-005 §Key Interfaces(:237-243,六抽象点中的四支)· ADR-007 §一(IEventAuthority)
//          · ADR-008 §一(IEventSink 路由口径)· ADR-009(:466-470,IIdAuthority 扩展)
//
// 六抽象点 = ADR-005 五个 + ADR-007 §一 的 IEventAuthority。本文件收四支;
// 另两支拆出理由:
//   · IVitalsQuery —— 已于 **b1b(2026-09-22 批)** 随 VitalsDto 落地于 VitalsDto.cs;
//   · ITeleportCommandSink —— ADR-025 §③ QQ-01 ①′:整数半在此包,Vector3 半在
//     Gameplay.Presentation;其形状与 RollRequest 无耦合,放本文件(见下)。
//
// ⚠️ IIdAuthority 的两支方法**无法逐字转录**:ADR-009:468-469 同时声明
//   `PatientId Next()` 与 `ItemInstanceId Next()`,而 C# 禁止仅返回类型不同的重载(CS0111),
//   原文照抄必编译失败。落地改名 `NextPatientId()` / `NextItemInstanceId()`,语义与
//   「计数器 + 高水位可重构」机制(ADR-006 Amendment B)完全不变。
//   该偏离属「ADR 原文不自洽、实现被迫修正」类,待下轮回写轮在 ADR-009 就地加注。

namespace DaYiJingCheng.Sim.Contracts
{
    /// <summary>全案 tick 唯一来源(ADR-005)。Step 由它驱动,不由渲染帧驱动。</summary>
    public interface ITickProvider
    {
        /// <summary>当前逻辑 tick。20 Hz ⇒ 每 tick 50 ms(technical-preferences 裁定值)。</summary>
        long CurrentTick { get; }
    }

    /// <summary>
    /// 事件写入通道(ADR-005:238;ADR-008 §一 扩展路由口径)。
    /// 主机在 Append 时执行 <c>Kind → StreamId</c> 纯函数路由;白名单 = kindgen 生成的
    /// StreamRouting(ADR-024),列表外 Kind 构建期即不存在,运行期拒收为双保险。
    /// P0 实现 = 本地 list(ADR-005 注)。
    /// </summary>
    public interface IEventSink
    {
        void Append(in SimEvent e);
    }

    /// <summary>
    /// 发号权威(ADR-005:239 定义 · ADR-009 扩展物品支 · ADR-006 Amendment B 机制 ·
    /// 第二十六批扩大适用面至 player_id —— 复用本机制,不新开第二计数器)。
    /// 防运行时 instance id;高水位可由事件流重构(next = max(已发号) + 1)。
    /// </summary>
    public interface IIdAuthority
    {
        /// <summary>病人 / 受伤实体 id(与敌人共用同一空间 —— ADR-016 §二)。</summary>
        PatientId NextPatientId();

        /// <summary>掉落 / 库存 / 建造件实例 id(TR-itemdb-019 / D-21-26)。</summary>
        ItemInstanceId NextItemInstanceId();
    }

    /// <summary>
    /// 掷骰权(ADR-007 §一,第六抽象点;与 IEventSink 刻意不合并 —— 写入通道语义 ≠ 掷骰权语义)。
    /// P0 = 本地占位,IsAuthority 恒 true。Roll 必须是纯函数:给定
    /// (WorldSeed, 配置, RollRequest, 候选集) → 同一 EventRollResult,不读任何运行时对象。
    /// </summary>
    public interface IEventAuthority
    {
        /// <summary>P0 恒 true;P1b 后区分主机 / 客户端。</summary>
        bool IsAuthority { get; }

        /// <summary>仅权威侧可调用。</summary>
        EventRollResult Roll(in RollRequest r);
    }

    /// <summary>
    /// 传送契约的整数半(ADR-025 §③ QQ-01 ①′)。
    /// 连续半(Vector3 落点微调)住 Gameplay.Presentation;本接口只发「把 actor 传到某格」的
    /// 整数意图 —— 全案唯一跨门调用点(29 结算 → 触发 1 传送)在此接缝越过门 A。
    /// </summary>
    public interface ITeleportCommandSink
    {
        void RequestTeleport(int actorId, WorldPos cell);
    }
}
