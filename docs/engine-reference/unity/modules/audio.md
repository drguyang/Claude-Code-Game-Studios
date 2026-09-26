# Unity 6.3 — Audio Module Reference

**Last verified:** 2026-02-13
**Knowledge Gap:** Unity 6 audio mixer improvements

---

## Overview

Unity 6.3 audio systems:
- **AudioSource**: Play sounds on GameObjects
- **Audio Mixer**: Mix, effect processing, dynamic mixing
- **Spatial Audio**: 3D positioned sound

---

## Basic Audio Playback

### AudioSource Component

```csharp
AudioSource audioSource = GetComponent<AudioSource>();

// ✅ Play
audioSource.Play();

// ✅ Play with delay
audioSource.PlayDelayed(0.5f); // 0.5 seconds

// ✅ Play one-shot (doesn't interrupt current sound)
audioSource.PlayOneShot(clip);

// ✅ Stop
audioSource.Stop();

// ✅ Pause/Resume
audioSource.Pause();
audioSource.UnPause();
```

### Play Sound at Position (Static Method)

```csharp
// ✅ Quick 3D sound playback (auto-destroys when done)
AudioSource.PlayClipAtPoint(clip, transform.position);

// ✅ With volume
AudioSource.PlayClipAtPoint(clip, transform.position, 0.7f);
```

---

## 3D Spatial Audio

### AudioSource 3D Settings

```csharp
AudioSource source = GetComponent<AudioSource>();

// Spatial Blend: 0 = 2D, 1 = 3D
source.spatialBlend = 1.0f; // Fully 3D

// Doppler effect (pitch shift based on velocity)
source.dopplerLevel = 1.0f;

// Distance attenuation
source.minDistance = 1f;   // Full volume within this distance
source.maxDistance = 50f;  // Inaudible beyond this distance
source.rolloffMode = AudioRolloffMode.Logarithmic; // Natural falloff
```

### Volume Rolloff Curves
- **Logarithmic**: Natural, realistic (RECOMMENDED)
- **Linear**: Steady decrease
- **Custom**: Define your own curve

---

## Audio Mixer (Advanced Mixing)

### Setup Audio Mixer

1. `Assets > Create > Audio Mixer`
2. Open mixer: `Window > Audio > Audio Mixer`
3. Create groups: Master > SFX, Music, Dialogue

### Assign AudioSource to Mixer Group

```csharp
using UnityEngine.Audio;

public AudioMixerGroup sfxGroup;

void Start() {
    AudioSource source = GetComponent<AudioSource>();
    source.outputAudioMixerGroup = sfxGroup; // Route to SFX group
}
```

### Control Mixer from Code

```csharp
using UnityEngine.Audio;

public AudioMixer audioMixer;

// ✅ Set volume (exposed parameter)
audioMixer.SetFloat("MusicVolume", -10f); // dB (-80 to 0)

// ✅ Get volume
audioMixer.GetFloat("MusicVolume", out float volume);

// Convert linear (0-1) to dB
float volumeDB = Mathf.Log10(volumeLinear) * 20f;
audioMixer.SetFloat("MusicVolume", volumeDB);
```

### Expose Mixer Parameters
In Audio Mixer window:
1. Right-click parameter (e.g., Volume)
2. "Expose 'Volume' to script"
3. Rename in "Exposed Parameters" tab (e.g., "MusicVolume")

---

## Audio Effects

### Add Effects to Mixer Groups

In Audio Mixer:
- Click group (e.g., SFX)
- Click "Add Effect"
- Choose: Reverb, Echo, Low Pass, High Pass, Distortion, etc.

### Duck Music During Dialogue (Sidechain)

```csharp
// Setup in Audio Mixer:
// 1. Create "Duck Volume" snapshot
// 2. Lower music volume in that snapshot
// 3. Transition to snapshot when dialogue plays

public AudioMixerSnapshot normalSnapshot;
public AudioMixerSnapshot duckedSnapshot;

public void PlayDialogue(AudioClip clip) {
    duckedSnapshot.TransitionTo(0.5f); // 0.5s transition
    audioSource.PlayOneShot(clip);
    Invoke(nameof(RestoreMusic), clip.length);
}

void RestoreMusic() {
    normalSnapshot.TransitionTo(1.0f); // 1s transition back
}
```

---

## Per-Source Filters & Ramp Evidence (补录 2026-09-26 · Story 004 MUST DO 5)

> **补录缘由**:`production/epics/audio-system/story-004-breath-layers-precision-tiers.md`
> Engine Notes 钉「逐源 `AudioLowPassFilter` 须补录 engine-reference」;下方两条是
> GDD F-44.1「滤波 / 噪声底变化 ramp ≥ 50 ms 硬下界」**不可用 `TransitionTo` 证明**的依据。
> 口径:不确定的写「未实测」,不写成事实。

### AudioLowPassFilter Component

```csharp
// ✅ 逐源低通滤波(与 AudioSource 同 GameObject;参数名 = 长期稳定 API,训练数据已知)
AudioLowPassFilter lowpass = GetComponent<AudioLowPassFilter>();
lowpass.cutoffFrequency = 1200f;   // Hz(默认 22000)
lowpass.lowpassResonanceQ = 1.0f;  // Q 值(默认 1)

// ✅ 读当前值
float hz = lowpass.cutoffFrequency;
```

- **与组级滤波的分工**:Audio Mixer 里的 LowPass effect 挂在**组**上 = 同组全部声源**共享**
  (逐 cue 带档位会互踩,GDD F-44.1 :306-310 注);`AudioLowPassFilter` 组件是**逐源**独立滤波
  —— 本项目用于听诊基础气流层与附加音层走**不同通带**的场景。
- **⚠️ 无内建 ramp 承诺**:直接赋值 `cutoffFrequency` 的**过渡行为未实测**(文档无 ≥ 50 ms
  过渡的承诺)—— 需要硬下界 ramp 时须**脚本自插值**(见下条),不得指望赋值本身平滑。
- **⚠️ Knowledge Gap(post-cutoff 未实测面)**:Unity 6「audio mixer improvements」是否改变
  该组件的参数范围 / 滤波曲线形状 —— **未在 6000.3.24f1 实测**;组件存在性与两个属性名
  为长期稳定 API(低风险)。

### Ramp ≥ 50 ms 的可用依据(SetFloat / TransitionTo 两签名 · 2026-09-26 Q3 核对)

```csharp
// ✅ 唯一签名:两参 —— 立即生效,无 transitionTime 参数
audioMixer.SetFloat("StethoscopeBandwidth", value);

// ✅ 单签名:time = 过渡时长(秒);过渡曲线由 Unity 内部决定
snapshot.TransitionTo(0.5f);
```

- `AudioMixer.SetFloat(string, float)` **无 transitionTime 重载**(6.3 文档面仅两参;
  2026-09-26 咨询核对)。
- `AudioMixerSnapshot.TransitionTo(float)` **单签名**;**过渡曲线形状由引擎内部决定、
  未文档化** ⇒ **不得**把 `TransitionTo(0.05f)` 当作「ramp ≥ 50 ms 达标」的证明
  (曲线不透明 ⇒ 判据写不出 = 假绿);曲线实际形状**未实测**。
- **项目结论(Story 004 / 006 依据)**:滤波 / 噪声底的 ≥ 50 ms ramp 由**脚本按 dB/oct
  对数轴自插值 + 逐帧 `SetFloat`** 实现;可测点 = 单帧 Δ 上限 + 到位计时 ≥ 50 ms。

---

## Audio Performance

### Optimize Audio Loading

```csharp
// Audio Import Settings (Inspector):
// - Load Type:
//   - Decompress On Load: Small clips (SFX), loads fully into memory
//   - Compressed In Memory: Medium clips, decompressed at runtime (RECOMMENDED)
//   - Streaming: Large clips (music), streamed from disk

// Compression Format:
// - PCM: Uncompressed, highest quality, largest size
// - ADPCM: 3.5x compression, good for SFX (RECOMMENDED for SFX)
// - Vorbis/MP3: High compression, good for music (RECOMMENDED for music)
```

### Preload Audio

```csharp
// Preload audio clip before playing (avoid stutter)
audioSource.clip.LoadAudioData();

// Check if loaded
if (audioSource.clip.loadState == AudioDataLoadState.Loaded) {
    audioSource.Play();
}
```

---

## Music Systems

### Crossfade Between Tracks

```csharp
public IEnumerator CrossfadeMusic(AudioSource from, AudioSource to, float duration) {
    float elapsed = 0f;
    to.Play();

    while (elapsed < duration) {
        elapsed += Time.deltaTime;
        float t = elapsed / duration;

        from.volume = Mathf.Lerp(1f, 0f, t);
        to.volume = Mathf.Lerp(0f, 1f, t);

        yield return null;
    }

    from.Stop();
}
```

### Seamless Music Looping

```csharp
// Audio Import Settings:
// - Check "Loop" for seamless music loops
audioSource.loop = true;
```

---

## Common Patterns

### Random Pitch Variation (Avoid Repetition)

```csharp
void PlaySoundWithVariation(AudioClip clip) {
    AudioSource source = GetComponent<AudioSource>();
    source.pitch = Random.Range(0.9f, 1.1f); // ±10% pitch variation
    source.PlayOneShot(clip);
}
```

### Footstep Sounds (Random from Array)

```csharp
public AudioClip[] footstepClips;

void PlayFootstep() {
    AudioClip clip = footstepClips[Random.Range(0, footstepClips.Length)];
    AudioSource.PlayClipAtPoint(clip, transform.position, 0.5f);
}
```

### Check if Sound is Playing

```csharp
if (audioSource.isPlaying) {
    // Sound is currently playing
}
```

---

## Audio Listener

### Single Listener Rule
- Only ONE `AudioListener` should be active at a time
- Usually attached to Main Camera

```csharp
// Disable extra listeners
AudioListener listener = GetComponent<AudioListener>();
listener.enabled = false;
```

---

## Debugging

### Audio Window
- `Window > Audio > Audio Mixer`
- Visualize levels, test snapshots

### Audio Settings
- `Edit > Project Settings > Audio`
- Global volume, DSP buffer size, speaker mode

---

## Sources
- https://docs.unity3d.com/6000.0/Documentation/Manual/Audio.html
- https://docs.unity3d.com/6000.0/Documentation/Manual/AudioMixer.html
