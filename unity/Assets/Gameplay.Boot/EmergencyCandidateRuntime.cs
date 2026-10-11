// sprint-07 S3.1 · 急救候选列表的**生产实例化宿主**(2026-10-11)
//
// 权威来源:
//   story-023(急救零数字 + 可跳过)边界裁定 ①:施加点 / 候选动作列表载体 / Idle 态可达
//     时序归 10 实现轮 + 数据绑定轮 —— 本宿主 = 该边界里「**载体**」的生产装配角色
//     (EmergencyCandidateList 为 story-023 形态件③的载体类,sprint-05 T2.4 已验);
//   sprint-07 §2 S3.1(运行期创建列表载体 + 跳过入口 + 候选动作标签喂入(止血/通气);
//     判据 = M2 #8 施加点③闭合:运行期列表实体存在 + 跳过入口可用);
//   ADR-013 §9 C3(42 只渲染不持状态 —— 本宿主只创建并持有载体,零 sim 态)。
//
// ⚠️ **只提供「实例存在」这一层**:候选**文字**由本宿主喂入(10 的最小动作标签),
//   但**动作判定 / Idle 可达时序 / 候选动作的点击行为**全部归 10 + 数据绑定轮 ——
//   本宿主零动作语义、零时序(承 story-023 边界①与 T2.4 反射面判据「零计时字段」)。
// ⚠️ 此「生产实例化」= 回答「形态件③在运行期**有载体**」;「跳过入口真的能跳过」的
//   交互面归 10 轮(同理不上设备级自动行使 —— 若自动跳过 = 玩家零输入出事件 = 假绿)。
//
// ⚠️ asmdef 边界(Gameplay.Boot 可引用 Gameplay.UI;但本测试装配 Sim.Contracts.Tests
//   不含 Gameplay.UI 引用 ⇒ 观测面刻意只暴露 UnityEngine.UIElements + 结构化只读，
//   **不**暴露 EmergencyCandidateList 类型 —— 避免「测试装配须挂新引用」)。

using DaYiJingCheng.Gameplay.UI.Skeuomorphic;
using UnityEngine.UIElements;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>
    /// 急救候选列表生产实例宿主 —— 组合根创建、BootRoot 挂载到 UI 根。
    /// <para>持有 <see cref="EmergencyCandidateList"/> 载体(创建 + 喂入 + 挂载),
    /// 供「运行期列表实体存在 + 跳过入口可用」的判据观测。</para>
    /// </summary>
    public sealed class EmergencyCandidateRuntime
    {
        /// <summary>10 的最小动作标签(急救动作小游戏两条主链)。
        /// <para>⚠️ 接线载体注记:真动作表归 21a / 10 烘焙轮;此处喂入是「有内容」,
        /// 不是「数值真源」(承 S1 勘误同口径:词条已是真词,缺口只在管线)。</para></summary>
        public static readonly string[] DefaultCandidateLabels = { "止血", "通气" };

        private readonly EmergencyCandidateList _list;
        private readonly string[] _labels;

        /// <summary>创建并实例化候选列表(喂入 10 最小动作标签)。</summary>
        public EmergencyCandidateRuntime()
        {
            _labels = (string[])DefaultCandidateLabels.Clone();
            _list = new EmergencyCandidateList();
            _list.Build(_labels);
        }

        /// <summary>候选列表根(挂载观测面 —— 挂载后 <c>Root.parent == UI 根</c>;
        /// 只暴露 <see cref="UnityEngine.UIElements.VisualElement"/>,不跨装配边界暴露
        /// Gameplay.UI 类型 —— 消费方(测试装配)无需引 Gameplay.UI)。</summary>
        public UnityEngine.UIElements.VisualElement Root => _list.Root;

        /// <summary>已喂入的候选动作标签(观测面;快照,调用方只读)。</summary>
        public System.Collections.Generic.IReadOnlyList<string> CandidateLabels => _labels;

        /// <summary>跳过入口是否已实例化(恒 true —— 可跳过是结构承诺,story-023)。</summary>
        public bool HasSkipEntry => _list.HasSkipEntry;

        /// <summary>候选项数(不含跳过入口;喂入 = 2)。</summary>
        public int EntryCount => _list.EntryCount;

        /// <summary>
        /// 挂到 UI 根(生产:BootRoot 在 <c>_uiDriver</c> 就绪后调用;
        /// 测试 / 无 panel 环境:传 null 根 = 合法不挂,树仍在 —— 只渲染的呈现纪律)。
        /// </summary>
        /// <param name="root">UI 根(UIDocument.rootVisualElement);null = no-op。</param>
        public void MountTo(UnityEngine.UIElements.VisualElement root)
        {
            if (root == null) return;
            if (_list.Root.parent != root)
                root.Add(_list.Root);
        }
    }
}