// 48 的**零设门**守卫(AC-48-17)—— 反射扫 `Gameplay.UI.Tutorial` 的公开面,证明「想设门也拿不到接口」。
//
// 权威来源:
//   `design/gdd/tutorial-and-onboarding.md` **规则五**(零设门)· **AC-48-17**:
//     「零解锁 / 门禁 / 完成状态查询 API」,由反射 / 接口面断言守住
//   `design/ux/tutorial-48.md:57` —— 「48 的 API 面被 AC-48-17 断言禁止暴露进度查询;
//     若本屏画步列表,等于用 UI 绕过该断言」
//   Story: `production/epics/tutorial-onboarding/story-005-zero-gate-zero-keyhint-guards-walkthrough.md`
//
// ── 为什么是「API 面不存在」而不是「没有显示」────────────────────────────
// 规则五的要求是**做不到**,不是**没做**:任何玩法系统(病例生成 / 建造 / 出诊)都**不能**
// 拿「玩家还在引导期」去改自己的行为。只要 48 的公开面上还有一个 `IsTutorialActive()`,
// 这条规则就退化成口头承诺 —— 下一个系统顺手一查,门就出现了。
//
// ── 判据(两条)────────────────────────────────────────────────────────────
//   G1 **标识符黑名单**:公开类型名 / 公开成员名命中
//      {progress, unlocked, gate, inTutorial, tutorialState, canAccess}(OrdinalIgnoreCase 子串)⇒ 违例
//   G2 **返回类型黑名单**:公开方法 / 属性 / 字段的**类型名**命中同一词表 ⇒ 违例
//      (挡住「方法名改叫 `Get()` 但返回 `TutorialProgressView`」这条改名旁路)
//
// ⚠️ 词表是**机械判据**,不是语义判据 —— 它抓的是「长成了门的样子」。
//    真正的语义守卫是 GDD 规则五本身;本门的价值在于**可执行、会在构建期红**。
//
// ⚠️ 扫描面 = **`GetExportedTypes()`**(公开类型)+ 其 `DeclaredOnly` 公开成员。
//    内部类型(将来若把投影内化)天然不在面内 —— 那正是「内化」能修好本门的机制。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace DaYiJingCheng.EditorTools.Gates
{
    /// <summary>AC-48-17 的执行体:48 公开面零「解锁 / 门禁 / 完成状态」查询 API。</summary>
    public static class TutorialZeroGate
    {
        /// <summary>48 的装配名(与 `ADR-025 §①` / `AssemblyGates.Manifest` 同一出处)。</summary>
        public const string TutorialAssemblyName = "Gameplay.UI.Tutorial";

        /// <summary>黑名单词表(小写;匹配 = 标识符小写后**子串**包含)。</summary>
        public static readonly string[] GateTokens =
        {
            "progress",     // 教学进度查询
            "unlocked",     // 解锁查询
            "gate",         // 门禁查询
            "intutorial",   // 「是否处于引导期」
            "tutorialstate",// 教学状态枚举
            "canaccess",    // 访问判定
        };

        /// <summary>48 目录下**允许**存在的文件后缀(资产闭集:AC-48-01/02 的 48 侧半边)。
        /// <para>48 **零自有资产** —— 纸的图走 6 的世界资产、UI 走 42 的元件库;
        /// 任何 `.uxml` / `.uss` / 图片落进本目录都是「48 自己开始画东西」的指纹。</para></summary>
        public static readonly string[] AllowedFileSuffixes =
        {
            ".cs", ".asmdef", ".meta", ".md",
        };

        /// <summary>构建期入口 —— 按名载入 48 装配并扫其公开面。</summary>
        /// <returns>违例串列表(空 = 全过)。</returns>
        public static List<string> RunAll()
        {
            Assembly asm;
            try
            {
                asm = Assembly.Load(TutorialAssemblyName);
            }
            catch (Exception ex)
            {
                // 装配缺失 / 改名 ⇒ 判据无法成立。**不静默跳过**(那会让 AC-48-17 悄然消失)。
                return new List<string>
                {
                    $"[AC-48-17] 找不到装配「{TutorialAssemblyName}」—— 零设门判据无法成立:" +
                    $"{ex.GetType().Name}: {ex.Message}",
                };
            }

            return ScanPublicSurface(asm.GetExportedTypes(), TutorialAssemblyName);
        }

        /// <summary>公开面扫描(纯函数,给定类型集 —— 测试用它喂合成类型做负夹具)。</summary>
        public static List<string> ScanPublicSurface(IEnumerable<Type> types, string label)
        {
            var errs = new List<string>();
            var scanned = 0;

            foreach (Type t in (types ?? Enumerable.Empty<Type>()).Where(t => t != null))
            {
                scanned++;
                if (HitToken(t.Name, out string token))
                {
                    errs.Add($"[AC-48-17] {label} 的**公开类型**「{t.Name}」命中门禁词「{token}」—— " +
                             "零设门要求这类查询面**不存在**;内化(internal + InternalsVisibleTo)或改名都不是等价物," +
                             "先确认它真的不是玩法可查的门。");
                }

                foreach (MemberInfo m in PublicDeclaredMembers(t))
                {
                    if (HitToken(m.Name, out string memberToken))
                    {
                        errs.Add($"[AC-48-17] {label} 的公开成员「{t.Name}.{m.Name}」命中门禁词「{memberToken}」" +
                                 "—— 玩法系统据此即可设门。");
                    }

                    // G2:名改了但**类型**还是门 —— 「返回 TutorialProgressView 的 Get()」同样是查询面
                    if (TryReturnTypeName(m, out string returnTypeName) &&
                        returnTypeName != null && HitToken(returnTypeName, out string typeToken))
                    {
                        errs.Add($"[AC-48-17] {label} 的公开成员「{t.Name}.{m.Name}」**返回类型**" +
                                 $"「{returnTypeName}」命中门禁词「{typeToken}」—— 改名旁路(方法名干净、类型仍露进度)。");
                    }
                }
            }

            if (scanned == 0)
                errs.Add($"[AC-48-17] {label} 的扫描面为 **0 个类型** —— 判据不可执行(非「通过」)。" +
                         "装配载入失败 / 命名空间搬家都会走到这里。");

            return errs;
        }

        /// <summary>资产闭集(AC-48-01/02 的 48 侧):目录下只允许 <see cref="AllowedFileSuffixes"/>。
        /// <para>⚠️ 空目录 / 目录不存在 ⇒ **不报错**(48 今日零自有资产是合法状态);
        /// 但**一旦出现** `.uxml` / `.uss` / 图片,立即红 —— 那意味着 48 开始自绘 HUD 叠加层。</para></summary>
        public static List<string> ScanAssetIndex(string directory)
        {
            var errs = new List<string>();
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                return errs;

            foreach (string file in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
            {
                string ext = Path.GetExtension(file) ?? string.Empty;
                if (AllowedFileSuffixes.Any(s => string.Equals(s, ext, StringComparison.OrdinalIgnoreCase)))
                    continue;

                errs.Add($"[AC-48-01/02] 48 目录里出现非代码资产「{Path.GetFileName(file)}」" +
                         $"({ext}) —— 48 零自有资产:纸的图走 6 的世界资产、UI 走 42 的元件库;" +
                         "任何浮层 / 贴图落在本目录都是「48 自己开始画」的指纹。");
            }

            return errs;
        }

        // ── 内部 ──

        private static bool HitToken(string identifier, out string token)
        {
            token = null;
            if (string.IsNullOrEmpty(identifier)) return false;

            string lower = identifier.ToLowerInvariant();
            foreach (string g in GateTokens)
            {
                if (lower.Contains(g)) { token = g; return true; }
            }
            return false;
        }

        private static IEnumerable<MemberInfo> PublicDeclaredMembers(Type t)
        {
            const BindingFlags Flags = BindingFlags.Public | BindingFlags.Instance |
                                       BindingFlags.Static | BindingFlags.DeclaredOnly;

            foreach (MemberInfo m in t.GetMethods(Flags))
            {
                if (m is MethodInfo mi && (mi.IsSpecialName || mi.IsPrivate)) continue; // 访问器另由属性报
                yield return m;
            }

            foreach (PropertyInfo p in t.GetProperties(Flags)) yield return p;
            foreach (FieldInfo f in t.GetFields(Flags)) yield return f;
            foreach (EventInfo e in t.GetEvents(Flags)) yield return e;
            // 构造函数也是公开面(参数类型可能露门),但不参与 G1/G2 的命名判定 —— 见下方说明。
            // 参数类型扫描会与属性 / 字段重复报同一件事,故只报**类型本身**命中的情况(上面已覆盖)。
        }

        private static bool TryReturnTypeName(MemberInfo m, out string typeName)
        {
            typeName = null;
            switch (m)
            {
                case MethodInfo mi: typeName = mi.ReturnType?.Name; return true;
                case PropertyInfo pi: typeName = pi.PropertyType?.Name; return true;
                case FieldInfo fi: typeName = fi.FieldType?.Name; return true;
                case EventInfo ei: typeName = ei.EventHandlerType?.Name; return true;
                default: return false;
            }
        }
    }
}
