# Repository instructions

- `DarkspyrePersistence` is the single canonical implementation of general-purpose blob persistence. Consumers reference the private `Darkspyre.Persistence` NuGet package; do not copy its source into application repositories.
- Preserve the storage-neutral `IPersistenceStore` contract. Backend paths, credentials, transport details, and browser-delivery concerns stay outside the public object-store contract.
- Every asset is addressed by both `storeName` and `assetName`. Keep stores isolated, keep asset names root-relative, and reject names that can escape or alias the configured root.
- Saving replaces one complete asset atomically. Missing reads return `null`, empty assets remain distinguishable from missing assets, and deleting a missing asset succeeds.
- Do not use this package for credentials or other secrets; use `Darkspyre.SecureStore`. Do not route a live database file through this object store.
- Keep the solution at zero warnings and run restore, build, test, format verification, pack, and the dependency vulnerability audit before release.
- Package releases use semantic versions and immutable Git tags. Never overwrite an already-published package version.
