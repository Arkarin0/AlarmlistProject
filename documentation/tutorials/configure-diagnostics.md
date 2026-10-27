# Configure diagnostic policy

Use the default playground's opt-in sample analyzer to see a warning and adjust
its per-file severity without changing mandatory compiler validation.

## Prerequisites and starting files

Prepare the default playground using
[the Visual Studio workflow](edit-and-inspect-alarms.md). Its preparation builds
and registers the matching sample analyzer. Work with `example/North` and keep a
copy of any existing `.editorconfig` changes. Commands run from the repository
root. Do not overwrite unrelated configuration sections.

## Steps

### 1. Locate the configured rule

Inspect `example/.editorconfig` and `example/North/.editorconfig`, and the
SAMPLE001 rule described in the [sample analyzer guide](../../src/Analyzers/Alarmlist.SampleAnalyzer/README.md).
Build North:

```powershell
dotnet msbuild example/North/North.almproj /t:Build
```

Expected result: on unchanged inputs the prepared playground reports intentional
SAMPLE001 warnings for nonmatching Code prefixes, per culture. They do not fail
the default build.

### 2. Promote the rule for North's local ALMX files

At the end of `example/North/.editorconfig`, add this exercise section, or edit
an equivalent final matching section:

```ini
[*.almx]
alarmlist_diagnostic.SAMPLE001.severity = error
```

Save configuration and build North again.

Expected result: a SAMPLE001 diagnostic whose primary source is a matching North
file is now an error and the build fails. Shared physical files use their own
directory's configuration, not North's merely because North consumes them.

### 3. Restore the warning policy

Remove only the exercise section, restoring any preexisting value you changed.
Save and repeat the build.

Expected result: the original warning policy returns and the unchanged sample
build succeeds. Compiler errors remain mandatory regardless of this setting.

## Verify the result

Compare SAMPLE001 severity and its physical source location before/after step 2.
A failed build preserves prior compiled XML rather than installing partial new
outputs. The final configuration should match your starting state.

## Troubleshooting

| Symptom | Cause | Resolution |
| --- | --- | --- |
| SAMPLE001 absent | Analyzer not prepared or sample changed | Rerun matching preparation and inspect the current Code/prefix configuration. |
| Shared warning unchanged | Physical source is outside North | Configure its owning directory deliberately if needed. |
| Setting has no effect | Configuration not saved or later section wins | Save and inspect matching sections in precedence order. |

## Next steps and related features

See [diagnostics and analyzers](../features/diagnostics-and-analyzers.md) for
suppression, promotion and runtime compatibility, and [project builds](../features/project-builds.md).
