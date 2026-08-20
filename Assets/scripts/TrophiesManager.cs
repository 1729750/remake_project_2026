using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 트로피 행(왼쪽부터 EnemyDisplay를 채워나가는 UI)을 관리하는 재사용 컴포넌트.
// RectTransform이 달린 컨테이너에 직접 부착해서 쓴다 — MapManager의 Trophies,
// GameEndManager의 트로피 컨테이너 등 여러 곳에서 각자 인스턴스를 갖는다.
[RequireComponent(typeof(RectTransform))]
public class TrophiesManager : MonoBehaviour
{
    [SerializeField] private float trophySpacing = 1.5f;

    // AddTrophy가 호출될 때마다 재생하는 등장 애니메이션: startScale에서 endScale까지 이차함수로
    // 줄어든다(size = startScale - t^2 * (startScale - endScale), t는 duration 기준 0~1 진행률).
    // 크기가 점점 줄어드는 모양이 마치 위에서 아래로 내려앉는 것처럼 보인다. 둘 다 여기 변수로
    // 둬서 나중에 수정하기 쉽게 한다.
    [SerializeField] private float trophyStartScale = 1.2f;
    [SerializeField] private float trophyEndScale = 0.5f;
    [SerializeField] private float trophyAnimationDuration = 1f;

    private RectTransform _rect;
    private GameObject _trophyPrefab;

    // 추가된 순서 = 왼쪽부터. 비주얼과 그 비주얼이 표시 중인 CharacterData를 나란히 들고 있는다.
    private readonly List<MapVisual> _trophyVisuals = new List<MapVisual>();
    private readonly List<CharacterData> _trophyData = new List<CharacterData>();

    // AddTrophy를 연달아 여러 번 호출해도 애니메이션이 동시에 겹치지 않도록, 실제 스폰+애니메이션은
    // 큐에 쌓아두고 하나씩 순서대로 처리한다. 아직 차례가 오지 않은 항목은 오브젝트로 존재하지 않는다
    // (ProcessQueue가 자기 차례가 됐을 때 비로소 instantiate한다).
    private readonly Queue<PendingTrophy> _pendingTrophies = new Queue<PendingTrophy>();
    private Coroutine _animationRoutine;

    private struct PendingTrophy
    {
        public CharacterData Data;
        public float Duration;
    }

    private void Awake()
    {
        _rect = GetComponent<RectTransform>();
    }

    // 비활성 상태에서 큐에만 쌓아뒀던 트로피가 있으면, 활성화되는 시점에 처리를 시작한다.
    private void OnEnable()
    {
        TryStartProcessing();
    }

    // 오브젝트가 비활성화되면 Unity가 진행 중이던 코루틴을 알아서 죽여버린다(재개되지 않는다).
    // _animationRoutine을 비워둬야 다음 OnEnable/AddTrophy에서 다시 시작할 수 있다.
    private void OnDisable()
    {
        _animationRoutine = null;
    }

    public IReadOnlyList<CharacterData> GetTrophyData() => _trophyData;

    // data를 트로피로 추가할 예약을 건다. duration을 생략(음수)하면 trophyAnimationDuration을 쓴다.
    // 이미 다른 트로피가 애니메이션 중이면 이번 건 큐에 쌓였다가 그 다음에 처리된다. 지금 이
    // 오브젝트가 비활성 상태라면(예: 아직 SelectEnemy로 전환되지 않은 mapManager) 큐에만 쌓아두고
    // 실제 처리는 OnEnable로 미룬다 — 비활성 오브젝트에서 StartCoroutine을 호출하면 예외가 난다.
    public void AddTrophy(CharacterData data, float duration = -1f)
    {
        if (data == null) return;

        _pendingTrophies.Enqueue(new PendingTrophy
        {
            Data = data,
            Duration = duration >= 0f ? duration : trophyAnimationDuration,
        });

        TryStartProcessing();
    }

    private void TryStartProcessing()
    {
        if (_animationRoutine != null || _pendingTrophies.Count == 0) return;
        if (!gameObject.activeInHierarchy) return;

        _animationRoutine = StartCoroutine(ProcessQueue());
    }

    // 큐가 빌 때까지 하나씩 순서대로 스폰+애니메이션을 처리한다.
    private IEnumerator ProcessQueue()
    {
        while (_pendingTrophies.Count > 0)
        {
            PendingTrophy next = _pendingTrophies.Dequeue();
            yield return SpawnAndAnimate(next.Data, next.Duration);
        }
        _animationRoutine = null;
    }

    // enemyDisplay 프리팹을 하나 instantiate해 자신의 child로 붙이고 data를 표시한 뒤, 왼쪽부터
    // 다시 정렬하고(MapTrophyManager) 등장 애니메이션이 끝날 때까지 기다린다.
    private IEnumerator SpawnAndAnimate(CharacterData data, float duration)
    {
        if (_trophyPrefab == null)
            _trophyPrefab = Resources.Load<GameObject>("Prefabs/EnemyDisplay");
        if (_trophyPrefab == null) yield break;

        MapVisual visual = Instantiate(_trophyPrefab, transform).GetComponent<MapVisual>();
        visual.SetCharacter(data);
        _trophyVisuals.Add(visual);
        _trophyData.Add(data);

        MapTrophyManager();

        yield return AnimateTrophyScale(visual.transform, duration);
    }

    private IEnumerator AnimateTrophyScale(Transform trophyTransform, float duration)
    {
        Vector3 baseScale = trophyTransform.localScale;

        if (duration <= 0f)
        {
            trophyTransform.localScale = baseScale * trophyEndScale;
            yield break;
        }

        float scaleDelta = trophyStartScale - trophyEndScale;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float scale = trophyStartScale - t * t * scaleDelta;
            trophyTransform.localScale = baseScale * scale;
            yield return null;
        }

        trophyTransform.localScale = baseScale * trophyEndScale;
    }

    // 트로피들의 위치를 관리한다. 컨테이너의 왼쪽 끝(rect.xMin)부터 trophySpacing 간격으로
    // _trophyVisuals에 추가된 순서 그대로(=왼쪽부터) 나열한다.
    private void MapTrophyManager()
    {
        float startX = _rect.rect.xMin + trophySpacing * 0.5f;
        for (int i = 0; i < _trophyVisuals.Count; i++)
        {
            MapVisual visual = _trophyVisuals[i];
            if (visual == null) continue;
            visual.transform.localPosition = new Vector3(startX + trophySpacing * i, 0f, 0f);
        }
    }

    // 트로피 행을 비운다. 매 게임(새 런) 시작 시 리셋되어야 한다. 아직 처리되지 않은 예약과
    // 진행 중이던 애니메이션도 함께 버린다.
    public void ResetTrophies()
    {
        if (_animationRoutine != null)
        {
            StopCoroutine(_animationRoutine);
            _animationRoutine = null;
        }
        _pendingTrophies.Clear();

        foreach (MapVisual visual in _trophyVisuals)
            if (visual != null) Destroy(visual.gameObject);
        _trophyVisuals.Clear();
        _trophyData.Clear();
    }
}
