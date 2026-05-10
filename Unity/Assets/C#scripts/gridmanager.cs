using UnityEngine;

public class GridManager : MonoBehaviour
{
    [Header("그리드 설정")]
    public float cellSize = 0.1f;      //그리드 1칸의 크기 (기본 10cm인데 바꿔도됨, Unity inspector에서도 바꿀 수 있음)
    public LayerMask boxLayer;         //Inspector에서 Box 레이어를 꼭 선택해야됨 왜냐면 이후에 박스가 있는지 체크하는 레이저가 Box만을 인식하도록 하려고

    [HideInInspector] public int gridWidth;
    [HideInInspector] public int gridDepth;
    
    //파이썬으로 넘길 배열 (0.0: 박스없, 1.0: 박스있, 2.0: 박스 생성할 때 grid 탐색하며 사용)
    public float[,] gridMap; 

    private BoxCollider shelfCollider;

    void Awake()
    {
        shelfCollider = GetComponent<BoxCollider>();
        if (shelfCollider == null)
        {
            Debug.LogError("이 객체에 BoxCollider가 없습니다!");
            return;
        }

        //선반 Collider 크기를 기반으로 배열 크기 계산
        Vector3 realSize = shelfCollider.bounds.size;
        gridWidth = Mathf.RoundToInt(realSize.x / cellSize);
        gridDepth = Mathf.RoundToInt(realSize.z / cellSize);

        gridMap = new float[gridWidth, gridDepth];
        Debug.Log($"선반 그리드 세팅 완료: {gridWidth}칸 x {gridDepth}칸");

        //시작할 때 놓여있는 박스들을 미리 스캔
        ScanInitialBoxes(); 
    }

    //배열 0으로 초기화
    public void ResetGrid()
    {
        for (int x = 0; x < gridWidth; x++)
        {
            for (int z = 0; z < gridDepth; z++) gridMap[x, z] = 0f;
        }
    }

    //이미 배치된 박스를 스캔하여 그리드 맵에 반영 (1.0으로)
    public void ScanInitialBoxes()
    {
        ResetGrid(); 
        Vector3 minBounds = shelfCollider.bounds.min;

        for (int x = 0; x < gridWidth; x++)
        {
            for (int z = 0; z < gridDepth; z++)
            {
                //각 칸의 중앙 X, Z 좌표
                float posX = minBounds.x + (x * cellSize) + (cellSize / 2f);
                float posZ = minBounds.z + (z * cellSize) + (cellSize / 2f);
                
                //선반 위쪽(y + 1m)에서 아래로 레이저를 쏴서 검사
                Vector3 rayStart = new Vector3(posX, shelfCollider.bounds.max.y + 1f, posZ);

                //아래 방향(Vector3.down)으로 레이저를 쏴서 box에 닿는지 확인
                if (Physics.Raycast(rayStart, Vector3.down, 1.1f, boxLayer)) //1.1f는 층 사이 간격
                {
                    gridMap[x, z] = 1.0f; // 닿았으면 해당 칸은 채워진 것으로 인식
                }
            }
        }
    }

    //에이전트가 지정한 위치(startX, startZ)에 박스를 놓을 수 있는지 확인하고 점유 처리
    public bool TryPlaceBox(int startX, int startZ, Vector3 boxSize, out Vector3 placePosition)
    {
        placePosition = Vector3.zero;

        //박스의 실제 크기를 셀 개수로 변환
        int boxCellsX = Mathf.RoundToInt(boxSize.x / cellSize);
        int boxCellsZ = Mathf.RoundToInt(boxSize.z / cellSize);

        //선반 밖으로 벗어나는지 검사
        if (startX < 0 || startZ < 0 || startX + boxCellsX > gridWidth || startZ + boxCellsZ > gridDepth)
        {
            return false; //배치 불가 (범위 초과)
        }

        //다른 박스와 겹치는지(1.0) 검사
        for (int x = startX; x < startX + boxCellsX; x++)
        {
            for (int z = startZ; z < startZ + boxCellsZ; z++)
            {
                if (gridMap[x, z] == 1.0f) return false; //배치 불가(이미 공간이 참)
            }
        }

        //검사를 통과했다면 해당 공간을 채움(1.0)
        for (int x = startX; x < startX + boxCellsX; x++)
        {
            for (int z = startZ; z < startZ + boxCellsZ; z++)
            {
                gridMap[x, z] = 1.0f;
            }
        }

        //grid에서 실제 월드 좌표 계산
        Vector3 minBounds = shelfCollider.bounds.min; //선반의 가장 왼쪽앞쪽아래쪽 끝점을 (0,0)으로
        float worldX = minBounds.x + (startX * cellSize) + (boxSize.x / 2f); //box 정중앙 좌표계산
        float worldZ = minBounds.z + (startZ * cellSize) + (boxSize.z / 2f);
        //높이는 선반 바닥(y) + 박스 높이의 절반
        float worldY = shelfCollider.bounds.max.y + (boxSize.y / 2f); //높이 y값 계산
        
        placePosition = new Vector3(worldX, worldY, worldZ); //최종좌표
        return true; //배치 성공
    }

    public bool TryGetAndReserveFittingBoxSize(out Vector3 boxSize, bool isLargeBox)
    {
        boxSize = Vector3.zero;
        int bestX = -1, bestZ = -1, bestW = 0, bestD = 0;
        int maxArea = 0;

        //그리드 탐색하고 가장 큰 빈 직사각형 찾기
        for (int startX = 0; startX < gridWidth; startX++)
        {
            for (int startZ = 0; startZ < gridDepth; startZ++)
            {
                if (gridMap[startX, startZ] != 0.0f) continue;
                int currentMaxW = 0;
                while (startX + currentMaxW < gridWidth && gridMap[startX + currentMaxW, startZ] == 0.0f)
                {
                    currentMaxW++;
                    if (currentMaxW >= 8) break;
                }
                for (int w = 1; w <= currentMaxW; w++)
                {
                    int currentMaxD = 0;
                    bool canExpand = true;
                    while (startZ + currentMaxD < gridDepth && canExpand)
                    {
                        for (int x = startX; x < startX + w; x++)
                        {
                            if (gridMap[x, startZ + currentMaxD] != 0.0f) { canExpand = false; break; }
                        }
                        if (canExpand) { currentMaxD++; if (currentMaxD >= 8) break; }
                    }
                    int area = w * currentMaxD;
                    if (area > maxArea && w >= 2 && currentMaxD >= 2)
                    {
                        maxArea = area; bestX = startX; bestZ = startZ; bestW = w; bestD = currentMaxD;
                    }
                }
            }
        }

        if (maxArea == 0) return false;

        //부피 조건(0.15 기준)과 y값 1m 이하 조건을 동시에 만족하는 x,z,y 찾기 : 층 결정 -> 층에 따른 부피 조건에 맞춰서 y값 정함요
        int finalW = 0, finalD = 0;
        float finalH = 0f;
        bool foundValidSize = false;

        for (int retry = 0; retry < 20; retry++)
        {
            finalW = Random.Range(2, bestW + 1);
            finalD = Random.Range(2, bestD + 1);
            float baseArea = (finalW * cellSize) * (finalD * cellSize); //바닥 면적

            if (isLargeBox) 
            {
                //대형 박스 부피 > 0.15
                float minRequiredHeight = 0.15f / baseArea; 
                
                //0.1 단위로 올림 ex) 0.23 -> 0.3
                int minHSteps = Mathf.CeilToInt(Mathf.Max(0.2f, minRequiredHeight) * 10f); 
                int maxHSteps = 10; // 1.0m = 10칸

                if (minHSteps <= maxHSteps) 
                {   finalH = Random.Range(minHSteps, maxHSteps + 1) * 0.1f;
                    foundValidSize = true; break;
                }
            }
            else 
            {
                //소형 박스 부피 <= 0.15
                float maxAllowedHeight = 0.149f / baseArea;
                
                int minHSteps = 2;
                //0.1 단위로 내림
                int maxHSteps = Mathf.FloorToInt(Mathf.Min(1.0f, maxAllowedHeight) * 10f); 

                if (minHSteps <= maxHSteps) 
                {
                    finalH = Random.Range(minHSteps, maxHSteps + 1) * 0.1f;
                    foundValidSize = true; break;
                }
            }
        }

        // 안전장치 있으면 좋대요: 20번 돌려도 못 찾으면 강제로 최대 면적 사용 후 높이 보정
        if (!foundValidSize)
        {
            finalW = bestW; finalD = bestD;
            float baseArea = (finalW * cellSize) * (finalD * cellSize);
            if (isLargeBox) 
            {
                float h = Mathf.Clamp(0.151f / baseArea, 0.2f, 1.0f);
                finalH = Mathf.Ceil(h * 10f) / 10f; // 0.1 단위 강제 올림
            }
            else 
            {
                float h = Mathf.Clamp(0.149f / baseArea, 0.2f, 1.0f);
                finalH = Mathf.Floor(h * 10f) / 10f;
            }
            finalH = Mathf.Clamp(finalH, 0.2f, 1.0f); // 최종적으로 0.2 ~ 1.0 사이 보장
        }

        int offsetX = Random.Range(0, bestW - finalW + 1);
        int offsetZ = Random.Range(0, bestD - finalD + 1);
        int finalX = bestX + offsetX;
        int finalZ = bestZ + offsetZ;

        for (int x = finalX; x < finalX + finalW; x++)
        {
            for (int z = finalZ; z < finalZ + finalD; z++) gridMap[x, z] = 2.0f; //잠시 탐색 위해
        }

        boxSize = new Vector3(finalW * cellSize, finalH, finalD * cellSize);
        return true;
    }

    public void ClearTemporaryReservations()
    {
        for (int x = 0; x < gridWidth; x++)
        {
            for (int z = 0; z < gridDepth; z++)
                if (gridMap[x, z] == 2.0f) gridMap[x, z] = 0.0f;
        }
    }

    public float[] GetGridData()
    {
        float[] gridtoarray = new float[gridWidth * gridDepth];
        int index = 0;
        for (int z = 0; z < gridDepth; z++)
            for (int x = 0; x < gridWidth; x++)
                gridtoarray[index++] = gridMap[x, z];
        return gridtoarray;
    }


    // 🌟 [추가된 함수] 현재 선반의 빈 공간을 분석하여 딱 들어맞는 상자 크기 반환
    public Vector3 GetFittingBoxSize()
    {
        System.Collections.Generic.List<Vector2Int> emptyCells = new System.Collections.Generic.List<Vector2Int>();
        
        // 1. 현재 0.0f(빈 공간)인 모든 셀 좌표 수집
        for (int x = 0; x < gridWidth; x++)
        {
            for (int z = 0; z < gridDepth; z++)
            {
                if (gridMap[x, z] == 0.0f)
                {
                    emptyCells.Add(new Vector2Int(x, z));
                }
            }
        }

        // 예외: 빈 공간이 아예 없으면 기본 최소 규격 반환
        if (emptyCells.Count == 0)
        {
            return new Vector3(0.2f, Random.Range(0.2f, 0.8f), 0.2f);
        }

        // 2. 빈 공간 중 랜덤으로 시작점 하나 선택
        Vector2Int startCell = emptyCells[Random.Range(0, emptyCells.Count)];
        int startX = startCell.x;
        int startZ = startCell.y;

        // 3. 가로(X) 방향으로 최대 몇 칸 연속 비어있는지 확인 (최대 8칸 = 0.8m 제한)
        int maxW = 0;
        for (int x = startX; x < gridWidth; x++)
        {
            if (gridMap[x, startZ] == 0.0f) maxW++;
            else break;
            
            if (maxW >= 8) break; 
        }
        int wCells = Random.Range(Mathf.Min(2, maxW), maxW + 1);

        // 4. 결정된 가로 폭(wCells)을 유지하면서 세로(Z) 방향으로 몇 칸 비어있는지 확인
        int maxD = 0;
        for (int z = startZ; z < gridDepth; z++)
        {
            bool rowClear = true;
            for (int x = startX; x < startX + wCells; x++)
            {
                if (gridMap[x, z] == 1.0f)
                {
                    rowClear = false;
                    break;
                }
            }
            if (rowClear) maxD++;
            else break;

            if (maxD >= 8) break;
        }
        int dCells = Random.Range(Mathf.Min(2, maxD), maxD + 1);

        // 5. 실제 미터(m) 단위 크기로 변환
        float sizeX = wCells * cellSize;
        float sizeZ = dCells * cellSize;
        float sizeY = Random.Range(0.2f, 0.8f); // 높이는 바닥 빈칸과 무관하므로 랜덤

        return new Vector3(sizeX, sizeY, sizeZ);
    }

}