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
    /// <example>
    /// var pool = new AudioSourcePool();
    /// AudioSource src = pool.Spawn(actorId: 7, cellX: 5, cellY: 0, cellZ: 5, cueId: 10);
    /// // ... 播放中 ...
    /// pool.Release(7);      // 归还(池内复用)
    /// pool.Dispose();       // 销毁 root 与全部源
    /// </example></summary>
    public sealed class AudioSourcePool : IDisposable
    {
        /// <summary>池自建 root 的默认名(层级辨识用)。</summary>
        public const string RootName = "AudioSourcePool";

        private readonly GameObject _root;
        private readonly Dictionary<int, Entry> _entries = new Dictionary<int, Entry>();
        private readonly Stack<AudioSource> _free = new Stack<AudioSource>();
        private bool _disposed;

        private sealed class Entry
        {
            public AudioSource Source;
            public int CellX, CellY, CellZ;
            public int CueId;
            public bool Active;
        }

        /// <summary>建池(创建隐藏 root)。</summary>
        /// <param name="rootParent">root 的父节点;null = 场景根。
        /// ⚠️ 传非 null 会让池成为**场景内容**的一部分 —— 生产装配**不得**传场景对象
        /// (ADR-028 ②);该参只供测试隔离用。</param>
        public AudioSourcePool(Transform rootParent = null)
        {
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
        {
            ThrowIfDisposed();

            if (!_entries.TryGetValue(actorId, out Entry entry))
            {
                entry = new Entry();
                _entries[actorId] = entry;
            }

            if (entry.Source == null)
                entry.Source = _free.Count > 0 ? _free.Pop() : CreateSource();

            entry.CellX = cellX; entry.CellY = cellY; entry.CellZ = cellZ;
            entry.CueId = cueId;
            entry.Active = true;
            entry.Source.gameObject.SetActive(true);
            return entry.Source;
        }

        /// <summary>归还 <paramref name="actorId"/> 的声源(池内复用,不销毁)。
        /// 未生过的 id = 空操作(幂等)。</summary>
        public void Release(int actorId)
        {
            ThrowIfDisposed();
            if (!_entries.TryGetValue(actorId, out Entry entry) || !entry.Active) return;

            entry.Active = false;
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
            _entries.Clear();
            _free.Clear();
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

        private AudioSource CreateSource()
        {
            var go = new GameObject("AudioSource");
            go.transform.SetParent(_root.transform, worldPositionStays: false);
            // 三件套 pin 归装配层(空间化参数是资产 / 调参面);池只保证「有源可用」。
            return go.AddComponent<AudioSource>();
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(AudioSourcePool));
        }
    }
}
