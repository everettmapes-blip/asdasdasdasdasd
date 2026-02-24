using KeyMouse.MoHide;
using UnityEngine;

public class PropMimicParticlesEffect : MonoBehaviour
{
    [SerializeField] private ParticleSystemRenderer _particleSystem;
    private HidingCharacter _hidingCharacter;

    private void Start()
    {
        _hidingCharacter = Object.FindFirstObjectByType<HidingCharacter>();

        SetParticlesMesh();
    }

    private void SetParticlesMesh()
    {
        Transform selectedProp = _hidingCharacter.currentObject.transform;

        if (selectedProp.TryGetComponent(out MeshFilter meshFilter))
        {
            _particleSystem.mesh = meshFilter.mesh;
        }
        else if (selectedProp.GetComponentInChildren<MeshFilter>())
        {
            _particleSystem.mesh = selectedProp.GetComponentInChildren<MeshFilter>().mesh;
        }

    }
}
