// X-1 用户裁定(2026-09-22):Sim.Contracts 内自建 Int3,不用 Unity.Mathematics.int3。
//
// 出处:ADR-018 §二(AudioCueDto.Cell)+ ADR-001 §一之二(WorldPosLatest.Cell)原文写 `int3`,
// 那是 Unity.Mathematics 引擎类型,与 Sim.Contracts 的「引用集 = BCL」+ noEngineReferences:true 冲突。
// 本类型是消解物:字段名 / 语义不变,仅类型换成纯 BCL 的 struct。

namespace DaYiJingCheng.Sim.Contracts
{
    /// <summary>呈现层整数锚点(i32 × 3)。与 WorldPos 区分:本类型是表现态锚点,不进事件流、不参与 sim 判定。</summary>
    public readonly struct Int3
    {
        public readonly int X;
        public readonly int Y;
        public readonly int Z;

        public Int3(int x, int y, int z) { X = x; Y = y; Z = z; }
    }
}
