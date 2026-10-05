// 权威来源:ADR-014 §二/§五(运行期读烘焙产物,零 JSON 解析;E-13 启动期硬失败口径)
//          · CookedFormat.cs(布局共参照)· Story 003(F-8.1 定表读面;与
//            Editor.Tools.Bake.DiagnosisReadFloorCookedWriter **严格镜像** —— 改一处必须改两处)
//
// ⚠️ 任何字段缺失 / 魔数不符 / 长度不符 / 版本不匹配 ⇒ 显式 throw(启动期硬失败,E-13 口径),
//    绝不返回默认值,绝不静默降级。
//
// ⚠️ 住前缀命名空间 —— 受 DiagnosisBoundaryGates 源层谓词约束:
//    · **无 `System.IO` 词面**(D-PERSIST)⇒ 不用 `BinaryReader` / `InvalidDataException`
//      (后者恰住 System.IO 命名空间)⇒ 改**手动字节游标** + `InvalidOperationException`
//      带 `[E-13]` 上下文(与 `DiagnosisSignCookedCodec` 同款硬失败形状);
//    · **零 `Fix` 构造 / 调用**(D-FIX IL 含 `.ctor`)⇒ 系数读作 raw `long`(不构 Fix)。
//
// ⚠️ 全部字段 = 整数 / raw i64 / binary32(float;G-4 位宽钉死 —— 禁 double),零解析器。

using System;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Presentation.Diagnosis
{
    /// <summary>F-8.1 定表烘焙产物的读方(与 <c>DiagnosisReadFloorCookedWriter</c> 镜像)。
    /// <para>手动 little-endian 字节游标;失败即 throw(E-13)。</para>
    /// </summary>
    public static class DiagnosisReadFloorCookedCodec
    {
        /// <summary>读方认领的 <c>schema_version</c>(源恰为此值,否则 = ADR-014 §五「版本不匹配
        /// 硬失败」⇒ E-13 拒收;升级 schema 须同步改本常量与绑定层)。</summary>
        public const uint ExpectedSchemaVersion = 1u;

        /// <summary>定表载荷的**固定头**字节数(skill_cap i32 4 + 3 个 i64 系数 24 + 表长 i32 4 = 32)。
        /// <para>⚠️ 与 <c>DiagnosisReadFloorCookedWriter</c> 的写序严格镜像 —— 改一处必改两处。</para></summary>
        public const int FixedHeadBytes = 32;

        /// <summary>读定表(含 <c>ConfigVersion</c> / <c>SchemaVersion</c>)。</summary>
        /// <exception cref="InvalidOperationException">头部损坏 / 版本不匹配 / 载荷长度不符 ——
        /// 启动期硬失败(消息带 [E-13] 上下文)。</exception>
        public static DiagnosisReadFloorDataSet Read(byte[] bytes)
        {
            uint configVersion;
            uint schemaVersion;
            int pos = ValidateHeader(bytes, out configVersion, out schemaVersion);

            // 固定头先钳制:表长字段本身不可信 ⇒ 剩余载荷须至少装得下固定头
            if (bytes.Length - pos < FixedHeadBytes)
                throw E13($"载荷 {bytes.Length - pos} B < 固定头 {FixedHeadBytes} B —— 产物损坏");

            int skillCap = ReadI32(bytes, ref pos);
            long baseReadRaw = ReadI64(bytes, ref pos);
            long readFloorMinRaw = ReadI64(bytes, ref pos);
            long readGammaRaw = ReadI64(bytes, ref pos);
            int count = ReadI32(bytes, ref pos);

            if (skillCap <= 0)
                throw E13($"skill_cap={skillCap} ≤ 0 —— 产物损坏");
            if (count != skillCap + 1)
                throw E13($"表长 count={count} ≠ skill_cap({skillCap}) + 1 —— 覆盖域须为 [0, SKILL_CAP]");
            // 钳制:表长 × 4 B 装不进剩余载荷 ⇒ 伪造计数,在分配之前拒
            if ((long)count * 4 > bytes.Length - pos)
                throw E13($"表长 {count} × 4 B 超出剩余载荷 {bytes.Length - pos} B —— 伪造计数,产物损坏");

            var floor = new float[count];
            for (int i = 0; i < count; i++)
                floor[i] = ReadF32(bytes, ref pos);

            if (pos != bytes.Length)
                throw E13($"载荷尾部不齐(读至 {pos},共 {bytes.Length})—— 产物损坏");

            return new DiagnosisReadFloorDataSet
            {
                Table = new DiagnosisReadFloorTable(
                    skillCap, baseReadRaw, readFloorMinRaw, readGammaRaw, floor),
                ConfigVersion = configVersion,
                SchemaVersion = schemaVersion,
            };
        }

        // ── 头部校验(20 B:DYJC + formatVersion + schemaVersion + configVersion + payloadLength)──

        private static int ValidateHeader(byte[] bytes, out uint configVersion, out uint schemaVersion)
        {
            configVersion = 0;
            schemaVersion = 0;

            if (bytes == null || bytes.Length < CookedFormat.HeaderSize)
                throw E13($"字节不足头部({CookedFormat.HeaderSize} B)—— 产物损坏");

            if (bytes[0] != (byte)'D' || bytes[1] != (byte)'Y' ||
                bytes[2] != (byte)'J' || bytes[3] != (byte)'C')
                throw E13("魔数不符(期待 \"DYJC\")—— 拒收");

            uint formatVersion = ReadU32At(bytes, 4);
            if (formatVersion > CookedFormat.FormatVersion)
                throw E13($"formatVersion={formatVersion} > 读方 {CookedFormat.FormatVersion}" +
                          " —— 读方过旧,拒收");

            schemaVersion = ReadU32At(bytes, 8);
            if (schemaVersion != ExpectedSchemaVersion)
                throw E13($"schemaVersion={schemaVersion} ≠ 读方认领 {ExpectedSchemaVersion}" +
                          " —— 版本不匹配,拒收(ADR-014 §五)");
            configVersion = ReadU32At(bytes, 12);
            uint payloadLength = ReadU32At(bytes, 16);
            if (bytes.Length - CookedFormat.HeaderSize != payloadLength)
                throw E13($"载荷长度不符(头声明 {payloadLength}," +
                          $"实际 {bytes.Length - CookedFormat.HeaderSize})—— 产物损坏");

            return CookedFormat.HeaderSize;
        }

        // ── 游标读件(手动 little-endian;越界 = E-13)──

        private static int ReadI32(byte[] b, ref int pos)
        {
            if (pos + 4 > b.Length) throw E13("读 i32 越界 —— 产物损坏");
            int v = b[pos] | (b[pos + 1] << 8) | (b[pos + 2] << 16) | (b[pos + 3] << 24);
            pos += 4;
            return v;
        }

        private static long ReadI64(byte[] b, ref int pos)
        {
            if (pos + 8 > b.Length) throw E13("读 i64 越界 —— 产物损坏");
            ulong lo = (uint)(b[pos] | (b[pos + 1] << 8) | (b[pos + 2] << 16) | (b[pos + 3] << 24));
            int hiStart = pos + 4;
            ulong hi = (uint)(b[hiStart] | (b[hiStart + 1] << 8) |
                              (b[hiStart + 2] << 16) | (b[hiStart + 3] << 24));
            pos += 8;
            return (long)((hi << 32) | lo);
        }

        /// <summary>读 binary32(little-endian)。用手动字节组装 + `BitConverter.Int32BitsToSingle`
        /// (端序无关,不经 `BitConverter.ToSingle` 的端序敏感路径;G-4 位宽钉死 Single)。</summary>
        private static float ReadF32(byte[] b, ref int pos)
        {
            if (pos + 4 > b.Length) throw E13("读 float32 越界 —— 产物损坏");
            int bits = b[pos] | (b[pos + 1] << 8) | (b[pos + 2] << 16) | (b[pos + 3] << 24);
            pos += 4;
            return BitConverter.Int32BitsToSingle(bits);
        }

        private static uint ReadU32At(byte[] b, int offset) =>
            (uint)(b[offset] | (b[offset + 1] << 8) | (b[offset + 2] << 16) | (b[offset + 3] << 24));

        private static InvalidOperationException E13(string what) =>
            new InvalidOperationException($"[E-13] diagnosis_read_floor cooked:{what}");
    }

    /// <summary>F-8.1 定表烘焙产物的解码形(<c>diagnosis_read_floor.cooked.bytes</c>)。
    /// <para><see cref="ConfigVersion"/> = 源数据集内容哈希派生(u32,ADR-014 §五)。</para></summary>
    public struct DiagnosisReadFloorDataSet
    {
        /// <summary>定表(系数 + <c>[0..SKILL_CAP]</c> 门槛值)。</summary>
        public DiagnosisReadFloorTable Table;

        /// <summary>烘焙时从源 JSON 文本派生的内容版本号(u32)。</summary>
        public uint ConfigVersion;

        /// <summary>烘焙时随产物落盘的 schema 版本(源 <c>schema_version</c> 字段)。</summary>
        public uint SchemaVersion;
    }
}
