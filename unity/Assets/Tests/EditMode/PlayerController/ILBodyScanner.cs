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

                    // ⚠️ **2026-10-03 扩展(评审 B1)**:
                    //   原版只认 `call`(0x28)/`callvirt`(0x6F) —— 而 **`new X(...)` 走 `newobj`(0x73)**,
                    //   其操作数是 **ctor**,方法名恒为 `.ctor`,**永不等目标类型名**。
                    //   ⇒ `new SimEvent(...)` 此前**结构性漏检**(实测:相机内真构造 SimEvent,判据 0 反应)。
                    //   现三路覆盖:
                    //     ① `call`/`callvirt` ⇒ 方法名匹配(原有)
                    //     ② `newobj`          ⇒ 方法名匹配(`.ctor` 亦可比,兼容旧用法)
                    //     ③ `newobj`/`ldfld`/`stfld`/`castclass`/`isinst`/`box`/`unbox`
                    //        ⇒ **类型名**匹配(抓 `new SimEvent(...)` 与字段/装箱引用)
                    bool isCallLike = opcode == (ushort)OpCodes.Call.Value
                                   || opcode == (ushort)OpCodes.Callvirt.Value
                                   || opcode == (ushort)OpCodes.Newobj.Value;

                    bool isTypeRef = opcode == (ushort)OpCodes.Newobj.Value
                                  || opcode == (ushort)OpCodes.Ldfld.Value
                                  || opcode == (ushort)OpCodes.Stfld.Value
                                  || opcode == (ushort)OpCodes.Castclass.Value
                                  || opcode == (ushort)OpCodes.Isinst.Value
                                  || opcode == (ushort)OpCodes.Box.Value
                                  || opcode == (ushort)OpCodes.Unbox.Value
                                  || opcode == (ushort)OpCodes.Unbox_Any.Value;

                    if (isCallLike || isTypeRef)
                    {
                        if (i + 4 >= il.Length) break;
                        int token = BitConverter.ToInt32(il, i + 1);

                        if (isCallLike)
                        {
                            string calledMethodName = ResolveMethodToken(method, token);
                            if (calledMethodName == targetMethodName) return true;
                        }

                        if (isTypeRef)
                        {
                            // 类型名匹配 —— 抓 `new SimEvent(...)`(ctor 名是 .ctor,须看声明类型)
                            string typeName = ResolveTypeToken(method, token);
                            if (typeName == targetMethodName) return true;
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
        /// 解析 token 获取**类型名**(含声明类型)。
        /// ⚠️ 2026-10-03(评审 B1):`newobj` 的操作数解析为 ctor,其 `Name` 恒为 `.ctor`
        /// ⇒ 判据须看**声明类型名**,否则 `new SimEvent(...)` 永不被捕获。
        /// </summary>
        private static string ResolveTypeToken(MethodInfo method, int token)
        {
            try
            {
                var module = method.Module;

                // 先试方法(ctor)⇒ 取其声明类型名
                try
                {
                    var m = module.ResolveMethod(token);
                    if (m?.DeclaringType != null) return m.DeclaringType.Name;
                    if (m != null) return m.Name;
                }
                catch { /* 非方法 token ⇒ 落下一步 */ }

                // 再试类型(字段 / 类型 token)
                try
                {
                    var t = module.ResolveType(token);
                    return t?.Name ?? string.Empty;
                }
                catch { return string.Empty; }
            }
            catch
            {
                return string.Empty;
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
