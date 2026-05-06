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
        for (int i = 0; i < allFloors.Length; i++)
        {
            float[] floorData = allFloors[i].GetGridData(); 
            foreach (float data in floorData)
            {
                sensor.AddObservation(data);
            }
        }
        
        if (currentBoxIndex < boxesToPlace.Count)
        {
            Box currentBoxScript = boxesToPlace[currentBoxIndex];
            sensor.AddObservation(currentBoxScript.size);         
            sensor.AddObservation(currentBoxScript.targetShelfID); 
        }
        else
        {
            sensor.AddObservation(Vector3.zero);
            sensor.AddObservation(0);
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        // 리필 안전장치
        if (currentBoxIndex >= boxesToPlace.Count)
        {
            boxesToPlace.Clear();
            GenerateBoxesToPlace(); 
            currentBoxIndex = 0;
        }

        Box currentBoxScript = boxesToPlace[currentBoxIndex];
        Vector3 currentBoxSize = currentBoxScript.size;

        float actionX = actions.ContinuousActions[0];
        float actionZ = actions.ContinuousActions[1];
        
        // 🌟 [핵심] 파이썬 매니저가 결정한 회전 여부와 층수(0 또는 1)를 수신합니다.
        int rotationAction = actions.DiscreteActions[0]; 
        int floorAction = actions.DiscreteActions[1];    

        // 회전 처리
        if (rotationAction == 1)
        {
            currentBoxSize = new Vector3(currentBoxSize.z, currentBoxSize.y, currentBoxSize.x);
            currentBoxScript.size = currentBoxSize;
            currentBoxScript.transform.localScale = currentBoxSize; 
        }

        // 🌟 낡은 5.0kg 로직은 완전 삭제! 파이썬이 지시한 층(floorAction)을 바로 따릅니다.
        int finalGridIndex = (currentBoxScript.targetShelfID * 2) + floorAction; 
        GridManager targetGrid = allFloors[finalGridIndex];

        int gridX = Mathf.Clamp(Mathf.FloorToInt((actionX + 1f) / 2f * targetGrid.gridWidth), 0, targetGrid.gridWidth - 1);
        int gridZ = Mathf.Clamp(Mathf.FloorToInt((actionZ + 1f) / 2f * targetGrid.gridDepth), 0, targetGrid.gridDepth - 1);

        Vector3 finalPosition;
        if (targetGrid.TryPlaceBox(gridX, gridZ, currentBoxSize, out finalPosition))
        {
            // 성공
            currentBoxScript.transform.position = finalPosition; 
            placedBoxCount++;
            currentBoxIndex++; 
            AddReward(0.1f); 
        }
        else
        {
            // 실패하더라도 에피소드를 끝내지 않고 다음 박스로 스킵!
            //SetReward(-0.1f); 
            Destroy(currentBoxScript.gameObject); 
            currentBoxIndex++; 
        }

        // 8개 모두 소진 시 무한 리필
        if (currentBoxIndex >= boxesToPlace.Count)
        {
            //AddReward(1.0f); 
            boxesToPlace.Clear();
            GenerateBoxesToPlace();
            currentBoxIndex = 0;
        }
    }
}