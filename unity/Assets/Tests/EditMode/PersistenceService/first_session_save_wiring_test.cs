// 「第一份存档」的**接线**断言(2026-11-04)。
//
// ─────────────────────────────────────────────────────────────────────────────
// 它补的是哪一半
// ─────────────────────────────────────────────────────────────────────────────
// `first_session_save_test` 证的是**决策体**(`FirstSessionSave.TryEstablish`)的语义;
// 本件证的是**它真的被启动序调用**。两半都要有 —— 本仓反复抓到的形态正是
// 「服务层全绿而生产零调用点」(`TD-skeuo-06` 的 `IProcessingCommandSink` 生产实现为零,
// 调用点 `_sink?.Ignite(...)` 静默跳过)。
//
// ─────────────────────────────────────────────────────────────────────────────
// ⚠️ 这是**源码级**断言,不是行为观测 —— 如实登记,不借绿
// ─────────────────────────────────────────────────────────────────────────────
// 它只能证明「非注释代码里存在这个调用,且排在启动序完成之后」。
// 它**不能**证明:该分支真的会在某次启动里被执行(需要 `BootRoot` 整段跑起来 — 今日无
// PlayMode 判据,与 `BootRoot` 其余 `OnTickEdge` 同状态)。
// 因此本件与 `first_session_save_test` 合起来 = 「语义 + 接线」,**不含**「端到端跑过一次」。
//
// ─────────────────────────────────────────────────────────────────────────────
// 读哪些仓库文件
// ─────────────────────────────────────────────────────────────────────────────
// 读:`unity/Assets/Gameplay.Boot/BootRoot.cs`(只读,不写)。
// 必须读**生产源文件本身**而不是把期望写在夹具里:本件判的就是「那段代码长什么样」。
// ⚠️ **必须剥注释** —— 生产件的解释性注释里逐字出现了 `FirstSessionSave.TryEstablish`
//    (本类自己写的),不剥就会让「注释里写着」冒充「代码里调着」。这正是 F6 形态。

using System;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace DaYiJingCheng.Tests.PersistenceService
{
    [TestFixture]
    public sealed class FirstSessionSaveWiringTest
    {
        private const string BootRootRelativePath = "Gameplay.Boot/BootRoot.cs";

        [Test]
        public void test_bootSequence_callsFirstSessionSave_afterStartupCompletes()
        {
            string code = StripComments(ReadProductionSource(BootRootRelativePath));

            int booted = code.IndexOf("_booted = true;", StringComparison.Ordinal);
            int continueCall = code.IndexOf("TryContinueFromSave(_continueSlot", StringComparison.Ordinal);
            int firstSave = code.IndexOf("FirstSessionSave.TryEstablish(", StringComparison.Ordinal);

            Assert.That(booted, Is.GreaterThanOrEqualTo(0),
                "[前置] 启动序的完成标记 `_booted = true;` 找不到 —— 本件须随启动序改动同步");
            Assert.That(continueCall, Is.GreaterThanOrEqualTo(0),
                "[前置] 「继续最近档」调用点找不到 —— 启动序结构已变,本件须重写而不是放宽");
            Assert.That(firstSave, Is.GreaterThanOrEqualTo(0),
                "启动序里找不到首份存档的调用点 —— 玩家开局前 5 分钟内崩溃仍然零存档");

            Assert.That(firstSave, Is.GreaterThan(booted),
                "首份存档排在启动序**完成之前** —— 此时世界/派生态尚未就绪," +
                "存下去的是一份半装配的世界(与「继续诊治」读档必须排在 8 的原因同源)");
            Assert.That(firstSave, Is.GreaterThan(continueCall),
                "首份存档排在「继续最近档」**之前** —— 那会在读档之前先写一次;" +
                "两条路径必须互斥(读档分支写档 = 覆盖玩家刚选的档)");
        }

        // ── 工具 ──────────────────────────────────────────────────────────

        private static string ReadProductionSource(string relativePath)
        {
            string path = Application.dataPath.Replace('\\', '/') + "/" + relativePath;
            Assert.That(File.Exists(path), Is.True,
                $"找不到生产源文件 {relativePath} —— 判据的对象被移动/改名,须同改本测");
            return File.ReadAllText(path);
        }

        /// <summary>剥掉行注释与块注释(见文件头的 F6 说明)。</summary>
        private static string StripComments(string text)
        {
            var sb = new StringBuilder(text.Length);
            bool inBlock = false;
            int i = 0;
            while (i < text.Length)
            {
                if (inBlock)
                {
                    int close = text.IndexOf("*/", i, StringComparison.Ordinal);
                    if (close < 0) return sb.ToString();
                    inBlock = false;
                    i = close + 2;
                    continue;
                }

                int lineComment = text.IndexOf("//", i, StringComparison.Ordinal);
                int blockOpen = text.IndexOf("/*", i, StringComparison.Ordinal);

                if (blockOpen < 0 && lineComment < 0) { sb.Append(text, i, text.Length - i); break; }
                if (blockOpen < 0 || (lineComment >= 0 && lineComment < blockOpen))
                {
                    sb.Append(text, i, lineComment - i);
                    int newline = text.IndexOf('\n', lineComment);
                    if (newline < 0) break;
                    i = newline;                       // 保留换行,继续下一行
                    continue;
                }

                sb.Append(text, i, blockOpen - i);
                inBlock = true;
                i = blockOpen + 2;
            }
            return sb.ToString();
        }
    }
}
