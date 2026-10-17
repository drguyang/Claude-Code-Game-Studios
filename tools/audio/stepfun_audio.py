#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""stepfun_audio —— StepFun 音频生成 / 后处理 / 档位校验 / 搬入(5 个 audio-* 技能共用)。

为什么是共享工具而不是 5 份内联 heredoc:
  同型论证见 `tools/falsify/README.md` —— 「此前这些反证一律写在 /tmp 的一次性 shell 里,
  每次重写、每次靠人记得还原」。重采样 / 去直流 / 归一化 / 裁静音 / 交叉淡化 / 档位预判
  这六件事对 5 个能力完全同构,抄 5 份 = 5 处会各自腐烂的判据。
  本机无 ffmpeg / sox / soundfile,只有 numpy + scipy + stdlib wave —— 同为 kindgen.py
  头注释登记过的「本机没有 X,故纯 Python」口径。

⚠️ 单一出处纪律(本工具的立身之本):
  档位阈值**读自** `unity/Assets/Editor.Tools.Bake/AudioImportSettingsGate.cs`,
  白名单**读自** `unity/Assets/Editor.Tools.Gates/AudioEventTableGates.cs`。
  本文件**不含任何** 512/4MB/5.0/30.0/"music_" 的字面量副本。
  锚点找不到 ⇒ 退出码 2(判据过期),而不是拿默认值静默失准。
  库里已两处登记过同型事故(ADR-024 §⑥「禁另抄一份」、AudioEventTableBinder 头注释)。

⚠️ 本工具**只出测量值,不出声学结论**。
  duration / bytes / peak / rms / dc / clip 是**测量**(事实);
  loadType 是**照 C# 判据算出的确定值**;
  「循环无缝」「音色中性可滤波」「不报状态」是**判断** —— 机器给不了,
  留给人耳(AC-44-03 [L])。侧车 JSON 里只有前者,永不写 "status": "verified"。

子命令:
  speech / sfx / music / asr / clone / voices / files-upload    ← 生成与查询(走网)
  account                                                       ← 余额
  post        <raw> --name <冻结名.wav>                          ← 后处理 + 测量 + 侧车
  verify      <wav|dir>                                         ← 档位判决(可比 .meta)
  frozen                                                        ← 列出已冻结文件名
  promote     <staged.wav>                                      ← 搬入 unity/Assets/Audio/
"""

from __future__ import annotations

import argparse
import base64
import glob as globmod
import json
import math
import os
import re
import shutil
import subprocess
import sys
import time
import wave
from datetime import datetime, timezone
from pathlib import Path

import numpy as np
import requests
from scipy.signal import resample_poly

# ---------------------------------------------------------------- 路径与常量

ROOT = Path(__file__).resolve().parents[2]
CFG_PATH = ROOT / ".claude" / "stepfun.json"
GATE_TIER = ROOT / "unity/Assets/Editor.Tools.Bake/AudioImportSettingsGate.cs"
GATE_TABLE = ROOT / "unity/Assets/Editor.Tools.Gates/AudioEventTableGates.cs"
AUDIO_DIR = ROOT / "unity/Assets/Audio"
STAGING = ROOT / "assets/audio-staging"
MANIFEST = ROOT / "design/assets/specs/audio-material-manifest.md"
EVENTS = ROOT / "assets/data/audio_events.json"

TARGET_RATE = 44100          # 工程内全部 wav 的采样率(实测)
TARGET_CHANNELS = 1
TARGET_BITS = 16

EXIT_OK, EXIT_FAIL, EXIT_STALE = 0, 1, 2

# 这些是**本工程**的约定,不是从 C# 读的判据 —— 它们不参与任何档位/白名单裁决
DEFAULT_XFADE_MS = 2000      # manifest 规格:music 循环交叉淡化 ≥ 2000
SILENCE_FLOOR_DBFS = -60.0   # 非静音下限:S14-6 那批 2044 B 全零占位就是为了拦这个
CLIP_EPS = 1e-6


def die(msg: str, code: int = EXIT_FAIL) -> "None":
    print(f"FAIL  {msg}", file=sys.stderr)
    sys.exit(code)


def ok(msg: str) -> None:
    print(f"PASS  {msg}")


def rel(p: Path) -> str:
    """仓内路径显示成相对路径,仓外(如 /tmp 试跑)原样显示 —— 不是为了好看,是为了不炸。"""
    try:
        return str(Path(p).resolve().relative_to(ROOT))
    except ValueError:
        return str(p)


# ------------------------------------------------------- 单一出处:读 C# 常量

def _cs_number(text: str, name: str) -> float:
    """从 C# 源码取出 `public const <type> <name> = <expr>;` 的数值。"""
    m = re.search(
        rf"public\s+const\s+\w+\s+{re.escape(name)}\s*=\s*([^;]+);", text
    )
    if not m:
        die(f"判据过期:C# 里找不到常量 {name}({GATE_TIER.name})", EXIT_STALE)
    expr = m.group(1)
    if not re.fullmatch(r"[0-9+\-*/\s().]+", expr):
        die(f"判据过期:常量 {name} 的表达式无法安全求值:{expr!r}", EXIT_STALE)
    return eval(expr, {"__builtins__": {}}, {})  # 已被上一行的白名单字符集约束


def _cs_string(text: str, name: str) -> str:
    m = re.search(
        rf'public\s+const\s+string\s+{re.escape(name)}\s*=\s*"([^"]*)"\s*;', text
    )
    if not m:
        die(f"判据过期:C# 里找不到常量 {name}({GATE_TIER.name})", EXIT_STALE)
    return m.group(1)


def _cs_string_array(text: str, name: str) -> list[str]:
    m = re.search(
        rf"public\s+static\s+readonly\s+IReadOnlyList<string>\s+{re.escape(name)}\s*=\s*new\[\]\s*\{{(.*?)\}};",
        text,
        re.S,
    )
    if not m:
        die(f"判据过期:C# 里找不到数组 {name}({GATE_TABLE.name})", EXIT_STALE)
    body = re.sub(r"//[^\n]*", "", m.group(1))          # 去行注释
    # 数组元素可能是字面量 `"PlayerAction"`,也可能是**常量名** `CategoryMusicLayer` /
    # `WorldBreathCue` —— 只抓引号会把后者整个丢掉(静默少一项,比报错更坏)。
    out = []
    for lit, ident in re.findall(r'"([^"]*)"|([A-Za-z_]\w*)', body):
        out.append(lit if lit else ident)
    return out


def tier_constants() -> dict:
    """档位判据常量 —— 全部读自 AudioImportSettingsGate.cs。"""
    text = GATE_TIER.read_text(encoding="utf-8")
    return {
        "SmallResidentBytes": int(_cs_number(text, "SmallResidentBytes")),
        "LargeResidentBytes": int(_cs_number(text, "LargeResidentBytes")),
        "ShortSeconds": _cs_number(text, "ShortSeconds"),
        "LongSeconds": _cs_number(text, "LongSeconds"),
        "MusicPrefix": _cs_string(text, "MusicPrefix"),
        "DecompressOnLoad": int(_cs_number(text, "DecompressOnLoad")),
        "CompressedInMemory": int(_cs_number(text, "CompressedInMemory")),
        "Streaming": int(_cs_number(text, "Streaming")),
    }


def table_constants() -> dict:
    """事件表白名单 —— 全部读自 AudioEventTableGates.cs。"""
    text = GATE_TABLE.read_text(encoding="utf-8")
    consts = dict(re.findall(r'public\s+const\s+string\s+(\w+)\s*=\s*"([^"]*)"\s*;', text))
    out = {
        "WhitelistCategories": _cs_string_array(text, "WhitelistCategories"),
        "ForbiddenTriggerSources": _cs_string_array(text, "ForbiddenTriggerSources"),
        "RequiredCues": _cs_string_array(text, "RequiredCues"),
    }
    out["WhitelistCategories"] = [
        consts.get(c, c) for c in out["WhitelistCategories"]       # CategoryMusicLayer → 字面量
    ]
    out["RequiredCues"] = [consts.get(c, c) for c in out["RequiredCues"]]
    return out


def required_load_type(name: str, duration_s: float, decoded_bytes: int, K: dict) -> int:
    """逐字镜像 AudioImportSettingsGate.RequiredLoadType(分支顺序含语义 —— 前缀须先判)。"""
    if name and name.startswith(K["MusicPrefix"]):
        return K["Streaming"]                                     # ① 语义档,排在数值判据之前
    if decoded_bytes > K["LargeResidentBytes"] or duration_s >= K["LongSeconds"]:
        return K["Streaming"]                                     # ② 大档
    if decoded_bytes <= K["SmallResidentBytes"] and duration_s < K["ShortSeconds"]:
        return K["DecompressOnLoad"]                              # ③ 小档(两条件须同时满足)
    return K["CompressedInMemory"]                                # ④ 中档


def describe_load_type(v: int, K: dict) -> str:
    return {K["DecompressOnLoad"]: "DecompressOnLoad(0)",
            K["CompressedInMemory"]: "CompressedInMemory(1)",
            K["Streaming"]: "Streaming(2)"}.get(v, f"未知档位({v})")


# ------------------------------------------------------------------ 配置

def _git(*args: str) -> subprocess.CompletedProcess:
    return subprocess.run(["git", *args], cwd=ROOT, capture_output=True, text=True, timeout=10)


def guard_secret(path: Path) -> None:
    """密钥文件的**处置纪律由本函数执行,而不是靠技能正文里的一句断言**。

    仓库是 PUBLIC —— 提交一个 key,几分钟内就被扫描、账号被吊销并扣费。
    所以每次启动都实查两件事(而不是嘴上说「它已 gitignored」):

      被跟踪了   ⇒ 硬失败。这已经是事故,不是提醒。
      没被忽略   ⇒ 警告。文件本身没进库,但下一次 `git add -A` 就会把它捎进去。
    """
    try:
        if _git("ls-files", "--error-unmatch", rel(path)).returncode == 0:
            die(f"**{rel(path)} 已被 git 跟踪** —— 立刻 git rm --cached 它并轮换 key。"
                f"仓库是 PUBLIC,提交的密钥几分钟内即被扫描", EXIT_FAIL)
        if _git("check-ignore", "-q", rel(path)).returncode != 0:
            print(f"WARN  {rel(path)} 当前**未被 .gitignore 覆盖**。它还没进库,"
                  f"但 `git add -A` 会把它捎进去 —— 请把该路径加进 .gitignore 的 Local secrets 段。")
    except (FileNotFoundError, subprocess.SubprocessError):
        pass          # 不是 git 仓库 / 没装 git:不阻塞正常使用


def load_config() -> dict:
    if not CFG_PATH.exists():
        die(f"缺少 {rel(CFG_PATH)} —— 向用户要 key 后写入该文件"
            f"(mode 600,且**不得**进任何 git 跟踪的文件)")
    guard_secret(CFG_PATH)
    cfg = json.loads(CFG_PATH.read_text(encoding="utf-8"))
    if not cfg.get("api_key"):
        die(f"{CFG_PATH.name} 里没有 api_key")
    cfg.setdefault("base_url", "https://api.stepfun.com/v1")
    cfg.setdefault("models", {})
    return cfg


def model_for(cfg: dict, kind: str, override: str | None = None) -> str:
    if override:
        return override
    m = cfg["models"].get(kind)
    if not m:
        die(f"{CFG_PATH.name} 的 models 里没有 '{kind}';用 --model 显式指定")
    return m


# ------------------------------------------------------------------ HTTP

def _auth(cfg: dict) -> dict:
    return {"Authorization": f"Bearer {cfg['api_key']}"}


def post_json(cfg: dict, path: str, payload: dict, timeout: int = 300) -> dict:
    url = cfg["base_url"].rstrip("/") + path
    r = requests.post(url, headers={**_auth(cfg), "Content-Type": "application/json"},
                      json=payload, timeout=timeout)
    if r.status_code != 200:
        # image-gen 的纪律:非 200 打印响应体后停,不静默重试
        die(f"HTTP {r.status_code} {path}\n{r.text[:2000]}")
    return r.json()


def get_json(cfg: dict, path: str, timeout: int = 60) -> dict:
    url = cfg["base_url"].rstrip("/") + path
    r = requests.get(url, headers=_auth(cfg), timeout=timeout)
    if r.status_code != 200:
        die(f"HTTP {r.status_code} {path}\n{r.text[:2000]}")
    return r.json()


def decode_audio_response(cfg: dict, resp: dict, out_path: Path) -> Path:
    """统一解开 3 种音频返回形:裸二进制已由调用方处理;此处是 base64 / url。"""
    blob = None
    if isinstance(resp.get("audio"), str):
        blob = base64.b64decode(resp["audio"])
    elif isinstance(resp.get("data"), dict) and resp["data"].get("audio"):
        blob = base64.b64decode(resp["data"]["audio"])
    elif isinstance(resp.get("data"), list) and resp["data"]:
        blob = base64.b64decode(resp["data"][0].get("b64_json") or resp["data"][0]["audio"])
    if blob is None:
        die(f"响应里找不到音频字段;顶层键 = {sorted(resp.keys())}\n{json.dumps(resp)[:800]}")
    out_path.parent.mkdir(parents=True, exist_ok=True)
    out_path.write_bytes(blob)
    return out_path


def assert_wav(path: Path) -> None:
    """响应是**不可信输入** —— 落盘前先验魔数,不让它决定后续怎么解释字节。"""
    head = path.read_bytes()[:12]
    if not (head[:4] == b"RIFF" and head[8:12] == b"WAVE"):
        die(f"返回的不是 RIFF/WAVE(前 12 字节 {head!r})。"
            f"请让请求带 response_format=wav —— 本机无 mp3/flac 解码器")


# ------------------------------------------------------- 测量(只出事实)

def read_wav(path: Path) -> tuple[np.ndarray, int, int, int]:
    """→ (float64 样本 [-1,1], sample_rate, channels, bits)。仅 WAV(本机无解码器)。"""
    if not path.exists():
        die(f"文件不存在:{path}")
    try:
        with wave.open(str(path), "rb") as w:
            ch, sw, sr, n = w.getnchannels(), w.getsampwidth(), w.getframerate(), w.getnframes()
            raw = w.readframes(n)
    except wave.Error as e:
        die(f"不是可解析的 WAV({e})。本机无 ffmpeg/soundfile,m4a/mp3/flac 请先转 wav:{path}")
    bits = sw * 8
    if bits == 8:
        x = (np.frombuffer(raw, dtype=np.uint8).astype(np.float64) - 128.0) / 128.0
    elif bits == 16:
        x = np.frombuffer(raw, dtype="<i2").astype(np.float64) / 32768.0
    elif bits == 24:
        b = np.frombuffer(raw, dtype=np.uint8).reshape(-1, 3).astype(np.int32)
        v = (b[:, 0] | (b[:, 1] << 8) | (b[:, 2] << 16))
        v = np.where(v & 0x800000, v - 0x1000000, v)
        x = v.astype(np.float64) / 8388608.0
    elif bits == 32:
        x = np.frombuffer(raw, dtype="<i4").astype(np.float64) / 2147483648.0
    else:
        die(f"不支持的位深 {bits}")
    return x, sr, ch, bits


def _dbfs(v: float) -> float:
    return 20.0 * math.log10(max(v, 1e-12))


def clip_run(x: np.ndarray) -> int:
    """最长连续满刻度样本数 —— 归一化**之前**测,否则增益会把削顶藏起来。"""
    hit = np.abs(x) >= (1.0 - CLIP_EPS)
    if not hit.any():
        return 0
    best = cur = 0
    for h in hit:
        cur = cur + 1 if h else 0
        best = max(best, cur)
    return int(best)


def measure(x: np.ndarray, sr: int, ch: int, bits: int) -> dict:
    frames = len(x) // ch if ch else 0
    peak = float(np.max(np.abs(x))) if x.size else 0.0
    rms = float(np.sqrt(np.mean(x ** 2))) if x.size else 0.0
    return {
        "sample_rate": sr, "channels": ch, "bits": bits,
        "frames": frames,
        "duration_s": round(frames / sr, 6) if sr else 0.0,
        "decoded_bytes": frames * ch * (bits // 8),
        "peak_dbfs": round(_dbfs(peak), 2),
        "rms_dbfs": round(_dbfs(rms), 2),
        "dc_offset": round(float(np.mean(x)) if x.size else 0.0, 6),
        "clipped_run_len": clip_run(x),
    }


def seam_discontinuity_db(x: np.ndarray, sr: int) -> float | None:
    """首尾拼接处的跳变(以短窗 RMS 归一)。**advisory**:数字只提示接缝,不能证明无缝。"""
    w = max(int(0.010 * sr), 1)
    if len(x) < 4 * w:
        return None
    head_rms = float(np.sqrt(np.mean(x[:w] ** 2)))
    tail_rms = float(np.sqrt(np.mean(x[-w:] ** 2)))
    ref = max((head_rms + tail_rms) / 2.0, 1e-12)
    return round(_dbfs(abs(float(x[0]) - float(x[-1])) / ref), 2)


# ------------------------------------------------------------- 后处理

def to_mono(x: np.ndarray, ch: int) -> np.ndarray:
    return x if ch == 1 else x.reshape(-1, ch).mean(axis=1)


def resample_to(x: np.ndarray, sr: int, target: int) -> np.ndarray:
    """只走 resample_poly(多相 FIR)。np.interp 是**混叠**路径 —— 48k→44.1k 是 147:160 非整数比,
    混叠出来的毛刺人耳常误记成「音色不对」,而不是「重采样没做对」。"""
    if sr == target:
        return x
    g = math.gcd(sr, target)
    return resample_poly(x, target // g, sr // g).astype(np.float64)


def trim_silence(x: np.ndarray, sr: int, thresh_db: float = -50.0, pad_ms: int = 10):
    """one-shot 首尾裁静音(带 padding)。返回 (y, 裁掉的首/尾样本数)。"""
    if x.size == 0:
        return x, 0, 0
    th = 10 ** (thresh_db / 20.0)
    loud = np.flatnonzero(np.abs(x) > th)
    if loud.size == 0:
        return x, 0, 0
    pad = int(pad_ms / 1000.0 * sr)
    a = max(int(loud[0]) - pad, 0)
    b = min(int(loud[-1]) + pad + 1, len(x))
    return x[a:b], a, len(x) - b


def crossfade_wrap(x: np.ndarray, sr: int, xfade_ms: int) -> np.ndarray:
    """把尾巴等功率交叉淡化叠到头上 ⇒ 产物首尾天然连续(循环体不含淡化尾)。

    与「输出末尾淡出到 0」的区别:后者能过存在性门与档位门,却在运行期每圈留一个可闻空档。
    """
    xf = int(xfade_ms / 1000.0 * sr)
    if xf <= 0 or len(x) < 3 * xf:
        return x
    t = np.linspace(0.0, 1.0, xf, endpoint=False)
    w_in, w_out = np.sin(t * math.pi / 2), np.cos(t * math.pi / 2)   # 等功率
    head, tail = x[:xf].copy(), x[-xf:].copy()
    return np.concatenate([head * w_in + tail * w_out, x[xf:-xf]])


def remove_dc(x: np.ndarray) -> np.ndarray:
    return x - float(np.mean(x)) if x.size else x


def normalize(x: np.ndarray, target_peak_dbfs: float) -> np.ndarray:
    peak = float(np.max(np.abs(x))) if x.size else 0.0
    if peak <= 1e-9:
        return x
    return x * ((10 ** (target_peak_dbfs / 20.0)) / peak)


def write_wav(path: Path, x: np.ndarray, sr: int) -> None:
    y = np.clip(x, -1.0, 1.0)
    pcm = np.round(y * 32767.0).astype("<i2")
    path.parent.mkdir(parents=True, exist_ok=True)
    with wave.open(str(path), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(sr)
        w.writeframes(pcm.tobytes())


# --------------------------------------------------------- 冻结文件名真源

def _expand_braces(tok: str) -> list[str]:
    """递归展开 `a{1,2}b` / `a{x|y}b` 的全部组合(可多组嵌套)。"""
    m = re.search(r"\{([^{}]*)\}", tok)
    if not m:
        return [tok]
    out: list[str] = []
    for alt in re.split(r"[,|]", m.group(1)):
        out += _expand_braces(tok[:m.start()] + alt.strip() + tok[m.end():])
    return out


def frozen_names() -> set[str]:
    """manifest 冻结段 ∪ audio_events.json 的 assets[] ∪ Audio 目录实际文件。

    这是「名字对不对」的判据来源 —— AC-44-D6 只查**文件在不在**,不查 cue 名
    (登记在案的 `内容-04` 洞:`phys_cough` vs 真源 `Voice_Cough_Damp`)。
    """
    names: set[str] = set()
    if MANIFEST.exists():
        # manifest 用花括号缩写:`sfx_footstep_{stone,dirt,humus,snow}_01.wav`,
        # 甚至多组混用 `vo_patient_cough_damp_{m|f}{1|2|3}_{1|2}.wav`。
        # 不展开就会把 `_f1.wav` 这种碎片当名字收进来,而真名反被判成「自造」——
        # 一个把合法件拒之门外的判据,比没有判据更坏。故递归展开**每一组**。
        for tok in re.findall(r"[A-Za-z0-9_{},|]+\.wav", MANIFEST.read_text(encoding="utf-8")):
            # 只收五族的名字。散文里的缩写续写(如 :118「`..._m1.wav` / `_f1.wav` 退役」)
            # 会留下 `_f1.wav` 这种不是真名的碎片 —— 收进来只会让判据看起来更宽,并不更准。
            names.update(n for n in _expand_braces(tok)
                         if n.startswith(("sfx_", "vo_", "amb_", "music_", "ui_")))
    if EVENTS.exists():
        try:
            for row in json.loads(EVENTS.read_text(encoding="utf-8")).get("rows", []):
                names |= {a for a in row.get("assets", []) if isinstance(a, str)}
        except json.JSONDecodeError:
            pass
    names |= {p.name for p in AUDIO_DIR.glob("*.wav")} if AUDIO_DIR.exists() else set()
    return names


# =========================================================== 子命令:生成

def cmd_speech(a, cfg) -> int:
    model = model_for(cfg, "tts", a.model)
    payload = {
        "model": model,
        "input": a.text,
        "voice": a.voice,
        "response_format": "wav",      # 本机无 mp3 解码器 —— 必须 wav
        "sample_rate": a.sample_rate,  # 44100 不在支持集内,见下方断言
    }
    if a.sample_rate not in (8000, 16000, 22050, 24000, 48000):
        die(f"sample_rate={a.sample_rate} 不被 StepFun 支持(仅 8000/16000/22050/24000/48000)。"
            f"工程要 44100 ⇒ 用 48000 生成,后处理再重采样")
    if a.instruction:
        # ⚠️ stepaudio-2.5/3-tts 传 voice_label 会**报错**;情绪/风格只能走 instruction 或文本内 ()
        payload["instruction"] = a.instruction
    if a.speed:
        payload["speed"] = a.speed
    if a.text_normalization:
        payload["text_normalization"] = a.text_normalization
    if a.timestamp:
        payload["timestamp"] = True
        payload["return_url"] = True   # timestamp 必须搭 return_url 或 stream_format=sse,否则 400

    url = cfg["base_url"].rstrip("/") + "/audio/speech"
    r = requests.post(url, headers={**_auth(cfg), "Content-Type": "application/json"},
                      json=payload, timeout=300)
    if r.status_code != 200:
        die(f"HTTP {r.status_code} /audio/speech\n{r.text[:1500]}")
    out = Path(a.out)
    if a.timestamp or "json" in r.headers.get("content-type", ""):
        out = decode_audio_response(cfg, r.json(), out)
        if a.timestamp:
            print(json.dumps(r.json().get("data", {}).get("subtitles", []),
                             ensure_ascii=False, indent=1)[:4000])
    else:
        out.parent.mkdir(parents=True, exist_ok=True)
        out.write_bytes(r.content)
    assert_wav(out)
    print(f"OK    {out}  ({out.stat().st_size} B)")
    print(f"      模型 {model} / 音色 {a.voice} / 请求 {a.sample_rate} Hz")
    return EXIT_OK


def cmd_sfx(a, cfg) -> int:
    """音效 / 环境音 / 非语言人声 —— 走统一音频生成(text_to_audio)。

    ⚠️ 12 支 `vo_patient_cough_damp_*` 归这里,不归 TTS:咳嗽是非语言的,
    stepaudio-3-tts 念的是文本。这是本项目最大的一笔待产件。
    """
    model = model_for(cfg, "sfx", a.model)
    if a.sample_rate not in (8000, 16000, 22050, 24000, 48000):
        die(f"sample_rate={a.sample_rate} 不被支持(仅 8000/16000/22050/24000/48000)")
    payload = {
        "model": model,
        "task": "text_to_audio",
        "scripts": [{"text": a.prompt if a.prompt.startswith("[") else f"[{a.prompt}]"}],
        "response_format": "wav",
        "sample_rate": a.sample_rate,
    }
    if a.instruction:
        payload["instruction"] = a.instruction
    out = Path(a.out)
    out.parent.mkdir(parents=True, exist_ok=True)
    # 与 TTS 同型:不传 return_url 时接口直接回**裸音频二进制**,不是 JSON。
    url = cfg["base_url"].rstrip("/") + "/audio/generate"
    r = requests.post(url, headers={**_auth(cfg), "Content-Type": "application/json"},
                      json=payload, timeout=600)
    if r.status_code != 200:
        die(f"HTTP {r.status_code} /audio/generate\n{r.text[:1500]}")
    if "json" in r.headers.get("content-type", ""):
        out = decode_audio_response(cfg, r.json(), out)
    else:
        out.write_bytes(r.content)
    assert_wav(out)
    print(f"OK    {out}  ({out.stat().st_size} B)")
    print(f"      模型 {model} / 请求 {a.sample_rate} Hz")
    return EXIT_OK


def cmd_music(a, cfg) -> int:
    """音乐 —— **异步** submit + query。输出的时长由模型决定(1~3 分钟),不能指定。"""
    model = model_for(cfg, "music", a.model)
    body = {
        "task": "text_to_music",
        "model_id": model,                 # 注意是 model_id,不是 model
        "caption": a.caption,
        "instrumental": not a.with_vocals,  # 环境层要器乐
        "response_format": "wav",
        "sample_rate": a.sample_rate,
    }
    if a.with_vocals and a.lyrics:
        body["lyrics"] = a.lyrics
    sub = post_json(cfg, "/audio/music/submit", body, timeout=120)
    task_id = sub.get("task_id")
    if not task_id:
        die(f"submit 未返回 task_id:{json.dumps(sub)[:500]}")
    print(f"      任务 {task_id} 已提交,每 5 s 轮询 …")
    deadline = time.time() + a.max_wait
    while True:
        q = post_json(cfg, "/audio/music/query", {"task_id": task_id}, timeout=120)
        st = q.get("status")
        if st == "SUCCESS":
            break
        if st == "FAILED":
            die(f"任务 {task_id} FAILED:{json.dumps(q, ensure_ascii=False)[:800]}")
        if time.time() > deadline:
            die(f"任务 {task_id} 超时({a.max_wait}s);状态仍为 {st}。task_id 保留可续查")
        time.sleep(5)
    out = decode_audio_response(cfg, q, Path(a.out))
    assert_wav(out)
    print(f"OK    {out}  ({out.stat().st_size} B)")
    if q.get("rewritten_caption"):
        print(f"      服务端改写后的 caption:{q['rewritten_caption']}")
    return EXIT_OK


def cmd_asr(a, cfg) -> int:
    model = model_for(cfg, "asr", a.model)
    p = Path(a.file)
    if not p.exists():
        die(f"文件不存在:{p}")
    with p.open("rb") as fh:
        r = requests.post(
            cfg["base_url"].rstrip("/") + "/audio/transcriptions",
            headers=_auth(cfg),
            files={"file": (p.name, fh, "audio/wav")},
            data={"model": model, "response_format": "json"},
            timeout=300,
        )
    if r.status_code != 200:
        # 非语言素材(音效/音乐/环境音/咳嗽/呼吸)传进来,服务端会回 400 `no speech found`。
        # 这不是一次失败的重试,而是**本技能不适用** —— 必须说清,不能让它看起来像网络错误。
        if r.status_code == 400 and "no speech found" in r.text.lower():
            print("该素材不是语音 —— 本技能对它不出任何判定。")
            print("非语言素材本就转写不出文本。把「无文本」读成「没问题 ⇒ PASS」")
            print("是本项目最典型的假绿。请交回人耳,以及 /audio-sfx 或 /audio-music。")
            return EXIT_FAIL
        die(f"HTTP {r.status_code} /audio/transcriptions\n{r.text[:1500]}")
    text = r.json().get("text", "") if "json" in r.headers.get("content-type", "") else r.text
    print(text)
    if a.expect is not None:
        # 只做**词法**核对。空转写不等于通过 —— 见 SKILL 的「不能证明什么」一节
        print("\n--- 词法核对(仅供参考,不构成任何验收结论)---")
        if not text.strip():
            print("WARN  转写为空。非语言素材(音效/音乐/咳嗽/呼吸)本就该是空的 —— "
                  "**不得**据此判 PASS,本技能对此类 cue 不出判定")
            return EXIT_FAIL
        print("期望:", a.expect)
        print("一致" if a.expect.strip() in text else "**不一致** —— 人工复核发音/多音字")
    return EXIT_OK


def cmd_voices(a, cfg) -> int:
    print(json.dumps(get_json(cfg, "/audio/voices"), ensure_ascii=False, indent=1))
    return EXIT_OK


def cmd_files_upload(a, cfg) -> int:
    """复刻前的音频上传(purpose=storage)→ file_id。"""
    p = Path(a.file)
    with p.open("rb") as fh:
        r = requests.post(
            cfg["base_url"].rstrip("/") + "/files",
            headers=_auth(cfg),
            files={"file": (p.name, fh, "audio/wav")},
            data={"purpose": "storage"},
            timeout=300,
        )
    if r.status_code != 200:
        die(f"HTTP {r.status_code} /files\n{r.text[:1200]}")
    d = r.json()
    print(json.dumps(d, ensure_ascii=False, indent=1))
    fid = d.get("id") or d.get("file_id")
    if fid:
        print(f"\nfile_id = {fid}")
    return EXIT_OK


def cmd_clone(a, cfg) -> int:
    """音色复刻。

    ⚠️ 两个已证实的边界:① 源音频须 5~10 s;② `model` 只接受
    stepaudio-2.5-tts / step-tts-2 / step-tts-mini —— **stepaudio-3-tts 不在列**,
    即复刻音色不能喂给 3-tts 合成。
    """
    model = model_for(cfg, "clone", a.model)
    if model == "stepaudio-3-tts":
        die("复刻接口不接受 stepaudio-3-tts;可选 stepaudio-2.5-tts / step-tts-2 / step-tts-mini")
    payload = {"model": model, "file_id": a.file_id}
    if a.text:
        payload["text"] = a.text
    d = post_json(cfg, "/audio/voices", payload, timeout=300)
    print(json.dumps(d, ensure_ascii=False, indent=1))
    print("\n⚠️ 音色 ID 与其参考音频**不得入库**(仓库是 PUBLIC,见 .gitignore 的 Local secrets 段);"
          "\n   克隆他人声音的权利归属未经本项目任何 ADR 裁定,使用前须用户单独拍板。")
    return EXIT_OK


def cmd_account(a, cfg) -> int:
    print(json.dumps(get_json(cfg, "/accounts"), ensure_ascii=False))
    return EXIT_OK


# ======================================================= 子命令:后处理

def cmd_post(a, cfg) -> int:
    K = tier_constants()
    src = Path(a.raw)
    x, sr, ch, bits = read_wav(src)
    src_measure = measure(x, sr, ch, bits)

    print(f"源   {src.name}: {sr} Hz / {ch} ch / {bits} bit / "
          f"{src_measure['duration_s']} s / peak {src_measure['peak_dbfs']} dBFS / "
          f"rms {src_measure['rms_dbfs']} dBFS")
    if src_measure["clipped_run_len"] > 0:
        print(f"WARN  源文件已有 {src_measure['clipped_run_len']} 个连续满刻度样本(削顶)。"
              f"归一化会把它藏起来,故**在增益之前**报出。")

    # 顺序有语义:先去直流,再判削顶/静音,最后才归一化
    y = resample_to(to_mono(x, ch), sr, TARGET_RATE)
    y = remove_dc(y)

    # 源若已满刻度,多相 FIR 必然产生轻微过冲(振铃)—— 那是重采样的正常行为,不是缺陷。
    # 中间值走 float64,有充裕余量;后面统一归一化会把它拉回来。
    # 只有**非有限值**或**量级离谱**才说明重采样路径真坏了。
    if not np.all(np.isfinite(y)):
        die("重采样后出现非有限值 —— 重采样路径有问题,不继续")
    peak_after = float(np.max(np.abs(y))) if y.size else 0.0
    if peak_after > 1.5:
        die(f"重采样后峰值 {peak_after:.3f} 远超 1.0 —— 重采样路径有问题,不继续")
    if peak_after > 1.0:
        print(f"      重采样过冲至 {peak_after:.4f}(源已满刻度;交由归一化回收)")

    m_pre = measure(y, TARGET_RATE, 1, TARGET_BITS)
    if m_pre["peak_dbfs"] < SILENCE_FLOOR_DBFS or m_pre["rms_dbfs"] < SILENCE_FLOOR_DBFS:
        die(f"静音判定:peak {m_pre['peak_dbfs']} dBFS / rms {m_pre['rms_dbfs']} dBFS。"
            f"低于 {SILENCE_FLOOR_DBFS} dBFS 上限 = 占位静音(本库出过一次 2044 B 全零件)。"
            f"**不产出** —— 别让一个空文件戴着合格的名字进库")

    loop = bool(a.loop) or a.name.startswith(K["MusicPrefix"]) or a.name.endswith("_loop.wav")
    if loop:
        y = crossfade_wrap(y, TARGET_RATE, a.xfade_ms)
        seam = seam_discontinuity_db(y, TARGET_RATE)
    else:
        seam = None
        if a.trim:
            y, cut_a, cut_b = trim_silence(y, TARGET_RATE)
            print(f"      裁静音:首 {cut_a} / 尾 {cut_b} 帧")

    y = normalize(y, a.target_peak_dbfs)

    stem = a.name[:-4] if a.name.lower().endswith(".wav") else a.name
    out_dir = Path(a.out_dir) if a.out_dir else (STAGING / stem)
    out_wav = out_dir / f"{stem}.post.wav"
    write_wav(out_wav, y, TARGET_RATE)

    # 读回**实测**表头 —— 不是自述
    z, sr2, ch2, bits2 = read_wav(out_wav)
    if (sr2, ch2, bits2) != (TARGET_RATE, TARGET_CHANNELS, TARGET_BITS):
        die(f"产物表头不符:{sr2}/{ch2}/{bits2},应为 {TARGET_RATE}/{TARGET_CHANNELS}/{TARGET_BITS}")
    m = measure(z, sr2, ch2, bits2)
    lt = required_load_type(a.name, m["duration_s"], m["decoded_bytes"], K)

    sidecar = {
        "generated_at": datetime.now(timezone.utc).isoformat(),
        "tool_git_sha": _git_sha(),
        "endpoint": a.endpoint or None,
        "model": a.model or None,
        "prompt": a.prompt or None,
        "seed": a.seed,
        "requested_sample_rate": a.sample_rate,
        "source_file": rel(src),
        "source_measurement": src_measure,
        "loop": loop,
        "xfade_ms": a.xfade_ms if loop else None,
        "measured": {**m, "seam_discontinuity_db": seam},
        "required_load_type": lt,
        "required_load_type_name": describe_load_type(lt, K),
        "_note": ("本件只有测量值与照 C# 判据算出的档位,**不含任何声学判定**。"
                  "「无缝」「中性可滤波」「不报状态」只能由人耳裁定(AC-44-03 [L])。"),
    }
    sidecar_path = out_dir / f"{stem}.json"
    sidecar_path.write_text(json.dumps(sidecar, ensure_ascii=False, indent=1), encoding="utf-8")

    frozen = frozen_names()
    print(f"\n产物 {rel(out_wav)}")
    print(f"  {m['sample_rate']} Hz / {m['channels']} ch / {m['bits']} bit / {m['duration_s']} s")
    print(f"  peak {m['peak_dbfs']} dBFS / rms {m['rms_dbfs']} dBFS / dc {m['dc_offset']}")
    print(f"  decoded {m['decoded_bytes']} B ({m['decoded_bytes']/1024:.1f} KB) / clipped_run {m['clipped_run_len']}")
    if seam is not None:
        print(f"  接缝跳变 {seam} dB(advisory:数字只能提示接缝,证明不了无缝)")
        if a.xfade_ms < DEFAULT_XFADE_MS:
            print(f"WARN  xfade {a.xfade_ms} ms < 规格下限 {DEFAULT_XFADE_MS} ms")
    print(f"  → 必需 loadType = {describe_load_type(lt, K)}  ← 搬入后要**手填**进 .wav.meta")
    print(f"  旁证 {rel(sidecar_path)}")
    print(f"  冻结名判定:{'在真源内' if a.name in frozen else '**不在** manifest/事件表/现有目录中'}"
          f"{'' if a.name in frozen else ' —— 疑似自造名,搬入会被拒(--allow-unfrozen 才放行)'}")
    print(f"\n下一步:人耳试听 {rel(out_wav)} —— 机器到此为止。")
    return EXIT_OK


def _git_sha() -> str | None:
    try:
        return subprocess.run(["git", "rev-parse", "--short", "HEAD"], cwd=ROOT,
                              capture_output=True, text=True, timeout=10).stdout.strip() or None
    except Exception:
        return None


# ======================================================= 子命令:校验

def _read_meta_load_type(meta: Path) -> tuple[int | None, str]:
    if not meta.exists():
        return None, "无 .meta(Unity 尚未导入)"
    m = re.search(r"loadType:\s*(\d+)", meta.read_text(encoding="utf-8", errors="replace"))
    return (int(m.group(1)), "") if m else (None, "meta 里没有 loadType 字段")


def cmd_verify(a, cfg) -> int:
    K = tier_constants()
    targets: list[Path] = []
    for pat in a.target:
        p = Path(pat)
        if p.is_dir():
            targets += sorted(p.glob("*.wav"))       # 目录要展开;之前把目录本身收进来又被跳过,
        elif "*" in pat:                             # 结果是「查了 0 个文件却报 PASS」—— 假绿。
            targets += sorted(Path(m) for m in globmod.glob(pat))
        else:
            targets.append(p)
    if not targets:
        die(f"没有匹配到任何 .wav({', '.join(a.target)})—— 空集不得报 PASS")
    bad = 0
    for wav in targets:
        if wav.is_dir():
            continue
        try:
            x, sr, ch, bits = read_wav(wav)
        except SystemExit:
            bad += 1
            continue
        m = measure(x, sr, ch, bits)
        req = required_load_type(wav.name, m["duration_s"], m["decoded_bytes"], K)
        actual, err = _read_meta_load_type(Path(str(wav) + ".meta"))
        fmt_ok = (sr, ch, bits) == (TARGET_RATE, TARGET_CHANNELS, TARGET_BITS)
        line = (f"{wav.name:44} {m['duration_s']:7.3f}s {m['decoded_bytes']/1024:8.1f}KB "
                f"need {describe_load_type(req, K):22}")
        if not fmt_ok:
            line += f"  FAIL 表头 {sr}/{ch}/{bits} 应为 44100/1/16"
            bad += 1
        if actual is None:
            line += f"  [{err}]"
        elif actual != req:
            line += f"  FAIL 实为 {describe_load_type(actual, K)}"
            bad += 1
        else:
            line += "  meta 一致"
        print(line)
    print(f"\n{'FAIL' if bad else 'PASS'}  {len(targets)} 个文件,{bad} 处不符")
    return EXIT_FAIL if bad else EXIT_OK


def cmd_frozen(a, cfg) -> int:
    for n in sorted(frozen_names()):
        print(n)
    return EXIT_OK


# ======================================================= 子命令:搬入

def cmd_promote(a, cfg) -> int:
    """搬入 unity/Assets/Audio/。**任一项不过就拒绝**,不看「文件确实在那儿」。

    这里刻意不碰 assets/data/audio_events.json:cue 名是闭集(RequiredCues 只有 4 支),
    且 rows[] 的**顺序即 cue 序号** —— 插入一行会静默重编所有后续 cue。
    事件表接线归人,本工具只打印草稿。
    """
    K = tier_constants()
    T = table_constants()
    staged = Path(a.staged)
    checks: list[tuple[str, bool, str]] = []

    if not staged.exists():
        die(f"暂存件不存在:{staged}")
    x, sr, ch, bits = read_wav(staged)
    m = measure(x, sr, ch, bits)
    name = a.name or staged.name.replace(".post", "")
    if not name.endswith(".wav"):
        name += ".wav"

    checks.append(("表头 44100/1/16", (sr, ch, bits) == (TARGET_RATE, TARGET_CHANNELS, TARGET_BITS),
                   f"实为 {sr}/{ch}/{bits}"))
    checks.append(("非静音", m["peak_dbfs"] > SILENCE_FLOOR_DBFS and m["rms_dbfs"] > SILENCE_FLOOR_DBFS,
                   f"peak {m['peak_dbfs']} / rms {m['rms_dbfs']} dBFS"))
    is_frozen = name in frozen_names()
    checks.append((
        "名字在冻结真源内",
        is_frozen or a.allow_unfrozen,
        "不在 manifest / 事件表 / 现有目录中 —— 疑似自造名。"
        "AC-44-D6 只查**文件在不在**,不查 cue 名(登记在案的 内容-04 洞),"
        "自造名的文件会安静地挂到任何东西上都挂不上。确要搬入请加 --allow-unfrozen",
    ))
    target = AUDIO_DIR / name
    checks.append(("目标不冲突", not target.exists() or a.replace,
                   f"{rel(target)} 已存在(要覆盖请加 --replace)"))

    print(f"=== 搬入前检查:{name} ===")
    for label, good, detail in checks:
        print(f"  {'PASS' if good else 'FAIL'}  {label}" + (f"  —— {detail}" if detail and not good else ""))
    if not all(c[1] for c in checks):
        print("\nBLOCKED —— 有检查未过,未搬入。")
        return EXIT_FAIL

    AUDIO_DIR.mkdir(parents=True, exist_ok=True)
    shutil.copy2(staged, target)
    side = staged.with_suffix(".json")
    if side.exists():
        shutil.copy2(side, AUDIO_DIR / f"{name}.json")

    lt = required_load_type(name, m["duration_s"], m["decoded_bytes"], K)
    print(f"\nPASS  已搬入 {rel(target)}  ({m['duration_s']} s / "
          f"{m['decoded_bytes']/1024:.1f} KB)")
    print(f"""
接下来三步人手做(**本工具到此为止**):

1. 在 Unity 里让它重新导入,然后把 {name}.meta 的 loadType 改成 {describe_load_type(lt, K)}
   —— 没有任何 repo 工具会写这个值,它是手改的(先例 commit 6040b17)。
   核验:grep -o 'loadType: [0-9]*' {rel(target)}.meta

2. 事件表接线 **归人**,且**只能追加行,不能插入**(rows[] 的顺序即 cue 序号;
   插入会静默重编号,同 cue id 换到另一个文件上)。
   cue 名取自 AudioEventTableGates.RequiredCues,现为:{T['RequiredCues']}
   合法 whitelist_category:{T['WhitelistCategories']}
   禁用的 trigger_source:{T['ForbiddenTriggerSources']}

3. 最后重烘焙(顺序不可颠倒 —— D6 对不存在的文件当场 throw):
   unity build unity --target StandaloneLinux64 \\
     --executeMethod DaYiJingCheng.EditorTools.Bake.DataBakeMenu.BakeAudioEvents

⚠️ 别把上面的 PASS 读成「声音是对的」。本工具查的**全是结构**:表头 / 是否静音 / 名字 /
   档位。声学判据(基础气流层须中性可滤波、细湿啰音须断续爆裂而非连续水声、咳嗽须闷短无混响尾、
   循环须无缝)没有任何闸门能读 —— AC-44-03 [L] 是人耳听测,不可由脚本代签。""")
    return EXIT_OK


# ---------------------------------------------------------------- 入口

def build_parser() -> argparse.ArgumentParser:
    p = argparse.ArgumentParser(prog="stepfun_audio.py", description=__doc__.split("\n")[1])
    p.add_argument("--config", default=str(CFG_PATH), help="密钥文件路径")
    sub = p.add_subparsers(dest="cmd", required=True)

    def net(name, help_):
        s = sub.add_parser(name, help=help_)
        s.add_argument("--model")
        return s

    s = net("speech", "TTS:文本 → 语音(stepaudio-3-tts)")
    s.add_argument("--text", required=True)
    s.add_argument("--voice", required=True)
    s.add_argument("--out", required=True)
    s.add_argument("--instruction", help="≤500 字;stepaudio-3-tts 的情绪/风格只走这里或文本内 ()")
    s.add_argument("--speed", type=float)
    s.add_argument("--text-normalization", choices=["standard", "enhanced"])
    s.add_argument("--sample-rate", type=int, default=48000)
    s.add_argument("--timestamp", action="store_true", help="词级时间戳(自动带 return_url)")
    s.set_defaults(func=cmd_speech)

    s = net("sfx", "音效/环境音/非语言人声(stepaudio-3-gen-preview)")
    s.add_argument("--prompt", required=True, help="[方括号内]的英文/中文声音描述")
    s.add_argument("--out", required=True)
    s.add_argument("--instruction")
    s.add_argument("--sample-rate", type=int, default=48000)
    s.set_defaults(func=cmd_sfx)

    s = net("music", "音乐生成(异步 submit + query)")
    s.add_argument("--caption", required=True, help="曲风/乐器/情绪/调式")
    s.add_argument("--out", required=True)
    s.add_argument("--with-vocals", action="store_true", help="默认器乐(instrumental=true)")
    s.add_argument("--lyrics")
    s.add_argument("--sample-rate", type=int, default=48000)
    s.add_argument("--max-wait", type=int, default=900)
    s.set_defaults(func=cmd_music)

    s = net("asr", "转写:回读校验(stepaudio-2.5-asr)")
    s.add_argument("--file", required=True)
    s.add_argument("--expect", help="期望文本,做词法比对")
    s.set_defaults(func=cmd_asr)

    s = net("voices", "列出已有音色")
    s.set_defaults(func=cmd_voices)

    s = net("files-upload", "上传参考音频取 file_id(复刻前置)")
    s.add_argument("--file", required=True)
    s.set_defaults(func=cmd_files_upload)

    s = net("clone", "音色复刻(file_id → voice_id)")
    s.add_argument("--file-id", required=True)
    s.add_argument("--text", help="参考音频对应的文本;强烈建议传")
    s.set_defaults(func=cmd_clone)

    s = net("account", "查余额")
    s.set_defaults(func=cmd_account)

    s = sub.add_parser("post", help="后处理:重采样/单声道/去直流/归一化/循环淡化 + 测量 + 侧车")
    s.add_argument("raw")
    s.add_argument("--name", required=True, help="目标冻结文件名,如 sfx_prop_place_01.wav")
    s.add_argument("--out-dir")
    s.add_argument("--loop", action="store_true", help="交叉淡化环绕(music_*/_loop 自动开)")
    s.add_argument("--xfade-ms", type=int, default=DEFAULT_XFADE_MS)
    s.add_argument("--target-peak-dbfs", type=float, default=-1.0)
    s.add_argument("--no-trim", dest="trim", action="store_false", default=True)
    s.add_argument("--endpoint"); s.add_argument("--model"); s.add_argument("--prompt")
    s.add_argument("--seed", type=int); s.add_argument("--sample-rate", type=int)
    s.set_defaults(func=cmd_post)

    s = sub.add_parser("verify", help="档位判决(含 .meta 比对);参数可为文件/目录/通配")
    s.add_argument("target", nargs="+")
    s.set_defaults(func=cmd_verify)

    s = sub.add_parser("frozen", help="列出冻结文件名真源")
    s.set_defaults(func=cmd_frozen)

    s = sub.add_parser("promote", help="搬入 unity/Assets/Audio/(前置检查不过则拒绝)")
    s.add_argument("staged")
    s.add_argument("--name")
    s.add_argument("--replace", action="store_true")
    s.add_argument("--allow-unfrozen", action="store_true")
    s.set_defaults(func=cmd_promote)
    return p


def main() -> int:
    a = build_parser().parse_args()
    if getattr(a, "config", None):
        global CFG_PATH
        CFG_PATH = Path(a.config)
    cfg = load_config()
    return a.func(a, cfg)


if __name__ == "__main__":
    sys.exit(main())
