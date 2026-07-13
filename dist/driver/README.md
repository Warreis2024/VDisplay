# Prebuilt driver package (end users)

This folder is installed by **Helper → 0. İlk kurulum** / `scripts/install-driver.ps1`.

Contents:

- `VDisplayDriver.dll` / `.inf` / `.cat` — test-signed IDD package  
- `VDisplayTestCert.cer` — trust this (script installs to Root + TrustedPublisher)

**No Visual Studio / WDK required** on end-user PCs.

Developers: after changing driver source, run:

```powershell
.\scripts\build-driver.ps1   # or MSBuild with SignMode=Off
.\scripts\publish-driver-package.ps1
```

Then commit updated files under `dist/driver/`.
