using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

public class EnemyConverter : MonoBehaviour
{
    private static int _convertedCount = 0;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.M))
        {
            TryConvertEnemies();
        }
    }

    private void TryConvertEnemies()
    {
        var allChars = GameObject.FindObjectsOfType<CharacterMainControl>();

        foreach (var ch in allChars)
        {
            if (ch == null) continue;
            if (ch == CharacterMainControl.Main) continue; 
            if (ch.Team == CharacterMainControl.Main.Team) continue;

            float dist = Vector3.Distance(ch.transform.position, CharacterMainControl.Main.transform.position);
            if (dist > 8f) continue;

            ConvertEnemy(ch);
        }
    }

    private void ConvertEnemy(CharacterMainControl target)
    {
        if (target == null) return;

        typeof(CharacterMainControl)
            .GetField("team", BindingFlags.NonPublic | BindingFlags.Instance)
            ?.SetValue(target, CharacterMainControl.Main.Team);

        var ai = target.GetComponent<AICharacterController>();
        if (ai != null) ai.StopMove();

        var old = target.GetComponent<BasicFollowAI>();
        if (old != null) Destroy(old);

        var follower = target.gameObject.AddComponent<BasicFollowAI>();
        follower.master = CharacterMainControl.Main;

        _convertedCount++;
        CharacterMainControl.Main.PopText($"동료로 전환됨! ({_convertedCount})", 3f);
    }
}
