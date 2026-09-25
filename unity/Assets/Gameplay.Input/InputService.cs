// Story 001 · 输入服务(系统 3)—— 全案唯一 InputActionAsset 实例的持有者
//
// 权威来源:
//   AC-3-A1(BLOCKING)单引用同一性 —— 断言实例同一性(比较 GetInstanceID(),
//     非比较文件路径或文件计数);“文件计数 = 1”型判据已明文废弃。
//   ADR-011 §一(一套动作资产统一组织三套绑重)· §Risks-A S2(判据层只断
//     「实例同一性」性质,不依赖 Instantiate()/Clone() 的符号形状)。
//   GDD input-system.md 规则一(全案只有一个 .inputactions)。
//
// P0 最小面(本故事只交付这三件,其余归别故事):
//   ① 持实例(构造注入,恰一个 InputActionAsset 引用);
//   ② 暴露访问(Actions 属性,恒返回同一实例);
//   ③ 同一性断言辅助(IsSameAssetInstance / EnsureSameAssetInstance,按 GetInstanceID)。
// 不做:急救直读(Story 007)· overrides(Story 003)· 焦点意图(Story 009)·
//      资产装载路径(Boot/Addressables 装载归后续故事,本类只消费已装载实例)。
//
// 用法示例:
//   var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(
//       "Assets/InputSystem_Actions.inputactions");   // 装载方(后续故事)换成 Addressables
//   var service = new InputService(asset);
//   var a1 = service.Actions;
//   var a2 = service.Actions;
//   Debug.Assert(InputService.IsSameAssetInstance(a1, a2));      // true(同一实例)
//   service.EnsureSameAssetInstance(otherAsset);                 // 不同实例 ⇒ 抛 InvalidOperationException

using System;
using UnityEngine.InputSystem;

namespace DaYiJingCheng.Gameplay.Input
{
    /// <summary>输入服务(系统 3)—— 恰持有 <b>一个</b> <see cref="InputActionAsset"/> 实例,
    /// 并提供按 <c>GetInstanceID()</c> 的实例同一性断言(AC-3-A1)。</summary>
    /// <remarks>
    /// 单实例纪律:本类不提供“再装载一次”的入口 —— 装载(唯一一次)由调用方完成并注入;
    /// 同一性判断只看 <c>GetInstanceID()</c>,<b>不看文件路径、不看文件计数</b>
    /// (路径比较会放过「同文件被实例化两次」的分叉,那正是 AC-3-A1 废弃旧判据的原因)。
    /// P1b 同机多玩家的每玩家 <c>Instantiate()</c> 克隆在本故事不实现 —— 「须每玩家
    /// Instantiate」出 ADR-011 §一 / Amendment A ②;「P1b 才做」的时序出 GDD input-system.md
    /// 与本故事 Out of Scope;克隆体不得传入本服务的同一性断言并期待为 true。
    /// </remarks>
    public sealed class InputService
    {
        /// <summary>本服务持有的唯一动作资产实例(构造后永不更换)。</summary>
        private readonly InputActionAsset _actions;

        /// <summary>以唯一动作资产实例构造输入服务。</summary>
        /// <param name="actions">已装载的动作资产实例;不得为 null。
        /// 全案恰一个 <c>.inputactions</c>(规则一),装载方只应装载一次。</param>
        /// <exception cref="ArgumentNullException"><paramref name="actions"/> 为 null。</exception>
        /// <example>
        /// <code>
        /// var asset = /* 装载方:Assets/InputSystem_Actions.inputactions */;
        /// var service = new InputService(asset);
        /// </code>
        /// </example>
        public InputService(InputActionAsset actions)
        {
            if (actions == null)
                throw new ArgumentNullException(nameof(actions));
            _actions = actions;
        }

        /// <summary>本服务持有的动作资产实例 —— 每次读取返回<b>同一对象</b>(AC-3-A1 前半条)。</summary>
        /// <example><code>int id1 = service.Actions.GetInstanceID();
        /// int id2 = service.Actions.GetInstanceID();
        /// // id1 == id2(同一实例,非路径比较)</code></example>
        public InputActionAsset Actions => _actions;

        /// <summary>本服务持有实例的 <c>GetInstanceID()</c>(现取现比,勿跨会话持久化 —— 跨会话 id 会变)。</summary>
        /// <example><code>int id = service.ActionsInstanceId;</code></example>
        public int ActionsInstanceId => _actions.GetInstanceID();

        /// <summary>实例同一性判据(AC-3-A1):比较 <c>GetInstanceID()</c>,
        /// <b>非</b>比较文件路径或文件计数。null 或已销毁对象参与比较 ⇒ false。</summary>
        /// <param name="a">资产实例甲(可为 null)。</param>
        /// <param name="b">资产实例乙(可为 null)。</param>
        /// <returns>两参数为同一实例时 true;否则 false(含「同文件的两份实例化」)。</returns>
        /// <example>
        /// <code>
        /// var clone = UnityEngine.Object.Instantiate(asset); // 反例夹具:同文件的第二份实例
        /// bool same = InputService.IsSameAssetInstance(asset, clone); // false(证明判据不是路径比较)
        /// </code>
        /// </example>
        public static bool IsSameAssetInstance(InputActionAsset a, InputActionAsset b)
        {
            // UnityEngine.Object 的 == 重载会把「已销毁」判为 null —— 先走它再取 GetInstanceID,
            // 避免对已销毁对象调 GetInstanceID 抛 MissingReferenceException。
            if (a == null || b == null)
                return false;
            if (ReferenceEquals(a, b))
                return true;
            return a.GetInstanceID() == b.GetInstanceID();
        }

        /// <summary>单实例纪律断言:<paramref name="other"/> 必须与本服务持有的是同一实例,
        /// 否则抛 <see cref="InvalidOperationException"/>(消息含双方 instance id,便于诊断)。</summary>
        /// <param name="other">待比对的资产实例(可为 null ⇒ 抛)。</param>
        /// <exception cref="InvalidOperationException">不是同一实例时抛出。</exception>
        /// <example>
        /// <code>
        /// var clone = UnityEngine.Object.Instantiate(service.Actions);
        /// service.EnsureSameAssetInstance(clone); // 抛 InvalidOperationException(反例夹具)
        /// </code>
        /// </example>
        public void EnsureSameAssetInstance(InputActionAsset other)
        {
            if (IsSameAssetInstance(_actions, other))
                return;
            int ours = _actions != null ? _actions.GetInstanceID() : 0;
            int theirs = other != null ? other.GetInstanceID() : 0;
            throw new InvalidOperationException(
                $"输入服务动作资产实例同一性被破坏(AC-3-A1):持有实例 id={ours},传入实例 id={theirs}。" +
                "同一性按 GetInstanceID 判定,非文件路径;同文件的第二份实例化即为违规。");
        }
    }
}
