# Learned Code Patterns - T2G Assistant

*This file documents coding patterns and techniques learned during development. 
It can be appended to or updated as new insights are gained.*

Last Updated: Auto-update recommended on each session start

---

## Pattern 1: Bidirectional Property Name Mapping
**File**: `Assets/Scripts/GameDesc/GameDescManager.cs`  
**Method**: `ReplaceWorldOrLocalProperty`  
**Purpose**: Remove corresponding local/non-local property when setting transform properties

### Structure:
```csharp
string counterpart = propertyName switch
{
    "Position" => "localPosition",
    "localPosition" => "Position",
    "Rotation" => "localRotation",
    "localRotation" => "Rotation",
    "Scale" => "localScale",
    "localScale" => "Scale",
    _ => null
};
```

### Usage Locations:
- **`ApplyPlaceOn`**: Removes local* properties when setting Position/Rotation/Scale on objects
- **`ApplySetProperty`**: Checks for all 6 property name variants before calling the method

### Key Insight:
Handle both directions: setting `Position` → removes `localPosition`, AND setting `localPosition` → removes `Position`.

---

## Pattern 2: Enhanced Property Validation
**File**: `Assets/Scripts/Translation/InstructionValidator.cs`  
**Change**: `break` → `result.AddError(...)` + variable rename

### Before:
```csharp
break;
```

### After:
```csharp
result.AddError($"Instruction {instruction.id}: " + $"Missing target object {objName}");
return;
```

### Also Renamed:
`foundParameter` → `param` for consistency in error messages.

### Key Insight:
Always record validation errors instead of silently skipping invalid instructions. This improves debugging and user feedback.

---

## Pattern 3: Position Formatting Cleanup
**File**: `Assets/Scripts/Translation/CommandTranslator/Translators/CmdTranslator_CreateObject.cs`  
**Change**: Remove extraneous spaces in Position parameter

### Before:
```csharp
instruction.parameters.Add(new Instruction.Parameter("Position", $"({x}, {y}, {z})"));
```

### After:
```csharp
instruction.parameters.Add(new Instruction.Parameter("Position", $"({x},{y},{z})"));
```

### Key Insight:
Consistent formatting prevents parsing issues and makes JSON/output more compact.

---

## How to Update This File

### To Add a New Pattern:
1. Add a new "Pattern X" section following the same structure
2. Include: File, Method/Purpose, Structure, Usage Locations, Key Insight
3. Update "Last Updated" timestamp

### To Update an Existing Pattern:
1. Locate the relevant section by file name or method name
2. Modify the code structure or add new insights
3. Update the timestamp

### To Append Recent Session Changes:
Add a "Recent Changes" subsection at the bottom of each pattern or add a new "Session Updates" section at the bottom of the file with date/description of what was learned.

---

*This file is maintained alongside the codebase. For best results, review and update at the start of each new development session.*