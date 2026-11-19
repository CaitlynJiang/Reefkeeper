# Hand-control comparison checks

The current `immersive-opening` checkpoint uses the first tool-grip gesture logic
with PICO SDK 6.2.0. The user confirmed swimming works in this rollback APK on
the Swan headset. The later regression's individual cause remains under investigation.

`CheckHandControls.cs` is the current Unity script-run fixture. Enter Play
mode in `ImmersiveOpening`, finish the dive transition and release both tools.
Run its static `Main` through the project's Unity Pipeline script runner:

```text
script run --file ../Tools/Validation/CheckHandControls.cs
```

It checks pinch/release, the 30 mm opposing grip/pinch offsets, middle-pinch journal
toggle and the retained hand-side cleaning HUD using synthetic XR Hands/XRI
inputs. The current increment changes only the tool's visual grip pose; the
headset-confirmed swim/gesture code and scene parameters remain unchanged. These
checks do not establish the new grip's fit on the user's hand.

The baseline tag retains `CheckFirstHandControls.cs` with the original 15 mm
geometry expectations. Use that version only when testing the baseline.

`CheckSwimHandShapes.cs` is retained as a historical fixture for the experimental
proportional finger classifier and comfort controls. It references APIs deliberately
removed from the comparison build and must not be run against this checkpoint.
Restore that experimental implementation before reusing it.

See `docs/swan-hand-controls-comparison.txt` for the exact comparison scope and
`docs/swan-sdk-upgrade.txt` for migration history. APKs and local backups stay
outside Git.
