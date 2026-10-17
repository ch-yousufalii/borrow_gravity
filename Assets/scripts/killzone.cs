using UnityEngine;

public class KillZone : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<PlayerController>() != null
            && GameManager.Instance != null)
        {
            GameManager.Instance.GameOver();
        }
    }
}