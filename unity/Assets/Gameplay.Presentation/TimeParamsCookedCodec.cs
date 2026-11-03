// 权威来源:ADR-014 §二/§五(运行期读烘焙产物,零 JSON 解析;E-13 启动期硬失败口径)
//          · CookedFormat.cs(布局共参照)
//          · ADR-038 §D1(时间参数外部化)· Editor.Tools.Bake.TimeParamsCookedWriter(写方,严格镜像)
//
// ⚠️ 任何字段缺失 / 魔数不符 / 长度不符 / 版本不匹配 ⇒ 显式 throw(启动期硬失败,E-13 口径),
//    绝不返回默认值,绝不静默降级。
//
// ⚠️ **本 codec 不做值域校验** —— 那是 `Sim.TimeParams.Validate()` 的职责(AC-5-11),
//    由装载方在拿到 `TimeParamsDataSet` 之后调一次。理由:校验规则要能被**直接注入**
//    的非法值驱动(AC-5-11 的 GIVEN 是「注入各非法配置」),若把规则复制进 codec,
//    就出现第二份判据(改一处忘另一处 = 静默分叉)。

using System;
using System.IO;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Presentation
{
    /// <summary>`5_time.cooked.bytes` 的运行期解码器(与 <c>TimeParamsCookedWriter</c> 镜像)。</summary>
    public static class TimeParamsCookedCodec
    {
        /// <summary>读方认领的 <c>schema_version</c>(源恰为此值;升级 schema 须同步改本常量与绑定层)。</summary>
        public const uint ExpectedSchemaVersion = 2u;

        /// <summary>产物文件名(错误报文用;与 `TimeParamsBaker.CookedFileName` 同值)。</summary>
        private const string ProductName = "5_time";

        /// <summary>载荷固定字节数(5 × i64)—— 与写方 `TimeParamsCookedWriter.PayloadBytes` 同值。</summary>
        public const int PayloadBytes = 40;

        /// <summary>解码。</summary>
        /// <exception cref="InvalidDataException">头部损坏 / schemaVersion 不匹配 / 载荷长度不符 ——
        /// 启动期硬失败(E-13 口径)。</exception>
        public static TimeParamsDataSet Read(byte[] bytes)
        {
            if (bytes == null || bytes.Length < CookedFormat.HeaderSize)
                throw E13($"字节不足头部({CookedFormat.HeaderSize} B)");

            uint schemaVersion = ReadU32Le(bytes, 8);
            if (schemaVersion != ExpectedSchemaVersion)
                throw E13($"schemaVersion={schemaVersion} ≠ 读方认领 {ExpectedSchemaVersion}" +
                          " —— 版本不匹配,拒收(ADR-014 §五)");

            uint payloadLength = ReadU32Le(bytes, 16);
            if (payloadLength != PayloadBytes)
                throw E13($"载荷长度 {payloadLength} B ≠ 固定 {PayloadBytes} B —— 产物损坏");

            using (BinaryReader r = CookedReadUtil.OpenChecked(bytes, ProductName, out uint configVersion))
            {
                var data = new TimeParamsDataSet
                {
                    TicksPerDay = r.ReadInt64(),
                    NightStart = r.ReadInt64(),
                    NightSpan = r.ReadInt64(),
                    TicksPerSeason = r.ReadInt64(),
                    SeasonsPerYear = r.ReadInt64(),
                    ConfigVersion = configVersion,
                    SchemaVersion = schemaVersion,
                };
                CookedReadUtil.AssertFullyConsumed(r, ProductName);
                return data;
            }
        }

        private static uint ReadU32Le(byte[] b, int offset) =>
            (uint)(b[offset] | (b[offset + 1] << 8) | (b[offset + 2] << 16) | (b[offset + 3] << 24));

        private static InvalidDataException E13(string what) =>
            new InvalidDataException($"[E-13] {ProductName} cooked:{what}");
    }
}
