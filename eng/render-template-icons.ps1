param()
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore, PresentationFramework, WindowsBase

# Run with Windows PowerShell -STA. Source drawings stay editable; only the ICO
# assets are packaged with templates. Each frame is rendered at its native size.
$repoRoot = Split-Path $PSScriptRoot -Parent
foreach ($name in @('AlarmList', 'Translations')) {
  $drawing = [Windows.Markup.XamlReader]::Parse([IO.File]::ReadAllText(
    (Join-Path $PSScriptRoot "assets/VisualStudio/$name.xaml")))
  $frames = @()
  foreach ($size in @(16, 20, 24, 32, 48, 64, 256)) {
    $visual = New-Object Windows.Media.DrawingVisual
    $context = $visual.RenderOpen()
    $context.DrawImage($drawing, (New-Object Windows.Rect(0, 0, $size, $size)))
    $context.Close()
    $bitmap = New-Object Windows.Media.Imaging.RenderTargetBitmap($size, $size, 96, 96, [Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($visual)
    $encoder = New-Object Windows.Media.Imaging.PngBitmapEncoder
    $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $stream = New-Object IO.MemoryStream
    $encoder.Save($stream)
    $frames += @{ Size = $size; Bytes = $stream.ToArray() }
    $stream.Dispose()
  }
  $path = Join-Path $repoRoot "src/VisualStudio/Alarmlist.VisualStudio/Templates/Items/$name/$name.ico"
  $writer = New-Object IO.BinaryWriter([IO.File]::Create($path))
  try {
    $writer.Write([UInt16]0)
    $writer.Write([UInt16]1)
    $writer.Write([UInt16]$frames.Count)
    $offset = 6 + 16 * $frames.Count
    foreach ($frame in $frames) {
      $dimension = $frame.Size % 256
      $writer.Write([byte]$dimension)
      $writer.Write([byte]$dimension)
      $writer.Write([UInt16]0)
      $writer.Write([UInt16]1)
      $writer.Write([UInt16]32)
      $writer.Write([UInt32]$frame.Bytes.Length)
      $writer.Write([UInt32]$offset)
      $offset += $frame.Bytes.Length
    }
    foreach ($frame in $frames) { $writer.Write([byte[]]$frame.Bytes) }
  }
  finally { $writer.Dispose() }
  Write-Output "Rendered $name.ico ($($frames.Count) transparent frames)."
}
