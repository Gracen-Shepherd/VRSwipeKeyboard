using UnityEngine;

public class KeyManager : MonoBehaviour
{
    private Renderer _renderer;
    private MaterialPropertyBlock _propBlock;
    
    private Color defaultColor = new Color32(20, 24, 30, 35);
    private float blendSpeed = 13f;
    private Color _targetColor;
    
    private Color _currentColor;
    
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        _renderer = GetComponent<Renderer>();
        _propBlock = new MaterialPropertyBlock();
        
        _currentColor = defaultColor;
        _targetColor = defaultColor;
    }

    // Update is called once per frame
    void Update()
    {
        _currentColor = Color.Lerp(_currentColor, _targetColor, Time.deltaTime * blendSpeed);
        
        _renderer.GetPropertyBlock(_propBlock);
        _propBlock.SetColor(BaseColor, _currentColor);
        _renderer.SetPropertyBlock(_propBlock);
    }

    public void SetColor(Color color)
    {
        _targetColor = color;
    }

    public void ResetColor()
    {
        _targetColor = defaultColor;
    }
}
