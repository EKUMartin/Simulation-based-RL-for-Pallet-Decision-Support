using UnityEngine;

public class Box : MonoBehaviour
{
    [Header("박스 정보")]
    public Vector3 size;          //RandomBox_generator가 계산해서 넣어줄 크기 (x,y,z)
    public float weight;          //RandomBox_generator가 부피에 비례해 계산해줄 무게
    public int targetShelfID;     //RandomBox_generator가 무게에 따라 정해줄 목표 선반 (0 또는 1)
}