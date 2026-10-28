# 修复轮原件 —— S20-20 挂标 #1「Addressables 装载面未接」2026-10-28

> 判定来源 = `production/sprints/sprint-20.md` §十(S20-20 的 5 条挂标,其中 #1)+
> `production/qa/evidence/fix-a5-sole-player-2026-10-28.md` §七-2
> (「`dataProvider != null` 那半今天**零夹具**」)。用户 2026-10-28 指令:「处理 #2:补 Addressables 装载面」。

---

## §一 病灶(两条,同一条装配链的两个缺口)

**主病灶 —— 生产 build 里槽位目录恒空。**
S20-20 把槽位骨架接进了 `MinimalWorldFactory`,但只接了**磁盘**那一半:
`DataCookedDisk.LoadBuildSlots()` 读 `Assets/DataCooked/`。该目录**不随玩家构建出货**
(ADR-014 §二:玩家构建零 JSON / 零源产物,只读 `data-core` 烘焙条目)⇒ 生产 build 上
`socket` 装载必抛 ⇒ 降级为空目录 ⇒ `PlaceableChecker.Check` 恒 `SlotNotFound`
⇒ **玩家建不了任何东西**,而一切夹具(走编辑器磁盘路径)全绿。
`AddressablesDataProvider` 侧**没有** `WorldBuildSlotsDataSet` 分支
⇒ 连「走对路」的可能性都不存在。

**连带病灶 —— 组条目缺席。**
`world_buildslots.cooked.bytes` 于 2026-10-28 09:45 由 S20-20 烘焙落盘,但**没进**
`data-core` Addressables 组(实测:盘上 26 份产物、组内 24 条,cooked 侧缺
`world_buildslots` 与 `foraging_constants` 两条)。即使 provider 分支存在,
`FetchBytes` 也会因 key 查不到而 E-13。

---

## §二 修法 —— 四处集合闭合 + 一条降级语义

| 要素 | 做法 |
|---|---|
| provider 分支 | `AddressablesDataProvider` 新增 `WorldBuildSlotsAddress` 常量 / `_worldBuildSlots` 缓存 / `Load<>` 分派 / `EnsureWorldBuildSlots()` / 进 `Preload()` |
| 地址表 | `DataCorePreloader.DataCoreAddresses` 追加同一条(四处集合:常量 ⊂ Ensure ⊂ Preload ⊂ 地址表;缺一处 ⇒ 既有两条门转红) |
| 消费方 | `MinimalWorldFactory.Create(IDataProvider dataProvider = null)`:provider 优先,`null` 才走磁盘兜底 |
| 生产调用点 | `BootRoot` 启动序传 `preloaded ? DataCorePreloader.Provider : null` |
| 降级语义 | provider 失败 ⇒ **空目录 + LogError**(不静默回落磁盘);报文新增「装载源」,使两条路径在日志上可分 |
| 组条目 | 跑「确保 data-core Addressables 组」菜单 ⇒ 26 条入库(见 §三 的 git 面) |

**为什么 `Create` 保留 `dataProvider = null` 的磁盘兜底而不是删掉**:109 处 EditMode 夹具
走的是 `Application.dataPath`(编辑器里有 `Assets/DataCooked`),删掉兜底会让它们全部
改为反射造 provider —— 那不是本节要修的缺陷,且与 `CompositionRoot` 的
`dataProvider != null ? … : DataCookedDisk.…` 既有形态不一致。**生产调用点不再有 `null` 分支**
才是判据(由 `BootRoot` 那一行的实参保证,见 §七-1 的诚实边界)。

---

## §三 落点

| 文件 | 位置 | 改动 |
|---|---|---|
| `unity/Assets/Gameplay.Presentation/AddressablesDataProvider.cs` | `:171` 常量 | 新增 `WorldBuildSlotsAddress`(= `WorldBuildSlotsDataSet.CookedFileName`) |
| 同上 | `:215` | 新增 `_worldBuildSlots` 缓存字段 |
| 同上 | `:359-361` | `Load<>` 新增 `WorldBuildSlotsDataSet` 分派 |
| 同上 | `:411` | `Preload()` 末尾追加 `EnsureWorldBuildSlots()` |
| 同上 | `:815` | 新增 `EnsureWorldBuildSlots()`(E-13 同款包裹) |
| 同上 | `:380` | 「未知数据集类型」报文追加本条 |
| `unity/Assets/Gameplay.Presentation/DataCorePreloader.cs` | `:237` | `DataCoreAddresses` 追加本条 + 计数注 22 → 23 |
| `unity/Assets/Gameplay.Boot/MinimalWorldFactory.cs` | `:88` | `Create(IDataProvider dataProvider = null)` + 形参 doc |
| 同上 | `:218-225` | `LoadSlotCatalog(dataProvider, …)`:provider 优先 |
| 同上 | `:139-141` / `:151` | LogError / 横幅 增「装载源」 |
| 同上 | `:267` | `SlotSourceLabel(...)`(日志可判源的唯一出处) |
| `unity/Assets/Gameplay.Boot/BootRoot.cs` | `:214` | `MinimalWorldFactory.Create(preloaded ? DataCorePreloader.Provider : null)` |
| `unity/Assets/Tests/EditMode/DataPipeline/provider_wiring_gate_test.cs` | `:182-190` | 门①追加三条 address ↔ 产物名断言 |
| `unity/Assets/Tests/EditMode/ModularBuilding/world_assembly_wiring_test.cs` | `:92-99` | 源码级门 `test_ac201b` 判据订正(见下「连带发现」) |
| `unity/Assets/Editor.Tools.Bake/DataBakeMenu.cs` | `:752-757` | 「Addressables 装载面尚未接」就地加注订正(原文不删) |
| `unity/Assets/Tests/EditMode/Boot/world_slots_provider_wiring_test.cs` | 全文(3 测) | 见 §四 |
| `unity/Assets/AddressableAssetsData/AssetGroups/data-core.asset` | 全文 | 组条目 24 → 26(菜单产出,见下) |
| `tools/falsify/specs/s20-20-slot-provider-surface.json` | 2 变异 | 见 §五 |

**组条目那一笔是菜单产物,不是我手改的**:`DataBakeMenu.EnsureDataCoreGroup`(目录全量扫描)
batch 实跑输出「`data-core` 组就绪:**26** 个条目已入组并打标签」。
⚠️ 它**顺带**登记了 `foraging_constants.cooked.bytes` —— 那是 S20-3(2026-10-26)烘完没进组的
**既有欠账**,与本节无因果关系。该份今天仍无运行期 provider 分支(无消费者),
登记后只是让它随组出货;若判定为「不该出货」,须在**它自己的轮次**裁,不在本轮顺手删。

**catalog 也已重建**(组条目变了就必须重建,否则运行期 `LoadAssetAsync` 查不到 key):
batch 跑 `DataBakeMenu.BuildAddressablesContent` ⇒ 日志「`Addressables 内容已构建 —— catalog 现含全部组条目`」。
可复算:`strings unity/Library/com.unity.addressables/aa/Linux/catalog.bin | grep -o "[a-z0-9_]*\.cooked\.bytes" | sort -u | wc -l` ⇒ **26**,
且明含 `world_buildslots.cooked.bytes` / `foraging_constants.cooked.bytes`。
⚠️ catalog 落 `unity/Library/`(gitignored,机器本地)⇒ 新检出后仍须重跑本菜单;
`catalog_freshness_gate_test` 在无 catalog 时报 **Inconclusive**(不是绿)。

### 连带发现:`world_assembly_wiring_test::test_ac201b` 会因实参变化转红

该门是**源码文本级**断言,原文逐字 `code.Contains("MinimalWorldFactory.Create()")` ——
本轮给工厂加了实参 ⇒ 字面串对不上 ⇒ 必红。**处置 = 改判据,不是删断言**:
现判据检 `MinimalWorldFactory.Create(` 存在**且**实参形如 `xxx ? …`(非无参)。
无参调用正是本轮的病灶形态(只走磁盘兜底),所以这条门现在的判别力**比原来更强**。

---

## §四 夹具 —— 三条,各挡一面

`unity/Assets/Tests/EditMode/Boot/world_slots_provider_wiring_test.cs`:

| # | 测名 | 判据 |
|---|---|---|
| ① | `test_slots_withProvider_usesProviderArtifact_notDisk` | 合成 **1 行**产物经 provider 送达 ⇒ `SlotCatalog.Count == 1` 且那格 `(3,0,3)` 在目录里,且注入地址被请求过。盘上真产物是 **13 行** ⇒ 「偷偷读了盘」被本断言直接否掉 |
| ② | `test_slots_providerFailure_degradesToEmptyCatalogAndNamesSource` | provider 全抛 ⇒ 空目录 + `LogAssert.Expect(Error, /槽位骨架装载失败.*装载源 —— data-core Addressables/)` |
| ③ | `test_a5_foldContext_tablesLoadedThroughProvider_deductsExactlyCost` | A-5 §七-2 的 provider 半边:三份表经 provider 送达 ⇒ 归因缺口消失、无「持有不足」、持有量**恰少 `cost(m)`** |

**①/② 为什么用注入字节源而不是真 catalog**:EditMode 无 Addressables locator(既有
`provider_wiring_gate_test` 文件头已载明同一限制)。注入缝 = `AddressablesDataProvider` 的
`internal AddressablesDataProvider(Func<string, byte[]>)`,经反射使用(先例
`clinic_machine_cooked_codec_test.cs:387` / `tutorial_content_provider_load_test.cs:50`)。
它只改「字节从哪来」,不改「失败怎么报」。

**③ 为什么另立文件而不加进 A-5 那条夹具**:A-5 的证伪 spec 用 `exactRed` 过滤
`inventory_fold_sole_player_wiring_test`;把 provider 半边并进去会让同一次变异多红一条,
`exactRed` 失效(证伪轮会误报「逃逸/多红」)。两条半边各在自己文件里,语义上也是两件事:
一条钉「槽 → fold 的接线」,一条钉「provider 分支与磁盘分支等价」。

---

## §五 证伪轮(2 变异,**2/2 exact**)

```
python3 tools/falsify/unity_falsify.py --spec tools/falsify/specs/s20-20-slot-provider-surface.json \
        --guard unity/Assets/DataCooked --guard unity/Assets/AddressableAssetsData
```

| 变异 | 打在 | 预期红 | 实得 |
|---|---|---|---|
| `M1-slotsProviderPathDropped`(三元的 provider 分支换成磁盘兜底) | `MinimalWorldFactory.cs:224` | ① `..._usesProviderArtifact_notDisk` + ② `..._providerFailure_degrades…` | ✅ **exact**(`total=3 passed=1 failed=2`,RED 恰为这两条) |
| `M2-foldItemsProviderBranchNeutered`(fold 的物品表 provider 分支返回空表) | `CompositionRoot.cs:434` | ③ `..._tablesLoadedThroughProvider_deductsExactlyCost` | ✅ **exact**(`total=3 passed=2 failed=1`,RED 恰为这条) |

- **一属性一条变异**:M1 = 「装载面那条腿真的被走」;M2 = 「fold 的三份表真的经 provider 送达」
  (A-5 §七-2 的那一半)。两条打**不同的文件、不同的语义面**,互不代偿。
- M1 的红是**两条**:① 断言「注入地址被请求 + 槽位恰好是注入的那 1 格」;② 断言「provider 失败
  须响亮报错」。退回磁盘后 ① 的两条子断言都红,② 则**根本没进入失败路径**(盘上产物装载成功、
  那条 `LogError` 不打)⇒ 两条各挡一面,不是「一条顺带红了」。

**⚠️ 本轮第一次 M1 是废轮(如实登记,不作证据)**:首版锚点打在 `:223` 的**条件行**
(`… data = dataProvider != null`)并整行替换成以 `;` 结尾的赋值 ⇒ 后面两行三元分支悬空
⇒ **编译失败**(驱动退出码 **6**、零结果 XML),驱动报 `!! 未产出结果 XML —— 编译失败?`
并判整轮**未通过**。教训(与「变异打错靶」同族):**变异必须语法自洽**,改三元表达式要动
**分支**而不是**条件**——条件是整条语句的一部分,单独替换会把语句切碎。
订正后 `--check-only` 双锚点通过,再跑得 2/2 exact。

- 还原 = **SHA-256 逐字节**(驱动输出「还原:SHA-256 逐字节校验通过」);
- 受保护路径(`unity/Assets/DataCooked` · `unity/Assets/AddressableAssetsData`)**指纹未变**
  —— 注意 `data-core.asset` 起跑时**已有改动**(本轮 §三 的组条目),驱动判的是
  「前后指纹相同」,不是「本来干净」。

---

## §六 回归(三元组)

**截至时刻** `2026-10-28T03:09Z` · **批次** S20-20 装载面修复轮全量 · **commit** `56633bce` + 未提交工作树

| 套件 | 退出码 | 实得 | 基线(同带三元组) | 差值 |
|---|---|---|---|---|
| EditMode | 0 | `total=5681 passed=5646 failed=0 inconclusive=0 skipped=35` | `5678/5643/0/0/35` @ `2026-10-28T02:49Z` · A-5 修复轮全量 · `85f248e8`(工作树干净) | **+3 / +3** |
| PlayMode | 2 | `total=238 passed=234 failed=0 inconclusive=2 skipped=2` | `238/234/0/2/2` @ 同上 | **0 / 0** |

**等式核账**(承 `coding-standards.md` §借绿禁令):`5681 − 5678 = 3` = 本轮新增夹具**恰 3 条**
(`world_slots_provider_wiring_test` ① ② ③;XML 内实测该 fixture 3 条全 `Passed`);通过数同量
`5646 − 5643 = 3`。**零回归**:其余 5678 条沿用同一夹具集会话,失败数前后同为 0。

PlayMode `exit=2` **不是失败**:`failed=0`;两条 `inconclusive` 是**既有**的
`test_u1assump6_judgement{1,2}_notRun`(与 A-5 轮逐字相同)。

**可复算命令**:

```bash
cd unity && ~/Unity/Hub/Editor/6000.3.24f1/Editor/Unity -batchmode -projectPath "$PWD" \
  -runTests -testPlatform EditMode -testResults "$PWD/Logs/s20-provider-full-editmode.xml" \
  -logFile "$PWD/Logs/s20-provider-full-editmode.log"
python3 - <<'PY'
import xml.etree.ElementTree as ET
tc=list(ET.parse('unity/Logs/s20-provider-full-editmode.xml').getroot().iter('test-case'))
print(len(tc), sum(1 for c in tc if c.get('result')=='Passed'))   # 期望 5681 5646
PY
```

⚠️ **本轮回归的判据面有一个前提**:`catalog_freshness_gate_test` 比对的是
**本机构建出的 catalog**(`unity/Library/`,gitignored)。组条目从 24 变 26 之后若**不重建 catalog**,
该门会红并点名缺 `world_buildslots.cooked.bytes`。本轮已跑「构建 Addressables 内容」
(§三 末),故该门绿是**真绿**;新检出机器上它报 **Inconclusive**(不是绿,不得当通过)。

---

## §七 本件**证明不了**的事(禁借绿)

1. **`BootRoot` 那一行的实参没有夹具** —— 「生产调用点传的是 provider」由
   `BootRoot.cs:214` 的源码承担,EditMode 夹具不跑 `BootRoot` 全链(它要 Addressables
   初始化 + 场景)。可证伪的替代面 = 源码级断言(候选:在 `provider_wiring_gate_test`
   追加「`MinimalWorldFactory.Create(` 的调用点须带 provider 实参」的文本扫描),本轮**未做**。

   > 🔁 **2026-10-28 追补(本轮已做,原文不删)** —— 上述候选**已落地**,但落点不是
   > `provider_wiring_gate_test`,而是 **`world_assembly_wiring_test.cs:105`**
   > (`test_ac201b` 的二次收紧):那里本来就是 `BootRoot.cs` 的源码级门,把判据并进去
   > 比新开一处更省。判据现要求**真分支逐字**是 `DataCorePreloader.Provider`:
   > ```
   > MinimalWorldFactory\.Create\(\s*[A-Za-z_]\w*\s*\?\s*DataCorePreloader\.Provider\s*:
   > ```
   > ⚠️ **上一版判据钉的是「形状」不是「内容」** —— 离线复算(6 个形态)实测:
   >
   > | 形态 | 旧判据 `Create\(\s*\w+\s*\?` | 新判据 |
   > |---|---|---|
   > | `Create(preloaded ? DataCorePreloader.Provider : null)` | 绿(正确) | 绿 |
   > | `Create(preloaded ? null : null)` | **绿(病灶)** | **红** |
   > | `Create(null ? foo : null)` | **绿(病灶)** | **红** |
   > | `Create(preloaded ? null : DataCorePreloader.Provider)`(假分支错) | **绿(病灶)** | **红** |
   > | `Create()`(无参) | 红 | 红 |
   > | 空白变体 | 绿 | 绿 |
   >
   > 复算命令(自包含):
   > ```bash
   > python3 - <<'PY'
   > import re
   > pat = r"MinimalWorldFactory\.Create\(\s*[A-Za-z_]\w*\s*\?\s*DataCorePreloader\.Provider\s*:"
   > old = r"MinimalWorldFactory\.Create\(\s*[A-Za-z_]\w*\s*\?"
   > for v in ["Create(preloaded ? DataCorePreloader.Provider : null)",
   >           "Create(preloaded ? null : null)",
   >           "Create(null ? foo : null)",
   >           "Create(preloaded ? null : DataCorePreloader.Provider)",
   >           "Create()",
   >           "Create( preloaded  ?  DataCorePreloader.Provider  : null)"]:
   >     s = "MinimalWorldFactory." + v
   >     print(bool(re.search(old, s)), bool(re.search(pat, s)), v)
   > PY
   > ```
   > 预期:第 2–4 行旧判据 `True`、新判据 `False`。
   >
   > **真变异轮**(不是只有离线推演):`tools/falsify/specs/s20-88-bootroot-provider-arg-gate.json`
   > 把 `BootRoot.cs:219` 的真分支换成 `null` ⇒ **恰 1 条红**(`test_ac201b…`,8 条里 7 绿),
   > 还原 SHA-256 逐字节通过、受保护路径指纹未变。
   > ```bash
   > python3 tools/falsify/unity_falsify.py \
   >   --spec tools/falsify/specs/s20-88-bootroot-provider-arg-gate.json \
   >   --guard unity/Assets/DataCooked --guard unity/Assets/AddressableAssetsData
   > ```
   >
   > ⚠️ **仍是源码级判据,不是运行期观测** —— 本件仍**没有**任何夹具真的跑 `BootRoot`
   > 全链并断言「provider 被请求过」。所以本条从「无门」升为「有文本门」,
   > **不升为「已实测」**。该门的判别力只覆盖「实参写法」,不覆盖「运行时该实参真的非 null」。
   > ⚠️ 另:证据件 §三 记的 `BootRoot.cs:214` 是**当时**的行号;本轮(2026-10-28 追补)
   > 实测该行已漂到 **`:219`**(上方注释块长了 5 行)。原文不删,以此处为准。
2. **`Addressables` 真实 catalog 的装载未测** —— 字节取自注入缝。真实 catalog 里是否有该条目
   由门②(组条目 ⊇ 全部 `*Address` 常量)钉,那是**文件级**判据,不是装载实测。
3. **`foraging_constants` 登记后的运行期面** —— 本轮不新增它的 provider 分支,
   它进组后**仍无消费者**(零读取路径)。这是既有欠账,不在本件裁决面。
4. **`Preload()` 扩容的连带影响** —— `DataCoreAddresses` 22 → 23 条 ⇒
   `LoadGlobalConfigVersion()` 的组合值**改变** ⇒ 旧存档头的 `ConfigVersion` 比对会判「不匹配」。
   这是 ADR-014 §五 的**设计内行为**(数据集变了,版本就该变),但对既有开发存档是可见变化;
   `TD-epic-01-f` 的开发视图会如实报「版本不匹配」。本轮未做存档兼容处理(也不需要:
   P0 无对外存档)。

---

## §八 判据来源索引

- `production/sprints/sprint-20.md` §十(S20-20 挂标 #1)
- `production/qa/evidence/fix-a5-sole-player-2026-10-28.md` §七-2(`dataProvider != null` 半零夹具)
- ADR-014 §二/§五(玩家构建零 JSON 直读 · data-core 单组预载 · ConfigVersion 派生)
- ADR-022 §二(槽位骨架唯一作者 = 关卡工具)
- `unity/Assets/Tests/EditMode/DataPipeline/provider_wiring_gate_test.cs` 文件头 ①②③④(四处集合闭合的既有门)
