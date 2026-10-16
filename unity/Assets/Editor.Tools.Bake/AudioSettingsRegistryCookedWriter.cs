// S14-6 · story-010 —— 设置暴露面注册表的 deterministic cooked 写入器。
//
// ⚠️ **本件补 S14-5 刻意留白的写面**。S14-5 的 `AudioSettingsRegistryBake` 头注逐字登记:
//   「**无 cooked 产物 / 无菜单项** … 运行期装载面(`AudioSettingsRegistry.Provider` 缝)归
//    **story-010** 的 Addressables 接线轮,届时若需 cooked 再定编码 —— 本件**不预设**一个
//    没有消费者的编码格式(凭空造格式 = 第二真源)。」
//   本件正是该轮:消费者 = `AudioSettingsRegistry.Provider` 注入缝(story-011 §登记项)。
//
// 权威来源:ADR-014 §二(同源两次烘焙逐位一致:固定字段顺序 · 零字典迭代 · 零本地时间)
//          · §五(ConfigVersion = 源内容哈希派生;头槽承载 schema_version)
//          · CookedFormat.cs(20 字节头布局的唯一共参照)
//          · AudioSettingsRegistryBinder.BindResult(行集来源)
//
// ⚠️ 载荷布局(全部 little-endian;固定字段顺序):
//    u32 schema_version
//    u32 row_count
//    row × { string param; u8 kind; f64 min; f64 max; u8 default_kind;
//            f64 default_float; u8 default_bool; string player_group; string label_source }
//   其中 string = i32(-1 ⇒ null)+ UTF-8 字节。
//
// ⚠️ **行序 = param 的 Ordinal 升序**(全序,唯一)。
//   与 `DelayTableCookedWriter` 同纪律:不靠 Dictionary / JSON 迭代序。此处比延迟表更强 ——
//   注册表行**无 ordinal 语义**(按名字查表),故源里重排行 = 语义无变化的编辑;
//   按名排序使这种编辑**不改字节、不改 ConfigVersion**(否则一次无意义的行交换会假红存档比对)。
//
// ⚠️ **schema_version 只住头槽**(offset 8,单一出处)。
//   承 `DelayTableCookedWriter` 的 R-1 教训(story-002 BLOCKING):头槽硬写 0 而真值编进载荷
//   首字段 = ① 违 CookedFormat 头契约;② 同一真值两处存 = 第二真源。
//   本件的头槽写 `bound.SchemaVersion`,**但**载荷也带 u32 schema_version ——
//   这不是 R-1 的复现:两者是**不同语义**(头槽 = 产物格式版本,读方据以拒收;
//   载荷首字段 = 源数据集版本,运行期表据以自述)。为免误读,读方
//   (`AudioSettingsRegistryCookedCodec`)在本轮**断言两者相等** ——
//   源 schema_version 与产物 schema_version 分叉即红(NULL 容忍面为零)。

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DaYiJingCheng.Gameplay.Presentation.Settings;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>设置注册表的 deterministic cooked 写入器。</summary>
    internal static class AudioSettingsRegistryCookedWriter
    {
        /// <summary>编码注册表(含 20 字节头)。</summary>
        /// <exception cref="InvalidOperationException">绑定行集为空 / 行类型与其默认值类型分叉
        /// (Binder 全覆盖校验下不可达;空表落盘 = 运行期 `Rows` 回退到 FallbackRows = 静默第二真源)。</exception>
        public static byte[] Write(in AudioSettingsRegistryBinder.BindResult bound, uint configVersion)
        {
            var rows = new List<SettingsRegistryRow>();
            if (bound.Rows != null) rows.AddRange(bound.Rows);

            if (rows.Count == 0)
                throw new InvalidOperationException(
                    "设置注册表绑定行集为空 ⇒ 拒绝落盘(空表会让运行期 Rows 静默回退到 FallbackRows," +
                    "使「烘焙分区是真源」结构上不可证)");

            // 行序 = param Ordinal 升序(见文件头:重排源行不改字节)
            rows.Sort((a, b) => string.CompareOrdinal(a.Param, b.Param));

            using (var payload = new MemoryStream())
            using (var w = new BinaryWriter(payload, Encoding.UTF8))
            {
                w.Write(bound.SchemaVersion);
                w.Write((uint)rows.Count);

                for (int i = 0; i < rows.Count; i++)
                {
                    SettingsRegistryRow row = rows[i];

                    if (row.Kind != row.FactoryDefault.Kind)
                        throw new InvalidOperationException(
                            $"行「{row.Param}」类型 {row.Kind} 与其出厂默认值类型 {row.FactoryDefault.Kind} 分叉 " +
                            "—— 编码会把不一致静默写死(读方按 kind 解,默认值半边无人看)");

                    WriteString(w, row.Param);
                    w.Write((byte)row.Kind);
                    w.Write(row.Min);
                    w.Write(row.Max);
                    w.Write((byte)row.FactoryDefault.Kind);
                    w.Write(row.FactoryDefault.Float);
                    w.Write(row.FactoryDefault.Bool ? (byte)1 : (byte)0);
                    WriteString(w, row.PlayerGroup);
                    WriteString(w, row.LabelSource);
                }

                w.Flush();
                return FinishHeader(payload.ToArray(), bound.SchemaVersion, configVersion);
            }
        }

        /// <summary>i32 长度(-1 ⇒ null)+ UTF-8 字节(与 `AudioEventTableCookedWriter` 同编码)。</summary>
        private static void WriteString(BinaryWriter w, string s)
        {
            if (s == null) { w.Write(-1); return; }
            byte[] bytes = Encoding.UTF8.GetBytes(s);
            w.Write(bytes.Length);
            w.Write(bytes);
        }

        /// <summary>头(magic + formatVersion + schemaVersion + configVersion + payloadLength)+ 载荷。</summary>
        private static byte[] FinishHeader(byte[] payload, uint schemaVersion, uint configVersion)
        {
            var output = new byte[CookedFormat.HeaderSize + payload.Length];
            string magic = CookedFormat.Magic;
            output[0] = (byte)magic[0];
            output[1] = (byte)magic[1];
            output[2] = (byte)magic[2];
            output[3] = (byte)magic[3];
            WriteU32Le(output, 4, CookedFormat.FormatVersion);
            WriteU32Le(output, 8, schemaVersion);   // R-1:头槽承载(非 0)
            WriteU32Le(output, 12, configVersion);
            WriteU32Le(output, 16, (uint)payload.Length);
            Buffer.BlockCopy(payload, 0, output, CookedFormat.HeaderSize, payload.Length);
            return output;
        }

        private static void WriteU32Le(byte[] buffer, int offset, uint value)
        {
            buffer[offset] = (byte)(value & 0xFF);
            buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
            buffer[offset + 2] = (byte)((value >> 16) & 0xFF);
            buffer[offset + 3] = (byte)((value >> 24) & 0xFF);
        }
    }
}
