---
name: plan-todolist
description: 'Research a complex task, produce a round-by-round execution plan with a progress checklist that a high-level orchestrator agent can use to dispatch work to subagents and track completion. Use when: plan todolist, orchestrator plan, execution rounds, round-by-round plan, dispatch subagents, track progress, split task into rounds, complex task planning, multi-round execution, subagent coordination.'
argument-hint: 'Describe the complex task or feature to plan. The skill will research the codebase, clarify requirements, and produce a round-by-round plan-todolist file at the workspace root.'
user-invocable: true
---

# Plan Todolist

Produce a structured, round-by-round execution plan for a complex task so that a high-level orchestrator agent can dispatch each round to subagents in isolated contexts, track progress by updating the plan file, and push the work forward incrementally.

## When to Use

- A task is too large or cross-cutting for a single agent pass.
- You need to break work into sequential or partially-parallel rounds with clear handoff points.
- You want a persistent plan file that an orchestrator can update round by round to track completion.
- You need subagents to work in isolated contexts with explicit input/output contracts per round.

## Output

A single Markdown file (default: `plan-todolist.md` at workspace root; override with user instruction).

The file contains these sections in order:

1. **Design Summary** — What, why, recommended approach, key decisions, scope boundaries.
2. **Relevant Files** — Full paths with brief descriptions of what to modify or reuse.
3. **Execution Rounds** — Numbered rounds, each with: goal, subtasks, expected output, and acceptance criteria.
4. **Orchestrator Notes** — Dependency graph between rounds, parallelism opportunities, and mandatory sequencing.
5. **Progress Checklist** — A flat `- [ ]` checklist covering every round and every subtask, suitable for incremental check-off.
6. **Progress Update Template** — A copy-paste template the orchestrator fills in after each round completes.
7. **Subagent Prompt Skeleton** — The minimum context every subagent dispatch must carry.

## Constraints

- Do NOT start implementation. This skill only produces the plan file.
- Do NOT guess at codebase structure. Use read-only exploration (subagents, search, file reads) to gather real context before writing rounds.
- Do NOT leave ambiguous round boundaries. Each round must have a concrete acceptance criterion that can be verified without reading the next round.
- Do NOT create rounds that are too large to fit in a single subagent context window. Split further if needed.
- Do NOT omit the progress checklist. It is the primary tracking surface for the orchestrator.
- The plan file must be self-contained: a future agent reading only this file should understand the full task, all decisions, and how to execute each round.

## Procedure

### Phase 1 — Discovery

1. Read the user's task description carefully. Identify the what, why, and scope boundaries.
2. Launch read-only exploration subagents (use the `Explore` agent when available) to gather codebase context. When the task spans multiple independent areas, launch 2–3 subagents in parallel — one per area.
3. Check `./memories/repo/` for relevant repository knowledge that may inform the plan.
4. Identify analogous existing implementations that can serve as reference patterns for the new work.

### Phase 2 — Alignment

5. If research reveals major ambiguities, use `vscode_askQuestions` to clarify with the user. Surface discovered constraints and alternative approaches.
6. If answers significantly change scope, loop back to Discovery.
7. Record key decisions (confirmed scope, rejected alternatives, chosen patterns) — these go into the Design Summary.

### Phase 3 — Round Design

8. Group the work into rounds. Each round should be:
   - **Independently verifiable** — it has its own acceptance criteria.
   - **Bounded** — a single subagent can complete it in one pass.
   - **Explicitly dependent** — state which prior rounds it depends on and which can run in parallel.
9. For each round, write:
   - **Goal** — one sentence.
   - **Subtasks** — lettered (A, B, C…), each a concrete action.
   - **Expected output** — what the round produces (files changed, APIs added, behaviors verified).
   - **Acceptance criteria** — how to know the round is done.
10. Write the Orchestrator Notes section: dependency graph, parallelism, mandatory sequencing, and any global invariants every round must preserve.

### Phase 4 — Checklist & Templates

11. Generate the Progress Checklist: one `- [ ]` line per round completion, plus one per subtask within each round.
12. Generate the Progress Update Template: a fill-in-the-blank block the orchestrator copies after each round.
13. Generate the Subagent Prompt Skeleton: the minimum context payload every subagent dispatch must include (task identity, global constraints, round goal, acceptance criteria, allowed file scope, required report-back format).

### Phase 5 — Output

14. Write the complete plan file to the target location.
15. Show a scannable summary of the plan to the user (the plan file is for persistence, not a substitute for showing it).
16. Ask the user for approval or revision via `vscode_askQuestions`.

## Round Design Guidelines

- Prefer 3–6 rounds for most tasks. Fewer than 3 usually means the rounds are too large; more than 8 usually means they are too granular.
- Round 0 should always be a read-only baseline/discovery round that locks down ambiguous design decisions before any implementation begins.
- The final round should always include regression verification and knowledge capture.
- If two subtasks within one round have no dependency on each other, note them as parallelizable so the orchestrator can dispatch them simultaneously.
- Each round's acceptance criteria should be testable: a build command, a runtime check, a log inspection, or a specific file state — not "looks correct."

## Progress Update Template Shape

```markdown
### Round N Status
- Status: Not started / In progress / Complete / Blocked
- Completed:
  - Subtask A description
  - Subtask B description
- Files changed:
  - path/to/file.cs
- Verified:
  - Acceptance criterion 1
  - Acceptance criterion 2
- Risks & blockers:
  - Risk A
- Next-round input context:
  - Conclusion A
  - Constraint B
```

## Subagent Prompt Skeleton Shape

Every round dispatch to a subagent must include at minimum:

- Current round number and goal
- Acceptance criteria for this round
- Global constraints that apply to all rounds (list them explicitly, do not say "see plan")
- Files allowed to be modified in this round
- Required report-back: files changed, verification results, remaining risks, next-round recommendations