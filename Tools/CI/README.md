# World checks and bundle export

GitHub Actions validates both committed Unity scenes on each push and pull request. **Validate world source** checks the audio, lighting assets, platform differences, gameplay wiring and official SDK dependency restore. **Visual Review** runs a separate collider-based walkability check.

To export the PC and Quest bundles on GitHub, a repository administrator can configure **Actions secrets** with a Unity Personal activation file in `UNITY_LICENSE` and the matching `UNITY_EMAIL` and `UNITY_PASSWORD`. For Unity Pro, set `UNITY_SERIAL` in place of `UNITY_LICENSE`. See the [GameCI activation instructions](https://game.ci/docs/github/activation/). Keep activation files and account credentials in the repository secret store.

With the workflow on a feature branch, push a new tag whose name starts with `liminal-build-` and points to the commit you want to export. For example, from the intended feature commit:

```sh
git tag liminal-build-review-01
git push origin refs/tags/liminal-build-review-01
```

Once the workflow is on the default branch, you can alternatively select **Actions → Export PC and Quest world bundles → Run workflow** and choose a branch. Each successful matrix job publishes a downloadable `.vrcw` artifact. The workflow checks the Unity bundle signature and the Quest download size. Publishing to a VRChat account is a separate step.

The licensed PC job also enters Play mode with VRChat ClientSim to check both walkable orientations, six velocity directions, return travel, jump cancellation and respawn. Its simulation step reports its own result after the bundle artifact is saved.
