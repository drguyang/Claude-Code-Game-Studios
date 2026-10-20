# S16-4 证据原件 —— 「测试不得写仓库」机械门(`FsWriteBoundaryGate`)

**日期**:2026-10-20 · **sprint**:16 · **工作项**:S16-4(retro-14 §Action Items **#4**)
**复杂度**:M · **判据口径**:`/story-done` 式「原判定 → 修复落点 → 验证命令」三段可复算,
**承 TD-epic-02 的最贵教训**:判据是「**改坏生产 ⇒ 断言转红**」,不是「删断言 ⇒ 变绿」。

---

## §一 原判定(为什么立这条门)

retro-14 A4 逐字:

> **「测试/工具不得写仓库」立机械门** —— 本类已三发(S14-6 副作用 · S15-5 的 `WriteOnce` 日期副本 ·
> 更早的 `Directory.CreateDirectory`);规则存在于 `test-standards.md` 但无门。

三发现场(逐条动手核对过,非转述):

| # | 现场 | 形态 | 后果 |
|---|---|---|---|
| ① | `unity/Assets/Tests/EditMode/Audio/pipeline_gate_test.cs`(S14-6 已删) | `Directory.CreateDirectory(repoRoot/assets/audio)` + `File.WriteAllText` | **`assets/audio/` 这个目录今天存在,唯一原因就是这个副作用** |
| ② | `statereport_walkthrough_probe.cs` 的 `WriteOnce`(S15-5) | 往 `docs/audio/` 写**日期命名副本** | 测试跑一次,仓库多一个文件,且**文件名随日期变** ⇒ 不可复现的脏树 |
| ③ | 更早同类 | `Directory.CreateDirectory` | 同上 |

规则的落点是 `.claude/rules/test-standards.md` 第 3 条(Unit tests must not depend on external
state (filesystem, network, database))—— **规则在,执行缺三轮**。

### 立项时**未发现**的一件事实:当时树上正有**在跑**的违规

起草期的实测(不是推理)查出 **2 个活违规**,都是同一种形态 —— 尖刺探针往 `unity/Assets/` 写结果文件:

```
unity/Assets/Tests/PlayMode/u1_scene_spikes_test.cs:99          Candidates.Add(Path.Combine(Application.dataPath, "u1_spike_results.txt"));
unity/Assets/Tests/PlayMode/story007_phase_spike_playmode_test.cs Candidates.Add(Path.Combine(Application.dataPath, "story007_spike_results.txt"));
```

两者都是「主候选写 `unity/Logs/`(gitignored)⇒ 退化时兜底写 `unity/Assets/`」。
`Assets/` 不是 gitignored ⇒ 一旦走到兜底,**测试产物变成 Unity 资产**(会被导入、被 `b` 系列门扫到)。
⇒ 本门不是「为将来立规矩」,而是**当天就抓到了两处**。两处已在 S16-4 删除该候选(见 §二)。

---

## §二 修复落点

### 2.1 门本体(新)

`unity/Assets/Editor.Tools.Gates/FsWriteBoundaryGate.cs`(+ `.meta`),接进
`AssemblyGates.RunAll(".")`(`AssemblyGates.cs`,紧随 `MedConsFloatGate` 之后)。

**扫描面 = `Assets/Tests/**/*.cs`(递归),刻意不含工具程序集** —— 这是**用户裁定**(「仅测试树」)。
理由不是省事:`Editor.Tools.Bake` 有 **35 处** `*.cooked` 写盘,那是 ADR-014 烘焙管线的**法定职责**;
把工具纳入只会逼出一条「整目录白名单」,正是 QA plan 明禁的「白名单 = 全部命中项」自我豁免。

**两条规则(默认放行,命中才红)**:

| 规则 | 判据 | 为什么 |
|---|---|---|
| **R1** | 写盘调用的第一个实参**直接就是字符串字面量** ⇒ 红 | 相对路径按 `cwd` 解析 = 落仓库。零误报面 |
| **R2** | 文件含写盘调用 **且** 含仓库**代码锚点** ⇒ 必须在 `Declarations` 里具名声明(带理由) | 锚点是「能算出仓库位置」的能力;有这种能力又写盘,就必须有人**读过并写下**它写哪儿 |

**代码锚点**(刻意只收 3 个,**不收字面量锚点**):`Application.dataPath` ·
`Directory.GetCurrentDirectory` · `Application.streamingAssetsPath`。

**声明不是豁免** —— 空理由 = 红;声明过期(文件已无锚点)= 红;声明悬空(文件不存在)= 红。
两个反向判据承 `WriterExistenceGate` 的「stale waiver is red」口径,防白名单腐烂。

### 2.2 声明表(6 条,逐条读过代码)

| # | 文件 | 读/写分离情况 |
|---|---|---|
| 1 | `PlayMode/u1_scene_spikes_test.cs` | 锚点仅用于推导 `unity/Logs/`(gitignored);另一候选 `persistentDataPath`;**原 `Assets/` 候选已删** |
| 2 | `PlayMode/story007_phase_spike_playmode_test.cs` | 同 1 |
| 3 | `EditMode/Audio/statereport_walkthrough_probe.cs` | `dataPath` 仅**读** DataCooked / Audio;写走 `Path.GetTempPath()` |
| 4 | `EditMode/PatientAI/presentation_projection_test.cs` | `GetCurrentDirectory` 出现在一条**读**搜索路径列表里;写落本地 tmp |
| 5 | `PlayMode/determinism_golden_crossplatform_test.cs` | `dataPath` 仅**读** golden;写落 `persistentDataPath` |
| 6 | `EditMode/Sim/assembly_gate_b6_test.cs` | `dataPath` 仅**读** `Sim.Codec` 源码;写落 `Path.GetTempPath()`(`tmp` / `tmp/Sub`) |

> ⚠️ **第 6 条是「真树测试抓出来的漏项」,必须留痕**:起草时的抽样只列了
> 「**有代码锚点且无字面量锚点**」的 4 个文件,把「两种锚点都有」的
> `assembly_gate_b6_test.cs` 漏掉了。是真树断言 `test_realTestTree_zeroRed` 把它逼出来的。
> **抽样不是普查** —— 这是「真树断言不可省」的当轮实证,已写进门文件头注。

### 2.3 两处活违规的修法

删掉 `Assets/` 兜底候选,**原文不删、就地加注**:

```csharp
Candidates.Add(Path.Combine(root, "Logs", "u1_spike_results.txt"));  // 主:项目根 Logs/(gitignored 指定暂存区)
// ⚠️ 原「兜底:Assets/」候选已于 **S16-4**(2026-10-20)删除 …… 门 = `FsWriteBoundaryGate`(R2 声明制)。**不要加回来**。
Candidates.Add(Path.Combine(Application.persistentDataPath, "u1_spike_results.txt"));  // 兜底:玩家数据目录
```

`git diff --stat` = `2 files changed, 8 insertions(+), 4 deletions(-)` —— 只有候选行的删除与注释新增,
无其它改动。

### 2.4 规则侧接线

`.claude/rules/test-standards.md` 第 3 条:原文**保留**,就地加注「本条现由机械门执行」+
门的三处判据 + 立门缘由 + **合规写法**(`Path.GetTempPath()` / `persistentDataPath`;唯一例外
`unity/Logs/`,须具名声明)。

---

## §三 判据设计:为什么是「文件级共存」而不是多跳数据流

这一节登记**被否决的两个原型与实测数字**,因为「为什么不用更强的判据」若不留痕,
下一个人会重写一遍同样的失败。

| 原型 | 做法 | 实测结果 | 判决 |
|---|---|---|---|
| v1 | 单跳变量解析(写调用 → 上一层赋值) | **4 个 REPO 命中,全是误报** | 否 |
| v2 | 对 **115** 个调用点做调用图 BFS 数据流 | **2 红,全是误报**(`_tempDir`);而最该抓的 `u1` / `story007` **仍漏** | 否 |

v2 漏抓的根因是结构性的:两者的写目标是 `foreach` 变量,而锚点在**另一个方法**的
`Candidates.Add(...)` 里 —— 静态数据流要跨方法、跨控制流地追,**追不到**。

⇒ 选**文件级共存**:一旦某文件「能算仓库位置」且「会写盘」,就要求人读一遍并具名声明。
代价 = 6 条声明;**收益 = 零误报 + 当天抓到 2 处真违规**。这是**判据强度 vs 白名单规模**的取舍,
写成显式选择,不是偷懒。

---

## §四 证伪轮(判据 = 变异转红)

驱动:`tools/falsify/unity_falsify.py`;**规格**:`tools/falsify/specs/s16-4-fs-write-boundary.json`;
过滤器 `FsWriteBoundaryGateTest`;**退出码 0**。

| # | 变异 | 变异面 | 预期转红 | 实测 | 判 |
|---|---|---|---|---|---|
| **F1** | 注入 `File.WriteAllText(Path.Combine(Application.dataPath, "x.txt"), "y")` 到 `cooked_codec_roundtrip_test.cs` | **测试树**(被扫对象) | `test_realTestTree_zeroRed` | 同 | ✅ exact |
| **F2** | `Declaration decl = FindDeclaration(relPath);` → `null` | **门本体**(停用声明查找) | `test_r2_anchorPlusWrite_withDeclaration_isGreen` + `test_realTestTree_zeroRed` | 同两条 | ✅ exact |
| **F3** | `string[] anchors = FindRepoAnchorsIn(structure);` → `new string[0]` | **门本体**(停用锚点识别) | `test_r2_anchorPlusWrite_withoutDeclaration_isRed` | 同 | ✅ exact |

**`exactRed` 的语义**:不仅「有红」,而是「**转红的集合恰等于预期集合**」——
多红一条(误伤)与少红一条(漏抓)都算不合格。

- F1 是 sprint-16 DoD 的**逐字判据**(「注入一处 `File.WriteAllText` ⇒ 门必须红」)。
- F2 / F3 证明**两条规则各自承重**,不是一条在跑、另一条是装饰。
- 还原:**SHA-256 逐字节校验通过**;指纹未变 —— 受保护路径
  `unity/Assets/AddressableAssetsData` · `unity/Assets/Audio`(新增文件数 0、内容哈希不变)。
- 变异残留:`grep -c "FALSIFY-F"` 对 `cooked_codec_roundtrip_test.cs` 与 `FsWriteBoundaryGate.cs`
  均返回 **0**。

> ⚠️ **如实登记一处口径差异(不借绿)**:QA plan 对 F3 的措辞是「停用门的调用点 ⇒
> F1 的注入不再被拦」。**本驱动不允许叠变异**(每条变异从干净基线出发),故 F3 停在
> **判别力的源头**这一层:停用锚点识别 ⇒ 合成夹具 `test_r2_anchorPlusWrite_withoutDeclaration_isRed`
> 转红。它与「注入不再被拦」是同一件事的合成形态(锚点识别若失效,真树注入的
> `Application.dataPath` 同样认不出),但**本 spec 未在注入态下实测后者** —— 已在 spec 的
> `_note` 里逐字写明。

---

## §五 回归(带基线溯源三元组)

### 5.1 本轮实测

| 字段 | 值 |
|---|---|
| 截至时刻 | **2026-10-20 23:45 CST**(XML mtime 23:41) |
| 批次 | sprint-16 · S16-4 · **全量 EditMode** |
| commit | **`f6cdaaf`**(工作树含未提交改动 —— 见 §5.3) |
| XML | `unity/Logs/s16-4-full-editmode.xml` |
| 结果 | `total 4071 · passed 4035 · failed 0 · inconclusive 0 · skipped 36` |

### 5.2 ⚠️ 基线订正 —— QA plan 的 `3940` 已过期

QA plan §Smoke Test Scope 第 1 条记的基线是 `3940 / 3904 / 0 / 36`。**那是 sprint 规划时刻的快照。**
同一天下午 clinic-machine story-001 的修复轮先期落了两批全量:

| 时刻 | 件 | 计数 |
|---|---|---|
| 2026-10-20 18:44 | `unity/Logs/clinic-001-editmode-final.xml` | `4041 / 4005 / 0 / 36` |
| 2026-10-20 19:10 | `unity/Logs/clinic-001-fix-full.xml` | **`4054 / 4018 / 0 / 36`** |
| 2026-10-20 23:41 | `unity/Logs/s16-4-full-editmode.xml`(本轮) | **`4071 / 4035 / 0 / 36`** |

⇒ **正确的对照基线 = `4054`**,Δ = **+17 / +17 / 0 / 0**。

**为什么必须写这一段**:若直接拿 `3940` 当对照,`Δ = +131`,而本门只新增 17 条 ——
差额 114 会被误读成「本门新增了 114 条」或「有 114 条回归被掩盖」。这正是
`feedback_borrowed_green_regression_counts` 记的第一形态(**照抄陈旧回归数当「含新测的全量」**)。
本轮**不借这个绿**:基线按当日最近一次全量取,且钉死等式 `Δtotal = Δpassed = 新增条数`。

### 5.3 差集实测(不是靠加法推断)

| 项 | 值 |
|---|---|
| 本轮用例集合 | 4071 |
| 前次(19:10)用例集合 | 4054 |
| **新增** | **17**,全为 `FsWriteBoundaryGateTest.*`(逐条列出见下) |
| **消失** | **0** |

新增 17 条:`test_r1_writeTargetIsRelativeLiteral_isRed` · `test_r1_verbatimLiteralTarget_isRed` ·
`test_r1_combineExpression_isNotLiteral_noR1` · `test_r2_anchorPlusWrite_withoutDeclaration_isRed` ·
`test_r2_anchorPlusWrite_withDeclaration_isGreen` · `test_r2_anchorWithNoWrite_isGreen` ·
`test_r2_writeWithNoAnchor_isGreen` · `test_r2_allThreeAnchors_areRecognized` ·
`test_comment_mentioningApi_notCounted` · `test_stringLiteralFixture_invokingApi_notCounted` ·
`test_allDeclaredApiNames_areMatched` · `test_reportedLine_matchesSourceLine` ·
`test_declaration_pointingAtMissingFile_isRed` · `test_declaration_whoseFileLostAnchor_isRed` ·
`test_declarations_allCarryReasons` · `test_realTestTree_zeroRed` · `test_scanDirExists_andIsNonEmpty`。

**「消失 0」是等式之外的一条独立判据** —— 它排除「新增 N 条同时坏了 M 条,而 N−M 恰好等于净增」的掩盖形态。

---

## §六 已知盲区(登记,不借绿)

| # | 盲区 | 触发升级的条件 |
|---|---|---|
| ① | 锚点与写盘**分处两个文件**(util 建路径、测试里写)⇒ 漏 | — |
| ② | **字面量锚点**(`"Assets/…"` / `"docs/…"`)不进 R2 共存规则。实测 **14 个文件**含此类字面量且含写盘调用,但多为**读**路径;强行纳入会新增 **9 条低信号声明** | 写目标**本身**是字面量时由 R1 捕获 |
| ③ | `Path.Combine("..", "..", "docs")` 这类**无锚点**绕行 ⇒ 看不见 | — |

**统一升级触发条件** = 出现**第四例**事故,且形态属上述盲区 ⇒ 按 `EcozoneIntegerGates` 的形状
升 **Cecil IL 级**(该门已用 `Mono.Cecil` 做 IL 级判据,是本仓的更强先例)。

**扫描面本身的边界**(非缺陷,是用户裁定):工具程序集不在扫描面内 —— 理由见 §2.1。

---

## §七 可复算命令清单

```bash
# 1) 门 + 其 17 条测试
cd unity && unity test . --mode EditMode --filter FsWriteBoundaryGateTest --output Logs/s16-4-gate.xml

# 2) 全量 EditMode 回归
cd unity && unity test . --mode EditMode --output Logs/s16-4-full-editmode.xml

# 3) 计数(带三元组)
python3 -c "import xml.etree.ElementTree as ET;r=ET.parse('unity/Logs/s16-4-full-editmode.xml').getroot();\
print({k:r.get(k) for k in ('total','passed','failed','inconclusive','skipped')})"

# 4) 差集(证明 Δ = 新增条数,且消失 0)
python3 - <<'PY'
import xml.etree.ElementTree as ET
def n(f):
    r=ET.parse(f).getroot()
    return {(t.get('fullname') or t.get('name')) for t in r.iter('test-case')}
a,b=n('unity/Logs/s16-4-full-editmode.xml'),n('unity/Logs/clinic-001-fix-full.xml')
print("新增",len(a-b),"消失",len(b-a))
PY

# 5) 证伪轮(变异 → 跑 → 还原,同一进程;退出码 0 = 全中)
python3 tools/falsify/unity_falsify.py --spec tools/falsify/specs/s16-4-fs-write-boundary.json

# 6) 变异残留(应为 0 / 0)
grep -c "FALSIFY-F" unity/Assets/Tests/EditMode/DataPipeline/cooked_codec_roundtrip_test.cs \
                    unity/Assets/Editor.Tools.Gates/FsWriteBoundaryGate.cs

# 7) 扫描面规模(上界:raw grep,含注释与夹具字符串)
grep -rE "\b(File\.(WriteAllText|AppendAllText|WriteAllBytes|WriteAllLines|AppendAllLines|Create|Delete|Move|Copy|Replace)|Directory\.(CreateDirectory|Delete|Move))\s*\(" \
     unity/Assets/Tests --include=*.cs | wc -l     # 131
```

---

## §八 残余与移交

| 项 | 状态 | 去向 |
|---|---|---|
| `unity/Assets/` 下遗留的 `*_results.txt` | **不存在**(实测 `ls` = 无此文件) | 无需清理 |
| 工具侧写盘(35 处 `*.cooked`) | **刻意不扫** | 若将来要管,须另立判据(非 grep 能答),不在本门扩面 |
| 盲区 ①②③ 的 IL 级升级 | **未做** | 触发条件见 §六 |
| 声明表维护 | 新增合法锚点 ⇒ 加声明并写理由;新增违规 ⇒ 修测试 | 门报错文本已逐字写明这两条出路 |

**V 判据自查(禁借绿)**:本件每条断言都有 §四/§五 的实测输出;未跑的项目(IL 级升级、
工具侧判据)明确写「未做」而非「不需要」。
