# World checks and bundle export

GitHub Actions validates both committed Unity scenes on each push and pull request. **Validate world source** checks the audio, lighting assets, platform differences, gameplay wiring and official SDK dependency restore. **Visual Review** runs a separate collider-based walkability check.

## Prepare the hosted Unity license

Open **Settings → Secrets and variables → Actions** in the PC repository. Under **Repository secrets**, choose **New repository secret** for each entry. [Open the repository secret settings](https://github.com/WeepingAngeIs/liminal-uwu-pc-side-high-quility/settings/secrets/actions)

| Name | Secret value |
| --- | --- |
| `UNITY_LICENSE` | For Unity Personal, use the complete contents of the active license file. On Windows the usual location is `C:\ProgramData\Unity\Unity_lic.ulf`. If this secret already exists, keep it. |
| `UNITY_EMAIL` | The email used to sign in to the Unity ID that owns the license. |
| `UNITY_PASSWORD` | The password for that Unity ID. |

With Unity Pro, supply the license serial in `UNITY_SERIAL` in place of the Personal license file. Enter account credentials privately in GitHub. [GameCI activation instructions](https://game.ci/docs/github/activation/)

## Build and read the result

With the workflow on a feature branch, push a new tag whose name starts with `liminal-build-` and points to the commit you want to export. For example, from the intended feature commit:

```sh
git tag liminal-build-review-01
git push origin refs/tags/liminal-build-review-01
```

On the PC repository select **Actions → Export PC and Quest world bundles → Run workflow**, choose **main**, and run it once. Both editions build on GitHub, one at a time. The run summary reports the exported Quest download in bytes and its margin against the world's upload limit from the pinned VRChat SDK. Each successful export provides a downloadable `.vrcw` and a size report. Publishing to a VRChat account is a separate step. [Open the export workflow](https://github.com/WeepingAngeIs/liminal-uwu-pc-side-high-quility/actions/workflows/export-world.yml)

The licensed PC job also enters Play mode with VRChat ClientSim to check both walkable orientations, six velocity directions, return travel, jump cancellation and respawn. Its simulation step reports its own result after the bundle artifact is saved.
