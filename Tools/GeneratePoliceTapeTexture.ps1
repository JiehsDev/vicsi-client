$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$textureDir = Join-Path $PSScriptRoot '../Assets/_Project/Art/Textures/PoliceTape'
[IO.Directory]::CreateDirectory($textureDir) | Out-Null
$bitmap = [Drawing.Bitmap]::new(2048,128)
$graphics = [Drawing.Graphics]::FromImage($bitmap)
$font = [Drawing.Font]::new('Arial Narrow',96,[Drawing.FontStyle]::Bold,[Drawing.GraphicsUnit]::Pixel)
$format = [Drawing.StringFormat]::new()
try {
    $graphics.Clear([Drawing.Color]::FromArgb(255,209,13))
    $graphics.TextRenderingHint = [Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $format.Alignment = [Drawing.StringAlignment]::Center
    $format.LineAlignment = [Drawing.StringAlignment]::Center
    $graphics.DrawString('POLICE LINE DO NOT CROSS',$font,[Drawing.Brushes]::Black,[Drawing.RectangleF]::new(32,0,1984,128),$format)
    $bitmap.Save((Join-Path $textureDir 'PoliceLineDoNotCross.png'),[Drawing.Imaging.ImageFormat]::Png)
} finally {
    $format.Dispose(); $font.Dispose(); $graphics.Dispose(); $bitmap.Dispose()
}
