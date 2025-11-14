using UnityEngine;

public class FollowAI : MonoBehaviour
{
    public CharacterMainControl master;

    public float followDistance = 4f;
    public float moveSpeed = 3f;
    public float rotateSpeed = 5f;

    CharacterMainControl self;

    private void Awake()
    {
        self = GetComponent<CharacterMainControl>();
    }

    private void Update()
    {
        if (master == null || self == null) return;

        Vector3 dir = master.transform.position - transform.position;
        float dist = dir.magnitude;

        if (dist > followDistance)
        {
            transform.position += dir.normalized * moveSpeed * Time.deltaTime;

            Vector3 lookDir = master.transform.position - transform.position;
            lookDir.y = 0f;

            if (lookDir != Vector3.zero)
            {
                Quaternion target = Quaternion.LookRotation(lookDir);
                transform.rotation = Quaternion.Lerp(transform.rotation, target, rotateSpeed * Time.deltaTime);
            }
        }
    }
}
