using UnityEngine;

public class CrushCheck : MonoBehaviour
{
    public float checkDistance = 1.05f;
    public float crushTime = 0.15f;
    float timer;

    void Update()
    {
        bool up = Probe(Vector3.up, out bool upMoving);
        bool down = Probe(Vector3.down, out bool downMoving);

        if (up && down && (upMoving || downMoving))
        {
            timer += Time.deltaTime;
            if (timer >= crushTime && GameManager.Instance != null)
                GameManager.Instance.GameOver();
        }
        else
        {
            timer = 0f;
        }
    }

    bool Probe(Vector3 dir, out bool isMovingObject)
    {
        isMovingObject = false;
        if (Physics.Raycast(transform.position, dir, out RaycastHit hit,
                            checkDistance, ~0, QueryTriggerInteraction.Ignore))
        {
            // Doosra player upar/neeche ho to kuchalna nahi
            if (hit.collider.GetComponentInParent<PlayerController>() != null)
                return false;

            isMovingObject = hit.collider.GetComponentInParent<GravityObject>() != null;
            return true;
        }
        return false;
    }
}