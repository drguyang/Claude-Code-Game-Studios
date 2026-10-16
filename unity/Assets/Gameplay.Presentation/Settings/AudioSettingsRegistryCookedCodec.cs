// S14-6 · story-010 —— 设置暴露面注册表 cooked 读方(与
// `Editor.Tools.Bake.AudioSettingsRegistryCookedWriter` **严格镜像**;
// 两侧布局分叉由 `AudioSettingsRegistryBake.Materialize` 的跨侧等值断言钉住)。
//
// 权威来源:
//   ADR-014 §五(cooked 装载 · E-13 失败语义 = 启动期硬失败,零默认值、零静默降级)
//   CookedFormat.cs(20 字节头 + 载荷小件规格的唯一共参照)
//   AC-44-D4(玩家构建**零 JSON 解析器** —— 本件只解字节,不碰任何 JSON 类型)
//
// ⚠️ 手动 little-endian 字节游标;任何缺失 / 魔数不符 / 长度不符 / 版本不匹配 / 尾部不齐
//   ⇒ 显式 throw(E-13 口径;承 `AudioEventCookedCodec` / `JudgmentLexiconCookedCodec` 同纪律)。
// ⚠️ **零 `System.IO` 词面** —— 本件不做文件 I/O,字节由 Addressables 侧喂入。
// ⚠️ 布局逐字镜像写方;**改任一侧必须同改另一侧**
//   (唯一自动兜现 = `AudioSettingsRegistryBake.Materialize` 的真往返 + 跨侧等值断言)。

using System;
using System.Collections.Generic;
using System.Text;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Presentation.Settings
{
    /// <summary>设置注册表烘焙产物的读方(写方 = <c>Editor.Tools.Bake.AudioSettingsRegistryCookedWriter</c>)。</summary>
    public static class AudioSettingsRegistryCookedCodec
    {
        /// <summary>读方认领的 schema_version(不匹配 = E-13 拒收)。</summary>
        public const uint ExpectedSchemaVersion = 1u;

        /// <summary>单行最小字节数(param 4 + kind 1 + min 8 + max 8 + defKind 1 + defFloat 8
        /// + defBool 1 + group 4 + label 4 = 39)—— 仅用于「计数伪造」的下界反推。</summary>
        public const int MinRowBytes = 39;

        /// <summary>读注册表(行集 + 双版本号)。</summary>
        /// <exception cref="InvalidOperationException">头部损坏 / 版本不匹配 / 载荷不齐 —— E-13 硬失败。</exception>
        public static AudioSettingsDataSet Read(byte[] bytes)
        {
            int pos = ValidateHeader(bytes, out uint configVersion, out uint headerSchemaVersion);

            uint payloadSchemaVersion = ReadU32(bytes, ref pos);
            if (payloadSchemaVersion != headerSchemaVersion)
                throw E13($"载荷 schema_version={payloadSchemaVersion} ≠ 头槽 {headerSchemaVersion} " +
                          "—— 同一真值两处存且已分叉(写方契约:两者必须相等)");

            uint count = ReadU32(bytes, ref pos);
            if (count > bytes.Length / MinRowBytes)
                throw E13($"行数={count} 超出行下界 {MinRowBytes} B 反推的上界 —— 伪造计数");

            var rows = new SettingsRegistryRow[count];
            for (uint i = 0; i < count; i++)
                rows[i] = ReadRow(bytes, ref pos);

            if (pos != bytes.Length)
                throw E13($"载荷尾部不齐(读至 {pos},共 {bytes.Length})");

            return new AudioSettingsDataSet(rows, headerSchemaVersion, configVersion);
        }

        private static SettingsRegistryRow ReadRow(byte[] b, ref int pos)
        {
            string param = ReadString(b, ref pos);
            SettingsValueKind kind = ReadKind(b, ref pos, "kind");
            double min = ReadF64(b, ref pos);
            double max = ReadF64(b, ref pos);
            SettingsValueKind defKind = ReadKind(b, ref pos, "default_kind");
            double defFloat = ReadF64(b, ref pos);
            bool defBool = ReadU8(b, ref pos) != 0;
            string playerGroup = ReadString(b, ref pos);
            string labelSource = ReadString(b, ref pos);

            if (defKind != kind)
                throw E13($"行「{param}」kind={kind} ≠ default_kind={defKind} —— 写方契约已破");

            SettingsValue factoryDefault = defKind == SettingsValueKind.Bool
                ? SettingsValue.OfBool(defBool)
                : SettingsValue.OfFloat(defFloat);

            return new SettingsRegistryRow(
                param, kind, min, max, factoryDefault, playerGroup, labelSource);
        }

        private static SettingsValueKind ReadKind(byte[] b, ref int pos, string field)
        {
            byte raw = ReadU8(b, ref pos);
            if (raw > (byte)SettingsValueKind.Bool)
                throw E13($"{field}={raw} ∉ 闭集 {{Float=0, Bool=1}} —— 损坏 / 写读分叉");
            return (SettingsValueKind)raw;
        }

        private static string ReadString(byte[] b, ref int pos)
        {
            int len = ReadI32(b, ref pos);
            if (len == -1) return null;
            if (len < 0 || pos + len > b.Length)
                throw E13($"字符串长度 {len} 越界 —— 损坏");
            string s = Encoding.UTF8.GetString(b, pos, len);
            pos += len;
            return s;
        }

        private static int ValidateHeader(byte[] bytes, out uint configVersion, out uint schemaVersion)
        {
            configVersion = 0;
            schemaVersion = 0;
            if (bytes == null || bytes.Length < CookedFormat.HeaderSize)
                throw E13("产物短于头部 —— 损坏");
            if (bytes[0] != 'D' || bytes[1] != 'Y' || bytes[2] != 'J' || bytes[3] != 'C')
                throw E13("魔数不符(≠ DYJC)—— 损坏");
            uint formatVersion = ReadU32(bytes, 4);
            if (formatVersion > CookedFormat.FormatVersion)
                throw E13($"formatVersion={formatVersion} > 读方 {CookedFormat.FormatVersion} —— 读方过旧");
            schemaVersion = ReadU32(bytes, 8);
            if (schemaVersion != ExpectedSchemaVersion)
                throw E13($"schemaVersion={schemaVersion} ≠ {ExpectedSchemaVersion} —— 版本不匹配");
            configVersion = ReadU32(bytes, 12);
            uint payloadLength = ReadU32(bytes, 16);
            if (payloadLength != bytes.Length - CookedFormat.HeaderSize)
                throw E13($"payloadLength={payloadLength} ≠ 实际 {bytes.Length - CookedFormat.HeaderSize}");
            return CookedFormat.HeaderSize;
        }

        // ══════════════ 字节小件(little-endian;无 BinaryReader —— 手动游标,边界可判)══════════════

        private static byte ReadU8(byte[] b, ref int pos)
        {
            if (pos + 1 > b.Length) throw E13("读 u8 越界 —— 载荷截断");
            return b[pos++];
        }

        private static int ReadI32(byte[] b, ref int pos)
        {
            if (pos + 4 > b.Length) throw E13("读 i32 越界 —— 载荷截断");
            int v = b[pos] | (b[pos + 1] << 8) | (b[pos + 2] << 16) | (b[pos + 3] << 24);
            pos += 4;
            return v;
        }

        private static uint ReadU32(byte[] b, ref int pos)
        {
            if (pos + 4 > b.Length) throw E13("读 u32 越界 —— 载荷截断");
            uint v = (uint)(b[pos] | (b[pos + 1] << 8) | (b[pos + 2] << 16) | (b[pos + 3] << 24));
            pos += 4;
            return v;
        }

        private static uint ReadU32(byte[] b, int offset)
            => (uint)(b[offset] | (b[offset + 1] << 8) | (b[offset + 2] << 16) | (b[offset + 3] << 24));

        /// <summary>读 f64(little-endian;经 `BitConverter.Int64BitsToDouble` —— 零浮点中转,
        /// 位模式逐位往返)。</summary>
        private static double ReadF64(byte[] b, ref int pos)
        {
            if (pos + 8 > b.Length) throw E13("读 f64 越界 —— 载荷截断");
            long bits = (long)(uint)(b[pos] | (b[pos + 1] << 8) | (b[pos + 2] << 16) | (b[pos + 3] << 24))
                      | ((long)(uint)(b[pos + 4] | (b[pos + 5] << 8) | (b[pos + 6] << 16) | (b[pos + 7] << 24)) << 32);
            pos += 8;
            return BitConverter.Int64BitsToDouble(bits);
        }

        private static InvalidOperationException E13(string detail)
            => new InvalidOperationException(
                "[E-13][audio_settings_registry] cooked 读取失败 —— 启动期硬失败,绝不降级:" + detail);
    }
}
