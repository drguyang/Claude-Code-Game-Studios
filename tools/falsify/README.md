# 证伪轮驱动(`tools/falsify/`)

`/story-done` 的 **Falsifiability Gate**(S12-4,2026-10-15 立)要求:凡命中
**定序 / 共存互斥 / 定义域收窄 / 一次性幂等 / 阈值边界** 五类形态的夹具,
须逐条反证 —— 「停用生产码的对应守卫 ⇒ 该断言转红」;仍绿 = **假绿夹具** = BLOCKING。

此前这些反证一律写在 `/tmp` 的一次性 shell 里,**每次重写、每次靠人记得还原**。

---

## 为什么把它固化成代码

2026-10-17 补口轮实测到「一次性 shell」做法的**具体代价**:

> M1/M3 停用「组缺失 ⇒ `throw`」后,生产码落入分支 ④,而该分支
> **先 `settings.AddLabel(假组名)` 再 `AssetDatabase.SaveAssets()`**
> ⇒ 守卫一破,**失败的测试把两个假标签写进了入库的**
> `unity/Assets/AddressableAssetsData/AddressableAssetSettings.asset`。
> 夹具原有的「无残留」断言只查**组** —— 组确实没建,**建的是标签**。

同型事故在 `production/sprint-status.yaml:149` 已登记过一次(S14-8 静默改写 `audio.asset`)。
⇒ 本驱动把两条纪律固化为**代码**,而不是留在人的记忆里:

| # | 纪律 | 落点 |
|---|------|------|
| ① | **改 → 跑 → 还原在同一进程内完成** | `try/finally`;崩溃也还原 |
| ② | 还原后 **SHA-256 逐字节校验** | `restore_all()` |
| ③ | 跑测后 **受保护路径指纹必须未变** | `guards_fingerprint()` |

③ 的理由:证伪轮的目的**不是**「弄红一个测试」,而是「证明守卫在承重」;
**过程中污染入库资产 = 这次证伪本身有缺陷**,断言是否转红都不作数。

> ⚠️ ③ **刻意不要求「起跑时干净」** —— 开发中的仓本来就常带未提交的正当改动
> (本例:`data-core.asset` 是 S14-6 的既有改动)。要求起跑干净会让驱动在绝大多数
> 真实场景下直接退出码 2 —— 那不是严谨,是**不可用**。
> 正确判据 = **前后指纹相同**:证伪轮不得**新增**任何痕迹。

---

## 用法

```bash
# 干跑:只校验前提(行号 + 原文锚点),不跑 Unity —— 秒级
python3 tools/falsify/unity_falsify.py --spec tools/falsify/specs/<name>.json --dry-run

# 真跑
python3 tools/falsify/unity_falsify.py --spec tools/falsify/specs/<name>.json

# 追加受保护路径(默认见 unity_falsify.py 的 DEFAULT_GUARDS)
python3 tools/falsify/unity_falsify.py --spec … --guard unity/Assets/DataCooked
```

⚠️ **Unity batch 独占工程** —— 起跑前确认编辑器已关(`unity/Temp/UnityLockfile` 不存在)。
本驱动**不**代你查(竞态仍在);它只保证自己不并发。

### ⚠️ 并发读取禁令(2026-10-21 立 · `TD-gate-03`)

**变异窗口内,任何人都不得读被变异的文件 —— 包括只读打开。**

本驱动把变异**写进工作树**(被测物是 Unity 编译产物,内存变异打不到),
故跑的那几分钟里,被变异文件在磁盘上**就是坏的**。此时:

- 读到的是**变异态**,不是真实代码 —— 会据此得出**错误结论**;
- 编辑器 / IDE 的自动重载可能把变异态**编译进去**;
- ⚠️ **驱动侦测不到**。它只校验自己的还原(`restore_all()`)与受保护路径指纹,
  **不校验「期间没人读过」** —— 读文件不留痕,原理上查不出。

**已实测两次**(`TD-gate-03` ②):
① sprint-16 QA 轮,qa-lead 读 `AssemblyGates.cs` 时正好落在编排方变异窗口内;
② C-1 轮,编排方自己在 F2 窗口内读了 `AssemblyGates.cs`,看到 `FALSIFY-F2` 标记。

**纪律**:

| 角色 | 义务 |
|---|---|
| **发令方**(跑驱动的编排方) | 跑之前**声明窗口**,写明「哪几个文件正在被变异」与预计时长 |
| **读者**(agent / 人) | 窗口内**不读**被变异文件;需要读 ⇒ 等窗口关,或改用**已提交版本**(`git show HEAD:<path>`) |

> **`git show HEAD:<path>` 是安全读法** —— 它读的是 git 对象库,不碰工作树。

### 还原判据是 SHA-256,不是标记名

**别指望用 grep 找变异标记来证明「已还原」** —— 那是**事后的、弱的、可绕的**检查
(换个标记名就抓不到;且只能证明「没留下这个字符串」,证明不了「内容真的一致」)。

驱动**真判据**是两条(`:26-28` 表内的 ②③):

- ② `restore_all()` **逐文件 SHA-256 比对**(`unity_falsify.py:152-176`,跑在 `finally` 里);
- ③ `guards_fingerprint()` 受保护路径**前后指纹相同**。

证据件里若出现 `grep -c "FALSIFY-F"`,那是**手工补充复核**,不是驱动的判据 ——
承 `TD-gate-03` 的订正记录(该条原登记「词表有盲区」**经实测不成立**:
驱动从来没有词表,哈希比词表强得多)。

### 退出码

| 码 | 含义 | 处置 |
|---|---|---|
| **0** | 全部变异**恰如预期**转红,且受保护路径指纹未变 | 通过 |
| **1** | 有变异**逃逸**(断言没红 = 假绿夹具),或**红得比预期多**(判别面不清) | 修夹具 |
| **2** | **夹具前提不成立**(目标行原文对不上)= **夹具过期信号**,不是判别力缺口 | 更新 spec 的行号/原文 |
| **3** | **还原或守卫失败**(SHA-256 不符 / 受保护路径被改动) | ⚠️ 最严重,立即人工介入 |

---

## 规格格式(`specs/*.json`)

```json
{
  "name": "人类可读的一轮证伪",
  "mode": "EditMode",                 // EditMode | PlayMode
  "filter": "PipelineGateTest",       // unity test --filter
  "unityProject": "unity",
  "guards": ["unity/Assets/AddressableAssetsData", "unity/Assets/Audio"],
  "mutations": [
    {
      "name": "F1-…",
      "file": "unity/Assets/…/Foo.cs",
      "line": 75,                       // 1-based
      "expect": "            <原文逐字>",  // 对不上 ⇒ 退出码 2
      "replace": "            <变异后>",
      "expectRed": ["test_a", "test_b"],
      "exactRed": true,                 // true ⇒ 红集须**恰好**等于 expectRed
      "_why": "这条守卫为什么承重"
    }
  ]
}
```

**`exactRed: true` 的用意**:只写 `expectRed ⊆ actual` 会让「变异打到了预期之外的断言」
蒙混过关(判别面不清)。边界类 / 定义域类变异应开 `exactRed`。

**每个变异从干净基线出发**(逐个施加、逐个还原,**不叠变异**)——
叠变异会让红的归属无法拆解。

### ⚠️ 写 `expectRed` 的三个坑(2026-10-21 · **第二次复发后成文**)

**坑 1:只想到「新加的那条断言」,漏掉既有的自洽检查也会一起红。**
改 `Kind` / 改清单条目这类变异,**不止**命中新写的那条断言 —— 既有的交叉检查
(如「`TestDriven` 档要求 `Consumer` 是文件路径」)同样会红。
⇒ **写 `expectRed` 时把「这个变异违反了几条既有约束」逐条数一遍**,不是只数新的那条。

**坑 2:处置顺序反了 —— 实得 ≠ 判据。** 首跑红得比预期多时,**不能把实得抄进 `expectRed`**,
那会让 `exactRed` 退化成 tautology(自己和自己比,永远过)。
**正确顺序 = 先逐条论证「它为什么**应该**红」,论证成立后才改预期**,并在 spec 里留
`_first_run_note` / `_expectRed_justification`。

**坑 3:零红的变异 = 假绿,但「红得少」也是问题。** 若某条断言在变异下**保持绿**,
先问:是它本当如此(如取三入口**并集**,删一处并集不变),还是它没判别力?
**前者须登记为 `_known_limit` 并另立变异去证它承重,不许写成「判据有效」。**

> ⚠️ **复发记录(如实记)**:同一个坑 **1** 犯了两次 —— `C-1` 轮 §6.0 是第一次,
> `C-2` 轮 F3 是第二次(`2 红 / 预期 1 红`,驱动判 exit 1)。两次都靠「先论证后改预期」纠正。
> **写到第二次才成文,这本身是流程延迟。**

**坑 4(2026-10-21 立 · S17 批 2 的 M12):手写脚本用锚串替换时,锚串可能不唯一。**

本目录的驱动**不受影响** —— 它按 `line` 行号定位(施加前先核 `lines[ln-1] == 原文`,
不符即 exit 2)。但**临时脚本**(如 `/tmp/fz-*/run*.py`)惯用
`content.replace(old, new, 1)`,而 `replace(..., 1)` 只命中**第一处**:
若锚串在文件里出现 **2 次**,变异会静默打在**另一个函数**上。

- **症状**:`RED=[]` —— 与「夹具无判别力」**一模一样**,极易误判成假绿。
- **实例**:`n.Kind != JsonNodeKind.Integer` 在 `TutorialContentBinder.cs` 出现 2 次
  (`:323` 的 `ReadSchemaVersion` / `:927` 的 `ReadInt`),M12 打中前者 ⇒ 零红;
  定向 `ReadInt` 后(M12b)恰 1 条红。
- **纪律**:施加前 `assert content.count(anchor) == 1`,不满足即**拒跑**;
  `assert anchor in content` **只能防「锚串不存在」,防不住「锚串不唯一」**。

### 并集 / 汇总型断言 —— 单点变异证不了它

若断言取的是**多处来源的并集**(例:三个入口的接线集合并后判「某门在链上」),
则**删掉其中任一处,并集不变 ⇒ 该断言保持绿**。
⇒ 这类断言**不能用「删一行」的变异来证明承重**,须另立一个**能真正破坏并集**的变异
(例:把一个**从未接线**的类谎报成已接线)。

**不得**把「并集恰好没变」写成「判据有效」—— 那是把**巧合**当**证明**。

### 与构建期门(`IPreprocessBuildWithReport`)配合时

构建期门的判据在 `OnPreprocessBuild` 里跑,**本驱动打不到**(它跑 Unity Test)。
要确认「某门真的在构建期执行」须用**探针轮** —— 做法与归因三件套见
根 `CLAUDE.md` §Unity Debugging → 「构建前门(BuildGate)—— 绿 = 没报错,不等于门跑了」。

---

## 与 `tools/kindgen/mutation_probe.py` 的分工

| | `kindgen/mutation_probe.py` | 本驱动 |
|---|---|---|
| 变异施加在 | **内存**(monkeypatch) | **工作树**(必须 —— 被测物是 Unity 编译产物) |
| 依赖 | 纯 Python + PyYAML | **Unity batch**(独占工程) |
| CI 常驻 | ✅(`tests.yml` 的 `kindgen-mutation-probe`) | ❌ 桌面轮(需 Unity license) |
| 风险面 | 零(不碰工作树) | **有**(故需 ①②③ 三条纪律) |

两者**互补不替代**:kindgen 那侧能在 CI 常驻,因为它变异的是纯 Python 判据;
Unity 侧判据只能走桌面 + 本驱动。

---

## 现存规格

| spec | 覆盖 | 变异数 | 立档 |
|---|---|---|---|
| `specs/audio-import-settings-and-playmode.json` | OQ-44-2 Import Load Type 门 + TD-audio-01f Play Mode Script 门 + wav 扩展名跨平台大小写 | **5**(F1–F5) | 2026-10-17 |
| `specs/s16-4-fs-write-boundary.json` | `FsWriteBoundaryGate`(S16-4 写盘边界门):F1 注入仓库写盘到测试树(R1/R2 判别面如常)· F2 停用声明查找 · F3 停用锚点识别 | **3**(F1–F3) | 2026-10-20 |
| `specs/c1-gate-wiring.json` | 门链接线判据(C-1 / `TD-gate-01`+`-02`):F1 删 `RunAll` 里一行 · F2 删清单一条声明 · F3 停用解析器 | **3**(F1–F3) | 2026-10-21 |
| `specs/c2-il-gate-wiring.json` | 6 / 10 两个 IL 门接进 BuildGate(C-2):F1 删 Ecozone 接线 · F2 删 Emergency 接线 · F3 已上线门改判回 `TestDriven` · F4 `PodTypeScanner` 谎报 `Chain` | **4**(F1–F4) | 2026-10-21 |

> **C-2 规格的两处口径说明(2026-10-21)**:
> ① **F1 / F2 证不了 `chainKindEntriesAreActuallyWired`** —— 该断言取**三入口并集**,
> 而 Ecozone / Emergency 在 `RunMenu` 与 `BuildGate` **两处**都接了 ⇒ 删任一处并集不变。
> ⇒ 另立 **F4**(把从未接线的 `PodTypeScanner` 谎报成 `Chain`)去真正证明那条断言承重。
> **不让「并集恰好没变」冒充「判据有效」。**
> ② **F3 首跑红 2 条而我只预期 1 条**,驱动判 exit 1。处置 = 逐条论证「为什么**应该**红」,
> 论证成立后才改期望(见 spec 的 `_expectRed_justification`)—— **没有直接把实得抄进
> `expectRed`**,那会让 `exactRed` 退化成 tautology(承 C-1 轮同款纪律)。

> **S16-4 规格的口径说明(2026-10-20)**:F2 / F3 打的是**门本体**而非被扫对象,
> 证明两条规则(R2 声明制 / 锚点识别)**各自承重**;F1 打被扫**测试树**,是
> `sprint-16.md` DoD 的逐字判据(「注入一处 `File.WriteAllText` ⇒ 门必须红」)。
> ⚠️ 本驱动**不允许叠变异**,故 QA plan 原措辞「停用门的调用点 ⇒ F1 的注入不再被拦」
> **未在叠加态实测**;F3 停在「判别力源头」这一合成形态,已在 spec 的 `_note` 里如实登记。

> **F5 的由来(2026-10-17 自查)**:本驱动**自己发现不了**它 —— 那是**语义**缺陷(漏扫),
> 不是**路径**污染,指纹守卫看不见。⇒ 它是人复核本轮新落生产件时抓到的,
> **正是「守卫 ≠ 自查」的实例**(见本目录 README 的「判据口径」条)。
