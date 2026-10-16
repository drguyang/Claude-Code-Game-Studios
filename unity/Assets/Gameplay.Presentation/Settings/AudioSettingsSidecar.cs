// 权威来源:production/epics/audio-system/story-011-settings-exposure-reset.md(AC-44-19 归零三步)
// GDD:design/gdd/audio-system.md AC-44-19(:1153-1159)· §Tuning Knobs 落点裁决(:707-716)
// ADR-018 §一(44 只渲染不持值)· OQ-SS-5=甲(端本地 sidecar,与存档位解耦)
//   · settings-shell-42.md OQ-SS-5 = 甲:端本地 sidecar,承 **3 规则四模式**(schema 头 + 载荷)
//   · OQ-SS-7 = 甲:归源三步 = 写默认 → 推 mixer → **落 sidecar**(缺第三步 = 归源无效)
// ADR-010(存档格式纪律的**同类**):显式版本化格式 · 损坏可诊断 · **禁引擎黑盒**
//   (丙案 PlayerPrefs 已否决 —— 无 schema 无版本)
//
// ⚠️ 文件布局(承 `Gameplay.Input/BindingsStore.cs` 两文件先例;头部不得与载荷同文件):
//   audio_settings.sidecar —— 本类自写的**载荷**:逐行 `param=value`(按 param 序数排序 ⇒ 确定性)。
//   audio_settings.schema.txt —— 本类自写的**头部**:format_version · registry_signature · param_count。
// ⚠️ **载荷刻意不用 JSON** —— 玩家构建零 JSON 解析器(ADR-014 §二 / AC-44-D4);设置值是
//   端本地偏好,不走烘焙管线 ⇒ 自持一个**无解析器**的行格式(唯一解析 = 按 '=' 切分 + 类型转换)。
// ⚠️ **只写盘不持值**:本类不缓存任何设置值(值真源 = <see cref="AudioSettingsStore"/>);
//   与 42 壳「零缓存宪法」(AC-42-G2)同族纪律的 store 侧对应。
// ⚠️ 失配语义承 3 规则五:签名不符 / 半截 / 头缺失 ⇒ **改名备份 + 载入默认 + 日志三要素**,
//   不静默、不部分恢复(半截载荷会让「设备偏好」静默错位)。

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace DaYiJingCheng.Gameplay.Presentation.Settings
{
    /// <summary><see cref="AudioSettingsSidecar.Load"/> 的出口(承 3 规则四读序的结果面)。</summary>
    public enum SettingsLoadResult
    {
        /// <summary>两文件皆无 —— 首次启动:出厂默认即所载(正常路径,非异常)。</summary>
        NoSidecar,

        /// <summary>头部签名匹配,载荷已逐行载入。</summary>
        Applied,

        /// <summary>失配(签名不同 / 头缺字段 / 载荷半截)—— 不采用载荷,维持出厂默认;
        /// 陈旧 sidecar 已改名备份 + 日志。</summary>
        Mismatch,
    }

    /// <summary>设置值的端本地 sidecar 读写(两文件:自写行格式载荷 + 自写头部)。
    /// <para>承 <c>Gameplay.Input.BindingsStore</c> 的 3 规则四模式(布局 / 读序 / 失配恢复同族),
    /// 但**载荷格式不同**:3 的载荷由 Unity 出厂 API 产出并逐字节回喂(本类不解析),
    /// 而设置值无出厂 API ⇒ 本类自持一个无解析器的行格式(玩家构建零 JSON 解析器)。</para></summary>
    public sealed class AudioSettingsSidecar
    {
        /// <summary>载荷文件名(落盘面白名单成员之一)。</summary>
        public const string PayloadFileName = "audio_settings.sidecar";

        /// <summary>头部文件名(落盘面白名单成员之一)。</summary>
        public const string SchemaFileName = "audio_settings.schema.txt";

        /// <summary>头部 format_version 当前值(布局演化时递增)。</summary>
        public const int FormatVersion = 1;

        private const string FormatVersionKey = "format_version";
        private const string RegistrySignatureKey = "registry_signature";
        private const string ParamCountKey = "param_count";

        private readonly string _directory;

        /// <summary>以 sidecar 所在目录构造(两文件都直接落在该目录下)。</summary>
        /// <param name="directory">sidecar 目录;空串或 null 抛 <see cref="ArgumentException"/>。</param>
        public AudioSettingsSidecar(string directory)
        {
            if (string.IsNullOrEmpty(directory))
                throw new ArgumentException("sidecar 目录不得为空", nameof(directory));
            _directory = directory;
        }

        /// <summary>载荷文件完整路径(经 <c>Path.Combine</c> 派生;写盘点 1 / 2)。</summary>
        public string PayloadPath => Path.Combine(_directory, PayloadFileName);

        /// <summary>头部文件完整路径(经 <c>Path.Combine</c> 派生;写盘点 2 / 2)。</summary>
        public string SchemaPath => Path.Combine(_directory, SchemaFileName);

        /// <summary>失配恢复的备份扩展名:原名 + 分隔符 + 三位序号。</summary>
        public const string BackupSuffixFormat = ".bak-{0:D3}";

        /// <summary>最近一次写入是否成功(<c>false</c> = 写失败,归零第三步不成立)。
        /// <para>⚠️ 由 <see cref="Save"/> 置位 —— 调用方**不得**以「调用过 Save」代替「Save 成功」。</para></summary>
        public bool LastWriteSucceeded { get; private set; }

        /// <summary>最近一次写失败的诊断(null = 无失败);只读,供归零结果承载。</summary>
        public string LastWriteError { get; private set; }

        /// <summary>把设置集写入 sidecar(两文件)。写失败(只读目录 / 磁盘满)只记诊断并返回
        /// <c>false</c> —— **不抛**、**不换写点**(换写点 = 丙案 PlayerPrefs,OQ-SS-5 已否决)。
        /// <para>文件顺序:先载荷后头部 —— 任一步失败留下半截 ⇒ 下次 Load 视同失配(安全方向)。</para></summary>
        /// <param name="values">设置集(键须 ⊆ 闭集;序数排序后写,确定性)。</param>
        /// <param name="registrySignature">闭集签名(<see cref="ComputeRegistrySignature"/>)。</param>
        /// <returns>true = 两文件皆写成功。</returns>
        public bool Save(IReadOnlyDictionary<string, SettingsValue> values, string registrySignature)
        {
            if (values == null)
            {
                LastWriteSucceeded = false;
                LastWriteError = "设置集缺失(null)—— sidecar 不可写";
                return false;
            }

            var keys = new List<string>(values.Keys);
            keys.Sort(StringComparer.Ordinal);   // 确定性:写入序与字典枚举序无关

            var payload = new StringBuilder();
            for (int i = 0; i < keys.Count; i++)
            {
                string key = keys[i];
                payload.Append(key).Append('=').Append(FormatValue(values[key])).Append('\n');
            }

            string header = string.Join("\n", new[]
            {
                FormatVersionKey + "=" + FormatVersion.ToString(CultureInfo.InvariantCulture),
                RegistrySignatureKey + "=" + (registrySignature ?? string.Empty),
                ParamCountKey + "=" + keys.Count.ToString(CultureInfo.InvariantCulture),
            });

            try
            {
                Directory.CreateDirectory(_directory);
                File.WriteAllBytes(PayloadPath, Encoding.UTF8.GetBytes(payload.ToString()));
                File.WriteAllBytes(SchemaPath, Encoding.UTF8.GetBytes(header));
                LastWriteSucceeded = true;
                LastWriteError = null;
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                LastWriteSucceeded = false;
                LastWriteError = "sidecar 写入失败(只记诊断,写点不换):" + ex.Message;
                return false;
            }
        }

        /// <summary>按读序载入:读头部 → 比对签名 → 匹配才逐行解析载荷。</summary>
        /// <remarks>
        /// 任何失配出口都执行失配恢复(**陈旧 sidecar 改名备份 + 维持出厂默认 + 日志三要素**);
        /// 匹配出口载荷文件原样不动。本方法不抛异常(只读目录 / 权限不足 ⇒ 记诊断)。
        /// </remarks>
        /// <param name="expectedSignature">当前闭集签名(<see cref="ComputeRegistrySignature"/>)。</param>
        /// <param name="loaded">出口:签名匹配时载入的设置集(否则空表)。</param>
        /// <returns>读序出口,见 <see cref="SettingsLoadResult"/>。</returns>
        public SettingsLoadResult Load(string expectedSignature, out IReadOnlyDictionary<string, SettingsValue> loaded)
        {
            loaded = new Dictionary<string, SettingsValue>(StringComparer.Ordinal);

            bool hasPayload = File.Exists(PayloadPath);
            bool hasHeader = File.Exists(SchemaPath);
            if (!hasPayload && !hasHeader)
                return SettingsLoadResult.NoSidecar;   // 首次启动:正常路径,不触发备份

            string storedSignature = null;
            bool headerOk = false;
            if (hasHeader)
            {
                try
                {
                    foreach (string line in File.ReadAllLines(SchemaPath))
                    {
                        int eq = line.IndexOf('=');
                        if (eq <= 0) continue;
                        string k = line.Substring(0, eq);
                        string v = line.Substring(eq + 1);
                        if (string.Equals(k, RegistrySignatureKey, StringComparison.Ordinal))
                            storedSignature = v;
                        else if (string.Equals(k, FormatVersionKey, StringComparison.Ordinal)
                                 && !string.Equals(v, FormatVersion.ToString(CultureInfo.InvariantCulture),
                                                   StringComparison.Ordinal))
                        {
                            // 版本不符 ⇒ 视为失配(布局已演化,旧载荷语义不可信)
                            BackupSidecar(storedSignature, expectedSignature);
                            return SettingsLoadResult.Mismatch;
                        }
                    }
                    headerOk = storedSignature != null;
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    headerOk = false;
                }
            }

            if (!headerOk || !string.Equals(storedSignature, expectedSignature, StringComparison.Ordinal))
            {
                BackupSidecar(storedSignature, expectedSignature);
                return SettingsLoadResult.Mismatch;
            }

            var parsed = new Dictionary<string, SettingsValue>(StringComparer.Ordinal);
            try
            {
                foreach (string line in File.ReadAllLines(PayloadPath))
                {
                    if (line.Length == 0) continue;
                    int eq = line.IndexOf('=');
                    if (eq <= 0 || !TryParseValue(line.Substring(eq + 1), out SettingsValue value))
                    {
                        // 半截 / 不可解析载荷 ⇒ 视同失配(不部分恢复)
                        BackupSidecar(storedSignature, expectedSignature);
                        return SettingsLoadResult.Mismatch;
                    }
                    parsed[line.Substring(0, eq)] = value;
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return SettingsLoadResult.Mismatch;
            }

            loaded = parsed;
            return SettingsLoadResult.Applied;
        }

        /// <summary>失配恢复:陈旧 sidecar 改名备份(原名 + <c>.bak-NNN</c>,不覆盖前次)+ 日志。
        /// 备份失败(只读目录 / 磁盘满)⇒ 记日志跳过备份,不抛、不中断(承 3 规则五同款口径)。</summary>
        /// <param name="storedSignature">头部存储的旧签名(null = 读不出)。</param>
        /// <param name="currentSignature">当前闭集签名。</param>
        public void BackupSidecar(string storedSignature, string currentSignature)
        {
            string oldSig = storedSignature ?? "(未知)";
            bool payloadOk = File.Exists(PayloadPath) && TryMoveToBackup(PayloadPath, "载荷", oldSig, currentSignature);
            bool headerOk = File.Exists(SchemaPath) && TryMoveToBackup(SchemaPath, "头部", oldSig, currentSignature);
            if (payloadOk || headerOk)
                Debug.LogWarning(
                    "[AudioSettingsSidecar] 失配恢复:陈旧 sidecar 已改名备份" +
                    $"(旧签名 {oldSig} / 新签名 {currentSignature});当前维持出厂默认。");
        }

        private static bool TryMoveToBackup(string path, string label, string oldSig, string currentSig)
        {
            for (int serial = 1; serial <= 999; serial++)
            {
                string backupPath = path + string.Format(CultureInfo.InvariantCulture, BackupSuffixFormat, serial);
                if (File.Exists(backupPath)) continue;
                try
                {
                    File.Move(path, backupPath);
                    return true;
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    Debug.LogWarning(
                        $"[AudioSettingsSidecar] 失配:{label}备份失败({ex.Message})—— 跳过备份,不中断。");
                    return false;
                }
            }
            Debug.LogWarning($"[AudioSettingsSidecar] 失配:{label}备份跳过(999 序号全占)—— 不中断。");
            return false;
        }

        /// <summary>闭集签名(纯 BCL 的 FNV-1a 32;跨平台逐位确定,承 ADR-010「只用 BCL 稳定 API」)。
        /// <para>输入 = 按序数排序后的参数名序列(每名后喂 <c>0x00</c>)—— 改名 / 增删 / 换序
        /// 都会改变签名 ⇒ 旧 sidecar 判失配。**刻意不含出厂默认值**:改默认值不该让
        /// 玩家的设备偏好失效(改默认 ≠ 改结构)。</para></summary>
        /// <param name="paramNames">闭集参数名;null ⇒ 返回空串(调用方须先判非空)。</param>
        public static string ComputeRegistrySignature(IReadOnlyList<string> paramNames)
        {
            if (paramNames == null) return string.Empty;

            var sorted = new List<string>(paramNames);
            sorted.Sort(StringComparer.Ordinal);

            uint hash = 2166136261u;   // FNV offset basis
            for (int i = 0; i < sorted.Count; i++)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(sorted[i]);
                for (int b = 0; b < bytes.Length; b++)
                {
                    hash ^= bytes[b];
                    hash *= 16777619u;   // FNV prime
                }
                hash ^= 0x00;
                hash *= 16777619u;
            }

            return hash.ToString("x8", CultureInfo.InvariantCulture);
        }

        private static string FormatValue(SettingsValue value) =>
            value.Kind == SettingsValueKind.Bool
                ? (value.Bool ? "true" : "false")
                : value.Float.ToString("R", CultureInfo.InvariantCulture);

        private static bool TryParseValue(string raw, out SettingsValue value)
        {
            value = default;
            if (raw == null) return false;

            if (string.Equals(raw, "true", StringComparison.Ordinal))
            {
                value = SettingsValue.OfBool(true);
                return true;
            }
            if (string.Equals(raw, "false", StringComparison.Ordinal))
            {
                value = SettingsValue.OfBool(false);
                return true;
            }

            if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double f))
            {
                value = SettingsValue.OfFloat(f);
                return true;
            }
            return false;
        }
    }
}
