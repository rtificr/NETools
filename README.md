# NETools

NETools is a retained-mode UI helper library for Spaceflight Simulator mods. It provides flexible,
nested layout primitives, resizable windows, reusable UI cursor visuals, and shared image assets.

## Highlights

- HTML-like retained UI trees via `Ui.Mount(...)`, `Row(...)`, `Column(...)`, `Card(...)`, and `Tabs(...)`
- Flex-style sizing and alignment helpers
- Native PNG-backed UI cursor and gizmo assets
- Window resizing and a configurable UI cursor overlay
- Standard UI elements including closable windows, number inputs, and radio buttons
- UITools-style opt-in update checks for NETools and dependent mods implementing `IUpdatable`

## Using NETools

Reference `NETools.dll` from your mod and add `NETools` as a mod dependency:

```csharp
public override Dictionary<string, string> Dependencies { get; } = new()
{
    { "NETools", "1.0.2" }
};
```

To opt into NETools' startup update check, implement `IUpdatable` on your mod's main class and
return the remote file URL and local destination path in `UpdatableFiles`. NETools checks hashes,
prompts before installing updates, and offers to restart after a successful update.

The hash service host can be changed before update checks run:

```csharp
ModsUpdater.HashApiBaseUrl = "https://updates.example.com";
```

That service must implement `GET /api/hashes/md5?file={URL-encoded-download-url}` and return the
file's raw MD5 digest encoded as Base64. The default host remains
`https://files.cucumber-space.online`.

```csharp
using NETools;

Ui.Mount(parent).Column(ui =>
{
    ui.Card("Example", card =>
    {
        card.Row(row =>
        {
            row.Label("Name").Width(120);
            row.TextInput("Value", OnChanged).Grow();
        });
    });
});
```

## Build

Set `GameManaged` to the Spaceflight Simulator managed-assemblies directory if necessary, then run:

```sh
dotnet build NETools.csproj -p:Deploy=true
```

`Deploy=true` copies `NETools.dll` to the game's `Mods/NETools/` folder.

## Relationship to UITools

NETools is a separate mod and is **not** a drop-in replacement for UITools. It has its own assembly
identity, namespace, mod ID, and installation directory, so both libraries can be installed side by side.
