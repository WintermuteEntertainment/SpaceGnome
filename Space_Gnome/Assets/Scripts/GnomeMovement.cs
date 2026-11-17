using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using DG.Tweening;
[System.Serializable]


public class GnomeMovement : MonoBehaviour
{
    [Header("Animation Variables")]

    public Animator player1Animator;
    [SerializeField] Transform modelTransform;
    [SerializeField] bool isStanding;

    public bool isFallingIdle;
    public bool isIdle;
    public bool isWalking;
    public bool isSprinting;
    public bool isDancing;
    public bool isJumping;

    //Dancing anim variables:
    public bool isDanceReady;
    public bool isDanceReady2;   // second Ready pose (Y + B held), 2026

    public bool isBreakDance1990;
    public bool isHeadspin;
    public bool isUprock;
    public bool isRockDancing;

    public bool isFlair;
    public bool isHipHopDancing;
    public bool isSillyDancing;
    public bool isBreakDanceEnding1;

    private bool hasTransitionedToDanceReady = false;

    //Fall Damage Booleans

    public bool startedStanding;
    public bool startedFalling;

    [Header("Input")]
    //INPUT:
    public SGInput player1Controls;

    [Header("Basic Variables")]
    //BASIC VARIABLES:

    [SerializeField] GameObject player1GO;
    public Rigidbody playerRB;

    public float defaultGravity = -9.81f;
    public float gravity = -9.81f;

    [Header("Movement Variables")]

    //MOVEMENT VARIABLES:

    [SerializeField] BoxCollider floorCollider;
    public FloorCollider m_floorCollider;

    public Vector3 move;
    public Vector3 look;

    public float playerSpeed;
    [SerializeField] int defaultPlayerSpeed;
    [SerializeField] int runSpeed;

    [Header("Jumping Variables")]

    public float jumpSpeed;
    public float fallSpeed;
    public Vector3 jumpHeight;
    [SerializeField] int boostJumpCost;   // no longer used (2026 rework); boost now drains per second while lifting
    [SerializeField] int boostDanceBonus;

    [Header("Jump & Boost (2026 rework)")]
    [Tooltip("Upward speed of the free jump from a platform (units/s). Always available, costs no boost.")]
    [SerializeField] float baseJumpSpeed = 12f;
    [Tooltip("How fast upward speed bleeds off after a jump or a released boost (units/s per second).")]
    [SerializeField] float jumpGravity = 30f;
    [Tooltip("Rise speed while Jump is held and there is boost (units/s).")]
    [SerializeField] float boostLiftSpeed = 10f;
    [Tooltip("Boost used per second of lift. The HUD's starting boost is 10000.")]
    [SerializeField] float boostDrainPerSecond = 2500f;
    [Tooltip("Allow holding Jump to lift while falling, not only after a jump.")]
    [SerializeField] bool boostWhileFalling = true;
    float verticalVelocity;

    [Header("Landing (2026 rework)")]
    [Tooltip("Gap kept between the gnome and the platform he stands on (units).")]
    [SerializeField] float groundSkin = 0.05f;
    [Tooltip("Tag of the things he can land on.")]
    [SerializeField] string floorTag = "Floor";
    [Tooltip("Seconds the ground must be missing before he counts as having left it (coyote time).")]
    [SerializeField] float groundLossGraceSeconds = 0.2f;
    float groundMissingFor;
    CapsuleCollider bodyCollider;
    FallDamage fallDamageTracker;

    [Header("Turning (2026 rework)")]
    [Tooltip("Maximum turn rate from the right stick (degrees/s).")]
    [SerializeField] float turnSpeedDegrees = 240f;
    [Tooltip("Right-stick deflection ignored before turning starts (0-1).")]
    [SerializeField] float lookDeadZone = 0.25f;

    //Combos
    public int comboCount;

    [Header("Cameras")]

    //CAMERA:
    public Cinemachine.CinemachineVirtualCamera vCam1;
    public Cinemachine.CinemachineVirtualCamera vCam2;

    public Cinemachine.CinemachineBrain cineBrain;

    //AUDIO

    private bool landingSoundPlayed;

    //START SINGLETON:

    public static GnomeMovement instance;
    public static GnomeMovement Instance
    {
        get
        {
            if (instance == null)
            {
                instance = GameObject.FindObjectOfType<GnomeMovement>();
                if (instance == null)
                {
                    GameObject singleton = new GameObject();
                    singleton.AddComponent<GnomeMovement>();
                    singleton.name = "(Singleton) GnomeMovement";
                }
            }
            return instance;
        }
    }

    //END SINGLETON +"instance=this;" in Awake();

    private void Awake()
    {
        instance = this;

        bodyCollider = GetComponent<CapsuleCollider>();
        fallDamageTracker = GetComponent<FallDamage>();

        player1Controls = new SGInput();

        gravity = defaultGravity;

        player1Controls.Player.Move.performed += ctx => move = ctx.ReadValue<Vector2>();
        player1Controls.Player.Move.canceled += ctx => move = Vector3.zero; // stick released
        player1Controls.Player.Look.performed += ctx => look = ctx.ReadValue<Vector2>();
        player1Controls.Player.Look.canceled += ctx => look = Vector3.zero;
        player1Controls.Player.Jump.performed += ctx => Jump();
        player1Controls.Player.Run.performed += ctx => Run();
        player1Controls.Player.Run.canceled += ctx => CancelRun();
        player1Controls.Player.Dance.performed += ctx => Dance();

        //DANCE CONTROLS:

        player1Controls.Player.DanceReady.performed += ctx => DanceReady();
        player1Controls.Player.BreakDance1990.performed += ctx => BreakDance1990();
        player1Controls.Player.Headspin.performed += ctx => Headspin();
        player1Controls.Player.Uprock.performed += ctx => Uprock();
        player1Controls.Player.RockDancing.performed += ctx => RockDancing();

        player1Controls.Player.Flair.performed += ctx => Flair();
        player1Controls.Player.HipHopDancing.performed += ctx => HipHopDancing();
        player1Controls.Player.SillyDancing.performed += ctx => SillyDancing();
        player1Controls.Player.BreakDanceEnding1.performed += ctx => BreakDanceEnding1();

        isFallingIdle = true;
    }


    void HandleMovement() //Move player transform based on Move Vector.
    {
        if (isWalking || isJumping || isSprinting || isFallingIdle)
        {
            Vector3 m = playerSpeed * Time.deltaTime * new Vector3(move.x, move.z, move.y).normalized;
            transform.Translate(m, Space.Self);
        }
    }
    // Dance tiers (2026, Tim's original design):
    //   0 = not dancing (Y / North not held, or not standing)
    //   1 = Y held: first Ready pose; the four basic moves (held LT / RT / RB / LB) are allowed
    //   2 = Y + B (East) held: the advanced moves (tap LB / LT / RB, RT) are allowed
    // Releasing Y ends the dance; releasing B ends the advanced move. Keyboard: Ctrl = Y, Shift = B.
    public int DanceTier
    {
        get
        {
            if (m_floorCollider == null || !m_floorCollider.isStanding) return 0;
            if (!player1Controls.Player.DanceReady.IsPressed()) return 0;
            return player1Controls.Player.Run.IsPressed() ? 2 : 1;
        }
    }

    // Boost and one combo step (max 5) for a basic dance move that actually started.
    void RewardDanceMove(bool started, string moveName)
    {
        if (!started) return;
        Toolbox.instance.m_playerManager.boost += boostDanceBonus;
        comboCount = Mathf.Min(comboCount + 1, 5);
        Debug.Log(moveName + " triggered. Combo count is " + comboCount + ".");
    }

    void HandleDanceTiers()
    {
        int tier = DanceTier;
        bool basicActive = isBreakDance1990 || isHeadspin || isUprock || isRockDancing;
        bool advancedActive = isFlair || isHipHopDancing || isSillyDancing || isBreakDanceEnding1;

        if (tier < 2 && advancedActive)
        {
            StopFlair(); StopHipHopDancing(); StopSillyDancing(); StopBreakDanceEnding1();
            advancedActive = false;
        }
        if (tier < 2 && isDanceReady2) StopDanceReady2();
        if (tier < 1 && (basicActive || isDanceReady))
        {
            StopBreakDance1990(); StopHeadspin(); StopUprock(); StopRockDancing(); StopDanceReady();
            basicActive = false;
        }

        // Ready poses while no move is playing: Y held = Ready (Breakdance Ready),
        // Y + B held = Ready 2 (Breakdance Footwork To Idle).
        if (!basicActive && !advancedActive)
        {
            if (tier == 1 && !isDanceReady)
            {
                player1Animator.SetBool("isDanceReady", true);
                isDanceReady = true;
            }
            else if (tier == 2 && !isDanceReady2)
            {
                StopDanceReady();
                player1Animator.SetBool("isDanceReady2", true);
                isDanceReady2 = true;
            }
        }
    }

    // Standing still (idle or dancing): the right stick orbits the camera (CameraOrbit) instead of
    // turning the gnome.
    public bool InOrbitMode =>
        m_floorCollider != null && m_floorCollider.isStanding && !isWalking && !isSprinting && !isJumping;

    CameraOrbit cameraOrbit;
    bool wasInOrbitMode;

    // When he starts moving after the camera was orbited, face the camera's direction first, so
    // stick-forward walks away from the camera instead of toward it.
    void AlignToCameraOnLeavingOrbit()
    {
        bool orbit = InOrbitMode;
        if (wasInOrbitMode && !orbit && (isWalking || isSprinting))
        {
            if (cameraOrbit == null) cameraOrbit = FindObjectOfType<CameraOrbit>();
            if (cameraOrbit != null) transform.rotation = Quaternion.Euler(0f, cameraOrbit.Yaw, 0f);
        }
        wasInOrbitMode = orbit;
    }

    void HandleRotation() //Turn the player toward the right-stick direction.
    {
        if (InOrbitMode) return; // the right stick orbits the camera instead
        // 2023 version snapped instantly with transform.LookAt on any stick deflection, which made
        // the right stick far too sensitive. Now: dead zone, then turn at a capped rate.
        Vector2 stick = new Vector2(look.x, look.y);
        if (stick.magnitude < lookDeadZone) return;

        Quaternion target = Quaternion.LookRotation(new Vector3(stick.x, 0f, stick.y), Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeedDegrees * Time.deltaTime);
    }

    // ------------------------------------------------------------------ landing (2026 rework)
    // The gnome is moved by transform.Translate, which physics does not sweep, so falling into a
    // platform used to overlap it until the separate FloorCollider box reported a contact, and the
    // physics push-out sometimes went the wrong way (falling through, with fall damage on the way
    // past). Now the gnome casts his own capsule along each step of the fall and stops on top of
    // anything tagged Floor, and "standing" comes from that same cast.

    // Nearest Floor-tagged surface below within `distance`, using the gnome's capsule shape.
    bool CastDown(float distance, out RaycastHit ground)
    {
        ground = default;
        Vector3 scale = transform.lossyScale;
        Vector3 center, top, bottom;
        float radius;
        if (bodyCollider != null)
        {
            center = transform.TransformPoint(bodyCollider.center);
            radius = bodyCollider.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z)) * 0.95f;
            float half = Mathf.Max(bodyCollider.height * Mathf.Abs(scale.y) * 0.5f - radius, 0f);
            top = center + transform.up * half;
            bottom = center - transform.up * half;
        }
        else
        {
            center = transform.position + Vector3.up;
            radius = 0.4f;
            top = bottom = center;
        }

        bool found = false;
        float best = float.MaxValue;
        foreach (var h in Physics.CapsuleCastAll(top, bottom, radius, Vector3.down, distance, ~0, QueryTriggerInteraction.Ignore))
        {
            if (!h.collider.CompareTag(floorTag) || h.distance >= best) continue;
            best = h.distance;
            ground = h;
            found = true;
        }
        return found;
    }

    // The surface he is standing on (null in the air). Platforms uses it so cleanup never
    // removes the platform under him.
    public Collider CurrentGround { get; private set; }

    void Land()
    {
        if (m_floorCollider.isStanding) return;
        m_floorCollider.isStanding = true;
        verticalVelocity = 0f;
        ResetStates();

        // GDD 3.2.2: landing on a platform interrupts any combo in progress. Combos are built
        // by dancing on a platform and cashed in on coins collected before the next landing.
        Debug.Log("Landed on '" + (CurrentGround != null ? CurrentGround.name : "?") + "' at y " + transform.position.y.ToString("0.00") +
                  ", t " + Time.time.ToString("0.00") + (comboCount > 0 ? ". Combo x" + comboCount + " interrupted." : "."));
        comboCount = 0;
        if (Toolbox.instance.m_coins != null) Toolbox.instance.m_coins.ResetCombo();
        if (fallDamageTracker != null) fallDamageTracker.OnLanded(transform.position.y);
    }

    void LeaveGround(bool jumped)
    {
        if (!m_floorCollider.isStanding) return;
        Debug.Log("Left ground: " + (jumped ? "jumped" : "no ground under him (walked off, or ground lost)") +
                  " from '" + (CurrentGround != null ? CurrentGround.name : "?") + "' at y " + transform.position.y.ToString("0.00") +
                  ", t " + Time.time.ToString("0.00"));
        m_floorCollider.isStanding = false;
        CurrentGround = null;
        if (fallDamageTracker != null) fallDamageTracker.OnLeftGround(transform.position.y);
        if (!jumped) FallingIdle(); // walked off the edge or the platform went away
    }

    void HandleGround()
    {
        if (verticalVelocity > 0f) return; // rising: not on the ground
        bool groundBelow = CastDown(groundSkin + 0.15f, out RaycastHit hit);
        if (groundBelow) CurrentGround = hit.collider;

        // Coyote time: only count as having left the ground after it has been missing for
        // groundLossGraceSeconds. A one-frame blip (an asteroid bump, a physics nudge) used to make
        // him "leave" and "land" again while standing still, and the landing reset the combo.
        if (m_floorCollider.isStanding && !groundBelow)
        {
            groundMissingFor += Time.deltaTime;
            if (groundMissingFor >= groundLossGraceSeconds) LeaveGround(false);
        }
        else
        {
            groundMissingFor = 0f;
            if (!m_floorCollider.isStanding && groundBelow) Land();
        }
    }

    // Fall one frame, stopping on top of the first platform in the way.
    void FallStep()
    {
        float step = fallSpeed * gravity * Time.deltaTime; // both negative in the prefab, so positive
        if (step <= 0f) return;
        if (CastDown(step + groundSkin, out RaycastHit ground))
        {
            transform.Translate(Vector3.down * Mathf.Max(0f, ground.distance - groundSkin), Space.World);
            CurrentGround = ground.collider;
            Land();
        }
        else
        {
            transform.Translate(Vector3.down * step, Space.World);
        }
    }

    // Free jump from a platform, then hold Jump to lift on boost until it runs out.
    void HandleJumpAndBoost()
    {
        var pm = Toolbox.instance.m_playerManager;
        bool held = player1Controls.Player.Jump.IsPressed();
        bool airborne = !m_floorCollider.isStanding;

        if (held && airborne && pm.boost > 0f && (isJumping || (boostWhileFalling && isFallingIdle)))
        {
            if (verticalVelocity < boostLiftSpeed) verticalVelocity = boostLiftSpeed;
            pm.boost = Mathf.Max(0f, pm.boost - boostDrainPerSecond * Time.deltaTime);
        }

        if (verticalVelocity > 0f)
        {
            transform.Translate(verticalVelocity * Time.deltaTime * Vector3.up, Space.World);
            verticalVelocity -= jumpGravity * Time.deltaTime;
            if (verticalVelocity <= 0f)
            {
                verticalVelocity = 0f;
                if (isJumping && !m_floorCollider.isStanding) FallingIdle(); // top of the jump: start falling again
            }
        }
    }
    public void ResetStates()  //Used to turn off all animations.
    {
        StopIdle();
        StopWalk();
        StopFallingIdle();
        StopJumping();
        CancelRun();
        StopDancing();

        //DANCING

        StopBreakDance1990();
        StopHeadspin();
        StopUprock();
        StopRockDancing();
        StopDanceReady();
        StopDanceReady2();

        StopFlair();
        StopHipHopDancing();
        StopSillyDancing();
        StopBreakDanceEnding1();

        //player1Animator.ResetTrigger("Flair");
        //player1Animator.ResetTrigger("HipHopDancing");
        //player1Animator.ResetTrigger("SillyDancing");
        //player1Animator.ResetTrigger("BreakDanceEnding1");

        hasTransitionedToDanceReady= false;
    }

    // ANIMATION STATES:

    void DanceReady()
    {
        ResetStates();
        if (!hasTransitionedToDanceReady) { hasTransitionedToDanceReady= true; }
       // player1Animator.SetBool("isDanceReady", true); isDanceReady = true;
        Debug.Log("DanceReady() called.");

       
        
            if (player1Controls.Player.BreakDance1990.triggered)
            {
                
                if (player1Controls.Player.Flair.triggered) { Flair(); }
                else if (player1Controls.Player.HipHopDancing.triggered) { HipHopDancing(); }
                else if (player1Controls.Player.SillyDancing.triggered) { SillyDancing(); }
                else if (player1Controls.Player.BreakDanceEnding1.triggered) { BreakDanceEnding1(); }
                else { BreakDance1990(); }
        }
            else if (player1Controls.Player.Headspin.triggered)
            {
                
                if (player1Controls.Player.Flair.triggered) { Flair(); }
                else if (player1Controls.Player.HipHopDancing.triggered) { HipHopDancing(); }
                else if (player1Controls.Player.SillyDancing.triggered) { SillyDancing(); }
                else if (player1Controls.Player.BreakDanceEnding1.triggered) { BreakDanceEnding1(); }
                else { Headspin(); }
        }
            else if (player1Controls.Player.Uprock.triggered)
            {
                
                if (player1Controls.Player.Flair.triggered) { Flair(); }
                else if (player1Controls.Player.HipHopDancing.triggered) { HipHopDancing(); }
                else if (player1Controls.Player.SillyDancing.triggered) { SillyDancing(); }
                else if (player1Controls.Player.BreakDanceEnding1.triggered) { BreakDanceEnding1(); }
                else { Uprock(); }
        }
            else if (player1Controls.Player.RockDancing.triggered)
            {
                
                if (player1Controls.Player.Flair.triggered) { Flair(); }
                else if (player1Controls.Player.HipHopDancing.triggered) { HipHopDancing(); }
                else if (player1Controls.Player.SillyDancing.triggered) { SillyDancing(); }
                else if (player1Controls.Player.BreakDanceEnding1.triggered) { BreakDanceEnding1(); }
                else { RockDancing(); }
        }
            else if (player1Controls.Player.Flair.triggered) { Flair(); }
            else if (player1Controls.Player.HipHopDancing.triggered) { HipHopDancing(); }
            else if (player1Controls.Player.SillyDancing.triggered) { SillyDancing(); }
            else if (player1Controls.Player.BreakDanceEnding1.triggered) { BreakDanceEnding1(); }

            else if (isBreakDance1990) { BreakDance1990(); }
            else if (isHeadspin) { Headspin(); }
            else if (isUprock) { Uprock(); }
            else if (isRockDancing) { RockDancing(); }

            else if (isFlair) { Flair(); }
            else if (isHipHopDancing) { HipHopDancing(); }
            else if (isSillyDancing) { SillyDancing(); }
            else if (isBreakDanceEnding1) { BreakDanceEnding1(); }

            else if (isDancing) { Dance(); }
            else if (!isDanceReady) { player1Animator.SetBool("isDanceReady", true); isDanceReady = true; Debug.Log("Started DanceReady."); }
        
        else { FallingIdle(); }

    }
    void StopDanceReady()
    {
        player1Animator.SetBool("isDanceReady", false);
        isDanceReady = false;
    }
    void StopDanceReady2()
    {
        player1Animator.SetBool("isDanceReady2", false);
        isDanceReady2 = false;
    }
    void BreakDance1990()
    {
        if (DanceTier < 1) return;   // basic move: needs Y (North) held
        ResetStates();

        player1Animator.SetBool("BreakDance1990", true);
        isBreakDance1990 = true;

    }
    void StopBreakDance1990()
    {
        player1Animator.SetBool("BreakDance1990", false);
        isBreakDance1990 = false;
    }
    void StopHeadspin()
    {
        player1Animator.SetBool("Headspin", false);
        isHeadspin = false;
    }
    void StopUprock()
    {
        player1Animator.SetBool("Uprock", false);
        isUprock = false;
    }
    void StopRockDancing()
    {
        player1Animator.SetBool("RockDancing", false);
        isRockDancing = false;
    }
    void Headspin()
    {
        if (DanceTier < 1) return;   // basic move: needs Y (North) held
        ResetStates();

        player1Animator.SetBool("Headspin", true);
        isHeadspin = true;

    }
    void Uprock()
    {
        if (DanceTier < 1) return;   // basic move: needs Y (North) held
        ResetStates();

        player1Animator.SetBool("Uprock", true);
        isUprock = true;

    }
    void RockDancing()
    {
        if (DanceTier < 1) return;   // basic move: needs Y (North) held
        ResetStates();
        player1Animator.SetBool("RockDancing", true);
        isRockDancing = true;
    }

    void Flair()
    {
        if (DanceTier < 2) return;   // advanced move: needs Y (North) + B (East) held
        ResetStates();

        player1Animator.SetBool("isFlair", true);
        isFlair = true;
        Debug.Log("Flair triggered.");
    }
    void HipHopDancing()
    {
        if (DanceTier < 2) return;   // advanced move: needs Y (North) + B (East) held
        ResetStates();

        player1Animator.SetBool("isHipHopDancing", true);
        isHipHopDancing = true;
        Debug.Log("HipHopDancing triggered.");
    }
    void SillyDancing()
    {
        if (DanceTier < 2) return;   // advanced move: needs Y (North) + B (East) held
        ResetStates();

        player1Animator.SetBool("isSillyDancing", true);
        isSillyDancing = true;
        Debug.Log("SillyDancing triggered.");
    }
    void BreakDanceEnding1()
    {
        if (DanceTier < 2) return;   // advanced move: needs Y (North) + B (East) held
        ResetStates();

        player1Animator.SetBool("isBreakDanceEnding1", true);
        isBreakDanceEnding1 = true;
        Debug.Log("BreakDanceEnding1 triggered.");
    }

    void StopFlair()
    {
        player1Animator.SetBool("isFlair", false);
        isFlair = false;
    }
    void StopHipHopDancing()
    {
        player1Animator.SetBool("isHipHopDancing", false);
        isHipHopDancing = false;
    }
    void StopSillyDancing()
    {
        player1Animator.SetBool("isSillyDancing", false);
        isSillyDancing = false;
    }
    void StopBreakDanceEnding1()
    {

        player1Animator.SetBool("isBreakDanceEnding1", false);
        isBreakDanceEnding1 = false;
    }
    public IEnumerator WaitOneSecond(float timeToWait)
    {
        // timeToWait = 1;
        yield return new WaitForSeconds(timeToWait);
    }
    public void Dance()
    {
        if (!isFallingIdle)
        {
            player1Animator.SetBool("isDancing", true);
            isDancing = true;
            Toolbox.instance.m_playerManager.boost += boostDanceBonus;
        }
        StartCoroutine(WaitOneSecond(1));

        if (player1Controls.Player.DanceReady.triggered) { DanceReady(); }

    }
    void FixedUpdate()
    {

        // SET VELOCITY TO 0 IF NO PLAYER INPUT

        if (!player1Controls.Player.Move.inProgress && !player1Controls.Player.Jump.inProgress && !player1Controls.Player.FirePlatform.inProgress
             && !player1Controls.Player.Run.inProgress && !player1Controls.Player.Dance.inProgress && !player1Controls.Player.DanceReady.inProgress
             && !player1Controls.Player.BreakDance1990.inProgress && !player1Controls.Player.Headspin.inProgress && !player1Controls.Player.Uprock.inProgress
             && !player1Controls.Player.RockDancing.inProgress && !player1Controls.Player.Flair.inProgress && !player1Controls.Player.HipHopDancing.inProgress
             && !player1Controls.Player.SillyDancing.inProgress && !player1Controls.Player.BreakDanceEnding1.inProgress) { playerRB.useGravity = false; }

        // 2026: the gnome is moved by code, never by physics, so clear any velocity physics gave
        // him (landing contacts, asteroid bumps) every step. The 2023 line playerRB.velocity.Set(0,0,0)
        // only changed a copy, so pushes accumulated and he kept sliding after landing.
        if (playerRB != null)
        {
            playerRB.velocity = Vector3.zero;
            playerRB.angularVelocity = Vector3.zero;
        }

    }
    void StopJumping()
    {
        player1Animator.SetBool("isJumping", false);
        isJumping = false;
    }
    //UPDATE
    void Update()
    {

        AlignToCameraOnLeavingOrbit();
        HandleMovement();
        HandleRotation();
        HandleJumpAndBoost();
        HandleGround();
        HandleDanceTiers();
        //Move transform down when in falling state (not while a jump or boost is carrying him up).

        if (isFallingIdle && verticalVelocity <= 0f && !m_floorCollider.isStanding)
        {
            FallStep();
        }

        else if (isWalking) { Walk(); }
        else if (isSprinting) { Run(); }
        //else if (isJumping) { Jump(); }
       else if (isStanding) { ResetStates(); }

        //DANCE MOVE PREREQUISITE STATES
        else if (player1Controls.Player.Dance.triggered) { Dance(); }
        else if (player1Controls.Player.DanceReady.triggered) { DanceReady(); }
        else if (player1Controls.Player.BreakDance1990.triggered)
        {

            if (player1Controls.Player.Flair.triggered) { ResetStates(); Flair(); }
            else if (player1Controls.Player.HipHopDancing.triggered) { ResetStates(); HipHopDancing(); }
            else if (player1Controls.Player.SillyDancing.triggered) { ResetStates(); SillyDancing(); }
            else if (player1Controls.Player.BreakDanceEnding1.triggered) { ResetStates(); BreakDanceEnding1(); }
            else { BreakDance1990(); }
        }
        else if (player1Controls.Player.Headspin.triggered)
        {

            if (player1Controls.Player.Flair.triggered) { ResetStates(); Flair(); }
            else if (player1Controls.Player.HipHopDancing.triggered) { ResetStates(); HipHopDancing(); }
            else if (player1Controls.Player.SillyDancing.triggered) { ResetStates(); SillyDancing(); }
            else if (player1Controls.Player.BreakDanceEnding1.triggered) { ResetStates(); BreakDanceEnding1(); }
            else { Headspin(); }

        }
        else if (player1Controls.Player.Uprock.triggered)
        {

            if (player1Controls.Player.Flair.triggered) { ResetStates(); Flair(); }
            else if (player1Controls.Player.HipHopDancing.triggered) { ResetStates(); HipHopDancing(); }
            else if (player1Controls.Player.SillyDancing.triggered) { ResetStates(); SillyDancing(); }
            else if (player1Controls.Player.BreakDanceEnding1.triggered) { ResetStates(); BreakDanceEnding1(); }
            else { Uprock(); }
        }
        else if (player1Controls.Player.RockDancing.triggered)
        {
            if (player1Controls.Player.Flair.triggered) { ResetStates(); Flair(); }
            else if (player1Controls.Player.HipHopDancing.triggered) { ResetStates(); HipHopDancing(); }
            else if (player1Controls.Player.SillyDancing.triggered) { ResetStates(); SillyDancing(); }
            else if (player1Controls.Player.BreakDanceEnding1.triggered) { ResetStates(); BreakDanceEnding1(); }
            else { RockDancing(); }

        }
        
        //LOOPING STATES

        else if (isFlair) { Flair(); }
        else if (isHipHopDancing) { HipHopDancing(); }
        else if (isSillyDancing) { SillyDancing(); }
        else if (isBreakDanceEnding1) { BreakDanceEnding1(); }

        else if (isBreakDance1990) { BreakDance1990(); } // 2023 called BreakDanceEnding1() here
        else if (isHeadspin) { Headspin(); }
        else if (isUprock) { Uprock(); }
        else if (isRockDancing) { RockDancing(); }

        else if (isDancing) { Dance(); }
        else if (isDanceReady) { DanceReady(); }
        
        

        // BASIC DANCE MOVES: boost + one combo step, only if the move actually started (Y held,
        // standing). 2026 rewrite of four copy-pasted 2023 blocks that added boost and a combo step
        // even when the move was blocked, capped Rock Dancing at 4, and at max combo did `return`,
        // which skipped the rest of this Update.
        if (player1Controls.Player.BreakDance1990.triggered) { BreakDance1990(); RewardDanceMove(isBreakDance1990, "BreakDance1990"); }
        if (player1Controls.Player.Headspin.triggered) { Headspin(); RewardDanceMove(isHeadspin, "Headspin"); }
        if (player1Controls.Player.Uprock.triggered) { Uprock(); RewardDanceMove(isUprock, "Uprock"); }
        if (player1Controls.Player.RockDancing.triggered) { RockDancing(); RewardDanceMove(isRockDancing, "RockDancing"); }

        // 2023: boost dropped by boostJumpCost every frame while jumping. Boost now drains per
        // second only while Jump is held for lift (HandleJumpAndBoost).


        if (m_floorCollider.isStanding) //IF FLOOR COLLIDER OBJECT IS COLLIDING WITH OBJECT TAGGED "Floor"...
        {

            if (!landingSoundPlayed)
            {
                Toolbox.instance.m_audio.landing.PlayOneShot(Toolbox.instance.m_audio.audioClip1);
                landingSoundPlayed = true;
            }

            if (isFallingIdle) { StopFallingIdle(); }
            if (isJumping) { StopJumping(); }
            Debug.Log("isStanding = true;");


            //WALKING
            // 2026: only START walking here. Move.triggered fires on every stick value change, so the
            // 2023 version re-ran Walk() almost every frame while steering, which cancelled the sprint.
            // Also starts if the stick was already held when he landed (Move.triggered only fires on
            // a change, so a steady stick never started walking and he looked unresponsive).
            if (new Vector2(move.x, move.y).sqrMagnitude > 0.04f && !isWalking && !isSprinting && DanceTier == 0)
            {
                Walk();
                isWalking = true;
                isIdle = false;
                isFallingIdle = false;
                isJumping = false;
                isSprinting = false;
                isDancing = false;
                Debug.Log("Started Walking.");
            }
            //IDLE
            if (
                
            !player1Controls.Player.Move.inProgress && !player1Controls.Player.Look.inProgress && !isJumping && !isFallingIdle && !isDancing && !isSprinting
             && !isDanceReady && !player1Controls.Player.BreakDance1990.inProgress && !player1Controls.Player.Headspin.inProgress && !player1Controls.Player.Uprock.inProgress
             && !player1Controls.Player.RockDancing.inProgress && !player1Controls.Player.Flair.inProgress && !player1Controls.Player.HipHopDancing.inProgress
             && !player1Controls.Player.SillyDancing.inProgress && !player1Controls.Player.BreakDanceEnding1.inProgress && !isFlair && !isHipHopDancing && !isSillyDancing
             && !isBreakDanceEnding1
             // 2026: these four were missing, so their dances were reset to Idle the frame after starting.
             && !isBreakDance1990 && !isHeadspin && !isUprock && !isRockDancing
             && !isDanceReady2 && DanceTier == 0)
            {
                ResetStates();
                Idle();

                Debug.Log("Started Idling.");


            }
            //JUMPING
            if (!isFallingIdle && !isJumping && player1Controls.Player.Jump.triggered)
            {
                ResetStates();
                Jump();
                isJumping = true;
                isFallingIdle = false;
                isIdle = false;
                isWalking = false;
                isSprinting = false;
                isDancing = false;
                Debug.Log("Jumped");
                landingSoundPlayed = false;

            }
            //DANCING
            if (player1Controls.Player.Dance.triggered)
            {
                ResetStates();
                Dance();
                isDancing = true;
                isJumping = false;
                isFallingIdle = false;
                isIdle = false;
                isWalking = false;
                isSprinting = false;
                Debug.Log("Started Dancing");
            }
            //RUNNING
            // 2026: sprint while Run is HELD and he is walking (2023 needed the press to land while
            // already walking, so holding Run first and then moving never sprinted).
            if (isWalking && player1Controls.Player.Run.IsPressed())
            {
                ResetStates();
                Run();
                isSprinting = true;
                isDancing = false;
                isJumping = false;
                isFallingIdle = false;
                isIdle = false;
                isWalking = false;
                Debug.Log("Started Running");
            }
            if (player1Controls.Player.DanceReady.triggered && player1Controls.Player.Dance.inProgress)
            {
                
                DanceReady();
                isDanceReady = true;
                isJumping = false;
                isFallingIdle = false;
                isIdle = false;
                isWalking = false;
                isDancing = false;
                isRockDancing = false;
                isBreakDance1990 = false;
                isHeadspin = false;
                isUprock = false;
            }

        }
        //FALLING IDLE
        else if (!m_floorCollider.isStanding && !player1Controls.Player.Jump.inProgress)
        {
            ResetStates();
            FallingIdle();
            isFallingIdle = true;
            isIdle = false;
            isWalking = false;
            isJumping = false;
            isSprinting = false;
            isDancing = false;
            Debug.Log("Started Falling Idle.");
            landingSoundPlayed = false;
        }


    }


    void StopDancing()
    {

        player1Animator.SetBool("isDancing", false);
        isDancing = false;
    }
    void Run()
    {
        if (DanceTier >= 1) return;  // B is the advanced-dance modifier while Y is held
        ResetStates();
        playerSpeed = runSpeed;
        player1Animator.SetBool("isRunning", true);
        isSprinting = true;
    }
    void CancelRun()
    {
        playerSpeed = defaultPlayerSpeed;
        player1Animator.SetBool("isRunning", false);
        isSprinting = false;
    }
    public void FallingIdle()
    {
        ResetStates();
        isFallingIdle = true;
        player1Animator.SetBool("isFallingIdle", true);

    }
    void StopFallingIdle()
    {
        player1Animator.SetBool("isFallingIdle", false);
        isFallingIdle = false;
    }

    void Walk()
    {
        ResetStates();
        player1Animator.SetBool("isWalking", true);
        isWalking = true;
    }
    void StopWalk()
    {
        player1Animator.SetBool("isWalking", false);
        isWalking = false;
    }
    void Idle()
    {
        ResetStates();
        player1Animator.SetBool("isIdle", true);
        isIdle = true;
    }
    void StopIdle()
    {
        player1Animator.SetBool("isIdle", false);
        isIdle = false;
    }
    void Jump()
    {
        // Free jump, only from a platform. The 2023 version needed boost and teleported the gnome
        // up by jumpHeight.y * jumpSpeed in one frame (and ran twice per press).
        // verticalVelocity > 0 means this press already started a jump this frame.
        if (!m_floorCollider.isStanding || verticalVelocity > 0f) return;

        ResetStates();
        verticalVelocity = baseJumpSpeed;
        isJumping = true;
        LeaveGround(true);
        Toolbox.instance.m_audio.jumping.Play();
        player1Animator.SetBool("isJumping", true);
        Debug.Log("isJumping");
    }



    [Header("Debug")]
    [Tooltip("Shows live input and state values in the top-left corner while playing.")]
    [SerializeField] bool showDebugReadout = true;

    void OnGUI()
    {
        if (!showDebugReadout) return;
        var pm = Toolbox.instance != null ? Toolbox.instance.m_playerManager : null;
        string text =
            "move " + move.ToString("0.00") + "   look " + look.ToString("0.00") + "\n" +
            "Move live " + player1Controls.Player.Move.ReadValue<Vector2>().ToString("0.00") +
            "   Look live " + player1Controls.Player.Look.ReadValue<Vector2>().ToString("0.00") + "\n" +
            "Jump held " + player1Controls.Player.Jump.IsPressed() + "   actions enabled " + player1Controls.Player.enabled + "\n" +
            "standing " + (m_floorCollider != null && m_floorCollider.isStanding) + "   fallingIdle " + isFallingIdle +
            "   jumping " + isJumping + "   walking " + isWalking + "\n" +
            "playerSpeed " + playerSpeed + "   fall " + (fallSpeed * gravity).ToString("0.0") +
            "   vertVel " + verticalVelocity.ToString("0.0") + "   boost " + (pm != null ? pm.boost.ToString("0") : "?") + "\n" +
            "pos " + transform.position.ToString("0.0") + "   timeScale " + Time.timeScale;
        GUI.Box(new Rect(10, 10, 560, 110), "");
        GUI.Label(new Rect(18, 14, 550, 105), text);
    }

    void OnEnable()
    {
        player1Controls.Player.Enable();

    }
    void OnDisable()
    {
        player1Controls.Player.Disable();
    }
}
    


