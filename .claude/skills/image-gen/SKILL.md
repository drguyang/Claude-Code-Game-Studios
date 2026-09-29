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
1. MOST IMPORTANT: the pages must be mostly BLANK. Remove the dense handwriting.
   Keep only a short title and 3-4 sparse characters in the top-left. At least 80%
   of the ruled lines must be empty. An empty line means "not yet filled in".
2. Replace the Western hardcover with thread binding: paper stitching and visible
   thread loops along the spine, no rigid cover.
3. ...
```

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

4. **No ImageMagick or ffmpeg on this box.** PIL only. It is at
   `/XYFS01/sysu_tyu2_2/miniconda3/bin/python3`.

5. **Bash occasionally fails** with `claude-sonnet-5 is temporarily unavailable, so
   auto mode cannot determine the safety of Bash`. This is a harness hiccup, not a user
   denial — just re-run the identical command.

6. **"空行即答案" discipline.** Any paper UI asset in this project defaults to *blank
   ruled lines*, not filled content. The generated image must model the empty state.
   This is the single most-missed requirement — flag it every round.

---

## Committing results

Reference art goes in `assets/design-references/`, committed normally. The API key never
is — it lives only in the gitignored `.claude/sensenova.json`. See
`memory: project-addressable-assetsdata-never-commit` for the sibling rule about
machine-generated Unity data.

Per `memory: feedback-commit-without-asking`, commit once self-checked — do not ask per
image.
