using UnityEngine;

public class LingeringSmokeShapeExpansion : MonoBehaviour
{
    [SerializeField] private ParticleSystem particleSystemRef;

    [Header("Cone Length")]
    [SerializeField] private float startLength = 2f;
    [SerializeField] private float endLength = 7f;

    [Header("Expansion")]
    [SerializeField] private float expansionDuration = 0.4f;

    [SerializeField]
    private AnimationCurve expansionCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private float timer;
    private bool isExpanding;

    private void Awake()
    {
        if (particleSystemRef == null)
            particleSystemRef = GetComponent<ParticleSystem>();
    }
    private void Update()
    {
        if (!isExpanding || particleSystemRef == null)
            return;

        var shape = particleSystemRef.shape;

        if (expansionDuration <= 0f)
        {
            shape.length = endLength;
            isExpanding = false;
            return;
        }

        timer += Time.deltaTime;

        float t = Mathf.Clamp01(timer / expansionDuration);
        float curveValue = expansionCurve.Evaluate(t);

        shape.length = Mathf.Lerp(
            startLength,
            endLength,
            curveValue
        );

        if (t >= 1f)
            isExpanding = false;
    }

    public void PlaySmoke()
    {
        if (particleSystemRef == null)
        {
            particleSystemRef = GetComponent<ParticleSystem>();

            if (particleSystemRef == null)
                return;
        }

        timer = 0f;
        isExpanding = true;

        var shape = particleSystemRef.shape;
        shape.length = startLength;

        particleSystemRef.Stop(
            true,
            ParticleSystemStopBehavior.StopEmittingAndClear
        );
        particleSystemRef.Play(true);
    }
}
