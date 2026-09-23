// R-1(U0-b 残留)· Fix 自定义编码器(D-21-18 的可执行形态)。
//
// 权威来源:
//   ADR-006 §五(D-21-18)—— Fix 不得经 Unity 内置序列化器承载;唯一合法落盘形状 =
//     内部 long(Q16.16 raw bits),由**自定义编码器**显式写出 / 读入。
//   ADR-025 §① —— Sim.Codec 成员含「Fix 自定义编码器(**internal** +
//     InternalsVisibleTo("Sim.Contracts.Tests"))」—— 本文件即该声明的兑现;
//     AssemblyInfo.cs 的 IVT 已于 U0 落盘。
//   失效模式(ADR-006 §五 末):交给内置序列化器 ⇒ 静默归零 ⇒ 「这剂药没效」,
//     不崩溃、不报错、回放仍一致 —— 只能靠 EditMode 探针发现
//     (探针落 sim_codec_roundtrip_test.cs,ADR-006 §Validation 同条)。
//
// ⚠️ 载荷字段的读写一律经本类(CodecWriter.WriteFieldFix / CodecReader.ReadFieldFix 转调),
//   不得在别处散写 `value.Raw` —— 那会把「自定义编码器」稀释回位置直写,
//   D-21-18 的命名守卫随之失效。

using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.Codec
{
    /// <summary>Fix(Q16.16)的唯一自定义编解码器。internal —— 载荷 codec 同程序集内用,测试经 IVT 直测。</summary>
    internal static class FixCodec
    {
        /// <summary>写出:_raw 的 long,8 字节小端。不出现 float / double(门 B 语义延伸)。</summary>
        internal static void Write(ref CodecWriter w, Fix value)
            => w.WriteInt64LittleEndian(value.Raw);

        /// <summary>读入:8 字节小端 long 显式构造 Fix(逐位还原 —— 探针断言面)。</summary>
        internal static Fix Read(ref CodecReader r)
            => new Fix(r.ReadInt64LittleEndian());
    }
}
