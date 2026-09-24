// 权威来源:Story 010 AC-21a-58(容器实例闭包四形态硬失败)· GDD item-database §Schema E
//          (容器内部 = 子实例 id 列表;children 只能是叶子 id,禁嵌套容器)
//          · AC-21a-34(容器结构守恒的结构前提)· ADR-010(存档读取侧的校验落点)
//
// 四形态(AC-58 Given,任一 ⇒ 显式 throw,禁 Debug.Assert —— 构建期/装配期执法体纪律):
//   ① 同一 instance_id 同属两容器 children;② 容器 children 含自身 id;
//   ③ 容器图有环(A→B→A);④ children 指向未登记实例。
// 附加两条(AC-58 Edge / Schema E):⑤ 登记表内 id 重复;⑥ 嵌套容器(child 自身有 children)。
//
// ⚠️ 判序 = ①登记 → ②未登记/自引用 → ④共享 → ③环 → ⑥嵌套:
//    环检测先于嵌套,使 A→B→A 报「环」而非「嵌套」(QA 四形态各报各的因);
//    合法单层容器(容器 → 全部叶子)与空容器全过。
//
// ⚠️ QA 边例「深链无环(过)」与「嵌套容器即拒」的文本张力(Story 010 测试内已注记):
//    Schema E 明文 children 只能是叶子 ⇒ 含中间容器的深链被 ⑥ 拒;
//    「深链无环(过)」按「环检测不误伤无环图」读 —— 深链的拒绝因是嵌套,**不是环**。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim
{
    /// <summary>容器实例闭包校验(装配期 / 读档期硬失败执法体,AC-21a-58)。</summary>
    public static class ContainerClosure
    {
        /// <summary>校验一批实例构成的容器闭包。全部合法时静默返回;任一形态违规显式抛。</summary>
        /// <param name="instances">全实例登记表(容器与叶子同列;id 必须互异)。</param>
        /// <exception cref="InvalidOperationException">六形态之一:重复登记 / 未登记子件 / 自引用 /
        /// 子件同属两容器 / 有环 / 嵌套容器。</exception>
        /// <exception cref="ArgumentNullException">instances 为 null。</exception>
        public static void Validate(IReadOnlyList<ItemInstance> instances)
        {
            if (instances == null) throw new ArgumentNullException(nameof(instances));

            // ① 登记表 + 重复 id 拒收
            var registry = new Dictionary<long, ItemInstance>(instances.Count);
            for (int i = 0; i < instances.Count; i++)
            {
                ItemInstance inst = instances[i];
                if (registry.ContainsKey(inst.InstanceId))
                    throw new InvalidOperationException(
                        $"容器闭包违规:实例 id {inst.InstanceId} 在登记表中重复 —— id 唯一是守恒的定义前提(AC-21a-58)");
                registry[inst.InstanceId] = inst;
            }

            // child → 父容器计数(④ 共享检测)+ 逐 child ② 未登记 / 自引用
            var parentCount = new Dictionary<long, int>();
            foreach (ItemInstance container in instances)
            {
                if (container.Children == null) continue;   // 叶子;解码侧保证 [],此处容 null

                foreach (long childId in container.Children)
                {
                    if (childId == container.InstanceId)
                        throw new InvalidOperationException(
                            $"容器闭包违规:容器 {container.InstanceId} 的 children 含自身 id —— 自引用(AC-21a-58)");

                    if (!registry.ContainsKey(childId))
                        throw new InvalidOperationException(
                            $"容器闭包违规:容器 {container.InstanceId} 引用未登记实例 {childId} —— children 须指向登记表内 id(AC-21a-58)");

                    parentCount.TryGetValue(childId, out int parents);
                    parentCount[childId] = parents + 1;
                    if (parents + 1 > 1)
                        throw new InvalidOperationException(
                            $"容器闭包违规:实例 {childId} 同属两个容器 —— 子件双属即静默复制(AC-21a-58)");
                }
            }

            // ③ 环检测(三色 DFS;先于 ⑥,使 A→B→A 报环因)
            var color = new Dictionary<long, int>(registry.Count);   // 0 白 1 灰 2 黑
            foreach (long id in registry.Keys)
                if (!Visit(id, registry, color))
                    throw new InvalidOperationException(
                        $"容器闭包违规:容器图存在环(经 id {id} 可达自身)—— children 引用成环即无限递归(AC-21a-58)");

            // ⑥ 嵌套容器拒收(Schema E:children 只能是叶子 id)
            foreach (ItemInstance container in instances)
            {
                if (container.Children == null) continue;
                foreach (long childId in container.Children)
                {
                    ItemInstance child = registry[childId];
                    if (child.Children != null && child.Children.Length > 0)
                        throw new InvalidOperationException(
                            $"容器闭包违规:容器 {container.InstanceId} 的子件 {childId} 自身也是容器 —— " +
                            "Schema E 禁嵌套实例,children 只能是叶子 id(AC-21a-58 边例)");
                }
            }
        }

        /// <summary>三色 DFS:返回 false 表示在本起点发现环。</summary>
        private static bool Visit(
            long id, Dictionary<long, ItemInstance> registry,
            Dictionary<long, int> color)
        {
            color.TryGetValue(id, out int c);
            if (c == 1) return false;                          // 灰节点再入 = 环
            if (c == 2) return true;                           // 黑 = 已证无环子树

            color[id] = 1;
            ItemInstance inst = registry[id];
            if (inst.Children != null)
            {
                foreach (long childId in inst.Children)
                {
                    if (registry.ContainsKey(childId) && !Visit(childId, registry, color))
                        return false;
                }
            }
            color[id] = 2;
            return true;
        }
    }
}
