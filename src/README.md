# DarkspyrePersistence

`DarkspyrePersistence` is a storage-neutral .NET blob store. Applications address each complete opaque
asset with two values:

- `storeName` isolates one application's or subsystem's assets from every other store.
- `assetName` is a root-relative, `/`-delimited logical name such as `images/portrait.webp`.

The `Darkspyre.Persistence` package contains the `IPersistenceStore` contract and the default
`OsUserPersistenceStore` filesystem implementation. It targets .NET 10.

## Contract

`IPersistenceStore` operates on raw bytes and does not serialize application models:

| Method | Behavior |
| --- | --- |
| `ExistsAsync(storeName, assetName)` | Reports whether one asset currently exists. |
| `OpenReadAsync(storeName, assetName)` | Returns a caller-owned readable stream, or `null` when the asset is missing. |
| `EnumerateAsync(storeName, assetNamePrefix)` | Returns matching logical names recursively in ordinal order. An empty prefix lists the complete store. |
| `SaveAsync(storeName, assetName, content)` | Creates or atomically replaces one complete asset using bytes from the stream's current position. The caller retains ownership of the input stream. |
| `DeleteAsync(storeName, assetName)` | Deletes one asset. Deleting a missing asset succeeds. |

Missing and empty assets are distinct: a missing read returns `null`, while a saved zero-byte asset
returns a readable empty stream. Convenience extensions provide complete byte-array and UTF-8 string
reads and writes over the same stream contract.

Store names are single portable filesystem segments. Asset names are relative logical paths that use
`/`; absolute paths, backslashes, empty segments, `.` and `..` are rejected. Do not depend on two names
that differ only by letter case because filesystem case behavior varies by operating system.

## Default OS provider

`OsUserPersistenceStore` keeps named stores beneath:

```text
Environment.SpecialFolder.ApplicationData/Darkspyre Studios/<storeName>/<assetName>
```

.NET resolves `ApplicationData` to the current user's appropriate application-data location on each
operating system. Writes are copied and flushed to private staging storage on the same filesystem,
then atomically moved into place. Readers see a complete old or new asset, never a partially written
destination.

```csharp
using System.Text;
using Darkspyre.Persistence;

IPersistenceStore persistence = new OsUserPersistenceStore();

const string storeName = "ExampleApp";
const string assetName = "documents/welcome.txt";

await using (var content = new MemoryStream(Encoding.UTF8.GetBytes("Hello from Darkspyre.")))
{
    await persistence.SaveAsync(storeName, assetName, content);
}

if (await persistence.ExistsAsync(storeName, assetName))
{
    await using Stream? saved = await persistence.OpenReadAsync(storeName, assetName);
    using var reader = new StreamReader(saved!);
    Console.WriteLine(await reader.ReadToEndAsync());
}

IReadOnlyList<string> documents =
    await persistence.EnumerateAsync(storeName, "documents/");

await persistence.DeleteAsync(storeName, assetName);
```

Pass an explicit root to `new OsUserPersistenceStore(rootDirectory)` when a host deliberately owns a
different application-data root or when an integration test needs a real temporary filesystem.

## Boundaries

- Persistence does not produce public or browser-loadable URLs. An application's authenticated delivery
  layer reads through `IPersistenceStore` and decides how authorized bytes are served.
- Persistence is not a credential vault. Store secrets with `Darkspyre.SecureStore`.
- Persistence is not the working lifecycle for a live database. A database provider owns its database;
  a consistent export or backup may be saved as a separate blob.
- Backend paths are implementation details and are not part of `IPersistenceStore`.
