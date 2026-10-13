---
name: image-vision
description: "Analyzes images with the SenseNova vision model (sensenova-6.8-flash-lite). Reads local screenshots, mockups, UI captures, or any image path and answers questions about what's in them. Use instead of the built-in Read tool for images — the Read tool's image handling is broken on this box. Also useful for comparing an image against a spec file and reporting visual mismatches."
argument-hint: "<image-path> --prompt <question> [--format text|json]"
user-invocable: true
allowed-tools: Read, Glob, Grep, Write, Edit, Bash, AskUserQuestion
model: sonnet
---

Sister skill to `image-gen` (same SenseNova account, same config file, same tooling).
`image-gen` makes images; this skill **reads images**. The built-in Read tool cannot
handle images on this box — always use this skill for anything visual.

The job is simple: embed the image, send it to the vision model, return the answer.

---

## Phase 0: Setup (identical to image-gen)

**API key** — read from `.claude/sensenova.json` (gitignored, mode 600, never commit it):

```bash
export SN_CFG=$(cat .claude/sensenova.json)
export SN_KEY=$(python3 -c "import json;print(json.loads(os.environ['SN_CFG'])['api_key'])")
export SN_BASE=$(python3 -c "import json;print(json.loads(os.environ['SN_CFG'])['base_url'])")
```

If the file is missing, ask the user for a key (https://console.sensecore.cn) and
create it — **do not** write the key into any file git tracks.

**Default model**: `sensenova-6.8-flash-lite` (fast, cheap, vision-capable). Can be
overridden via `--model <name>` if a bigger multimodal model is available.

---

## Phase 1: Parse arguments

| Arg | Meaning | Default |
|-----|---------|---------|
| `<image-path>` | **local image file** (screenshot, mockup, capture) | required |
| `--prompt <text>` | the question about the image | `"描述这张图片里有什么"` |
| `--model <name>` | override the vision model | config's model |
| `--format text\|json` | response style | text |

Local paths are resolved relative to the **project root** (cwd of the Bash session).
The **user's global CLAUDE.md** forbids the built-in Read tool for images — route
everything through this API.

> ⚠️ **Remote URLs do NOT work.** The model rejects an `https://` image URL with
> `HTTP 400 inference request is invalid` (verified 2026-10-13). Always download the
> image to a local file first, then embed it as a base64 data URI (Phase 2). The
> official SenseNova example also uses a local file, not a URL.

---

## Phase 2: Build the payload

### Local image → data URI (the only working form)

Embed via `data:image/...;base64`, using a Python heredoc — never inline base64 on
the command line (a few MB of base64 is unreadable and risks shell limits):

```python
import base64, json, os, mimetypes
src = "/abs/path/to/shot.png"          # resolve to abspath first
b64 = base64.b64encode(open(src, "rb").read()).decode()
mime = mimetypes.guess_type(src)[0] or "image/png"
payload = {
    "model": MODEL,
    "messages": [
        {"content": "你是图像识别专家", "role": "system"},
        {"content": [
            {"type": "text", "text": PROMPT},
            {"type": "image_url", "image_url": {"url": f"data:{mime};base64,{b64}"}}
        ], "role": "user"}
    ],
    "n": 1,
    "max_tokens": 1000,
    "reasoning_effort": "none",
}
json.dump(payload, open("/tmp/vision_req.json", "w"))
```

### URL image → download first, then embed

Remote URLs are **rejected** (HTTP 400). If you only have a URL, `curl -o /tmp/img.png`
it first, then embed as above.

> Note: the API accepts PNG/JPG/WebP. For a Unity screenshot, PNG is safest. If you have
> a screenshot larger than ~10 MB, downscale with PIL first (thumbnail to ≤2048) — tiny
> text may blur, but the model sees composition, colors, and layout just fine.
> Verified working payload: a 2.4 MB base64 body → HTTP 200 (2026-10-13).

---

## Phase 3: Send it

Endpoint is `/v1/chat/completions` (chat, not images/generations):

```bash
curl -s -w "\nHTTP %{http_code}\n" -X POST "$SN_BASE/chat/completions" \
  -H "Authorization: Bearer $SN_KEY" \
  -H "Content-Type: application/json" \
  --data-binary @/tmp/vision_req.json \
  --max-time 300 -o /tmp/vision_resp.json
```

- `$SN_BASE` is `https://token.sensenova.cn/v1`, so the full URL becomes
  `https://token.sensenova.cn/v1/chat/completions`.
- `--max-time 300` — vision inference can take 20–90s.
- On non-200, print the response body and stop. Do **not** silently retry.
- Gotcha (from `image-gen`): a Bash classifier rate-limit hiccup is a harness issue,
  not a user denial — re-run the identical command.

---

## Phase 4: Parse and report

```python
import json
d = json.load(open("/tmp/vision_resp.json"))
print(d["choices"][0]["message"]["content"])
```

Print the answer **verbatim** (or as the requested format). If you used the model to
make a judgement (e.g. "does the screenshot match the spec?"), give the verdict plus
a short ranked list of what landed vs what's off — do not just dump the JSON.

---

## Gotchas (learned from image-gen)

1. **This is not the Read tool.** Never fall back to `Read <image.png>` when this skill
   is available — the user's global CLAUDE.md specifically bans it.
2. **Don't commit the key.** It lives in gitignored `.claude/sensenova.json`.
3. **Never inline base64** on the command line for local images; use `--data-binary @file`.
4. **Downscale huge screenshots** with PIL to ≤2048px if the API complains about size —
   the thumbnailing helper lives in `image-gen` §Phase 0.
5. Same Bash classifier hiccup as `image-gen` §Gotcha 5 — just re-run.