// M2 接线轮阶段 1 装配轮 · 组合根案 A(2026-10-09)
//
// 权威来源:
//   ADR-023 §① 三场景制(Boot 常驻,永不用 LoadSceneMode.Single)· §② Boot 可有相机 / AudioListener
//     · §⑤ 拆序六步(本阶段只做「加载」半边;卸载 / flush 归 Phase 2)
//   ADR-014 §五(Addressables 首次 Step 前预载;E-13 启动期硬失败 = 抛,不 null 解引用降级)
//   ADR-025 §①(Gameplay.Boot 装配行,2026-10-09 增补)
//   OQ-25-8(TICK_SECONDS = 0.05;1 tick ≈ 3 帧不整除是刻意设计,步相位由 ITickProvider 驱动)
//
// 启动序(顺序不可改):
//   1. Addressables.InitializeAsync(autoReleaseHandle: false)(显式 init;await Task;
//      失败 = LogError + rethrow,E-13 硬失败;成功后自己 Release init 句柄)
//   2. CompositionRoot.Assemble()(生产装配,失败即异常向上传播)
//   3. Addressables.LoadSceneAsync("world", Additive) —— 永不 Single。
//      ⚠️ 本版(2.10.3)的 LoadSceneAsync **无 autoReleaseHandle 形参**,可用等价语义 =
//      默认 SceneReleaseMode.ReleaseSceneWhenSceneUnloaded(卸载时自动释放 handle);
//      handle 存 _worldLoadHandle 供 Phase 2 ADR-023 §⑤ 拆序第 4 步使用 —— 非泄漏。
//   4. 生成玩家(PlayerController)并 Initialize(Host / 流 / tick 驱动 / 编码器 / actorId)
//
// ⚠️ 刻意不抽 ISceneRouter:Phase 1 只有一个 World 场景,路由器是第二个场景(MainMenu)
//    出现时才有的真实复杂度 —— 现在抽 = 为不存在的调用方立接口(与 ADR-023 §⑤「拆序
//    六步」只实现当下半边同纪律)。届时再抽,本注即登记点。

using System;
using System.Collections.Generic;    // IReadOnlyList<string>(A-5 错误的返回形,TD-audio-032 ③ 拆分后进签名)
using System.IO;                     // Path.Combine(7a 存档目录,S19-1)
using System.Threading.Tasks;
using DaYiJingCheng.Gameplay.Presentation;
using DaYiJingCheng.Gameplay.Presentation.Audio;      // 44 运行期音频链(story-018 装配面)
using DaYiJingCheng.Gameplay.Presentation.Camera;
using DaYiJingCheng.Gameplay.Presentation.Input;      // DeviceSwitchTuningDataSet(第十一片取值缝)
using DaYiJingCheng.Gameplay.Presentation.Player;
using DaYiJingCheng.Gameplay.Presentation.Settings;   // AudioSettingsRegistry(S14-6 注入缝)
using DaYiJingCheng.Gameplay.Presentation.Telemetry;  // TelemetryAnalyticsTuning / DataSet(第十二片取值缝)
using DaYiJingCheng.Gameplay.UI.Skeuomorphic;
using DaYiJingCheng.Sim.Contracts;                    // IDataProvider(ApplyAudioSettingsProvider 形参)
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Audio;                              // AudioMixer(story-018 装载缝形参)
using UnityEngine.EventSystems;               // EventSystem(B-6-1:官方 UI 输入桥宿主)
using UnityEngine.InputSystem;                 // InputSystem.actions(project-wide 动作资产)
using UnityEngine.InputSystem.UI;              // InputSystemUIInputModule(ADR-013 §F1 官方桥)
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;                 // VisualElement(面板 root 取用)

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>
    /// 启动序宿主(挂 Boot.unity)—— 启动序与每帧 tick 泵的唯一承载。
    /// <para><b>只渲染 / 只驱动,不持有游戏状态</b>(与 42 / 44 / 2 相机同纪律,ADR-013 §9 C3):
    /// 本类只把墙钟折成 tick 并转发给已装配的订阅者,不保存任何 sim 真值。</para>
    /// </summary>
    public sealed class BootRoot : MonoBehaviour
    {
        private SimTickDriver _tickDriver;
        private CompositionRootServices _services;
        private PlayerController _player;
        private CameraRig _cameraRig;
        private SkeuoRuntimeDriver _uiDriver;
        private InteractionInputDriver _interactionDriver;
        private BuildInputDriver _buildDriver;      // S20-19:建造输入驱动

        /// <summary>官方 UI 输入桥宿主(B-6-1)。<see cref="EnsureEventSystem"/> 建或复用;
        /// 面板 <c>selectableGameObject</c> 就绪后由 <see cref="BootstrapEventSystemSelection"/> 选中。</summary>
        private EventSystem _eventSystem;

        /// <summary>World 场景的加载句柄(Phase 2 拆序第 4 步 <c>UnloadSceneAsync</c> 所需;
        /// 本版默认 releaseMode = 场景卸载时自动释放,常驻不等于泄漏)。</summary>
        private AsyncOperationHandle<SceneInstance> _worldLoadHandle;

        private bool _booted;

        /// <summary>
        /// 本启动序装载的世界是否是**最小合成世界**(S20-17 · 2026-10-28)。
        /// <para>真 ⇔ <see cref="MinimalWorldFactory.Create"/> 的产物(16×1×16 全 walkable +
        /// 空槽位目录),即「世界烘焙数据尚未产出」的状态。</para>
        /// <para>⚠️ <b>这是可观测的诊断位,不是游戏状态</b>(本类不持有 sim 真值,见类注):
        /// 它只陈述「装配从哪来」。世界数据落地后由装载路径替换 ⇒ 本属性应随之为 false。</para>
        /// </summary>
        public bool IsSyntheticWorld { get; private set; }

        /// <summary>44 侧装配缝(story-018)—— <see cref="BuildAudioSeams"/> 的产物。
        /// <para>本类只**持有引用**用于卸载期释放(<see cref="AudioClipLibrary.Dispose"/>);
        /// cue 语义 / 状态一律不在此(ADR-013 §9 C3 · ADR-018 §一)。</para></summary>
        private AudioAssemblySeams _audioSeams;

        /// <summary>3 的设备态(启动序 2.9 挂接;TD-epic-01 第八片 —— cause 判据的输入面)。</summary>
        private Input.DeviceStateManager _deviceState;

        /// <summary>
        /// 退出钩子回调(AC-7a-17 · `TD-persist-06`)。**必须缓存成字段**:
        /// <c>Application.wantsToQuit</c> 是**静态**事件,每次注册都要给同一个 delegate 实例
        /// 才能注销 —— 用 lambda 直接 `+=` 之后无法 `-=`(每次求值都是新对象),
        /// 会留下悬挂引用在下次 Play 里再触发一次。
        /// </summary>
        private Func<bool> _wantsToQuitHandler;

        /// <summary>
        /// 启动期读到的「上一次会话未完整保存」标志(AC-7a-17)。
        /// <para>⚠️ **只读位、不出提示** —— GDD `:586` 逐字「提示承载归 42 / 7b,
        /// 7a 只断言标志位置位」。本字段是**交给 42 / 7b 的交接点**,不是提示本身。
        /// 在 42 / 7b 接上之前,它没有任何消费者 —— 这是**如实登记的缺口**,不是静默满足。</para>
        /// </summary>
        private bool _sessionWasIncomplete;

        /// <summary>
        /// 只缓存引用 / 初始化字段 —— **零异步、零装配**(装配严格在 <see cref="Start"/> 启动序第 2 步)。
        /// </summary>
        private void Awake()
        {
            // 字段默认值即所需;此处刻意不 Assemble(启动序顺序不可改)。
            _booted = false;
        }

        /// <summary>
        /// 启动序(见文件头四步;顺序不可改)。失败一律 rethrow —— 停在错误态,
        /// 不降级为 null 解引用(E-13 启动期硬失败)。
        /// </summary>
        private async void Start()
        {
            // ── 1. Addressables 显式初始化(E-13 硬失败)──
            // ⚠️ 本工程 Addressables 2.10.3 的 handle **不可直接 `await`**(无 GetAwaiter),
            //    await 面 = `handle.Task`(AsyncOperationHandle.Task 属性);且无参
            //    `InitializeAsync()` 默认 autoReleaseHandle = true ⇒ 完成后 handle 即失效,
            //    再读 Status 会抛「invalid operation handle」(U1 spike 坑 A 同源)。
            //    故:显式 autoReleaseHandle: false + await Task + 读 Status + 成功后自己 Release。
            var initHandle = Addressables.InitializeAsync(autoReleaseHandle: false);
            try
            {
                await initHandle.Task;
                if (initHandle.Status == AsyncOperationStatus.Failed)
                    throw new InvalidOperationException(
                        "Addressables.InitializeAsync() 返回 Failed" +
                        (initHandle.OperationException != null
                            ? ":" + initHandle.OperationException.Message
                            : string.Empty));
            }
            catch (Exception e)
            {
                Debug.LogError($"[BootRoot][E-13] Addressables 初始化失败 —— 启动终止,不降级:{e}");
                throw;
            }
            finally
            {
                // 我们持有的 init 句柄,用完即释(防泄漏)—— 放 finally:成功与失败路径都释放
                // (评审代码面 F1:原裸 Release 在 try/catch 之外,失败路径泄漏句柄)。
                Addressables.Release(initHandle);
            }

            // ── 2. 生产装配(fail-loud:任一依赖缺失 = 具名异常,无半装配袋)──
            // ⚠️ 2026-10-12 C3-B1:data-core 预载 + 带 provider 装配 —— 原裸
            //   `Assemble()` 使 S1.2 的 Addressables 生产分支成为死代码。
            //   现生产恒走乙案(Addressables 装载同一份 cooked);测试/未重建 catalog
            //   环境 TryPreload 失败 ⇒ 组合根磁盘兜底(同一份字节,真装载)。
            //   生产构建乙案失败 ⇒ 磁盘兜底路径不存在(DataCooked 不进构建)⇒ 仍 E-13 硬失败。
            bool preloaded = DataCorePreloader.TryPreload();   // 首次 Step 之前(ADR-014 §五)

            // ── 2.4 44 侧设置注册表注入缝(S14-6 · story-010 · 兑清 story-011 §登记项)──
            // story-011 的 `AudioSettingsRegistry` 立了 `Provider` 缝并逐字登记
            // 「运行期装载面归 story-010 的 Addressables 接线轮」。本处即该轮:
            // 预载成功 ⇒ 烘焙分区(AC-44-13 真源)成为 `Rows` 的供给;
            // 预载失败 ⇒ **不注入**,`Rows` 走 `FallbackRows`(与烘焙分区同值,
            // 由 AudioSettingsRegistryBinder 的构建期双向值对账钉住)。
            // ⚠️ 这里**刻意不抛**:设置值不是 sim 真值(端本地设备偏好,OQ-SS-5=甲),
            //    「回退集」是设计内的降级面,不是静默失败 —— 与 E-13 对**数据产物**的
            //    硬失败口径不冲突(那条管的是「产物不可读」,此处是「未装配」)。
            // ⚠️ 2026-10-17 修复:`DataCorePreloader.Provider` 是**抛形**属性(null 即抛),
            //    不能无条件求值再靠形参判 null —— 预载失败时求值本身就抛,「不注入」分支不可达
            //    (PlayMode 9 红实测:静态 `IsPreloaded` 跨 Play 轮残留 ⇒ TryPreload 短路返回 true
            //    而 `_provider` 已随上一轮失效)。改三元短路:只有真成功才取 Provider,
            //    且取到的 null 仍由 `ApplyAudioSettingsProvider` 的形参检查兜住。
            ApplyAudioSettingsProvider(preloaded, preloaded ? DataCorePreloader.Provider : null);

            // ── 2.6 44 运行期音频链装配(story-018 · A-1…A-7 的启动序半边)──
            // 解析混音器 → 赋值 `MixerResolver` → 应用 `FactoryDefaults`;同时建 clip 库 +
            // 总线解析器,交给组合根把「池 → 渲染目标 → sink」接起来(A-1/A-2/A-4)。
            // ⚠️ `PlayerBusVolumeInitializer.Bootstrap()` 是 `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]`
            //    —— 它在**场景加载前**就跑过一遍,那时 `MixerResolver` 尚未赋值 ⇒ 静默 return。
            //    故 `BuildAudioSeams` 内赋值后**显式**调 `ApplyDefaults`(不指望自动入口二次触发)。
            // ⚠️ 预载失败(provider == null)⇒ 缝不接:`Assemble` 走 `NullRenderTarget` 链,
            //    `AudioRenderAssembled = false` 可观测 —— 不是静默失败。
            _audioSeams = BuildAudioSeams(preloaded ? DataCorePreloader.Provider : null);

            // ── 2.5 项目级动作资产 fail-loud(story-009 R-9.5 · AC-9-01)──
            // 通道装载 = InputSystem.actions(原稿 Addressables 路径被实测推翻:该资产是
            // 项目级设置、git-tracked;Addressables 装载同资产 = 第二实例,违 AC-3-A1)。
            // 缺失即抛(E-13 同口径),不静默降级为零急救输入 + 零 UI 输入。
            var inputActions = InputSystem.actions;
            if (inputActions == null)
                throw new InvalidOperationException(
                    "[BootRoot][story-009] InputSystem.actions 为 null —— 项目级动作资产未注册。" +
                    "检查 ProjectSettings/EditorBuildSettings.asset 的 com.unity.input.settings.actions" +
                    "(期望 guid 052faaac586de48259a63d0c4782560b)。急救直读通道与 UI 输入将不可用。");

            // ── 2.8 World 场景装配轮(S20-17 · 2026-10-28)────────────────────────────
            // 病因:`CompositionRoot.cs:787-796` 逐字记着「23 的 `StructureInstanceRegistry`
            //   在生产树**零构造**(`new World` 的构造同样零构造)」,并指名「`BootRoot` 的 World
            //   场景装配轮」。`impl-s20-2a-building01-2026-10-25.md:176` 的 N5 把这件事记为
            //   「本件未做」,而 `TD-building-01` 已于 2026-10-25 结案 ⇒ 缺口零载体。
            //
            // ⚠️ **为什么是「合成」而不是「装载」** —— 世界几何的真源 = ADR-022 关卡工具产出
            //    → ADR-014 烘焙的 `world_*.cooked`。今日**三样全无**(`assets/data/` 零份
            //    `world_*` JSON、DataCooked 的 25 份产物里零份世界数据),而关卡工具已被裁定
            //    「不立 epic、不排期」⇒ 装配轮**没有可装载的输入**。用户 2026-10-28 裁定边界
            //    取「最小合成世界」。详见 `MinimalWorldFactory` 的文件头。
            //
            // ⚠️ **本装配解除了哪些、没解除哪些**:
            //    解除:① `World` 在生产路径上**存在**;② `StructureWriter` 有生产实例
            //          (`Place` / `Remove` 可被调用);③ `ClinicRoomMatching` 首次非 null。
            //    **没解除**:建造的**发起方**归 42 的建造交互(`O-23-6`),今日零调用点
            //          ⇒ 「写者存在」≠「玩家能建造」;`BuildSlotCatalog` 为空 ⇒
            //          `AC-23-08` 第三半边仍无载体。逐条登记在 sprint-20 §十的卡里。
            // ⚠️ 2026-10-28(S20-20 装载面补接):**必须**传 provider —— 此前无参调用使槽位骨架
            //    只能走 `DataCookedDisk` 的磁盘路径,而 `Assets/DataCooked` 不随玩家构建出货
            //    ⇒ 生产构建的槽位目录**恒空**(`PlaceableChecker` 恒 `SlotNotFound`)。
            //    预载失败(未重建 catalog / 组条目缺)⇒ 传 null 走磁盘兜底(编辑器可读,
            //    玩家构建必失败 ⇒ 空目录 + LogError,与接线前同形,不静默)。
            // ── ① 格边长换源(`TD-audio-035` ① · 2026-10-29)────────────────────────
            // 病因:44 的空间化换算此前读 `AudioTuning.LatticeSizePlaceholder`(44 自造的 1f),
            //   而 `LATTICE_SIZE` 归**系统 6** —— `LatticeToWorld` 文件头明令「44 不得自造比例」。
            //   6 侧的载体早已存在(关卡工具 story-002 起 `world_geometry` 带 `lattice_size_mm`,
            //   经 ADR-014 烘进 `world_geometry.cooked`),但**运行期无出口** ⇒ 装配层取不到。
            //   本处接上那条出口,44 侧从此只**消费** 6 的值。
            // ⚠️ 数值本身**未定**(`P0ClinicLevel.LatticeSizeMm` 自陈「占位值,归用户数值轮」)
            //   —— 本轮改的是**来源**,不是**数值**。数值轮改 6 侧一处,44 的米制尺度随之变。
            // ⚠️ 赋值**必须在 `Assemble` 之前**:组合根在装配期一次性把该值交给
            //   `SpatializationDirector` / `AudioSourceRenderTarget`,之后再写不生效。
            var world = MinimalWorldFactory.Create(preloaded ? DataCorePreloader.Provider : null,
                                                  out int latticeSizeMm);
            AudioAssemblySeams.ApplyLatticeFromWorldGeometry(_audioSeams, latticeSizeMm);
            // 🔁 **2026-10-29(关卡工具 story-002)此处置 `false`,原文不删**:
            //    此前恒 `true` —— 世界几何由 `MinimalWorldFactory` **合成**(16×1×16 全 walkable),
            //    因为 `world_*.json` / `world_*.cooked` 三样全无。story-002 之后
            //    `MinimalWorldFactory` 是**纯装载器**(几何 / 可走性 / 速度档全部来自烘焙产物,
            //    产物缺失即启动期硬失败),合成支已删除 ⇒ 本属性在当前生产路径上**恒 false**。
            //    ⚠️ 属性本身保留:它是 PlayMode 可断言的可观测诊断位,且 AC-4 的判据援引它。
            //    若将来重新引入合成支(须另裁,理由见 `MinimalWorldFactory` 文件头 ①②③),
            //    这里必须重新如实赋值 —— 不得让它变成「写着 false 但可能合成」。
            IsSyntheticWorld = false;

            _services = CompositionRoot.Assemble(
                registry: null,
                dataProvider: preloaded ? DataCorePreloader.Provider : null,
                inputActions: inputActions,
                audio: _audioSeams,
                // 51 遥测六旋钮(TD-epic-01 第十二片):预载成功 ⇒ 烘焙表;失败 ⇒ null(报告维持「未给」)。
                telemetryKnobs: ResolveTelemetryKnobs(preloaded ? DataCorePreloader.Provider : null),
                // 7a 存档目录(S19-1)。⚠️ 只有本处(生产启动序)显式给目录 —— 109 处
                // EditMode 夹具不给 ⇒ 它们不构造 SaveService(否则各在用户数据目录造一份)。
                // ⚠️ 今日 `Checkpoint` / `Load` 仍必然抛(叙事上下文驱动源未接通,
                //    fail-closed,TD-persist-04)—— 目录就位 ≠ 功能可用。
                saveBasePath: Path.Combine(Application.persistentDataPath, "saves"),
                // S20-17:世界交给装配根 —— 注册表从 `world.Structures` 取(不另传 structures,
                // 否则「同给且不同实例」的 fail-closed 断言会抛),`StructureWriter` 由它构造。
                world: world);

            // ── 2.95 退出保存接线 + 会话未完整标志位(AC-7a-17 · `TD-persist-06` · 2026-10-25)──
            // 病因:`SaveService.SaveOnExit` 方法**早已存在**(`SaveService.cs:356`)但
            //   **零生产调用点** —— `Application.wantsToQuit` 全仓零命中。⇒ AC-7a-17 的
            //   「退出保存失败 ⇒ 限时重试 + 下次启动提示」整条链在**形状上**不存在。
            //
            // ⚠️ **本轮只接「退出保存 + 标志位」,不接「提示」** —— GDD `:586` 逐字
            //   「提示承载归 42 / 7b,7a 只断言标志位置位」。本类读位后**存字段**,
            //   字段的消费者(42 / 7b 的提示 UI)**尚未存在** ⇒ 这是**如实登记的缺口**,
            //   不是静默满足(见 `_sessionWasIncomplete` 的 doc)。
            //
            // ⚠️ ~~今日 `Checkpoint` / `Load` 仍必然抛~~(**2026-10-25 生产接线轮过期**,原文不删):
            //    叙事上下文瞬时源已接通(`NoPendingMedicalActionQuery`,装配根 Bind)⇒
            //    `SaveOnExit` **不再**在 `RequireNarrativeContext` 处抛,而是**真写**
            //    `save_exit.bin`(独立文件名,不占玩家槽)。本 try/catch **保留** ——
            //    它兜的「驱动源缺失 = 编程错误」分支退为纵深,写盘侧异常本就不经此抛。
            //    (原记录:兜住后走「置位 + 记日志」路径,即接线前的生产真实行为。)
            var saveService = _services.SaveService;
            if (saveService != null)
            {
                _sessionWasIncomplete = saveService.IsSessionIncomplete;

                _wantsToQuitHandler = () =>
                {
                    try
                    {
                        saveService.SaveOnExit();
                        // ⚠️ SaveOnExit **不抛**写盘失败(它自己置位 + 交观察者),
                        //    但**会抛**驱动源缺失(编程错误)。两者都在此兜住。
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning(
                            "[BootRoot][AC-7a-17] 退出保存失败(不阻塞退出):" + e);
                    }
                    return true;   // 恒放行退出 —— 「不卡退」是硬约束
                };
                Application.wantsToQuit += _wantsToQuitHandler;
            }

            // ── 2.9 设备态挂接(TD-epic-01 第八片 · 2026-10-19)──
            // 病因:`DeviceStateManager`(story-008)此前**零生产实例**(零生产调用方,与
            // story-008 Handover 第 7 条「四件新 API 零生产调用点」登记吻合)—— 而 10 的
            // `cause`(GDD 规则十二:无模拟量通道 ⇒ 降级)要读它。本处即该接线的启动序半边:
            //   · `Attach()` 快照 `InputSystem.devices` 并按 onDeviceChange 维护在场集;
            //   · 用 3 的 `EmergencyDeviceReadiness` 把读数交给 `EmergencyManualDriver`
            //     (启动序 4.65 的跳过入口之后才有动作,故先于玩家生成即可)。
            // ⚠️ 迟滞六旋钮 = **data-core 烘焙表**(TD-epic-01 第十一片,2026-10-19)——
            //    此前是 `CompositionRoot.EmergencyDeviceSwitchSeed*` 六个手抄常量(与源 JSON
            //    同值但无机械对账 ⇒ 数值轮改一边不改另一边 = 静默分叉)。现行为:
            //      预载成功 ⇒ provider 读 `input_device_switch.cooked.bytes`(真源);
            //      预载失败 ⇒ 常量兜底(与 `AudioSettingsRegistry.Provider` 同款设计内降级面)。
            //    两条路径的等值由 **EditMode 对账夹具**钉住(种子常量 == 烘焙表,逐值),
            //    与 44 侧 `AudioSettingsRegistryBinder.ValidateValueReconciliation` 同构。
            // ⚠️ 未挂接 / 失败 ≠ 阻断启动:读数恒「无模拟量」⇒ 所有 cause 记降级(如实可见,
            //    不是静默记成「玩家选择」)。设备态是 P0 的展示 / 读数面,不是 sim 真值。
            try
            {
                (int axisThreshold, int axisTolerance, int axisDwell,
                 int pointerThreshold, int pointerTolerance, int pointerDwell) =
                    ResolveDeviceSwitchKnobs(preloaded ? DataCorePreloader.Provider : null);
                var devices = new Input.DeviceStateManager(
                    axisThreshold, axisTolerance, axisDwell,
                    pointerThreshold, pointerTolerance, pointerDwell);
                devices.Attach();
                _deviceState = devices;
                _services.EmergencyManualDriver?.BindDeviceReadiness(
                    new Input.EmergencyDeviceReadiness(devices));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[BootRoot][TD-epic-01] 设备态挂接失败 ⇒ cause 记降级(无模拟量):" + e);
            }

            // ── 3. World 场景 additive 加载(ADR-023:永不用 LoadSceneMode.Single)──
            // ⚠️ 本版 `LoadSceneAsync` **没有 `autoReleaseHandle` 形参**(U1 spike 坑 A 的
            //    autoReleaseHandle 属 `UnloadSceneAsync`,不是 Load)。可用的等价语义 =
            //    默认 `SceneReleaseMode.ReleaseSceneWhenSceneUnloaded`:场景卸载时自动释放
            //    该 handle —— 既不泄漏(卸载即释放),又保住 ADR-023 §⑤ 拆序第 4 步
            //    `UnloadSceneAsync(handle)` 所需的句柄;Phase 1 不卸载 ⇒ handle 常驻是
            //    设计而非泄漏。**场景内实例不手动 Release**(坑 B:Addressables 自管)。
            try
            {
                _worldLoadHandle = Addressables.LoadSceneAsync("world", LoadSceneMode.Additive);
                await _worldLoadHandle.Task;
                if (_worldLoadHandle.Status == AsyncOperationStatus.Failed)
                    throw new InvalidOperationException(
                        "LoadSceneAsync(world) 返回 Failed" +
                        (_worldLoadHandle.OperationException != null
                            ? ":" + _worldLoadHandle.OperationException.Message
                            : string.Empty));
            }
            catch (Exception e)
            {
                Debug.LogError($"[BootRoot][E-13] World 场景 additive 加载失败(address = world):{e}");
                throw;
            }

            // ── 4. 玩家生成 ──
            // tick 驱动由组合根创建(ADR-005 抽象点 · G2.2 起墨龄同源;纯 C# 装配件,
            // 非异步;每帧由 Update 泵)。⚠️ 不再本处 new —— 服务袋是 tick 唯一出处。
            _tickDriver = _services.TickDriver;

            var playerObject = new GameObject("Player");
            _player = playerObject.AddComponent<PlayerController>(); // RequireComponent 自动补 CharacterController

            // 玩家场景归属钉死:玩家常驻 Boot 场景 —— Phase 2 拆序第 5 步
            // `UnloadSceneAsync(World)` 不得波及玩家(ADR-023 §⑤;`new GameObject` 默认落在
            // 当前活动场景,World 刚 additive 加载后可能成为活动场景 ⇒ 须显式搬回 Boot)。
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(
                playerObject, gameObject.scene);

            // actorId:玩家 id 走 IIdAuthority 机制 A —— 受伤实体计数器「病人 / 敌人 / 玩家
            // 共用同一空间」(IdAuthority 头注 + ADR-006 Amendment B 第二十六批:不新开第二计数器;
            // entities.yaml 的 next_player_id 是**存档头字段位**,不新增第三个 C# 计数器)。
            int actorId = _services.IdAuthority.NextPatientId().Value;

            // ── 4.4. 玩家 actor 登记(2026-10-24 story-004 装配轮)──
            // id 在**装配之后**才铸造 ⇒ 登记必然是后置动作(与 SetViewer / SetAuthor 同格)。
            // 后果:登记前 `ClassOfDeathCandidate` 恒 Patient ⇒ 死亡候选的类别闸不放行;
            // 登记后玩家才「是玩家」。⚠️ 只登记**本机玩家自己**的 id ——
            // 联机远端玩家的 id 归 45 的会话建立轮(ADR-001 第二 QoS 上行)。
            _services.PlayerActors.Register(actorId);

            // ── 4.45. 建造发起方接线(S20-19 · 2026-10-28)────────────────────────
            // 三条后置绑定(与 SetAuthor / SetViewer / BindPlayer 同格 —— actorId 在
            // `Assemble` 之后才铸造,装配期拿不到):
            //   ① 扣料归属 = 本机玩家(P0 甲案:建造扣料恒归唯一玩家);
            //   ② 取格委托 = `PlayerController.Cell`(**唯一**量化器 `CellFromPosition`);
            //   ③ 落流回调 = 重建库存投影(`StructurePlaced` 的扣减在 fold 里,不重建读不到新余量)。
            //
            // ⚠️ **默认幽灵模块 = 目录里 id 最小的那个** —— 玩家选料界面(42 的幽灵循环选择)
            //   本批**未落地**,`SelectedModuleId` 是留给它的接缝。此默认值只为让
            //   「按键 → 判定 → 落流」这条链在真实运行时可走通,不是设计决定。
            var build = _services.BuildController;
            if (build != null)
            {
                build.BindOwner(actorId);

                int defaultModule = int.MaxValue;
                foreach (int id in build.ModuleIds)
                    if (id < defaultModule) defaultModule = id;

                _buildDriver = new BuildInputDriver(
                    build,
                    () => _player != null ? _player.Cell : default,
                    onCommitted: () => _services.InventoryLoad?.Rebuild());
                _buildDriver.SelectedModuleId = defaultModule == int.MaxValue ? -1 : defaultModule;
            }

            // 52 出诊启动接线(2026-10-25 接线轮):玩家 id 铸造后晚绑定到导演主循环壳
            // (id 在装配之后才生成 ⇒ 后置形态,承 NoPendingMedicalActionQuery.Bind 先例;
            // 未绑定期的边沿驱动为 no-op fail-safe)。
            _services.RandomEventsDirector?.BindPlayer(actorId);

            _player.Initialize(
                SimAuthorityMode.Host,
                _services.EventSink,
                _tickDriver,
                _services.Encoder,
                actorId);

            // 落到世界原点上方 1 m(CharacterController 需落地;落地表现后续 PlayMode 验)。
            _player.Teleport(new Vector3(0f, 1f, 0f));

            // ── 4.5. 玩家交互输入驱动(sprint-07 S2):E 查体 / F 急救,每帧由 Update 转发 ──
            var interaction = new InteractionInputDriver(
                _services,
                () => _player != null ? _player.transform.position : Vector3.zero,
                actorId);
            _interactionDriver = interaction;

            // ── 4.55. 急救直读通道接线(story-009 · AC-9-01)──
            // Attach = 挂 onAfterUpdate + Emergency 动作解析 fail-loud(FindAction 落空 ⇒
            // 通道抛 InvalidOperationException)—— 启动期炸,不带病运行。
            _services.EmergencyChannel.Attach();

            // ── 4.6. 方笺页接线宿主(S2.3/S2.4):SetAuthor(玩家 actorId 已定)──
            // 落笔 / 开方的作者 = 玩家;至此 PrescriptionPageHost 可接动作。
            // ⚠️ 绑定病人由查体驱动(InteractionInputDriver 的 E 查体已切脉案绑定)
            //    —— 方笺页落笔 / 开方对象 = 当前脉案绑定病人(42 侧会话)。
            _services.PrescriptionPage?.SetAuthor(actorId);

            // ── 4.65. 急救跳过入口写路径接线(story-009 · R-9.4)──
            // 候选列表「跳过」项 Submit → EmergencyManualDriver.TryManual(method 1)。
            // actorId 由 BootRoot 生成玩家后注入(SetAuthor 同款后置形态 —— 装配期未知)。
            _services.CasebookCommands?.BindEmergencySkip(_services.EmergencyManualDriver, actorId);

            // ── 4.7. 病名册观看者(sprint-08 S1.1 件 E):SetViewer(玩家 actorId 已定)──
            // 病名册条目集按人分册(铁律⑤「每人一本」)—— 观看者 = 玩家。
            // 至此 42 脉案源可读病名册(未设 ⇒ 病名册空,反幻想二)。
            _services.CasebookSource?.SetViewer(actorId);

            // ── 5. 相机机位装配(sprint-05 T1.5 · 2026-10-10 AudioListener 泄漏修复)──
            // ADR-020:相机 = 自建机位,不引入 Cinemachine
            // 跟随 = 每帧把相机位置设到玩家位置 + 偏移
            //
            // ⚠️ **不自加 AudioListener**(ADR-020 §七 / AC-20-10 单挂点铁律):
            //   T1.5 初版在此 `AddComponent<AudioListener>()`,而 Boot.unity 预摆的
            //   `Main Camera`(Transform + Camera + AudioListener + URP data)**已有一个**
            //   ⇒ 场景内恒 2 个 listener,Unity 每帧刷一条警告。
            //   实测代价(2026-10-10 人工 playtest):Editor.log 31868 条同文本,
            //   Console 被淹 → Editor IPC 管道超载 → `write EPIPE` → 进程异常终止,
            //   表现为「无法退出 Play / 点关闭无响应」。根因是日志洪泛,不是死循环。
            //   修复 ① = 单挂点:本节**禁用**预摆相机(它只持 listener 语义)。
            //   ⚠️ **修复 ②(2026-10-20)** —— 修复 ① 只做了一半:预摆相机被禁用后,
            //   运行时 CameraRig 相机**没有**补 listener ⇒ 运行时 active listener = 0,
            //   整个场景静音。Editor.log 实测 10845 条
            //   `There are no audio listeners in the scene`(首次出现于本次 Play 首帧)。
            //   正确形态 = listener 随「主相机」迁到 CameraRig 运行时相机(ADR-020 §七:
            //   单挂点就是主相机)。预摆相机上的那份**必须先摘除** —— 否则它会以 inactive
            //   形态留在挂载计数里,把「零 active」掩盖成「1 个挂载」(见 AssertSingleAudioListener)。
            // ⚠️ **必须含 inactive 地找预摆相机**(2026-10-20 全量 PlayMode 实测):
            //   `GameObject.Find` 只找 **active** 对象 ⇒ 若预摆相机此刻已 inactive,本节整个跳过
            //   ⇒ 它身上的 listener **摘不掉**,以「挂载但不活动」的形态留在计数里,
            //   与运行时 CameraRig 的那一个并成 **2 个挂载**,`AssertSingleAudioListener` 抛。
            //   实测路径:全量 run 里 `camera_follow_wiring_test` 自建的预摆相机被启动序禁用后
            //   再没被恢复(它的拆卸也用 `GameObject.Find` 恢复 —— 同一处死代码),
            //   后续测试经 `FindPresetCamera`(**含 inactive**)复用它并补 listener
            //   ⇒ 单跑绿、全量红。本件与「单件全绿 ≠ 全量全绿」同型。
            //   语义上本节的义务是「**无论预摆相机处于什么状态**,都不许它与运行时相机并存有 listener」
            //   —— 用 `FindObjectsByType(Camera, includeInactive)` 而非名字 Find 才对得上这句话。
            var presetCamera = FindInactiveInclusive("Main Camera");
            if (presetCamera != null)
            {
                var presetListener = presetCamera.GetComponent<UnityEngine.AudioListener>();
                if (presetListener != null)
                    UnityEngine.Object.DestroyImmediate(presetListener);
                presetCamera.SetActive(false);
            }

            var cameraObj = new GameObject("CameraRig");
            // ⚠️ **必须归 BootRoot**(2026-10-20 · 全量 PlayMode 实测出的一处真泄漏):
            //   `new GameObject` 落**当前活动场景**,而启动序第 3 步已 additive 载入 World
            //   ⇒ 不归 Boot 则 ① Phase 2 卸载 World 会带走相机;② **更早暴露的是测试间泄漏** ——
            //   CameraRig 是根级对象,`BootRoot.OnDestroy` 从不销毁它(它不在 BootRoot 层级下),
            //   每个 PlayMode 测试都留下一个。修前它无害(运行时相机**没有** listener,泄漏件不计数);
            //   本轮把 listener 迁到它身上之后,**每个残留 CameraRig 都带一个 listener**
            //   ⇒ `AssertSingleAudioListener` 的 active 计数逐测递增(2 → 3 → … → 7),全量 7 红。
            //   单件跑看不见(每测试基数都是 1)—— 这是「单件全绿 ≠ 全量全绿」的一次实证。
            //   与 `EnsureEventSystem`(:430)同一纪律,此处用 SetParent 更简
            //   (跨场景 reparent 自动随父进 Boot 场景;相机每帧赋**世界坐标**,reparent 不影响)。
            cameraObj.transform.SetParent(transform, worldPositionStays: false);
            _cameraRig = cameraObj.AddComponent<CameraRig>();
            cameraObj.AddComponent<UnityEngine.Camera>();
            cameraObj.AddComponent<UnityEngine.AudioListener>();   // 运行时主相机 = 唯一听音点
            _cameraRig.SetModeForTest(CameraMode.Explore);

            // ── 5.1. 空间化听者锚点(S15-4 · TD-audio-028)────────────────────
            // 听者 = **主相机**(ADR-020 §七:`AudioListener` 的单挂点就是它)——
            // 用相机位置算距离,与 Unity 自身的距离衰减是**同一个参考点**;
            // 若改用玩家格,第三人称越肩视角下衰减中心会与耳朵错开。
            // ⚠️ 必须在**相机装好之后**注入(组合根建渲染目标时相机还不存在)。
            // ⚠️ 读的是**每帧实时**位置(委托),不是快照 —— 玩家移动时距离必须跟着变。
            if (_services.RenderTarget != null)
                _services.RenderTarget.SetListenerPosition(_cameraRig.Camera.transform);

            // ── 6. 拟物 UI 运行时装配(sprint-05 T2.1 / T2.2)──
            // ADR-023 §①:UI 载体由运行期生成。**例外(prefab 空壳)**:Boot.unity 预摆的
            //    `SkeuoUI`(Transform + UIDocument + SkeuoRuntimeDriver)只持**渲染资源**
            //    (PanelSettings),与预摆 Main Camera 只持 AudioListener 同型 —— 零游戏状态、
            //    零 Screen 实例(BuildUI 在 Awake 里由 driver 跑)。
            //    ⚠️ 为何必须有预摆:2026-10-10 实测(PlayMode 探针)UIDocument 无 PanelSettings
            //       ⇒ `rootVisualElement.panel == null` ⇒ **逻辑树在但不渲染**。因此预摆不是
            //       「第二真源」,是让 panel 非 null 的唯一途径(运行时脚本无法凭空造图标资源);
            //    真 Screen / 真绑定 / 真值区仍在运行期,预摆不参与任何一态。
            _uiDriver = FindOrCreateSkeuoUi();
            if (_services.CasebookSource != null)
                _uiDriver.Source = _services.CasebookSource;
            // ── 6.1 写通道注入(sprint-08 S1.1 件 F1):病名栏 Submit → 装配层写者 ──
            // 42 只渲染不持状态(ADR-013 §9 C3)⇒ 病名栏 Submit 经命令面转发给装配层的
            // CasebookCommandRouter(实现 ICasebookCommandSink)。缺注入 ⇒ Submit 留空(不崩)。
            if (_services.CasebookCommands != null)
                _uiDriver.CommandSink = _services.CasebookCommands;

            // ── 6.5. 急救候选列表挂载(S3.1 · 2026-10-11)──
            // 形态件③「运行期有载体」半边:经组合根的 EmergencyCandidateRuntime 把候选
            // 列表(止血/通气 + 跳过入口)挂到 UI 根。挂载 = 纯渲染资源(42 C3)。
            // ⚠️ 42 侧 future 若把候选列表并入某个 Screen,此处只改挂点,载体语义不变。
            if (_services.EmergencyCandidates != null)
                _services.EmergencyCandidates.MountTo(_uiDriver.GetUiRoot());

            // ── 6.6. 急救候选列表交互接线(story-009 · R-9.4)──
            // 「跳过」项 Submit(键盘 / 鼠标双路径,42 侧 Guarded 回调)→ 命令路由。
            // 候选项动作语义归 21a/10 真动作表轮 —— 本 story 只接跳过。
            if (_services.EmergencyCandidates != null && _services.CasebookCommands != null)
                _services.EmergencyCandidates.SetSkipCallback(
                    () => _services.CasebookCommands.SubmitEmergencySkip());

            // ── 6.7. 官方 UI 输入桥装配(B-6-1 · 2026-10-13)──
            // ADR-013 §F1/F4 + ADR-011 §三:UI Toolkit 平面栈的指针/导航事件经**官方桥**
            // (`EventSystem` + `InputSystemUIInputModule`)—— 此前启动序从未装配它,导致
            // 脉案交互面(病名栏 Submit / 页缘翻页 / 戥子选档)**鼠标与键盘双路径全断**
            // (第六轮 playtest B-6-1)。缺 EventSystem 时 UI Toolkit 会退回引擎内置
            // `DefaultEventSystem`,但 ADR-013 明文要官方桥 ⇒ 本步把它立起来。
            _eventSystem = EnsureEventSystem();

            // fail-loud(AudioListener 单挂点 · AC-20-10):>1 即抛。
            // 把「运行期每帧刷 3 万条日志 →  Editor 卡死」的失效模式提前成具名异常。
            AssertSingleAudioListener();

            _booted = true;
        }

        /// <summary>
        /// 取 / 建拟物 UI 运行宿主(场景预摆优先,缺则自建)。
        /// <para>预摆 = Boot.unity 的 <c>SkeuoUI</c>(持 UIDocument + PanelSettings);
        /// 缺失 = 测试里没造 fixture ⇒ 自建(此时无 PanelSettings ⇒ 逻辑树在、不渲染,
        /// 见 <see cref="AssertSingleAudioListener"/> 旁注的实测记录)。</para>
        /// </summary>
        private SkeuoRuntimeDriver FindOrCreateSkeuoUi()
        {
            var existing = UnityEngine.Object.FindFirstObjectByType<SkeuoRuntimeDriver>();
            if (existing != null)
                return existing;

            var uiObj = new GameObject("SkeuoUI");
            UnityEngine.Debug.LogWarning(
                "[BootRoot] Boot.unity 未预摆 SkeuoUI(UIDocument + PanelSettings)—— " +
                "自建空壳 ⇒ UI 逻辑树在但不渲染(panel == null,2026-10-10 实测)。" +
                "生产场景应保留预摆;测试环境可忽略本警告。");
            return uiObj.AddComponent<SkeuoRuntimeDriver>();
        }

        /// <summary>
        /// 取 / 建官方 UI 输入桥(<c>EventSystem</c> + <c>InputSystemUIInputModule</c>)—— B-6-1。
        /// <para>预摆优先(测试 / 未来场景),缺则运行期自建(承 <see cref="FindOrCreateSkeuoUi"/> 先例)。
        /// 只挂官方桥,**禁 <c>StandaloneInputModule</c>**(ADR-013 §F4:双输入模块 = 真冲突)。</para>
        /// <para><b>fail-loud</b>(承 <see cref="AssertSingleAudioListener"/> 口径):动作资产缺失或
        /// 引用不全即抛 —— **不静默降级为「零 UI 输入」**(那正是 B-6-1 的失效形态)。</para>
        /// </summary>
        private EventSystem EnsureEventSystem()
        {
            // 含 inactive:预摆件可能被禁用,仍是「唯一合法挂载点」(承 AudioListener 断言的 IncludeInactive 纪律)。
            var existing = UnityEngine.Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include);
            if (existing != null)
            {
                var presetModule = existing.GetComponent<InputSystemUIInputModule>();
                if (presetModule == null)
                    throw new InvalidOperationException(
                        "[BootRoot][B-6-1] 场景内 EventSystem 未挂 InputSystemUIInputModule —— " +
                        "UI Toolkit 面板将收不到任何指针/导航事件(鼠标键盘全断)。" +
                        "ADR-013 §F4 禁 StandaloneInputModule 与官方桥并存/替代。");
                AssertUiActionsWired(presetModule, "预摆");
                return existing;
            }

            var go = new GameObject("EventSystem");
            // ⚠️ 必须归 BootRoot:new GameObject 落**当前活动场景**(启动序第 3 步 additive
            //    载入 World 后可能成为活动场景)⇒ 不归 Boot 则 Phase 2 卸载 World 会带走它;
            //    reparent 后随 BootRoot 一同销毁 ⇒ 免测试间泄漏(承 Player 的 MoveGameObjectToScene
            //    同纪律,此处用 SetParent 更简 —— 跨场景 reparent 自动随父进 Boot 场景)。
            go.transform.SetParent(transform, worldPositionStays: false);

            var es = go.AddComponent<EventSystem>();
            var module = go.AddComponent<InputSystemUIInputModule>();

            var actions = InputSystem.actions;
            if (actions == null)
                throw new InvalidOperationException(
                    "[BootRoot][B-6-1] InputSystem.actions 为 null —— 项目级动作资产未注册。" +
                    "检查 ProjectSettings/EditorBuildSettings.asset 的 com.unity.input.settings.actions" +
                    "(期望 guid 052faaac586de48259a63d0c4782560b)。UI 输入将不可用。");

            // ⚠️ **顺序敏感**:AddComponent 在 active GO 上会**立即**触发 OnEnable →
            //    HasNoActions()==true → AssignDefaultActions() 填好 8 个引用;此后本赋值才能经
            //    UpdateReferenceForNewAsset 按**同名**重指向到项目资产(它只重指向已有引用、
            //    **不填充空引用**:oldAction==null ⇒ return null)⇒ 若先赋值后 AddComponent(或
            //    inactive 上建),8 引用全 null = **静默零输入**。故下面必须断言,不得只断 actionsAsset。
            module.actionsAsset = actions;
            AssertUiActionsWired(module, "自建");
            return es;
        }

        /// <summary>UI 动作引用完整性(fail-loud):8 个引用缺任一 ⇒ UI 输入静默失效。</summary>
        private static void AssertUiActionsWired(InputSystemUIInputModule module, string origin)
        {
            var missing = new System.Collections.Generic.List<string>();
            if (module.point?.action == null)       missing.Add("Point");
            if (module.move?.action == null)        missing.Add("Navigate");
            if (module.submit?.action == null)      missing.Add("Submit");
            if (module.cancel?.action == null)      missing.Add("Cancel");
            if (module.leftClick?.action == null)   missing.Add("Click");
            if (module.middleClick?.action == null) missing.Add("MiddleClick");
            if (module.rightClick?.action == null)  missing.Add("RightClick");
            if (module.scrollWheel?.action == null) missing.Add("ScrollWheel");

            if (missing.Count > 0)
                throw new InvalidOperationException(
                    $"[BootRoot][B-6-1] {origin} InputSystemUIInputModule 动作引用不齐,缺:{string.Join(", ", missing)}。" +
                    "InputSystemUIInputModule.actionsAsset setter 只**重指向已有引用**、**不填充空引用**" +
                    "(UpdateReferenceForNewAsset: oldAction==null ⇒ return null)⇒ 引用为空 = 该动作永不派发。" +
                    "正确形态:自建时确保 AddComponent 先于 actionsAsset 赋值(OnEnable 的 AssignDefaultActions 打底);" +
                    "或预摆时在 Inspector 填好 8 引用(照 SpikeAssump6.unity)。");
        }

        /// <summary>
        /// 面板 <c>selectableGameObject</c> 就绪后把它选为当前 UI 对象(B-6-1 · 守「零初始选中」死锁)。
        /// <para><b>为何必需</b>:<c>InputSystemUIInputModule.ProcessNavigation</c> 的 move 与 submit
        /// **都 gate 于 <c>currentSelectedGameObject != null</c>**,而它只在元素**已获焦点**时由
        /// <c>PanelEventHandler.OnElementFocus</c> 设 ⇒ 无初始选中 = 键盘导航/提交**永不派发**。
        /// 面板载体 GO 由 interop bridge **运行期**新建(Boot 时不存在)⇒ <c>firstSelectedGameObject</c>
        /// 无法预摆,只能在面板出现后 bootstrap 选中。</para>
        /// <para>⚠️ **不违反 AC-42-C10**(42 不自设初始焦点):本步选的是**面板载体**,
        /// <c>PanelEventHandler.OnSelect</c> 只做面板级 <c>panel.Focus()</c>(<c>focusedElement</c> 仍为 null),
        /// 首次 <c>NavigationMoveEvent</c> 才落焦到具体控件。</para>
        /// <para><b>鼠标路径不依赖本步</b>:指针事件经 <c>PanelRaycaster</c> 直接命中,不经此门
        /// ⇒ 即便本步未及时执行,鼠标点击仍可用。</para>
        /// </summary>
        private void BootstrapEventSystemSelection()
        {
            if (_eventSystem == null || _uiDriver == null)
                return;

            var root = _uiDriver.GetUiRoot();
            // ⚠️ 必须转 **`IRuntimePanel`**(public):`VisualElement.panel` 声明为 `IPanel`,
            //    而 `selectableGameObject` 只在 `IRuntimePanel`(public 接口,运行期面板实现它)
            //    与 `BaseRuntimePanel`(**internal**,够不着)上 —— 用 `BaseRuntimePanel`
            //    会 CS0122(inaccessible due to its protection level,2026-10-13 实测编译红)。
            //    面板未 attach(自建空壳 / 未渲染)⇒ 转型失败 ⇒ 跳过。
            if (!(root?.panel is IRuntimePanel panel))
                return;

            var selectable = panel.selectableGameObject;
            if (selectable == null)
                return;   // bridge 尚未建面板 GO(下一帧 Update 才建)⇒ 下次调用再试

            if (_eventSystem.currentSelectedGameObject != selectable)
                _eventSystem.SetSelectedGameObject(selectable);
        }


        /// <summary>
        /// AudioListener 单挂点断言(ADR-020 §七 / AC-20-10)—— **fail-loud**。
        /// <para><b>为何必须是断言而不是依赖 Unity 的警告</b>:Unity 对多 listener
        /// 只发一条**每帧重复**的警告(不抛、不阻断)。2026-10-10 实测:该警告刷满
        /// Editor.log 31868 条,Console 管道超载后 Editor 进程以 <c>write EPIPE</c>
        /// 异常终止 —— 人工表现为「无法退出 Play / 点关闭无响应」。
        /// 一条不可抛的警告足以杀死进程 ⇒ 必须由我方在启动序内把它变成具名异常。</para>
        /// <para><b>为何是启动序末而不是 Awake</b>:本章第 5 步才装配相机,
        /// Awake 时场景内的 listener 集尚未反映运行期装配结果。</para>
        /// </summary>
        /// <exception cref="InvalidOperationException">场景内 AudioListener ≠ 1 个。
        /// 含 0 个(无听音点)与 ≥2 个(多挂点)—— 两者都是配置错误。</exception>
        /// <summary>装配 44 侧设置注册表的运行期供给(S14-6 · 兑清 story-011 §登记项)。
        /// <para>预载成功 ⇒ 烘焙分区(AC-44-13 真源)成为 <see cref="AudioSettingsRegistry.Rows"/> 的供给;
        /// 预载失败 / provider 为 null ⇒ <b>不注入</b>,`Rows` 走 `FallbackRows`(与烘焙分区同值,
        /// 由 `AudioSettingsRegistryBinder` 的构建期双向值对账钉住)。</para>
        /// <para><b>为何是静态可直驱的方法而不是内联在 <see cref="Start"/> 里</b>:内联后本接线
        /// 只能经完整启动序(Addressables catalog + 场景)才可达 ⇒ EditMode 无从断言,
        /// 「接线是否存在」退化为不可证伪的口头承诺 —— 正是本 sprint 要根治的假绿形态。
        /// 抽成静态后由 <c>pipeline_gate_test.test_bootroot_*</c> 直驱。</para>
        /// <para>⚠️ <b>第二个直接调用点(2026-10-17)</b>:<c>Tests/PlayMode/precheck_allKeys_test.cs</c>
        /// 用它验证「<paramref name="preloaded"/> = true 但 <paramref name="provider"/> = null ⇒ 不注入」——
        /// 这是把 <c>Start</c> 里抛形属性求值摘出接线后、钉住「null 仍不注入」这一半的唯一载体。</para>
        /// <para>⚠️ 这里**刻意不抛**:设置值不是 sim 真值(端本地设备偏好,OQ-SS-5=甲),
        /// 「回退集」是设计内的降级面,不是静默失败 —— 与 E-13 对**数据产物**的硬失败口径不冲突
        /// (那条管「产物不可读」,此处是「未装配」)。</para></summary>
        /// <param name="preloaded">data-core 预载是否成功。</param>
        /// <param name="provider">预载成功时的 provider(null ⇒ 不注入)。</param>
        public static void ApplyAudioSettingsProvider(bool preloaded, IDataProvider provider)
        {
            if (!preloaded || provider == null) return;
            AudioSettingsRegistry.Provider =
                () => provider.Load<AudioSettingsDataSet>().Rows;
        }

        /// <summary>遥测 F4–F7 六旋钮的**取值缝**(TD-epic-01 第十二片)。
        /// <para>provider 非空 ⇒ 取烘焙表(<c>telemetry_analytics.cooked.bytes</c>,真源,加载期已过范围门);
        /// provider 为 null(预载失败)⇒ **返回 null**,由 <see cref="TelemetryReporter"/> 维持「未给」
        /// 形态 —— <b>不</b>造第二份常量兜底(与第十一片的设备旋钮刻意不同:GDD「数值归用户」,
        /// 51 不替用户转旋钮;凭空兜底默认值 = 没告诉任何人就改了口径)。
        /// W &lt; 1 / S &lt; 1 在<b>装载期</b>已被 <c>TelemetryAnalyticsCookedCodec</c> 拒收 ⇒
        /// 此处拿到的值必然合法;到达本处的越界值 ⇒ startup hard failure 已先发生。</para></summary>
        /// <param name="provider">data-core provider(null ⇒ null)。</param>
        public static TelemetryAnalyticsTuning? ResolveTelemetryKnobs(IDataProvider provider)
        {
            return provider?.Load<TelemetryAnalyticsDataSet>().Tuning;
        }

        /// <summary>设备切换迟滞六旋钮的**取值缝**(TD-epic-01 第十一片)。
        /// <para>预载成功 ⇒ 取烘焙表(<c>input_device_switch.cooked.bytes</c>,真源);
        /// 预载失败 / provider 为 null ⇒ 取 <see cref="CompositionRoot.EmergencyDeviceSwitchSeed*"/>
        /// 常量兜底(设计内降级面,与 <see cref="ApplyAudioSettingsProvider"/> 同款)。</para>
        /// <para><b>为何是静态可直驱的方法而不是内联在 <see cref="Start"/> 里</b>:承
        /// <see cref="ApplyAudioSettingsProvider"/> 同款理由 —— 内联后本接线只能经完整启动序
        /// (Addressables catalog + 场景)才可达 ⇒ EditMode 无从断言。</para>
        /// <para>返回序 = <c>DeviceStateManager</c> 构造序(轴列三 + 指针列三)。</para></summary>
        /// <param name="provider">data-core provider(null ⇒ 常量兜底)。</param>
        /// <returns>(axisThreshold, axisTolerance, axisDwell, pointerThreshold, pointerTolerance, pointerDwell)。</returns>
        public static (int axisThreshold, int axisTolerance, int axisDwell,
                       int pointerThreshold, int pointerTolerance, int pointerDwell)
            ResolveDeviceSwitchKnobs(IDataProvider provider)
        {
            if (provider != null)
            {
                var data = provider.Load<DeviceSwitchTuningDataSet>();
                // ⚠️ Fix.Raw 是 long,而 DeviceStateManager 收 Q16.16 **int**(Gameplay.Input
                //    不引 Sim.Contracts —— 见 DeviceSwitchTuning.cs 头注的落点注记)。
                //    窄化是**契约面**而非顺手转换:装载期 InputDeviceSwitchBinder 已断区间,
                //    越界值到不了这里;真越界 ⇒ 窄化抛 OverflowException(启动期硬失败,零静默截断)。
                return (checked((int)data.Tuning.Axis.DeviceSwitchThreshold.Raw),
                        checked((int)data.Tuning.Axis.DriftTolerance.Raw),
                        data.Tuning.Axis.DwellTicks,
                        checked((int)data.Tuning.Pointer.DeviceSwitchThreshold.Raw),
                        checked((int)data.Tuning.Pointer.DriftTolerance.Raw),
                        data.Tuning.Pointer.DwellTicks);
            }

            return (CompositionRoot.EmergencyDeviceSwitchSeedAxisThreshold,
                    CompositionRoot.EmergencyDeviceSwitchSeedAxisTolerance,
                    CompositionRoot.EmergencyDeviceSwitchSeedAxisDwell,
                    CompositionRoot.EmergencyDeviceSwitchSeedPointerThreshold,
                    CompositionRoot.EmergencyDeviceSwitchSeedPointerTolerance,
                    CompositionRoot.EmergencyDeviceSwitchSeedPointerDwell);
        }

        /// <summary>装配 44 运行期音频链的启动序半边(story-018 · A-1/A-2/A-4/A-5/A-7)。
        /// <para>职责五件:① 从 provider 取音频事件表;② 建 <see cref="AudioClipLibrary"/> 并
        /// 逐行预载 clip(A-2);③ 解析混音器 —— **解析不到即 fail-loud**(A-7);④ 建
        /// <see cref="AudioBusResolver"/>(A-6 的运行期半边);⑤ 赋值
        /// <see cref="PlayerBusVolumeInitializer.MixerResolver"/> 并**显式**应用
        /// <c>FactoryDefaults</c>(A-5)。</para>
        /// <para><b>为何是静态可直驱的方法而不是内联在 <see cref="Start"/> 里</b>:承
        /// <see cref="ApplyAudioSettingsProvider"/> 同款理由 —— 内联后本接线只能经完整启动序
        /// (Addressables catalog + 场景)才可达 ⇒ EditMode 无从断言,「接线是否存在」退化为
        /// 不可证伪的口头承诺。抽成静态后由 <c>runtime_assembly_test</c> 直驱(注入
        /// <paramref name="mixerLoader"/> 假件即可覆盖 fail-loud 分支,零 catalog 依赖)。</para>
        /// <para><b>为何 <paramref name="provider"/> 为 null 是「返回 null」而非抛</b>:
        /// 预载失败 = 本机未重建 catalog / 测试环境 ⇒ 组合根走 <c>NullRenderTarget</c> 链,
        /// <c>AudioRenderAssembled = false</c> **可观测**(AC-018-3 ②「不静默」)。而
        /// **混音器**解析失败不是环境问题、是 A-7 未补(组未登记)⇒ 那一处必须抛。</para></summary>
        /// <param name="provider">data-core provider(null ⇒ 缝不接,返回 null)。</param>
        /// <param name="mixerLoader">混音器装载缝(null ⇒ 走生产路径
        /// <see cref="AudioClipLibrary.LoadMixer"/>)。测试注入假件以覆盖 fail-loud 分支。</param>
        /// <param name="clipLoader">clip 装载缝(null ⇒ 走生产路径 Addressables)。测试注入假件以
        /// **观测释放**:`TD-audio-031` ② 的判据是「抛之前已预载的 clip 被 Dispose」,而
        /// EditMode 无 catalog ⇒ 生产路径下 `_handles` 恒空、`Dispose` 是空操作、**零判别力**。
        /// 经缝装来的 clip 由缝方持有,释放走 <see cref="AudioClipLibrary.Preload"/> 的
        /// <c>internalRelease</c> 通道。</param>
        /// <param name="clipRelease">释放缝方 clip 的回调(null ⇒ 交由缝方自行释放)。</param>
        /// <param name="busResolverFactory">总线解析缝(null ⇒ 生产路径 `new AudioBusResolver(mixer)`)。
        /// <para>⚠️ **为什么需要这条缝**(`TD-audio-031` ②,2026-10-29):该子项的判据是
        /// 「抛之前已预载的 clip 被 Dispose」,而唯一生产可达的抛点是 `AudioBusResolver`
        /// 的**缺组**分支。EditMode 下**造不出缺组混音器** —— 实测 `AudioMixer` 不是
        /// `ScriptableObject`(不能 `CreateInstance`)、`AudioMixerController` 不在公共 API
        /// (改组必须动盘上的作者态资产,不可接受)。⇒ 只有经缝注入一个必抛的解析器,
        /// 才能让那条 catch 真的被跑到(否则该分支只有纸面存在,零判别力)。</para></param>
        /// <returns>装配缝(provider 为 null 时返回 null)。</returns>
        /// <exception cref="InvalidOperationException">混音器解析不到(A-7)· 玩家音量默认集
        /// 键覆盖校验失败(A-5)。两者都是装配错,不是环境降级面。</exception>
        public static AudioAssemblySeams BuildAudioSeams(IDataProvider provider,
                                                         Func<AudioMixer> mixerLoader = null,
                                                         Func<string, AudioClip> clipLoader = null,
                                                         Action<AudioClip> clipRelease = null,
                                                         Func<AudioMixer, AudioBusResolver> busResolverFactory = null)
        {
            if (provider == null) return null;   // 缝不接:NullRenderTarget 链,可观测

            AudioEventDataSet table = provider.Load<AudioEventDataSet>();
            var clips = new AudioClipLibrary(table);

            // ③ 混音器解析(A-7)—— **先于 clip 预载**:fail-loud 分支不该先走一遍 catalog。
            AudioMixer mixer = ResolveMixerOrFail(clips, mixerLoader);

            // ② clip 预载(A-2)—— 逐行按事件表 assets[] 的 bare 名装载。
            // ⚠️ 缺条目**不抛**(承 AudioClipLibrary 头注:素材存在性已由构建期 D6 门拦下,
            //    运行期 miss = 装配错,记数可观测);是否真齐由 PlayMode 断言 MissingCues 为空。
            clips.Preload(loader: clipLoader, internalRelease: clipRelease);

            // ④⑤ 总线解析 + 玩家音量 —— 失败即释放已预载 clip(`TD-audio-031` ②)。
            var buses = BuildBusFaceOrRelease(mixer, clips, busResolverFactory,
                                              out IReadOnlyList<string> errors);

            return new AudioAssemblySeams
            {
                Clips = clips,
                Buses = buses,
                BusVolumeErrors = errors,
                Table = table,
            };
        }

        /// <summary>④ 总线解析 + ⑤ 玩家总线音量;任一步抛 ⇒ **先释放已预载的 clip** 再重抛。
        /// <para>🔁 2026-10-29(`TD-audio-031` ②,新立):`Preload` 成功之后,`AudioBusResolver`
        /// 的构造(缺组)与 `ApplyDefaults`(errors)都可能抛 —— 而抛出去之后**没有任何人持有
        /// <paramref name="clips"/>** ⇒ ≤15 个 Addressables 句柄随异常一起悬挂(下次 Play 前
        /// 不会有人 Release)。</para>
        /// <para>⚠️ 用 try/catch 而不是「先判断再抛」:两处抛点的判据都由**被调方**给出,
        /// 在调用方复算一遍 = 第二真源。catch → Dispose → 重抛,异常类型与报文原样保留。</para></summary>
        /// <param name="mixer">已解析的混音器。</param>
        /// <param name="clips">已预载的 clip 库(抛路径上由本方法负责释放)。</param>
        /// <param name="busResolverFactory">总线解析缝(null ⇒ 生产路径)。</param>
        /// <param name="errors">出口:A-5 的错误列表(空 = 全部成功)。</param>
        /// <returns>已解析的总线面。</returns>
        /// <exception cref="InvalidOperationException">缺组(A-6)或音量默认集失败(A-5)。</exception>
        private static AudioBusResolver BuildBusFaceOrRelease(AudioMixer mixer, AudioClipLibrary clips,
                                                             Func<AudioMixer, AudioBusResolver> busResolverFactory,
                                                             out IReadOnlyList<string> errors)
        {
            try
            {
                // ④ 总线解析器(A-6 运行期半边):七总线名 → AudioMixerGroup。
                var buses = busResolverFactory != null
                    ? busResolverFactory(mixer)
                    : new AudioBusResolver(mixer);

                // ⑤ 玩家总线音量(A-5)。
                errors = ApplyPlayerBusDefaultsOrFail(mixer);
                return buses;
            }
            catch
            {
                clips.Dispose();   // 幂等(`_disposed` 守卫);重抛保留原异常类型与报文
                throw;
            }
        }

        /// <summary>③ 混音器解析(A-7)。解析不到 = **装配错**,不是环境降级面 ⇒ 抛,不得静默跳过。
        /// <para>🔁 2026-10-29(`TD-audio-032` ③ 收尾)从 `BuildAudioSeams` 抽出 —— 原文案与抛点一字未改。</para></summary>
        /// <param name="clips">clip 库(生产路径用它装载混音器)。</param>
        /// <param name="mixerLoader">测试注入缝(null ⇒ 走生产路径)。</param>
        /// <returns>已解析的混音器(非 null)。</returns>
        /// <exception cref="InvalidOperationException">解析不到。</exception>
        private static AudioMixer ResolveMixerOrFail(AudioClipLibrary clips, Func<AudioMixer> mixerLoader)
        {
            AudioMixer mixer = mixerLoader != null ? mixerLoader() : clips.LoadMixer();
            if (mixer == null)
            {
                throw new InvalidOperationException(
                    "[BootRoot][story-018][A-7] 混音器解析不到(address = " +
                    AudioClipLibrary.AudioMixerAddress + ")—— Addressables 组未登记或 catalog 未重建。" +
                    "修法:跑菜单「大医精诚/数据管线/确保 audio Addressables 组」再「构建 Addressables 内容」。" +
                    "⚠️ 不得静默跳过:混音器缺失 ⇒ 七总线拓扑与音量滑块全失效(A-5/A-6)。");
            }
            return mixer;
        }

        /// <summary>⑤ 玩家总线音量默认集(A-5),失败即抛(半初始化已由初始化器自行回退)。
        /// <para>⚠️ `Bootstrap()` 已在本场景加载前跑过(那时 resolver 还是 null)⇒ 此处赋值后
        /// 必须**显式** `ApplyDefaults`,不能等自动入口。</para>
        /// <para>🔁 2026-10-29(`TD-audio-032` ③ 收尾)从 `BuildAudioSeams` 抽出 —— 原文案与抛点一字未改。</para></summary>
        /// <param name="mixer">已解析的混音器。</param>
        /// <returns>错误列表(空 = 全部成功)。</returns>
        /// <exception cref="InvalidOperationException">存在失败项。</exception>
        private static IReadOnlyList<string> ApplyPlayerBusDefaultsOrFail(AudioMixer mixer)
        {
            PlayerBusVolumeInitializer.MixerResolver = () => mixer;
            var errors = PlayerBusVolumeInitializer.ApplyDefaults(
                mixer, PlayerBusVolumeInitializer.FactoryDefaults);
            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    "[BootRoot][story-018][A-5] 玩家总线音量初始化失败(半初始化已回退,零 SetFloat):\n" +
                    string.Join("\n", errors));
            }
            return errors;
        }

        /// <summary>
        /// 按名字查相机,**含 inactive**(<see cref="GameObject.Find"/> 只找 active)。
        /// <para>预摆 <c>Main Camera</c> 在启动序里会被 <c>SetActive(false)</c>;若某条路径
        /// 让它带着 listener 留成 inactive 形态,再用 active-only 的 Find 去摘就会**整个跳过**。</para>
        /// </summary>
        private static GameObject FindInactiveInclusive(string objectName)
        {
            var cameras = UnityEngine.Object.FindObjectsByType<UnityEngine.Camera>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var cam in cameras)
            {
                if (cam != null && cam.gameObject.name == objectName)
                    return cam.gameObject;
            }
            return null;
        }

        private static void AssertSingleAudioListener()
        {
            // **双判据,缺一不可**(2026-10-20 修):
            //  ① active = 1 —— Unity 是否认这个听音点。0 个 ⇒ 全场景静音,每帧刷
            //     `no audio listeners in the scene`(2026-10-20 实测 10845 条,Play 首帧起)。
            //  ② 挂载总数 = 1 —— 无残留挂载。≥2 ⇒ 每帧警告洪泛 → Console 管道超载 →
            //     Editor 进程 EPIPE 终止(2026-10-10 实测 31868 条)。
            // ⚠️ **旧实现只查 `Resources.FindObjectsOfTypeAll`(含 inactive)的总数 = 1**
            //    ⇒ 把「预摆已禁用 + CameraRig 未挂」判成正确形态,**永远抓不到零 active**。
            //    旧注释「用 active-only 得 0 ⇒ 断言把正确形态判成错误」正是把缺陷写成了规格。
            var active = UnityEngine.Object.FindObjectsByType<UnityEngine.AudioListener>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            var all = UnityEngine.Object.FindObjectsByType<UnityEngine.AudioListener>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (active.Length != 1 || all.Length != 1)
            {
                throw new InvalidOperationException(
                    $"AC-20-10 AudioListener 单挂点:active {active.Length} 个 / 挂载总数 {all.Length} 个(应各为 1)。" +
                    "0 个 active = 无听音点 ⇒ 整个场景静音(2026-10-20 实测 10845 条警告);" +
                    "≥2 个 = 每帧警告洪泛 → Console 管道超载 → Editor 进程 EPIPE 终止(2026-10-10 实测 31868 条)。" +
                    "正确形态:运行时 CameraRig 主相机持唯一 listener;预摆 Main Camera 禁用前已摘除其 listener。");
            }
        }

        /// <summary>
        /// 生产读档触发点(TD-persist-04 义务② · Load 接线轮 2026-10-25 · 裁定②
        /// 「公开方法·通电待信」)—— 转发给 <see cref="LoadSequencer.Load"/>(调用序、
        /// 原子序与派生态未重建清单全在 <see cref="LoadSequencer"/> 类头注)。
        /// <para><b>通电待信</b>:本方法今日零调用方(UI 侧 SaveSlotItem 是纯视觉元件,
        /// 存档位界面尚未接线)—— 存档位 UI 接线轮落地即激活,零代码改动。</para>
        /// </summary>
        /// <param name="slot">读档槽位。</param>
        /// <exception cref="InvalidOperationException">装配未含读档序列器
        /// (saveBasePath 为 null 的部分装配 = 编程错误;显式请求被静默吞 = 更糟,故 fail-loud)。</exception>
        public void RequestLoad(Sim.Codec.SaveSlot slot)
        {
            var sequencer = _services?.LoadSequencer;
            if (sequencer == null)
                throw new InvalidOperationException(
                    "装配未含读档序列器(saveBasePath 为 null ⇒ 三件存档侧组件同 null);" +
                    "生产装配必须走完整 BootRoot 启动序,RequestLoad 不接受部分装配");
            sequencer.Load(slot);
        }

        /// <summary>
        /// 每帧帧泵(薄壳):读移动输入 → <see cref="MovementFeed.PumpFrame"/>(喂入 / 采样 /
        /// tick 推进 / 边沿提交的次序全在那)。
        /// <para>接线面 = 两个订阅者:① PlayerController(其公开每 tick 入口 = <c>OnTickEdge</c>);
        /// ② <c>DiseaseVitalsService.OnTickEdge</c>(批次 C · 体征链核心,2026-10-09 接入)——
        /// 每个 tick 边沿在 ① **之后**驱动 ②,语义 = <b>体征在 tick 边沿后可见</b>,
        /// 且本边沿写入的病史事件在**本 tick** 内进求值(次序与 tick 回推的可测身全在
        /// <see cref="MovementFeed.PumpFrame"/>,本类只做转发)。仍待 Phase 2:全局 sim Step(非疾病侧)。</para>
        /// <para><b>2026-10-09 采样缺口已闭</b>:原登记「<c>OnPositionSample</c> / <c>OnUplinkSample</c>
        /// 零生产调用点 ⇒ <c>ActorCellEntered</c> 运行期恒不发事件」—— 现由
        /// <see cref="MovementFeed.ProcessMovementFrame"/> 提供唯一生产调用点
        /// (Move 后立刻采样)。<c>OnUplinkSample</c> 仍零调用点是**设计**:它是 Client 模式的
        /// 上行缝,联机(P1b · 45)才接,单机 Host 模式永不走它。</para>
        /// <para>急救动作不在此读(ADR-011 §二 独立直读通道);本文件只接普通移动。</para>
        /// </summary>
        private void Update()
        {
            if (!_booted || _tickDriver == null || _player == null) return;

            // 先跑 tick 泵(Advance + 逐边沿),再按推进后的 CurrentTick 驱动病人出现 ——
            // 次序不可反:驱动读的是**本帧推进后**的 tick;若在 PumpFrame 之前调,
            // CurrentTick 恒 0(首帧未 Advance)⇒ 驱动把 _lastSpawnTick 钉在 0,
            // 之后每个新 tick 都满足 `tick - 0 < 间隔` ⇒ 永不 spawn(2026-10-10 实测红)。
            // 帧节奏注入(sprint-05 T1.1):captureDeltaTime 只影响 Time.deltaTime,
            // **不传导到 unscaledDeltaTime**(2026-10-10 实测:tick Δ=0 恒不推进)。
            // 而 tick 泵吃的是 unscaledDeltaTime ⇒ 需要时显式注入固定步长,令
            // 「batch 快帧」与「编辑器 16.7ms 真帧」在 tick 语义上等价。
            // ⚠️ 只在 capture 激活时注入(生产路径永远走真实墙钟,不受影响)。
            double delta = Time.unscaledDeltaTime;
            if (Time.captureDeltaTime > 0d)
                delta = Time.captureDeltaTime;

            long tickBefore = _tickDriver.CurrentTick;
            MovementFeed.PumpFrame(_player, _tickDriver,
                                   MovementInputReader.ReadMoveAxis(),
                                   delta,
                                   StepVitals);

            // chunk 驻留驱动(ADR-023 ⑥ · 系统 6):读**最后一条已进流的 `ActorCellEntered`** 的格,
            // 纯函数重推激活集。⚠️ 刻意读 `LastCommittedCell` 而不是 `transform.position` ——
            // 后者是表现态,用它驱动会让驻留序列不可重放(ADR-016 §三)。
            // ⚠️ 放在 `PumpFrame` 之后:本帧提交发生在帧内 tick 边沿,出来后读到的已是最新格。
            if (_services != null && _services.World != null && _player != null)
                _services.World.OnActorCellEntered(_player.LastCommittedCell);

            // 病人出现驱动:仅在**本帧真推进了 tick** 时驱动(未推进 = 无 tick 边沿)
            if (_tickDriver.CurrentTick > tickBefore
                && _services != null && _services.PatientAppearedDriver != null)
            {
                _services.PatientAppearedDriver.OnTickEdge(_tickDriver.CurrentTick);
            }

            // 病例开账驱动(sprint-05 T1.2):在同一 tick 边沿序列内,病人出现驱动**之后**调用
            // 次序 = 病人先出现(DiseaseOnset 入流)→ 再开案(CaseOpened 入流)
            if (_tickDriver.CurrentTick > tickBefore
                && _services != null && _services.CaseOpenedDriver != null)
            {
                _services.CaseOpenedDriver.OnTickEdge(_tickDriver.CurrentTick);
            }

            // 急救链接线驱动(sprint-05 T1.3):在同一 tick 边沿序列内,开案驱动**之后**调用
            // 次序 = 病人先出现 → 再开案 → 最后触发急救(EmergencyAttempt + EmergencyTreatmentApplied 入流)
            if (_tickDriver.CurrentTick > tickBefore
                && _services != null && _services.EmergencyAttemptDriver != null)
            {
                _services.EmergencyAttemptDriver.OnTickEdge(_tickDriver.CurrentTick);
            }

            // 急救直读通道中止判据(story-009 · 规则六之甲):同一 tick 边沿序列内检查
            // Armed 闲置 —— 按住语义下即「按住超阈值 ⇒ 中止,零事件」(R-9.3)。
            if (_tickDriver.CurrentTick > tickBefore && _services != null)
            {
                _services.EmergencyChannel?.OnTickEdge(_tickDriver.CurrentTick);
            }

            // 52 出诊启动驱动(TD-randomevents-02 接线轮 · 2026-10-25 三裁全取推荐):
            // 同一 tick 边沿序列内,急救通道检查**之后**、定时兜底**之前** ——
            // 出诊启动锚点(HouseCallStarted)须在本边沿先落流,末段锚点触发器的
            // Drain 才能在同一边沿扫到(插在 Drain 之后 = 锚点观察晚一拍,缝)。
            // ⚠️ 生产「通电待信」态:出诊类 event_key 集与医馆地块集均为空(三裁②③)
            //    ⇒ 写入恒零;两处数据回填(37 key 集 / 23-24 地块)后自动激活。
            if (_tickDriver.CurrentTick > tickBefore && _services != null)
            {
                _services.RandomEventsDirector?.OnTickEdge(_tickDriver.CurrentTick);
            }

            // 定时兜底 checkpoint(AC-7a-20 定时半边 · TD-persist-05 动作④ · 2026-10-25 生产接线轮):
            // 同一 tick 边沿序列内,急救通道检查**之后**(兜底判定读的「未决医疗动作」Armed 态
            // 须是本 tick 终态;halt 期无 Advance ⇒ 无边沿 ⇒ 不驱动,承 S6 停机时钟语义)。
            // ⚠️ 按 **tick 数**驱动(`OnTicks(Δtick)`),不按帧 —— 一帧推进 N tick 时
            //    按帧驱动会把 N 个 tick 计成 1,interval(以 tick 计)被帧率稀释。
            //    闭包内异常已在装配根兜住(LogError,不杀帧)。
            if (_tickDriver.CurrentTick > tickBefore && _services != null)
            {
                _services.CheckpointFallback?.OnTicks(_tickDriver.CurrentTick - tickBefore);
            }

            // 事件触发 checkpoint(AC-7a-20 事件触发半边 · TD-persist-04 动作① · 2026-10-25 裁定轮):
            // 集中观察(裁定①)—— 本 tick 落流事件交触发器扫白名单,命中且瞬时源安全 ⇒
            // 调同一带门 Checkpoint 入口;成功 ⇒ 兜底计数清零(裁定④)。**在兜底之后**:
            // 兜底本轮若已触发,本 drain 不会重复触发(游标语义 + 同 tick 一次);halt 期
            // 无 Advance ⇒ 无新事件,不驱动(与兜底同款停机时钟语义)。读档换流
            // (`EventStream.Restore`)后游标由 Drain 的钳制对齐 —— 生产 Load 接线落地时
            // 须另调 `ResetCursor()`(证据件缺口登记)。
            if (_tickDriver.CurrentTick > tickBefore && _services != null)
            {
                _services.AnchorCheckpointTrigger?.Drain(_services.Stream.Events);
            }

            // 病人视觉实体(sprint-05 T2.0):急救驱动**之后**调用 —— 视觉跟着在场集走,
            // 与写者次序解耦(视觉是派生态,不参与事件入流次序)
            if (_tickDriver.CurrentTick > tickBefore
                && _services != null && _services.PatientVisualSpawner != null)
            {
                _services.PatientVisualSpawner.OnTickEdge(_tickDriver.CurrentTick);
            }

            // 一次性 cue 的归还泵(S15-5 · TD-audio-029):问 `isPlaying`,播完即把源还回池。
            // ⚠️ **挂在 tick 边沿上**(20 Hz),不是每帧 —— 与本文件其余驱动同一节奏,
            //    每帧一次会多出 2 倍的无收益遍历(素材最短也有数百毫秒)。
            //    挂在 `PatientCueDriver` **之前**:本拍新发的一次性 cue 不会在本拍被观测(它刚开播)。
            if (_tickDriver.CurrentTick > tickBefore && _services != null && _services.AudioPool != null)
                OneShotReclaimPump.Pump(_services.AudioPool);

            // 病人 cue 驱动(story-018 · AC-018-6):在场集 → 13 调度 → 44 发射。
            // 次序 = 视觉实体**之后**、玩家交互输入**之前**:两者都读本 tick 的在场集,
            // 但 cue 还要读体征 —— `StepVitals` 已在 `PumpFrame` 的边沿序列里跑完
            // (故新出现的病人在**本拍**已建档,`hasCourse` 门不会误跳)。
            // ⚠️ `PruneAbsent` 先于 `OnTickEdge`:离场病人的呼吸层句柄须先收,
            //    否则句柄残留在驱动内、`ActiveBreathCount` 永远不归零。
            if (_tickDriver.CurrentTick > tickBefore
                && _services != null && _services.PatientCueDriver != null)
            {
                _services.PatientCueDriver.PruneAbsent(_tickDriver.CurrentTick);
                _services.PatientCueDriver.OnTickEdge(_tickDriver.CurrentTick);
            }

            // 玩家交互输入(sprint-07 S2):E 查体 / F 急救 —— **视觉实体更新之后**(读的是
            // 本 tick 的在场集+视觉位置;换热须已本 tick Step),拟物 UI 刷新之前
            // (查体后本帧即见值区有词)。
            _interactionDriver?.OnFrame();

            // 建造输入(S20-19 · 2026-10-28):B 开关 / Enter 放置 / R 旋转 / Delete 拆除。
            // ⚠️ 紧跟交互驱动:两者都读设备按下沿,互不依赖;建造在**非建造模式**下是纯 no-op。
            _buildDriver?.OnFrame();

            // 听诊精度档位(B 轮 2026-10-20):**按帧**推 ramp(档变才 SetTargets;未接 table ⇒ null)。
            // ⚠️ 不挂 tick 边沿 —— ramp 是逐帧平滑(GDD F-44.1 注 ≥ 50 ms),tick 边沿 20 Hz 会跳。
            _services?.AudioTierDriver?.Tick(UnityEngine.Time.deltaTime);

            // 拟物 UI 值区(sprint-05 T2.1/T2.3):视觉实体**之后**调用 —— UI 读的是
            // **本 tick 已推进完**的体征(Driver.Bind 的源零缓存,每次读都过体征链)。
            // 次序不可反:若在体征 Step 前刷新,同一 tick 里 UI 显示的是上一 tick 的读数。
            if (_tickDriver.CurrentTick > tickBefore && _uiDriver != null)
            {
                _uiDriver.OnTickEdge();
            }

            // UI 输入桥初始选中(B-6-1):面板 selectableGameObject 由 interop bridge 在
            // 面板 attach 后的某帧新建 ⇒ 每帧幂等尝试,直到选中成功(成功即 no-op)。
            // ⚠️ 必须在面板就绪后 —— 故放 Update 而非启动序(启动序时面板 GO 可能未建)。
            BootstrapEventSystemSelection();

            // 相机跟随(sprint-05 T1.5):每帧把相机位置设到玩家位置 + 偏移
            // ADR-020:相机 = 自建机位,不引入 Cinemachine
            if (_cameraRig != null && _player != null)
            {
                var cam = _cameraRig.Camera;
                if (cam != null)
                {
                    Vector3 target = _player.transform.position;
                    cam.transform.position = _cameraRig.GetCameraPosition(target);
                    cam.transform.LookAt(target);
                }
            }
        }

        /// <summary>
        /// tick 边沿第二驱动口(批次 C):体征链 Step —— 在同边沿 <c>PlayerController.OnTickEdge</c>
        /// **之后**调用(见 <see cref="MovementFeed.PumpFrame"/> 次序头注)。
        /// </summary>
        /// <remarks>薄转发:本类不持游戏状态(ADR-013 §9 C3),病程真源在事件流。</remarks>
        /// <param name="tick">该边沿自己的逻辑 tick(由帧泵回推,非末 tick)。</param>
        private void StepVitals(long tick)
        {
            if (_services == null || _services.VitalsService == null) return;
            _services.VitalsService.OnTickEdge(tick);
        }

        /// <summary>
        /// 退出清理:Phase 1 **不做存档、不 flush、不卸载场景**(ADR-023 §⑤ 拆序六步与
        /// ADR-010 §六 定期 checkpoint 归 Phase 2)。
        /// </summary>
        private void OnDestroy()
        {
            // 病人视觉实体清理(sprint-05 T2.0):视觉根节点由 PatientVisualSpawner 自持,
            // 不靠层级继承(Boot 场景常驻,看不见 World 卸载)⇒ 显式调用销毁面。
            _services?.PatientVisualSpawner?.DestroyAll();

            // 急救直读通道卸挂(story-009):onAfterUpdate 是全局静态回调,必须在
            // BootRoot 销毁时显式 Detach,否则域内悬挂委托引用已弃通道(下次 Play 泄漏)。
            _services?.EmergencyChannel?.Detach();

            // 设备态卸挂(TD-epic-01 第八片):onDeviceChange 同属全局静态回调,须显式摘。
            _deviceState?.Detach();

            // 退出钩子卸挂(AC-7a-17):`Application.wantsToQuit` 是**静态**事件,
            // 不摘会在下次 Play 里重复触发已弃的 SaveService(域内悬挂委托)。
            // ⚠️ 用**缓存字段**注销,不能用 lambda 重新 `-=`(每次求值都是新对象)。
            if (_wantsToQuitHandler != null)
            {
                Application.wantsToQuit -= _wantsToQuitHandler;
                _wantsToQuitHandler = null;
            }

            // 病人 cue 驱动收尾(story-018 · ADR-018 §一「句柄由唯一持有者负责收尾」)——
            // 本驱动即呼吸层句柄的持有者;不显式 EndLoop ⇒ 句柄跨 Play 轮残留。
            _services?.PatientCueDriver?.StopAll();

            // 44 装配缝释放(story-018):池根节点 + clip / mixer 的 Addressables 句柄。
            // ⚠️ 池根是**非 Boot 场景**内容(ADR-028 ② 零场景预摆:池根由 `new GameObject` 建,
            //    不挂 BootRoot 层级)⇒ 不靠层级继承销毁,必须显式 Dispose。
            // ⚠️ **不调 `Spatialization?.Dispose()`** —— 导演与渲染目标**共享**同一个池
            //    (AC-018-4 单池不变量),池的所有者是本装配袋;导演只借用。
            //    若两处都 Dispose,第二个是空操作(池幂等)但语义上会造成「谁拥有池」的二义。
            _services?.AudioPool?.Dispose();
            if (_audioSeams?.Clips is AudioClipLibrary clipLibrary) clipLibrary.Dispose();
            _audioSeams = null;

            // 🔁 2026-10-29(`TD-audio-031` ①):**两条静态装配缝置 null**。
            // 病因:`AudioSettingsRegistry.Provider`(由 `ApplyAudioSettingsProvider` 赋值)与
            //   `PlayerBusVolumeInitializer.MixerResolver`(由 `ApplyPlayerBusDefaultsOrFail` 赋值)
            //   都是 `public static`,而本方法此前**只 dispose 五件、不置 null 两缝** ⇒
            //   它们跨 Play 轮悬垂指向已 `Addressables.Release` 的 provider / mixer。
            // ⚠️ **今天为什么没炸**:`EditorSettings.asset` 的 `m_EnterPlayModeOptionsEnabled: 1`
            //   + `m_EnterPlayModeOptions: 0` ⇒ **完整域重载**,静态每次 Play 复位。
            //   该设置一旦关掉(域重载关闭是常见的加速手段),即刻咬人 —— 故本债判「条件性」。
            // ⚠️ **顺序**:必须在 `_audioSeams = null` **之后**(上面那条 dispose 依赖 `_audioSeams`);
            //   两条缝与 `_audioSeams` 无引用关系,置 null 只是断静态根引用。
            AudioSettingsRegistry.Provider = null;
            PlayerBusVolumeInitializer.MixerResolver = null;

            _booted = false;
            _player = null;
            // CameraRig 的销毁**不在这里**:它已 SetParent(transform)(启动序第 5 步),
            // 随本对象的层级销毁一并走 —— 它身上挂着**唯一** AudioListener(AC-20-10),
            // 显式再 Destroy 会与层级销毁重复、且若顺序在前会让本帧出现「零听音点」窗口。
            _cameraRig = null;
            _services = null;
            _tickDriver = null;
            _uiDriver = null;
            // EventSystem 随 BootRoot 一同销毁(reparent 到 transform ⇒ 无需显式 Destroy);
            // 仅清引用。承 FindOrCreateSkeuoUi 同纪律。
            _eventSystem = null;
        }
    }
}
