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
    private int selectedShelfIndex = 0; //이번 턴에 파이썬에게 보여준 선반 번호

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

        //생성 박스 개수 랜덤이요~
        int targetBoxCount = Random.Range(1, 9);

        for (int i = 0; i < targetBoxCount; i++)
        {
            //박스 대형(>0.15) or 소형(<=0.15) 50% 확률로 결정 -> 이거에 따라서 나중에 박스 y값 정하는거임요
            bool isLargeBox = Random.value > 0.5f;

            //선반 그룹 선택
            //대형이면 0 또는 1번 선반(1층), 소형이면 2 또는 3번 선반(2층)
            int targetShelfID = isLargeBox ? Random.Range(0, 2) : Random.Range(2, 4);
            GridManager targetShelf = allShelves[targetShelfID];

            //대형/소형 조건과 y<=1m 조건 만족하도록 y 크기 역산 
            if (targetShelf.TryGetAndReserveFittingBoxSize(out Vector3 fittingSize, isLargeBox))
            {
                //크기 찾고 박스 생성
                GameObject newBoxObj = boxManager.GenerateBox(i, fittingSize, targetShelfID);
                //Box boxScript = newBoxObj.GetComponent<Box>();
                tempBoxes.Add(newBoxObj.GetComponent<Box>());
                //tempBoxes.Add(boxScript);
                allSpawnedBoxes.Add(newBoxObj);
            }
        }

        //박스 생성할 때 채운 데이터 (2.0) 지우기
        foreach (var shelf in allShelves)
        {
            shelf.ClearTemporaryReservations();
        }

        if (tempBoxes.Count == 0)
        {
            EndEpisode();
            return;
        }

    // 정렬 유지 (큰 상자부터 처리하도록)
        tempBoxes.Sort((a, b) => (b.size.x * b.size.z).CompareTo(a.size.x * a.size.z));
        boxesToPlace.AddRange(tempBoxes);
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (currentBoxIndex >= boxesToPlace.Count) return;

    //미리 정해진 층의 인덱스를 사용
        selectedShelfIndex = boxesToPlace[currentBoxIndex].targetShelfID;

    // 파이썬에게는 해당 층의 지도 데이터만 전송
        sensor.AddObservation(allShelves[selectedShelfIndex].GetGridData());
        //float[] gridData = allShelves[selectedShelfIndex].GetGridData();
        //foreach (float data in gridData) sensor.AddObservation(data);

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