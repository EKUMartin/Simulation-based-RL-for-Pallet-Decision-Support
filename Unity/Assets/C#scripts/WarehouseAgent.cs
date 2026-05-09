using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using System.Collections.Generic;

public class WarehouseAgent : Agent
{
    [Header("4개 층의 GridManager 연결")]
    public GridManager shelf0_Floor1; // ID: 0
    public GridManager shelf0_Floor2; // ID: 1
    public GridManager shelf1_Floor1; // ID: 2
    public GridManager shelf1_Floor2; // ID: 3

    private GridManager[] allFloors;  

    [Header("박스 생성 관련")]
    public RandomBox_generator boxManager;
    public int totalBoxesToPlace = 8;

    private List<GameObject> allSpawnedBoxes = new List<GameObject>();
    private List<Box> boxesToPlace = new List<Box>();

    private int currentBoxIndex = 0;
    private int placedBoxCount = 0;
    private int selectedFloorIndex = 0;

    public override void Initialize()
    {
        allFloors = new GridManager[] { shelf0_Floor1, shelf0_Floor2, shelf1_Floor1, shelf1_Floor2 };
    }

    public override void OnEpisodeBegin()
    {
        // 1. 대청소: 유령 상자 병목을 막기 위해 SetActive(false)로 즉시 끕니다.
        foreach (var box in allSpawnedBoxes)
        {
            if (box != null) 
            {
                box.SetActive(false);
                Destroy(box);
            }
        }
        allSpawnedBoxes.Clear();
        boxesToPlace.Clear();
        
        currentBoxIndex = 0;
        placedBoxCount = 0;
        totalBoxesToPlace = 8;

        // 2. 초기 맵 세팅: 그리드 리셋 및 랜덤 박스 배치
        foreach (var floor in allFloors)
        {
            floor.ResetGrid(); 
            SpawnInitialRandomBoxes(floor);
        }

        // 3. 타겟 박스 준비 (8개 세트 생성)
        GenerateBoxesToPlace();
    }

    private void SpawnInitialRandomBoxes(GridManager targetGrid)
    {
        // 0, 1, 2, 3개 중 하나를 랜덤으로 뽑습니다.
        int randomBoxCount = Random.Range(0, 3); 

        for (int i = 0; i < randomBoxCount; i++)
        {
            GameObject initBox = boxManager.GenerateBox(0); 
            
            // 초기 상자는 배경처럼 보이게 흰색(또는 원하는 색)으로 칠합니다.
            Renderer boxRenderer = initBox.GetComponent<Renderer>();
            if (boxRenderer != null)
            {
                boxRenderer.material.color = Color.white; 
            }
            
            Box boxScript = initBox.GetComponent<Box>();

            int randX = Random.Range(0, targetGrid.gridWidth);
            int randZ = Random.Range(0, targetGrid.gridDepth);

            Vector3 finalPosition;
            if (targetGrid.TryPlaceBox(randX, randZ, boxScript.size, out finalPosition))
            {
                initBox.transform.position = finalPosition;
                allSpawnedBoxes.Add(initBox); 
            }
            else
            {
                initBox.SetActive(false);
                Destroy(initBox);
            }
        }
    }

    private void GenerateBoxesToPlace()
    {
        for (int i = 0; i < totalBoxesToPlace; i++)
        {
            GameObject newBox = boxManager.GenerateBox(i + 1);
            Box boxScript = newBox.GetComponent<Box>();
            
            boxesToPlace.Add(boxScript); 
            allSpawnedBoxes.Add(newBox); 
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
    //박스가 남아있을 때만 진짜 데이터를 보냄
        if (currentBoxIndex < boxesToPlace.Count) 
        {
            Box currentBoxScript = boxesToPlace[currentBoxIndex];
        
            float minUsage = float.MaxValue; 
            int bestFloorIndex = 0;          

        // 🌟 전체 창고(4개 층) 스캔 로직
            for (int i = 0; i < allFloors.Length; i++)
            {
                float[] floorData = allFloors[i].GetGridData();
            
                float currentUsage = 0f; 
                foreach (float v in floorData) currentUsage += v; 

                if (currentUsage < minUsage)
                {
                minUsage = currentUsage;
                bestFloorIndex = i;
                }
            }

            selectedFloorIndex = bestFloorIndex; // 유니티가 선택한 층 기억하기

        // 딱 1개 층의 데이터만 파이썬으로 쏨
            float[] selectedData = allFloors[selectedFloorIndex].GetGridData();
            foreach (float data in selectedData)
            {
                sensor.AddObservation(data);
            }
        
        // 박스 정보 전송 (크기 3 + 목적지 ID 1 = 총 4개)
            sensor.AddObservation(currentBoxScript.size);         
            sensor.AddObservation(currentBoxScript.targetShelfID); 
        }
        else
        {
        // 박스를 다 소진했을 때의 규격 맞추기용 더미 데이터
            int singleFloorSize = allFloors[0].gridWidth * allFloors[0].gridDepth;
            for (int i = 0; i < singleFloorSize; i++) sensor.AddObservation(0f);
        
            sensor.AddObservation(Vector3.zero);
            sensor.AddObservation(0);
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        Box currentBoxScript = boxesToPlace[currentBoxIndex];
        Vector3 currentBoxSize = currentBoxScript.size;

    // 🌟 파이썬이 보낸 정수 3개를 직통으로 받음
        int gridX = actions.DiscreteActions[0];          
        int gridZ = actions.DiscreteActions[1];          
        int rotationAction = actions.DiscreteActions[2]; 

    // 회전 처리
        if (rotationAction == 1)
        {
            currentBoxSize = new Vector3(currentBoxSize.z, currentBoxSize.y, currentBoxSize.x);
            currentBoxScript.size = currentBoxSize;
            currentBoxScript.transform.localScale = currentBoxSize; 
        }

    // 🌟 파이썬이 층을 정해주지 않아도, 유니티가 기억해둔 층으로 자동 지정
        GridManager targetGrid = allFloors[selectedFloorIndex];

        Vector3 finalPosition;
        if (targetGrid.TryPlaceBox(gridX, gridZ, currentBoxSize, out finalPosition))
        {
            currentBoxScript.transform.position = finalPosition; 
            placedBoxCount++;
            currentBoxIndex++; 
        }
        else
        {
            currentBoxScript.gameObject.SetActive(false); // 실패 시 무한루프 방지
            currentBoxIndex++; 
        }
        if (currentBoxIndex >= boxesToPlace.Count) 
        {
        int boxesToAdd = 8;
        for (int i = 0; i < boxesToAdd; i++)
        {
            // 인덱스가 겹치지 않게 현재까지 생성된 총 개수(boxesToPlace.Count)를 기준으로 생성
            GameObject newBox = boxManager.GenerateBox(boxesToPlace.Count + 1);
            Box boxScript = newBox.GetComponent<Box>();
            
            boxesToPlace.Add(boxScript); 
            allSpawnedBoxes.Add(newBox); 
        }

        totalBoxesToPlace += boxesToAdd; // 로그 확인용 누적 개수 갱신
        Debug.Log($"[계속 진행] 8개 배치 완료. 현재까지 총 {totalBoxesToPlace}개 생성됨. 선반 상태 유지!");
        }
    }
}