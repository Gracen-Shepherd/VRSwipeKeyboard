using UnityEngine;
using TMPro;

[ExecuteAlways]
[RequireComponent(typeof(TextMeshPro))]
public class AutoKeyLabelEditor : MonoBehaviour
{
    private TextMeshPro _textComponent;

    void OnEnable()
    {
        _textComponent = GetComponent<TextMeshPro>();
        UpdateLabel();
    }

    // This runs automatically in the editor whenever you rename or move things
    void OnValidate()
    {
        UpdateLabel();
    }

    private void UpdateLabel()
    {
        if (_textComponent == null) _textComponent = GetComponent<TextMeshPro>();
        
        if (transform.parent != null && _textComponent != null)
        {
            string parentName = transform.parent.name;
            
            // Only update if the text is actually different to save processor cycles
            if (_textComponent.text != parentName)
            {
                _textComponent.text = parentName;
            }
        }
    }
}