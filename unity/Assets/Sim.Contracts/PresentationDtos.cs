// 权威来源:ADR-018 §二/Key Interfaces(AudioCueDto + IAudioCueSink,44 名义并入 ADR-025 §①)
//          · ADR-001 §一之二(WorldPosLatest + IPositionalChannel,第二 QoS latest-value 表)
//          · **b1b 支 3 · X-1 裁定(2026-09-22 批,b1a 已登记)**:原文 `int3` =
//            Unity.Mathematics 引擎类型,与 Sim.Contracts「引用集 = BCL + noEngineReferences:true」
//            冲突 ⇒ 消解 = 自建 Int3(Int3.cs),字段名 / 语义不变,本文件兑现换入。
//
// ⚠️ ADR-018 Key Interfaces 块(:435-441)是 2026-09-18 修订**前**的旧抄本
//   (带 `byte Tier` / `WorldPos Cell` / `PatientId Source`);现行形状 = §二(:197-205,
//   「原语 int3 / int」)。本文件按 §二 + 修订①② 落地。architecture.yaml:392 同旧抄本 ⇒ 回写轮义务。
// ⚠️ CueId 不造包装类型(支 3-a 口径同 1-b:int + 注释);Source 用 int 不用 PatientId =
//   ADR-018 修订② 字面(契约零 sim 程序集引用的原始理由虽因 PatientId 住本包而半失效,
//   仍照原文落 —— 该 DTO 的消费者 44 与病人身份空间无判定耦合,弱化是刻意)。
// ⚠️ AudioCueHandle:ADR-018 :447 用此类型但**全库零定义**(幽灵类型,同 WorldPosLatest 先例)。
//   落地 = 全整数 handle struct(契约层禁 unsafe / 禁引用)。若验收要别的形状,属另裁。

using System;

namespace DaYiJingCheng.Sim.Contracts
{
    /// <summary>循环 cue 的句柄(幽灵类型补定义:一次性 id,由 BeginLoop 返回,EndLoop 原样回传)。</summary>
    public readonly struct AudioCueHandle
    {
        public readonly int Id;
        public AudioCueHandle(int id) { Id = id; }
    }

    /// <summary>听诊音精度档的来源(ADR-018 §五 裁定 D-A:一律本机玩家档)。</summary>
    public enum TierSource : int
    {
        Local,
    }

    /// <summary>音频呈现 DTO(§二)。整数语义,**无 disease_id**(PresentationDtoGuard 递归扫描覆盖);
    /// 44 唯一数据入口,永不回写 sim。</summary>
    public readonly struct AudioCueDto
    {
        /// <summary>语义 cue id(音频事件表 ordinal;解析在上游完成,44 只收结果 cue)。</summary>
        public readonly int Cue;
        /// <summary>0–255 归一强度(呈现参数,在定点域之外,取整数为与上游一致)。</summary>
        public readonly byte Intensity;
        /// <summary>空间化锚点(整数格,ADR-015)。X-1 换入:原文 int3。</summary>
        public readonly Int3 Cell;
        /// <summary>声源实体 id;None = -1(环境音)。</summary>
        public readonly int Source;
        /// <summary>循环(呼吸层)/ 一次性(咳嗽)。</summary>
        public readonly bool Looped;

        public AudioCueDto(int cue, byte intensity, Int3 cell, int source, bool looped)
        {
            Cue = cue; Intensity = intensity; Cell = cell; Source = source; Looped = looped;
        }
    }

    /// <summary>44 对上游的唯一界面(只入不出,不持有状态 —— ADR-018 §一)。</summary>
    public interface IAudioCueSink
    {
        void Emit(in AudioCueDto cue);                     // 一次性 cue(咳嗽 / 翻页 / 狗吠)
        AudioCueHandle BeginLoop(in AudioCueDto cue);      // 循环 cue(呼吸层 / 环境)
        void EndLoop(AudioCueHandle handle);               // 由唯一持有者(上游)负责收尾
        void SetTier(TierSource source);                   // 设备级;一律 TierSource.Local(§五)
    }

    /// <summary>第二 QoS:表现态位置 latest-value 表的一行(ADR-001 §一之二)。
    /// 不进事件流、不存档;X-1 换入:原文 int3。</summary>
    public readonly struct WorldPosLatest
    {
        public readonly int ActorId;     // 与 IIdAuthority 同空间(病人 / 敌人 / 玩家)
        public readonly Int3 Cell;       // 整数格(ADR-015 §三)
        public readonly uint ServerTick; // 主机 tick 序号(客户端陈旧丢弃)
        public readonly byte Flags;      // 位域,见 PosFlags

        public WorldPosLatest(int actorId, Int3 cell, uint serverTick, byte flags)
        {
            ActorId = actorId; Cell = cell; ServerTick = serverTick; Flags = flags;
        }
    }

    /// <summary>WorldPosLatest.Flags 位定义(线上契约,跨发布者共享 ⇒ 住 contracts,支 3-d 口径)。
    /// ADR-001 只点名三旗,未给位序 ⇒ 位序是本实现的登记点,改序 = 破协议。</summary>
    public static class PosFlags
    {
        public const int IsMoving       = 1 << 0;
        public const int IsDowned       = 1 << 1;
        public const int OcclusionHint  = 1 << 2;
    }

    /// <summary>第二 QoS 通道(与三流可靠 pipe 分离;P0 = 本地占位实现,ADR-001 §P0 预埋)。
    /// 44 消费纪律:只读声源锚点,不写回、不做判定(AC-44 家族 / ADR-001 Validation)。</summary>
    public interface IPositionalChannel
    {
        void PublishLatest(in WorldPosLatest p);                      // 主机:按 ActorId 覆盖写
        bool TryReadLatest(int actorId, out WorldPosLatest p);        // 消费:取最新值(本机帧)
        void SubscribeActor(int actorId, Action<WorldPosLatest> onUpdate); // 订阅单实体
    }
}
