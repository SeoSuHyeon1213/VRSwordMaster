using UnityEngine;

public class Cube : MonoBehaviour
{
    [SerializeField] private float missDistance = 1.5f;
    [Tooltip("노트가 날아오는 속도 (m/s). 채보 모드에서는 Spawner 가 설정")]
    public float speed = 2f;

    // 채보의 '아무 방향' 노트: 어느 방향으로 베어도 인정
    public bool AnyDirection { get; private set; }

    private bool missStarted = false;

    public void SetAnyDirection(bool value)
    {
        AnyDirection = value;

        // 방향이 없는 노트는 화살표를 숨김
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "Arrow") t.gameObject.SetActive(!value);
        }
    }

    void Update()
    {
        transform.position += Time.deltaTime * transform.forward * -speed;

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
