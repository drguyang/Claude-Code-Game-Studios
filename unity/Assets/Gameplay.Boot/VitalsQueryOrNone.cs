// ADR-035 §①B —— 11 的禁忌判据要读 `VitalsDto.SignChannelMask`,而 9 的体征服务对
// **无病程**的病人会抛 `PatientCourseNotFoundException`。
//
// 权威来源:
//   `DiseaseVitalsService.GetVitals` 的契约(无 `_results` 项 ⇒ 抛)
//   ADR-005(`IVitalsQuery` = 唯一浮点出口)
//
// ⚠️ **为什么必须有这层包装**:「在场」(`IPresenceQuery.IsPresent`)与「有病程」是两件事 ——
//   玩家可以给一个只是站在那儿、没有病的人开药。直接传 `DiseaseVitalsService` 会让这条路
//   变成异常;而 11 侧 catch 9 的异常类型 = 让 11 依赖 9 的内部异常面(跨域耦合)。
//   ⇒ 在**装配层**(`Gameplay.Boot`,本来就同时看得见两侧)做边界归一。
//
// ⚠️ **只吞 `PatientCourseNotFoundException`**:其余异常(注册表缺行 / SCALE ≤ 0 等内部不一致)
//   一律上抛 —— 那些是真故障,吞掉会把「数据坏了」伪装成「病人没病」。
//
// ⚠️ 无病程 ⇒ 返回 `default(VitalsDto)`(各字段 0)⇒ `SignChannelMask = 0` ⇒ 判据不命中。
//   语义 = 「没有可判的体征」,与「判了没命中」在流上同形(都写哨兵)。

using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.DiseaseSimulation;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>把「无病程 ⇒ 抛」的体征查询归一成「无病程 ⇒ 零体征」。</summary>
    /// <remarks>只用于**装配层**边界;11 / 9 两侧均不感知本类型。</remarks>
    public sealed class VitalsQueryOrNone : IVitalsQuery
    {
        private readonly IVitalsQuery _inner;

        /// <summary>包装一个体征查询。</summary>
        /// <param name="inner">真实体征源(为 <c>null</c> ⇒ 本包装恒返回零体征)。</param>
        public VitalsQueryOrNone(IVitalsQuery inner)
        {
            _inner = inner;
        }

        /// <inheritdoc />
        public VitalsDto GetVitals(PatientId p)
        {
            if (_inner == null) return default;
            try
            {
                return _inner.GetVitals(p);
            }
            catch (PatientCourseNotFoundException)
            {
                return default;   // 无病程 ⇒ 没有可判的体征(不是错误)
            }
        }
    }
}
