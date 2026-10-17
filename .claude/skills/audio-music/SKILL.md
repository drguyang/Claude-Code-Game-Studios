---
name: audio-music
description: "Generate looping instrumental music layers for the encounter-music bus via the StepFun asynchronous text_to_music API (submit + poll), then post-process to a seamless-looping 44.1 kHz mono candidate in assets/audio-staging/. Use when a music_* cue needs content. Every output is music_-prefixed, which pins the import tier to Streaming; the skill enforces the project's xfade_ms >= 2000 loop rule and refuses tracks that are not loopable."
argument-hint: "<曲风描述> --name music_<cue>_loop.wav [--with-vocals --lyrics <text>] [--sample-rate 48000]"
user-invocable: true
allowed-tools: Read, Glob, Grep, Write, Edit, Bash, AskUserQuestion
model: sonnet
---

生成**可循环的音乐层**。这是本项目唯一会把导入档位钉到 Streaming 的素材族。

---

## Phase 0: Setup

密钥读自 `.claude/stepfun.json`(本地密钥件,mode 600)。**该文件不得被 git 跟踪** ——
工具每次运行都查这一条,查到即拒跑;未被 `.gitignore` 覆盖则告警。
**不要**回显 key,不要写进任何 git 跟踪的文件(仓库是 PUBLIC)。

```bash
python3 tools/audio/stepfun_audio.py account     # 验通密钥 + 看余额
```

---

## Phase 1: 能力边界 —— 什么时候用这个技能

**用 `audio-music` 生成:** 遭遇音乐层,以及 `design/assets/entity-inventory.md` 登记的其余
Music Cue。当前 P0 只有一支:`music_encounter_layer_loop.wav`。

**不要用:**

| 你想要 | 该用 |
|---|---|
| 环境底噪(诊室房间音) | `/audio-sfx` —— `amb_clinic_room_loop.wav` 是环境音,**不是**音乐 |
| 一声锣 / 铃 / 犬吠 | `/audio-sfx` —— 那是世界可闻音,不是音乐层 |
| 带歌词的患者台词 | `/audio-tts` |

> 分界线:`music_*` 前缀决定 bus(`Music`)与导入档位(Streaming)。
> 把一段环境音命名成 `music_*` 会同时改错这两样。

---

## Phase 2: 参数与已证伪的用法

音乐是**异步**接口 —— 提交拿 `task_id`,再轮询到终态。工具已封装:

```
POST /v1/audio/music/submit   → { task_id }
POST /v1/audio/music/query    → { status, audio(base64), rewritten_caption, rewritten_lyrics }
```

轮询间隔 5 s,状态 ∈ `PENDING / RUNNING / SUCCESS / FAILED`。

**已证伪(不要试):**

1. **参数名是 `model_id`,不是 `model`。** 写 `model` 会 400。
2. **`sample_rate` 没有 44100。** 用 48000 生成,Phase 4 重采样。
3. **输出时长不可控。** 官方明说「由模型决定,无法通过参数精确控制」,通常是 **1~3 分钟**,
   且**同一输入多次生成的时长可能不同**。
   ⇒ 现有 `music_encounter_layer_loop.wav` 是 23.3 s,新生成的会长得多。
   对**循环层**这没问题(长一点反而更不易听出重复),但别拿它去对齐任何按秒计的编排。
4. **器乐必须显式 `instrumental=true`,且此时不得传 `lyrics`。** 歌词里的 `[Instrumental]`
   只是**段内**标记,不能替代顶层的 `instrumental=true`。
5. **`disable_caption_rewrite=true` 不能与 `lyrics_rewrite=true` 同用。**
6. 服务端默认把 `caption` 改写成规范英文 style prompt,并用 `rewritten_caption` 回传 ——
   **把它记进旁证**,否则你无法复现。

环境层默认走**器乐**;只是要一段无人声的循环层时,不要打开 `--with-vocals`。

---

## Phase 3: 生成到暂存区

```bash
python3 tools/audio/stepfun_audio.py music \
  --caption "sparse guqin and low drone, tense, slow, clinical unease, instrumental loop, no percussion" \
  --sample-rate 48000 \
  --out assets/audio-staging/music_encounter_layer_loop/raw.wav
```

该调用会**阻塞到任务终态**(默认上限 900 s)。超时不会丢任务 —— `task_id` 会被打印出来,
可续查。多轮迭代保留每一版,不要覆盖。

---

## Phase 4: 后处理与自检

```bash
python3 tools/audio/stepfun_audio.py post \
  assets/audio-staging/music_encounter_layer_loop/raw.wav \
  --name music_encounter_layer_loop.wav
```

`music_` 前缀会让 `post` **自动进入循环模式**:交叉淡化环绕(默认 `xfade_ms=2000`,与
`design/assets/specs/audio-material-manifest.md` 的循环规格一致)→ 重采样 44100 → 单声道 →
去直流 → 归一化 → 读回表头实测 → 算档位 → 写旁证。

**环绕淡化 ≠ 末尾淡出。** 工具把尾巴等功率叠到头上,让产物首尾天然连续;
「末尾淡出到 0」的写法能过存在性门与档位门,却在运行期**每圈留一个可闻空档**。

`post` 还会给一个 `seam_discontinuity_db`。它是 **advisory** —— 数字能提示接缝存在,
**证明不了无缝**。别把它当验收。

**ADR-018 自检:** 这段音乐是否在**报告状态**?遭遇音乐层是氛围,不是「你进战斗了」的广播。
禁 sting、禁 jingle、禁用音乐表达本该由玩家读数得出的结论。

---

## Phase 5: 试听与搬入

**先请人试听 `*.post.wav`,而且必须听接缝** —— 请对方专门听首尾相接处,以及在播放器里
循环播放若干圈。这是唯一能判「无缝」的办法。

> 我不能播放音频,只能测量。`AC-44-03 [L]` 是人耳听测。

通过后:

```
我可以把 assets/audio-staging/<...>/<name>.post.wav 搬入 unity/Assets/Audio/<name>.wav 吗?
```

**规范问法**(本项目协议原文):**"May I write this to `unity/Assets/Audio/<name>.wav`?"**
未得答复即不搬。**默认不搬。**

```bash
python3 tools/audio/stepfun_audio.py promote \
  assets/audio-staging/music_encounter_layer_loop/music_encounter_layer_loop.post.wav
```

搬入后 `.meta` 的 `loadType` **必须手改成 2(Streaming)** —— Unity 的默认值是 0,
而档位门要求 2(实测:23.3 s / 2050 KB 的 `music_*` 判 Streaming,且**前缀判定排在数值判据之前**,
所以哪怕只有 3 s 也判 Streaming)。**没有任何 repo 工具会写这个值。**

---

## 本技能不能证明什么

- ❌ **循环无缝。** 见 Phase 4,只由人耳裁定。
- ❌ **`AC-44-03 [L]`**(人耳听测)。
- ❌ **`AC-44-09`**(是否在报状态)—— 语义属性,无闸门可读。
- ❌ **音乐层与 `EncounterMusicLayer` 类别的配对是否正确。** 该配对是**非恒等**的
  (`MusicLayer` 只能配 `EncounterMusicLayer`),接线归人,见 `AudioEventTableGates.cs`。

> `promote` 打印 `PASS` = 「已测量、已暂存、机械上自洽」,**不是**「这段音乐对」。

---

## 交接

- 环境底噪 / 拟音 → `/audio-sfx`
- 台词 → `/audio-tts`
- 接线 + 重烘焙后 → `/story-done`
