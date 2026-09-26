// 权威来源:production/epics/audio-system/story-003-mixer-topology-snapshots.md(AC-44-E3 ③)
// GDD:design/gdd/audio-system.md AC-44-E3(reverb preset 切换所有者 = 44;触发输入 = 玩家
//      所在房间格,表现层派生,不进流)· 素材 reverb_preset_{indoor,outdoor,cave} 归 Story 010

namespace DaYiJingCheng.Gameplay.Presentation.Audio
{
    /// <summary>reverb preset 闭枚举(与 <c>reverb_preset_*</c> 快照名 / 素材族一一对应)。
    /// 扩枚举 = 资产、快照、门常量同批改(闭集定义在代码,数据只承载字面量)。</summary>
    public enum ReverbPreset
    {
        /// <summary>室外(默认,低混响)。</summary>
        Outdoor = 0,

        /// <summary>室内(房间格;常规医馆 / 民居)。</summary>
        Indoor = 1,

        /// <summary>洞穴 / 地窖(长混响)。</summary>
        Cave = 2,
    }
}
