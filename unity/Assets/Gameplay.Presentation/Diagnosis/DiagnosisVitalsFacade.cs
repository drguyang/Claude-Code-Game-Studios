// diagnosis-system Story 001 —— 8 的 VitalsDto 只读门面(唯一取数入口)。
//
// 权威来源:
//   GDD design/gdd/diagnosis-system.md 规则一(8 只读不写不持状态;输入只经 IVitalsQuery
//     门面)· 边界五条铁律②(8 不向 9 写任何东西)· F-8.6(8 在 VitalsDto 之后,
//     是唯一浮点出口的消费侧)
//   ADR-005 抽象点之四 `IVitalsQuery`(`VitalsDto` = 全案唯一浮点出口)· ADR-006(浮点
//     不出边界,8 只消费)· ADR-025 §①(8 落 Gameplay.Presentation,L4 边界层)
//   TR-diag-001(8 位于唯一浮点出口之后)· TR-diag-007(8 不在定点域内)·
//   control-manifest Required「8 的全部输入 = `VitalsDto`(float 只进呈现/求值门面)」
//
// ⚠️ **静态纯函数形状**:依赖(`IVitalsQuery`)显式入参、**零字段** —— 8 不持任何状态
//    (AC-8-3 字段反射遍历对本类型零命中;「当帧」量走方法内局部变量)。
// ⚠️ **取数收口在调用点,不在持有点**:8 的实现类型**持有** `IVitalsQuery`(依赖注入)
//    合法;但 `.GetVitals(...)` **调用**只允许出现在本门面内 —— 由
//    `DiagnosisBoundaryGates` 的 `[D-FACADE]` 谓词按调用点断言(IL 层)。收口判据落在
//    调用点而非持有点:持有限死会逼出「把 query 藏到别处再读」的绕行,调用点收口才是
//    「唯一取数入口」的可证伪形态。
// ⚠️ 新增读路径必须经本门面;绕开直调 `GetVitals` = 违反 Required「只进门面」,
//    构建前门 + EditMode 测试双层红。

using System;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Presentation.Diagnosis
{
    /// <summary>8 诊断的体征只读门面 —— <b>唯一取数入口</b>(规则一 / TR-diag-001 / AC-8-2)。
    /// <para>纯静态、零字段、纯转发:不夹取、不缓存、不推断 —— 读数语义(地板 / 精度档槽 /
    /// 把握度)归 story 003/004 的公式层,本门面只负责「8 从哪读」。</para></summary>
    public static class DiagnosisVitalsFacade
    {
        /// <summary>读取病人体征投影(全案唯一浮点出口的消费侧)。</summary>
        /// <param name="vitalsQuery">9 的体征查询门面(ADR-005 抽象点之四;由调用方注入)。</param>
        /// <param name="patient">病人身份(无病例语境 = <see cref="PatientId.None"/>)。</param>
        /// <returns>体征投影 DTO(字段白名单由 `VitalsDto` 构建期反射断言守,AC-20)。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="vitalsQuery"/> 为 null ——
        /// 显式失败优于空解引用静默(装配门「拒以空集冒充绿」同族纪律)。</exception>
        public static VitalsDto Read(IVitalsQuery vitalsQuery, PatientId patient)
        {
            if (vitalsQuery == null)
                throw new ArgumentNullException(nameof(vitalsQuery));
            return vitalsQuery.GetVitals(patient);
        }
    }
}
