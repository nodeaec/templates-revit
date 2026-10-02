# Node.aec Revit Plugin Template

`dotnet new` project template for a licensed Autodesk Revit add-in with Node.aec offline license verification built in. Greenfield path for new plugins; existing standalone plugins adopt Lite via `PackageReference`.

## Install

```powershell
dotnet new install ./revit-templates
```

## Scaffold

```powershell
dotnet new nodeaec-revit-plugin -n MyPlugin --ProductSlug my-plugin --AddInId (New-Guid)
cd MyPlugin
dotnet build -p:RevitYear=2026
```

> **Pre-release note:** the generated project references
> `NodeAec.Licensing.Lite 1.0.0-preview.1` via `PackageReference`. Until that
> package is published, `dotnet build` fails at restore (`NU1101`, package not
> found). That failure confirms the reference resolves from NuGet.

## Generated files

| File | Role |
|---|---|
| `App.cs` | `IExternalApplication`: creates or reuses the shared `Node.aec` ribbon tab, adds the plugin panel (`PanelName`), inserts the command button idempotently, registers AdWindows deduplication hooks, and loads optional PNG icons with `BitmapCacheOption.OnLoad` + `Freeze()`. Contains no licensing calls. |
| `Commands/HelloCommand.cs` | Reference `IExternalCommand`: calls `NodeAecLicenseGate.Validate()` as the first statement, shows `Message` verbatim plus `OpenConnector()` and returns `Result.Cancelled` when not licensed, otherwise shows the greeting with the license block and returns `Result.Succeeded`. |
| `Licensing/NodeAecLicenseGate.cs` | Single licensing seam of the plugin. Holds `ProductSlug` and maps the `Gate.Validate()` result to `GateSnapshot`. Commands branch only on `GateSnapshot.IsLicensed`. |
| `MyPlugin.addin` | Add-in manifest. `<Assembly>` uses a path relative to the manifest location (`MyPlugin\MyPlugin.dll`). `<AddInId>` is set from `--AddInId`. |
| `MyPlugin.csproj` | Multi-targeting project driven by the `RevitYear` property (2023–2027). Declares `PackageReference` entries for the Revit API (reference-only, `PrivateAssets="all"`, `ExcludeAssets="runtime"`) and `NodeAec.Licensing.Lite`. |
| `scripts/release.ps1` | Release pipeline: builds the floor year of a compatibility group, stages the payload, and produces a versioned `.zip` with a `.sha256` checksum. With Inno Setup 6 installed, also compiles `scripts/installer.iss` into a per-group `Setup.exe`. |
| `scripts/installer.iss` | Inno Setup definition consumed by `release.ps1`. Installs the payload under `%ProgramData%\Autodesk\Revit\Addins\<year>\` for every installed year of the group. |

## Licensing integration

The template already wires the three integration points. After scaffolding,
only `ProductSlug` needs to match the product registered in the Node.aec catalog.

1. Package declaration (`MyPlugin.csproj`):

```xml
<PackageReference Include="NodeAec.Licensing.Lite" Version="1.0.0-preview.1" />
```

2. Product identifier (`Licensing/NodeAecLicenseGate.cs`):

```csharp
public const string ProductSlug = "my-plugin";
```

3. License check as the first statement of every `IExternalCommand.Execute`:

```csharp
var gate = NodeAecLicenseGate.Validate();
if (!gate.IsLicensed)
{
    // Show gate.Message verbatim, offer OpenConnector(), return Result.Cancelled.
}
```

Rules:

- Branch only on `IsLicensed`; never parse `Message`.
- Show `Message` verbatim (PT-BR, do not translate or reword).
- Never copy `RevitAPI*.dll`, `RevitAPIUI.dll`, `AdWindows.dll`, or `UIFramework*` to the output payload; they are provided by Revit at runtime.
- Never create a plugin-owned ribbon tab; always use the shared `Node.aec` tab with a plugin-owned panel.

## Parameters

| Flag | Default | Meaning |
|---|---|---|
| `-n` | `MyRevitPlugin` | Plugin, assembly, namespace, and manifest name. Also becomes the ribbon panel name — change `PanelName` in `App.cs` to use a display name with spaces. |
| `--ProductSlug` | `my-revit-plugin` | Product slug in the Node.aec catalog. Must match the entitlement claim checked by `Gate.Validate`. |
| `--AddInId` | Fresh GUID | Unique manifest identifier (`<AddInId>` in the `.addin` file). Generate one GUID per plugin; never reuse another add-in's value. |
| `--AppId` | Fresh GUID | Installer identity passed to `release.ps1` / `installer.iss`. One value per product, shared by all Revit-year groups, producing a single entry under Windows Settings > Apps. |

## Build matrix

`RevitYear` selects both the Revit API package version and the target framework:

| `RevitYear` | Target framework | Notes |
|---|---|---|
| `2023`, `2024` | `net48` | Compatibility group `2023-2024`, built with year `2023`. |
| `2025`, `2026` | `net8.0-windows` | Compatibility group `2025-2026`, built with year `2025`. `2026` stays on `net8.0-windows` so the same binary loads on Revit 2026.0–2026.4 and 2026.5. Default `RevitYear` is `2026`. |
| `2027` | `net10.0-windows` | Compatibility group `2027`, built with year `2027`. |

```powershell
dotnet build -p:RevitYear=2023   # net48
dotnet build -p:RevitYear=2026   # net8.0-windows (default)
dotnet build -p:RevitYear=2027   # net10.0-windows
```

The payload of a compatibility group is always compiled with the floor year of
the group (2023, 2025, 2027), so one DLL loads on every Revit year of that group.

## Deploy

Manual deployment (development loop, per Revit year):

```powershell
# Copy bin\<year>\<tfm>\*.dll and the .addin to:
# %ProgramData%\Autodesk\Revit\Addins\<year>\
```

Grouped release artifact (zip + checksum, optionally installer):

```powershell
powershell -ExecutionPolicy Bypass -File scripts/release.ps1 -Version 0.1 -RevitYear 2025-2026
```

`release.ps1` accepts one compatibility group per invocation
(`2023-2024` / `2025-2026` / `2027`); a single year (`2023`–`2027`) is accepted
as an alias and resolves to its group. With `-Install`, the staged payload is
also copied to every installed Revit year of the group, each with its own
absolute `<Assembly>` path in the manifest.

## Manual verification

1. Licensed machine: run the command, expect the greeting dialog with the
   license block and `Result.Succeeded`.
2. No lease, wrong slug, or expired entitlement: expect the blocked dialog
   showing the Node.aec reason verbatim and `Result.Cancelled`.
3. Reload the add-in twice: expect a single `Node.aec` tab (no duplicates).

## Repository layout

```
revit-templates/
├── .template.config/template.json   # shortName nodeaec-revit-plugin
├── content/__PluginName__/          # scaffolded as <YourName>/ by -n
│   ├── App.cs
│   ├── __PluginName__.csproj
│   ├── __PluginName__.addin
│   ├── Commands/HelloCommand.cs
│   ├── Licensing/NodeAecLicenseGate.cs
│   └── scripts/release.ps1 + installer.iss + installer-after*.txt
└── README.md
```
