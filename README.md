<img src="https://github.com/DarkspyreStudios/Persist/raw/main/.github/banner.svg" alt="Darkspyre Persist">

# Darkspyre Persist

<!-- ownership-notice:start -->
> Copyright (c) 2026 Darkspyre Studios, LLC.
> Licensed under the [MIT License](LICENSE.md). Provided "as is", without warranty.
<!-- ownership-notice:end -->

Darkspyre Persist stores complete files for an application: settings, images, documents,
caches, exports. Application code addresses each file by a store name and a logical asset name,
and never builds a filesystem path. The default provider keeps files in the current user's
application-data directory and replaces each one atomically.

## Purpose

Most applications write a handful of files to a per-user folder, and most of them get the details
wrong in the same ways:

- A crash mid-write leaves a truncated file, and the next launch cannot read its own settings.
- A name taken from user input, such as `../../.ssh/config`, escapes the intended folder.
- The folder location differs per operating system, and each application computes it again.
- Code that builds paths directly cannot move to another backend later.

Persist solves these once. `IPersistStore` is a storage-neutral contract over raw bytes.
`OsUserPersistStore` implements it on the local filesystem:

- Every save writes to a private staging file, flushes it to disk, and moves it into place.
  A reader sees the complete old asset or the complete new one, never a partial write.
- Every name is validated. A name that could escape its store or alias another is rejected.
- Stores are isolated. Two applications that use different store names never see each other's
  assets.
- A missing asset reads as `null`, and an empty asset reads as an empty stream. The two are
  distinct.

Persist is not a credential vault. Store tokens, keys, and passwords with
`Darkspyre.SecureStore`.

## Local source consumption

`eng/SourceReferences.targets` replaces a consumer's `Darkspyre.Persist` package reference
with the `src/Darkspyre.Persist/Darkspyre.Persist.csproj` project in this worktree.
The consumer imports it after declaring package references. Reference metadata is retained;
runtime exclusions disable output copying, and compile exclusions disable the assembly reference.

## Install

```
dotnet add package Darkspyre.Persist
```

The package targets .NET 10. The namespace is `Darkspyre.Persist`.

## Quick start

```csharp
using Darkspyre.Persist;

IPersistStore persistence = new OsUserPersistStore();

await persistence.SaveStringAsync("ExampleApp", "notes/welcome.txt", "Hello from Darkspyre.");

string? text = await persistence.ReadStringAsync("ExampleApp", "notes/welcome.txt");
```

On macOS this writes `~/Library/Application Support/Darkspyre Studios/ExampleApp/notes/welcome.txt`.
Windows and Linux use their own per-user application-data directory.

With a service container, register the provider as a singleton:

```csharp
services.AddSingleton<IPersistStore, OsUserPersistStore>();
```

## Names

Every call takes two names.

`storeName` isolates one application or subsystem. It is a single segment, such as `ExampleApp`.
Pick one value per application and keep it stable across releases, because it addresses every
asset already saved.

`assetName` is a relative, `/`-delimited logical name, such as `images/portrait.webp`. The
provider creates intermediate directories as needed.

Both names must be portable across operating systems. The provider rejects:

- absolute names, backslashes, and empty segments
- the segments `.` and `..`
- segments that begin or end with whitespace, or end with `.`
- the characters `< > : " | ? *` and control characters
- Windows device names such as `CON`, `NUL`, and `COM1`, with or without an extension

An invalid name throws `ArgumentException`. Do not rely on two names that differ only by letter
case. Windows and macOS usually treat them as one file, and Linux treats them as two.

## The interface

`IPersistStore` operates on streams:

| Method | Behavior |
| --- | --- |
| `ExistsAsync(storeName, assetName)` | Reports whether the asset exists. |
| `OpenReadAsync(storeName, assetName)` | Returns a readable stream, or `null` when the asset is missing. The caller disposes the stream, and must not assume it is seekable. |
| `EnumerateAsync(storeName, assetNamePrefix)` | Returns every asset name that starts with the prefix, recursively, in ordinal order. An empty prefix lists the whole store. |
| `SaveAsync(storeName, assetName, content)` | Creates or atomically replaces the asset with the bytes from the stream's current position. The caller keeps ownership of the stream. |
| `DeleteAsync(storeName, assetName)` | Deletes the asset. Deleting a missing asset succeeds, and never creates directories. |

`PersistStoreExtensions` adds whole-asset helpers over the same contract:

| Method | Behavior |
| --- | --- |
| `SaveAsync(storeName, assetName, ReadOnlyMemory<byte>)` | Saves a byte buffer. |
| `SaveStringAsync(storeName, assetName, string)` | Saves a string as UTF-8 without a byte-order mark. |
| `ReadBytesAsync(storeName, assetName)` | Reads the asset into a byte array, or returns `null`. |
| `ReadStringAsync(storeName, assetName)` | Reads the asset as UTF-8 text, or returns `null`. |

Every method accepts an optional `CancellationToken`.

## Examples

### Application settings as JSON

```csharp
using System.Text.Json;
using Darkspyre.Persist;

public sealed record Settings(string Theme, int FontSize);

public sealed class SettingsService(IPersistStore persistence)
{
    private const string StoreName = "ExampleApp";
    private const string AssetName = "settings.json";

    public async Task<Settings> LoadAsync()
    {
        string? json = await persistence.ReadStringAsync(StoreName, AssetName);
        return json is null
            ? new Settings("light", 12)
            : JsonSerializer.Deserialize<Settings>(json)!;
    }

    public Task SaveAsync(Settings settings) =>
        persistence.SaveStringAsync(StoreName, AssetName, JsonSerializer.Serialize(settings));
}
```

A crash during `SaveAsync` leaves the previous `settings.json` intact.

### Streaming a large file

The stream methods copy data without loading the whole asset into memory:

```csharp
await using (FileStream source = File.OpenRead(pickedFilePath))
{
    await persistence.SaveAsync("ExampleApp", "images/portrait.webp", source);
}

await using Stream? image = await persistence.OpenReadAsync("ExampleApp", "images/portrait.webp");
if (image is not null)
{
    await image.CopyToAsync(destination);
}
```

### Listing and clearing a folder

`EnumerateAsync` matches the prefix as plain text. End the prefix with `/` to match one folder
only. The prefix `cache` also matches `cache-old/data.bin`, and `cache/` does not.

```csharp
IReadOnlyList<string> cached = await persistence.EnumerateAsync("ExampleApp", "cache/");

foreach (string assetName in cached)
{
    await persistence.DeleteAsync("ExampleApp", assetName);
}
```

### Testing against a temporary directory

`new OsUserPersistStore(rootDirectory)` roots every store beneath an explicit directory. A
test uses it to exercise the real filesystem without touching the user's application data:

```csharp
string root = Directory.CreateTempSubdirectory().FullName;
try
{
    IPersistStore persistence = new OsUserPersistStore(root);
    var settings = new SettingsService(persistence);

    await settings.SaveAsync(new Settings("dark", 14));

    Assert.Equal("dark", (await settings.LoadAsync()).Theme);
}
finally
{
    Directory.Delete(root, recursive: true);
}
```

A host that owns a different application-data root uses the same constructor.

## Storage layout

`OsUserPersistStore` keeps each store beneath:

```text
Environment.SpecialFolder.ApplicationData/Darkspyre Studios/<storeName>/<assetName>
```

Each store holds a `.darkspyre-persistence` directory for staging files. `EnumerateAsync` never
returns it, and an asset name cannot use it. The layout is an implementation detail of this
provider and is not part of `IPersistStore`.

## Boundaries

- Persist does not produce public or browser-loadable URLs. An application's delivery layer
  reads through `IPersistStore` and decides which bytes an authorized caller receives.
- Persist is not a credential vault. Store secrets with `Darkspyre.SecureStore`.
- Persist does not host a live database file. A database provider owns its own file. A
  consistent export or backup may be saved as a separate asset.

## Errors

| Situation | Result |
| --- | --- |
| Reading a missing asset | returns `null` |
| Deleting a missing asset | succeeds |
| Enumerating a store that does not exist | returns an empty list |
| Invalid store name or asset name | `ArgumentException` |
| Content stream that cannot be read | `ArgumentException` |
| Access denied, disk full, or another filesystem failure | the underlying `IOException` or `UnauthorizedAccessException` |
