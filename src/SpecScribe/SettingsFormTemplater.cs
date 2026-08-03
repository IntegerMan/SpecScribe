using System.Text;

namespace SpecScribe;

/// <summary>Renders the VS Code settings form — the whole self-contained webview document, produced by the CORE
/// so the shim renders nothing (ADR 0005 §1 upheld; ADR 0037 Decision 3).
///
/// <para><b>⚠️ There is no <c>&lt;form&gt;</c> element, and that is a correctness requirement rather than a style
/// choice.</b> The webview CSP (<see cref="WebviewRenderAdapter.CspPolicy"/>) carries <c>form-action 'none'</c>, so
/// a submit is blocked outright, and <c>script-src 'nonce-…'</c> blocks an inline <c>onsubmit</c>. The controls are
/// plain <c>&lt;input&gt;</c>/<c>&lt;select&gt;</c> plus a <c>&lt;button type="button"&gt;</c>, and the nonce'd
/// bridge at the foot reads them and posts to the host.</para>
///
/// <para><b>Why every control offers "Inherit default".</b> The saved document persists only what the user
/// explicitly chose (<see cref="SettingsStore.Capture"/>'s persist-only-when-set rule), so "unset" is a real,
/// reachable state and not the same as "set to the default value". Choosing it posts a <c>--clear</c> for that
/// field. This is also why the two booleans are three-option selects and not checkboxes: a checkbox can only say
/// true or false, and would write <c>deepGit: false</c>, which the core deliberately never writes.</para>
///
/// <para><b>Provenance is shown, not just the value.</b> Each row carries where its effective value came from —
/// the settings file, auto-discovery, or the built-in default — which is <c>--show-config</c>'s answer to "why is
/// it building into <em>that</em> folder?" finally rendered somewhere a user will look.</para></summary>
public static class SettingsFormTemplater
{
    /// <summary>One row of the form: what to label it, which <see cref="SettingsResolver.Fields"/> key it posts
    /// back under, and how it is edited.</summary>
    private sealed record Row(string Field, string Label, string Hint, RowKind Kind);

    private enum RowKind
    {
        /// <summary>A free-text value.</summary>
        Text,

        /// <summary>A directory: free text plus a Browse button that opens the host's folder picker.</summary>
        Directory,

        /// <summary>A tri-state boolean whose OFF state cannot currently be pinned — see the DeepGit note in
        /// <see cref="RenderBody"/>.</summary>
        OnOrInherit,

        /// <summary>A tri-state boolean where all three states are honoured.</summary>
        TriBool,

        /// <summary>The date-cutoff policy token.</summary>
        DatePolicy,
    }

    /// <summary>The form's rows, in the order they render. Exactly the eight fields
    /// <see cref="SavedSettings"/> persists — the form cannot offer a setting the core cannot store.</summary>
    private static readonly Row[] Rows =
    [
        new(SettingsResolver.Fields.Project, "Project name",
            "How the portal is branded. Defaults to project_name from _bmad/config.toml.", RowKind.Text),
        new(SettingsResolver.Fields.Source, "Source artifacts directory",
            "Where the spec artifacts live. Defaults to the nearest _bmad-output above this folder.", RowKind.Directory),
        new(SettingsResolver.Fields.Adrs, "ADR directory",
            "Hand-authored architecture decision records. Defaults to docs/adrs.", RowKind.Directory),
        new(SettingsResolver.Fields.Output, "Output directory",
            "Where the generated site is written. Defaults to SpecScribeOutput.", RowKind.Directory),
        new(SettingsResolver.Fields.DeepGit, "Deep git analytics",
            "Change coupling and hotspots, as an opt-in dashboard panel. Costs time on large histories.", RowKind.OnOrInherit),
        new(SettingsResolver.Fields.Readme, "Include the repository README",
            "Renders README.md as a page in the portal.", RowKind.TriBool),
        new(SettingsResolver.Fields.CodeUrl, "Source hosting base URL",
            "Makes code citations link out to your host. Blank keeps them in-portal and auto-detects.", RowKind.Text),
        new(SettingsResolver.Fields.TodayPolicy, "\"Today\" for date pages",
            "What the portal treats as today when rendering relative dates.", RowKind.DatePolicy),
    ];

    /// <summary>The whole document: shell, form, and the nonce'd bridge. <c>__CSP_SOURCE__</c>/<c>__NONCE__</c> are
    /// left unsubstituted — the same two-value seam the portal document uses, so the shim's job is identical for
    /// both surfaces and it still holds no knowledge of either one's content.</summary>
    public static string RenderDocument(ResolvedConfig resolved, SettingsLoad load)
    {
        var settingsPath = load.Path is { } p
            ? PathUtil.NormalizeSlashes(Path.Combine(p, SettingsStore.ConfigFileName))
            : null;

        return DocumentTemplate
            .Replace("__CSP__", WebviewRenderAdapter.CspPolicy)
            .Replace("__CSS__", WebviewRenderAdapter.StylesheetCss)
            .Replace("__THEME_CSS__", WebviewRenderAdapter.ThemeBridgeCss)
            .Replace("__SETTINGS_PATH_LABEL__", PathUtil.Html(settingsPath ?? "not created yet"))
            .Replace("__HAS_FILE__", settingsPath is null ? "false" : "true")
            .Replace("__BODY__", RenderBody(resolved, load));
    }

    /// <summary>The form rows. Values and provenance come from the SAME <see cref="ResolvedConfig"/> the CLI
    /// reports, so the form and <c>--show-config</c> can never disagree about what is in effect.</summary>
    private static string RenderBody(ResolvedConfig resolved, SettingsLoad load)
    {
        var byField = resolved.Provenance.ToDictionary(e => e.Field, StringComparer.Ordinal);
        var sb = new StringBuilder();

        foreach (var row in Rows)
        {
            if (!byField.TryGetValue(row.Field, out var entry)) continue;

            var savedExplicitly = IsSavedExplicitly(row.Field, load.Saved);
            sb.Append($"<div class=\"ss-form-row\" data-field=\"{PathUtil.Html(row.Field)}\">\n");
            sb.Append($"  <label class=\"ss-form-label\" for=\"ss-f-{PathUtil.Html(row.Field)}\">{PathUtil.Html(row.Label)}</label>\n");
            sb.Append($"  <p class=\"ss-form-hint\">{PathUtil.Html(row.Hint)}</p>\n");
            sb.Append("  <div class=\"ss-form-control\">\n");
            AppendControl(sb, row, entry, savedExplicitly);
            sb.Append("  </div>\n");
            // The provenance tag. Never color-alone — it is a WORD (UX-DR17), and it is the reason this form is
            // more useful than editing the JSON: the JSON cannot tell you that a path you are looking at was
            // auto-discovered rather than chosen.
            sb.Append($"  <p class=\"ss-form-prov\">In effect: <code>{PathUtil.Html(entry.EffectiveValue)}</code> ")
              .Append($"<span class=\"ss-form-prov-tag\">({PathUtil.Html(ProvenanceWord(entry, savedExplicitly))})</span></p>\n");
            sb.Append($"  <p class=\"ss-form-error\" id=\"ss-e-{PathUtil.Html(row.Field)}\" role=\"alert\" hidden></p>\n");
            sb.Append("</div>\n");
        }

        return sb.ToString();
    }

    private static void AppendControl(StringBuilder sb, Row row, ConfigProvenance entry, bool savedExplicitly)
    {
        var id = $"ss-f-{PathUtil.Html(row.Field)}";
        // A field the user has NOT explicitly saved shows blank/inherit rather than pre-filling the resolved value.
        // Pre-filling would be the CodeUrl trap in general form: pressing Save would silently promote an
        // auto-discovered value into an explicit, frozen one. The resolved value is shown in the provenance line
        // instead, where it informs without being submitted.
        var current = savedExplicitly ? entry.EffectiveValue : string.Empty;

        switch (row.Kind)
        {
            case RowKind.Directory:
                sb.Append($"    <input class=\"ss-form-input\" type=\"text\" id=\"{id}\" value=\"{PathUtil.Html(current)}\" placeholder=\"Inherit default\" />\n");
                sb.Append($"    <button type=\"button\" class=\"ss-form-browse\" data-browse=\"{PathUtil.Html(row.Field)}\">Browse&hellip;</button>\n");
                break;

            case RowKind.Text:
                sb.Append($"    <input class=\"ss-form-input\" type=\"text\" id=\"{id}\" value=\"{PathUtil.Html(current)}\" placeholder=\"Inherit default\" />\n");
                break;

            case RowKind.OnOrInherit:
                // ⚠️ Two options, not three, and the missing one is deliberate. SettingsStore.ApplyTo reads
                // `saved.DeepGit == true` only — there is no --no-deep-git for a persisted `false` to suppress — so
                // a pinned "Off" is a choice the core cannot honour. Offering it would show the user a control that
                // silently does nothing. Recorded as an open item in ADR 0037 rather than worked around here.
                sb.Append($"    <select class=\"ss-form-select\" id=\"{id}\">\n");
                AppendOption(sb, "", $"Inherit default ({entry.EffectiveValue})", !savedExplicitly);
                AppendOption(sb, "true", "On", savedExplicitly);
                sb.Append("    </select>\n");
                break;

            case RowKind.TriBool:
                sb.Append($"    <select class=\"ss-form-select\" id=\"{id}\">\n");
                AppendOption(sb, "", $"Inherit default ({entry.EffectiveValue})", !savedExplicitly);
                AppendOption(sb, "true", "Yes", savedExplicitly && entry.EffectiveValue == "true");
                AppendOption(sb, "false", "No", savedExplicitly && entry.EffectiveValue == "false");
                sb.Append("    </select>\n");
                break;

            case RowKind.DatePolicy:
                sb.Append($"    <select class=\"ss-form-select\" id=\"{id}\">\n");
                AppendOption(sb, "", $"Inherit default ({entry.EffectiveValue})", !savedExplicitly);
                foreach (var policy in Enum.GetValues<DatePolicy>())
                {
                    var cutoff = new DateCutoff(policy, null);
                    // ⚠️ The AsOf option carries the bare PREFIX, not `Token(cutoff)`. `Token` degrades a DATELESS
                    // AsOf to the machine-local token by design (it must never claim a pin the run did not use) —
                    // so asking it for an option value here produced a "fixed date" option whose value was
                    // `machine-local`: a duplicate of another option, and one the bridge's `as-of` test could never
                    // match, so the companion date input never appeared. Found in a live browser; the option looked
                    // right and did nothing. The date input supplies the day and the bridge composes `as-of:{iso}`.
                    var token = policy == DatePolicy.AsOf ? DatePolicies.AsOfTokenPrefix : DatePolicies.Token(cutoff);
                    var selected = savedExplicitly && entry.EffectiveValue.StartsWith(token, StringComparison.Ordinal);
                    AppendOption(sb, token, DatePolicies.Label(cutoff), selected);
                }
                sb.Append("    </select>\n");
                sb.Append($"    <input class=\"ss-form-date\" type=\"date\" id=\"{id}-date\" value=\"{PathUtil.Html(PinnedDate(entry))}\" aria-label=\"Pinned date\" hidden />\n");
                break;
        }
    }

    private static void AppendOption(StringBuilder sb, string value, string label, bool selected)
        => sb.Append($"      <option value=\"{PathUtil.Html(value)}\"{(selected ? " selected" : string.Empty)}>{PathUtil.Html(label)}</option>\n");

    /// <summary>The ISO date inside an <c>as-of:{iso}</c> token, or empty for every other policy.</summary>
    private static string PinnedDate(ConfigProvenance entry)
        => DatePolicies.TryParse(entry.EffectiveValue, out var cutoff) && cutoff is { Policy: DatePolicy.AsOf, AsOf: { } day }
            ? PortalDates.IsoDay(day)
            : string.Empty;

    /// <summary>The human word for where a value came from. Distinguishes "saved" from "in effect": a field can be
    /// in effect from the command line on a run that also has it saved, and the form is about the SAVED state.</summary>
    private static string ProvenanceWord(ConfigProvenance entry, bool savedExplicitly) => savedExplicitly
        ? $"from {SettingsStore.FileName}"
        : entry.Source == ConfigSource.CommandLine
            ? $"from {entry.Option} on this run — not saved"
            : "auto-detected or default — not saved";

    private static bool IsSavedExplicitly(string field, SavedSettings? saved) => saved is not null && field switch
    {
        SettingsResolver.Fields.Source => saved.Source is not null,
        SettingsResolver.Fields.Adrs => saved.Adrs is not null,
        SettingsResolver.Fields.Output => saved.Output is not null,
        SettingsResolver.Fields.Project => saved.ProjectName is not null,
        SettingsResolver.Fields.Readme => saved.IncludeReadme is not null,
        SettingsResolver.Fields.DeepGit => saved.DeepGit is not null,
        SettingsResolver.Fields.CodeUrl => saved.CodeUrl is not null,
        SettingsResolver.Fields.TodayPolicy => saved.TodayPolicy is not null,
        _ => false,
    };

    // The settings document's shell. Deliberately its own template rather than WebviewRenderAdapter's: that one
    // carries the portal toolbar, the surface container and the navigation bridge, none of which mean anything
    // here. What the two SHARE is what must never diverge — the CSP policy string and both stylesheets, all three
    // substituted from WebviewRenderAdapter rather than copied.
    private const string DocumentTemplate = """
        <!DOCTYPE html>
        <html lang="en">
        <head>
        <meta charset="UTF-8" />
        <meta http-equiv="Content-Security-Policy" content="__CSP__" />
        <meta name="viewport" content="width=device-width, initial-scale=1.0" />
        <title>SpecScribe Settings</title>
        <style>__CSS__</style>
        <style>__THEME_CSS__</style>
        </head>
        <body>
        <div class="ss-webview-toolbar">
        <span class="ss-webview-toolbar-label">SpecScribe Settings</span>
        <button type="button" class="ss-form-reveal" data-has-file="__HAS_FILE__">Edit config.json</button>
        </div>
        <main id="main-content" class="ss-form-main">
        <h1 class="ss-form-title">Project settings</h1>
        <p class="ss-form-lead">These are written to <code>__SETTINGS_PATH_LABEL__</code> and shared by the CLI, watch mode and this editor. Host preferences such as the tool path stay in VS Code's own settings.</p>
        <div class="ss-form-status" id="ss-form-status" role="status" aria-live="polite" hidden></div>
        __BODY__
        <div class="ss-form-actions">
        <button type="button" class="ss-form-save" id="ss-form-save">Save settings</button>
        <button type="button" class="ss-form-cancel" id="ss-form-cancel">Close</button>
        </div>
        </main>
        <script nonce="__NONCE__">
        (function () {
          // There is no HTML form element anywhere above: the CSP carries `form-action 'none'`, so a submit is blocked
          // and an inline onsubmit is blocked by `script-src 'nonce-…'`. This bridge IS the submit path.
          var vscode = (typeof acquireVsCodeApi === 'function') ? acquireVsCodeApi() : null;
          var status = document.getElementById('ss-form-status');

          function rows() { return Array.prototype.slice.call(document.querySelectorAll('.ss-form-row')); }
          function controlIn(row) { return row.querySelector('.ss-form-input, .ss-form-select'); }

          // The pinned-date input is only meaningful for the as-of policy; reveal it on that selection alone so an
          // inert control is never shown. Runs once at load and on every change, so a document rendered with
          // as-of already selected shows its date.
          function syncDate() {
            var row = document.querySelector('.ss-form-row[data-field="today_policy"]');
            if (!row) return;
            var select = row.querySelector('.ss-form-select');
            var date = row.querySelector('.ss-form-date');
            if (!select || !date) return;
            date.hidden = String(select.value).indexOf('as-of') !== 0;
          }
          syncDate();
          document.addEventListener('change', syncDate);

          function collect() {
            var values = {};
            var cleared = [];
            rows().forEach(function (row) {
              var field = row.getAttribute('data-field');
              var control = controlIn(row);
              if (!field || !control) return;
              var value = String(control.value || '').trim();
              // Empty means "inherit the default", which is an UNSET rather than an empty value — the core has no
              // other way to hear it, since an absent option is indistinguishable from one never passed.
              if (value === '') { cleared.push(field); return; }
              if (field === 'today_policy' && value.indexOf('as-of') === 0) {
                var date = row.querySelector('.ss-form-date');
                var iso = date ? String(date.value || '').trim() : '';
                if (!iso) { cleared.push(field); return; }
                value = 'as-of:' + iso;
              }
              values[field] = value;
            });
            return { values: values, cleared: cleared };
          }

          function showStatus(text, level) {
            if (!status) return;
            status.hidden = !text;
            status.textContent = text || '';
            status.setAttribute('data-level', level || 'info');
          }

          function clearErrors() {
            Array.prototype.forEach.call(document.querySelectorAll('.ss-form-error'), function (el) {
              el.hidden = true;
              el.textContent = '';
            });
          }

          document.addEventListener('click', function (ev) {
            var browse = ev.target.closest ? ev.target.closest('.ss-form-browse') : null;
            if (browse && vscode) {
              vscode.postMessage({ type: 'settingsPick', field: browse.getAttribute('data-browse') });
              return;
            }
            if (ev.target.closest && ev.target.closest('.ss-form-reveal') && vscode) {
              vscode.postMessage({ type: 'settingsRevealFile' });
              return;
            }
            if (ev.target.id === 'ss-form-save' && vscode) {
              clearErrors();
              showStatus('Saving…', 'info');
              var payload = collect();
              vscode.postMessage({ type: 'settingsSave', values: payload.values, cleared: payload.cleared });
              return;
            }
            if (ev.target.id === 'ss-form-cancel' && vscode) {
              vscode.postMessage({ type: 'settingsCancel' });
            }
          });

          window.addEventListener('message', function (ev) {
            var msg = ev.data || {};
            if (msg.type === 'settingsPicked') {
              var row = document.querySelector('.ss-form-row[data-field="' + msg.field + '"]');
              var control = row ? controlIn(row) : null;
              if (control) control.value = msg.value || '';
              return;
            }
            if (msg.type !== 'settingsResult') return;
            clearErrors();
            if (msg.ok) {
              showStatus('Settings saved to ' + (msg.savedTo || 'the settings file') + '.', 'info');
              return;
            }
            var errors = Array.isArray(msg.errors) ? msg.errors : [];
            // Attach each message to the FIELD that caused it — the reason the core reports failures as
            // machine-readable JSON lines rather than a human sentence the host would have to screen-scrape.
            var unattached = [];
            errors.forEach(function (err) {
              var el = err.field ? document.getElementById('ss-e-' + err.field) : null;
              if (el) { el.hidden = false; el.textContent = err.message; }
              else unattached.push(err.message);
            });
            showStatus(unattached.length ? unattached.join(' ') : 'Could not save — see the messages below.', 'error');
          });
        }());
        </script>
        </body>
        </html>
        """;
}
