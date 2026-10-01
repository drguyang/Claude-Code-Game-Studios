---
name: switch-claude-model
description: Switch the Claude Code model across the proxy and config files, restart it, and verify connectivity. Use when the user asks to set or change the Claude or sonnet model. Not for OpenCode-only edits.
---

# Switch Claude Code Model

Change the upstream model used by Claude Code in this workspace. The chain is:

`claude` → `http://127.0.0.1:8123` (local proxy `~/claude-proxy.js`) → `https://opencode.ai/zen/go`

Three places must stay in sync:

1. `~/claude-proxy.js` — `const DEFAULT_TARGET_MODEL = '...'` (this is what actually rewrites every request)
2. `~/.claude/settings.json` — env keys `ANTHROPIC_MODEL` and `ANTHROPIC_DEFAULT_SONNET_MODEL`
3. `.claude/opencode.json` in this workspace — same two keys (used by the OpenCode client, not by claude)

## Procedure

Run the deterministic script with the requested model id:

```bash
bash .trae/skills/switch-claude-model/scripts/switch-model.sh <model-id>
```

Example model ids: `mimo-v2.6-flash`, `space-bunny-free`, `longcat-2.5-preview-free`.

The script, in order:

1. Validates the model id format and confirms the model exists in the upstream `/v1/models` list (fails early if not).
2. Updates all three locations above; other config keys are left untouched.
3. Kills every existing `node claude-proxy.js` process (duplicate stale processes have caused port conflicts before — verify only one remains).
4. Starts the proxy, confirms it owns `127.0.0.1:8123`, and checks the startup log line `model rewrite: ALL -> <model-id>`.
5. Runs a real `claude -p` round-trip from the workspace root and checks the answer.

## Reporting

- Only report success after the `claude -p` round-trip returns; if any step fails, report the exact failing step and its output — do not claim it is connected.
- A first run can take 60–120s because startup triggers several serial upstream requests. The script already allows for this; do not shorten the timeout.
- The stderr line `[claude-code:unrecognized_model] ...` is expected for non-Anthropic model ids and does not indicate failure.
- `TRAE Sandbox Error` noise around `~/.claude/session-env/` comes from the agent sandbox, not the proxy; it is irrelevant to connectivity.

## Known upstream pitfall — DNS poisoning of opencode.ai

Local DNS resolves `opencode.ai` to `141.193.154.x`, which is hijacked and answers every path with `302 → https://m.baidu.com/`. Symptoms: the proxy logs `upstream 302`, or requests fail with `self-signed certificate`. The real address is Cloudflare `172.65.90.x` (verified via `dig @8.8.8.8` and `@223.5.5.5`).

Both the proxy and the script already work around this — do not "fix" it back to a hostname lookup:

- `claude-proxy.js` connects to the Cloudflare IP with `servername`/`host` set to `opencode.ai`, plus `rejectUnauthorized: false`.
- `switch-model.sh` resolves via public DNS first and passes `--resolve` to curl, falling back to a hard-coded IP.

If the upstream starts returning `302` again, re-resolve the IP with `dig @8.8.8.8 opencode.ai` and update both places.

## If the script cannot run

Apply the same five steps manually. Never edit only `opencode.json` — Claude Code does not read it, so the proxy file and `~/.claude/settings.json` are the effective config.
