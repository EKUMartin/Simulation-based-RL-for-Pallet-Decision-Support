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
        List<Box> tempBoxes = new List<Box>();

        for (int i = 0; i < 8; i++)
        {
            GameObject newBoxObj = boxManager.GenerateBox(i);
            Box boxScript = newBoxObj.GetComponent<Box>();

            float volume = boxScript.size.x * boxScript.size.y * boxScript.size.z;

        // 기준값 0.15f
            if (volume > 0.15f) 
            {
            // 무겁고 큰 박스 -> 1층부 (Element 0 또는 1번 선반)
            // 선반1의 1층과 선반2의 1층 중 랜덤으로 골고루 들어감
                boxScript.targetShelfID = Random.Range(0, 2); 
            } 
            else 
            {
            // 가볍고 작은 박스 -> 2층부 (Element 2 또는 3번 선반)
            // 선반1의 2층과 선반2의 2층 중 랜덤으로 골고루 들어감
                boxScript.targetShelfID = Random.Range(2, 4); 
            }

            tempBoxes.Add(boxScript);
            allSpawnedBoxes.Add(newBoxObj);
        }
    
    // 정렬 유지 (큰 상자부터 처리하도록)
        tempBoxes.Sort((a, b) => (b.size.x * b.size.z).CompareTo(a.size.x * a.size.z));
        boxesToPlace.AddRange(tempBoxes);
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (currentBoxIndex >= boxesToPlace.Count) return;

    // 가장 비어있는 층이 아니라, 상자에 미리 정해진 층의 인덱스를 사용
        selectedShelfIndex = boxesToPlace[currentBoxIndex].targetShelfID;

    // 파이썬에게는 해당 층의 지도 데이터만 전송
        float[] gridData = allShelves[selectedShelfIndex].GetGridData();
        foreach (float data in gridData) sensor.AddObservation(data);

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

        //파이썬이 준 좌표를 그대로 적용
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

}