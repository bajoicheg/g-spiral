# CDC 2.11.2 adoption

Canonical release: `refs/heads/release/v2.11.2` at `48e637b230e7640d0dcd13da60d712f38f59a2b6`.
Exact package tree: `7a7a7faa75b7fc9160d912d8fb507c6b9573d17f`.
Adapter policy revision: `2026-09-30-cdc-2.11.2-fleet-adoption`.
Adapter semantic digest: `118c57ea352e32b5666b763956eb3deaae20be9abe9606ae45ed5c4c2fc20f11`.

Process-only adoption on released/no-guard generation 4. No product source, scheduler, external start, budget or release authority changes. The repo has no scheduled watchdog. The canonical fault-injection fixture is retained outside the immutable package only so vendored regression paths remain executable; the package subtree itself is byte-identical to canonical CDC 2.11.2.
