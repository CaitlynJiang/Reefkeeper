# Hand-control comparison checks

The current `immersive-opening` checkpoint uses the first tool-grip gesture logic
with PICO SDK 6.2.0. Headset swimming remains under investigation.

`CheckFirstHandControls.cs` is the current Unity script-run fixture. Enter Play
mode in `ImmersiveOpening`, finish the dive transition and release both tools.
Run its static `Main` through the project's Unity Pipeline script runner:

```text
script run --file ../Tools/Validation/CheckFirstHandControls.cs
```

It checks pinch/release, the original 15 mm grip geometry, middle-pinch journal
toggle and the retained hand-side cleaning HUD using synthetic XR Hands/XRI
inputs. It does not establish that native headset swimming works.

`CheckSwimHandShapes.cs` is retained as a historical fixture for the experimental
proportional finger classifier and comfort controls. It references APIs deliberately
removed from the comparison build and must not be run against this checkpoint.
Restore that experimental implementation before reusing it.

See `docs/swan-hand-controls-comparison.txt` for the exact comparison scope and
`docs/swan-sdk-upgrade.txt` for migration history. APKs and local backups stay
outside Git.
