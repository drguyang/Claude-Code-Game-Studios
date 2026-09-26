// 权威来源:production/epics/audio-system/story-003-mixer-topology-snapshots.md(2026-09-26 readiness 裁定:
//   「.mixer 资产由本 story 创建」;测试 mixer_topology_test.cs 钉路径 Assets/Audio/DaYiJingCheng.mixer)
// 2026-09-26 unity-specialist 裁定:**不手写 YAML** —— 6000.3.24f1 无公开创作 API
//   (UnityEditor.xml 无 AudioMixer.AddGroup/Snapshot/Send),内部符号存在于 UnityEditor.dll
//   字符串表(Internal_CreateAudioMixerController / AddGroup / CreateSnapshot /
//   AddExposedParameter / DoCreateAudioMixer …)⇒ 走**反射 + batch 执行**,
//   先生成黄金样例、再据实对齐 MixerTopologyGates 的 YAML 提取口径。
// 执行方式(非 GUI):
//   unity build <project> --target StandaloneLinux64 \
//     --executeMethod DaYiJingCheng.EditorTools.Gates.MixerAssetGenerator.Create
//   先探签名(对齐轮):同命令 --executeMethod ...MixerAssetGenerator.Discover
// 结构基准(以 MixerTopologyGates 断言为准):
//   · 七总线两级组:每总线(Master/Music/Ambience/Voice/SFX/Stethoscope/UICue)直接子 =
//     bus_volume_<bus>(玩家音量组,同 exposed 参数名)+ duck_<bus>(快照 duck 组);
//     Reverb 为 Aux 返回组(Master 之子)
//   · send 清单 = MixerTopologyGates.RegisteredAuxSends(当前 1 条:Ambience → Reverb)
//   · 快照五员 = MixerRegistry.SnapshotNames(Default/StethoscopeFocus/DialogueFocus/Paused/VRComfort)
//   · exposed = MixerRegistry.BusVolumeParameters(7 个 bus_volume_*)
//   · 快照捕获只打 duck 组(交集 ∅);**数值 = 占位 dB,归用户轮调**(工具内初值不是逻辑)
// ⚠️ **幂等**:资产已存在 ⇒ 不重建(保护用户调参),只跑校验日志;删文件重跑才重建。
// ⚠️ **风险登记(2026-09-26 交付报告)**:内部 API 签名未实测(UnityEditor.xml 无载荷)——
//   绑定按**参数名 + 类型**启发式;绑定失败 = 显式 throw(带全部候选与尝试轨迹),
//   batch 日志可直接定位;首跑预期需一轮签名对齐。

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace DaYiJingCheng.EditorTools.Gates
{
    /// <summary>.mixer 资产生成器(编辑期工具;Editor.Tools 族,**不进构建**)。
    /// 反射走 <c>UnityEditor.Audio.AudioMixerController</c> 内部创作 API,建七总线两级组 +
    /// Aux/Reverb send + 快照五员 + 7 exposed 参数,产物由 <c>MixerTopologyGates</c> 断言。</summary>
    public static class MixerAssetGenerator
    {
        /// <summary>资产路径(mixer_topology_test.cs 同一常量面)。</summary>
        public const string MixerAssetPath = "Assets/Audio/DaYiJingCheng.mixer";

        private const string ControllerTypeName = "UnityEditor.Audio.AudioMixerController, UnityEditor";
        private const string BindingsTypeName = "UnityEditor.Audio.AudioMixerControllerBindings, UnityEditor";

        private static readonly string[] GroupCreationMethods =
            { "Internal_CreateAudioMixerGroupController", "AddAudioMixerGroup", "AddGroup" };

        // 2026-09-26 三/四轮:MixerCreationMethods、SnapshotCreationMethods 常量已删 ——
        // 资产创建走 Discover 实测签名(CreateDefaultAsset / CreateMixerControllerAtPath);
        // 快照创建 = EnsureSnapshotsViaYaml **YAML 文本合成**(五轮实测:internal API 无创建面,
        // CloneNewSnapshotFromTarget 需编辑器窗口态,batch 返回空 —— 反射此路已证不通)。

        /// <summary>batch 主入口:缺失则生成(已有则幂等跳过重建),随后 SaveAssets + Refresh +
        /// 结构校验日志。</summary>
        /// <exception cref="InvalidOperationException">内部 API 绑定失败 / 结构关键步缺失(显式红)。</exception>
        public static void Create()
        {
            var log = new List<string>();
            try
            {
                string absoluteDir = Path.GetDirectoryName(
                    Path.GetFullPath(Path.Combine(Application.dataPath, "..", MixerAssetPath)));
                if (absoluteDir != null && !Directory.Exists(absoluteDir))
                    Directory.CreateDirectory(absoluteDir);

                string absolutePath = Path.GetFullPath(
                    Path.Combine(Application.dataPath, "..", MixerAssetPath));
                bool fileMissing = !File.Exists(absolutePath);
                bool fresh = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerAssetPath) == null;
                if (fileMissing)
                {
                    // 2026-09-27 修复:文件缺失时先建裸样例基底(CreateDefaultAsset),
                    // 否则 EnsureSnapshotsViaYaml 无快照文档可改 ⇒ throw
                    log.Add("[创建] 资产文件缺失 → 先建裸样例基底");
                    CreateMixerAsset(log);
                    log.Add($"[黄金样例] {MixerAssetPath} —— 基底由 Unity 自写(真字段形态来源)");
                }
                else if (fresh)
                {
                    log.Add("[创建] 资产缺失 → 走内部 API 生成");
                    CreateMixerAsset(log);
                    log.Add($"[黄金样例] {MixerAssetPath} —— 基底由 Unity 自写(真字段形态来源)");
                }
                else
                {
                    log.Add("[幂等] 资产已存在 → 不重建;拓扑**增量**补齐(已存在的组/快照跳过)");
                }

                // 2026-09-26 四轮:BuildTopology 接回 —— 组与快照按实测签名照抄执行(增量幂等);
                // effect 构造 / exposed 元素形态无签名 ⇒ 分相**探测 + 日志**,失败不阻断组/快照
                // (留待下轮,报告写明)。
                BuildTopology(log);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerAssetPath);
                if (mixer == null)
                    throw new InvalidOperationException(
                        "[MixerAssetGenerator] SaveAssets/Refresh 后资产仍不可加载:" + MixerAssetPath);

                // ② 快照五员 —— YAML 文本合成:**必须在组刷盘之后**(防原生 Save 覆盖文本);
                //    内含 ImportAsset(ForceUpdate);Unity 拒载会在此处显形(预期对齐信号)
                EnsureSnapshotsViaYaml(log);
                mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerAssetPath);
                if (mixer == null)
                    throw new InvalidOperationException(
                        "[MixerAssetGenerator] 快照 YAML 合成重载后资产不可加载 —— Unity 拒载?" +
                        "(见导入日志的 YAML 解析错)");

                // ②b 快照捕获(八轮):native SetValueForVolume 让 **Unity 自己序列化**
                //    m_FloatValues(键形态零猜测)—— 须在快照文本合成 + 重载之后(要快照/组对象)、
                //    最终 SaveAssets 之前(native 脏数据刷盘);随后读回断言非空(空 = 硬 throw)
                ApplySnapshotCapturesNative(mixer, log);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerAssetPath);
                if (mixer == null)
                    throw new InvalidOperationException(
                        "[捕获] SaveAssets 后资产不可加载 —— 捕获写入破坏了资产?");
                VerifyCapturesWritten(log);

                // API 层粗查(日志面;测试 test_realMixerAsset_* 是正式判据)
                AudioMixerGroup[] groups = mixer.FindMatchingGroups("");
                int snapshotHits = 0;
                foreach (string snapshotName in DaYiJingCheng.Gameplay.Presentation.Audio.MixerRegistry.SnapshotNames)
                    if (mixer.FindSnapshot(snapshotName) != null)
                        snapshotHits++;
                log.Add($"[API] 组数 = {groups.Length};快照命中 = {snapshotHits}/" +
                        DaYiJingCheng.Gameplay.Presentation.Audio.MixerRegistry.SnapshotNames.Count);

                // YAML 判词 —— **硬 throw**(八轮用户裁定:半成品静默落盘是真风险;
                // 门 = 构建失败级,与 MixerTopologyGates「错误列表 ⇒ throw」的调用方口径一致)
                string absolute = Path.GetFullPath(Path.Combine(Application.dataPath, "..", MixerAssetPath));
                if (File.Exists(absolute))
                {
                    IReadOnlyList<string> gateErrors =
                        MixerTopologyGates.ValidateMixerTopology(File.ReadAllText(absolute));
                    if (gateErrors.Count == 0)
                    {
                        log.Add("[YAML] MixerTopologyGates 全绿");
                    }
                    else
                    {
                        foreach (string error in gateErrors)
                            Debug.LogError("[MixerAssetGenerator][YAML门] " + error);
                        throw new InvalidOperationException(
                            "[MixerAssetGenerator] MixerTopologyGates 失败(" + gateErrors.Count +
                            " 条)—— 半成品不静默落盘,见上方红行:\n" + string.Join("\n", gateErrors));
                    }
                }

                Debug.Log("[MixerAssetGenerator] 完成\n" + string.Join("\n", log));
            }
            catch (Exception ex)
            {
                Debug.LogError("[MixerAssetGenerator] 失败\n" + string.Join("\n", log) +
                               "\n---- 异常 ----\n" + ex);
                throw;
            }
        }

        /// <summary>签名探针(对齐轮用):导出内部类型的**方法 + 构造 + 实例字段**签名到日志。
        /// 2026-09-26 二轮:扫描目标扩至快照 / 组 / 效果 / 参数路径四个内部类型
        /// (一轮只扫 Controller,拿不到「新建快照 / 加 send / AudioParameterPath 形态」)。</summary>
        public static void Discover()
        {
            foreach (string typeName in new[]
                     {
                         ControllerTypeName, BindingsTypeName,
                         "UnityEditor.Audio.AudioMixerSnapshotController, UnityEditor",
                         "UnityEditor.Audio.AudioMixerGroupController, UnityEditor",
                         "UnityEditor.Audio.AudioMixerEffectController, UnityEditor",
                         "UnityEditor.Audio.AudioParameterPath, UnityEditor",
                         // 2026-09-26 四轮增补:exposed 备选路径 set_exposedParameters(T[]) 的
                         // 元素类型 —— 两个候选命名空间(找不到 = WARN 不中断)
                         "UnityEditor.Audio.ExposedAudioParameter, UnityEditor",
                         "UnityEngine.ExposedAudioParameter, UnityEngine.AudioModule",
                     })
            {
                Type type = Type.GetType(typeName);
                if (type == null)
                {
                    Debug.LogWarning("[MixerAssetGenerator.Discover] 类型未找到:" + typeName);
                    continue;
                }
                foreach (MethodInfo method in type.GetMethods(
                             BindingFlags.Public | BindingFlags.NonPublic |
                             BindingFlags.Static | BindingFlags.Instance |
                             BindingFlags.DeclaredOnly))
                {
                    string parms = string.Join(", ",
                        Array.ConvertAll(method.GetParameters(), p => p.ParameterType.Name + " " + p.Name));
                    Debug.Log($"[Discover] {type.Name}::{method.Name}" +
                              (method.IsStatic ? " (static)" : "") + $"({parms}) → {method.ReturnType.Name}");
                }
                foreach (ConstructorInfo ctor in type.GetConstructors(
                             BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    string parms = string.Join(", ",
                        Array.ConvertAll(ctor.GetParameters(), p => p.ParameterType.Name + " " + p.Name));
                    Debug.Log($"[Discover] {type.Name}::.ctor({parms})");
                }
                // 2026-09-26 二轮增:实例字段(AudioParameterPath 的构成 / 快照控制器的挂接点)
                foreach (FieldInfo field in type.GetFields(
                             BindingFlags.Public | BindingFlags.NonPublic |
                             BindingFlags.Static | BindingFlags.Instance |
                             BindingFlags.DeclaredOnly))
                {
                    Debug.Log($"[Discover] {type.Name}::{field.Name}" +
                              (field.IsStatic ? " (static)" : "") +
                              $" : {field.FieldType.Name}");
                }
                if (type.IsEnum)
                    Debug.Log($"[Discover] {type.Name} 枚举值: " +
                              string.Join(", ", Enum.GetNames(type)));
            }
            Debug.Log("[MixerAssetGenerator.Discover] 完成 —— 按日志回改 Create 的绑定启发式");
        }

        // ══════════════ 步骤 1:资产本体 ══════════════

        private static void CreateMixerAsset(List<string> log)
        {
            Type controllerType = Type.GetType(ControllerTypeName);
            if (controllerType == null)
                throw new InvalidOperationException("[创建] 未找到 " + ControllerTypeName);

            // 2026-09-26 Discover 实测签名 —— 不再猜,只走两条已知路径:
            //   CreateDefaultAsset(String path) → Void            ← 首选(Unity 自写默认样例,
            //       产物即**黄金样例**:m_Sends / m_ValueMap / 快照段真实字段形态以它为准)
            //   ⚠️ 2026-09-27:CreateDefaultAsset 是**实例方法**(IL 证实),需 controller 实例调用
            //   CreateMixerControllerAtPath(String path) → AudioMixerController ← 兜底(static)
            var attempts = new List<string>();
            foreach (string methodName in new[] { "CreateDefaultAsset", "CreateMixerControllerAtPath" })
            {
                MethodInfo match = null;
                foreach (MethodInfo candidate in controllerType.GetMethods(
                             BindingFlags.Public | BindingFlags.NonPublic |
                             BindingFlags.Static | BindingFlags.Instance))
                {
                    ParameterInfo[] parameters = candidate.GetParameters();
                    if (candidate.Name == methodName && parameters.Length == 1 &&
                        parameters[0].ParameterType == typeof(string))
                    {
                        match = candidate;
                        break;
                    }
                }
                if (match == null)
                {
                    attempts.Add(methodName + ":未找到「(String path)」形态 —— 按二轮 Discover 日志核对");
                    continue;
                }
                try
                {
                    // 2026-09-27:CreateDefaultAsset 是实例方法,需 controller 实例;
                    // CreateMixerControllerAtPath 是静态方法,target 传 null
                    object target = match.IsStatic ? null : CreateControllerInstance(controllerType);
                    match.Invoke(target, new object[] { MixerAssetPath });
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                    log.Add($"[创建] 命中 {methodName}(path = {MixerAssetPath})");
                    if (AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerAssetPath) != null)
                        return;   // 落盘成功 → 由 Create() 读 YAML 当黄金样例
                    attempts.Add(methodName + ":调用无异常但资产未出现");
                }
                catch (Exception ex)
                {
                    attempts.Add(methodName + " → " + ex.GetBaseException().Message);
                }
            }

            throw new InvalidOperationException(
                "[创建] 两条已知路径全失败:\n" + string.Join("\n", attempts));
        }

        /// <summary>创建 AudioMixerController 实例(供实例方法 CreateDefaultAsset 调用)。
        /// 2026-09-27:AudioMixer 无公开构造函数,用 GetUninitializedObject 绕过。</summary>
        private static object CreateControllerInstance(Type controllerType)
        {
            try
            {
                return System.Runtime.Serialization.FormatterServices.GetUninitializedObject(controllerType);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "[创建] 无法构造 AudioMixerController 实例:" + ex.GetBaseException().Message);
            }
        }

        // ══════════════ 步骤 2:拓扑(两级组 / 快照 / exposed / send / 捕获)══════════════

        private static void BuildTopology(List<string> log)
        {
            Type controllerType = Type.GetType(ControllerTypeName);
            AudioMixer mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerAssetPath);
            if (controllerType == null || mixer == null)
                throw new InvalidOperationException("[拓扑] controller 类型 / mixer 资产缺失");

            // 运行期加载的资产本体是内部子类(AudioMixerController : AudioMixer)—— 直接当
            // controller 用;不是则按 Discover 日志核对取用路径(硬失败)。
            object controller = mixer;
            if (!controllerType.IsInstanceOfType(mixer))
                throw new InvalidOperationException(
                    "[拓扑] 资产本体不是 AudioMixerController 实例(实际 = " +
                    mixer.GetType().FullName + ")—— 按 Discover 日志调整取用路径");

            // ① 七总线两级组(硬阶段 · 签名照抄):CreateNewGroup(String, Boolean) +
            //    AddChildToParent(child, parent);**增量幂等**(已存在的组跳过 —— 裸样例基底
            //    与二次运行都不重复建)。
            object master = GetMember(controller, "masterGroup");
            if (master == null)
                throw new InvalidOperationException(
                    "[拓扑·组] 取不到 masterGroup(property / get_masterGroup 均无)");

            var groups = new Dictionary<string, object>(StringComparer.Ordinal) { ["Master"] = master };
            foreach (string bus in DaYiJingCheng.Gameplay.Presentation.Audio.MixerRegistry.BusNames)
            {
                if (bus == "Master") continue;
                object busGroup = EnsureChildGroup(controller, master, bus, log);
                groups[bus] = busGroup;

                string volumeName = DaYiJingCheng.Gameplay.Presentation.Audio.MixerRegistry
                    .VolumeGroupForBus(bus);
                string duckName = DaYiJingCheng.Gameplay.Presentation.Audio.MixerRegistry
                    .DuckGroupForBus(bus);
                groups[volumeName] = EnsureChildGroup(controller, busGroup, volumeName, log);
                groups[duckName] = EnsureChildGroup(controller, busGroup, duckName, log);
            }

            // Master 自己的两级组(挂在 Master 之下,与兄弟总线同格)
            string masterVolume = DaYiJingCheng.Gameplay.Presentation.Audio.MixerRegistry
                .VolumeGroupForBus("Master");
            string masterDuck = DaYiJingCheng.Gameplay.Presentation.Audio.MixerRegistry
                .DuckGroupForBus("Master");
            groups[masterVolume] = EnsureChildGroup(controller, master, masterVolume, log);
            groups[masterDuck] = EnsureChildGroup(controller, master, masterDuck, log);

            // Reverb 返回组(Aux 目标 —— send 的落点,下一轮 send 阶段用)
            groups["Reverb"] = EnsureChildGroup(controller, master, "Reverb", log);
            log.Add($"[拓扑·组] 完成:七总线两级 + Reverb(组对象 {groups.Count} 个)");

            // ② 快照五员 —— **改走 YAML 文本合成**(五轮实测裁定:CloneNewSnapshotFromTarget
            //    依赖编辑器窗口态,batch 返回空;全库无 AddSnapshot/CreateSnapshot,
            //    EditingTargetSnapshot 只有 getter)⇒ 合成移出本方法,由 Create() 在
            //    SaveAssets 把组刷盘**之后**调 EnsureSnapshotsViaYaml(防原生 Save 覆盖文本)。

            // ③ Aux/Reverb send(五轮实测路线):CopyEffect(既有 Attenuation)复制两侧 effect
            //    → set_sendTarget(目标 effect)→ InsertEffect(effect, group, index)。
            BuildSends(controller, groups, log);

            // ④ exposed 7 参数(软阶段):备选 set_exposedParameters(T[]) 元素成员反射填充;
            //    形态不符 = 记日志留待下轮(AudioParameterPath 构造待 Discover),不猜。
            TryExposeBusVolumeParameters(controller, log);
        }

        /// <summary>确保 parent 下存在名为 name 的子组(增量幂等):
        /// 已存在直接取;否则 <c>CreateNewGroup(name, false)</c> + <c>AddChildToParent(child, parent)</c>
        /// (签名照抄;参序回退一次;「已自动挂接」放行)。</summary>
        private static object EnsureChildGroup(object controller, object parent, string name,
                                               List<string> log)
        {
            object existing = FindChildByName(parent, name);
            if (existing != null) return existing;

            object created = InvokeSig(controller, "CreateNewGroup", name, false);
            if (created == null)
                created = FindChildByName(parent, name);
            if (created == null)
                throw new InvalidOperationException(
                    $"[拓扑·组] CreateNewGroup(\"{name}\") 后既无返回值也找不到子组 —— 按 Discover 日志核对");
            Rename(created, name);

            try
            {
                InvokeSig(controller, "AddChildToParent", created, parent);
            }
            catch (Exception primary)
            {
                try
                {
                    InvokeSig(controller, "AddChildToParent", parent, created);   // 参序回退
                }
                catch (Exception secondary)
                {
                    if (FindChildByName(parent, name) == null)
                        throw new InvalidOperationException(
                            $"[拓扑·组] AddChildToParent(\"{name}\") 两种参序均失败且未自动挂接:" +
                            primary.GetBaseException().Message + " / " +
                            secondary.GetBaseException().Message);
                    log.Add($"[拓扑·组] AddChildToParent(\"{name}\") 报错但组已在父下(自动挂接)—— 放行");
                }
            }

            log.Add($"[拓扑·组] + {name}");
            return created;
        }

        /// <summary>快照五员阶段 —— **YAML 文本合成**(五轮实测裁定:internal API 无快照创建面,
        /// CloneNewSnapshotFromTarget 需编辑器窗口态,batch 返回空 ⇒ 反射此路不通,不再试)。
        /// <para>做法:既有首个快照改名 Default → 按样例模板补写缺员文档(大正整数 fileID 查重、
        /// m_AudioMixer 从既有快照文档读、m_SnapshotID 32 位 hex 各唯一)→ 新 fileID 追加进根文档
        /// `m_Snapshots:` 列表 → 写回 + `ImportAsset(ForceUpdate)` 让 Unity 重载验证。
        /// <b>幂等</b>:5 名齐则整段跳过。须在 SaveAssets 刷盘之后调用(防原生状态覆盖文本)。</para></summary>
        private static void EnsureSnapshotsViaYaml(List<string> log)
        {
            string absolute = Path.GetFullPath(Path.Combine(Application.dataPath, "..", MixerAssetPath));
            if (!File.Exists(absolute))
                throw new InvalidOperationException("[快照·YAML] 资产文件缺失:" + absolute);
            // 统一 LF(下方所有行级判定 / 区间裁剪都在 \n 上做;Force Text 两种行尾 Unity 均可载)
            string text = File.ReadAllText(absolute).Replace("\r\n", "\n");

            // ⓪ 裁剪野员:m_Name ∉ 五员允许集的快照文档(如早期废案克隆留下的
            //    "Default - Copy")⇒ 删文档 + 摘 m_Snapshots 引用 —— **整行精确比对**
            //    (名含 `-`,前缀匹配会放过;允许集 = MixerRegistry.SnapshotNames 闭集)
            text = PruneForeignSnapshots(text, log);

            // ⓪a 悬空槽位修复 —— **无条件**:上一轮裁掉的野员可能让 m_TargetSnapshot 悬空,
            //     本轮没有新裁剪也必须修(Unity 导入报 Broken text PPtr)。
            text = RepairDanglingSnapshotSlots(text, log);

            // ⓪b exposed GUID 对齐同名组 m_Volume 哈希(2026-09-26 重写,见方法注):
            //     旧「全零 ⇒ b500… 条序合成值」对不上任何真实参数 ⇒ SetFloat 恒 false = 哑 exposed;
            //     现按暴露名 == 组名查真哈希,查不到 ⇒ 硬 throw(拒绝静默写假值)。
            text = NormalizeExposedGuids(text, log);

            // ⓪c 七轮校准:孤儿 effect 裁剪 —— 有 m_SendTarget ≠ 0 却未挂在任何组 m_Effects
            //     下的 effect 文档(前几轮失败尝试的遗留)⇒ 门的 AC-44-E3 ② 红源,删文档治愈;
            //     m_SendTarget == 0 的游离 effect 门不罚,留白(避免误删待用返回侧)。
            text = PruneOrphanEffects(text, log);

            // ① 既有首个快照(裸样例名 "Snapshot")改名 Default —— 只动首个快照文档的 m_Name 行
            if (!ContainsSnapshotName(text, "Default"))
            {
                int firstDoc = text.IndexOf("AudioMixerSnapshotController:", StringComparison.Ordinal);
                if (firstDoc < 0)
                    throw new InvalidOperationException("[快照·YAML] 无既有快照文档 —— 无改名基底");
                int nameKey = text.IndexOf("m_Name:", firstDoc, StringComparison.Ordinal);
                if (nameKey < 0)
                    throw new InvalidOperationException("[快照·YAML] 首个快照文档无 m_Name 行");
                int lineEnd = text.IndexOf('\n', nameKey);
                if (lineEnd < 0) lineEnd = text.Length;
                text = text.Substring(0, nameKey) + "m_Name: Default" + text.Substring(lineEnd);
                log.Add("[快照·YAML] 既有首个快照改名 → Default");
            }

            // ② m_AudioMixer 引用(从既有快照文档读,不写死)
            int mixerRefKey = text.IndexOf("m_AudioMixer:", StringComparison.Ordinal);
            if (mixerRefKey < 0)
                throw new InvalidOperationException("[快照·YAML] 既有快照无 m_AudioMixer 行 —— 无法取根 fileID");
            int mixerRefEnd = text.IndexOf('\n', mixerRefKey);
            if (mixerRefEnd < 0) mixerRefEnd = text.Length;
            string mixerRef = text.Substring(mixerRefKey, mixerRefEnd - mixerRefKey)
                .Trim();   // 形如 m_AudioMixer: {fileID: 24100000}

            // ③ 缺员按模板补写(幂等:已存在的名字跳过)
            var appended = new List<KeyValuePair<string, long>>();
            int sequence = 1;
            foreach (string snapshotName in DaYiJingCheng.Gameplay.Presentation.Audio.MixerRegistry.SnapshotNames)
            {
                if (ContainsSnapshotName(text, snapshotName)) continue;
                long fileId = 810000000000000000L + sequence;
                while (text.Contains("&" + fileId.ToString(System.Globalization.CultureInfo.InvariantCulture)))
                    fileId++;
                // 32 位 hex:前缀 da70 + 26 个 0 + 两位序号(各快照唯一、确定性、非随机)
                string snapshotId = "da70" + new string('0', 26) +
                                    sequence.ToString("D2", System.Globalization.CultureInfo.InvariantCulture);
                text += (text.EndsWith("\n", StringComparison.Ordinal) ? "" : "\n") +
                        "--- !u!245 &" + fileId + "\n" +
                        "AudioMixerSnapshotController:\n" +
                        "  m_ObjectHideFlags: 0\n" +
                        "  m_CorrespondingSourceObject: {fileID: 0}\n" +
                        "  m_PrefabInstance: {fileID: 0}\n" +
                        "  m_PrefabAsset: {fileID: 0}\n" +
                        "  m_Name: " + snapshotName + "\n" +
                        "  " + mixerRef + "\n" +
                        "  m_SnapshotID: " + snapshotId + "\n" +
                        "  m_FloatValues: {}\n" +
                        "  m_TransitionOverrides: {}\n";
                appended.Add(new KeyValuePair<string, long>(snapshotName, fileId));
                log.Add($"[快照·YAML] + {snapshotName}(fileID {fileId}, SnapshotID {snapshotId})");
                sequence++;
            }

            // ④ 新 fileID 追加进根文档 m_Snapshots: 列表(插在列表末项之后、下一键之前)
            if (appended.Count > 0)
            {
                int key = text.IndexOf("m_Snapshots:", StringComparison.Ordinal);
                if (key < 0)
                    throw new InvalidOperationException("[快照·YAML] 根文档无 m_Snapshots: 列表 —— 无法注册");
                int cursor = text.IndexOf('\n', key) + 1;
                while (cursor < text.Length)
                {
                    int lineEnd = text.IndexOf('\n', cursor);
                    if (lineEnd < 0) lineEnd = text.Length;
                    string line = text.Substring(cursor, lineEnd - cursor).Trim();
                    if (line.StartsWith("- ", StringComparison.Ordinal))
                    { cursor = lineEnd + 1; continue; }
                    break;
                }
                string insert = "";
                foreach (KeyValuePair<string, long> entry in appended)
                    insert += "  - {fileID: " + entry.Value + "}\n";
                text = text.Substring(0, cursor) + insert + text.Substring(cursor);
                log.Add($"[快照·YAML] m_Snapshots: 追加 {appended.Count} 条引用");
            }

            // ⑤ 写回 + 强制重载(Unity 拒载会在导入日志 / 测试中显形 —— 预期内的对齐信号)
            File.WriteAllText(absolute, text);
            AssetDatabase.ImportAsset(MixerAssetPath, ImportAssetOptions.ForceUpdate);
            log.Add("[快照·YAML] 写回 + ImportAsset(ForceUpdate) —— 由后续 API 粗查 / 测试验证重载");
        }

        /// <summary>exposed GUID **对齐同名组 <c>m_Volume</c> 哈希**(2026-09-26 用户裁定;旧
        /// 「全零 ⇒ 合成 <c>b500…</c> 条序值」路径**退役**)。
        /// <para><b>根因</b>:Unity <c>AudioMixer.SetFloat(name, v)</c> 按 name → guid → **真实
        /// 参数路径**解析,合成 guid 对不上任何真实参数 ⇒ 返回 false,**整批 exposed 哑**
        /// (探针 <c>test_busVolumeExposedParam_actuallyAcceptsSetFloat</c> 实证)。
        /// 暴露名 == 组名(<c>bus_volume_music</c> 两者同名)⇒ 一一对应、确定性同源。</para>
        /// <para><b>失败行为(拒绝静默写假值 —— 正是本次踩的坑)</b>:① 全文解析不到任何组
        /// <c>m_Volume</c> 哈希 ⇒ LogError + throw;② 某 exposed 名查不到同名组 ⇒ LogError +
        /// throw(含该名 —— 未来 filter 参数须先认领资产拓扑,见
        /// <c>MixerRegistry.TierFilterParameters</c> 缺口登记)。幂等:已是正确哈希则不改写。</para>
        /// <para>哈希解析**复用** <c>MixerTopologyGates.GroupVolumeHashByName</c>(内部即
        /// <c>MixerDoc.VolumeHash</c>)—— 单一出处,不另写解析。</para></summary>
        public static string NormalizeExposedGuids(string text, List<string> log)
        {
            string[] lines = text.Split('\n');
            int start = -1;
            for (int i = 0; i < lines.Length; i++)
                if (lines[i].Trim() == "m_ExposedParameters:") { start = i; break; }
            if (start < 0) return text;

            // 暴露名 → 真实参数哈希(组 m_Volume);空 = 无从对齐 ⇒ 硬失败
            Dictionary<string, string> volumeHashByName =
                DaYiJingCheng.EditorTools.Gates.MixerTopologyGates.GroupVolumeHashByName(text);
            if (volumeHashByName.Count == 0)
            {
                string msg = "[exposed·YAML] 未解析到任何组的 m_Volume 哈希 —— 无法写真实 guid" +
                             "(拒绝静默写假值 —— 2026-09-26 哑 exposed 事故根因)";
                Debug.LogError("[MixerAssetGenerator] " + msg);
                throw new InvalidOperationException(msg);
            }

            int entry = 0;
            bool changed = false;
            int guidLine = -1;
            string currentHex = null;
            for (int i = start + 1; i < lines.Length; i++)
            {
                string trimmed = lines[i].Trim();
                if (trimmed.StartsWith("- guid:", StringComparison.Ordinal) ||
                    trimmed.StartsWith("guid:", StringComparison.Ordinal))
                {
                    int key = trimmed.IndexOf("guid:", StringComparison.Ordinal);
                    currentHex = trimmed.Substring(key + "guid:".Length).Trim();
                    guidLine = i;
                    entry++;
                    continue;
                }
                if (trimmed.StartsWith("- name:", StringComparison.Ordinal) ||
                    trimmed.StartsWith("name:", StringComparison.Ordinal))
                {
                    int nameKey = trimmed.IndexOf("name:", StringComparison.Ordinal);
                    string name = trimmed.Substring(nameKey + "name:".Length).Trim();
                    if (guidLine < 0)
                    {
                        string msg = $"[exposed·YAML] 条目「{name}」有 name 无 guid 行 ——" +
                                     " 结构异常,拒绝继续";
                        Debug.LogError("[MixerAssetGenerator] " + msg);
                        throw new InvalidOperationException(msg);
                    }

                    if (!volumeHashByName.TryGetValue(name, out string realHash))
                    {
                        // 合成条序 guid(b500…)已退役:查不到同名组 ⇒ 硬失败,绝不写假值
                        string msg = $"[exposed·YAML] exposed 名「{name}」查不到同名组的 " +
                                     "m_Volume 哈希 —— 拒绝写假 guid(合成条序路径 2026-09-26 退役;" +
                                     "若为未来 filter 参数,先裁定资产拓扑)";
                        Debug.LogError("[MixerAssetGenerator] " + msg);
                        throw new InvalidOperationException(msg);
                    }

                    if (!string.Equals(currentHex, realHash, StringComparison.Ordinal))
                    {
                        // 含旧 b500… 合成值与全零 —— 一律改写为真哈希(本方法的存在理由)
                        lines[guidLine] = "  - guid: " + realHash;
                        changed = true;
                    }

                    guidLine = -1;
                    currentHex = null;
                    continue;
                }
                if (trimmed.Length == 0) continue;
                break;   // 块结束(下一个 m_ 键 / 文档头)
            }

            if (!changed) return text;
            log.Add($"[exposed·YAML] GUID 对齐同名组 m_Volume 哈希(条目 {entry};真参数落点 ——" +
                    " 合成 b500… 条序值退役,2026-09-26)");
            return string.Join("\n", lines);
        }

        /// <summary>裁剪孤儿 effect(七轮):<c>m_SendTarget ≠ 0</c> 却**未挂在任何组
        /// <c>m_Effects</c>** 下的 effect 文档 = 前几轮失败尝试的遗留(门 AC-44-E3 ② 的红源),
        /// 删除其文档区间;sendTarget 为 0 的游离 effect 不罚也留白(避免误删返回侧)。</summary>
        public static string PruneOrphanEffects(string text, List<string> log)
        {
            string[] lines = text.Split('\n');

            // ① 已挂组的 effect id 集(组文档 m_Effects 列表)
            var owned = new HashSet<long>();
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Trim() != "m_Effects:") continue;
                int j = i + 1;
                while (j < lines.Length && lines[j].Trim().StartsWith("- ", StringComparison.Ordinal))
                {
                    long id = FileIdOf(lines[j]);
                    if (id != 0) owned.Add(id);
                    j++;
                }
            }

            // ② 扫 effect 文档:未挂组 ∧ sendTarget ≠ 0 ⇒ 删
            var removeLine = new bool[lines.Length];
            bool pruned = false;
            for (int i = 0; i < lines.Length; i++)
            {
                if (!lines[i].StartsWith("--- ", StringComparison.Ordinal)) continue;
                if (i + 1 >= lines.Length ||
                    !string.Equals(lines[i + 1].Trim(), "AudioMixerEffectController:",
                        StringComparison.Ordinal))
                    continue;
                int end = lines.Length;
                for (int j = i + 1; j < lines.Length; j++)
                    if (lines[j].StartsWith("--- ", StringComparison.Ordinal)) { end = j; break; }

                long fileId = AmpFileId(lines[i]);
                long sendTarget = 0;
                string effectName = "?";
                for (int j = i + 1; j < end; j++)
                {
                    string trimmed = lines[j].Trim();
                    if (trimmed.StartsWith("m_SendTarget:", StringComparison.Ordinal))
                        sendTarget = FileIdOf(lines[j]);
                    else if (trimmed.StartsWith("m_EffectName:", StringComparison.Ordinal))
                        effectName = trimmed.Substring("m_EffectName:".Length).Trim();
                }

                if (fileId == 0 || owned.Contains(fileId) || sendTarget == 0) continue;
                for (int j = i; j < end; j++) removeLine[j] = true;
                pruned = true;
                log.Add($"[send·YAML] 裁剪孤儿 effect「{effectName}」(fileID {fileId}, " +
                        $"sendTarget {sendTarget})—— 有 sendTarget 未挂任何组(失败遗留)");
            }

            if (!pruned) return text;
            var builder = new System.Text.StringBuilder(text.Length);
            for (int i = 0; i < lines.Length; i++)
            {
                if (removeLine[i]) continue;
                builder.Append(lines[i]).Append('\n');
            }
            return builder.ToString();
        }

        /// <summary>行内 <c>fileID: N</c> 取值(无则 0)。</summary>
        private static long FileIdOf(string line)
        {
            int idx = line.IndexOf("fileID:", StringComparison.Ordinal);
            if (idx < 0) return 0;
            int start = idx + "fileID:".Length;
            while (start < line.Length && char.IsWhiteSpace(line[start])) start++;
            int end = start;
            if (end < line.Length && line[end] == '-') end++;
            while (end < line.Length && char.IsDigit(line[end])) end++;
            return long.TryParse(line.Substring(start, end - start), out long id) ? id : 0;
        }

        /// <summary>头行 <c>--- !u!244 &amp;N</c> 的 fileID(无则 0)。</summary>
        private static long AmpFileId(string headerLine)
        {
            int amp = headerLine.IndexOf('&');
            if (amp < 0) return 0;
            int start = amp + 1, end = start;
            // fileID 可为负(Unity 分配的大负数,如 &-3543781766084107196)——
            // 原实现只扫数字 ⇒ 负号处即停、子串为空 ⇒ 返回 0 ⇒ 孤儿 effect 被判「fileId==0」跳过,
            // PruneOrphanEffects 永远裁不掉它(2026-09-26 八轮实测)。与 FileIdOf 的负号处理对齐。
            if (end < headerLine.Length && headerLine[end] == '-') end++;
            while (end < headerLine.Length && char.IsDigit(headerLine[end])) end++;
            return long.TryParse(headerLine.Substring(start, end - start), out long id) ? id : 0;
        }

        /// <summary>裁剪野员快照:遍历 <c>AudioMixerSnapshotController</c> 文档,<c>m_Name</c>
        /// **整行精确比对** ∉ <c>MixerRegistry.SnapshotNames</c> 允许集 ⇒ 删除整个文档区间 +
        /// 从根文档 <c>m_Snapshots:</c> 列表摘除对应 fileID 行。
        /// <para>2026-09-26 六轮:早前 <c>CloneNewSnapshotFromTarget</c> 半成功留下
        /// <c>Default - Copy</c> 野员(6 快照)—— 名中含 `-`,**前缀匹配会放过**,必须整行比对。
        /// 缺 <c>m_Name</c> 的快照文档**不擅自删**(归 roster 判红)。</para></summary>
        public static string PruneForeignSnapshots(string text, List<string> log)
        {
            var allowed = new HashSet<string>(StringComparer.Ordinal);
            foreach (string name in DaYiJingCheng.Gameplay.Presentation.Audio.MixerRegistry.SnapshotNames)
                allowed.Add(name);
            // REC(八轮):允许集 = 五员 ∪ reverb_preset_ 前缀 —— MixerRegistry.ReverbPresetSnapshotPrefix
            // 自称「快照由本生成器占位」;只认五员会把将来的 reverb_preset_{indoor,…} 当野员删掉。
            // 门侧 ValidateSnapshotRoster 同口径(五员必在 + 额外快照不报错)。
            // 2026-09-27:允许集 += "Snapshot" —— CreateDefaultAsset 产出的裸样例基底名,
            //  EnsureSnapshotsViaYaml 需要它作为改名 Default 的基底;裁掉则无基底可改。
            allowed.Add("Snapshot");

            string[] lines = text.Split('\n');
            var removeLine = new bool[lines.Length];
            var removedFileIds = new List<long>();
            bool pruned = false;

            for (int i = 0; i < lines.Length; i++)
            {
                if (!lines[i].StartsWith("--- ", StringComparison.Ordinal)) continue;
                int typeLine = i + 1;
                if (typeLine >= lines.Length) break;
                if (!string.Equals(lines[typeLine].Trim(), "AudioMixerSnapshotController:",
                        StringComparison.Ordinal))
                    continue;

                // 文档区间:end = 下一个 "--- " 行(不含)或文件尾
                int end = lines.Length;
                for (int j = i + 1; j < lines.Length; j++)
                    if (lines[j].StartsWith("--- ", StringComparison.Ordinal)) { end = j; break; }

                // m_Name 取值(整行 trim 后比对 —— "Default - Copy" 含 `-`,禁前缀匹配)
                string snapshotName = null;
                for (int j = i + 1; j < end; j++)
                {
                    int key = lines[j].IndexOf("m_Name:", StringComparison.Ordinal);
                    if (key < 0) continue;
                    snapshotName = lines[j].Substring(key + "m_Name:".Length).Trim();
                    break;
                }
                if (snapshotName == null || allowed.Contains(snapshotName) ||
                    snapshotName.StartsWith(
                        DaYiJingCheng.Gameplay.Presentation.Audio.MixerRegistry
                            .ReverbPresetSnapshotPrefix, StringComparison.Ordinal))
                    continue;   // 五员 ∪ reverb_preset_* 前缀(见方法头 REC 注)

                // fileID(头行 & 之后)
                long fileId = 0;
                int amp = lines[i].IndexOf('&');
                if (amp >= 0)
                {
                    int s = amp + 1, e = s;
                    if (e < lines[i].Length && lines[i][e] == '-') e++; // fileID 可为负(同 AmpFileId 教训)
                    while (e < lines[i].Length && char.IsDigit(lines[i][e])) e++;
                    long.TryParse(lines[i].Substring(s, e - s), out fileId);
                }

                for (int j = i; j < end; j++) removeLine[j] = true;
                if (fileId != 0) removedFileIds.Add(fileId);
                pruned = true;
                log.Add($"[快照·YAML] 裁剪野员「{snapshotName}」(fileID {fileId})—— " +
                        "∉ 五员允许集(整行精确比对)");
            }

            if (!pruned) return text;

            // 摘除 m_Snapshots: 列表中的对应引用行(`  - {fileID: N}`)
            for (int i = 0; i < lines.Length; i++)
            {
                if (removeLine[i]) continue;
                string trimmed = lines[i].Trim();
                if (!trimmed.StartsWith("- ", StringComparison.Ordinal)) continue;
                foreach (long fileId in removedFileIds)
                    if (trimmed.Contains("{fileID: " + fileId + "}"))
                    { removeLine[i] = true; break; }
            }

            var builder = new System.Text.StringBuilder(text.Length);
            for (int i = 0; i < lines.Length; i++)
            {
                if (removeLine[i]) continue;
                builder.Append(lines[i]).Append('\n');
            }
            return builder.ToString();
        }

        /// <summary>修复**悬空快照槽位**:根文档 <c>m_StartSnapshot</c> / <c>m_TargetSnapshot</c>
        /// 指向的 fileID 在文件里已无对应文档 ⇒ Unity 报
        /// <c>Broken text PPtr ... Local file identifier (N) doesn't exist</c>。
        /// <para>**无条件执行**(不挂在「本轮是否有裁剪」上)—— 野员可能在**上一轮**已被裁掉,
        /// 本轮没有新裁剪但槽位仍悬空(2026-09-26 八轮实测:门断言已绿,Unity 导入错仍红)。</para>
        /// <para>修法 = 重指到文件里**第一个仍存在的快照文档**。</para></summary>
        public static string RepairDanglingSnapshotSlots(string text, List<string> log)
        {
            string[] lines = text.Split('\n');

            // ① 仍存在的快照文档 fileID 集
            var alive = new HashSet<long>();
            for (int i = 0; i < lines.Length; i++)
            {
                if (!lines[i].StartsWith("--- ", StringComparison.Ordinal)) continue;
                if (i + 1 >= lines.Length ||
                    !string.Equals(lines[i + 1].Trim(), "AudioMixerSnapshotController:",
                        StringComparison.Ordinal))
                    continue;
                long id = AmpFileId(lines[i]);
                if (id != 0) alive.Add(id);
            }
            if (alive.Count == 0) return text;

            long fallback = 0;
            foreach (long id in alive) { fallback = id; break; }

            bool changed = false;
            for (int i = 0; i < lines.Length; i++)
            {
                string trimmed = lines[i].Trim();
                bool isSlot = trimmed.StartsWith("m_StartSnapshot:", StringComparison.Ordinal) ||
                              trimmed.StartsWith("m_TargetSnapshot:", StringComparison.Ordinal);
                if (!isSlot) continue;
                long id = FileIdOf(lines[i]);
                if (id != 0 && alive.Contains(id)) continue;

                int at = lines[i].IndexOf("{fileID: ", StringComparison.Ordinal);
                int close = at >= 0 ? lines[i].IndexOf('}', at) : -1;
                if (at < 0 || close <= at) continue;
                lines[i] = lines[i].Substring(0, at) + "{fileID: " + fallback + "}" +
                           lines[i].Substring(close + 1);
                changed = true;
                log.Add($"[快照·YAML] 槽位 {trimmed.Split(':')[0].Trim()} 悬空(fileID {id} 无文档)" +
                        $" ⇒ 重指到存留快照 {fallback}(消 Broken text PPtr)");
            }

            return changed ? string.Join("\n", lines) : text;
        }

        /// <summary>文本内是否存在某快照名的 <c>m_Name:</c> 行(快照名与组名零交集 ——
        /// Default/StethoscopeFocus/DialogueFocus/Paused/VRComfort 均非组名)。</summary>
        private static bool ContainsSnapshotName(string text, string snapshotName)
            => text.Contains("m_Name: " + snapshotName + "\n") ||
               text.Contains("m_Name: " + snapshotName + "\r");

        /// <summary>send 阶段(软 · 五轮实测路线):<c>CopyEffect(既有 Attenuation)</c> 复制出
        /// 目标侧与源侧两个 effect → 源 effect <c>set_sendTarget(目标 effect)</c> →
        /// <c>InsertEffect(effect, group, index)</c> 分别挂进目标组与源组。
        /// 任一步失败 = 记日志留待下轮(不阻断组 / 快照)。</summary>
        private static void BuildSends(object controller, Dictionary<string, object> groups,
                                       List<string> log)
        {
            // CopyEffect 源 = Master 组既有的 Attenuation(裸样例自带;effects 成员名取
            // "effects" —— 与 children 同族命名,Discover 字段面可复核)
            object template = null;
            if (groups.TryGetValue("Master", out object master))
            {
                object effects = GetMember(master, "effects");
                if (effects is IEnumerable enumerable)
                    foreach (object effect in enumerable)
                        if (effect != null) { template = effect; break; }
            }
            if (template == null)
            {
                log.Add("[send] 留待下轮:Master 组取不到既有 effect 作 CopyEffect 源" +
                        "(effects 成员名待 Discover 字段面复核)");
                return;
            }

            foreach (MixerTopologyGates.MixerSendRegistration send in
                     MixerTopologyGates.RegisteredAuxSends)
            {
                if (!groups.TryGetValue(send.SourceGroup, out object source) ||
                    !groups.TryGetValue(send.TargetGroup, out object target))
                {
                    log.Add($"[send] 源/目标组缺失,跳过({send.SourceGroup}/{send.TargetGroup})");
                    continue;
                }

                // 幂等:源组已有「sendTarget 解析到目标组内 effect」的挂组 effect ⇒ 已建成,
                // 不再造副本(七轮孤儿的成因之一 = 每次重跑都 CopyEffect 两份)
                if (SendAlreadyPresent(source, target, send, log)) continue;

                // 七轮顺序修复(选项 a:**每个** CopyEffect 出的 effect 都 InsertEffect 且**复检**):
                // 先造返回侧 → 插入目标组 → **复检在组内**才造发送侧 + set_sendTarget + 插入复检 ——
                // 任一复检失败即中止,绝不留下「有 sendTarget 却未挂组」的新孤儿;
                // 历史孤儿由 EnsureSnapshotsViaYaml 的 PruneOrphanEffects 文本裁剪治愈。
                // 选 a 不选 b(单份):send 语义需要**两个** effect(发送侧 sendTarget 的指向物 =
                // 返回侧),单份无法同时满足「源已挂组」与「目标已挂组」两条门判据。
                try
                {
                    object returnEffect = InvokeSig(controller, "CopyEffect", template);
                    if (returnEffect == null)
                    {
                        log.Add("[send] 留待下轮:CopyEffect(返回侧)返回空 —— 按 Discover 日志核对");
                        return;
                    }
                    Rename(returnEffect, "ReverbReturn");
                    InsertEffect(target, returnEffect);
                    if (!IsEffectInGroup(target, returnEffect))
                    {
                        log.Add("[send] 中止:ReverbReturn 未落目标组(InsertEffect 未生效,复检失败)" +
                                "—— 不造发送侧,防新孤儿");
                        return;
                    }

                    object sendEffect = InvokeSig(controller, "CopyEffect", template);
                    if (sendEffect == null)
                    {
                        log.Add("[send] 留待下轮:CopyEffect(发送侧)返回空 —— 返回侧已挂组,幂等面可重入");
                        return;
                    }
                    Rename(sendEffect, send.Name);
                    SetMember(sendEffect, "sendTarget", returnEffect);
                    InsertEffect(source, sendEffect);
                    if (!IsEffectInGroup(source, sendEffect))
                    {
                        log.Add("[send] 中止:发送侧 effect 未落源组(复检失败)—— " +
                                "其 sendTarget 已置,若成孤儿由文本裁剪下轮清掉");
                        return;
                    }

                    log.Add($"[send] {send.SourceGroup} → {send.TargetGroup} 已建" +
                            "(两侧 InsertEffect 复检均在组内)");
                }
                catch (Exception ex)
                {
                    log.Add("[send] 留待下轮:CopyEffect/set_sendTarget/InsertEffect 失败 → " +
                            ex.GetBaseException().Message);
                }
            }
        }

        /// <summary>组的 effects 成员枚举(成员名 "effects",与 children 同族;取不到 = 空)。</summary>
        private static IEnumerable<object> EffectsOf(object group)
        {
            object effects = GetMember(group, "effects");
            if (effects is IEnumerable enumerable)
                foreach (object effect in enumerable)
                    if (effect != null) yield return effect;
        }

        /// <summary>effect 是否已挂在该组的 effects 内(引用同一实例;复检判据)。</summary>
        private static bool IsEffectInGroup(object group, object effect)
        {
            foreach (object candidate in EffectsOf(group))
                if (ReferenceEquals(candidate, effect)) return true;
            return false;
        }

        /// <summary>幂等:源组已存在「sendTarget 解析到目标组内 effect」的挂组 effect ⇒
        /// 该登记 send 已建成,跳过再造(防重复跑堆积副本)。</summary>
        private static bool SendAlreadyPresent(object sourceGroup, object targetGroup,
                                               MixerTopologyGates.MixerSendRegistration send,
                                               List<string> log)
        {
            foreach (object effect in EffectsOf(sourceGroup))
            {
                object sendTarget = GetMember(effect, "sendTarget");
                if (sendTarget == null) continue;
                if (IsEffectInGroup(targetGroup, sendTarget))
                {
                    log.Add($"[send] {send.SourceGroup} → {send.TargetGroup} 已存在" +
                            "(源侧 effect 的 sendTarget 命中目标组),幂等跳过");
                    return true;
                }
            }
            return false;
        }

        /// <summary>InsertEffect 形态回退链(六轮实测修正):方法挂在**组实例**上 ——
        /// <c>AudioMixerGroupController::InsertEffect(AudioMixerEffectController effect, Int32 index)</c>
        /// 实例方法;原对 controller 调 3 参「无签名匹配」即 send 失败根因。
        /// 主形态 <c>(effect, index)</c>;回退 <c>(index, effect)</c>;全败 = 抛给上层记软日志。</summary>
        private static void InsertEffect(object group, object effect)
        {
            try
            {
                InvokeSig(group, "InsertEffect", effect, 0);
                return;
            }
            catch (Exception primary)
            {
                try { InvokeSig(group, "InsertEffect", 0, effect); }
                catch (Exception secondary)
                {
                    throw new InvalidOperationException(
                        "组实例 InsertEffect 两形态全败:" + primary.GetBaseException().Message +
                        " / " + secondary.GetBaseException().Message);
                }
            }
        }

        /// <summary>exposed 阶段(软):备选路径 <c>set_exposedParameters(T[])</c> ——
        /// 元素类型反射构造 + 名字成员探测填充;任一环形态不符 = 留待下轮,不猜签名。</summary>
        private static void TryExposeBusVolumeParameters(object controller, List<string> log)
        {
            MethodInfo setter = null;
            foreach (MethodInfo method in controller.GetType().GetMethods(
                         BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                if (method.Name == "set_exposedParameters" && method.GetParameters().Length == 1 &&
                    method.GetParameters()[0].ParameterType.IsArray)
                { setter = method; break; }
            if (setter == null)
            {
                log.Add("[exposed] 留待下轮:无 set_exposedParameters(T[]);AddExposedParameter 的 " +
                        "AudioParameterPath 构造待 Discover —— 7 个 bus_volume_* 组已建,参数不猜");
                return;
            }

            Type elementType = setter.GetParameters()[0].ParameterType.GetElementType();
            var parameters = DaYiJingCheng.Gameplay.Presentation.Audio.MixerRegistry.BusVolumeParameters;
            Array array = Array.CreateInstance(elementType, parameters.Count);
            for (int i = 0; i < parameters.Count; i++)
            {
                object element = CreateExposedElement(elementType, parameters[i]);
                if (element == null)
                {
                    log.Add($"[exposed] 留待下轮:元素类型 {elementType.FullName} 的名字成员未实证" +
                            "(Discover 已加扫 ExposedAudioParameter)—— 不猜");
                    return;
                }
                array.SetValue(element, i);
            }

            try
            {
                setter.Invoke(controller, new object[] { array });
                log.Add($"[exposed] set_exposedParameters({parameters.Count}) 注册完成");
            }
            catch (Exception ex)
            {
                log.Add("[exposed] 留待下轮:set_exposedParameters 抛错 → " +
                        ex.GetBaseException().Message);
            }
        }

        /// <summary>构造 exposed 元素并写入名字(仅探测可写成员 <c>name/Name/parameterName/
        /// m_Name</c>;全不中 = null ⇒ 上层留待下轮)。</summary>
        private static object CreateExposedElement(Type elementType, string parameterName)
        {
            object element;
            if (elementType.IsValueType)
            {
                element = Activator.CreateInstance(elementType);
            }
            else
            {
                ConstructorInfo ctor = null;
                foreach (ConstructorInfo candidate in elementType.GetConstructors(
                             BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                    if (candidate.GetParameters().Length == 0) { ctor = candidate; break; }
                if (ctor == null) return null;
                element = ctor.Invoke(new object[0]);
            }
            if (element == null) return null;

            foreach (string memberName in new[] { "name", "Name", "parameterName", "m_Name" })
            {
                PropertyInfo property = elementType.GetProperty(memberName,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (property != null && property.CanWrite && property.PropertyType == typeof(string))
                {
                    property.SetValue(element, parameterName);
                    return element;
                }
                FieldInfo field = elementType.GetField(memberName,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null && field.FieldType == typeof(string))
                {
                    field.SetValue(element, parameterName);
                    return element;
                }
            }
            return null;
        }

        /// <summary>探测式构造 effect 实例(.NET 构造 —— Discover 已 dump .ctor;仅按参数**类型**
        /// 填值:string→name / bool→false / int→0 / 枚举→0 / 引用→null;其他值类型 = 不猜,跳过)。</summary>
        private static object CreateEffectInstance(Type effectType, string name, List<string> log)
        {
            foreach (ConstructorInfo ctor in effectType.GetConstructors(
                         BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                ParameterInfo[] parameters = ctor.GetParameters();
                var arguments = new object[parameters.Length];
                bool supported = true;
                for (int i = 0; i < parameters.Length; i++)
                {
                    Type parameterType = parameters[i].ParameterType;
                    if (parameterType == typeof(string)) arguments[i] = name;
                    else if (parameterType == typeof(bool)) arguments[i] = false;
                    else if (parameterType == typeof(int)) arguments[i] = 0;
                    else if (parameterType.IsEnum) arguments[i] = Enum.ToObject(parameterType, 0);
                    else if (parameterType.IsValueType) { supported = false; break; }
                    else arguments[i] = null;
                }
                if (!supported) continue;
                try
                {
                    object instance = ctor.Invoke(arguments);
                    if (instance != null)
                    {
                        Rename(instance, name);
                        return instance;
                    }
                }
                catch (Exception ex)
                {
                    log.Add($"[send] effect .ctor({parameters.Length} 参)失败 → " +
                            ex.GetBaseException().Message);
                }
            }
            return null;
        }

        // ── 反射小件 ──────────────────────────────────────────────────────────

        /// <summary>取实例成员(property 优先,get_ 方法兜底);失败返回 null(调用方决定软硬)。</summary>
        private static object GetMember(object target, string name)
        {
            if (target == null) return null;
            PropertyInfo property = target.GetType().GetProperty(name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
                BindingFlags.FlattenHierarchy);
            if (property != null && property.GetIndexParameters().Length == 0)
            {
                try { return property.GetValue(target); }
                catch { /* 成员存在但读取失败 → 按 get_ 方法再试 */ }
            }
            MethodInfo getter = target.GetType().GetMethod("get_" + name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (getter != null && getter.GetParameters().Length == 0)
            {
                try { return getter.Invoke(target, null); }
                catch { /* 同上,交调用方处理 null */ }
            }
            return null;
        }

        /// <summary>按名字 + 实参类型精确匹配调用(签名照抄的执行体);无匹配 / 全抛 = throw
        /// (带尝试轨迹)。</summary>
        private static object InvokeSig(object target, string methodName, params object[] arguments)
        {
            var attempts = new List<string>();
            // 含 Static:InsertEffect 曾被首轮 Discover 标为 (static) —— 静态/实例两面都找
            // (Invoke 对静态忽略 target,零副作用)
            foreach (MethodInfo method in target.GetType().GetMethods(
                         BindingFlags.Public | BindingFlags.NonPublic |
                         BindingFlags.Instance | BindingFlags.Static))
            {
                if (method.Name != methodName) continue;
                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length != arguments.Length) continue;
                bool compatible = true;
                for (int i = 0; i < parameters.Length; i++)
                {
                    if (arguments[i] == null)
                    {
                        if (parameters[i].ParameterType.IsValueType) { compatible = false; break; }
                        continue;
                    }
                    if (!parameters[i].ParameterType.IsInstanceOfType(arguments[i]))
                    { compatible = false; break; }
                }
                if (!compatible) continue;
                try { return method.Invoke(target, arguments); }
                catch (Exception ex) { attempts.Add(methodName + " → " + ex.GetBaseException().Message); }
            }

            if (attempts.Count == 0)
                throw new InvalidOperationException(
                    $"[反射] {target.GetType().Name}::{methodName}({arguments.Length} 参)无签名匹配");
            throw new InvalidOperationException(
                $"[反射] {methodName} 匹配到但全部抛错:" + string.Join(" | ", attempts));
        }

        /// <summary>写实例成员(property 优先,set_ 方法兜底);都没有 = throw。</summary>
        private static void SetMember(object target, string name, object value)
        {
            PropertyInfo property = target.GetType().GetProperty(name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (property != null && property.CanWrite)
            {
                property.SetValue(target, value);
                return;
            }
            MethodInfo setter = target.GetType().GetMethod("set_" + name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (setter != null)
            {
                setter.Invoke(target, new[] { value });
                return;
            }
            throw new InvalidOperationException(
                $"[反射] {target.GetType().Name} 无可写成员「{name}」");
        }

        /// <summary>在 parent 的 children 成员里按名找子组(增量幂等的查重面)。</summary>
        private static object FindChildByName(object parent, string name)
        {
            object children = GetMember(parent, "children");
            if (children is IEnumerable enumerable)
            {
                foreach (object child in enumerable)
                {
                    if (child == null) continue;
                    if (string.Equals(NameOf(child), name, StringComparison.Ordinal))
                        return child;
                }
            }
            return null;
        }

        /// <summary>对象名(UnityEngine.Object.name 优先,反射 name 属性兜底)。</summary>
        private static string NameOf(object target)
        {
            if (target is UnityEngine.Object unityObject && unityObject != null)
                return unityObject.name;
            return GetMember(target, "name") as string;
        }

        /// <summary>改名(UnityEngine.Object.name 优先;反射可写 name 属性兜底)。</summary>
        private static void Rename(object target, string name)
        {
            if (target is UnityEngine.Object unityObject && unityObject != null)
            {
                if (unityObject.name != name) unityObject.name = name;
                return;
            }
            PropertyInfo property = target.GetType().GetProperty("name",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (property != null && property.CanWrite)
                property.SetValue(target, name);
        }

        private static object CreateAndNameGroup(object controller, object parent, string name,
                                                  List<string> log)
        {
            object created = InvokeFirstMatch(controller, GroupCreationMethods,
                ctx => ctx.With("parent", parent).With("group", parent).With("name", name)
                           .With(name, name),
                "创建组 " + name, log);

            if (created is UnityEngine.Object unityObject && unityObject != null)
            {
                unityObject.name = name;
                return unityObject;
            }
            if (created != null)
            {
                PropertyInfo nameProperty = created.GetType().GetProperty("name") ??
                                             created.GetType().GetProperty("Name");
                nameProperty?.SetValue(created, name);
                return created;
            }

            // AddGroup 返回 void 的形态:取新出现的无名组兜底
            object fallback = FindGroupByPath(controller, name, log);
            if (fallback == null)
                throw new InvalidOperationException("[拓扑] 组「" + name + "」创建失败且无法定位");
            return fallback;
        }

        // ══════════════ 反射绑定启发式 ══════════════

        private sealed class ArgContext
        {
            public string AssetPath;
            public string FolderPath;
            public string MixerName;
            public readonly Dictionary<string, object> ByName =
                new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            public ArgContext With(string key, object value)
            {
                ByName[key] = value;
                return this;
            }
        }

        private static object InvokeFirstMatch(object target, string[] methodNames,
                                               Func<ArgContext, ArgContext> contextTuner,
                                               string step, List<string> log, bool optional = false)
        {
            Type type = target.GetType();
            var attempts = new List<string>();
            foreach (string methodName in methodNames)
            {
                foreach (MethodInfo method in type.GetMethods(
                             BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    if (method.Name != methodName) continue;
                    try
                    {
                        ArgContext ctx = contextTuner(new ArgContext());
                        object[] args = BindArguments(method, ctx);
                        object result = method.Invoke(target, args);
                        log.Add($"[拓扑] {step}:命中 {type.Name}::{method.Name}");
                        return result;
                    }
                    catch (Exception ex)
                    {
                        attempts.Add(method.Name + " → " + ex.GetBaseException().Message);
                    }
                }
            }
            if (optional)
            {
                log.Add($"[拓扑] {step}:无可用方法(可选步,跳过)—— " +
                        string.Join(" | ", attempts));
                return null;
            }
            throw new InvalidOperationException(
                $"[拓扑] {step} 失败,候选全灭:\n" + string.Join("\n", attempts));
        }

        /// <summary>按**参数名 + 类型**绑定实参:名字命中 ByName 表 → 取表值;
        /// 字符串兜底 path/folder/mixerName;已知 controller/group 类型走上下文;数值/bool 默认。</summary>
        private static object[] BindArguments(MethodInfo method, ArgContext ctx)
        {
            ParameterInfo[] parameters = method.GetParameters();
            var args = new object[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                ParameterInfo p = parameters[i];
                string name = (p.Name ?? "").ToLowerInvariant();

                if (ctx.ByName.TryGetValue(p.Name ?? "", out object named) ||
                    ctx.ByName.TryGetValue(name, out named))
                {
                    args[i] = named;
                    continue;
                }
                if (p.ParameterType == typeof(string))
                {
                    if (name.Contains("path") || name.Contains("folder")) args[i] = ctx.FolderPath;
                    else if (name.Contains("name")) args[i] = ctx.MixerName;
                    else args[i] = ctx.MixerName;
                    continue;
                }
                if (p.ParameterType == typeof(int)) { args[i] = 0; continue; }
                if (p.ParameterType == typeof(float)) { args[i] = 0f; continue; }
                if (p.ParameterType == typeof(double)) { args[i] = 0d; continue; }
                if (p.ParameterType == typeof(bool)) { args[i] = false; continue; }
                if (p.ParameterType.IsInstanceOfType(ctx.GetType())) { args[i] = ctx; continue; }
                if (!p.ParameterType.IsValueType)
                {
                    args[i] = null;   // parent / mixer 等引用型:上下文没给就 null(根组)
                    continue;
                }
                throw new InvalidOperationException(
                    $"参数 {p.ParameterType.Name} {p.Name} 无绑定策略(值类型)—— 按 Discover 日志补");
            }
            return args;
        }

        private static object FindGroupByPath(object controller, string name, List<string> log)
        {
            // 兜底:走公开面 AudioMixer.FindMatchingGroups("")(经 controller 对象也适用)
            if (controller is AudioMixer mixer)
            {
                foreach (AudioMixerGroup group in mixer.FindMatchingGroups(""))
                    if (group != null && group.name == name)
                        return group;
            }
            return null;
        }

        // ══════════════ send / 捕获(SerializedObject 对齐点)══════════════

        private static bool TryAddSend(object sourceGroup, object targetGroup,
                                       MixerTopologyGates.MixerSendRegistration send, List<string> log)
        {
            // 首选内部方法(签名未知,按名字启发式)
            foreach (string methodName in new[] { "AddSend", "AddAudioMixerSend" })
            {
                MethodInfo method = sourceGroup.GetType().GetMethod(
                    methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (method == null) continue;
                try
                {
                    object[] args = BindArguments(method, new ArgContext()
                        .With("name", send.Name).With(send.Name, send.Name)
                        .With("target", targetGroup).With("sendTarget", targetGroup));
                    method.Invoke(sourceGroup, args);
                    log.Add($"[send] 内部方法命中 {methodName}({send.Name})");
                    return true;
                }
                catch (Exception ex)
                {
                    log.Add($"[send] {methodName} 失败 → {ex.GetBaseException().Message}");
                }
            }

            // 回退:SerializedObject(m_Sends 数组 —— 字段名 = 登记过的对齐点)
            if (sourceGroup is UnityEngine.Object sourceAsset)
            {
                try
                {
                    var so = new SerializedObject(sourceAsset);
                    SerializedProperty sends = so.FindProperty("m_Sends");
                    if (sends != null && sends.isArray)
                    {
                        sends.arraySize++;
                        SerializedProperty element = sends.GetArrayElementAtIndex(sends.arraySize - 1);
                        SetFirstField(element, new[] { "sendName", "m_SendName", "name" }, send.Name);
                        SetObjectRef(element, new[] { "sendTarget", "m_SendTarget", "target" },
                            targetGroup as UnityEngine.Object);
                        so.ApplyModifiedPropertiesWithoutUndo();
                        log.Add($"[send] SerializedObject 路径写入 {send.Name}");
                        return true;
                    }
                    log.Add("[send] ⚠️ m_Sends 属性未找到(对齐点:按真格式改字段名)");
                }
                catch (Exception ex)
                {
                    log.Add("[send] SerializedObject 失败 → " + ex.GetBaseException().Message);
                }
            }
            return false;
        }

        // ── 快照捕获(八轮):native SetValueForVolume —— **Unity 自己序列化 m_FloatValues**
        //    (键形态零猜测;手写 YAML 路线作废 —— 旧 m_ValueMap 版 ApplySnapshotCaptures 已删)──

        /// <summary>捕获种子值(dB)。**种子值,用户自己调**(项目铁律:数值归数值轮)——
        /// 中性可辨识:压 6 dB(听得出让位、不夸张)。</summary>
        private const float CaptureSeedDb = -6f;

        /// <summary>捕获计划(**闭集常量**,别散在循环里;组名 = 已建出的 duck 组):
        /// <c>Default</c> / <c>VRComfort</c> **刻意不写**(空 = 默认电平天然正确 / P1b 占位);
        /// <c>StethoscopeFocus</c> **只**压 duck_ambience + duck_music(F7=甲「只压
        /// Ambience/Music」,别多压);<c>DialogueFocus</c> 压 duck_ambience + duck_music +
        /// duck_sfx(语音让位,**不碰** duck_voice);<c>Paused</c> 压全部 7 个 duck_*
        /// (全总线衰减)。数值 = <see cref="CaptureSeedDb"/> 种子。</summary>
        private static readonly IReadOnlyList<KeyValuePair<string, string[]>> SnapshotCapturePlan =
            new[]
            {
                new KeyValuePair<string, string[]>("StethoscopeFocus",
                    new[] { Duck("Ambience"), Duck("Music") }),
                new KeyValuePair<string, string[]>("DialogueFocus",
                    new[] { Duck("Ambience"), Duck("Music"), Duck("SFX") }),
                new KeyValuePair<string, string[]>("Paused",
                    new[] { Duck("Master"), Duck("Music"), Duck("Ambience"), Duck("Voice"),
                            Duck("SFX"), Duck("Stethoscope"), Duck("UICue") }),
            };

        /// <summary>native 捕获写入(签名照抄 Discover):<c>AudioMixerGroupController
        /// .SetValueForVolume(AudioMixerController, AudioMixerSnapshotController, Single)</c>
        /// (组实例上)。**任一环失败 = 硬 throw** —— 捕获缺失 = F7=甲 资产半边缺失,
        /// 不静默、不改手写 YAML 路线(batch 下该 API 不可用时须立刻上报裁决)。</summary>
        private static void ApplySnapshotCapturesNative(AudioMixer mixer, List<string> log)
        {
            foreach (KeyValuePair<string, string[]> plan in SnapshotCapturePlan)
            {
                AudioMixerSnapshot snapshot = mixer.FindSnapshot(plan.Key);
                if (snapshot == null)
                    throw new InvalidOperationException($"[捕获] 快照「{plan.Key}」缺失 —— 捕获不可写");

                foreach (string duckName in plan.Value)
                {
                    AudioMixerGroup duckGroup = null;
                    foreach (AudioMixerGroup candidate in mixer.FindMatchingGroups(duckName))
                        if (candidate != null && candidate.name == duckName)
                        { duckGroup = candidate; break; }
                    if (duckGroup == null)
                        throw new InvalidOperationException($"[捕获] duck 组「{duckName}」缺失 —— 捕获不可写");

                    try
                    {
                        InvokeSig(duckGroup, "SetValueForVolume", mixer, snapshot, CaptureSeedDb);
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException(
                            $"[捕获] SetValueForVolume({duckName}, {plan.Key}, {CaptureSeedDb}) 失败 —— " +
                            "若 batch 下该 API 不可用,**立刻报告**(勿改手写 YAML 路线):" +
                            ex.GetBaseException().Message);
                    }
                    log.Add($"[捕获] {plan.Key} ← {duckName} = {CaptureSeedDb} dB(种子,用户调)");
                }
            }
        }

        /// <summary>读回断言(SaveAssets 之后):计划内快照的 <c>m_FloatValues</c> 必须非空 ——
        /// 空 = <c>SetValueForVolume</c> 未落盘 ⇒ **硬 throw**,不静默。
        /// <c>Default</c> / <c>VRComfort</c> 刻意不检(空是正确态)。</summary>
        private static void VerifyCapturesWritten(List<string> log)
        {
            string absolute = Path.GetFullPath(Path.Combine(Application.dataPath, "..", MixerAssetPath));
            if (!File.Exists(absolute))
                throw new InvalidOperationException("[捕获] 资产文件缺失,无法断言 m_FloatValues:" + absolute);
            string text = File.ReadAllText(absolute);

            foreach (KeyValuePair<string, string[]> plan in SnapshotCapturePlan)
            {
                int nameKey = text.IndexOf("m_Name: " + plan.Key + "\n", StringComparison.Ordinal);
                if (nameKey < 0)
                    nameKey = text.IndexOf("m_Name: " + plan.Key + "\r", StringComparison.Ordinal);
                if (nameKey < 0)
                    throw new InvalidOperationException($"[捕获] 文本内找不到快照「{plan.Key}」—— 断言不可执行");
                int valueKey = text.IndexOf("m_FloatValues:", nameKey, StringComparison.Ordinal);
                int docEnd = text.IndexOf("--- ", nameKey, StringComparison.Ordinal);
                if (valueKey < 0 || (docEnd >= 0 && valueKey > docEnd))
                    throw new InvalidOperationException($"[捕获] 快照「{plan.Key}」无 m_FloatValues 行");
                // 两种真形态(2026-09-26 实测):
                //   ① 内联 `m_FloatValues: {}` 或 `{hash: dB, ...}`
                //   ② **块式** `m_FloatValues:` 换行后缩进 `hash: dB` 逐条 ← Unity 实际写出的形态
                // 原实现只找 `{`/`}`,块式会一路找到下一行 `m_TransitionOverrides: {}` 的花括号 ⇒
                // inner 为空 ⇒ 误报「仍为空」(捕获其实已落盘)。
                int lineEnd = text.IndexOf('\n', valueKey);
                string sameLine = text.Substring(
                    valueKey, (lineEnd < 0 ? text.Length : lineEnd) - valueKey);
                bool hasEntries;
                int colon = sameLine.IndexOf("m_FloatValues:", StringComparison.Ordinal);
                string inline = colon >= 0 ? sameLine.Substring(colon + "m_FloatValues:".Length) : "";
                if (inline.IndexOf('{') >= 0)
                {
                    int open = text.IndexOf('{', valueKey);
                    int close = text.IndexOf('}', valueKey);
                    if (open < 0 || close <= open)
                        throw new InvalidOperationException(
                            $"[捕获] 快照「{plan.Key}」m_FloatValues 括号不可解析");
                    hasEntries = text.Substring(open + 1, close - open - 1).Trim().Length > 0;
                }
                else
                {
                    // 块式:数 valueKey 之后、下一处 `m_` 键或文档分隔之前的 `xxx: 数字` 行
                    hasEntries = false;
                    int scan = lineEnd < 0 ? text.Length : lineEnd + 1;
                    while (scan < text.Length)
                    {
                        int next = text.IndexOf('\n', scan);
                        string row = text.Substring(
                            scan, (next < 0 ? text.Length : next) - scan);
                        string t = row.Trim();
                        if (t.Length == 0) break;
                        if (t.StartsWith("m_", StringComparison.Ordinal) ||
                            t.StartsWith("--- ", StringComparison.Ordinal))
                            break;
                        int colonIdx = t.IndexOf(':');
                        if (colonIdx > 0 && colonIdx < t.Length - 1)
                        { hasEntries = true; break; }
                        scan = next < 0 ? text.Length : next + 1;
                    }
                }
                if (!hasEntries)
                    throw new InvalidOperationException(
                        $"[捕获] 快照「{plan.Key}」m_FloatValues 仍为空 —— SetValueForVolume 未落盘" +
                        "(batch 行为?立刻报告,勿手写 YAML)");
            }
            log.Add("[捕获] 读回断言:计划内三快照 m_FloatValues 均非空");
        }

        private static SerializedProperty FindChild(SerializedProperty parent, string[] candidates)
        {
            SerializedProperty iterator = parent.Copy();
            SerializedProperty end = parent.GetEndProperty();
            if (!iterator.Next(true)) return null;
            while (!SerializedProperty.EqualContents(iterator, end))
            {
                foreach (string candidate in candidates)
                    if (iterator.name == candidate)
                        return iterator;
                if (!iterator.Next(false)) break;
            }
            return null;
        }

        private static void SetFirstField(SerializedProperty element, string[] candidates, string value)
        {
            foreach (string candidate in candidates)
            {
                SerializedProperty field = FindChild(element, new[] { candidate });
                if (field == null) continue;
                field.stringValue = value;
                return;
            }
        }

        private static void SetObjectRef(SerializedProperty element, string[] candidates,
                                          UnityEngine.Object value)
        {
            foreach (string candidate in candidates)
            {
                SerializedProperty field = FindChild(element, new[] { candidate });
                if (field == null) continue;
                field.objectReferenceValue = value;
                return;
            }
        }

        private static string Duck(string bus)
            => DaYiJingCheng.Gameplay.Presentation.Audio.MixerRegistry.DuckGroupForBus(bus);
    }
}
