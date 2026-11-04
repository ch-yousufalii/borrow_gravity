using UnityEngine;

namespace BorrowGravity
{
    /// <summary>
    /// The Warden, Level 7's boss. It hunts the players with ground strikes and can only be hurt by both powers at once:
    /// B freezes it, then A changes the arena's gravity while it is frozen. Three hits break it out of the arena, each one
    /// making the fight harder. It isn't finished: it chases the team along the escape route under the countdown, then
    /// makes a last stand on the ship deck, where two more hits destroy it and open the way to the ship.
    /// Damage survives a respawn, so a fall never undoes progress.
    /// </summary>
    [RequireComponent(typeof(Nullifiable))]
    public class WardenBoss : MonoBehaviour, IResettable
    {
        enum Phase { Waiting, Fighting, Stunned, Chasing, Dead }

        public int section;
        public GravityZone arenaZone;
        public GameObject model;
        [Tooltip("One light per hit point; each hit switches one off.")]
        public GameObject[] healthLights;
        public WardenStrike[] strikes;
        [Tooltip("Spinning beam switched on after the first hit.")]
        public GameObject sweepArm;
        [Tooltip("Floor tiles that start dropping after the second hit.")]
        public CrumblePlatform[] tiles;
        [Tooltip("Force field across the exit, removed when the Warden dies.")]
        public GameObject exitBarrier;
        public Vector3 arenaCenter;
        public float wakeDistance = 18f;
        public float hoverHeight = 5f;
        public float moveSpeed = 3.5f;
        public float stunSeconds = 3f;
        public float escapeSeconds = 35f;
        public float chaseSpeed = 5f;

        [Header("Last stand on the ship deck")]
        [Tooltip("Gravity zone over the ship deck. Empty = the Warden simply dies after the arena fight.")]
        public GravityZone lastStandZone;
        [Tooltip("Force field in front of the ship, removed when the Warden is destroyed.")]
        public GameObject lastStandBarrier;
        public Vector3 lastStandCenter;
        [Tooltip("The last stand starts when a player gets past this x.")]
        public float lastStandStartX = 90f;
        public int lastStandHits = 2;

        static readonly float[] StrikeInterval = { 2.6f, 2.1f, 1.6f };
        static readonly float[] Telegraph = { 1.2f, 1f, 0.85f };
        const float TileInterval = 3f;
        const float ChaseStrikeInterval = 2.2f;

        public int Section => section;
        public int Hits { get; private set; }
        public bool Defeated => phase == Phase.Dead;

        Phase phase = Phase.Waiting;
        Nullifiable shell;
        Rigidbody body;
        float nextStrike, nextTile, stunEnd, escapeLeft, deathFall = -1f;
        bool wasFrozen, countdownOn, lastStand;
        int stageHits;

        GravityZone CurrentZone => lastStand ? lastStandZone : arenaZone;
        int HitsNeeded => lastStand ? Mathf.Min(lastStandHits, healthLights.Length) : healthLights.Length;

        void Awake()
        {
            shell = GetComponent<Nullifiable>();
            body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            if (sweepArm != null) sweepArm.SetActive(false);
        }

        void OnEnable()
        {
            if (arenaZone != null) arenaZone.Changed += OnGravityChanged;
            if (lastStandZone != null) lastStandZone.Changed += OnGravityChanged;
        }

        void OnDisable()
        {
            if (arenaZone != null) arenaZone.Changed -= OnGravityChanged;
            if (lastStandZone != null) lastStandZone.Changed -= OnGravityChanged;
        }

        int Tier => Mathf.Clamp(Hits, 0, StrikeInterval.Length - 1);

        void Update()
        {
            var sm = StageManager.Instance;
            if (sm == null) return;
            if (sm.State == StageState.Complete && countdownOn)
            {
                countdownOn = false;
                GameUI.StopCountdown();
            }
            if (sm.State != StageState.Playing) return;

            switch (phase)
            {
                case Phase.Waiting:
                    foreach (var p in Players.All)
                    {
                        if (p == null || Vector3.Distance(p.transform.position, arenaCenter) > wakeDistance) continue;
                        phase = Phase.Fighting;
                        nextStrike = Time.time + 2f;
                        GameUI.ShowWarning(HudHazard.General, "THE WARDEN HAS WOKEN", 0f, 3f);
                        GameUI.ShowHint("B: freeze the Warden.  A: change gravity while it's frozen.", 8f);
                        Sfx.Play(SfxId.Boom, 0.9f);
                        CoopCamera.Shake(0.5f, 0.8f);
                        break;
                    }
                    break;

                case Phase.Fighting:
                    if (shell.IsActive)
                    {
                        if (!wasFrozen) GameUI.ShowMessage("It's frozen! A — change gravity now!", 3f);
                        wasFrozen = true;
                        break; // a frozen Warden doesn't attack
                    }
                    wasFrozen = false;
                    if (Time.time >= nextStrike) Attack();
                    if (!lastStand && Hits >= 2 && Time.time >= nextTile) DropTile();
                    break;

                case Phase.Stunned:
                    if (Time.time >= stunEnd)
                    {
                        phase = Phase.Fighting;
                        shell.enabled = true;
                        nextStrike = Time.time + 1f;
                        nextTile = Time.time + TileInterval;
                    }
                    break;

                case Phase.Chasing:
                    UpdateEscape(sm);
                    if (phase != Phase.Chasing) break;
                    if (Time.time >= nextStrike) { nextStrike = Time.time + ChaseStrikeInterval; StrikePlayers(Telegraph[1], false); }
                    foreach (var p in Players.All)
                        if (p != null && p.transform.position.x >= lastStandStartX) { BeginLastStand(); break; }
                    break;

                case Phase.Dead:
                    if (lastStandZone == null) UpdateEscape(sm);
                    break;
            }
        }

        void FixedUpdate()
        {
            if (deathFall >= 0f)
            {
                deathFall += Time.fixedDeltaTime;
                body.MovePosition(body.position + Vector3.down * (6f + deathFall * 14f) * Time.fixedDeltaTime);
                if (deathFall > 1.8f && model.activeSelf) model.SetActive(false);
                return;
            }
            if (phase == Phase.Dead || shell.IsActive) return;
            Vector3 target;
            if (phase == Phase.Stunned)
                target = new Vector3(body.position.x, arenaCenter.y + 2.2f, body.position.z);
            else
            {
                var p = Nearest();
                Vector3 goal = p != null && (phase == Phase.Fighting || phase == Phase.Chasing) ? p.transform.position : arenaCenter;
                float baseY = phase == Phase.Chasing && p != null ? p.transform.position.y - 1f : arenaCenter.y;
                target = new Vector3(goal.x, baseY + hoverHeight + Mathf.Sin(Time.time * 1.4f) * 0.4f, goal.z);
            }
            float speed = phase == Phase.Stunned ? 14f : phase == Phase.Chasing ? chaseSpeed : moveSpeed + stageHits + (lastStand ? 2f : 0f);
            if (lastStand && (target - body.position).sqrMagnitude > 144f) speed = 14f; // left behind on the route: catch up fast
            body.MovePosition(Vector3.MoveTowards(body.position, target, speed * Time.fixedDeltaTime));
        }

        PlayerController Nearest()
        {
            PlayerController best = null;
            float bestDist = float.MaxValue;
            foreach (var p in Players.All)
            {
                if (p == null) continue;
                float d = (p.transform.position - body.position).sqrMagnitude;
                if (d < bestDist) { bestDist = d; best = p; }
            }
            return best;
        }

        void Attack()
        {
            nextStrike = Time.time + StrikeInterval[Tier];
            StrikePlayers(Telegraph[Tier], Hits >= 1);
        }

        /// <summary>A strike under each player; once hurt, a second one where they're running to.</summary>
        void StrikePlayers(float telegraph, bool lead)
        {
            foreach (var p in Players.All)
            {
                if (p == null || (phase == Phase.Fighting && !lastStand && p.transform.position.x > arenaCenter.x + 21f)) continue;
                FireAt(p.transform.position, telegraph);
                if (lead && p.MoveDirection.sqrMagnitude > 0.01f)
                    FireAt(p.transform.position + p.MoveDirection * p.moveSpeed * telegraph, telegraph);
            }
        }

        void FireAt(Vector3 at, float telegraph)
        {
            // land on whatever is under that spot (nothing there = no strike)
            if (!Physics.Raycast(at + Vector3.up * 0.5f, Vector3.down, out var hit, 14f, ~0, QueryTriggerInteraction.Ignore)) return;
            foreach (var s in strikes)
            {
                if (s == null || s.Busy) continue;
                s.Fire(new Vector3(at.x, hit.point.y + 0.05f, at.z), telegraph);
                return;
            }
        }

        void DropTile()
        {
            nextTile = Time.time + TileInterval;
            if (tiles == null || tiles.Length == 0) return;
            var t = tiles[Random.Range(0, tiles.Length)];
            if (t != null && t.Current == CrumblePlatform.State.Solid) t.Trigger(1.4f);
        }

        void OnGravityChanged(GravityZone z)
        {
            if (phase != Phase.Fighting || !shell.IsActive || z != CurrentZone) return;
            Hits++;
            stageHits++;
            if (healthLights != null && stageHits <= healthLights.Length && healthLights[stageHits - 1] != null)
                healthLights[stageHits - 1].SetActive(false);
            shell.ForceRelease(false);
            shell.enabled = false; // can't be frozen again until it recovers
            foreach (var s in strikes) if (s != null) s.Clear();
            Sfx.Play(SfxId.Boom, 1f);
            CoopCamera.Shake(0.8f, 0.8f);
            if (healthLights != null && stageHits >= HitsNeeded)
            {
                if (!lastStand && lastStandZone != null) BeginChase();
                else Die();
                return;
            }
            phase = Phase.Stunned;
            stunEnd = Time.time + stunSeconds;
            if (lastStand) GameUI.ShowWarning(HudHazard.General, "ONE MORE! Finish it!", 0f, 3f);
            else if (Hits == 1)
            {
                if (sweepArm != null) sweepArm.SetActive(true);
                GameUI.ShowWarning(HudHazard.Electric, "HIT! Now jump its sweeping beam", 0f, 3f);
            }
            else GameUI.ShowWarning(HudHazard.BridgeCrack, "ONE MORE! The floor is failing", 0f, 3f);
        }

        /// <summary>Knocked out of the arena, but still flying: the exit opens and it hunts the team along the route.</summary>
        void BeginChase()
        {
            phase = Phase.Chasing;
            nextStrike = Time.time + 2.5f;
            if (sweepArm != null) sweepArm.SetActive(false);
            if (exitBarrier != null) exitBarrier.SetActive(false);
            if (arenaZone != null) arenaZone.SetMode(GravityMode.Down, false);
            GameUI.ShowWarning(HudHazard.General, "IT'S STILL COMING — RUN FOR THE SHIP!", 0f, 4f);
            Sfx.Play(SfxId.Riser, 1f);
            Sfx.SetLoop(LoopId.Alarm, true, 0.5f);
            escapeLeft = escapeSeconds;
            StartCountdown();
        }

        /// <summary>The Warden gets in front of the ship: same trick as before, two more hits, and no timer to rush it.</summary>
        void BeginLastStand()
        {
            lastStand = true;
            stageHits = 0;
            arenaCenter = lastStandCenter;
            for (int i = 0; i < healthLights.Length; i++)
                if (healthLights[i] != null) healthLights[i].SetActive(i < HitsNeeded);
            foreach (var s in strikes) if (s != null) s.Clear();
            countdownOn = false;
            GameUI.StopCountdown();
            Sfx.SetLoop(LoopId.Alarm, false);
            phase = Phase.Fighting;
            shell.enabled = true;
            wasFrozen = false;
            nextStrike = Time.time + 2.5f;
            GameUI.ShowWarning(HudHazard.General, "LAST STAND — TWO MORE HITS", 0f, 4f);
            GameUI.ShowHint("B: freeze the Warden.  A: change gravity while it's frozen.", 6f);
            Sfx.Play(SfxId.Boom, 0.9f);
            CoopCamera.Shake(0.6f, 0.8f);
        }

        void Die()
        {
            phase = Phase.Dead;
            deathFall = 0f;
            if (sweepArm != null) sweepArm.SetActive(false);
            if (exitBarrier != null) exitBarrier.SetActive(false);
            if (lastStandBarrier != null) lastStandBarrier.SetActive(false);
            if (arenaZone != null) arenaZone.SetMode(GravityMode.Down, false);
            if (lastStandZone != null)
            {
                lastStandZone.SetMode(GravityMode.Down, false);
                GameUI.ShowWarning(HudHazard.General, "THE WARDEN IS DESTROYED — GET TO THE SHIP!", 0f, 4f);
                Sfx.Play(SfxId.Riser, 1f);
                return;
            }
            GameUI.ShowWarning(HudHazard.General, "THE WARDEN IS DOWN — RUN FOR THE SHIP!", 0f, 4f);
            Sfx.Play(SfxId.Riser, 1f);
            Sfx.SetLoop(LoopId.Alarm, true, 0.5f);
            escapeLeft = escapeSeconds;
            StartCountdown();
        }

        void StartCountdown()
        {
            countdownOn = true;
            GameUI.StartCountdown("ESCAPE", HudHazard.General, () => Mathf.Max(0f, escapeLeft));
        }

        void UpdateEscape(StageManager sm)
        {
            // a checkpoint reset stops the HUD timer, so put it back on the next frame
            if (!countdownOn) StartCountdown();
            escapeLeft -= Time.deltaTime;
            if (escapeLeft > 0f) return;
            escapeLeft = escapeSeconds;
            GameUI.ShowMessage("Too slow! The core pulled you back", 2.5f);
            sm.ResetToCheckpoint();
            countdownOn = false;
        }

        public void ResetState()
        {
            foreach (var s in strikes) if (s != null) s.Clear();
            if (phase == Phase.Dead || phase == Phase.Chasing)
            {
                escapeLeft = escapeSeconds;
                countdownOn = false;
                return;
            }
            // damage is kept; the Warden just goes back to the middle of its arena and waits for the team
            shell.enabled = true;
            shell.ForceRelease(false);
            wasFrozen = false;
            phase = Phase.Waiting;
            body.position = arenaCenter + Vector3.up * hoverHeight;
            transform.position = body.position;
        }
    }
}
