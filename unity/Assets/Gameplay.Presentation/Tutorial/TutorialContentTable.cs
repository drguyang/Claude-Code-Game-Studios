// S17-3 · tutorial-001 —— 48 教学内容的**运行期只读表**(cooked 解码面)。
//
// 权威来源:ADR-014 §二/§五(烘焙产物 + 运行期零 JSON 解析器)
//          · TutorialContentCookedWriter(Editor.Tools.Bake)= **本解码器的字节布局唯一真源**
//          · Sim.Contracts/CookedFormat.cs(20 字节头:magic / formatVersion / schemaVersion /
//            configVersion / payloadLength)
//
// ═══ 跨侧字段序契约(BLOCKING)═══════════════════════════════════════════
// ⚠️ **字段序的唯一真源是 `Editor.Tools.Bake.TutorialContentCookedWriter.Write`**。
//    本文件是该布局的**镜像**;改任一侧**必须同改另一侧**,否则解码**静默错位**
//    (读到的是别的字段的字节 —— 不抛、不崩,只是内容全错)。
//
//    载荷布局(全 little-endian;字符串 = i32 字节长 + UTF-8 字节,无终止符;−1 = null):
//      step_count        : i32
//      × { id, title, predicate_kind, note,
//          narration_count: i32 + × str, demonstration_count: i32 + × str,
//          paper_count: i32 + × str }
//      narration_count   : i32
//      × { id, cue_id, order: i32, text }
//      demonstration_count : i32
//      × { id, step_id, actor, clip_key }
//      paper_count       : i32
//      × { id, step_id, title, body, aspect_ratio, illustration_key }
//
//    **行序也是契约的一部分**(写方按「六步闭集序 + 全序复合键」重排)——
//    解码器原样保序,不做二次排序(否则即第二真源)。
//
//    **跨侧一致性的验证方式**:`unity/Assets/Tests/EditMode/Tutorial/
//    tutorial_content_runtime_table_test.cs` 的往返测(同源两次烘焙逐位相同 +
//    `TryDecode` 后行数与 id 序逐个等于 binder 绑定结果)。该测试是**唯一**能发现
//    「只改了一侧」的机械判据 —— 改布局时它必红,不要改测试迁就。
//
// ⚠️ 本件**零跨域依赖**:不引用 `SimEvent` / 任何 gameplay 契约,只吃 `byte[]`。
//    (唯一的 Sim.Contracts 引用是 <c>CookedFormat</c> 的头部常量 —— 头布局的共参照,
//    不构成第二真源。)
// ⚠️ **只读**:无 setter、无 `Add`/`Sort`/`Clear`;集合经 `ReadOnlyCollection` 承载
//    (数组的强转可变路径也被堵死)。承 casebook-002 的反射断言先例。

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Presentation.Tutorial
{
    /// <summary><c>steps[]</c> 的一行(运行期只读形)。</summary>
    public readonly struct TutorialStepRow
    {
        /// <summary>步 id ∈ 六步闭集。</summary>
        public string Id { get; }

        /// <summary>步名(仅文档;48 零 UI)。</summary>
        public string Title { get; }

        /// <summary>判据 Kind 名(或 archive 步的 <c>Checkpoint</c>)。</summary>
        public string PredicateKind { get; }

        /// <summary>实现注记(不渲染)。</summary>
        public string Note { get; }

        /// <summary>口述条目 id 引用。</summary>
        public IReadOnlyList<string> NarrationIds { get; }

        /// <summary>示范条目 id 引用。</summary>
        public IReadOnlyList<string> DemonstrationIds { get; }

        /// <summary>纸条目 id 引用。</summary>
        public IReadOnlyList<string> PaperIds { get; }

        public TutorialStepRow(string id, string title, string predicateKind, string note,
            IReadOnlyList<string> narrationIds, IReadOnlyList<string> demonstrationIds,
            IReadOnlyList<string> paperIds)
        {
            Id = id; Title = title; PredicateKind = predicateKind; Note = note;
            NarrationIds = narrationIds ?? Array.Empty<string>();
            DemonstrationIds = demonstrationIds ?? Array.Empty<string>();
            PaperIds = paperIds ?? Array.Empty<string>();
        }
    }

    /// <summary><c>narration[]</c> 的一行(运行期只读形)。</summary>
    public readonly struct TutorialNarrationRow
    {
        /// <summary>条目 id。</summary>
        public string Id { get; }

        /// <summary>44 语音 cue 键。</summary>
        public string CueId { get; }

        /// <summary>步内次序。</summary>
        public int Order { get; }

        /// <summary>口述文本本体。</summary>
        public string Text { get; }

        public TutorialNarrationRow(string id, string cueId, int order, string text)
        {
            Id = id; CueId = cueId; Order = order; Text = text;
        }
    }

    /// <summary><c>demonstrations[]</c> 的一行(运行期只读形)。</summary>
    public readonly struct TutorialDemonstrationRow
    {
        /// <summary>条目 id。</summary>
        public string Id { get; }

        /// <summary>所属步 id。</summary>
        public string StepId { get; }

        /// <summary>示范者(闭集 {mentor})。</summary>
        public string Actor { get; }

        /// <summary>示范 clip 键。</summary>
        public string ClipKey { get; }

        public TutorialDemonstrationRow(string id, string stepId, string actor, string clipKey)
        {
            Id = id; StepId = stepId; Actor = actor; ClipKey = clipKey;
        }
    }

    /// <summary><c>papers[]</c> 的一行(运行期只读形)。</summary>
    public readonly struct TutorialPaperRow
    {
        /// <summary>条目 id(6 侧布置引用它 ⇒ 跨系统稳定键)。</summary>
        public string Id { get; }

        /// <summary>所属步 id。</summary>
        public string StepId { get; }

        /// <summary>纸名 / 图题。</summary>
        public string Title { get; }

        /// <summary>一句话说明。</summary>
        public string Body { get; }

        /// <summary>出版纵横比(W:H)。</summary>
        public string AspectRatio { get; }

        /// <summary>插图素材键。</summary>
        public string IllustrationKey { get; }

        public TutorialPaperRow(string id, string stepId, string title, string body,
            string aspectRatio, string illustrationKey)
        {
            Id = id; StepId = stepId; Title = title; Body = body;
            AspectRatio = aspectRatio; IllustrationKey = illustrationKey;
        }
    }

    /// <summary>48 教学内容的运行期只读表(<c>48_tutorial_content.cooked.bytes</c> 的解码结果)。
    /// <para>⚠️ 布局真源 = <c>TutorialContentCookedWriter</c>(见文件头契约段)。</para>
    /// <example><code>if (!TutorialContentTable.TryDecode(bytes, out var table, out string error))
    ///     throw new InvalidOperationException(error);
    /// string firstStep = table.Steps[0].Id;</code></example>
    /// </summary>
    public readonly struct TutorialContentTable
    {
        /// <summary>cooked 产物文件名(= Addressables address;= 菜单写入的文件名)。</summary>
        public const string CookedFileName = "48_tutorial_content.cooked.bytes";

        private static readonly ReadOnlyCollection<TutorialStepRow> NoSteps =
            Array.AsReadOnly(Array.Empty<TutorialStepRow>());
        private static readonly ReadOnlyCollection<TutorialNarrationRow> NoNarration =
            Array.AsReadOnly(Array.Empty<TutorialNarrationRow>());
        private static readonly ReadOnlyCollection<TutorialDemonstrationRow> NoDemonstrations =
            Array.AsReadOnly(Array.Empty<TutorialDemonstrationRow>());
        private static readonly ReadOnlyCollection<TutorialPaperRow> NoPapers =
            Array.AsReadOnly(Array.Empty<TutorialPaperRow>());

        private readonly ReadOnlyCollection<TutorialStepRow> _steps;
        private readonly ReadOnlyCollection<TutorialNarrationRow> _narration;
        private readonly ReadOnlyCollection<TutorialDemonstrationRow> _demonstrations;
        private readonly ReadOnlyCollection<TutorialPaperRow> _papers;
        private readonly uint _schemaVersion;
        private readonly uint _configVersion;

        private TutorialContentTable(
            ReadOnlyCollection<TutorialStepRow> steps, ReadOnlyCollection<TutorialNarrationRow> narration,
            ReadOnlyCollection<TutorialDemonstrationRow> demonstrations, ReadOnlyCollection<TutorialPaperRow> papers,
            uint schemaVersion, uint configVersion)
        {
            _steps = steps; _narration = narration; _demonstrations = demonstrations;
            _papers = papers; _schemaVersion = schemaVersion; _configVersion = configVersion;
        }

        /// <summary>六步(写方行序 = 六步闭集序;解码不重排)。</summary>
        public IReadOnlyList<TutorialStepRow> Steps => (IReadOnlyList<TutorialStepRow>)_steps ?? NoSteps;

        /// <summary>口述条目(写方行序)。</summary>
        public IReadOnlyList<TutorialNarrationRow> Narration =>
            (IReadOnlyList<TutorialNarrationRow>)_narration ?? NoNarration;

        /// <summary>示范条目(写方行序)。</summary>
        public IReadOnlyList<TutorialDemonstrationRow> Demonstrations =>
            (IReadOnlyList<TutorialDemonstrationRow>)_demonstrations ?? NoDemonstrations;

        /// <summary>纸条目(写方行序)。</summary>
        public IReadOnlyList<TutorialPaperRow> Papers =>
            (IReadOnlyList<TutorialPaperRow>)_papers ?? NoPapers;

        /// <summary>头部 schemaVersion(offset 8)。</summary>
        public uint SchemaVersion => _schemaVersion;

        /// <summary>头部 ConfigVersion(offset 12;ADR-010 §七 存档头比对用,不匹配非致命)。</summary>
        public uint ConfigVersion => _configVersion;

        /// <summary>解码 cooked 字节。
        /// <para>失败语义:**返回 false + 非空 error,不抛未捕获异常**(调用方
        /// <c>AddressablesDataProvider</c> 负责包成 E-13 启动期硬失败)。</para></summary>
        /// <param name="bytes">Addressables <c>TextAsset.bytes</c>(含 20 字节头)。</param>
        /// <param name="table">成功时为表;失败时为 <c>default</c>(空表)。</param>
        /// <param name="error">失败原因(人读,含偏移)。</param>
        public static bool TryDecode(byte[] bytes, out TutorialContentTable table, out string error)
        {
            table = default;
            error = null;

            if (bytes == null) { error = "[48] cooked 字节为 null"; return false; }
            if (bytes.Length < CookedFormat.HeaderSize)
            {
                error = $"[48] cooked 字节 {bytes.Length} B 短于头部 {CookedFormat.HeaderSize} B";
                return false;
            }
            if (bytes[0] != (byte)'D' || bytes[1] != (byte)'Y' || bytes[2] != (byte)'J' || bytes[3] != (byte)'C')
            {
                error = "[48] 魔数不匹配(期望 DYJC)—— 不是本格式的产物";
                return false;
            }

            uint formatVersion = ReadU32Le(bytes, 4);
            if (formatVersion > CookedFormat.FormatVersion)
            {
                error = $"[48] formatVersion={formatVersion} > 支持上限 {CookedFormat.FormatVersion}" +
                        "(读方过旧 ⇒ 拒收硬失败)";
                return false;
            }
            uint schemaVersion = ReadU32Le(bytes, 8);
            uint configVersion = ReadU32Le(bytes, 12);
            uint payloadLength = ReadU32Le(bytes, 16);

            if (payloadLength != (uint)(bytes.Length - CookedFormat.HeaderSize))
            {
                error = $"[48] 头部 payloadLength={payloadLength} ≠ 实际 " +
                        $"{bytes.Length - CookedFormat.HeaderSize}(截断或拼接损坏)";
                return false;
            }

            try
            {
                using (var payload = new MemoryStream(bytes, CookedFormat.HeaderSize,
                                                     (int)payloadLength, writable: false))
                using (var r = new BinaryReader(payload, Encoding.UTF8))
                {
                    var steps = ReadSteps(r);
                    var narration = ReadNarration(r);
                    var demonstrations = ReadDemonstrations(r);
                    var papers = ReadPapers(r);

                    // 载荷必须**恰好读完**:多出的字节 = 有一侧多写了字段(布局漂移的机械兜现)。
                    if (payload.Position != payload.Length)
                    {
                        error = $"[48] 载荷未恰好读完(停在 {payload.Position} / {payload.Length})" +
                                "—— 两侧字段序可能不一致";
                        return false;
                    }

                    table = new TutorialContentTable(steps, narration, demonstrations, papers,
                                                     schemaVersion, configVersion);
                    return true;
                }
            }
            catch (Exception ex) when (ex is EndOfStreamException || ex is IOException ||
                                       ex is ArgumentException || ex is FormatException ||
                                       ex is OverflowException)
            {
                table = default;
                error = $"[48] 载荷解码失败(偏移附近的字节与布局不符):{ex.GetType().Name}: {ex.Message}";
                return false;
            }
        }

        // ─── 逐段解码(严格镜像 TutorialContentCookedWriter 的写入序)──────────

        private static ReadOnlyCollection<TutorialStepRow> ReadSteps(BinaryReader r)
        {
            int count = ReadCount(r, "steps", minBytesPerRow: 7 * 4);
            var rows = new TutorialStepRow[count];
            for (int i = 0; i < count; i++)
            {
                rows[i] = new TutorialStepRow(
                    ReadString(r), ReadString(r), ReadString(r), ReadString(r),
                    ReadStringList(r), ReadStringList(r), ReadStringList(r));
            }
            return Array.AsReadOnly(rows);
        }

        private static ReadOnlyCollection<TutorialNarrationRow> ReadNarration(BinaryReader r)
        {
            int count = ReadCount(r, "narration", minBytesPerRow: 3 * 4 + 4);
            var rows = new TutorialNarrationRow[count];
            for (int i = 0; i < count; i++)
            {
                string id = ReadString(r);
                string cueId = ReadString(r);
                int order = r.ReadInt32();
                rows[i] = new TutorialNarrationRow(id, cueId, order, ReadString(r));
            }
            return Array.AsReadOnly(rows);
        }

        private static ReadOnlyCollection<TutorialDemonstrationRow> ReadDemonstrations(BinaryReader r)
        {
            int count = ReadCount(r, "demonstrations", minBytesPerRow: 4 * 4);
            var rows = new TutorialDemonstrationRow[count];
            for (int i = 0; i < count; i++)
                rows[i] = new TutorialDemonstrationRow(
                    ReadString(r), ReadString(r), ReadString(r), ReadString(r));
            return Array.AsReadOnly(rows);
        }

        private static ReadOnlyCollection<TutorialPaperRow> ReadPapers(BinaryReader r)
        {
            int count = ReadCount(r, "papers", minBytesPerRow: 6 * 4);
            var rows = new TutorialPaperRow[count];
            for (int i = 0; i < count; i++)
            {
                rows[i] = new TutorialPaperRow(
                    ReadString(r), ReadString(r), ReadString(r),
                    ReadString(r), ReadString(r), ReadString(r));
            }
            return Array.AsReadOnly(rows);
        }

        /// <summary>读段计数并做**结构性上界校验**(负数 / 超出剩余字节 ⇒ 抛 ⇒ TryDecode 转 false)。</summary>
        private static int ReadCount(BinaryReader r, string section, int minBytesPerRow)
        {
            int count = r.ReadInt32();
            if (count < 0)
                throw new FormatException($"{section} 计数 = {count}(负数 ⇒ 布局错位)");

            long remaining = r.BaseStream.Length - r.BaseStream.Position;
            if (minBytesPerRow > 0 && (long)count * minBytesPerRow > remaining)
                throw new FormatException(
                    $"{section} 计数 = {count} 超出剩余 {remaining} B(按每行最少 {minBytesPerRow} B 估算)");
            return count;
        }

        /// <summary>读字符串:i32 字节长 + UTF-8;−1 = null(镜像写方的 null 编码)。</summary>
        private static string ReadString(BinaryReader r)
        {
            int length = r.ReadInt32();
            if (length == -1) return null;
            if (length < 0) throw new FormatException($"字符串长度 = {length}(非法;只允许 −1 或 ≥0)");
            long remaining = r.BaseStream.Length - r.BaseStream.Position;
            if (length > remaining) throw new EndOfStreamException($"字符串长度 {length} > 剩余 {remaining} B");
            if (length == 0) return string.Empty;

            byte[] buffer = r.ReadBytes(length);
            if (buffer.Length != length) throw new EndOfStreamException("字符串字节不足");
            return Encoding.UTF8.GetString(buffer);
        }

        /// <summary>读字符串数组:i32 计数(−1 = null)+ 逐项字符串。</summary>
        private static ReadOnlyCollection<string> ReadStringList(BinaryReader r)
        {
            int count = r.ReadInt32();
            if (count == -1) return Array.AsReadOnly(Array.Empty<string>());
            if (count < 0) throw new FormatException($"字符串数组计数 = {count}(非法)");

            long remaining = r.BaseStream.Length - r.BaseStream.Position;
            if ((long)count * 4 > remaining)
                throw new EndOfStreamException($"字符串数组计数 {count} 超出剩余 {remaining} B");

            var items = new string[count];
            for (int i = 0; i < count; i++) items[i] = ReadString(r);
            return Array.AsReadOnly(items);
        }

        private static uint ReadU32Le(byte[] buffer, int offset)
            => (uint)(buffer[offset] | (buffer[offset + 1] << 8) |
                      (buffer[offset + 2] << 16) | (buffer[offset + 3] << 24));
    }
}
