using UnityEngine;

public class Cube : MonoBehaviour
{
    [SerializeField] private float missDistance = 1.5f;
    private bool missStarted = false;

    void Update()
    {
        transform.position += Time.deltaTime * transform.forward * -2;

        if (!missStarted)
        {
            Camera cam = Camera.main;
            if (cam != null && Vector3.Distance(transform.position, cam.transform.position) < missDistance)
            {
                missStarted = true;
                Invoke(nameof(OnMiss), 1f);
            }
        }
    }

    void OnMiss()
    {
        if (ComboManager.Instance != null) ComboManager.Instance.RegisterMiss();
        if (HealthManager.Instance != null) HealthManager.Instance.TakeDamage();
        Destroy(gameObject);
    }
}
