// S14-6 补口轮 · story-010 —— 音频素材 **Import Load Type 门**(OQ-44-2 连带裁定的执行体)。
//
// 权威来源:
//   GDD design/gdd/audio-system.md §OQ-44-2 附(`:1208-1247`,2026-10-17 用户裁定,含可证伪判据)
//     · OQ-44-2 结案时**连带裁定两项**:素材量(每组合 ≥2)+ **Import Load Type 定档**
//     · 档位判据三条(§附 表):DecompressOnLoad(0) / CompressedInMemory(1) / Streaming(2)
//   docs/engine-reference/unity/modules/audio.md:213-222(语义三档;**无秒数 / 字节阈值** —— 本门补阈值)
//   ADR-014 §三(阶段 2 per-schema 绑定 · 校验聚合)
//
// ⚠️ **为什么需要本件(而不是让判据停在 GDD 的一行 `grep` 里)**
//   §OQ-44-2 附 的「验证命令」是一对 `python3 -c …` + `grep -o 'loadType: [0-9]*'` ——
//   那是**人肉复核**,不是**可执行判据**:档位一旦漂移,没有任何东西转红。
//   本件把该判据升为**可跑的门**:从 **wav 自己的头**算时长与解码后驻留,再与 `.meta` 的
//   `loadType` 逐文件比对。⇒ 后续 12 个 `vo_` 变体素材落地时**自动纳入**,零改门。
//
// ⚠️ **`music_*` 的口径张力(承 §附 的登记,不隐藏)**
//   按**数值判据**,`music_encounter_layer_loop.wav`(23.3160 s / 2008 KB)落在
//   「中档 ∩ 大档」的重叠区(512 KB–4 MB);GDD 取 **Streaming**,理由 = `audio.md:217`
//   的语义档点名「大片段(**music**)」+ 乐层 `loop: true` 长驻。
//   ⇒ 本件把它编码为**具名前缀规则**(<see cref="MusicPrefix"/>),而非把阈值改成「> 512 KB 即 Streaming」——
//   后者会**连带改判** `sfx_breath_*`(168 KB,不受影响)之外的一切,且与 §附 表格不符。
//   **若后续 Music 素材改短(如 < 5 s 的 stinger),须重裁 GDD §附 那一行,再改本门。**

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>音频 Import Load Type 门(OQ-44-2 连带裁定;判据 = GDD §OQ-44-2 附)。</summary>
    public static class AudioImportSettingsGate
    {
        /// <summary>小档上界:解码后驻留 ≤ 该值**且**时长 &lt; <see cref="ShortSeconds"/> ⇒ <c>DecompressOnLoad</c>。</summary>
        public const long SmallResidentBytes = 512 * 1024;

        /// <summary>大档下界:解码后驻留 &gt; 该值 **或** 时长 ≥ <see cref="LongSeconds"/> ⇒ <c>Streaming</c>。</summary>
        public const long LargeResidentBytes = 4 * 1024 * 1024;

        /// <summary>短片段上界(秒)。</summary>
        public const double ShortSeconds = 5.0;

        /// <summary>长片段下界(秒)。</summary>
        public const double LongSeconds = 30.0;

        /// <summary>Music 总线素材的具名前缀(§附 的语义档覆盖;见文件头「口径张力」)。</summary>
        public const string MusicPrefix = "music_";

        /// <summary>WAV 头探测窗(字节)。`fmt` / `data` 两个块在本项目全部素材里都落在此窗内;
        /// 超出即报「data 块缺失」而**不**静默猜(承本件「读不到 ⇒ 报违例」纪律)。</summary>
        public const int HeaderProbeBytes = 64 * 1024;

        /// <summary>`UnityEngine.AudioClipLoadType` 的枚举值(真值来源 = `UnityEngine.AudioModule.dll` 反汇编,
        /// 承 GDD §附「枚举真值来源」—— **非记忆**)。</summary>
        public const int DecompressOnLoad = 0;
        /// <inheritdoc cref="DecompressOnLoad"/>
        public const int CompressedInMemory = 1;
        /// <inheritdoc cref="DecompressOnLoad"/>
        public const int Streaming = 2;

        // ══════════════════════════════════════════════════════════════════════
        // 纯谓词半边(夹具可注入;不触文件系统)
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>由「文件名 + 时长 + 解码后驻留」求**应然**档位。纯函数,零 I/O。</summary>
        /// <param name="fileName">bare 文件名(只用于 <see cref="MusicPrefix"/> 语义档判定)。</param>
        /// <param name="durationSeconds">时长(秒)。</param>
        /// <param name="decodedBytes">解码后驻留字节 = 帧数 × 通道数 × (位深/8)。</param>
        /// <returns><see cref="DecompressOnLoad"/> / <see cref="CompressedInMemory"/> / <see cref="Streaming"/>。</returns>
        public static int RequiredLoadType(string fileName, double durationSeconds, long decodedBytes)
        {
            // ① 语义档覆盖(§附:audio.md:217 点名「大片段(music)」+ loop 长驻)——
            //    须排在数值判据**之前**,否则 23.3 s / 2008 KB 会落进中档。
            if (!string.IsNullOrEmpty(fileName) &&
                fileName.StartsWith(MusicPrefix, StringComparison.Ordinal))
                return Streaming;

            // ② 大档
            if (decodedBytes > LargeResidentBytes || durationSeconds >= LongSeconds)
                return Streaming;

            // ③ 小档(两条须**同时**满足 —— 承 §附「须同时满足才升档」)
            if (decodedBytes <= SmallResidentBytes && durationSeconds < ShortSeconds)
                return DecompressOnLoad;

            // ④ 中档
            return CompressedInMemory;
        }

        /// <summary>档位枚举值 → GDD 用语(错误报文用;未知值原样回显)。</summary>
        public static string DescribeLoadType(int loadType)
        {
            switch (loadType)
            {
                case DecompressOnLoad: return "DecompressOnLoad(0)";
                case CompressedInMemory: return "CompressedInMemory(1)";
                case Streaming: return "Streaming(2)";
                default: return "未知档位(" + loadType.ToString(CultureInfo.InvariantCulture) + ")";
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // 真环境半边
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>解析素材根(复用 <see cref="AudioAssetExistence.ResolveRoot"/> —— **单一出处**,禁另抄)。</summary>
        public static string ResolveRoot(string repoRoot) => AudioAssetExistence.ResolveRoot(repoRoot);

        /// <summary>读 WAV 头求时长与解码后驻留。**只读文件前 64 KB,不读采样数据**。</summary>
        /// <remarks>⚠️ <b>为什么是「读进 byte[] 手工走索引」而不是 `BinaryReader` + `FileStream.Position`</b>
        /// (2026-10-17 实测踩过,不是预防性设计):`BinaryReader` **自带读前缓冲**,
        /// 读几个字节就会把底层流推远 ⇒ `FileStream.Position` **不等于**逻辑读位,
        /// 用它算 chunk 偏移会**全部错位**。本机首跑症状 = 5 个文件**全部**
        /// 报「data 块缺失」(而文件明明是合法 RIFF)。
        /// <para>⛔ 也**不用** `BinaryReader` + `MemoryStream`(同样的缓冲语义,同样不可依赖 `Position`)。
        /// 索引直读是唯一无歧义的形态。字节序按小端(`BitConverter` 的宿主序;
        /// 本项目全部目标平台为小端,承 ADR-005 的平台面)。</para></remarks>
        /// <param name="wavPath">wav 绝对路径。</param>
        /// <param name="durationSeconds">出参:时长(秒);失败为 0。</param>
        /// <param name="decodedBytes">出参:帧数 × 通道数 × (位深/8);失败为 0。</param>
        /// <param name="error">出参:失败原因(成功为 null)。</param>
        /// <returns>成功 true。</returns>
        public static bool TryReadWaveInfo(
            string wavPath, out double durationSeconds, out long decodedBytes, out string error)
        {
            durationSeconds = 0;
            decodedBytes = 0;
            error = null;

            if (!File.Exists(wavPath)) { error = "文件不存在"; return false; }

            try
            {
                long fileLength;
                byte[] head;

                using (var s = new FileStream(wavPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    fileLength = s.Length;
                    if (fileLength < 44) { error = "文件短于最小 RIFF/WAVE 头(44 字节)"; return false; }

                    int take = (int)Math.Min(fileLength, HeaderProbeBytes);
                    head = new byte[take];
                    int read = 0;
                    while (read < take)
                    {
                        int n = s.Read(head, read, take - read);
                        if (n <= 0) break;
                        read += n;
                    }
                    if (read < take) Array.Resize(ref head, read);
                }

                if (Ascii(head, 0) != "RIFF") { error = "缺 RIFF 魔数"; return false; }
                if (Ascii(head, 8) != "WAVE") { error = "缺 WAVE 标识"; return false; }

                int channels = 0, bitsPerSample = 0, sampleRate = 0;
                long dataBytes = -1;

                int p = 12;                                       // 跳过 RIFF/WAVE 头
                while (p + 8 <= head.Length)
                {
                    string id = Ascii(head, p);
                    uint size = BitConverter.ToUInt32(head, p + 4);
                    int body = p + 8;

                    if (id == "fmt ")
                    {
                        if (body + 16 > head.Length) { error = "fmt 块被截断"; return false; }
                        channels = BitConverter.ToUInt16(head, body + 2);
                        sampleRate = (int)BitConverter.ToUInt32(head, body + 4);
                        bitsPerSample = BitConverter.ToUInt16(head, body + 14);
                    }
                    else if (id == "data")
                    {
                        // 有些写入器把 size 写错(0 / 0xFFFFFFFF)或写得比文件长 ⇒ 取「实际剩余」为准。
                        long remain = fileLength - body;
                        dataBytes = (size == 0 || size == uint.MaxValue || size > remain) ? remain : size;
                        break;
                    }

                    long next = (long)body + size + (size % 2);    // chunk 按偶字节对齐
                    if (next <= p || next > head.Length) break;    // 走不动 / 越过探测窗 ⇒ 停
                    p = (int)next;
                }

                if (channels <= 0 || sampleRate <= 0 || bitsPerSample <= 0)
                { error = "fmt 块缺失或字段非法"; return false; }
                if (dataBytes < 0) { error = "data 块缺失(前 " + HeaderProbeBytes + " 字节内未找到)" ; return false; }

                int blockAlign = channels * (bitsPerSample / 8);
                if (blockAlign <= 0) { error = "blockAlign 非法"; return false; }

                long frames = dataBytes / blockAlign;
                durationSeconds = (double)frames / sampleRate;
                decodedBytes = frames * blockAlign;                // = 帧数 × 通道数 × (位深/8)
                return true;
            }
            catch (Exception ex)
            {
                error = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        /// <summary>ASCII 取 4 字节 chunk id(越界回空串 —— 由调用方的长度检查兜底)。</summary>
        private static string Ascii(byte[] buf, int offset)
        {
            if (offset < 0 || offset + 4 > buf.Length) return string.Empty;
            return new string(new[] { (char)buf[offset], (char)buf[offset + 1], (char)buf[offset + 2], (char)buf[offset + 3] });
        }

        /// <summary>从 `.meta` 读 <c>defaultSettings.loadType</c>(**第一个** `loadType:` 行 ——
        /// `platformSettingOverrides` 内的逐平台覆盖不在本门判据面内)。</summary>
        /// <param name="metaPath">`.meta` 绝对路径。</param>
        /// <param name="loadType">出参:档位;失败为 -1。</param>
        /// <param name="error">出参:失败原因(成功为 null)。</param>
        /// <returns>成功 true。</returns>
        public static bool TryReadLoadType(string metaPath, out int loadType, out string error)
        {
            loadType = -1;
            error = null;
            if (!File.Exists(metaPath)) { error = "meta 不存在"; return false; }

            foreach (string raw in File.ReadAllLines(metaPath))
            {
                string line = raw.Trim();
                if (!line.StartsWith("loadType:", StringComparison.Ordinal)) continue;

                string value = line.Substring("loadType:".Length).Trim();
                if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out loadType))
                { error = "loadType 非整数:" + value; return false; }
                return true;
            }

            error = "meta 内无 loadType 行";
            return false;
        }

        /// <summary>文件名是否为本门判据面内的 wav —— **纯谓词,零 I/O**。
        /// <para>⚠️ <b>本方法自 2026-10-17(story-017)起只是委派</b>:判据真源已上移到
        /// <c>DaYiJingCheng.EditorTools.Gates.AudioAssetGroupGates.IsAudioAssetName</c>。</para>
        /// <remarks>⚠️ <b>为什么单拆一条谓词而不是直接 `Directory.GetFiles(dir, "*.wav")`</b>
        /// (2026-10-17 实测,不是预防性设计):**该 pattern 在 Linux 上区分大小写**。
        /// 实测(net8.0 · Linux):目录含 `lower.wav` / `UPPER.WAV` / `Mixed.Wav` 时,
        /// `GetFiles(dir, "*.wav")` **只返回 `lower.wav`** —— 另两个被静默漏扫。
        /// 而 CI 平台正是 Linux(ADR-012)⇒ 素材批若交付 `.WAV`,本门会**少报违例**。
        /// <para>修法 = 枚举 `"*"` 后用本谓词按 OrdinalIgnoreCase 过滤
        /// (⛔ **不得**改成 `"*.WAV"` —— 那只是把分叉挪到另一边)。
        /// **拆成谓词的另一收益**:夹具可注入文件名直断大小写三形态,**不必碰文件系统**
        /// (承本文件「夹具零文件写入」纪律,见 `test_discipline_noFilesystemWrites`)。</para>
        /// <para>⚠️ <b>与 story-017 的 W-2 是同一缺陷的两处</b>:`AudioAssetGroupGates`
        /// (`AudioAssetsOnDisk`)同型 —— 由 story-017 的 **AC-44-D5b** 修。**为什么真源搬到 `Gates`**:
        /// 装配引用是 `Bake → Gates` **单向**(`Editor.Tools.Bake.asmdef`),`Gates` 够不着 `Bake`;
        /// 两处各留一份判据 = 「另抄一份」,与本仓「单一出处」纪律冲突 ⇒ 由本方法委派到下层真源。
        /// **两处仍不合并记账**(不同文件、不同 AC 面),但**判据只有一条实现**。</para></remarks>
        /// <param name="fileName">bare 文件名或路径(只取其扩展名)。</param>
        /// <returns>扩展名等于 <c>.wav</c>(忽略大小写)⇒ true。</returns>
        public static bool IsWavName(string fileName)
            => DaYiJingCheng.EditorTools.Gates.AudioAssetGroupGates.IsAudioAssetName(fileName);

        /// <summary>扫描素材根下全部 wav(大小写不敏感),逐条比对**应然档位**与 `.meta` 现值。</summary>
        /// <param name="audioDir">素材根绝对路径(见 <see cref="ResolveRoot"/>)。</param>
        /// <returns>违例报文列表(**保序**:按文件名 Ordinal 排序);全合规 = 空列表。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="audioDir"/> 为 null。</exception>
        public static IReadOnlyList<string> Validate(string audioDir)
        {
            if (audioDir == null) throw new ArgumentNullException(nameof(audioDir));

            var violations = new List<string>();
            if (!Directory.Exists(audioDir))
            {
                violations.Add("[根不存在] " + audioDir);
                return violations;
            }

            // ⚠️ 取 "*" 再按 IsWavName 过滤 —— **不得**用 "*.wav"(Linux 上区分大小写,见 IsWavName 注释)。
            var wavs = new List<string>();
            foreach (string p in Directory.GetFiles(audioDir, "*", SearchOption.TopDirectoryOnly))
                if (IsWavName(p)) wavs.Add(p);
            wavs.Sort(StringComparer.Ordinal);                        // 确定性输出

            foreach (string wav in wavs)
            {
                string name = Path.GetFileName(wav);

                if (!TryReadWaveInfo(wav, out double dur, out long decoded, out string wavErr))
                {
                    violations.Add($"[wav 头不可读] {name} —— {wavErr}");
                    continue;
                }

                int required = RequiredLoadType(name, dur, decoded);

                if (!TryReadLoadType(wav + ".meta", out int actual, out string metaErr))
                {
                    violations.Add($"[meta 不可读] {name}.meta —— {metaErr}");
                    continue;
                }

                if (actual != required)
                {
                    violations.Add(
                        $"[档位不符] {name} —— 应 {DescribeLoadType(required)},实 {DescribeLoadType(actual)}" +
                        $"({dur.ToString("F4", CultureInfo.InvariantCulture)} s / " +
                        $"{(decoded / 1024.0).ToString("F1", CultureInfo.InvariantCulture)} KB)");
                }
            }

            return violations;
        }
    }
}
