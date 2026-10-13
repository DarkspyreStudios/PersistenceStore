# Repository instructions

- Preserve the storage-neutral `IPersist` contract. Backend paths, credentials, transport details, and browser-delivery concerns stay outside the public object-store contract.
- Every asset is addressed by both `storeName` and `assetName`. Keep stores isolated, keep asset names root-relative, and reject names that can escape or alias the configured root.
- Saving replaces one complete asset atomically. Missing reads return `null`, empty assets remain distinguishable from missing assets, and deleting a missing asset succeeds.
- Do not use this package for credentials or other secrets; use `Darkspyre.SecureStore`. Do not route a live database file through this object store.

## Operating-system boundaries

All OS-specific code in every Darkspyre project must live behind an OS-agnostic capability contract.

Portable consumers call that contract. Platform implementations own OS detection, native bindings and ABI layouts, native-library names and loading, system commands, platform paths, filesystem semantics, and platform-specific process and UI behaviour. Only platform composition selects the implementation. SDK contracts, plugins, extensions, core modules, and portable host code must not require consumers to know the operating system or call a platform implementation directly.

An interface that exposes OS-specific commands, types, paths, or layouts does not satisfy this rule. Reuse the existing capability seam. A platform without an implementation reports the capability unavailable through the same contract.

This boundary lets another operating system supply an implementation without changing portable consumers.

**Check.** Audit source and package configuration for OS branches, native imports, platform paths and commands, filesystem assumptions, and platform-only dependencies. Trace each hit through its callers and registration. Every OS-specific operation must remain inside a platform implementation selected through an OS-agnostic contract. A filename, namespace, annotation, or `IsMacOS()` guard alone does not establish that boundary.
