// 截图工具(编辑期 · ADR-025 §① `Editor.Tools` 族 · 2026-10-14 立)
//
// ▍用途
//   调试 / playtest 留证时快速截图。**不做第三方插件** —— 项目零第三方取向
//   (ADR-017/018/019/020 连续拒第三方运行期依赖;「官方包」亦非豁免),
//   故走自研编辑期工具,效果等同、不破纪律。
//
// ▍⚠️ 硬边界:本工具**只在 Play 模式有效**
//   项目已两次独立实测同一边界:
//   ① 2026-10-13(S3 可读性轮):`ScreenCapture` 在无渲染宿主(batch)抛异常、PNG 零字节,
//      `Screen.width=640×480` 无有效帧(见 `font_rendering_path_spike_test.cs` SPIKE-D);
//   ② 2026-10-14(S2 PAGES_MAX spike · 18 轮):batch 下 UI Toolkit 纹理永不注册进图集
//      (需真 GPU 绘制 pass),同一 `Screen=640×480`。
//   ⇒ **batch 出不了图是结构性事实,非本工具缺陷**。本工具服务于「编辑器 Play / 真机」,
//     这正是它该在的地方。
//
// ▍用法
//   - 编辑器聚焦 Game 视图 → 按 **F12** ⇒ 存 `Logs/shots/<时间戳>.png`(2× 高清)
//   - 或菜单 `大医精诚/Capture/…`(含 1× / 2× / 4× 三档)
//   ⚠️ `ScreenCapture.CaptureScreenshot` **实际写盘延后一两帧** —— 按完别立刻退 Play。
//      F12 快捷项绑在菜单项上 ⇒ **编辑器 Play 中**生效(`[MenuItem]` 不监听 runtime 按键)。

using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DaYiJingCheng.EditorTools.Capture
{
    /// <summary>Play 模式截图工具(F12 / 菜单三档倍率)。</summary>
    internal static class ScreenshotTool
    {
        // 落点:项目根下 Logs/shots/(Logs/ 已被 .gitignore 覆盖 ⇒ 截图永不入库)
        private const string ShotDir = "Logs/shots";

        [MenuItem("大医精诚/Capture/截图 1x %F12")]
        private static void Shot1x() => Shot(1);

        [MenuItem("大医精诚/Capture/截图 2x(高清) %#F12")]
        private static void Shot2x() => Shot(2);

        [MenuItem("大医精诚/Capture/截图 4x(超清) %&F12")]
        private static void Shot4x() => Shot(4);

        /// <summary>
        /// 截当前 Game 视图。<paramref name="superSize"/> = 倍率(ScreenCapture 语义:
        /// 实际像素 = 屏幕分辨率 × superSize)。
        /// </summary>
        private static void Shot(int superSize)
        {
            if (!Application.isPlaying)
            {
                // 静默失败会让人以为「截了但找不到」—— 显式告知
                Debug.LogWarning(
                    "[ScreenshotTool] 截图只在 **Play 模式** 有效(Edit 模式无渲染帧 ⇒ 会出零字节 PNG)。" +
                    "请先 Enter Play,再按 F12 / 走菜单。");
                return;
            }

            string dir = Path.Combine(ProjectRoot(), ShotDir);
            Directory.CreateDirectory(dir);

            string name = $"{DateTime.Now:yyyyMMdd_HHmmss}_{superSize}x.png";
            string path = Path.Combine(dir, name);

            ScreenCapture.CaptureScreenshot(path, superSize);
            // ⚠️ 写盘延后一两帧 —— 此处只报「已请求」,不断言文件已存在(那会是假判据)
            Debug.Log($"[ScreenshotTool] 已请求截图 {superSize}× ⇒ {path}" +
                      "(写盘延后 1–2 帧,稍候即现)");
        }

        /// <summary>工程根(= `unity/` 的父目录;`Application.dataPath` = `<root>/unity/Assets`)。</summary>
        private static string ProjectRoot()
            => Directory.GetParent(Application.dataPath)?.Parent?.FullName
               ?? Application.dataPath;

        /// <summary>打开截图目录(方便找刚截的图)。</summary>
        [MenuItem("大医精诚/Capture/打开截图目录")]
        private static void OpenShotDir()
        {
            string dir = Path.Combine(ProjectRoot(), ShotDir);
            Directory.CreateDirectory(dir);
            EditorUtility.RevealInFinder(dir);
        }
    }
}
