using UnityEngine;

[DisallowMultipleComponent]
public sealed class LiraMove : MonoBehaviour
{
    [Tooltip("Напрямок у світових осях: X = 1 — уздовж осі X.")]
    [SerializeField] private Vector3 direction = Vector3.right;

    [Tooltip("Відстань від початку до розвороту в одиницях Unity.")]
    [Min(0f)] [SerializeField] private float distance = 4f;

    [Tooltip("Швидкість руху в одиницях за секунду.")]
    [Min(0f)] [SerializeField] private float speed = 2f;

    private float cycle;
    private float previousOffset;

    private void OnEnable()
    {
        cycle = 0f;
        previousOffset = 0f;
    }

    private void Update()
    {
        if (distance <= 0.0001f || speed <= 0f || direction.sqrMagnitude < 0.0001f)
            return;

        cycle = Mathf.Repeat(cycle + speed * Time.deltaTime, distance * 2f);
        float offset = Mathf.PingPong(cycle, distance);

        // Додаємо лише крок руху: портал не спричинить стрибок назад до старту.
        transform.Translate(direction.normalized * (offset - previousOffset), Space.World);
        previousOffset = offset;
    }
}
