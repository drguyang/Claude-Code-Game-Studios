// 权威来源:production/epics/audio-system/story-018-runtime-audio-assembly.md(AC-018-1)
//   · A-2「`ICueRenderTarget` 只有 `NullRenderTarget`」—— 本类即真实现。
// ADR-028 ①(世界语境声源池**唯一所有者 = 44**)· ⑤(cue 驱动生命周期)
// ADR-018 §一(44 只渲染,不持游戏状态)· §二(`AudioCueDto` 唯一入口)
// GDD design/gdd/audio-system.md F-44.7(:619 同格 N 病人 = N 条独立声源,不合并)
//
// ⚠️ **本类不持句柄簿记** —— 句柄唯一性 / 同源替换 / 过期忽略全归 `CueRenderSink`
//   (它是 `IAudioCueSink` 的语义实现)。本类只做「44 的渲染动作 → 引擎对象」的翻译:
//   `PlayOneShot` → 池取源 + 载 clip + `PlayOneShot`;
//   `StartLoop` / `StopLoop` → 池取源 + `clip` + `Play` / `Stop`。
//   ⇒ 句柄 id 在本类只作**循环表的键**,不产生任何语义。
//
// ⚠️ **clip 解析与播放分离**:clip 解析走**注入的委托** `Func<int, AudioClip>`(素材装载归
//   Addressables 装配面;本类不引 `Addressables`,以便 EditMode 用假件直驱「cue → 池动作」这一半)。
//   ⚠️ **为什么是委托而不是自定义接口**:本命名空间的类型受 **AC-44-B3 入口门**约束 ——
//   `unity/Assets/Tests/EditMode/Audio/assembly_boundary_test.cs::ScanEntryPoints` 断言
//   「44 的类型实现的项目接口 ⊆ {IAudioCueSink} ∪ EntryInterfaceAllowlist」。曾用
//   `IClipProvider` 接口实现,该门**逐条判红**(2026-10-18 实测 3 条);改委托后零门面扩张
//   (委托不是「实现的项目接口」),也不必往白名单里加条目 —— 白名单每加一条都在削弱该门。
//   ⚠️ 解析不到 clip ⇒ **不静默**:记一次 miss 并计数(可观测),但不抛 —— 播放面不能因
//   一条 cue 缺素材炸掉整帧(素材存在性已由构建期 D6 门拦下,运行期 miss = 装配错)。
//
// ⚠️ **强度 / 距离 / 遮挡**不在本类面内:`Intensity` 的增益映射归 `VoiceVariantLib`
//   (13 的语义侧),空间化归 `SpatializationDirector`。本类只保证「有声源、有 clip、在响」。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;
using UnityEngine;

namespace DaYiJingCheng.Gameplay.Presentation.Audio
{
    /// <summary><c>ICueRenderTarget</c> 的**生产实现**(A-2):把 cue 渲染动作翻译成
    /// <see cref="AudioSourcePool"/> 上的真实播放。</summary>
    /// <para>⚠️ 只做翻译,零游戏状态(ADR-018 §一);句柄语义全归 <see cref="CueRenderSink"/>。</para>
    /// <example><code>var target = new AudioSourceRenderTarget(pool, cue => clipOf(cue), busForCue);
    /// var sink = new CueRenderSink(target);</code></example>
    public sealed class AudioSourceRenderTarget : ICueRenderTarget
    {
        private readonly AudioSourcePool _pool;
        private readonly Func<int, AudioClip> _clipOf;
        // story-020:按**整条 cue**(含声源实体 / 强度)解析 —— 变体选择要用到 Source 作患者身份。
        private readonly Func<AudioCueDto, AudioClip> _cueClipOf;
        private readonly Func<int, string> _busForCue;

        // S15-4(TD-audio-028):空间化链。**同一池**共享(AC-018-4 单池不变量)——
        // 位置换算 / 格内偏移 / 排名全归它;本类只在 spawn 之后把源交给它定位。
        // null ⇒ 退回**旧形态**(不写 transform.position)—— 只为既有 62 处无音频缝的
        // EditMode 调用保持行为;生产路径(组合根)恒非 null。
        private readonly SpatializationDirector _spatial;
        private readonly Func<Int3, Int3, bool> _occludedFor;
        private Func<Int3> _listenerCell;

        /// <summary>事件表(空间化四列的**真源**)。null = 未注入 ⇒ 四列全走代码常量兜底。
        /// <para>🔁 2026-10-29(`TD-audio-035` ③)。走 <see cref="SetTable"/> 后注入而不是构造参数:
        /// 62 处既有 EditMode 调用不传表,加构造参数会全部编译不过;而「未注入 ⇒ 兜底」
        /// 本身就是设计内的降级面(与 `AudioAssemblySeams.HasAny` 同款可观测)。</para></summary>
        private AudioEventDataSet? _table;

        /// <summary>听者变换(世界坐标读它)。⚠️ 由装配层注入 —— 44 **不自己 `Find` 相机**
        /// (ADR-020 §七 的单挂点归 Boot;此处只读)。null ⇒ 距离记 0(排名退到 cell_jitter 键)。
        /// <para>⚠️ 持 `Transform` 而**不是** <c>Vector3</c>:AC-44-B4 的字段类型白名单把
        /// `UnityEngine.Vector3` 判为「游戏状态」(float 世界坐标),而 `UnityEngine.Object`
        /// 子类是允许的(它是渲染资源引用,不是坐标值)。**门是对的**,不该为它开口子 ——
        /// 持引用也顺带解决了「快照会过期」:位置在用时才读。</para></summary>
        private Transform _listener;

        /// <summary>本类写入空间化锚点的**次数**(可观测面:> 0 证明位置真的被写)。
        /// <para>⚠️ 与 `ClipMissCount` 同性质 —— 判「空间化接没接」看它,不看代码里有没有那行。</para></summary>
        public int SpatializedCount { get; private set; }

        /// <summary>空间化锚点用的格边长(米)。⚠️ **必须与 director 的那一个是同一个值**,
        /// 否则本类算的距离与 director 写进 `transform` 的坐标分属两套比例。
        /// 未接空间化 ⇒ 0(本值不被使用)。</summary>
        public float LatticeSize => _spatial != null ? _latticeSize : 0f;
        private readonly float _latticeSize;

        // 循环句柄 id → 已占用的 actorId。循环 cue 的 actor = 句柄 id 的**负偏移**
        // (一次性 cue 用 cue.Source,循环用独立命名空间 —— 两者不会撞池条目)。
        // ⚠️ 负号是**键空间分隔**,不是语义:池只把 actorId 当字典键。
        private readonly Dictionary<int, int> _loopActors = new Dictionary<int, int>();

        /// <summary>clip 解析失败次数(可观测面;运行期 miss = 装配错,构建期 D6 门本应拦下)。</summary>
        public int ClipMissCount { get; private set; }

        /// <summary>构造。</summary>
        /// <param name="pool">声源池(非 null)。</param>
        /// <param name="clipOf">cue ordinal → clip 的解析缝(null ⇒ **恒 miss** —— 相当于
        /// 「总线路由可验、clip 面未装配」,每次调用记一次 <see cref="ClipMissCount"/>,可观测不静默)。</param>
        /// <param name="busForCue">cue ordinal → bus 名(事件表 `bus` 列;null = 不挂组)。
        /// <para>⚠️ 传函数而不是字典 —— 事件表是**唯一真源**(ADR-024 §⑥),本类不缓存第二份。</para></param>
        /// <exception cref="ArgumentNullException"><paramref name="pool"/> 为 null。</exception>
        public AudioSourceRenderTarget(AudioSourcePool pool, Func<int, AudioClip> clipOf = null,
                                       Func<int, string> busForCue = null)
        {
            _pool = pool ?? throw new ArgumentNullException(nameof(pool));
            _clipOf = clipOf;
            _busForCue = busForCue;
        }

        /// <summary>按**整条 cue** 解析 clip 的构造(story-020 —— 生产路径用这条)。
        /// <para>为什么需要整条:变体选择键含**患者身份**(<c>AudioCueDto.Source</c>),
        /// 只给 ordinal 无法区分「同一 cue 的两个病人」。见
        /// <c>AudioClipLibrary.ResolveCue</c>。</para></summary>
        /// <param name="pool">声源池(非 null)。</param>
        /// <param name="cueClipOf">cue → clip 的解析缝(null ⇒ 恒 miss,可观测)。</param>
        /// <param name="busForCue">cue ordinal → bus 名。</param>
        public AudioSourceRenderTarget(AudioSourcePool pool, Func<AudioCueDto, AudioClip> cueClipOf,
                                       Func<int, string> busForCue = null)
        {
            _pool = pool ?? throw new ArgumentNullException(nameof(pool));
            _cueClipOf = cueClipOf;
            _busForCue = busForCue;
        }

        /// <summary>接空间化链的构造(S15-4 · TD-audio-028 的接线面)。
        /// <para>与上两个构造的区别**只是** spawn 之后多一步:把源交给
        /// <see cref="SpatializationDirector"/> 按 `cue.Cell` 定位 + 按 `occlusion_lowpass`
        /// 施加遮挡。clip / 总线解析与已有一致。</para></summary>
        /// <param name="pool">声源池(非 null;**必须与 <paramref name="spatial"/> 是同一个**)。
        /// <para>⚠️ 违反本约束的形态(existing 池 ≠ director 的池)= 两个根 /
        /// 定位写进 A 池而播放从 B 池取 ⇒ 接线的观测面可能绿而声音仍在原点。</para></param>
        /// <param name="spatial">空间化导演(非 null;与 <paramref name="pool"/> 共享实例)。</param>
        /// <param name="latticeSize">格边长(米)—— **与 director 构造函数收到的是同一个值**。</param>
        /// <param name="cueClipOf">cue → clip 的解析缝。</param>
        /// <param name="busForCue">cue ordinal → bus 名。</param>
        /// <param name="occludedFor">遮挡判定缝:**声源格 + 听者格 → 是否被挡**。
        /// <para>🔁 **2026-10-29(`TD-audio-035` ②,用户裁定甲案)** —— 原签名是 `Func<bool>`,
        /// **无格入参、且只在 `Spawn` 求值一次** ⇒ 即便有了遮挡几何也表达不出「听者到**这一条**
        /// 声源是否被挡」,更不随听者移动更新。现改为双格签名,由装配层注入
        /// `Gameplay.Boot.LatticeOcclusionProbe`(整数格视线走查,**不用物理射线** ——
        /// 理由见该类头注)。</para>
        /// <para>null ⇒ 恒 false(未接遮挡链;`OcclusionEvaluations` 仍计数,可观测)。</para></param>
        /// <exception cref="ArgumentNullException"><paramref name="pool"/> / <paramref name="spatial"/> 为 null。</exception>
        /// <remarks>⚠️ **听者格不在构造里收** —— 相机在启动序**更晚**才装配(BootRoot 第 5 步),
        /// 而渲染目标在组合根装配期更早创建 ⇒ 构造期给它会是 null。与
        /// <see cref="SetListenerPosition"/> 同一理由、同一模式:走 `SetListenerCell` 后注入。</remarks>
        public AudioSourceRenderTarget(AudioSourcePool pool, SpatializationDirector spatial,
                                       float latticeSize, Func<AudioCueDto, AudioClip> cueClipOf,
                                       Func<int, string> busForCue = null,
                                       Func<Int3, Int3, bool> occludedFor = null)
        {
            _pool = pool ?? throw new ArgumentNullException(nameof(pool));
            _spatial = spatial ?? throw new ArgumentNullException(nameof(spatial));
            if (!(latticeSize > 0f))
                throw new ArgumentOutOfRangeException(nameof(latticeSize), latticeSize,
                    "格边长必须 > 0(与 LatticeToWorld.ToWorld 的守卫同口径)");
            _latticeSize = latticeSize;
            _cueClipOf = cueClipOf;
            _busForCue = busForCue;
            _occludedFor = occludedFor;
        }

        private AudioClip ClipOf(in AudioCueDto cue)
            => _cueClipOf != null ? _cueClipOf(cue)
             : _clipOf != null ? _clipOf(cue.Cue) : null;

        /// <summary>当前在响的循环数。</summary>
        public int ActiveLoopCount => _loopActors.Count;

        /// <summary>一次性 cue 的归还推进(S15-5 · TD-audio-029)。**由帧泵每帧调用**(BootRoot)。
        /// <para>本类只做**执行**:让池归还已判完的源(探针面 / 兜底窗口 / 帧泵的 <see cref="AudioSourcePool.MarkFinished"/>)。
        /// 「谁观测播完」住 <see cref="Gameplay.Boot.OneShotReclaimPump"/> —— 它只依赖池,
        /// 故此缝在无渲染目标(`NullRenderTarget` 链)时同样可跑。</para>
        /// <para>可被反复调用:零分配、无状态。</para></summary>
        /// <returns>本帧归还的源数。</returns>
        public int Pump() => _pool.ReclaimExpired();

        /// <inheritdoc/>
        public void PlayOneShot(in AudioCueDto cue)
        {
            AudioClip clip = ClipOf(cue);
            if (clip == null) { ClipMissCount++; return; }

            // 一次性 cue 的 actor 键 = 声源实体 id(病人 / 玩家);同实体复播复用同一源。
            // ⚠️ **播完即归还**(TD-audio-029):排队后由 Pump + 池外观测归还 ——
            //    没有这一步,池的闲置栈永不回流,每次 one-shot 都新建源。
            AudioSource source = _pool.Spawn(cue.Source, cue.Cell.X, cue.Cell.Y, cue.Cell.Z,
                                             cue.Cue, BusOf(cue.Cue));
            Place(in cue, cue.Source);
            source.clip = clip;
            source.loop = false;
            source.Play();
            _pool.EnqueueOneShot(cue.Source, source);
        }

        /// <inheritdoc/>
        public void StartLoop(in AudioCueDto cue, int handleId)
        {
            AudioClip clip = ClipOf(cue);
            if (clip == null) { ClipMissCount++; return; }

            int actor = LoopActorOf(handleId);
            _loopActors[handleId] = actor;

            AudioSource source = _pool.Spawn(actor, cue.Cell.X, cue.Cell.Y, cue.Cell.Z,
                                             cue.Cue, BusOf(cue.Cue));
            // ⚠️ 传 **actor**(池键),不是 `cue.Source` —— 循环源的池键是 `LoopActorOf(handleId)`
            //    (负偏移),而 `SpatializationDirector.Spawn` 内部**再次**用 `cue.Source` 取池条目。
            //    传 cue.Source 会把位置写到**另一条源**上(通常是 null / 别人的源),
            //    而真正在响的循环源留在原点 —— 症状是「呼吸层听着不随病人走」。
            Place(in cue, actor);
            source.clip = clip;
            source.loop = true;
            source.Play();
        }

        /// <summary>把刚 spawn 的源交给空间化链定位(未接线 ⇒ 空操作,保持旧形态)。
        /// <para>⚠️ **必须在 `Play()` 之前** —— Unity 在 `Play` 时按当前 transform 建立
        /// 空间化的距离参数;先播后挪会在首帧响在原点。</para></summary>
        /// <param name="cue">原始 cue。</param>
        /// <param name="actorId">**池键**(一次性 = `cue.Source`;循环 = `LoopActorOf(handleId)`)。
        /// <para>⚠️ 两者可能不同 ⇒ 交给导演前须把 `Source` 重映射成池键:
        /// <see cref="SpatializationDirector.Spawn"/> 内部用 `cue.Source` 取池条目,
        /// 传原 cue 会让位置写到**另一条源**上,而真正在响的循环源留在原点。</para></param>
        private void Place(in AudioCueDto cue, int actorId)
        {
            if (_spatial == null) return;
            AudioCueDto anchored = actorId == cue.Source
                ? cue
                : new AudioCueDto(cue.Cue, cue.Intensity, cue.Cell, actorId, cue.Looped);
            _spatial.Spawn(anchored, PriorityClassOf(cue.Cue), IsOccluded(cue.Cell),
                           OcclusionLowpassOf(cue.Cue), DistanceFromListener(cue.Cell));
            SpatializedCount++;
        }

        /// <summary>遮挡判定的**求值次数**(可观测面 —— `> 0` 证明遮挡链真的被问过,
        /// 不是「代码里有个 `occludedFor` 字段」)。</summary>
        /// <remarks>🔁 2026-10-29(`TD-audio-035` ②):与 `SpatializedCount` 同性质。
        /// ⚠️ 未接缝(`_occludedFor == null`)时**不计数** —— 那时「遮挡恒 false」是**未装配**,
        /// 不是「判了没挡」,两者不可混为一谈。</remarks>
        public int OcclusionEvaluations { get; private set; }

        /// <summary>声源格相对听者格是否被挡(遮挡缝的双格求值,**每源每次 Spawn 一次**)。
        /// <para>⚠️ 听者格由 `listenerCell` 缝给出;未给 ⇒ 以**声源格自身**为听者格 ⇒
        /// 零长视线 ⇒ 恒 `false`(等价于旧行为,且**不伪造**一个「听者在某处」的假设)。</para></summary>
        private bool IsOccluded(in Int3 sourceCell)
        {
            if (_occludedFor == null) return false;
            Int3 listener = _listenerCell != null ? _listenerCell() : sourceCell;
            OcclusionEvaluations++;
            return _occludedFor(listener, sourceCell);
        }

        /// <summary>遮挡低通的**兜底值**(Hz)。🔁 2026-10-29(`TD-audio-035` ③):正式值已改由
        /// 事件表 `occlusion_lowpass` 列承载(全 19 行),本常量只在「表未注入 / 行缺列」时生效。
        /// <para>⚠️ 取 1200 的理由不再是「照抄 WorldBreath 行」,而是**与表内现值同值** ——
        /// 生产语义下它就是表给的值,把它当「44 的裁定值」读 = 第二真源。</para></summary>
        private const float OcclusionLowpassHz = 1200f;

        /// <summary>cue → 遮挡低通截止(Hz)。表未注入 / 行缺列 / 值非法 ⇒ 回落
        /// <see cref="OcclusionLowpassHz"/> 并计 <see cref="SpatialParamFallbacks"/>。</summary>
        private float OcclusionLowpassOf(int cue)
        {
            AudioEventRow? row = RowOf(cue);
            if (row.HasValue)
            {
                string raw = row.Value.OcclusionLowpassRaw;
                if (raw != null && int.TryParse(raw, out int hz) && hz > 0) return hz;
            }
            SpatialParamFallbacks++;
            return OcclusionLowpassHz;
        }

        /// <summary>注入听者变换(装配层在相机装好之后调用)。
        /// <para>为什么不搬进构造:相机在启动序**更晚**才装配(BootRoot 第 5 步),
        /// 而渲染目标更早创建 ⇒ 构造期给它会是 null。</para></summary>
        /// <param name="listener">听者的 <c>Transform</c>(通常 = 主相机;null ⇒ 距离记 0)。</param>
        public void SetListenerPosition(Transform listener) => _listener = listener;

        /// <summary>注入听者**当前格**的读取缝(遮挡判定用;装配层在相机装好之后调用)。
        /// <para>🔁 2026-10-29(`TD-audio-035` ②)。与 <see cref="SetListenerPosition"/> 同一理由 ——
        /// 相机在启动序更晚才装配,构造期给它会是 null。</para>
        /// <para>⚠️ 传格而不是 `Transform`:44 的遮挡输入必须是**整数格**(ADR-015 §一 两层纪律),
        /// 装配层负责把相机位置换成格,44 不读表现态 float。</para></summary>
        /// <param name="cell">返回听者当前格的委托(null ⇒ 以声源格自身为听者格 ⇒ 恒不遮挡)。</param>
        public void SetListenerCell(Func<Int3> cell) => _listenerCell = cell;

        /// <summary>注入事件表(空间化四列的真源)。未注入 ⇒ 四列全走 `AudioTuning` 兜底。
        /// <para>🔁 2026-10-29(`TD-audio-035` ③)。与 <see cref="SetListenerCell"/> 同款「后注入」模式
        /// —— 62 处既有 EditMode 调用不传表,加构造参数会全部编译不过。</para></summary>
        /// <param name="table">事件表(`AudioAssemblySeams.Table` 的同一个实例)。</param>
        public void SetTable(AudioEventDataSet table) => _table = table;

        /// <summary>声源格到听者的**世界距离**(米)。未注入听者 ⇒ 0。</summary>
        /// <remarks>⚠️ 用 director **同一个** <see cref="LatticeSize"/> 换算,并经
        /// <see cref="LatticeToWorld"/> 这唯一入口 —— 不在这里重写一次乘法(那会是第二真源)。</remarks>
        private float DistanceFromListener(Int3 cell)
        {
            if (_listener == null) return 0f;
            Vector3 world = LatticeToWorld.ToWorld(cell.X, cell.Y, cell.Z, _latticeSize);
            return Vector3.Distance(world, _listener.position);
        }

        /// <summary>cue → 优先级类别(GDD `:863` 类序:自身/检查 0 &lt; 世界≈生理 1 &lt; 氛围 2)。
        /// <para>🔁 **2026-10-29(`TD-audio-035` ③)**:由**查表**取代代码常量。原实现恒返回
        /// <see cref="AudioTuning.PriorityClassWorldOrPhysiology"/> ⇒ 所有 cue 同档 ⇒
        /// `rank_key` 的第二、三键退化为纯距离 + jitter。
        /// 现读事件表行级 `priority_class`(全 19 行已承载,构建期门 `ValidateSpatialColumns`
        /// 断言每行必带 + 值 ∈ {0,1,2})。</para>
        /// <para>⚠️ **缺列仍回落代码常量**:`_table` 未注入(62 处 EditMode 夹具)或行缺列时
        /// 返回兜底值 —— 这与「表给了值」在 <see cref="SpatialParamFallbacks"/> 上可分。</para></summary>
        private int PriorityClassOf(int cue)
        {
            if (_table.HasValue)
            {
                AudioEventDataSet t = _table.Value;
                if (cue >= 0 && cue < t.RowCount)
                {
                    string raw = t.Row(cue).PriorityClassRaw;
                    if (raw != null && int.TryParse(raw, out int parsed)) return parsed;
                }
            }
            SpatialParamFallbacks++;
            return AudioTuning.PriorityClassWorldOrPhysiology;
        }

        /// <summary>空间化四列的**兜底次数**(可观测面:&gt; 0 证明「有列没查到、退回了代码常量」)。
        /// <para>🔁 2026-10-29(`TD-audio-035` ③)。与 `OcclusionEvaluations` 同性质 ——
        /// 判「表列真的被消费」看它,不看代码里有没有那一行。</para></summary>
        public int SpatialParamFallbacks { get; private set; }

        /// <summary>cue → 该行的空间化列原文(未注入表 / 越界 ⇒ null)。</summary>
        private AudioEventRow? RowOf(int cue)
        {
            if (!_table.HasValue) return null;
            AudioEventDataSet t = _table.Value;
            return cue >= 0 && cue < t.RowCount ? t.Row(cue) : (AudioEventRow?)null;
        }

        /// <summary>改循环 cue 的渲染增益(尾音 W-1 接线 · 2026-10-20)。
        /// <para>等价物 = 该循环 <c>AudioSource.volume</c>(线性 0..1);未知 / 已停句柄 ⇒ 返回 false
        /// (不抛 —— 与 <see cref="StopLoop"/> 的「过期句柄空操作」同口径)。
        /// 它是 <see cref="ICueRenderTarget.SetLoopGain"/> 的实现,不是第二入口:
        /// 上游只能经 <see cref="CueRenderSink.SetLoopGain"/> 到达。</para></summary>
        /// <param name="handleId">44 分配的循环句柄 id。</param>
        /// <param name="gain">线性增益(非有限 / ≤0 / ≥1 者在 sink 侧已被拒)。</param>
        /// <returns>该句柄在响时写入 ⇒ true。</returns>
        public bool SetLoopGain(int handleId, float gain)
        {
            if (!_loopActors.TryGetValue(handleId, out int actor)) return false;
            AudioSource source = _pool.SourceForActor(actor);
            if (source == null) return false;
            source.volume = gain;
            return true;
        }

        /// <inheritdoc/>
        public void StopLoop(int handleId)
        {
            // 过期句柄 ⇒ 空操作(与 CueRenderSink 的「过期 EndLoop 忽略」同口径)。
            if (!_loopActors.TryGetValue(handleId, out int actor)) return;
            _loopActors.Remove(handleId);
            _pool.Release(actor);
        }

        private string BusOf(int cue)
        {
            if (_busForCue == null) return null;
            string bus = _busForCue(cue);
            return string.IsNullOrEmpty(bus) ? null : bus;
        }

        /// <summary>循环句柄 id → 池 actor 键(负偏移 = 与一次性 cue 的实体 id 分居两个键空间)。</summary>
        private static int LoopActorOf(int handleId) => -handleId - 1;
    }
}
