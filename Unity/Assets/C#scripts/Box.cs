using UnityEngine;

public class Box : MonoBehaviour
{
    [Header("박스 정보")]
    public Vector3 size;          // 박스의 xyz 크기
    public int targetShelfID;     // 0: shelf0, 1:shelf1 나중에 agent에서 해당하는 shelf에 넣었는지 확인할꺼

    //랜덤 크기와 타겟을 부여
    public void InitRandomBox()
    {
        //0.2~0.9m 사이의 랜덤한 크기 부여 왜냐면 층 간격이 1.1정두거든
        size = new Vector3(Random.Range(0.2f, 0.9f), Random.Range(0.2f, 0.9f), Random.Range(0.2f, 0.9f));
        targetShelfID = Random.Range(0, 2); //shelf 2개니까
        
        // 시각적으로도 크기를 맞춰줌
        transform.localScale = size;
    }
}