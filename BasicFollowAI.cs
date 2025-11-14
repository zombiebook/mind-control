using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BasicFollowAI : MonoBehaviour
{
    public CharacterMainControl master;
    public float followDistance = 3f;
    public float moveSpeed = 4f;

    private CharacterMainControl self;

    private void Start()
    {
        self = GetComponent<CharacterMainControl>();
    }

    private void Update()
    {
        if (master == null || self == null) return;

        Vector3 pos = self.transform.position;
        Vector3 mpos = master.transform.position;

        float dist = Vector3.Distance(pos, mpos);

        if (dist > followDistance)
        {
            Vector3 dir = (mpos - pos).normalized;
            Vector3 next = pos + dir * moveSpeed * Time.deltaTime;

            self.SetPosition(next);
        }
    }
}
