using UnityEngine;
using static GameConstants;

public class AggroTrigger : MonoBehaviour
{
    void Awake() => GetComponent<SphereCollider>().radius = AggroRange;

    void OnTriggerEnter(Collider other) { if (other.CompareTag(TagEnemy)) other.GetComponent<Enemy>().HasTarget(true); }
}