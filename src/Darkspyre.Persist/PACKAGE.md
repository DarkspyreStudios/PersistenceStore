# Darkspyre.Persist

Storage-neutral whole-asset persistence for .NET. `IPersist` addresses each asset by a store name and a root-relative
asset name. `OsUserPersist` stores the bytes in the current user's application-data directory and replaces each asset
atomically. The package targets .NET 10. Its namespace is `Darkspyre.Persist`.

## Install

```bash
dotnet add package Darkspyre.Persist
```

## Setup and usage

```csharp
using Darkspyre.Persist;

IPersist persistence = new OsUserPersist();
await persistence.SaveStringAsync("ExampleApp", "notes/welcome.txt", "Hello from the application.");
string? text = await persistence.ReadStringAsync("ExampleApp", "notes/welcome.txt");
```

A service container can register `IPersist` with `OsUserPersist` as a singleton. A host that owns another data root
constructs `new OsUserPersist(rootDirectory)`. The caller retains ownership of that directory.

Every call takes `storeName` and `assetName`. Keep the store name stable across releases; changing it addresses a
different store. Asset names use `/`, such as `images/portrait.webp`. The provider creates intermediate directories.
Names must not escape the root or alias another asset. Absolute names, backslashes, empty segments, `.` and `..`,
reserved device names and nonportable filename characters are rejected. Do not distinguish names only by letter case.

## Streams and complete assets

```csharp
await using (Stream source = File.OpenRead(pickedFilePath))
    await persistence.SaveAsync("ExampleApp", "images/portrait.webp", source);

await using Stream? saved = await persistence.OpenReadAsync("ExampleApp", "images/portrait.webp");
if (saved is not null)
    await saved.CopyToAsync(destination);
```

`SaveAsync` reads from the content stream's current position. The caller keeps ownership of that stream. A save
replaces the complete asset; readers see the complete old value or the complete new value. A returned read stream
belongs to the caller and need not be seekable.

| Operation | Behavior |
| --- | --- |
| `ExistsAsync(storeName, assetName)` | Reports whether the asset exists. |
| `OpenReadAsync(storeName, assetName)` | Returns a stream, or null for a missing asset. |
| `EnumerateAsync(storeName, prefix)` | Lists matching asset names recursively in ordinal order. |
| `SaveAsync(storeName, assetName, content)` | Creates or replaces one complete asset. |
| `DeleteAsync(storeName, assetName)` | Removes the named asset. Deleting a missing asset succeeds. |

`PersistExtensions` supplies byte-array and UTF-8 string reads and writes. A missing read returns null; an empty asset
remains distinct from a missing asset. An absent store enumerates as an empty list. Every operation accepts a
cancellation token.

`EnumerateAsync` matches a plain prefix. Use `cache/` to select the contents of one logical folder; `cache` also
matches `cache-old/data.bin`. Deletion is explicit:

```csharp
foreach (string asset in await persistence.EnumerateAsync("ExampleApp", "cache/"))
    await persistence.DeleteAsync("ExampleApp", asset);
```

## Boundaries and errors

- Store credentials and keys in a credential vault, not Persist.
- Do not place a live database file in the object store. A database provider owns its file; a consistent export can
  be saved as an asset.
- The package does not create public URLs. The application's delivery layer authorizes access and reads the bytes.
- Invalid names or unreadable input streams throw `ArgumentException`. Filesystem and permission failures propagate
  as `IOException` or `UnauthorizedAccessException`.

The package is licensed under MIT. `LICENSE.md` contains the license text.
