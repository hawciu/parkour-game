using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Timeline;
using UnityEngine.UIElements;
using static UnityEngine.GraphicsBuffer;

public class newMovementScript : MonoBehaviour
{

    public GameObject marker;
    //DEBUG
    [SerializeField]
    bool drawCollisionNormals = false;
    //DEBUG


    [SerializeField]
    playerState state = playerState.NORMAL;

    [SerializeField]
    float mouseSensitivity = 0.1f;

    [SerializeField, Range(0f, 100f)]
    float maxSpeed = 10f;

    [SerializeField, Range(0f, 100f)]
    float maxAcceleration = 100f;

    [SerializeField, Range(0f, 10f)]
    float jumpHeight = 2f;

    [SerializeField]
    InputActionReference move, look;

    [SerializeField]
    float speed = 5;

    Vector2 rotation = Vector2.zero;

    Rigidbody body;

    Vector3 lastTouchedWallAngle;
    Vector3 velocity;

    Vector3 horizontalMovement;
    Vector3 wallRunningDirection;
    Vector3 slidingDirection;

    Quaternion camRotationStart;
    Quaternion camRotationTarget;

    public GameObject cam;
    public GameObject camTarget;
    public GameObject wallRunParticlePivot;
    public ParticleSystem wallRunParticle;
    public GameObject cap;

    bool jumpPressed = false;
    bool wallRunningPressed = false;
    bool slidingPressed = false;
    bool slidingExited = true;

    float camRotationLerp = 2;
    float camFOVLerp = 2;
    float camFOVTarget = 90;
    float camFOVStart;
    float wallRunningStart;
    float slidingStart;

    [SerializeField]
    bool canWallJump = false;

    float lastGrounded = 0;
    float lastWalled = 0;
    float maxSpeedChange;


    void Awake()
    {
        body = GetComponent<Rigidbody>();
    }

    // Start is called before the first frame update
    void Start()
    {
    }

    // Update is called once per frame
    void Update()
    {
        maxSpeedChange = maxAcceleration * Time.deltaTime;
        GetCameraInput();
        UpdateCameraRotation();
        UpdateCameraFoV();
        GetMovementInput();
    }

    private void FixedUpdate()
    {
        CalculateVelocity();
    }

    void CalculateVelocity()
    {
        velocity = body.velocity;
        switch (state)
        {
            case playerState.NORMAL:
                //velocity.x = Mathf.MoveTowards(velocity.x, targetVelocity.x, maxSpeedChange);
                //velocity.z = Mathf.MoveTowards(velocity.z, targetVelocity.z, maxSpeedChange);
                velocity.x = horizontalMovement.x;
                velocity.z = horizontalMovement.z;
                if (!slidingExited)
                {
                    TryExitSliding();
                    break;
                }
                JumpCheck();
                if (SlidingCheck()) break;
                if (WallRunCheck()) break;
                break;

            case playerState.WALLRUNNING:
                WallJumpCheck();
                if (wallRunningStart + 2 < Time.time || lastWalled + 0.1f < Time.time)
                {
                    StopWallRunning();
                    break;
                }
                velocity.x = wallRunningDirection.x;
                velocity.z = wallRunningDirection.z;
                break;

            case playerState.SLIDING:
                if (slidingStart + 1 < Time.time)
                {
                    StopSliding();
                    break;
                }
                velocity.x = slidingDirection.x;
                velocity.z = slidingDirection.z;
                break;
        }
        UpdateBodyVelocity();
        ConsumeInputs();
    }

    private void UpdateBodyVelocity()
    {
        velocity.x = velocity.x * speed;
        velocity.z = velocity.z * speed;
        body.velocity = velocity;
    }

    void SwitchState(playerState newState)
    {
        state = newState;
        switch (state)
        {
            case playerState.NORMAL:
                break;

            case playerState.WALLRUNNING:
                break;

            case playerState.SLIDING:
                break;
        }
    }

    void GetMovementInput()
    {
        Vector3 camForward = new Vector3(camTarget.transform.forward.x, 0, camTarget.transform.forward.z).normalized;
        horizontalMovement = camTarget.transform.right * move.action.ReadValue<Vector2>().x + camForward * move.action.ReadValue<Vector2>().y;
    }

    void GetCameraInput()
    {
        Vector2 movementInput = look.action.ReadValue<Vector2>();
        rotation.y += movementInput.x;
        rotation.x += -movementInput.y;
        rotation.x = Mathf.Clamp(rotation.x, -900f, 900f);
        camTarget.transform.eulerAngles = (Vector2)rotation * mouseSensitivity;
    }

    void OnJump()
    {
        jumpPressed = true;
    }

    void OnWallRun()
    {
        wallRunningPressed = true;
    }

    void OnSlide()
    {
        slidingPressed = true;
    }

    bool JumpCheck()
    {
        if (jumpPressed)
        {
            if (lastGrounded + 0.1f > Time.time)
            {
                Jump();
                return true;
            }
        }
        return false;
    }

    void Jump()
    {
        velocity.y = Mathf.Sqrt(-2f * Physics.gravity.y * jumpHeight);
    }

    bool WallJumpCheck()
    {
        if (canWallJump && jumpPressed && lastWalled + 0.1f > Time.time) //if touching wall or wallrunning - wall jump
        {
            StopWallRunning();
            canWallJump = false;
            velocity.y = Mathf.Sqrt(-2f * Physics.gravity.y * jumpHeight);
            Debug.DrawRay(transform.position, lastTouchedWallAngle, Color.black, 1000f);
            return true;
        }
        return false;
    }

    private void OnCollisionStay(Collision collision)
    {
        AngleCheck(collision);
    }

    void AngleCheck(Collision collision)
    {
        ContactPoint[] contacts = new ContactPoint[collision.contactCount];
        int points = collision.GetContacts(contacts);
        for (int i = 0; i < points; i++)
        {
            Vector3 n = contacts[i].normal;
            Vector3 m = new Vector3(contacts[i].normal.x, 0, contacts[i].normal.z).normalized;
            float angleCos = n.x * m.x + n.y * m.y + n.z * m.z;
            float degree = Mathf.Rad2Deg * Mathf.Acos(Mathf.Clamp(angleCos, -1, 1));

            if (90 >= degree && degree >= 40) //floor
            {
                lastGrounded = Time.time;
                DrawCollisions(contacts[i].point, contacts[i].normal, Color.green, degree);
            }
            else if (40 > degree && degree > 15)  //slide down? no jump
            {
                DrawCollisions(contacts[i].point, contacts[i].normal, Color.red, degree);
            }
            else if (15 >= degree && degree >= 0)  //wall
            {
                lastWalled = Time.time;
                lastTouchedWallAngle = contacts[i].normal;
                DrawCollisions(contacts[i].point, contacts[i].normal, Color.yellow, degree);
            }
        }
    }

    Vector3 CalculateWallRunDirection()
    {
        Vector3 right = -new Vector3(lastTouchedWallAngle.z, lastTouchedWallAngle.y, -lastTouchedWallAngle.x);
        Vector3 left = -new Vector3(-lastTouchedWallAngle.z, lastTouchedWallAngle.y, lastTouchedWallAngle.x);

        //vector 90 right
        float currentAngle = Vector3.Angle(right, camTarget.transform.forward);
        float smallestAngle = currentAngle;
        Vector3 wallRunDirection = right;
        SetCameraRotationTarget(new Vector3(0, 0, -15));

        //Vector3 90 left
        currentAngle = Vector3.Angle(left, camTarget.transform.forward);
        if (currentAngle < smallestAngle)
        {
            smallestAngle = currentAngle;
            wallRunDirection = left;
            SetCameraRotationTarget(new Vector3(0, 0, 15));
        }

        Debug.DrawRay(transform.position, lastTouchedWallAngle * 2, Color.black, 1000f);
        Debug.DrawRay(transform.position, camTarget.transform.forward, Color.red, 1000f);
        Debug.DrawRay(transform.position, -lastTouchedWallAngle * 0.2f, Color.white, 1000f);
        Debug.DrawRay(transform.position, right * 0.2f, Color.gray, 1000f);
        Debug.DrawRay(transform.position, left * 0.2f, Color.cyan, 1000f);


        Debug.DrawRay(transform.position, wallRunDirection * 10f, Color.green, 1000f);

        //wallRunDirection.y = Mathf.Sqrt(-2f * Physics.gravity.y * jumpHeight);
        return wallRunDirection;
    }

    void DrawCollisions(Vector3 start, Vector3 dir, Color color, float degree)
    {
        if (!drawCollisionNormals) return;
        Debug.DrawRay(start, dir, color, 1000f);
    }

    void StartWallRunning()
    {
        SetCameraFOVTarget(100);
        canWallJump = true;
        body.useGravity = false;
        wallRunningStart = Time.time;
        wallRunningDirection = CalculateWallRunDirection();
        velocity = wallRunningDirection;
        ActivateWallRunParticle(wallRunningDirection);
        UpdateBodyVelocity();
        SwitchState(playerState.WALLRUNNING);
    }

    void StopWallRunning()
    {
        SetCameraFOVTarget(90);
        SetCameraRotationTarget(Vector3.zero);
        wallRunParticle.Stop();
        body.useGravity = true;
        SwitchState(playerState.NORMAL);
    }

    void ActivateWallRunParticle(Vector3 dir)
    {
        wallRunParticlePivot.transform.rotation = Quaternion.Euler(lastTouchedWallAngle);
        /*
        if (dir.z < 0)
        {
            wallRunParticlePivot.transform.rotation = Quaternion.Euler(new Vector3(0, 180, 0));
        }
        else if (dir.z > 0)
        {
            wallRunParticlePivot.transform.rotation = Quaternion.Euler(new Vector3(0, 0, 0));
        }
        else
        {
            wallRunParticlePivot.transform.rotation = Quaternion.Euler(new Vector3(-90, 0, 0));
        }*/
        wallRunParticle.Play();
    }

    bool WallRunCheck()
    {
        if (wallRunningPressed && lastWalled + 0.1f > Time.time)
        {
            StartWallRunning();
            return true;
        }
        return false;
    }

    bool SlidingCheck()
    {
        if (slidingPressed && lastGrounded + 0.1f > Time.time)
        {
            StartSliding();
            return true;
        }
        return false;
    }

    void StartSliding()
    {
        SetCameraFOVTarget(100);
        slidingExited = false;
        Vector3 camForward = new Vector3(camTarget.transform.forward.x, 0, camTarget.transform.forward.z).normalized;
        slidingDirection = camTarget.transform.right * move.action.ReadValue<Vector2>().x + camForward * move.action.ReadValue<Vector2>().y;
        slidingStart = Time.time;
        cap.transform.localScale = new Vector3(1, 0.5f, 1);
        cap.transform.localPosition = new Vector3(0, 0.5f, 0);
        SwitchState(playerState.SLIDING);
    }

    void StopSliding()
    {
        SetCameraFOVTarget(90);
        SwitchState(playerState.NORMAL);
    }

    void TryExitSliding()
    {
        LayerMask mask = ~8;
        Collider[] colliders = Physics.OverlapCapsule(transform.position + new Vector3(0, 1.5f, 0), transform.position + new Vector3(0, 0.51f, 0), 0.5f, mask);
        foreach(Collider collider in colliders)
        {
            print(collider + " " + collider.gameObject);
        }
        if (colliders.Length == 0)
        {
            cap.transform.localScale = new Vector3(1, 1, 1);
            cap.transform.localPosition = new Vector3(0, 1, 0);
            slidingExited = true;
        }
    }

    void ConsumeInputs()
    {
        jumpPressed = false;
        wallRunningPressed = false;
        slidingPressed = false;
    }

    void SetCameraRotationTarget(Vector3 target)
    {
        camRotationLerp = 0;
        camRotationStart = cam.transform.localRotation;
        camRotationTarget = Quaternion.Euler(target);
    }

    void UpdateCameraRotation()
    {
        camRotationLerp += Time.deltaTime * 5;
        if (camRotationLerp > 1) return;
        cam.transform.localRotation = Quaternion.Lerp(camRotationStart, camRotationTarget, camRotationLerp);
    }
    
    void SetCameraFOVTarget(float target)
    {
        camFOVLerp = 0;
        camFOVStart = cam.GetComponent<Camera>().fieldOfView;
        camFOVTarget = target;
    }

    void UpdateCameraFoV()
    {
        camFOVLerp += Time.deltaTime * 5;
        if (camFOVLerp > 1) return;
        cam.GetComponent<Camera>().fieldOfView = Mathf.Lerp(camFOVStart, camFOVTarget, camFOVLerp);
    }
}
