#!/usr/bin/env python3
"""AC 编号撞号门 —— `TD-docs-01` 的唯一机械载体(2026-11-02 立)。

## 为什么有这个工具

`AC-20-NN` 这一串 ID **同时属于两个编号空间**:

- `ADR-020`(玩家控制器与相机)自有 `AC-20-01…17`
- **系统 20 库存 GDD**(`inventory-and-items.md`)自有 `AC-20-01…25`

⇒ 17 个 ID 每一位都有两个意思,两侧都是 BLOCKING。根因不是「历史遗留没办法」,
是 **ADR-020 Amendment C(2026-11-01)铸新 ID 时没查重**(库存那批 2026-10-26
就已在被引用)。

用户 2026-11-02 **先裁 ①「零改名」,同日改判 ②「全 17 个改名」**:
ADR 自有的 AC 一律写 **`AC-ADR0NN-NN`**(ADR-019 已于 S23-35 同款处理,
`AC-19-01…08` → `AC-ADR019-01…08`),系统 GDD 的维持 `AC-{sys}-{n}`。
本工具是该裁定的**执行体** —— 它不要求任何人「记得查重」,而是让违规当场红。

## 判据(十条)

- **C1 新撞号**:某 ID 被 ≥2 个文件「定义」,且不在基线里 ⇒ 红
- **C2 基线不得扩大**:基线里的 ID 不得被**第三个**文件再定义 ⇒ 红
- **C3 基线不得陈旧**:基线里的 ID 必须**仍然是**撞号(否则说明有人改了名 /
  删了定义)⇒ 红,迫使基线随之刷新
- **C4 空集守卫**:扫描到的定义行数为 0 ⇒ 红(路径写错会让 C1 在空集上恒绿,
  这是本仓抓过的「空洞正控」形态)
- **C5 引用面混写**:同一文件里同时出现 `AC-ADR<n>-x` 与同名裸 `AC-<n>-x` ⇒ 红
- **C6 白名单陈旧**:两张白名单里的文件已不再命中 ⇒ 红(白名单不许永久掩护)
- **C7 ADR 用裸名铸 AC**:`adr-<n>-*.md` 里以定义句式写 `AC-<n>-<m>` ⇒ 红
  (**撞号的生产线**;C1 要等系统 GDD 成文才发作,C7 在铸的当时就红)
- **C8 裸名误引 ADR 自有 AC**:活文档里裸 `AC-<n>-<m>` 实为 ADR 自有 AC ⇒ 红
  (C5 的盲区:文件里**一处新名都没有**时 C5 结构性沉默)
- **C9 story 级 AC 旧形**:出现 3 位编号的 `AC-0NN-M` ⇒ 红(一律写 `SAC-0NN-M`)
- **C10 ADR 用了系统 AC 的定义形**:ADR 里以**粗体**(`- [ ] **AC-x-y**` / `- **AC-x-y**`)
  写 `AC-<sys>-<n>` ⇒ 红 —— **不看 GDD 有没有这个 ID**。
  ADR 只能**引用**系统 AC,引用形 = **反引号**(`` - [ ] `AC-x-y` ``)。
  自有 AC 写 `AC-ADR0NN-NN`(C7 管)

⚠️ **C8 只认 1–2 位编号位** —— 第三个编号空间(story 级 AC)已由 **C9** 单独接管:
统一 `SAC-0NN-M`。三个空间自此从**字符串层面**可分:

| 写法 | 归属 | 例 |
|---|---|---|
| `AC-ADR0NN-NN` | ADR 自有 AC | `AC-ADR019-06` |
| `AC-<sys>-<n>` | 系统 GDD 的 AC | `AC-20-03`(库存) |
| `SAC-0NN-M` | **story 级 AC** | `SAC-018-4` |

## 行级 `legacy` 标记(历史引用的合法出口)

「原文不删」要求记录件保留改名前的写法,它们落进活文档就会被 C5 / C8 判成漏改。
用**行内标记** `<!-- ac-id-gate: legacy … -->` 豁免:**范围 = 那一行** · **自清理**
(行删了标记跟着没)· **可见**(每次 `--check` 都列进「提示」,不静默)。

## 「定义」怎么认(不靠语义,靠两种固定句式)

- GDD 表行:`| **AC-20-01** | **[A]** | ...`
- ADR 校验清单:`- [ ] **AC-20-01(BLOCKING)**:...`

**定义源按编号空间分工**(S23-37):

| ID 形状 | 谁能定义 | 句式 |
|---|---|---|
| `AC-<sys>-<n>` | `design/gdd/`(顶层,不含 `reviews/`) | 表行 · 复选框 · **`## Acceptance Criteria` 节内项目符号** |
| `AC-ADR<adr>-<n>` | `docs/architecture/adr-<adr>-*.md`(且 adr = 本 ADR 号) | 表行 · 复选框 |

**ADR 里出现 `AC-<sys>-<n>` 一律算引用,不算定义** —— `## Validation Criteria` 列的是
「本 ADR 验证哪些 AC」。实测:全库 **0 个 ID 只由 ADR 定义**(两边都有的 9 个全是
ADR-018 复述 44 GDD 的 `AC-44-*`)。

**记法区分(2026-11-02 立)**:同一个 ID,**粗体 = 定义形,反引号 = 引用形**。
ADR 里用粗体写系统 AC 一律由 **C10** 判红 —— 因为读者分不出那是「复述」还是
「第二定义」,而同号异义正是 ADR-020 当年的形态。

⚠️ **`## GDD Requirements Addressed` 整节跳过** —— 该节的表是**引用**别的系统的 AC,
不是定义。ADR-033 就有一条 `| **AC-23-02** | ...`,按逐行扫会被误判成第二处定义
(本工具首版原型实测踩到过)。

## 用法

```bash
python3 tools/acid/acid_check.py --check           # CI / 提交前:违规即退出码 1
python3 tools/acid/acid_check.py --print           # 打印全部定义与撞号
python3 tools/acid/acid_check.py --mutation-probe  # 内存内变异,自证有判别力
```
"""

import argparse
import json
import os
import re
import sys
from collections import defaultdict

REPO_ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
BASELINE = os.path.join(os.path.dirname(os.path.abspath(__file__)), "acid-baseline.json")

SCAN_ROOTS = ("design/gdd", "docs/architecture")

# 定义句式(两种,均为**行首固定形态**;不做行内匹配,避免把散文引用算成定义)
RE_GDD_ROW = re.compile(r"^\|\s*\*\*(AC-\d+-\d+)\*\*\s*\|")
RE_ADR_BOX = re.compile(r"^- \[ \] \*\*(AC-\d+-\d+)")
RE_HEADING = re.compile(r"^##\s+(.*)$")

# 该节整节跳过:它是**引用**别的系统 AC 的表,不是定义
SKIP_SECTION_MARK = "GDD Requirements Addressed"

# ── C5 / C6:引用面判据(2026-11-02 改名轮后追加)────────────────────────────────
# 改名把 ADR-020 的 AC 变成 `AC-ADR020-NN`,系统 20 的仍是裸 `AC-20-NN`。
# ⇒ **同一个文件里同时出现「`AC-ADR{n}-x`」与「裸 `AC-{n}-x`」= 两种语义混在一处**,
# 这是改名漏改 / 新写误用的指纹。纯语义文件应当**只有一种**。
RE_ADR_AC = re.compile(r"AC-ADR(\d+)-(\d+)")

# ── C7 / C8:ADR 自有 AC 的「不得裸名」判据(2026-11-02 S23-35 追加)────────────
# 病因:C5 判的是「**混写**」,不是「**正确**」。一个文件若一处新名都没有、只是把 ADR 自有的
# AC 全写成裸名 ⇒ C5 **结构性沉默**(实测漏过 8 处)。ADD-019 的潜伏撞号更是连 C1 都抓不到
# (系统 19 无 GDD ⇒ 只有一处定义)。
# ⇒ 拆成两面:**C7 管定义侧**(ADR 不得再用裸名铸 AC)· **C8 管引用侧**(活文档里
# 裸 `AC-{n}-{m}` 与 `ADR-{n}` 同句出现,且 m ∈ 该 ADR 的自有 AC 集 ⇒ 指错)。
RE_ADR_FILE = re.compile(r"^adr-(\d+)-")
RE_ADR_BARE_DEF_BOX = re.compile(r"^- \[ \] \*\*AC-(\d+)-(\d+)")
RE_ADR_BARE_DEF_ROW = re.compile(r"^\|\s*\*\*AC-(\d+)-(\d+)\*\*\s*\|")
RE_ADR_MENTION = re.compile(r"ADR-?0*(\d+)")
# ADR 自有 AC(`AC-ADR0NN-NN`)的定义句式 —— `RE_GDD_ROW` / `RE_ADR_BOX` 只吃纯数字 ID
RE_ADR_OWN_ROW = re.compile(r"^\|\s*\*\*(AC-ADR\d+-\d+)\*\*\s*\|")
RE_ADR_OWN_BOX = re.compile(r"^- \[ \] \*\*(AC-ADR\d+-\d+)")
RE_BARE_AC = re.compile(r"AC-(\d{1,2})-(0*\d+)(?!\d)")
# C9:story 级 AC 的旧形。`(?<![A-Za-z])` 防 `SAC-018-4` 被当成 `AC-018-4` 再判一次。
RE_STORY_AC_OLD = re.compile(r"(?<![A-Za-z])AC-(0\d\d)-(\d+)(?!\d)")
# AC 节内的项目符号定义(仅用于 C8a 的「系统侧有无此 AC」判定,不进 C1 的定义集)
RE_H2 = re.compile(r"^##\s+(.*)$")
RE_AC_BULLET = re.compile(r"^- \*\*(AC-\d+-\d+)(?![\d-])")
RE_ADR_OWN_DEF = re.compile(r"^- \[ \] \*\*AC-ADR(\d+)-(\d+)|^\|\s*\*\*AC-ADR(\d+)-(\d+)\*\*\s*\|")

# C8 的扫描面 = **活文档**(不含记录件:债册 / sprint / session-state / evidence / retro)。
# 记录件按「原文不删」纪律合法保留历史写法,把它们拉进来只会制造噪音。
LIVE_DOC_ROOTS = (
    "design/", "docs/architecture/", ".claude/docs/", "production/epics/",
    "unity/Assets/", "tests/",
)
# 白名单:**确实需要**在活文档里并存「ADR 名 + 裸同名 AC」的文件(逐条给理由)。
# C6 会检查它不得陈旧 —— 文件不再命中就必须删条。
BARE_ADRREF_ALLOW = {}

# ── 行级「历史引用」豁免标记(2026-11-02)────────────────────────────────────
# 「原文不删」纪律要求记录件保留改名前的写法,但它们若落在活文档里,就会被 C5 / C8 判成漏改。
# ⇒ 用**行级标记**而不是**文件级白名单**:
#   ① 豁免范围 = 那一行,不会顺手掩护同文件里将来真漏改的地方;
#   ② **自清理** —— 行删了标记跟着没,不存在「白名单腐烂」;
#   ③ **可见** —— 每次 `--check` 都会把带标记的行列进「提示」,不是静默豁免。
# ⚠️ 只给「记录历史」用。新写的引用**不许**靠它过关。
LEGACY_MARK = "ac-id-gate: legacy"
RE_LEGACY_MARK = re.compile(r"ac-id-gate:\s*legacy")


def split_legacy(lines):
    """返回 (有效行, [(行号, 原文)]) —— 带 legacy 标记的行被摘出并回报。"""
    live, marked = [], []
    for idx, line in enumerate(lines, 1):
        if RE_LEGACY_MARK.search(line):
            marked.append((idx, line.strip()))
            live.append("")
        else:
            live.append(line)
    return live, marked

# 引用面扫描范围(比定义扫描宽:代码 / 数据 / 文档都算)
BARE_SCAN_SUFFIXES = (".md", ".cs", ".yaml", ".yml", ".json", ".txt")
BARE_SKIP_DIRS = {".git", "Library", "Logs", "Temp", "obj", "node_modules", "session-logs"}

# 白名单:**确实需要同时持有两种写法**的文件(逐条给理由)。
# ⚠️ 加条目之前先问:这处裸名是不是**没改干净**?
BARE_MIXED_ALLOW = {
    ".claude/docs/coding-standards.md":
        "**规则文件本身**:写法表的左右两栏就是「新名 / 裸名」对照,必须都出现",
    "docs/tech-debt-register.md":
        "记录 TD-docs-01 自身:裸名描述改名前状态,新名描述改名后 —— 两种都要留",
    "docs/architecture/tr-registry.yaml":
        "混杂:系统 1/2 的 TR 引 ADR-020(新名),系统 20 的 TR 引库存(同号裸名)",
    "docs/architecture/traceability-index.md":
        "同上 —— 一张索引里同时有相机行与库存行,且号有重合",
    "design/gdd/death-and-respawn.md":
        "29 引 ADR-020 位移边界(新名)+ 引 20 的 InventoryOf 重建(裸名)—— 同号不同物",
    "design/gdd/foraging.md":
        "17 引库存 fold 判据(裸名)+ 引 ADR-020 反射断言先例(新名)—— 同号不同物",
    "design/gdd/interaction-system.md":
        "4 的 GDD 早先对**同一个撞号**的登记(原文不删)+ 引 ADR-020 判据(新名)",
    "design/gdd/prescription-and-medication.md":
        "11 同款:库存 fold(裸名)+ ADR-020 反射断言先例(新名)",
    "production/sprints/sprint-23.md": "本债的实施记录:改名前后两种写法都要写",
    "production/session-state/active.md": "会话状态:同上",
    "production/qa/evidence/impl-td-docs-01-ac-id-gate-2026-11-02.md":
        "第一轮原件:冻结时是 17 条撞号(裸名),结案后补记了新名",
    "production/qa/evidence/impl-td-docs-01-ac-rename-2026-11-02.md":
        "改名轮原件:逐条语义判定,两侧写法都要引用",
}


def scan(root):
    """返回 {ac_id: [相对路径, ...]}(路径已排序,便于逐字节比对)。

    **定义源按编号空间分工**(2026-11-02 裁定后的口径,S23-37):

    | ID 形状 | 谁能定义 | 定义句式 |
    |---|---|---|
    | `AC-<sys>-<n>` | **`design/gdd/`**(顶层 GDD,不含 `reviews/`) | 表行 · 复选框 · **`## Acceptance Criteria` 节内的项目符号** |
    | `AC-ADR<adr>-<n>` | **`docs/architecture/adr-<adr>-*.md`**,且 adr 必须 = 本 ADR 号 | 表行 · 复选框 |

    **ADR 里出现 `AC-<sys>-<n>` 一律算引用**,不算定义 —— `## Validation Criteria` 列的是
    「本 ADR 验证哪些 AC」。实测依据:全库 **0 个 ID 只由 ADR 定义**(9 个两边都有的
    全是 ADR-018 复述 44 GDD 的 `AC-44-*`)。铸号一侧由 **C7**(用本 ADR 的号)与
    **C10**(该 ID 在 GDD 里根本不存在)兜住。
    """
    found = defaultdict(set)

    # ── 系统 AC:只认 design/gdd 顶层(不含 reviews/)──────────────────
    gdd_root = os.path.join(root, "design/gdd")
    for dirpath, dirnames, filenames in os.walk(gdd_root):
        dirnames[:] = [d for d in dirnames if d != "reviews"]
        for name in sorted(filenames):
            if not name.endswith(".md"):
                continue
            path = os.path.join(dirpath, name)
            rel = os.path.relpath(path, root).replace(os.sep, "/")
            section = ""
            skip = False
            with open(path, encoding="utf-8", errors="replace") as handle:
                for line in handle:
                    heading = RE_HEADING.match(line)
                    if heading:
                        section = heading.group(1)
                        skip = SKIP_SECTION_MARK in section
                        continue
                    if skip:
                        continue
                    match = RE_GDD_ROW.match(line) or RE_ADR_BOX.match(line)
                    if match is None and "Acceptance Criteria" in section:
                        match = RE_AC_BULLET.match(line)
                    if match:
                        found[match.group(1)].add(rel)

    # ── ADR 自有 AC:只认 docs/architecture/adr-<own>-*.md ─────────────
    adr_root = os.path.join(root, "docs/architecture")
    for name in sorted(os.listdir(adr_root)):
        own = RE_ADR_FILE.match(name)
        if not name.endswith(".md") or not own:
            continue
        rel = f"docs/architecture/{name}"
        skip = False
        with open(os.path.join(adr_root, name), encoding="utf-8", errors="replace") as handle:
            for line in handle:
                heading = RE_HEADING.match(line)
                if heading:
                    skip = SKIP_SECTION_MARK in heading.group(1)
                    continue
                if skip:
                    continue
                for match in (RE_ADR_OWN_ROW.match(line), RE_ADR_OWN_BOX.match(line)):
                    if match and match.group(1).startswith(f"AC-ADR{int(own.group(1)):03d}-"):
                        found[match.group(1)].add(rel)

    return {key: sorted(value) for key, value in found.items()}


def scan_adr_system_forms(root):
    """C10 的输入:ADR 里以**定义句式**出现的系统 AC(`AC-<sys>-<n>`)。

    它们不算定义(见 `scan` 的说明),但**必须真的存在于某个 GDD** —— 否则就是
    「ADR 铸了一个别处不存在的系统 AC」,与 ADR-020 / ADR-019 同型的生产线。
    """
    out = []
    adr_root = os.path.join(root, "docs/architecture")
    for name in sorted(os.listdir(adr_root)):
        if not name.endswith(".md") or not RE_ADR_FILE.match(name):
            continue
        rel = f"docs/architecture/{name}"
        skip = False
        for idx, line in enumerate(
                open(os.path.join(adr_root, name), encoding="utf-8", errors="replace"), 1):
            heading = RE_HEADING.match(line)
            if heading:
                skip = SKIP_SECTION_MARK in heading.group(1)
                continue
            if skip:
                continue
            match = RE_GDD_ROW.match(line) or RE_ADR_BOX.match(line)
            if match is None:
                match = RE_AC_BULLET.match(line)
            if match:
                out.append((rel, idx, match.group(1)))
    return out


def collisions(definitions):
    return {ac: files for ac, files in definitions.items() if len(files) > 1}


def scan_bare_mix(root):
    """返回 {相对路径: [撞号的系统号, ...]} —— 同一文件里同时有 ADR 新名与同名裸名。"""
    mixed = {}
    for dirpath, dirnames, filenames in os.walk(root):
        dirnames[:] = [d for d in dirnames if d not in BARE_SKIP_DIRS]
        for name in sorted(filenames):
            if not name.endswith(BARE_SCAN_SUFFIXES):
                continue
            path = os.path.join(dirpath, name)
            rel = os.path.relpath(path, root).replace(os.sep, "/")
            try:
                text = open(path, encoding="utf-8", errors="replace").read()
            except OSError:
                continue
            text, _ = split_legacy(text.splitlines())
            text = "\n".join(text)
            # ⚠️ 必须按**完整 ID** 比,不能只比系统号:一个文件里有 `AC-ADR020-05`
            # 和另一条无关的 `AC-20-18`(库存的)并不构成混写 —— 两者指的根本不是同一条。
            # 首版原型按系统号比,把 17 个大杂烩文件全判成混写(实测踩到)。
            news = {(int(m.group(1)), int(m.group(2))) for m in RE_ADR_AC.finditer(text)}
            if not news:
                continue
            both = [f"AC-{num}-{idx:02d}" for num, idx in sorted(news)
                    if re.search(rf"AC-{num}-0*{idx}(?![0-9])", text)]
            if both:
                mixed[rel] = both
    return mixed


def scan_adr_bare_defs(root):
    """C7:ADR 自有 AC 不得用裸名铸。返回 [(相对路径, 行号, AC ID)]。

    判据 = 在 `adr-<n>-*.md` 里,以定义句式出现 `AC-<n>-<m>`(n = 本 ADR 号)。
    ⇒ 这就是 ADR-019 那颗雷的**铸点**;C1 只能等到系统 GDD 成文才发作,C7 铸的当时就红。
    """
    hits = []
    base_dir = os.path.join(root, "docs/architecture")
    for name in sorted(os.listdir(base_dir)):
        if not name.endswith(".md"):
            continue
        match = RE_ADR_FILE.match(name)
        if not match:
            continue
        adrno = int(match.group(1))
        rel = f"docs/architecture/{name}"
        for idx, line in enumerate(
                open(os.path.join(base_dir, name), encoding="utf-8", errors="replace"), 1):
            box = RE_ADR_BARE_DEF_BOX.match(line) or RE_ADR_BARE_DEF_ROW.match(line)
            if box and int(box.group(1)) == adrno:
                hits.append((rel, idx, f"AC-{adrno}-{int(box.group(2)):02d}"))
                continue
            own = RE_ADR_OWN_ROW.match(line) or RE_ADR_OWN_BOX.match(line)
            if own:
                k = int(re.match(r"AC-ADR(\d+)-", own.group(1)).group(1))
                if k != adrno:
                    hits.append((rel, idx, own.group(1)))
    return hits


def scan_bare_adrrefs(root):
    """C8:裸 `AC-<n>-<m>` 其实是 ADR 自有 AC 的两条路径。

    **C8a(精确,不看上下文)**:该 ID **只**作为 ADR 自有 AC 存在 ——
    即系统 n 的 GDD 里没有定义 `AC-<n>-<m>`(系统 19 今日零定义 ⇒ 8 条全中)。
    裸串本身就无处可指 ⇒ 必是 ADR 引用。

    **C8b(启发式,看上下文)**:系统 n 确实定义了 `AC-<n>-<m>`,但同一行里点了 `ADR-<n>`
    ⇒ 该处极可能指 ADR。这一条**可能误报**,所以只作为提示面 (C8)。

    ⚠️ **只认 1–2 位的编号位**:`AC-(\\d{1,2})-…`。库里存在**第三个编号空间**——
    **story 号**写法 `AC-0NN-M`(如 `story-019` 的 `AC-019-6`)。若把 3 位也当系统/ADR 号,
    `AC-019-6` 会被误读成 ADR-019 的 06(实测首版就是这样:10 个 audio 文件全是假阳性)。
    ⇒ `AC-0NN-M` 归 story 空间,本判据不碰;**它的撞号面单独登记,不假装已覆盖**。

    返回 {相对路径: [命中描述, ...]}。
    """
    owned = defaultdict(set)
    system_defined = defaultdict(set)
    # ⚠️ 定义句式**比 C1 宽**:C1 只认表行 + 复选框,但系统 1 / 2 / 44 的 GDD 用
    # `- **AC-1-01(BLOCKING)** ——` 这种**项目符号**定义 ⇒ 只认前两种的话,
    # `system_defined` 会偏小 ⇒ C8a **多报**。这里补第三种:**仅限 `## Acceptance Criteria`
    # 节内**(其余位置的加粗 ID 多为引用,实测放开就会把 review log / ADR 复述算成定义)。
    # ⚠️ 本节**只影响 C8a 的假阳性率**,不改 C1 的定义集 —— C1 要不要认 bullet 是另一个裁定。
    for dirpath, _dirnames, filenames in os.walk(os.path.join(root, "design/gdd")):
        for name in sorted(filenames):
            if not name.endswith(".md"):
                continue
            sect = ""
            for line in open(os.path.join(dirpath, name), encoding="utf-8", errors="replace"):
                head = RE_H2.match(line)
                if head:
                    sect = head.group(1)
                    continue
                hit = RE_GDD_ROW.match(line) or RE_ADR_BOX.match(line)
                if hit is None and "Acceptance Criteria" in sect:
                    hit = RE_AC_BULLET.match(line)
                if hit:
                    t, seq = hit.group(1).split("-")[1:]
                    system_defined[int(t)].add(int(seq))

    base_dir = os.path.join(root, "docs/architecture")
    for name in sorted(os.listdir(base_dir)):
        match = RE_ADR_FILE.match(name)
        if not name.endswith(".md") or not match:
            continue
        adrno = int(match.group(1))
        text = open(os.path.join(base_dir, name), encoding="utf-8", errors="replace").read()
        for m in RE_ADR_AC.finditer(text):
            if int(m.group(1)) == adrno:
                owned[adrno].add(int(m.group(2)))
    if not owned:
        return {}

    found = defaultdict(list)
    for root_dir in LIVE_DOC_ROOTS:
        for dirpath, dirnames, filenames in os.walk(os.path.join(root, root_dir)):
            dirnames[:] = [d for d in dirnames if d not in BARE_SKIP_DIRS]
            for name in sorted(filenames):
                if not name.endswith(BARE_SCAN_SUFFIXES):
                    continue
                path = os.path.join(dirpath, name)
                rel = os.path.relpath(path, root).replace(os.sep, "/")
                try:
                    lines = open(path, encoding="utf-8", errors="replace").read().splitlines()
                except OSError:
                    continue
                lines, _marked = split_legacy(lines)
                for idx, line in enumerate(lines, 1):
                    nums = {int(g) for g in RE_ADR_MENTION.findall(line)}
                    for target, seq in RE_BARE_AC.findall(line):
                        t, sq = int(target), int(seq)
                        if sq not in owned.get(t, ()):
                            continue
                        if sq not in system_defined.get(t, ()):
                            # C8a:该裸 ID 在系统侧根本不存在 ⇒ 必是 ADR 引用
                            found[rel].append(
                                f"L{idx}: 裸 `AC-{t}-{sq:02d}` 在系统 {t} 侧无定义,"
                                f"只作为 ADR-{t} 自有 AC 存在")
                        elif t in nums:
                            # C8b:系统侧也有同号,但同行点了 ADR-{t} ⇒ 极可能指 ADR
                            found[rel].append(
                                f"L{idx}: 裸 `AC-{t}-{sq:02d}` 与 `ADR-{t}` 同句"
                                f"(该 ADR 自有此 AC)")
    return dict(found)


def scan_legacy_story_ac(root):
    """C9:3 位编号的 `AC-0NN-M` 一律拒收 —— story 级 AC 必须写 `SAC-0NN-M`。

    病因:story 级 AC 原用 `AC-0NN-M`(3 位零填充 story 号),与 2 位的 `AC-<n>-<m>` 肉眼同形,
    而系统 9 / 18 / 19 / 20 / 21… 都存在。例:`AC-019-6`(story-019 第 6 条)与
    `AC-19-06`(ADR-019 第 6 条)只差一个 `0` 的位置。
    ⇒ 2026-11-02 用户裁定改为 **`SAC-` 前缀**,从字符串层面分开三个空间;旧形全部拒收。
    """
    hits = []
    for dirpath, dirnames, filenames in os.walk(root):
        dirnames[:] = [d for d in dirnames if d not in BARE_SKIP_DIRS]
        for name in sorted(filenames):
            if not name.endswith(BARE_SCAN_SUFFIXES):
                continue
            path = os.path.join(dirpath, name)
            rel = os.path.relpath(path, root).replace(os.sep, "/")
            if rel == "tools/acid/acid_check.py":      # 本工具自己的正则与报文
                continue
            try:
                lines = open(path, encoding="utf-8", errors="replace").read().splitlines()
            except OSError:
                continue
            lines, _marked = split_legacy(lines)
            for idx, line in enumerate(lines, 1):
                for m in RE_STORY_AC_OLD.finditer(line):
                    hits.append((rel, idx, m.group(0)))
    return hits


def scan_legacy_marks(root):
    """列出所有带 legacy 标记的行 —— 豁免必须**可见**,不能静默。"""
    out = []
    for dirpath, dirnames, filenames in os.walk(root):
        dirnames[:] = [d for d in dirnames if d not in BARE_SKIP_DIRS]
        for name in sorted(filenames):
            if not name.endswith(BARE_SCAN_SUFFIXES):
                continue
            path = os.path.join(dirpath, name)
            rel = os.path.relpath(path, root).replace(os.sep, "/")
            try:
                lines = open(path, encoding="utf-8", errors="replace").read().splitlines()
            except OSError:
                continue
            _live, marked = split_legacy(lines)
            for idx, _text in marked:
                out.append(f"{rel}:{idx}")
    return sorted(out)


def load_baseline():
    with open(BASELINE, encoding="utf-8") as handle:
        return json.load(handle)


def evaluate(definitions, baseline, bare_mixed=None,
             adr_bare_defs=None, bare_adrrefs=None, story_ac_old=None,
             adr_sys_forms=None):
    """返回 (violations, warnings)。violations 非空 ⇒ 退出码 1。

    `bare_mixed` 为 None 时不判 C5 / C6;C7 / C8 / C9 同理(内存探针用)。
    """
    violations = []
    dups = collisions(definitions)
    grandfathered = baseline["known_collisions"]

    # C4 空集守卫 —— 先判,否则 C1 会在空集上恒绿
    defined_lines = sum(len(files) for files in definitions.values())
    if defined_lines == 0:
        violations.append(
            "C4 空集守卫:扫描到 0 条 AC 定义行 ⇒ 扫描路径或句式可能已失效"
            f"(roots={list(SCAN_ROOTS)})。空集上的『无撞号』不算通过。")

    # C1 新撞号
    for ac in sorted(dups):
        if ac not in grandfathered:
            violations.append(
                f"C1 新撞号:{ac} 被 {len(dups[ac])} 个文件同时定义为 AC —— "
                f"{dups[ac]}。\n"
                "    修法(用户 2026-11-02 裁定 ①):ADR 自有的 AC 写 `ADR-0NN AC-NN`,\n"
                "    系统 GDD 的维持 `AC-{sys}-{n}`;**不要**再铸一个同名 ID。")

    # C2 基线不得扩大
    for ac, recorded in sorted(grandfathered.items()):
        actual = dups.get(ac)
        if actual is None:
            continue
        for path in actual:
            if path not in recorded:
                violations.append(
                    f"C2 基线撞号扩大:{ac} 的既有定义方是 {recorded},"
                    f"现在又多了 `{path}` ⇒ 撞号面变大。")

    # C3 基线不得陈旧
    for ac, recorded in sorted(grandfathered.items()):
        actual = dups.get(ac)
        if actual is None:
            violations.append(
                f"C3 基线陈旧:{ac} 仍记在基线里,但它已不再被 ≥2 个文件定义 ⇒ "
                "撞号已消除(改名 / 删定义)。请从 `acid-baseline.json` 删掉该条,"
                "让基线跟上现状 —— 否则基线会永久掩盖一个已不存在的问题。")
        elif sorted(actual) != sorted(recorded):
            violations.append(
                f"C3 基线定义方不符:{ac} 记为 {sorted(recorded)},实测 {sorted(actual)}。"
                "若是有意的改名 / 挪动,同步更新基线;否则先查清。")

    # C5 / C6:引用面(只有调用方给了 bare_mixed 才判)
    if bare_mixed is not None:
        for rel in sorted(bare_mixed):
            if rel not in BARE_MIXED_ALLOW:
                nums = ", ".join(str(x) for x in bare_mixed[rel])
                violations.append(
                    f"C5 引用面混写:`{rel}` 同时出现 `AC-ADR<号>-…` 与同名裸 `AC-<号>-…`"
                    f"(系统号 {nums})。\n"
                    "    两种语义混在同一文件 = 改名漏改或新写误用的指纹。\n"
                    "    **先查清每一处是哪个语义**:ADR 自有改 `AC-ADR0NN-NN`,\n"
                    "    库存维持裸名。确实是两种都要留的,才加进 `BARE_MIXED_ALLOW` 并写明理由。")
        for rel in sorted(BARE_MIXED_ALLOW):
            if rel not in bare_mixed:
                violations.append(
                    f"C6 白名单陈旧:`{rel}` 仍记在 `BARE_MIXED_ALLOW` 里,"
                    "但它已不再同时持有两种写法 ⇒ 从白名单删掉,别让它永久掩护一个已消失的混写。")

    # C7 / C8:ADR 自有 AC 的裸名面(调用方给了才判)
    if adr_bare_defs is not None:
        for rel, idx, ac in adr_bare_defs:
            violations.append(
                f"C7 ADR 用裸名铸 AC:`{rel}:{idx}` 以定义句式出现 `{ac}` —— "
                "这正是 ADR-019 / ADR-020 撞号的生产线。\n"
                "    ADR 自有的 AC 一律写 `AC-ADR0NN-NN`;裸 `AC-<n>-<m>` 专归系统 n 的 GDD。\n"
                "    ⇒ C1 只在系统 GDD 成文那天才发作,**本条在铸的当时就红**(防潜伏)。")
    if bare_adrrefs is not None:
        for rel in sorted(bare_adrrefs):
            if rel in BARE_ADRREF_ALLOW:
                continue
            detail = ";".join(bare_adrrefs[rel][:3])
            more = "" if len(bare_adrrefs[rel]) <= 3 else f" …共 {len(bare_adrrefs[rel])} 处"
            violations.append(
                f"C8 裸名误引 ADR 自有 AC:`{rel}` —— {detail}{more}。\n"
                "    该 AC 属 ADR 自有 ⇒ 应写 `AC-ADR0NN-NN`;裸 `AC-<n>-<m>` 指系统 n 的 GDD。\n"
                "    ⚠️ C5 看不见这一类:文件里若**一处新名都没有**,就不构成「混写」。\n"
                "    确实是历史引用且必须留的,才加进 `BARE_ADRREF_ALLOW` 并写明理由。")
        for rel in sorted(BARE_ADRREF_ALLOW):
            if rel not in bare_adrrefs:
                violations.append(
                    f"C6 白名单陈旧:`{rel}` 仍记在 `BARE_ADRREF_ALLOW` 里,"
                    "但它已不再命中 ⇒ 从白名单删掉,别让它永久掩护一个已消失的误引。")

    # C9:story 级 AC 的旧形
    if story_ac_old is not None:
        for rel, idx, ac in story_ac_old:
            violations.append(
                f"C9 story 级 AC 旧形:`{rel}:{idx}` 出现 `{ac}` —— "
                "3 位编号的 AC 一律归 story 空间,须写 `SAC-0NN-M`。\n"
                "    `AC-0NN-M` 与 2 位的 `AC-<n>-<m>` 肉眼同形(如 `AC-019-6` vs `AC-19-06`),\n"
                "    而系统 9 / 18 / 19 / 20 / 21 都存在 ⇒ 三个空间必须从字符串层面分开。")

    # C10:ADR 里出现**定义形**的系统 AC —— 一律红(不看 GDD 有没有)
    if adr_sys_forms is not None:
        for rel, idx, ac in adr_sys_forms:
            violations.append(
                f"C10 ADR 用了系统 AC 的定义形:`{rel}:{idx}` 出现 `{ac}`。\n"
                "    系统 AC 的户主是 `design/gdd/` —— ADR **只能引用,不能铸**。\n"
                "    引用形 = **反引号**(`AC-0-NN`),定义形 = **粗体**(`**AC-0-NN**`)。\n"
                "    本条不查「GDD 里有没有这个 ID」:即便有,ADR 用定义形也会让人读不出\n"
                "    「这是复述」还是「这是第二定义」—— 同号异义正是 ADR-020 当年的形态。\n"
                "    ADR 自有的 AC 请写 `AC-ADR0NN-NN`(C7 管)。")

    # 基线覆盖度提示(非违规):让「还剩多少撞号」始终可见
    warnings = []
    remaining = len(grandfathered)
    if remaining:
        warnings.append(
            f"仍存 {remaining} 个已知撞号(基线冻结;见 TD-docs-01)。"
            "本门只保证**不再新增**,不声称它们已解决。")
    return violations, warnings


def cmd_print(definitions, baseline):
    dups = collisions(definitions)
    print(f"定义行所在唯一 AC ID: {len(definitions)}")
    print(f"撞号(≥2 文件定义): {len(dups)}(基线冻结 {len(baseline['known_collisions'])})")
    for ac in sorted(dups):
        mark = "基线" if ac in baseline["known_collisions"] else "**新增**"
        print(f"  [{mark}] {ac}")
        for path in dups[ac]:
            print(f"          {path}")
    return 0


def cmd_check(definitions, baseline, bare_mixed, adr_bare_defs, bare_adrrefs,
              story_ac_old, adr_sys_forms):
    violations, warnings = evaluate(definitions, baseline, bare_mixed,
                                    adr_bare_defs, bare_adrrefs, story_ac_old,
                                    adr_sys_forms)
    for note in warnings:
        print(f"提示:{note}")
    marks = scan_legacy_marks(REPO_ROOT)
    if marks:
        print(f"提示:legacy 豁免 {len(marks)} 行(历史引用,原文不删)—— "
              f"{', '.join(marks[:5])}{' …' if len(marks) > 5 else ''}")
    if not violations:
        print(f"✅ AC 编号撞号门:无新增撞号;引用面 {len(bare_mixed)} 个混写文件全在白名单内;"
              f"ADR 自有 AC 裸名定义 0 条、裸名误引 {len(bare_adrrefs)} 文件全在白名单内;"
              f"story 级 AC 旧形 0 处;ADR 铸号 0 处")
        return 0
    for item in violations:
        print(f"❌ {item}")
    print(f"\n共 {len(violations)} 项违规。工具说明见本文件头部注释。")
    return 1


def cmd_mutation_probe(definitions, baseline):
    """内存内变异,证明四条判据各自真的会红(**完全合成,不依赖仓库现状**)。

    ⚠️ **为什么不拿真实定义集做底**:2026-11-02 改名轮后基线**已是空集**,
    早期版本用 `sorted(base["known_collisions"])[0]` 取一个真基线 ID 当靶,
    在空基线上直接 `IndexError` —— 探针自己先崩了。⇒ 改为合成一套最小定义集,
    与仓库现状解耦:M1–M4 测的是**判据逻辑**,真实接线由 `--check` + 证伪轮的
    R1–R3(真文件变异)覆盖。
    """
    FAKE_BASE = {
        "known_collisions": {
            "AC-99-01": ["design/gdd/fake-a.md", "docs/architecture/adr-099-fake.md"],
        }
    }
    FAKE_DEFS = {
        "AC-99-01": ["design/gdd/fake-a.md", "docs/architecture/adr-099-fake.md"],
    }

    scenarios = []

    def probe(label, mutate, expect_prefix):
        mutated_defs, mutated_base = mutate()
        violations, _ = evaluate(mutated_defs, mutated_base)
        hit = any(item.startswith(expect_prefix) for item in violations)
        scenarios.append((label, expect_prefix, hit, violations))

    def copy_defs(defs):
        return {ac: list(files) for ac, files in defs.items()}

    # M1:凭空造一个新撞号(不在基线里)
    def m1():
        defs = copy_defs(FAKE_DEFS)
        defs["AC-99-02"] = ["design/gdd/fake-b.md", "docs/architecture/adr-099-fake.md"]
        return defs, FAKE_BASE

    # M2:让基线 ID 的定义方多一个文件(撞号面扩大)
    def m2():
        defs = copy_defs(FAKE_DEFS)
        defs["AC-99-01"] = defs["AC-99-01"] + ["design/gdd/some-third-file.md"]
        return defs, FAKE_BASE

    # M3:基线 ID 只剩一个定义方(撞号已消除 ⇒ 基线陈旧)
    def m3():
        defs = copy_defs(FAKE_DEFS)
        defs["AC-99-01"] = defs["AC-99-01"][:1]
        return defs, FAKE_BASE

    # M4:空集守卫
    def m4():
        return {}, FAKE_BASE

    probe("M1 新撞号", m1, "C1")
    probe("M2 基线扩大", m2, "C2")
    probe("M3 基线陈旧", m3, "C3")
    probe("M4 空集守卫", m4, "C4")

    # ── C5 / C6:引用面 ───────────────────────────────────────────────────
    def probe_bare(label, mixed, expect_prefix):
        violations, _ = evaluate(FAKE_DEFS, FAKE_BASE, mixed)
        hit = any(item.startswith(expect_prefix) for item in violations)
        scenarios.append((label, expect_prefix, hit, violations))

    # M5:白名单外的文件同时持有两种写法 ⇒ 混写
    probe_bare("M5 引用面混写",
               {**{k: [20] for k in BARE_MIXED_ALLOW}, "design/gdd/some-new-file.md": [20]},
               "C5")

    # M6:白名单里的文件不再混写 ⇒ 白名单陈旧
    probe_bare("M6 白名单陈旧",
               {k: [20] for k in list(BARE_MIXED_ALLOW)[1:]},
               "C6")

    # ── C7 / C8:ADR 自有 AC 的裸名面 ──────────────────────────────────────
    def probe_adr(label, defs_hits, refs_hits, expect_prefix):
        violations, _ = evaluate(FAKE_DEFS, FAKE_BASE,
                                 {k: [20] for k in BARE_MIXED_ALLOW},
                                 defs_hits, refs_hits)
        hit = any(item.startswith(expect_prefix) for item in violations)
        scenarios.append((label, expect_prefix, hit, violations))

    # M7:ADR 用裸名铸 AC
    probe_adr("M7 ADR 裸名铸 AC",
              [("docs/architecture/adr-099-fake.md", 1, "AC-99-01")], {},
              "C7")
    # M8:活文档里裸名误引 ADR 自有 AC
    probe_adr("M8 裸名误引 ADR 自有 AC", {},
              {"design/gdd/some-new-file.md": ["L1: 裸 `AC-99-01` 与 `ADR-99` 同句"]},
              "C8")

    # M9:story 级 AC 写成旧形
    violations, _ = evaluate(FAKE_DEFS, FAKE_BASE,
                             {k: [20] for k in BARE_MIXED_ALLOW}, {}, {},
                             [("design/gdd/some-new-file.md", 1, "AC-018-4")])
    hit = any(item.startswith("C9") for item in violations)
    scenarios.append(("M9 story 级 AC 旧形", "C9", hit, violations))

    # M10:ADR 铸了一个 GDD 里不存在的系统 AC
    violations, _ = evaluate(FAKE_DEFS, FAKE_BASE,
                             {k: [20] for k in BARE_MIXED_ALLOW}, {}, {}, {},
                             [("docs/architecture/adr-099-fake.md", 1, "AC-44-99")])
    hit = any(item.startswith("C10") for item in violations)
    scenarios.append(("M10 ADR 铸不存在的系统 AC", "C10", hit, violations))


    failed = 0
    for label, expect, hit, violations in scenarios:
        status = "红(符合预期)" if hit else "**未红 —— 该判据无判别力**"
        print(f"[{label}] 期望命中 {expect} ⇒ {status}")
        if not hit:
            failed += 1
            for item in violations:
                print(f"        实际违规:{item.splitlines()[0]}")
    print("PROBE-OK" if failed == 0 else f"PROBE-FAILED({failed})")
    return 0 if failed == 0 else 1


def main(argv):
    parser = argparse.ArgumentParser(description="AC 编号撞号门(TD-docs-01)")
    group = parser.add_mutually_exclusive_group()
    group.add_argument("--check", action="store_true", help="违规即退出码 1(CI 用)")
    group.add_argument("--print", action="store_true", help="打印全部定义与撞号")
    group.add_argument("--mutation-probe", action="store_true", help="内存内变异自证判别力")
    args = parser.parse_args(argv)

    definitions = scan(REPO_ROOT)
    baseline = load_baseline()
    bare_mixed = scan_bare_mix(REPO_ROOT)
    adr_bare_defs = scan_adr_bare_defs(REPO_ROOT)
    bare_adrrefs = scan_bare_adrrefs(REPO_ROOT)
    story_ac_old = scan_legacy_story_ac(REPO_ROOT)
    adr_sys_forms = scan_adr_system_forms(REPO_ROOT)

    if args.print:
        return cmd_print(definitions, baseline)
    if args.mutation_probe:
        return cmd_mutation_probe(definitions, baseline)
    return cmd_check(definitions, baseline, bare_mixed, adr_bare_defs, bare_adrrefs,
                     story_ac_old, adr_sys_forms)


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
