Push-Location $PSScriptRoot
try {
    & "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /win32icon:screen-seam.ico /win32manifest:seam.manifest /r:System.Xml.Linq.dll /out:ScreenSeam.exe ScreenSeam.cs ScreenSeamTests.cs OverlayRenderer.cs CalibrationWizard.cs
    if ($LASTEXITCODE -ne 0) { throw "Compilation failed" }
} finally { Pop-Location }
