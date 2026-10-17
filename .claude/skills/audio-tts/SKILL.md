---
name: audio-tts
description: "Generate spoken lines from text with a chosen voice via the StepFun stepaudio-3-tts API, including emotion and delivery control via the instruction field or inline () direction. Lands a 44.1 kHz mono candidate in assets/audio-staging/ for human audition. Forward-looking: there is currently NO P0 pending deliverable for this skill — the project's 12 cough files are non-lexical and belong to audio-sfx, not here."
argument-hint: "<台词> --voice <音色> --name <冻结文件名.wav> [--instruction <情绪/人设>] [--speed N] [--timestamp]"
user-invocable: true
allowed-tools: Read, Glob, Grep, Write, Edit, Bash, AskUserQuestion
model: sonnet
---

把**文本**合成为语音。控制项是情绪、人设、语速与发音 —— 不是音效。

> **先读这一句:** 本技能目前**没有 P0 待产件**。本项目的咳嗽族
> (`vo_patient_cough_damp_*`)是**非语言**的,归 `/audio-sfx`。
> 这里是为将来可能出现的患者台词/旁白准备的。

---

## Phase 0: Setup

密钥读自 `.claude/stepfun.json`(本地密钥件,mode 600)。**该文件不得被 git 跟踪** ——
工具每次运行都查这一条,查到即拒跑;未被 `.gitignore` 覆盖则告警。
**不要**回显 key,不要写进任何 git 跟踪的文件(仓库是 PUBLIC)。

```bash
python3 tools/audio/stepfun_audio.py account      # 验通密钥 + 看余额
python3 tools/audio/stepfun_audio.py voices       # 看有哪些可用音色
```

---

## Phase 1: 能力边界 —— 什么时候用这个技能

**用 `audio-tts`:**

- 患者主诉、问诊应答等**有词**的台词;
- 需要指定**音色 + 情绪 + 语速**的整段播报;
- 与 `/audio-asr` 组**回读闭环**(合成 → 转写 → 比对文本),核对多音字与错读。

**不要用:**

| 你想要 | 该用 |
|---|---|
| 咳嗽、呻吟、喘息、叹气 | `/audio-sfx` —— 这些是**非语言**的 |
| 脚步 / 器物 / 环境音 | `/audio-sfx` |
| 音乐 | `/audio-music` |
| 一个贯穿多条台词的统一音色 | 先 `/audio-voice-clone` 建音色,再回这里用 `--voice` |

> **最容易走错的一步:** 把 12 支咳嗽丢给 TTS。`stepaudio-3-tts` 念的是文本 ——
> 你得到的会是「念出‘咳嗽’两个字」或者一段空白,而它可能照样通过所有机械检查。

---

## Phase 2: 参数与已证伪的用法

接口 `POST /v1/audio/speech`,`model: stepaudio-3-tts`。

**控制情绪的两种合法途径(3-tts 只有这两条):**

1. `--instruction` —— 全局自然语言指导(整段的人设/基调),`stepaudio-3-tts` 上限 **500 字**
   (`stepaudio-2.5-tts` 是 200 字)。
   ```
   --instruction "中年男性,声音虚弱带痰音,语速偏慢,气息不足"
   ```
2. 文本内**圆括号** —— 局部表演提示。**括号内默认不发音**;若这个词本身要念出来,就不要加括号。

**已证伪(不要试):**

1. **`voice_label` 对 `stepaudio-2.5-tts` / `stepaudio-3-tts` 会直接报错。**
   官方 Warning 原文如此。情绪/风格**只能**走上面两条途径。工具不暴露该字段 —— 不是遗漏,是它本来就不能用。
2. **`sample_rate` 没有 44100** —— 只有 `8000/16000/22050/24000/48000`。用 48000 生成,
   Phase 4 重采样。工具会在参数不合法时**当场拒绝**,不会把错值发出去。
3. **`timestamp=true` 必须搭 `return_url=true` 或 `stream_format="sse"`**,否则 HTTP 400
   (`timestamp requires return_url=true or stream_format=sse`)。工具在 `--timestamp` 时会自动补
   `return_url=true`。
4. **`input` 上限 1000 字符。** 长文要切段 —— 但切段会打断韵律,能一次给完就一次给完。
5. **响应必须是 wav。** 本机无 mp3/flac 解码器,工具强制 `response_format=wav`。
6. **音色来自复刻时注意模型错配:** `/v1/audio/voices` 复刻出的音色**不能**喂给
   `stepaudio-3-tts`(复刻接口只接受 `stepaudio-2.5-tts` / `step-tts-2` / `step-tts-mini`)。
   详见 `/audio-voice-clone`。

---

## Phase 3: 生成到暂存区

```bash
python3 tools/audio/stepfun_audio.py speech \
  --text "大夫……我这胸口,闷了有半个月了。(气短)" \
  --voice <音色> \
  --instruction "五十岁男性,气息不足,语速慢,偶有停顿" \
  --sample-rate 48000 \
  --out assets/audio-staging/<cue>/raw.wav
```

多轮迭代保留每一版,不要覆盖 —— 试听时人要比的是「哪一版最对」。
台词文本若日后要用于字幕,`--timestamp` 会一并取回逐词时间轴(毫秒)。

---

## Phase 4: 后处理与自检

```bash
python3 tools/audio/stepfun_audio.py post \
  assets/audio-staging/<cue>/raw.wav --name <冻结文件名.wav>
```

重采样 44100 → 单声道 → 去直流 → 判静音 → 裁首尾静音(one-shot)→ 归一化 →
读回表头实测 → 算档位 → 写旁证 sidecar。**不通过 = 非零退出,不产出。**

`vo_*` 是 one-shot,**不要**加 `--loop`(只有 `music_*` / `_loop.wav` 才环绕)。

**ADR-018 自检:** 这段语音是否在**报告状态**?台词可以让患者**说**他难受,
但音频本身不得用来播报「脉象越界了」这类本该由玩家读数得出的结论。

---

## Phase 5: 试听与搬入

**先请人听 `*.post.wav`**,并特别核对:多音字、断句、气口、情绪是否符合人设。

> 我不能播放音频,只能测量。`AC-44-03 [L]` 是人耳听测。

可选但强烈建议 —— 用 `/audio-asr` 做**词法**回读,抓念错/漏字:

```bash
python3 tools/audio/stepfun_audio.py asr --file <...>.post.wav --expect "大夫我这胸口闷了有半个月了"
```

**空转写或「一致」都不是验收结论**,只是提示。见 `/audio-asr` 的「不能证明什么」。

通过后:

```
我可以把 assets/audio-staging/<...>/<name>.post.wav 搬入 unity/Assets/Audio/<name>.wav 吗?
```

**规范问法**(本项目协议原文):**"May I write this to `unity/Assets/Audio/<name>.wav`?"**
未得答复即不搬。**默认不搬。**

```bash
python3 tools/audio/stepfun_audio.py promote assets/audio-staging/<cue>/<name>.post.wav
```

---

## 本技能不能证明什么

- ❌ **`AC-44-03 [L]`**(人耳听测)。
- ❌ **语气是否像病人。** 这不可测。
- ❌ **`AC-44-09`**(是否在报状态)。
- ❌ **字幕/时间轴是否与音频对齐。** `--timestamp` 给的是服务端切句结果,不是对齐证明。
- ❌ **音色是否获得授权。** 见 `/audio-voice-clone`。

> `promote` 打印 `PASS` = 「已测量、已暂存、机械上自洽」,**不是**「这句话念对了」。

---

## 交接

- 非语言人声(咳嗽/喘息) → `/audio-sfx`
- 建可复用音色 → `/audio-voice-clone`
- 回读核对念对了没有 → `/audio-asr`
- 接线 + 重烘焙后 → `/story-done`
