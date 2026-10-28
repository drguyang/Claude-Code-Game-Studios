// ADR-010 §一 —— 事件列表容器编解码(字节 ↔ IReadOnlyList<SimEvent>)。
//
// 权威来源:
//   ADR-010 §一 —— 三流「编码记录序列」;按字段名/tag 编码,禁位置打包
//   ADR-006 §五 —— SimEvent 一类值 struct,经自定义编码器
//   SimEventCodec —— 单事件头部编解码(本类逐条复用之,不重复实现)
//
// 本文件补的缺口(S19-1 判据 ⑦,2026-10-24 实测):
//   `SimEventCodec` **只解单事件头部**(`Decode(ReadOnlySpan<byte>) → SimEvent`),
//   全仓**无列表容器编解码器**(`grep -rn "IReadOnlyList<SimEvent>" unity/Assets/Sim.Codec`
//   ⇒ 0)⇒ `Load` 的 `Replaying` 前半段(「字节 → 列表」)无可调之物。本类即该段。
//
// 布局:`[eventCount:i32][每事件: len:i32 + headerBytes[len]]`
//   —— 逐条封装形式与 `SaveCodec.WriteEvent` / `ReadEvent` **逐字相同**
//      (`SaveCodec.cs:17/:24`:int32 长度前缀 + header 字节段),故两者可互操作。
//
// ⚠️ 本类**不做**校验和、不做头部 —— 它只处理「正文里的事件段」。

using System;
using System.Collections.Generic;
using System.IO;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.Codec
{
    /// <summary>事件列表的容器编解码。定长计数前缀 + 逐条长度前缀。</summary>
    public static class SimEventListCodec
    {
        /// <summary>写事件列表。空列表合法(写 count=0)。</summary>
        /// <exception cref="ArgumentNullException"><paramref name="events"/> 为 null。</exception>
        /// <exception cref="ArgumentNullException"><paramref name="codec"/> 为 null。</exception>
        public static void Write(IReadOnlyList<SimEvent> events, ISaveCodec codec, ref CodecWriter w)
        {
            if (events == null) throw new ArgumentNullException(nameof(events));
            if (codec == null) throw new ArgumentNullException(nameof(codec));

            w.WriteInt32LittleEndian(events.Count);
            for (int i = 0; i < events.Count; i++)
            {
                // ⚠️ 不得写 `in events[i]` —— `in` 要求可按引用传递的表达式,
                //    索引器调用是**返回值**,C# 直接拒(CS8156)。先落局部再传。
                SimEvent e = events[i];
                codec.WriteEvent(in e, ref w);
            }
        }

        /// <summary>
        /// 读事件列表。段须恰好读尽 —— 多一个字节即抛(防「长度写错但前 N 条对」的静默错读)。
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="codec"/> 为 null。</exception>
        /// <exception cref="InvalidDataException">count 为负 / 段未读尽 / 内部事件解码失败。</exception>
        public static IReadOnlyList<SimEvent> Read(ref CodecReader r, ISaveCodec codec)
        {
            IReadOnlyList<SimEvent> list = ReadWithinBody(ref r, codec);
            if (r.Remaining != 0)
                throw new InvalidDataException(
                    $"事件列表段未读尽,剩余 {r.Remaining} 字节(布局与长度前缀不符)");
            return list;
        }

        /// <summary>
        /// 复合体内的计数前缀读(v1 布局 = 事件段 + 折叠段,**事件段后还有后续段**)。
        /// 🔁 TD-persist-03 修复轮:复合体里事件段**不读尽**是常态 ⇒ 读尽断言上移给
        /// 调用方(存档体末段须读尽,由 <c>SaveService.ParseBody</c> 经折叠段的
        /// 读尽校验闭合)。段边界 = 折叠段自描述的 count 前缀。
        /// <para>🔁 <b>2026-10-28 ADR-034 就地加注</b>:布局已是 v2 = 事件段 + 折叠段 + **载荷段**,
        /// 且折叠段**不再**是末段(故它不能再靠「吃尽剩余 + 段末校验」闭合)。
        /// 读尽断言现落在**载荷段**末:`SaveService.ParseBody` 末尾的
        /// <c>EnsureFullyConsumed</c>。本节其余口径(段边界 = count 前缀)不变。</para>
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="codec"/> 为 null。</exception>
        /// <exception cref="InvalidDataException">count 为负 / 超上界 / 内部事件解码失败。</exception>
        public static IReadOnlyList<SimEvent> ReadWithinBody(ref CodecReader r, ISaveCodec codec)
        {
            if (codec == null) throw new ArgumentNullException(nameof(codec));

            int count = r.ReadInt32LittleEndian();
            if (count < 0)
                throw new InvalidDataException($"事件列表 count={count} 为负");
            // 每条事件至少 4 字节(长度前缀)⇒ 上界校验防超大分配
            if (count > r.Remaining / 4)
                throw new InvalidDataException(
                    $"事件列表 count={count} 超出剩余字节可容纳的上界({r.Remaining / 4})");

            var list = new SimEvent[count];
            for (int i = 0; i < count; i++)
                list[i] = codec.ReadEvent(ref r);

            return list;
        }

        /// <summary>便捷重载:从字节段直接读(段须恰为事件列表本身,不含头部/校验和)。</summary>
        public static IReadOnlyList<SimEvent> Decode(ReadOnlySpan<byte> segment, ISaveCodec codec)
        {
            var r = new CodecReader(segment);
            return Read(ref r, codec);
        }

        /// <summary>便捷重载:写成独立字节段。</summary>
        public static byte[] Encode(IReadOnlyList<SimEvent> events, ISaveCodec codec)
        {
            var w = new CodecWriter();
            Write(events, codec, ref w);
            return w.ToArray();
        }
    }
}
