---
name: image-gen
description: "Generates and iterates AI reference art (concept art, asset reference images, UI mockups) via the SenseNova U1.5 Fast image API. Handles text-to-image generation, image-to-image editing of an existing reference, downloading results, and generating small thumbnails for visual self-review. Use when producing concept art, asset reference images, or when the user asks to look at / fix a generated image."
argument-hint: "<prompt> [--ref <path>] [--size 2048x2048] [--out assets/design-references/<name>.png] [--extend true|false] [--thumb]"
user-invocable: true
allowed-tools: Read, Glob, Grep, Write, Edit, Bash, AskUserQuestion
model: sonnet
---

Generates reference art and iterates on it until it matches the spec. The two things
that make this workflow work: **always review the result yourself before showing the
user**, and **never Read a large PNG directly into context** (see Thumbnail rule).

---

## Phase 0: Setup

**API key** — read from `.claude/sensenova.json` (gitignored, mode 600, never commit it):

```bash
cat .claude/sensenova.json
# {"base_url":"https://token.sensenova.cn/v1","api_key":"sk-...","model":"sensenova-u1.5-fast",...}
```

If the file is missing, ask the user for a key (https://console.sensecore.cn) and
create it — **do not** write the key into any file that git tracks.

Load config once per session into a shell variable:

```bash
export SN_CFG=$(cat .claude/sensenova.json)
export SN_KEY=$(python3 -c "import json,os;print(json.loads(os.environ['SN_CFG'])['api_key'])")
```

**Model**: `sensenova-u1.5-fast` (always; do not substitute).

---

## Thumbnail rule (read this before any Read of an image)

A 2048×2048 PNG costs **~660k tokens** to Read. The context limit is 262k. Reading one
directly **will** blow the session, and compaction will fail the same way.

```bash
python3 -c "
from PIL import Image
im = Image.open('assets/design-references/foo.png')
im.thumbnail((700,700))          # PIL's thumbnail() is aspect-preserving
im.convert('RGB').save('/tmp/foo-thumb.png')
print('thumb', im.size)
"
```

Always `convert('RGB')` — alpha or palette modes blow up token count too.

Review the thumbnail. Only Read the full-resolution file if the user asks a question
that genuinely needs pixel detail.

---

## Phase 1: Parse arguments

| Arg | Meaning | Default |
|-----|---------|---------|
| `<prompt>` | the image prompt (required) | — |
| `--ref <path>` | reference image → switches to **i2i edit** mode | none → t2i |
| `--size WxH` | 512–4096, multiple of 32, max 3:1 | `2048x2048` |
| `--out <path>` | output PNG path | `assets/design-references/<slug>.png` |
| `--extend true\|false` | `prompt_extend` | `true` for t2i, `false` for i2i |
| `--thumb` | write a `/tmp/*-thumb.png` | always done internally |

If no prompt given and no `--ref`, ask what image they want.

---

## Phase 2: Build the request

Write a Python heredoc to build the JSON payload. **Always use `response_format:
"b64_json"`** — the URL form expires in 24h and the download is a second failure point.

### Text-to-image

```python
import json, os
json.dump({
    "model": "sensenova-u1.5-fast",
    "prompt": PROMPT,
    "size": "2048x2048",
    "n": 1,                       # n=1 is the only supported value
    "response_format": "b64_json",
    "output_format": "png",
    "watermark": True,
    "prompt_extend": True,
}, open("/tmp/gen_req.json", "w"))
```

### Image-to-image edit (`--ref`)

Embed the source as a data URI. For a 6 MB PNG this makes a ~9 MB request body — fine,
but pass it with `--data-binary @file`, never inline on the command line.

```python
import base64, json
src = "assets/design-references/casebook-paper-ref.png"
b64 = base64.b64encode(open(src, "rb").read()).decode()
json.dump({
    "model": "sensenova-u1.5-fast",
    "images": [{"image_url": "data:image/png;base64," + b64}],   # ≤5 refs
    "prompt": EDIT_PROMPT,
    "size": "2048x2048",
    "n": 1,
    "response_format": "b64_json",
    "output_format": "png",
    "watermark": True,
    "prompt_extend": False,        # see Gotcha 1
}, open("/tmp/edit_req.json", "w"))
```

---

## Phase 3: Send it

```bash
curl -s -w "\nHTTP %{http_code}\n" -X POST "$SN_BASE/images/generations" \
  -H "Authorization: Bearer $SN_KEY" \
  -H "Content-Type: application/json" \
  --data-binary @/tmp/gen_req.json \
  --max-time 600 -o /tmp/resp.json
```

- `images/generations` for t2i, `images/edits` for i2i.
- `--max-time 600` — these take 60–180s; the default timeout will kill it.
- On non-200, print the response body and stop. Do not silently retry.

Decode and save:

```python
import base64, json
from PIL import Image
d = json.load(open("/tmp/resp.json"))
raw = base64.b64decode(d["data"][0]["b64_json"])
im = Image.open(__import__("io").BytesIO(raw))
im.save(OUT)                      # normalizes mode + strips nothing
print("saved", im.size, im.mode, len(raw))
```

For an **edit round**, do not overwrite the source. Write `-v2`, `-v3`, … and keep every
version on disk — the artist may want to see the progression.

---

## Phase 4: Review it yourself — do not skip

**The model can see images. It must look at every result before reporting.** Reporting
"done, image saved" without looking is the failure mode this phase exists to prevent.

1. Thumbnail (Phase 0 rule).
2. Read the thumbnail.
3. Judge against the **spec file**, not against vibes. For this project the judging
   rubric is `design/assets/specs/<name>.md` — the Visual Description block, the Color
   Palette table, and the Interaction States table.
4. Also judge against the art bible identity (`design/art/art-bible.md` §1): 水墨 × 黄铜
   dual-track, 纸面 dominant, no glossy photoreal drift.

Produce an explicit verdict table — what landed, what didn't, ranked. Be willing to
conclude it does **not** match; that is the useful answer.

### Report shape

| # | Spec requirement | Result |
|---|---|---|
| 1 | … | ✅ landed |
| 2 | … | ❌ still failing — why |

Then offer the next round. Never present a mediocre result as done.

---

## Phase 5: Iterate

Each round: state the failures → write a numbered correction prompt → i2i edit → review.

**Correction prompt shape** — number the fixes, put the most important first, and say
what to do rather than what to avoid:

```
1. MOST IMPORTANT: preserve the complete traditional casebook page layout — all ruled
   lines, four diagnostic section headers, seal stamp positions, and gutter binding
   must remain visible. This is a material reference board, not a blank template.
2. Replace the Western hardcover with thread binding: paper stitching and visible
   thread loops along the spine, no rigid cover.
3. ...
```

> **Note (2026-09-29 workflow update)**: The project now uses AI-generated **complete
> composition reference boards** (full layout with ruled lines, headers, and placeholder
> text positions), then slices by material semantics into independent texture layers.
> Dynamic text is rendered via UXML `<Label>` overlay at runtime. The correction prompt
> must preserve layout completeness — do NOT ask for "blank" or "empty" pages.

---

## Phase 6: 归档与清理 —— 审核结论必须落到文件系统

**触发**：一组概念图的**人工审核结束**（用户裁定「这张可以 / 这张不行」）。**每组做一次，落定即收口。**

理由：一组图迭代到第 5 版是常态，而其中往往只有一版可用。全部留盘时，
「哪张是定稿」只能靠记忆和文件名猜；全部删掉又不留因，下一轮会重犯同一批错。
⇒ 定稿归档、废版删除、**判读表留下**，三件事一起做才算收口。

### 两条硬规则

| 审核结论 | 动作 |
|---|---|
| **纳入**（经审核采用） | 移入 `assets/design-references/<对象>/approved/<slug>.png`（连带 `-thumb`）。**归档即去版本号** —— `-v4` 改成 `<slug>` |
| **不采用**（经审核否决） | **从盘上删除**（连带 `-thumb`）。**不建 `rejected/` 目录** —— 留一个没人看的回收站等于没删 |

### 目录形态

```
assets/design-references/<对象>/
├── approved/
│   ├── <slug>.png            ← 定稿。此目录内**永不出现 -vN**
│   └── <slug>-thumb.png
├── <slug>.png                ← 在审版本(未裁 ⇒ 保留)
└── <slug>-v2.png …           ← 迭代版(审核后按上表两条处置)
```

### 三条边界（防误删 / 防误档）

1. **`审核` 的主体是用户，不是我的判读。** 我自己写「v4 可用 / v5 否决」**不构成审核** ——
   未经用户开口的版本一律**保留**，挂着等裁。把自评当审核等于替用户做删除决定。
2. **判读表是删除的前提，不是删除的替代。** 删之前，规格里必须已经有一张判读表写明
   「哪一版、为什么被否、下一版改了什么」。**没有判读表的组，不得删除任何文件** ——
   否则否因永久丢失，下一轮必然重犯（实测：`scene-interior-a` 的
   v2 白斑 → v3 剥没了 → v4 才对，这条链只存在于 `church-interior.md` §4.1 里）。
3. **只动本组目录、只动自己生成的文件。** 删除范围 = `design-references/<对象>/` 下、
   本次迭代产出的 `.png` / `-thumb.png`。**不碰** `approved/` 之外的既有参考件、
   不碰 `unity/Assets/`、不做 `find` 批量删除。

### 归档 / 删除后必写回

在规格的判读表里给每一版标出处置：`→ approved/<slug>.png` 或 `已删（审核不采用）`。
`approved/` 里有什么，读文档就能推出来；盘上有什么，不必反查。

> ⚠️ `assets/design-references/` 整体被 `.gitignore:241` 忽略（实测
> `git check-ignore -v assets/design-references/church-interior/approved/scene-interior-a.png`
> 命中同一行）⇒ `approved/` 同样不入库，删除用普通 `rm`（不是 `git rm`，该目录不被跟踪）。

---

## Gotchas (learned the hard way)

1. **`prompt_extend` fights you on i2i.** The extender takes "make it sparse" and
   re-injects "handwritten Chinese characters" detail. Set `prompt_extend: false` on
   every edit round. Leave it `true` for t2i where you want the enrichment.

2. **i2i edits a wrong base into a more-wrong result.** If the v1 miss is structural
   (wrong object, wrong composition, wrong text density), stop editing and re-generate
   with t2i. i2i can only adjust; it cannot un-choose a composition.

3. **"Remove X" underperforms "make it Y."** Positive phrasing ("keep only a short
   title and 3-4 characters") lands where negations ("no handwriting, no text, empty
   pages") get quietly ignored.

4. **No ImageMagick or ffmpeg on this box.** PIL only, from the default `python3`
   (conda base env — discover it rather than hardcoding:
   `python3 -c "import sys, PIL; print(sys.executable, PIL.__version__)"`).
   ⚠️ The absolute path previously recorded here,
   `/XYFS01/sysu_tyu2_2/miniconda3/bin/python3`, **no longer exists** (verified
   2026-10-30 — the whole `/XYFS01` mount is absent on this box now, cause not
   established here). Do not re-add absolute interpreter paths to this skill.

5. **Bash occasionally fails** with `claude-sonnet-5 is temporarily unavailable, so
   auto mode cannot determine the safety of Bash`. This is a harness hiccup, not a user
   denial — just re-run the identical command.

6. **"空行即答案" discipline.** Any paper UI asset in this project defaults to *blank
   ruled lines*, not filled content. The generated image must model the empty state.
   This is the single most-missed requirement — flag it every round.

---

## Committing results

Reference art goes in `assets/design-references/` and is **never committed** — the directory
is in `.gitignore` (2026-09-29). Generated images are local intermediates, not repo assets:
5–6 MB each, unreproducible without re-spending an API call, and each version means something
different depending on which spec round produced it — committing them invents version authority
that never existed. The spec is the truth; the image is a thing you look at while making it.

The API key is likewise never committed — it lives only in the gitignored `.claude/sensenova.json`.
See `memory: project-addressable-assetsdata-never-commit` for the sibling rule about
machine-generated Unity data.

When a spec change is the real deliverable, commit **the spec edit** and leave the PNGs on disk.

**定稿的落点是 `approved/`，不是仓库** —— 见 Phase 6。归档不等于入库：`approved/` 一样在
`.gitignore` 里，它解决的是「哪一版是定稿」，不是「怎么分发」。
