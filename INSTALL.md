# Fork installation and upgrades

This is the yaqub0r-maintained fork, not a release from the original Microsoft Store listing. It retains the original .NET 6 / Windows App SDK 1.1 dependency baseline. Modernizing those unsupported dependencies is a separate maintenance task before a general public release.

## Upgrade from the Personal build and replace the Store app

Version 1.1.1.0 removes “(Personal)” from the visible name without changing package name, publisher, or settings identity. Use a higher-versioned MSIX signed with the existing certificate; do not uninstall the fork before upgrading it. The standard `AudioDeviceSwitcher.exe` alias is now provided, while `AudioDeviceSwitcherPersonal.exe` remains as a compatibility alias.

1. Verify the build's CI result, manifest identity/version, signed-package hash, and trusted signer, as described below.
2. Stop both apps using their exact installed package paths. Run the new migration utility's `backup <new-private-backup-directory>` command. It backs up the fork's current settings and, if installed, the Store app's settings without overwriting either. Keep the previous signed MSIX as well.
3. Run `Add-AppxPackage -Path <signed-msix>` to upgrade the fork in place. Run `SettingsMigration.exe verify-backup <backup-directory>` before launching to confirm that every saved setting is unchanged.
4. Check that the fork's startup preference is retained. Remove only the original package for the current user: `Get-AppxPackage -Name 16084JoseTorres.AudioDeviceSwitcher | Remove-AppxPackage`. This removes the Store app's live data, so retain the settings backup first. Do not remove `yaqub0r.AudioDeviceSwitcher` or shared runtime dependencies.
5. Open **Audio Device Switcher**, check the About repository points to `yaqub0r/AudioDeviceSwitcher`, and test a saved hotkey. Re-pin the app if a pinned shortcut belonged to the removed Store app.

After removal, the old startup-handover rollback script alone cannot restore the Store app. Recovery requires reinstalling the original Store package and restoring its saved settings, or reinstalling the retained previous fork package and restoring its fork settings. Keep backups private and do not commit them or attach them to public issues.

## Build and provenance

CI produces the `personal-package-x64-unsigned` artifact after the safe test suite and MSIX build succeed. Use the artifact from the exact reviewed commit. It contains the unsigned personal package, any generated framework packages, a self-contained migration utility, and installer scripts.

The app's identity differs from upstream. Its startup entry is disabled in the package manifest so merely installing it cannot compete with the Store app at the next login. The standard execution alias is shared with upstream, so complete migration and remove the old app before relying on it.

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

Without `-Apply`, it validates the package identity, hash, signer, and unchanged source backup and shows a preview. With `-Apply`, it additionally requires a trusted, valid signature, installs the separate package, imports into an empty settings store, verifies all commands, records rollback information, transfers startup preference, and stops only the original package's process. Then launch **Audio Device Switcher** and test the hotkeys. This first-install script retains the Store package and its settings; remove it after verification using the replacement steps above.

The script is intentionally for first installation. It refuses to overwrite an existing personal app or existing personal settings. Future app upgrades should preserve those settings and use a higher package version signed by the same publisher.

## Rollback

Run `scripts/Rollback-Personal.ps1 -InstallationRecord <installation-record.json>` to preview; add `-Apply` to stop the personal app, disable its startup, and restore the original startup preference. Open the original Store app afterward. Both apps, both settings stores, and the private backup are preserved. Removing a package or signing certificate is a separate explicit action.
