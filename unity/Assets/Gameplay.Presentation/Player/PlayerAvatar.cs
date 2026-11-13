// 玩家角色的**正式美术载体**(2026-11-13)。
//
// 权威:`design/assets/specs/protagonist.md`(主角规格)· art-bible §8.9.1(主角 ≤ 40,000 tris)
//      · 同 `church-wall` 的 `asset_home: local-only` 裁定(2026-11-13「甲」)。
//
// ═══ 本类补的是什么 ═══════════════════════════════════════════════════════
//   在此之前**玩家没有任何可视模型** —— 只有一个 `CharacterController` 的不可见体积。
//   第三人称相机对着空气推,截图里"玩家在哪"只能从相机位置倒推。
//
// ═══ 三条纪律 ════════════════════════════════════════════════════════════
//   ① **只挂不改判定**:模型是 `CharacterController` 的子节点,零碰撞体、零脚本 ——
//      位移仍由 1 写,模型不参与物理(`PlayerController` 的"唯一位移写入点"不破)。
//   ② **不进世界几何层**:相机摇臂查的是 `WorldGeometry(8)`,玩家模型留在 `Default` ——
//      否则相机把自己队友当墙,贴脸时臂长会被玩家自己顶到最小。
//   ③ **缺席是正常态**:资产 `local-only`(仓库 PUBLIC + 火山方舟 ToS 未定)⇒ 别的机器上
//      没有这个文件,本类跳过并记一行,不抛。

using UnityEngine;

namespace DaYiJingCheng.Gameplay.Presentation.Player
{
    /// <summary>把主角模型挂到玩家实体上(灰盒期:没有则什么都不做)。</summary>
    public static class PlayerAvatar
    {
        /// <summary>`Resources` 下的模型路径(不含扩展名)。</summary>
        public const string ResourcePath = "Art/physician/physician";

        /// <summary>模型高度(米)。用于把脚底对齐到 `CharacterController` 的脚底。</summary>
        public const float ModelHeight = 1.68f;

        /// <summary>挂载。</summary>
        /// <param name="player">玩家根节点(`PlayerController` 所在对象)。</param>
        /// <param name="feetOffset">脚底相对玩家枢轴的 Y 偏移(米)。
        /// <para>`CharacterController` 的 `height = 1.8` / `center = 0` ⇒ 枢轴在胶囊中心,
        /// 脚底在 `-0.9`。模型枢轴约定为**脚底中心**(与 `church-wall` 同一约定)⇒ 直接放 `-0.9`。</para></param>
        /// <param name="yawDegrees">朝向修正(模型自身正面向 vs 玩家正面的差)。</param>
        /// <returns>挂上的模型(缺席 ⇒ `null`)。</returns>
        public static GameObject Attach(Transform player, float feetOffset = -0.9f, float yawDegrees = 0f,
                                       float pitchFixDegrees = 0f)
        {
            if (player == null) return null;

            GameObject asset = Resources.Load<GameObject>(ResourcePath);
            if (asset == null)
            {
                Debug.Log($"[美术] 本地缺席:{ResourcePath}(主角模型)—— local-only 资产不在本机," +
                          "玩家维持不可见体积(不是接进来了)");
                return null;
            }

            GameObject go = Object.Instantiate(asset, player);
            go.name = "PlayerAvatar";
            go.transform.localPosition = new Vector3(0f, feetOffset, 0f);
            go.transform.localRotation = Quaternion.Euler(pitchFixDegrees, yawDegrees, 0f);
            go.transform.localScale = Vector3.one;

            // ① 只挂不改判定:模型上任何碰撞体都摘掉(否则它会与 CharacterController 互相推挤)。
            Collider[] colliders = go.GetComponentsInChildren<Collider>(includeInactive: true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (Application.isPlaying) Object.Destroy(colliders[i]);
                else Object.DestroyImmediate(colliders[i]);
            }

            // ② 留在 Default 层(显式写一次,防美术件被批量改层时误伤)。
            Transform[] all = go.GetComponentsInChildren<Transform>(includeInactive: true);
            for (int i = 0; i < all.Length; i++) all[i].gameObject.layer = 0;

            return go;
        }
    }
}
