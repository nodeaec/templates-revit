using System;
using System.IO;
using System.Linq;
using System.Windows.Media.Imaging;
using Autodesk.Revit.UI;
using Autodesk.Windows;
using __PluginName__.Commands;
using RevitRibbonPanel = Autodesk.Revit.UI.RibbonPanel;

namespace __PluginName__;

/// <summary>
/// __PluginName__ entry point (<see cref="IExternalApplication"/>).
/// <para>
/// Ribbon rules (same conventions as the Node.aec Connector):
/// the button lives in the SHARED canonical <c>Node.aec</c> tab — this add-in never
/// owns a tab of its own — inside its own <c>__PluginName__</c> panel with a single
/// <c>Hello World</c> push button. Tab creation tolerates only the "already exists"
/// ArgumentException, panel/button insertion is idempotent, and AdWindows lifecycle
/// hooks keep the tab deduplicated across add-in reloads.
/// </para>
/// <para>
/// Deliberately contains no Node.aec licensing API calls: the Ribbon must come up
/// even when the license cannot be verified, so the only licensing surface of this
/// plugin lives in <see cref="Licensing.NodeAecLicenseGate"/>.
/// </para>
/// </summary>
public class App : IExternalApplication
{
    /// <summary>Shared canonical tab. Owned by the Node.aec ecosystem — never re-created as a plugin tab.</summary>
    public const string TabName = "Node.aec";

    /// <summary>This plugin's panel inside the shared tab.</summary>
    public const string PanelName = "__PluginName__";

    /// <summary>Internal (non-localized) id of the push button; used for idempotent insertion.</summary>
    public const string ButtonId = "__PluginName___HelloWorld";

    /// <summary>Display text of the push button.</summary>
    public const string ButtonText = "Hello World";

    /// <summary>
    /// Builds the Ribbon (tab → panel → button) and registers the dedup hooks.
    /// Any exception becomes a clean <see cref="Result.Failed"/> instead of a
    /// half-built Ribbon with no cause recorded.
    /// </summary>
    /// <param name="application">Revit-provided handle to the Ribbon of this session.</param>
    /// <returns><see cref="Result.Succeeded"/> when the Ribbon is ready, otherwise <see cref="Result.Failed"/>.</returns>
    public Result OnStartup(UIControlledApplication application)
    {
        try
        {
            EnsureTab(application);
            DeduplicateTab();

            RevitRibbonPanel panel = GetOrCreatePanel(application, TabName, PanelName);
            AddButtonIfMissing(panel, BuildHelloButtonData());

            RegisterRibbonHooks(application);
            return Result.Succeeded;
        }
        catch (Exception)
        {
            // Revit reports the add-in failure. No licensing calls here on purpose:
            // this class must not depend on NodeAec.Licensing at JIT time.
            return Result.Failed;
        }
    }

    /// <summary>
    /// Clean shutdown: unhooks the AdWindows/lifecycle delegates so a reload of the
    /// add-in in the same process never stacks handlers (the hooks are detached with
    /// "-=" before being attached with "+=" for the same reason).
    /// </summary>
    /// <param name="application">Revit-provided handle to the Ribbon of this session.</param>
    /// <returns>Always <see cref="Result.Succeeded"/>; shutdown has nothing that can fail.</returns>
    public Result OnShutdown(UIControlledApplication application)
    {
        try
        {
            application.ControlledApplication.ApplicationInitialized -= OnApplicationInitialized;
            ComponentManager.UIElementActivated -= OnUiElementActivated;
        }
        catch (Exception)
        {
            // Best-effort detachment; the delegates are static and idempotent anyway.
        }

        return Result.Succeeded;
    }

    /// <summary>
    /// Creates the shared <c>Node.aec</c> tab when it is not there yet (the connector
    /// usually creates it first). Only the "name already in use / rejected" exception
    /// is treated as the expected add-in-reload state; every other failure propagates
    /// to the top guard of <see cref="OnStartup"/> — a bare catch would mask real errors.
    /// </summary>
    private static void EnsureTab(UIControlledApplication application)
    {
        try
        {
            application.CreateRibbonTab(TabName);
        }
        catch (Exception ex) when (
            ex is Autodesk.Revit.Exceptions.ArgumentException || ex is ArgumentException)
        {
            // Expected: the tab already exists (connector or another plugin created it).
        }
    }

    /// <summary>
    /// Creates or reuses the <see cref="PanelName"/> panel inside the shared tab —
    /// the idempotent GetOrCreatePanel pattern.
    /// </summary>
    private static RevitRibbonPanel GetOrCreatePanel(UIControlledApplication application, string tabName, string panelName)
    {
        try
        {
            var existing = application.GetRibbonPanels(tabName);
            var found = existing.FirstOrDefault(p => string.Equals(p.Name, panelName, StringComparison.OrdinalIgnoreCase));
            if (found != null)
            {
                return found;
            }
        }
        catch (Exception)
        {
            // Lookup failure falls through to CreateRibbonPanel, which reports real problems.
        }

        return application.CreateRibbonPanel(tabName, panelName);
    }

    /// <summary>Creates the <c>Hello World</c> push button descriptor bound to <see cref="HelloCommand"/>.</summary>
    private static PushButtonData BuildHelloButtonData()
    {
        var data = new PushButtonData(
            ButtonId,
            ButtonText,
            typeof(App).Assembly.Location,
            typeof(HelloCommand).FullName ?? string.Empty)
        {
            ToolTip = "__PluginName__: greets the user and shows the Node.aec license backing this command."
        };

        TrySetIcons(data);
        return data;
    }

    /// <summary>
    /// Optional 16/32px button icons (<c>Resources/Icon16.png</c>, <c>Resources/Icon32.png</c>
    /// next to the plugin DLL, marked as <c>Resource</c> or copied to output). Missing files
    /// are a silent no-op: the button stays text-only and the add-in still loads.
    /// </summary>
    private static void TrySetIcons(PushButtonData data)
    {
        try
        {
            string dir = Path.GetDirectoryName(typeof(App).Assembly.Location) ?? string.Empty;
            string icon16 = Path.Combine(dir, "Resources", "Icon16.png");
            string icon32 = Path.Combine(dir, "Resources", "Icon32.png");

            if (File.Exists(icon16))
            {
                data.Image = LoadBitmap(icon16);
            }

            if (File.Exists(icon32))
            {
                data.LargeImage = LoadBitmap(icon32);
            }
        }
        catch (Exception)
        {
            // Icons are decoration; a missing/unreadable PNG must never break the Ribbon.
        }
    }

    /// <summary>
    /// Loads a PNG for Ribbon use. <see cref="BitmapCacheOption.OnLoad"/> reads the bytes
    /// immediately so the file is not locked, and <c>Freeze()</c> makes the bitmap safe to
    /// share across Revit's UI threads.
    /// </summary>
    private static BitmapSource LoadBitmap(string path)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.UriSource = new Uri(path, UriKind.Absolute);
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    /// <summary>
    /// Adds the button only when its internal name is not already present — safe on
    /// add-in reloads and when another instance of the panel already holds it.
    /// </summary>
    private static void AddButtonIfMissing(RevitRibbonPanel panel, PushButtonData buttonData)
    {
        try
        {
            var items = panel.GetItems().ToList();
            bool present = items.Any(i => string.Equals(i.Name, buttonData.Name, StringComparison.OrdinalIgnoreCase));
            if (!present)
            {
                panel.AddItem(buttonData);
            }
        }
        catch (Exception)
        {
            // Never let a Ribbon hiccup take the whole add-in down.
        }
    }

    /// <summary>
    /// Registers the AdWindows/Revit lifecycle hooks that keep the shared tab unique.
    /// Named static handlers with "-=" before "+=" so reloads don't stack delegates.
    /// </summary>
    private static void RegisterRibbonHooks(UIControlledApplication application)
    {
        try
        {
            application.ControlledApplication.ApplicationInitialized -= OnApplicationInitialized;
            application.ControlledApplication.ApplicationInitialized += OnApplicationInitialized;

            ComponentManager.UIElementActivated -= OnUiElementActivated;
            ComponentManager.UIElementActivated += OnUiElementActivated;
        }
        catch (Exception)
        {
            // AdWindows unavailable in this host: dedup stays best-effort, Ribbon still loads.
        }
    }

    /// <summary>Re-runs deduplication once the Ribbon is fully initialized.</summary>
    private static void OnApplicationInitialized(object? sender, EventArgs e) => DeduplicateTab();

    /// <summary>Re-runs deduplication on every Ribbon interaction (cheap no-op when there is one tab).</summary>
    private static void OnUiElementActivated(object? sender, EventArgs e) => DeduplicateTab();

    /// <summary>
    /// Removes duplicate <c>Node.aec</c> tabs (add-in reloads, mixed connector/plugin
    /// startup orders): keeps the first match, moves panels that are not present in it,
    /// then hides and removes the duplicate.
    /// </summary>
    private static void DeduplicateTab()
    {
        try
        {
            var ribbon = ComponentManager.Ribbon;
            if (ribbon is null)
            {
                return;
            }

            var matchingTabs = ribbon.Tabs
                .Where(t => string.Equals(t.Title, TabName, StringComparison.OrdinalIgnoreCase)
                         || string.Equals(t.Id, TabName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (matchingTabs.Count <= 1)
            {
                return;
            }

            var primaryTab = matchingTabs[0];
            for (int i = 1; i < matchingTabs.Count; i++)
            {
                var duplicateTab = matchingTabs[i];

                foreach (var panel in duplicateTab.Panels.ToList())
                {
                    duplicateTab.Panels.Remove(panel);

                    bool alreadyInPrimary = primaryTab.Panels.Any(p =>
                        string.Equals(p.Source?.Title, panel.Source?.Title, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(p.Source?.Id, panel.Source?.Id, StringComparison.OrdinalIgnoreCase));

                    if (!alreadyInPrimary)
                    {
                        primaryTab.Panels.Add(panel);
                    }
                }

                duplicateTab.IsVisible = false;
                ribbon.Tabs.Remove(duplicateTab);
            }
        }
        catch (Exception)
        {
            // Deduplication must never throw into Revit's event pipeline.
        }
    }
}
