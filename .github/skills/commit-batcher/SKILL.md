---
name: commit-batcher
description: 'Review a large git worktree, group changes into small coherent batches, create English commit messages, and explain the per-batch rationale in reusable commit-detail bullets. Use when: split commits, batch commits, review workspace changes, commit restructuring, commit rationale, English commit messages, commit detail.'
argument-hint: 'Review the current git changes, group them into small coherent commits, commit each batch with a clear English message, and return per-batch rationale plus reusable commit-detail bullets.'
user-invocable: true
---

# Commit Batcher

## When to Use

- Split a broad refactor into small reviewable commits.
- Review current workspace changes before staging.
- Create English commit messages for coherent batches, including reusable detail bullets in the commit body.
- Produce clear per-batch rationale that can be reused as commit description or review notes.
- Leave ambiguous or local-only files out of functional commits.

## Constraints

- Do not create one large catch-all commit unless the user explicitly asks for it.
- Do not mix unrelated local or temporary files into product-code commits.
- Do not rewrite history, amend commits, or discard user changes unless the user explicitly asks.
- Do not rely on assumptions about grouping until you inspect the actual git diff.
- Only commit batches that are understandable on their own.
- Do not stop at naming the batch; explain why the files belong together and what technical intent the batch represents.
- Do not return vague rationale such as "related changes" or "same feature"; tie the grouping back to concrete API, model, workflow, or behavior changes.
- Never put commit-splitting rationale, batching justification, exclusion notes, or "why not merged" style explanations into the actual git commit message body.
- Treat grouping rationale as chat/report output only unless the user explicitly asks to preserve that rationale outside the commit object.

## Procedure

1. Inspect git status, changed files, and diff summaries to understand the scope.
2. If the change set is large, use the `SubRunner` helper agent for read-only analysis to propose safe commit groupings before staging anything.
3. Identify local-only, generated, temporary, debug, or editor-specific files that should be excluded or isolated.
4. For each proposed batch, write down the grouping basis before staging: the shared technical intent, why the files belong together, and what key changes define the batch.
5. Stage one coherent batch at a time, sanity-check the staged diff, prepare 2 to 5 reusable detail bullets that describe only the staged code or asset changes, then commit using a multi-line commit message whose first line is a concise English title, followed by exactly one blank line, and then the detail bullets.
6. Treat the detail bullets as part of the actual commit message body by default, not merely as notes returned in chat, unless the user explicitly asks for title-only commits.
7. Before committing, verify that the detail bullets still match the staged diff rather than the earlier proposed grouping, and correct them if staging changed the scope.
8. Repeat until all commit-worthy batches are handled, and leave questionable leftovers uncommitted with a short explanation.
9. When practical, run a build or the most relevant verification step after major batches to reduce the risk of broken history.

## Commit Rules

- Prefer small commits that map to one intent, such as model refactor, service adaptation, resource migration, or cleanup.
- Commit messages must be in English and easy to understand.
- The default commit message format is: first line as a short verb-phrase title, then exactly one blank line, then 2 to 5 `- ` bullet lines as the commit body.
- Do not place the first detail bullet on the line immediately after the title; the blank line between subject and body is mandatory.
- When using git tools or wrappers that accept a single message string, ensure the string contains `title + "\n\n" + body` rather than `title + "\n" + body`.
- Prefer titles in the form of a short verb phrase, for example: "Refactor collectible model and runtime data system".
- If a file is local configuration, debug scaffolding, or otherwise ambiguous, keep it out of functional commits unless its purpose is verified.
- For every batch, include a concrete "Grouping basis" explanation tied to APIs, type signatures, runtime flow, data model changes, or documentation scope.
- For every batch, prepare reusable "Commit details" bullets that are written into the commit message body and can also serve as review notes or changelog fragments.
- Commit-message bullets must describe concrete staged changes only; they must not talk about why files were split, why other files were excluded, or why neighboring batches were kept separate.
- Hard ban phrases such as "split commit", "separate commit", "kept outside the workspace", "not included in this commit", "to distinguish this batch from other batches", or similar batching/process commentary inside the actual commit body.
- Good detail bullets name the concrete changes, for example: interface split, runtime storage rename, caller adaptation, rule clarification, or build/documentation verification.
- Avoid generic detail bullets such as "optimize code structure", "clean up logic", or "update related content" unless they are backed by specific technical points.
- Do not let detail bullets merely restate the commit title; each bullet should add new information about concrete API changes, affected call sites, knowledge updates, or verification.
- If a split between neighboring batches is not obvious, explain why they were intentionally separated instead of merged into one commit.

Example commit message shape:

```text
Support manual assembly types skipping auto-assembly diagnostics

- Add ManualAssemblyAttribute to express that a type has been manually assembled by the developer.
- Dependency injection generator adds unified attribute recognition logic, supporting Chinese name, English name, and fully qualified name matching.
- Generator skips concrete classes with manual assembly mark when scanning interface implementations, avoiding false positive auto-assembly missing diagnostics.
```

## Verification Guidance

- Prefer the existing workspace build tasks over ad-hoc shell wrappers.
- Use `Build Main Project` for a quick verification build.
- Use `Build Main Project to Log` when you need a persistent build log.
- Use `Reuild Main Project to Log` when you need a non-incremental logged build.
- When validating commit message formatting, prefer a raw or medium git log view rather than `--oneline`, and confirm there is a blank line between the subject and the first detail bullet.

## Output Format

Return:

1. A brief summary of the grouping strategy.
2. For each proposed or created batch: file list, grouping basis, and 2 to 5 commit detail bullets written in English.
3. The commits created, in order, with their commit hashes and full English commit messages, including the detail-bullet body.
4. Any files intentionally left uncommitted, with the reason.
5. The verification you ran, or a short note if verification was not possible.

### Output Expectations

- Grouping basis should explain the shared intent of the batch in 2 to 4 concrete sentences.
- Commit details should be phrased so they can be written directly into the commit message body with minimal editing.
- Commit details must match the final staged diff of that batch, not just the initial plan.
- Unless the user explicitly asks otherwise, the created commit should already contain those detail bullets in its body rather than only returning them in chat.
- The actual commit object must contain a blank separator line between the title and the first `- ` detail bullet; a visually wrapped single-paragraph message is not acceptable.
- Grouping basis, "why not merged" explanations, leftover explanations, and other batching rationale belong in the returned report, not in the actual commit object.
- If a knowledge file under `memories/repo` is tightly coupled to a code batch, explicitly state whether it should be committed together with that batch or separately, and why.
- If a file is left out because its purpose is uncertain, say what evidence would be needed before it can be safely committed.
- When a separation decision may be questioned, add a brief "why not merged" explanation for that batch.
