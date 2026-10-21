# 实现证据原件 —— O-24-1 兑现:`FURN_TABLE` 闭集的生产链路(S17-4)

> **对象**:`docs/tech-debt-register.md` 的 **`TD-clinic-01`** · 路线图 §三 **G-8** ·
> `production/epics/clinic-machine/story-001` 的 `AC-24-08` / `AC-24-12`(闭集半边)
> **日期**:2026-10-21
> **批次**:sprint-17 批 2 续(S17-4)
> **用户指令逐字**:「FURN_TYPES 闭集(O-24-1 / TD-clinic-01,sprint-18 的 G-8)未兑现 ⇒
> FurnTraitsProductionReady => false,求值器在生产链路上没有输入源,只跑在测试注入的夹具源上。
> 这部分完成还差的输入源现在补齐」
> **状态**:输入源已补齐、证伪轮 4/4 已过、全量回归待填(见 §四)。

---

## 一、病因与判据形态

**病**:`FURN_TABLE`(`type` / `tier` / `e_env`)在 GDD `clinic-machine.md:297` 明文归 21a,
且**双向登记在册**(`clinic-machine.md:301` + `item-database.md:859`),但**全库零落点** ——
21a epic 已 `Complete 12/12` 而认领 `O-24-1` 的 story = 0。后果是一条**结构性**死链:

```
ClinicModifierEvaluator.FurnTraitsProductionReady => false
  ⇒ IClinicFurnitureTraitsSource 只有测试注入的夹具源
  ⇒ 求值器在生产链路上没有输入源
  ⇒ AC-24-08 / AC-24-12 的闭集半边结构上不可达
```

**判据口径(本件自设)**:补齐的判据不是「新增了一个文件」,而是**生产链路上闭集真的被强制
且有机器判据**。这两件事分开证,见 §三。

---

## 二、交付面(实测落盘)

### 2.1 作者态 + 烘焙期(21a 侧)

| 文件 | 性质 | 内容 |
|---|---|---|
| `assets/data/item_database_furniture.json`(新) | 作者态 | `schema_version:1` · `tier_max:3` · `furn[]` **7 行** |
| `unity/Assets/Editor.Tools.Bake/ItemDatabaseFurnitureBinder.cs`(新) | 阶段 2 绑定 | 三条域门:`tier ∈ [0, tier_max]` · `\|e_env\| ≤ C_max = 1/64` · `tier_max` 与 24 侧对账;**聚合 throw** |
| `unity/Assets/Editor.Tools.Bake/ItemDatabaseFurnitureCookedWriter.cs`(新) | 编码 | deterministic;行序 = `type` ordinal 全序 |
| `unity/Assets/Editor.Tools.Bake/ItemDatabaseFurnitureBake.cs`(新) | 装载器 | `BakeFromRepo` / `BakeFromSourceText` / `AsFurnTypeSource` |
| `unity/Assets/DataCooked/item_database_furniture.cooked.bytes`(新) | 产物 | **216 B / 7 家具类型**,`ConfigVersion = 0xD0DC16A9` |

**种子类型(7)**:`bed` · `surgery_table` · `sterile_cabinet` · `herb_cabinet` · `grinder` ·
`drying_rack` · `chamber_pot`。数值按 GDD 量级域取种子值(用户裁定),
**只用于验机制,不是平衡值**(承「数值用户自己调」)。

> ⚠️ **「21a 侧作者态」的准确读法(2026-10-21 补注)**:上表该列写的是**归属**(该文件属 21a 名下),
> **不是**「与 23 的 `cost(m)` 同源」。后者今日**做不到** —— `cost(m)` 全库无作者态载体。
> 详见 §六 7 与债册 `TD-itemdb-01`。

### 2.2 运行期(24 侧)

| 文件 | 性质 | 内容 |
|---|---|---|
| `unity/Assets/Sim/Clinic/ClinicFurnitureCatalogue.cs`(新) | 运行期表 | `FurnTypes` 闭集 + 逐件 `tier`/`e_env`;构造期纵深拒收(域 / 重复 type / `tier_max`) |
| `unity/Assets/Gameplay.Presentation/ClinicFurnitureCookedCodec.cs`(新) | 解码 | 20 字节头校验 + 行读 + `AssertFullyConsumed` |
| `unity/Assets/Gameplay.Presentation/AddressablesDataProvider.cs`(改) | 支路 | `FurnitureCatalogueAddress` + `Load<ClinicFurnitureDataSet>` + `EnsureClinicFurniture()` + `Preload()` 追加 |
| `unity/Assets/Gameplay.Presentation/DataCorePreloader.cs`(改) | 预载 | 19 → **20** 条 |
| `unity/Assets/Sim/Clinic/ClinicFurnitureTraitsSource.cs`(新) | **生产属性源** | `IClinicFurnitureTraitsSource` 的生产实现:23 只读视图 + 21a 目录 → **三值**查询 |
| `unity/Assets/Sim/Clinic/IPlacedStructureReadView.cs`(改) | 视图契约 | 新增 `GetModuleId(int)`(`module_id` 从此释放) |
| `unity/Assets/Sim/Clinic/ClinicRoomExtraction.cs`(改) | 真判据入口 | 新增 `Extract(view, furnTypes)` 重载;两条入口共用同一实现体 |
| `unity/Assets/Sim/Clinic/ClinicModifierEvaluator.cs`(改) | 哨兵 | `FurnTraitsProductionReady => **true**` |

### 2.3 菜单(生产链路)

| 文件 | 内容 |
|---|---|
| `unity/Assets/Editor.Tools.Bake/DataBakeMenu.cs`(改) | `BakeClinicMachineTablesCore(repoRoot, out furn)` = **计算段**(先烘 21a 取闭集 → 注入 24);菜单本体只做落盘 4 份产物 + 日志 |

**为什么抽 Core(本件的一个设计决定)**:菜单方法带 `[MenuItem]`,batch 下**不被调用**
(只有 `-executeMethod` 点名才跑)。若把注入段写在菜单体内,测试只能**同构复刻**一遍再断言 ——
而复刻品与菜单一旦漂移,断言打的就不是生产链路(C-1 轮「栈帧 ≠ 接线」同型风险)。
抽出后**菜单与测试调同一符号**,漂移不可能发生。

⚠️ **判据边界(如实登记)**:菜单本体仍未被测试执行。Core 是菜单体内**除落盘/Refresh/日志外
唯一的计算段**,故「Core 绿」⇒「菜单的计算段绿」;这不是「菜单被跑过」,不得如此主张。

---

## 三、判据(已落 / 已证 双列)

| 判据 | Landed | Proven | 落点 | Proven-by(名字逐条实测存在) |
|---|---|---|---|---|
| 闭集有生产来源 | ✅ | ✅ | `item_database_furniture.json` + binder | `test_furniture_seedSource_bakesCleanly` |
| 域门:`tier ∈ [0, tier_max]` | ✅ | ✅ | `ItemDatabaseFurnitureBinder` | `test_furniture_{missingTier,tierAboveMax,negativeTier}_isRejected` |
| 域门:`\|e_env\| ≤ C_max` | ✅ | ✅ | 同上 | `test_furniture_eEnvBeyondCMax_isRejected` |
| 域门:`Fix` 必须是字符串 | ✅ | ✅ | 同上(经 `FixParse`) | `test_furniture_eEnvAsJsonNumber_isRejected` |
| 域门:重复 type 拒 | ✅ | ✅ | 同上 | `test_furniture_duplicateType_isRejected` |
| 读方↔写方镜像 | ✅ | ✅ | codec ↔ writer | `test_furniture_diskMatchesFreshBake`(**盘上产物 ↔ 现烘逐字节**) |
| 三值不退化 | ✅ | ✅ | `ClinicFurnitureTraitsSource.Lookup` | `test_traitsSource_threeValuedLookup` · **反证 F1** |
| 真判据按闭集筛成员 | ✅ | ✅ | `ClinicRoomExtraction.Extract(view, furnTypes)` | `test_roomExtraction_truePredicate_excludesNonFurnitureStructures` 等 3 条 · **反证 F2** |
| 生产属性源就位 | ✅ | ✅ | 哨兵 + 实现者 | `test_clinic_furnTraitsProductionReady_sentinelTrue_andImplementationUsable` · **反证 F3** |
| **生产链路强制闭集** | ✅ | ✅ | `DataBakeMenu.BakeClinicMachineTablesCore` | `test_productionMenuPath_coreForcesFurnClosure` · **反证 F4** |

### 3.1 AC-24-08 / AC-24-12 的状态变化

| AC | 之前 | 现在 | 说明 |
|---|---|---|---|
| **AC-24-08**(FURN_TABLE 字段断言) | `BLOCKED-BY-O-24-1` | **可判,且已证** | 字段类型 ∈ 整数域 / `Fix` 字符串 / 缺字段 = 构建失败 —— 三条域门各有负向夹具 |
| **AC-24-12**(子句 type ⊆ FURN_TYPES) | `BLOCKED-BY-O-24-1` | **可判,且已证** | 闭集来源已落地;`FurnMembershipEnforced == true` 由 Core 测钉住 |

⚠️ **「可判」≠「已全证」**:AC-24-12 的 (a) 半边(子句类型闭集 + 方向确定性)属 story-001 本体,
2026-10-20 已证;(b) 半边(成员资格)由本件补齐。**两部分都有载体,才谈得上全证。**

---

## 四、证伪轮(Falsifiability Gate)

驱动:`tools/falsify/unity_falsify.py`(行号 + 原文双锚定;施加与还原同进程;SHA-256 逐字节校验;
受保护路径指纹前后比对)。spec:`tools/falsify/specs/s17-4-furniture-catalogue.json`(4 个变异,全 `exactRed: true`)。

| 变异 | 靶点 | 停用什么 | 实测红集 | 判定 |
|---|---|---|---|---|
| F1-unknownCollapsedToNonFurniture | `ClinicFurnitureTraitsSource.cs:89` | `Unknown` → `NonFurniture` | `traitsSource_threeValuedLookup` / `sentinelTrue_andImplementationUsable` | ✅ 恰 2 条 |
| F2-truePredicateIgnoresClosure | `ClinicRoomExtraction.cs:169` | 真判据转发代理版 | `excludesNonFurnitureStructures` / `excludesMissingModuleId` / `catalogueFurnTypes_feedTruePredicate` | ✅ 恰 3 条 |
| F3-productionReadySentinel | `ClinicModifierEvaluator.cs:62` | `=> true` → `=> false` | `sentinelTrue_andImplementationUsable` / `furn_type_closure_absent_isDetectable` | ✅ 恰 2 条 |
| **F4-menucCoreDropsFurnClosure** | `DataBakeMenu.cs:585` | 闭集注入 → `null` | `test_productionMenuPath_coreForcesFurnClosure` | ✅ **恰 1 条** |

判据输出逐字:`══ 证伪轮通过:4 个变异全部恰如预期 ══`,`EXIT=0`;
还原 `SHA-256 逐字节校验通过`;受保护路径 `unity/Assets/DataCooked` **指纹未变**。

### 4.1 ⚠️ 首轮(3 变异)exit 1 —— 病因与处置(如实登记)

**首轮(`unity/Logs/s17-4-falsify.log`,2026-10-21 10:00)三条全红得比预期多**:
F1 得 2 / F2 得 3 / F3 得 2,驱动判 `exit 1`。

**病因 = `tools/falsify/README.md` 坑 1 的第三次复发**:我写 `expectRed` 时只数了
「新加的那条断言」,漏掉既有的交叉检查。**不是守卫松,也不是误伤** —— 六条红逐条核对过:

| 额外红 | 为什么**应该**红(论证,非事后追认) |
|---|---|
| F1 → `sentinelTrue_andImplementationUsable` | 它后半段独立断言 `Lookup((9,9,0)) == Unknown`(`clinic_modifier_eval_test.cs:455`) |
| F2 → `excludesMissingModuleId` | 代理版不看 `module_id` ⇒ null 实例照样贡献格(期望 0,实得 1) |
| F2 → `catalogueFurnTypes_feedTruePredicate` | 代理版把 wall 算进(期望 1,实得 2) |
| F3 → `furn_type_closure_absent_isDetectable` | 其豁免前提逐字是「哨兵 false 仅在 `FurnTraitsProductionReady == true` 时合法」—— 哨兵回退**正是它要抓的动作** |

**处置顺序(承 README 坑 2)**:先逐条论证「为什么应该红」,论证成立后才改预期,
并把论证写进 spec 的 `_expectRed_justification`。**没有把实得抄进 `expectRed`** ——
那会让 `exactRed` 退化成 tautology。

### 4.2 F4 的由来 —— 补测而非补证

首轮之后核查发现一处**真缺口**:`DataBakeMenu.BakeClinicMachineTables()` 是闭集注入的
**唯一生产调用点**,而 `grep -rn "BakeClinicMachineTables" unity/Assets/` 在**测试树零命中**。
即「生产路径上闭集真被强制」当时**只有烘焙日志可依,没有机器判据** ——
这正是 `/story-done` 点名的「扫不到被测装配」形态(症状是跳过,不是红)。

⇒ 补:抽出 `BakeClinicMachineTablesCore` + 新测 + **F4 变异**证明新测有判别力。
**新测本身若不红过就不算证明**,这是 F4 存在的理由。

---

## 五、全量回归

**基线溯源三元组**:截至 `2026-10-21 10:17 CST` · 批次 `sprint-17 批 2 续 S17-4` · commit `15fcda8` + 工作树

**`4265 / 4229 / 0 / 36`**(`unity/Logs/s17-4-full-regression-r2.xml`,退出码 0)

**对账**:对 `4237 / 4201 / 0 / 36 @ 2026-10-21 09:02 CST · sprint-17 批 2 S17-2 收尾轮 · 3a22dbf`
做 `fullname` 集合差 = **Δ +29 / 消失 1**。

| 差 | 数 | 明细(**逐条点名,非减法推算**) |
|---|---|---|
| 新增 | +29 | ① `ClinicFurnitureCatalogueTest` **27** 条(26 实现 + 1 本轮补的 Core 测)<br>② `ClinicModifierEvalTest.test_clinic_furnTraitsProductionReady_sentinelTrue_andImplementationUsable` —— **改名后的对侧**(旧名消失) <br>③ `cooked_drift_all_artifacts_test.drift[item_database_furniture.cooked.bytes]` —— 新产物进漂移门登记的**参数化用例** |
| 消失 | 1 | `test_clinic_furnTraitsProductionReady_sentinelFalse` —— **改名**为 ② 那条(哨兵语义翻转,预期内),非丢失 |

⇒ 测试**净增 28**(29 新增 − 1 改名对),**无一条因失败或删除而消失**。

### 5.1 ⚠️ 首跑(全量)**1 条真红** —— 病因与修复(如实登记)

**首跑 `4265 / 4228 / 1 / 36`,退出码 2**:

```
RED: ProviderWiringGateTest.test_wiring_dataCoreGroup_containsEveryProviderAddress
     data-core 组条目数 19 少于 address 常量数 20
     Expected: greater than or equal to 20 / But was: 19
```

**病因**:`item_database_furniture.cooked.bytes` 的**产物烘了、`Preload()` 调了、
`DataCorePreloader` 登记了,但 `data-core` Addressables 组里没注册条目**。
运行期后果不是「测试挑剔」而是 **E-13 启动期硬失败**(`Preload()` 会 `Load` 一条组里没有的 address)。

**这是仓库已记过两次的同一条教训的第三次现身** —— **「补烘 ≠ 登记 ≠ 接线」是三件事**:

| 面 | 状态 | 谁看得见 |
|---|---|---|
| 产物落盘 | ✅ 216 B | 漂移门(已登记,绿) |
| `Preload()` 调用 | ✅ 已加 | 接线门 ④(绿) |
| **组内条目** | ❌ **缺** | **接线门 ②(唯一红的)** |

**修复 = 走生产路径**:`unity build <unity/> --target StandaloneLinux64 --execute-method
DaYiJingCheng.EditorTools.Bake.DataBakeMenu.EnsureDataCoreGroup`
⇒ 日志逐字 `[大医精诚] data-core 组就绪:20 个条目已入组并打标签。`,
组文件条目 19 → **20**,`m_Address: item_database_furniture.cooked.bytes` 在位。
**未手改 `.asset`** —— 该菜单头注写明组定义已入库,`CreateGroup` 会与入库 schema 分叉。

⚠️ **顺带的自纠**:我在补测前曾说「`DataBakeMenu` 的重构只抽函数,不影响
`provider_wiring_gate_test`」—— 重构部分确实无影响,但我**把更早那次「加了 address 却没注册组条目」
的欠账算漏了**。测试没有骗人,是判断漏了。

---

## 六、未及核查 / 未跑项(如实登记,禁借绿)

1. **IL2CPP 三格 / 跨平台逐位**(ADR-012 矩阵):**NOT-RUN**,本机仅 Mono。<br>⚠️ **2026-10-21 订正:本句「本机仅 Mono」为假** —— ADR-012 §Validation 2026-10-17 已逐格实测:Linux-x64-Mono ✅ 09-24(3/3)· **Linux-x64-IL2CPP ✅ 09-24**(StandaloneLinux64 player 3/3,与独立 Python 金标准 + Mono 三方 19/19 逐位相同)· Linux-ARM64-IL2CPP ❌ **目标不存在**(pinned 工具链 46 个 BuildTarget 无任何 arm 目标;用户裁定长期未达)· CI 常驻 ⚠️ job 已建但从未跑(缺 `UNITY_CLIENT_ID`/`UNITY_CLIENT_SECRET`)。⇒ **矩阵级,非本件个体判据**。原文保留不删。
2. **菜单本体未被测试执行**:`[MenuItem]` 在 batch 下不触发;本件测的是它内部唯一计算段(Core)。
   详见 §二 末尾的判据边界。
3. **`FurnTypeClosureEnforced` 仍为 `false`** —— 这是**正确的**:析出的**默认单参入口**仍走代理判据
   (story-002 已验收的行为,改它会静默改变既有测试语义)。真判据入口已提供且已证,
   装配层**尚未选它** —— 选哪条是装配层接线,未落。C11 测因此改了判据(见 §七 1)。
4. **`orientation` 仍未在 `IPlacedStructureReadView` 暴露** —— 若有需要它的判据,当前不可表达。
   本轮只释放 `module_id`。
5. **数值未定稿**:7 行家具的 `tier` / `e_env` 为 GDD 量级域的种子值,归用户数值轮。
6. **`ClinicFurnitureTraitsSource` 是快照语义**:构造时摊平布局,之后不订阅变化。
   自动跟随会把它从读方变成监听方,破坏「24 只读不持占用真相」(GDD 规则五)⇒ **装配层须在布局变更后重建**。
   该义务未落(无装配层)。
7. **`O-24-1` 的「与 `cost(m)` 同源」没有做到,且被同源的那一方今日不存在**(2026-10-21 补登)。
   `clinic-machine.md:297` 逐字要求家具目录「与 23 的 `cost(m)` **同源目录数据**」,`item-database.md:859`
   亦逐字认领「(与 `cost(m)` 同源)」。本件落的是**独立文件** `item_database_furniture.json`。
   ⚠️ **这不是本件的过错** —— 实测 `cost(m)` 全库无作者态载体(零命中清单见债册 `TD-itemdb-01`),
   无从同源。但 §二 2.1 把该文件写成「21a 侧作者态」时**读起来像在满足同源要求**,
   该表述须读作「21a **名下**的作者态」,不读作「与 `cost(m)` 同源」。**登记去向 = `TD-itemdb-01`。**
8. **`e_env` 取 `1/64` = 现值域上界,而 GDD 同一行给的案卷建议区间在该上界下不可达**(2026-10-21 补登)。
   7 行家具的 `e_env` 取 `±1/64`。这满足 GDD 的**硬界**(`:188` `e_env ∈ [−C_max, C_max]`,
   `C_max = 1/64`),但 GDD `:330` **同一行**还写着「(正 ∈ [1/16,1/8]、负 ∈ [−1/4,−1/8] 为案卷建议)」。
   实测这两个区间与硬界**互斥**,不是「弱了 4 倍」这种程度问题:

   | 案卷建议 | 幅度 | Q16.16 raw | 对 `C_max raw = 1024` | binder 判定 |
   |---|---|---|---|---|
   | 正下界 `1/16` | 0.0625 | 4096 | 4× | **拒收** |
   | 正上界 `1/8` | 0.125 | 8192 | 8× | **拒收** |
   | 负上界 `−1/8`(最小幅度) | 0.125 | 8192 | 8× | **拒收** |
   | 负下界 `−1/4` | 0.25 | 16384 | 16× | **拒收** |
   | **现值 `±1/64`** | 0.015625 | **1024** | **1×** | 通过 |

   ⇒ **案卷建议区间里的任何取值都无法通过烘焙**(`ItemDatabaseFurnitureBinder.cs:186-192` 越界拒收;
   `ClinicFurnitureCatalogue.cs:100-103` 构造期同样拒收)。**这是 GDD 内部口径的冲突,不是本件的取值缺陷**:
   `C_max ≈ 1/n_max` 是 OQ-24-5 / OQ-24-6 的**设计约束**(`n·C_max ≤ 1`),而案卷建议的幅度量级
   与之相差 4–16 倍。**须由用户裁定谁让**(改 `n_max` / 改 `C_max` 的派生式 / 或作废案卷建议区间),
   **本件按硬界取值,并在数值轮之前一直取 `C_max` 本身**。
   ⚠️ 登记口径:**现值安全**(贴着上界但未越界),**但「安全」不等于「符合建议」** ——
   二者在今日**不可能同时成立**。

   **⚠️ 顺带查出的一处口径瑕疵(`C_max` 在 GDD 里有两个互不等价的定义,本件不裁定、只登记)**:
   - `:330` 的定义可推导:`C_max = |ENV_MOD_MAX − ENV_MOD_MIN| / n_max = 1 / 64`(**外部量**,可验算);
   - `:188` 的定义是**自指**的:逐字 `C_max := max|e_env| over FURN_TABLE` ——
     即「上界 = 表里的最大值」。按该定义,`|e_env| ≤ C_max` **对任何表恒真**(最大值当然不超过最大值)
     ⇒ **该行单独看零约束力**。
   ⇒ 生效的界来自 **OQ-24-5 / OQ-24-6 的裁定**(`C_max = 1/64`)与 `:330` 的派生式,
   **不是** `:188`。本件按前者实现(`EnvContributionMaxRaw = Fix.OneRaw / 64`,
   `ClinicFurnitureCatalogue.cs:48`)。**`max|e_env|` 的真实语义应是「由 `n_max` 反推的容量上限」,
   不是「表内实际最大值」** —— 措辞订正归 GDD 修订轮,不在本件。

---

## 七、偏离与边界

1. **C11 检测测的判据已改判(承卡面实施约束 ③「届时改判据并如实登记,不得静默改绿」)**:
   旧判据 = 「有 `FurnType` 痕迹 ⇒ 哨兵必须 true」。它与本轮真实状态冲突 —— 闭集已落地(有痕迹),
   但默认入口仍走代理判据 ⇒ `FurnTypeClosureEnforced` 的真值**就是 false**,翻成 true 才是撒谎。
   改为更锐利的可机器查判据:痕迹存在时,哨兵仍 false 仅在**① `FurnTraitsProductionReady == true`
   ∧ ② 真判据重载存在**时合法。
2. **两条入口并存而非改写代理版**:代理版是 story-002 已验收的行为;改它会静默改变既有测试语义。
   卡面实施约束 ③ 明确「不得静默改绿」。
3. **`BakeClinicMachineTablesCore` 是新公开 API**:菜单与测试共用。它的存在本身是「不让测试复刻生产码」
   的结构性手段,不是为测试开的后门(它不落盘、不碰 Addressables、不打印)。
4. **`ClinicFurnitureCatalogue.Rows` / `FurnTypes` 用 `Array.AsReadOnly`** ——
   裸数组可被强转回数组就地改写(承 S17-2 的 R1/R2 先例)。

---

## 八、交接

- **story-001 的 Test Evidence 须回填**:本件的产物 + 接线 + 闭集强制属 story-001 面
  (S17-2 证据件 §七 已登记该义务)。`AC-24-08` / `AC-24-12` 的 `BLOCKED-BY-O-24-1` 挂标可解除。
- **`TD-clinic-01` 可结**:判据见 §三。
- **路线图 §三 G-8 可结**:档位是 **链门** —— 结它解锁 clinic 004/005。
- **story-003 转 `Complete` 的硬前置已解除**(哨兵为 true);其自身的 AC 证据另见
  `impl-s17-2-clinic-modifier-eval-2026-10-21.md`。
- **两条未闭项已入册,不随本件结清**(2026-10-21 补):
  - **`TD-itemdb-01`**(债册)—— `cost(m)` 作者态缺席 ⇒「同源」要求今日不可满足。
    归属 21a,**触发条件 = 23 建造系统落地 `build_modules.json` 的作者态轮**(届时两表须同源)。
  - **`e_env` 量级冲突**(§六 8)—— GDD `:188` 硬界与 `:330` 案卷建议**互斥**,
    须用户裁定;在此之前取值恒为 `C_max = 1/64`。**该冲突不阻塞本件**,但阻塞「数值定稿」。

---

## 九、2026-10-21 裁定落盘 —— `C_max` 1/64 → 1/8

**裁定**:用户 2026-10-21 选定「**甲 · `C_max` 抬到 1/8,案卷建议落地**」。
本条**只记录该裁定的执行面**,不重开 §一–§八 的任何判定。§六 8 的冲突登记**原文保留**(承「原文不删」)。

### 9.1 改判内容(逐条)

| 项 | 改前 | 改后 | 依据 |
|---|---|---|---|
| `C_max` | `1/64`(= raw `1024`) | **`1/8`**(= raw `8192`) | 用户 2026-10-21 裁定 |
| `C_max` 的来源 | `C_max = 1/n_max`(**派生式**) | **独立裁定值**(与 `n_max` 解耦) | 同上 |
| 耦合约束 `n·C_max ≤ 1` | 成立(满 64 件 = 1.0) | **作废**(满 64 件 = **8.0**) | 同上 |
| int64 算术域上界 | `17.5` | **`24.5`** | `1/2 + 64×(1/8) + 64×(1/4)`;余量约 `5×10⁷` 倍,**结论不变** |
| `n_max = 64` | — | **不变** | 本裁定未触及 |

**改判理由(裁定时的论证,非事后补述)**:`1/64` 使**单条邻接边(`W_max = 1/4`)等于 16 件家具的贡献**
⇒ 家具类型几乎不影响 `env_score`,沦为纯装饰;取 `1/8` 后边:件权重比降为 **2:1**。

### 9.2 改后仍未消解的两处(如实登记,不假装全解决)

1. **案卷建议的负半只有下段可达**:`[−1/4, −1/8]` 中 `−1/8`(= `8192`)✓ 通过,
   `−1/4`(= `16384`)是 `C_max` 的 **2×** ⇒ **仍被拒**。**负半的幅度上段今日仍不可达**。
2. **GDD 药房算例的 `e_env` 仍越界**(`+12000` = 1.46×、`−20000` = 2.44×)——
   这是**第三处冲突**,不在本裁定面内;已在 GDD `:206-214` 就地登记。
   ⚠️ **更早的事实**:该算例的四个值在**旧 `C_max = 1/64` 下同样全部越界** ——
   即它**自 2026-09-20 起就与 F-24-1 的定义域不自洽**,不是本裁定引入的。

### 9.3 落盘面(5 处代码 / 1 处数据 / 1 处文档)

| 落点 | 改动 |
|---|---|
| `ItemDatabaseFurnitureBinder.cs:41-57` | `EnvContributionMax` = `Fix.OneRaw / 8`;`<remarks>` 记裁定 + 理由 + 三项代价 |
| `ClinicFurnitureCatalogue.cs:45` | `EnvContributionMaxRaw = Fix.OneRaw / 8`(跨层同值,由 `cMax_matchesBinder` 钉住) |
| `DataBakeMenu.cs:552` | 日志串 `C_max = 1/8` |
| `ClinicMachineSchema.cs:231` | doc 注释同步 |
| `assets/data/item_database_furniture.json` | 正器 `1/64 → 1/16`(案卷建议下界);`chamber_pot` `−1/64 → −1/8` |
| `design/gdd/clinic-machine.md` | `:188`(自指定义订正)· `:194`(上界重算 24.5)· `:206-214`(算例冲突登记)· `:317`(O-24-2 复核口径)· `:330`(安全区间)· `:334`(旋钮头)· OQ-24-5 / OQ-24-6 两行 —— **全部就地加注,原文不删** |

**重烘**:`unity/Assets/DataCooked/item_database_furniture.cooked.bytes`
`f3abb31d…` → `cd7aedd5f53e9a4d8919976f59f71bf4d498b29a566fcb60456824962d71ae71`(216 B 不变)。
日志判据行:`[大医精诚] 烘焙完成:item_database_furniture.cooked.bytes = 216 B,家具类型 7 件(FURN_TYPES 闭集),tier_max = 3,e_env 界 = ±8192 raw(C_max = 1/8),ConfigVersion = 0x8060E37A`。

### 9.4 证伪轮(2 例,`exactRed`)

**预期先写、后跑**(承「证伪预期必须先写再跑」)。还原一律 `cp` 回 + `sha256sum -c`,**未用 `git checkout`**。

| # | 变异点 | 变异手法 | **预写预期** | 实测 | 判定 |
|---|---|---|---|---|---|
| A | `ItemDatabaseFurnitureBinder.cs:204` 的 `e_env` 越界判据 | 条件前置 `false &&` | `test_furniture_eEnvBeyondCMax_isRejected` 转红 | **恰 1 条红**,正是该测(`26/27`) | ✅ `exactRed` |
| B | `ClinicFurnitureCatalogue.cs:50` 的 `EnvContributionMaxRaw` | `/8` → `/4` | 至少 `cMax_matchesBinder` 红;`rejectsOutOfDomainRow` 亦可能红 | **2 条红**,两条皆中(`25/27`) | ✅ `exactRed` |

**A 的意义**:夹具值在裁定中由 `1/32` 改为 `1/4`(因 `1/32` 在新上界 `1/8` 下**变合法**,判别力会静默归零)。
本证伪证明**改后的夹具仍有判别力** —— 停用门 ⇒ 该测必红,而不是「换了值以后恰好也过」。

**还原校验**:两处均 `sha256sum -c` **成功**(逐字节一致)。

### 9.5 全量回归

`4272 / 4236 / 0 / 36 @ 2026-10-21 12:06 CST · HEAD 009f301 + 工作树`
基线 = S17-2 的 `4272 / 4236 / 0 / 36` ⇒ **逐项相同,零回归**。
(`skipped` 的 36 条为既有 `[Ignore]` 标注项,非本轮新增。)
⚠️ **本数不含 §9.4 的两轮变异跑**(那是 `--filter` 的 27 测子集,不作基线)。

### 9.6 未及核查(禁借绿)

- **IL2CPP 三格 / 跨平台逐位**(ADR-012 矩阵):**NOT-RUN**,本机仅 Mono。<br>⚠️ **2026-10-21 订正:本句「本机仅 Mono」为假** —— ADR-012 §Validation 2026-10-17 已逐格实测:Linux-x64-Mono ✅ 09-24(3/3)· **Linux-x64-IL2CPP ✅ 09-24**(StandaloneLinux64 player 3/3,与独立 Python 金标准 + Mono 三方 19/19 逐位相同)· Linux-ARM64-IL2CPP ❌ **目标不存在**(pinned 工具链 46 个 BuildTarget 无任何 arm 目标;用户裁定长期未达)· CI 常驻 ⚠️ job 已建但从未跑(缺 `UNITY_CLIENT_ID`/`UNITY_CLIENT_SECRET`)。⇒ **矩阵级,非本件个体判据**。原文保留不删。
- **数值定稿**:`1/16` / `−1/8` 是**落在案卷建议区间内的种子值**,不是平衡值 —— 仍归用户数值轮(`OQ-24-3`)。
- **GDD 药房算例未重写** —— 只登记冲突;重写归 FURN_TABLE 内容轮。
- **`TD-itemdb-01`(`cost(m)`)与本裁定无关,仍 `open`** —— 见 §六 7。
