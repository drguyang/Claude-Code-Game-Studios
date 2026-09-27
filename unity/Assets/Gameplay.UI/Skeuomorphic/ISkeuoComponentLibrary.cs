namespace DaYiJingCheng.Gameplay.UI.Skeuomorphic
{
    using System;
    using UnityEngine.UIElements;

    /// <summary>拟物元件库工厂接口(ADR-013 §四 Key Interfaces · ISkeuoElementLibrary)。</summary>
    public interface ISkeuoComponentLibrary
    {
        /// <summary>按元件种类创建 USS 类 + 九宫格绑定的 VisualElement。</summary>
        VisualElement Create(SkeuoElement kind);
    }
}
