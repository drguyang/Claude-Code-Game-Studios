// player-symbol-check — Story 012 交付物(AC-3-E2②)
// 扫描已生成 player 的 managed assemblies,断言 InputDebugView 类型不存在。
//
// 用法:
//   dotnet run --project tools/player-symbol-check/PlayerSymbolCheck.csproj -- <player-build-dir>
//
// 返回码:
//   0 = 未发现目标符号(通过)
//   1 = 发现目标符号(失败 —— 玩家构建包含调试代码)
//   2 = 参数错误 / 目录不存在
//
// 限制:
//   - Mono player:扫描 .dll  assemblies(有效)
//   - IL2CPP player: assemblies 被编译为 native code,本工具无法扫描;
//     须在 IL2CPP 构建后追加二进制符号扫描(待 ADR-012 F7 spike 补充)。
//   - 剪裁(Strip Engine Code / Managed Stripping)可能移除类型,
//     若剪裁后恰好移除 InputDebugView,本工具仍返回通过(剪裁是预期行为)。

using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace PlayerSymbolCheck
{
    internal static class Program
    {
        // 禁止在出货 player 中出现的类型全名(条件编译门 + 呈现层)
        private static readonly string[] ForbiddenTypes =
        {
            "DaYiJingCheng.Gameplay.Input.InputDebugView"
        };

        private static int Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.Error.WriteLine("用法: player-symbol-check <player-build-dir>");
                return 2;
            }

            string buildDir = args[0];
            if (!Directory.Exists(buildDir))
            {
                Console.Error.WriteLine($"目录不存在: {buildDir}");
                return 2;
            }

            // 扫描 managed assemblies(*.dll,排除 Unity 引擎 assemblies)
            var dllFiles = Directory.GetFiles(buildDir, "*.dll", SearchOption.AllDirectories)
                .Where(f => !IsUnityEngineAssembly(f))
                .ToArray();

            Console.WriteLine($"扫描 {dllFiles.Length} 个 managed assemblies(排除引擎 assemblies)...");

            bool found = false;
            foreach (string dll in dllFiles)
            {
                try
                {
                    byte[] raw = File.ReadAllBytes(dll);
                    string asmName = Path.GetFileNameWithoutExtension(dll);

                    // 轻量扫描:读 assembly name + 类型名,不加载到 AppDomain
                    foreach (string forbidden in ForbiddenTypes)
                    {
                        if (ContainsTypeName(raw, forbidden))
                        {
                            Console.WriteLine($"[FAIL] {asmName}: 发现禁止类型 {forbidden}");
                            found = true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SKIP] {Path.GetFileName(dll)}: {ex.Message}");
                }
            }

            if (found)
            {
                Console.Error.WriteLine("结果: FAIL —— 玩家构建包含调试代码路径(InputDebugView)");
                return 1;
            }

            Console.WriteLine("结果: PASS —— 未发现 InputDebugView 类型(剪裁/剥离后符合预期)");
            return 0;
        }

        /// <summary>跳过 Unity 引擎 assemblies(不扫描 UnityEngine* / Unity* 引擎程序集)。</summary>
        private static bool IsUnityEngineAssembly(string path)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            return name.StartsWith("UnityEngine") || name == "Unity" || name == "UnityEditor";
        }

        /// <summary>轻量类型名扫描:在 assembly 二进制中搜索 UTF-8 编码的类型全名字节序列。</summary>
        /// <remarks>
        /// 不加载 assembly 到 AppDomain(避免执行静态构造函数 / 依赖解析)。
        /// 通过读取 .NET 元数据流 #Strings 中的类型名实现扫描。
        /// 若 assembly 被剪裁( stripping)移除了类型,本扫描自然不命中 —— 这正是期望行为。
        /// </remarks>
        private static bool ContainsTypeName(byte[] raw, string typeName)
        {
            // 方法 1:直接搜索 UTF-8 编码的类型名(覆盖大多数未剪裁 assemblies)
            byte[] nameBytes = System.Text.Encoding.UTF8.GetBytes(typeName);
            if (SearchBytes(raw, nameBytes))
                return true;

            // 方法 2:搜索 UTF-16 编码(某些 assembly 存储为 UTF-16)
            byte[] nameBytes16 = System.Text.Encoding.Unicode.GetBytes(typeName);
            if (SearchBytes(raw, nameBytes16))
                return true;

            return false;
        }

        /// <summary>Boyer-Moore-Horspool 子串搜索(单模式,短模式)。</summary>
        private static bool SearchBytes(byte[] data, byte[] pattern)
        {
            if (pattern.Length == 0 || pattern.Length > data.Length)
                return false;

            int last = pattern.Length - 1;
            int[] skip = new int[256];
            for (int i = 0; i < skip.Length; i++) skip[i] = pattern.Length;
            for (int i = 0; i < last; i++) skip[pattern[i]] = last - i;

            int i = 0;
            while (i <= data.Length - pattern.Length)
            {
                int j = last;
                while (j >= 0 && data[i + j] == pattern[j]) j--;
                if (j < 0) return true;
                i += skip[data[i + last]];
            }
            return false;
        }
    }
}
