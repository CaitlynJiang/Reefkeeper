FIRST-PERSON OPENING DIVER / 第一人称开场潜水员原型

Original procedurally authored geometry for Reefkeeper: waist/hips, two legs,
boots and flippers. No head, arms, hands, scuba tank, collider or camera.
蓝色潜水服、浅湖蓝脚蹼、少量黄色点缀；不含头、手臂和手。

Budget: 908 triangles, one skinned mesh, one opaque palette material, 9 bones.
Texture: 32 x 4 pixels. No transparency, cloth simulation or physical body rig.
Animation: SeatedIdle, 4-second loop; restrained asynchronous shin/foot/fin sway.
Rest pose: standing; authored clip: seated. Use Generic animation, not Humanoid:
this intentionally incomplete skeleton has no spine/head/arms for a Humanoid avatar.
This is visual lower-body embodiment, not tracked knees/feet or full-body IK.

Source: ArtSource/OpeningDiver/OpeningDiver.blend (repository root).
Rebuild: Blender --background --factory-startup --python Tools/Art/BuildOpeningDiver.py
Unity setup menu: Reefkeeper > Opening > Build seated diver preview.
Independent review scene: Assets/Reefkeeper/Scenes/SurfaceIntro_DiverPreview.unity
This scene does not replace the V4 spawn or add the playable water-entry flow.
First-person preview is a static downward composition; headset tracking is not wired yet.

Visual inspiration requested by the user:
Diver by nolanfa, https://sketchfab.com/3d-models/diver-9ee9b8b722ed457da424869cd8d08b1a
Reference listing: CC BY 4.0, https://creativecommons.org/licenses/by/4.0/
No original downloadable model, textures or animation were acquired or copied.
This newly built prototype uses its own mesh, palette, rig and animation; it is not
the nolanfa asset and is not endorsed by that artist. Reference credit retained here.

TODO: approve silhouette/colour, test seated fit and natural look-down in headset,
calibrate torso/eye offset for different seated heights, connect actual tracked hands,
test frame time on PICO, integrate the manual and explicit fade-to-dive sequence.
The head camera must never be animated by the leg idle clip.
