using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

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
    public GameObject boxPrefab;
    public Transform boxSpawnPoint;

    private GameObject currentBox;
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
        // 4개 층 모두 그리드 초기화 및 선반 위 박스, frame 스캔 (선반 위 박스 랜덤 생성하는 코드는 아직 미완)
        foreach (var floor in allFloors)
        {
            floor.ScanInitialBoxes(); 
        }
        
        placedBoxCount = 0; //성공 카운트 초기화
        
        //쥐고 있던 박스가 있다면 제거
        if (currentBox != null) Destroy(currentBox);

        SpawnNextBox(); // 첫 번째 박스 소환
    }

    //새로운 박스를 소환
    private void SpawnNextBox()
    {
        currentBox = Instantiate(boxPrefab, boxSpawnPoint.position, Quaternion.identity);
        Box boxScript = currentBox.GetComponent<Box>();
        
        if (boxScript != null)
        {
        
            boxScript.InitRandomBox(); 
            currentBoxSize = boxScript.size;
            targetShelfID = boxScript.targetShelfID;
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
        //현재 배치해야 할 박스의 크기 정보 x, y, z
        sensor.AddObservation(currentBoxSize); 

        //박스가 가야 할 목표 shelfID 0 or 1
        sensor.AddObservation(targetShelfID);
    }

    //행동 결정했을 때 <- 파이썬이랑 어떤식으로 주고받을지 정하고 수정해야됨요!!!!!!!!!!!
    public override void OnActionReceived(ActionBuffers actions)
    {
        //여기는 상황에 맞게 수정해야해
        //파이썬에서 내리는 명령 수신 ContinuousActions에서 선반의 x와 y비율을 줌
        float actionX = actions.ContinuousActions[0];
        float actionZ = actions.ContinuousActions[1];
        //DiscreteActions에서 1층 또는 2층을 줌
        int floorAction = actions.DiscreteActions[0];

        // targetShelfID는 0 또는 1. floorAction도 0 또는 1. 현재 잘못된 shelf에 배치할 확률은 0임.
        int finalGridIndex = (targetShelfID * 2) + floorAction; 
        GridManager targetGrid = allFloors[finalGridIndex];

        //좌표 계산
        int gridX = Mathf.FloorToInt((actionX + 1f) / 2f * targetGrid.gridWidth);
        int gridZ = Mathf.FloorToInt((actionZ + 1f) / 2f * targetGrid.gridDepth);

        gridX = Mathf.Clamp(gridX, 0, targetGrid.gridWidth - 1);
        gridZ = Mathf.Clamp(gridZ, 0, targetGrid.gridDepth - 1);

        // 
        Vector3 finalPosition;
        bool isSuccess = targetGrid.TryPlaceBox(gridX, gridZ, currentBoxSize, out finalPosition);

        if (isSuccess)
        {
            // --- 성공 시 ---
            currentBox.transform.position = finalPosition; // 순간이동
            currentBox = null;// 에이전트 손에서 놓음
            
            placedBoxCount++;
            AddReward(1.0f); // 1개 배치 성공 보상

            if (placedBoxCount >= 8)
            {
                AddReward(5.0f); // 8개 모두 성공 시 추가 보상 (에피소드 하나 클리어)
                EndEpisode(); // 에피소드 종료
            }
            else
            {
                SpawnNextBox(); // 다음 박스 생성
            }
        }
        else
        {
            // --- 실패 시 ---
            SetReward(-1.0f); // 박스 놓을 수 없는 위치에 박스 놓을 때 페널티
            EndEpisode(); // 즉시 에피소드 종료 및 리셋
        }
    }
}