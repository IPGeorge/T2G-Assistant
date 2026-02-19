using UnityEngine;

public class GameDescManager 
{
    private static GameDescManager _instance = null;

    public static GameDescManager Instance
    {
        get
        {
            if(_instance == null)
            {
                _instance = new GameDescManager();
            }
            return _instance;
        }
    }






}
