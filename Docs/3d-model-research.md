# 3D model research

Research date: 14 October 2026.

## Goal and art direction

Find replacement models for Cockroach Fantasia, a cute, comedic kitchen game
built with Unity 6.3 LTS and the Universal Render Pipeline (URP).

Prefer a consistent cartoon style across cockroaches, the human, food, and
furniture. For cockroaches, the suggested direction is big eyes, a rounded body,
and exaggerated antennae. These are selection criteria, not verified features
of the candidates below.

## Cute cockroach candidates

| Candidate | Findings | Still to check |
| --- | --- | --- |
| [Cockroach — Quirky Series, Omabuarts Studio](https://sketchfab.com/3d-models/cockroach-quirky-series-22ae6ce868654a44b9bd99866fbf1979) | Search listing has cute/cartoon tags. First candidate to inspect for visual fit. | Current purchase/download availability, price, licence, file formats, rig, animation clips, and Unity material setup. |
| [Cockroach Low Poly, Gabriel_Adriano](https://gabriel-adriano.itch.io/cockroach) | Search listing describes a cartoon model, a rigged FBX, original Blender file, PNG texture, and rendered images. Listed at a $1 USD minimum during research. | Current price, commercial-use licence, visual fit, and whether animation clips are included. A rig alone does not establish that animations are supplied. |

The candidate details above came from search listings. Direct page retrieval
failed during research, so neither candidate has been fully inspected or tested
in Unity. No assets have been purchased or downloaded.

## Other sources and packs

| Source | Suggested use | Notes |
| --- | --- | --- |
| [Kenney Food Kit](https://kenney.nl/assets/food-kit) | Replace food props. | Official page lists 200 assets, free download, and CC0 licence. |
| [Kenney Furniture Kit](https://kenney.nl/assets/furniture-kit) | Replace furniture and interior props. | Official page lists 140 assets, free download, and CC0 licence. Suggested companion to the food pack; inspect available pieces for kitchen coverage. |
| [Sketchfab cockroach search](https://sketchfab.com/search?q=cockroach&type=models) | Find more cockroach candidates. | Filter for downloadable and animated models. Availability and licences vary by model. |
| [Mixamo](https://www.mixamo.com/) | Browse human characters and preview animations. | Potential source for the human character and movement animations; inspect style and terms before selecting. |
| [Unity Asset Store](https://assetstore.unity.com/) | Find matching environment and character packs. | Suggested searches: "stylized kitchen", "cartoon insect", and "animated cockroach". No particular pack was selected or verified. |

## Selection and integration checklist

- Prefer FBX for character imports, with a rig and supplied animation clips.
- Look for cockroach idle and walk/run animations; preview movement before buying.
- Check URP-compatible materials, or whether supplied textures can be assigned to URP materials.
- Check scale, orientation, polygon count, and texture sizes for gameplay suitability.
- Confirm the asset licence permits the intended game release, and record provenance and any required credits.
- Keep the existing player controls, networking, and colliders when replacing character visuals.
- Adapt the character animation setup: the current project refers to named body parts such as legs and antennae, so an imported rig should not be assumed to work as a direct swap.

## Suggested next step

Inspect the two cartoon cockroach candidates and their movement previews first.
Kenney's food and furniture packs are a promising starting point for a consistent
environment upgrade. Select the cockroach as the main visual focus, then match
the rest of the game's art to it.
