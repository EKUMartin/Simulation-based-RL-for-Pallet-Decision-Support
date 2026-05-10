using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using System.Collections.Generic;

public class WarehouseAgent : Agent
{
    [Header("모든 선반 연결 (층 구분 없이 등록)")]
    public List<GridManager> allShelves = new List<GridManager>(); 

    [Header("박스 생성 관련")]
    public RandomBox_generator boxManager;
    
    private List<GameObject> allSpawnedBoxes = new List<GameObject>();
    private List<Box> boxesToPlace = new List<Box>();
    private int currentBoxIndex = 0;
    private int selectedShelfIndex = 0; // 이번 턴에 파이썬에게 보여준 선반 번호

    public override void OnEpisodeBegin()
    {
        // 1. 기존 상자 청소
        foreach (var box in allSpawnedBoxes) { if (box != null) Destroy(box); }
        allSpawnedBoxes.Clear();
        boxesToPlace.Clear();
        currentBoxIndex = 0;

        // 2. 모든 선반 리셋
        foreach (var shelf in allShelves) { shelf.ResetGrid(); }

        // 3. 배치할 상자들 생성 (층 정보 없이 크기만 생성)
        GenerateBoxes();
    }

    private void GenerateBoxes()
    {
        for (int i = 0; i < 8; i++)
        {
            GameObject newBox = boxManager.GenerateBox(i);
            boxesToPlace.Add(newBox.GetComponent<Box>());
            allSpawnedBoxes.Add(newBox);
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (currentBoxIndex >= boxesToPlace.Count) return;

        //가장 비어있는 층 하나를 '선택'해서 그 지도만 보냄
        int bestShelf = 0;
        float minUsage = float.MaxValue;
        for (int i = 0; i < allShelves.Count; i++)
        {
            float usage = GetUsage(allShelves[i]);
            if (usage < minUsage) { minUsage = usage; bestShelf = i; }
        }
        selectedShelfIndex = bestShelf;

        // 파이썬에게는 이 층의 지도만 전송
        float[] gridData = allShelves[selectedShelfIndex].GetGridData();
        foreach (float data in gridData) sensor.AddObservation(data);

        // 상자 크기 정보 전송
        sensor.AddObservation(boxesToPlace[currentBoxIndex].size);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (currentBoxIndex >= boxesToPlace.Count) return;

        Box currentBox = boxesToPlace[currentBoxIndex];
        int x = actions.DiscreteActions[0];
        int z = actions.DiscreteActions[1];
        int rot = actions.DiscreteActions[2];

        Vector3 size = currentBox.size;
        if (rot == 1) size = new Vector3(size.z, size.y, size.x);

        //파이썬이 준 좌표를 '아까 그 층'에 그대로 적용
        if (allShelves[selectedShelfIndex].TryPlaceBox(x, z, size, out Vector3 pos))
        {
            currentBox.transform.position = pos;
            currentBox.transform.localScale = size;
        }
        else
        {
            currentBox.gameObject.SetActive(false); // 배치 실패
        }

        currentBoxIndex++;
        // 상자를 다 썼으면 추가 생성
        if (currentBoxIndex >= boxesToPlace.Count) GenerateBoxes();
    }

    private float GetUsage(GridManager shelf)
    {
        float usage = 0;
        foreach (float v in shelf.gridMap) usage += v;
        return usage;
    }
}