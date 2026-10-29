// 权威来源:ADR-014 §二/§五(运行期读烘焙产物,零 JSON 解析;E-13 启动期硬失败口径)
//          · CookedFormat.cs(布局共参照)
//          · `docs/tech-debt-register.md` `TD-audio-032` ①(调参常量入生产)
//
// ⚠️ 与 Editor.Tools.Bake.PatientAiCookedWriter **严格镜像** —— 改一处必须改两处。
//
// ⚠️ 任何字段缺失 / 魔数不符 / 长度不符 / 版本不匹配 ⇒ 显式 throw(启动期硬失败,E-13 口径),
//    绝不返回默认值,绝不静默降级。
//
// ⚠️ 载荷布局(全部 little-endian):
//    cue_intervals.severe   : i32
//    cue_intervals.mild     : i32
//    cue_intervals.recover  : i32
//    behavior_bands.mild_min      : float32
//    behavior_bands.seek_min      : float32
//    behavior_bands.collapse_min  : float32
//    behavior_bands.recover_max   : float32
//    behavior_bands.hyst          : float32
//    behavior_bands.death_band_min: float32
//
// ⚠️ 阈值以 **float32** 落盘(G-4 位宽钉死 —— 禁 double;承 `DiagnosisReadFloorCookedWriter`
//    的定表同款)。
//    ⚠️ **定点化发生在 `FixParse` 那一步**(binder 把源分数 `"1/5"` 经 Q16.16 → float),
//    不是在本 codec 里 —— GDD `patient-ai.md` 规则五明令「`ai_patient.json` 里禁 float 字段,
//    非空间量写 int 或定点串」⇒ `1/5` 落 Q16.16 后是 `0.199996948`(不可精确表示,定点域本质)。
//    本 codec 只是把**已定点化**的 float 原样搬运,不再二次量化。

using System;
using DaYiJingCheng.Sim.Contracts;   // CookedFormat(20 字节头布局契约)

namespace DaYiJingCheng.Gameplay.PatientAI
{
    /// <summary>`ai_patient.json` 烘焙产物的读方(与 <c>PatientAiCookedWriter</c> 镜像)。
    /// <para>手动 little-endian 字节游标;失败即 throw(E-13)。</para>
    /// </summary>
    public static class PatientAiCookedCodec
    {
        /// <summary>读方认领的 <c>schema_version</c>(源恰为此值,否则 = ADR-014 §五「版本不匹配
        /// 硬失败」⇒ E-13 拒收;升级 schema 须同步改本常量与绑定层)。</summary>
        public const uint ExpectedSchemaVersion = 1u;

        /// <summary>载荷的**固定字节数**(3 × i32 + 6 × float32 = 12 + 24 = 36)。
        /// <para>⚠️ 与 <c>PatientAiCookedWriter</c> 的写序严格镜像 —— 改一处必改两处。</para></summary>
        public const int PayloadBytes = 36;

        /// <summary>读调参表(含 <c>ConfigVersion</c> / <c>SchemaVersion</c>)。</summary>
        /// <exception cref="InvalidOperationException">头部损坏 / 版本不匹配 / 载荷长度不符 ——
        /// 启动期硬失败(消息带 [E-13] 上下文)。</exception>
        public static PatientAiDataSet Read(byte[] bytes)
        {
            uint configVersion;
            uint schemaVersion;
            int pos = ValidateHeader(bytes, out configVersion, out schemaVersion);

            if (bytes.Length - pos != PayloadBytes)
                throw E13($"载荷 {bytes.Length - pos} B ≠ 固定 {PayloadBytes} B —— 产物损坏");

            int severe = ReadI32(bytes, ref pos);
            int mild = ReadI32(bytes, ref pos);
            int recover = ReadI32(bytes, ref pos);

            float mildMin = ReadF32(bytes, ref pos);
            float seekMin = ReadF32(bytes, ref pos);
            float collapseMin = ReadF32(bytes, ref pos);
            float recoverMax = ReadF32(bytes, ref pos);
            float hyst = ReadF32(bytes, ref pos);
            float deathBandMin = ReadF32(bytes, ref pos);

            return new PatientAiDataSet(
                new CueIntervals(severe, mild, recover),
                new BehaviorBands(mildMin, seekMin, collapseMin, recoverMax, hyst, deathBandMin),
                configVersion, schemaVersion);
        }

        // ── 头校验 / 字节游标 ────────────────────────────────────────────

        private static int ValidateHeader(byte[] bytes, out uint configVersion, out uint schemaVersion)
        {
            configVersion = 0;
            schemaVersion = 0;
            if (bytes == null)
                throw E13("字节为 null");
            if (bytes.Length < CookedFormat.HeaderSize)
                throw E13($"长度 {bytes.Length} B < 头 {CookedFormat.HeaderSize} B");

            if (bytes[0] != 'D' || bytes[1] != 'Y' || bytes[2] != 'J' || bytes[3] != 'C')
                throw E13("魔数不符(期望 D Y J C)");

            uint formatVersion = ReadU32At(bytes, 4);
            if (formatVersion != CookedFormat.FormatVersion)
                throw E13($"格式版本 {formatVersion} ≠ {CookedFormat.FormatVersion}");

            schemaVersion = ReadU32At(bytes, 8);
            if (schemaVersion != ExpectedSchemaVersion)
                throw E13($"schema_version {schemaVersion} ≠ 读方认领的 {ExpectedSchemaVersion}" +
                          "(版本不匹配 = 源改了但读方没改,硬失败)");

            configVersion = ReadU32At(bytes, 12);
            uint payloadLength = ReadU32At(bytes, 16);
            if (payloadLength != (uint)(bytes.Length - CookedFormat.HeaderSize))
                throw E13($"头声明的载荷长 {payloadLength} B ≠ 实得 " +
                          $"{bytes.Length - CookedFormat.HeaderSize} B");

            return CookedFormat.HeaderSize;
        }

        private static int ReadI32(byte[] b, ref int pos)
        {
            if (pos + 4 > b.Length) throw E13("读 i32 越界");
            int v = b[pos] | (b[pos + 1] << 8) | (b[pos + 2] << 16) | (b[pos + 3] << 24);
            pos += 4;
            return v;
        }

        /// <summary>读 binary32(G-4 位宽钉死 —— 禁 double)。</summary>
        /// <remarks>⚠️ 本件受 `PresentationDtoGuard` / 边界纪律约束,**不引 `System.IO`** ——
        /// 故不用 `BinaryReader`,手动按 little-endian 拼 int 位型再 `BitConverter.Int32BitsToSingle`
        /// (纯 BCL,无 I/O 依赖)。</remarks>
        private static float ReadF32(byte[] b, ref int pos)
        {
            int bits = ReadI32(b, ref pos);
            return System.BitConverter.Int32BitsToSingle(bits);
        }

        private static uint ReadU32At(byte[] b, int offset) =>
            (uint)(b[offset] | (b[offset + 1] << 8) | (b[offset + 2] << 16) | (b[offset + 3] << 24));

        private static InvalidOperationException E13(string what) =>
            new InvalidOperationException($"[E-13] ai_patient cooked:{what}");
    }

    /// <summary>`ai_patient.json` 烘焙产物的解码形(`ai_patient.cooked.bytes`)。
    /// <para><see cref="ConfigVersion"/> = 源数据集内容哈希派生(u32,ADR-014 §五)。</para></summary>
    public struct PatientAiDataSet
    {
        /// <summary>产物文件名(**单一出处** —— 菜单写盘名与 Addressables address 共用此常量)。
        /// <para>承 `AudioEventDataSet.CookedFileName` 同纪律:两处各写一次字面量 = 第二真源,
        /// 改名时必有一处漏改,且失败形态是「装载不到」而非「编译不过」。</para></summary>
        public const string CookedFileName = "ai_patient.cooked.bytes";

        /// <summary>cue 间隔表(`CUE_INTERVAL[*]`)。</summary>
        public CueIntervals CueIntervals;

        /// <summary>行为分档阈值表(`BEHAVIOR_BAND_*`)。</summary>
        public BehaviorBands BehaviorBands;

        /// <summary>烘焙时从源 JSON 文本派生的内容版本号(u32)。</summary>
        public uint ConfigVersion;

        /// <summary>烘焙时随产物落盘的 schema 版本(源 <c>schema_version</c> 字段)。</summary>
        public uint SchemaVersion;

        public PatientAiDataSet(CueIntervals cueIntervals, BehaviorBands behaviorBands,
                                uint configVersion, uint schemaVersion)
        {
            CueIntervals = cueIntervals;
            BehaviorBands = behaviorBands;
            ConfigVersion = configVersion;
            SchemaVersion = schemaVersion;
        }
    }
}
