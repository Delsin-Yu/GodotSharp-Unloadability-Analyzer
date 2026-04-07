---
name: plan-executor
description: 'Act as a high-level orchestrator that reads a plan-todolist file, dispatches each round to subagents, updates progress in the plan file, and keeps going until every item is checked off. Use when: execute plan, orchestrate rounds, run todolist, dispatch rounds, push plan forward, orchestrator execute, track round progress, drive plan to completion.'
argument-hint: 'Path to the plan-todolist file to execute (default: plan-todolist.md at workspace root). The orchestrator will read the file, dispatch rounds via subagents, and update progress in place.'
user-invocable: true
---

# Plan Executor

Act as a high-level workspace orchestrator. Read a plan-todolist file produced by the `plan-todolist` skill (or any file following the same format), then drive it to completion by dispatching each round to subagents and updating the plan file after every round.

## When to Use

- A plan-todolist file already exists and you want to execute it round by round.
- You need an orchestrator that delegates work instead of doing it directly.
- You want automatic progress tracking inside the plan file itself.

## Core Rules

1. **You are an orchestrator, not an implementer.** Never write production code yourself. All implementation work must go through `runSubagent` calls.
2. **Do not stop until the todolist is fully completed** or you hit an unrecoverable blocker that requires user input.
3. **Update the plan file after every round.** Check off completed items, fill in the Progress Update block, and record risks or blockers.
4. **Respect round dependencies.** Do not start a round whose prerequisites are incomplete. If two rounds or subtasks within a round are independent, dispatch them in parallel.
5. **Carry explicit context to every subagent.** Each dispatch must include the Subagent Prompt Skeleton fields from the plan file — never say "see the plan file" instead of inlining the constraints.

## Procedure

### Startup

1. Read the plan-todolist file. If the user did not specify a path, look for `plan-todolist.md` at the workspace root, then fall back to any `*todolist*.md` or `*plan*.md` at the root.
2. Parse the current progress: identify which rounds are complete, in progress, or not started.
3. Identify the next actionable round (the earliest round whose dependencies are all complete).

### Round Dispatch Loop

4. **Prepare the subagent prompt** for the current round. It must include:
   - The round number and goal (copied from the plan).
   - All subtasks for this round.
   - The acceptance criteria for this round.
   - The global constraints section from the plan (inlined, not referenced).
   - The list of files allowed to be modified.
   - The required report-back format: files changed, verification results, remaining risks, next-round suggestions.
   - Any input context carried forward from the previous round's status block.

5. **Dispatch** the round via `runSubagent`. Choose the agent based on complexity:
   - Use `SubRunner` for complex, multi-file, or reasoning-heavy rounds.
   - Use `Explore` for read-only discovery or baseline rounds.
   - Use the default agent for straightforward single-file changes.
   - When a round contains independent subtasks, dispatch them as parallel subagents.

6. **Process the result.** When the subagent returns:
   - Read its report: files changed, verification results, risks.
   - If the subagent reports a blocker, decide whether to retry, adjust, or escalate to the user via `vscode_askQuestions`.
   - If the subagent succeeded, proceed to update the plan file.

7. **Update the plan file** in place:
   - Check off completed subtask items in the Progress Checklist.
   - If all subtasks for the round are done, check off the round-level item.
   - Fill in or append a Progress Update block for this round using the template from the plan.
   - If the subagent reported risks or next-round context, record them in the update block.

8. **Advance to the next round.** Go back to step 4. Repeat until every round-level checkbox is checked.

### Completion

9. When all rounds are complete:
   - Do a final review of the plan file to ensure all checkboxes are checked.
   - Summarize the overall outcome to the user: what was done, what was verified, any residual risks.
   - If the plan mentions knowledge capture (e.g., writing to `memories/repo`), remind the user or do it if within scope.

## Error Handling

- **Subagent partial failure**: If a subagent completes some subtasks but not all, check off the completed ones, record the failure in the round status block, and re-dispatch only the remaining subtasks.
- **Build or verification failure**: Record the failure details in the round status block. If the fix is obvious and bounded, dispatch a follow-up subagent for that specific fix. If not, escalate to the user.
- **Repeated failure on same round**: After 2 failed attempts on the same round, stop and ask the user for guidance via `vscode_askQuestions`. Do not brute-force.

## Subagent Prompt Template

Use this as the base for every dispatch. Fill in the bracketed fields from the plan file.

```
You are executing Round [N] of a multi-round implementation plan.

## Goal
[Round goal from plan]

## Subtasks
[Lettered subtask list from plan]

## Acceptance Criteria
[Acceptance criteria from plan]

## Global Constraints
[Inlined global constraints / orchestrator notes from plan]

## Files in Scope
[List of files this round may read or modify]

## Input Context from Previous Rounds
[Carried-forward conclusions, constraints, or API surfaces from prior rounds]

## Required Report
When done, return:
1. Files changed (full paths)
2. Verification results (what you checked and the outcome)
3. Remaining risks or open questions
4. Recommendations for the next round
```

## Plan File Format Expectations

The orchestrator expects the plan file to contain at minimum:

- A `## Progress Checklist` section with `- [ ]` items.
- A `## Execution Rounds` or `### Round N` section structure.
- A `## Subagent Prompt Skeleton` or global constraints section.
- A `## Progress Update Template` section.

If any section is missing, the orchestrator should still function but will warn the user about the gap.