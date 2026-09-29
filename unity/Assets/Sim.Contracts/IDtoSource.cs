namespace DaYiJingCheng.Sim.Contracts
{
    /// <summary>
    /// 只读 DTO 源接口 — 呈现层绑定的数据源契约。
    /// <para>铁律:呈现层只读取,永不缓存副本(caching = 第二份真相,破规则六)。</para>
    /// <para>实现方提供具体 DTO 访问方法;本接口为标记契约,确保绑定关系显式化。</para>
    /// </summary>
    public interface IDtoSource
    {
        // 标记接口 — 具体 DTO 访问方法由实现方定义
        // 呈现层通过此接口引用数据源,不直接耦合具体实现
    }
}
