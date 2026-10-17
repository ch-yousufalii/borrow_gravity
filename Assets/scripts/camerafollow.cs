using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform player1;
    public Transform player2;
    public float minDistance = 12f;
    public float maxDistance = 30f;
    public float zoomFactor = 0.8f;
    public float smooth = 5f;

    void LateUpdate()
    {
        if (!player1 || !player2) return;

        Vector3 mid = (player1.position + player2.position) / 2f;
        float dist = Vector3.Distance(player1.position, player2.position);
        float camDist = Mathf.Clamp(minDistance + dist * zoomFactor, minDistance, maxDistance);

        Vector3 target = new Vector3(mid.x, mid.y + 2f, -camDist);
        transform.position = Vector3.Lerp(transform.position, target, smooth * Time.deltaTime);
    }
}