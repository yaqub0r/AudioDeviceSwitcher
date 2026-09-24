# Personal build installation

This is a personal development build of the fork, not a Microsoft Store release. It retains the original .NET 6 / Windows App SDK 1.1 dependency baseline. Modernizing those unsupported dependencies is a separate maintenance task.

## Build and provenance

CI produces the `personal-package-x64-unsigned` artifact after the safe test suite and MSIX build succeed. Use the artifact from the exact reviewed commit. It contains the unsigned personal package, any generated framework packages, a self-contained migration utility, and installer scripts.

The app's identity and execution alias differ from upstream. Its startup entry is disabled in the package manifest so merely installing it cannot compete with the Store app at the next login.

## Back up and prepare settings

Run the included migration utility with a new directory outside source control:

```powershell
SettingsMigration.exe prepare C:\path\to\new-private-backup
```

This reads the Store application's `settings` value through `ApplicationDataManager.CreateForPackageFamily`. It writes `original-settings.json`, `prepared-settings.json`, and `migration-report.json`, without modifying either application's settings. Existing directories are rejected to preserve earlier backups.

For missing IDs, the tool checks the optional Windows registry endpoint-history property `{4b416b7d-8501-40c1-acfd-97aa9bdc17c8},1`. This undocumented property is treated as an optional migration aid, not a guaranteed Windows interface. Only an exact historical ID with one current candidate in the same device category is recovered. Current IDs win; unresolved and ambiguous entries remain saved. Current device names are obtained through the same Windows enumeration API as the app.

## Sign locally

Create a code-signing certificate whose subject is `CN=yaqub0r.AudioDeviceSwitcher`, with a non-exportable private key in the current user's personal certificate store. Export only the public `.cer`. Sign the package using Microsoft's SignTool with SHA-256 and that certificate's thumbprint. Keep the certificate for future upgrades; do not generate a replacement for every build.

Review the package manifest, signing certificate thumbprint, and signed-package SHA-256 hash before trusting it. Windows requires the self-signed public certificate in the local machine's **Trusted People** store to install the MSIX. This is a security decision and usually requires elevation. Do not add it as a root CA or disable Windows signature checks. Neither installation script adds certificate trust.

## Preview and apply

Run `scripts/Install-Personal.ps1` with these mandatory arguments:

- `PackagePath`: the locally signed personal MSIX.
- `MigrationToolPath`: the built `SettingsMigration.exe`.
- `PreparedSettingsPath`: the private `prepared-settings.json`.
- `ExpectedPackageSha256`: the reviewed signed-package SHA-256.
- `ExpectedSignerThumbprint`: the reviewed certificate thumbprint.

Without `-Apply`, it validates the package identity, hash, signer, and unchanged source backup and shows a preview. With `-Apply`, it additionally requires a trusted, valid signature, installs the separate package, imports into an empty settings store, verifies all commands, records rollback information, transfers startup preference, and stops only the original package's process. Then launch **Audio Device Switcher (Personal)** and test the hotkeys. The Store package and its settings are retained.

The script is intentionally for first installation. It refuses to overwrite an existing personal app or existing personal settings. Future app upgrades should preserve those settings and use a higher package version signed by the same publisher.

## Rollback

Run `scripts/Rollback-Personal.ps1 -InstallationRecord <installation-record.json>` to preview; add `-Apply` to stop the personal app, disable its startup, and restore the original startup preference. Open the original Store app afterward. Both apps, both settings stores, and the private backup are preserved. Removing a package or signing certificate is a separate explicit action.
