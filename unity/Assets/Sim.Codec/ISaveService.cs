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

    /// <summary>存档服务接口（低层原语）。高层方法（Checkpoint/SaveOnExit/Load）归后续 story。</summary>
    public interface ISaveService
    {
        /// <summary>原子写字节到存档文件。</summary>
        void SaveBytes(SaveSlot slot, byte[] data);

        /// <summary>读档字节。校验和验证 + 自动回退 bak。</summary>
        byte[] LoadBytes(SaveSlot slot);
    }
}
