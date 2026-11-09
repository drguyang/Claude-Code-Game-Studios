// S25-4c —— 教学链的**装配缝**:装配期定下「表 + 翻译层 + 空间化锚点」,
// 玩家生成后再 `Attach` 出 `TutorialChainDriver`(玩家 id 到那时才确定)。
//
// 为什么拆成两步:`TutorialDispatcher` / `TutorialTrigger` 的构造**必须**拿到
// `player_id`(机制 A),而玩家 id 由 `BootRoot` 启动序第 4 步生成 —— `CompositionRoot.Assemble`
// 在它之前。同 `JudgmentWriter.SetAuthor` 的既有形状:装配期给形状,运行期给身份。
//
// ⚠️ `Attach` 内部会**立即播种**(按当时流投影一次,只置饱和不 Fire)——
//    读档场景下存档里已完成的步因此不会被重播。调用点必须落在「流已就绪、玩家已有 id」之后。

using System;
using DaYiJingCheng.Gameplay.Presentation.Tutorial;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>教学链的装配缝(装配期构造,`Attach` 时绑定玩家)。</summary>
    public sealed class TutorialChainSeams
    {
        private readonly TutorialContentTable _content;
        private readonly TutorialMediaDriver _media;
        private readonly Func<Int3> _sourceCell;
        private readonly Func<bool> _checkpointWritten;

        /// <summary>构造。</summary>
        /// <param name="content">48 内容表(`data-core` 产物)。</param>
        /// <param name="media">翻译层(44 cue + 42 字幕)。</param>
        /// <param name="sourceCell">口述的空间化锚点取值委托。</param>
        /// <param name="checkpointWritten">7a 的「已存过档」回执;**null = 该缝未接**。</param>
        /// <exception cref="ArgumentException">内容表为零步 —— 装配期即拒(空教学 ≠ 无教学)。</exception>
        public TutorialChainSeams(
            TutorialContentTable content,
            TutorialMediaDriver media,
            Func<Int3> sourceCell,
            Func<bool> checkpointWritten = null)
        {
            if (content.Steps.Count == 0)
                throw new ArgumentException("48 内容表为零步 —— 教学链拒绝以空表装配(空表会让六步永不完结,静默)",
                    nameof(content));
            _content = content;
            _media = media ?? throw new ArgumentNullException(nameof(media));
            _sourceCell = sourceCell ?? throw new ArgumentNullException(nameof(sourceCell));
            _checkpointWritten = checkpointWritten;
        }

        /// <summary>`archive` 步的回执缝是否已接(`false` ⇒ 该步永不完结)。</summary>
        public bool CheckpointWritable => _checkpointWritten != null;

        /// <summary>
        /// 48 内容表**本身**(T10 · 2026-11-09)。
        /// <para>为什么公开:手册近景(42 的 `PaperCloseup48`)要读同一张 `papers[]`,
        /// 而它的宿主住 `Gameplay.Boot`。若宿主另走一条装载路径,同一张表就有两条来路
        /// —— 一条烘焙产物、一条随便什么,迟早分叉。本属性让**同一个实例**被两个消费者读。</para>
        /// <para>⚠️ 只读语义:调用方不得改表(表是不可变解码产物)。</para>
        /// </summary>
        public TutorialContentTable Content => _content;

        /// <summary>内容表(`internal` —— 仅供同程序集的走查仪表复查口述清单)。</summary>
        internal TutorialContentTable ContentForDiagnostics => _content;

        /// <summary>翻译层(`internal` —— 仅供同程序集的走查仪表逐条投递口述)。</summary>
        internal TutorialMediaDriver MediaForDiagnostics => _media;

        /// <summary>口述的空间化锚点取值委托(`internal` —— 同上)。</summary>
        internal Func<Int3> SourceCellForDiagnostics => _sourceCell;

        /// <summary>绑定玩家,产出可驱动的链。</summary>
        /// <param name="stream">三流合并视图。</param>
        /// <param name="decoder">载荷解码器。</param>
        /// <param name="playerId">本机玩家 id(机制 A;负值会被 `TutorialTrigger` 拒)。</param>
        public TutorialChainDriver Attach(EventStream stream, IPayloadDecoder decoder, int playerId)
            => new TutorialChainDriver(stream, decoder, _content, playerId, _media, _sourceCell, _checkpointWritten);
    }
}
