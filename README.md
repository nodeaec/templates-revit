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

Each run generates fresh GUIDs for the `.addin` `<AddInId>`, the installer `AppId`, and the bundle's `ProductCode` / `UpgradeCode`.

## Generated files

| File | Role |
|---|---|
| `App.cs` | `IExternalApplication`: creates or reuses the shared `Node.aec` ribbon tab, adds the plugin panel (`PanelName`), inserts the command button idempotently, registers AdWindows deduplication hooks, and loads optional PNG icons with `BitmapCacheOption.OnLoad` + `Freeze()`. Contains no licensing calls. |
| `Commands/HelloCommand.cs` | Reference `IExternalCommand` and the whole licensing integration: holds `ProductSlug`, calls `Gate.Validate(ProductSlug)` as the first statement, and when not licensed shows the gate's message in Revit's UI language with an `Open Node.aec Connector...` link and returns `Result.Cancelled`. Otherwise it shows the greeting with the license details (never the license key) and returns `Result.Succeeded`. |
| `MyPlugin.addin` | Add-in manifest. `<Assembly>` is relative to the manifest (`MyPlugin.dll`): packaging copies it next to the DLL in each `Contents\<group>\` folder of the bundle. `<AddInId>` is a fresh GUID (or `--AddInId`). |
| `MyPlugin.csproj` | Multi-targeting project driven by the `RevitYear` property (2023–2027). Declares `PackageReference` entries for the Revit API (reference-only, `PrivateAssets="all"`, `ExcludeAssets="runtime"`) and `NodeAec.Licensing.Lite`. |
| `scripts/release.ps1` | Release pipeline: builds the three compatibility groups (floor years 2023 / 2025 / 2027) into one Autodesk bundle (`MyPlugin.bundle`: `PackageContents.xml` + `Contents\<group>\`) and zips it with a `.sha256` checksum. `-Version` is required and must match `<Version>` in the csproj. With Inno Setup 6 installed, also compiles `scripts/installer.iss` into `MyPlugin-<version>-Setup.exe`. `-Install` copies the bundle to `%ProgramData%\Autodesk\ApplicationPlugins\`. |
| `scripts/installer.iss` | Inno Setup definition consumed by `release.ps1`. One Setup for Revit 2023–2027: the user checks the Revit versions (installed ones start checked; a re-run starts from the previous choice; silent: `/RevitYears=2025,2026`). Installs the bundle under `%ProgramData%\Autodesk\ApplicationPlugins\` with only the checked groups' builds, and removes installs made by the per-group installers of template 0.2.0 and older. |
| `AGENTS.md` | Brief for coding agents working in the generated project: the project map, the rules that must never break, common tasks, and where to check versions that go stale. |
| `.agents/skills/licensing-integrate/` | The Node.aec licensing skill (copy of the [canonical one in the Sample Plugin](https://github.com/nodeaec/revit-sample-plugin/tree/main/.agents/skills/licensing-integrate)): detailed steps, rules and the manual Revit checks. |

## After scaffolding

1. **Check the product slug.** `ProductSlug` in `Commands/HelloCommand.cs` must be the slug
   registered on Node.aec (`--ProductSlug` set it). A typo blocks every user with "product
   not found".
2. **Name your panel.** `PanelName` in `App.cs` is the project name; set the name users
   should see inside the `Node.aec` tab.
3. **Add your commands.** Copy `HelloCommand`, keep its first lines (the license check) and
   replace the rest with your feature. Add a button for it in `App.cs`
   (`AddButtonIfMissing`, with a unique internal name). With several commands, move
   `ProductSlug` to one shared class.
4. **Build** each group of Revit years: `dotnet build -p:RevitYear=2023`, `2025`, `2027`.
5. **Test in Revit.** `scripts/release.ps1 -Version 0.1.0 -Install` (Revit closed), open
   Revit, click your button: without a license you see the blocked dialog, after signing in
   with the Node.aec Connector the command runs. The full list of checks is in
   `.agents/skills/licensing-integrate/references/validation.md`.
6. **Release.** Raise `<Version>` in the csproj, run `scripts/release.ps1 -Version <it>`, and
   ship `release/MyPlugin-<version>-Setup.exe`. Tell your users they also need the
   [Node.aec Connector](https://nodeaec.com.br/products/nodeaec-connector).

The source comments explain why each piece is the way it is. The
[Sample Plugin](https://github.com/nodeaec/revit-sample-plugin) is this template's output with
demo content and a longer walkthrough.

### With a coding agent

The generated project carries `AGENTS.md` and the skill `.agents/skills/licensing-integrate/`.
Agents that read `AGENTS.md` pick them up on their own; the prompt below also points to them.
Paste it into your agent:

```text
Create a new Autodesk Revit plugin licensed through Node.aec.

Follow the skill at https://github.com/nodeaec/revit-sample-plugin/tree/main/.agents/skills/licensing-integrate
(SKILL.md, path A). After scaffolding, follow the project's AGENTS.md and its copy of the skill.

Before writing code, ask me the questions in the skill's section 2 (product slug, Revit
years, panel name, which commands are paid, test machine) and wait for my answers.

Then: install the template (dotnet new install NodeAec.Templates.RevitPlugin), scaffold with
dotnet new nodeaec-revit-plugin, add my commands with the gate as their first statement,
build every compatibility group with 0 errors, and package with scripts/release.ps1.

Finish with: the files you created or changed, and the manual Revit checks from the
skill's references/validation.md that I still have to run.
```

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
| `--AppId` | Fresh GUID per run | Installer identity in `installer.iss`: one value per product, producing a single entry under Windows Settings > Apps. When regenerating an existing product, pass its current `AppId` so the new Setup takes over the old entry. |

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

One run builds every compatibility group and packages one Autodesk bundle:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/release.ps1 -Version 0.1.0
powershell -ExecutionPolicy Bypass -File scripts/release.ps1 -Version 0.1.0 -Install   # also install it here (close Revit first)
```

```
MyPlugin.bundle\
  PackageContents.xml        one component per Revit year (2023-2027)
  Contents\2023-2024\        net48 build + MyPlugin.addin
  Contents\2025-2026\        net8.0-windows build + MyPlugin.addin
  Contents\2027\             net10.0-windows build + MyPlugin.addin
```

Artifacts in `release\`:

- `MyPlugin-<version>-Setup.exe`: the installer for end users (needs Inno Setup 6). One checkbox per Revit year; only the checked years load the plugin.
- `MyPlugin-<version>.bundle.zip`: the same bundle for IT deployment; extract it into `%ProgramData%\Autodesk\ApplicationPlugins\`.

Revit reads `%ProgramData%\Autodesk\ApplicationPlugins\*.bundle\PackageContents.xml` at
startup and loads only the component matching its version. The `ProductCode` and
`UpgradeCode` in `release.ps1` and `installer.iss` identify the bundle: keep them
identical in both files and never change them between versions.

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
│   ├── AGENTS.md
│   ├── .agents/skills/licensing-integrate/   # copy of the Sample Plugin's skill
│   └── scripts/release.ps1 + installer.iss + installer-after*.txt
├── AGENTS.md                        # for agents maintaining this template
└── README.md
```
