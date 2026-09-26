// Story 008 · D1 绑定查询契约(AC-3-D1 · TR-input-013)
//
// 权威来源:
//   GDD input-system.md §Visual/Audio 三:QueryBinding → { device, bindingPath, iconKey };
//   device ∈ {Kbd, Pad, Xr};bindingPath = Input System 绑重路径(例 "<Keyboard>/e");
//   iconKey = **稳定键名字符串非素材**(例 "Kbd.E" / "Pad.South" —— 素材图集归 42/美术)。
//   AC-3-D1(BLOCKING):**未绑定返回明确枚举**(非空指针、非异常)—— 消费方(42/20)按枚举分支。
//   Story 008 QA:Edge = device 未知枚举值 / bindingPath 空串 / 复合绑重的 part 路径。
//   口径注记:registry(TR-input-013)签名写 QueryBinding(actionId),story AC 与 GDD §States
//   一「QueryBinding 的 device 返回 Pad」按 (device, bindingPath) 操作 ⇒ **story 文本为准**,
//   本实现取 (device, bindingPath) 形(两口径的差待收口轮登记进 Completion Notes)。
//
// 形状纪律:返回类型是 **struct(值类型)** —— 「null 表未绑定」在这一形状下物理不可能,
//   未绑定态由 BindingQueryStatus 枚举承载(AC-3-D1 的两条负例形态由此结构性封死)。
//   字段集恰 {Status, Device, BindingPath, IconKey} —— D2 类型断言(AC-3-D2)按本名单执法,
//   加一支 `float FadeDuration` 即构建红(负例夹具住测试装配)。
//   住根命名空间(非 Intents)—— 交出物闭集 AC-3-A6 只管 Intents 子命名空间,本件是
//   查询基础设施(同 InputService / BindingsStore 先例),不入五意图闭集面。

using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace DaYiJingCheng.Gameplay.Input
{
    /// <summary>绑重所属设备类(GDD §Visual/Audio 三 device 列的闭集三值)。</summary>
    public enum BindingDeviceClass
    {
        /// <summary>未知 / 不属于三类(未绑定与非法入参共用本值,配合 Status 分支)。</summary>
        Unknown = 0,

        /// <summary>键盘。</summary>
        Kbd = 1,

        /// <summary>手柄(Gamepad 布局族)。</summary>
        Pad = 2,

        /// <summary>XR 控制器(XRController 布局)。</summary>
        Xr = 3,
    }

    /// <summary>查询结论闭集(AC-3-D1「明确枚举」的载体;消费方分支唯一入口)。
    /// ⚠️ 零值 = NotBound(fail-safe):default(BindingQueryResult) 落在「未绑定」而非
    /// 「已绑定」—— 忘赋 Status 的代码路径不可能伪装成 Bound(结构负例「null 表未绑定」
    /// 的等价面在值类型形状下的强化形)。</summary>
    public enum BindingQueryStatus
    {
        /// <summary>该设备类下无此绑重路径(含空串路径)—— 非 null、非异常(零值)。</summary>
        NotBound = 0,

        /// <summary>查得绑重:三字段俱有效。</summary>
        Bound = 1,

        /// <summary>入参 device ∉ {Kbd, Pad, Xr}(未知枚举值;QA Edge)。</summary>
        UnknownDevice = 2,
    }

    /// <summary>QueryBinding 交付物(GDD §Visual/Audio 三 三字段 + 状态枚举)。
    /// ⚠️ D2 类型断言按 <c>InputBoundaryGates.BindingResultAllowedFieldNames</c> 对本结构的
    /// **实例字段集**执法:恰 {Device, BindingPath, IconKey, Status};出现时机 / 可见性 /
    /// 素材类型 ⇒ 构建红(AC-3-D2)。</summary>
    public readonly struct BindingQueryResult
    {
        /// <summary>查询结论(Bound 之外三字段退化为空值/Unknown)。</summary>
        public readonly BindingQueryStatus Status;

        /// <summary>绑重所属设备类(Kbd / Pad / Xr;非 Bound = Unknown)。</summary>
        public readonly BindingDeviceClass Device;

        /// <summary>Input System 绑重路径原文(例 "<Keyboard>/e";非 Bound = 空串)。
        /// 键名不是素材 —— 本串不含也不得被消费方当作资产路径(D2 构建报告面扫 TypeRef,
        /// 不扫字符串值;GDD:iconKey 值本身含素材扩展名亦不约束 —— 键名不约束)。</summary>
        public readonly string BindingPath;

        /// <summary>稳定键名 iconKey(例 "Kbd.E" / "Pad.South" / 复合 part "Pad.LeftStick_Up";
        /// 非 Bound = 空串)。素材归 42;3 只给键名(GDD §Visual/Audio 三 裁定②)。</summary>
        public readonly string IconKey;

        /// <summary>构造查询结果(由 <see cref="BindingQuery.QueryBinding"/> 调用;
        /// 公开以支持装配层/测试合成)。</summary>
        public BindingQueryResult(BindingQueryStatus status, BindingDeviceClass device,
                                  string bindingPath, string iconKey)
        {
            Status = status;
            Device = device;
            BindingPath = bindingPath ?? string.Empty;
            IconKey = iconKey ?? string.Empty;
        }

        /// <summary>未绑定 / 非法入参的规范退化结果(全类共用,防各处手搓不一致)。</summary>
        public static BindingQueryResult NotBound(BindingQueryStatus status = BindingQueryStatus.NotBound)
            => new BindingQueryResult(status, BindingDeviceClass.Unknown, string.Empty, string.Empty);
    }

    /// <summary>绑定查询器(AC-3-D1 实现载体):在全案唯一动作资产(Story 001 注入)上,
    /// 按 (device, bindingPath) 查绑重并派生 iconKey。**纯查询,零状态、零副作用、不抛业务异常**。</summary>
    public static class BindingQuery
    {
        /// <summary>查一对 (device, bindingPath):
        /// ① device ∉ {Kbd,Pad,Xr} ⇒ <see cref="BindingQueryStatus.UnknownDevice"/>;
        /// ② bindingPath 为 null / 空串 ⇒ <see cref="BindingQueryStatus.NotBound"/>;
        /// ③ 资产中该设备类下无路径全等绑重 ⇒ NotBound;
        /// ④ 命中 ⇒ Bound + iconKey。<b>任何分支都返回值类型结果,不返 null、不抛</b>
        /// (仅 actions==null 属装配层编程错误 ⇒ <see cref="ArgumentNullException"/>)。
        /// 复合绑重的 part 路径(如 "<Gamepad>/leftStick/up")按 part 绑重自身命中,
        /// iconKey 以 "_" 连接设备内段("Pad.LeftStick_Up")。</summary>
        /// <param name="device">设备类闭集入参。</param>
        /// <param name="bindingPath">Input System 绑重路径(Ordinal 全等比较)。</param>
        /// <param name="actions">全案唯一动作资产(规则一;null = 装配错误,抛)。</param>
        public static BindingQueryResult QueryBinding(BindingDeviceClass device, string bindingPath,
                                                      InputActionAsset actions)
        {
            if (actions == null)
                throw new ArgumentNullException(nameof(actions),
                    "QueryBinding 要求注入动作资产(全案唯一 .inputactions,规则一)—— null = 装配错误。");
            if (device != BindingDeviceClass.Kbd && device != BindingDeviceClass.Pad &&
                device != BindingDeviceClass.Xr)
                return BindingQueryResult.NotBound(BindingQueryStatus.UnknownDevice);
            if (string.IsNullOrEmpty(bindingPath))
                return BindingQueryResult.NotBound();

            foreach (var binding in actions.bindings)
            {
                // Ordinal 全等(绑重路径大小写敏感);复合本体路径与 part 路径同面可命中。
                if (!string.Equals(binding.path, bindingPath, StringComparison.Ordinal)) continue;
                if (ClassOfPath(binding.path) != device) continue;
                return new BindingQueryResult(BindingQueryStatus.Bound, device,
                                              binding.path, IconKeyOf(device, binding.path));
            }
            return BindingQueryResult.NotBound();
        }

        /// <summary>绑重路径 → 设备类("&lt;Keyboard&gt;/…" → Kbd;"&lt;Gamepad&gt;/…" → Pad;
        /// 布局名含 XRController → Xr;无设备前缀(如 "*" 掩码或 tag 路径)→ Unknown)。</summary>
        public static BindingDeviceClass ClassOfPath(string path)
        {
            if (string.IsNullOrEmpty(path) || path[0] != '<') return BindingDeviceClass.Unknown;
            int close = path.IndexOf('>', 1);
            if (close < 0) return BindingDeviceClass.Unknown;
            string layout = path.Substring(1, close - 1);
            if (layout == "Keyboard") return BindingDeviceClass.Kbd;
            if (layout == "Gamepad") return BindingDeviceClass.Pad;
            if (layout.IndexOf("XRController", StringComparison.Ordinal) >= 0) return BindingDeviceClass.Xr;
            return BindingDeviceClass.Unknown;
        }

        /// <summary>iconKey 派生(键名非素材):设备类前缀 + 设备内路径段逐段首字母大写,
        /// 多段以 "_" 连接;"button" 前缀剥除(buttonSouth → South,承 GDD 例 "Pad.South")。
        /// 本函数是**稳定纯函数** —— 同输入恒同键,素材映射(键 → 图集)归 42。</summary>
        public static string IconKeyOf(BindingDeviceClass device, string bindingPath)
        {
            string prefix;
            switch (device)
            {
                case BindingDeviceClass.Kbd: prefix = "Kbd"; break;
                case BindingDeviceClass.Pad: prefix = "Pad"; break;
                case BindingDeviceClass.Xr: prefix = "Xr"; break;
                default: return string.Empty;
            }
            if (string.IsNullOrEmpty(bindingPath)) return string.Empty;

            // 取设备前缀之后的控制路径:"<Keyboard>/e" → "e";"<Gamepad>/leftStick/up" → "leftStick/up"
            string tail = bindingPath;
            int slash = tail.IndexOf('/');
            if (tail.Length > 0 && tail[0] == '<' && slash > 0) tail = tail.Substring(slash + 1);
            if (tail.Length == 0) return string.Empty;

            var parts = tail.Split('/');
            var keys = new List<string>(parts.Length);
            foreach (var raw in parts)
            {
                string p = raw;
                // 去掉 <...> 与 {..} 形态(掩码 / tag 段无可读键名,原样剥角括号)
                if (p.StartsWith("<", StringComparison.Ordinal))
                {
                    int c = p.IndexOf('>');
                    if (c > 0) p = p.Substring(c + 1);
                }
                p = p.TrimStart('{').TrimEnd('}');
                if (p.StartsWith("button", StringComparison.Ordinal) && p.Length > 6)
                    p = p.Substring(6);
                if (p.Length == 0) continue;
                keys.Add(char.ToUpperInvariant(p[0]) + p.Substring(1));
            }
            return keys.Count == 0 ? string.Empty : prefix + "." + string.Join("_", keys);
        }
    }
}
