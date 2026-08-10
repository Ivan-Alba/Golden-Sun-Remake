using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

// Enum for all rebindable actions in the game
public enum InputAction
{
    Run,
    Interact,
    Hero1,
    Hero2,
    Hero3,
    Psynergy1,
    Psynergy2,
    Psynergy3,
    Psynergy4,
    PsynergyUp,
    PsynergyDown,
    Aim,
    Menu
}

// Serializable class used for JSON export/import
[System.Serializable]
public class ControlBindingsData
{
    public string runKey = KeyCode.LeftShift.ToString();
    public string interactKey = KeyCode.E.ToString();
    public string hero1Key = KeyCode.F1.ToString();
    public string hero2Key = KeyCode.F2.ToString();
    public string hero3Key = KeyCode.F3.ToString();
    public string psynergy1Key = KeyCode.Alpha1.ToString();
    public string psynergy2Key = KeyCode.Alpha2.ToString();
    public string psynergy3Key = KeyCode.Alpha3.ToString();
    public string psynergy4Key = KeyCode.Alpha4.ToString();
    public string psynergyUpKey = KeyCode.UpArrow.ToString();
    public string psynergyDownKey = KeyCode.DownArrow.ToString();
    public string aimKey = KeyCode.Q.ToString();
    public string menuKey = KeyCode.Escape.ToString();
}

public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }

    public Vector2 MoveInput { get; private set; }
    public bool IsRunning { get; private set; }

    // --- EVENTS FOR DISCRETE ACTIONS ---
    public event Action<int> OnChangeHeroPressed;
    public event Action OnInteractPressed;
    public event Action OnToggleMenuPressed;
    public event Action OnStartAimPressed;
    public event Action OnStopAimPressed;
    public event Action<int> OnUsePsynergyPressed;
    public event Action<int, bool> OnHoldPsynergyBarChanged; // int: slotIndex, bool: isHolding
    public event Action OnPsynergyNavigateUpPressed;
    public event Action OnPsynergyNavigateDownPressed;

    // Internal Dictionary for fast lookup
    private Dictionary<InputAction, KeyCode> keyBindings = new Dictionary<InputAction, KeyCode>();
    private bool[] psynergyKeyHeld = new bool[4];
    private string jsonFilePath;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        jsonFilePath = Path.Combine(Application.persistentDataPath, "controls.json");
        LoadControlBindings();
    }

    private void Update()
    {
        //Debug.Log(GameManager.Instance.CurrentState.ToString());
        if (GameManager.Instance.CurrentState != GameManager.GameState.WorldExploration)
            return;

        if (GameManager.Instance != null && GameManager.Instance.LoadingData)
        {
            MoveInput = Vector2.zero;
            IsRunning = false;
            return;
        }

        HandleMovementInput();
        HandleHeroSelectionInput();
        HandleInteractionInput();
        HandleAimInput();
        HandleMenuInput();
        HandlePsynergyInput();
    }

    #region Input Processing

    private void HandleMovementInput()
    {
        float x = Input.GetAxisRaw("Horizontal");
        float z = Input.GetAxisRaw("Vertical");
        MoveInput = new Vector2(x, z).normalized;

        IsRunning = Input.GetKey(keyBindings[InputAction.Run]);
    }

    private void HandleHeroSelectionInput()
    {
        if (Input.GetKeyDown(keyBindings[InputAction.Hero1])) OnChangeHeroPressed?.Invoke(1);
        if (Input.GetKeyDown(keyBindings[InputAction.Hero2])) OnChangeHeroPressed?.Invoke(2);
        if (Input.GetKeyDown(keyBindings[InputAction.Hero3])) OnChangeHeroPressed?.Invoke(3);
    }

    private void HandleInteractionInput()
    {
        if (Input.GetKeyDown(keyBindings[InputAction.Interact]))
        {
            OnInteractPressed?.Invoke();
        }
    }

    private void HandleAimInput()
    {
        if (Input.GetKeyDown(keyBindings[InputAction.Aim])) OnStartAimPressed?.Invoke();
        if (Input.GetKeyUp(keyBindings[InputAction.Aim])) OnStopAimPressed?.Invoke();
    }

    private void HandleMenuInput()
    {
        if (Input.GetKeyDown(keyBindings[InputAction.Menu]))
        {
            OnToggleMenuPressed?.Invoke();
        }
    }

    private void HandlePsynergyInput()
    {
        InputAction[] psynergyActions = { InputAction.Psynergy1, InputAction.Psynergy2, InputAction.Psynergy3, InputAction.Psynergy4 };

        for (int i = 0; i < psynergyActions.Length; i++)
        {
            KeyCode code = keyBindings[psynergyActions[i]];

            if (Input.GetKeyDown(code))
            {
                OnUsePsynergyPressed?.Invoke(i);
            }
            else if (Input.GetKey(code) && !psynergyKeyHeld[i])
            {
                psynergyKeyHeld[i] = true;
                OnHoldPsynergyBarChanged?.Invoke(i, true);
            }
            else if (Input.GetKeyUp(code) && psynergyKeyHeld[i])
            {
                psynergyKeyHeld[i] = false;
                OnHoldPsynergyBarChanged?.Invoke(i, false);
            }
        }

        if (Input.GetKeyDown(keyBindings[InputAction.PsynergyUp])) OnPsynergyNavigateUpPressed?.Invoke();
        if (Input.GetKeyDown(keyBindings[InputAction.PsynergyDown])) OnPsynergyNavigateDownPressed?.Invoke();
    }

    #endregion

    #region JSON & Binding Management

    // Loads bindings from controls.json or creates default file if missing
    public void LoadControlBindings()
    {
        ControlBindingsData data;

        if (File.Exists(jsonFilePath))
        {
            string json = File.ReadAllText(jsonFilePath);
            data = JsonUtility.FromJson<ControlBindingsData>(json);
        }
        else
        {
            data = new ControlBindingsData(); // Generates default keys
            SaveControlBindingsData(data);
        }

        PopulateDictionaryFromData(data);
    }

    // Saves current dictionary back into the JSON file
    public void SaveCurrentBindings()
    {
        ControlBindingsData data = ConvertDictionaryToData();
        SaveControlBindingsData(data);
    }

    private void SaveControlBindingsData(ControlBindingsData data)
    {
        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(jsonFilePath, json);
    }

    private void PopulateDictionaryFromData(ControlBindingsData data)
    {
        keyBindings[InputAction.Run] = ParseKey(data.runKey, KeyCode.LeftShift);
        keyBindings[InputAction.Interact] = ParseKey(data.interactKey, KeyCode.E);
        keyBindings[InputAction.Hero1] = ParseKey(data.hero1Key, KeyCode.F1);
        keyBindings[InputAction.Hero2] = ParseKey(data.hero2Key, KeyCode.F2);
        keyBindings[InputAction.Hero3] = ParseKey(data.hero3Key, KeyCode.F3);
        keyBindings[InputAction.Psynergy1] = ParseKey(data.psynergy1Key, KeyCode.Alpha1);
        keyBindings[InputAction.Psynergy2] = ParseKey(data.psynergy2Key, KeyCode.Alpha2);
        keyBindings[InputAction.Psynergy3] = ParseKey(data.psynergy3Key, KeyCode.Alpha3);
        keyBindings[InputAction.Psynergy4] = ParseKey(data.psynergy4Key, KeyCode.Alpha4);
        keyBindings[InputAction.PsynergyUp] = ParseKey(data.psynergyUpKey, KeyCode.UpArrow);
        keyBindings[InputAction.PsynergyDown] = ParseKey(data.psynergyDownKey, KeyCode.DownArrow);
        keyBindings[InputAction.Aim] = ParseKey(data.aimKey, KeyCode.Q);
        keyBindings[InputAction.Menu] = ParseKey(data.menuKey, KeyCode.Escape);
    }

    private ControlBindingsData ConvertDictionaryToData()
    {
        ControlBindingsData data = new ControlBindingsData();
        data.runKey = keyBindings[InputAction.Run].ToString();
        data.interactKey = keyBindings[InputAction.Interact].ToString();
        data.hero1Key = keyBindings[InputAction.Hero1].ToString();
        data.hero2Key = keyBindings[InputAction.Hero2].ToString();
        data.hero3Key = keyBindings[InputAction.Hero3].ToString();
        data.psynergy1Key = keyBindings[InputAction.Psynergy1].ToString();
        data.psynergy2Key = keyBindings[InputAction.Psynergy2].ToString();
        data.psynergy3Key = keyBindings[InputAction.Psynergy3].ToString();
        data.psynergy4Key = keyBindings[InputAction.Psynergy4].ToString();
        data.psynergyUpKey = keyBindings[InputAction.PsynergyUp].ToString();
        data.psynergyDownKey = keyBindings[InputAction.PsynergyDown].ToString();
        data.aimKey = keyBindings[InputAction.Aim].ToString();
        data.menuKey = keyBindings[InputAction.Menu].ToString();
        return data;
    }

    private KeyCode ParseKey(string keyString, KeyCode fallback)
    {
        if (Enum.TryParse(keyString, out KeyCode parsedKey))
        {
            return parsedKey;
        }
        return fallback;
    }

    // Public method to rebind a key at runtime (for future UI Options Menu)
    public void RebindKey(InputAction action, KeyCode newKey)
    {
        keyBindings[action] = newKey;
        SaveCurrentBindings();
    }

    // Public method to fetch current KeyCode for display in UI
    public KeyCode GetMappedKey(InputAction action)
    {
        return keyBindings.ContainsKey(action) ? keyBindings[action] : KeyCode.None;
    }

    #endregion
}