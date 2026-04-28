---
name: git-commit-push-zh
description: >
  在用户要求提交代码、git commit、git push、推送到远端、同步仓库等场景时，代理应在仓库内执行完整提交流程（查看状态与差异、暂存、中文说明的 commit、push），
  而不是只给出待用户手动复制的命令。适用于 J999 及同类需中文提交说明的仓库工作流。
---

# 中文提交并推送（git commit + push）

## 何时启用

用户提到任一即可：**提交**、**推送**、**git push**、**commit**、**推上去**、**同步到远端**、**保存到 git** 等，且意图是把当前改动落到远端分支。

## 必须遵守

1. **由代理执行命令**（本环境具备 shell），不要只写「请你本地执行…」清单。
2. **提交说明使用中文**；首行概括改动，必要时空一行后写要点列表。
3. **先看清再提交**：`git status` → 必要时 `git diff` / `git diff --stat`，确认要纳入的文件与影响范围。
4. **无改动则停止**：若工作区干净，说明已同步，不要造空提交。
5. **禁止擅自 `git push --force`**（尤其 `main`/`master`）；仅当用户**明确**要求强推时再执行，并再次确认分支名与风险。

## 推荐流程

1. `git status`（确认分支与未跟踪文件）。
2. 按用户意图暂存：`git add -p`（需交互时）或 `git add <路径>`；用户未指定且意图为「全部当前改动」时用 `git add -A`（注意别误加密钥或本机-only 文件；遵守 `.gitignore`）。
3. `git commit -m "中文标题" -m "可选正文"`；多文件改动时正文用 `-` 分行写清模块或原因。
4. `git push`（或用户指定的 `git push origin <分支>`）。
5. 若 push 失败：根据错误处理（无上游则 `git push -u origin HEAD`；认证失败则说明需 token/ssh，不猜测密码）。

## 提交说明示例

```
修复 autopull 在 root cron 下的 safe.directory 配置

- cd 到仓库后写入全局 safe.directory，避免 dubious ownership
```

## 与本仓库的衔接

- 若用户同时要求更新 README（例如后端约定），在 **commit 之前** 完成文件修改，再一并 `git add`。
- 大改动或跨多子项目时，首行写总览，正文按 `J9_Admin` / `J9_APP_103` 等分点说明。
