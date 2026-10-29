// 权威来源:production/epics/audio-system/story-008-spatialization-world-breath.md(AC-44-D8 ②)
// ADR-028 ①②⑤(世界语境声源池**唯一所有者 = 44**;零场景预摆;生命周期 cue 驱动)
// GDD:design/gdd/audio-system.md F-44.7(:619「同格两病人经 LatticeToWorld 得同一 float 锚点
//    ⇒ distance 相等 ⇒ 由 cell_jitter 分出主次」)· AC-44-D8 ②「同 cell N 病人 = N 条独立 AudioSource」
//
// ⚠️ **ADR-028 ②「零场景预摆」**:`AudioSource` 作为非 Boot 场景内容 = **构建失败**。
//    本池的 root GameObject 是**运行期产物**(构造时创建,非场景资产),故不触该条 ——
//    池**自建隐藏 root**,全部源挂其下;`Dispose()` 销毁 root 即销毁全部源。
// ⚠️ **同 cell N 源不合并**(GDD :619 / ADR-028 ⑤ 逐字)—— 每个 ActorId 一条**独立**
//    `AudioSource`,哪怕 Cell 完全相同。合并会让「同格 N 病人」退化成一条声。
// ⚠️ **存在性不进三流**(ADR-028 ⑤):池的状态是纯表现层,零 `IEventSink` 调用。

using System;
using System.Collections.Generic;
using UnityEngine;

namespace DaYiJingCheng.Gameplay.Presentation.Audio
{
    /// <summary>44 拥有的**世界语境声源池**(ADR-028 ① 唯一所有者)。
    /// <para>职责:创建 / 复用 / 释放承载 3D 空间化的 <c>AudioSource</c>。同 cell N 源
    /// **不合并**(AC-44-D8 ②)。</para>
    /// <para>⚠️ 本类**不持有游戏状态** —— 只有「哪些声源正在响」这一呈现层事实
    /// (承 ADR-018 §一「44 只触发 / 只渲染」)。</para>
    /// <para>⚠️ **生命周期(S15-5 · TD-audio-029)**:循环源由持有者在 <see cref="Release"/> 归还;
    /// **一次性源**没有持有者 ⇒ 经 <see cref="EnqueueOneShot"/> 排队,被观测到播完后由
    /// <see cref="MarkFinished"/> / <see cref="ReclaimExpired"/> 归还。两条路都回到同一闲置栈
    /// ⇒ 池内条目数**有界于并发**而非**累计**。</para>
    /// <example>
    /// var pool = new AudioSourcePool();
    /// AudioSource src = pool.Spawn(actorId: 7, cellX: 5, cellY: 0, cellZ: 5, cueId: 10);  // 循环
    /// AudioSource one = pool.Spawn(actorId: 8, cellX: 5, cellY: 0, cellZ: 5, cueId: 10);
    /// pool.EnqueueOneShot(actorId: 8, one);   // 一次性:排队待归还
    /// // ... 播放中(每帧)...
    /// pool.MarkFinished(8);    // 观测面:该源已不在播
    /// pool.ReclaimExpired();   // 归还可回收的一次性源
    /// pool.Release(7);         // 循环归还
    /// pool.Dispose();          // 销毁 root 与全部源
    /// </example></summary>
    public sealed class AudioSourcePool : IDisposable
    {
        /// <summary>池自建 root 的默认名(层级辨识用)。</summary>
        public const string RootName = "AudioSourcePool";

        /// <summary>一次性 cue 的**兜底**到期窗口(秒)—— **远大于任何素材**,只为保证不提前掐断。
        /// <para>⚠️ 它不是数值旋钮:正常回收走观测面(<see cref="MarkFinished"/> / 探针),
        /// 本窗口只在观测面全部沉默时兜底。**池不学时长**(它不持 clip;学时长 = 池外的事)。</para></summary>
        public const float UnobservedOneShotWindowSeconds = 3600f;

        private readonly GameObject _root;
        private readonly Dictionary<int, Entry> _entries = new Dictionary<int, Entry>();
        private readonly Stack<AudioSource> _free = new Stack<AudioSource>();
        private bool _disposed;

        // ── A-6(story-018):总线路由(运行期 `outputAudioMixerGroup`,见 AudioBusResolver 头注)──
        // null = 未装配混音器 ⇒ 源落 Master 直通(与装配前行为一致;由 AudioBusResolver 的
        // 构造期全量校验保证「装配了就一定全可解析」)。
        private readonly AudioBusResolver _busResolver;

        // ── 一次性 cue 的到期回收(S15-5 · TD-audio-029)─────────────────────
        // 一次性 cue 没有上游句柄、没有自评兜底的持有者(ADR-001 §一之三 只管循环)
        // ⇒ **池是它的唯一持有者**。但池**不读素材时长**(池不是素材装载面,不持 clip),所以
        // 「播完」只能来自**池外的观测**,池只负责把「已播完」落成归还。两条观测面:
        //   ① `MarkFinished(actorId)`(显式):池外持有者报「该源已不在播」(生产 = Boot 帧泵
        //      轮询 `AudioSource.isPlaying`)。
        //   ② `EnqueueOneShot(..., standaloneProbe)`(探针):池外持有者**判活** —— 探针
        //      (通常 = 该源所在 GameObject)**永不入池**,其生命周期由池外决定;
        //      判活为 false ⇒ 视为播完。两条观测面**任一**成立即归还。
        // ⚠️ 判据是「观测到播完」,**不是「估出一个到期时刻」** —— 池里没有任何时长估算;
        //    兜底窗口(远大于任何素材)只为「两条观测面都沉默」不无限泄漏。
        private readonly Func<float> _clock;
        private int _oneShotPending;
        // 「刚排队」保护 = `Entry.JustQueued`:排队后的**第一次** `ReclaimExpired` 扫描跳过它,
        // 该次扫描顺手清除标记(下一轮起可回收)。⇒「排队 → 同帧回收」不会掐掉刚开播的源,
        // 而「排队 → 回收 → 观测 → 回收」两段式(Pump 的形态)在第二段即可归还。

        private readonly List<int> _reclaimScan = new List<int>();          // 回收扫描(复用 buffer)
        private List<int> _pendingSnapshot;                                 // PendingOneShotActors 快照

        private sealed class Entry
        {
            public AudioSource Source;
            public int CellX, CellY, CellZ;
            public int CueId;
            public string Bus;
            public bool Active;
            public int OneShotId;              // 0 = 非一次性(循环 / 默认 spawn 不走回收)
            public double EndsAtWallSeconds;   // 兜底窗口的到期时刻(只在观测面沉默时生效)
            public UnityEngine.Object StandaloneProbe;   // 池外持有者(观测面 ②;null = 无)
            public bool JustQueued;            // 排队后尚未经过一次回收扫描(该次扫描跳过它)
        }

        /// <summary>建池(创建隐藏 root)。</summary>
        /// <param name="rootParent">root 的父节点;null = 场景根。
        /// ⚠️ 传非 null 会让池成为**场景内容**的一部分 —— 生产装配**不得**传场景对象
        /// (ADR-028 ②);该参只供测试隔离用。</param>
        /// <param name="busResolver">总线解析器(A-6;null = 未装配混音器,源落 Master 直通)。
        /// <para>⚠️ 传 null 是**装配前**的合法态,不是降级 —— 一旦装配就必然全量可解析
        /// (见 <see cref="AudioBusResolver"/> 构造期校验)。</para></param>
        /// <param name="clock">一次性 cue 到期回收用的时钟(秒);null = `AudioSettings.dspTime`
        /// (生产 / EditMode 通用的音频时钟 —— 不用 unscaledTime,后者在暂停时仍走而音频不走)。</param>
        public AudioSourcePool(Transform rootParent = null, AudioBusResolver busResolver = null,
                               Func<float> clock = null)
        {
            _busResolver = busResolver;
            _clock = clock ?? DefaultClock;
            _root = new GameObject(RootName);
            // DontSave:池是运行期产物,不进场景序列化、不被保存 —— 与 ADR-028 ②「零场景预摆」同向。
            // ⚠️ 刻意**不**加 HideInHierarchy —— 池在调试期须可在层级里查到;
            //    「零场景预摆」由「运行期创建 + DontSave」保证,与层级可见性无关。
            _root.hideFlags = HideFlags.DontSave;
            if (rootParent != null) _root.transform.SetParent(rootParent, worldPositionStays: false);
        }

        /// <summary>root 对象(测试 / 装配定位用;只读)。</summary>
        public GameObject Root => _root;

        /// <summary>当前**在响**的声源数(释放后不计)。</summary>
        public int ActiveCount
        {
            get
            {
                int n = 0;
                foreach (KeyValuePair<int, Entry> kv in _entries)
                    if (kv.Value.Active) n++;
                return n;
            }
        }

        /// <summary>为 <paramref name="actorId"/> 取一条声源(生或复用)。</summary>
        /// <param name="actorId">声源实体 id。</param>
        /// <param name="cellX">锚点格 X。</param>
        /// <param name="cellY">锚点格 Y。</param>
        /// <param name="cellZ">锚点格 Z。</param>
        /// <param name="cueId">语义 cue id(池只记录,播放归装配层)。</param>
        /// <returns>该 actor 独占的 <c>AudioSource</c>(**不同 actorId 恒不同实例**)。</returns>
        /// <remarks>⚠️ 格以**三轴 int** 承载而非 <c>Int3</c> —— 44 的公开方法签名受 AC-44-B3
        /// 入口白名单约束(`assembly_boundary_test` b3):状态入口仅 <c>AudioCueDto</c>。
        /// 池不参与状态语义,故拆轴;换算归 <see cref="SpatializationDirector"/>。
        /// 世界坐标写入 <c>transform</c> 由装配层经 <see cref="LatticeToWorld"/> 完成。</remarks>
        public AudioSource Spawn(int actorId, int cellX, int cellY, int cellZ, int cueId)
            => Spawn(actorId, cellX, cellY, cellZ, cueId, bus: null);

        /// <summary>为 <paramref name="actorId"/> 取一条声源,并按 <paramref name="bus"/> 挂混音组(A-6)。
        /// <para>⚠️ <b>bus 归属在「建源」时决定</b>(承本 story 边界裁定 3):同一条池内源被不同 bus
        /// 的 cue 复用时,**重挂**而非重建 —— 池的复用语义(不销毁)不变。</para>
        /// <para>⚠️ <paramref name="bus"/> = null ⇒ 不挂组(落 Master 直通);**非 null 且未装配
        /// 解析器 / 名字不在注册集 ⇒ 抛**(禁静默落 Master,见 <see cref="AudioBusResolver"/>)。</para></summary>
        /// <param name="actorId">声源实体 id。</param>
        /// <param name="cellX">锚点格 X。</param>
        /// <param name="cellY">锚点格 Y。</param>
        /// <param name="cellZ">锚点格 Z。</param>
        /// <param name="cueId">语义 cue id(池只记录,播放归装配层)。</param>
        /// <param name="bus">事件表 `bus` 列(`MixerRegistry.BusNames` 之一;null = 不挂组)。</param>
        /// <exception cref="ArgumentException"><paramref name="bus"/> 非 null 但解析不到组。</exception>
        public AudioSource Spawn(int actorId, int cellX, int cellY, int cellZ, int cueId, string bus)
        {
            ThrowIfDisposed();
            return SpawnCore(actorId, cellX, cellY, cellZ, cueId, bus);
        }

        /// <summary>把一条**一次性** cue 的源记成「待归还」(S15-5 · TD-audio-029)。
        /// <para>一次性 cue 没有持有者,池是它的唯一持有者 ⇒ 没有这一步,闲置栈永不回流、
        /// 已播完静音的源仍计入「在响」。归还由 <see cref="ReclaimExpired"/> 在<b>观测到播完</b>
        /// 后执行。<b>观测面两条,任一成立即算播完</b>:</para>
        /// <list type="number">
        /// <item><b>显式面</b>:<see cref="MarkFinished"/> —— 池外持有者(渲染目标)报「不在播了」。</item>
        /// <item><b>存活探针面</b>:<paramref name="standaloneProbe"/> 是**池外**的对像,
        /// **永不入池** ⇒ 「池外持有者已把它交给 Unity」时池判活为 false ⇒ 视为播完。
        /// 这给出「一次性源由外部负责归还」这一假设的**可判落点**,而不必在池里读素材时长。</item>
        /// </list>
        /// <para>兜底还有一个**上限窗口**(远大于任何素材):两条观测面都没报上来时仍会归还,
        /// 但它不承担正常回收。</para></summary>
        /// <param name="actorId">声源实体 id(池键)。</param>
        /// <param name="source">该 actor 当前的源(<see cref="SourceForActor"/>;**必须是它在池里的那一条**)。
        /// <para>⚠️ 传它而不是让本方法自己 Spawn:渲染目标的顺序必须是 Spawn → 定位 → 排队。</para></param>
        /// <param name="standaloneProbe">池外持有者的探针(通常 = 该源 GameObject 的持有者)。
        /// null = 无池外持有者(只用显式面 + 兜底窗口)。</param>
        /// <returns>该 actor 独占的 <c>AudioSource</c>(= <paramref name="source"/>)。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="source"/> 为 null。</exception>
        /// <exception cref="InvalidOperationException"><paramref name="actorId"/> 当前没有这条活动源。</exception>
        /// <remarks>探针**不默认**为源自身:`AudioSource` 本身是池的成员,拿它当探针 = 恒活 ⇒
        /// 隐式面永久沉默(不回收也不报错)。生产与测试都显式传(见 <c>OneShotReclaimPump</c>)。</remarks>
        /// <remarks>同一 actor 在归还前再次排队 ⇒ 以**最后一次**为准(重新起算),与
        /// `CueRenderSink`「同源重复以最后一次为准」同口径。</remarks>
        public AudioSource EnqueueOneShot(int actorId, AudioSource source, UnityEngine.Object standaloneProbe = null)
        {
            ThrowIfDisposed();
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (!_entries.TryGetValue(actorId, out Entry entry) || !entry.Active || entry.Source != source)
                throw new InvalidOperationException(
                    $"actor {actorId} 当前没有这条活动源 —— 排队必须紧跟它自己的 Spawn(池键错位会让归还落到别人身上)");

            _pendingSnapshot = null;
            entry.StandaloneProbe = standaloneProbe;
            entry.JustQueued = true;
            entry.EndsAtWallSeconds = (double)_clock() + UnobservedOneShotWindowSeconds;   // 兜底
            if (entry.OneShotId == 0) _oneShotPending++;   // 重复排队不重复计数
            entry.OneShotId = 1;                        // 非 0 = 待归还
            _pendingSnapshot = null;
            return source;
        }

        /// <summary>显式报告「该源的播放已结束」(<b>观测面 ①</b>)。幂等:非待归还的源 = 空操作。
        /// <para>⚠️ 本方法只收池键、不收源 ⇒ **调用方须保证它观测的就是该键名下那条源**
        /// (池的「一键一源」不变量使这一点可核:键名下同时只有一条活动源)。</para></summary>
        /// <param name="actorId">声源实体 id。</param>
        /// <returns>该源此前处于待归还 ⇒ true。</returns>
        public bool MarkFinished(int actorId)
        {
            if (!_entries.TryGetValue(actorId, out Entry e) || !e.Active || e.OneShotId == 0) return false;
            Release(actorId);
            return true;
        }

        /// <summary>归还**已观测到播完**的一次性 cue 源(循环源 / 默认 spawn 的源永不被本方法回收)。
        /// <para>判据(任一):① 池外持有者已把它交出去(探针判活为假 —— 见 <see cref="EnqueueOneShot"/>);
        /// ② 兜底窗口到期(`now ≥ EndsAtWallSeconds`)。兜底窗口**远大于任何素材**,只为兜住
        /// 「两条观测面都没报上来」的情形,不承担正常回收。</para>
        /// <para>时刻取池自己的时钟(<c>dspTime</c>;夹具注入可控时钟),故测试零真实等待。</para></summary>
        /// <returns>本次归还的源数(= 「播完即回落」的可观测面)。</returns>
        /// <remarks>⚠️ 自上次本调用以来**新排队**的源不参与本次回收(否则「排队 → 立刻回收」同帧掐断)。
        /// 「循环 / 默认 spawn 的源永不被回收」由 <see cref="Entry.OneShotId"/> 机械保证,
        /// **不靠归还后是否残留旧值** —— 每次 <see cref="SpawnCore"/> 都会重置该字段。</remarks>
        public int ReclaimExpired()
        {
            ThrowIfDisposed();

            // ① 自上次调用以来新排队的源**不参与本次回收** —— 它们刚开播,不可能已播完;
            //    不排除的话「排队 → 立刻回收」会在同一帧把源掐掉(观测面尚未来得及说话)。
            //    ⚠️ 排除集跨调用**累积**(不是「本帧清空」):观测(帧泵问 `isPlaying`)发生在两次
            //       回收**之间**,若回收先把「新排队」标记抹掉,紧接着的帧泵就会把刚开播的源交给
            //       观测面(生产上 `isPlaying` 首拍可能仍为 false —— 2026-10-18 实测:先抹标记
            //       会让探针面在零帧处直接回收新鲜源,夹具因此转红)。
            //    排除在「首次交到观测面」(<see cref="PendingOneShotActors"/> 返回)时撤销;
            //    源被回收 / 重新 Spawn 时也一并撤销。
            if (_oneShotPending == 0) return 0;

            // ② 谓词 = 「池外持有者已把它交出去」**或** 兜底窗口到期。三点注意:
            //    · 未绑探针 ⇒ **视为仍由池外持有**(不可判 ≠ 已归还 —— 噪声必须往「多留一会儿」偏);
            //    · 未绑定探针的源(理论不可达)同理不回收;
            //    · 上面 ① 的排除集不回收。
            float now = _clock();
            _reclaimScan.Clear();
            foreach (KeyValuePair<int, Entry> kv in _entries)
            {
                Entry e = kv.Value;
                if (!e.Active || e.OneShotId == 0) continue;
                if (e.JustQueued) { e.JustQueued = false; continue; }   // 本轮不碰,本轮之后才可回收
                // 显式面已在 MarkFinished 里直接归还;此处只判探针面。
                // ⚠️ 「没绑探针」与「探针已被销毁」在 Unity 里**都**满足 `probe == null` ——
                //    前者不可判(不得回收),后者才是播完。区分靠 `ReferenceEquals`(真引用空)
                //    与 Unity 重载的 `==`(已销毁也等于 null):**只有绑过、且已销毁**才算播完。
                bool bound = !ReferenceEquals(e.StandaloneProbe, null);
                bool finished = bound && e.StandaloneProbe == null;
                if (finished || e.EndsAtWallSeconds <= now) _reclaimScan.Add(kv.Key);
            }

            int n = _reclaimScan.Count;
            for (int i = 0; i < n; i++) Release(_reclaimScan[i]);
            return n;
        }

        /// <summary>待归还的一次性池键(<b>只读视图</b>;归还推进由 Boot 帧泵轮询 `isPlaying` 驱动,
        /// 而帧泵只持池、不持渲染目标 —— S15-5)。快照只在成员变动时重建。</summary>
        /// <returns>此刻处于待归还态的 actor id 序列。</returns>
        public IReadOnlyList<int> PendingOneShotActors()
        {
            ThrowIfDisposed();
            if (_pendingSnapshot == null)
            {
                // 先按**旧的**新鲜态记下谁新鲜,再统一解除:调用方按 `IsFreshOneShot` 返回时值跳过它们。
                _pendingSnapshot = new List<int>();
                foreach (KeyValuePair<int, Entry> kv in _entries)
                    if (kv.Value.Active && kv.Value.OneShotId != 0) _pendingSnapshot.Add(kv.Key);
            }
            return _pendingSnapshot;
        }


        /// <summary>当前**待归还**的一次性源数(可观测面;释放后回落)。</summary>
        public int PendingOneShotCount => _oneShotPending;

        private bool HasPending(int actorId)
            => _entries.TryGetValue(actorId, out Entry e) && e.Active && e.OneShotId != 0;

        private AudioSource SpawnCore(int actorId, int cellX, int cellY, int cellZ, int cueId, string bus)
        {
            _oneShotPending -= (HasPending(actorId) ? 1 : 0);   // 复用即取消上一轮待归还
            _pendingSnapshot = null;
            if (!_entries.TryGetValue(actorId, out Entry entry))
            {
                entry = new Entry();
                _entries[actorId] = entry;
            }

            if (entry.Source == null)
                entry.Source = _free.Count > 0 ? _free.Pop() : CreateSource();

            // 🔁 2026-10-29(`TD-audio-035` ③):距离两端按**本行**的 `min_distance` /
            //    `max_distance` 重设。原来只在 `CreateSource`(源**创建**点)pin 一次 ——
            //    但源是**复用**的(闲置栈回流),第二次 Spawn 换了 cue 就会沿用上一条 cue 的距离。
            //    生产语义 = 逐 cue 生效,故必须每次 Spawn 重设。
            ApplySpatialDistance(entry.Source, cueId);

            entry.CellX = cellX; entry.CellY = cellY; entry.CellZ = cellZ;
            entry.CueId = cueId;
            // ⚠️ **未给 bus 不覆盖已登记归属**(2026-10-19):`bus == null` 有两种语义 ——
            //    ① 新建源(`entry.Bus` 本来就 null)⇒ 不挂组,Master 直通;
            //    ② 复用已有源且本调用方未指定 ⇒ **保持原归属不变**。
            //    原实现无条件 `entry.Bus = bus`,导致「先带 bus 建源、后被无 bus 的二次调用覆盖」
            //    时,**混音组仍是旧组、登记却变 null**(`ApplyBus` 见 null 提前返回)⇒ 账与音分叉。
            //    纵深防御:治在池本身,不只治某个调用方的调用序。
            if (bus != null) entry.Bus = bus;
            entry.Active = true;
            entry.Source.gameObject.SetActive(true);
            ApplyBus(entry.Source, bus);
            return entry.Source;
        }

        private static float DefaultClock() => (float)AudioSettings.dspTime;

        /// <summary>把源挂到 <paramref name="bus"/> 对应的混音组(A-6)。
        /// <para>⚠️ 这是**唯一**写 `outputAudioMixerGroup` 的地方 —— 全库零第二处
        /// (该属性的语义见 `docs/engine-reference/unity/modules/audio.md:96`)。</para></summary>
        private void ApplyBus(AudioSource source, string bus)
        {
            if (bus == null) return;                       // 不挂组(装配前 / 非总线 cue)
            if (_busResolver == null)
            {
                throw new InvalidOperationException(
                    $"[A-6] 池未装配混音器(构造时 busResolver = null),但 Spawn 指定了 bus「{bus}」——" +
                    "装配期错误:要么不传 bus,要么先装配 AudioBusResolver(禁静默落 Master)。");
            }
            source.outputAudioMixerGroup = _busResolver.GroupFor(bus);
        }

        /// <summary>取 <paramref name="actorId"/> 当前声源的 bus 归属(未生 / 已释放 ⇒ null)。
        /// <para>测试与调试读 —— 「cue 挂到了哪条总线」的可观测面(A-6 判据)。</para></summary>
        public string BusForActor(int actorId)
            => _entries.TryGetValue(actorId, out Entry e) && e.Active ? e.Bus : null;

        /// <summary>归还 <paramref name="actorId"/> 的声源(池内复用,不销毁)。
        /// 未生过的 id = 空操作(幂等)。</summary>
        public void Release(int actorId)
        {
            ThrowIfDisposed();
            if (!_entries.TryGetValue(actorId, out Entry entry) || !entry.Active) return;

            _oneShotPending -= entry.OneShotId != 0 ? 1 : 0;
            _pendingSnapshot = null;
            entry.Active = false;
            entry.JustQueued = false;
            entry.OneShotId = 0;              // 归还后不再是「待观测」的源(再 Spawn 即重新选择语义)
            entry.StandaloneProbe = null;
            entry.EndsAtWallSeconds = 0.0;
            entry.Source.Stop();
            entry.Source.gameObject.SetActive(false);
            _free.Push(entry.Source);
            entry.Source = null;
        }

        /// <summary>取 <paramref name="actorId"/> 当前持有的声源;未生 / 已释放 ⇒ null。</summary>
        public AudioSource SourceForActor(int actorId)
            => _entries.TryGetValue(actorId, out Entry e) && e.Active ? e.Source : null;

        /// <summary>取 <paramref name="actorId"/> 当前锚点格(三轴出参;未生 ⇒ false)。
        /// <para>⚠️ 出参为 int 而非 <c>Int3</c> —— 同 <see cref="Spawn"/> 的 B3 约束。</para></summary>
        /// <returns>该 actor 当前有在响声源 ⇒ true。</returns>
        public bool TryGetCell(int actorId, out int cellX, out int cellY, out int cellZ)
        {
            cellX = cellY = cellZ = 0;
            if (!_entries.TryGetValue(actorId, out Entry e) || !e.Active) return false;
            cellX = e.CellX; cellY = e.CellY; cellZ = e.CellZ;
            return true;
        }

        /// <summary>销毁 root 与**全部**声源(含池中闲置的)。幂等。</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _pendingSnapshot = null;
            _entries.Clear();
            _free.Clear();
            _oneShotPending = 0;
            if (_root != null) DestroyObject(_root);
        }

        /// <summary>销毁时机双路径 —— `DestroyImmediate` 仅在编辑器**非播放态**合法,
        /// 播放态调用会抛(「Destroy may not be called from edit mode」的反向)。
        /// <para>⚠️ 本池同时服务 EditMode 测试与运行期装配,故须分流 —— 单用任一都会在另一半炸。</para></summary>
        private static void DestroyObject(UnityEngine.Object o)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) { UnityEngine.Object.DestroyImmediate(o); return; }
#endif
            UnityEngine.Object.Destroy(o);
        }

        /// <summary>注入事件表(空间化四列的真源)。未注入 ⇒ 四列全走 `AudioTuning` 兜底。
        /// <para>🔁 2026-10-29(`TD-audio-035` ③)。后注入模式 —— 既有 EditMode 调用不传表。</para></summary>
        /// <param name="table">事件表(`AudioAssemblySeams.Table` 的同一个实例)。</param>
        public void SetTable(AudioEventDataSet table) => _table = table;

        /// <summary>事件表(空间化四列真源;null = 未注入)。</summary>
        private AudioEventDataSet? _table;

        private AudioSource CreateSource()
        {
            var go = new GameObject("AudioSource");
            go.transform.SetParent(_root.transform, worldPositionStays: false);
            var source = go.AddComponent<AudioSource>();

            // ── 空间化三件套 pin(S15-4 · story-008「装配轮」兑现 · TD-audio-028)──
            // ⚠️ **为什么 pin 在池里而不是装配层**:池是 `AudioSource` 的**唯一创建点**
            //    (`grep AddComponent<AudioSource>` 全仓仅此处),pin 放在创建点 ⇒ 不存在
            //    「某条源漏 pin」的形态;而装配层若逐条 pin,新增一个播放路径就漏一次。
            //    (story-008 §NOT-RUN 原文「三件套 pin 归装配轮」—— 本池就是装配轮落点。)
            // ① `spatialBlend = 1`:**全 3D**。Unity 默认 0 = 纯 2D ⇒ 全部声音从听者正上方发,
            //    `transform.position` 写了也不起作用(这正是修此前「声音从原点发」的根因之一)。
            // ② Logarithmic rolloff:**听感的等比分贝每距离翻倍**,与 GDD「近的听得清、远的听不清」
            //    (物理事实,不构成播报)同向;Linear 会让远处骤停,倒像一个门限。
            // ③ `dopplerLevel = 0`:**关多普勒** —— 本项目玩家移动速度下多普勒只会把
            //    在响的呼吸层拉出可听辨的音高变化,那是「声音在动」的伪事件(违 AC-44-10 同源纪律)。
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.dopplerLevel = 0f;
            // ⚠️ 距离两端**不在这里**定 —— 它们逐 cue 变化,见 `ApplySpatialDistance`
            //    (在 `SpawnCore` 里按本行的表列重设;此处只给兜底初值,防首帧读到 Unity 默认 1/500)。
            source.minDistance = AudioTuning.SpatialMinDistance;
            source.maxDistance = AudioTuning.SpatialMaxDistance;
            return source;
        }

        /// <summary>按事件表行级的 `min_distance` / `max_distance` 重设距离两端
        /// (`TD-audio-035` ③)。表未注入 / 行缺列 / 值非法 ⇒ 回落 `AudioTuning` 兜底并计数。</summary>
        /// <param name="source">目标源(非 null)。</param>
        /// <param name="cueId">cue ordinal(取行用)。</param>
        private void ApplySpatialDistance(AudioSource source, int cueId)
        {
            float min = AudioTuning.SpatialMinDistance;
            float max = AudioTuning.SpatialMaxDistance;

            if (_table.HasValue)
            {
                AudioEventDataSet t = _table.Value;
                if (cueId >= 0 && cueId < t.RowCount)
                {
                    AudioEventRow row = t.Row(cueId);
                    bool minOk = int.TryParse(row.MinDistanceRaw, out int m1) && m1 > 0;
                    bool maxOk = int.TryParse(row.MaxDistanceRaw, out int m2) && m2 > 0;
                    if (minOk && maxOk && m1 <= m2) { min = m1; max = m2; }
                    else SpatialParamFallbacks++;
                }
                else SpatialParamFallbacks++;
            }
            else SpatialParamFallbacks++;

            source.minDistance = min;
            source.maxDistance = max;
        }

        /// <summary>空间化四列的**兜底次数**(可观测面:&gt; 0 证明「有列没查到、退回了代码常量」)。
        /// <para>🔁 2026-10-29(`TD-audio-035` ③)。判「表列真的被消费」看它,不看代码里有没有那一行。</para></summary>
        public int SpatialParamFallbacks { get; private set; }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(AudioSourcePool));
        }
    }
}
