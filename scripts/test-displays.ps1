param(
    [int]$Seconds = 15
)

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$screens = [System.Windows.Forms.Screen]::AllScreens | Sort-Object { $_.Bounds.X }, { $_.Bounds.Y }
$colors = @(
    [Drawing.Color]::Coral,
    [Drawing.Color]::LightSkyBlue,
    [Drawing.Color]::LightGreen,
    [Drawing.Color]::Plum,
    [Drawing.Color]::Khaki,
    [Drawing.Color]::Salmon
)

Write-Host "Toplam ekran: $($screens.Count)" -ForegroundColor Cyan
Write-Host "Her ekranda numarali test penceresi aciliyor ($Seconds sn)..." -ForegroundColor Cyan
Write-Host "VDisplay VM'lerde fiziksel masaustunun bolunmus goruntusu gorunmeli." -ForegroundColor DarkGray
Write-Host ""

$forms = New-Object System.Collections.Generic.List[System.Windows.Forms.Form]
$i = 0

foreach ($screen in $screens) {
    $i++
    $color = $colors[($i - 1) % $colors.Count]
    $bounds = $screen.Bounds

    $form = New-Object System.Windows.Forms.Form
    $form.Text = "VDisplay test $i"
    $form.StartPosition = [System.Windows.Forms.FormStartPosition]::Manual
    $form.Location = New-Object System.Drawing.Point($bounds.X, $bounds.Y)
    $form.Size = New-Object System.Drawing.Size($bounds.Width, $bounds.Height)
    $form.FormBorderStyle = [System.Windows.Forms.FormBorderStyle]::None
    $form.TopMost = $true
    $form.BackColor = $color
    $form.Opacity = 0.85

    $label = New-Object System.Windows.Forms.Label
    $label.AutoSize = $false
    $label.Dock = [System.Windows.Forms.DockStyle]::Fill
    $label.TextAlign = [System.Drawing.ContentAlignment]::MiddleCenter
    $label.Font = New-Object System.Drawing.Font("Segoe UI", 36, [System.Drawing.FontStyle]::Bold)
    $label.ForeColor = [Drawing.Color]::Black
    $label.Text = @"
EKRAN $i
$($screen.DeviceName)
$($bounds.Width) x $($bounds.Height)
@ ($($bounds.X), $($bounds.Y))
"@

    $form.Controls.Add($label)
    $forms.Add($form) | Out-Null

    Write-Host ("[{0}] {1}  {2}x{3}  @ ({4},{5})  Primary={6}" -f $i, $screen.DeviceName, $bounds.Width, $bounds.Height, $bounds.X, $bounds.Y, $screen.Primary) -ForegroundColor Green
}

foreach ($form in $forms) {
    $form.Show()
}

$timer = New-Object System.Windows.Forms.Timer
$timer.Interval = $Seconds * 1000
$timer.Add_Tick({
    $timer.Stop()
    foreach ($form in $forms) { $form.Close() }
    [System.Windows.Forms.Application]::ExitThread()
})
$timer.Start()

[System.Windows.Forms.Application]::Run()

Write-Host "Test pencereleri kapandi." -ForegroundColor Yellow
