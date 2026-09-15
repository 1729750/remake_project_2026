using System.Collections.Generic;
using UnityEngine;

// 특정 프리팹을 특정 부모 밑에서 계속 재사용하기 위한 범용 오브젝트 풀. 매번 Instantiate/Destroy하는
// 대신, 비활성(SetActive(false))인 기존 인스턴스가 있으면 그걸 재활성화해서 돌려주고, 없을 때만 새로
// Instantiate해서 풀에 등록한다. Get()으로 꺼내 쓰고, 다 쓴 뒤에는 Release()(또는 한꺼번에
// ReleaseAll())로 SetActive(false)만 해서 풀에 반납한다 — 실제로 파괴하지 않는다.
public class GameObjectPool
{
    private readonly GameObject _prefab;
    private readonly Transform _parent;
    private readonly List<GameObject> _pool = new List<GameObject>();

    public GameObjectPool(GameObject prefab, Transform parent)
    {
        _prefab = prefab;
        _parent = parent;
    }

    // 비활성 상태인 기존 인스턴스를 재활성화해서 반환하고, 없으면 새로 Instantiate해서 풀에 등록한다.
    public GameObject Get()
    {
        foreach (GameObject go in _pool)
        {
            if (go == null) continue; // 씬 전환 등으로 이미 파괴된 항목은 건너뛴다.
            if (!go.activeSelf)
            {
                go.SetActive(true);
                return go;
            }
        }

        GameObject created = Object.Instantiate(_prefab, _parent);
        _pool.Add(created);
        return created;
    }

    // go를 비활성화해 풀에 반납한다(파괴하지 않는다).
    public void Release(GameObject go)
    {
        if (go != null) go.SetActive(false);
    }

    // 풀이 관리하는 인스턴스를 전부 한꺼번에 반납한다 — 배틀 재시작처럼 "전부 다시 쓸 것"이 정해진
    // 시점에 개별 Release 대신 쓴다.
    public void ReleaseAll()
    {
        foreach (GameObject go in _pool)
        {
            if (go != null) go.SetActive(false);
        }
    }
}
