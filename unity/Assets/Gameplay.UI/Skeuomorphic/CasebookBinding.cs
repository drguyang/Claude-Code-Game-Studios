// sprint-05 T2.1 / T2.3 · 42 侧只读绑定面(2026-10-10 立)
//
// 权威来源:
//   ADR-013 §9 C3 —— 42 只渲染,永不持有游戏状态(本接口零可变字段契约)
//   story-024 边界裁定 ①②④ —— 42 零自定义映射(通道/词/极性全消费 8 的表)
//   ADR-005 抽象点之四 `IVitalsQuery` / `VitalsDto`(全案唯一浮点出口)
//   story-011 双真源声明 —— `BuildUI()` 代码面与 `Casebook39.uxml` spec 面
//     **不得因数据绑定而变形**:本接口把「读」与「建树」分开 ⇒ BuildUI 不变、
//     PlayMode `casebook_render_test` 六条 + 交叉断言零回归。
//
// 设计说明:
//   · `IDtoSource` 是**标记接口**(具体 DTO 访问方法由实现方定义,Apr-ADR-013
//     原文)—— 本接口是 42 侧对该契约的**唯一收口形状**:driver 只知道要读什么,
//     不知道谁产这些数,更不自己造(禁假数据:零 fallback 词 / 零占位读数)。
//   · `VitalsDto` 是**门**(通道活跃掩码 + SignCount,AC-20 字段白名单),词条来自 8 的
//     `SignLexemeRow`(revealed 集归 9 查体链;本接口只消费,不生产)。
//   · 实现方住装配层(ADR-025 §① `Gameplay.Boot` 组合根案 A),运行期经场景引用
//     注入 driver —— 42 不反向引用装配层(装配方向不可倒)。

namespace DaYiJingCheng.Gameplay.UI.Skeuomorphic
{
    using System.Collections.Generic;
    using DaYiJingCheng.Gameplay.Presentation.Diagnosis;
    using DaYiJingCheng.Sim.Contracts;

    /// <summary>
    /// 脉案页的只读数据源契约(T2.1 DTO 绑定面)。
    /// <para>实现方 = 装配层的数据提供者(测试侧 = 合成 fixture);42 只读不写,
    /// 每次刷新**重读**(caching = 第二份真相,ADR-013 §9 C3 / IDtoSource 铁律)。</para>
    /// </summary>
    public interface ICasebookDtoSource : IDtoSource
    {
        /// <summary>活跃体征数(**门**;= <c>VitalsDto.SignCount</c>,AC-20 字段白名单内的整数)。
        /// <para>0 = 无体征可显示 ⇒ 全部通道空行(story-024 边界③ 未查 = 空行);&gt; 0 = 放行
        /// <see cref="RevealedRows"/> 进值区。</para>
        /// <para>⚠️ 为何不是 <c>VitalsDto</c> 属性:AC-42-D3 断言 42 命名空间**零 DTO 副本**
        /// (反射扫字段/属性类型)—— 那是「不持有」的机械判据,读穿属性也在其面上。
        /// 本接口因此只收 AC-20 白名单**整数**,DTO 本体留在装配层;这不是绕过 AC,
        /// 而是让 42 与 DTO 的耦合降到「一个白名单 int」。</para></summary>
        int ActiveSignCount { get; }

        /// <summary>已查体征词条流(9 查体链的 revealed 集 —— 归 9,42 只消费)。
        /// <c>null</c> / 空集 = 尚未查体 ⇒ 全部通道空行(story-024 边界③:
        /// 未查 = 空行,复用 <c>.empty-row</c>,`:329` 明文非 badge)。</summary>
        IReadOnlyList<SignLexemeRow> RevealedRows { get; }

        /// <summary>当前精度档槽位(0 = 粗 … 2 = 细;F-8.2 向尾回退起点)。
        /// 取值归读数侧(8 / 9),42 只透传给 <see cref="SignChannelBinder"/>。</summary>
        int PrecisionSlot { get; }

        /// <summary>当前读数行的墨龄 = `当前 tick − 落笔 tick`(**派生态**,两个 tick 相减)。
        /// <para>规则十三:`skeuomorphic-ui.md:351` —— 落笔 tick 归 37(判断记录落病例流,
        /// ADR-008),`当前 tick` 归 `ITickProvider` 只读视图(OQ-42-14 裁定:P0 就绪,
        /// **降级为会话内伪 tick 路径已否决**)。本属性 = **已减好的差值**,
        /// 42 侧因此**零计时器 / 零时钟读取**(AC-42-G6 反射面:42 内无计时器字段)。</para>
        /// <para>负值 = 落笔 tick 在未来 = 数据错误(fail-loud 由实现方负责)。</para>
        long InkAgeTicks { get; }

        /// <summary>病名册可指认条目 id 集(升序;病名栏 Submit ⇒ 右页内嵌病名册,§5.2)。
        /// <para>⚠️ <b>显式 `lexicon_id`(非显示索引)</b> —— TD 第五风险钉死:指认落笔须带
        /// 真实词表 ordinal(<c>JudgmentRecorded.lexicon_id</c>),呈现层不得用「册内第 n 条」
        /// 作代理(索引随条目集变化而漂移 ⇒ 落笔写错词)。</para>
        /// <para><b>守密</b>:只出 `lexicon_id`(装配层经 <c>LexiconBookSource</c> 反查词表
        /// <c>disease_ids</c> 后**只留 id**)—— <c>disease_id</c> 不进 42(AC-37-15)。</para>
        /// <para>空 = 未见过任何病 ⇒ 病名册空(反幻想二:没见过就翻不到)。</para>
        /// <para>⚠️ 42 只读不持:本属性每次重读(零缓存,ADR-013 §9 C3);实现方住装配层。</para>
        IReadOnlyList<int> SeenLexiconIds { get; }

        /// <summary>取词条文本(病名册条目显示用)。</summary>
        /// <param name="lexiconId">词条 id(≥ 1;来自 <see cref="SeenLexiconIds"/>)。</param>
        /// <returns>词条文本;未找到 ⇒ <c>null</c>(42 按「无字」处理,不崩)。</returns>
        /// <remarks>守密:只回文本,**不回** <c>disease_ids</c>(AC-37-15)。文本解析住装配层
        /// (<c>LexiconBookSource.TextOf</c>)—— 42 不持有词表。</remarks>
        string TextOfLexicon(int lexiconId);

        /// <summary>待落笔置信度档(0 = 空 / 1 = 疑似 / 2 = 确定;<c>OQ-CB-5</c> 甲循环落笔)。
        /// <para>⚠️ <b>42 只读不持</b>(ADR-013 §9 C3):档位住在命令面实现方(装配层),
        /// 42 每次渲染**回读**本属性 —— 与 <see cref="InkAgeTicks"/> / <see cref="SeenLexiconIds"/>
        /// 同构(装配层算、42 只读)⇒ 42 内零游戏状态字段(AC-42-D3)。</para>
        /// <para>值域 = <c>JudgmentState</c>(<c>Empty = 0 / Suspected = 1 / Confirmed = 2</c>);
        /// 与 <see cref="SeenLexiconIds"/> 的空集语义一致 —— 无绑定病人 / 未落笔 ⇒ 0。</para>
        /// </summary>
        byte PendingConfidence { get; }
    }

    /// <summary>
    /// 脉案页的**写通道**契约(42 UI 事件 → 装配层写者;<c>ICasebookDtoSource</c> 的镜像)。
    /// </summary>
    /// <remarks>
    /// <para><b>为何需要这条通道</b>:42 是纯渲染层(ADR-013 §9 C3 零写),而病名栏 Submit
    /// 必须路由到当前焦点目标(焦点态由 42 持有)⇒ Submit **必须由 42 自己接**,
    /// 再转发给装配层写者(ADR-011 <b>Amendment C</b> 2026-10-12 明确此口径,并否决了
    /// 「Boot 直读 Submit」的乙案 —— 那会迫使 Boot 读 42 的焦点态,破坏只读边界)。</para>
    /// <para>⚠️ <b>方向</b>:42 不反向引用装配层(ADR-025 §① 装配方向不可倒)——
    /// 本接口住 <c>Gameplay.UI</c>,实现方住 <c>Gameplay.Boot</c>,经
    /// <c>SkeuoRuntimeDriver</c> 注入(与 <see cref="ICasebookDtoSource"/> 同款)。</para>
    /// <para>⚠️ 本接口是 <c>ADR-013 §四 Key Interfaces</c> 的**首个出向契约**(§四 原有接口
    /// 全为入向:<c>Bind</c> / <c>SetFocusGate</c>)—— 登记见 sprint-08 S1.1。</para>
    /// <para><b>守密</b>:只传 <c>lexicon_id</c>(42 结构上不见 <c>disease_id</c>,AC-37-15);
    /// 语义动作只暴露「指认了哪条词 + 循环到哪一档」,不暴露病人 / 病种。</para>
    /// </remarks>
    public interface ICasebookCommandSink
    {
        /// <summary>病名栏落笔 / 指认(病名册条目被 Submit)。</summary>
        /// <param name="lexiconId">被指认的词条 id(<b>显式 lexicon_id,非册内显示索引</b>
        /// —— TD 第五风险:显示序 ≠ lexicon_id 序时用索引作代理会写错词)。
        /// <para>调用方 = 42 病名册条目行;值来自 <see cref="ICasebookDtoSource.SeenLexiconIds"/>。</para>
        /// </param>
        /// <remarks>实现方(装配层)据当前档位决定写 <c>JudgmentRecorded</c>(首落)
        /// 还是 <c>JudgmentRevised</c>(改判),并推进档位循环(空 → 疑似 → 确定 → 擦除)。</remarks>
        void SubmitDiseaseName(int lexiconId);

        /// <summary>病名栏再次落笔(同栏重复 Submit ⇒ 档位循环;<c>OQ-CB-5</c> 甲)。</summary>
        /// <remarks>语义 = 「同一条已指认的词,再落一笔」—— 空 → 疑似 → 确定 → 擦除回空,
        /// 恰三态(<c>S-8.3</c>),零第四档。实现方**不产独立流事件**:档位仅作为
        /// <c>judgment.confidence: u8</c> 随落笔 / 改判事件载荷写入。</remarks>
        void SubmitConfidence();
    }
}
