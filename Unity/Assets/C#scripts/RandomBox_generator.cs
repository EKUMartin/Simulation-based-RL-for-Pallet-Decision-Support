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

    // 🌟 타겟 선반(targetShelf)을 매개변수로 받을 수 있도록 수정
    public GameObject GenerateBox(int index, Vector3 predefinedSize, int targetShelfID)
    {
        if (boxPrefab == null) return null;

        /*
        // 🌟 타겟 선반이 전달되었다면, 남은 빈 공간에 딱 맞는 크기 역산
        if (targetShelf != null)
        {
            float w = predefinedSize.x;
            float h = predefinedSize.y;
            float d = predefinedSize.z;
        }
        else
        {
            // 기존 랜덤 방식
            w = Random.Range(0.2f, 0.8f);
            h = Random.Range(0.2f, 0.8f);
            d = Random.Range(0.2f, 0.8f);
        }
        */

        // 부피에 비례하는 무게 계산 (밀도 상수 30f)
        //float weight = (w * h * d) * 30f;

        GameObject box = Instantiate(boxPrefab);

        // 좌표 분산 로직 (기존 코드 그대로 유지)
        float angle = index * (Mathf.PI * 2f / 5f);
        float radius = index * 0.4f;

        float currentX = 0f + (Mathf.Cos(angle) * radius);
        float currentZ = -16f + (Mathf.Sin(angle) * radius);

        box.transform.position = new Vector3(currentX, 1.5f, currentZ);
        box.transform.localScale = predefinedSize;
        //box.transform.localScale = new Vector3(w, h, d);
        /*
        Box boxScript = box.GetComponent<Box>();
        if (boxScript == null) 
        {
            boxScript = box.AddComponent<Box>();
        }
        */

        Box boxScript = box.GetComponent<Box>();
        if (boxScript == null)
        {
            boxScript = box.AddComponent<Box>();
        }

        boxScript.size = predefinedSize;
        //boxScript.size = new Vector3(w, h, d);
        boxScript.weight = (predefinedSize.x * predefinedSize.y * predefinedSize.z) * 30f;
        //boxScript.weight = weight;

        boxScript.targetShelfID = targetShelfID;

        // 우선 0번으로 초기화 (Agent에서 부피에 따라 1번 그룹 혹은 2번 그룹으로 보낼 예정)
        //boxScript.targetShelfID = Random.Range(0, 4); 

        return box;
    }
}