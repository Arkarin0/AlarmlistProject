# Localization and culture outputs

Resolve case-sensitive resource keys from saved RESX strings and build one alarm
list for each configured culture.

## When to use it

Use localization for operator text shared between alarms or translated into
multiple languages.

## Requirements and availability

An SDK project includes RESX files by default. Use string entries only; resource
objects, file references and DTDs are rejected. The matching extension supports
resource completion and translated previews.

## How it works

`$(loc:Key)` selects the requested culture, then its parents, stopping before
invariant resources. `de-DE` may use `de`; it does not fall back to English or an
unqualified catalogue. Empty strings continue parent lookup; whitespace is a value.
Keys are project-wide and case-sensitive within each culture. Duplicate keys fail.
Keyword mode retains localization tokens. Resources still undergo validation.

Translations may use parameters evaluated for the consuming alarm. Nested
localization is unsupported; inserted parameter text is not rescanned.
Builds and live analysis use saved resources. Uncommitted cells or unsaved XML
resource buffers do not supply translations.

## Configuration or syntax

Set `AlarmlistCultures` to a semicolon-separated output list. `AlarmlistCulture`
selects the summary language independently. A resource filename's last dotted
suffix supplies the culture, unless explicit `AlarmlistResource` Culture metadata
overrides it. Keys use letters/underscore first, then letters, digits, `_`, `.` or `-`.

## Minimal example

In a project with `AlarmlistCultures` set to `keyword;en`, use
`<Name>$(loc:Ready)</Name>` inside an alarm. Save `Translations.en.resx`:

```xml
<root>
  <data name="Ready" xml:space="preserve"><value>Ready</value></data>
</root>
```

This minimal string-only file is accepted by the compiler. Use the Visual Studio
Translations (RESX) item template when creating files for the resource editor; it
also supplies the standard RESX headers.

### Expected result

The English build contains Name `Ready`; the keyword build contains
`$(loc:Ready)`. Outputs are in separate `en` and `keyword` directories.

## Troubleshooting

| Symptom | Cause | Resolution |
| --- | --- | --- |
| Missing translation | No nonempty value in requested culture/parents | Add the exact key in a suitable saved culture file. |
| Preview differs from build | Summary and build cultures are independent | Compare Summary language with AlarmlistCultures. |
| Duplicate key | Multiple included resources define it in one culture | Remove the duplicate or correct resource membership. |

## Related features and tutorials

See [translate alarms](../tutorials/translate-alarms.md),
[XLIFF exchange](xliff-translation-exchange.md), and [lookups](lookups-and-instruction-templates.md).
