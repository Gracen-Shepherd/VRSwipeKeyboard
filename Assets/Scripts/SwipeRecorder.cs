using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using System.Threading.Tasks;

public class SwipeRecorder : MonoBehaviour
{
    private DollarRecognizer _recognizer;
    private Dictionary<char, Vector2> _keys;
    private List<Vector2> _points = new List<Vector2>();

    public string[] commonWords { get; private set; }

    [Header("Settings")]
    [Tooltip("Minimum pixels the mouse must move before recording a new point. Prevents noise and bad anchor letters.")]
    [SerializeField] private float movementThreshold = 1f;
    [SerializeField] private int minPointCount = 4;

    [Header("Meta Hand Tracking")] 
    [SerializeField] private OVRSkeleton leftSkeleton;
    [SerializeField] private OVRSkeleton rightSkeleton;

    [Header("Physics")] 
    [SerializeField] private LayerMask keyboardLayer;

    [Header("Reference Plane")] 
    [Tooltip("The transform of your invisible KeyboardPlane prefab.")] 
    [SerializeField] private Transform keyboardPlane;

    [Header("Output TMP")] 
    [Tooltip("The TextMeshPro used to display the output word.")] 
    [SerializeField] private TextMeshPro outputText;

    [Header("Visual Effects")] 
    [SerializeField] private TrailRenderer swipeTrail;
    [SerializeField] private Color startColor;
    [SerializeField] private Color hoverColor;

    [Header("Debug")] 
    [Tooltip("Something to stick to the right hand tip, for debug purposes.")] 
    [SerializeField] private GameObject rightHandMarker;
    [SerializeField] private TextMeshPro debugText;
    [SerializeField] private TextMeshPro statusText;

    private bool _handJustDown = false;
    private bool _handJustUp = false;
    private bool _handDown = false;
    private bool _leftHandBool = false;
    private Transform _leftIndexTip;
    private Transform _rightIndexTip;
    private bool _isInitialized = false;

    private GameObject _startLetter;
    private GameObject _endLetter;
    private GameObject _hoveredLetter;
    private List<char> _anchorLetters = new List<char>();
    private static readonly float _bigLog = Mathf.Log(200000f);

    // --- STATE VARIABLES ---
    private bool _isShiftActive = false;
    private GameObject _shiftKeyObj = null;
    private bool _isCapsActive = false;
    private GameObject _capsKeyObj = null;
    
    // --- CARET SYSTEM ---
    private string _rawInputText = ""; // Holds the actual underlying characters
    private bool _isCaretVisible = true;
    private const char CARET_CHAR = '|'; // The caret character style
    private float _nextBlinkTime = 0f;

    void Awake()
    {
        LoadWordsFromResources();
    }

    void Start()
    {
        _recognizer = new DollarRecognizer();
        _keys = KeyboardLayout.Keys;

        StartCoroutine(InitializeSkeletons());
        StartCoroutine(BlinkCaretRoutine());

        // Pre-cache Shift and Caps keys if they exist in the scene to guarantee visual toggles work immediately
        KeyManager[] allKeys = FindObjectsByType<KeyManager>();
        foreach (KeyManager km in allKeys)
        {
            if (km.gameObject.name.Equals("Shift", StringComparison.OrdinalIgnoreCase))
                _shiftKeyObj = km.gameObject;
            else if (km.gameObject.name.Equals("Caps", StringComparison.OrdinalIgnoreCase) || km.gameObject.name.Equals("CAPS", StringComparison.OrdinalIgnoreCase))
                _capsKeyObj = km.gameObject;
        }
        

        foreach (var word in commonWords)
        {
            List<Vector2> points = new List<Vector2>();
            Vector2 lastAddedPosition = new Vector2(-999f, -999f); 

            string lowerWord = word.ToLower();

            foreach (char letter in lowerWord)
            {
                if (_keys.TryGetValue(letter, out Vector2 keyPosition))
                {
                    if (points.Count == 0 || keyPosition != lastAddedPosition)
                    {
                        points.Add(keyPosition);
                        lastAddedPosition = keyPosition; 
                    }
                }
            }

            if (points.Count > 1)
            {
                _recognizer.SavePattern(word, points);
            }
        }

        Debug.Log($"Successfully saved {_recognizer._library.Count} words into memory.");
        _rawInputText = ""; 
    }

    void Update()
    {
        try
        {
            if (!_isInitialized) return;

            // Handle handDown logic
            bool leftInvalid = (_leftIndexTip.position == Vector3.zero);
            bool rightInvalid = (_rightIndexTip.position == Vector3.zero);
            
            Vector3 leftPoint = keyboardPlane.InverseTransformPoint(_leftIndexTip.position);
            Vector3 rightPoint = keyboardPlane.InverseTransformPoint(_rightIndexTip.position);
            
            if (Mathf.Abs(leftPoint.z) >= 10f) leftInvalid = true;
            if (Mathf.Abs(leftPoint.x) >= 4f) leftInvalid = true;
            if (Mathf.Abs(rightPoint.z) >= 10f) rightInvalid = true;
            if (Mathf.Abs(rightPoint.x) >= 4f) rightInvalid = true;
            
            if (_leftHandBool && _handDown && leftInvalid) return; 
            if (!_leftHandBool && _handDown && rightInvalid) return;

            float localHeightLeft = keyboardPlane.InverseTransformPoint(_leftIndexTip.position).y;
            float localHeightRight = keyboardPlane.InverseTransformPoint(_rightIndexTip.position).y;

            if (_handDown)
            {
                if ((_leftHandBool ? localHeightLeft : localHeightRight) > 0.15f) 
                {
                    _handJustUp = true;
                    _handDown = false;
                }
            }
            else if (localHeightRight < 0 && !rightInvalid) 
            {
                _handDown = true;
                _handJustDown = true;
                _leftHandBool = false;
            }
            else if (localHeightLeft < 0 && !leftInvalid) 
            {
                _handDown = true;
                _handJustDown = true;
                _leftHandBool = true;
            }

            // Handle keyboard + trail logic
            if (_handJustDown) 
            {
                _handJustDown = false; 

                _points.Clear(); 
                Vector2 initialPosition = GetMousePosition();
                _points.Add(initialPosition); 
                _anchorLetters.Clear();
                
                _startLetter = GetHoveredKey(_leftHandBool);
                _hoveredLetter = _startLetter;

                if (_startLetter != null)
                {
                    var keyManager = _startLetter.GetComponent<KeyManager>();
                    if (keyManager != null) keyManager.SetColor(startColor);
                }

                swipeTrail.enabled = false;
                swipeTrail.transform.position = Project2DToSurface(initialPosition);
                swipeTrail.Clear();
                swipeTrail.enabled = true;
            }
            else if (_handDown) 
            {
                Vector2 currentPos = GetMousePosition();
                swipeTrail.transform.position = Project2DToSurface(currentPos);

                GameObject currentHover = GetHoveredKey(_leftHandBool);
                if (_hoveredLetter != currentHover)
                {
                    if (_hoveredLetter != null && _hoveredLetter != _startLetter)
                    {
                        ResetKeyColorSafely(_hoveredLetter);
                    }
                    
                    _hoveredLetter = currentHover;
                    
                    if (_hoveredLetter != null && _hoveredLetter != _startLetter)
                    {
                        var keyManager = _hoveredLetter.GetComponent<KeyManager>();
                        if (keyManager != null) keyManager.SetColor(hoverColor);
                    }
                }
                
                if (Vector2.Distance(currentPos, _points[_points.Count - 1]) > movementThreshold) 
                {
                    _points.Add(currentPos); 

                    if (_points.Count >= 3)
                    {
                        Vector2 vA = (_points[_points.Count - 2] - _points[_points.Count - 3]).normalized;
                        Vector2 vB = (currentPos - _points[_points.Count - 2]).normalized;

                        if (Vector2.Dot(vA, vB) < -0.5f && _hoveredLetter != null)
                        {
                            if (!string.IsNullOrEmpty(_hoveredLetter.name) && _hoveredLetter.name.Length == 1)
                            {
                                _anchorLetters.Add(_hoveredLetter.name.ToLower()[0]);
                            }
                        }
                    }
                }
            }

            if (_handJustUp)
            {
                _handJustUp = false;
                swipeTrail.enabled = false;

                ResetKeyColorSafely(_hoveredLetter);
                ResetKeyColorSafely(_startLetter);

                if (_startLetter == null || _hoveredLetter == null) return;
                
                _endLetter = GetHoveredKey(_leftHandBool);
                if (_endLetter == null) return;

                // --- ROUTING: Single Tap vs Swipe ---
                if (_points.Count < minPointCount)
                {
                    ProcessSingleKeyTap(_endLetter);
                    return;
                }

                // --- ASYNC TRIGGER ---
                List<Vector2> pointsSnapshot = new List<Vector2>(_points);
                List<char> anchorsSnapshot = new List<char>(_anchorLetters);
                char startChar = _startLetter.name.ToLower()[0];
                char endChar = _endLetter.name.ToLower()[0];
                string startName = _startLetter.name;
                string endName = _endLetter.name;

                ProcessSwipeAsync(pointsSnapshot, startChar, endChar, anchorsSnapshot, startName, endName);
            }
        }
        catch (Exception e)
        {
            if(debugText != null) debugText.text = e.ToString();
        }
    }

    // --- STRUCTURAL KEYBOARD LOGIC ---

    private void ResetKeyColorSafely(GameObject keyObj)
    {
        if (keyObj == null) return;

        // Prevent structural modifier keys from losing color on un-hover while actively engaged
        if (keyObj == _shiftKeyObj && _isShiftActive) return;
        if (keyObj == _capsKeyObj && _isCapsActive) return;

        var keyManager = keyObj.GetComponent<KeyManager>();
        if (keyManager != null) keyManager.ResetColor();
    }
    
    private void ProcessSingleKeyTap(GameObject keyObj)
    {
        if (keyObj == null) return;
        string keyName = keyObj.name;

        // 1. Modifiers
        if (keyName.Equals("Shift", StringComparison.OrdinalIgnoreCase))
        {
            ToggleShift(keyObj);
            return;
        }
        if (keyName.Equals("Caps", StringComparison.OrdinalIgnoreCase) || keyName.Equals("CAPS", StringComparison.OrdinalIgnoreCase))
        {
            ToggleCaps(keyObj);
            return;
        }

        // 2. Backspace and Word Deletion
        if (keyName.Equals("Back", StringComparison.OrdinalIgnoreCase))
        {
            HandleBackspace();
            return;
        }
        if (keyName.Equals("DEL WORD", StringComparison.OrdinalIgnoreCase))
        {
            HandleDeleteWord();
            return;
        }

        // 3. Layout Triggers
        if (keyName.Equals("Enter", StringComparison.OrdinalIgnoreCase))
        {
            ConsumeShift();
            AppendText("\n");
            return;
        }
        if (keyName.Equals("Tab", StringComparison.OrdinalIgnoreCase))
        {
            ConsumeShift();
            AppendText("\t");
            return;
        }

        // 4. Dual-Character Keys (e.g., "?\n/" or "?\\n/")
        if (keyName.Contains("\n") || keyName.Contains("\\n"))
        {
            string[] parts = keyName.Split(new string[] { "\n", "\\n" }, StringSplitOptions.None);
            if (parts.Length >= 2)
            {
                string charToAdd = _isShiftActive ? parts[0] : parts[1];
                ConsumeShift(); 
                InsertText(charToAdd, false);
                return;
            }
        }

        // 5. Standard Keys (Single Characters)
        bool applyUpper = _isShiftActive ^ _isCapsActive; 
        string letterToAdd = applyUpper ? keyName.ToUpper() : keyName.ToLower();
        
        if (letterToAdd.Length > 1) 
        {
            letterToAdd = letterToAdd.Substring(0, 1);
        }

        ConsumeShift();
        InsertText(letterToAdd, false);
    }

    private void ToggleShift(GameObject shiftKey)
    {
        _isShiftActive = !_isShiftActive;
        _shiftKeyObj = shiftKey; 

        var keyManager = shiftKey.GetComponent<KeyManager>();
        if (keyManager != null)
        {
            if (_isShiftActive) keyManager.SetColor(hoverColor);
            else keyManager.ResetColor();
        }
    }

    private void ForceEnableShift()
    {
        if (!_isShiftActive)
        {
            _isShiftActive = true;
            if (_shiftKeyObj != null)
            {
                var keyManager = _shiftKeyObj.GetComponent<KeyManager>();
                if (keyManager != null) keyManager.SetColor(hoverColor);
            }
        }
    }

    private void ToggleCaps(GameObject capsKey)
    {
        _isCapsActive = !_isCapsActive;
        _capsKeyObj = capsKey;

        var keyManager = capsKey.GetComponent<KeyManager>();
        if (keyManager != null)
        {
            if (_isCapsActive) keyManager.SetColor(hoverColor);
            else keyManager.ResetColor();
        }
    }

    private void ConsumeShift()
    {
        if (_isShiftActive)
        {
            _isShiftActive = false;
            if (_shiftKeyObj != null)
            {
                var keyManager = _shiftKeyObj.GetComponent<KeyManager>();
                if (keyManager != null) keyManager.ResetColor();
            }
        }
    }

    private void HandleBackspace()
    {
        _isCaretVisible = true;
        _nextBlinkTime = Time.time + 0.5f;
        
        string currentText = _rawInputText;
        if (!string.IsNullOrEmpty(currentText))
        {
            if (_isShiftActive || _isCapsActive)
            {
                // Fallback shortcut: If Shift/Caps are active on regular Backspace, delete whole word.
                HandleDeleteWord();
            }
            else
            {
                _rawInputText = currentText.Substring(0, currentText.Length - 1);
                UpdateDisplay();
            }
        }
    }

    private void HandleDeleteWord()
    {
        _isCaretVisible = true;
        _nextBlinkTime = Time.time + 0.5f;
        
        string currentText = _rawInputText;
        if (string.IsNullOrEmpty(currentText)) return;

        ConsumeShift();

        // Strip trailing spaces so we target the actual end of the last word text block
        string trimmedText = currentText.TrimEnd(' ');
        if (string.IsNullOrEmpty(trimmedText))
        {
            _rawInputText = "";
            UpdateDisplay();
            return;
        }

        int lastSpaceIndex = trimmedText.LastIndexOf(' ');
        if (lastSpaceIndex == -1)
        {
            // There are no spaces left; this is the single remaining word in the field. Clear it.
            _rawInputText = "";
        }
        else
        {
            // Slice the string right after that last space character to preserve space tracking
            _rawInputText = trimmedText.Substring(0, lastSpaceIndex + 1);
        }
        
        UpdateDisplay();
    }

    private void AppendText(string text)
    {
        _rawInputText += text;
        _isCaretVisible = true;
        _nextBlinkTime = Time.time + 0.5f;
        UpdateDisplay();
    }

    private void InsertText(string textToAdd, bool isWord)
    {
        if (string.IsNullOrEmpty(textToAdd)) return;

        string currentText = _rawInputText;
        string trailingPunctuation = ",.?!:;)'\n";

        bool isPunctuation = (!isWord && textToAdd.Length == 1 && trailingPunctuation.Contains(textToAdd));

        if (isPunctuation)
        {
            if (currentText.EndsWith(" "))
            {
                currentText = currentText.Substring(0, currentText.Length - 1);
                _rawInputText = currentText;
            }

            AppendText(textToAdd + " ");

            if (textToAdd == "." || textToAdd == "?" || textToAdd == "!" || textToAdd == "\n")
            {
                ForceEnableShift();
            }
        }
        else
        {
            AppendText(textToAdd + (isWord ? " " : ""));
        }
    }

    private void AppendWord(string word)
    {
        if (string.IsNullOrEmpty(word)) return;

        string finalWord = word.ToLower();

        if (_isCapsActive && !_isShiftActive) 
            finalWord = finalWord.ToUpper();
        else if (!_isCapsActive && _isShiftActive) 
            finalWord = char.ToUpper(finalWord[0]) + finalWord.Substring(1);
        else if (_isCapsActive && _isShiftActive) 
            finalWord = finalWord.ToLower(); 

        ConsumeShift(); 
        InsertText(finalWord, true);
    }

    // --- END STRUCTURAL KEYBOARD LOGIC ---

    public Vector2 GetMousePosition()
    {
        Vector3 worldPosition = _leftHandBool ? _leftIndexTip.position : _rightIndexTip.position;
        Vector3 localPoint = keyboardPlane.InverseTransformPoint(worldPosition);
        return new Vector2(localPoint.z, -localPoint.x); 
    }

    public GameObject GetHoveredKey(bool useLeftHand)
    {
        if (!_isInitialized) return null;

        Transform activeFingerTip = useLeftHand ? _leftIndexTip : _rightIndexTip;
        if (activeFingerTip == null) return null;

        Vector3 point = activeFingerTip.position;
        Collider[] hits = Physics.OverlapSphere(point, 0.001f, keyboardLayer);

        if (hits.Length == 0) return null;
        if (hits.Length == 1) return hits[0].gameObject;

        Collider closestHit = null;
        float closestSqrDistance = float.MaxValue;

        foreach (Collider hit in hits)
        {
            float sqrDist = (hit.transform.position - point).sqrMagnitude;
            if (sqrDist < closestSqrDistance)
            {
                closestSqrDistance = sqrDist;
                closestHit = hit;
            }
        }

        return closestHit.gameObject;
    }

    private IEnumerator InitializeSkeletons()
    {
        while (!IsSkeletonReady(leftSkeleton) || !IsSkeletonReady(rightSkeleton))
        {
            yield return null;
        }

        _leftIndexTip = FindIndexTip(leftSkeleton);
        _rightIndexTip = FindIndexTip(rightSkeleton);

        _isInitialized = (_leftIndexTip != null && _rightIndexTip != null);
    }

    private bool IsSkeletonReady(OVRSkeleton skeleton)
    {
        return skeleton != null && skeleton.IsDataValid && skeleton.Bones.Count > 0;
    }

    private Transform FindIndexTip(OVRSkeleton skeleton)
    {
        OVRSkeleton.SkeletonType type = skeleton.GetSkeletonType();
        OVRSkeleton.BoneId targetId =
            (type == OVRSkeleton.SkeletonType.XRHandLeft || type == OVRSkeleton.SkeletonType.XRHandRight)
                ? OVRSkeleton.BoneId.XRHand_IndexTip
                : OVRSkeleton.BoneId.Hand_IndexTip;

        foreach (OVRBone bone in skeleton.Bones)
        {
            if (bone.Id == targetId) return bone.Transform;
        }

        return null;
    }

    private void LoadWordsFromResources()
    {
        TextAsset textData = Resources.Load<TextAsset>("common_words");

        if (textData != null)
        {
            commonWords = textData.text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        }
        else
        {
            Debug.LogError("[Error] Couldn't find 'common_words.txt' inside Assets/Resources/ folder!");
        }
    }

    private Vector3 Project2DToSurface(Vector2 coord2D)
    {
        return _leftHandBool ? _leftIndexTip.position : _rightIndexTip.position;
    }
    
    private async void ProcessSwipeAsync(List<Vector2> points, char startChar, char endChar, List<char> anchors, string startName, string endName)
    {
        var resultsTuple = await Task.Run(() =>
        {
            List<DollarRecognizer.Result> results = _recognizer.Recognize(points, startChar, endChar, anchors);
            
            if (results == null || results.Count == 0)
                return (finalWord: "", rawWord: "", success: false);

            var sortedResults = results
                .Select(result =>
                {
                    string word = result.Match.Name;
                    int clampedRank = Mathf.Clamp(Array.IndexOf(commonWords, word), 1, 50000);
                    float score = result.Score * Mathf.Pow(1.0f - (Mathf.Log(clampedRank) / _bigLog), 0.5f);
                    return (word, score);
                })
                .OrderByDescending(r => r.score)
                .ToList();

            string winningWord = sortedResults[0].word;
            string rawWord = results[0].Match.Name;

            return (finalWord: winningWord, rawWord: rawWord, success: true);
        });

        if (!resultsTuple.success) return;

        if (statusText != null)
        {
            statusText.text = $"start is {startName}, end is {endName}, anchors are {new string(anchors.ToArray())}... final word is {resultsTuple.finalWord}.";
        }

        AppendWord(resultsTuple.finalWord);
    }
    
    private IEnumerator BlinkCaretRoutine()
    {
        while (true)
        {
            if (Time.time >= _nextBlinkTime)
            {
                _isCaretVisible = !_isCaretVisible;
                _nextBlinkTime = Time.time + 0.5f;
                UpdateDisplay();
            }
            yield return null;
        }
    }
    
    private void UpdateDisplay()
    {
        if (_isCaretVisible)
        {
            outputText.text = _rawInputText + CARET_CHAR;
        }
        else
        {
            outputText.text = _rawInputText + "<color=#00000000>" + CARET_CHAR + "</color>";
        }
    }
}