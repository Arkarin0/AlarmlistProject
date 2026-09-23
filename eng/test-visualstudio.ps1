# Run with Windows PowerShell 5.1 -STA after deploying to the AlarmlistExp instance.
[CmdletBinding()]
param(
  [Parameter(Mandatory = $true)]
  [string]$VisualStudioPath,
  [string]$Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -ne 'Desktop' -or [Threading.Thread]::CurrentThread.ApartmentState -ne 'STA') {
  throw 'Run this script with powershell.exe -STA (Windows PowerShell 5.1).'
}
$repoRoot = Split-Path $PSScriptRoot -Parent
$runDirectory = Join-Path $repoRoot "artifacts/log/$Configuration/VisualStudioSmoke/$([Guid]::NewGuid().ToString('N'))"
$projectDirectory = Join-Path $runDirectory 'Plant'
New-Item -ItemType Directory -Path $projectDirectory -Force | Out-Null
Copy-Item (Join-Path $repoRoot 'src/SDK/Alarmlist.SDK.Tests/testassets/boilerplate/Directory.Build.*') $runDirectory
# This retained consumer lives inside the checkout, whose source mapping otherwise
# routes every package to nuget.org. Give the isolated feed an exact SDK mapping.
@'
<configuration>
  <config><add key="globalPackagesFolder" value=".packages" /></config>
  <packageSources><clear /><add key="local" value="feed" /></packageSources>
  <disabledPackageSources><clear /></disabledPackageSources>
  <packageSourceMapping>
    <packageSource key="local"><package pattern="Alarmlist.MSBuild.SDK" /></packageSource>
  </packageSourceMapping>
</configuration>
'@ | Set-Content (Join-Path $runDirectory 'NuGet.Config')
New-Item -ItemType Directory -Path (Join-Path $runDirectory 'feed') | Out-Null
Copy-Item (Join-Path $repoRoot "artifacts/packages/$Configuration/NonShipping/Alarmlist.MSBuild.SDK.*.nupkg") (Join-Path $runDirectory 'feed')
Write-Output "Smoke-test files and logs: $runDirectory"
# Opening a solution enters the IDE directly. A bare devenv launch may remain on
# the Start window and never publish its DTE automation object.
$solutionPath = Join-Path $runDirectory 'Smoke.sln'
@'
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio Version 17
Global
EndGlobal
'@ | Set-Content $solutionPath

# Bind only to the process started below, never to an existing user IDE session.
$interopAssembly = Join-Path $VisualStudioPath 'Common7/IDE/PublicAssemblies/Microsoft.VisualStudio.Interop.dll'
[Reflection.Assembly]::LoadFrom($interopAssembly) | Out-Null
Add-Type -ReferencedAssemblies $interopAssembly -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
public static class AlarmlistRunningObjectTable
{
    // PowerShell's COM adapter exposes the default interfaces, which omit these
    // newer Solution2/DTE2 members. Query the required interfaces explicitly.
    public static string GetTemplate(object automation, bool item)
    {
        var solution = (EnvDTE80.Solution2)((EnvDTE.DTE)automation).Solution;
        string path = item ? solution.GetProjectItemTemplate("AlarmList.vstemplate", "Alarmlist")
                           : solution.GetProjectTemplate("Alarmlist.vstemplate", "Alarmlist");
        Console.WriteLine((item ? "Item template: " : "Project template: ") + path);
        return path;
    }
    public static bool HasCompilerError(object automation)
    {
        var errors = ((EnvDTE80.DTE2)automation).ToolWindows.ErrorList.ErrorItems;
        for (int i = 1; i <= errors.Count; i++)
        {
            var error = errors.Item(i);
            if (error.ErrorLevel == EnvDTE80.vsBuildErrorLevel.vsBuildErrorLevelHigh
                && error.Description == "Alarm 'Invalid' references missing alarm 'Missing'.") return true;
        }
        return false;
    }
    public static void DumpBuildOutput(object automation)
    {
        var errors = ((EnvDTE80.DTE2)automation).ToolWindows.ErrorList.ErrorItems;
        for (int i = 1; i <= errors.Count; i++) Console.WriteLine(errors.Item(i).Description);
        foreach (EnvDTE.OutputWindowPane pane in ((EnvDTE80.DTE2)automation).ToolWindows.OutputWindow.OutputWindowPanes)
        {
            try
            {
                var start = pane.TextDocument.StartPoint.CreateEditPoint();
                Console.WriteLine(pane.Name + ": " + start.GetText(pane.TextDocument.EndPoint));
            }
            catch (COMException) { }
        }
    }
    public static object GetProject(object automation)
    {
        return ((EnvDTE.DTE)automation).Solution.Projects.Item(1);
    }
    public static void AddItem(object automation, string template)
    {
        var project = ((EnvDTE.DTE)automation).Solution.Projects.Item(1);
        var folder = project.ProjectItems.AddFolder("sources", EnvDTE.Constants.vsProjectItemKindPhysicalFolder);
        folder.ProjectItems.AddFromTemplate(template, "Additional.almx");
    }
    public static bool FindItem(object automation, string folder, string name, string operation)
    {
        var project = ((EnvDTE.DTE)automation).Solution.Projects.Item(1);
        var items = String.IsNullOrEmpty(folder) ? project.ProjectItems : project.ProjectItems.Item(folder).ProjectItems;
        foreach (EnvDTE.ProjectItem item in items)
        {
            if (item.Name != name) continue;
            if (operation == "rename") { item.Name = "Renamed.almx"; project.Save(""); }
            if (operation == "delete") item.Delete();
            return true;
        }
        return false;
    }
    [DllImport("ole32.dll")]
    private static extern int GetRunningObjectTable(int reserved, out IRunningObjectTable table);
    [DllImport("ole32.dll")]
    private static extern int CreateBindCtx(int reserved, out IBindCtx context);
    public static object Find(int processId)
    {
        IRunningObjectTable table;
        IBindCtx context;
        Marshal.ThrowExceptionForHR(GetRunningObjectTable(0, out table));
        Marshal.ThrowExceptionForHR(CreateBindCtx(0, out context));
        IEnumMoniker enumerator = null;
        try
        {
            table.EnumRunning(out enumerator);
            IMoniker[] monikers = new IMoniker[1];
            while (enumerator.Next(1, monikers, IntPtr.Zero) == 0)
            {
                try
                {
                    string name;
                    monikers[0].GetDisplayName(context, null, out name);
                    if (name.StartsWith("!VisualStudio.DTE.", StringComparison.Ordinal)
                        && name.EndsWith(":" + processId, StringComparison.Ordinal))
                    {
                        object result;
                        table.GetObject(monikers[0], out result);
                        return result;
                    }
                }
                finally { Marshal.ReleaseComObject(monikers[0]); }
            }
            return null;
        }
        finally
        {
            if (enumerator != null) Marshal.ReleaseComObject(enumerator);
            Marshal.ReleaseComObject(context);
            Marshal.ReleaseComObject(table);
        }
    }
}
'@

function Wait-For($Action, [string]$Description) {
  $deadline = [DateTime]::UtcNow.AddSeconds(120)
  do {
    try {
      $result = & $Action
      if ($result) { return $result }
    } catch [Runtime.InteropServices.COMException] {
      if ($_.Exception.HResult -notin @(-2147418111, -2147417846)) { throw }
    }
    Start-Sleep -Milliseconds 500
  } while ([DateTime]::UtcNow -lt $deadline)
  throw "Timed out: $Description"
}

function Assert-AlarmCount([int]$Expected) {
  [xml]$compiled = Get-Content (Join-Path $projectDirectory 'bin/Debug/Plant.Alarmlist.xml')
  if (@($compiled.AlarmList.Alarm).Count -ne $Expected) {
    throw "Expected $Expected compiled alarms."
  }
}

$ide = $null
$dte = $null
try {
  $ide = Start-Process -FilePath (Join-Path $VisualStudioPath 'Common7/IDE/devenv.exe') `
    -ArgumentList @(('"' + $solutionPath + '"'), '/RootSuffix', 'AlarmlistExp', '/Log', ('"' + (Join-Path $runDirectory 'ActivityLog.xml') + '"')) `
    -WindowStyle Hidden -PassThru
  $dte = Wait-For { [AlarmlistRunningObjectTable]::Find($ide.Id) } 'Visual Studio automation startup'
  # The ROT entry may be published before the IDE accepts automation calls.
  Wait-For {
    $dte.SuppressUI = $true
    $dte.MainWindow.Visible = $false
    $dte.Solution.IsOpen
  } 'empty solution loading' | Out-Null
  $projectTemplate = [AlarmlistRunningObjectTable]::GetTemplate($dte, $false)
  $itemTemplate = [AlarmlistRunningObjectTable]::GetTemplate($dte, $true)
  if (!(Test-Path $projectTemplate) -or !(Test-Path $itemTemplate)) { throw 'Installed Alarmlist templates were not found.' }
  $dte.Solution.AddFromTemplate($projectTemplate, $projectDirectory, 'Plant', $false) | Out-Null
  $project = Wait-For { if ($dte.Solution.Projects.Count -eq 1) { [AlarmlistRunningObjectTable]::GetProject($dte) } } 'project creation'
  $dte.Solution.SaveAs((Join-Path $runDirectory 'Smoke.sln'))
  $dte.Solution.SolutionBuild.Build($true)
  if ($dte.Solution.SolutionBuild.LastBuildInfo -ne 0) { throw 'The new project did not build.' }
  Assert-AlarmCount 1
  Write-Output 'Created a project from the installed template and built its starter alarm.'

  [AlarmlistRunningObjectTable]::AddItem($dte, $itemTemplate)
  Wait-For { [AlarmlistRunningObjectTable]::FindItem($dte, 'sources', 'Additional.almx', '') } 'new item' | Out-Null
  $dte.Solution.SolutionBuild.Build($true)
  Assert-AlarmCount 2
  [AlarmlistRunningObjectTable]::FindItem($dte, 'sources', 'Additional.almx', 'rename') | Out-Null
  $dte.Solution.Close($true)
  $dte.Solution.Open((Join-Path $runDirectory 'Smoke.sln'))
  Wait-For { [AlarmlistRunningObjectTable]::FindItem($dte, 'sources', 'Renamed.almx', 'delete') } 'project reopening and item deletion' | Out-Null
  $dte.Solution.SolutionBuild.Build($true)
  Assert-AlarmCount 1
  Write-Output 'Added an item and folder, renamed the item, reopened the solution, and deleted the item.'

  $outputFile = Join-Path $projectDirectory 'bin/Debug/Plant.Alarmlist.xml'
  $before = [IO.File]::ReadAllText($outputFile)
  [IO.File]::WriteAllText((Join-Path $projectDirectory 'Invalid.almx'), '<Alarmlist><Alarm><FullyQualifiedName>Invalid</FullyQualifiedName><ReferenceName>Missing</ReferenceName></Alarm></Alarmlist>')
  Wait-For { [AlarmlistRunningObjectTable]::FindItem($dte, $null, 'Invalid.almx', '') } 'globbed input discovery' | Out-Null
  $dte.Solution.SolutionBuild.Build($true)
  if ($dte.Solution.SolutionBuild.LastBuildInfo -eq 0) { throw 'An invalid reference did not fail the IDE build.' }
  Wait-For { [AlarmlistRunningObjectTable]::HasCompilerError($dte) } 'compiler error in Error List' | Out-Null
  if ([IO.File]::ReadAllText($outputFile) -ne $before) { throw 'A failed build replaced the previous output.' }
  [AlarmlistRunningObjectTable]::FindItem($dte, $null, 'Invalid.almx', 'delete') | Out-Null
  $dte.Solution.SolutionBuild.Clean($true)
  if (Test-Path $outputFile) { throw 'Clean did not delete the output.' }
  $dte.ExecuteCommand('Build.RebuildSolution')
  Wait-For { Test-Path $outputFile } 'Rebuild output' | Out-Null
  Assert-AlarmCount 1
  Write-Output 'Compiler errors appear in Error List; failed output preservation, Clean, and Rebuild passed.'
} catch {
  if ($dte) { try { [AlarmlistRunningObjectTable]::DumpBuildOutput($dte) } catch { Write-Warning $_ } }
  throw
} finally {
  if ($dte) {
    try { $dte.Solution.Close($false); $dte.Quit() } catch { Write-Warning $_ }
  }
  if ($ide -and !$ide.WaitForExit(10000)) { Stop-Process -Id $ide.Id }
}
