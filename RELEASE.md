# GitHub releases

Distribute this fork through GitHub Releases with manual downloads. No Microsoft Store submission, separate download website, or automatic updater is required.

## Current readiness

The release workflow is infrastructure for future releases. Before the first general public release, modernize the unsupported .NET / Windows App SDK baseline and choose public code signing. The existing self-signed development certificate is trusted only on machines where it was explicitly installed. Do not present it as a generally trusted public installer or upload its private key.

## Prepare a version

1. Merge reviewed changes, including the intended manifest version, into `master`. Use three-part release versions: `v1.2.0` maps to MSIX `1.2.0.0`. Keep the package name and publisher stable for upgrades. A public signing service may require a different publisher: resolve that identity/migration decision before the first public release.
2. Confirm CI passed for the chosen commit and test installation and upgrading from the previous version with settings retained.
3. Create and push an annotated version tag on that exact merged commit. For example, when preparing version 1.2.0, use `git tag -a v1.2.0 <reviewed-commit> -m "Audio Device Switcher 1.2.0"`, then `git push origin v1.2.0`. These are examples, not a request to release the current development version.

Tag pushes run the safe tests, build the x64 MSIX and migration utility, and validate the version and ancestry in `master`. Only after success does a separate job with `contents: write` create a **draft** release. Pull requests and branch pushes cannot run that job. The build job has read-only repository permissions, and no signing secret is configured.

## Finish the draft

1. Download `personal-package-x64-unsigned` from the exact tag's successful CI run. That historical artifact name is internal; it is not the public download name. CI artifacts expire after 14 days. Record the tag, source commit, and build URL.
2. Sign and timestamp the application MSIX with the approved signing identity. Verify its signature, publisher, version, and architecture. Keep Microsoft-signed framework dependencies unchanged. Signing changes the package hash, so calculate SHA-256 checksums **after** signing.
3. Assemble a clearly named download, such as `AudioDeviceSwitcher-1.2.0-x64.zip`, containing the signed MSIX, required x64 framework dependencies, the migration tool, applicable scripts, installation instructions, and license. Test this exact bundle on a clean supported Windows installation and as an upgrade. Cover startup, tray/background behavior, playback/recording hotkeys, and settings retention. A main MSIX alone is insufficient if the recipient lacks its framework dependencies.
4. Upload the signed distribution and `SHA256SUMS.txt` to the draft's Assets section. Replace the maintainer-only draft notes with changes, supported Windows versions, x64 architecture, installation/upgrade instructions, and known issues. Do not attach unsigned application packages or private settings as public downloads.
5. Review the actual downloadable files, then publish the release manually. No workflow currently signs, publishes, purchases signing services, or modifies certificate trust.

Existing releases are never overwritten by the workflow. A repeated draft-creation job fails rather than replacing notes or assets. Inspect any existing draft before retrying. Never move a published version tag to different code; use a new version for corrections. Retain previous release assets for diagnosis and recovery; reinstalling an older version may require an explicit downgrade procedure and compatible settings backup.

## User downloads and updates

After the first public release exists, add a prominent **Download latest version** link in the README pointing to `https://github.com/yaqub0r/AudioDeviceSwitcher/releases/latest`. Until then, do not imply a public installer is available.

Users download the release asset, not GitHub's automatically generated Source code ZIP. Initially, users update by downloading and installing the newer version over the existing fork. Keep settings and package identity stable. An automatic updater can be evaluated separately later.
