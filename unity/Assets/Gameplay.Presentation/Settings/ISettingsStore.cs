// 权威来源:production/epics/audio-system/story-011-settings-exposure-reset.md(AC-44-19)
// GDD:design/gdd/audio-system.md AC-44-19(:1153-1159)· §Tuning Knobs 落点裁决(:707-716)
// ADR-018 §一 · ADR-013(控件呈现归 42)· OQ-SS-5=甲 · OQ-SS-7=甲
//
// ⚠️ **归属诚实声明**:本接口名(`ISettingsStore`)在 S13-9 评审时**全库无权威定义** ——
//    它首现于 story-011 的测试注释(评审原件 §2.5 逐字点名「接口名本身是测试作者的发明,
//    story 事后追认」)。**S14-5 用户裁定「全落生产」后**,本接口自此成为**生产契约**,
//    不再是测试自造物 —— 但它的**来源是测试,不是设计文档**,此事实如实登记不掩盖。
// ⚠️ 接口**只暴露 42 壳 + 归零钮所需的面**(读 / 写 / 归零 / 载入),不含任何 sim 面:
//    设置是端本地设备偏好(GDD :1204),与三流 / 存档头零关系(OQ-SS-5=甲)。
// ⚠️ 未来 49(textScale)/ 3(键位,P1a)进架时**扩展本接口或另立同族接口** ——
//    本接口当前只承载 44 的 8 行(注册表闭集)。

using System.Collections.Generic;

namespace DaYiJingCheng.Gameplay.Presentation.Settings
{
    /// <summary>设置 store 契约(壳只读它、归零钮只调它;**持有者 = 边界层**)。
    /// <para>⚠️ 壳**禁缓存**本接口返回的任何值(AC-42-G2 零缓存宪法):每次呈现都现读。</para></summary>
    public interface ISettingsStore
    {
        /// <summary>读一个设置值;未登记参数 ⇒ false(调用方据此判「越集」)。</summary>
        /// <param name="param">exposed 参数名(注册表闭集成员)。</param>
        /// <param name="value">出口:当前值。</param>
        bool TryGet(string param, out SettingsValue value);

        /// <summary>写一个设置值(仅限注册表闭集;越集 ⇒ 抛)。</summary>
        /// <exception cref="System.ArgumentOutOfRangeException">参数 ∉ 注册表。</exception>
        void Set(string param, SettingsValue value);

        /// <summary>当前值集只读快照(按参数序数排序 ⇒ 确定性)。</summary>
        IReadOnlyDictionary<string, SettingsValue> Snapshot();

        /// <summary>当前值集是否逐键等于出厂默认集(AC-44-19 ① 的判据本体)。</summary>
        bool EqualsFactoryDefaults();

        /// <summary>**归零三步**:写默认 → 推 mixer → 落 sidecar(AC-44-19 / OQ-SS-7 ②)。
        /// <para>⚠️ 缺一步 = 归源无效 ⇒ 结果须可诊断,不得静默成功。</para></summary>
        ResetOutcome ResetToFactory();

        /// <summary>从端本地 sidecar 载入(启动路径;失配 ⇒ 维持默认 + 陈旧件备份)。</summary>
        SettingsLoadResult LoadFromSidecar();
    }
}
