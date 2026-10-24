// 存档组版器 —— S19-1 判据 ④ 的「位置半」落点。
//
// 权威来源:
//   ADR-010 §一 —— 存档布局 = 头部 + 三流 + 快照;全二进制,显式小端
//   ADR-010 §四 —— 校验和覆盖 checksum 字段之后的全部字节
//   ADR-010 §六 —— Checkpoint 唯一写入口;组版由构造注入的 compose 委托提供
//   ADR-010 §三 义务 G-2 —— 折叠谓词 + 折叠行产出
//   ADR-026 §⑦ —— 折叠谓词 `Folded(p)` 豁免 `SkillGrown` 行(案 1)
//   production/qa/evidence/s19-1-fold-adjudication-2026-10-24.md §二(甲-a 接线形态)
//   production/sprints/sprint-19.md §S19-1 判据 ④ 位置半
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
// ⚠️ 本类**只完成判据 ④ 的位置半 + 委托半**,不完成字节半
// ─────────────────────────────────────────────────────────────────────────────
// 判据 ④ 字节半(「`Checkpoint` 产出的字节里折叠行出现」)= **NOT-RUN**
// (用户 2026-10-24 裁定:并入 `TD-persist-03`)。缺口逐条实测见 adjudication 原件 §三 B-1…B-6:
//   B-1 `SaveHeader` 七字段无折叠段偏移(只有 `SnapshotOffset`)
//   B-2 `Sim.Codec` 内零 `FoldRow` 编码器
//   B-3 全库无任何存档体生产器(本类是**第一个**,但只组「头部 + 事件列表」两段)
//   B-4 `SimEventCodec.Decode` 只解单事件头部(列表容器 = `SimEventListCodec`,S19-1 新写)
//   B-5 `CodecWriter` 的数组写只有定长 `WriteFieldArrayInt32/Int64`
//   B-6 `SaveHeader` 无读回方法(读回 = `SaveHeaderCodec.Read`,S19-1 新写)
//
// ⇒ 本类遇到「确实需要产出折叠行」时**fail-loud 抛出**,而不是写一个丢了折叠行的存档
//    —— 折叠行未落盘 = 被抹掉的病史行不可恢复(`HistoryFolder.Erase` 不可逆)。
//    「写坏档」比「写不出档」严重得多。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>
    /// 存档组版器 —— 把 sim 的当前状态折成一份完整的存档字节(含校验和槽)。
    /// <para><b>纯函数</b>(无 I/O、无墙钟、无表现态):同样的事件流 + 同样的谓词 ⇒ 同样字节。
    /// 写盘归 <c>SaveService.SaveBytes</c>,不在本类。</para>
    /// </summary>
    public static class SaveComposer
    {
        /// <summary>
        /// 组版一份存档。
        /// </summary>
        /// <param name="allEvents">三流合并视图(`EventStream.Events`;本类按
        /// <see cref="StreamRouting.Of"/> 自行分流,不假设调用方已分好)。</param>
        /// <param name="folded">折叠谓词(判定归它 —— 本类只执行,不重复判)。</param>
        /// <param name="decoder">载荷解码器(ADR-031;`HistoryFolder.Fold` 的必需入参)。</param>
        /// <param name="codec">事件编解码器。</param>
        /// <param name="worldSeed">世界种子(存档头,ADR-007 §二)。</param>
        /// <param name="configVersion">配置版本(存档头,ADR-014 §五)。⚠️ 见下方缺口注。</param>
        /// <param name="tick">当前逻辑 tick(存档头)。</param>
        /// <returns>完整存档字节:`magic + SHA256 + 头部其余字段 + 事件列表段`。</returns>
        /// <exception cref="ArgumentNullException">任一引用入参为 null。</exception>
        /// <exception cref="NotSupportedException">
        /// 存在**需要折叠**的病人(谓词为真且产出折叠行)—— 折叠行字节编码器尚不存在
        /// (`TD-persist-03` 动作 ③/⑤)。**拒绝写出丢掉折叠行的存档。**
        /// </exception>
        /// <remarks>
        /// ⚠️ <b>`configVersion` 的运行期来源缺口(如实登记,不假装解决)</b>:
        /// ADR-014 §五 定 `ConfigVersion` = 源数据集内容哈希派生,而派生器
        /// (`ConfigVersionUtility`)住 `Editor.Tools.Bake` —— **不进构建**。
        /// 运行期各烘焙数据集**各带一份**自己的 `ConfigVersion`,**无全局当前值**
        /// (`grep -rn "ConfigVersion" unity/Assets/Gameplay.Boot unity/Assets/Sim unity/Assets/Telemetry`
        /// 除 51 的比对件外零命中)。
        /// ⇒ 装配根今日传 <c>0</c> 占位。后果:51 的 `AC-51-D7`(存档头 ConfigVersion 与当前产物比对)
        /// 在 P0 恒「匹配」—— <b>这是假绿形态,不得读成「版本比对已实现」</b>。
        /// 真来源归 `TD-persist-03`(与三流字节布局同批裁)。
        /// </remarks>
        public static byte[] Compose(
            IReadOnlyList<SimEvent> allEvents,
            IFoldedPredicate folded,
            IPayloadDecoder decoder,
            ISaveCodec codec,
            ulong worldSeed,
            uint configVersion,
            long tick)
        {
            if (allEvents == null) throw new ArgumentNullException(nameof(allEvents));
            if (folded == null) throw new ArgumentNullException(nameof(folded));
            if (decoder == null) throw new ArgumentNullException(nameof(decoder));
            if (codec == null) throw new ArgumentNullException(nameof(codec));

            IReadOnlyList<SimEvent> history = FilterByStream(allEvents, StreamId.History);
            IReadOnlyList<SimEvent> caseStream = FilterByStream(allEvents, StreamId.Case);

            IReadOnlyList<SimEvent> body = FoldHistory(history, caseStream, folded, decoder);

            byte[] header = SaveHeaderCodec.WriteWithoutChecksum(worldSeed, configVersion, tick, 0UL);
            byte[] events = SimEventListCodec.Encode(body, codec);

            var combined = new byte[header.Length + events.Length];
            Array.Copy(header, 0, combined, 0, header.Length);
            Array.Copy(events, 0, combined, header.Length, events.Length);
            return SaveService.AddChecksum(combined);
        }

        /// <summary>
        /// 逐病人执行折叠。**这是 `HistoryFolder.Fold` 的生产调用点**(判据 ④ 位置半 (i))。
        /// <para>谓词为假的病人原样保留;谓词为真 ⇒ 调 `Fold`,而其产出**必须能落字节** ——
        /// 今日不能 ⇒ 抛。</para>
        /// </summary>
        private static IReadOnlyList<SimEvent> FoldHistory(
            IReadOnlyList<SimEvent> history,
            IReadOnlyList<SimEvent> caseStream,
            IFoldedPredicate folded,
            IPayloadDecoder decoder)
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

                // 谓词为真但产出零行 = 恒等折叠(无 onset / 无终态可折)⇒ 安全,继续。
                if (result.Rows.Count == 0) continue;

                // ⚠️ 有折叠行要落盘,但折叠行编码器不存在 ⇒ fail-loud。
                //    静默只写 Retained 会**永久抹掉**被折病人的病史行(不可逆)。
                throw new NotSupportedException(
                    $"病人 {patientId} 产出了 {result.Rows.Count} 条折叠行,但存档体尚无折叠段布局 —— " +
                    "拒绝写出丢掉折叠行的存档(被抹掉的病史行不可恢复)。" +
                    "折叠段字节编码器归 TD-persist-03(动作 ③ 三流字节布局 / 动作 ⑤ 折叠段)。" +
                    "本条 = S19-1 判据 ④ 字节半的 NOT-RUN 事实,不得标绿。");
            }

            return current;
        }

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
