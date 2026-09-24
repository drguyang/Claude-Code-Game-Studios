// 权威来源:ADR-014 §一/§二(确定性 *.cooked 产物格式)· ADR-010 §七(ConfigVersion u32 进存档头)
//          · Story 008(烘焙写入方在 Editor.Tools.Bake;读取方在 Gameplay.Presentation)
//
// ⚠️ 本格式**零 float/double**(ADR-005/006:全部模拟数学在整数定点域;Fix = raw long)。
// ⚠️ 字节序 = little-endian(固定,不随平台);字段顺序固定 = 同源两次烘焙逐位一致。
// ⚠️ 读写两端(写:Editor.Tools.Bake.CookedWriter / 读:Gameplay.Presentation.CookedCodec)
//    必须严格镜像本文件登记的布局 —— Story 011 黄金哈希负责钉逐位回归。

namespace DaYiJingCheng.Sim.Contracts
{
    /// <summary>*.cooked 产物的**格式契约**(头部布局 + 版本常量)。写读两端唯一共参照。
    /// <para>头部布局(20 字节,全 little-endian):</para>
    /// <code>
    /// offset 0  : magic[4]        = "DYJC"(ASCII,无 NUL)
    /// offset 4  : formatVersion   = u32(本格式自身的演进号;不匹配 ⇒ 读方拒收)
    /// offset 8  : schemaVersion   = u32(作者态 JSON schema_version 回声;不可读 ⇒ ADR-010 §七 致命面)
    /// offset 12 : configVersion   = u32(源数据集内容哈希派生;与存档头比对走 ADR-010 §七,不匹配非致命)
    /// offset 16 : payloadLength   = u32(载荷字节数)
    /// offset 20 : payload         = [payloadLength] 字节
    /// </code>
    /// <para>载荷内:字符串 = i32 字节长 + UTF-8 字节(无终止符);枚举/整数 = i32;
    /// 定点 = i64 raw;布尔 = u8;数组 = i32 元素数(−1 = null,0 = 空);可空 Fix = u8 标志 + i64。</para></summary>
    public static class CookedFormat
    {
        /// <summary>文件魔数(ASCII "DYJC" = DaYi Jing Cheng 首字母)。读方首检,错即拒收。</summary>
        public const string Magic = "DYJC";

        /// <summary>格式版本 —— 头部第 2 个 u32。读方要求 ≤ 本常量(更高 = 读方过旧,拒收硬失败)。</summary>
        public const uint FormatVersion = 1;

        /// <summary>作者态 schema_version 的期望值(头部第 3 个 u32 的回声)。
        /// 不可读/不匹配 = 存档侧致命面(ADR-010 §七);烘焙期与各 JSON 的 schema_version 同源校验。</summary>
        public const uint SchemaVersion = 1;

        /// <summary>头部固定字节数(magic 4 + 四个 u32 = 20)。短于此的输入直接判定损坏。</summary>
        public const int HeaderSize = 20;
    }
}
