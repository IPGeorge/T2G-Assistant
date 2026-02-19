using UnityEngine;

public class PrefsKeys
{
    //Game project
    public static readonly string k_GameProjectPath = "GameProjectPath";
    public static readonly string k_ProjectAssetRoot = "Assets";
    public static readonly string k_StartListener = "StartListener";
    public static readonly string k_IsReloadLaunch = "IsReloadLaunch";
    public static readonly string k_SessionId = "SessionId";

    public static readonly string k_InitRefreshAssetDatabase = "RefreshAssetData";
    public static readonly string k_InitOpenScene = "OpenScene";

    //Execution EditorPrefs keys
    public static readonly string k_Pending_AddonScript = "PendingAddonScript";
    public static readonly string k_Pending_NewPrefabObject = "NewPrefabObject";
    public static readonly string k_Pending_Arguments = "PendingArguments";
    public static readonly string k_Pending_ImportPackage = "ImportPackage";
    public static readonly string k_Pending_Controller = "PendingController";
    public static readonly string k_Pending_ControllerSetValues = "PendingControllerSetValues";

    //GameDesc key names
    public static readonly string k_GameDesc_CategoryKeyName = "Category";
    public static readonly string k_GameDesc_NameKey = "Name";
    public static readonly string k_GameDesc_GameWorldsKey = "GameWorlds";
    public static readonly string k_GameDesc_PositionKey = "Position";
    public static readonly string k_GameDesc_RotationKey = "Rotation";
    public static readonly string k_GameDesc_ScaleKey = "Scale";
    public static readonly string k_GameDesc_PrefabKey = "Prefab";
    public static readonly string k_GameDesc_PrefabControllerKey = "Controller";
    public static readonly string k_GameDesc_PrefabSetValuesKey = "SetControllerValues";
    public static readonly string k_GameDesc_PackagesKey = "Packages";
    public static readonly string k_GameDesc_AddonsKey = "Addons";
    public static readonly string k_GameDesc_AddonTypeKey = "AddonType";

    //GameDesc category names
    public static readonly string k_GameDescCategory = "GameDesc";
    public static readonly string k_GameWorldCategory = "GameWorld";
    public static readonly string k_WorldObjectCategory = "WorldObject";
    public static readonly string k_ObjectAddonCategory = "Addon";

    //GameDesc game generations
    public static readonly string k_EndOfGameGeneration = "END_OF_GAME_GENERATION";

    //Instruction
    public static readonly string k_InstructionExecutionHasResponseMessage = "InstructionExecutionHasResponseMessage";
    public static readonly string k_InstructionExecutionSucceeded = "InstructionExecutionSucceeded";
    public static readonly string k_InstructionExecutionResponseSucceeded = "InstructionExecutionResponseSucceeded";
    public static readonly string k_InstructionExecutionResponseFailed = "InstructionExecutionResponseFailed";

    //Game Generation
    public static readonly string k_SpacesDirectory = "Assets/Spaces";
    public static readonly string k_DefaultSpaceName = "PrimeSpace";


}
