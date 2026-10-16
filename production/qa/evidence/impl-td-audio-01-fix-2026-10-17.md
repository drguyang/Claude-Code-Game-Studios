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

---

## §七 未及核查 / 仍悬(禁借绿)

- **`01c` 真·全新 clone 复测 = 未跑**。原模拟方式已失效(§一);替代判据只有**静态核实**
  (引用链 `DefaultObject.asset.m_AddressableAssetSettingsGuid = 2c864c592004e1c729cd6c8ed9da4089`
  = `AddressableAssetSettings.asset.meta` 的 guid ⇒ 完整)。
  真复测须 `git clone` + 全量导入 ⇒ **登记为未验**。
- **W-5② `CreateGroup` schema 不复现 = 仍 ✗ 未修**。`CreateGroup` 只给默认 schema;
  现入库的 `audio_BundledAssetGroupSchema` 有 `m_UseDefaultSchemaSettings: 0`(已脱离默认)
  ⇒ 重建值不能靠默认值白拿。**须先裁「哪些 schema 值算定稿」**。
- **`EnsureDataCoreGroup` 的同款两静默分支 = 未修**(用户裁定 01e 范围不含它)。
  已在其 doc 就地注记:「以 `-executeMethod` 作 CI 前置步时同样会假绿」。
- **真 `AudioClip.loadState` 的 PlayMode 半边 = NOT-RUN**(素材内容未定稿,承 story-015)。
- **IL2CPP 三格矩阵 = NOT-RUN**(本机仅 Mono)。

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

# 5) 静态引用链(01c 的替代判据)
grep -m1 m_AddressableAssetSettingsGuid unity/Assets/AddressableAssetsData/DefaultObject.asset
grep -m1 '^guid:' unity/Assets/AddressableAssetsData/AddressableAssetSettings.asset.meta
#    两行 guid 须相同
```

---

## §九 结论

| 债 | 判定 |
|---|---|
| **01b** | ✅ **已修**(硬失败 + 指引恢复;原「自举」方向经裁定**不采纳**) |
| **01d** | ✅ **结案 —— 原判定的危害陈述证伪**(维持 GDD 口径,零代码改动) |
| **01e** | ✅ **已修**(两处分支,证伪轮 2/2 全红,零回归) |
| **01c** | ⚠️ **未验**(原判据失效;静态引用链已核实,真 clone 复测未跑) |

**回归**:`3615 / 3570 / 0 / 45`(基线 `3613 / 3568 / 0 / 45`,增量 = 新增 2 测)。
