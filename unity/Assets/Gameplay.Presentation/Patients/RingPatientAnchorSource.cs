// 权威来源:
//   ADR-016 §一(三源不变量:同 id 恒同点;禁 PRNG / 墙钟 / 未版本化几何)
//   ADR-015 §三(`WorldPos` 单一整数格 —— 本类返回格,不返回连续坐标)
//   ADR-009 §一(锚点 = 派生态:不进三流、不存档、重建期重建)
//
// 🔁 **2026-10-30(`TD-prescription-06` 的「灰盒做好,等真实资产兑换」轮)**:
//   从 `Gameplay.Boot/PatientVisualSpawner.cs` 的 `SpawnAnchorFor` **原样迁来**,
//   但**改成返回整数格**而非 `Vector3`(承 ADR-015 §三:格 → 连续坐标的换算归表现层)。
//
// ⚠️ **这是接线期占位,不是最终形态**:真实锚点归 **6 世界生态区 + 13 病人 AI 空间侧**
//   (`LogiPose` / `HomeRegion`)。兑换 = 在 `CompositionRoot` 换掉这一行。
//
// ⚠️ **占位算法为什么是「围绕玩家的环」**:它必须同时满足三件事 ——
//   ① **确定性**(同 id 恒同点,禁 PRNG —— ADR-016 §一);
//   ② **可区分**(不同 id 不重叠,否则交互的「最近病人」判定退化);
//   ③ **可见**(生成在玩家附近,否则走查看不到)。
//   「按 id 稳定哈希折到环上」是三者都满足的最简形态;真位置来了整条退役。

using DaYiJingCheng.Sim.Contracts;
using UnityEngine;

namespace DaYiJingCheng.Gameplay.Presentation.Patients
{
    /// <summary>病人锚点的**占位实现** —— 围绕玩家按 id 稳布于一个环上(接线期)。
    /// <para><b>纯函数确定性</b>:同 id 恒同点(禁 PRNG);用 `SplitMix64` 把 id 折到 `[0, 2π)`。</para>
    /// <para>⚠️ 本类**读玩家当前位置**(表现态)—— 这使它**不是** ADR-016 §一 意义下的
    /// 「可从三源重建」之物。它**是**占位:真实锚点(6 + 13 的整数格)天然满足三源不变量,
    /// 而本类的「围绕玩家」在接线期是有意为之的临时妥协(走查必须看得见病人)。
    /// ⚠️ 因此本类**只允许**被表现层消费,**禁止**任何 sim 侧系统读它(那会破 ADR-016 §一)。</para>
    /// </summary>
    public sealed class RingPatientAnchorSource : IPatientVisualAnchorSource
    {
        /// <summary>环半径(m;接线占位 —— 真实位置归 6 + 13)。</summary>
        private const float RingRadiusMeters = 2.5f;

        /// <summary>玩家对象名 —— 与 `PlayerController` 的宿主 GameObject 同名(接线约定)。</summary>
        private const string PlayerObjectName = "Player";

        /// <summary>格边长的米数 —— 占位期取 1 m/格(真值归 ADR-015 §三 的世界格定义)。</summary>
        private const float MetersPerCell = 1f;

        /// <inheritdoc />
        /// <remarks>
        /// ⚠️ 恒返回 <c>true</c>:占位期「所有在场病人都有锚点」(它们围绕玩家排开)。
        /// 真实实现会返回 <c>false</c>(如病人尚未进入世界 / 无空间分配)。
        /// ⇒ 该分支**今天不可达**,如实登记 —— 不假装测过。
        /// </remarks>
        public bool TryGetAnchorCell(int patientId, out WorldPos cell)
        {
            // 用稳定哈希把 id 折到 [0, 2π):SplitMix64 是 sim 侧哈希源,表现层可读(纯函数)
            ulong h = SplitMix64.Hash(patientId, 0);
            float angle = (float)(h % 100000UL) / 100000f * Mathf.PI * 2f;

            // 环上的连续偏移 → 玩家所在格 → 加环偏移后取整格
            Vector3 origin = PlayerOrigin();
            float x = origin.x + Mathf.Cos(angle) * RingRadiusMeters;
            float z = origin.z + Mathf.Sin(angle) * RingRadiusMeters;

            cell = new WorldPos(
                Mathf.RoundToInt(x / MetersPerCell),
                Mathf.RoundToInt(origin.y / MetersPerCell),
                Mathf.RoundToInt(z / MetersPerCell));
            return true;
        }

        /// <summary>玩家当前位置(找不到 ⇒ 原点)。</summary>
        /// <remarks>
        /// ⚠️ `GameObject.Find` 是**按名字**的弱耦合 —— 接线期可接受(全案唯一的玩家宿主),
        /// 真位置实现(6 + 13)不需要它。若将来出现第二个同名对象,这里会静默取到错的,
        /// 故真锚点落地时本类**整体退役**,不做「按名字找」的加固。
        /// </remarks>
        private static Vector3 PlayerOrigin()
        {
            var player = GameObject.Find(PlayerObjectName);
            return player != null ? player.transform.position : Vector3.zero;
        }
    }
}
