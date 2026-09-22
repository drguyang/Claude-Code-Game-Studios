# U1 spike 批 —— 落盘卡 + 【桌面】run-book

> 批次:U1(承 U0 结案 2026-09-22)· 状态:**F7 载荷腿【超算】已落盘,桌面验收待做;引擎腿已脚本化(§4/§5),桌面按菜单+Test Runner 执行**
> 权威来源:ADR-023 §Validation(S1–S7)· ADR-012 F7(2026-09-21 承 RC-4 降级)·
> ADR-013 假设 6(§6.6 半可信)· ADR-006 §三(舍入)· D-1/D-2(本批裁定)

## 0. 裁定记录(本批)

| # | 问题 | 裁定 | 影响面 |
|---|------|------|--------|
| **D-1** | 多输入 `SplitMix64` 折叠形 | **乙 · 链式 avalanche**(state=首输入;每后续 w: `state=Avalanche(state+w)`;终态再 Avalanche 一次;mod 2⁶⁴ 回绕) | 一经落码即锁:改折叠 = 全平台重签 + 9/52/7a 哈希语义作废 |
| **D-2** | `FixMul` 右移步舍入模式 | **消解,无需另裁** —— ADR-006 §三「全部舍入 ROUND_HALF_AWAY_FROM_ZERO」已钉死本步,与 `FixParse` 同模式 | — |

D-1 首两输入可交换(a+b 交换 + 同步 avalanche),第三输入起有序 —— 交换性只是黄金测试探针性质,**消费方不得依赖**。

## 1. F7 载荷腿(【超算】已落盘 · 2026-09-22)

### 1.1 落盘清单

| 文件 | 动作 | 内容 |
|------|------|------|
| `unity/Assets/Sim.Contracts/SplitMix64.cs` | 新增 | `Gamma` / `Avalanche` / `NextValue` / `Fold` / `Hash(a,b|c|d)` / `HashTagged(a,tag,b|c)` / 私有 `FoldTag`(ASCII + `[长度]` + 每 8B 大端补零块)—— 全 `ulong` + `unchecked` |
| `unity/Assets/Sim.Contracts/Fix.cs` | 修改 | 新增 `operator *` + `MulRaw(long,long)`:符号分离 → 四路无符号 64×64→128 量积 → `(hi<<48)\|(lo>>16)` 回 Q16.16 → half-away(bit15)→ 三段域守卫(`hi>>16` / 舍入进位 / `SignBound`)→ 一次回符号 |
| `unity/Assets/Tests/EditMode/Sim/golden_hash_v1_test.cs` | 新增 | `GoldenHashV1Test`:Avalanche 5 / 流 6+回绕 2 / fold 2+3+4 / HashTagged 6 / FixMul 25+溢出 3+operator 1 / S7 哈希半边 1 = **43 条测试行** |

### 1.2 落点理由

- **`SplitMix64` 住 `Sim.Contracts` 而非 `Sim`**:S7 表现层生成者引用不到 `Sim`(ADR-016 门 F 白名单);哈希是「三源之纯函数」,两侧都要跑。门 A 安全:纯 BCL,零引擎引用。
- **`FixMul` 中间全程 `ulong`**:F7 降级的落点 —— 有符号溢出 UB **结构性不存在**(C# 与 IL2CPP C++ 两侧对无符号溢出同为定义性 mod 2⁶⁴)。
- **溢出 = `OverflowException`**(非静默回绕):AC-4「溢出即测试失败」的运行期形态。

### 1.3 【超算】自查(2026-09-22)

黄金期望值由**独立 Python 参考实现**算出(与 C# 实现零共享代码),随后对「逐行模拟现行 C# 文本」的第二实现全量对拍:

- FixMul:25 条合法向量 + 3 条溢出路径 —— **ALL OK**
- SplitMix64:全部 Avalanche / 流 / fold / HashTagged / S7 向量 —— **ALL OK**

⚠️ **集群无 C# 编译器**(mono/csc/dotnet 均无)—— 编译与测试执行判定**唯一归【桌面】**。
⚠️ 黄金值**禁就地改**(ADR-012 版本化刷新:变 = 新 golden-vN + 变更日志 + 全平台重签)。

### 1.4 EditMode 基线变化(回归注记)

| | 数量 |
|---|---|
| 既有基线(b5 迁 `FixParse` 后确认) | **16 绿** |
| 本批新增(`GoldenHashV1Test`,35 TestCase 行 + 8 Test 方法) | **+43** |
| **本批后期望总数** | **59 绿** |

三门(Assembly Gates)判据不变,仍须全过。

## 2. 【桌面】F7 验收 run-book(先做)

1. `git pull`(应到含本卡的 commit;`Fix.cs` 修改 + `SplitMix64.cs` / `golden_hash_v1_test.cs` / 本卡共 4 个文件变更)
2. 打开 Unity → 等它为两支新 `.cs` 生成 `.meta`
3. Console 零编译错误(硬判据)
4. 菜单 `DaYi/Validation/Run Assembly Gates` → 三门全过
5. Test Runner → EditMode → **59 绿**(16 旧 + 43 新;任一红 = 实现漂移,**回报红的测试名**,勿改期望值)
6. 回传 meta:`SplitMix64.cs.meta` + `SplitMix64` 无(同文件)+ `golden_hash_v1_test.cs.meta` 共 2 支 → push

## 3. 引擎腿逐条裁定(范围决策)

| Spike | 本批裁定 | 理由 / 触发条件 |
|-------|---------|----------------|
| **S1** Addressables Additive 加载/卸载 | ✅ **本批执行**(§4.1) | ADR-023 Accepted 硬前置(S1/S3/S4 三选一不可);参考件载形状未验行为 |
| **S3** `UnloadSceneAsync` 不销毁 `InstantiateAsync` 产物 + bundle refcount | ✅ **本批执行**(§4.2) | ⑤ 拆序六步存在的全部理由;含 2026-09-21 S-4 补强(bundle refcount 归零) |
| **S4** 场景 chunk vs SetActive 成本 | ✅ **本批执行**(§4.3) | S1/S3/S4 同属 Accepted 硬前置 |
| **S2** World.unity 零 gameplay 对象扫描 + RC-6 相机/AudioListener 增列 | ⏸ **延后**(触发:首个 World/MainMenu 场景落地) | 现只有 `Boot.unity`,扫描无对象可扫;归「场景落地 + gates 扩员」批(与残留 R-2 同族) |
| **S5** Renderer Feature 触发条款 | 💤 **休眠** | 仅当 ADR-013 假设 6 spike **失败**时才需验「UI 兜底不够、须 RF」;假设 6 结果未知前无从谈起 |
| **S6** 菜单时钟(`Time.unscaledTime` vs tick driver 停机) | ⏸ **延后**(触发:`ITickProvider` 实现落地) | 现无 tick driver,两态行为无从测;归 tick driver 实现批 |
| **S7** 确定性偏移夹具 | 🟡 **哈希半边已落**(§1.1 S7 测试);**全夹具延后**(触发:ADR-022 逻辑层 + 可走性 C2 数据) | 「无对象落入不可走格」依赖关卡逻辑层,尚不存在;哈希半边 = 现在唯一可判的半边,已测 |
| **ADR-013 假设 6** 手柄焦点导航原型 | ✅ **本批执行**(§5,独立桌面腿) | ADR-013 定为 P0 早期、两天量级;失败 = 走自实现焦点算法(接口不变),连带 S5 仍休眠与否的判定 |

## 4. 【桌面】引擎腿 run-book(S1 / S3 / S4)—— 已脚本化

> **2026-09-22 改**:原手工建场景/挂临时脚本步骤**全部脚本化**(用户裁定,见 §8)。
> **判据与回填目标不变** —— 本卡只改「怎么跑」,不改「判什么」。
>
> 脚本入库:
> - `unity/Assets/Editor.Tools.Spike/`(`U1SpikeSetup.cs` Setup/Teardown 菜单 + `U1FocusProbe.cs` 假设 6 探针 + `Editor.Tools.Spike.asmdef`)
> - `unity/Assets/Tests/PlayMode/u1_scene_spikes_test.cs`(S1/S3/S4 三条 PlayMode 测试)
> - 已联动改动:`PlayMode.asmdef` 补 `references`(Addressables + ResourceManager)·
>   `AssemblyGates.cs` Manifest 增 `Editor.Tools.Spike` · `.gitignore` 增 `unity/Assets/Scenes/Spike*`
>
> 临时资产(4 场景 + Cube prefab + 假设 6 装置)由 **Setup 菜单生成、Teardown 菜单清理**,
> 已被 `.gitignore` 拦下 —— **永不提交**。前置:Addressables 包已在 `manifest.json`(`2.10.3`),
> 首次 Addressables 初始化由 Setup 菜单自动完成(无需手动开 Groups 窗口)。

### 4.0 一键步骤(取代原 4.1/4.2/4.3 的手工建场景步骤)

1. `git pull` → 开 Unity → 等编译(Console 零错误)→ 回传新增 `.meta`
2. 菜单 **`DaYi/Spike/Setup U1 Spikes`**(自动:建 4 个临时场景 + Cube prefab + 假设 6 场景
   + 标 Addressable + 尝试切 Play Mode 到 Existing Build + `BuildPlayerContent`;日志看 Console `[U1]` 前缀)
3. Test Runner → **PlayMode** → 跑 `U1SceneSpikesTest` 的三条测试(S1 / S3 / S4)
4. 结果**自动写** `unity/Logs/u1_spike_results.txt`(每行同时打进 Console `[U1-S1]`/`[U1-S3]`/`[U1-S4]`)
   —— 把该文件内容贴回本卡 §6 对应行即可(或整文件贴我)
5. 菜单 **`DaYi/Spike/Teardown U1 Spikes`** 清临时资产
   (AddressableAssetsData/ **保留、不要提交**,归 ADR-014 正式批;结果文件保留)

测试是**执行装置不是黄金断言**:绿 = 装置跑通;存活方向/毫秒数/bundle 计数**不作 Assert**,
任一方向都是 spike 发现,写进结果文件供 §6 回填(详见测试文件头注)。

### 4.1 S1 —— Additive 场景加载/卸载行为

**跑法**:已由 §4.0 第 3 步覆盖(测试 `test_s1_additive_load_unload_logs_ms`)。

**判据**(不变):6.3 实测可用作菜单/世界换入换出;记录:加载毫秒、卸载毫秒、Console 是否有
Addressables 异常(参考件 `plugins/addressables.md:263-276` 载形状,ADR-014 §五 注 6.2+ 抛异常
行为一并观察)。→ 结果行 `[U1-S1] cold_load_ms=… warm_load_ms=… unload_ms=…`

### 4.2 S3 —— `UnloadSceneAsync` 是否销毁 `InstantiateAsync` 产物(+ refcount)

**跑法**:已由 §4.0 第 3 步覆盖(测试 `test_s3_instance_survival_and_refcount_logs`)。
测试对每个判据做**双亲代变体**(外部 Holder + 场景内 Marker 亲代),两个方向都记录 —— 供 §6 判读,
不作 Assert。

**判据**(不变):
- **判据 1**:实例在 `UnloadSceneAsync` 后**未被销毁**(⑤ 存在的前提;若被销毁 = 前提被推翻,
  回报 —— 按 ADR-023 附条件口径,第 6 步断言零成本保留、不改判,但须登记)。
- **判据 2(2026-09-21 S-4 补强)**:`Addressables.ReleaseInstance(instHandle)` 之后,bundle 引用计数
  **归零**。**注意顺序**:handle 全 Release 后 bundle 仍被引用是 Addressables 已知形态 ——
  这正是第 6 步运行期断言只查登记簿、查不到 bundle 层的原因,实测须证实该形态存在
  (存在 = S-4 补强有理)。测试用 `Resources.FindObjectsOfTypeAll<AssetBundle>().Length` 基线差分。
- **判据 3(可观测性)**:故意**漏 Release** 实例,记录泄漏的可观测信号(实例残留 / bundle
  refcount 不归零 / profiler 增长)—— 第 6 步断言要拦的就是这个,须确认「漏了是能被看见的」。

跑完 Teardown(§4.0 第 5 步),临时资产**勿 push**。

### 4.3 S4 —— chunk 级激活:Addressables 场景分块 vs 单场景 SetActive

**跑法**:已由 §4.0 第 3 步覆盖(测试 `test_s4_scene_switch_vs_setactive_logs_ms`)。
A 路 = 两个 Addressable 场景(`SpikeS4_A1`/`SpikeS4_A2`)load/unload 交替 ×20;
B 路 = 单场景双根(`RootA`/`RootB`)SetActive ×20。

**判据**(不变):⑥ 只裁「归属 = 系统 6」不裁机制 —— 本测产出**机制建议**(哪条加载/卸载成本
可接受),回填本卡 §6 结果表;不产生新 ADR。→ 结果行 `[U1-S4] A路 … B路 … A/B 量级比=…`

## 5. 【桌面】ADR-013 假设 6 spike —— 手柄焦点导航原型

> 独立于 S1/S3/S4,可并批或随后跑;量级 ~2 天(ADR-013 原估)。
> 目标:把「UI Toolkit 运行时手柄焦点导航**可用且质量达标**」从 ⚠️ 半可信变成实测结论。

### 5.1 最小装置 —— 已脚本化(Setup 菜单生成)

**跑法**:
1. §4.0 第 2 步的 Setup 菜单已生成场景 `Assets/Scenes/SpikeAssump6.unity`
   (EventSystem 仅挂 `InputSystemUIInputModule`,已绑 `InputSystem_Actions` +
   UIDocument + 3×3 UXML 按钮 + 焦点态 USS + `U1FocusProbe` 探针)。
2. 打开该场景 → 按 Play → 插手柄(或 Input System 的 Gamepad layout 模拟)。
3. 探针自动记 `[U1-A6]` 行到 Console:每次 nav 事件 / 焦点跳变,同帧 ≥2 次 = 疑似双触发
   (**同键双触发**是本 spike 头号失败 —— 探针自动抓,Play 停止时输出对账汇总)。
4. 手感四问仍须**人工**(~5 分钟,见 §5.2)。

### 5.2 判据(照抄 ADR-013 §Risks / §Migration)

| # | 判据 | 通过 | 失败动作 |
|---|------|------|---------|
| 1 | 方向键/摇杆 → 焦点移动,**每次输入恰好一次移动** | 无双触发 | 记录复现步骤;走「冻单一来源 + 焦点单栈门」复核 |
| 2 | `FocusController` 自动完成空间选邻居(不手写焦点算法) | 网格四向导航正确,边界环绕/截断行为可预期 | 质量不达预期 → **自实现焦点算法**(接口不变,ADR-013 已登记回退) |
| 3 | 手柄**没有指针**仍能完整操作全部控件(technical-preferences 硬约束) | 全部按钮可达、高亮可见(焦点态 USS) | 同上 |
| 4 | 与 ADR-011 对接:只用官方 `UI/Navigate`,不另绑焦点动作 | 单一来源,无同键双绑 | 冻单一来源 |

### 5.3 结果回填

- **通过** → ADR-013 假设 6 标「已实测」(回写轮改 `architecture-review §6.6` 引用处 + ADR-013 §Risks 该行);S5 **保持休眠**(不需要 Renderer Feature 兜底)。
- **失败** → 触发 ADR-023 ⑧ Renderer Feature 条款评估(**S5 随之激活**,§3 该行改判);ADR-013 走「自实现焦点算法」回退(接口不变);两条路径都须回报,不得静默降级。

## 6. 结果回报表(桌面跑完填)

> S1/S3/S4 数字来源:`unity/Logs/u1_spike_results.txt`(测试自动写)· Console `[U1-S1]`/`[U1-S3]`/`[U1-S4]`/`[U1-A6]` 行。

| Spike | 结论(可用/不可用/部分) | 关键数字/现象 | 回填目标 ADR |
|-------|------------------------|--------------|--------------|
| F7(59 绿) | | | ADR-012 §Validation F7 勾选 |
| S1 | | 加载 _ms / 卸载 _ms / 异常 | ADR-023 S1 勾选 |
| S3 | | 实测存活?refcount 归零?漏 Release 可观测? | ADR-023 S3 勾选 |
| S4 | | A 路 _ms vs B 路 _ms(量级) | ADR-023 S4 勾选(机制建议) |
| 假设 6 | | 双触发?导航质量? | ADR-013 §6.6 + S5 激活与否 |

## 7. 残留与后续(出本批)

| # | 项 | 归属 |
|---|----|----|
| R-1 | Sim.Codec 解码器本体 + `PayloadRef` blob 契约 + b5 小端 helper 迁移 | Sim.Codec 批 |
| R-2 | b4 白名单断言扩员(`VitalsDto`/`ClinicEnvDto`/`AudioCueDto` 反射扫描) | gates 扩员批(与 S2 同族) |
| R-3 | `member_set` 双语义(抄本 vs registry 冻结三元组) | 37 验收判 |
| R-4 | adr-008 §三 抄本降级注 | 回写轮 |
| R-5 | 45 铸造契约(`player_id` 铸造面) | 45 GDD 轮 |
| R-6(本批新) | S2 扫描器(零 gameplay + RC-6 相机增列) | 首个 World/MainMenu 场景落地批 |
| R-7(本批新) | S6 时钟源实测 | `ITickProvider` 实现批 |
| R-8(本批新) | S7 全夹具(可走性 C2 + 3 次重建抽查) | ADR-022 逻辑层落地批 |
| — | S5 | 触发:假设 6 失败 |
| — | b7 半(包引用随首个消费代码) | **部分落地(本批)**:`PlayMode.asmdef` + `Editor.Tools.Spike.asmdef` 已补 Addressables/ResourceManager/InputSystem 引用 —— spike 消费代码即首个消费方;剩余(业务运行期 `Gameplay.Presentation`/`Gameplay.UI` 的引用)仍归首个业务消费批 |

## 8. 已知偏离 / 勘误登记

- **kindgen 口径偏离**(承 b6,非本批新):ADR-024 设想 .NET 工具,本机无 SDK,现为 Python —— 断言逻辑不依赖引擎,偏离已登记于 `StreamRouting.g.cs` 产物头注。
- **测试装配 `noEngineReferences` 不写**(承 U0 卡 §1.8 ERRATUM):UTF 装配必引 `UnityEngine.TestRunner` ⇒ EditMode 纪律 D7 已降为评审级,本批不改变该口径。
- **S1/S3/S4 + 假设 6 装置脚本化**(2026-09-22 用户裁定,记忆 `feedback-scripted-spikes`):
  原手工建场景步骤改为 Setup 菜单 + PlayMode 测试,判据不变。
  临时资产改由 `.gitignore` 拦下(`unity/Assets/Scenes/Spike*`)而非跑完手动删 —— 更可靠。
  **仍归人工的只有**假设 6 的四问手感(~5 分钟手柄走查)。
- **Addressables 编辑期 API 未在集群验证**(本批新):`AddAssetEntry`/`ActivePlayModeDataBuilderIndex`/
  `InputSystemUIInputModule` 等按 `docs/engine-reference` 钉版形状书写,但集群无 Unity 编译器 ——
  **编译判定唯一归【桌面】**,失败则回报 Console 红行、按实际签名就地修(风险面)。
- 本卡不改任何既有 ADR 正文;勾选/回写在结果回报后由回写轮执行(借绿禁令:本卡发出时全部 spike 仍 `NOT-RUN`)。
