# CDC 2.11.1 adoption

Source: `bajoicheg/g-spiral@d52b9fff3fabcdd4d4fac3a38951cbf36152b27d` on `main`.
Canonical release: `a262f78b82cd9e8eba9bc3b6108e0b52a17c33b0`; exact package tree: `6ffacd32cce74c3537150778d9b37cfeb361a621`.
Policy migration uses strict YAML and section replacement; replay is a no-op.
Adapter and checkpoint v4 validators pass under released CDC 2.11.1.

This process-only update preserves the product task, candidate/release SHA, prior platform evidence and budget/audit history. The live adoption result and exact lease release are recorded separately on cdc/coordination; historical checkpoint lease fields are not current authority.

This update changes the CDC dependency and project policy/checkpoint only. It does not close a product implementation task or provide new Windows/Android/runtime evidence. Existing product release and acceptance gates remain unchanged. No scheduler mutation or product Compute/CI launch is part of this adoption.
