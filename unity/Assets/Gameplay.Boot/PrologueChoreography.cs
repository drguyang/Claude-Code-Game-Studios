// 第二段四拍的行为编排(2026-11-14 · N5 的 ❗「行为编排」段)
//
// 权威:
//   `design/levels/prologue-beat-script.md` 第二段(逐字):
//     P2-02 野狗:野狗扑向孩子;镜头切近;孩子倒地
//     P2-03 砸石头:石头飞出 → 狗松口 → 逃(判据 = **野狗离场**)
//     P2-06 地痞:三个地痞;对话(短);其中一个推你 ⇒ 玩家被击退 1 格(判据 = 对话结束)
//     P2-09 回程:第二次遇狗:狗带幼崽,看你一眼走开
//   ADR-016 §一(敌人格**不进流** —— 位置是派生态)
//   ADR-016 §二(敌人复用 9 的伤情模型;「倒地」= 状态机,不是另一套 HP)
//   ADR-013 §9 C3(呈现层只渲染,不持游戏状态)
//   GDD enemy-ai.md 规则二十、二十一(遭遇生命周期;Ended ≠ 实体消失)
//
// ═══ 本类是什么 ═════════════════════════════════════════════════════════
//   **四条脚本行为的时间线**:把「谁、在哪个 tick、走到哪一格、摆什么姿态」按拍表算出来,
//   交给两个现成的渲染方(敌人位姿桥 / 病人视觉)去画。它不是 AI:没有感知、没有寻路、
//   没有决策 —— 是一条**写死的时间线**,只对三个脚本遭遇 id 生效。
//
// ═══ 它写什么、不写什么 ═════════════════════════════════════════════════
//   写:P2-03 的「野狗离场」一条 `EncounterEnded(reason = 0 脱离)`(经 27 的写者,
//       去重判据在写者里 —— 本类不复制)。
//   写:P2-06 的「推你 1 格」经 `PlayerController.Teleport`(被放置路径;格提交仍走
//       `ActorCellEntered` 既有通道,不在本类另写)。
//   不写:伤情(25 的写者在 `CombatDriver`)、倒地(9 的写者)、遭遇开始(52)。
//   ⇒ 「野狗咬孩子」的伤是**叙事**(孩子是病人,伤情归 9 / 10),本类只演「咬住」的位姿。
//
// ═══ 为什么敌人位置可以写死 ═════════════════════════════════════════════
//   这三支是**脚本遭遇**(节拍脚本 :232:「P2-02 / P2-09 是脚本触发的固定遭遇,不走掷骰
//   (否则不可复现)」)。位置写死 = 可复现;真 AI 接入后本类整体退役(27 的 tick 循环接管)。
//
// ═══ tick 口径 ═══════════════════════════════════════════════════════════
//   全部时长以 **tick** 计(20 Hz,`SimTickDriver` 同源),渲染帧率不参与判定 ——
//   与 `PrologueBeatDriver` / `CutscenePlayer` 同一理由(高帧率不得加快演出)。
//   ⚠️ 读档回卷(tick 倒退)⇒ 全量复位:时间线从流里重推(读一次档重演一次演出,
//      与 `TentBuildCutscene` 同一裁定)。
//
// ⚠️ **本类不证明「手按 E 在真关卡里能看完四拍」** —— 那需要真启动序走查,登记在
//   `TD-building-07` 的收口段(与 `enemy_encounter_test` 头注同一条)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using UnityEngine;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>
    /// 一名脚本敌人的一帧位姿(敌人**位置 / 朝向 / 六态 / 缩放**的表现面快照)。
    /// <para>⚠️ 不是游戏状态:消费方是 <see cref="EnemyVisualSpawner"/>(渲染),不是任何 sim 驱动。</para>
    /// </summary>
    public readonly struct ScriptedEnemyPose
    {
        /// <summary>世界连续坐标(脚底;格 → 米由本类换算)。</summary>
        public readonly Vector3 Position;

        /// <summary>朝向(绕世界 Y 轴;0 = +Z)。</summary>
        public readonly float YawDegrees;

        /// <summary>六态(27 的状态词汇,42 只画)。</summary>
        public readonly EnemyStateMachine.State State;

        /// <summary>视觉缩放(幼崽 = 0.55;常态 1)。</summary>
        public readonly float Scale;

        /// <summary>构造一帧。</summary>
        public ScriptedEnemyPose(Vector3 position, float yawDegrees,
                                 EnemyStateMachine.State state, float scale = 1f)
        {
            Position = position;
            YawDegrees = yawDegrees;
            State = state;
            Scale = scale;
        }
    }

    /// <summary>第二段四拍的脚本时间线(只演,不判)。</summary>
    public sealed class PrologueChoreography
    {
        // ── 遭遇 id(PrologueEncounterDriver.PrologueThree 的声明序)───────────
        /// <summary>P2-06 三个地痞的遭遇 id。</summary>
        public const int ThugEncounterId = 1;
        /// <summary>P2-02 / P2-03 两条野狗的遭遇 id。</summary>
        public const int DogEncounterId = 2;
        /// <summary>P2-09 狗带幼崽的遭遇 id。</summary>
        public const int CampDogEncounterId = 3;

        // ── 时间线(tick = 0.05 s)──────────────────────────────────────────
        /// <summary>P2-02 扑击:从出生格冲到孩子身边(2.0 s)。</summary>
        public const int LungeTicks = 40;
        /// <summary>P2-03 逃:松口后跑到远处(2.0 s),到点写「离场」。</summary>
        public const int FleeTicks = 40;
        /// <summary>P2-09 看一眼:停住朝玩家(2.0 s)。</summary>
        public const int LookTicks = 40;
        /// <summary>P2-09 走开:离开(3.0 s),到点写「离场」。</summary>
        public const int WalkAwayTicks = 60;
        /// <summary>P2-06 每句台词停留(2.5 s)。</summary>
        public const int LineTicks = 50;
        /// <summary>P2-06 地痞上前一步(1.0 s)。</summary>
        public const int StepTicks = 20;

        /// <summary>幼崽视觉缩放(「狗带幼崽」的呈现;原型仍是野狗)。</summary>
        public const float PupScale = 0.55f;

        /// <summary>P2-06 的三句短对话(逐字取自节拍脚本「对话(短)」;对话本体归 52,今日由本类演)。</summary>
        public static readonly IReadOnlyList<string> ThugLines = new[]
        {
            "干什么的?",
            "找本书。",
            "书能当饭吃?",
        };

        private readonly Func<IReadOnlyList<SimEvent>> _eventsOf;
        private readonly IPayloadDecoder _decoder;
        private readonly EncounterEndWriter _ends;
        private readonly Func<WorldPos> _playerCell;
        private readonly Func<Vector3> _playerPosition;
        private readonly Action<Vector3> _teleport;
        private readonly Func<WorldPos, bool> _walkable;
        private readonly Action<string> _showLine;
        private readonly Func<bool> _isNarrating;

        private readonly Dictionary<int, Track> _tracks = new Dictionary<int, Track>(3);

        private long _lastTick = long.MinValue;
        private int _cursor;
        private int _childId = -1;
        private bool _childDowned;

        /// <param name="eventsOf">事件流只读视图。</param>
        /// <param name="decoder">载荷解码器。</param>
        /// <param name="ends">`EncounterEnded` 写者(27;P2-03 / P2-09 的「离场」)。</param>
        /// <param name="playerCell">玩家经流确立格(`LastCommittedCell`)。</param>
        /// <param name="playerPosition">玩家连续位置(推格时保留高度)。</param>
        /// <param name="teleport">传送门(被放置路径;P2-06 的击退 1 格)。</param>
        /// <param name="walkable">可通行查询(击退方向只落在可走格;可为 null ⇒ 不校验)。</param>
        /// <param name="showLine">一行字幕(P2-06 的对话;可为 null ⇒ 只走时间不显示)。</param>
        /// <param name="isNarrating">旁白是否在播(节拍驱动的 `ShowingId != null`)。
        /// P2-06 的对话**等旁白播完**再开口 —— 否则会盖掉 P2-05 那一句。</param>
        /// <exception cref="ArgumentNullException">必需依赖为 null。</exception>
        public PrologueChoreography(Func<IReadOnlyList<SimEvent>> eventsOf, IPayloadDecoder decoder,
                                    EncounterEndWriter ends,
                                    Func<WorldPos> playerCell, Func<Vector3> playerPosition,
                                    Action<Vector3> teleport, Func<WorldPos, bool> walkable,
                                    Action<string> showLine, Func<bool> isNarrating)
        {
            _eventsOf = eventsOf ?? throw new ArgumentNullException(nameof(eventsOf));
            _decoder = decoder ?? throw new ArgumentNullException(nameof(decoder));
            _ends = ends ?? throw new ArgumentNullException(nameof(ends));
            _playerCell = playerCell ?? throw new ArgumentNullException(nameof(playerCell));
            _playerPosition = playerPosition ?? throw new ArgumentNullException(nameof(playerPosition));
            _teleport = teleport ?? throw new ArgumentNullException(nameof(teleport));
            _walkable = walkable;     // 允许 null(无世界装配的夹具)
            _showLine = showLine;     // 允许 null(只走时间)
            _isNarrating = isNarrating;
        }

        /// <summary>
        /// P2-02 的效果落点:孩子出现了 —— 记下 id(扑击的落点)并开始「咬住」的下游态。
        /// </summary>
        /// <param name="patientId">孩子(病人 id;≥ 0)。</param>
        public void SetChild(int patientId)
        {
            if (patientId < 0) return;
            _childId = patientId;
        }

        /// <summary>孩子是否已倒地(给病人视觉的倒地闸门;只认 P2-02 的孩子)。</summary>
        /// <param name="patientId">病人 id。</param>
        public bool IsChildDowned(int patientId)
            => _childDowned && _childId >= 0 && patientId == _childId;

        /// <summary>
        /// 本 tick 的出手**强制目标**(P2-03 的石头;−1 = 无)。
        /// <para>⚠️ 只在「狗咬住」相位返回狗 id —— 石头是**投掷**,不受手长限制;
        /// 其余时间 `CombatDriver` 走正常判距(见该类 ②)。</para>
        /// </summary>
        public int ScriptedAttackTarget { get; private set; } = -1;

        /// <summary>取某敌人的脚本位姿(给 <see cref="EnemyVisualSpawner"/>;无脚本 ⇒ false)。</summary>
        /// <param name="actorId">敌人 id。</param>
        /// <param name="pose">位姿快照。</param>
        public bool TryGetPose(int actorId, out ScriptedEnemyPose pose)
        {
            foreach (KeyValuePair<int, Track> kv in _tracks)
            {
                Track t = kv.Value;
                int i = t.IndexOf(actorId);
                if (i < 0) continue;
                pose = new ScriptedEnemyPose(t.Pos[i], t.Yaw[i], t.State[i], t.Scale[i]);
                return true;
            }
            pose = default;
            return false;
        }

        /// <summary>
        /// tick 边沿驱动 —— 先吃流里的新事件(遭遇开始 / 石头命中),再推进时间线。
        /// </summary>
        /// <param name="tick">本边沿的逻辑 tick(≥ 0)。</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="tick"/> 为负。</exception>
        public void OnTickEdge(long tick)
        {
            if (tick < 0)
                throw new ArgumentOutOfRangeException(nameof(tick), tick, "逻辑 tick 不得为负");

            if (tick < _lastTick) Reset();   // 读档回卷 ⇒ 时间线重推(见头注)
            _lastTick = tick;

            ConsumeEvents(tick);

            // P2-03 的石头目标:只有「咬住」相位才有(由 ConsumeEvents 之后的相位决定)
            ScriptedAttackTarget = -1;

            foreach (KeyValuePair<int, Track> kv in _tracks)
            {
                Advance(kv.Value, tick);
                if (kv.Value.Beat == Beat.DogsBiting && kv.Value.Lead >= 0)
                    ScriptedAttackTarget = kv.Value.Actors[kv.Value.Lead];
            }
        }

        // ── 事件消费(遭遇开始 / 石头命中)──────────────────────────────────
        private void ConsumeEvents(long tick)
        {
            IReadOnlyList<SimEvent> events = _eventsOf();
            if (events == null) return;
            if (_cursor > events.Count) _cursor = 0;   // 流被换过(兜底;正常走 Reset)

            for (; _cursor < events.Count; _cursor++)
            {
                SimEvent e = events[_cursor];
                switch (e.Kind)
                {
                    case EventKind.EncounterStarted:
                    {
                        EncounterStartedPayload p =
                            _decoder.Decode<EncounterStartedPayload>(e.Kind, e.Payload);
                        if (p.ActorIds == null || p.ActorIds.Length == 0) break;
                        if (_tracks.ContainsKey(p.EncounterId)) break;   // 去重(读档重放)
                        Begin(p, tick);
                        break;
                    }
                    case EventKind.EnemyInjuryOnset:
                    {
                        EnemyInjuryOnsetPayload p =
                            _decoder.Decode<EnemyInjuryOnsetPayload>(e.Kind, e.Payload);
                        if (_tracks.TryGetValue(DogEncounterId, out Track dog)
                            && dog.Beat == Beat.DogsBiting
                            && dog.Lead >= 0 && dog.Actors[dog.Lead] == p.TargetId)
                            StartDogsFlee(dog, tick);
                        break;
                    }
                }
            }
        }

        private void Begin(in EncounterStartedPayload p, long tick)
        {
            var track = new Track
            {
                EncounterId = p.EncounterId,
                Actors = (int[])p.ActorIds.Clone(),
                Pos = new Vector3[p.ActorIds.Length],
                Yaw = new float[p.ActorIds.Length],
                State = new EnemyStateMachine.State[p.ActorIds.Length],
                Scale = new float[p.ActorIds.Length],
                Lead = 0,
                BeatStart = tick,
            };
            var spawn = new Vector3(p.SpawnCell.X, p.SpawnCell.Y, p.SpawnCell.Z);
            for (int i = 0; i < track.Actors.Length; i++)
            {
                track.Pos[i] = spawn + new Vector3(i, 0f, 0f);   // 同格多成员按 id 序错开
                track.Yaw[i] = 0f;
                track.State[i] = EnemyStateMachine.State.Patrol;
                track.Scale[i] = 1f;
            }

            switch (p.EncounterId)
            {
                case DogEncounterId:   // P2-02:扑向孩子
                    track.Beat = Beat.DogsLunge;
                    track.State[track.Lead] = EnemyStateMachine.State.Chase;
                    track.MotionStart = tick;
                    track.MotionTicks = LungeTicks;
                    StartMotion(track, BiteCell);
                    break;

                case ThugEncounterId:  // P2-06:等旁白 → 对话 → 上前 → 推
                    track.Beat = Beat.ThugsWaitNarration;
                    break;

                case CampDogEncounterId:   // P2-09:看你一眼,走开
                    track.Beat = Beat.PackLook;
                    track.Scale[1] = PupScale;
                    for (int i = 0; i < track.Actors.Length; i++)
                        track.State[i] = EnemyStateMachine.State.Alert;
                    break;

                default:
                    // 不是脚本遭遇 ⇒ **不留轨**(视觉仍按出生格);掷骰池不归本类管。
                    return;
            }

            _tracks[p.EncounterId] = track;
        }

        // ── 时间线推进 ─────────────────────────────────────────────────────
        private void Advance(Track t, long tick)
        {
            switch (t.Beat)
            {
                case Beat.DogsLunge:
                    StepMotion(t, tick);
                    if (tick - t.BeatStart < LungeTicks) break;
                    t.Pos[t.Lead] = t.MotionTo[t.Lead];
                    t.Yaw[t.Lead] = YawToward(t.Pos[t.Lead], ChildWorld());
                    t.State[0] = EnemyStateMachine.State.Engage;   // 咬住
                    t.Beat = Beat.DogsBiting;
                    t.BeatStart = tick;
                    _childDowned = true;                            // 孩子倒地(呈现)
                    break;

                case Beat.DogsBiting:
                    // 停住咬住 —— 等玩家按 Attack(石头由 CombatDriver 判定)
                    t.Yaw[0] = YawToward(t.Pos[0], ChildWorld());
                    break;

                case Beat.DogsFlee:
                    StepMotion(t, tick);
                    if (tick - t.BeatStart < FleeTicks) break;
                    t.Pos[t.Lead] = t.MotionTo[t.Lead];
                    t.State[t.Lead] = EnemyStateMachine.State.Patrol;
                    t.Beat = Beat.Done;
                    _ends.TryEnd(t.EncounterId, EncounterEndReasonKind.Disengaged, tick);   // 「野狗离场」
                    break;

                case Beat.ThugsWaitNarration:
                    if (_isNarrating != null && _isNarrating()) break;   // 旁白播完再开口
                    t.Beat = Beat.ThugsDialogue;
                    t.BeatStart = tick;
                    t.Line = 0;
                    ShowLine(0);
                    break;

                case Beat.ThugsDialogue:
                {
                    int line = (int)((tick - t.BeatStart) / LineTicks);
                    if (line <= t.Line) break;
                    if (line < ThugLines.Count)
                    {
                        t.Line = line;
                        ShowLine(line);
                        break;
                    }
                    // 对话结束 ⇒ 最近的地痞上前一步
                    t.Beat = Beat.ThugsApproach;
                    t.BeatStart = tick;
                    t.Lead = NearestToPlayer(t);
                    t.State[t.Lead] = EnemyStateMachine.State.Engage;
                    StartMotion(t, OneStepToward(t.Pos[t.Lead], PlayerWorld()));
                    t.MotionTicks = StepTicks;
                    break;
                }

                case Beat.ThugsApproach:
                    StepMotion(t, tick);
                    if (tick - t.BeatStart < StepTicks) break;
                    t.Pos[t.Lead] = t.MotionTo[t.Lead];
                    PushPlayer(t);                                    // 推你 1 格
                    t.State[t.Lead] = EnemyStateMachine.State.Patrol;
                    t.Beat = Beat.Done;
                    break;

                case Beat.PackLook:
                    for (int i = 0; i < t.Actors.Length; i++)
                        t.Yaw[i] = YawToward(t.Pos[i], PlayerWorld());
                    if (tick - t.BeatStart < LookTicks) break;
                    t.Beat = Beat.PackWalkAway;
                    t.BeatStart = tick;
                    t.MotionStart = tick;
                    t.MotionTicks = WalkAwayTicks;
                    t.MotionFrom = (Vector3[])t.Pos.Clone();
                    var away = new Vector3[t.Actors.Length];
                    for (int i = 0; i < t.Actors.Length; i++)
                    {
                        away[i] = t.Pos[i] + new Vector3(6f, 0f, 0f);   // 背向玩家(玩家自西来)
                        t.State[i] = EnemyStateMachine.State.Disengage;
                    }
                    t.MotionTo = away;
                    break;

                case Beat.PackWalkAway:
                    StepMotion(t, tick);
                    if (tick - t.BeatStart < WalkAwayTicks) break;
                    for (int i = 0; i < t.Actors.Length; i++)
                    {
                        t.Pos[i] = t.MotionTo[i];
                        t.State[i] = EnemyStateMachine.State.Patrol;
                    }
                    t.Beat = Beat.Done;
                    _ends.TryEnd(t.EncounterId, EncounterEndReasonKind.Disengaged, tick);   // 「走开」
                    break;
            }
        }

        /// <summary>线性插值落到**当前相位的主运动者**(<see cref="Track.Lead"/>)上。</summary>
        private static void StepMotion(Track t, long tick)
        {
            if (t.MotionTicks <= 0 || t.MotionFrom == null || t.MotionTo == null) return;
            if (t.MotionFrom.Length != t.Pos.Length || t.MotionTo.Length != t.Pos.Length) return;

            float u = Mathf.Clamp01((tick - t.MotionStart) / (float)t.MotionTicks);
            t.MotionFrom.CopyTo(t.Pos, 0);
            for (int i = 0; i < t.Pos.Length; i++)
                if (i == t.Lead || t.Beat == Beat.PackWalkAway)
                    t.Pos[i] = Vector3.Lerp(t.MotionFrom[i], t.MotionTo[i], u);

            if (t.Lead >= 0 && t.Lead < t.Pos.Length)
                t.Yaw[t.Lead] = YawToward(t.MotionFrom[t.Lead], t.MotionTo[t.Lead]);
        }

        private void StartDogsFlee(Track t, long tick)
        {
            t.Beat = Beat.DogsFlee;
            t.BeatStart = tick;
            t.State[t.Lead] = EnemyStateMachine.State.Disengage;   // 松口
            StartMotion(t, t.Pos[t.Lead] + new Vector3(-6f, 0f, -1f));   // 逃回七里路
            t.MotionTicks = FleeTicks;
        }

        /// <summary>
        /// 开一段「主运动者(Lead)单独走」的线性运动。
        /// <para>⚠️ 数组**全成员长**(不只是 Lead 一个):<see cref="StepMotion"/> 会整体
        /// `CopyTo` 回 <c>Pos</c>,短数组在 Lead ≠ 0 时会越界(地痞上前就踩过)。</para>
        /// </summary>
        private static void StartMotion(Track t, Vector3 to)
        {
            t.MotionFrom = (Vector3[])t.Pos.Clone();
            t.MotionTo = (Vector3[])t.Pos.Clone();
            t.MotionTo[t.Lead] = to;
            t.MotionStart = t.BeatStart;
        }

        private void ShowLine(int index)
        {
            if (_showLine == null || index < 0 || index >= ThugLines.Count) return;
            _showLine(ThugLines[index]);
        }

        /// <summary>
        /// 推你 1 格:把 8 个相邻格按「**远离**该地痞」的方向排序(点积降序),
        /// 取第一个**可走**的;都不行 ⇒ 不传送(不把玩家塞进墙)。
        /// </summary>
        private void PushPlayer(Track t)
        {
            WorldPos player = _playerCell();
            WorldPos thug = CellOf(t.Pos[t.Lead]);
            float ax = player.X - thug.X;
            float az = player.Z - thug.Z;
            if (Mathf.Abs(ax) < 0.5f && Mathf.Abs(az) < 0.5f) ax = 1f;   // 同格:默认往东

            var candidates = new List<WorldPos>(8);
            for (int dx = -1; dx <= 1; dx++)
            for (int dz = -1; dz <= 1; dz++)
            {
                if (dx == 0 && dz == 0) continue;
                candidates.Add(new WorldPos(player.X + dx, player.Y, player.Z + dz));
            }
            // 点积降序:最「背着地痞」的那格排第一;副轴分量罚 0.25 ⇒ 同分时
            // 直着被推出去优先(对角只是退路)。平手才落到枚举序。
            // ⚠️ 不能用 `Mathf.Sign` —— 它对 0 返回 **+1**(Unity 口径),会把「正南」
            //    算成「东南」。这里要的是 `System.Math.Sign` 的 0。
            float sx = ax > 0f ? 1f : ax < 0f ? -1f : 0f;
            float sz = az > 0f ? 1f : az < 0f ? -1f : 0f;
            float Score(WorldPos c)
            {
                float dx = c.X - player.X;
                float dz = c.Z - player.Z;
                return dx * sx + dz * sz - 0.25f * (Mathf.Abs(dx * sz) + Mathf.Abs(dz * sx));
            }
            candidates.Sort((a, b) => Score(b).CompareTo(Score(a)));

            for (int i = 0; i < candidates.Count; i++)
            {
                if (_walkable != null && !_walkable(candidates[i])) continue;
                Vector3 p = _playerPosition();
                _teleport(new Vector3(candidates[i].X, p.y, candidates[i].Z));
                return;
            }
        }

        // ── 几何 ─────────────────────────────────────────────────────────
        /// <summary>孩子的出现格(BootRoot 的 P2-02 效果点 `(45, 0, 22)`;格 → 米 1:1)。</summary>
        private static readonly Vector3 ChildCell = new Vector3(45f, 0f, 22f);

        /// <summary>咬住的位置:孩子北侧一格。</summary>
        private static readonly Vector3 BiteCell = new Vector3(ChildCell.x, 0f, ChildCell.z - 1f);

        private static Vector3 ChildWorld() => ChildCell;

        private Vector3 PlayerWorld() => _playerPosition();

        private static WorldPos CellOf(Vector3 p)
            => new WorldPos(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y), Mathf.FloorToInt(p.z));

        private static float YawToward(Vector3 from, Vector3 to)
        {
            float dx = to.x - from.x;
            float dz = to.z - from.z;
            if (Mathf.Abs(dx) < 1e-5f && Mathf.Abs(dz) < 1e-5f) return 0f;
            return Mathf.Atan2(dx, dz) * Mathf.Rad2Deg;
        }

        /// <summary>最近的地痞(到玩家格的切比雪夫距离最小;平手取 id 小者 —— 与 27 同口径)。</summary>
        private int NearestToPlayer(Track t)
        {
            WorldPos player = _playerCell();
            int best = 0;
            long bestDist = long.MaxValue;
            for (int i = 0; i < t.Pos.Length; i++)
            {
                WorldPos c = CellOf(t.Pos[i]);
                long dx = Math.Abs((long)c.X - player.X);
                long dz = Math.Abs((long)c.Z - player.Z);
                long d = Math.Max(dx, dz);
                if (d < bestDist) { bestDist = d; best = i; }
            }
            return best;
        }

        /// <summary>朝玩家走一格(单位步;与玩家同格 ⇒ 不动)。</summary>
        private Vector3 OneStepToward(Vector3 from, Vector3 player)
        {
            float dx = player.x - from.x;
            float dz = player.z - from.z;
            if (Mathf.Abs(dx) < 0.5f && Mathf.Abs(dz) < 0.5f) return from;
            return new Vector3(from.x + Math.Sign(dx), from.y, from.z + Math.Sign(dz));
        }

        private void Reset()
        {
            _tracks.Clear();
            _cursor = 0;
            _childDowned = false;
            ScriptedAttackTarget = -1;
            // ⚠️ `_childId` **不**清:孩子的 id 来自 P2-02 的效果闩锁,读档后仍然有效。
        }

        /// <summary>一条遭遇的时间线(渲染簿记,不是游戏状态)。</summary>
        private enum Beat
        {
            Done = 0,
            DogsLunge,
            DogsBiting,
            DogsFlee,
            ThugsWaitNarration,
            ThugsDialogue,
            ThugsApproach,
            PackLook,
            PackWalkAway,
        }

        private sealed class Track
        {
            public int EncounterId;
            public int[] Actors;
            public Vector3[] Pos;
            public float[] Yaw;
            public EnemyStateMachine.State[] State;
            public float[] Scale;
            public Beat Beat;
            public long BeatStart;
            public int Lead;
            public int Line;

            public Vector3[] MotionFrom;
            public Vector3[] MotionTo;
            public long MotionStart;
            public int MotionTicks;

            public int IndexOf(int actorId)
            {
                for (int i = 0; i < Actors.Length; i++)
                    if (Actors[i] == actorId) return i;
                return -1;
            }
        }
    }
}
