Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$screen = [Windows.Forms.Screen]::PrimaryScreen.Bounds
$w = [Math]::Min(400, $screen.Width)
$h = [Math]::Min(200, $screen.Height)
$bmp = New-Object System.Drawing.Bitmap $w, $h
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($screen.X, $screen.Y, 0, 0, [System.Drawing.Size]::new($w, $h))

$dark = 0
$total = 0
for ($x = 0; $x -lt $w; $x += 20) {
    for ($y = 0; $y -lt $h; $y += 20) {
        $c = $bmp.GetPixel($x, $y)
        $total++
        if ($c.R -lt 10 -and $c.G -lt 10 -and $c.B -lt 10) { $dark++ }
    }
}

$out = Join-Path (Split-Path -Parent $PSScriptRoot) "cap-test.png"
$bmp.Save($out)
Write-Host "Primary $($screen.Width)x$screen.Height @ ($($screen.X),$($screen.Y))"
Write-Host "Dark pixels: $dark / $total"
Write-Host "Saved: $out"
