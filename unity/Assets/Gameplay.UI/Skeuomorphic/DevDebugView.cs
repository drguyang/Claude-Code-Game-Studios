// 权威来源:design/gdd/skeuomorphic-ui.md 规则九 + AC-42-A6 / AC-42-D4
//   · production/epics/skeuomorphic-ui/story-018-dev-debug-view.md
//
// 设计说明:
//   · 整个类型包在 #if 内 — Release 构建中代码路径不存在(AC-42-A6)。
//   · 读到的 DTO 成员 ⊆ 白名单(枚举 / bool / count 字段);不读游戏量(AC-42-D4)。

#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace DaYiJingCheng.Gameplay.UI.Skeuomorphic
{
    using System.Collections.Generic;

    /// <summary>
    /// 开发者调试视图(规则九)—— 焦点栈 / 元件库 / DTO 绑定结果;不显示游戏数值。
    /// <para>AC-42-A6:本文件整体在 Release 构建中不存在。</para>
    /// <para>AC-42-D4:只读白名单成员(枚举 / bool / count),禁读游戏量字段。</para>
    /// </summary>
    public sealed class DevDebugView
    {
        /// <summary>调试视图可见性(仅开发期)。</summary>
        public bool Visible { get; set; }

        /// <summary>读取的白名单快照(count 类型,合法)。</summary>
        public IReadOnlyList<string> WhitelistedReadings { get; private set; } = new List<string>();

        /// <summary>登记一条白名单读数(枚举名 / bool / count)。</summary>
        public void Record(string name, int count)
        {
            var list = new List<string>(WhitelistedReadings) { $"{name}={count}" };
            WhitelistedReadings = list;
        }
    }
}
#endif
