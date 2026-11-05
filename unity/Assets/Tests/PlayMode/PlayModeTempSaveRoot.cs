// `TD-persist-23` 解除动作 ① 的载体:PlayMode「真启动序」夹具的**临时存档根**工厂。
//
// 病灶(2026-11-05 坐实):凡 `AddComponent<BootRoot>()` 而不设 `SaveRootOverride` /
//   `SaveBasePathOverride` 的夹具,`BootRoot` 会落到**默认存档根** = `persistentDataPath/saves`
//   ⇒ 启动序里的 `FirstSessionSave` 往**玩家真实存档目录**写一份 `save_auto.bin`。
//   实测判据(可复跑):跑全量 PlayMode 前后各 `diff -rq <saves>`,恰多出 `save_auto.bin`
//   (1622 B,魔数 `DYJQ`,`SaveVersion = 5`)。
//
// ⇒ 本类只做一件事:给每个夹具发一个**互不相同**的临时根目录。
//   ⚠️ 为什么不用固定名字:同一轮 PlayMode 里多个夹具并发/先后跑,固定目录会让
//   「A 写了档、B 读到」成为隐式依赖(测试隔离规则:每条测试自备状态)。
//
// ⚠️ 调用点**必须**把返回值赋给 `BootRoot.SaveRootOverride`(或 `SaveBasePathOverride`),
//   机械门 `playmode_save_root_gate_test` 按**同文件内出现属性名**判定 —— 只调本类不赋属性 = 仍红。

using System;
using System.IO;

namespace DaYiJingCheng.Tests.PlayMode
{
    /// <summary>为 PlayMode 夹具生成互不相同的临时存档根(根下照常是 `w1/`、`worlds.index`)。</summary>
    internal static class PlayModeTempSaveRoot
    {
        /// <summary>建一个临时根目录并返回其路径。
        /// <para>⚠️ 不清理:`Path.GetTempPath()` 是系统临时区,项目既有 fixture
        /// (`save_slot_save_wiring_test` 等)同样只建不删 —— 删除若在 `TearDown` 抛异常,
        /// 会把「清理」变成二次故障(同 `CapsulePatientVisualFactory.Destroy` 的纪律)。</para>
        /// </summary>
        /// <param name="tag">夹具短名(用于肉眼辨认是哪条测试留下的)。</param>
        public static string New(string tag)
        {
            string safe = string.IsNullOrWhiteSpace(tag) ? "boot" : tag;
            string dir = Path.Combine(
                Path.GetTempPath(),
                "dyjc-pm-" + safe + "-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(dir);
            return dir;
        }
    }
}
