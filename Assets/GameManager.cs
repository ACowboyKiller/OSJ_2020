using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameManager : MonoBehaviour
{

    #region --------------------    Public Enumerations

    /// <summary>
    /// The enum used to determine the script type
    /// </summary>
    public enum ObjType { Manager, Cube };

    /// <summary>
    /// The enum used to determine the difficulty of the game
    /// </summary>
    public enum Difficulty { Easy = 1, Medium = 2, Hard = 3 };

    #endregion

    #region --------------------    Public Properties

    /// <summary>
    /// The singleton instance for the class
    /// </summary>
    public static GameManager instance { get; private set; } = null;

    /// <summary>
    /// The registry of all cubes
    /// </summary>
    public static List<GameManager> cubes { get; private set; } = new List<GameManager>();

    /// <summary>
    /// The dictionary used to store grid status
    /// </summary>
    public static Dictionary<Vector3, GameManager> grid { get; private set; } = new Dictionary<Vector3, GameManager>();

    /// <summary>
    /// Returns whether or not the game has started
    /// </summary>
    public static bool isPlaying { get; private set; } = false;

    /// <summary>
    /// Counts the number of cleared cells
    /// </summary>
    public static int clearedCount { get; private set; } = 0;

    /// <summary>
    /// The number of cleared cells to win
    /// </summary>
    public static int goal { get; private set; } = 0;

    /// <summary>
    /// Returns whether or not the round has been won
    /// </summary>
    public static bool isWon => clearedCount == goal;

    /// <summary>
    /// Returns the ratio needed to calculate trigger counts
    /// </summary>
    public static Dictionary<Difficulty, float> triggerRatios { get; private set; } = new Dictionary<Difficulty, float>()
    {
        { Difficulty.Easy, 0.18f },
        { Difficulty.Medium, 0.22f },
        { Difficulty.Hard, 0.25f }
    };

    /// <summary>
    /// Used for performing lerps on update
    /// </summary>
    public delegate void LerpEvent();
    public LerpEvent OnLerpsTickEvent { get; set; } = null;

    /// <summary>
    /// Returns whether or not the object is a manager
    /// </summary>
    public bool isManager => _type == ObjType.Manager;

    /// <summary>
    /// Returns whether or not each individual cube is a trigger
    /// </summary>
    public bool isTrigger { get; private set; } = false;

    /// <summary>
    /// Stores the number of nearby triggers for each individual cube
    /// </summary>
    public int nearbyCount { get; set; } = 0;

    /// <summary>
    /// Stores whether or not the cube is flagged
    /// </summary>
    public bool isFlagged { get; private set; } = false;

    #endregion

    #region --------------------    Public Methods

    /// <summary>
    /// Sets the difficulty for the game
    /// </summary>
    /// <param name="_pDifficulty"></param>
    public void SetDifficulty(int _pDifficulty) => _gameDifficulty = (Difficulty)_pDifficulty;

    /// <summary>
    /// Starts the game round
    /// </summary>
    public void StartRound() 
    {
        _roundDuration = 0f;
        clearedCount = 0;
        _ConfigureGrid();
        instance.OnLerpsTickEvent += _FadeOutMenu;
    }

    /// <summary>
    /// Clears out the cube
    /// </summary>
    public void Clear()
    {
        _collider.enabled = false;
        _isClicked = true;
        nearbyCount = _CountNearby();
        _text.text = (nearbyCount > 0) ? nearbyCount.ToString() : "";
        GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        instance.OnLerpsTickEvent += _FadeOutCube;
        clearedCount++;
        if (isWon) instance.WinRound();
    }

    /// <summary>
    /// Flags the cube
    /// </summary>
    public void ToggleFlag()
    {
        isFlagged = !isFlagged;
        _material.SetColor("_Color", (isFlagged) ? Color.red : _defaultColor);
    }

    /// <summary>
    /// Fires the trigger & ends the round
    /// </summary>
    public void LoseRound()
    {
        instance.OnLerpsTickEvent -= _RoundClock;
        Debug.Log("You lose");
        _meltdown.Play();
        ResetGame();
        //  TODO:   Perform some cool animation
    }

    /// <summary>
    /// Fired whenever the player has won the round
    /// </summary>
    public void WinRound()
    {
        instance.OnLerpsTickEvent -= _RoundClock;
        Debug.Log("You Win");
        ResetGame();
        //  TODO:   Play some animation
        //  TODO:   Update leaderboard
    }

    /// <summary>
    /// Resets the game & brings up the main menu
    /// </summary>
    public void ResetGame()
    {
        isPlaying = false;
        instance.OnLerpsTickEvent += _FadeInMenu;
    }

    #endregion

    #region --------------------    Private Fields

    [Header("Script Configurations")]
    /// <summary>
    /// The type of the script
    /// </summary>
    [SerializeField] private ObjType _type = ObjType.Manager;


    [Header("Manager Configurations")]
    /// <summary>
    /// The cube prefabs for spawning
    /// </summary>
    [SerializeField] private GameObject _cubePrefab = null;

    /// <summary>
    /// The main camera
    /// </summary>
    [SerializeField] private Transform _cameraDolly = null;

    /// <summary>
    /// The canvas group associated with the menu
    /// </summary>
    [SerializeField] private CanvasGroup _canvasGroup = null;

    /// <summary>
    /// The duration label for the game-play
    /// </summary>
    [SerializeField] private TMP_Text _durationLabel = null;

    /// <summary>
    /// The meltdown particles
    /// </summary>
    [SerializeField] private ParticleSystem _meltdown = null;

    /// <summary>
    /// The difficulty of the game
    /// </summary>
    [SerializeField] private Difficulty _gameDifficulty = Difficulty.Easy;

    /// <summary>
    /// How long the player has been playing the round
    /// </summary>
    [SerializeField] private float _roundDuration = 0f;

    /// <summary>
    /// The material for coloring each individual cube
    /// </summary>
    private Material _material = null;

    /// <summary>
    /// The default color for all cubes
    /// </summary>
    private Color _defaultColor = Color.white;

    /// <summary>
    /// The collider for each individual cube
    /// </summary>
    private Collider _collider = null;

    /// <summary>
    /// Whether or not each individual cube has been clicked
    /// </summary>
    private bool _isClicked = false;

    /// <summary>
    /// The text field associated with each individual cube
    /// </summary>
    private TMP_Text _text = null;

    #endregion

    #region --------------------    Private Methods

    private void Awake()
    {
        _ManagerSetup();
        _CubeSetup();
    }

    /// <summary>
    /// Performs the setup for the manager
    /// </summary>
    private void _ManagerSetup()
    {
        if (_type != ObjType.Manager) return;
        instance = this;
    }

    /// <summary>
    /// Performs the setup for the cubes
    /// </summary>
    private void _CubeSetup()
    {
        if (_type != ObjType.Cube) return;
        cubes.Add(this);
        _material = GetComponent<MeshRenderer>().material;
        _defaultColor = _material.GetColor("_Color");
        _collider = GetComponent<Collider>();
        _text = GetComponentInChildren<TMP_Text>();
    }

    /// <summary>
    /// Rotates the camera dolly and performs lerp events
    /// </summary>
    private void Update()
    {
        if (!isManager) return; 
        OnLerpsTickEvent?.Invoke();
        float _y = (Input.GetKey(KeyCode.A)) ? -1f : ((Input.GetKey(KeyCode.D)) ? 1f : 0f);
        float _x = (Input.GetKey(KeyCode.W)) ? -1f : ((Input.GetKey(KeyCode.S)) ? 1f : 0f);
        float _z = (Input.GetKey(KeyCode.Q)) ? -1f : ((Input.GetKey(KeyCode.E)) ? 1f : 0f);
        _cameraDolly.Rotate(new Vector3(_x, _y, _z));
    }

    /// <summary>
    /// Rotates the text labels in the cubes to face the camera
    /// </summary>
    private void LateUpdate()
    {
        if (isManager || !_isClicked) return;
        _text.transform.localRotation = instance._cameraDolly.localRotation;
    }

    /// <summary>
    /// Configures the grid by spawning cubes and distributing triggers
    /// </summary>
    private void _ConfigureGrid()
    {
        /// Reset Game Grid
        grid.Clear();

        /// Deactivate all cells
        cubes.ForEach(c => c.gameObject.SetActive(false));

        /// Calculate the number of triggers to add
        int _triggerCount = Mathf.FloorToInt(Mathf.Pow(((int)_gameDifficulty * 2) + 1, 3) * triggerRatios[_gameDifficulty]);
        List<Vector3> _triggers = new List<Vector3>();

        /// Create the layers of the grid
        for (int i = 1; i <= (int)_gameDifficulty; i ++)
        {
            for (int x = -i; x <= i; x ++)
            {
                for (int y = -i; y <= i; y++)
                {
                    for (int z = -i; z <= i; z++)
                    {
                        Vector3 _cell = new Vector3(x, y, z);
                        if (_cell != Vector3.zero && !grid.ContainsKey(_cell))
                        {
                            _triggers.Add(_cell);
                            GameObject _obj = _cubePrefab.PoolInstantiate();
                            GameManager _mgr = _obj.GetComponent<GameManager>();
                            grid.Add(_cell, _mgr);
                            _obj.transform.SetParent(transform);
                            _obj.transform.localPosition = _cell;
                            _obj.SetActive(true);
                            _obj.GetComponent<Collider>().enabled = true;
                            _obj.GetComponent<MeshRenderer>().material.SetColor("_Color", _mgr._defaultColor);
                            _obj.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                            _mgr._text.text = "";
                            _mgr.isTrigger = false;
                            _mgr.isFlagged = false;
                            _mgr._isClicked = false;
                        }
                    }
                }
            }
        }

        /// Set the goal for the game
        goal = _triggers.Count - _triggerCount;

        /// Add triggers to the grid
        for (int i = 0; i < _triggerCount; i ++)
        {
            Vector3 _target = _triggers[Random.Range(0, _triggers.Count)];
            grid[_target].isTrigger = true;
            _triggers.Remove(_target);
        }
    }

    /// <summary>
    /// The click event for the grid cells
    /// </summary>
    private void OnMouseOver()
    {
        if (isManager || !isPlaying) return;
        if (Input.GetMouseButtonDown(0))
        {
            if (!isTrigger)
            {
                Clear();
            }
            else
            {
                instance.LoseRound();
            }
        }
        if (Input.GetMouseButtonDown(1))
        {
            ToggleFlag();
        }
    }

    /// <summary>
    /// Counts the nearby number of triggers and returns the value
    /// </summary>
    /// <returns></returns>
    private int _CountNearby()
    {
        Vector3 _pos = new Vector3(Mathf.RoundToInt(transform.localPosition.x), Mathf.RoundToInt(transform.localPosition.y), Mathf.RoundToInt(transform.localPosition.z));
        int _count = 0;
        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                for (int z = -1; z <= 1; z++)
                {
                    Vector3 _cell = new Vector3(x, y, z) + _pos;
                    if (_cell - _pos != Vector3.zero && grid.ContainsKey(_cell))
                    {
                        if (grid[_cell].isTrigger)
                        {
                            _count++;
                        }
                        else
                        {
                            if (!grid[_cell]._isClicked && Random.Range(0f, 1f) < 0.2f)
                            {
                                grid[_cell].Clear();
                            }
                        }
                    }
                }
            }
        }
        return _count;
    }

    /// <summary>
    /// Fades out the canvas group
    /// </summary>
    private void _FadeOutMenu()
    {
        _canvasGroup.blocksRaycasts = false;
        _canvasGroup.interactable = false;
        _canvasGroup.alpha -= (Time.deltaTime * 2f);
        if (_canvasGroup.alpha <= 0f)
        {
            isPlaying = true;
            instance.OnLerpsTickEvent -= _FadeOutMenu;
            instance.OnLerpsTickEvent += _RoundClock;
        }
    }

    /// <summary>
    /// Fades in the canvas group
    /// </summary>
    private void _FadeInMenu()
    {
        _canvasGroup.alpha += (Time.deltaTime * 2f);
        if (_canvasGroup.alpha >= 1f)
        {
            _canvasGroup.blocksRaycasts = true;
            _canvasGroup.interactable = true;
            instance.OnLerpsTickEvent -= _FadeInMenu;
        }
    }

    /// <summary>
    /// Fades out the cube
    /// </summary>
    private void _FadeOutCube()
    {
        Color _col = _material.GetColor("_Color");
        float _alpha = Mathf.Clamp(_col.a - (Time.deltaTime * 2f), (nearbyCount == 0) ? 0f : 0.05f, 1f);
        _material.SetColor("_Color", new Color(_col.r, _col.g, _col.b, _alpha));
        if (_alpha <= ((nearbyCount == 0)? 0f : 0.05f))
        {
            instance.OnLerpsTickEvent -= _FadeOutCube;
        }
    }

    /// <summary>
    /// Increments the round duration
    /// </summary>
    private void _RoundClock()
    {
        _roundDuration += Time.deltaTime;
        instance._durationLabel.text = Mathf.FloorToInt(_roundDuration).ToString();
    }

    #endregion

}

/// <summary>
/// Used as the pool storage for all game objects in the pool
/// </summary>
public class GameObjectPool
{

    #region --------------------    Public Properties

    /// <summary>
    /// The list of all pooled gameobjects in the current scene.
    /// </summary>
    public static List<GameObject> pool { get; private set; } = new List<GameObject>();

    #endregion

}

/// <summary>
/// The pooling extension method for pooled objects
/// </summary>
public static class GameObjectPoolerExtensions
{

    #region --------------------    Public Methods

    /// <summary>
    /// Instantiates a copy of the provided prefab and returns the clone as inactive.
    /// NOTE: This will disable & re-enable the source gameobject!
    /// </summary>
    /// <param name="gameObjectToClone">The prefab or gameobject to clone.</param>
    public static GameObject PoolInstantiate(this GameObject gameObjectToClone)
    {
        GameObjectPool.pool.RemoveAll(go => go == null);
        var clonedGameObject = GameObjectPool.pool.Find(go => !go.activeSelf && go.name == gameObjectToClone.name);

        if (clonedGameObject != null) return clonedGameObject;

        var isActive = gameObjectToClone.activeSelf;
        gameObjectToClone.SetActive(false);
        clonedGameObject = Object.Instantiate(gameObjectToClone);
        clonedGameObject.name = gameObjectToClone.name;
        gameObjectToClone.SetActive(isActive);
        GameObjectPool.pool.Add(clonedGameObject);
        return clonedGameObject;
    }

    #endregion

}