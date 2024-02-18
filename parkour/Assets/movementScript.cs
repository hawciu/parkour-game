using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

public class movementScript : MonoBehaviour
{
    //DEBUG
    [SerializeField]
    bool drawCollisionNormals = false;
    //DEBUG

    [SerializeField]
    InputActionReference move, look, jump;

    playerState state = playerState.NORMAL;

    Vector3 velocity, targetVelocity;
    public GameObject cam;
    Vector2 rotation = Vector2.zero;
    public float speed = 3;
    [SerializeField]
    float mouseSensitivity = 0.05f;

    [SerializeField, Range(0f, 100f)]
    float maxSpeed = 10f;
    [SerializeField, Range(0f, 100f)]
    float maxAcceleration = 100f;
    [SerializeField, Range(0f, 10f)]
    float jumpHeight = 2f;

    Vector3 lastTouchedWallAngle;

    Rigidbody body;
    bool jumpPressed = false;
    bool wallRunningPressed = false;
    bool isWallRunning = false;
    Vector3 wallRunningDirection;
    float wallRunningStart;
    float wallRunningDuration = 2;

    [SerializeField]
    bool canWallJump = false;

    float lastGrounded = 0;
    float lastWalled = 0;

    void Awake()
    {
        body = GetComponent<Rigidbody>();
    }

    // Start is called before the first frame update
    void Start()
    {
    }

    void Update()
    {
        GetCameraInput();
        GetMovementInput();
    }

    void FixedUpdate()
    {
        Move();
    }

    void GetCameraInput()
    {
        Vector2 movementInput = look.action.ReadValue<Vector2>();
        rotation.y += movementInput.x;
        rotation.x += -movementInput.y;
        rotation.x = Mathf.Clamp(rotation.x, -900f, 900f);
        cam.transform.eulerAngles = (Vector2)rotation * mouseSensitivity;
    }

    void GetMovementInput()
    {
        Vector3 movement;
        Vector3 camForward = new Vector3(cam.transform.forward.x, 0 , cam.transform.forward.z).normalized;
        movement = cam.transform.right * move.action.ReadValue<Vector2>().x + camForward * move.action.ReadValue<Vector2>().y;
        targetVelocity = movement * maxSpeed;
    }

    void OnJump()
    {
        jumpPressed = true;
    }

    void OnWallRun()
    {
        wallRunningPressed = true;
    }

    void Move()
    {
        velocity = body.velocity;
        float maxSpeedChange = maxAcceleration * Time.deltaTime;
        if (!isWallRunning && wallRunningPressed)
        {
            print(">>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>wr start");
            wallRunningPressed = false;
            isWallRunning = true;
            canWallJump = true;
            wallRunningDirection = CalculateWallRunDirection();
            wallRunningStart = Time.time;
            body.useGravity = false;
        }

        if (jumpPressed)
        {
            print("jump");
            Jump();
        }

        print("check");
        if (lastWalled + 0.1f < Time.time || wallRunningStart + wallRunningDuration < Time.time) //check if is wallrunning
        {
            print("check false");
            StopWallRunning();
        }

        print("-check");
        if (isWallRunning)
        {
            print("isWallRunning vel: " + velocity);
            targetVelocity = wallRunningDirection * maxSpeed;
            velocity.x = Mathf.MoveTowards(velocity.x, targetVelocity.x, maxSpeedChange);
            velocity.z = Mathf.MoveTowards(velocity.z, targetVelocity.z, maxSpeedChange);
        }
        else
        {
            print("else vel:" + velocity);
            velocity.x = Mathf.MoveTowards(velocity.x, targetVelocity.x, maxSpeedChange);
            velocity.z = Mathf.MoveTowards(velocity.z, targetVelocity.z, maxSpeedChange);
        }
        print("end");
        body.velocity = velocity;
    }

    void Jump()
    {
        jumpPressed = false;
        if (lastGrounded + 0.1f > Time.time)
        {
            velocity.y = Mathf.Sqrt(-2f * Physics.gravity.y * jumpHeight);
        }
        else if (lastWalled + 0.1f > Time.time) //if touching wall or wallrunning - wall jump
        {
            if (!canWallJump) return;
            StopWallRunning();
            canWallJump = false;
            velocity.y = Mathf.Sqrt(-2f * Physics.gravity.y * jumpHeight);
            Debug.DrawRay(transform.position, lastTouchedWallAngle, Color.black, 1000f);
        }
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

            if (90 >= degree && degree >=40) //floor
            {
                StopWallRunning();
                lastGrounded = Time.time;
                canWallJump = true;
                DrawCollisions(contacts[i].point, contacts[i].normal, Color.green, degree);
            }
            else if (40 > degree && degree > 15)  //slide down? no jump
            {
                StopWallRunning();
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
        Debug.DrawRay(transform.position, lastTouchedWallAngle * 2, Color.black, 1000f);
        Debug.DrawRay(transform.position, cam.transform.forward, Color.red, 1000f);
        float currentAngle = Vector3.Angle(-lastTouchedWallAngle, cam.transform.forward);
        float smallestAngle = currentAngle;
        Vector3 right = -new Vector3(lastTouchedWallAngle.z, lastTouchedWallAngle.y, -lastTouchedWallAngle.x);
        Vector3 left = -new Vector3(-lastTouchedWallAngle.z, lastTouchedWallAngle.y, lastTouchedWallAngle.x);
        Debug.DrawRay(transform.position, -lastTouchedWallAngle * 0.2f, Color.white, 1000f);
        Debug.DrawRay(transform.position, right * 0.2f, Color.gray, 1000f);
        Debug.DrawRay(transform.position, left * 0.2f, Color.cyan, 1000f);
        //print("wall: " + lastTouchedWallAngle+ " cam: " + cam.transform.forward+ " front: " + -lastTouchedWallAngle+ " right: " + right+ " left: " + left);
        Vector3 wallRunDirection = -lastTouchedWallAngle;
        //vector 90 right
        currentAngle = Vector3.Angle(right, cam.transform.forward);
        if (currentAngle < smallestAngle)
        {
            smallestAngle = currentAngle;
            wallRunDirection = right;
        }
        //Vector3 90 left
        currentAngle = Vector3.Angle(left, cam.transform.forward);
        if (currentAngle < smallestAngle)
        {
            wallRunDirection= left;
            smallestAngle = currentAngle;
        }
        Debug.DrawRay(transform.position, wallRunDirection * 0.1f, Color.green, 1000f);
        return wallRunDirection;
    }
    
    void StopWallRunning()
    {
        isWallRunning = false;
        body.useGravity = true;
    }

    void DrawCollisions(Vector3 start, Vector3 dir, Color color, float degree)
    {
        if (!drawCollisionNormals) return;
        print(degree);
        Debug.DrawRay(start, dir, color, 1000f);
    }

    private void OnCollisionEnter(Collision collision)
    {
        return;
        foreach (var item in collision.contacts)
        {
            Vector3 n = item.normal;
            print(n);
            Vector3 m = new Vector3(item.normal.x, 0, item.normal.z).normalized;
            float gorne = n.x * m.x + n.y * m.y + n.z * m.z;
            float degree = Mathf.Rad2Deg * Mathf.Acos(Mathf.Clamp(gorne, -1, 1));
            if (n.y >= 0)
            {
                Debug.DrawRay(item.point, item.normal, Color.green, 1000f);
            }
            /*
            if (degree == 90)
            {
                Debug.DrawRay(item.point, item.normal, Color.green, 1000f);
            }
            else if (degree == 0)
            {
                Debug.DrawRay(item.point, item.normal, Color.green, 1000f);
            }
            else if (60 >= degree && degree > 0)
            {
                Debug.DrawRay(item.point, item.normal, Color.yellow, 1000f);
            }
            else if (90 > degree && degree > 45) //floor
            {
                Debug.DrawRay(item.point, item.normal, Color.yellow, 1000f);
            }
            else
            {
                print("else");
            }*/
        }
    }
}
