namespace DaYiJingCheng.Gameplay.UI.Skeuomorphic
{
    using System;
    using System.Collections.Generic;

    /// <summary>九宫格边框值断言工具(AC-42-C1)。</summary>
    public static class NineSliceBoundsValidator
    {
        /// <summary>验证单条 slice 值合法性: > 0 且 < 半尺寸。</summary>
        public static bool IsValid(int slice, int halfSize)
        {
            return slice > 0 && slice < halfSize;
        }

        /// <summary>验证一组九宫格边框值。</summary>
        public static IReadOnlyList<string> Validate(string elementName, int left, int right, int top, int bottom, int width, int height)
        {
            var errs = new List<string>();
            int halfWidth = width / 2;
            int halfHeight = height / 2;

            if (!IsValid(left, halfWidth))
                errs.Add($"[C1] 「{elementName}」-unity-slice-left={left} 不在 (0, {halfWidth})。");
            if (!IsValid(right, halfWidth))
                errs.Add($"[C1] 「{elementName}」-unity-slice-right={right} 不在 (0, {halfWidth})。");
            if (!IsValid(top, halfHeight))
                errs.Add($"[C1] 「{elementName}」-unity-slice-top={top} 不在 (0, {halfHeight})。");
            if (!IsValid(bottom, halfHeight))
                errs.Add($"[C1] 「{elementName}」-unity-slice-bottom={bottom} 不在 (0, {halfHeight})。");

            return errs;
        }
    }
}
