using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 멀티플레이용 보상/기능 선택 화면의 흐름을 담당할 프로토타입.
///
/// 현재 단계에서는 Network 코드를 직접 넣지 않고,
/// 기존 싱글플레이 RewardManager의 흐름을 그대로 재현한다.
///
/// 기본 흐름:
///
/// 1. 후보 생성
/// 2. 후보 데이터 저장
/// 3. RewardDisplay 생성
/// 4. 각 Display에 후보 데이터 전달
/// 5. 좌/우 선택
/// 6. 선택 확정
///
/// 추후 멀티플레이에서는
/// "후보 생성"과 "선택 확정"만 서버 권한으로 옮기는 것을 목표로 한다.
///
/// [싱글 구조]
/// Client
///   └─ 후보 생성
///   └─ 후보 표시
///   └─ 선택
///   └─ 적용
///
/// [향후 멀티 구조]
/// Server
///   └─ 후보 생성
///        ↓
/// Client
///   └─ 후보 표시
///   └─ 선택
///        ↓
/// Server
///   └─ 선택 검증 / 확정
///        ↓
/// All Clients
///   └─ 확정 결과 반영
///
/// ※ 클래스 이름은 요청에 따라 RewardDisplay_Multi지만,
///    실제 역할은 RewardManager_Multi 또는 RewardSelectionManager에 더 가깝다.
/// </summary>
public class RewardDisplay_Multi : MonoBehaviour
{
    // ---------------------------------------------------------
    // 기본 설정
    // ---------------------------------------------------------

    /// <summary>
    /// 한 번에 보여줄 후보 개수.
    ///
    /// 기존 RewardManager의 강화 선택지가 3개이므로
    /// 우선 동일하게 3개로 맞춘다.
    /// </summary>
    private const int CandidateCount = 3;


    /// <summary>
    /// 기존 RewardDisplay 프리팹.
    ///
    /// 기존 싱글 버전과 같은 Prefab을 재사용할 수 있다면
    /// 멀티 전용 Prefab을 따로 만들 필요는 없다.
    ///
    /// Display는 "화면에 보여주는 역할"만 담당하기 때문에
    /// 네트워크 여부와 크게 상관없다.
    /// </summary>
    [SerializeField]
    private GameObject rewardDisplayPrefab;


    // ---------------------------------------------------------
    // 후보 데이터
    // ---------------------------------------------------------

    /// <summary>
    /// 이번 선택 화면에서 보여줄 후보 데이터.
    ///
    /// 현재는 CardUpgrade를 그대로 사용한다.
    ///
    /// 싱글:
    ///     이 클래스가 직접 RollEnhanceOption()을 호출해서 생성
    ///
    /// 멀티:
    ///     서버가 생성한 후보 데이터를 받아서 저장
    ///
    /// 즉 나중에도 Display/선택 로직은 그대로 두고
    /// "이 배열을 누가 채우느냐"만 바꾸면 된다.
    /// </summary>
    private CardUpgrade[] _candidates;


    /// <summary>
    /// 후보를 실제 화면에 보여주는 RewardDisplay들.
    ///
    /// _candidates[0] → _displays[0]
    /// _candidates[1] → _displays[1]
    /// _candidates[2] → _displays[2]
    ///
    /// 이런 식으로 1:1 대응한다.
    /// </summary>
    private RewardDisplay[] _displays;


    /// <summary>
    /// 현재 플레이어가 선택하고 있는 후보의 index.
    ///
    /// 0 = 왼쪽
    /// 1 = 가운데
    /// 2 = 오른쪽
    /// </summary>
    private int _selectedIndex;


    /// <summary>
    /// 중복 확정을 막기 위한 플래그.
    ///
    /// 멀티에서는 네트워크 지연 때문에
    /// Select 입력이 여러 번 들어오는 상황을 방지하는 데 더 중요해진다.
    /// </summary>
    private bool _selectionConfirmed;


    // ---------------------------------------------------------
    // 선택 화면 시작
    // ---------------------------------------------------------

    /// <summary>
    /// 기능 선택 화면 시작.
    ///
    /// 현재 로컬 테스트에서는:
    ///
    /// GenerateCandidatesLocal()
    ///     ↓
    /// StoreCandidates()
    ///     ↓
    /// CreateDisplays()
    ///     ↓
    /// PushCandidateDataToDisplays()
    ///     ↓
    /// 입력 대기
    ///
    /// 순서로 진행한다.
    ///
    /// 멀티 버전에서는 이 함수에서 직접 후보를 생성하지 않고
    /// 서버에 후보 생성을 요청하는 형태로 변경한다.
    /// </summary>
    public void StartSelection()
    {
        _selectionConfirmed = false;
        _selectedIndex = 0;

        // -----------------------------------------------------
        // 현재: 로컬 프로토타입
        // -----------------------------------------------------

        CardUpgrade[] generatedCandidates = GenerateCandidatesLocal();

        StoreCandidates(generatedCandidates);

        CreateDisplays();

        PushCandidateDataToDisplays();

        RefreshSelection();


        // -----------------------------------------------------
        // TODO [MULTI]
        //
        // 최종적으로는 위의 GenerateCandidatesLocal() 대신:
        //
        // RequestCandidatesFromServer();
        //
        // 와 같은 구조가 되어야 한다.
        //
        // 서버가 후보를 만든 뒤
        //
        // ReceiveCandidatesFromServer(...)
        //
        // 에 후보 데이터를 전달하면
        //
        // StoreCandidates()
        // CreateDisplays()
        // PushCandidateDataToDisplays()
        //
        // 를 실행한다.
        // -----------------------------------------------------
    }


    // ---------------------------------------------------------
    // 1. 후보 생성
    // ---------------------------------------------------------

    /// <summary>
    /// [현재 로컬 테스트용]
    ///
    /// 후보 3개를 랜덤 생성한다.
    ///
    /// 기존 RewardManager의
    /// RewardManager.RollEnhanceOption()
    /// 로직을 그대로 사용한다.
    ///
    /// IMPORTANT:
    ///
    /// 이 함수가 나중에 멀티에서 가장 먼저 서버 쪽으로 이동해야 한다.
    ///
    /// 이유:
    /// 각 클라이언트가 Random을 따로 돌리면
    ///
    /// Player A:
    ///     Attack / Poison / Heal
    ///
    /// Player B:
    ///     Burn / Shield / Draw
    ///
    /// 처럼 서로 다른 후보가 생길 수 있기 때문이다.
    ///
    /// 따라서 최종 멀티에서는:
    ///
    /// 서버가 한 번 생성
    ///        ↓
    /// 모든 필요한 Client에게 같은 데이터 전달
    ///
    /// 구조가 되어야 한다.
    /// </summary>
    private CardUpgrade[] GenerateCandidatesLocal()
    {
        CardUpgrade[] result = new CardUpgrade[CandidateCount];

        for (int i = 0; i < result.Length; i++)
        {
            result[i] = RewardManager.RollEnhanceOption();
        }

        return result;
    }


    // ---------------------------------------------------------
    // 2. 후보 데이터 저장
    // ---------------------------------------------------------

    /// <summary>
    /// 생성되었거나 서버에서 전달받은 후보 데이터를 저장한다.
    ///
    /// 이 함수는 네트워크 여부와 관계없이 그대로 재사용 가능하다.
    /// </summary>
    private void StoreCandidates(CardUpgrade[] candidates)
    {
        _candidates = candidates;
    }


    // ---------------------------------------------------------
    // 3. RewardDisplay 생성
    // ---------------------------------------------------------

    /// <summary>
    /// 후보 수만큼 RewardDisplay를 생성한다.
    ///
    /// Display 자체는 네트워크 객체일 필요가 없다.
    ///
    /// 이유:
    /// RewardDisplay는 게임 상태를 결정하는 객체가 아니라
    /// 이미 결정된 데이터를 화면에 보여주는 로컬 UI이기 때문이다.
    ///
    /// 즉 멀티에서도 각 Client가 자기 화면에 로컬 Instantiate하면 된다.
    /// </summary>
    private void CreateDisplays()
    {
        ClearDisplays();

        if (_candidates == null || _candidates.Length == 0)
            return;

        if (rewardDisplayPrefab == null)
        {
            rewardDisplayPrefab =
                Resources.Load<GameObject>("Prefabs/RewardDisplay");
        }

        if (rewardDisplayPrefab == null)
        {
            Debug.LogError("RewardDisplay prefab을 찾을 수 없습니다.");
            return;
        }


        _displays = new RewardDisplay[_candidates.Length];


        for (int i = 0; i < _candidates.Length; i++)
        {
            GameObject obj = Instantiate(
                rewardDisplayPrefab,
                transform
            );

            RewardDisplay display =
                obj.GetComponent<RewardDisplay>();

            _displays[i] = display;
        }


        // -----------------------------------------------------
        // Display 위치 배치
        //
        // 우선 싱글 RewardManager와 비슷하게
        // 좌 / 중앙 / 우 형태로 배치한다.
        //
        // 나중에는 LayoutGroup 또는 별도의 UI Layout 시스템으로
        // 분리해도 된다.
        // -----------------------------------------------------

        float spacing = 3f;

        for (int i = 0; i < _displays.Length; i++)
        {
            if (_displays[i] == null)
                continue;

            float x =
                (i - (_displays.Length - 1) / 2f)
                * spacing;

            _displays[i].transform.localPosition =
                new Vector3(x, 0f, 0f);
        }
    }


    // ---------------------------------------------------------
    // 4. Display에 후보 데이터 전달
    // ---------------------------------------------------------

    /// <summary>
    /// 저장된 후보 데이터를 실제 RewardDisplay에 넣는다.
    ///
    /// 핵심:
    ///
    /// 데이터 결정
    ///     ↓
    /// _candidates
    ///     ↓
    /// RewardDisplay.SetUpgrade()
    ///     ↓
    /// EffectDisplay
    ///     ↓
    /// 화면 표시
    ///
    /// 따라서 Display는 후보를 랜덤 생성하지 않는다.
    /// 이미 결정된 결과만 받아서 보여준다.
    /// </summary>
    private void PushCandidateDataToDisplays()
    {
        if (_candidates == null || _displays == null)
            return;

        int count =
            Mathf.Min(_candidates.Length, _displays.Length);

        for (int i = 0; i < count; i++)
        {
            if (_displays[i] == null)
                continue;

            // 필요하다면 Init을 먼저 호출.
            //
            // 강화 후보의 경우 기존 RewardManager에서는
            // 빈 Label로 RewardDisplay를 초기화하고
            // SetUpgrade()를 호출한다.
            //
            // 실제 RewardDisplay.Init()의 정확한 시그니처에 맞춰
            // 여기 부분은 조정한다.

            _displays[i].SetUpgrade(_candidates[i]);
        }
    }


    // ---------------------------------------------------------
    // 5. 좌 / 우 선택
    // ---------------------------------------------------------

    /// <summary>
    /// 선택 커서를 좌/우로 이동.
    ///
    /// delta:
    /// -1 = 왼쪽
    /// +1 = 오른쪽
    ///
    /// 이 부분 역시 네트워크 동기화가 반드시 필요한 로직은 아니다.
    ///
    /// 플레이어가 "어디를 보고 있는지"까지 다른 플레이어에게
    /// 실시간으로 보여줄 필요가 없다면 완전히 로컬 처리해도 된다.
    /// </summary>
    public void MoveSelection(int delta)
    {
        if (_selectionConfirmed)
            return;

        if (_candidates == null || _candidates.Length == 0)
            return;


        int count = _candidates.Length;

        _selectedIndex =
            ((_selectedIndex + delta) % count + count)
            % count;


        RefreshSelection();
    }


    /// <summary>
    /// 현재 선택된 Display에 Highlight 표시.
    ///
    /// 네트워크와 무관한 순수 UI 로직.
    /// </summary>
    private void RefreshSelection()
    {
        if (_displays == null)
            return;

        for (int i = 0; i < _displays.Length; i++)
        {
            _displays[i]?.SetSelected(
                i == _selectedIndex
            );
        }
    }


    // ---------------------------------------------------------
    // 6. 선택 확정
    // ---------------------------------------------------------

    /// <summary>
    /// 현재 선택된 후보를 확정한다.
    ///
    /// 현재 로컬 프로토타입:
    ///
    /// _selectedIndex
    ///      ↓
    /// _candidates[_selectedIndex]
    ///      ↓
    /// ApplyConfirmedSelection()
    ///
    ///
    /// 멀티:
    ///
    /// _selectedIndex
    ///      ↓
    /// 서버에 "나는 N번 선택"
    ///      ↓
    /// 서버 검증
    ///      ↓
    /// 서버에서 최종 적용
    ///
    /// 로 변경한다.
    /// </summary>
    public void ConfirmSelection()
    {
        if (_selectionConfirmed)
            return;

        if (_candidates == null ||
            _candidates.Length == 0)
            return;


        _selectionConfirmed = true;


        CardUpgrade selected =
            _candidates[_selectedIndex];


        // -----------------------------------------------------
        // 현재 로컬 테스트
        // -----------------------------------------------------

        ApplyConfirmedSelection(selected);


        // -----------------------------------------------------
        // TODO [MULTI]
        //
        // 나중에는 ApplyConfirmedSelection()을
        // Client가 직접 호출하지 않는 것이 좋다.
        //
        // 대신:
        //
        // SendSelectionToServer(_selectedIndex);
        //
        // 형태로 서버에게 선택 index만 보낸다.
        //
        // 서버는 자신이 가지고 있는 후보 배열에서:
        //
        // CardUpgrade selected =
        //     serverCandidates[selectedIndex];
        //
        // 를 꺼내서 실제 게임 데이터에 적용한다.
        //
        // Client가 CardUpgrade 전체를 보내는 것보다
        // index만 보내는 편이 검증하기 쉽다.
        // -----------------------------------------------------
    }


    // ---------------------------------------------------------
    // 선택 결과 적용
    // ---------------------------------------------------------

    /// <summary>
    /// 확정된 선택지를 실제 게임에 적용하는 자리.
    ///
    /// 아직 멀티 구조가 정해지지 않았으므로
    /// 실제 적용 코드 대신 TODO만 둔다.
    ///
    /// 예:
    ///
    /// Player의 특정 기능 강화
    /// Card에 Upgrade 적용
    /// Character 능력 추가
    /// 시작 특성 추가
    /// 등.
    ///
    /// 최종 멀티에서는 서버 권한으로 실행하는 것이 안전하다.
    /// </summary>
    private void ApplyConfirmedSelection(CardUpgrade selected)
    {
        if (selected == null)
            return;


        Debug.Log(
            $"Reward 선택 완료. index = {_selectedIndex}"
        );


        // TODO:
        // 실제 선택 결과 적용.
        //
        // 예:
        //
        // player.ApplyUpgrade(selected);
        //
        // 또는
        //
        // GameManager.Instance.ApplyStartFeature(selected);


        ClearDisplays();
    }


    // ---------------------------------------------------------
    // 정리
    // ---------------------------------------------------------

    /// <summary>
    /// 화면에 만들어진 RewardDisplay들을 제거한다.
    ///
    /// 다음 선택 화면을 다시 열었을 때
    /// 이전 Display가 남아 중복 생성되는 것을 막는다.
    /// </summary>
    private void ClearDisplays()
    {
        if (_displays == null)
            return;


        foreach (RewardDisplay display in _displays)
        {
            if (display != null)
                Destroy(display.gameObject);
        }


        _displays = null;
    }


    // ---------------------------------------------------------
    // 아래는 향후 Network 구현 시 들어갈 자리
    // ---------------------------------------------------------

    /*
    ====================================================================
    TODO [MULTIPLAYER]
    ====================================================================


    1. 후보 생성 요청

        Client
            ↓
        Server

        RequestCandidatesServerRpc();


    ------------------------------------------------------------


    2. 서버가 후보 생성

        Server:

        CardUpgrade[] candidates = new CardUpgrade[3];

        for (...)
            candidates[i] = RollEnhanceOption();


    ------------------------------------------------------------


    3. 후보 전달

        Server
            ↓
        Client

        ReceiveCandidatesClientRpc(...);


        Client에서는 전달받은 데이터를:

        StoreCandidates(candidates);
        CreateDisplays();
        PushCandidateDataToDisplays();
        RefreshSelection();

        순서로 처리한다.


    ------------------------------------------------------------


    4. 플레이어 좌/우 이동

        이것은 기본적으로 Client Local 처리.

        MoveSelection(-1);
        MoveSelection(+1);

        다른 플레이어에게 내 Highlight를 보여줘야 하는 경우에만
        Network 동기화를 추가한다.


    ------------------------------------------------------------


    5. 선택 확정

        Client:

        ConfirmSelection()

            ↓

        SendSelectionServerRpc(_selectedIndex);


    ------------------------------------------------------------


    6. 서버 검증

        Server:

        if (selectedIndex < 0 ||
            selectedIndex >= serverCandidates.Length)
            return;


        CardUpgrade selected =
            serverCandidates[selectedIndex];


    ------------------------------------------------------------


    7. 실제 적용

        서버가 selected를 Player 데이터에 적용.


    ------------------------------------------------------------


    8. 결과 동기화

        Server
            ↓
        All Clients

        "Player A가 1번 기능을 선택함"
        "선택 단계가 완료됨"

        등의 결과만 Client에게 전달.


    ====================================================================


    중요한 원칙


    [서버가 가져갈 것]

    - Random 후보 생성
    - 후보의 authoritative 데이터
    - 최종 선택 검증
    - 실제 게임 데이터 변경


    [Client가 가져갈 것]

    - RewardDisplay Instantiate
    - EffectDisplay
    - Highlight
    - 좌 / 우 커서
    - 선택 입력
    - 애니메이션 / 사운드


    즉:


           SERVER
        후보 생성 / 확정
             │
             │ 데이터
             ▼
           CLIENT
        Display 생성
        좌우 선택
             │
             │ index
             ▼
           SERVER
        선택 검증 / 적용


    ====================================================================
    */
}