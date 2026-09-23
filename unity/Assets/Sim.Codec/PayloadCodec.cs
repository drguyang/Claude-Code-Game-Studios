// R-1(U0-b 残留)· 34 支载荷编解码主体 + PayloadRef 访问签名(支 0 甲 / 拍板点 0-2)。
//
// 权威来源:
//   ADR-024 §① —— schema 真源 = design/registry/entities.yaml 各 Kind 的 payload_schema;
//     本文件的字段序 / 宽度 / tag 分配逐字转录自它(经 Payloads/ 三文件的 struct 声明序镜像)。
//   ADR-006 §五 —— 按字段名/tag 编码,禁位置打包。本层的实现形:
//     **1 字节 tag + 定宽小端值**;tag = struct 声明序(1 起,升序 = canonical 落笔序)。
//     解码为**乱序容忍**(field-name 语义的判据:字段重排不改可解码性 —— 见 shuffled 测试)。
//   ADR-006 Amendment G-3 —— freehand_text 例外迁入 blob 变长 UTF-8 段:
//     JudgmentRecorded / JudgmentRevised 恒写 tag 6 = byteLen + 严格 UTF-8;
//     **sim 消费面不持有该值**(struct 无字段;Decode<T> 读出即弃 —— 违例面在消费,
//     不在游标推进)。呈现读取 = TryGetFreehandText(仅 42 / 37 呈现侧调用)。
//   b1b 设计卡 · 支 1 —— 34 支 struct 由本 codec 构造(ctor 已于本批补入,
//     依 PayloadCommon.cs 原注「归首个消费代码批补 ctor」)。
//   b1b 拍板点 0-2 —— codec 访问签名 = TryGetPayload<T>(...);原卡签名省写了
//     pool 形参(载荷字节必须寻址 blob 池),本批补全,登记于此。
//
// 读写侧 API 形(两端对称性说明,防误用):
//   写侧 WriteField*(tag, value) = tag + 值**原子落笔**(调用方不另写 tag);
//   读侧 ReadField*()            = **仅值段**(tag 已在 Decode 分发循环消耗)。
//
// 严格性(同 CodecReader 头注):未知 tag / 重复 tag / 缺字段(位掩码不符)/
//   跨字段约束(EmergencyAttempt.Edges=长度、Craft 两组等长)⇒ InvalidDataException。
//   写侧对同一约束先拒(ArgumentException)—— 坏数据不进字节面。

using System;
using System.IO;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.Codec
{
    /// <summary>34 支 per-Kind 载荷的唯一编解码器(b1b 支 0 甲:解码即构造)。</summary>
    public static partial class PayloadCodec
    {
        // ── 分发 ──────────────────────────────────────────────────────────

        /// <summary>
        /// 按 Kind 解码为强类型载荷。类型与 Kind 不匹配 ⇒ 抛(不静默 default);
        /// 未知 Kind / schema 违例 ⇒ InvalidDataException。
        /// </summary>
        public static T Decode<T>(EventKind kind, ReadOnlySpan<byte> payloadBytes) where T : struct
        {
            object boxed = DecodeBoxed(kind, payloadBytes);
            if (boxed is T typed) return typed;
            throw new InvalidDataException(
                $"Kind={kind} 的载荷类型 {boxed.GetType().Name} 与请求的 {typeof(T).Name} 不匹配");
        }

        private static object DecodeBoxed(EventKind kind, ReadOnlySpan<byte> payloadBytes)
        {
            switch (kind)
            {
                case EventKind.InjuryOnset:               return DecodeInjuryOnset(payloadBytes);
                case EventKind.CompoundTriggered:         return DecodeCompoundTriggered(payloadBytes);
                case EventKind.CompoundExpired:           return DecodeCompoundExpired(payloadBytes);
                case EventKind.CareApplied:               return DecodeCareApplied(payloadBytes);
                case EventKind.EmergencyAttempt:          return DecodeEmergencyAttempt(payloadBytes);
                case EventKind.EmergencyTreatmentApplied: return DecodeEmergencyTreatmentApplied(payloadBytes);
                case EventKind.DrugTreatmentApplied:      return DecodeDrugTreatmentApplied(payloadBytes);
                case EventKind.SkillGrown:                return DecodeSkillGrown(payloadBytes);
                case EventKind.EventRolled:               return DecodeEventRolled(payloadBytes);
                case EventKind.EventArrived:              return DecodeEventArrived(payloadBytes);
                case EventKind.ThreatDeferred:            return DecodeThreatDeferred(payloadBytes);
                case EventKind.ThreatDeferralCleared:     return DecodeThreatDeferralCleared(payloadBytes);
                case EventKind.HistoryFlagChanged:        return DecodeHistoryFlagChanged(payloadBytes);
                case EventKind.CaseOpened:                return DecodeCaseOpened(payloadBytes);
                case EventKind.CaseClosed:                return DecodeCaseClosed(payloadBytes);
                case EventKind.PatternRecognized:         return DecodePatternRecognized(payloadBytes);
                case EventKind.JudgmentRecorded:          return DecodeJudgmentRecorded(payloadBytes);
                case EventKind.JudgmentRevised:           return DecodeJudgmentRevised(payloadBytes);
                case EventKind.ActorCellEntered:          return DecodeActorCellEntered(payloadBytes);
                case EventKind.ResourceHarvested:         return DecodeResourceHarvested(payloadBytes);
                case EventKind.DropSpawned:               return DecodeDropSpawned(payloadBytes);
                case EventKind.DropClaimed:               return DecodeDropClaimed(payloadBytes);
                case EventKind.DropDespawned:             return DecodeDropDespawned(payloadBytes);
                case EventKind.Craft:                     return DecodeCraft(payloadBytes);
                case EventKind.StructurePlaced:           return DecodeStructurePlaced(payloadBytes);
                case EventKind.StructureModified:         return DecodeStructureModified(payloadBytes);
                case EventKind.StructureRemoved:          return DecodeStructureRemoved(payloadBytes);
                case EventKind.PoiStateChanged:           return DecodePoiStateChanged(payloadBytes);
                case EventKind.EnemyInjuryOnset:          return DecodeEnemyInjuryOnset(payloadBytes);
                case EventKind.InjuryStateChanged:        return DecodeInjuryStateChanged(payloadBytes);
                case EventKind.EncounterStarted:          return DecodeEncounterStarted(payloadBytes);
                case EventKind.EncounterEnded:            return DecodeEncounterEnded(payloadBytes);
                case EventKind.ConsequenceResolved:       return DecodeConsequenceResolved(payloadBytes);
                case EventKind.PlayerDied:                return DecodePlayerDied(payloadBytes);
                default:
                    throw new InvalidDataException($"未知 EventKind={(int)kind} —— 不在 34 支闭集内");
            }
        }

        // ── 分发循环共用件 ────────────────────────────────────────────────
        // seen 掩码纪律:每个 tag 恰好一次(缺失 / 重复均在循环尾部拒绝)。
        // tag 值域 1..31(uint 掩码上限;现有 schema 最宽 = Judgment 6 字段)。

        // ref:CodecReader 是 ref struct,按值传 = 拷贝 —— ReadTag 推进的是副本,
        // 调用方游标停在原地,下一轮把上一字段的值字节误读成 tag(2026-09-23 桌面 4 红根因)。
        private static byte ReadTagChecked(ref CodecReader r, string kindName, ref uint seen)
        {
            byte tag = r.ReadTag();
            if (tag < 1 || tag > 31)
                throw new InvalidDataException($"{kindName}: tag={tag} 越界(1..31)");
            uint bit = 1u << (tag - 1);
            if ((seen & bit) != 0)
                throw new InvalidDataException($"{kindName}: tag={tag} 重复");
            seen |= bit;
            return tag;
        }

        private static void RequireCompleteMask(string kindName, uint seen, uint expected)
        {
            if (seen != expected)
                throw new InvalidDataException(
                    $"{kindName}: 缺字段(已见 mask=0x{seen:X},应为 0x{expected:X})");
        }

        // ── PayloadRef 访问签名(拍板点 0-2)──────────────────────────────

        /// <summary>
        /// 经事件头的 PayloadRef 从 blob 池寻址并解码为强类型载荷。
        /// 池未命中 / 引用越界 ⇒ false + default(**宽容** —— 事件可能引用未驻留 blob);
        /// 字节内容违反 schema ⇒ 抛(损坏不是「查无此数据」)。
        /// </summary>
        public static bool TryGetPayload<T>(in SimEvent e, IBlobPool pool, out T payload)
            where T : struct
        {
            payload = default;
            if (pool == null) throw new ArgumentNullException(nameof(pool));
            if (!TryGetPayloadSpan(e, pool, out var span)) return false;
            payload = Decode<T>(e.Kind, span);
            return true;
        }

        /// <summary>
        /// 仅读 Judgment 的 freehand_text(G-3 变长段)。呈现侧专用 —— sim 消费面
        /// 调用本方法即违例纪律。非 Judgment 两支 Kind ⇒ InvalidOperationException(编程错误)。
        /// 池未命中 / 越界 ⇒ false + null。
        /// </summary>
        public static bool TryGetFreehandText(in SimEvent e, IBlobPool pool, out string text)
        {
            text = null;
            if (pool == null) throw new ArgumentNullException(nameof(pool));
            if (e.Kind != EventKind.JudgmentRecorded && e.Kind != EventKind.JudgmentRevised)
                throw new InvalidOperationException(
                    $"TryGetFreehandText 仅适用 Judgment 两支,当前 Kind={e.Kind} —— sim 侧不得读取自由文本");
            if (!TryGetPayloadSpan(e, pool, out var span)) return false;
            text = ExtractFreehand(e.Kind, span);
            return true;
        }

        private static bool TryGetPayloadSpan(in SimEvent e, IBlobPool pool, out ReadOnlySpan<byte> span)
        {
            span = default;
            var r = e.Payload;
            if (r.Offset < 0 || r.Length < 0) return false;
            if (!pool.TryGetBlob(r.BlobId, out var blob)) return false;
            if ((long)r.Offset + r.Length > blob.Length) return false;
            span = blob.Span.Slice(r.Offset, r.Length);
            return true;
        }

        /// <summary>Judgment 段中提取 tag 6(其余 tag 读出即弃 —— 游标推进非消费)。</summary>
        private static string ExtractFreehand(EventKind kind, ReadOnlySpan<byte> bytes)
        {
            var r = new CodecReader(bytes);
            uint seen = 0;
            string freehand = null;
            string kindName = kind.ToString();

            while (r.HasMore)
            {
                byte tag = ReadTagChecked(ref r, kindName, ref seen);
                switch (tag)
                {
                    case 1: r.ReadInt32LittleEndian(); break;                    // PatientId
                    case 2: r.ReadFieldCaseId(); break;                          // CaseId
                    case 3: r.ReadInt32LittleEndian(); break;                    // AuthorPlayerId
                    case 4: r.ReadInt32LittleEndian(); break;                    // LexiconId
                    case 5: r.ReadByte(); break;                                 // Confidence
                    case 6: freehand = r.ReadFieldUtf8(); break;                 // freehand_text
                    default:
                        throw new InvalidDataException($"{kindName}: 未知 tag={tag}");
                }
            }
            RequireCompleteMask(kindName, seen, (1u << 6) - 1);
            r.EnsureFullyConsumed();
            return freehand ?? string.Empty;
        }
    }
}
