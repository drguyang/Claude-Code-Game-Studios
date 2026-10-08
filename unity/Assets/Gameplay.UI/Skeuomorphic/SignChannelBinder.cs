// 权威来源:design/gdd/diagnosis-system.md 规则五四态 · F-8.2 空档回退(:1003-1011) ·
//   §Edge Cases(:1178-1186 并列不相斥 / 阴性形态) · skeuomorphic-ui.md:64-65(纸面物理形态回答
//   「看懂了没有」) + production/epics/skeuomorphic-ui/story-024-m2-form-item-4-status-feedback-channel.md
//
// 设计说明:
//   · M2 形态件④「一条真实状态反馈通道」:体征词条(8 的 SignLexemeRow 真源)→ 脉案五通道行。
//   · **42 只画**:分流 + 档位取词 + F-8.2 回退,零自定义映射(通道/词/极性全来自 8 的表)。
//   · 纯函数零状态;运行时绑定(9 查体 revealed 集 + 挂行)归 9 实现轮 / 数据绑定轮 —— 本类零施加点。

namespace DaYiJingCheng.Gameplay.UI.Skeuomorphic
{
    using System;
    using System.Collections.Generic;
    using DaYiJingCheng.Gameplay.Presentation.Diagnosis; // SignLexemeRow / SignChannel / SignPolarity

    /// <summary>一条通道读数(分发产物 —— 供 42 画到对应通道行)。</summary>
    public readonly struct ChannelReading
    {
        /// <summary>体征主键(8 的 R-8.1)。</summary>
        public readonly string SignId;

        /// <summary>呈现词(8 的 <c>DisplayWords</c> 经 F-8.2 回退后的落点)。
        /// <para><c>null</c> = 该精度下回退到底仍无词 = **读不出** —— 按**阴性形态**显示
        /// (diagnosis-system :1005,不是阳性;呈现层挂 `.channel-reading-negative`)。</para></summary>
        public readonly string Word;

        /// <summary>极性(透传 8 的表;阴性词条与「读不出」在呈现层同走阴性形态类)。</summary>
        public readonly SignPolarity Polarity;

        /// <summary>输入序(同通道多词条**并列显示不相斥** —— :1183;保序供渲染层稳定排版)。</summary>
        public readonly int Order;

        public ChannelReading(string signId, string word, SignPolarity polarity, int order)
        {
            SignId = signId;
            Word = word;
            Polarity = polarity;
            Order = order;
        }
    }

    /// <summary>体征词条 → 五通道分发(纯函数 · M2 形态件④ 通道本体)。</summary>
    public static class SignChannelBinder
    {
        /// <summary>通道闭集(五通道 + 病史例外 —— AC-8-32「五通道 + 病史例外」)。</summary>
        public const int ChannelCardinality = 6;

        /// <summary>把已查词条按通道分发、按档位取词。
        /// <para><b>规则</b>(逐条挂权威):</para>
        /// <para>① 按 <c>Channel</c> 分流,键集 ⊆ 枚举闭集(非法枚举值 fail-loud);</para>
        /// <para>② 档位取词 = 从 <paramref name="slot"/> 起**向数组尾找第一个非空词**
        /// (F-8.2 :1003 字面:粗档无专属词 → 中档;全空 ⇒ 读不出)。
        /// <para>⚠️ **等价性出处**(评审 A1):「向数组尾」≡「向高精度 slot」的约定
        /// = `SignLexemeRow.DisplayWords` **数组序即 slot 序**(粗/中/细,见
        /// `DiagnosisSignTable.cs:109-110` 字段注释)—— 该约定是 GDD 与烘焙表的接口,
        /// 非本类自定;数组序若变,此处回退方向须同步。</para>
        /// <para>③ 回退到底无词 ⇒ <see cref="ChannelReading.Word"/> = <c>null</c>
        /// (读不出 ⇒ 阴性形态,:1005 —— 不是阳性);</para>
        /// <para>④ 同通道多词条**并列保序**(:1183 不相斥);⑤ 零状态零副作用。</para></summary>
        /// <param name="revealed">已查词条流(9 查体链的 revealed 集 —— 归属 9,本函数只消费)。</param>
        /// <param name="slot">当前精度档槽位(0 = 粗 … 2 = 细;越界 fail-loud)。</param>
        /// <exception cref="ArgumentOutOfRangeException">slot 越界或词条通道枚举非法。</exception>
        public static IReadOnlyDictionary<SignChannel, IReadOnlyList<ChannelReading>> Bind(
            IEnumerable<SignLexemeRow> revealed, int slot)
        {
            if (revealed == null) throw new ArgumentNullException(nameof(revealed));
            // 全通道预建空桶:未出现词条的通道 = 空列表(渲染层据此走「未查 = 空行」,
            // skeuomorphic-ui :329 —— 未查是空行不是徽章,分发器不发明第四态)。
            var byChannel = new Dictionary<SignChannel, List<ChannelReading>>();
            foreach (SignChannel ch in Enum.GetValues(typeof(SignChannel)))
                byChannel[ch] = new List<ChannelReading>();

            int order = 0;
            foreach (var row in revealed)
            {
                if (!byChannel.ContainsKey(row.Channel))
                    throw new ArgumentOutOfRangeException(nameof(revealed), row.Channel,
                        "[SignChannelBinder] 词条通道超出枚举闭集(五通道 + 病史例外)。");
                string word = WordAtOrFallback(row.DisplayWords, slot);
                byChannel[row.Channel].Add(
                    new ChannelReading(row.SignId, word, row.Polarity, order));
                order++;
            }

            var result = new Dictionary<SignChannel, IReadOnlyList<ChannelReading>>();
            foreach (var kv in byChannel)
                result[kv.Key] = kv.Value.AsReadOnly();
            return result;
        }

        /// <summary>档位取词 + F-8.2 向尾回退:从 <paramref name="slot"/> 起取第一个非空词;
        /// 全空 ⇒ <c>null</c>(读不出)。空串按空处理(GDD 禁空串 —— 空串与阴性形态呈现层同构,
        /// 此处防御性归入回退,真表零空串由 AC-024-3 断言)。</summary>
        private static string WordAtOrFallback(string[] words, int slot)
        {
            if (words == null || words.Length == 0) return null;
            if (slot < 0 || slot >= words.Length)
                throw new ArgumentOutOfRangeException(nameof(slot), slot,
                    $"[SignChannelBinder] 档位 slot 越界(词表长度 {words.Length})。");
            for (int i = slot; i < words.Length; i++)
                if (!string.IsNullOrEmpty(words[i]))
                    return words[i];
            return null;
        }
    }
}
