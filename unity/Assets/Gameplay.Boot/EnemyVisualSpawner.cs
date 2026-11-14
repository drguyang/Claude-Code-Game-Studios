// 敌人最小可见实体(2026-11-14 · 「完整可见」轮)
//
// 权威:
//   ADR-016 §二(敌人复用 9 的伤情模型;「生命归零 ⇒ INJ_COMA」)
//   ADR-013 §9 C3(呈现层只渲染,不持游戏状态)
//   ADR-015 §三(`WorldPos` 单一整数格;格 → 连续坐标换算归表现层)
//   GDD enemy-ai.md 规则二十二(六态可读 / 归零 = 不动了 + 呼吸层 / 可取材高亮)
//   GDD skeuomorphic-ui.md V-10(敌人读数条;值经既有 VitalsDto)
//   production/milestones/p0-sweep-2026-10-29.md:308(灰色人形 proxy 即可 —— 占位口径)
//
// ═══ 它做什么 ═══════════════════════════════════════════════════════════
//   每 tick 边沿:重投影 `EnemyRoster` → 对比已建视觉字典:
//     · 新敌人 ⇒ `IEnemyVisualFactory.Create`(人形/野兽组合体)
//     · 已在场 ⇒ 把「位置 / 六态 / 倒地 / 可取材 / 淡入 / 读数」写成 `EnemyPose` 交给位姿桥
//     · 投影里消失 ⇒ 销毁视觉 + 读数面片
//   ⚠️ **`EncounterEnded` ≠ 实体消失**(GDD 规则二十二):收场后成员仍在花名册里,
//      视觉**不销毁**(归零 = 不动了 + 呼吸层)。只有投影真的不再含该 id(今日无此路径,
//      teardown 除外)才销毁 —— 见 `DestroyAll`。
//
// ═══ 为什么本类不依赖 27 ═════════════════════════════════════════════════
//   27 的遭遇/状态事件今日**无生产构造点**(与 `EnemyRoster` 同境况):本类只消费流,
//   ⇒ 今天场上恒空;27 接线后**本类一字不改**即开始出现敌人。
//   ⚠️ 六态今日恒 Patrol(AI 未接);「六态姿态」由 `EnemyPoseRig` 可证伪地实现。
//
// ═══ 纪律 ═══════════════════════════════════════════════════════════════
//   · 只读流:不写事件、不持游戏状态(视觉字典只是渲染簿记)。
//   · 值经 `IVitalsQuery`(复合门面):敌人 id 走 9 的 `VitalsDto` 通道,不新开通道。
//   · 视觉根自持(`EnemyVisuals`),`DestroyAll` 连根清 —— 见 `PatientVisualSpawner` 的
//     PlayMode teardown 事故注(层级继承会漏)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Presentation.Enemies;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using UnityEngine;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>敌人最小可见实体驱动(只渲染,不持游戏状态)。</summary>
    public sealed class EnemyVisualSpawner
    {
        /// <summary>视觉根节点名(Hierarchy 走查用)。</summary>
        private const string VisualRootName = "EnemyVisuals";

        /// <summary>读数面片相对视觉根(脚底)的高度(米)。</summary>
        private const float BillboardHeight = 2.0f;

        /// <summary>默认单 tick 时长(0.05 s = 20 Hz,OQ-25-8;与 `SimTickDriver` 同值)。</summary>
        public const double DefaultSecondsPerTick = 0.05;

        private readonly Func<IReadOnlyList<SimEvent>> _eventsOf;
        private readonly IPayloadDecoder _decoder;
        private readonly IEnemyVisualFactory _factory;
        private readonly IVitalsQuery _vitals;
        private readonly Func<Camera> _camera;
        private readonly double _secondsPerTick;

        private readonly Dictionary<int, Entry> _visuals = new Dictionary<int, Entry>();
        private readonly List<int> _stale = new List<int>();
        private readonly HashSet<int> _seen = new HashSet<int>();

        private GameObject _visualRoot;

        /// <param name="eventsOf">事件流只读视图(每次调用重取)。</param>
        /// <param name="decoder">载荷解码器。</param>
        /// <param name="factory">视觉构造器(人形/野兽;真资产来了换实现)。</param>
        /// <param name="vitals">体征查询(复合门面;敌人 id 走 9 的 `VitalsDto` 通道)。</param>
        /// <param name="camera">当前渲染相机(可为 null ⇒ 读数面片不转向)。</param>
        /// <param name="secondsPerTick">单 tick 时长(表现时钟;默认 0.05 s)。</param>
        public EnemyVisualSpawner(Func<IReadOnlyList<SimEvent>> eventsOf, IPayloadDecoder decoder,
                                  IEnemyVisualFactory factory, IVitalsQuery vitals,
                                  Func<Camera> camera,
                                  double secondsPerTick = DefaultSecondsPerTick)
        {
            _eventsOf = eventsOf ?? throw new ArgumentNullException(nameof(eventsOf));
            _decoder = decoder ?? throw new ArgumentNullException(nameof(decoder));
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _vitals = vitals ?? throw new ArgumentNullException(nameof(vitals));
            _camera = camera;   // 允许 null(无相机 = 不转向)
            if (secondsPerTick <= 0d)
                throw new ArgumentOutOfRangeException(nameof(secondsPerTick), secondsPerTick,
                    "单 tick 时长必须为正");
            _secondsPerTick = secondsPerTick;
        }

        /// <summary>
        /// 脚本位姿源(迟绑定;2026-11-14 · 第二段四拍)。
        /// <para>⚠️ **迟绑定**的理由与 `TeleportSink.BindPlayer` 同格:本类在
        /// `CompositionRoot` 里构造,而编排要玩家(第 4 步才铸造)⇒ 只能后置注入。</para>
        /// <para>null / 返回 false ⇒ 全部按出生格 + <see cref="EnemyStateMachine.State.Patrol"/>;
        /// 非脚本遭遇(掷骰池)恒走这条。</para>
        /// </summary>
        public Func<int, ScriptedEnemyPose?> ScriptedPose { get; set; }

        /// <summary>
        /// tick 边沿驱动 —— 重投影花名册,增删/更新视觉实体。
        /// </summary>
        /// <param name="tick">本边沿的逻辑 tick(不得为负)。</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="tick"/> 为负。</exception>
        public void OnTickEdge(long tick)
        {
            if (tick < 0)
                throw new ArgumentOutOfRangeException(nameof(tick), tick, "逻辑 tick 不得为负");

            IReadOnlyList<EnemyActor> rows = EnemyRoster.Build(_eventsOf(), _decoder);

            _seen.Clear();
            for (int i = 0; i < rows.Count; i++)
            {
                EnemyActor row = rows[i];
                _seen.Add(row.ActorId);

                if (!_visuals.TryGetValue(row.ActorId, out Entry entry))
                {
                    entry = Create(row);
                    if (entry == null) continue;   // factory 选择不建(契约允许)
                    _visuals[row.ActorId] = entry;
                }

                entry.ElapsedSeconds += _secondsPerTick;
                Apply(entry, row);
            }

            // 从投影里消失的实体(今日无此生产路径;`EncounterEnded` **不**触发 —— 收场 ≠ 消失)
            _stale.Clear();
            foreach (KeyValuePair<int, Entry> kv in _visuals)
                if (!_seen.Contains(kv.Key))
                    _stale.Add(kv.Key);

            for (int i = 0; i < _stale.Count; i++)
            {
                Entry entry = _visuals[_stale[i]];
                _factory.Destroy(entry.Visual);
                entry.Bar.Destroy();
                _visuals.Remove(_stale[i]);
            }
        }

        /// <summary>
        /// 销毁全部视觉实体与面片(PlayMode teardown 用;幂等)。
        /// <para>⚠️ 构造时注入的 factory 若实现 <see cref="IDisposable"/>(组合体 factory 持有
        /// 运行时轮廓材质),本方法**一并释放** —— 所有权随注入转移,teardown 一次清干净。</para>
        /// </summary>
        public void DestroyAll()
        {
            foreach (KeyValuePair<int, Entry> kv in _visuals)
            {
                _factory.Destroy(kv.Value.Visual);
                kv.Value.Bar.Destroy();
            }
            _visuals.Clear();

            if (_visualRoot != null)
            {
                // 编辑态(EditMode 夹具 / 工具)必须 immediate —— 与 factory / 面片同款守卫。
                if (!Application.isPlaying) UnityEngine.Object.DestroyImmediate(_visualRoot);
                else UnityEngine.Object.Destroy(_visualRoot);
                _visualRoot = null;
            }

            // 运行时材质(组合体 factory 自持)随所有权一起释放;幂等。
            (_factory as IDisposable)?.Dispose();
        }

        /// <summary>当前已建视觉实体数(测试接缝 —— 可证伪「敌人真的出现了」)。</summary>
        public int VisualCount => _visuals.Count;

        /// <summary>取某实体的视觉根(测试接缝;不存在 ⇒ false)。</summary>
        public bool TryGetVisual(int actorId, out GameObject visual)
        {
            if (_visuals.TryGetValue(actorId, out Entry entry)) { visual = entry.Visual; return true; }
            visual = null;
            return false;
        }

        /// <summary>取某实体的位姿桥(测试接缝;不存在 ⇒ false)。</summary>
        public bool TryGetRig(int actorId, out EnemyPoseRig rig)
        {
            if (_visuals.TryGetValue(actorId, out Entry entry)) { rig = entry.Rig; return true; }
            rig = null;
            return false;
        }

        // ── 内部 ─────────────────────────────────────────────────────────
        private Entry Create(in EnemyActor row)
        {
            var pose = new EnemyPose(row.ActorId, row.ProtoId, CellToWorld(row.Cell), 0f,
                                     EnemyStateMachine.State.Patrol, row.Downed,
                                     IsSalvageable(row), 0f, ReadPosition(row), BandOf(row));
            if (TryResolveScripted(row.ActorId, out ScriptedEnemyPose scripted))
                pose = new EnemyPose(row.ActorId, row.ProtoId, scripted.Position, scripted.YawDegrees,
                                     scripted.State, row.Downed, IsSalvageable(row), 0f,
                                     ReadPosition(row), BandOf(row));

            GameObject visual = _factory.Create(pose);
            if (visual == null) return null;   // factory 选择不建

            visual.transform.SetParent(VisualRoot.transform, false);

            var rig = visual.GetComponent<EnemyPoseRig>();
            var bar = new EnemyVitalsBar(row.ActorId);

            return new Entry(visual, rig, bar);
        }

        private void Apply(Entry entry, in EnemyActor row)
        {
            float alpha = Mathf.Clamp01((float)(entry.ElapsedSeconds / EnemyPoseRig.FadeInSeconds));
            var pose = new EnemyPose(row.ActorId, row.ProtoId, CellToWorld(row.Cell), 0f,
                                     EnemyStateMachine.State.Patrol, row.Downed,
                                     IsSalvageable(row), alpha, ReadPosition(row), BandOf(row));
            float scale = 1f;
            if (TryResolveScripted(row.ActorId, out ScriptedEnemyPose scripted))
            {
                pose = new EnemyPose(row.ActorId, row.ProtoId, scripted.Position, scripted.YawDegrees,
                                     scripted.State, row.Downed, IsSalvageable(row), alpha,
                                     ReadPosition(row), BandOf(row));
                scale = scripted.Scale;
            }

            entry.Rig?.Apply(pose, (float)_secondsPerTick);

            // 缩放**在** `Apply` 之后乘 —— 位姿桥每帧把 localScale 重置成淡入插值,
            // 这里再乘脚本缩放(幼崽 0.55)才是幂等的。
            if (entry.Visual != null && !Mathf.Approximately(scale, 1f))
                entry.Visual.transform.localScale *= scale;

            entry.Bar.Visible = true;
            entry.Bar.CurrentState = BarStateOf(row, pose.Band);   // 六态映射(27 的状态语义 → 42 的渲染形态)
            entry.Bar.RenderReading(pose.VitalsFill, BandWord(pose.Band), pose.Downed);
            entry.Bar.WorldPosition = pose.Position + new Vector3(0f, BillboardHeight, 0f);
            entry.Bar.FaceCamera(_camera?.Invoke());
        }

        private bool TryResolveScripted(int actorId, out ScriptedEnemyPose pose)
        {
            Func<int, ScriptedEnemyPose?> source = ScriptedPose;
            if (source == null) { pose = default; return false; }
            ScriptedEnemyPose? result = source(actorId);
            pose = result ?? default;
            return result.HasValue;
        }

        /// <summary>读数 0–1(值经 9 的 `VitalsDto` 通道;42 不解释战力)。</summary>
        private float ReadPosition(in EnemyActor row)
        {
            VitalsDto vitals = _vitals.GetVitals(new PatientId(row.ActorId));
            return vitals.Position;
        }

        /// <summary>档位(9 的纯分类;42 只取文案)。</summary>
        private static InjuryState BandOf(in EnemyActor row) => EnemyVitalsProjection.BandOf(row.Hurt);

        /// <summary>倒地野兽可取材(人形恒 false;GDD 规则二十二 · 2026-11-14 裁定)。</summary>
        private static bool IsSalvageable(in EnemyActor row)
            => row.Downed && row.ProtoId == ComposedEnemyVisualFactory.WildDogProtoId;

        /// <summary>整数格 → 世界连续坐标(脚底;敌人视觉根在脚底,故无 y 偏移)。</summary>
        private static Vector3 CellToWorld(WorldPos cell) => new Vector3(cell.X, cell.Y, cell.Z);

        /// <summary>档位文案(27 的伤情词汇;42 不比较阈值)。</summary>
        private static string BandWord(InjuryState band)
        {
            switch (band)
            {
                case InjuryState.Mild: return "轻伤";
                case InjuryState.Moderate: return "中伤";
                case InjuryState.Severe: return "重伤";
                case InjuryState.Critical: return "危殆";
                case InjuryState.Coma: return "昏迷";
                case InjuryState.Recovered: return "复苏";
                case InjuryState.Chronic: return "旧伤";
                case InjuryState.Deceased: return "殁";
                default: return "无恙";
            }
        }

        /// <summary>伤情档位 → 读数条六态(27 的状态语义 → 42 的渲染形态;纯映射)。</summary>
        private static EnemyState BarStateOf(in EnemyActor row, InjuryState band)
        {
            if (row.Downed)
                return band == InjuryState.Deceased ? EnemyState.Dead : EnemyState.Downed;

            switch (band)
            {
                case InjuryState.Mild:
                case InjuryState.Moderate:
                    return EnemyState.Injured;
                case InjuryState.Severe:
                case InjuryState.Critical:
                    return EnemyState.Staggered;
                default:
                    return EnemyState.Healthy;
            }
        }

        private GameObject VisualRoot
        {
            get
            {
                if (_visualRoot == null) _visualRoot = new GameObject(VisualRootName);
                return _visualRoot;
            }
        }

        /// <summary>一个实体的渲染簿记(不是游戏状态)。</summary>
        private sealed class Entry
        {
            public readonly GameObject Visual;
            public readonly EnemyPoseRig Rig;
            public readonly EnemyVitalsBar Bar;
            public double ElapsedSeconds;

            public Entry(GameObject visual, EnemyPoseRig rig, EnemyVitalsBar bar)
            {
                Visual = visual;
                Rig = rig;
                Bar = bar;
            }
        }
    }
}
