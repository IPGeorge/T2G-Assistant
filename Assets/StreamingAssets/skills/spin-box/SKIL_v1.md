\---

name: spin-box

description: Create a playable scene containing a cube that rotates using the left and right arrow keys.

\---



\# SpinBox Game Creation Skill



\## Purpose



Construct a playable 3D scene in which the player rotates a cube around its vertical axis using the left and right arrow keys.



\## Scene Construction



When the user requests a SpinBox-style game, generate an InstructionSequence containing the operations needed to create the following elements.



1\. Create the requested space, using `SpinBox` as the default name.

2\. Create a camera named `Camera` at `(0,1,-3)`.

3\. Create a plane named `Ground` at `(0,0,0)`.

4\. Create a cube named `Box` at `(0,0.5,0)` so that it rests on the ground.

5\. Create a directional light named `Sun`.

6\. Add a `SpinController` behavior script to `Box`.



Use `create\_space`, `create\_object`, and `add\_component` as appropriate.



\## SpinController Behavior



Generate a Unity C# MonoBehaviour script named `SpinController` with the following behavior:



\- Public rotation speed `Speed`, defaulting to 90 degrees per second.

\- Right arrow key rotates the cube clockwise around the Y axis.

\- Left arrow key rotates the cube counterclockwise around the Y axis.

\- No arrow key stops rotation.

\- Rotation uses `Time.deltaTime` for frame-rate independence.


Canonical Script Implementation — Copy Verbatim
The following C# script is the canonical implementation.
When generating the add_component instruction, copy this
script exactly into the script asset's source field.
Do not regenerate or modify the implementation.

```csharp

using UnityEngine;



public class SpinController : MonoBehaviour

{

&#x20;   public float Speed = 90.0f;

&#x20;   private float multiplier;



&#x20;   void Update()

&#x20;   {

&#x20;       if (Input.GetKey(KeyCode.RightArrow))

&#x20;           multiplier = -1.0f;

&#x20;       else if (Input.GetKey(KeyCode.LeftArrow))

&#x20;           multiplier = 1.0f;

&#x20;       else

&#x20;           multiplier = 0.0f;



&#x20;       transform.Rotate(

&#x20;           Vector3.up,

&#x20;           Speed \* Time.deltaTime \* multiplier);

&#x20;   }

}

```



\## Instruction Generation



\- Generate one InstructionSequence for the complete task.

\- Create the space before its objects.

\- Generate creation instructions for the camera, ground, cube, and light.

\- Generate an `add\_component` instruction that targets `Box` and includes the `SpinController` script as a script asset.

\- Use the T2G instruction schema and asset representation defined in the system prompt.

\- Include the complete C# source in the script asset's `source` field when supported by the T2G executor.

\- Do not output a standalone Markdown code block instead of T2G instructions.

\- Do not claim the scene or script has already been executed.



\## Existing Project Handling



If the current GameDesc already contains the requested objects, reuse them instead of creating duplicates.



If the user explicitly requests a different space name, object name, position, or rotation behavior, adapt the workflow accordingly.

