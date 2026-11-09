---
name: spin-box
description: Create a playable scene containing a cube that rotates using the left and right arrow keys.
---

# SpinBox Game Creation Skill

## Purpose

Construct a playable 3D scene in which the player rotates a cube around its vertical (Y) axis using the left and right arrow keys.

## Required Scene Construction

When this skill is selected for creating a **new** SpinBox scene, generate **one T2G InstructionSequence** containing all six operations below, in this order. Do not omit steps just because the user prompt mentions only the cube and its behavior. If the user explicitly excludes an element, honor that request.

1. `create_space`: create the requested space; default name `SpinBox`.
2. `create_object`: create a camera named `Camera` at `(0,1,-3)`; semantic asset `camera`.
3. `create_object`: create a plane named `Ground` at `(0,0,0)`; semantic asset `plane`.
4. `create_object`: create a cube named `Box` at `(0,0.5,0)`; semantic asset `cube`.
5. `create_object`: create a directional light named `Sun`; semantic asset `directional light`. Use the system prompt's default position when none is specified.
6. `add_component`: attach the canonical `SpinController` script to `Box`.

Use the exact action names and parameter names defined in the T2G system prompt. If the user supplies a different name or position, use that explicit value consistently, including for the script target. Do not introduce a separate `Player` object merely because the human player presses keys.

## Canonical SpinController Behavior

- Public rotation speed `Speed` is `90.0f` degrees per second.
- Holding the right arrow key rotates clockwise about Y.
- Holding the left arrow key rotates counterclockwise about Y.
- Releasing both keys stops rotation.
- Rotation is frame-rate independent using `Time.deltaTime`.
- Use `Input.GetKey`, **not** `Input.GetKeyDown`.

### Canonical Script Implementation — Copy Verbatim

The following is the canonical implementation. When the executor supports inline C# source, copy the entire script into the `add_component` instruction's script asset `source` string. Preserve the class name, field names, method, operators, and behavior. Escape newlines and quotation marks for JSON serialization, but do not alter the underlying C# code.

```csharp
using UnityEngine;

public class SpinController : MonoBehaviour
{
    public float Speed = 90.0f;
    private float multiplier;

    void Update()
    {
        if (Input.GetKey(KeyCode.RightArrow))
            multiplier = -1.0f;
        else if (Input.GetKey(KeyCode.LeftArrow))
            multiplier = 1.0f;
        else
            multiplier = 0.0f;
        transform.Rotate(Vector3.up, Speed * Time.deltaTime * multiplier);
    }
}
```

## T2G Script Instruction Mapping

For the sixth operation, generate an Elemental `add_component` instruction with `type=0`, `state=0`, and `action="add_component"`. Its required `Name` parameter identifies the cube. Use the top-level `desc` field to describe the operation; do not add an extra parameter named `desc`.

The following is a **structural illustration**. Replace the placeholder in `source` with the **complete canonical C# script above**, JSON-escaped. Do not output the placeholder as an actual instruction.

```json
{
  "type": 0,
  "state": 0,
  "action": "add_component",
  "desc": "Attach canonical SpinController to Box",
  "parameters": [
    { "name": "Name", "type": "String", "value": "Box" }
  ],
  "assets": [
    {
      "desc": "SpinController",
      "type": 3,
      "source": "<FULL JSON-ESCAPED CANONICAL C# SOURCE>"
    }
  ]
}
```

`AssetType.Script=3` identifies inline script source. Use this mapping only when supported by the configured T2G executor. Otherwise follow the system prompt's rules for unresolved or unsupported script assets.

## Existing Project Handling

When modifying an existing scene, consult CURRENT PROJECT GAMEDESC and reuse existing objects instead of creating duplicates. Reference GameDescs are examples, not proof that an object exists. Adapt the workflow to explicit user requests; do not overwrite unrelated scene content.

## Final Completeness Check

Before returning the single JSON response envelope, check that:

- The outer `instruction` has `type=3` and contains the six required child instructions for a new SpinBox scene.
- No Elemental child contains its own `instructions` array.
- The camera, ground, cube, and directional light are included unless explicitly excluded.
- The script target matches the cube's actual name.
- The script asset has `type=3`, `desc="SpinController"`, and the complete canonical source, not a rewritten controller.
- The output follows the T2G system prompt's schema and does not contain Markdown outside JSON.
