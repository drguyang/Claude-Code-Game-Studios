// PlayMode TearDown 的 `GameObject.Find` 清场门(2026-11-01)
//
// ─────────────────────────────────────────────────────────────────────────────
// 为什么立这条门(一次**偶发红**换来的)
// ─────────────────────────────────────────────────────────────────────────────
//   `death_transition_playmode_test` 在**全量串跑**下**偶发**红:
//     `AC-ADR020-10 AudioListener 单挂点:active 2 个 / 挂载总数 2 个`
//   连续两轮实测:run1 绿 / run2 红 ⇒ **不是确定性缺陷,是枚举序相关的时序缺陷**。
//
//   根因链(四步,每步都实测):
//     ① `camera_follow_wiring_test` 的 TearDown 写
//        `var preset = GameObject.Find("Main Camera"); if (preset != null) preset.SetActive(true);`
//        —— `GameObject.Find` **只查 active 对象**,而该预摆已被启动序 `SetActive(false)`
//        ⇒ `Find` 返回 **null** ⇒ 那句「恢复激活」**恒不执行**,
//        预摆(带 `AudioListener`)**原样留在场上**。
//     ② `death_transition_playmode_test` 自造同名 `Main Camera`(带 listener)。
//     ③ `BootRoot.FindInactiveInclusive("Main Camera")` 用
//        `FindObjectsByType<Camera>(Include, SortMode.None)` 取**第一个**同名相机,
//        并摘掉它的 listener —— 但 `SortMode.None` 的**枚举序不保证稳定**。
//     ④ 枚举序有时先返回**残留的旧件**(摘错 ⇒ 本次新建的 listener 仍在 ⇒ 2 个 ⇒ 抛),
//        有时先返回**本次新建的**(摘对 ⇒ 绿)。⇒ **偶发**。
//
// ─────────────────────────────────────────────────────────────────────────────
// 判据(只钉形态,不钉意图)
// ─────────────────────────────────────────────────────────────────────────────
//   扫 `Assets/Tests/PlayMode/**/*.cs`,凡 `[UnityTearDown]` 的 `TearDown` 方法**体**内
//   出现 `GameObject.Find(` ⇒ **红**。
//
//   ⚠️ **为什么只扫 TearDown 体,不扫全文件**:`GameObject.Find` 在**断言**里是合法的
//   (`Assert.IsNotNull(GameObject.Find("Player"))` —— 那条测的就是「玩家被造出来了」,
//   它要的正是「激活」语义)。禁令只针对**清场**:清场必须看见 inactive 件。
//
//   ⚠️ **为什么不用「禁 GameObject.Find」全局词表**:那是把一条正确的 API 整体判死,
//   会让将来合法的激活态查找也写不了 —— 与 `coding-standards` 的
//   「命名 — 机械门避让表」同族的**过宽判据**风险。
//
// ─────────────────────────────────────────────────────────────────────────────
// 豁免:没有。本门**不设白名单**
// ─────────────────────────────────────────────────────────────────────────────
//   理由:正确的替代写法(`Resources.FindObjectsOfTypeAll` / `FindObjectsByType(..., Include)`)
//   在**任何**清场场景都成立,没有「这一处只能 Find」的情形。
//   ⚠️ 若将来真出现需要豁免的形态,须在本文件头**具名登记理由**并给出该处 `file:line`
//   —— 承 `WriterExistenceGate` 的「具名豁免」口径,不得静默放行。

using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace DaYiJingCheng.EditorTools.Gates
{
    /// <summary>PlayMode TearDown 禁 `GameObject.Find` 清场(见文件头的四步根因链)。</summary>
    public static class PlayModeTeardownFindGate
    {
        /// <summary>扫描目录(相对工程根 `unity/`)。</summary>
        public const string ScanDirRelative = "Assets/Tests/PlayMode";

        /// <summary>禁用形态(在 TearDown 方法体内)。</summary>
        private static readonly Regex Forbidden = new Regex(@"GameObject\s*\.\s*Find\s*\(", RegexOptions.Compiled);

        /// <summary>`TearDown` 方法体(含特性行;非贪婪到首个「缩进与签名同级」的闭括号)。</summary>
        private static readonly Regex TeardownBody = new Regex(
            @"\[UnityTearDown\][\s\S]{0,200}?public\s+IEnumerator\s+TearDown\s*\(\s*\)\s*\{(?<body>[\s\S]*?)\n\s{8,12}\}",
            RegexOptions.Compiled);

        /// <summary>跑门(返回红行;空 = 绿)。</summary>
        /// <param name="projectRoot">工程根(相对或绝对;测试传 ".")。</param>
        public static List<string> RunAll(string projectRoot)
        {
            var errs = new List<string>();
            string dir = Path.Combine(projectRoot, ScanDirRelative);
            if (!Directory.Exists(dir))
            {
                // ⚠️ 目录缺失 ⇒ **不静默绿**(那是「门跑了但扫了个空」的假绿形态 F6/F5)。
                errs.Add($"[pm-teardown-find] 扫描目录不存在:{ScanDirRelative} —— 门无法生效");
                return errs;
            }

            foreach (string file in Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories))
            {
                string text = File.ReadAllText(file);
                string stripped = StripComments(text);

                Match m = TeardownBody.Match(stripped);
                if (!m.Success) continue;   // 无 TearDown ⇒ 本门不适用

                Match bad = Forbidden.Match(m.Groups["body"].Value);
                if (!bad.Success) continue;

                // 报行号(相对工程根,便于定位)。
                int lineNo = LineOfMatch(stripped, m.Groups["body"].Index + bad.Index);
                // 相对工程根展示(⚠️ 用 Path.GetRelativePath,不用字符串 Replace ——
                // `projectRoot == "."` 时 `TrimEnd('.')` 会得到空串 ⇒ 路径拼成 `.AssetsTests…`)。
                string rel = Path.GetRelativePath(projectRoot, file).Replace('\\', '/');
                errs.Add($"[pm-teardown-find] {rel}:{lineNo} TearDown 内用 `GameObject.Find(` —— " +
                         "它**只查 active 对象**,而被启动序 `SetActive(false)` 的预摆恰是 inactive ⇒ " +
                         "清场静默失效 ⇒ 同名件跨测累积 ⇒ 后续 `BootRoot.FindInactiveInclusive` 按" +
                         "不稳定枚举序摘错 listener(`AC-ADR020-10` 偶发红)。" +
                         "改用 `Resources.FindObjectsOfTypeAll<GameObject>()`(含 inactive,排除资产)" +
                         "或 `FindObjectsByType(..., FindObjectsInactive.Include)`");
            }

            return errs;
        }

        /// <summary>把注释剥掉(判据看的是**执行体**,注释里提到 `GameObject.Find` 不算违规)。</summary>
        private static string StripComments(string src)
        {
            string noBlock = Regex.Replace(src, @"/\*.*?\*/", " ", RegexOptions.Singleline);
            return Regex.Replace(noBlock, @"//[^\n]*", " ");
        }

        /// <summary>字符下标 → 1-based 行号。</summary>
        private static int LineOfMatch(string text, int index)
        {
            int line = 1;
            for (int i = 0; i < index && i < text.Length; i++)
                if (text[i] == '\n') line++;
            return line;
        }
    }
}
