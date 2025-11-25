using System;
using System.Collections.Generic;
using System.Reflection;
using ItemStatsSystem;
using UnityEngine;
using Duckov;

namespace enemyally
{
    // Duckov 모드 로더가 찾는 엔트리: enemyally.ModBehaviour
    public class ModBehaviour : Duckov.Modding.ModBehaviour
    {
        protected override void OnAfterSetup()
        {
            try
            {
                GameObject root = new GameObject("EnemyAllyRoot");
                UnityEngine.Object.DontDestroyOnLoad(root);

                root.AddComponent<EnemyAllyManager>();

                Debug.Log("[EnemyAlly] OnAfterSetup - 아군 전환 매니저 초기화 완료");
            }
            catch (Exception ex)
            {
                Debug.Log("[EnemyAlly] OnAfterSetup 예외: " + ex);
            }
        }
    }

    public class EnemyAllyManager : MonoBehaviour
    {
        private static EnemyAllyManager _instance;
        public static EnemyAllyManager Instance { get { return _instance; } }

        private Camera _mainCamera;
        private CharacterMainControl _player;

        // 이미 아군으로 전환한 대상들
        private readonly List<CharacterMainControl> _allies = new List<CharacterMainControl>();

        // 설득 키, 거리
        private static readonly KeyCode RecruitKey = KeyCode.PageUp;
        private const float MaxRecruitDistance = 60f;

        // 블랙리스트(비밀 상인, pet 등)
        private static readonly string[] _blacklistNameKeywords = new string[]
        {
            "비밀 상인",
            "謎の商人",
            "secretmerchant",
            "secret_merchant",
            "secret trader",
            "secrettrader",
            "secret_trader",
            "mysterious merchant",
            "mysterious_trader",
            "pet"          // 강아지 이름
        };

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            Debug.Log("[EnemyAlly] EnemyAllyManager Awake");
        }

        private void Update()
        {
            if (!Application.isPlaying)
                return;

            // 플레이어, 카메라 캐시
            _player = GetPlayer();
            if (_player == null)
                return;

            if (_mainCamera == null || !_mainCamera.enabled)
                _mainCamera = Camera.main;

            if (_mainCamera == null)
                return;

            // PageUp 키로 설득
            if (Input.GetKeyDown(RecruitKey))
            {
                TryRecruitFromCamera();
            }
        }

        // CharacterMainControl.Main 기반으로 플레이어 찾기
        private CharacterMainControl GetPlayer()
        {
            try
            {
                CharacterMainControl p = CharacterMainControl.Main;
                if (p == null || p.Health == null || p.Health.IsDead)
                    return null;
                return p;
            }
            catch (Exception ex)
            {
                Debug.Log("[EnemyAlly] GetPlayer 예외: " + ex);
                return null;
            }
        }

        // PopText로 게임 내 말풍선 띄우기
        private void ShowPopText(CharacterMainControl target, string msg)
        {
            if (string.IsNullOrEmpty(msg))
                return;

            CharacterMainControl c = target;
            if (c == null)
                c = _player;

            if (c == null)
            {
                try { c = CharacterMainControl.Main; } catch { }
            }

            if (c == null)
                return;

            try
            {
                c.PopText(msg, -1f);
            }
            catch (Exception ex)
            {
                Debug.Log("[EnemyAlly] PopText 예외: " + ex);
            }
        }

        // 화면(카메라) 안에 보이는 적들 중, 화면 중앙에 가장 가까운 적을 찾아서 설득
        private void TryRecruitFromCamera()
        {
            try
            {
                CharacterMainControl target = FindBestEnemyInView();

                if (target == null)
                {
                    Debug.Log("[EnemyAlly] 카메라 안에 설득 가능한 적이 없습니다.");
                    ShowPopText(_player, "근처에 적이 없습니다");
                    return;
                }

                RecruitAsAlly(target);
            }
            catch (Exception ex)
            {
                Debug.Log("[EnemyAlly] TryRecruitFromCamera 예외: " + ex);
            }
        }

        // 카메라 화면 안에 있는 적 중, 화면 중앙에 가장 가까운 적 찾기
        private CharacterMainControl FindBestEnemyInView()
        {
            if (_player == null || _mainCamera == null)
                return null;

            CharacterMainControl[] all = UnityEngine.Object.FindObjectsOfType<CharacterMainControl>();
            if (all == null || all.Length == 0)
                return null;

            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f;

            float bestScreenDistSqr = float.MaxValue;
            CharacterMainControl best = null;

            Vector3 playerPos = _player.transform.position;

            for (int i = 0; i < all.Length; i++)
            {
                CharacterMainControl c = all[i];
                if (!IsEnemyCandidate(c))
                    continue;

                Transform t = c.transform;
                if (t == null)
                    continue;

                // 플레이어와의 실제 거리 제한
                float worldDist = Vector3.Distance(playerPos, t.position);
                if (worldDist > MaxRecruitDistance)
                    continue;

                // 화면 좌표로 변환 (머리 위 쪽을 기준)
                Vector3 worldPos = t.position + Vector3.up * 1.5f;
                Vector3 sp = _mainCamera.WorldToScreenPoint(worldPos);

                // 카메라 뒤에 있으면 제외
                if (sp.z <= 0f)
                    continue;

                // 화면 밖이면 제외
                if (sp.x < 0f || sp.x > Screen.width || sp.y < 0f || sp.y > Screen.height)
                    continue;

                float dx = sp.x - cx;
                float dy = sp.y - cy;
                float screenDistSqr = dx * dx + dy * dy;

                if (screenDistSqr < bestScreenDistSqr)
                {
                    bestScreenDistSqr = screenDistSqr;
                    best = c;
                }
            }

            return best;
        }

        // 이 캐릭터가 "설득 가능한 적"인지 판정
        private bool IsEnemyCandidate(CharacterMainControl c)
        {
            if (c == null)
                return false;

            if (c == _player)
                return false;

            // 이름/표시 이름으로 블랙리스트 (비밀 상인 + pet 등)
            if (IsBlacklisted(c))
                return false;

            // Health 플래그 기준으로 적인지 확인
            try
            {
                Health h = c.GetComponent<Health>();
                if (h == null)
                    return false;

                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                Type ht = h.GetType();

                bool isEnemy = true;

                FieldInfo fIsEnemy = ht.GetField("isEnemy", flags);
                if (fIsEnemy != null && fIsEnemy.FieldType == typeof(bool))
                {
                    isEnemy = (bool)fIsEnemy.GetValue(h);
                }
                else
                {
                    PropertyInfo pIsEnemy = ht.GetProperty("isEnemy", flags);
                    if (pIsEnemy != null && pIsEnemy.CanRead && pIsEnemy.PropertyType == typeof(bool))
                    {
                        isEnemy = (bool)pIsEnemy.GetValue(h, null);
                    }
                }

                // 이미 아군이면 제외
                FieldInfo fIsFriendly = ht.GetField("isFriendly", flags);
                if (fIsFriendly != null && fIsFriendly.FieldType == typeof(bool))
                {
                    bool isFriendly = (bool)fIsFriendly.GetValue(h);
                    if (isFriendly)
                        return false;
                }

                return isEnemy;
            }
            catch (Exception ex)
            {
                Debug.Log("[EnemyAlly] IsEnemyCandidate 예외: " + ex);
                return false;
            }
        }

        // 이름/표시 이름으로 블랙리스트 판정 (비밀 상인, pet 등)
        private bool IsBlacklisted(CharacterMainControl c)
        {
            try
            {
                if (c == null)
                    return false;

                string name = c.name;
                string displayName = GetCharacterDisplayName(c);

                if (!string.IsNullOrEmpty(name))
                {
                    string lower = name.ToLowerInvariant();
                    for (int i = 0; i < _blacklistNameKeywords.Length; i++)
                    {
                        string kw = _blacklistNameKeywords[i];
                        if (string.IsNullOrEmpty(kw)) continue;

                        if (lower.Contains(kw.ToLowerInvariant()))
                            return true;
                    }
                }

                if (!string.IsNullOrEmpty(displayName))
                {
                    string lowerDisplay = displayName.ToLowerInvariant();
                    for (int i = 0; i < _blacklistNameKeywords.Length; i++)
                    {
                        string kw = _blacklistNameKeywords[i];
                        if (string.IsNullOrEmpty(kw)) continue;

                        if (lowerDisplay.Contains(kw.ToLowerInvariant()))
                            return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Log("[EnemyAlly] IsBlacklisted 예외: " + ex);
            }

            return false;
        }

        // 캐릭터의 displayName 읽기 (있으면)
        private string GetCharacterDisplayName(CharacterMainControl c)
        {
            if (c == null)
                return null;

            try
            {
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                Type t = c.GetType();

                FieldInfo f = t.GetField("displayName", flags);
                if (f != null)
                {
                    object value = f.GetValue(c);
                    if (value != null) return value.ToString();
                }

                PropertyInfo p = t.GetProperty("displayName", flags);
                if (p != null && p.CanRead)
                {
                    object value = p.GetValue(c, null);
                    if (value != null) return value.ToString();
                }
            }
            catch (Exception ex)
            {
                Debug.Log("[EnemyAlly] GetCharacterDisplayName 예외: " + ex);
            }

            return null;
        }

        // Health 플래그를 이용해서 진짜 "아군"으로 표시
        private void MarkAsFriendly(CharacterMainControl target)
        {
            if (target == null)
                return;

            try
            {
                Health h = target.GetComponent<Health>();
                if (h != null)
                {
                    const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                    Type ht = h.GetType();

                    FieldInfo fIsEnemy = ht.GetField("isEnemy", flags);
                    if (fIsEnemy != null && fIsEnemy.FieldType == typeof(bool))
                    {
                        fIsEnemy.SetValue(h, false);
                        Debug.Log("[EnemyAlly] Health.isEnemy = false");
                    }

                    FieldInfo fIsFriendly = ht.GetField("isFriendly", flags);
                    if (fIsFriendly != null && fIsFriendly.FieldType == typeof(bool))
                    {
                        fIsFriendly.SetValue(h, true);
                        Debug.Log("[EnemyAlly] Health.isFriendly = true");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Log("[EnemyAlly] MarkAsFriendly 예외: " + ex);
            }
        }

        // 실제 아군 전환 로직
        private void RecruitAsAlly(CharacterMainControl target)
        {
            if (target == null)
                return;

            if (_allies.Contains(target))
            {
                Debug.Log("[EnemyAlly] 이미 아군으로 전환된 대상입니다: " + target.name);
                ShowPopText(target, "넌 내꺼야♥");
                return;
            }

            if (_player == null)
                _player = GetPlayer();

            if (_player == null)
            {
                Debug.Log("[EnemyAlly] 플레이어를 찾지 못해 설득 실패");
                return;
            }

            try
            {
                // 1) 플레이어의 team 값(실제 enum)을 Reflection으로 읽어옴
                object playerTeamValue = GetTeamValue(_player);
                if (playerTeamValue == null)
                {
                    Debug.Log("[EnemyAlly] 플레이어 팀 정보를 찾지 못했습니다. (team 필드/프로퍼티 없음?)");
                }
                else
                {
                    // 2) 대상 캐릭터의 team을 플레이어와 동일하게 설정
                    SetTeamValue(target, playerTeamValue);
                }

                // 3) Health 플래그를 아군 상태로 전환
                MarkAsFriendly(target);

                // 4) 표시 이름 뒤에 (ALLY) 붙여서 시각적으로 구분
                string disp = GetCharacterDisplayName(target);
                if (!string.IsNullOrEmpty(disp) && !disp.Contains("(ALLY)"))
                {
                    SetCharacterDisplayName(target, disp + " (ALLY)");
                }

                _allies.Add(target);

                // PopText로 말풍선 출력
                ShowPopText(target, "넌 내꺼야♥");

                Debug.Log("[EnemyAlly] 적을 아군으로 전환 완료: " +
                          (string.IsNullOrEmpty(disp) ? target.name : disp + " (ALLY)"));
            }
            catch (Exception ex)
            {
                Debug.Log("[EnemyAlly] RecruitAsAlly 예외: " + ex);
            }
        }

        // CharacterMainControl에서 team 필드/프로퍼티 값 읽기
        private object GetTeamValue(CharacterMainControl c)
        {
            if (c == null)
                return null;

            try
            {
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                Type t = c.GetType();

                FieldInfo f = t.GetField("team", flags);
                if (f != null)
                {
                    object value = f.GetValue(c);
                    Debug.Log("[EnemyAlly] team 필드 읽기 성공: " + (value != null ? value.ToString() : "null"));
                    return value;
                }

                PropertyInfo p = t.GetProperty("team", flags);
                if (p != null && p.CanRead)
                {
                    object value = p.GetValue(c, null);
                    Debug.Log("[EnemyAlly] team 프로퍼티 읽기 성공: " + (value != null ? value.ToString() : "null"));
                    return value;
                }
            }
            catch (Exception ex)
            {
                Debug.Log("[EnemyAlly] GetTeamValue 예외: " + ex);
            }

            return null;
        }

        // CharacterMainControl의 team 필드/프로퍼티 설정
        private void SetTeamValue(CharacterMainControl c, object teamValue)
        {
            if (c == null)
                return;

            try
            {
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                Type t = c.GetType();

                FieldInfo f = t.GetField("team", flags);
                if (f != null)
                {
                    f.SetValue(c, teamValue);
                    Debug.Log("[EnemyAlly] CharacterMainControl.team 필드 설정: " + (teamValue != null ? teamValue.ToString() : "null"));
                    return;
                }

                PropertyInfo p = t.GetProperty("team", flags);
                if (p != null && p.CanWrite)
                {
                    p.SetValue(c, teamValue, null);
                    Debug.Log("[EnemyAlly] CharacterMainControl.team 프로퍼티 설정: " + (teamValue != null ? teamValue.ToString() : "null"));
                }
            }
            catch (Exception ex)
            {
                Debug.Log("[EnemyAlly] SetTeamValue 예외: " + ex);
            }
        }

        // displayName 변경만 담당
        private void SetCharacterDisplayName(CharacterMainControl c, string newName)
        {
            if (c == null)
                return;

            if (string.IsNullOrEmpty(newName))
                return;

            try
            {
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                Type t = c.GetType();

                FieldInfo f = t.GetField("displayName", flags);
                if (f != null && f.FieldType == typeof(string))
                {
                    f.SetValue(c, newName);
                    Debug.Log("[EnemyAlly] displayName 필드 변경: " + newName);
                    return;
                }

                PropertyInfo p = t.GetProperty("displayName", flags);
                if (p != null && p.CanWrite && p.PropertyType == typeof(string))
                {
                    p.SetValue(c, newName, null);
                    Debug.Log("[EnemyAlly] displayName 프로퍼티 변경: " + newName);
                }
            }
            catch (Exception ex)
            {
                Debug.Log("[EnemyAlly] SetCharacterDisplayName 예외: " + ex);
            }
        }
    }
}
