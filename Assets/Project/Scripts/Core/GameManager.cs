using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Central game hub. Tracks collected items, triggers the win screen,
/// and handles the player being caught. All other scripts talk to this.
///
/// Uses the Singleton pattern so any script can call GameManager.Instance
/// without needing a dragged reference in the Inspector.
/// </summary>
public class GameManager : MonoBehaviour
{
    // ── Singleton ──────────────────────────────────────────────────────────

    public static GameManager Instance { get; private set; }

    // ── Inspector Settings ─────────────────────────────────────────────────

    [Header("Items")]
    [SerializeField] private int totalItemsRequired = 3;

    [Header("Scene Names")]
    [SerializeField] private string mainMenuScene = "MainMenu";

    [Header("UI Panels")]
    [SerializeField] private GameObject winPanel;
    [SerializeField] private GameObject losePanel;

    // ── Private State ──────────────────────────────────────────────────────

    private int  _itemsCollected = 0;
    private bool _gameOver       = false;

    // ── Unity Lifecycle ────────────────────────────────────────────────────

    private void Awake()
    {
        // Enforce a single instance; destroy any duplicate
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        // Make sure UI panels are hidden at game start
        if (winPanel  != null) winPanel.SetActive(false);
        if (losePanel != null) losePanel.SetActive(false);

        // Ensure the cursor is locked when gameplay begins
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
    }

    // ── Public API ─────────────────────────────────────────────────────────

    /// <summary>
    /// Called by ItemPickup when the player collects an item.
    /// Returns true if all items are now collected.
    /// </summary>
    public bool OnItemCollected()
    {
        if (_gameOver) return false;

        _itemsCollected++;
        Debug.Log($"Items collected: {_itemsCollected} / {totalItemsRequired}");

        return _itemsCollected >= totalItemsRequired;
    }

    /// <summary>Called by DoorController when the player escapes.</summary>
    public void OnPlayerEscaped()
    {
        if (_gameOver) return;
        _gameOver = true;

        Debug.Log("Player escaped! You win.");
        ShowWin();
    }

    /// <summary>Called by the AI when it catches the player.</summary>
    public void OnPlayerCaught()
    {
        if (_gameOver) return;
        _gameOver = true;

        Debug.Log("Player caught! Game over.");
        ShowLose();
    }

    /// <summary>How many items the player still needs to find.</summary>
    public int ItemsRemaining => totalItemsRequired - _itemsCollected;

    /// <summary>True once all required items have been picked up.</summary>
    public bool AllItemsCollected => _itemsCollected >= totalItemsRequired;

    /// <summary>True if the game has ended (win or lose).</summary>
    public bool IsGameOver => _gameOver;

    // ── UI Helpers ─────────────────────────────────────────────────────────

    private void ShowWin()
    {
        if (winPanel != null) winPanel.SetActive(true);

        // Disable the player controller so they can't move during the screen
        PlayerController player = FindFirstObjectByType<PlayerController>();
        if (player != null) player.SetInputEnabled(false);

        // Release cursor so they can click the UI button
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
    }

    private void ShowLose()
    {
        if (losePanel != null) losePanel.SetActive(true);

        PlayerController player = FindFirstObjectByType<PlayerController>();
        if (player != null) player.SetInputEnabled(false);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
    }

    // ── Button Callbacks (wire these to your UI buttons) ──────────────────

    /// <summary>Restart button on the lose screen.</summary>
    public void RestartGame()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    /// <summary>Main menu button on win or lose screen.</summary>
    public void GoToMainMenu()
    {
        SceneManager.LoadScene(mainMenuScene);
    }
}