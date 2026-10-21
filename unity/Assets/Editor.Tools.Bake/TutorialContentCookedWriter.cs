// S17-3 · story-001 —— 48 教学内容的 deterministic cooked 写入器(阶段 2 烘焙的写面)。
//
// 权威来源:ADR-014 §二(同源两次烘焙逐位一致:固定字段顺序 · 零字典迭代 · 零本地时间)
//          · Sim.Contracts/CookedFormat.cs(20 字节头布局的唯一共参照)
//          · TutorialContentBinder.BindResult(内容来源)
//
// ⚠️ 载荷布局(全部 little-endian;固定字段顺序;字符串 = i32 字节长 + UTF-8 字节,无终止符):
//    step_count        : i32
//    step_count × { id: str, title: str, predicate_kind: str, note: str,
//                   narration_count: i32, × str,
//                   demonstration_count: i32, × str,
//                   paper_count: i32, × str }
//    narration_count   : i32
//    narration_count × { id: str, cue_id: str, order: i32, text: str }
//    demonstration_count : i32
//    demonstration_count × { id: str, step_id: str, actor: str, clip_key: str }
//    paper_count       : i32
//    paper_count × { id: str, step_id: str, title: str, body: str, aspect_ratio: str, illustration_key: str }
//
// ⚠️ **行序 = 全序,唯一**:
//   · steps            → 按 **schema 六步闭集序**(TutorialContentSchema.StepIds 的下标;
//                        不用源文件序 —— 作者调整段落顺序不该改字节流)
//   · narration        → (所属 step 的闭集序, order, id 序数)
//   · demonstrations   → (所属 step 的闭集序, id 序数)
//   · papers           → (所属 step 的闭集序, id 序数)
//   2026-10-15 story-002 返工的教训照搬:**禁 `List.Sort` 单键不稳定排序**——此处给的都是
//   全序复合键(末位 id 序数在数据内唯一,构建期已由 Binder 保证不重复)⇒ 同源两次烘焙字节必同。
//
// ⚠️ **schemaVersion 只住头槽**(offset 8,单一出处);载荷**无** schema 字段
//   (承 DelayTableCookedWriter 的 R-1 教训:同一真值两处存 = 第二真源)。
//
// ⚠️ 本件**零 float/double**(48 零数值 —— GDD §Formulas 首行),故零 Fix 编码、零舍入。

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>48 教学内容的 deterministic cooked 写入器。</summary>
    internal static class TutorialContentCookedWriter
    {
        /// <summary>编码教学内容(含 20 字节头)。</summary>
        /// <exception cref="InvalidOperationException">步集为空(Binder 的闭集校验下不可达;
        /// 空步集落盘 = 静默产出「永不触发任何教学」的产物,fail-loud)。</exception>
        public static byte[] Write(in TutorialContentBinder.BindResult bound, uint configVersion)
        {
            if (bound.Steps == null || bound.Steps.Count == 0)
                throw new InvalidOperationException(
                    "教学内容步集为空 ⇒ 拒绝落盘(Binder 的六步闭集校验应已拦;空集 = 教学永不触发)");

            var steps = OrderSteps(bound.Steps);
            var narration = OrderNarration(bound.Narration, steps);
            var demos = OrderDemonstrations(bound.Demonstrations);
            var papers = OrderPapers(bound.Papers);

            using (var payload = new MemoryStream())
            using (var w = new BinaryWriter(payload, Encoding.UTF8))
            {
                WriteSteps(w, steps);
                WriteNarration(w, narration);
                WriteDemonstrations(w, demos);
                WritePapers(w, papers);

                w.Flush();
                return FinishHeader(payload.ToArray(), bound.SchemaVersion, configVersion);
            }
        }

        // ─── 排序(全序,禁依赖源序 / 字典迭代序)──────────────────────────

        private static List<TutorialStep> OrderSteps(IReadOnlyList<TutorialStep> source)
        {
            var list = new List<TutorialStep>(source);
            list.Sort((a, b) =>
            {
                int cmp = StepOrdinal(a.Id).CompareTo(StepOrdinal(b.Id));
                if (cmp != 0) return cmp;
                return string.CompareOrdinal(a.Id, b.Id);   // 防御破平(闭集外 id 由 Binder 拦)
            });
            return list;
        }

        private static List<TutorialNarration> OrderNarration(
            IReadOnlyList<TutorialNarration> source, List<TutorialStep> steps)
        {
            var list = new List<TutorialNarration>(source ?? Array.Empty<TutorialNarration>());
            list.Sort((a, b) =>
            {
                int cmp = StepOrdinalOfEntry(a.Id, steps).CompareTo(StepOrdinalOfEntry(b.Id, steps));
                if (cmp != 0) return cmp;
                cmp = a.Order.CompareTo(b.Order);
                if (cmp != 0) return cmp;
                return string.CompareOrdinal(a.Id, b.Id);
            });
            return list;
        }

        /// <summary>示范行按 (step 闭集序, id ordinal) 排全序。</summary>
        /// <remarks>🔁 2026-10-21（评审 E1）：删去未被使用的 <c>steps</c> 形参 —— 排序键只取
        /// <c>StepId</c> 的**闭集下标**（<see cref="StepOrdinal"/>），与 <c>steps</c> 内容无关；
        /// 保留一个不读的形参只会让人以为「排序依赖 steps 列表」。</remarks>
        private static List<TutorialDemonstration> OrderDemonstrations(
            IReadOnlyList<TutorialDemonstration> source)
        {
            var list = new List<TutorialDemonstration>(source ?? Array.Empty<TutorialDemonstration>());
            list.Sort((a, b) =>
            {
                int cmp = StepOrdinal(a.StepId).CompareTo(StepOrdinal(b.StepId));
                if (cmp != 0) return cmp;
                return string.CompareOrdinal(a.Id, b.Id);
            });
            return list;
        }

        /// <summary>纸行按 (step 闭集序, id ordinal) 排全序。</summary>
        /// <remarks>🔁 2026-10-21（评审 E1）：同 <see cref="OrderDemonstrations"/>，删去未使用的 <c>steps</c> 形参。</remarks>
        private static List<TutorialPaper> OrderPapers(
            IReadOnlyList<TutorialPaper> source)
        {
            var list = new List<TutorialPaper>(source ?? Array.Empty<TutorialPaper>());
            list.Sort((a, b) =>
            {
                int cmp = StepOrdinal(a.StepId).CompareTo(StepOrdinal(b.StepId));
                if (cmp != 0) return cmp;
                return string.CompareOrdinal(a.Id, b.Id);
            });
            return list;
        }

        /// <summary>step id → 闭集下标(闭集外 id 排到末尾 —— Binder 已拦,此处只保证全序)。</summary>
        private static int StepOrdinal(string stepId)
        {
            for (int i = 0; i < TutorialContentSchema.StepIds.Count; i++)
                if (string.Equals(TutorialContentSchema.StepIds[i], stepId, StringComparison.Ordinal))
                    return i;
            return TutorialContentSchema.StepIds.Count;
        }

        /// <summary>条目 id → 所属 step 的闭集下标(口述的所属步 = 列出它的那一步)。</summary>
        private static int StepOrdinalOfEntry(string entryId, List<TutorialStep> steps)
        {
            for (int i = 0; i < steps.Count; i++)
                for (int j = 0; j < steps[i].NarrationIds.Count; j++)
                    if (string.Equals(steps[i].NarrationIds[j], entryId, StringComparison.Ordinal))
                        return StepOrdinal(steps[i].Id);
            return TutorialContentSchema.StepIds.Count;
        }

        // ─── 载荷写入 ──────────────────────────────────────────────────────

        private static void WriteSteps(BinaryWriter w, List<TutorialStep> steps)
        {
            w.Write(steps.Count);
            for (int i = 0; i < steps.Count; i++)
            {
                TutorialStep s = steps[i];
                WriteString(w, s.Id);
                WriteString(w, s.Title);
                WriteString(w, s.PredicateKind);
                WriteString(w, s.Note);
                WriteStringArray(w, s.NarrationIds);
                WriteStringArray(w, s.DemonstrationIds);
                WriteStringArray(w, s.PaperIds);
            }
        }

        private static void WriteNarration(BinaryWriter w, List<TutorialNarration> rows)
        {
            w.Write(rows.Count);
            for (int i = 0; i < rows.Count; i++)
            {
                WriteString(w, rows[i].Id);
                WriteString(w, rows[i].CueId);
                w.Write(rows[i].Order);
                WriteString(w, rows[i].Text);
            }
        }

        private static void WriteDemonstrations(BinaryWriter w, List<TutorialDemonstration> rows)
        {
            w.Write(rows.Count);
            for (int i = 0; i < rows.Count; i++)
            {
                WriteString(w, rows[i].Id);
                WriteString(w, rows[i].StepId);
                WriteString(w, rows[i].Actor);
                WriteString(w, rows[i].ClipKey);
            }
        }

        private static void WritePapers(BinaryWriter w, List<TutorialPaper> rows)
        {
            w.Write(rows.Count);
            for (int i = 0; i < rows.Count; i++)
            {
                WriteString(w, rows[i].Id);
                WriteString(w, rows[i].StepId);
                WriteString(w, rows[i].Title);
                WriteString(w, rows[i].Body);
                WriteString(w, rows[i].AspectRatio);
                WriteString(w, rows[i].IllustrationKey);
            }
        }

        // ─── 头 + 小件(与 CookedFormat 严格镜像)─────────────────────────

        private static byte[] FinishHeader(byte[] payload, uint schemaVersion, uint configVersion)
        {
            var output = new byte[CookedFormat.HeaderSize + payload.Length];
            string magic = CookedFormat.Magic;
            output[0] = (byte)magic[0];
            output[1] = (byte)magic[1];
            output[2] = (byte)magic[2];
            output[3] = (byte)magic[3];
            WriteU32Le(output, 4, CookedFormat.FormatVersion);
            WriteU32Le(output, 8, schemaVersion);    // 头槽承载(非载荷首字段)
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

        private static void WriteString(BinaryWriter w, string value)
        {
            if (value == null) { w.Write(-1); return; }
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            w.Write(bytes.Length);
            w.Write(bytes);
        }

        private static void WriteStringArray(BinaryWriter w, IReadOnlyList<string> values)
        {
            if (values == null) { w.Write(-1); return; }
            w.Write(values.Count);
            for (int i = 0; i < values.Count; i++) WriteString(w, values[i]);
        }
    }
}
