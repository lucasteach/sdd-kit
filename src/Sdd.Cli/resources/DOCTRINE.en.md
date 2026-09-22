# SDD-Kit Doctrine v1.1

1. **Spec before code**: no functional code without an approved spec.
2. **Draft → ratification → bounded phases**: closed scope, visible milestone.
3. **Dated, statused decisions**: open / ratified / deferred; a deferred
   decision names its resolution phase.
4. **Atomic commits**, clean tree, message type(scope): subject, hash quoted
   in reports.
5. **One bounded task at a time** per agent; never two agents on the same
   working tree.
6. **Anti-forgetting**: every deferred idea is recorded in the backlog in the
   very commit that defers it.
7. **Repo memory**: AGENT_STATE at every phase or 3 commits; new chat =
   "Read docs/AGENT_STATE.md and continue".
8. **Owner validation**: no visual task is closed without a recorded capture
   or an owner check.
9. **Honesty layer**: data → real rendering; absence → empty state with an
   action; failure → readable alert; success → only if N > 0.
10. **Tooling boundary**: internal tools do not leak into shipped code; only
    public libraries travel.
11. **Namespace separation**: `Dn` names a spec decision, `#N` names a hard
    doctrine rule. Never the reverse.
    - *Example*: "D3 ratified" points to the spec; "rule #12" points to this
      doctrine.
    - *Anti-pattern*: writing "rule 3" for a spec decision, or "D12" for a
      doctrine rule — the reader no longer knows what to re-read.
12. **Forensic audit before assertion**: a hypothesis is not a source until it
    has been checked against the disk (file, line, command output).
    - *Example*: "the field is missing" is written after a `grep -n`, not
      after a recollection.
    - *Anti-pattern*: asserting a file contains X because an earlier report
      said so.
13. **Agent memory is a cache, never a source**: what is not in the repo does
    not exist; a conversation, summary or chat context is dropped the moment
    it contradicts the repo.
    - *Example*: a commit hash is re-read from `git log`, never from memory.
    - *Anti-pattern*: "per the previous session, the test passes".
14. **Volatile memory**: `AGENT_STATE.md` stays under 5 KB and carries only
    current state; forensic detail moves to `HISTORY_ARCHIVE`.
    - *Example*: the session commit table is bounded to the latest entries,
      the rest archived.
    - *Anti-pattern*: an AGENT_STATE replaying the whole history until the
      next agent cannot read it.
