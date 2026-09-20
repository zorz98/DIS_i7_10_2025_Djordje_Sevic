---
name: pr-review
description: Review a GitHub PR (by number) or the current diff against GreenFinance-specific conventions (event contracts, EF migrations, Docker/compose sync, MassTransit wiring, test coverage). Use when the user asks to review a PR, review this diff, review my changes, or invokes /pr-review.
---

# PR review (GreenFinance)

This project has a specialized `pr-reviewer` subagent (`.claude/agents/pr-reviewer.md`)
that knows this repo's microservices conventions. Use it instead of a generic review
whenever reviewing changes in this repository.

## Steps

1. Figure out what to review:
   - If the user gave a PR number/URL, that's the target (`gh pr diff <number>` to
     preview it yourself if useful, but let the subagent fetch and analyze it).
   - If the user said "review my changes" / "review the diff" with no PR reference,
     the target is the current working tree diff against `main` (or the branch's
     merge-base with `main` if on a feature branch).
   - If genuinely ambiguous, ask the user which one they mean.
2. Dispatch to the subagent via the Agent tool with `subagent_type: pr-reviewer`.
   Give it a self-contained prompt: what to review (PR number, or "the diff on this
   branch against main"), and remind it to check `CLAUDE.md` and the relevant
   `.claude/skills/<service>/SKILL.md` files for any service the diff touches.
3. Relay the subagent's findings to the user as-is (most severe first) — don't
   re-summarize away specifics like file paths or line numbers.
4. Do not apply any fixes yourself unless the user explicitly asks you to after
   seeing the findings.
