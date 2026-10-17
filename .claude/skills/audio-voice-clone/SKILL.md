---
name: audio-voice-clone
description: "Enroll a reusable custom voice from a 5-10 second reference clip so that several generated audio files share one timbre: uploads the clip, calls the StepFun voice-cloning API, and returns a voice id for audio-tts to consume. Forward-looking (no P0 deliverable) and gated on a rights ruling — cloning a voice whose owner has not consented is not covered by any ADR in this project."
argument-hint: "--file <参考音频.wav> [--text <该音频的文本>] [--model stepaudio-2.5-tts] | --list"
user-invocable: true
allowed-tools: Read, Glob, Grep, Write, Edit, Bash, AskUserQuestion
model: sonnet
---

从 5~10 秒参考音频**复刻**一个可复用音色,产出 `voice_id`,供 `/audio-tts` 的 `--voice` 引用。

> **两件事先说清:**
> 1. 本技能目前**没有 P0 待产件** —— 它是为「多条台词要同一个音色」准备的。
> 2. **克隆谁的声音,必须先有用户拍板。** 详见 Phase 1 的权利一节。

---

## Phase 0: Setup

密钥读自 `.claude/stepfun.json`(本地密钥件,mode 600)。**该文件不得被 git 跟踪** ——
工具每次运行都查这一条,查到即拒跑;未被 `.gitignore` 覆盖则告警。
**不要**回显 key,不要写进任何 git 跟踪的文件(仓库是 PUBLIC)。

```bash
python3 tools/audio/stepfun_audio.py account      # 验通密钥 + 看余额(复刻 9.9 元/音色)
python3 tools/audio/stepfun_audio.py voices       # 列出已有音色(含官方的与自建的)
```

---

## Phase 1: 能力边界与权利前置

**用 `audio-voice-clone`:**

- 同一角色有多条台词,需要一个**统一音色**;
- 需要一个官方音色清单里没有的特定嗓音。

**不要用:**

| 你想要 | 该用 |
|---|---|
| 一条台词的合成 | `/audio-tts` —— 先用官方音色即可,别为一条台词建音色(要花 9.9 元) |
| 咳嗽/喘息这类非语言人声 | `/audio-sfx` —— **音色复刻不影响它们**,它们不是合成语音 |
| 音乐里的人声 | `/audio-music` |

### ⚠️ 权利与同意(本技能的第一道门,不是附注)

克隆音色是**复制一个真实人的声音特征**。本项目的任何 ADR(含 ADR-018)**都没有**
裁定过这件事。因此在调用任何复刻接口之前:

1. **问清参考音频的来源与授权** —— 谁的声?得到同意了吗?用途覆盖游戏内使用吗?
2. **没有明确答复 ⇒ `BLOCKED`,不做。** 这不是「先跑通再补手续」的环节:
   一旦复刻成功,声音特征就已经落在服务端上了,喊停也收不回来。
   判定口径:授权明确 = 可继续;含糊、回避、或「先试试」= 一律 `BLOCKED`。
3. **参考音频不得入库。** 仓库是 PUBLIC(`.gitignore` 的 Local secrets 段自己写明了这一点);
   参考音频与 `voice_id` 都按本地材料处理,只留在暂存区或本地磁盘。
4. 复刻结果**不随技能一起提交**;它是一条服务端侧的资源,记下 id 与来源即可。

---

## Phase 2: 参数与已证伪的用法

复刻是**两步**:先把音频上传拿 `file_id`,再拿它换音色。

```
POST /v1/files              (multipart, purpose=storage)  → file_id
POST /v1/audio/voices       { model, file_id, text? }     → { id, object, duplicated }
```

**已证伪 / 硬约束(不要试):**

1. **`model` 只接受 `stepaudio-2.5-tts` / `step-tts-2` / `step-tts-mini`。**
   **`stepaudio-3-tts` 不在列** —— 也就是说,
   **用本技能复刻出的音色无法喂给 `stepaudio-3-tts` 合成。**
   工具在检测到该错配时会当场拒绝。选音色模型前先想清楚要用哪个 TTS 模型。
2. **参考音频须 5~10 秒**,格式 `mp3` / `wav`。太短复刻不出特征,太长接口不收。
3. **`text` 建议一定传** —— 它是参考音频对应的文本;不传则服务端自行跑 ASR 解析,
   官方原文即「为保证效果,建议传入」。文本对不对直接决定音色质量。
4. **`duplicated` 字段**:若重复请求,响应会告诉你这个音色已经建过 ——
   别把它当失败,它是在防止你二次付费(9.9 元/音色)。

---

## Phase 3: 上传与复刻

```bash
# 1) 上传参考音频 → file_id
python3 tools/audio/stepfun_audio.py files-upload --file /path/to/ref.wav

# 2) 复刻 → voice_id
python3 tools/audio/stepfun_audio.py clone \
  --file-id <上一步的 file_id> \
  --text "这段参考音频里说的那句话" \
  --model stepaudio-2.5-tts
```

参考音频先自检:5~10 s、**无人声以外的强背景音**、无削顶。用
`python3 tools/audio/stepfun_audio.py verify <ref.wav>` 看时长与格式。

---

## Phase 4: 记录与复用

复刻成功后,**把这条音色记进项目的素材生成记录**
(`design/assets/specs/audio-material-manifest.md` §「素材生成记录」——
该节 §181 已预先授权按 `prompt / model / seed` 同型字段增列)。

建议记录:`voice_id` / 复刻所用 model / 参考音频的**来源与授权说明** / 复刻日期。
**不要把参考音频本身或 `voice_id` 写进任何 git 跟踪的文件。**

之后在 `/audio-tts` 里:

```bash
python3 tools/audio/stepfun_audio.py speech --voice <voice_id> --text "..." --out ...
```

> ⚠️ 再次强调模型错配:若 `voice_id` 是在 `stepaudio-2.5-tts` 上建的,
> 合成时就得用 `stepaudio-2.5-tts`,不能换成 `stepaudio-3-tts`。

---

## Phase 5: 留痕(需要批准)

若要把「复刻了哪个音色、来源是什么」写进项目文档:

```
我可以把这条音色的记录(voice_id / model / 来源与授权说明 / 日期)写入
design/assets/specs/audio-material-manifest.md 的「素材生成记录」吗?
```

**规范问法**(本项目协议原文):**"May I write this to `design/assets/specs/audio-material-manifest.md`?"**

得到同意再写。**默认不写** —— 音色是本地资源,不是仓库资产。

---

## 本技能不能证明什么

- ❌ **授权是否成立。** 工具查不了权利;这一条只有用户能给答复。
- ❌ **音色像不像目标人。** 不可测 —— 只能人耳比对参考音频与合成结果。
- ❌ **`AC-44-03 [L]`**(人耳听测)。
- ❌ **合成结果的情绪/表演是否合适。** 那是 `/audio-tts` 的事。

> 本技能只证明一件事:**一个 `voice_id` 被服务端建出来了**,以及它的来源被记录在案。

---

## 交接

- 用它合成台词 → `/audio-tts`
- 非语言人声 → `/audio-sfx`
- 音乐 → `/audio-music`
