using System.Runtime.CompilerServices;

// story-016 · AC-44-E5(2026-10-17):走查件须与门**同源**读 `audio_events.json`(取 `subtitle_text`)——
// 词法器 `JsonStage1Lexer` / `JsonNode` / `JsonNodeKind` 是本装配的 `internal`。
// 先例 = Sim.Codec/AssemblyInfo.cs、Editor.Tools.Spike/AssemblyInfo.cs(同款 IVT,ADR-025 §① 成例)。
// 注:本程序集 Editor-only(includePlatforms: ["Editor"]),IVT 不触及门 A,亦不影响任何出货构建。
[assembly: InternalsVisibleTo("Sim.Contracts.Tests")]
