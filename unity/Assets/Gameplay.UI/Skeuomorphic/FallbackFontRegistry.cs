#nullable enable

namespace DaYiJingCheng.Gameplay.UI.Skeuomorphic
{
    using System;
    using System.Collections.Generic;

    /// <summary>fallback 字体注册表(AC-42-C6:中英文至少各一)。</summary>
    public static class FallbackFontRegistry
    {
        private const string ChineseSlot = "chinese";
        private const string EnglishSlot = "english";

        private static readonly Dictionary<string, string> _fonts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { ChineseSlot, "NotoSerifCJKsc-Regular" },
            { EnglishSlot, "NotoSerif-Regular" }
        };

        private static bool _locked;

        /// <summary>注册一个 fallback 字体槽位(构建期完成)。</summary>
        public static void Register(string slot, string fontName)
        {
            if (_locked) throw new InvalidOperationException("[FallbackFontRegistry] 注册表已锁定。");
            if (string.IsNullOrWhiteSpace(slot)) throw new ArgumentException("slot 不能为空。", nameof(slot));
            if (string.IsNullOrWhiteSpace(fontName)) throw new ArgumentException("fontName 不能为空。", nameof(fontName));
            _fonts[slot] = fontName;
        }

        /// <summary>锁定注册表,运行期只读。</summary>
        public static void Lock() => _locked = true;

        /// <summary>获取某槽位的字体名。</summary>
        public static string Get(string slot)
        {
            if (_fonts.TryGetValue(slot, out var name)) return name;
            throw new KeyNotFoundException($"[FallbackFontRegistry] 字体槽位「{slot}」未登记(AC-42-C6)。");
        }

        /// <summary>断言两个必需槽位均存在。</summary>
        public static IReadOnlyList<string> ValidateFallbacks()
        {
            var errs = new List<string>();
            if (!_fonts.ContainsKey(ChineseSlot))
                errs.Add($"[C6] 中文字体槽位「{ChineseSlot}」未登记。");
            if (!_fonts.ContainsKey(EnglishSlot))
                errs.Add($"[C6] 英文字体槽位「{EnglishSlot}」未登记。");
            return errs;
        }

        /// <summary>返回全部槽位快照。</summary>
        public static IReadOnlyDictionary<string, string> All => _fonts;
    }
}
