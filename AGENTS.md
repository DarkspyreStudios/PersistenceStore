# Repository instructions

- Preserve the storage-neutral `IPersist` contract. Backend paths, credentials, transport details, and browser-delivery concerns stay outside the public object-store contract.
- Every asset is addressed by both `storeName` and `assetName`. Keep stores isolated, keep asset names root-relative, and reject names that can escape or alias the configured root.
- Saving replaces one complete asset atomically. Missing reads return `null`, empty assets remain distinguishable from missing assets, and deleting a missing asset succeeds.
- Do not use this package for credentials or other secrets; use `Darkspyre.SecureStore`. Do not route a live database file through this object store.
