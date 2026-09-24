// 权威来源:Story 010 AC-21a-53(Fix 自定义编码器往返逐位复原;含 drug_potency / half_life /
//          axis_offset_by_quality[] 含负值)· ADR-006 §五(D-21-18:Fix 不可经 Unity 内置序列化器)
//          · ADR-010(存档全二进制;_raw long 显式写出/读入)· TR-itemdb-028(符号位不得丢)
//
// ⚠️ 断言面纪律(AC-53 原文):本文件断言的是「**自定义编码器正确**」;
//    Unity 内置序列化器的行为(跳过 readonly struct 私有字段)是**观察记录**,不作断言 ——
//    防「引擎变好反而测试失败」。观察用例只 Debug.Log + 无失败路径。
//
// ⚠️ 落点:账本路径 = tests/unit/item_database/fix_codec_roundtrip.cs(GDD 指名,照录);
//    Unity 只编译 unity/Assets/ 树 ⇒ 真身 = 本文件(Story 001–009 同一先例)。
// ⚠️ FixCodec 是 Sim.Codec **internal**,经 AssemblyInfo InternalsVisibleTo("Sim.Contracts.Tests")
//    直测(ADR-025 §① 声明的 IVT 面)。
//
// 测试纪律:test_[scenario]_[expected];TestCase 字面 = 边界值(coding-standards 例外:精确数字即本测之点)。

using System;
using NUnit.Framework;
using UnityEngine;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.Codec;

namespace DaYiJingCheng.Tests.Unit.ItemDatabase
{
    [TestFixture]
    internal sealed class FixCodecRoundtripTest
    {
        // ══════════════ 裸 _raw 往返(8 字节小端;AC-53 断言面)══════════════

        [TestCase(0L, TestName = "AC-53 raw=0")]
        [TestCase(1L, TestName = "AC-53 raw=+1 最小正")]
        [TestCase(-1L, TestName = "AC-53 raw=-1 符号位")]
        [TestCase(32768L, TestName = "AC-53 raw=32768 半单位 1/2")]
        [TestCase(-32768L, TestName = "AC-53 raw=-32768 负半单位")]
        [TestCase(49152L, TestName = "AC-53 raw=49152 3/4")]
        [TestCase(long.MinValue, TestName = "AC-53 raw=long.MinValue 极小")]
        [TestCase(long.MaxValue, TestName = "AC-53 raw=long.MaxValue 极大")]
        public void test_ac21a53_fixRaw_nakedRoundTrip_bitwiseEqual(long raw)
        {
            var w = new CodecWriter();
            FixCodec.Write(ref w, new Fix(raw));
            byte[] bytes = w.ToArray();

            Assert.That(bytes.Length, Is.EqualTo(8),
                "FixCodec 裸写 = 恰 8 字节(无 tag、无 float —— ADR-005 存档禁浮点)");

            var r = new CodecReader(bytes);
            Fix restored = FixCodec.Read(ref r);

            Assert.That(restored.Raw, Is.EqualTo(raw),
                $"_raw 逐位复原失败(raw={raw})—— 符号位/极值丢失即「这剂药没效」静默失效(D-21-18)");
        }

        [Test]
        public void test_ac21a53_fixRaw_payloadTaggedPath_bitwiseEqual()
        {
            // 载荷侧实际走的面:WriteFieldFix / ReadFieldFix(转调 FixCodec —— 唯一编码器纪律)。
            var w = new CodecWriter();
            w.WriteFieldFix(1, new Fix(49152L));
            w.WriteFieldFix(2, new Fix(-1L));
            byte[] bytes = w.ToArray();

            var r = new CodecReader(bytes);
            byte tag1 = r.ReadTag();
            Fix a = r.ReadFieldFix();
            byte tag2 = r.ReadTag();
            Fix b = r.ReadFieldFix();

            Assert.That(tag1, Is.EqualTo(1), "tag1 落笔");
            Assert.That(tag2, Is.EqualTo(2), "tag2 落笔");
            Assert.That(a.Raw, Is.EqualTo(49152L), "正向 3/4 逐位");
            Assert.That(b.Raw, Is.EqualTo(-1L), "负值逐位(符号位不丢)");
        }

        // ══════════════ 具名集合 + 数组(AC-53 Given:drug_potency / half_life / axis[])══════════════

        [Test]
        public void test_ac21a53_drugAndAxisSet_roundTripsIncludingNegatives()
        {
            Fix drugPotency = FixParse.Parse("7/8");
            Fix halfLife = FixParse.Parse("12");
            var axisOffsets = new[]
            {
                new Fix(0L),            // 0
                new Fix(-1L),           // 负 1 raw(符号位)
                new Fix(32768L),        // 半单位
                new Fix(-49152L),       // -3/4
                new Fix(long.MinValue), // 极小
                new Fix(long.MaxValue), // 极大
            };

            AssertRoundTrip(drugPotency, "drug_potency");
            AssertRoundTrip(halfLife, "half_life");
            for (int i = 0; i < axisOffsets.Length; i++)
                AssertRoundTrip(axisOffsets[i], $"axis_offset_by_quality[{i}]");

            // 数组整段往返(逐元素,顺序保持)
            var w = new CodecWriter();
            for (int i = 0; i < axisOffsets.Length; i++)
                w.WriteFieldFix((byte)(i + 1), axisOffsets[i]);
            byte[] bytes = w.ToArray();

            var r = new CodecReader(bytes);
            for (int i = 0; i < axisOffsets.Length; i++)
            {
                byte tag = r.ReadTag();
                Assert.That(tag, Is.EqualTo((byte)(i + 1)), $"数组第 {i} 段 tag");
                Fix restored = r.ReadFieldFix();
                Assert.That(restored.Raw, Is.EqualTo(axisOffsets[i].Raw),
                    $"axis_offset_by_quality[{i}] 数组往返逐位不等");
            }
        }

        private static void AssertRoundTrip(Fix value, string quantity)
        {
            var w = new CodecWriter();
            FixCodec.Write(ref w, value);
            var r = new CodecReader(w.ToArray());
            Fix restored = FixCodec.Read(ref r);
            Assert.That(restored.Raw, Is.EqualTo(value.Raw), $"{quantity} 往返逐位不等");
        }

        // ══════════════ 内置序列化器观察探针(记录观察,不作断言)══════════════

        [Test]
        public void test_ac21a53_unitySerializerProbe_observedNotAsserted()
        {
            // AC-53 原文:内置序列化器行为由本探针记录为**观察事实**,不作为断言。
            // 若引擎未来开始正确序列化 Fix,本用例**保持绿** —— 断言只落在自定义编码器对照组。
            const long originalRaw = 49152L;
            var carrier = new FixCarrier { value = new Fix(originalRaw) };

            string json = JsonUtility.ToJson(carrier);
            var restored = JsonUtility.FromJson<FixCarrier>(json);
            long observed = restored != null ? restored.value.Raw : 0L;

            Debug.Log(
                $"[AC-21a-53 观察] Unity 内置序列化器往返:原 raw={originalRaw},观察 raw={observed} " +
                $"(相等={observed == originalRaw})—— 仅记录,不作断言(AC-53 探针纪律);" +
                "存档路径唯一合法承载 = FixCodec 自定义编码器(上面的往返用例即断言面)。");

            // 对照组(唯一的断言):自定义编码器逐位复原。
            var w = new CodecWriter();
            FixCodec.Write(ref w, new Fix(originalRaw));
            var r = new CodecReader(w.ToArray());
            Assert.That(FixCodec.Read(ref r).Raw, Is.EqualTo(originalRaw),
                "自定义编码器对照组必须逐位复原(本用例的全部断言在此)");
        }

        [Serializable]
        private sealed class FixCarrier
        {
            public Fix value;
        }
    }
}
