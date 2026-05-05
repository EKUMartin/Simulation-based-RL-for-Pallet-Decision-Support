using UnityEngine;
using System.Collections;

public class RandomBox_generator : MonoBehaviour
{
    [Header("박스 프리팹 연결")]
    public GameObject boxPrefab; //inspector에서 박스 프래팹 연결해야됨요

    /* 수정 (사용제외)
    [Header("Generation Settings")]
    private int maxSpawnCount = 8;   // 작업 당 생성할 최대 박스 개수
    public float interval = 0.3f;    // 생성 간격 (초 단위)
    private bool isWorking = false;  // 중복 실행 방지용 상태 변수

    // 실시간 데이터 공유 변수 (강화학습 및 외부 스크립트 참조용)
    // lastWeight의 단위는 kg 입니다.
    public static float lastW, lastH, lastD, lastWeight;
    public static bool isDataNew = false; 

    void Update()
    {
        if ((Input.GetKeyDown(KeyCode.G) || Input.GetKeyDown(KeyCode.Space)) && !isWorking)
        {
            StartGeneration();
        }
    }

    // 강화학습 팀이 에피소드 시작 시 호출할 함수
    public void StartGeneration()
    {
        if (!isWorking)
        {
            StartCoroutine(AutoSpawnByLimit());
        }
    }

    IEnumerator AutoSpawnByLimit()
    {
        isWorking = true;
        Debug.Log($"[Process Start] temporary_buffer 구역에 {maxSpawnCount}개 생성 시작");

        for (int i = 0; i < maxSpawnCount; i++)
        {
            GenerateBox(i + 1);
            yield return new WaitForSeconds(interval);
        }

        isWorking = false;
        Debug.Log("[Process End] 모든 박스 생성 완료");
    }
    */

    public GameObject GenerateBox(int index) //박스 생성할 때 호출하는 함수
    {
        if (boxPrefab == null) return null;

        //랜덤 규격 결정 (0.2 ~ 1.0)
        float w = Random.Range(0.2f, 1.0f);
        float h = Random.Range(0.2f, 1.0f);
        float d = Random.Range(0.2f, 1.0f);

        //부피에 비례하는 무게 계산 (밀도 상수 10f)
        float weight = (w * h * d) * 10f;

        //프리팹 인스턴스 생성
        GameObject box = Instantiate(boxPrefab);

        // ✅ [좌표 분산 로직 수정] 소용돌이(Spiral) 형태로 빙글빙글 퍼지게 배치
        // index가 커질수록 회전 각도(angle)와 중심으로부터의 거리(radius)가 증가함
        float angle = index * (Mathf.PI * 2f / 5f); // 각도: 약 72도씩 회전
        float radius = index * 0.4f;                // 거리: 0.4m씩 바깥으로 퍼짐

        float currentX = 0f + (Mathf.Cos(angle) * radius);
        float currentZ = -16f + (Mathf.Sin(angle) * radius);

        // 높이(Y)는 1.5 유지
        box.transform.position = new Vector3(currentX, 1.5f, currentZ);
        box.transform.localScale = new Vector3(w, h, d);
        
        /* 수정 (사용제외)
        //정적 변수에 최신 데이터 기록
        lastW = w;
        lastH = h;
        lastD = d;
        lastWeight = weight;
        isDataNew = true;

        // 콘솔 로그 출력 (무게 단위 kg 명시)
        Debug.Log($"[Memory Update] Box {index} -> W:{w:F2}, H:{h:F2}, D:{d:F2}, Weight:{weight:F2}kg");
        Debug.Log($"[Size Check] Box X length: {w:F2}");
        Debug.Log($"[Size Check] Box Y length: {h:F2}");
        */
        //Box 스크립트 연동 및 에이전트 전달용 데이터 세팅
        Box boxScript = box.GetComponent<Box>();
        if (boxScript == null) 
        {
            boxScript = box.AddComponent<Box>();
        }
        
        boxScript.size = new Vector3(w, h, d);
        boxScript.weight = weight;
        // 1층/2층 구분용 타겟 선반 ID 지정 로직 
        // 예: 무게가 5.0kg 이상이면 1층(ID:0), 가벼우면 2층(ID:1)
        boxScript.targetShelfID = (weight >= 5.0f) ? 0 : 1; 

        // 생성된 박스 오브젝트를 WarehouseAgent로 반환
        return box;
    }
}