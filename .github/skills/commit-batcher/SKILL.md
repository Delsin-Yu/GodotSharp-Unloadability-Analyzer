---
name: commit-batcher
description: 'Review a large git worktree, group changes into small coherent batches, create simplified Chinese commit messages, and explain the per-batch rationale in reusable commit-detail bullets. Use when: split commits, batch commits, review workspace changes, commit restructuring, commit rationale, Chinese commit messages, commit detail.'
argument-hint: 'Review the current git changes, group them into small coherent commits, commit each batch with a clear simplified Chinese message, and return per-batch rationale plus reusable commit-detail bullets.'
user-invocable: true
---

# Commit Batcher

## When to Use

- Split a broad refactor into small reviewable commits.
- Review current workspace changes before staging.
- Create simplified Chinese commit messages for coherent batches, including reusable detail bullets in the commit body.
- Produce clear per-batch rationale that can be reused as commit description or review notes.
- Leave ambiguous or local-only files out of functional commits.

## Constraints

- Do not create one large catch-all commit unless the user explicitly asks for it.
- Do not mix unrelated local or temporary files into product-code commits.
- Do not rewrite history, amend commits, or discard user changes unless the user explicitly asks.
- Do not rely on assumptions about grouping until you inspect the actual git diff.
- Only commit batches that are understandable on their own.
- Do not stop at naming the batch; explain why the files belong together and what technical intent the batch represents.
- Do not return vague rationale such as “related changes” or “same feature”; tie the grouping back to concrete API, model, workflow, or behavior changes.
- Never put commit-splitting rationale, batching justification, exclusion notes, or “为何不合并” style explanations into the actual git commit message body.
- Treat grouping rationale as chat/report output only unless the user explicitly asks to preserve that rationale outside the commit object.

## Procedure

1. Inspect git status, changed files, and diff summaries to understand the scope.
2. If the change set is large, use the `SubRunner` helper agent for read-only analysis to propose safe commit groupings before staging anything.
3. Identify local-only, generated, temporary, debug, or editor-specific files that should be excluded or isolated.
4. For each proposed batch, write down the grouping basis before staging: the shared technical intent, why the files belong together, and what key changes define the batch.
5. Stage one coherent batch at a time, sanity-check the staged diff, prepare 2 to 5 reusable zh-Hans detail bullets that describe only the staged code or asset changes, then commit using a multi-line commit message whose first line is a concise zh-Hans title, followed by exactly one blank line, and then the detail bullets.
6. Treat the `提交细节` bullets as part of the actual commit message body by default, not merely as notes returned in chat, unless the user explicitly asks for title-only commits.
7. Before committing, verify that the detail bullets still match the staged diff rather than the earlier proposed grouping, and correct them if staging changed the scope.
8. Repeat until all commit-worthy batches are handled, and leave questionable leftovers uncommitted with a short explanation.
9. When practical, run a build or the most relevant verification step after major batches to reduce the risk of broken history.

## Commit Rules

- Prefer small commits that map to one intent, such as model refactor, service adaptation, resource migration, or cleanup.
- Commit messages must be in simplified Chinese and easy to understand.
- The default commit message format is: first line as a short verb-phrase title, then exactly one blank line, then 2 to 5 `- ` bullet lines as the commit body.
- Do not place the first detail bullet on the line immediately after the title; the blank line between subject and body is mandatory.
- When using git tools or wrappers that accept a single message string, ensure the string contains `title + "\n\n" + body` rather than `title + "\n" + body`.
- Prefer titles in the form of a short verb phrase, for example: "重构可收集物模型与运行时数据体系".
- If a file is local configuration, debug scaffolding, or otherwise ambiguous, keep it out of functional commits unless its purpose is verified.
- For every batch, include a concrete “分组依据” explanation tied to APIs, type signatures, runtime flow, data model changes, or documentation scope.
- For every batch, prepare reusable “提交细节” bullets that are written into the commit message body and can also serve as review notes or changelog fragments.
- Commit-message bullets must describe concrete staged changes only; they must not talk about why files were split, why other files were excluded, or why neighboring batches were kept separate.
- Hard ban phrases such as “拆分提交”, “单独提交”, “留在工作区外”, “不并入本次提交”, “便于区分本批次与其他批次”, or similar batching/process commentary inside the actual commit body.
- Good detail bullets name the concrete changes, for example: interface split, runtime storage rename, caller adaptation, rule clarification, or build/documentation verification.
- Avoid generic detail bullets such as “优化代码结构”, “整理逻辑”, or “更新相关内容” unless they are backed by specific technical points.
- Do not let `提交细节` merely restate the commit title; each bullet should add new information about concrete API changes, affected call sites, knowledge updates, or verification.
- If a split between neighboring batches is not obvious, explain why they were intentionally separated instead of merged into one commit.

Example commit message shape:

```text
支持手动装配类型跳过自动装配诊断

- 新增 ManualAssemblyAttribute，用类级标记表达该类型已由开发者手动装配。
- 依赖注入生成器增加统一属性识别逻辑，同时兼容中文名称、英文名称和全限定名匹配。
- 生成器在扫描接口实现类型时跳过带手动装配标记的具体类，避免误报自动装配缺失诊断。
```

## Verification Guidance

- Prefer the existing workspace build tasks over ad-hoc shell wrappers.
- Use `Build Main Project` for a quick verification build.
- Use `Build Main Project to Log` when you need a persistent build log.
- Use `Reuild Main Project to Log` when you need a non-incremental logged build.
- For XML documentation cleanup passes, prefer `Reuild Main Project to Log` to generate a fresh `build.log`, then use `Build Main Project to Log` for follow-up verification.
- When batching or validating XML documentation warning cleanup, treat the repository-root `build.log` as the source of truth rather than terminal summaries, because terminal output may truncate or corrupt Chinese log lines.
- In AkisFarm, the actionable XML documentation warning codes for cleanup and verification are `CS1591`, `CS1573`, `CS1572`, `CS1574`, `CS1580`, `CS1581`, and `CS1712`; verification can directly check whether `build.log` still contains any of these codes.
- When validating commit message formatting, prefer a raw or medium git log view rather than `--oneline`, and confirm there is a blank line between the subject and the first detail bullet.

## Output Format

Return:

1. A brief summary of the grouping strategy.
2. For each proposed or created batch: file list, `分组依据`, and 2 to 5 `提交细节` bullets written in simplified Chinese.
3. The commits created, in order, with their commit hashes and full zh-Hans commit messages, including the detail-bullet body.
4. Any files intentionally left uncommitted, with the reason.
5. The verification you ran, or a short note if verification was not possible.

### Output Expectations

- `分组依据` should explain the shared intent of the batch in 2 to 4 concrete sentences.
- `提交细节` should be phrased so they can be written directly into the commit message body with minimal editing.
- `提交细节` must match the final staged diff of that batch, not just the initial plan.
- Unless the user explicitly asks otherwise, the created commit should already contain those `提交细节` bullets in its body rather than only returning them in chat.
- The actual commit object must contain a blank separator line between the title and the first `- ` detail bullet; a visually wrapped single-paragraph message is not acceptable.
- “分组依据”、“为何不合并”、leftover explanations, and other batching rationale belong in the returned report, not in the actual commit object.
- If a knowledge file under `memories/repo` is tightly coupled to a code batch, explicitly state whether it should be committed together with that batch or separately, and why.
- If a file is left out because its purpose is uncertain, say what evidence would be needed before it can be safely committed.
- When a separation decision may be questioned, add a brief `为何不合并` explanation for that batch.