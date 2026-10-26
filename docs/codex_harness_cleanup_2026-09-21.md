# Codex harness cleanup — 2026-09-21

> 이 문서는 작성 당시의 보고서·검증 이력이다. 과거 카드/DB버전·미완료 항목·가동 상태를 오늘의 상태로 적용하지 않는다. 2026-10-20 현재 정리는 [프로젝트 상태](current_project_status.md), 운영은 [Lightsail 안내](lightsail_operations.md)를 따른다. 당시 측정값·성공/실패 기록은 보존한다.

## Applied

- Active After333 AGENTS.md: 30,942 → 4,136 bytes (about 86.6% smaller).
- Old prototype AGENTS.md: 12,480 → 726 bytes (about 94.2% smaller), now routes After333 work to C:/Project_333.
- Unmodified originals are preserved in docs/archive/AGENTS_Project333_before_2026-09-21.md and AGENTS_old_workspace_before_2026-09-21.md.
- docs/implementation_contract.md preserves the complete former active contract after a precedence notice; relevant sections are loaded on demand.
- docs/codex_handoff.md provides a short continuation record with dated verification/deployment evidence.
- User config model_reasoning_effort and models.new_thread.model_reasoning_effort are medium. The model remains gpt-6-astra; existing explicit per-task selections override defaults.
- Removed stale config entries sites@openai-bundled and github@openai-curated. The installed GitHub plugin is github@openai-curated-remote and remains enabled.
- The installed plugin inventory remains 11 enabled plugins. No plugin was uninstalled. Sites was absent from that inventory; its old local config entry was not evidence of an independently installed active plugin.
- The old config is backed up at C:/Users/youndonghyeok/.codex/backups/harness-20260921/config.before.toml.

## Validation and limits

The extracted contract ends with the exact original text; both original AGENTS files were hash-verified when backed up. Changed Markdown passes git diff --check. The Codex CLI successfully parsed the new config and listed the retained installed plugins.

No game/runtime code, account data, server process, app build, conversation history or cache was changed/deleted. This existing task's working directory and already-injected history were not rewritten. For new After333 work choose the already registered Project_333 project at C:/Project_333; use codex_handoff.md if continuity is needed. Existing tasks explicitly set to ultra retain that selection.

The byte reductions measure instruction-file size, not an observed billing reduction or a guaranteed token saving. Relevant detailed rules still consume input when a task needs them. Prompt caching remains enabled.
