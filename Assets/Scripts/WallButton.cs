using System.Collections;
using Unity.VisualScripting;
using UnityEngine;

public class WallButton : MonoBehaviour
{
    
    [Header("Hand Tracking")] 
    [SerializeField] private OVRSkeleton leftSkeleton;
    [SerializeField] private OVRSkeleton rightSkeleton;
    
    [Header("Keyboard")]
    [SerializeField] private Transform keyboardOrigin;
    [SerializeField] private float keyboardMinSize = 0.75f;
    
    [Header("Button")]
    [SerializeField] private Transform selfTransform;
    [SerializeField] private float lerpSpeed = 5f;
    [SerializeField] private Color pressColor;

    private Vector3 _targetPosition = Vector3.zero;
    private Vector3 _startPosition;
    
    private bool _isRunning = false;
    private float _miny;
    private Collider _myCollider;
    private float _scale = 0.05f;
    
    private KeyManager _keyManager;
    
    

    private void Start()
    {
        _myCollider = GetComponent<Collider>();
        _startPosition = selfTransform.localPosition;
        _targetPosition = selfTransform.localPosition;
        
        _keyManager = GetComponent<KeyManager>();
    }
    
    /* private void OnTriggerEnter(Collider other)
    {
        // Check if the thing touching the button is a hand/finger
        // (Meta's hand prefabs usually have tags or names like "Hand" or "Finger")
        if (other.name.Contains("Hand") || other.name.Contains("Finger") || other.CompareTag("Player"))
        {
            if (!_isRunning)
            {
                _miny = 1000f;
                TriggerButtonAction();
            }
        }
    }
    */
    

    private void Update()
    {
        if (_isRunning)
        {
            /*
            Transform trns = FindIndexTip(rightSkeleton);
            if (trns != null)
            { 
                _miny = Mathf.Min(_miny, trns.position.y);
            }
            */
            
        }
        else
        {
            if (IsInside(FindIndexTip(rightSkeleton)) || IsInside(FindIndexTip(leftSkeleton)))
            {
                _miny = 1000f;
                TriggerButtonAction();
            }
        }
        selfTransform.localPosition = Vector3.Lerp(selfTransform.localPosition, _targetPosition, lerpSpeed * Time.deltaTime);
    }

    private void TriggerButtonAction()
    {
        if (_isRunning) return; // Ignore if already running (cooldown)
        StartCoroutine(ExecuteActionRoutine());
    }
    
    private bool IsInside(Transform target)
    {
        if (target.IsUnityNull()) return false;
        
        if (target.position == _myCollider.ClosestPoint(target.position)) return true;
        
        return false;
    }

    private IEnumerator ExecuteActionRoutine()
    {
        _isRunning = true;
        // Debug.Log("Button pressed via Trigger!");
        _targetPosition = _startPosition + new Vector3(0, 0.1f, 0);
        _keyManager.SetColor(pressColor);
        
        
        
        yield return new WaitForSeconds(3f); // 3-second cooldown
        try
        {
            Vector3 rpos = FindIndexTip(rightSkeleton).position;
            Vector3 lpos = FindIndexTip(leftSkeleton).position;
            Vector3 origin = (rpos + lpos) / 2;
            
            float dist = Vector2.Distance(new Vector2(rpos.x, rpos.z), new Vector2(lpos.x, lpos.z));

            keyboardOrigin.position = origin;

            Vector3 heading = rpos - lpos;
            heading.y = 0;
            heading.Normalize();
            
            keyboardOrigin.rotation = Quaternion.LookRotation(heading, Vector3.up);
            // keyboardOrigin.rotation = Quaternion.Euler(0,90f,0); // Point forward
            keyboardOrigin.localRotation *= Quaternion.Euler(0, 0, -90f);

            _scale = Mathf.Max(dist / 14.5f, keyboardMinSize / 14.5f);
            keyboardOrigin.localScale = new Vector3(_scale, _scale, _scale);
            
            // origin.y += 1f * _scale;
            
            keyboardOrigin.position += keyboardOrigin.up * ((.4f * _scale) + 0.01f);

            // Debug.Log("Ready again.");
            _isRunning = false;
            _targetPosition = _startPosition;
            _keyManager.ResetColor();
        }
        catch
        {
            _isRunning = false;
            _targetPosition = _startPosition;
            _keyManager.ResetColor();
        }

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
}