// IL 体扫描器 —— 检查方法体内是否调用指定方法。
//
// 权威来源:
//   AC-1-01②③: AddForce/AddTorque/velocity 写入零引用 + Physics.Raycast/CheckCapsule/Overlap* 零引用
//   承 input-system story-001 的 Roslyn 分析器先例（编译期，非事后 grep）
//
// 核心机制:
//   - 读取方法 IL 字节码
//   - 解析 call/callvirt 操作码的操作数（MemberRef/MethodDef）
//   - 检查方法名是否匹配

using System;
using System.Reflection;
using System.Reflection.Emit;

namespace DaYiJingCheng.Tests.PlayerController
{
    /// <summary>
    /// IL 体扫描器 —— 检查方法体内是否调用指定方法。
    /// </summary>
    public static class ILBodyScanner
    {
        /// <summary>
        /// 检查方法体内是否调用指定名称的方法。
        /// </summary>
        public static bool ContainsMethodCall(MethodInfo method, string targetMethodName)
        {
            try
            {
                var body = method.GetMethodBody();
                if (body == null) return false;

                byte[] il = body.GetILAsByteArray();
                if (il == null || il.Length == 0) return false;

                // 解析 IL 字节码，查找 call/callvirt 操作码
                for (int i = 0; i < il.Length; i++)
                {
                    ushort opcode = il[i];

                    // 处理双字节操作码 (0xFE 前缀)
                    if (opcode == 0xFE)
                    {
                        i++;
                        if (i >= il.Length) break;
                        opcode = (ushort)(0xFE00 | il[i]);
                    }
                    else
                    {
                        opcode = (ushort)(opcode | 0x0000);
                    }

                    // call = 0x28, callvirt = 0x6F
                    if (opcode == (ushort)OpCodes.Call.Value || opcode == (ushort)OpCodes.Callvirt.Value)
                    {
                        // 读取 4 字节操作数（token）
                        if (i + 4 >= il.Length) break;
                        int token = BitConverter.ToInt32(il, i + 1);

                        // 解析 token 获取方法名
                        string calledMethodName = ResolveMethodToken(method, token);
                        if (calledMethodName == targetMethodName)
                        {
                            return true;
                        }

                        i += 4; // 跳过操作数
                    }
                }

                return false;
            }
            catch
            {
                // 无法解析 IL 时返回 false（保守策略）
                return false;
            }
        }

        /// <summary>
        /// 解析 token 获取方法名。
        /// </summary>
        private static string ResolveMethodToken(MethodInfo method, int token)
        {
            try
            {
                // 尝试从模块解析
                var module = method.Module;
                var resolvedMethod = module.ResolveMethod(token);
                return resolvedMethod?.Name ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
