using UnityEngine;

public class WorkerMovement : MonoBehaviour
{
    // 인스펙터 창에서 이동 속도를 조절할 수 있는 변수
    public float speed = 5.0f;

    void Update()
    {
        // 1. 키보드 입력 받기 (W/S/상/하, A/D/좌/우)
        float moveX = Input.GetAxis("Horizontal"); 
        float moveZ = Input.GetAxis("Vertical");

        // 2. 이동 방향 벡터 만들기 (Y축은 점프이므로 0으로 둡니다)
        Vector3 movement = new Vector3(moveX, 0, moveZ);

        // 3. 실제 오브젝트 이동시키기
        // Time.deltaTime을 곱해줘야 컴퓨터 성능에 상관없이 일정한 속도로 부드럽게 이동합니다.
        transform.Translate(movement * speed * Time.deltaTime, Space.World);

        // 4. (보너스) 이동하는 방향으로 캐릭터가 고개를 돌리게 만들기
        if (movement != Vector3.zero)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(movement), 0.15f);
        }
    }
}
