// AC-29 跨平台对拍 · Standalone 后端切换器(编辑期工具,不进构建 —— Editor.Tools.Spike 同族)
//   ADR-012 §二:IL2CPP 对拍 = player 构建;EditMode/编辑器 PlayMode 恒为 Mono(F4)⇒
//   Standalone player 必须显式切 IL2CPP 才构成「两个后端 = 两个独立构建」。
// 用法(均 batch,退出后 ProjectSettings 脏 —— 跑完须 SetMono 归位或 git restore):
//   unity run <proj> -- -batchmode -nographics -quit \
//     -executeMethod DaYiJingCheng.EditorTools.Spike.Ac29Il2CppSwitch.SetIl2cpp
//   unity run <proj> -- -batchmode -nographics -quit \
//     -executeMethod DaYiJingCheng.EditorTools.Spike.Ac29Il2CppSwitch.SetMono
// 🔁 2026-11-03:本类此前**落在全局命名空间** —— 而同目录另外 4 个文件、以及 asmdef 的
//    `rootNamespace`,全都是 `DaYiJingCheng.EditorTools.Spike`。`.github/workflows/unity-tests.yml`
//    调的是带命名空间的名字,于是那条 CI 步骤只要真跑起来就会「找不到方法」。
//    ⇒ 补命名空间使代码与 asmdef / workflow 三方一致(**改代码,不改 workflow**)。
//    ⚠️ `-executeMethod` 必须写**完整限定名**,上面的用法行已同步。

using UnityEditor;
using UnityEditor.Build;   // NamedBuildTarget(2026-11-03:`BuildTargetGroup` 重载在 Unity 6 已废弃)
using UnityEngine;

namespace DaYiJingCheng.EditorTools.Spike
{
public static class Ac29Il2CppSwitch
{
    // 🔁 2026-11-03:`PlayerSettings.Get/SetScriptingBackend(BuildTargetGroup)` 在 Unity 6 已废弃
    //    (CS0618)×4 处换 `NamedBuildTarget` —— 那个 API 把「目标平台」与「平台组」分开,
    //    避免 `BuildTargetGroup.Standalone` 在打包时被当成整组。语义等价:Standalone 组 → Standalone。
    private static NamedBuildTarget StandaloneTarget
        => NamedBuildTarget.FromBuildTargetGroup(BuildTargetGroup.Standalone);

    public static void SetIl2cpp()
    {
        NamedBuildTarget target = StandaloneTarget;
        ScriptingImplementation before = PlayerSettings.GetScriptingBackend(target);
        PlayerSettings.SetScriptingBackend(target, ScriptingImplementation.IL2CPP);
        ScriptingImplementation after = PlayerSettings.GetScriptingBackend(target);
        Debug.Log($"[AC-29] Standalone backend: {before}({(int)before}) -> {after}({(int)after})");
        if (after != ScriptingImplementation.IL2CPP)
        {
            Debug.LogError("[AC-29] 后端切换未生效 —— IL2CPP player 腿不可执行");
            EditorApplication.Exit(1);
        }
    }

    public static void SetMono()
    {
        NamedBuildTarget target = StandaloneTarget;
        PlayerSettings.SetScriptingBackend(target, ScriptingImplementation.Mono2x);
        ScriptingImplementation after = PlayerSettings.GetScriptingBackend(target);
        Debug.Log($"[AC-29] Standalone backend 归位: {after}({(int)after})");
    }
}
}
