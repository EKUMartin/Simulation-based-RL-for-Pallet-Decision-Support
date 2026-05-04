using UnityEngine;

public class GridManager : MonoBehaviour
{
    [Header("그리드 설정")]
    public float cellSize = 0.1f;      //그리드 1칸의 크기 (기본 10cm인데 바꿔도됨, Unity inspector에서도 바꿀 수 있음)
    public LayerMask boxLayer;         //Inspector에서 Box 레이어를 꼭 선택해야됨 왜냐면 이후에 박스가 있는지 체크하는 레이저가 Box만을 인식하도록 하려고

    [HideInInspector] public int gridWidth;
    [HideInInspector] public int gridDepth;
    
    //파이썬으로 넘길 배열 (0.0: 박스없, 1.0: 박스있)
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

    //배열을 0으로 초기화
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
                // 각 칸의 중앙 X, Z 좌표
                float posX = minBounds.x + (x * cellSize) + (cellSize / 2f);
                float posZ = minBounds.z + (z * cellSize) + (cellSize / 2f);
                
                // 선반 위쪽(높이 y + 1m)에서 아래로 레이저를 쏴서 검사
                Vector3 rayStart = new Vector3(posX, shelfCollider.bounds.max.y + 1f, posZ);

                // 아래 방향(Vector3.down)으로 레이저를 쏴서 box에 닿는지 확인
                if (Physics.Raycast(rayStart, Vector3.down, 1.1f, boxLayer)) //1.1f는 층 사이 간격
                {
                    gridMap[x, z] = 1.0f; // 닿았으면 해당 칸은 채워진 것으로 인식
                }
            }
        }
    }

    //에이전트가 지정한 위치(startX, startZ)에 박스(boxSize)를 놓을 수 있는지 확인하고 점유 처리
    public bool TryPlaceBox(int startX, int startZ, Vector3 boxSize, out Vector3 placePosition)
    {
        placePosition = Vector3.zero;

        //박스의 실제 크기를 셀 개수로 변환 (예: 0.3m / 0.1m = 3칸)
        int boxCellsX = Mathf.RoundToInt(boxSize.x / cellSize);
        int boxCellsZ = Mathf.RoundToInt(boxSize.z / cellSize);

        //선반 밖으로 벗어나는지 검사
        if (startX < 0 || startZ < 0 || startX + boxCellsX > gridWidth || startZ + boxCellsZ > gridDepth)
        {
            return false; //배치 불가 (범위 초과)
        }

        // 다른 박스와 겹치는지(1.0f) 검사
        for (int x = startX; x < startX + boxCellsX; x++)
        {
            for (int z = startZ; z < startZ + boxCellsZ; z++)
            {
                if (gridMap[x, z] == 1.0f) return false; //배치 불가(이미 공간이 참)
            }
        }

        //검사를 통과했다면 해당 공간을 채움(1.0f)
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
        float worldY = shelfCollider.bounds.max.y + (boxSize.y / 2f); //높이(y)값 계산
        
        placePosition = new Vector3(worldX, worldY, worldZ); //최종좌표
        return true; // 배치 성공
    }
}