// 存档组版器 —— S19-1 判据 ④ 位置半 + 委托半的落点;
// 2026-10-24 `TD-persist-03` 修复轮完成**字节半**(动作 ③⑤,用户四裁定)。
//
// 权威来源:
//   ADR-010 §一 + §一增补(本批 2026-10-24)—— 存档体 v1 布局 =
//     `头部 + 事件段(三流合一单列表)+ 折叠段`;快照段 / 分册态段**留待后续**
//     (`SnapshotOffset` 写 0 哨兵)。用户裁定:**修 ADR-010 承认单列表**(甲案),
//     不实现物理分块;病例流 / 世界流**必须进存档**(消灭 S19-1 版的静默丢弃)。
//   ADR-010 §四 —— 校验和覆盖 checksum 字段之后的全部字节
//   ADR-010 §六 —— Checkpoint 唯一写入口;组版由构造注入的 compose 委托提供
//   ADR-010 §三 义务 G-2 —— 折叠谓词 + 折叠行产出
//   ADR-026 §⑦ —— 折叠谓词 `Folded(p)` 豁免 `SkillGrown` 行(案 1)
//   production/qa/evidence/s19-1-fold-adjudication-2026-10-24.md §二(甲-a 接线形态)
//   docs/tech-debt-register.md TD-persist-03(本类兑现其动作 ③ + ⑤ 与卡住判据的字节半)
//
// ─────────────────────────────────────────────────────────────────────────────
// 为什么本类住 `Gameplay.Boot`(而不是 `Sim.Codec`)
// ─────────────────────────────────────────────────────────────────────────────
// `Sim` 与 `Sim.Codec` 的 asmdef `references` **互不相含**(两者均恰 = `{Sim.Contracts}`,
// ADR-025 §① 刻意)。而折叠动作要同时用到两侧的东西:
//   · `HistoryFolder` / `FoldRow` / `FoldedPredicate` —— 住 `Sim`
//   · `SaveHeaderCodec` / `SimEventListCodec` / `SaveCodec` —— 住 `Sim.Codec`
// ⇒ **唯一**同时看得见两者的装配就是本装配。这与 `CompositionRoot` 构造
//   `PayloadEncoder` / `PayloadDecoder` 是同一手法(案 A,`CompositionRoot.cs:186`)。
// ⚠️ 反例(已登记判例):给 `Sim.Codec` 加 `Sim` 引用**不撞任何机械门**
//    (b2 白名单被 `AssemblyGates.cs:214` 的 `if (asmName == "Sim")` 守卫、只对 `Sim` 生效;
//    b3 只比 asmdef 的 `name`、从不读 `references`)⇒ 门绿 ≠ ADR 合规。
//    原件 = `production/qa/evidence/s19-1-fold-adjudication-2026-10-24.md` §五。
//
// ─────────────────────────────────────────────────────────────────────────────
// 布局(v1 · 本批定稿;S19-1 旧版「头部 + 病史流两段」已成历史,见证据原件 §旧布局)
// ─────────────────────────────────────────────────────────────────────────────
//   [头部(headerSize) | 事件段(count 前缀 + 逐事件) | 折叠段(count 前缀 + 定长行)]
//   · 事件段 = **三流合一**(病史 + 病例 + 世界,按 `EventStream` 追加序 = Tick 非降);
//     折叠病人的病史行**从事件段抹除**(替换为其 Retained),折叠行落折叠段。
//   · 折叠段自描述(count 前缀)⇒ **无需头部偏移字段** —— S19-1 判据 B-1 的
//     「SaveHeader 无折叠段偏移」就此按「不需要」结案,不是「补字段」。
//     `SnapshotOffset` 仍为快照段预留,本批恒写 0(哨兵 = 无快照段)。
//   · 读侧对称性:`SaveService.Load` 事件段读尽后**必见**折叠段(≥4 字节),
//     原始字节交注入的折叠行委托,装配根经 `FoldRowCodec` 解回行(见其文件头)。
// ═════════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>
    /// 存档组版器 —— 把 sim 的当前状态折成一份存档字节(含校验和槽)。
    /// <para><b>纯函数</b>(无 I/O、无墙钟、无表现态):同样的事件流 + 同样的谓词 ⇒ 同样字节。
    /// 写盘归 <c>SaveService.SaveBytes</c>,不在本类。</para>
    /// <para>⚠️ <b>v1 布局</b>(本批定稿):头部 + 事件段(三流合一,折叠病人行已抹)+
    /// 折叠段。快照段 / 分册态段留待后续(<c>SnapshotOffset = 0</c> 哨兵)。</para>
    /// </summary>
    public static class SaveComposer
    {
        /// <summary>
        /// 组版一份存档。
        /// </summary>
        /// <param name="allEvents">三流合并视图(`EventStream.Events`;本类按
        /// <see cref="StreamRouting.Of"/> 自行识别病史流,不假设调用方已分好)。</param>
        /// <param name="folded">折叠谓词(判定归它 —— 本类只执行,不重复判)。</param>
        /// <param name="decoder">载荷解码器(ADR-031;`HistoryFolder.Fold` 的必需入参)。</param>
        /// <param name="codec">事件编解码器。</param>
        /// <param name="worldSeed">世界种子(存档头,ADR-007 §二)。</param>
        /// <param name="configVersion">配置版本(存档头,ADR-014 §五)。
        /// ⚠️ 生产路径 = `CompositionRoot` 的全局组合值(22 份产物头 FNV-1a 组合,
        /// 2026-10-24 `TD-epic-01-f` 接线;此处不做口径,只透传)。</param>
        /// <param name="tick">当前逻辑 tick(存档头)。</param>
        /// <param name="existingFoldedRows">
        /// 读档恢复后**已在 sim 里**的折叠行(<c>EventStream.FoldedRows</c>)。
        /// <para>⚠️ <b>回写义务,不是可选优化</b>:被折叠病人的病史行已从流中抹除
        /// ⇒ 本次折叠循环<b>看不见他们</b>,不回写 = 二次 Checkpoint 静默丢折叠行
        /// = 当年 fail-loud 要防的同一件事。生产路径恒传;缺省 null 视为空
        /// (全新会话首存,折叠段只含本轮新折的行)。</para>
        /// </param>
        /// <returns>完整存档字节:头部 + 事件段 + 折叠段(含校验和)。</returns>
        /// <exception cref="ArgumentNullException">任一引用入参为 null。</exception>
        public static byte[] Compose(
            IReadOnlyList<SimEvent> allEvents,
            IFoldedPredicate folded,
            IPayloadDecoder decoder,
            ISaveCodec codec,
            ulong worldSeed,
            uint configVersion,
            long tick,
            IReadOnlyList<FoldRow> existingFoldedRows = null,
            SaveBufferPool bufferPool = null)
        {
            if (allEvents == null) throw new ArgumentNullException(nameof(allEvents));
            if (folded == null) throw new ArgumentNullException(nameof(folded));
            if (decoder == null) throw new ArgumentNullException(nameof(decoder));
            if (codec == null) throw new ArgumentNullException(nameof(codec));

            // 折叠循环:逐病人(升序,确定性)执行 Fold;新折的行进 rowsAccum,
            // 处理后的病史(被抹行的)留在 currentHistory。
            var rowsAccum = new List<FoldRow>();
            if (existingFoldedRows != null && existingFoldedRows.Count > 0)
                rowsAccum.AddRange(existingFoldedRows); // 回写在前 —— 行序 = 既有行在前、新折行在后(确定性)

            IReadOnlyList<SimEvent> history = FilterByStream(allEvents, StreamId.History);
            IReadOnlyList<SimEvent> currentHistory = FoldHistory(history, folded, decoder, rowsAccum);

            // 把处理后的病史按原位置拼回全局序:非病史事件原样,病史事件从
            // currentHistory 顺序取(Fold 的 Retained 保持原相对序 ⇒ 指针拼接是恒等的)。
            List<SimEvent> finalEvents = SpliceBack(allEvents, currentHistory);

            byte[] header = SaveHeaderCodec.WriteWithoutChecksum(worldSeed, configVersion, tick, 0UL);
            byte[] events = SimEventListCodec.Encode(finalEvents, codec);
            byte[] foldedSegment = FoldRowCodec.EncodeSegment(rowsAccum);

            var combined = bufferPool == null
                ? new byte[header.Length + events.Length + foldedSegment.Length]
                : bufferPool.Rent(header.Length + events.Length + foldedSegment.Length);
            Array.Copy(header, 0, combined, 0, header.Length);
            Array.Copy(events, 0, combined, header.Length, events.Length);
            Array.Copy(foldedSegment, 0, combined, header.Length + events.Length, foldedSegment.Length);

            // 🔁 **2026-10-25 AC-7a-07:本方法**不再**加校验和(原文 `return SaveService.AddChecksum(combined)`)。
            //
            // 病因:AC-7a-07 逐字「**写盘 IO 与 SHA256 在非主线程**」,而 `Compose` 是
            // `compose` 委托的实现、在**主线程**的 Step 边界被调 ⇒ `AddChecksum` 在此处
            // 执行 = SHA256 在主线程,直接违反该 AC。
            //
            // 现分工:
            //   · **本方法(主线程)**:头部 + 事件段 + 折叠段 → **不含校验和槽**的字节;
            //   · **`SaveService` 的 worker(后台线程)**:`AddChecksum` 回填校验和 → 原子写。
            //
            // ⚠️ 返回形态因此变了:从「含校验和的完整存档」变成「不含校验和的待签名体」。
            //    `SaveService.StripChecksum` 的逆语义在此成立 —— 需要完整存档的调用方
            //    (夹具 / 迁移链)自行 `AddChecksum`。**14 处测试调用点已机械补齐。**
            //
            // ⚠️ **池的所有权**:`combined` 由本方法租出但**不归还** —— 它的内容被
            //    `AddChecksum` 拷贝后由 worker 归还(见 `SaveService.Checkpoint`)。
            //    本方法不知道下游何时用完,归还早了会被覆写。
            return combined;
        }

        /// <summary>
        /// 逐病人执行折叠。**这是 `HistoryFolder.Fold` 的生产调用点**(判据 ④ 位置半 (i))。
        /// <para>谓词为假的病人原样保留;谓词为真 ⇒ 调 `Fold`,其折叠行进
        /// <paramref name="rowsOut"/>,其 Retained 成为本轮病史。</para>
        /// </summary>
        private static IReadOnlyList<SimEvent> FoldHistory(
            IReadOnlyList<SimEvent> history,
            IFoldedPredicate folded,
            IPayloadDecoder decoder,
            List<FoldRow> rowsOut)
        {
            List<int> patients = CollectPatientIds(history);
            if (patients.Count == 0) return history;

            IReadOnlyList<SimEvent> current = history;

            for (int i = 0; i < patients.Count; i++)
            {
                int patientId = patients[i];

                // 谓词为假 ⇒ 不折(判定归谓词,本方法不重复判)。
                if (!folded.Folded(patientId)) continue;

                FoldResult result = HistoryFolder.Fold(current, patientId, folded, decoder);

                // ⚠️ 零行 = 恒等折叠。**今日该分支不可达**(进循环前已用同一谓词判过为真,
                //    而 `FoldedPredicate` 自陈无状态缓存 ⇒ `Fold` 内部不会反悔),留着是防御。
                //    它**不得**被读成「安全路径」—— 若谓词将来变为有状态,这里会静默吞掉
                //    本该落折叠行的情形(S19-1 `/code-review` R-2)。
                if (result.Rows.Count == 0) continue;

                rowsOut.AddRange(result.Rows);
                current = result.Retained;
            }

            return current;
        }

        /// <summary>
        /// 把处理后的病史(被抹行的)按**原位置**拼回全局事件序。
        /// <para>判定法 = **键集成员**:折叠抹行后 `processedHistory` 比原病史子列**短**
        /// (被折病人的行没了)⇒ 位置指针拼接必越界;改为对每条病史事件判
        /// 「是否在处理后的集合里」,不在 = 被抹,跳过。键 = 流内身份
        /// (Kind, Patient, Tick, Seq)—— 与 `EventStream` 去重键同构,流内唯一。</para>
        /// <para>事件段乱序 = 重放不确定 ⇒ 拼接保持原全局序(被抹行缺失)。</para>
        /// </summary>
        private static List<SimEvent> SpliceBack(
            IReadOnlyList<SimEvent> allEvents,
            IReadOnlyList<SimEvent> processedHistory)
        {
            var retained = new HashSet<string>();
            for (int i = 0; i < processedHistory.Count; i++)
                retained.Add(IdentityKey(processedHistory[i]));

            var final = new List<SimEvent>(allEvents.Count - processedHistory.Count);
            for (int i = 0; i < allEvents.Count; i++)
            {
                SimEvent e = allEvents[i];
                if (StreamRouting.Of(e.Kind) == StreamId.History)
                {
                    if (retained.Contains(IdentityKey(e)))
                        final.Add(e);      // 幸存的原位保留
                    // else:被折叠抹除 —— 原位跳过(缺失是本布局的语义,不是错位)
                }
                else
                {
                    final.Add(e); // 病例流 / 世界流原样进档(本批起不再静默丢弃)
                }
            }
            return final;
        }

        /// <summary>病史事件身份键(流内唯一,与 EventStream 去重键同构;
        /// 载荷身份不进键 —— 被折行与其幸存副本不会同键共存)。</summary>
        private static string IdentityKey(in SimEvent e)
            => $"{e.Kind}_{e.Patient.Value}_{e.Tick}_{e.Seq}";

        /// <summary>
        /// 病史流里出现过的病人 id(升序去重;`PatientId.None` = 世界级事件,不是可折叠对象)。
        /// <para>升序 = 折叠次序确定性(字节级夹具依赖它)。</para>
        /// </summary>
        private static List<int> CollectPatientIds(IReadOnlyList<SimEvent> history)
        {
            var seen = new HashSet<int>();
            var ids = new List<int>();
            for (int i = 0; i < history.Count; i++)
            {
                int id = history[i].Patient.Value;
                if (id < 0) continue;              // 世界级哨兵,非病人
                if (seen.Add(id)) ids.Add(id);
            }
            ids.Sort();
            return ids;
        }

        /// <summary>
        /// 按流别取子集。路由口径 = 既有 <see cref="StreamRouting.Of"/>(kindgen 产物),
        /// **不另抄白名单**(第二真源禁令)。与 `TelemetryReporter.FilterByStream` 同款,
        /// 但那处是 private ⇒ 本类自持一份(两行,不值得为它开公共件)。
        /// <para>公开是因为装配根构造 <see cref="FoldedPredicate"/> 时也要同一份分流
        /// (病史流 / 病例流两个只读视图),两处用同一实现才不会分叉。</para>
        /// </summary>
        public static IReadOnlyList<SimEvent> FilterByStream(IReadOnlyList<SimEvent> all, StreamId id)
        {
            var picked = new List<SimEvent>();
            for (int i = 0; i < all.Count; i++)
            {
                SimEvent e = all[i];
                if (StreamRouting.Of(e.Kind) == id) picked.Add(e);
            }
            return picked;
        }
    }
}
