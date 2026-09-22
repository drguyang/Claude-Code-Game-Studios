// 权威来源:ADR-014 §五(:222)· ADR-025 §①(住 Sim.Contracts)
//
// sim 零 UnityEngine(门 A)⇒ 装载(含 Addressables 调用)发生在**边界程序集**;
// 本接口只是 sim 侧可见的取数形状:边界程序集把**已解析的领域 struct**交给 sim。
//
// ⚠️ 「常驻」口径(ADR-014 §五 2026-09-16 补注):仅约束小体量启动前置数据;
//    逻辑导航格按 chunk 切片按需加载,未驻留 chunk 视为全 block —— 对本接口透明。
// ⚠️ 失败语义:装载失败 = 启动期硬失败(E-13 兜底),故本接口**无错误返回通道**;
//    实现方内部 try/catch 后直接 throw,不得返回默认值静默继续(Step 无错误通道,ADR-006 §一)。

namespace DaYiJingCheng.Sim.Contracts
{
    /// <summary>烘焙数据(*.cooked)→ 领域 struct 数据集的唯一取数接口。</summary>
    public interface IDataProvider
    {
        TDataSet Load<TDataSet>() where TDataSet : struct;
    }
}
