// `case_pattern.json`(37 的同源检测旋钮)的 deterministic cooked 写方。
//
// 权威来源:ADR-014 §二/§五(两阶段烘焙 · ConfigVersion 内容哈希)· `CookedFormat.cs`(20 B 头)
//          · `CasePatternBinder.BindResult`(数据唯一来源)
//
// ⚠️ 载荷布局(全部 little-endian)—— 与运行期读方 `CasePatternCookedCodec` 严格镜像:
//   pattern_threshold : i32

using System;
using System.IO;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>`case_pattern`(37 的同源检测旋钮)deterministic cooked 写入器。</summary>
    internal static class CasePatternCookedWriter
    {
        /// <summary>编码旋钮表(含 20 字节头)。</summary>
        public static byte[] Write(
            in CasePatternBinder.BindResult r, uint schemaVersion, uint configVersion)
        {
            using (var payload = new MemoryStream())
            using (var w = new BinaryWriter(payload, System.Text.Encoding.UTF8))
            {
                w.Write(r.PatternThreshold);
                w.Flush();
                return ClinicMachineCookedWriter.FinishHeader(
                    payload.ToArray(), schemaVersion, configVersion);
            }
        }
    }
}
