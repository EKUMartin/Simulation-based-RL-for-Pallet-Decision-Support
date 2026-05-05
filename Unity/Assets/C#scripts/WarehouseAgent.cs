using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using System.Collections.Generic;
public class WarehouseAgent : Agent
{
    [Header("4개 층의 GridManager 연결")]
    //Inspector 창에서 각각의 floor의 GridManager를 순서대로 연결
    public GridManager shelf0_Floor1; // ID: 0
    public GridManager shelf0_Floor2; // ID: 1
    public GridManager shelf1_Floor1; // ID: 2
    public GridManager shelf1_Floor2; // ID: 3

    private GridManager[] allFloors;  // 4개 층을 담을 배열

    [Header("박스 생성 관련")]
    public RandomBox_generator boxManager;
    public int totalBoxesToPlace = 8;

    // 씬에 생성된 '모든 박스'를 담아두는 쓰레기통 (에피소드 리셋 시 한 번에 비우기 위함)
    private List<GameObject> allSpawnedBoxes = new List<GameObject>();
    // 에이전트가 배치해야 할 8개의 타겟 박스가 담긴 '대기열'
    private List<Box> boxesToPlace = new List<Box>();

    // 현재 에이전트의 손에 들려있는(배치할 차례인) 박스의 순서(인덱스)
    private int currentBoxIndex = 0;
    private int placedBoxCount = 0;

    [HideInInspector] public Vector3 currentBoxSize;
    [HideInInspector] public int targetShelfID;

    public override void Initialize()
    {
        //4개의 GridManager를 배열로
        allFloors = new GridManager[] { shelf0_Floor1, shelf0_Floor2, shelf1_Floor1, shelf1_Floor2 };
    }

    public override void OnEpisodeBegin()
    {
        // 1. 대청소: 이전 에피소드에서 씬에 남겨진 모든 박스를 파괴하고 리스트를 비웁니다.
        foreach (var box in allSpawnedBoxes)
        {
            if (box != null) Destroy(box);
        }
        allSpawnedBoxes.Clear();
        boxesToPlace.Clear();
        
        currentBoxIndex = 0;
        placedBoxCount = 0;

        // 2. 초기 맵 세팅: 4개 층 모두 그리드 데이터를 리셋하고, 0~3개의 랜덤 박스를 미리 깔아둡니다.
        foreach (var floor in allFloors)
        {
            floor.ScanInitialBoxes(); // (이 함수 안에 기존 박스를 지우는 로직이 없다면 그리드 맵만 0으로 초기화됨)
            SpawnInitialRandomBoxes(floor);
        }

        // 3. 타겟 박스 준비: 이번 에피소드에서 파이썬이 배치해야 할 8개의 박스를 한 번에 생성합니다. (SpawnNextBox 대체)
        GenerateBoxesToPlace();
    }

    // --- 요구사항 1: 각 층에 0~3개의 랜덤 초기 박스 생성 ---
    private void SpawnInitialRandomBoxes(GridManager targetGrid)
    {
        int randomBoxCount = Random.Range(0, 3); // 0,1,2 중 랜덤

        for (int i = 0; i < randomBoxCount; i++)
        {
            // BoxManager에게 박스 생성을 요청합니다. (index 0을 주어 중심점에 겹치게 생성)
            GameObject initBox = boxManager.GenerateBox(0); 
            Box boxScript = initBox.GetComponent<Box>();

            // 해당 층의 랜덤한 그리드 좌표에 배치할 수 있는지 찔러봅니다.
            int randX = Random.Range(0, targetGrid.gridWidth);
            int randZ = Random.Range(0, targetGrid.gridDepth);

            Vector3 finalPosition;
            bool isSuccess = targetGrid.TryPlaceBox(randX, randZ, boxScript.size, out finalPosition);

            if (isSuccess)
            {
                // 공간이 비어있다면 해당 좌표로 순간이동 시키고 추적 리스트에 넣습니다.
                initBox.transform.position = finalPosition;
                allSpawnedBoxes.Add(initBox); 
            }
            else
            {
                // 공간이 겹친다면 이 초기 상자는 쿨하게 파괴합니다.
                Destroy(initBox);
            }
        }
    }

    // --- 요구사항 2: 에이전트가 배치할 8개의 박스 한 번에 생성 ---
    private void GenerateBoxesToPlace()
    {
        for (int i = 0; i < totalBoxesToPlace; i++)
        {
            // BoxManager의 GenerateBox(index)를 호출하면 알아서 소용돌이 형태로 배치되어 나옵니다.
            GameObject newBox = boxManager.GenerateBox(i + 1);
            Box boxScript = newBox.GetComponent<Box>();
            
            boxesToPlace.Add(boxScript); // 대기열에 추가
            allSpawnedBoxes.Add(newBox); // 청소 리스트에 추가
        }
    }

    //Observation 전달
    public override void CollectObservations(VectorSensor sensor)
    {
        //4개 층 Grid 정보 전달
        for (int i = 0; i < allFloors.Length; i++)
        {
            //각 층(GridManager)에서 현재 grid 상태 가져옴
            float[] floorData = allFloors[i].GetGridData(); 
            
            //가져온 숫자들을 순서대로 파이썬으로 보냄
            foreach (float data in floorData)
            {
                sensor.AddObservation(data);
            }
        }
        // 2. 현재 차례의 박스 정보 전달
        if (currentBoxIndex < boxesToPlace.Count)
        {
            Box currentBoxScript = boxesToPlace[currentBoxIndex];
            sensor.AddObservation(currentBoxScript.size);         // 크기 정보 (x, y, z)
            sensor.AddObservation(currentBoxScript.targetShelfID); // 가야 할 선반 ID (무거우면 0, 가벼우면 1)
        }
        else
        {
            // 박스를 모두 배치해서 더 이상 볼 게 없더라도 배열 크기를 맞추기 위해 더미 값을 넣습니다.
            sensor.AddObservation(Vector3.zero);
            sensor.AddObservation(0);
        }
    }

    //행동 결정했을 때 <- 파이썬이랑 어떤식으로 주고받을지 정하고 수정해야됨요!!!!!!!!!!!
    // --- 파이썬에서 행동(Action) 명령을 받았을 때 ---
    public override void OnActionReceived(ActionBuffers actions)
    {
        // 8개를 다 배치했다면 더 이상 행동하지 않음
        if (currentBoxIndex >= boxesToPlace.Count) return;

        // 대기열에서 이번 턴에 배치할 상자(현재 인덱스)의 정보를 가져옵니다.
        Box currentBoxScript = boxesToPlace[currentBoxIndex];
        Vector3 currentBoxSize = currentBoxScript.size;
        int targetShelfID = currentBoxScript.targetShelfID;

        // 파이썬의 Action 데이터 파싱
        float actionX = actions.ContinuousActions[0];
        float actionZ = actions.ContinuousActions[1];
        int floorAction = actions.DiscreteActions[0]; // 0이면 1층, 1이면 2층

        // 목표 선반(0 또는 1)과 행동 층수(0 또는 1)를 조합해 최종 목표 GridManager(0~3)를 찾습니다.
        int finalGridIndex = (targetShelfID * 2) + floorAction; 
        GridManager targetGrid = allFloors[finalGridIndex];

        // 파이썬에서 온 -1.0 ~ 1.0 값을 유니티 그리드 인덱스 좌표로 변환
        int gridX = Mathf.FloorToInt((actionX + 1f) / 2f * targetGrid.gridWidth);
        int gridZ = Mathf.FloorToInt((actionZ + 1f) / 2f * targetGrid.gridDepth);

        // 그리드 밖으로 튀어나가지 않게 안전장치
        gridX = Mathf.Clamp(gridX, 0, targetGrid.gridWidth - 1);
        gridZ = Mathf.Clamp(gridZ, 0, targetGrid.gridDepth - 1);

        // 유니티 상에서 해당 좌표에 상자를 넣을 수 있는지 검증
        Vector3 finalPosition;
        bool isSuccess = targetGrid.TryPlaceBox(gridX, gridZ, currentBoxSize, out finalPosition);

        if (isSuccess)
        {
            // [성공]
            // 상자를 선반 위로 순간이동시킵니다.
            currentBoxScript.transform.position = finalPosition; 
            
            placedBoxCount++;
            currentBoxIndex++; // 다음 박스로 차례(인덱스)를 넘깁니다. (SpawnNextBox의 역할을 이 한 줄이 대신함!)
            AddReward(1.0f);   // 배치 성공 보상

            // 8개를 전부 성공적으로 배치했다면 에피소드 클리어
            if (placedBoxCount >= totalBoxesToPlace)
            {
                AddReward(5.0f); 
                EndEpisode(); 
            }
        }
        else
        {
            // [실패] 겹치거나 튀어나가는 자리에 두려고 한 경우
            SetReward(-1.0f); // 페널티 부여
            EndEpisode();     // 즉시 에피소드 강제 종료 (리셋됨)
        }
    }
}