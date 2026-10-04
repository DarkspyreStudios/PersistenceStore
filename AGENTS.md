# Repository instructions

- `DarkspyrePersistence` is the single canonical implementation of general-purpose blob persistence. Development consumers use sibling project references; release consumers reference the private `Darkspyre.Persistence` NuGet package; do not copy its source into application repositories.
- Preserve the storage-neutral `IPersistenceStore` contract. Backend paths, credentials, transport details, and browser-delivery concerns stay outside the public object-store contract.
- Every asset is addressed by both `storeName` and `assetName`. Keep stores isolated, keep asset names root-relative, and reject names that can escape or alias the configured root.
- Saving replaces one complete asset atomically. Missing reads return `null`, empty assets remain distinguishable from missing assets, and deleting a missing asset succeeds.
- Do not use this package for credentials or other secrets; use `Darkspyre.SecureStore`. Do not route a live database file through this object store.
- Keep the solution at zero warnings and run restore, build, test, format verification, pack, and the dependency vulnerability audit before release.
- Package releases use semantic versions and immutable Git tags. Never overwrite an already-published package version.

## Local development and releases

Normal builds and tests use local source from the seven sibling workspace worktrees: agent-ds,
AgentDSPlugins, Inference, TensorSharp, RightSize, SecureStore and PersistenceStore. No source-root
flags or per-session local props files are required. Missing source must fail without a package fallback.
Do not build in primary checkouts or create interim NuGet packages. Read `~/.agent-ds/build-instructions.md`.

`DarkspyreDependencyMode=Packages` is explicit release publishing/deployment mode. Debug/Release,
signing and interim installation do not select it. Test copies retain compatible prebuilt GGML driver
hardlinks. Signed bundles may copy prebuilt drivers before signing. Native compilation always needs
an explicit request.
