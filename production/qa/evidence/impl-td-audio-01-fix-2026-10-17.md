# 实施证据原件 —— `TD-audio-01b` / `01d` / `01e` 处置

> **对象**:`docs/tech-debt-register.md` §`TD-audio-01` 的三条未修面
> **日期**:2026-10-17
> **裁定出处**:用户 2026-10-17(承 story-015 代码评审原件
> `production/qa/evidence/review-s15-code-2026-10-17.md` 的笔 2 三路归属)
> **性质**:修复轮 —— 三条 WARNING 级债的**兑现**。本件不是评审(不产出新判定),
> 是「原判定 → 修复落点 → 验证命令」的**可证伪结账**。

---

## §一 对象与范围

笔 2 把评审的 7 条 WARNING 分三路,其中**路 ②** = W-4 / W-5 / W-7 并入
`TD-audio-01`(登记为 `01b` / `01c` / `01d`),外加 01c 实测**新发现**的 `01e`。
本次处置 `01b` / `01d` / `01e` 三条。

### ⚠️ 勘察先行发现:一条债的前提已被另一条债的修复改变

**`01a`(2026-10-17 已修)收窄了 `.gitignore`,把 `AddressableAssetsData/` 的组定义入库。**
这直接改变了 `01b` 与 `01c` 的前提:

| | 01a **之前** | 01a **之后** |
|---|---|---|
| 全新检出 | `AddressableAssetsData/` **不存在** | 组定义 / schema / `AddressableAssetSettings.asset` / `DefaultObject.asset` **全在** |
| `Settings` | `null` | 由入库文件解析(引用链完整,见 §四) |
| 01c 的模拟方式(「移走 `AddressableAssetsData/`」) | 等价于全新检出 | **不再等价** |

⇒ **不得照旧债条目字面修**:那会修一个已不存在的洞,而漏掉真正的新洞。

---

## §二 三笔裁定(用户 2026-10-17)

| 债 | 裁定 | 落点 |
|---|---|---|
| **01b**(自举) | **硬失败 + 指引恢复,不自举** | `DataBakeMenu.cs` |
| **01d**(判据弱于 AC) | **维持 GDD 口径 + 订正 TD 登记**,不改代码 | `docs/tech-debt-register.md` |
| **01e**(静默失败) | **同文件两处都修** | `DataBakeMenu.cs` |

**01b 裁定理由(逐字承用户选项)**:组定义已入库 ⇒ 「设置缺失」的含义由「没初始化过」
变为「入库文件被删/损坏」。`GetSettings(create:true)` 会建一份**只含默认组**的 settings,
与入库定义**分叉** —— 那是假绿(看着绿,组是错的),正是本项目最忌的失效模式。

**01e 范围**:裁定限于 `EnsureAudioGroup` 的**两处**分支;
`EnsureDataCoreGroup` 的同款两分支**不在本次面内**(已在其 doc 就地注记,见 §七)。

---

## §三 代码改动逐处

文件:`unity/Assets/Editor.Tools.Bake/DataBakeMenu.cs`

### 3.1 拆分可注入实现体

```
[MenuItem(...)] public static void EnsureAudioGroup()      // 菜单入口,取真设置
    → EnsureAudioGroupCore(AddressableAssetSettingsDefaultObject.Settings, "Assets/Audio")

public static void EnsureAudioGroupCore(AddressableAssetSettings settings, string audioDirRel)
```

**为何改名而不重载**(`EnsureAudioGroupCore` 而非 `EnsureAudioGroup(settings, dir)`):
既有 `test_wiring_bakeMenuEntriesExist` 用
`menu.GetMethod(name, BindingFlags.Public | BindingFlags.Static)` **按名**反射查菜单项存在性
(`pipeline_gate_test.cs:817-826`)。同名重载会让它抛 `AmbiguousMatchException`
—— 即**改生产码会打红一条与本次无关的既有测试**。

**形态出处**:承 `AudioAssetGroupGates.ValidateEntries`(纯谓词)+ `ValidateSettings`(真设置)
的「两半分离」形态 —— 负向夹具可注入,不需真环境。

### 3.2 两条硬失败(01b ① + 01e ①②)

| 分支 | 原行为 | 现行为 |
|---|---|---|
| `settings == null` | `Debug.LogError` + `return` | **`throw new InvalidOperationException`**,报文指向 `git checkout -- unity/Assets/AddressableAssetsData` |
| 零素材 | `Debug.LogWarning` + `return` | **`throw new InvalidOperationException`**,报文点名目录 |

**先验后改**:两条都在**任何写入之前**判定 ⇒ 失败时设置零改动。
这既是设计改进(不留半成品状态),也使夹具可安全注入**真设置**而不污染工程
Addressables 状态(见 §五 F2)。

⚠️ **行为变更(如实登记)**:原实现在素材为空时**会先把组建出来**再 `LogWarning`;
现在**先抛**,组不建。P0 工程 `Assets/Audio/` 有 5 个 `.wav`,空目录属异常态,此变更符合意图。

### 3.3 同批文案订正(01a 造成的事实性错误,零行为变更)

`01a` 使「`AddressableAssetsData/` 是机器生成物,**永不提交**」成为**假陈述**。四处订正:

| 位置 | 原文 | 现文 |
|---|---|---|
| 文件头 `:4-5` | 「永不提交…不手改、不入库」 | 组定义已入库;仍忽略的只有逐平台 `content_state.bin`(原文保留为口径沿革) |
| `EnsureAudioGroup` doc | 「机器生成物,永不提交」 | 「组定义已随仓库入库」+ 硬失败口径 |
| `EnsureDataCoreGroup` doc `:513` | 「永不提交」 | 同上 + **注记其两处静默返回尚未加固** |
| `EnsureDataCoreGroup` 日志 | 「(AddressableAssetsData 为机器生成物,永不提交)」 | 删该括注 |

---

## §四 `01d`:原判定的危害陈述证伪(不改代码)

### 4.1 原判定

> `ValidateEntries` (a) 半只断言「登记在**某个非 `data-core`** 组」,**不断言**「登记在 `audio` 组」
> ⇒ `.wav` 落在 `Default Local Group` 亦**判过**,**素材仍被打进主包**,破 AC-44-D5 意图。

### 4.2 证伪证据(实测)

`BuildPlayerContent` 的产物(`unity/Library/com.unity.addressables/aa/Linux/StandaloneLinux64/`):

```
defaultlocalgroup_scenes_all_3bd2fd285e9ce864b73bf0713b428fa2.bundle
audio_assets_all_183d25f36eea546c371a53af1de5a818.bundle
data-core_assets_all_f8acb18615cd2f9ccb6ca0508c6628e9.bundle
```

⇒ **每个组产出各自的 bundle**,`Default Local Group` 有自己的 `defaultlocalgroup_*.bundle`,
**不是主包**。原判定「素材仍被打进主包」这一危害陈述**不成立**。

### 4.3 判据语义另有明文

GDD `design/gdd/audio-system.md:1104-1105`:

> 判据 = **组区分**(素材 ∈ 非 `data-core` 组 ∧ `data-core` 零 `Assets/Audio/` 条目),
> **不是**「组叫什么名字」

⇒ W-4 要求的「断言登记在 `audio` 组」**与 GDD 明文冲突**。用户裁定维持 GDD 口径。

### 4.4 残余(如实登记,不隐藏)

`.wav` 落在 `Default Local Group` 时本门**判过**。这是 **GDD 明文裁定的范围**,
**不是缺陷** —— 但它是一个真实的判据边界,登记在 `TD-audio-01d` 行内,不隐藏。

---

## §五 夹具与证伪轮

### 5.1 新增夹具(`unity/Assets/Tests/EditMode/Audio/pipeline_gate_test.cs`)

| 测试 | 命中分支 | 判据 |
|---|---|---|
| `test_d5_menu_settingsMissing_throwsHard` | `settings == null` | 抛 `InvalidOperationException`;报文含 `git checkout`;**不得**含 `Window > Asset Management` |
| `test_d5_menu_noAssets_throwsHard` | 零素材 | 抛 `InvalidOperationException`;报文点名出错目录 |

**配套装配改动**:`unity/Assets/Tests/EditMode/EditMode.asmdef` 增 `Unity.Addressables` +
`Unity.Addressables.Editor`(夹具需直用 `AddressableAssetSettingsDefaultObject` /
`AddressableAssetSettings`)。**成例**:`PlayMode.asmdef` 早已引用 `Unity.Addressables` /
`Unity.ResourceManager`。装配门按**装配名**断言(`AssemblyGates.Manifest`),不管引用集。

### 5.2 证伪轮(S12-4 形态 · 判据 = 变异测试)

**纪律**:每例「改 → 跑 → 还原」**在同一命令内**完成;`trap … EXIT` 还原后做 SHA-256 逐字节校验。
脚本 = `/tmp/s18-falsify.sh`(仓外)。

| 例 | 停用的守卫 | 期望 | **实测** |
|---|---|---|---|
| **F1** | `settings == null ⇒ throw`(改回 `LogError` + `return`) | `test_d5_menu_settingsMissing_throwsHard` 红 | ✅ **红**(`total=54 passed=53 failed=1`,**只红该 1 条**) |
| **F2** | 零素材 `⇒ throw`(改回 `LogWarning` + `return`) | `test_d5_menu_noAssets_throwsHard` 红 | ✅ **红**(`total=54 passed=53 failed=1`,**只红该 1 条**) |

**还原校验**:两次均 `SHA-256(before) == SHA-256(after) == 084c74e7d743ca7a07fe86f77e27da37d868d9295bb4bb5923c5e35fbdf7c3e3`
⇒ **逐字节一致**,带 bug 的生产码未留在盘上。

> **判据说明**:两例各只红 1 条 ⇒ 夹具**精确判别**,无连带误报;
> 「仍绿」的情形未出现 ⇒ **无假绿夹具**。

---

## §六 回归

### 6.1 全量 EditMode

| | total | passed | failed | skipped | 出处 |
|---|---|---|---|---|---|
| **基线**(S14-6 加固前) | 3613 | 3568 | 0 | 45 | `unity/Logs/s14-6-final.xml` |
| **本批** | **3615** | **3570** | **0** | **45** | `unity/Logs/s18-final.xml` |
| 增量 | **+2** | **+2** | **0** | **0** | = 本次新增的 2 条夹具 |

⇒ **零回归**。增量恰等于新增夹具数。

### 6.2 ⚠️ 顺带发现:一处被误引的「回归基线」

`story-015` 的收口行与 `TD-audio-01c` 行引「回归 `unity/Logs/s15-final.xml` = `395/395/0`」。
实测该文件 **`total=395`** —— 那是 **audio 命名空间过滤集**,**不是全量**
(证据原件 `impl-s15-asset-content-2026-10-17.md:112` 自己声明了该口径,
但 story 行与 TD 登记引用时**漏了范围限定**)。

**性质**:口径标注缺失,**不改变任何判定**(395/395/0 本身为真),但会让读者
把过滤集误读为全量。**本件如实登记**,已在 `story-015` 加注。

### 6.3 过滤集(本次新增夹具所在)

`unity/Logs/s18-fix-filtered.xml` = **54/54/0**(改动前为 52)。

### 6.4 ✅ `01c` 真·全新 clone 复测(2026-10-17 补跑)

**跑法**:`git clone . <全新空目录>`(仓外 `/tmp/s18-freshclone-run-*`)。克隆态实测:

| 项 | 实测 |
|---|---|
| `Library/` | **不存在** ⇒ 真·全新检出 |
| `AddressableAssetsData/Linux/addressables_content_state.bin` | **不存在**(被忽略,符合预期) |
| `AssetGroups/{audio,data-core,Default Local Group}.asset` | **齐备** |
| 引用链 | `DefaultObject.asset` 的 `m_AddressableAssetSettingsGuid: 2c864c592004e1c729cd6c8ed9da4089` = `AddressableAssetSettings.asset.meta` 的 `guid` ✅ |

**测量一:门(决定性)**

```
unity test <clone>/unity --mode EditMode --filter PipelineGateTest --output clone-pipeline.xml
⇒ total=54 passed=54 failed=0 skipped=0        (real 1m17s)
```

其中 **`test_d5_realSettings_audioAssetsSeparateFromDataCore` = Passed**
⇒ `AddressableAssetSettingsDefaultObject.Settings` 在**真·全新检出**上**解析成功**
⇒ 评审原件 §3.3 记的「全新检出必红」**已被 `01a` 消除**。

**测量二:菜单项进程级端到端**(`unity build … --execute-method …DataBakeMenu.EnsureAudioGroup`)

| 场景 | 退出码 | 日志判定行 |
|---|---|---|
| 完好克隆 | **0** | `[大医精诚] audio 组就绪:5 个素材已入组并打标签(与 data-core 分家 —— AC-44-D5)。` |
| **移走 `AddressableAssetsData/`** | **6** | `InvalidOperationException: [大医精诚] Addressables 设置缺失 —— 组定义已随仓库入库…跑 git checkout -- unity/Assets/AddressableAssetsData 恢复;⚠️ 不要手建设置…` |

⇒ **`01e` 的进程级静默确已闭合**:同样输入下**旧行为退出码 = 0**
(`s17-freshclone-20261017-051600.log`),**现行为 = 6**。

**顺带结论**:`CreateGroup` 分支在全新检出上**不再被走到**(组已随仓库到达)
⇒ `01c` 的**原始关切**(CreateGroup + schema 首跑)因 `01a` 而**结构性消解**;
残留的 schema 定稿问题另立(见 §七 W-5②)。

⚠️ 克隆为**一次性**;主仓 `git status` 全程干净(实测)。

---

## §七 未及核查 / 仍悬(禁借绿)

- ~~**`01c` 真·全新 clone 复测 = 未跑**~~ ⇒ ✅ **2026-10-17 已补跑**,见 §6.4
  (真 `git clone` + 全量导入;门 `54/54/0`,`test_d5_realSettings_…` **Passed**;
  进程级 `EXIT=0` / `EXIT=6` 成对)。原模拟方式(移走 `AddressableAssetsData/`)已作废。
- ~~**W-5② `CreateGroup` schema 不复现 = 仍 ✗ 未修**~~ ⇒ **✅ 2026-10-17 已修(甲案)**。
  `EnsureAudioGroupCore` / `EnsureDataCoreGroupCore` 的 `CreateGroup` 分支**已删**,组缺失改**硬失败**
  + `git checkout -- unity/Assets/AddressableAssetsData` 恢复指引。**原前置「须先裁哪些 schema 值算定稿」
  就此消解** —— 既然不建组,默认值与入库值的差异**不再是本菜单需要知道的量**。
  判据:`test_d5_menu_groupMissing_throwsHard` / `test_dcore_menu_groupMissing_throwsHard`
  (先验后改:抛异常后 `settings.FindGroup(absent)` 仍为 null)。
- ~~**`EnsureDataCoreGroup` 的同款两静默分支 = 未修**~~ ⇒ **✅ 2026-10-17 已修**。拆出可注入的
  `EnsureDataCoreGroupCore(settings, cookedDirRel, groupName)`,**三处硬失败**(设置缺失 / 产物目录零
  `*.cooked.bytes` / 组缺失);且零产物扫描**提到任何写入之前**(先验后改 —— 原实现是「先建组、先打标签
  才 `LogWarning`」)。判据:`test_dcore_menu_{settingsMissing,noCooked,groupMissing}_throwsHard`。
- ~~**真 `AudioClip.loadState` 的 PlayMode 半边 = NOT-RUN**~~ ⇒ **✅ 2026-10-17 已跑**,见 **§十**。
- ~~**IL2CPP 三格矩阵 = NOT-RUN(本机仅 Mono)**~~ ⇒ **⚠️ 2026-10-17 订正:原措辞为假**,见 **§十一**。

---

## §八 复现命令

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios

# 0) 工程独占检查(Unity batch 需独占)
ps -eo pid,comm,args --no-headers | grep -iE "Editor/Unity|bee_backend" | grep -v claude.exe
ls unity/Temp/UnityLockfile 2>/dev/null || echo "无锁 ✅"

# 1) 过滤集(本次新增夹具所在)
unity test unity --mode EditMode --filter PipelineGateTest --output unity/Logs/s18-fix-filtered.xml
#    期望 total=54 passed=54 failed=0

# 2) 全量回归
unity test unity --mode EditMode --output unity/Logs/s18-final.xml
#    期望 total=3615 passed=3570 failed=0 skipped=45(基线 3613/3568/0/45)

# 3) 证伪轮(改 → 跑 → 还原 同命令内;还原后 SHA-256 校验)
bash /tmp/s18-falsify.sh
#    期望 F1/F2 各红 1 条;两次还原 SHA-256 = 084c74e7d743ca7a07fe86f77e27da37d868d9295bb4bb5923c5e35fbdf7c3e3

# 4) 01d 证伪证据(每组各自成 bundle)
ls unity/Library/com.unity.addressables/aa/Linux/StandaloneLinux64/*.bundle

# 5) 静态引用链
grep -m1 m_AddressableAssetSettingsGuid unity/Assets/AddressableAssetsData/DefaultObject.asset
grep -m1 '^guid:' unity/Assets/AddressableAssetsData/AddressableAssetSettings.asset.meta
#    两行 guid 须相同

# 6) 01c 真·全新 clone 复测(§6.4)—— 仓外一次性目录,勿在仓内跑
CLONE=/tmp/s18-freshclone-run-$(date +%H%M%S)/repo
mkdir -p "$(dirname "$CLONE")" && git clone . "$CLONE"
test -d "$CLONE/unity/Library" && echo "⚠️ 非全新(不该有 Library)" || echo "全新 ✅"
ls "$CLONE/unity/Assets/AddressableAssetsData/AssetGroups/"      # 三组定义须在

# 6a) 门
unity test "$CLONE/unity" --mode EditMode --filter PipelineGateTest --output /tmp/clone-pipeline.xml
#     期望 total=54 passed=54 failed=0(决定性:`test_d5_realSettings_…` Passed)

# 6b) 菜单项进程级端到端 —— 完好 ⇒ EXIT=0;移走 AddressableAssetsData/ ⇒ EXIT=6
# ⚠️ `--execute-method` 必须写**全命名空间**;写短名 `DataBakeMenu.EnsureAudioGroup` 会得到
#    `executeMethod class 'DataBakeMenu' could not be found.` + exit 1(2026-10-17 实测,
#    provenance `unity/Logs/s18-buildcontent-20261017.provenance.json` = outcome:failure/exitCode:1)。
unity build "$CLONE/unity" --target StandaloneLinux64 \
  --execute-method DaYiJingCheng.EditorTools.Bake.DataBakeMenu.EnsureAudioGroup \
  --log-file /tmp/clone-menu.log --no-tail
echo "EXIT=$?"
```

---

## §九 结论

| 债 | 判定 |
|---|---|
| **01b** | ✅ **已修**(硬失败 + 指引恢复;原「自举」方向经裁定**不采纳**) |
| **01d** | ✅ **结案 —— 原判定的危害陈述证伪**(维持 GDD 口径,零代码改动) |
| **01e** | ✅ **已修**(两处分支,证伪轮 2/2 全红,**进程级端到端 `EXIT=0`/`EXIT=6`**,零回归) |
| **01c** | ✅ **已复测**(真·全新 clone:门 `54/54/0`,`test_d5_realSettings_…` Passed ⇒ `Settings` 解析成功;**原关切因 `01a` 结构性消解**) |
| **W-5②** | ✅ **已修(补口轮 · 2026-10-17)** —— `CreateGroup` 分支删除,组缺失改硬失败(见 **§十**) |
| **01f** | ✅ **已解(补口轮新发现)** —— PlayMode 预建 catalog 陈旧致 9 条启动序红(见 **§十**) |

**回归**:`3615 / 3570 / 0 / 45`(基线 `3613 / 3568 / 0 / 45`,增量 = 新增 2 测)。
**补口轮回归**(2026-10-17 同日):EditMode **3622 / 3577 / 0 / 45**;PlayMode **189 / 189 / 0 / 0**
(基线 `s11-b6-playmode.xml` = 186/186/0/0)。详见 §十。
**末次全量复跑**(夹具加固**之后**,`unity/Logs/s18-4-final.xml`,`EXIT=0`):
`total=3622 passed=3577 failed=0 skipped=45` —— **与加固前逐位相同**,且 `s18-3-final.xml` 同值
⇒ 加固**零回归**(夹具改动只在失败路径上加清理,**不改任何通过路径**)。

---

## §十 补口轮(2026-10-17 · 用户「现在修」)—— W-5② / `EnsureDataCoreGroup` / PlayMode 半边 / `01f`

### 10.1 三项「仍悬」的处置

| 原「仍悬」项 | 处置 | 落点 |
|---|---|---|
| **W-5② `CreateGroup` schema 不复现** | ✅ **修(甲案)**:删掉重建路径,改**硬失败** + `git checkout` 指引 | `DataBakeMenu.EnsureAudioGroupCore` / `EnsureDataCoreGroupCore` |
| **`EnsureDataCoreGroup` 同款两静默分支** | ✅ **修**:拆可注入实现体,三处硬失败;零产物扫描**提到写入之前** | `DataBakeMenu.EnsureDataCoreGroupCore`(新) |
| **真 `AudioClip.loadState` 的 PlayMode 半边** | ✅ **跑**:落适配器 + PlayMode 件,**3/3 绿** | `Gameplay.Presentation/Audio/AudioClipLoadStateAdapter.cs`(新)+ `unity/Assets/Tests/PlayMode/Audio/stethoscope_preload_loadstate_test.cs`(新) |

**W-5② 的裁定口径(用户 2026-10-17 授权「现在修」,取甲)**:原前置是「**须先裁哪些 schema 值算定稿**」。
**该前置随甲案消解** —— 甲案**不建组**,所以「默认值 vs 入库值差多少」**不再是本菜单需要知道的量**。
「组缺失」的含义 = **入库文件被删/损坏**(承 01a),恢复动作 = `git checkout`,与分支 ①(`settings == null`)
**完全同构**。

### 10.2 夹具(EditMode `pipeline_gate_test.cs` 52 → **61 测**)

新增 4 条菜单硬失败夹具 + 4 条适配器夹具,**并删掉 1 条缺口哨兵**:

- `test_d5_menu_groupMissing_throwsHard` / `test_dcore_menu_groupMissing_throwsHard`
  —— 注入不存在的组名 ⇒ 必抛;且**抛后 `FindGroup(absent)` 仍为 `null`**(先验后改)
- `test_dcore_menu_settingsMissing_throwsHard` / `test_dcore_menu_noCooked_throwsHard`
- `test_preload_adapter_{mapsAllFourUnityLoadStates, nullClip_isFailedNotLoaded,
  nullResolver_isHardFailure, unresolvableCue_blocksGate}`
- ⚠️ **哨兵 `test_preload_realAudioClipLoadState_pendingPlayModeHalf` 已按其自身口径转成真断言**
  (它自陈的未覆盖面 = 「Unity 侧 `loadState` 枚举与 `ClipLoadState` 的适配器」)—— **不是被当回归修掉**。

### 10.3 证伪轮(BLOCKING · 改 → 跑 → 还原,**同一命令内**完成)

**形态**:6 处变异(M1–M6)一次施加,跑 `PipelineGateTest`;还原后 `sha256sum -c` **逐字节校验通过**。

| 变异 | 停用的守卫 | 期望转红的断言 | 实测 |
|---|---|---|---|
| **M1** | `EnsureAudioGroupCore` `if (group == null)` | `test_d5_menu_groupMissing_throwsHard` | ✅ **红** |
| **M2** | `EnsureDataCoreGroupCore` `if (guids.Count == 0)` | `test_dcore_menu_noCooked_throwsHard` | ✅ **红** |
| **M3** | `EnsureDataCoreGroupCore` `if (group == null)` | `test_dcore_menu_groupMissing_throwsHard` | ✅ **红** |
| **M4** | `AudioClipLoadStateAdapter.StateOf` 的 `null ⇒ Failed` | `test_preload_adapter_nullClip_isFailedNotLoaded` | ✅ **红**(连带 `unresolvableCue_blocksGate` 红 —— 同一路径,预期) |
| **M5** | `AudioClipLoadStateAdapter.Query` 的 `resolve == null` 守卫 | `test_preload_adapter_nullResolver_isHardFailure` | ✅ **红** |
| **M6** | `StethoscopePreloadGate.IsReady` 的「全 `Loaded` 才放行」 | (EditMode)5 条 preload 阻断类 | ✅ **红**(`unresolvableCue_blocksGate` / `failedState_blocks` / `handoverMux_gateBlocksEntry` / `notLoaded_blocks` / `partialLoaded_blocks`) |

**EditMode 实测**:`total=61 passed=51 failed=10 skipped=0` —— **10 条红全部可归因**(M1×1 / M2×1 / M3×1 /
M4×2 / M6×5),**零意外红**;D6 素材门 / D5 真设置门 / 烘焙确定性等 51 条**照绿**。
**还原**:`DataBakeMenu.cs` / `AudioClipLoadStateAdapter.cs` / `StethoscopePreloadGate.cs` 三件
`SHA-256 成功`。

**PlayMode 半边(M6,真 clip 环境)**:

```
total=3 passed=1 failed=2 skipped=0
    Passed: test_preload_adapter_matchesRealClipState          ← 纯映射,不触 IsReady ⇒ 正确保持绿
    Failed: test_preload_gateAgreesWithRealClipLoadState
    Failed: test_preload_realClip_transitionsUnloadedToLoaded_andGateFlips
```

⇒ **判别力成立**:门判据一停,两条门相关断言转红,而**不依赖门的适配器断言正确保持绿**
(证明红是**定向**的,不是整片塌)。还原 `SHA-256 成功`。

### 10.4 ⚠️ `01f`:PlayMode 启动序被预建 catalog 陈旧击穿(补口轮**新发现**)

**症状**:全量 PlayMode `total=189 passed=180 failed=9` —— 9 条**全是启动序/Addressables** 测试
(`CameraFollowWiringTest` / `CaseOpenedWiringTest` / `CompositionRootSmokeTest` / `EmergencyAttemptWiringTest`×2 /
`FallResetWiringTest` / `PatientAppearedWiringTest` / `PatientVisualWiringTest` /
`SkeuomorphicUI.ui_input_module_wiring_test`)。

**根因(日志栈逐帧)**:

```
AddressablesDataProvider:Preload()      (AddressablesDataProvider.cs:143)
  → EnsureAudioEvents()                 (:230)
    → FetchBytes()                      (:264)
      → UnityEngine.AddressableAssets.InvalidKeyException:
        No Location found for Key=audio_events.cooked.bytes
```

**两条独立事实**:

1. **入库设置 `m_ActivePlayerDataBuilderIndex: 2`**(= `BuildScriptPackedMode`)⇒ PlayMode 解析的是
   `unity/Library/com.unity.addressables/aa/Linux/catalog.bin` —— **机器本地的预建产物**(实测时间戳
   `05:17`,`strings` 含旧 5 条 cooked + 5 个 `.wav`,**不含**新烘的两条)。
2. **两条 audio 产物此前根本不存在**(补口轮实测):`audio_events.cooked.bytes` /
   `audio_settings_registry.cooked.bytes` **既不在盘上也不在 git 里**,`data-core` 组无 audio 条目,
   且**无任何构建钩子产出它们** —— 而 `AddressablesDataProvider.EnsureAudioEvents()` 与 `BootRoot` 的
   `Provider` 注入**都指向它们**。⇒ 已补跑 `BakeAudioEvents` / `BakeAudioSettingsRegistry` /
   `EnsureDataCoreGroup`(均 `EXIT=0`)。

**⚠️ 潜伏期(不是本轮引入的)**:`git log -L 136,145:…AddressablesDataProvider.cs` 显示
**`8d852fe`(2026-10-16,S14-6)** 把 `EnsureAudioEvents(); EnsureAudioSettings();` 加进了 `Preload()`;
而 **PlayMode 自 2026-10-15 起没跑过全量**(最后一次全绿 = `s11-b6-playmode.xml` = 186/186/0/0)
⇒ 炸点在库里躺了一天,**本轮发现并解掉**。

**处置**:新增菜单「大医精诚/数据管线/**构建 Addressables 内容(BuildPlayerContent)**」并跑之
(`DaYiJingCheng.EditorTools.Bake.DataBakeMenu.BuildAddressablesContent`,provenance
`unity/Logs/s18-buildcontent-20261017b.provenance.json` = `success/exitCode 0`)。
**验证**:catalog 重建(06:28,`strings` 现含 `audio_events.cooked.bytes` +
`audio_settings_registry.cooked.bytes`)⇒ 全量 PlayMode **`189 / 189 / 0 / 0`**
(基线 `186 / 186 / 0 / 0`;**+3 = 本件新增的 3 条 PlayMode 测**,**零回归**)。

**残留(未修,如实登记)**:`m_ActivePlayerDataBuilderIndex: 2` **仍在入库设置里** —— 直接改 YAML 会被
Unity batch 存盘**回滚**(实测:改 `2 → 0` 后跑一次即被写回 `2`)。⇒ **每次新检出 / 改过组条目后须重跑
该菜单**,否则 PlayMode 启动序红。**出路(未裁)**:改走 `BuildScriptFastMode`(AssetDatabase 直读,
免预建产物)或把「构建内容」并入 CI 前置步。**归 `TD-audio-01f`。**

### 10.5 ⚠️ 附带订正:§八 的 `--execute-method` 写短了

§八 原写 `--execute-method DataBakeMenu.EnsureAudioGroup`(短名)。**实测短名跑不通**:

```
executeMethod class 'DataBakeMenu' could not be found.
Argument was -executeMethod DataBakeMenu.BuildAddressablesContent
```

⇒ provenance `outcome: failure` / `exitCode: 1`。**能跑的是全命名空间**
(`DaYiJingCheng.EditorTools.Bake.DataBakeMenu.…`;实证:`s18-*.provenance.json` 三条 +
`s17-freshclone-20261017-051600.log:23-24`)。§八 已就地补全 FQN 并加警示。
**§6.4 的判定不受影响** —— 那次 clone 实测用的**就是全 FQN**(`s17-freshclone…:538` 有
`DaYiJingCheng.EditorTools.Bake.DataBakeMenu:EnsureAudioGroup` 栈帧),只是**写进 §八 时被缩写**。

### 10.6 ⚠️ 证伪轮**自身的副作用**:失败路径污染了已提交资产(已修 + 已加判据)

**现象**。首轮证伪(§10.3)在**源码还原之后**,于 `git status` 上留下一处**计划外的入库资产改动**:

```
 M unity/Assets/AddressableAssetsData/AddressableAssetSettings.asset
   +    - audio__no_such_group__
   +    - data-core__no_such_group__
```

**根因(逐行)**。变异 M1 / M3 停用「组缺失 ⇒ `throw`」后,生产码**落入分支 ④**:

```csharp
if (!settings.GetLabels().Contains(groupName))
    settings.AddLabel(groupName, postEvent: true);     // ← 先写标签
...
EditorUtility.SetDirty(settings);
AssetDatabase.SaveAssets();                            // ← 再落盘
```

标签写在**触碰 `group` 之前** ⇒ 即使随后因 `group == null` 抛 NRE,**标签已经落盘**。
⇒ **守卫一破,失败的测试会污染已提交的 `AddressableAssetSettings.asset`。**

**为什么首轮没抓到**:夹具的「无残留」断言只查**组**(`settings.FindGroup(absent) == null`)——
组确实没建;**建的是标签**。

**⚠️ 这不是新形态** —— `production/sprint-status.yaml:149` 已登记同型一次:
「证伪轮一触发 Unity 资产维护,静默改写 `audio.asset`(5 → 4 条)」。**这是第二次。**

**处置**:

1. **还原污染** —— `git checkout -- unity/Assets/AddressableAssetsData/AddressableAssetSettings.asset`。
   实测该文件 diff **仅此两行**,还原后标签表回到 `default / data-core / audio`。
2. **加固夹具** —— 新增包装器 `AssertThrowsAndLeaveNoTrace(settings, fakeGroupName, code, why)`:
   `try { Assert.Throws(…) } finally { if (含该标签) { RemoveLabel + SetDirty + SaveAssets } }`;
   并把「无残留」判据从「只查**组**」扩到「**查组 + 查标签**」。
   依据 `.claude/rules/test-standards.md`「Integration tests must clean up after themselves」。

### 10.7 加固后的复跑(判据 = ① 仍转红 ② **零 diff**)

```
前置         : git diff --quiet AddressableAssetSettings.asset  ⇒ OK(干净)
施加         : M1(EnsureAudioGroupCore 分支 ③)+ M3(EnsureDataCoreGroupCore 分支 ③)
跑           : PipelineGateTest ⇒ total=61 passed=59 failed=2 skipped=0
                RED: test_d5_menu_groupMissing_throwsHard
                RED: test_dcore_menu_groupMissing_throwsHard
判据二       : git diff AddressableAssetSettings.asset ⇒ ✅ **零 diff**(加固前此处命中两个假标签)
还原         : DataBakeMenu.cs SHA-256 逐字节校验通过
```

⇒ **判别力不变**(仍**恰 2 条**红,不多不少),且**失败路径不再留痕**。

---

## §十一 IL2CPP 三格登记订正(2026-10-17)

**订正对象**:本件 §七 原写「**IL2CPP 三格矩阵 = NOT-RUN(本机仅 Mono)**」——
**该措辞为假**。本机装有 IL2CPP 模块,矩阵的**两格早已在桌面跑过**。

**逐格实测状态**(证据原件 `production/qa/evidence/ac-29-il2cpp-crosscheck-2026-09-24.md`):

| 格 | 状态 | 实测 |
|---|---|---|
| **Linux-x64-Mono**(编辑器 PlayMode) | ✅ **2026-09-24** | 3/3 Passed |
| **Linux-x64-IL2CPP**(StandaloneLinux64 player) | ✅ **2026-09-24** | 3/3 Passed;与独立 Python 金标准 + Mono **三方 19/19 逐位相同** |
| **Mono player**(第三上下文) | ✅ **2026-09-25** | player 套件 15/15;四方逐位相同 |
| **F7 反汇编验无 FMA** | ✅ **2026-09-25** | `Fix.MulRaw` / `SplitMix64.Avalanche` 各 0 FMA、0 FP/SIMD |
| **Linux-ARM64-IL2CPP** | ❌ **NOT-RUN** | 本机无 ARM64 模块;ADR-012 §二 要求**原生 ARM64 runner**,qemu 已否决 |
| **Windows-x64 / Apple Silicon** | ❌ **NOT-RUN** | ADR-012 §二:发版前必跑,不进常驻 |
| **CI 常驻矩阵 job** | ❌ **未建** | `unity-tests.yml` 的 `il2cpp-determinism` = TODO 占位;EditMode/PlayMode 两 job 由 `UNITY_CLIENT_ID`/`UNITY_CLIENT_SECRET` secret 门控 |

⇒ **矩阵整体仍 NOT-RUN**(缺 ARM64 一格 + 无常驻 job + 无版本化刷新协议),
**但原因不是「本机只有 Mono」**。**登记订正落点**:本件 §七 · `docs/tech-debt-register.md` ·
ADR-012 §Validation · 6 份 audio story 的「NOT-RUN 如实登记」段
(story-007 / 008 / 009 / 010 / 011 / 012 —— 同一条样板句,同批订正)。

**⚠️ 本件的 IL2CPP 面不冒充已跑**:audio epic 自身**无跨平台判据**(44 侧零 sim 整数数学);
上表是**全案 ADR-012 矩阵**的状态,列在此处只为订正措辞,不声称 audio 侧新增了任何跨平台证据。

---

## §十二 悬项收口轮(2026-10-17 · 用户「现在修,有悬就不要停直到解决」)

**轮次口径**:上一轮(§十)留下了**四项仍悬**(登记在 `production/session-state/active.md`
的「仍悬」段与 §七)。本轮**逐项收口**,不留「归某轮」的悬置 —— 四项**全部结清**
(12.4 的裁剪候选已提交用户并获裁定,见该节)。

### 12.1 ✅ `01f` 残留 —— 且 **§10.4 的残留诊断本身是错的**

**§10.4 原写**(原文保留作沿革):

> **残留(未修)**:`m_ActivePlayerDataBuilderIndex: 2` **仍在入库设置里** … 出路:改走
> `BuildScriptFastMode` 或把「构建内容」并入 CI 前置步。

**该诊断错在把 Build 脚本当成了 Play Mode 脚本**。逐条实测:

| 事实 | 实测 |
|---|---|
| 入库的 `m_ActivePlayerDataBuilderIndex: 2` 是什么 | **Build 脚本** = `BuildScriptPackedMode`(「Default Build Script」)—— **正确值**,不该改 |
| Play Mode 脚本住哪 | **不在** `AddressableAssetSettings.asset` 里 —— 住 `ProjectConfigData.ActivePlayModeIndex`,落 **`unity/Library/AddressablesConfig.dat`**(**机器本地 · `.gitignore` 已忽略 · 不入库**;包源码 `ProjectConfigData.cs:390`) |
| 它的默认值 | **`0` = Use Asset Database** ⇒ **全新检出在结构上就没有这个问题** |
| 那本机为什么坏 | 实测本机值 = **`1`**(Use Existing Build)。元凶 = `U1SpikeSetup.ConfigurePlayMode()`(`unity/Assets/Editor.Tools.Spike/U1SpikeSetup.cs:444`)—— 它**刻意**切到 Existing Build 且**不复原** |
| 为什么从未出现在 git diff 里 | 因为它是 `ActivePlayModeDataBuilderIndex` 的**读取来源**在包内部,而**不是** `AddressableAssetSettings` 上的序列化字段 —— 包源码 `AddressableAssetSettings.cs` 的 `ActivePlayModeDataBuilderIndex` getter 直接读 `ProjectConfigData` |

⇒ **原「残留」的出路(改 YAML / 换 FastMode)是错的**:YAML 里那个字段与 Play Mode 无关,
改了才会真的把 Build 脚本改坏。

**修复(三件)**:

1. **`EnsureAssetDatabasePlayMode(AddressableAssetSettings)` 由 `private` 升为可注入 public**
   (`unity/Assets/Editor.Tools.Bake/DataBakeMenu.cs`),并**两半分离**:
   纯谓词 `FindAssetDatabaseBuilderIndex(settings)`(可注入 builder 名单直断,不必经真设置)
   + 真环境半。**先验后改**:找不到目标 builder 或已在目标 ⇒ **零写入**返回 `false`。
   ⚠️ `DataBuilders` 的声明类型是 `List<ScriptableObject>`(2.10.3 实读)⇒ 必须走
   `GetDataBuilder(i)` 取 `Name`(`IDataBuilder` 成员),承 `U1SpikeSetup` 同款警告。
2. **新菜单**「大医精诚/数据管线/**Play Mode 归位(Use Asset Database)**」
   (`ResetPlayModeToAssetDatabase`)—— 一键修机器侧漂移,**不必**重建 Addressables 内容(那是另一件事)。
3. **新增机器前置门** `test_playModeScript_isAssetDatabase` —— 断言真设置的
   `ActivePlayModeDataBuilderIndex == FindAssetDatabaseBuilderIndex(settings)`。
   漂移从此表现为**一条具名红**,而不是 9 条神秘红。

**验证(实证,非推断)**:

- 本机实跑菜单,日志实证:
  `[大医精诚] Play Mode → Use Asset Database(DataBuilder[0]「Use Asset Database (fastest)」;原「Use Existing Build (requires built groups)」)`
  —— **「原」串逐字等于 `1` 的语义名** ⇒ 同时钉住了「本机值确实是 1」与「修复确实生效」。
- 门通过:`test_playModeScript_isAssetDatabase` 在**当时**的 68 测过滤集中 **Passed**
  (同轮末 §12.5 自查再 +1 ⇒ 过滤集现为 **69**,该测仍 Passed;见 §14.1)。
- **反证**:`tools/falsify` 的 F3 变异(`FindAssetDatabaseBuilderIndex` 的 `-1` 返回改成 `0`)
  ⇒ `test_playModeScript_predicate_nullOrMiss_returnsMinusOne` **恰转红**(见 12.3)。

**残留(未修,如实登记)**:`U1SpikeSetup` 本身**仍未**在 Teardown 复原该设置 ——
本轮只在 `DataBakeMenu` 侧加了修复入口与门,**未改 spike**。改它须先裁「U1 spike 是否保留」,
**不属本债**。风险面因此收窄为:**跑过 U1 spike 的机器**(新机器默认值恰好是对的)。

### 12.2 ✅ 素材 Import Settings 定稿 —— 从「人肉复核」升为**可跑的门**

**先说清一个前提**:5 个 `.meta` **本来就符合** `OQ-44-2`(2026-10-17 用户裁定),
实测:

```
music_encounter_layer_loop.wav.meta          loadType:2   (Streaming)
sfx_breath_adventitious_fine.wav.meta        loadType:0   (DecompressOnLoad)
sfx_breath_base_loop_small.wav.meta          loadType:0
vo_patient_cough_damp_f1.wav.meta            loadType:0
vo_patient_cough_damp_m1.wav.meta            loadType:0
```

⇒ **缺的不是「定稿」,是「判据可执行」**。GDD §OQ-44-2 附(`design/gdd/audio-system.md:1208-1247`)
给的验证命令是一对 `python3 -c …` + `grep -o 'loadType: [0-9]*'` —— 那是**人肉复核**:
档位一旦漂移,**没有任何东西转红**。

**新增 `unity/Assets/Editor.Tools.Bake/AudioImportSettingsGate.cs`**:

- **纯谓词半边** `RequiredLoadType(fileName, durationSeconds, decodedBytes)` —— 零 I/O,夹具可注入。
  判据顺序(顺序本身承重):① `music_` **具名前缀** ⇒ Streaming;② 驻留 > 4 MB **或** 时长 ≥ 30 s
  ⇒ Streaming;③ 驻留 ≤ 512 KB **且** 时长 < 5 s ⇒ DecompressOnLoad;④ 其余 ⇒ CompressedInMemory。
- **真环境半边** `TryReadWaveInfo`(**从 wav 自己的头**算时长与解码后驻留)+ `TryReadLoadType`
  + `Validate(audioDir)` ⇒ 逐文件比对**应然 vs 现值**,保序输出违例。
- **前瞻性收益**:后续 **12 个 `vo_` 变体**素材落地时**自动纳入**,零改门。

**⚠️ 本件自身的一个实测 bug(如实登记,不是预防性设计)**:

首跑 `test_importSettings_realMetas_matchRequiredTier` ⇒ **5 个文件全部**报「data 块缺失」,
而文件明明是合法 RIFF。**根因 = `BinaryReader` 自带读前缓冲** —— 读几个字节就把底层流推远,
`FileStream.Position` **不再等于**逻辑读位 ⇒ 用它算 chunk 偏移**全部错位**。
**修法**:改为「读进 `byte[]` 手工走索引解析」(⛔ 也不用 `BinaryReader` + `MemoryStream`,
同样的缓冲语义)。修后 5/5 通过。

> **这次失败本身是对门纪律的确认** —— 它**报了违例**而不是**静默跳过**。
> 若当初写成「读不到就跳过」,这个 bug 会以「全绿」的形式留下来。

**⚠️ `music_*` 的口径张力(承 §附 的登记,不隐藏)**:按**数值判据**,
`music_encounter_layer_loop.wav`(23.3160 s / 2008 KB)落在「中档 ∩ 大档」的**重叠区**(512 KB–4 MB);
GDD 取 **Streaming**,理由 = `audio.md:217` 的语义档点名「大片段(**music**)」+ 乐层 `loop: true` 长驻。
⇒ 本件编码为**具名前缀规则**而非把阈值改成「> 512 KB 即 Streaming」(后者会**连带改判**一切,
且与 §附 表格不符)。**若后续 Music 素材改短(如 < 5 s 的 stinger),须重裁 GDD §附 那一行,再改本门。**

### 12.3 ✅ 通用防线 —— 从「一次性 `git diff --quiet` 建议」升为**可复用驱动**

原建议(未裁)是:证伪脚本跑测后统一加一步
`git diff --quiet -- unity/Assets/AddressableAssetsData unity/Assets/Audio`。**已落,且形态更强。**

**落点 `tools/falsify/`**(`unity_falsify.py` + `specs/` + `README.md`):

| 纪律 | 落点 | 相对原建议的差别 |
|---|---|---|
| ① 改 → 跑 → 还原**同进程**完成 | `try/finally`(崩溃也还原) | 原建议只字未提(那是 §10.6 事故的**直接成因**:人记得还原 ≠ 一定还原) |
| ② 还原后 **SHA-256 逐字节校验** | `restore_all()` | 原建议只查「没新 diff」,查不出**改错又改回** |
| ③ 跑测后**受保护路径指纹未变** | `guards_fingerprint()` = `git status --short` + `git diff` 合并文本 | 原建议的 `git diff --quiet` **看不见未跟踪文件**(`git status` 才看得见) |

**⚠️ 一处刻意的设计修正(首版实测教训)**:首版要求「受保护路径**起跑时必须干净**」⇒
干跑直接**退出码 2**。**那是错的** —— 开发中的仓本来就常带未提交的正当改动
(本例 `data-core.asset` 是 S14-6 的既有改动,5 → 7 条目)⇒ 该判据会让驱动在**绝大多数真实场景**
下不可用。**正确判据 = 前后指纹相同**:证伪轮不得**新增**任何痕迹。

**退出码语义**:`0` 全符合预期 / `1` 逃逸或红得比预期多(判别面不清)/ `2` **夹具过期**
(目标行原文对不上 —— 这不是判别力缺口,是 spec 该更新了)/ **`3` 还原或守卫失败(最严重,立即人工介入)**。

**⚠️ 与 `tools/kindgen/mutation_probe.py` 的分工(互补不替代)**:

| | `kindgen/mutation_probe.py` | `tools/falsify/unity_falsify.py` |
|---|---|---|
| 变异施加在 | **内存**(monkeypatch) | **工作树**(必须 —— 被测物是 Unity 编译产物) |
| 依赖 | 纯 Python + PyYAML | **Unity batch**(独占工程) |
| CI 常驻 | ✅ | ❌(桌面轮,需 Unity license) |
| 风险面 | 零(不碰工作树) | **有** ⇒ 故需 ①②③ 三条纪律 |

**本轮判据(自证 —— 驱动必须证明自己能用)**:

```
$ python3 tools/falsify/unity_falsify.py --spec tools/falsify/specs/audio-import-settings-and-playmode.json
```

```
ℹ️ 受保护路径起跑时已有改动(允许)—— 判据是**前后指纹相同**,不是「本来干净」:
   M unity/Assets/AddressableAssetsData/AssetGroups/data-core.asset
──── 施加 F1-musicPrefix:…/AudioImportSettingsGate.cs:75 ────  total=68 passed=65 failed=3
    RED: test_importSettings_musicPrefix_overridesNumericTier
    RED: test_importSettings_realMetas_matchRequiredTier
    RED: test_importSettings_requiredLoadType_matchesGddTable          ✅ 恰如预期
──── 施加 F2-smallTierInclusive:…/AudioImportSettingsGate.cs:83 ────  total=68 passed=67 failed=1
    RED: test_importSettings_boundaries_areInclusiveExclusiveAsSpecified   ✅ 恰如预期
──── 施加 F3-playModePredicateMinusOne:…/DataBakeMenu.cs:839 ────  total=68 passed=67 failed=1
    RED: test_playModeScript_predicate_nullOrMiss_returnsMinusOne      ✅ 恰如预期
──── 施加 F4-unreadableMetaSilentZero:…/AudioImportSettingsGate.cs:221 ────  total=68 passed=67 failed=1
    RED: test_importSettings_unreadableInput_isViolationNotSilentSkip  ✅ 恰如预期
──── 还原:SHA-256 逐字节校验通过 ────
──── 判据二:受保护路径**指纹未变**(证伪轮不留痕)────  ✅ 指纹未变
══ 证伪轮通过:4 个变异全部恰如预期 ══
FALSIFY_EXIT=0
```

四个变异**全部 `exactRed: true`**(红集须**恰好**等于预期 —— 只写「⊆」会让「变异打到了预期之外的
断言」蒙混过关)。**全部逐个施加、逐个还原,不叠变异**(叠变异会让红的归属无法拆解)。

> **⚠️ 同日追加第 5 个变异 F5**(`wavExtCaseSensitive`,见 **§12.5**)—— 该变异源于
> **对本轮新落生产件的人复核**,而非驱动自身。⇒ 与上表同 spec、同判据,复跑结果
> `total=69 passed=68 failed=1`,**恰转红**;SHA-256 与指纹双判据同样通过。
> **这条恰好示范了驱动的边界**:它是**语义**缺陷(漏扫),指纹守卫**看不见**。

**⚠️ 该驱动的边界(不隐藏)**:它守的是**路径**不是**语义** —— 若污染写到守卫路径**之外**,
指纹判据看不见 ⇒ **新证伪轮仍须逐例人判「该守卫能写到哪」**(§10.6 的教训正是「失败路径写的不是组,
是**标签**」)。另:它只保证**被检出**,**不保证不被写入** —— 防写入仍需夹具侧包装器
(`AssertThrowsAndLeaveNoTrace`),两者是**检出 vs 预防**的分工。

### 12.4 ⚠️ ARM64 腿 —— **升级为「不可达 + 依据」,用户裁定「维持三格原样,登记为长期未达」**

**探针实测结论**:`BuildTargetProbeCli` 在 Unity 6000.3.24f1 上列出 **46 个 BuildTarget,
含「arm」的 = 零**;`IsBuildTargetSupported(Standalone, …)` 下 **只有 `StandaloneLinux64` 受支持**
(即 **x64**)。⇒ ADR-012 §二:156「Linux-ARM64 是受支持目标」**为假**,
**F2 spike 的前置判据不成立,且不取决于 runner 架构**(原生 ARM64 runner 上同样没有目标可构建)。
**原件 + 可复跑命令见 §十三。**

**⚠️ 本轮不自行改判的理由**:三条候选出路(① 换别的 ARM64 面 / ② 矩阵降为两格 /
③ 跨 ISA 判据改挂发版前的 Apple Silicon)**都是对已 Accepted 裁决的裁剪** ⇒
承常设纪律「**里程碑完整性优先于跑通,裁剪只能显式提交用户裁**」与 ADR-017
「触发后须另开 ADR,不得以修订直接改判」同款口径。

**✅ 用户裁定(2026-10-17,三候选已完整提交)= 「维持三格原样,登记为长期未达」**

⇒ **ADR-012 §一「三格全免费常驻矩阵」裁决一字不改** —— 不降格、不换面、不改挂。
⇒ **同时如实登记其后果**(不隐藏):

| 后果 | 说明 |
|---|---|
| 第三格**长期不可达** | 工具链支持前不会变化 |
| 「三格全绿」复选框**结构上不可达成** | 它**不会被勾**,且这不是「尚未跑」,而是「**跑不了**」 |
| 后续 `/gate-check` **都会带这一条未达项** | 这是**刻意保留的可见性**,不是遗漏 ⇒ **不得因「长期红」而静默移除或降级为 advisory** |
| **解锁条件** | 引擎升级后重跑 §十三 探针;届时若出现受支持的 ARM64 目标,该格**自然解锁**,本条登记随之作废 |

**⚠️ 顺带兑现的另一半**:CI 常驻矩阵 job 由 TODO 占位**改为真 job**
(`.github/workflows/unity-tests.yml` 的 `il2cpp-determinism`)。**但本 job 从未跑过** ——
仓库未配置 `UNITY_CLIENT_ID`/`UNITY_CLIENT_SECRET` ⇒ 走 skip 分支(且 skip 时**响亮告警**:
「本 job **未跑**,不得记为绿」)。⇒ **常驻性仍未兑现,该条维持未勾。**
⚠️ 与 12.4 的 ARM64 腿**不同因**:这条是「缺凭据」,那条是「目标不存在」—— **勿合并读**。

### 12.5 ⚠️ 收口轮**自查抓到的新缺陷**:本门自带的大小写漏扫(已修 + 已反证)

**发现路径**:回答「还有悬的吗」时逐一复核**本轮自己新落的生产件**,发现
`AudioImportSettingsGate.Validate` 用了 `Directory.GetFiles(audioDir, "*.wav", TopDirectoryOnly)`
—— 而 story-017 的 **W-2** 正在为**同一条 pattern** 立件。⇒ **本门继承了同一缺陷**。

**先实测该 pattern 的行为(不凭记忆)** —— 独立 net8.0 探针 · Linux:

```
OS              = Linux
dir 内含        = lower.wav, UPPER.WAV, Mixed.Wav, note.txt
GetFiles("*.wav") = [lower.wav]        ← 另两个被静默漏扫
GetFiles("*.WAV") = [UPPER.WAV]
GetFiles("*.Wav") = [Mixed.Wav]
GetFiles("*")     = [Mixed.Wav, UPPER.WAV, lower.wav, note.txt]
```

⇒ **`Directory.GetFiles` 的 pattern 在 Linux 上区分大小写**(与 Windows 相反)。
**CI 平台正是 Linux(ADR-012)** ⇒ 素材批若交付 `.WAV` / `.Wav`,本门**少报违例** ——
正是「静默跳过」这一类失败模式,与本件 §12.2 反复强调的纪律相悖。

**修复(两半分离,承本文件既有惯例)**:

- 新增**纯谓词** `IsWavName(string fileName)` —— `Path.GetExtension(fileName)` 按
  `StringComparison.OrdinalIgnoreCase` 比对 `.wav`;零 I/O。
- `Validate` 改为枚举 `"*"` 后按该谓词过滤。
  ⛔ **不得**改成 `"*.WAV"` —— 那只是把分叉挪到另一边。
- **拆谓词的第二收益**:夹具可注入文件名直断三形态,**不必碰文件系统**
  (承本文件 `test_discipline_noFilesystemWrites` 的零写入纪律)。

**⚠️ 与 story-017 W-2 的关系(不合并记账)**:两者是**同一缺陷的两处** ——
`AudioAssetGroupGates.cs:197`(`AudioAssetsOnDisk`)同型,归 **story-017**(`AC-44-D5b`);
本处是本门自带的,**随本件一并修**。不同文件、不同 story ⇒ **两处分别记账**。

**夹具 + 反证**:

| 项 | 内容 |
|---|---|
| 夹具 | `test_importSettings_wavExtension_isCaseInsensitiveAcrossPlatforms` —— 三形态(`.wav`/`.WAV`/`.Wav`)+ 带目录路径**须识别**;`note.txt` / 无扩展名 / `x.wav.bak`(伪后缀)/ 空 / null **须拒绝** |
| 反证 | spec 新增 **F5-wavExtCaseSensitive**(`OrdinalIgnoreCase` → `Ordinal`)⇒ 该测**恰转红** |
| 形态 | **定义域收窄类**(S12-4 五形态之一)⇒ 反证**强制** |

**⚠️ 这条也顺带印证了 §12.3 的边界声明**:`tools/falsify` 的**指纹守卫看不见这个缺陷** ——
它是**语义**缺陷(漏扫),不是**路径**污染。⇒ 守卫与自查是**两件事**,不可互替。

---

## §十三 ARM64 探针原件与可复跑命令

**用途**:ADR-012 §Validation 的 §ARM64 探针裁定 的**证据原件**(承 ADR-012 §Risks
「引擎升级时须重跑矩阵」—— 本探针入库为常驻件,升级后重跑同一命令即可复核)。

**探针**:`unity/Assets/Editor.Tools.Bake/Cli/BuildTargetProbeCli.cs`
(**只读**,不写入任何资产;`Probe()` **恒以 0 退出** —— 它只报告,不判定)。

**可复跑命令**(⚠️ Unity batch **独占工程** ⇒ 先确认编辑器已关):

```bash
# 前置:确认无活 batch
pgrep -af "Unity" || true
test -e unity/Temp/UnityLockfile && echo "⚠️ 编辑器在跑,先关" || echo "✅ 可跑"

# 探针(全命名空间 FQN —— 短名跑不通,承 §10.5)
unity build unity --target StandaloneLinux64 \
  --execute-method DaYiJingCheng.EditorTools.Bake.Cli.BuildTargetProbeCli.Probe \
  --log-file unity/Logs/build-buildtargetprobe.log --no-tail

# 判决行
grep -nE "含「arm」|IsBuildTargetSupported|受支持|PlaybackEngines|ScriptingBackend" \
  unity/Logs/build-buildtargetprobe.log
```

**实测输出(2026-10-17 · Unity 6000.3.24f1)**:

```
Unity 6000.3.24f1 · LinuxEditor · x86-64
── ① BuildTarget 枚举共 46 项 ──
  含「arm」的目标:(无)
  含「linux」的目标:EmbeddedLinux, LinuxHeadlessSimulation,
                    StandaloneLinux, StandaloneLinux64, StandaloneLinuxUniversal
── ② IsBuildTargetSupported(BuildTargetGroup.Standalone, t) ──
  EmbeddedLinux           = ❌ 不受支持
  LinuxHeadlessSimulation = ❌ 不受支持
  StandaloneLinux         = ❌ 不受支持
  StandaloneLinux64       = ✅ 受支持          ← 唯一受支持的 Linux 目标 = x64
  StandaloneLinuxUniversal= ❌ 不受支持
── ③ PlaybackEngines/ ──
  LinuxStandaloneSupport └ il2cpp · mono
                         └ linux64_player_{development,nondevelopment}_{il2cpp,mono}
```

**交叉旁证**(不依赖本机 Unity):Unity Hub 模块目录(`releases-linux.json`,2020 / 2021 / 2022
LTS 三版)的 Linux 模块**只有** `linux-il2cpp`(x64)与 `linux-server`,**无任何 ARM64 模块**。

**⇒ 裁定**:矩阵第三格(`Linux-ARM64-IL2CPP`)在本 pinned 工具链上**不可达**;
**用户 2026-10-17 裁定 = 维持三格原样,登记为长期未达**(见 §12.4)。
**本件不声称该格已跑,亦未代为改判。**

---

## §十四 本轮落账清单(2026-10-17 悬项收口轮)

| # | 对象 | 落点 | 判据 |
|---|---|---|---|
| 1 | `01f` Play Mode 门 | `DataBakeMenu.cs`(`EnsureAssetDatabasePlayMode` public + `FindAssetDatabaseBuilderIndex` 纯谓词 + 菜单 `ResetPlayModeToAssetDatabase`) | 菜单实跑日志 `原「Use Existing Build (requires built groups)」`;门 `test_playModeScript_isAssetDatabase` Passed;反证 F3 恰红 |
| 2 | 素材 Import 门 | `AudioImportSettingsGate.cs`(**新**) | 纯谓词对 GDD §附 表 **5/5**;真环境 `test_importSettings_realMetas_matchRequiredTier` Passed;反证 F1(3 红)/ F2(1 红)/ F4(1 红) |
| 3 | 通用防线 | `tools/falsify/`(**新**:驱动 + spec + README) | 自证 `FALSIFY_EXIT=0`;4/4 恰如预期;SHA-256 + 指纹双判据通过 |
| 4 | ARM64 探针 | `Cli/BuildTargetProbeCli.cs`(**新**)+ ADR-012 §Validation §ARM64 探针裁定 | 46 目标零 arm;`StandaloneLinux64` 唯一受支持;**用户裁定「维持三格原样,登记为长期未达」** |
| 5 | 夹具扩容 | `pipeline_gate_test.cs` **61 → 68 测**(+7);**§12.5 自查后再 +1 ⇒ 69** | 过滤集 **69 / 69 / 0 / 0**;全量 EditMode 见下 **§14.1** |
| 5b | **§12.5 自查缺陷修复** | `AudioImportSettingsGate.IsWavName`(纯谓词)+ `Validate` 改 `"*"` 过滤 | 夹具 1 条;反证 **F5** 恰转红;**与 story-017 W-2 同型不同处,不合并记账** |
| 6 | CI job | `.github/workflows/unity-tests.yml` `il2cpp-determinism` TODO → 真 job | YAML 经 `yaml.safe_load` 校验;**从未跑过**(无 secret)⇒ **常驻性未兑现,不借绿** |

### 14.1 全量 EditMode 回归(⚠️ 一处**如实登记的时序缺口**,已当场补跑)

**⚠️ 先说缺口**:本轮新增 7 条测后,`unity/Logs/s18-4-final.xml` 的
**`3622 / 3577 / 0 / 45`**(基线 S14-5 = `3572 / 3526 / 0 / 46` ⇒ +50 / +51)是**加固前**的产物,
**早于这 7 条测** —— 那 7 条只在**过滤集**(`s19b-filtered.xml` = `68 / 68 / 0 / 0`)里验过。
⇒ **「全量含新测」这一格当时是空的**。若照抄 `3622` 当作「含新测的全量」,即为**借绿**。

**补跑**(本轮当场执行,不是留待下轮):

```
$ unity test unity --mode EditMode --output unity/Logs/s19-full-final.xml
```

| 运行 | 范围 | 结果 | 含新测? |
|---|---|---|---|
| `s18-4-final.xml`(06:46) | 全量 | `3622 / 3577 / 0 / 45` | ❌ 否 |
| `s19b-filtered.xml`(07:07) | `--filter PipelineGateTest` | `68 / 68 / 0 / 0` | ✅ 7 条 |
| `s19-full-final.xml` | 全量 | `3629 / 3584 / 0 / 45` | ✅ 7 条 |
| `s19c-filtered.xml` | `--filter PipelineGateTest` | `69 / 69 / 0 / 0` | ✅ 8 条(**含 §12.5 自查那 1 条**) |
| **`s19d-full-final.xml`** | **全量** | **`3630 / 3585 / 0 / 45`** | ✅ **8 条** |

**逐次对账**(实测,均 `TEST_EXIT=0`):

| 对账 | 值 |
|---|---|
| 本轮最终全量 | `3630 / 3585 / 0 / 45` |
| 上一全量(`s19-full-final.xml`) | `3629 / 3584 / 0 / 45` |
| **差** | **+1 / +1 / 0 / 0** —— 恰等于 §12.5 新增的 1 条 ✅ |
| 加固前全量(`s18-4-final.xml`) | `3622 / 3577 / 0 / 45` |
| **本轮累计差** | **+8 / +8 / 0 / 0** —— 恰等于本轮新增测数(7 + 1)✅ |
| 基线 S14-5 | `3572 / 3526 / 0 / 46` |
| 相对基线 | **+58 / +59**;`skipped` 46 → 45 = **既有变化**(见 §6.2),非本轮引入 |

**判据**:全量 `failed = 0` **且** `total - 前次 total = 新增测数`。
两条同时成立才叫「零回归」—— 只看 `failed=0` 会被「新测根本没被收集」蒙混过去
(本例正是:若不补跑,`3622` 那行会以「含新测」的姿态留下来)。

**⚠️ 一次改测就重跑一次全量** —— 本轮**跑了两次**全量(7 条后一次、§12.5 又 +1 条后一次),
**不是**沿用上一次的数字。这是本节判据的直接推论:等式右边随新增测数变化。

**本轮新增的 7 条测逐条对应**:

| 测试 | 钉住什么 |
|---|---|
| `test_playModeScript_isAssetDatabase` | **真设置**的 Play Mode 索引 == 谓词求得的索引(漂移 ⇒ 具名红) |
| `test_playModeScript_predicate_nullOrMiss_returnsMinusOne` | 纯谓词的**定义域**:null 设置 / 名单无匹配 ⇒ `-1`(非 `0`) |
| `test_importSettings_requiredLoadType_matchesGddTable` | 5 行 GDD §附 表逐行(**边界值豁免**:精确数字即判据本身) |
| `test_importSettings_boundaries_areInclusiveExclusiveAsSpecified` | 512 KB / 5 s **闭**、4 MB / 30 s **开** —— 阈值边界形态 |
| `test_importSettings_musicPrefix_overridesNumericTier` | 前缀**优先于**数值档(且 `StartsWith` 非 `Contains`) |
| `test_importSettings_realMetas_matchRequiredTier` | **真环境扫描**(5 个 wav × `.meta`) |
| `test_importSettings_unreadableInput_isViolationNotSilentSkip` | **负面夹具**:不可读输入 ⇒ **报违例**,不静默跳过 |
| `test_importSettings_wavExtension_isCaseInsensitiveAcrossPlatforms` | **§12.5 自查新增**:扩展名三形态跨平台一致(反证 F5 恰红) |

**⚠️ 仍悬(禁借绿,如实登记)**:

- **ARM64 腿** —— **已裁定:维持三格原样,登记为「长期未达」**(§12.4)⇒ ADR-012 该复选框
  **长期未勾且不得静默移除**;解锁条件 = 引擎升级后出现受支持的 ARM64 目标。**这不是「待办」,是已裁的长期状态。**
- **CI 常驻矩阵** —— job 已建但**从未跑过**(无 secret)⇒ **常驻性未兑现**(与上一条**不同因**:
  这条是「缺凭据」,那条是「目标不存在」)。
- **`U1SpikeSetup` 的 Teardown 复原** —— 未改(须先裁「spike 是否保留」)。
- **`tools/falsify` 的路径≠语义边界** —— 守卫路径之外的污染看不见,仍须逐例人判。
- **素材 Import Settings 的「内容批」半边** —— 本件只把判据做成门;**真实素材内容**仍归 content 批(`OQ-44-2`)。
- **`S14-8` 的 `audio.asset` 侧** —— 由通用防线**覆盖检出**,但**无专用包装器**(防写入)。

**⚠️ 仍未 commit**(本轮无用户指令)。
