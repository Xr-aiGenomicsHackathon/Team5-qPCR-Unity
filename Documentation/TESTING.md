# Presentation setup, testing and builds

## Supported route and editor

Use **Unity 6000.3.26f1** and the saved **LobbyScene → DemoScene** route. The old realistic-lab scene is outside this presentation route. Legacy tests for that old scene are not evidence for the current lesson and should not be counted as presentation tests.

Open `Assets/Team5/Scenes/LobbyScene.unity`, enter a nickname and press Continue, then Enter Laboratory. There is no practice gate and no learner-facing PC Simulator checkbox or help overlay. For PC testing, leave **TrainingAudioSettings → Use Simulator In Editor** checked in both scenes. The optional **Show Simulator Controls In Editor** developer help overlay defaults to off; keyboard control still works. For headset testing, uncheck Use Simulator before Play; builds disable simulation automatically. After transitions the settings component re-evaluates simulated tracking once, without forcing controllers active on a headset.

## Inspector wiring and values

### Entry and audio

The lobby's Enter button calls `LobbyController.EnterLaboratory`. The controller references its nickname field, feedback field, nickname-entry panel, welcome panel, narration and `TrainingSceneFade`. A valid nickname enables entry; a loading failure restores the button and reports the problem. Both scenes must be enabled in the build list, lobby first.

Voice volume defaults to `0.8`. Why opens silently; Read Aloud speaks its current content; Replay Instruction repeats the task. Missing audio is allowed. Approved files belong in `Assets/Team5/Audio/Narration/Adeola/`, named with their exact cue keys. Use **Export Narration Recording Script** for `Temp/TrainingFlow/AdeolaRecordingScript.json`, then **Refresh Narration Clip Assignments** in each saved scene. Existing recordings are preserved. ElevenLabs generation is currently blocked by an account restriction; no successful Adeola take is being claimed.

### Hover labels

`50_LESSON_AND_UI/LAB_HOVER_LABELS` has one `LabHoverLabelController` referencing each controller's existing Near-Far Interactor, the rig and two shared popup views.

- Dwell: `0.2 s`; maximum reading distance: `3 m`.
- Each `LabLabelTarget` contains Display Name, Purpose and an optional Label Anchor. Edit these on the logical object root to change wording.
- Label-only trigger proxies use `LabLabelsOnly`. This layer is ignored by physics collisions and excluded from XRI's interaction casters. Do not move ordinary physical colliders onto this layer.
- Popups have no raycast targets and do not advance lesson steps or play speech. UI pointing has priority.
- **Install Or Refresh Hover Labels** refreshes identification coverage in DemoScene. Group models logically; do not label each mesh triangle, screw, tile or individual plate well.
- Unknown artist props use neutral fixture/container wording rather than asserting unverified contents or functions.

### PPE and qPCR

PPE requires coat, wet hands, soap, moving scrub, rinse, tap off, dry and matching-hand gloves. Timers: wet `2 s`, scrub `20 s`, rinse `3 s`, dry `2 s`. Scrub requires hands within `0.28 m` and relative speed at least `0.025 m/s`; stopped gestures pause progress. This cannot verify real washing technique.

Physical controls use the actual controller position and a `0.22 m` reach limit. Identification labels do not relax it. Plate home position is approximately `(2.49, 0.98, 3.28)`; its root collider is `(0.13, 0.02, 0.09)`, mass `0.04`. Release away from valid seating restores gravity. Floor contact compromises the teaching plate until Reset.

Protocol editing uses four pages of four fields, a numeric keypad and final review. Wrong values block Start. All run graphs and interpretations use synthetic, assay-specific teaching data.

### Optional video

Import your MP4/H.264/AAC file into `Assets/Team5/Videos/Teaching/`. Select `50_LESSON_AND_UI/QPCR_VIDEO_SYSTEM`, expand **LessonVideoController → Cues → Element 0**, and assign its **Clip**. Start with a 16:9, `1280 × 720` file. Leave State = ObserveAmplification and Auto Open checked.

The popup starts muted beside the graph and uses a 1280 × 720 RenderTexture, depth 0, antialiasing 1 and bilinear filtering. Close/pause/replay never pauses qPCR. Narration temporarily suppresses video sound. Reset/scene exit cancels playback. No clip means no empty popup; headset decoding remains a separate device test.

### Lighting

**Refine Laboratory Lighting Only** adjusts lighting without resetting materials or furniture. **Bake Laboratory Lighting** starts the bake. Save after completion.

Room lights start at 5000 K with soft shadows; static room lights are baked. Training settings use 20 texels/metre, a 2048 atlas, baked reflections at 256 resolution and 60 light-probe positions. Preserve the generated lighting data and lightmap/reflection assets when sharing.

## Automated checks

In Unity Test Runner, run all Team5 EditMode tests and these current-route PlayMode fixtures:

- `LobbySceneTests`
- `LabHoverLabelTests`
- `TrainingFlowSceneTests`
- `DemoSceneQpcrLessonTests`

The lobby fixture exercises its wired Button pointer-click path. Hover tests cover dwell, both hands, walls, range, UI priority and disabled targets. PPE tests include matching-hand glove appearances, relative-motion scrubbing and repeated scene transitions. Station tests include the real XRI plate-release lifecycle, gravity, snapping, graph meshes, protocol validation and result gating. Method-level tests do not substitute for a complete human controller/headset rehearsal.

With a connected Editor and Unity CLI:

```powershell
unity command run_tests --mode editor --filter Team5 --async_tests true --project-path . --caller plugin --skill unity-cli --format json
unity command test_status --project-path . --caller plugin --skill unity-cli --format json
unity command run_tests --mode playmode --filter LobbySceneTests --async_tests true --project-path . --caller plugin --skill unity-cli --format json
```

Run one fixture at a time and wait for completion before starting another. Record actual results and the editor version; never reuse an earlier pass count as proof for a changed scene.

## Manual acceptance checklist

1. Reject blank input; trim nickname; enforce 24 characters. Confirm Enter is immediately available without practice.
2. Click Enter twice, return to lobby and enter again. Check exactly one rig, EventSystem and AudioListener; check fresh PPE.
3. Point with either controller at objects and room surfaces. Check meaningful labels, dwell, wall occlusion, no stale popups and no UI obstruction.
4. Complete every PPE interaction; reject wrong-hand gloves and distant controls. Pause scrub movement; progress must pause. Reset restores coat/gloves.
5. Release the plate over the floor, clean home and open drawer. Check gravity, floor compromise, both snaps and wrong-A1 rejection. Close the drawer with the correctly seated plate.
6. Enter a wrong/missing protocol field. Correct it, review all sixteen values, then run. Verify live axes, legends, threshold labels, amplification, melt curve and all result questions.
7. Check silent Why/parameter selection, Replay Instruction, Read Aloud, mute/unmute, missing clips and interruption.
8. With a supplied video, test muted opening, pause, replay, close/reopen, invalid media and reset during preparation while the graph keeps updating.
9. Inspect lighting, plate/A1 readability, moving hands, metal/glass and frame rate in the actual headset. Record headset model and verified controller/tracking mode.

## Builds and clean checkout

The build helper includes LobbyScene first and DemoScene second and requires 6000.3.26f1. Use `Team5.qPCR.Editor.Team5Build.BuildWindows` or `BuildAndroid` with a new `-buildOutput` subfolder under the project's sibling `deliverables/Team5-qPCR/Builds`. Existing output is not deleted automatically. OpenXR settings are preserved/restored, not stripped for a desktop MVP.

Verify a separate checkout with Git LFS assets materialised, all metadata and the resolved package lock, no pre-existing Library, and no machine-specific runtime paths. Package downloads and licensing are initial setup requirements, not runtime dependencies. Mac/headset verification and video playback must be reported separately when those environments/media are unavailable. Do not reset or overwrite unsynced Mac work or push automatically.
