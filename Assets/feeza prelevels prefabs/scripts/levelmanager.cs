using UnityEngine;

public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance;

    public GameObject[] levels;      // Level prefabs: Level1, Level2, Level3
    public Transform player1;
    public Transform player2;
    public int startLevel = 0;

    public int Index { get; private set; }
    GameObject current;

    void Awake() { Instance = this; }
    public bool autoStart = false;   // test ke liye tick kar sakti ho
    void Start() { if (autoStart) Load(startLevel); }

    public void Load(int i)
    {
        if (levels == null || levels.Length == 0) return;
        Index = Mathf.Clamp(i, 0, levels.Length - 1);
        Time.timeScale = 1f;

        if (current != null) Destroy(current);
        current = Instantiate(levels[Index]);

        GravityObject.RoomGravityOn = true;

        Teleport(player1, current.transform.Find("Spawn1"));
        Teleport(player2, current.transform.Find("Spawn2"));
    }

    public void Restart() { Load(Index); }

    public void NextLevel()
    {
        if (Index + 1 < levels.Length) Load(Index + 1);
        else Load(0);   // aakhri level ke baad shuru se
    }

    public bool IsLastLevel { get { return Index >= levels.Length - 1; } }

    void Teleport(Transform p, Transform spawn)
    {
        if (p == null || spawn == null) return;
        var rb = p.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.position = spawn.position;
        }
        p.position = spawn.position;
    }
}