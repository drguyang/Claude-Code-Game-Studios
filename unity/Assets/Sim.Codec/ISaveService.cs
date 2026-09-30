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

    /// <summary>存档服务接口。</summary>
    public interface ISaveService
    {
        /// <summary>写 checkpoint。序列化主线程,写盘后台线程。</summary>
        void Checkpoint(SaveSlot slot);

        /// <summary>退出保存。</summary>
        void SaveOnExit();

        /// <summary>读档。校验和 → 自动回退 bak → 迁移链 → 三流重放。</summary>
        void Load(SaveSlot slot);
    }
}
