// `case_pattern.cooked.bytes`(37 的同源检测旋钮)的**运行期解码器** —— T20 · 2026-11-10
//
// 权威来源:
//   ADR-014 §二/§五(运行期读烘焙产物)· `CookedFormat.cs`(布局契约)
//   · 写方严格镜像:`Editor.Tools.Bake/CasePatternCookedWriter.cs`
//   · 域判据单一出处:`CasePatternBinder`(构建期;本件是**加载期**的第二道,同域不同时机 ——
//     与 `TelemetryAnalyticsCookedCodec` 的「两半各有一道」同口径)
//
// ⚠️ 载荷布局(全部 little-endian):
//   pattern_threshold : i32
//
// ⚠️ 任何字段缺失 / 魔数不符 / 长度不符 / 越域 ⇒ 显式 throw(启动期硬失败,E-13 口径)。

using System;
using System.IO;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Presentation
{
    /// <summary>`case_pattern.cooked.bytes` 的运行期解码器。</summary>
    public static class CasePatternCookedCodec
    {
        /// <summary>载荷固定字节数(i32 = 4)。</summary>
        public const int PayloadBytes = 4;

        /// <summary>读旋钮表。</summary>
        /// <exception cref="InvalidDataException">头部损坏 / 长度不符 / 阈值越域 —— 启动期硬失败。</exception>
        public static CasePatternDataSet Read(byte[] bytes)
        {
            using (BinaryReader r = CookedReadUtil.OpenChecked(bytes, "case_pattern", out uint configVersion))
            {
                int threshold = r.ReadInt32();
                CookedReadUtil.AssertFullyConsumed(r, "case_pattern");

                if (threshold < 2)
                    throw new InvalidDataException(
                        $"[cooked] case_pattern pattern_threshold = {threshold} —— 须 ≥ 2 " +
                        "(GDD case-system.md:480 安全范围;1 = 单例即同源)" +
                        " —— 加载期硬失败,不得静默取默认值(E-13)");

                return new CasePatternDataSet(threshold, configVersion);
            }
        }
    }

    /// <summary>37 同源检测旋钮表的解码形。</summary>
    public readonly struct CasePatternDataSet
    {
        /// <summary>cooked 产物文件名(= Addressables address;单一出处
        /// = <c>CasePatternBake.CookedFileName</c>,边界程序集不引用 Editor.Tools.Bake,故为字面串)。</summary>
        public const string CookedFileName = "case_pattern.cooked.bytes";

        /// <summary>`PATTERN_THRESHOLD`(≥ 2;GDD P0 = 3)。</summary>
        public readonly int PatternThreshold;

        /// <summary>烘焙时从源 JSON 文本派生的内容版本号(u32,ADR-014 §五)。</summary>
        public readonly uint ConfigVersion;

        /// <summary>构造(读方唯一调用点)。</summary>
        public CasePatternDataSet(int patternThreshold, uint configVersion)
        {
            PatternThreshold = patternThreshold;
            ConfigVersion = configVersion;
        }
    }
}
