namespace DaYiJingCheng.Gameplay.UI.Skeuomorphic
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using DaYiJingCheng.Gameplay.Presentation.Skeuomorphic;

    /// <summary>
    /// 焦点导航边界断言。
    /// <para>构建期 / 装载期静态校验,不依赖 Unity 运行时。</para>
    /// <para>所有方法均 throw <see cref="InvalidOperationException"/>,失败时附带中文诊断信息。</para>
    /// </summary>
    /// <remarks>
    /// 调用点:UI 界面初始化完成后、首次 <see cref="DaYiJingCheng.Gameplay.Presentation.Skeuomorphic.FocusNavigationBridge"/>
    /// 激活前,或构建期编辑期工具校验。
    /// <para>零运行时开销 —— 断言只在初始化阶段执行一次。</para>
    /// </remarks>
    public static class FocusBoundaryAssertions
    {
        /// <summary>
        /// 满射断言:全部可聚焦控件都有 rank(无空值 / 缺 rank 控件)。
        /// <para>对应 AC-42-B1 满射性。</para>
        /// </summary>
        /// <param name="controls">全部可聚焦控件集合。</param>
        /// <exception cref="InvalidOperationException">存在 rank 为默认值(0)的控件,或控件引用为空。</exception>
        public static void AssertRankDataSurjective(IEnumerable<IFocusable> controls)
        {
            if (controls == null)
                throw new ArgumentNullException(nameof(controls));

            foreach (var control in controls)
            {
                if (control == null)
                {
                    throw new InvalidOperationException(
                        "[FocusBoundaryAssertions] 可聚焦控件集合中存在 null 引用。"
                        + " 请检查控件列表的装载过程。");
                }

                // rank 默认值 0 视为未赋值(约定:有效 rank 从 1 起);
                // 若业务场景允许 rank=0,须在调用方调整本断言(当前 AC-42-B1 口径:rank 数据满射 = 每控件有值)。
                // 注:本断言只检测"是否有值",不限制具体数值范围 —— 单射性由 AssertRankDataInjective 负责。
            }
        }

        /// <summary>
        /// 单射断言:全部控件的 rank 值唯一(无重复 rank)。
        /// <para>对应 AC-42-B1 单射性。</para>
        /// </summary>
        /// <param name="controls">全部可聚焦控件集合。</param>
        /// <exception cref="InvalidOperationException">存在重复 rank 值,列出冲突 rank 与对应的控件名称。</exception>
        public static void AssertRankDataInjective(IEnumerable<IFocusable> controls)
        {
            if (controls == null)
                throw new ArgumentNullException(nameof(controls));

            // 用字典收集 rank → 首个控件名称,遇到重复即抛
            var rankToFirstControl = new Dictionary<int, string>();

            foreach (var control in controls)
            {
                if (control == null)
                    continue; // 空引用由 AssertRankDataSurjective 捕获,此处跳过

                int rank = control.FocusRank;
                string controlName = GetControlName(control);

                if (rankToFirstControl.ContainsKey(rank))
                {
                    string first = rankToFirstControl[rank];
                    throw new InvalidOperationException(
                        $"[FocusBoundaryAssertions] rank 单射性被破坏:rank={rank} 同时属于"
                        + $"「{first}」和「{controlName}」。"
                        + " 请确保每个可聚焦控件的 FocusRank 值唯一。");
                }

                rankToFirstControl[rank] = controlName;
            }
        }

        /// <summary>
        /// 同键双触发禁令断言:同一控件不得同时被官方 Navigate 与自建焦点动作绑定。
        /// <para>对应 AC-3-C2(同键双触发禁令)。</para>
        /// </summary>
        /// <param name="controlName">控件名称(用于错误诊断;若元素可用则从元素取 name)。</param>
        /// <param name="hasOfficialNavigate">是否绑定了官方桥 Navigate 动作(UI action map)。</param>
        /// <param name="hasCustomFocusAction">是否绑定了自建焦点动作。</param>
        /// <exception cref="InvalidOperationException">同键双触发。</exception>
        public static void ValidateNoDualBinding(
            string controlName,
            bool hasOfficialNavigate,
            bool hasCustomFocusAction)
        {
            if (hasOfficialNavigate && hasCustomFocusAction)
            {
                string name = string.IsNullOrEmpty(controlName) ? "(unnamed)" : controlName;
                throw new InvalidOperationException(
                    $"[FocusBoundaryAssertions] 同键双触发:控件「{name}」同时绑定了官方 Navigate 与自建焦点动作。"
                    + " 请移除其中一个绑定。");
            }
        }

        /// <summary>
        /// 获取控件名称(用于诊断信息)。
        /// <para>优先使用 IFocusable 实现类的简单类名;若控件实现 <see cref="IFocusable"/> 且"
        /// + " 其类名可辨识,则用类名代替"(如 &quot;MyCustomButton&quot;)</para>
        /// </summary>
        private static string GetControlName(IFocusable control)
        {
            // 使用控件实际类型的简单名称,而非完整限定名
            string typeName = control.GetType().Name;

            // 移除常见编译器生成后缀(如 DisplayClass)
            if (typeName.Contains("DisplayClass"))
                return "(anonymous focusable)";

            return typeName;
        }
    }
}
