---
name: audio-sfx
description: "Generate non-lexical game audio — foley, contact sounds, world ambience, and non-verbal vocalizations (coughs, breath layers) — from a text description via the StepFun text_to_audio API, and land 44.1 kHz mono candidates in assets/audio-staging/ for human audition. Use when a frozen sfx_/amb_/vo_ filename needs real audio content. This — not audio-tts — is the skill that owns the 12 vo_patient_cough_damp_* files: a cough is non-lexical."
argument-hint: "<声效描述> --name <冻结文件名.wav> [--loop] [--instruction <氛围>] [--sample-rate 48000] [--seed N]"
user-invocable: true
allowed-tools: Read, Glob, Grep, Write, Edit, Bash, AskUserQuestion
model: sonnet
---

生成**非语言**音频:拟音、接触音、世界环境音,以及咳嗽/呼吸这类**非语言人声**。

本技能是本项目**待产音频的最大出口** —— 冻结清单里除音乐与台词之外的全部。

---

## Phase 0: Setup

密钥读自 `.claude/stepfun.json`(本地密钥件,mode 600)。**该文件不得被 git 跟踪** ——
工具每次运行都查这一条,查到即拒跑;未被 `.gitignore` 覆盖则告警。
**不要**把 key 回显到对话或日志里,也**不要**把它写进任何 git 跟踪的文件
—— 仓库是 PUBLIC,提交后几分钟内即被扫描。

本技能不自己解析该文件,一律经由共享工具:

```bash
python3 tools/audio/stepfun_audio.py account        # 顺带验通密钥与网络,并看余额
```

工具里没有任何档位/白名单字面量:它每次启动都**从 C# 读** `AudioImportSettingsGate.cs`
与 `AudioEventTableGates.cs`。锚点找不到 ⇒ 退出码 2,不是拿默认值静默失准。

若 `.claude/stepfun.json` 不存在:向用户要 key,写入该文件后再继续。

---

## Phase 1: 能力边界 —— 什么时候用这个技能

**用 `audio-sfx` 生成:**

| 冻结件 | 说明 |
|---|---|
| `vo_patient_cough_damp_{m\|f}{1\|2\|3}_{1\|2}.wav`(**12 支**) | 阻尼咳嗽。`1=强 · 2=中 · 3=弱`;`m/f` 为音色;末位 `_1/_2` 是同一组合的两个变体 |
| `sfx_pulse_contact_01.wav` | 切脉接触 |
| `sfx_steth_contact_{chest,back}_01.wav` | 听诊器贴胸/贴背 |
| `sfx_footstep_{stone,dirt,humus,snow}_01.wav` | 四种地表脚步 |
| `sfx_cabinet_{drawer,handle}_01.wav` | 药柜抽屉 / 拉手 |
| `sfx_prop_place_01.wav` | 器物放置 |
| `sfx_world_{dog,horse,gong,bell,birds}_01.wav` | 世界可闻音(威胁档 + 灾难档钟声) |
| `sfx_world_{door_bell,vendor_cry,throat_clear,crying}_01.wav` | 世界可闻音 · **52 预告线索补 4 项**(机会档 2 / 反应档 1 / 灾难档 1) |
| `amb_clinic_room_loop.wav` | 诊室环境底噪(**循环**) |
| `ui_page_turn_{fwd,back}_01.wav` | 翻页 |
| `sfx_breath_base_loop_small.wav` · `sfx_breath_adventitious_fine.wav` | 呼吸两层(**AC-44-01 / AC-44-08 的载体,重做风险最高,见 Phase 2**) |

**不要用 `audio-sfx`:**

| 你想要 | 该用 |
|---|---|
| 说出**词**的患者台词 | `/audio-tts` —— 本技能生成的是非语言的 |
| 音乐 / 和声层 | `/audio-music` |
| 建一个可复用音色 | `/audio-voice-clone` |
| 核对刚生成的台词念对没有 | `/audio-asr` |

> **12 支咳嗽归这里,不归 TTS。** 这是最容易走错的一步:`stepaudio-3-tts` 念的是文本,
> 而咳嗽是非语言的。把它丢给 TTS,得到的是「念出‘咳嗽’两个字」或者一段空白。

---

## Phase 2: 参数与已证伪的用法

接口 `POST /v1/audio/generate`,`model: stepaudio-3-gen-preview`,`task: text_to_audio`。

音效描述用**方括号**包裹(方括号 = 非语言描述;台词才用 `speaker`)。工具会自动补方括号。

```
"damped wet cough, single burst, close-mic, no reverb tail, clinical recording"
"single footstep on dry gravel, close perspective, no room tone"
```

**已证伪(不要试):**

1. **`sample_rate` 没有 44100** —— 只有 `8000/16000/22050/24000/48000`。工程要 44100,
   所以**用 48000 生成**,再由 Phase 4 重采样。别指望接口直接给 44100。
2. **响应必须是 wav。** 本机**没有** mp3/flac 解码器(无 ffmpeg、无 soundfile),
   工具强制 `response_format=wav`,落到非 RIFF/WAVE 会当场拒绝。

**两条声学硬要求(它们没有任何闸门能读,只能靠你的 prompt + 人耳):**

- `sfx_breath_base_loop_small.wav` 必须**中性可滤波** —— **禁自带音高、禁自带环境底噪**
  (`AC-44-01`,BLOCKING)。运行期会在它上面挂 `tier_map` 滤镜;自带音高的底噪听起来
  「有质感」,但会把滤镜结果污染成另一个东西。
- `sfx_breath_adventitious_fine.wav` 是**吸气末、断续、细密爆裂音**,**禁连续水声**
  (`AC-44-08`)。「湿啰音」三个字很容易被理解成冒泡/水声 —— 那是错的。

---

## Phase 3: 生成到暂存区

```bash
python3 tools/audio/stepfun_audio.py sfx \
  --prompt "damped wet cough, single burst, close-mic, no reverb tail" \
  --sample-rate 48000 \
  --out assets/audio-staging/vo_patient_cough_damp_m1_1/raw.wav
```

多轮迭代时保留每一版(`--seed` 换种子或改 prompt),不要覆盖 —— 试听时人要看的是
「哪一版最像」,不是「最后一版是什么」。

---

## Phase 4: 后处理与自检

```bash
python3 tools/audio/stepfun_audio.py post \
  assets/audio-staging/vo_patient_cough_damp_m1_1/raw.wav \
  --name vo_patient_cough_damp_m1_1.wav
```

`post` 会:重采样到 44100(`resample_poly`,非整数比 147:160)→ 单声道 → 去直流 →
判静音 → 裁首尾静音(one-shot)/ 交叉淡化环绕(loop)→ 归一化 → **读回表头实测** →
算出必需 `loadType` → 写旁证 sidecar。

**不通过 = 退出码非 0,不产出。** 它会拦住:

- 静音件(peak 或 rms 低于 −60 dBFS)—— S14-6 那批 2044 B 全零占位就是为此设的;
- 表头不是 44100/1/16;
- 重采样后样本越界;
- `music_*` / `_loop.wav` 的 `xfade_ms` 低于 2000。

**ADR-018 自检(必须自己做,机器不查):** 这个声音是否**在报告状态**?
禁 sting / jingle、禁 ducking 报状态、禁素材切换报状态、禁用音频表达本该由玩家读数得出的结论。
一个「很好听」的提示音如果是在告诉玩家「脉象越界了」,它就是违规的 —— 哪怕它每一个机械检查都是绿的。

---

## Phase 5: 试听与搬入

**先请人试听 `*.post.wav`。** 我无法播放音频,只能测量它。

> 「机器到此为止」不是客套:上面每一道检查查的都是**结构** —— 表头 / 是否静音 / 名字 / 档位。
> 声学判据(中性可滤波、断续爆裂、闷短无混响尾、循环无缝)**没有任何闸门能读**。
> `AC-44-03 [L]` 是人耳听测,不可由脚本或 ASR 代签。

人耳通过后:

```
我可以把 assets/audio-staging/<...>/<name>.post.wav 搬入 unity/Assets/Audio/<name>.wav 吗?
```

**规范问法**(本项目协议原文):**"May I write this to `unity/Assets/Audio/<name>.wav`?"**
未得答复即不搬。**默认不搬。**

得到同意后:

```bash
python3 tools/audio/stepfun_audio.py promote \
  assets/audio-staging/vo_patient_cough_damp_m1_1/vo_patient_cough_damp_m1_1.post.wav
```

`promote` 的检查不过就**拒绝**(表头 / 非静音 / 名字是否在冻结真源内 / 目标是否冲突),
并打印接下来三步**必须由人做**的事:手改 `.meta` 的 `loadType`、事件表接线(只能追加行)、重烘焙。

---

## 本技能不能证明什么

写完就结束、不留这一段,是本项目最怕的假绿来源。逐条写清:

- ❌ **`AC-44-03 [L]`** —— 人耳听测。本技能永不代表它通过。
- ❌ **`AC-44-01`**(基础气流层可滤波)。没有任何闸门读频谱平坦度。
- ❌ **`AC-44-08`**(细湿啰音医学真实性)。
- ❌ **`AC-44-09`**(是否在报状态)。这是**语义**属性,任何 DSP 或 ASR 都看不见。
- ❌ **循环无缝**。`post` 给的 `seam_discontinuity_db` 是 **advisory**:数字能提示接缝存在,
  证明不了无缝。一个末尾淡出到 0 的文件能过存在性门和档位门,却在每圈留一个可闻空档。
- ❌ **cue 名对不对**。`AC-44-D6` 只查**文件在不在**,不查 cue 名(登记在案的 `内容-04` 洞)。
  工具会拿 manifest / 事件表 / 现有目录比对名字,但那只说明「名字是登记过的」。

> `promote` 打印 `PASS` 的含义是「已测量、已暂存、机械上自洽」,
> **不是**「声音是对的」,也**不**关闭任何 AC。

---

## 交接

- 台词类素材 → `/audio-tts`
- 音乐层 → `/audio-music`
- 搬入并接线完成后 → 重烘焙,再走 `/story-done` 收口(评审报告原件要求见
  `.claude/docs/coding-standards.md` §Review Evidence Standards)。

`assets/audio-staging/` 是试听台,不是资产库 —— 没搬进去的迭代版不入库。
