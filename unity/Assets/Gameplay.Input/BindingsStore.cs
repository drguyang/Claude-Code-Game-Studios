// Story 003 · 绑重 overrides sidecar 持久化(系统 3 的唯一落盘职责)
//
// 权威来源:
//   AC-3-A2(Save→Load 往返逐键一致,含复合绑重)· AC-3-A5(落盘面白名单:仅两文件;
//     拒绝清单见测试侧 WriteSurface 扫描)· AC-3-E3⑥(喂入字节 == 保存字节,不解析不重写)
//   GDD input-system.md 规则四(sidecar 两文件布局 + 出厂 API 载荷 + 读序)·
//     规则五(重载前 Disable + RemoveAllBindingOverrides 两步;失配优雅清空与备份归 Story 005)·
//     Edge Cases 二(首次启动 / 写失败只记日志 / 损坏半截视同失配)
//   ADR-011 §一 + Implementation Guidelines 5 + Amendment A ②(两步次序)
//   ADR-010:overrides 刻意不进存档、不进三流(TR-input-003)
//
// 文件布局(规则四定死,头部不得与载荷同文件):
//   bindings.overrides.json —— 出厂 SaveBindingOverridesAsJson() 输出逐字节落盘;
//     读取时逐字节喂回 LoadBindingOverridesFromJson() —— 本类不解析、不重写其载荷。
//   bindings.schema.txt —— 本类自写的头部:format_version · schema_hash · asset_id · asset_version。
//
// 职责边界:
//   · hash 的**计算**归 Story 004(ComputeSchemaHash)—— Save / Load 的 hash 入参都由调用方
//     算好,本类只存、只做序数比对(本故事写头部,不算 hash 语义)。
//   · 失配后的改名备份与恢复归 Story 005(本文件同时实现)—— 失配 ⇒ 陈旧 sidecar 改名备份
//     (原名 + ".bak-" + 三位序号,连续失配不覆盖前次)+ 载入默认 + 日志(旧 hash / 新 hash / 备份路径三要素)。
//   · 不调用 Enable():GDD 规则五尾句「Load → Enable」的 Enable 归调用方(Boot 装配流在
//     Load 返回后启用资产)。本类的 Load 会 Disable() 资产。
//   · P0 无改键 UI(TR-input-014 / OQ-3-1):只交付 Save/Load 契约,补界面不返工。
//   · AC-3-A5:本程序集的写盘点 = 两处 File.WriteAllBytes + 两处失配恢复备份 File.Move
//     (测试侧 WriteSurface 扫描断言:写盘点去重 ∈ {两文件},计数 4,拒绝清单符号一票否决)。

using System;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DaYiJingCheng.Gameplay.Input
{
    /// <summary><see cref="BindingsStore.Load"/> 的四种出口(规则四读序的结果面)。</summary>
    public enum OverridesLoadResult
    {
        /// <summary>两文件皆无 —— 首次启动:资产默认绑定即所载(GDD Edge Cases 二:正常路径,非异常)。</summary>
        NoSidecar,

        /// <summary>头部 hash 匹配,载荷已逐字节喂入出厂 API。</summary>
        Applied,

        /// <summary>失配(头部 hash 不同 / sidecar 半截 / 头部缺 hash 字段)——
        /// 不采用载荷,资产维持默认;陈旧 sidecar 已改名备份 + 日志(Story 005)。</summary>
        Mismatch,

        /// <summary>hash 匹配但载荷被出厂 API 拒收(损坏)—— 视同失配,
        /// 不静默、不部分恢复;陈旧 sidecar 已改名备份 + 日志(Story 005)。</summary>
        CorruptPayload,
    }

    /// <summary>绑重 overrides 的 sidecar 读写(两文件:出厂 JSON 载荷 + 自写头部)。</summary>
    /// <remarks>
    /// 布局与读序由 GDD 规则四定死;重载前两步由 ADR-011 Amendment A ② 定死
    /// (<c>Disable()</c> 单独不移除 override —— override 是叠加语义,漏第二步 =
    /// 旧条目在新载荷上静默残留)。失配恢复(改名备份 + 载入默认 + 日志)由本类实现
    /// (Story 005 —— GDD 规则五 step 4 / Edge Cases 二)。本类是输入层唯一写盘职责
    /// 的实现体(TR-input-003:不进 ADR-010 存档、不进三流)。
    /// </remarks>
    public sealed class BindingsStore
    {
        /// <summary>载荷文件名(AC-3-A5 白名单成员之一)。</summary>
        public const string OverridesFileName = "bindings.overrides.json";

        /// <summary>头部文件名(AC-3-A5 白名单成员之一)。</summary>
        public const string SchemaFileName = "bindings.schema.txt";

        /// <summary>头部 format_version 当前值(布局演化时递增;与 Story 004 的 hash 语义无关)。</summary>
        public const int FormatVersion = 1;

        private const string FormatVersionKey = "format_version";
        private const string SchemaHashKey = "schema_hash";
        private const string AssetIdKey = "asset_id";
        private const string AssetVersionKey = "asset_version";

        private readonly string _directory;

        /// <summary>以 sidecar 所在目录构造(两文件都直接落在该目录下)。</summary>
        /// <param name="directory">sidecar 目录;空串或 null 抛 <see cref="ArgumentException"/>。</param>
        public BindingsStore(string directory)
        {
            if (string.IsNullOrEmpty(directory))
                throw new ArgumentException("sidecar 目录不得为空", nameof(directory));
            _directory = directory;
        }

        /// <summary>载荷文件完整路径(经 <c>Path.Combine</c> 派生;写盘点 1 / 2)。</summary>
        public string OverridesPath => Path.Combine(_directory, OverridesFileName);

        /// <summary>头部文件完整路径(经 <c>Path.Combine</c> 派生;写盘点 2 / 2)。</summary>
        public string SchemaPath => Path.Combine(_directory, SchemaFileName);

        /// <summary>失配恢复的备份扩展名:原名 + 分隔符 + 三位序号(<c>bindings.overrides.json.bak-001</c>)。</summary>
        public const string BackupSuffixFormat = ".bak-{0:D3}";

        /// <summary>失配恢复:陈旧 sidecar 改名备份 + 记日志,原文件从原路径移除(后续 Save 可重建)。</summary>
        /// <remarks>
        /// Story 005 / GDD 规则五 step 4 与 Edge Cases 二:失配时旧文件<b>保留</b>(改名而非删除,
        /// 玩家/开发者可定位)⇒ 载荷与头部各自 <c>File.Move(原名, 备份名)</c> 一次性从原路径移到备份名。
        /// 备份名带三位序号(<c>.bak-001</c>),连续多次失配各占一个序号、互不覆盖。备份目录不可写 /
        /// 移动失败(只读目录、磁盘满):记日志<b>跳过备份</b>,资产仍维持默认、不崩(GDD Edge Cases 二
        /// 「只读目录 / 磁盘满」的失配侧)。顺序:载荷在前、头部在后 —— 任一步失败留下可诊断的线索,
        /// 另一文件仍按原路径被后续 Load 视同半截(安全方向)。此方法不抛异常。
        /// 日志三要素(A8「可诊断」最低口径):旧 hash / 新 hash / 备份文件路径。
        /// </remarks>
        /// <param name="storedHash">头部存储的旧 hash(与 <paramref name="currentHash"/> 失配的一方);
        /// null 表示头部缺失/读不出 —— 日志写旧 hash 为 "(未知)"。</param>
        /// <param name="currentHash">当前资产结构的 hash(Story 004 计算)。</param>
        public void BackupSidecar(string storedHash, string currentHash)
        {
            bool hasPayload = File.Exists(OverridesPath);
            bool hasHeader = File.Exists(SchemaPath);
            if (!hasPayload && !hasHeader)
                return;   // 两文件皆无 = 无陈旧 sidecar 可备份(正常首载,规则五不触发备份)

            string oldHash = storedHash ?? "(未知)";
            bool payloadOk = hasPayload && TryMovePayloadToBackup(oldHash, currentHash);
            bool headerOk = hasHeader && TryMoveHeaderToBackup(oldHash, currentHash);

            if (payloadOk || headerOk)
            {
                string scope = hasPayload && hasHeader ? "overrides 与头部" : (hasPayload ? "overrides" : "头部");
                Debug.LogWarning(
                    $"绑重 sidecar 失配恢复完成:陈旧{scope}已改名备份(旧 hash {oldHash} / 新 hash {currentHash});" +
                    "原路径已移除,当前载入默认绑定。");
            }
        }

        /// <summary>把陈旧载荷文件移到首个空序号备份名;失败记日志、返回 false(不抛)。
        /// Move 首实参直接引用 <see cref="OverridesPath"/>(AC-3-A5 白名单文件,写点扫描可解析)。</summary>
        private bool TryMovePayloadToBackup(string oldHash, string currentHash)
        {
            for (int serial = 1; serial <= 999; serial++)
            {
                string backupPath = OverridesPath + string.Format(BackupSuffixFormat, serial);
                if (File.Exists(backupPath))
                    continue;   // 该序号已被前次失配占用 —— 找下一个(不覆盖前次备份)
                try
                {
                    File.Move(OverridesPath, backupPath);
                    Debug.LogWarning(
                        $"绑重 sidecar 失配:载荷已改名备份(旧 hash {oldHash} / 新 hash {currentHash});" +
                        $"备份路径 {backupPath};当前载入默认绑定。");
                    return true;
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    Debug.LogWarning($"绑重 sidecar 失配:载荷备份失败({ex.Message})—— 跳过备份,仍载入默认,不中断。");
                    return false;
                }
            }
            return false;   // 999 个备份都被占用(极端)⇒ 不覆盖任何前次备份,跳过备份
        }

        /// <summary>同上,针对头部文件(<see cref="SchemaPath"/> 直接作为 Move 首实参)。</summary>
        private bool TryMoveHeaderToBackup(string oldHash, string currentHash)
        {
            for (int serial = 1; serial <= 999; serial++)
            {
                string backupPath = SchemaPath + string.Format(BackupSuffixFormat, serial);
                if (File.Exists(backupPath))
                    continue;
                try
                {
                    File.Move(SchemaPath, backupPath);
                    Debug.LogWarning(
                        $"绑重 sidecar 失配:头部已改名备份(旧 hash {oldHash} / 新 hash {currentHash});" +
                        $"备份路径 {backupPath};当前载入默认绑定。");
                    return true;
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    Debug.LogWarning($"绑重 sidecar 失配:头部备份失败({ex.Message})—— 跳过备份,仍载入默认,不中断。");
                    return false;
                }
            }
            return false;
        }

        /// <summary>把资产当前全部 binding overrides 逐字节写入 sidecar(两文件)。</summary>
        /// <remarks>
        /// 载荷 = 出厂 <c>SaveBindingOverridesAsJson()</c> 的原样输出,UTF-8 无 BOM、
        /// 不附加换行(AC-3-E3⑥ 字节保真;无 overrides 时出厂返回空串 ⇒ 载荷文件 0 字节)。
        /// 写失败(只读目录 / 磁盘满)只记错误日志并返回 null —— 内存态 overrides 继续生效、
        /// 写点不换(GDD Edge Cases 二:不静默失败)。文件顺序:先载荷后头部
        /// (任一步失败留下半截 ⇒ 下次 Load 视同失配,安全方向)。
        /// </remarks>
        /// <param name="asset">宿主动作资产(调用方持有;不为 null)。</param>
        /// <param name="schemaHash">当前资产结构的 schema hash 字符串(Story 004 计算,本类只存)。</param>
        /// <param name="assetId">写入头部的资产标识;null 则取 <c>asset.name</c>。</param>
        /// <param name="assetVersion">写入头部的资产版本;null 则写空串(版本语义归后续故事)。</param>
        /// <returns>实际写盘的载荷字符串;写失败返回 null。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="asset"/> 或 <paramref name="schemaHash"/> 为 null。</exception>
        public string Save(InputActionAsset asset, string schemaHash,
            string assetId = null, string assetVersion = null)
        {
            if (asset == null)
                throw new ArgumentNullException(nameof(asset));
            if (schemaHash == null)
                throw new ArgumentNullException(nameof(schemaHash));

            string payload = asset.SaveBindingOverridesAsJson();
            byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);
            string header = string.Join("\n", new[]
            {
                FormatVersionKey + "=" + FormatVersion,
                SchemaHashKey + "=" + schemaHash,
                AssetIdKey + "=" + (assetId ?? asset.name),
                AssetVersionKey + "=" + (assetVersion ?? string.Empty),
            });
            byte[] headerBytes = Encoding.UTF8.GetBytes(header);

            try
            {
                Directory.CreateDirectory(_directory);
                File.WriteAllBytes(OverridesPath, payloadBytes);   // 写盘点 1 / 2(AC-3-A5 白名单)
                File.WriteAllBytes(SchemaPath, headerBytes);       // 写盘点 2 / 2(AC-3-A5 白名单)
                return payload;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                // GDD Edge Cases 二「只读目录 / 磁盘满」:记日志 + 本次会话内存态仍生效,下次启动回退默认。
                // 只记不抛、不换写点 —— 换写点(如引擎偏好存储)正是 AC-3-A5 拒绝清单要拦的东西。
                Debug.LogError(
                    $"绑重 sidecar 写入失败(只记日志,写点不换;内存态 overrides 继续生效,下次启动回退默认):{ex.Message}");
                return null;
            }
        }

        /// <summary>按规则四读序把 sidecar 载入资产:读头部 → 比对 hash → 匹配才逐字节喂载荷。</summary>
        /// <remarks>
        /// 无条件先执行重载前两步(ADR-011 Amendment A ②):<c>asset.Disable()</c> →
        /// <c>asset.RemoveAllBindingOverrides()</c> —— 单独 <c>Disable()</c> 不清 override,
        /// 漏第二步 = 陈旧条目在新载荷上静默残留。本方法不调用 <c>Enable()</c>
        /// (资产启用归调用方;GDD 规则五尾句的 Enable 在 Load 返回后由装配流执行)。
        /// 任何失配/损坏出口都执行 Story 005 恢复:<b>陈旧 sidecar 改名备份 + 载入默认 + 日志</b>
        /// (见 <see cref="BackupSidecar"/>);匹配出口载荷文件原样不动。
        /// </remarks>
        /// <param name="asset">宿主动作资产(调用方持有;不为 null)。</param>
        /// <param name="currentSchemaHash">当前资产结构的 schema hash(Story 004 计算;本类只做序数比对)。</param>
        /// <returns>读序出口,见 <see cref="OverridesLoadResult"/>。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="asset"/> 或 <paramref name="currentSchemaHash"/> 为 null。</exception>
        public OverridesLoadResult Load(InputActionAsset asset, string currentSchemaHash)
        {
            if (asset == null)
                throw new ArgumentNullException(nameof(asset));
            if (currentSchemaHash == null)
                throw new ArgumentNullException(nameof(currentSchemaHash));

            // 规则五 step 1-2:重载前两步(无条件先清 —— 载荷将重建状态,陈旧条目不得残留)
            asset.Disable();
            asset.RemoveAllBindingOverrides();

            bool hasHeader = File.Exists(SchemaPath);
            bool hasPayload = File.Exists(OverridesPath);
            if (!hasHeader && !hasPayload)
                return OverridesLoadResult.NoSidecar;   // 首次启动:默认绑定已就位,正常路径

            if (!hasHeader || !hasPayload)
            {
                // GDD Edge Cases 二「半截 ⇒ 视同失配」:不尝试部分恢复(半个 sidecar 的语义未定义)。
                Debug.LogWarning(
                    $"绑重 sidecar 半截(头部存在={hasHeader} / 载荷存在={hasPayload})—— 视同失配,不采用,资产维持默认。");
                BackupSidecar(null, currentSchemaHash);   // Story 005:半截文件改名备份(头部缺失 ⇒ 旧 hash 未知)
                return OverridesLoadResult.Mismatch;
            }

            string headerText;
            try
            {
                headerText = Encoding.UTF8.GetString(File.ReadAllBytes(SchemaPath));
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                // 读失败与写失败同族 fail-safe(code review F4):两步已清,读不到 = 状态未定义 ⇒
                // 视同失配,资产维持默认,不把异常抛给调用方。
                Debug.LogWarning($"绑重头部读取失败 —— 视同失配,不采用,资产维持默认:{ex.Message}");
                BackupSidecar(null, currentSchemaHash);
                return OverridesLoadResult.Mismatch;
            }

            string storedHash = ParseHeaderHash(headerText);
            if (storedHash == null)
            {
                Debug.LogWarning("绑重 sidecar 头部缺 schema_hash 字段 —— 视同失配,不采用,资产维持默认。");
                BackupSidecar(null, currentSchemaHash);
                return OverridesLoadResult.Mismatch;
            }

            if (!string.Equals(storedHash, currentSchemaHash, StringComparison.Ordinal))
            {
                Debug.LogWarning(
                    $"绑重 schema hash 失配(头部 {storedHash} ≠ 当前 {currentSchemaHash})—— 不采用载荷," +
                    "资产维持默认;陈旧 sidecar 改名备份(Story 005)。");
                BackupSidecar(storedHash, currentSchemaHash);
                return OverridesLoadResult.Mismatch;
            }

            // 匹配 ⇒ 逐字节喂出厂 API(AC-3-E3⑥):只解码,不解析、不重排、不重写。
            string payload;
            try
            {
                payload = Encoding.UTF8.GetString(File.ReadAllBytes(OverridesPath));
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                // 读失败同上(code review F4):视同失配,不静默、不部分恢复。
                Debug.LogWarning($"绑重载荷读取失败 —— 视同失配,不采用,资产维持默认:{ex.Message}");
                BackupSidecar(storedHash, currentSchemaHash);
                return OverridesLoadResult.Mismatch;
            }
            try
            {
                asset.LoadBindingOverridesFromJson(payload);
                return OverridesLoadResult.Applied;
            }
            catch (Exception ex)
            {
                // 外部文件内容是系统边界:出厂 API 拒收(损坏 JSON)⇒ 视同失配,不静默、不部分恢复。
                Debug.LogError($"绑重载荷被出厂 API 拒收(损坏 → 视同失配,不静默):{ex.Message}");
                BackupSidecar(storedHash, currentSchemaHash);
                return OverridesLoadResult.CorruptPayload;
            }
        }

        /// <summary>从头部文本取 schema_hash 值(逐行 `key=value`,序数比对键名);缺失返回 null。</summary>
        private static string ParseHeaderHash(string headerText)
        {
            string[] lines = headerText.Split('\n');
            foreach (string raw in lines)
            {
                string line = raw.TrimEnd('\r');
                int eq = line.IndexOf('=');
                if (eq <= 0)
                    continue;
                if (string.Equals(line.Substring(0, eq), SchemaHashKey, StringComparison.Ordinal))
                    return line.Substring(eq + 1);
            }
            return null;
        }
    }
}
