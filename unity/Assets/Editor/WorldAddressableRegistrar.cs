// M2 接线轮阶段 1 装配轮 · World 场景的 Addressables 注册工具(一次性 · 保留无害)。
//
// 权威来源:
//   ADR-014 §五(Addressables 数据/场景承载;玩家构建零 JSON 解析器)
//   ADR-023 §① 三场景制(World 由组合根经 Addressables additive 加载)
//
// 存在理由:World.unity 走 Addressables key = "world"(BootRoot 启动序第 3 步)。
// 本工具以 Addressables 官方 API 做**幂等注册 + 读回校验**,既可菜单手工跑,
// 也可 `unity run … -executeMethod` 批处理跑(失败即 throw ⇒ 非零退出码)。
// 存储位置与 5 个 spike 场景同源:Default Local Group 的 `m_SerializeEntries`。

using System;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace DaYiJingCheng.Editor.Boot
{
    /// <summary>把 <c>Assets/Scenes/World.unity</c> 以 address = <c>world</c> 注册进 Addressables 默认本地组。</summary>
    public static class WorldAddressableRegistrar
    {
        /// <summary>场景资产路径。</summary>
        public const string ScenePath = "Assets/Scenes/World.unity";

        /// <summary>Addressable address(= BootRoot 启动序第 3 步的 key)。</summary>
        public const string Address = "world";

        [MenuItem("大医精诚/Boot/注册 World 场景到 Addressables")]
        public static void Register()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
                throw new InvalidOperationException("[WorldAddr] AddressableAssetSettings 不存在 —— 拒以空配置冒充成功");

            string guid = AssetDatabase.AssetPathToGUID(ScenePath);
            if (string.IsNullOrEmpty(guid))
                throw new InvalidOperationException($"[WorldAddr] 找不到资产「{ScenePath}」—— 注册中止");

            AddressableAssetGroup group = settings.DefaultGroup;
            if (group == null)
                throw new InvalidOperationException("[WorldAddr] 默认组缺失 —— 注册中止");

            AddressableAssetEntry entry = settings.FindAssetEntry(guid);
            bool isNew = entry == null;
            string oldAddress = entry?.address;

            // CreateOrMoveEntry = 幂等(新建或移入默认组;Addressables 2.x 官方创建面)
            entry = settings.CreateOrMoveEntry(guid, group, false, true);
            if (entry == null)
                throw new InvalidOperationException($"[WorldAddr] CreateOrMoveEntry 失败(guid = {guid})");

            if (entry.address != Address)
            {
                entry.SetAddress(Address, true);
                Debug.Log($"[WorldAddr] 地址设为「{Address}」(原「{oldAddress ?? "(无)"}」)");
            }

            Debug.Log(isNew
                ? $"[WorldAddr] 新建条目:guid={guid} address={Address} group={group.name}"
                : $"[WorldAddr] 条目已在位:guid={guid} group={group.name}");

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();

            // ── 读回校验(拒绝「写了但没生效」)──
            AddressableAssetEntry check = settings.FindAssetEntry(guid);
            if (check == null)
                throw new InvalidOperationException($"[WorldAddr] 读回校验失败:guid={guid} 查无条目");
            if (check.address != Address)
                throw new InvalidOperationException(
                    $"[WorldAddr] 读回校验失败:address =「{check.address}」≠ 期望「{Address}」");

            Debug.Log($"[WorldAddr] ✅ 注册确认:address={check.address} guid={guid} group={check.parentGroup?.name}");
        }

        /// <summary>CI / 命令行入口(<c>-executeMethod</c>);失败 = 抛出 ⇒ batch 非零退出。</summary>
        public static void RegisterFromCli() => Register();
    }
}
