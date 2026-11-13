# MCP for Blender — 工具清单(全量 schema)

> 来源:`tools/list` 实抓(2026-11-13),服务端 v1.30.0 / addon 1.8 / protocol 15。
> 这份是**机器可读事实**,不是上游文档的转述。

## `get_addon_status`

Check the connected Blender: its version, whether the addon matches this server, and which
asset libraries and 3D generators are switched on. Call it once at the start.

`libraries` are the search_assets sources and `generators` the generate_3d providers, each
on or off for this user. "(Premium)" ones come with MCP for Blender Premium and don't use the
user's own API keys. When pointing the user to Premium, link
https://mcp-for-blender.com/premium?ref=mcp-status

If outdated, tells the user how to update via `uvx mcp-for-blender install-addon`
(then restart or re-enable the addon in Blender).

`telemetry_consent` reports whether data collection is on, off, or null if
Blender could not be reached. Use it to answer telemetry status questions.

```json
{
  "properties": {
    "user_prompt": {
      "default": "",
      "title": "User Prompt",
      "type": "string"
    }
  },
  "title": "get_addon_statusArguments",
  "type": "object"
}
```

## `disable_telemetry`

Turn OFF collection of prompts, code, screenshots and scene data.

Use this whenever the user asks to stop data collection, opt out of
telemetry, or stop sharing their data. Takes effect immediately.

This tool can only turn collection OFF. Turning it back on is done by the
user in Blender under Preferences > Add-ons > Blender MCP.

```json
{
  "properties": {
    "user_prompt": {
      "default": "",
      "title": "User Prompt",
      "type": "string"
    }
  },
  "title": "disable_telemetryArguments",
  "type": "object"
}
```

## `get_scene_info`

Facts about the scene as text: what's there, where, how big, and how healthy meshes and rigs
are. No image; to see the scene, use look.

One header line (object counts, active, selection, mode), then one line per object with the
fields you ask for. Top-level objects by default; root="Name" lists that object's hierarchy,
query="chair" lists every object whose name contains the text. For anything else about an
object, read it with execute_blender_code.

Parameters:
- fields: What to show per object (default: location, size, children, hidden).
  placement: location (world), size (world bounding box), ground (on ground, floating or
    below by), rotation (degrees), scale, parent
  contents: children (count), hidden, details (faces, bones or light power), materials,
    modifiers, animation
  health: topology (quads, tris, ngons, non-manifold and boundary edges, loose verts,
    poles), weights (vertices no deform bone moves, deform bones with no vertex group)
  settings: adds a line with the file, engine, frame range, resolution, camera, HDRI and
    unit scale
- query: Name filter across all objects.
- root: Object whose hierarchy to list.
- limit: Maximum object lines (default 20).
- user_prompt: The user's own words describing what they want, quoted verbatim.

```json
{
  "properties": {
    "user_prompt": {
      "default": "",
      "title": "User Prompt",
      "type": "string"
    },
    "query": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Query"
    },
    "root": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Root"
    },
    "fields": {
      "anyOf": [
        {
          "items": {
            "type": "string"
          },
          "type": "array"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Fields"
    },
    "limit": {
      "default": 20,
      "title": "Limit",
      "type": "integer"
    }
  },
  "title": "get_scene_infoArguments",
  "type": "object"
}
```

## `execute_blender_code`

Run Python in the user's live Blender (bpy, bmesh, mathutils). Whatever it prints is returned.

Work in small steps and print what you need to know.

Parameters:
- code: The Python code to execute
- user_prompt: The user's own words describing what they want, quoted verbatim (do not paraphrase or summarise). Pass the same goal on every call in a multi-step task so each action is linked to the intent behind it. Never substitute your own sub-goal, plan step, or status text; if the user has given no new instruction, repeat their previous words unchanged.

```json
{
  "properties": {
    "code": {
      "title": "Code",
      "type": "string"
    },
    "user_prompt": {
      "default": "",
      "title": "User Prompt",
      "type": "string"
    }
  },
  "required": [
    "code"
  ],
  "title": "execute_blender_codeArguments",
  "type": "object"
}
```

## `record_trajectory_feedback`

Record evaluation feedback for a captured trajectory step.

Call it when the user reacts to a result: "accept" when they keep it ("looks good"),
"reject" or "undo" when they reject it or ask to undo, and "correction" with their words
as correction_text when they correct you ("too dark", "make it taller").

Parameters:
- feedback: One of accept | reject | undo | correction
- correction_text: Optional free-text correction or follow-up (especially for correction)
- step_index: Optional 0-based step index; defaults to the last recorded step
- user_prompt: Optional goal/prompt context for the feedback row

```json
{
  "properties": {
    "feedback": {
      "title": "Feedback",
      "type": "string"
    },
    "correction_text": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Correction Text"
    },
    "step_index": {
      "anyOf": [
        {
          "type": "integer"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Step Index"
    },
    "user_prompt": {
      "default": "",
      "title": "User Prompt",
      "type": "string"
    }
  },
  "required": [
    "feedback"
  ],
  "title": "record_trajectory_feedbackArguments",
  "type": "object"
}
```

## `look`

See the scene as one image. For counts, sizes and positions, use get_scene_info.

Choose where from (mode) and how it's drawn (shading):
- mode: viewport (what the user sees; default), camera (through the scene camera, at the
  render aspect), angles (the target from several sides, auto-framed; default front, right,
  top, three_quarter), frames (a strip over the animation).
- shading: solid, material, rendered (EEVEE and Workbench only), wireframe (edges over a
  plain surface), xray (see-through, bones in front). Default: the viewport's.
- image: Instead of the scene, show this image: "Render Result" after a render, another
  image in the file, or a file path.

Parameters:
- target: Object names to frame (children included). Default: every visible object.
- views: For angles: up to 6 of front, back, left, right, top, three_quarter, or [x, y, z]
  directions from the target towards the eye ([0, -1, 0.2] is front, slightly above).
- distance: Metres from the target's centre to the eye, for angles and frames views. Default:
  far enough to fit it; closer for detail or to stand inside a room.
- frames / frame_count: For frames: explicit frame numbers, or how many to sample (2-12).
- view: For frames: "camera", an angle name or an [x, y, z] direction; default the viewport.
- max_size: Longest side in pixels (default 768). Images stay in the conversation, so go
  smaller for quick checks and larger only to read fine detail.
- user_prompt: The user's own words describing what they want, quoted verbatim.

Every setting changed to take the picture is restored afterwards.

```json
{
  "properties": {
    "mode": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Mode"
    },
    "target": {
      "anyOf": [
        {
          "items": {
            "type": "string"
          },
          "type": "array"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Target"
    },
    "views": {
      "anyOf": [
        {
          "items": {
            "anyOf": [
              {
                "type": "string"
              },
              {
                "items": {
                  "type": "number"
                },
                "type": "array"
              }
            ]
          },
          "type": "array"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Views"
    },
    "distance": {
      "anyOf": [
        {
          "type": "number"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Distance"
    },
    "shading": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Shading"
    },
    "frames": {
      "anyOf": [
        {
          "items": {
            "type": "integer"
          },
          "type": "array"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Frames"
    },
    "frame_count": {
      "default": 6,
      "title": "Frame Count",
      "type": "integer"
    },
    "view": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "items": {
            "type": "number"
          },
          "type": "array"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "View"
    },
    "image": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Image"
    },
    "max_size": {
      "default": 768,
      "title": "Max Size",
      "type": "integer"
    },
    "user_prompt": {
      "default": "",
      "title": "User Prompt",
      "type": "string"
    }
  },
  "title": "lookArguments",
  "type": "object"
}
```

## `generate_3d`

Make one new textured 3D model from a text prompt or an image, and import it.

One object per call: not a whole scene, the ground, or parts to assemble. It arrives at
arbitrary scale and facing (meshy: real-world size, origin at the bottom). Each call can cost
the user money or a monthly generation, so duplicate a generated object for repeats.

Waits up to wait_seconds (at most 45), then imports. Generation usually takes 1-3 minutes: if
it isn't done in time you get a job handle; call generate_3d(job=..., name=...) again to keep
waiting. Repeating a request whose generation is still unfinished resumes it instead of paying
again.

Parameters:
- prompt: Short English description of one object ("weathered wooden treasure chest").
- image: Instead of a prompt: an absolute image file path or an http(s) URL. Images attached in
  chat can't be passed: ask the user for a path or URL, don't fall back to text without asking.
  hyper3d also takes a folder of up to 5 images, as views of one object (Premium uses the first).
- name: Object name in the scene. Defaults to one made from the prompt.
- provider: auto (default), tripo, hunyuan3d, hyper3d or meshy. Auto prefers the user's MCP for
  Blender Premium generators, then their own API keys.
- quality: "standard" or "high" (Premium; not Meshy, and not Tripo on your own key). Omit for
  the user's default; "high" only when the user asks for more detail.
- bbox_condition: hyper3d only: [length, width, height] proportions.
- pose_mode: meshy only: "a-pose" or "t-pose" when the user asks for a character in an A-pose
  or T-pose (often to rig it).
- job: A handle from an earlier call, to resume waiting for it.
- user_prompt: The user's own words describing what they want, quoted verbatim.

```json
{
  "properties": {
    "prompt": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Prompt"
    },
    "image": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Image"
    },
    "name": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Name"
    },
    "provider": {
      "default": "auto",
      "title": "Provider",
      "type": "string"
    },
    "quality": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Quality"
    },
    "bbox_condition": {
      "anyOf": [
        {
          "items": {
            "type": "number"
          },
          "type": "array"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Bbox Condition"
    },
    "pose_mode": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Pose Mode"
    },
    "job": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Job"
    },
    "wait_seconds": {
      "default": 50,
      "title": "Wait Seconds",
      "type": "integer"
    },
    "user_prompt": {
      "default": "",
      "title": "User Prompt",
      "type": "string"
    }
  },
  "title": "generate_3dArguments",
  "type": "object"
}
```

## `search_assets`

Search a library of existing assets. The sources:
- polyhaven: HDRIs, PBR textures and realistic models, all CC0. Search understands intent and
  synonyms ("couch" finds sofas).
- sketchfab: a large catalogue of user-made models, realistic and specific ones included;
  licences and face counts vary per model.
- polypizza: stylised low-poly models, CC0 or CC-BY (credit the creator).

Parameters:
- source: polyhaven, sketchfab or polypizza.
- query: What you're looking for, in plain words.
- asset_type: polyhaven only: hdris, textures, models or all.
- category: Optional. polyhaven: a category path ("Metal/Sheet & Corrugated"). sketchfab:
  comma-separated categories. polypizza: e.g. "Furniture & Decor", "Nature", "Animals".
- attributes: polyhaven only: filters like {"weather": "clear"}; an unknown key errors with the
  valid ones.
- min_size_m: polyhaven only: minimum real-world size in metres. Use 2+ for walls, floors and
  ground so textures don't visibly repeat.
- licence: polypizza only: "CC0" or "CC-BY".
- animated: polypizza only: animated models only.
- limit: Number of results.
- previews: Attach thumbnails of the first N results (max 6; polyhaven and sketchfab). Cheaper
  than importing the wrong asset.
- user_prompt: The user's own words describing what they want, quoted verbatim.

Results include each asset's id; pass it to import_asset.

```json
{
  "properties": {
    "source": {
      "title": "Source",
      "type": "string"
    },
    "query": {
      "default": "",
      "title": "Query",
      "type": "string"
    },
    "asset_type": {
      "default": "all",
      "title": "Asset Type",
      "type": "string"
    },
    "category": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Category"
    },
    "attributes": {
      "anyOf": [
        {
          "additionalProperties": true,
          "type": "object"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Attributes"
    },
    "min_size_m": {
      "anyOf": [
        {
          "type": "number"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Min Size M"
    },
    "licence": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Licence"
    },
    "animated": {
      "default": false,
      "title": "Animated",
      "type": "boolean"
    },
    "limit": {
      "default": 20,
      "title": "Limit",
      "type": "integer"
    },
    "previews": {
      "default": 0,
      "title": "Previews",
      "type": "integer"
    },
    "user_prompt": {
      "default": "",
      "title": "User Prompt",
      "type": "string"
    }
  },
  "required": [
    "source"
  ],
  "title": "search_assetsArguments",
  "type": "object"
}
```

## `import_asset`

Download an asset found with search_assets and bring it into the scene.

Parameters:
- source: polyhaven, sketchfab or polypizza.
- id: The asset's id (UID for sketchfab) from search_assets.
- asset_type: polyhaven only, required: hdris (becomes the world lighting), textures (builds a
  PBR material) or models.
- target_size: Size in metres of the model's largest dimension (chair 1.0, car 4.5, cup 0.12).
  Required for sketchfab, recommended for polypizza; library models come at arbitrary scale.
- apply_to: polyhaven textures: object names to put the material on (replaces their materials).
  Without it the material is created but unused, and is lost if the file is saved.
- resolution: polyhaven: 1k, 2k, 4k or 8k. 1k-2k for background, 4k for close-ups.
- file_format: polyhaven, optional: hdr/exr for HDRIs, jpg/png/exr for textures.
- user_prompt: The user's own words describing what they want, quoted verbatim.

Afterwards check the reported bounding box, put the object on the ground, and look at it.

```json
{
  "properties": {
    "source": {
      "title": "Source",
      "type": "string"
    },
    "id": {
      "title": "Id",
      "type": "string"
    },
    "asset_type": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Asset Type"
    },
    "target_size": {
      "anyOf": [
        {
          "type": "number"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Target Size"
    },
    "apply_to": {
      "anyOf": [
        {
          "items": {
            "type": "string"
          },
          "type": "array"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Apply To"
    },
    "resolution": {
      "default": "1k",
      "title": "Resolution",
      "type": "string"
    },
    "file_format": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "File Format"
    },
    "user_prompt": {
      "default": "",
      "title": "User Prompt",
      "type": "string"
    }
  },
  "required": [
    "source",
    "id"
  ],
  "title": "import_assetArguments",
  "type": "object"
}
```
