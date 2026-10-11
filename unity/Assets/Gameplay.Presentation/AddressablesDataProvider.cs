// 权威来源:ADR-014 §五(Addressables 装载落边界程序集 IDataProvider;E-13 加载失败 = 启动期硬失败)
//          · IDataProvider(Sim.Contracts):「无错误返回通道 —— 实现方 try/catch 后直接 throw,不得返回默认值」
//          · Story 008(运行期取数唯一实现)
//
// ⚠️ E-13:Unity 6 Addressables 6.2+ 可能抛异常而非返回 null(post-cutoff,须桌面实测)——
//    全路径 try/catch 后带上下文 rethrow;任何 null / 短于头 / 魔数错同样 throw;绝不 null 解引用。
// ⚠️ 本程序集零 JSON 解析、零 FixParse(AC-48 结构守卫 RuntimeSourceGuard 扫描面内)。

using System;
using DaYiJingCheng.Gameplay.Presentation.Diagnosis;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using SignDataSet = DaYiJingCheng.Gameplay.Presentation.Diagnosis.DiagnosisSignDataSet;
using ReadFloorDataSet = DaYiJingCheng.Gameplay.Presentation.Diagnosis.DiagnosisReadFloorDataSet;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DaYiJingCheng.Gameplay.Presentation
{
    /// <summary>data-core 组 cooked 产物的 <see cref="IDataProvider"/> 实现(Addressables 装载 + 解码 + 缓存)。
    /// <para>失败语义:任何装载 / 校验失败 ⇒ 带 [E-13] 上下文的 <see cref="InvalidOperationException"/>
    /// (启动期硬失败),<b>绝不</b>返回默认值或 null。</para>
    /// <example><code>var provider = new AddressablesDataProvider();
    /// ItemDataSet items = provider.Load&lt;ItemDataSet&gt;();</code></example>
    /// </summary>
    public sealed class AddressablesDataProvider : IDataProvider
    {
        /// <summary>物品种子表 Addressables address(= DataCooked 文件名,菜单写入时设置)。</summary>
        public const string ItemsAddress = "item_database_items.cooked.bytes";

        /// <summary>配方表 Addressables address。</summary>
        public const string RecipesAddress = "item_database_recipes.cooked.bytes";

        /// <summary>8 词表装载常量(cooked 产物文件名,与 bake / DataCooked 目录同值)。</summary>
        private static class SignsAddr
        {
            /// <summary>词条表文件名(字面串 —— 不是型名 token)。</summary>
            public const string Signs = "diagnosis_signs.cooked.bytes";

            /// <summary>门槛定表文件名(同上)。</summary>
            public const string Floor = "diagnosis_read_floor.cooked.bytes";
        }

        /// <summary>词条表 address(S1.2;bake 产物同目录)。</summary>
        public const string SignTableAddress = SignsAddr.Signs;

        /// <summary>门槛定表 address(S1.2;bake 产物同目录)。</summary>
        public const string ReadFloorTableAddress = SignsAddr.Floor;

        /// <summary>data-core 组标签(ADR-014 §五:单组预载)。</summary>
        public const string DataCoreLabel = "data-core";

        private ItemDataSet? _items;
        private RecipeDataSet? _recipes;
        // 8 侧两数据集型经文件头 **using 别名**(SignDataSet / ReadFloorDataSet)引入。
        private SignDataSet? _signs;
        private ReadFloorDataSet? _readFloor;

        // ══════════ 8 词表/门槛装载(sprint-07 S1.2)══════════
        // 逃逸判据(S1.2 修订 2026-10-11):CheckSourceEscape 只判「非 8 前缀文件**声明**了
        // 8 类型」;**引用/消费** 8 公开 API = 合法面 ⇒ 本文件直接引用,无需绕 token。

        /// <inheritdoc />
        /// <exception cref="InvalidOperationException">装载失败 / 未知数据集类型(启动期硬失败)。</exception>
        public TDataSet Load<TDataSet>() where TDataSet : struct
        {
            if (typeof(TDataSet) == typeof(ItemDataSet))
            {
                EnsureItems();
                return (TDataSet)(object)_items.Value;
            }
            if (typeof(TDataSet) == typeof(RecipeDataSet))
            {
                EnsureRecipes();
                return (TDataSet)(object)_recipes.Value;
            }
            // ── S1.2(2026-10-11):8 侧两数据集经 codec 装载(逃逸判据只拦「声明」,引用放行)──
            if (typeof(TDataSet) == typeof(SignDataSet))
            {
                EnsureSigns();
                return (TDataSet)(object)_signs.Value;
            }
            if (typeof(TDataSet) == typeof(ReadFloorDataSet))
            {
                EnsureReadFloor();
                return (TDataSet)(object)_readFloor.Value;
            }
            throw new InvalidOperationException(
                $"[E-13][data-core] 未知数据集类型 {typeof(TDataSet).FullName} —— 启动期硬失败;" +
                "IDataProvider 只承载 ItemDataSet / RecipeDataSet + 两份 8 侧产物(S1.2 扩)");
        }

        /// <summary>首次 Step 前的全量预载(调用契约见 <see cref="DataCorePreloader"/>;幂等,重复调用走缓存)。</summary>
        /// <exception cref="InvalidOperationException">任一产物装载失败(启动期硬失败)。</exception>
        public void Preload()
        {
            EnsureItems();
            EnsureRecipes();
            EnsureSigns();
            EnsureReadFloor();
        }

        private void EnsureItems()
        {
            if (_items.HasValue) return;
            byte[] bytes = FetchBytes(ItemsAddress);
            try
            {
                _items = CookedCodec.ReadItemDataSet(bytes);
            }
            catch (Exception ex) when (!(ex is InvalidOperationException))
            {
                throw new InvalidOperationException(
                    $"[E-13][data-core] 物品表 cooked 解码失败(address={ItemsAddress})—— 启动期硬失败,绝不降级:{ex.Message}",
                    ex);
            }
        }

        private void EnsureRecipes()
        {
            if (_recipes.HasValue) return;
            byte[] bytes = FetchBytes(RecipesAddress);
            try
            {
                _recipes = CookedCodec.ReadRecipeDataSet(bytes);
            }
            catch (Exception ex) when (!(ex is InvalidOperationException))
            {
                throw new InvalidOperationException(
                    $"[E-13][data-core] 配方表 cooked 解码失败(address={RecipesAddress})—— 启动期硬失败,绝不降级:{ex.Message}",
                    ex);
            }
        }

        private void EnsureSigns()
        {
            if (_signs.HasValue) return;
            byte[] bytes = FetchBytes(SignTableAddress);
            try
            {
                _signs = DiagnosisSignCookedCodec.Read(bytes);
            }
            catch (Exception ex) when (!(ex is InvalidOperationException))
            {
                throw new InvalidOperationException(
                    $"[E-13][data-core] 8 词条表 cooked 解码失败(address={SignTableAddress})—— 启动期硬失败,绝不降级:{ex.Message}",
                    ex);
            }
        }

        private void EnsureReadFloor()
        {
            if (_readFloor.HasValue) return;
            byte[] bytes = FetchBytes(ReadFloorTableAddress);
            try
            {
                _readFloor = DiagnosisReadFloorCookedCodec.Read(bytes);
            }
            catch (Exception ex) when (!(ex is InvalidOperationException))
            {
                throw new InvalidOperationException(
                    $"[E-13][data-core] 8 门槛定表 cooked 解码失败(address={ReadFloorTableAddress})—— 启动期硬失败,绝不降级:{ex.Message}",
                    ex);
            }
        }

        /// <summary>经 Addressables 取 TextAsset 字节;一切失败路径带 [E-13] 上下文 rethrow,绝不返回 null。</summary>
        private static byte[] FetchBytes(string address)
        {
            try
            {
                var handle = Addressables.LoadAssetAsync<TextAsset>(address);
                TextAsset asset = handle.WaitForCompletion(); // 启动期同步等(E-13:6.2+ 可能抛而非 null)
                if (asset == null)
                    throw new InvalidOperationException($"Addressables 返回 null(address={address})");
                byte[] bytes = asset.bytes;
                if (bytes == null || bytes.Length < CookedFormat.HeaderSize)
                    throw new InvalidOperationException(
                        $"产物字节为空或短于头部 {CookedFormat.HeaderSize} B(address={address})");
                return bytes;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"[E-13][data-core] 启动期硬失败:装载 \"{address}\" 失败 —— " +
                    "ADR-014 §五:装载异常不得 null 解引用、不得降级,直接终止启动。原因:" + ex.Message,
                    ex);
            }
        }
    }
}
