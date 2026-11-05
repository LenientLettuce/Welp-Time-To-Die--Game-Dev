# Original systems and merge verification

Created on 5 November 2026 before changing the original scenes or gameplay scripts.

- OriginalScenesAndSystems.unitypackage: Unity-exported original Assets tree,
  including Main, RectangleArena, scripts, UI, sprites, tests and GUIDs.
- ProjectSource.zip: original Assets, Packages, ProjectSettings, Docs, and
  AgentScripts with their paths preserved.
- AbbasArtAndRig.unitypackage: selective import used during the merge. The supplied
  original remains at `Abbas Work/Abbas.unitypackage`.
- MigrationScripts: retired setup/verification utilities and one-off merge tools.
- XML reports and PNG captures record merge verification.

Restore the original Unity package into a separate project copy for inspection
or recovery. Its Main GUID matches the current scene, so importing it over the
merged project restores the old scene and systems.

Before removal, checks included 37 Edit Mode tests, all 55 original Play Mode
regressions, and UI-driven Play Mode checks for preview/Go agreement, immediate
bone freeze/restoration, golem alignment, frame stepping, seeds, and camera.
Retirement removed superseded controllers and their tests. The integrated arena
and terrain suites remain in Assets/Tests.
