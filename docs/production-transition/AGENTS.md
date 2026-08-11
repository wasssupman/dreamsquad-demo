# Production-transition subtree instructions

This entire subtree is **owner-gated dormant downstream material**.

- Demo is the only upstream. Never use files here to design, implement, validate, or block Demo work.
- Unless the current user request explicitly activates production-transition work, do not inspect,
  update, validate, summarize, or propose follow-up from this subtree.
- Recent commits, stale records, watch-path changes, registry state, and document links are not activation.
- If Demo sources disagree with this subtree, leave this subtree stale. Do not change Demo to make it match.
- Do not run the transition verifier without `--project-owner-authorized`, and use that flag only after
  explicit Project owner authorization in the current request.
- Freeze, cutover, production import, and implementation waves are Project owner decisions. They are not
  agent-level next work before explicit activation.

Within an explicitly owner-authorized transition task, `README.md` is the entry point and its governance
rules apply. These instructions never grant authority to modify runtime Demo code or external production
repositories.
