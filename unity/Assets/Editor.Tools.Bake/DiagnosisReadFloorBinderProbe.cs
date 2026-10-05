// 权威来源:ADR-014 §三(阶段 2 绑定的可测接缝)· Story 003
//
// ⚠️ 本件是**测试可见的薄探针** —— 把 `DiagnosisReadFloorBinder` / `DiagnosisReadFloorBaker` 的
//    internal 面暴露成 public,唯一目的是让 EditMode 测试能**驱动生产同一台机器**
//    (反空转:测试断言的必须是生产接缝,不是测试侧重实现)。
//
// ⚠️ 零逻辑:本件不含任何判据、不做任何转换,只转发。判据全部在生产件里。

using System.Collections.Generic;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>生产烘焙接缝的 public 转发(仅供测试驱动;零逻辑)。</summary>
    public static class DiagnosisReadFloorBinderProbe
    {
        /// <summary>用给定源文本跑完整阶段 2(绑定 + C-1/C-5 校验 + 定表生成 + 编码),返回 cooked 字节。</summary>
        /// <exception cref="BakeValidationException">任一违例(聚合硬失败)。</exception>
        public static byte[] Bake(string readFloorJson)
        {
            DiagnosisReadFloorBinder.BindResult bound = DiagnosisReadFloorBinder.Bind(readFloorJson);
            uint configVersion = ConfigVersionOf(readFloorJson);
            return DiagnosisReadFloorCookedWriter.Write(bound, configVersion);
        }

        /// <summary>用给定源文本生成定表(不经编码;供形状断言直读)。</summary>
        /// <exception cref="BakeValidationException">任一违例(聚合硬失败)。</exception>
        public static IReadOnlyList<float> TableOf(string readFloorJson)
            => DiagnosisReadFloorBinder.Bind(readFloorJson).Table;

        /// <summary>派生源文本集的 ConfigVersion(与生产同源,供确定性断言)。</summary>
        public static uint ConfigVersionOf(string readFloorJson)
            => ConfigVersionUtility.DeriveConfigVersion(new[]
            {
                new KeyValuePair<string, string>(
                    DiagnosisReadFloorBaker.ReadFloorFileName, readFloorJson),
            });
    }
}
