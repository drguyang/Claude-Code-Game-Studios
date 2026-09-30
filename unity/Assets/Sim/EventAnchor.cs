// ADR-015 / ADR-009 / GDD random-events.md DC-4 —— spawn_anchor 解析与事件状态机。
//
// 权威来源:
//   ADR-015 §一 —— 两层世界（烘焙逻辑层 + 纯视觉层）
//   ADR-009 §一 —— 三态分类（预告 = 导演本地态不进流，降临才进流）
//   GDD random-events.md DC-4 —— 生成与锚点 · 规则七 六态状态机
//
// 核心机制:
//   - 三锚点解析器各自确定性
//   - 六态迁移表完整实现且闭集
//   - 事件驱动零轮询

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim
{
    /// <summary>
    /// 事件状态机六态。
    /// </summary>
    public enum EventState
    {
        Pending = 0,    // 待触发
        Preview = 1,    // 预告
        Arrival = 2,    // 降临
        Evasion = 3,    // 避险
        Resolution = 4, // 结算
        Ended = 5       // 结束
    }

    /// <summary>
    /// 状态机触发事件。
    /// </summary>
    public enum EventTrigger
    {
        Tick = 0,       // tick 推进
        PreviewDone = 1, // 预告完成
        PlayerStay = 2,   // 玩家留下
        PlayerLeave = 3,  // 玩家离开
        Resolve = 4,      // 结算
        End = 5           // 结束
    }

    /// <summary>
    /// 锚点解析上下文。
    /// </summary>
    public readonly struct AnchorContext
    {
        public readonly ulong WorldSeed;
        public readonly long Tick;
        public readonly int ExpeditionTarget; // 出诊目标（-1 = 无）
        public readonly int GatherPointCount; // 采集点数量

        public AnchorContext(ulong worldSeed, long tick, int expeditionTarget, int gatherPointCount)
        {
            WorldSeed = worldSeed;
            Tick = tick;
            ExpeditionTarget = expeditionTarget;
            GatherPointCount = gatherPointCount;
        }
    }

    /// <summary>
    /// spawn_anchor 解析器。
    /// </summary>
    public static class EventAnchor
    {
        /// <summary>
        /// 解析 CLINIC_FRONT 锚点。
        /// </summary>
        public static int ResolveClinicFront(AnchorContext ctx)
        {
            // 简化版：返回固定锚点（完整版需要查询烘焙逻辑层）
            return 0;
        }

        /// <summary>
        /// 解析 TRAVEL_PATH 锚点。
        /// </summary>
        public static int ResolveTravelPath(AnchorContext ctx)
        {
            // 简化版：出诊目标缺失时回退 CLINIC_FRONT
            if (ctx.ExpeditionTarget < 0)
            {
                return ResolveClinicFront(ctx);
            }
            return ctx.ExpeditionTarget;
        }

        /// <summary>
        /// 解析 GATHER_POINT 锚点。
        /// </summary>
        public static int ResolveGatherPoint(AnchorContext ctx)
        {
            // 简化版：S mod count
            if (ctx.GatherPointCount <= 0) return 0;
            ulong hash = SplitMix64.Hash((long)ctx.WorldSeed, ctx.Tick, 0);
            return (int)(hash % (ulong)ctx.GatherPointCount);
        }

        /// <summary>
        /// 解析锚点（统一入口）。
        /// </summary>
        public static int Resolve(SpawnAnchor anchor, AnchorContext ctx)
        {
            switch (anchor)
            {
                case SpawnAnchor.ClinicFront: return ResolveClinicFront(ctx);
                case SpawnAnchor.TravelPath: return ResolveTravelPath(ctx);
                case SpawnAnchor.GatherPoint: return ResolveGatherPoint(ctx);
                default: throw new ArgumentException($"未知锚点: {anchor}");
            }
        }
    }

    /// <summary>
    /// 事件状态机。
    /// </summary>
    public static class EventStateMachine
    {
        // 六态迁移表（闭集）
        private static readonly Dictionary<EventState, Dictionary<EventTrigger, EventState>> TransitionTable =
            new Dictionary<EventState, Dictionary<EventTrigger, EventState>>
            {
                { EventState.Pending, new Dictionary<EventTrigger, EventState>
                    {
                        { EventTrigger.Tick, EventState.Preview },
                        { EventTrigger.End, EventState.Ended }
                    }
                },
                { EventState.Preview, new Dictionary<EventTrigger, EventState>
                    {
                        { EventTrigger.PreviewDone, EventState.Arrival },
                        { EventTrigger.PlayerLeave, EventState.Evasion },
                        { EventTrigger.End, EventState.Ended }
                    }
                },
                { EventState.Arrival, new Dictionary<EventTrigger, EventState>
                    {
                        { EventTrigger.PlayerStay, EventState.Resolution },
                        { EventTrigger.PlayerLeave, EventState.Evasion },
                        { EventTrigger.End, EventState.Ended }
                    }
                },
                { EventState.Evasion, new Dictionary<EventTrigger, EventState>
                    {
                        { EventTrigger.End, EventState.Ended }
                    }
                },
                { EventState.Resolution, new Dictionary<EventTrigger, EventState>
                    {
                        { EventTrigger.Resolve, EventState.Ended },
                        { EventTrigger.End, EventState.Ended }
                    }
                },
                { EventState.Ended, new Dictionary<EventTrigger, EventState>
                    {
                        { EventTrigger.End, EventState.Ended }
                    }
                }
            };

        /// <summary>
        /// 执行状态迁移。
        /// </summary>
        public static EventState Transition(EventState current, EventTrigger trigger)
        {
            if (!TransitionTable.TryGetValue(current, out var triggers))
            {
                throw new InvalidOperationException($"状态 {current} 未定义迁移表");
            }

            if (!triggers.TryGetValue(trigger, out var next))
            {
                throw new InvalidOperationException($"非法迁移: {current} + {trigger}");
            }

            return next;
        }

        /// <summary>
        /// 验证迁移表闭集。
        /// </summary>
        public static bool ValidateClosedSet()
        {
            foreach (var state in TransitionTable.Keys)
            {
                foreach (var trigger in TransitionTable[state].Keys)
                {
                    var next = TransitionTable[state][trigger];
                    if (!TransitionTable.ContainsKey(next))
                    {
                        return false;
                    }
                }
            }
            return true;
        }
    }
}
