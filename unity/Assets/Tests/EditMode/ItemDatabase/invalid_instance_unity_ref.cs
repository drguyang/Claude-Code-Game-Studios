// AC-21a-27 负向夹具(GDD 点名 invalid_instance_unity_ref.cs —— 静态断言夹具,故意违例)。
//
// ⚠️ 只进**测试程序集**(能见 UnityEngine;Sim.Contracts 零引擎引用、Gates 按 FullName 判 ——
//    unity-specialist 约束④:勿落 Sim.Contracts / Gates)。
// 覆盖 GDD §Schema E「递归类型图三类漏检」:
//   ① 一层直接 UnityEngine.Sprite 字段
//   ② 二层嵌套类内携带 GameObject(字段类型自身无引擎引用,须递归才抓得到)
//   ③ 接口字段 / 抽象类字段 / [SerializeReference] 字段(可装任意子类携带引用)
//   ④ 泛型容器实参携带(List<Sprite> / Dictionary<, GameObject> —— F2 修的回归证物)

namespace DaYiJingCheng.Tests.Unit.ItemDatabase
{
    /// <summary>违例根:一层直接引用(Sprite)+ 二层嵌套类入口(Carrier 自身零引擎引用)。</summary>
    internal sealed class InvalidInstanceUnityRef
    {
        /// <summary>一层直接引擎引用(扫描应直接命中)。</summary>
        public UnityEngine.Sprite Icon;

        /// <summary>二层入口:本字段类型零引擎引用,须递归进 <see cref="InvalidNestedCarrier"/> 才命中。</summary>
        public InvalidNestedCarrier Carrier;
    }

    /// <summary>嵌套类:自身可被误判为「无引擎引用」,字段实际携带 GameObject(GDD 漏检②)。</summary>
    internal sealed class InvalidNestedCarrier
    {
        /// <summary>嵌套类内携带的引擎引用(递归类型图须抓到)。</summary>
        public UnityEngine.GameObject Prefab;
    }

    /// <summary>接口字段的目标接口(GDD 漏检③:接口字段可装任意子类)。</summary>
    internal interface IInvalidSpriteHolder
    {
    }

    /// <summary>接口的具体实现:子类内塞引擎引用 —— 接口字段的「子类携带」半边。</summary>
    internal sealed class InvalidSpriteHolderImpl : IInvalidSpriteHolder
    {
        /// <summary>子类内携带的 Sprite(直接扫实现类时应命中)。</summary>
        public UnityEngine.Sprite Sprite;
    }

    /// <summary>接口字段形态:字段类型是接口 ⇒ 直判违例(不递归,但违例必须报)。</summary>
    internal sealed class InvalidViaInterface
    {
        /// <summary>接口字段(可装 <see cref="InvalidSpriteHolderImpl"/> 一类子类)。</summary>
        public IInvalidSpriteHolder Holder;
    }

    /// <summary>[SerializeReference] + object 双违例形态(GDD 漏检① 的属性半边)。</summary>
    internal sealed class InvalidSerializeReference
    {
        /// <summary>SerializeReference 允许任意子类;object 字段自身也是禁形。</summary>
        [UnityEngine.SerializeReference]
        public object Payload;
    }

    /// <summary>泛型容器携带形态(F2 修,2026-09-23 双评审 S1):字段类型
    /// <c>List&lt;Sprite&gt;</c> / <c>Dictionary&lt;, GameObject&gt;</c> 自身在 System.Collections.*,
    /// 扫描器必须解泛型实参才抓得到 —— 不解则走「基座类型不递归」分支静默漏检。</summary>
    internal sealed class InvalidViaGenericContainer
    {
        /// <summary>List 实参携带引擎引用(修前漏检)。</summary>
        public System.Collections.Generic.List<UnityEngine.Sprite> Attachments;

        /// <summary>Dictionary 第二实参携带引擎引用(修前漏检);string 实参应被跳过。</summary>
        public System.Collections.Generic.Dictionary<string, UnityEngine.GameObject> PrefabMap;

        /// <summary>对照:纯 BCL 实参的泛型容器完全合法(ItemInstance.Children 同族)。</summary>
        public System.Collections.Generic.List<long> Ids;

        /// <summary>嵌套容器携带引擎引用(F7 修):外层 args = [string, List&lt;Sprite&gt;],
        /// 内层 List&lt;Sprite&gt; 必须回环进完整判据链 —— 不回环则被基座类型分支跳过、静默漏检。</summary>
        public System.Collections.Generic.Dictionary<string,
            System.Collections.Generic.List<UnityEngine.Sprite>> NestedList;

        /// <summary>对照(F7):纯 BCL 嵌套容器不得误报。</summary>
        public System.Collections.Generic.Dictionary<string,
            System.Collections.Generic.List<long>> CleanNested;
    }

    /// <summary>抽象类字段的基类(GDD §Schema E 漏检①「接口/抽象」的抽象侧;
    /// 实现方可携带引擎引用 —— Banner)。</summary>
    internal abstract class InvalidAbstractHolderBase
    {
        /// <summary>抽象基类自身携带的引擎引用(子类形态的证物,扫描走直判路径不依赖它)。</summary>
        public UnityEngine.Sprite Banner;
    }

    /// <summary>抽象类字段形态(F3 修,2026-09-23 双评审 qa GAPS):字段类型是抽象类 ⇒
    /// 扫描器 PodTypeScanner 直判违例(接口/抽象/object 分支),不递归。</summary>
    internal sealed class InvalidViaAbstract
    {
        /// <summary>抽象类字段(可装任意子类携带引擎引用)。</summary>
        public InvalidAbstractHolderBase Holder;
    }
}
