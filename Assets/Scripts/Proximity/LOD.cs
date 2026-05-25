using UnityEngine;
using static GameConstants;

public class LOD : MonoBehaviour
{
    void Awake() => GetComponent<SphereCollider>().radius = LODDetectionRadius;

    void OnTriggerEnter(Collider other) { if (other.CompareTag(TagCoin) || other.CompareTag(TagEnemy)) other.GetComponent<OptimizableObject>().Toggle(true); }

    void OnTriggerExit(Collider other) { if (other.CompareTag(TagCoin) || other.CompareTag(TagEnemy)) other.GetComponent<OptimizableObject>().Toggle(false); }
}