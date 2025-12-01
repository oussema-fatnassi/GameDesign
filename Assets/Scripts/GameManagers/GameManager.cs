using Unity.Netcode;
using UnityEngine;
using System;

public class GameManager : MonoBehaviour
{
    #region Fields
    [Header("Wave System")]
    [SerializeField] private WaveManager waveManager; // Référence au WaveManager

    private EClientType _clientType = EClientType.NONE;
    private bool _isGamePaused = false;
    private bool _isStarted = false;
    private bool _isGameOver = false;

    public event Action<bool> _isGameStartedEvent;
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
        set
        {
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
        // Toggle pause state with Escape key
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            _isGamePaused = !_isGamePaused;
            if (_isGamePaused)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }
    #endregion

    /// <summary>
    /// Method to start the game
    /// </summary>
    private void StartGame()
    {
        Debug.Log("Client Called StartGame: " + _clientType);
        _isGameStartedEvent?.Invoke(true);

        if (_clientType == EClientType.HOST)
        {
            Debug.Log("Game Started by Host");

            // Start wave system
            if (waveManager != null)
            {
                waveManager.StartWaveSystem();
            }
            else
            {
                Debug.LogError("WaveManager not assigned to GameManager!");
            }
        }
    }

    /// <summary>
    /// Public method to stop the game
    /// </summary>
    public void StopGame()
    {
        if (_clientType == EClientType.HOST && waveManager != null)
        {
            waveManager.StopWaveSystem();
        }

        _isGameOver = true;
    }

    /// <summary>
    /// Called when all waves are completed
    /// </summary>
    public void OnVictory()
    {
        Debug.Log("=== VICTORY! All waves completed! ===");
        _isGameOver = true;

        // Show victory screen, etc.
        // TODO: Implement victory UI
    }

    /// <summary>
    /// Called when players lose (optional)
    /// </summary>
    public void OnDefeat()
    {
        Debug.Log("=== DEFEAT! Objective destroyed! ===");
        _isGameOver = true;

        // Show defeat screen, etc.
        // TODO: Implement defeat UI
    }
}