Unity Editor capture scripts (kept outside Assets to avoid runtime compilation).
Run through Unity Pipeline script run, with the Reefkeeper project open:
script run --file ../Tools/Art/CaptureAssetPortraits.cs
script run --file ../Tools/Art/ComposeReefIcon.cs
Uses existing game meshes/materials; no generated character/species images.
Portrait capture restores changed shader globals in finally. Temporary cameras/models/canvases are cleaned up.
The composition script creates WithHelmet and WithoutHelmet comparisons; copy WithHelmet PNGs to AppIcon root to update the chosen delivery after rerunning.
