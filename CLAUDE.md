# Claude Code Game Studios -- Game Studio Agent Architecture

Indie game development managed through 54 coordinated Claude Code subagents.
Each agent owns a specific domain, enforcing separation of concerns and quality.

## Technology Stack

- **Engine**: Unity 6.3 LTS
- **Language**: C#
- **Rendering**: URP (Universal Render Pipeline)
- **Physics**: PhysX (默认 3D 物理);DOTS 侧走 Unity Physics
- **XR**: OpenXR (VR 仅用于急救动作小游戏)
- **Version Control**: Git with trunk-based development
- **Build System**: Unity Build Pipeline (Unity Build Automation for CI)
- **Asset Pipeline**: Unity Asset Import Pipeline + Addressables

> **Note**: Engine-specialist agents exist for Godot, Unity, Unreal, and Babylon.js.
> Use the set matching your engine. `unity-specialist` is the primary for this project;
> see `.claude/docs/technical-preferences.md` for the full routing table.

## Project Structure

@.claude/docs/directory-structure.md

## Engine Version Reference

@docs/engine-reference/unity/VERSION.md

## Technical Preferences

@.claude/docs/technical-preferences.md

## Coordination Rules

@.claude/docs/coordination-rules.md

## Collaboration Protocol

**User-driven collaboration, not autonomous execution.**
Every task follows: **Question -> Options -> Decision -> Draft -> Approval**

- Agents MUST ask "May I write this to [filepath]?" before using Write/Edit tools
- Agents MUST show drafts or summaries before requesting approval
- Multi-file changes require explicit approval for the full changeset
- No commits without user instruction

See `docs/COLLABORATIVE-DESIGN-PRINCIPLE.md` for full protocol and examples.

> **First session?** If the project has no engine configured and no game concept,
> run `/start` to begin the guided onboarding flow.

## Coding Standards

@.claude/docs/coding-standards.md

## Context Management

@.claude/docs/context-management.md
