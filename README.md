# Node.aec Revit Plugin Template

`dotnet new` project template for a licensed Autodesk Revit add-in with Node.aec offline license verification built in. The generated project references `NodeAec.Licensing.Lite` 0.3.0 from nuget.org via `PackageReference`, and the verification code ships inside the plugin's folder.

The [Sample Plugin](https://github.com/nodeaec/revit-sample-plugin) is this template's output with demo content, and its `API.md` is the full integration reference.

## Install

```powershell
dotnet new install NodeAec.Templates.RevitPlugin
```

To update an existing install: `dotnet new install NodeAec.Templates.RevitPlugin --force`.

## Scaffold

```powershell
dotnet new nodeaec-revit-plugin -n MyPlugin --ProductSlug my-plugin
cd MyPlugin
dotnet build -p:RevitYear=2026
```

Each run generates fresh GUIDs for the `.addin` `<AddInId>` and the installer `AppId`.

## Generated files

| File | Role |
|---|---|
| `App.cs` | `IExternalApplication`: creates or reuses the shared `Node.aec` ribbon tab, adds the plugin panel (`PanelName`), inserts the command button idempotently, registers AdWindows deduplication hooks, and loads optional PNG icons with `BitmapCacheOption.OnLoad` + `Freeze()`. Contains no licensing calls. |
| `Commands/HelloCommand.cs` | Reference `IExternalCommand` and the whole licensing integration: holds `ProductSlug`, calls `Gate.Validate(ProductSlug)` as the first statement, and when not licensed shows the gate's message in Revit's UI language with an `Open Node.aec Connector...` link and returns `Result.Cancelled`. Otherwise it shows the greeting with the license details (never the license key) and returns `Result.Succeeded`. |
| `MyPlugin.addin` | Add-in manifest. `<Assembly>` uses a path relative to the manifest location (`MyPlugin\MyPlugin.dll`). `<AddInId>` is a fresh GUID (or `--AddInId`). |
| `MyPlugin.csproj` | Multi-targeting project driven by the `RevitYear` property (2023–2027). Declares `PackageReference` entries for the Revit API (reference-only, `PrivateAssets="all"`, `ExcludeAssets="runtime"`) and `NodeAec.Licensing.Lite`. |
| `scripts/release.ps1` | Release pipeline: builds the floor year of a compatibility group, stages the payload, and produces a versioned `.zip` with a `.sha256` checksum. `-Version` is required and must match `<Version>` in the csproj. With Inno Setup 6 installed, also compiles `scripts/installer.iss` into a per-group `Setup.exe`. |
| `scripts/installer.iss` | Inno Setup definition consumed by `release.ps1`. Installs the payload under `%ProgramData%\Autodesk\Revit\Addins\<year>\` for every installed year of the group. |

## Licensing integration

The template already wires the three integration points. After scaffolding,
only `ProductSlug` needs to match the product registered in the Node.aec catalog
(`--ProductSlug` sets it).

1. Package declaration (`MyPlugin.csproj`):

```xml
<PackageReference Include="NodeAec.Licensing.Lite" Version="0.3.0" />
```

2. Product identifier (`Commands/HelloCommand.cs`; with several commands, move it to a shared static class):

```csharp
public const string ProductSlug = "my-plugin";
```

3. License check as the first statement of every `IExternalCommand.Execute`:

```csharp
Snapshot gate = Gate.Validate(ProductSlug); // never throws: no try/catch, no wrapper
if (!gate.IsLicensed)
{
    // Show gate.GetMessage(language) as the whole dialog content,
    // offer Gate.OpenConnector(), return Result.Cancelled.
}
```

Rules:

- Decide only on `IsLicensed`. `gate.Reason` (`GateReason`) names the case for logs or UI tweaks; never parse the message text.
- Show the message as-is, in the user's language (`MessageLanguage.Portuguese` or `English`; the template follows Revit's UI language). It already names the one reason and the next step, so never add a list of possible causes around it.
- Never show or log `gate.LicenseKey`: it is the end user's secret.
- Never copy `RevitAPI*.dll`, `RevitAPIUI.dll`, `AdWindows.dll`, or `UIFramework*` to the output payload; they are provided by Revit at runtime.
- Never create a plugin-owned ribbon tab; always use the shared `Node.aec` tab with a plugin-owned panel.

## Parameters

| Flag | Default | Meaning |
|---|---|---|
| `-n` | `MyRevitPlugin` | Plugin, assembly, namespace, and manifest name. Also becomes the ribbon panel name — change `PanelName` in `App.cs` to use a display name with spaces. |
| `--ProductSlug` | `my-revit-plugin` | Product slug in the Node.aec catalog. Must match the entitlement claim checked by `Gate.Validate`. |
| `--AddInId` | Fresh GUID per run | Unique manifest identifier (`<AddInId>` in the `.addin` file). Generate one GUID per plugin; never reuse another add-in's value. |
| `--AppId` | Fresh GUID per run | Installer identity passed to `release.ps1` / `installer.iss`. One value per product, shared by all Revit-year groups, producing a single entry under Windows Settings > Apps. |

## Updating Lite

The Node.aec Connector loads its own copy of `NodeAec.Licensing.Lite` at Revit startup, and on
Revit 2025+ only one version loads per session. Build against the Lite version the current
Connector ships, or an older one: a plugin built against a newer Lite than the installed
Connector fails on Revit 2025+. Lite only adds members, so older versions keep working.

## Build matrix

`RevitYear` selects both the Revit API package version and the target framework:

| `RevitYear` | Target framework | Notes |
|---|---|---|
| `2023`, `2024` | `net48` | Compatibility group `2023-2024`, built with year `2023`. |
| `2025`, `2026` | `net8.0-windows` | Compatibility group `2025-2026`, built with year `2025`. `2026` targets `net8.0-windows` so one binary loads on Revit 2026.0–2026.4 and 2026.5. Default `RevitYear` is `2026`. |
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
powershell -ExecutionPolicy Bypass -File scripts/release.ps1 -Version 0.1.0 -RevitYear 2025-2026
```

`release.ps1` accepts one compatibility group per invocation
(`2023-2024` / `2025-2026` / `2027`); a single year (`2023`–`2027`) is accepted
as an alias and resolves to its group. With `-Install`, the staged payload is
also copied to every installed Revit year of the group, each with its own
absolute `<Assembly>` path in the manifest.

## Manual verification

1. Licensed machine: run the command, expect the greeting dialog with the
   license details (no license key) and `Result.Succeeded`.
2. No lease, wrong slug, or expired entitlement: expect the blocked dialog
   showing only the Node.aec message, in Revit's UI language, and `Result.Cancelled`.
3. Reload the add-in twice: expect a single `Node.aec` tab (no duplicates).

## Repository layout

```
revit-templates/
├── .template.config/template.json   # shortName nodeaec-revit-plugin
├── content/__PluginName__/          # packed flat: its files become <YourName>/ (-n)
│   ├── App.cs
│   ├── __PluginName__.csproj
│   ├── __PluginName__.addin
│   ├── Commands/HelloCommand.cs
│   └── scripts/release.ps1 + installer.iss + installer-after*.txt
└── README.md
```
