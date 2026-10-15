# Team 5 XR Genomics Laboratory

VR-first educational qPCR simulation. The presentation route is **LobbyScene → DemoScene**: nickname, PPE, plate inspection, loading, protocol configuration, accelerated run, results and recap. The scene layout and supplied artist models are retained.

## Clone and open

1. Install Unity Hub and **Unity 6000.3.26f1**. Add Android Build Support (SDK, NDK, OpenJDK and IL2CPP) for Quest builds.
2. Install Git LFS, then clone and retrieve the binary assets:

   ```powershell
   git lfs install
   git clone https://github.com/JoshuaWX/Team5-qPCR-Unity.git
   cd Team5-qPCR-Unity
   git lfs pull
   git lfs fsck
   ```

3. Add the cloned folder to Unity Hub. Keep `Assets`, all `.meta` files, `Packages` and `ProjectSettings`. Allow package resolution and importing to finish.
4. Open `Assets/Team5/Scenes/LobbyScene.unity`, then press Play. No scene-generation menu is required after cloning.
5. Enter a nickname, press Continue, then Enter Laboratory. No practice-object task is required.

The project is pinned to 6000.3.26f1. Results obtained with another patch version must be reported separately. Initial package installation may require Unity Hub sign-in and internet access; imported lesson audio and video do not require a runtime service connection.

## Headset testing

XR Simulator was intentionally removed from the saved presentation scenes. Do not reinstall it or run broad scene installers to add narration. Test input using the configured OpenXR headset/runtime; an APK uses the headset's tracked controllers, not simulated PC controllers.

Aim at UI and use the trigger action; grab physical objects using grip. Object interactions require the actual controller within 22 cm. A pointing label can be read from farther away without enabling remote grabbing. Native Mac headset support is not implied by cloning the project.

## Learning and identification

- Presentation default: coat → tap on → both hands beneath water for one continuous second → tap off → dry for two seconds → left glove → right glove → qPCR. This abbreviated demonstration is **not complete handwashing**. Full Training retains soap, a 20-second movement-based scrub and rinse.
- Point at a labelled object for 0.2 seconds, within 3 metres, to see its name and purpose. Walls occlude labels and touchscreen/UI input takes priority.
- Plate checks are visual observations confirmed on the lesson card, not artificial label-touch tasks.
- Release the plate near its clean home or correctly aligned drawer holder to snap it into place. A floor drop compromises the teaching plate and requires Reset.
- Configure all 16 training values across four pages, validate, review, then Start Run.
- Observe live amplification/melt graphs and answer the results questions. Curves are **synthetic teaching data**, not patient results.
- Why opens silently; Read Aloud speaks the displayed explanation; Replay Instruction repeats the current task. Mute and volume remain available.
- Restart qPCR preserves completed PPE. Return to Lobby starts another session.

The protocol and result interpretation are assay-specific teaching examples. The simulation does not replace an approved experimental protocol, local safety procedures or real handwashing training.

## Narration and optional videos

Adeola recordings are local assets in `Assets/Team5/Audio/Narration/Adeola/`; no runtime ElevenLabs account, credentials or internet connection is required. Keep recordings and their `.meta` files together when committing or cloning. Retain old recordings until their replacements are checked.

To add or replace narration:

1. Use **Team 5 → Training Flow → Export Narration Recording Script** to export the current cue text. Save each recording as an MP3 with its exact cue key as filename, without duplicate suffixes such as `(1)`.
2. The two deliberate mappings are `Why_Ppe_Presentation_TurnOnTap` → `Why_Ppe_Presentation_WetHands.mp3` (identical explanation) and `Parameter_PostRunHold` → `Parameter_PostRunHold_15C.mp3`. The older 4°C recording is retained but is not used for the revised hold.
3. Wait for importing and compilation to finish. In Edit Mode choose **Team 5 → Training Flow → Prepare Adeola Audio For Quest**. This applies Compressed In Memory, Vorbis quality 80, preserved 44.1 kHz, mono and preloaded data, including an Android override. MP3 is a lossy source and is re-encoded during import; keep the originals and check voice quality on the headset.
4. Open DemoScene, choose **Refresh Narration Clip Assignments**, then repeat in LobbyScene. This refresh changes cue assignments without rebuilding the UI, XR rig or lesson. It reports missing or ambiguous recordings rather than selecting another voice. Do not run the installation or navigation-repair menus.
5. Check the Console for `88/88` assigned cues in each scene. Missing clips never block written instructions. Instructions play automatically; Why and parameter selection stay silent until Read Aloud; Replay Instruction repeats the task; unmuting does not start playback.

Before presenting, test lobby welcome/ready, every active PPE task, qPCR instructions, the 15°C explanation, mute/volume/replay and video sound priority. Test the built Quest application with internet disconnected; Editor playback and MP3 decoding do not prove on-headset sound quality or pronunciation.

The supplied `Videos/Video.mp4` is preserved. Its playback copy, `Assets/Team5/Videos/Teaching/QpcrProcess_Quest.mp4`, is 720 × 1280 portrait, 30 fps, H.264 Main/AAC, approximately 4 Mbps and 22.2 seconds. The existing 1280 × 720 RenderTexture uses Fit Inside: portrait footage is letterboxed, not stretched or cropped. Keep the MP4 and its `.meta` file in Git LFS when cloning.

To replace the video, import an MP4/H.264/AAC clip into `Assets/Team5/Videos/Teaching/`. In DemoScene select `40_QPCR_STATION/50_LESSON_AND_UI/QPCR_VIDEO_SYSTEM`, expand LessonVideoController → Cues → Element 0, and drag the imported VideoClip into **Clip**. Leave ObserveAmplification and Auto Open selected. The popup starts muted beside the graph; Play/Pause, Replay, Sound and Close never pause the experiment. Results collapse the panel, and Watch Video permits replay. Reset cancels preparation and clears playback. An absent clip hides the popup; a preparation failure shows a message without stopping qPCR. Narration temporarily mutes video sound.

## PPE doorway access

The glass opens automatically after the final PPE checkpoint. It takes 1.2 seconds to slide approximately 1.449 m and stays open until DemoScene is reloaded. `PPE_Doorway_Access_Gate` is a stationary sibling of `PPE_Sliding_Glass_Door`, not a child of the moving glass. Its solid BoxCollider remains enabled until `PpeSlidingGlassDoor.IsOpen` is true. Both the moving panel and the fixed barrier use the existing `LabWorldSolid` layer (9).

On `PPE_Doorway_Access_Gate`, DoorwayAccessGate → Door references the moving door; Blocker references its own BoxCollider. On the laboratory's `Epoxy_Floor`, DoorAwareTeleportationArea retains the original XRI settings and references that gate plus the XR Origin's CharacterController. This checks the body-to-destination path even if the teleport arc passes over the door. Missing references reject teleport requests rather than silently allowing access. The existing HeadBoundaryWarning references the same gate: closed-door head penetration fades the view and asks the learner to return; it never moves or freezes tracked head poses.

For a new video or door adjustment, test joystick movement at the centre and edges, teleporting beyond/above the closed door, opening clearance, passage after opening, and a fresh scene reload. Test the video with graph updates, pause/close/replay, reset during preparation and narration sound priority. Compare Quest frame timing with video enabled/disabled at the headset's actual refresh rate; Editor decoding does not establish headset performance. If the video causes sustained frame-budget misses, prepare a 540 × 960, 24 fps copy and repeat the device test.

## Development and release

This README is the only published Markdown file. Instruction sheets and development notes stay local and are not required to clone or open the project.

Builders use LobbyScene first and DemoScene second. Use a new build-output subfolder: existing builds are not deleted automatically. Build helpers require the pinned editor and preserve existing XR settings rather than stripping features for the old desktop MVP.

Baked lighting, probes, fonts, textures, audio and their metadata must accompany the scenes. Generated Library/Temp/Logs folders, credentials and signing keys must not be committed. No automatic push or publication is performed.

Physical-headset verification and independent Mac verification must be recorded separately from simulator tests. Hardware performance cannot be assumed identical. Confirm asset permissions before wider distribution.

Source Sans 3 fonts retain Adobe's copyright notice and the SIL Open Font License in [the font licence](Assets/Team5/UI/Fonts/LICENSE.txt). Unity dependencies retain their package licences. Supplied model redistribution rights are not verified; inclusion does not grant independent reuse or redistribution permission.
