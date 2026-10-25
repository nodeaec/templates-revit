# AGENTS.md — maintaining the `nodeaec-revit-plugin` template

Instructions for coding agents working in **this repository** (the template package
`NodeAec.Templates.RevitPlugin`). Agents working in a project *generated* by the template
follow `content/__PluginName__/AGENTS.md` instead, which ships inside every scaffold.

## What lives where

| Path | Role |
|---|---|
| `content/__PluginName__/` | The files every scaffold gets. `__PluginName__` and the other `__Token__` values are replaced by `dotnet new` (see `.template.config/template.json`). |
| `.template.config/template.json` | Template parameters and generated GUIDs (`AddInId`, `AppId`, `ProductCode`, `UpgradeCode`). |
| `NodeAec.Templates.RevitPlugin.csproj` | Packs `content/` into the NuGet package; holds the template `<Version>`. |
| `README.md` | What users read on GitHub and NuGet. |

## The template mirrors the Sample Plugin

The [Sample Plugin](https://github.com/nodeaec/revit-sample-plugin) is this template's output
plus demo content. Keep them in step:

| Template file | Same as, in revit-sample-plugin |
|---|---|
| `App.cs`, `Commands/HelloCommand.cs` | `src/SamplePlugin/App.cs`, `Commands/HelloCommand.cs` (names replaced by tokens; the template's `App.cs` also loads optional icons) |
| `__PluginName__.csproj`, `__PluginName__.addin` | `src/SamplePlugin/SamplePlugin.csproj`, `SamplePlugin.addin` |
| `scripts/*` | `scripts/*` (identity values replaced by tokens) |
| `.agents/skills/licensing-integrate/` | `.agents/skills/licensing-integrate/`, **byte for byte**: copy the folder over, never edit it here first |

When one side changes, the other changes in its own repo; say so to the user if you can
change only one.

## Facts that go stale, and where to check them

| Fact | Source of truth | Also written in |
|---|---|---|
| Template version | `<Version>` in `NodeAec.Templates.RevitPlugin.csproj` | `README.md` mentions, the Sample Plugin's README status note and skill |
| Latest published template | <https://www.nuget.org/packages/NodeAec.Templates.RevitPlugin> | |
| Lite version scaffolds reference | `PackageReference` in `content/__PluginName__/__PluginName__.csproj`; never newer than the Connector's ([revit-connector `src/NodeAec.Connector/NodeAec.Connector.csproj`](https://github.com/nodeaec/revit-connector/blob/main/src/NodeAec.Connector/NodeAec.Connector.csproj)) | `README.md` (licensing section) |
| Latest Lite | <https://www.nuget.org/packages/NodeAec.Licensing.Lite> | |
| Revit API package pins, year → TFM | `RevitApiPkgVersion` / `TargetFramework` in the content csproj; same values as the Sample Plugin's csproj | `README.md` (RevitYear table) |

After changing a value, find every place that repeats it: `grep -rn "<old value>" .`

## Verify before committing

```powershell
dotnet pack NodeAec.Templates.RevitPlugin.csproj -c Release -o <tmp>\pkg
dotnet new install <tmp>\pkg\NodeAec.Templates.RevitPlugin.<version>.nupkg --debug:custom-hive <tmp>\hive
cd <tmp>; dotnet new nodeaec-revit-plugin -n TplCheck --ProductSlug tpl-check --debug:custom-hive <tmp>\hive
cd TplCheck; powershell -ExecutionPolicy Bypass -File scripts\release.ps1 -Version 0.1.0
```

The three group builds must end with 0 warnings and 0 errors, and no `__Token__` may remain
in the generated files. Publishing to nuget.org is done by the maintainer, never by an agent.
