using UnityEngine;

public class GameManager : MonoBehaviour
{
    #region Fields
    private EClientType  _clientType = EClientType.NONE;
    private bool _isGamePaused = false;
    private bool _isStarted = false;
    private bool _isGameOver = false;
    #endregion

    #region Properties
    public EClientType ClientType
    {
        get { return _clientType; }
        set { _clientType = value; }
    }

    public bool IsStarted
    {
        get { return _isStarted; }
        set { 
            _isStarted = value;
            if (_isStarted) StartGame();
        }
    }
    #endregion

    #region Singleton Pattern
    public static GameManager Instance { get; private set; }

    private void Awake()
    {
        // Ensure only one instance of GameManager exists
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Persist across scenes
        }
        else
        {
            Destroy(gameObject); // Destroy duplicate
        }
    }

    private void Update()
    {
        // Example: Toggle pause state with Escape key
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            _isGamePaused = !_isGamePaused;
            if (_isGamePaused)
            {
                Time.timeScale = 0f; // Pause game
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Time.timeScale = 1f; // Resume game
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }
    #endregion

    // Method to start the game
    private void StartGame()
    {
        Debug.LogError("Client Called StartGame: " + _clientType);
        if (_clientType == EClientType.HOST)
        {
            // Lock cursor
            //TODO : Change this later to be dynamic based on game state, GameManager?
            

            Debug.Log("Game Started by Host");
            // hERE LOGIC RELATED TO WAVEMANAGER ETC
        }
    }
}
