# AGENTS.md — Wahoo-KICKR-Randomizer (BikeFitnessApp: "Hack Your Ride")

C#/.NET (Avalonia) desktop app that turns a Wahoo KICKR smart trainer into a mountain
simulator (no subscriptions). Channel: #bike-fitness. Desktop app — no box runtime/deploy
checkout; ship changes via PR like any Verity repo.

## Standard Ways of Working (applies to all dev agents — Hermes / Verity Systems Dev etc.)

Dev agents working in this repo MUST follow this workflow. The OpenClaw "Verity Researcher"
is read-only and does not code/push.

### The dev loop
1. Read the requirement in Slack. If unclear/incomplete -> ask Jason questions first. Never guess scope.
2. Never commit to the default branch directly. Work on a feature branch pushed to GitHub
   (use a git worktree or separate clone, NOT the running deploy checkout).
3. Implement the change in small diffs, following existing repo style.
4. Add/update unit tests for your change.
5. Test locally. If the change needs real-world/hardware verification (camera, MQTT, live
   pantry + Google Keep, Kasa/TP-Link cloud, cron end-to-end, physical device) say so explicitly
   and ask Jason to do it. Don't claim done when it needs Jason's hands.
6. Open a PR with a concise summary: what / why / tests run / anything needing Jason or another
   agent to verify. Tag reviewers (Jason + relevant agents).
7. Do not merge your own PR. It merges after review/approval.

### Git & deploy
- Source of truth = GitHub. All changes ship via PR -> merge to the default branch.
- Deployed runtime checkouts on the boxes stay on the default branch and update ONLY via
  `git pull --ff-only origin <branch>` (NO rsync of code, no hand-editing the deploy tree for
  feature work).
- After a merge, deploy = pull in the deploy checkout, then confirm the service/cron run.

### Guardrails
- Never commit secrets (.env, .tplink_creds, tokens/keys). Real secrets live in the vault
  (/etc/verity/*.env). Keep .gitignore authoritative.
- Destructive or irreversible actions (deleting data, killing services, history rewrites) -> ask first.
- Anything that leaves the machine (posts, external sends, real money) -> ask first.
- Small diffs > giant ones; ask when in doubt.
