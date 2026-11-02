# 实现原件 —— `TD-skeuo-02` 收口:42 的 UI 载体归 `BootRoot` 层级

> **对象**:`docs/tech-debt-register.md` 的 `TD-skeuo-02`
> **批次**:`S23-19`(用户指令「修 TD-skeuo-02」)· **截至时刻**:见 §四
> **commit**:本批未提交工作树(基于 `7b5bde91`)

---

## 一、病因(改前实测)

`BootRoot.FindOrCreateSkeuoUi` 用 `new GameObject("SkeuoUI")` 建 42 的 UI 载体,**且**用
`FindFirstObjectByType<SkeuoRuntimeDriver>()` **复用**已有实例;该对象**未** `SetParent` 到
`BootRoot`。而 `Boot.unity` 里的预摆壳是与 `BootRoot` **平级的根对象**
(`m_Father: {fileID: 0}`,实测 `Boot.unity` 的 `&5100000002`)⇒ `BootRoot` 销毁后它**存活**
⇒ 下一件 PlayMode 夹具拿到的是**上一件留下的驱动**(`Awake` 不再跑)⇒ 其
`_currentModal` / `_screen` / 注入面**原样带入**。

| # | 事实 | 复算 |
|---|---|---|
| ① | 复用点 + 创建点 | `grep -n "FindFirstObjectByType<SkeuoRuntimeDriver>\|new GameObject(\"SkeuoUI\")" unity/Assets/Gameplay.Boot/BootRoot.cs` |
| ② | 预摆壳是场景根 | `Boot.unity` 的 `SkeuoUI` Transform `m_Father: {fileID: 0}` |
| ③ | 改前零归位 | `grep -c "SetParent.*SkeuoUI" unity/Assets/Gameplay.Boot/BootRoot.cs` ⇒ **0** |
| ④ | 治标面(5 件测试各自拆) | `grep -rln '"SkeuoUI"' unity/Assets/Tests --include=*.cs` ⇒ 6 件(其中 2 件用 `DestroyAllNamed`) |

---

## 二、改了什么

| # | 文件 | 变更 |
|---|---|---|
| 1 | `Gameplay.Boot/BootRoot.cs` | `FindOrCreateSkeuoUi` **两条路径都归位**:预摆件 `IsChildOf` 守卫 + `SetParent(transform)`;自建件 `SetParent(transform)` 后再 `AddComponent` |
| 2 | 同上 · `OnDestroy` | 注释同步(EventSystem **与** SkeuoUI 都随层级销毁,仅清引用) |
| 3 | `Tests/PlayMode/SkeuomorphicUI/skeuo_ui_lifetime_test.cs` | **新增** 2 条判据(见 §三) |

**为何只归位、不重建预摆件**:预摆壳持有 `UIDocument` + `PanelSettings` —— 那是渲染资源的
**唯一来源**(`BootRoot.cs:886-888` 自陈:无 PanelSettings ⇒ `panel == null` ⇒ 逻辑树在但不渲染)。
重建会得到一个空壳 ⇒ 因此「取用即归位」,复用语义保留为「同一 `BootRoot` 内幂等」。

---

## 三、判据(两条,逐条可证伪)

| # | 判据 | 位置 |
|---|---|---|
| ① | **归位**:预摆壳被取用后父级 = 该 `BootRoot`,且**是同一实例**(不重建) | `test_tdSkeuo02_preplacedShell_isParentedUnderBootRoot` |
| ② | **销毁闭合 + 新实例干净态**:先让驱动进 `Pause`(前置正控)⇒ 销毁 `BootRoot` ⇒ **零**驱动存活 ⇒ 新 `BootRoot` 拿到**新实例**,`CurrentModal == None`、`BlocksPlay == false` | `test_tdSkeuo02_destroyingBootRoot_leavesNoDriver_andNextBootGetsFreshState` |

**F4 空洞正控(刻意)**:② 的「`None`」只有在「上一件确实处在 `Pause`」时才具判别力
⇒ 先断言 `OpenModal(Pause)` 成功且 `CurrentModal == Pause`。缺这一步,把 `OpenModal` 删掉本条照样绿。

**与债册原文观测的差异(如实登记)**:债册举的失效观测是「前一件把模态关成 `None` 后,后一件
读到启动态 `Casebook`」。2026-11-02 起启动形态改为**无常驻屏**(`_screenName` 为空 ⇒
`CurrentModal = None`,见 `SkeuoRuntimeDriver.cs:184-189`)⇒ 本件改用**反向同构**的脏态
(`Pause`)。判据性质不变:**非 `None` 的模态态不得跨 `BootRoot` 带入**。

---

## 四、证伪轮

| 变异 | 打什么 | 实测红 | 形态 |
|---|---|---|---|
| **N1** | 删预摆件分支的 `IsChildOf` + `SetParent` | **2 红**(两条判据全红) | **超集**(预摆不归位 ⇒ 销毁后仍存活 ⇒ ③ 的零存活也红) |
| **N2** | 删自建件的 `SetParent` | **1 红**:`自建件同样归 BootRoot 层级` | **exact** |
| **N3** | 永不复用预摆件(`existing = null`) | **2 红**(同一实例 + 前提:bootA 取用的就是预摆那只) | **超集**(复用判据与其前置共用同一观察点) |

**结果:3/3 转红,3/3 SHA-256 逐字节还原**(每条 `RESTORE-OK (sha=2440830…)` +
`FINAL-OK sha 一致`,收尾强制 `grep MUTATION` = 0)。

⚠️ **N2 首轮 NO-XML,是变异写坏不是判据红**:我误把 Python 的 `#` 注释写进 C#,
编译报 `error CS1024: Preprocessor directive expected`。改写成合法的 `//` 注释后重跑,
得 1 红。**该轮读数作废,不引用**。

⚠️ **N1 只红 2 条而非「预摆件一条」**:因为预摆件不归位时,③ 的「销毁后零驱动」也随之破
—— 两者共用同一条因果链,是**超集**而非判据重复。

**脚本本身的两轮失败(如实登记,见 §七)**:v1 的 `$2` 未绑定 bug 导致「Unity 没跑 + 还原没做」,
使变异在盘上跨命令残留;v2 加了锚点预检、逐条 `MUTATION` 强校验与收尾哈希比对。

---

## 五、回归

| 套 | 读数 | 文件 |
|---|---|---|
| **PlayMode 全量** | **`299 / 295 / 0 / 2 / 2`** @ `2026-11-02 12:30:11 CST` | `unity/Logs/skeuo02-pm-full2.xml` |
| **42 侧整类**(含前序夹具,验证清场修复) | **`83 / 83 / 0 / 0 / 0`** @ `2026-11-02 12:28:57 CST` | `unity/Logs/skeuo02-skeuo-class.xml` |
| **本件过滤** | `2 / 2 / 0 / 0 / 0` @ `2026-11-02 12:16:21 CST` | `unity/Logs/skeuo02-lifetime.xml` |

**等式**:`299 − 297 = 2` = 本件新增两条 `[UnityTest]`(基线 `297/293/0/2/2` = 上一批 `S23-18`)。
`passed` 同步 `293 → 295`。**EditMode 未跑** —— 本轮零 EditMode 改动(`BootRoot` 是 PlayMode 面;
本件新夹具在 `Tests/PlayMode/`)。

⚠️ **首轮全量是红的**(`299/294/1` @ 12:25:15),红的正是本件第二条 —— 根因见 §六.0(全局判据撞上
前一夹具的帧末销毁)。修法落地后 42 侧整类 `83/83`、全量 `295/0 failed`,**两次读数都保留在案**,
不以「修好后那次」覆盖「红的那次」。

⚠️ **`PM exit=2` 仍是本仓 PlayMode 常态退出码**(承 `S23-18` 的记录)⇒ 判据取 XML 的 `failed` 计数。

---

## 六、证明不了的事(禁借绿)

0. ⚠️ **首轮全量 PlayMode 我的夹具自己红了一次(已修)** —— 根因不是泄漏,是**断言口径过宽**:
   「销毁后零驱动存活」是**全局**判据,而前一夹具(`skeuo_stylesheet_mount_probe_test`)
   用普通 `[TearDown]` 的 `Object.Destroy` **延迟到帧末** ⇒ 断言当刻它还活着。
   **单独过滤跑是绿的、全量跑是红的** —— 这正是本债的病灶形态(判据依赖执行序),
   而它这次出现在**夹具自己身上**。修法 = `[UnitySetUp]` 先清场并让出一帧,使判据与执行序无关。

1. **实机未走查**:证据是 PlayMode 的真装配(真 `BootRoot` + 真 `SkeuoRuntimeDriver` + 真
   `PanelSettings`),不是玩家按 E 的端到端。
2. **本件不跑启动序**:`BootRoot` 全程 `SetActive(false)` ⇒ 不触 Addressables / 不建世界。
   被测面**只有** `FindOrCreateSkeuoUi` 的归属与生命周期。
3. **旧有 5 件测试的 `DestroyAllNamed` 未删** —— 它们现在是**冗余**而非必需,删不删归后续
   清理轮(本轮不扩大改动面)。
4. **未证明「42 的 UI 载体归位不影响渲染」** —— `SetParent` 对 `UIDocument` 的 `PanelSettings`
   与排序无影响(屏仍渲染),但本轮**没有**截图佐证。

---

## 七、本轮**如实登记**的过程事故(重要)

证伪脚本 v1 有两个 bug,导致**变异残留在盘上跨了两条命令**:

| # | bug | 后果 |
|---|---|---|
| ① | `run_tests` 引用未传的 `$2`(`set -u` 下) | Unity 命令**根本没跑**,每条变异只落盘、无读数 |
| ② | 由 ① 连带,`restore_and_verify` 的 `$2` 同样未绑定 | 还原**未执行**;且第二轮 `snapshot` 把**已污染文件**存成备份 ⇒ 备份自带 3 处 `MUTATION` |

**处置**:从 git HEAD 逐条**逆向剥离**三处变异(锚点唯一命中,`MUTATION` 计数 0),
重建 `BootRoot.cs`,并**新写 v2 脚本**(锚点干跑预检 + 每条结束强制 `grep MUTATION` 为零 +
收尾 `FINAL-OK` 哈希比对)。**v1 的三轮读数全部作废,不引用**。

⚠️ 这与 `feedback_falsify_concurrent_edit_window` 记的教训同族:还原判据必须是
**逐字节哈希**,且脚本本身要先证明它会红 —— v1 恰好是「脚本静默失败 ⇒ 看起来像跑了」。
