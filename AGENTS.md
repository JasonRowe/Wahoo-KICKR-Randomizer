# AGENTS.md — Wahoo-KICKR-Randomizer (BikeFitnessApp: "Hack Your Ride")

C#/.NET (Avalonia) desktop app that turns a Wahoo KICKR trainer into a mountain simulator. Slack: `#bike-fitness`. All changes ship via PR.

## Dev Loop (All dev agents)

1. **Scope**: Read requirements in Slack. If unclear, ask Jason first—never guess scope.
2. **Branch**: Never commit directly to the default branch (`main`). Always work on a feature branch.
3. **Develop**: Keep diffs small and follow existing repo style.
4. **Test**: Add/update unit tests and test locally (`dotnet build` & `dotnet test`). For hardware verification (BLE, physical trainer), state it explicitly and ask Jason to test—never claim done without physical verification.
5. **PR**: Open a PR with a concise summary (`what` / `why` / `tests run` / `verification needed`). Tag Jason and relevant agents.
6. **Merge**: Only merge after Jason approves. Once approved, merge yourself:
   `gh pr merge <n> --squash --delete-branch`

## Guardrails

- **Secrets**: Never commit credentials or secrets (`.env`, tokens/keys). 
- **Approvals**: Ask Jason first before destructive/irreversible actions (deleting data, killing services, rewriting history) or anything leaving the machine.
- **Roles**: OpenClaw "Verity Researcher" is read-only (no code/push).
- **Style**: Small diffs > giant ones; ask when in doubt.
