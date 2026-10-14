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

## PC simulation and headset use

The PC simulator switch is deliberately hidden from learner UI. Select `Training_Audio_Settings` in each scene and use **TrainingAudioSettings → Use Simulator In Editor**:

- Checked: test VR input on the PC in Unity.
- Unchecked before Play: test with a connected headset.
- Player builds disable the simulator automatically and require the configured XR input/runtime.

Use the simulator's own controls overlay for its current bindings. Aim at UI and use the UI/trigger action; grab physical objects using grip. Object interactions require the actual controller within 22 cm. A pointing label can be read from farther away without enabling remote grabbing.

There is no new desktop-camera lesson route. Windows builds target the configured VR runtime; a Mac editor clone can use simulation, but native Mac headset support is not implied.

## Learning and identification

- Complete coat → wet hands → soap → 20-second moving scrub → rinse → tap off → dry → gloves before qPCR.
- Point at a labelled object for 0.2 seconds, within 3 metres, to see its name and purpose. Walls occlude labels and touchscreen/UI input takes priority.
- Plate checks are visual observations confirmed on the lesson card, not artificial label-touch tasks.
- Release the plate near its clean home or correctly aligned drawer holder to snap it into place. A floor drop compromises the teaching plate and requires Reset.
- Configure all 16 training values across four pages, validate, review, then Start Run.
- Observe live amplification/melt graphs and answer the results questions. Curves are **synthetic teaching data**, not patient results.
- Why opens silently; Read Aloud speaks the displayed explanation; Replay Instruction repeats the current task. Mute and volume remain available.
- Restart qPCR preserves completed PPE. Return to Lobby starts another session.

The protocol and result interpretation are assay-specific teaching examples. The simulation does not replace an approved experimental protocol, local safety procedures or real handwashing training.

## Narration and optional videos

Approved Adeola recordings belong in `Assets/Team5/Audio/Narration/Adeola/`, with their exact cue key as filename. Use **Team 5 → Training Flow → Export Narration Recording Script**, then **Refresh Narration Clip Assignments** in each saved scene after importing clips. Existing recordings remain intact. New Adeola generation is pending resolution of the ElevenLabs account restriction; missing clips never block the written lesson.

Put your own MP4/H.264/AAC teaching clip in `Assets/Team5/Videos/Teaching/`. In DemoScene select `50_LESSON_AND_UI/QPCR_VIDEO_SYSTEM`, expand LessonVideoController → Cues → Element 0, and drag the imported VideoClip into **Clip**. Leave ObserveAmplification and Auto Open selected. The popup starts muted beside the graph; playback controls never pause qPCR. No empty popup is shown before a clip is assigned.

## Development and release

See [testing, wiring and builds](Documentation/TESTING.md), [Git/LFS updates](Documentation/GITHUB.md), [science notes](Documentation/SCIENCE.md) and [asset provenance](Documentation/ASSET_PROVENANCE.md).

Builders use LobbyScene first and DemoScene second. Use a new build-output subfolder: existing builds are not deleted automatically. Build helpers require the pinned editor and preserve existing XR settings rather than stripping features for the old desktop MVP.

Baked lighting, probes, fonts, textures, audio and their metadata must accompany the scenes. Generated Library/Temp/Logs folders, credentials and signing keys must not be committed. No automatic push or publication is performed.

Physical-headset verification and independent Mac verification must be recorded separately from simulator tests. Hardware performance cannot be assumed identical. Confirm asset permissions before wider distribution.
