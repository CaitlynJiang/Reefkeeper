Scallop currency source supplied by the user, 2026-11-21.
Original location: C:/Users/anlan/Downloads/scallop
The .blend and icon-export scripts are archived here, outside Unity Assets.
Runtime uses all six AnglePreviews images in a shared 768x512 atlas at
Resources/ScallopReward/Scallop.png. Each flight randomly selects one angle;
the counter uses 03_right_30.png (upper-right view). render_angles.py renders
the original model; Tools/Validation/PrepareScallopAtlas.cs packs the images.
The original blend, original icons, and full-resolution angle renders are preserved.
White and red alternatives are retained as source variants; no alternate currency tiers are implemented.
