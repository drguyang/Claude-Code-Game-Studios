// sprint-08 S1.0 · 生产烘焙接缝的 public 转发(仅供测试驱动;零逻辑)
//
// ⚠️ 承 DiagnosisSignBinderProbe 先例:internal Binder 暴露成 public 薄探针,
//    让 EditMode 测试驱动**生产同一台机器**(反空转)。零判据、零转换,只转发。

using System.Collections.Generic;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>病名册词表烘焙接缝的 public 转发(仅供测试驱动;零逻辑)。</summary>
    public static class JudgmentLexiconBinderProbe
    {
        /// <summary>用给定源文本跑完整阶段 2(绑定 + 校验 + 编码),返回 cooked 字节。</summary>
        /// <exception cref="BakeValidationException">任一违例(聚合硬失败)。</exception>
        public static byte[] Bake(string json)
        {
            JudgmentLexiconBinder.BindResult bound = JudgmentLexiconBinder.Bind(json);
            uint configVersion = ConfigVersionUtility.DeriveConfigVersion(new[]
            {
                new KeyValuePair<string, string>(JudgmentLexiconBaker.FileName, json),
            });
            return JudgmentLexiconCookedWriter.Write(bound.Rows, bound.SchemaVersion, configVersion);
        }
    }
}
