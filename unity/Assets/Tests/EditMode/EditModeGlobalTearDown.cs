// EditMode 全局兜底清理(清债轮 · TD-audio-026 · 2026-10-25)
//
// 背景:CompositionRoot.Assemble() 无条件建 AudioSourcePool(TD-audio-026 丙案
// 「完全构造」,AC-018-4 单池不变量),池根 = DontSave GameObject —— 不清理则跨夹具残留。
// 释放主路径 = 服务袋 `Dispose()`(测试持有袋引用时逐件调);本件是**运行末兜底**:
// 整个 EditMode 跑完后把残留池根扫掉,不让泄漏漂进编辑器的下一次操作。
// ⚠️ 运行中途的夹具间清理不归本件(NUnit 无夹具间钩子);ac018_4 的自隔离前置
//    (runtime_assembly_test「夹具起点零池根」)按夹具侧判据维持。
// ⚠️ 只清 EditMode 程序集(本文件所在树);PlayMode 退出播放态时 Unity 自行回收。

using UnityEngine;

namespace DaYiJingCheng.Tests
{
    /// <summary>EditMode 全局一次性清理(运行末兜底,非夹具间钩子)。</summary>
    [NUnit.Framework.SetUpFixture]
    public sealed class EditModeGlobalTearDown
    {
        [NUnit.Framework.OneTimeTearDown]
        public void DestroyResidualPoolRoots()
        {
            foreach (GameObject root in Resources.FindObjectsOfTypeAll<GameObject>())
                if (root != null && root.name == DaYiJingCheng.Gameplay.Presentation.Audio.AudioSourcePool.RootName
                    && root.scene.IsValid())
                    Object.DestroyImmediate(root);
        }
    }
}
